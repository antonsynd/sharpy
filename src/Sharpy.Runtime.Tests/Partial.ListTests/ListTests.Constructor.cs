using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_No_Args_Constructor()
    {
        // If/when
        var l = new List<int>();

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Empty_Initializer_List()
    {
        // If/when
        List<int> l = [];

        // Then
        Len(l).Should().Be(0);

        var actual = l.ToList<int>();
        actual.Count.Should().Be(0);
    }

    [Fact]
    public void List_Initializer_List()
    {
        // If/when
        List<int> l = [1, 3, 5, 7];

        // Then
        Len(l).Should().Be(4);

        var actual = l.ToList<int>();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Empty_Iterable_Constructor()
    {
        // If/when
        List<int> source = [];
        var l = new List<int>(Iter(source));

        // Then
        Len(l).Should().Be(0);

        var actual = l.ToList<int>();
        actual.Count.Should().Be(0);
    }

    [Fact]
    public void List_Iterable_Constructor()
    {
        // If/when
        List<int> source = [1, 3, 5, 7];
        var l = new List<int>(Iter(source));

        // Then
        Len(l).Should().Be(4);

        var actual = l.ToList<int>();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }
}
