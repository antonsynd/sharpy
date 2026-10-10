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
            return new IndentMap(new Dictionary<int, int>(), new List<Token>(), true, new HashSet<int>(), new HashSet<int>(), 0, null, null,
                new Dictionary<int, RecoveryLine>(), new HashSet<int>(), new HashSet<int>(), false, new HashSet<int>());
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
        var (sourceLines, sourceBreaks) = LineDiff.Split(source);
        var lineStarts = new int[sourceLines.Count + 1]; // lineStarts[l] is the offset of 1-based line l; [0] unused
        for (var l = 1; l < sourceLines.Count; l++)
            lineStarts[l + 1] = lineStarts[l] + sourceLines[l - 1].Length + sourceBreaks[l - 1].Length;
        var logicalStart = new Dictionary<int, bool>();
        var opensBlock = new Dictionary<int, bool>(); // keyed by the logical line's first line
        var resumes = lexer.RecoveryResumes;
        var resume = 0;
        var partiallyRead = new HashSet<int>(); // logical lines error recovery dropped after reading part of them
        var possibleHeaders = new HashSet<int>(); // those of them that could open a block (lead ruling L8)
        var atLogicalLineStart = true;
        var currentStart = 0;
        Token? previous = null;

        // A resume that does not continue the dropped line, reached while a logical line is being read: that logical
        // line was partially read. Its dropped physical line — the one the resume follows — is the logical line's
        // continuation even when the lexer read no token on it (`for i in range(1,` / `$):`: the abort is the line's
        // first token), so the width loop below must not take it for a dropped line of its own; keyed by the START of the
        // logical line, the recovery-line judgment then finds `partiallyRead` at the line after it (the /verify-implementation
        // round's C1: with the dropped line read as its own logical start, the body below was levelled out of its block).
        void NotePartiallyRead(int resumePosition)
        {
            if (atLogicalLineStart || currentStart <= 0)
                return;
            partiallyRead.Add(currentStart);
            if (lexer.RecoveryResumesAfterAPossibleHeader.Contains(resumePosition))
                possibleHeaders.Add(currentStart);
            var dropped = LineOf(lineStarts, Math.Max(0, resumePosition - 1));
            if (dropped > currentStart && !logicalStart.ContainsKey(dropped))
                logicalStart[dropped] = false;
        }

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
                    // Each non-continued resume ends the logical line being read, as in the token arm below: the next
                    // resume (A8's whole-dropped line) is then no partial read of the SAME line.
                    for (; resume < resumes.Count && resumes[resume] <= token.Position; resume++)
                    {
                        if (!lexer.RecoveryResumesAfterAContinuedLine.Contains(resumes[resume]))
                        {
                            NotePartiallyRead(resumes[resume]);
                            atLogicalLineStart = true;
                        }
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
                            NotePartiallyRead(resumes[resume]);
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
        var recoveryLines = new Dictionary<int, RecoveryLine>();
        var blockOpeners = new HashSet<int>();
        var unplaced = new HashSet<int>();
        var commentLines = new HashSet<int>();
        var otherWhitespace = false;
        int? putativeBodyAbove = null; // the column width bounding the putative body being read (L8, L11): the shallowest
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
                var trimmed = sourceLines[line - 1].Trim(' ', '\t');
                if (trimmed.Length == 0 || literalLines.Contains(line))
                    continue;
                // Trivia to the lexer: no level, and no route edits it (R-GD, #2290) — not a level guessed here.
                if (IsCommentOnlyLine(sourceLines[line - 1]))
                {
                    commentLines.Add(line);
                    continue;
                }

                startsLogicalLine = true; // a code line the lexer's error recovery dropped
                var hash = trimmed.IndexOf('#', StringComparison.Ordinal);
                lineOpensBlock = (hash >= 0 ? trimmed.Substring(0, hash) : trimmed).TrimEnd().EndsWith(":", StringComparison.Ordinal);
            }

            if (startsLogicalLine)
            {
                logicalLineStarts.Add(line);
                if (lineOpensBlock)
                    blockOpeners.Add(line);
                var width = LeadingWhitespaceWidth(sourceLines, line);
                var columns = IndentColumns(sourceLines[line - 1]);
                if (putativeBodyAbove is { } bound && columns <= bound)
                    putativeBodyAbove = null;
                // The first logical line after a partially read dropped one: the recovery line (R-FU). Its level is
                // unknown — it and every later line deeper than the dropped line are a putative body, to the first line
                // at or above the dropped width — when the dropped line could open a block and it is deeper (L8), or when
                // it is a level or more deeper than any other dropped line (L6, L11). A body nested in a body keeps the
                // SHALLOWEST bound (L11): an inner header's width does not end the outer header's body.
                if (partiallyRead.Contains(previousStart))
                {
                    var recovery = new RecoveryLine(previousStart, IndentColumns(sourceLines[previousStart - 1]), possibleHeaders.Contains(previousStart));
                    recoveryLines[line] = recovery;
                    if ((recovery.PossibleHeader && columns > recovery.DroppedColumns)
                        || columns >= recovery.DroppedColumns + Compiler.Lexer.Lexer.IndentWidth)
                    {
                        unplaced.Add(recovery.DroppedLine);
                        putativeBodyAbove = putativeBodyAbove is { } outer ? Math.Min(outer, recovery.DroppedColumns) : recovery.DroppedColumns;
                    }
                }
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
            if (putativeBodyAbove != null && !literalLines.Contains(line))
                unplaced.Add(line);
        }

        otherWhitespace = sourceLines.Any(HasOtherLeadingWhitespace);

        // While a bracket left open freezes a non-blank tail, the opener line is part of the statement the tail
        // continues and is frozen with it (lead ruling L9); an opener on the last line freezes nothing.
        int? frozenFrom = lexer.BracketLeftOpenAtLine is { } opener
            ? sourceLines.Skip(opener).Any(l => l.Trim(' ', '\t').Length > 0) ? opener : opener + 1
            : null;

        var indentationDiagnostics = lexer.Diagnostics.GetAll().Count(d =>
            d.Code is DiagnosticCodes.Lexer.InvalidIndentation or DiagnosticCodes.Lexer.IndentationMismatch);
        return new IndentMap(lineIndent, tokens, lexer.LiteralStateUnknown, logicalLineStarts, literalLines, indentationDiagnostics,
            lexer.BracketLeftOpenAtLine, frozenFrom, recoveryLines, unplaced, blockOpeners, otherWhitespace, commentLines);
    }

    /// <summary>
    /// The columns of <paramref name="text"/>'s leading whitespace, a tab advancing to the next multiple of
    /// <see cref="TabStop"/> as Python's tokenizer reads it: the width the recovery-line rules compare (lead rulings L6,
    /// L8), so two tabs are not two columns (verifier 1b). The lexer refuses tab indentation (SPY0012), so a tab's width
    /// is a convention; the widest one in use keeps a tab-indented body deeper than the space-indented header above it
    /// (<c>    if foo($):</c> / <c>\ty = 1</c>), which refuses rather than repairs. Never the editor's tabSize (P22b).
    /// </summary>
    internal static int IndentColumns(string text)
    {
        var columns = 0;
        foreach (var c in text)
        {
            if (c == ' ')
                columns++;
            else if (c == '\t')
                columns += TabStop - columns % TabStop;
            else
                break;
        }

        return columns;
    }

    /// <summary>
    /// Whether <paramref name="line"/> is a comment-only line: its first character after leading spaces and tabs is
    /// <c>#</c> — the lexer's own test at line start (<c>Lexer.Indentation</c>), which reads such a line as trivia: no
    /// INDENT/DEDENT, no indentation diagnostic, no token. The width loop of <see cref="BuildIndentMap"/> reads it here, so
    /// the map and every reader of comment-only lines share one spelling (R-GD, #2290).
    /// </summary>
    internal static bool IsCommentOnlyLine(string line)
    {
        var trimmed = line.TrimStart(' ', '\t');
        return trimmed.Length > 0 && trimmed[0] == '#';
    }

    /// <summary>
    /// The 1-based line holding the character at <paramref name="position"/>, given <paramref name="lineStarts"/>
    /// (<c>lineStarts[l]</c> the offset of line <c>l</c>, index 0 unused): the last line that starts at or before it, so a
    /// line's own break belongs to it and a recovery's resume (the start of the line after the dropped one) minus one lands
    /// on the dropped line whether or not that line was the source's last.
    /// </summary>
    private static int LineOf(int[] lineStarts, int position)
    {
        var lo = 1;
        var hi = lineStarts.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (lineStarts[mid] <= position)
                lo = mid;
            else
                hi = mid - 1;
        }

        return lo;
    }

    /// <summary>
    /// Whether <paramref name="text"/>'s leading whitespace holds a character other than a space or a tab — a form feed,
    /// a vertical tab, a no-break space: the width functions read only spaces and tabs (the lexer's indentation), while
    /// <c>string.TrimStart()</c> strips every whitespace character, so a re-indent built that way deleted the character
    /// and moved the line (the second verifier's FF, L11). A document with such a line gets no indent-only edit
    /// (<see cref="IndentMap.HasOtherWhitespace"/>, lead ruling L12).
    /// </summary>
    internal static bool HasOtherLeadingWhitespace(string text)
    {
        foreach (var c in text)
        {
            if (c is ' ' or '\t')
                continue;
            return char.IsWhiteSpace(c);
        }

        return false;
    }

    /// <summary>Python's tab stop (<c>tokenize</c>): a tab advances to the next multiple of 8 columns.</summary>
    private const int TabStop = 8;

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
/// A recovery line's dropped line (<see cref="IndentMap.RecoveryLines"/>): its first line, its leading columns
/// (<see cref="IndentationService.IndentColumns"/>), and whether it could open a block — the lexer read a <c>:</c> at
/// bracket depth 0 in it (<see cref="Compiler.Lexer.Lexer.RecoveryResumesAfterAPossibleHeader"/>, lead ruling L8).
/// </summary>
internal readonly record struct RecoveryLine(int DroppedLine, int DroppedColumns, bool PossibleHeader);

/// <summary>
/// <see cref="IndentationService.BuildIndentMap"/>'s result (lines 1-based). <see cref="OpenBracketLine"/> is the lexer's
/// <see cref="Compiler.Lexer.Lexer.BracketLeftOpenAtLine"/> (P22h, R-FU, #2279): the line of the opener of a bracket the
/// lexer never saw closed — open to the end of the source (<c>xs = [1,</c>), or open at the END of a line its error
/// recovery dropped (<c>if foo("abc):</c>, <c>x = foo($,</c>) — or null. Every line from <see cref="FrozenFrom"/> on is the
/// bracket's statement to the lexer, whatever the user meant: no indent-only route edits one (the builders return it
/// verbatim and <c>FormattingFallback.IndentOnlyPreserved</c> refuses an applied text that changes it); the lines above
/// keep their repairs. <see cref="FrozenFrom"/> is the opener line itself while a line below it holds text (lead ruling
/// L9: the opener moves only with its tail) and the line after it otherwise.
/// <see cref="RecoveryLines"/>: each recovery line — the first logical line after a line error recovery dropped AFTER
/// reading part of it (a resume that does not continue) — with its dropped line (<see cref="RecoveryLine"/>).
/// <see cref="UnplacedLines"/>: the lines whose level is unknown, which no route may edit (clause 6c): below a dropped line
/// that could open a block, the recovery line and every later line deeper than that header, with the header itself
/// (lead ruling L8 — the dropped colon's block, whatever width its body has); below any other dropped line, a recovery
/// line one indentation unit or more deeper, with the dropped line (lead ruling L6). Enforced by the check alone: a
/// builder skip would also leave the string lines of a lost literal (X6, C1) unedited and make the literal-loss refusals
/// of those documents unobservable. A putative body runs to the first line at or above the dropped line's width, and a
/// body nested in a body keeps the shallowest bound (L11). <see cref="BlockOpeners"/>: the logical-line starts the map
/// reads as ending in <c>:</c> (clause 6b's landing test). <see cref="HasOtherWhitespace"/>: some line's leading whitespace
/// holds a character other than a space or a tab (<see cref="IndentationService.HasOtherLeadingWhitespace"/>) — the
/// lexer reads it as an unexpected character, so the map has no structure to give that line, and a level it skips or
/// takes misplaces the lines around it (the third verifier's OW-LEVEL: <c>    if c:</c> / <c>\f        y = 1</c> /
/// <c>        z = 2</c> levelled <c>z</c> out of the <c>if</c>). Like <see cref="LiteralStateUnknown"/>, it switches
/// on-type and the indent-only pass off for the whole document (lead ruling L12). <see cref="CommentLines"/>: the comment-only
/// lines (<see cref="IndentationService.IsCommentOnlyLine"/>) that do not start inside a literal — trivia to the lexer, so the
/// map has no level for them (P22i, R-GD, #2290): every indent-only route returns such a line verbatim, byte-identical
/// leading whitespace, and <c>FormattingFallback.IndentOnlyPreserved</c> refuses an applied text that changes one (clause
/// 8). Not in <see cref="UnplacedLines"/> either: a comment in a putative body is held by clause 8, never by 6c.
/// </summary>
internal sealed record IndentMap(
    Dictionary<int, int> LineIndent,
    List<Token> Tokens,
    bool LiteralStateUnknown,
    HashSet<int> LogicalLineStarts,
    HashSet<int> LiteralLines,
    int IndentationDiagnostics,
    int? OpenBracketLine,
    int? FrozenFrom,
    IReadOnlyDictionary<int, RecoveryLine> RecoveryLines,
    IReadOnlySet<int> UnplacedLines,
    IReadOnlySet<int> BlockOpeners,
    bool HasOtherWhitespace,
    IReadOnlySet<int> CommentLines)
{
    /// <summary>Whether no indent-only route may edit the 1-based <paramref name="line"/>: it is at or below <see cref="FrozenFrom"/>.</summary>
    internal bool IsFrozenByOpenBracket(int line) => FrozenFrom is { } from && line >= from;
}
