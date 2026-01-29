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
        Assert.False(opt.IsNothing);
        Assert.Equal(42, opt.Unwrap());
    }

    [Fact]
    public void Nothing_CreatesEmptyOptional()
    {
        var opt = Optional<int>.Nothing;
        Assert.False(opt.IsSome);
        Assert.True(opt.IsNothing);
    }

    [Fact]
    public void Unwrap_ThrowsOnNothing()
    {
        var opt = Optional<int>.Nothing;
        Assert.Throws<InvalidOperationException>(() => opt.Unwrap());
    }

    [Fact]
    public void UnwrapOr_ReturnsValueWhenSome()
    {
        var opt = Optional<int>.Some(42);
        Assert.Equal(42, opt.UnwrapOr(0));
    }

    [Fact]
    public void UnwrapOr_ReturnsDefaultWhenNothing()
    {
        var opt = Optional<int>.Nothing;
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
    public void UnwrapOrElse_CallsFuncWhenNothing()
    {
        var opt = Optional<int>.Nothing;
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
    public void Map_ReturnsNothingWhenNothing()
    {
        var opt = Optional<int>.Nothing;
        var mapped = opt.Map(x => x.ToString());
        Assert.True(mapped.IsNothing);
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
    public void Equality_NothingsEqual()
    {
        var a = Optional<int>.Nothing;
        var b = Optional<int>.Nothing;
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_SomeAndNothingNotEqual()
    {
        var some = Optional<int>.Some(42);
        var nothing = Optional<int>.Nothing;
        Assert.NotEqual(some, nothing);
        Assert.True(some != nothing);
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
    public void ToString_ShowsNothingForEmpty()
    {
        var opt = Optional<int>.Nothing;
        Assert.Equal("Nothing", opt.ToString());
    }
}
