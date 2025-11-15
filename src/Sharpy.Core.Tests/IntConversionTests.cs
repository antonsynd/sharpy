using Xunit;
using Sharpy.Core;

namespace Sharpy.Core.Tests;

public class IntConversionTests
{
    [Fact]
    public void Int_FromBool_True_Returns1()
    {
        var result = Exports.Int(true);
        Assert.Equal(1, result);
    }

    [Fact]
    public void Int_FromBool_False_Returns0()
    {
        var result = Exports.Int(false);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Int_FromInt_ReturnsIdentity()
    {
        var result = Exports.Int(42);
        Assert.Equal(42, result);
    }

    [Fact]
    public void Int_FromLong_ValidValue_ReturnsInt()
    {
        var result = Exports.Int(100L);
        Assert.Equal(100, result);
    }

    [Fact]
    public void Int_FromLong_OutOfRange_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Exports.Int(long.MaxValue));
    }

    [Fact]
    public void Int_FromFloat_Truncates()
    {
        var result = Exports.Int(3.7f);
        Assert.Equal(3, result);
    }

    [Fact]
    public void Int_FromFloat_NaN_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Exports.Int(float.NaN));
    }

    [Fact]
    public void Int_FromFloat_PositiveInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Exports.Int(float.PositiveInfinity));
    }

    [Fact]
    public void Int_FromFloat_NegativeInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Exports.Int(float.NegativeInfinity));
    }

    [Fact]
    public void Int_FromDouble_Truncates()
    {
        var result = Exports.Int(3.9);
        Assert.Equal(3, result);
    }

    [Fact]
    public void Int_FromDouble_NaN_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Exports.Int(double.NaN));
    }

    [Fact]
    public void Int_FromDouble_PositiveInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Exports.Int(double.PositiveInfinity));
    }

    [Fact]
    public void Int_FromDouble_NegativeInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Exports.Int(double.NegativeInfinity));
    }

    [Fact]
    public void Int_FromDecimal_Truncates()
    {
        var result = Exports.Int(3.5m);
        Assert.Equal(3, result);
    }

    [Fact]
    public void Int_FromString_ValidInteger_ReturnsInt()
    {
        var result = Exports.Int("42");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Int_FromString_WithWhitespace_ReturnsInt()
    {
        var result = Exports.Int("  42  ");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Int_FromString_InvalidFormat_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Exports.Int("not a number"));
    }

    [Fact]
    public void Int_FromString_Empty_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Exports.Int(""));
    }

    [Fact]
    public void Int_FromByte_ReturnsInt()
    {
        byte b = 255;
        var result = Exports.Int(b);
        Assert.Equal(255, result);
    }

    [Fact]
    public void Int_FromShort_ReturnsInt()
    {
        short s = 1000;
        var result = Exports.Int(s);
        Assert.Equal(1000, result);
    }

    [Fact]
    public void Int_FromUShort_ReturnsInt()
    {
        ushort us = 5000;
        var result = Exports.Int(us);
        Assert.Equal(5000, result);
    }

    [Fact]
    public void Int_FromUInt_ValidValue_ReturnsInt()
    {
        uint u = 1000;
        var result = Exports.Int(u);
        Assert.Equal(1000, result);
    }

    [Fact]
    public void Int_FromUInt_OutOfRange_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Exports.Int(uint.MaxValue));
    }
}
