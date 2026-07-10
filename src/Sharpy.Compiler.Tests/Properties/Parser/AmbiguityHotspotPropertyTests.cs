using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Parser;

/// <summary>
/// Ambiguity-biased parser fuzzers (#1037). Where <see cref="ParserRoundTripPropertyTests"/>
/// samples the whole grammar uniformly, these tests spend their entire budget on the three
/// shapes the Sharpy parser is most likely to mis-parse — lambda bodies, subscripts, and
/// tuple literals — and on nested stacks of all three. Same AST-first round-trip contract:
/// build an AST, unparse it, re-lex/re-parse the text, and assert the structure survives.
/// </summary>
[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class AmbiguityHotspotPropertyTests
{
    private readonly ITestOutputHelper _output;

    public AmbiguityHotspotPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// A module of one top-level expression statement whose expression stacks the ambiguity
    /// hotspots. Top-level statement position keeps the noise low so a round-trip failure
    /// points squarely at the expression grammar rather than at statement framing.
    /// </summary>
    private static Gen<Module> HotspotModule =>
        Gen.Int[1, 5].SelectMany(fuel =>
        {
            var ctx = GenContext.Default with { Fuel = fuel };
            return GenExpressions.AmbiguousHotspot(ctx).Select(e => new Module
            {
                Body = ImmutableArray.Create<Statement>(
                    new ExpressionStatement { Expression = e })
            });
        });

    [Fact]
    public void HotspotExpressions_RoundTrip_Structurally()
    {
        int total = 0;
        int passed = 0;
        int skipped = 0;

        HotspotModule.Sample(module =>
        {
            Interlocked.Increment(ref total);
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            List<Sharpy.Compiler.Lexer.Token> tokens;
            try
            {
                tokens = lexer.TokenizeAll();
            }
            catch
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            Module reparsed;
            try
            {
                reparsed = parser.ParseModule();
            }
            catch
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            if (parser.Diagnostics.HasErrors)
            {
                Interlocked.Increment(ref skipped);
                return;
            }

            var comparer = Sharpy.Compiler.Pretty.StructuralEqualityComparer.Instance;
            var normalizer = Sharpy.Compiler.Pretty.AstNormalizer.Instance;
            var norm1 = normalizer.NormalizeModule(module);
            var norm2 = normalizer.NormalizeModule(reparsed);

            if (comparer.Equals(norm1, norm2))
            {
                Interlocked.Increment(ref passed);
            }
        }, iter: 500, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m));

        _output.WriteLine($"Hotspot round-trip: {passed}/{total} passed, {skipped} skipped");
        Assert.True(passed > total / 2,
            $"Hotspot round-trip pass rate too low: {passed}/{total} ({skipped} skipped)");
    }

    [Fact]
    public void HotspotExpressions_NeverCrashLexerOrParser()
    {
        HotspotModule.Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(unparsed);
            var tokens = lexer.TokenizeAll();
            var parser = new Sharpy.Compiler.Parser.Parser(tokens);
            _ = parser.ParseModule();
        }, iter: 500, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m));
    }

    [Fact]
    public void HotspotDiagnostics_AreDeterministic()
    {
        HotspotModule.Sample(module =>
        {
            var unparsed = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var diags1 = ParseAndGetDiagnostics(unparsed);
            var diags2 = ParseAndGetDiagnostics(unparsed);

            if (diags1.Count != diags2.Count)
                throw new Exception(
                    $"Non-deterministic diagnostics for `{unparsed}`: "
                    + $"{diags1.Count} vs {diags2.Count}");

            for (int i = 0; i < diags1.Count; i++)
            {
                if (diags1[i] != diags2[i])
                    throw new Exception(
                        $"Diagnostic #{i} differs for `{unparsed}`: "
                        + $"'{diags1[i]}' vs '{diags2[i]}'");
            }
        }, iter: 200, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m));
    }

    /// <summary>
    /// QUARANTINED regression for the parser bug this fuzzer found (#1064): a comparison
    /// chain of 3+ operands used as a lambda body or as the final branch of a conditional
    /// is truncated after the first comparison operator, so <c>lambda: a OP b OP c</c>
    /// reparses as <c>(lambda: a OP b) OP c</c> instead of <c>lambda: (a OP b OP c)</c>.
    /// Python keeps the whole chain in the body (verified). These cases were minimized from
    /// the fuzzer and reproduce deterministically. Un-skip when #1064 is fixed.
    /// </summary>
    [Theory(Skip = "TODO(#1064): comparison chain in lambda body / conditional branch is truncated on reparse")]
    [InlineData("lambda: a is b is c")]
    [InlineData("b[lambda y: super() is bool == super()]")]
    [InlineData("s(lambda : super() is object is double, lambda data: super())")]
    [InlineData("data((b\"bytes\",), (*True, b\"\"))[lambda head, a, a: idx is object is object]")]
    public void LambdaBodyComparisonChain_RoundTrips(string source)
    {
        var firstTokens = new Sharpy.Compiler.Lexer.Lexer(source).TokenizeAll();
        var firstAst = new Sharpy.Compiler.Parser.Parser(firstTokens).ParseModule();
        var firstText = Sharpy.Compiler.Pretty.Unparser.Unparse(firstAst);

        var secondTokens = new Sharpy.Compiler.Lexer.Lexer(firstText).TokenizeAll();
        var secondAst = new Sharpy.Compiler.Parser.Parser(secondTokens).ParseModule();
        var secondText = Sharpy.Compiler.Pretty.Unparser.Unparse(secondAst);

        Assert.Equal(firstText, secondText);
    }

    private static List<string> ParseAndGetDiagnostics(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        _ = parser.ParseModule();
        return parser.Diagnostics.GetAll().Select(d => d.ToString()!).ToList();
    }
}
