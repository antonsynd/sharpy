using System.Collections.Immutable;
using System.Linq;
using CsCheck;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.PrettyTests;

/// <summary>
/// #1247 was found by <c>UnparseIdempotencePropertyTests</c>, which is seed-fresh every run: the
/// discovering input existed for exactly one execution. These are its deterministic replacements.
///
/// <para>The failure mode is specifically an unparse round-trip one. A generated body whose first
/// statement is <c>"world" - False</c> unparses to source that the buggy parser read back as a
/// docstring plus <c>-False</c>, so the second unparse produced a <c>"""world"""</c> line and a
/// separate statement — different text, different meaning, from an AST that had round-tripped
/// through legal source. Asserting only that the two texts match would be a weak guard, so each
/// case also asserts the reparsed shape directly; a future regression fails on <em>what</em> the
/// parser built, not merely on formatting.</para>
/// </summary>
public class UnparseDocStringRoundTripTests
{
    private readonly ITestOutputHelper _output;

    public UnparseDocStringRoundTripTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Module Reparse(string source)
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "unparsed output must be re-parseable: "
            + string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => d.Message)));
        return module;
    }

    /// <summary>
    /// A function whose body opens with the binary expression from the discovering counterexample,
    /// built as an AST rather than as text so the case cannot drift with the generator.
    /// </summary>
    private static Module Repro1247Module() => new()
    {
        Body = ImmutableArray.Create<Statement>(new FunctionDef
        {
            Name = "f",
            Body = ImmutableArray.Create<Statement>(new ExpressionStatement
            {
                Expression = new BinaryOp
                {
                    Operator = BinaryOperator.Subtract,
                    Left = new StringLiteral { Value = "world" },
                    Right = new BooleanLiteral { Value = false }
                }
            })
        })
    };

    [Fact]
    public void StringLedBinaryExpression_SurvivesTheUnparseRoundTrip()
    {
        var pass1 = Unparser.Unparse(Repro1247Module());
        _output.WriteLine(pass1);

        var reparsed = Reparse(pass1);

        var function = reparsed.Body.Single().Should().BeOfType<FunctionDef>().Subject;
        function.DocString.Should().BeNull("the string is the left operand of '-', not a docstring");
        function.Body.Single().Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<BinaryOp>()
            .Which.Operator.Should().Be(BinaryOperator.Subtract);

        Unparser.Unparse(reparsed).Should().Be(pass1);
    }

    /// <summary>
    /// The other direction of the same contract: a real docstring must survive as a docstring. If
    /// the classifier over-corrected and stopped recognizing them, the first assertion above would
    /// still pass, so this case is what stops the fix from becoming "never a docstring".
    /// </summary>
    [Fact]
    public void GenuineDocString_SurvivesTheUnparseRoundTrip()
    {
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(new FunctionDef
            {
                Name = "f",
                DocString = "Real docstring.",
                Body = ImmutableArray.Create<Statement>(new PassStatement())
            })
        };

        var pass1 = Unparser.Unparse(module);
        var reparsed = Reparse(pass1);

        reparsed.Body.Single().Should().BeOfType<FunctionDef>()
            .Which.DocString.Should().Be("Real docstring.");
        Unparser.Unparse(reparsed).Should().Be(pass1);
    }

    /// <summary>
    /// Replays the CsCheck seed that produced the #1247 counterexample. A seed only reproduces
    /// against the generator that produced it, and this batch changed <c>GenStatements.ExprStmt</c>
    /// (grouping is now stripped before the un-round-trippable string-statement test), so the seed's
    /// value is as a recorded historical input rather than as the guard — the guard is
    /// <see cref="StringLedBinaryExpression_SurvivesTheUnparseRoundTrip"/> above, which is
    /// generator-independent. Kept because replaying it costs one iteration and it is the only
    /// surviving link back to the discovery.
    /// </summary>
    [Fact]
    [Trait("Category", "Property")]
    public void DiscoveringSeed_ReplaysClean()
    {
        Gen.Int[1, 4].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenSharpy.Module(ctx);
        }).Sample(module =>
        {
            var pass1 = Unparser.Unparse(module);

            var parser = new ParserNs.Parser(new LexerNs.Lexer(pass1).TokenizeAll());
            if (parser.Diagnostics.HasErrors)
                return;

            var reparsed = parser.ParseModule();
            if (parser.Diagnostics.HasErrors)
                return;

            Unparser.Unparse(reparsed).Should().Be(pass1, "unparse must be idempotent");
        }, seed: "48LXYxopTCZo", iter: 1);
    }
}
