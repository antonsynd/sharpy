using Xunit;

namespace Sharpy.Compiler.Tests.Properties.Differential;

/// <summary>A fixed program's facts about the sweep's own text, in a file of their own: PropertyOracleConformanceTests
/// holds this lane's verdicts to the seam, and what a verdict's text says is not one.</summary>
public partial class DifferentialExecutionTests
{
    /// <summary>The compile path is muted here, so a failed arm's text exists only in what the verdict carries. This drives
    /// a program that does not compile through the sweep's own arm and classifier: a red has to say why.</summary>
    [Fact]
    public void AnArmThatFailsToCompile_ItsVerdictNamesTheErrorsCode()
    {
        var arm = RunSharpyArm(new Program("t", "def main() -> None:\n    print(no_such_name)\n"));

        var classification = Classify(arm, new PyResult(Ok: true, Stdout: "1\n", Stderr: "", TimedOut: false, SyntaxError: false));

        Assert.False(arm.Ok);
        Assert.Equal(Verdict.PythonOkSharpyError, classification.Verdict);
        Assert.Contains("SPY0200", classification.Detail, StringComparison.Ordinal);
    }
}
