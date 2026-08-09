using Sharpy.Compiler.Discovery.Caching;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery.Caching;

public class OverloadIndexCacheTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly OverloadIndexCache _cache;

    public OverloadIndexCacheTests()
    {
        // Use a unique temporary directory for each test instance to avoid conflicts
        _testCacheDir = Path.Combine(Path.GetTempPath(), "sharpy-test-cache", Guid.NewGuid().ToString());
        _cache = new OverloadIndexCache(_testCacheDir);
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
    public void GetInfo_ReturnsValidInfo()
    {
        // Act
        var info = _cache.GetInfo();

        // Assert
        Assert.NotNull(info);
        Assert.NotEmpty(info.CacheDirectory);
    }

    [Fact]
    public void TryLoad_ReturnsNullForNonExistentCache()
    {
        // Arrange
        var identity = new AssemblyIdentity
        {
            Name = "NonExistent",
            Version = "1.0.0",
            ContentHash = "xyz789"
        };

        // Act
        var index = _cache.TryLoad(identity);

        // Assert
        Assert.Null(index);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        // Arrange
        var identity = new AssemblyIdentity
        {
            Name = "TestAssembly",
            Version = "1.0.0",
            ContentHash = "abc123test",
            FilePath = "/test/path.dll"
        };

        var originalIndex = new OverloadIndex
        {
            Identity = identity,
            CreatedAt = DateTime.UtcNow,
            CacheFormatVersion = OverloadIndexCache.CurrentCacheFormatVersion
        };

        originalIndex.Modules["testmodule"] = new ModuleOverloads
        {
            ModuleName = "testmodule",
            Functions = new Dictionary<string, List<FunctionSignature>>
            {
                ["test_func"] = new List<FunctionSignature>
                {
                    new FunctionSignature
                    {
                        Name = "test_func",
                        ReturnType = new TypeSignature { Name = "int" },
                        Parameters = new List<ParameterSignature>
                        {
                            new ParameterSignature
                            {
                                Name = "x",
                                Type = new TypeSignature { Name = "int" }
                            }
                        }
                    }
                }
            }
        };

        try
        {
            // Act
            _cache.Save(originalIndex);
            var loadedIndex = _cache.TryLoad(identity);

            // Assert
            Assert.NotNull(loadedIndex);
            Assert.Equal(identity, loadedIndex.Identity);
            Assert.True(loadedIndex.Modules.TryGetValue("testmodule", out var module));
            Assert.Single(module.Functions);
        }
        finally
        {
            // Cleanup
            _cache.ClearAll();
        }
    }

    [Fact]
    public void TryLoad_RejectsOldCacheFormatVersion()
    {
        // Arrange - save a V1 cache
        var identity = new AssemblyIdentity
        {
            Name = "OldVersionTest",
            Version = "1.0.0",
            ContentHash = "oldver123",
            FilePath = "/test/oldver.dll"
        };

        var v1Index = new OverloadIndex
        {
            Identity = identity,
            CreatedAt = DateTime.UtcNow,
            CacheFormatVersion = 1
        };
        _cache.Save(v1Index);

        // Act
        var loaded = _cache.TryLoad(identity);

        // Assert - V1 cache should be rejected
        Assert.Null(loaded);
    }

    /// <summary>
    /// #1313: an index built by a different compiler is never served. The cache key carries the
    /// compiler identity, so the rebuilt compiler looks somewhere else entirely — which is what
    /// makes a CLR-type-mapping change take effect without a manual
    /// <c>CurrentCacheFormatVersion</c> bump.
    /// </summary>
    [Fact]
    public void TryLoad_RejectsIndexBuiltByADifferentCompiler()
    {
        var built = MakeIdentity("CompilerVersionTest", "1.0.0.0-aaaaaaaaaaaa");
        _cache.Save(MakeIndex(built, "from_the_old_compiler"));

        var rebuilt = MakeIdentity("CompilerVersionTest", "1.0.0.0-bbbbbbbbbbbb");

        Assert.Null(_cache.TryLoad(rebuilt));
        Assert.NotNull(_cache.TryLoad(built));
    }

    /// <summary>
    /// The two indices live under distinct keys, so each compiler keeps serving its own — a
    /// rebuilt compiler does not clobber the index the previous one wrote (and vice versa).
    /// If the compiler component ever left <c>ToCacheKey</c> the second save would overwrite the
    /// first and this fails.
    /// </summary>
    [Fact]
    public void TryLoad_IndicesFromDifferentCompilersCoexist()
    {
        var built = MakeIdentity("CoexistTest", "1.0.0.0-aaaaaaaaaaaa");
        var rebuilt = MakeIdentity("CoexistTest", "1.0.0.0-bbbbbbbbbbbb");

        _cache.Save(MakeIndex(built, "module_from_a"));
        _cache.Save(MakeIndex(rebuilt, "module_from_b"));

        var loadedA = _cache.TryLoad(built);
        var loadedB = _cache.TryLoad(rebuilt);

        Assert.NotNull(loadedA);
        Assert.NotNull(loadedB);
        Assert.Contains("module_from_a", loadedA.Modules.Keys);
        Assert.Contains("module_from_b", loadedB.Modules.Keys);
    }

    /// <summary>
    /// Belt and braces for the key partition: a cache FILE whose serialized identity names a
    /// different compiler than the requesting identity is rejected and deleted, exactly as a
    /// content-hash mismatch is. This is the <c>Identity.Equals</c> half of #1313 — remove
    /// <c>CompilerVersion</c> from <c>AssemblyIdentity.Equals</c> and the stale index is served.
    /// </summary>
    [Fact]
    public void TryLoad_DeletesCacheFileWhoseCompilerVersionMismatches()
    {
        var built = MakeIdentity("PlantedStaleTest", "1.0.0.0-aaaaaaaaaaaa");
        var rebuilt = MakeIdentity("PlantedStaleTest", "1.0.0.0-bbbbbbbbbbbb");

        _cache.Save(MakeIndex(built, "stale_module"));

        // Plant the old compiler's index at the new compiler's cache path so TryLoad has to
        // reach the identity check rather than simply missing the file.
        var builtPath = Path.Combine(_testCacheDir, built.ToCacheKey());
        var rebuiltPath = Path.Combine(_testCacheDir, rebuilt.ToCacheKey());
        File.Move(builtPath, rebuiltPath);

        var loaded = _cache.TryLoad(rebuilt);

        Assert.Null(loaded);
        Assert.False(File.Exists(rebuiltPath), "a stale index is deleted, not left to be re-read");
    }

    /// <summary>
    /// The process-lifetime memo (OverloadIndexCache.TryLoad's fast path) must uphold the same
    /// compiler-identity invariant the disk path does. The on-disk file is deleted first, so the
    /// only thing that can answer either probe is the in-memory layer.
    /// </summary>
    [Fact]
    public void TryLoad_InMemoryFastPath_RejectsADifferentCompilerVersion()
    {
        var built = MakeIdentity("MemoFastPathTest", "1.0.0.0-aaaaaaaaaaaa");
        var rebuilt = MakeIdentity("MemoFastPathTest", "1.0.0.0-bbbbbbbbbbbb");

        _cache.Save(MakeIndex(built, "memoized_module"));
        // Warm the memo through the documented entry point, then remove every file so a disk
        // read can neither serve nor rescue the next probe.
        Assert.NotNull(_cache.TryLoad(built));
        foreach (var file in Directory.GetFiles(_testCacheDir, "*.json.gz"))
            File.Delete(file);

        var servedFromMemo = _cache.TryLoad(built);
        var rejected = _cache.TryLoad(rebuilt);

        Assert.NotNull(servedFromMemo);
        Assert.Contains("memoized_module", servedFromMemo.Modules.Keys);
        Assert.Null(rejected);
    }

    private static AssemblyIdentity MakeIdentity(string name, string compilerVersion) => new()
    {
        Name = name,
        Version = "1.0.0",
        ContentHash = "compilerversion123",
        FilePath = $"/test/{name}.dll",
        CompilerVersion = compilerVersion
    };

    private static OverloadIndex MakeIndex(AssemblyIdentity identity, string moduleName)
    {
        var index = new OverloadIndex
        {
            Identity = identity,
            CreatedAt = DateTime.UtcNow,
            CacheFormatVersion = OverloadIndexCache.CurrentCacheFormatVersion
        };
        index.Modules[moduleName] = new ModuleOverloads { ModuleName = moduleName };
        return index;
    }

    [Fact]
    public void ClearAll_RemovesCacheFiles()
    {
        // Arrange
        var identity = new AssemblyIdentity
        {
            Name = "ClearTest",
            Version = "1.0.0",
            ContentHash = "clear123",
            FilePath = "/test/clear.dll"
        };

        var index = new OverloadIndex { Identity = identity };
        _cache.Save(index);

        // Act
        _cache.ClearAll();
        var loaded = _cache.TryLoad(identity);

        // Assert
        Assert.Null(loaded);
    }
}
