using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Modifier and access-modifier decorator validation: @virtual, @override, @final,
/// @readonly, @static, and the access modifiers (@public/@protected/@private/@internal).
/// </summary>
internal partial class DecoratorValidator
{
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
}
