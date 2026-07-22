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
    /// </remarks>
    public static class NumericSafeCast
    {
        // int.MinValue / int.MaxValue are exactly representable as double.
        private const double IntMin = int.MinValue;
        private const double IntMax = int.MaxValue;

        // long.MinValue (-2^63) is exactly representable as double; long.MaxValue (2^63-1) is not, so the
        // upper bound is the strictly-excluded power of two 2^63 (which IS exactly representable).
        private const double LongMin = long.MinValue;
        private const double LongUpperExclusive = 9223372036854775808.0; // 2^63

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
    }
}
