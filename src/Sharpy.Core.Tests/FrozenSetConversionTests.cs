using Xunit;
using Sharpy.Core;

namespace Sharpy.Core.Tests;

public class FrozenSetConversionTests
{
    [Fact]
    public void FrozenSet_FromEmpty_ReturnsEmptyFrozenSet()
    {
        var result = Exports.FrozenSet<int>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void FrozenSet_FromIEnumerable_ReturnsFrozenSet()
    {
        var source = new System.Collections.Generic.List<int> { 1, 2, 3, 2, 1 };
        var result = Exports.FrozenSet<int>(source);

        Assert.Equal(3, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);
    }

    [Fact]
    public void FrozenSet_FromIterable_ReturnsFrozenSet()
    {
        var range = Exports.Range(1, 4); // 1, 2, 3
        var result = Exports.FrozenSet(range);

        Assert.Equal(3, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);
    }

    [Fact]
    public void FrozenSet_FromIEnumerableWithDuplicates_RemovesDuplicates()
    {
        var source = new System.Collections.Generic.List<string> { "a", "b", "a", "c", "b" };
        var result = Exports.FrozenSet<string>(source);

        Assert.Equal(3, result.Count);
        Assert.Contains("a", result);
        Assert.Contains("b", result);
        Assert.Contains("c", result);
    }

    [Fact]
    public void FrozenSet_FromExistingFrozenSet_CreatesSameFrozenSet()
    {
        var original = Exports.FrozenSet(new[] { 1, 2, 3 });
        var result = Exports.FrozenSet<int>(original);

        Assert.Equal(3, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);

        // FrozenSets are immutable, so creating from existing should work
        Assert.Equal(original, result);
    }
}
