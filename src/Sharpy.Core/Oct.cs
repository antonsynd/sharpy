using System;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return an octal string prefixed with "0o".
        /// </summary>
        /// <param name="x">The integer to convert</param>
        /// <returns>An octal string representation</returns>
        /// <example>
        /// <code>
        /// oct(8)      # "0o10"
        /// oct(-8)     # "-0o10"
        /// oct(0)      # "0o0"
        /// </code>
        /// </example>
        public static string Oct(int x)
        {
            if (x >= 0)
            {
                return "0o" + Convert.ToString(x, 8);
            }

            return "-0o" + Convert.ToString(-(long)x, 8);
        }

        /// <summary>
        /// Return an octal string prefixed with "0o" for long integers.
        /// </summary>
        /// <param name="x">The long integer to convert</param>
        /// <returns>An octal string representation</returns>
        public static string Oct(long x)
        {
            if (x >= 0)
            {
                return "0o" + Convert.ToString(x, 8);
            }

            if (x == long.MinValue)
            {
                return "-0o1000000000000000000000";
            }

            return "-0o" + Convert.ToString(-x, 8);
        }
    }
}
