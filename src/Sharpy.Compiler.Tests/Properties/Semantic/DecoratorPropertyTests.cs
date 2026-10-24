using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class DecoratorPropertyTests
{
    private readonly ITestOutputHelper _output;

    public DecoratorPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ValidDecorator_CompilesClean() =>
        AssertEverySampleAnalysesClean(GenDecorators.ModuleWithDecoratedFunction(validDecorator: true), "valid decorator");

    /// <summary>Deterministic single valid module (Service.@virtual compute) — must analyse clean.</summary>
    [Fact]
    public void DecoratorStacking_ValidCombinations() =>
        AssertEverySampleAnalysesClean(GenDecorators.ModuleWithDecoratorStack(valid: true), "valid decorator stack");

    /// <summary>
    /// Each invalid combination is a conflicting-access-modifier pair, so every sample must draw
    /// exactly SPY0430 (ConflictingAccessModifiers) — the combination rule itself, not an incidental
    /// single-decorator error (#1031) — judged through the one refusal seam (#2288): the lane used to
    /// compare codes itself, which also admitted a second error beside SPY0430.
    /// </summary>
    [Fact]
    public void DecoratorStacking_InvalidCombinations()
    {
        var verdicts = ErrorInjector.JudgeSamples(GenDecorators.ModuleWithDecoratorStack(valid: false),
            source => ErrorInjector.JudgeSource(source, "SPY0430", CompilerDiagnosticSeverity.Error, null,
                "conflicting access-modifier decorators"), iter: 50);
        _output.WriteLine($"Invalid decorator stacking: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} drew SPY0430");

        Assert.True(verdicts.Tested > 0, "No invalid decorator stacks were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} invalid decorator stacks misjudged; the first:\n{verdicts.First}");
    }

    /// <summary>Field counts 1-4 are all valid dataclasses — every sample must analyse clean.</summary>
    [Fact]
    public void DataclassDecorator_GeneratesCorrectMembers() =>
        AssertEverySampleAnalysesClean(GenDecorators.ModuleWithDataclass(), "dataclass");

    /// <summary>Two analyses of each sample agree: success, and every diagnostic's code, position and
    /// message (<see cref="PropertyAnalysis.Fingerprint"/> — the lane compares two runs and judges nothing).
    /// A throw fails the sample.</summary>
    [Fact]
    public void BuiltinDecorators_BehaviorConsistent()
    {
        var errors = new List<string>();

        GenDecorators.ModuleWithDecoratorDeterminism()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                var first = PropertyAnalysis.Fingerprint(source);
                var second = PropertyAnalysis.Fingerprint(source);
                if (first != second)
                {
                    lock (errors)
                        errors.Add($"Two analyses disagree on:\n{source}\nrun 1: {first}\nrun 2: {second}");
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        foreach (var error in errors)
            _output.WriteLine(error);

        Assert.Empty(errors);
    }

    /// <summary>Every sample of <paramref name="gen"/> analyses clean through <see cref="ErrorInjector.JudgeClean"/>
    /// — judged per sample; the first refused source (with its codes) is the failure (#1031 is why the old
    /// helpers printed every captured failure: a "valid" corpus that emitted invalid programs).</summary>
    private void AssertEverySampleAnalysesClean(Gen<Module> gen, string label)
    {
        var verdicts = ErrorInjector.JudgeSamples(gen, source => ErrorInjector.JudgeClean(source, label), iter: 50);
        _output.WriteLine($"{label}: {verdicts.Tested - verdicts.Failures}/{verdicts.Tested} analysed clean");
        Assert.True(verdicts.Tested > 0, $"No {label} programs were sampled");
        Assert.True(verdicts.First is null,
            $"{verdicts.Failures}/{verdicts.Tested} {label} programs refused; the first:\n{verdicts.First}");
    }
}
