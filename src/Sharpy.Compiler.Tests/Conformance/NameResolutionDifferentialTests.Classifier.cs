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
    /// compile output exhausted the testhost's heap when vstest serialized it. The harness mutes the compile path, so
    /// <c>RunSharpy</c> buffers nothing. The same compile with the mute lifted does, which shows the recorder sees a
    /// write.</summary>
    [Fact]
    public void RunSharpy_BuffersNothingInTheTestResult()
    {
        const string Program = "def main() -> None:\n    print(1)\n";
        var recorded = new RecordingOutput();
        var harness = new NameResolutionDifferentialTests(recorded);

        var run = harness.RunSharpy(Program);

        Assert.Equal("1", Normalize(run.Stdout));
        Assert.Empty(recorded.Lines);

        harness.MuteCompilePath = false;
        harness.CompileAndExecute(Program, "name_resolution.spy");

        Assert.Contains(recorded.Lines, line => line.Contains("Generated C#", StringComparison.Ordinal));
    }

    /// <summary>D2.9's order of work: the imported outer's compile path is proved through THIS harness before a cell rides it. The
    /// verify round read SPY0300 from code and a 2026-10-22 measurement. The same source without the flag is the control: the
    /// reference is what makes it compile, and the Core-only cells keep Core only.</summary>
    [Fact]
    public void ImportedOuter_CompilesInProcess()
    {
        const string Program = "from string import digits\ndef main() -> None:\n    print(len(digits))\n";

        var withStdlib = RunSharpy(Program, needsStdlib: true);
        Assert.Equal(SharpyKind.Ran, ClassifySharpy(withStdlib));
        Assert.Equal("10", Normalize(withStdlib.Stdout));

        var coreOnly = RunSharpy(Program);
        Assert.Equal(SharpyKind.Refused, ClassifySharpy(coreOnly));
        Assert.Contains("SPY0300", coreOnly.ErrorCodes);
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

    /// <summary>D4.3: a run that times out once and runs at twice the timeout is that run's verdict.</summary>
    [Fact]
    public void Retry_ATimeoutThatRunsAtTwiceTheTimeout_IsRan()
    {
        var timeouts = new List<int>();

        var run = RetryingATimeout(ms =>
        {
            timeouts.Add(ms);
            return timeouts.Count == 1 ? Literal() with { TimedOut = true } : Literal();
        }, 12_000, out var retried);

        Assert.Equal(new[] { 12_000, 24_000 }, timeouts);
        Assert.True(retried);
        Assert.Equal(SharpyKind.Ran, ClassifySharpy(run));
    }

    /// <summary>D4.3: one retry and no more, so a real hang stays red.</summary>
    [Fact]
    public void Retry_ASecondTimeout_IsNotRetriedAgain_AndStaysHarness()
    {
        var timeouts = new List<int>();

        var run = RetryingATimeout(ms =>
        {
            timeouts.Add(ms);
            return Literal() with { TimedOut = true };
        }, 12_000, out var retried);

        Assert.Equal(new[] { 12_000, 24_000 }, timeouts);
        Assert.True(retried);
        Assert.Equal(SharpyKind.TimedOut, ClassifySharpy(run));
    }

    /// <summary>D4.3: only a timeout is retried. A refusal or a throw is a verdict the first time.</summary>
    [Fact]
    public void Retry_ARunThatFinished_IsNotRetried()
    {
        var calls = 0;

        var run = RetryingATimeout(_ =>
        {
            calls++;
            return Literal("SPY0200");
        }, 12_000, out var retried);

        Assert.Equal(1, calls);
        Assert.False(retried);
        Assert.Equal(SharpyKind.Refused, ClassifySharpy(run));
    }

    /// <summary>D4.3's positive control through the sweep's own path: a real program that never ends times out at both
    /// timeouts and is Harness, with or without the retry.</summary>
    [Fact]
    public void Retry_ARealHang_StaysHarness()
    {
        var run = RunSharpyRetryingATimeout("def main() -> None:\n    while True:\n        pass\n", 500, needsStdlib: false, out _);

        var kind = ClassifySharpy(run);
        Assert.Equal(SharpyKind.TimedOut, kind);
        Assert.Equal(VerdictClass.Harness, Judge(CellTagged(SharpyTag.MustRun, "1"), kind, run.Stdout));
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

    /// <summary>The classifier's order: an internal failure beats everything else that came back with it, and a summary code
    /// beside a clean run changes nothing. No generated cell reaches these today, so each is a literal.</summary>
    [Fact]
    public void Classifier_InternalFailures_WinOverWhateverElseCameBack()
    {
        Assert.Equal(SharpyKind.Ice, ClassifySharpy(Literal() with { ThrewInHarness = true }));
        Assert.Equal(SharpyKind.Ice, ClassifySharpy(Literal("SPY0902", "SPY0908")));
        Assert.Equal(SharpyKind.Harness, ClassifySharpy(Literal("SPY0200", "SPY0902")));
        var ranWithASummaryCode = Literal() with { ErrorCodes = new[] { "SPY0900" }, AllCodes = new[] { "SPY0900" } };
        Assert.Equal(SharpyKind.Ran, ClassifySharpy(ranWithASummaryCode));
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
    [InlineData("SPY0999", "UNROUTED(SPY0999)")]
    internal void Cite_AStoreRefusal_RoutesByItsCode(string code, string expected)
    {
        var cell = CellTagged(SharpyTag.MustRun, "1") with { Family = Family.Store, Form = StoreForm.Walrus };

        Assert.StartsWith(expected + " ", ProposedCite(cell, VerdictClass.FalseRefusal, Literal(code)), StringComparison.Ordinal);
    }

    /// <summary>An ICE routes by its C# code. There is no default arm: a code nobody has triaged is red until a route names its cure.</summary>
    [Theory]
    [InlineData("CS1955", BindingKind.ModuleVar, "#2423")]
    [InlineData("CS8917", BindingKind.ModuleVar, "#2423")]
    [InlineData("CS1729", BindingKind.ModuleClass, "#2423")]
    [InlineData("CS0029", BindingKind.LocalVar, "#2423")]
    [InlineData("CS0133", BindingKind.ModuleVar, "#2432")]
    [InlineData("CS1736", BindingKind.StaticProperty, "#2432")]
    [InlineData("CS0149", BindingKind.StaticProperty, "#2417")]
    [InlineData("CS0266", BindingKind.StaticProperty, "#2417")]
    [InlineData("CS0149", BindingKind.ModuleClass, "#2423")]
    [InlineData("CS0266", BindingKind.ModuleClass, "#2423")]
    [InlineData("CS1503", BindingKind.ModuleClass, "#2423")]
    [InlineData("CS1061", BindingKind.NestedType, "#2397")]
    [InlineData("CS0149", BindingKind.NestedType, "#2397")]
    [InlineData("CS0266", BindingKind.NestedType, "#2397")]
    [InlineData("CS9999", BindingKind.ModuleVar, "UNROUTED(CS9999)")]
    [InlineData("CS1061", BindingKind.ModuleVar, "UNROUTED(CS1061)")]
    internal void Cite_AnIce_RoutesByItsCode(string cs, BindingKind target, string expected)
    {
        var cell = CellTagged(SharpyTag.MustRun, "1") with { Target = target };

        Assert.StartsWith(expected + " ", ProposedCite(cell, VerdictClass.Ice, Literal("SPY0908") with { CsCode = cs }), StringComparison.Ordinal);
    }

    /// <summary>A union case reached through an alias refuses at both orders: the alias is the cause, not the order (#1925, not #2249).</summary>
    [Theory]
    [InlineData("typemember/union/constructor/alias/match/before", "#1925")]
    [InlineData("typemember/union/constructor/bare/match/after", "#2249")]
    internal void Cite_ATypeMemberRefusal_RoutesBySpelling(string id, string expected)
    {
        var cell = CellTagged(SharpyTag.MustRun, "1") with { Id = id, Family = Family.TypeMember };

        Assert.StartsWith(expected + " ", ProposedCite(cell, VerdictClass.FalseRefusal, Literal("SPY0203")), StringComparison.Ordinal);
    }

    /// <summary>The refusal has to be the rule's own. A foreign code is the program refused for an unrelated reason, which would
    /// otherwise read Match and hide the cell.</summary>
    [Theory]
    [InlineData("N1-methods", "SPY0200", true)]
    [InlineData("N1-methods", "SPY0202", false)]
    [InlineData("N6", "SPY0222", true)]
    [InlineData("N6", "SPY0200,SPY0202", false)]
    [InlineData("kind:SPY0290", "SPY0290,SPY0401", true)]
    [InlineData("no-such-rule", "SPY0200", false)]
    internal void RefusalReason_ReadsEveryErrorCode(string rule, string codes, bool admitted)
    {
        var cell = CellTagged(SharpyTag.MustRefuse, null) with { Rule = rule };

        Assert.Equal(admitted, RefusalReasonFailure(cell, Literal(codes.Split(','))) == null);
    }

    [Fact]
    public void RefusalReason_ASeedReadsItsOwnCodes_AndTheSummaryCodeIsIgnored()
    {
        var cell = CellTagged(SharpyTag.MustRefuse, null) with { Rule = "seed", SeedRefusalCodes = new HashSet<string> { "SPY0204" } };

        Assert.Null(RefusalReasonFailure(cell, Literal("SPY0204", "SPY0905")));
        Assert.NotNull(RefusalReasonFailure(cell, Literal("SPY0200")));
    }

    /// <summary>A seed's verdict in the table's normal form, as the outcome the python control compares.</summary>
    [Theory]
    [InlineData("10 / 10", PyKind.Ran, "10\n10")]
    [InlineData("2", PyKind.Ran, "2")]
    [InlineData("EXC NameError: name 'a' is not defined", PyKind.NameError, "a")]
    [InlineData("EXC UnboundLocalError: cannot access local variable 'q' where it is not associated with a value", PyKind.UnboundLocalError, "q")]
    [InlineData("EXC TypeError: 'int' object is not callable", PyKind.OtherError, "TypeError")]
    public void Seed_AVerdict_ParsesToAPythonOutcome(string verdict, PyKind kind, string value)
        => Assert.Equal(new PyOutcome(kind, value), ParseSeedVerdict(verdict));

    /// <summary>D2: a must-run program runs in python or fails only on declaration order. The positive control is the order-only NameError.</summary>
    [Fact]
    public void PythonControl_AMustRunThatPythonRefuses_IsAGeneratorBug()
    {
        var unbound = CellTagged(SharpyTag.MustRun, "5") with { Python = PyOutcome.Unbound("x") };
        Assert.Contains("must-run", PythonControlFailure(unbound, PyOutcome.Unbound("x")));

        var typeError = CellTagged(SharpyTag.MustRun, "5") with { Python = PyOutcome.Other("TypeError") };
        Assert.Contains("must-run", PythonControlFailure(typeError, PyOutcome.Other("TypeError")));

        var orderOnly = CellTagged(SharpyTag.MustRun, "5") with { Python = PyOutcome.Name("x") };
        Assert.Null(PythonControlFailure(orderOnly, PyOutcome.Name("x")));
    }

    /// <summary>A seed cell answers to python like a generated one, and to its deviation list. The table it reads is hand-kept, so
    /// a seed tagged must-run over python's different output would otherwise read Match.</summary>
    [Fact]
    public void PythonControl_ASeedCell_AnswersToItsVerdict()
    {
        var seed = CellTagged(SharpyTag.MustRun, "5") with { Id = "seed/B02", Family = Family.Seed, Python = ParseSeedVerdict("1"), SeedPythonVerdict = "1" };

        Assert.Contains("no deviation", PythonControlFailure(seed, seed.Python));
        Assert.Null(PythonControlFailure(seed with { Model = "1" }, seed.Python));

        var raised = seed with { Python = ParseSeedVerdict("EXC TypeError: x"), SeedPythonVerdict = "EXC TypeError: x" };
        Assert.Contains("must-run", PythonControlFailure(raised, raised.Python));
    }

    /// <summary>A may-refuse model answers to python like a must-run's. An unruled cell's prediction answers too: nothing about a
    /// question being open excuses predicting python wrongly, and a SyntaxError on its twin is a generator bug like any other.</summary>
    [Fact]
    public void PythonControl_MayRefuseAndUnruledCells_AnswerToTheirPrediction()
    {
        var mayRefuse = CellTagged(SharpyTag.MayRefuse, "12") with { Python = PyOutcome.Ran("7") };
        Assert.Contains("no deviation", PythonControlFailure(mayRefuse, PyOutcome.Ran("7")));

        var unruled = CellTagged(SharpyTag.Unruled, null) with { Python = PyOutcome.Ran("15") };
        Assert.Null(PythonControlFailure(unruled, PyOutcome.Ran("15")));
        Assert.Contains("predicted", PythonControlFailure(unruled, PyOutcome.Ran("999")));
        Assert.Contains("predicted", PythonControlFailure(unruled, new PyOutcome(PyKind.SyntaxError, "x")));
    }

    /// <summary>D2's deviation row: naming a ledger row is not enough, the cell has to be on that row's axes.</summary>
    [Theory]
    [InlineData("block-scoping-for-loop-variable", Family.Local, LocalTarget.For, BindingKind.None, "local/For/after-block/outer=None/FunctionBody", true)]
    [InlineData("block-scoping-for-loop-variable", Family.Local, LocalTarget.For, BindingKind.ModuleVar, "local/For/after-block/outer=ModuleVar/FunctionBody", false)]
    [InlineData("block-scoping-with", Family.Local, LocalTarget.For, BindingKind.None, "local/For/after-block/outer=None/FunctionBody", false)]
    [InlineData("loop-variable-fresh-per-iteration", Family.Store, LocalTarget.For, BindingKind.None, "store/Assign/outer=None/FunctionBody", false)]
    [InlineData("no-such-row", Family.Local, LocalTarget.For, BindingKind.None, "local/For/after-block/outer=None/FunctionBody", false)]
    internal void Deviation_ARowOffItsAxes_IsRed(string row, Family family, LocalTarget construct, BindingKind outer, string id, bool holds)
    {
        var cell = CellTagged(SharpyTag.MustRun, "1") with { Id = id, Family = family, Construct = construct, Outer = outer, Deviation = row };

        Assert.Equal(holds, DeviationAxesHold(cell));
    }

    [Fact]
    public void Deviation_ASeedOffItsList_IsRed()
    {
        var cell = CellTagged(SharpyTag.MustRun, "1") with { Id = "seed/B02", Family = Family.Seed, Deviation = "block-scoping-if" };

        Assert.False(DeviationAxesHold(cell));
        Assert.True(DeviationAxesHold(cell with { Id = "seed/D15" }));
    }

    /// <summary>The generator's output is pinned, compiler-independent: a dropped axis or a retagged arm moves a count here even when
    /// no allowlist row notices. A drain plan never touches this table, because a verdict moving changes no tag.</summary>
    [Fact]
    public void Census_TheGeneratedPool_IsPinned()
        => Assert.Empty(CensusFailures(GeneratedCells()));

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
    [InlineData("lambda-self", Family.Local, BindingKind.LocalVar, BindingKind.None, "local/LocalVar/self/outer=None/FunctionBody")]
    [InlineData("match-capture:none", Family.Local, BindingKind.LocalVar, BindingKind.None, "local/For/after-block/outer=None/FunctionBody")]
    [InlineData("rowless-let:if-then", Family.Local, BindingKind.LocalLet, BindingKind.None, "local/LocalLet/let-in/if-then/outer=None/FunctionBody")]
    [InlineData("s2-builtin", Family.Local, BindingKind.LocalVar, BindingKind.ModuleVar, "local/For/after-block/outer=ModuleVar/FunctionBody")]
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
