using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/didOpen, didChange, didClose, and didSave notifications.
/// Uses full document sync (the client sends the entire document content on each change).
/// </summary>
internal sealed class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private readonly SharplyWorkspace _workspace;
    private readonly DiagnosticPublisher _diagnosticPublisher;

    public TextDocumentSyncHandler(
        SharplyWorkspace workspace,
        DiagnosticPublisher diagnosticPublisher)
    {
        _workspace = workspace;
        _diagnosticPublisher = diagnosticPublisher;

        // Subscribe to analysis completion to publish diagnostics
        _workspace.DocumentAnalyzed += OnDocumentAnalyzedAsync;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "sharpy");
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var text = request.TextDocument.Text;
        var version = request.TextDocument.Version ?? 0;

        _workspace.OpenDocument(uri, text, version);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var version = request.TextDocument.Version ?? 0;

        var changes = request.ContentChanges.ToArray();
        if (changes.Length > 0)
        {
            var currentDoc = _workspace.GetDocument(uri);
            var currentText = currentDoc?.Text ?? string.Empty;
            var text = ApplyIncrementalChanges(currentText, changes);
            _workspace.UpdateDocument(uri, text, version);
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        _diagnosticPublisher.ClearDiagnostics(uri);
        _workspace.CloseDocument(uri);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken ct)
    {
        // Reanalysis triggered by didChange; save is a no-op for now
        return Unit.Task;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability,
        ClientCapabilities clientCapabilities)
    {
        return new TextDocumentSyncRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForPattern("**/*.spy"),
            Change = TextDocumentSyncKind.Incremental,
            Save = new SaveOptions { IncludeText = false }
        };
    }

    private Task OnDocumentAnalyzedAsync(string uri, Compiler.SemanticResult result)
    {
        var sourceText = _workspace.GetSourceText(uri);
        _diagnosticPublisher.PublishDiagnostics(uri, result, sourceText);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies incremental text changes to a document. Each change has an optional Range
    /// (for partial updates) or null Range (for full replacement).
    /// </summary>
    private static string ApplyIncrementalChanges(
        string currentText,
        TextDocumentContentChangeEvent[] changes)
    {
        var text = currentText;
        foreach (var change in changes)
        {
            if (change.Range != null)
            {
                var startOffset = GetOffset(text, change.Range.Start);
                var endOffset = GetOffset(text, change.Range.End);
                text = string.Concat(text.AsSpan(0, startOffset), change.Text, text.AsSpan(endOffset));
            }
            else
            {
                // Full document replacement (no range = full sync fallback)
                text = change.Text;
            }
        }
        return text;
    }

    /// <summary>
    /// Converts an LSP Position (0-based line/character) to a string offset.
    /// Handles both \n and \r\n line endings.
    /// </summary>
    private static int GetOffset(string text, Position position)
    {
        var offset = 0;
        var line = 0;

        while (line < position.Line && offset < text.Length)
        {
            if (text[offset] == '\r' && offset + 1 < text.Length && text[offset + 1] == '\n')
            {
                offset += 2;
                line++;
            }
            else if (text[offset] == '\n')
            {
                offset++;
                line++;
            }
            else
            {
                offset++;
            }
        }

        // Add character offset within the target line
        offset += position.Character;

        // Clamp to text length
        return System.Math.Min(offset, text.Length);
    }
}
