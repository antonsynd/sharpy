using Sharpy.Compiler.Tests.Properties.Differential;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The verdict classifier's positive controls (D2, D8 M9): each outcome class is reached by a real program or a literal
/// of the recorded shape, because a class the sweep can leave at a count of 0 is otherwise an absence assertion.
/// </summary>
public partial class NameResolutionDifferentialTests
{
    private static SharpyRun Literal(params string[] codes) => new(
        Success: codes.Length == 0,
        ExitCode: codes.Length == 0 ? 0 : null,
        ThrewInHarness: false,
        TimedOut: false,
        Stdout: "",
        Stderr: "",
        ErrorCodes: codes,
        AllCodes: codes,
        CsCode: null);

    /// <summary>M9: a program that throws at run time is a child process's non-zero exit with no diagnostic, read
    /// positively from <c>ExitCode</c>. It is a third outcome, never "refused" and never "ran".</summary>
    [Fact]
    public void Classifier_AThrowingProgram_IsRuntimeThrow()
    {
        var run = RunSharpy("def main() -> None:\n    print(1)\n    raise ValueError(\"x\")\n");

        Assert.Equal(SharpyKind.RuntimeThrow, ClassifySharpy(run));
        Assert.NotEqual(0, run.ExitCode);
        Assert.Empty(run.ErrorCodes);
        Assert.Equal(VerdictClass.RuntimeThrow, Judge(CellTagged(SharpyTag.MustRun, "1"), SharpyKind.RuntimeThrow, run.Stdout));
    }

    /// <summary>The positive control of "never SPY0908": a C#-compile failure reaches the classifier through the mapped
    /// diagnostics as Ice, and the same program is never read as a refusal.</summary>
    [Fact]
    public void Classifier_AGeneratedCSharpFailure_IsIce()
    {
        var run = RunSharpy("k: int = 3\ng: () -> int = lambda: k * 2\ndef main() -> None:\n    print(g())\n");

        Assert.Contains("SPY0908", run.AllCodes);
        Assert.Equal(SharpyKind.Ice, ClassifySharpy(run));
    }

    /// <summary>The Harness arm's positive control: a timed-out program is not a verdict on either arm.</summary>
    [Fact]
    public void Classifier_ATimedOutProgram_IsHarness()
    {
        var run = RunSharpy("def main() -> None:\n    while True:\n        pass\n", executionTimeoutMs: 500);

        Assert.Equal(SharpyKind.TimedOut, ClassifySharpy(run));
        Assert.Equal(VerdictClass.Harness, Judge(CellTagged(SharpyTag.MustRun, "1"), SharpyKind.TimedOut, ""));
    }

    [Fact]
    public void Classifier_CodeSets_ClassifyByCode()
    {
        Assert.Equal(SharpyKind.Ice, ClassifySharpy(Literal("SPY0200", "SPY0909")));
        Assert.Equal(SharpyKind.Harness, ClassifySharpy(Literal("SPY0910")));
        Assert.Equal(SharpyKind.Refused, ClassifySharpy(Literal("SPY0200", "SPY0905")));
        Assert.Equal(SharpyKind.Harness, ClassifySharpy(Literal("SPY0905")));
        Assert.Equal(SharpyKind.Ran, ClassifySharpy(Literal()));
        Assert.Equal(SharpyKind.Harness, ClassifySharpy(Literal() with { Success = false, ExitCode = null }));
    }

    /// <summary>The python parse is prefix-anchored on the LAST stderr line: python 3.12 appends a suggestion to it.</summary>
    [Theory]
    [InlineData("NameError: name 'f' is not defined. Did you mean: 'self.f'?", PyKind.NameError, "f")]
    [InlineData("NameError: name 'Later' is not defined. Did you mean: 'iter'?", PyKind.NameError, "Later")]
    [InlineData("NameError: name 'ab' is not defined", PyKind.NameError, "ab")]
    [InlineData("NameError: cannot access free variable 'n' where it is not associated with a value in enclosing scope", PyKind.NameError, "n")]
    [InlineData("UnboundLocalError: cannot access local variable 'q' where it is not associated with a value", PyKind.UnboundLocalError, "q")]
    [InlineData("TypeError: 'int' object is not callable", PyKind.OtherError, "TypeError")]
    [InlineData("ValueError: x", PyKind.OtherError, "ValueError")]
    public void Classifier_PythonLastLine_ParsesKindAndName(string lastLine, PyKind kind, string value)
    {
        var observed = ClassifyPython(new PyResult(false, "", $"Traceback (most recent call last):\n  File \"<string>\", line 1\n{lastLine}\n", false, false));

        Assert.Equal(new PyOutcome(kind, value), observed);
    }

    private static NameCell CellTagged(SharpyTag tag, string? model) => new()
    {
        Id = "unit",
        Family = Family.Read,
        Source = "",
        Tag = tag,
        Model = model,
        Python = PyOutcome.Ran(""),
        Rule = "unit",
    };
}
