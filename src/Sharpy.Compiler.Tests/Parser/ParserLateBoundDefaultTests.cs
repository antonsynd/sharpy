using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

public class ParserLateBoundDefaultTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    [Fact]
    public void LateBoundDefault_SimpleExpression_SetsIsLateBound()
    {
        var module = Parse("def f(x: int, y: int => 0) -> int:\n    return x + y\n");

        var funcDef = module.Body.OfType<FunctionDef>().Single();
        var paramY = funcDef.Parameters.Single(p => p.Name == "y");

        paramY.IsLateBound.Should().BeTrue();
        paramY.DefaultValue.Should().NotBeNull();
    }

    [Fact]
    public void EarlyBoundDefault_SimpleExpression_IsLateBoundFalse()
    {
        var module = Parse("def f(x: int = 0) -> int:\n    return x\n");

        var funcDef = module.Body.OfType<FunctionDef>().Single();
        var paramX = funcDef.Parameters.Single(p => p.Name == "x");

        paramX.IsLateBound.Should().BeFalse();
        paramX.DefaultValue.Should().NotBeNull();
    }

    [Fact]
    public void LateBoundDefault_ReferencingPriorParam_ParsesCorrectly()
    {
        var module = Parse("def f(x: int, y: int => x + 1) -> int:\n    return y\n");

        var funcDef = module.Body.OfType<FunctionDef>().Single();
        var paramY = funcDef.Parameters.Single(p => p.Name == "y");

        paramY.IsLateBound.Should().BeTrue();
        paramY.DefaultValue.Should().BeOfType<BinaryOp>();
    }

    [Fact]
    public void FatArrowToken_IsLexedCorrectly()
    {
        var lexer = new LexerNs.Lexer("=> x");
        var tokens = lexer.TokenizeAll();

        tokens[0].Type.Should().Be(LexerNs.TokenType.FatArrow);
        tokens[0].Value.Should().Be("=>");
    }

    [Fact]
    public void FatArrow_DoesNotConfuseAssign_LessThanSequence()
    {
        // Ensure '= >' (with space) produces Assign then Greater, not FatArrow
        var lexer = new LexerNs.Lexer("= >");
        var tokens = lexer.TokenizeAll();

        tokens[0].Type.Should().Be(LexerNs.TokenType.Assign);
        tokens[1].Type.Should().Be(LexerNs.TokenType.Greater);
    }
}
