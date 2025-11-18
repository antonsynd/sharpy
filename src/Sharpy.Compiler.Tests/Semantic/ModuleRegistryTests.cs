using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class ModuleRegistryTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly OverloadIndexCache _cache;

    public ModuleRegistryTests()
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
    public void Constructor_InitializesSuccessfully()
    {
        var registry = new ModuleRegistry(cache: _cache);

        Assert.NotNull(registry);
        Assert.Empty(registry.Errors);
    }

    [Fact]
    public void LoadReference_WithSharpyCore_LoadsSuccessfully()
    {
        var registry = new ModuleRegistry(cache: _cache);
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

        var result = registry.LoadReference(sharpyCoreAssembly);

        Assert.True(result);
        Assert.Empty(registry.Errors);
        Assert.Contains("builtins", registry.GetLoadedModules());
    }

    [Fact]
    public void LoadReference_WithNonExistentAssembly_ReturnsFalse()
    {
        var registry = new ModuleRegistry(cache: _cache);

        var result = registry.LoadReference("NonExistent.dll");

        Assert.False(result);
        Assert.NotEmpty(registry.Errors);
    }

    [Fact]
    public void LoadReference_SameAssemblyTwice_DoesNotDuplicate()
    {
        var registry = new ModuleRegistry(cache: _cache);
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

        var result1 = registry.LoadReference(sharpyCoreAssembly);
        var result2 = registry.LoadReference(sharpyCoreAssembly);

        Assert.True(result1);
        Assert.True(result2);
        Assert.Single(registry.GetLoadedModules(), m => m == "builtins");
    }

    [Fact]
    public void GetModuleFunctions_WithBuiltins_ReturnsFunctions()
    {
        var registry = new ModuleRegistry(cache: _cache);
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        registry.LoadReference(sharpyCoreAssembly);

        var functions = registry.GetModuleFunctions("builtins");

        Assert.NotEmpty(functions);
        Assert.Contains(functions, f => f.Name == "print");
        Assert.Contains(functions, f => f.Name == "range");
        Assert.Contains(functions, f => f.Name == "len");
    }

    [Fact]
    public void GetModuleFunctions_WithNonExistentModule_ReturnsEmpty()
    {
        var registry = new ModuleRegistry(cache: _cache);

        var functions = registry.GetModuleFunctions("nonexistent");

        Assert.Empty(functions);
    }

    [Fact]
    public void IsModuleLoaded_WithLoadedModule_ReturnsTrue()
    {
        var registry = new ModuleRegistry(cache: _cache);
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        registry.LoadReference(sharpyCoreAssembly);

        var result = registry.IsModuleLoaded("builtins");

        Assert.True(result);
    }

    [Fact]
    public void IsModuleLoaded_WithNonLoadedModule_ReturnsFalse()
    {
        var registry = new ModuleRegistry(cache: _cache);

        var result = registry.IsModuleLoaded("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public void AddModulePath_WithValidPath_AddsSuccessfully()
    {
        var registry = new ModuleRegistry(cache: _cache);
        var tempPath = Path.GetTempPath();

        registry.AddModulePath(tempPath);

        // No direct way to verify, but should not throw
        Assert.Empty(registry.Errors);
    }

    [Fact]
    public void AddModulePath_WithNonExistentPath_LogsWarning()
    {
        var registry = new ModuleRegistry(cache: _cache);
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Should not throw, just log warning
        registry.AddModulePath(nonExistentPath);

        Assert.Empty(registry.Errors);
    }

    [Fact]
    public void ClearCache_DoesNotThrow()
    {
        var registry = new ModuleRegistry(cache: _cache);

        // Should not throw
        registry.ClearCache();

        Assert.Empty(registry.Errors);
    }
}
