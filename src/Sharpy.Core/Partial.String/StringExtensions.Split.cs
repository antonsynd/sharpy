using System;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Split, Rsplit, Splitlines, Partition, and Rpartition methods for StringExtensions.
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// Return a list of the lines in the string, breaking at line
        /// boundaries. Line breaks are not included in the resulting list.
        /// Python: <c>str.splitlines()</c>
        /// </summary>
        /// <remarks>
        /// Recognizes all Python line boundaries: \n, \r\n, \r, \v (0x0B),
        /// \f (0x0C), \x1C, \x1D, \x1E, \x85 (NEL), \u2028 (LS), \u2029 (PS).
        /// </remarks>
        /// <example>
        /// <code>
        /// "a\nb\nc".splitlines()    # ["a", "b", "c"]
        /// </code>
        /// </example>
        public static List<string> Splitlines(this string s)
        {
            return Splitlines(s, false);
        }

        /// <summary>
        /// Return a list of the lines in the string, breaking at line
        /// boundaries. When <paramref name="keepends"/> is <c>true</c>, line
        /// break characters are included in the resulting strings.
        /// Python: <c>str.splitlines(keepends)</c>
        /// </summary>
        /// <remarks>
        /// Recognizes all Python line boundaries: \n, \r\n, \r, \v (0x0B),
        /// \f (0x0C), \x1C, \x1D, \x1E, \x85 (NEL), \u2028 (LS), \u2029 (PS).
        /// </remarks>
        public static List<string> Splitlines(this string s, bool keepends)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(s))
            {
                return result;
            }

            var currentLine = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (c == '\r')
                {
                    // Handle \r\n and \r
                    if (i + 1 < s.Length && s[i + 1] == '\n')
                    {
                        if (keepends)
                        {
                            currentLine.Append("\r\n");
                        }
                        i++; // Skip the \n
                    }
                    else
                    {
                        if (keepends)
                        {
                            currentLine.Append(c);
                        }
                    }
                    result.Add(currentLine.ToString());
                    currentLine.Clear();
                }
                else if (c == '\n' || c == '\x0B' || c == '\x0C'
                    || c == '\x1C' || c == '\x1D' || c == '\x1E'
                    || c == '\x85' || c == '\u2028' || c == '\u2029')
                {
                    if (keepends)
                    {
                        currentLine.Append(c);
                    }
                    result.Add(currentLine.ToString());
                    currentLine.Clear();
                }
                else
                {
                    currentLine.Append(c);
                }
            }

            // Add remaining content if any
            if (currentLine.Length > 0)
            {
                result.Add(currentLine.ToString());
            }

            return result;
        }

        // ----------------------------------------------------------------
        // Split / Rsplit
        // ----------------------------------------------------------------

        /// <summary>
        /// Split the string on whitespace. Consecutive whitespace is collapsed,
        /// and leading/trailing whitespace is stripped.
        /// Python: <c>str.split()</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "a b  c".split()    # ["a", "b", "c"]
        /// </code>
        /// </example>
        public static List<string> Split(this string s)
        {
            var result = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                // Skip whitespace
                while (i < s.Length && char.IsWhiteSpace(s[i]))
                {
                    i++;
                }
                if (i >= s.Length)
                {
                    break;
                }
                // Collect non-whitespace
                int start = i;
                while (i < s.Length && !char.IsWhiteSpace(s[i]))
                {
                    i++;
                }
                result.Add(s.Substring(start, i - start));
            }
            return result;
        }

        /// <summary>
        /// Split the string on a separator string.
        /// Python: <c>str.split(sep)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "a,b,c".split(",")    # ["a", "b", "c"]
        /// </code>
        /// </example>
        public static List<string> Split(this string s, string sep)
        {
            return Split(s, sep, -1);
        }

        /// <summary>
        /// Split the string on a separator string, performing at most
        /// <paramref name="maxsplit"/> splits (from the left).
        /// Python: <c>str.split(sep, maxsplit)</c>
        /// </summary>
        /// <exception cref="ValueError">Thrown if <paramref name="sep"/> is empty.</exception>
        public static List<string> Split(this string s, string sep, int maxsplit)
        {
            if (sep == null)
            {
                throw new ArgumentNullException(nameof(sep));
            }
            if (sep.Length == 0)
            {
                throw new ValueError("empty separator");
            }

            var result = new List<string>();
            int start = 0;
            int splits = 0;

            while (start <= s.Length)
            {
                if (maxsplit >= 0 && splits >= maxsplit)
                {
                    break;
                }
                int index = s.IndexOf(sep, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }
                result.Add(s.Substring(start, index - start));
                start = index + sep.Length;
                splits++;
            }
            result.Add(s.Substring(start));
            return result;
        }

        /// <summary>
        /// Split the string on whitespace from the right. Consecutive whitespace
        /// is collapsed, and leading/trailing whitespace is stripped.
        /// Python: <c>str.rsplit()</c>
        /// </summary>
        public static List<string> Rsplit(this string s)
        {
            // rsplit() with no args behaves the same as split() with no args
            return Split(s);
        }

        /// <summary>
        /// Split the string on a separator string from the right.
        /// Python: <c>str.rsplit(sep)</c>
        /// </summary>
        public static List<string> Rsplit(this string s, string sep)
        {
            return Rsplit(s, sep, -1);
        }

        /// <summary>
        /// Split the string on a separator string from the right, performing at
        /// most <paramref name="maxsplit"/> splits.
        /// Python: <c>str.rsplit(sep, maxsplit)</c>
        /// </summary>
        /// <exception cref="ValueError">Thrown if <paramref name="sep"/> is empty.</exception>
        public static List<string> Rsplit(this string s, string sep, int maxsplit)
        {
            if (sep == null)
            {
                throw new ArgumentNullException(nameof(sep));
            }
            if (sep.Length == 0)
            {
                throw new ValueError("empty separator");
            }

            if (maxsplit < 0)
            {
                // No limit — same as split
                return Split(s, sep, -1);
            }

            // Split from the right: collect parts in reverse
            var parts = new System.Collections.Generic.List<string>();
            int end = s.Length;
            int splits = 0;

            while (end > 0 && splits < maxsplit)
            {
                int index = s.LastIndexOf(sep, end - 1, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }
                parts.Add(s.Substring(index + sep.Length, end - index - sep.Length));
                end = index;
                splits++;
            }
            parts.Add(s.Substring(0, end));
            parts.Reverse();
            return new List<string>(parts);
        }

        // ----------------------------------------------------------------
        // Partition / Rpartition
        // ----------------------------------------------------------------

        /// <summary>
        /// Split the string at the first occurrence of <paramref name="sep"/>,
        /// and return a 3-tuple containing the part before the separator, the
        /// separator itself, and the part after the separator. If the separator
        /// is not found, return a 3-tuple containing the string itself, followed
        /// by two empty strings.
        /// Python: <c>str.partition(sep)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "a.b.c".partition(".")    # ("a", ".", "b.c")
        /// </code>
        /// </example>
        /// <exception cref="ValueError">Thrown if <paramref name="sep"/> is empty.</exception>
        public static (string, string, string) Partition(this string s, string sep)
        {
            if (sep == null)
            {
                throw new ArgumentNullException(nameof(sep));
            }
            if (sep.Length == 0)
            {
                throw new ValueError("empty separator");
            }
            int index = s.IndexOf(sep, StringComparison.Ordinal);
            if (index < 0)
            {
                return (s, "", "");
            }
            return (s.Substring(0, index), sep, s.Substring(index + sep.Length));
        }

        /// <summary>
        /// Split the string at the last occurrence of <paramref name="sep"/>,
        /// and return a 3-tuple containing the part before the separator, the
        /// separator itself, and the part after the separator. If the separator
        /// is not found, return a 3-tuple containing two empty strings, followed
        /// by the string itself.
        /// Python: <c>str.rpartition(sep)</c>
        /// </summary>
        /// <example>
        /// <code>
        /// "a.b.c".rpartition(".")    # ("a.b", ".", "c")
        /// </code>
        /// </example>
        /// <exception cref="ValueError">Thrown if <paramref name="sep"/> is empty.</exception>
        public static (string, string, string) Rpartition(this string s, string sep)
        {
            if (sep == null)
            {
                throw new ArgumentNullException(nameof(sep));
            }
            if (sep.Length == 0)
            {
                throw new ValueError("empty separator");
            }
            int index = s.LastIndexOf(sep, StringComparison.Ordinal);
            if (index < 0)
            {
                return ("", "", s);
            }
            return (s.Substring(0, index), sep, s.Substring(index + sep.Length));
        }
    }
}
