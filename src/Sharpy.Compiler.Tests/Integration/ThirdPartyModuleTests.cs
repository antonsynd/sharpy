using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

using Sharpy.TestInfrastructure;

namespace Sharpy.Compiler.Tests.Integration;

public class ThirdPartyModuleTests : IDisposable
{
    private const string SampleModulePath = "../../../../build/modules/SampleModule.dll";
    private readonly string _testCacheDir;
    private readonly OverloadIndexCache _cache;

    public ThirdPartyModuleTests()
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
    public void LoadSampleModule_LoadsSuccessfully()
    {
        var registry = new ModuleRegistry(cache: _cache);

        // Skip if module doesn't exist (e.g., in CI before build)
        if (!File.Exists(SampleModulePath))
            return;

        var result = registry.LoadReference(SampleModulePath);

        Assert.True(result, $"Failed to load sample module. Errors: {string.Join(", ", registry.Diagnostics.GetErrors().Select(e => e.Message))}");
        Assert.False(registry.Diagnostics.HasErrors);
    }

    [Fact]
    public void SampleModule_ExportsFunctions()
    {
        var registry = new ModuleRegistry(cache: _cache);

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
        var registry = new ModuleRegistry(cache: _cache);

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
        var registry = new ModuleRegistry(cache: _cache);

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
        var registry = new ModuleRegistry(cache: _cache);
        var sharpyCoreAssembly = SharpyCoreReference.Location;

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
