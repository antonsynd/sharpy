using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
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
    private readonly ILanguageServerFacade _server;

    public TextDocumentSyncHandler(
        SharplyWorkspace workspace,
        DiagnosticPublisher diagnosticPublisher,
        ILanguageServerFacade server)
    {
        _workspace = workspace;
        _diagnosticPublisher = diagnosticPublisher;
        _diagnosticPublisher.SetServer(server);
        _server = server;

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

        // Full sync mode: the last content change contains the full document
        var changes = request.ContentChanges.ToArray();
        if (changes.Length > 0)
        {
            var text = changes[^1].Text;
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
            Change = TextDocumentSyncKind.Full,
            Save = new SaveOptions { IncludeText = false }
        };
    }

    private Task OnDocumentAnalyzedAsync(string uri, Compiler.SemanticResult result)
    {
        var sourceText = _workspace.GetSourceText(uri);
        _diagnosticPublisher.PublishDiagnostics(uri, result, sourceText);
        return Task.CompletedTask;
    }
}
