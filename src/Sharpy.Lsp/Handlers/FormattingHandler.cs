using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/formatting requests.
/// Uses the Lexer's Indent/Dedent tokens to determine correct indentation levels,
/// then re-indents each line according to the user's tab size/spaces preference.
/// </summary>
internal sealed class SharpyFormattingHandler : DocumentFormattingHandlerBase
{
    private readonly SharpyWorkspace _workspace;

    public SharpyFormattingHandler(SharpyWorkspace workspace)
    {
        _workspace = workspace;
    }

    public override Task<TextEditContainer?> Handle(
        DocumentFormattingParams request,
        CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var doc = _workspace.GetDocument(uri);

        if (doc == null)
            return Task.FromResult<TextEditContainer?>(null);

        var text = doc.Text;
        var tabSize = (int)request.Options.TabSize;
        var insertSpaces = request.Options.InsertSpaces;
        var indentStr = insertSpaces ? new string(' ', tabSize) : "\t";

        var (lineIndentLevels, tokens) = IndentationService.BuildIndentMap(text);
        var multiLineStringLines = IndentationService.FindMultiLineStringLines(tokens);

        var lines = text.Split('\n');
        var formatted = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();

            // Empty lines stay empty
            if (trimmed.Length == 0)
            {
                formatted.Add("");
                continue;
            }

            // Lines inside multi-line strings: preserve as-is
            if (multiLineStringLines.Contains(i + 1)) // tokens use 1-based lines
            {
                formatted.Add(line);
                continue;
            }

            // Apply indent level from token stream
            var level = lineIndentLevels.TryGetValue(i + 1, out var l) ? l : 0;
            var formattedLine = string.Concat(Enumerable.Repeat(indentStr, level)) + trimmed;
            formatted.Add(formattedLine);
        }

        var formattedText = string.Join("\n", formatted);

        if (formattedText == text)
            return Task.FromResult<TextEditContainer?>(null);

        var lastLine = lines.Length - 1;
        var lastCol = lines[lastLine].TrimEnd('\r').Length;

        var edits = new List<TextEdit>
        {
            new()
            {
                Range = new LspRange(
                    new Position(0, 0),
                    new Position(lastLine, lastCol)),
                NewText = formattedText
            }
        };

        return Task.FromResult<TextEditContainer?>(new TextEditContainer(edits));
    }

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(
        DocumentFormattingCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentFormattingRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy")
        };
    }
}
