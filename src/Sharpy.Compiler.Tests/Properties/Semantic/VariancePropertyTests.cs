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
public class VariancePropertyTests
{
    private readonly ITestOutputHelper _output;

    public VariancePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CovariantInterface_CompilesClean() =>
        AssertEverySampleIsDiagnosticFree(GenVariance.CovariantInterfaceProgram(), "covariant interface");

    [Fact]
    public void ContravariantInterface_CompilesClean() =>
        AssertEverySampleIsDiagnosticFree(GenVariance.ContravariantInterfaceProgram(), "contravariant interface");

    [Fact]
    public void VarianceOnClass_ProducesDiagnostic() =>
        AssertEverySampleDraws(GenVariance.VarianceOnClassProgram(), "SPY0417", "variance on class");

    [Fact]
    public void CovariantInInputPosition_ProducesDiagnostic() =>
        AssertEverySampleDraws(GenVariance.CovariantInInputPositionProgram(), "SPY0418",
            "covariant in input position");

    [Fact]
    public void ContravariantInOutputPosition_ProducesDiagnostic() =>
        AssertEverySampleDraws(GenVariance.ContravariantInOutputPositionProgram(), "SPY0419",
            "contravariant in output position");

    /// <summary>
    /// Every sample of a one-fact invalid generator draws exactly <paramref name="code"/> through
    /// <see cref="ErrorInjector.JudgeSource"/> (#2288): before, the lane passed when a quarter of the
    /// samples carried the code among any others, and a throw was swallowed.
    /// </summary>
    private void AssertEverySampleDraws(Gen<string> gen, string code, string label)
    {
        int tested = 0;
        var failures = new List<string>();

        gen.Sample(source =>
        {
            Interlocked.Increment(ref tested);
            if (ErrorInjector.JudgeSource(source, code, CompilerDiagnosticSeverity.Error, null, label) is { } failure)
            {
                lock (failures)
                    failures.Add(failure);
            }
        }, iter: 50);

        _output.WriteLine($"{label}: {tested - failures.Count}/{tested} drew {code}");
        Assert.True(tested > 0, $"No {label} samples were generated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} {label} samples misjudged; the first:\n{failures.FirstOrDefault()}");
    }

    /// <summary>
    /// Every sample of a valid generator analyses with no error AND no warning: the interface
    /// methods are spelled with the `: ...` body, not the deprecated body-less form that drew
    /// SPY0464 on every sample before #2288.
    /// </summary>
    private void AssertEverySampleIsDiagnosticFree(Gen<string> gen, string label)
    {
        var verdicts = ErrorInjector.JudgeSamples(gen, source => ErrorInjector.JudgeNoDiagnostic(source, label), iter: 50);
        _output.WriteLine($"{label}: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} analysed with no diagnostic");
        Assert.True(verdicts.Tested > 0, $"No {label} samples were generated");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} {label} samples drew a diagnostic; the first:\n{verdicts.First}");
    }
}
