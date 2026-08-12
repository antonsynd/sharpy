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

        return ApplyProblemCap(result, configuration?.MaxNumberOfProblems);
    }

    /// <summary>
    /// Applies <c>sharpy.lsp.maxNumberOfProblems</c>. Returns <paramref name="diagnostics"/>
    /// unchanged when there is no cap or the document is already under it.
    /// </summary>
    /// <remarks>
    /// The dropped diagnostics are the <i>least severe</i> ones, not simply the last ones: the
    /// compiler emits in phase order, and validators that produce hints (transition hints, order 56)
    /// run well before validators that produce errors, so a positional truncation could hide every
    /// error behind a wall of hints. Within one severity the compiler's order is kept, and the
    /// surviving diagnostics are published in their original order, so a cap never reshuffles what
    /// the editor already showed — it only takes from the bottom of the severity ladder.
    /// </remarks>
    internal static System.Collections.Generic.List<Diagnostic> ApplyProblemCap(
        System.Collections.Generic.List<Diagnostic> diagnostics, int? maxNumberOfProblems)
    {
        if (maxNumberOfProblems is not { } cap || diagnostics.Count <= cap)
            return diagnostics;

        if (cap <= 0)
            return new System.Collections.Generic.List<Diagnostic>();

        var kept = diagnostics
            .Select((diagnostic, index) => (diagnostic, index))
            .OrderBy(entry => SeverityRank(entry.diagnostic.Severity))
            .ThenBy(entry => entry.index)
            .Take(cap)
            .OrderBy(entry => entry.index)
            .Select(entry => entry.diagnostic)
            .ToList();

        return kept;
    }

    /// <summary>
    /// Orders severities most-severe-first for the problem cap. Diagnostics with no severity are
    /// ranked with warnings — the LSP treats an absent severity as client's choice, and dropping
    /// them ahead of hints would be a guess in the wrong direction.
    /// </summary>
    private static int SeverityRank(DiagnosticSeverity? severity) => severity switch
    {
        DiagnosticSeverity.Error => 0,
        DiagnosticSeverity.Warning => 1,
        null => 1,
        DiagnosticSeverity.Information => 2,
        DiagnosticSeverity.Hint => 3,
        _ => 4,
    };

    internal static Diagnostic ConvertDiagnostic(CompilerDiagnostic diag, SourceText? sourceText, CompilerDiagnostic? generatedOrigin = null)
    {
        var range = PositionConverter.DiagnosticToRange(diag, sourceText);

        // Two sources of related information, both rendered by editors as a clickable "see also"
        // beside the primary squiggle: the rerouted-from-generated-source origin, and the
        // diagnostic's own structured related locations (e.g. the FIRST of the two declarations a
        // name collision refuses, #1388).
        var related = new List<DiagnosticRelatedInformation>();

        if (generatedOrigin != null)
        {
            // Attach the original (synthetic) location as related information so editors
            // can show "Also see: <generated:Foo:Bar>" alongside the rerouted diagnostic.
            var origRange = PositionConverter.DiagnosticToRange(generatedOrigin, sourceText: null);
            var originPath = generatedOrigin.FilePath ?? "<generated>";
            related.Add(new DiagnosticRelatedInformation
            {
                Location = new Location
                {
                    Uri = DocumentUri.From(new Uri($"sharpy-generated:{Uri.EscapeDataString(originPath)}", UriKind.Absolute)),
                    Range = origRange,
                },
                Message = $"In generated source {originPath}",
            });
        }

        if (diag.RelatedLocations is { Count: > 0 } relatedLocations)
        {
            foreach (var location in relatedLocations)
            {
                var path = location.FilePath ?? diag.FilePath;
                if (string.IsNullOrEmpty(path))
                    continue;

                // A related location in the file being published shares its SourceText, so a
                // recorded Span resolves to a real range; one in another file has only line/column.
                var sameFile = string.Equals(path, diag.FilePath, StringComparison.Ordinal);
                var locationRange = PositionConverter.DiagnosticToRange(
                    new CompilerDiagnostic(
                        location.Message,
                        diag.Severity,
                        location.Line,
                        location.Column,
                        path,
                        Span: location.Span),
                    sameFile ? sourceText : null);

                related.Add(new DiagnosticRelatedInformation
                {
                    Location = new Location
                    {
                        Uri = DocumentUri.FromFileSystemPath(path),
                        Range = locationRange,
                    },
                    Message = location.Message,
                });
            }
        }

        Container<DiagnosticRelatedInformation>? relatedInfo =
            related.Count > 0 ? new Container<DiagnosticRelatedInformation>(related) : null;

        var lspDiag = new Diagnostic
        {
            Range = range,
            Severity = ConvertSeverity(diag.Severity),
            Message = diag.Message,
            Source = BuildSource(diag),
            Code = !string.IsNullOrEmpty(diag.Code)
                ? new DiagnosticCode(diag.Code)
                : default,
            Tags = GetDiagnosticTags(diag),
            Data = BuildData(diag),
            RelatedInformation = relatedInfo,
        };

        return lspDiag;
    }

    /// <summary>
    /// The default <c>source</c> label applied to Sharpy diagnostics.
    /// </summary>
    internal const string DefaultSource = "sharpy";

    /// <summary>
    /// Data key under which the originating <see cref="CompilerPhase"/> is published to editors.
    /// </summary>
    internal const string PhaseDataKey = "phase";

    /// <summary>
    /// Builds the LSP <c>source</c> label. The producing validator (when a diagnostic carries
    /// one via <see cref="DiagnosticBag.ProducerDataKey"/>) is folded into the source as
    /// <c>sharpy:&lt;producer&gt;</c> so editors surface provenance in the diagnostic's origin
    /// column. Diagnostics without a producer keep the plain <see cref="DefaultSource"/> label.
    /// </summary>
    internal static string BuildSource(CompilerDiagnostic diag)
    {
        if (diag.Data != null
            && diag.Data.TryGetValue(DiagnosticBag.ProducerDataKey, out var producer)
            && !string.IsNullOrEmpty(producer))
        {
            return $"{DefaultSource}:{producer}";
        }

        return DefaultSource;
    }

    /// <summary>
    /// Builds the published <c>data</c> payload. The compiler's <see cref="CompilerDiagnostic.Data"/>
    /// (which includes the producer, when present) is forwarded as-is, and the originating
    /// <see cref="CompilerPhase"/> is added under <see cref="PhaseDataKey"/> unless it is
    /// <see cref="CompilerPhase.Unknown"/>. Returns <c>null</c> when there is nothing to publish,
    /// preserving the historic behavior for diagnostics that carry no provenance.
    /// </summary>
    internal static JObject? BuildData(CompilerDiagnostic diag)
    {
        var hasData = diag.Data is { Count: > 0 };
        var hasPhase = diag.Phase != CompilerPhase.Unknown;
        if (!hasData && !hasPhase)
        {
            return null;
        }

        var payload = hasData ? JObject.FromObject(diag.Data!) : new JObject();
        if (hasPhase)
        {
            payload[PhaseDataKey] = diag.Phase.ToString();
        }

        return payload;
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
