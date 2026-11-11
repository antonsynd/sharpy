using System.Globalization;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The one divisor rule for <c>/</c> (#2517), read by the type checker (which records whether a true
/// division routes through the zero-guarded <c>Builtins.TrueDiv</c>) and by
/// <see cref="ConstEligibility"/>'s cross-module route (which has no recorded lowerings and must answer
/// the same way from the AST alone). Pure: it reads only literal text.
/// </summary>
internal static class TrueDivisionRules
{
    /// <summary>
    /// Whether a <c>/</c> divisor is provably non-zero from its spelling alone: a numeric literal, through
    /// parentheses and unary <c>+</c>/<c>-</c>, whose value is not zero, or <c>+</c>/<c>-</c>/<c>*</c>/shift
    /// arithmetic over integer literals (<see cref="IntegerConstantEvaluator"/>). Such a division cannot
    /// trap, so it keeps the native C# operator and stays a C# constant expression when its operands are.
    /// Anything else — a name (even a constant's), a call, a literal that is zero or underflows to zero
    /// (<c>1e-400</c>) — answers <c>false</c> and keeps the runtime guard.
    /// </summary>
    public static bool IsNonZeroLiteralDivisor(Expression divisor)
    {
        if (IntegerConstantEvaluator.TryGetConstantInteger(divisor, out var integer))
            return !integer.IsZero;

        return AstHelper.UnwrapParenthesized(divisor) switch
        {
            UnaryOp { Operator: UnaryOperator.Minus or UnaryOperator.Plus } unary
                => IsNonZeroLiteralDivisor(unary.Operand),
            FloatLiteral floatLiteral
                => double.TryParse(floatLiteral.Value.Replace("_", "", StringComparison.Ordinal),
                       NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                   && value != 0.0 && !double.IsInfinity(value),
            _ => false,
        };
    }
}
