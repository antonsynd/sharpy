using CsCheck;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

public class PropertyCorpusTests
{
    private readonly ITestOutputHelper _output;

    public PropertyCorpusTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void NullSlot_FailsWithNamedIndex_NotNRE()
    {
        var samples = new List<PropertyCorpus.SampleResult?>
        {
            new("x: int = 1", true, new[] { "SPY0000" }),
            null!,
            new("y: str = 2", false, new[] { "SPY0220" }),
        };

        var ex = Assert.ThrowsAny<Exception>(() =>
            PropertyCorpus.AssertAllPassOrAllowed(
                samples!, allowedCodes: null, _output, "NullSlotTest"));

        Assert.Contains("null sample slot at index 1", ex.Message);
        Assert.DoesNotContain("NullReferenceException", ex.GetType().Name);
    }

    [Fact]
    public void NullErrorCodes_FailsWithNamedIndex()
    {
        var samples = new List<PropertyCorpus.SampleResult>
        {
            new("x: int = 1", true, new[] { "SPY0000" }),
            new("y: str = 2", false, null!),
        };

        var ex = Assert.ThrowsAny<Exception>(() =>
            PropertyCorpus.AssertAllPassOrAllowed(
                samples, allowedCodes: null, _output, "NullCodesTest"));

        Assert.Contains("null ErrorCodes at index 1", ex.Message);
    }

    [Fact]
    public void FailingSample_NamesSource()
    {
        var source = "x: int = \"not_an_int\"";
        var samples = new List<PropertyCorpus.SampleResult>
        {
            new(source, false, new[] { "SPY0220" }),
        };

        var ex = Assert.ThrowsAny<Exception>(() =>
            PropertyCorpus.AssertAllPassOrAllowed(
                samples, allowedCodes: null, _output, "SourceNameTest"));

        Assert.Contains(source, ex.Message);
        Assert.Contains("SPY0220", ex.Message);
    }

    [Fact]
    public void CompileSample_ThrowingInput_YieldsExceptionCode()
    {
        // null source triggers NullReferenceException or ArgumentNullException in the compiler
        var result = PropertyCorpus.CompileSample(null!);

        Assert.False(result.Passed);
        Assert.Single(result.ErrorCodes);
        Assert.StartsWith("EXCEPTION:", result.ErrorCodes[0]);
    }

    [Fact]
    public void CompileSample_ValidSource_ReturnsNonNullResult()
    {
        var result = PropertyCorpus.CompileSample("x: int = 42");

        Assert.NotNull(result);
        Assert.NotNull(result.ErrorCodes);
        Assert.Equal("x: int = 42", result.Source);
    }

    [Fact]
    public void CompileAll_ReturnsExactlyIterSamples()
    {
        var gen = Gen.Const("x: int = 42");
        var samples = PropertyCorpus.CompileAll(gen, iter: 10);

        Assert.Equal(10, samples.Count);
        Assert.All(samples, s =>
        {
            Assert.NotNull(s);
            Assert.NotNull(s.ErrorCodes);
        });
    }

    [Fact]
    public void CompileAll_GenericOverload_ReturnsExactlyIterSamples()
    {
        var gen = Gen.Const(42);
        var samples = PropertyCorpus.CompileAll(gen, i => $"x: int = {i}", iter: 10);

        Assert.Equal(10, samples.Count);
        Assert.All(samples, s =>
        {
            Assert.NotNull(s);
            Assert.NotNull(s.ErrorCodes);
        });
    }
}
