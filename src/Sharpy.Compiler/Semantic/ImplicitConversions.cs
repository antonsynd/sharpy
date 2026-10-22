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
    ///
    /// <para>An UNSUFFIXED integer literal converts by VALUE to every unsigned type (R-IC, #2344):
    /// <c>x: uint32 = 4294967295</c> and <c>m: uint32 = 0xFFFFFFFF</c> compile as C#'s
    /// <c>uint x = 4294967295;</c> does. C# reaches that by typing the literal <c>uint</c> (the first
    /// of int, uint, long, ulong its value fits); Sharpy types an unsuffixed literal above
    /// <c>int32</c> as <c>int64</c> (<c>integer_literals.md</c>), so the value rule is what admits it.
    /// A literal that does not fit keeps its own type. Any other <c>int64</c> constant — <c>5L</c>, a
    /// <c>const L: int64</c>, <c>4294967296 - 1</c> — converts to <c>uint64</c> only, as C#'s
    /// <c>long</c> constants do (§10.2.11, #1355).</para>
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
        {
            return targetInfo.Kind == PrimitiveCatalog.NumericKind.UnsignedInteger
                && FitsInRange(constant, targetInfo)
                && (targetInfo.ClrType == typeof(ulong)
                    || Shared.AstHelper.UnwrapParenthesized(value) is IntegerLiteral { Suffix: null });
        }

        if (sourceInfo.ClrType != typeof(int))
            return false;

        return targetInfo.Kind is PrimitiveCatalog.NumericKind.SignedInteger or PrimitiveCatalog.NumericKind.UnsignedInteger
            && targetInfo.ClrType != typeof(int)
            && targetInfo.ClrType != typeof(long)
            && FitsInRange(constant, targetInfo);
    }

    /// <summary>
    /// Whether a constant of <paramref name="source"/> admitted into <paramref name="target"/> by
    /// <see cref="IsImplicitIntegerConstantConversion"/> needs the conversion spelled in C#: C#
    /// §10.2.11 converts a <c>long</c> constant only to <c>ulong</c>, and an <c>int64</c> constant is
    /// emitted with its <c>L</c> suffix, so <c>uint x = 4294967295L;</c> would be CS0266 (#2344).
    /// </summary>
    public static bool RequiresConstantCast(SemanticType source, SemanticType target)
    {
        var sourceInfo = PrimitiveCatalog.GetPrimitiveInfo(source);
        var targetInfo = PrimitiveCatalog.GetPrimitiveInfo(target);
        return sourceInfo?.ClrType == typeof(long)
            && targetInfo != null
            && targetInfo.ClrType != typeof(long)
            && targetInfo.ClrType != typeof(ulong);
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
    ///   wrong value, so a non-zero literal that becomes zero is refused — by the same
    ///   <see cref="Shared.FloatLiteralClassifier.Check"/> the suffixed <c>1e-30m</c> asks (#2344).</item>
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

        // FORM, RANGE and UNDERFLOW are the literal-typing arm's own question (#2340, #2344): one
        // seam answers "does this text have a decimal value" for `1e29m` / `1e-30m` and for
        // `d: decimal = 1e29` / `d: decimal = 1e-30` alike.
        return Shared.FloatLiteralClassifier.Fits(literal.Value, typeof(double))
            && Shared.FloatLiteralClassifier.Fits(literal.Value, typeof(decimal));
    }
}
