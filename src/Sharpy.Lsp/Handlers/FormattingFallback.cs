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
    /// client count lines), so a CRLF document stays CRLF (#2168). Returns the formatted text; equal to the
    /// input if no changes are needed.
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
    /// A line the lexer drops (<see cref="BlockDepths"/>) is the misindentation the fallback repairs and is exempt.</item>
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

        // The lines the map reads as continuations only because no Newline follows an error recovery
        // (HiddenAfterRecovery) are judged with the logical lines: a re-indent may not move one either (P22g).
        var appliedDepths = BlockDepths(appliedLines, DepthJudgedLines(after));
        foreach (var (line, depth) in BlockDepths(sourceLines, DepthJudgedLines(before)))
        {
            if (depth >= 0 && (!appliedDepths.TryGetValue(line, out var appliedDepth) || appliedDepth != depth))
                return false;
        }

        return true;
    }

    /// <summary>
    /// The block depth of each logical-line-start line by the lexer's width rule: a width stack over the
    /// lines' leading whitespace, in line order — a wider line pushes; a narrower line pops to an EQUAL
    /// width. Unlike the indent map, a deeper line opens a block whatever ends the line above it, and a
    /// dedent between two widths opens nothing. A line the lexer reports and drops has no depth (-1), and
    /// so is exempt from the comparison: a width on no enclosing level (SPY0014) leaves the stack as it
    /// was; a width that is not a multiple of <see cref="Compiler.Lexer.Lexer.IndentWidth"/> (SPY0013) or
    /// is indented with a tab still moves it — a 2-space document's nesting is the structure its re-indent
    /// to 4 spaces must keep.
    /// </summary>
    private static HashSet<int> DepthJudgedLines((Dictionary<int, int> LineIndent, List<Compiler.Lexer.Token> Tokens, bool LiteralStateUnknown,
        HashSet<int> LogicalLineStarts, HashSet<int> LiteralLines, int IndentationDiagnostics, HashSet<int> HiddenAfterRecovery) map)
    {
        var lines = new HashSet<int>(map.LogicalLineStarts);
        lines.UnionWith(map.HiddenAfterRecovery);
        return lines;
    }

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
            depths[line] = dropped ? -1 : depth;
        }

        return depths;
    }
}
