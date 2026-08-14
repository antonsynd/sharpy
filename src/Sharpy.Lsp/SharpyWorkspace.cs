using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Lsp;

/// <summary>
/// Document state for a single open document.
/// SourceText is the primary text buffer; string Text is a computed property.
/// </summary>
internal sealed class DocumentState : IDisposable
{
    public string Uri { get; }
    public SourceText SourceText { get; private set; }
    public string Text => SourceText.ToString();
    public int Version { get; private set; }
    public SemanticResult? CachedAnalysis { get; private set; }
    public ParseResult? CachedParseResult { get; private set; }

    private readonly SemaphoreSlim _analysisSemaphore = new(1, 1);
    private readonly SemaphoreSlim _parseSemaphore = new(1, 1);
    private readonly object _stateLock = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pendingCtsRegistry = new();
    private int _analysisVersion;
    private SemanticResult? _previousAnalysis;
    private ParseResult? _previousParseResult;

    // The options generation this document's caches belong to (#1375). _analysisVersion already
    // discards a result whose analysis STARTED before an invalidation; this discards one that
    // started after the invalidation but under options captured before it. Those are different
    // windows, and only the first was covered — see GetOrRunAnalysisAsync's write guard.
    private int _optionsGeneration;

    // The last analysis that actually produced a queryable tree, kept alongside the text it was
    // produced from (#1360). This is NOT _previousAnalysis: that one is "last completed" and is
    // overwritten by a failed parse, which is exactly the state a mid-edit buffer is in. A buffer
    // with a trailing dot analyzes to Ast=null/SymbolTable=null/SemanticQuery=null (measured:
    // SPY0101), so every handler that needs a tree has nothing to answer from at the instant an
    // editor asks. Holding the text too is what lets a consumer verify the stale tree still
    // describes the code it is being asked about instead of guessing — see
    // SharpyCompletionHandler's drift check.
    private SemanticResult? _lastGoodAnalysis;
    private SourceText? _lastGoodSourceText;

    /// <summary>
    /// Monotonically increasing version counter. Incremented on every edit.
    /// Used to detect and discard stale analysis results.
    /// </summary>
    public int AnalysisVersion
    {
        get { lock (_stateLock) { return _analysisVersion; } }
    }

    public DocumentState(string uri, string text, int version, int optionsGeneration)
    {
        Uri = uri;
        SourceText = new SourceText(text, uri);
        Version = version;
        _optionsGeneration = optionsGeneration;
    }

    public void Update(string text, int version)
    {
        lock (_stateLock)
        {
            SourceText = new SourceText(text, Uri);
            Version = version;
            CachedAnalysis = null;
            CachedParseResult = null;
            _analysisVersion++;
        }
    }

    /// <summary>
    /// Applies incremental text changes from LSP content change events.
    /// Each change with a Range is mapped to a TextChange; changes without
    /// a Range are treated as full-document replacements.
    /// </summary>
    public void ApplyIncrementalChanges(
        IReadOnlyList<(OmniSharp.Extensions.LanguageServer.Protocol.Models.Range? Range, string Text)> changes,
        int version)
    {
        lock (_stateLock)
        {
            var currentSource = SourceText;

            foreach (var (range, text) in changes)
            {
                if (range == null)
                {
                    // Full sync fallback: replace entire document
                    currentSource = new SourceText(text, Uri);
                }
                else
                {
                    // Convert LSP 0-based line/character to compiler 1-based, then to offset
                    var startOffset = currentSource.GetPosition(
                        range.Start.Line + 1, range.Start.Character + 1);
                    var endOffset = currentSource.GetPosition(
                        range.End.Line + 1, range.End.Character + 1);

                    var span = TextSpan.FromBounds(startOffset, endOffset);
                    var textChange = new TextChange(span, text);
                    currentSource = currentSource.WithChanges([textChange]);
                }
            }

            SourceText = currentSource;
            Version = version;
            CachedAnalysis = null;
            CachedParseResult = null;
            _analysisVersion++;
        }
    }

    /// <summary>
    /// Drops every cached analysis for this document so the next request re-analyzes it. Used when
    /// the workspace's analysis options change (a <c>.spyproj</c> load or a <c>sharpy.features</c>
    /// configuration change — #1149): results computed under the previous options are stale, and the
    /// incremental fast paths must not reuse them either, so the fingerprint baselines go too.
    /// Bumping the version means an analysis already in flight under the old options will not be
    /// cached when it completes.
    /// </summary>
    public void InvalidateAnalysis(int optionsGeneration)
    {
        lock (_stateLock)
        {
            _optionsGeneration = optionsGeneration;
            CachedAnalysis = null;
            CachedParseResult = null;
            _previousAnalysis = null;
            _previousParseResult = null;
            // The last-good snapshot goes too: it was produced under the PREVIOUS options, so
            // serving it after a feature/.spyproj change would reintroduce the staleness this
            // method exists to remove. An edit (Update/ApplyIncrementalChanges) deliberately keeps
            // it — surviving edits is the entire point — but an options change does not.
            _lastGoodAnalysis = null;
            _lastGoodSourceText = null;
            _analysisVersion++;
        }
    }

    /// <summary>
    /// The most recent analysis that produced a queryable tree, with the source text it was
    /// produced from. Both or neither: a consumer that cannot check drift should not be answering
    /// from a stale tree at all.
    /// </summary>
    public (SemanticResult Analysis, SourceText SourceText)? LastGoodAnalysis
    {
        get
        {
            lock (_stateLock)
            {
                return _lastGoodAnalysis != null && _lastGoodSourceText != null
                    ? (_lastGoodAnalysis, _lastGoodSourceText)
                    : null;
            }
        }
    }

    /// <summary>
    /// Whether an analysis that started at <paramref name="versionAtStart"/> under options
    /// generation <paramref name="optionsGeneration"/> may still be cached. Caller holds
    /// <see cref="_stateLock"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two independent staleness windows, and the second is the one #1375 reported. The version
    /// check discards a result whose analysis started BEFORE an invalidation and finished after.
    /// It cannot see the other order: a request that read <c>_workspaceOptions</c>, was descheduled,
    /// and resumed after the options changed starts its analysis with a version already bumped —
    /// so the version matches and the result caches, under options the workspace has abandoned.
    /// The observed symptom was the pre-change SPY0331 being served after
    /// <c>didChangeConfiguration</c> enabled the feature.
    /// </para>
    /// <para>
    /// Closing it needs both halves: options and generation are read together under
    /// <c>_optionsLock</c> (so they can never disagree), and the generation is re-checked here at
    /// the write. Either alone leaves the window open.
    /// </para>
    /// </remarks>
    private bool CanCache(int versionAtStart, int optionsGeneration)
        => _analysisVersion == versionAtStart && _optionsGeneration == optionsGeneration;

    /// <summary>
    /// Whether <paramref name="result"/> can answer a tree-shaped query. A failed parse yields a
    /// result with every one of these null, which is what makes "last completed" useless as a
    /// fallback and "last good" worth tracking separately.
    /// </summary>
    private static bool IsQueryable(SemanticResult result)
        => result.Ast != null && result.SymbolTable != null && result.SemanticQuery != null;

    /// <summary>
    /// Records <paramref name="result"/> as the last-good snapshot when it is queryable. Caller
    /// holds <see cref="_stateLock"/>. Called from every cache-write site so a result that reaches
    /// the cache by ANY path — full analysis, unchanged-AST reuse, or scoped body re-check — is a
    /// candidate; missing one would make the fallback silently depend on which path ran.
    /// </summary>
    private void RecordLastGood(SemanticResult result, SourceText sourceText)
    {
        if (IsQueryable(result))
        {
            _lastGoodAnalysis = result;
            _lastGoodSourceText = sourceText;
        }
    }

    /// <summary>
    /// Returns the current SourceText snapshot.
    /// </summary>
    public SourceText GetSourceTextSnapshot()
    {
        lock (_stateLock)
        {
            return SourceText;
        }
    }

    /// <param name="stageMetrics">
    /// Optional per-call stage attribution (#1140), forwarded to <see cref="CompilerApi.Analyze"/>.
    /// Collects nothing on the incremental fast paths below, which never reach the compiler call —
    /// an empty breakdown is the honest report that no full analysis ran.
    /// </param>
    /// <param name="optionsGeneration">
    /// The generation <paramref name="options"/> was read at, captured atomically with it (#1375).
    /// A result produced under a superseded generation is returned to its caller but never cached:
    /// it describes the file under options the workspace has already moved on from.
    /// </param>
    public async Task<SemanticResult> GetOrRunAnalysisAsync(CompilerApi api, CompilerOptions options,
        int optionsGeneration, CancellationToken ct, CompilationMetrics? stageMetrics = null)
    {
        lock (_stateLock)
        {
            if (CachedAnalysis != null)
                return CachedAnalysis;
        }

        await _analysisSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string text;
            int versionAtStart;
            SemanticResult? previousAnalysis;
            ParseResult? previousParse;
            SourceText sourceTextAtStart;
            lock (_stateLock)
            {
                // Double-check after acquiring lock
                if (CachedAnalysis != null)
                    return CachedAnalysis;
                sourceTextAtStart = SourceText;
                text = sourceTextAtStart.ToString();
                versionAtStart = _analysisVersion;
                previousAnalysis = _previousAnalysis;
                previousParse = _previousParseResult;
            }

            using var scope = new CancellableAnalysisScope(_pendingCtsRegistry, Uri, ct);

            // Incremental fast paths compare a fresh parse against the previous analysis's AST. A
            // fresh document has no previous analysis to fingerprint against, so parsing up front is
            // pure waste there (#1137): api.Analyze reparses internally, and its entry AST feeds the
            // parse cache below. Only parse up front when a prior analysis exists to compare against.
            ParseResult? newParse = null;
            if (previousAnalysis != null && previousParse?.Ast != null)
                newParse = api.Parse(text, scope.Token);

            if (previousAnalysis != null && previousParse?.Ast != null && newParse?.Ast != null)
            {
                var change = AstFingerprint.Classify(previousParse.Ast, newParse.Ast);
                if (change.Kind == AstChangeKind.NoChange)
                {
                    // AST is structurally identical — reuse the previous result
                    lock (_stateLock)
                    {
                        if (CanCache(versionAtStart, optionsGeneration))
                        {
                            _previousParseResult = newParse;
                            CachedAnalysis = previousAnalysis;
                            CachedParseResult = newParse;
                            RecordLastGood(previousAnalysis, sourceTextAtStart);
                        }
                    }
                    return previousAnalysis;
                }
                if (change.Kind == AstChangeKind.BodyOnly)
                {
                    // Function body change only — use scoped re-check
                    var partialResult = await Task.Run(
                        () => ScopedTypeChecker.RecheckFunction(api, text, scope.Token),
                        scope.Token
                    ).ConfigureAwait(false);

                    if (partialResult != null)
                    {
                        lock (_stateLock)
                        {
                            if (CanCache(versionAtStart, optionsGeneration))
                            {
                                _previousAnalysis = partialResult;
                                _previousParseResult = newParse;
                                CachedAnalysis = partialResult;
                                CachedParseResult = newParse;
                                RecordLastGood(partialResult, sourceTextAtStart);
                            }
                            // Note: If version changed, we still return the result (best-effort).
                            // The next analysis cycle will produce a fresh result for the new version.
                        }
                        return partialResult;
                    }
                    // Fall through to full analysis if partial failed
                }
            }

            var result = await Task.Run(
                () => api.Analyze(text, options, stageMetrics, scope.Token),
                scope.Token
            ).ConfigureAwait(false);

            // The parse result to remember for the next edit's fingerprint and for parse-only
            // handlers. The incremental path already parsed (newParse); the fresh path derives it
            // from the analysis's entry AST — structurally identical to a standalone parse, with
            // the syntactic diagnostic slice projected so Success/Diagnostics stay honest. A null
            // AST leaves the caches cold, matching an on-demand parse.
            var parseForCache = newParse ?? ParseResultFromAnalysis(result);

            lock (_stateLock)
            {
                // Only cache if document hasn't changed during analysis
                // and analysis wasn't cancelled (SPY0901). Cancelled results
                // would poison the cache — the next caller must retry.
                if (CanCache(versionAtStart, optionsGeneration) && !IsCancelledResult(result))
                {
                    _previousAnalysis = result;
                    _previousParseResult = parseForCache;
                    CachedAnalysis = result;
                    CachedParseResult = parseForCache;
                    RecordLastGood(result, sourceTextAtStart);
                }
            }
            return result;
        }
        finally
        {
            _analysisSemaphore.Release();
        }
    }

    /// <summary>
    /// Returns a cached parse result or runs a parse-only pass.
    /// Parse is stateless and much faster than full semantic analysis.
    /// </summary>
    public async Task<ParseResult?> GetOrRunParseAsync(CompilerApi api, CancellationToken ct)
    {
        lock (_stateLock)
        {
            if (CachedParseResult != null)
                return CachedParseResult;
        }

        await _parseSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string text;
            int versionAtStart;
            lock (_stateLock)
            {
                if (CachedParseResult != null)
                    return CachedParseResult;
                text = SourceText.ToString();
                versionAtStart = _analysisVersion;
            }

            var result = api.Parse(text, ct);

            lock (_stateLock)
            {
                // Only cache if document hasn't changed during parse
                if (_analysisVersion == versionAtStart)
                {
                    CachedParseResult = result;
                }
            }
            return result;
        }
        finally
        {
            _parseSemaphore.Release();
        }
    }

    /// <summary>
    /// Builds a <see cref="ParseResult"/> view over an analysis result's entry AST, for the fresh
    /// document path where no standalone parse was run (#1137). The syntactic slice of the analysis
    /// diagnostics (lexer/parser phases) is projected onto the result so
    /// <see cref="ParseResult.Success"/> and <see cref="ParseResult.Diagnostics"/> honor their
    /// contract on error-recovered documents — today's consumers (the incremental fingerprint and
    /// the parse-only LSP handlers) read only <see cref="ParseResult.Ast"/>, but a cached result
    /// claiming success on a syntax-error document would mislead the next consumer that trusts it.
    /// Returns null when analysis produced no AST, leaving the parse cache cold so a later
    /// parse-only request reparses on demand (identical to how a fresh parse would populate it).
    /// </summary>
    private static ParseResult? ParseResultFromAnalysis(SemanticResult result)
    {
        if (result.Ast == null)
            return null;

        var syntaxDiagnostics = result.Diagnostics
            .Where(d => d.Phase is CompilerPhase.Lexer or CompilerPhase.Parser)
            .ToArray();
        return new ParseResult
        {
            Success = !syntaxDiagnostics.Any(d => d.IsError),
            Diagnostics = syntaxDiagnostics,
            Ast = result.Ast
        };
    }

    private static bool IsCancelledResult(SemanticResult result)
    {
        return result.Diagnostics.Any(
            d => d.Code == DiagnosticCodes.Infrastructure.CompilationCancelled);
    }

    public void Dispose()
    {
        foreach (var kvp in _pendingCtsRegistry)
        {
            try
            { kvp.Value.Cancel(); }
            catch (ObjectDisposedException) { }
            kvp.Value.Dispose();
        }
        _pendingCtsRegistry.Clear();
        _analysisSemaphore.Dispose();
        _parseSemaphore.Dispose();
    }
}

/// <summary>
/// Manages open document state and cached analysis results for the LSP server.
/// Thread-safe via ConcurrentDictionary and per-document SemaphoreSlim.
/// </summary>
internal sealed class SharpyWorkspace : IDisposable
{
    private readonly ConcurrentDictionary<string, DocumentState> _documents = new();
    private readonly CompilerApi _api;
    private readonly ILogger<SharpyWorkspace> _logger;

    // Workspace-level compiler options passed to analysis, built through the one options seam every
    // entry point uses (#1144) from the same two sources the CLI reads: the workspace's .spyproj
    // (pushed in by LanguageService as it discovers or reloads it) and the editor's `sharpy.features`
    // workspace configuration (#1149). Until #1149 this was a constant with no features, so gated
    // syntax the CLI accepted under <Features>/--enable-feature red-squiggled SPY0331 in-editor.
    // Written under _optionsLock, read on request threads.
    private readonly object _optionsLock = new();
    private ProjectConfig? _projectConfig;
    private IReadOnlyList<string> _configuredFeatures = Array.Empty<string>();
    private volatile CompilerOptions _workspaceOptions = CompilerOptionsFactory.ForLsp();

    // Incremented whenever _workspaceOptions is replaced, under _optionsLock, so the pair can be
    // read atomically (#1375). `volatile` gives visibility, not atomicity of a read-then-use: a
    // request could read the old options, be descheduled past a whole RebuildOptions, and resume
    // to analyse under options nobody uses any more. The generation travels with the options into
    // DocumentState, which refuses to cache a result stamped with a superseded one.
    private int _optionsGeneration;

    // Debounce timers per document
    private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new();
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Fires when a document has been analyzed (after debounce).
    /// The string parameter is the document URI.
    /// </summary>
    public event Func<string, SemanticResult, Task>? DocumentAnalyzed;

    public SharpyWorkspace(CompilerApi api, ILogger<SharpyWorkspace> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>
    /// The options every analysis in this workspace runs with. Also handed to project-mode analysis
    /// so both LSP paths gate features and apply warning configuration identically.
    /// </summary>
    public CompilerOptions Options => _workspaceOptions;

    /// <summary>
    /// Applies the workspace's <c>.spyproj</c> (or null when none was found or it failed to load).
    /// Its <c>&lt;Features&gt;</c>, warning settings, references and module paths become the base of
    /// the analysis options, with editor configuration layered over them.
    /// </summary>
    /// <returns>True if the resolved options changed (open documents were re-analyzed).</returns>
    public bool SetProjectConfig(ProjectConfig? config)
    {
        lock (_optionsLock)
        {
            _projectConfig = config;
            return RebuildOptions();
        }
    }

    /// <summary>
    /// Applies the experimental features requested by LSP workspace configuration
    /// (<c>sharpy.features</c>). Unknown names are reported and dropped — never silently enabled or
    /// silently ignored — mirroring how every other boundary validates against
    /// <see cref="FeatureFlags.KnownFeatures"/>.
    /// </summary>
    /// <returns>True if the resolved options changed (open documents were re-analyzed).</returns>
    public bool SetConfiguredFeatures(IEnumerable<string>? featureNames)
    {
        var known = new List<string>();
        foreach (var name in featureNames ?? Enumerable.Empty<string>())
        {
            if (FeatureFlags.TryValidate(name, out var error))
                known.Add(name);
            else
                _logger.LogError("Ignoring sharpy.features entry: {Error}", error);
        }

        lock (_optionsLock)
        {
            _configuredFeatures = known;
            return RebuildOptions();
        }
    }

    /// <summary>
    /// Rebuilds the analysis options from the current sources and, when they actually changed,
    /// invalidates every cached analysis and re-analyzes the open documents so the editor's
    /// diagnostics match what the new options produce. Caller holds <see cref="_optionsLock"/>.
    /// </summary>
    private bool RebuildOptions()
    {
        var rebuilt = CompilerOptionsFactory.ForLsp(_projectConfig, FeatureFlags.None.Enable(_configuredFeatures));
        if (SameAnalysisInputs(_workspaceOptions, rebuilt))
            return false;

        _workspaceOptions = rebuilt;
        // Bumped under _optionsLock together with the options themselves, so a reader that takes
        // both under the lock can never see one without the other (#1375).
        _optionsGeneration++;
        _logger.LogInformation(
            "Workspace analysis options updated: features=[{Features}] warningsAsErrors={WarningsAsErrors} noWarn=[{NoWarn}]",
            string.Join(", ", rebuilt.Features.EnabledFeatures),
            rebuilt.WarningsAsErrors,
            string.Join(", ", rebuilt.SuppressedWarnings.OrderBy(w => w, StringComparer.Ordinal)));

        foreach (var kvp in _documents)
        {
            kvp.Value.InvalidateAnalysis(_optionsGeneration);
            ScheduleAnalysis(kvp.Key);
        }

        return true;
    }

    /// <summary>
    /// Whether two option sets would produce the same analysis. Only the members LSP analysis reads
    /// are compared; <see cref="CompilerOptions"/> has no value equality of its own, and rebuilding
    /// from an unchanged <c>.spyproj</c> must not throw away every cached analysis.
    /// </summary>
    private static bool SameAnalysisInputs(CompilerOptions a, CompilerOptions b)
        => a.OutputType == b.OutputType
            && a.WarningsAsErrors == b.WarningsAsErrors
            // A project that flips <TestHost> changes what a `@test` body LOWERS to (#1495), and a
            // lowering change is an analysis change: the framework-free arm can report diagnostics
            // the Xunit arm cannot, and the reverse.
            && a.TargetsTestHost == b.TargetsTestHost
            && a.MaxErrors == b.MaxErrors
            && a.SuppressedWarnings.SetEquals(b.SuppressedWarnings)
            && a.Features.EnabledFeatures.SequenceEqual(b.Features.EnabledFeatures, StringComparer.Ordinal)
            && SameSequence(a.References, b.References)
            && SameSequence(a.ModulePaths, b.ModulePaths);

    private static bool SameSequence(string[]? a, string[]? b)
        => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>(), StringComparer.Ordinal);

    public void OpenDocument(string uri, string text, int version)
    {
        // A document opened after an options change starts in the CURRENT generation; one opened
        // before keeps whatever generation its last invalidation set.
        int generationAtOpen;
        lock (_optionsLock)
            generationAtOpen = _optionsGeneration;
        var state = new DocumentState(uri, text, version, generationAtOpen);
        _documents[uri] = state;
        ScheduleAnalysis(uri);
    }

    public void UpdateDocument(string uri, string text, int version)
    {
        if (_documents.TryGetValue(uri, out var state))
        {
            state.Update(text, version);
            ScheduleAnalysis(uri);
        }
    }

    public void ApplyChanges(
        string uri,
        IReadOnlyList<(OmniSharp.Extensions.LanguageServer.Protocol.Models.Range? Range, string Text)> changes,
        int version)
    {
        if (_documents.TryGetValue(uri, out var state))
        {
            state.ApplyIncrementalChanges(changes, version);
            ScheduleAnalysis(uri);
        }
    }

    public void CloseDocument(string uri)
    {
        if (_documents.TryRemove(uri, out var state))
        {
            state.Dispose();
        }

        if (_debounceTimers.TryRemove(uri, out var timer))
        {
            timer.Dispose();
        }
    }

    /// <param name="uri">The document to analyze.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <param name="stageMetrics">
    /// Optional per-call stage attribution (#1140). Null on every production request except when
    /// debug logging is on; see <see cref="FireAndForgetAnalysis"/>.
    /// </param>
    public async Task<SemanticResult?> GetAnalysisAsync(string uri, CancellationToken ct = default,
        CompilationMetrics? stageMetrics = null)
    {
        if (_documents.TryGetValue(uri, out var state))
        {
            // Options and their generation are read together under the lock; passing them
            // separately from two unsynchronised reads is the race this closes (#1375).
            CompilerOptions options;
            int generation;
            lock (_optionsLock)
            {
                options = _workspaceOptions;
                generation = _optionsGeneration;
            }

            return await state.GetOrRunAnalysisAsync(_api, options, generation, ct, stageMetrics)
                .ConfigureAwait(false);
        }
        return null;
    }

    public async Task<ParseResult?> GetParseResultAsync(string uri, CancellationToken ct = default)
    {
        if (_documents.TryGetValue(uri, out var state))
        {
            return await state.GetOrRunParseAsync(_api, ct).ConfigureAwait(false);
        }
        return null;
    }

    /// <summary>
    /// The document's last analysis that produced a queryable tree, with the text it came from
    /// (#1360). Deliberately a SEPARATE accessor: <see cref="GetAnalysisAsync"/> must never fall
    /// back to this. That method is what <c>DiagnosticPublisher</c> publishes from and what
    /// <c>FrontEndParityTests</c> compares against the <c>CompilerApi.Analyze</c> baseline, so a
    /// fallback there would serve stale diagnostics for every non-parsing document — a parity
    /// violation by construction, and visible to users as diagnostics for text they already fixed.
    /// Only handlers that can tolerate (and check) staleness may call this.
    /// </summary>
    public (SemanticResult Analysis, SourceText SourceText)? GetLastGoodAnalysis(string uri)
    {
        return _documents.TryGetValue(uri, out var state) ? state.LastGoodAnalysis : null;
    }

    public DocumentState? GetDocument(string uri)
    {
        _documents.TryGetValue(uri, out var state);
        return state;
    }

    public SourceText? GetSourceText(string uri)
    {
        if (_documents.TryGetValue(uri, out var state))
        {
            return state.GetSourceTextSnapshot();
        }
        return null;
    }

    /// <summary>
    /// Returns all open document URIs.
    /// </summary>
    public IReadOnlyCollection<string> GetAllDocumentUris()
    {
        return (IReadOnlyCollection<string>)_documents.Keys;
    }

    private void ScheduleAnalysis(string uri)
    {
        // Reuse existing timer via GetOrAdd + Change to avoid timer leaks.
        // GetOrAdd creates a dormant timer (infinite delay) on first call per URI;
        // subsequent calls reset it. This avoids the CAS-race leak that
        // AddOrUpdate's factory-based overloads can cause.
        var timer = _debounceTimers.GetOrAdd(uri,
            _ => new Timer(_ => FireAndForgetAnalysis(uri),
                null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan));

        try
        { timer.Change(DebounceDelay, Timeout.InfiniteTimeSpan); }
        catch (ObjectDisposedException) { /* Timer was disposed by CloseDocument race */ }
    }

    // Timer callbacks require void return; the full try-catch ensures no exceptions escape.
#pragma warning disable VSTHRD100
    private async void FireAndForgetAnalysis(string uri)
#pragma warning restore VSTHRD100
    {
        try
        {
            // Per-stage attribution (#1140) is opt-in because it allocates a metrics object and
            // brackets every pipeline stage — on the very path whose latency is being measured.
            // The server's minimum level defaults to Information, so this stays null on every
            // keystroke unless someone asks for Debug (--log-level / SHARPY_LSP_LOG_LEVEL, #1225)
            // to investigate.
            var stageMetrics = _logger.IsEnabled(LogLevel.Debug)
                ? new CompilationMetrics(fileName: uri)
                : null;

            // Measure change→publish wall time for the single-file path: analysis plus the
            // DocumentAnalyzed handler that publishes diagnostics. Recorded so the LSP
            // incremental-frontend work (#1099) starts from data, not intuition.
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // The publish half of #1375. Refusing to CACHE a superseded result is not enough on its
            // own: this callback also raises DocumentAnalyzed, which is what DiagnosticPublisher
            // publishes from, so an analysis overtaken by a configuration change would still put
            // pre-change diagnostics on screen once. RebuildOptions re-arms this timer, so the
            // correct diagnostics are already coming — dropping the overtaken publish costs nothing
            // and is the difference between a transient wrong squiggle and none.
            int generationAtStart;
            lock (_optionsLock)
                generationAtStart = _optionsGeneration;

            var result = await GetAnalysisAsync(uri, CancellationToken.None, stageMetrics)
                .ConfigureAwait(false);

            int generationNow;
            lock (_optionsLock)
                generationNow = _optionsGeneration;

            if (generationNow != generationAtStart)
            {
                _logger.LogDebug(
                    "Dropping analysis of {Uri}: options moved from generation {Start} to {Now} "
                    + "while it ran; a re-analysis is already scheduled.",
                    uri, generationAtStart, generationNow);
                return;
            }

            if (result != null)
            {
                var handler = DocumentAnalyzed;
                if (handler != null)
                {
                    await handler(uri, result).ConfigureAwait(false);
                }
                stopwatch.Stop();
                AnalysisLatencyLog.LogLatency(
                    _logger,
                    AnalysisLatencyLog.SingleFilePath,
                    affectedFiles: 1,
                    stopwatch.Elapsed.TotalMilliseconds);

                // Empty when an incremental fast path served the edit without a compiler call —
                // there are no stages to attribute, and claiming otherwise would be a lie.
                if (stageMetrics is { Phases.Count: > 0 })
                {
                    AnalysisLatencyLog.LogStages(
                        _logger,
                        AnalysisLatencyLog.SingleFilePath,
                        stageMetrics.Phases,
                        stopwatch.Elapsed.TotalMilliseconds);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when document changes rapidly
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document {Uri}", uri);
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _documents)
        {
            kvp.Value.Dispose();
        }
        _documents.Clear();

        foreach (var kvp in _debounceTimers)
        {
            kvp.Value.Dispose();
        }
        _debounceTimers.Clear();
    }
}
