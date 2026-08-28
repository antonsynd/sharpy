using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for uint32 (uint).
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to uint32. True becomes 1, False becomes 0.
        /// </summary>
        public static uint UInt32(bool b)
        {
            return b ? 1u : 0u;
        }

        /// <summary>
        /// Convert int to uint32.
        /// </summary>
        public static uint UInt32(int i)
        {
            if (i < 0)
            {
                throw new OverflowError($"Value {i} is out of range for uint32");
            }
            return (uint)i;
        }

        /// <summary>
        /// Convert long to uint32.
        /// </summary>
        public static uint UInt32(long l)
        {
            if (l < uint.MinValue || l > uint.MaxValue)
            {
                throw new OverflowError($"Value {l} is out of range for uint32");
            }
            return (uint)l;
        }

        /// <summary>
        /// Convert float to uint32 (truncates toward zero).
        /// </summary>
        public static uint UInt32(float f)
        {
            if (float.IsNaN(f))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (float.IsPositiveInfinity(f) || float.IsNegativeInfinity(f))
            {
                throw new OverflowError($"Value {f} is out of range for uint32");
            }
            if (f < uint.MinValue || f > uint.MaxValue)
            {
                throw new OverflowError($"Value {f} is out of range for uint32");
            }
            return (uint)f;
        }

        /// <summary>
        /// Convert double to uint32 (truncates toward zero).
        /// </summary>
        public static uint UInt32(double d)
        {
            if (double.IsNaN(d))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (double.IsInfinity(d) || d < uint.MinValue || d > uint.MaxValue)
            {
                throw new OverflowError($"Value {d} is out of range for uint32");
            }
            return (uint)d;
        }

        /// <summary>
        /// Convert decimal to uint32 (truncates toward zero).
        /// </summary>
        public static uint UInt32(decimal m)
        {
            if (m < uint.MinValue || m > uint.MaxValue)
            {
                throw new OverflowError($"Value {m} is out of range for uint32");
            }
            return (uint)m;
        }

        /// <summary>
        /// Parse string to uint32.
        /// </summary>
        public static uint UInt32(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"invalid literal for uint32() with base 10: '{s}'");
            }

            s = s.Trim();

            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                throw new ValueError($"invalid literal for uint32() with base 10: '{s}'");
            }

            if (result < uint.MinValue || result > uint.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for uint32");
            }

            return (uint)result;
        }

        /// <summary>
        /// Parse string to uint32 with explicit base.
        /// </summary>
        public static uint UInt32(string s, int @base)
        {
            long result = ParseIntWithBase(s, @base, "uint32");
            if (result < uint.MinValue || result > uint.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for uint32");
            }
            return (uint)result;
        }

        /// <summary>
        /// Convert byte to uint32 (widening).
        /// </summary>
        public static uint UInt32(byte b)
        {
            return b;
        }

        /// <summary>
        /// Convert sbyte to uint32.
        /// </summary>
        public static uint UInt32(sbyte sb)
        {
            if (sb < 0)
            {
                throw new OverflowError($"Value {sb} is out of range for uint32");
            }
            return (uint)sb;
        }

        /// <summary>
        /// Convert short to uint32.
        /// </summary>
        public static uint UInt32(short s)
        {
            if (s < 0)
            {
                throw new OverflowError($"Value {s} is out of range for uint32");
            }
            return (uint)s;
        }

        /// <summary>
        /// Convert ushort to uint32 (widening).
        /// </summary>
        public static uint UInt32(ushort us)
        {
            return us;
        }

        /// <summary>
        /// Convert uint to uint32 (identity).
        /// </summary>
        public static uint UInt32(uint u)
        {
            return u;
        }

        /// <summary>
        /// Convert ulong to uint32.
        /// </summary>
        public static uint UInt32(ulong ul)
        {
            if (ul > uint.MaxValue)
            {
                throw new OverflowError($"Value {ul} is out of range for uint32");
            }
            return (uint)ul;
        }
    }
}
