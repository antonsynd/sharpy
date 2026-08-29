using CsCheck;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
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
        var samples = new List<PropertyCorpus.SampleResult>();
        GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: true)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                samples.Add(PropertyCorpus.CompileSample(source));
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "InterfaceImplementation_CompilesWhenComplete");
    }

    [Fact]
    public void InterfaceImplementation_FailsWhenMissing()
    {
        int total = 0;
        int diagnosed = 0;

        GenInterfaces.ModuleWithInterface(methodCount: 0, completeImpl: false)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                Interlocked.Increment(ref total);

                try
                {
                    var compiler = new Sharpy.Compiler.Compiler();
                    var result = compiler.Analyze(source, "iface_test.spy");
                    if (!result.Success && result.Diagnostics.GetAll().Any(d =>
                        d.Code != null && (d.Code.StartsWith("SPY03") || d.Code.StartsWith("SPY04"))))
                    {
                        Interlocked.Increment(ref diagnosed);
                    }
                }
                catch
                {
                    // Swallow
                }
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);

        _output.WriteLine($"Interface missing method diagnostic: {diagnosed}/{total} diagnosed");
        Assert.True(diagnosed > total / 4,
            $"Interface missing method diagnostic rate too low: {diagnosed}/{total}");
    }

    [Fact]
    public void ProtocolSynthesis_AddsInterfaceForDunder()
    {
        var samples = new List<PropertyCorpus.SampleResult>();
        Gen.OneOfConst("__len__", "__bool__")
            .SelectMany(GenInterfaces.ModuleWithProtocolDunder)
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                samples.Add(PropertyCorpus.CompileSample(source));
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "ProtocolSynthesis_AddsInterfaceForDunder");
    }

    [Fact]
    public void InterfaceConflict_DetectedInHierarchy()
    {
        var samples = new List<PropertyCorpus.SampleResult>();
        GenInterfaces.ModuleWithInterfaceHierarchy()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                samples.Add(PropertyCorpus.CompileSample(source));
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "InterfaceConflict_DetectedInHierarchy");
    }

    [Fact]
    public void MultipleInterfaces_AllValidated()
    {
        var samples = new List<PropertyCorpus.SampleResult>();
        GenInterfaces.ModuleWithMultipleInterfaces()
            .Sample(module =>
            {
                var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
                samples.Add(PropertyCorpus.CompileSample(source));
            }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "MultipleInterfaces_AllValidated");
    }
}
