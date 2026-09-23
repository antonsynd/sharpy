namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Convert a value to a "formatted" representation, as controlled by format_spec.
        /// The interpretation of format_spec will depend on the type of the value argument.
        /// </summary>
        /// <param name="value">The value to format</param>
        /// <param name="formatSpec">The format specification string (default is empty string)</param>
        /// <returns>The formatted string representation</returns>
        /// <example>
        /// <code>
        /// format(42, "d")        # "42"
        /// format(3.14, ".1f")    # "3.1"
        /// format(255, "x")       # "ff"
        /// </code>
        /// </example>
        public static string Format(object? value, [FormatSpec("value")] string formatSpec = "")
        {
            return PyFormat.Apply(value, formatSpec);
        }

    }
}
