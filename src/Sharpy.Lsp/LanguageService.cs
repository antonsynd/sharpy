using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Sharpy.Compiler;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Services;

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
    private ProjectConfig? _projectConfig;
    private ProjectAnalysisResult? _projectAnalysis;
    private readonly ConcurrentDictionary<string, SemanticResult> _fileResults = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _analysisLock = new(1, 1);

    public LanguageService(SharplyWorkspace workspace, CompilerApi api, ILogger<LanguageService> logger)
    {
        _workspace = workspace;
        _api = api;
        _logger = logger;
    }

    /// <summary>
    /// Whether a project has been loaded.
    /// </summary>
    public bool HasProject => _projectConfig != null;

    /// <summary>
    /// The current project analysis result, if available.
    /// </summary>
    internal ProjectAnalysisResult? ProjectAnalysis => _projectAnalysis;

    /// <summary>
    /// Read-only dependency query from the last project analysis.
    /// </summary>
    public IDependencyQuery? Dependencies => _projectAnalysis?.Dependencies;

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
            return false;
        }

        try
        {
            var project = SpyProjectLoader.Load(projectFilePath);

            _logger.LogInformation(
                "Loaded project {Namespace} with {Count} source file(s)",
                project.RootNamespace, project.SourceFiles.Count);

            var config = project.ToProjectConfig();

            await _analysisLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var result = await Task.Run(
                    () => _api.AnalyzeProject(config, ct), ct).ConfigureAwait(false);

                _projectConfig = config;
                _projectAnalysis = result;

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

                _logger.LogInformation(
                    "Project analysis {Status} with {ErrorCount} error(s), {FileCount} file result(s) cached",
                    result.Success ? "succeeded" : "failed",
                    result.Diagnostics.ErrorCount,
                    _fileResults.Count);

                return result.Success;
            }
            finally
            {
                _analysisLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Project initialization cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize project from {Path}", projectFilePath);
            return false;
        }
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
        // Try project-level cached result first
        var filePath = UriToFilePath(uri);
        if (filePath != null && _fileResults.TryGetValue(filePath, out var cached))
            return cached;

        // Fall back to single-file analysis via workspace
        return await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
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
        _analysisLock.Dispose();
    }
}
