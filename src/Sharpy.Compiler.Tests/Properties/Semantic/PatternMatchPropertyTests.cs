using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class PatternMatchPropertyTests
{
    private readonly ITestOutputHelper _output;

    public PatternMatchPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Every sample draws exactly the SPY0463 warning and no error through the warning arm of
    /// <see cref="ErrorInjector.JudgeSource"/> (#2288): before, ANY failed analysis or ANY warning
    /// counted as a catch in a quarter of the samples, and every sample was also refused with
    /// SPY0266 — the generator's match now sits in a <c>-> None</c> body.
    /// </summary>
    [Fact]
    public void MatchNonExhaustive_ProducesDiagnostic()
    {
        int tested = 0;
        var failures = new List<string>();

        GenMatchPatterns.MatchNonExhaustive()
            .Sample(source =>
            {
                Interlocked.Increment(ref tested);
                if (ErrorInjector.JudgeSource(source, "SPY0463", CompilerDiagnosticSeverity.Warning, null,
                        "non-exhaustive match statement") is { } failure)
                {
                    lock (failures)
                        failures.Add(failure);
                }
            }, iter: 50);

        _output.WriteLine($"non-exhaustive match statement: {tested - failures.Count}/{tested} drew SPY0463");
        Assert.True(tested > 0, "No non-exhaustive match samples were generated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} non-exhaustive match samples misjudged; the first:\n{failures.FirstOrDefault()}");
    }

    /// <summary>
    /// The expression form beside the statement form (#2288 Phase 4): every sample of a
    /// <c>return match …</c> missing its covering arm draws exactly the SPY0416 ERROR — a missing arm
    /// is a runtime <c>SwitchExpressionException</c>, so the expression form is refused where the
    /// statement form only warns.
    /// </summary>
    [Fact]
    public void MatchExpressionNonExhaustive_IsRejected()
    {
        var verdicts = ErrorInjector.JudgeSamples(GenMatchPatterns.MatchExpressionNonExhaustive(),
            source => ErrorInjector.JudgeSource(source, "SPY0416", CompilerDiagnosticSeverity.Error, null,
                "non-exhaustive match expression"), iter: 50);

        _output.WriteLine($"non-exhaustive match expression: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} drew SPY0416");
        Assert.True(verdicts.Tested > 0, "No non-exhaustive match expression samples were generated");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} non-exhaustive match expression samples misjudged; the first:\n{verdicts.First}");
    }

    /// <summary>The positive control of <see cref="MatchExpressionNonExhaustive_IsRejected"/>: the same
    /// functions with the covering arm kept analyse clean, sample for sample — so SPY0416 is drawn by
    /// the missing arm, not by the match expression itself.</summary>
    [Fact]
    public void MatchExpressionExhaustive_AnalysesClean()
    {
        var verdicts = ErrorInjector.JudgeSamples(GenMatchPatterns.MatchExpressionExhaustive(),
            source => ErrorInjector.JudgeClean(source, "exhaustive match expression"), iter: 50);

        _output.WriteLine($"exhaustive match expression: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} analysed clean");
        Assert.True(verdicts.Tested > 0, "No exhaustive match expression samples were generated");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} exhaustive match expression samples refused; the first:\n{verdicts.First}");
    }
}
