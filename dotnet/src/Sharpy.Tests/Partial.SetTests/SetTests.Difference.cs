using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Difference_Non_Empty()
    {
        // If
        Set<int> s1 = [1, 3, 5, 7, 9];
        Set<int> s2 = [3, 7];

        // When
        var actual = s1.Difference(s2).ToHashSet();

        // Then
        HashSet<int> expected = [1, 5, 9];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void Set_Difference_Non_Empty_Right_Bigger()
    {
        // If
        Set<int> s1 = [1, 3, 5, 7, 9];
        Set<int> s2 = [3, 7, 11, 15, 19];

        // When
        var actual = s1.Difference(s2).ToHashSet();

        // Then
        HashSet<int> expected = [1, 5, 9];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void Set_Difference_No_Overlap()
    {
        // If
        Set<int> s1 = [1, 3, 5, 7, 9];
        Set<int> s2 = [11, 15, 19];

        // When
        var actual = s1.Difference(s2).ToHashSet();

        // Then
        HashSet<int> expected = [1, 3, 5, 7, 9];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void Set_Difference_Left_Empty()
    {
        // If
        Set<int> s1 = [];
        Set<int> s2 = [3, 7];

        // When/then
        Len(s1.Difference(s2)).Should().Be(0);
    }

    [Fact]
    public void Set_Difference_Right_Empty()
    {
        // If
        Set<int> s1 = [1, 3, 5, 7, 9];
        Set<int> s2 = [];

        // When
        var actual = s1.Difference(s2).ToHashSet();

        // Then
        HashSet<int> expected = [1, 3, 5, 7, 9];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void Set_Difference_Equal()
    {
        // If
        Set<int> s1 = [1, 3, 5, 7, 9];
        Set<int> s2 = [9, 7, 5, 3, 1];

        // When/then
        Len(s1.Difference(s2)).Should().Be(0);
    }
}
