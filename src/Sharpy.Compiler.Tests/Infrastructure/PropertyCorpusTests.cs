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

    /// <summary>
    /// Standing guard for the #1711 cure: the racy hand-rolled accumulator pattern
    /// (a plain sample list filled from inside a CsCheck .Sample callback, which runs
    /// on threads = logical CPUs by default) must never reappear under Properties/.
    /// The one-time "grep returns 0" recorded in the migration commit body regresses
    /// silently; this test enforces it on every run.
    /// </summary>
    [Fact]
    public void NoPlainSampleResultListAccumulators_UnderProperties()
    {
        // Split so this guard's own source is not a match for the pattern it hunts —
        // the positive control below must be carried by the real synthetic-sample
        // tests in this file, never by the guard itself.
        var pattern = "new List<PropertyCorpus." + "SampleResult>";

        var repoRoot = FindRepoRoot();
        var propertiesDir = Path.Combine(
            repoRoot, "src", "Sharpy.Compiler.Tests", "Properties");

        var offenders = Directory.GetFiles(propertiesDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains(pattern))
            .Select(f => Path.GetRelativePath(repoRoot, f))
            .OrderBy(f => f)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Plain SampleResult list accumulators under Properties/ — migrate to " +
            $"PropertyCorpus.CompileAll:\n  {string.Join("\n  ", offenders)}");

        // Positive control: the same probe finds the pattern in this file's own
        // synthetic-sample tests, proving it detects the spelling it hunts.
        var self = Path.Combine(
            repoRoot, "src", "Sharpy.Compiler.Tests", "Infrastructure", "PropertyCorpusTests.cs");
        Assert.Contains(pattern, File.ReadAllText(self));
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, ".git"))
                || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
