using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public partial class List_Tests
{
    [Fact]
    public void List_Native_Iteration()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var expected = l.ToList();

        // When
        DotNetList<int> actual = [];

        foreach (var elem in l)
        {
            actual.Add(elem);
        }

        // Then
        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Iterator_Iteration()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var expected = l.ToList();
        var it = Iter(l);

        // When
        DotNetList<int> actual = [];

        foreach (var elem in it)
        {
            actual.Add(elem);
        }

        // Then
        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Iterator_Iteration_GetEnumerator()
    {
        // If
        List<int> l = [1, 3, 5, 7];
        var expected = l.ToList();

        // When
        DotNetList<int> actual = [];
        var enumerator = l.GetEnumerator();

        while (enumerator.MoveNext())
        {
            actual.Add(enumerator.Current);
        }

        // Then
        actual.Should().Equal(expected);
    }

    [Fact]
    public void List_Concrete_GetEnumerator_Is_A_Value_Type()
    {
        // The concrete foreach enumerator is a struct, so foreach over a
        // concrete List<T> allocates no iterator on the heap.
        typeof(List<int>.Enumerator).IsValueType.Should().BeTrue();
    }

    [Fact]
    public void List_Interface_GetEnumerator_Returns_Class_Iterator()
    {
        // If — interface-based iteration keeps the Python-protocol class iterator.
        List<int> l = [1, 3, 5, 7];

        // When
        var enumerator = ((System.Collections.Generic.IEnumerable<int>)l).GetEnumerator();

        // Then
        enumerator.Should().BeOfType<ListIterator<int>>();
    }

    [Fact]
    public void List_Struct_And_Class_Enumerators_Yield_Identical_Sequences()
    {
        // If
        List<int> l = [9, 4, 7, 2, 2, 0];

        // When — struct path (concrete) and class path (interface).
        DotNetList<int> viaStruct = [];
        foreach (var elem in l)
        {
            viaStruct.Add(elem);
        }

        DotNetList<int> viaClass = [];
        var classEnumerator = ((System.Collections.Generic.IEnumerable<int>)l).GetEnumerator();
        while (classEnumerator.MoveNext())
        {
            viaClass.Add(classEnumerator.Current);
        }

        // Then
        viaStruct.Should().Equal(viaClass);
        viaStruct.Should().Equal(l.ToList());
    }

    [Fact]
    public void List_Struct_Enumerator_Reset_Throws_Like_Class_Iterator()
    {
        // Mirrors ListIterator, which is single-pass and cannot be reset.
        List<int> l = [1, 2, 3];
        var enumerator = l.GetEnumerator();

        var act = () =>
        {
            System.Collections.Generic.IEnumerator<int> e = enumerator;
            e.Reset();
        };

        act.Should().Throw<System.NotSupportedException>();
    }

    [Fact]
    public void List_Empty_Concrete_Iteration()
    {
        // If
        List<int> l = [];

        // When
        DotNetList<int> actual = [];
        foreach (var elem in l)
        {
            actual.Add(elem);
        }

        // Then
        actual.Should().BeEmpty();
    }
}
