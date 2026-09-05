using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class SumNarrowWidth_Tests
{
    // ── sbyte ──

    [Fact]
    public void Sum_SbyteList_ReturnsInt()
    {
        var list = new List<sbyte> { 1, 2, 3 };
        int result = Sum(list);
        result.Should().Be(6);
    }

    [Fact]
    public void Sum_EmptySbyteList_ReturnsZero()
    {
        var list = new List<sbyte>();
        Sum(list).Should().Be(0);
    }

    [Fact]
    public void Sum_SbyteList_WithStart_AddsStart()
    {
        var list = new List<sbyte> { 10, 20 };
        Sum(list, 100).Should().Be(130);
    }

    [Fact]
    public void Sum_EmptySbyteList_WithStart_ReturnsStart()
    {
        var list = new List<sbyte>();
        Sum(list, 42).Should().Be(42);
    }

    [Fact]
    public void Sum_NullSbyteIterable_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<sbyte>)null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Sum_NullSbyteIterable_WithStart_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<sbyte>)null!, 0))
            .Should().Throw<TypeError>();
    }

    // ── byte ──

    [Fact]
    public void Sum_ByteList_ReturnsInt()
    {
        var list = new List<byte> { 100, 150, 50 };
        int result = Sum(list);
        result.Should().Be(300);
    }

    [Fact]
    public void Sum_EmptyByteList_ReturnsZero()
    {
        var list = new List<byte>();
        Sum(list).Should().Be(0);
    }

    [Fact]
    public void Sum_ByteList_WithStart_AddsStart()
    {
        var list = new List<byte> { 10, 20 };
        Sum(list, 1000).Should().Be(1030);
    }

    [Fact]
    public void Sum_EmptyByteList_WithStart_ReturnsStart()
    {
        var list = new List<byte>();
        Sum(list, 7).Should().Be(7);
    }

    [Fact]
    public void Sum_NullByteIterable_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<byte>)null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Sum_NullByteIterable_WithStart_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<byte>)null!, 0))
            .Should().Throw<TypeError>();
    }

    // ── short ──

    [Fact]
    public void Sum_ShortList_ReturnsInt()
    {
        var list = new List<short> { 1000, 2000, 3000 };
        int result = Sum(list);
        result.Should().Be(6000);
    }

    [Fact]
    public void Sum_EmptyShortList_ReturnsZero()
    {
        var list = new List<short>();
        Sum(list).Should().Be(0);
    }

    [Fact]
    public void Sum_ShortList_WithStart_AddsStart()
    {
        var list = new List<short> { 100, 200 };
        Sum(list, 50000).Should().Be(50300);
    }

    [Fact]
    public void Sum_EmptyShortList_WithStart_ReturnsStart()
    {
        var list = new List<short>();
        Sum(list, 99).Should().Be(99);
    }

    [Fact]
    public void Sum_NullShortIterable_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<short>)null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Sum_NullShortIterable_WithStart_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<short>)null!, 0))
            .Should().Throw<TypeError>();
    }

    // ── ushort ──

    [Fact]
    public void Sum_UshortList_ReturnsInt()
    {
        var list = new List<ushort> { 10000, 20000, 30000 };
        int result = Sum(list);
        result.Should().Be(60000);
    }

    [Fact]
    public void Sum_EmptyUshortList_ReturnsZero()
    {
        var list = new List<ushort>();
        Sum(list).Should().Be(0);
    }

    [Fact]
    public void Sum_UshortList_WithStart_AddsStart()
    {
        var list = new List<ushort> { 500, 600 };
        Sum(list, 10000).Should().Be(11100);
    }

    [Fact]
    public void Sum_EmptyUshortList_WithStart_ReturnsStart()
    {
        var list = new List<ushort>();
        Sum(list, 12).Should().Be(12);
    }

    [Fact]
    public void Sum_NullUshortIterable_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<ushort>)null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Sum_NullUshortIterable_WithStart_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<ushort>)null!, 0))
            .Should().Throw<TypeError>();
    }

    // ── uint ──

    [Fact]
    public void Sum_UintList_ReturnsUint()
    {
        var list = new List<uint> { 1u, 2u, 3u };
        uint result = Sum(list);
        result.Should().Be(6u);
    }

    [Fact]
    public void Sum_EmptyUintList_ReturnsZero()
    {
        var list = new List<uint>();
        Sum(list).Should().Be(0u);
    }

    [Fact]
    public void Sum_UintList_WithStart_AddsStart()
    {
        var list = new List<uint> { 10u, 20u };
        Sum(list, 100u).Should().Be(130u);
    }

    [Fact]
    public void Sum_EmptyUintList_WithStart_ReturnsStart()
    {
        var list = new List<uint>();
        Sum(list, 5u).Should().Be(5u);
    }

    [Fact]
    public void Sum_UintList_Overflow_ThrowsOverflowException()
    {
        var list = new List<uint> { uint.MaxValue, 1u };
        FluentActions.Invoking(() => Sum(list))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void Sum_UintList_WithStart_Overflow_ThrowsOverflowException()
    {
        var list = new List<uint> { uint.MaxValue };
        FluentActions.Invoking(() => Sum(list, 1u))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void Sum_NullUintIterable_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<uint>)null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Sum_NullUintIterable_WithStart_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<uint>)null!, 0u))
            .Should().Throw<TypeError>();
    }

    // ── ulong ──

    [Fact]
    public void Sum_UlongList_ReturnsUlong()
    {
        var list = new List<ulong> { 1UL, 2UL, 3UL };
        ulong result = Sum(list);
        result.Should().Be(6UL);
    }

    [Fact]
    public void Sum_EmptyUlongList_ReturnsZero()
    {
        var list = new List<ulong>();
        Sum(list).Should().Be(0UL);
    }

    [Fact]
    public void Sum_UlongList_WithStart_AddsStart()
    {
        var list = new List<ulong> { 10UL, 20UL };
        Sum(list, 100UL).Should().Be(130UL);
    }

    [Fact]
    public void Sum_EmptyUlongList_WithStart_ReturnsStart()
    {
        var list = new List<ulong>();
        Sum(list, 5UL).Should().Be(5UL);
    }

    [Fact]
    public void Sum_UlongList_Overflow_ThrowsOverflowException()
    {
        var list = new List<ulong> { ulong.MaxValue, 1UL };
        FluentActions.Invoking(() => Sum(list))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void Sum_UlongList_WithStart_Overflow_ThrowsOverflowException()
    {
        var list = new List<ulong> { ulong.MaxValue };
        FluentActions.Invoking(() => Sum(list, 1UL))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void Sum_NullUlongIterable_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<ulong>)null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Sum_NullUlongIterable_WithStart_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sum((IEnumerable<ulong>)null!, 0UL))
            .Should().Throw<TypeError>();
    }

    // ── negative sbyte values ──

    [Fact]
    public void Sum_SbyteList_NegativeValues_Works()
    {
        var list = new List<sbyte> { -10, 20, -5 };
        Sum(list).Should().Be(5);
    }

    // ── negative short values ──

    [Fact]
    public void Sum_ShortList_NegativeValues_Works()
    {
        var list = new List<short> { -1000, 2000, -500 };
        Sum(list).Should().Be(500);
    }

    // ── large uint sum within range ──

    [Fact]
    public void Sum_UintList_LargeValues_NoOverflow()
    {
        var list = new List<uint> { 1_000_000_000u, 2_000_000_000u };
        Sum(list).Should().Be(3_000_000_000u);
    }

    // ── large ulong sum within range ──

    [Fact]
    public void Sum_UlongList_LargeValues_NoOverflow()
    {
        var list = new List<ulong> { 10_000_000_000UL, 20_000_000_000UL };
        Sum(list).Should().Be(30_000_000_000UL);
    }
}
