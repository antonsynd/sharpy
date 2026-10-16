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
}
