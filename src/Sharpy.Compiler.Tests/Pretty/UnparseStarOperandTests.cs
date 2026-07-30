using System.Collections.Immutable;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Xunit;
using SharpyLexer = Sharpy.Compiler.Lexer.Lexer;
using SharpyParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.PrettyTests;

/// <summary>
/// Unparse output must be a fixed point: <c>unparse(parse(unparse(ast))) == unparse(ast)</c>. A
/// bare starred form (<c>*x</c>, or the unpacking tuple <c>a, *b</c>) is only grammatical as a call
/// argument, a collection element, an unpacking target or a standalone expression — never as an
/// operand. Written bare in operand position it re-parsed as something else entirely, and after
/// <c>??</c> it did so *silently*: `a ?? *b` came back as `a?? * b`, two postfix early-return `?`
/// and a multiplication (#1172, seed CsCheck_Seed=1cE5FmA5nL2h).
///
/// These are the deterministic counterparts of UnparseIdempotencePropertyTests, which only reaches
/// the shapes its seed happens to generate.
/// </summary>
public class UnparseStarOperandTests
{
    private static Module Parse(string source)
    {
        var lexer = new SharpyLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new SharpyParser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "unparse output must be parseable, got: " + source);
        return module;
    }

    /// <summary>
    /// Unparses <paramref name="module"/>, re-parses that text and unparses again, asserting the
    /// two renderings are identical, and returns the canonical text.
    /// </summary>
    private static string RoundTrip(Module module)
    {
        var pass1 = Unparser.Unparse(module);
        var pass2 = Unparser.Unparse(Parse(pass1));
        pass2.Should().Be(pass1, "unparse output must be a fixed point");
        return pass1;
    }

    private static Module ModuleOf(Expression value) => new()
    {
        Body = ImmutableArray.Create<Statement>(new ExpressionStatement { Expression = value })
    };

    private static StarExpression Star(string name) =>
        new() { Operand = new Identifier { Name = name } };

    private static BinaryOp NullCoalesce(Expression left, Expression right) =>
        new() { Operator = BinaryOperator.NullCoalesce, Left = left, Right = right };

    [Fact]
    public void NullCoalesceOverBytesLiteralWithStarUnpack_IsAFixedPoint()
    {
        // The exact shape the property test's seed produced.
        var module = ModuleOf(NullCoalesce(
            new BytesLiteralExpression { Value = "test" },
            new StarExpression { Operand = new SuperExpression() }));

        RoundTrip(module).Should().Be("b\"test\" ?? (*super(),)\n");
    }

    [Fact]
    public void StarExpressionAsRightOperand_IsAFixedPoint()
    {
        var module = ModuleOf(NullCoalesce(new Identifier { Name = "a" }, Star("b")));

        RoundTrip(module).Should().Be("a ?? (*b,)\n");
    }

    [Fact]
    public void StarExpressionAsLeftOperand_IsAFixedPoint()
    {
        var module = ModuleOf(NullCoalesce(Star("b"), new Identifier { Name = "a" }));

        RoundTrip(module).Should().Be("(*b,) ?? a\n");
    }

    [Fact]
    public void SpreadElementAsOperand_IsAFixedPoint()
    {
        var module = ModuleOf(NullCoalesce(
            new Identifier { Name = "a" },
            new SpreadElement { Value = new Identifier { Name = "b" } }));

        RoundTrip(module).Should().Be("a ?? (*b,)\n");
    }

    [Fact]
    public void StarExpressionAsArithmeticOperand_IsAFixedPoint()
    {
        var module = ModuleOf(new BinaryOp
        {
            Operator = BinaryOperator.Add,
            Left = new Identifier { Name = "a" },
            Right = Star("b")
        });

        RoundTrip(module).Should().Be("a + (*b,)\n");
    }

    [Fact]
    public void StarExpressionAsPostfixObject_IsAFixedPoint()
    {
        var module = ModuleOf(new QuestionMarkExpression { Operand = Star("b") });

        RoundTrip(module).Should().Be("(*b,)?\n");
    }

    [Fact]
    public void UnpackingTupleAsOperand_IsAFixedPoint()
    {
        var module = ModuleOf(NullCoalesce(
            new Identifier { Name = "a" },
            new TupleLiteral
            {
                Elements = ImmutableArray.Create<Expression>(
                    new Identifier { Name = "x" }, Star("b"))
            }));

        RoundTrip(module).Should().Be("a ?? (x, *b)\n");
    }

    [Fact]
    public void SingleElementUnpackingTupleAsOperand_IsAFixedPoint()
    {
        var module = ModuleOf(NullCoalesce(
            new Identifier { Name = "a" },
            new TupleLiteral { Elements = ImmutableArray.Create<Expression>(Star("b")) }));

        RoundTrip(module).Should().Be("a ?? (*b,)\n");
    }

    [Fact]
    public void StarInPositionsThatAllowItStaysBare()
    {
        // Unpacking target, call argument, collection element and standalone expression: the bare
        // form is the grammatical one here, so parenthesizing would be wrong (and, for the target,
        // would change the parse).
        foreach (var source in new[]
        {
            "def main():\n    first, *rest = items\n",
            "f(*args)\n",
            "xs = [*a, b]\n",
            "*b\n"
        })
        {
            Unparser.Unparse(Parse(source)).Should().Be(source);
            RoundTrip(Parse(source)).Should().Be(source);
        }
    }
}
