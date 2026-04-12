using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

public class ByteStringParserTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = new List<LexerNs.Token>();
        while (true)
        {
            var token = lexer.NextToken();
            tokens.Add(token);
            if (token.Type == LexerNs.TokenType.Eof)
                break;
        }
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    [Fact]
    public void Parse_ByteStringLiteral()
    {
        var module = Parse("x = b\"hello\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assignment.Value.Should().BeOfType<BytesLiteralExpression>().Subject;
        literal.Value.Should().Be("hello");
    }

    [Fact]
    public void Parse_ByteStringLiteral_Empty()
    {
        var module = Parse("x = b\"\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assignment.Value.Should().BeOfType<BytesLiteralExpression>().Subject;
        literal.Value.Should().Be("");
    }

    [Fact]
    public void Parse_ByteStringLiteral_WithEscapes()
    {
        var module = Parse("x = b\"\\x00\\xff\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assignment.Value.Should().BeOfType<BytesLiteralExpression>().Subject;
        literal.Value.Should().Be("\x00\xff");
    }

    [Fact]
    public void Parse_ByteStringLiteral_SpanTracking()
    {
        var module = Parse("x = b\"hello\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assignment.Value.Should().BeOfType<BytesLiteralExpression>().Subject;
        literal.LineStart.Should().Be(1);
        literal.ColumnStart.Should().Be(5);
    }

    [Fact]
    public void Parse_ByteStringLiteral_AsExpressionStatement()
    {
        var module = Parse("b\"data\"");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<BytesLiteralExpression>()
            .Which.Value.Should().Be("data");
    }
}
