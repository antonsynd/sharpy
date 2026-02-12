using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Addition_Assignment_Operator()
    {
        // If
        List<int> l = [9, 11, 13];
        List<int> other = [1, 3, 5, 7];

        // When
        l += other;

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [9, 11, 13, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Addition_Assignment_Operator_Null()
    {
        // If
        List<int> l = [9, 11, 13];

        // When
        FluentActions.Invoking(() => l += (List<int>)null).Should().Throw<TypeError>();
    }

    [Fact]
    public void List_Addition_Operator()
    {
        // If
        List<int> l = [9, 11, 13];
        List<int> other = [1, 3, 5, 7];

        // When
        var sum = l + other;

        // Then
        var actual = sum.ToList();
        DotNetList<int> expected = [9, 11, 13, 1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Addition_Operator_Null()
    {
        // If
        List<int> l = [9, 11, 13];

        // When
        FluentActions.Invoking(() => l + (List<int>)null).Should().Throw<TypeError>();
    }

    [Fact]
    public void List_Addition_Operator_Null_Reverse()
    {
        // If
        List<int> l = [9, 11, 13];

        // When
        FluentActions.Invoking(() => (List<int>)null + l).Should().Throw<TypeError>();
    }

}
