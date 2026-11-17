using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// P93 D4 (plan-67a01f), the parser half: a comparison operator that follows a <see cref="TypeCheck"/>
/// head with a dotted-name reading (<see cref="TypeCheck.IdentityOperand"/>, #2669) makes that head the
/// chain's first <c>is</c> link. The chain is built over <c>[Value, IdentityOperand, …]</c> and keeps
/// the head's type spelling as <see cref="ComparisonChain.HeadCheckType"/>, which the checker reads to
/// give link 0 <c>CheckTypeCheck</c>'s rule (enum member → identity link; else SPY0349). A head with no
/// dotted-name reading stays a <see cref="TypeCheck"/> operand, as before. Mutation M4 (the parser drops
/// the head's identity operand again) turns the chain-shape facts here red.
/// </summary>
public class TypeSpelledChainHeadParserTests
{
    private static Expression ParseExpression(string source)
    {
        var parser = new SParser(new SLexer(source).TokenizeAll());
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse("`{0}` parses", source);
        return module.Body.Should().ContainSingle().Which.Should().BeOfType<ExpressionStatement>().Subject.Expression;
    }

    private static void ShouldBeDottedName(Expression expression, params string[] segments)
    {
        var expr = expression;
        for (var i = segments.Length - 1; i > 0; i--)
        {
            var access = expr.Should().BeOfType<MemberAccess>().Subject;
            access.Member.Should().Be(segments[i]);
            expr = access.Object;
        }
        expr.Should().BeOfType<Identifier>().Which.Name.Should().Be(segments[0]);
    }

    [Fact]
    public void IsLinkAfterADottedHead_IsAThreeOperandIsChain()
    {
        var chain = ParseExpression("c is Color.RED is d").Should().BeOfType<ComparisonChain>().Subject;

        chain.Operands.Should().HaveCount(3);
        chain.Operands[0].Should().BeOfType<Identifier>().Which.Name.Should().Be("c");
        ShouldBeDottedName(chain.Operands[1], "Color", "RED");
        chain.Operands[2].Should().BeOfType<Identifier>().Which.Name.Should().Be("d");
        chain.Operators.Should().Equal(ComparisonOperator.Is, ComparisonOperator.Is);
        chain.OperatorPositions.Should().Equal((1, 3), (1, 16));
        chain.HeadCheckType.Should().NotBeNull();
        chain.HeadCheckType!.NameParts.Should().Equal("Color", "RED");
        (chain.LineStart, chain.ColumnStart).Should().Be((1, 1));
    }

    [Fact]
    public void EqualityLinkAfterADottedHead_IsAnIsThenEqualChain()
    {
        var chain = ParseExpression("c is Color.RED == d").Should().BeOfType<ComparisonChain>().Subject;

        chain.Operands.Should().HaveCount(3);
        chain.Operands[0].Should().BeOfType<Identifier>().Which.Name.Should().Be("c");
        ShouldBeDottedName(chain.Operands[1], "Color", "RED");
        chain.Operators.Should().Equal(ComparisonOperator.Is, ComparisonOperator.Equal);
        chain.HeadCheckType.Should().NotBeNull();
    }

    [Fact]
    public void NestedTypeSpelling_StillHeadsTheChain_TheCheckerRefusesIt()
    {
        // `Outer.Inner` has the same dotted-name reading as an enum member: only the checker can tell
        // them apart (T3: SPY0349 for a non-member), so the parser keeps the type spelling on the chain.
        var chain = ParseExpression("x is Outer.Inner is d").Should().BeOfType<ComparisonChain>().Subject;

        ShouldBeDottedName(chain.Operands[1], "Outer", "Inner");
        chain.Operators.Should().Equal(ComparisonOperator.Is, ComparisonOperator.Is);
        chain.HeadCheckType!.NameParts.Should().Equal("Outer", "Inner");
    }

    [Fact]
    public void LongerChainAfterAQualifiedHead_KeepsEveryLink()
    {
        var chain = ParseExpression("c is mod.Color.RED is d == e").Should().BeOfType<ComparisonChain>().Subject;

        // `mod` is lowercase, so `is mod...` is not a type-test spelling at all: an ordinary chain.
        chain.HeadCheckType.Should().BeNull();
        chain.Operators.Should().Equal(ComparisonOperator.Is, ComparisonOperator.Is, ComparisonOperator.Equal);

        var typed = ParseExpression("c is Mod.Color.RED is d == e").Should().BeOfType<ComparisonChain>().Subject;
        ShouldBeDottedName(typed.Operands[1], "Mod", "Color", "RED");
        typed.Operands.Should().HaveCount(4);
        typed.Operators.Should().Equal(ComparisonOperator.Is, ComparisonOperator.Is, ComparisonOperator.Equal);
        typed.HeadCheckType!.NameParts.Should().Equal("Mod", "Color", "RED");
    }

    [Fact]
    public void SingleNameHead_HasNoDottedReading_StaysATypeCheckOperand()
    {
        // A bare name gets no IdentityOperand (#2669), so the head is unchanged: the type test is the
        // left operand of the `is` comparison (the checker refuses the TypeCheck, SPY0349).
        var binary = ParseExpression("x is Dog is d").Should().BeOfType<BinaryOp>().Subject;

        binary.Operator.Should().Be(BinaryOperator.Is);
        var head = binary.Left.Should().BeOfType<TypeCheck>().Subject;
        head.CheckType.Name.Should().Be("Dog");
        head.IdentityOperand.Should().BeNull();
        binary.Right.Should().BeOfType<Identifier>().Which.Name.Should().Be("d");
    }

    [Fact]
    public void GenericSpelling_HasNoDottedReading_StaysATypeCheckOperand()
    {
        ParseExpression("x is Box[int] == d").Should().BeOfType<BinaryOp>()
            .Which.Left.Should().BeOfType<TypeCheck>().Which.IdentityOperand.Should().BeNull();
    }

    [Fact]
    public void LoneDottedTypeTest_IsUnchanged()
    {
        // Positive control: with no comparison after it, the head stays a TypeCheck with its reading.
        var check = ParseExpression("c is Color.RED").Should().BeOfType<TypeCheck>().Subject;
        ShouldBeDottedName(check.IdentityOperand!, "Color", "RED");
    }

    [Fact]
    public void IsNotHead_IsNotATypeTest_AndHasNoHeadCheckType()
    {
        // `is not` never reads as a TypeCheck head (the token after `is` is `not`), so the chain is
        // the ordinary one and the checker sees no type spelling.
        var chain = ParseExpression("c is not Color.RED == d").Should().BeOfType<ComparisonChain>().Subject;

        ShouldBeDottedName(chain.Operands[1], "Color", "RED");
        chain.Operators.Should().Equal(ComparisonOperator.IsNot, ComparisonOperator.Equal);
        chain.HeadCheckType.Should().BeNull();
    }

    [Fact]
    public void ParenthesizedHead_IsAnOrdinaryOperand()
    {
        var chain = ParseExpression("c is (Color.RED) is d").Should().BeOfType<ComparisonChain>().Subject;

        chain.Operands[1].Should().BeOfType<Parenthesized>();
        chain.HeadCheckType.Should().BeNull();
    }

    [Theory]
    [InlineData("c is Color.RED is d")]
    [InlineData("c is Color.RED == d")]
    [InlineData("x is Outer.Inner is d")]
    [InlineData("c is Mod.Color.RED is d == e")]
    public void TypeSpelledHead_UnparsesToItsSource(string source)
    {
        var module = new SParser(new SLexer(source).TokenizeAll()).ParseModule();
        Pretty.Unparser.Unparse(module).TrimEnd().Should().Be(source);
    }
}
