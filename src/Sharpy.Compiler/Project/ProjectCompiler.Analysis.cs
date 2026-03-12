using System.Threading;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
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
    public ProjectAnalysisResult AnalyzeProject(ProjectConfig config, CancellationToken ct = default)
    {
        _logger.LogInfo($"Starting project analysis: {config.RootNamespace}");
        _cancellationToken = ct;

        _diagnostics = new DiagnosticBag(_warningsAsErrors, _suppressedWarnings);
        _projectMetricsBacking = new ProjectCompilationMetrics(config.RootNamespace, config.Configuration);
        _projectModel = new ProjectModel(config);

        try
        {
            // Phase 1: Parse all source files
            if (!ParseAllFiles(config, ct))
            {
                return CreateAnalysisResult(success: false);
            }
            ct.ThrowIfCancellationRequested();

            // Phase 2: Initialize shared symbol table and semantic info
            InitializeSharedState();
            ct.ThrowIfCancellationRequested();

            // Phase 3: Collect type declarations from all files (first pass - type shells only)
            CollectTypeDeclarations(config, ct);
            CompilerInvariants.AssertPostNameResolution(SymbolTable, _diagnostics);
            ct.ThrowIfCancellationRequested();

            // Phase 4: Resolve imports and build dependency information
            if (!ResolveImports(config, ct))
            {
                return CreateAnalysisResult(success: false);
            }
            ct.ThrowIfCancellationRequested();

            // Phase 4b: Resolve inheritance (now that imports are resolved)
            ResolveInheritanceRelationships(ct);
            CompilerInvariants.AssertPostInheritance(SymbolTable, _diagnostics);
            ct.ThrowIfCancellationRequested();

            // Phase 4c: Auto-import transitive base types and resolve imported inheritance
            var compilationPipeline = new FileCompilationPipeline(SymbolTable, SemanticInfo, _projectModel.SemanticBinding, _logger);
            compilationPipeline.ResolveImportedInheritanceAndMaterialize(ImportResolver);
            ct.ThrowIfCancellationRequested();

            // Phase 5: Perform semantic analysis on all files
            var semanticSuccess = PerformSemanticAnalysis(compilationPipeline, config, ct);
            compilationPipeline.MaterializeTypeInfo();
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

            var errorMessage = ex is InternalCompilerErrorException ice
                ? $"Internal compiler error in {ice.Component} ({ex.GetType().Name}): {ex.Message}"
                : $"Project analysis failed ({ex.GetType().Name}): {ex.Message}";

            _diagnostics.AddError(errorMessage, code: DiagnosticCodes.Infrastructure.CompilationFailed);
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
