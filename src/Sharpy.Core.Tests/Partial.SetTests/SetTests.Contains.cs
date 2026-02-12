using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Contains_Empty()
    {
        // If
        var s = new Set<int>();

        // When/then
        s.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void Set_Contains_Not_Actually_In()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        s.Contains(4).Should().BeFalse();
    }

    [Fact]
    public void Set_Contains_Actually_In()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        s.Contains(5).Should().BeTrue();
    }
}
