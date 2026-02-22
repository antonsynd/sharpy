using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Indexer_Int_Get_By_Positive_Index()
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
    public void List_Indexer_Int_Get_By_Negative_Index()
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
    public void List_Indexer_Int_Get_By_Out_Of_Bounds()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When/then
        FluentActions.Invoking(() => { var _ = l[-5]; }).Should().Throw<IndexError>();
        FluentActions.Invoking(() => { var _ = l[4]; }).Should().Throw<IndexError>();
    }

    [Fact]
    public void List_Indexer_Slice()
    {
        // If
        List<int> l = [1, 3, 5, 7, 9];

        // When
        var res = l[1, 5, 2];

        // Then
        var actual = res.ToList();
        DotNetList<int> expected = [3, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Indexer_Slice_Object()
    {
        // If
        List<IntWrapper> l = [1, 3, 5, 7, 9];

        // When
        var res = l[1, 5, 2];

        // Then
        var actual = res.ToList();
        DotNetList<IntWrapper> expected = [3, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Slice_Zero_Step()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];

        // When/then
        FluentActions.Invoking(() => l.GetSlice(new Slice(0, 0, 0))).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Dunder_Slice_Negative_Step()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];

        // When
        var actual = l.GetSlice(new Slice(0, 1, -1));

        // Then
        Len(actual).Should().Be(0);
    }

    [Fact]
    public void List_Dunder_Slice_Same_Start_And_End()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];

        // When
        var actual = l.GetSlice(new Slice(1, 1));

        // Then
        Len(actual).Should().Be(0);
    }

    [Fact]
    public void List_Dunder_Slice_Single_Step()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var res = l.GetSlice(new Slice(1, 3));

        // Then
        var actual = res.ToList();
        DotNetList<int> expected = [3, 5];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Slice_Not_Single_Step_Not_Enough()
    {
        // If
        List<int> l = [1, 3, 5, 7];

        // When
        var res = l.GetSlice(new Slice(1, 3, 4));

        // Then
        var actual = res.ToList();
        DotNetList<int> expected = [3];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Slice_Not_Single_Step_Enough()
    {
        // If
        List<int> l = [1, 3, 5, 7, 9];

        // When
        var res = l.GetSlice(new Slice(1, 5, 2));

        // Then
        var actual = res.ToList();
        DotNetList<int> expected = [3, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Slice_Out_Of_Bounds_Left()
    {
        // If
        List<int> l = [1, 3, 5, 7, 9];

        // When
        var res = l.GetSlice(new Slice(-9, 4, 2));

        // Then
        var actual = res.ToList();
        DotNetList<int> expected = [1, 5];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Slice_Out_Of_Bounds_Right()
    {
        // If
        List<int> l = [1, 3, 5, 7, 9];

        // When
        var res = l.GetSlice(new Slice(0, 9, 2));

        // Then
        var actual = res.ToList();
        DotNetList<int> expected = [1, 5, 9];

        actual.Should().Equal(expected);
    }
}
