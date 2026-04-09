using System;
using System.Globalization;
namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for long.
    /// Python's int() converts various types to integer; Sharpy's long() converts to System.Int64.
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to long. True becomes 1, False becomes 0.
        /// </summary>
        public static long Long(bool b)
        {
            return b ? 1L : 0L;
        }

        /// <summary>
        /// Convert int to long (widening)
        /// </summary>
        public static long Long(int i)
        {
            return i;
        }

        /// <summary>
        /// Convert long to long (identity)
        /// </summary>
        public static long Long(long l)
        {
            return l;
        }

        /// <summary>
        /// Convert float to long (truncates)
        /// </summary>
        public static long Long(float f)
        {
            if (float.IsNaN(f))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (float.IsPositiveInfinity(f) || float.IsNegativeInfinity(f))
            {
                throw new OverflowException($"Value {f} is out of range for long");
            }
            if (f < long.MinValue || f > long.MaxValue)
            {
                throw new OverflowException($"Value {f} is out of range for long");
            }
            return (long)f;
        }

        /// <summary>
        /// Convert double to long (truncates)
        /// </summary>
        public static long Long(double d)
        {
            if (double.IsNaN(d))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (double.IsInfinity(d) || d < long.MinValue || d > long.MaxValue)
            {
                throw new OverflowException($"Value {d} is out of range for long");
            }
            return (long)d;
        }

        /// <summary>
        /// Convert decimal to long (truncates)
        /// </summary>
        public static long Long(decimal m)
        {
            if (m < long.MinValue || m > long.MaxValue)
            {
                throw new OverflowException($"Value {m} is out of range for long");
            }
            return (long)m;
        }

        /// <summary>
        /// Parse string to long
        /// </summary>
        public static long Long(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"invalid literal for long() with base 10: '{s}'");
            }

            s = s.Trim();

            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                throw new ValueError($"invalid literal for long() with base 10: '{s}'");
            }

            return result;
        }

        /// <summary>
        /// Convert byte to long
        /// </summary>
        public static long Long(byte b)
        {
            return b;
        }

        /// <summary>
        /// Convert sbyte to long
        /// </summary>
        public static long Long(sbyte sb)
        {
            return sb;
        }

        /// <summary>
        /// Convert short to long
        /// </summary>
        public static long Long(short s)
        {
            return s;
        }

        /// <summary>
        /// Convert ushort to long
        /// </summary>
        public static long Long(ushort us)
        {
            return us;
        }

        /// <summary>
        /// Convert uint to long
        /// </summary>
        public static long Long(uint u)
        {
            return u;
        }

        /// <summary>
        /// Convert ulong to long
        /// </summary>
        public static long Long(ulong ul)
        {
            if (ul > (ulong)long.MaxValue)
            {
                throw new OverflowException($"Value {ul} is out of range for long");
            }
            return (long)ul;
        }
    }
}
