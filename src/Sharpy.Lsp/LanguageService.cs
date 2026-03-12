using Microsoft.Extensions.Logging;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Lsp;

/// <summary>
/// Project-aware analysis layer between the workspace and LSP handlers.
/// Manages project-level compilation state and provides per-file analysis results
/// that are compatible with the existing single-file <see cref="SemanticResult"/>.
/// </summary>
internal sealed class LanguageService
{
    private readonly SharplyWorkspace _workspace;
    private readonly ILogger<LanguageService> _logger;

    // Project state
    private SpyProject? _project;
    private ProjectAnalysisResult? _projectAnalysis;
    private readonly object _projectLock = new();

    public LanguageService(SharplyWorkspace workspace, ILogger<LanguageService> logger)
    {
        _workspace = workspace;
        _logger = logger;
    }

    /// <summary>
    /// Whether a project has been loaded.
    /// </summary>
    public bool HasProject
    {
        get
        {
            lock (_projectLock)
            {
                return _project != null;
            }
        }
    }

    /// <summary>
    /// The current project analysis result, if available.
    /// </summary>
    internal ProjectAnalysisResult? ProjectAnalysis
    {
        get
        {
            lock (_projectLock)
            {
                return _projectAnalysis;
            }
        }
    }

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

            var result = await Task.Run(() =>
            {
                var compiler = new ProjectCompiler();
                var config = project.ToProjectConfig();
                return compiler.AnalyzeProject(config, ct);
            }, ct).ConfigureAwait(false);

            lock (_projectLock)
            {
                _project = project;
                _projectAnalysis = result;
            }

            _logger.LogInformation(
                "Project analysis {Status} with {ErrorCount} error(s)",
                result.Success ? "succeeded" : "failed",
                result.Diagnostics.ErrorCount);

            return result.Success;
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
        // Try project-level analysis first
        var projectResult = TryGetProjectFileResult(uri);
        if (projectResult != null)
            return projectResult;

        // Fall back to single-file analysis via workspace
        return await _workspace.GetAnalysisAsync(uri, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts a per-file <see cref="SemanticResult"/> from the project analysis,
    /// or returns null if the file is not part of the project.
    /// </summary>
    private SemanticResult? TryGetProjectFileResult(string uri)
    {
        ProjectAnalysisResult? analysis;
        lock (_projectLock)
        {
            analysis = _projectAnalysis;
        }

        if (analysis == null)
            return null;

        // Convert URI to file path for lookup
        var filePath = UriToFilePath(uri);
        if (filePath == null)
            return null;

        var fileResult = analysis.GetFileResult(filePath);
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
        if (Path.IsPathRooted(uri))
            return uri;

        return null;
    }
}
