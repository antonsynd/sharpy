using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Formatting;
using LspFormatOptions = Sharpy.Compiler.Formatting.FormatOptions;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/rangeFormatting requests.
/// Primary path: format the whole document with <see cref="FormatterService"/>,
/// then emit per-line edits only for lines that intersect the requested range.
/// Fallback: when the document fails to parse, fall back to indent-only
/// formatting (the previous behaviour). A document that parses but whose
/// formatting the formatter declines (SPY0912) gets no edits.
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

        var lines = text.Split('\n');
        var startLine = request.Range.Start.Line;
        var endLine = System.Math.Min(request.Range.End.Line, lines.Length - 1);

        // Primary path: run the full formatter, then narrow to per-line edits
        // for lines that intersect the requested range.
        var options = new LspFormatOptions
        {
            LineEnding = "\n"
        };

        var formatResult = FormatterService.Format(text, options);

        // SPY0912: the document parsed, but the formatter refused its own output because it would
        // change what the file says. No edits — the indent-only fallback is for documents that fail
        // to parse and must not re-indent a parseable one (P22b).
        if (formatResult.Diagnostics.Any(d => d.Code == DiagnosticCodes.Infrastructure.FormatterDeclined))
            return Task.FromResult(new TextEditContainer());

        if (formatResult.Diagnostics.Count == 0)
        {
            if (!formatResult.HasChanges)
                return Task.FromResult(new TextEditContainer());

            var formattedLines = formatResult.FormattedText.Split('\n');
            var edits = new List<TextEdit>();

            // Only emit edits for lines that already exist in the original
            // document and intersect the requested range. Added/removed lines
            // (e.g., blank-line normalization) are intentionally skipped — range
            // formatting must not affect text outside the requested range.
            for (var i = startLine; i <= endLine; i++)
            {
                if (i >= formattedLines.Length)
                    break;

                var original = lines[i].TrimEnd('\r');
                var replacement = formattedLines[i].TrimEnd('\r');

                if (replacement != original)
                {
                    edits.Add(new TextEdit
                    {
                        Range = new LspRange(new Position(i, 0), new Position(i, lines[i].Length)),
                        NewText = replacement
                    });
                }
            }

            return Task.FromResult(new TextEditContainer(edits));
        }

        // Fallback: indent-only formatting per line.
        var fallbackEdits = ComputeIndentOnlyRangeEdits(text, startLine, endLine);
        return Task.FromResult(new TextEditContainer(fallbackEdits));
    }

    private static List<TextEdit> ComputeIndentOnlyRangeEdits(string text, int startLine, int endLine)
    {
        var indentStr = new string(' ', Compiler.Lexer.Lexer.IndentWidth);

        var (lineIndentLevels, tokens, _) = IndentationService.BuildIndentMap(text);
        var multiLineStringLines = IndentationService.FindMultiLineStringLines(tokens, text);

        var lines = text.Split('\n');
        endLine = System.Math.Min(endLine, lines.Length - 1);

        var edits = new List<TextEdit>();

        for (var i = startLine; i <= endLine; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            // A line that starts inside a string literal is its data — never re-indented or blanked,
            // whitespace-only lines included (#2062).
            if (multiLineStringLines.Contains(i + 1))
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
