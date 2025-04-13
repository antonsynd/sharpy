using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_No_Args_Constructor()
    {
        // If/when
        var l = new Set<int>();

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void Set_Empty_Initializer_Set()
    {
        // If/when
        Set<int> l = [];

        // Then
        Len(l).Should().Be(0);

        var actual = l.ToHashSet<int>();
        actual.Count.Should().Be(0);
    }

    [Fact]
    public void Set_Initializer_Set()
    {
        // If/when
        Set<int> l = [1, 3, 5, 7];

        // Then
        Len(l).Should().Be(4);

        var actual = l.ToHashSet<int>();
        HashSet<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void Set_Empty_Iterable_Constructor()
    {
        // If/when
        Set<int> source = [];
        var l = new Set<int>(Iter(source));

        // Then
        Len(l).Should().Be(0);

        var actual = l.ToHashSet<int>();
        actual.Count.Should().Be(0);
    }

    [Fact]
    public void Set_Iterable_Constructor()
    {
        // If/when
        Set<int> source = [1, 3, 5, 7];
        var l = new Set<int>(Iter(source));

        // Then
        Len(l).Should().Be(4);

        var actual = l.ToHashSet<int>();
        HashSet<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }
}
