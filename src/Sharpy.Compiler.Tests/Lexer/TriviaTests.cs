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

    [Fact]
    public void PreserveMode_InlineComment_AttachedAsTrailingTrivia()
    {
        var lexer = new SharpyLexer("x = 1  # inline\ny = 2\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var oneToken = tokens.First(t => t.Type == TokenType.Integer && t.Value == "1");
        Assert.NotNull(oneToken.TrailingTrivia);
        Assert.Single(oneToken.TrailingTrivia);
        Assert.Equal(TriviaKind.Comment, oneToken.TrailingTrivia[0].Kind);
        Assert.Contains("inline", oneToken.TrailingTrivia[0].Text);

        var yToken = tokens.First(t => t.Type == TokenType.Identifier && t.Value == "y");
        Assert.True(yToken.LeadingTrivia == null || yToken.LeadingTrivia.Count == 0);
    }

    [Fact]
    public void PreserveMode_StandaloneComment_RemainsLeadingTrivia()
    {
        var lexer = new SharpyLexer("# standalone\nx = 1\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var xToken = tokens.First(t => t.Type == TokenType.Identifier);
        Assert.NotNull(xToken.LeadingTrivia);
        Assert.Single(xToken.LeadingTrivia);
        Assert.Contains("standalone", xToken.LeadingTrivia[0].Text);
        Assert.Null(xToken.TrailingTrivia);
    }

    [Fact]
    public void PreserveMode_CommentAfterColon_TrailingTrivia()
    {
        var lexer = new SharpyLexer("def foo():  # docstring\n    pass\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var colonToken = tokens.First(t => t.Type == TokenType.Colon);
        Assert.NotNull(colonToken.TrailingTrivia);
        Assert.Single(colonToken.TrailingTrivia);
        Assert.Contains("docstring", colonToken.TrailingTrivia[0].Text);
    }

    [Fact]
    public void PreserveMode_MixedLeadingAndTrailing()
    {
        var lexer = new SharpyLexer("x = 1  # inline\n# standalone\ny = 2\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var oneToken = tokens.First(t => t.Type == TokenType.Integer && t.Value == "1");
        Assert.NotNull(oneToken.TrailingTrivia);
        Assert.Single(oneToken.TrailingTrivia);
        Assert.Contains("inline", oneToken.TrailingTrivia[0].Text);

        var yToken = tokens.First(t => t.Type == TokenType.Identifier && t.Value == "y");
        Assert.NotNull(yToken.LeadingTrivia);
        Assert.Single(yToken.LeadingTrivia);
        Assert.Contains("standalone", yToken.LeadingTrivia[0].Text);
    }

    [Fact]
    public void PreserveMode_CommentAtEof_TrailingTrivia()
    {
        var lexer = new SharpyLexer("x = 1  # end", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var oneToken = tokens.First(t => t.Type == TokenType.Integer && t.Value == "1");
        Assert.NotNull(oneToken.TrailingTrivia);
        Assert.Single(oneToken.TrailingTrivia);
        Assert.Contains("end", oneToken.TrailingTrivia[0].Text);
    }

    [Fact]
    public void PreserveMode_StandaloneCommentAtEof_LeadingTriviaOnEof()
    {
        var lexer = new SharpyLexer("x = 1\n# end comment\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var eofToken = tokens.First(t => t.Type == TokenType.Eof);
        Assert.NotNull(eofToken.LeadingTrivia);
        Assert.Single(eofToken.LeadingTrivia);
        Assert.Contains("end comment", eofToken.LeadingTrivia[0].Text);
    }

    [Fact]
    public void PreserveMode_SingleBlankLine_BlankLinesTrivia()
    {
        var lexer = new SharpyLexer("x = 1\n\ny = 2\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var yToken = tokens.First(t => t.Type == TokenType.Identifier && t.Value == "y");
        Assert.NotNull(yToken.LeadingTrivia);
        Assert.Single(yToken.LeadingTrivia);
        Assert.Equal(TriviaKind.BlankLines, yToken.LeadingTrivia[0].Kind);
        Assert.Equal(1, yToken.LeadingTrivia[0].BlankLineCount);
    }

    [Fact]
    public void PreserveMode_MultipleBlankLines_CorrectCount()
    {
        var lexer = new SharpyLexer("x = 1\n\n\ny = 2\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var yToken = tokens.First(t => t.Type == TokenType.Identifier && t.Value == "y");
        Assert.NotNull(yToken.LeadingTrivia);
        Assert.Single(yToken.LeadingTrivia);
        Assert.Equal(TriviaKind.BlankLines, yToken.LeadingTrivia[0].Kind);
        Assert.Equal(2, yToken.LeadingTrivia[0].BlankLineCount);
    }

    [Fact]
    public void PreserveMode_BlankLinesThenComment_CorrectOrder()
    {
        var lexer = new SharpyLexer("x = 1\n\n# comment\ny = 2\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var yToken = tokens.First(t => t.Type == TokenType.Identifier && t.Value == "y");
        Assert.NotNull(yToken.LeadingTrivia);
        Assert.Equal(2, yToken.LeadingTrivia.Count);
        Assert.Equal(TriviaKind.BlankLines, yToken.LeadingTrivia[0].Kind);
        Assert.Equal(1, yToken.LeadingTrivia[0].BlankLineCount);
        Assert.Equal(TriviaKind.Comment, yToken.LeadingTrivia[1].Kind);
    }

    [Fact]
    public void PreserveMode_BlankLineCommentBlankLine_ThreeTrivia()
    {
        var lexer = new SharpyLexer("x = 1\n\n# comment\n\ny = 2\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var yToken = tokens.First(t => t.Type == TokenType.Identifier && t.Value == "y");
        Assert.NotNull(yToken.LeadingTrivia);
        Assert.Equal(3, yToken.LeadingTrivia.Count);
        Assert.Equal(TriviaKind.BlankLines, yToken.LeadingTrivia[0].Kind);
        Assert.Equal(1, yToken.LeadingTrivia[0].BlankLineCount);
        Assert.Equal(TriviaKind.Comment, yToken.LeadingTrivia[1].Kind);
        Assert.Equal(TriviaKind.BlankLines, yToken.LeadingTrivia[2].Kind);
        Assert.Equal(1, yToken.LeadingTrivia[2].BlankLineCount);
    }

    [Fact]
    public void PreserveMode_BlankLinesAtStartOfFile()
    {
        var lexer = new SharpyLexer("\n\nx = 1\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var xToken = tokens.First(t => t.Type == TokenType.Identifier && t.Value == "x");
        Assert.NotNull(xToken.LeadingTrivia);
        Assert.Contains(xToken.LeadingTrivia, t => t.Kind == TriviaKind.BlankLines && t.BlankLineCount == 2);
    }

    [Fact]
    public void PreserveMode_NoBlankLines_NoBlankLinesTrivia()
    {
        var lexer = new SharpyLexer("x = 1\ny = 2\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var yToken = tokens.First(t => t.Type == TokenType.Identifier && t.Value == "y");
        Assert.True(
            yToken.LeadingTrivia == null ||
            !yToken.LeadingTrivia.Any(t => t.Kind == TriviaKind.BlankLines));
    }

    [Fact]
    public void PreserveMode_BlankLinesAtEndOfFile_OnEofToken()
    {
        var lexer = new SharpyLexer("x = 1\n\n\n", preserveTrivia: true);
        var tokens = lexer.TokenizeAll();

        var eofToken = tokens.First(t => t.Type == TokenType.Eof);
        Assert.NotNull(eofToken.LeadingTrivia);
        Assert.Contains(eofToken.LeadingTrivia, t => t.Kind == TriviaKind.BlankLines && t.BlankLineCount == 2);
    }

    [Fact]
    public void DefaultMode_BlankLines_NoTrivia()
    {
        var lexer = new SharpyLexer("x = 1\n\n\ny = 2\n");
        var tokens = lexer.TokenizeAll();

        foreach (var token in tokens)
        {
            Assert.Null(token.LeadingTrivia);
            Assert.Null(token.TrailingTrivia);
        }
    }
}
