using Sharpy.Compiler.Logging;
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
        var sourceFiles = DiscoverLocalImportClosure(source, entryFilePath, options, logger);

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
            NullifyEntryFilePath = nullifyEntryFilePath
        };
    }

    /// <summary>
    /// Walks the transitive closure of local <c>.spy</c> imports starting from the entry file.
    /// Standard-library and CLR imports resolve to no <c>.spy</c> file and are excluded; only
    /// on-disk <c>.spy</c> sources become project source files. The entry file's live
    /// <paramref name="entrySource"/> is used for its own scan; imported files are read from disk.
    /// </summary>
    private static List<string> DiscoverLocalImportClosure(
        string entrySource, string entryFilePath, CompilerOptions options, ICompilerLogger logger)
    {
        var closure = new List<string>();
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

            var module = ParseModuleForImports(moduleSource, file, options.Features, logger);
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
    private static Parser.Ast.Module? ParseModuleForImports(
        string source, string filePath, Shared.FeatureFlags features, ICompilerLogger logger)
    {
        try
        {
            var sourceText = new SourceText(source, filePath);
            var lexer = new Lexer.Lexer(sourceText, logger) { Features = features };
            var tokens = lexer.TokenizeAll();
            if (lexer.Diagnostics.HasErrors)
                return null;
            var parser = new Parser.Parser(tokens, logger, maxErrors: 25, features: features);
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

    /// <summary>
    /// True if two file paths refer to the same file (case-insensitive full-path compare).
    /// </summary>
    public static bool PathsEqual(string a, string b)
        => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
