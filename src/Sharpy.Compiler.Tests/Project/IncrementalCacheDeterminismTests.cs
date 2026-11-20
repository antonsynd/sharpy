using System.Text.Json;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The incremental cache files are a function of the cache's content, not of the order the build
/// reached it in (the cell on #2114, issuecomment-7046628875). Cold builds of one project wrote
/// <c>main.spy</c>'s dependencies in two orders in three runs: the dependency graph holds them in an <c>ImmutableHashSet</c>, whose order follows
/// the per-process randomized string hash. The same class covers every keyed map the cache writes:
/// the file entries and file hashes (a warm build appends a new file after the ones the last build
/// wrote) and each entry's generator outputs (filled in generator-run order, merged with the last
/// build's).
///
/// <para>A hash order cannot be varied inside one process, so the guard feeds every order-carrying
/// input in two different orders and asserts identical bytes; the control proves the two feeds
/// differ and that their order reaches the serializer when nothing canonicalizes it.</para>
/// </summary>
public class IncrementalCacheDeterminismTests : IDisposable
{
    private static readonly string[] s_modules = { "alpha", "delta", "bravo", "echo", "charlie" };
    private static readonly string[] s_generators = { "Gen@Zed", "Gen@Abc", "Gen@Mid" };

    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public IncrementalCacheDeterminismTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_cache_determinism_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    /// <summary>
    /// The same cache content written from two feeds in opposite orders (dependencies, file entries,
    /// file hashes, generator outputs cached before and after the file's entry) writes byte-identical
    /// <c>.sharpy-symbols</c> and <c>.sharpy-cache</c>.
    /// </summary>
    [Fact]
    public void SameContentFedInDifferentOrders_WritesIdenticalCacheFiles()
    {
        var config = CreateConfig();

        var forward = WriteCache(config, reverse: false);
        var reversed = WriteCache(config, reverse: true);

        reversed.Symbols.Should().Be(forward.Symbols,
            "the symbol cache must not depend on the order dependencies, entries and generator outputs arrive in");
        reversed.Hashes.Should().Be(forward.Hashes,
            "the hash cache must not depend on the order files were hashed in");
    }

    /// <summary>
    /// Control for the guard above: the two feeds are in different orders, and a dictionary filled in
    /// each order serializes differently. Without this the byte equality could hold because the feeds
    /// were the same, or because the serializer happened to order keys itself.
    /// </summary>
    [Fact]
    public void Control_TheTwoFeedsDifferAndOrderReachesTheSerializer()
    {
        Feed(s_modules, reverse: true).Should().NotEqual(Feed(s_modules, reverse: false),
            "control: the guard must feed its inputs in two different orders");
        Feed(s_modules, reverse: false).Should().NotBeInAscendingOrder(StringComparer.Ordinal,
            "control: the forward feed must not already be the ordinal order the cache writes");
        Feed(s_generators, reverse: false).Should().NotBeInAscendingOrder(StringComparer.Ordinal,
            "control: nor the generator identities");

        static string Serialized(IEnumerable<string> keys)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var key in keys)
                map[key] = 0;
            return JsonSerializer.Serialize(map);
        }

        Serialized(Feed(s_modules, reverse: true)).Should().NotBe(Serialized(Feed(s_modules, reverse: false)),
            "control: a dictionary's insertion order reaches the JSON, so an uncanonicalized map would differ");
    }

    /// <summary>
    /// A real build writes every dependency list, entry key and hash key in ordinal order. Eight
    /// imports leave a hash order one chance in 40320 of being ordinal by accident.
    /// </summary>
    [Fact]
    public void ProjectBuild_WritesDependenciesAndKeysInOrdinalOrder()
    {
        var names = new[] { "kilo", "golf", "alpha", "hotel", "echo", "india", "bravo", "foxtrot" };
        using var helper = new Helpers.ProjectCompilationHelper(_output)
            .WithRootNamespace("CacheDeterminism")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy")
            .WithIncremental();

        foreach (var name in names)
            helper.AddSourceFile($"{name}.spy", $"def {name}_value() -> int:\n    return 1\n");
        helper.AddSourceFile("main.spy",
            string.Concat(names.Select(n => $"import {n}\n"))
            + "\n\ndef main():\n    print("
            + string.Join(" + ", names.Select(n => $"{n}.{n}_value()")) + ")\n");
        helper.CreateProjectFile();
        helper.AssertCompilationSucceeded(helper.Compile());

        var objDir = Path.Combine(helper.ProjectDirectory, "obj", "Debug");
        using var symbols = JsonDocument.Parse(File.ReadAllText(Path.Combine(objDir, ".sharpy-symbols")));
        using var hashes = JsonDocument.Parse(File.ReadAllText(Path.Combine(objDir, ".sharpy-cache")));

        var files = symbols.RootElement.GetProperty("Files");
        var main = files.EnumerateObject().Single(f => f.Name.EndsWith("/main.spy", StringComparison.Ordinal));
        var dependencies = main.Value.GetProperty("Dependencies").EnumerateArray().Select(d => d.GetString()!).ToList();

        dependencies.Should().HaveCount(names.Length, "control: main.spy's entry records every import");
        dependencies.Should().BeInAscendingOrder(StringComparer.Ordinal);
        files.EnumerateObject().Select(f => f.Name).Should().BeInAscendingOrder(StringComparer.Ordinal);
        hashes.RootElement.GetProperty("FileHashes").EnumerateObject().Select(f => f.Name)
            .Should().HaveCount(names.Length + 1).And.BeInAscendingOrder(StringComparer.Ordinal);
    }

    private static List<string> Feed(IEnumerable<string> items, bool reverse)
        => reverse ? items.Reverse().ToList() : items.ToList();

    /// <summary>
    /// Writes one cache state from scratch, every order-carrying input fed forward or reversed:
    /// <c>main.spy</c> depends on every module; the first module gets generator outputs cached before
    /// its entry exists (the pending path), the last after (the existing-entry path).
    /// </summary>
    private (string Symbols, string Hashes) WriteCache(ProjectConfig config, bool reverse)
    {
        var objDir = Path.Combine(config.ProjectDirectory, "obj");
        if (Directory.Exists(objDir))
            Directory.Delete(objDir, recursive: true);

        var cache = new IncrementalCompilationCache(config, NullLogger.Instance);
        var modulePaths = s_modules.Select(ModulePath).ToList();
        var pendingTarget = modulePaths[0];
        var existingTarget = modulePaths[^1];

        foreach (var identity in Feed(s_generators, reverse))
            cache.CacheGeneratorOutput(pendingTarget, identity, "gh", "th", null, $"# {identity}");

        foreach (var file in Feed(config.SourceFiles, reverse))
        {
            cache.UpdateHash(file);
            var dependencies = file == MainPath ? Feed(modulePaths, reverse) : new List<string>();
            cache.SaveFileCache(file, new List<Symbol>(), $"// {Path.GetFileName(file)}", dependencies);
        }

        foreach (var identity in Feed(s_generators, reverse))
            cache.CacheGeneratorOutput(existingTarget, identity, "gh", "th", null, $"# {identity}");

        cache.SaveAllCaches();
        return (File.ReadAllText(Path.Combine(objDir, "Debug", ".sharpy-symbols")),
            File.ReadAllText(Path.Combine(objDir, "Debug", ".sharpy-cache")));
    }

    private string MainPath => Path.Combine(_tempDir, "main.spy");

    private string ModulePath(string name) => Path.Combine(_tempDir, $"{name}.spy");

    private ProjectConfig CreateConfig()
    {
        var sources = new List<string>();
        foreach (var name in s_modules)
        {
            File.WriteAllText(ModulePath(name), $"def {name}() -> int:\n    return 1\n");
            sources.Add(ModulePath(name));
        }
        File.WriteAllText(MainPath, "def main():\n    pass\n");
        sources.Add(MainPath);

        return new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = sources,
            Configuration = "Debug"
        };
    }
}
