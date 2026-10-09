using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Formatting;
using LspFormatOptions = Sharpy.Compiler.Formatting.FormatOptions;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/rangeFormatting (Format Selection, P22e #2168).
/// Primary path: <see cref="FormatterService.FormatRange"/> — the hunks of the checked whole-document
/// output that the selection touches, with the net run on the text they produce — turned into edits
/// by <see cref="FormattingEdits.ToTextEdits"/>, so the client applies exactly that checked text: a
/// hunk that adds or removes lines is applied whole, never cut to the lines it shares with the source.
/// A document that parses but whose selected hunks the formatter declines (SPY0912) gets no edits.
/// Fallback: when the document fails to lex or parse, indent-only formatting of the selected lines,
/// through <see cref="FormattingEdits.CheckedIndentOnly"/>.
/// </summary>
internal sealed class SharpyRangeFormattingHandler : DocumentRangeFormattingHandlerBase
{
    private readonly SharpyWorkspace _workspace;

    public SharpyRangeFormattingHandler(SharpyWorkspace workspace)
    {
        _workspace = workspace;
    }

    public override Task<TextEditContainer> Handle(
        DocumentRangeFormattingParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var doc = _workspace.GetDocument(uri);

        if (doc == null)
            return Task.FromResult(new TextEditContainer());

        var text = doc.Text;
        // The editor's tabSize/insertSpaces never choose the indentation: Sharpy indentation is exactly
        // Lexer.IndentWidth spaces per level, no tabs (indentation.md; owner ruling 2026-09-30, P22b) —
        // a 2-space or tab re-indent would leave a file that does not lex.

        var options = new LspFormatOptions
        {
            LineEnding = "\n"
        };
        var selection = new FormatSelection(request.Range.Start.Line, request.Range.End.Line, request.Range.End.Character);
        var result = FormatterService.FormatRange(text, selection, options);

        if (result.SourceParses)
        {
            // SPY0912: the document parsed, but the formatter refused the text the selected hunks
            // produce because it would change what the file says. No edits — the indent-only fallback
            // is for documents that fail to parse and must not re-indent a parseable one (P22b).
            if (result.Diagnostics.Any(d => d.Code == DiagnosticCodes.Infrastructure.FormatterDeclined))
                return Task.FromResult(new TextEditContainer());

            return Task.FromResult(new TextEditContainer(FormattingEdits.ToTextEdits(text, result.Hunks)));
        }

        // Fallback: indent-only formatting per line, applied only when the text it produces passes the
        // indent-only check (P22e decision 8, #2168). The selected lines are decision 2's, the same
        // rule as the primary path: an editor's whole-line selection ends at column 0 of the NEXT
        // line, which is not selected.
        var (startLine, endLine) = selection.SelectedLines(LineDiff.Split(text).Lines);
        var fallbackEdits = ComputeIndentOnlyRangeEdits(text, startLine, endLine);
        return Task.FromResult(new TextEditContainer(FormattingEdits.CheckedIndentOnly(text, fallbackEdits)));
    }

    /// <summary>
    /// The indent-only candidate for lines <paramref name="startLine"/>..<paramref name="endLine"/>: one edit per
    /// line whose leading whitespace changes, over the line's content only — its line break is never in the
    /// range. Lines are the client's (<see cref="LineDiff.Split"/>: <c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>). A line
    /// a bracket the lexer never saw closed freezes (<see cref="IndentMap.FrozenFrom"/>, R-FU, #2279, L9) gets no edit,
    /// whitespace-only lines included.
    /// </summary>
    internal static List<TextEdit> ComputeIndentOnlyRangeEdits(string text, int startLine, int endLine)
    {
        var indentStr = new string(' ', Compiler.Lexer.Lexer.IndentWidth);

        var map = IndentationService.BuildIndentMap(text);
        var lineIndentLevels = map.LineIndent;
        var multiLineStringLines = map.LiteralLines;

        var lines = LineDiff.Split(text).Lines;
        endLine = System.Math.Min(endLine, lines.Count - 1);

        var edits = new List<TextEdit>();

        for (var i = startLine; i <= endLine; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart(' ', '\t');

            // A line that starts inside a string literal is its data — never re-indented or blanked,
            // whitespace-only lines included (#2062).
            if (multiLineStringLines.Contains(i + 1))
                continue;

            // A line below a bracket the lexer never saw closed is the bracket's to the lexer (R-FU, #2279).
            if (!map.IsReindentable(i + 1))
                continue;

            if (trimmed.Length == 0)
            {
                if (line.Length > 0)
                {
                    edits.Add(new TextEdit
                    {
                        Range = new LspRange(new Position(i, 0), new Position(i, line.Length)),
                        NewText = ""
                    });
                }
                continue;
            }

            var level = lineIndentLevels.TryGetValue(i + 1, out var l) ? l : 0;
            var formattedLine = string.Concat(Enumerable.Repeat(indentStr, level)) + trimmed;

            if (formattedLine != line)
            {
                edits.Add(new TextEdit
                {
                    Range = new LspRange(new Position(i, 0), new Position(i, line.Length)),
                    NewText = formattedLine
                });
            }
        }

        return edits;
    }

    protected override DocumentRangeFormattingRegistrationOptions CreateRegistrationOptions(
        DocumentRangeFormattingCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentRangeFormattingRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
