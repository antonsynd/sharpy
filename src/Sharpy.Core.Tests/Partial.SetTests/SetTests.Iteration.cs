using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

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
    public void Set_Iterator_Iteration_GetEnumerator()
    {
        // If
        Set<int> s = [1, 3, 5, 7];
        var expected = s.ToHashSet();

        // When
        HashSet<int> actual = [];
        var enumerator = s.GetEnumerator();

        while (enumerator.MoveNext())
        {
            actual.Add(enumerator.Current);
        }

        // Then
        actual.Should().Equal(expected);
    }
}
