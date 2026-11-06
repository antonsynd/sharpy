using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// #2438: a passing Compiler test run read "Test Run Failed" with 0 failed tests because vstest serialized a sweep's
/// 8 M-char output as one string and the testhost ran out of heap. <see cref="BoundedTestOutput"/> is the output every
/// <see cref="IntegrationTestBase"/> writes through: the compile path is muted on demand, and a test whose output passes
/// <see cref="BoundedTestOutput.MaxCharsPerTest"/> fails instead of being written.
/// </summary>
public class BoundedTestOutputTests
{
    private sealed class RecordingOutput : ITestOutputHelper
    {
        public List<string> Lines { get; } = new();

        public long Chars => Lines.Sum(l => (long)l.Length + Environment.NewLine.Length);

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }

    [Fact]
    public void UnderTheCeiling_EveryWriteIsForwardedAndCounted()
    {
        var inner = new RecordingOutput();
        // A line costs its length plus the newline xUnit appends to it.
        int n = Environment.NewLine.Length;
        var output = new BoundedTestOutput(inner, maxChars: 10 + 2 * n);

        output.WriteLine("12345");
        output.WriteLine("{0}{1}", "678", "90");

        Assert.Equal(new[] { "12345", "67890" }, inner.Lines);
        Assert.Equal(10 + 2 * n, output.CharsWritten);
        Assert.False(output.CeilingExceeded);
        output.ThrowIfCeilingExceeded();
    }

    [Fact]
    public void TheWriteThatCrossesTheCeiling_IsDroppedAndThrows_AndSoIsEveryLaterOne()
    {
        var inner = new RecordingOutput();
        int n = Environment.NewLine.Length;
        var output = new BoundedTestOutput(inner, maxChars: 10 + n);
        output.WriteLine("12345678");

        Assert.Throws<TestOutputCeilingExceededException>(() => output.WriteLine("abc"));
        Assert.True(output.CeilingExceeded);
        Assert.Throws<TestOutputCeilingExceededException>(() => output.WriteLine("d"));
        Assert.Throws<TestOutputCeilingExceededException>(() => output.CompilePath.WriteLine("e"));
        Assert.Throws<TestOutputCeilingExceededException>(output.ThrowIfCeilingExceeded);

        // The kept write and ONE notice reach the result; nothing past the ceiling does.
        Assert.Equal(2, inner.Lines.Count);
        Assert.Equal("12345678", inner.Lines[0]);
        Assert.Contains("test output ceiling", inner.Lines[1], StringComparison.Ordinal);
        Assert.Equal(8 + n, output.CharsWritten);
    }

    [Fact]
    public void AMutedCompilePath_ForwardsAndCountsNothing_WhileTheTestsOwnWritesStillPrint()
    {
        var inner = new RecordingOutput();
        var output = new BoundedTestOutput(inner, maxChars: 10) { MuteCompilePath = true };

        output.CompilePath.WriteLine(new string('x', 100));
        output.CompilePath.WriteLine("{0}", new string('y', 100));
        output.WriteLine("report");

        Assert.Equal(new[] { "report" }, inner.Lines);
        Assert.Equal(6 + Environment.NewLine.Length, output.CharsWritten);
        Assert.False(output.CeilingExceeded);

        // Positive control: the same compile-path write unmuted is forwarded.
        output.MuteCompilePath = false;
        output.CompilePath.WriteLine("cs");
        Assert.Equal(new[] { "report", "cs" }, inner.Lines);
    }

    [Fact]
    public void TheBaseClassCeiling_IsOneMillionChars()
        => Assert.Equal(1_000_000, BoundedTestOutput.MaxCharsPerTest);

    /// <summary>The compile helpers catch every exception into an <c>ExecutionResult</c>; the ceiling must survive
    /// that catch. A compile whose generated C# crosses the ceiling throws out of <c>CompileAndExecute</c>, and the
    /// result carries at most the ceiling.</summary>
    [Fact]
    public void ACompileThatCrossesTheCeiling_FailsTheCallerThroughTheHelpersCatch()
    {
        var inner = new RecordingOutput();
        var harness = new Probe(inner);
        harness.Print(new string('x', BoundedTestOutput.MaxCharsPerTest - 10));

        Assert.Throws<TestOutputCeilingExceededException>(() => harness.Compile());
        Assert.True(inner.Chars <= BoundedTestOutput.MaxCharsPerTest + 400,
            $"{inner.Chars} chars reached the test result past the ceiling");
    }

    /// <summary>The same compile with the compile path muted writes nothing and passes; and unmuted under the ceiling,
    /// a non-sweep test's generated C# still reaches its result (the positive control of the mute).</summary>
    [Fact]
    public void AMutedCompile_PassesUnderTheSamePrint_AndAnUnmutedOnePrintsItsGeneratedCSharp()
    {
        var muted = new RecordingOutput();
        var harness = new Probe(muted) { Muted = true };
        harness.Print(new string('x', BoundedTestOutput.MaxCharsPerTest - 10));

        Assert.True(harness.Compile());
        Assert.Single(muted.Lines);

        var unmuted = new RecordingOutput();
        Assert.True(new Probe(unmuted).Compile());
        Assert.Contains(unmuted.Lines, l => l.Contains("=== Generated C# ===", StringComparison.Ordinal));
        Assert.Contains(unmuted.Lines, l => l.Contains("class ", StringComparison.Ordinal));
    }

    private sealed class Probe(ITestOutputHelper output) : IntegrationTestBase(output)
    {
        public bool Muted
        {
            get => MuteCompilePath;
            set => MuteCompilePath = value;
        }

        public void Print(string text) => Output.WriteLine(text);

        public bool Compile() => CompileAndExecute("def main() -> None:\n    print(1)\n", "ceiling_probe.spy").Success;
    }
}
