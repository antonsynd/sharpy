using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Len_Zero()
    {
        // If
        var l = new List<int>();

        // When/then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Len_Non_Zero()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        Len(l).Should().Be(4);
    }
}
