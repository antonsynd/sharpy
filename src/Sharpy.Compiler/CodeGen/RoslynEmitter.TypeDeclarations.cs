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
    private MethodDeclarationSyntax GenerateFunctionDeclaration(FunctionDef func)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        // Clear declared variables and version tracking for new function scope
        ResetMethodScope(func);

        // Pre-scan the function body to collect all variable names that will be declared.
        // This enables us to avoid generating versioned names (x_1, x_2) that collide
        // with user-declared variables.
        CollectSourceVariableNames(func.Body);

        // Transform name using NameMangler
        // Special case: only convert "main" to "Main" if this is the entry point file
        var mangledName = func.Name == "main" && !_context.IsEntryPoint
            ? "MainFunc"  // Rename to avoid C# entry point conflict in non-entry files
            : NameMangler.Transform(func.Name, NameContext.Method);

        // Check if this function is a generator and/or async
        using var _ = SetGeneratorScope(_context.SemanticInfo?.IsGenerator(func) == true);
        using var _async = SetAsyncScope(func.IsAsync);

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
            // For non-generator async functions, wrap return type in Task<T> or Task
            if (func.ReturnType != null)
            {
                returnType = WrapInTask(returnType);
            }
            else
            {
                returnType = TaskType();
            }
        }

        // Process decorators to determine modifiers
        var modifiers = GenerateModifiersFromDecorators(func.Decorators);

        // Reorder parameters for C# compliance (required before optional, params last)
        var orderedParams = ReorderParametersForCSharp(func.Parameters);

        // Generate parameters with type annotations
        var parameters = orderedParams
            .Select(GenerateParameter)
            .ToArray();

        // Track parameters as declared variables
        foreach (var param in func.Parameters)
        {
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            // Also track in version map so assignments to parameters work correctly
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Generate method body
        var body = Block(func.Body.SelectMany(GenerateBodyStatements));

        var method = MethodDeclaration(returnType, mangledName)
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
                .Select(GenerateTypeParameterSyntax)
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

        return method;
    }

    private ParameterSyntax GenerateParameter(Parameter param)
    {
        var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);

        // Get parameter type from annotation or default to object
        TypeSyntax paramType = param.Type != null
            ? _typeMapper.MapType(param.Type)
            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

        // For variadic parameters (*args), wrap the element type in an array
        if (param.IsVariadic)
        {
            paramType = ArrayType(paramType)
                .WithRankSpecifiers(SingletonList(ArrayRankSpecifier()));
        }

        var parameter = Parameter(Identifier(paramName))
            .WithType(paramType);

        // For variadic parameters, add the 'params' modifier
        if (param.IsVariadic)
        {
            parameter = parameter.WithModifiers(TokenList(Token(SyntaxKind.ParamsKeyword)));
        }

        // Add default value if present
        if (param.DefaultValue != null)
        {
            ExpressionSyntax defaultExpr;
            // None() as default param → default (Optional<T> is a struct, default = None)
            if (param.DefaultValue is FunctionCall { Function: NoneLiteral } noneCall
                && noneCall.Arguments.Length == 0
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

    private SyntaxTokenList GenerateModifiersFromDecorators(IReadOnlyList<Decorator> decorators)
    {
        var tokens = new List<SyntaxToken>();

        // Check for access modifiers
        bool hasAccessModifier = false;
        foreach (var decorator in decorators)
        {
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

        // Default to public if no access modifier specified
        if (!hasAccessModifier)
        {
            tokens.Add(Token(SyntaxKind.PublicKeyword));
        }

        // Check for other modifiers
        foreach (var decorator in decorators)
        {
            switch (decorator.Name)
            {
                case DecoratorNames.Static:
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
                case DecoratorNames.Abstract:
                    tokens.Add(Token(SyntaxKind.AbstractKeyword));
                    break;
                case DecoratorNames.Virtual:
                    tokens.Add(Token(SyntaxKind.VirtualKeyword));
                    break;
                case DecoratorNames.Override:
                    tokens.Add(Token(SyntaxKind.OverrideKeyword));
                    break;
                case DecoratorNames.Final:
                    tokens.Add(Token(SyntaxKind.SealedKeyword));
                    break;
            }
        }

        // For module-level functions, add static modifier if not already present
        // and if it's not a method (we'll handle this differently in classes)
        if (!tokens.Any(t => t.IsKind(SyntaxKind.StaticKeyword) ||
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

        // Check if this is an abstract class (for implicit abstract method detection)
        var wasInAbstractClass = _isInAbstractClass;
        _isInAbstractClass = classDef.Decorators.Any(d => d.Name == DecoratorNames.Abstract);

        // Transform class name
        var className = NameMangler.Transform(classDef.Name, NameContext.Type);

        // Process decorators to determine modifiers
        var modifiers = GenerateTypeModifiersFromDecorators(classDef.Decorators);

        // Create class declaration
        var classDecl = ClassDeclaration(className)
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
            var baseTypes = classDef.BaseClasses
                .Select(bc => (BaseTypeSyntax)SimpleBaseType(_typeMapper.MapType(bc)))
                .ToList();

            // Synthesize protocol interfaces from dunder methods
            var synthesizedInterfaces = CollectSynthesizedInterfaces(classDef.Body, classDef.BaseClasses, className, classDef.Name);
            baseTypes.AddRange(synthesizedInterfaces);

            if (baseTypes.Count > 0)
            {
                classDecl = classDecl.WithBaseList(BaseList(SeparatedList(baseTypes)));
            }
        }

        // Generate class members from body
        var members = GenerateClassMembers(classDef.Body, className, classDef.Name);

        // For abstract classes implementing interfaces, generate abstract stubs for missing methods
        if (_isInAbstractClass && classDef.BaseClasses.Length > 0)
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

        // Restore previous abstract class context
        _isInAbstractClass = wasInAbstractClass;

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
            _ => ParseName(info.Namespace)
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
                var paramName = NameMangler.Transform(p.Name, NameContext.Parameter);
                TypeSyntax paramType = p.Type is UnknownType or null
                    ? PredefinedType(Token(SyntaxKind.ObjectKeyword))
                    : _typeMapper.MapSemanticType(p.Type);

                // For variadic parameters, wrap in array
                if (p.IsVariadic)
                {
                    paramType = ArrayType(paramType)
                        .WithRankSpecifiers(SingletonList(ArrayRankSpecifier()));
                }

                var param = Parameter(Identifier(paramName)).WithType(paramType);
                if (p.IsVariadic)
                {
                    param = param.WithModifiers(TokenList(Token(SyntaxKind.ParamsKeyword)));
                }
                return param;
            })
            .ToArray();

        // Create abstract method declaration
        return MethodDeclaration(returnType, mangledName)
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
        var structName = NameMangler.Transform(structDef.Name, NameContext.Type);

        // Process decorators to determine modifiers
        var modifiers = GenerateTypeModifiersFromDecorators(structDef.Decorators);

        // Create struct declaration
        var structDecl = StructDeclaration(structName)
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
        var modifiers = GenerateTypeModifiersFromDecorators(interfaceDef.Decorators);

        // Create interface declaration
        var interfaceDecl = InterfaceDeclaration(interfaceName)
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
    /// </summary>
    private static TypeParameterSyntax GenerateTypeParameterSyntax(TypeParameterDef tp)
    {
        var typeParam = TypeParameter(tp.Name);
        return tp.Variance switch
        {
            TypeParameterVariance.Covariant => typeParam.WithVarianceKeyword(Token(SyntaxKind.OutKeyword)),
            TypeParameterVariance.Contravariant => typeParam.WithVarianceKeyword(Token(SyntaxKind.InKeyword)),
            _ => typeParam
        };
    }

    private SyntaxList<TypeParameterConstraintClauseSyntax> GenerateConstraintClauses(
        IReadOnlyList<TypeParameterDef> typeParameters)
    {
        var clauses = new List<TypeParameterConstraintClauseSyntax>();

        foreach (var typeParam in typeParameters)
        {
            if (typeParam.Constraints.Length == 0)
                continue;

            var constraintSyntaxes = new List<TypeParameterConstraintSyntax>();

            // Order: class/struct first, then types, then new()
            var ordered = typeParam.Constraints
                .OrderBy(c => c switch
                {
                    ClassConstraint => 0,
                    StructConstraint => 0,
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

    /// <summary>
    /// Determines if an enum is a string enum (has at least one string literal value)
    /// </summary>
    private bool IsStringEnum(EnumDef enumDef)
    {
        // Check if any member has a string value
        foreach (var member in enumDef.Members)
        {
            if (member.Value is StringLiteral)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a TypeSymbol represents a string enum using CodeGenInfo.
    /// String enums are detected during semantic analysis and stored in CodeGenInfo.IsStringEnum.
    /// </summary>
    private bool IsStringEnumSymbol(TypeSymbol enumSymbol)
    {
        return GetCodeGenInfo(enumSymbol)?.IsStringEnum == true;
    }

    /// <summary>
    /// Generates a C# enum for integer enums
    /// </summary>
    private EnumDeclarationSyntax GenerateIntegerEnum(EnumDef enumDef)
    {
        // Transform enum name
        var enumName = NameMangler.Transform(enumDef.Name, NameContext.Type);

        // Enums are always public by default
        var modifiers = TokenList(Token(SyntaxKind.PublicKeyword));

        // Generate enum members
        var members = enumDef.Members
            .Select(GenerateEnumMember)
            .ToArray();

        var enumDecl = EnumDeclaration(enumName)
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
    /// Generates a sealed class with public static readonly string fields for string enums
    /// </summary>
    private ClassDeclarationSyntax GenerateStringEnumClass(EnumDef enumDef)
    {
        // Transform enum name
        var className = NameMangler.Transform(enumDef.Name, NameContext.Type);

        // Create public sealed class
        var modifiers = TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.SealedKeyword)
        );

        var classDecl = ClassDeclaration(className)
            .WithModifiers(modifiers);

        // Generate public static readonly string fields for each member
        var members = new List<MemberDeclarationSyntax>();

        foreach (var member in enumDef.Members)
        {
            var fieldName = NameMangler.Transform(member.Name, NameContext.Constant);

            // Determine the value - use the explicit value if provided, otherwise use the member name
            ExpressionSyntax valueExpr;
            if (member.Value is StringLiteral strLit)
            {
                valueExpr = GenerateExpression(strLit);
            }
            else
            {
                // Default to the original member name as a string
                valueExpr = LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    Literal(member.Name)
                );
            }

            var field = FieldDeclaration(
                VariableDeclaration(
                    PredefinedType(Token(SyntaxKind.StringKeyword))
                )
                .WithVariables(
                    SingletonSeparatedList(
                        VariableDeclarator(Identifier(fieldName))
                            .WithInitializer(EqualsValueClause(valueExpr))
                    )
                )
            )
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword),
                Token(SyntaxKind.ReadOnlyKeyword)
            ));

            members.Add(field);
        }

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

    private SyntaxTokenList GenerateTypeModifiersFromDecorators(IReadOnlyList<Decorator> decorators)
    {
        var tokens = new List<SyntaxToken>();

        // Check for access modifiers
        bool hasAccessModifier = false;
        foreach (var decorator in decorators)
        {
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

        // Default to public if no access modifier specified
        if (!hasAccessModifier)
        {
            tokens.Add(Token(SyntaxKind.PublicKeyword));
        }

        // Check for other modifiers
        foreach (var decorator in decorators)
        {
            switch (decorator.Name)
            {
                case DecoratorNames.Abstract:
                    tokens.Add(Token(SyntaxKind.AbstractKeyword));
                    break;
                case "sealed":
                case DecoratorNames.Final:
                    tokens.Add(Token(SyntaxKind.SealedKeyword));
                    break;
                case DecoratorNames.Static:
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
            }
        }

        return TokenList(tokens);
    }

    /// <summary>
    /// Generates C# attribute lists from decorators that are not known modifier decorators.
    /// Unknown decorators are emitted as C# attributes with name mangling (snake_case → PascalCase).
    /// Dotted names become qualified names. Arguments and keyword arguments are mapped to attribute arguments.
    /// </summary>
    private SyntaxList<AttributeListSyntax> GenerateAttributeListsFromDecorators(IReadOnlyList<Decorator> decorators)
    {
        var attributeLists = new List<AttributeListSyntax>();

        foreach (var decorator in decorators)
        {
            if (DecoratorNames.KnownModifierDecorators.Contains(decorator.Name))
                continue;

            // Skip @dataclass — it's handled by dataclass codegen, not emitted as an attribute
            if (decorator.Name == DecoratorNames.Dataclass)
                continue;

            // Build the attribute name
            NameSyntax attributeName;
            if (decorator.QualifiedParts.Length > 0)
            {
                // Dotted name: build QualifiedNameSyntax from parts, each PascalCase-mangled
                attributeName = IdentifierName(NameMangler.ToPascalCase(decorator.QualifiedParts[0]));
                for (int i = 1; i < decorator.QualifiedParts.Length; i++)
                {
                    attributeName = QualifiedName(attributeName, IdentifierName(NameMangler.ToPascalCase(decorator.QualifiedParts[i])));
                }
            }
            else
            {
                attributeName = IdentifierName(NameMangler.ToPascalCase(decorator.Name));
            }

            var attribute = Attribute(attributeName);

            // Build attribute argument list if there are arguments
            if (decorator.Arguments.Length > 0 || decorator.KeywordArguments.Length > 0)
            {
                var args = new List<AttributeArgumentSyntax>();

                // Positional arguments
                foreach (var arg in decorator.Arguments)
                {
                    args.Add(AttributeArgument(GenerateAttributeArgumentExpression(arg)));
                }

                // Keyword arguments: name=value → NameEquals
                foreach (var kwArg in decorator.KeywordArguments)
                {
                    var nameEquals = NameEquals(IdentifierName(NameMangler.ToPascalCase(kwArg.Name)));
                    args.Add(AttributeArgument(GenerateAttributeArgumentExpression(kwArg.Value))
                        .WithNameEquals(nameEquals));
                }

                attribute = attribute.WithArgumentList(AttributeArgumentList(SeparatedList(args)));
            }

            attributeLists.Add(AttributeList(SingletonSeparatedList(attribute)));
        }

        return List(attributeLists);
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
                IdentifierName(NameMangler.ToPascalCase(objId.Name)),
                IdentifierName(NameMangler.ToPascalCase(memberAccess.Member))),
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

        var unionName = NameMangler.Transform(unionDef.Name, NameContext.Type);

        // Look up the union symbol for field type information
        var unionSymbol = _context.LookupSymbol(unionDef.Name) as TypeSymbol;

        // Create abstract base class with public modifier
        var classDecl = ClassDeclaration(unionName)
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
        var privateCtor = ConstructorDeclaration(Identifier(unionName))
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
        var caseDecl = ClassDeclaration(caseName)
            .WithModifiers(TokenList(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.SealedKeyword)));

        // Base type: union base class (with type arguments if generic)
        TypeSyntax baseType;
        if (typeParams.Length > 0)
        {
            var typeArgs = typeParams
                .Select(tp => (TypeSyntax)IdentifierName(tp.Name))
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
            var propName = NameMangler.ToPascalCase(field.Name);
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
                Parameter(Identifier(NameMangler.ToCamelCase(f.Name)))
                    .WithType(_typeMapper.MapSemanticType(f.Type)))
                .ToArray();

            var ctorBody = fields.Select(f =>
            {
                var propName = NameMangler.ToPascalCase(f.Name);
                var paramName = NameMangler.ToCamelCase(f.Name);
                return (StatementSyntax)ExpressionStatement(
                    AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName(propName),
                        IdentifierName(paramName)));
            }).ToArray();

            var ctor = ConstructorDeclaration(Identifier(caseName))
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
            var ctor = ConstructorDeclaration(Identifier(caseName))
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
            Parameter(Identifier(NameMangler.ToCamelCase(f.Name)))
                .WithType(_typeMapper.MapSemanticType(f.Type))
                .WithModifiers(TokenList(Token(SyntaxKind.OutKeyword))))
            .ToArray();

        var body = fields.Select(f =>
        {
            var paramName = NameMangler.ToCamelCase(f.Name);
            var propName = NameMangler.ToPascalCase(f.Name);
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
        var delegateName = NameMangler.Transform(delegateDef.Name, NameContext.Type);

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

        var delegateDecl = DelegateDeclaration(returnType, delegateName)
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
