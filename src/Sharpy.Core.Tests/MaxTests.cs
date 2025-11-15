using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Max_Tests
{
    [Fact]
    public void Max_List_Empty()
    {
        // If
        var l = new List<int>();

        // When/then
        FluentActions.Invoking(() => Max(l)).Should().Throw<ValueError>();
    }

    [Fact]
    public void Max_List_Non_Empty()
    {
        // If
        List<int> l = [5, 7, 3, 1];

        // When/then
        Max(l).Should().Be(7);
    }

    // [Fact]
    // public void Max_List_With_Nullable()
    // {
    //     // If
    //     List<Optional<int>> l = [ 5, 7, null, 1 ];

    //     // When/then
    //     Min(l, value => value.GetValue()).Should().Be(Some(7));
    // }
}
