using System.Threading;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Handles multi-file project compilation with proper dependency management
/// and two-phase type declaration collection for cross-file visibility
/// </summary>
internal partial class ProjectCompiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;
    private readonly bool _warningsAsErrors;
    private readonly HashSet<string> _suppressedWarnings;
    private readonly int _maxErrors;
    private readonly bool _incremental;
    private CancellationToken _cancellationToken;

    // Shared symbol table and semantic info across all files.
    // SemanticInfo is shared (not per-file) because files are processed sequentially
    // in dependency order. For parallel per-file analysis (e.g., LSP), SemanticInfo
    // should be created per-file while SymbolTable and SemanticBinding remain shared.
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

    // Track errors and warnings using structured diagnostics
    private DiagnosticBag _diagnostics = new();
    private DependencyGraph? _dependencyGraph;

    // Unified project model containing all CompilationUnits
    private ProjectModel? _projectModel;

    // Incremental compilation cache
    private IncrementalCompilationCache? _incrementalCache;

    // Files to skip during incremental compilation (unchanged with valid cache)
    private HashSet<string> _filesToSkip = new(StringComparer.OrdinalIgnoreCase);

    // Registry of restored symbols from cache (for resolving cross-references)
    private Dictionary<string, Symbol> _restoredSymbols = new();

    public ProjectCompiler(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null,
        bool warningsAsErrors = false, HashSet<string>? suppressedWarnings = null, int maxErrors = 0,
        bool incremental = false)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = moduleRegistry;
        _warningsAsErrors = warningsAsErrors;
        _suppressedWarnings = suppressedWarnings ?? new HashSet<string>();
        _maxErrors = maxErrors;
        _incremental = incremental;
    }

    /// <summary>
    /// Compile a Sharpy project through the multi-file compilation pipeline
    /// </summary>
    public ProjectCompilationResult Compile(ProjectConfig config) =>
        Compile(config, CancellationToken.None);

    /// <summary>
    /// Compile a Sharpy project through the multi-file compilation pipeline with cancellation support
    /// </summary>
    public ProjectCompilationResult Compile(ProjectConfig config, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"Starting project compilation: {config.RootNamespace}");
        _cancellationToken = cancellationToken;

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
            var inheritanceResolver = new InheritanceResolver(SymbolTable, _logger, _projectModel.SemanticBinding);
            inheritanceResolver.ResolveAll(ImportResolver);
            _projectModel.SemanticBinding.MaterializeInheritance();
            DualWriteAssertions.AssertInheritanceConsistency(SymbolTable, _projectModel.SemanticBinding);
            _projectModel.SemanticBinding.FreezeInheritance();
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 5: Perform semantic analysis on all files
            if (!PerformSemanticAnalysis(config, cancellationToken))
            {
                return CreateFailureResult();
            }
            _projectModel.SemanticBinding.MaterializeCodeGenInfo();
            _projectModel.SemanticBinding.MaterializeVariableTypes();
            DualWriteAssertions.AssertCodeGenInfoConsistency(SymbolTable, _projectModel.SemanticBinding);
            DualWriteAssertions.AssertVariableTypeConsistency(SymbolTable, _projectModel.SemanticBinding);
            _projectModel.SemanticBinding.FreezeVariableTypes();
            _projectModel.SemanticBinding.FreezeCodeGenInfo();
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 6: Generate C# code for all files
            var generatedCSharp = GenerateCode(config);
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 7: Compile to assembly
            return CompileAssembly(config, generatedCSharp);
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
                ProjectModel = _projectModel
            };
        }
        catch (Exception ex)
        {
            // Log full exception including stack trace for debugging
            _logger.LogError($"Project compilation failed with {ex.GetType().Name}: {ex}", 0, 0);

            // Create a user-facing error message that includes exception type for identification
            var errorMessage = ex is InternalCompilerErrorException ice
                ? $"Internal compiler error in {ice.Component} ({ex.GetType().Name}): {ex.Message}"
                : $"Project compilation failed ({ex.GetType().Name}): {ex.Message}";

            _diagnostics.AddError(errorMessage, code: DiagnosticCodes.Infrastructure.CompilationFailed);
            return new ProjectCompilationResult
            {
                Success = false,
                Diagnostics = _diagnostics,
                Metrics = _projectMetricsBacking,
                DependencyGraph = _dependencyGraph,
                ProjectModel = _projectModel
            };
        }
    }

    private static InvalidOperationException CompilationNotStarted(string fieldName)
    {
        return new InvalidOperationException(
            $"Cannot access {fieldName}: Compile() has not been called yet. " +
            "This is a compiler bug - please report it.");
    }
}
