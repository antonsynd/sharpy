namespace Sharpy.Compiler.Lexer;

public partial class Lexer
{
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
