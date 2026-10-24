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
            return NumericText.TryToInt32(s);
        }
    }
}
