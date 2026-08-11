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
            RegisterLocalSlot(baseName, param.Name);
        }

        // Find super().__init__() or self.__init__() anywhere in the body and convert to
        // constructor initializer (: base(...) or : this(...))
        ConstructorInitializerSyntax? constructorInitializer = null;
        int initializerCallIndex = -1;

        for (int i = 0; i < func.Body.Length; i++)
        {
            // Matched against the canonical (paren-stripped) callee, as GenerateCall does (#1147,
            // #1170): `(self.__init__)(x)` is the same initializer call as `self.__init__(x)`, and
            // missing it here would emit a `self.Constructor(...)` member invocation instead. The
            // statement's own expression is stripped for the same reason (#1197): `(super().__init__())`
            // is the same initializer call, and missing it here reports SPY0501 from the fallback arm
            // in GenerateCall — an accepted program rejected purely for a redundant grouping.
            if (func.Body[i] is ExpressionStatement es &&
                Shared.AstHelper.UnwrapParenthesized(es.Expression) is FunctionCall initCall &&
                Shared.AstHelper.UnwrapParenthesized(initCall.Function) is MemberAccess ma &&
                ma.Member == DunderNames.Init)
            {
                if (ma.Object is SuperExpression)
                {
                    initializerCallIndex = i;
                    // Route through the regular call-argument pipeline so keyword arguments
                    // (FunctionCall.KeywordArguments) reach the base constructor instead of
                    // being silently dropped (#906). Resolve the target constructor symbol when
                    // available for correct reordering; otherwise the pipeline falls back to
                    // positional + C# named arguments so nothing is lost.
                    var baseArgs = GenerateReorderedCallArguments(
                        initCall, _context.SemanticInfo?.GetCallTarget(initCall));
                    constructorInitializer = ConstructorInitializer(
                        SyntaxKind.BaseConstructorInitializer,
                        ArgumentList(SeparatedList(baseArgs)));
                    break;
                }

                if (ma.Object is Identifier { Name: PythonNames.Self })
                {
                    initializerCallIndex = i;
                    var thisArgs = GenerateReorderedCallArguments(
                        initCall, _context.SemanticInfo?.GetCallTarget(initCall));
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

        // Generate constructor body.
        // In Python __init__, assignments like self.name = name set instance fields;
        // in C# these become this.Name = name in the constructor body.
        //
        // The body is routed through GenerateSuite so a `defer` in the constructor wraps the
        // remainder of the body in try/finally (#1065). The per-statement special-casing
        // (self.field → this.Field mapping, collection-literal target-type context,
        // Optional-delegate conversion) is threaded in as the suite's statement generator so it
        // survives the defer split.
        List<StatementSyntax> GenerateConstructorBodyStatement(Statement stmt)
        {
            // @suppress wrapper (#1024): decorators are compile-time-only; the inner
            // statement must still get the constructor's field-assignment conversion.
            if (stmt is DecoratedStatement decorated)
                stmt = decorated.Statement;

            // Convert self.field = value to this.Field = value (capitalized)
            if (stmt is Assignment assign &&
                assign.Target is MemberAccess memberAccess &&
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
                    EscapedIdentifierName(fieldName));

                // For the right-hand side, check if it's an identifier that matches a parameter
                ExpressionSyntax assignValue;
                if (assign.Value is Identifier valueId && parameterMapping.TryGetValue(valueId.Name, out var mappedName))
                {
                    assignValue = EscapedIdentifierName(mappedName);
                }
                else
                {
                    // Set target type context for collection literal inference (e.g., self.items = [])
                    var previousTargetType = _targetTypeContext;
                    var hasFieldType = fieldTypeMapping.TryGetValue(memberAccess.Member, out var fieldType);
                    if (hasFieldType)
                    {
                        _targetTypeContext = fieldType;
                    }
                    try
                    {
                        // `self.field = None` for an Optional<T> field → Optional<T>.None.
                        assignValue = (hasFieldType
                                ? GenerateInitializerValue(assign.Value, fieldType)
                                : null)
                            ?? GenerateExpression(assign.Value);
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
                return new List<StatementSyntax> { AttachLineDirective(selfAssign, stmt) };
            }

            // Other statements, generate normally
            return GenerateBodyStatements(stmt);
        }

        var constructorBody = new List<Statement>();
        for (int i = bodyStartIndex; i < func.Body.Length; i++)
        {
            // Skip the initializer call — it was already converted to : base(...) or : this(...)
            if (i == initializerCallIndex)
                continue;
            constructorBody.Add(func.Body[i]);
        }

        var bodyStatements = GenerateSuite(constructorBody, GenerateConstructorBodyStatement);

        var body = AttachLineDirectiveToBlock(Block(bodyStatements), func.LineStart);

        var constructor = ConstructorDeclaration(EscapedIdentifier(className))
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
                    var defaultExpr = GenerateInitializerValue(fieldDecl.InitialValue!, fieldDecl.Type);
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

            constructors.Add(ConstructorDeclaration(EscapedIdentifier(className))
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

            var param = Parameter(EscapedIdentifier(paramName))
                .WithType(paramType);

            // Add default value if present
            if (fieldDecl.InitialValue != null)
            {
                var previousTargetType = _targetTypeContext;
                _targetTypeContext = fieldDecl.Type;
                try
                {
                    var defaultExpr = GenerateInitializerValue(fieldDecl.InitialValue, fieldDecl.Type);
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
                    EscapedIdentifierName(paramName))));
        }

        // Only add the parameterized constructor if it has at least one parameter
        // (avoids duplicate when parameterless was already generated above)
        if (orderedFields.Count > 0)
        {
            constructors.Add(ConstructorDeclaration(EscapedIdentifier(className))
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
        // The forwarders' SIGNATURES are a semantic decision, materialized per derived class on
        // CodeGenInfo (#1408): the ancestor's Constructors carry the base's OPEN parameter types
        // (`List[T].List(IEnumerable[T])` is one shared FunctionSymbol across every instantiation), so
        // emitting them verbatim put a `Sharpy.List<T>` into a class with no `T`. Substituting here
        // would make the emitter decide a type, which Rule 2 forbids; it reads the frozen fact instead.
        //
        // Read from the MATERIALIZED property, not the binding-first GetCodeGenInfo helper: the
        // bridging happens at MaterializeCodeGenInfo, which writes Symbol.CodeGenInfo, so the raw
        // binding entry the helper prefers never carries this field. Same reason #1122's
        // OverridesClrBaseMember is read as `symbol.CodeGenInfo?.…` at
        // RoslynEmitter.ClassMembers.Methods.cs:155.
        if (_currentTypeSymbol?.CodeGenInfo?.ForwardingConstructors is { } substituted)
        {
            return GenerateForwardersFrom(className, substituted);
        }

        // No materialized fact — the base chain's arguments could not be read, so there is nothing to
        // substitute and the ancestor's own signatures are the only answer available (#1287 DD2).
        // Walk up the inheritance chain to the nearest ancestor with __init__.
        // Use SemanticBinding first (consistent with base list generation at line 1194)
        var ancestor = _currentTypeSymbol is not null
            ? _context.SemanticBinding.GetBaseType(_currentTypeSymbol) ?? _currentTypeSymbol.BaseType
            : null;
        while (ancestor != null)
        {
            if (ancestor.Constructors.Count > 0)
                return GenerateForwardersFrom(className, ancestor.Constructors);

            ancestor = _context.SemanticBinding.GetBaseType(ancestor) ?? ancestor.BaseType;
        }

        return new List<MemberDeclarationSyntax>();
    }

    /// <summary>
    /// Emits one forwarding constructor per entry of <paramref name="inheritedConstructors"/>, whose
    /// parameter types are taken verbatim.
    /// </summary>
    private List<MemberDeclarationSyntax> GenerateForwardersFrom(
        string className, IReadOnlyList<FunctionSymbol> inheritedConstructors)
    {
        var constructors = new List<MemberDeclarationSyntax>();

        // A parameterless base constructor needs a forwarder only when some OTHER forwarder
        // is emitted. C# supplies an implicit `E()` for a class that declares no constructor
        // at all, so emitting one where it is the only forwarder would be noise — but the
        // moment any parameterised forwarder is declared, the implicit one is gone, and
        // `E()` stops compiling. That is the shape #1408 reported as a dropped 0-arg
        // overload, and it is the reason `raise E()` must be re-checked whenever `raise
        // E('boom')` is fixed (#1367): the two are one decision, not two.
        var forwardable = inheritedConstructors
            .Select(m => (Method: m, NonSelf: m.Parameters
                .Where(p => !string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase))
                .ToList()))
            .ToList();
        var emitsParameterised = forwardable.Any(f => f.NonSelf.Count > 0);

        foreach (var (_, nonSelfParams) in forwardable)
        {
            if (nonSelfParams.Count == 0 && !emitsParameterised)
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
                    paramType = VariadicArrayType(paramType);
                }

                var paramSyntax = Parameter(EscapedIdentifier(paramName)).WithType(paramType);

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
                return Argument(EscapedIdentifierName(paramName));
            }).ToArray();

            var ctor = ConstructorDeclaration(EscapedIdentifier(className))
                .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(ParameterList(SeparatedList(parameters)))
                .WithInitializer(ConstructorInitializer(
                    SyntaxKind.BaseConstructorInitializer,
                    ArgumentList(SeparatedList(baseArgs))))
                .WithBody(Block());

            constructors.Add(ctor);
        }

        return constructors;
    }

}
