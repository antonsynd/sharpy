using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Text;

namespace Sharpy.Lsp;

/// <summary>
/// Converts compiler diagnostics to LSP diagnostics and publishes them.
/// </summary>
internal sealed class DiagnosticPublisher
{
    private ILanguageServerFacade? _server;

    public DiagnosticPublisher()
    {
    }

    public DiagnosticPublisher(ILanguageServerFacade server)
    {
        _server = server;
    }

    public void SetServer(ILanguageServerFacade server)
    {
        _server = server;
    }

    public void PublishDiagnostics(string uri, SemanticResult result, SourceText? sourceText)
    {
        if (_server == null)
            return;

        var lspDiagnostics = ConvertDiagnostics(result.Diagnostics, sourceText);

        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = DocumentUri.From(uri),
            Diagnostics = new Container<Diagnostic>(lspDiagnostics)
        });
    }

    public void ClearDiagnostics(string uri)
    {
        _server?.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = DocumentUri.From(uri),
            Diagnostics = new Container<Diagnostic>()
        });
    }

    internal static System.Collections.Generic.List<Diagnostic> ConvertDiagnostics(
        IReadOnlyList<CompilerDiagnostic> diagnostics,
        SourceText? sourceText)
    {
        var result = new System.Collections.Generic.List<Diagnostic>(diagnostics.Count);

        foreach (var diag in diagnostics)
        {
            result.Add(ConvertDiagnostic(diag, sourceText));
        }

        return result;
    }

    internal static Diagnostic ConvertDiagnostic(CompilerDiagnostic diag, SourceText? sourceText)
    {
        var range = PositionConverter.DiagnosticToRange(diag, sourceText);

        return new Diagnostic
        {
            Range = range,
            Severity = ConvertSeverity(diag.Severity),
            Message = diag.Message,
            Source = "sharpy",
            Code = !string.IsNullOrEmpty(diag.Code)
                ? new DiagnosticCode(diag.Code)
                : default
        };
    }

    internal static DiagnosticSeverity ConvertSeverity(CompilerDiagnosticSeverity severity)
    {
        return severity switch
        {
            CompilerDiagnosticSeverity.Error => DiagnosticSeverity.Error,
            CompilerDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            CompilerDiagnosticSeverity.Info => DiagnosticSeverity.Information,
            CompilerDiagnosticSeverity.Hint => DiagnosticSeverity.Hint,
            _ => DiagnosticSeverity.Information
        };
    }
}
