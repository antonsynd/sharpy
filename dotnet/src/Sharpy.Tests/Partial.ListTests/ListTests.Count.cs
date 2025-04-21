using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Count_Empty()
    {
        // If
        var l = new List<int>();

        // When/then
        l.Count(1).Should().Be(0);
    }

    [Fact]
    public void List_Count_Zero()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l.Count(9).Should().Be(0);
    }

    [Fact]
    public void List_Count_Non_Zero()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];

        // When/then
        l.Count(1).Should().Be(2);
    }

    [Fact]
    public void List_Count_Property_Empty()
    {
        // If
        var l = new List<int>();

        // When/then
        ((ICollection<int>)l).Count.Should().Be(0);
    }

    [Fact]
    public void List_Count_Property_Non_Empty()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        ((ICollection<int>)l).Count.Should().Be(4);
    }
}
