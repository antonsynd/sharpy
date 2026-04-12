using FluentAssertions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

public class ByteStringLexerTests
{
    private static List<LexerNs.Token> Tokenize(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        return lexer.TokenizeAll();
    }

    private static LexerNs.Token SingleToken(string source)
    {
        var tokens = Tokenize(source);
        tokens.Should().HaveCount(2);
        tokens[1].Type.Should().Be(TokenType.Eof);
        return tokens[0];
    }

    private static string TokenizeExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        lexer.TokenizeAll();
        Assert.True(lexer.Diagnostics.HasErrors, "Expected lexer to report an error for input: " + source);
        return string.Join("\n", lexer.Diagnostics.GetErrors().Select(d => d.Message));
    }

    [Fact]
    public void Tokenize_ByteString_DoubleQuoted()
    {
        var token = SingleToken("b\"hello\"");
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("hello");
    }

    [Fact]
    public void Tokenize_ByteString_SingleQuoted()
    {
        var token = SingleToken("b'hello'");
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("hello");
    }

    [Fact]
    public void Tokenize_ByteString_Empty()
    {
        var token = SingleToken("b\"\"");
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("");
    }

    [Fact]
    public void Tokenize_ByteString_HexEscape()
    {
        var token = SingleToken("b\"\\x00\\xff\"");
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("\x00\xff");
    }

    [Fact]
    public void Tokenize_ByteString_StandardEscapes()
    {
        var token = SingleToken("b\"\\n\\r\\t\\0\\\\\"");
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("\n\r\t\0\\");
    }

    [Fact]
    public void Tokenize_ByteString_TripleQuoted()
    {
        var source = "b\"\"\"hello\nworld\"\"\"";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("hello\nworld");
    }

    [Fact]
    public void Tokenize_ByteString_TripleQuotedSingleQuote()
    {
        var source = "b'''hello\nworld'''";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("hello\nworld");
    }

    [Fact]
    public void Tokenize_ByteString_RejectsUnicodeEscape_Lowercase()
    {
        var error = TokenizeExpectingError("b\"\\u0041\"");
        error.Should().Contain("Unicode escape sequences are not allowed in byte strings");
    }

    [Fact]
    public void Tokenize_ByteString_RejectsUnicodeEscape_Uppercase()
    {
        var error = TokenizeExpectingError("b\"\\U00000041\"");
        error.Should().Contain("Unicode escape sequences are not allowed in byte strings");
    }

    [Fact]
    public void Tokenize_ByteString_LengthIncludesPrefixAndQuotes()
    {
        // b"hello" is 8 characters in source (b + quote + hello + quote)
        var token = SingleToken("b\"hello\"");
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("hello");
        token.Length.Should().Be(8);
    }

    [Fact]
    public void Tokenize_ByteString_TripleQuotedLength()
    {
        // b"""hello""" is 12 characters in source (b + 3 quotes + hello + 3 quotes)
        var source = "b\"\"\"hello\"\"\"";
        var token = SingleToken(source);
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("hello");
        token.Length.Should().Be(12);
    }

    [Fact]
    public void Tokenize_ByteString_NotConfusedWithBinaryLiteral()
    {
        // b followed by a digit should still be parsed as an identifier + number,
        // not as a byte string. Only b + quote starts a byte string.
        var tokens = Tokenize("b = 1");
        tokens[0].Type.Should().Be(TokenType.Identifier);
        tokens[0].Value.Should().Be("b");
    }

    [Fact]
    public void Tokenize_ByteString_UnterminatedIsError()
    {
        var error = TokenizeExpectingError("b\"hello");
        error.Should().Contain("Unterminated byte string literal");
    }

    [Fact]
    public void Tokenize_ByteString_EscapedQuoteInside()
    {
        var token = SingleToken("b\"he\\\"llo\"");
        token.Type.Should().Be(TokenType.ByteString);
        token.Value.Should().Be("he\"llo");
    }
}
