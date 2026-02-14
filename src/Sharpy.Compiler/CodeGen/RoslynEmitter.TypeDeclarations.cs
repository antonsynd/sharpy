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
        _declaredVariables.Clear();
        _variableVersions.Clear();
        _constVariables.Clear();
        _sourceVariableNames.Clear();
        _narrowedOptionals.Clear();
        _isNullableNarrowing.Clear();

        // Pre-scan the function body to collect all variable names that will be declared.
        // This enables us to avoid generating versioned names (x_1, x_2) that collide
        // with user-declared variables.
        CollectSourceVariableNames(func.Body);

        // Transform name using NameMangler
        // Special case: only convert "main" to "Main" if this is the entry point file
        var mangledName = func.Name == "main" && !_context.IsEntryPoint
            ? "MainFunc"  // Rename to avoid C# entry point conflict in non-entry files
            : NameMangler.Transform(func.Name, NameContext.Method);

        // Determine return type from annotation or infer void
        TypeSyntax returnType = func.ReturnType != null
            ? _typeMapper.MapType(func.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Process decorators to determine modifiers
        var modifiers = GenerateModifiersFromDecorators(func.Decorators);

        // Generate parameters with type annotations
        var parameters = func.Parameters
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

        // Add type parameters if generic
        if (func.TypeParameters.Length > 0)
        {
            var typeParams = func.TypeParameters
                .Select(tp => TypeParameter(tp.Name))
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
                case "private":
                    tokens.Add(Token(SyntaxKind.PrivateKeyword));
                    hasAccessModifier = true;
                    break;
                case "protected":
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    hasAccessModifier = true;
                    break;
                case "internal":
                    tokens.Add(Token(SyntaxKind.InternalKeyword));
                    hasAccessModifier = true;
                    break;
                case "public":
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
                case "static":
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
                case "abstract":
                    tokens.Add(Token(SyntaxKind.AbstractKeyword));
                    break;
                case "virtual":
                    tokens.Add(Token(SyntaxKind.VirtualKeyword));
                    break;
                case "override":
                    tokens.Add(Token(SyntaxKind.OverrideKeyword));
                    break;
                case "final":
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
        _isInAbstractClass = classDef.Decorators.Any(d => d.Name == "abstract");

        // Transform class name
        var className = NameMangler.Transform(classDef.Name, NameContext.Type);

        // Process decorators to determine modifiers
        var modifiers = GenerateTypeModifiersFromDecorators(classDef.Decorators);

        // Create class declaration
        var classDecl = ClassDeclaration(className)
            .WithModifiers(modifiers);

        // Add type parameters if generic
        if (classDef.TypeParameters.Length > 0)
        {
            var typeParams = classDef.TypeParameters
                .Select(tp => TypeParameter(tp.Name))
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
            var interfaceMethods = CollectInterfaceMethodDefs(classDef.BaseClasses);
            var definedMethods = GetDefinedMethodNames(classDef.Body);

            var stubMembers = new List<MemberDeclarationSyntax>();

            foreach (var interfaceMethod in interfaceMethods)
            {
                // Skip if method is already defined in the class
                if (definedMethods.Contains(interfaceMethod.Name))
                    continue;

                // Generate abstract stub
                var stub = GenerateAbstractMethodStub(interfaceMethod);
                stubMembers.Add(stub);
            }

            // Add stubs to members list
            if (stubMembers.Count > 0)
            {
                members = members.Concat(stubMembers).ToList();
            }
        }

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
    /// Collects all method FunctionDefs from interfaces that a class implements.
    /// Recursively collects from base interfaces as well.
    /// </summary>
    private List<FunctionDef> CollectInterfaceMethodDefs(IReadOnlyList<TypeAnnotation> baseTypes)
    {
        var result = new List<FunctionDef>();
        var visited = new HashSet<string>();
        var seenMethods = new HashSet<string>();

        void CollectFromInterface(string interfaceName)
        {
            if (visited.Contains(interfaceName))
                return;
            visited.Add(interfaceName);

            // Look up the interface definition
            if (!_interfaceDefinitions.TryGetValue(interfaceName, out var interfaceDef))
                return;

            // Collect methods from this interface
            foreach (var stmt in interfaceDef.Body)
            {
                if (stmt is FunctionDef funcDef)
                {
                    // Skip if we've already seen a method with this name
                    if (seenMethods.Contains(funcDef.Name))
                        continue;
                    seenMethods.Add(funcDef.Name);
                    result.Add(funcDef);
                }
            }

            // Recursively collect from base interfaces
            foreach (var baseInterface in interfaceDef.BaseInterfaces)
            {
                var baseName = baseInterface.Name;
                if (!string.IsNullOrEmpty(baseName))
                {
                    CollectFromInterface(baseName);
                }
            }
        }

        foreach (var baseType in baseTypes)
        {
            var typeName = baseType.Name;
            if (string.IsNullOrEmpty(typeName))
                continue;

            // Check if this is an interface (exists in our interface definitions)
            if (_interfaceDefinitions.ContainsKey(typeName))
            {
                CollectFromInterface(typeName);
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
    /// </summary>
    private MethodDeclarationSyntax GenerateAbstractMethodStub(FunctionDef interfaceMethod)
    {
        var mangledName = DunderMapping.ResolveCSharpName(interfaceMethod.Name)
            ?? NameMangler.Transform(interfaceMethod.Name, NameContext.Method);

        // Determine return type from annotation or infer void
        TypeSyntax returnType = interfaceMethod.ReturnType != null
            ? _typeMapper.MapType(interfaceMethod.ReturnType)
            : PredefinedType(Token(SyntaxKind.VoidKeyword));

        // Generate parameters (skip 'self')
        var parameters = interfaceMethod.Parameters
            .Where(p => p.Name != PythonNames.Self)
            .Select(GenerateParameter)
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

        // Add type parameters if generic
        if (structDef.TypeParameters.Length > 0)
        {
            var typeParams = structDef.TypeParameters
                .Select(tp => TypeParameter(tp.Name))
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

        // Interfaces are always public by default
        var modifiers = TokenList(Token(SyntaxKind.PublicKeyword));

        // Create interface declaration
        var interfaceDecl = InterfaceDeclaration(interfaceName)
            .WithModifiers(modifiers);

        // Add type parameters if generic
        if (interfaceDef.TypeParameters.Length > 0)
        {
            var typeParams = interfaceDef.TypeParameters
                .Select(tp => TypeParameter(tp.Name))
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
                case "private":
                    tokens.Add(Token(SyntaxKind.PrivateKeyword));
                    hasAccessModifier = true;
                    break;
                case "protected":
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    hasAccessModifier = true;
                    break;
                case "internal":
                    tokens.Add(Token(SyntaxKind.InternalKeyword));
                    hasAccessModifier = true;
                    break;
                case "public":
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
                case "abstract":
                    tokens.Add(Token(SyntaxKind.AbstractKeyword));
                    break;
                case "sealed":
                case "final":
                    tokens.Add(Token(SyntaxKind.SealedKeyword));
                    break;
                case "static":
                    tokens.Add(Token(SyntaxKind.StaticKeyword));
                    break;
            }
        }

        return TokenList(tokens);
    }

    #endregion
}
