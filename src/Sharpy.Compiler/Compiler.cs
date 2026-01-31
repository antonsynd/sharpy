#pragma warning disable CS0618 // LexerError, ParserError, and SemanticError are obsolete
using System.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Services;
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
        var projectCompiler = new ProjectCompiler(_logger, _moduleRegistry);
        return projectCompiler.Compile(projectConfig);
    }

    public CompilationResult Compile(string sourceCode, string filePath) =>
        Compile(sourceCode, filePath, CancellationToken.None);

    public CompilationResult Compile(string sourceCode, string filePath, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"Starting compilation of {filePath}");
        var metrics = new CompilationMetrics(fileName: filePath);
        var diagnostics = new DiagnosticBag();

        try
        {
            // Phase 1: Lexical Analysis
            _logger.LogInfo("Phase 1: Lexical Analysis");
            metrics.StartPhase("Lexical Analysis");
            var lexer = new Lexer.Lexer(sourceCode, _logger);
            var tokens = lexer.TokenizeAll();
            metrics.EndPhase();

            // Assertion: Lexer must produce at least an EOF token
            Debug.Assert(tokens.Count > 0, "Lexer should produce at least one token (EOF)");

            // Check for lexer errors collected via DiagnosticBag
            if (lexer.Diagnostics.HasErrors)
            {
                diagnostics.Merge(lexer.Diagnostics);
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 2: Syntax Analysis
            _logger.LogInfo("Phase 2: Syntax Analysis");
            metrics.StartPhase("Syntax Analysis");
            var parser = new Parser.Parser(tokens, _logger);
            var module = parser.ParseModule();
            metrics.EndPhase();

            // Check if parser collected any errors into DiagnosticBag
            if (parser.Diagnostics.HasErrors)
            {
                diagnostics.Merge(parser.Diagnostics);
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics
                };
            }

            // Assertion: Parser must produce a valid module with span info
            Debug.Assert(module != null, "Parser should produce a non-null Module");
            Debug.Assert(module.Body != null, "Module.Body should not be null");
            AssertStatementsHaveSpans(module);

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 3: Semantic Analysis
            _logger.LogInfo("Phase 3: Semantic Analysis");
            var builtinRegistry = new BuiltinRegistry();
            var symbolTable = new SymbolTable(builtinRegistry);
            var semanticInfo = new SemanticInfo();

            // Check for module registry errors
            if (_moduleRegistry != null && _moduleRegistry.Diagnostics.HasErrors)
            {
                diagnostics.Merge(_moduleRegistry.Diagnostics);
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics
                };
            }

            // Pass 1: Name resolution (declarations)
            metrics.StartPhase("Name Resolution");
            var nameResolver = new NameResolver(symbolTable, _logger);
            nameResolver.ResolveDeclarations(module);
            nameResolver.ResolveInheritance(); // Second pass: resolve inheritance after all types are declared
            metrics.EndPhase();

            // Assertion: After name resolution, all defined type symbols must have names
            AssertAllSymbolsHaveNames(symbolTable);

            if (nameResolver.Diagnostics.HasErrors)
            {
                diagnostics.Merge(nameResolver.Diagnostics);
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Pass 1.5: Import resolution (resolves imports and registers symbols)
            metrics.StartPhase("Import Resolution");
            var moduleSearchPaths = _moduleRegistry?.GetModulePaths()?.ToArray() ?? Array.Empty<string>();
            _logger.LogDebug($"Module search paths: [{string.Join(", ", moduleSearchPaths)}]");
            var moduleResolver = new ModuleResolver(_logger, moduleSearchPaths);
            var importResolver = new ImportResolver(_logger, _moduleRegistry, moduleResolver);
            importResolver.SetCurrentModule(filePath);

            // Get the directory of the current file as the search path
            var currentDir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            _logger.LogDebug($"Current directory for import resolution: {currentDir}");

            foreach (var statement in module.Body)
            {
                if (statement is ImportStatement import)
                {
                    var modules = importResolver.ResolveImport(import, currentDir);

                    // Register module symbols and their exports
                    for (int i = 0; i < import.Names.Length && i < modules.Count; i++)
                    {
                        var importAlias = import.Names[i];
                        var moduleInfo = modules[i];

                        // Handle aliased imports (import x as y)
                        if (importAlias.AsName != null)
                        {
                            var aliasedModule = new ModuleSymbol
                            {
                                Name = importAlias.AsName,
                                Kind = SymbolKind.Module,
                                FilePath = moduleInfo.Path,
                                Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols)
                            };
                            symbolTable.TryDefine(aliasedModule);
                        }
                        else
                        {
                            // Handle non-aliased imports by building nested module structure
                            var parts = importAlias.Name.Split('.');

                            var leafModule = new ModuleSymbol
                            {
                                Name = parts[^1],
                                Kind = SymbolKind.Module,
                                FilePath = moduleInfo.Path,
                                Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols)
                            };

                            ModuleSymbol currentModule = leafModule;
                            for (int j = parts.Length - 2; j >= 0; j--)
                            {
                                var parentModule = new ModuleSymbol
                                {
                                    Name = parts[j],
                                    Kind = SymbolKind.Module,
                                    FilePath = "",
                                    Exports = new Dictionary<string, Symbol> { { currentModule.Name, currentModule } }
                                };
                                currentModule = parentModule;
                            }

                            symbolTable.TryDefine(currentModule);
                        }
                    }
                }
                else if (statement is FromImportStatement fromImport)
                {
                    _logger.LogDebug($"Processing from-import: from {fromImport.Module} import {string.Join(", ", fromImport.Names.Select(n => n.Name))}");
                    var moduleInfo = importResolver.ResolveFromImport(fromImport, currentDir);
                    if (moduleInfo != null)
                    {
                        _logger.LogDebug($"  Module resolved: {moduleInfo.Path}");
                        _logger.LogDebug($"  Exported symbols: [{string.Join(", ", moduleInfo.ExportedSymbols.Keys)}]");
                        var reExportedSymbols = fromImport.ReExportedSymbols ?? moduleInfo.ExportedSymbols;

                        if (fromImport.ImportAll)
                        {
                            foreach (var (name, symbol) in reExportedSymbols)
                            {
                                _logger.LogDebug($"  Defining symbol (import *): {name}");
                                symbolTable.TryDefine(symbol);
                            }
                        }
                        else
                        {
                            foreach (var importAlias in fromImport.Names)
                            {
                                var symbolName = importAlias.AsName ?? importAlias.Name;
                                if (reExportedSymbols.TryGetValue(symbolName, out var symbol))
                                {
                                    _logger.LogDebug($"  Defining imported symbol: {symbol.Name} ({symbol.Kind})");
                                    symbolTable.TryDefine(symbol);
                                }
                                else
                                {
                                    _logger.LogWarning($"Symbol '{symbolName}' not found in module exports",
                                        fromImport.LineStart, fromImport.ColumnStart);
                                }
                            }
                        }
                    }
                }
            }

            // Resolve inheritance for imported types (transitive base types + imported type inheritance)
            var inheritanceResolver = new InheritanceResolver(symbolTable, _logger);
            inheritanceResolver.ResolveAll(importResolver);

            metrics.EndPhase();

            if (importResolver.Diagnostics.HasErrors)
            {
                diagnostics.Merge(importResolver.Diagnostics);
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Pass 2: Type resolution and type checking
            metrics.StartPhase("Type Resolution");
            var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
            metrics.EndPhase();

            metrics.StartPhase("Type Checking");
            var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger, pipeline);
            // Single-file compilation is always an entry point
            typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: true);
            metrics.EndPhase();

            // Assertion: After successful type checking, warn if unknown types remain
            WarnIfUnknownTypes(semanticInfo, typeChecker.Diagnostics);
            // Assertion: Type checking should have processed at least some expressions
            Debug.Assert(semanticInfo.ExpressionTypeCount > 0 || module.Body.Length == 0,
                "Type checker should record at least one expression type for non-empty modules");

            if (typeChecker.Diagnostics.HasErrors)
            {
                diagnostics.Merge(typeChecker.Diagnostics);
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // TODO: Pass 3: Semantic validation (will implement in Phase 3)

            // Phase 4: Code Generation - Generate C# code from AST using RoslynEmitter
            _logger.LogInfo("Phase 4: Code Generation");
            metrics.StartPhase("Code Generation");

            // For single-file compilation, derive a namespace from the file name
            var defaultNamespace = !string.IsNullOrEmpty(filePath)
                ? Path.GetFileNameWithoutExtension(filePath)
                : null;

            var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
            {
                SourceFilePath = filePath,
                // For single-file, we only set ProjectNamespace (not ProjectRootPath)
                // This tells the emitter to use a simple file-based namespace
                ProjectNamespace = !string.IsNullOrEmpty(defaultNamespace)
                    ? $"Sharpy.{ToPascalCase(defaultNamespace)}"
                    : null,
                // Single-file compilation is always an entry point - generate Main method
                IsEntryPoint = true,
                Logger = _logger,
                SemanticInfo = semanticInfo
            };
            var emitter = new RoslynEmitter(codeGenContext);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var csharpCode = compilationUnit.ToFullString();

            // Assertion: Generated C# should parse without syntax errors
            AssertGeneratedCSharpParses(csharpCode);

            // Check for code generation errors
            if (codeGenContext.HasErrors)
            {
                foreach (var error in codeGenContext.Errors)
                {
                    diagnostics.AddError(error, filePath: filePath, phase: CompilerPhase.CodeGeneration);
                }
                return new CompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics
                };
            }

            // Generate C# for all imported .spy modules
            var allGeneratedFiles = new Dictionary<string, string>();

            // Add entry file
            allGeneratedFiles[filePath] = csharpCode;

            // Add all imported modules
            foreach (var (modulePath, moduleInfo) in importResolver.LoadedSpyModules)
            {
                // Skip the entry file (already added)
                if (string.Equals(Path.GetFullPath(modulePath), Path.GetFullPath(filePath),
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                var moduleCs = GenerateCSharpForModule(
                    moduleInfo, symbolTable, builtinRegistry,
                    codeGenContext.ProjectNamespace, semanticInfo);

                if (moduleCs != null)
                {
                    allGeneratedFiles[modulePath] = moduleCs;
                    _logger.LogInfo($"Generated C# for imported module: {Path.GetFileName(modulePath)}");
                }
            }

            metrics.EndPhase();

            return new CompilationResult
            {
                Success = true,
                Diagnostics = diagnostics,
                Module = module,
                SymbolTable = symbolTable,
                SemanticInfo = semanticInfo,
                ModuleRegistry = _moduleRegistry,
                GeneratedCSharpCode = csharpCode,  // Keep for backward compatibility
                GeneratedCSharpFiles = allGeneratedFiles,
                Metrics = metrics
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Compilation cancelled");
            diagnostics.AddError("Compilation cancelled", filePath: filePath);
            return new CompilationResult
            {
                Success = false,
                Diagnostics = diagnostics,
                Metrics = metrics
            };
        }
        catch (LexerError ex)
        {
            _logger.LogError($"Compilation failed with lexer error: {ex.Message}", ex.Line, ex.Column);
            diagnostics.AddLexerError(ex, filePath);
            return new CompilationResult
            {
                Success = false,
                Diagnostics = diagnostics,
                Metrics = metrics
            };
        }
        catch (ParserError ex)
        {
            _logger.LogError($"Compilation failed with parser error: {ex.Message}", ex.Line, ex.Column);
            diagnostics.AddParserError(ex, filePath);
            return new CompilationResult
            {
                Success = false,
                Diagnostics = diagnostics,
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Compilation failed with exception: {ex.Message}", 0, 0);
            diagnostics.AddError($"Compilation failed: {ex.Message}", filePath: filePath);
            return new CompilationResult
            {
                Success = false,
                Diagnostics = diagnostics,
                Metrics = metrics
            };
        }
    }

    // ----- Phase Boundary Assertions (compiled out in Release) -----

    /// <summary>
    /// Verify top-level statements have TextSpan populated.
    /// </summary>
    [Conditional("DEBUG")]
    private static void AssertStatementsHaveSpans(Module module)
    {
        foreach (var stmt in module.Body)
        {
            // Import statements may not have spans (they're processed before codegen)
            if (stmt is ImportStatement or FromImportStatement)
                continue;

            Debug.Assert(stmt.Span.HasValue,
                $"Statement {stmt.GetType().Name} at line {stmt.LineStart} is missing TextSpan");
        }
    }

    /// <summary>
    /// Verify all symbols in the global scope have non-empty names.
    /// </summary>
    [Conditional("DEBUG")]
    private static void AssertAllSymbolsHaveNames(SymbolTable symbolTable)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols())
        {
            Debug.Assert(!string.IsNullOrEmpty(symbol.Name),
                $"Symbol with kind {symbol.Kind} has null/empty name");
        }
    }

    /// <summary>
    /// Log a warning if unknown expression types remain after successful type checking.
    /// Unknown types are acceptable when there are semantic errors (error recovery)
    /// or in cross-module scenarios where imported types may not be fully resolved.
    /// </summary>
    [Conditional("DEBUG")]
    private void WarnIfUnknownTypes(SemanticInfo semanticInfo, DiagnosticBag diagnostics)
    {
        if (!diagnostics.HasErrors && semanticInfo.HasUnknownExpressionTypes())
        {
            _logger.LogWarning(
                "Unknown expression types remain after type checking (possible cross-module resolution gap)", 0, 0);
        }
    }

    /// <summary>
    /// Verify generated C# code parses without syntax errors.
    /// This catches codegen bugs that produce malformed C#.
    /// </summary>
    [Conditional("DEBUG")]
    private static void AssertGeneratedCSharpParses(string csharpCode)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpCode);
        var parseDiagnostics = tree.GetDiagnostics()
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToList();
        Debug.Assert(parseDiagnostics.Count == 0,
            $"Generated C# has {parseDiagnostics.Count} parse error(s): " +
            string.Join("; ", parseDiagnostics.Take(3).Select(d => d.GetMessage())));
    }

    /// <summary>
    /// Create CompilerServices from compilation state.
    /// </summary>
    private CompilerServices CreateServices(
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        TypeResolver typeResolver,
        ClrMemberCache? clrCache = null)
    {
        return new CompilerServicesBuilder()
            .WithLogger(_logger)
            .WithSymbolTable(symbolTable)
            .WithSemanticInfo(semanticInfo)
            .WithTypeResolver(typeResolver)
            .WithClrCache(clrCache ?? new ClrMemberCache())
            .Build();
    }

    /// <summary>
    /// Resolve inheritance relationships for imported types.
    /// Delegates to <see cref="InheritanceResolver"/>.
    /// </summary>
    [Obsolete("Use InheritanceResolver.ResolveImportedTypeInheritance() instead")]
    internal static void ResolveImportedTypeInheritance(SymbolTable symbolTable, ICompilerLogger logger)
    {
        new InheritanceResolver(symbolTable, logger).ResolveImportedTypeInheritance();
    }

    /// <summary>
    /// Auto-import transitive base types from loaded modules.
    /// Delegates to <see cref="InheritanceResolver"/>.
    /// </summary>
    [Obsolete("Use InheritanceResolver.ResolveTransitiveBaseTypes() instead")]
    internal static void ResolveTransitiveBaseTypes(SymbolTable symbolTable, ImportResolver importResolver, ICompilerLogger logger)
    {
        new InheritanceResolver(symbolTable, logger).ResolveTransitiveBaseTypes(importResolver);
    }

    /// <summary>
    /// Simple PascalCase conversion for file names to namespace components.
    /// Handles snake_case, kebab-case, and ensures valid C# identifiers.
    /// </summary>
    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Replace invalid identifier characters with underscores
        var sanitized = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sanitized.Append(c);
            else
                sanitized.Append('_');
        }

        // Split by underscore and capitalize each part
        var parts = sanitized.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "_";

        var result = string.Join("", parts.Select(p =>
            char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p[1..] : "")
        ));

        // If result starts with a digit, prefix with underscore
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }

    /// <summary>
    /// Generate C# code for a single module that has already been parsed and type-checked.
    /// Used for generating code for imported modules discovered during compilation.
    /// </summary>
    private string? GenerateCSharpForModule(
        ModuleInfo moduleInfo,
        SymbolTable symbolTable,
        BuiltinRegistry builtinRegistry,
        string? projectNamespace,
        SemanticInfo? semanticInfo = null)
    {
        if (moduleInfo.Module == null || moduleInfo.IsNetModule)
            return null;

        var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
        {
            SourceFilePath = moduleInfo.Path,
            ProjectNamespace = projectNamespace,
            // Imported modules are NOT entry points - no Main method
            IsEntryPoint = false,
            Logger = _logger,
            SemanticInfo = semanticInfo
        };

        var emitter = new RoslynEmitter(codeGenContext);
        var compilationUnit = emitter.GenerateCompilationUnit(moduleInfo.Module);

        if (codeGenContext.HasErrors)
        {
            foreach (var error in codeGenContext.Errors)
            {
                _logger.LogError($"Code generation error in {moduleInfo.Path}: {error}", 0, 0);
            }
            return null;
        }

        return compilationUnit.ToFullString();
    }
}

/// <summary>
/// Result of compilation including success status, errors, and generated artifacts
/// </summary>
public class CompilationResult
{
    public bool Success { get; init; }

    /// <summary>
    /// Structured diagnostics from all compilation phases.
    /// This is the primary way to access errors, warnings, and other diagnostics.
    /// </summary>
    public DiagnosticBag Diagnostics { get; init; } = new();

    /// <summary>
    /// Backward-compatible convenience property for error messages as strings.
    /// Prefer using <see cref="Diagnostics"/> for structured access.
    /// </summary>
    public List<string> Errors
    {
        get => Diagnostics.GetErrors().Select(d => d.ToString()).ToList();
        init
        {
            // Support legacy initialization: convert string errors to diagnostics
            foreach (var error in value)
            {
                Diagnostics.AddError(error);
            }
        }
    }

    public Module? Module { get; init; }
    public SymbolTable? SymbolTable { get; init; }
    public SemanticInfo? SemanticInfo { get; init; }
    public ModuleRegistry? ModuleRegistry { get; init; }
    public string? GeneratedCSharpCode { get; init; }

    /// <summary>
    /// All generated C# code files (entry point + all imported modules).
    /// Key is the source file path, value is the generated C# code.
    /// </summary>
    public Dictionary<string, string> GeneratedCSharpFiles { get; init; } = new();

    public CompilationMetrics? Metrics { get; init; }
}

/// <summary>
/// Result of project compilation
/// </summary>
public class ProjectCompilationResult
{
    public bool Success { get; init; }

    /// <summary>
    /// Structured diagnostics from all compilation phases.
    /// This is the primary way to access errors, warnings, and other diagnostics.
    /// </summary>
    public DiagnosticBag Diagnostics { get; init; } = new();

    /// <summary>
    /// Backward-compatible convenience property for error messages as strings.
    /// Prefer using <see cref="Diagnostics"/> for structured access.
    /// </summary>
    public List<string> Errors
    {
        get => Diagnostics.GetErrors().Select(d => d.ToString()).ToList();
        init
        {
            foreach (var error in value)
            {
                Diagnostics.AddError(error);
            }
        }
    }

    /// <summary>
    /// Backward-compatible convenience property for warning messages as strings.
    /// Prefer using <see cref="Diagnostics"/> for structured access.
    /// </summary>
    public List<string> Warnings
    {
        get => Diagnostics.GetWarnings().Select(d => d.ToString()).ToList();
        init
        {
            foreach (var warning in value)
            {
                Diagnostics.AddWarning(warning);
            }
        }
    }

    public string? OutputAssemblyPath { get; init; }
    public Dictionary<string, string> GeneratedCSharpFiles { get; init; } = new();
    public ProjectCompilationMetrics? Metrics { get; init; }

    /// <summary>
    /// The dependency graph built during compilation.
    /// Available for tooling/analysis (e.g., incremental compilation, build order visualization).
    /// </summary>
    public Project.DependencyGraph? DependencyGraph { get; init; }

    /// <summary>
    /// The ProjectModel containing all CompilationUnits.
    /// Available for tooling and analysis.
    /// </summary>
    public Model.ProjectModel? ProjectModel { get; init; }
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
