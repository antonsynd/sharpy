using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class OperatorOverloadPropertyTests
{
    private readonly ITestOutputHelper _output;

    public OperatorOverloadPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BinaryOperator_DunderCompiles()
    {
        var samples = PropertyCorpus.CompileAll(GenOperators.BinaryOperatorProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "BinaryOperator_DunderCompiles");
    }

    [Fact]
    public void UnaryOperator_DunderCompiles()
    {
        var samples = PropertyCorpus.CompileAll(GenOperators.UnaryOperatorProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "UnaryOperator_DunderCompiles");
    }

    [Fact]
    public void ComparisonOperator_DunderCompiles()
    {
        var samples = PropertyCorpus.CompileAll(GenOperators.ComparisonOperatorProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ComparisonOperator_DunderCompiles");
    }

    [Fact]
    public void OperatorPrecedence_PreservedThroughCompilation()
    {
        var samples = PropertyCorpus.CompileAll(GenOperators.PrecedenceProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "OperatorPrecedence_PreservedThroughCompilation");
    }

    /// <summary>
    /// An operator dunder with the wrong arity draws exactly SPY0321 on every sample through
    /// <see cref="ErrorInjector.JudgeSource"/> (#2288). Before, one lane mixed this with the unknown
    /// dunder below (SPY0414) and passed when ANY failed analysis hit a quarter of the samples.
    /// </summary>
    [Fact]
    public void WrongArityOperatorDunder_ProducesDiagnostic() =>
        AssertEverySampleDraws(GenOperators.WrongArityOperatorDunderProgram(), "SPY0321",
            "wrong-arity operator dunder");

    /// <summary>A dunder name the language does not define draws exactly SPY0414 on every sample.</summary>
    [Fact]
    public void UnknownDunder_ProducesDiagnostic() =>
        AssertEverySampleDraws(GenOperators.UnknownDunderProgram(), "SPY0414", "unknown dunder");

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
}
