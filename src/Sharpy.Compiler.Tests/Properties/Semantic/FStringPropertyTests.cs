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
public class FStringPropertyTests
{
    private readonly ITestOutputHelper _output;

    public FStringPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FString_WithMethodCalls_CompilesClean()
    {
        var samples = new List<PropertyCorpus.SampleResult>();
        GenFStrings.FStringWithMethodCalls()
            .Sample(source => samples.Add(PropertyCorpus.CompileSample(source)), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "FString_WithMethodCalls_CompilesClean");
    }

    [Fact]
    public void FString_WithNestedCalls_CompilesClean()
    {
        var samples = new List<PropertyCorpus.SampleResult>();
        GenFStrings.FStringWithNestedCalls()
            .Sample(source => samples.Add(PropertyCorpus.CompileSample(source)), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "FString_WithNestedCalls_CompilesClean");
    }

    [Fact]
    public void FString_WithFormatSpecs_CompilesClean()
    {
        var samples = new List<PropertyCorpus.SampleResult>();
        GenFStrings.FStringWithFormatSpecs()
            .Sample(source => samples.Add(PropertyCorpus.CompileSample(source)), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "FString_WithFormatSpecs_CompilesClean");
    }

    [Fact]
    public void FString_WithArithmetic_CompilesClean()
    {
        var samples = new List<PropertyCorpus.SampleResult>();
        GenFStrings.FStringWithArithmetic()
            .Sample(source => samples.Add(PropertyCorpus.CompileSample(source)), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "FString_WithArithmetic_CompilesClean");
    }

    [Fact]
    public void FString_ComplexCombined_CompilesClean()
    {
        var samples = new List<PropertyCorpus.SampleResult>();
        GenFStrings.FStringComplexCombined()
            .Sample(source => samples.Add(PropertyCorpus.CompileSample(source)), iter: 50);
        PropertyCorpus.AssertAllPassOrAllowed(samples, allowedCodes: null, _output,
            "FString_ComplexCombined_CompilesClean");
    }
}
