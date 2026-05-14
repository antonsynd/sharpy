using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/onTypeFormatting requests.
/// Triggers on newline (auto-indent the new line) and on ':' (re-indent the
/// current line, e.g. for dedented 'else:' / 'elif x:' / 'except:').
/// Uses <see cref="IndentationService"/>'s lexer-based indent map; this works
/// even when the file is mid-edit and not yet parseable.
/// </summary>
internal sealed class SharpyOnTypeFormattingHandler : DocumentOnTypeFormattingHandlerBase
{
    private readonly SharpyWorkspace _workspace;

    public SharpyOnTypeFormattingHandler(SharpyWorkspace workspace)
    {
        _workspace = workspace;
    }

    public override Task<TextEditContainer?> Handle(
        DocumentOnTypeFormattingParams request,
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

        var line = request.Position.Line;
        // request.Character holds the trigger character; not currently needed
        // because both '\n' and ':' use the same indent-map alignment logic.
        _ = request.Character;

        // Always use the lexer-based indent map here — the file is being typed
        // and is usually not parseable until the user finishes the line.
        var (lineIndentLevels, _) = IndentationService.BuildIndentMap(text);

        var lines = text.Split('\n');
        if (line < 0 || line >= lines.Length)
            return Task.FromResult<TextEditContainer?>(null);

        var currentLine = lines[line].TrimEnd('\r');
        var trimmed = currentLine.TrimStart();

        // Blank lines have nothing to align.
        if (trimmed.Length == 0)
            return Task.FromResult<TextEditContainer?>(null);

        // 1-based for the indent map.
        var level = lineIndentLevels.TryGetValue(line + 1, out var l) ? l : 0;
        var desiredIndent = string.Concat(Enumerable.Repeat(indentStr, level));
        var existingIndent = currentLine.Substring(0, currentLine.Length - trimmed.Length);

        if (desiredIndent == existingIndent)
            return Task.FromResult<TextEditContainer?>(null);

        var edit = new TextEdit
        {
            Range = new LspRange(
                new Position(line, 0),
                new Position(line, existingIndent.Length)),
            NewText = desiredIndent
        };

        return Task.FromResult<TextEditContainer?>(new TextEditContainer(new[] { edit }));
    }

    protected override DocumentOnTypeFormattingRegistrationOptions CreateRegistrationOptions(
        DocumentOnTypeFormattingCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new DocumentOnTypeFormattingRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            FirstTriggerCharacter = "\n",
            MoreTriggerCharacter = new Container<string>(":")
        };
    }
}
