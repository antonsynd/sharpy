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
            var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger, pipeline);
            typeChecker.CheckModule(module, computeCodeGenInfo: true);
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
                Logger = _logger
            };
            var emitter = new RoslynEmitter(codeGenContext);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var csharpCode = compilationUnit.ToFullString();
            metrics.EndPhase();

            // Check for code generation errors
            if (codeGenContext.HasErrors)
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = codeGenContext.Errors.ToList(),
                    Metrics = metrics
                };
            }

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
