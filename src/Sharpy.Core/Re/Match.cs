using System;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Wraps a .NET <see cref="System.Text.RegularExpressions.Match"/> with Python-compatible API.
    /// </summary>
    public sealed class ReMatch
    {
        private readonly System.Text.RegularExpressions.Match _match;
        private readonly Regex? _compiledRegex;

        /// <summary>The input string.</summary>
        public string String { get; }

        /// <summary>The pattern string.</summary>
        public string Pattern { get; }

        /// <summary>The start position of the search.</summary>
        public int Pos { get; }

        /// <summary>The end position of the search.</summary>
        public int Endpos { get; }

        internal ReMatch(System.Text.RegularExpressions.Match match, string input, string pattern, int pos, int endpos, Regex? compiledRegex = null)
        {
            _match = match;
            String = input;
            Pattern = pattern;
            Pos = pos;
            Endpos = endpos;
            _compiledRegex = compiledRegex;
        }

        /// <summary>
        /// Return the string matched by group number. Group 0 is the entire match.
        /// Returns null if the group didn't participate in the match.
        /// </summary>
        public string? Group(int n = 0)
        {
            if (n < 0 || n >= _match.Groups.Count)
            {
                throw new IndexError("no such group");
            }

            var g = _match.Groups[n];
            return g.Success ? g.Value : null;
        }

        /// <summary>
        /// Return the string matched by a named group.
        /// Returns null if the group didn't participate in the match.
        /// </summary>
        public string? Group(string name)
        {
            var g = _match.Groups[name];
            if (g == null)
            {
                throw new IndexError("no such group");
            }

            return g.Success ? g.Value : null;
        }

        /// <summary>
        /// Return a list of all subgroups (groups 1..n).
        /// </summary>
        public List<string?> Groups()
        {
            var result = new List<string?>();
            for (int i = 1; i < _match.Groups.Count; i++)
            {
                var g = _match.Groups[i];
                result.Append(g.Success ? g.Value : null);
            }

            return result;
        }

        /// <summary>
        /// Return a Dict of all named subgroups.
        /// </summary>
        public Dict<string, string?> Groupdict()
        {
            var result = new Dict<string, string?>();
            foreach (string name in GetGroupNames())
            {
                var g = _match.Groups[name];
                result[name] = g.Success ? g.Value : null;
            }

            return result;
        }

        /// <summary>Start index of the matched group.</summary>
        public int Start(int group = 0)
        {
            if (group < 0 || group >= _match.Groups.Count)
            {
                throw new IndexError("no such group");
            }

            var g = _match.Groups[group];
            return g.Success ? g.Index : -1;
        }

        /// <summary>End index of the matched group.</summary>
        public int End(int group = 0)
        {
            if (group < 0 || group >= _match.Groups.Count)
            {
                throw new IndexError("no such group");
            }

            var g = _match.Groups[group];
            return g.Success ? g.Index + g.Length : -1;
        }

        /// <summary>Returns (start, end) for the matched group.</summary>
        public (int, int) Span(int group = 0)
        {
            return (Start(group), End(group));
        }

        private string[] GetGroupNames()
        {
            // Use cached regex if available, otherwise compile once
            var regex = _compiledRegex ?? new Regex(RePatternTranslator.Translate(Pattern));
            var names = regex.GetGroupNames();
            // Filter out numeric-only names (those are unnamed groups)
            var named = new System.Collections.Generic.List<string>();
            foreach (string name in names)
            {
                if (!int.TryParse(name, out _))
                {
                    named.Add(name);
                }
            }

            return named.ToArray();
        }

        /// <summary>
        /// String representation of the match.
        /// </summary>
        public override string ToString()
        {
            return "<re.Match object; span=(" + _match.Index + ", " + (_match.Index + _match.Length) + "), match='" + _match.Value + "'>";
        }
    }
}
