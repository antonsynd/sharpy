using System.Collections.Immutable;
using Xunit;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeUtilsTests
{
    [Theory]
    [InlineData("int")]
    [InlineData("long")]
    [InlineData("float")]
    [InlineData("double")]
    public void IsNumericOrUnknown_ForNumericTypes_ReturnsTrue(string typeName)
    {
        var type = GetBuiltinType(typeName);
        Assert.True(TypeUtils.IsNumericOrUnknown(type));
    }

    [Fact]
    public void IsNumericOrUnknown_ForUnknownType_ReturnsTrue()
    {
        Assert.True(TypeUtils.IsNumericOrUnknown(SemanticType.Unknown));
    }

    [Fact]
    public void IsNumericOrUnknown_ForNonNumericType_ReturnsFalse()
    {
        Assert.False(TypeUtils.IsNumericOrUnknown(SemanticType.Str));
    }

    [Theory]
    [InlineData("int", true)]
    [InlineData("long", true)]
    [InlineData("float", true)]
    [InlineData("double", true)]
    public void IsNumeric_ForNumericTypes_ReturnsTrue(string typeName, bool expected)
    {
        var type = GetBuiltinType(typeName);
        Assert.Equal(expected, TypeUtils.IsNumeric(type));
    }

    [Theory]
    [InlineData("str", false)]
    [InlineData("bool", false)]
    public void IsNumeric_ForNonNumericTypes_ReturnsFalse(string typeName, bool expected)
    {
        var type = GetBuiltinType(typeName);
        Assert.Equal(expected, TypeUtils.IsNumeric(type));
    }

    [Theory]
    [InlineData("int", true)]
    [InlineData("long", true)]
    public void IsInteger_ForIntegerTypes_ReturnsTrue(string typeName, bool expected)
    {
        var type = GetBuiltinType(typeName);
        Assert.Equal(expected, TypeUtils.IsInteger(type));
    }

    [Theory]
    [InlineData("float", false)]
    [InlineData("double", false)]
    public void IsInteger_ForFloatTypes_ReturnsFalse(string typeName, bool expected)
    {
        var type = GetBuiltinType(typeName);
        Assert.Equal(expected, TypeUtils.IsInteger(type));
    }

    [Fact]
    public void IsFloatingPoint_ForFloatAndDouble_ReturnsTrue()
    {
        Assert.True(TypeUtils.IsFloatingPoint(SemanticType.Float));
        Assert.True(TypeUtils.IsFloatingPoint(SemanticType.Double));
    }

    [Fact]
    public void IsFloatingPoint_ForInt_ReturnsFalse()
    {
        Assert.False(TypeUtils.IsFloatingPoint(SemanticType.Int));
    }

    [Fact]
    public void IsString_ForStrType_ReturnsTrue()
    {
        Assert.True(TypeUtils.IsString(SemanticType.Str));
    }

    [Fact]
    public void IsString_ForIntType_ReturnsFalse()
    {
        Assert.False(TypeUtils.IsString(SemanticType.Int));
    }

    [Fact]
    public void IsBool_ForBoolType_ReturnsTrue()
    {
        Assert.True(TypeUtils.IsBool(SemanticType.Bool));
    }

    [Fact]
    public void IsCollection_ForListType_ReturnsTrue()
    {
        var listType = new GenericType { Name = "list", TypeArguments = [SemanticType.Int] };
        Assert.True(TypeUtils.IsCollection(listType));
    }

    [Fact]
    public void IsCollection_ForDictType_ReturnsTrue()
    {
        var dictType = new GenericType { Name = "dict", TypeArguments = [SemanticType.Str, SemanticType.Int] };
        Assert.True(TypeUtils.IsCollection(dictType));
    }

    [Fact]
    public void IsCollection_ForSetType_ReturnsTrue()
    {
        var setType = new GenericType { Name = "set", TypeArguments = [SemanticType.Int] };
        Assert.True(TypeUtils.IsCollection(setType));
    }

    [Fact]
    public void IsList_ForListType_ReturnsTrue()
    {
        var listType = new GenericType { Name = "list", TypeArguments = [SemanticType.Int] };
        Assert.True(TypeUtils.IsList(listType));
    }

    [Fact]
    public void IsDict_ForDictType_ReturnsTrue()
    {
        var dictType = new GenericType { Name = "dict", TypeArguments = [SemanticType.Str, SemanticType.Int] };
        Assert.True(TypeUtils.IsDict(dictType));
    }

    [Fact]
    public void IsSet_ForSetType_ReturnsTrue()
    {
        var setType = new GenericType { Name = "set", TypeArguments = [SemanticType.Int] };
        Assert.True(TypeUtils.IsSet(setType));
    }

    [Fact]
    public void IsTuple_ForTupleType_ReturnsTrue()
    {
        var tupleType = new TupleType { ElementTypes = [SemanticType.Int, SemanticType.Str] };
        Assert.True(TypeUtils.IsTuple(tupleType));
    }

    [Fact]
    public void GetElementType_ForList_ReturnsElementType()
    {
        var listType = new GenericType { Name = "list", TypeArguments = [SemanticType.Int] };
        var elementType = TypeUtils.GetElementType(listType);
        Assert.Equal(SemanticType.Int, elementType);
    }

    [Fact]
    public void GetElementType_ForDict_ReturnsValueType()
    {
        var dictType = new GenericType { Name = "dict", TypeArguments = [SemanticType.Str, SemanticType.Int] };
        var valueType = TypeUtils.GetElementType(dictType);
        Assert.Equal(SemanticType.Int, valueType);
    }

    [Fact]
    public void GetKeyType_ForDict_ReturnsKeyType()
    {
        var dictType = new GenericType { Name = "dict", TypeArguments = [SemanticType.Str, SemanticType.Int] };
        var keyType = TypeUtils.GetKeyType(dictType);
        Assert.Equal(SemanticType.Str, keyType);
    }

    [Fact]
    public void UnwrapAllNullable_SingleLayer_Unwraps()
    {
        var nullable = new NullableType { UnderlyingType = SemanticType.Int };
        var unwrapped = TypeUtils.UnwrapAllNullable(nullable);
        Assert.Equal(SemanticType.Int, unwrapped);
    }

    [Fact]
    public void UnwrapAllNullable_MultipleLayers_UnwrapsAll()
    {
        var doubleNullable = new NullableType
        {
            UnderlyingType = new NullableType
            {
                UnderlyingType = SemanticType.Int
            }
        };

        var unwrapped = TypeUtils.UnwrapAllNullable(doubleNullable);
        Assert.Equal(SemanticType.Int, unwrapped);
    }

    [Fact]
    public void UnwrapAllNullable_NonNullable_ReturnsSameType()
    {
        var unwrapped = TypeUtils.UnwrapAllNullable(SemanticType.Int);
        Assert.Equal(SemanticType.Int, unwrapped);
    }

    [Fact]
    public void AreEquivalent_SameTypes_ReturnsTrue()
    {
        Assert.True(TypeUtils.AreEquivalent(SemanticType.Int, SemanticType.Int));
    }

    [Fact]
    public void AreEquivalent_DifferentTypes_ReturnsFalse()
    {
        Assert.False(TypeUtils.AreEquivalent(SemanticType.Int, SemanticType.Str));
    }

    [Fact]
    public void GetCommonType_SameTypes_ReturnsSameType()
    {
        var common = TypeUtils.GetCommonType(SemanticType.Int, SemanticType.Int);
        Assert.Equal(SemanticType.Int, common);
    }

    [Fact]
    public void GetCommonType_IntAndLong_ReturnsLong()
    {
        var common = TypeUtils.GetCommonType(SemanticType.Int, SemanticType.Long);
        Assert.Equal(SemanticType.Long, common);
    }

    [Fact]
    public void GetCommonType_IntAndFloat_ReturnsDouble()
    {
        // Note: Sharpy's float maps to C# double
        var common = TypeUtils.GetCommonType(SemanticType.Int, SemanticType.Float);
        // Float in Sharpy is double, so GetCommonType returns Double
        Assert.Equal(SemanticType.Double, common);
    }

    // ========================================
    // TupleType Equality Tests
    // ========================================

    [Fact]
    public void TupleType_Equals_SameNamesAndTypes_ReturnsTrue()
    {
        var tuple1 = new TupleType
        {
            ElementTypes = [SemanticType.Float, SemanticType.Float],
            ElementNames = ImmutableArray.Create<string?>("x", "y")
        };
        var tuple2 = new TupleType
        {
            ElementTypes = [SemanticType.Float, SemanticType.Float],
            ElementNames = ImmutableArray.Create<string?>("x", "y")
        };
        Assert.Equal(tuple1, tuple2);
    }

    [Fact]
    public void TupleType_Equals_DifferentNamesSameTypes_ReturnsFalse()
    {
        var tuple1 = new TupleType
        {
            ElementTypes = [SemanticType.Float, SemanticType.Float],
            ElementNames = ImmutableArray.Create<string?>("x", "y")
        };
        var tuple2 = new TupleType
        {
            ElementTypes = [SemanticType.Float, SemanticType.Float],
            ElementNames = ImmutableArray.Create<string?>("a", "b")
        };
        Assert.NotEqual(tuple1, tuple2);
    }

    [Fact]
    public void TupleType_Equals_NamedVsUnnamed_ReturnsFalse()
    {
        var named = new TupleType
        {
            ElementTypes = [SemanticType.Int, SemanticType.Str],
            ElementNames = ImmutableArray.Create<string?>("x", "y")
        };
        var unnamed = new TupleType
        {
            ElementTypes = [SemanticType.Int, SemanticType.Str]
        };
        Assert.NotEqual(named, unnamed);
    }

    [Fact]
    public void TupleType_GetHashCode_ConsistentWithEquals()
    {
        var tuple1 = new TupleType
        {
            ElementTypes = [SemanticType.Int, SemanticType.Str],
            ElementNames = ImmutableArray.Create<string?>("a", "b")
        };
        var tuple2 = new TupleType
        {
            ElementTypes = [SemanticType.Int, SemanticType.Str],
            ElementNames = ImmutableArray.Create<string?>("a", "b")
        };
        Assert.Equal(tuple1.GetHashCode(), tuple2.GetHashCode());
    }

    // ========================================
    // Placeholder Type Tests (v0.2.x features)
    // ========================================

    [Fact]
    public void UnionType_GetDisplayName_ReturnsName()
    {
        var unionType = new UnionType { Name = "Result" };
        Assert.Equal("Result", unionType.GetDisplayName());
    }

    [Fact]
    public void UnionType_IsAssignableTo_SameUnion_ReturnsTrue()
    {
        var union1 = new UnionType { Name = "Result" };
        var union2 = new UnionType { Name = "Result" };
        Assert.True(union1.IsAssignableTo(union2));
    }

    [Fact]
    public void UnionType_IsAssignableTo_DifferentUnion_ReturnsFalse()
    {
        var union1 = new UnionType { Name = "Result" };
        var union2 = new UnionType { Name = "Option" };
        Assert.False(union1.IsAssignableTo(union2));
    }

    [Fact]
    public void UnionType_IsAssignableTo_Object_ReturnsTrue()
    {
        var unionType = new UnionType { Name = "Result" };
        Assert.True(unionType.IsAssignableTo(SemanticType.Object));
    }

    [Fact]
    public void UnionType_CaseTypes_CanBePopulated()
    {
        var unionType = new UnionType
        {
            Name = "Result",
            CaseTypes = [SemanticType.Int, SemanticType.Str]
        };
        Assert.Equal(2, unionType.CaseTypes.Count);
    }

    [Fact]
    public void TaskType_GetDisplayName_VoidTask_ReturnsTask()
    {
        var taskType = new TaskType { ResultType = null };
        Assert.Equal("Task", taskType.GetDisplayName());
    }

    [Fact]
    public void TaskType_GetDisplayName_WithResultType_ReturnsTaskOfT()
    {
        var taskType = new TaskType { ResultType = SemanticType.Int };
        Assert.Equal("Task[int]", taskType.GetDisplayName());
    }

    [Fact]
    public void TaskType_ClrType_VoidTask_ReturnsTaskType()
    {
        var taskType = new TaskType { ResultType = null };
        Assert.Equal(typeof(System.Threading.Tasks.Task), taskType.ClrType);
    }

    [Fact]
    public void TaskType_ClrType_GenericTask_ReturnsNull()
    {
        // Generic Task<T> needs runtime resolution
        var taskType = new TaskType { ResultType = SemanticType.Int };
        Assert.Null(taskType.ClrType);
    }

    [Fact]
    public void TaskType_IsAssignableTo_Object_ReturnsTrue()
    {
        var taskType = new TaskType { ResultType = SemanticType.Int };
        Assert.True(taskType.IsAssignableTo(SemanticType.Object));
    }

    private static SemanticType GetBuiltinType(string name)
    {
        return name switch
        {
            "int" => SemanticType.Int,
            "long" => SemanticType.Long,
            "float" => SemanticType.Float,
            "double" => SemanticType.Double,
            "str" => SemanticType.Str,
            "bool" => SemanticType.Bool,
            _ => SemanticType.Unknown
        };
    }
}
