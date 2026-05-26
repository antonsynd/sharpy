using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Functools_Tests
{
    [Fact]
    public void Reduce_Sum_WithoutInitial()
    {
        var result = Sharpy.Functools.Reduce((x, y) => x + y, new Sharpy.List<int>(new[] { 1, 2, 3, 4, 5 }));
        result.Should().Be(15);
    }

    [Fact]
    public void Reduce_Sum_WithInitial()
    {
        var result = Sharpy.Functools.Reduce((x, y) => x + y, new Sharpy.List<int>(new[] { 1, 2, 3, 4, 5 }), 10);
        result.Should().Be(25);
    }

    [Fact]
    public void Reduce_SingleElement_WithoutInitial()
    {
        var result = Sharpy.Functools.Reduce((x, y) => x + y, new Sharpy.List<int>(new[] { 42 }));
        result.Should().Be(42);
    }

    [Fact]
    public void Reduce_EmptyIterable_WithoutInitial_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Sharpy.Functools.Reduce((x, y) => x + y, new Sharpy.List<int>()))
            .Should().Throw<Sharpy.TypeError>()
            .WithMessage("reduce() of empty iterable with no initial value");
    }

    [Fact]
    public void Reduce_EmptyIterable_WithInitial_ReturnsInitial()
    {
        var result = Sharpy.Functools.Reduce((x, y) => x + y, new Sharpy.List<int>(), 42);
        result.Should().Be(42);
    }

    [Fact]
    public void Reduce_StringConcatenation()
    {
        var result = Sharpy.Functools.Reduce((x, y) => x + y, new Sharpy.List<string>(new[] { "a", "b", "c" }));
        result.Should().Be("abc");
    }

    [Fact]
    public void Reduce_Product()
    {
        var result = Sharpy.Functools.Reduce((x, y) => x * y, new Sharpy.List<int>(new[] { 1, 2, 3, 4 }));
        result.Should().Be(24);
    }

    [Fact]
    public void Reduce_WithInitial_SingleElement()
    {
        var result = Sharpy.Functools.Reduce((x, y) => x + y, new Sharpy.List<int>(new[] { 5 }), 10);
        result.Should().Be(15);
    }

    [Fact]
    public void CmpToKey_ReturnsComparer()
    {
        var comparer = Sharpy.Functools.CmpToKey<int>((a, b) => a - b);
        comparer.Should().NotBeNull();
    }

    [Fact]
    public void CmpToKey_AscendingSort()
    {
        var comparer = Sharpy.Functools.CmpToKey<int>((a, b) => a - b);
        var list = new System.Collections.Generic.List<int> { 3, 1, 4, 1, 5, 9, 2, 6 };
        list.Sort(comparer.Compare);
        list.Should().Equal(1, 1, 2, 3, 4, 5, 6, 9);
    }

    [Fact]
    public void CmpToKey_DescendingSort()
    {
        var comparer = Sharpy.Functools.CmpToKey<int>((a, b) => b - a);
        var list = new System.Collections.Generic.List<int> { 3, 1, 4, 1, 5, 9, 2, 6 };
        list.Sort(comparer.Compare);
        list.Should().Equal(9, 6, 5, 4, 3, 2, 1, 1);
    }

    [Fact]
    public void CmpToKey_StringLengthSort()
    {
        var comparer = Sharpy.Functools.CmpToKey<string>((a, b) => a.Length - b.Length);
        var list = new System.Collections.Generic.List<string> { "hello", "hi", "hey" };
        list.Sort(comparer.Compare);
        list.Should().Equal("hi", "hey", "hello");
    }

    [Fact]
    public void Reduce_WithIEnumerable()
    {
        IEnumerable<int> seq = Enumerable.Range(1, 5);
        var result = Sharpy.Functools.Reduce((x, y) => x + y, new Sharpy.List<int>(seq));
        result.Should().Be(15);
    }

    [Fact]
    public void Reduce_LargeList_WithoutInitial()
    {
        // Sum of 1..1000 == 500500 (verified against CPython functools.reduce)
        var result = Sharpy.Functools.Reduce((x, y) => x + y, new Sharpy.List<int>(Enumerable.Range(1, 1000)));
        result.Should().Be(500500);
    }

    [Fact]
    public void Reduce_LeftFold_Subtraction_WithoutInitial()
    {
        // reduce(a-b, [10,1,2,3]) folds left-to-right: ((10-1)-2)-3 == 4
        var result = Sharpy.Functools.Reduce((x, y) => x - y, new Sharpy.List<int>(new[] { 10, 1, 2, 3 }));
        result.Should().Be(4);
    }

    [Fact]
    public void Reduce_LeftFold_Subtraction_WithInitial()
    {
        // reduce(a-b, [1,2,3], 100) == ((100-1)-2)-3 == 94
        var result = Sharpy.Functools.Reduce((x, y) => x - y, new Sharpy.List<int>(new[] { 1, 2, 3 }), 100);
        result.Should().Be(94);
    }

    [Fact]
    public void Reduce_SingleElement_DoesNotInvokeFunc()
    {
        // With a single element and no initializer, the accumulator is the lone
        // element and the reducer is never called.
        var result = Sharpy.Functools.Reduce<int>(
            (x, y) => throw new InvalidOperationException("reducer should not run"),
            new Sharpy.List<int>(new[] { 99 }));
        result.Should().Be(99);
    }

    [Fact]
    public void Reduce_EmptyIterable_WithInitial_DoesNotInvokeFunc()
    {
        var result = Sharpy.Functools.Reduce<int>(
            (x, y) => throw new InvalidOperationException("reducer should not run"),
            new Sharpy.List<int>(),
            7);
        result.Should().Be(7);
    }

    [Fact]
    public void CmpToKey_EmptyList_Sort_StaysEmpty()
    {
        var comparer = Sharpy.Functools.CmpToKey<int>((a, b) => a - b);
        var list = new System.Collections.Generic.List<int>();
        list.Sort(comparer.Compare);
        list.Should().BeEmpty();
    }

    [Fact]
    public void CmpToKey_SingleElement_Sort()
    {
        var comparer = Sharpy.Functools.CmpToKey<int>((a, b) => a - b);
        var list = new System.Collections.Generic.List<int> { 42 };
        list.Sort(comparer.Compare);
        list.Should().Equal(42);
    }

    [Fact]
    public void CmpToKey_AllEqual_PreservesAllElements()
    {
        // A comparer that reports every pair as equal must not lose or duplicate
        // elements during the sort.
        var comparer = Sharpy.Functools.CmpToKey<int>((a, b) => 0);
        var list = new System.Collections.Generic.List<int> { 3, 1, 2, 1, 3 };
        list.Sort(comparer.Compare);
        list.Should().HaveCount(5);
        list.Should().BeEquivalentTo(new[] { 1, 1, 2, 3, 3 });
    }
}
