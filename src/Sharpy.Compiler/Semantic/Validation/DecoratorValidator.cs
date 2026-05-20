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
internal class DecoratorValidator : ValidatingAstWalker
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
    private void ValidateInterfaceDecorators(InterfaceDef interfaceDef)
    {
        // Decorators that are invalid on interfaces — these are member-level modifiers
        HashSet<string> invalidOnInterface = new()
        {
            DecoratorNames.Virtual,
            DecoratorNames.Override,
            DecoratorNames.Abstract,
            DecoratorNames.Static,
            DecoratorNames.Final,
        };

        foreach (var decorator in interfaceDef.Decorators)
        {
            if (invalidOnInterface.Contains(decorator.Name))
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

    private static readonly HashSet<string> AccessModifierDecorators = new()
    {
        DecoratorNames.Public,
        DecoratorNames.Protected,
        DecoratorNames.Private,
        DecoratorNames.Internal,
    };

    /// <summary>
    /// Validates access modifier decorators: no conflicts, no access modifiers on dunders.
    /// </summary>
    private void ValidateAccessModifierDecorators(IEnumerable<Decorator> decorators, string memberName, string definitionName)
    {
        var accessDecorators = decorators.Where(d => AccessModifierDecorators.Contains(d.Name)).ToList();

        // Check for conflicting access modifiers
        if (accessDecorators.Count > 1)
        {
            var names = string.Join(", ", accessDecorators.Select(d => $"@{d.Name}"));
            var second = accessDecorators[1];
            AddError(
                $"Conflicting access modifier decorators on '{definitionName}': {names}. Only one access modifier is allowed.",
                second.LineStart,
                second.ColumnStart,
                code: DiagnosticCodes.Validation.ConflictingAccessModifiers,
                span: second.Span);
        }

        // Check for access modifiers on dunder methods
        if (accessDecorators.Count > 0 && DunderDetector.IsDunderMethod(memberName))
        {
            var decorator = accessDecorators[0];
            AddError(
                $"Access modifier '@{decorator.Name}' cannot be applied to dunder method '{memberName}'. " +
                "Dunder methods are protocol methods and their access level is determined by convention.",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Validation.AccessModifierOnDunder,
                span: decorator.Span);
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
            .Append(DecoratorNames.ClassMethod));

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

    /// <summary>
    /// Validates that @virtual is not used on struct methods (structs are sealed in C#).
    /// </summary>
    private void ValidateVirtualOnStruct(FunctionDef method, string typeName)
    {
        var virtualDecorator = method.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Virtual);
        if (virtualDecorator != null)
        {
            AddError(
                $"Struct method '{method.Name}' in '{typeName}' cannot be @virtual. " +
                "Struct methods cannot be virtual because structs are implicitly sealed.",
                virtualDecorator.LineStart,
                virtualDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.VirtualOnStructMethod,
                span: virtualDecorator.Span);
        }
    }

    /// <summary>
    /// Warns when @virtual is used on dunder methods that always generate 'override'
    /// (e.g., __str__ -> ToString(), __hash__ -> GetHashCode()).
    /// </summary>
    private void ValidateVirtualOnObjectOverride(FunctionDef method, string typeName)
    {
        if (!method.Decorators.Any(d => d.Name == DecoratorNames.Virtual))
            return;

        // These dunders always generate 'override' on Object methods
        string? csharpName = method.Name switch
        {
            DunderNames.Str => "Object.ToString()",
            DunderNames.Hash => "Object.GetHashCode()",
            _ => null
        };

        // __eq__ with object parameter also overrides Object.Equals
        if (csharpName == null && method.Name == DunderNames.Eq
            && method.Parameters.Any(p =>
                p.Name != PythonNames.Self
                && p.Type?.Name == BuiltinNames.Object))
        {
            csharpName = "Object.Equals()";
        }

        if (csharpName != null)
        {
            var virtualDecorator = method.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Virtual);
            if (virtualDecorator == null)
                return;
            AddWarning(
                $"@virtual is redundant on '{method.Name}' in '{typeName}' — " +
                $"it always overrides {csharpName}. The @virtual decorator will be ignored.",
                virtualDecorator.LineStart,
                virtualDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.VirtualOnObjectOverride,
                span: virtualDecorator.Span);
        }
    }

    /// <summary>
    /// Validates that @final on an event is always accompanied by @override.
    /// </summary>
    private void ValidateEventFinalRequiresOverride(EventDef eventDef, string typeName)
    {
        bool hasFinal = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Final);
        bool hasOverride = eventDef.Decorators.Any(d => d.Name == DecoratorNames.Override);

        if (hasFinal && !hasOverride)
        {
            var finalDecorator = eventDef.Decorators.First(d => d.Name == DecoratorNames.Final);
            AddError(
                $"Event '{eventDef.Name}' in '{typeName}' is marked @final but not @override. " +
                "The @final decorator prevents further overriding and requires @override.",
                finalDecorator.LineStart,
                finalDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithoutOverride,
                span: finalDecorator.Span);
        }
    }

    /// <summary>
    /// Validates that @final on a method is always accompanied by @override.
    /// @final prevents further overriding, so it only makes sense on an override method.
    /// </summary>
    private void ValidateFinalRequiresOverride(FunctionDef method, string typeName)
    {
        bool hasFinal = method.Decorators.Any(d => d.Name == DecoratorNames.Final);
        bool hasOverride = method.Decorators.Any(d => d.Name == DecoratorNames.Override);

        if (hasFinal && !hasOverride)
        {
            var finalDecorator = method.Decorators.First(d => d.Name == DecoratorNames.Final);
            AddError(
                $"Method '{method.Name}' in '{typeName}' is marked @final but not @override. " +
                "The @final decorator prevents further overriding and requires @override.",
                finalDecorator.LineStart,
                finalDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.FinalWithoutOverride,
                span: finalDecorator.Span);
        }
    }

    /// <summary>
    /// Valid keyword argument names for @dataclass decorator.
    /// </summary>
    private static readonly IReadOnlySet<string> DataclassKnownOptions = DataclassOptionNames.KnownOptions;

    /// <summary>
    /// Validates that @dataclass is not applied to a non-class type definition.
    /// </summary>
    private void ValidateDataclassOnNonClass(IEnumerable<Decorator> decorators, string typeName, string typeKind)
    {
        var dataclassDecorator = decorators.FirstOrDefault(d => d.Name == DecoratorNames.Dataclass);
        if (dataclassDecorator != null)
        {
            AddError(
                $"The '@dataclass' decorator can only be applied to classes, not to {typeKind} '{typeName}'.",
                dataclassDecorator.LineStart,
                dataclassDecorator.ColumnStart,
                code: DiagnosticCodes.Semantic.DataclassOnNonClass,
                span: dataclassDecorator.Span);
        }
    }

    /// <summary>
    /// Validates @dataclass decorator arguments: no positional args, only known keyword args with bool values.
    /// </summary>
    private void ValidateDataclassArguments(IEnumerable<Decorator> decorators, string typeName)
    {
        var dataclassDecorator = decorators.FirstOrDefault(d => d.Name == DecoratorNames.Dataclass);
        if (dataclassDecorator == null)
            return;

        // No positional arguments allowed
        if (dataclassDecorator.Arguments.Length > 0)
        {
            AddError(
                $"'@dataclass' on '{typeName}' does not accept positional arguments. " +
                "Use keyword arguments: @dataclass(frozen=True, eq=True, repr=True).",
                dataclassDecorator.Arguments[0].LineStart,
                dataclassDecorator.Arguments[0].ColumnStart,
                code: DiagnosticCodes.Semantic.DataclassInvalidOption,
                span: dataclassDecorator.Arguments[0].Span);
        }

        // Validate keyword arguments
        foreach (var kwArg in dataclassDecorator.KeywordArguments)
        {
            if (!DataclassKnownOptions.Contains(kwArg.Name))
            {
                AddError(
                    $"Unknown @dataclass option '{kwArg.Name}' on '{typeName}'. " +
                    "Valid options are: frozen, eq, repr.",
                    kwArg.Value.LineStart,
                    kwArg.Value.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassInvalidOption,
                    span: kwArg.Value.Span);
            }
            else if (kwArg.Value is not BooleanLiteral)
            {
                AddError(
                    $"@dataclass option '{kwArg.Name}' must be a boolean literal (True or False).",
                    kwArg.Value.LineStart,
                    kwArg.Value.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassInvalidOption,
                    span: kwArg.Value.Span);
            }
        }
    }

    private void ValidateReadonlyOnProperty(PropertyDef propDef, string definitionName)
    {
        var readonlyDecorator = propDef.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Readonly);
        if (readonlyDecorator == null)
            return;

        if (propDef.Accessor == PropertyAccessor.Set)
        {
            AddError(
                $"'@readonly' cannot be combined with 'property set' on '{definitionName}'. " +
                "A write-only property cannot be readonly.",
                readonlyDecorator.LineStart,
                readonlyDecorator.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                span: readonlyDecorator.Span);
        }
    }

    private void ValidateReadonlyNotOnNonProperty(IEnumerable<Decorator> decorators, string definitionName, string kind)
    {
        var readonlyDecorator = decorators.FirstOrDefault(d => d.Name == DecoratorNames.Readonly);
        if (readonlyDecorator != null)
        {
            AddError(
                $"'@readonly' cannot be applied to {kind} '{definitionName}'. " +
                "Use @readonly on property declarations only.",
                readonlyDecorator.LineStart,
                readonlyDecorator.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                span: readonlyDecorator.Span);
        }
    }

    /// <summary>
    /// Validates that @deprecated has exactly one positional string argument (the message).
    /// </summary>
    private void ValidateDeprecatedArguments(Decorator decorator, string definitionName)
    {
        if (decorator.Arguments.Length != 1 || decorator.Arguments[0] is not StringLiteral)
        {
            AddError(
                $"'@deprecated' on '{definitionName}' requires exactly one string argument: @deprecated(\"reason\")",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                span: decorator.Span);
        }

        if (decorator.KeywordArguments.Length > 0)
        {
            AddError(
                $"'@deprecated' on '{definitionName}' does not accept keyword arguments",
                decorator.KeywordArguments[0].Value.LineStart,
                decorator.KeywordArguments[0].Value.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                span: decorator.KeywordArguments[0].Value.Span);
        }
    }

    /// <summary>
    /// Validates that @deprecated is not applied to variable declarations.
    /// It's valid on functions, methods, classes, and properties.
    /// </summary>
    private void ValidateDeprecatedOnVariable(VariableDeclaration varDecl, string definitionName)
    {
        var deprecatedDecorator = varDecl.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Deprecated);
        if (deprecatedDecorator != null)
        {
            AddError(
                $"'@deprecated' cannot be applied to variable '{definitionName}'. " +
                "Use @deprecated on functions, methods, classes, or properties.",
                deprecatedDecorator.LineStart,
                deprecatedDecorator.ColumnStart,
                code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                span: deprecatedDecorator.Span);
        }
    }

    /// <summary>
    /// Validates @lru_cache and @cache decorator arguments.
    /// @cache must have no arguments. @lru_cache(maxsize=N) accepts a single optional
    /// 'maxsize' keyword (or single positional) that must be a non-negative integer
    /// literal or None.
    /// </summary>
    private void ValidateLruCacheArguments(IEnumerable<Decorator> decorators, string definitionName)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name == DecoratorNames.Cache)
            {
                if (decorator.Arguments.Length > 0 || decorator.KeywordArguments.Length > 0)
                {
                    AddError(
                        $"'@cache' on '{definitionName}' does not accept arguments. " +
                        "Use '@lru_cache(maxsize=N)' to set a bound.",
                        decorator.LineStart,
                        decorator.ColumnStart,
                        code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                        span: decorator.Span);
                }
                continue;
            }

            if (decorator.Name != DecoratorNames.LruCache)
                continue;

            // No arguments → equivalent to @cache (unbounded). Allowed.
            if (decorator.Arguments.Length == 0 && decorator.KeywordArguments.Length == 0)
                continue;

            // Disallow multiple arguments
            int totalArgs = decorator.Arguments.Length + decorator.KeywordArguments.Length;
            if (totalArgs > 1)
            {
                AddError(
                    $"'@lru_cache' on '{definitionName}' accepts at most one 'maxsize' argument.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                    span: decorator.Span);
                continue;
            }

            // Keyword arguments must be named 'maxsize'
            if (decorator.KeywordArguments.Length == 1)
            {
                var kw = decorator.KeywordArguments[0];
                if (kw.Name != "maxsize")
                {
                    AddError(
                        $"Unknown @lru_cache option '{kw.Name}' on '{definitionName}'. " +
                        "The only supported option is 'maxsize'.",
                        kw.Value.LineStart,
                        kw.Value.ColumnStart,
                        code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                        span: kw.Value.Span);
                    continue;
                }

                ValidateLruCacheMaxSizeValue(kw.Value, definitionName);
            }
            else
            {
                // Positional argument: must be the maxsize value
                ValidateLruCacheMaxSizeValue(decorator.Arguments[0], definitionName);
            }
        }
    }

    /// <summary>
    /// Validates that the maxsize argument is either a non-negative integer literal or None.
    /// </summary>
    private void ValidateLruCacheMaxSizeValue(Expression value, string definitionName)
    {
        switch (value)
        {
            case NoneLiteral:
                return;
            case IntegerLiteral:
                // Integer literals from the parser are always non-negative; the unary
                // minus case is handled below as a separate AST node.
                return;
            case UnaryOp { Operator: UnaryOperator.Minus, Operand: IntegerLiteral }:
                AddError(
                    $"'@lru_cache' on '{definitionName}' requires a non-negative 'maxsize' value.",
                    value.LineStart,
                    value.ColumnStart,
                    code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                    span: value.Span);
                return;
            default:
                AddError(
                    $"'@lru_cache' on '{definitionName}' requires 'maxsize' to be an integer literal or None.",
                    value.LineStart,
                    value.ColumnStart,
                    code: DiagnosticCodes.Validation.LruCacheInvalidMaxSize,
                    span: value.Span);
                return;
        }
    }

    /// <summary>
    /// Reports an error when @lru_cache or @cache is applied to a non-function definition.
    /// </summary>
    private void ValidateLruCacheNotOnNonFunction(
        IEnumerable<Decorator> decorators, string definitionName, string kind)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name == DecoratorNames.LruCache || decorator.Name == DecoratorNames.Cache)
            {
                AddError(
                    $"'@{decorator.Name}' cannot be applied to {kind} '{definitionName}'. " +
                    "Memoization decorators only apply to functions and methods.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Validation.LruCacheOnNonFunction,
                    span: decorator.Span);
            }
        }
    }

    /// <summary>
    /// Validates @test decorator on function/method declarations.
    /// Rules:
    /// - Cannot combine with @abstract, @virtual, or @static
    /// - Cannot be applied to dunder methods
    /// - Accepts zero or one positional string argument (description)
    /// </summary>
    private void ValidateTestDecorator(IReadOnlyList<Decorator> decorators, string functionName, bool isDunder)
    {
        var testDecorator = decorators.FirstOrDefault(d => d.Name == DecoratorNames.Test);
        if (testDecorator == null)
            return;

        // @test on dunder methods
        if (isDunder)
        {
            AddError(
                $"'@test' cannot be applied to dunder method '{functionName}'. " +
                "Test decorators are only valid on regular methods.",
                testDecorator.LineStart,
                testDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidTarget,
                span: testDecorator.Span);
            return;
        }

        // @test + @abstract/@virtual/@static
        foreach (var decorator in decorators)
        {
            if (decorator.Name == DecoratorNames.Abstract
                || decorator.Name == DecoratorNames.Virtual
                || decorator.Name == DecoratorNames.Static)
            {
                AddError(
                    $"'@test' cannot be combined with '@{decorator.Name}' on '{functionName}'. " +
                    "Test methods must be concrete instance methods.",
                    testDecorator.LineStart,
                    testDecorator.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidCombination,
                    span: testDecorator.Span);
                return;
            }
        }

        // Argument validation: 0 or 1 positional string arg
        if (testDecorator.Arguments.Length > 1)
        {
            AddWarning(
                $"'@test' on '{functionName}' accepts at most one string argument (description).",
                testDecorator.LineStart,
                testDecorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: testDecorator.Span);
        }
        else if (testDecorator.Arguments.Length == 1 && testDecorator.Arguments[0] is not StringLiteral)
        {
            AddWarning(
                $"'@test' argument on '{functionName}' must be a string literal (description).",
                testDecorator.Arguments[0].LineStart,
                testDecorator.Arguments[0].ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: testDecorator.Arguments[0].Span);
        }

        if (testDecorator.KeywordArguments.Length > 0)
        {
            AddWarning(
                $"'@test' on '{functionName}' does not accept keyword arguments.",
                testDecorator.KeywordArguments[0].Value.LineStart,
                testDecorator.KeywordArguments[0].Value.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: testDecorator.KeywordArguments[0].Value.Span);
        }
    }

    /// <summary>
    /// Validates that @test (and its sub-decorators like @test.parametrize) is not applied to type
    /// definitions (classes, structs, interfaces, enums) or to properties/events.
    /// </summary>
    private void ValidateTestDecoratorNotOnType(IEnumerable<Decorator> decorators, string typeName, string typeKind)
    {
        foreach (var decorator in decorators)
        {
            if (!DecoratorNames.KnownTestDecorators.Contains(decorator.Name))
                continue;

            AddError(
                $"'@{decorator.Name}' cannot be applied to {typeKind} '{typeName}'. " +
                "Test decorators are only valid on function and method declarations.",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidTarget,
                span: decorator.Span);
        }
    }

    /// <summary>
    /// Validates @test.parametrize decorator on a function definition.
    /// Rules:
    /// - Must take exactly one positional argument (a list literal of tuples).
    /// - Each tuple element must have the same arity as the function's parameters
    ///   (excluding 'self' for methods).
    /// - Cannot be combined with plain @test (it implies @test by itself).
    /// </summary>
    private void ValidateTestParametrizeDecorator(FunctionDef function, string definitionName)
    {
        var parametrize = function.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.TestParametrize);
        if (parametrize == null)
            return;

        // Cannot combine with plain @test
        var plainTest = function.Decorators.FirstOrDefault(d => d.Name == DecoratorNames.Test);
        if (plainTest != null)
        {
            AddError(
                $"'@test.parametrize' cannot be combined with '@test' on '{definitionName}'. " +
                "'@test.parametrize' already marks the function as a test.",
                parametrize.LineStart,
                parametrize.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidCombination,
                span: parametrize.Span);
        }

        // Keyword arguments are not supported
        if (parametrize.KeywordArguments.Length > 0)
        {
            AddWarning(
                $"'@test.parametrize' on '{definitionName}' does not accept keyword arguments.",
                parametrize.KeywordArguments[0].Value.LineStart,
                parametrize.KeywordArguments[0].Value.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: parametrize.KeywordArguments[0].Value.Span);
        }

        // Must take exactly one positional argument
        if (parametrize.Arguments.Length != 1)
        {
            AddWarning(
                $"'@test.parametrize' on '{definitionName}' requires exactly one argument: " +
                "a list of tuples with parameter values.",
                parametrize.LineStart,
                parametrize.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: parametrize.Span);
            return;
        }

        var argument = parametrize.Arguments[0];
        if (argument is not ListLiteral listLiteral)
        {
            AddWarning(
                $"'@test.parametrize' argument on '{definitionName}' must be a list literal " +
                "of tuples (e.g. [(1, 2), (3, 4)]).",
                argument.LineStart,
                argument.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: argument.Span);
            return;
        }

        // Compute the expected arity: number of function parameters, excluding 'self' on methods.
        int expectedArity = function.Parameters.Length;
        if (_containingType != null && function.Parameters.Length > 0
            && function.Parameters[0].Name == PythonNames.Self)
        {
            expectedArity = function.Parameters.Length - 1;
        }

        for (int i = 0; i < listLiteral.Elements.Length; i++)
        {
            var element = listLiteral.Elements[i];
            if (element is not TupleLiteral tuple)
            {
                // A single-parameter function may use a flat list of values (e.g. [1, 2, 3]).
                // Otherwise require a tuple.
                if (expectedArity != 1)
                {
                    AddWarning(
                        $"'@test.parametrize' element {i} on '{definitionName}' must be a tuple " +
                        $"with {expectedArity} values (one per parameter).",
                        element.LineStart,
                        element.ColumnStart,
                        code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                        span: element.Span);
                }
                continue;
            }

            if (tuple.Elements.Length != expectedArity)
            {
                AddWarning(
                    $"'@test.parametrize' element {i} on '{definitionName}' has " +
                    $"{tuple.Elements.Length} value(s); expected {expectedArity} " +
                    "(one per function parameter).",
                    element.LineStart,
                    element.ColumnStart,
                    code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                    span: element.Span);
            }
        }
    }

    /// <summary>
    /// Validates @test.skip and @test.skip_if decorators on function definitions.
    /// Rules:
    /// - @test.skip(reason) takes exactly one string argument.
    /// - @test.skip_if(condition, reason) takes exactly two arguments; the second must be a string.
    /// - These decorators can be combined with @test or @test.parametrize.
    /// - Cannot be applied to type definitions (handled by ValidateTestDecoratorNotOnType).
    /// </summary>
    private void ValidateTestSkipDecorators(IReadOnlyList<Decorator> decorators, string definitionName)
    {
        foreach (var decorator in decorators)
        {
            if (decorator.Name == DecoratorNames.TestSkip)
            {
                ValidateTestSkipDecorator(decorator, definitionName);
            }
            else if (decorator.Name == DecoratorNames.TestSkipIf)
            {
                ValidateTestSkipIfDecorator(decorator, definitionName);
            }
        }
    }

    private void ValidateTestSkipDecorator(Decorator decorator, string definitionName)
    {
        if (decorator.KeywordArguments.Length > 0)
        {
            AddWarning(
                $"'@test.skip' on '{definitionName}' does not accept keyword arguments.",
                decorator.KeywordArguments[0].Value.LineStart,
                decorator.KeywordArguments[0].Value.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.KeywordArguments[0].Value.Span);
        }

        if (decorator.Arguments.Length != 1)
        {
            AddWarning(
                $"'@test.skip' on '{definitionName}' requires exactly one string argument: " +
                "@test.skip(\"reason\").",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.Span);
            return;
        }

        if (decorator.Arguments[0] is not StringLiteral)
        {
            AddWarning(
                $"'@test.skip' argument on '{definitionName}' must be a string literal (the skip reason).",
                decorator.Arguments[0].LineStart,
                decorator.Arguments[0].ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.Arguments[0].Span);
        }
    }

    private void ValidateTestSkipIfDecorator(Decorator decorator, string definitionName)
    {
        if (decorator.KeywordArguments.Length > 0)
        {
            AddWarning(
                $"'@test.skip_if' on '{definitionName}' does not accept keyword arguments.",
                decorator.KeywordArguments[0].Value.LineStart,
                decorator.KeywordArguments[0].Value.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.KeywordArguments[0].Value.Span);
        }

        if (decorator.Arguments.Length != 2)
        {
            AddWarning(
                $"'@test.skip_if' on '{definitionName}' requires exactly two arguments: " +
                "@test.skip_if(condition, \"reason\").",
                decorator.LineStart,
                decorator.ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.Span);
            return;
        }

        if (decorator.Arguments[1] is not StringLiteral)
        {
            AddWarning(
                $"'@test.skip_if' second argument on '{definitionName}' must be a string literal (the skip reason).",
                decorator.Arguments[1].LineStart,
                decorator.Arguments[1].ColumnStart,
                code: DiagnosticCodes.Validation.TestDecoratorInvalidArgument,
                span: decorator.Arguments[1].Span);
        }
    }

    private enum ContainingTypeKind
    {
        Class,
        Struct,
        Interface,
    }

    private sealed record ContainingTypeInfo(string Name, ContainingTypeKind Kind);
}
