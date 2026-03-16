using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class SumStart_Tests
{
    // ── int ──

    [Fact]
    public void Sum_IntList_WithStart_AddsStartToSum()
    {
        List<int> list = [1, 2, 3];
        Sum(list, 10).Should().Be(16);
    }

    [Fact]
    public void Sum_EmptyIntList_WithStart_ReturnsStart()
    {
        var empty = new List<int>();
        Sum(empty, 5).Should().Be(5);
    }

    [Fact]
    public void Sum_IntList_WithZeroStart_SameAsWithout()
    {
        List<int> list = [1, 2, 3];
        Sum(list, 0).Should().Be(Sum(list));
    }

    // ── long ──

    [Fact]
    public void Sum_LongList_WithStart_AddsStartToSum()
    {
        List<long> list = [1L, 2L, 3L];
        Sum(list, 100L).Should().Be(106L);
    }

    [Fact]
    public void Sum_EmptyLongList_WithStart_ReturnsStart()
    {
        var empty = new List<long>();
        Sum(empty, 5L).Should().Be(5L);
    }

    // ── float ──

    [Fact]
    public void Sum_FloatList_WithStart_AddsStartToSum()
    {
        List<float> list = [1.5f, 2.5f];
        Sum(list, 10.0f).Should().Be(14.0f);
    }

    [Fact]
    public void Sum_EmptyFloatList_WithStart_ReturnsStart()
    {
        var empty = new List<float>();
        Sum(empty, 3.14f).Should().Be(3.14f);
    }

    // ── double ──

    [Fact]
    public void Sum_DoubleList_WithStart_AddsStartToSum()
    {
        List<double> list = [1.1, 2.2, 3.3];
        Sum(list, 10.0).Should().BeApproximately(16.6, 0.0001);
    }

    [Fact]
    public void Sum_EmptyDoubleList_WithStart_ReturnsStart()
    {
        var empty = new List<double>();
        Sum(empty, 7.5).Should().Be(7.5);
    }

    // ── decimal ──

    [Fact]
    public void Sum_DecimalList_WithStart_AddsStartToSum()
    {
        List<decimal> list = [1.1m, 2.2m, 3.3m];
        Sum(list, 10.0m).Should().Be(16.6m);
    }

    [Fact]
    public void Sum_EmptyDecimalList_WithStart_ReturnsStart()
    {
        var empty = new List<decimal>();
        Sum(empty, 7.5m).Should().Be(7.5m);
    }

    // ── null checks ──

    [Fact]
    public void Sum_NullIntIterable_WithStart_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<int>)null!, 0))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Sum_NullLongIterable_WithStart_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<long>)null!, 0L))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Sum_NullDoubleIterable_WithStart_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<double>)null!, 0.0))
            .Should().Throw<TypeError>();
    }

    // ── negative start ──

    [Fact]
    public void Sum_IntList_WithNegativeStart_Works()
    {
        List<int> list = [10, 20];
        Sum(list, -5).Should().Be(25);
    }
}
