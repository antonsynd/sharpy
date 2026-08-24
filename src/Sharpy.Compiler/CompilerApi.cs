using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler;

/// <summary>
/// Clean, documented entry point for programmatic compilation.
/// Provides a stable interface for tooling consumers (LSP, IDE plugins, build systems)
/// without depending on CLI internals.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps the internal <see cref="Compiler"/> pipeline and exposes
/// structured result types that are easy to consume programmatically.
/// All methods accept <see cref="CancellationToken"/> for cooperative cancellation.
/// </para>
/// <para>
/// Three levels of analysis are available:
/// <list type="bullet">
///   <item><see cref="Parse"/> — Lexer + Parser only (syntax information)</item>
///   <item><see cref="Analyze"/> — Lexer + Parser + Semantic (type information, no codegen)</item>
///   <item><see cref="Compile(string, CompilerOptions?, string?, CancellationToken)"/> — Full pipeline including C# code generation</item>
/// </list>
/// </para>
/// </remarks>
public sealed class CompilerApi
{
    private readonly ICompilerLogger _logger;
    private readonly ICodeEmitterFactory _emitterFactory;
    private readonly AstPositionService _positionService = new();
    private readonly string[] _defaultReferences;

    private readonly object _analysisCacheLock = new();
    private AnalysisCacheEntry? _analysisCache;

    /// <summary>
    /// Creates a new CompilerApi with default settings.
    /// </summary>
    public CompilerApi() : this(null, null, null) { }

    /// <summary>
    /// Creates a new CompilerApi with a custom logger.
    /// </summary>
    /// <param name="logger">Optional compiler logger. Uses NullLogger if null.</param>
    public CompilerApi(ICompilerLogger? logger) : this(logger, null, null) { }

    /// <summary>
    /// Creates a new CompilerApi with a custom logger and default assembly references.
    /// Default references (e.g., Sharpy.Core.dll) are automatically included in every
    /// analysis and compilation, enabling stdlib module resolution.
    /// </summary>
    /// <param name="logger">Optional compiler logger. Uses NullLogger if null.</param>
    /// <param name="defaultReferences">
    /// Paths to .NET assemblies that should be loaded for every analysis.
    /// Typically includes Sharpy.Core.dll for stdlib support.
    /// </param>
    public CompilerApi(ICompilerLogger? logger, string[]? defaultReferences)
        : this(logger, defaultReferences, null) { }

    internal CompilerApi(ICompilerLogger? logger, string[]? defaultReferences,
        ICodeEmitterFactory? emitterFactory)
    {
        _logger = logger ?? NullLogger.Instance;
        _emitterFactory = emitterFactory ?? new RoslynEmitterFactory();
        _defaultReferences = defaultReferences ?? Array.Empty<string>();
    }

    /// <summary>
    /// Compiles Sharpy source code through the full pipeline (Lexer → Parser → Semantic → CodeGen).
    /// </summary>
    /// <param name="source">The Sharpy source code to compile.</param>
    /// <param name="options">Optional compiler options. Uses sensible defaults if null.</param>
    /// <param name="filePath">Optional file path for diagnostics. Defaults to "&lt;source&gt;".</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="CompileResult"/> with the compilation outcome.</returns>
    public CompileResult Compile(
        string source,
        CompilerOptions? options = null,
        string? filePath = null,
        CancellationToken cancellationToken = default)
    {
        var opts = WithMergedReferences(options ?? CompilerOptionsFactory.Default());

        // #1038: a single-file compile is a synthetic project-of-one-file driven through
        // ProjectCompiler. When the entry is a real file on disk, the project pipeline
        // reads all source files from disk and walks the local-import closure here.
        // Purely in-memory callers (no backing file — e.g. api.Compile(source)) go through
        // Compiler.Compile, which reaches the same ProjectCompiler pipeline via
        // SyntheticProject.BuildConfig + in-memory sources — both branches share one path.
        if (filePath != null && File.Exists(filePath))
        {
            return CompileAsSyntheticProject(source, opts, Path.GetFullPath(filePath), cancellationToken);
        }

        var resolvedPath = filePath ?? "<source>";
        var compiler = new Compiler(opts, _logger, _emitterFactory);

        var result = compiler.Compile(source, resolvedPath, cancellationToken);

        return new CompileResult
        {
            Success = result.Success,
            Diagnostics = result.Diagnostics.GetAll(),
            GeneratedCSharp = result.GeneratedCSharpCode,
            GeneratedCSharpFiles = new Dictionary<string, string>(result.GeneratedCSharpFiles),
            Ast = result.Module,
            SemanticInfo = result.SemanticInfo,
            Metrics = result.Metrics,
            UsedAssemblyPaths = result.ModuleRegistry?.GetUsedAssemblyPaths()
                ?? (IReadOnlySet<string>)new HashSet<string>()
        };
    }

    /// <summary>
    /// Compiles a single entry file as a synthetic in-memory project-of-one-file (#1038):
    /// the entry file plus the transitive closure of its local <c>.spy</c> imports become a
    /// <see cref="ProjectConfig"/> fed to <see cref="Project.ProjectCompiler"/>. This is the
    /// one code path from source to generated C#; the CLI emits the assembly itself using the
    /// same <see cref="ProjectConfig"/> (returned on <see cref="CompileResult.ProjectConfig"/>).
    /// </summary>
    private CompileResult CompileAsSyntheticProject(
        string source, CompilerOptions options, string entryFilePath, CancellationToken cancellationToken)
    {
        var config = SyntheticProject.BuildConfig(source, entryFilePath, options, _logger);
        var registry = BuildModuleRegistry(config);
        // The synthetic config was built from these very options, so it has nothing independent to
        // merge in; incremental is off because single-file compiles never use the project cache.
        var projectCompiler = new Project.ProjectCompiler(
            logger: _logger,
            moduleRegistry: registry,
            options: ProjectOptionsMerge.Merge(options, incremental: false),
            emitterFactory: _emitterFactory);

        // Emit the assembly inside the project pipeline only when a concrete output path was
        // requested; otherwise stop after codegen so emit/analysis callers write nothing.
        var emitAssembly = !string.IsNullOrEmpty(options.OutputAssemblyPath);
        var result = projectCompiler.Compile(config, cancellationToken, emitAssembly);

        // Re-key the generated C# by source path (the CompileResult contract) and pick out the
        // entry file's C# from the project model.
        var generatedBySource = new Dictionary<string, string>();
        string? entryCSharp = null;
        Parser.Ast.Module? entryAst = null;
        CompilationMetrics? entryMetrics = null;
        var model = result.ProjectModel;
        if (model != null)
        {
            foreach (var unit in model.Units.Values)
            {
                if (unit.GeneratedCSharp != null)
                    generatedBySource[unit.FilePath] = unit.GeneratedCSharp;
                if (SyntheticProject.PathsEqual(unit.FilePath, entryFilePath))
                {
                    entryCSharp = unit.GeneratedCSharp;
                    entryAst = unit.Ast;
                    entryMetrics = unit.Metrics;
                }
            }
        }

        return new CompileResult
        {
            Success = result.Success,
            Diagnostics = result.Diagnostics.GetAll(),
            GeneratedCSharp = entryCSharp,
            GeneratedCSharpFiles = generatedBySource,
            Ast = entryAst,
            SemanticInfo = model?.SemanticInfo,
            Metrics = entryMetrics,
            UsedAssemblyPaths = result.UsedAssemblyPaths,
            ProjectConfig = config,
            OutputAssemblyPath = result.OutputAssemblyPath,
            ProjectMetrics = result.Metrics
        };
    }

    /// <summary>
    /// Compiles a Sharpy source file through the full pipeline.
    /// </summary>
    /// <param name="filePath">Path to the Sharpy source file.</param>
    /// <param name="options">Optional compiler options.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="CompileResult"/> with the compilation outcome.</returns>
    /// <exception cref="FileNotFoundException">If the source file does not exist.</exception>
    public CompileResult CompileFile(
        string filePath,
        CompilerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Source file not found: {filePath}", filePath);

        var source = File.ReadAllText(filePath);
        return Compile(source, options, filePath, cancellationToken);
    }

    /// <summary>
    /// Parses Sharpy source code (Lexer → Parser only).
    /// Returns the AST and any lexer/parser diagnostics. No semantic analysis or codegen.
    /// Useful for tooling that only needs syntax information (e.g., syntax highlighting, formatting).
    /// </summary>
    /// <param name="source">The Sharpy source code to parse.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="ParseResult"/> with the parse outcome.</returns>
    public ParseResult Parse(string source, CancellationToken cancellationToken = default)
    {
        var diagnostics = new DiagnosticBag();

        try
        {
            var sourceText = new SourceText(source, "<source>");

            // Phase 1: Lexical Analysis
            var lexer = new Lexer.Lexer(sourceText, _logger, cancellationToken: cancellationToken);
            var tokens = lexer.TokenizeAll();

            if (lexer.Diagnostics.HasErrors)
            {
                diagnostics.Merge(lexer.Diagnostics);
                return new ParseResult
                {
                    Success = false,
                    Diagnostics = diagnostics.GetAll()
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Phase 2: Syntax Analysis
            var parser = new Parser.Parser(tokens, _logger, maxErrors: 25, cancellationToken);
            var module = parser.ParseModule();

            diagnostics.Merge(lexer.Diagnostics);
            diagnostics.Merge(parser.Diagnostics);

            return new ParseResult
            {
                Success = !diagnostics.HasErrors,
                Diagnostics = diagnostics.GetAll(),
                Ast = module
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            diagnostics.AddError($"Parse failed: {ex.Message}");
            return new ParseResult
            {
                Success = false,
                Diagnostics = diagnostics.GetAll()
            };
        }
    }

    /// <summary>
    /// Analyzes Sharpy source code (Lexer → Parser → Semantic). No codegen.
    /// Returns the AST, semantic info, and symbol table.
    /// Useful for LSP hover, completion, and go-to-definition.
    /// </summary>
    /// <param name="source">The Sharpy source code to analyze.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="SemanticResult"/> with the analysis outcome.</returns>
    public SemanticResult Analyze(string source, CancellationToken cancellationToken = default)
        => Analyze(source, CompilerOptionsFactory.ForLibraryAnalysis(), cancellationToken);

    /// <summary>
    /// Analyzes Sharpy source code with explicit compiler options (e.g. to supply
    /// <see cref="CompilerOptions.Features"/>). This is the seam through which the LSP
    /// passes workspace-level options into analysis.
    /// </summary>
    /// <param name="source">The Sharpy source code to analyze.</param>
    /// <param name="options">Compiler options for this analysis.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="SemanticResult"/> with the analysis outcome.</returns>
    public SemanticResult Analyze(string source, CompilerOptions options, CancellationToken cancellationToken = default)
        => Analyze(source, options, stageMetrics: null, cancellationToken);

    /// <summary>
    /// Analyzes source with explicit options while attributing the call's wall time to the
    /// per-call pipeline stages it runs through (see <see cref="AnalysisStageNames"/>).
    /// </summary>
    /// <remarks>
    /// The attribution seam for the LSP change→publish latency baseline (#1140). Since #1087 a
    /// single-file analyze is a synthetic project driven through <c>ProjectCompiler</c>, so each
    /// call rebuilds whole-project scaffolding — a module registry, a project model, a shared
    /// symbol table — that the recorded baseline reports only as one aggregate number. Passing a
    /// <see cref="CompilationMetrics"/> here breaks that number apart.
    /// <para>
    /// Attribution is opt-in and this path runs per keystroke, so <paramref name="stageMetrics"/>
    /// must be null unless someone is reading the result: with null, every bracket collapses to a
    /// null check and nothing is allocated. <paramref name="cancellationToken"/> deliberately has
    /// no default value — giving it one would make <c>Analyze(source, options)</c> ambiguous
    /// against the overload above.
    /// </para>
    /// </remarks>
    /// <param name="source">The Sharpy source code to analyze.</param>
    /// <param name="options">Compiler options for this analysis.</param>
    /// <param name="stageMetrics">
    /// Collects a phase per pipeline stage, or null to run uninstrumented.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="SemanticResult"/> with the analysis outcome.</returns>
    public SemanticResult Analyze(string source, CompilerOptions options,
        CompilationMetrics? stageMetrics, CancellationToken cancellationToken)
    {
        // Single-file analyze is a synthetic project-of-one-file driven through the same
        // ProjectCompiler that compile uses (#1087) — no parallel phase sequencer. The entry
        // source is fed in-memory under a virtual path (a caller with no file at all);
        // preserveTrivia surfaces per-unit CommentSpans for hover, and nullifyEntryFilePath
        // records the entry file's symbols/references with a null path (so LSP handlers treat
        // them as the current document, the historical single-file contract). OutputType flows
        // from the caller's options (the no-options overload keeps its "library" default so LSP
        // analysis gains no entry-point/missing-main diagnostics).
        //
        // An editor buffer that DOES have a path wants AnalyzeDocument below: it keeps the
        // symbol-nullification half of this contract while naming the file (#1433).
        const string entryPath = "<source>";
        return AnalyzeCore(source, entryPath, nullifyEntryFilePath: true, options, stageMetrics,
            cancellationToken);
    }

    /// <summary>
    /// Analyzes an editor buffer that has a path: the document names its own file while its
    /// symbols keep the null-path identity LSP handlers depend on (#1433, #1087).
    /// </summary>
    /// <remarks>
    /// The two things a path controls are independent, and pinning the entry to
    /// <c>"&lt;source&gt;"</c> conflated them:
    /// <list type="bullet">
    ///   <item><b>What is this file called?</b> Feeds the module-class name derivation, the import
    ///   closure's root directory, and every diagnostic whose very existence depends on the file's
    ///   identity — SPY0523 (a module-level <c>def foo</c> in <c>foo.spy</c> colliding with the
    ///   generated module class) and SPY0302 (a module importing itself). With no name, neither
    ///   diagnostic is producible at all, so the editor showed no squiggle for code that the build
    ///   then refused (#1433).</item>
    ///   <item><b>Whose symbols are these?</b> The entry file's symbols and per-node reference
    ///   locations are recorded with a null path so handlers (rename, highlight, type hierarchy)
    ///   fall back to the request document URI — the historical single-file contract (#1087).</item>
    /// </list>
    /// Before this overload the two axes were coupled to overload choice: every path-less shape
    /// nullified, the one path-carrying shape did not. An editor needs the first axis without the
    /// second, which is what this provides. <c>Analyze(source, entryFilePath, ...)</c> remains the
    /// shape for a genuine on-disk entry outside an editor, where nullifying would be meaningless.
    /// </remarks>
    /// <param name="source">
    /// The buffer's text, fed in-memory under <paramref name="documentFilePath"/> so an unsaved
    /// edit analyzes as typed while its siblings are read from disk.
    /// </param>
    /// <param name="documentFilePath">
    /// The document's local path. Null or empty (an untitled buffer, which genuinely has no name)
    /// falls back to the path-less contract above.
    /// </param>
    /// <param name="options">Compiler options for this analysis.</param>
    /// <param name="stageMetrics">Collects a phase per pipeline stage, or null to run uninstrumented.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="SemanticResult"/> with the analysis outcome.</returns>
    public SemanticResult AnalyzeDocument(string source, string? documentFilePath,
        CompilerOptions options, CompilationMetrics? stageMetrics, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(documentFilePath))
            return Analyze(source, options, stageMetrics, cancellationToken);

        // Same resolution the compile path applies: a real file is canonicalized so the closure
        // walk and the project model agree on identity; anything else is used verbatim.
        var resolved = File.Exists(documentFilePath) ? Path.GetFullPath(documentFilePath) : documentFilePath;
        return AnalyzeCore(source, resolved, nullifyEntryFilePath: true, options, stageMetrics,
            cancellationToken);
    }

    /// <summary>
    /// Analyzes an entry file that exists on disk, discovering and resolving its sibling imports
    /// from the file's own directory exactly as <c>run</c> and <c>project</c> do (#1377).
    /// </summary>
    /// <remarks>
    /// The path-less overloads above pin the entry to the virtual <c>"&lt;source&gt;"</c>, which has
    /// no directory, so <c>SyntheticProject.BuildConfig</c> rooted the import closure at the process
    /// working directory and a sibling <c>import lib</c> could not resolve. The analysis then
    /// described a DIFFERENT program from the one <c>run</c> compiles — SPY0300 plus whatever
    /// downstream diagnostics the unresolved names produced — which is the #1144 front-end-parity
    /// class. Supplying the real path is the whole fix; the closure walk it feeds already existed.
    /// <para>
    /// Unlike <see cref="AnalyzeDocument"/> this does NOT nullify the entry file's path identity:
    /// an on-disk entry outside an editor has a genuine path, and stripping it would attribute its
    /// symbols to "the current document" — meaningless when there is no document. An editor buffer
    /// that has a path wants <see cref="AnalyzeDocument"/>, which names the file without giving up
    /// the #1087 symbol contract.
    /// </para>
    /// </remarks>
    /// <param name="source">
    /// The entry file's source text. Fed in-memory under <paramref name="entryFilePath"/>, so an
    /// unsaved editor buffer analyzes as typed while its siblings are read from disk.
    /// </param>
    /// <param name="entryFilePath">
    /// The entry file's path. Null or empty falls back to the path-less contract above.
    /// </param>
    /// <param name="options">Compiler options for this analysis.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="SemanticResult"/> with the analysis outcome.</returns>
    public SemanticResult Analyze(string source, string? entryFilePath, CompilerOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(entryFilePath))
            return Analyze(source, options, stageMetrics: null, cancellationToken);

        // Same resolution the compile path applies (see Compile): a real file is canonicalized so
        // the closure walk and the project model agree on identity; anything else is used verbatim
        // so a virtual path survives into diagnostics unchanged.
        var resolved = File.Exists(entryFilePath) ? Path.GetFullPath(entryFilePath) : entryFilePath;
        return AnalyzeCore(source, resolved, nullifyEntryFilePath: false, options,
            stageMetrics: null, cancellationToken);
    }

    /// <summary>
    /// The one analyze body. Both public shapes differ only in the entry path they name and
    /// whether that entry keeps its path identity.
    /// </summary>
    private SemanticResult AnalyzeCore(string source, string entryPath, bool nullifyEntryFilePath,
        CompilerOptions options, CompilationMetrics? stageMetrics, CancellationToken cancellationToken)
    {
        var opts = WithMergedReferences(options ?? CompilerOptionsFactory.ForLibraryAnalysis());

        ProjectConfig config;
        using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.SyntheticProjectSetup))
        {
            config = SyntheticProject.BuildConfig(source, entryPath, opts, _logger,
                preserveTrivia: true, nullifyEntryFilePath: nullifyEntryFilePath);
        }

        ModuleRegistry? registry;
        BuiltinRegistry builtins;
        using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.ModuleRegistry))
        {
            (registry, builtins) = GetOrBuildAnalysisContext(config);
        }

        var result = SyntheticProject.Analyze(config, opts, _logger, registry,
            _emitterFactory, cancellationToken, stageMetrics, builtins);
        var analysis = result.Analysis;
        Project.FileAnalysisResult? entryResult;
        using (MetricsStage.Begin(stageMetrics, AnalysisStageNames.EntryFileResult))
        {
            entryResult = result.EntryUnit != null
                ? analysis.GetFileResult(result.EntryUnit.FilePath)
                : null;
        }

        return new SemanticResult
        {
            // Diagnostics and success come from the whole analysis (project bag), matching the
            // former single-file driver and the compile path; the AST/semantic/symbol/comment
            // artifacts are the entry file's per-unit view.
            Success = !analysis.Diagnostics.HasErrors,
            Diagnostics = analysis.Diagnostics.GetAll(),
            Ast = entryResult?.Ast,
            SemanticInfo = entryResult?.SemanticInfo,
            // Already positioned at the entry file's module scope by SyntheticProject.Analyze.
            SymbolTable = entryResult?.SymbolTable,
            CommentSpans = entryResult?.CommentSpans ?? Array.Empty<CommentSpan>()
        };
    }

    /// <summary>
    /// Analyzes a multi-file Sharpy project (Lexer → Parser → Semantic). No codegen.
    /// Runs phases 1-5 of the project compilation pipeline.
    /// </summary>
    /// <param name="config">The project configuration.</param>
    /// <param name="options">
    /// Optional compiler options. When null a fresh <see cref="CompilerOptions"/> is used, so the
    /// project's own <c>&lt;Features&gt;</c>, <c>&lt;WarningsAsErrors&gt;</c>, and <c>&lt;NoWarn&gt;</c>
    /// still apply via the shared merge. Threading real options future-proofs <c>MaxErrors</c> and
    /// CLI-supplied feature/warning parity for <c>.spyproj</c> analysis (#1109).
    /// </param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="ProjectAnalysisResult"/> with the analysis outcome.</returns>
    public Project.ProjectAnalysisResult AnalyzeProject(
        ProjectConfig config,
        CompilerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? CompilerOptionsFactory.Default();

        var (registry, builtins) = GetOrBuildAnalysisContext(config);
        var compiler = new Project.ProjectCompiler(
            logger: _logger,
            moduleRegistry: registry,
            options: ProjectOptionsMerge.Merge(opts, config, incremental: false, sharedBuiltins: builtins),
            emitterFactory: _emitterFactory);
        return compiler.AnalyzeProject(config, cancellationToken);
    }

    /// <summary>
    /// Finds the innermost AST node at the given source position.
    /// Delegates to <see cref="AstPositionService.FindInnermostNode"/>.
    /// </summary>
    /// <param name="module">The parsed module to search.</param>
    /// <param name="line">1-based line number (matching LSP conventions).</param>
    /// <param name="column">1-based column number (matching LSP conventions).</param>
    /// <returns>The innermost node at the position, or null if outside all nodes.</returns>
    public Node? FindNodeAtPosition(Module module, int line, int column)
    {
        return _positionService.FindInnermostNode(module, line, column);
    }

    /// <summary>
    /// Finds the innermost AST node of a specific type at the given source position.
    /// Delegates to <see cref="AstPositionService.FindNodeOfType{T}"/>.
    /// </summary>
    /// <typeparam name="T">The AST node type to find.</typeparam>
    /// <param name="module">The parsed module to search.</param>
    /// <param name="line">1-based line number (matching LSP conventions).</param>
    /// <param name="column">1-based column number (matching LSP conventions).</param>
    /// <returns>The innermost node of type T at the position, or null if not found.</returns>
    public T? FindNodeOfType<T>(Module module, int line, int column) where T : Node
    {
        return _positionService.FindNodeOfType<T>(module, line, column);
    }

    /// <summary>
    /// Formats a diagnostic for display, with optional source context and underlines.
    /// Delegates to <see cref="DiagnosticRenderer"/>.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to format.</param>
    /// <param name="source">Optional source code for context rendering (enables ^^^ underlines).</param>
    /// <returns>A formatted string suitable for display.</returns>
    public string FormatDiagnostic(CompilerDiagnostic diagnostic, string? source = null)
    {
        var renderer = new DiagnosticRenderer(useColor: false);
        SourceText? sourceText = source != null
            ? new SourceText(source, diagnostic.FilePath ?? "<source>")
            : null;
        return renderer.Render(diagnostic, sourceText);
    }

    /// <summary>
    /// Returns <paramref name="options"/> with <see cref="_defaultReferences"/> folded into
    /// <see cref="CompilerOptions.References"/>. Never mutates the input — the LSP's shared
    /// <c>_workspaceOptions</c> stays pristine so <c>SameAnalysisInputs</c> compares like
    /// against like (#1140 H8).
    /// </summary>
    private CompilerOptions WithMergedReferences(CompilerOptions options)
    {
        if (_defaultReferences.Length == 0)
            return options;

        var existing = options.References ?? Array.Empty<string>();
        return options.WithReferences(existing.Concat(_defaultReferences).Distinct().ToArray());
    }

    /// <summary>
    /// Builds a <see cref="ModuleRegistry"/> from default references and project config.
    /// Returns null if no references or module paths are configured.
    /// </summary>
    private ModuleRegistry? BuildModuleRegistry(ProjectConfig config)
    {
        if (_defaultReferences.Length == 0 && config.References.Count == 0
            && config.ModulePaths.Count == 0 && config.PackageReferences.Count == 0)
            return null;

        var registry = new ModuleRegistry(_logger);

        foreach (var reference in _defaultReferences)
            registry.LoadReference(reference);

        foreach (var reference in config.References)
            registry.LoadReference(reference);

        foreach (var packageRef in config.PackageReferences)
        {
            var packageAssemblies = Project.NuGetResolver.ResolvePackage(
                packageRef, config.TargetFramework, _logger, config.ResolvedVersions);
            foreach (var assemblyPath in packageAssemblies)
                registry.LoadReference(assemblyPath);
        }

        foreach (var modulePath in config.ModulePaths)
            registry.AddModulePath(modulePath);

        return registry;
    }

    /// <summary>
    /// Returns a cached <see cref="AnalysisCacheEntry"/> when the reference, module-path, and
    /// package-reference set is unchanged (path + mtime identity), or builds a fresh one and
    /// caches it. The compile path bypasses this entirely — H5/H6/H9 make per-call fresh
    /// registries essential there. Sharing the registry across analyses is safe for inheritance
    /// materialization because its TypeSymbols are constructed fully resolved — never with
    /// deferred base/interface names — so per-analysis freezes write nothing onto them
    /// (see <c>SemanticBinding.MaterializeInheritance</c>, #1140 H3).
    /// </summary>
    internal (ModuleRegistry? Registry, BuiltinRegistry Builtins) GetOrBuildAnalysisContext(
        ProjectConfig config)
    {
        var key = BuildAnalysisCacheKey(config);

        lock (_analysisCacheLock)
        {
            if (_analysisCache != null && _analysisCache.Key.SequenceEqual(key))
                return (_analysisCache.Registry, _analysisCache.Builtins);
        }

        var builtins = new BuiltinRegistry(_logger);
        var registry = BuildModuleRegistry(config);

        var entry = new AnalysisCacheEntry(key, registry, builtins);
        lock (_analysisCacheLock)
        {
            _analysisCache = entry;
        }

        return (registry, builtins);
    }

    internal IReadOnlyList<(string Path, long Ticks)> BuildAnalysisCacheKey(ProjectConfig config)
    {
        var parts = new List<(string Path, long Ticks)>();

        foreach (var r in _defaultReferences)
            parts.Add((r, GetMtimeTicks(r)));

        foreach (var r in config.References)
            parts.Add((r, GetMtimeTicks(r)));

        foreach (var m in config.ModulePaths)
            parts.Add((m, GetMtimeTicks(m)));

        // BuildModuleRegistry also loads the assemblies resolved from PackageReferences, so the
        // key must cover those same inputs: the declared package identity (which includes the
        // target framework the resolution depends on, and distinguishes configs whose packages
        // resolve to no assemblies) plus each resolved assembly with its mtime, so a re-restored
        // package invalidates. Resolution is a local ~/.nuget directory scan — no network. The
        // logger is withheld here so resolution warnings are reported once by the actual registry
        // build, not on every cache probe.
        foreach (var packageRef in config.PackageReferences)
        {
            parts.Add(($"nuget:{packageRef.Name}/{packageRef.Version}@{config.TargetFramework}", 0));
            foreach (var assemblyPath in Project.NuGetResolver.ResolvePackage(
                packageRef, config.TargetFramework, logger: null, config.ResolvedVersions))
            {
                parts.Add((assemblyPath, GetMtimeTicks(assemblyPath)));
            }
        }

        return parts;

        static long GetMtimeTicks(string path)
        {
            try
            { return File.GetLastWriteTimeUtc(path).Ticks; }
            catch { return 0; }
        }
    }

    private sealed class AnalysisCacheEntry
    {
        public IReadOnlyList<(string Path, long Ticks)> Key { get; }
        public ModuleRegistry? Registry { get; }
        public BuiltinRegistry Builtins { get; }

        public AnalysisCacheEntry(
            IReadOnlyList<(string Path, long Ticks)> key,
            ModuleRegistry? registry,
            BuiltinRegistry builtins)
        {
            Key = key;
            Registry = registry;
            Builtins = builtins;
        }
    }
}
