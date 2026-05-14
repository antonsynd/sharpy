using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Formatting;
using LspFormatOptions = Sharpy.Compiler.Formatting.FormatOptions;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/formatting requests.
/// Primary path: invoke <see cref="FormatterService"/> to fully reformat the
/// document via the pretty printer (trivia-aware emission, blank-line rules).
/// Fallback: when the document fails to parse, fall back to
/// <see cref="IndentationService"/>-based indent-only formatting so users can
/// still tidy up half-written code.
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

        // Primary path: full FormatterService pass.
        var options = new LspFormatOptions
        {
            IndentSize = tabSize,
            UseTabs = !insertSpaces,
            LineEnding = "\n"
        };

        var formatResult = FormatterService.Format(text, options);

        string formattedText;
        if (formatResult.Diagnostics.Count == 0)
        {
            if (!formatResult.HasChanges)
                return Task.FromResult<TextEditContainer?>(null);

            formattedText = formatResult.FormattedText;
        }
        else
        {
            // Fall back to indent-only formatting for unparseable files.
            formattedText = FormattingFallback.ReindentDocument(text, tabSize, insertSpaces);
            if (formattedText == text)
                return Task.FromResult<TextEditContainer?>(null);
        }

        var lines = text.Split('\n');
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
