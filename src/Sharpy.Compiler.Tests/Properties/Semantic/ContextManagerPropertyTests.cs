using CsCheck;
using Sharpy.TestInfrastructure;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class ContextManagerPropertyTests
{
    private readonly ITestOutputHelper _output;

    public ContextManagerPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ValidContextManager_CompilesClean()
    {
        var samples = PropertyCorpus.CompileAll(GenContextManagers.ValidContextManagerProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ValidContextManager_CompilesClean");
    }

    [Fact]
    public void ContextManagerWithAsBinding_CompilesClean()
    {
        var samples = PropertyCorpus.CompileAll(GenContextManagers.ContextManagerWithAsBinding(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ContextManagerWithAsBinding_CompilesClean");
    }

    [Fact]
    public void AsyncContextManager_CompilesClean()
    {
        var samples = PropertyCorpus.CompileAll(GenContextManagers.AsyncContextManagerProgram(), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "AsyncContextManager_CompilesClean");
    }

    [Fact]
    public void MissingEnterOrExit_ProducesDiagnostic()
    {
        int total = 0;
        int diagnosed = 0;

        GenContextManagers.MissingEnterOrExitProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "context_test.spy");
                    if (!result.Success)
                        Interlocked.Increment(ref diagnosed);
                }
                catch
                {
                    // Swallow
                }
            }, iter: 50);

        _output.WriteLine($"Missing enter/exit diagnostic: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 2,
            $"Missing enter/exit diagnostic rate too low: {diagnosed}/{total}");
    }
}
