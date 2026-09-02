using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Pins what a source generator receives for a decorator argument
/// (<c>GeneratorContextBuilder.ExtractLiteralValue</c>, fed into <c>GeneratorContext.Arguments</c>
/// / <c>KeywordArguments</c>). 4f7be10f3 routed this site through the shared literal classifier
/// and changed the contract without a probe or a direction note (found by the verify round of
/// plan-950124): a float argument now arrives as a boxed <see cref="double"/> where e523ceec3
/// handed the generator the raw source text, and a negated integer arrives as <c>"-42"</c> where
/// e523ceec3 handed it the AST record's <c>ToString()</c>. Integer, string, boolean and None
/// arguments are unchanged. This class is the executing record of that contract; a change to the
/// classifier's value types is a change to the generator API and fails here.
/// </summary>
public class GeneratorContextBuilderLiteralValueTests
{
    [Fact]
    public void IntegerLiteral_ArrivesAsSourceText()
        => Assert.Equal("42", GeneratorContextBuilder.ExtractLiteralValue(new IntegerLiteral { Value = "42" }));

    [Fact]
    public void NegatedIntegerLiteral_ArrivesAsSignedSourceText()
        => Assert.Equal("-42", GeneratorContextBuilder.ExtractLiteralValue(
            new UnaryOp { Operator = UnaryOperator.Minus, Operand = new IntegerLiteral { Value = "42" } }));

    [Fact]
    public void FloatLiteral_ArrivesAsBoxedDouble()
        => Assert.Equal(1.5d, GeneratorContextBuilder.ExtractLiteralValue(new FloatLiteral { Value = "1.5" }));

    [Fact]
    public void NegatedFloatLiteral_ArrivesAsNegativeDouble()
        => Assert.Equal(-1.5d, GeneratorContextBuilder.ExtractLiteralValue(
            new UnaryOp { Operator = UnaryOperator.Minus, Operand = new FloatLiteral { Value = "1.5" } }));

    [Fact]
    public void StringLiteral_ArrivesAsText()
        => Assert.Equal("name", GeneratorContextBuilder.ExtractLiteralValue(new StringLiteral { Value = "name" }));

    [Fact]
    public void BooleanLiteral_ArrivesAsBool()
        => Assert.Equal(true, GeneratorContextBuilder.ExtractLiteralValue(new BooleanLiteral { Value = true }));

    [Fact]
    public void NoneLiteral_ArrivesAsNull()
        => Assert.Null(GeneratorContextBuilder.ExtractLiteralValue(new NoneLiteral()));

    [Fact]
    public void NonLiteral_FallsBackToTheRecordText()
    {
        // The pre-existing fallback for a non-literal argument: the AST record's ToString(), so a
        // generator can at least see the shape it was given. Not a typed contract — a generator
        // needing a computed argument must ask for a literal.
        var expr = new Identifier { Name = "x" };
        var value = GeneratorContextBuilder.ExtractLiteralValue(expr);
        Assert.Equal(expr.ToString(), value);
    }
}
