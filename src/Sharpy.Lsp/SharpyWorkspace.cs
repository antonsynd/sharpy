using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;
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

    /// <summary>
    /// Monotonically increasing version counter. Incremented on every edit.
    /// Used to detect and discard stale analysis results.
    /// </summary>
    public int AnalysisVersion
    {
        get { lock (_stateLock) { return _analysisVersion; } }
    }

    public DocumentState(string uri, string text, int version)
    {
        Uri = uri;
        SourceText = new SourceText(text, uri);
        Version = version;
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
    /// Returns the current SourceText snapshot.
    /// </summary>
    public SourceText GetSourceTextSnapshot()
    {
        lock (_stateLock)
        {
            return SourceText;
        }
    }

    public async Task<SemanticResult> GetOrRunAnalysisAsync(CompilerApi api, CompilerOptions options, CancellationToken ct)
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
            lock (_stateLock)
            {
                // Double-check after acquiring lock
                if (CachedAnalysis != null)
                    return CachedAnalysis;
                text = SourceText.ToString();
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
                        if (_analysisVersion == versionAtStart)
                        {
                            _previousParseResult = newParse;
                            CachedAnalysis = previousAnalysis;
                            CachedParseResult = newParse;
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
                            if (_analysisVersion == versionAtStart)
                            {
                                _previousAnalysis = partialResult;
                                _previousParseResult = newParse;
                                CachedAnalysis = partialResult;
                                CachedParseResult = newParse;
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
                () => api.Analyze(text, options, scope.Token),
                scope.Token
            ).ConfigureAwait(false);

            // The parse result to remember for the next edit's fingerprint and for parse-only
            // handlers. The incremental path already parsed (newParse); the fresh path derives it
            // from the analysis's entry AST — structurally identical to a standalone parse, and both
            // the fingerprint and the parse-only handlers read only .Ast (never diagnostics). A null
            // AST leaves the caches cold, matching an on-demand parse.
            var parseForCache = newParse ?? ParseResultFromAnalysis(result);

            lock (_stateLock)
            {
                // Only cache if document hasn't changed during analysis
                // and analysis wasn't cancelled (SPY0901). Cancelled results
                // would poison the cache — the next caller must retry.
                if (_analysisVersion == versionAtStart && !IsCancelledResult(result))
                {
                    _previousAnalysis = result;
                    _previousParseResult = parseForCache;
                    CachedAnalysis = result;
                    CachedParseResult = parseForCache;
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
    /// document path where no standalone parse was run (#1137). Only <see cref="ParseResult.Ast"/>
    /// is consumed downstream — by the incremental fingerprint and the parse-only LSP handlers;
    /// parse diagnostics live on the <see cref="SemanticResult"/>. Returns null when analysis
    /// produced no AST, leaving the parse cache cold so a later parse-only request reparses on
    /// demand (identical to how a fresh parse would populate it).
    /// </summary>
    private static ParseResult? ParseResultFromAnalysis(SemanticResult result)
        => result.Ast == null
            ? null
            : new ParseResult { Success = true, Ast = result.Ast };

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

    // Workspace-level compiler options passed to analysis. This is the seam through
    // which experimental FeatureFlags reach LSP analysis; sourcing them from a
    // workspace .spyproj is follow-up work (roadmap C1).
    private readonly CompilerOptions _workspaceOptions = new() { OutputType = "library" };

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

    public void OpenDocument(string uri, string text, int version)
    {
        var state = new DocumentState(uri, text, version);
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

    public async Task<SemanticResult?> GetAnalysisAsync(string uri, CancellationToken ct = default)
    {
        if (_documents.TryGetValue(uri, out var state))
        {
            return await state.GetOrRunAnalysisAsync(_api, _workspaceOptions, ct).ConfigureAwait(false);
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
            // Measure change→publish wall time for the single-file path: analysis plus the
            // DocumentAnalyzed handler that publishes diagnostics. Recorded so the LSP
            // incremental-frontend work (#1099) starts from data, not intuition.
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await GetAnalysisAsync(uri).ConfigureAwait(false);
            if (result != null)
            {
                var handler = DocumentAnalyzed;
                if (handler != null)
                {
                    await handler(uri, result).ConfigureAwait(false);
                }
                stopwatch.Stop();
                _logger.LogInformation("{LatencyLine}", AnalysisLatencyLog.Format(
                    AnalysisLatencyLog.SingleFilePath,
                    affectedFiles: 1,
                    stopwatch.Elapsed.TotalMilliseconds));
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
