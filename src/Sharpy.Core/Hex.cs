using System;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a lowercase hexadecimal string prefixed with "0x".
        /// </summary>
        /// <param name="x">The integer to convert</param>
        /// <returns>A hexadecimal string representation</returns>
        /// <example>
        /// <code>
        /// hex(255)    # "0xff"
        /// hex(-42)    # "-0x2a"
        /// hex(0)      # "0x0"
        /// </code>
        /// </example>
        public static string Hex(int x)
        {
            if (x >= 0)
            {
                return "0x" + Convert.ToString(x, 16);
            }

            return "-0x" + Convert.ToString(-(long)x, 16);
        }

        /// <summary>
        /// Return a lowercase hexadecimal string prefixed with "0x" for long integers.
        /// </summary>
        /// <param name="x">The long integer to convert</param>
        /// <returns>A hexadecimal string representation</returns>
        public static string Hex(long x)
        {
            if (x >= 0)
            {
                return "0x" + Convert.ToString(x, 16);
            }

            if (x == long.MinValue)
            {
                return "-0x8000000000000000";
            }

            return "-0x" + Convert.ToString(-x, 16);
        }
    }
}
