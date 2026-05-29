using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// An event-driven HTML parser, similar to Python's html.parser.HTMLParser.
    /// Subclass this and override the handle_* methods to process HTML content.
    /// </summary>
    [SharpyModuleType("html")]
    public class HTMLParser
    {
        private string _rawdata = string.Empty;
        private int _pos;

        // Regex patterns for HTML parsing
        private static readonly Regex TagOpenRegex = new Regex(
            @"<([a-zA-Z][a-zA-Z0-9]*)",
            RegexOptions.Compiled);

        private static readonly Regex AttrRegex = new Regex(
            @"\s+([a-zA-Z_][\w\-.]*)(?:\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]*)))?",
            RegexOptions.Compiled);

        private static readonly Regex CommentRegex = new Regex(
            @"<!--(.*?)-->",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex EndTagRegex = new Regex(
            @"</([a-zA-Z][a-zA-Z0-9]*)\s*>",
            RegexOptions.Compiled);

        private static readonly Regex DoctypeRegex = new Regex(
            @"<!DOCTYPE\s+([^>]*)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex StartTagRegex = new Regex(
            @"<([a-zA-Z][a-zA-Z0-9]*)((?:\s+[a-zA-Z_][\w\-.]*(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'>]*))?)*)\s*(/?)>",
            RegexOptions.Compiled);

        /// <summary>
        /// Feed some text to the parser. It is processed insofar as it consists of
        /// complete elements; incomplete data is buffered until more data is fed or
        /// <see cref="Close"/> is called.
        /// </summary>
        /// <param name="data">The HTML text to parse.</param>
        public void Feed(string data)
        {
            _rawdata += data;
            GoAhead();
        }

        /// <summary>
        /// Force processing of all buffered data as if it were followed by an end-of-file mark.
        /// </summary>
        public void Close()
        {
            GoAhead();
            _rawdata = string.Empty;
            _pos = 0;
        }

        /// <summary>Reset the instance. Loses all unprocessed data.</summary>
        public void Reset()
        {
            _rawdata = string.Empty;
            _pos = 0;
        }

        private void GoAhead()
        {
            int i = _pos;
            int n = _rawdata.Length;

            while (i < n)
            {
                int ltPos = _rawdata.IndexOf('<', i);

                if (ltPos < 0)
                {
                    // No more tags; rest is data
                    if (i < n)
                    {
                        HandleData(_rawdata.Substring(i, n - i));
                    }
                    i = n;
                    break;
                }

                // Handle text before the tag
                if (ltPos > i)
                {
                    HandleData(_rawdata.Substring(i, ltPos - i));
                }

                i = ltPos;

                // Try to match comment
                if (i + 4 <= n && _rawdata.Substring(i, 4) == "<!--")
                {
                    var commentMatch = CommentRegex.Match(_rawdata, i);
                    if (commentMatch.Success && commentMatch.Index == i)
                    {
                        HandleComment(commentMatch.Groups[1].Value);
                        i = commentMatch.Index + commentMatch.Length;
                        continue;
                    }
                    // Incomplete comment, treat as data
                    HandleData("<");
                    i++;
                    continue;
                }

                // Try to match DOCTYPE
                if (i + 2 <= n && _rawdata.Substring(i, 2) == "<!")
                {
                    var doctypeMatch = DoctypeRegex.Match(_rawdata, i);
                    if (doctypeMatch.Success && doctypeMatch.Index == i)
                    {
                        HandleDecl(doctypeMatch.Groups[1].Value);
                        i = doctypeMatch.Index + doctypeMatch.Length;
                        continue;
                    }
                }

                // Try to match end tag
                if (i + 2 <= n && _rawdata.Substring(i, 2) == "</")
                {
                    var endMatch = EndTagRegex.Match(_rawdata, i);
                    if (endMatch.Success && endMatch.Index == i)
                    {
                        HandleEndtag(endMatch.Groups[1].Value.ToLowerInvariant());
                        i = endMatch.Index + endMatch.Length;
                        continue;
                    }
                    // Malformed end tag, treat as data
                    HandleData("<");
                    i++;
                    continue;
                }

                // Try to match start tag
                var startMatch = StartTagRegex.Match(_rawdata, i);
                if (startMatch.Success && startMatch.Index == i)
                {
                    string tag = startMatch.Groups[1].Value.ToLowerInvariant();
                    string attrString = startMatch.Groups[2].Value;
                    bool selfClosing = startMatch.Groups[3].Value == "/";

                    var attrs = ParseAttrs(attrString);
                    HandleStarttag(tag, attrs);

                    if (selfClosing)
                    {
                        HandleStarttagEnd(tag);
                    }

                    i = startMatch.Index + startMatch.Length;

                    // Handle raw text elements (script, style)
                    if (!selfClosing && (tag == "script" || tag == "style"))
                    {
                        int endIdx = _rawdata.IndexOf(
                            "</" + tag, i, StringComparison.OrdinalIgnoreCase);
                        if (endIdx >= 0)
                        {
                            string content = _rawdata.Substring(i, endIdx - i);
                            HandleData(content);
                            i = endIdx;
                        }
                    }

                    continue;
                }

                // No match — treat '<' as data
                HandleData("<");
                i++;
            }

            _pos = 0;
            _rawdata = string.Empty;
        }

        private static List<(string, string?)> ParseAttrs(string attrString)
        {
            var attrs = new List<(string, string?)>();
            if (string.IsNullOrWhiteSpace(attrString))
            {
                return attrs;
            }

            var matches = AttrRegex.Matches(attrString);
            foreach (Match m in matches)
            {
                string name = m.Groups[1].Value.ToLowerInvariant();
                string? value = null;

                if (m.Groups[2].Success)
                    value = m.Groups[2].Value;
                else if (m.Groups[3].Success)
                    value = m.Groups[3].Value;
                else if (m.Groups[4].Success)
                    value = m.Groups[4].Value;

                attrs.Add((name, value));
            }

            return attrs;
        }

        /// <summary>
        /// Called when a start tag is encountered. Override to handle.
        /// </summary>
        /// <param name="tag">The tag name (lowercased).</param>
        /// <param name="attrs">List of (name, value) tuples for the tag attributes.</param>
        public virtual void HandleStarttag(string tag, List<(string, string?)> attrs)
        {
        }

        /// <summary>
        /// Called when the end of a self-closing start tag is encountered (e.g., &lt;br/&gt;).
        /// Override to handle.
        /// </summary>
        /// <param name="tag">The tag name (lowercased).</param>
        public virtual void HandleStarttagEnd(string tag)
        {
        }

        /// <summary>
        /// Called when an end tag is encountered. Override to handle.
        /// </summary>
        /// <param name="tag">The tag name (lowercased).</param>
        public virtual void HandleEndtag(string tag)
        {
        }

        /// <summary>
        /// Called when character data (text) is encountered. Override to handle.
        /// </summary>
        /// <param name="data">The text content.</param>
        public virtual void HandleData(string data)
        {
        }

        /// <summary>
        /// Called when an HTML comment is encountered. Override to handle.
        /// </summary>
        /// <param name="data">The comment text (without &lt;!-- and --&gt;).</param>
        public virtual void HandleComment(string data)
        {
        }

        /// <summary>
        /// Called when a declaration (e.g., DOCTYPE) is encountered. Override to handle.
        /// </summary>
        /// <param name="decl">The declaration content.</param>
        public virtual void HandleDecl(string decl)
        {
        }
    }
}
