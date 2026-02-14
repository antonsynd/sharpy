using System.Threading;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Handles multi-file project compilation with proper dependency management
/// and two-phase type declaration collection for cross-file visibility
/// </summary>
internal class ProjectCompiler
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

    private static InvalidOperationException CompilationNotStarted(string fieldName)
    {
        return new InvalidOperationException(
            $"Cannot access {fieldName}: Compile() has not been called yet. " +
            "This is a compiler bug - please report it.");
    }

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
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 3b: Validate restored symbols against current symbol table (DISABLED)
            //
            // The validation logic is implemented but currently disabled because:
            // 1. The existing dependency graph already handles most stale reference cases
            //    via transitive invalidation (if A imports B and B changes, A is recompiled)
            // 2. The validation has edge cases that need more work (e.g., correctly handling
            //    symbols that reference other cached symbols vs. newly compiled symbols)
            //
            // The validation infrastructure is kept for future use when we need to catch
            // subtle signature changes that don't affect the dependency graph.
            //
            // Scenarios the validation would catch (once fixed):
            // - Type renamed in a dependency (file A imports B.MyClass, B renames to NewClass)
            // - Function signature changed (return type, parameter types)
            // - Base class removed or changed incompatibly
            //
            // To re-enable: Remove the #if false and fix the validation edge cases
#if false
            if (_incremental && ValidateRestoredSymbols())
            {
                _logger.LogInfo("Parsing invalidated files after symbol validation");
                if (!ParseInvalidatedFiles(config))
                {
                    return CreateFailureResult();
                }
                CollectTypeDeclarations(config, cancellationToken);
            }
#endif
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

    /// <summary>
    /// Phase 1: Parse all source files into AST modules
    /// </summary>
    private bool ParseAllFiles(ProjectConfig config, CancellationToken cancellationToken = default)
    {
        var filesToParse = config.SourceFiles.Count - _filesToSkip.Count;
        _logger.LogInfo($"Phase 1: Parsing {filesToParse} source files ({_filesToSkip.Count} skipped)");

        foreach (var sourceFile in config.SourceFiles)
        {
            var fileMetrics = new CompilationMetrics(
                fileName: Path.GetRelativePath(config.ProjectDirectory, sourceFile),
                projectName: config.RootNamespace,
                configuration: config.Configuration);

            try
            {
                // Skip unchanged files in incremental mode
                if (_filesToSkip.Contains(sourceFile))
                {
                    var skippedModulePath = CompilationUnitFactory.ComputeModulePath(sourceFile, config.ProjectDirectory);
                    var skippedSource = File.ReadAllText(sourceFile);
                    var unit = _projectModel!.CreateUnit(sourceFile, skippedModulePath, skippedSource);

                    // Restore cached generated C# code
                    var cached = _incrementalCache?.GetFileCache(sourceFile);
                    if (cached != null)
                    {
                        unit.GeneratedCSharp = cached.GeneratedCSharp;
                    }

                    unit.Phase = CompilationPhase.Skipped;
                    ProjectMetrics.AddSkippedFile(sourceFile);

                    if (_logger.IsEnabled(CompilerLogLevel.Debug))
                    {
                        _logger.LogDebug($"Skipping {Path.GetFileName(sourceFile)} (unchanged)");
                    }
                    continue;
                }

                var source = File.ReadAllText(sourceFile);

                // Create CompilationUnit for this file
                var modulePath = CompilationUnitFactory.ComputeModulePath(sourceFile, config.ProjectDirectory);
                var compilationUnit = _projectModel!.CreateUnit(sourceFile, modulePath, source);

                cancellationToken.ThrowIfCancellationRequested();

                fileMetrics.StartPhase("Lexical Analysis");
                var sourceText = new Text.SourceText(source, sourceFile);
                var lexer = new Lexer.Lexer(sourceText, _logger, cancellationToken: cancellationToken);
                if (_maxErrors > 0)
                {
                    lexer.MaxErrors = _maxErrors;
                }
                var tokens = lexer.TokenizeAll();
                fileMetrics.EndPhase();

                // Capture token count immediately (available even if later phases fail)
                fileMetrics.TokenCount = tokens.Count;

                // Check if lexer collected any errors
                if (lexer.Diagnostics.HasErrors)
                {
                    compilationUnit.Diagnostics.Merge(lexer.Diagnostics);
                    compilationUnit.Phase = CompilationPhase.Failed;
                    fileMetrics.DiagnosticCount = lexer.Diagnostics.GetAll().Count;
                    _diagnostics.Merge(lexer.Diagnostics);
                    ProjectMetrics.AddFileMetrics(fileMetrics);
                    continue;
                }

                // Store tokens in CompilationUnit
                compilationUnit.Tokens = tokens;
                compilationUnit.Phase = CompilationPhase.Lexed;

                fileMetrics.StartPhase("Syntax Analysis");
                var parserMaxErrors = _maxErrors > 0 ? _maxErrors : 25;
                var parser = new Parser.Parser(tokens, _logger, parserMaxErrors, cancellationToken);
                var module = parser.ParseModule();
                fileMetrics.EndPhase();

                // Capture AST node count immediately (available even if later phases fail)
                if (module != null)
                {
                    fileMetrics.AstNodeCount = CountAstNodes(module);
                }

                // Check if parser collected any errors
                if (parser.Diagnostics.HasErrors)
                {
                    compilationUnit.Diagnostics.Merge(parser.Diagnostics);
                    compilationUnit.Phase = CompilationPhase.Failed;
                    fileMetrics.DiagnosticCount = parser.Diagnostics.GetAll().Count;
                    _diagnostics.Merge(parser.Diagnostics);
                    ProjectMetrics.AddFileMetrics(fileMetrics);
                    continue;
                }

                // Store AST in CompilationUnit (module is non-null at this point - parser always returns a Module)
                compilationUnit.Ast = module!;
                compilationUnit.Phase = CompilationPhase.Parsed;

                // Extract imports from AST
                var imports = new List<ImportStatement>();
                var fromImports = new List<FromImportStatement>();
                foreach (var stmt in module!.Body)
                {
                    if (stmt is ImportStatement import)
                        imports.Add(import);
                    else if (stmt is FromImportStatement fromImport)
                        fromImports.Add(fromImport);
                }
                compilationUnit.Imports = imports;
                compilationUnit.FromImports = fromImports;

                // Store metrics in CompilationUnit
                compilationUnit.Metrics = fileMetrics;

                // Log per-file metrics at Debug level
                if (_logger.IsEnabled(CompilerLogLevel.Debug))
                {
                    _logger.LogDebug($"Parsed {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                }

                ProjectMetrics.AddFileMetrics(fileMetrics);
            }
            catch (OperationCanceledException)
            {
                // Re-throw so the Compile() method's handler records CompilationCancelled
                throw;
            }
            catch (Exception ex)
            {
                // Log full exception for debugging
                _logger.LogError($"Failed to parse {sourceFile} ({ex.GetType().Name}): {ex}", 0, 0);

                // Create error message with exception type for identification
                var errorMessage = $"Failed to parse file ({ex.GetType().Name}): {ex.Message}";

                // Add to CompilationUnit diagnostics if available
                var unit = _projectModel!.GetUnit(sourceFile);
                if (unit != null)
                {
                    unit.Diagnostics.AddError(errorMessage, filePath: sourceFile, code: DiagnosticCodes.Infrastructure.FileReadError);
                    unit.Phase = CompilationPhase.Failed;
                }

                _diagnostics.AddError(errorMessage, filePath: sourceFile, code: DiagnosticCodes.Infrastructure.FileReadError);
                ProjectMetrics.AddFileMetrics(fileMetrics);
            }
        }

        return !_diagnostics.HasErrors;
    }

    /// <summary>
    /// Parses files that were invalidated after symbol validation.
    /// These files were previously skipped but their cached symbols are now stale.
    /// </summary>
    private bool ParseInvalidatedFiles(ProjectConfig config)
    {
        foreach (var sourceFile in config.SourceFiles)
        {
            var unit = _projectModel!.GetUnit(sourceFile);
            if (unit == null)
                continue;

            // Only parse files that need recompilation (phase was reset from Skipped)
            // Skip files that are still Skipped or already processed
            if (unit.Phase != CompilationPhase.Parsed && !_filesToSkip.Contains(sourceFile))
            {
                // This file was invalidated and needs to be parsed
                var fileMetrics = new CompilationMetrics(
                    fileName: Path.GetRelativePath(config.ProjectDirectory, sourceFile),
                    projectName: config.RootNamespace,
                    configuration: config.Configuration);

                try
                {
                    // The source text is already in the unit (created during initial parsing)
                    // We just need to re-parse it
                    var source = unit.SourceText;
                    unit.GeneratedCSharp = null; // Clear cached C#

                    fileMetrics.StartPhase("Lexical Analysis");
                    var sourceText = new Text.SourceText(source, sourceFile);
                    var lexer = new Lexer.Lexer(sourceText, _logger);
                    if (_maxErrors > 0)
                    {
                        lexer.MaxErrors = _maxErrors;
                    }
                    var tokens = lexer.TokenizeAll();
                    fileMetrics.EndPhase();

                    // Capture token count immediately (available even if later phases fail)
                    fileMetrics.TokenCount = tokens.Count;

                    if (lexer.Diagnostics.HasErrors)
                    {
                        unit.Diagnostics.Merge(lexer.Diagnostics);
                        unit.Phase = CompilationPhase.Failed;
                        fileMetrics.DiagnosticCount = lexer.Diagnostics.GetAll().Count;
                        _diagnostics.Merge(lexer.Diagnostics);
                        ProjectMetrics.AddFileMetrics(fileMetrics);
                        continue;
                    }

                    unit.Tokens = tokens;
                    unit.Phase = CompilationPhase.Lexed;

                    fileMetrics.StartPhase("Syntax Analysis");
                    var parserMaxErrors = _maxErrors > 0 ? _maxErrors : 25;
                    var parser = new Parser.Parser(tokens, _logger, parserMaxErrors);
                    var module = parser.ParseModule();
                    fileMetrics.EndPhase();

                    // Capture AST node count immediately (available even if later phases fail)
                    if (module != null)
                    {
                        fileMetrics.AstNodeCount = CountAstNodes(module);
                    }

                    if (parser.Diagnostics.HasErrors)
                    {
                        unit.Diagnostics.Merge(parser.Diagnostics);
                        unit.Phase = CompilationPhase.Failed;
                        fileMetrics.DiagnosticCount = parser.Diagnostics.GetAll().Count;
                        _diagnostics.Merge(parser.Diagnostics);
                        ProjectMetrics.AddFileMetrics(fileMetrics);
                        continue;
                    }

                    // Store AST (module is non-null at this point - parser always returns a Module)
                    unit.Ast = module!;
                    unit.Phase = CompilationPhase.Parsed;

                    // Extract imports from AST
                    var imports = new List<ImportStatement>();
                    var fromImports = new List<FromImportStatement>();
                    foreach (var stmt in module!.Body)
                    {
                        if (stmt is ImportStatement import)
                            imports.Add(import);
                        else if (stmt is FromImportStatement fromImport)
                            fromImports.Add(fromImport);
                    }
                    unit.Imports = imports;
                    unit.FromImports = fromImports;
                    unit.Metrics = fileMetrics;

                    if (_logger.IsEnabled(CompilerLogLevel.Debug))
                    {
                        _logger.LogDebug($"Re-parsed {Path.GetFileName(sourceFile)} (invalidated): {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                    }

                    ProjectMetrics.AddFileMetrics(fileMetrics);
                }
                catch (Exception ex)
                {
                    // Log full exception for debugging
                    _logger.LogError($"Failed to re-parse {sourceFile} ({ex.GetType().Name}): {ex}", 0, 0);

                    // Create error message with exception type for identification
                    var errorMessage = $"Failed to re-parse file ({ex.GetType().Name}): {ex.Message}";

                    unit.Diagnostics.AddError(errorMessage, filePath: sourceFile, code: DiagnosticCodes.Infrastructure.FileReadError);
                    unit.Phase = CompilationPhase.Failed;
                    _diagnostics.AddError(errorMessage, filePath: sourceFile, code: DiagnosticCodes.Infrastructure.FileReadError);
                    ProjectMetrics.AddFileMetrics(fileMetrics);
                }
            }
        }

        return !_diagnostics.HasErrors;
    }

    /// <summary>
    /// Phase 2: Initialize shared state (symbol table, semantic info)
    /// </summary>
    private void InitializeSharedState()
    {
        var builtinRegistry = new BuiltinRegistry(_logger);
        _symbolTableBacking = new SymbolTable(builtinRegistry);
        _semanticInfoBacking = new SemanticInfo();
        _importResolverBacking = new ImportResolver(_logger, _moduleRegistry);

        // Create SemanticBinding for storing semantic data separate from AST
        var semanticBinding = new SemanticBinding();

        // Store in ProjectModel
        _projectModel!.GlobalSymbols = SymbolTable;
        _projectModel.SemanticInfo = SemanticInfo;
        _projectModel.SemanticBinding = semanticBinding;

        // Initialize dependency graph builder and connect to import resolver
        _graphBuilderBacking = new DependencyGraphBuilder();
        ImportResolver.SetDependencyGraphBuilder(GraphBuilder);

        // Connect SemanticBinding to import resolver for storing import data
        ImportResolver.SetSemanticBinding(semanticBinding);

        // Register all parsed files in the dependency graph
        foreach (var sourceFile in _projectModel!.Units.Keys)
        {
            GraphBuilder.AddFile(sourceFile);
        }

        // Restore cached symbols for skipped files (incremental compilation)
        if (_incremental && _incrementalCache != null && _filesToSkip.Count > 0)
        {
            RestoreCachedSymbols();
        }
    }

    /// <summary>
    /// Restore symbols from cache for files that were skipped during incremental compilation.
    /// </summary>
    private void RestoreCachedSymbols()
    {
        if (_incrementalCache == null)
            return;

        var semanticBinding = _projectModel!.SemanticBinding;
        var restoredCount = 0;

        foreach (var filePath in _filesToSkip)
        {
            if (_incrementalCache.RestoreSymbols(filePath, _restoredSymbols))
            {
                // Register the restored symbols in the symbol table
                foreach (var symbol in _restoredSymbols.Values)
                {
                    // Only register top-level symbols (types, functions, variables)
                    // Skip parameters and other nested symbols
                    if (symbol is TypeSymbol typeSymbol)
                    {
                        SymbolTable.TryDefine(symbol);

                        // Register CodeGenInfo to maintain dual-write consistency
                        if (typeSymbol.CodeGenInfo != null)
                        {
                            semanticBinding.SetCodeGenInfo(typeSymbol, typeSymbol.CodeGenInfo);
                        }

                        // Also register variable types and CodeGenInfo for fields
                        // This ensures DualWriteAssertions pass for restored symbols
                        foreach (var field in typeSymbol.Fields)
                        {
                            if (field.Type != SemanticType.Unknown)
                            {
                                semanticBinding.SetVariableType(field, field.Type);
                            }
                            if (field.CodeGenInfo != null)
                            {
                                semanticBinding.SetCodeGenInfo(field, field.CodeGenInfo);
                            }
                        }

                        // Register CodeGenInfo for methods
                        foreach (var method in typeSymbol.Methods)
                        {
                            if (method.CodeGenInfo != null)
                            {
                                semanticBinding.SetCodeGenInfo(method, method.CodeGenInfo);
                            }
                        }

                        // Register CodeGenInfo for constructors
                        foreach (var ctor in typeSymbol.Constructors)
                        {
                            if (ctor.CodeGenInfo != null)
                            {
                                semanticBinding.SetCodeGenInfo(ctor, ctor.CodeGenInfo);
                            }
                        }
                    }
                    else if (symbol is FunctionSymbol fs)
                    {
                        SymbolTable.TryDefine(symbol);

                        // Register CodeGenInfo for functions
                        if (fs.CodeGenInfo != null)
                        {
                            semanticBinding.SetCodeGenInfo(fs, fs.CodeGenInfo);
                        }
                    }
                    else if (symbol is VariableSymbol vs && !vs.IsParameter)
                    {
                        SymbolTable.TryDefine(symbol);

                        // Register variable type and CodeGenInfo in SemanticBinding
                        if (vs.Type != SemanticType.Unknown)
                        {
                            semanticBinding.SetVariableType(vs, vs.Type);
                        }
                        if (vs.CodeGenInfo != null)
                        {
                            semanticBinding.SetCodeGenInfo(vs, vs.CodeGenInfo);
                        }
                    }
                }
                restoredCount++;
            }
        }

        if (restoredCount > 0)
        {
            _logger.LogInfo($"Restored symbols from {restoredCount} cached file(s)");
        }
    }

    /// <summary>
    /// Validates restored symbols against the current symbol table after all declarations are collected.
    /// If a restored symbol has stale references (e.g., to a type that was renamed or a function
    /// whose signature changed), the file is marked for recompilation.
    /// </summary>
    /// <returns>True if any files were invalidated and need recompilation.</returns>
    private bool ValidateRestoredSymbols()
    {
        if (_restoredSymbols.Count == 0)
            return false;

        var invalidFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var semanticBinding = _projectModel!.SemanticBinding;

        foreach (var (symbolId, symbol) in _restoredSymbols)
        {
            if (!ValidateRestoredSymbol(symbol))
            {
                var filePath = GetSymbolFilePath(symbol);
                if (filePath != null)
                {
                    var normalizedPath = PathNormalizer.Normalize(filePath);
                    invalidFiles.Add(normalizedPath);
                    _logger.LogInfo($"Restored symbol validation failed for '{symbol.Name}'; will recompile: {Path.GetFileName(filePath)}");
                }
            }
        }

        if (invalidFiles.Count == 0)
            return false;

        // Remove invalidated files from skip list so they get recompiled
        foreach (var file in invalidFiles)
        {
            // Find the original file path (before normalization) in _filesToSkip
            var originalPath = _filesToSkip.FirstOrDefault(f => PathNormalizer.Normalize(f) == file);
            if (originalPath != null)
            {
                _filesToSkip.Remove(originalPath);

                // Mark the unit as needing recompilation
                var unit = _projectModel!.GetUnit(originalPath);
                if (unit != null && unit.Phase == CompilationPhase.Skipped)
                {
                    unit.Phase = CompilationPhase.Parsed; // Reset to parsed so it gets processed
                }
            }

            // Remove invalidated symbols from the symbol table and restored set
            var symbolsToRemove = _restoredSymbols
                .Where(kvp =>
                {
                    var path = GetSymbolFilePath(kvp.Value);
                    return path != null && PathNormalizer.Normalize(path) == file;
                })
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in symbolsToRemove)
            {
                var symbolToRemove = _restoredSymbols[key];
                SymbolTable.Remove(symbolToRemove.Name);
                _restoredSymbols.Remove(key);

                // Also remove from semantic binding
                if (symbolToRemove is TypeSymbol ts)
                {
                    semanticBinding.RemoveCodeGenInfo(ts);
                    foreach (var field in ts.Fields)
                    {
                        semanticBinding.RemoveVariableType(field);
                        semanticBinding.RemoveCodeGenInfo(field);
                    }
                    foreach (var method in ts.Methods)
                    {
                        semanticBinding.RemoveCodeGenInfo(method);
                    }
                    foreach (var ctor in ts.Constructors)
                    {
                        semanticBinding.RemoveCodeGenInfo(ctor);
                    }
                }
                else if (symbolToRemove is FunctionSymbol fs)
                {
                    semanticBinding.RemoveCodeGenInfo(fs);
                }
                else if (symbolToRemove is VariableSymbol vs)
                {
                    semanticBinding.RemoveVariableType(vs);
                    semanticBinding.RemoveCodeGenInfo(vs);
                }
            }
        }

        _logger.LogInfo($"Invalidated {invalidFiles.Count} cached file(s) due to stale symbol references");
        return true;
    }

    /// <summary>
    /// Validates that a restored symbol's type references resolve in the current SymbolTable.
    /// Returns false if any reference is stale (type renamed, removed, signature changed, etc.).
    /// </summary>
    private bool ValidateRestoredSymbol(Symbol symbol)
    {
        return symbol switch
        {
            VariableSymbol v => ValidateType(v.Type),
            FunctionSymbol f => ValidateFunctionSymbol(f),
            TypeSymbol t => ValidateTypeSymbol(t),
            _ => true // Other symbol types (Module, etc.) don't have type refs to validate
        };
    }

    /// <summary>
    /// Validates that a type reference still resolves to a valid type.
    /// </summary>
    private bool ValidateType(SemanticType? type)
    {
        if (type == null)
            return true;

        return type switch
        {
            UserDefinedType udt => SymbolTable.Lookup(udt.Name) is TypeSymbol,
            GenericType gt => ValidateGenericType(gt),
            NullableType nt => ValidateType(nt.UnderlyingType),
            OptionalType ot => ValidateType(ot.UnderlyingType),
            Semantic.FunctionType ft => ValidateType(ft.ReturnType) && ft.ParameterTypes.All(ValidateType),
            Semantic.TupleType tt => tt.ElementTypes.All(ValidateType),
            ResultType rt => ValidateType(rt.OkType) && ValidateType(rt.ErrorType),
            UnionType ut => ut.CaseTypes.All(ValidateType),
            TaskType tt => ValidateType(tt.ResultType),
            // The following types are always valid (no nested types to validate)
            BuiltinType => true,
            VoidType => true,
            UnknownType => true,
            ModuleType => true,
            TypeParameterType => true,
            GenericFunctionType => true,
            // SemanticType hierarchy is sealed - this should never be reached.
            // If a new type is added, this will fail at runtime reminding the developer to update this switch.
            _ => throw new InvalidOperationException($"Unhandled SemanticType in ValidateType: {type.GetType().Name}")
        };
    }

    /// <summary>
    /// Validates a generic type (e.g., list[MyClass]).
    /// </summary>
    private bool ValidateGenericType(GenericType gt)
    {
        // Validate all type arguments
        return gt.TypeArguments.All(ValidateType);
    }

    /// <summary>
    /// Validates a function symbol's signature (return type and parameters).
    /// </summary>
    private bool ValidateFunctionSymbol(FunctionSymbol cached)
    {
        // Validate return type exists
        if (!ValidateType(cached.ReturnType))
        {
            _logger.LogDebug($"Function '{cached.Name}' has invalid return type");
            return false;
        }

        // Validate parameter types exist
        foreach (var param in cached.Parameters)
        {
            if (!ValidateType(param.Type))
            {
                _logger.LogDebug($"Function '{cached.Name}' has invalid parameter type for '{param.Name}'");
                return false;
            }
        }

        // If this function references an imported function, verify the current version has matching signature
        // Look up by the function's fully qualified name or just name
        var lookupName = cached.Name;
        var current = SymbolTable.Lookup(lookupName);

        // If the same function exists in a dependency that was recompiled, check signatures match
        if (current is FunctionSymbol currentFunc && !ReferenceEquals(cached, currentFunc))
        {
            if (!SignaturesMatch(cached, currentFunc))
            {
                _logger.LogDebug($"Function signature changed for '{cached.Name}'");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a type symbol (class/struct) for valid base type and interface references.
    /// </summary>
    private bool ValidateTypeSymbol(TypeSymbol cached)
    {
        // Validate base type still exists
        if (cached.BaseType != null)
        {
            var currentBase = SymbolTable.Lookup(cached.BaseType.Name);
            if (currentBase == null)
            {
                _logger.LogDebug($"Type '{cached.Name}' has invalid base type '{cached.BaseType.Name}'");
                return false;
            }

            // Verify the base type hasn't changed in incompatible ways
            if (currentBase is TypeSymbol currentBaseType && !ReferenceEquals(cached.BaseType, currentBaseType))
            {
                // Check that the base type still has compatible members
                // (A full signature check would be expensive; for now just check it exists)
            }
        }

        // Validate interface implementations still exist
        foreach (var iface in cached.Interfaces)
        {
            if (SymbolTable.Lookup(iface.Name) == null)
            {
                _logger.LogDebug($"Type '{cached.Name}' implements non-existent interface '{iface.Name}'");
                return false;
            }
        }

        // Validate field types
        foreach (var field in cached.Fields)
        {
            if (!ValidateType(field.Type))
            {
                _logger.LogDebug($"Type '{cached.Name}' has field '{field.Name}' with invalid type");
                return false;
            }
        }

        // Validate method signatures
        foreach (var method in cached.Methods)
        {
            if (!ValidateFunctionSymbol(method))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if two function signatures match (return type and parameter types).
    /// </summary>
    private bool SignaturesMatch(FunctionSymbol cached, FunctionSymbol current)
    {
        // Return type must match
        if (!TypesEqual(cached.ReturnType, current.ReturnType))
            return false;

        // Parameter count must match
        if (cached.Parameters.Count != current.Parameters.Count)
            return false;

        // Each parameter type must match
        for (int i = 0; i < cached.Parameters.Count; i++)
        {
            if (!TypesEqual(cached.Parameters[i].Type, current.Parameters[i].Type))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Compares two types for equality.
    /// </summary>
    private static bool TypesEqual(SemanticType? a, SemanticType? b)
    {
        if (a == null && b == null)
            return true;
        if (a == null || b == null)
            return false;

        // SemanticType records use structural equality
        return a.Equals(b);
    }

    /// <summary>
    /// Save incremental compilation caches for all successfully compiled files.
    /// </summary>
    private void SaveIncrementalCaches(ProjectConfig config)
    {
        if (_incrementalCache == null)
            return;

        var savedCount = 0;

        foreach (var (_, unit) in _projectModel!.Units)
        {
            var sourceFile = unit.FilePath;

            // Update hash for all files
            _incrementalCache.UpdateHash(sourceFile);

            // Save file cache for newly compiled files (not skipped)
            if (unit.Phase == CompilationPhase.CodeGenerated && !_filesToSkip.Contains(sourceFile))
            {
                // Extract symbols declared in this file
                var fileSymbols = ExtractFileSymbols(sourceFile);

                // Extract dependencies from the dependency graph
                var dependencies = _dependencyGraph != null
                    ? _dependencyGraph.GetDirectDependencies(sourceFile).ToList()
                    : new List<string>();

                // Save the file cache
                _incrementalCache.SaveFileCache(
                    sourceFile,
                    fileSymbols,
                    unit.GeneratedCSharp ?? string.Empty,
                    dependencies,
                    unit.ModulePath);

                savedCount++;
            }
        }

        // Persist caches to disk
        _incrementalCache.SaveAllCaches();

        if (savedCount > 0)
        {
            _logger.LogInfo($"Saved incremental cache for {savedCount} file(s)");
        }
    }

    /// <summary>
    /// Extract all symbols declared in a specific file.
    /// </summary>
    private List<Symbol> ExtractFileSymbols(string filePath)
    {
        var symbols = new List<Symbol>();

        // Get all symbols from the global scope that were declared in this file
        foreach (var symbol in SymbolTable.GlobalScope.GetAllSymbols())
        {
            // Check if the symbol was declared in this file
            var symbolFilePath = GetSymbolFilePath(symbol);
            if (symbolFilePath != null &&
                string.Equals(PathNormalizer.Normalize(symbolFilePath), PathNormalizer.Normalize(filePath), StringComparison.OrdinalIgnoreCase))
            {
                symbols.Add(symbol);
            }
        }

        return symbols;
    }

    /// <summary>
    /// Get the file path where a symbol was declared.
    /// </summary>
    private static string? GetSymbolFilePath(Symbol symbol)
    {
        return symbol switch
        {
            TypeSymbol ts => ts.DefiningFilePath,
            ModuleSymbol ms => ms.FilePath,
            _ => null
        };
    }

    /// <summary>
    /// Phase 3: Collect type declarations from all files
    /// This is a preliminary pass that registers type names in the symbol table
    /// without resolving inheritance or members yet. This enables cross-file type references.
    ///
    /// IMPORTANT: We use a SINGLE NameResolver instance across all files so that the
    /// _classDefs, _structDefs, and _interfaceDefs lists are populated with ALL type
    /// definitions before resolving inheritance. This is critical for cross-module
    /// inheritance to work correctly.
    ///
    /// NOTE: Inheritance resolution is deferred to Phase 4b (after imports are resolved)
    /// so that imported base types are available in the symbol table.
    /// </summary>
    private void CollectTypeDeclarations(ProjectConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Phase 3: Collecting type declarations across all files");

        // Create a SINGLE NameResolver for ALL files to preserve type definition lists
        // across files for correct inheritance resolution
        _sharedNameResolver = new NameResolver(SymbolTable, _logger, _projectModel!.SemanticBinding);

        // Collect all type declarations (shells only)
        foreach (var (_, unit) in _projectModel!.Units)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Set current file path so types know which file they're defined in
            // Use unit.FilePath for original path (Units dictionary keys are normalized)
            _sharedNameResolver.SetCurrentFilePath(unit.FilePath);

            // Only collect declarations - don't resolve inheritance yet
            // The NameResolver.ResolveDeclarations() method registers type names
            // and stores ClassDef/StructDef/InterfaceDef in internal lists
            _sharedNameResolver.ResolveDeclarations(unit.Ast, cancellationToken);

            // Update phase
            unit.Phase = CompilationPhase.NamesResolved;
        }

        // NOTE: Inheritance resolution is now done in ResolveInheritanceRelationships()
        // after imports are resolved (Phase 4b)

        // Collect declaration errors (inheritance errors will be collected in Phase 4b)
        if (_sharedNameResolver.Diagnostics.HasErrors)
        {
            foreach (var error in _sharedNameResolver.Diagnostics.GetErrors())
            {
                _projectModel!.GlobalDiagnostics.AddError(error.Message, error.Line, error.Column, code: error.Code);
                _diagnostics.AddError(error.Message, error.Line, error.Column, code: error.Code, phase: CompilerPhase.NameResolution);
            }
        }
    }

    /// <summary>
    /// Phase 4b: Resolve inheritance relationships
    /// This is called AFTER imports are resolved so that imported base types
    /// are available in the symbol table for cross-module inheritance.
    /// </summary>
    private void ResolveInheritanceRelationships(CancellationToken cancellationToken = default)
    {
        if (_sharedNameResolver == null)
            return;

        _logger.LogInfo("Phase 4b: Resolving inheritance across all files");

        // Track previous error count so we only collect new inheritance errors
        // (declaration errors were already collected in Phase 3)
        var previousErrorCount = _sharedNameResolver.Diagnostics.ErrorCount;

        _sharedNameResolver.ResolveInheritance(cancellationToken);

        // Collect only new inheritance errors (skip already collected declaration errors)
        var newErrors = _sharedNameResolver.Diagnostics.GetErrors().Skip(previousErrorCount);
        foreach (var error in newErrors)
        {
            _projectModel!.GlobalDiagnostics.AddError(error.Message, error.Line, error.Column, code: error.Code);
            _diagnostics.AddError(error.Message, error.Line, error.Column, code: error.Code, phase: CompilerPhase.NameResolution);
        }
    }

    /// <summary>
    /// Phase 4: Resolve imports and build symbol table with imported symbols
    /// </summary>
    private bool ResolveImports(ProjectConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Phase 3: Resolving imports and building symbol table");

        ImportResolver.SetCancellationToken(cancellationToken);

        // Resolve imports for each module
        foreach (var (_, unit) in _projectModel!.Units)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Use unit.FilePath for original path (Units dictionary keys are normalized)
            ImportResolver.SetCurrentModule(unit.FilePath);

            foreach (var statement in unit.Ast.Body)
            {
                if (statement is ImportStatement import)
                {
                    var modules = ImportResolver.ResolveImport(import, config.ProjectDirectory);

                    // Match each resolved module with its import alias to get the correct name/alias
                    for (int i = 0; i < import.Names.Length && i < modules.Count; i++)
                    {
                        var importAlias = import.Names[i];
                        var moduleInfo = modules[i];

                        // Skip failed imports (null entries maintain positional alignment)
                        if (moduleInfo == null)
                            continue;

                        // Handle aliased imports (import x as y)
                        if (importAlias.AsName != null)
                        {
                            // Create a single ModuleSymbol with the alias name
                            var aliasedModule = new ModuleSymbol
                            {
                                Name = importAlias.AsName,
                                Kind = SymbolKind.Module,
                                FilePath = moduleInfo.Path,
                                Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols)
                            };
                            SymbolTable.TryDefine(aliasedModule);
                            continue;
                        }

                        // Handle non-aliased imports by building nested module structure
                        // For "import lib.math", we need lib -> math -> (exports)
                        var parts = importAlias.Name.Split('.');

                        // Create the leaf module with actual exports
                        var leafModule = new ModuleSymbol
                        {
                            Name = parts[^1], // Last part (e.g., "math")
                            Kind = SymbolKind.Module,
                            FilePath = moduleInfo.Path,
                            Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols)
                        };

                        // Build nested structure from inside out
                        ModuleSymbol currentModule = leafModule;
                        for (int j = parts.Length - 2; j >= 0; j--)
                        {
                            var parentModule = new ModuleSymbol
                            {
                                Name = parts[j],
                                Kind = SymbolKind.Module,
                                FilePath = "", // Parent modules don't have their own file
                                Exports = new Dictionary<string, Symbol> { { currentModule.Name, currentModule } }
                            };
                            currentModule = parentModule;
                        }

                        // Register the root module (or merge with existing if it exists)
                        var rootName = parts[0];
                        var existingSymbol = SymbolTable.Lookup(rootName, searchParents: false);
                        if (existingSymbol is ModuleSymbol existingModule)
                        {
                            // Merge: add the new nested exports to the existing module
                            MergeModuleExports(existingModule, currentModule);
                        }
                        else
                        {
                            SymbolTable.TryDefine(currentModule);
                        }
                    }
                }
                else if (statement is FromImportStatement fromImport)
                {
                    var moduleInfo = ImportResolver.ResolveFromImport(fromImport, config.ProjectDirectory);
                    if (moduleInfo != null)
                    {
                        // Use ReExportedSymbols which have DefiningModule set for cross-module type references
                        // This is populated by ImportResolver.ResolveFromImport via CreateReExportSymbol
                        // Check SemanticBinding first, then fall back to AST property for backward compatibility
                        var reExportedSymbols = _projectModel!.SemanticBinding.GetReExportedSymbols(fromImport)
                                                ?? fromImport.ReExportedSymbols;
                        var symbolsToImport = reExportedSymbols ?? moduleInfo.ExportedSymbols;

                        // Add specific imported symbols (skip if already defined from project files)
                        if (fromImport.ImportAll)
                        {
                            foreach (var (name, symbol) in symbolsToImport)
                            {
                                SymbolTable.TryDefine(symbol);
                            }
                        }
                        else
                        {
                            foreach (var importAlias in fromImport.Names)
                            {
                                var symbolName = importAlias.AsName ?? importAlias.Name;
                                if (symbolsToImport.TryGetValue(symbolName, out var symbol))
                                {
                                    SymbolTable.TryDefine(symbol);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Build the dependency graph after all imports are resolved
        _dependencyGraph = GraphBuilder.Build();

        // Store in ProjectModel
        _projectModel!.DependencyGraph = _dependencyGraph;

        // Detect circular dependencies first - this is more specific than generic import errors
        var cycles = _dependencyGraph.DetectCycles();
        if (cycles.Count > 0)
        {
            foreach (var cycle in cycles)
            {
                var cycleFiles = cycle.Select(Path.GetFileName).ToList();
                var cycleDescription = string.Join(" → ", cycleFiles);
                var errorMsg = $"Circular dependency detected: {cycleDescription}";
                _projectModel!.GlobalDiagnostics.AddError(errorMsg, code: DiagnosticCodes.Semantic.CircularImport);
                _diagnostics.AddError(errorMsg, code: DiagnosticCodes.Semantic.CircularImport, phase: CompilerPhase.ImportResolution);
            }
            // Don't add import resolver errors when we have circular dependencies
            // as they would be redundant/confusing (e.g., "module not found" errors
            // that are caused by the circular import)
            return false;
        }

        // Merge all import diagnostics (errors + warnings) so they appear in the
        // final result. Continue to type checking even if imports failed, so users
        // see the full picture (import errors + type errors) — matching the
        // single-file Compiler.Compile() behavior.
        foreach (var diag in ImportResolver.Diagnostics.GetAll())
        {
            if (diag.IsError)
            {
                _projectModel!.GlobalDiagnostics.AddError(diag.Message, code: diag.Code);
                _diagnostics.AddError(diag.Message, diag.Line, diag.Column, code: diag.Code, phase: CompilerPhase.ImportResolution);
            }
            else if (diag.IsWarning)
            {
                _projectModel!.GlobalDiagnostics.AddWarning(diag.Message, code: diag.Code);
                _diagnostics.AddWarning(diag.Message, diag.Line, diag.Column, code: diag.Code, phase: CompilerPhase.ImportResolution);
            }
        }

        // Transfer root cause identifiers from import resolution to project diagnostics
        // so TypeChecker can suppress cascading errors for failed imports
        foreach (var rootCause in ImportResolver.Diagnostics.GetRootCauses())
        {
            _diagnostics.MarkAsRootCause(rootCause);
        }

        // Continue to type checking even with non-circular import errors.
        // Missing imports produce Unknown types, which prevents cascading errors
        // in the type checker (UnknownType.IsAssignableTo returns true).
        return true;
    }

    /// <summary>
    /// Phase 5: Perform semantic analysis (type checking) on all modules
    /// </summary>
    private bool PerformSemanticAnalysis(ProjectConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Phase 4: Semantic Analysis");

        // Process modules in dependency order (dependencies before dependents)
        // This ensures proper symbol resolution across modules
        IEnumerable<string> modulesToProcess;
        if (_dependencyGraph != null)
        {
            // Build a mapping from normalized paths to original paths
            var normalizedToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in _projectModel!.Units.Keys)
            {
                var normalized = PathNormalizer.Normalize(path);
                normalizedToOriginal[normalized] = path;
            }

            // Get build order and map back to original paths
            var buildOrder = _dependencyGraph.GetBuildOrder();
            modulesToProcess = buildOrder
                .Select(normalized => normalizedToOriginal.TryGetValue(normalized, out var original) ? original : null)
                .Where(path => path != null)!;
        }
        else
        {
            modulesToProcess = _projectModel!.Units.Keys;
        }

        foreach (var sourceFile in modulesToProcess)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var unit = _projectModel!.GetUnit(sourceFile);
            if (unit == null || unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Get the file metrics we created during parsing
            var fileMetrics = unit.Metrics;
            if (fileMetrics == null)
                continue;

            // Type resolution
            fileMetrics.StartPhase("Type Resolution");
            var typeResolver = new TypeResolver(SymbolTable, SemanticInfo, _logger, cancellationToken);
            fileMetrics.EndPhase();

            // Type checking
            fileMetrics.StartPhase("Type Checking");
            var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
            var semanticMaxErrors = _maxErrors > 0 ? _maxErrors : 100;
            var typeChecker = new TypeChecker(SymbolTable, SemanticInfo, typeResolver, _logger, pipeline)
            {
                CurrentFilePath = unit.FilePath,
                SemanticBinding = _projectModel.SemanticBinding,
                MaxErrors = semanticMaxErrors
            };

            // Import root causes from import resolution so TypeChecker can suppress cascading errors
            typeChecker.ImportRootCauses(_diagnostics);

            // Determine if this file is the entry point for module-level validation
            var isEntryPoint = IsEntryPointFileForTypeCheck(sourceFile, config);
            try
            {
                typeChecker.CheckModule(unit.Ast, computeCodeGenInfo: config.UsePrecomputedCodeGenInfo, isEntryPoint: isEntryPoint, cancellationToken);
            }
            catch (SemanticAnalysisException)
            {
                // End the Type Checking phase even on error for consistent metrics
                fileMetrics.EndPhase();

                // Capture artifact counts even on error paths for better observability
                fileMetrics.SymbolCount = SymbolTable.GlobalScope.GetAllSymbols().Count();
                if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> errorValidatorDict)
                {
                    fileMetrics.SetValidatorTimes(errorValidatorDict);
                }
                fileMetrics.DiagnosticCount = unit.Diagnostics.GetAll().Count + typeChecker.Diagnostics.GetAll().Count;

                // Preserve all accumulated diagnostics from the type checker
                _diagnostics.Merge(typeChecker.Diagnostics);
                unit.Phase = CompilationPhase.Failed;
                continue;
            }
            fileMetrics.EndPhase();

            // Capture per-validator timing for performance analysis
            if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> validatorDict)
            {
                fileMetrics.SetValidatorTimes(validatorDict);
            }

            // Merge all type checking diagnostics to both unit and project level
            unit.Diagnostics.Merge(typeChecker.Diagnostics);
            _diagnostics.Merge(typeChecker.Diagnostics);

            // Capture per-file artifact counts
            fileMetrics.DiagnosticCount = unit.Diagnostics.GetAll().Count;
            fileMetrics.SymbolCount = SymbolTable.GlobalScope.GetAllSymbols().Count();

            if (typeChecker.Diagnostics.HasErrors)
            {
                unit.Phase = CompilationPhase.Failed;
            }
            else
            {
                unit.Phase = CompilationPhase.TypeChecked;
            }

            // Log per-file semantic analysis metrics at Debug level
            if (_logger.IsEnabled(CompilerLogLevel.Debug))
            {
                _logger.LogDebug($"Analyzed {Path.GetFileName(unit.FilePath)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
            }
        }

        return !_diagnostics.HasErrors;
    }

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

    /// <summary>
    /// Determine if a file is the entry point for validation and code generation.
    /// Used during type checking and code generation phases.
    /// </summary>
    private static bool IsEntryPointFileForTypeCheck(string file, ProjectConfig config)
    {
        // Library projects never have an entry point file
        if (config.OutputType.Equals("library", StringComparison.OrdinalIgnoreCase))
            return false;

        var fileName = Path.GetFileName(file);

        // If EntryPoint is specified in config, check against it
        if (!string.IsNullOrWhiteSpace(config.EntryPoint))
        {
            return fileName.Equals(config.EntryPoint, StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals(Path.GetFileName(config.EntryPoint), StringComparison.OrdinalIgnoreCase);
        }

        // Otherwise, default to main.spy for executable projects
        var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
        return fileNameNoExt.Equals("main", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Create a failure result with accumulated errors
    /// </summary>
    private ProjectCompilationResult CreateFailureResult()
    {
        return new ProjectCompilationResult
        {
            Success = false,
            Diagnostics = _diagnostics,
            Metrics = ProjectMetrics,
            DependencyGraph = _dependencyGraph,
            ProjectModel = _projectModel
        };
    }

    /// <summary>
    /// Compute the source root path from the project configuration.
    /// This is the common directory containing all source files, used for relative path calculation.
    /// </summary>
    private string ComputeSourceRootPath(ProjectConfig config)
    {
        if (config.SourceFiles.Count == 0)
        {
            return config.ProjectDirectory;
        }

        // Find the common directory prefix of all source files
        var directories = config.SourceFiles
            .Select(f => Path.GetDirectoryName(Path.GetFullPath(f)))
            .Where(d => d != null)
            .Select(d => d!)
            .Distinct()
            .ToList();

        if (directories.Count == 0)
        {
            return config.ProjectDirectory;
        }

        if (directories.Count == 1)
        {
            // All files are in the same directory
            return directories[0];
        }

        // Find the longest common prefix path
        var commonPath = directories[0];
        foreach (var dir in directories.Skip(1))
        {
            commonPath = GetLongestCommonPath(commonPath, dir);
            if (string.IsNullOrEmpty(commonPath))
            {
                // No common path, fall back to project directory
                return config.ProjectDirectory;
            }
        }

        return commonPath;
    }

    /// <summary>
    /// Get the longest common path prefix between two paths.
    /// </summary>
    private string GetLongestCommonPath(string path1, string path2)
    {
        var parts1 = path1.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parts2 = path2.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var commonParts = new List<string>();
        var minLength = System.Math.Min(parts1.Length, parts2.Length);

        for (int i = 0; i < minLength; i++)
        {
            if (string.Equals(parts1[i], parts2[i], StringComparison.OrdinalIgnoreCase))
            {
                commonParts.Add(parts1[i]);
            }
            else
            {
                break;
            }
        }

        return string.Join(Path.DirectorySeparatorChar.ToString(), commonParts);
    }

    /// <summary>
    /// Merge exports from a source module into a target module.
    /// Used to combine nested module structures when the same root is imported multiple times.
    /// </summary>
    private void MergeModuleExports(ModuleSymbol target, ModuleSymbol source)
    {
        foreach (var (name, symbol) in source.Exports)
        {
            if (target.Exports.TryGetValue(name, out var existing))
            {
                // If both are modules, merge recursively
                if (existing is ModuleSymbol existingModule && symbol is ModuleSymbol sourceModule)
                {
                    MergeModuleExports(existingModule, sourceModule);
                }
                // Otherwise, the existing symbol takes precedence (first import wins)
            }
            else
            {
                target.Exports[name] = symbol;
            }
        }
    }

    /// <summary>
    /// Counts the total number of AST nodes in a module for metrics.
    /// This provides a rough measure of program complexity.
    /// Uses the AST nodes' GetChildNodes() method for recursive traversal.
    /// </summary>
    private static int CountAstNodes(Parser.Ast.Module module)
    {
        var count = 1; // Count the module itself
        var stack = new Stack<Parser.Ast.Node>();

        // Initialize stack with module body
        foreach (var statement in module.Body)
        {
            stack.Push(statement);
        }

        // Iterative depth-first traversal (more efficient than recursion for large ASTs)
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            count++;

            // Push all children onto the stack
            foreach (var child in node.GetChildNodes())
            {
                stack.Push(child);
            }
        }

        return count;
    }
}
