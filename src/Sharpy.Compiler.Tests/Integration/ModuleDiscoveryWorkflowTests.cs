using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// End-to-end tests demonstrating the full module discovery workflow
/// </summary>
public class ModuleDiscoveryWorkflowTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly OverloadIndexCache _cache;

    public ModuleDiscoveryWorkflowTests()
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
    public void Workflow_LoadBuiltinsAndSampleModule_Success()
    {
        // Arrange
        var registry = new ModuleRegistry(cache: _cache);
        var sharpyCoreAssembly = SharpyCoreReference.Location;

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
        const string sampleModulePath = "../../../../build/modules/SampleModule.dll";

        // Skip test if SampleModule hasn't been built
        if (!File.Exists(sampleModulePath))
        {
            Assert.True(true, $"Test skipped: SampleModule not found at {sampleModulePath}");
            return;
        }

        // Arrange
        var registry = new ModuleRegistry(cache: _cache);
        var modulesDir = Path.GetDirectoryName(Path.GetFullPath(sampleModulePath))!;

        // Act - Add module search path
        registry.AddModulePath(modulesDir);

        // Load by filename only (should resolve from search path)
        var loaded = registry.LoadReference("SampleModule.dll");

        // Assert
        Assert.True(loaded, $"Failed to load module. Errors: {string.Join(", ", registry.Diagnostics.GetErrors().Select(e => e.Message))}");
        Assert.True(registry.IsModuleLoaded("samplemodule"));
    }

    [Fact]
    public void Workflow_FunctionOverloads_DiscoveredCorrectly()
    {
        // Arrange
        var registry = new ModuleRegistry(cache: _cache);
        var sharpyCoreAssembly = SharpyCoreReference.Location;

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
        const string sampleModulePath = "../../../../build/modules/SampleModule.dll";

        // Skip test if SampleModule hasn't been built
        if (!File.Exists(sampleModulePath))
        {
            Assert.True(true, $"Test skipped: SampleModule not found at {sampleModulePath}");
            return;
        }

        // Arrange
        var registry = new ModuleRegistry(cache: _cache);

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
        const string sampleModulePath = "../../../../build/modules/SampleModule.dll";

        // Skip test if SampleModule hasn't been built
        if (!File.Exists(sampleModulePath))
        {
            Assert.True(true, $"Test skipped: SampleModule not found at {sampleModulePath}");
            return;
        }

        // Arrange
        var registry = new ModuleRegistry(cache: _cache);
        var sharpyCoreAssembly = SharpyCoreReference.Location;

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
    public void Workflow_CachingWorks_HitOnSecondLoad()
    {
        var sharpyCoreAssembly = SharpyCoreReference.Location;

        // First load — populates cache, should record misses
        var cache = new OverloadIndexCache(_testCacheDir);
        var registry1 = new ModuleRegistry(cache: cache);
        registry1.LoadReference(sharpyCoreAssembly);

        Assert.True(cache.Statistics.Misses >= 1,
            $"Expected at least 1 cache miss on first load, got {cache.Statistics.Misses}");
        Assert.Equal(0, cache.Statistics.Hits);

        // Second load — same cache directory, should hit cache
        var cache2 = new OverloadIndexCache(_testCacheDir);
        var registry2 = new ModuleRegistry(cache: cache2);
        registry2.LoadReference(sharpyCoreAssembly);

        Assert.True(cache2.Statistics.Hits >= 1,
            $"Expected at least 1 cache hit on second load, got {cache2.Statistics.Hits}");
    }

    [Fact]
    public void Workflow_ModuleFunctionOverloads_PopulatedInModuleInfo()
    {
        var registry = new ModuleRegistry(cache: _cache);
        registry.LoadReference(SharpyCoreReference.Location);
        registry.LoadReference(SharpyStdlibReference.Location);

        // Act - Get os.path functions
        var osPathFunctions = registry.GetModuleFunctions("os.path");

        // Assert - join should have 3 overloads (2-arg, 3-arg, 4-arg)
        var joinOverloads = osPathFunctions.Where(f => f.Name == "join").ToList();
        Assert.True(joinOverloads.Count >= 2,
            $"Expected at least 2 overloads for os.path.join, got {joinOverloads.Count}");
        Assert.Contains(joinOverloads, f => f.Parameters.Count == 2);
        Assert.Contains(joinOverloads, f => f.Parameters.Count == 3);
    }

    [Fact]
    public void Workflow_ModuleFunctionOverloads_ThreadedToModuleSymbol()
    {
        var registry = new ModuleRegistry(cache: _cache);
        registry.LoadReference(SharpyCoreReference.Location);
        registry.LoadReference(SharpyStdlibReference.Location);

        var logger = Sharpy.Compiler.Logging.NullLogger.Instance;
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticBinding = new SemanticBinding();

        var importResolver = new ImportResolver(logger, moduleRegistry: registry,
            semanticBinding: semanticBinding);

        // Act - Resolve os.path via import statement
        var source = "import os.path\n\ndef main():\n    pass\n";
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, logger);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, logger);
        var module = parser.ParseModule();

        importResolver.ResolveAllImports(module, symbolTable, "/test");

        // Assert - ModuleSymbol should have FunctionOverloads populated
        var osSymbol = symbolTable.Lookup("os", searchParents: false) as ModuleSymbol;
        Assert.NotNull(osSymbol);

        var pathSymbol = osSymbol.Exports["path"] as ModuleSymbol;
        Assert.NotNull(pathSymbol);
        Assert.True(pathSymbol.FunctionOverloads.ContainsKey("join"),
            "ModuleSymbol.FunctionOverloads should contain 'join'");

        var joinOverloads = pathSymbol.FunctionOverloads["join"];
        Assert.True(joinOverloads.Count >= 2,
            $"Expected at least 2 overloads for join in FunctionOverloads, got {joinOverloads.Count}");
    }
}
