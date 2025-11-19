using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.CodeGen;
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

        try
        {
            // Phase 1: Parse all source files
            _logger.LogInfo($"Phase 1: Parsing {projectConfig.SourceFiles.Count} source files");
            var parsedModules = new Dictionary<string, Module>();

            foreach (var sourceFile in projectConfig.SourceFiles)
            {
                try
                {
                    var source = File.ReadAllText(sourceFile);
                    var lexer = new Lexer.Lexer(source, _logger);
                    var tokens = lexer.TokenizeAll();
                    var parser = new Parser.Parser(tokens, _logger);
                    var module = parser.ParseModule();
                    parsedModules[sourceFile] = module;
                }
                catch (LexerError ex)
                {
                    allErrors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
                }
                catch (ParserError ex)
                {
                    allErrors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    allErrors.Add($"{sourceFile}: error: {ex.Message}");
                }
            }

            // Stop if parsing failed
            if (allErrors.Any())
            {
                return new ProjectCompilationResult
                {
                    Success = false,
                    Errors = allErrors
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
                var nameResolver = new NameResolver(symbolTable, _logger);
                nameResolver.ResolveDeclarations(module);
                nameResolver.ResolveInheritance();

                if (nameResolver.Errors.Any())
                {
                    allErrors.AddRange(nameResolver.Errors.Select(e =>
                        $"{sourceFile}({e.Line},{e.Column}): error: {e.Message}"));
                }

                var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
                var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
                typeChecker.CheckModule(module);

                if (typeChecker.Errors.Any())
                {
                    allErrors.AddRange(typeChecker.Errors.Select(e =>
                        $"{sourceFile}({e.Line},{e.Column}): error: {e.Message}"));
                }
            }

            // Stop if semantic analysis failed
            if (allErrors.Any())
            {
                return new ProjectCompilationResult
                {
                    Success = false,
                    Errors = allErrors
                };
            }

            // Phase 4: Code generation for all modules
            _logger.LogInfo("Phase 4: Code Generation");
            var generatedCSharp = new Dictionary<string, string>();

            foreach (var (sourceFile, module) in parsedModules)
            {
                var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
                {
                    SourceFilePath = sourceFile,
                    ProjectNamespace = projectConfig.RootNamespace,
                    ProjectRootPath = Path.Combine(projectConfig.ProjectDirectory, "src")
                };

                var emitter = new RoslynEmitter(codeGenContext);
                var compilationUnit = emitter.GenerateCompilationUnit(module);
                var csharpCode = compilationUnit.ToFullString();

                // Use relative path for C# file name
                var relativePath = Path.GetRelativePath(projectConfig.ProjectDirectory, sourceFile);
                var csharpFileName = Path.ChangeExtension(relativePath, ".cs");
                generatedCSharp[csharpFileName] = csharpCode;
            }

            // Phase 5: Compile to assembly
            _logger.LogInfo("Phase 5: Assembly Compilation");
            var assemblyCompiler = new AssemblyCompiler(_logger);
            var assemblyResult = assemblyCompiler.CompileToAssembly(generatedCSharp, projectConfig);

            if (!assemblyResult.Success)
            {
                allErrors.AddRange(assemblyResult.Errors);
                return new ProjectCompilationResult
                {
                    Success = false,
                    Errors = allErrors,
                    Warnings = assemblyResult.Warnings
                };
            }

            allWarnings.AddRange(assemblyResult.Warnings);

            return new ProjectCompilationResult
            {
                Success = true,
                OutputAssemblyPath = assemblyResult.OutputAssemblyPath,
                Warnings = allWarnings,
                GeneratedCSharpFiles = generatedCSharp
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Project compilation failed: {ex.Message}", 0, 0);
            return new ProjectCompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Project compilation failed: {ex.Message}" }
            };
        }
    }

    public CompilationResult Compile(string sourceCode, string filePath)
    {
        _logger.LogInfo($"Starting compilation of {filePath}");

        try
        {
            // Phase 1: Lexical Analysis
            _logger.LogInfo("Phase 1: Lexical Analysis");
            var lexer = new Lexer.Lexer(sourceCode, _logger);
            var tokens = lexer.TokenizeAll();

            // Phase 2: Syntax Analysis
            _logger.LogInfo("Phase 2: Syntax Analysis");
            var parser = new Parser.Parser(tokens, _logger);
            var module = parser.ParseModule();

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
                    Errors = _moduleRegistry.Errors.Select(e => e.Message).ToList()
                };
            }

            // Pass 1: Name resolution (declarations)
            var nameResolver = new NameResolver(symbolTable, _logger);
            nameResolver.ResolveDeclarations(module);
            nameResolver.ResolveInheritance(); // Second pass: resolve inheritance after all types are declared

            if (nameResolver.Errors.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = nameResolver.Errors.Select(e => e.Message).ToList()
                };
            }

            // Pass 2: Type resolution and type checking
            var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
            typeChecker.CheckModule(module);

            if (typeChecker.Errors.Any())
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = typeChecker.Errors.Select(e => e.Message).ToList()
                };
            }

            // TODO: Pass 3: Semantic validation (will implement in Phase 3)

            // Phase 4: Code Generation - Generate C# code from AST using RoslynEmitter
            _logger.LogInfo("Phase 4: Code Generation");
            var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
            {
                SourceFilePath = filePath
            };
            var emitter = new RoslynEmitter(codeGenContext);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var csharpCode = compilationUnit.ToFullString();

            return new CompilationResult
            {
                Success = true,
                Module = module,
                SymbolTable = symbolTable,
                SemanticInfo = semanticInfo,
                ModuleRegistry = _moduleRegistry,
                GeneratedCSharpCode = csharpCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Compilation failed with exception: {ex.Message}", 0, 0);
            return new CompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Compilation failed: {ex.Message}" }
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
