using System.Collections.Concurrent;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Lsp;

/// <summary>
/// Document state for a single open document.
/// </summary>
internal sealed class DocumentState : IDisposable
{
    public string Uri { get; }
    public string Text { get; private set; }
    public int Version { get; private set; }
    public SemanticResult? CachedAnalysis { get; private set; }
    public SourceText? CachedSourceText { get; private set; }

    private readonly SemaphoreSlim _analysisSemaphore = new(1, 1);
    private CancellationTokenSource? _pendingCts;

    public DocumentState(string uri, string text, int version)
    {
        Uri = uri;
        Text = text;
        Version = version;
    }

    public void Update(string text, int version)
    {
        Text = text;
        Version = version;
        CachedAnalysis = null;
        CachedSourceText = null;
    }

    public async Task<SemanticResult> GetOrRunAnalysisAsync(CompilerApi api, CancellationToken ct)
    {
        if (CachedAnalysis != null)
            return CachedAnalysis;

        await _analysisSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (CachedAnalysis != null)
                return CachedAnalysis;

            // Cancel any previous pending analysis
            if (_pendingCts != null)
            {
                await _pendingCts.CancelAsync().ConfigureAwait(false);
                _pendingCts.Dispose();
            }
            _pendingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var text = Text;
            var result = await Task.Run(
                () => api.Analyze(text, _pendingCts.Token),
                _pendingCts.Token
            ).ConfigureAwait(false);

            CachedAnalysis = result;
            CachedSourceText = new SourceText(text, Uri);
            return result;
        }
        finally
        {
            _analysisSemaphore.Release();
        }
    }

    public void Dispose()
    {
        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
        _analysisSemaphore.Dispose();
    }
}

/// <summary>
/// Manages open document state and cached analysis results for the LSP server.
/// Thread-safe via ConcurrentDictionary and per-document SemaphoreSlim.
/// </summary>
internal sealed class SharplyWorkspace : IDisposable
{
    private readonly ConcurrentDictionary<string, DocumentState> _documents = new();
    private readonly CompilerApi _api;

    // Debounce timers per document
    private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new();
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Fires when a document has been analyzed (after debounce).
    /// The string parameter is the document URI.
    /// </summary>
    public event Func<string, SemanticResult, Task>? DocumentAnalyzed;

    public SharplyWorkspace(CompilerApi api)
    {
        _api = api;
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
            return await state.GetOrRunAnalysisAsync(_api, ct).ConfigureAwait(false);
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
            return state.CachedSourceText ?? new SourceText(state.Text, uri);
        }
        return null;
    }

    private void ScheduleAnalysis(string uri)
    {
        // Cancel and recreate debounce timer
        if (_debounceTimers.TryRemove(uri, out var oldTimer))
        {
            oldTimer.Dispose();
        }

        var timer = new Timer(_ => _ = RunAnalysisAsync(uri),
            null, DebounceDelay, Timeout.InfiniteTimeSpan);
        _debounceTimers[uri] = timer;
    }

    private async Task RunAnalysisAsync(string uri)
    {
        try
        {
            var result = await GetAnalysisAsync(uri).ConfigureAwait(false);
            if (result != null)
            {
                DocumentAnalyzed?.Invoke(uri, result);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when document changes rapidly
        }
        catch (Exception)
        {
            // Log but don't crash the server
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
