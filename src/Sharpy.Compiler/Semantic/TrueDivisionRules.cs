using System.Globalization;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The one divisor rule for <c>/</c> (#2517), read by the type checker (which records whether a true
/// division routes through the zero-guarded <c>Builtins.TrueDiv</c>), by <see cref="ConstEligibility"/>'s
/// cross-module route (which has no recorded lowerings and must answer the same way from the AST and the
/// exported symbols alone), and by the const fold that records a float/decimal const's value
/// (<see cref="VariableSymbol.FloatConstantValue"/>). Pure: it reads literal text and the const symbols the
/// caller's <see cref="ConstantLookup"/> resolves.
/// </summary>
internal static class TrueDivisionRules
{
    /// <summary>
    /// How a constant NAME resolves: the <c>const</c> symbol a bare name (<see cref="Name"/>) or a qualified
    /// name (<see cref="Member"/>: <c>a.SCALE</c>, <c>C.K</c>) denotes, or null. Its folded value is
    /// <see cref="VariableSymbol.ConstantValue"/> (integers) or <see cref="VariableSymbol.FloatConstantValue"/>
    /// (float32/float64/decimal, already rounded to the declared type).
    /// </summary>
    public sealed record ConstantLookup(
        Func<Identifier, VariableSymbol?> Name,
        Func<MemberAccess, VariableSymbol?>? Member = null);

    /// <summary>The precision a folded value is held at; arithmetic is done in this type.</summary>
    private enum Precision { Integer, Float32, Float64, Decimal }

    /// <summary>
    /// Whether a <c>/</c> divisor is a compile-time constant known to be non-zero: a numeric literal, a
    /// const name, or <c>+</c>/<c>-</c>/<c>*</c>/unary minus over those, each step evaluated in the operand
    /// type's own precision (<see cref="TryEvaluate"/>). Such a division cannot trap, so it keeps the native
    /// C# operator and stays a C# constant expression when its operands are. A divisor that folds to ±0, that
    /// does not fold (a variable, a call, <c>/</c>, <c>**</c>, decimal arithmetic), or whose fold leaves the
    /// finite range answers <c>false</c> and keeps the runtime guard.
    /// </summary>
    public static bool IsNonZeroConstantDivisor(Expression divisor, ConstantLookup? lookup)
        => TryEvaluate(divisor, lookup, out var value, out _) && value != 0.0;

    /// <summary>
    /// The folded value of a float32/float64/decimal const's initializer ROUNDED TO ITS DECLARED TYPE, or null
    /// when it does not fold. The rounding is the narrowing the store performs: an unsuffixed <c>1e-46</c>
    /// stored in a <c>float32</c> const is <c>0f</c> at run time (float_literals.md, Narrowing), so its value
    /// here must be zero too, or a division by it would be judged non-zero and print inf.
    /// </summary>
    public static double? FoldFloatConstant(Expression? initializer, ConstantLookup? lookup, SemanticType declaredType)
    {
        if (initializer == null || !TryEvaluate(initializer, lookup, out var value, out _))
            return null;
        if (declaredType == SemanticType.Float32)
            return Finite((float)value);
        if (declaredType == SemanticType.Decimal)
            return value == 0.0 || Math.Abs(value) >= 1e-28 ? value : 0.0;
        return value;
    }

    private static double? Finite(double value) => double.IsFinite(value) ? value : null;

    /// <summary>
    /// Evaluates a constant numeric expression to a <see cref="double"/> together with the precision it is held
    /// at. Integer arithmetic is exact (<see cref="IntegerConstantEvaluator"/>); <c>+</c>, <c>-</c>, <c>*</c> and
    /// unary minus over float operands are evaluated in the promoted operand type, a float32 step rounded with
    /// <c>(float)</c> after every operation, as the emitted C# computes. Decimal arithmetic is not folded (a
    /// double cannot answer a decimal zero exactly), nor is any other operator. A non-finite step fails.
    /// </summary>
    private static bool TryEvaluate(Expression expression, ConstantLookup? lookup, out double value, out Precision precision)
    {
        if (IntegerConstantEvaluator.TryGetConstantInteger(
                expression, out var integer, lookup == null ? null : id => lookup.Name(id)?.ConstantValue))
        {
            value = (double)integer;
            precision = Precision.Integer;
            return double.IsFinite(value);
        }

        value = 0.0;
        precision = Precision.Float64;
        switch (AstHelper.UnwrapParenthesized(expression))
        {
            case UnaryOp { Operator: UnaryOperator.Plus } plus:
                return TryEvaluate(plus.Operand, lookup, out value, out precision);

            case UnaryOp { Operator: UnaryOperator.Minus } minus:
                if (!TryEvaluate(minus.Operand, lookup, out var operand, out precision))
                    return false;
                value = -operand;
                return true;

            case BinaryOp { Operator: BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply } binary:
                {
                    if (!TryEvaluate(binary.Left, lookup, out var left, out var leftPrecision)
                        || !TryEvaluate(binary.Right, lookup, out var right, out var rightPrecision))
                        return false;
                    if (leftPrecision == Precision.Decimal || rightPrecision == Precision.Decimal)
                        return false;
                    precision = leftPrecision == Precision.Float64 || rightPrecision == Precision.Float64
                        ? Precision.Float64
                        : leftPrecision == Precision.Float32 || rightPrecision == Precision.Float32
                            ? Precision.Float32
                            : Precision.Float64;
                    if (precision == Precision.Float32)
                    {
                        // C# converts an integer operand to float BEFORE the operation (`int - float`
                        // is `(float)int - float`): 16777217 becomes 16777216f, so round each operand.
                        left = (float)left;
                        right = (float)right;
                    }
                    var result = binary.Operator switch
                    {
                        BinaryOperator.Add => left + right,
                        BinaryOperator.Subtract => left - right,
                        _ => left * right,
                    };
                    if (precision == Precision.Float32)
                        result = (float)result;
                    value = result;
                    return double.IsFinite(result);
                }

            case FloatLiteral literal:
                {
                    if (FloatLiteralClassifier.Classify(literal.Value, literal.Suffix).IsError)
                        return false;
                    var text = literal.Value.Replace("_", "", StringComparison.Ordinal);
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                        return false;
                    var type = FloatLiteralClassifier.TypeOf(literal.Suffix);
                    precision = type == SemanticType.Float32 ? Precision.Float32
                        : type == SemanticType.Decimal ? Precision.Decimal
                        : Precision.Float64;
                    value = precision == Precision.Float32 ? (float)parsed : parsed;
                    return double.IsFinite(value);
                }

            case Identifier id:
                return FromConst(lookup?.Name(id), out value, out precision);

            case MemberAccess member:
                return FromConst(lookup?.Member?.Invoke(member), out value, out precision);

            default:
                return false;
        }
    }

    private static bool FromConst(VariableSymbol? symbol, out double value, out Precision precision)
    {
        value = 0.0;
        precision = Precision.Float64;
        if (symbol is not { IsConstant: true })
            return false;
        if (symbol.ConstantValue is { } integer)
        {
            value = (double)integer;
            precision = Precision.Integer;
            return double.IsFinite(value);
        }
        if (symbol.FloatConstantValue is not { } folded)
            return false;
        value = folded;
        precision = symbol.Type == SemanticType.Float32 ? Precision.Float32
            : symbol.Type == SemanticType.Decimal ? Precision.Decimal
            : Precision.Float64;
        return true;
    }
}
