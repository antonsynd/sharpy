using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
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

    [Fact]
    public void List_Set_Slice_Default_Step()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];
        List<int> other = [2, 4, 6];

        // When
        l[1, 1] = other;

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 4, 6, 3, 5, 1, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Set_Slice_Custom_Step()
    {
        // If
        List<int> l = [1, 3, 5, 7, 9];
        List<int> other = [2, 4];

        // When
        l[1, 4, 2] = other;

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 5, 4, 9];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Zero_Step()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];
        List<int> other = [2, 4, 6];

        // When/then
        FluentActions.Invoking(() => l.SetSlice(new Slice(0, 0, 0), other)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Negative_Step()
    {
        List<int> l = [1, 3, 5, 1, 7];
        List<int> other = [2, 4, 6];

        var act = () => l.SetSlice(new Slice(0, 1, -1), other);

        act.Should().Throw<NotImplementedError>();
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Same_Start_And_End_More_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];
        List<int> other = [2, 4, 6];

        // When
        l.SetSlice(new Slice(1, 1), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 4, 6, 3, 5, 1, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Same_Start_And_End_Less_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];
        List<int> other = [2, 4];

        // When
        l.SetSlice(new Slice(1, 1), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 4, 3, 5, 1, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Single_Step_More_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2, 4, 6];

        // When
        l.SetSlice(new Slice(1, 3), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 4, 6, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Single_Step_Less_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2];

        // When
        l.SetSlice(new Slice(1, 3), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Single_Step_Same_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2, 4];

        // When
        l.SetSlice(new Slice(1, 3), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 4, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Not_Single_Step_Not_Same_Num_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2, 4, 6];

        // When/then
        FluentActions.Invoking(() => l.SetSlice(new Slice(1, 3, 4), other)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Not_Single_Step_Same_Num_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7, 9];
        List<int> other = [2, 4];

        // When
        l.SetSlice(new Slice(1, 4, 2), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 5, 4, 9];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Not_Single_Step_Same_Start_And_End_Not_Same_Num_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2, 4, 6];

        // When/then
        FluentActions.Invoking(() => l.SetSlice(new Slice(1, 1, 4), other)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Dunder_Replace_Slice_Not_Single_Step_Same_Start_And_End_Same_Num_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7, 9];
        List<int> other = [];

        // When
        l.SetSlice(new Slice(1, 1, 2), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 5, 7, 9];

        actual.Should().Equal(expected);
    }
}
