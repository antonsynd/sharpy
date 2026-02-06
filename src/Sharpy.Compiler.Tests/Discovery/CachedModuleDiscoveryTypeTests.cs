using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

public class CachedModuleDiscoveryTypeTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly CachedModuleDiscovery _discovery;

    public CachedModuleDiscoveryTypeTests()
    {
        _testCacheDir = Path.Combine(Path.GetTempPath(), "sharpy-test-cache", Guid.NewGuid().ToString());
        var cache = new OverloadIndexCache(_testCacheDir);
        _discovery = new CachedModuleDiscovery(cache);
        _discovery.LoadAssembly(SharpyCoreReference.Assembly);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testCacheDir))
        {
            try
            { Directory.Delete(_testCacheDir, recursive: true); }
            catch { /* ignore cleanup errors */ }
        }
    }

    [Fact]
    public void GetModuleTypes_ReturnsTypeSymbolsForExceptionTypes()
    {
        // Act
        var types = _discovery.GetModuleTypes("builtins");

        // Assert
        Assert.NotEmpty(types);
        var exceptionTypes = types.Where(t => typeof(Exception).IsAssignableFrom(t.ClrType)).ToList();
        Assert.Contains(exceptionTypes, t => t.Name == "TypeError");
        Assert.Contains(exceptionTypes, t => t.Name == "ValueError");
        Assert.Contains(exceptionTypes, t => t.Name == "RuntimeError");
    }

    [Fact]
    public void GetModuleTypes_TypeSymbolsHaveNonNullClrType()
    {
        // Act
        var types = _discovery.GetModuleTypes("builtins");

        // Assert
        foreach (var typeSymbol in types)
        {
            Assert.NotNull(typeSymbol.ClrType);
        }
    }

    [Fact]
    public void GetModuleTypes_EmptyModuleReturnsEmptyList()
    {
        // Act
        var types = _discovery.GetModuleTypes("nonexistent_module");

        // Assert
        Assert.Empty(types);
    }

    [Fact]
    public void GetModuleTypes_TypeSymbolsArePublic()
    {
        // Act
        var types = _discovery.GetModuleTypes("builtins");

        // Assert
        foreach (var typeSymbol in types)
        {
            Assert.Equal(AccessLevel.Public, typeSymbol.AccessLevel);
            Assert.Equal(SymbolKind.Type, typeSymbol.Kind);
        }
    }
}
