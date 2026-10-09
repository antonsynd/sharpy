using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Lexer;

namespace Sharpy.Lsp;

internal static class IndentationService
{
    /// <summary>
    /// The lexer every indent-only route reads, through <see cref="BuildIndentMap"/> (#2273, owner ruling R-FP):
    /// the compiler's lexer stops at its error budget (<see cref="Compiler.Lexer.Lexer.MaxErrors"/>, 25) and leaves
    /// the rest of the source without a token, so every later line read as a dropped code line that starts a
    /// logical line — a bracket continuation at column 0 then popped every block and Format Document moved a method
    /// body out of its method. The budget caps the compiler's diagnostics, not the editor's structure: this lexer
    /// reads every line, and a dropped code line is only one the lexer's error recovery dropped. The ONE place the
    /// map's lexer is built; the route-parity sweep's mechanism read builds its lexer here too. A pasted file of N
    /// error lines costs N diagnostics in a bag nothing reads — linear, as lexing N clean lines is.
    /// </summary>
    internal static Compiler.Lexer.Lexer StructureLexer(string source) => new(source) { MaxErrors = int.MaxValue };

    /// <summary>
    /// The indent level of every physical line, the tokens it was computed from, and the lexer's
    /// <see cref="Compiler.Lexer.Lexer.LiteralStateUnknown"/>: when true the lexer lost a literal that
    /// can span lines, so which lines are string content (and hence which lines the map may re-indent)
    /// is unknown for the whole document. Also the facts the indent-only check reads
    /// (<c>FormattingFallback.IndentOnlyPreserved</c>, P22e decision 8, #2168), all 1-based lines:
    /// the lines that start a logical line (the block level is defined only there), the lines that
    /// start inside a literal — never re-indented, stripped or blanked: a triple-quoted string's inner
    /// lines, a multi-line replacement field's continuation lines (#2022, #2062; the spans are
    /// <see cref="LiteralSpans"/>, the definition <c>FormatterService</c> uses) — how many indentation diagnostics
    /// (SPY0013, SPY0014) the lexer reported, and <see cref="IndentMap.OpenBracketLine"/>.
    /// <para>
    /// The line after a line the lexer's error recovery dropped starts a logical line (P22h, R-FU, #2279): recovery
    /// emits no <c>Newline</c>, so the token walk resets at each point where recovery resumed
    /// (<see cref="Compiler.Lexer.Lexer.RecoveryResumes"/>) — the line is then a statement the routes repair and the
    /// check judges like any other (P22g read it as a continuation: never a repair target). Not when the dropped line
    /// itself continues (<see cref="Compiler.Lexer.Lexer.RecoveryResumesAfterAContinuedLine"/>): after a trailing
    /// backslash the next line is the user's continuation, which the fallback aligns to its block; after a bracket
    /// left open it lies below <see cref="IndentMap.OpenBracketLine"/> and no route edits it.
    /// </para>
    /// </summary>
    internal static IndentMap BuildIndentMap(string source)
    {
        var lexer = StructureLexer(source);
        List<Token> tokens;
        try
        {
            tokens = lexer.TokenizeAll();
        }
        catch (Exception)
        {
            // no tokens: no literal is known
            return new IndentMap(new Dictionary<int, int>(), new List<Token>(), true, new HashSet<int>(), new HashSet<int>(), 0, null, new Dictionary<int, (int, int)>());
        }

        // Levels come from the WIDTH stack of each logical line's leading whitespace, in physical
        // line order — not from counting the lexer's INDENT/DEDENT tokens. For a document indented
        // with any width other than Lexer.IndentWidth (a 2-space paste: exactly the document the
        // indent-only fallback exists to repair) the lexer reports SPY0013 on each such line and its
        // error recovery drops the line's tokens, so a token-counted map read every line as level 0
        // and the fallback flattened the whole program to column 0. A code line with no tokens (not
        // blank, not a comment, not inside a literal) is therefore a logical line the lexer's error recovery
        // dropped — never a line past an error-budget stop: StructureLexer reads every line (R-FP, #2273).
        // A deeper line opens a level only after a block opener (a logical line ending in ':'),
        // so an unexpected indent stays at the current level. For a valid document this agrees with
        // the lexer: INDENT/DEDENT are emitted exactly where the width stack pushes and pops.
        // The lexer's lines: \r\n, \n and a lone \r each end one (LineDiff.Split, #2168), so sourceLines[line - 1]
        // is the line a token's 1-based Line names.
        var sourceLines = LineDiff.Split(source).Lines;
        var logicalStart = new Dictionary<int, bool>();
        var opensBlock = new Dictionary<int, bool>(); // keyed by the logical line's first line
        var resumes = lexer.RecoveryResumes;
        var resume = 0;
        var partiallyRead = new HashSet<int>(); // logical lines error recovery dropped after reading part of them
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
                case TokenType.Eof:
                    // A dropped line with no token after it (A8: the line after it is dropped whole) is still partially read.
                    for (; resume < resumes.Count && resumes[resume] <= token.Position; resume++)
                    {
                        if (!lexer.RecoveryResumesAfterAContinuedLine.Contains(resumes[resume]) && !atLogicalLineStart && currentStart > 0)
                            partiallyRead.Add(currentStart);
                    }
                    break;
                case TokenType.Indent:
                case TokenType.Dedent:
                    break;
                default:
                    // The first token after a point where error recovery resumed starts a logical line unless the
                    // dropped line continues (R-FU). The dropped line's opensBlock stays unset: whether a line the
                    // lexer read only in part ends in ':' is not known, and no guess is made.
                    for (; resume < resumes.Count && resumes[resume] <= token.Position; resume++)
                    {
                        if (!lexer.RecoveryResumesAfterAContinuedLine.Contains(resumes[resume]))
                        {
                            if (!atLogicalLineStart && currentStart > 0)
                                partiallyRead.Add(currentStart);
                            atLogicalLineStart = true;
                        }
                    }

                    if (!logicalStart.ContainsKey(token.Line))
                        logicalStart[token.Line] = atLogicalLineStart;
                    if (atLogicalLineStart)
                        currentStart = token.Line;
                    atLogicalLineStart = false;
                    previous = token;
                    break;
            }
        }

        var literalLines = LiteralSpans.LinesStartingInside(source, LiteralSpans.Of(tokens, resumes));

        var lineIndent = new Dictionary<int, int>();
        var logicalLineStarts = new HashSet<int>();
        var widths = new List<int> { 0 };
        var previousOpensBlock = false;
        var previousStart = 0;
        var recoveryLines = new Dictionary<int, (int DroppedLine, int DroppedWidth)>();
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
                // The first logical line after a partially read dropped one: the recovery line (R-FU, lead ruling L6).
                if (partiallyRead.Contains(previousStart))
                    recoveryLines[line] = (previousStart, LeadingWhitespaceWidth(sourceLines, previousStart));
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
                previousStart = line;
            }

            // Continuation lines (inside brackets) take the current block's level, as before.
            lineIndent[line] = widths.Count - 1;
        }

        var indentationDiagnostics = lexer.Diagnostics.GetAll().Count(d =>
            d.Code is DiagnosticCodes.Lexer.InvalidIndentation or DiagnosticCodes.Lexer.IndentationMismatch);
        return new IndentMap(lineIndent, tokens, lexer.LiteralStateUnknown, logicalLineStarts, literalLines, indentationDiagnostics,
            lexer.BracketLeftOpenAtLine, recoveryLines);
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

/// <summary>
/// <see cref="IndentationService.BuildIndentMap"/>'s result (lines 1-based). <see cref="OpenBracketLine"/> is the lexer's
/// <see cref="Compiler.Lexer.Lexer.BracketLeftOpenAtLine"/> (P22h, R-FU, #2279): the line of the opener of a bracket the
/// lexer never saw closed — open to the end of the source (<c>xs = [1,</c>), or open at the END of a line its error
/// recovery dropped (<c>if foo("abc):</c>, <c>x = foo($,</c>) — or null. Every line strictly below it is the bracket's
/// to the lexer, whatever the user meant: no indent-only route edits one (the builders return it verbatim, on-type
/// refuses it, and <c>FormattingFallback.IndentOnlyPreserved</c> refuses an applied text that changes it); the lines at
/// or above it keep their repairs. <see cref="RecoveryLines"/>: each recovery line — the first logical line after a line
/// error recovery dropped AFTER reading part of it (a resume that does not continue) — with that dropped logical line's
/// first line and its leading-whitespace width (P22h, lead ruling L6; <see cref="IsRecoveryLineALevelDeeper"/>).
/// </summary>
internal sealed record IndentMap(
    Dictionary<int, int> LineIndent,
    List<Token> Tokens,
    bool LiteralStateUnknown,
    HashSet<int> LogicalLineStarts,
    HashSet<int> LiteralLines,
    int IndentationDiagnostics,
    int? OpenBracketLine,
    IReadOnlyDictionary<int, (int DroppedLine, int DroppedWidth)> RecoveryLines)
{
    /// <summary>Whether no indent-only route may edit the 1-based <paramref name="line"/>: it lies strictly below <see cref="OpenBracketLine"/>.</summary>
    internal bool IsBelowOpenBracket(int line) => OpenBracketLine is { } opener && line > opener;

    /// <summary>
    /// Whether the 1-based <paramref name="line"/>, whose text is <paramref name="text"/>, is a recovery line
    /// (<see cref="RecoveryLines"/>) at least one indentation unit (<see cref="Compiler.Lexer.Lexer.IndentWidth"/>, the
    /// language's 4 spaces — never the editor's tabSize, P22b) deeper than its dropped line: whether the dropped line
    /// opens a block is unknown (<c>    if foo($):</c> / <c>          y = 1</c>, A8), and R-FU forbids the colon guess, so
    /// the line's level is unknown and it is not a repair target (lead ruling L6). Enforced by the check alone
    /// (<c>FormattingFallback.IndentOnlyPreserved</c> clause 6c): a builder skip would also leave the string lines of a
    /// lost literal (X6, C1) unedited and make the literal-loss refusals of those documents unobservable.
    /// </summary>
    internal bool IsRecoveryLineALevelDeeper(int line, string text) =>
        RecoveryLines.TryGetValue(line, out var dropped)
        && text.Length - text.TrimStart(' ', '\t').Length >= dropped.DroppedWidth + Compiler.Lexer.Lexer.IndentWidth;
}
