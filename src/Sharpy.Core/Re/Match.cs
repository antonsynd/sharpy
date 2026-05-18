using System;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// Wraps a .NET <see cref="System.Text.RegularExpressions.Match"/> with Python-compatible API.
    /// </summary>
    [SharpyModuleType("re", "Match")]
    public sealed class ReMatch
    {
        private readonly System.Text.RegularExpressions.Match _match;
        private readonly RePattern? _pattern;

        /// <summary>The input string.</summary>
        public string String { get; }

        /// <summary>The pattern string.</summary>
        public string Pattern { get; }

        /// <summary>The start position of the search.</summary>
        public int Pos { get; }

        /// <summary>The end position of the search.</summary>
        public int Endpos { get; }

        /// <summary>The compiled pattern object that produced this match, or null.</summary>
        public RePattern? Re => _pattern;

        internal ReMatch(System.Text.RegularExpressions.Match match, string input, string pattern, int pos, int endpos, RePattern? compiledPattern = null)
        {
            _match = match;
            String = input;
            Pattern = pattern;
            Pos = pos;
            Endpos = endpos;
            _pattern = compiledPattern;
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

        /// <summary>
        /// The integer index of the last matched capturing group, or null if no group matched.
        /// </summary>
        public int? Lastindex
        {
            get
            {
                for (int i = _match.Groups.Count - 1; i >= 1; i--)
                {
                    if (_match.Groups[i].Success)
                        return i;
                }

                return null;
            }
        }

        /// <summary>
        /// The name of the last matched capturing group, or null if unnamed or no group matched.
        /// </summary>
        public string? Lastgroup
        {
            get
            {
                var idx = Lastindex;
                if (idx == null)
                    return null;
                var regex = _pattern?.InternalRegex;
                if (regex == null)
                    return null;
                string name = regex.GroupNameFromNumber(idx.Value);
                // If the name is just the numeric index, it's unnamed
                if (name == idx.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    return null;
                return name;
            }
        }

        /// <summary>
        /// Return the string obtained by doing backslash substitution on the template.
        /// Supports \1, \2, ... and \g&lt;name&gt; references.
        /// </summary>
        public string Expand(string template)
        {
            string translated = TranslateTemplate(template);
            return _match.Result(translated);
        }

        /// <summary>Access group by index. Equivalent to Group(n).</summary>
        public string? this[int n] => Group(n);

        private static string TranslateTemplate(string template)
        {
            var sb = new System.Text.StringBuilder(template.Length);
            int i = 0;
            while (i < template.Length)
            {
                if (template[i] == '\\' && i + 1 < template.Length)
                {
                    if (template[i + 1] == 'g' && i + 2 < template.Length && template[i + 2] == '<')
                    {
                        // \g<name> → ${name}
                        i += 3;
                        int nameStart = i;
                        while (i < template.Length && template[i] != '>')
                            i++;
                        string name = template.Substring(nameStart, i - nameStart);
                        sb.Append("${");
                        sb.Append(name);
                        sb.Append('}');
                        if (i < template.Length)
                            i++;
                        continue;
                    }

                    if (char.IsDigit(template[i + 1]))
                    {
                        // \N → $N
                        sb.Append('$');
                        sb.Append(template[i + 1]);
                        i += 2;
                        continue;
                    }

                    // Other escapes: pass through
                    sb.Append(template[i]);
                    sb.Append(template[i + 1]);
                    i += 2;
                    continue;
                }

                sb.Append(template[i]);
                i++;
            }

            return sb.ToString();
        }

        private string[] GetGroupNames()
        {
            // Use compiled pattern's regex if available, otherwise compile once
            var regex = _pattern?.InternalRegex ?? new Regex(RePatternTranslator.Translate(Pattern));
            var names = regex.GetGroupNames();
            // Filter out numeric-only names (those are unnamed groups)
            var named = new System.Collections.Generic.List<string>();
            foreach (string name in names)
            {
                if (!int.TryParse(name, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
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
