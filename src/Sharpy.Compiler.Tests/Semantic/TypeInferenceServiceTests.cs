using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

// Resolve ambiguous types
using TupleType = Sharpy.Compiler.Semantic.TupleType;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for TypeInferenceService - the type inference component extracted from validators.
/// </summary>
public class TypeInferenceServiceTests
{
    private readonly TypeInferenceService _service;

    public TypeInferenceServiceTests()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        _service = new TypeInferenceService(symbolTable);
    }

    #region Binary Arithmetic Operations

    [Fact]
    public void InferBinaryOpType_IntPlusInt_ReturnsInt()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferBinaryOpType_IntMinusInt_ReturnsInt()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Subtract, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferBinaryOpType_IntTimesInt_ReturnsInt()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Multiply, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferBinaryOpType_IntDivideInt_ReturnsDouble()
    {
        // Python semantics: / always returns float
        var result = _service.InferBinaryOpType(BinaryOperator.Divide, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void InferBinaryOpType_IntFloorDivideInt_ReturnsInt()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.FloorDivide, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferBinaryOpType_IntModuloInt_ReturnsInt()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Modulo, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferBinaryOpType_IntPowerInt_ReturnsDouble()
    {
        // Math.Pow returns double
        var result = _service.InferBinaryOpType(BinaryOperator.Power, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Double);
    }

    #endregion

    #region Binary Operations with Type Promotion

    [Fact]
    public void InferBinaryOpType_IntPlusLong_ReturnsLong()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Int, SemanticType.Long);
        result.Should().Be(SemanticType.Long);
    }

    [Fact]
    public void InferBinaryOpType_IntPlusDouble_ReturnsDouble()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Int, SemanticType.Double);
        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void InferBinaryOpType_DoublePlusFloat32_ReturnsDouble()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Double, SemanticType.Float32);
        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void InferBinaryOpType_Float32PlusFloat32_ReturnsFloat32()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Float32, SemanticType.Float32);
        result.Should().Be(SemanticType.Float32);
    }

    #endregion

    #region Comparison Operations

    [Fact]
    public void InferBinaryOpType_IntLessThanInt_ReturnsBool()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.LessThan, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void InferBinaryOpType_IntEqualInt_ReturnsBool()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Equal, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void InferBinaryOpType_IntNotEqualDouble_ReturnsBool()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.NotEqual, SemanticType.Int, SemanticType.Double);
        result.Should().Be(SemanticType.Bool);
    }

    #endregion

    #region Logical Operations

    [Fact]
    public void InferBinaryOpType_BoolAndBool_ReturnsBool()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.And, SemanticType.Bool, SemanticType.Bool);
        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void InferBinaryOpType_IntOrInt_ReturnsBool()
    {
        // Logical operators always return bool in Sharpy
        var result = _service.InferBinaryOpType(BinaryOperator.Or, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void InferBinaryOpType_IsOperator_ReturnsBool()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Is, SemanticType.Object, SemanticType.Object);
        result.Should().Be(SemanticType.Bool);
    }

    #endregion

    #region Bitwise Operations

    [Fact]
    public void InferBinaryOpType_IntBitwiseAndInt_ReturnsInt()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.BitwiseAnd, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferBinaryOpType_LongBitwiseOrLong_ReturnsLong()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.BitwiseOr, SemanticType.Long, SemanticType.Long);
        result.Should().Be(SemanticType.Long);
    }

    [Fact]
    public void InferBinaryOpType_IntLeftShift_ReturnsInt()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.LeftShift, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferBinaryOpType_BitwiseOnDouble_ReturnsNull()
    {
        // Bitwise operations not supported on floating point
        var result = _service.InferBinaryOpType(BinaryOperator.BitwiseAnd, SemanticType.Double, SemanticType.Int);
        result.Should().BeNull();
    }

    #endregion

    #region String Operations

    [Fact]
    public void InferBinaryOpType_StringPlusString_ReturnsString()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Str, SemanticType.Str);
        result.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void InferBinaryOpType_StringLessThanString_ReturnsBool()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.LessThan, SemanticType.Str, SemanticType.Str);
        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void InferBinaryOpType_StringTimesInt_ReturnsString()
    {
        var result = _service.InferBinaryOpType(BinaryOperator.Multiply, SemanticType.Str, SemanticType.Int);
        result.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void InferBinaryOpType_StringMinusString_ReturnsNull()
    {
        // String subtraction not supported
        var result = _service.InferBinaryOpType(BinaryOperator.Subtract, SemanticType.Str, SemanticType.Str);
        result.Should().BeNull();
    }

    [Fact]
    public void InferBinaryOpType_StringPlusInt_ReturnsNull()
    {
        // No implicit conversion
        var result = _service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Str, SemanticType.Int);
        result.Should().BeNull();
    }

    #endregion

    #region List Operations

    [Fact]
    public void InferBinaryOpType_ListPlusList_ReturnsList()
    {
        var listInt = new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
        var result = _service.InferBinaryOpType(BinaryOperator.Add, listInt, listInt);
        result.Should().BeEquivalentTo(listInt);
    }

    [Fact]
    public void InferBinaryOpType_ListEqualList_ReturnsBool()
    {
        var listInt = new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
        var result = _service.InferBinaryOpType(BinaryOperator.Equal, listInt, listInt);
        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void InferBinaryOpType_DifferentListTypes_ReturnsNull()
    {
        var listInt = new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
        var listStr = new GenericType { Name = "list", TypeArguments = { SemanticType.Str } };
        var result = _service.InferBinaryOpType(BinaryOperator.Add, listInt, listStr);
        result.Should().BeNull();
    }

    #endregion

    #region Null Coalescing

    [Fact]
    public void InferBinaryOpType_NullCoalesce_ValidTypes_ReturnsNonNullable()
    {
        var nullableInt = new NullableType { UnderlyingType = SemanticType.Int };
        var result = _service.InferBinaryOpType(BinaryOperator.NullCoalesce, nullableInt, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferBinaryOpType_NullCoalesce_NonNullableLeft_ReturnsNull()
    {
        // Left must be nullable for null coalescing
        var result = _service.InferBinaryOpType(BinaryOperator.NullCoalesce, SemanticType.Int, SemanticType.Int);
        result.Should().BeNull();
    }

    #endregion

    #region Unary Operations

    [Fact]
    public void InferUnaryOpType_NotAnything_ReturnsBool()
    {
        var result = _service.InferUnaryOpType(UnaryOperator.Not, SemanticType.Int);
        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void InferUnaryOpType_NegateInt_ReturnsInt()
    {
        var result = _service.InferUnaryOpType(UnaryOperator.Minus, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferUnaryOpType_PositiveDouble_ReturnsDouble()
    {
        var result = _service.InferUnaryOpType(UnaryOperator.Plus, SemanticType.Double);
        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void InferUnaryOpType_BitwiseNotInt_ReturnsInt()
    {
        var result = _service.InferUnaryOpType(UnaryOperator.BitwiseNot, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferUnaryOpType_BitwiseNotDouble_ReturnsNull()
    {
        // Bitwise not not supported on floating point
        var result = _service.InferUnaryOpType(UnaryOperator.BitwiseNot, SemanticType.Double);
        result.Should().BeNull();
    }

    [Fact]
    public void InferUnaryOpType_NegateString_ReturnsNull()
    {
        var result = _service.InferUnaryOpType(UnaryOperator.Minus, SemanticType.Str);
        result.Should().BeNull();
    }

    #endregion

    #region Protocol Inference - Iteration

    [Fact]
    public void InferIterableElementType_ListInt_ReturnsInt()
    {
        var listInt = new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
        var result = _service.InferIterableElementType(listInt);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferIterableElementType_DictStrInt_ReturnsStr()
    {
        // Dict iteration yields keys
        var dictStrInt = new GenericType { Name = "dict", TypeArguments = { SemanticType.Str, SemanticType.Int } };
        var result = _service.InferIterableElementType(dictStrInt);
        result.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void InferIterableElementType_String_ReturnsString()
    {
        var result = _service.InferIterableElementType(SemanticType.Str);
        result.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void InferIterableElementType_TupleIntStr_ReturnsFirst()
    {
        // Simplified: returns first element type
        var tuple = new TupleType { ElementTypes = { SemanticType.Int, SemanticType.Str } };
        var result = _service.InferIterableElementType(tuple);
        result.Should().Be(SemanticType.Int);
    }

    #endregion

    #region Protocol Inference - Index Access

    [Fact]
    public void InferIndexAccessType_ListInt_ReturnsInt()
    {
        var listInt = new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
        var result = _service.InferIndexAccessType(listInt, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferIndexAccessType_DictStrInt_ReturnsInt()
    {
        // Dict indexing returns value type
        var dictStrInt = new GenericType { Name = "dict", TypeArguments = { SemanticType.Str, SemanticType.Int } };
        var result = _service.InferIndexAccessType(dictStrInt, SemanticType.Str);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferIndexAccessType_String_ReturnsString()
    {
        var result = _service.InferIndexAccessType(SemanticType.Str, SemanticType.Int);
        result.Should().Be(SemanticType.Str);
    }

    #endregion

    #region Protocol Inference - Other

    [Fact]
    public void InferMembershipType_Always_ReturnsBool()
    {
        var listInt = new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
        var result = _service.InferMembershipType(listInt, SemanticType.Int);
        result.Should().Be(SemanticType.Bool);
    }

    [Fact]
    public void InferLenType_Always_ReturnsInt()
    {
        var listInt = new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
        var result = _service.InferLenType(listInt);
        result.Should().Be(SemanticType.Int);
    }

    #endregion

    #region Caching

    [Fact]
    public void InferBinaryOpType_SameInputs_ReturnsCachedResult()
    {
        // First call
        var result1 = _service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Int, SemanticType.Int);

        // Second call should use cache
        var result2 = _service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Int, SemanticType.Int);

        result1.Should().Be(result2);
        result1.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferUnaryOpType_SameInputs_ReturnsCachedResult()
    {
        // First call
        var result1 = _service.InferUnaryOpType(UnaryOperator.Minus, SemanticType.Int);

        // Second call should use cache
        var result2 = _service.InferUnaryOpType(UnaryOperator.Minus, SemanticType.Int);

        result1.Should().Be(result2);
        result1.Should().Be(SemanticType.Int);
    }

    #endregion

    #region Augmented Assignment Inference

    [Fact]
    public void InferAugmentedAssignmentType_SimpleAssign_ReturnsValueType()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.Assign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferAugmentedAssignmentType_PlusAssignIntInt_ReturnsInt()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.PlusAssign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferAugmentedAssignmentType_MinusAssignLongLong_ReturnsLong()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.MinusAssign, SemanticType.Long, SemanticType.Long);
        result.Should().Be(SemanticType.Long);
    }

    [Fact]
    public void InferAugmentedAssignmentType_StarAssignDoubleInt_ReturnsDouble()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.StarAssign, SemanticType.Double, SemanticType.Int);
        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void InferAugmentedAssignmentType_SlashAssignIntInt_ReturnsDouble()
    {
        // Division always returns double (Python semantics)
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.SlashAssign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void InferAugmentedAssignmentType_DoubleSlashAssignIntInt_ReturnsInt()
    {
        // Floor division preserves integer type
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.DoubleSlashAssign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferAugmentedAssignmentType_PercentAssignIntInt_ReturnsInt()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.PercentAssign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferAugmentedAssignmentType_PowerAssignIntInt_ReturnsDouble()
    {
        // Power always returns double (Math.Pow)
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.PowerAssign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Double);
    }

    [Fact]
    public void InferAugmentedAssignmentType_AndAssignIntInt_ReturnsInt()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.AndAssign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferAugmentedAssignmentType_OrAssignLongLong_ReturnsLong()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.OrAssign, SemanticType.Long, SemanticType.Long);
        result.Should().Be(SemanticType.Long);
    }

    [Fact]
    public void InferAugmentedAssignmentType_XorAssignIntInt_ReturnsInt()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.XorAssign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferAugmentedAssignmentType_LeftShiftAssignIntInt_ReturnsInt()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.LeftShiftAssign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferAugmentedAssignmentType_RightShiftAssignIntInt_ReturnsInt()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.RightShiftAssign, SemanticType.Int, SemanticType.Int);
        result.Should().Be(SemanticType.Int);
    }

    [Fact]
    public void InferAugmentedAssignmentType_NullCoalesceAssign_ValidTypes_ReturnsTargetType()
    {
        var nullableInt = new NullableType { UnderlyingType = SemanticType.Int };
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.NullCoalesceAssign, nullableInt, SemanticType.Int);
        result.Should().Be(nullableInt);
    }

    [Fact]
    public void InferAugmentedAssignmentType_NullCoalesceAssign_NonNullableTarget_ReturnsNull()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.NullCoalesceAssign, SemanticType.Int, SemanticType.Int);
        result.Should().BeNull();
    }

    [Fact]
    public void InferAugmentedAssignmentType_PlusAssignStringString_ReturnsString()
    {
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.PlusAssign, SemanticType.Str, SemanticType.Str);
        result.Should().Be(SemanticType.Str);
    }

    [Fact]
    public void InferAugmentedAssignmentType_PlusAssignListList_ReturnsList()
    {
        var listInt = new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.PlusAssign, listInt, listInt);
        result.Should().BeEquivalentTo(listInt);
    }

    [Fact]
    public void InferAugmentedAssignmentType_UnsupportedOperation_ReturnsNull()
    {
        // String doesn't support -=
        var result = _service.InferAugmentedAssignmentType(
            AssignmentOperator.MinusAssign, SemanticType.Str, SemanticType.Str);
        result.Should().BeNull();
    }

    #endregion
}
