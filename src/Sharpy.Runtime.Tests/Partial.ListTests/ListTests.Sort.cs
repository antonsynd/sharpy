using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Sort_Empty()
    {
        // If
        List<int> l = [];

        // When
        l.Sort();

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Sort()
    {
        // If
        List<int> l = [7, 3, 1, 1, 5];

        // When
        l.Sort();

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_Reverse()
    {
        // If
        List<int> l = [7, 3, 1, 1, 5];

        // When
        l.Sort(true);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [7, 5, 3, 1, 1];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_Reverse_Empty()
    {
        // If
        List<int> l = [];

        // When
        l.Sort(true);

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Sort_With_Key()
    {
        // If
        List<int> l = [7, 3, 1, 1, 5];

        // This effectively inverts the sort
        var key = (int i) =>
        {
            return 1.0 / i;
        };

        // When
        l.Sort(key);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [7, 5, 3, 1, 1];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_Empty_With_Key()
    {
        // If
        List<int> l = [];

        // This effectively inverts the sort
        var key = (int i) =>
        {
            return 1.0 / i;
        };

        // When
        l.Sort(key);

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Sort_With_Key_And_Reverse()
    {
        // If
        List<int> l = [7, 3, 1, 1, 5];

        // This effectively inverts the sort, but the reverse reverses it again
        var key = (int i) =>
        {
            return 1.0 / i;
        };

        // When
        l.Sort(key, true);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Sort_Empty_With_Key_And_Reverse()
    {
        // If
        List<int> l = [];

        // This effectively inverts the sort
        var key = (int i) =>
        {
            return 1.0 / i;
        };

        // When
        l.Sort(key, true);

        // Then
        Len(l).Should().Be(0);
    }
}
