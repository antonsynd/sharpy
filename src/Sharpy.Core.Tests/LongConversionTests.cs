using Xunit;
using Sharpy;

namespace Sharpy.Core.Tests;

public class LongConversionTests
{
    [Fact]
    public void Long_FromBool_True_Returns1()
    {
        var result = Builtins.Long(true);
        Assert.Equal(1L, result);
    }

    [Fact]
    public void Long_FromBool_False_Returns0()
    {
        var result = Builtins.Long(false);
        Assert.Equal(0L, result);
    }

    [Fact]
    public void Long_FromInt_ReturnsWidened()
    {
        var result = Builtins.Long(42);
        Assert.Equal(42L, result);
    }

    [Fact]
    public void Long_FromLong_ReturnsIdentity()
    {
        var result = Builtins.Long(100L);
        Assert.Equal(100L, result);
    }

    [Fact]
    public void Long_FromFloat_Truncates()
    {
        var result = Builtins.Long(3.7f);
        Assert.Equal(3L, result);
    }

    [Fact]
    public void Long_FromFloat_NaN_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Long(float.NaN));
    }

    [Fact]
    public void Long_FromFloat_PositiveInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Long(float.PositiveInfinity));
    }

    [Fact]
    public void Long_FromFloat_NegativeInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Long(float.NegativeInfinity));
    }

    [Fact]
    public void Long_FromDouble_Truncates()
    {
        var result = Builtins.Long(3.9);
        Assert.Equal(3L, result);
    }

    [Fact]
    public void Long_FromDouble_NaN_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Long(double.NaN));
    }

    [Fact]
    public void Long_FromDouble_PositiveInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Long(double.PositiveInfinity));
    }

    [Fact]
    public void Long_FromDouble_NegativeInfinity_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Long(double.NegativeInfinity));
    }

    [Fact]
    public void Long_FromDecimal_Truncates()
    {
        var result = Builtins.Long(3.5m);
        Assert.Equal(3L, result);
    }

    [Fact]
    public void Long_FromDecimal_OutOfRange_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Long(decimal.MaxValue));
    }

    [Fact]
    public void Long_FromString_ValidInteger_ReturnsLong()
    {
        var result = Builtins.Long("42");
        Assert.Equal(42L, result);
    }

    [Fact]
    public void Long_FromString_WithWhitespace_ReturnsLong()
    {
        var result = Builtins.Long("  42  ");
        Assert.Equal(42L, result);
    }

    [Fact]
    public void Long_FromString_NegativeValue_ReturnsLong()
    {
        var result = Builtins.Long("-100");
        Assert.Equal(-100L, result);
    }

    [Fact]
    public void Long_FromString_LargeValue_ReturnsLong()
    {
        var result = Builtins.Long("9223372036854775807");
        Assert.Equal(long.MaxValue, result);
    }

    [Fact]
    public void Long_FromString_InvalidFormat_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Long("not a number"));
    }

    [Fact]
    public void Long_FromString_Empty_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Long(""));
    }

    [Fact]
    public void Long_FromByte_ReturnsLong()
    {
        byte b = 255;
        var result = Builtins.Long(b);
        Assert.Equal(255L, result);
    }

    [Fact]
    public void Long_FromSByte_ReturnsLong()
    {
        sbyte sb = -128;
        var result = Builtins.Long(sb);
        Assert.Equal(-128L, result);
    }

    [Fact]
    public void Long_FromShort_ReturnsLong()
    {
        short s = 1000;
        var result = Builtins.Long(s);
        Assert.Equal(1000L, result);
    }

    [Fact]
    public void Long_FromUShort_ReturnsLong()
    {
        ushort us = 5000;
        var result = Builtins.Long(us);
        Assert.Equal(5000L, result);
    }

    [Fact]
    public void Long_FromUInt_ReturnsLong()
    {
        uint u = 1000;
        var result = Builtins.Long(u);
        Assert.Equal(1000L, result);
    }

    [Fact]
    public void Long_FromULong_ValidValue_ReturnsLong()
    {
        ulong ul = 1000;
        var result = Builtins.Long(ul);
        Assert.Equal(1000L, result);
    }

    [Fact]
    public void Long_FromULong_OutOfRange_ThrowsOverflowException()
    {
        Assert.Throws<OverflowException>(() => Builtins.Long(ulong.MaxValue));
    }
}
