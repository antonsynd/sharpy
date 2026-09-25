namespace Sharpy.Compiler.Lexer;

/// <summary>
/// Where literal text lies in a lexed source (#2062): the source span of every string-literal token
/// and of every outermost f-/t-string (<c>FStringStart</c> … <c>FStringEnd</c>, holes included — a
/// multi-line hole's whitespace is a t-string's <c>Interpolation.expression</c>). A formatter must not
/// rewrite any byte inside one: no trailing-whitespace strip, no re-indent, no blank-line collapse.
/// Shared by <c>FormatterService</c> and the LSP indent-only fallback so the rule has one definition.
/// </summary>
internal static class LiteralSpans
{
    /// <summary>The [start, end) spans, in source order, non-overlapping.</summary>
    public static List<(int Start, int End)> Of(IReadOnlyList<Token> tokens)
    {
        var spans = new List<(int, int)>();
        var fstringDepth = 0;
        var fstringStart = -1;
        foreach (var token in tokens)
        {
            if (token.Position < 0)
                continue;
            switch (token.Type)
            {
                // An f-string never contains a Newline token (in-hole newlines are trivia); one here
                // means error recovery abandoned an unterminated f-string.
                case TokenType.Newline or TokenType.Eof when fstringDepth > 0:
                    fstringDepth = 0;
                    break;
                case TokenType.FStringStart:
                    if (fstringDepth++ == 0)
                        fstringStart = token.Position;
                    break;
                case TokenType.FStringEnd when fstringDepth > 0:
                    if (--fstringDepth == 0)
                        spans.Add((fstringStart, token.Position + token.Length));
                    break;
                case TokenType.String or TokenType.RawString or TokenType.ByteString when fstringDepth == 0:
                    spans.Add((token.Position, token.Position + token.Length));
                    break;
            }
        }
        return spans;
    }

    /// <summary>
    /// The 1-based numbers of the lines whose first character lies strictly inside a span — the lines
    /// whose leading whitespace is literal content, and whose preceding line's trailing whitespace is
    /// too. <c>\r\n</c>, <c>\n</c> and a lone <c>\r</c> each end a line, as in the lexer.
    /// </summary>
    public static HashSet<int> LinesStartingInside(string source, IReadOnlyList<(int Start, int End)> spans)
    {
        var lines = new HashSet<int>();
        if (spans.Count == 0)
            return lines;

        var span = 0;
        var line = 1;
        for (int i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c != '\n' && c != '\r')
                continue;
            if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                i++;
            line++;
            var lineStart = i + 1;
            while (span < spans.Count && spans[span].End <= lineStart)
                span++;
            if (span == spans.Count)
                break;
            if (spans[span].Start < lineStart)
                lines.Add(line);
        }
        return lines;
    }
}
