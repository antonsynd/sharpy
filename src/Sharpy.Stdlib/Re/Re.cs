using System;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Python-compatible regular expression module.
    /// Wraps .NET's System.Text.RegularExpressions with Python-compatible API.
    /// </summary>
    public static partial class ReModule
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

        /// <summary>Allow verbose regex with comments and whitespace.</summary>
        public const int VERBOSE = 64;

        /// <summary>Shorthand for VERBOSE.</summary>
        public const int X = 64;

        /// <summary>Make \w, \b, etc. match Unicode (default on .NET, accepted for compatibility).</summary>
        public const int UNICODE = 32;

        /// <summary>Shorthand for UNICODE.</summary>
        public const int U = 32;

        /// <summary>Make \w, \b, etc. match ASCII only (no-op on .NET, accepted for compatibility).</summary>
        public const int ASCII = 256;

        /// <summary>Shorthand for ASCII.</summary>
        public const int A = 256;

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
        /// <param name="pattern">The regular expression pattern.</param>
        /// <param name="s">The string to search.</param>
        /// <param name="flags">Optional regex flags (e.g., re.IGNORECASE).</param>
        /// <returns>A match object, or <c>null</c> if no match is found.</returns>
        /// <example>
        /// <code>
        /// m = re.search(r"\d+", "abc123")
        /// m.group()    # "123"
        /// </code>
        /// </example>
        public static ReMatch? Search(string pattern, string s, int flags = 0)
        {
            return Compile(pattern, flags).Search(s);
        }

        /// <summary>
        /// Try to apply the pattern at the start of the string.
        /// </summary>
        /// <param name="pattern">The regular expression pattern.</param>
        /// <param name="s">The string to match against.</param>
        /// <param name="flags">Optional regex flags.</param>
        /// <returns>A match object, or <c>null</c> if the pattern does not match at the start.</returns>
        /// <example>
        /// <code>
        /// m = re.match(r"\d+", "123abc")
        /// m.group()    # "123"
        /// </code>
        /// </example>
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
        /// <param name="pattern">The regular expression pattern.</param>
        /// <param name="repl">The replacement string.</param>
        /// <param name="s">The string to search and replace in.</param>
        /// <param name="count">Maximum number of replacements (0 = all).</param>
        /// <param name="flags">Optional regex flags.</param>
        /// <returns>The modified string.</returns>
        /// <example>
        /// <code>
        /// re.sub(r"\d+", "N", "abc123def456")    # "abcNdefN"
        /// </code>
        /// </example>
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
        /// Like sub(), but returns a tuple (new_string, number_of_subs_made).
        /// </summary>
        public static (string, int) Subn(string pattern, string repl, string s, int count = 0, int flags = 0)
        {
            return Compile(pattern, flags).Subn(repl, s, count);
        }

        /// <summary>
        /// Return the string obtained by replacing occurrences using a callable.
        /// The callable receives the match object and returns the replacement string.
        /// </summary>
        public static string Sub(string pattern, Func<ReMatch, string> repl, string s, int count = 0, int flags = 0)
        {
            return Compile(pattern, flags).Sub(repl, s, count);
        }

        /// <summary>
        /// Like sub() with callable, but returns a tuple (new_string, number_of_subs_made).
        /// </summary>
        public static (string, int) Subn(string pattern, Func<ReMatch, string> repl, string s, int count = 0, int flags = 0)
        {
            return Compile(pattern, flags).Subn(repl, s, count);
        }

        /// <summary>
        /// Clear the regular expression cache. No-op on .NET (no internal cache).
        /// </summary>
        public static void Purge()
        {
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
