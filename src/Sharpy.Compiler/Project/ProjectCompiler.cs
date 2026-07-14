using System.Threading;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Services;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Handles multi-file project compilation with proper dependency management
/// and two-phase type declaration collection for cross-file visibility
/// </summary>
internal partial class ProjectCompiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;
    private readonly ICodeEmitterFactory _emitterFactory;
    private readonly bool _warningsAsErrors;
    private readonly HashSet<string> _suppressedWarnings;
    private readonly int _maxErrors;
    private readonly bool _incremental;
    private readonly Shared.FeatureFlags _features;
    private CancellationToken _cancellationToken;

    // Per-file state isolation (#610): SymbolTable, SemanticInfo, and SemanticBinding
    // are created per-file during their respective phases and merged into shared
    // project-level instances. See docs/design/parallel-compilation.md.
    // Actual parallel dispatch (Parallel.ForEachAsync) is a separate future step.
    //
    // These fields are initialized during Compile() and accessed via guarded properties
    // to provide helpful error messages if accessed before initialization.
    private SymbolTable? _symbolTableBacking;
    private SemanticInfo? _semanticInfoBacking;
    private ImportResolver? _importResolverBacking;
    private ProjectCompilationMetrics? _projectMetricsBacking;
    private DependencyGraphBuilder? _graphBuilderBacking;

    // Guarded property accessors for fields initialized during Compile()
    private SymbolTable SymbolTable => _symbolTableBacking
        ?? throw CompilationNotStarted(nameof(SymbolTable));
    private SemanticInfo SemanticInfo => _semanticInfoBacking
        ?? throw CompilationNotStarted(nameof(SemanticInfo));
    private ImportResolver ImportResolver => _importResolverBacking
        ?? throw CompilationNotStarted(nameof(ImportResolver));
    private ProjectCompilationMetrics ProjectMetrics => _projectMetricsBacking
        ?? throw CompilationNotStarted(nameof(ProjectMetrics));
    private DependencyGraphBuilder GraphBuilder => _graphBuilderBacking
        ?? throw CompilationNotStarted(nameof(GraphBuilder));

    // Store NameResolver for deferred inheritance resolution
    private NameResolver? _sharedNameResolver;

    // Per-file NameResolvers for aggregated inheritance resolution
    private List<NameResolver>? _perFileResolvers;

    // Track errors and warnings using structured diagnostics
    private DiagnosticBag _diagnostics = new();

    /// <summary>
    /// Merges <paramref name="source"/> into <paramref name="target"/>, back-filling any
    /// still-<see cref="CompilerPhase.Unknown"/> diagnostic with <paramref name="phase"/> while
    /// preserving explicit phases and validator producers already stamped at the source.
    /// </summary>
    private static void MergeWithPhase(DiagnosticBag target, DiagnosticBag source, CompilerPhase phase)
    {
        using (target.BeginPhaseScope(phase))
        {
            target.Merge(source);
        }
    }
    private DependencyGraph? _dependencyGraph;

    // Files involved in deferred circular import cycles (normalized paths)
    private HashSet<string>? _deferredCycleFiles;

    // Unified project model containing all CompilationUnits
    private ProjectModel? _projectModel;

    // Incremental compilation cache
    private IncrementalCompilationCache? _incrementalCache;

    // Files to skip during incremental compilation (unchanged with valid cache)
    private HashSet<string> _filesToSkip = new(StringComparer.OrdinalIgnoreCase);

    // Registry of restored symbols from cache (for resolving cross-references)
    private Dictionary<string, Symbol> _restoredSymbols = new();

    // Maps target file -> set of generator source files it depends on for code
    // generation. Merged into the dependency graph + cache so that editing a
    // generator file invalidates every target that uses it.
    private readonly Dictionary<string, HashSet<string>> _generatorDependencies =
        new(StringComparer.OrdinalIgnoreCase);

    public ProjectCompiler(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null,
        bool warningsAsErrors = false, HashSet<string>? suppressedWarnings = null, int maxErrors = 0,
        bool incremental = false, ICodeEmitterFactory? emitterFactory = null,
        Shared.FeatureFlags? features = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = moduleRegistry;
        _emitterFactory = emitterFactory ?? new RoslynEmitterFactory();
        _warningsAsErrors = warningsAsErrors;
        _suppressedWarnings = suppressedWarnings ?? new HashSet<string>();
        _maxErrors = maxErrors;
        _incremental = incremental;
        _features = features ?? Shared.FeatureFlags.None;
    }

    /// <summary>
    /// The effective feature flags for this compilation (CLI flags unioned with the
    /// project's <c>&lt;Features&gt;</c>). Exposed so results and threading can reflect
    /// the merged set.
    /// </summary>
    internal Shared.FeatureFlags Features => _features;

    /// <summary>
    /// Compile a Sharpy project through the multi-file compilation pipeline
    /// </summary>
    public ProjectCompilationResult Compile(ProjectConfig config) =>
        Compile(config, CancellationToken.None);

    /// <summary>
    /// Compile a Sharpy project through the multi-file compilation pipeline with cancellation support.
    /// </summary>
    /// <param name="config">The project configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="emitAssembly">
    /// When true (the default), the pipeline runs through assembly emission (phase 7).
    /// When false, it stops after code generation (phase 6) and returns the generated C#
    /// without writing an assembly. The synthetic project-of-one-file path (#1038) uses
    /// <c>false</c> so callers that only need generated C# (e.g. <c>emit csharp</c>) — and
    /// the CLI, which emits the assembly itself with the same config — avoid a redundant
    /// or unwanted assembly write.
    /// </param>
    public ProjectCompilationResult Compile(ProjectConfig config, CancellationToken cancellationToken,
        bool emitAssembly = true)
    {
        _logger.LogInfo($"Starting project compilation: {config.RootNamespace}");
        _cancellationToken = cancellationToken;

        // Defense in depth for #1032: even if a caller supplied SourceFiles in a
        // filesystem-dependent order (e.g. a hand-built ProjectConfig that bypassed
        // ProjectFileParser.Load), stabilize the compile order with an ordinal sort so
        // every phase that iterates ProjectModel.Units sees a deterministic order.
        config.SourceFiles.Sort(StringComparer.Ordinal);

        _diagnostics = new DiagnosticBag(_warningsAsErrors, _suppressedWarnings);
        _projectMetricsBacking = new ProjectCompilationMetrics(config.RootNamespace, config.Configuration);
        _projectModel = new ProjectModel(config);

        // Initialize incremental compilation cache if enabled
        _filesToSkip.Clear();
        _restoredSymbols.Clear();

        if (_incremental)
        {
            _incrementalCache = new IncrementalCompilationCache(config, _logger);
            _incrementalCache.LoadAllCaches();

            // Build a dependency graph from cached dependencies to determine transitive affected files.
            // This is critical for correctness: if file A imports B and B changes, A must be recompiled
            // even though A's hash hasn't changed. Without using the cached dependency graph,
            // we would incorrectly skip A.
            var cachedDepGraph = _incrementalCache.BuildCachedDependencyGraph(config.SourceFiles);

            // Determine which files need to be recompiled, including transitive dependents
            var staleFiles = _incrementalCache.GetFilesToRecompile(config.SourceFiles, cachedDepGraph);

            foreach (var sourceFile in config.SourceFiles)
            {
                // A file can be skipped if:
                // 1. It's not in the stale files list (unchanged content AND not transitively affected)
                // 2. It has a valid file cache with symbols and generated C#
                if (!staleFiles.Contains(sourceFile) && _incrementalCache.HasValidFileCache(sourceFile))
                {
                    _filesToSkip.Add(sourceFile);
                }
            }

            var skippedCount = _filesToSkip.Count;
            var compiledCount = config.SourceFiles.Count - skippedCount;
            _logger.LogInfo($"Incremental mode: {compiledCount} file(s) to compile, {skippedCount} skipped (unchanged)");

            if (_logger.IsEnabled(CompilerLogLevel.Debug))
            {
                foreach (var file in _filesToSkip)
                {
                    _logger.LogDebug($"  Skipping: {Path.GetFileName(file)}");
                }
            }
        }

        try
        {
            // Phase 1: Parse all source files
            if (!ParseAllFiles(config, cancellationToken))
            {
                return CreateFailureResult();
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 2: Initialize shared symbol table and semantic info
            InitializeSharedState();

            // Phase 3: Collect type declarations from all files (first pass - type shells only)
            CollectTypeDeclarations(config, cancellationToken);
            CompilerInvariants.AssertPostNameResolution(SymbolTable, _diagnostics);
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 4: Resolve imports and build dependency information
            // Returns false only for circular imports (which break the dependency graph).
            // Non-circular import errors are merged into diagnostics but compilation
            // continues to type checking so users see the full picture.
            if (!ResolveImports(config, cancellationToken))
            {
                return CreateFailureResult();
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 4b: Resolve inheritance (now that imports are resolved)
            // This must happen AFTER imports are resolved so that imported base types
            // are available in the symbol table for cross-module inheritance
            ResolveInheritanceRelationships(cancellationToken);
            CompilerInvariants.AssertPostInheritance(SymbolTable, _diagnostics);

            // Phase 4c: Auto-import transitive base types from external modules,
            // then resolve inheritance for imported types.
            // ResolveInheritanceRelationships() handles types declared within the project,
            // but imported types from external modules still have unresolved base names.
            var compilationPipeline = new FileCompilationPipeline(SymbolTable, SemanticInfo, _projectModel.SemanticBinding, _logger);
            compilationPipeline.ResolveImportedInheritanceAndMaterialize(ImportResolver);
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 4d: Partition source generators (if any)
            var generatorPartition = PartitionGenerators();
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 5: Perform semantic analysis on all files
            if (!PerformSemanticAnalysis(compilationPipeline, config, cancellationToken))
            {
                return CreateFailureResult();
            }
            compilationPipeline.MaterializeTypeInfo();
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 5b: Execute source generators (if any)
            if (generatorPartition.HasGenerators)
            {
                if (!ExecuteGeneratorPipeline(generatorPartition, compilationPipeline, config, cancellationToken))
                {
                    return CreateFailureResult();
                }
                cancellationToken.ThrowIfCancellationRequested();
            }

            // Phase 6: Generate C# code for all files
            var generatedCode = GenerateCode(config);
            cancellationToken.ThrowIfCancellationRequested();

            // Codegen-only mode (synthetic project-of-one-file, #1038): stop here and hand
            // back the generated C#. The caller either only needs the C# (emit) or emits the
            // assembly itself using the very same ProjectConfig.
            if (!emitAssembly)
            {
                return new ProjectCompilationResult
                {
                    Success = !_diagnostics.HasErrors,
                    Diagnostics = _diagnostics,
                    GeneratedCSharpFiles = generatedCode.Sources,
                    Metrics = ProjectMetrics,
                    DependencyGraph = _dependencyGraph,
                    ProjectModel = _projectModel,
                    EffectiveFeatures = _features,
                    ImportResolver = _importResolverBacking,
                    UsedAssemblyPaths = _moduleRegistry?.GetUsedAssemblyPaths()
                        ?? (IReadOnlySet<string>)new HashSet<string>()
                };
            }

            // Phase 7: Compile to assembly
            return CompileAssembly(config, generatedCode);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Project compilation cancelled");
            _diagnostics.AddError("Compilation cancelled", code: DiagnosticCodes.Infrastructure.CompilationCancelled);
            return new ProjectCompilationResult
            {
                Success = false,
                Diagnostics = _diagnostics,
                Metrics = _projectMetricsBacking,
                DependencyGraph = _dependencyGraph,
                ProjectModel = _projectModel,
                EffectiveFeatures = _features,
                ImportResolver = _importResolverBacking
            };
        }
        catch (Exception ex)
        {
            // Log full exception including stack trace for debugging
            _logger.LogError($"Project compilation failed with {ex.GetType().Name}: {ex}", 0, 0);

            ReportInternalCompilerError(ex, config);
            return new ProjectCompilationResult
            {
                Success = false,
                Diagnostics = _diagnostics,
                Metrics = _projectMetricsBacking,
                DependencyGraph = _dependencyGraph,
                ProjectModel = _projectModel,
                EffectiveFeatures = _features,
                ImportResolver = _importResolverBacking
            };
        }
    }

    /// <summary>
    /// Last-chance internal-compiler-error handler (SPY0909). An exception that reaches here escaped
    /// every earlier check, so by definition it is a compiler bug rather than a user error. Writes a
    /// minimal-repro crash bundle (source, compiler version/commit, failing phase + producer, exception
    /// + stack, nearest AST span) and adds a SPY0909 diagnostic that points at the bundle. The phase and
    /// producer are read from the diagnostic bag's crash-context high-water mark, since the phase scope
    /// that threw has already unwound by the time this outer catch runs.
    /// </summary>
    private void ReportInternalCompilerError(Exception ex, ProjectConfig config)
    {
        var ice = ex as InternalCompilerErrorException;

        var request = new CrashBundleRequest
        {
            Exception = ex,
            Phase = _diagnostics.LastEnteredPhase,
            Producer = _diagnostics.LastEnteredProducer,
            Component = ice?.Component,
            SourceFiles = CollectSourceFiles(config),
            Node = ice?.Node,
            Span = ice?.Span,
            Line = ice?.Node is { LineStart: > 0 } n ? n.LineStart : null,
            Column = ice?.Node is { ColumnStart: > 0 } c ? c.ColumnStart : null,
            Kind = "internal-compiler-error"
        };

        var bundlePath = CrashBundleWriter.TryWrite(ResolveCrashOutputDirectory(config), request);

        var component = ice != null ? $" in {ice.Component}" : string.Empty;
        var bundleNote = bundlePath != null
            ? $" A minimal-repro crash bundle was written to: {bundlePath}"
            : string.Empty;
        var errorMessage =
            $"internal compiler error{component} ({ex.GetType().Name}): {ex.Message}. "
            + "This is a Sharpy compiler bug — please report it at https://github.com/antonsynd/sharpy/issues."
            + bundleNote;

        _diagnostics.AddError(errorMessage, code: DiagnosticCodes.Infrastructure.InternalCompilerError);
    }

    /// <summary>
    /// Gathers source files for the crash bundle: prefers in-memory source text from the project model
    /// (already parsed), falling back to the configured source-file paths (read from disk by the writer).
    /// </summary>
    private IReadOnlyDictionary<string, string> CollectSourceFiles(ProjectConfig config)
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var model = _projectModel;
        if (model != null)
        {
            foreach (var unit in model.Units.Values)
            {
                if (!sources.ContainsKey(unit.FilePath))
                    sources[unit.FilePath] = unit.SourceText;
            }
        }

        foreach (var path in config.SourceFiles)
        {
            if (!sources.ContainsKey(path))
                sources[path] = string.Empty; // writer reads from disk on demand
        }

        return sources;
    }

    /// <summary>
    /// Chooses where to root the <c>.sharpy-crash</c> directory: the output assembly's directory when
    /// available, else the directory of the first source file, else the current directory.
    /// </summary>
    private static string? ResolveCrashOutputDirectory(ProjectConfig config)
    {
        try
        {
            var outputPath = config.OutputAssemblyPath;
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
                return outputDir;
        }
        catch
        {
            // Fall through to source-file-based resolution.
        }

        if (config.SourceFiles.Count > 0)
        {
            var dir = Path.GetDirectoryName(config.SourceFiles[0]);
            if (!string.IsNullOrEmpty(dir))
                return dir;
        }

        return null;
    }

    private static InvalidOperationException CompilationNotStarted(string fieldName)
    {
        return new InvalidOperationException(
            $"Cannot access {fieldName}: Compile() has not been called yet. " +
            "This is a compiler bug - please report it.");
    }
}
