using Xunit;
using Sharpy;

namespace Sharpy.Core.Tests;

public class DoubleConversionTests
{
    [Fact]
    public void Double_FromBool_True_Returns1()
    {
        var result = Builtins.Double(true);
        Assert.Equal(1.0, result);
    }

    [Fact]
    public void Double_FromBool_False_Returns0()
    {
        var result = Builtins.Double(false);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Double_FromInt_ReturnsDouble()
    {
        var result = Builtins.Double(42);
        Assert.Equal(42.0, result);
    }

    [Fact]
    public void Double_FromLong_ReturnsDouble()
    {
        var result = Builtins.Double(100L);
        Assert.Equal(100.0, result);
    }

    [Fact]
    public void Double_FromFloat_ReturnsDouble()
    {
        var result = Builtins.Double(3.14f);
        Assert.InRange(result, 3.13, 3.15); // Float precision loss
    }

    [Fact]
    public void Double_FromDouble_ReturnsIdentity()
    {
        var result = Builtins.Double(3.14159);
        Assert.Equal(3.14159, result);
    }

    [Fact]
    public void Double_FromDecimal_ReturnsDouble()
    {
        var result = Builtins.Double(3.5m);
        Assert.Equal(3.5, result);
    }

    [Fact]
    public void Double_FromString_ValidNumber_ReturnsDouble()
    {
        var result = Builtins.Double("3.14");
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void Double_FromString_WithWhitespace_ReturnsDouble()
    {
        var result = Builtins.Double("  3.14  ");
        Assert.Equal(3.14, result);
    }

    [Fact]
    public void Double_FromString_InvalidFormat_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Double("not a number"));
    }

    [Fact]
    public void Double_FromString_Empty_ThrowsValueError()
    {
        Assert.Throws<ValueError>(() => Builtins.Double(""));
    }

    [Fact]
    public void Double_FromByte_ReturnsDouble()
    {
        byte b = 255;
        var result = Builtins.Double(b);
        Assert.Equal(255.0, result);
    }

    [Fact]
    public void Double_FromShort_ReturnsDouble()
    {
        short s = 1000;
        var result = Builtins.Double(s);
        Assert.Equal(1000.0, result);
    }

    [Fact]
    public void Double_FromUInt_ReturnsDouble()
    {
        uint u = 5000;
        var result = Builtins.Double(u);
        Assert.Equal(5000.0, result);
    }
}
