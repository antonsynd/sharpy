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
        // Merge project-level and compiler-level warning/error/feature settings. This is the
        // single shared definition of the merge (also used by CompilerApi.AnalyzeProject), so the
        // compile and analyze paths can never diverge (#1109). MaxErrors comes from options only.
        var merged = ProjectOptionsMerge.Merge(_options, projectConfig);

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
            merged.WarningsAsErrors, merged.SuppressedWarnings, _options.MaxErrors, _options.Incremental,
            _emitterFactory, merged.Features);
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
    /// Analyze Sharpy source code through semantic analysis (no codegen).
    /// Returns the same <see cref="CompilationResult"/> shape but with no generated C#.
    /// </summary>
    public CompilationResult Analyze(string sourceCode, string filePath) =>
        Analyze(sourceCode, filePath, CancellationToken.None);

    /// <summary>
    /// Analyze Sharpy source code through semantic analysis (no codegen). Like
    /// <see cref="Compile(string, string, CancellationToken)"/>, this lowers the entry file plus
    /// its local-import closure to a synthetic project-of-one-file and drives
    /// <see cref="ProjectCompiler.AnalyzeProject"/> (#1087) — there is no separate analyze
    /// sequencer. The entry file is given no path identity (matching the historical single-file
    /// analyze contract) so tooling that reads reference/declaration paths falls back to the
    /// caller's document.
    /// </summary>
    public CompilationResult Analyze(string sourceCode, string filePath, CancellationToken cancellationToken,
        bool preserveTrivia = false)
    {
        _logger.LogInfo($"Starting analysis of {filePath}");

        var entryFilePath = File.Exists(filePath) ? Path.GetFullPath(filePath) : filePath;

        var config = SyntheticProject.BuildConfig(sourceCode, entryFilePath, _options, _logger,
            preserveTrivia: preserveTrivia, nullifyEntryFilePath: true);
        var result = SyntheticProject.Analyze(config, _options, _logger, _moduleRegistry,
            _emitterFactory, cancellationToken);

        // Reference-load failures are recorded on the shared module registry in the constructor,
        // before the pipeline runs; surface them so analyze callers see the same errors compile does.
        if (_moduleRegistry != null && _moduleRegistry.Diagnostics.HasErrors)
            result.Analysis.Diagnostics.Merge(_moduleRegistry.Diagnostics);

        return MapAnalysisResult(result, filePath, sourceCode, preserveTrivia);
    }

    /// <summary>
    /// Reconstitutes a single-file <see cref="CompilationResult"/> from the unified project
    /// analyze pipeline's per-unit artifacts (parse through type checking, no codegen), without
    /// re-running any analysis. Mirrors <see cref="MapProjectResult"/> for the analyze path.
    /// </summary>
    private CompilationResult MapAnalysisResult(
        SyntheticAnalysis result, string originalFilePath, string sourceCode, bool preserveTrivia)
    {
        var analysis = result.Analysis;
        var model = analysis.ProjectModel;
        var entryUnit = result.EntryUnit;

        // Reference/stdlib discovery ran in the constructor before any per-file metrics existed;
        // record it as a first-class phase on the entry file's metrics (parity with MapProjectResult).
        if (entryUnit?.Metrics != null && _discoveryTime > TimeSpan.Zero
            && !entryUnit.Metrics.Phases.Any(p => p.Name == CompilerPhaseNames.ModuleDiscovery))
        {
            entryUnit.Metrics.RecordExternalPhase(CompilerPhaseNames.ModuleDiscovery, _discoveryTime);
        }

        return new CompilationResult
        {
            Success = !analysis.Diagnostics.HasErrors,
            Diagnostics = analysis.Diagnostics,
            Module = entryUnit?.Ast,
            // The shared table was already positioned at the entry file's module scope by
            // SyntheticProject.Analyze.
            SymbolTable = model.GlobalSymbols,
            SemanticInfo = entryUnit?.FileSemanticInfo ?? model.SemanticInfo,
            SemanticBinding = model.SemanticBinding,
            ModuleRegistry = _moduleRegistry,
            GeneratedCSharpCode = null,
            Metrics = entryUnit?.Metrics,
            SourceText = new SourceText(sourceCode, originalFilePath),
            Tokens = entryUnit?.Tokens,
            CommentSpans = preserveTrivia ? entryUnit?.CommentSpans : null
        };
    }

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
