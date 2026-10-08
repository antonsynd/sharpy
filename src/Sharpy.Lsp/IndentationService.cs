using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;

namespace Sharpy.Lsp;

internal static class IndentationService
{
    /// <summary>
    /// The indent level of every physical line, the tokens it was computed from, and the lexer's
    /// <see cref="Compiler.Lexer.Lexer.LiteralStateUnknown"/>: when true the lexer lost a literal that
    /// can span lines, so which lines are string content (and hence which lines the map may re-indent)
    /// is unknown for the whole document. Also the facts the indent-only check reads
    /// (<c>FormattingFallback.IndentOnlyPreserved</c>, P22e decision 8, #2168), all 1-based lines:
    /// the lines that start a logical line (the block level is defined only there), the lines that
    /// start inside a literal — never re-indented, stripped or blanked: a triple-quoted string's inner
    /// lines, a multi-line replacement field's continuation lines (#2022, #2062; the spans are
    /// <see cref="LiteralSpans"/>, the definition <c>FormatterService</c> uses) — and how many indentation diagnostics (SPY0013, SPY0014) the lexer reported.
    /// <c>HiddenAfterRecovery</c>: the lines whose first token follows a point where the lexer's error recovery
    /// resumed (<see cref="Compiler.Lexer.Lexer.RecoveryResumes"/>) and that the token walk read as a continuation —
    /// recovery emits no <c>Newline</c>, so the line after a dropped one never starts a logical line here. The map
    /// leaves them as continuations; the indent-only check judges their block depth too, so a re-indent that moves
    /// one is refused (P22g: an on-type re-indent of the line above an erroring closer line moved the line after it).
    /// </summary>
    internal static (Dictionary<int, int> LineIndent, List<Token> Tokens, bool LiteralStateUnknown,
        HashSet<int> LogicalLineStarts, HashSet<int> LiteralLines, int IndentationDiagnostics, HashSet<int> HiddenAfterRecovery) BuildIndentMap(string source)
    {
        var lexer = new Compiler.Lexer.Lexer(source);
        List<Token> tokens;
        try
        {
            tokens = lexer.TokenizeAll();
        }
        catch (Exception)
        {
            // no tokens: no literal is known
            return (new Dictionary<int, int>(), new List<Token>(), true, new HashSet<int>(), new HashSet<int>(), 0, new HashSet<int>());
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
        // The lexer's lines: \r\n, \n and a lone \r each end one (LineDiff.Split, #2168), so sourceLines[line - 1]
        // is the line a token's 1-based Line names.
        var sourceLines = LineDiff.Split(source).Lines;
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

        var resumes = lexer.RecoveryResumes;
        var literalLines = LiteralSpans.LinesStartingInside(source, LiteralSpans.Of(tokens, resumes));

        var hiddenAfterRecovery = new HashSet<int>();
        var resume = 0;
        foreach (var token in tokens)
        {
            if (resume == resumes.Count)
                break;
            if (token.Position < 0 || token.Type is TokenType.Indent or TokenType.Dedent or TokenType.Newline or TokenType.Eof
                || token.Position < resumes[resume])
                continue;
            while (resume < resumes.Count && resumes[resume] <= token.Position)
                resume++;
            if (logicalStart.TryGetValue(token.Line, out var startsLine) && !startsLine && !literalLines.Contains(token.Line))
                hiddenAfterRecovery.Add(token.Line);
        }
        var lineIndent = new Dictionary<int, int>();
        var logicalLineStarts = new HashSet<int>();
        var widths = new List<int> { 0 };
        var previousOpensBlock = false;
        for (var line = 1; line <= sourceLines.Count; line++)
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
                logicalLineStarts.Add(line);
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

        var indentationDiagnostics = lexer.Diagnostics.GetAll().Count(d =>
            d.Code is DiagnosticCodes.Lexer.InvalidIndentation or DiagnosticCodes.Lexer.IndentationMismatch);
        return (lineIndent, tokens, lexer.LiteralStateUnknown, logicalLineStarts, literalLines, indentationDiagnostics, hiddenAfterRecovery);
    }

    private static int LeadingWhitespaceWidth(IReadOnlyList<string> sourceLines, int line)
    {
        if (line < 1 || line > sourceLines.Count)
            return 0;
        var text = sourceLines[line - 1];
        var width = 0;
        while (width < text.Length && (text[width] == ' ' || text[width] == '\t'))
            width++;
        return width;
    }
}
