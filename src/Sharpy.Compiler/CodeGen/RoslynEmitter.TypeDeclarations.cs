using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Type declarations (functions, classes, structs, interfaces, enums)
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>
    /// Specifies whether decorator-to-modifier generation is for a member (function/method)
    /// or a type (class/struct/interface).
    /// </summary>
    private enum ModifierContext
    {
        /// <summary>Member-level: supports Virtual, Override, New, Readonly, Extern; adds mandatory static default.</summary>
        Member,
        /// <summary>Type-level: supports only Abstract, Final/Sealed, Static; no mandatory static default.</summary>
        Type
    }
    private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        // Clear declared variables and version tracking for new function scope
        ResetMethodScope(func);

        // Pre-scan the function body to collect all variable names that will be declared.
        // This enables us to avoid generating versioned names (x_1, x_2) that collide
        // with user-declared variables.
        CollectSourceVariableNames(func.Body);

        // Transform name using NameCasing, which honours the backtick escape — the reference side
        // already did, so resolving the declaration without the flag made `def `str`` declare `Str`
        // and its call site emit `str()` (CS0103, #1241).
        // Special case: only convert "main" to "Main" if this is the entry point file
        var mangledName = func.Name == "main" && !_context.IsEntryPoint
            ? "MainFunc"  // Rename to avoid C# entry point conflict in non-entry files
            : NameCasing.ResolveMethod(func.Name, func.IsNameBacktickEscaped);

        // Check if this function is a generator and/or async
        using var _ = SetGeneratorScope(_context.Ir?.IsGenerator(func) == true);
        using var _async = SetAsyncScope(func.IsAsync);

        // Track @test context so assert statements in the body emit xUnit assertions
        // (instead of System.Diagnostics.Debug.Assert). Matches @test and its
        // sub-decorators (@test.parametrize, @test.skip, @test.skip_if).
        bool isTestFunction = func.Decorators.Any(IsTestDecorator);
        bool savedIsInTestFunction = _isInTestFunction;
        if (isTestFunction)
        {
            _isInTestFunction = true;
        }

        // Determine return type from annotation or infer void
        TypeSyntax returnType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // For generators, wrap the annotated return type T in IEnumerable<T> or IAsyncEnumerable<T>
        bool isAsync = func.IsAsync;
        if (_isCurrentMethodGenerator)
        {
            returnType = isAsync ? WrapInIAsyncEnumerable(returnType) : WrapInIEnumerable(returnType);
        }
        else if (isAsync)
        {
            // For non-generator async functions, wrap return type in Task<T> or Task.
            // An explicit `-> None` annotation maps to `void`, which must become bare
            // `Task` (not `Task<void>` — that is invalid C#).
            if (func.ReturnType != null && !IsVoidType(returnType))
            {
                returnType = WrapInTask(returnType);
            }
            else
            {
                returnType = TaskType();
            }
        }

        // Process decorators to determine modifiers
        var modifiers = GenerateModifiersFromDecorators(func.Decorators, func.Name);

        // Reorder parameters for C# compliance (required before optional, params last)
        var orderedParams = ReorderParametersForCSharp(func.Parameters);

        // Generate parameters with type annotations
        var parameters = orderedParams
            .Select(GenerateParameter)
            .ToArray();

        // Track parameters as declared variables
        foreach (var param in func.Parameters)
        {
            var paramName = ParameterCSharpName(param);
            if (param.IsLateBound)
            {
                // The C# parameter is named `y__lb`; the preamble local is named `y`
                _declaredVariables.Add(paramName + LateBoundSuffix);
                _declaredVariables.Add(paramName);
            }
            else
            {
                _declaredVariables.Add(paramName);
            }
            // Also track in version map so assignments to parameters work correctly
            var baseName = ParameterCSharpName(param);
            RegisterLocalSlot(baseName, param.Name);
        }

        // Generate method body, prepending late-bound default locals
        var preamble = GenerateLateBoundPreamble(func.Parameters);
        var body = Block(preamble.Concat(GenerateSuite(func.Body)));

        var method = MethodDeclaration(returnType, EscapedIdentifier(mangledName))
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add C# attributes from unknown decorators
        var attributes = GenerateAttributeListsFromDecorators(func.Decorators);
        if (attributes.Count > 0)
        {
            method = method.WithAttributeLists(attributes);
        }

        if (isAsync)
        {
            method = method.AddModifiers(Token(SyntaxKind.AsyncKeyword));
        }

        // Add type parameters if generic
        if (func.TypeParameters.Length > 0)
        {
            var typeParams = func.TypeParameters
                .Select(GenerateMethodTypeParameterSyntax)
                .ToArray();
            method = method
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(func.TypeParameters));
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            method = method.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        _isInTestFunction = savedIsInTestFunction;

        return method;
    }

    private ParameterSyntax GenerateParameter(Parameter param)
    {
        var paramName = ParameterCSharpName(param);

        // Late-bound default (PEP 671): emit as nullable sentinel parameter.
        // E.g., `y: int => x + 1` becomes `int? y__lb = null` in the C# signature.
        // The actual local `y` is emitted in the method body preamble via GenerateLateBoundPreamble.
        if (param.IsLateBound)
        {
            TypeSyntax baseType = param.Type != null
                ? _typeMapper.MapType(param.Type)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));

            // Value types need Nullable<T> wrapper; reference/optional types already allow null.
            TypeSyntax lbType = IsValueTypeAnnotation(param.Type)
                ? NullableType(baseType)
                : baseType;

            return Parameter(EscapedIdentifier(paramName + LateBoundSuffix))
                .WithType(lbType)
                .WithDefault(EqualsValueClause(
                    LiteralExpression(SyntaxKind.NullLiteralExpression)));
        }

        // Get parameter type from annotation or default to object
        TypeSyntax paramType = param.Type != null
            ? _typeMapper.MapType(param.Type)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // For variadic parameters (*args), wrap the element type in an array
        if (param.IsVariadic)
        {
            paramType = VariadicArrayType(paramType);
        }

        var parameter = Parameter(EscapedIdentifier(paramName))
            .WithType(paramType);

        // Add ref/out/in modifier
        if (param.Modifier != ParameterModifier.None)
        {
            var modKind = param.Modifier switch
            {
                ParameterModifier.Ref => SyntaxKind.RefKeyword,
                ParameterModifier.Out => SyntaxKind.OutKeyword,
                ParameterModifier.In => SyntaxKind.InKeyword,
                _ => SyntaxKind.None
            };
            if (modKind != SyntaxKind.None)
                parameter = parameter.WithModifiers(TokenList(Token(modKind)));
        }

        // For variadic parameters, add the 'params' modifier
        if (param.IsVariadic)
        {
            parameter = parameter.WithModifiers(TokenList(Token(SyntaxKind.ParamsKeyword)));
        }

        // Add default value if present
        if (param.DefaultValue != null)
        {
            ExpressionSyntax defaultExpr;
            // None or None() as default param → default (Optional<T> is a struct, default = None)
            if ((param.DefaultValue is NoneLiteral
                || (param.DefaultValue is FunctionCall { Function: NoneLiteral } noneCall
                    && noneCall.Arguments.Length == 0))
                && param.Type is { IsOptional: true })
            {
                defaultExpr = LiteralExpression(SyntaxKind.DefaultLiteralExpression);
            }
            else
            {
                defaultExpr = GenerateExpression(param.DefaultValue);
            }
            parameter = parameter.WithDefault(EqualsValueClause(defaultExpr));
        }

        return parameter;
    }

    private const string LateBoundSuffix = "__lb";

    /// <summary>
    /// Returns true when a type annotation refers to a .NET value type that requires
    /// Nullable&lt;T&gt; wrapping to accept null as a sentinel in a C# parameter default.
    /// </summary>
    private static bool IsValueTypeAnnotation(Parser.Ast.TypeAnnotation? type)
    {
        if (type == null || type.IsOptional || type.IsCSharpNullable)
            return false;
        return type.Name is
            BuiltinNames.Int or BuiltinNames.Bool or
            BuiltinNames.Long or BuiltinNames.Float or
            BuiltinNames.Float32 or BuiltinNames.Double or
            BuiltinNames.Float64 or BuiltinNames.Decimal;
    }

    /// <summary>
    /// Generates the late-bound default preamble for a function body.
    /// For each late-bound parameter `y`, emits: <c>var y = y__lb ?? &lt;default_expr&gt;;</c>
    /// This shadows the C# sentinel parameter with a local of the correct non-nullable type.
    /// </summary>
    private IEnumerable<StatementSyntax> GenerateLateBoundPreamble(
        IReadOnlyList<Parameter> parameters)
    {
        foreach (var param in parameters)
        {
            if (!param.IsLateBound || param.DefaultValue == null)
                continue;

            var paramName = ParameterCSharpName(param);
            var lbParamName = paramName + LateBoundSuffix;
            var defaultExpr = GenerateExpression(param.DefaultValue);

            yield return LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"),
                    SingletonSeparatedList(
                        VariableDeclarator(EscapedIdentifier(paramName))
                            .WithInitializer(EqualsValueClause(
                                BinaryExpression(
                                    SyntaxKind.CoalesceExpression,
                                    IdentifierName(lbParamName),
                                    defaultExpr))))));
        }
    }

    /// <summary>
    /// Convenience overload for member-level modifier generation (functions/methods).
    /// When <paramref name="memberName"/> is provided, underscore-prefixed names get
    /// reduced visibility (internal for _name, private for __name) instead of public.
    /// </summary>
    private SyntaxTokenList GenerateModifiersFromDecorators(IReadOnlyList<Decorator> decorators, string? memberName = null)
    {
        return GenerateModifiersFromDecorators(decorators, ModifierContext.Member, memberName);
    }

    /// <summary>
    /// Generates C# modifier tokens from Sharpy decorators. The <paramref name="context"/> parameter
    /// controls which non-access modifiers are recognized and whether a mandatory static default is added.
    /// </summary>
    private SyntaxTokenList GenerateModifiersFromDecorators(IReadOnlyList<Decorator> decorators, ModifierContext context, string? memberName = null)
    {
        var tokens = new List<SyntaxToken>();

        // Check for access modifiers (identical for both member and type contexts)
        bool hasAccessModifier = false;
        foreach (var decorator in decorators)
        {
            if (decorator.IsBracketAttribute)
                continue;
            switch (decorator.Name)
            {
                case DecoratorNames.Private:
                    tokens.Add(Token(SyntaxKind.PrivateKeyword));
                    hasAccessModifier = true;
                    break;
                case DecoratorNames.Protected:
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    hasAccessModifier = true;
                    break;
                case DecoratorNames.Internal:
                    tokens.Add(Token(SyntaxKind.InternalKeyword));
                    hasAccessModifier = true;
                    break;
                case DecoratorNames.Public:
                    tokens.Add(Token(SyntaxKind.PublicKeyword));
                    hasAccessModifier = true;
                    break;
            }
        }

        if (!hasAccessModifier)
        {
            if (memberName != null)
            {
                tokens.Add(Token(GetModuleLevelAccessModifier(memberName)));
            }
            else
            {
                tokens.Add(Token(SyntaxKind.PublicKeyword));
            }
        }

        // Check for other modifiers (context-dependent)
        foreach (var decorator in decorators)
        {
            if (decorator.IsBracketAttribute)
                continue;
            switch (decorator.Name)
            {
                case DecoratorNames.Static:
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
                case DecoratorNames.Abstract:
                    tokens.Add(Token(SyntaxKind.AbstractKeyword));
                    break;
                case "sealed" when context == ModifierContext.Type:
                case DecoratorNames.Final:
                    tokens.Add(Token(SyntaxKind.SealedKeyword));
                    break;
                case DecoratorNames.Virtual when context == ModifierContext.Member:
                    tokens.Add(Token(SyntaxKind.VirtualKeyword));
                    break;
                case DecoratorNames.Override when context == ModifierContext.Member:
                    tokens.Add(Token(SyntaxKind.OverrideKeyword));
                    break;
            }
        }

        // For module-level functions (Member context), add static modifier if not already present
        // and if it's not a method (we'll handle this differently in classes)
        if (context == ModifierContext.Member &&
            !tokens.Any(t => t.IsKind(SyntaxKind.StaticKeyword) ||
                            t.IsKind(SyntaxKind.AbstractKeyword) ||
                            t.IsKind(SyntaxKind.VirtualKeyword) ||
                            t.IsKind(SyntaxKind.OverrideKeyword)))
        {
            tokens.Add(Token(SyntaxKind.StaticKeyword));
        }

        return TokenList(tokens);
    }

    private SyntaxTriviaList GenerateXmlDocComment(string docString)
    {
        // Convert Python docstring to C# XML documentation
        var lines = docString.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        var triviaList = new List<SyntaxTrivia>
        {
            Comment("/// <summary>"),
            EndOfLine("\n")
        };

        triviaList.AddRange(lines
            .Select(line => line.Trim())
            .Where(trimmedLine => !string.IsNullOrEmpty(trimmedLine))
            .SelectMany(trimmedLine => new[]
            {
                Comment($"/// {trimmedLine}"),
                EndOfLine("\n")
            }));

        triviaList.Add(Comment("/// </summary>"));
        triviaList.Add(EndOfLine("\n"));

        return TriviaList(triviaList);
    }

    #region Class, Struct, Interface, and Enum Generation

    private ClassDeclarationSyntax GenerateClassDeclaration(ClassDef classDef)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        // Note: Class type detection is now done via SymbolTable lookup during expression generation.
        // The _classNames tracking set was used for instantiation detection but is no longer needed
        // since the symbol table is populated during semantic analysis.

        // Transform class name
        var className = NameCasing.ResolveType(classDef.Name, classDef.IsNameBacktickEscaped);

        // Process decorators to determine modifiers
        var modifiers = GenerateModifiersFromDecorators(classDef.Decorators, ModifierContext.Type);

        // Detect inheritance from unittest.TestCase. TestCase is a marker base class
        // used to drive xUnit lifecycle synthesis (constructor from setup, Dispose from
        // teardown). It must NOT appear in the emitted C# base list.
        bool isTestCase = classDef.BaseClasses.Any(bc =>
            bc.Name == "TestCase" || bc.Name == "unittest.TestCase");

        bool hasSetup = false;
        bool hasTeardown = false;
        if (isTestCase)
        {
            hasSetup = classDef.Body.OfType<FunctionDef>().Any(f => f.Name == "setup");
            hasTeardown = classDef.Body.OfType<FunctionDef>().Any(f => f.Name == "teardown");

            // Force public visibility — xUnit requires test classes be public.
            if (!modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            {
                modifiers = TokenList(new[] { Token(SyntaxKind.PublicKeyword) }
                    .Concat(modifiers.Where(m =>
                        !m.IsKind(SyntaxKind.PrivateKeyword)
                        && !m.IsKind(SyntaxKind.ProtectedKeyword)
                        && !m.IsKind(SyntaxKind.InternalKeyword))));
            }
        }

        // Create class declaration
        var classDecl = ClassDeclaration(EscapedIdentifier(className))
            .WithModifiers(modifiers);

        // Add C# attributes from unknown decorators
        var classAttributes = GenerateAttributeListsFromDecorators(classDef.Decorators);
        if (classAttributes.Count > 0)
        {
            classDecl = classDecl.WithAttributeLists(classAttributes);
        }

        // Add type parameters if generic
        if (classDef.TypeParameters.Length > 0)
        {
            var typeParams = classDef.TypeParameters
                .Select(GenerateTypeParameterSyntax)
                .ToArray();
            classDecl = classDecl
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(classDef.TypeParameters));
        }

        // Add base class and interfaces (including synthesized protocol interfaces)
        {
            var baseClassesForEmit = isTestCase
                ? classDef.BaseClasses
                    .Where(bc => bc.Name != "TestCase" && bc.Name != "unittest.TestCase")
                    .ToList()
                : (IReadOnlyList<TypeAnnotation>)classDef.BaseClasses;

            var baseTypes = baseClassesForEmit
                .Select(bc => (BaseTypeSyntax)SimpleBaseType(_typeMapper.MapType(bc)))
                .ToList();

            // Synthesize protocol interfaces from dunder methods
            var synthesizedInterfaces = CollectSynthesizedInterfaces(classDef.Body, classDef.BaseClasses, className, classDef.Name);
            baseTypes.AddRange(synthesizedInterfaces);

            // If TestCase subclass has teardown(), add System.IDisposable so the
            // synthesized Dispose() method satisfies xUnit's per-test cleanup contract.
            if (isTestCase && hasTeardown)
            {
                baseTypes.Add(SimpleBaseType(MakeGlobalQualifiedName("System", "IDisposable")));
            }

            if (baseTypes.Count > 0)
            {
                classDecl = classDecl.WithBaseList(BaseList(SeparatedList(baseTypes)));
            }
        }

        // Track TestCase context so GenerateClassMethod can adjust setup/teardown visibility.
        var wasInTestCaseClass = _isInTestCaseClass;
        _isInTestCaseClass = isTestCase;

        // Generate class members from body
        var members = GenerateClassMembers(classDef.Body, className, classDef.Name);

        _isInTestCaseClass = wasInTestCaseClass;

        // Synthesize xUnit-compatible constructor (from setup) and Dispose (from teardown).
        if (isTestCase)
        {
            if (hasSetup)
            {
                // public ClassName() { Setup(); }
                var ctor = ConstructorDeclaration(EscapedIdentifier(className))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(ParameterList())
                    .WithBody(Block(ExpressionStatement(
                        InvocationExpression(IdentifierName("Setup")))));
                members = new List<MemberDeclarationSyntax> { ctor }.Concat(members).ToList();
            }

            if (hasTeardown)
            {
                // public void Dispose() { Teardown(); }
                var dispose = MethodDeclaration(
                        PredefinedType(Token(SyntaxKind.VoidKeyword)),
                        Identifier("Dispose"))
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithParameterList(ParameterList())
                    .WithBody(Block(ExpressionStatement(
                        InvocationExpression(IdentifierName("Teardown")))));
                members.Add(dispose);
            }
        }

        // For abstract classes implementing interfaces, generate abstract stubs for missing methods
        var classTypeSymbol = _context.LookupSymbol(classDef.Name) as TypeSymbol;
        if (classTypeSymbol?.IsAbstract == true && classDef.BaseClasses.Length > 0)
        {
            var interfaceMethods = CollectInterfaceMethodSymbols(classDef.BaseClasses);
            var definedMethods = GetDefinedMethodNames(classDef.Body);

            var stubMembers = new List<MemberDeclarationSyntax>();

            foreach (var interfaceMethod in interfaceMethods)
            {
                // Skip if method is already defined in the class
                if (definedMethods.Contains(interfaceMethod.Name))
                    continue;

                // Generate abstract stub from semantic model
                var stub = GenerateAbstractMethodStub(interfaceMethod);
                stubMembers.Add(stub);
            }

            // Add stubs to members list
            if (stubMembers.Count > 0)
            {
                members = members.Concat(stubMembers).ToList();
            }
        }

        // `Self` in an interface member names the interface; in the implementing class it names
        // the class — the two C# signatures cannot bind directly (#1285).
        var selfBridges = GenerateSelfInterfaceBridges(classDef, classTypeSymbol);
        if (selfBridges.Count > 0)
        {
            members = members.Concat(selfBridges).ToList();
        }

        // NOTE: Default interface methods are handled at the call site by
        // TryGetDefaultMethodInterface() in RoslynEmitter.Expressions.Access.cs,
        // which emits ((IInterface)obj).Method() casts. Forwarding stubs were removed
        // because they cause infinite recursion in C# (the stub becomes the most-derived
        // implementation, so ((IInterface)this).Method() dispatches back to the stub).

        classDecl = classDecl.WithMembers(List(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(classDef.DocString))
        {
            classDecl = classDecl.WithLeadingTrivia(GenerateXmlDocComment(classDef.DocString));
        }

        return classDecl;
    }

    /// <summary>
    /// Collects interface method definitions from interfaces that a class implements.
    /// Supports both same-module interfaces (via AST) and cross-module interfaces (via SymbolTable).
    /// Returns FunctionSymbol instances for uniform handling.
    /// </summary>
    private List<FunctionSymbol> CollectInterfaceMethodSymbols(IReadOnlyList<TypeAnnotation> baseTypes)
    {
        var result = new List<FunctionSymbol>();
        var visited = new HashSet<string>();
        var seenMethods = new HashSet<string>();

        void CollectFromInterfaceSymbol(TypeSymbol interfaceSymbol)
        {
            if (visited.Contains(interfaceSymbol.Name))
                return;
            visited.Add(interfaceSymbol.Name);

            // Collect methods from this interface's symbol
            foreach (var method in interfaceSymbol.Methods)
            {
                if (seenMethods.Contains(method.Name))
                    continue;
                seenMethods.Add(method.Name);
                result.Add(method);
            }

            // Recursively collect from base interfaces
            foreach (var baseInterface in interfaceSymbol.Interfaces)
            {
                if (baseInterface.Definition?.TypeKind == Semantic.TypeKind.Interface)
                {
                    CollectFromInterfaceSymbol(baseInterface.Definition);
                }
            }
        }

        void CollectFromInterfaceAst(string interfaceName)
        {
            if (visited.Contains(interfaceName))
                return;
            visited.Add(interfaceName);

            if (!_interfaceDefinitions.TryGetValue(interfaceName, out var interfaceDef))
                return;

            // Collect methods from this interface's AST
            foreach (var stmt in interfaceDef.Body)
            {
                if (stmt is FunctionDef funcDef)
                {
                    if (seenMethods.Contains(funcDef.Name))
                        continue;
                    seenMethods.Add(funcDef.Name);

                    // Look up the FunctionSymbol from the SymbolTable for this method
                    var typeSymbol = _context.SymbolTable.LookupType(interfaceName);
                    var methodSymbol = typeSymbol?.Methods.FirstOrDefault(m => m.Name == funcDef.Name);
                    if (methodSymbol != null)
                    {
                        result.Add(methodSymbol);
                    }
                    else
                    {
                        _context.Diagnostics.AddError(
                            $"Cannot resolve interface method '{funcDef.Name}' from interface '{interfaceName}' for abstract stub generation",
                            funcDef.LineStart, funcDef.ColumnStart,
                            code: DiagnosticCodes.CodeGen.EmitError);
                    }
                }
            }

            // Recursively collect from base interfaces, dispatching between
            // same-module (AST) and cross-module (SymbolTable) paths
            foreach (var baseInterface in interfaceDef.BaseInterfaces)
            {
                var baseName = baseInterface.Name;
                if (!string.IsNullOrEmpty(baseName))
                {
                    if (_interfaceDefinitions.ContainsKey(baseName))
                    {
                        CollectFromInterfaceAst(baseName);
                    }
                    else
                    {
                        var baseSymbol = _context.SymbolTable.LookupType(baseName);
                        if (baseSymbol?.TypeKind == Semantic.TypeKind.Interface)
                        {
                            CollectFromInterfaceSymbol(baseSymbol);
                        }
                    }
                }
            }
        }

        foreach (var baseType in baseTypes)
        {
            var typeName = baseType.Name;
            if (string.IsNullOrEmpty(typeName))
                continue;

            // Try same-module AST first
            if (_interfaceDefinitions.ContainsKey(typeName))
            {
                CollectFromInterfaceAst(typeName);
            }
            else
            {
                // Fall back to SymbolTable for cross-module interfaces
                var typeSymbol = _context.SymbolTable.LookupType(typeName);
                if (typeSymbol?.TypeKind == Semantic.TypeKind.Interface)
                {
                    CollectFromInterfaceSymbol(typeSymbol);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the set of method names that are defined in the class body.
    /// </summary>
    private HashSet<string> GetDefinedMethodNames(IReadOnlyList<Statement> classBody)
    {
        var defined = new HashSet<string>();

        foreach (var stmt in classBody)
        {
            if (stmt is FunctionDef func)
            {
                defined.Add(func.Name);
            }
        }

        return defined;
    }

    /// <summary>
    /// Computes synthesized interfaces using SynthesisAnalyzer (the single source of truth)
    /// and converts them to Roslyn BaseTypeSyntax entries for class/struct declarations.
    /// Avoids duplicates if the user already explicitly listed the interface.
    /// </summary>
    private List<BaseTypeSyntax> CollectSynthesizedInterfaces(
        IReadOnlyList<Statement> body,
        IReadOnlyList<TypeAnnotation> explicitBaseClasses,
        string className,
        string originalTypeName)
    {
        var result = new List<BaseTypeSyntax>();
        var explicitNames = new HashSet<string>(explicitBaseClasses.Select(bc => bc.Name));

        // Look up the TypeSymbol to use SynthesisAnalyzer
        var typeSymbol = _context.LookupSymbol(originalTypeName) as TypeSymbol;
        if (typeSymbol == null)
            return result;

        var synthesized = SynthesisAnalyzer.ComputeSynthesizedInterfaces(typeSymbol);

        // Find AST nodes for diagnostic line/column reporting
        var dunderFuncs = new Dictionary<string, FunctionDef>();
        foreach (var stmt in body)
        {
            if (stmt is FunctionDef fd && DunderMapping.IsDunderMethod(fd.Name) && !dunderFuncs.ContainsKey(fd.Name))
                dunderFuncs[fd.Name] = fd;
        }

        foreach (var info in synthesized)
        {
            // Skip if user already explicitly listed this interface
            if (explicitNames.Contains(info.InterfaceName))
                continue;

            var baseType = ConvertSynthesizedInterfaceToBaseType(info);
            result.Add(baseType);
            explicitNames.Add(info.InterfaceName);

            // Emit SPY1001 info diagnostic
            var displayName = info.TypeArgs.Length > 0
                ? $"{info.InterfaceName}<{string.Join(", ", info.TypeArgs.Select(t => t.GetDisplayName()))}>"
                : info.InterfaceName;
            var qualifiedName = info.Namespace.Length > 0
                ? $"{info.Namespace}.{displayName}"
                : displayName;

            dunderFuncs.TryGetValue(info.TriggeringDunder, out var triggeringFunc);
            _context.AddInfo(
                $"Type '{className}' implicitly implements '{qualifiedName}' via '{info.TriggeringDunder}'.",
                DiagnosticCodes.Info.ImplicitInterfaceSynthesis,
                triggeringFunc?.LineStart ?? 0,
                triggeringFunc?.ColumnStart ?? 0);
        }

        return result;
    }

    /// <summary>
    /// Converts a SynthesizedInterfaceInfo to a Roslyn BaseTypeSyntax.
    /// </summary>
    private BaseTypeSyntax ConvertSynthesizedInterfaceToBaseType(SynthesizedInterfaceInfo info)
    {
        // Build the namespace-qualified name
        NameSyntax namespaceName = info.Namespace switch
        {
            "Sharpy" => IdentifierName("Sharpy"),
            "System" => IdentifierName("System"),
            "System.Collections.Generic" => QualifiedName(
                QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
                IdentifierName("Generic")),
            _ => ParseQualifiedName(info.Namespace)
        };

        SimpleNameSyntax interfaceName;
        if (info.TypeArgs.Length > 0)
        {
            var typeArgs = info.TypeArgs.Select(t => _typeMapper.MapSemanticType(t)).ToArray();
            interfaceName = GenericName(info.InterfaceName)
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
        }
        else
        {
            interfaceName = IdentifierName(info.InterfaceName);
        }

        return SimpleBaseType(QualifiedName(namespaceName, interfaceName));
    }

    /// <summary>
    /// Generates an abstract method stub for an interface method that is not implemented.
    /// Uses the semantic model (FunctionSymbol) for type information, which works for both
    /// same-module and cross-module interfaces.
    /// </summary>
    /// <summary>
    /// `Self` in an interface member resolves to the interface; in the implementing class the same
    /// annotation resolves to the class. C# has no way to express that correspondence directly, so
    /// the implementation never binds to the contract (CS0535 for a `Self` parameter, CS0738 for a
    /// `Self` return). Emit the explicit-interface bridge a C# author writes by hand: the
    /// interface's exact signature, forwarding to the class's own member with a downcast at each
    /// `Self` parameter position. Only top-level `Self` is bridged — a `Self` nested in a type
    /// argument (`list[Self]`) or a generic declaring interface needs substitution machinery this
    /// does not have, and is left to fail loudly (#1342).
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateSelfInterfaceBridges(ClassDef classDef, TypeSymbol? classSymbol)
    {
        var bridges = new List<MemberDeclarationSyntax>();
        if (classSymbol == null || classDef.BaseClasses.Length == 0)
            return bridges;

        foreach (var (interfaceSymbol, method) in CollectInterfaceMethodsWithDeclarer(classDef.BaseClasses))
        {
            // A generic declaring interface would need its specifier substituted with the
            // implementing base annotation's arguments — out of scope (#1342).
            if (interfaceSymbol.TypeParameters.Count > 0)
                continue;

            var ifaceParams = method.Parameters.Where(p => p.Name != PythonNames.Self).ToList();
            if (method.ReturnType is not SelfType && !ifaceParams.Any(p => p.Type is SelfType))
                continue;

            var impl = classSymbol.Methods.FirstOrDefault(m => m.Name == method.Name
                && m.Parameters.Count(p => p.Name != PythonNames.Self) == ifaceParams.Count);
            if (impl == null)
                continue;

            var implParams = impl.Parameters.Where(p => p.Name != PythonNames.Self).ToList();

            // If the class spells the interface's own types, C# binds directly — no bridge.
            if (MappedTypeText(method.ReturnType) == MappedTypeText(impl.ReturnType)
                && ifaceParams.Zip(implParams, (a, b) => MappedTypeText(a.Type) == MappedTypeText(b.Type)).All(x => x))
            {
                continue;
            }

            var specifier = _typeMapper.MapSemanticType(
                new UserDefinedType { Name = interfaceSymbol.Name, Symbol = interfaceSymbol });
            if (specifier is not NameSyntax interfaceName)
                continue;

            var mangledName = DunderMapping.ResolveCSharpName(method.Name)
                ?? NameMangler.Transform(method.Name, NameContext.Method);

            TypeSyntax returnType = method.ReturnType is VoidType or UnknownType or null
                ? PredefinedType(Token(SyntaxKind.VoidKeyword))
                : _typeMapper.MapSemanticType(method.ReturnType);

            // Both sides declare the same logical parameters, so the C#-ordering permutation is
            // the same on both — reorder the interface's list and index the class's by position.
            var ordered = ReorderParameterSymbolsForCSharp(ifaceParams).ToList();
            var parameters = new List<ParameterSyntax>();
            var arguments = new List<ArgumentSyntax>();

            foreach (var p in ordered)
            {
                var paramName = ParameterCSharpName(p);
                TypeSyntax paramType = p.Type is UnknownType or null
                    ? PredefinedType(Token(SyntaxKind.ObjectKeyword))
                    : _typeMapper.MapSemanticType(p.Type);
                if (p.IsVariadic)
                    paramType = VariadicArrayType(paramType);

                var parameter = Parameter(EscapedIdentifier(paramName)).WithType(paramType);
                if (p.IsVariadic)
                    parameter = parameter.WithModifiers(TokenList(Token(SyntaxKind.ParamsKeyword)));
                parameters.Add(parameter);

                ExpressionSyntax argument = EscapedIdentifierName(paramName);
                var implParam = implParams[ifaceParams.IndexOf(p)];
                var implText = MappedTypeText(implParam.Type);
                if (implText != null && implText != MappedTypeText(p.Type))
                {
                    argument = CastExpression(_typeMapper.MapSemanticType(implParam.Type),
                        ParenthesizedExpression(argument));
                }
                arguments.Add(Argument(argument));
            }

            var forward = InvocationExpression(
                    MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(), EscapedIdentifierName(mangledName)))
                .WithArgumentList(ArgumentList(SeparatedList(arguments)));

            bridges.Add(MethodDeclaration(returnType, EscapedIdentifier(mangledName))
                .WithExplicitInterfaceSpecifier(ExplicitInterfaceSpecifier(interfaceName))
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithExpressionBody(ArrowExpressionClause(forward))
                .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));
        }

        return bridges;
    }

    /// <summary>Rendered C# text of a mapped semantic type, or null when there is nothing to map.</summary>
    private string? MappedTypeText(SemanticType? type)
        => type is null or UnknownType ? null : _typeMapper.MapSemanticType(type).ToString();

    /// <summary>
    /// Interface methods reachable from a class's base list, each paired with the interface that
    /// declares it (an explicit implementation must name the declaring interface).
    /// </summary>
    private List<(TypeSymbol Interface, FunctionSymbol Method)> CollectInterfaceMethodsWithDeclarer(
        IReadOnlyList<TypeAnnotation> baseTypes)
    {
        var result = new List<(TypeSymbol, FunctionSymbol)>();
        var visited = new HashSet<string>();

        void Walk(TypeSymbol interfaceSymbol)
        {
            if (!visited.Add(interfaceSymbol.Name))
                return;

            foreach (var method in interfaceSymbol.Methods)
                result.Add((interfaceSymbol, method));

            foreach (var baseInterface in interfaceSymbol.Interfaces)
            {
                if (baseInterface.Definition?.TypeKind == Semantic.TypeKind.Interface)
                    Walk(baseInterface.Definition);
            }
        }

        foreach (var baseType in baseTypes)
        {
            if (string.IsNullOrEmpty(baseType.Name))
                continue;
            var typeSymbol = _context.SymbolTable.LookupType(baseType.Name);
            if (typeSymbol?.TypeKind == Semantic.TypeKind.Interface)
                Walk(typeSymbol);
        }

        return result;
    }

    private MethodDeclarationSyntax GenerateAbstractMethodStub(FunctionSymbol method)
    {
        var mangledName = DunderMapping.ResolveCSharpName(method.Name)
            ?? NameMangler.Transform(method.Name, NameContext.Method);

        // Map return type from SemanticType
        TypeSyntax returnType = method.ReturnType is VoidType or UnknownType or null
            ? PredefinedType(Token(SyntaxKind.VoidKeyword))
            : _typeMapper.MapSemanticType(method.ReturnType);

        // Generate parameters from ParameterSymbol (skip 'self')
        // Reorder for C# compliance (required before optional, params last)
        var filteredStubParams = method.Parameters
            .Where(p => p.Name != PythonNames.Self);
        var orderedStubParams = ReorderParameterSymbolsForCSharp(filteredStubParams);
        var parameters = orderedStubParams
            .Select(p =>
            {
                var paramName = ParameterCSharpName(p);
                TypeSyntax paramType = p.Type is UnknownType or null
                    ? PredefinedType(Token(SyntaxKind.ObjectKeyword))
                    : _typeMapper.MapSemanticType(p.Type);

                // For variadic parameters, wrap in array
                if (p.IsVariadic)
                {
                    paramType = VariadicArrayType(paramType);
                }

                var param = Parameter(EscapedIdentifier(paramName)).WithType(paramType);
                if (p.IsVariadic)
                {
                    param = param.WithModifiers(TokenList(Token(SyntaxKind.ParamsKeyword)));
                }
                return param;
            })
            .ToArray();

        // Create abstract method declaration
        return MethodDeclaration(returnType, EscapedIdentifier(mangledName))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.AbstractKeyword)))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
    }

    private StructDeclarationSyntax GenerateStructDeclaration(StructDef structDef)
    {
        // Note: Struct type detection is now done via SymbolTable lookup during expression generation.
        // The _structNames tracking set was used for instantiation detection but is no longer needed
        // since the symbol table is populated during semantic analysis.

        // Transform struct name
        var structName = NameCasing.ResolveType(structDef.Name, structDef.IsNameBacktickEscaped);

        // Process decorators to determine modifiers
        var modifiers = GenerateModifiersFromDecorators(structDef.Decorators, ModifierContext.Type);

        // Create struct declaration
        var structDecl = StructDeclaration(EscapedIdentifier(structName))
            .WithModifiers(modifiers);

        // Add C# attributes from unknown decorators
        var structAttributes = GenerateAttributeListsFromDecorators(structDef.Decorators);
        if (structAttributes.Count > 0)
        {
            structDecl = structDecl.WithAttributeLists(structAttributes);
        }

        // Add type parameters if generic
        if (structDef.TypeParameters.Length > 0)
        {
            var typeParams = structDef.TypeParameters
                .Select(GenerateTypeParameterSyntax)
                .ToArray();
            structDecl = structDecl
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(structDef.TypeParameters));
        }

        // Add interfaces (structs can only implement interfaces, not inherit)
        {
            var baseTypes = structDef.BaseClasses
                .Select(bc => (BaseTypeSyntax)SimpleBaseType(_typeMapper.MapType(bc)))
                .ToList();

            // Synthesize protocol interfaces from dunder methods
            var synthesizedInterfaces = CollectSynthesizedInterfaces(structDef.Body, structDef.BaseClasses, structName, structDef.Name);
            baseTypes.AddRange(synthesizedInterfaces);

            if (baseTypes.Count > 0)
            {
                structDecl = structDecl.WithBaseList(BaseList(SeparatedList(baseTypes)));
            }
        }

        // Generate struct members from body
        var members = GenerateClassMembers(structDef.Body, structName, structDef.Name);
        structDecl = structDecl.WithMembers(List(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(structDef.DocString))
        {
            structDecl = structDecl.WithLeadingTrivia(GenerateXmlDocComment(structDef.DocString));
        }

        return structDecl;
    }

    private InterfaceDeclarationSyntax GenerateInterfaceDeclaration(InterfaceDef interfaceDef)
    {
        // Transform interface name using Interface context to preserve I prefix pattern
        var interfaceName = NameMangler.Transform(interfaceDef.Name, NameContext.Interface);

        // Process decorators to determine modifiers (access modifiers)
        var modifiers = GenerateModifiersFromDecorators(interfaceDef.Decorators, ModifierContext.Type);

        // Create interface declaration
        var interfaceDecl = InterfaceDeclaration(EscapedIdentifier(interfaceName))
            .WithModifiers(modifiers);

        // Add C# attributes from custom decorators
        var interfaceAttributes = GenerateAttributeListsFromDecorators(interfaceDef.Decorators);
        if (interfaceAttributes.Count > 0)
        {
            interfaceDecl = interfaceDecl.WithAttributeLists(interfaceAttributes);
        }

        // Add type parameters if generic
        if (interfaceDef.TypeParameters.Length > 0)
        {
            var typeParams = interfaceDef.TypeParameters
                .Select(GenerateTypeParameterSyntax)
                .ToArray();
            interfaceDecl = interfaceDecl
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(interfaceDef.TypeParameters));
        }

        // Add base interfaces
        if (interfaceDef.BaseInterfaces.Length > 0)
        {
            var baseTypes = interfaceDef.BaseInterfaces
                .Select(bi => SimpleBaseType(_typeMapper.MapType(bi)))
                .ToArray();
            interfaceDecl = interfaceDecl.WithBaseList(BaseList(SeparatedList<BaseTypeSyntax>(baseTypes)));
        }

        // Generate interface members (methods only, no implementation)
        var members = GenerateInterfaceMembers(interfaceDef.Body);
        interfaceDecl = interfaceDecl.WithMembers(List(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(interfaceDef.DocString))
        {
            interfaceDecl = interfaceDecl.WithLeadingTrivia(GenerateXmlDocComment(interfaceDef.DocString));
        }

        return interfaceDecl;
    }

    /// <summary>
    /// Creates a TypeParameterSyntax from a TypeParameterDef, applying variance annotations.
    /// Covariant (out) → SyntaxKind.OutKeyword, Contravariant (in) → SyntaxKind.InKeyword.
    /// Use only for interface/delegate type parameters (and class/struct, where variance is
    /// rejected upstream by VarianceValidator). For method/function/local-function type
    /// parameters use <see cref="GenerateMethodTypeParameterSyntax"/>, which never emits variance.
    /// </summary>
    private static TypeParameterSyntax GenerateTypeParameterSyntax(TypeParameterDef tp)
    {
        var typeParam = TypeParameter(TypeParameterIdentifier(tp.Name));
        return tp.Variance switch
        {
            TypeParameterVariance.Covariant => typeParam.WithVarianceKeyword(Token(SyntaxKind.OutKeyword)),
            TypeParameterVariance.Contravariant => typeParam.WithVarianceKeyword(Token(SyntaxKind.InKeyword)),
            _ => typeParam
        };
    }

    /// <summary>
    /// Creates a TypeParameterSyntax for a method/function/local-function type parameter.
    /// Variance keywords (out/in) are intentionally NOT emitted: C# only permits variance on
    /// interface and delegate type parameters (CS1960 otherwise). Variance on method/function
    /// type parameters is rejected upstream by VarianceValidator (SPY0417); this helper is the
    /// defense-in-depth that keeps non-halting emit paths (LSP, playground) producing valid C#.
    /// </summary>
    private static TypeParameterSyntax GenerateMethodTypeParameterSyntax(TypeParameterDef tp)
        => TypeParameter(TypeParameterIdentifier(tp.Name));

    private SyntaxList<TypeParameterConstraintClauseSyntax> GenerateConstraintClauses(
        IReadOnlyList<TypeParameterDef> typeParameters)
    {
        var clauses = new List<TypeParameterConstraintClauseSyntax>();

        foreach (var typeParam in typeParameters)
        {
            if (typeParam.Constraints.Length == 0)
                continue;

            var constraintSyntaxes = new List<TypeParameterConstraintSyntax>();

            // Order: class/struct/notnull first, then types, then new()
            var ordered = typeParam.Constraints
                .OrderBy(c => c switch
                {
                    ClassConstraint => 0,
                    StructConstraint => 0,
                    NotnullConstraint => 0,
                    Parser.Ast.TypeConstraint => 1,
                    NewConstraint => 2,
                    _ => 3
                });

            foreach (var constraint in ordered)
            {
                constraintSyntaxes.Add(constraint switch
                {
                    ClassConstraint => ClassOrStructConstraint(
                        SyntaxKind.ClassConstraint),
                    StructConstraint => ClassOrStructConstraint(
                        SyntaxKind.StructConstraint),
                    NotnullConstraint => Microsoft.CodeAnalysis.CSharp.SyntaxFactory.TypeConstraint(
                        IdentifierName("notnull")),
                    Parser.Ast.TypeConstraint tc => Microsoft.CodeAnalysis.CSharp.SyntaxFactory.TypeConstraint(
                        _typeMapper.MapType(tc.Type)),
                    NewConstraint => ConstructorConstraint(),
                    _ => throw new InvalidOperationException($"Unknown constraint type: {constraint.GetType().Name}")
                });
            }

            clauses.Add(TypeParameterConstraintClause(typeParam.Name)
                .WithConstraints(SeparatedList(constraintSyntaxes)));
        }

        return List(clauses);
    }

    private SyntaxNode GenerateEnumDeclaration(EnumDef enumDef)
    {
        // Determine if this is a string enum or integer enum
        // Note: String enum detection during expression generation now uses CodeGenInfo.IsStringEnum
        // which is computed during semantic analysis, so we no longer need the _stringEnumNames tracking set.
        bool isStringEnum = IsStringEnum(enumDef);

        if (isStringEnum)
        {
            return GenerateStringEnumClass(enumDef);
        }
        else
        {
            return GenerateIntegerEnum(enumDef);
        }
    }

    /// <summary>The generated name of a string enum's all-members list (#1284).</summary>
    internal const string StringEnumValuesMember = "Values";

    /// <summary>
    /// The C# expression `for x in SomeEnum` iterates. An int-backed enum is a real C# enum and
    /// uses <c>Enum.GetValues&lt;T&gt;()</c>; a string-backed enum is a class of singletons, for
    /// which that call does not even bind (CS0453 — not a value type), so it reads the generated
    /// <c>Values</c> list instead (#1284).
    /// </summary>
    private ExpressionSyntax GenerateEnumValuesIterator(Semantic.UserDefinedType enumUdt)
    {
        var enumTypeSyntax = _typeMapper.MapSemanticType(enumUdt);

        if (enumUdt.Symbol is { } enumSymbol && IsStringEnumSymbol(enumSymbol))
        {
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                enumTypeSyntax,
                IdentifierName(StringEnumValuesMember));
        }

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("Enum"),
                GenericName(Identifier("GetValues"))
                    .WithTypeArgumentList(TypeArgumentList(
                        SingletonSeparatedList(enumTypeSyntax)))));
    }

    /// <summary>
    /// Whether an enum declaration is string-backed. Reads the semantic fact rather than
    /// re-deriving it from the AST — the emitter had a second copy of the rule here, which is
    /// exactly the duplication the pure-translator contract exists to prevent (#1284).
    /// </summary>
    private bool IsStringEnum(EnumDef enumDef)
        => _context.LookupSymbol(enumDef.Name) is TypeSymbol { TypeKind: Semantic.TypeKind.Enum } sym
            ? IsStringEnumSymbol(sym)
            : enumDef.Members.Any(m => m.Value is StringLiteral);

    /// <summary>
    /// Checks if a TypeSymbol represents a string enum. <c>TypeSymbol.IsStringEnum</c> is set in
    /// name resolution; <c>CodeGenInfo.IsStringEnum</c> is the materialized mirror, consulted for
    /// symbols restored from a cache that predates the symbol flag.
    /// </summary>
    private bool IsStringEnumSymbol(TypeSymbol enumSymbol)
        => enumSymbol.IsStringEnum || GetCodeGenInfo(enumSymbol)?.IsStringEnum == true;

    /// <summary>
    /// The C# identifier that names <paramref name="memberName"/> on <paramref name="enumSymbol"/>.
    /// Three kinds of enum, three spellings, and the caller must not have to know which:
    ///
    /// <list type="bullet">
    /// <item><description>a string-backed enum is a CLASS of singleton fields, emitted by
    /// <see cref="GenerateStringEnumClass"/> through <c>NameContext.Constant</c>, so the reference
    /// has to use that same casing;</description></item>
    /// <item><description>a CLR enum already carries correct .NET member names, so mangling would
    /// only corrupt them;</description></item>
    /// <item><description>a source int-backed enum is a real C# enum whose members go through
    /// <see cref="NameMangler.ToEnumMemberName"/>.</description></item>
    /// </list>
    ///
    /// <para>One helper rather than the rule written out at each reference site, because that is
    /// exactly how it broke: the expression path made this three-way choice and the pattern path
    /// made a two-way one, so a `match` arm naming a string-enum or CLR member whose casing the two
    /// contexts spell differently emitted an identifier no member has (CS0117 behind SPY0908 —
    /// `Debugmode` for a field named `DebugMode`, `Ordinalignorecase` for `OrdinalIgnoreCase`).
    /// Only names already SCREAMING_SNAKE_CASE, single-word, or snake_case survived the divergence,
    /// which is why nothing in the corpus caught it (#1284).</para>
    /// </summary>
    private SimpleNameSyntax EnumMemberIdentifier(
        TypeSymbol enumSymbol, string memberName, bool isMemberBacktickEscaped = false)
    {
        if (IsStringEnumSymbol(enumSymbol))
            return EscapedIdentifierName(NameCasing.ResolveConstant(memberName, isMemberBacktickEscaped));

        return IdentifierName(enumSymbol.ClrType != null
            ? memberName
            : NameMangler.ToEnumMemberName(memberName));
    }

    /// <summary>
    /// Generates a C# enum for integer enums
    /// </summary>
    private EnumDeclarationSyntax GenerateIntegerEnum(EnumDef enumDef)
    {
        // Transform enum name
        var enumName = NameCasing.ResolveType(enumDef.Name, enumDef.IsNameBacktickEscaped);

        // Enums are always public by default
        var modifiers = TokenList(Token(SyntaxKind.PublicKeyword));

        // Generate enum members
        var members = enumDef.Members
            .Select(GenerateEnumMember)
            .ToArray();

        var enumDecl = EnumDeclaration(EscapedIdentifier(enumName))
            .WithModifiers(modifiers)
            .WithMembers(SeparatedList(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(enumDef.DocString))
        {
            enumDecl = enumDecl.WithLeadingTrivia(GenerateXmlDocComment(enumDef.DocString));
        }

        return enumDecl;
    }

    /// <summary>
    /// Generates the sealed class a string-backed enum lowers to (#1284): one singleton instance
    /// per member, a <c>Value</c>/<c>Name</c> pair, <c>ToString()</c> returning the value, an
    /// implicit conversion to <c>string</c>, and a static <c>Values</c> list the iteration arms
    /// consume in place of <c>Enum.GetValues&lt;T&gt;()</c>.
    ///
    /// <para>
    /// This is CPython's <c>StrEnum</c> shape: a member is its own type AND compares equal to its
    /// string. The previous emission — bare <c>static readonly string</c> fields — made the member
    /// literally a string, so the declared enum type could not appear in any annotation the
    /// semantic layer agreed with; every use was refused or emitted uncompilable C#.
    /// </para>
    /// </summary>
    private ClassDeclarationSyntax GenerateStringEnumClass(EnumDef enumDef)
    {
        // Transform enum name
        var className = NameCasing.ResolveType(enumDef.Name, enumDef.IsNameBacktickEscaped);
        var classType = (TypeSyntax)EscapedIdentifierName(className);

        // Create public sealed class
        var modifiers = TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.SealedKeyword)
        );

        var classDecl = ClassDeclaration(EscapedIdentifier(className))
            .WithModifiers(modifiers);

        var stringType = PredefinedType(Token(SyntaxKind.StringKeyword));
        var members = new List<MemberDeclarationSyntax>();

        // private LogLevel(string name, string value) { Name = name; Value = value; }
        members.Add(ConstructorDeclaration(EscapedIdentifier(className))
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
            .WithParameterList(ParameterList(SeparatedList(new[]
            {
                Parameter(Identifier("name")).WithType(stringType),
                Parameter(Identifier("value")).WithType(stringType)
            })))
            .WithBody(Block(
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName("Name"), IdentifierName("name"))),
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName("Value"), IdentifierName("value"))))));

        // public string Name { get; }  /  public string Value { get; }
        foreach (var propName in new[] { "Name", "Value" })
        {
            members.Add(PropertyDeclaration(stringType, Identifier(propName))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithAccessorList(AccessorList(SingletonList(
                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))))));
        }

        // public static readonly LogLevel INFO = new LogLevel("INFO", "INFO");
        var memberFieldNames = new List<string>();
        foreach (var member in enumDef.Members)
        {
            var fieldName = NameMangler.Transform(member.Name, NameContext.Constant);
            memberFieldNames.Add(fieldName);

            // Use the explicit value if provided, otherwise the member name as written.
            ExpressionSyntax valueExpr = member.Value is StringLiteral strLit
                ? GenerateExpression(strLit)
                : LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(member.Name));

            var instance = ObjectCreationExpression(classType)
                .WithArgumentList(ArgumentList(SeparatedList(new[]
                {
                    Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(member.Name))),
                    Argument(valueExpr)
                })));

            members.Add(FieldDeclaration(
                    VariableDeclaration(classType)
                        .WithVariables(SingletonSeparatedList(
                            VariableDeclarator(EscapedIdentifier(fieldName))
                                .WithInitializer(EqualsValueClause(instance)))))
                .WithModifiers(TokenList(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.StaticKeyword),
                    Token(SyntaxKind.ReadOnlyKeyword))));
        }

        // public override string ToString() => Value;
        members.Add(MethodDeclaration(stringType, Identifier("ToString"))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.OverrideKeyword)))
            .WithParameterList(ParameterList())
            .WithExpressionBody(ArrowExpressionClause(IdentifierName("Value")))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // public static implicit operator string(LogLevel value) => value.Value;
        members.Add(ConversionOperatorDeclaration(Token(SyntaxKind.ImplicitKeyword), stringType)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(ParameterList(SingletonSeparatedList(
                Parameter(Identifier("value")).WithType(classType))))
            .WithExpressionBody(ArrowExpressionClause(MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression, IdentifierName("value"), IdentifierName("Value"))))
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        // public static readonly Sharpy.List<LogLevel> Values = new(new LogLevel[] { ... });
        // Declared last: C# initializes static fields in textual order, so the members must exist.
        // Built as real syntax, not as an identifier spelled "Sharpy.List": a dotted identifier
        // prints correctly but does not bind under direct tree handoff (#1095's guard).
        var valuesType = (TypeSyntax)QualifiedName(
            MakeGlobalQualifiedName("Sharpy"),
            GenericName(Identifier("List"))
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(classType))));
        var valuesArray = ArrayCreationExpression(
                ArrayType(classType).WithRankSpecifiers(SingletonList(
                    ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))))
            .WithInitializer(InitializerExpression(SyntaxKind.ArrayInitializerExpression,
                SeparatedList<ExpressionSyntax>(memberFieldNames.Select(EscapedIdentifierName))));
        members.Add(FieldDeclaration(
                VariableDeclaration(valuesType)
                    .WithVariables(SingletonSeparatedList(
                        VariableDeclarator(Identifier(StringEnumValuesMember))
                            .WithInitializer(EqualsValueClause(
                                ObjectCreationExpression(valuesType)
                                    .WithArgumentList(ArgumentList(SingletonSeparatedList(
                                        Argument(valuesArray)))))))))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword))));

        classDecl = classDecl.WithMembers(List(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(enumDef.DocString))
        {
            classDecl = classDecl.WithLeadingTrivia(GenerateXmlDocComment(enumDef.DocString));
        }

        return classDecl;
    }

    private EnumMemberDeclarationSyntax GenerateEnumMember(EnumMember member)
    {
        // Enum members use PascalCase in C# (RED -> Red, DARK_BLUE -> DarkBlue)
        // Need custom logic because NameMangler.ToPascalCase preserves all-caps words
        var memberName = NameMangler.ToEnumMemberName(member.Name);

        var enumMember = EnumMemberDeclaration(Identifier(memberName));

        // Add explicit value if present
        if (member.Value != null)
        {
            var valueExpr = GenerateExpression(member.Value);
            enumMember = enumMember.WithEqualsValue(EqualsValueClause(valueExpr));
        }

        return enumMember;
    }

    /// <summary>
    /// Generates C# attribute lists from decorators.
    /// Bracket attributes (@[...]) are emitted verbatim without name mangling.
    /// @deprecated is mapped to [Obsolete]. All other known decorators are handled elsewhere.
    ///
    /// <para>The test decorators (<c>@test</c> and its family) map to <c>Xunit.*</c> attributes,
    /// which only exist where the framework does. Outside a test host they are DROPPED and a
    /// <c>@test</c> function emits as an ordinary method (#1495) — emitting them unconditionally is
    /// what made any program containing a <c>@test</c> function uncompilable under
    /// <c>sharpyc run</c>: CS0246 behind SPY0908, an internal-error report for an ordinary program.
    /// The decorators keep their SEMANTIC meaning either way; what is host-conditional is only the
    /// runner integration.</para>
    /// </summary>
    /// <summary>
    /// A bracket attribute whose name resolves to a <see cref="TypeSymbol"/> with the materialized
    /// <see cref="TypeSymbol.IsSourceGenerator"/> flag set — a generator trigger, not a C# attribute
    /// (#1431). Reads the flag NameResolver set at inheritance resolution; the emitter makes no
    /// decision, it applies one (Critical Rule 2). Kept in lockstep with
    /// <c>DecoratorValidator.IsSourceGeneratorBracketAttribute</c>, which does the same lookup.
    /// </summary>
    private bool IsSourceGeneratorTrigger(Decorator decorator)
        => _context.SymbolTable.LookupType(decorator.Name) is { IsSourceGenerator: true };

    private SyntaxList<AttributeListSyntax> GenerateAttributeListsFromDecorators(IReadOnlyList<Decorator> decorators)
    {
        var attributeLists = new List<AttributeListSyntax>();

        // Pre-extract @test.skip / @test.skip_if so they can be merged into [Fact(Skip=...)]
        // or [Theory(Skip=...)] rather than emitted as standalone attributes. Only a test host has
        // a runner to skip anything, so outside one there is nothing to merge.
        string? skipReason = _context.TargetsTestHost ? ResolveSkipReason(decorators) : null;

        // Track whether the loop emitted a [Fact] or [Theory] attribute; if not but a skip
        // applies, we still need to mark the function as a test method.
        bool emittedTestAttribute = false;

        foreach (var decorator in decorators)
        {
            // A bracket attribute naming a source generator is a generator TRIGGER, consumed by the
            // generator pipeline (Phase 5b), never a C# attribute — its members are folded into the
            // target by the generator. Emitting it as a plain attribute produced CS0616 ("... is not
            // an attribute class") behind SPY0908 (#1431). Mirrors
            // DecoratorValidator.IsSourceGeneratorBracketAttribute, which exempts the same shape from
            // the unknown-attribute refusal by the same materialized flag.
            if (decorator.IsBracketAttribute && IsSourceGeneratorTrigger(decorator))
                continue;

            if (!decorator.IsBracketAttribute)
            {
                if (DecoratorNames.KnownModifierDecorators.Contains(decorator.Name))
                    continue;

                // Every Xunit-producing decorator is dropped outside a test host (#1495). Bracket
                // attributes are unaffected: they name types the user chose, not a framework this
                // compiler assumed.
                if (!_context.TargetsTestHost && DecoratorNames.IsTestFrameworkDecorator(decorator.Name))
                    continue;

                if (decorator.Name == DecoratorNames.Dataclass)
                    continue;

                if (decorator.Name == DecoratorNames.LruCache
                    || decorator.Name == DecoratorNames.Cache
                    || decorator.Name == DecoratorNames.StaticMethod
                    || decorator.Name == DecoratorNames.ClassMethod)
                    continue;

                // @suppress is a compile-time-only diagnostic decorator — it scopes warning
                // suppression during validation and emits no C# attribute.
                if (decorator.Name == DecoratorNames.Suppress)
                    continue;

                // @must_use is a compile-time-only marker read by MustUseValidator — no C# attribute.
                if (decorator.Name == DecoratorNames.MustUse)
                    continue;

                // @test.skip / @test.skip_if are merged into the [Fact]/[Theory] attribute
                // (handled via skipReason above), not emitted as separate attributes.
                if (decorator.Name == DecoratorNames.TestSkip
                    || decorator.Name == DecoratorNames.TestSkipIf)
                    continue;

                // @test.fixture functions are emitted as standalone fixture classes via
                // GenerateFixtureClass; the decorator itself produces no method-level attribute.
                if (decorator.Name == DecoratorNames.TestFixture)
                    continue;

                // @test.collection("name") → [Xunit.CollectionAttribute("name")]
                // Class-level decorator that groups tests for sequential execution in xUnit.
                if (decorator.Name == DecoratorNames.TestCollection)
                {
                    if (decorator.Arguments.Length == 1
                        && decorator.Arguments[0] is StringLiteral collectionName)
                    {
                        var collectionAttribute = Attribute(ParseQualifiedName("Xunit.CollectionAttribute"))
                            .WithArgumentList(AttributeArgumentList(SingletonSeparatedList(
                                AttributeArgument(LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal(collectionName.Value))))));
                        attributeLists.Add(AttributeList(SingletonSeparatedList(collectionAttribute)));
                    }
                    continue;
                }

                // @test.mark("category") → [Xunit.TraitAttribute("Category", "category")]
                // Multiple @test.mark decorators produce multiple [Trait] attributes.
                if (decorator.Name == DecoratorNames.TestMark)
                {
                    if (decorator.Arguments.Length == 1
                        && decorator.Arguments[0] is StringLiteral markValue)
                    {
                        var traitAttribute = Attribute(ParseQualifiedName("Xunit.TraitAttribute"))
                            .WithArgumentList(AttributeArgumentList(SeparatedList(new[]
                            {
                                AttributeArgument(LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal(CSharpTypeNames.XunitTraitCategory))),
                                AttributeArgument(LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal(markValue.Value))),
                            })));
                        attributeLists.Add(AttributeList(SingletonSeparatedList(traitAttribute)));
                    }
                    continue;
                }

                // @test → [Xunit.FactAttribute]
                // @test("description") → [Xunit.FactAttribute(DisplayName = "description")]
                if (decorator.Name == DecoratorNames.Test)
                {
                    var factAttribute = Attribute(ParseQualifiedName("Xunit.FactAttribute"));
                    var factArgs = new List<AttributeArgumentSyntax>();

                    if (decorator.Arguments.Length == 1
                        && decorator.Arguments[0] is StringLiteral descLit)
                    {
                        factArgs.Add(AttributeArgument(LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            Literal(descLit.Value)))
                            .WithNameEquals(NameEquals("DisplayName")));
                    }

                    if (skipReason != null)
                    {
                        factArgs.Add(AttributeArgument(LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            Literal(skipReason)))
                            .WithNameEquals(NameEquals("Skip")));
                    }

                    if (factArgs.Count > 0)
                    {
                        factAttribute = factAttribute.WithArgumentList(
                            AttributeArgumentList(SeparatedList(factArgs)));
                    }
                    attributeLists.Add(AttributeList(SingletonSeparatedList(factAttribute)));
                    emittedTestAttribute = true;
                    continue;
                }

                // @test.parametrize([(a,b,...), ...]) → [Xunit.TheoryAttribute] + [Xunit.InlineDataAttribute(...)]
                if (decorator.Name == DecoratorNames.TestParametrize)
                {
                    attributeLists.AddRange(GenerateParametrizeAttributes(decorator, skipReason));
                    emittedTestAttribute = true;
                    continue;
                }
            }

            NameSyntax attributeName;
            if (decorator.Name == DecoratorNames.Deprecated)
            {
                attributeName = IdentifierName("Obsolete");
            }
            else if (decorator.IsBracketAttribute)
            {
                // Bracket attributes: snake_case → PascalCase mangling (backtick-escaped parts are verbatim)
                attributeName = IdentifierName(MangleBracketPart(decorator, 0));
                for (int i = 1; i < decorator.QualifiedParts.Length; i++)
                {
                    attributeName = QualifiedName(attributeName, IdentifierName(MangleBracketPart(decorator, i)));
                }
            }
            else
            {
                // Unknown non-bracket decorator — should have been rejected by DecoratorValidator
                continue;
            }

            var attribute = Attribute(attributeName);

            if (decorator.Arguments.Length > 0 || decorator.KeywordArguments.Length > 0)
            {
                var args = new List<AttributeArgumentSyntax>();

                foreach (var arg in decorator.Arguments)
                {
                    args.Add(AttributeArgument(GenerateAttributeArgumentExpression(arg)));
                }

                foreach (var kwArg in decorator.KeywordArguments)
                {
                    var nameEquals = NameEquals(IdentifierName(NameCasing.ResolveField(kwArg.Name, false)));
                    args.Add(AttributeArgument(GenerateAttributeArgumentExpression(kwArg.Value))
                        .WithNameEquals(nameEquals));
                }

                attribute = attribute.WithArgumentList(AttributeArgumentList(SeparatedList(args)));
            }

            attributeLists.Add(AttributeList(SingletonSeparatedList(attribute)));
        }

        // If @test.skip / @test.skip_if was the only test marker (no @test or @test.parametrize),
        // synthesize a [Fact(Skip = "reason")] so the method is registered as a skipped test.
        if (skipReason != null && !emittedTestAttribute)
        {
            var skippedFact = Attribute(ParseQualifiedName("Xunit.FactAttribute"))
                .WithArgumentList(AttributeArgumentList(SingletonSeparatedList(
                    AttributeArgument(LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        Literal(skipReason)))
                        .WithNameEquals(NameEquals("Skip")))));
            attributeLists.Add(AttributeList(SingletonSeparatedList(skippedFact)));
        }

        return List(attributeLists);
    }

    /// <summary>
    /// Resolves a Skip reason string from @test.skip / @test.skip_if decorators on a member.
    /// Returns null if no skip applies.
    /// - @test.skip("reason") always skips.
    /// - @test.skip_if(condition, "reason") skips only when condition is the literal True.
    ///   When the condition is the literal False, the decorator has no effect at codegen time.
    ///   Non-constant conditions fall back to "reason" (conservatively always skip), since
    ///   xUnit v2 has no runtime-conditional Skip mechanism. The skip reason is appended with
    ///   a hint noting the condition is runtime-evaluated.
    /// </summary>
    private static string? ResolveSkipReason(IReadOnlyList<Decorator> decorators)
    {
        foreach (var d in decorators)
        {
            if (d.IsBracketAttribute)
                continue;

            if (d.Name == DecoratorNames.TestSkip
                && d.Arguments.Length >= 1
                && d.Arguments[0] is StringLiteral reason)
            {
                return reason.Value;
            }

            if (d.Name == DecoratorNames.TestSkipIf
                && d.Arguments.Length >= 2
                && d.Arguments[1] is StringLiteral skipIfReason)
            {
                var condition = d.Arguments[0];
                // Compile-time evaluation: literal True → skip, literal False → don't skip.
                if (condition is BooleanLiteral { Value: false })
                    continue;
                if (condition is BooleanLiteral { Value: true })
                    return skipIfReason.Value;

                // Non-constant condition: conservatively skip with a hint in the reason.
                return $"{skipIfReason.Value} (condition is runtime-evaluated; always skipped)";
            }
        }
        return null;
    }

    /// <summary>
    /// Generates [Theory(Skip=...)] + one [InlineData(...)] per row for a @test.parametrize decorator.
    /// The argument shape is validated by DecoratorValidator:
    /// - decorator.Arguments[0] is a ListLiteral (inline rows → [InlineData] per row), or
    /// - decorator.Arguments[0] is an Identifier referencing a module-level variable
    ///   (→ a single [MemberData] pointing at a generated wrapper property on the module class)
    /// - ListLiteral elements are TupleLiterals (or scalar literals for single-parameter functions)
    /// </summary>
    private IEnumerable<AttributeListSyntax> GenerateParametrizeAttributes(Decorator decorator, string? skipReason)
    {
        var result = new List<AttributeListSyntax>();

        // [Xunit.TheoryAttribute] (with optional Skip)
        var theoryAttr = Attribute(ParseQualifiedName("Xunit.TheoryAttribute"));
        if (skipReason != null)
        {
            theoryAttr = theoryAttr.WithArgumentList(
                AttributeArgumentList(SingletonSeparatedList(
                    AttributeArgument(LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        Literal(skipReason)))
                        .WithNameEquals(NameEquals("Skip")))));
        }
        result.Add(AttributeList(SingletonSeparatedList(theoryAttr)));

        // @test.parametrize(VARIABLE) → [Xunit.MemberData(nameof(Module.VarMemberData), MemberType = typeof(Module))]
        // The wrapper property (generated by GenerateParametrizeMemberDataProperties) adapts the
        // module-level list variable to xUnit's IEnumerable<object[]> MemberData contract.
        if (decorator.Arguments.Length == 1 && decorator.Arguments[0] is Identifier dataVariable)
        {
            result.Add(AttributeList(SingletonSeparatedList(
                GenerateMemberDataAttribute(dataVariable.Name))));
            return result;
        }

        if (decorator.Arguments.Length != 1 || decorator.Arguments[0] is not ListLiteral listLit)
        {
            // Should be unreachable — DecoratorValidator rejects this. Return just [Theory].
            return result;
        }

        foreach (var element in listLit.Elements)
        {
            IReadOnlyList<Expression> rowValues = element switch
            {
                TupleLiteral t => t.Elements,
                // Single-parameter functions may use a flat list of scalars.
                _ => new[] { element },
            };

            var args = rowValues
                .Select(v => AttributeArgument(GenerateAttributeArgumentExpression(v)))
                .ToArray();

            var inlineData = Attribute(ParseQualifiedName("Xunit.InlineDataAttribute"));
            if (args.Length > 0)
            {
                inlineData = inlineData.WithArgumentList(AttributeArgumentList(SeparatedList(args)));
            }

            result.Add(AttributeList(SingletonSeparatedList(inlineData)));
        }

        return result;
    }

    /// <summary>
    /// Builds [Xunit.MemberDataAttribute(nameof(Module.VarMemberData), MemberType = typeof(Module))]
    /// for a @test.parametrize(VARIABLE) decorator. Also records the variable name so the module
    /// class generation emits the companion MemberData wrapper property.
    /// </summary>
    private AttributeSyntax GenerateMemberDataAttribute(string variableName)
    {
        _memberDataVariables.Add(variableName);

        var moduleClassName = _resolvedModuleClassName ?? GetModuleClassName();
        var propertyName = GetMemberDataPropertyName(variableName);

        // nameof(Module.VarMemberData) — the invocation target must carry ContextualKind ==
        // NameOfKeyword (the shape Roslyn's parser produces) so the binder recognizes the
        // nameof-expression. A plain IdentifierName("nameof") prints identically but binds as a
        // call to an undefined method 'nameof' (CS0103) under direct CSharpSyntaxTree.Create (#1095).
        var nameofArgument = AttributeArgument(
            InvocationExpression(IdentifierName(
                Identifier(TriviaList(), SyntaxKind.NameOfKeyword, "nameof", "nameof", TriviaList())))
                .WithArgumentList(ArgumentList(SingletonSeparatedList(
                    Argument(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(moduleClassName),
                        EscapedIdentifierName(propertyName)))))));

        // MemberType = typeof(Module)
        var memberTypeArgument = AttributeArgument(
            TypeOfExpression(IdentifierName(moduleClassName)))
            .WithNameEquals(NameEquals("MemberType"));

        return Attribute(ParseQualifiedName("Xunit.MemberDataAttribute"))
            .WithArgumentList(AttributeArgumentList(SeparatedList(new[]
            {
                nameofArgument,
                memberTypeArgument,
            })));
    }

    /// <summary>
    /// Name of the generated MemberData wrapper property for a parametrize data variable:
    /// PascalCase of the variable name + "MemberData" (e.g., TEST_DATA → TestDataMemberData).
    /// </summary>
    private static string GetMemberDataPropertyName(string variableName)
        => NameMangler.Transform(variableName, NameContext.Field) + "MemberData";

    private static string MangleBracketPart(Decorator decorator, int index)
    {
        var part = decorator.QualifiedParts[index];
        var isEscaped = decorator.BacktickEscapedParts.Length > index && decorator.BacktickEscapedParts[index];
        return NameCasing.ResolveType(part, isEscaped);
    }

    /// <summary>
    /// Generates a C# expression for a decorator argument.
    /// Only compile-time constant expressions are valid (validated by DecoratorValidator).
    /// Handles: literals, None → null, type(X) → typeof(X), member access (enum values),
    /// negative numeric literals.
    /// </summary>
    private ExpressionSyntax GenerateAttributeArgumentExpression(Expression expr)
    {
        return expr switch
        {
            StringLiteral strLit => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(strLit.Value)),
            IntegerLiteral intLit => GenerateIntegerLiteral(intLit),
            FloatLiteral floatLit => GenerateFloatLiteral(floatLit),
            BooleanLiteral boolLit => LiteralExpression(boolLit.Value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression),
            NoneLiteral => LiteralExpression(SyntaxKind.NullLiteralExpression),
            // Negative numeric literals: -42, -3.14
            UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral or FloatLiteral } unaryOp
                => PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, GenerateAttributeArgumentExpression(unaryOp.Operand)),
            // type(X) → typeof(X)
            FunctionCall { Function: Identifier { Name: "type" }, Arguments.Length: 1, KeywordArguments.Length: 0 } call
                => TypeOfExpression(_typeMapper.MapTypeFromExpression(call.Arguments[0])),
            // Member access (e.g., StringComparison.ordinal → StringComparison.Ordinal)
            // Intentionally permissive — accepts any Identifier.Member form. Invalid cases
            // (non-enum, non-const fields) are caught downstream by the C# compiler.
            MemberAccess { Object: Identifier objId } memberAccess => MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(NameCasing.ResolveType(objId.Name, objId.IsNameBacktickEscaped)),
                IdentifierName(NameCasing.ResolveField(memberAccess.Member, false))),
            _ => throw new InvalidOperationException(
                $"Unsupported decorator argument expression: {expr.GetType().Name}. " +
                "DecoratorValidator should have rejected this."),
        };
    }

    #endregion

    #region Union Declarations

    private SyntaxNode GenerateUnionDeclaration(UnionDef unionDef)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        var unionName = NameCasing.ResolveType(unionDef.Name, unionDef.IsNameBacktickEscaped);

        // Look up the union symbol for field type information
        var unionSymbol = _context.LookupSymbol(unionDef.Name) as TypeSymbol;

        // Create abstract base class with public modifier
        var classDecl = ClassDeclaration(EscapedIdentifier(unionName))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.AbstractKeyword)));

        // Add C# attributes from unknown decorators
        var unionAttributes = GenerateAttributeListsFromDecorators(unionDef.Decorators);
        if (unionAttributes.Count > 0)
        {
            classDecl = classDecl.WithAttributeLists(unionAttributes);
        }

        // Add type parameters if generic
        if (unionDef.TypeParameters.Length > 0)
        {
            var typeParams = unionDef.TypeParameters
                .Select(GenerateTypeParameterSyntax)
                .ToArray();
            classDecl = classDecl
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)));
        }

        // Generate members: private constructor + sealed case classes
        var members = new List<MemberDeclarationSyntax>();

        // Private parameterless constructor to prevent external subclassing
        var privateCtor = ConstructorDeclaration(EscapedIdentifier(unionName))
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword)))
            .WithParameterList(ParameterList())
            .WithBody(Block());
        members.Add(privateCtor);

        // Generate sealed case classes
        for (int i = 0; i < unionDef.Cases.Length; i++)
        {
            var caseDef = unionDef.Cases[i];
            var caseSymbol = unionSymbol?.UnionCases.FirstOrDefault(c => c.Name == caseDef.Name);
            members.Add(GenerateUnionCaseClass(caseDef, caseSymbol, unionName, unionDef.TypeParameters));
        }

        // Generate method members from the union body
        if (unionDef.Body.Length > 0)
        {
            var bodyMembers = GenerateClassMembers(unionDef.Body, unionName, unionDef.Name);
            members.AddRange(bodyMembers);
        }

        classDecl = classDecl.WithMembers(List(members));

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(unionDef.DocString))
        {
            classDecl = classDecl.WithLeadingTrivia(GenerateXmlDocComment(unionDef.DocString));
        }

        return classDecl;
    }

    private ClassDeclarationSyntax GenerateUnionCaseClass(
        UnionCaseDef caseDef,
        TypeSymbol? caseSymbol,
        string baseClassName,
        ImmutableArray<TypeParameterDef> typeParams)
    {
        var caseName = NameMangler.Transform(caseDef.Name, NameContext.Type);

        // public sealed class CaseName : BaseClass
        var caseDecl = ClassDeclaration(EscapedIdentifier(caseName))
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.SealedKeyword)));

        // Base type: union base class (with type arguments if generic)
        TypeSyntax baseType;
        if (typeParams.Length > 0)
        {
            var typeArgs = typeParams
                .Select(tp => (TypeSyntax)TypeParameterIdentifierName(tp.Name))
                .ToArray();
            baseType = GenericName(Identifier(baseClassName))
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
        }
        else
        {
            baseType = IdentifierName(baseClassName);
        }
        caseDecl = caseDecl.WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(baseType))));

        var caseMembers = new List<MemberDeclarationSyntax>();
        var fields = caseSymbol?.Fields ?? new List<VariableSymbol>();

        // Generate read-only auto-properties for each field
        foreach (var field in fields)
        {
            var propName = NameCasing.ResolveField(field.Name, false);
            var propType = _typeMapper.MapSemanticType(field.Type);

            var prop = PropertyDeclaration(propType, Identifier(propName))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithAccessorList(AccessorList(SingletonList(
                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))));
            caseMembers.Add(prop);
        }

        // Generate constructor
        if (fields.Count > 0)
        {
            var ctorParams = fields.Select(f =>
                Parameter(EscapedIdentifier(NameMangler.ToCamelCase(f.Name)))
                    .WithType(_typeMapper.MapSemanticType(f.Type)))
                .ToArray();

            var ctorBody = fields.Select(f =>
            {
                var propName = NameCasing.ResolveField(f.Name, false);
                var paramName = NameMangler.ToCamelCase(f.Name);
                return (StatementSyntax)ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(propName),
                        IdentifierName(paramName)));
            }).ToArray();

            var ctor = ConstructorDeclaration(EscapedIdentifier(caseName))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList(SeparatedList(ctorParams)))
                .WithBody(Block(ctorBody));
            caseMembers.Add(ctor);

            // Generate Deconstruct method
            caseMembers.Add(GenerateDeconstructMethod(fields));
        }
        else
        {
            // Parameterless constructor for cases with no fields
            var ctor = ConstructorDeclaration(EscapedIdentifier(caseName))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList())
                .WithBody(Block());
            caseMembers.Add(ctor);
        }

        caseDecl = caseDecl.WithMembers(List(caseMembers));
        return caseDecl;
    }

    private MethodDeclarationSyntax GenerateDeconstructMethod(List<VariableSymbol> fields)
    {
        var outParams = fields.Select(f =>
            Parameter(EscapedIdentifier(NameMangler.ToCamelCase(f.Name)))
                .WithType(_typeMapper.MapSemanticType(f.Type))
                .WithModifiers(TokenList(Token(SyntaxKind.OutKeyword))))
            .ToArray();

        var body = fields.Select(f =>
        {
            var paramName = NameMangler.ToCamelCase(f.Name);
            var propName = NameCasing.ResolveField(f.Name, false);
            return (StatementSyntax)ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(paramName),
                    IdentifierName(propName)));
        }).ToArray();

        return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), "Deconstruct")
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ParameterList(SeparatedList(outParams)))
            .WithBody(Block(body));
    }

    #endregion

    #region Delegate Declarations

    private DelegateDeclarationSyntax GenerateDelegateDeclaration(DelegateDef delegateDef)
    {
        // Transform delegate name using Type context (PascalCase)
        var delegateName = NameCasing.ResolveType(delegateDef.Name, delegateDef.IsNameBacktickEscaped);

        // Determine return type from annotation or default to void
        TypeSyntax returnType = delegateDef.ReturnType != null
            ? _typeMapper.MapType(delegateDef.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Delegates are always public
        var modifiers = TokenList(Token(SyntaxKind.PublicKeyword));

        // Reorder parameters for C# compliance (required before optional, params last)
        var orderedParams = ReorderParametersForCSharp(delegateDef.Parameters);

        // Generate parameters with type annotations
        var parameters = orderedParams
            .Select(GenerateParameter)
            .ToArray();

        var delegateDecl = DelegateDeclaration(returnType, EscapedIdentifier(delegateName))
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)));

        // Add type parameters if generic
        if (delegateDef.TypeParameters.Length > 0)
        {
            var typeParams = delegateDef.TypeParameters
                .Select(GenerateTypeParameterSyntax)
                .ToArray();
            delegateDecl = delegateDecl
                .WithTypeParameterList(TypeParameterList(SeparatedList(typeParams)))
                .WithConstraintClauses(GenerateConstraintClauses(delegateDef.TypeParameters));
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(delegateDef.DocString))
        {
            delegateDecl = delegateDecl.WithLeadingTrivia(GenerateXmlDocComment(delegateDef.DocString));
        }

        return delegateDecl;
    }

    #endregion
}
