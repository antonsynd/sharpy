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

    /// <summary>
    /// Every sample of a `with` over a class with no <c>__enter__</c>/<c>__exit__</c> draws exactly
    /// SPY0324 through <see cref="ErrorInjector.JudgeSource"/> (#2288): before, ANY failed analysis
    /// in half the samples passed the lane and a throw was swallowed.
    /// </summary>
    [Fact]
    public void MissingEnterOrExit_ProducesDiagnostic()
    {
        int tested = 0;
        var failures = new List<string>();

        GenContextManagers.MissingEnterOrExitProgram()
            .Sample(source =>
            {
                Interlocked.Increment(ref tested);
                if (ErrorInjector.JudgeSource(source, "SPY0324", CompilerDiagnosticSeverity.Error, null,
                        "with over a non-context-manager") is { } failure)
                {
                    lock (failures)
                        failures.Add(failure);
                }
            }, iter: 50);

        _output.WriteLine($"with over a non-context-manager: {tested - failures.Count}/{tested} drew SPY0324");
        Assert.True(tested > 0, "No missing-enter/exit samples were generated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} missing-enter/exit samples misjudged; the first:\n{failures.FirstOrDefault()}");
    }
}
