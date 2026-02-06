using Xunit;
using Sharpy;

namespace Sharpy.Core.Tests;

public class ResultTests
{
    [Fact]
    public void Ok_CreatesResultWithValue()
    {
        var result = Result<int, string>.Ok(42);
        Assert.True(result.IsOk);
        Assert.False(result.IsErr);
        Assert.Equal(42, result.Unwrap());
    }

    [Fact]
    public void Err_CreatesResultWithError()
    {
        var result = Result<int, string>.Err("failed");
        Assert.False(result.IsOk);
        Assert.True(result.IsErr);
        Assert.Equal("failed", result.UnwrapErr());
    }

    [Fact]
    public void Unwrap_ThrowsOnErr()
    {
        var result = Result<int, string>.Err("failed");
        var ex = Assert.Throws<InvalidOperationException>(() => result.Unwrap());
        Assert.Contains("failed", ex.Message);
    }

    [Fact]
    public void UnwrapErr_ThrowsOnOk()
    {
        var result = Result<int, string>.Ok(42);
        Assert.Throws<InvalidOperationException>(() => result.UnwrapErr());
    }

    [Fact]
    public void UnwrapOr_ReturnsValueWhenOk()
    {
        var result = Result<int, string>.Ok(42);
        Assert.Equal(42, result.UnwrapOr(0));
    }

    [Fact]
    public void UnwrapOr_ReturnsDefaultWhenErr()
    {
        var result = Result<int, string>.Err("failed");
        Assert.Equal(0, result.UnwrapOr(0));
    }

    [Fact]
    public void UnwrapOrElse_DoesNotCallFuncWhenOk()
    {
        var called = false;
        var result = Result<int, string>.Ok(42);
        var value = result.UnwrapOrElse(e => { called = true; return 0; });
        Assert.Equal(42, value);
        Assert.False(called);
    }

    [Fact]
    public void UnwrapOrElse_CallsFuncWithErrorWhenErr()
    {
        var result = Result<int, string>.Err("failed");
        var value = result.UnwrapOrElse(e => e.Length);
        Assert.Equal(6, value);
    }

    [Fact]
    public void Map_TransformsValueWhenOk()
    {
        var result = Result<int, string>.Ok(42);
        var mapped = result.Map(x => x.ToString());
        Assert.True(mapped.IsOk);
        Assert.Equal("42", mapped.Unwrap());
    }

    [Fact]
    public void Map_PreservesErrorWhenErr()
    {
        var result = Result<int, string>.Err("failed");
        var mapped = result.Map(x => x.ToString());
        Assert.True(mapped.IsErr);
        Assert.Equal("failed", mapped.UnwrapErr());
    }

    [Fact]
    public void MapErr_PreservesValueWhenOk()
    {
        var result = Result<int, string>.Ok(42);
        var mapped = result.MapErr(e => e.Length);
        Assert.True(mapped.IsOk);
        Assert.Equal(42, mapped.Unwrap());
    }

    [Fact]
    public void MapErr_TransformsErrorWhenErr()
    {
        var result = Result<int, string>.Err("failed");
        var mapped = result.MapErr(e => e.Length);
        Assert.True(mapped.IsErr);
        Assert.Equal(6, mapped.UnwrapErr());
    }

    [Fact]
    public void Equality_OkValuesEqual()
    {
        var a = Result<int, string>.Ok(42);
        var b = Result<int, string>.Ok(42);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_ErrValuesEqual()
    {
        var a = Result<int, string>.Err("failed");
        var b = Result<int, string>.Err("failed");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_OkAndErrNotEqual()
    {
        var ok = Result<int, string>.Ok(42);
        var err = Result<int, string>.Err("failed");
        Assert.NotEqual(ok, err);
        Assert.True(ok != err);
    }

    [Fact]
    public void ToString_ShowsOkForValue()
    {
        var result = Result<int, string>.Ok(42);
        Assert.Equal("Ok(42)", result.ToString());
    }

    [Fact]
    public void ToString_ShowsErrForError()
    {
        var result = Result<int, string>.Err("failed");
        Assert.Equal("Err(failed)", result.ToString());
    }

    #region Result.Try

    [Fact]
    public void Try_SuccessfulExpression_ReturnsOk()
    {
        var result = Result.Try(() => int.Parse("42"));
        Assert.True(result.IsOk);
        Assert.Equal(42, result.Unwrap());
    }

    [Fact]
    public void Try_ThrowingExpression_ReturnsErr()
    {
        var result = Result.Try(() => int.Parse("not a number"));
        Assert.True(result.IsErr);
        Assert.IsType<FormatException>(result.UnwrapErr());
    }

    [Fact]
    public void Try_Typed_MatchingException_ReturnsErr()
    {
        var result = Result.Try<int, FormatException>(() => int.Parse("not a number"));
        Assert.True(result.IsErr);
        Assert.IsType<FormatException>(result.UnwrapErr());
    }

    [Fact]
    public void Try_Typed_NonMatchingException_Propagates()
    {
        Assert.Throws<FormatException>(() =>
        {
            // InvalidOperationException doesn't match FormatException, so it should propagate
            Result.Try<int, InvalidOperationException>(() => int.Parse("not a number"));
        });
    }

    [Fact]
    public void Try_Typed_SuccessfulExpression_ReturnsOk()
    {
        var result = Result.Try<int, FormatException>(() => int.Parse("42"));
        Assert.True(result.IsOk);
        Assert.Equal(42, result.Unwrap());
    }

    #endregion
}
