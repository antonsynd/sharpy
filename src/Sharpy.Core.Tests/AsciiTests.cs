using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Ascii_Tests
{
    [Fact]
    public void Ascii_AsciiOnlyString_PassesThroughUnchanged()
    {
        Ascii("hello world").Should().Be("hello world");
    }

    [Fact]
    public void Ascii_EmptyString_ReturnsEmpty()
    {
        Ascii("").Should().Be("");
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
        // U+00E9 (e with acute accent) fits in \xFF range
        Ascii("h\u00e9llo").Should().Be("h\\xe9llo");
    }

    [Fact]
    public void Ascii_CjkChar_EscapedAsBackslashU()
    {
        // U+4E16 is a BMP char above 0xFF
        Ascii("\u4e16").Should().Be("\\u4e16");
    }

    [Fact]
    public void Ascii_SurrogatePairEmoji_EscapedAsBackslashUpperU()
    {
        // U+1F600 is a supplementary plane character (surrogate pair)
        Ascii("\U0001f600").Should().Be("\\U0001f600");
    }

    [Fact]
    public void Ascii_MixedContent_EscapesOnlyNonAscii()
    {
        Ascii("abc\u00e9def\u4e16ghi").Should().Be("abc\\xe9def\\u4e16ghi");
    }

    [Fact]
    public void Ascii_AllAsciiPrintable_NoEscaping()
    {
        string printable = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Ascii(printable).Should().Be(printable);
    }
}
