using System.Collections.Immutable;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

/// <summary>
/// The generated lane's filter fails loudly (#2324, P63b Decision 13): a generator whose every sample the
/// compiler refuses fails with the sink's report — the refusal's code and the first refused source — not
/// with CsCheck's "Failing Where max count", which names neither.
/// </summary>
[Trait("Category", "Property")]
[Collection("PropertySerial")]
public class RejectionSinkTests
{
    private readonly ITestOutputHelper _output;

    public RejectionSinkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Every sample reads a name nothing defines: the failure names the sink's refusal codes and the source.</summary>
    [Fact]
    public void AnAlwaysRefusedGenerator_FailsWithTheSinksReport()
    {
        var refused = Gen.Int[0, 9].Select(n => PrintOf(new Identifier { Name = $"undefined_{n}" }));
        var sink = new RejectionSink("RejectionSinkTests.always refused");

        var thrown = Record.Exception(() => SemanticFilter.WellTypedProgram(refused, sink).Sample(_ => { }, iter: 1));

        var text = thrown?.ToString() ?? "(nothing thrown)";
        _output.WriteLine(text.Length > 2000 ? text[..2000] : text);
        // The failure carries the sink's own bucket (its refusal codes, read from the sink — a code literal
        // here would be a second judging core) and a refused source. The bucket must hold a code: a sink that
        // recorded none describes itself as "refused without an error code" and would match its own text.
        Assert.True(sink.Unexplained.Count > 0, "the sink recorded no rejection");
        Assert.True(sink.Unexplained[0].Codes.Count > 0, $"the sink's rejection carries no code: {sink.Unexplained[0].Describe()}");
        Assert.Contains("unexplained by bucket: " + sink.Unexplained[0].Describe(), text, StringComparison.Ordinal);
        Assert.Contains("undefined_", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The applicability-first filter (#2324, R-IX): a draw its selector skips passes WITHOUT analysis and
    /// never reaches the sink, and a draw it selects passes only when it analyses clean. Even draws are
    /// refused programs the selector skips (they must come through, and no rejection may carry their
    /// source); odd draws are selected — well-typed when <c>n % 4 == 1</c>, refused when <c>n % 4 == 3</c>.
    /// </summary>
    [Fact]
    public void TheApplicabilityFirstFilter_AnalysesOnlyTheDrawsItSelects()
    {
        var draws = Gen.Int[0, 99].Select(n => (N: n, Module: n % 4 == 1
            ? PrintOf(new IntegerLiteral { Value = n.ToString(System.Globalization.CultureInfo.InvariantCulture) })
            : PrintOf(new Identifier { Name = n % 2 == 0 ? $"skipped_{n}" : $"refused_{n}" })));
        var sink = new RejectionSink("RejectionSinkTests.applicability first");
        var passed = new System.Collections.Concurrent.ConcurrentBag<int>();

        SemanticFilter.WellTypedProgram(draws, d => d.N % 2 == 1 ? d.Module : null, sink)
            .Sample(d => passed.Add(d.N), iter: 200);

        var seen = passed.ToList();
        _output.WriteLine($"passed {seen.Count}: {seen.Count(n => n % 2 == 0)} skipped, {seen.Count(n => n % 4 == 1)} admitted; "
            + $"sink drew {sink.Drawn}, accepted {sink.Accepted}, rejected {sink.Rejections.Count} (measured)");
        Assert.True(seen.Any(n => n % 2 == 0), "no skipped draw passed — the filter analysed (and refused) a draw its selector skipped");
        Assert.True(seen.Any(n => n % 4 == 1), "no selected well-typed draw passed — the positive control for admission");
        Assert.True(!seen.Any(n => n % 4 == 3), "a selected refused draw passed — the filter admitted a draw without analysing it");
        Assert.True(sink.Rejections.Count > 0, "the sink recorded no rejection — the selected refused draws never reached it");
        var skippedInSink = sink.Rejections.Where(r => r.Source.Contains("skipped_", StringComparison.Ordinal)).ToList();
        Assert.True(skippedInSink.Count == 0, $"{skippedInSink.Count} skipped draw(s) reached the sink: {skippedInSink.FirstOrDefault()?.Source}");
    }

    private static Module PrintOf(Expression argument) => new()
    {
        Body = ImmutableArray.Create<Statement>(new FunctionDef
        {
            Name = "main",
            Body = ImmutableArray.Create<Statement>(new ExpressionStatement
            {
                Expression = new FunctionCall
                {
                    Function = new Identifier { Name = "print" },
                    Arguments = ImmutableArray.Create<Expression>(argument),
                },
            }),
        }),
    };
}
