using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates decorator usage across all definitions.
///
/// Currently validates:
/// - @staticmethod is not supported (methods without 'self' are automatically static)
/// - @final without @override on methods (SPY0412)
///
/// This validator runs early (Order 60) to catch decorator errors before
/// other validators attempt to process the decorated definitions.
/// </summary>
internal partial class DecoratorValidator : ValidatingAstWalker
{
    public override string Name => "DecoratorValidator";
    public override int Order => 60; // After ModuleLevelValidator (50), before SignatureValidator (150)

    private ICompilerLogger _logger = NullLogger.Instance;

    /// <summary>
    /// Tracks the containing type context during traversal.
    /// Null at module level; set to the type name/kind when inside a class, struct, or interface.
    /// </summary>
    private ContainingTypeInfo? _containingType;

    /// <summary>
    /// Set of decorators that are explicitly unsupported with their error messages.
    /// </summary>
    private static readonly Dictionary<string, string> UnsupportedDecorators = new()
    {
        [DecoratorNames.StaticMethod] = "The '@staticmethod' decorator is not supported in Sharpy. " +
                           "Methods without a 'self' parameter are automatically static. " +
                           "Use @static for static fields.",
        [DecoratorNames.ClassMethod] = "The '@classmethod' decorator is not supported in Sharpy. " +
                          "Methods without a 'self' parameter are automatically static. " +
                          "Use @static for static fields.",
    };

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        _logger.LogDebug("Starting decorator validation");
        _containingType = null;
        base.Validate(module, context);
    }

    public override void VisitFunctionDef(FunctionDef node)
    {
        var definitionName = _containingType != null
            ? $"{_containingType.Name}.{node.Name}"
            : node.Name;
        ValidateDecorators(node.Decorators, definitionName);
        ValidateAccessModifierDecorators(node.Decorators, node.Name, definitionName);
        ValidateReadonlyNotOnNonProperty(node.Decorators, definitionName, "function");
        ValidateLruCacheArguments(node.Decorators, definitionName);
        ValidateTestDecorator(node.Decorators, node.Name, isDunder: DunderDetector.IsDunderMethod(node.Name));
        ValidateTestParametrizeDecorator(node, definitionName);
        ValidateTestSkipDecorators(node.Decorators, definitionName);
        ValidateTestFixtureDecorator(node, definitionName, isInsideType: _containingType != null);
        ValidateTestMarkDecorators(node.Decorators, definitionName, hasTestDecorator: HasAnyTestMarker(node.Decorators));
        ValidateTestCollectionDecorator(node.Decorators, definitionName, "function", allowOnThisKind: false);

        if (_containingType != null)
        {
            ValidateFinalRequiresOverride(node, _containingType.Name);

            if (_containingType.Kind == ContainingTypeKind.Class)
            {
                ValidateVirtualOnObjectOverride(node, _containingType.Name);
            }
            else if (_containingType.Kind == ContainingTypeKind.Struct)
            {
                ValidateVirtualOnStruct(node, _containingType.Name);
            }
        }

        base.VisitFunctionDef(node);
    }

    public override void VisitClassDef(ClassDef node)
    {
        ValidateDecorators(node.Decorators, node.Name);
        ValidateDataclassArguments(node.Decorators, node.Name);
        ValidateReadonlyNotOnNonProperty(node.Decorators, node.Name, "class");
        ValidateLruCacheNotOnNonFunction(node.Decorators, node.Name, "class");
        ValidateTestDecoratorNotOnType(node.Decorators, node.Name, "class");
        ValidateTestCollectionDecorator(node.Decorators, node.Name, "class", allowOnThisKind: true);

        var previousType = _containingType;
        _containingType = new ContainingTypeInfo(node.Name, ContainingTypeKind.Class);
        base.VisitClassDef(node);
        _containingType = previousType;
    }

    public override void VisitStructDef(StructDef node)
    {
        ValidateDecorators(node.Decorators, node.Name);
        ValidateDataclassOnNonClass(node.Decorators, node.Name, "struct");
        ValidateLruCacheNotOnNonFunction(node.Decorators, node.Name, "struct");
        ValidateTestDecoratorNotOnType(node.Decorators, node.Name, "struct");
        ValidateTestCollectionDecorator(node.Decorators, node.Name, "struct", allowOnThisKind: false);

        var previousType = _containingType;
        _containingType = new ContainingTypeInfo(node.Name, ContainingTypeKind.Struct);
        base.VisitStructDef(node);
        _containingType = previousType;
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        ValidateDecorators(node.Decorators, node.Name);
        ValidateDataclassOnNonClass(node.Decorators, node.Name, "interface");
        ValidateLruCacheNotOnNonFunction(node.Decorators, node.Name, "interface");
        ValidateTestDecoratorNotOnType(node.Decorators, node.Name, "interface");
        ValidateTestCollectionDecorator(node.Decorators, node.Name, "interface", allowOnThisKind: false);
        ValidateInterfaceDecorators(node);

        var previousType = _containingType;
        _containingType = new ContainingTypeInfo(node.Name, ContainingTypeKind.Interface);
        base.VisitInterfaceDef(node);
        _containingType = previousType;
    }

    public override void VisitEnumDef(EnumDef node)
    {
        ValidateDecorators(node.Decorators, node.Name);
        ValidateDataclassOnNonClass(node.Decorators, node.Name, "enum");
        ValidateLruCacheNotOnNonFunction(node.Decorators, node.Name, "enum");
        ValidateTestDecoratorNotOnType(node.Decorators, node.Name, "enum");
        ValidateTestCollectionDecorator(node.Decorators, node.Name, "enum", allowOnThisKind: false);
        base.VisitEnumDef(node);
    }

    public override void VisitPropertyDef(PropertyDef node)
    {
        var definitionName = _containingType != null
            ? $"{_containingType.Name}.{node.Name}"
            : node.Name;
        ValidateDecorators(node.Decorators, definitionName);
        ValidateAccessModifierDecorators(node.Decorators, node.Name, definitionName);
        ValidateReadonlyOnProperty(node, definitionName);
        ValidateTestDecoratorNotOnType(node.Decorators, definitionName, "property");
        base.VisitPropertyDef(node);
    }

    public override void VisitEventDef(EventDef node)
    {
        var definitionName = _containingType != null
            ? $"{_containingType.Name}.{node.Name}"
            : node.Name;
        ValidateDecorators(node.Decorators, definitionName);
        ValidateAccessModifierDecorators(node.Decorators, node.Name, definitionName);
        ValidateTestDecoratorNotOnType(node.Decorators, definitionName, "event");

        if (_containingType != null)
        {
            ValidateEventFinalRequiresOverride(node, _containingType.Name);
        }

        base.VisitEventDef(node);
    }

    public override void VisitVariableDeclaration(VariableDeclaration node)
    {
        if (node.Decorators.Length > 0)
        {
            if (_containingType != null)
            {
                ValidateFieldDecorators(node, _containingType.Name);
                var definitionName = $"{_containingType.Name}.{node.Name}";
                ValidateAccessModifierDecorators(node.Decorators, node.Name, definitionName);
            }
            else
            {
                // Decorated variables at module level are not allowed —
                // @static only makes sense on class/struct fields
                ValidateModuleLevelFieldDecorators(node);
            }
        }

        base.VisitVariableDeclaration(node);
    }

    /// <summary>
    /// Validates that member-level modifier decorators are not applied to interface definitions.
    /// Only custom attribute decorators (and access modifiers) are valid on interfaces.
    /// </summary>
    private static readonly HashSet<string> InvalidOnInterface = new()
    {
        DecoratorNames.Virtual,
        DecoratorNames.Override,
        DecoratorNames.Abstract,
        DecoratorNames.Static,
        DecoratorNames.Final,
    };

    private void ValidateInterfaceDecorators(InterfaceDef interfaceDef)
    {
        foreach (var decorator in interfaceDef.Decorators)
        {
            if (InvalidOnInterface.Contains(decorator.Name))
            {
                AddError(
                    $"Decorator '@{decorator.Name}' is not valid on interface '{interfaceDef.Name}'. " +
                    "Only custom attribute decorators and access modifiers are allowed on interfaces.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                    span: decorator.Span);
            }
        }
    }

    /// <summary>
    /// All known Sharpy decorator names (modifiers + attribute decorators + special built-ins).
    /// Any @decorator not in this set is rejected with SPY0444.
    /// </summary>
    private static readonly HashSet<string> AllKnownDecorators = new(
        DecoratorNames.KnownModifierDecorators
            .Union(DecoratorNames.KnownAttributeDecorators)
            .Union(DecoratorNames.KnownTestDecorators)
            .Append(DecoratorNames.Dataclass)
            .Append(DecoratorNames.LruCache)
            .Append(DecoratorNames.Cache)
            .Append(DecoratorNames.StaticMethod)
            .Append(DecoratorNames.ClassMethod)
            // @test.mark is supplementary metadata: not in KnownTestDecorators
            // (so ValidateTestDecoratorNotOnType doesn't reject it on classes for collection use),
            // but still must pass the unknown-decorator check.
            .Append(DecoratorNames.TestMark)
            // @test.collection is a class-level decorator (maps to xUnit [Collection]).
            // Not in KnownTestDecorators because it's only valid on classes.
            .Append(DecoratorNames.TestCollection));

    private void ValidateDecorators(IEnumerable<Decorator> decorators, string definitionName)
    {
        foreach (var decorator in decorators)
        {
            // Bracket attributes (@[...]) are usually C# attributes whose
            // arguments must be compile-time constants. Source generator
            // bracket attributes are different — their arguments are runtime
            // values passed to the generator at compile time and may be any
            // expression. Delegate validation of generator bracket attributes
            // to SourceGeneratorValidator (Order 65).
            if (decorator.IsBracketAttribute)
            {
                if (!IsSourceGeneratorBracketAttribute(decorator))
                {
                    ValidateDecoratorArgumentsAreConstants(decorator);
                }
                continue;
            }

            if (UnsupportedDecorators.TryGetValue(decorator.Name, out var errorMessage))
            {
                _logger.LogDebug($"Found unsupported decorator '@{decorator.Name}' on '{definitionName}'");
                AddError(errorMessage, decorator.LineStart, decorator.ColumnStart, code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                    span: decorator.Span);
                continue;
            }

            // Known modifier decorators must not have arguments
            if (DecoratorNames.KnownModifierDecorators.Contains(decorator.Name)
                && (decorator.Arguments.Length > 0 || decorator.KeywordArguments.Length > 0))
            {
                AddError(
                    $"Built-in decorator '@{decorator.Name}' does not accept arguments",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                    span: decorator.Span);
            }

            // @deprecated requires exactly one positional string argument
            if (decorator.Name == DecoratorNames.Deprecated)
            {
                ValidateDeprecatedArguments(decorator, definitionName);
                continue;
            }

            // Reject unknown decorators — C# attributes must use @[...] syntax
            if (!AllKnownDecorators.Contains(decorator.Name))
            {
                var suggestedName = SuggestBracketSyntax(decorator);
                AddError(
                    $"Unknown decorator '@{decorator.Name}'. To apply a .NET attribute, use {suggestedName} syntax.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Validation.UnknownDecorator,
                    span: decorator.Span);
                continue;
            }
        }
    }

    private static string SuggestBracketSyntax(Decorator decorator)
    {
        return $"@[{decorator.Name}]";
    }

    /// <summary>
    /// Returns true if the bracket attribute's name resolves to a type symbol
    /// whose <see cref="TypeSymbol.IsSourceGenerator"/> flag is set.
    /// IsSourceGenerator is populated by <c>NameResolver.ResolveInheritance</c>
    /// (Pass 1b), so the flag is reliably available by the time validators
    /// run. Source generator attributes accept arbitrary runtime arguments
    /// and are validated separately by <see cref="SourceGeneratorValidator"/>.
    /// </summary>
    private bool IsSourceGeneratorBracketAttribute(Decorator decorator)
    {
        var symbol = Context.SymbolTable.LookupType(decorator.Name);
        return symbol is { IsSourceGenerator: true };
    }

    /// <summary>
    /// Validates that all arguments to a custom decorator are compile-time constant expressions.
    /// Allowed: string/int/float/bool literals, None, enum member access (dotted names), type(X).
    /// </summary>
    private void ValidateDecoratorArgumentsAreConstants(Decorator decorator)
    {
        foreach (var arg in decorator.Arguments)
        {
            if (!IsCompileTimeConstant(arg))
            {
                var message = arg is Identifier id
                    ? $"Variable reference '{id.Name}' is not a compile-time constant; use a literal or enum member access"
                    : "Decorator argument must be a compile-time constant";
                AddError(
                    message,
                    arg.LineStart,
                    arg.ColumnStart,
                    code: DiagnosticCodes.Validation.NonConstantDecoratorArgument,
                    span: arg.Span);
            }
        }

        foreach (var kwArg in decorator.KeywordArguments)
        {
            if (!IsCompileTimeConstant(kwArg.Value))
            {
                var message = kwArg.Value is Identifier id
                    ? $"Variable reference '{id.Name}' is not a compile-time constant; use a literal or enum member access"
                    : "Decorator argument must be a compile-time constant";
                AddError(
                    message,
                    kwArg.Value.LineStart,
                    kwArg.Value.ColumnStart,
                    code: DiagnosticCodes.Validation.NonConstantDecoratorArgument,
                    span: kwArg.Value.Span);
            }
        }
    }

    /// <summary>
    /// Returns true if the expression is a compile-time constant suitable for a decorator argument.
    /// Allowed forms:
    /// - Literals: string, int, float, bool, None
    /// - Enum member access: dotted name like EnumType.member (MemberAccess with Identifier object)
    /// - type(X): FunctionCall with name "type" and exactly one argument
    /// </summary>
    private static bool IsCompileTimeConstant(Expression expr)
    {
        return expr switch
        {
            StringLiteral => true,
            IntegerLiteral => true,
            FloatLiteral => true,
            BooleanLiteral => true,
            NoneLiteral => true,
            // Enum member access or const field: SomeType.Member
            // Intentionally permissive — we can't resolve types at this validation phase (Order 60).
            // Invalid cases (non-const fields, instance members) are caught by the C# compiler.
            MemberAccess { Object: Identifier } => true,
            // type(X) is allowed as typeof equivalent
            FunctionCall { Function: Identifier { Name: "type" }, Arguments.Length: 1, KeywordArguments.Length: 0 } => true,
            // Negative numeric literals: -42, -3.14
            UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral or FloatLiteral } => true,
            _ => false,
        };
    }

    /// <summary>
    /// Validates that module-level variable declarations cannot have decorators.
    /// Decorators like @static only make sense on class/struct fields.
    /// </summary>
    private void ValidateModuleLevelFieldDecorators(VariableDeclaration varDecl)
    {
        foreach (var decorator in varDecl.Decorators)
        {
            AddError(
                $"Decorators cannot be applied to module-level variable declarations. " +
                $"'@{decorator.Name}' on '{varDecl.Name}' is only valid inside a class or struct body.",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                span: decorator.Span);
        }
    }

    /// <summary>
    /// Validates decorators on field declarations. Only @static, @final, and access modifiers are allowed.
    /// </summary>
    private void ValidateFieldDecorators(VariableDeclaration varDecl, string typeName)
    {
        foreach (var decorator in varDecl.Decorators)
        {
            // Bracket attributes are always allowed on fields (they're C# attributes)
            if (decorator.IsBracketAttribute)
                continue;

            if (decorator.Name != DecoratorNames.Static
                && decorator.Name != DecoratorNames.Final
                && !AccessModifierDecorators.Contains(decorator.Name))
            {
                AddError(
                    $"Decorator '@{decorator.Name}' is not valid on field '{varDecl.Name}' in '{typeName}'. " +
                    "Only @static, @final, and access modifier decorators are allowed on field declarations.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                    span: decorator.Span);
            }
        }

        ValidateDeprecatedOnVariable(varDecl, $"{typeName}.{varDecl.Name}");
    }

    private enum ContainingTypeKind
    {
        Class,
        Struct,
        Interface,
    }

    private sealed record ContainingTypeInfo(string Name, ContainingTypeKind Kind);
}
