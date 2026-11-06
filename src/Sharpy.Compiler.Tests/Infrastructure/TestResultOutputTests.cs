using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// The per-result output ceiling and the compile-path mute (#2438, P77b D8). That xUnit really marks a test failed from
/// <see cref="IntegrationTestBase.Dispose"/> is the mutation in the commit that added this file: an un-muted sweep's own
/// test goes red. These facts pin what the wrapper counts, drops and fails on.
/// </summary>
public class TestResultOutputTests
{
    private const string Program = "def main() -> None:\n    print(1)\n";

    private static readonly int LineCost = Environment.NewLine.Length;

    private sealed class RecordingOutput : ITestOutputHelper
    {
        public List<string> Lines { get; } = new();

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }

    private sealed class Probe : IntegrationTestBase
    {
        public bool Released { get; private set; }

        public Probe(ITestOutputHelper output, bool mute) : base(output) => MuteCompilePath = mute;

        public bool Compile(string arm)
        {
            if (arm == "single")
                return CompileAndExecute(Program, "main.spy").Success;

            var dir = Directory.CreateTempSubdirectory("sharpy-result-output-").FullName;
            try
            {
                var entry = Path.Combine(dir, "main.spy");
                File.WriteAllText(entry, Program);
                return (arm == "project" ? CompileAndExecuteProject(dir, "main.spy") : CompileAndExecuteEntryFile(entry)).Success;
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        public void Write(string message) => Output.WriteLine(message);

        protected override void DisposeTestResources() => Released = true;
    }

    private static string Line(long charactersIncludingNewline) => new('x', (int)charactersIncludingNewline - LineCost);

    [Fact]
    public void AWriteAtTheCeiling_Passes_AndPrints()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded);

        output.WriteLine(Line(TestResultOutput.CharacterCeiling));

        Assert.Equal(TestResultOutput.CharacterCeiling, output.CharactersWritten);
        Assert.Single(recorded.Lines);
        output.FailIfOverCeiling();
    }

    [Fact]
    public void AWritePastTheCeiling_Fails_AndIsDropped()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded);
        output.WriteLine(Line(TestResultOutput.CharacterCeiling));

        output.WriteLine("x");
        output.WriteLine("x");

        Assert.Equal(TestResultOutput.CharacterCeiling + 2 * (1 + LineCost), output.CharactersWritten);
        // The line at the ceiling and one note: the writes past it reach neither the result nor the heap.
        Assert.Equal(2, recorded.Lines.Count);
        Assert.Contains("ceiling", recorded.Lines[1], StringComparison.Ordinal);
        var failure = Assert.ThrowsAny<XunitException>(output.FailIfOverCeiling);
        Assert.Contains("#2438", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFormatOverload_CountsWhatItFormats()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded);

        output.WriteLine("{0}-{1}", "a", "b");

        Assert.Equal("a-b", Assert.Single(recorded.Lines));
        Assert.Equal(3 + LineCost, output.CharactersWritten);
    }

    [Fact]
    public void MutedCompilePathWrites_NeitherCount_NorPrint()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded) { MuteCompilePath = true };

        output.CompilePath.WriteLine(Line(TestResultOutput.CharacterCeiling * 2));
        output.CompilePath.WriteLine("{0}", "formatted");

        Assert.Empty(recorded.Lines);
        Assert.Equal(0, output.CharactersWritten);
        output.FailIfOverCeiling();

        // The same two writes un-muted, on the same output, which shows the mute is what kept them out.
        output.MuteCompilePath = false;
        output.CompilePath.WriteLine("line");
        output.CompilePath.WriteLine("{0}", "formatted");
        Assert.Equal(new[] { "line", "formatted" }, recorded.Lines);
        Assert.Equal(4 + 9 + 2 * LineCost, output.CharactersWritten);
    }

    [Fact]
    public void TheMute_LeavesATestsOwnWritesAlone()
    {
        var recorded = new RecordingOutput();
        var output = new TestResultOutput(recorded) { MuteCompilePath = true };

        output.WriteLine("own");

        Assert.Equal("own", Assert.Single(recorded.Lines));
        Assert.Equal(3 + LineCost, output.CharactersWritten);
    }

    [Fact]
    public void TheBaseClass_FailsWhenTheTestEnds_AfterReleasingTheSubclassResources()
    {
        var overCeiling = new Probe(new RecordingOutput(), mute: false);
        overCeiling.Write(Line(TestResultOutput.CharacterCeiling + 1));

        Assert.ThrowsAny<XunitException>(overCeiling.Dispose);
        Assert.True(overCeiling.Released);

        var underCeiling = new Probe(new RecordingOutput(), mute: false);
        underCeiling.Write(Line(TestResultOutput.CharacterCeiling));
        underCeiling.Dispose();
        Assert.True(underCeiling.Released);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("project")]
    [InlineData("entry-file")]
    public void TheBaseClass_RoutesEachCompilesOutputThroughTheMute(string arm)
    {
        var muted = new RecordingOutput();
        Assert.True(new Probe(muted, mute: true).Compile(arm));
        Assert.Empty(muted.Lines);

        var unmuted = new RecordingOutput();
        Assert.True(new Probe(unmuted, mute: false).Compile(arm));
        Assert.Contains(unmuted.Lines, line => line.Contains("Generated C#", StringComparison.Ordinal));
    }
}
