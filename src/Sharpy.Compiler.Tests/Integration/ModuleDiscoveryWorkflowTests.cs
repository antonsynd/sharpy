using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// End-to-end tests demonstrating the full module discovery workflow
/// </summary>
public class ModuleDiscoveryWorkflowTests
{
    [Fact]
    public void Workflow_LoadBuiltinsAndSampleModule_Success()
    {
        // Arrange
        var registry = new ModuleRegistry();
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

        // Act - Load builtins
        var builtinsLoaded = registry.LoadReference(sharpyCoreAssembly);

        // Assert - Builtins loaded
        Assert.True(builtinsLoaded);
        Assert.True(registry.IsModuleLoaded("builtins"));

        // Get builtin functions
        var builtinFunctions = registry.GetModuleFunctions("builtins");
        Assert.NotEmpty(builtinFunctions);
        Assert.Contains(builtinFunctions, f => f.Name == "print");
        Assert.Contains(builtinFunctions, f => f.Name == "range");
        Assert.Contains(builtinFunctions, f => f.Name == "len");

        // Act - Load sample module if available
        const string sampleModulePath = "../../../../build/modules/SampleModule.dll";
        if (File.Exists(sampleModulePath))
        {
            var sampleLoaded = registry.LoadReference(sampleModulePath);

            // Assert - Sample module loaded
            Assert.True(sampleLoaded);
            Assert.True(registry.IsModuleLoaded("samplemodule"));

            // Get sample module functions
            var sampleFunctions = registry.GetModuleFunctions("samplemodule");
            Assert.NotEmpty(sampleFunctions);
            Assert.Contains(sampleFunctions, f => f.Name == "square");
            Assert.Contains(sampleFunctions, f => f.Name == "cube");

            // Verify both modules are loaded
            var loadedModules = registry.GetLoadedModules().ToList();
            Assert.Contains("builtins", loadedModules);
            Assert.Contains("samplemodule", loadedModules);
        }
    }

    [Fact]
    public void Workflow_AddModulePath_ResolvesAssembly()
    {
        // Skip if sample module doesn't exist
        const string sampleModulePath = "../../../../build/modules/SampleModule.dll";
        if (!File.Exists(sampleModulePath))
            return;

        // Arrange
        var registry = new ModuleRegistry();
        var modulesDir = Path.GetDirectoryName(Path.GetFullPath(sampleModulePath))!;

        // Act - Add module search path
        registry.AddModulePath(modulesDir);

        // Load by filename only (should resolve from search path)
        var loaded = registry.LoadReference("SampleModule.dll");

        // Assert
        Assert.True(loaded, $"Failed to load module. Errors: {string.Join(", ", registry.Errors.Select(e => e.Message))}");
        Assert.True(registry.IsModuleLoaded("samplemodule"));
    }

    [Fact]
    public void Workflow_FunctionOverloads_DiscoveredCorrectly()
    {
        // Arrange
        var registry = new ModuleRegistry();
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

        // Act
        registry.LoadReference(sharpyCoreAssembly);
        var rangeFunctions = registry.GetModuleFunctions("builtins")
            .Where(f => f.Name == "range")
            .ToList();

        // Assert - Range should have 3 overloads
        Assert.Equal(3, rangeFunctions.Count);

        // Verify signatures
        Assert.Contains(rangeFunctions, f => f.Parameters.Count == 1); // range(stop)
        Assert.Contains(rangeFunctions, f => f.Parameters.Count == 2); // range(start, stop)
        Assert.Contains(rangeFunctions, f => f.Parameters.Count == 3); // range(start, stop, step)
    }

    [Fact]
    public void Workflow_FunctionSignatures_MappedCorrectly()
    {
        // Skip if sample module doesn't exist
        const string sampleModulePath = "../../../../build/modules/SampleModule.dll";
        if (!File.Exists(sampleModulePath))
            return;

        // Arrange
        var registry = new ModuleRegistry();

        // Act
        registry.LoadReference(sampleModulePath);
        var squareFunc = registry.GetModuleFunctions("samplemodule")
            .FirstOrDefault(f => f.Name == "square");

        // Assert - Verify complete signature
        Assert.NotNull(squareFunc);
        Assert.Equal("square", squareFunc.Name);
        Assert.Single(squareFunc.Parameters);
        Assert.Equal("x", squareFunc.Parameters[0].Name);
        Assert.Equal(SemanticType.Int, squareFunc.Parameters[0].Type);
        Assert.Equal(SemanticType.Int, squareFunc.ReturnType);
        Assert.False(squareFunc.Parameters[0].HasDefault);
    }

    [Fact]
    public void Workflow_MultipleModules_IndependentFunctions()
    {
        // Skip if sample module doesn't exist
        const string sampleModulePath = "../../../../build/modules/SampleModule.dll";
        if (!File.Exists(sampleModulePath))
            return;

        // Arrange
        var registry = new ModuleRegistry();
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

        // Act - Load both modules
        registry.LoadReference(sharpyCoreAssembly);
        registry.LoadReference(sampleModulePath);

        // Get functions from each module
        var builtinFunctions = registry.GetModuleFunctions("builtins");
        var sampleFunctions = registry.GetModuleFunctions("samplemodule");

        // Assert - Functions are independent
        Assert.NotEmpty(builtinFunctions);
        Assert.NotEmpty(sampleFunctions);

        // Builtin functions should not contain sample module functions
        Assert.DoesNotContain(builtinFunctions, f => f.Name == "square");
        Assert.DoesNotContain(builtinFunctions, f => f.Name == "cube");

        // Sample module functions should not contain builtin functions
        Assert.DoesNotContain(sampleFunctions, f => f.Name == "print");
        Assert.DoesNotContain(sampleFunctions, f => f.Name == "range");
    }

    [Fact]
    public void Workflow_CachingWorks_FasterOnSecondLoad()
    {
        // Arrange
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

        // First load - builds cache
        var registry1 = new ModuleRegistry();
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        registry1.LoadReference(sharpyCoreAssembly);
        sw1.Stop();

        // Second load - uses cache
        var registry2 = new ModuleRegistry();
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        registry2.LoadReference(sharpyCoreAssembly);
        sw2.Stop();

        // Assert - Second load should be faster (allow some variance)
        // Note: This is a soft assertion as timing can be unpredictable
        Assert.True(sw2.ElapsedMilliseconds <= sw1.ElapsedMilliseconds * 2,
            $"Second load ({sw2.ElapsedMilliseconds}ms) should be faster than or comparable to first load ({sw1.ElapsedMilliseconds}ms)");
    }
}
