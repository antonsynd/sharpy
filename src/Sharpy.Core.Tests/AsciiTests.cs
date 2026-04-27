using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Ascii_Tests
{
    [Fact]
    public void Ascii_AsciiOnlyString_PassesThroughUnchanged()
    {
        Ascii("hello world").Should().Be("'hello world'");
    }

    [Fact]
    public void Ascii_EmptyString_ReturnsEmpty()
    {
        Ascii("").Should().Be("''");
    }

    [Fact]
    public void Ascii_Null_ReturnsNone()
    {
        Ascii(null!).Should().Be("None");
    }

    [Fact]
    public void Ascii_Integer_ReturnsToString()
    {
        Ascii(42).Should().Be("42");
    }

    [Fact]
    public void Ascii_LatinNonAsciiChar_EscapedAsBackslashX()
    {
        Ascii("h\u00e9llo").Should().Be("'h\\xe9llo'");
    }

    [Fact]
    public void Ascii_CjkChar_EscapedAsBackslashU()
    {
        Ascii("\u4e16").Should().Be("'\\u4e16'");
    }

    [Fact]
    public void Ascii_SurrogatePairEmoji_EscapedAsBackslashUpperU()
    {
        Ascii("\U0001f600").Should().Be("'\\U0001f600'");
    }

    [Fact]
    public void Ascii_MixedContent_EscapesOnlyNonAscii()
    {
        Ascii("abc\u00e9def\u4e16ghi").Should().Be("'abc\\xe9def\\u4e16ghi'");
    }

    [Fact]
    public void Ascii_AllAsciiPrintable_NoEscaping()
    {
        string printable = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Ascii(printable).Should().Be("'" + printable + "'");
    }

    [Fact]
    public void Ascii_Bool_ReturnsPythonKeyword()
    {
        Ascii(true).Should().Be("True");
        Ascii(false).Should().Be("False");
    }
}
