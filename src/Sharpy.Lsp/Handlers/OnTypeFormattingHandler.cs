using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler.Formatting;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/onTypeFormatting requests.
/// Triggers on newline (auto-indent the new line) and on ':' (re-indent the
/// current line). The indent map has no keyword rule, so an 'else:' typed at its body's width is not dedented.
/// Uses <see cref="IndentationService"/>'s lexer-based indent map; this works
/// even when the file is mid-edit and not yet parseable. Only a line that starts a logical line,
/// outside any literal, is re-indented, and only when <see cref="FormattingEdits.CheckedIndentOnly"/>
/// passes (P22e decision 6, #2168).
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
        // The editor's tabSize/insertSpaces never choose the indentation: Sharpy indentation is exactly
        // Lexer.IndentWidth spaces per level, no tabs (indentation.md; owner ruling 2026-09-30, P22b) —
        // a 2-space or tab re-indent would leave a file that does not lex.
        var indentStr = new string(' ', Compiler.Lexer.Lexer.IndentWidth);

        var line = request.Position.Line;
        // request.Character holds the trigger character; not currently needed
        // because both '\n' and ':' use the same indent-map alignment logic.
        _ = request.Character;

        // Always use the lexer-based indent map here — the file is being typed
        // and is usually not parseable until the user finishes the line.
        var map = IndentationService.BuildIndentMap(text);

        // The request's line as the client counts lines: \r\n, \n or a lone \r (#2168).
        var lines = LineDiff.Split(text).Lines;
        if (line < 0 || line >= lines.Count)
            return Task.FromResult<TextEditContainer?>(null);

        var currentLine = lines[line];
        var trimmed = currentLine.TrimStart(' ', '\t');

        // Blank lines have nothing to align.
        if (trimmed.Length == 0)
            return Task.FromResult<TextEditContainer?>(null);

        // P22e decision 6 (#2168), all 1-based for the map: when the lexer lost a literal that can span
        // lines, which lines are string content is unknown; a line that starts inside a literal is string
        // content; and the map's level is a block level, which is only a logical line's FIRST line's
        // indentation — a bracket or backslash continuation line (or a comment line) is not re-indented. A line below a
        // bracket the lexer never saw closed, or a recovery line of unknown level, is refused by the check
        // (FormattingFallback.IndentOnlyPreserved clauses 7 and 6c), which every candidate here passes through: a
        // refusal here as well would be inert (P22h mutation (d)) — as a refusal of a document indented with other
        // whitespace was (lead ruling L12 is the funnel's, FormattingEdits.CheckedIndentOnly; its twin here went red
        // under no mutation at /verify-implementation and was removed).
        if (map.LiteralStateUnknown
            || map.LiteralLines.Contains(line + 1)
            || !map.LogicalLineStarts.Contains(line + 1))
            return Task.FromResult<TextEditContainer?>(null);

        var level = map.LineIndent.TryGetValue(line + 1, out var l) ? l : 0;
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

        // The re-indent is applied only when the indent-only check passes (decision 8): it changes a width,
        // never a string and never the block the lexer reads any line in — the requested line's included.
        var checkedEdits = FormattingEdits.CheckedIndentOnly(text, new[] { edit });
        if (checkedEdits.Count == 0)
            return Task.FromResult<TextEditContainer?>(null);

        return Task.FromResult<TextEditContainer?>(new TextEditContainer(checkedEdits));
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
