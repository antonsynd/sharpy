using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Services;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Result of project-level semantic analysis (parse through type checking, no codegen).
/// Provides access to the project model, dependency graph, and diagnostics.
/// </summary>
public sealed class ProjectAnalysisResult
{
    internal ProjectAnalysisResult(
        bool success,
        ProjectModel projectModel,
        DependencyGraph? dependencyGraph,
        DiagnosticBag diagnostics)
    {
        Success = success;
        ProjectModel = projectModel;
        DependencyGraph = dependencyGraph;
        Diagnostics = diagnostics;
    }

    /// <summary>Whether analysis completed without errors.</summary>
    public bool Success { get; }

    /// <summary>The project model containing all CompilationUnits.</summary>
    public ProjectModel ProjectModel { get; }

    /// <summary>The dependency graph (internal, use <see cref="Dependencies"/> for public access).</summary>
    internal DependencyGraph? DependencyGraph { get; }

    /// <summary>All diagnostics from all phases up to and including semantic analysis.</summary>
    public DiagnosticBag Diagnostics { get; }

    /// <summary>
    /// Read-only query interface for file dependency information.
    /// </summary>
    public IDependencyQuery? Dependencies => DependencyGraph;

    /// <summary>
    /// Gets per-file analysis results for a specific file, compatible with
    /// <see cref="SemanticResult"/> for single-file consumers.
    /// </summary>
    /// <param name="filePath">The file path to look up.</param>
    /// <returns>A per-file view, or null if the file is not in the project.</returns>
    public FileAnalysisResult? GetFileResult(string filePath)
    {
        var unit = ProjectModel.GetUnit(filePath);
        if (unit == null)
            return null;

        var succeeded = unit.Phase == CompilationPhase.TypeChecked;
        var semanticInfo = unit.FileSemanticInfo ?? ProjectModel.SemanticInfo;

        return new FileAnalysisResult(
            Success: succeeded,
            Ast: unit.Ast,
            SemanticInfo: semanticInfo,
            SymbolTable: ProjectModel.GlobalSymbols,
            Diagnostics: unit.Diagnostics.GetAll());
    }
}

/// <summary>
/// Per-file view of analysis results, compatible with <see cref="SemanticResult"/>.
/// </summary>
public sealed record FileAnalysisResult(
    bool Success,
    Module? Ast,
    SemanticInfo? SemanticInfo,
    SymbolTable? SymbolTable,
    IReadOnlyList<CompilerDiagnostic> Diagnostics)
{
    /// <summary>Read-only query interface for semantic information.</summary>
    public ISemanticQuery? SemanticQuery => SemanticInfo;
}
