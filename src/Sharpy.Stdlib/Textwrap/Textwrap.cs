// Generated from src/Sharpy.Stdlib/spy/textwrap.spy — do not edit directly.
// To regenerate: sharpyc emit csharp src/Sharpy.Stdlib/spy/textwrap.spy -t library -n Sharpy
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::Sharpy;

namespace Sharpy
{
    public static partial class Textwrap
    {
        public static Sharpy.List<string> Wrap(string text, int width = 70)
        {
            if (text == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            Sharpy.List<string> result = new Sharpy.List<string>()
            {
            };
            string collapsed = _CollapseWhitespace(text);
            if (collapsed.Length == 0)
            {
                return result;
            }

            Sharpy.List<string> words = collapsed.Split(" ");
            global::System.Text.StringBuilder currentLine = new global::System.Text.StringBuilder();
            foreach (var __loopVar_0 in words)
            {
                var word = __loopVar_0;
                if (word.Length == 0)
                {
                    continue;
                }

                if (word.Length > width)
                {
                    if (currentLine.Length > 0)
                    {
                        result.Append(currentLine.ToString());
                        currentLine.Clear();
                    }

                    int offset = 0;
                    while (offset < word.Length)
                    {
                        int chunkSize = global::System.Math.Min(width, word.Length - offset);
                        result.Append(word.Substring(offset, chunkSize));
                        offset = offset + chunkSize;
                    }
                }
                else if (currentLine.Length == 0)
                {
                    currentLine.Append(word);
                }
                else if (currentLine.Length + 1 + word.Length <= width)
                {
                    currentLine.Append(" ");
                    currentLine.Append(word);
                }
                else
                {
                    result.Append(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
            {
                result.Append(currentLine.ToString());
            }

            return result;
        }

        public static string Fill(string text, int width = 70)
        {
            Sharpy.List<string> lines = Wrap(text, width);
            return "\n".Join(lines);
        }

        public static string Dedent(string text)
        {
            if (text == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            Sharpy.List<string> lines = text.Split("\n");
            string commonPrefix = "";
            bool hasPrefix = false;
            foreach (var __loopVar_1 in lines)
            {
                var line = __loopVar_1;
                if (line.Length == 0 || _IsWhitespaceOnly(line))
                {
                    continue;
                }

                string leadingWs = _GetLeadingWhitespace(line);
                if (!hasPrefix)
                {
                    commonPrefix = leadingWs;
                    hasPrefix = true;
                }
                else
                {
                    commonPrefix = _CommonPrefix(commonPrefix, leadingWs);
                }

                if (commonPrefix.Length == 0)
                {
                    break;
                }
            }

            if (!hasPrefix || commonPrefix.Length == 0)
            {
                return text;
            }

            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder();
            int i = 0;
            while (i < global::Sharpy.Builtins.Len(lines))
            {
                if (i > 0)
                {
                    sb.Append("\n");
                }

                if (lines[i].Length == 0)
                {
                    i = i + 1;
                    continue;
                }

                if (lines[i].Startswith(commonPrefix))
                {
                    sb.Append(lines[i].Substring(commonPrefix.Length));
                }
                else
                {
                    sb.Append(lines[i].TrimStart());
                }

                i = i + 1;
            }

            return sb.ToString();
        }

        public static string Indent(string text, string prefix)
        {
            if (text == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            if (prefix == null)
            {
                throw new global::Sharpy.TypeError("prefix must be str, not NoneType");
            }

            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder();
            Sharpy.List<string> lines = _SplitKeepEnds(text);
            foreach (var __loopVar_2 in lines)
            {
                var line = __loopVar_2;
                if (!_IsWhitespaceOnly(line))
                {
                    sb.Append(prefix);
                }

                sb.Append(line);
            }

            return sb.ToString();
        }

        public static string Shorten(string text, int width)
        {
            if (text == null)
            {
                throw new global::Sharpy.TypeError("argument must be str, not NoneType");
            }

            string placeholder = " [...]";
            string collapsed = _CollapseWhitespace(text);
            if (collapsed.Length <= width)
            {
                return collapsed;
            }

            if (width < global::Sharpy.Builtins.Len(placeholder.TrimStart()))
            {
                throw new global::Sharpy.ValueError("placeholder too large for max width");
            }

            int maxContent = width - placeholder.Length;
            if (maxContent <= 0)
            {
                return placeholder.TrimStart().Substring(0, width);
            }

            int breakAt = collapsed.LastIndexOf(" ", maxContent);
            if (breakAt <= 0)
            {
                return collapsed.Substring(0, maxContent) + placeholder;
            }

            return collapsed.Substring(0, breakAt) + placeholder;
        }

        public static string _CollapseWhitespace(string text)
        {
            global::System.Text.StringBuilder sb = new global::System.Text.StringBuilder(text.Length);
            bool inWhitespace = false;
            bool hasContent = false;
            foreach (var __loopVar_3 in global::Sharpy.StringHelpers.Iterate(text))
            {
                var c = __loopVar_3;
                if (c.Isspace())
                {
                    if (hasContent)
                    {
                        inWhitespace = true;
                    }
                }
                else
                {
                    if (inWhitespace)
                    {
                        sb.Append(" ");
                        inWhitespace = false;
                    }

                    sb.Append(c);
                    hasContent = true;
                }
            }

            return sb.ToString();
        }

        public static bool _IsWhitespaceOnly(string line)
        {
            foreach (var __loopVar_4 in global::Sharpy.StringHelpers.Iterate(line))
            {
                var c = __loopVar_4;
                if (!c.Isspace())
                {
                    return false;
                }
            }

            return true;
        }

        public static string _GetLeadingWhitespace(string line)
        {
            int i = 0;
            while (i < line.Length && global::Sharpy.StringHelpers.GetItem(line, i).Isspace())
            {
                i = i + 1;
            }

            return line.Substring(0, i);
        }

        public static string _CommonPrefix(string a, string b)
        {
            int minLen = global::System.Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < minLen && global::Sharpy.StringHelpers.GetItem(a, i) == global::Sharpy.StringHelpers.GetItem(b, i))
            {
                i = i + 1;
            }

            return a.Substring(0, i);
        }

        public static Sharpy.List<string> _SplitKeepEnds(string text)
        {
            Sharpy.List<string> lines = new Sharpy.List<string>()
            {
            };
            int start = 0;
            int i = 0;
            while (i < text.Length)
            {
                if (global::Sharpy.StringHelpers.GetItem(text, i) == "\n")
                {
                    lines.Append(text.Substring(start, i - start + 1));
                    start = i + 1;
                }
                else if (global::Sharpy.StringHelpers.GetItem(text, i) == "\r")
                {
                    if (i + 1 < text.Length && global::Sharpy.StringHelpers.GetItem(text, i + 1) == "\n")
                    {
                        lines.Append(text.Substring(start, i - start + 2));
                        start = i + 2;
                        i = i + 1;
                    }
                    else
                    {
                        lines.Append(text.Substring(start, i - start + 1));
                        start = i + 1;
                    }
                }

                i = i + 1;
            }

            if (start < text.Length)
            {
                lines.Append(text.Substring(start));
            }

            return lines;
        }
    }
}
