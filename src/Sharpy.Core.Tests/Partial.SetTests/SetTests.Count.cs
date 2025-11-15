using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Count_Property_Empty()
    {
        // If
        var s = new Set<int>();

        // When/then
        ((ICollection<int>)s).Count.Should().Be(0);
    }

    [Fact]
    public void Set_Count_Property_Non_Empty()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        ((ICollection<int>)s).Count.Should().Be(4);
    }
}
