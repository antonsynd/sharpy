using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Add_Item_New()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When
        s.Add(9);

        // Then
        Len(s).Should().Be(5);

        var actual = s.ToHashSet();
        HashSet<int> expected = [1, 3, 5, 7, 9];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void Set_Add_Item_Existing()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When
        s.Add(5);

        // Then
        Len(s).Should().Be(4);

        var actual = s.ToHashSet();
        HashSet<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }
}
