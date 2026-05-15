using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Text;

namespace Sharpy.Lsp;

/// <summary>
/// Converts compiler diagnostics to LSP diagnostics and publishes them.
/// </summary>
internal sealed class DiagnosticPublisher
{
    private readonly ILanguageServerFacade _server;
    private readonly LspConfiguration _configuration;

    public DiagnosticPublisher(ILanguageServerFacade server, LspConfiguration configuration)
    {
        _server = server;
        _configuration = configuration;
    }

    public void PublishDiagnostics(string uri, SemanticResult result, SourceText? sourceText)
    {
        var lspDiagnostics = ConvertDiagnostics(result.Diagnostics, sourceText, _configuration, result.SemanticQuery, uri);

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
        SourceText? sourceText,
        LspConfiguration? configuration = null,
        ISemanticQuery? semanticQuery = null,
        string? documentUri = null)
    {
        var result = new System.Collections.Generic.List<Diagnostic>(diagnostics.Count);
        var transitionHintsEnabled = configuration?.TransitionHintsEnabled ?? true;

        foreach (var diag in diagnostics)
        {
            // Filter out transition hints when disabled.
            // Transition hints are Hint-severity diagnostics in the SPY0470-SPY0489 range.
            if (!transitionHintsEnabled
                && diag.Severity == CompilerDiagnosticSeverity.Hint
                && IsTransitionHintCode(diag.Code))
            {
                continue;
            }

            // Source generator diagnostics: if the diagnostic carries a synthetic file path
            // like `<generated:GenName:TargetName>`, route it back to the original source
            // file at the generator's trigger decorator location.
            var rerouted = TryRerouteGeneratedDiagnostic(diag, semanticQuery, documentUri);
            result.Add(ConvertDiagnostic(rerouted ?? diag, sourceText, rerouted != null ? diag : null));
        }

        return result;
    }

    internal static Diagnostic ConvertDiagnostic(CompilerDiagnostic diag, SourceText? sourceText, CompilerDiagnostic? generatedOrigin = null)
    {
        var range = PositionConverter.DiagnosticToRange(diag, sourceText);

        Container<DiagnosticRelatedInformation>? relatedInfo = null;
        if (generatedOrigin != null)
        {
            // Attach the original (synthetic) location as related information so editors
            // can show "Also see: <generated:Foo:Bar>" alongside the rerouted diagnostic.
            var origRange = PositionConverter.DiagnosticToRange(generatedOrigin, sourceText: null);
            var originPath = generatedOrigin.FilePath ?? "<generated>";
            relatedInfo = new Container<DiagnosticRelatedInformation>(
                new DiagnosticRelatedInformation
                {
                    Location = new Location
                    {
                        Uri = DocumentUri.From(new Uri($"sharpy-generated:{Uri.EscapeDataString(originPath)}", UriKind.Absolute)),
                        Range = origRange,
                    },
                    Message = $"In generated source {originPath}",
                });
        }

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
                : null,
            RelatedInformation = relatedInfo,
        };

        return lspDiag;
    }

    /// <summary>
    /// Prefix used on file paths produced by the source-generator pipeline.
    /// See <c>ProjectCompiler.IntegrateGeneratedSource</c>.
    /// </summary>
    internal const string GeneratedFilePathPrefix = "<generated:";

    /// <summary>
    /// Returns true if the file path is a synthetic generator path of the form
    /// <c>&lt;generated:GeneratorName:TargetName&gt;</c>.
    /// </summary>
    internal static bool IsGeneratedFilePath(string? filePath)
    {
        return !string.IsNullOrEmpty(filePath)
            && filePath!.StartsWith(GeneratedFilePathPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses a synthetic generator file path into its generator name and target name
    /// components. Returns null if the path is not a valid generator path.
    /// </summary>
    internal static (string GeneratorName, string TargetName)? ParseGeneratedFilePath(string? filePath)
    {
        if (!IsGeneratedFilePath(filePath))
            return null;

        var inner = filePath!.Substring(GeneratedFilePathPrefix.Length);
        if (inner.EndsWith(">", StringComparison.Ordinal))
            inner = inner.Substring(0, inner.Length - 1);

        var colonIdx = inner.IndexOf(':', StringComparison.Ordinal);
        if (colonIdx < 0)
            return (inner, string.Empty);

        return (inner.Substring(0, colonIdx), inner.Substring(colonIdx + 1));
    }

    /// <summary>
    /// If the diagnostic originates from a synthetic generated source path, returns a
    /// remapped <see cref="CompilerDiagnostic"/> whose line/column point at the matching
    /// generator's trigger decorator in the original source file. Returns null when no
    /// remap applies.
    /// </summary>
    internal static CompilerDiagnostic? TryRerouteGeneratedDiagnostic(
        CompilerDiagnostic diag,
        ISemanticQuery? semanticQuery,
        string? documentUri)
    {
        var parsed = ParseGeneratedFilePath(diag.FilePath);
        if (parsed is null)
            return null;
        if (semanticQuery is null)
            return null;

        var (generatorName, _) = parsed.Value;

        // Search recorded generator bindings for a trigger decorator whose name matches.
        // The bracket attribute's Name carries the generator's class name (e.g., "GenerateEquals").
        foreach (var (_, bindings) in semanticQuery.GetAllGeneratorBindings())
        {
            foreach (var binding in bindings)
            {
                if (binding.Trigger.Name == generatorName)
                {
                    var trigger = binding.Trigger;
                    return diag with
                    {
                        FilePath = documentUri,
                        Line = trigger.LineStart,
                        Column = trigger.ColumnStart,
                        // Clear the original span so PositionConverter falls back to Line/Column.
                        Span = null,
                    };
                }
            }
        }

        return null;
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

    private const string DiagnosticCodePrefix = "SPY";
    private const int TransitionHintRangeStart = 470;
    private const int TransitionHintRangeEnd = 489;

    /// <summary>
    /// Returns true if the diagnostic code is a transition hint (SPY0470-SPY0489).
    /// </summary>
    internal static bool IsTransitionHintCode(string? code)
    {
        if (string.IsNullOrEmpty(code)
            || code.Length != 7
            || !code.StartsWith(DiagnosticCodePrefix))
        {
            return false;
        }

        if (!int.TryParse(code.AsSpan(DiagnosticCodePrefix.Length), out var n))
        {
            return false;
        }

        return n >= TransitionHintRangeStart && n <= TransitionHintRangeEnd;
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
