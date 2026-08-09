using System.Text.Json;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// What <see cref="IncrementalCompilationCache"/> does when the files on disk are not what it wrote
/// (#175). The happy round-trip is covered by <c>IncrementalCompilationTests</c>; the three ways a
/// cache goes bad are not, and each one fails silently by design — the loaders swallow the problem
/// and return an empty dictionary, so a regression that turned "rebuild" into "restore garbage"
/// would surface as a wrong compile rather than an exception.
///
/// <para>Every case is paired with the untouched cache as its control. A cache that had never
/// restored anything would satisfy "does not restore" for free, which is exactly how an
/// invalidation test passes while testing nothing.</para>
/// </summary>
public class IncrementalCompilationCacheEdgeTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public IncrementalCompilationCacheEdgeTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_cache_edge_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    // --- .sharpy-symbols: corruption ------------------------------------------------------------

    /// <summary>
    /// A truncated or hand-mangled <c>.sharpy-symbols</c> must degrade to a clean rebuild, not an
    /// exception: <c>LoadSymbolCache</c> catches <see cref="JsonException"/> and starts fresh. The
    /// control restore proves the cache really did hold this file before the corruption.
    /// </summary>
    [Fact]
    public void CorruptSymbolCache_LoadsCleanAndRestoresNothing()
    {
        var config = CreateConfig("def main():\n    print('hello')\n");
        var file = config.SourceFiles[0];

        SaveOneEntry(config, file, "// generated");

        var control = new IncrementalCompilationCache(config, NullLogger.Instance);
        control.LoadAllCaches();
        control.GetFileCache(file).Should().NotBeNull(
            "control: an intact cache restores this file — without it, 'restores nothing' below is free");

        File.WriteAllText(SymbolCachePath(config), "{ \"SchemaVersion\": 22, \"Files\": { truncated");

        var afterCorruption = new IncrementalCompilationCache(config, NullLogger.Instance);
        var act = () => afterCorruption.LoadAllCaches();

        act.Should().NotThrow("a corrupt cache must never fail the build — it must be rebuilt");
        afterCorruption.GetFileCache(file).Should().BeNull(
            "no entry survives an unparseable cache, so the file is recompiled");
        afterCorruption.HasValidFileCache(file).Should().BeFalse();
    }

    /// <summary>
    /// End-to-end: a corrupt symbol cache costs a rebuild and nothing else. Asserting the skip
    /// count is what makes this more than "it compiled" — a build that silently restored the
    /// garbage would also succeed, and a build that skipped on hashes alone (ignoring the missing
    /// symbols) would be the actual #175 failure mode.
    /// </summary>
    [Fact]
    public void CorruptSymbolCache_SecondBuildRecompilesEveryFile()
    {
        using var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace("CorruptSymbolCache")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .WithIncremental();

        helper.AddSourceFile("lib.spy", "def value() -> int:\n    return 42\n");
        helper.AddSourceFile("main.spy", "from lib import value\n\n\ndef main():\n    print(value())\n");
        helper.CreateProjectFile();

        helper.AssertCompilationSucceeded(helper.Compile());

        var symbolCache = Path.Combine(helper.ProjectDirectory, "obj", "Debug", ".sharpy-symbols");
        File.Exists(symbolCache).Should().BeTrue("control: the first build must have written a symbol cache");

        // Control run first: with the cache intact the second build skips, so the assertion below
        // measures the corruption and not, say, incremental mode being off.
        var warm = helper.Compile();
        helper.AssertCompilationSucceeded(warm);
        warm.Metrics!.SkippedFileCount.Should().BeGreaterThan(
            0, "control: an intact cache lets the second build skip unchanged files");

        File.WriteAllText(symbolCache, "not json at all");

        var afterCorruption = helper.Compile();

        helper.AssertCompilationSucceeded(afterCorruption);
        afterCorruption.Metrics!.SkippedFileCount.Should().Be(
            0, "a file can only be skipped when it has a valid cache entry, so a corrupt symbol "
            + "cache must recompile everything rather than skip on hashes alone");
    }

    // --- .sharpy-symbols: schema version --------------------------------------------------------

    /// <summary>
    /// A cache written by an older schema must be rebuilt, not read. This is the invalidation that
    /// every <c>CurrentSchemaVersion</c> bump relies on: v22 exists because restoring a v21 entry
    /// would emit a different type (#1284).
    /// </summary>
    [Fact]
    public void OlderSchemaVersion_IsRebuiltNotRestored()
    {
        var config = CreateConfig("def main():\n    print('hello')\n");
        var file = config.SourceFiles[0];

        SaveOneEntry(config, file, "// generated");

        var control = new IncrementalCompilationCache(config, NullLogger.Instance);
        control.LoadAllCaches();
        control.GetFileCache(file).Should().NotBeNull(
            "control: at the current schema version the entry restores");

        var path = SymbolCachePath(config);
        var json = File.ReadAllText(path);
        json.Should().Contain(
            $"\"SchemaVersion\": {IncrementalCompilationCache.CurrentSchemaVersion}",
            "the saved envelope must carry the current version, or downgrading it below tests nothing");
        File.WriteAllText(path, json.Replace(
            $"\"SchemaVersion\": {IncrementalCompilationCache.CurrentSchemaVersion}",
            $"\"SchemaVersion\": {IncrementalCompilationCache.CurrentSchemaVersion - 1}",
            StringComparison.Ordinal));

        var downgraded = new IncrementalCompilationCache(config, NullLogger.Instance);
        downgraded.LoadAllCaches();

        downgraded.GetFileCache(file).Should().BeNull(
            "an envelope from a different schema version is discarded wholesale — restoring it would "
            + "deserialize fields that no longer mean what they meant when they were written");
    }

    // --- .sharpy-cache: compiler version --------------------------------------------------------

    /// <summary>
    /// The hash cache is keyed to the compiler that wrote it. A rebuilt compiler can lower the same
    /// unchanged source differently, so <c>LoadHashCache</c> drops every hash when the recorded
    /// version does not match — which makes every file stale and forces a full recompile. This is
    /// the only invalidation that catches a compiler change with no source change at all.
    /// </summary>
    [Fact]
    public void CompilerVersionChange_MakesEveryFileStale()
    {
        var config = CreateConfig("def main():\n    print('hello')\n");
        var file = config.SourceFiles[0];

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache.UpdateHash(file);
        cache.SaveCache();

        var control = new IncrementalCompilationCache(config, NullLogger.Instance);
        control.IsStale(file).Should().BeFalse(
            "control: with a matching compiler version the recorded hash is honoured — without this, "
            + "'stale' below would just mean the hash was never written");

        var path = HashCachePath(config);
        var metadata = JsonSerializer.Deserialize<CacheMetadata>(File.ReadAllText(path), JsonOptions)!;
        metadata.CompilerVersion.Should().Be(
            IncrementalCompilationCache.GetCompilerVersion(),
            "the saved metadata must record this compiler, or replacing it below proves nothing");
        metadata.FileHashes.Should().ContainKey(
            PathNormalizer.Normalize(file), "the hash for the source file must be what gets invalidated");

        File.WriteAllText(path, JsonSerializer.Serialize(
            metadata with { CompilerVersion = "0.0.0-not-this-compiler" }, JsonOptions));

        var afterVersionChange = new IncrementalCompilationCache(config, NullLogger.Instance);

        afterVersionChange.IsStale(file).Should().BeTrue(
            "a hash written by a different compiler says nothing about whether this compiler's output "
            + "is current, so every file must be recompiled");
        afterVersionChange.GetFilesToRecompile(config.SourceFiles, dependencyGraph: null)
            .Should().BeEquivalentTo(
                config.SourceFiles,
                "invalidation is wholesale: no file keeps a hash from another compiler");
    }

    /// <summary>
    /// A hash cache from before the versioned format (a bare <c>{path: hash}</c> object) is also
    /// discarded rather than trusted. Same reasoning as the version mismatch — the entries predate
    /// the guarantee that makes them meaningful.
    /// </summary>
    [Fact]
    public void LegacyHashCacheFormat_MakesEveryFileStale()
    {
        var config = CreateConfig("def main():\n    print('hello')\n");
        var file = config.SourceFiles[0];

        // No cache has been constructed yet, so obj/{Configuration}/ does not exist — the real
        // compiler creates it in the IncrementalCompilationCache constructor.
        Directory.CreateDirectory(ObjDir(config));
        File.WriteAllText(HashCachePath(config), JsonSerializer.Serialize(
            new Dictionary<string, string>
            {
                [PathNormalizer.Normalize(file)] = IncrementalCompilationCache.ComputeFileHash(file)
            },
            JsonOptions));

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);

        cache.IsStale(file).Should().BeTrue(
            "the hash in a legacy-format cache is correct for the file but carries no compiler "
            + "identity, so it cannot be honoured");
    }

    // --- Arrangement ---------------------------------------------------------------------------

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private ProjectConfig CreateConfig(string source)
    {
        var file = Path.Combine(_tempDir, "main.spy");
        File.WriteAllText(file, source);

        return new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { file },
            Configuration = "Debug"
        };
    }

    private static void SaveOneEntry(ProjectConfig config, string file, string generatedCSharp)
    {
        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        cache.SaveFileCache(
            file,
            new List<Symbol>
            {
                new FunctionSymbol
                {
                    Name = "main",
                    Kind = SymbolKind.Function,
                    Parameters = new List<ParameterSymbol>(),
                    ReturnType = SemanticType.Void
                }
            },
            generatedCSharp,
            new List<string>());
        cache.SaveAllCaches();
    }

    private static string ObjDir(ProjectConfig config)
        => Path.Combine(config.ProjectDirectory, "obj", config.Configuration);

    private static string SymbolCachePath(ProjectConfig config)
        => Path.Combine(ObjDir(config), ".sharpy-symbols");

    private static string HashCachePath(ProjectConfig config)
        => Path.Combine(ObjDir(config), ".sharpy-cache");
}
