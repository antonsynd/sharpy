using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class MathAdditional_Tests
{
    // --- Lcm ---

    [Fact]
    public void Lcm_ReturnsLeastCommonMultiple()
    {
        Sharpy.Math.Lcm(12, 8).Should().Be(24);
    }

    [Fact]
    public void Lcm_WithZero_ReturnsZero()
    {
        Sharpy.Math.Lcm(0, 5).Should().Be(0);
        Sharpy.Math.Lcm(5, 0).Should().Be(0);
    }

    [Fact]
    public void Lcm_NegativeValues_ReturnsPositive()
    {
        Sharpy.Math.Lcm(-4, 6).Should().Be(12);
    }

    // --- Isclose ---

    [Fact]
    public void Isclose_CloseValues_ReturnsTrue()
    {
        Sharpy.Math.Isclose(1.0, 1.0000000001).Should().BeTrue();
    }

    [Fact]
    public void Isclose_FarValues_ReturnsFalse()
    {
        Sharpy.Math.Isclose(1.0, 1.1).Should().BeFalse();
    }

    [Fact]
    public void Isclose_EqualValues_ReturnsTrue()
    {
        Sharpy.Math.Isclose(0.0, 0.0).Should().BeTrue();
    }

    [Fact]
    public void Isclose_Infinity_ReturnsFalse()
    {
        Sharpy.Math.Isclose(double.PositiveInfinity, 1e308).Should().BeFalse();
    }

    [Fact]
    public void Isclose_BothInfinity_ReturnsTrue()
    {
        Sharpy.Math.Isclose(double.PositiveInfinity, double.PositiveInfinity).Should().BeTrue();
    }

    // --- Comb ---

    [Fact]
    public void Comb_ValidInput_ReturnsCorrect()
    {
        Sharpy.Math.Comb(10, 3).Should().Be(120);
    }

    [Fact]
    public void Comb_KGreaterThanN_ReturnsZero()
    {
        Sharpy.Math.Comb(3, 5).Should().Be(0);
    }

    [Fact]
    public void Comb_NegativeN_ReturnsZero()
    {
        Sharpy.Math.Comb(-1, 2).Should().Be(0);
    }

    [Fact]
    public void Comb_KZero_ReturnsOne()
    {
        Sharpy.Math.Comb(5, 0).Should().Be(1);
    }

    [Fact]
    public void Comb_KEqualN_ReturnsOne()
    {
        Sharpy.Math.Comb(5, 5).Should().Be(1);
    }

    // --- Perm ---

    [Fact]
    public void Perm_ValidInput_ReturnsCorrect()
    {
        Sharpy.Math.Perm(5, 3).Should().Be(60);
    }

    [Fact]
    public void Perm_KGreaterThanN_ReturnsZero()
    {
        Sharpy.Math.Perm(3, 5).Should().Be(0);
    }

    [Fact]
    public void Perm_KZero_ReturnsOne()
    {
        Sharpy.Math.Perm(5, 0).Should().Be(1);
    }

    // --- Fsum ---

    [Fact]
    public void Fsum_AccurateSummation()
    {
        var values = new double[10];
        for (int i = 0; i < 10; i++) values[i] = 0.1;
        Sharpy.Math.Fsum(values).Should().Be(1.0);
    }

    [Fact]
    public void Fsum_EmptyIterable_ReturnsZero()
    {
        Sharpy.Math.Fsum(new double[0]).Should().Be(0.0);
    }

    // --- Prod ---

    [Fact]
    public void Prod_IntValues_ReturnsProduct()
    {
        Sharpy.Math.Prod(new[] { 1, 2, 3, 4, 5 }).Should().Be(120);
    }

    [Fact]
    public void Prod_WithStart_MultipliesStart()
    {
        Sharpy.Math.Prod(new[] { 2, 3 }, 10).Should().Be(60);
    }

    [Fact]
    public void Prod_DoubleValues_ReturnsProduct()
    {
        Sharpy.Math.Prod(new[] { 1.5, 2.0, 3.0 }).Should().Be(9.0);
    }

    // --- Hypot ---

    [Fact]
    public void Hypot_3_4_Returns5()
    {
        Sharpy.Math.Hypot(3, 4).Should().Be(5.0);
    }

    // --- Expm1 ---

    [Fact]
    public void Expm1_SmallX_MoreAccurate()
    {
        var result = Sharpy.Math.Expm1(1e-15);
        result.Should().BeApproximately(1e-15, 1e-20);
    }

    [Fact]
    public void Expm1_LargeX_SameAsExpMinus1()
    {
        var result = Sharpy.Math.Expm1(1.0);
        result.Should().BeApproximately(System.Math.E - 1, 1e-10);
    }

    // --- Log1p ---

    [Fact]
    public void Log1p_SmallX_MoreAccurate()
    {
        var result = Sharpy.Math.Log1p(1e-15);
        result.Should().BeApproximately(1e-15, 1e-20);
    }

    [Fact]
    public void Log1p_LargeX_SameAsLog1PlusX()
    {
        var result = Sharpy.Math.Log1p(1.0);
        result.Should().BeApproximately(System.Math.Log(2.0), 1e-10);
    }

    // --- Remainder ---

    [Fact]
    public void Remainder_PositiveValues()
    {
        Sharpy.Math.Remainder(10, 3).Should().Be(1.0);
    }

    [Fact]
    public void Remainder_NegativeValues()
    {
        Sharpy.Math.Remainder(-10, 3).Should().Be(-1.0);
    }
}
