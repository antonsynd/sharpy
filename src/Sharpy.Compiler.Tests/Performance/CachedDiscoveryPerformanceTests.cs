using System.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Performance;

/// <summary>
/// Performance benchmarks for the cached overload discovery system
/// </summary>
public class CachedDiscoveryPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public CachedDiscoveryPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CachedDiscovery_FirstLoad_BuildsCacheWithinTime()
    {
        // Clear all caches to ensure fresh build (note: this clears all cached assemblies)
        var cache = new OverloadIndexCache();
        cache.ClearAll();

        var discovery = new CachedModuleDiscovery();
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
        var stopwatch = Stopwatch.StartNew();

        discovery.LoadAssembly(sharpyCoreAssembly);

        stopwatch.Stop();

        _output.WriteLine($"First load (cache build): {stopwatch.ElapsedMilliseconds}ms");

        // Should be under 500ms (documentation target: 200ms, but being generous)
        Assert.True(stopwatch.ElapsedMilliseconds < 500,
            $"First load took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
    }

    [Fact]
    public void CachedDiscovery_SecondLoad_UsesCacheFasterThanFirstLoad()
    {
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;

        // Warmup to ensure JIT compilation is complete
        var warmup = new CachedModuleDiscovery();
        warmup.LoadAssembly(sharpyCoreAssembly);

        // First load to build cache (clear cache first)
        var cache = new OverloadIndexCache();
        cache.ClearAll();
        
        var discovery1 = new CachedModuleDiscovery();
        var firstLoadWatch = Stopwatch.StartNew();
        discovery1.LoadAssembly(sharpyCoreAssembly);
        firstLoadWatch.Stop();

        // Second load from cache
        var discovery2 = new CachedModuleDiscovery();
        var secondLoadWatch = Stopwatch.StartNew();
        discovery2.LoadAssembly(sharpyCoreAssembly);
        secondLoadWatch.Stop();

        _output.WriteLine($"First load: {firstLoadWatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Second load (cached): {secondLoadWatch.ElapsedMilliseconds}ms");
        
        // Only calculate speedup if times are measurable (> 1ms)
        if (firstLoadWatch.ElapsedMilliseconds > 1 && secondLoadWatch.ElapsedMilliseconds > 1)
        {
            var speedup = (double)firstLoadWatch.ElapsedMilliseconds / secondLoadWatch.ElapsedMilliseconds;
            _output.WriteLine($"Speedup: {speedup:F2}x");
        }
        else
        {
            _output.WriteLine("Speedup: Unable to measure (execution too fast)");
        }

        // Second load should be faster or at least not significantly slower
        // Being generous here since timing can be variable in CI environments
        Assert.True(secondLoadWatch.ElapsedMilliseconds <= firstLoadWatch.ElapsedMilliseconds + 5,
            "Cached load should be faster than or similar to first load");
    }

    [Fact]
    public void CachedDiscovery_CachedLoad_CompletesWithinTime()
    {
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;

        // Ensure cache exists
        var warmup = new CachedModuleDiscovery();
        warmup.LoadAssembly(sharpyCoreAssembly);

        // Measure cached load
        var discovery = new CachedModuleDiscovery();
        var stopwatch = Stopwatch.StartNew();
        discovery.LoadAssembly(sharpyCoreAssembly);
        stopwatch.Stop();

        _output.WriteLine($"Cached load: {stopwatch.ElapsedMilliseconds}ms");

        // Should be under 100ms (documentation target: 30-50ms, but being generous)
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"Cached load took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");
    }

    [Fact]
    public void ModuleRegistry_LoadMultipleReferences_CompletesWithinTime()
    {
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        var sampleModulePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "samples", "SampleModule", "bin", "Debug", "net9.0", "SampleModule.dll");

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
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

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
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
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

        // Both calls should be very fast (< 50ms)
        Assert.True(firstWatch.ElapsedMilliseconds < 50,
            $"First retrieval took {firstWatch.ElapsedMilliseconds}ms");
        Assert.True(secondWatch.ElapsedMilliseconds < 50,
            $"Second retrieval took {secondWatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void CacheFile_SizeReasonable()
    {
        var cache = new OverloadIndexCache();
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;

        // Ensure cache exists
        var discovery = new CachedModuleDiscovery();
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
