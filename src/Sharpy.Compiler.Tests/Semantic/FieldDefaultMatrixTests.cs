using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Per-instance field-default VALUES (#1684, R-A): a dataclass or struct field whose default is
/// the mutable-collection family (a list/dict/set literal, a <c>list()</c>/<c>dict()</c>/
/// <c>set()</c> call, or a list/dict/set comprehension) is evaluated PER INSTANCE by the
/// synthesized constructor — never shared as a C# default-parameter value.
///
/// <para><b>Contract.</b> <c>ConstantDefaultClassifier</c>/<c>AdmissionTable.PerInstanceFieldDefault</c>
/// (Semantic.Validation) admit the family at the dataclass/struct host; <c>CodeGenInfo.RequiresPerInstanceDefault</c>
/// (a symbol-keyed, Rule-2(a) materialized fact, computed once in <c>CodeGenInfoComputer</c> by
/// calling the SAME classifier) is what CodeGen reads. When true, the synthesized constructor emits
/// a sentinel parameter (<c>T? name = null</c>) and a body statement
/// <c>if (name is null) { &lt;hoisted&gt;; this.Field = &lt;default&gt;; } else { this.Field = name; }</c>,
/// whose absent arm owns the hoist sink #1685 gives every other initializer host, so a comprehension
/// default's loop-building statements land INSIDE the branch that evaluates them. The member's own
/// initializer is dropped on that path — see the evaluation-count matrix below. A NULLABLE-typed
/// field (<c>T | None</c>) keeps the SPY0400 refusal — the <c>null</c> sentinel this lowering tests
/// would collide with a caller
/// legitimately passing <c>None</c>. This matrix asserts every python3-matching VALUE (not merely
/// "compiles") — <see cref="!:IntegrationTestBase.CompileAndExecute" /> executes the generated
/// program for every cell.</para>
///
/// <para><b>Axis 1.</b> Literal kind (the family <c>ConstantDefaultClassifier.EmittableConstantKind.Collection</c>
/// / <c>.Comprehension</c> names — 9 kinds, anchored to a literal count) × Host {Dataclass, Struct}:
/// 18 cells, every one two-instance-INDEPENDENT (the discriminating assertion — a shared C#
/// default-parameter value would make the second instance see the first's mutation). The
/// <c>ListComprehension</c> × Dataclass and × Struct cells are the plan's named d06/c10 cells: the
/// comprehension default whose hoisted temp must land under the constructor's hoist sink.</para>
///
/// <para><b>Axis 2 (named scenarios, plan line 180).</b> Explicit-argument-wins; a keyword argument
/// skipping an earlier defaulted field (<c>Bag(xs=[4])</c>); frozen dataclass per-instance
/// independence; an inherited dataclass chain; <c>__post_init__</c> seeing the default (R-AV
/// ordering: the <c>??</c> assignment runs BEFORE <c>PostInit()</c>); the nullable-typed positive
/// control (stays SPY0400); tuple/<c>Some(...)</c>/user-call controls (stay SPY0401, R-R — already
/// the megatest's job, referenced not duplicated here).</para>
///
/// <para><b>R-AV frozen × <c>__post_init__</c>-assignment gap (landed).</b> R-AV names a SECOND cell
/// this ruling adds: <c>@dataclass(frozen=True)</c> + a field ASSIGNMENT inside <c>__post_init__</c>
/// must become a named refusal (Python: <c>FrozenInstanceError</c>), with the non-frozen twin
/// (assigns, prints the value) as the positive control. Measured @ HEAD (b8bc7825a): the frozen cell
/// reproduced the ORIGINAL ICE (SPY0908/CS8852 — <c>NonFrozenPostInit_Assigns_PrintsTheAssignedValue</c>
/// below is that non-frozen twin, executing and printing <c>8</c>, matching python3). No seam walked a
/// frozen dataclass's <c>__post_init__</c> body for a <c>self.field = ...</c> store — orthogonal to
/// #1684/#1685's lowering (it reproduced for a plain SCALAR field, not just a mutable default) — filed
/// as <see href="https://github.com/antonsynd/sharpy/issues/1902">#1902</see> and fixed by
/// <c>FrozenDataclassValidator</c> (SPY0706): any <c>self.&lt;field&gt; = ...</c> assignment to a
/// frozen-dataclass field outside the declaring class's own <c>__init__</c> is refused by name.
/// <see cref="FrozenPostInit_AssignsAField_ShouldBeANamedRefusal_NotAnICE" /> asserts the refusal.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class FieldDefaultMatrixTests : IntegrationTestBase
{
    public FieldDefaultMatrixTests(ITestOutputHelper output) : base(output) { }

    // ══ Axis 1: literal kind × host, two-instance independence ═══════════════════════════════

    private const int LiteralKindCount = 9;
    private const int HostCount = 2;

    private sealed record LiteralKind(
        string Name,
        string SharpyType,
        string DefaultExpr,
        // The program mutates instance `a` and prints both `a.<field>`/`b.<field>` (or, for a
        // set, `sorted(...)` of each — Sharpy's HashSet-backed set has no guaranteed print order).
        string MutateAndPrint,
        string ExpectedOutput);

    private static readonly LiteralKind[] LiteralKinds =
    {
        new("ListLiteral", "list[int]", "[1]",
            "a.xs.append(2)\n    print(a.xs)\n    print(b.xs)\n", "[1, 2]\n[1]\n"),
        new("DictLiteral", "dict[str, int]", "{\"a\": 1}",
            "a.xs[\"b\"] = 2\n    print(a.xs)\n    print(b.xs)\n", "{'a': 1, 'b': 2}\n{'a': 1}\n"),
        new("SetLiteral", "set[int]", "{1, 2}",
            "a.xs.add(3)\n    print(sorted(a.xs))\n    print(sorted(b.xs))\n", "[1, 2, 3]\n[1, 2]\n"),
        new("ListCall", "list[int]", "list()",
            "a.xs.append(1)\n    print(a.xs)\n    print(b.xs)\n", "[1]\n[]\n"),
        new("DictCall", "dict[str, int]", "dict()",
            "a.xs[\"k\"] = 1\n    print(a.xs)\n    print(b.xs)\n", "{'k': 1}\n{}\n"),
        new("SetCall", "set[int]", "set()",
            "a.xs.add(1)\n    print(sorted(a.xs))\n    print(sorted(b.xs))\n", "[1]\n[]\n"),
        // The plan's named d06 (Dataclass) / c10 (Struct) cells: the hoisted comprehension temp
        // must land under the constructor's hoist sink (Design Decision 2 note ii).
        new("ListComprehension", "list[int]", "[i * i for i in range(3)]",
            "a.xs.append(99)\n    print(a.xs)\n    print(b.xs)\n", "[0, 1, 4, 99]\n[0, 1, 4]\n"),
        new("DictComprehension", "dict[int, int]", "{i: i * i for i in range(3)}",
            "a.xs[9] = 9\n    print(a.xs)\n    print(b.xs)\n", "{0: 0, 1: 1, 2: 4, 9: 9}\n{0: 0, 1: 1, 2: 4}\n"),
        new("SetComprehension", "set[int]", "{i * i for i in range(3)}",
            "a.xs.add(9)\n    print(sorted(a.xs))\n    print(sorted(b.xs))\n", "[0, 1, 4, 9]\n[0, 1, 4]\n"),
    };

    private sealed record Host(string Name, Func<LiteralKind, string> Compose);

    private static readonly Host[] Hosts =
    {
        new("Dataclass", k =>
            $"@dataclass\nclass Bag:\n    xs: {k.SharpyType} = {k.DefaultExpr}\n\n"
            + $"def main() -> None:\n    a = Bag()\n    b = Bag()\n    {k.MutateAndPrint}"),
        new("Struct", k =>
            $"struct Bag:\n    xs: {k.SharpyType} = {k.DefaultExpr}\n\n"
            + $"def main() -> None:\n    a = Bag()\n    b = Bag()\n    {k.MutateAndPrint}"),
    };

    private static LiteralKind LK(string name) => LiteralKinds.Single(k => k.Name == name);
    private static Host H(string name) => Hosts.Single(h => h.Name == name);

    public static IEnumerable<object[]> LiteralKindHostCells =>
        from h in Hosts from k in LiteralKinds select new object[] { h.Name, k.Name };

    [Theory]
    [MemberData(nameof(LiteralKindHostCells))]
    public void LiteralKindHostCell_TwoInstancesAreIndependent(string host, string kind)
    {
        var h = H(host);
        var k = LK(kind);
        var source = h.Compose(k);

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError,
            $"[{host} × {kind}] a per-instance mutable-collection default must never produce "
            + $"SPY0908/SPY0909. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{host} × {kind}] must compile and run. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(k.ExpectedOutput,
            $"[{host} × {kind}] the two instances must be independent — a shared C# default-parameter "
            + $"value would make b see a's mutation\n{source}");
    }

    [Fact]
    public void LiteralKindHostMatrix_IsTotalOverItsAxes()
    {
        LiteralKinds.Length.Should().Be(LiteralKindCount);
        Hosts.Length.Should().Be(HostCount);
        LiteralKinds.Select(k => k.Name).Should().OnlyHaveUniqueItems();
        Hosts.Select(h => h.Name).Should().OnlyHaveUniqueItems();
        LiteralKindHostCells.Count().Should().Be(LiteralKindCount * HostCount,
            "every literal kind runs at every host — there is no refused cell in this family "
            + "(R-A admits the whole mutable-collection family at both hosts)");
    }

    // ══ Axis 2: named value scenarios (plan line 180) ═════════════════════════════════════════

    [Fact]
    public void ExplicitArgument_WinsOverTheDefault()
    {
        var result = CompileAndExecute(
            "@dataclass\nclass Bag:\n    xs: list[int] = [1]\n\n"
            + "def main() -> None:\n    print(Bag([5]).xs)\n");

        result.Success.Should().BeTrue($"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.Should().Be("[5]\n");
    }

    [Fact]
    public void KeywordArgument_SkipsAnEarlierDefaultedField()
    {
        var result = CompileAndExecute(
            "@dataclass\nclass Bag:\n    n: int = 1\n    xs: list[int] = [4]\n\n"
            + "def main() -> None:\n    b = Bag(xs=[9])\n    print(b.n)\n    print(b.xs)\n");

        result.Success.Should().BeTrue($"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.Should().Be("1\n[9]\n");
    }

    [Fact]
    public void Frozen_PerInstanceDefault_TwoInstancesAreIndependent()
    {
        var result = CompileAndExecute(
            "@dataclass(frozen=True)\nclass Bag:\n    xs: list[int] = [1]\n\n"
            + "def main() -> None:\n    a = Bag()\n    b = Bag()\n"
            + "    a.xs.append(2)\n    print(a.xs)\n    print(b.xs)\n");

        result.Success.Should().BeTrue($"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.Should().Be("[1, 2]\n[1]\n",
            "frozen only forbids REBINDING the field, not mutating the list it names — matches python3");
    }

    [Fact]
    public void InheritedDataclassChain_EachFieldIsIndependentPerInstance()
    {
        var result = CompileAndExecute(
            "@dataclass\nclass Base:\n    xs: list[int] = [1]\n\n"
            + "@dataclass\nclass Derived(Base):\n    ys: list[int] = [2]\n\n"
            + "def main() -> None:\n    a = Derived()\n    b = Derived()\n"
            + "    a.xs.append(9)\n    a.ys.append(8)\n"
            + "    print(a.xs)\n    print(b.xs)\n    print(a.ys)\n    print(b.ys)\n");

        result.Success.Should().BeTrue($"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.Should().Be("[1, 9]\n[1]\n[2, 8]\n[2]\n");
    }

    /// <summary>
    /// R-AV ordering: the <c>??</c> per-instance assignment runs BEFORE <c>PostInit()</c> — python3's
    /// <c>default_factory</c> runs before <c>__post_init__</c>, and <c>__post_init__</c> reading the
    /// already-assigned default (length 2, not 0/null) is what proves the ordering.
    /// </summary>
    [Fact]
    public void PostInit_SeesTheAlreadyAssignedDefault()
    {
        var result = CompileAndExecute(
            "@dataclass\nclass Bag:\n    xs: list[int] = [1, 2]\n\n"
            + "    def __post_init__(self) -> None:\n        print(len(self.xs))\n\n"
            + "def main() -> None:\n    Bag()\n");

        result.Success.Should().BeTrue($"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.Should().Be("2\n");
    }

    /// <summary>
    /// Positive control (R-A): a NULLABLE-typed field with a mutable-collection default keeps the
    /// SPY0400 refusal — the <c>null</c> sentinel this lowering needs would
    /// collide with a caller legitimately passing <c>None</c> for that field.
    /// </summary>
    [Fact]
    public void NullableTypedField_MutableDefault_StaysRefused()
    {
        var source = "@dataclass\nclass Bag:\n    xs: list[int] | None = [1]\n\n"
            + "def main() -> None:\n    print(Bag().xs)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[nullable field] must stay refused\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Validation.MutableDefault,
            $"[nullable field] must report SPY0400, unchanged by R-A. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError,
            $"[nullable field] the refusal must be ours, never SPY0908/SPY0909\n{source}");
    }

    /// <summary>
    /// The non-frozen twin the R-AV ruling names as the positive control for the frozen ×
    /// <c>__post_init__</c>-assignment cell below: an ordinary field assignment inside
    /// <c>__post_init__</c> runs and prints the assigned value (python3-matching — no
    /// <c>FrozenInstanceError</c> without <c>frozen=True</c>).
    /// </summary>
    [Fact]
    public void NonFrozenPostInit_Assigns_PrintsTheAssignedValue()
    {
        var result = CompileAndExecute(
            "@dataclass\nclass NonFrozen:\n    n: int = 1\n\n"
            + "    def __post_init__(self) -> None:\n        self.n = 8\n\n"
            + "def main() -> None:\n    print(NonFrozen().n)\n");

        result.Success.Should().BeTrue($"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.Should().Be("8\n");
    }

    /// <summary>
    /// R-AV's second cell: <c>@dataclass(frozen=True)</c> + a field assignment inside
    /// <c>__post_init__</c> must be a named refusal (python3: <c>FrozenInstanceError</c>) —
    /// <c>FrozenDataclassValidator</c> (SPY0706, #1902). Never the SPY0908/CS8852 ICE this cell
    /// reproduced at HEAD before the fix (see the class remarks).
    /// </summary>
    [Fact]
    public void FrozenPostInit_AssignsAField_ShouldBeANamedRefusal_NotAnICE()
    {
        var source = "@dataclass(frozen=True)\nclass Frozen:\n    n: int = 1\n\n"
            + "    def __post_init__(self) -> None:\n        self.n = 8\n\n"
            + "def main() -> None:\n    print(Frozen().n)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[frozen __post_init__ assign] must be refused\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError,
            $"[frozen __post_init__ assign] must be OUR refusal, never SPY0908 (the ICE #1902 tracked)\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.ValidationOverflow.FrozenFieldReassignment,
            $"[frozen __post_init__ assign] must report SPY0706. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
    }

    // ══ Axis 3: evaluation COUNT × host × default kind × construction path (#1901) ════════════

    /// <summary>
    /// The marker every counted default calls once per element it evaluates. Printing is what makes
    /// the NUMBER of evaluations observable — the axes above assert VALUES only, which is why the
    /// whole family could evaluate its default two or four times per construction and stay green.
    /// </summary>
    private const string MarkerFunction =
        "def side(i: int) -> int:\n    print(\"side\")\n    return i\n\n\n";

    private const string MarkerLine = "side\n";

    /// <summary>
    /// A default whose element expression calls <c>side</c>. <c>ConstantDefaultClassifier</c> admits
    /// this family by AST SHAPE with no purity check, so an impure element is squarely inside R-A's
    /// admitted set — it is not an exotic case the lowering may ignore.
    /// </summary>
    /// <param name="DefaultMarkerCount">
    /// How many times <c>side</c> runs when the default is evaluated ONCE — the number python3
    /// prints for the equivalent <c>field(default_factory=lambda: …)</c>.
    /// </param>
    private sealed record CountedDefault(
        string Name,
        string SharpyType,
        string DefaultExpr,
        string ExplicitArg,
        string ExplicitRepr,
        int DefaultMarkerCount,
        string DefaultRepr,
        int DefaultLength);

    private static readonly CountedDefault[] CountedDefaults =
    {
        new("ListDisplayWithCalls", "list[int]", "[side(1), side(2)]", "[9]", "[9]", 2, "[1, 2]", 2),
        new("ListComprehension", "list[int]", "[side(i) for i in range(2)]", "[9]", "[9]", 2, "[0, 1]", 2),
        new("DictDisplayWithCalls", "dict[str, int]", "{\"a\": side(1)}", "{\"z\": 9}", "{'z': 9}", 1, "{'a': 1}", 1),
        // A walrus hoists exactly like a comprehension, so its hoisted statements must also land
        // inside the absent-argument branch rather than above the whole assignment.
        new("WalrusInListDisplay", "list[int]", "[(w := side(1)), w + 1]", "[9]", "[9]", 1, "[1, 2]", 2),
    };

    private sealed record CountedHost(string Name, string Header, bool SupportsPostInit);

    private static readonly CountedHost[] CountedHosts =
    {
        new("Dataclass", "@dataclass\nclass Bag:\n", true),
        new("FrozenDataclass", "@dataclass(frozen=True)\nclass Bag:\n", true),
        // A struct is not a dataclass: it has no __post_init__ hook, so that path has no cell here.
        new("Struct", "struct Bag:\n", false),
    };

    private sealed record CountedPath(string Name, bool ArgumentSupplied, bool Keyword, bool PostInit);

    private static readonly CountedPath[] CountedPaths =
    {
        new("PositionalExplicit", true, false, false),
        new("KeywordExplicit", true, true, false),
        new("Default", false, false, false),
        new("DefaultWithPostInit", false, false, true),
    };

    private const int CountedDefaultCount = 4;
    private const int CountedHostCount = 3;
    private const int CountedPathCount = 4;

    private static string ComposeCounted(CountedHost h, CountedDefault k, CountedPath p)
    {
        var classBody = $"    xs: {k.SharpyType} = {k.DefaultExpr}\n";
        if (p.PostInit)
            classBody += "\n    def __post_init__(self) -> None:\n        print(len(self.xs))\n";

        var construction = p.ArgumentSupplied
            ? (p.Keyword ? $"Bag(xs={k.ExplicitArg})" : $"Bag({k.ExplicitArg})")
            : "Bag()";

        return MarkerFunction + h.Header + classBody
            + $"\n\ndef main() -> None:\n    b = {construction}\n    print(b.xs)\n";
    }

    /// <summary>
    /// python3's answer for the equivalent <c>field(default_factory=lambda: …)</c>, verified by
    /// running it (CLAUDE.md Rule 6): the factory runs exactly once, and only when the caller
    /// supplies no argument.
    /// </summary>
    private static string ExpectedCounted(CountedDefault k, CountedPath p)
    {
        if (p.ArgumentSupplied)
            return k.ExplicitRepr + "\n";

        var markers = string.Concat(Enumerable.Repeat(MarkerLine, k.DefaultMarkerCount));
        return p.PostInit
            ? markers + k.DefaultLength + "\n" + k.DefaultRepr + "\n"
            : markers + k.DefaultRepr + "\n";
    }

    public static IEnumerable<object[]> EvaluationCountCells =>
        from h in CountedHosts
        from k in CountedDefaults
        from p in CountedPaths
        where h.SupportsPostInit || !p.PostInit
        select new object[] { h.Name, k.Name, p.Name };

    /// <summary>
    /// The class contract R-A commits to, stated as a count (#1901): the default expression is
    /// evaluated EXACTLY ONCE, and only on a construction path where the argument is absent — zero
    /// times when the caller supplies one, positionally or by keyword.
    ///
    /// <para>The explicit-argument cells are absence assertions ("no <c>side</c> line"), so they need
    /// a positive control or they pass on any program that prints nothing: the <c>Default</c> and
    /// <c>DefaultWithPostInit</c> cells of the SAME host × kind are that control — they assert the
    /// marker DOES appear, exactly <c>DefaultMarkerCount</c> times, in the same composed program
    /// shape. A lowering that never evaluated the default would redden them.</para>
    ///
    /// <para>Measured @ 322fbd20e, before the fix: <c>Dataclass × ListComprehension ×
    /// PositionalExplicit</c> printed four marker lines instead of zero (an eager property
    /// initializer, plus the comprehension's hoisted statements flushed FLAT above the
    /// <c>??</c>), and <c>× Default</c> printed four instead of two.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(EvaluationCountCells))]
    public void EvaluationCountCell_EvaluatesTheDefaultOnceAndOnlyWhenTheArgumentIsAbsent(
        string host, string kind, string path)
    {
        var h = CountedHosts.Single(x => x.Name == host);
        var k = CountedDefaults.Single(x => x.Name == kind);
        var p = CountedPaths.Single(x => x.Name == path);
        var source = ComposeCounted(h, k, p);
        var expected = ExpectedCounted(k, p);

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError,
            $"[{host} × {kind} × {path}] must never produce SPY0908/SPY0909. Diagnostics: "
            + $"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{host} × {kind} × {path}] must compile and run. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected,
            $"[{host} × {kind} × {path}] the default must be evaluated "
            + (p.ArgumentSupplied
                ? "ZERO times — the caller supplied the argument, so no 'side' line may appear"
                : $"EXACTLY once ({k.DefaultMarkerCount} 'side' line(s), never 2× or 4× that)")
            + $" — python3's field(default_factory=…) answer\n{source}");
    }

    [Fact]
    public void EvaluationCountMatrix_IsTotalOverItsAxes()
    {
        CountedDefaults.Length.Should().Be(CountedDefaultCount);
        CountedHosts.Length.Should().Be(CountedHostCount);
        CountedPaths.Length.Should().Be(CountedPathCount);
        CountedDefaults.Select(k => k.Name).Should().OnlyHaveUniqueItems();
        CountedHosts.Select(h => h.Name).Should().OnlyHaveUniqueItems();
        CountedPaths.Select(p => p.Name).Should().OnlyHaveUniqueItems();

        CountedPaths.Count(p => p.ArgumentSupplied).Should().Be(2,
            "an explicit argument reaches the constructor positionally and by keyword, and both "
            + "must skip the default");

        // The literal cell count, not a product of the same arrays: 3 hosts × 4 default kinds ×
        // 4 paths = 48, minus the 4 Struct × DefaultWithPostInit cells (a struct has no
        // __post_init__ hook). Anchoring to 44 is what makes a silently shrunk axis a failure.
        EvaluationCountCells.Count().Should().Be(44);

        EvaluationCountCells
            .Select(c => ((string)c[0], (string)c[2]))
            .Should().NotContain(("Struct", "DefaultWithPostInit"),
                "the only excluded combination");
    }
}
