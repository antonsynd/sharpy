using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Index_Empty()
    {
        // If
        var l = new List<int>();

        // When/then
        FluentActions.Invoking(() => l.Index(5)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Index_Non_Empty()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l.Index(5).Should().Be(2);
    }

    [Fact]
    public void List_Index_Non_Empty_Object_Equal()
    {
        // If
        List<IntWrapper> l = [1, 3, 5, 7];

        IntWrapper i = 5;

        // When/then
        l.Index(i).Should().Be(2);
    }

    [Fact]
    public void List_Index_Non_Empty_Object_Not_Equal()
    {
        // If
        List<IntWrapper> l = [1, 3, 5, 7];

        IntWrapper i = 4;

        // When/then
        FluentActions.Invoking(() => l.Index(i)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Index_Non_Empty_Object_Same()
    {
        // If
        List<IntIdentityWrapper> l = [1, 3, 5, 7];

        var i = l[2];

        // When/then
        l.Index(i).Should().Be(2);
    }

    [Fact]
    public void List_Index_Non_Empty_Object_Not_Same()
    {
        // If
        List<IntIdentityWrapper> l = [1, 3, 5, 7];

        IntIdentityWrapper i = 5;

        // When/then
        FluentActions.Invoking(() => l.Index(i)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Index_Default_End_Includes_Last_Element()
    {
        // #1073: the default end must search the whole list, including the
        // last element.
        List<int> l = [0, 1];

        l.Index(1).Should().Be(1);
    }

    [Fact]
    public void List_Index_Explicit_Negative_One_End_Excludes_Last()
    {
        // Explicit end=-1 is interpreted in slice notation and excludes the
        // last element (matching CPython).
        List<int> l = [0, 1];

        FluentActions.Invoking(() => l.Index(1, 0, -1)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Index_Start_Greater_Than_End_Throws()
    {
        // An empty search window (start > end) finds nothing.
        List<int> l = [0, 1];

        FluentActions.Invoking(() => l.Index(1, 5, 2)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Index_End_Beyond_Count_Is_Clamped()
    {
        // An out-of-range end clamps to Count instead of throwing.
        List<int> l = [1, 2, 3];

        l.Index(2, 0, 100).Should().Be(1);
    }
}
