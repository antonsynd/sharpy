using FluentAssertions;
using LexerNs = Sharpy.Compiler.Lexer;
using TokenType = Sharpy.Compiler.Lexer.TokenType;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

public class BangTokenTests
{
    private static List<LexerNs.Token> Tokenize(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        return lexer.TokenizeAll();
    }

    #region Standalone Bang

    [Fact]
    public void Lexer_StandaloneBang_ProducesBangToken()
    {
        var tokens = Tokenize("!");

        tokens.Should().HaveCount(2); // Bang + EOF
        tokens[0].Type.Should().Be(TokenType.Bang);
        tokens[0].Value.Should().Be("!");
    }

    #endregion

    #region Bang in Type Annotation Context

    [Fact]
    public void Lexer_BangInTypeAnnotation_ProducesBangToken()
    {
        var tokens = Tokenize("int !ValueError");

        // Should produce: Identifier(int), Bang, Identifier(ValueError), EOF
        tokens.Should().HaveCount(4);
        tokens[0].Type.Should().Be(TokenType.Identifier);
        tokens[0].Value.Should().Be("int");
        tokens[1].Type.Should().Be(TokenType.Bang);
        tokens[2].Type.Should().Be(TokenType.Identifier);
        tokens[2].Value.Should().Be("ValueError");
    }

    #endregion

    #region NotEqual Still Works

    [Fact]
    public void Lexer_NotEqual_StillProducesNotEqualToken()
    {
        var tokens = Tokenize("a != b");

        // Should produce: Identifier(a), NotEqual, Identifier(b), EOF
        tokens.Should().HaveCount(4);
        tokens[1].Type.Should().Be(TokenType.NotEqual);
        tokens[1].Value.Should().Be("!=");
    }

    #endregion

    #region Bang Followed by Equals (Separate Tokens)

    [Fact]
    public void Lexer_BangSpaceEquals_ProducesSeparateTokens()
    {
        var tokens = Tokenize("! =");

        // Should produce: Bang, Assign, EOF
        tokens.Should().HaveCount(3);
        tokens[0].Type.Should().Be(TokenType.Bang);
        tokens[1].Type.Should().Be(TokenType.Assign);
    }

    #endregion

    #region Position Tracking

    [Fact]
    public void Lexer_BangToken_HasCorrectPosition()
    {
        var tokens = Tokenize("int !E");

        var bangToken = tokens[1];
        bangToken.Type.Should().Be(TokenType.Bang);
        bangToken.Position.Should().Be(4); // 0-indexed, after "int "
        bangToken.Length.Should().Be(1);
    }

    [Fact]
    public void Lexer_BangToken_HasCorrectLineAndColumn()
    {
        var tokens = Tokenize("int !E");

        var bangToken = tokens[1];
        bangToken.Line.Should().Be(1);
        bangToken.Column.Should().Be(5); // 1-indexed
    }

    #endregion

    #region Complex Type with Bang

    [Fact]
    public void Lexer_ComplexTypeWithBang_TokenizesCorrectly()
    {
        var tokens = Tokenize("Result[int, str] !IOError");

        // Find the Bang token
        var bangIndex = tokens.FindIndex(t => t.Type == TokenType.Bang);
        bangIndex.Should().BeGreaterThan(0, "Bang token should be present");
        tokens[bangIndex].Type.Should().Be(TokenType.Bang);
        tokens[bangIndex + 1].Type.Should().Be(TokenType.Identifier);
        tokens[bangIndex + 1].Value.Should().Be("IOError");
    }

    #endregion
}
