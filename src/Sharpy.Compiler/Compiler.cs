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
    private readonly ICodeEmitterFactory _emitterFactory;
    private readonly CompilerOptions _options;

    // Phase timing for structured logging
    private readonly Stopwatch _phaseStopwatch = new();
    private string? _currentPhaseName;

    // Accumulated time spent loading module references (module discovery). This runs
    // before any CompilationMetrics object exists (in the ctor and in CompileProject),
    // so it is stashed here and recorded as a first-class phase once metrics are created.
    private TimeSpan _discoveryTime;

    public Compiler(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = null;
        _emitterFactory = new RoslynEmitterFactory();
        _options = new CompilerOptions();
    }

    public Compiler(CompilerOptions options, ICompilerLogger? logger = null)
        : this(options, logger, emitterFactory: null)
    {
    }

    internal Compiler(CompilerOptions options, ICompilerLogger? logger,
        ICodeEmitterFactory? emitterFactory)
    {
        _logger = logger ?? NullLogger.Instance;
        _emitterFactory = emitterFactory ?? new RoslynEmitterFactory();
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
            var discoveryStopwatch = Stopwatch.StartNew();
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
            discoveryStopwatch.Stop();
            _discoveryTime += discoveryStopwatch.Elapsed;
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

        // Union CLI-supplied features with the project's <Features>, mirroring the
        // SuppressedWarnings merge above. Names are already validated at each boundary
        // (CLI arg parsing and project load), so this is a pure union.
        var mergedFeatures = _options.Features.Enable(projectConfig.Features);

        // Resolve NuGet package references so their types are available during semantic analysis
        if (_moduleRegistry != null && projectConfig.PackageReferences.Count > 0)
        {
            var discoveryStopwatch = Stopwatch.StartNew();
            foreach (var packageRef in projectConfig.PackageReferences)
            {
                var packageAssemblies = Project.NuGetResolver.ResolvePackage(packageRef, projectConfig.TargetFramework, _logger);
                foreach (var assemblyPath in packageAssemblies)
                    _moduleRegistry.LoadReference(assemblyPath);
            }
            discoveryStopwatch.Stop();
            _discoveryTime += discoveryStopwatch.Elapsed;
        }

        var projectCompiler = new ProjectCompiler(_logger, _moduleRegistry,
            warnAsErrors, mergedSuppressed, _options.MaxErrors, _options.Incremental,
            _emitterFactory, mergedFeatures);
        var projectResult = projectCompiler.Compile(projectConfig, cancellationToken);

        // Record module discovery (reference + NuGet loading) as a project-level phase,
        // distinct from AssemblyCompiler's Reference Resolution (Roslyn metadata refs).
        if (_discoveryTime > TimeSpan.Zero)
        {
            projectResult.Metrics?.SetDiscoveryTime(_discoveryTime);
        }

        return projectResult;
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
        AnalyzeInternal(sourceCode, filePath, cancellationToken, preserveTrivia: preserveTrivia);

    public CompilationResult Compile(string sourceCode, string filePath) =>
        Compile(sourceCode, filePath, CancellationToken.None);

    /// <summary>
    /// Compiles a single Sharpy file through the unified pipeline: the entry file plus the
    /// transitive closure of its local <c>.spy</c> imports is lowered to a synthetic
    /// project-of-one-file and driven through <see cref="ProjectCompiler"/> (#1038). There is
    /// no separate single-file codegen path — this is a thin facade over the one code path from
    /// source to generated C#.
    /// </summary>
    public CompilationResult Compile(string sourceCode, string filePath, CancellationToken cancellationToken)
    {
        _logger.LogInfo($"Starting compilation of {filePath}");

        // The entry file's source is fed in-memory (see SyntheticProject.BuildConfig) keyed by
        // the path below, so no temp file is created and the caller's path is preserved verbatim
        // for #line directives and deterministic output. On-disk callers still get a canonical
        // absolute path so their local imports resolve.
        var entryFilePath = File.Exists(filePath) ? Path.GetFullPath(filePath) : filePath;

        var config = SyntheticProject.BuildConfig(sourceCode, entryFilePath, _options, _logger);
        var projectCompiler = new ProjectCompiler(_logger, _moduleRegistry,
            _options.WarningsAsErrors, _options.SuppressedWarnings, _options.MaxErrors,
            incremental: false, _emitterFactory, _options.Features);

        // Emit an assembly from inside the project pipeline only when a concrete output path was
        // requested; otherwise stop after codegen (single-file callers historically only produced
        // generated C#, leaving assembly emission to the CLI).
        var emitAssembly = !string.IsNullOrEmpty(_options.OutputAssemblyPath);
        var projectResult = projectCompiler.Compile(config, cancellationToken, emitAssembly);

        // Module discovery ran in the constructor (loading referenced assemblies/stdlib); surface
        // it on the reconstituted per-file metrics, matching the project path.
        if (_discoveryTime > TimeSpan.Zero)
            projectResult.Metrics?.SetDiscoveryTime(_discoveryTime);

        // Reference-load failures are recorded on the shared module registry in the constructor,
        // before the project pipeline runs; surface them here so single-file callers still see
        // the "failed to load reference" error the legacy path reported.
        var registryHasErrors = false;
        if (_moduleRegistry != null && _moduleRegistry.Diagnostics.HasErrors)
        {
            projectResult.Diagnostics.Merge(_moduleRegistry.Diagnostics);
            registryHasErrors = true;
        }

        return MapProjectResult(projectResult, entryFilePath, filePath, sourceCode, registryHasErrors);
    }

    /// <summary>
    /// Reconstitutes a single-file <see cref="CompilationResult"/> from the unified project
    /// pipeline's per-unit artifacts, without re-running any analysis. <see cref="SourceText"/>
    /// carries the caller-facing path so single-file consumers see the path they passed in.
    /// </summary>
    private CompilationResult MapProjectResult(
        ProjectCompilationResult projectResult, string entryFilePath, string originalFilePath,
        string sourceCode, bool registryHasErrors)
    {
        var model = projectResult.ProjectModel;
        Model.CompilationUnit? entryUnit = null;
        var generatedFiles = new Dictionary<string, string>();
        if (model != null)
        {
            foreach (var unit in model.Units.Values)
            {
                if (unit.GeneratedCSharp != null)
                    generatedFiles[unit.FilePath] = unit.GeneratedCSharp;
                if (SyntheticProject.PathsEqual(unit.FilePath, entryFilePath))
                    entryUnit = unit;
            }
        }

        if (entryUnit?.Metrics != null)
        {
            // For a project-of-one-file, every diagnostic belongs to the single entry file, so
            // surface the full count on its per-file metrics (error-path parity with the former
            // single-file driver).
            entryUnit.Metrics.DiagnosticCount = projectResult.Diagnostics.GetAll().Count;

            // Reference/stdlib discovery ran in the constructor before any per-file metrics existed;
            // record it as a first-class phase on the entry file's metrics.
            if (_discoveryTime > TimeSpan.Zero
                && !entryUnit.Metrics.Phases.Any(p => p.Name == CompilerPhaseNames.ModuleDiscovery))
            {
                entryUnit.Metrics.RecordExternalPhase(CompilerPhaseNames.ModuleDiscovery, _discoveryTime);
            }
        }

        return new CompilationResult
        {
            Success = projectResult.Success && !registryHasErrors,
            Diagnostics = projectResult.Diagnostics,
            Module = entryUnit?.Ast,
            SymbolTable = model?.GlobalSymbols,
            SemanticInfo = model?.SemanticInfo,
            SemanticBinding = model?.SemanticBinding,
            ModuleRegistry = _moduleRegistry,
            GeneratedCSharpCode = entryUnit?.GeneratedCSharp,
            GeneratedCSharpFiles = generatedFiles,
            Metrics = entryUnit?.Metrics,
            SourceText = new SourceText(sourceCode, originalFilePath),
            Tokens = entryUnit?.Tokens,
            ImportResolver = projectResult.ImportResolver
        };
    }

    /// <summary>
    /// Analyze Sharpy source code through phases 1–3 (Lexer → Parser → Semantic) without codegen.
    /// </summary>
    private CompilationResult AnalyzeInternal(
        string sourceCode, string filePath, CancellationToken cancellationToken,
        bool preserveTrivia = false)
    {
        _logger.LogInfo($"Starting analysis of {filePath}");
        var metrics = new CompilationMetrics(fileName: filePath);

        // Module discovery ran in the constructor (loading referenced assemblies/stdlib),
        // before this metrics object existed — surface it as a first-class phase.
        if (_discoveryTime > TimeSpan.Zero)
        {
            metrics.RecordExternalPhase(CompilerPhaseNames.ModuleDiscovery, _discoveryTime);
        }

        var diagnostics = new DiagnosticBag(_options.WarningsAsErrors, _options.SuppressedWarnings);
        var result = new CompilationResultBuilder(diagnostics, metrics);
        var assertionTimer = new Stopwatch();

        try
        {
            // Phase 1: Lexical Analysis
            metrics.StartPhase(CompilerPhaseNames.LexicalAnalysis);
            LogPhaseStart(CompilerPhaseNames.LexicalAnalysis, filePath);
            var sourceText = new SourceText(sourceCode, filePath);
            result.SourceText = sourceText;
            var lexResult = FileCompilationPipeline.Lex(sourceText, _logger, _options.MaxErrors, cancellationToken, preserveTrivia, _options.Features);
            result.Tokens = lexResult.Tokens;
            metrics.TokenCount = lexResult.Tokens.Count;
            if (preserveTrivia)
                result.CommentSpans = CommentSpanExtractor.Extract(lexResult.Tokens);
            LogPhaseEnd(filePath, lexResult.Diagnostics.ErrorCount);
            metrics.EndPhase();

            if (lexResult.HasErrors)
                return MergeAndFail(diagnostics, lexResult.Diagnostics, metrics, result, CompilerPhase.Lexer);
            cancellationToken.ThrowIfCancellationRequested();

            // Phase 2: Syntax Analysis
            metrics.StartPhase(CompilerPhaseNames.SyntaxAnalysis);
            LogPhaseStart(CompilerPhaseNames.SyntaxAnalysis, filePath, lexResult.Tokens.Count);
            var parserMaxErrors = _options.MaxErrors > 0 ? _options.MaxErrors : 25;
            var parseResult = FileCompilationPipeline.Parse(lexResult.Tokens, _logger, parserMaxErrors, cancellationToken, _options.Features);
            var module = parseResult.Module;
            result.Module = module;
            if (module != null)
            {
                metrics.AstNodeCount = AstValidator.CountNodes(module);
            }
            LogPhaseEnd(filePath, parseResult.Diagnostics.ErrorCount);
            metrics.EndPhase();

            if (parseResult.HasErrors)
                return MergeAndFail(diagnostics, parseResult.Diagnostics, metrics, result, CompilerPhase.Parser);
            MergeWithPhase(diagnostics, parseResult.Diagnostics, CompilerPhase.Parser);

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
            metrics.StartPhase(CompilerPhaseNames.NameResolution);
            LogPhaseStart(CompilerPhaseNames.NameResolution, filePath, module.Body.Length);
            var nameResult = pipeline.ResolveNames(module, cancellationToken);
            LogPhaseEnd(filePath, nameResult.Diagnostics.ErrorCount);
            metrics.EndPhase();
            MergeWithPhase(diagnostics, nameResult.Diagnostics, CompilerPhase.NameResolution);

            cancellationToken.ThrowIfCancellationRequested();

            // Pass 1.5 + 1b: Import resolution + inheritance
            metrics.StartPhase(CompilerPhaseNames.ImportResolution);
            LogPhaseStart(CompilerPhaseNames.ImportResolution, filePath);
            var importResult = pipeline.ResolveImports(
                module, nameResult.NameResolver, filePath, _moduleRegistry, cancellationToken);
            var importResolver = importResult.ImportResolver;
            result.ImportResolver = importResolver;

            RunTimedAssertion(assertionTimer, "Post-name-resolution", () => CompilerInvariants.AssertPostNameResolution(symbolTable, diagnostics));
            RunTimedAssertion(assertionTimer, "Post-inheritance", () => CompilerInvariants.AssertPostInheritance(symbolTable, diagnostics));

            LogPhaseEnd(filePath, importResolver.Diagnostics.ErrorCount);
            metrics.EndPhase();

            if (importResolver.Diagnostics.GetAll().Count > 0)
                MergeWithPhase(diagnostics, importResolver.Diagnostics, CompilerPhase.ImportResolution);

            if (nameResult.HasErrors)
            {
                metrics.SymbolCount = symbolTable.GlobalScope.GetAllSymbols().Count();
                return FailWithDiagnostics(diagnostics, metrics, result);
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Pass 2: Type checking
            metrics.StartPhase(CompilerPhaseNames.TypeChecking);
            LogPhaseStart(CompilerPhaseNames.TypeChecking, filePath);
            var isEntryPoint = _options.OutputType.Equals("exe", StringComparison.OrdinalIgnoreCase);
            // Union compilation-wide features with any per-file `from __future__ import`
            // features discovered during import resolution for this file.
            var fileFeatures = importResolver.GetEffectiveFeatures(_options.Features, filePath);
            // Reject uses of constructs gated behind experimental features that are not enabled,
            // before type resolution runs. No-op until a real gated construct is registered.
            pipeline.CheckFeatureGates(module, filePath, fileFeatures, diagnostics);
            // Advance the main bag's phase high-water mark to TypeChecking for the whole type-check
            // window. The ICE handler reads diagnostics.LastEnteredPhase, and until this scope opens
            // the mark is still NameResolution/ImportResolution — so a crash inside CheckModule would
            // otherwise be mis-attributed. The merge-back scopes below only open after TypeCheck
            // returns, leaving the call itself uncovered (#1083).
            using var typeCheckPhaseScope = diagnostics.BeginPhaseScope(CompilerPhase.TypeChecking);
            var typeCheckResult = pipeline.TypeCheck(
                module, filePath, isEntryPoint, _options.MaxErrors, diagnostics,
                computeCodeGenInfo: true, cancellationToken: cancellationToken,
                moduleRegistry: _moduleRegistry, features: fileFeatures);
            var typeChecker = typeCheckResult.TypeChecker;

            if (typeCheckResult.Aborted)
            {
                LogPhaseEnd(filePath, typeChecker.Diagnostics.ErrorCount);
                metrics.EndPhase();
                metrics.SymbolCount = symbolTable.GlobalScope.GetAllSymbols().Count();
                if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> errorValidatorDict)
                    metrics.SetValidatorTimes(errorValidatorDict);
                metrics.DiagnosticCount = diagnostics.GetAll().Count + typeChecker.Diagnostics.GetAll().Count;
                MergeWithPhase(diagnostics, typeChecker.Diagnostics, CompilerPhase.TypeChecking);
                return result.WithSymbolTable(symbolTable).WithSemanticInfo(semanticInfo).BuildFailure();
            }
            LogPhaseEnd(filePath, typeChecker.Diagnostics.ErrorCount);
            metrics.EndPhase();

            // Type-check imported .spy modules for SemanticInfo population
            pipeline.TypeCheckImportedModules(importResolver, filePath, diagnostics, cancellationToken, _moduleRegistry);

            RunTimedAssertion(assertionTimer, "Post-type-checking", () => CompilerInvariants.AssertPostTypeChecking(semanticInfo, typeChecker.Diagnostics));
            AssertExpressionTypesRecorded(module, semanticInfo, diagnostics);

            RunTimedAssertion(assertionTimer, "Post-materialization", () => pipeline.MaterializeTypeInfo());

            metrics.SymbolCount = symbolTable.GlobalScope.GetAllSymbols().Count();
            if (typeChecker.ValidatorTimes is Dictionary<string, TimeSpan> validatorDict)
                metrics.SetValidatorTimes(validatorDict);
            MergeWithPhase(diagnostics, typeChecker.Diagnostics, CompilerPhase.TypeChecking);

            if (diagnostics.HasErrors)
                return FailWithDiagnostics(diagnostics, metrics, result.WithSymbolTable(symbolTable).WithSemanticInfo(semanticInfo));
            cancellationToken.ThrowIfCancellationRequested();

            metrics.DiagnosticCount = diagnostics.GetAll().Count;
            return result
                .WithSuccess(!diagnostics.HasErrors)
                .WithSymbolTable(symbolTable).WithSemanticInfo(semanticInfo)
                .WithModuleRegistry(_moduleRegistry).Build();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Analysis cancelled");
            diagnostics.AddError("Compilation cancelled", filePath: filePath, code: DiagnosticCodes.Infrastructure.CompilationCancelled);
            return result.BuildFailure();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Compilation failed with {ex.GetType().Name}: {ex}", 0, 0);

            // Last-chance ICE handler (SPY0909): an exception that reaches here is a compiler bug,
            // not a user error. Write a minimal-repro crash bundle and point the diagnostic at it.
            var ice = ex as InternalCompilerErrorException;
            var request = new CrashBundleRequest
            {
                Exception = ex,
                Phase = diagnostics.LastEnteredPhase,
                Producer = diagnostics.LastEnteredProducer,
                Component = ice?.Component,
                SourceFiles = new Dictionary<string, string> { [filePath] = sourceCode },
                Node = ice?.Node,
                Span = ice?.Span,
                SpanFilePath = filePath,
                Line = ice?.Node is { LineStart: > 0 } n ? n.LineStart : null,
                Column = ice?.Node is { ColumnStart: > 0 } c ? c.ColumnStart : null
            };
            var crashDir = File.Exists(filePath) ? Path.GetDirectoryName(filePath) : null;
            var bundlePath = CrashBundleWriter.TryWrite(crashDir, request);

            var component = ice != null ? $" in {ice.Component}" : string.Empty;
            var bundleNote = bundlePath != null
                ? $" A minimal-repro crash bundle was written to: {bundlePath}"
                : string.Empty;
            var errorMessage =
                $"internal compiler error{component} ({ex.GetType().Name}): {ex.Message}. "
                + "This is a Sharpy compiler bug — please report it at https://github.com/antonsynd/sharpy/issues."
                + bundleNote;

            diagnostics.AddError(errorMessage, filePath: filePath, code: DiagnosticCodes.Infrastructure.InternalCompilerError);
            return result.BuildFailure();
        }
    }

    // ----- Helpers -----

    private static CompilationResult MergeAndFail(
        DiagnosticBag target, DiagnosticBag source, CompilationMetrics metrics, CompilationResultBuilder result,
        CompilerPhase phase = CompilerPhase.Unknown)
    {
        MergeWithPhase(target, source, phase);
        metrics.DiagnosticCount = target.GetAll().Count;
        return result.BuildFailure();
    }

    /// <summary>
    /// Merges <paramref name="source"/> into <paramref name="target"/>, back-filling any
    /// still-<see cref="CompilerPhase.Unknown"/> diagnostics with <paramref name="phase"/>.
    /// Diagnostics that already carry an explicit phase (or a validator producer) keep it.
    /// </summary>
    private static void MergeWithPhase(DiagnosticBag target, DiagnosticBag source, CompilerPhase phase)
    {
        if (phase == CompilerPhase.Unknown)
        {
            target.Merge(source);
            return;
        }
        using (target.BeginPhaseScope(phase))
        {
            target.Merge(source);
        }
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
    /// File paths of stdlib/reference assemblies actually used during compilation, as
    /// tracked by the <see cref="Semantic.Registry.ModuleRegistry"/>. Enables selective
    /// runtime-dependency copying for the synthetic project-of-one-file path (#1038).
    /// </summary>
    public IReadOnlySet<string> UsedAssemblyPaths { get; init; } = new HashSet<string>();

    /// <summary>
    /// The effective experimental feature flags for this compilation: the CLI
    /// <c>--enable-feature</c> set unioned with the project's <c>&lt;Features&gt;</c>.
    /// Exposed for tooling and tests that need to observe the merged set.
    /// </summary>
    internal Shared.FeatureFlags EffectiveFeatures { get; init; } = Shared.FeatureFlags.None;

    /// <summary>
    /// The dependency graph built during compilation.
    /// Available for tooling/analysis (e.g., incremental compilation, build order visualization).
    /// </summary>
    internal Project.DependencyGraph? DependencyGraph { get; init; }

    /// <summary>
    /// The import resolver with loaded module information, as of the point compilation
    /// stopped. Null if compilation failed before import resolution (e.g. a lexer error).
    /// Exposed so the synthetic project-of-one-file path (#1038) can reconstitute
    /// <see cref="CompilationResult.ImportResolver"/> for single-file callers.
    /// </summary>
    internal ImportResolver? ImportResolver { get; init; }

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

    /// <summary>
    /// Wrap generated code in a namespace declaration.
    /// Used by <c>emit csharp --namespace</c> for Unity integration.
    /// When null, single-file compilation uses the global namespace.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// The set of enabled experimental feature flags. Sourced from
    /// <c>--enable-feature</c> and <c>&lt;Features&gt;</c> in <c>.spyproj</c>, then
    /// threaded through the compilation phases. Defaults to
    /// <see cref="Shared.FeatureFlags.None"/>.
    /// </summary>
    public Shared.FeatureFlags Features { get; set; } = Shared.FeatureFlags.None;

    /// <summary>
    /// Build configuration ("Debug" or "Release"). Threaded onto the synthetic
    /// project-of-one-file (#1038) so single-file compiles honor the same optimization
    /// level and PDB behavior as project builds. Default: "Debug".
    /// </summary>
    public string Configuration { get; set; } = "Debug";

    /// <summary>
    /// Explicit output assembly name for single-file compilation. When null the entry
    /// file's stem is used. Only consulted when the synthetic project emits an assembly.
    /// </summary>
    public string? AssemblyName { get; set; }

    /// <summary>
    /// Explicit output assembly path for single-file compilation. When set, the synthetic
    /// project-of-one-file directs its emitted assembly here (see
    /// <see cref="ProjectConfig.OutputAssemblyPathOverride"/>). When null, no assembly path
    /// is forced and callers that only need generated C# (e.g. <c>emit csharp</c>) skip
    /// assembly emission entirely.
    /// </summary>
    public string? OutputAssemblyPath { get; set; }
}
