using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Phase 6: Generate C# code for all modules
    /// </summary>
    private Dictionary<string, string> GenerateCode(ProjectConfig config)
    {
        _logger.LogInfo("Phase 5: Code Generation");
        var generatedCSharp = new Dictionary<string, string>();
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

            // Get the file metrics we created during parsing
            var fileMetrics = unit.Metrics;

            fileMetrics?.StartPhase("Code Generation");

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
                SemanticInfo = SemanticInfo
            };

            var emitter = new RoslynEmitter(codeGenContext, _cancellationToken);
            var roslynCompilationUnit = emitter.GenerateCompilationUnit(unit.Ast);
            var csharpCode = roslynCompilationUnit.ToFullString();

            fileMetrics?.EndPhase();

            // Check for code generation errors
            if (codeGenContext.HasErrors)
            {
                unit.Diagnostics.Merge(codeGenContext.Diagnostics);
                _diagnostics.Merge(codeGenContext.Diagnostics);
                unit.Phase = CompilationPhase.Failed;
                continue;
            }

            // Store generated C# in CompilationUnit
            unit.GeneratedCSharp = csharpCode;
            unit.Phase = CompilationPhase.CodeGenerated;
            CompilerInvariants.AssertPostCodeGen(csharpCode, _diagnostics);

            // Log per-file code gen metrics at Debug level
            if (_logger.IsEnabled(CompilerLogLevel.Debug) && fileMetrics != null)
            {
                _logger.LogDebug($"Generated {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
            }

            generatedCSharp[csharpFileName] = csharpCode;
        }

        return generatedCSharp;
    }

    /// <summary>
    /// Phase 7: Compile generated C# code to assembly
    /// </summary>
    private ProjectCompilationResult CompileAssembly(ProjectConfig config, Dictionary<string, string> generatedCSharp)
    {
        _logger.LogInfo("Phase 6: Assembly Compilation");
        var assemblyCompiler = new AssemblyCompiler(_logger);
        var assemblyResult = assemblyCompiler.CompileToAssembly(generatedCSharp, config);

        // Add assembly metrics to project metrics
        if (assemblyResult.Metrics != null)
        {
            ProjectMetrics.SetAssemblyMetrics(assemblyResult.Metrics);
        }

        // Merge assembly diagnostics into project diagnostics
        _diagnostics.Merge(assemblyResult.Diagnostics);

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
                ProjectModel = _projectModel
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
            ProjectModel = _projectModel
        };
    }
}
