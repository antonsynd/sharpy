using System.Text.RegularExpressions;
using Sharpy.Compiler.Tests.Properties.Differential;
using Sharpy.TestInfrastructure.Oracles;
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

    private sealed class RecordingOutput : Xunit.Abstractions.ITestOutputHelper
    {
        public List<string> Lines { get; } = new();

        public void WriteLine(string message) => Lines.Add(message);

        public void WriteLine(string format, params object[] args) => Lines.Add(string.Format(format, args));
    }

    /// <summary>A whole-solution gate read Test Run Failed with 0 failed tests @ b3bac05af: the sweep's buffered
    /// compile output exhausted the testhost's heap when vstest serialized it. <c>RunSharpy</c> buffers nothing. The
    /// same compile through the base class does, which shows the recorder sees a write and the mute was lifted.</summary>
    [Fact]
    public void RunSharpy_BuffersNothingInTheTestResult()
    {
        const string Program = "def main() -> None:\n    print(1)\n";
        var recorded = new RecordingOutput();
        var harness = new NameResolutionDifferentialTests(recorded);

        var run = harness.RunSharpy(Program);

        Assert.Equal("1", Normalize(run.Stdout));
        Assert.Empty(recorded.Lines);

        harness.CompileAndExecute(Program, "name_resolution.spy");

        Assert.Contains(recorded.Lines, line => line.Contains("Generated C#", StringComparison.Ordinal));
    }

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

    /// <summary>D2's table cell by cell. Several arms have no generated cell that reaches them today, so a sweep alone leaves
    /// them free to change: `MustRefuse` × a throw read as agreement stays green over 6000 cells.</summary>
    [Theory]
    [InlineData(SharpyTag.MustRun, SharpyKind.Ran, "1\n2", VerdictClass.Match)]
    [InlineData(SharpyTag.MustRun, SharpyKind.Ran, "1\n3", VerdictClass.SilentWrong)]
    [InlineData(SharpyTag.MustRun, SharpyKind.Refused, "", VerdictClass.FalseRefusal)]
    [InlineData(SharpyTag.MustRun, SharpyKind.RuntimeThrow, "", VerdictClass.RuntimeThrow)]
    [InlineData(SharpyTag.MustRun, SharpyKind.Ice, "", VerdictClass.Ice)]
    [InlineData(SharpyTag.MustRefuse, SharpyKind.Refused, "", VerdictClass.Match)]
    [InlineData(SharpyTag.MustRefuse, SharpyKind.Ran, "1\n2", VerdictClass.SilentWrong)]
    [InlineData(SharpyTag.MustRefuse, SharpyKind.RuntimeThrow, "", VerdictClass.RuntimeThrow)]
    [InlineData(SharpyTag.MustRefuse, SharpyKind.Ice, "", VerdictClass.Ice)]
    [InlineData(SharpyTag.MustRefuseOrThrow, SharpyKind.Refused, "", VerdictClass.Match)]
    [InlineData(SharpyTag.MustRefuseOrThrow, SharpyKind.RuntimeThrow, "", VerdictClass.Match)]
    [InlineData(SharpyTag.MustRefuseOrThrow, SharpyKind.Ran, "1\n2", VerdictClass.SilentWrong)]
    [InlineData(SharpyTag.MustRefuseOrThrow, SharpyKind.Ice, "", VerdictClass.Ice)]
    [InlineData(SharpyTag.MayRefuse, SharpyKind.Refused, "", VerdictClass.RefuseOk)]
    [InlineData(SharpyTag.MayRefuse, SharpyKind.Ran, "1\n2", VerdictClass.Match)]
    [InlineData(SharpyTag.MayRefuse, SharpyKind.Ran, "1\n3", VerdictClass.SilentWrong)]
    [InlineData(SharpyTag.MayRefuse, SharpyKind.RuntimeThrow, "", VerdictClass.RuntimeThrow)]
    [InlineData(SharpyTag.Unruled, SharpyKind.Ran, "1\n3", VerdictClass.Unruled)]
    [InlineData(SharpyTag.Unruled, SharpyKind.Refused, "", VerdictClass.Unruled)]
    [InlineData(SharpyTag.Unruled, SharpyKind.RuntimeThrow, "", VerdictClass.RuntimeThrow)]
    [InlineData(SharpyTag.Unruled, SharpyKind.Ice, "", VerdictClass.Ice)]
    [InlineData(SharpyTag.Unruled, SharpyKind.TimedOut, "", VerdictClass.Harness)]
    [InlineData(SharpyTag.MustRefuse, SharpyKind.Harness, "", VerdictClass.Harness)]
    internal void Classifier_JudgeTable_ReadsEachArm(SharpyTag tag, SharpyKind kind, string stdout, VerdictClass expected)
        => Assert.Equal(expected, Judge(CellTagged(tag, "1\n2"), kind, stdout));

    /// <summary>D2's code sets, one literal per code: neither a Roslyn failure nor an infrastructure failure is ever a refusal.</summary>
    [Theory]
    [InlineData("SPY0510", SharpyKind.Ice)]
    [InlineData("SPY0904", SharpyKind.Ice)]
    [InlineData("SPY0906", SharpyKind.Ice)]
    [InlineData("SPY0907", SharpyKind.Ice)]
    [InlineData("SPY0908", SharpyKind.Ice)]
    [InlineData("SPY0909", SharpyKind.Ice)]
    [InlineData("SPY0902", SharpyKind.Harness)]
    [InlineData("SPY0903", SharpyKind.Harness)]
    [InlineData("SPY0910", SharpyKind.Harness)]
    [InlineData("SPY0911", SharpyKind.Harness)]
    [InlineData("SPY0913", SharpyKind.Harness)]
    internal void Classifier_EachInternalCode_IsNeverARefusal(string code, SharpyKind expected)
        => Assert.Equal(expected, ClassifySharpy(Literal("SPY0200", code)));

    /// <summary>D4's route: the row cites the issue whose fix turns the cell green.</summary>
    [Theory]
    [InlineData(Family.Read, BindingKind.ModuleTypeAlias, BindingKind.None, "read/ModuleTypeAlias/LambdaStored/Later/Bare/outer=None", VerdictClass.Ice, "CS0103", "#1925")]
    [InlineData(Family.Store, BindingKind.ModuleTypeAlias, BindingKind.ModuleTypeAlias, "store/Assign/outer=ModuleTypeAlias", VerdictClass.Ice, "CS0103", "#1925")]
    [InlineData(Family.TypeMember, BindingKind.None, BindingKind.None, "typemember/class/constructor/alias/bare/after", VerdictClass.Ice, "CS0103", "#1925")]
    [InlineData(Family.Store, BindingKind.ModuleVar, BindingKind.ModuleVar, "store/Assign/outer=ModuleVar", VerdictClass.Ice, "CS0103", "#2151")]
    [InlineData(Family.Read, BindingKind.StaticField, BindingKind.ModuleVar, "read/StaticField/field-init", VerdictClass.SilentWrong, null, "#2423")]
    [InlineData(Family.Read, BindingKind.StaticField, BindingKind.None, "read/StaticField/StaticInitializer", VerdictClass.SilentWrong, null, "#2422")]
    internal void Cite_RoutesToTheDrainingIssue(Family family, BindingKind target, BindingKind outer, string id, VerdictClass verdict, string? cs, string expected)
    {
        var cell = CellTagged(SharpyTag.MustRun, "1") with { Id = id, Family = family, Target = target, Outer = outer };
        var run = Literal() with { CsCode = cs };

        Assert.StartsWith(expected + " ", ProposedCite(cell, verdict, run), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SPY0134", "#2424")]
    [InlineData("SPY0600", "#2423")]
    internal void Cite_AWalrusRefusal_RoutesByItsCode(string code, string expected)
    {
        var cell = CellTagged(SharpyTag.MustRun, "1") with { Family = Family.Store, Form = StoreForm.Walrus };

        Assert.StartsWith(expected + " ", ProposedCite(cell, VerdictClass.FalseRefusal, Literal(code)), StringComparison.Ordinal);
    }

    /// <summary>A deviation names a ledger row, and the row exists (Acceptance 3). The named cells are the positive control.</summary>
    [Fact]
    public void Deviation_EveryCitedRow_IsInTheLedger()
    {
        var path = PythonInterpreter.FindRepoFile("docs", "deviations.yaml")
            ?? throw new InvalidOperationException("docs/deviations.yaml is missing");
        var ledger = File.ReadAllLines(path)
            .Select(l => Regex.Match(l, @"^\s*- id:\s*(\S+)"))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var cited = GeneratedCells().Where(c => c.Deviation != null).ToList();

        Assert.NotEmpty(cited);
        Assert.Empty(cited.Where(c => !ledger.Contains(c.Deviation!)).Select(c => $"{c.Id}: {c.Deviation}"));
    }

    /// <summary>M3's other half: the python control fails a must-run model that disagrees with python and names no deviation.</summary>
    [Fact]
    public void PythonControl_AModelThatDisagreesWithPython_NeedsADeviation()
    {
        var cell = CellTagged(SharpyTag.MustRun, "12") with { Python = PyOutcome.Ran("7") };

        Assert.Contains("no deviation", PythonControlFailure(cell, PyOutcome.Ran("7")));
        Assert.Null(PythonControlFailure(cell with { Deviation = "row" }, PyOutcome.Ran("7")));
        Assert.Null(PythonControlFailure(cell with { Model = "7" }, PyOutcome.Ran("7")));
    }

    /// <summary>The unruled tag's closed set reads the cell's axes, not its label: a listed shape on the wrong axes is red.</summary>
    [Theory]
    [InlineData("L28", Family.Read, BindingKind.StaticField, BindingKind.None, "read/StaticField/MethodBody/Later/Bare/outer=None")]
    [InlineData("before-binding:module", Family.Local, BindingKind.LocalVar, BindingKind.None, "local/LocalVar/above/outer=None/FunctionBody")]
    [InlineData("store-non-writable:ModuleDef:Assign", Family.Store, BindingKind.ModuleVar, BindingKind.ModuleVar, "store/Assign/outer=ModuleVar/FunctionBody/no-decl")]
    [InlineData("seed:A12", Family.Read, BindingKind.None, BindingKind.None, "seed/A13")]
    internal void ClosedSet_AListedShapeOnTheWrongAxes_IsRed(string shape, Family family, BindingKind target, BindingKind outer, string id)
    {
        var cell = CellTagged(SharpyTag.Unruled, null) with { Id = id, Family = family, Target = target, Outer = outer, Shape = shape };

        Assert.False(UnruledAxesHold(cell));
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
