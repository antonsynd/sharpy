using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Contains_Empty()
    {
        // If
        var l = new List<int>();

        // When/then
        l.Contains(1).Should().BeFalse();
        l.__Contains__(1).Should().BeFalse();
    }

    [Fact]
    public void List_Contains_Not_Actually_In()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l.Contains(4).Should().BeFalse();
        l.__Contains__(4).Should().BeFalse();
    }

    [Fact]
    public void List_Contains_Actually_In()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l.Contains(5).Should().BeTrue();
        l.__Contains__(5).Should().BeTrue();
    }
}
