using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Sharpy.Compiler;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/didOpen, didChange, didClose, and didSave notifications.
/// Uses incremental document sync (the client sends only changed ranges).
/// </summary>
internal sealed class TextDocumentSyncHandler : TextDocumentSyncHandlerBase, IDisposable
{
    private readonly SharplyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly DiagnosticPublisher _diagnosticPublisher;

    public TextDocumentSyncHandler(
        SharplyWorkspace workspace,
        LanguageService languageService,
        DiagnosticPublisher diagnosticPublisher)
    {
        _workspace = workspace;
        _languageService = languageService;
        _diagnosticPublisher = diagnosticPublisher;

        // Subscribe to analysis completion to publish single-file diagnostics.
        // Project-file reanalysis is triggered directly in Handle(DidChange) instead.
        _workspace.DocumentAnalyzed += OnDocumentAnalyzedAsync;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
    {
        return new TextDocumentAttributes(uri, "sharpy");
    }

    public override async Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var text = request.TextDocument.Text;
        var version = request.TextDocument.Version ?? 0;

        _workspace.OpenDocument(uri, text, version);

        // For project files, trigger project-level reanalysis directly
        if (_languageService.IsProjectFile(uri))
        {
            await TriggerProjectReanalysisAsync(uri, ct).ConfigureAwait(false);
        }

        return Unit.Value;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var version = request.TextDocument.Version ?? 0;

        var changes = request.ContentChanges.ToArray();
        if (changes.Length > 0)
        {
            // Map LSP content change events to (Range?, Text) tuples for incremental application
            var mapped = changes
                .Select(c => (c.Range, c.Text))
                .ToList();
            _workspace.ApplyChanges(uri, mapped, version);
        }

        // For project files, kick off project-level reanalysis in the background.
        // Single-file diagnostics are published by the workspace debounce → DocumentAnalyzed path.
        // Project reanalysis updates the LanguageService cache for subsequent handler requests.
        if (_languageService.IsProjectFile(uri))
        {
            _ = TriggerProjectReanalysisAsync(uri, ct);
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

    /// <summary>
    /// Triggers project-level reanalysis for a file. Updates the LanguageService
    /// cache silently — diagnostic publishing is handled by the workspace's
    /// DocumentAnalyzed event (single-file analysis via debounce).
    /// </summary>
    private async Task TriggerProjectReanalysisAsync(string uri, CancellationToken ct)
    {
        try
        {
            await _languageService.OnDocumentChangedAsync(uri, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when document changes rapidly
        }
    }

    /// <summary>
    /// Handles workspace single-file analysis completion. Publishes diagnostics
    /// for the analyzed document. Project-level reanalysis for affected files is
    /// triggered directly in Handle(DidOpen/DidChange) instead of cascading here.
    /// </summary>
    private Task OnDocumentAnalyzedAsync(string uri, SemanticResult result)
    {
        var sourceText = _workspace.GetSourceText(uri);
        _diagnosticPublisher.PublishDiagnostics(uri, result, sourceText);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _workspace.DocumentAnalyzed -= OnDocumentAnalyzedAsync;
    }
}
