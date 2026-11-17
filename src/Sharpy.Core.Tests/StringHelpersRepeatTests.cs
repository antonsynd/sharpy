using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Count-first <c>StringHelpers.Repeat(count, str)</c> overloads: the lowering of
/// <c>count * str</c>, which evaluates the left operand first.
/// </summary>
public class StringHelpersRepeatTests
{
    [Fact]
    public void CountFirst_Int_RepeatsString() =>
        StringHelpers.Repeat(3, "ab").Should().Be("ababab");

    [Fact]
    public void CountFirst_Int_NegativeCount_ReturnsEmpty() =>
        StringHelpers.Repeat(-1, "ab").Should().Be("");

    [Fact]
    public void CountFirst_Int_ZeroCount_ReturnsEmpty() =>
        StringHelpers.Repeat(0, "ab").Should().Be("");

    [Fact]
    public void CountFirst_Int_EmptyString_ReturnsEmpty() =>
        StringHelpers.Repeat(5, "").Should().Be("");

    [Fact]
    public void CountFirst_Int_One_ReturnsSame() =>
        StringHelpers.Repeat(1, "ab").Should().Be("ab");

    [Fact]
    public void CountFirst_Int_Large()
    {
        var r = StringHelpers.Repeat(100_000, "xy");
        r.Length.Should().Be(200_000);
        r.Should().Be(StringHelpers.Repeat("xy", 100_000));
    }

    [Fact]
    public void CountFirst_Long_RepeatsString() =>
        StringHelpers.Repeat(3L, "ab").Should().Be("ababab");

    [Fact]
    public void CountFirst_Long_NegativeAndZero_ReturnEmpty()
    {
        StringHelpers.Repeat(-1L, "ab").Should().Be("");
        StringHelpers.Repeat(0L, "ab").Should().Be("");
    }

    [Fact]
    public void CountFirst_Long_OutOfIntRange_Throws()
    {
        var act = () => StringHelpers.Repeat((long)int.MaxValue + 1, "ab");
        act.Should().Throw<OverflowError>();
    }

    [Fact]
    public void CountFirst_MatchesStrFirst_ForSharedCounts()
    {
        foreach (var n in new[] { -3, 0, 1, 2, 7 })
            StringHelpers.Repeat(n, "ab").Should().Be(StringHelpers.Repeat("ab", n));
    }

    [Fact]
    public void CountFirst_NarrowAndUnsignedCounts_BindUnambiguously()
    {
        sbyte sb = 3;
        byte b = 3;
        short sh = 3;
        ushort us = 3;
        uint ui = 3;
        StringHelpers.Repeat(sb, "ab").Should().Be("ababab");
        StringHelpers.Repeat(b, "ab").Should().Be("ababab");
        StringHelpers.Repeat(sh, "ab").Should().Be("ababab");
        StringHelpers.Repeat(us, "ab").Should().Be("ababab");
        StringHelpers.Repeat(ui, "ab").Should().Be("ababab");
    }
}
