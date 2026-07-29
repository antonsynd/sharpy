using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler;

/// <summary>
/// Builds the synthetic in-memory <see cref="ProjectConfig"/> that turns a single-file
/// compile into a project-of-one-file driven through <see cref="Project.ProjectCompiler"/>
/// (#1038). This is the one place single-file inputs are lowered to the unified pipeline;
/// both <see cref="CompilerApi"/> and the lower-level <see cref="Compiler"/> facade delegate
/// here so there is exactly one code path from source to generated C#.
/// </summary>
internal static class SyntheticProject
{
    /// <summary>
    /// Builds the synthetic <see cref="ProjectConfig"/> for a single-file compile per
    /// Design Decision 3 of the Wave 2 plan (#1038): <c>RootNamespace</c> defaults to empty
    /// (preserving single-file global-namespace output), <c>ProjectRootPath</c> is the entry
    /// file's directory (aligning single-file module naming with project mode — the #940 fix),
    /// the entry file is the sole <c>EntryPoint</c>, incremental is off, and <c>SourceFiles</c>
    /// is seeded by walking the local-import closure up front.
    /// </summary>
    /// <param name="source">The live source text of the entry file (used for its import scan).</param>
    /// <param name="entryFilePath">The full, on-disk path of the entry file.</param>
    /// <param name="options">Compiler options supplying namespace, output type, references, etc.</param>
    /// <param name="logger">Logger for the best-effort import scan.</param>
    /// <param name="preserveTrivia">
    /// When true, the parse phase preserves trivia and surfaces per-unit CommentSpans for hover.
    /// Set by the analyze paths (#1087); the compile path leaves it off for byte-identical output.
    /// </param>
    /// <param name="nullifyEntryFilePath">
    /// When true, the entry file is given no path identity (null DeclaringFilePath / reference
    /// paths) so LSP handlers fall back to the request document URI — the historical single-file
    /// analyze contract (#1087). Set by the analyze paths; off for compile.
    /// </param>
    public static ProjectConfig BuildConfig(
        string source, string entryFilePath, CompilerOptions options, ICompilerLogger logger,
        bool preserveTrivia = false, bool nullifyEntryFilePath = false)
    {
        var projectDirectory = Path.GetDirectoryName(entryFilePath);
        if (string.IsNullOrEmpty(projectDirectory))
            projectDirectory = Directory.GetCurrentDirectory();
        var sourceFiles = DiscoverLocalImportClosure(
            source, entryFilePath, options, preserveTrivia, logger, out var preParsedUnits);

        // Feed the entry file's source in-memory keyed by its verbatim path so the compiler
        // uses the caller-supplied text (matching the historical single-file contract) and
        // emits the caller's path in #line directives, without materializing a temp file.
        var inMemory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [entryFilePath] = source
        };

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
            InMemorySources = inMemory,
            References = (options.References ?? Array.Empty<string>()).ToList(),
            ModulePaths = (options.ModulePaths ?? Array.Empty<string>()).ToList(),
            Configuration = options.Configuration,
            WarningsAsErrors = options.WarningsAsErrors,
            SuppressedWarnings = new HashSet<string>(options.SuppressedWarnings, StringComparer.OrdinalIgnoreCase),
            OutputAssemblyPathOverride = options.OutputAssemblyPath,
            PreserveTrivia = preserveTrivia,
            NullifyEntryFilePath = nullifyEntryFilePath,
            PreParsedUnits = preParsedUnits
        };
    }

    /// <summary>
    /// Drives <see cref="ProjectCompiler.AnalyzeProject"/> over a synthetic single-file config
    /// and applies the single-file analyze adaptations shared by
    /// <see cref="Compiler.Analyze(string, string, CancellationToken, bool)"/> and
    /// <see cref="CompilerApi.Analyze(string, CompilerOptions, CancellationToken)"/> (#1087):
    /// the <see cref="ProjectCompiler"/> is constructed from the caller's full options via
    /// <see cref="Services.ProjectOptionsMerge.Merge"/> (WarningsAsErrors, SuppressedWarnings,
    /// MaxErrors, and Features reach the compiler as its options value, not as config fields —
    /// dropping them would silently lose feature flags and SPY0331 gating in analyze
    /// mode), the entry file's unit is located by path, and the shared symbol table is
    /// positioned at the entry file's module scope so bare <c>SymbolTable.Lookup</c> resolves
    /// module-level symbols (the project pipeline scopes them per module rather than in the
    /// global scope). The table is built fresh per call and consumed read-only afterward, so
    /// entering a scope here is safe; it is skipped on parse failure, when no module scope
    /// exists — there are no symbols to find anyway.
    /// </summary>
    public static SyntheticAnalysis Analyze(
        ProjectConfig config, CompilerOptions options, ICompilerLogger logger,
        ModuleRegistry? moduleRegistry, ICodeEmitterFactory? emitterFactory,
        CancellationToken cancellationToken)
    {
        var projectCompiler = new ProjectCompiler(logger, moduleRegistry,
            Services.ProjectOptionsMerge.Merge(options, incremental: false), emitterFactory);

        var analysis = projectCompiler.AnalyzeProject(config, cancellationToken);

        // Pick the entry file's unit out of the project model (the closure may also contain
        // imported local .spy files). Match by path the same way MapProjectResult does.
        // EntryPoint is always set by BuildConfig; the null check satisfies the nullable type.
        CompilationUnit? entryUnit = null;
        if (config.EntryPoint is { } entryPoint)
        {
            foreach (var unit in analysis.ProjectModel.Units.Values)
            {
                if (PathsEqual(unit.FilePath, entryPoint))
                {
                    entryUnit = unit;
                    break;
                }
            }
        }

        var symbolTable = analysis.ProjectModel.GlobalSymbols;
        if (symbolTable != null && entryUnit != null
            && symbolTable.CurrentScope == symbolTable.GlobalScope
            && symbolTable.GetModuleScope(entryUnit.ModulePath) != null)
        {
            symbolTable.EnterModuleScope(entryUnit.ModulePath);
        }

        return new SyntheticAnalysis(analysis, entryUnit);
    }

    /// <summary>
    /// Walks the transitive closure of local <c>.spy</c> imports starting from the entry file.
    /// Standard-library and CLR imports resolve to no <c>.spy</c> file and are excluded; only
    /// on-disk <c>.spy</c> sources become project source files. The entry file's live
    /// <paramref name="entrySource"/> is used for its own scan; imported files are read from disk.
    /// </summary>
    /// <remarks>
    /// Every file the walk visits is lexed and parsed to find its imports. Files whose parse was
    /// pristine (zero lexer/parser diagnostics) are handed forward in <paramref name="preParsed"/>
    /// so <see cref="Project.ProjectCompiler.ParseAllFiles"/> reuses the tokens/AST instead of
    /// lexing and parsing them a second time (#1086). Error-bearing files are deliberately not
    /// cached: discovery swallows their diagnostics, so the parse phase must re-run them to
    /// produce authoritative diagnostics.
    /// </remarks>
    private static List<string> DiscoverLocalImportClosure(
        string entrySource, string entryFilePath, CompilerOptions options, bool preserveTrivia,
        ICompilerLogger logger, out Dictionary<string, PreParsedUnit> preParsed)
    {
        var closure = new List<string>();
        preParsed = new Dictionary<string, PreParsedUnit>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolver = new Semantic.ModuleResolver(logger, options.ModulePaths);

        // Use the entry path verbatim (do not canonicalize) so a virtual path like "test.spy"
        // stays intact for codegen and diagnostics; on-disk callers already pass a full path.
        var entryFull = entryFilePath;
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
                try
                { moduleSource = File.ReadAllText(file); }
                catch { continue; }
            }

            var parse = ParseModuleForImports(moduleSource, file, options.Features, preserveTrivia, logger);
            if (parse.Module == null)
                continue;

            // Retain pristine parses for reuse by ParseAllFiles. The cached source is the exact
            // text these artifacts were built from, so a cache hit compiles that text (no disk
            // TOCTOU); CommentSpans are extracted only when trivia is preserved so they match
            // what the parse phase would surface for the same source.
            if (parse.IsClean)
            {
                var commentSpans = preserveTrivia
                    ? CommentSpanExtractor.Extract(parse.Tokens!)
                    : null;
                preParsed[file] = new PreParsedUnit(moduleSource, parse.Tokens!, parse.Module, commentSpans);
            }

            resolver.SetCurrentModulePath(file);
            foreach (var moduleName in CollectImportModuleNames(parse.Module))
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
    /// The outcome of a best-effort discovery-pass parse: the module (null when lexing failed or
    /// an exception was thrown), the tokens it was built from, and whether the parse was pristine
    /// (zero lexer and parser diagnostics) and therefore eligible to be handed to ParseAllFiles.
    /// </summary>
    private readonly record struct DiscoveryParse(
        Parser.Ast.Module? Module, List<Lexer.Token>? Tokens, bool IsClean);

    /// <summary>
    /// Lexes and parses <paramref name="source"/> to inspect its import statements. Returns a
    /// null <see cref="DiscoveryParse.Module"/> if lexing errors or an exception aborts the parse;
    /// import discovery is best-effort and real syntax errors surface later in the actual compile.
    /// <see cref="DiscoveryParse.IsClean"/> is true only when neither the lexer nor the parser
    /// recorded any diagnostic — the gate for reusing the artifacts (#1086). Lexing honors
    /// <paramref name="preserveTrivia"/> so reused tokens/CommentSpans match the parse phase.
    /// </summary>
    private static DiscoveryParse ParseModuleForImports(
        string source, string filePath, Shared.FeatureFlags features, bool preserveTrivia,
        ICompilerLogger logger)
    {
        try
        {
            var sourceText = new SourceText(source, filePath);
            var lexer = new Lexer.Lexer(sourceText, logger, preserveTrivia: preserveTrivia) { Features = features };
            var tokens = lexer.TokenizeAll();
            if (lexer.Diagnostics.HasErrors)
                return default;
            var parser = new Parser.Parser(tokens, logger, maxErrors: 25, features: features);
            var module = parser.ParseModule();
            var isClean = lexer.Diagnostics.GetAll().Count == 0 && parser.Diagnostics.GetAll().Count == 0;
            return new DiscoveryParse(module, tokens, isClean);
        }
        catch
        {
            return default;
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

    /// <summary>
    /// True if two file paths refer to the same file (case-insensitive full-path compare).
    /// </summary>
    public static bool PathsEqual(string a, string b)
        => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Result of <see cref="SyntheticProject.Analyze"/>: the whole-project analysis plus the entry
/// file's compilation unit (null when the entry never made it into the project model). The
/// analysis' shared symbol table is already positioned at the entry file's module scope.
/// </summary>
internal readonly record struct SyntheticAnalysis(
    ProjectAnalysisResult Analysis, CompilationUnit? EntryUnit);
