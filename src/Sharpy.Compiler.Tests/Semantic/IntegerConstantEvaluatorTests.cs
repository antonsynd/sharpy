using System.Linq;
using System.Numerics;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Direct tests for <see cref="IntegerConstantEvaluator"/>'s recursive arithmetic (#1234) — the
/// plan's unit-test deliverable Batch C skipped. The evaluator is the ONE authority both the
/// checker's constant fold (SPY0348/SPY0328) and the E2 lowering re-derivation read, so a wrong
/// cell here is a wrong fold everywhere; these pin the recursion shape itself, independent of
/// the fixtures that pin the end-to-end diagnostics.
/// </summary>
public class IntegerConstantEvaluatorTests
{
    /// <summary>
    /// Parses <paramref name="exprSource"/> as the value of a module-level expression statement
    /// and returns that expression. Parsing through the real parser keeps these tests honest
    /// about the AST shapes the evaluator actually receives (parenthesization, unary nesting,
    /// radix literals) rather than hand-building nodes it might never see.
    /// </summary>
    private static Expression ParseValue(string exprSource)
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer(exprSource + "\n").TokenizeAll());
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            $"`{exprSource}` must parse cleanly; got: "
            + string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => d.Message)));
        var stmt = module.Body.OfType<ExpressionStatement>().Single();
        return stmt.Expression;
    }

    [Theory]
    [InlineData("(2 * 3) * 4", 24)]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("10 - 3 - 4", 3)]
    [InlineData("-(2 + 3)", -5)]
    [InlineData("-2 * -3", 6)]
    [InlineData("0x10 + 0o10 + 0b10", 26)]
    [InlineData("1_000_000 * 2", 2000000)]
    public void ConstantSubtrees_EvaluateExactly(string source, long expected)
    {
        IntegerConstantEvaluator.TryGetConstantInteger(ParseValue(source), out var value)
            .Should().BeTrue($"`{source}` is a constant integer subtree");
        value.Should().Be(new BigInteger(expected));
    }

    [Fact]
    public void ResultsBeyondLong_AreExact_BigIntegerNotClamped()
    {
        // The evaluator owns exactness; the CALLER owns bounds. 2**64-shaped magnitudes must
        // come back exact so SPY0348's bounds check judges the true value.
        IntegerConstantEvaluator.TryGetConstantInteger(
            ParseValue("4294967296 * 4294967296"), out var value).Should().BeTrue();
        value.Should().Be(BigInteger.Pow(2, 64));
    }

    [Theory]
    [InlineData("x * 3")]
    [InlineData("2 + f()")]
    [InlineData("2.0 * 3")]
    [InlineData("\"a\" + \"b\"")]
    public void NonConstantOrNonIntegerOperands_ReturnFalse(string source)
    {
        IntegerConstantEvaluator.TryGetConstantInteger(ParseValue(source), out _)
            .Should().BeFalse($"`{source}` is not a constant integer subtree");
    }

    [Fact]
    public void FoldedSubtree_ComposesIntoPowerFolding()
    {
        // (2*3) ** 2 — the base is itself a folded subtree. The power fold reads the same
        // evaluator, so this composition is what makes SPY0328 see 36, not a non-constant.
        var power = (BinaryOp)ParseValue("(2 * 3) ** 2");
        IntegerConstantEvaluator.TryGetConstantInteger(power.Left, out var baseValue)
            .Should().BeTrue("the parenthesized product is constant");
        baseValue.Should().Be(new BigInteger(6));
    }
}
