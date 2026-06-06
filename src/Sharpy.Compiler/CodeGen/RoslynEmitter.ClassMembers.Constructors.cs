using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// RoslynEmitter partial class: Constructor generation
/// </summary>
internal partial class RoslynEmitter
{
    private ConstructorDeclarationSyntax GenerateConstructor(
        FunctionDef func,
        string className,
        Dictionary<string, string> fieldMapping,
        Dictionary<string, TypeAnnotation> fieldTypeMapping)
    {
        // Clear declared variables and version tracking for new method scope
        ResetMethodScope();

        // Pre-scan the constructor body to collect all variable names that will be declared.
        // This enables us to avoid generating versioned names (x_1, x_2) that collide
        // with user-declared variables.
        CollectSourceVariableNames(func.Body);

        // Process decorators to determine modifiers
        var modifiers = GenerateMethodModifiers(func.Name, func.Decorators);

        // Generate parameters with type annotations, skipping 'self' parameter
        // Reorder for C# compliance (required before optional, params last)
        var filteredParams = func.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        var orderedParams = ReorderParametersForCSharp(filteredParams);
        var parameters = orderedParams
            .Select(GenerateParameter)
            .ToArray();

        // Create a mapping of parameter names (original) to their mangled names
        var parameterMapping = func.Parameters
            .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                p => p.Name,
                p => NameMangler.Transform(p.Name, NameContext.Parameter));

        // Track parameters as declared variables
        foreach (var param in func.Parameters)
        {
            if (string.Equals(param.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                continue;
            var paramName = NameMangler.Transform(param.Name, NameContext.Parameter);
            _declaredVariables.Add(paramName);
            // Also track in version map so assignments to parameters work correctly
            var baseName = NameMangler.ToCamelCase(param.Name);
            _variableVersions[baseName] = 0;
        }

        // Find super().__init__() or self.__init__() anywhere in the body and convert to
        // constructor initializer (: base(...) or : this(...))
        ConstructorInitializerSyntax? constructorInitializer = null;
        int initializerCallIndex = -1;

        for (int i = 0; i < func.Body.Length; i++)
        {
            if (func.Body[i] is ExpressionStatement es &&
                es.Expression is FunctionCall initCall &&
                initCall.Function is MemberAccess ma &&
                ma.Member == DunderNames.Init)
            {
                if (ma.Object is SuperExpression)
                {
                    initializerCallIndex = i;
                    var baseArgs = initCall.Arguments.Select(arg => Argument(GenerateExpression(arg))).ToArray();
                    constructorInitializer = ConstructorInitializer(
                        SyntaxKind.BaseConstructorInitializer,
                        ArgumentList(SeparatedList(baseArgs)));
                    break;
                }

                if (ma.Object is Identifier { Name: PythonNames.Self })
                {
                    initializerCallIndex = i;
                    var thisArgs = initCall.Arguments.Select(arg => Argument(GenerateExpression(arg))).ToArray();
                    constructorInitializer = ConstructorInitializer(
                        SyntaxKind.ThisConstructorInitializer,
                        ArgumentList(SeparatedList(thisArgs)));
                    break;
                }
            }
        }

        var bodyStartIndex = 0;

        if (initializerCallIndex >= 0)
        {
            if (initializerCallIndex == 0)
            {
                // Simple case: initializer call is the first statement, skip it
                bodyStartIndex = 1;
            }
            // else: initializer call is not the first statement.
            // We still emit the initializer and skip the call during body generation.
            // Statements before the call are emitted as regular constructor body.
        }

        // Generate constructor body
        // In Python __init__, assignments like self.name = name set instance fields
        // In C#, these become this.Name = name in the constructor body
        var bodyStatements = new List<StatementSyntax>();

        for (int i = bodyStartIndex; i < func.Body.Length; i++)
        {
            // Skip the initializer call — it was already converted to : base(...) or : this(...)
            if (i == initializerCallIndex)
                continue;

            var stmt = func.Body[i];

            // Convert self.field = value to this.Field = value (capitalized)
            if (stmt is Assignment assign)
            {
                // Check if this is a self.field assignment
                if (assign.Target is MemberAccess memberAccess &&
                    memberAccess.Object is Identifier id &&
                    string.Equals(id.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                {
                    // Look up the field name from the field mapping to ensure consistency
                    // For fields not in mapping (inherited fields), use PascalCase to match
                    // the convention used by GenerateField
                    string fieldName = fieldMapping.TryGetValue(memberAccess.Member, out var mappedFieldName)
                        ? mappedFieldName
                        : NameCasing.ResolveField(memberAccess.Member, false);

                    // Generate: this.Field = value;
                    var thisAccess = MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        IdentifierName(fieldName));

                    // For the right-hand side, check if it's an identifier that matches a parameter
                    ExpressionSyntax assignValue;
                    if (assign.Value is Identifier valueId && parameterMapping.TryGetValue(valueId.Name, out var mappedName))
                    {
                        assignValue = IdentifierName(mappedName);
                    }
                    else
                    {
                        // Set target type context for collection literal inference (e.g., self.items = [])
                        var previousTargetType = _targetTypeContext;
                        if (fieldTypeMapping.TryGetValue(memberAccess.Member, out var fieldType))
                        {
                            _targetTypeContext = fieldType;
                        }
                        try
                        {
                            assignValue = GenerateExpression(assign.Value);
                        }
                        finally
                        {
                            _targetTypeContext = previousTargetType;
                        }

                        // Method group → Optional<delegate> field needs an explicit delegate cast
                        assignValue = ApplyOptionalDelegateConversion(
                            assign.Value, assignValue, GetExpressionSemanticType(assign.Target));
                    }

                    var selfAssign = ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            thisAccess,
                            assignValue));
                    bodyStatements.Add(AttachLineDirective(selfAssign, stmt));
                }
                else
                {
                    // Other assignments, generate normally
                    bodyStatements.AddRange(GenerateBodyStatements(stmt));
                }
            }
            else
            {
                // Other statements, generate normally
                bodyStatements.AddRange(GenerateBodyStatements(stmt));
            }
        }

        var body = AttachLineDirectiveToBlock(Block(bodyStatements), func.LineStart);

        var constructor = ConstructorDeclaration(className)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add C# attributes from unknown decorators
        var ctorAttributes = GenerateAttributeListsFromDecorators(func.Decorators);
        if (ctorAttributes.Count > 0)
        {
            constructor = constructor.WithAttributeLists(ctorAttributes);
        }

        // Add constructor initializer if present (: base(...) or : this(...))
        if (constructorInitializer != null)
        {
            constructor = constructor.WithInitializer(constructorInitializer);
        }

        // Add XML documentation from docstring if present
        if (!string.IsNullOrEmpty(func.DocString))
        {
            constructor = constructor.WithLeadingTrivia(GenerateXmlDocComment(func.DocString));
        }

        return constructor;
    }

    /// <summary>
    /// Generates auto-constructor(s) for a struct that has fields but no explicit __init__.
    /// Required fields (no default) become required parameters; fields with defaults become
    /// optional parameters. Required parameters precede optional ones.
    /// When all fields have defaults, also generates an explicit parameterless constructor
    /// so that <c>new T()</c> uses the declared defaults rather than zero-initialization.
    /// </summary>
    private List<ConstructorDeclarationSyntax> GenerateStructAutoConstructors(
        string className,
        IReadOnlyList<Statement> body)
    {
        var constructors = new List<ConstructorDeclarationSyntax>();

        // Collect instance field declarations in body order
        var fieldDecls = body.OfType<VariableDeclaration>()
            .Where(v => !v.Decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Static))
            .ToList();

        // Partition into required (no default) and optional (with default), preserving order within each group
        var requiredFields = fieldDecls.Where(f => f.InitialValue == null).ToList();
        var optionalFields = fieldDecls.Where(f => f.InitialValue != null).ToList();
        var orderedFields = requiredFields.Concat(optionalFields).ToList();

        // When all fields have defaults, generate an explicit parameterless constructor.
        // Without this, `new T()` on a struct uses zero-initialization and skips
        // the constructor whose parameters all happen to be optional.
        if (requiredFields.Count == 0 && optionalFields.Count > 0)
        {
            var parameterlessStatements = new List<StatementSyntax>();
            foreach (var fieldDecl in optionalFields)
            {
                var fieldSymbol = _currentTypeSymbol?.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
                var propName = fieldSymbol != null
                    ? (GetCodeGenInfo(fieldSymbol)?.CSharpName ?? NameCasing.ResolveField(fieldDecl.Name, fieldDecl.IsNameBacktickEscaped))
                    : NameCasing.ResolveField(fieldDecl.Name, fieldDecl.IsNameBacktickEscaped);

                var previousTargetType = _targetTypeContext;
                _targetTypeContext = fieldDecl.Type;
                try
                {
                    var defaultExpr = GenerateExpression(fieldDecl.InitialValue!);
                    parameterlessStatements.Add(ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                ThisExpression(),
                                IdentifierName(propName)),
                            defaultExpr)));
                }
                finally
                {
                    _targetTypeContext = previousTargetType;
                }
            }

            constructors.Add(ConstructorDeclaration(Identifier(className))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList())
                .WithBody(Block(parameterlessStatements)));
        }

        // Build the main constructor with parameters
        var parameters = new List<ParameterSyntax>();
        foreach (var fieldDecl in orderedFields)
        {
            var paramName = fieldDecl.Name;
            TypeSyntax paramType = fieldDecl.Type != null
                ? _typeMapper.MapType(fieldDecl.Type)
                : PredefinedType(Token(SyntaxKind.ObjectKeyword));

            var param = Parameter(Identifier(paramName))
                .WithType(paramType);

            // Add default value if present
            if (fieldDecl.InitialValue != null)
            {
                var previousTargetType = _targetTypeContext;
                _targetTypeContext = fieldDecl.Type;
                try
                {
                    var defaultExpr = GenerateExpression(fieldDecl.InitialValue);
                    param = param.WithDefault(EqualsValueClause(defaultExpr));
                }
                finally
                {
                    _targetTypeContext = previousTargetType;
                }
            }

            parameters.Add(param);
        }

        // Build constructor body: assign all fields
        var statements = new List<StatementSyntax>();
        foreach (var fieldDecl in orderedFields)
        {
            var fieldSymbol = _currentTypeSymbol?.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
            var propName = fieldSymbol != null
                ? (GetCodeGenInfo(fieldSymbol)?.CSharpName ?? NameCasing.ResolveField(fieldDecl.Name, fieldDecl.IsNameBacktickEscaped))
                : NameCasing.ResolveField(fieldDecl.Name, fieldDecl.IsNameBacktickEscaped);
            var paramName = fieldDecl.Name;

            statements.Add(ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        ThisExpression(),
                        IdentifierName(propName)),
                    IdentifierName(paramName))));
        }

        // Only add the parameterized constructor if it has at least one parameter
        // (avoids duplicate when parameterless was already generated above)
        if (orderedFields.Count > 0)
        {
            constructors.Add(ConstructorDeclaration(Identifier(className))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithBody(Block(statements)));
        }

        return constructors;
    }

    /// <summary>
    /// Generates forwarding constructors for a class that doesn't define __init__
    /// but inherits from a class that has constructors with parameters.
    /// C# doesn't inherit constructors, so we must explicitly forward them.
    /// Walks up the inheritance chain to find the nearest ancestor with __init__.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateForwardingConstructors(string className)
    {
        var constructors = new List<MemberDeclarationSyntax>();

        // Walk up inheritance chain to find nearest ancestor with __init__
        // Use SemanticBinding first (consistent with base list generation at line 1194)
        var ancestor = _currentTypeSymbol is not null
            ? _context.SemanticBinding.GetBaseType(_currentTypeSymbol) ?? _currentTypeSymbol.BaseType
            : null;
        while (ancestor != null)
        {
            var initMethods = ancestor.Constructors;
            if (initMethods.Count > 0)
            {
                foreach (var initMethod in initMethods)
                {
                    // Skip parameterless constructors — C# handles these automatically
                    var nonSelfParams = initMethod.Parameters
                        .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (nonSelfParams.Count == 0)
                        continue;

                    // Reorder for C# compliance (required before optional, params last)
                    var orderedNonSelfParams = ReorderParameterSymbolsForCSharp(nonSelfParams);

                    // Generate parameter list from semantic ParameterSymbol data
                    var parameters = orderedNonSelfParams.Select(p =>
                    {
                        var paramName = NameMangler.Transform(p.Name, NameContext.Parameter);
                        var paramType = p.Type is not null and not UnknownType
                            ? _typeMapper.MapSemanticType(p.Type)
                            : PredefinedType(Token(SyntaxKind.ObjectKeyword));

                        // For variadic parameters, wrap the element type in an array
                        if (p.IsVariadic)
                        {
                            paramType = ArrayType(paramType)
                                .WithRankSpecifiers(SingletonList(ArrayRankSpecifier()));
                        }

                        var paramSyntax = Parameter(Identifier(paramName)).WithType(paramType);

                        // For variadic parameters, add the 'params' modifier
                        if (p.IsVariadic)
                        {
                            paramSyntax = paramSyntax.WithModifiers(TokenList(Token(SyntaxKind.ParamsKeyword)));
                        }

                        // Handle default values
                        if (p.DefaultValue != null)
                        {
                            paramSyntax = paramSyntax.WithDefault(
                                EqualsValueClause(GenerateExpression(p.DefaultValue)));
                        }

                        return paramSyntax;
                    }).ToArray();

                    // Generate base constructor call arguments (same reordered order)
                    var baseArgs = orderedNonSelfParams.Select(p =>
                    {
                        var paramName = NameMangler.Transform(p.Name, NameContext.Parameter);
                        return Argument(IdentifierName(paramName));
                    }).ToArray();

                    var ctor = ConstructorDeclaration(Identifier(className))
                        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                        .WithParameterList(ParameterList(SeparatedList(parameters)))
                        .WithInitializer(ConstructorInitializer(
                            SyntaxKind.BaseConstructorInitializer,
                            ArgumentList(SeparatedList(baseArgs))))
                        .WithBody(Block());

                    constructors.Add(ctor);
                }
                break; // Only forward from nearest ancestor with constructors
            }
            ancestor = _context.SemanticBinding.GetBaseType(ancestor) ?? ancestor.BaseType;
        }

        return constructors;
    }

}
