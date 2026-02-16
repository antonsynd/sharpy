using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StrTests
{
    [Fact]
    public void Str_Char_ReturnsCharAsString()
    {
        Builtins.Str('h').Should().Be("h");
    }

    [Fact]
    public void Str_Char_DoesNotReturnAsciiCode()
    {
        Builtins.Str('h').Should().NotBe("104");
    }

    [Theory]
    [InlineData(5.0, "5.0")]
    [InlineData(0.0, "0.0")]
    [InlineData(100.0, "100.0")]
    [InlineData(3.14, "3.14")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(double.NaN, "nan")]
    [InlineData(double.PositiveInfinity, "inf")]
    [InlineData(double.NegativeInfinity, "-inf")]
    public void Str_Double_FormatsLikePython(double value, string expected)
    {
        Builtins.Str(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(5.0f, "5.0")]
    [InlineData(0.0f, "0.0")]
    [InlineData(0.5f, "0.5")]
    [InlineData(float.NaN, "nan")]
    [InlineData(float.PositiveInfinity, "inf")]
    [InlineData(float.NegativeInfinity, "-inf")]
    public void Str_Float_FormatsLikePython(float value, string expected)
    {
        Builtins.Str(value).Should().Be(expected);
    }

    [Fact]
    public void Str_BoxedDouble_FormatsLikePython()
    {
        object boxed = 5.0;
        Builtins.Str(boxed).Should().Be("5.0");
    }

    [Fact]
    public void Str_BoxedFloat_FormatsLikePython()
    {
        object boxed = 5.0f;
        Builtins.Str(boxed).Should().Be("5.0");
    }
}
