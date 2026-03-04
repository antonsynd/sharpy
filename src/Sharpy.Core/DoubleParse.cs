using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Static helper for safe float/double parsing. Called by float.parse() in Sharpy.
    /// Returns Result[float, ValueError] instead of throwing.
    /// </summary>
    public static class DoubleParse
    {
        /// <summary>
        /// Parse a string to double, returning Ok(value) on success or Err(ValueError) on failure.
        /// </summary>
        public static Result<double, ValueError> Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return Result<double, ValueError>.Err(
                    new ValueError($"could not convert string to float: '{s}'"));
            }

            s = s.Trim();

            if (double.TryParse(s, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double result))
            {
                return Result<double, ValueError>.Ok(result);
            }

            return Result<double, ValueError>.Err(
                new ValueError($"could not convert string to float: '{s}'"));
        }
    }
}
