namespace Sharpy
{
    /// <summary>
    /// Type conversion functions for float (delegates to Double).
    /// Python's float() maps to .NET double. These overloads ensure
    /// that the builtin discovery finds "float" as a valid builtin name.
    /// </summary>
    public static partial class Builtins
    {
        /// <summary>
        /// Convert a bool to float. True becomes 1.0, False becomes 0.0.
        /// </summary>
        /// <param name="b">The bool value</param>
        /// <returns>1.0 for True, 0.0 for False</returns>
        public static double Float(bool b)
        {
            return Double(b);
        }

        /// <summary>
        /// Convert an int to float.
        /// </summary>
        /// <param name="i">The int value</param>
        /// <returns>The value as a double</returns>
        public static double Float(int i)
        {
            return Double(i);
        }

        /// <summary>
        /// Convert a long to float.
        /// </summary>
        /// <param name="l">The long value</param>
        /// <returns>The value as a double</returns>
        public static double Float(long l)
        {
            return Double(l);
        }

        /// <summary>
        /// Convert a float to double (widening).
        /// </summary>
        /// <param name="f">The float value</param>
        /// <returns>The value as a double</returns>
        public static double Float(float f)
        {
            return Double(f);
        }

        /// <summary>
        /// Convert a double to float (identity, since Python float maps to .NET double).
        /// </summary>
        /// <param name="d">The double value</param>
        /// <returns>The same double value</returns>
        public static double Float(double d)
        {
            return Double(d);
        }

        /// <summary>
        /// Convert a decimal to float.
        /// </summary>
        /// <param name="m">The decimal value</param>
        /// <returns>The value as a double</returns>
        public static double Float(decimal m)
        {
            return Double(m);
        }

        /// <summary>
        /// Parse a string to float.
        /// </summary>
        /// <param name="s">The string to parse</param>
        /// <returns>The parsed double value</returns>
        /// <exception cref="ValueError">Thrown when the string cannot be parsed</exception>
        /// <example>
        /// <code>
        /// float("3.14")    # 3.14
        /// float("42")      # 42.0
        /// float("-1.5")    # -1.5
        /// </code>
        /// </example>
        public static double Float(string s)
        {
            return Double(s);
        }

        /// <summary>
        /// Parse Str to float (delegates to string overload).
        /// Enables <c>Func&lt;Str, double&gt;</c> method group conversion.
        /// </summary>
        public static double Float(Str s) => Float((string)s);
    }
}
