namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Indent-only formatting fallback used when the document fails to parse
/// (so <see cref="Sharpy.Compiler.Formatting.FormatterService"/> can't run).
/// Builds an indent map via the lexer's INDENT/DEDENT tokens and re-indents
/// each line, preserving multi-line string contents.
/// </summary>
internal static class FormattingFallback
{
    /// <summary>
    /// Re-indents the entire document using the lexer-based indent map.
    /// Returns the formatted text; equal to the input if no changes are needed.
    /// </summary>
    internal static string ReindentDocument(string text, int tabSize, bool insertSpaces)
    {
        var indentStr = insertSpaces ? new string(' ', tabSize) : "\t";

        var (lineIndentLevels, tokens) = IndentationService.BuildIndentMap(text);
        var multiLineStringLines = IndentationService.FindMultiLineStringLines(tokens);

        var lines = text.Split('\n');
        var formatted = new List<string>(lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (trimmed.Length == 0)
            {
                formatted.Add("");
                continue;
            }

            if (multiLineStringLines.Contains(i + 1)) // tokens use 1-based lines
            {
                formatted.Add(line);
                continue;
            }

            var level = lineIndentLevels.TryGetValue(i + 1, out var l) ? l : 0;
            formatted.Add(string.Concat(Enumerable.Repeat(indentStr, level)) + trimmed);
        }

        return string.Join("\n", formatted);
    }
}
