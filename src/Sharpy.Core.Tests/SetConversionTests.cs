using Xunit;
using Sharpy.Core;

namespace Sharpy.Core.Tests;

public class SetConversionTests
{
    [Fact]
    public void Set_FromEmpty_ReturnsEmptySet()
    {
        var result = Exports.Set<int>();
        Assert.NotNull(result);
        Assert.Equal(0u, result.__Len__());
    }

    [Fact]
    public void Set_FromIEnumerable_ReturnsSet()
    {
        var source = new System.Collections.Generic.List<int> { 1, 2, 3, 2, 1 };
        var result = Exports.Set<int>(source);

        Assert.Equal(3u, result.__Len__());
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);
    }

    [Fact]
    public void Set_CopyConstructor_CreatesNewSet()
    {
        var original = new Set<int>();
        original.Add(1);
        original.Add(2);
        original.Add(3);

        var copy = Exports.Set(original);

        Assert.Equal(3u, copy.__Len__());
        Assert.Contains(1, copy);
        Assert.Contains(2, copy);
        Assert.Contains(3, copy);

        // Verify it's a copy, not the same instance
        original.Add(4);
        Assert.Equal(4u, original.__Len__());
        Assert.Equal(3u, copy.__Len__());
    }

    [Fact]
    public void Set_FromIterable_ReturnsSet()
    {
        var range = Exports.Range(1, 4); // 1, 2, 3
        var result = Exports.Set(range);

        Assert.Equal(3u, result.__Len__());
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.Contains(3, result);
    }

    [Fact]
    public void Set_FromIEnumerableWithDuplicates_RemovesDuplicates()
    {
        var source = new System.Collections.Generic.List<string> { "a", "b", "a", "c", "b" };
        var result = Exports.Set<string>(source);

        Assert.Equal(3u, result.__Len__());
        Assert.Contains("a", result);
        Assert.Contains("b", result);
        Assert.Contains("c", result);
    }
}
