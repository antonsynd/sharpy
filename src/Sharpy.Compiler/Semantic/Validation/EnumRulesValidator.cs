using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates enum-specific rules:
/// - All enum values must be explicit
/// - All values must be int or str
/// - All values must be the same type
/// </summary>
internal class EnumRulesValidator : ValidatingAstWalker
{
    public override string Name => "EnumRulesValidator";
    public override int Order => 147;

    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _logger = context.Logger;
        base.Validate(module, context);
    }

    public override void VisitEnumDef(EnumDef node)
    {
        ValidateEnumRules(node);
        base.VisitEnumDef(node);
    }

    private void ValidateEnumRules(EnumDef enumDef)
    {
        _logger.LogDebug($"Validating enum-specific rules for '{enumDef.Name}'");

        SemanticType? enumValueType = null;

        foreach (var member in enumDef.Members)
        {
            if (member.Value == null)
            {
                AddError(
                    $"Enum member '{member.Name}' requires an explicit value. All enum members must have explicit constant values.",
                    member.LineStart,
                    member.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidEnumValue,
                    span: member.Span);
                continue;
            }

            var valueType = Context.SemanticInfo.GetExpressionType(member.Value);
            if (valueType == null || valueType == SemanticType.Unknown)
                continue;

            if (!PrimitiveCatalog.IsSharpyInteger(valueType) && !IsStrType(valueType))
            {
                AddError(
                    $"Enum member '{member.Name}' has invalid value type '{valueType.GetDisplayName()}'. Enum values must be int or str.",
                    member.LineStart,
                    member.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidEnumValue,
                    span: member.Span);
                continue;
            }

            if (enumValueType == null)
            {
                enumValueType = valueType;
            }
            else if (!valueType.Equals(enumValueType))
            {
                AddError(
                    $"Enum member '{member.Name}' has type '{valueType.GetDisplayName()}' but previous members have type '{enumValueType.GetDisplayName()}'. All enum values must be the same type.",
                    member.LineStart,
                    member.ColumnStart,
                    code: DiagnosticCodes.Semantic.InvalidEnumValue,
                    span: member.Span);
            }
        }
    }

    private static bool IsStrType(SemanticType type) => type == SemanticType.Str;
}
