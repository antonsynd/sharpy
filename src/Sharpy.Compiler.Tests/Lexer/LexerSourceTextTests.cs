using Sharpy.Compiler.Text;
using LexerNs = Sharpy.Compiler.Lexer;
using Xunit;

namespace Sharpy.Compiler.Tests.Lexer;

/// <summary>
/// Tests for the Lexer's SourceText constructor overload (Phase 6.1).
/// </summary>
public class LexerSourceTextTests
{
    [Fact]
    public void Lexer_WithSourceText_TokenizesCorrectly()
    {
        var source = "x = 42\n";
        var sourceText = new SourceText(source, "test.spy");
        var lexer = new LexerNs.Lexer(sourceText);
        var tokens = lexer.TokenizeAll();

        Assert.False(lexer.Diagnostics.HasErrors);
        Assert.True(tokens.Count > 0);
        Assert.Equal(LexerNs.TokenType.Identifier, tokens[0].Type);
        Assert.Equal("x", tokens[0].Value);
    }

    [Fact]
    public void Lexer_WithSourceText_ExposesSourceText()
    {
        var source = "x = 1\n";
        var sourceText = new SourceText(source, "test.spy");
        var lexer = new LexerNs.Lexer(sourceText);

        Assert.NotNull(lexer.SourceText);
        Assert.Same(sourceText, lexer.SourceText);
        Assert.Equal("test.spy", lexer.SourceText!.FilePath);
    }

    [Fact]
    public void Lexer_WithStringConstructor_SourceTextIsNull()
    {
        var lexer = new LexerNs.Lexer("x = 1\n");

        Assert.Null(lexer.SourceText);
    }

    [Fact]
    public void Lexer_WithSourceText_ProducesSameTokensAsStringConstructor()
    {
        var source = "def main():\n    x: int = 42\n    return x\n";
        var sourceText = new SourceText(source);

        var lexerFromString = new LexerNs.Lexer(source);
        var lexerFromSourceText = new LexerNs.Lexer(sourceText);

        var tokensFromString = lexerFromString.TokenizeAll();
        var tokensFromSourceText = lexerFromSourceText.TokenizeAll();

        Assert.Equal(tokensFromString.Count, tokensFromSourceText.Count);

        for (int i = 0; i < tokensFromString.Count; i++)
        {
            Assert.Equal(tokensFromString[i].Type, tokensFromSourceText[i].Type);
            Assert.Equal(tokensFromString[i].Value, tokensFromSourceText[i].Value);
            Assert.Equal(tokensFromString[i].Line, tokensFromSourceText[i].Line);
            Assert.Equal(tokensFromString[i].Column, tokensFromSourceText[i].Column);
        }
    }

    [Fact]
    public void Lexer_WithSourceText_HandlesEmptySource()
    {
        var sourceText = new SourceText("");
        var lexer = new LexerNs.Lexer(sourceText);
        var tokens = lexer.TokenizeAll();

        Assert.False(lexer.Diagnostics.HasErrors);
        Assert.Single(tokens);
        Assert.Equal(LexerNs.TokenType.Eof, tokens[0].Type);
    }

    [Fact]
    public void Lexer_WithSourceText_HandlesMultilineProgram()
    {
        var source = @"class Foo:
    x: int = 0

    def bar(self) -> int:
        return self.x
";
        var sourceText = new SourceText(source, "foo.spy");
        var lexer = new LexerNs.Lexer(sourceText);
        var tokens = lexer.TokenizeAll();

        Assert.False(lexer.Diagnostics.HasErrors);
        Assert.Equal(LexerNs.TokenType.Class, tokens[0].Type);
        Assert.Equal(LexerNs.TokenType.Eof, tokens[^1].Type);
    }
}
