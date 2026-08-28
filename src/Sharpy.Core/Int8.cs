using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for int8 (sbyte).
    /// Converts various types to System.SByte with Python-style parse rules
    /// and OverflowError for out-of-range values.
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to int8. True becomes 1, False becomes 0.
        /// </summary>
        public static sbyte Int8(bool b)
        {
            return b ? (sbyte)1 : (sbyte)0;
        }

        /// <summary>
        /// Convert int to int8.
        /// </summary>
        public static sbyte Int8(int i)
        {
            if (i < sbyte.MinValue || i > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {i} is out of range for int8");
            }
            return (sbyte)i;
        }

        /// <summary>
        /// Convert long to int8.
        /// </summary>
        public static sbyte Int8(long l)
        {
            if (l < sbyte.MinValue || l > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {l} is out of range for int8");
            }
            return (sbyte)l;
        }

        /// <summary>
        /// Convert float to int8 (truncates toward zero).
        /// </summary>
        public static sbyte Int8(float f)
        {
            if (float.IsNaN(f))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (float.IsPositiveInfinity(f) || float.IsNegativeInfinity(f))
            {
                throw new OverflowError($"Value {f} is out of range for int8");
            }
            if (f < sbyte.MinValue || f > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {f} is out of range for int8");
            }
            return (sbyte)f;
        }

        /// <summary>
        /// Convert double to int8 (truncates toward zero).
        /// </summary>
        public static sbyte Int8(double d)
        {
            if (double.IsNaN(d))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (double.IsInfinity(d) || d < sbyte.MinValue || d > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {d} is out of range for int8");
            }
            return (sbyte)d;
        }

        /// <summary>
        /// Convert decimal to int8 (truncates toward zero).
        /// </summary>
        public static sbyte Int8(decimal m)
        {
            if (m < sbyte.MinValue || m > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {m} is out of range for int8");
            }
            return (sbyte)m;
        }

        /// <summary>
        /// Parse string to int8.
        /// </summary>
        public static sbyte Int8(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"invalid literal for int8() with base 10: '{s}'");
            }

            s = s.Trim();

            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                throw new ValueError($"invalid literal for int8() with base 10: '{s}'");
            }

            if (result < sbyte.MinValue || result > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for int8");
            }

            return (sbyte)result;
        }

        /// <summary>
        /// Parse string to int8 with explicit base (2, 8, 10, or 16).
        /// </summary>
        public static sbyte Int8(string s, int @base)
        {
            long result = ParseIntWithBase(s, @base, "int8");
            if (result < sbyte.MinValue || result > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for int8");
            }
            return (sbyte)result;
        }

        /// <summary>
        /// Convert byte to int8.
        /// </summary>
        public static sbyte Int8(byte b)
        {
            if (b > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {b} is out of range for int8");
            }
            return (sbyte)b;
        }

        /// <summary>
        /// Convert sbyte to int8 (identity).
        /// </summary>
        public static sbyte Int8(sbyte sb)
        {
            return sb;
        }

        /// <summary>
        /// Convert short to int8.
        /// </summary>
        public static sbyte Int8(short s)
        {
            if (s < sbyte.MinValue || s > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {s} is out of range for int8");
            }
            return (sbyte)s;
        }

        /// <summary>
        /// Convert ushort to int8.
        /// </summary>
        public static sbyte Int8(ushort us)
        {
            if (us > sbyte.MaxValue)
            {
                throw new OverflowError($"Value {us} is out of range for int8");
            }
            return (sbyte)us;
        }

        /// <summary>
        /// Convert uint to int8.
        /// </summary>
        public static sbyte Int8(uint u)
        {
            if (u > (uint)sbyte.MaxValue)
            {
                throw new OverflowError($"Value {u} is out of range for int8");
            }
            return (sbyte)u;
        }

        /// <summary>
        /// Convert ulong to int8.
        /// </summary>
        public static sbyte Int8(ulong ul)
        {
            if (ul > (ulong)sbyte.MaxValue)
            {
                throw new OverflowError($"Value {ul} is out of range for int8");
            }
            return (sbyte)ul;
        }
    }
}
