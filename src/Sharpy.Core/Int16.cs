using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for int16 (short).
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to int16. True becomes 1, False becomes 0.
        /// </summary>
        public static short Int16(bool b)
        {
            return b ? (short)1 : (short)0;
        }

        /// <summary>
        /// Convert int to int16.
        /// </summary>
        public static short Int16(int i)
        {
            if (i < short.MinValue || i > short.MaxValue)
            {
                throw new OverflowError($"Value {i} is out of range for int16");
            }
            return (short)i;
        }

        /// <summary>
        /// Convert long to int16.
        /// </summary>
        public static short Int16(long l)
        {
            if (l < short.MinValue || l > short.MaxValue)
            {
                throw new OverflowError($"Value {l} is out of range for int16");
            }
            return (short)l;
        }

        /// <summary>
        /// Convert float to int16 (truncates toward zero).
        /// </summary>
        public static short Int16(float f)
        {
            if (float.IsNaN(f))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (float.IsPositiveInfinity(f) || float.IsNegativeInfinity(f))
            {
                throw new OverflowError($"Value {f} is out of range for int16");
            }
            if (f < short.MinValue || f > short.MaxValue)
            {
                throw new OverflowError($"Value {f} is out of range for int16");
            }
            return (short)f;
        }

        /// <summary>
        /// Convert double to int16 (truncates toward zero).
        /// </summary>
        public static short Int16(double d)
        {
            if (double.IsNaN(d))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (double.IsInfinity(d) || d < short.MinValue || d > short.MaxValue)
            {
                throw new OverflowError($"Value {d} is out of range for int16");
            }
            return (short)d;
        }

        /// <summary>
        /// Convert decimal to int16 (truncates toward zero).
        /// </summary>
        public static short Int16(decimal m)
        {
            if (m < short.MinValue || m > short.MaxValue)
            {
                throw new OverflowError($"Value {m} is out of range for int16");
            }
            return (short)m;
        }

        /// <summary>
        /// Parse string to int16.
        /// </summary>
        public static short Int16(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"invalid literal for int16() with base 10: '{s}'");
            }

            s = s.Trim();

            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                throw new ValueError($"invalid literal for int16() with base 10: '{s}'");
            }

            if (result < short.MinValue || result > short.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for int16");
            }

            return (short)result;
        }

        /// <summary>
        /// Parse string to int16 with explicit base.
        /// </summary>
        public static short Int16(string s, int @base)
        {
            long result = ParseIntWithBase(s, @base, "int16");
            if (result < short.MinValue || result > short.MaxValue)
            {
                throw new OverflowError($"Value {result} is out of range for int16");
            }
            return (short)result;
        }

        /// <summary>
        /// Convert byte to int16 (widening).
        /// </summary>
        public static short Int16(byte b)
        {
            return b;
        }

        /// <summary>
        /// Convert sbyte to int16 (widening).
        /// </summary>
        public static short Int16(sbyte sb)
        {
            return sb;
        }

        /// <summary>
        /// Convert short to int16 (identity).
        /// </summary>
        public static short Int16(short s)
        {
            return s;
        }

        /// <summary>
        /// Convert ushort to int16.
        /// </summary>
        public static short Int16(ushort us)
        {
            if (us > short.MaxValue)
            {
                throw new OverflowError($"Value {us} is out of range for int16");
            }
            return (short)us;
        }

        /// <summary>
        /// Convert uint to int16.
        /// </summary>
        public static short Int16(uint u)
        {
            if (u > (uint)short.MaxValue)
            {
                throw new OverflowError($"Value {u} is out of range for int16");
            }
            return (short)u;
        }

        /// <summary>
        /// Convert ulong to int16.
        /// </summary>
        public static short Int16(ulong ul)
        {
            if (ul > (ulong)short.MaxValue)
            {
                throw new OverflowError($"Value {ul} is out of range for int16");
            }
            return (short)ul;
        }
    }
}
