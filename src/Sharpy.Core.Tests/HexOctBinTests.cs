using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class HexOctBin_Tests
{
    // ── Hex(int) ──

    [Theory]
    [InlineData(0, "0x0")]
    [InlineData(255, "0xff")]
    [InlineData(16, "0x10")]
    [InlineData(1, "0x1")]
    [InlineData(-42, "-0x2a")]
    [InlineData(-1, "-0x1")]
    public void Hex_Int_ReturnsCorrectString(int input, string expected)
    {
        Hex(input).Should().Be(expected);
    }

    [Fact]
    public void Hex_IntMaxValue_DoesNotOverflow()
    {
        Hex(int.MaxValue).Should().Be("0x7fffffff");
    }

    [Fact]
    public void Hex_IntMinValue_DoesNotOverflow()
    {
        Hex(int.MinValue).Should().Be("-0x80000000");
    }

    // ── Hex(long) ──

    [Theory]
    [InlineData(0L, "0x0")]
    [InlineData(255L, "0xff")]
    [InlineData(-42L, "-0x2a")]
    public void Hex_Long_ReturnsCorrectString(long input, string expected)
    {
        Hex(input).Should().Be(expected);
    }

    [Fact]
    public void Hex_LongMaxValue_DoesNotOverflow()
    {
        Hex(long.MaxValue).Should().Be("0x7fffffffffffffff");
    }

    [Fact]
    public void Hex_LongMinValue_HandlesSpecialCase()
    {
        Hex(long.MinValue).Should().Be("-0x8000000000000000");
    }

    // ── Oct(int) ──

    [Theory]
    [InlineData(0, "0o0")]
    [InlineData(8, "0o10")]
    [InlineData(7, "0o7")]
    [InlineData(64, "0o100")]
    [InlineData(-8, "-0o10")]
    [InlineData(-1, "-0o1")]
    public void Oct_Int_ReturnsCorrectString(int input, string expected)
    {
        Oct(input).Should().Be(expected);
    }

    [Fact]
    public void Oct_IntMaxValue_DoesNotOverflow()
    {
        Oct(int.MaxValue).Should().Be("0o17777777777");
    }

    [Fact]
    public void Oct_IntMinValue_DoesNotOverflow()
    {
        Oct(int.MinValue).Should().Be("-0o20000000000");
    }

    // ── Oct(long) ──

    [Fact]
    public void Oct_LongMinValue_HandlesSpecialCase()
    {
        Oct(long.MinValue).Should().Be("-0o1000000000000000000000");
    }

    [Fact]
    public void Oct_LongMaxValue_DoesNotOverflow()
    {
        Oct(long.MaxValue).Should().Be("0o777777777777777777777");
    }

    // ── Bin(int) ──

    [Theory]
    [InlineData(0, "0b0")]
    [InlineData(10, "0b1010")]
    [InlineData(1, "0b1")]
    [InlineData(255, "0b11111111")]
    [InlineData(-10, "-0b1010")]
    [InlineData(-1, "-0b1")]
    public void Bin_Int_ReturnsCorrectString(int input, string expected)
    {
        Bin(input).Should().Be(expected);
    }

    [Fact]
    public void Bin_IntMaxValue_DoesNotOverflow()
    {
        Bin(int.MaxValue).Should().Be("0b1111111111111111111111111111111");
    }

    [Fact]
    public void Bin_IntMinValue_DoesNotOverflow()
    {
        Bin(int.MinValue).Should().Be("-0b10000000000000000000000000000000");
    }

    // ── Bin(long) ──

    [Fact]
    public void Bin_LongMinValue_HandlesSpecialCase()
    {
        Bin(long.MinValue).Should().Be("-0b1000000000000000000000000000000000000000000000000000000000000000");
    }

    [Fact]
    public void Bin_LongMaxValue_DoesNotOverflow()
    {
        Bin(long.MaxValue).Should().Be("0b111111111111111111111111111111111111111111111111111111111111111");
    }
}
