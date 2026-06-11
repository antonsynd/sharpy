using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Repr_Tests
{
    [Theory]
    [InlineData("\0", "'\\x00'")]
    [InlineData("\a", "'\\x07'")]
    [InlineData("\b", "'\\x08'")]
    [InlineData("\f", "'\\x0c'")]
    [InlineData("\v", "'\\x0b'")]
    [InlineData("\x1b", "'\\x1b'")]
    [InlineData("\x1f", "'\\x1f'")]
    [InlineData("\x7f", "'\\x7f'")]
    public void Repr_ControlCharacter_EscapedAsHex(string input, string expected)
    {
        Builtins.Repr(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("\t", "'\\t'")]
    [InlineData("\n", "'\\n'")]
    [InlineData("\r", "'\\r'")]
    public void Repr_NamedEscapes_Preserved(string input, string expected)
    {
        Builtins.Repr(input).Should().Be(expected);
    }

    [Fact]
    public void Repr_MixedControlAndText_EscapesControlOnly()
    {
        Builtins.Repr("hello\x00world").Should().Be("'hello\\x00world'");
    }

    [Fact]
    public void Repr_ControlCharWithSmartQuoting_UsesDoubleQuotes()
    {
        // String contains single quote but no double quote → smart quoting picks double quotes
        Builtins.Repr("it's \a").Should().Be("\"it's \\x07\"");
    }

    [Fact]
    public void Repr_SingleElementTuple_HasTrailingComma()
    {
        // Python: repr((1,)) == "(1,)" — trailing comma disambiguates from grouping
        Builtins.Repr(System.ValueTuple.Create(1)).Should().Be("(1,)");
    }

    [Fact]
    public void Repr_TwoElementTuple_NoTrailingComma()
    {
        // Python: repr((1, 2)) == "(1, 2)"
        Builtins.Repr((1, 2)).Should().Be("(1, 2)");
    }

    [Fact]
    public void Repr_NestedSingleElementTuple_HasTrailingCommas()
    {
        // Python: repr(((1,),)) == "((1,),)"
        Builtins.Repr(System.ValueTuple.Create(System.ValueTuple.Create(1)))
            .Should().Be("((1,),)");
    }

    [Fact]
    public void Repr_SingleElementStringTuple_QuotesElement()
    {
        // Python: repr(("a",)) == "('a',)"
        Builtins.Repr(System.ValueTuple.Create("a")).Should().Be("('a',)");
    }

    [Fact]
    public void Repr_EightElementTuple_FlattensRest()
    {
        // ValueTuple packs the 8th element into a nested TRest; repr must flatten.
        // Python: repr((1,2,3,4,5,6,7,8)) == "(1, 2, 3, 4, 5, 6, 7, 8)"
        Builtins.Repr((1, 2, 3, 4, 5, 6, 7, 8))
            .Should().Be("(1, 2, 3, 4, 5, 6, 7, 8)");
    }

    [Fact]
    public void Repr_FifteenElementTuple_FlattensNestedRest()
    {
        // 15 elements span two levels of TRest nesting.
        Builtins.Repr((1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15))
            .Should().Be("(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15)");
    }
}
