using System.Globalization;
using System.Numerics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The one divisor rule for <c>/</c> (#2517), read by the type checker (which records whether a true
/// division routes through the zero-guarded <c>Builtins.TrueDiv</c>), by <see cref="ConstEligibility"/>'s
/// cross-module route (which has no recorded lowerings and must answer the same way from the AST and the
/// exported symbols alone), and by the const fold that records a float/decimal const's value
/// (<see cref="VariableSymbol.FloatConstantValue"/>). Pure: it reads literal text and the values the caller's
/// <see cref="ConstantLookup"/> resolves.
/// </summary>
internal static class TrueDivisionRules
{
    /// <summary>
    /// How a constant NAME resolves to its folded value: an integer const's
    /// <see cref="VariableSymbol.ConstantValue"/>, a float/decimal const's
    /// <see cref="VariableSymbol.FloatConstantValue"/>. Null for anything that is not a folded const.
    /// </summary>
    public sealed record ConstantLookup(Func<Identifier, BigInteger?> Integer, Func<Identifier, double?> Float);

    /// <summary>
    /// Whether a <c>/</c> divisor is a compile-time constant known to be non-zero: a numeric literal (through
    /// parentheses and unary <c>+</c>/<c>-</c>), integer arithmetic over literals and integer consts
    /// (<see cref="IntegerConstantEvaluator"/>), or a const name whose folded value is non-zero. Such a
    /// division cannot trap, so it keeps the native C# operator and stays a C# constant expression when its
    /// operands are. A divisor that is zero, unknown (a variable, a call, a member access), or a literal its
    /// own type cannot hold (<c>1e-46f</c> underflows float32 to zero) answers <c>false</c> and keeps the
    /// runtime guard.
    /// </summary>
    public static bool IsNonZeroConstantDivisor(Expression divisor, ConstantLookup? lookup)
        => TryEvaluate(divisor, lookup, out var value) && value != 0.0;

    /// <summary>
    /// Evaluates a constant numeric expression to a <see cref="double"/> (precise enough to decide zero
    /// against non-zero for every numeric type: a non-zero decimal is at least 1e-28). Float arithmetic is
    /// deliberately not folded — a float32 product can underflow to zero where the double product does not —
    /// so a float value comes only from a literal or a folded const.
    /// </summary>
    public static bool TryEvaluate(Expression expression, ConstantLookup? lookup, out double value)
    {
        if (IntegerConstantEvaluator.TryGetConstantInteger(expression, out var integer, lookup?.Integer))
        {
            value = (double)integer;
            return true;
        }

        switch (AstHelper.UnwrapParenthesized(expression))
        {
            case UnaryOp { Operator: UnaryOperator.Plus } plus:
                return TryEvaluate(plus.Operand, lookup, out value);

            case UnaryOp { Operator: UnaryOperator.Minus } minus:
                if (TryEvaluate(minus.Operand, lookup, out var operand))
                {
                    value = -operand;
                    return true;
                }
                break;

            case FloatLiteral literal:
                {
                    if (FloatLiteralClassifier.Classify(literal.Value, literal.Suffix).IsError)
                        break;
                    var text = literal.Value.Replace("_", "", StringComparison.Ordinal);
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        value = FloatLiteralClassifier.TypeOf(literal.Suffix) == SemanticType.Float32
                            ? (float)parsed
                            : parsed;
                        return true;
                    }
                    break;
                }

            case Identifier id when lookup?.Float(id) is { } folded:
                value = folded;
                return true;
        }

        value = 0.0;
        return false;
    }

    /// <summary>
    /// The folded value of a float/decimal const's initializer, or null when it does not fold
    /// (<see cref="TryEvaluate"/>'s rules).
    /// </summary>
    public static double? FoldFloatConstant(Expression? initializer, ConstantLookup? lookup)
        => initializer != null && TryEvaluate(initializer, lookup, out var value) ? value : null;
}
