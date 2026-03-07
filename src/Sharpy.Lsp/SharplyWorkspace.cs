using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
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
    private readonly object _stateLock = new();
    private CancellationTokenSource? _pendingCts;

    public DocumentState(string uri, string text, int version)
    {
        Uri = uri;
        Text = text;
        Version = version;
    }

    public void Update(string text, int version)
    {
        lock (_stateLock)
        {
            Text = text;
            Version = version;
            CachedAnalysis = null;
            CachedSourceText = null;
        }
    }

    /// <summary>
    /// Returns a consistent snapshot of Text and CachedSourceText.
    /// </summary>
    public (string Text, SourceText? CachedSourceText) GetTextSnapshot()
    {
        lock (_stateLock)
        {
            return (Text, CachedSourceText);
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
            lock (_stateLock)
            {
                // Double-check after acquiring lock
                if (CachedAnalysis != null)
                    return CachedAnalysis;
                text = Text;
            }

            // Cancel any previous pending analysis
            if (_pendingCts != null)
            {
                await _pendingCts.CancelAsync().ConfigureAwait(false);
                _pendingCts.Dispose();
            }
            _pendingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var result = await Task.Run(
                () => api.Analyze(text, _pendingCts.Token),
                _pendingCts.Token
            ).ConfigureAwait(false);

            lock (_stateLock)
            {
                CachedAnalysis = result;
                CachedSourceText = new SourceText(text, Uri);
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
    private readonly ILogger<SharplyWorkspace> _logger;

    // Debounce timers per document
    private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new();
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    // Project support
    private SpyProject? _project;
    private readonly ConcurrentDictionary<string, string> _projectFiles = new();

    // Simple dependency tracking: file path -> set of file paths that depend on it
    private readonly ConcurrentDictionary<string, ImmutableHashSet<string>> _reverseDependencies = new();

    /// <summary>
    /// Fires when a document has been analyzed (after debounce).
    /// The string parameter is the document URI.
    /// </summary>
    public event Func<string, SemanticResult, Task>? DocumentAnalyzed;

    public SharplyWorkspace(CompilerApi api, ILogger<SharplyWorkspace> logger)
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
            var (text, cachedSourceText) = state.GetTextSnapshot();
            return cachedSourceText ?? new SourceText(text, uri);
        }
        return null;
    }

    /// <summary>
    /// Loads a project from the workspace root. Searches for .spyproj files
    /// and tracks all project source files for cross-file analysis.
    /// </summary>
    public void LoadProject(string workspaceRoot)
    {
        var projectFilePath = SpyProjectLoader.FindProjectFile(workspaceRoot);
        if (projectFilePath == null)
            return;

        try
        {
            _project = SpyProjectLoader.Load(projectFilePath);

            _projectFiles.Clear();
            foreach (var sourceFile in _project.SourceFiles)
            {
                var uri = FilePathToUri(sourceFile);
                _projectFiles[uri] = sourceFile;

                // If the file is not already open, load it from disk
                if (!_documents.ContainsKey(uri) && File.Exists(sourceFile))
                {
                    var text = File.ReadAllText(sourceFile);
                    var state = new DocumentState(uri, text, 0);
                    _documents.TryAdd(uri, state);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load project from {WorkspaceRoot}", workspaceRoot);
        }
    }

    /// <summary>
    /// Reloads the project structure from disk. Call when the .spyproj file changes.
    /// </summary>
    public void ReloadProject()
    {
        if (_project == null)
            return;

        LoadProject(_project.ProjectDirectory);
    }

    /// <summary>
    /// Returns all tracked document URIs (both open and project files).
    /// </summary>
    public IReadOnlyCollection<string> GetAllDocumentUris()
    {
        var uris = new HashSet<string>(_documents.Keys);
        foreach (var uri in _projectFiles.Keys)
        {
            uris.Add(uri);
        }
        return uris;
    }

    /// <summary>
    /// Called when an external file change is detected (e.g., file saved outside the editor).
    /// Reloads the file content from disk and triggers reanalysis if the file is tracked.
    /// </summary>
    public void OnExternalFileChanged(string filePath)
    {
        var uri = FilePathToUri(filePath);

        // Only handle project files not currently open in the editor
        if (!_projectFiles.ContainsKey(uri))
            return;

        if (!File.Exists(filePath))
        {
            // File was deleted
            if (_documents.TryRemove(uri, out var removed))
            {
                removed.Dispose();
            }
            return;
        }

        var text = File.ReadAllText(filePath);

        if (_documents.TryGetValue(uri, out var state))
        {
            state.Update(text, state.Version + 1);
        }
        else
        {
            state = new DocumentState(uri, text, 0);
            _documents[uri] = state;
        }

        ScheduleAnalysis(uri);
        InvalidateDependents(uri);
    }

    /// <summary>
    /// Records that <paramref name="dependentUri"/> depends on (imports) <paramref name="dependencyUri"/>.
    /// </summary>
    public void RecordDependency(string dependencyUri, string dependentUri)
    {
        _reverseDependencies.AddOrUpdate(
            dependencyUri,
            _ => ImmutableHashSet.Create(dependentUri),
            (_, existing) => existing.Add(dependentUri));
    }

    /// <summary>
    /// Invalidates analysis caches for all documents that depend on the given URI,
    /// and schedules their reanalysis.
    /// </summary>
    private void InvalidateDependents(string uri)
    {
        if (!_reverseDependencies.TryGetValue(uri, out var dependents))
            return;

        foreach (var dependent in dependents)
        {
            if (_documents.TryGetValue(dependent, out var state))
            {
                // Invalidate cached analysis by updating with same text
                state.Update(state.Text, state.Version + 1);
                ScheduleAnalysis(dependent);
            }
        }
    }

    private static string FilePathToUri(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        return new Uri(fullPath).ToString();
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
