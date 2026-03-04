using System;
using System.Globalization;

namespace Sharpy
{
    /// <summary>
    /// Static helper for safe int parsing. Called by int.parse() in Sharpy.
    /// Returns Result[int, ValueError] instead of throwing.
    /// </summary>
    public static class IntParse
    {
        /// <summary>
        /// Parse a string to int, returning Ok(value) on success or Err(ValueError) on failure.
        /// </summary>
        public static Result<int, ValueError> Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return Result<int, ValueError>.Err(
                    new ValueError($"invalid literal for int() with base 10: '{s}'"));
            }

            s = s.Trim();

            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                return Result<int, ValueError>.Ok(result);
            }

            return Result<int, ValueError>.Err(
                new ValueError($"invalid literal for int() with base 10: '{s}'"));
        }
    }
}
