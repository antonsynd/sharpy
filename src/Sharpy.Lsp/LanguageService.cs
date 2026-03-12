using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Sharpy.Compiler;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Text;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Lsp;

/// <summary>
/// Project-aware analysis layer between the workspace and LSP handlers.
/// Manages project-level compilation state and provides per-file analysis results
/// that are compatible with the existing single-file <see cref="SemanticResult"/>.
/// </summary>
internal sealed class LanguageService : IDisposable
{
    private readonly SharplyWorkspace _workspace;
    private readonly CompilerApi _api;
    private readonly ILogger<LanguageService> _logger;

    // Project state
    private string? _workspaceRoot;
    private ProjectConfig? _projectConfig;
    private ProjectAnalysisResult? _projectAnalysis;
    private readonly ConcurrentDictionary<string, SemanticResult> _fileResults = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _analysisLock = new(1, 1);

    // Per-document cancellation: cancel stale reanalysis when newer edits arrive
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _documentCts = new(StringComparer.OrdinalIgnoreCase);

    // Background indexing
    private ProgressReporter? _progressReporter;
    private CancellationTokenSource? _indexingCts;

    // Atomic state: 0 = Ready, 1 = Indexing
    private const int StateReady = 0;
    private const int StateIndexing = 1;
    private volatile int _state = StateReady;

    public LanguageService(SharplyWorkspace workspace, CompilerApi api, ILogger<LanguageService> logger)
    {
        _workspace = workspace;
        _api = api;
        _logger = logger;
    }

    /// <summary>
    /// Sets the progress reporter for background indexing notifications.
    /// Must be called after the LSP server is initialized.
    /// </summary>
    public void SetProgressReporter(ProgressReporter reporter)
    {
        _progressReporter = reporter;
    }

    /// <summary>
    /// The progress reporter for background operations, if available.
    /// </summary>
    internal ProgressReporter? ProgressReporter => _progressReporter;

    /// <summary>
    /// Whether a project has been loaded.
    /// </summary>
    public bool HasProject => Volatile.Read(ref _projectConfig) != null;

    /// <summary>
    /// Whether background indexing has completed and project-level results are available.
    /// Returns true when no project is loaded and no indexing is in progress.
    /// </summary>
    public bool IsReady => _state == StateReady;

    /// <summary>
    /// The current project analysis result, if available.
    /// </summary>
    internal ProjectAnalysisResult? ProjectAnalysis => Volatile.Read(ref _projectAnalysis);

    /// <summary>
    /// Read-only dependency query from the last project analysis.
    /// </summary>
    public IDependencyQuery? Dependencies => Volatile.Read(ref _projectAnalysis)?.Dependencies;

    /// <summary>
    /// Discovers a .spyproj file in the workspace root, loads it, and runs
    /// project-level semantic analysis (phases 1-5, no codegen).
    /// </summary>
    /// <param name="workspaceRoot">The workspace root directory path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if a project was found and analyzed successfully.</returns>
    public async Task<bool> InitializeProjectAsync(string workspaceRoot, CancellationToken ct = default)
    {
        var projectFilePath = SpyProjectLoader.FindProjectFile(workspaceRoot);
        if (projectFilePath == null)
        {
            _logger.LogInformation("No .spyproj file found in {Root}", workspaceRoot);
            Interlocked.Exchange(ref _state, StateReady);
            return false;
        }

        try
        {
            var project = SpyProjectLoader.Load(projectFilePath);

            _logger.LogInformation(
                "Loaded project {Namespace} with {Count} source file(s)",
                project.RootNamespace, project.SourceFiles.Count);

            var config = project.ToProjectConfig();
            Volatile.Write(ref _workspaceRoot, workspaceRoot);
            Volatile.Write(ref _projectConfig, config);
            Interlocked.Exchange(ref _state, StateIndexing);

            var progress = _progressReporter != null
                ? await _progressReporter.BeginAsync("Indexing Sharpy project", ct).ConfigureAwait(false)
                : ProgressScope.NoOp;

            using (progress)
            {
                progress.Report($"Analyzing {config.SourceFiles.Count} file(s)...", 0);

                await _analysisLock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var result = await Task.Run(
                        () => _api.AnalyzeProject(config, ct), ct).ConfigureAwait(false);

                    Volatile.Write(ref _projectAnalysis, result);

                    // Populate per-file result cache
                    _fileResults.Clear();
                    foreach (var filePath in config.SourceFiles)
                    {
                        var fileResult = ExtractFileResult(filePath, result);
                        if (fileResult != null)
                        {
                            _fileResults[filePath] = fileResult;
                        }
                    }

                    Interlocked.Exchange(ref _state, StateReady);

                    _logger.LogInformation(
                        "Project analysis {Status} with {ErrorCount} error(s), {FileCount} file result(s) cached",
                        result.Success ? "succeeded" : "failed",
                        result.Diagnostics.ErrorCount,
                        _fileResults.Count);

                    progress.Complete(result.Success
                        ? $"Indexed {_fileResults.Count} file(s)"
                        : $"Indexing completed with {result.Diagnostics.ErrorCount} error(s)");

                    return result.Success;
                }
                finally
                {
                    _analysisLock.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Project initialization cancelled");
            Interlocked.Exchange(ref _state, StateReady);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize project from {Path}", projectFilePath);
            Interlocked.Exchange(ref _state, StateReady); // Mark ready even on failure to avoid blocking handlers
            return false;
        }
    }

    /// <summary>
    /// Starts background indexing of the project. Returns immediately; the caller
    /// can check <see cref="IsReady"/> to know when indexing completes.
    /// Cancels any previously running background indexing.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root directory path.</param>
    /// <param name="onComplete">Optional callback invoked after successful indexing.</param>
    /// <param name="ct">Cancellation token linked to the server lifetime.</param>
    public void StartBackgroundIndexing(string workspaceRoot, Func<Task>? onComplete = null, CancellationToken ct = default)
    {
        // Cancel any previous indexing
        _indexingCts?.Cancel();
        _indexingCts?.Dispose();

        Interlocked.Exchange(ref _state, StateIndexing);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _indexingCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                var success = await InitializeProjectAsync(workspaceRoot, cts.Token).ConfigureAwait(false);
                if (success && onComplete != null)
                {
                    await onComplete().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Background indexing cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background indexing failed");
            }
            finally
            {
                Interlocked.CompareExchange(ref _state, StateReady, StateIndexing);
            }
        }, cts.Token);
    }

    /// <summary>
    /// Gets analysis results for a specific file. Returns project-level results if
    /// a project is loaded and the file is part of it; otherwise falls back to
    /// single-file analysis via the workspace.
    /// </summary>
    /// <param name="uri">The document URI.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The semantic analysis result, or null if the document is not tracked.</returns>
    public async Task<SemanticResult?> GetAnalysisAsync(string uri, CancellationToken ct = default)
    {
        // Try project-level cached result first.
        // Note: This may return a stale result if a document was just edited and project
        // reanalysis hasn't completed yet. This is a deliberate trade-off — returning the
        // last-known-good project result is faster than blocking on reanalysis, and handlers
        // receive updated results once OnDocumentChangedAsync completes in the background.
        var filePath = UriToFilePath(uri);
        if (filePath != null && _fileResults.TryGetValue(filePath, out var cached))
            return cached;

        // Fall back to single-file analysis via workspace
        return await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns source text for a file. Checks workspace for open docs first,
    /// then reads from disk for project files not currently open.
    /// </summary>
    public SourceText? GetSourceText(string uri)
    {
        // Check workspace first (open documents)
        var wsText = _workspace.GetSourceText(uri);
        if (wsText != null)
            return wsText;

        // For project files not open in the editor, read from disk
        var filePath = UriToFilePath(uri);
        if (filePath != null && _fileResults.ContainsKey(filePath) && File.Exists(filePath))
        {
            var text = File.ReadAllText(filePath);
            return new SourceText(text, filePath);
        }

        return null;
    }

    /// <summary>
    /// Returns URIs of all files in the current project.
    /// Empty if no project is loaded.
    /// </summary>
    public IReadOnlyList<string> GetProjectFileUris()
    {
        var config = _projectConfig;
        if (config == null)
            return Array.Empty<string>();

        return config.SourceFiles
            .Select(FilePathToUri)
            .ToList();
    }

    /// <summary>
    /// Checks if a URI belongs to the current project.
    /// </summary>
    public bool IsProjectFile(string uri)
    {
        var filePath = UriToFilePath(uri);
        if (filePath == null)
            return false;

        return _fileResults.ContainsKey(filePath);
    }

    /// <summary>
    /// Called when a document changes. If the file is part of a project, re-runs
    /// project-level analysis and updates cached results for all affected files.
    /// Returns the URIs of files whose analysis results changed (for diagnostic publishing).
    /// </summary>
    /// <param name="uri">The URI of the changed document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// URIs of files whose analysis results were updated, or empty if the file
    /// is not part of a project or no project is loaded.
    /// </returns>
    public async Task<IReadOnlyList<string>> OnDocumentChangedAsync(string uri, CancellationToken ct = default)
    {
        var config = _projectConfig;
        if (config == null)
            return Array.Empty<string>();

        var changedFilePath = UriToFilePath(uri);
        if (changedFilePath == null || !_fileResults.ContainsKey(changedFilePath))
            return Array.Empty<string>();

        // Cancel any in-flight reanalysis for this document and create a new linked CTS
        var newCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (_documentCts.TryGetValue(uri, out var oldCts))
        {
            try
            { await oldCts.CancelAsync().ConfigureAwait(false); }
            catch (ObjectDisposedException) { }
            oldCts.Dispose();
        }
        _documentCts[uri] = newCts;
        var linkedToken = newCts.Token;

        // Determine affected files using the dependency graph
        var deps = _projectAnalysis?.Dependencies;
        var affectedPaths = deps != null
            ? deps.GetAffectedFiles(changedFilePath)
            : new[] { PathNormalizer.Normalize(changedFilePath) }.ToImmutableHashSet();

        _logger.LogInformation(
            "Document changed: {File}, {Count} affected file(s)",
            System.IO.Path.GetFileName(changedFilePath), affectedPaths.Count);

        // Re-run full project analysis (ProjectCompiler assumes single-use lifecycle)
        await _analysisLock.WaitAsync(linkedToken).ConfigureAwait(false);
        try
        {
            var result = await Task.Run(
                () => _api.AnalyzeProject(config, linkedToken), linkedToken).ConfigureAwait(false);

            Volatile.Write(ref _projectAnalysis, result);

            // Update cached results for all project files
            var updatedUris = new List<string>();
            foreach (var filePath in config.SourceFiles)
            {
                var fileResult = ExtractFileResult(filePath, result);
                if (fileResult != null)
                {
                    _fileResults[filePath] = fileResult;

                    // Only report affected files as updated
                    // affectedPaths uses normalized (lowercased on macOS) paths
                    if (affectedPaths.Contains(PathNormalizer.Normalize(filePath)))
                    {
                        updatedUris.Add(FilePathToUri(filePath));
                    }
                }
            }

            return updatedUris;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Reanalysis cancelled for {File}", System.IO.Path.GetFileName(changedFilePath));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reanalyze project after change to {File}", changedFilePath);
            return Array.Empty<string>();
        }
        finally
        {
            _analysisLock.Release();
        }
    }

    /// <summary>
    /// Re-parses the .spyproj config and triggers a full project reanalysis.
    /// Call when the .spyproj file itself changes.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if reanalysis succeeded.</returns>
    public async Task<bool> ReloadProjectAsync(CancellationToken ct = default)
    {
        var root = Volatile.Read(ref _workspaceRoot);
        if (root == null)
        {
            _logger.LogWarning("Cannot reload project: no workspace root set");
            return false;
        }

        _logger.LogInformation("Reloading project from {Root}", root);
        return await InitializeProjectAsync(root, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles a file changed outside the editor (detected by file watcher).
    /// If the file is part of the project, triggers reanalysis of affected files.
    /// </summary>
    /// <param name="filePath">The absolute file path that changed on disk.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>URIs of files whose analysis results were updated.</returns>
    public async Task<IReadOnlyList<string>> OnExternalFileChangedAsync(string filePath, CancellationToken ct = default)
    {
        if (!_fileResults.ContainsKey(filePath))
            return Array.Empty<string>();

        var uri = FilePathToUri(filePath);
        return await OnDocumentChangedAsync(uri, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts a <see cref="SemanticResult"/> from a <see cref="ProjectAnalysisResult"/>
    /// for a specific file.
    /// </summary>
    private static SemanticResult? ExtractFileResult(string filePath, ProjectAnalysisResult result)
    {
        var fileResult = result.GetFileResult(filePath);
        if (fileResult == null)
            return null;

        return new SemanticResult
        {
            Success = fileResult.Success,
            Diagnostics = fileResult.Diagnostics,
            Ast = fileResult.Ast,
            SemanticInfo = fileResult.SemanticInfo,
            SymbolTable = fileResult.SymbolTable
        };
    }

    private static string? UriToFilePath(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
            return parsed.LocalPath;

        // Already a file path
        if (System.IO.Path.IsPathRooted(uri))
            return uri;

        return null;
    }

    private static string FilePathToUri(string filePath)
    {
        var fullPath = System.IO.Path.GetFullPath(filePath);
        return new Uri(fullPath).ToString();
    }

    public void Dispose()
    {
        _indexingCts?.Cancel();
        _indexingCts?.Dispose();

        foreach (var kvp in _documentCts)
        {
            try
            { kvp.Value.Cancel(); }
            catch (ObjectDisposedException) { }
            kvp.Value.Dispose();
        }
        _documentCts.Clear();

        _analysisLock.Dispose();
    }
}
