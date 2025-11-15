using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Insert_Into_Empty()
    {
        // If
        var l = new List<int>();

        // When
        l.Insert(0, 5);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [5];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Insert_Into_Non_Empty()
    {
        // If
        List<int> l = [1, 3, 7];

        // When
        l.Insert(1, 5);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 5, 3, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Insert_Into_Non_Empty_Beyond_Left_Bound()
    {
        // If
        List<int> l = [1, 3, 7];

        // When
        l.Insert(-100, 5);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [5, 1, 3, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Insert_Into_Non_Empty_Beyond_Right_Bound()
    {
        // If
        List<int> l = [1, 3, 7];

        // When
        l.Insert(100, 5);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 7, 5];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Insert_Into_Non_Empty_At_Left_Bound()
    {
        // If
        List<int> l = [1, 3, 7];

        // When
        l.Insert(0, 5);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [5, 1, 3, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Insert_Into_Non_Empty_Before_Right_Bound()
    {
        // If
        List<int> l = [1, 3, 7];

        // When
        l.Insert(-1, 5);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Insert_Into_Non_Empty_At_Right_Bound()
    {
        // If
        List<int> l = [1, 3, 7];

        // When
        l.Insert(3, 5);

        // Then
        var actual = l.ToList();
        DotNetList<int> expected = [1, 3, 7, 5];

        actual.Should().Equal(expected);
    }
}
