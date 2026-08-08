namespace Sharpy
{
    /// <summary>
    /// Throwing numeric narrowing conversions between numeric primitives, emitted by the Sharpy
    /// compiler for the checked cast form (<c>value as! T</c>) when the source is a concrete
    /// numeric type. Each method throws <see cref="OverflowError"/> for out-of-range values and
    /// <see cref="ValueError"/> for NaN — matching CPython's <c>int()</c> behavior and the house
    /// pattern (<c>CheckedIntPow</c>, <c>FloorDiv</c>), not <c>System.OverflowException</c> (#1306).
    /// </summary>
    /// <remarks>
    /// Boundary constants are shared with <see cref="NumericSafeCast"/> — the range checks are
    /// identical, only the failure action differs (throw vs None).
    /// </remarks>
    public static class NumericCheckedCast
    {
        private const double IntMin = int.MinValue;
        private const double IntMax = int.MaxValue;
        private const double LongMin = long.MinValue;
        private const double LongUpperExclusive = 9223372036854775808.0; // 2^63

        /// <summary>double → int, truncating toward zero. Throws for NaN, ±∞, or out of int range.</summary>
        public static int ToInt(double value)
        {
            if (double.IsNaN(value))
                throw new ValueError($"Cannot convert NaN to int");
            if (value < IntMin || value > IntMax)
                throw new OverflowError($"Value {value} is out of range for int");
            return (int)value;
        }

        /// <summary>float → int.</summary>
        public static int ToInt(float value) => ToInt((double)value);

        /// <summary>long → int.</summary>
        public static int ToInt(long value)
        {
            if (value < int.MinValue || value > int.MaxValue)
                throw new OverflowError($"Value {value} is out of range for int");
            return (int)value;
        }

        /// <summary>double → long, truncating toward zero.</summary>
        public static long ToLong(double value)
        {
            if (double.IsNaN(value))
                throw new ValueError($"Cannot convert NaN to long");
            if (value < LongMin || value >= LongUpperExclusive)
                throw new OverflowError($"Value {value} is out of range for long");
            return (long)value;
        }

        /// <summary>float → long.</summary>
        public static long ToLong(float value) => ToLong((double)value);

        /// <summary>double → byte.</summary>
        public static byte ToByte(double value)
        {
            if (double.IsNaN(value))
                throw new ValueError($"Cannot convert NaN to byte");
            if (value < byte.MinValue || value > byte.MaxValue)
                throw new OverflowError($"Value {value} is out of range for byte");
            return (byte)value;
        }

        /// <summary>long → byte.</summary>
        public static byte ToByte(long value)
        {
            if (value < byte.MinValue || value > byte.MaxValue)
                throw new OverflowError($"Value {value} is out of range for byte");
            return (byte)value;
        }

        /// <summary>int → byte.</summary>
        public static byte ToByte(int value)
        {
            if (value < byte.MinValue || value > byte.MaxValue)
                throw new OverflowError($"Value {value} is out of range for byte");
            return (byte)value;
        }

        /// <summary>double → uint.</summary>
        public static uint ToUInt(double value)
        {
            if (double.IsNaN(value))
                throw new ValueError($"Cannot convert NaN to uint");
            if (value < uint.MinValue || value > uint.MaxValue)
                throw new OverflowError($"Value {value} is out of range for uint");
            return (uint)value;
        }

        /// <summary>long → uint.</summary>
        public static uint ToUInt(long value)
        {
            if (value < uint.MinValue || value > uint.MaxValue)
                throw new OverflowError($"Value {value} is out of range for uint");
            return (uint)value;
        }

        /// <summary>double → ulong.</summary>
        public static ulong ToULong(double value)
        {
            if (double.IsNaN(value))
                throw new ValueError($"Cannot convert NaN to ulong");
            if (value < 0 || value >= 18446744073709551616.0) // 2^64
                throw new OverflowError($"Value {value} is out of range for ulong");
            return (ulong)value;
        }

        /// <summary>long → ulong.</summary>
        public static ulong ToULong(long value)
        {
            if (value < 0)
                throw new OverflowError($"Value {value} is out of range for ulong");
            return (ulong)value;
        }
    }
}
