using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sharpy.Compiler;
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

    private readonly SemaphoreSlim _analysisSemaphore = new(1, 1);
    private readonly object _stateLock = new();
    private CancellationTokenSource? _pendingCts;

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

    public async Task<SemanticResult> GetOrRunAnalysisAsync(CompilerApi api, CancellationToken ct)
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
            SourceText sourceText;
            lock (_stateLock)
            {
                // Double-check after acquiring lock
                if (CachedAnalysis != null)
                    return CachedAnalysis;
                sourceText = SourceText;
                text = sourceText.ToString();
            }

            // Cancel any previous pending analysis
            var newCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var oldCts = Interlocked.Exchange(ref _pendingCts, newCts);
            if (oldCts != null)
            {
                await oldCts.CancelAsync().ConfigureAwait(false);
                oldCts.Dispose();
            }

            var result = await Task.Run(
                () => api.Analyze(text, newCts.Token),
                newCts.Token
            ).ConfigureAwait(false);

            lock (_stateLock)
            {
                CachedAnalysis = result;
            }
            return result;
        }
        finally
        {
            _analysisSemaphore.Release();
        }
    }

    public void Dispose()
    {
        var cts = Interlocked.Exchange(ref _pendingCts, null);
        cts?.Cancel();
        cts?.Dispose();
        _analysisSemaphore.Dispose();
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
        // Atomically replace the debounce timer to avoid race conditions
        // between concurrent ScheduleAnalysis calls on the same URI.
        _debounceTimers.AddOrUpdate(
            uri,
            _ => new Timer(_ => FireAndForgetAnalysis(uri),
                null, DebounceDelay, Timeout.InfiniteTimeSpan),
            (_, oldTimer) =>
            {
                oldTimer.Dispose();
                return new Timer(_ => FireAndForgetAnalysis(uri),
                    null, DebounceDelay, Timeout.InfiniteTimeSpan);
            });
    }

    // Timer callbacks require void return; the full try-catch ensures no exceptions escape.
#pragma warning disable VSTHRD100
    private async void FireAndForgetAnalysis(string uri)
#pragma warning restore VSTHRD100
    {
        try
        {
            var result = await GetAnalysisAsync(uri).ConfigureAwait(false);
            if (result != null)
            {
                var handler = DocumentAnalyzed;
                if (handler != null)
                {
                    await handler(uri, result).ConfigureAwait(false);
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
