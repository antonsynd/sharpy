using Sharpy.Compiler.Lexer;
using Xunit;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;

namespace Sharpy.Compiler.Tests.Lexer;

public class TriviaTests
{
    [Fact]
    public void DefaultMode_NoTriviaOnTokens()
    {
        var lexer = new SharpyLexer("# comment\nx = 1\n");
        var tokens = lexer.TokenizeAll();

        foreach (var token in tokens)
        {
            Assert.Null(token.LeadingTrivia);
            Assert.Null(token.TrailingTrivia);
        }
    }

    [Fact]
    public void PreserveMode_LeadingComment_AttachedToNextToken()
    {
        var lexer = new SharpyLexer("# my comment\nx = 1\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        // Find the first non-trivial token (should be 'x')
        var xToken = tokens.First(t => t.Type == TokenType.Identifier);
        Assert.NotNull(xToken.LeadingTrivia);
        Assert.Single(xToken.LeadingTrivia);
        Assert.Equal(TriviaKind.Comment, xToken.LeadingTrivia[0].Kind);
        Assert.Contains("my comment", xToken.LeadingTrivia[0].Text);
    }

    [Fact]
    public void PreserveMode_MultipleComments_CollectedAsLeadingTrivia()
    {
        var lexer = new SharpyLexer("# comment 1\n# comment 2\nx = 1\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var xToken = tokens.First(t => t.Type == TokenType.Identifier);
        Assert.NotNull(xToken.LeadingTrivia);
        Assert.Equal(2, xToken.LeadingTrivia.Count);
        Assert.Contains("comment 1", xToken.LeadingTrivia[0].Text);
        Assert.Contains("comment 2", xToken.LeadingTrivia[1].Text);
    }

    [Fact]
    public void PreserveMode_CommentIncludesHash()
    {
        var lexer = new SharpyLexer("# hello\nx = 1\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var xToken = tokens.First(t => t.Type == TokenType.Identifier);
        Assert.NotNull(xToken.LeadingTrivia);
        Assert.StartsWith("#", xToken.LeadingTrivia[0].Text);
    }

    [Fact]
    public void PreserveMode_CommentHasCorrectPosition()
    {
        var lexer = new SharpyLexer("# hello\nx = 1\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var xToken = tokens.First(t => t.Type == TokenType.Identifier);
        Assert.NotNull(xToken.LeadingTrivia);
        Assert.Equal(1, xToken.LeadingTrivia[0].Line);
        Assert.Equal(1, xToken.LeadingTrivia[0].Column);
    }

    [Fact]
    public void PreserveMode_NoComment_NoTrivia()
    {
        var lexer = new SharpyLexer("x = 1\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var xToken = tokens.First(t => t.Type == TokenType.Identifier);
        // LeadingTrivia may be null or empty
        Assert.True(xToken.LeadingTrivia == null || xToken.LeadingTrivia.Count == 0);
    }
}
