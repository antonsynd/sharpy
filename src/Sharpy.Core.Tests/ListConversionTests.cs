using Xunit;
using Sharpy.Core;

namespace Sharpy.Core.Tests;

public class ListConversionTests
{
    [Fact]
    public void List_FromEmpty_ReturnsEmptyList()
    {
        var result = Exports.List<int>();
        Assert.NotNull(result);
        Assert.Equal(0u, result.__Len__());
    }

    [Fact]
    public void List_FromIEnumerable_ReturnsList()
    {
        var source = new System.Collections.Generic.List<int> { 1, 2, 3 };
        var result = Exports.List<int>(source);

        Assert.Equal(3u, result.__Len__());
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);
        Assert.Equal(3, result[2]);
    }

    [Fact]
    public void List_CopyConstructor_CreatesNewList()
    {
        var original = new List<int>();
        original.Add(1);
        original.Add(2);
        original.Add(3);

        var copy = Exports.List(original);

        Assert.Equal(3u, copy.__Len__());
        Assert.Equal(1, copy[0]);
        Assert.Equal(2, copy[1]);
        Assert.Equal(3, copy[2]);

        // Verify it's a copy, not the same instance
        original.Add(4);
        Assert.Equal(4u, original.__Len__());
        Assert.Equal(3u, copy.__Len__());
    }

    [Fact]
    public void List_FromIterable_ReturnsList()
    {
        var range = Exports.Range(1, 4); // 1, 2, 3
        var result = Exports.List(range);

        Assert.Equal(3u, result.__Len__());
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);
        Assert.Equal(3, result[2]);
    }
}
