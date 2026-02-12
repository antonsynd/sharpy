using Xunit;
using Sharpy;

namespace Sharpy.Core.Tests;

public class ListConversionTests
{
    [Fact]
    public void List_FromEmpty_ReturnsEmptyList()
    {
        var result = Builtins.List<int>();
        Assert.NotNull(result);
        Assert.Equal(0, Len(result));
    }

    [Fact]
    public void List_FromIEnumerable_ReturnsList()
    {
        var source = new System.Collections.Generic.List<int> { 1, 2, 3 };
        var result = Builtins.List<int>(source);

        Assert.Equal(3, Len(result));
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

        var copy = Builtins.List(original);

        Assert.Equal(3, Len(copy));
        Assert.Equal(1, copy[0]);
        Assert.Equal(2, copy[1]);
        Assert.Equal(3, copy[2]);

        // Verify it's a copy, not the same instance
        original.Add(4);
        Assert.Equal(4, Len(original));
        Assert.Equal(3, Len(copy));
    }

    [Fact]
    public void List_FromIterable_ReturnsList()
    {
        var range = Builtins.Range(1, 4); // 1, 2, 3
        var result = Builtins.List(range);

        Assert.Equal(3, Len(result));
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);
        Assert.Equal(3, result[2]);
    }
}
