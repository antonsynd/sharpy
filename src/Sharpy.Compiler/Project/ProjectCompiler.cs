using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
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
    /// </summary>
    private void CollectTypeDeclarations(ProjectConfig config)
    {
        _logger.LogInfo("Phase 2: Collecting type declarations across all files");

        foreach (var (sourceFile, module) in _parsedModules)
        {
            // Use NameResolver to collect type declarations
            // This will register type names in the symbol table
            var nameResolver = new NameResolver(_symbolTable, _logger);

            // Only collect declarations - don't resolve inheritance yet
            // The NameResolver.ResolveDeclarations() method already does two passes:
            // 1. First pass: registers all type names
            // 2. Second pass: resolves members
            nameResolver.ResolveDeclarations(module);

            // Collect any errors but don't fail yet - we'll do full validation in semantic analysis
            if (nameResolver.Errors.Any())
            {
                foreach (var error in nameResolver.Errors)
                {
                    _logger.LogWarning($"{sourceFile}({error.Line},{error.Column}): {error.Message}",
                        error.Line ?? 0, error.Column ?? 0);
                }
            }
        }

        // Now that all types are declared, resolve inheritance relationships
        _logger.LogInfo("Phase 2b: Resolving inheritance across all files");
        foreach (var (sourceFile, module) in _parsedModules)
        {
            var nameResolver = new NameResolver(_symbolTable, _logger);
            nameResolver.ResolveInheritance();

            if (nameResolver.Errors.Any())
            {
                _errors.AddRange(nameResolver.Errors.Select(e =>
                    $"{sourceFile}({e.Line},{e.Column}): error: {e.Message}"));
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
        foreach (var (sourceFile, module) in _parsedModules)
        {
            _importResolver.SetCurrentModule(sourceFile);

            foreach (var statement in module.Body)
            {
                if (statement is ImportStatement import)
                {
                    var modules = _importResolver.ResolveImport(import, config.ProjectDirectory);
                    foreach (var moduleInfo in modules)
                    {
                        // Add imported symbols to symbol table
                        foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
                        {
                            _symbolTable.Define(symbol);
                        }
                    }
                }
                else if (statement is FromImportStatement fromImport)
                {
                    var moduleInfo = _importResolver.ResolveFromImport(fromImport, config.ProjectDirectory);
                    if (moduleInfo != null)
                    {
                        // Add specific imported symbols
                        if (fromImport.ImportAll)
                        {
                            foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
                            {
                                _symbolTable.Define(symbol);
                            }
                        }
                        else
                        {
                            foreach (var importAlias in fromImport.Names)
                            {
                                if (moduleInfo.ExportedSymbols.TryGetValue(importAlias.Name, out var symbol))
                                {
                                    // If there's an alias, we need to create a new symbol with the aliased name
                                    var symbolName = importAlias.AsName ?? importAlias.Name;
                                    if (symbolName != symbol.Name)
                                    {
                                        // Clone the symbol with new name
                                        var aliasedSymbol = symbol with { Name = symbolName };
                                        _symbolTable.Define(aliasedSymbol);
                                    }
                                    else
                                    {
                                        _symbolTable.Define(symbol);
                                    }
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
            var typeChecker = new TypeChecker(_symbolTable, _semanticInfo, typeResolver, _logger);
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
            // Entry point is main.spy or a file that contains executable statements
            var fileName = Path.GetFileNameWithoutExtension(sourceFile);
            var isEntryPoint = fileName.Equals("main", StringComparison.OrdinalIgnoreCase);

            var codeGenContext = new CodeGenContext(_symbolTable, builtinRegistry)
            {
                SourceFilePath = sourceFile,
                ProjectNamespace = config.RootNamespace,
                ProjectRootPath = Path.Combine(config.ProjectDirectory, "src"),
                IsEntryPoint = isEntryPoint
            };

            var emitter = new RoslynEmitter(codeGenContext);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var csharpCode = compilationUnit.ToFullString();

            fileMetrics.EndPhase();

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
}
