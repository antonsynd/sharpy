using System;
using System.Text.RegularExpressions;

namespace Sharpy
{
    /// <summary>
    /// A simple HTML parser, similar to Python's html.parser.HTMLParser.
    /// Users subclass this and override the Handle* methods to receive parse events.
    /// </summary>
    [SharpyModuleType("html", "HTMLParser")]
    public class HTMLParser
    {
        private readonly bool _convertCharrefs;
        private string _buffer = "";
        private string? _lastStarttagText;
        private int _line = 1;
        private int _column = 0;

        // CDATA content elements: content is delivered as raw data
        private static readonly System.Collections.Generic.HashSet<string> CdataElements =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "script", "style", "textarea", "title"
            };

        private static readonly Regex EntityRefRegex = new Regex(
            @"&([a-zA-Z][a-zA-Z0-9]*);",
            RegexOptions.Compiled);

        private static readonly Regex CharRefRegex = new Regex(
            @"&#([xX]?[0-9a-fA-F]+);",
            RegexOptions.Compiled);

        /// <summary>
        /// Create a new HTML parser.
        /// </summary>
        /// <param name="convertCharrefs">
        /// When true (default), character references are automatically converted
        /// and delivered via <see cref="HandleData"/> instead of
        /// <see cref="HandleEntityref"/> / <see cref="HandleCharref"/>.
        /// </param>
        public HTMLParser(bool convertCharrefs = true)
        {
            _convertCharrefs = convertCharrefs;
        }

        /// <summary>
        /// Feed some text to the parser. It is processed insofar as it consists
        /// of complete elements; incomplete data is buffered until more data is
        /// fed or <see cref="Close"/> is called.
        /// </summary>
        public void Feed(string data)
        {
            _buffer += data;
            Parse();
        }

        /// <summary>
        /// Force processing of all buffered data. This method may be called when
        /// the end of input is reached. Any remaining data is treated as text data.
        /// </summary>
        public void Close()
        {
            if (_buffer.Length > 0)
            {
                FlushData(_buffer);
                _buffer = "";
            }
        }

        /// <summary>
        /// Reset the parser instance. Loses all unprocessed data.
        /// </summary>
        public void Reset()
        {
            _buffer = "";
            _line = 1;
            _column = 0;
        }

        /// <summary>
        /// Return the current position as a (line, column) tuple.
        /// Line numbers are 1-based, column offsets are 0-based.
        /// </summary>
        public (int, int) Getpos()
        {
            return (_line, _column);
        }

        /// <summary>
        /// Return the text of the most recently opened start tag.
        /// </summary>
        public string? GetStarttagText()
        {
            return _lastStarttagText;
        }

        // ---- Virtual callback methods (users override these) ----

        /// <summary>Called when an opening tag is encountered.</summary>
        /// <param name="tag">The tag name, lowercased.</param>
        /// <param name="attrs">List of (name, value) tuples. Value is null for valueless attributes.</param>
        public virtual void HandleStarttag(string tag, List<(string, string?)> attrs)
        {
        }

        /// <summary>Called when a closing tag is encountered.</summary>
        /// <param name="tag">The tag name, lowercased.</param>
        public virtual void HandleEndtag(string tag)
        {
        }

        /// <summary>
        /// Called when a self-closing tag like &lt;br/&gt; is encountered.
        /// The default implementation calls <see cref="HandleStarttag"/> then <see cref="HandleEndtag"/>.
        /// </summary>
        /// <param name="tag">The tag name, lowercased.</param>
        /// <param name="attrs">List of (name, value) tuples.</param>
        public virtual void HandleStartendtag(string tag, List<(string, string?)> attrs)
        {
            HandleStarttag(tag, attrs);
            HandleEndtag(tag);
        }

        /// <summary>Called when character data is encountered.</summary>
        /// <param name="data">The character data.</param>
        public virtual void HandleData(string data)
        {
        }

        /// <summary>Called when an HTML comment &lt;!-- ... --&gt; is encountered.</summary>
        /// <param name="data">The comment text (without delimiters).</param>
        public virtual void HandleComment(string data)
        {
        }

        /// <summary>
        /// Called when a named character reference like &amp;amp; is encountered
        /// (only when convertCharrefs is false).
        /// </summary>
        /// <param name="name">The entity name (e.g., "amp").</param>
        public virtual void HandleEntityref(string name)
        {
        }

        /// <summary>
        /// Called when a numeric character reference like &amp;#60; or &amp;#x3c; is encountered
        /// (only when convertCharrefs is false).
        /// </summary>
        /// <param name="name">The numeric reference (e.g., "60" or "x3c").</param>
        public virtual void HandleCharref(string name)
        {
        }

        /// <summary>Called when a DOCTYPE declaration is encountered.</summary>
        /// <param name="decl">The declaration text (e.g., "DOCTYPE html").</param>
        public virtual void HandleDecl(string decl)
        {
        }

        /// <summary>Called when a processing instruction like &lt;?...?&gt; is encountered.</summary>
        /// <param name="data">The processing instruction data.</param>
        public virtual void HandlePi(string data)
        {
        }

        // ---- Internal parsing ----

        private void Parse()
        {
            int pos = 0;

            while (pos < _buffer.Length)
            {
                int ltPos = _buffer.IndexOf('<', pos);

                if (ltPos < 0)
                {
                    // No more tags, handle remaining as text/entities
                    HandleTextSegment(_buffer.Substring(pos));
                    pos = _buffer.Length;
                    break;
                }

                // Handle text before the '<'
                if (ltPos > pos)
                {
                    HandleTextSegment(_buffer.Substring(pos, ltPos - pos));
                }

                // Try to parse from '<'
                int consumed = TryParseTag(ltPos);
                if (consumed > 0)
                {
                    AdvancePosition(_buffer.Substring(ltPos, consumed));
                    pos = ltPos + consumed;
                }
                else
                {
                    // Incomplete tag at end of buffer -- wait for more data
                    if (ltPos == _buffer.Length - 1)
                    {
                        // Just a bare '<' at the end -- buffer it
                        _buffer = _buffer.Substring(ltPos);
                        return;
                    }

                    // Check if this is definitely not a valid tag start
                    char next = _buffer[ltPos + 1];
                    if (next != '/' && next != '!' && next != '?' && !char.IsLetter(next))
                    {
                        // Bare '<' -- deliver as data
                        FlushData("<");
                        AdvancePosition("<");
                        pos = ltPos + 1;
                    }
                    else
                    {
                        // Possibly incomplete -- buffer the rest
                        _buffer = _buffer.Substring(ltPos);
                        return;
                    }
                }
            }

            _buffer = "";
        }

        private void HandleTextSegment(string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            if (_convertCharrefs)
            {
                // Convert entity/char refs and deliver as data
                string decoded = System.Net.WebUtility.HtmlDecode(text);
                FlushData(decoded);
            }
            else
            {
                // Deliver entity refs and char refs via callbacks
                int pos = 0;
                while (pos < text.Length)
                {
                    int ampPos = text.IndexOf('&', pos);
                    if (ampPos < 0)
                    {
                        // No more entities
                        string remaining = text.Substring(pos);
                        if (remaining.Length > 0)
                        {
                            FlushData(remaining);
                        }
                        break;
                    }

                    // Text before the &
                    if (ampPos > pos)
                    {
                        FlushData(text.Substring(pos, ampPos - pos));
                    }

                    // Try entity ref
                    var entityMatch = EntityRefRegex.Match(text, ampPos);
                    if (entityMatch.Success && entityMatch.Index == ampPos)
                    {
                        HandleEntityref(entityMatch.Groups[1].Value);
                        pos = ampPos + entityMatch.Length;
                        continue;
                    }

                    // Try char ref
                    var charMatch = CharRefRegex.Match(text, ampPos);
                    if (charMatch.Success && charMatch.Index == ampPos)
                    {
                        HandleCharref(charMatch.Groups[1].Value);
                        pos = ampPos + charMatch.Length;
                        continue;
                    }

                    // Bare '&' -- deliver as data
                    FlushData("&");
                    pos = ampPos + 1;
                }
            }

            // Advance position for the entire text segment
            AdvancePosition(text);
        }

        private int TryParseTag(int start)
        {
            if (start + 1 >= _buffer.Length)
            {
                return 0; // Need more data
            }

            char next = _buffer[start + 1];

            // Comment: <!--
            if (next == '!' && start + 3 < _buffer.Length &&
                _buffer[start + 2] == '-' && _buffer[start + 3] == '-')
            {
                return TryParseComment(start);
            }

            // DOCTYPE or other declaration: <!...>
            if (next == '!' && start + 2 < _buffer.Length && char.IsLetter(_buffer[start + 2]))
            {
                return TryParseDecl(start);
            }

            // Processing instruction: <?...?>
            if (next == '?')
            {
                return TryParsePi(start);
            }

            // End tag: </tag>
            if (next == '/')
            {
                return TryParseEndTag(start);
            }

            // Start tag: <tag ...>
            if (char.IsLetter(next))
            {
                return TryParseStartTag(start);
            }

            return 0;
        }

        private int TryParseComment(int start)
        {
            // start points to '<' of '<!--'
            int searchStart = start + 4;
            int endIdx = _buffer.IndexOf("-->", searchStart, StringComparison.Ordinal);
            if (endIdx < 0)
            {
                return 0; // Incomplete
            }

            string commentText = _buffer.Substring(start + 4, endIdx - start - 4);
            HandleComment(commentText);
            return endIdx + 3 - start;
        }

        private int TryParseDecl(int start)
        {
            // <!DOCTYPE html> or similar
            int endIdx = _buffer.IndexOf('>', start + 2);
            if (endIdx < 0)
            {
                return 0; // Incomplete
            }

            string declText = _buffer.Substring(start + 2, endIdx - start - 2);
            HandleDecl(declText);
            return endIdx + 1 - start;
        }

        private int TryParsePi(int start)
        {
            // <?...?>
            int endIdx = _buffer.IndexOf("?>", start + 2, StringComparison.Ordinal);
            if (endIdx < 0)
            {
                // Fallback: look for just '>'
                endIdx = _buffer.IndexOf('>', start + 2);
                if (endIdx < 0)
                {
                    return 0; // Incomplete
                }
                string piTextFallback = _buffer.Substring(start + 2, endIdx - start - 2);
                HandlePi(piTextFallback);
                return endIdx + 1 - start;
            }

            string piText = _buffer.Substring(start + 2, endIdx - start - 2);
            HandlePi(piText);
            return endIdx + 2 - start;
        }

        private int TryParseEndTag(int start)
        {
            // </tag>
            int endIdx = _buffer.IndexOf('>', start + 2);
            if (endIdx < 0)
            {
                return 0; // Incomplete
            }

            string tagName = _buffer.Substring(start + 2, endIdx - start - 2).Trim().ToLowerInvariant();
            if (tagName.Length > 0)
            {
                HandleEndtag(tagName);
            }
            return endIdx + 1 - start;
        }

        private int TryParseStartTag(int start)
        {
            // Find the end of the tag
            int pos = start + 1;

            // Read tag name
            int nameStart = pos;
            while (pos < _buffer.Length && (char.IsLetterOrDigit(_buffer[pos]) || _buffer[pos] == '-' || _buffer[pos] == ':'))
            {
                pos++;
            }
            if (pos >= _buffer.Length)
            {
                return 0; // Incomplete
            }

            string tagName = _buffer.Substring(nameStart, pos - nameStart).ToLowerInvariant();

            // Parse attributes
            var attrs = new List<(string, string?)>();
            while (pos < _buffer.Length)
            {
                // Skip whitespace
                while (pos < _buffer.Length && char.IsWhiteSpace(_buffer[pos]))
                {
                    pos++;
                }

                if (pos >= _buffer.Length)
                {
                    return 0; // Incomplete
                }

                // Check for end of tag
                if (_buffer[pos] == '>')
                {
                    // Check for self-closing: previous non-whitespace is '/'
                    bool selfClosing = false;
                    int checkPos = pos - 1;
                    while (checkPos > start && char.IsWhiteSpace(_buffer[checkPos]))
                    {
                        checkPos--;
                    }
                    if (checkPos > start && _buffer[checkPos] == '/')
                    {
                        selfClosing = true;
                    }

                    int totalLen = pos + 1 - start;

                    if (selfClosing)
                    {
                        _lastStarttagText = _buffer.Substring(start, totalLen);
                        HandleStartendtag(tagName, attrs);
                    }
                    else
                    {
                        _lastStarttagText = _buffer.Substring(start, totalLen);
                        HandleStarttag(tagName, attrs);
                        // If CDATA element, consume raw content until matching end tag
                        if (CdataElements.Contains(tagName))
                        {
                            int afterTag = start + totalLen;
                            int cdataConsumed = TryCdataContent(tagName, afterTag);
                            if (cdataConsumed > 0)
                            {
                                totalLen += cdataConsumed;
                            }
                        }
                    }

                    return totalLen;
                }

                if (_buffer[pos] == '/')
                {
                    // Self-closing slash -- skip, the '>' handler above will detect it
                    pos++;
                    continue;
                }

                // Parse attribute name
                if (!char.IsLetter(_buffer[pos]) && _buffer[pos] != '_')
                {
                    // Not an attribute, skip unknown char
                    pos++;
                    continue;
                }

                int attrNameStart = pos;
                while (pos < _buffer.Length && (char.IsLetterOrDigit(_buffer[pos]) ||
                       _buffer[pos] == '-' || _buffer[pos] == '_' || _buffer[pos] == '.' || _buffer[pos] == ':'))
                {
                    pos++;
                }
                if (pos >= _buffer.Length)
                {
                    return 0; // Incomplete
                }

                string attrName = _buffer.Substring(attrNameStart, pos - attrNameStart).ToLowerInvariant();

                // Skip whitespace
                while (pos < _buffer.Length && char.IsWhiteSpace(_buffer[pos]))
                {
                    pos++;
                }
                if (pos >= _buffer.Length)
                {
                    return 0; // Incomplete
                }

                // Check for '='
                if (_buffer[pos] != '=')
                {
                    // Valueless attribute (e.g., "disabled")
                    attrs.Add((attrName, null));
                    continue;
                }

                pos++; // Skip '='

                // Skip whitespace
                while (pos < _buffer.Length && char.IsWhiteSpace(_buffer[pos]))
                {
                    pos++;
                }
                if (pos >= _buffer.Length)
                {
                    return 0; // Incomplete
                }

                // Parse attribute value
                string? attrValue;
                if (_buffer[pos] == '"')
                {
                    pos++; // Skip opening quote
                    int valueStart = pos;
                    int endQuote = _buffer.IndexOf('"', pos);
                    if (endQuote < 0)
                    {
                        return 0; // Incomplete
                    }
                    attrValue = _buffer.Substring(valueStart, endQuote - valueStart);
                    pos = endQuote + 1;
                }
                else if (_buffer[pos] == '\'')
                {
                    pos++; // Skip opening quote
                    int valueStart = pos;
                    int endQuote = _buffer.IndexOf('\'', pos);
                    if (endQuote < 0)
                    {
                        return 0; // Incomplete
                    }
                    attrValue = _buffer.Substring(valueStart, endQuote - valueStart);
                    pos = endQuote + 1;
                }
                else
                {
                    // Unquoted value
                    int valueStart = pos;
                    while (pos < _buffer.Length && !char.IsWhiteSpace(_buffer[pos]) &&
                           _buffer[pos] != '>' && _buffer[pos] != '/')
                    {
                        pos++;
                    }
                    if (pos >= _buffer.Length)
                    {
                        return 0; // Incomplete
                    }
                    attrValue = _buffer.Substring(valueStart, pos - valueStart);
                }

                attrs.Add((attrName, attrValue));
            }

            return 0; // Incomplete
        }

        private int TryCdataContent(string tagName, int start)
        {
            // Look for the closing tag (case-insensitive)
            int searchPos = start;

            while (searchPos < _buffer.Length)
            {
                int idx = _buffer.IndexOf("</", searchPos, StringComparison.Ordinal);
                if (idx < 0)
                {
                    return 0; // Incomplete -- need more data
                }

                // Check if this is our closing tag
                int afterSlash = idx + 2;
                int remaining = _buffer.Length - afterSlash;
                if (remaining < tagName.Length)
                {
                    return 0; // Incomplete
                }

                string candidate = _buffer.Substring(afterSlash, tagName.Length);
                if (string.Equals(candidate, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    // Check for '>' after tag name
                    int afterName = afterSlash + tagName.Length;
                    // Skip whitespace
                    while (afterName < _buffer.Length && char.IsWhiteSpace(_buffer[afterName]))
                    {
                        afterName++;
                    }
                    if (afterName >= _buffer.Length)
                    {
                        return 0; // Incomplete
                    }
                    if (_buffer[afterName] == '>')
                    {
                        // Deliver the raw content
                        string content = _buffer.Substring(start, idx - start);
                        if (content.Length > 0)
                        {
                            FlushData(content);
                        }
                        // Deliver the end tag
                        HandleEndtag(tagName);
                        return afterName + 1 - start;
                    }
                }

                searchPos = idx + 2;
            }

            return 0; // Incomplete
        }

        private void FlushData(string data)
        {
            if (data.Length > 0)
            {
                HandleData(data);
            }
        }

        private void AdvancePosition(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    _line++;
                    _column = 0;
                }
                else
                {
                    _column++;
                }
            }
        }
    }
}
