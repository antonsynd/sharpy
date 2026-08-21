using System.Threading;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Analyze a Sharpy project through semantic analysis (phases 1-5) without codegen.
    /// This is the primary entry point for LSP and tooling that need type information
    /// without generating C# code or compiling to an assembly.
    /// </summary>
    /// <remarks>
    /// Each call creates fresh internal state (symbol table, semantic info, etc.),
    /// so this method can be called on a fresh <see cref="ProjectCompiler"/> instance.
    /// </remarks>
    /// <param name="config">The project configuration.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <param name="stageMetrics">
    /// Optional per-call stage attribution (#1140). Non-null only when a caller is reading the
    /// breakdown — this method is the LSP's per-keystroke path, so with null every bracket below
    /// is a null check and nothing extra is allocated.
    /// </param>
    public ProjectAnalysisResult AnalyzeProject(ProjectConfig config, CancellationToken ct = default,
        CompilationMetrics? stageMetrics = null)
    {
        _logger.LogInfo($"Starting project analysis: {config.RootNamespace}");
        _cancellationToken = ct;

        using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.ProjectSetup))
        {
            _diagnostics = new DiagnosticBag(_warningsAsErrors, _suppressedWarnings);
            _projectMetricsBacking = new ProjectCompilationMetrics(config.RootNamespace, config.Configuration);
            _projectModel = new ProjectModel(config);
        }

        try
        {
            // Phase 1: Parse all source files. Parse errors no longer abort analysis —
            // ParsedWithErrors units carry a partial AST that semantic analysis can process,
            // giving LSP receivers for trailing-dot completion (#1360). Only abort if every
            // file failed at the lexer stage (no AST at all).
            using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.ProjectParse))
            {
                ParseAllFiles(config, ct);
                if (!_projectModel!.Units.Values.Any(u => u.Ast != null))
                {
                    return CreateAnalysisResult(success: false);
                }
            }
            ct.ThrowIfCancellationRequested();

            // Phase 2: Initialize shared symbol table and semantic info
            using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.SharedStateInit))
            {
                InitializeSharedState();
            }
            ct.ThrowIfCancellationRequested();

            // Phase 3: Collect type declarations from all files (first pass - type shells only)
            using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.TypeDeclarations))
            {
                CollectTypeDeclarations(config, ct);
                CompilerInvariants.AssertPostNameResolution(SymbolTable, _diagnostics);
            }
            ct.ThrowIfCancellationRequested();

            // Phase 4: Resolve imports and build dependency information
            using (MetricsStage.Begin(stageMetrics, CompilerPhaseNames.ImportResolution))
            {
                if (!ResolveImports(config, ct))
                {
                    return CreateAnalysisResult(success: false);
                }
            }
            ct.ThrowIfCancellationRequested();

            // Phase 4b/4c: Resolve inheritance (now that imports are resolved), then auto-import
            // transitive base types and resolve imported inheritance.
            FileCompilationPipeline compilationPipeline;
            using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.InheritanceResolution))
            {
                ResolveInheritanceRelationships(ct);
                CompilerInvariants.AssertPostInheritance(SymbolTable, _diagnostics);
                ct.ThrowIfCancellationRequested();

                compilationPipeline = new FileCompilationPipeline(SymbolTable, SemanticInfo, _projectModel!.SemanticBinding, _logger);
                compilationPipeline.ResolveImportedInheritanceAndMaterialize(ImportResolver);
            }
            ct.ThrowIfCancellationRequested();

            // Phase 5: Perform semantic analysis on all files
            bool semanticSuccess;
            using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.SemanticAnalysis))
            {
                semanticSuccess = PerformSemanticAnalysis(compilationPipeline, config, ct);
            }
            using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.Materialization))
            {
                compilationPipeline.MaterializeTypeInfo();
                compilationPipeline.FreezeTypeInfo();
            }
            ct.ThrowIfCancellationRequested();

            // Stop here - no codegen (Phase 6) or assembly compilation (Phase 7)
            return CreateAnalysisResult(success: semanticSuccess);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Project analysis cancelled");
            _diagnostics.AddError("Analysis cancelled", code: DiagnosticCodes.Infrastructure.CompilationCancelled);
            return CreateAnalysisResult(success: false);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Project analysis failed with {ex.GetType().Name}: {ex}", 0, 0);

            ReportInternalCompilerError(ex, config);
            return CreateAnalysisResult(success: false);
        }
    }

    private ProjectAnalysisResult CreateAnalysisResult(bool success)
    {
        return new ProjectAnalysisResult(
            success: success,
            projectModel: _projectModel!,
            dependencyGraph: _dependencyGraph,
            diagnostics: _diagnostics);
    }
}
