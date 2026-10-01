using Sharpy.Compiler.Lexer;

namespace Sharpy.Lsp;

internal static class IndentationService
{
    internal static (Dictionary<int, int> LineIndent, List<Token> Tokens) BuildIndentMap(string source)
    {
        var lexer = new Compiler.Lexer.Lexer(source);
        List<Token> tokens;
        try
        {
            tokens = lexer.TokenizeAll();
        }
        catch (Exception)
        {
            return (new Dictionary<int, int>(), new List<Token>());
        }

        // Levels come from the WIDTH stack of each logical line's leading whitespace, in physical
        // line order — not from counting the lexer's INDENT/DEDENT tokens. For a document indented
        // with any width other than Lexer.IndentWidth (a 2-space paste: exactly the document the
        // indent-only fallback exists to repair) the lexer reports SPY0013 on each such line and its
        // error recovery drops the line's tokens, so a token-counted map read every line as level 0
        // and the fallback flattened the whole program to column 0. A code line with no tokens (not
        // blank, not a comment, not inside a literal) is therefore a logical line the lexer dropped.
        // A deeper line opens a level only after a block opener (a logical line ending in ':'),
        // so an unexpected indent stays at the current level. For a valid document this agrees with
        // the lexer: INDENT/DEDENT are emitted exactly where the width stack pushes and pops.
        var sourceLines = source.Split('\n');
        var logicalStart = new Dictionary<int, bool>();
        var opensBlock = new Dictionary<int, bool>(); // keyed by the logical line's first line
        var atLogicalLineStart = true;
        var currentStart = 0;
        Token? previous = null;
        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case TokenType.Newline:
                    if (currentStart > 0)
                        opensBlock[currentStart] = previous?.Type == TokenType.Colon;
                    atLogicalLineStart = true;
                    break;
                case TokenType.Indent:
                case TokenType.Dedent:
                case TokenType.Eof:
                    break;
                default:
                    if (!logicalStart.ContainsKey(token.Line))
                        logicalStart[token.Line] = atLogicalLineStart;
                    if (atLogicalLineStart)
                        currentStart = token.Line;
                    atLogicalLineStart = false;
                    previous = token;
                    break;
            }
        }

        var literalLines = LiteralSpans.LinesStartingInside(source, LiteralSpans.Of(tokens));
        var lineIndent = new Dictionary<int, int>();
        var widths = new List<int> { 0 };
        var previousOpensBlock = false;
        for (var line = 1; line <= sourceLines.Length; line++)
        {
            bool startsLogicalLine;
            bool lineOpensBlock;
            if (logicalStart.TryGetValue(line, out var isStart))
            {
                startsLogicalLine = isStart;
                lineOpensBlock = opensBlock.TryGetValue(line, out var opens) && opens;
            }
            else
            {
                var trimmed = sourceLines[line - 1].Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#' || literalLines.Contains(line))
                    continue;
                startsLogicalLine = true; // a code line the lexer's error recovery dropped
                var hash = trimmed.IndexOf('#', StringComparison.Ordinal);
                lineOpensBlock = (hash >= 0 ? trimmed.Substring(0, hash) : trimmed).TrimEnd().EndsWith(":", StringComparison.Ordinal);
            }

            if (startsLogicalLine)
            {
                var width = LeadingWhitespaceWidth(sourceLines, line);
                if (width > widths[^1])
                {
                    if (previousOpensBlock)
                        widths.Add(width);
                }
                else
                {
                    while (widths.Count > 1 && width < widths[^1])
                        widths.RemoveAt(widths.Count - 1);
                    // A dedent to a width no enclosing block used (inconsistent): a block at that
                    // width, rather than snapping to a wrong outer level.
                    if (width > widths[^1])
                        widths.Add(width);
                }
                previousOpensBlock = lineOpensBlock;
            }

            // Continuation lines (inside brackets) take the current block's level, as before.
            lineIndent[line] = widths.Count - 1;
        }

        return (lineIndent, tokens);
    }

    private static int LeadingWhitespaceWidth(string[] sourceLines, int line)
    {
        if (line < 1 || line > sourceLines.Length)
            return 0;
        var text = sourceLines[line - 1];
        var width = 0;
        while (width < text.Length && (text[width] == ' ' || text[width] == '\t'))
            width++;
        return width;
    }

    /// <summary>
    /// The 1-based lines whose text must not be re-indented, stripped or blanked: every line that
    /// starts inside a string literal or an f-/t-string (a triple-quoted string's inner lines, a
    /// multi-line replacement field's continuation lines — re-indenting a hole changes a t-string's
    /// <c>Interpolation.expression</c>, #2022). The spans come from the one shared definition
    /// (<see cref="LiteralSpans"/>, #2062) that <c>FormatterService</c> uses.
    /// </summary>
    internal static HashSet<int> FindMultiLineStringLines(List<Token> tokens, string source) =>
        LiteralSpans.LinesStartingInside(source, LiteralSpans.Of(tokens));
}
