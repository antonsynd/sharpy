using System;
using Xunit;
using Sharpy;

namespace Sharpy.Core.Tests;

public class TupleConversionTests
{
    [Fact]
    public void Tuple_FromIEnumerable_TwoElements_ReturnsTuple()
    {
        var source = new System.Collections.Generic.List<object> { 1, "hello" };
        var result = Builtins.Tuple<int, string>(source);

        Assert.Equal(1, result.Item1);
        Assert.Equal("hello", result.Item2);
    }

    [Fact]
    public void Tuple_FromIEnumerable_ThreeElements_ReturnsTuple()
    {
        var source = new System.Collections.Generic.List<object> { 1, "hello", 3.14 };
        var result = Builtins.Tuple<int, string, double>(source);

        Assert.Equal(1, result.Item1);
        Assert.Equal("hello", result.Item2);
        Assert.Equal(3.14, result.Item3);
    }

    [Fact]
    public void Tuple_FromIEnumerable_WrongCountTwoElements_ThrowsValueError()
    {
        var source = new System.Collections.Generic.List<object> { 1, 2, 3 };

        var ex = Assert.Throws<ValueError>(() => Builtins.Tuple<int, int>(source));
        Assert.Contains("Expected 2 items", ex.Message, StringComparison.Ordinal);
        Assert.Contains("got 3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tuple_FromIEnumerable_WrongCountThreeElements_ThrowsValueError()
    {
        var source = new System.Collections.Generic.List<object> { 1, 2 };

        var ex = Assert.Throws<ValueError>(() => Builtins.Tuple<int, int, int>(source));
        Assert.Contains("Expected 3 items", ex.Message, StringComparison.Ordinal);
        Assert.Contains("got 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tuple_FromIterable_TwoElements_ReturnsTuple()
    {
        var list = new List<object>();
        list.Add(42);
        list.Add("world");

        var result = Builtins.Tuple<int, string>(list);

        Assert.Equal(42, result.Item1);
        Assert.Equal("world", result.Item2);
    }

    [Fact]
    public void Tuple_FromIterable_ThreeElements_ReturnsTuple()
    {
        var list = new List<object>();
        list.Add(100);
        list.Add("test");
        list.Add(true);

        var result = Builtins.Tuple<int, string, bool>(list);

        Assert.Equal(100, result.Item1);
        Assert.Equal("test", result.Item2);
        Assert.True(result.Item3);
    }

    [Fact]
    public void Tuple_FromIterable_WrongCountTwoElements_ThrowsValueError()
    {
        var list = new List<object>();
        list.Add(1);

        var ex = Assert.Throws<ValueError>(() => Builtins.Tuple<int, int>(list));
        Assert.Contains("Expected 2 items", ex.Message, StringComparison.Ordinal);
        Assert.Contains("got 1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tuple_FromIterable_WrongCountThreeElements_ThrowsValueError()
    {
        var list = new List<object>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        var ex = Assert.Throws<ValueError>(() => Builtins.Tuple<int, int, int>(list));
        Assert.Contains("Expected 3 items", ex.Message, StringComparison.Ordinal);
        Assert.Contains("got 4", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tuple_FromRange_TwoElements_ReturnsTuple()
    {
        var list = new List<object>();
        list.Add(10);
        list.Add(11);

        var result = Builtins.Tuple<int, int>(list);

        Assert.Equal(10, result.Item1);
        Assert.Equal(11, result.Item2);
    }

    [Fact]
    public void Tuple_FromRange_ThreeElements_ReturnsTuple()
    {
        var list = new List<object>();
        list.Add(5);
        list.Add(6);
        list.Add(7);

        var result = Builtins.Tuple<int, int, int>(list);

        Assert.Equal(5, result.Item1);
        Assert.Equal(6, result.Item2);
        Assert.Equal(7, result.Item3);
    }

    [Fact]
    public void Tuple_MixedTypes_ReturnsTuple()
    {
        var source = new System.Collections.Generic.List<object> { 42, "hello" };
        var result = Builtins.Tuple<int, string>(source);

        Assert.Equal(42, result.Item1);
        Assert.Equal("hello", result.Item2);
    }

    [Fact]
    public void Tuple_ThreeElementsMixedTypes_ReturnsTuple()
    {
        var source = new System.Collections.Generic.List<object> { true, 3.14, "test" };
        var result = Builtins.Tuple<bool, double, string>(source);

        Assert.True(result.Item1);
        Assert.Equal(3.14, result.Item2);
        Assert.Equal("test", result.Item3);
    }
}
