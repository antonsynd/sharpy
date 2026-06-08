#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    /// <summary>
    /// Regular expression matching operations.
    /// </summary>
    public static partial class ReModule
    {
        public static int IGNORECASE = 2;
        public static int I = 2;
        public static int MULTILINE = 8;
        public static int M = 8;
        public static int DOTALL = 16;
        public static int S = 16;
        public static int VERBOSE = 64;
        public static int X = 64;
        public static int UNICODE = 32;
        public static int U = 32;
        public static int ASCII = 256;
        public static int A = 256;
        /// <summary>
        /// Fast path: check if pattern contains Python-specific syntax.
        /// </summary>
        internal static bool _NeedsTranslation(string pattern)
        {
            int i = 0;
            while (i < pattern.Length)
            {
                if (global::Sharpy.StringHelpers.GetItem(pattern, i) == "\\" && i + 1 < pattern.Length)
                {
                    i = i + 2;
                    continue;
                }

                if (global::Sharpy.StringHelpers.GetItem(pattern, i) == "(" && i + 1 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 1) == "?")
                {
                    if (i + 2 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 2) == "P")
                    {
                        return true;
                    }

                    if (i + 2 < pattern.Length && _IsInlineFlagChar(global::Sharpy.StringHelpers.GetItem(pattern, i + 2)))
                    {
                        int j = i + 2;
                        while (j < pattern.Length && _IsInlineFlagChar(global::Sharpy.StringHelpers.GetItem(pattern, j)))
                        {
                            j = j + 1;
                        }

                        if (j < pattern.Length && (global::Sharpy.StringHelpers.GetItem(pattern, j) == ")" || global::Sharpy.StringHelpers.GetItem(pattern, j) == ":"))
                        {
                            string flags = pattern.Substring(i + 2, j - (i + 2));
                            if (flags.Contains("a") || flags.Contains("u") || flags.Contains("L"))
                            {
                                return true;
                            }
                        }
                    }
                }

                i = i + 1;
            }

            return false;
        }

        /// <summary>
        /// Check if character is a valid inline flag character.
        /// </summary>
        internal static bool _IsInlineFlagChar(string c)
        {
            return "aiLmsux".Contains(c);
        }

        /// <summary>
        /// Strip Python-only flags (a, u, L), keep .NET-compatible ones (i, m, s, x).
        /// </summary>
        internal static string _FilterInlineFlags(string flags)
        {
            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder(flags.Length);
            int i = 0;
            while (i < flags.Length)
            {
                string c = global::Sharpy.StringHelpers.GetItem(flags, i);
                if (c != "a" && c != "u" && c != "L")
                {
                    sb.Append(c);
                }

                i = i + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Translate Python regex syntax to .NET regex syntax.
        /// </summary>
        internal static string _TranslatePattern(string pattern)
        {
            if (pattern == null)
            {
                throw new global::Sharpy.TypeError("expected string, got NoneType");
            }

            if (!_NeedsTranslation(pattern))
            {
                return pattern;
            }

            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder(pattern.Length);
            int i = 0;
            while (i < pattern.Length)
            {
                if (global::Sharpy.StringHelpers.GetItem(pattern, i) == "\\" && i + 1 < pattern.Length)
                {
                    sb.Append(global::Sharpy.StringHelpers.GetItem(pattern, i));
                    sb.Append(global::Sharpy.StringHelpers.GetItem(pattern, i + 1));
                    i = i + 2;
                    continue;
                }

                if (global::Sharpy.StringHelpers.GetItem(pattern, i) == "(" && i + 1 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 1) == "?")
                {
                    if (i + 2 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 2) == "P")
                    {
                        if (i + 3 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 3) == "<")
                        {
                            sb.Append("(?<");
                            i = i + 4;
                            continue;
                        }

                        if (i + 3 < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i + 3) == "=")
                        {
                            i = i + 4;
                            int nameStart = i;
                            while (i < pattern.Length && global::Sharpy.StringHelpers.GetItem(pattern, i) != ")")
                            {
                                i = i + 1;
                            }

                            string name = pattern.Substring(nameStart, i - nameStart);
                            sb.Append("\\k<");
                            sb.Append(name);
                            sb.Append(">");
                            if (i < pattern.Length)
                            {
                                i = i + 1;
                            }

                            continue;
                        }
                    }

                    if (i + 2 < pattern.Length && _IsInlineFlagChar(global::Sharpy.StringHelpers.GetItem(pattern, i + 2)))
                    {
                        int flagStart = i + 2;
                        int j = flagStart;
                        while (j < pattern.Length && _IsInlineFlagChar(global::Sharpy.StringHelpers.GetItem(pattern, j)))
                        {
                            j = j + 1;
                        }

                        if (j < pattern.Length && (global::Sharpy.StringHelpers.GetItem(pattern, j) == ")" || global::Sharpy.StringHelpers.GetItem(pattern, j) == ":"))
                        {
                            string flagsStr = pattern.Substring(flagStart, j - flagStart);
                            string filtered = _FilterInlineFlags(flagsStr);
                            if (global::Sharpy.StringHelpers.GetItem(pattern, j) == ")")
                            {
                                if (filtered.Length > 0)
                                {
                                    sb.Append("(?");
                                    sb.Append(filtered);
                                    sb.Append(")");
                                }

                                i = j + 1;
                                continue;
                            }
                            else
                            {
                                if (filtered.Length > 0)
                                {
                                    sb.Append("(?");
                                    sb.Append(filtered);
                                    sb.Append(":");
                                }
                                else
                                {
                                    sb.Append("(?:");
                                }

                                i = j + 1;
                                continue;
                            }
                        }
                    }
                }

                sb.Append(global::Sharpy.StringHelpers.GetItem(pattern, i));
                i = i + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Translate Python replacement syntax to .NET replacement syntax.
        /// </summary>
        internal static string _TranslateReplacement(string template)
        {
            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder(template.Length);
            int i = 0;
            while (i < template.Length)
            {
                if (global::Sharpy.StringHelpers.GetItem(template, i) == "\\" && i + 1 < template.Length)
                {
                    if (global::Sharpy.StringHelpers.GetItem(template, i + 1) == "g" && i + 2 < template.Length && global::Sharpy.StringHelpers.GetItem(template, i + 2) == "<")
                    {
                        i = i + 3;
                        int nameStart = i;
                        while (i < template.Length && global::Sharpy.StringHelpers.GetItem(template, i) != ">")
                        {
                            i = i + 1;
                        }

                        string grpName = template.Substring(nameStart, i - nameStart);
                        sb.Append("${");
                        sb.Append(grpName);
                        sb.Append("}");
                        if (i < template.Length)
                        {
                            i = i + 1;
                        }

                        continue;
                    }

                    if (global::Sharpy.StringHelpers.GetItem(template, i + 1).Isdigit())
                    {
                        sb.Append("$");
                        sb.Append(global::Sharpy.StringHelpers.GetItem(template, i + 1));
                        i = i + 2;
                        continue;
                    }

                    sb.Append(global::Sharpy.StringHelpers.GetItem(template, i));
                    sb.Append(global::Sharpy.StringHelpers.GetItem(template, i + 1));
                    i = i + 2;
                    continue;
                }

                sb.Append(global::Sharpy.StringHelpers.GetItem(template, i));
                i = i + 1;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Map Python flag ints to .NET RegexOptions.
        /// </summary>
        internal static global::System.Text.RegularExpressions.RegexOptions _FlagsToOptions(int flags)
        {
            int options = 0;
            if ((flags & IGNORECASE) != 0)
            {
                options = options | ((int)global::System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            if ((flags & MULTILINE) != 0)
            {
                options = options | ((int)global::System.Text.RegularExpressions.RegexOptions.Multiline);
            }

            if ((flags & DOTALL) != 0)
            {
                options = options | ((int)global::System.Text.RegularExpressions.RegexOptions.Singleline);
            }

            if ((flags & VERBOSE) != 0)
            {
                options = options | ((int)global::System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);
            }

            return (global::System.Text.RegularExpressions.RegexOptions)options;
        }

        /// <summary>
        /// Exception raised when a regex pattern is invalid.
        /// </summary>
        public class Error : Exception
        {
            public string Msg;
            public string? Pattern;
            public int? Pos;
            public int? Lineno;
            public int? Colno;
            /// <summary>
            /// Create an error with the specified message and optional pattern/position info.
            /// </summary>
            public Error(string msg, string? pattern = default, int? pos = default) : base(msg)
            {
                this.Msg = msg;
                this.Pattern = pattern;
                this.Pos = pos;
                if (pos != null && pattern != null)
                {
                    int line = 1;
                    int col = pos.Value + 1;
                    int idx = 0;
                    while (idx < pos.Value && idx < pattern!.Length)
                    {
                        if (global::Sharpy.StringHelpers.GetItem(pattern!, idx) == "\n")
                        {
                            line = line + 1;
                            col = pos.Value - idx;
                        }

                        idx = idx + 1;
                    }

                    this.Lineno = line;
                    this.Colno = col;
                }
                else
                {
                    this.Lineno = default;
                    this.Colno = default;
                }
            }
        }

        /// <summary>
        /// Compiled regular expression pattern, wrapping .NET Regex.
        /// </summary>
        public sealed class Pattern
        {
            private global::System.Text.RegularExpressions.Regex _Regex;
            private string _PatternStr;
            private int _Flags;
            private Sharpy.Dict<string, int> _Groupindex;
            private bool _GroupindexCached;
            /// <summary>
            /// Scan through string looking for the first match.
            /// </summary>
            public MatchResult? Search(string s, int pos = 0, int? endpos = default)
            {
                string target = _ApplyEndpos(s, endpos);
                global::System.Text.RegularExpressions.Match m = this._Regex.Match(target, pos);
                if (!m.Success)
                {
                    return default;
                }

                int actualEndpos = s.Length;
                if (endpos != null)
                {
                    actualEndpos = endpos.Value;
                }

                return new MatchResult(m, s, this._PatternStr, pos, actualEndpos, this);
            }

            /// <summary>
            /// Try to apply the pattern at the start of the string.
            /// </summary>
            public MatchResult? Match(string s, int pos = 0, int? endpos = default)
            {
                string target = _ApplyEndpos(s, endpos);
                global::System.Text.RegularExpressions.Match m = this._Regex.Match(target, pos);
                if (!m.Success || m.Index != pos)
                {
                    return default;
                }

                int actualEndpos = s.Length;
                if (endpos != null)
                {
                    actualEndpos = endpos.Value;
                }

                return new MatchResult(m, s, this._PatternStr, pos, actualEndpos, this);
            }

            /// <summary>
            /// Try to apply the pattern to the entire string.
            /// </summary>
            public MatchResult? Fullmatch(string s, int pos = 0, int? endpos = default)
            {
                string target = _ApplyEndpos(s, endpos);
                global::System.Text.RegularExpressions.Match m = this._Regex.Match(target, pos);
                if (!m.Success || m.Index != pos || m.Length != target.Length - pos)
                {
                    return default;
                }

                int actualEndpos = s.Length;
                if (endpos != null)
                {
                    actualEndpos = endpos.Value;
                }

                return new MatchResult(m, s, this._PatternStr, pos, actualEndpos, this);
            }

            /// <summary>
            /// Return all non-overlapping matches as a list.
            /// </summary>
            public Sharpy.List<object> Findall(string s, int pos = 0, int? endpos = default)
            {
                string target = _ApplyEndpos(s, endpos);
                global::System.Text.RegularExpressions.MatchCollection matches = this._Regex.Matches(target);
                Sharpy.List<object> result = new Sharpy.List<object>()
                {
                };
                bool hasGroups = global::Sharpy.Builtins.Len(this._Regex.GetGroupNumbers()) > 1;
                foreach (var __loopVar_0 in matches)
                {
                    var mRaw = __loopVar_0;
                    global::System.Text.RegularExpressions.Match m = (global::System.Text.RegularExpressions.Match)mRaw;
                    if (m.Index < pos)
                    {
                        continue;
                    }

                    if (!hasGroups)
                    {
                        result.Append(m.Value);
                    }
                    else if (m.Groups.Count == 2)
                    {
                        global::System.Text.RegularExpressions.Group g = m.Groups[1];
                        result.Append(g.Success ? g.Value : "");
                    }
                    else
                    {
                        Sharpy.List<object> groupValues = new Sharpy.List<object>()
                        {
                        };
                        int gi = 1;
                        while (gi < m.Groups.Count)
                        {
                            global::System.Text.RegularExpressions.Group grp = m.Groups[gi];
                            groupValues.Append(grp.Success ? grp.Value : "");
                            gi = gi + 1;
                        }

                        result.Append(groupValues);
                    }
                }

                return result;
            }

            /// <summary>
            /// Return a list of MatchResult objects over all non-overlapping matches.
            /// </summary>
            public Sharpy.List<MatchResult> Finditer(string s, int pos = 0, int? endpos = default)
            {
                string target = _ApplyEndpos(s, endpos);
                global::System.Text.RegularExpressions.MatchCollection matches = this._Regex.Matches(target);
                Sharpy.List<MatchResult> result = new Sharpy.List<MatchResult>()
                {
                };
                int actualEndpos = s.Length;
                if (endpos != null)
                {
                    actualEndpos = endpos.Value;
                }

                foreach (var __loopVar_1 in matches)
                {
                    var mRaw = __loopVar_1;
                    global::System.Text.RegularExpressions.Match m = (global::System.Text.RegularExpressions.Match)mRaw;
                    if (m.Index < pos)
                    {
                        continue;
                    }

                    result.Append(new MatchResult(m, s, this._PatternStr, pos, actualEndpos, this));
                }

                return result;
            }

            /// <summary>
            /// Return the string obtained by replacing occurrences using a string.
            /// </summary>
            public string Sub(string repl, string s, int count = 0)
            {
                string translated = _TranslateReplacement(repl);
                if (count == 0)
                {
                    return this._Regex.Replace(s, translated);
                }

                return this._Regex.Replace(s, translated, count);
            }

            /// <summary>
            /// Return the string obtained by replacing occurrences using a callable.
            /// </summary>
            public string Sub(global::System.Func<MatchResult, string> repl, string s, int count = 0)
            {
                Sharpy.List<int> replaced = new Sharpy.List<int>()
                {
                    0
                };
                string Evaluator(global::System.Text.RegularExpressions.Match m)
                {
                    if (count > 0 && replaced[0] >= count)
                    {
                        return m.Value;
                    }

                    replaced[0] = replaced[0] + 1;
                    MatchResult reMatch = new MatchResult(m, s, this._PatternStr, 0, s.Length, this);
                    return repl(reMatch);
                }

                return this._Regex.Replace(s, Evaluator);
            }

            /// <summary>
            /// Like sub(), but returns (new_string, number_of_subs_made).
            /// </summary>
            public global::System.ValueTuple<string, int> Subn(string repl, string s, int count = 0)
            {
                string translated = _TranslateReplacement(repl);
                Sharpy.List<int> replacementCount = new Sharpy.List<int>()
                {
                    0
                };
                string StrEvaluator(global::System.Text.RegularExpressions.Match m)
                {
                    replacementCount[0] = replacementCount[0] + 1;
                    return m.Result(translated);
                }

                string result;
                if (count == 0)
                {
                    result = this._Regex.Replace(s, StrEvaluator);
                }
                else
                {
                    result = this._Regex.Replace(s, StrEvaluator, count);
                }

                return (result, replacementCount[0]);
            }

            /// <summary>
            /// Like sub() with callable, but returns (new_string, number_of_subs_made).
            /// </summary>
            public global::System.ValueTuple<string, int> Subn(global::System.Func<MatchResult, string> repl, string s, int count = 0)
            {
                Sharpy.List<int> replacementCount = new Sharpy.List<int>()
                {
                    0
                };
                string CallableEvaluator(global::System.Text.RegularExpressions.Match m)
                {
                    if (count > 0 && replacementCount[0] >= count)
                    {
                        return m.Value;
                    }

                    replacementCount[0] = replacementCount[0] + 1;
                    MatchResult reMatch = new MatchResult(m, s, this._PatternStr, 0, s.Length, this);
                    return repl(reMatch);
                }

                string result = this._Regex.Replace(s, CallableEvaluator);
                return (result, replacementCount[0]);
            }

            /// <summary>
            /// Split string by the occurrences of the pattern.
            /// </summary>
            public Sharpy.List<string> Split(string s, int maxsplit = 0)
            {
                Sharpy.List<string> result = new Sharpy.List<string>()
                {
                };
                if (maxsplit == 0)
                {
                    foreach (var __loopVar_2 in this._Regex.Split(s))
                    {
                        var part = __loopVar_2;
                        result.Append(part);
                    }
                }
                else
                {
                    foreach (var __loopVar_3 in this._Regex.Split(s, maxsplit + 1))
                    {
                        var part = __loopVar_3;
                        result.Append(part);
                    }
                }

                return result;
            }

            /// <summary>
            /// Returns a string representation of the compiled pattern.
            /// </summary>
            public override string ToString()
            {
                return "re.compile('" + this._PatternStr + "')";
            }

            public string PatternStr
            {
                get
                {
                    _ = "The original pattern string.";
                    return this._PatternStr;
                }
            }

            public int Flags
            {
                get
                {
                    _ = "The flags used to compile this pattern.";
                    return this._Flags;
                }
            }

            public int Groups
            {
                get
                {
                    _ = "The number of capturing groups in the pattern.";
                    return global::Sharpy.Builtins.Len(this._Regex.GetGroupNumbers()) - 1;
                }
            }

            public Sharpy.Dict<string, int> Groupindex
            {
                get
                {
                    _ = "A dictionary mapping named group names to group numbers.";
                    if (global::Sharpy.Builtins.Len(this._Groupindex) > 0 || this._GroupindexCached)
                    {
                        return this._Groupindex;
                    }

                    var names = this._Regex.GetGroupNames();
                    foreach (var __loopVar_4 in names)
                    {
                        var name = __loopVar_4;
                        if (!name.Isdigit())
                        {
                            this._Groupindex[name] = this._Regex.GroupNumberFromName(name);
                        }
                    }

                    this._GroupindexCached = true;
                    return this._Groupindex;
                }
            }

            public global::System.Text.RegularExpressions.Regex Regex
            {
                get
                {
                    _ = "The internal .NET Regex object.";
                    return this._Regex;
                }
            }

            /// <summary>
            /// Compile a regular expression pattern.
            /// </summary>
            public Pattern(string patternStr, int flags = 0)
            {
                this._PatternStr = patternStr;
                this._Flags = flags;
                this._Groupindex = new Sharpy.Dict<string, int>()
                {
                };
                this._GroupindexCached = false;
                string translated = _TranslatePattern(patternStr);
                try
                {
                    this._Regex = new global::System.Text.RegularExpressions.Regex(translated, _FlagsToOptions(flags));
                }
                catch (Exception ex)
                {
                    throw new Error(ex.Message, patternStr);
                }
            }
        }

        /// <summary>
        /// Wraps a .NET Match with Python-compatible regex match API.
        /// </summary>
        public sealed class MatchResult
        {
            private global::System.Text.RegularExpressions.Match _Match;
            private string _String;
            private string _PatternStr;
            private int _Pos;
            private int _Endpos;
            private Pattern? _Re;
            /// <summary>
            /// Return the string matched by group number.
            /// </summary>
            public string? Group(int n = 0)
            {
                if (n < 0 || n >= this._Match.Groups.Count)
                {
                    throw new global::Sharpy.IndexError("no such group");
                }

                global::System.Text.RegularExpressions.Group g = this._Match.Groups[n];
                return g.Success ? g.Value : default;
            }

            /// <summary>
            /// Return the string matched by a named group.
            /// </summary>
            public string? Group(string name)
            {
                if (this._Re != null)
                {
                    if (this._Re!.Regex.GroupNumberFromName(name) == -1)
                    {
                        throw new global::Sharpy.IndexError("no such group");
                    }
                }

                global::System.Text.RegularExpressions.Group g = this._Match.Groups[name];
                return g.Success ? g.Value : default;
            }

            /// <summary>
            /// Return a list of all subgroups (groups 1..n).
            /// </summary>
            public Sharpy.List<string?> Groups()
            {
                Sharpy.List<string?> result = new Sharpy.List<string?>()
                {
                };
                int i = 1;
                while (i < this._Match.Groups.Count)
                {
                    global::System.Text.RegularExpressions.Group g = this._Match.Groups[i];
                    result.Append(g.Success ? g.Value : default);
                    i = i + 1;
                }

                return result;
            }

            /// <summary>
            /// Return a dict of all named subgroups.
            /// </summary>
            public Sharpy.Dict<string, string?> Groupdict()
            {
                Sharpy.Dict<string, string?> result = new Sharpy.Dict<string, string?>()
                {
                };
                if (this._Re == null)
                {
                    return result;
                }

                var names = this._Re!.Regex.GetGroupNames();
                foreach (var __loopVar_5 in names)
                {
                    var name = __loopVar_5;
                    if (!name.Isdigit())
                    {
                        global::System.Text.RegularExpressions.Group g = this._Match.Groups[name];
                        result[name] = g.Success ? g.Value : default;
                    }
                }

                return result;
            }

            /// <summary>
            /// Start index of the matched group.
            /// </summary>
            public int Start(int groupNum = 0)
            {
                if (groupNum < 0 || groupNum >= this._Match.Groups.Count)
                {
                    throw new global::Sharpy.IndexError("no such group");
                }

                global::System.Text.RegularExpressions.Group g = this._Match.Groups[groupNum];
                return g.Success ? g.Index : -1;
            }

            /// <summary>
            /// End index of the matched group.
            /// </summary>
            public int End(int groupNum = 0)
            {
                if (groupNum < 0 || groupNum >= this._Match.Groups.Count)
                {
                    throw new global::Sharpy.IndexError("no such group");
                }

                global::System.Text.RegularExpressions.Group g = this._Match.Groups[groupNum];
                return g.Success ? g.Index + g.Length : -1;
            }

            /// <summary>
            /// Returns (start, end) for the matched group.
            /// </summary>
            public global::System.ValueTuple<int, int> Span(int groupNum = 0)
            {
                return (this.Start(groupNum), this.End(groupNum));
            }

            /// <summary>
            /// Return the string obtained by doing backslash substitution on the template.
            /// </summary>
            public string Expand(string template)
            {
                string translated = _TranslateReplacement(template);
                return this._Match.Result(translated);
            }

            /// <summary>
            /// String representation of the match.
            /// </summary>
            public override string ToString()
            {
                return "<re.Match object; span=(" + global::Sharpy.Builtins.Str(this._Match.Index) + ", " + global::Sharpy.Builtins.Str(this._Match.Index + this._Match.Length) + "), match='" + this._Match.Value + "'>";
            }

            public string String
            {
                get
                {
                    _ = "The input string.";
                    return this._String;
                }
            }

            public string Pattern
            {
                get
                {
                    _ = "The pattern string.";
                    return this._PatternStr;
                }
            }

            public int Pos
            {
                get
                {
                    _ = "The start position of the search.";
                    return this._Pos;
                }
            }

            public int Endpos
            {
                get
                {
                    _ = "The end position of the search.";
                    return this._Endpos;
                }
            }

            public Pattern? Re
            {
                get
                {
                    _ = "The compiled pattern object that produced this match.";
                    return this._Re;
                }
            }

            public int? Lastindex
            {
                get
                {
                    _ = "The integer index of the last matched capturing group.";
                    int i = this._Match.Groups.Count - 1;
                    while (i >= 1)
                    {
                        if (this._Match.Groups[i].Success)
                        {
                            return i;
                        }

                        i = i - 1;
                    }

                    return default;
                }
            }

            public string? Lastgroup
            {
                get
                {
                    _ = "The name of the last matched capturing group, or None if unnamed.";
                    int? idx = this.Lastindex;
                    if (idx == null)
                    {
                        return default;
                    }

                    if (this._Re == null)
                    {
                        return default;
                    }

                    int grpIdx = idx.Value;
                    string name = this._Re!.Regex.GroupNameFromNumber(grpIdx);
                    if (name == global::Sharpy.Builtins.Str(grpIdx))
                    {
                        return default;
                    }

                    return name;
                }
            }

            /// <summary>
            /// Access group by index.
            /// </summary>
            public string? this[int n]
            {
                get
                {
                    return this.Group(n);
                }
            }

            /// <summary>
            /// Create a MatchResult wrapping a .NET Match object.
            /// </summary>
            public MatchResult(global::System.Text.RegularExpressions.Match netMatch, string @string, string patternStr, int pos, int endpos, Pattern? compiledPattern = default)
            {
                this._Match = netMatch;
                this._String = @string;
                this._PatternStr = patternStr;
                this._Pos = pos;
                this._Endpos = endpos;
                this._Re = compiledPattern;
            }
        }

        /// <summary>
        /// Apply endpos truncation to a string.
        /// </summary>
        internal static string _ApplyEndpos(string s, int? endpos)
        {
            if (endpos == null)
            {
                return s;
            }

            int ep = endpos.Value;
            if (ep >= s.Length)
            {
                return s;
            }

            return s.Substring(0, ep);
        }

        /// <summary>
        /// Escape special characters in pattern.
        /// </summary>
        internal static string _EscapePattern(string pattern)
        {
            return global::System.Text.RegularExpressions.Regex.Escape(pattern);
        }

        /// <summary>
        /// Compile a regular expression pattern into a Pattern object.
        /// </summary>
        public static Pattern Compile(string pattern, int flags = 0)
        {
            return new Pattern(pattern, flags);
        }

        /// <summary>
        /// Scan through string looking for the first match.
        /// </summary>
        public static MatchResult? Search(string pattern, string s, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Search(s, 0, default);
        }

        /// <summary>
        /// Try to apply the pattern at the start of the string.
        /// </summary>
        public static MatchResult? Match(string pattern, string s, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Match(s, 0, default);
        }

        /// <summary>
        /// Try to apply the pattern to the entire string.
        /// </summary>
        public static MatchResult? Fullmatch(string pattern, string s, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Fullmatch(s, 0, default);
        }

        /// <summary>
        /// Return all non-overlapping matches of pattern in string.
        /// </summary>
        public static Sharpy.List<object> Findall(string pattern, string s, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Findall(s, 0, default);
        }

        /// <summary>
        /// Return a list of match objects over all non-overlapping matches.
        /// </summary>
        public static Sharpy.List<MatchResult> Finditer(string pattern, string s, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Finditer(s, 0, default);
        }

        /// <summary>
        /// Return the string obtained by replacing occurrences.
        /// </summary>
        public static string Sub(string pattern, string repl, string s, int count = 0, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Sub(repl, s, count);
        }

        /// <summary>
        /// Return the string obtained by replacing occurrences using a callable.
        /// </summary>
        public static string Sub(string pattern, global::System.Func<MatchResult, string> repl, string s, int count = 0, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Sub(repl, s, count);
        }

        /// <summary>
        /// Like sub(), but returns (new_string, number_of_subs_made).
        /// </summary>
        public static global::System.ValueTuple<string, int> Subn(string pattern, string repl, string s, int count = 0, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Subn(repl, s, count);
        }

        /// <summary>
        /// Like sub() with callable, but returns (new_string, number_of_subs_made).
        /// </summary>
        public static global::System.ValueTuple<string, int> Subn(string pattern, global::System.Func<MatchResult, string> repl, string s, int count = 0, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Subn(repl, s, count);
        }

        /// <summary>
        /// Split string by the occurrences of pattern.
        /// </summary>
        public static Sharpy.List<string> Split(string pattern, string s, int maxsplit = 0, int flags = 0)
        {
            Pattern p = Compile(pattern, flags);
            return p.Split(s, maxsplit);
        }

        /// <summary>
        /// Clear the regular expression cache (no-op on .NET).
        /// </summary>
        public static void Purge()
        {
            ;
        }

        /// <summary>
        /// Escape special characters in pattern.
        /// </summary>
        public static string Escape(string pattern)
        {
            return _EscapePattern(pattern);
        }
    }
}
