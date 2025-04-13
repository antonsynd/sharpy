using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Extend_Empty_And_Empty_Other()
    {
        // If
        var l = new List<int>();
        var other = new List<int>();

        // When
        l.Extend(other);

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Extend_Empty_And_Non_Empty_Other()
    {
        // If
        var l = new List<int>();
        List<int> other = [1, 3, 5, 7];

        // When
        l.Extend(other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Extend_Non_Empty_And_Non_Empty_Other()
    {
        // If
        List<int> l = [9, 11, 13];
        List<int> other = [1, 3, 5, 7];

        // When
        l.Extend(other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [9, 11, 13, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }
}
