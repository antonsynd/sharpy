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

    [Fact]
    public void GetModuleTypes_GenericTypesHaveTypeParameters()
    {
        // Act - Counter<T> is a generic type in the collections module
        var types = _discovery.GetModuleTypes("collections");

        // Assert - Counter should be marked as generic
        var counter = types.FirstOrDefault(t => t.Name.StartsWith("Counter", StringComparison.Ordinal));
        Assert.NotNull(counter);
        Assert.True(counter.IsGeneric, $"Counter (Name={counter.Name}) should be marked as generic");
        Assert.Single(counter.TypeParameters);
        Assert.Equal("T0", counter.TypeParameters[0].Name);
    }

    [Fact]
    public void GetModuleTypes_GenericTypesHave__getitem__Protocol()
    {
        // Act - Counter<T> has an indexer (this[T key])
        var types = _discovery.GetModuleTypes("collections");

        // Assert - Counter should have __getitem__ protocol from CLR indexer
        var counter = types.FirstOrDefault(t => t.Name.StartsWith("Counter", StringComparison.Ordinal));
        Assert.NotNull(counter);
        Assert.True(counter.ProtocolMethods.ContainsKey("__getitem__"),
            "Counter should have __getitem__ protocol from CLR indexer");
    }

    [Fact]
    public void GetModuleTypes_GenericTypeMethodsUseSharedTypeParams()
    {
        // Act
        var types = _discovery.GetModuleTypes("collections");
        var counter = types.FirstOrDefault(t => t.Name.StartsWith("Counter", StringComparison.Ordinal));
        Assert.NotNull(counter);
        Assert.True(counter.IsGeneric);

        // Find a method that uses the type parameter (e.g., most_common returns list of tuples)
        // Verify all TypeParameterType references share the same name
        var tpName = counter.TypeParameters[0].Name;
        foreach (var method in counter.Methods)
        {
            foreach (var param in method.Parameters)
            {
                if (param.Type is TypeParameterType tpt)
                {
                    Assert.Equal(tpName, tpt.Name);
                }
            }
        }
    }

    [Fact]
    public void GetModuleTypes_DefaultDictIsGenericWithTwoTypeParams()
    {
        // Act - DefaultDict<TKey, TValue> should have 2 type parameters
        var types = _discovery.GetModuleTypes("collections");
        var defaultDict = types.FirstOrDefault(
            t => t.Name.StartsWith("DefaultDict", StringComparison.Ordinal));
        Assert.NotNull(defaultDict);

        // Assert
        Assert.True(defaultDict.IsGeneric, "DefaultDict should be marked as generic");
        Assert.Equal(2, defaultDict.TypeParameters.Count);
        Assert.Equal("T0", defaultDict.TypeParameters[0].Name);
        Assert.Equal("T1", defaultDict.TypeParameters[1].Name);
    }

    [Fact]
    public void GetModuleTypes_GenericTypeOperatorsUseSharedTypeParams()
    {
        // Counter supports binary operators (+ on Counter<T>)
        var types = _discovery.GetModuleTypes("collections");
        var counter = types.FirstOrDefault(t => t.Name.StartsWith("Counter", StringComparison.Ordinal));
        Assert.NotNull(counter);

        // Counter should have operator methods from discovery
        if (counter.OperatorMethods.Count > 0)
        {
            var tpName = counter.TypeParameters[0].Name;
            foreach (var (_, overloads) in counter.OperatorMethods)
            {
                foreach (var op in overloads)
                {
                    foreach (var param in op.Parameters)
                    {
                        if (param.Type is TypeParameterType tpt)
                        {
                            Assert.Equal(tpName, tpt.Name);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void GetModuleTypes_DefaultDictHas__getitem__AndMethods()
    {
        // DefaultDict<TKey, TValue> has an indexer and methods
        var types = _discovery.GetModuleTypes("collections");
        var defaultDict = types.FirstOrDefault(
            t => t.Name.StartsWith("DefaultDict", StringComparison.Ordinal));
        Assert.NotNull(defaultDict);

        // Should have __getitem__ protocol from CLR indexer
        Assert.True(defaultDict.ProtocolMethods.ContainsKey("__getitem__"),
            "DefaultDict should have __getitem__ protocol from CLR indexer");

        // Should have methods discovered from Sharpy.Core
        Assert.NotEmpty(defaultDict.Methods);
    }

    [Fact]
    public void GetModuleTypes_GenericSkeletonsHaveDeclaringTypeOnTypeParams()
    {
        // TypeParameterType instances on skeleton-loaded types should have
        // DeclaringType set to the skeleton TypeSymbol
        var types = _discovery.GetModuleTypes("collections");
        var counter = types.FirstOrDefault(t => t.Name.StartsWith("Counter", StringComparison.Ordinal));
        Assert.NotNull(counter);
        Assert.True(counter.IsGeneric);

        // Method parameters that are TypeParameterType should have DeclaringType set
        // (set during LoadAssembly Pass 2 in CachedModuleDiscovery)
        var typeParamTypes = counter.Methods
            .SelectMany(m => m.Parameters)
            .Select(p => p.Type)
            .OfType<TypeParameterType>()
            .ToList();

        if (typeParamTypes.Count > 0)
        {
            foreach (var tpt in typeParamTypes)
            {
                Assert.NotNull(tpt.DeclaringType);
                Assert.Equal(counter, tpt.DeclaringType);
            }
        }
    }
}
