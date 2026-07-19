using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Phase 6: Generate C# code for all modules.
    /// </summary>
    /// <remarks>
    /// Returns both the generated C# text (keyed by output .cs path — the source of truth
    /// for snapshots, the incremental cache, <c>emit csharp</c>, and LSP) and the
    /// post-processed Roslyn <see cref="SyntaxTree"/>s (D3, #1050). The trees wrap the exact
    /// emitted nodes with no reparse and are handed straight to <c>CSharpCompilation.Create</c>;
    /// only cache-served files (which have text but no tree) are reparsed downstream.
    /// </remarks>
    private GeneratedCode GenerateCode(ProjectConfig config)
    {
        _logger.LogInfo("Phase 6: Code Generation");

        // Lowering (E2, #1056): build the middle-end IR once per project — over the merged,
        // project-level SemanticInfo — immediately before per-file code generation. Nothing reads
        // it yet; it is handed to each file's CodeGenContext for the first migrated construct.
        var ir = BuildLoweringIr(config);

        var generatedCSharp = new Dictionary<string, string>();
        var generatedTrees = new Dictionary<string, SyntaxTree>();
        var builtinRegistry = new BuiltinRegistry(_logger);

        foreach (var (_, unit) in _projectModel!.Units)
        {
            var sourceFile = unit.FilePath;
            var relativePath = Path.GetRelativePath(config.ProjectDirectory, sourceFile);
            var csharpFileName = Path.ChangeExtension(relativePath, ".cs");

            // Include cached C# code for skipped files
            if (unit.Phase == CompilationPhase.Skipped)
            {
                if (!string.IsNullOrEmpty(unit.GeneratedCSharp))
                {
                    generatedCSharp[csharpFileName] = unit.GeneratedCSharp;

                    if (_logger.IsEnabled(CompilerLogLevel.Debug))
                    {
                        _logger.LogDebug($"Using cached C# for {Path.GetFileName(sourceFile)}");
                    }
                }
                continue;
            }

            // Only generate code for successfully type-checked units
            if (unit.Phase != CompilationPhase.TypeChecked || unit.Ast == null)
                continue;

            // Enter per-module scope so code generation resolves names from the correct scope
            SymbolTable.EnterModuleScope(unit.ModulePath);
            try
            {
                // Get the file metrics we created during parsing
                var fileMetrics = unit.Metrics;

                fileMetrics?.StartPhase(CompilerPhaseNames.CodeGeneration);
                LogPhaseStartEvent(CompilerPhaseNames.CodeGeneration, sourceFile);

                // Determine if this file is the entry point
                var isEntryPoint = IsEntryPointFileForTypeCheck(sourceFile, config);

                var isPackageInit = Path.GetFileNameWithoutExtension(sourceFile) == DunderNames.Init;

                var codeGenContext = new CodeGenContext(SymbolTable, builtinRegistry)
                {
                    SourceFilePath = sourceFile,
                    ProjectNamespace = config.RootNamespace,
                    ProjectRootPath = ComputeSourceRootPath(config),
                    IsEntryPoint = isEntryPoint,
                    IsPackageInit = isPackageInit,
                    Logger = _logger,
                    SemanticBinding = _projectModel.SemanticBinding,
                    SemanticInfo = SemanticInfo,
                    Ir = ir,
                    EmitLineDirectives = config.EmitLineDirectives,
                    Features = ImportResolver.GetEffectiveFeatures(_features, unit.FilePath)
                };

                var emitter = _emitterFactory.Create(codeGenContext, _cancellationToken);
                var roslynCompilationUnit = emitter.GenerateCompilationUnit(unit.Ast);
                var csharpCode = roslynCompilationUnit.ToFullString();

                // The emitter's tree is handed directly to CSharpCompilation (zero-parse, #1095):
                // no ToFullString()→ParseText round-trip. ReparseEquivalenceConformanceTests
                // guarantees Create(root) binds identically to ParseText(root.ToFullString()) across
                // the stdlib + fixture corpus. #line directives are emitter-built syntax that Create
                // carries natively (the tree still prints them into csharpCode below for snapshots).
                //
                // INVARIANT: any TEXT transformation of the generated C# after emission — e.g.
                // LineDirectivePostProcessor when #1077 wires it — must revert THIS path to
                // CSharpSyntaxTree.ParseText(processedText): a text edit invalidates the prebuilt tree.
                var syntaxTree = CSharpSyntaxTree.Create(
                    roslynCompilationUnit,
                    options: CSharpParseOptions.Default,
                    path: csharpFileName,
                    encoding: System.Text.Encoding.UTF8);

                fileMetrics?.EndPhase();
                LogPhaseEndEvent(fileMetrics, sourceFile, codeGenContext.Diagnostics.ErrorCount);

                // Surface all code generation diagnostics — not just errors. Warnings and
                // info notes (e.g. SPY1001 implicit-interface synthesis) must reach the
                // result bag so the CLI, LSP, and fixture .warning assertions can observe
                // them; previously they were silently dropped unless codegen also errored.
                MergeWithPhase(unit.Diagnostics, codeGenContext.Diagnostics, CompilerPhase.CodeGeneration);
                MergeWithPhase(_diagnostics, codeGenContext.Diagnostics, CompilerPhase.CodeGeneration);

                // Check for code generation errors
                if (codeGenContext.HasErrors)
                {
                    unit.Phase = CompilationPhase.Failed;
                    continue;
                }

                // Store generated C# and its Roslyn tree in CompilationUnit
                unit.GeneratedCSharp = csharpCode;
                unit.GeneratedSyntaxTree = syntaxTree;
                unit.Phase = CompilationPhase.CodeGenerated;

                // Structured code-gen event carrying this file's generated C# size (#1077). One
                // per successfully generated unit; the deleted single-file driver emitted the same.
                LogCodeGenEvent(sourceFile, System.Text.Encoding.UTF8.GetByteCount(csharpCode));
                // D3 (#1050): validate the emitter's tree directly (GetDiagnostics), instead
                // of reparsing csharpCode — this is the same tree handed to Roslyn.
                CompilerInvariants.AssertPostCodeGen(syntaxTree, _diagnostics);

                // Log per-file code gen metrics at Debug level
                if (_logger.IsEnabled(CompilerLogLevel.Debug) && fileMetrics != null)
                {
                    _logger.LogDebug($"Generated {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                }

                generatedCSharp[csharpFileName] = csharpCode;
                generatedTrees[csharpFileName] = syntaxTree;
            }
            finally
            {
                SymbolTable.ExitScope();
            }
        }

        return new GeneratedCode(generatedCSharp, generatedTrees);
    }

    /// <summary>
    /// Builds the middle-end lowering IR for the whole project (E2, #1056). Runs once per compile,
    /// after <c>SemanticInfo.MergeFrom</c> and immediately before per-file code generation, over the
    /// merged project-level <see cref="SemanticInfo"/> (Design Decision 2). The work is bracketed by
    /// the <see cref="CompilerPhaseNames.Lowering"/> phase metric; the result is stored on the
    /// project model and returned for the per-file <see cref="CodeGenContext"/>. In E2 Phase 1
    /// nothing reads the IR, and the emitted C# is unchanged.
    /// </summary>
    private IrCompilation BuildLoweringIr(ProjectConfig config)
    {
        var metrics = new CompilationMetrics(projectName: config.RootNamespace, configuration: config.Configuration);
        metrics.StartPhase(CompilerPhaseNames.Lowering);
        try
        {
            // Order by source path so the module order is independent of unit-dictionary
            // enumeration order (A3 determinism).
            var modules = _projectModel!.Units.Values
                .Where(unit => unit.Ast != null)
                .OrderBy(unit => unit.FilePath, StringComparer.Ordinal)
                .Select(unit => (unit.FilePath, unit.Ast!))
                .ToList();

            var ir = new LoweringPass().Lower(modules, SemanticInfo, SymbolTable);
            ir = RunIrOptimizationPasses(ir);
            _projectModel.IrCompilation = ir;
            return ir;
        }
        finally
        {
            metrics.EndPhase();
            ProjectMetrics.SetLoweringMetrics(metrics);
        }
    }

    /// <summary>
    /// Runs the E3 IR optimization passes (#1057) over each lowered module, keyed to that module's
    /// effective feature set (compilation-wide flags unioned with its per-file
    /// <c>from __future__ import</c>). Passes are pure IR→IR rewrites gated on CodeGen-scoped flags via
    /// <see cref="IrPassManager"/>; with no pass registered (the default) or every flag off, this is a
    /// no-op that returns the same <see cref="IrCompilation"/>. Runs inside the Lowering phase bracket.
    /// </summary>
    /// <remarks>
    /// When a pass rewrites a module, the returned <see cref="IrCompilation"/> carries the new modules
    /// but the reference-keyed <see cref="IrCompilation.Index"/> still maps to the pre-rewrite nodes;
    /// wiring the emitter to read rewritten IR is added by the pass phases (E3 Phases 6–9, #1057). Until
    /// a pass is registered, no rewrite occurs and the index stays exact.
    /// </remarks>
    private IrCompilation RunIrOptimizationPasses(IrCompilation ir)
    {
        var manager = IrPassManager.Default;
        var context = new IrRewriteContext(SymbolTable, SemanticInfo);

        ImmutableArray<IrModule>.Builder? rewritten = null;
        Dictionary<Expression, IrConstant>? folds = null;
        Dictionary<Expression, IrLoweredLoop>? loops = null;
        HashSet<ListLiteral>? stackLiterals = null;
        for (int i = 0; i < ir.Modules.Length; i++)
        {
            var module = ir.Modules[i];
            var features = ImportResolver.GetEffectiveFeatures(_features, module.FilePath);
            var optimized = manager.Run(module, features, context);
            if (!ReferenceEquals(optimized, module))
            {
                rewritten ??= ir.Modules.ToBuilder();
                rewritten[i] = optimized;
                folds ??= new Dictionary<Expression, IrConstant>(ReferenceEqualityComparer.Instance);
                IrPassManager.CollectFoldedConstants(optimized, folds);
                loops ??= new Dictionary<Expression, IrLoweredLoop>(ReferenceEqualityComparer.Instance);
                IrPassManager.CollectOptimizedComprehensions(optimized, loops);
                stackLiterals ??= new HashSet<ListLiteral>(ReferenceEqualityComparer.Instance);
                IrPassManager.CollectStackAllocatedLiterals(optimized, stackLiterals);
            }
        }

        if (rewritten is null)
            return ir;

        var result = ir with { Modules = rewritten.ToImmutable() };
        if (folds is { Count: > 0 })
            result = result with { FoldedConstants = folds };
        if (loops is { Count: > 0 })
            result = result with { OptimizedComprehensions = loops };
        if (stackLiterals is { Count: > 0 })
            result = result with { StackAllocatedLiterals = stackLiterals };
        return result;
    }

    /// <summary>
    /// Phase 7: Compile generated C# code to assembly
    /// </summary>
    private ProjectCompilationResult CompileAssembly(ProjectConfig config, GeneratedCode generated)
    {
        _logger.LogInfo("Phase 7: Assembly Compilation");
        var generatedCSharp = generated.Sources;
        var assemblyCompiler = new AssemblyCompiler(_logger);
        var assemblyResult = assemblyCompiler.CompileToAssembly(generatedCSharp, generated.Trees, config);

        // Add assembly metrics to project metrics
        if (assemblyResult.Metrics != null)
        {
            ProjectMetrics.SetAssemblyMetrics(assemblyResult.Metrics);
        }

        // Merge assembly diagnostics into project diagnostics
        MergeWithPhase(_diagnostics, assemblyResult.Diagnostics, CompilerPhase.Assembly);

        if (!assemblyResult.Success)
        {
            // Also add errors to project model global diagnostics for project-level access
            foreach (var error in assemblyResult.Diagnostics.GetErrors())
            {
                _projectModel!.GlobalDiagnostics.Add(error);
            }

            return new ProjectCompilationResult
            {
                Success = false,
                Diagnostics = _diagnostics,
                // Include generated C# for debugging even on failure
                GeneratedCSharpFiles = generatedCSharp,
                Metrics = ProjectMetrics,
                DependencyGraph = _dependencyGraph,
                ProjectModel = _projectModel,
                EffectiveFeatures = _features,
                UsedAssemblyPaths = _moduleRegistry?.GetUsedAssemblyPaths()
                    ?? (IReadOnlySet<string>)new HashSet<string>()
            };
        }

        // Save incremental compilation cache on success
        if (_incrementalCache != null)
        {
            SaveIncrementalCaches(config);
        }

        return new ProjectCompilationResult
        {
            Success = true,
            Diagnostics = _diagnostics,
            OutputAssemblyPath = assemblyResult.OutputAssemblyPath,
            GeneratedCSharpFiles = generatedCSharp,
            Metrics = ProjectMetrics,
            DependencyGraph = _dependencyGraph,
            ProjectModel = _projectModel,
            EffectiveFeatures = _features,
            ImportResolver = _importResolverBacking,
            UsedAssemblyPaths = _moduleRegistry?.GetUsedAssemblyPaths()
                ?? (IReadOnlySet<string>)new HashSet<string>()
        };
    }
}

/// <summary>
/// Output of Phase 6 (code generation): the generated C# text per output .cs path plus,
/// for freshly-generated files, the post-processed Roslyn <see cref="SyntaxTree"/> that
/// wraps the exact emitted node (no reparse — D3, #1050). Cache-served (skipped) files
/// appear in <see cref="Sources"/> only; <see cref="AssemblyCompiler"/> reparses those.
/// </summary>
internal readonly record struct GeneratedCode(
    Dictionary<string, string> Sources,
    Dictionary<string, SyntaxTree> Trees);
