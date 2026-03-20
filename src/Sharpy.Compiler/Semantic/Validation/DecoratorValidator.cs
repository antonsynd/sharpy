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

        var previousType = _containingType;
        _containingType = new ContainingTypeInfo(node.Name, ContainingTypeKind.Class);
        base.VisitClassDef(node);
        _containingType = previousType;
    }

    public override void VisitStructDef(StructDef node)
    {
        ValidateDecorators(node.Decorators, node.Name);

        var previousType = _containingType;
        _containingType = new ContainingTypeInfo(node.Name, ContainingTypeKind.Struct);
        base.VisitStructDef(node);
        _containingType = previousType;
    }

    public override void VisitInterfaceDef(InterfaceDef node)
    {
        ValidateDecorators(node.Decorators, node.Name);
        ValidateInterfaceDecorators(node);

        var previousType = _containingType;
        _containingType = new ContainingTypeInfo(node.Name, ContainingTypeKind.Interface);
        base.VisitInterfaceDef(node);
        _containingType = previousType;
    }

    public override void VisitPropertyDef(PropertyDef node)
    {
        ValidateDecorators(node.Decorators, node.Name);
        base.VisitPropertyDef(node);
    }

    public override void VisitEventDef(EventDef node)
    {
        var definitionName = _containingType != null
            ? $"{_containingType.Name}.{node.Name}"
            : node.Name;
        ValidateDecorators(node.Decorators, definitionName);

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

    private void ValidateDecorators(IEnumerable<Decorator> decorators, string definitionName)
    {
        foreach (var decorator in decorators)
        {
            if (UnsupportedDecorators.TryGetValue(decorator.Name, out var errorMessage))
            {
                _logger.LogDebug($"Found unsupported decorator '@{decorator.Name}' on '{definitionName}'");
                AddError(errorMessage, decorator.LineStart, decorator.ColumnStart, code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                    span: decorator.Span);
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

            // For unknown decorators (custom attributes), validate arguments are compile-time constants
            if (!DecoratorNames.KnownModifierDecorators.Contains(decorator.Name)
                && !UnsupportedDecorators.ContainsKey(decorator.Name))
            {
                ValidateDecoratorArgumentsAreConstants(decorator);
            }
        }
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
    /// Validates decorators on field declarations. Only @static is allowed.
    /// </summary>
    private void ValidateFieldDecorators(VariableDeclaration varDecl, string typeName)
    {
        foreach (var decorator in varDecl.Decorators)
        {
            if (decorator.Name != DecoratorNames.Static)
            {
                AddError(
                    $"Decorator '@{decorator.Name}' is not valid on field '{varDecl.Name}' in '{typeName}'. " +
                    "Only @static is allowed on field declarations.",
                    decorator.LineStart,
                    decorator.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidDecoratorUsage,
                    span: decorator.Span);
            }
        }
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

    private enum ContainingTypeKind
    {
        Class,
        Struct,
        Interface,
    }

    private sealed record ContainingTypeInfo(string Name, ContainingTypeKind Kind);
}
