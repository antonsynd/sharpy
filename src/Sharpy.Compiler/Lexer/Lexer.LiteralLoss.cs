namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
    private int _lastMultiLineCloseLine = -1;   // see NoteMultiLineLiteralClosed

    /// <summary>
    /// Error recovery is about to skip the rest of the current line (<see cref="RecoverFromError"/>):
    /// <see cref="LiteralLoss.DroppedOpener"/> when that text holds a literal that can span lines. At an
    /// indentation error (SPY0011–SPY0014) <see cref="_position"/> is the line start
    /// (<see cref="HandleLineStartIndentation"/> restores it before measuring), so the span is the whole
    /// line; after a mid-line abort it is the rest of the line (<c>x = $"""</c>). An unterminated short
    /// string aborts on the line break: the span is empty.
    /// </summary>
    private void NoteDroppedSpan()
    {
        var end = _position;
        while (end < _source.Length && !IsLineBreak(_source[end]))
            end++;
        if (HoldsALiteralSpanningLines(_source.AsSpan(_position, end - _position)))
            LiteralLoss |= LiteralLoss.DroppedOpener;
    }

    /// <summary>
    /// Called by the five arms that close a triple-quoted literal (<see cref="ReadTripleQuotedString"/>
    /// for plain and <c>d</c>, <see cref="ReadTripleQuotedByteString"/>, the triple arms of
    /// <see cref="ReadRawString"/> and <see cref="ReadDedentedRawString"/>, and the f-/t-/df-string
    /// <c>FStringEnd</c> triple arm) with <see cref="_position"/> just past the closer and the line the
    /// literal started on. Records the closer's context for <see cref="LiteralLoss.RePairedCloser"/>:
    /// (a) the closer is immediately followed by a quote character — implicit concatenation is not Sharpy
    /// syntax, so this never occurs in a program that parses: the closer re-paired with a quote inside a
    /// short string (<c>t = '"""'</c> below a stray <c>"""</c>); (b) the close line, when the literal
    /// spanned lines, so that <see cref="ReportError"/> can tell an unterminated short string reported on
    /// it (<c>t = '""" '</c>). A literal closed on its own line cannot have lost multi-line content, so
    /// <c>x = """a""" + 'b</c> being typed records nothing (owner ruling 2026-10-07).
    /// </summary>
    private void NoteMultiLineLiteralClosed(int startLine)
    {
        if (startLine < _line)
            _lastMultiLineCloseLine = _line;
        if (_position < _source.Length && _source[_position] is '"' or '\'')
            LiteralLoss |= LiteralLoss.RePairedCloser;
    }

    /// <summary>
    /// Whether <paramref name="text"/> holds a literal that can span lines (#2271, P22f decision 2): the
    /// ONE test behind <see cref="LiteralLoss.DroppedOpener"/> (the text error recovery skips) and
    /// <see cref="LiteralLoss.UnreadRemainder"/> (the source left when the error budget stops the lexer).
    /// Walks the text the way the lexer would read it from its start: a <c>#</c> outside a literal skips to
    /// the end of its line; at a quote character, with the prefix letters immediately before it —
    /// <list type="bullet">
    /// <item>a triple opener whose closer is absent from the text or lies on a later line → true (a closer
    /// on the same line closes it; the walk continues after it);</item>
    /// <item>a single-quoted literal is skipped to its closing quote on the same line; if the line ends
    /// first, an <c>f</c>/<c>t</c>/<c>df</c>-prefixed one → true (a replacement field may span lines),
    /// any other prefix → the walk continues after the line break (a short string ends at its line).</item>
    /// </list>
    /// Backslash escapes are honoured except in <c>r</c>/<c>dr</c> literals, as the lexer reads them. A
    /// short string holding a triple (<c>'"""'</c>) or a closed triple on its line (<c>"""abc"""</c>)
    /// opens nothing that spans lines: a dropped line holding one keeps its repair. Known limits
    /// (#2271): a nested quote inside a replacement field (<c>f"{'"""'}"</c>), and an opener swallowed
    /// by an unterminated short string on its line (<c>x = 'abc """</c> → false).
    /// </summary>
    internal static bool HoldsALiteralSpanningLines(ReadOnlySpan<char> text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '#')
            {
                while (i < text.Length && !IsLineBreak(text[i]))
                    i++;
                continue;
            }
            if (c is not ('"' or '\''))
            {
                i++;
                continue;
            }

            var prefix = PrefixBefore(text, i);
            var raw = prefix is "r" or "dr";
            if (i + 2 < text.Length && text[i + 1] == c && text[i + 2] == c)
            {
                var k = i + 3;
                while (true)
                {
                    if (k >= text.Length || IsLineBreak(text[k]))
                        return true;    // the closer is absent or on a later line
                    if (text[k] == '\\' && !raw)
                    {
                        k += k + 1 < text.Length && !IsLineBreak(text[k + 1]) ? 2 : 1;
                        continue;
                    }
                    if (text[k] == c && k + 2 < text.Length && text[k + 1] == c && text[k + 2] == c)
                        break;
                    k++;
                }
                i = k + 3;
                continue;
            }

            var j = i + 1;
            var closed = false;
            while (j < text.Length && !IsLineBreak(text[j]))
            {
                if (text[j] == '\\' && !raw)
                {
                    j += j + 1 < text.Length && !IsLineBreak(text[j + 1]) ? 2 : 1;
                    continue;
                }
                if (text[j] == c)
                {
                    closed = true;
                    break;
                }
                j++;
            }
            if (closed)
            {
                i = j + 1;
                continue;
            }
            if (prefix is "f" or "t" or "df")
                return true;    // a replacement field left open on its line may span lines
            i = j;              // a short string ends at its line
        }
        return false;
    }

    private static bool IsLineBreak(char c) => c is '\n' or '\r';

    /// <summary>
    /// The string prefix spelled immediately before the quote at <paramref name="quoteAt"/>: the word that
    /// ends there when it is one the lexer recognises (<see cref="StringLiteralPrefixes"/>), else empty — a
    /// longer word (<c>xf"</c>, <c>rf"</c>) is an identifier followed by a plain string.
    /// </summary>
    private static string PrefixBefore(ReadOnlySpan<char> text, int quoteAt)
    {
        var start = quoteAt;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
            start--;
        var word = text[start..quoteAt];
        foreach (var (prefix, _) in StringLiteralStarts)
        {
            if (prefix.Length > 0 && word.SequenceEqual(prefix))
                return prefix;
        }
        return "";
    }
}
