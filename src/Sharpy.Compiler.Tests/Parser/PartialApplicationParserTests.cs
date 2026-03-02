using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests for partial application (call placeholders) and operator sections.
/// </summary>
public class PartialApplicationParserTests
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

    private static string ParseExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();

        var allErrors = lexer.Diagnostics.GetErrors()
            .Concat(parser.Diagnostics.GetErrors())
            .ToList();

        allErrors.Should().NotBeEmpty("Expected parser to report an error for input: " + source);
        return string.Join("\n", allErrors.Select(d => $"{d.Code}: {d.Message}"));
    }

    #region Partial Application (Call Placeholders)

    [Fact]
    public void SinglePlaceholder_LowersToLambda()
    {
        var module = Parse("f(_)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().HaveCount(1);
        lambda.Parameters[0].Name.Should().Be("__placeholder_0");
        var call = lambda.Body.Should().BeOfType<FunctionCall>().Subject;
        call.Function.Should().BeOfType<Identifier>().Which.Name.Should().Be("f");
        call.Arguments.Should().HaveCount(1);
        call.Arguments[0].Should().BeOfType<Identifier>().Which.Name.Should().Be("__placeholder_0");
    }

    [Fact]
    public void PlaceholderWithFixedArg_LowersToLambda()
    {
        var module = Parse("f(5, _)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().HaveCount(1);
        lambda.Parameters[0].Name.Should().Be("__placeholder_0");
        var call = lambda.Body.Should().BeOfType<FunctionCall>().Subject;
        call.Arguments.Should().HaveCount(2);
        call.Arguments[0].Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("5");
        call.Arguments[1].Should().BeOfType<Identifier>().Which.Name.Should().Be("__placeholder_0");
    }

    [Fact]
    public void MultiplePlaceholders_LowersToLambdaWithMultipleParams()
    {
        var module = Parse("f(_, _, \"Smith\")");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().HaveCount(2);
        lambda.Parameters[0].Name.Should().Be("__placeholder_0");
        lambda.Parameters[1].Name.Should().Be("__placeholder_1");
        var call = lambda.Body.Should().BeOfType<FunctionCall>().Subject;
        call.Arguments.Should().HaveCount(3);
        call.Arguments[0].Should().BeOfType<Identifier>().Which.Name.Should().Be("__placeholder_0");
        call.Arguments[1].Should().BeOfType<Identifier>().Which.Name.Should().Be("__placeholder_1");
        call.Arguments[2].Should().BeOfType<StringLiteral>().Which.Value.Should().Be("Smith");
    }

    [Fact]
    public void KeywordArgPlaceholder_EmitsError()
    {
        var errors = ParseExpectingError("f(x=_)");
        errors.Should().Contain("SPY0130");
    }

    [Fact]
    public void PlaceholderWithSpread_EmitsError()
    {
        var errors = ParseExpectingError("f(_, *args)");
        errors.Should().Contain("SPY0131");
    }

    [Fact]
    public void UnderscoreAssignment_NotAPlaceholder()
    {
        // _ = 5 should be a regular assignment, not a placeholder
        var module = Parse("_ = 5");
        // It should be an Assignment statement, not lowered to a LambdaExpression
        module.Body[0].Should().BeOfType<Assignment>();
    }

    [Fact]
    public void MethodCallPlaceholder_LowersToLambda()
    {
        var module = Parse("obj.method(_, y)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var lambda = exprStmt.Expression.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().HaveCount(1);
        lambda.Parameters[0].Name.Should().Be("__placeholder_0");
        var call = lambda.Body.Should().BeOfType<FunctionCall>().Subject;
        call.Function.Should().BeOfType<MemberAccess>();
        call.Arguments.Should().HaveCount(2);
        call.Arguments[0].Should().BeOfType<Identifier>().Which.Name.Should().Be("__placeholder_0");
        call.Arguments[1].Should().BeOfType<Identifier>().Which.Name.Should().Be("y");
    }

    [Fact]
    public void NestedCallWithPlaceholder_InnerLowered()
    {
        // f(g(_)) -- inner g(_) should be lowered, f sees a lambda argument
        var module = Parse("f(g(_))");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        // The outer call f(...) should remain a FunctionCall (not lowered)
        var outerCall = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        outerCall.Function.Should().BeOfType<Identifier>().Which.Name.Should().Be("f");
        // The inner g(_) is lowered to a LambdaExpression
        outerCall.Arguments.Should().HaveCount(1);
        var innerLambda = outerCall.Arguments[0].Should().BeOfType<LambdaExpression>().Subject;
        innerLambda.Parameters.Should().HaveCount(1);
        var innerCall = innerLambda.Body.Should().BeOfType<FunctionCall>().Subject;
        innerCall.Function.Should().BeOfType<Identifier>().Which.Name.Should().Be("g");
    }

    [Fact]
    public void NoPlaceholder_RemainsUnchanged()
    {
        var module = Parse("f(5, 3)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<FunctionCall>();
    }

    #endregion
}
