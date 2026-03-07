using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Compiled regular expression pattern, wrapping .NET's Regex.
    /// </summary>
    public sealed class RePattern
    {
        private readonly Regex _regex;

        /// <summary>The original pattern string.</summary>
        public string PatternStr { get; }

        /// <summary>The flags used to compile this pattern.</summary>
        public int Flags { get; }

        internal RePattern(string pattern, int flags)
        {
            PatternStr = pattern;
            Flags = flags;
            string translated = RePatternTranslator.Translate(pattern);
            _regex = new Regex(translated, FlagsToOptions(flags));
        }

        /// <summary>
        /// Scan through string looking for the first location where the pattern produces a match.
        /// </summary>
        public ReMatch? Search(string s, int pos = 0, int endpos = -1)
        {
            string target = ApplyEndpos(s, endpos);
            var m = _regex.Match(target, pos);
            if (!m.Success)
                return null;
            return new ReMatch(m, s, PatternStr, pos, endpos < 0 ? s.Length : endpos, _regex);
        }

        /// <summary>
        /// Try to apply the pattern at the start of the string.
        /// </summary>
        public ReMatch? Match(string s, int pos = 0, int endpos = -1)
        {
            string target = ApplyEndpos(s, endpos);
            var m = _regex.Match(target, pos);
            if (!m.Success || m.Index != pos)
                return null;
            return new ReMatch(m, s, PatternStr, pos, endpos < 0 ? s.Length : endpos, _regex);
        }

        /// <summary>
        /// Try to apply the pattern to the entire string.
        /// </summary>
        public ReMatch? Fullmatch(string s, int pos = 0, int endpos = -1)
        {
            string target = ApplyEndpos(s, endpos);
            var m = _regex.Match(target, pos);
            if (!m.Success || m.Index != pos || m.Length != target.Length - pos)
                return null;
            return new ReMatch(m, s, PatternStr, pos, endpos < 0 ? s.Length : endpos, _regex);
        }

        /// <summary>
        /// Return all non-overlapping matches as a list of strings.
        /// If the pattern has groups, returns the group(s) rather than the full match.
        /// </summary>
        public List<object?> Findall(string s, int pos = 0, int endpos = -1)
        {
            string target = ApplyEndpos(s, endpos);
            var matches = _regex.Matches(target);
            var result = new List<object?>();
            bool hasGroups = _regex.GetGroupNumbers().Length > 1;

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (m.Index < pos)
                    continue;

                if (!hasGroups)
                {
                    result.Append(m.Value);
                }
                else if (m.Groups.Count == 2)
                {
                    // Single group → return group value
                    result.Append(m.Groups[1].Success ? m.Groups[1].Value : "");
                }
                else
                {
                    // Multiple groups → return list of group values
                    var groupValues = new List<object?>();
                    for (int i = 1; i < m.Groups.Count; i++)
                    {
                        groupValues.Append(m.Groups[i].Success ? m.Groups[i].Value : "");
                    }

                    result.Append(groupValues);
                }
            }

            return result;
        }

        /// <summary>
        /// Return an iterator yielding ReMatch objects over all non-overlapping matches.
        /// </summary>
        public List<ReMatch> Finditer(string s, int pos = 0, int endpos = -1)
        {
            string target = ApplyEndpos(s, endpos);
            var matches = _regex.Matches(target);
            var result = new List<ReMatch>();
            int actualEndpos = endpos < 0 ? s.Length : endpos;

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (m.Index < pos)
                    continue;
                result.Append(new ReMatch(m, s, PatternStr, pos, actualEndpos, _regex));
            }

            return result;
        }

        /// <summary>
        /// Return the string obtained by replacing the leftmost non-overlapping occurrences of pattern.
        /// </summary>
        public string Sub(string repl, string s, int count = 0)
        {
            if (count == 0)
            {
                return _regex.Replace(s, repl);
            }

            return _regex.Replace(s, repl, count);
        }

        /// <summary>
        /// Split string by the occurrences of the pattern.
        /// </summary>
        public List<string> Split(string s, int maxsplit = 0)
        {
            string[] parts;
            if (maxsplit == 0)
            {
                parts = _regex.Split(s);
            }
            else
            {
                parts = _regex.Split(s, maxsplit + 1);
            }

            var result = new List<string>();
            foreach (string p in parts)
            {
                result.Append(p);
            }

            return result;
        }

        public override string ToString()
        {
            return "re.compile('" + PatternStr + "')";
        }

        private static string ApplyEndpos(string s, int endpos)
        {
            if (endpos < 0 || endpos >= s.Length)
                return s;
            return s.Substring(0, endpos);
        }

        internal static RegexOptions FlagsToOptions(int flags)
        {
            var options = RegexOptions.None;
            if ((flags & Re.IGNORECASE) != 0)
            {
                options |= RegexOptions.IgnoreCase;
            }

            if ((flags & Re.MULTILINE) != 0)
            {
                options |= RegexOptions.Multiline;
            }

            if ((flags & Re.DOTALL) != 0)
            {
                options |= RegexOptions.Singleline;
            }

            return options;
        }
    }
}
