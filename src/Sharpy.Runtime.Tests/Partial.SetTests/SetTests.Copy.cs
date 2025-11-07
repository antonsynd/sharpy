using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class Set_Tests
{
    [Fact]
    public void Set_Copy_Empty()
    {
        // If
        var s = new Set<int>();

        // When
        var copy = s.Copy();
        copy.Add(5);

        // Then
        s.Should().NotEqual(copy);
        Len(s).Should().NotBe(Len(copy));
    }

    [Fact]
    public void Set_Copy_Non_Empty()
    {
        // If
        Set<int> s = [1, 3, 5, 7];

        // When
        var copy = s.Copy();
        copy.Add(9);

        // Then
        var actual_l_items = s.ToHashSet();
        HashSet<int> expected_s_items = [1, 3, 5, 7];
        actual_l_items.Should().Equal(expected_s_items);

        var actual_copy_items = copy.ToHashSet();
        HashSet<int> expected_copy_items = [1, 3, 5, 7, 9];
        actual_copy_items.Should().Equal(expected_copy_items);
    }
}
