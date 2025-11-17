using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

public class ThirdPartyModuleTests
{
    private const string SampleModulePath = "../../../../build/modules/SampleModule.dll";

    [Fact]
    public void LoadSampleModule_LoadsSuccessfully()
    {
        var registry = new ModuleRegistry();

        // Skip if module doesn't exist (e.g., in CI before build)
        if (!File.Exists(SampleModulePath))
            return;

        var result = registry.LoadReference(SampleModulePath);

        Assert.True(result, $"Failed to load sample module. Errors: {string.Join(", ", registry.Errors.Select(e => e.Message))}");
        Assert.Empty(registry.Errors);
    }

    [Fact]
    public void SampleModule_ExportsFunctions()
    {
        var registry = new ModuleRegistry();

        // Skip if module doesn't exist
        if (!File.Exists(SampleModulePath))
            return;

        registry.LoadReference(SampleModulePath);
        var functions = registry.GetModuleFunctions("samplemodule");

        Assert.NotEmpty(functions);
        Assert.Contains(functions, f => f.Name == "square");
        Assert.Contains(functions, f => f.Name == "cube");
        Assert.Contains(functions, f => f.Name == "average");
        Assert.Contains(functions, f => f.Name == "is_prime");
        Assert.Contains(functions, f => f.Name == "factorial");
    }

    [Fact]
    public void SampleModule_SquareFunction_HasCorrectSignature()
    {
        var registry = new ModuleRegistry();

        // Skip if module doesn't exist
        if (!File.Exists(SampleModulePath))
            return;

        registry.LoadReference(SampleModulePath);
        var functions = registry.GetModuleFunctions("samplemodule");
        var squareFunc = functions.FirstOrDefault(f => f.Name == "square");

        Assert.NotNull(squareFunc);
        Assert.Single(squareFunc.Parameters);
        Assert.Equal("x", squareFunc.Parameters[0].Name);
        Assert.Equal(SemanticType.Int, squareFunc.Parameters[0].Type);
        Assert.Equal(SemanticType.Int, squareFunc.ReturnType);
    }

    [Fact]
    public void SampleModule_AverageFunction_HasVariadicParameter()
    {
        var registry = new ModuleRegistry();

        // Skip if module doesn't exist
        if (!File.Exists(SampleModulePath))
            return;

        registry.LoadReference(SampleModulePath);
        var functions = registry.GetModuleFunctions("samplemodule");
        var averageFunc = functions.FirstOrDefault(f => f.Name == "average");

        Assert.NotNull(averageFunc);
        Assert.Single(averageFunc.Parameters);
        Assert.Equal("numbers", averageFunc.Parameters[0].Name);
        // Parameter type should be an array of double
        Assert.True(averageFunc.Parameters[0].Type is GenericType);
    }

    [Fact]
    public void ModuleRegistry_LoadsMultipleModules()
    {
        var registry = new ModuleRegistry();
        var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly.Location;

        registry.LoadReference(sharpyCoreAssembly);

        // Skip sample module if it doesn't exist
        if (File.Exists(SampleModulePath))
        {
            registry.LoadReference(SampleModulePath);
            
            var modules = registry.GetLoadedModules().ToList();
            Assert.Contains("builtins", modules);
            Assert.Contains("samplemodule", modules);
        }
        else
        {
            var modules = registry.GetLoadedModules().ToList();
            Assert.Contains("builtins", modules);
        }
    }
}
