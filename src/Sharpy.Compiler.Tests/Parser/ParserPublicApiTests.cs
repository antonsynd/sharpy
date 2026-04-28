using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

public class ParserPublicApiTests
{
    private static List<LexerNs.Token> Lex(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        return lexer.TokenizeAll();
    }

    #region ParseSingleExpression

    [Fact]
    public void ParseSingleExpression_IntegerLiteral()
    {
        var parser = new ParserNs.Parser(Lex("42"));
        var expr = parser.ParseSingleExpression();

        expr.Should().BeOfType<IntegerLiteral>()
            .Which.Value.Should().Be("42");
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleExpression_BinaryOp()
    {
        var parser = new ParserNs.Parser(Lex("1 + 2"));
        var expr = parser.ParseSingleExpression();

        var binOp = expr.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Add);
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleExpression_FunctionCall()
    {
        var parser = new ParserNs.Parser(Lex("foo(1, 2)"));
        var expr = parser.ParseSingleExpression();

        var call = expr.Should().BeOfType<FunctionCall>().Subject;
        call.Arguments.Should().HaveCount(2);
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleExpression_Lambda()
    {
        var parser = new ParserNs.Parser(Lex("lambda x: x + 1"));
        var expr = parser.ParseSingleExpression();

        expr.Should().BeOfType<LambdaExpression>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleExpression_Ternary()
    {
        var parser = new ParserNs.Parser(Lex("a if cond else b"));
        var expr = parser.ParseSingleExpression();

        expr.Should().BeOfType<ConditionalExpression>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleExpression_ListComprehension()
    {
        var parser = new ParserNs.Parser(Lex("[x * 2 for x in items]"));
        var expr = parser.ParseSingleExpression();

        expr.Should().BeOfType<ListComprehension>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleExpression_TrailingTokens_ReportsDiagnostic()
    {
        var parser = new ParserNs.Parser(Lex("42 + 3\nreturn 1"));
        var expr = parser.ParseSingleExpression();

        expr.Should().BeOfType<BinaryOp>();
        parser.Diagnostics.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void ParseSingleExpression_StringLiteral()
    {
        var parser = new ParserNs.Parser(Lex("\"hello\""));
        var expr = parser.ParseSingleExpression();

        expr.Should().BeOfType<StringLiteral>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    #endregion

    #region ParseSingleStatement

    [Fact]
    public void ParseSingleStatement_Assignment()
    {
        var parser = new ParserNs.Parser(Lex("x = 42"));
        var stmt = parser.ParseSingleStatement();

        stmt.Should().BeOfType<Assignment>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleStatement_IfStatement()
    {
        var parser = new ParserNs.Parser(Lex("if True:\n    pass"));
        var stmt = parser.ParseSingleStatement();

        stmt.Should().BeOfType<IfStatement>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleStatement_ForLoop()
    {
        var parser = new ParserNs.Parser(Lex("for i in range(10):\n    pass"));
        var stmt = parser.ParseSingleStatement();

        stmt.Should().BeOfType<ForStatement>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleStatement_FunctionDef()
    {
        var parser = new ParserNs.Parser(Lex("def foo(x: int) -> int:\n    return x"));
        var stmt = parser.ParseSingleStatement();

        stmt.Should().BeOfType<FunctionDef>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleStatement_ReturnStatement()
    {
        var parser = new ParserNs.Parser(Lex("return 42"));
        var stmt = parser.ParseSingleStatement();

        stmt.Should().BeOfType<ReturnStatement>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseSingleStatement_ErrorInput_ReturnsErrorNode()
    {
        var parser = new ParserNs.Parser(Lex("@@@"));
        var stmt = parser.ParseSingleStatement();

        stmt.Should().NotBeNull();
        parser.Diagnostics.HasErrors.Should().BeTrue();
    }

    #endregion

    #region ParseStatements

    [Fact]
    public void ParseStatements_MultipleStatements()
    {
        var parser = new ParserNs.Parser(Lex("x = 1\ny = 2\nz = x + y"));
        var stmts = parser.ParseStatements();

        stmts.Should().HaveCount(3);
        stmts.Should().AllBeOfType<Assignment>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseStatements_EmptyInput()
    {
        var parser = new ParserNs.Parser(Lex(""));
        var stmts = parser.ParseStatements();

        stmts.Should().BeEmpty();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseStatements_MixedStatementTypes()
    {
        var source = "x = 1\nif x > 0:\n    pass\nfor i in range(3):\n    pass";
        var parser = new ParserNs.Parser(Lex(source));
        var stmts = parser.ParseStatements();

        stmts.Should().HaveCount(3);
        stmts[0].Should().BeOfType<Assignment>();
        stmts[1].Should().BeOfType<IfStatement>();
        stmts[2].Should().BeOfType<ForStatement>();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseStatements_WithErrors_ContinuesParsing()
    {
        var source = "x = 1\n@@@\ny = 2";
        var parser = new ParserNs.Parser(Lex(source));
        var stmts = parser.ParseStatements();

        stmts.Count.Should().BeGreaterThanOrEqualTo(1);
        parser.Diagnostics.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void ParseStatements_OnlyNewlines()
    {
        var parser = new ParserNs.Parser(Lex("\n\n\n"));
        var stmts = parser.ParseStatements();

        stmts.Should().BeEmpty();
        parser.Diagnostics.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ParseStatements_EquivalentToParseModule()
    {
        var source = "x = 1\ny = 2";

        var moduleLexer = new LexerNs.Lexer(source);
        var moduleParser = new ParserNs.Parser(moduleLexer.TokenizeAll());
        var module = moduleParser.ParseModule();

        var stmtsLexer = new LexerNs.Lexer(source);
        var stmtsParser = new ParserNs.Parser(stmtsLexer.TokenizeAll());
        var stmts = stmtsParser.ParseStatements();

        stmts.Should().HaveCount(module.Body.Length);
    }

    #endregion
}
