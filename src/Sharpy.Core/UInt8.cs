using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for uint8 (byte).
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to uint8. True becomes 1, False becomes 0.
        /// </summary>
        public static byte UInt8(bool b)
        {
            return b ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// Convert int to uint8.
        /// </summary>
        public static byte UInt8(int i)
        {
            if (i < byte.MinValue || i > byte.MaxValue)
            {
                throw new OverflowError($"Value {i} is out of range for uint8");
            }
            return (byte)i;
        }

        /// <summary>
        /// Convert long to uint8.
        /// </summary>
        public static byte UInt8(long l)
        {
            if (l < byte.MinValue || l > byte.MaxValue)
            {
                throw new OverflowError($"Value {l} is out of range for uint8");
            }
            return (byte)l;
        }

        /// <summary>
        /// Convert float to uint8 (truncates toward zero).
        /// </summary>
        public static byte UInt8(float f)
        {
            if (float.IsNaN(f))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (float.IsPositiveInfinity(f) || float.IsNegativeInfinity(f))
            {
                throw new OverflowError($"Value {f} is out of range for uint8");
            }
            if (f < byte.MinValue || f > byte.MaxValue)
            {
                throw new OverflowError($"Value {f} is out of range for uint8");
            }
            return (byte)f;
        }

        /// <summary>
        /// Convert double to uint8 (truncates toward zero).
        /// </summary>
        public static byte UInt8(double d)
        {
            if (double.IsNaN(d))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (double.IsInfinity(d) || d < byte.MinValue || d > byte.MaxValue)
            {
                throw new OverflowError($"Value {d} is out of range for uint8");
            }
            return (byte)d;
        }

        /// <summary>
        /// Convert decimal to uint8 (truncates toward zero).
        /// </summary>
        public static byte UInt8(decimal m)
        {
            if (m < byte.MinValue || m > byte.MaxValue)
            {
                throw new OverflowError($"Value {m} is out of range for uint8");
            }
            return (byte)m;
        }

        /// <summary>
        /// Parse string to uint8.
        /// </summary>
        public static byte UInt8(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"invalid literal for uint8() with base 10: '{s}'");
            }

            s = s.Trim();

            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                throw new ValueError($"invalid literal for uint8() with base 10: '{s}'");
            }

            if (result < byte.MinValue || result > byte.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for uint8");
            }

            return (byte)result;
        }

        /// <summary>
        /// Parse string to uint8 with explicit base.
        /// </summary>
        public static byte UInt8(string s, int @base)
        {
            long result = ParseIntWithBase(s, @base, "uint8");
            if (result < byte.MinValue || result > byte.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for uint8");
            }
            return (byte)result;
        }

        /// <summary>
        /// Convert byte to uint8 (identity).
        /// </summary>
        public static byte UInt8(byte b)
        {
            return b;
        }

        /// <summary>
        /// Convert sbyte to uint8.
        /// </summary>
        public static byte UInt8(sbyte sb)
        {
            if (sb < 0)
            {
                throw new OverflowError($"Value {sb} is out of range for uint8");
            }
            return (byte)sb;
        }

        /// <summary>
        /// Convert short to uint8.
        /// </summary>
        public static byte UInt8(short s)
        {
            if (s < byte.MinValue || s > byte.MaxValue)
            {
                throw new OverflowError($"Value {s} is out of range for uint8");
            }
            return (byte)s;
        }

        /// <summary>
        /// Convert ushort to uint8.
        /// </summary>
        public static byte UInt8(ushort us)
        {
            if (us > byte.MaxValue)
            {
                throw new OverflowError($"Value {us} is out of range for uint8");
            }
            return (byte)us;
        }

        /// <summary>
        /// Convert uint to uint8.
        /// </summary>
        public static byte UInt8(uint u)
        {
            if (u > byte.MaxValue)
            {
                throw new OverflowError($"Value {u} is out of range for uint8");
            }
            return (byte)u;
        }

        /// <summary>
        /// Convert ulong to uint8.
        /// </summary>
        public static byte UInt8(ulong ul)
        {
            if (ul > byte.MaxValue)
            {
                throw new OverflowError($"Value {ul} is out of range for uint8");
            }
            return (byte)ul;
        }
    }
}
