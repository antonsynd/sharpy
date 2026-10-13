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
using Sharpy.Compiler.Utilities;

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

    /// <summary>
    /// Publishes <paramref name="result"/>'s diagnostics, each on the file it belongs to.
    /// </summary>
    /// <param name="uri">The document the analysis was requested for. It always receives a
    /// notification, empty when none of the diagnostics are its own (that clears stale squiggles).</param>
    /// <param name="result">The analysis. Its bag may hold diagnostics of other files — the
    /// single-file door returns the whole bag of its synthetic project, import closure included.</param>
    /// <param name="sourceText">The text of <paramref name="uri"/>; only that document's own
    /// diagnostics are positioned against it. Null → line/column ranges.</param>
    /// <param name="sourceTextFor">Resolves another file's text from its document URI (the string
    /// that file's notification is published on). Null, or a null answer → line/column ranges for
    /// that file's diagnostics.</param>
    public void PublishDiagnostics(
        string uri,
        SemanticResult result,
        SourceText? sourceText,
        Func<string, SourceText?>? sourceTextFor = null)
    {
        foreach (var publication in BuildPublications(uri, result, sourceText, sourceTextFor, _configuration))
        {
            _server.TextDocument.PublishDiagnostics(publication);
        }
    }

    /// <summary>
    /// Partitions <paramref name="result"/>'s diagnostics by the file they belong to and converts
    /// each partition against that file's own text (#2242). The requesting document's notification
    /// comes first and is always present; every other file a diagnostic names gets one notification
    /// on its own URI, in first-appearance order.
    /// </summary>
    /// <remarks>
    /// <para>Generated-source diagnostics are rerouted to their trigger decorator FIRST — the reroute
    /// stamps this document's URI, so it lands in this document's partition — and partitioned
    /// after.</para>
    /// <para>Ownership, by <see cref="CompilerDiagnostic.FilePath"/>: null/empty, the document's own
    /// URI, or a path (or file URI) that normalizes to the document's path → this document,
    /// positioned against <paramref name="sourceText"/>. Another path → that file's notification,
    /// positioned against <paramref name="sourceTextFor"/>'s answer or by line/column. A name that is
    /// no file (<c>&lt;source&gt;</c>, a path-less buffer's virtual name; an unrerouted
    /// <c>&lt;generated:…&gt;</c> path) → this document, the only place it can be shown — positioned
    /// against <paramref name="sourceText"/> unless it is generated source, whose positions index the
    /// generated text and are published by line/column only.</para>
    /// <para>The problem cap (<c>sharpy.lsp.maxNumberOfProblems</c>) applies per notification; the
    /// setting is per file. This method never clears a file it published to earlier and does not
    /// name now — which route owns a file's squiggles is the caller's decision.</para>
    /// </remarks>
    internal static IReadOnlyList<PublishDiagnosticsParams> BuildPublications(
        string uri,
        SemanticResult result,
        SourceText? sourceText,
        Func<string, SourceText?>? sourceTextFor,
        LspConfiguration? configuration)
    {
        var documentPath = LanguageService.UriToFilePath(uri);
        var documentKey = documentPath != null ? PathNormalizer.Normalize(documentPath) : null;

        var own = new System.Collections.Generic.List<Diagnostic>();
        var foreignOrder = new System.Collections.Generic.List<string>();
        var foreign = new Dictionary<string, (string Uri, System.Collections.Generic.List<CompilerDiagnostic> Diagnostics)>(
            StringComparer.Ordinal);

        foreach (var diag in result.Diagnostics)
        {
            if (IsFilteredTransitionHint(diag, configuration))
                continue;

            var rerouted = TryRerouteGeneratedDiagnostic(diag, result.SemanticQuery, uri);
            var effective = rerouted ?? diag;
            var generatedOrigin = rerouted != null ? diag : null;

            var filePath = effective.FilePath;
            if (string.IsNullOrEmpty(filePath) || string.Equals(filePath, uri, StringComparison.Ordinal))
            {
                own.Add(ConvertDiagnostic(effective, sourceText, generatedOrigin));
                continue;
            }

            var localPath = LanguageService.UriToFilePath(filePath);
            if (localPath == null)
            {
                // Names no file, so there is nowhere else to publish it. Generated-source positions
                // index the generated text, never this one.
                own.Add(ConvertDiagnostic(effective, IsGeneratedFilePath(filePath) ? null : sourceText, generatedOrigin));
                continue;
            }

            var key = PathNormalizer.Normalize(localPath);
            if (string.Equals(key, documentKey, StringComparison.Ordinal))
            {
                own.Add(ConvertDiagnostic(effective, sourceText, generatedOrigin));
                continue;
            }

            if (!foreign.TryGetValue(key, out var partition))
            {
                partition = (LanguageService.FilePathToUri(localPath), new System.Collections.Generic.List<CompilerDiagnostic>());
                foreign[key] = partition;
                foreignOrder.Add(key);
            }

            partition.Diagnostics.Add(effective);
        }

        var publications = new System.Collections.Generic.List<PublishDiagnosticsParams>(1 + foreignOrder.Count)
        {
            CreatePublication(uri, ApplyProblemCap(own, configuration?.MaxNumberOfProblems)),
        };

        foreach (var key in foreignOrder)
        {
            var (foreignUri, diagnostics) = foreign[key];
            var foreignText = sourceTextFor?.Invoke(foreignUri);
            var converted = new System.Collections.Generic.List<Diagnostic>(diagnostics.Count);
            foreach (var diag in diagnostics)
            {
                converted.Add(ConvertDiagnostic(diag, foreignText));
            }

            publications.Add(CreatePublication(foreignUri, ApplyProblemCap(converted, configuration?.MaxNumberOfProblems)));
        }

        return publications;
    }

    /// <summary>
    /// A file's text as it is on disk, or null when it cannot be read. The text resolver for a
    /// route whose analysis read the other files from disk — the single-file door reads its import
    /// closure from disk, never from other open buffers — so a sibling's spans are positioned
    /// against the text they were recorded in.
    /// </summary>
    /// <remarks>
    /// An unreadable file loses no diagnostic: its diagnostics are still published, positioned by
    /// their recorded line/column instead of their span.
    /// </remarks>
    internal static SourceText? ReadSourceTextFromDisk(string uri)
    {
        var path = LanguageService.UriToFilePath(uri);
        if (path == null)
            return null;

        try
        {
            return SourceText.FromFile(path);
        }
        catch (IOException)
        {
            // Includes FileNotFoundException and DirectoryNotFoundException (deleted since analysis).
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static PublishDiagnosticsParams CreatePublication(
        string uri, System.Collections.Generic.List<Diagnostic> diagnostics)
        => new()
        {
            Uri = DocumentUri.From(uri),
            Diagnostics = new Container<Diagnostic>(diagnostics),
        };

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

        foreach (var diag in diagnostics)
        {
            if (IsFilteredTransitionHint(diag, configuration))
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
    /// Filters out transition hints when they are disabled. Transition hints are Hint-severity
    /// diagnostics in the SPY0470-SPY0479 range. BOTH conjuncts are load-bearing: the severity
    /// check is what kept the over-wide range (#1466) from suppressing the SPY0480-0484 warnings,
    /// and it stays the authority even now that the range is right — severity is never inferred
    /// from a code number here.
    /// </summary>
    private static bool IsFilteredTransitionHint(CompilerDiagnostic diag, LspConfiguration? configuration)
        => !(configuration?.TransitionHintsEnabled ?? true)
            && diag.Severity == CompilerDiagnosticSeverity.Hint
            && IsTransitionHintCode(diag.Code);

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

    // The transition band ends at 0479, not 0489. SPY0480-SPY0489 is the validation-WARNING
    // overflow band (SPY0480-0484 active — MustUseValueDiscarded, UnusedSuppression,
    // InvalidSuppressionCode, BuiltinNameShadowedInValuePosition, BuiltinRebornByExplicitImport),
    // whose own declaration says severity is "set explicitly at emission via AddWarning, never
    // inferred from the code range". This predicate inferred from the range anyway and claimed
    // all five (#1466). Nothing was actually suppressed — the only caller also requires
    // Hint severity and those five emit at Warning — so the bug was latent, a trap for the next
    // caller rather than a live defect. It is fixed here at the source of the claim.
    private const int TransitionHintRangeEnd = 479;

    /// <summary>
    /// Returns true if the diagnostic code is a transition hint (SPY0470-SPY0479).
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
