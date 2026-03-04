using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// Tests that ConvertToFunctionSymbol with shared TypeParameterType[] remaps
/// generic type parameters to the exact same instances (reference equality).
/// </summary>
public class TypeParameterRemappingTests : IDisposable
{
    private readonly string _testCacheDir;
    private readonly CachedModuleDiscovery _discovery;

    public TypeParameterRemappingTests()
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
    public void ConvertToFunctionSymbol_WithSharedTypeParams_RemapsParameterTypes()
    {
        // Arrange - a signature with a generic parameter at position 0
        var sig = new FunctionSignature
        {
            Name = "append",
            Parameters = new List<ParameterSignature>
            {
                new ParameterSignature
                {
                    Name = "item",
                    Type = new TypeSignature
                    {
                        Name = "T",
                        IsGenericParameter = true,
                        GenericParameterPosition = 0
                    }
                }
            },
            ReturnType = new TypeSignature { Name = "None" }
        };

        var sharedT0 = new TypeParameterType { Name = "T0" };
        var sharedTypeParams = new[] { sharedT0 };

        // Act
        var result = _discovery.ConvertToFunctionSymbol(sig, "List", sharedTypeParams);

        // Assert - parameter type is the exact same object
        Assert.Single(result.Parameters);
        Assert.True(ReferenceEquals(sharedT0, result.Parameters[0].Type),
            "Parameter type should be the same TypeParameterType instance (reference equality)");
    }

    [Fact]
    public void ConvertToFunctionSymbol_WithSharedTypeParams_RemapsReturnType()
    {
        // Arrange - return type is a generic parameter at position 0
        var sig = new FunctionSignature
        {
            Name = "pop",
            Parameters = new List<ParameterSignature>(),
            ReturnType = new TypeSignature
            {
                Name = "T",
                IsGenericParameter = true,
                GenericParameterPosition = 0
            }
        };

        var sharedT0 = new TypeParameterType { Name = "T0" };
        var sharedTypeParams = new[] { sharedT0 };

        // Act
        var result = _discovery.ConvertToFunctionSymbol(sig, "List", sharedTypeParams);

        // Assert
        Assert.True(ReferenceEquals(sharedT0, result.ReturnType),
            "Return type should be the same TypeParameterType instance");
    }

    [Fact]
    public void ConvertToFunctionSymbol_WithTwoSharedTypeParams_RemapsByPosition()
    {
        // Arrange - dict.get(key: TKey) -> TValue, positions 0 and 1
        var sig = new FunctionSignature
        {
            Name = "get",
            Parameters = new List<ParameterSignature>
            {
                new ParameterSignature
                {
                    Name = "key",
                    Type = new TypeSignature
                    {
                        Name = "TKey",
                        IsGenericParameter = true,
                        GenericParameterPosition = 0
                    }
                },
                new ParameterSignature
                {
                    Name = "default",
                    Type = new TypeSignature
                    {
                        Name = "TValue",
                        IsGenericParameter = true,
                        GenericParameterPosition = 1
                    }
                }
            },
            ReturnType = new TypeSignature
            {
                Name = "TValue",
                IsGenericParameter = true,
                GenericParameterPosition = 1
            }
        };

        var sharedT0 = new TypeParameterType { Name = "T0" };
        var sharedT1 = new TypeParameterType { Name = "T1" };
        var sharedTypeParams = new[] { sharedT0, sharedT1 };

        // Act
        var result = _discovery.ConvertToFunctionSymbol(sig, "Dict", sharedTypeParams);

        // Assert - position 0 maps to T0, position 1 maps to T1
        Assert.True(ReferenceEquals(sharedT0, result.Parameters[0].Type),
            "key parameter should be T0");
        Assert.True(ReferenceEquals(sharedT1, result.Parameters[1].Type),
            "default parameter should be T1");
        Assert.True(ReferenceEquals(sharedT1, result.ReturnType),
            "return type should be T1");
    }

    [Fact]
    public void ConvertToFunctionSymbol_WithoutSharedTypeParams_CreatesNewInstances()
    {
        // Arrange - same signature but no shared type params
        var sig = new FunctionSignature
        {
            Name = "append",
            Parameters = new List<ParameterSignature>
            {
                new ParameterSignature
                {
                    Name = "item",
                    Type = new TypeSignature
                    {
                        Name = "T",
                        IsGenericParameter = true,
                        GenericParameterPosition = 0
                    }
                }
            },
            ReturnType = new TypeSignature { Name = "None" }
        };

        // Act
        var result = _discovery.ConvertToFunctionSymbol(sig, "List", sharedTypeParams: null);

        // Assert - creates a new TypeParameterType (not null, correct name)
        var paramType = Assert.IsType<TypeParameterType>(result.Parameters[0].Type);
        Assert.Equal("T", paramType.Name);
    }

    [Fact]
    public void ConvertToFunctionSymbol_SharedTypeParams_InGenericTypeArguments()
    {
        // Arrange - extend(items: list[T]) where T is at position 0
        var sig = new FunctionSignature
        {
            Name = "extend",
            Parameters = new List<ParameterSignature>
            {
                new ParameterSignature
                {
                    Name = "items",
                    Type = new TypeSignature
                    {
                        Name = "list",
                        IsGeneric = true,
                        TypeArguments = new List<TypeSignature>
                        {
                            new TypeSignature
                            {
                                Name = "T",
                                IsGenericParameter = true,
                                GenericParameterPosition = 0
                            }
                        }
                    }
                }
            },
            ReturnType = new TypeSignature { Name = "None" }
        };

        var sharedT0 = new TypeParameterType { Name = "T0" };
        var sharedTypeParams = new[] { sharedT0 };

        // Act
        var result = _discovery.ConvertToFunctionSymbol(sig, "List", sharedTypeParams);

        // Assert - T inside list[T] should be the shared instance
        var paramType = Assert.IsType<GenericType>(result.Parameters[0].Type);
        Assert.Equal("list", paramType.Name);
        Assert.Single(paramType.TypeArguments);
        Assert.True(ReferenceEquals(sharedT0, paramType.TypeArguments[0]),
            "Type argument inside list[T] should be the shared T0 instance");
    }

    [Fact]
    public void GetTypeByName_WithSharedTypeParams_AllMethodsShareSameInstances()
    {
        // Arrange
        var sharedT0 = new TypeParameterType { Name = "T0" };
        var sharedTypeParams = new[] { sharedT0 };

        // Act - get the List type with shared params
        var listType = _discovery.GetTypeByName("List", sharedTypeParams);

        // Assert - all methods that use T should reference the same T0
        Assert.NotNull(listType);
        Assert.NotEmpty(listType.Methods);

        // Find append method - its parameter should be T0
        var appendMethod = listType.Methods.FirstOrDefault(m => m.Name == "append");
        Assert.NotNull(appendMethod);
        Assert.Single(appendMethod.Parameters);
        Assert.True(ReferenceEquals(sharedT0, appendMethod.Parameters[0].Type),
            "append(item) parameter type should be shared T0 instance");

        // Find pop method - its return type should be T0
        var popMethod = listType.Methods.FirstOrDefault(m => m.Name == "pop");
        Assert.NotNull(popMethod);
        Assert.True(ReferenceEquals(sharedT0, popMethod.ReturnType),
            "pop() return type should be shared T0 instance");
    }

    [Fact]
    public void GetTypeByName_Dict_WithTwoSharedTypeParams()
    {
        // Arrange
        var sharedT0 = new TypeParameterType { Name = "T0" };
        var sharedT1 = new TypeParameterType { Name = "T1" };
        var sharedTypeParams = new[] { sharedT0, sharedT1 };

        // Act
        var dictType = _discovery.GetTypeByName("Dict", sharedTypeParams);

        // Assert
        Assert.NotNull(dictType);
        Assert.NotEmpty(dictType.Methods);

        // Find a method that uses both K and V type parameters
        // dict.get with 2 params: get(key: K, default: V) -> V
        var getMethods = dictType.Methods.Where(m => m.Name == "get").ToList();
        Assert.NotEmpty(getMethods);

        var getWithDefault = getMethods.FirstOrDefault(m => m.Parameters.Count == 2);
        Assert.NotNull(getWithDefault);
        Assert.True(ReferenceEquals(sharedT0, getWithDefault.Parameters[0].Type),
            "get(key) first param should be shared T0");
        Assert.True(ReferenceEquals(sharedT1, getWithDefault.Parameters[1].Type),
            "get(key, default) second param should be shared T1");
    }
}
