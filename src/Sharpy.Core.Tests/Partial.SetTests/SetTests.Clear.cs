using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Clear_Empty()
    {
        // If
        var s = new Set<int>();

        // When
        s.Clear();

        // Then
        Len(s).Should().Be(0);
    }

    [Fact]
    public void Set_Clear_Non_Empty()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When
        s.Clear();

        // Then
        Len(s).Should().Be(0);
    }
}
