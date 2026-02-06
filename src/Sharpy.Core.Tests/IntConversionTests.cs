using Xunit;
using Sharpy.Core;

namespace Sharpy.Core.Tests;

public class IntConversionTests
{
    [Fact]
    public void Int_FromBool_True_Returns1()
    {
        var result = Builtins.Int(true);
        Assert.Equal(1, result);
    }

    [Fact]
    public void Int_FromBool_False_Returns0()
    {
        var result = Builtins.Int(false);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Int_FromInt_ReturnsIdentity()
    {
        var result = Builtins.Int(42);
        Assert.Equal(42, result);
    }

    [Fact]
    public void Int_FromLong_ValidValue_ReturnsInt()
    {
        var result = Builtins.Int(100L);
        Assert.Equal(100, result);
    }

    [Fact]
    public void Int_FromLong_OutOfRange_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Int(long.MaxValue));
    }

    [Fact]
    public void Int_FromFloat_Truncates()
    {
        var result = Builtins.Int(3.7f);
        Assert.Equal(3, result);
    }

    [Fact]
    public void Int_FromFloat_NaN_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Int(float.NaN));
    }

    [Fact]
    public void Int_FromFloat_PositiveInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Int(float.PositiveInfinity));
    }

    [Fact]
    public void Int_FromFloat_NegativeInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Int(float.NegativeInfinity));
    }

    [Fact]
    public void Int_FromDouble_Truncates()
    {
        var result = Builtins.Int(3.9);
        Assert.Equal(3, result);
    }

    [Fact]
    public void Int_FromDouble_NaN_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Int(double.NaN));
    }

    [Fact]
    public void Int_FromDouble_PositiveInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Int(double.PositiveInfinity));
    }

    [Fact]
    public void Int_FromDouble_NegativeInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Int(double.NegativeInfinity));
    }

    [Fact]
    public void Int_FromDecimal_Truncates()
    {
        var result = Builtins.Int(3.5m);
        Assert.Equal(3, result);
    }

    [Fact]
    public void Int_FromString_ValidInteger_ReturnsInt()
    {
        var result = Builtins.Int("42");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Int_FromString_WithWhitespace_ReturnsInt()
    {
        var result = Builtins.Int("  42  ");
        Assert.Equal(42, result);
    }

    [Fact]
    public void Int_FromString_InvalidFormat_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Int("not a number"));
    }

    [Fact]
    public void Int_FromString_Empty_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Int(""));
    }

    [Fact]
    public void Int_FromByte_ReturnsInt()
    {
        byte b = 255;
        var result = Builtins.Int(b);
        Assert.Equal(255, result);
    }

    [Fact]
    public void Int_FromShort_ReturnsInt()
    {
        short s = 1000;
        var result = Builtins.Int(s);
        Assert.Equal(1000, result);
    }

    [Fact]
    public void Int_FromUShort_ReturnsInt()
    {
        ushort us = 5000;
        var result = Builtins.Int(us);
        Assert.Equal(5000, result);
    }

    [Fact]
    public void Int_FromUInt_ValidValue_ReturnsInt()
    {
        uint u = 1000;
        var result = Builtins.Int(u);
        Assert.Equal(1000, result);
    }

    [Fact]
    public void Int_FromUInt_OutOfRange_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Int(uint.MaxValue));
    }
}
