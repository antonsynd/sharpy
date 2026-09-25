using System.Collections.Generic;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// #2005 (R-CH): an <c>Optional</c>'s <c>str</c> is transparent — the value's <c>str</c>, or
/// <c>None</c> — and its <c>repr</c> is the constructor spelling — <c>Some(&lt;repr of the
/// value&gt;)</c>, or <c>None()</c> — reached through the <see cref="IRepr"/> channel, so every repr
/// route (a container element, a nested Optional) agrees. Optional has no python twin: the values are
/// the owner ruling's spelling. <see cref="Optional{T}.ToString"/> is unchanged (<c>OptionalTests</c>).
/// </summary>
public class OptionalReprMatrixTests
{
    public static IEnumerable<object[]> Cells()
    {
        //                                 value                                           str        repr
        yield return new object[] { Optional<int>.Some(1), "1", "Some(1)" };
        yield return new object[] { Optional<int>.None, "None", "None()" };
        yield return new object[] { Optional<string>.Some("ab"), "ab", "Some('ab')" };
        yield return new object[] { Optional<string>.None, "None", "None()" };
        yield return new object[] { Optional<bool>.Some(true), "True", "Some(True)" };
        yield return new object[] { Optional<double>.Some(2.0), "2.0", "Some(2.0)" };
        yield return new object[] { Optional<Optional<int>>.Some(Optional<int>.Some(1)), "1", "Some(Some(1))" };
        yield return new object[] { Optional<Optional<int>>.Some(Optional<int>.None), "None", "Some(None())" };
        yield return new object[] { Optional<List<int>>.Some(new List<int>(new[] { 1, 2 })), "[1, 2]", "Some([1, 2])" };
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Str_IsTransparent_Repr_IsTheConstructorSpelling(object value, string str, string repr)
    {
        Assert.Equal(str, Builtins.Str(value));
        Assert.Equal(repr, Builtins.Repr(value));
    }

    [Fact]
    public void ContainerElements_UseTheRepr()
    {
        var xs = new List<Optional<int>>(new[] { Optional<int>.Some(1), Optional<int>.None });
        Assert.Equal("[Some(1), None()]", Builtins.Str(xs));
        Assert.Equal("[Some(1), None()]", Builtins.Repr(xs));
        var d = new Dict<string, Optional<string>> { ["k"] = Optional<string>.Some("v") };
        Assert.Equal("{'k': Some('v')}", Builtins.Str(d));
    }
}
