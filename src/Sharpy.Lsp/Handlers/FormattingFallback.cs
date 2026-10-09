using System.Text;
using Sharpy.Compiler.Formatting;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Indent-only formatting fallback used when the document fails to parse
/// (so <see cref="Sharpy.Compiler.Formatting.FormatterService"/> can't run).
/// Builds an indent map via the lexer's INDENT/DEDENT tokens and re-indents
/// each line, preserving multi-line string contents. Its output reaches the client only through
/// <see cref="FormattingEdits.CheckedIndentOnly"/>, which applies <see cref="IndentOnlyPreserved"/>.
/// </summary>
internal static class FormattingFallback
{
    /// <summary>
    /// Re-indents the entire document using the lexer-based indent map, at the language's one
    /// indentation unit (<see cref="Compiler.Lexer.Lexer.IndentWidth"/> spaces — never the editor's
    /// tabSize/insertSpaces, indentation.md). Only leading whitespace changes: each line keeps its own
    /// line break (<see cref="LineDiff.Split"/> — <c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>, as the lexer and the
    /// client count lines), so a CRLF document stays CRLF (#2168). A line strictly below a bracket the lexer never
    /// saw closed (<see cref="IndentMap.OpenBracketLine"/>, R-FU, #2279) is returned verbatim, whitespace-only lines
    /// included. Returns the formatted text; equal to the input if no changes are needed.
    /// </summary>
    internal static string ReindentDocument(string text)
    {
        var indentStr = new string(' ', Compiler.Lexer.Lexer.IndentWidth);

        var map = IndentationService.BuildIndentMap(text);
        var lineIndentLevels = map.LineIndent;
        var multiLineStringLines = map.LiteralLines;

        var (lines, breaks) = LineDiff.Split(text);
        var formatted = new StringBuilder(text.Length);

        for (var i = 0; i < lines.Count; i++)
        {
            formatted.Append(Reindent(lines[i], i + 1)); // tokens use 1-based lines
            if (i < breaks.Count)
                formatted.Append(breaks[i]);
        }

        return formatted.ToString();

        string Reindent(string line, int number)
        {
            // A line that starts inside a string literal is its data — kept verbatim, whitespace-only
            // lines included (#2062).
            if (multiLineStringLines.Contains(number))
                return line;

            // A line below a bracket the lexer never saw closed is the bracket's to the lexer (R-FU, #2279).
            if (map.IsBelowOpenBracket(number))
                return line;

            var trimmed = line.TrimStart();
            if (trimmed.Length == 0)
                return "";

            var level = lineIndentLevels.TryGetValue(number, out var l) ? l : 0;
            return string.Concat(Enumerable.Repeat(indentStr, level)) + trimmed;
        }
    }

    /// <summary>
    /// The check an indent-only edit of an UNPARSEABLE document must pass before it is applied (P22e
    /// decision 8, #2168) — the parseable case is the SPY0912 net's. True iff <paramref name="applied"/>
    /// differs from <paramref name="source"/> only in what indentation means nothing to the lexer:
    /// <list type="number">
    /// <item>the same number of lines, each ending in the same line break;</item>
    /// <item>each line's content after its leading whitespace unchanged (a whitespace-only line may become empty);</item>
    /// <item>the same lines start inside a literal, and each of them is byte-identical;</item>
    /// <item>the indent map's level of every logical-line-start line unchanged — re-indenting ONE line of
    /// an 8-space block moves it out of the block (cell 16);</item>
    /// <item>no more lexer indentation diagnostics (SPY0013, SPY0014) than the source — a dedent between
    /// two widths of the stack keeps its level in the map, which opens a block at the unseen width, while
    /// the lexer reports the line (cell 21). A backstop since clause 6 landed: measured @ 92af80222, every
    /// text this clause refuses clause 6 refuses too (dropped alone, the sweep and every formatting test
    /// stay green), while with clause 6 dropped it still refuses cell 21;</item>
    /// <item>the lexer's block depth (<see cref="BlockDepths"/>) of every logical-line-start line whose
    /// source width is on its enclosing stack unchanged — the map opens a block only after a line ending
    /// in <c>:</c> and compares with itself, so it accepts a property-observer block re-indented one level
    /// too shallow, or a block re-nested to column 0 by triple quotes that re-pair without a lexer error.
    /// A line the lexer drops (<see cref="BlockDepths"/>) is the misindentation the fallback repairs and is exempt.
    /// The line after a line error recovery dropped starts a logical line (<see cref="IndentationService.BuildIndentMap"/>,
    /// R-FU) and is judged here like any other: a re-indent that moves it, or the line after it, a block is refused;</item>
    /// <item>(6b, P22h) every logical-line-start line the applied text EDITS lands on a level of the applied text's
    /// width stack — deeper than its top, or equal to a width on it. Clause 6 exempts a line whose SOURCE width is on no
    /// level, so the line after an error repaired ALONE to 4 spaces under an 8-space body (<c>    y = 1</c> on the
    /// stack <c>[0, 8]</c>) passed it while the lexer, whose stack restarts at <c>[0]</c> after the recovery, reads the
    /// next 8-space line one block deeper. An unchanged line keeps its exemption: it is not this text's doing;</item>
    /// <item>(6c, P22h, lead ruling L6) a recovery line one indentation unit or more deeper than the line error recovery
    /// dropped before it (<see cref="IndentMap.IsRecoveryLineALevelDeeper"/>), and that dropped line, byte-identical:
    /// whether the dropped line opens a block is unknown, so how the two relate is too (A8 <c>    if foo($):</c> /
    /// <c>          y = 1</c>: moving the body to 4 spaces takes it out of the <c>if</c> — clause 6 exempts its SPY0013
    /// width and 4 lands on a level; moving the header of the 8-space twin to 4 alone changes the pair the same way);</item>
    /// <item>(7, P22h, R-FU, #2279) every line strictly below <see cref="IndentMap.OpenBracketLine"/> — a bracket the
    /// lexer never saw closed, so every later line is the bracket's — byte-identical: the builders never edit one, and
    /// a builder that forgets is refused here;</item>
    /// <item>(7b, P22h, lead ruling L5) while such a bracket freezes a tail that holds text, every logical-line-start line
    /// whose source width is on its enclosing level (clause 6's depth ≥ 0) keeps its width: re-indenting the lines above
    /// an opener of an 8-space document to 4 spaces leaves the frozen tail at 8 — one block deeper once the bracket
    /// closes. Only the repairs (lines on no level) move; an opener on the last line freezes nothing.</item>
    /// </list>
    /// Lines are the client's and the lexer's (<see cref="LineDiff.Split"/>: <c>\r\n</c>, <c>\n</c> or a lone
    /// <c>\r</c>), so the text judged is the text the client applies (#2168).
    /// </summary>
    internal static bool IndentOnlyPreserved(string source, string applied)
    {
        var (sourceLines, sourceBreaks) = LineDiff.Split(source);
        var (appliedLines, appliedBreaks) = LineDiff.Split(applied);
        if (sourceLines.Count != appliedLines.Count || !sourceBreaks.SequenceEqual(appliedBreaks, StringComparer.Ordinal))
            return false;

        for (var i = 0; i < sourceLines.Count; i++)
        {
            if (!string.Equals(sourceLines[i].TrimStart(), appliedLines[i].TrimStart(), StringComparison.Ordinal))
                return false;
        }

        var before = IndentationService.BuildIndentMap(source);
        var after = IndentationService.BuildIndentMap(applied);

        // Clause 7: no line below a bracket the lexer never saw closed changes (R-FU).
        for (var line = (before.OpenBracketLine ?? sourceLines.Count) + 1; line <= sourceLines.Count; line++)
        {
            if (!string.Equals(sourceLines[line - 1], appliedLines[line - 1], StringComparison.Ordinal))
                return false;
        }

        // Clause 6c: a recovery line a level or more deeper than its dropped line, and that dropped line, are not edited
        // (lead ruling L6): how the two relate is what is unknown.
        foreach (var (line, (dropped, _)) in before.RecoveryLines)
        {
            if (before.IsRecoveryLineALevelDeeper(line, sourceLines[line - 1])
                && (!string.Equals(sourceLines[line - 1], appliedLines[line - 1], StringComparison.Ordinal)
                    || !string.Equals(sourceLines[dropped - 1], appliedLines[dropped - 1], StringComparison.Ordinal)))
                return false;
        }

        if (!before.LiteralLines.SetEquals(after.LiteralLines))
            return false;
        foreach (var line in before.LiteralLines)
        {
            if (!string.Equals(sourceLines[line - 1], appliedLines[line - 1], StringComparison.Ordinal))
                return false;
        }

        foreach (var line in before.LogicalLineStarts)
        {
            if (!after.LogicalLineStarts.Contains(line)
                || !before.LineIndent.TryGetValue(line, out var level)
                || !after.LineIndent.TryGetValue(line, out var appliedLevel)
                || level != appliedLevel)
                return false;
        }

        if (after.IndentationDiagnostics > before.IndentationDiagnostics)
            return false;

        // Clause 7b applies while a line below an open bracket holds text: an opener on the last line freezes nothing.
        var frozenTail = before.OpenBracketLine is { } opener && sourceLines.Skip(opener).Any(l => l.Trim().Length > 0);
        var appliedDepths = BlockDepths(appliedLines, after.LogicalLineStarts);
        foreach (var (line, depth) in BlockDepths(sourceLines, before.LogicalLineStarts))
        {
            // Clause 6: the depth of a line on its enclosing stack is kept.
            var judged = appliedDepths.TryGetValue(line, out var appliedDepth);
            if (depth >= 0 && (!judged || appliedDepth != depth))
                return false;
            // Clause 7b: while a bracket left open freezes a tail, a line on its level keeps its width (lead ruling L5).
            if (frozenTail && depth >= 0 && !string.Equals(sourceLines[line - 1], appliedLines[line - 1], StringComparison.Ordinal))
                return false;
            // Clause 6b: an edited line lands on a level.
            if (appliedDepth == OnNoLevel && !string.Equals(sourceLines[line - 1], appliedLines[line - 1], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>A <see cref="BlockDepths"/> value: a width that is a multiple of <see cref="Compiler.Lexer.Lexer.IndentWidth"/>, tab-free, on no level of the stack (SPY0014).</summary>
    private const int OnNoLevel = -2;

    /// <summary>A <see cref="BlockDepths"/> value: a width that is not a multiple of <see cref="Compiler.Lexer.Lexer.IndentWidth"/> (SPY0013) or is indented with a tab.</summary>
    private const int Dropped = -1;

    /// <summary>
    /// The block depth of each logical-line-start line by the lexer's width rule: a width stack over the
    /// lines' leading whitespace, in line order — a wider line pushes; a narrower line pops to an EQUAL
    /// width. Unlike the indent map, a deeper line opens a block whatever ends the line above it, and a
    /// dedent between two widths opens nothing. A line the lexer reports and drops has no depth (negative), and
    /// so is exempt from clause 6's comparison: a width on no enclosing level (<see cref="OnNoLevel"/>, SPY0014)
    /// leaves the stack as it was; a width that is not a multiple of <see cref="Compiler.Lexer.Lexer.IndentWidth"/>
    /// (SPY0013) or is indented with a tab (<see cref="Dropped"/>) still moves it — a 2-space document's nesting is the
    /// structure its re-indent to 4 spaces must keep.
    /// </summary>
    private static Dictionary<int, int> BlockDepths(IReadOnlyList<string> lines, HashSet<int> logicalLineStarts)
    {
        var depths = new Dictionary<int, int>();
        var widths = new List<int> { 0 };
        foreach (var line in logicalLineStarts.OrderBy(l => l))
        {
            var text = lines[line - 1];
            var indent = text.Substring(0, text.Length - text.TrimStart(' ', '\t').Length);
            var width = indent.Length;
            int depth;
            if (width > widths[^1])
            {
                widths.Add(width);
                depth = widths.Count - 1;
            }
            else
            {
                depth = widths.IndexOf(width);
                if (depth >= 0)
                    widths.RemoveRange(depth + 1, widths.Count - depth - 1);
            }

            var dropped = width % Compiler.Lexer.Lexer.IndentWidth != 0 || indent.Contains('\t', StringComparison.Ordinal);
            depths[line] = dropped ? Dropped : depth < 0 ? OnNoLevel : depth;
        }

        return depths;
    }
}
