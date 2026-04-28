using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Parser;

public class ParserTriviaTests
{
    private static Module ParseWithTrivia(string source)
    {
        var lexer = new SharpyLexer(source, preserveTrivia: true);
        var tokens = lexer.TokenizeAll();
        var parser = new SharpyParser(tokens);
        return parser.ParseModule();
    }

    private static Module ParseWithoutTrivia(string source)
    {
        var lexer = new SharpyLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new SharpyParser(tokens);
        return parser.ParseModule();
    }

    [Fact]
    public void StatementLeadingTrivia_CommentAboveFunctionDef()
    {
        var source = "# this is a function\ndef foo():\n    pass\n";
        var module = ParseWithTrivia(source);

        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.LeadingTrivia.Should().NotBeNull();
        funcDef.LeadingTrivia.Should().ContainSingle();
        funcDef.LeadingTrivia![0].Kind.Should().Be(TriviaKind.Comment);
        funcDef.LeadingTrivia[0].Text.Should().Contain("this is a function");
    }

    [Fact]
    public void StatementLeadingTrivia_CommentAboveAssignment()
    {
        var source = "# set x\nx = 42\n";
        var module = ParseWithTrivia(source);

        module.Body.Should().HaveCount(1);
        var stmt = module.Body[0];
        stmt.LeadingTrivia.Should().NotBeNull();
        stmt.LeadingTrivia.Should().ContainSingle();
        stmt.LeadingTrivia![0].Text.Should().Contain("set x");
    }

    [Fact]
    public void StatementLeadingTrivia_MultipleComments()
    {
        var source = "# comment 1\n# comment 2\nx = 1\n";
        var module = ParseWithTrivia(source);

        module.Body.Should().HaveCount(1);
        var stmt = module.Body[0];
        stmt.LeadingTrivia.Should().NotBeNull();
        stmt.LeadingTrivia.Should().HaveCount(2);
        stmt.LeadingTrivia![0].Text.Should().Contain("comment 1");
        stmt.LeadingTrivia[1].Text.Should().Contain("comment 2");
    }

    [Fact]
    public void NoTriviaMode_AllTriviaFieldsAreNull()
    {
        var source = "# comment\nx = 1\ny = 2\n";
        var module = ParseWithoutTrivia(source);

        foreach (var stmt in module.Body)
        {
            stmt.LeadingTrivia.Should().BeNull();
            stmt.TrailingTrivia.Should().BeNull();
        }
    }

    [Fact]
    public void ExpressionTrivia_CommentBeforeExpression()
    {
        var source = "# before\nx = 42\n";
        var module = ParseWithTrivia(source);

        module.Body.Should().HaveCount(1);
        var stmt = module.Body[0];
        stmt.LeadingTrivia.Should().NotBeNull();
        stmt.LeadingTrivia![0].Text.Should().Contain("before");
    }

    [Fact]
    public void MultipleStatements_EachGetsOwnTrivia()
    {
        var source = "# first\nx = 1\n# second\ny = 2\n";
        var module = ParseWithTrivia(source);

        module.Body.Should().HaveCount(2);

        module.Body[0].LeadingTrivia.Should().NotBeNull();
        module.Body[0].LeadingTrivia![0].Text.Should().Contain("first");

        module.Body[1].LeadingTrivia.Should().NotBeNull();
        module.Body[1].LeadingTrivia![0].Text.Should().Contain("second");
    }

    [Fact]
    public void NoComment_TriviaIsNull()
    {
        var source = "x = 1\n";
        var module = ParseWithTrivia(source);

        module.Body.Should().HaveCount(1);
        module.Body[0].LeadingTrivia.Should().BeNull();
    }

    [Fact]
    public void InlineComment_NotAttachedToStatements()
    {
        // Inline comments (after code on the same line) are attached to the
        // Newline token by the lexer. The parser skips newlines without
        // preserving their trivia, so inline comments are not yet propagated
        // to AST nodes. A future formatter will need to address this.
        var source = "x = 1  # inline comment\ny = 2\n";
        var module = ParseWithTrivia(source);

        module.Body.Should().HaveCount(2);
        module.Body[0].TrailingTrivia.Should().BeNull();
        module.Body[0].LeadingTrivia.Should().BeNull();
        module.Body[1].TrailingTrivia.Should().BeNull();
        module.Body[1].LeadingTrivia.Should().BeNull();
    }

    [Fact]
    public void TriviaPreserved_ExistingTestsStillPass()
    {
        var source = "def foo(x: int) -> int:\n    return x + 1\n";
        var module = ParseWithTrivia(source);

        module.Body.Should().HaveCount(1);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Name.Should().Be("foo");
        funcDef.LeadingTrivia.Should().BeNull();
    }
}
