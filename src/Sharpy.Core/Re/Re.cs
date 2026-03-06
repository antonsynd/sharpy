using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Sharpy
{
    public static partial class Re
    {
        // Python re flags
        public static readonly int IGNORECASE = 2;
        public static readonly int I = 2;
        public static readonly int MULTILINE = 8;
        public static readonly int M = 8;
        public static readonly int DOTALL = 16;
        public static readonly int S = 16;

        /// <summary>
        /// Search for the pattern anywhere in the string.
        /// </summary>
        public static ReMatch? Search(string pattern, string input, int flags = 0)
        {
            var compiled = CompileInternal(pattern, flags);
            return compiled.Search(input);
        }

        /// <summary>
        /// Match the pattern at the beginning of the string.
        /// </summary>
        public static ReMatch? Match(string pattern, string input, int flags = 0)
        {
            var compiled = CompileInternal(pattern, flags);
            return compiled.Match(input);
        }

        /// <summary>
        /// Match the pattern against the entire string.
        /// </summary>
        public static ReMatch? Fullmatch(string pattern, string input, int flags = 0)
        {
            var compiled = CompileInternal(pattern, flags);
            return compiled.Fullmatch(input);
        }

        /// <summary>
        /// Find all non-overlapping matches as a list of strings.
        /// </summary>
        public static List<string> Findall(string pattern, string input, int flags = 0)
        {
            var compiled = CompileInternal(pattern, flags);
            return compiled.Findall(input);
        }

        /// <summary>
        /// Find all matches as an iterator of Match objects.
        /// </summary>
        public static IEnumerable<ReMatch> Finditer(string pattern, string input, int flags = 0)
        {
            var compiled = CompileInternal(pattern, flags);
            return compiled.Finditer(input);
        }

        /// <summary>
        /// Return the string obtained by replacing the leftmost non-overlapping occurrences.
        /// </summary>
        public static string Sub(string pattern, string repl, string input, int count = 0, int flags = 0)
        {
            var compiled = CompileInternal(pattern, flags);
            return compiled.Sub(repl, input, count);
        }

        /// <summary>
        /// Split the string by occurrences of the pattern.
        /// </summary>
        public static List<string> Split(string pattern, string input, int maxsplit = 0, int flags = 0)
        {
            var compiled = CompileInternal(pattern, flags);
            return compiled.Split(input, maxsplit);
        }

        /// <summary>
        /// Compile a pattern into a Pattern object.
        /// </summary>
        public static RePattern Compile(string pattern, int flags = 0)
        {
            return new RePattern(pattern, flags);
        }

        /// <summary>
        /// Escape special characters in pattern.
        /// </summary>
        public static string Escape(string pattern)
        {
            return Regex.Escape(pattern);
        }

        private static RePattern CompileInternal(string pattern, int flags)
        {
            return new RePattern(pattern, flags);
        }

        /// <summary>
        /// Translate Python regex syntax to .NET regex syntax.
        /// </summary>
        internal static string TranslatePattern(string pattern)
        {
            // (?P<name>...) → (?<name>...)
            // (?P=name) → \k<name>
            var sb = new StringBuilder(pattern.Length);
            int i = 0;
            while (i < pattern.Length)
            {
                if (i + 3 < pattern.Length && pattern[i] == '(' && pattern[i + 1] == '?' && pattern[i + 2] == 'P')
                {
                    if (i + 3 < pattern.Length && pattern[i + 3] == '<')
                    {
                        // (?P<name>...) → (?<name>...)
                        sb.Append("(?<");
                        i += 4; // skip (?P<
                    }
                    else if (i + 3 < pattern.Length && pattern[i + 3] == '=')
                    {
                        // (?P=name) → \k<name>
                        i += 4; // skip (?P=
                        int nameStart = i;
                        while (i < pattern.Length && pattern[i] != ')')
                            i++;
                        string name = pattern.Substring(nameStart, i - nameStart);
                        sb.Append("\\k<");
                        sb.Append(name);
                        sb.Append('>');
                        if (i < pattern.Length)
                            i++; // skip )
                    }
                    else
                    {
                        sb.Append(pattern[i]);
                        i++;
                    }
                }
                else
                {
                    sb.Append(pattern[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Map Python re flags to .NET RegexOptions.
        /// </summary>
        internal static RegexOptions MapFlags(int flags)
        {
            var options = RegexOptions.None;
            if ((flags & IGNORECASE) != 0)
                options |= RegexOptions.IgnoreCase;
            if ((flags & MULTILINE) != 0)
                options |= RegexOptions.Multiline;
            if ((flags & DOTALL) != 0)
                options |= RegexOptions.Singleline;
            return options;
        }
    }
}
