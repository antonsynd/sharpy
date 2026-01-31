using Xunit;
using Sharpy.Core;

namespace Sharpy.Core.Tests;

public class OptionalTests
{
    [Fact]
    public void Some_CreatesOptionalWithValue()
    {
        var opt = Optional<int>.Some(42);
        Assert.True(opt.IsSome);
        Assert.False(opt.IsNone);
        Assert.Equal(42, opt.Unwrap());
    }

    [Fact]
    public void None_CreatesEmptyOptional()
    {
        var opt = Optional<int>.None;
        Assert.False(opt.IsSome);
        Assert.True(opt.IsNone);
    }

    [Fact]
    public void Unwrap_ThrowsOnNone()
    {
        var opt = Optional<int>.None;
        Assert.Throws<InvalidOperationException>(() => opt.Unwrap());
    }

    [Fact]
    public void UnwrapOr_ReturnsValueWhenSome()
    {
        var opt = Optional<int>.Some(42);
        Assert.Equal(42, opt.UnwrapOr(0));
    }

    [Fact]
    public void UnwrapOr_ReturnsDefaultWhenNone()
    {
        var opt = Optional<int>.None;
        Assert.Equal(0, opt.UnwrapOr(0));
    }

    [Fact]
    public void UnwrapOrElse_DoesNotCallFuncWhenSome()
    {
        var called = false;
        var opt = Optional<int>.Some(42);
        var result = opt.UnwrapOrElse(() => { called = true; return 0; });
        Assert.Equal(42, result);
        Assert.False(called);
    }

    [Fact]
    public void UnwrapOrElse_CallsFuncWhenNone()
    {
        var opt = Optional<int>.None;
        var result = opt.UnwrapOrElse(() => 99);
        Assert.Equal(99, result);
    }

    [Fact]
    public void Map_TransformsValueWhenSome()
    {
        var opt = Optional<int>.Some(42);
        var mapped = opt.Map(x => x.ToString());
        Assert.True(mapped.IsSome);
        Assert.Equal("42", mapped.Unwrap());
    }

    [Fact]
    public void Map_ReturnsNoneWhenNone()
    {
        var opt = Optional<int>.None;
        var mapped = opt.Map(x => x.ToString());
        Assert.True(mapped.IsNone);
    }

    [Fact]
    public void Equality_SomeValuesEqual()
    {
        var a = Optional<int>.Some(42);
        var b = Optional<int>.Some(42);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_NonesEqual()
    {
        var a = Optional<int>.None;
        var b = Optional<int>.None;
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_SomeAndNoneNotEqual()
    {
        var some = Optional<int>.Some(42);
        var none = Optional<int>.None;
        Assert.NotEqual(some, none);
        Assert.True(some != none);
    }

    [Fact]
    public void StaticSome_InfersTypeCorrectly()
    {
        var opt = Optional.Some(42);
        Assert.True(opt.IsSome);
        Assert.Equal(42, opt.Unwrap());
    }

    [Fact]
    public void ToString_ShowsSomeForValue()
    {
        var opt = Optional<int>.Some(42);
        Assert.Equal("Some(42)", opt.ToString());
    }

    [Fact]
    public void ToString_ShowsNoneForEmpty()
    {
        var opt = Optional<int>.None;
        Assert.Equal("None", opt.ToString());
    }
}
