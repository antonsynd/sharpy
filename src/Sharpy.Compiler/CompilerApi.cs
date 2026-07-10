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
        var opts = options ?? new CompilerOptions();
        MergeDefaultReferences(opts);

        // #1038: a single-file compile is a synthetic project-of-one-file driven through
        // ProjectCompiler. This requires the entry to be a real file on disk (the project
        // pipeline reads all source files from disk and walks the local-import closure).
        // Purely in-memory callers (no backing file — e.g. api.Compile(source)) keep the
        // legacy single-file path until it is removed in a later task.
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
        var config = BuildSyntheticProjectConfig(source, options, entryFilePath);
        var registry = BuildModuleRegistry(config);
        var projectCompiler = new Project.ProjectCompiler(
            logger: _logger,
            moduleRegistry: registry,
            warningsAsErrors: options.WarningsAsErrors,
            suppressedWarnings: options.SuppressedWarnings,
            maxErrors: options.MaxErrors,
            incremental: false,
            emitterFactory: _emitterFactory,
            features: options.Features);

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
                if (PathsEqual(unit.FilePath, entryFilePath))
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
    /// Builds the synthetic <see cref="ProjectConfig"/> for a single-file compile per
    /// Design Decision 3 of the Wave 2 plan (#1038): <c>RootNamespace</c> defaults to empty
    /// (preserving single-file global-namespace output), <c>ProjectRootPath</c> is the entry
    /// file's directory (aligning single-file module naming with project mode — the #940 fix),
    /// the entry file is the sole <c>EntryPoint</c>, incremental is off, and <c>SourceFiles</c>
    /// is seeded by walking the local-import closure up front.
    /// </summary>
    private ProjectConfig BuildSyntheticProjectConfig(string source, CompilerOptions options, string entryFilePath)
    {
        var projectDirectory = Path.GetDirectoryName(entryFilePath) ?? Directory.GetCurrentDirectory();
        var sourceFiles = DiscoverLocalImportClosure(source, entryFilePath, options);

        return new ProjectConfig
        {
            ProjectFilePath = entryFilePath,
            ProjectDirectory = projectDirectory,
            // Empty root namespace keeps single-file output in the global namespace; the
            // emit-csharp --namespace override flows through CompilerOptions.Namespace.
            RootNamespace = options.Namespace ?? string.Empty,
            OutputType = options.OutputType,
            TargetFramework = "net10.0",
            AssemblyName = options.AssemblyName,
            EntryPoint = entryFilePath,
            SourceFiles = sourceFiles,
            References = (options.References ?? Array.Empty<string>()).ToList(),
            ModulePaths = (options.ModulePaths ?? Array.Empty<string>()).ToList(),
            Configuration = options.Configuration,
            WarningsAsErrors = options.WarningsAsErrors,
            SuppressedWarnings = new HashSet<string>(options.SuppressedWarnings, StringComparer.OrdinalIgnoreCase),
            OutputAssemblyPathOverride = options.OutputAssemblyPath
        };
    }

    /// <summary>
    /// Walks the transitive closure of local <c>.spy</c> imports starting from the entry file.
    /// Standard-library and CLR imports resolve to no <c>.spy</c> file and are excluded; only
    /// on-disk <c>.spy</c> sources become project source files. The entry file's live
    /// <paramref name="entrySource"/> is used for its own scan; imported files are read from disk.
    /// </summary>
    private List<string> DiscoverLocalImportClosure(string entrySource, string entryFilePath, CompilerOptions options)
    {
        var closure = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolver = new Semantic.ModuleResolver(_logger, options.ModulePaths);

        var entryFull = Path.GetFullPath(entryFilePath);
        visited.Add(entryFull);
        var queue = new Queue<(string Path, string? Source)>();
        queue.Enqueue((entryFull, entrySource));

        while (queue.Count > 0)
        {
            var (file, inlineSource) = queue.Dequeue();
            closure.Add(file);

            string moduleSource;
            if (inlineSource != null)
            {
                moduleSource = inlineSource;
            }
            else
            {
                try { moduleSource = File.ReadAllText(file); }
                catch { continue; }
            }

            var module = ParseModuleForImports(moduleSource, file, options.Features);
            if (module == null)
                continue;

            resolver.SetCurrentModulePath(file);
            foreach (var moduleName in CollectImportModuleNames(module))
            {
                var resolved = resolver.Resolve(moduleName);
                if (resolved == null)
                    continue; // stdlib / CLR / unresolved — not a local source file

                var target = Path.GetFullPath(resolved.FullPath);
                if (visited.Add(target))
                    queue.Enqueue((target, null));
            }
        }

        return closure;
    }

    /// <summary>
    /// Lexes and parses <paramref name="source"/> just far enough to inspect its import
    /// statements. Returns null if lexing/parsing fails; import discovery is best-effort and
    /// real syntax errors surface later in the actual compile.
    /// </summary>
    private Parser.Ast.Module? ParseModuleForImports(string source, string filePath, Shared.FeatureFlags features)
    {
        try
        {
            var sourceText = new SourceText(source, filePath);
            var lexer = new Lexer.Lexer(sourceText, _logger) { Features = features };
            var tokens = lexer.TokenizeAll();
            if (lexer.Diagnostics.HasErrors)
                return null;
            var parser = new Parser.Parser(tokens, _logger, maxErrors: 25, features: features);
            return parser.ParseModule();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Collects the dotted module names referenced by all <c>import</c> and
    /// <c>from … import</c> statements anywhere in the module.
    /// </summary>
    private static IEnumerable<string> CollectImportModuleNames(Parser.Ast.Module module)
    {
        var collector = new ImportNameCollector();
        collector.VisitModule(module);
        return collector.ModuleNames;
    }

    private sealed class ImportNameCollector : Parser.Ast.AstVisitor
    {
        public List<string> ModuleNames { get; } = new();

        public override void VisitImportStatement(Parser.Ast.ImportStatement node)
        {
            foreach (var alias in node.Names)
            {
                if (!string.IsNullOrWhiteSpace(alias.Name))
                    ModuleNames.Add(alias.Name);
            }
            base.VisitImportStatement(node);
        }

        public override void VisitFromImportStatement(Parser.Ast.FromImportStatement node)
        {
            if (!string.IsNullOrWhiteSpace(node.Module))
                ModuleNames.Add(node.Module);
            base.VisitFromImportStatement(node);
        }
    }

    private static bool PathsEqual(string a, string b)
        => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

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
        => Analyze(source, new CompilerOptions { OutputType = "library" }, cancellationToken);

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
    {
        var opts = options ?? new CompilerOptions { OutputType = "library" };
        MergeDefaultReferences(opts);
        var compiler = new Compiler(opts, _logger, _emitterFactory);
        var result = compiler.Analyze(source, "<source>", cancellationToken, preserveTrivia: true);

        return new SemanticResult
        {
            Success = !result.Diagnostics.HasErrors,
            Diagnostics = result.Diagnostics.GetAll(),
            Ast = result.Module,
            SemanticInfo = result.SemanticInfo,
            SymbolTable = result.SymbolTable,
            CommentSpans = result.CommentSpans ?? Array.Empty<CommentSpan>()
        };
    }

    /// <summary>
    /// Analyzes a multi-file Sharpy project (Lexer → Parser → Semantic). No codegen.
    /// Runs phases 1-5 of the project compilation pipeline.
    /// </summary>
    /// <param name="config">The project configuration.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="ProjectAnalysisResult"/> with the analysis outcome.</returns>
    public Project.ProjectAnalysisResult AnalyzeProject(
        ProjectConfig config,
        CancellationToken cancellationToken = default)
    {
        var registry = BuildModuleRegistry(config);
        var compiler = new Project.ProjectCompiler(logger: _logger, moduleRegistry: registry,
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
    /// Merges <see cref="_defaultReferences"/> into the given options.
    /// </summary>
    private void MergeDefaultReferences(CompilerOptions options)
    {
        if (_defaultReferences.Length == 0)
            return;

        var existing = options.References ?? Array.Empty<string>();
        options.References = existing.Concat(_defaultReferences).Distinct().ToArray();
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
            var packageAssemblies = Project.NuGetResolver.ResolvePackage(packageRef, config.TargetFramework, _logger);
            foreach (var assemblyPath in packageAssemblies)
                registry.LoadReference(assemblyPath);
        }

        foreach (var modulePath in config.ModulePaths)
            registry.AddModulePath(modulePath);

        return registry;
    }
}
