using Newtonsoft.Json.Linq;
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
    private readonly ILanguageServerFacade _server;

    public DiagnosticPublisher(ILanguageServerFacade server)
    {
        _server = server;
    }

    public void PublishDiagnostics(string uri, SemanticResult result, SourceText? sourceText)
    {
        var lspDiagnostics = ConvertDiagnostics(result.Diagnostics, sourceText);

        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = DocumentUri.From(uri),
            Diagnostics = new Container<Diagnostic>(lspDiagnostics)
        });
    }

    public void ClearDiagnostics(string uri)
    {
        _server.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams
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

        var lspDiag = new Diagnostic
        {
            Range = range,
            Severity = ConvertSeverity(diag.Severity),
            Message = diag.Message,
            Source = "sharpy",
            Code = !string.IsNullOrEmpty(diag.Code)
                ? new DiagnosticCode(diag.Code)
                : default,
            Tags = GetDiagnosticTags(diag),
            Data = diag.Data is { Count: > 0 }
                ? JObject.FromObject(diag.Data)
                : null
        };

        return lspDiag;
    }

    /// <summary>
    /// Returns LSP diagnostic tags for a compiler diagnostic. Tags allow editors to
    /// render diagnostics with special styling (e.g., faded text for unnecessary code).
    /// </summary>
    internal static Container<DiagnosticTag>? GetDiagnosticTags(CompilerDiagnostic diag)
    {
        // Tag advisory hints about redundant/unnecessary code so editors render
        // them with faded text. Only specific transition hints qualify — most
        // hints are informational about behavioral differences, not redundancy.
        if (diag.Severity == CompilerDiagnosticSeverity.Hint && IsUnnecessaryCodeHint(diag.Code))
        {
            return new Container<DiagnosticTag>(DiagnosticTag.Unnecessary);
        }

        return null;
    }

    /// <summary>
    /// Returns true if the diagnostic code identifies code that is unnecessary
    /// or redundant and should be rendered with faded text.
    /// </summary>
    private static bool IsUnnecessaryCodeHint(string? code)
    {
        // SPY0477: @static decorator is unnecessary on a method without 'self'.
        // Other transition hints (SPY0470-SPY0476) are informational about behavioral
        // differences from Python/C# — the code is not redundant.
        return code == DiagnosticCodes.Validation.UnnecessaryStaticDecoratorHint;
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
