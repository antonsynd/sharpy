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
    /// The file a diagnostic with no <see cref="CompilerDiagnostic.FilePath"/> belongs to in
    /// <see cref="GetFileResult"/> (normalized; D5 of #2242). One rule for every per-file reader —
    /// this projection, the editor's publisher, the front-end parity sweep — see
    /// <see cref="ProjectModel.UnattributedDiagnosticTarget"/>.
    /// </summary>
    public string? UnattributedTarget => ProjectModel.UnattributedDiagnosticTarget;

    /// <summary>
    /// Gets per-file analysis results for a specific file, compatible with
    /// <see cref="SemanticResult"/> for single-file consumers.
    /// </summary>
    /// <remarks>
    /// <see cref="FileAnalysisResult.Diagnostics"/> is <see cref="ProjectModel.DiagnosticsForFile"/>
    /// over <see cref="Diagnostics"/>: every build diagnostic stamped with this file, from every
    /// phase — not only the unit's own bag, which until #2242 hid import, declaration,
    /// inheritance, cycle and collision errors from the editor.
    /// </remarks>
    /// <param name="filePath">The file path to look up.</param>
    /// <returns>A per-file view, or null if the file is not in the project.</returns>
    public FileAnalysisResult? GetFileResult(string filePath)
    {
        var unit = ProjectModel.GetUnit(filePath);
        if (unit == null)
            return null;

        var diagnostics = ProjectModel.DiagnosticsForFile(filePath, Diagnostics);

        // Phase alone is not success: since #1360 a unit that parsed with errors still advances
        // to TypeChecked when the type checker itself adds nothing, so the file's diagnostics
        // must also be clean (matching the single-file path, which derives success from the
        // diagnostic bag) — the projected set, so an import or declaration error the unit's own
        // bag never saw also fails the file (#2242).
        var succeeded = unit.Phase == CompilationPhase.TypeChecked && !diagnostics.Any(d => d.IsError);
        var semanticInfo = unit.FileSemanticInfo ?? ProjectModel.SemanticInfo;

        return new FileAnalysisResult(
            Success: succeeded,
            Ast: unit.Ast,
            SemanticInfo: semanticInfo,
            SymbolTable: ProjectModel.GlobalSymbols,
            Diagnostics: diagnostics,
            CommentSpans: unit.CommentSpans);
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
    IReadOnlyList<CompilerDiagnostic> Diagnostics,
    IReadOnlyList<CommentSpan> CommentSpans)
{
    /// <summary>Read-only query interface for semantic information.</summary>
    public ISemanticQuery? SemanticQuery => SemanticInfo;
}
