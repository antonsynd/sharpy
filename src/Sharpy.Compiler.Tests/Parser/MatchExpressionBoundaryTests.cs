using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// A match expression is the only expression form that consumes a DEDENT. Once it returns, the
/// next token starts the following statement, so no infix or postfix continuation may attach
/// across that boundary. Before the guard, the statement after a match-expression block was read
/// as a continuation of the match expression's value: an <c>if</c> statement became a ternary and
/// demanded <c>else</c> (#1169, SPY0104), a leading <c>(</c> became a call and a leading <c>-</c>
/// silently became a subtraction — a wrong answer with no diagnostic (#1183).
///
/// Each test asserts the *shape*: two statements, the first whose initial value is the bare
/// <see cref="MatchExpression"/>, not a BinaryOp / ConditionalExpression / FunctionCall wrapping it.
/// </summary>
public class MatchExpressionBoundaryTests
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
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "the source is valid: " + string.Join("; ", parser.Diagnostics.GetAll().Select(d => d.Message)));
        return module;
    }

    /// <summary>
    /// Builds a module whose first statement declares a variable from a match-expression block and
    /// whose second statement is <paramref name="following"/>, then asserts the declaration's value
    /// is the match expression itself and returns the second statement for the caller to inspect.
    /// </summary>
    private static Statement ParseMatchThen(string following)
    {
        var source = "code: int = 2\n"
            + "v: int = match code:\n"
            + "    case 1: 100\n"
            + "    case _: 0\n"
            + following + "\n";

        var module = Parse(source);
        module.Body.Should().HaveCount(3, "the match expression must not swallow the next statement");

        var declaration = module.Body[1].Should().BeOfType<VariableDeclaration>().Subject;
        declaration.Name.Should().Be("v");
        declaration.InitialValue.Should().BeOfType<MatchExpression>(
            "no continuation may attach across the block boundary");

        return module.Body[2];
    }

    [Fact]
    public void IfStatementAfterMatchExpression_IsAStatementNotATernary()
    {
        var following = ParseMatchThen("if True:\n    print(v)");

        var ifStatement = following.Should().BeOfType<IfStatement>().Subject;
        ifStatement.Test.Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeTrue();
    }

    [Fact]
    public void ParenthesizedCallAfterMatchExpression_IsAStatementNotACall()
    {
        var following = ParseMatchThen("(print(v))");

        following.Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<Parenthesized>()
            .Which.Expression.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void ListLiteralAfterMatchExpression_IsAStatementNotAnIndex()
    {
        var following = ParseMatchThen("[1, 2]");

        following.Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<ListLiteral>()
            .Which.Elements.Should().HaveCount(2);
    }

    [Fact]
    public void UnaryMinusAfterMatchExpression_IsAStatementNotASubtraction()
    {
        var following = ParseMatchThen("-v");

        following.Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<UnaryOp>()
            .Which.Operator.Should().Be(UnaryOperator.Minus);
    }

    [Fact]
    public void NotExpressionAfterMatchExpression_IsAStatementNotAComparison()
    {
        var following = ParseMatchThen("not v");

        following.Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<UnaryOp>()
            .Which.Operator.Should().Be(UnaryOperator.Not);
    }

    [Fact]
    public void DecoratedDefAfterMatchExpression_IsADefinitionNotAMatMul()
    {
        // `@` is infix matrix-multiply at multiplicative precedence as well as the decorator
        // sigil, so a decorated definition is the multiplicative continuation's statement form.
        var following = ParseMatchThen("@staticmethod\ndef helper():\n    pass");

        var definition = following.Should().BeOfType<FunctionDef>().Subject;
        definition.Name.Should().Be("helper");
        definition.Decorators.Should().ContainSingle();
    }

    [Fact]
    public void MatchExpressionAtEndOfBlock_StillParses()
    {
        var module = Parse(
            "def compute(code: int) -> int:\n"
            + "    v: int = match code:\n"
            + "        case 1: 100\n"
            + "        case _: 0\n"
            + "    return v\n");

        var definition = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        definition.Body.Should().HaveCount(2);
        definition.Body[0].Should().BeOfType<VariableDeclaration>()
            .Which.InitialValue.Should().BeOfType<MatchExpression>();
        definition.Body[1].Should().BeOfType<ReturnStatement>();
    }

    [Fact]
    public void ContinuationsInsideAnArmAreUnaffected()
    {
        // The guard keys on a just-consumed DEDENT, so ordinary continuations inside an arm
        // result — here a ternary and a subtraction — must keep binding normally.
        var module = Parse(
            "code: int = 2\n"
            + "v: int = match code:\n"
            + "    case 1: 100 if code > 0 else 200\n"
            + "    case _: code - 1\n");

        var arms = module.Body[1].Should().BeOfType<VariableDeclaration>()
            .Which.InitialValue.Should().BeOfType<MatchExpression>().Subject.Arms;

        arms.Should().HaveCount(2);
        arms[0].Result.Should().BeOfType<ConditionalExpression>();
        arms[1].Result.Should().BeOfType<BinaryOp>()
            .Which.Operator.Should().Be(BinaryOperator.Subtract);
    }

    [Fact]
    public void NestedMatchExpressionInLastArm_IsTheArmResult()
    {
        // The inner match consumes the arm's newline and its own DEDENT, so the arm is terminated
        // by that DEDENT rather than a NEWLINE — the outer arm must accept it (#1196).
        var module = Parse(
            "code: int = 2\n"
            + "other: int = 1\n"
            + "v: int = match code:\n"
            + "    case 1: 100\n"
            + "    case _: match other:\n"
            + "        case 1: 7\n"
            + "        case _: 8\n");

        var arms = module.Body[2].Should().BeOfType<VariableDeclaration>()
            .Which.InitialValue.Should().BeOfType<MatchExpression>().Subject.Arms;

        arms.Should().HaveCount(2);
        arms[0].Result.Should().BeOfType<IntegerLiteral>();
        arms[1].Result.Should().BeOfType<MatchExpression>()
            .Which.Arms.Should().HaveCount(2);
    }

    [Fact]
    public void NestedMatchExpressionInMiddleArm_LeavesFollowingArmsIntact()
    {
        var module = Parse(
            "code: int = 2\n"
            + "other: int = 1\n"
            + "v: int = match code:\n"
            + "    case 1: match other:\n"
            + "        case 1: 7\n"
            + "        case _: 8\n"
            + "    case 2: 200\n"
            + "    case _: 0\n");

        var arms = module.Body[2].Should().BeOfType<VariableDeclaration>()
            .Which.InitialValue.Should().BeOfType<MatchExpression>().Subject.Arms;

        arms.Should().HaveCount(3, "the nested match must not swallow the following arms");
        arms[0].Result.Should().BeOfType<MatchExpression>()
            .Which.Arms.Should().HaveCount(2);
        arms[1].Result.Should().BeOfType<IntegerLiteral>();
        arms[2].Result.Should().BeOfType<IntegerLiteral>();
    }

    [Fact]
    public void FlatArmMissingItsNewline_StillErrors()
    {
        // The added tolerance keys on a just-consumed DEDENT, so a genuinely run-together arm list
        // must still be rejected.
        var source = "v: int = match 1:\n"
            + "    case 1: 5 case 2: 6\n";

        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        parser.ParseModule();

        parser.Diagnostics.GetErrors().Select(d => d.Message)
            .Should().Contain(m => m.Contains("Expected end of statement"),
                "`case 2` on the same line as the first arm's result is not a valid arm terminator");
    }
}
