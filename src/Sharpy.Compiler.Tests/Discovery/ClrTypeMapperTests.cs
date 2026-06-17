extern alias SharpyRT;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Discovery;

enum TestEnum { A, B, C }

public class ClrTypeMapperTests
{
    private readonly ClrTypeMapper _mapper = new();

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
    public void MapArray_ToArrayType()
    {
        var result = _mapper.MapClrTypeToSemanticType(typeof(int[]));

        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("array", genericType.Name);
        Assert.Single(genericType.TypeArguments);
        Assert.Equal(SemanticType.Int, genericType.TypeArguments[0]);
    }

    [Fact]
    public void MapStringArray_ToArrayType()
    {
        var result = _mapper.MapClrTypeToSemanticType(typeof(string[]));

        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("array", genericType.Name);
        Assert.Single(genericType.TypeArguments);
        Assert.Equal(SemanticType.Str, genericType.TypeArguments[0]);
    }

    [Fact]
    public void MapByteArray_ToArrayType()
    {
        var result = _mapper.MapClrTypeToSemanticType(typeof(byte[]));

        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("array", genericType.Name);
        Assert.Single(genericType.TypeArguments);
        var elementType = Assert.IsType<BuiltinType>(genericType.TypeArguments[0]);
        Assert.Equal(typeof(byte), elementType.ClrType);
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
    public void MapSharpyDict_ToDictType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(SharpyRT::Sharpy.Dict<string, int>));

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
    public void MapNullableBytesStruct_ToNullableOfConcreteBytes()
    {
        // Arrange & Act — Nullable<Bytes> (hmac.new's `msg: bytes?` parameter, #890) must map
        // to a NullableType wrapping the concrete Bytes type so a plain `bytes` argument is
        // assignable to it during overload resolution.
        var result = _mapper.MapClrTypeToSemanticType(typeof(SharpyRT::Sharpy.Bytes?));

        // Assert
        Assert.IsType<NullableType>(result);
        var nullableType = (NullableType)result;
        var underlying = Assert.IsType<UserDefinedType>(nullableType.UnderlyingType);
        Assert.Equal("Bytes", underlying.Name);
        Assert.Equal(typeof(SharpyRT::Sharpy.Bytes), underlying.Symbol?.ClrType);
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
    public void MapSingleElementValueTuple_ToOneElementTupleType()
    {
        // Arrange & Act — System.ValueTuple<int> must round-trip to a 1-element TupleType
        // (#888): codegen maps tuple[int] -> ValueTuple<int>, and discovery must map it back.
        var result = _mapper.MapClrTypeToSemanticType(typeof(System.ValueTuple<int>));

        // Assert
        Assert.IsType<TupleType>(result);
        var tupleType = (TupleType)result;
        Assert.Single(tupleType.ElementTypes);
        Assert.Equal(SemanticType.Int, tupleType.ElementTypes[0]);
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
    public void MapIReadOnlyListOfString_ToListType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(IReadOnlyList<string>));

        // Assert
        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("list", genericType.Name);
        Assert.Single(genericType.TypeArguments);
        Assert.Equal(SemanticType.Str, genericType.TypeArguments[0]);
    }

    [Fact]
    public void MapIReadOnlyCollectionOfInt_ToListType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(IReadOnlyCollection<int>));

        // Assert
        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("list", genericType.Name);
        Assert.Single(genericType.TypeArguments);
        Assert.Equal(SemanticType.Int, genericType.TypeArguments[0]);
    }

    [Fact]
    public void MapIReadOnlyDictionaryOfStringInt_ToDictType()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(IReadOnlyDictionary<string, int>));

        // Assert
        Assert.IsType<GenericType>(result);
        var genericType = (GenericType)result;
        Assert.Equal("dict", genericType.Name);
        Assert.Equal(2, genericType.TypeArguments.Count);
        Assert.Equal(SemanticType.Str, genericType.TypeArguments[0]);
        Assert.Equal(SemanticType.Int, genericType.TypeArguments[1]);
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
    public void MapTask_ReturnsTaskTypeWithNullResult()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(System.Threading.Tasks.Task));

        // Assert
        Assert.IsType<TaskType>(result);
        var taskType = (TaskType)result;
        Assert.Null(taskType.ResultType);
    }

    [Fact]
    public void MapTaskOfInt_ReturnsTaskTypeWithIntResult()
    {
        // Arrange & Act
        var result = _mapper.MapClrTypeToSemanticType(typeof(System.Threading.Tasks.Task<int>));

        // Assert
        Assert.IsType<TaskType>(result);
        var taskType = (TaskType)result;
        Assert.Equal(SemanticType.Int, taskType.ResultType);
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

    [Fact]
    public void MapDisposableClrType_SynthesizesIDisposableInterface()
    {
        // Arrange & Act - MemoryStream implements IDisposable
        var result = _mapper.MapClrTypeToSemanticType(typeof(System.IO.MemoryStream));

        // Assert
        Assert.IsType<UserDefinedType>(result);
        var udt = (UserDefinedType)result;
        Assert.Equal("MemoryStream", udt.Name);
        Assert.NotNull(udt.Symbol);
        Assert.Equal(typeof(System.IO.MemoryStream), udt.Symbol!.ClrType);
        Assert.NotNull(udt.Symbol.Interfaces);
        Assert.Contains(udt.Symbol.Interfaces!, i => i.Definition.Name == "IDisposable");
    }

    [Fact]
    public void MapNonDisposableClrType_NoIDisposableInterface()
    {
        // Arrange & Act - Uri does not implement IDisposable
        var result = _mapper.MapClrTypeToSemanticType(typeof(System.Uri));

        // Assert
        Assert.IsType<UserDefinedType>(result);
        var udt = (UserDefinedType)result;
        Assert.Equal("Uri", udt.Name);
        Assert.NotNull(udt.Symbol);
        Assert.DoesNotContain(udt.Symbol!.Interfaces, i => i.Definition.Name == "IDisposable");
    }

    [Fact]
    public void BuildInterfaceList_GenericInterfaceCarriesResolvedTypeArguments()
    {
        // Arrange & Act - Uri implements IEquatable<Uri> (#827)
        var result = _mapper.MapClrTypeToSemanticType(typeof(System.Uri));

        // Assert
        var udt = Assert.IsType<UserDefinedType>(result);
        Assert.NotNull(udt.Symbol);
        var equatableRef = udt.Symbol!.Interfaces
            .SingleOrDefault(i => i.Definition.Name == "IEquatable");
        Assert.NotNull(equatableRef);
        var arg = Assert.Single(equatableRef.ResolvedTypeArguments);
        // Self-referential argument (Uri : IEquatable<Uri>) maps to a shallow reference
        var selfArg = Assert.IsType<UserDefinedType>(arg);
        Assert.Equal("Uri", selfArg.Name);
    }

    [Fact]
    public void BuildInterfaceList_SharesInterfaceDefinitionSymbolAcrossTypes()
    {
        // Arrange & Act - both implement IEquatable<T>
        var uri = (UserDefinedType)_mapper.MapClrTypeToSemanticType(typeof(System.Uri));
        var version = (UserDefinedType)_mapper.MapClrTypeToSemanticType(typeof(System.Version));

        // Assert - the IEquatable definition symbol is shared
        var uriEquatable = uri.Symbol!.Interfaces.Single(i => i.Definition.Name == "IEquatable");
        var versionEquatable = version.Symbol!.Interfaces.Single(i => i.Definition.Name == "IEquatable");
        Assert.Same(uriEquatable.Definition, versionEquatable.Definition);
        Assert.Single(uriEquatable.Definition.TypeParameters);
        Assert.Equal("T0", uriEquatable.Definition.TypeParameters[0].Name);
    }

    [Fact]
    public void BuildInterfaceList_FiltersNonGenericDuplicateOfGenericForm()
    {
        // Arrange & Act - Version implements both IComparable and IComparable<Version>
        var result = _mapper.MapClrTypeToSemanticType(typeof(System.Version));

        // Assert - only the generic form survives
        var udt = Assert.IsType<UserDefinedType>(result);
        var comparableRefs = udt.Symbol!.Interfaces
            .Where(i => i.Definition.Name == "IComparable")
            .ToList();
        var singleRef = Assert.Single(comparableRefs);
        Assert.NotEmpty(singleRef.ResolvedTypeArguments);
    }

    [Fact]
    public void MapTextFile_SynthesizesIDisposableInterface()
    {
        // Arrange & Act - TextFile implements IDisposable (previously hardcoded)
        var result = _mapper.MapClrTypeToSemanticType(typeof(SharpyRT::Sharpy.TextFile));

        // Assert
        Assert.IsType<UserDefinedType>(result);
        var udt = (UserDefinedType)result;
        Assert.Equal("TextFile", udt.Name);
        Assert.NotNull(udt.Symbol);
        Assert.Equal(typeof(SharpyRT::Sharpy.TextFile), udt.Symbol!.ClrType);
        Assert.NotNull(udt.Symbol.Interfaces);
        Assert.Contains(udt.Symbol.Interfaces!, i => i.Definition.Name == "IDisposable");
    }
}
