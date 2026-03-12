using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Lsp.Handlers;

/// <summary>
/// Handles textDocument/didOpen, didChange, didClose, and didSave notifications.
/// Uses incremental document sync (the client sends only changed ranges).
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
            // Map LSP content change events to (Range?, Text) tuples for incremental application
            var mapped = changes
                .Select(c => (c.Range, c.Text))
                .ToList();
            _workspace.ApplyChanges(uri, mapped, version);
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

    private Task OnDocumentAnalyzedAsync(string uri, SemanticResult result)
    {
        var sourceText = _workspace.GetSourceText(uri);
        _diagnosticPublisher.PublishDiagnostics(uri, result, sourceText);

        // Populate dependency graph from import statements
        if (result.Ast != null)
        {
            RecordImportDependencies(uri, result.Ast);
        }

        return Task.CompletedTask;
    }

    private void RecordImportDependencies(string currentUri, Module ast)
    {
        foreach (var stmt in ast.Body)
        {
            switch (stmt)
            {
                case ImportStatement importStmt:
                    foreach (var alias in importStmt.Names)
                    {
                        var importedUri = ResolveModuleToUri(alias.Name);
                        if (importedUri != null)
                        {
                            _workspace.RecordDependency(importedUri, currentUri);
                        }
                    }
                    break;

                case FromImportStatement fromStmt:
                    var moduleUri = ResolveModuleToUri(fromStmt.ResolvedModulePath ?? fromStmt.Module);
                    if (moduleUri != null)
                    {
                        _workspace.RecordDependency(moduleUri, currentUri);
                    }
                    break;
            }
        }
    }

    private string? ResolveModuleToUri(string moduleName)
    {
        // Convert module.submodule to module/submodule.spy path and check workspace
        var relativePath = moduleName.Replace('.', '/') + ".spy";
        foreach (var docUri in _workspace.GetAllDocumentUris())
        {
            if (docUri.EndsWith(relativePath, StringComparison.OrdinalIgnoreCase))
            {
                return docUri;
            }
        }
        return null;
    }

}
