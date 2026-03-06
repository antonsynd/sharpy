using System;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible regular expression module.
    /// Wraps .NET's System.Text.RegularExpressions with Python-compatible API.
    /// </summary>
    public static partial class Re
    {
        // Flags — match Python's re module constants
        /// <summary>Perform case-insensitive matching.</summary>
        public const int IGNORECASE = 2;

        /// <summary>Shorthand for IGNORECASE.</summary>
        public const int I = 2;

        /// <summary>Make ^ and $ match at line boundaries.</summary>
        public const int MULTILINE = 8;

        /// <summary>Shorthand for MULTILINE.</summary>
        public const int M = 8;

        /// <summary>Make . match any character including newline.</summary>
        public const int DOTALL = 16;

        /// <summary>Shorthand for DOTALL.</summary>
        public const int S = 16;

        /// <summary>
        /// Compile a pattern into a RePattern object.
        /// </summary>
        public static RePattern Compile(string pattern, int flags = 0)
        {
            return new RePattern(pattern, flags);
        }

        /// <summary>
        /// Scan through string looking for the first location where the pattern produces a match.
        /// </summary>
        public static ReMatch? Search(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Search(s);
        }

        /// <summary>
        /// Try to apply the pattern at the start of the string.
        /// </summary>
        public static ReMatch? Match(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Match(s);
        }

        /// <summary>
        /// Try to apply the pattern to the entire string.
        /// </summary>
        public static ReMatch? Fullmatch(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Fullmatch(s);
        }

        /// <summary>
        /// Return all non-overlapping matches of pattern in string, as a list.
        /// </summary>
        public static List<object?> Findall(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Findall(s);
        }

        /// <summary>
        /// Return an iterator yielding match objects over all non-overlapping matches.
        /// </summary>
        public static List<ReMatch> Finditer(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Finditer(s);
        }

        /// <summary>
        /// Return the string obtained by replacing the leftmost non-overlapping occurrences.
        /// </summary>
        public static string Sub(string pattern, string repl, string s, int count = 0, int flags = 0)
        {
            return Compile(pattern, flags).Sub(repl, s, count);
        }

        /// <summary>
        /// Split string by the occurrences of the pattern.
        /// </summary>
        public static List<string> Split(string pattern, string s, int maxsplit = 0, int flags = 0)
        {
            return Compile(pattern, flags).Split(s, maxsplit);
        }

        /// <summary>
        /// Escape special characters in pattern.
        /// </summary>
        public static string Escape(string pattern)
        {
            return Regex.Escape(pattern);
        }
    }
}
