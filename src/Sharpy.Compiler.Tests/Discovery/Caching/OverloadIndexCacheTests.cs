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
            CacheFormatVersion = 2
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
