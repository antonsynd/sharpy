using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for the variadic value form of min()/max() (#1010): two or more scalar arguments,
/// e.g. min(2, 3) / max(2, 3, 1), matching Python.
/// </summary>
public class MinMaxMultiTests
{
    // ── Min, value form ──

    [Fact]
    public void Min_TwoInts()
    {
        Min(2, 3).Should().Be(2);
        Min(3, 2).Should().Be(2);
    }

    [Fact]
    public void Min_ManyInts()
    {
        Min(5, 2, 8, 1).Should().Be(1);
    }

    [Fact]
    public void Min_Strings()
    {
        Min("foo", "bar").Should().Be("bar");
        Min("c", "b", "a").Should().Be("a");
    }

    [Fact]
    public void Min_NegativeNumbers()
    {
        Min(-1, -5, -3).Should().Be(-5);
    }

    // ── Max, value form ──

    [Fact]
    public void Max_TwoInts()
    {
        Max(2, 3).Should().Be(3);
        Max(3, 2).Should().Be(3);
    }

    [Fact]
    public void Max_ManyInts()
    {
        Max(2, 3, 1).Should().Be(3);
        Max(5, 2, 8, 1).Should().Be(8);
    }

    [Fact]
    public void Max_Strings()
    {
        Max("a", "b", "c").Should().Be("c");
    }

    [Fact]
    public void Max_NegativeNumbers()
    {
        Max(-1, -5, -3).Should().Be(-1);
    }

    // ── Null elements raise (matching the iterable form's contract) ──

    [Fact]
    public void Min_NullValue_Throws()
    {
        string? a = null;
        FluentActions.Invoking(() => Min(a!, "x")).Should().Throw<TypeError>();
    }

    [Fact]
    public void Max_NullInRest_Throws()
    {
        string? c = null;
        FluentActions.Invoking(() => Max("a", "b", c!)).Should().Throw<TypeError>();
    }
}
