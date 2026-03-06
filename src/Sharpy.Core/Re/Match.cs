using System;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Wraps a .NET Match object to provide a Python-like re.Match API.
    /// </summary>
    public class ReMatch
    {
        private readonly System.Text.RegularExpressions.Match _match;
        private readonly string _input;
        private readonly Regex _regex;

        internal ReMatch(System.Text.RegularExpressions.Match match, string input, Regex regex)
        {
            _match = match;
            _input = input;
            _regex = regex;
        }

        /// <summary>
        /// Return the string matched by the group (0 = entire match).
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
        /// </summary>
        public string? Group(string name)
        {
            var g = _match.Groups[name];
            if (g == null || !g.Success)
            {
                return null;
            }
            return g.Value;
        }

        /// <summary>
        /// Return a list of all matched subgroups (excluding group 0).
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
        /// Return a Dict of named groups.
        /// </summary>
        public Dict<string, string?> Groupdict()
        {
            var result = new Dict<string, string?>();
            string[] names = _regex.GetGroupNames();
            foreach (string name in names)
            {
                // Skip numeric group names
                if (int.TryParse(name, out _))
                    continue;
                var g = _match.Groups[name];
                result[name] = g.Success ? g.Value : null;
            }
            return result;
        }

        /// <summary>
        /// Return the start position of the match for the given group.
        /// </summary>
        public int Start(int group = 0)
        {
            if (group < 0 || group >= _match.Groups.Count)
            {
                throw new IndexError("no such group");
            }
            var g = _match.Groups[group];
            return g.Success ? g.Index : -1;
        }

        /// <summary>
        /// Return the end position of the match for the given group.
        /// </summary>
        public int End(int group = 0)
        {
            if (group < 0 || group >= _match.Groups.Count)
            {
                throw new IndexError("no such group");
            }
            var g = _match.Groups[group];
            return g.Success ? g.Index + g.Length : -1;
        }

        /// <summary>
        /// Return (start, end) tuple for the given group.
        /// </summary>
        public Tuple<int, int> Span(int group = 0)
        {
            return new Tuple<int, int>(Start(group), End(group));
        }

        /// <summary>
        /// The input string.
        /// </summary>
        public string String => _input;

        /// <summary>
        /// The start position of the search.
        /// </summary>
        public int Pos => 0;

        /// <summary>
        /// The end position of the search.
        /// </summary>
        public int Endpos => _input.Length;

        public override string ToString()
        {
            return "<re.Match object; span=(" + Start() + ", " + End() + "), match='" + Group() + "'>";
        }
    }
}
