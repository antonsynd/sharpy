using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for uint16 (ushort).
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to uint16. True becomes 1, False becomes 0.
        /// </summary>
        public static ushort UInt16(bool b)
        {
            return b ? (ushort)1 : (ushort)0;
        }

        /// <summary>
        /// Convert int to uint16.
        /// </summary>
        public static ushort UInt16(int i)
        {
            if (i < ushort.MinValue || i > ushort.MaxValue)
            {
                throw new OverflowError($"Value {i} is out of range for uint16");
            }
            return (ushort)i;
        }

        /// <summary>
        /// Convert long to uint16.
        /// </summary>
        public static ushort UInt16(long l)
        {
            if (l < ushort.MinValue || l > ushort.MaxValue)
            {
                throw new OverflowError($"Value {l} is out of range for uint16");
            }
            return (ushort)l;
        }

        /// <summary>
        /// Convert float to uint16 (truncates toward zero).
        /// </summary>
        public static ushort UInt16(float f)
        {
            if (float.IsNaN(f))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (float.IsPositiveInfinity(f) || float.IsNegativeInfinity(f))
            {
                throw new OverflowError($"Value {f} is out of range for uint16");
            }
            if (f < ushort.MinValue || f > ushort.MaxValue)
            {
                throw new OverflowError($"Value {f} is out of range for uint16");
            }
            return (ushort)f;
        }

        /// <summary>
        /// Convert double to uint16 (truncates toward zero).
        /// </summary>
        public static ushort UInt16(double d)
        {
            if (double.IsNaN(d))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (double.IsInfinity(d) || d < ushort.MinValue || d > ushort.MaxValue)
            {
                throw new OverflowError($"Value {d} is out of range for uint16");
            }
            return (ushort)d;
        }

        /// <summary>
        /// Convert decimal to uint16 (truncates toward zero).
        /// </summary>
        public static ushort UInt16(decimal m)
        {
            if (m < ushort.MinValue || m > ushort.MaxValue)
            {
                throw new OverflowError($"Value {m} is out of range for uint16");
            }
            return (ushort)m;
        }

        /// <summary>
        /// Parse string to uint16.
        /// </summary>
        public static ushort UInt16(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"invalid literal for uint16() with base 10: '{s}'");
            }

            s = s.Trim();

            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                throw new ValueError($"invalid literal for uint16() with base 10: '{s}'");
            }

            if (result < ushort.MinValue || result > ushort.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for uint16");
            }

            return (ushort)result;
        }

        /// <summary>
        /// Parse string to uint16 with explicit base.
        /// </summary>
        public static ushort UInt16(string s, int @base)
        {
            long result = ParseIntWithBase(s, @base, "uint16");
            if (result < ushort.MinValue || result > ushort.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for uint16");
            }
            return (ushort)result;
        }

        /// <summary>
        /// Convert byte to uint16 (widening).
        /// </summary>
        public static ushort UInt16(byte b)
        {
            return b;
        }

        /// <summary>
        /// Convert sbyte to uint16.
        /// </summary>
        public static ushort UInt16(sbyte sb)
        {
            if (sb < 0)
            {
                throw new OverflowError($"Value {sb} is out of range for uint16");
            }
            return (ushort)sb;
        }

        /// <summary>
        /// Convert short to uint16.
        /// </summary>
        public static ushort UInt16(short s)
        {
            if (s < 0)
            {
                throw new OverflowError($"Value {s} is out of range for uint16");
            }
            return (ushort)s;
        }

        /// <summary>
        /// Convert ushort to uint16 (identity).
        /// </summary>
        public static ushort UInt16(ushort us)
        {
            return us;
        }

        /// <summary>
        /// Convert uint to uint16.
        /// </summary>
        public static ushort UInt16(uint u)
        {
            if (u > ushort.MaxValue)
            {
                throw new OverflowError($"Value {u} is out of range for uint16");
            }
            return (ushort)u;
        }

        /// <summary>
        /// Convert ulong to uint16.
        /// </summary>
        public static ushort UInt16(ulong ul)
        {
            if (ul > ushort.MaxValue)
            {
                throw new OverflowError($"Value {ul} is out of range for uint16");
            }
            return (ushort)ul;
        }
    }
}
