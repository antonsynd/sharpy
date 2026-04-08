using System;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Text wrapping and filling, matching Python's textwrap module.
    /// </summary>
    public static partial class Textwrap
    {
        /// <summary>
        /// Wraps a single paragraph of text and returns a list of wrapped lines.
        /// Whitespace in the input is collapsed, and words longer than
        /// <paramref name="width"/> are broken to fit.
        /// </summary>
        /// <param name="text">The text to wrap.</param>
        /// <param name="width">The maximum line width (default 70).</param>
        /// <returns>A list of wrapped lines without trailing newlines.</returns>
        public static Sharpy.List<Str> Wrap(Str text, int width = 70)
        {
            string textStr = (string)text;
            if (textStr == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            var result = new System.Collections.Generic.List<Str>();

            // Collapse whitespace (including newlines) and trim
            var collapsed = CollapseWhitespace(textStr);
            if (collapsed.Length == 0)
            {
                return new Sharpy.List<Str>(result);
            }

            string[] words = collapsed.Split(' ');
            var currentLine = new StringBuilder();

            foreach (string word in words)
            {
                if (word.Length == 0)
                {
                    continue;
                }

                if (word.Length > width)
                {
                    // If there's content on the current line, flush it first
                    if (currentLine.Length > 0)
                    {
                        result.Add((Str)currentLine.ToString());
                        currentLine.Clear();
                    }

                    // Break long word into chunks
                    int offset = 0;
                    while (offset < word.Length)
                    {
                        int chunkSize = System.Math.Min(width, word.Length - offset);
                        result.Add((Str)word.Substring(offset, chunkSize));
                        offset += chunkSize;
                    }
                }
                else if (currentLine.Length == 0)
                {
                    currentLine.Append(word);
                }
                else if (currentLine.Length + 1 + word.Length <= width)
                {
                    currentLine.Append(' ');
                    currentLine.Append(word);
                }
                else
                {
                    result.Add((Str)currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
            {
                result.Add((Str)currentLine.ToString());
            }

            return new Sharpy.List<Str>(result);
        }

        /// <summary>
        /// Wraps a single paragraph of text and returns a single string
        /// containing the wrapped paragraph. This is shorthand for
        /// "\n".join(wrap(text, width)).
        /// </summary>
        /// <param name="text">The text to fill.</param>
        /// <param name="width">The maximum line width (default 70).</param>
        /// <returns>A single string with line breaks inserted.</returns>
        public static Str Fill(Str text, int width = 70)
        {
            var lines = Wrap(text, width);
            return (Str)string.Join("\n", (System.Collections.Generic.IEnumerable<Str>)lines);
        }

        /// <summary>
        /// Remove any common leading whitespace from all lines in <paramref name="text"/>.
        /// Lines that consist solely of whitespace are treated as if they have
        /// no indentation (they don't affect the common prefix calculation) but
        /// their leading whitespace is still stripped.
        /// </summary>
        /// <param name="text">The text to dedent.</param>
        /// <returns>The dedented text.</returns>
        public static Str Dedent(Str text)
        {
            string textStr = (string)text;
            if (textStr == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            string[] lines = textStr.Split('\n');

            // Find common leading whitespace among non-empty lines
            string? commonPrefix = null;
            foreach (string line in lines)
            {
                if (line.Length == 0 || IsWhitespaceOnly(line))
                {
                    continue;
                }

                string leadingWs = GetLeadingWhitespace(line);
                if (commonPrefix == null)
                {
                    commonPrefix = leadingWs;
                }
                else
                {
                    commonPrefix = CommonPrefix(commonPrefix, leadingWs);
                }

                if (commonPrefix.Length == 0)
                {
                    break;
                }
            }

            if (commonPrefix == null || commonPrefix.Length == 0)
            {
                return text;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }

                if (lines[i].Length == 0)
                {
                    continue;
                }

                if (lines[i].StartsWith(commonPrefix, StringComparison.Ordinal))
                {
                    sb.Append(lines[i].Substring(commonPrefix.Length));
                }
                else
                {
                    // Whitespace-only line shorter than prefix: strip all leading whitespace
                    sb.Append(lines[i].TrimStart());
                }
            }

            return (Str)sb.ToString();
        }

        /// <summary>
        /// Add <paramref name="prefix"/> to the beginning of selected lines in
        /// <paramref name="text"/>. By default, the prefix is added to all lines
        /// that do not consist solely of whitespace (including any line endings).
        /// </summary>
        /// <param name="text">The text to indent.</param>
        /// <param name="prefix">The prefix to add.</param>
        /// <returns>The indented text.</returns>
        public static Str Indent(Str text, Str prefix)
        {
            string textStr = (string)text;
            string prefixStr = (string)prefix;
            if (textStr == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            if (prefixStr == null)
            {
                throw new TypeError("prefix must be str, not NoneType");
            }

            var sb = new StringBuilder();
            string[] lines = SplitKeepEnds(textStr);

            foreach (string line in lines)
            {
                if (!IsWhitespaceOnly(line))
                {
                    sb.Append(prefixStr);
                }

                sb.Append(line);
            }

            return (Str)sb.ToString();
        }

        /// <summary>
        /// Collapse and truncate the given text to fit in the given width.
        /// Whitespace is first collapsed, then the text is truncated to fit
        /// with " [...]" appended as a placeholder.
        /// </summary>
        /// <param name="text">The text to shorten.</param>
        /// <param name="width">The maximum width.</param>
        /// <returns>The shortened text.</returns>
        /// <exception cref="ValueError">Thrown if the placeholder is too large for the width.</exception>
        public static Str Shorten(Str text, int width)
        {
            string textStr = (string)text;
            if (textStr == null)
            {
                throw new TypeError("argument must be str, not NoneType");
            }

            const string placeholder = " [...]";
            string collapsed = CollapseWhitespace(textStr);

            if (collapsed.Length <= width)
            {
                return (Str)collapsed;
            }

            if (width < placeholder.TrimStart().Length)
            {
                throw new ValueError("placeholder too large for max width");
            }

            // Try to break at a word boundary
            int maxContent = width - placeholder.Length;
            if (maxContent <= 0)
            {
                return (Str)placeholder.TrimStart().Substring(0, width);
            }

            // Find last space at or before maxContent
            int breakAt = collapsed.LastIndexOf(' ', maxContent);
            if (breakAt <= 0)
            {
                // No word boundary found, just truncate
                return (Str)(collapsed.Substring(0, maxContent) + placeholder);
            }

            return (Str)(collapsed.Substring(0, breakAt) + placeholder);
        }

        private static string CollapseWhitespace(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool inWhitespace = false;
            bool hasContent = false;

            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
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
                        sb.Append(' ');
                        inWhitespace = false;
                    }

                    sb.Append(c);
                    hasContent = true;
                }
            }

            return sb.ToString();
        }

        private static bool IsWhitespaceOnly(string line)
        {
            foreach (char c in line)
            {
                if (!char.IsWhiteSpace(c))
                {
                    return false;
                }
            }

            return true;
        }

        private static string GetLeadingWhitespace(string line)
        {
            int i = 0;
            while (i < line.Length && char.IsWhiteSpace(line[i]))
            {
                i++;
            }

            return line.Substring(0, i);
        }

        private static string CommonPrefix(string a, string b)
        {
            int minLen = System.Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < minLen && a[i] == b[i])
            {
                i++;
            }

            return a.Substring(0, i);
        }

        private static string[] SplitKeepEnds(string text)
        {
            var lines = new System.Collections.Generic.List<string>();
            int start = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lines.Add(text.Substring(start, i - start + 1));
                    start = i + 1;
                }
                else if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        lines.Add(text.Substring(start, i - start + 2));
                        start = i + 2;
                        i++;
                    }
                    else
                    {
                        lines.Add(text.Substring(start, i - start + 1));
                        start = i + 1;
                    }
                }
            }

            if (start < text.Length)
            {
                lines.Add(text.Substring(start));
            }
            else if (start == text.Length && text.Length > 0)
            {
                // Text ended with newline, don't add empty trailing segment
            }

            return lines.ToArray();
        }
    }
}
