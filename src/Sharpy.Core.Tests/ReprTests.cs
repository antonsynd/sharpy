using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ReprTests
{
    [Theory]
    [InlineData("\0", "'\\x00'")]
    [InlineData("\a", "'\\x07'")]
    [InlineData("\b", "'\\x08'")]
    [InlineData("\f", "'\\x0c'")]
    [InlineData("\v", "'\\x0b'")]
    [InlineData("\x1b", "'\\x1b'")]
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
}
