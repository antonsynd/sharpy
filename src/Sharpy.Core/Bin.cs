using System;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a binary string prefixed with "0b".
        /// </summary>
        /// <param name="x">The integer to convert</param>
        /// <returns>A binary string representation</returns>
        /// <example>
        /// <code>
        /// bin(10)     # "0b1010"
        /// bin(-10)    # "-0b1010"
        /// bin(0)      # "0b0"
        /// </code>
        /// </example>
        public static string Bin(int x)
        {
            if (x >= 0)
            {
                return "0b" + Convert.ToString(x, 2);
            }

            return "-0b" + Convert.ToString(-(long)x, 2);
        }

        /// <summary>
        /// Return a binary string prefixed with "0b" for long integers.
        /// </summary>
        /// <param name="x">The long integer to convert</param>
        /// <returns>A binary string representation</returns>
        public static string Bin(long x)
        {
            if (x >= 0)
            {
                return "0b" + Convert.ToString(x, 2);
            }

            if (x == long.MinValue)
            {
                return "-0b1000000000000000000000000000000000000000000000000000000000000000";
            }

            return "-0b" + Convert.ToString(-x, 2);
        }
    }
}
