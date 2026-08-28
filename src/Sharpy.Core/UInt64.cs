using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for uint64 (ulong).
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to uint64. True becomes 1, False becomes 0.
        /// </summary>
        public static ulong UInt64(bool b)
        {
            return b ? 1UL : 0UL;
        }

        /// <summary>
        /// Convert int to uint64.
        /// </summary>
        public static ulong UInt64(int i)
        {
            if (i < 0)
            {
                throw new OverflowError($"Value {i} is out of range for uint64");
            }
            return (ulong)i;
        }

        /// <summary>
        /// Convert long to uint64.
        /// </summary>
        public static ulong UInt64(long l)
        {
            if (l < 0)
            {
                throw new OverflowError($"Value {l} is out of range for uint64");
            }
            return (ulong)l;
        }

        /// <summary>
        /// Convert float to uint64 (truncates toward zero).
        /// </summary>
        public static ulong UInt64(float f)
        {
            if (float.IsNaN(f))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (float.IsPositiveInfinity(f) || float.IsNegativeInfinity(f))
            {
                throw new OverflowError($"Value {f} is out of range for uint64");
            }
            if (f < 0 || f >= 18446744073709551616.0f)
            {
                throw new OverflowError($"Value {f} is out of range for uint64");
            }
            return (ulong)f;
        }

        /// <summary>
        /// Convert double to uint64 (truncates toward zero).
        /// </summary>
        public static ulong UInt64(double d)
        {
            if (double.IsNaN(d))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (double.IsInfinity(d) || d < 0 || d >= 18446744073709551616.0)
            {
                throw new OverflowError($"Value {d} is out of range for uint64");
            }
            return (ulong)d;
        }

        /// <summary>
        /// Convert decimal to uint64 (truncates toward zero).
        /// </summary>
        public static ulong UInt64(decimal m)
        {
            if (m < ulong.MinValue || m > ulong.MaxValue)
            {
                throw new OverflowError($"Value {m} is out of range for uint64");
            }
            return (ulong)m;
        }

        /// <summary>
        /// Parse string to uint64.
        /// </summary>
        public static ulong UInt64(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"invalid literal for uint64() with base 10: '{s}'");
            }

            s = s.Trim();

            if (!ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong result))
            {
                throw new ValueError($"invalid literal for uint64() with base 10: '{s}'");
            }

            return result;
        }

        /// <summary>
        /// Parse string to uint64 with explicit base.
        /// </summary>
        public static ulong UInt64(string s, int @base)
        {
            long result = ParseIntWithBase(s, @base, "uint64");
            if (result < 0)
            {
                throw new OverflowError($"Value {result} is out of range for uint64");
            }
            return (ulong)result;
        }

        /// <summary>
        /// Convert byte to uint64 (widening).
        /// </summary>
        public static ulong UInt64(byte b)
        {
            return b;
        }

        /// <summary>
        /// Convert sbyte to uint64.
        /// </summary>
        public static ulong UInt64(sbyte sb)
        {
            if (sb < 0)
            {
                throw new OverflowError($"Value {sb} is out of range for uint64");
            }
            return (ulong)sb;
        }

        /// <summary>
        /// Convert short to uint64.
        /// </summary>
        public static ulong UInt64(short s)
        {
            if (s < 0)
            {
                throw new OverflowError($"Value {s} is out of range for uint64");
            }
            return (ulong)s;
        }

        /// <summary>
        /// Convert ushort to uint64 (widening).
        /// </summary>
        public static ulong UInt64(ushort us)
        {
            return us;
        }

        /// <summary>
        /// Convert uint to uint64 (widening).
        /// </summary>
        public static ulong UInt64(uint u)
        {
            return u;
        }

        /// <summary>
        /// Convert ulong to uint64 (identity).
        /// </summary>
        public static ulong UInt64(ulong ul)
        {
            return ul;
        }
    }
}
