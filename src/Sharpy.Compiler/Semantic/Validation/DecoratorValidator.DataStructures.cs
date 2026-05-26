using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validation for data-structure-oriented decorators: @dataclass and @deprecated.
/// </summary>
internal partial class DecoratorValidator
{
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
}
