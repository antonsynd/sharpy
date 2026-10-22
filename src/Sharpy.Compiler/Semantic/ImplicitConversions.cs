using System.Globalization;
using System.Numerics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Static helpers that decide whether a value of one type can be stored into a slot of another
/// type WITHOUT an explicit cast — the implicit-conversion family. Extracted from
/// <see cref="TypeChecker"/> so every store position can share the same decision logic.
/// </summary>
internal static class ImplicitConversions
{
    /// <summary>
    /// Whether <paramref name="value"/> is an integer constant expression whose folded value fits
    /// in <paramref name="target"/>'s range. Renamed from <c>IsImplicitConstantConversion</c>.
    /// </summary>
    public static bool IsImplicitIntegerConstantConversion(
        Expression? value,
        SemanticType source,
        SemanticType target,
        Func<Identifier, BigInteger?>? resolveConstant)
    {
        if (value == null)
            return false;

        if (!IntegerConstantEvaluator.TryGetConstantInteger(value, out var constant, resolveConstant))
            return false;

        var sourceInfo = PrimitiveCatalog.GetPrimitiveInfo(source);
        var targetInfo = PrimitiveCatalog.GetPrimitiveInfo(target);
        if (sourceInfo == null || targetInfo == null)
            return false;

        if (sourceInfo.ClrType == typeof(long))
            return targetInfo.ClrType == typeof(ulong) && constant.Sign >= 0;

        if (sourceInfo.ClrType != typeof(int))
            return false;

        return targetInfo.Kind is PrimitiveCatalog.NumericKind.SignedInteger or PrimitiveCatalog.NumericKind.UnsignedInteger
            && targetInfo.ClrType != typeof(int)
            && targetInfo.ClrType != typeof(long)
            && FitsInRange(constant, targetInfo);
    }

    /// <summary>
    /// Whether an exact constant lies in <paramref name="target"/>'s range, derived from
    /// <see cref="PrimitiveCatalog.PrimitiveInfo.SizeInBits"/> and
    /// <see cref="PrimitiveCatalog.PrimitiveInfo.IsSigned"/>.
    /// </summary>
    public static bool FitsInRange(BigInteger constant, PrimitiveCatalog.PrimitiveInfo target)
    {
        if (target.IsSigned)
        {
            var limit = BigInteger.One << (target.SizeInBits - 1);
            return constant >= -limit && constant < limit;
        }

        return constant.Sign >= 0 && constant < (BigInteger.One << target.SizeInBits);
    }

    /// <summary>
    /// Whether an unsuffixed <see cref="FloatLiteral"/> typed as <c>double</c> can narrow to
    /// <c>float32</c> without going out of range.
    /// </summary>
    public static bool IsFloat32LiteralNarrowing(
        SemanticType declaredType,
        SemanticType initType,
        Expression? initialValue)
    {
        if (initialValue is not FloatLiteral { Suffix: null } literal)
            return false;

        if (PrimitiveCatalog.GetPrimitiveInfo(declaredType)?.ClrType != typeof(float)
            || PrimitiveCatalog.GetPrimitiveInfo(initType)?.ClrType != typeof(double))
            return false;

        // The range question is the literal-typing arm's own (#2340): one seam answers "does this
        // text have a finite float32 value" for `1e39f` and for `x: float32 = 1e39` alike. A miss
        // is the refusal (`1e40` is SPY0220, never a crash), and an exponent form that fits
        // (`1.5e2`) narrows exactly as a plain one does.
        return Shared.FloatLiteralClassifier.Fits(literal.Value, typeof(float));
    }

    /// <summary>
    /// Whether an unsuffixed <see cref="FloatLiteral"/> typed as <c>double</c> can narrow to
    /// <c>decimal</c>. Twin of <see cref="IsFloat32LiteralNarrowing"/>.
    ///
    /// <para>Finiteness as a <c>double</c> is not the question — <c>decimal</c>'s range is
    /// ±7.9e28, far narrower, and its smallest non-zero magnitude is 1e-28. The predicate used to
    /// ask only <c>double.IsFinite</c>, so it admitted <c>1e40</c>, the seam re-typed the literal
    /// to <c>decimal</c>, and the emitter's <c>decimal.Parse</c> threw — SPY0909, a compiler CRASH
    /// where the honest answer is the SPY0220 the same store drew before the decimal arm existed.
    /// Three questions, all answered without throwing:</para>
    /// <list type="number">
    ///   <item>FORM — <c>decimal.TryParse</c> under <see cref="NumberStyles.Float"/>, which is what
    ///   makes an exponent literal (<c>1.5e2</c>) a decimal literal at all.</item>
    ///   <item>RANGE — the same call returns false for <c>1e40</c>, above decimal's maximum.</item>
    ///   <item>UNDERFLOW — <c>1e-30</c> PARSES, to <c>0m</c>. Admitting it would store a silently
    ///   wrong value, so a non-zero literal that becomes zero is refused.</item>
    /// </list>
    /// </summary>
    public static bool IsDecimalLiteralNarrowing(
        SemanticType declaredType,
        SemanticType initType,
        Expression? initialValue)
    {
        if (initialValue is not FloatLiteral { Suffix: null } literal)
            return false;

        if (PrimitiveCatalog.GetPrimitiveInfo(declaredType)?.ClrType != typeof(decimal)
            || PrimitiveCatalog.GetPrimitiveInfo(initType)?.ClrType != typeof(double))
            return false;

        // FORM and RANGE are the literal-typing arm's own question (#2340): one seam answers
        // "does this text have a decimal value" for `1e29m` and for `d: decimal = 1e29` alike.
        if (!Shared.FloatLiteralClassifier.Fits(literal.Value, typeof(double))
            || !Shared.FloatLiteralClassifier.Fits(literal.Value, typeof(decimal)))
        {
            return false;
        }

        // UNDERFLOW stays a narrowing-only question: an unsuffixed literal is a float64, and
        // re-typing a non-zero one to a decimal zero would change its value.
        var asDouble = double.Parse(literal.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        var asDecimal = decimal.Parse(literal.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        return asDecimal != 0m || asDouble == 0.0;
    }
}
