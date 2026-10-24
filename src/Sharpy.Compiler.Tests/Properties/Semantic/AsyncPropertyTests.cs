using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;
using Sharpy.TestInfrastructure;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class AsyncPropertyTests
{
    private readonly ITestOutputHelper _output;

    public AsyncPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AsyncFunction_CompilesWithAwait()
    {
        var samples = PropertyCorpus.CompileAll(
            GenAsync.ModuleWithAsyncFunction(valid: true),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "AsyncFunction_CompilesWithAwait");
    }

    /// <summary>
    /// Every sample draws exactly SPY0273 through <see cref="ErrorInjector.JudgeSource"/> (#2288):
    /// before, a quarter of the samples carrying SPY0273 among any other codes passed the lane, a
    /// throw was swallowed, and every sample also drew SPY0274 because main awaited the sync
    /// `process()` — the generator now calls it synchronously, so the await outside async is the
    /// program's one broken fact.
    /// </summary>
    [Fact]
    public void AwaitOutsideAsync_ProducesDiagnostic()
    {
        int tested = 0;
        var failures = new List<string>();

        GenAsync.ModuleWithAwaitOutsideAsync()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);
                if (ErrorInjector.JudgeSource(source, "SPY0273", CompilerDiagnosticSeverity.Error, null,
                        "await outside async") is { } failure)
                {
                    lock (failures)
                        failures.Add(failure);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"await outside async: {tested - failures.Count}/{tested} drew SPY0273");
        Assert.True(tested > 0, "No await-outside-async samples were generated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} await-outside-async samples misjudged; the first:\n{failures.FirstOrDefault()}");
    }

    [Fact]
    public void AsyncFunction_ReturnTypeInference()
    {
        var samples = PropertyCorpus.CompileAll(
            GenAsync.ModuleWithAsyncFunction(valid: true),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "AsyncFunction_ReturnTypeInference");
    }

    [Fact]
    public void AsyncFunction_Deterministic()
    {
        var errors = new List<string>();

        GenAsync.ModuleWithAsyncDeterminism()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);

                // A throw fails the sample: it used to be swallowed (#2288). Two analyses must agree on
                // success and every diagnostic (PropertyAnalysis.Fingerprint compares runs, judges nothing).
                var first = PropertyAnalysis.Fingerprint(source);
                var second = PropertyAnalysis.Fingerprint(source);
                if (first != second)
                {
                    lock (errors)
                        errors.Add($"Two analyses disagree:\nrun 1: {first}\nrun 2: {second}");
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        Assert.Empty(errors);
    }
}
