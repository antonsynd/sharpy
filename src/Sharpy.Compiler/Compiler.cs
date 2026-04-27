using System.Diagnostics;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler;

/// <summary>
/// Main compiler driver orchestrating the compilation pipeline.
/// </summary>
/// <remarks>
/// <para>
/// This class is the primary public entry point for all compilation operations.
/// Use <see cref="Compile(string, string)"/> for single-file compilation and
/// <see cref="CompileProject(ProjectConfig)"/> for multi-file project compilation.
/// Both return comprehensive result objects (<see cref="CompilationResult"/> and
/// <see cref="ProjectCompilationResult"/>) that expose all intermediate artifacts
/// (tokens, AST, semantic info, generated C#, diagnostics) for tooling consumption.
/// </para>
/// <para>
/// Internal compiler components (<see cref="Lexer.Lexer"/>, <see cref="Parser.Parser"/>,
/// <see cref="Semantic.NameResolver"/>, <see cref="Semantic.TypeChecker"/>,
/// <see cref="CodeGen.RoslynEmitter"/>, etc.) should not be used directly by external
/// consumers. The only exception is diagnostic-only tools (e.g., <c>emit tokens</c>,
/// <c>emit ast</c>) that intentionally use only the lexer or parser stages.
/// </para>
/// </remarks>
public class Compiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;
    private readonly CompilerOptions _options;

    // Phase timing for structured logging
    private readonly Stopwatch _phaseStopwatch = new();
    private string? _currentPhaseName;

    public Compiler(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = null;
        _options = new CompilerOptions();
    }

    public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _options = options ?? new CompilerOptions();
        _moduleRegistry = new ModuleRegistry(_logger);

        // Add module search paths
        if (_options.ModulePaths != null)
        {
            foreach (var path in _options.ModulePaths)
            {
                _moduleRegistry.AddModulePath(path);
                _logger.LogDebug($"Added module search path: {path}");
            }
        }

        // Load referenced assemblies
        if (_options.References != null)
        {
            foreach (var reference in _options.References)
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
    public ProjectCompilationResult CompileProject(ProjectConfig projectConfig) =>
        CompileProject(projectConfig, CancellationToken.None);

    /// <summary>
    /// Compile a Sharpy project from a .spyproj file with cancellation support
    /// </summary>
    public ProjectCompilationResult CompileProject(ProjectConfig projectConfig, CancellationToken cancellationToken)
    {
        // Merge project-level and compiler-level warning/error settings
        var mergedSuppressed = new HashSet<string>(_options.SuppressedWarnings, StringComparer.OrdinalIgnoreCase);
        mergedSuppressed.UnionWith(projectConfig.SuppressedWarnings);
        var warnAsErrors = _options.WarningsAsErrors || projectConfig.WarningsAsErrors;

        var projectCompiler = new ProjectCompiler(_logger, _moduleRegistry,
            warnAsErrors, mergedSuppressed, _options.MaxErrors, _options.Incremental);
        return projectCompiler.Compile(projectConfig, cancellationToken);
    }

    /// <summary>
    /// Analyze Sharpy source code through phases 1–3 (Lexer → Parser → Semantic) without codegen.
    /// Returns the same <see cref="CompilationResult"/> shape but with no generated C#.
    /// </summary>
    public CompilationResult Analyze(string sourceCode, string filePath) =>
        Analyze(sourceCode, filePath, CancellationToken.None);

    /// <summary>
    /// Analyze Sharpy source code through phases 1–3 (Lexer → Parser → Semantic) without codegen.
    /// Returns the same <see cref="CompilationResult"/> shape but with no generated C#.
    /// </summary>
    public CompilationResult Analyze(string sourceCode, string filePath, CancellationToken cancellationToken,
        bool preserveTrivia = false) =>
        CompileInternal(sourceCode, filePath, cancellationToken, analyzeOnly: true, preserveTrivia: preserveTrivia);

    public CompilationResult Compile(string sourceCode, string filePath) =>
        Compile(sourceCode, filePath, CancellationToken.None);

    public CompilationResult Compile(string sourceCode, string filePath, CancellationToken cancellationToken) =>
        CompileInternal(sourceCode, filePath, cancellationToken, analyzeOnly: false);

    private CompilationResult CompileInternal(
        string sourceCode, string filePath, CancellationToken cancellationToken, bool analyzeOnly,
        bool preserveTrivia = false)
    {
        _logger.LogInfo($"Starting {(analyzeOnly ? "analysis" : "compilation")} of {filePath}");
        var metrics = new CompilationMetrics(fileName: filePath);
        var diagnostics = new DiagnosticBag(_options.WarningsAsErrors, _options.SuppressedWarnings);
        var result = new CompilationResultBuilder(diagnostics, metrics);
        var assertionTimer = new Stopwatch();

        try
        {
            // Phase 1: Lexical Analysis
            metrics.StartPhase("Lexical Analysis");
            LogPhaseStart("Lexical Analysis", filePath);
            var sourceText = new SourceText(sourceCode, filePath);
            result.SourceText = sourceText;
            var lexResult = FileCompilationPipeline.Lex(sourceText, _logger, _options.MaxErrors, cancellationToken, preserveTrivia);
            result.Tokens = lexResult.Tokens;
            metrics.TokenCount = lexResult.Tokens.Count;
            if (preserveTrivia)
                result.CommentSpans = ExtractCommentSpans(lexResult.Tokens);
            LogPhaseEnd(filePath, lexResult.Diagnostics.ErrorCount);
            metrics.EndPhase();

            if (lexResult.HasErrors)
                return MergeAndFail(diagnostics, lexResult.Diagnostics, metrics, result);
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 2: Syntax Analysis
            metrics.StartPhase("Syntax Analysis");
            LogPhaseStart("Syntax Analysis", filePath, lexResult.Tokens.Count);
            var parserMaxErrors = _options.MaxErrors > 0 ? _options.MaxErrors : 25;
            var parseResult = FileCompilationPipeline.Parse(lexResult.Tokens, _logger, parserMaxErrors, cancellationToken);
            var module = parseResult.Module;
            result.Module = module;
            if (module != null)
            {
                metrics.AstNodeCount = AstValidator.CountNodes(module);
            }
            LogPhaseEnd(filePath, parseResult.Diagnostics.ErrorCount);
            metrics.EndPhase();

            if (parseResult.HasErrors)
                return MergeAndFail(diagnostics, parseResult.Diagnostics, metrics, result);

            Debug.Assert(module != null, "Parser should produce a non-null Module");
            Debug.Assert(module.Body != null, "Module.Body should not be null");
            RunTimedAssertion(assertionTimer, "Post-parse", () => CompilerInvariants.AssertPostParse(module, diagnostics));

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 3: Semantic Analysis
            var builtinRegistry = new BuiltinRegistry(_logger);
            var symbolTable = new SymbolTable(builtinRegistry);
            var semanticInfo = new SemanticInfo();
            semanticInfo.SetSymbolTable(symbolTable);
            var semanticBinding = new SemanticBinding();
            result.SemanticBinding = semanticBinding;

            if (_moduleRegistry != null && _moduleRegistry.Diagnostics.HasErrors)
                return MergeAndFail(diagnostics, _moduleRegistry.Diagnostics, metrics, result);

            var pipeline = new FileCompilationPipeline(symbolTable, semanticInfo, semanticBinding, _logger);

            // Pass 1: Name resolution
            metrics.StartPhase("Name Resolution");
            LogPhaseStart("Name Resolution", filePath, module.Body.Length);
            var nameResult = pipeline.ResolveNames(module, cancellationToken);
            LogPhaseEnd(filePath, nameResult.Diagnostics.ErrorCount);
            metrics.EndPhase();
            diagnostics.Merge(nameResult.Diagnostics);

            cancellationToken.ThrowIfCancellationRequested();

            // Pass 1.5 + 1b: Import resolution + inheritance
            metrics.StartPhase("Import Resolution");
            LogPhaseStart("Import Resolution", filePath);
            var importResult = pipeline.ResolveImports(
                module, nameResult.NameResolver, filePath, _moduleRegistry, cancellationToken);
            var importResolver = importResult.ImportResolver;
            result.ImportResolver = importResolver;

            RunTimedAssertion(assertionTimer, "Post-name-resolution", () => CompilerInvariants.AssertPostNameResolution(symbolTable, diagnostics));
            RunTimedAssertion(assertionTimer, "Post-inheritance", () => CompilerInvariants.AssertPostInheritance(symbolTable, diagnostics));

            LogPhaseEnd(filePath, importResolver.Diagnostics.ErrorCount);
            metrics.EndPhase();

            if (importResolver.Diagnostics.GetAll().Count > 0)
                diagnostics.Merge(importResolver.Diagnostics);

            if (nameResult.HasErrors)
            {
                metrics.SymbolCount = symbolTable.GlobalScope.GetAllSymbols().Count();
                return FailWithDiagnostics(diagnostics, metrics, result);
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Pass 2: Type checking
            metrics.StartPhase("Type Checking");
            LogPhaseStart("Type Checking", filePath);
            var isEntryPoint = _options.OutputType.Equals("exe", StringComparison.OrdinalIgnoreCase);
            var typeCheckResult = pipeline.TypeCheck(
                module, filePath, isEntryPoint, _options.MaxErrors, diagnostics,
                computeCodeGenInfo: true, cancellationToken: cancellationToken);
            var typeChecker = typeCheckResult.TypeChecker;

            if (typeCheckResult.Aborted)
            {
                LogPhaseEnd(filePath, typeChecker.Diagnostics.ErrorCount);
                metrics.EndPhase();
                metrics.SymbolCount = symbolTable.GlobalScope.GetAllSymbols().Count();
                if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> errorValidatorDict)
                    metrics.SetValidatorTimes(errorValidatorDict);
                metrics.DiagnosticCount = diagnostics.GetAll().Count + typeChecker.Diagnostics.GetAll().Count;
                diagnostics.Merge(typeChecker.Diagnostics);
                return result.WithSymbolTable(symbolTable).WithSemanticInfo(semanticInfo).BuildFailure();
            }
            LogPhaseEnd(filePath, typeChecker.Diagnostics.ErrorCount);
            metrics.EndPhase();

            // Type-check imported .spy modules for SemanticInfo population
            pipeline.TypeCheckImportedModules(importResolver, filePath, diagnostics, cancellationToken);

            RunTimedAssertion(assertionTimer, "Post-type-checking", () => CompilerInvariants.AssertPostTypeChecking(semanticInfo, typeChecker.Diagnostics));
            AssertExpressionTypesRecorded(module, semanticInfo, diagnostics);

            RunTimedAssertion(assertionTimer, "Post-materialization", () => pipeline.MaterializeTypeInfo());

            metrics.SymbolCount = symbolTable.GlobalScope.GetAllSymbols().Count();
            if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> validatorDict)
                metrics.SetValidatorTimes(validatorDict);
            diagnostics.Merge(typeChecker.Diagnostics);

            if (diagnostics.HasErrors)
                return FailWithDiagnostics(diagnostics, metrics, result.WithSymbolTable(symbolTable).WithSemanticInfo(semanticInfo));
            cancellationToken.ThrowIfCancellationRequested();

            if (analyzeOnly)
            {
                metrics.DiagnosticCount = diagnostics.GetAll().Count;
                return result
                    .WithSuccess(!diagnostics.HasErrors)
                    .WithSymbolTable(symbolTable).WithSemanticInfo(semanticInfo)
                    .WithModuleRegistry(_moduleRegistry).Build();
            }

            // Phase 4: Code Generation
            metrics.StartPhase("Code Generation");
            LogPhaseStart("Code Generation", filePath);

            var codeGenResult = pipeline.GenerateCode(
                module, filePath, importResolver, builtinRegistry,
                isEntryPoint, "", _logger, cancellationToken);

            if (codeGenResult.HasErrors)
            {
                LogPhaseEnd(filePath, codeGenResult.Diagnostics.ErrorCount);
                metrics.EndPhase();
                return MergeAndFail(diagnostics, codeGenResult.Diagnostics, metrics, result);
            }

            // Emit CodeGenEvent with the size of generated code
            if (_logger.SupportsStructuredLogging)
            {
                var totalBytes = codeGenResult.AllGeneratedFiles.Values.Sum(cs => System.Text.Encoding.UTF8.GetByteCount(cs));
                _logger.LogEvent(new CodeGenEvent("CSharp", totalBytes) { FilePath = filePath });
            }

            LogPhaseEnd(filePath, codeGenResult.Diagnostics.ErrorCount);
            metrics.EndPhase();
            metrics.DiagnosticCount = diagnostics.GetAll().Count;

            return result
                .WithSuccess(!diagnostics.HasErrors)
                .WithSymbolTable(symbolTable).WithSemanticInfo(semanticInfo)
                .WithModuleRegistry(_moduleRegistry)
                .WithGeneratedCSharpCode(codeGenResult.CSharpCode)
                .WithGeneratedCSharpFiles(codeGenResult.AllGeneratedFiles)
                .Build();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Compilation cancelled");
            diagnostics.AddError("Compilation cancelled", filePath: filePath, code: DiagnosticCodes.Infrastructure.CompilationCancelled);
            return result.BuildFailure();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Compilation failed with {ex.GetType().Name}: {ex}", 0, 0);
            var errorMessage = ex is InternalCompilerErrorException ice
                ? $"Internal compiler error in {ice.Component} ({ex.GetType().Name}): {ex.Message}"
                : $"Compilation failed ({ex.GetType().Name}): {ex.Message}";
            diagnostics.AddError(errorMessage, filePath: filePath, code: DiagnosticCodes.Infrastructure.CompilationFailed);
            return result.BuildFailure();
        }
    }

    // ----- Helpers -----

    private static IReadOnlyList<CommentSpan> ExtractCommentSpans(IReadOnlyList<Lexer.Token> tokens)
    {
        var spans = new List<CommentSpan>();
        var seen = new HashSet<(int, int)>();
        foreach (var tok in tokens)
        {
            AppendCommentSpans(tok.LeadingTrivia, spans, seen);
            AppendCommentSpans(tok.TrailingTrivia, spans, seen);
        }
        return spans;

        static void AppendCommentSpans(
            IReadOnlyList<Lexer.Trivia>? trivia,
            List<CommentSpan> spans,
            HashSet<(int, int)> seen)
        {
            if (trivia == null)
                return;
            foreach (var t in trivia)
            {
                if (t.Kind != Lexer.TriviaKind.Comment)
                    continue;
                var key = (t.Line, t.Column);
                if (!seen.Add(key))
                    continue;
                spans.Add(new CommentSpan(t.Line, t.Column, t.Column + t.Text.Length));
            }
        }
    }

    private static CompilationResult MergeAndFail(
        DiagnosticBag target, DiagnosticBag source, CompilationMetrics metrics, CompilationResultBuilder result)
    {
        target.Merge(source);
        metrics.DiagnosticCount = target.GetAll().Count;
        return result.BuildFailure();
    }

    private static CompilationResult FailWithDiagnostics(
        DiagnosticBag diagnostics, CompilationMetrics metrics, CompilationResultBuilder result)
    {
        metrics.DiagnosticCount = diagnostics.GetAll().Count;
        return result.BuildFailure();
    }

    private void RunTimedAssertion(Stopwatch timer, string label, Action action)
    {
        timer.Restart();
        action();
        timer.Stop();
        _logger.LogDebug($"{label} assertions completed in {timer.ElapsedMilliseconds}ms");
    }

    private static void AssertExpressionTypesRecorded(Module module, SemanticInfo semanticInfo, DiagnosticBag diagnostics)
    {
        var hasOnlyDeclarations = module.Body.All(s =>
            s is InterfaceDef or ClassDef or StructDef or FunctionDef
            or ImportStatement or FromImportStatement or TypeAlias);
        Debug.Assert(semanticInfo.ExpressionTypeCount > 0 || module.Body.Length == 0 || diagnostics.HasErrors || hasOnlyDeclarations,
            "Type checker should record at least one expression type for non-empty error-free modules with executable statements");
    }

    private void LogPhaseStart(string phaseName, string? filePath = null, int nodeCount = 0)
    {
        _currentPhaseName = phaseName;
        _phaseStopwatch.Restart();

        if (_logger.SupportsStructuredLogging)
        {
            _logger.LogEvent(new PhaseStartEvent(phaseName, nodeCount) { FilePath = filePath });
        }
    }

    private void LogPhaseEnd(string? filePath = null, int errorCount = 0)
    {
        _phaseStopwatch.Stop();

        if (_logger.SupportsStructuredLogging && _currentPhaseName != null)
        {
            _logger.LogEvent(new PhaseEndEvent(_currentPhaseName, _phaseStopwatch.Elapsed, errorCount) { FilePath = filePath });
        }

        _currentPhaseName = null;
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

    public Module? Module { get; init; }
    public SymbolTable? SymbolTable { get; init; }
    public SemanticInfo? SemanticInfo { get; init; }
    public ISemanticQuery? SemanticQuery => SemanticInfo;
    internal ModuleRegistry? ModuleRegistry { get; init; }
    public string? GeneratedCSharpCode { get; init; }

    /// <summary>
    /// All generated C# code files (entry point + all imported modules).
    /// Key is the source file path, value is the generated C# code.
    /// </summary>
    public Dictionary<string, string> GeneratedCSharpFiles { get; init; } = new();

    public CompilationMetrics? Metrics { get; init; }

    /// <summary>
    /// The source text used for compilation.
    /// Available for tooling that needs structured source access (e.g., LSP, diagnostic rendering).
    /// </summary>
    public Text.SourceText? SourceText { get; init; }

    /// <summary>
    /// The token list produced by the lexer.
    /// Available for tooling that needs token-level access (e.g., syntax highlighting, LSP).
    /// </summary>
    public IReadOnlyList<Lexer.Token>? Tokens { get; init; }

    /// <summary>
    /// The semantic binding data from semantic analysis.
    /// Available for tooling that needs semantic information (e.g., LSP go-to-definition, hover).
    /// </summary>
    public SemanticBinding? SemanticBinding { get; init; }

    /// <summary>
    /// The import resolver with loaded module information.
    /// Available for tooling that needs resolved module info (e.g., LSP go-to-definition across modules).
    /// </summary>
    internal ImportResolver? ImportResolver { get; init; }

    /// <summary>
    /// Read-only query interface for import resolution information.
    /// </summary>
    public IImportQuery? Imports => ImportResolver != null ? new ImportQueryAdapter(ImportResolver) : null;

    /// <summary>
    /// Comment spans extracted from trivia when <c>preserveTrivia</c> is enabled.
    /// Available for tooling that needs comment location data (e.g., LSP hover filtering).
    /// </summary>
    public IReadOnlyList<CommentSpan>? CommentSpans { get; init; }
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

    public string? OutputAssemblyPath { get; init; }
    public Dictionary<string, string> GeneratedCSharpFiles { get; init; } = new();
    public ProjectCompilationMetrics? Metrics { get; init; }

    /// <summary>
    /// The dependency graph built during compilation.
    /// Available for tooling/analysis (e.g., incremental compilation, build order visualization).
    /// </summary>
    internal Project.DependencyGraph? DependencyGraph { get; init; }

    /// <summary>
    /// Read-only query interface for file dependency information.
    /// </summary>
    public IDependencyQuery? Dependencies => DependencyGraph;

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

    /// <summary>
    /// Treat all warnings as errors. When true, any warning causes compilation
    /// to report failure (warnings are promoted to error severity).
    /// </summary>
    public bool WarningsAsErrors { get; set; }

    /// <summary>
    /// Warning codes to suppress (e.g., "SPY0451", "SPY0452").
    /// Suppressed warnings are silently discarded and do not appear in diagnostics.
    /// </summary>
    public HashSet<string> SuppressedWarnings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maximum number of errors before the compiler stops reporting.
    /// Applies to both parser and semantic analysis.
    /// Default: 0 (use component defaults: 25 for parser, 100 for semantic).
    /// </summary>
    public int MaxErrors { get; set; }

    /// <summary>
    /// Enable incremental compilation. When true, only files that have changed
    /// (or whose dependencies have changed) are recompiled. File content hashes
    /// are cached in the project's obj/ directory.
    /// </summary>
    public bool Incremental { get; set; }

    /// <summary>
    /// Output type: "exe" or "library". Controls whether the compiler requires
    /// a main() entry point and generates a Main method.
    /// Default: "exe" (entry point required).
    /// </summary>
    public string OutputType { get; set; } = "exe";
}
