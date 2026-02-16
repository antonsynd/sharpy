using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_No_Args_Constructor()
    {
        // If/when
        var l = new List<int>();

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Empty_Initializer_List()
    {
        // If/when
        List<int> l = [];

        // Then
        Len(l).Should().Be(0);

        var actual = l.ToList<int>();
        actual.Count.Should().Be(0);
    }

    [Fact]
    public void List_Initializer_List()
    {
        // If/when
        List<int> l = [1, 3, 5, 7];

        // Then
        Len(l).Should().Be(4);

        var actual = l.ToList<int>();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Empty_Iterable_Constructor()
    {
        // If/when
        List<int> source = [];
        var l = new List<int>(Iter(source));

        // Then
        Len(l).Should().Be(0);

        var actual = l.ToList<int>();
        actual.Count.Should().Be(0);
    }

    [Fact]
    public void List_Iterable_Constructor()
    {
        // If/when
        List<int> source = [1, 3, 5, 7];
        var l = new List<int>(Iter(source));

        // Then
        Len(l).Should().Be(4);

        var actual = l.ToList<int>();
        DotNetList<int> expected = [1, 3, 5, 7];

        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Implicit_From_Array()
    {
        // If/when
        List<string> l = new string[] { "a", "b", "c" };

        // Then
        Len(l).Should().Be(3);
        l[0].Should().Be("a");
        l[1].Should().Be("b");
        l[2].Should().Be("c");
    }

    [Fact]
    public void List_Implicit_From_Empty_Array()
    {
        // If/when
        List<int> l = new int[0];

        // Then
        Len(l).Should().Be(0);
    }

    [Fact]
    public void List_Implicit_From_String_Split()
    {
        // If/when
        List<string> l = "hello world".Split(' ');

        // Then
        Len(l).Should().Be(2);
        l[0].Should().Be("hello");
        l[1].Should().Be("world");
    }
}
