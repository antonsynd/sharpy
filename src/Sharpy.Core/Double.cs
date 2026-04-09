using System;
using System.Globalization;
namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for double (Python's float maps to .NET double).
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert bool to double. True becomes 1.0, False becomes 0.0.
        /// </summary>
        /// <param name="b">The bool value</param>
        /// <returns>1.0 for True, 0.0 for False</returns>
        public static double Double(bool b)
        {
            return b ? 1.0 : 0.0;
        }

        /// <summary>
        /// Convert int to double
        /// </summary>
        public static double Double(int i)
        {
            return i;
        }

        /// <summary>
        /// Convert long to double
        /// </summary>
        public static double Double(long l)
        {
            return l;
        }

        /// <summary>
        /// Convert float to double
        /// </summary>
        public static double Double(float f)
        {
            return f;
        }

        /// <summary>
        /// Convert double to double (identity)
        /// </summary>
        public static double Double(double d)
        {
            return d;
        }

        /// <summary>
        /// Convert decimal to double
        /// </summary>
        public static double Double(decimal m)
        {
            return (double)m;
        }

        /// <summary>
        /// Parse string to double
        /// </summary>
        public static double Double(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                throw new ValueError($"could not convert string to float: '{s}'");
            }

            s = s.Trim();

            if (!double.TryParse(s, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double result))
            {
                throw new ValueError($"could not convert string to float: '{s}'");
            }

            return result;
        }

        /// <summary>
        /// Convert byte to double
        /// </summary>
        public static double Double(byte b)
        {
            return b;
        }

        /// <summary>
        /// Convert sbyte to double
        /// </summary>
        public static double Double(sbyte sb)
        {
            return sb;
        }

        /// <summary>
        /// Convert short to double
        /// </summary>
        public static double Double(short s)
        {
            return s;
        }

        /// <summary>
        /// Convert ushort to double
        /// </summary>
        public static double Double(ushort us)
        {
            return us;
        }

        /// <summary>
        /// Convert uint to double
        /// </summary>
        public static double Double(uint u)
        {
            return u;
        }

        /// <summary>
        /// Convert ulong to double
        /// </summary>
        public static double Double(ulong ul)
        {
            return ul;
        }
    }
}
