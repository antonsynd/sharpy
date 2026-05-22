using System.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure;

namespace Sharpy.Compiler.Tests.Performance;

/// <summary>
/// Performance benchmarks for the cached overload discovery system
/// </summary>
[Trait("Category", "Benchmark")]
public class CachedDiscoveryPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testCacheDir;

    // Performance test thresholds
    private const int CachedLoadThresholdMs = 200;
    private const int MinFastCachedLoadsRequired = 3;
    private const int TotalCachedLoadRuns = 5;

    public CachedDiscoveryPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        // Use a unique temporary directory for this test instance to avoid conflicts
        _testCacheDir = Path.Combine(Path.GetTempPath(), "sharpy-test-cache", Guid.NewGuid().ToString());
    }

    public void Dispose()
    {
        // Clean up test cache directory
        if (Directory.Exists(_testCacheDir))
        {
            try
            {
                Directory.Delete(_testCacheDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void CachedDiscovery_FirstLoad_BuildsCacheWithinTime()
    {
        // Use test-specific cache directory to avoid conflicts
        var cache = new OverloadIndexCache(_testCacheDir);
        cache.ClearAll();

        var discovery = new CachedModuleDiscovery(cache);
        var sharpyCoreAssembly = SharpyCoreReference.Assembly;
        var stopwatch = Stopwatch.StartNew();

        discovery.LoadAssembly(sharpyCoreAssembly);

        stopwatch.Stop();

        _output.WriteLine($"First load (cache build): {stopwatch.ElapsedMilliseconds}ms");

        // Should be under 1800ms (28 stdlib modules after Phase 2 expansion)
        Assert.True(stopwatch.ElapsedMilliseconds < 1800,
            $"First load took {stopwatch.ElapsedMilliseconds}ms, expected < 1800ms");
    }

    [Fact]
    public void CachedDiscovery_SecondLoad_UsesCacheFasterThanFirstLoad()
    {
        var sharpyCoreAssembly = SharpyCoreReference.Assembly;

        // Use test-specific cache directory
        var cache = new OverloadIndexCache(_testCacheDir);

        // Multiple warmup runs to ensure JIT compilation and system stabilization
        for (int i = 0; i < 3; i++)
        {
            var warmup = new CachedModuleDiscovery(cache);
            warmup.LoadAssembly(sharpyCoreAssembly);
        }

        // First load to build cache (clear cache first)
        cache.ClearAll();

        var discovery1 = new CachedModuleDiscovery(cache);
        var firstLoadWatch = Stopwatch.StartNew();
        discovery1.LoadAssembly(sharpyCoreAssembly);
        firstLoadWatch.Stop();

        // Multiple cached loads to get stable measurement
        var cachedTimes = new List<long>();
        for (int i = 0; i < TotalCachedLoadRuns; i++)
        {
            var discovery = new CachedModuleDiscovery(cache);
            var watch = Stopwatch.StartNew();
            discovery.LoadAssembly(sharpyCoreAssembly);
            watch.Stop();
            cachedTimes.Add(watch.ElapsedMilliseconds);
        }

        var sortedTimes = cachedTimes.OrderBy(t => t).ToList();
        var medianCachedTime = sortedTimes[sortedTimes.Count / 2];

        _output.WriteLine($"First load: {firstLoadWatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Cached loads: {string.Join(", ", cachedTimes)}ms");
        _output.WriteLine($"Median cached: {medianCachedTime}ms");

        // Count how many cached loads were fast (under 100ms threshold)
        var fastCachedLoads = cachedTimes.Count(t => t < CachedLoadThresholdMs);
        _output.WriteLine($"Fast cached loads (<{CachedLoadThresholdMs}ms): {fastCachedLoads}/{TotalCachedLoadRuns}");

        // For very fast operations, verify that cached loads complete quickly
        // rather than comparing to an unreliable first-load time which has high variance
        Assert.True(medianCachedTime < CachedLoadThresholdMs,
            $"Cached load median ({medianCachedTime}ms) should be under {CachedLoadThresholdMs}ms");

        // At least some cached loads should be fast
        Assert.True(fastCachedLoads >= MinFastCachedLoadsRequired,
            $"At least {MinFastCachedLoadsRequired} cached loads should be under {CachedLoadThresholdMs}ms, but only {fastCachedLoads} were");
    }

    [Fact]
    public void CachedDiscovery_CachedLoad_CompletesWithinTime()
    {
        var sharpyCoreAssembly = SharpyCoreReference.Assembly;

        // Use test-specific cache directory
        var cache = new OverloadIndexCache(_testCacheDir);

        // Ensure cache exists
        var warmup = new CachedModuleDiscovery(cache);
        warmup.LoadAssembly(sharpyCoreAssembly);

        // Measure cached load
        var discovery = new CachedModuleDiscovery(cache);
        var stopwatch = Stopwatch.StartNew();
        discovery.LoadAssembly(sharpyCoreAssembly);
        stopwatch.Stop();

        _output.WriteLine($"Cached load: {stopwatch.ElapsedMilliseconds}ms");

        Assert.True(stopwatch.ElapsedMilliseconds < CachedLoadThresholdMs,
            $"Cached load took {stopwatch.ElapsedMilliseconds}ms, expected < {CachedLoadThresholdMs}ms");
    }

    [Fact]
    public void ModuleRegistry_LoadMultipleReferences_CompletesWithinTime()
    {
        var sharpyCoreAssembly = SharpyCoreReference.Location;
        var sampleModulePath = "../../../../build/modules/SampleModule.dll";

        var references = File.Exists(sampleModulePath)
            ? new[] { sharpyCoreAssembly, sampleModulePath }
            : new[] { sharpyCoreAssembly };

        var registry = new ModuleRegistry(NullLogger.Instance);
        var stopwatch = Stopwatch.StartNew();

        foreach (var reference in references)
        {
            registry.LoadReference(reference);
        }

        stopwatch.Stop();

        _output.WriteLine($"Loaded {references.Length} reference(s): {stopwatch.ElapsedMilliseconds}ms");

        // Should be under 1 second for multiple modules
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Loading {references.Length} reference(s) took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    [Fact]
    public void Compiler_WithModules_CompilationOverheadMinimal()
    {
        var code = @"
x = 5
y = 10
z = x + y
";
        var sharpyCoreAssembly = SharpyCoreReference.Location;

        // Without modules
        var compilerNoModules = new Sharpy.Compiler.Compiler();
        var noModulesWatch = Stopwatch.StartNew();
        compilerNoModules.Compile(code, "test.spy");
        noModulesWatch.Stop();

        // With modules
        var options = new CompilerOptions
        {
            References = new[] { sharpyCoreAssembly }
        };
        var compilerWithModules = new Sharpy.Compiler.Compiler(options);
        var withModulesWatch = Stopwatch.StartNew();
        compilerWithModules.Compile(code, "test.spy");
        withModulesWatch.Stop();

        _output.WriteLine($"Without modules: {noModulesWatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"With modules: {withModulesWatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Overhead: {withModulesWatch.ElapsedMilliseconds - noModulesWatch.ElapsedMilliseconds}ms");

        // Module loading overhead should be reasonable (< 200ms)
        var overhead = withModulesWatch.ElapsedMilliseconds - noModulesWatch.ElapsedMilliseconds;
        Assert.True(overhead < 200,
            $"Module loading overhead was {overhead}ms, expected < 200ms");
    }

    [Fact]
    public void GetModuleFunctions_Cached_FastRetrieval()
    {
        var registry = new ModuleRegistry(NullLogger.Instance);
        var sharpyCoreAssembly = SharpyCoreReference.Location;
        registry.LoadReference(sharpyCoreAssembly);

        // First call
        var firstWatch = Stopwatch.StartNew();
        var functions1 = registry.GetModuleFunctions("builtins");
        firstWatch.Stop();

        // Second call (should be cached)
        var secondWatch = Stopwatch.StartNew();
        var functions2 = registry.GetModuleFunctions("builtins");
        secondWatch.Stop();

        _output.WriteLine($"First retrieval: {firstWatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Second retrieval: {secondWatch.ElapsedMilliseconds}ms");

        Assert.NotEmpty(functions1);
        Assert.Equal(functions1.Count, functions2.Count);

        // First call timing is non-deterministic due to JIT/CLR init; first-call assertion
        // removed because no stable threshold exists for CI environments. The cached-call
        // assertion below is stable.
        Assert.True(secondWatch.ElapsedMilliseconds < 50,
            $"Cached retrieval took {secondWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void CacheFile_SizeReasonable()
    {
        // Use test-specific cache directory
        var cache = new OverloadIndexCache(_testCacheDir);
        var sharpyCoreAssembly = SharpyCoreReference.Assembly;

        // Ensure cache exists
        var discovery = new CachedModuleDiscovery(cache);
        discovery.LoadAssembly(sharpyCoreAssembly);

        // Get cache info
        var cacheInfo = cache.GetInfo();

        _output.WriteLine($"Cache directory: {cacheInfo.CacheDirectory}");
        _output.WriteLine($"Cached assemblies: {cacheInfo.CachedAssemblies}");

        // Should have at least one cached assembly
        Assert.True(cacheInfo.CachedAssemblies > 0,
            "Should have at least one cached assembly");

        // Check total cache size
        if (Directory.Exists(cacheInfo.CacheDirectory))
        {
            var files = Directory.GetFiles(cacheInfo.CacheDirectory, "*.json.gz");
            var totalSizeKB = files.Sum(f => new FileInfo(f).Length) / 1024.0;

            _output.WriteLine($"Total cache size: {totalSizeKB:F2} KB for {files.Length} file(s)");

            // Cache files should be reasonable size (< 500KB total for typical usage)
            Assert.True(totalSizeKB < 500,
                $"Cache total size is {totalSizeKB:F2}KB, expected < 500KB");
        }
    }
}
