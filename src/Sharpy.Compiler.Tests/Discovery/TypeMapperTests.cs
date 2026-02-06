extern alias SharpyRT;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

enum TestEnum { A, B, C }

public class TypeMapperTests
{
    private readonly TypeMapper _mapper = new();

    [Fact]
    public void MapPrimitiveTypes()
    {
        Assert.Equal(SemanticType.Int, _mapper.MapClrTypeToSemanticType(typeof(int)));
        Assert.Equal(SemanticType.Long, _mapper.MapClrTypeToSemanticType(typeof(long)));
        Assert.Equal(SemanticType.Float32, _mapper.MapClrTypeToSemanticType(typeof(float)));  // C# float -> Sharpy float32
        Assert.Equal(SemanticType.Double, _mapper.MapClrTypeToSemanticType(typeof(double)));
        Assert.Equal(SemanticType.Bool, _mapper.MapClrTypeToSemanticType(typeof(bool)));
        Assert.Equal(SemanticType.Str, _mapper.MapClrTypeToSemanticType(typeof(string)));
        Assert.Equal(SemanticType.Void, _mapper.MapClrTypeToSemanticType(typeof(void)));
        Assert.Equal(SemanticType.Object, _mapper.MapClrTypeToSemanticType(typeof(object)));
    }

    [Fact]
    public void MapArray_ToListType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(int[]));

        // Assert
        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("list", genericType.Name);
        Assert.Single(genericType.TypeArguments);
        Assert.Equal(SemanticType.Int, genericType.TypeArguments[0]);
    }

    [Fact]
    public void MapListOfT_ToListType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(List<string>));

        // Assert
        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("list", genericType.Name);
        Assert.Single(genericType.TypeArguments);
        Assert.Equal(SemanticType.Str, genericType.TypeArguments[0]);
    }

    [Fact]
    public void MapDictionary_ToDictType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(Dictionary<string, int>));

        // Assert
        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("dict", genericType.Name);
        Assert.Equal(2, genericType.TypeArguments.Count);
        Assert.Equal(SemanticType.Str, genericType.TypeArguments[0]);
        Assert.Equal(SemanticType.Int, genericType.TypeArguments[1]);
    }

    [Fact]
    public void MapHashSet_ToSetType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(HashSet<double>));

        // Assert
        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("set", genericType.Name);
        Assert.Single(genericType.TypeArguments);
        Assert.Equal(SemanticType.Double, genericType.TypeArguments[0]);
    }

    [Fact]
    public void MapNullableValueType_ToNullableType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(int?));

        // Assert
        Assert.IsType<NullableType>(result);
        var nullableType = (NullableType)result;
        Assert.Equal(SemanticType.Int, nullableType.UnderlyingType);
    }

    [Fact]
    public void MapTuple_ToTupleType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof((int, string)));

        // Assert
        Assert.IsType<TupleType>(result);
        var tupleType = (TupleType)result;
        Assert.Equal(2, tupleType.ElementTypes.Count);
        Assert.Equal(SemanticType.Int, tupleType.ElementTypes[0]);
        Assert.Equal(SemanticType.Str, tupleType.ElementTypes[1]);
    }

    [Fact]
    public void MapEnum_ToInt()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(TestEnum));

        // Assert
        Assert.Equal(SemanticType.Int, result);
    }

    [Fact]
    public void MapIEnumerable_ToListType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(IEnumerable<bool>));

        // Assert
        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("list", genericType.Name);
        Assert.Single(genericType.TypeArguments);
        Assert.Equal(SemanticType.Bool, genericType.TypeArguments[0]);
    }

    [Fact]
    public void CachesResults()
    {
        // Arrange & Act
        var result1 = _mapper.MapClrTypeToSemanticType(typeof(int));
        var result2 = _mapper.MapClrTypeToSemanticType(typeof(int));

        // Assert
        Assert.Same(result1, result2);
    }

    [Fact]
    public void MapRangeIterator_ToBuiltinType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(SharpyRT::Sharpy.RangeIterator));

        // Assert
        Assert.IsType<BuiltinType>(result);
        var builtinType = (BuiltinType)result;
        Assert.Equal("RangeIterator", builtinType.Name);
        Assert.Equal(typeof(SharpyRT::Sharpy.RangeIterator), builtinType.ClrType);
    }
}
