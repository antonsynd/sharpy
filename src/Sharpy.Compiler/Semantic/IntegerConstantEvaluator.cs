using System.Numerics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Pure evaluation of constant integer literal expressions, shared by the type checker (which folds
/// constant integer exponentiation and reports overflow) and the lowering pass (which re-derives the
/// folded value into an <c>IrConstant</c>, E2 #1056). Contains no inference and no reflection — it
/// reads only the AST literal text — so it is safe to call from the lowering pass.
/// </summary>
internal static class IntegerConstantEvaluator
{
    /// <summary>
    /// Extracts a constant integer value (as <see cref="BigInteger"/>) from an expression, handling
    /// underscores, <c>0x</c>/<c>0o</c>/<c>0b</c> prefixes, unary <c>+</c>/<c>-</c>, parentheses, and
    /// <c>+</c>/<c>-</c>/<c>*</c> over constant-integer subtrees. Returns <c>false</c> for anything
    /// else — a name, a call, a division (a zero divisor is a runtime trap, not a constant), or a
    /// float.
    /// <para>
    /// Arithmetic is exact <see cref="BigInteger"/>: this evaluator decides only <em>what the value
    /// is</em>, never whether it fits a type. Range decisions belong to the callers, which is what
    /// lets the type checker refuse an out-of-range constant (SPY0348) while the lowering pass emits
    /// an in-range one, both from the same numbers (#1234).
    /// </para>
    /// </summary>
    public static bool TryGetConstantInteger(Expression expr, out BigInteger value)
        => TryGetConstantInteger(expr, out value, resolver: null);

    /// <summary>
    /// Overload that accepts an optional <paramref name="resolver"/> for identifier-to-constant-value
    /// lookup. When non-null, an <see cref="Identifier"/> node consults the resolver before giving up,
    /// enabling <c>const LIMIT: int = 200</c> then <c>LIMIT + 1</c> to fold (#1460). The resolver is
    /// threaded through recursive calls so compound expressions over constants fold transitively.
    /// </summary>
    public static bool TryGetConstantInteger(
        Expression expr, out BigInteger value, Func<string, BigInteger?>? resolver)
    {
        value = BigInteger.Zero;

        switch (expr)
        {
            case IntegerLiteral intLit:
                return TryParseIntegerLiteral(intLit.Value, out value);

            case Identifier id when resolver != null:
                {
                    var resolved = resolver(id.Name);
                    if (resolved == null)
                        return false;
                    value = resolved.Value;
                    return true;
                }

            case Parenthesized paren:
                return TryGetConstantInteger(paren.Expression, out value, resolver);

            case UnaryOp { Operator: UnaryOperator.Plus } plus:
                return TryGetConstantInteger(plus.Operand, out value, resolver);

            case UnaryOp { Operator: UnaryOperator.Minus } minus:
                if (!TryGetConstantInteger(minus.Operand, out var magnitude, resolver))
                    return false;
                value = -magnitude;
                return true;

            case BinaryOp
            {
                Operator: BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply
                    or BinaryOperator.LeftShift or BinaryOperator.RightShift
            } binary:
                if (!TryGetConstantInteger(binary.Left, out var left, resolver)
                    || !TryGetConstantInteger(binary.Right, out var right, resolver))
                {
                    return false;
                }
                if (binary.Operator is BinaryOperator.LeftShift or BinaryOperator.RightShift)
                {
                    if (right < 0)
                    {
                        value = default;
                        return false;
                    }
                    value = binary.Operator == BinaryOperator.LeftShift
                        ? left << (int)right
                        : left >> (int)right;
                    return true;
                }
                value = binary.Operator switch
                {
                    BinaryOperator.Add => left + right,
                    BinaryOperator.Subtract => left - right,
                    _ => left * right
                };
                return true;

            default:
                return false;
        }
    }

    private static bool TryParseIntegerLiteral(string raw, out BigInteger value)
    {
        value = BigInteger.Zero;
        var text = raw.Replace("_", "", System.StringComparison.Ordinal);
        if (text.Length == 0)
            return false;

        try
        {
            if (text.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
            {
                // Force a non-negative interpretation by prefixing "0" (BigInteger treats a
                // leading hex digit >= 8 as a negative two's-complement value otherwise).
                return BigInteger.TryParse(
                    "0" + text[2..],
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value);
            }
            if (text.StartsWith("0o", System.StringComparison.OrdinalIgnoreCase))
            {
                value = ParseBase(text[2..], 8);
                return true;
            }
            if (text.StartsWith("0b", System.StringComparison.OrdinalIgnoreCase))
            {
                value = ParseBase(text[2..], 2);
                return true;
            }

            return BigInteger.TryParse(
                text,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }
        catch (System.FormatException)
        {
            return false;
        }
    }

    // Only called for radix 2 and 8 (hex goes through BigInteger.TryParse above), so
    // numeric digits suffice. Malformed digits throw FormatException, which the caller's
    // try/catch converts to false — the lexer has already validated the literal, so this
    // is pure defense.
    private static BigInteger ParseBase(string digits, int radix)
    {
        var result = BigInteger.Zero;
        foreach (var c in digits)
        {
            var digit = c >= '0' && c <= '9' ? c - '0' : -1;
            if (digit < 0 || digit >= radix)
                throw new System.FormatException($"Invalid digit '{c}' for base {radix}");
            result = result * radix + digit;
        }
        return result;
    }
}
