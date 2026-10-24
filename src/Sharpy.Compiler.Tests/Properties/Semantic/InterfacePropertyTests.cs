using CsCheck;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class InterfacePropertyTests
{
    private readonly ITestOutputHelper _output;

    public InterfacePropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InterfaceImplementation_CompilesWhenComplete()
    {
        var samples = PropertyCorpus.CompileAll(
            GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: true),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "InterfaceImplementation_CompilesWhenComplete");
    }

    /// <summary>
    /// Every sample of the incomplete implementation draws exactly SPY0325 (interface method not
    /// implemented) through <see cref="ErrorInjector.JudgeSource"/> (#2288): before, any SPY03xx or
    /// SPY04xx code in a quarter of the samples passed the lane and a throw was swallowed.
    /// </summary>
    [Fact]
    public void InterfaceImplementation_FailsWhenMissing()
    {
        int tested = 0;
        var failures = new List<string>();

        GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: false)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref tested);
                if (ErrorInjector.JudgeSource(source, "SPY0325", CompilerDiagnosticSeverity.Error, null,
                        "interface method missing") is { } failure)
                {
                    lock (failures)
                        failures.Add(failure);
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"interface method missing: {tested - failures.Count}/{tested} drew SPY0325");
        Assert.True(tested > 0, "No incomplete-implementation samples were generated");
        Assert.True(failures.Count == 0,
            $"{failures.Count}/{tested} incomplete-implementation samples misjudged; the first:\n{failures.FirstOrDefault()}");
    }

    [Fact]
    public void ProtocolSynthesis_AddsInterfaceForDunder()
    {
        var samples = PropertyCorpus.CompileAll(
            Gen.OneOfConst("__len__", "__bool__")
                .SelectMany(GenInterfaces.ModuleWithProtocolDunder),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ProtocolSynthesis_AddsInterfaceForDunder");
    }

    /// <summary>An ACCEPTANCE lane: every <see cref="GenInterfaces.ModuleWithInterfaceHierarchy"/> sample
    /// (an interface extending another, implemented by a class) analyses clean. It was named
    /// <c>InterfaceConflict_DetectedInHierarchy</c>, but no generator emits a conflicting hierarchy, so it
    /// never detected one; a detection lane for a conflict is a #2287 roster row (#2324).</summary>
    [Fact]
    public void InterfaceHierarchy_AnalysesClean()
    {
        var samples = PropertyCorpus.CompileAll(
            GenInterfaces.ModuleWithInterfaceHierarchy(),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "InterfaceHierarchy_AnalysesClean");
    }

    [Fact]
    public void MultipleInterfaces_AllValidated()
    {
        var samples = PropertyCorpus.CompileAll(
            GenInterfaces.ModuleWithMultipleInterfaces(),
            m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "MultipleInterfaces_AllValidated");
    }
}
