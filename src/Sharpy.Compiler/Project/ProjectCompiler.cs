using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Project;

/// <summary>
/// Handles multi-file project compilation with proper dependency management
/// and two-phase type declaration collection for cross-file visibility
/// </summary>
public class ProjectCompiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;

    // Track parsed modules by file path
    private Dictionary<string, Module> _parsedModules = new();

    // Track file metrics by file path
    private Dictionary<string, CompilationMetrics> _fileMetrics = new();

    // Shared symbol table and semantic info across all files
    private SymbolTable _symbolTable = null!;
    private SemanticInfo _semanticInfo = null!;
    private ImportResolver _importResolver = null!;

    // Track errors and warnings
    private List<string> _errors = new();
    private List<string> _warnings = new();

    // Metrics tracking
    private ProjectCompilationMetrics _projectMetrics = null!;

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
        _parsedModules = new Dictionary<string, Module>();
        _fileMetrics = new Dictionary<string, CompilationMetrics>();

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
                Metrics = _projectMetrics
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

                fileMetrics.StartPhase("Lexical Analysis");
                var lexer = new Lexer.Lexer(source, _logger);
                var tokens = lexer.TokenizeAll();
                fileMetrics.EndPhase();

                fileMetrics.StartPhase("Syntax Analysis");
                var parser = new Parser.Parser(tokens, _logger);
                var module = parser.ParseModule();
                fileMetrics.EndPhase();

                _parsedModules[sourceFile] = module;
                _fileMetrics[sourceFile] = fileMetrics;

                // Log per-file metrics at Debug level
                if (_logger.IsEnabled(CompilerLogLevel.Debug))
                {
                    _logger.LogDebug($"Parsed {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                }

                _projectMetrics.AddFileMetrics(fileMetrics);
            }
            catch (LexerError ex)
            {
                _errors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
                _projectMetrics.AddFileMetrics(fileMetrics);
            }
            catch (ParserError ex)
            {
                _errors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
                _projectMetrics.AddFileMetrics(fileMetrics);
            }
            catch (Exception ex)
            {
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
        foreach (var (sourceFile, module) in _parsedModules)
        {
            // Set current file path so types know which file they're defined in
            nameResolver.SetCurrentFilePath(sourceFile);

            // Only collect declarations - don't resolve inheritance yet
            // The NameResolver.ResolveDeclarations() method registers type names
            // and stores ClassDef/StructDef/InterfaceDef in internal lists
            nameResolver.ResolveDeclarations(module);
        }

        // Phase 3b: Resolve inheritance (using the SAME NameResolver instance)
        // Now all types from all files are in the internal lists, so cross-module
        // inheritance resolution will work correctly
        _logger.LogInfo("Phase 2b: Resolving inheritance across all files");
        nameResolver.ResolveInheritance();

        // Collect all errors from both declaration and inheritance phases
        if (nameResolver.Errors.Any())
        {
            _errors.AddRange(nameResolver.Errors.Select(e =>
                $"({e.Line},{e.Column}): error: {e.Message}"));
        }
    }

    /// <summary>
    /// Phase 4: Resolve imports and build symbol table with imported symbols
    /// </summary>
    private bool ResolveImports(ProjectConfig config)
    {
        _logger.LogInfo("Phase 3: Resolving imports and building symbol table");

        // Resolve imports for each module
        foreach (var (sourceFile, module) in _parsedModules)
        {
            _importResolver.SetCurrentModule(sourceFile);

            foreach (var statement in module.Body)
            {
                if (statement is ImportStatement import)
                {
                    var modules = _importResolver.ResolveImport(import, config.ProjectDirectory);

                    // Match each resolved module with its import alias to get the correct name/alias
                    for (int i = 0; i < import.Names.Count && i < modules.Count; i++)
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
                        var symbolsToImport = fromImport.ReExportedSymbols ?? moduleInfo.ExportedSymbols;

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

        if (_importResolver.Errors.Any())
        {
            _errors.AddRange(_importResolver.Errors.Select(e => e.Message));
        }

        return !_errors.Any();
    }

    /// <summary>
    /// Phase 5: Perform semantic analysis (type checking) on all modules
    /// </summary>
    private bool PerformSemanticAnalysis(ProjectConfig config)
    {
        _logger.LogInfo("Phase 4: Semantic Analysis");

        foreach (var (sourceFile, module) in _parsedModules)
        {
            // Get the file metrics we created during parsing
            var fileMetrics = _fileMetrics[sourceFile];

            // Type resolution
            fileMetrics.StartPhase("Type Resolution");
            var typeResolver = new TypeResolver(_symbolTable, _semanticInfo, _logger);
            fileMetrics.EndPhase();

            // Type checking
            fileMetrics.StartPhase("Type Checking");
            var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
            var typeChecker = new TypeChecker(_symbolTable, _semanticInfo, typeResolver, _logger, pipeline);
            typeChecker.CheckModule(module);
            fileMetrics.EndPhase();

            if (typeChecker.Errors.Any())
            {
                _errors.AddRange(typeChecker.Errors.Select(e =>
                    $"{sourceFile}({e.Line},{e.Column}): error: {e.Message}"));
            }

            // Log per-file semantic analysis metrics at Debug level
            if (_logger.IsEnabled(CompilerLogLevel.Debug))
            {
                _logger.LogDebug($"Analyzed {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
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

        foreach (var (sourceFile, module) in _parsedModules)
        {
            // Get the file metrics we created during parsing
            var fileMetrics = _fileMetrics[sourceFile];
            var relativePath = Path.GetRelativePath(config.ProjectDirectory, sourceFile);

            fileMetrics.StartPhase("Code Generation");

            // Determine if this file is the entry point
            // If EntryPoint is specified in config, use that; otherwise default to main.spy
            var isEntryPoint = IsEntryPointFile(sourceFile, config);

            // Helper method to determine entry point
            bool IsEntryPointFile(string file, ProjectConfig cfg)
            {
                var fileName = Path.GetFileName(file);

                // If EntryPoint is specified in config, check against it
                if (!string.IsNullOrWhiteSpace(cfg.EntryPoint))
                {
                    return fileName.Equals(cfg.EntryPoint, StringComparison.OrdinalIgnoreCase) ||
                           fileName.Equals(Path.GetFileName(cfg.EntryPoint), StringComparison.OrdinalIgnoreCase);
                }

                // Otherwise, default to main.spy for executable projects
                var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
                return fileNameNoExt.Equals("main", StringComparison.OrdinalIgnoreCase);
            }

            var codeGenContext = new CodeGenContext(_symbolTable, builtinRegistry)
            {
                SourceFilePath = sourceFile,
                ProjectNamespace = config.RootNamespace,
                ProjectRootPath = ComputeSourceRootPath(config),
                IsEntryPoint = isEntryPoint,
                Logger = _logger
            };

            var emitter = new RoslynEmitter(codeGenContext);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var csharpCode = compilationUnit.ToFullString();

            fileMetrics.EndPhase();

            // Check for code generation errors
            if (codeGenContext.HasErrors)
            {
                foreach (var error in codeGenContext.Errors)
                {
                    _errors.Add($"{sourceFile}: error: {error}");
                }
                continue;
            }

            // Log per-file code gen metrics at Debug level
            if (_logger.IsEnabled(CompilerLogLevel.Debug))
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
            _errors.AddRange(assemblyResult.Errors);
            return new ProjectCompilationResult
            {
                Success = false,
                Errors = _errors,
                Warnings = assemblyResult.Warnings,
                Metrics = _projectMetrics
            };
        }

        _warnings.AddRange(assemblyResult.Warnings);

        return new ProjectCompilationResult
        {
            Success = true,
            OutputAssemblyPath = assemblyResult.OutputAssemblyPath,
            Warnings = _warnings,
            GeneratedCSharpFiles = generatedCSharp,
            Metrics = _projectMetrics
        };
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
            Metrics = _projectMetrics
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
}
