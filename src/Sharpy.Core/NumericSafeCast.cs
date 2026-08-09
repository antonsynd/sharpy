namespace Sharpy
{
    /// <summary>
    /// Range-checked narrowing conversions between numeric primitives, emitted by the Sharpy compiler
    /// for the safe cast forms (<c>value to T?</c> / <c>value as? T</c>) when the source is a concrete
    /// numeric type. Each method returns <see cref="Optional{T}"/>: <c>Some(truncated)</c> when the value
    /// fits the target's representable integer range, <c>None</c> when it is out of range, NaN, or ±∞.
    /// </summary>
    /// <remarks>
    /// The boundary predicates are deliberately explicit because the representability of the target's
    /// extremes as <c>double</c> differs by target:
    /// <list type="bullet">
    /// <item><c>int.MinValue</c>/<c>int.MaxValue</c> are BOTH exactly representable as <c>double</c>, so
    /// an inclusive <c>[int.MinValue, int.MaxValue]</c> range check is exact.</item>
    /// <item><c>long.MinValue</c> (−2^63) is exactly representable, but <c>long.MaxValue</c> (2^63−1) is
    /// NOT — it rounds up to 2^63. The upper guard must therefore be a strict <c>&lt; 2^63</c>, never
    /// <c>&lt;= long.MaxValue</c> (which would round to 2^63 and admit an overflowing value).</item>
    /// </list>
    /// Truncation is toward zero (matching Python's <c>int(x)</c>: <c>int(3.9) == 3</c>,
    /// <c>int(-3.9) == -3</c>), performed by the C# cast after the range check guarantees no overflow.
    /// The <c>float</c> overloads widen to <c>double</c> first (a lossless conversion), so they reuse the
    /// exact <c>double</c> predicates rather than comparing against a target extreme that <c>float</c>
    /// cannot represent.
    /// <para>
    /// Sibling of <see cref="NumericCheckedCast"/>, which has the identical range predicates and throws
    /// instead of returning <c>None</c>. The compiler casts the argument to one of three hubs —
    /// <c>long</c>, <c>ulong</c>, <c>double</c> — before calling, because a <c>uint</c> argument would
    /// otherwise be CS0121-ambiguous between the <c>long</c> and <c>ulong</c> overloads (#1306). The
    /// pre-existing <c>float</c> overloads are kept for hand-written callers.
    /// </para>
    /// </remarks>
    public static class NumericSafeCast
    {
        // int.MinValue / int.MaxValue are exactly representable as double.
        private const double IntMin = int.MinValue;
        private const double IntMax = int.MaxValue;

        // long.MinValue (-2^63) is exactly representable as double; long.MaxValue (2^63-1) is not, so the
        // upper bound is the strictly-excluded power of two 2^63 (which IS exactly representable).
        private const double LongMin = long.MinValue;
        private const double LongUpperExclusive = 9223372036854775808.0;   // 2^63
        private const double ULongUpperExclusive = 18446744073709551616.0; // 2^64

        /// <summary>double → int?, truncating toward zero. None for NaN, ±∞, or out of int range.</summary>
        public static Optional<int> ToIntOrNone(double value)
        {
            if (double.IsNaN(value) || value < IntMin || value > IntMax)
            {
                return Optional<int>.None;
            }

            return Optional<int>.Some((int)value);
        }

        /// <summary>float → int?. Widens to double (lossless) and applies the exact double predicate.</summary>
        public static Optional<int> ToIntOrNone(float value) => ToIntOrNone((double)value);

        /// <summary>long → int?. None when out of int range; otherwise the exact truncated value.</summary>
        public static Optional<int> ToIntOrNone(long value)
        {
            if (value < int.MinValue || value > int.MaxValue)
            {
                return Optional<int>.None;
            }

            return Optional<int>.Some((int)value);
        }

        /// <summary>double → long?, truncating toward zero. None for NaN, ±∞, or out of long range.</summary>
        public static Optional<long> ToLongOrNone(double value)
        {
            if (double.IsNaN(value) || value < LongMin || value >= LongUpperExclusive)
            {
                return Optional<long>.None;
            }

            return Optional<long>.Some((long)value);
        }

        /// <summary>float → long?. Widens to double (lossless) and applies the exact double predicate.</summary>
        public static Optional<long> ToLongOrNone(float value) => ToLongOrNone((double)value);

        // ---------------------------------------------------------------------------------------
        // The widths beyond int/long (#1306). Before these existed the checker declined to classify a
        // byte/short/uint/ulong target, so `x as? byte` fell through to the type-pattern lowering —
        // CS8121 on a concrete numeric source, an ICE rather than a cast.
        // ---------------------------------------------------------------------------------------

        /// <summary>long → sbyte?.</summary>
        public static Optional<sbyte> ToSByteOrNone(long value)
            => value < sbyte.MinValue || value > sbyte.MaxValue
                ? Optional<sbyte>.None
                : Optional<sbyte>.Some((sbyte)value);

        /// <summary>long → byte?.</summary>
        public static Optional<byte> ToByteOrNone(long value)
            => value < byte.MinValue || value > byte.MaxValue
                ? Optional<byte>.None
                : Optional<byte>.Some((byte)value);

        /// <summary>long → short?.</summary>
        public static Optional<short> ToShortOrNone(long value)
            => value < short.MinValue || value > short.MaxValue
                ? Optional<short>.None
                : Optional<short>.Some((short)value);

        /// <summary>long → ushort?.</summary>
        public static Optional<ushort> ToUShortOrNone(long value)
            => value < ushort.MinValue || value > ushort.MaxValue
                ? Optional<ushort>.None
                : Optional<ushort>.Some((ushort)value);

        /// <summary>long → uint?.</summary>
        public static Optional<uint> ToUIntOrNone(long value)
            => value < uint.MinValue || value > uint.MaxValue
                ? Optional<uint>.None
                : Optional<uint>.Some((uint)value);

        /// <summary>long → ulong?. None only for negatives.</summary>
        public static Optional<ulong> ToULongOrNone(long value)
            => value < 0 ? Optional<ulong>.None : Optional<ulong>.Some((ulong)value);

        /// <summary>ulong → sbyte?.</summary>
        public static Optional<sbyte> ToSByteOrNone(ulong value)
            => value > (ulong)sbyte.MaxValue
                ? Optional<sbyte>.None
                : Optional<sbyte>.Some((sbyte)value);

        /// <summary>ulong → byte?.</summary>
        public static Optional<byte> ToByteOrNone(ulong value)
            => value > byte.MaxValue ? Optional<byte>.None : Optional<byte>.Some((byte)value);

        /// <summary>ulong → short?.</summary>
        public static Optional<short> ToShortOrNone(ulong value)
            => value > (ulong)short.MaxValue
                ? Optional<short>.None
                : Optional<short>.Some((short)value);

        /// <summary>ulong → ushort?.</summary>
        public static Optional<ushort> ToUShortOrNone(ulong value)
            => value > ushort.MaxValue
                ? Optional<ushort>.None
                : Optional<ushort>.Some((ushort)value);

        /// <summary>ulong → int?.</summary>
        public static Optional<int> ToIntOrNone(ulong value)
            => value > int.MaxValue ? Optional<int>.None : Optional<int>.Some((int)value);

        /// <summary>ulong → uint?.</summary>
        public static Optional<uint> ToUIntOrNone(ulong value)
            => value > uint.MaxValue ? Optional<uint>.None : Optional<uint>.Some((uint)value);

        /// <summary>ulong → long?.</summary>
        public static Optional<long> ToLongOrNone(ulong value)
            => value > long.MaxValue ? Optional<long>.None : Optional<long>.Some((long)value);

        /// <summary>double → sbyte?, truncating toward zero.</summary>
        public static Optional<sbyte> ToSByteOrNone(double value)
            => double.IsNaN(value) || value < sbyte.MinValue || value > sbyte.MaxValue
                ? Optional<sbyte>.None
                : Optional<sbyte>.Some((sbyte)value);

        /// <summary>double → byte?, truncating toward zero.</summary>
        public static Optional<byte> ToByteOrNone(double value)
            => double.IsNaN(value) || value < byte.MinValue || value > byte.MaxValue
                ? Optional<byte>.None
                : Optional<byte>.Some((byte)value);

        /// <summary>double → short?, truncating toward zero.</summary>
        public static Optional<short> ToShortOrNone(double value)
            => double.IsNaN(value) || value < short.MinValue || value > short.MaxValue
                ? Optional<short>.None
                : Optional<short>.Some((short)value);

        /// <summary>double → ushort?, truncating toward zero.</summary>
        public static Optional<ushort> ToUShortOrNone(double value)
            => double.IsNaN(value) || value < ushort.MinValue || value > ushort.MaxValue
                ? Optional<ushort>.None
                : Optional<ushort>.Some((ushort)value);

        /// <summary>double → uint?, truncating toward zero.</summary>
        public static Optional<uint> ToUIntOrNone(double value)
            => double.IsNaN(value) || value < uint.MinValue || value > uint.MaxValue
                ? Optional<uint>.None
                : Optional<uint>.Some((uint)value);

        /// <summary>double → ulong?, truncating toward zero.</summary>
        public static Optional<ulong> ToULongOrNone(double value)
            => double.IsNaN(value) || value < 0 || value >= ULongUpperExclusive
                ? Optional<ulong>.None
                : Optional<ulong>.Some((ulong)value);
    }
}
