extern alias SharpyRT;

using FormatOperandKind = SharpyRT::Sharpy.FormatOperandKind;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The compile-time projection of a format operand: the operand kind Core's validator distinguishes
/// (<c>Sharpy.FormatOperandKind</c>) and the python type name its messages spell. The kinds are
/// Core's own enum, so the static twin cannot grow a kind the runtime does not have.
/// <see cref="Unknown"/> means the kind is not statically known (an <c>object</c> hole, a union, an
/// inferred variable): the spec is not refused early and Core validates it at runtime instead.
/// </summary>
internal readonly record struct FormatOperand(FormatOperandKind Kind, string PyTypeName)
{
    public static FormatOperand Unknown => new(FormatOperandKind.Unknown, "object");
    public static FormatOperand NoneValue => new(FormatOperandKind.NoneValue, "NoneType");
    public static FormatOperand Str => new(FormatOperandKind.Str, "str");
    public static FormatOperand Bool => new(FormatOperandKind.Bool, "bool");
    public static FormatOperand Integral => new(FormatOperandKind.Integral, "int");
    public static FormatOperand Float => new(FormatOperandKind.Float, "float");
}

/// <summary>
/// The static twin of Core's format-spec refusals (#1814, #1815, #1956): a STATIC spec (one with no
/// nested replacement fields) is validated at compile time by the ONE Core parser and validator,
/// <c>Sharpy.PyFormatSpec.Validate</c> — the same code <c>Sharpy.PyFormat.Apply</c> runs at runtime
/// (#1984, R-BX). No grammar or rule lives here: the compiler's only contribution is the operand
/// projection (<see cref="FormatOperand"/>), so a spec this refuses is one Core raises the same
/// <c>ValueError</c>/<c>TypeError</c> for, with the same message, in the same rule order.
/// </summary>
internal static class FormatSpecGrammar
{
    /// <summary>
    /// Returns <c>null</c> when <paramref name="spec"/> is a valid format spec for
    /// <paramref name="operand"/>, or CPython's message (the text of the <c>ValueError</c> /
    /// <c>TypeError</c> the runtime raises) when it is not. An empty spec is always valid, and an
    /// <see cref="FormatOperand.Unknown"/> operand is never refused here.
    /// </summary>
    public static string? Validate(string spec, FormatOperand operand)
        => SharpyRT::Sharpy.PyFormatSpec.Validate(spec, operand.Kind, operand.PyTypeName, out _)?.Message;
}
