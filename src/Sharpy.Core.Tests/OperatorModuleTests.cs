using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class OperatorModule_Tests
{
    // --- Add ---

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 0)]
    public void Add_Int_ReturnsSum(int a, int b, int expected)
    {
        Sharpy.Operator.Add(a, b).Should().Be(expected);
    }

    [Fact]
    public void Add_Long_ReturnsSum()
    {
        Sharpy.Operator.Add(1L, 2L).Should().Be(3L);
    }

    [Fact]
    public void Add_Double_ReturnsSum()
    {
        Sharpy.Operator.Add(1.5, 2.5).Should().Be(4.0);
    }

    [Fact]
    public void Add_String_ReturnsConcatenation()
    {
        Sharpy.Operator.Add("hello", " world").Should().Be("hello world");
    }

    [Fact]
    public void Add_Float_ReturnsSum()
    {
        Sharpy.Operator.Add(1.0f, 2.0f).Should().Be(3.0f);
    }

    [Fact]
    public void Add_Decimal_ReturnsSum()
    {
        Sharpy.Operator.Add(1.0m, 2.0m).Should().Be(3.0m);
    }

    // --- Mul ---

    [Theory]
    [InlineData(2, 3, 6)]
    [InlineData(-2, 3, -6)]
    [InlineData(0, 100, 0)]
    public void Mul_Int_ReturnsProduct(int a, int b, int expected)
    {
        Sharpy.Operator.Mul(a, b).Should().Be(expected);
    }

    [Fact]
    public void Mul_Double_ReturnsProduct()
    {
        Sharpy.Operator.Mul(2.5, 4.0).Should().Be(10.0);
    }

    [Fact]
    public void Mul_Long_ReturnsProduct()
    {
        Sharpy.Operator.Mul(3L, 4L).Should().Be(12L);
    }

    [Fact]
    public void Mul_Float_ReturnsProduct()
    {
        Sharpy.Operator.Mul(2.0f, 3.0f).Should().Be(6.0f);
    }

    [Fact]
    public void Mul_Decimal_ReturnsProduct()
    {
        Sharpy.Operator.Mul(2.5m, 4.0m).Should().Be(10.0m);
    }

    // --- Eq ---

    [Fact]
    public void Eq_EqualInts_ReturnsTrue()
    {
        Sharpy.Operator.Eq((IComparable<int>)5, 5).Should().BeTrue();
    }

    [Fact]
    public void Eq_UnequalInts_ReturnsFalse()
    {
        Sharpy.Operator.Eq((IComparable<int>)5, 10).Should().BeFalse();
    }

    [Fact]
    public void Eq_SameReference_ReturnsTrue()
    {
        var obj = new object();
        Sharpy.Operator.Eq(obj, obj).Should().BeTrue();
    }

    [Fact]
    public void Eq_NullNull_ReturnsTrue()
    {
        Sharpy.Operator.Eq((object)null!, (object)null!).Should().BeTrue();
    }

    // --- Ne ---

    [Fact]
    public void Ne_EqualInts_ReturnsFalse()
    {
        Sharpy.Operator.Ne((IComparable<int>)5, 5).Should().BeFalse();
    }

    [Fact]
    public void Ne_UnequalInts_ReturnsTrue()
    {
        Sharpy.Operator.Ne((IComparable<int>)5, 10).Should().BeTrue();
    }

    [Fact]
    public void Ne_SameReference_ReturnsFalse()
    {
        var obj = new object();
        Sharpy.Operator.Ne((IComparable)5, (object)5).Should().BeFalse();
    }

    // --- Lt ---

    [Fact]
    public void Lt_LessThan_ReturnsTrue()
    {
        Sharpy.Operator.Lt((IComparable<int>)3, 5).Should().BeTrue();
    }

    [Fact]
    public void Lt_Equal_ReturnsFalse()
    {
        Sharpy.Operator.Lt((IComparable<int>)5, 5).Should().BeFalse();
    }

    [Fact]
    public void Lt_GreaterThan_ReturnsFalse()
    {
        Sharpy.Operator.Lt((IComparable<int>)7, 5).Should().BeFalse();
    }

    // --- Le ---

    [Fact]
    public void Le_LessThan_ReturnsTrue()
    {
        Sharpy.Operator.Le((IComparable<int>)3, 5).Should().BeTrue();
    }

    [Fact]
    public void Le_GreaterThan_ReturnsFalse()
    {
        Sharpy.Operator.Le((IComparable<int>)7, 5).Should().BeFalse();
    }

    // --- Gt ---

    [Fact]
    public void Gt_GreaterThan_ReturnsTrue()
    {
        Sharpy.Operator.Gt((IComparable<int>)7, 5).Should().BeTrue();
    }

    [Fact]
    public void Gt_Equal_ReturnsFalse()
    {
        Sharpy.Operator.Gt((IComparable<int>)5, 5).Should().BeFalse();
    }

    [Fact]
    public void Gt_LessThan_ReturnsFalse()
    {
        Sharpy.Operator.Gt((IComparable<int>)3, 5).Should().BeFalse();
    }

    // --- Ge ---

    [Fact]
    public void Ge_GreaterThan_ReturnsTrue()
    {
        Sharpy.Operator.Ge((IComparable<int>)7, 5).Should().BeTrue();
    }

    [Fact]
    public void Ge_LessThan_ReturnsFalse()
    {
        Sharpy.Operator.Ge((IComparable<int>)3, 5).Should().BeFalse();
    }

    // --- Not ---

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Not_Bool_ReturnsNegation(bool value, bool expected)
    {
        Sharpy.Operator.Not(value).Should().Be(expected);
    }

    [Fact]
    public void Not_EmptyGenericCollection_ReturnsTrue()
    {
        Sharpy.Operator.Not<int>(new List<int>()).Should().BeTrue();
    }

    [Fact]
    public void Not_NonEmptyGenericCollection_ReturnsFalse()
    {
        Sharpy.Operator.Not<int>(new List<int> { 1 }).Should().BeFalse();
    }

    // --- Truth ---

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Truth_Bool_ReturnsValue(bool value, bool expected)
    {
        Sharpy.Operator.Truth(value).Should().Be(expected);
    }

    [Fact]
    public void Truth_EmptyGenericCollection_ReturnsFalse()
    {
        Sharpy.Operator.Truth<int>(new List<int>()).Should().BeFalse();
    }

    [Fact]
    public void Truth_NonEmptyGenericCollection_ReturnsTrue()
    {
        Sharpy.Operator.Truth<int>(new List<int> { 1, 2 }).Should().BeTrue();
    }

    // --- Is / IsNot ---

    [Fact]
    public void Is_SameReference_ReturnsTrue()
    {
        var obj = new object();
        Sharpy.Operator.Is(obj, obj).Should().BeTrue();
    }

    [Fact]
    public void Is_DifferentReferences_ReturnsFalse()
    {
        Sharpy.Operator.Is(new object(), new object()).Should().BeFalse();
    }

    [Fact]
    public void IsNot_SameReference_ReturnsFalse()
    {
        var obj = new object();
        Sharpy.Operator.IsNot(obj, obj).Should().BeFalse();
    }

    [Fact]
    public void IsNot_DifferentReferences_ReturnsTrue()
    {
        Sharpy.Operator.IsNot(new object(), new object()).Should().BeTrue();
    }

    // --- Abs ---

    [Theory]
    [InlineData(-5, 5)]
    [InlineData(5, 5)]
    [InlineData(0, 0)]
    public void Abs_Int_ReturnsAbsoluteValue(int value, int expected)
    {
        Sharpy.Operator.Abs(value).Should().Be(expected);
    }

    [Fact]
    public void Abs_Double_ReturnsAbsoluteValue()
    {
        Sharpy.Operator.Abs(-3.14).Should().Be(3.14);
    }

    [Fact]
    public void Abs_Long_ReturnsAbsoluteValue()
    {
        Sharpy.Operator.Abs(-100L).Should().Be(100L);
    }

    [Fact]
    public void Abs_Float_ReturnsAbsoluteValue()
    {
        Sharpy.Operator.Abs(-2.5f).Should().Be(2.5f);
    }

    [Fact]
    public void Abs_Decimal_ReturnsAbsoluteValue()
    {
        Sharpy.Operator.Abs(-9.99m).Should().Be(9.99m);
    }

    [Fact]
    public void Abs_Short_ReturnsAbsoluteValue()
    {
        Sharpy.Operator.Abs((short)-7).Should().Be((short)7);
    }

    [Fact]
    public void Abs_Sbyte_ReturnsAbsoluteValue()
    {
        Sharpy.Operator.Abs((sbyte)-3).Should().Be((sbyte)3);
    }

    // --- IAdd ---

    [Fact]
    public void IAdd_Int_AddsInPlace()
    {
        int x = 5;
        Sharpy.Operator.IAdd(ref x, 3);
        x.Should().Be(8);
    }

    [Fact]
    public void IAdd_Double_AddsInPlace()
    {
        double x = 1.5;
        Sharpy.Operator.IAdd(ref x, 2.5);
        x.Should().Be(4.0);
    }

    [Fact]
    public void IAdd_Long_AddsInPlace()
    {
        long x = 10L;
        Sharpy.Operator.IAdd(ref x, 20L);
        x.Should().Be(30L);
    }

    // --- IMul ---

    [Fact]
    public void IMul_Int_MultipliesInPlace()
    {
        int x = 5;
        Sharpy.Operator.IMul(ref x, 3);
        x.Should().Be(15);
    }

    [Fact]
    public void IMul_Double_MultipliesInPlace()
    {
        double x = 2.5;
        Sharpy.Operator.IMul(ref x, 4.0);
        x.Should().Be(10.0);
    }

    [Fact]
    public void IMul_Long_MultipliesInPlace()
    {
        long x = 10L;
        Sharpy.Operator.IMul(ref x, 5L);
        x.Should().Be(50L);
    }
}
