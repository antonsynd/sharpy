using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Wraps a compiled .NET Regex to provide a Python-like re.Pattern API.
    /// </summary>
    public class RePattern
    {
        private readonly Regex _regex;
        private readonly string _pattern;
        private readonly int _flags;

        internal RePattern(string pattern, int flags)
        {
            _pattern = pattern;
            _flags = flags;
            string translated = Re.TranslatePattern(pattern);
            _regex = new Regex(translated, Re.MapFlags(flags));
        }

        internal Regex InnerRegex => _regex;

        /// <summary>The pattern string.</summary>
        public string PatternStr => _pattern;

        /// <summary>The flags.</summary>
        public int Flags => _flags;

        /// <summary>Search for the pattern anywhere in the string.</summary>
        public ReMatch? Search(string input)
        {
            var m = _regex.Match(input);
            return m.Success ? new ReMatch(m, input, _regex) : null;
        }

        /// <summary>Match the pattern at the start of the string.</summary>
        public ReMatch? Match(string input)
        {
            var m = _regex.Match(input);
            return (m.Success && m.Index == 0) ? new ReMatch(m, input, _regex) : null;
        }

        /// <summary>Match the pattern against the entire string.</summary>
        public ReMatch? Fullmatch(string input)
        {
            var m = _regex.Match(input);
            return (m.Success && m.Index == 0 && m.Length == input.Length) ? new ReMatch(m, input, _regex) : null;
        }

        /// <summary>Find all non-overlapping matches.</summary>
        public List<string> Findall(string input)
        {
            var result = new List<string>();
            var matches = _regex.Matches(input);
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                // If there are groups, return group 1 (Python behavior)
                if (m.Groups.Count > 1)
                {
                    result.Append(m.Groups[1].Value);
                }
                else
                {
                    result.Append(m.Value);
                }
            }
            return result;
        }

        /// <summary>Find all matches as an iterator of Match objects.</summary>
        public IEnumerable<ReMatch> Finditer(string input)
        {
            var matches = _regex.Matches(input);
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                yield return new ReMatch(m, input, _regex);
            }
        }

        /// <summary>Return a substituted string.</summary>
        public string Sub(string repl, string input, int count = 0)
        {
            return count > 0
                ? _regex.Replace(input, repl, count)
                : _regex.Replace(input, repl);
        }

        /// <summary>Split the string by pattern occurrences.</summary>
        public List<string> Split(string input, int maxsplit = 0)
        {
            var result = new List<string>();
            string[] parts;
            if (maxsplit > 0)
            {
                parts = _regex.Split(input, maxsplit + 1);
            }
            else
            {
                parts = _regex.Split(input);
            }
            foreach (var p in parts)
            {
                result.Append(p);
            }
            return result;
        }
    }
}
