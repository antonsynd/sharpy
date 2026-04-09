using System;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Split, join, partition extension methods for string.
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// Return a string which is the concatenation of the strings in
        /// <paramref name="iterable"/>. The separator between elements is this
        /// string.
        /// Python: <c>str.join(iterable)</c> — called as <c>separator.join(list)</c>.
        /// </summary>
        public static string Join(this string s, IEnumerable<string> iterable)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var item in iterable)
            {
                parts.Add(item);
            }
            return string.Join(s, parts);
        }

        /// <summary>
        /// Split on whitespace. Consecutive whitespace is collapsed,
        /// leading/trailing whitespace is stripped.
        /// Python: <c>str.split()</c>
        /// </summary>
        public static List<string> Split(this string s)
        {
            var result = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i]))
                {
                    i++;
                }
                if (i >= s.Length)
                {
                    break;
                }
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
        /// Split on a separator string.
        /// Python: <c>str.split(sep)</c>
        /// </summary>
        public static List<string> Split(this string s, string sep)
        {
            return Split(s, sep, -1);
        }

        /// <summary>
        /// Split on a separator string, performing at most
        /// <paramref name="maxsplit"/> splits (from the left).
        /// Python: <c>str.split(sep, maxsplit)</c>
        /// </summary>
        public static List<string> Split(this string s, string sep, int maxsplit)
        {
            if (sep == null)
            {
                throw TypeError.ArgNone("split", "sep");
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
        /// Split on whitespace from the right.
        /// Python: <c>str.rsplit()</c>
        /// </summary>
        public static List<string> Rsplit(this string s)
        {
            return Split(s);
        }

        /// <summary>
        /// Split on a separator string from the right.
        /// Python: <c>str.rsplit(sep)</c>
        /// </summary>
        public static List<string> Rsplit(this string s, string sep)
        {
            return Rsplit(s, sep, -1);
        }

        /// <summary>
        /// Split on a separator string from the right, performing at most
        /// <paramref name="maxsplit"/> splits.
        /// Python: <c>str.rsplit(sep, maxsplit)</c>
        /// </summary>
        public static List<string> Rsplit(this string s, string sep, int maxsplit)
        {
            if (sep == null)
            {
                throw TypeError.ArgNone("rsplit", "sep");
            }
            if (sep.Length == 0)
            {
                throw new ValueError("empty separator");
            }

            if (maxsplit < 0)
            {
                return Split(s, sep, -1);
            }

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

        /// <summary>
        /// Return a list of the lines in the string, breaking at line boundaries.
        /// Python: <c>str.splitlines()</c>
        /// </summary>
        public static List<string> Splitlines(this string s)
        {
            return Splitlines(s, false);
        }

        /// <summary>
        /// Return a list of lines, optionally keeping line break characters.
        /// Python: <c>str.splitlines(keepends)</c>
        /// </summary>
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
                    if (i + 1 < s.Length && s[i + 1] == '\n')
                    {
                        if (keepends)
                        {
                            currentLine.Append("\r\n");
                        }
                        i++;
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

            if (currentLine.Length > 0)
            {
                result.Add(currentLine.ToString());
            }

            return result;
        }

        /// <summary>
        /// Split at the first occurrence of <paramref name="sep"/>, returning a
        /// 3-tuple.
        /// Python: <c>str.partition(sep)</c>
        /// </summary>
        public static (string, string, string) Partition(this string s, string sep)
        {
            if (sep == null)
            {
                throw TypeError.ArgNone("partition", "sep");
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
            return (
                s.Substring(0, index),
                sep,
                s.Substring(index + sep.Length)
            );
        }

        /// <summary>
        /// Split at the last occurrence of <paramref name="sep"/>, returning a
        /// 3-tuple.
        /// Python: <c>str.rpartition(sep)</c>
        /// </summary>
        public static (string, string, string) Rpartition(this string s, string sep)
        {
            if (sep == null)
            {
                throw TypeError.ArgNone("rpartition", "sep");
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
            return (
                s.Substring(0, index),
                sep,
                s.Substring(index + sep.Length)
            );
        }
    }
}
