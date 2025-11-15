using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Bool_Empty()
    {
        // If
        var s = new Set<int>();

        // When/then
        Bool(s).Should().BeFalse();
        s.__Bool__().Should().BeFalse();
    }

    [Fact]
    public void Set_Bool_Non_Empty()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When/then
        Bool(s).Should().BeTrue();
        s.__Bool__().Should().BeTrue();
    }
}
