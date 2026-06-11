using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Len_Tuple_Tests
{
    [Fact]
    public void Len_SingleElementTuple_IsOne()
    {
        Builtins.Len(System.ValueTuple.Create(1)).Should().Be(1);
    }

    [Fact]
    public void Len_TwoElementTuple_IsTwo()
    {
        Builtins.Len((1, 2)).Should().Be(2);
    }

    [Fact]
    public void Len_ThreeElementTuple_IsThree()
    {
        Builtins.Len((1, "a", 3.0)).Should().Be(3);
    }

    [Fact]
    public void Len_NullTuple_Throws()
    {
        System.Runtime.CompilerServices.ITuple? t = null;
        var act = () => Builtins.Len(t!);
        act.Should().Throw<TypeError>();
    }
}
