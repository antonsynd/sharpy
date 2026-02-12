using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Len_Zero()
    {
        // If
        var s = new Set<int>();

        // When/then
        Len(s).Should().Be(0);
        s.Count.Should().Be(0);
    }

    [Fact]
    public void Set_Len_Non_Zero()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        Len(s).Should().Be(4);
        s.Count.Should().Be(4);
    }
}
