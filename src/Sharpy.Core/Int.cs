using System;
using System.Globalization;
namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for int.
    /// Python's int() converts various types to integer, truncating floats.
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to int. True becomes 1, False becomes 0.
        /// </summary>
        /// <param name="b">The bool value</param>
        /// <returns>1 for True, 0 for False</returns>
        /// <example>
        /// <code>
        /// int(True)      # 1
        /// int(False)     # 0
        /// int(3.9)       # 3 (truncates)
        /// int("42")      # 42
        /// </code>
        /// </example>
        public static int Int(bool b)
        {
            return b ? 1 : 0;
        }

        /// <summary>
        /// Convert int to int (identity)
        /// </summary>
        public static int Int(int i)
        {
            return i;
        }

        /// <summary>
        /// Convert long to int
        /// </summary>
        public static int Int(long l)
        {
            if (l < int.MinValue || l > int.MaxValue)
            {
                throw new OverflowException($"Value {l} is out of range for int");
            }
            return (int)l;
        }

        /// <summary>
        /// Convert float to int (truncates)
        /// </summary>
        public static int Int(float f)
        {
            if (float.IsNaN(f))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (float.IsPositiveInfinity(f) || float.IsNegativeInfinity(f))
            {
                throw new OverflowException($"Value {f} is out of range for int");
            }
            if (f < int.MinValue || f > int.MaxValue)
            {
                throw new OverflowException($"Value {f} is out of range for int");
            }
            return (int)f;
        }

        /// <summary>
        /// Convert double to int (truncates)
        /// </summary>
        public static int Int(double d)
        {
            if (double.IsNaN(d))
            {
                throw new ValueError("cannot convert float NaN to int");
            }
            if (double.IsInfinity(d) || d < int.MinValue || d > int.MaxValue)
            {
                throw new OverflowException($"Value {d} is out of range for int");
            }
            return (int)d;
        }

        /// <summary>
        /// Convert decimal to int (truncates)
        /// </summary>
        public static int Int(decimal m)
        {
            if (m < int.MinValue || m > int.MaxValue)
            {
                throw new OverflowException($"Value {m} is out of range for int");
            }
            return (int)m;
        }

        /// <summary>
        /// Parse string to int
        /// </summary>
        public static int Int(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"invalid literal for int() with base 10: '{s}'");
            }

            s = s.Trim();

            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                throw new ValueError($"invalid literal for int() with base 10: '{s}'");
            }

            return result;
        }

        /// <summary>
        /// Convert byte to int
        /// </summary>
        public static int Int(byte b)
        {
            return b;
        }

        /// <summary>
        /// Convert sbyte to int
        /// </summary>
        public static int Int(sbyte sb)
        {
            return sb;
        }

        /// <summary>
        /// Convert short to int
        /// </summary>
        public static int Int(short s)
        {
            return s;
        }

        /// <summary>
        /// Convert ushort to int
        /// </summary>
        public static int Int(ushort us)
        {
            return us;
        }

        /// <summary>
        /// Convert uint to int
        /// </summary>
        public static int Int(uint u)
        {
            if (u > int.MaxValue)
            {
                throw new OverflowException($"Value {u} is out of range for int");
            }
            return (int)u;
        }

        /// <summary>
        /// Convert ulong to int
        /// </summary>
        public static int Int(ulong ul)
        {
            if (ul > (ulong)int.MaxValue)
            {
                throw new OverflowException($"Value {ul} is out of range for int");
            }
            return (int)ul;
        }
    }
}
