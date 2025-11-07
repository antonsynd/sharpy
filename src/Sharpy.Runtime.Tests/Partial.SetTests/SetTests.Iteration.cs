using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Native_Iteration()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var expected = s.ToHashSet();

        // When
        HashSet<int> actual = [];

        foreach (var elem in s)
        {
            actual.Add(elem);
        }

        // Then
        actual.Should().Equal(expected);
    }

    [Fact]
    public void Set_Iterator_Iteration()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var expected = s.ToHashSet();
        var it = Iter(s);

        // When
        HashSet<int> actual = [];

        foreach (var elem in it)
        {
            actual.Add(elem);
        }

        // Then
        actual.Should().Equal(expected);
    }

    [Fact]
    public void Set_Iterator_Iteration_Dunder()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var expected = s.ToHashSet();
        var it = s.__Iter__();

        // When
        HashSet<int> actual = [];

        foreach (var elem in it)
        {
            actual.Add(elem);
        }

        // Then
        actual.Should().Equal(expected);
    }
}
