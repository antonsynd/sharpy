using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Semantic;

public class OperatorRegistryTests
{
    #region IsOperatorDunder Tests

    [Fact]
    public void IsOperatorDunder_RecognizesArithmeticOperators()
    {
        OperatorRegistry.IsOperatorDunder("__add__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__sub__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__mul__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__div__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__mod__").Should().BeTrue();
    }

    [Fact]
    public void IsOperatorDunder_RecognizesBitwiseOperators()
    {
        OperatorRegistry.IsOperatorDunder("__and__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__or__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__xor__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__lshift__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__rshift__").Should().BeTrue();
    }

    [Fact]
    public void IsOperatorDunder_RejectsInPlaceOperators()
    {
        // In-place operators don't exist in Sharpy per spec
        OperatorRegistry.IsOperatorDunder("__iadd__").Should().BeFalse();
        OperatorRegistry.IsOperatorDunder("__isub__").Should().BeFalse();
        OperatorRegistry.IsOperatorDunder("__imul__").Should().BeFalse();
    }

    [Fact]
    public void IsOperatorDunder_RecognizesComparisonOperators()
    {
        OperatorRegistry.IsOperatorDunder("__eq__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__ne__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__lt__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__le__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__gt__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__ge__").Should().BeTrue();
    }

    [Fact]
    public void IsOperatorDunder_RecognizesUnaryOperators()
    {
        OperatorRegistry.IsOperatorDunder("__pos__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__neg__").Should().BeTrue();
        OperatorRegistry.IsOperatorDunder("__invert__").Should().BeTrue();
    }

    [Fact]
    public void IsOperatorDunder_RejectsNonOperatorDunders()
    {
        OperatorRegistry.IsOperatorDunder("__init__").Should().BeFalse();
        OperatorRegistry.IsOperatorDunder("__str__").Should().BeFalse();
        OperatorRegistry.IsOperatorDunder("__iter__").Should().BeFalse();
        OperatorRegistry.IsOperatorDunder("__hash__").Should().BeFalse();
        OperatorRegistry.IsOperatorDunder("__len__").Should().BeFalse();
        OperatorRegistry.IsOperatorDunder("__getitem__").Should().BeFalse();
    }

    [Fact]
    public void IsOperatorDunder_RejectsRegularMethods()
    {
        OperatorRegistry.IsOperatorDunder("regular_method").Should().BeFalse();
        OperatorRegistry.IsOperatorDunder("_private_method").Should().BeFalse();
    }

    #endregion

    #region GetOperatorKind Tests

    [Theory]
    [InlineData("__add__", OperatorKind.BinaryArithmetic)]
    [InlineData("__sub__", OperatorKind.BinaryArithmetic)]
    [InlineData("__and__", OperatorKind.BinaryBitwise)]
    [InlineData("__or__", OperatorKind.BinaryBitwise)]
    [InlineData("__eq__", OperatorKind.Comparison)]
    [InlineData("__lt__", OperatorKind.Comparison)]
    [InlineData("__neg__", OperatorKind.Unary)]
    [InlineData("__invert__", OperatorKind.Unary)]
    public void GetOperatorKind_ReturnsCorrectKind(string methodName, OperatorKind expectedKind)
    {
        OperatorRegistry.GetOperatorKind(methodName).Should().Be(expectedKind);
    }

    [Fact]
    public void GetOperatorKind_ReturnsNullForNonOperator()
    {
        OperatorRegistry.GetOperatorKind("__init__").Should().BeNull();
        OperatorRegistry.GetOperatorKind("regular_method").Should().BeNull();
    }

    #endregion

    #region GetExpectedParamCount Tests

    [Theory]
    [InlineData("__neg__", 1)]
    [InlineData("__pos__", 1)]
    [InlineData("__invert__", 1)]
    [InlineData("__add__", 2)]
    [InlineData("__eq__", 2)]
    [InlineData("__and__", 2)]
    public void GetExpectedParamCount_ReturnsCorrectCount(string methodName, int expectedCount)
    {
        OperatorRegistry.GetExpectedParamCount(methodName).Should().Be(expectedCount);
    }

    [Fact]
    public void GetExpectedParamCount_ReturnsNullForNonOperator()
    {
        OperatorRegistry.GetExpectedParamCount("__init__").Should().BeNull();
    }

    #endregion

    #region Classification Tests

    [Theory]
    [InlineData("__neg__")]
    [InlineData("__pos__")]
    [InlineData("__invert__")]
    public void IsUnaryOperator_ReturnsTrueForUnaryOps(string methodName)
    {
        OperatorRegistry.IsUnaryOperator(methodName).Should().BeTrue();
    }

    [Fact]
    public void IsUnaryOperator_ReturnsFalseForBinaryOps()
    {
        OperatorRegistry.IsUnaryOperator("__add__").Should().BeFalse();
        OperatorRegistry.IsUnaryOperator("__eq__").Should().BeFalse();
    }

    [Theory]
    [InlineData("__eq__")]
    [InlineData("__ne__")]
    [InlineData("__lt__")]
    [InlineData("__le__")]
    [InlineData("__gt__")]
    [InlineData("__ge__")]
    public void IsComparisonOperator_ReturnsTrueForComparisonOps(string methodName)
    {
        OperatorRegistry.IsComparisonOperator(methodName).Should().BeTrue();
    }

    [Fact]
    public void IsComparisonOperator_ReturnsFalseForNonComparisonOps()
    {
        OperatorRegistry.IsComparisonOperator("__add__").Should().BeFalse();
        OperatorRegistry.IsComparisonOperator("__neg__").Should().BeFalse();
    }

    #endregion

    #region Count and GetAllOperators Tests

    [Fact]
    public void Count_ReturnsExpectedNumberOfOperators()
    {
        // 5 arithmetic + 5 bitwise + 6 comparison + 3 unary = 19
        OperatorRegistry.Count.Should().Be(19);
    }

    [Fact]
    public void GetAllOperators_ReturnsAllRegisteredOperators()
    {
        var allOps = OperatorRegistry.GetAllOperators().ToList();
        allOps.Should().HaveCount(OperatorRegistry.Count);
        allOps.Should().Contain("__add__");
        allOps.Should().Contain("__neg__");
        allOps.Should().Contain("__eq__");
        allOps.Should().Contain("__and__");
    }

    #endregion
}
