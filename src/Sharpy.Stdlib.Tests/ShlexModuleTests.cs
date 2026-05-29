using Xunit;
using FluentAssertions;
using System.Collections.Generic;

namespace Sharpy.Core.Tests;

public class ShlexModuleTests
{
    // --- split() ---

    [Fact]
    public void Split_SimpleWords()
    {
        var result = ShlexModule.Split("hello world");
        result.Should().Equal("hello", "world");
    }

    [Fact]
    public void Split_SingleQuotedString()
    {
        var result = ShlexModule.Split("echo 'hello world'");
        result.Should().Equal("echo", "hello world");
    }

    [Fact]
    public void Split_DoubleQuotedString()
    {
        var result = ShlexModule.Split("echo \"hello world\"");
        result.Should().Equal("echo", "hello world");
    }

    [Fact]
    public void Split_MixedQuotes()
    {
        var result = ShlexModule.Split("echo 'hello world' | grep hello");
        result.Should().Equal("echo", "hello world", "|", "grep", "hello");
    }

    [Fact]
    public void Split_BackslashEscaping()
    {
        var result = ShlexModule.Split(@"hello\ world");
        result.Should().Equal("hello world");
    }

    [Fact]
    public void Split_BackslashInDoubleQuotes()
    {
        var result = ShlexModule.Split("\"hello\\\"world\"");
        result.Should().Equal("hello\"world");
    }

    [Fact]
    public void Split_PipesAndRedirects()
    {
        var result = ShlexModule.Split("cat file.txt | grep pattern > output.txt");
        result.Should().Equal("cat", "file.txt", "|", "grep", "pattern", ">", "output.txt");
    }

    [Fact]
    public void Split_EmptyString()
    {
        var result = ShlexModule.Split("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Split_WhitespaceOnly()
    {
        var result = ShlexModule.Split("   \t  \n  ");
        result.Should().BeEmpty();
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
    public void Split_UnicodeInQuotes()
    {
        var result = ShlexModule.Split("echo '日本語テスト'");
        result.Should().Equal("echo", "日本語テスト");
    }

    [Fact]
    public void Split_AdjacentQuotedStrings()
    {
        var result = ShlexModule.Split("'hello ''world'");
        result.Should().Equal("hello world");
    }

    [Fact]
    public void Split_MultipleSpaces()
    {
        var result = ShlexModule.Split("a   b     c");
        result.Should().Equal("a", "b", "c");
    }

    // --- quote() ---

    [Fact]
    public void Quote_SafeString_NoQuoting()
    {
        var result = ShlexModule.Quote("hello");
        result.Should().Be("hello");
    }

    [Fact]
    public void Quote_EmptyString()
    {
        var result = ShlexModule.Quote("");
        result.Should().Be("''");
    }

    [Fact]
    public void Quote_StringWithSpaces()
    {
        var result = ShlexModule.Quote("file with spaces.txt");
        result.Should().Be("'file with spaces.txt'");
    }

    [Fact]
    public void Quote_StringWithSingleQuote()
    {
        var result = ShlexModule.Quote("it's");
        result.Should().Be("'it'\"'\"'s'");
    }

    [Fact]
    public void Quote_StringWithSpecialChars()
    {
        var result = ShlexModule.Quote("hello;world");
        result.Should().Be("'hello;world'");
    }

    [Fact]
    public void Quote_SafeCharsNotQuoted()
    {
        var result = ShlexModule.Quote("file-name_v2.0/path");
        result.Should().Be("file-name_v2.0/path");
    }

    // --- join() ---

    [Fact]
    public void Join_SimpleTokens()
    {
        var result = ShlexModule.Join(new List<string>(new[] { "echo", "hello world" }));
        result.Should().Be("echo 'hello world'");
    }

    [Fact]
    public void Join_EmptyList()
    {
        var result = ShlexModule.Join(new List<string>());
        result.Should().Be("");
    }

    [Fact]
    public void Join_SingleToken()
    {
        var result = ShlexModule.Join(new List<string>(new[] { "hello" }));
        result.Should().Be("hello");
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

    // --- null handling ---

    [Fact]
    public void Split_Null_ThrowsTypeError()
    {
        var act = () => ShlexModule.Split(null!);
        act.Should().Throw<TypeError>();
    }

    [Fact]
    public void Quote_Null_ThrowsTypeError()
    {
        var act = () => ShlexModule.Quote(null!);
        act.Should().Throw<TypeError>();
    }

    [Fact]
    public void Join_Null_ThrowsTypeError()
    {
        var act = () => ShlexModule.Join(null!);
        act.Should().Throw<TypeError>();
    }
}
