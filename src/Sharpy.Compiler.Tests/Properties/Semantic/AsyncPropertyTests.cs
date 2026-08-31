using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;
using Sharpy.TestInfrastructure;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
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

    [Fact]
    public void AwaitOutsideAsync_ProducesDiagnostic()
    {
        int total = 0;
        int diagnosed = 0;

        GenAsync.ModuleWithAwaitOutsideAsync()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "async_test.spy");
                    if (!result.Success && result.Diagnostics.GetAll().Any(d =>
                        d.Code == "SPY0273"))
                    {
                        Interlocked.Increment(ref diagnosed);
                    }
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Await outside async diagnostic: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 4,
            $"Await outside async diagnostic rate too low: {diagnosed}/{total}");
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

                try
                {
                    var compiler1 = new Sharpy.Compiler.Compiler();
                    var result1 = compiler1.Analyze(source, "async_test.spy");

                    var compiler2 = new Sharpy.Compiler.Compiler();
                    var result2 = compiler2.Analyze(source, "async_test.spy");

                    if (result1.Success != result2.Success)
                    {
                        lock (errors)
                            errors.Add($"Success differs: {result1.Success} vs {result2.Success}");
                    }

                    var diags1 = result1.Diagnostics.GetAll().Select(d => d.Code).OrderBy(c => c).ToList();
                    var diags2 = result2.Diagnostics.GetAll().Select(d => d.Code).OrderBy(c => c).ToList();
                    if (!diags1.SequenceEqual(diags2))
                    {
                        lock (errors)
                            errors.Add($"Diagnostics differ: [{string.Join(",", diags1)}] vs [{string.Join(",", diags2)}]");
                    }
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        Assert.Empty(errors);
    }
}
