using System;

namespace Sharpy
{
    /// <summary>
    /// Containment extension methods for string.
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// Return true if <paramref name="substring"/> is found within this string.
        /// Used for <c>"x" in s</c> codegen.
        /// </summary>
        public static bool Contains(this string s, string substring)
        {
            return s.IndexOf(substring, StringComparison.Ordinal) >= 0;
        }
    }
}
