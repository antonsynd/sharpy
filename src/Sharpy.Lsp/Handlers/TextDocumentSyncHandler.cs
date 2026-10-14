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
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly DiagnosticRoutes _diagnosticRoutes;

    public TextDocumentSyncHandler(
        SharpyWorkspace workspace,
        LanguageService languageService,
        DiagnosticRoutes diagnosticRoutes)
    {
        _workspace = workspace;
        _languageService = languageService;
        _diagnosticRoutes = diagnosticRoutes;

        // Subscribe to analysis completion to publish single-file diagnostics (the single-file
        // route). Project files are published by the project route (DiagnosticRoutes), whose
        // reanalysis Handle(DidOpen/DidChange) triggers directly.
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

        // For project files, kick off project-level reanalysis in the background; it publishes
        // every project file when it completes (a buffer identical to what was analyzed —
        // whitespace/comment-only — publishes nothing: the project's publication stands). Must not
        // be awaited — awaiting can deadlock when background indexing holds the analysis lock.
        if (_languageService.IsProjectFile(uri))
        {
            _ = TriggerProjectReanalysisAsync(uri);
        }

        return Unit.Task;
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

        // For project files, kick off project-level reanalysis in the background; it publishes
        // every project file when it completes. A newer edit cancels it, and the newer one
        // publishes.
        if (_languageService.IsProjectFile(uri))
        {
            _ = TriggerProjectReanalysisAsync(uri);
        }

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken ct)
    {
        var uri = request.TextDocument.Uri.ToString();
        var closedBufferText = _workspace.GetDocument(uri)?.Text;
        _workspace.CloseDocument(uri);

        // A project file keeps the project's publication — the build still reports its
        // diagnostics with the file closed; any other file's squiggles are cleared (#2242).
        _diagnosticRoutes.OnDocumentClosed(uri);

        // ...unless that publication came from an unsaved buffer the close just discarded: the
        // file's truth is its disk text again, so the project is reanalyzed from disk (which also
        // supersedes a still-running reanalysis of the buffer). Fire-and-forget, like an edit's.
        if (closedBufferText != null && _languageService.IsProjectFile(uri))
        {
            _ = TriggerCloseReanalysisAsync(uri, closedBufferText);
        }

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
    /// Triggers project-level reanalysis for a file; the LanguageService publishes its results
    /// through the project route when it completes.
    /// </summary>
    /// <remarks>
    /// Deliberately not passed the notification's cancellation token: OmniSharp cancels that token
    /// when the notification handler returns — immediately, since this runs fire-and-forget — and a
    /// reanalysis linked to it was cancelled on every open and edit (#2242). The LanguageService
    /// cancels a superseded reanalysis itself and links the rest to the server's lifetime.
    /// </remarks>
    private async Task TriggerProjectReanalysisAsync(string uri)
    {
        try
        {
            await _languageService.OnDocumentChangedAsync(uri).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when document changes rapidly
        }
    }

    /// <summary>
    /// The close half of the project route: see <see cref="LanguageService.OnDocumentClosedAsync"/>.
    /// Not passed the notification's token, for the reason <see cref="TriggerProjectReanalysisAsync"/>
    /// gives.
    /// </summary>
    private async Task TriggerCloseReanalysisAsync(string uri, string closedBufferText)
    {
        try
        {
            await _languageService.OnDocumentClosedAsync(uri, closedBufferText).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer open/edit of the same document, which publishes.
        }
    }

    /// <summary>
    /// Handles workspace single-file analysis completion: the single-file route. Publishes nothing
    /// for a project file once the project has been analyzed — the project's per-file result is
    /// that file's publication (#2242). Before the first indexing completes the project owns no
    /// file, so a document opened during indexing gets a single-file publication first and the
    /// index's publication after it.
    /// </summary>
    private Task OnDocumentAnalyzedAsync(string uri, SemanticResult result)
    {
        // The single-file result carries its import closure's diagnostics too; the routes publish
        // an imported file's share only when no one else owns that file.
        _diagnosticRoutes.PublishSingleFile(uri, result, _workspace.GetSourceText(uri));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _workspace.DocumentAnalyzed -= OnDocumentAnalyzedAsync;
    }
}
