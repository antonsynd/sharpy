using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
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
                    Features = ImportResolver.GetEffectiveFeatures(_features, unit.FilePath)
                };

                var emitter = _emitterFactory.Create(codeGenContext, _cancellationToken);
                var roslynCompilationUnit = emitter.GenerateCompilationUnit(unit.Ast);
                var csharpCode = roslynCompilationUnit.ToFullString();

                // D3 (#1050): wrap the exact emitted node in a SyntaxTree with no reparse,
                // so the compile path hands it straight to CSharpCompilation.Create. The path
                // and UTF-8 encoding mirror the ParseText call this replaces (AssemblyCompiler),
                // keeping #line mapping, PDB checksums, and emit output byte-for-byte identical.
                var syntaxTree = CSharpSyntaxTree.Create(
                    roslynCompilationUnit,
                    options: null,
                    path: csharpFileName,
                    encoding: System.Text.Encoding.UTF8);

                // NOTE(#1077): enhanced #line directives keep the emitter's placeholder char
                // offset here; LineDirectivePostProcessor (run by the deleted single-file path)
                // is not yet wired into project-mode codegen. Wiring it requires regenerating all
                // line-directive .expected.cs snapshots, so it is deferred out of the #1038
                // pipeline-deletion change.

                fileMetrics?.EndPhase();

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
                CompilerInvariants.AssertPostCodeGen(csharpCode, _diagnostics);

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
