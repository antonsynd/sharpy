using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Copy_Empty()
    {
        // If
        var l = new List<int>();

        // When
        var copy = l.Copy();
        copy.Append(5);

        // Then
        l.Should().NotEqual(copy);
        Len(l).Should().NotBe(Len(copy));
    }

    [Fact]
    public void List_Copy_Non_Empty()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var copy = l.Copy();
        copy.Append(9);

        // Then
        var actual_l_items = l.ToList();
        DotNetList<int> expected_l_items = [1, 3, 5, 7];
        actual_l_items.Should().Equal(expected_l_items);

        var actual_copy_items = copy.ToList();
        DotNetList<int> expected_copy_items = [1, 3, 5, 7, 9];
        actual_copy_items.Should().Equal(expected_copy_items);
    }
}
