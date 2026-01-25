using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Handles multi-file project compilation with proper dependency management
/// and two-phase type declaration collection for cross-file visibility
/// </summary>
public class ProjectCompiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;

    // Shared symbol table and semantic info across all files
    private SymbolTable _symbolTable = null!;
    private SemanticInfo _semanticInfo = null!;
    private ImportResolver _importResolver = null!;

    // Track errors and warnings
    private List<string> _errors = new();
    private List<string> _warnings = new();

    // Metrics tracking
    private ProjectCompilationMetrics _projectMetrics = null!;

    // Dependency graph for build ordering and incremental compilation
    private DependencyGraphBuilder _graphBuilder = null!;
    private DependencyGraph? _dependencyGraph;

    // Unified project model containing all CompilationUnits
    private ProjectModel? _projectModel;

    public ProjectCompiler(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = moduleRegistry;
    }

    /// <summary>
    /// Compile a Sharpy project through the multi-file compilation pipeline
    /// </summary>
    public ProjectCompilationResult Compile(ProjectConfig config)
    {
        _logger.LogInfo($"Starting project compilation: {config.RootNamespace}");

        _errors = new List<string>();
        _warnings = new List<string>();
        _projectMetrics = new ProjectCompilationMetrics(config.RootNamespace, config.Configuration);
        _projectModel = new ProjectModel(config);

        try
        {
            // Phase 1: Parse all source files
            if (!ParseAllFiles(config))
            {
                return CreateFailureResult();
            }

            // Phase 2: Initialize shared symbol table and semantic info
            InitializeSharedState();

            // Phase 3: Collect type declarations from all files (first pass - type shells only)
            CollectTypeDeclarations(config);

            // Phase 4: Resolve imports and build dependency information
            if (!ResolveImports(config))
            {
                return CreateFailureResult();
            }

            // Phase 5: Perform semantic analysis on all files
            if (!PerformSemanticAnalysis(config))
            {
                return CreateFailureResult();
            }

            // Phase 6: Generate C# code for all files
            var generatedCSharp = GenerateCode(config);

            // Phase 7: Compile to assembly
            return CompileAssembly(config, generatedCSharp);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Project compilation failed: {ex.Message}", 0, 0);
            return new ProjectCompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Project compilation failed: {ex.Message}" },
                Metrics = _projectMetrics,
                DependencyGraph = _dependencyGraph,
                ProjectModel = _projectModel
            };
        }
    }

    /// <summary>
    /// Phase 1: Parse all source files into AST modules
    /// </summary>
    private bool ParseAllFiles(ProjectConfig config)
    {
        _logger.LogInfo($"Phase 1: Parsing {config.SourceFiles.Count} source files");

        foreach (var sourceFile in config.SourceFiles)
        {
            var fileMetrics = new CompilationMetrics(
                fileName: Path.GetRelativePath(config.ProjectDirectory, sourceFile),
                projectName: config.RootNamespace,
                configuration: config.Configuration);

            try
            {
                var source = File.ReadAllText(sourceFile);

                // Create CompilationUnit for this file
                var modulePath = CompilationUnitFactory.ComputeModulePath(sourceFile, config.ProjectDirectory);
                var compilationUnit = _projectModel!.CreateUnit(sourceFile, modulePath, source);

                fileMetrics.StartPhase("Lexical Analysis");
                var lexer = new Lexer.Lexer(source, _logger);
                var tokens = lexer.TokenizeAll();
                fileMetrics.EndPhase();

                // Store tokens in CompilationUnit
                compilationUnit.Tokens = tokens;
                compilationUnit.Phase = CompilationPhase.Lexed;

                fileMetrics.StartPhase("Syntax Analysis");
                var parser = new Parser.Parser(tokens, _logger);
                var module = parser.ParseModule();
                fileMetrics.EndPhase();

                // Store AST in CompilationUnit
                compilationUnit.Ast = module;
                compilationUnit.Phase = CompilationPhase.Parsed;

                // Extract imports from AST
                var imports = new List<ImportStatement>();
                var fromImports = new List<FromImportStatement>();
                foreach (var stmt in module.Body)
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

                _projectMetrics.AddFileMetrics(fileMetrics);
            }
            catch (LexerError ex)
            {
                // Add to CompilationUnit diagnostics if available
                var unit = _projectModel!.GetUnit(sourceFile);
                if (unit != null)
                {
                    unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, sourceFile);
                    unit.Phase = CompilationPhase.Failed;
                }

                _errors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
                _projectMetrics.AddFileMetrics(fileMetrics);
            }
            catch (ParserError ex)
            {
                // Add to CompilationUnit diagnostics if available
                var unit = _projectModel!.GetUnit(sourceFile);
                if (unit != null)
                {
                    unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, sourceFile);
                    unit.Phase = CompilationPhase.Failed;
                }

                _errors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
                _projectMetrics.AddFileMetrics(fileMetrics);
            }
            catch (Exception ex)
            {
                // Add to CompilationUnit diagnostics if available
                var unit = _projectModel!.GetUnit(sourceFile);
                if (unit != null)
                {
                    unit.Diagnostics.AddError(ex.Message, filePath: sourceFile);
                    unit.Phase = CompilationPhase.Failed;
                }

                _errors.Add($"{sourceFile}: error: {ex.Message}");
                _projectMetrics.AddFileMetrics(fileMetrics);
            }
        }

        return !_errors.Any();
    }

    /// <summary>
    /// Phase 2: Initialize shared state (symbol table, semantic info)
    /// </summary>
    private void InitializeSharedState()
    {
        var builtinRegistry = new BuiltinRegistry();
        _symbolTable = new SymbolTable(builtinRegistry);
        _semanticInfo = new SemanticInfo();
        _importResolver = new ImportResolver(_logger, _moduleRegistry);

        // Create SemanticBinding for storing semantic data separate from AST
        var semanticBinding = new SemanticBinding();

        // Store in ProjectModel
        _projectModel!.GlobalSymbols = _symbolTable;
        _projectModel.SemanticInfo = _semanticInfo;
        _projectModel.SemanticBinding = semanticBinding;

        // Initialize dependency graph builder and connect to import resolver
        _graphBuilder = new DependencyGraphBuilder();
        _importResolver.SetDependencyGraphBuilder(_graphBuilder);

        // Connect SemanticBinding to import resolver for storing import data
        _importResolver.SetSemanticBinding(semanticBinding);

        // Register all parsed files in the dependency graph
        foreach (var sourceFile in _projectModel!.Units.Keys)
        {
            _graphBuilder.AddFile(sourceFile);
        }
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
    /// </summary>
    private void CollectTypeDeclarations(ProjectConfig config)
    {
        _logger.LogInfo("Phase 2: Collecting type declarations across all files");

        // Create a SINGLE NameResolver for ALL files to preserve type definition lists
        // across files for correct inheritance resolution
        var nameResolver = new NameResolver(_symbolTable, _logger);

        // Phase 3a: Collect all type declarations (shells only)
        foreach (var (_, unit) in _projectModel!.Units)
        {
            if (unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Set current file path so types know which file they're defined in
            // Use unit.FilePath for original path (Units dictionary keys are normalized)
            nameResolver.SetCurrentFilePath(unit.FilePath);

            // Only collect declarations - don't resolve inheritance yet
            // The NameResolver.ResolveDeclarations() method registers type names
            // and stores ClassDef/StructDef/InterfaceDef in internal lists
            nameResolver.ResolveDeclarations(unit.Ast);

            // Update phase
            unit.Phase = CompilationPhase.NamesResolved;
        }

        // Phase 3b: Resolve inheritance (using the SAME NameResolver instance)
        // Now all types from all files are in the internal lists, so cross-module
        // inheritance resolution will work correctly
        _logger.LogInfo("Phase 2b: Resolving inheritance across all files");
        nameResolver.ResolveInheritance();

        // Collect all errors from both declaration and inheritance phases
        if (nameResolver.Errors.Any())
        {
            foreach (var error in nameResolver.Errors)
            {
                var errorMsg = $"({error.Line},{error.Column}): error: {error.Message}";
                _projectModel!.GlobalDiagnostics.AddError(error.Message, error.Line, error.Column);
                _errors.Add(errorMsg);
            }
        }
    }

    /// <summary>
    /// Phase 4: Resolve imports and build symbol table with imported symbols
    /// </summary>
    private bool ResolveImports(ProjectConfig config)
    {
        _logger.LogInfo("Phase 3: Resolving imports and building symbol table");

        // Resolve imports for each module
        foreach (var (_, unit) in _projectModel!.Units)
        {
            if (unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Use unit.FilePath for original path (Units dictionary keys are normalized)
            _importResolver.SetCurrentModule(unit.FilePath);

            foreach (var statement in unit.Ast.Body)
            {
                if (statement is ImportStatement import)
                {
                    var modules = _importResolver.ResolveImport(import, config.ProjectDirectory);

                    // Match each resolved module with its import alias to get the correct name/alias
                    for (int i = 0; i < import.Names.Length && i < modules.Count; i++)
                    {
                        var importAlias = import.Names[i];
                        var moduleInfo = modules[i];

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
                            _symbolTable.TryDefine(aliasedModule);
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
                        var existingSymbol = _symbolTable.Lookup(rootName, searchParents: false);
                        if (existingSymbol is ModuleSymbol existingModule)
                        {
                            // Merge: add the new nested exports to the existing module
                            MergeModuleExports(existingModule, currentModule);
                        }
                        else
                        {
                            _symbolTable.TryDefine(currentModule);
                        }
                    }
                }
                else if (statement is FromImportStatement fromImport)
                {
                    var moduleInfo = _importResolver.ResolveFromImport(fromImport, config.ProjectDirectory);
                    if (moduleInfo != null)
                    {
                        // Use ReExportedSymbols which have DefiningModule set for cross-module type references
                        // This is populated by ImportResolver.ResolveFromImport via CreateReExportSymbol
                        // Check SemanticBinding first, then fall back to AST property for backward compatibility
                        var reExportedSymbols = _projectModel!.SemanticBinding?.GetReExportedSymbols(fromImport)
                                                ?? fromImport.ReExportedSymbols;
                        var symbolsToImport = reExportedSymbols ?? moduleInfo.ExportedSymbols;

                        // Add specific imported symbols (skip if already defined from project files)
                        if (fromImport.ImportAll)
                        {
                            foreach (var (name, symbol) in symbolsToImport)
                            {
                                _symbolTable.TryDefine(symbol);
                            }
                        }
                        else
                        {
                            foreach (var importAlias in fromImport.Names)
                            {
                                var symbolName = importAlias.AsName ?? importAlias.Name;
                                if (symbolsToImport.TryGetValue(symbolName, out var symbol))
                                {
                                    _symbolTable.TryDefine(symbol);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Build the dependency graph after all imports are resolved
        _dependencyGraph = _graphBuilder.Build();

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
                _projectModel!.GlobalDiagnostics.AddError(errorMsg);
                _errors.Add(errorMsg);
            }
            // Don't add import resolver errors when we have circular dependencies
            // as they would be redundant/confusing (e.g., "module not found" errors
            // that are caused by the circular import)
            return false;
        }

        // Add import resolver errors only if no circular dependencies were detected
        if (_importResolver.Errors.Any())
        {
            foreach (var error in _importResolver.Errors)
            {
                _projectModel!.GlobalDiagnostics.AddError(error.Message);
                _errors.Add(error.Message);
            }
        }

        return !_errors.Any();
    }

    /// <summary>
    /// Phase 5: Perform semantic analysis (type checking) on all modules
    /// </summary>
    private bool PerformSemanticAnalysis(ProjectConfig config)
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
                var normalized = NormalizePath(path);
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
            var unit = _projectModel!.GetUnit(sourceFile);
            if (unit == null || unit.Phase == CompilationPhase.Failed || unit.Ast == null)
                continue;

            // Get the file metrics we created during parsing
            var fileMetrics = unit.Metrics;
            if (fileMetrics == null)
                continue;

            // Type resolution
            fileMetrics.StartPhase("Type Resolution");
            var typeResolver = new TypeResolver(_symbolTable, _semanticInfo, _logger);
            fileMetrics.EndPhase();

            // Type checking
            fileMetrics.StartPhase("Type Checking");
            var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
            var typeChecker = new TypeChecker(_symbolTable, _semanticInfo, typeResolver, _logger, pipeline);

            // Determine if this file is the entry point for module-level validation
            var isEntryPoint = IsEntryPointFileForTypeCheck(sourceFile, config);
            typeChecker.CheckModule(unit.Ast, computeCodeGenInfo: config.UsePrecomputedCodeGenInfo, isEntryPoint: isEntryPoint);
            fileMetrics.EndPhase();

            if (typeChecker.Errors.Any())
            {
                // Add to unit diagnostics
                foreach (var error in typeChecker.Errors)
                {
                    unit.Diagnostics.AddError(error.Message, error.Line, error.Column, unit.FilePath);
                }
                unit.Phase = CompilationPhase.Failed;

                _errors.AddRange(typeChecker.Errors.Select(e =>
                    $"{unit.FilePath}({e.Line},{e.Column}): error: {e.Message}"));
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

        return !_errors.Any();
    }

    /// <summary>
    /// Phase 6: Generate C# code for all modules
    /// </summary>
    private Dictionary<string, string> GenerateCode(ProjectConfig config)
    {
        _logger.LogInfo("Phase 5: Code Generation");
        var generatedCSharp = new Dictionary<string, string>();
        var builtinRegistry = new BuiltinRegistry();

        foreach (var (_, unit) in _projectModel!.Units)
        {
            // Only generate code for successfully type-checked units
            if (unit.Phase != CompilationPhase.TypeChecked || unit.Ast == null)
                continue;

            // Use unit.FilePath for original path (Units dictionary keys are normalized)
            var sourceFile = unit.FilePath;

            // Get the file metrics we created during parsing
            var fileMetrics = unit.Metrics;
            var relativePath = Path.GetRelativePath(config.ProjectDirectory, sourceFile);

            fileMetrics?.StartPhase("Code Generation");

            // Determine if this file is the entry point
            var isEntryPoint = IsEntryPointFileForTypeCheck(sourceFile, config);

            var codeGenContext = new CodeGenContext(_symbolTable, builtinRegistry)
            {
                SourceFilePath = sourceFile,
                ProjectNamespace = config.RootNamespace,
                ProjectRootPath = ComputeSourceRootPath(config),
                IsEntryPoint = isEntryPoint,
                Logger = _logger,
                SemanticBinding = _projectModel.SemanticBinding
            };

            var emitter = new RoslynEmitter(codeGenContext);
            var roslynCompilationUnit = emitter.GenerateCompilationUnit(unit.Ast);
            var csharpCode = roslynCompilationUnit.ToFullString();

            fileMetrics?.EndPhase();

            // Check for code generation errors
            if (codeGenContext.HasErrors)
            {
                foreach (var error in codeGenContext.Errors)
                {
                    unit.Diagnostics.AddError(error, filePath: sourceFile);
                    _errors.Add($"{sourceFile}: error: {error}");
                }
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

            // Use relative path for C# file name
            var csharpFileName = Path.ChangeExtension(relativePath, ".cs");
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
            _projectMetrics.SetAssemblyMetrics(assemblyResult.Metrics);
        }

        if (!assemblyResult.Success)
        {
            // Add assembly errors to global diagnostics
            foreach (var error in assemblyResult.Errors)
            {
                _projectModel!.GlobalDiagnostics.AddError(error);
            }
            _errors.AddRange(assemblyResult.Errors);
            return new ProjectCompilationResult
            {
                Success = false,
                Errors = _errors,
                Warnings = assemblyResult.Warnings,
                // Include generated C# for debugging even on failure
                GeneratedCSharpFiles = generatedCSharp,
                Metrics = _projectMetrics,
                DependencyGraph = _dependencyGraph,
                ProjectModel = _projectModel
            };
        }

        _warnings.AddRange(assemblyResult.Warnings);

        return new ProjectCompilationResult
        {
            Success = true,
            OutputAssemblyPath = assemblyResult.OutputAssemblyPath,
            Warnings = _warnings,
            GeneratedCSharpFiles = generatedCSharp,
            Metrics = _projectMetrics,
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
            Errors = _errors,
            Metrics = _projectMetrics,
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
    /// Normalizes a file path for consistent comparison.
    /// Uses the same normalization logic as DependencyGraph.
    /// </summary>
    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!OperatingSystem.IsLinux())
        {
            normalized = normalized.ToLowerInvariant();
        }
        return normalized;
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
}
