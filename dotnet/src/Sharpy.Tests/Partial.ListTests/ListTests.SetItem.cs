using Xunit;
using FluentAssertions;

namespace Sharpy.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Replace_Slice_Zero_Step()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];
        List<int> other = [2, 4, 6];

        // When/then
        FluentActions.Invoking(() => l.__SetItem__(new Slice(0, 0, 0), other)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Replace_Slice_Negative_Step()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];
        List<int> other = [2, 4, 6];

        // When
        l.__SetItem__(new Slice(0, 1, -1), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 5, 1, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Replace_Slice_Same_Start_And_End_More_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];
        List<int> other = [2, 4, 6];

        // When
        l.__SetItem__(new Slice(1, 1), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 4, 6, 3, 5, 1, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Replace_Slice_Same_Start_And_End_Less_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 1, 7];
        List<int> other = [2, 4];

        // When
        l.__SetItem__(new Slice(1, 1), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 4, 3, 5, 1, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Replace_Slice_Single_Step_More_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2, 4, 6];

        // When
        l.__SetItem__(new Slice(1, 3), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 4, 6, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Replace_Slice_Single_Step_Less_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2];

        // When
        l.__SetItem__(new Slice(1, 3), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Replace_Slice_Single_Step_Same_New_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2, 4];

        // When
        l.__SetItem__(new Slice(1, 3), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 4, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Replace_Slice_Not_Single_Step_Not_Same_Num_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2, 4, 6];

        // When/then
        FluentActions.Invoking(() => l.__SetItem__(new Slice(1, 3, 4), other)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Replace_Slice_Not_Single_Step_Same_Num_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7, 9];
        List<int> other = [2, 4];

        // When
        l.__SetItem__(new Slice(1, 4, 2), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 2, 5, 4, 9];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Replace_Slice_Not_Single_Step_Same_Start_And_End_Not_Same_Num_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        List<int> other = [2, 4, 6];

        // When/then
        FluentActions.Invoking(() => l.__SetItem__(new Slice(1, 1, 4), other)).Should().Throw<ValueError>();
    }

    [Fact]
    public void List_Replace_Slice_Not_Single_Step_Same_Start_And_End_Same_Num_Elems()
    {
        // If
        List<int> l = [1, 3, 5, 7, 9];
        List<int> other = [];

        // When
        l.__SetItem__(new Slice(1, 1, 2), other);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 5, 7, 9];

        actual.Should().Equal(expected);
    }
}
