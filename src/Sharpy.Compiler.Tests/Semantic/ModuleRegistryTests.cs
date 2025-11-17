using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class ModuleRegistryTests
{
    [Fact]
    public void Constructor_InitializesSuccessfully()
    {
        var registry = new ModuleRegistry();
        
        Assert.NotNull(registry);
        Assert.Empty(registry.Errors);
    }

    [Fact]
    public void LoadReference_WithSharpyCore_LoadsSuccessfully()
    {
        var registry = new ModuleRegistry();
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

        var result = registry.LoadReference(sharpyCoreAssembly);

        Assert.True(result);
        Assert.Empty(registry.Errors);
        Assert.Contains("builtins", registry.GetLoadedModules());
    }

    [Fact]
    public void LoadReference_WithNonExistentAssembly_ReturnsFalse()
    {
        var registry = new ModuleRegistry();

        var result = registry.LoadReference("NonExistent.dll");

        Assert.False(result);
        Assert.NotEmpty(registry.Errors);
    }

    [Fact]
    public void LoadReference_SameAssemblyTwice_DoesNotDuplicate()
    {
        var registry = new ModuleRegistry();
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

        var result1 = registry.LoadReference(sharpyCoreAssembly);
        var result2 = registry.LoadReference(sharpyCoreAssembly);

        Assert.True(result1);
        Assert.True(result2);
        Assert.Single(registry.GetLoadedModules().Where(m => m == "builtins"));
    }

    [Fact]
    public void GetModuleFunctions_WithBuiltins_ReturnsFunctions()
    {
        var registry = new ModuleRegistry();
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
        var registry = new ModuleRegistry();

        var functions = registry.GetModuleFunctions("nonexistent");

        Assert.Empty(functions);
    }

    [Fact]
    public void IsModuleLoaded_WithLoadedModule_ReturnsTrue()
    {
        var registry = new ModuleRegistry();
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;
        registry.LoadReference(sharpyCoreAssembly);

        var result = registry.IsModuleLoaded("builtins");

        Assert.True(result);
    }

    [Fact]
    public void IsModuleLoaded_WithNonLoadedModule_ReturnsFalse()
    {
        var registry = new ModuleRegistry();

        var result = registry.IsModuleLoaded("nonexistent");

        Assert.False(result);
    }

    [Fact]
    public void AddModulePath_WithValidPath_AddsSuccessfully()
    {
        var registry = new ModuleRegistry();
        var tempPath = Path.GetTempPath();

        registry.AddModulePath(tempPath);

        // No direct way to verify, but should not throw
        Assert.Empty(registry.Errors);
    }

    [Fact]
    public void AddModulePath_WithNonExistentPath_LogsWarning()
    {
        var registry = new ModuleRegistry();
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Should not throw, just log warning
        registry.AddModulePath(nonExistentPath);

        Assert.Empty(registry.Errors);
    }

    [Fact]
    public void ClearCache_DoesNotThrow()
    {
        var registry = new ModuleRegistry();
        
        // Should not throw
        registry.ClearCache();
        
        Assert.Empty(registry.Errors);
    }
}
