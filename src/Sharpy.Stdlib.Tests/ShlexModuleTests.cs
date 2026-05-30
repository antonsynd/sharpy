using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class ShlexModuleTests
{
    [Fact]
    public void Split_SimpleWords()
    {
        ShlexModule.Split("hello world").Should().Equal("hello", "world");
    }

    [Fact]
    public void Split_SingleQuotedString()
    {
        ShlexModule.Split("echo 'hello world'").Should().Equal("echo", "hello world");
    }

    [Fact]
    public void Split_DoubleQuotedString()
    {
        ShlexModule.Split("echo \"hello world\"").Should().Equal("echo", "hello world");
    }

    [Fact]
    public void Split_BackslashEscaping()
    {
        ShlexModule.Split(@"hello\ world").Should().Equal("hello world");
    }

    [Fact]
    public void Split_BackslashInDoubleQuotes()
    {
        ShlexModule.Split("\"hello\\\"world\"").Should().Equal("hello\"world");
    }

    [Fact]
    public void Split_PipesAndRedirects()
    {
        ShlexModule.Split("cat file.txt | grep pattern > output.txt")
            .Should().Equal("cat", "file.txt", "|", "grep", "pattern", ">", "output.txt");
    }

    [Fact]
    public void Split_EmptyString()
    {
        ShlexModule.Split("").Should().BeEmpty();
    }

    [Fact]
    public void Split_WhitespaceOnly()
    {
        ShlexModule.Split("   \t  \n  ").Should().BeEmpty();
    }

    [Fact]
    public void Split_MultipleSpaces()
    {
        ShlexModule.Split("a   b     c").Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Split_UnicodeInQuotes()
    {
        ShlexModule.Split("echo '日本語テスト'").Should().Equal("echo", "日本語テスト");
    }

    [Fact]
    public void Split_AdjacentQuotedStrings()
    {
        ShlexModule.Split("'hello ''world'").Should().Equal("hello world");
    }

    [Fact]
    public void Split_MixedQuotes()
    {
        ShlexModule.Split("echo 'single' \"double\"").Should().Equal("echo", "single", "double");
    }

    [Fact]
    public void Split_HashNotCommentByDefault()
    {
        ShlexModule.Split("echo foo#bar").Should().Equal("echo", "foo#bar");
    }

    [Fact]
    public void Split_HashAsCommentWhenEnabled()
    {
        ShlexModule.Split("echo foo #comment", comments: true).Should().Equal("echo", "foo");
    }

    [Fact]
    public void Split_HashInsideQuotesNotComment()
    {
        ShlexModule.Split("echo 'foo#bar'", comments: true).Should().Equal("echo", "foo#bar");
    }

    [Fact]
    public void Split_UnclosedSingleQuote_Throws()
    {
        var act = () => ShlexModule.Split("echo 'hello");
        act.Should().Throw<ValueError>().WithMessage("No closing quotation");
    }

    [Fact]
    public void Split_UnclosedDoubleQuote_Throws()
    {
        var act = () => ShlexModule.Split("echo \"hello");
        act.Should().Throw<ValueError>().WithMessage("No closing quotation");
    }

    [Fact]
    public void Split_TrailingBackslash_Throws()
    {
        var act = () => ShlexModule.Split("echo test\\");
        act.Should().Throw<ValueError>().WithMessage("No escaped character");
    }

    [Fact]
    public void Split_NonPosixMode_Throws()
    {
        var act = () => ShlexModule.Split("test", posix: false);
        act.Should().Throw<ValueError>().WithMessage("non-POSIX mode is not supported");
    }

    [Fact]
    public void Split_Null_ThrowsTypeError()
    {
        var act = () => ShlexModule.Split(null!);
        act.Should().Throw<TypeError>();
    }

    [Fact]
    public void Split_TabAndNewlineAsDelimiters()
    {
        ShlexModule.Split("a\tb\nc").Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Split_NonSpecialBackslashInDoubleQuotes()
    {
        ShlexModule.Split("\"hello\\nworld\"").Should().Equal("hello\\nworld");
    }

    // --- Quote ---

    [Fact]
    public void Quote_SafeString_NoQuoting()
    {
        ShlexModule.Quote("hello").Should().Be("hello");
    }

    [Fact]
    public void Quote_EmptyString()
    {
        ShlexModule.Quote("").Should().Be("''");
    }

    [Fact]
    public void Quote_StringWithSpaces()
    {
        ShlexModule.Quote("hello world").Should().Be("'hello world'");
    }

    [Fact]
    public void Quote_StringWithSingleQuote()
    {
        ShlexModule.Quote("it's").Should().Be("'it'\"'\"'s'");
    }

    [Fact]
    public void Quote_StringWithSpecialChars()
    {
        ShlexModule.Quote("hello;world").Should().Be("'hello;world'");
    }

    [Fact]
    public void Quote_SafeCharsNotQuoted()
    {
        ShlexModule.Quote("file-name_v2.0/path").Should().Be("file-name_v2.0/path");
    }

    [Fact]
    public void Quote_Null_ThrowsTypeError()
    {
        var act = () => ShlexModule.Quote(null!);
        act.Should().Throw<TypeError>();
    }

    // --- Join ---

    [Fact]
    public void Join_SimpleTokens()
    {
        ShlexModule.Join(new List<string>(new[] { "echo", "hello world" }))
            .Should().Be("echo 'hello world'");
    }

    [Fact]
    public void Join_EmptyList()
    {
        ShlexModule.Join(new List<string>()).Should().Be("");
    }

    [Fact]
    public void Join_SingleToken()
    {
        ShlexModule.Join(new List<string>(new[] { "hello" })).Should().Be("hello");
    }

    [Fact]
    public void Join_Null_ThrowsTypeError()
    {
        var act = () => ShlexModule.Join(null!);
        act.Should().Throw<TypeError>();
    }

    // --- Roundtrip ---

    [Fact]
    public void Split_Join_Roundtrip()
    {
        var tokens = new List<string>(new[] { "echo", "hello world", "|", "grep", "hello" });
        var joined = ShlexModule.Join(tokens);
        var split = ShlexModule.Split(joined);
        split.Should().Equal(tokens);
    }
}
