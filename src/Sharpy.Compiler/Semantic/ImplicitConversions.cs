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

        return double.TryParse(
                literal.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value)
            && !double.IsInfinity((double)(float)value);
    }

    /// <summary>
    /// Whether an unsuffixed <see cref="FloatLiteral"/> typed as <c>double</c> can narrow to
    /// <c>decimal</c> without going out of range. Twin of <see cref="IsFloat32LiteralNarrowing"/>.
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

        if (!double.TryParse(
                literal.Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
            return false;

        return double.IsFinite(value);
    }
}
