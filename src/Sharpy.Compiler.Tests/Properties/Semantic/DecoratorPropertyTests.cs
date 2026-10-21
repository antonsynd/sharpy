using System.Collections.Concurrent;
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
    public void ValidDecorator_CompilesClean()
    {
        var set = RunSamples(GenDecorators.ModuleWithDecoratedFunction(validDecorator: true));

        PrintFailures("Valid decorator compilation", set, s => !s.Success);
        // The valid corpus is valid by construction, so every sample must compile.
        // PrintFailures above dumps source + diagnostics for any that don't (#1031).
        Assert.Equal(set.Total, set.Passed);
    }

    [Fact]
    public void DecoratorStacking_ValidCombinations()
    {
        var set = RunSamples(GenDecorators.ModuleWithDecoratorStack(valid: true));

        PrintFailures("Valid decorator stacking", set, s => !s.Success);
        // Deterministic single valid module (Service.@virtual compute) — must compile.
        Assert.Equal(set.Total, set.Passed);
    }

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

    [Fact]
    public void DataclassDecorator_GeneratesCorrectMembers()
    {
        var set = RunSamples(GenDecorators.ModuleWithDataclass());

        PrintFailures("Dataclass decorator", set, s => !s.Success);
        // Field counts 1-4 are all valid dataclasses — must compile.
        Assert.Equal(set.Total, set.Passed);
    }

    [Fact]
    public void BuiltinDecorators_BehaviorConsistent()
    {
        var errors = new List<string>();

        GenDecorators.ModuleWithDecoratorDeterminism()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

                try
                {
                    var compiler1 = new Sharpy.Compiler.Compiler();
                    var result1 = compiler1.Analyze(source, "decorator_test.spy");

                    var compiler2 = new Sharpy.Compiler.Compiler();
                    var result2 = compiler2.Analyze(source, "decorator_test.spy");

                    if (result1.Success != result2.Success)
                    {
                        lock (errors)
                            errors.Add(
                                $"Success differs: {result1.Success} vs {result2.Success}\n" +
                                $"Source:\n{source}\n" +
                                $"Run 1 diagnostics:\n{FormatDiagnostics(result1.Diagnostics)}\n" +
                                $"Run 2 diagnostics:\n{FormatDiagnostics(result2.Diagnostics)}");
                    }
                }
                catch (Exception ex)
                {
                    lock (errors)
                        errors.Add($"Exception on:\n{source}\n{ex}");
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        foreach (var error in errors)
            _output.WriteLine(error);

        Assert.Empty(errors);
    }

    // -- Instrumentation helpers -------------------------------------------------
    //
    // Failures used to be silently swallowed (uncounted `!Success`, empty `catch`),
    // which hid #1031: the generator emitted invalid programs into a "valid" corpus.
    // These helpers capture each sample's source + diagnostics so a failing round
    // prints exactly what the compiler rejected.

    private readonly record struct DiagInfo(string? Code, string Message, int? Line, int? Column)
    {
        public override string ToString() =>
            $"    [{Code ?? "----"}] ({Line?.ToString() ?? "?"},{Column?.ToString() ?? "?"}) {Message}";
    }

    private sealed record CompileSample(
        string Source,
        bool Success,
        IReadOnlyList<DiagInfo> Diagnostics,
        string? Exception);

    private sealed class SampleSet
    {
        public int Total;
        public int Passed;
        public readonly ConcurrentBag<CompileSample> Samples = new();
    }

    private static IReadOnlyList<DiagInfo> Describe(DiagnosticBag bag) =>
        bag.GetAll()
            .Select(d => new DiagInfo(d.Code, d.Message, d.Line, d.Column))
            .ToList();

    private static string FormatDiagnostics(DiagnosticBag bag) =>
        string.Join("\n", Describe(bag).Select(d => d.ToString()));

    private static SampleSet RunSamples(Gen<Module> gen, int iter = 50)
    {
        var set = new SampleSet();

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            Interlocked.Increment(ref set.Total);

            try
            {
                var compiler = new Sharpy.Compiler.Compiler();
                var result = compiler.Analyze(source, "decorator_test.spy");
                if (result.Success)
                    Interlocked.Increment(ref set.Passed);
                set.Samples.Add(new CompileSample(
                    source, result.Success, Describe(result.Diagnostics), null));
            }
            catch (Exception ex)
            {
                set.Samples.Add(new CompileSample(
                    source, false, Array.Empty<DiagInfo>(), ex.ToString()));
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: iter);

        return set;
    }

    private void PrintFailures(string label, SampleSet set, Func<CompileSample, bool> isFailure)
    {
        var failures = set.Samples.Where(isFailure).ToList();
        _output.WriteLine($"{label}: {set.Passed}/{set.Total} passed, {failures.Count} captured failure(s)");

        foreach (var failure in failures)
        {
            _output.WriteLine("---- CAPTURED FAILURE ----");
            _output.WriteLine(failure.Source);
            if (failure.Exception != null)
                _output.WriteLine("EXCEPTION: " + failure.Exception);
            foreach (var diagnostic in failure.Diagnostics)
                _output.WriteLine(diagnostic.ToString());
        }
    }
}
