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
    /// Re-indents the entire document using the lexer-based indent map, at the language's one
    /// indentation unit (<see cref="Compiler.Lexer.Lexer.IndentWidth"/> spaces — never the editor's
    /// tabSize/insertSpaces, indentation.md). Returns the formatted text; equal to the input if no
    /// changes are needed.
    /// </summary>
    internal static string ReindentDocument(string text)
    {
        var indentStr = new string(' ', Compiler.Lexer.Lexer.IndentWidth);

        var (lineIndentLevels, tokens, _) = IndentationService.BuildIndentMap(text);
        var multiLineStringLines = IndentationService.FindMultiLineStringLines(tokens, text);

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
}
