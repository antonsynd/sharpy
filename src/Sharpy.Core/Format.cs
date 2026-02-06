using System.Globalization;
using System;
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
        public static string Format(object? value, string formatSpec = "")
        {
            if (value is null)
            {
                return "None";
            }

            if (string.IsNullOrEmpty(formatSpec))
            {
                return value.ToString() ?? "None";
            }

            // For numeric types, use standard .NET formatting
            if (value is IFormattable formattable)
            {
                return formattable.ToString(formatSpec, System.Globalization.CultureInfo.InvariantCulture);
            }

            // For other types, just return ToString()
            return value.ToString() ?? "None";
        }

    }
}
