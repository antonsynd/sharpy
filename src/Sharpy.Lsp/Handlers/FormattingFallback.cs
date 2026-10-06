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
    /// tabSize/insertSpaces, indentation.md). Returns the formatted text; equal to the input if no
    /// changes are needed.
    /// </summary>
    internal static string ReindentDocument(string text)
    {
        var indentStr = new string(' ', Compiler.Lexer.Lexer.IndentWidth);

        var map = IndentationService.BuildIndentMap(text);
        var lineIndentLevels = map.LineIndent;
        var multiLineStringLines = map.LiteralLines;

        var lines = text.Split('\n');
        var formatted = new List<string>(lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            // A line that starts inside a string literal is its data — kept verbatim, whitespace-only
            // lines included (#2062).
            if (multiLineStringLines.Contains(i + 1)) // tokens use 1-based lines
            {
                formatted.Add(line);
                continue;
            }

            if (trimmed.Length == 0)
            {
                formatted.Add("");
                continue;
            }

            var level = lineIndentLevels.TryGetValue(i + 1, out var l) ? l : 0;
            formatted.Add(string.Concat(Enumerable.Repeat(indentStr, level)) + trimmed);
        }

        return string.Join("\n", formatted);
    }

    /// <summary>
    /// The check an indent-only edit of an UNPARSEABLE document must pass before it is applied (P22e
    /// decision 8, #2168) — the parseable case is the SPY0912 net's. True iff <paramref name="applied"/>
    /// differs from <paramref name="source"/> only in what indentation means nothing to the lexer:
    /// <list type="number">
    /// <item>the same number of lines;</item>
    /// <item>each line's content after its leading whitespace unchanged (a whitespace-only line may become empty);</item>
    /// <item>the same lines start inside a literal, and each of them is byte-identical;</item>
    /// <item>the indent map's level of every logical-line-start line unchanged — re-indenting ONE line of
    /// an 8-space block moves it out of the block (cell 16);</item>
    /// <item>no more lexer indentation diagnostics (SPY0013, SPY0014) than the source — a dedent between
    /// two widths of the stack keeps its level in the map, which opens a block at the unseen width, while
    /// the lexer reports the line (cell 21).</item>
    /// </list>
    /// Lines are compared without their line breaks.
    /// </summary>
    internal static bool IndentOnlyPreserved(string source, string applied)
    {
        var sourceLines = Lines(source);
        var appliedLines = Lines(applied);
        if (sourceLines.Length != appliedLines.Length)
            return false;

        for (var i = 0; i < sourceLines.Length; i++)
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

        return after.IndentationDiagnostics <= before.IndentationDiagnostics;

        static string[] Lines(string text) => text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
    }
}
