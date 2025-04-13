using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Get_By_Positive_Index()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l[0].Should().Be(1);
        l[1].Should().Be(3);
        l[2].Should().Be(5);
        l[3].Should().Be(7);
    }

    [Fact]
    public void List_Get_By_Negative_Index()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        l[-1].Should().Be(7);
        l[-2].Should().Be(5);
        l[-3].Should().Be(3);
        l[-4].Should().Be(1);
    }

    [Fact]
    public void List_Get_By_Out_Of_Bounds()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        FluentActions.Invoking(() => { var _ = l[-5]; }).Should().Throw<IndexError>();
        FluentActions.Invoking(() => { var _ = l[4]; }).Should().Throw<IndexError>();
    }

    [Fact]
    public void List_Set_By_Positive_Index()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l[2] = 6;

        // Then
        l[0].Should().Be(1);
        l[1].Should().Be(3);
        l[2].Should().Be(6);
        l[3].Should().Be(7);
    }

    [Fact]
    public void List_Set_By_Negative_Index()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        l[-3] = 4;

        // Then
        l[-1].Should().Be(7);
        l[-2].Should().Be(5);
        l[-3].Should().Be(4);
        l[-4].Should().Be(1);
    }

    [Fact]
    public void List_Set_By_Out_Of_Bounds()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        FluentActions.Invoking(() => { l[-5] = 9; }).Should().Throw<IndexError>();
        FluentActions.Invoking(() => { l[4] = 11; }).Should().Throw<IndexError>();
    }
}
