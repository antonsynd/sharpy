using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Textwrap_Tests
{
    // --- wrap ---

    [Fact]
    public void Wrap_BasicWordWrap()
    {
        var result = Sharpy.Textwrap.Wrap("Hello World", 5);
        result.Should().HaveCount(2);
        ((string)result[0]).Should().Be("Hello");
        ((string)result[1]).Should().Be("World");
    }

    [Fact]
    public void Wrap_EmptyString_ReturnsEmptyList()
    {
        var result = Sharpy.Textwrap.Wrap("", 5);
        result.Should().HaveCount(0);
    }

    [Fact]
    public void Wrap_WhitespaceOnly_ReturnsEmptyList()
    {
        var result = Sharpy.Textwrap.Wrap("   ", 5);
        result.Should().HaveCount(0);
    }

    [Fact]
    public void Wrap_LongWord_BreaksWord()
    {
        var result = Sharpy.Textwrap.Wrap("abcdefgh", 5);
        result.Should().HaveCount(2);
        ((string)result[0]).Should().Be("abcde");
        ((string)result[1]).Should().Be("fgh");
    }

    [Fact]
    public void Wrap_VeryLongWord_BreaksIntoMultipleChunks()
    {
        var result = Sharpy.Textwrap.Wrap("abcdefghijklmnop", 5);
        result.Should().HaveCount(4);
        ((string)result[0]).Should().Be("abcde");
        ((string)result[1]).Should().Be("fghij");
        ((string)result[2]).Should().Be("klmno");
        ((string)result[3]).Should().Be("p");
    }

    [Fact]
    public void Wrap_DefaultWidth70()
    {
        var text = new string('a', 100);
        var result = Sharpy.Textwrap.Wrap(text);
        result.Should().HaveCount(2);
        ((string)result[0]).Length.Should().Be(70);
        ((string)result[1]).Length.Should().Be(30);
    }

    [Fact]
    public void Wrap_CollapsesWhitespace()
    {
        var result = Sharpy.Textwrap.Wrap("hello   world", 70);
        result.Should().HaveCount(1);
        ((string)result[0]).Should().Be("hello world");
    }

    [Fact]
    public void Wrap_CollapsesNewlines()
    {
        var result = Sharpy.Textwrap.Wrap("hello\nworld", 70);
        result.Should().HaveCount(1);
        ((string)result[0]).Should().Be("hello world");
    }

    // --- fill ---

    [Fact]
    public void Fill_JoinsWithNewlines()
    {
        var result = Sharpy.Textwrap.Fill("Hello World", 5);
        ((string)result).Should().Be("Hello\nWorld");
    }

    [Fact]
    public void Fill_EmptyString_ReturnsEmpty()
    {
        var result = Sharpy.Textwrap.Fill("", 5);
        ((string)result).Should().Be("");
    }

    // --- dedent ---

    [Fact]
    public void Dedent_RemovesCommonIndentation()
    {
        var result = Sharpy.Textwrap.Dedent("  hello\n  world");
        ((string)result).Should().Be("hello\nworld");
    }

    [Fact]
    public void Dedent_PartialCommonIndentation()
    {
        var result = Sharpy.Textwrap.Dedent("  hello\n    world");
        ((string)result).Should().Be("hello\n  world");
    }

    [Fact]
    public void Dedent_EmptyLinesIgnored()
    {
        var result = Sharpy.Textwrap.Dedent("  hello\n\n  world");
        ((string)result).Should().Be("hello\n\nworld");
    }

    [Fact]
    public void Dedent_NoCommonIndentation()
    {
        var result = Sharpy.Textwrap.Dedent("hello\nworld");
        ((string)result).Should().Be("hello\nworld");
    }

    [Fact]
    public void Dedent_TabIndentation()
    {
        var result = Sharpy.Textwrap.Dedent("\thello\n\tworld");
        ((string)result).Should().Be("hello\nworld");
    }

    [Fact]
    public void Dedent_EmptyString()
    {
        var result = Sharpy.Textwrap.Dedent("");
        ((string)result).Should().Be("");
    }

    // --- indent ---

    [Fact]
    public void Indent_AddsPrefix()
    {
        var result = Sharpy.Textwrap.Indent("hello\nworld", "  ");
        ((string)result).Should().Be("  hello\n  world");
    }

    [Fact]
    public void Indent_SkipsEmptyLines()
    {
        var result = Sharpy.Textwrap.Indent("hello\n\nworld", "> ");
        ((string)result).Should().Be("> hello\n\n> world");
    }

    [Fact]
    public void Indent_SkipsWhitespaceOnlyLines()
    {
        var result = Sharpy.Textwrap.Indent("hello\n  \nworld", "> ");
        ((string)result).Should().Be("> hello\n  \n> world");
    }

    [Fact]
    public void Indent_PreservesTrailingNewline()
    {
        var result = Sharpy.Textwrap.Indent("hello\n\nworld\n", "> ");
        ((string)result).Should().Be("> hello\n\n> world\n");
    }

    [Fact]
    public void Indent_EmptyString()
    {
        var result = Sharpy.Textwrap.Indent("", "  ");
        ((string)result).Should().Be("");
    }

    // --- shorten ---

    [Fact]
    public void Shorten_FitsWithinWidth()
    {
        var result = Sharpy.Textwrap.Shorten("Hello", 5);
        ((string)result).Should().Be("Hello");
    }

    [Fact]
    public void Shorten_TruncatesWithPlaceholder()
    {
        // "Hello World" is 11 chars, width=10 requires truncation
        var result = Sharpy.Textwrap.Shorten("Hello World", 10);
        ((string)result).Length.Should().BeLessThanOrEqualTo(10);
        ((string)result).Should().Contain("[...]");
    }

    [Fact]
    public void Shorten_CollapsesWhitespace()
    {
        var result = Sharpy.Textwrap.Shorten("Hello  World", 11);
        ((string)result).Should().Be("Hello World");
    }

    [Fact]
    public void Shorten_VerySmallWidth_Throws()
    {
        var act = () => Sharpy.Textwrap.Shorten("Hello World", 3);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Shorten_CollapsedTextFitsExactly()
    {
        var result = Sharpy.Textwrap.Shorten("Hello  World", 12);
        ((string)result).Should().Be("Hello World");
    }

    [Fact]
    public void Shorten_SingleLongWord()
    {
        var result = Sharpy.Textwrap.Shorten("Supercalifragilistic", 10);
        ((string)result).Should().Contain("[...]");
        ((string)result).Length.Should().BeLessThanOrEqualTo(10);
    }
}
