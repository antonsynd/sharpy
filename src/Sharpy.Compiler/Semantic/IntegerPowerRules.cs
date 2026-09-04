using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The ONE integer <c>**</c> rule (#1700): the <c>Sharpy.Builtins.CheckedIntPow</c> overload the
/// emitter binds AND the result type the checker records come from a single classification of the
/// operand pair, so the tag and the type cannot drift.
///
/// <para><b>Why one classification.</b> The lowering kind and the result type were derived by two
/// separate pieces of arithmetic — <c>ClassifyIntegerPower</c> keyed on the operand types, and
/// <c>InferPowerResultType</c> on <c>GetPromotedType</c> plus a width fixup. For <c>(int32,
/// uint64)</c> promotion answers <c>null</c> (C# has no operator for that pair, §12.4.7), so the
/// type fell back to <c>int32</c> while the tag picked the <c>(long, ulong)</c> overload, whose
/// CLR result is <c>long</c>: <c>z: int32 = a ** n</c> was SPY0908/CS0266 and <c>b: bool = a ** n</c>
/// named <c>int32</c>. <c>**</c> is a CALL, so promotion is not its rule — its overload set is
/// (#1662's shape): <c>(int,int)</c>, <c>(long,long)</c>, <c>(ulong,ulong)</c>, <c>(ulong,long)</c>,
/// <c>(long,ulong)</c>, and the recorded type is the bound overload's return type.</para>
///
/// <para><b>The rule</b> (plan-299c1b Decision 4), applied to the EFFECTIVE operand types — the
/// constant-operand pre-step (<c>EffectiveOperandTypes</c>, §10.2.11) runs first, so
/// <c>u64 ** 2</c> arrives here as <c>(uint64, uint64)</c>:</para>
/// <list type="bullet">
/// <item><c>(uint64, unsigned)</c> → <c>CheckedIntPow(ulong, ulong)</c> → <c>uint64</c>.</item>
/// <item><c>(uint64, signed)</c> → <c>CheckedIntPow(ulong, long)</c> → <c>uint64</c> (the exponent
/// keeps its own width; the overload's negative-exponent branch answers the spec's
/// <c>x ** -1 == 0</c>).</item>
/// <item><c>(signed, uint64)</c> → <c>CheckedIntPow(long, ulong)</c> → <c>int64</c>.</item>
/// <item>otherwise the promoted width decides: <c>uint32</c> or <c>int64</c> →
/// <c>CheckedIntPow(long, long)</c> → <c>int64</c> (there is no <c>uint</c> overload — the spec's
/// <c>uint32 ** uint32 → int64</c> row); every narrower width floors to <c>int</c> →
/// <c>CheckedIntPow(int, int)</c> → <c>int32</c>.</item>
/// </list>
/// </summary>
internal static class IntegerPowerRules
{
    /// <summary>The pair (lowering tag, recorded result type) that one classification produces.</summary>
    internal readonly record struct Classification(OperatorLoweringKind Kind, SemanticType ResultType);

    /// <summary>
    /// Classifies an integer <c>**</c> from its effective operand types. Returns <c>null</c> when
    /// either operand is not a primitive integer (float/decimal/user-defined pairs are classified
    /// by the caller).
    /// </summary>
    internal static Classification? Classify(SemanticType left, SemanticType right)
    {
        if (!TypeUtils.IsInteger(left) || !TypeUtils.IsInteger(right))
            return null;

        var leftInfo = PrimitiveCatalog.GetPrimitiveInfo(left);
        var rightInfo = PrimitiveCatalog.GetPrimitiveInfo(right);
        if (leftInfo == null || rightInfo == null)
            return null;

        var leftIsULong = left == SemanticType.ULong;
        var rightIsULong = right == SemanticType.ULong;

        if (leftIsULong)
        {
            return rightIsULong || !rightInfo.IsSigned
                ? new Classification(OperatorLoweringKind.IntegerPowULong, SemanticType.ULong)
                : new Classification(OperatorLoweringKind.IntegerPowULongExponentLong, SemanticType.ULong);
        }

        if (rightIsULong)
            return new Classification(OperatorLoweringKind.IntegerPowLongExponentULong, SemanticType.Long);

        // Neither operand is ulong, so promotion never answers null here; the `?? left` keeps the
        // classification total for a pair GetPromotedType does not rank.
        var width = TypeInferenceService.ApplyIntegerFloor(
            PrimitiveCatalog.GetPromotedType(left, right) ?? left);

        return width == SemanticType.UInt || width == SemanticType.Long
            ? new Classification(OperatorLoweringKind.IntegerPowLong, SemanticType.Long)
            : new Classification(OperatorLoweringKind.IntegerPowInt, SemanticType.Int);
    }
}
