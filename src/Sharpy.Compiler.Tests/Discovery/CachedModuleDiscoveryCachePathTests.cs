using FluentAssertions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.TestInfrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// The three ways <see cref="CachedModuleDiscovery.LoadAssembly"/> can end (#175): a cold miss that
/// reflects and writes an index, a warm hit that does not reflect, and a rebuild after the cache is
/// cleared. Existing coverage tests the two ends — <c>OverloadIndexCacheTests</c> exercises
/// <c>TryLoad</c>/<c>Save</c> on hand-built indices, and <c>CachedModuleDiscoveryTypeTests</c>
/// exercises what discovery produces — but nothing pins the transition between them, which is where
/// a stale index becomes a wrong compile.
///
/// <para><b>Hermetic by construction.</b> Every test gets its own cache directory. That matters
/// more than usual here: the real cache lives in <c>~/.sharpy/cache/overload-index</c>, outside the
/// repo, so no clean checkout or <c>obj/</c> wipe resets it — a test that touched it could pass or
/// fail based on a previous session's artifacts. It also isolates the process-lifetime memo, which
/// <see cref="OverloadIndexCache"/> keys by full cache-file path (#1049): distinct directories key
/// distinctly, so one test's warm memo can never serve another test's cold load.</para>
/// </summary>
public class CachedModuleDiscoveryCachePathTests : IDisposable
{
    private readonly List<string> _cacheDirs = new();

    public void Dispose()
    {
        foreach (var dir in _cacheDirs)
        {
            try
            { Directory.Delete(dir, recursive: true); }
            catch { }
        }
    }

    /// <summary>Cold path: nothing on disk, so discovery reflects and writes an index.</summary>
    [Fact]
    public void LoadAssembly_ColdCache_RecordsAMissAndWritesAnIndex()
    {
        var dir = NewCacheDir();
        var cache = new OverloadIndexCache(dir);
        var discovery = new CachedModuleDiscovery(cache);

        cache.GetInfo().CachedAssemblies.Should().Be(
            0, "the arrangement is only cold if the directory starts empty");

        discovery.LoadAssembly(SharpyCoreReference.Assembly);

        cache.Statistics.Misses.Should().Be(1, "an empty cache directory cannot serve the assembly");
        cache.Statistics.Hits.Should().Be(0);
        cache.GetInfo().CachedAssemblies.Should().Be(
            1, "the index built by reflection must be written back, or every compile pays for it again");

        // Non-vacuity: a discovery that loaded nothing would also report one miss.
        discovery.GetModuleFunctions("builtins").Should().NotBeEmpty(
            "the cold load must actually produce the builtins module's functions");
    }

    /// <summary>
    /// Warm path: a second discovery over the same cache directory is served, not re-reflected —
    /// and is served the <em>same</em> content, which is the half a hit counter cannot check.
    /// </summary>
    [Fact]
    public void LoadAssembly_WarmCache_RecordsAHitAndServesTheSameSymbols()
    {
        var dir = NewCacheDir();

        var coldCache = new OverloadIndexCache(dir);
        var cold = new CachedModuleDiscovery(coldCache);
        cold.LoadAssembly(SharpyCoreReference.Assembly);
        coldCache.Statistics.Misses.Should().Be(1, "control: the first load must be the cold one");

        var warmCache = new OverloadIndexCache(dir);
        var warm = new CachedModuleDiscovery(warmCache);
        warm.LoadAssembly(SharpyCoreReference.Assembly);

        warmCache.Statistics.Hits.Should().Be(
            1, "the index written by the cold load must satisfy the second load");
        warmCache.Statistics.Misses.Should().Be(
            0, "a miss here would mean the written index was rejected — a silent re-reflect on every compile");

        var coldFunctions = Signatures(cold);
        var warmFunctions = Signatures(warm);

        coldFunctions.Should().NotBeEmpty("without content there is nothing for the comparison to mean");
        warmFunctions.Should().BeEquivalentTo(
            coldFunctions,
            "a cached load must reconstruct what reflection found; a hit that serves a lossy index is "
            + "worse than a miss, because the compile silently sees fewer overloads");
    }

    /// <summary>
    /// Rebuild path. <c>ClearAll</c> has to evict the process-lifetime memo for its directory as
    /// well as the files (#1049) — otherwise "clear the cache and rebuild" silently re-serves the
    /// in-memory copy and a compiler-side mapping change stays invisible. Only a
    /// <em>miss</em> proves the rebuild happened; a passing compile would not.
    /// </summary>
    [Fact]
    public void LoadAssembly_AfterClearCache_RebuildsFromReflection()
    {
        var dir = NewCacheDir();

        var firstCache = new OverloadIndexCache(dir);
        var first = new CachedModuleDiscovery(firstCache);
        first.LoadAssembly(SharpyCoreReference.Assembly);
        firstCache.GetInfo().CachedAssemblies.Should().Be(1, "control: the first load populated the cache");

        first.ClearCache();
        firstCache.GetInfo().CachedAssemblies.Should().Be(0, "ClearCache must delete the index files");

        var rebuiltCache = new OverloadIndexCache(dir);
        var rebuilt = new CachedModuleDiscovery(rebuiltCache);
        rebuilt.LoadAssembly(SharpyCoreReference.Assembly);

        rebuiltCache.Statistics.Misses.Should().Be(
            1, "a cleared cache must force a reflection rebuild; a hit here means the in-memory layer "
            + "outlived the clear and the rebuild never happened (#1049)");
        rebuiltCache.GetInfo().CachedAssemblies.Should().Be(
            1, "the rebuilt index is written back like any other cold load");
        Signatures(rebuilt).Should().NotBeEmpty("the rebuild must produce real symbols, not an empty index");
    }

    /// <summary>
    /// The same assembly twice on one instance is one load. <c>_loadedIndices</c> is a
    /// <c>Lazy</c>-valued <c>GetOrAdd</c> precisely so a concurrent second caller does not run the
    /// factory again; the single-threaded shape of that guarantee is what this pins.
    /// </summary>
    [Fact]
    public void LoadAssembly_TwiceOnOneInstance_ConsultsTheCacheOnce()
    {
        var dir = NewCacheDir();
        var cache = new OverloadIndexCache(dir);
        var discovery = new CachedModuleDiscovery(cache);

        discovery.LoadAssembly(SharpyCoreReference.Assembly);
        discovery.LoadAssembly(SharpyCoreReference.Assembly);

        (cache.Statistics.Hits + cache.Statistics.Misses).Should().Be(
            1, "the second LoadAssembly is served from _loadedIndices and must not reach the cache at all");
        discovery.GetLoadedModules().Should().Contain(
            "builtins", "the one load that did happen must still have registered the assembly's modules");
    }

    private string NewCacheDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy-discovery-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _cacheDirs.Add(dir);
        return dir;
    }

    /// <summary>
    /// A comparable projection of what discovery produced: name plus arity for every builtins
    /// function. Comparing symbols directly would compare object identity, which differs by
    /// construction — each <see cref="CachedModuleDiscovery"/> builds its own instances.
    /// </summary>
    private static List<string> Signatures(CachedModuleDiscovery discovery)
        => discovery.GetModuleFunctions("builtins")
            .Select(f => $"{f.Name}/{f.Parameters.Count}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
}
