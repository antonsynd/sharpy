using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

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
        var tabSize = (int)request.Options.TabSize;
        var insertSpaces = request.Options.InsertSpaces;
        var indentStr = insertSpaces ? new string(' ', tabSize) : "\t";

        var (lineIndentLevels, tokens) = IndentationService.BuildIndentMap(text);
        var multiLineStringLines = IndentationService.FindMultiLineStringLines(tokens);

        var lines = text.Split('\n');
        var startLine = request.Range.Start.Line;
        var endLine = System.Math.Min(request.Range.End.Line, lines.Length - 1);

        var edits = new List<TextEdit>();

        for (var i = startLine; i <= endLine; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

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

            // 1-based line for indent map and multi-line string check
            if (multiLineStringLines.Contains(i + 1))
                continue;

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

        if (edits.Count == 0)
            return Task.FromResult(new TextEditContainer());

        return Task.FromResult<TextEditContainer>(new TextEditContainer(edits));
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
