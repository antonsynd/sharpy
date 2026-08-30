using System.Xml.Linq;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;

namespace Sharpy.Compiler;

/// <summary>
/// A NuGet package reference declared in a .spyproj file
/// </summary>
public record PackageRef(string Name, string Version);

/// <summary>
/// A source file the import-closure discovery pass already lexed and parsed cleanly, handed
/// forward so <see cref="Project.ProjectCompiler.ParseAllFiles"/> can reuse the artifacts
/// instead of lexing and parsing the same file a second time (#1086). Only clean parses (zero
/// lexer/parser diagnostics) are cached; error-bearing files are omitted so the parse phase
/// re-runs them to produce authoritative diagnostics. The captured <see cref="Source"/> is the
/// exact text the artifacts were produced from, so a cache hit compiles that text (no disk
/// TOCTOU) — the same contract as <see cref="ProjectConfig.InMemorySources"/>.
/// </summary>
internal sealed record PreParsedUnit(
    string Source, IReadOnlyList<Token> Tokens, Module Ast, IReadOnlyList<CommentSpan>? CommentSpans);

/// <summary>
/// Configuration for a Sharpy project loaded from a .spyproj file
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// Full path to the .spyproj file
    /// </summary>
    public string ProjectFilePath { get; init; } = string.Empty;

    /// <summary>
    /// Directory containing the .spyproj file (project root)
    /// </summary>
    public string ProjectDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Root namespace for the project
    /// </summary>
    public string RootNamespace { get; init; } = string.Empty;

    /// <summary>
    /// Output type: "library" or "exe"
    /// </summary>
    public string OutputType { get; init; } = "library";

    /// <summary>
    /// Target framework (e.g., "net10.0")
    /// </summary>
    public string TargetFramework { get; init; } = "net10.0";

    /// <summary>
    /// Output assembly name (defaults to root namespace if not specified)
    /// </summary>
    public string? AssemblyName { get; init; }

    /// <summary>
    /// Entry point file for executable projects (e.g., "main.spy")
    /// If not specified, defaults to "main.spy" for Exe projects
    /// </summary>
    public string? EntryPoint { get; init; }

    /// <summary>
    /// List of Sharpy source files to compile (resolved from glob patterns)
    /// </summary>
    public List<string> SourceFiles { get; init; } = new();

    /// <summary>
    /// Optional in-memory source overrides keyed by source-file path. When a file in
    /// <see cref="SourceFiles"/> has an entry here, the compiler uses this text instead of
    /// reading the file from disk. Used by the synthetic project-of-one-file (#1038) so a
    /// single-file compile of inline source keeps the caller's path verbatim (for <c>#line</c>
    /// directives and deterministic output) without materializing a temp file.
    /// </summary>
    internal Dictionary<string, string>? InMemorySources { get; init; }

    /// <summary>
    /// Optional pre-parsed artifacts keyed by source-file path (same
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> comparer as <see cref="InMemorySources"/>,
    /// supplied at construction in <see cref="SyntheticProject.BuildConfig"/>). When a file in
    /// <see cref="SourceFiles"/> has an entry here, the parse phase reuses the discovery pass's
    /// tokens/AST/CommentSpans instead of lexing and parsing it again (#1086). Only clean parses
    /// are handed forward; a file with lexer/parser diagnostics is absent from this map so
    /// <see cref="Project.ProjectCompiler.ParseAllFiles"/> re-parses it for real diagnostics.
    /// </summary>
    internal Dictionary<string, PreParsedUnit>? PreParsedUnits { get; init; }

    /// <summary>
    /// When true, the parse phase lexes with trivia preservation on and surfaces
    /// per-unit <see cref="Model.CompilationUnit.CommentSpans"/>. Off by default (the
    /// project/CLI compile path never needs comment locations); the single-file analyze
    /// path (LSP hover) sets it so <see cref="Project.FileAnalysisResult.CommentSpans"/>
    /// matches what the former single-file analyze driver produced. With the flag off,
    /// output is byte-identical to trivia-less parsing.
    /// </summary>
    internal bool PreserveTrivia { get; init; }

    /// <summary>
    /// When true, the entry file is given no path identity: its symbols' DeclaringFilePath /
    /// DefiningFilePath and its per-node reference locations are recorded with a null path.
    /// Off by default. The single-file analyze path (#1087) sets it to reproduce the historical
    /// single-file contract where the main file carried no path, so LSP handlers (rename, type
    /// hierarchy, highlight) fall back to the request document URI instead of the synthetic
    /// entry path (<c>&lt;source&gt;</c>). Only the entry file is affected; imported closure
    /// files keep their real paths for cross-file navigation.
    /// </summary>
    internal bool NullifyEntryFilePath { get; init; }

    /// <summary>
    /// Controls whether code generation emits <c>#line</c> directives mapping generated C# back
    /// to Sharpy source. Defaults to true (the <see cref="CodeGen.CodeGenContext"/> default).
    /// The REPL (#1087) sets it false: it compiles the generated C# to an in-memory assembly and
    /// executes it, so source-line mapping is irrelevant and generated-C# coordinates are what the
    /// SPY0908 net reports on emit failure.
    /// </summary>
    internal bool EmitLineDirectives { get; init; } = true;

    /// <summary>
    /// List of .NET assembly references
    /// </summary>
    public List<string> References { get; init; } = new();

    /// <summary>
    /// Module search paths for assembly resolution
    /// </summary>
    public List<string> ModulePaths { get; init; } = new();

    /// <summary>
    /// NuGet package references for the project
    /// </summary>
    public List<PackageRef> PackageReferences { get; init; } = new();

    /// <summary>
    /// Maps lowercase package name to the version actually resolved by NuGet restore
    /// (from project.assets.json). Populated by the CLI after a successful restore;
    /// consumed by <see cref="Project.NuGetResolver"/> to find the correct cache path
    /// when NuGet resolves a different version than what was requested (#1580).
    /// </summary>
    public IReadOnlyDictionary<string, string>? ResolvedVersions { get; set; }

    /// <summary>
    /// Build configuration (Debug or Release)
    /// </summary>
    public string Configuration { get; init; } = "Debug";

    /// <summary>
    /// Treat all warnings as errors during compilation.
    /// Set via &lt;WarningsAsErrors&gt;true&lt;/WarningsAsErrors&gt; in .spyproj.
    /// </summary>
    public bool WarningsAsErrors { get; init; }

    /// <summary>
    /// Whether this project's emitted C# is compiled into a TEST HOST — an xUnit project that
    /// supplies the framework reference and runs the result through a test runner (#1495).
    ///
    /// <para>Spelled <c>&lt;TestHost&gt;true&lt;/TestHost&gt;</c>. It is opt-in and false everywhere
    /// else, because the framework-coupled `@test` assert lowering is only correct where the
    /// framework is present: emitting <c>Xunit.Assert.*</c> without it made any program containing a
    /// <c>@test</c> function uncompilable under <c>sharpyc run</c> (CS0246 behind SPY0908). The spy
    /// test corpora set it so their generated C# keeps the runner integration byte-for-byte.</para>
    /// </summary>
    public bool TestHost { get; init; }

    /// <summary>
    /// Warning codes to suppress (e.g., "SPY0451", "SPY0452").
    /// Set via &lt;NoWarn&gt;SPY0451,SPY0452&lt;/NoWarn&gt; in .spyproj.
    /// </summary>
    public HashSet<string> SuppressedWarnings { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Experimental feature flags to enable for this project.
    /// Set via &lt;Features&gt;feature_a;feature_b&lt;/Features&gt; in .spyproj.
    /// Merged (unioned) with CLI <c>--enable-feature</c> flags at compile entry.
    /// </summary>
    public List<string> Features { get; init; } = new();

    /// <summary>
    /// Output directory for compiled assemblies (relative to project directory)
    /// </summary>
    public string OutputPath
    {
        get
        {
            var binPath = Path.Combine(ProjectDirectory, "bin", Configuration, TargetFramework);
            return binPath;
        }
    }

    /// <summary>
    /// When set, overrides the computed <see cref="OutputAssemblyPath"/> with an explicit
    /// path. Used by single-file compilation (the synthetic project-of-one-file, #1038) so
    /// the CLI can direct the emitted assembly to a chosen location (a temp file for
    /// <c>run</c>, or the <c>-o</c> path for <c>build</c>/<c>compile</c>) instead of the
    /// project's <c>bin/{Config}/{TFM}</c> convention.
    /// </summary>
    public string? OutputAssemblyPathOverride { get; set; }

    /// <summary>
    /// When set, crash bundles land under this directory instead of the output assembly's
    /// directory. The test harness sets it to a temp path so crash-bundle copies of fixture
    /// sources never end up inside the fixture tree (#1660).
    /// </summary>
    public string? CrashRoot { get; init; }

    /// <summary>
    /// Compute CodeGenInfo during semantic analysis. This is required for code generation.
    /// Symbols will have their CodeGenInfo property populated after type checking,
    /// containing pre-computed C# names, version numbers, and other code generation metadata.
    /// Note: Setting this to false will cause code generation to fail since legacy tracking
    /// has been removed.
    /// </summary>
    public bool UsePrecomputedCodeGenInfo { get; set; } = true;

    /// <summary>
    /// Full path to the output assembly
    /// </summary>
    public virtual string OutputAssemblyPath
    {
        get
        {
            if (!string.IsNullOrEmpty(OutputAssemblyPathOverride))
                return OutputAssemblyPathOverride!;

            var assemblyName = AssemblyName ?? RootNamespace;
            var extension = OutputType.ToLowerInvariant() == "exe" ? ".exe" : ".dll";
            return Path.Combine(OutputPath, assemblyName + extension);
        }
    }

    /// <summary>
    /// Returns a shallow copy of this config with <see cref="InMemorySources"/> set to
    /// <paramref name="sources"/>, leaving the original untouched. The LSP (#1099) uses this to
    /// overlay unsaved workspace buffers onto project-mode analysis without mutating the cached
    /// <c>_projectConfig</c>. Every other property (the source-file list, references, feature
    /// flags, ...) is carried over by reference; the compiler treats those as read-only during a
    /// single compile, so sharing the collection instances is safe.
    /// </summary>
    internal ProjectConfig WithInMemorySources(Dictionary<string, string> sources)
    {
        return new ProjectConfig
        {
            ProjectFilePath = ProjectFilePath,
            ProjectDirectory = ProjectDirectory,
            RootNamespace = RootNamespace,
            OutputType = OutputType,
            TargetFramework = TargetFramework,
            AssemblyName = AssemblyName,
            EntryPoint = EntryPoint,
            SourceFiles = SourceFiles,
            InMemorySources = sources,
            PreParsedUnits = PreParsedUnits,
            PreserveTrivia = PreserveTrivia,
            NullifyEntryFilePath = NullifyEntryFilePath,
            EmitLineDirectives = EmitLineDirectives,
            References = References,
            ModulePaths = ModulePaths,
            PackageReferences = PackageReferences,
            ResolvedVersions = ResolvedVersions,
            Configuration = Configuration,
            WarningsAsErrors = WarningsAsErrors,
            TestHost = TestHost,
            SuppressedWarnings = SuppressedWarnings,
            Features = Features,
            OutputAssemblyPathOverride = OutputAssemblyPathOverride,
            UsePrecomputedCodeGenInfo = UsePrecomputedCodeGenInfo,
            CrashRoot = CrashRoot,
        };
    }
}

/// <summary>
/// Parser and discovery helper for .spyproj project files. This is the single
/// .spyproj parser used by both the compiler and the LSP (see #1038).
/// </summary>
public static class ProjectFileParser
{
    /// <summary>
    /// Load and parse a .spyproj file
    /// </summary>
    public static ProjectConfig Load(string projectFilePath, string? configuration = null)
    {
        if (!File.Exists(projectFilePath))
        {
            throw new FileNotFoundException($"Project file not found: {projectFilePath}");
        }

        var projectDirectory = Path.GetDirectoryName(projectFilePath) ?? Directory.GetCurrentDirectory();
        var document = XDocument.Load(projectFilePath);
        var root = document.Root;

        if (root == null || root.Name.LocalName != "Project")
        {
            throw new InvalidDataException("Invalid .spyproj file: missing <Project> root element");
        }

        // Parse PropertyGroup
        var propertyGroup = root.Element("PropertyGroup");
        if (propertyGroup == null)
        {
            throw new InvalidDataException("Invalid .spyproj file: missing <PropertyGroup> element");
        }

        var rootNamespace = propertyGroup.Element("RootNamespace")?.Value;
        if (string.IsNullOrWhiteSpace(rootNamespace))
        {
            throw new InvalidDataException("Invalid .spyproj file: <RootNamespace> is required");
        }

        var outputType = propertyGroup.Element("OutputType")?.Value ?? "library";
        var targetFramework = propertyGroup.Element("TargetFramework")?.Value ?? "net10.0";
        var assemblyName = propertyGroup.Element("AssemblyName")?.Value;
        var entryPoint = propertyGroup.Element("EntryPoint")?.Value;
        var warningsAsErrors = bool.TryParse(propertyGroup.Element("WarningsAsErrors")?.Value, out var wae) && wae;
        var testHost = bool.TryParse(propertyGroup.Element("TestHost")?.Value, out var th) && th;
        var noWarnValue = propertyGroup.Element("NoWarn")?.Value;
        var suppressedWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(noWarnValue))
        {
            foreach (var code in noWarnValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                suppressedWarnings.Add(code.Trim());
        }

        // Parse experimental feature flags. Unknown names fail fast at project load
        // so a typo cannot silently disable a feature.
        var featuresValue = propertyGroup.Element("Features")?.Value;
        var features = new List<string>();
        if (!string.IsNullOrWhiteSpace(featuresValue))
        {
            foreach (var raw in featuresValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = raw.Trim();
                if (name.Length == 0)
                    continue;
                if (!Shared.FeatureFlags.TryValidate(name, out var error))
                    throw new InvalidDataException($"Invalid <Features> value in {Path.GetFileName(projectFilePath)}: {error}");
                if (!features.Contains(name))
                    features.Add(name);
            }
        }

        // Parse ItemGroup for source files
        var sourceFiles = new List<string>();
        var references = new List<string>();
        var modulePaths = new List<string>();
        var packageReferences = new List<PackageRef>();

        foreach (var itemGroup in root.Elements("ItemGroup"))
        {
            // Parse SpyFile includes with Exclude support
            foreach (var spyFile in itemGroup.Elements("SpyFile"))
            {
                var include = spyFile.Attribute("Include")?.Value;
                var exclude = spyFile.Attribute("Exclude")?.Value;

                if (!string.IsNullOrWhiteSpace(include))
                {
                    var resolvedFiles = ResolveGlobPattern(projectDirectory, include, exclude);
                    sourceFiles.AddRange(resolvedFiles);
                }
            }

            // Also support SourceFile element name (alias for SpyFile)
            foreach (var sourceFile in itemGroup.Elements("SourceFile"))
            {
                var include = sourceFile.Attribute("Include")?.Value;
                var exclude = sourceFile.Attribute("Exclude")?.Value;

                if (!string.IsNullOrWhiteSpace(include))
                {
                    var resolvedFiles = ResolveGlobPattern(projectDirectory, include, exclude);
                    sourceFiles.AddRange(resolvedFiles);
                }
            }

            // Parse Reference includes
            foreach (var reference in itemGroup.Elements("Reference"))
            {
                var include = reference.Attribute("Include")?.Value;
                if (!string.IsNullOrWhiteSpace(include))
                {
                    // Resolve relative paths
                    var referencePath = Path.IsPathRooted(include)
                        ? include
                        : Path.Combine(projectDirectory, include);
                    references.Add(referencePath);
                }
            }

            // Parse ModulePath includes
            foreach (var modulePath in itemGroup.Elements("ModulePath"))
            {
                var include = modulePath.Attribute("Include")?.Value;
                if (!string.IsNullOrWhiteSpace(include))
                {
                    var modulePathResolved = Path.IsPathRooted(include)
                        ? include
                        : Path.Combine(projectDirectory, include);
                    modulePaths.Add(modulePathResolved);
                }
            }

            // Parse PackageReference includes
            foreach (var packageRef in itemGroup.Elements("PackageReference"))
            {
                var include = packageRef.Attribute("Include")?.Value;
                var version = packageRef.Attribute("Version")?.Value;
                if (!string.IsNullOrWhiteSpace(include) && !string.IsNullOrWhiteSpace(version))
                {
                    packageReferences.Add(new PackageRef(include, version));
                }
            }
        }

        // Remove duplicates from source files, then sort deterministically (#1032).
        // Glob enumeration order is filesystem-dependent; an ordinal sort on the
        // normalized full path gives a culture-invariant, stable compile order.
        sourceFiles = sourceFiles
            .Distinct()
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        if (sourceFiles.Count == 0)
        {
            throw new InvalidDataException(
                "No source files found in project. Add <SpyFile Include=\"...\" /> or <SourceFile Include=\"...\" /> elements.");
        }

        return new ProjectConfig
        {
            ProjectFilePath = Path.GetFullPath(projectFilePath),
            ProjectDirectory = projectDirectory,
            RootNamespace = rootNamespace,
            OutputType = outputType,
            TargetFramework = targetFramework,
            AssemblyName = assemblyName,
            EntryPoint = entryPoint,
            SourceFiles = sourceFiles,
            References = references,
            ModulePaths = modulePaths,
            PackageReferences = packageReferences,
            Configuration = configuration ?? "Debug",
            WarningsAsErrors = warningsAsErrors,
            TestHost = testHost,
            SuppressedWarnings = suppressedWarnings,
            Features = features
        };
    }

    /// <summary>
    /// Find a .spyproj file in the specified directory
    /// </summary>
    public static string? FindProjectFile(string directory)
    {
        var projectFiles = SourceGlob.EnumerateArtifacts(directory, "*.spyproj").ToArray();

        if (projectFiles.Length == 0)
        {
            return null;
        }

        if (projectFiles.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple .spyproj files found in '{directory}'. Please specify which project to build:\n" +
                string.Join("\n", projectFiles.Select(f => $"  - {Path.GetFileName(f)}")));
        }

        return projectFiles[0];
    }

    private static List<string> ResolveGlobPattern(string baseDirectory, string includePattern, string? excludePattern = null)
        => SourceGlob.ResolveGlob(baseDirectory, includePattern, excludePattern);
}
