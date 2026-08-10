using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// The mixed dict/frozendict <c>|</c> operators (#1312, re-landed with #1361). The left operand
/// decides the RESULT TYPE, by analogy with set/frozenset; keys on the right win, matching each
/// type's own same-type <c>|</c> and CPython's PEP 584 <c>dict | dict</c>. Both operands are left
/// unmodified — <c>|</c> merges into a new value, unlike <c>|=</c>.
/// </summary>
public class FrozenDictMixedOperatorTests
{
    private static Dict<string, int> D(params (string Key, int Value)[] items)
    {
        var dict = new Dict<string, int>();
        foreach (var (key, value) in items)
        {
            dict[key] = value;
        }
        return dict;
    }

    private static FrozenDict<string, int> F(params (string Key, int Value)[] items)
        => new(D(items));

    [Fact]
    public void DictOnTheLeft_YieldsADict_WithRightKeysWinning()
    {
        var result = D(("a", 1), ("b", 2)) | F(("b", 20), ("c", 30));

        result.Should().BeOfType<Dict<string, int>>();
        result.Count.Should().Be(3);
        result["a"].Should().Be(1);
        result["b"].Should().Be(20, "keys on the right win");
        result["c"].Should().Be(30);
    }

    [Fact]
    public void FrozenDictOnTheLeft_YieldsAFrozenDict_WithRightKeysWinning()
    {
        var result = F(("a", 1), ("b", 2)) | D(("b", 20), ("c", 30));

        result.Should().BeOfType<FrozenDict<string, int>>();
        result.Count.Should().Be(3);
        result["a"].Should().Be(1);
        result["b"].Should().Be(20, "keys on the right win");
        result["c"].Should().Be(30);
    }

    [Fact]
    public void MixedMerge_LeavesBothOperandsUnchanged()
    {
        var left = D(("a", 1));
        var right = F(("a", 99), ("b", 2));

        _ = left | right;
        _ = right | left;

        left.Count.Should().Be(1);
        left["a"].Should().Be(1);
        right.Count.Should().Be(2);
        right["a"].Should().Be(99);
    }

    [Fact]
    public void EmptyOperands_BehaveAsIdentity()
    {
        (D() | F(("a", 1)))["a"].Should().Be(1);
        (F() | D(("a", 1)))["a"].Should().Be(1);
        (D(("a", 1)) | F()).Count.Should().Be(1);
        (F(("a", 1)) | D()).Count.Should().Be(1);
    }
}
