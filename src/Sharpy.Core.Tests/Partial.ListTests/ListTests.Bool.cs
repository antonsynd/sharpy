using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Bool_Empty()
    {
        // If
        var l = new List<int>();

        // When/then - test operator true/false
        if (l)
        {
            true.Should().BeFalse("empty list should be falsy");
        }
        Bool(l).Should().BeFalse();
    }

    [Fact]
    public void List_Bool_Non_Empty()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then - test operator true/false
        if (l)
        {
            // This should execute
        }
        else
        {
            true.Should().BeFalse("non-empty list should be truthy");
        }
        Bool(l).Should().BeTrue();
    }

    [Fact]
    public void List_Bool_Null()
    {
        // If
        List<int>? l = null;

        // When/then - null list should be falsy
        if (l)
        {
            true.Should().BeFalse("null list should be falsy");
        }
    }
}
