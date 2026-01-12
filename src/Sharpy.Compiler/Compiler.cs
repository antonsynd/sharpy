using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace Sharpy.Compiler;

/// <summary>
/// Main compiler driver orchestrating the compilation pipeline
/// </summary>
public class Compiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;

    public Compiler(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = null;
    }

    public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = new ModuleRegistry(_logger);

        // Add module search paths
        if (options.ModulePaths != null)
        {
            foreach (var path in options.ModulePaths)
            {
                _moduleRegistry.AddModulePath(path);
                _logger.LogDebug($"Added module search path: {path}");
            }
        }

        // Load referenced assemblies
        if (options.References != null)
        {
            foreach (var reference in options.References)
            {
                var success = _moduleRegistry.LoadReference(reference);
                if (success)
                {
                    _logger.LogInfo($"Loaded module reference: {reference}");
                }
                else
                {
                    _logger.LogWarning($"Failed to load module reference: {reference}", 0, 0);
                }
            }
        }
    }

    /// <summary>
    /// Compile a Sharpy project from a .spyproj file
    /// </summary>
    public ProjectCompilationResult CompileProject(ProjectConfig projectConfig)
    {
        _logger.LogInfo($"Starting project compilation: {projectConfig.RootNamespace}");
        var allErrors = new List<string>();
        var allWarnings = new List<string>();
        var projectMetrics = new ProjectCompilationMetrics(projectConfig.RootNamespace, projectConfig.Configuration);

        try
        {
            // Phase 1: Parse all source files
            _logger.LogInfo($"Phase 1: Parsing {projectConfig.SourceFiles.Count} source files");
            var parsedModules = new Dictionary<string, Module>();

            foreach (var sourceFile in projectConfig.SourceFiles)
            {
                var fileMetrics = new CompilationMetrics(
                    fileName: Path.GetRelativePath(projectConfig.ProjectDirectory, sourceFile),
                    projectName: projectConfig.RootNamespace,
                    configuration: projectConfig.Configuration);

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

                    parsedModules[sourceFile] = module;

                    // Log per-file metrics at Debug level
                    if (_logger.IsEnabled(CompilerLogLevel.Debug))
                    {
                        _logger.LogDebug($"Parsed {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                    }

                    projectMetrics.AddFileMetrics(fileMetrics);
                }
                catch (LexerError ex)
                {
                    allErrors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
                    projectMetrics.AddFileMetrics(fileMetrics);
                }
                catch (ParserError ex)
                {
                    allErrors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
                    projectMetrics.AddFileMetrics(fileMetrics);
                }
                catch (Exception ex)
                {
                    allErrors.Add($"{sourceFile}: error: {ex.Message}");
                    projectMetrics.AddFileMetrics(fileMetrics);
                }
            }

            // Stop if parsing failed
            if (allErrors.Any())
            {
                return new ProjectCompilationResult
                {
                    Success = false,
                    Errors = allErrors,
                    Metrics = projectMetrics
                };
            }

            // Phase 2: Build shared symbol table and resolve imports
            _logger.LogInfo("Phase 2: Resolving imports and building symbol table");
            var builtinRegistry = new BuiltinRegistry();
            var symbolTable = new SymbolTable(builtinRegistry);
            var semanticInfo = new SemanticInfo();
            var importResolver = new ImportResolver(_logger, _moduleRegistry);

            // Resolve imports for each module
            foreach (var (sourceFile, module) in parsedModules)
            {
                importResolver.SetCurrentModule(sourceFile);

                foreach (var statement in module.Body)
                {
                    if (statement is ImportStatement import)
                    {
                        var modules = importResolver.ResolveImport(import, projectConfig.ProjectDirectory);
                        foreach (var moduleInfo in modules)
                        {
                            // Add imported symbols to symbol table
                            foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
                            {
                                symbolTable.Define(symbol);
                            }
                        }
                    }
                    else if (statement is FromImportStatement fromImport)
                    {
                        var moduleInfo = importResolver.ResolveFromImport(fromImport, projectConfig.ProjectDirectory);
                        if (moduleInfo != null)
                        {
                            // Add specific imported symbols
                            if (fromImport.ImportAll)
                            {
                                foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
                                {
                                    symbolTable.Define(symbol);
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
                                            symbolTable.Define(aliasedSymbol);
                                        }
                                        else
                                        {
                                            symbolTable.Define(symbol);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (importResolver.Errors.Any())
            {
                allErrors.AddRange(importResolver.Errors.Select(e => e.Message));
            }

            // Phase 3: Semantic analysis for all modules
            _logger.LogInfo("Phase 3: Semantic Analysis");

            foreach (var (sourceFile, module) in parsedModules)
            {
                // Find the corresponding file metrics
                var relativePath = Path.GetRelativePath(projectConfig.ProjectDirectory, sourceFile);
                var fileMetrics = projectMetrics.FileMetrics
                    .FirstOrDefault(m => m.Phases.Any()); // Get the last added metrics

                if (fileMetrics == null)
                {
                    fileMetrics = new CompilationMetrics(
                        fileName: relativePath,
                        projectName: projectConfig.RootNamespace,
                        configuration: projectConfig.Configuration);
                }

                fileMetrics.StartPhase("Name Resolution");
                var nameResolver = new NameResolver(symbolTable, _logger);
                nameResolver.ResolveDeclarations(module);
                nameResolver.ResolveInheritance();
                fileMetrics.EndPhase();

                if (nameResolver.Errors.Any())
                {
                    allErrors.AddRange(nameResolver.Errors.Select(e =>
                        $"{sourceFile}({e.Line},{e.Column}): error: {e.Message}"));
                }

                fileMetrics.StartPhase("Type Resolution");
                var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
                fileMetrics.EndPhase();

                fileMetrics.StartPhase("Type Checking");
                var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
                typeChecker.CheckModule(module);
                fileMetrics.EndPhase();

                if (typeChecker.Errors.Any())
                {
                    allErrors.AddRange(typeChecker.Errors.Select(e =>
                        $"{sourceFile}({e.Line},{e.Column}): error: {e.Message}"));
                }

                // Log per-file semantic analysis metrics at Debug level
                if (_logger.IsEnabled(CompilerLogLevel.Debug))
                {
                    _logger.LogDebug($"Analyzed {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                }
            }

            // Stop if semantic analysis failed
            if (allErrors.Any())
            {
                return new ProjectCompilationResult
                {
                    Success = false,
                    Errors = allErrors,
                    Metrics = projectMetrics
                };
            }

            // Phase 4: Code generation for all modules
            _logger.LogInfo("Phase 4: Code Generation");
            var generatedCSharp = new Dictionary<string, string>();

            foreach (var (sourceFile, module) in parsedModules)
            {
                // Find the corresponding file metrics
                var relativePath = Path.GetRelativePath(projectConfig.ProjectDirectory, sourceFile);
                var fileMetrics = projectMetrics.FileMetrics
                    .FirstOrDefault(m => m.Phases.Any());

                if (fileMetrics != null)
                {
                    fileMetrics.StartPhase("Code Generation");
                }

                // Determine if this file is the entry point
                // Entry point is main.spy or a file that contains executable statements
                var fileName = Path.GetFileNameWithoutExtension(sourceFile);
                var isEntryPoint = fileName.Equals("main", StringComparison.OrdinalIgnoreCase);

                var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
                {
                    SourceFilePath = sourceFile,
                    ProjectNamespace = projectConfig.RootNamespace,
                    ProjectRootPath = Path.Combine(projectConfig.ProjectDirectory, "src"),
                    IsEntryPoint = isEntryPoint
                };

                var emitter = new RoslynEmitter(codeGenContext);
                var compilationUnit = emitter.GenerateCompilationUnit(module);
                var csharpCode = compilationUnit.ToFullString();

                if (fileMetrics != null)
                {
                    fileMetrics.EndPhase();

                    // Log per-file code gen metrics at Debug level
                    if (_logger.IsEnabled(CompilerLogLevel.Debug))
                    {
                        _logger.LogDebug($"Generated {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
                    }
                }

                // Use relative path for C# file name
                var csharpFileName = Path.ChangeExtension(relativePath, ".cs");
                generatedCSharp[csharpFileName] = csharpCode;
            }

            // Phase 5: Compile to assembly
            _logger.LogInfo("Phase 5: Assembly Compilation");
            var assemblyCompiler = new AssemblyCompiler(_logger);
            var assemblyResult = assemblyCompiler.CompileToAssembly(generatedCSharp, projectConfig);

            // Add assembly metrics to project metrics
            if (assemblyResult.Metrics != null)
            {
                projectMetrics.SetAssemblyMetrics(assemblyResult.Metrics);
            }

            if (!assemblyResult.Success)
            {
                allErrors.AddRange(assemblyResult.Errors);
                return new ProjectCompilationResult
                {
                    Success = false,
                    Errors = allErrors,
                    Warnings = assemblyResult.Warnings,
                    Metrics = projectMetrics
                };
            }

            allWarnings.AddRange(assemblyResult.Warnings);

            return new ProjectCompilationResult
            {
                Success = true,
                OutputAssemblyPath = assemblyResult.OutputAssemblyPath,
                Warnings = allWarnings,
                GeneratedCSharpFiles = generatedCSharp,
                Metrics = projectMetrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Project compilation failed: {ex.Message}", 0, 0);
            return new ProjectCompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Project compilation failed: {ex.Message}" },
                Metrics = projectMetrics
            };
        }
    }

    public CompilationResult Compile(string sourceCode, string filePath)
    {
        _logger.LogInfo($"Starting compilation of {filePath}");
        var metrics = new CompilationMetrics(fileName: filePath);

        try
        {
            // Phase 1: Lexical Analysis
            _logger.LogInfo("Phase 1: Lexical Analysis");
            metrics.StartPhase("Lexical Analysis");
            var lexer = new Lexer.Lexer(sourceCode, _logger);
            var tokens = lexer.TokenizeAll();
            metrics.EndPhase();

            // Phase 2: Syntax Analysis
            _logger.LogInfo("Phase 2: Syntax Analysis");
            metrics.StartPhase("Syntax Analysis");
            var parser = new Parser.Parser(tokens, _logger);
            var module = parser.ParseModule();
            metrics.EndPhase();

            // Phase 3: Semantic Analysis
            _logger.LogInfo("Phase 3: Semantic Analysis");
            var builtinRegistry = new BuiltinRegistry();
            var symbolTable = new SymbolTable(builtinRegistry);
            var semanticInfo = new SemanticInfo();

            // Check for module registry errors
            if (_moduleRegistry != null && _moduleRegistry.Errors.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = _moduleRegistry.Errors.Select(e => e.Message).ToList(),
                    Metrics = metrics
                };
            }

            // Pass 1: Name resolution (declarations)
            metrics.StartPhase("Name Resolution");
            var nameResolver = new NameResolver(symbolTable, _logger);
            nameResolver.ResolveDeclarations(module);
            nameResolver.ResolveInheritance(); // Second pass: resolve inheritance after all types are declared
            metrics.EndPhase();

            if (nameResolver.Errors.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = nameResolver.Errors.Select(e => e.Message).ToList(),
                    Metrics = metrics
                };
            }

            // Pass 2: Type resolution and type checking
            metrics.StartPhase("Type Resolution");
            var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
            metrics.EndPhase();

            metrics.StartPhase("Type Checking");
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
            typeChecker.CheckModule(module);
            metrics.EndPhase();

            if (typeChecker.Errors.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = typeChecker.Errors.Select(e => e.Message).ToList(),
                    Metrics = metrics
                };
            }

            // TODO: Pass 3: Semantic validation (will implement in Phase 3)

            // Phase 4: Code Generation - Generate C# code from AST using RoslynEmitter
            _logger.LogInfo("Phase 4: Code Generation");
            metrics.StartPhase("Code Generation");
            var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
            {
                SourceFilePath = filePath
            };
            var emitter = new RoslynEmitter(codeGenContext);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var csharpCode = compilationUnit.ToFullString();
            metrics.EndPhase();

            return new CompilationResult
            {
                Success = true,
                Module = module,
                SymbolTable = symbolTable,
                SemanticInfo = semanticInfo,
                ModuleRegistry = _moduleRegistry,
                GeneratedCSharpCode = csharpCode,
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Compilation failed with exception: {ex.Message}", 0, 0);
            return new CompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Compilation failed: {ex.Message}" },
                Metrics = metrics
            };
        }
    }
}

/// <summary>
/// Result of compilation including success status, errors, and generated artifacts
/// </summary>
public class CompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public Module? Module { get; init; }
    public SymbolTable? SymbolTable { get; init; }
    public SemanticInfo? SemanticInfo { get; init; }
    public ModuleRegistry? ModuleRegistry { get; init; }
    public string? GeneratedCSharpCode { get; init; }
    public CompilationMetrics? Metrics { get; init; }
}

/// <summary>
/// Result of project compilation
/// </summary>
public class ProjectCompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string? OutputAssemblyPath { get; init; }
    public Dictionary<string, string> GeneratedCSharpFiles { get; init; } = new();
    public ProjectCompilationMetrics? Metrics { get; init; }
}

/// <summary>
/// Options for configuring the compiler
/// </summary>
public class CompilerOptions
{
    /// <summary>
    /// Paths to search for module assemblies
    /// </summary>
    public string[]? ModulePaths { get; set; }

    /// <summary>
    /// Paths to .NET assemblies to reference
    /// </summary>
    public string[]? References { get; set; }
}
