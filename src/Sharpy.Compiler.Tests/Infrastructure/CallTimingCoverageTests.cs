using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// #2259: the per-call timing instrument of <c>IntegrationTestBase</c> (#2180) is a standing
/// check, not a printed one. Its own validity check is the coverage row — Σ per-call component
/// times against Σ per-call totals, per execute arm — which the <c>SHARPY_TEST_TIMING</c> report
/// prints as <c>within_5pct={yes,no}</c> and nothing failed on. This Fact runs every arm
/// in-process under a <see cref="IntegrationTestBase.CaptureCallTiming"/> capture (no environment
/// variable, no child test run) and asserts each arm's coverage is within 5% of 1: a component
/// whose recording is dropped (P41 P1.2 dropped the process component by hand: ratios fell to
/// 0.729 / 0.936 / 0.747) leaves its time unattributed and turns this red.
///
/// <para>Cost: one warm-up call per arm outside the capture (first-call JIT of the arms' own code
/// runs partly outside every component and is not what the instrument measures), then
/// <see cref="CallsPerArm"/> captured calls per arm — about a dozen tiny compile-and-run calls,
/// a few seconds. The unattributed remainder is microseconds of bookkeeping between components
/// against tens of milliseconds of process time per call, so the margin to 0.95 is wide
/// (measured over the FileBased corpus: 0.9988 single / 0.9981 project).</para>
/// </summary>
public class CallTimingCoverageTests : IntegrationTestBase
{
    private const int CallsPerArm = 3;

    private const string Program = "def main() -> None:\n    print(\"timed\")\n";

    public CallTimingCoverageTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void EveryExecuteArm_AttributesWithinFivePercentOfItsWallClock_ToItsComponents()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"sharpy_timing_{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);
        try
        {
            var entryFile = Path.Combine(projectDir, "main.spy");
            File.WriteAllText(entryFile, Program);

            RunEveryArm(projectDir, entryFile);

            IReadOnlyList<TimingCoverage> coverage;
            using (var capture = CaptureCallTiming())
            {
                for (var i = 0; i < CallsPerArm; i++)
                    RunEveryArm(projectDir, entryFile);
                coverage = capture.Coverage();
            }

            foreach (var arm in coverage)
                Output.WriteLine($"arm={arm.Arm} calls={arm.Calls} sum_components={arm.ComponentSumMs:F1} sum_total={arm.TotalSumMs:F1} ratio={arm.Ratio:F4}");

            // Control: the capture saw exactly the captured calls of all three arms — an arm that
            // stopped recording would otherwise pass as "no arm out of range".
            Assert.Equal(new[] { "entry-file", "project", "single" }, coverage.Select(c => c.Arm));
            Assert.All(coverage, c => Assert.Equal(CallsPerArm, c.Calls));
            Assert.All(coverage, c => Assert.True(c.WithinFivePercent,
                $"arm {c.Arm}: components cover {c.ComponentSumMs:F1} of {c.TotalSumMs:F1} ms (ratio {c.Ratio:F4}); " +
                "a timed region of the execute path is no longer recorded (or is recorded twice)"));
        }
        finally
        {
            try
            {
                Directory.Delete(projectDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>The capture's boundary: a call made before it opened is not in it.</summary>
    [Fact]
    public void ACapture_SeesOnlyTheCallsMadeWhileItIsOpen()
    {
        AssertRan(CompileAndExecute(Program), "single");

        using var capture = CaptureCallTiming();
        Assert.Empty(capture.Coverage());
        Assert.True(CompileAndExecute(Program).Success);
        Assert.Equal(1, Assert.Single(capture.Coverage()).Calls);
    }

    private void RunEveryArm(string projectDir, string entryFile)
    {
        AssertRan(CompileAndExecute(Program), "single");
        AssertRan(CompileAndExecuteProject(projectDir, "main.spy"), "project");
        AssertRan(CompileAndExecuteEntryFile(entryFile), "entry-file");
    }

    private static void AssertRan(ExecutionResult result, string arm)
    {
        Assert.True(result.Success, $"{arm}: {string.Join("; ", result.CompilationErrors)} {result.StandardError}");
        Assert.Equal("timed", result.StandardOutput.Trim());
    }
}
