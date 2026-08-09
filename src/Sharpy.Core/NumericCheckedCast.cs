namespace Sharpy
{
    /// <summary>
    /// Throwing numeric narrowing conversions between numeric primitives, emitted by the Sharpy
    /// compiler for the checked cast form (<c>value as! T</c> / <c>value to T</c>) when the source is
    /// a concrete numeric type. Each method throws <see cref="OverflowError"/> for out-of-range values
    /// and <see cref="ValueError"/> for NaN — matching CPython's <c>int()</c> behavior and the house
    /// pattern (<c>CheckedIntPow</c>, <c>FloorDiv</c>), not <c>System.OverflowException</c>, which
    /// Sharpy's <c>except OverflowError</c> cannot catch (#1306).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sibling of <see cref="NumericSafeCast"/>: identical range predicates, opposite failure action
    /// (throw vs <c>None</c>). The two files must stay in lockstep — a pair the compiler classifies as
    /// narrowing must have a method here AND an <c>OrNone</c> method there.
    /// </para>
    /// <para>
    /// <b>Three source hubs, not one overload per source type.</b> The compiler casts the argument to
    /// <c>long</c>, <c>ulong</c>, or <c>double</c> before calling, so only those three parameter shapes
    /// exist. That cast is not cosmetic: with overloads for both <c>long</c> and <c>ulong</c>, a
    /// <c>uint</c> argument converts implicitly to either and neither is better, so unqualified calls
    /// would be CS0121-ambiguous. Pinning the hub in the emitted syntax makes overload resolution
    /// deterministic for every source width.
    /// </para>
    /// <para>
    /// <b>Range predicates are on the value, not on its truncation.</b> A <c>double</c> is accepted when
    /// it lies within the target's representable range and is then truncated toward zero by the C# cast
    /// (Python's <c>int(3.9) == 3</c>, <c>int(-3.9) == -3</c>). Values such as <c>-0.5</c> therefore fail
    /// for an unsigned target even though truncation would land on <c>0</c> — the same convention
    /// <see cref="NumericSafeCast"/> has always used for <c>int</c>.
    /// </para>
    /// <para>
    /// <c>long.MaxValue</c> (2^63−1) is NOT exactly representable as <c>double</c> — it rounds up to
    /// 2^63 — so its upper guard is the strict exclusive power of two, never <c>&lt;= long.MaxValue</c>.
    /// Same for <c>ulong.MaxValue</c> and 2^64.
    /// </para>
    /// </remarks>
    public static class NumericCheckedCast
    {
        // Exactly representable as double; 2^63 and 2^64 are the strict exclusive upper bounds for the
        // 64-bit targets whose MaxValue is not.
        private const double LongMin = long.MinValue;
        private const double LongUpperExclusive = 9223372036854775808.0;   // 2^63
        private const double ULongUpperExclusive = 18446744073709551616.0; // 2^64

        // ---------------------------------------------------------------------------------------
        // long hub — every signed/unsigned integral source up to 64 bits except ulong widens to long.
        // ---------------------------------------------------------------------------------------

        /// <summary>long → sbyte.</summary>
        public static sbyte ToSByte(long value)
        {
            if (value < sbyte.MinValue || value > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for sbyte");
            }

            return (sbyte)value;
        }

        /// <summary>long → byte.</summary>
        public static byte ToByte(long value)
        {
            if (value < byte.MinValue || value > byte.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for byte");
            }

            return (byte)value;
        }

        /// <summary>long → short.</summary>
        public static short ToShort(long value)
        {
            if (value < short.MinValue || value > short.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for short");
            }

            return (short)value;
        }

        /// <summary>long → ushort.</summary>
        public static ushort ToUShort(long value)
        {
            if (value < ushort.MinValue || value > ushort.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for ushort");
            }

            return (ushort)value;
        }

        /// <summary>long → int.</summary>
        public static int ToInt(long value)
        {
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for int");
            }

            return (int)value;
        }

        /// <summary>long → uint.</summary>
        public static uint ToUInt(long value)
        {
            if (value < uint.MinValue || value > uint.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for uint");
            }

            return (uint)value;
        }

        /// <summary>long → ulong. Fails only for negatives.</summary>
        public static ulong ToULong(long value)
        {
            if (value < 0)
            {
                throw new OverflowError($"Value {value} is out of range for ulong");
            }

            return (ulong)value;
        }

        // ---------------------------------------------------------------------------------------
        // ulong hub — the one integral source with no implicit conversion to long.
        // ---------------------------------------------------------------------------------------

        /// <summary>ulong → sbyte.</summary>
        public static sbyte ToSByte(ulong value)
        {
            if (value > (ulong)sbyte.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for sbyte");
            }

            return (sbyte)value;
        }

        /// <summary>ulong → byte.</summary>
        public static byte ToByte(ulong value)
        {
            if (value > byte.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for byte");
            }

            return (byte)value;
        }

        /// <summary>ulong → short.</summary>
        public static short ToShort(ulong value)
        {
            if (value > (ulong)short.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for short");
            }

            return (short)value;
        }

        /// <summary>ulong → ushort.</summary>
        public static ushort ToUShort(ulong value)
        {
            if (value > ushort.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for ushort");
            }

            return (ushort)value;
        }

        /// <summary>ulong → int.</summary>
        public static int ToInt(ulong value)
        {
            if (value > int.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for int");
            }

            return (int)value;
        }

        /// <summary>ulong → uint.</summary>
        public static uint ToUInt(ulong value)
        {
            if (value > uint.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for uint");
            }

            return (uint)value;
        }

        /// <summary>ulong → long.</summary>
        public static long ToLong(ulong value)
        {
            if (value > long.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for long");
            }

            return (long)value;
        }

        // ---------------------------------------------------------------------------------------
        // double hub — float widens to double losslessly, so both floating sources land here.
        // ---------------------------------------------------------------------------------------

        /// <summary>double → sbyte, truncating toward zero.</summary>
        public static sbyte ToSByte(double value)
        {
            if (double.IsNaN(value))
            {
                throw new ValueError("Cannot convert NaN to sbyte");
            }
            if (value < sbyte.MinValue || value > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for sbyte");
            }

            return (sbyte)value;
        }

        /// <summary>double → byte, truncating toward zero.</summary>
        public static byte ToByte(double value)
        {
            if (double.IsNaN(value))
            {
                throw new ValueError("Cannot convert NaN to byte");
            }
            if (value < byte.MinValue || value > byte.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for byte");
            }

            return (byte)value;
        }

        /// <summary>double → short, truncating toward zero.</summary>
        public static short ToShort(double value)
        {
            if (double.IsNaN(value))
            {
                throw new ValueError("Cannot convert NaN to short");
            }
            if (value < short.MinValue || value > short.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for short");
            }

            return (short)value;
        }

        /// <summary>double → ushort, truncating toward zero.</summary>
        public static ushort ToUShort(double value)
        {
            if (double.IsNaN(value))
            {
                throw new ValueError("Cannot convert NaN to ushort");
            }
            if (value < ushort.MinValue || value > ushort.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for ushort");
            }

            return (ushort)value;
        }

        /// <summary>double → int, truncating toward zero.</summary>
        public static int ToInt(double value)
        {
            if (double.IsNaN(value))
            {
                throw new ValueError("Cannot convert NaN to int");
            }
            if (value < int.MinValue || value > int.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for int");
            }

            return (int)value;
        }

        /// <summary>double → uint, truncating toward zero.</summary>
        public static uint ToUInt(double value)
        {
            if (double.IsNaN(value))
            {
                throw new ValueError("Cannot convert NaN to uint");
            }
            if (value < uint.MinValue || value > uint.MaxValue)
            {
                throw new OverflowError($"Value {value} is out of range for uint");
            }

            return (uint)value;
        }

        /// <summary>double → long, truncating toward zero.</summary>
        public static long ToLong(double value)
        {
            if (double.IsNaN(value))
            {
                throw new ValueError("Cannot convert NaN to long");
            }
            if (value < LongMin || value >= LongUpperExclusive)
            {
                throw new OverflowError($"Value {value} is out of range for long");
            }

            return (long)value;
        }

        /// <summary>double → ulong, truncating toward zero.</summary>
        public static ulong ToULong(double value)
        {
            if (double.IsNaN(value))
            {
                throw new ValueError("Cannot convert NaN to ulong");
            }
            if (value < 0 || value >= ULongUpperExclusive)
            {
                throw new OverflowError($"Value {value} is out of range for ulong");
            }

            return (ulong)value;
        }
    }
}
