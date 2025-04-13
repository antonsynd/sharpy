using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Str_Empty()
    {
        // If
        var l = new List<int>();

        // When/then
        Str(l).Should().Be("[]");
    }

    [Fact]
    public void List_Str_Not_Empty()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        Str(l).Should().Be("[1, 3, 5, 7]");
    }
}
