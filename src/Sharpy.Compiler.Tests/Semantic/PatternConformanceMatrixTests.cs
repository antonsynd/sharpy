using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Pattern-reification conformance matrix (P5, #1708/#1619). A runtime type test on a generic
/// builtin names a CLOSED reified type, decided once in semantic analysis, and the answer does not
/// depend on the FORM the test is written in. This matrix is total over
/// <c>builtin {list, dict, set} × scrutinee {closed, T | None, object, type parameter}
/// × spelling {bare, explicit, wrong-arity}</c>, run once as a class <b>pattern</b> and once as
/// <b>isinstance</b>, and the <see cref="CrossForm_PatternAndIsinstance_AgreePerCell"/> test pins
/// that the two forms return the IDENTICAL verdict for each cell — the permanent version of the
/// "change axis when every spelling agrees" rule (verification-contract §6).
/// <para>
/// Verdicts (measured @ HEAD with the built <c>sharpyc</c>; python3 3.12 where python applies):
/// a closed or <c>T | None</c> scrutinee <b>fills</b> from the subject and runs; an <c>object</c> or
/// type-parameter scrutinee under a <b>bare</b> head is refused <b>SPY0345</b> (nothing determines
/// the vector) with the closed-spelling steer; an <b>explicit</b> head names the vector and runs; a
/// <b>wrong-arity</b> head is <b>SPY0224</b>. The (explicit × type parameter) cell is rostered N/A —
/// the pattern side over-refuses SPY0361 while isinstance runs (#1889), a live pattern/isinstance
/// divergence that would otherwise break the cross-form contract.
/// </para>
/// <para>
/// The <c>Sharpy.IList/IDict/ISet</c> erasure and the <c>object</c>-vector capture of earlier
/// versions are gone: every bare-on-object cell that used to RUN erased is now refused, and the
/// capture on a closed subject keeps its element type (Group Capture below).
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class PatternConformanceMatrixTests : IntegrationTestBase
{
    public PatternConformanceMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string SPY0345 = DiagnosticCodes.Semantic.OpenGenericTypeTest;
    private const string SPY0224 = DiagnosticCodes.Semantic.WrongArgumentCount;
    private const string SPY0361 = DiagnosticCodes.Semantic.TypePatternIncompatible;
    private const string SPY0202 = DiagnosticCodes.Semantic.UndefinedType;
    private const string SPY0700 = DiagnosticCodes.ValidationOverflow.IrrefutablePatternNotLast;

    // ══ Axes (anchored to literals, not to any enum's own totality) ═══════════════════════════

    // The three FILLING builtins. tuple never fills (structural ValueTuple) and frozenset is handled
    // in its own group; both keep the {list, dict, set} grid honest by contrast.
    private static readonly string[] Builtins = { "list", "dict", "set" };
    private static readonly string[] Scrutinees = { "closed", "tnone", "object", "typeparam" };
    private static readonly string[] Spellings = { "bare", "explicit", "wrongarity" };

    private sealed record BuiltinInfo(string Closed, string Literal, string Explicit, string WrongArity);

    private static BuiltinInfo Info(string b) => b switch
    {
        "list" => new("list[int]", "[1, 2]", "list[int]", "list[int, str]"),
        "dict" => new("dict[str, int]", "{\"a\": 1}", "dict[str, int]", "dict[str]"),
        "set" => new("set[int]", "{1, 2}", "set[int]", "set[int, str]"),
        _ => throw new ArgumentOutOfRangeException(nameof(b), b, null),
    };

    // A cell's expected outcome: Run(stdout) or Refuse(code).
    private sealed record Verdict(string? Output, string? Code)
    {
        public static Verdict Run(string output = "hit") => new(output, null);
        public static Verdict Refuse(string code) => new(null, code);
        public bool IsRun => Code is null;
    }

    // The verdict is a function of (scrutinee, spelling) only — identical for every filling builtin
    // and for BOTH the pattern and isinstance forms. That identity is the reification contract; the
    // cross-form test below asserts it independently.
    private static Verdict VerdictFor(string scrutinee, string spelling) => (scrutinee, spelling) switch
    {
        (_, "wrongarity") => Verdict.Refuse(SPY0224),
        ("object", "bare") => Verdict.Refuse(SPY0345),
        ("typeparam", "bare") => Verdict.Refuse(SPY0345),
        // ("typeparam", "explicit") is rostered #1889 — never reaches here.
        (_, _) => Verdict.Run(),
    };

    // (explicit × type parameter) is the one cell the pattern and isinstance forms DISAGREE on today:
    // the pattern side reports SPY0361 ("incompatible with scrutinee type 'T'") while
    // isinstance(v, list[int]) runs. Rostered until #1889 unifies them.
    private static bool IsRostered(string scrutinee, string spelling)
        => scrutinee == "typeparam" && spelling == "explicit";

    public static IEnumerable<object[]> GridCells()
    {
        foreach (var b in Builtins)
            foreach (var s in Scrutinees)
                foreach (var sp in Spellings)
                    if (!IsRostered(s, sp))
                        yield return new object[] { b, s, sp };
    }

    // ── Source builders ──────────────────────────────────────────────────────────────────────

    private static string HeadFor(string builtin, string spelling)
    {
        var info = Info(builtin);
        return spelling switch
        {
            "bare" => $"{builtin}()",
            "explicit" => $"{info.Explicit}()",
            "wrongarity" => $"{info.WrongArity}()",
            _ => throw new ArgumentOutOfRangeException(nameof(spelling)),
        };
    }

    private static string TypeFor(string builtin, string spelling)
    {
        var info = Info(builtin);
        return spelling switch
        {
            "bare" => builtin,
            "explicit" => info.Explicit,
            "wrongarity" => info.WrongArity,
            _ => throw new ArgumentOutOfRangeException(nameof(spelling)),
        };
    }

    private static string PatternSource(string builtin, string scrutinee, string spelling)
    {
        var info = Info(builtin);
        var head = HeadFor(builtin, spelling);
        return scrutinee switch
        {
            "closed" => $@"
def check(xs: {info.Closed}) -> None:
    match xs:
        case {head}:
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    xs: {info.Closed} = {info.Literal}
    check(xs)
",
            // An explicit `case None:` tail (#1891 fixed, P13 D2/D3): the payload head is PayloadTotal
            // over `T | None`, `case None:` is the None arm, and the pair is exhaustive — no spurious
            // SPY0700/SPY0463. The passed value is non-None, so it takes the head arm ("hit"). The
            // none-first order and the int() value-payload twin are the dedicated tests below.
            "tnone" => $@"
def check(xs: {info.Closed} | None) -> None:
    match xs:
        case {head}:
            print(""hit"")
        case None:
            print(""none"")

def main() -> None:
    xs: {info.Closed} = {info.Literal}
    check(xs)
",
            "object" => $@"
def check(o: object) -> None:
    match o:
        case {head}:
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    xs: {info.Closed} = {info.Literal}
    o: object = xs
    check(o)
",
            "typeparam" => $@"
def check[T](v: T) -> None:
    match v:
        case {head}:
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    xs: {info.Closed} = {info.Literal}
    check[{info.Closed}](xs)
",
            _ => throw new ArgumentOutOfRangeException(nameof(scrutinee)),
        };
    }

    private static string IsinstanceSource(string builtin, string scrutinee, string spelling)
    {
        var info = Info(builtin);
        var type = TypeFor(builtin, spelling);
        return scrutinee switch
        {
            "closed" => $@"
def check(xs: {info.Closed}) -> None:
    if isinstance(xs, {type}):
        print(""hit"")
    else:
        print(""miss"")

def main() -> None:
    xs: {info.Closed} = {info.Literal}
    check(xs)
",
            "tnone" => $@"
def check(xs: {info.Closed} | None) -> None:
    if isinstance(xs, {type}):
        print(""hit"")
    else:
        print(""miss"")

def main() -> None:
    xs: {info.Closed} = {info.Literal}
    check(xs)
",
            "object" => $@"
def check(o: object) -> None:
    if isinstance(o, {type}):
        print(""hit"")
    else:
        print(""miss"")

def main() -> None:
    xs: {info.Closed} = {info.Literal}
    o: object = xs
    check(o)
",
            "typeparam" => $@"
def check[T](v: T) -> None:
    if isinstance(v, {type}):
        print(""hit"")
    else:
        print(""miss"")

def main() -> None:
    xs: {info.Closed} = {info.Literal}
    check[{info.Closed}](xs)
",
            _ => throw new ArgumentOutOfRangeException(nameof(scrutinee)),
        };
    }

    private void AssertVerdict(ExecutionResult result, Verdict expected, string label, string source)
    {
        if (expected.IsRun)
        {
            result.Success.Should().BeTrue(
                $"[{label}] must compile and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.TrimEnd().Should().Be(expected.Output,
                $"[{label}] fills from the subject / the explicit head and matches\n{source}");
        }
        else
        {
            result.Success.Should().BeFalse(
                $"[{label}] must be refused. Output was: {result.StandardOutput}\n{source}");
            result.RawDiagnostics.Should().Contain(d => d.Code == expected.Code,
                $"[{label}] must report {expected.Code}. Diagnostics: "
                + $"{string.Join(" | ", result.RawDiagnostics.Select(d => d.Code))}\n{source}");
        }
    }

    // ── Group Grid-Pattern: builtin × scrutinee × spelling, PATTERN form ──────────────────────

    [Theory]
    [MemberData(nameof(GridCells))]
    public void Grid_PatternForm(string builtin, string scrutinee, string spelling)
    {
        var source = PatternSource(builtin, scrutinee, spelling);
        var result = CompileAndExecute(source);
        AssertVerdict(result, VerdictFor(scrutinee, spelling),
            $"pattern {builtin}/{scrutinee}/{spelling}", source);
    }

    // ── Group Grid-Isinstance: builtin × scrutinee × spelling, ISINSTANCE form ────────────────

    [Theory]
    [MemberData(nameof(GridCells))]
    public void Grid_IsinstanceForm(string builtin, string scrutinee, string spelling)
    {
        var source = IsinstanceSource(builtin, scrutinee, spelling);
        var result = CompileAndExecute(source);
        AssertVerdict(result, VerdictFor(scrutinee, spelling),
            $"isinstance {builtin}/{scrutinee}/{spelling}", source);
    }

    // ── Cross-form: the two forms return the SAME verdict for each cell ───────────────────────
    // This is the load-bearing agreement assertion: it recomputes the pattern and isinstance
    // outcomes independently (not from VerdictFor) and asserts they match, so a future divergence
    // like #1889 in a NON-rostered cell would fail here rather than pass silently.

    [Theory]
    [MemberData(nameof(GridCells))]
    public void CrossForm_PatternAndIsinstance_AgreePerCell(string builtin, string scrutinee, string spelling)
    {
        var pat = CompileAndExecute(PatternSource(builtin, scrutinee, spelling));
        var iso = CompileAndExecute(IsinstanceSource(builtin, scrutinee, spelling));

        var label = $"{builtin}/{scrutinee}/{spelling}";
        pat.Success.Should().Be(iso.Success,
            $"[{label}] the pattern and isinstance forms must agree on whether the test is refused. "
            + $"pattern: {string.Join(",", pat.RawDiagnostics.Select(d => d.Code))} | "
            + $"isinstance: {string.Join(",", iso.RawDiagnostics.Select(d => d.Code))}");

        if (pat.Success)
        {
            pat.StandardOutput.TrimEnd().Should().Be(iso.StandardOutput.TrimEnd(),
                $"[{label}] both forms fill/test the same reified type and print the same result");
        }
        else
        {
            var patCodes = pat.RawDiagnostics.Select(d => d.Code).ToHashSet();
            var isoCodes = iso.RawDiagnostics.Select(d => d.Code).ToHashSet();
            (patCodes.Contains(SPY0345) || patCodes.Contains(SPY0224))
                .Should().BeTrue($"[{label}] pattern refuses with the shared refusal code");
            isoCodes.Overlaps(patCodes).Should().BeTrue(
                $"[{label}] the two forms share their primary refusal code. "
                + $"pattern: {string.Join(",", patCodes)} | isinstance: {string.Join(",", isoCodes)}");
        }
    }

    // ── Totality (against the literal axes, and the rostered cell) ────────────────────────────

    [Fact]
    public void Grid_IsTotalOverItsAxes()
    {
        Builtins.Length.Should().Be(3);
        Scrutinees.Length.Should().Be(4);
        Spellings.Length.Should().Be(3);

        var product = Builtins.Length * Scrutinees.Length * Spellings.Length; // 36
        var executing = GridCells().Count();
        var rostered = product - executing;

        // Exactly one cell per builtin is rostered: (explicit × type parameter), #1889.
        rostered.Should().Be(Builtins.Length,
            "the only N/A cell is (explicit × type parameter) — pattern SPY0361 vs isinstance run (#1889)");
        (executing + rostered).Should().Be(product);
    }

    // ── Group T|None ordering: a payload head + `case None:` is exhaustive in BOTH orders ──────
    // P13 D2/D3 (#1891): a payload head over `T | None` is PayloadTotal (not Total, so no spurious
    // SPY0700 subsumption), `case None:` is the None arm, and the two together are the finite family
    // (no SPY0463/SPY0416). Both orders compile clean and route None to the None arm. Anchored to
    // literals: the passed value is None, so the None arm's "none" is the observable proof it is
    // reachable — head-first would have subsumed it before the fix.

    public static IEnumerable<object[]> TNoneOrderingCells()
    {
        foreach (var b in Builtins)
            foreach (var order in new[] { "headfirst", "nonefirst" })
                yield return new object[] { b, order };
    }

    private static string TNonePayloadAndNoneSource(string closed, string head, string order)
    {
        var headArm = $@"        case {head}:
            print(""hit"")";
        var noneArm = @"        case None:
            print(""none"")";
        var body = order == "headfirst" ? $"{headArm}\n{noneArm}" : $"{noneArm}\n{headArm}";
        return $@"
def check(xs: {closed} | None) -> None:
    match xs:
{body}

def main() -> None:
    check(None)
";
    }

    [Theory]
    [MemberData(nameof(TNoneOrderingCells))]
    public void TNone_PayloadHeadAndNoneArm_BothOrders_Run(string builtin, string order)
    {
        var info = Info(builtin);
        var source = TNonePayloadAndNoneSource(info.Closed, $"{builtin}()", order);
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"[tnone {builtin}/{order}] a payload head + case None over T | None is exhaustive and "
            + $"non-subsuming (#1891). Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.TrimEnd().Should().Be("none",
            $"[tnone {builtin}/{order}] None routes to the None arm in both orders\n{source}");
    }

    [Theory]
    [InlineData("headfirst")]
    [InlineData("nonefirst")]
    public void TNone_ValueTypePayload_IntAndNone_BothOrders_Run(string order)
    {
        // The int() value-payload twin of the list() row: a value-typed payload is PayloadTotal the
        // same way (D3's "value-type payloads" are covered once the coverage is null-aware, not the
        // pre-fix assignability test that failed for them).
        var source = TNonePayloadAndNoneSource("int", "int()", order);
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"[tnone int/{order}] int() + case None over int | None is exhaustive (#1891, D3). "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.TrimEnd().Should().Be("none",
            $"[tnone int/{order}] None routes to the None arm\n{source}");
    }

    // ── Group 3: cross-collection (R-P5-1) ───────────────────────────────────────────────────
    // A BARE list head on a determinate-incompatible closed subject (dict[str,int]) hits arm 3 →
    // SPY0345 (open generic), for BOTH pattern and isinstance — restoring SPY0361 here would require
    // the default-object vector the reification ruling deletes. The EXPLICIT determinate-incompatible
    // spelling is arm 2, decided-closed → SPY0361.

    [Fact]
    public void CrossCollection_BareListOnDict_SPY0345()
    {
        const string source = @"
def check(d: dict[str, int]) -> None:
    match d:
        case list():
            print(""never"")
        case _:
            print(""dict"")

def main() -> None:
    d: dict[str, int] = {""a"": 1}
    check(d)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("a bare cross-collection head is refused, not SPY0361");
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345,
            "R-P5-1: a bare list head on a dict subject hits arm 3 → SPY0345");
    }

    [Fact]
    public void CrossCollection_BareListOnDict_Isinstance_SPY0345()
    {
        const string source = @"
def check(d: dict[str, int]) -> None:
    if isinstance(d, list):
        print(""never"")
    else:
        print(""dict"")

def main() -> None:
    d: dict[str, int] = {""a"": 1}
    check(d)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345,
            "isinstance agrees with the pattern form: a bare cross-collection test is SPY0345");
    }

    [Fact]
    public void CrossCollection_ExplicitListOnDict_SPY0361()
    {
        const string source = @"
def check(d: dict[str, int]) -> None:
    match d:
        case list[int]():
            print(""never"")
        case _:
            print(""dict"")

def main() -> None:
    d: dict[str, int] = {""a"": 1}
    check(d)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("an explicit closed head on a determinate-incompatible subject is SPY0361");
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0361,
            "a dict[str,int] can never be a list[int] — the decided closed type is statically impossible");
    }

    // ── Group as-cast: as? / as! forms (bare → SPY0345, explicit → run), uniform ──────────────

    [Fact]
    public void AsOptional_BareListOnObject_SPY0345()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[int] = [1, 2]
    o: object = xs
    r = o as? list
    print(r is not None)
");
        result.Success.Should().BeFalse("a bare open cast target is refused");
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345,
            "the cast form joins the shared decider: a bare open generic is SPY0345 (exactly one diagnostic)");
        result.RawDiagnostics.Count(d => d.Code == SPY0345).Should().Be(1,
            "the cast-site double report (SPY0345 + SPY0224) was folded — one refusal, one code");
    }

    [Fact]
    public void AsChecked_BareListOnObject_SPY0345()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[int] = [1, 2]
    o: object = xs
    r = o as! list
    print(len(r))
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345,
            "as! refuses the bare open generic uniformly with as?");
    }

    [Fact]
    public void AsOptional_ExplicitList_Runs_ReifiedExact()
    {
        // Reification is visible at the cast: a list[int] value is NOT a list[object].
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[int] = [1, 2]
    o: object = xs
    same = o as? list[int]
    other = o as? list[object]
    print(same is not None)
    print(other is not None)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be("True\nFalse",
            "list[int] casts to list[int] (True) but is not a list[object] (False) — reified, exact");
    }

    // ── Group not-isinstance: the negated form agrees with isinstance ─────────────────────────

    [Fact]
    public void NotIsinstance_OnClosed_Runs()
    {
        var result = CompileAndExecute(@"
def check(xs: list[int]) -> None:
    if not isinstance(xs, list):
        print(""no"")
    else:
        print(""yes"")

def main() -> None:
    check([1])
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("yes");
    }

    [Fact]
    public void NotIsinstance_BareOnObject_SPY0345()
    {
        var result = CompileAndExecute(@"
def check(o: object) -> None:
    if not isinstance(o, list):
        print(""no"")
    else:
        print(""yes"")

def main() -> None:
    check(1)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345,
            "`not isinstance` refuses the bare open generic exactly as `isinstance` does");
    }

    // ── Group frozenset / tuple: fill-vs-structural contrast ──────────────────────────────────
    // frozenset fills from a closed subject (like list/dict/set); tuple NEVER fills — a Sharpy
    // tuple is a structural ValueTuple, so even a closed subject under a bare head is SPY0345 and
    // only the explicit spelling runs.

    [Fact]
    public void Frozenset_ClosedSubject_BareHead_Fills()
    {
        var result = CompileAndExecute(@"
def check(s: frozenset[int]) -> None:
    match s:
        case frozenset():
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    check(frozenset([1, 2]))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("hit");
    }

    [Fact]
    public void Frozenset_ObjectSubject_BareHead_SPY0345()
    {
        var result = CompileAndExecute(@"
def check(o: object) -> None:
    match o:
        case frozenset():
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    check(1)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345);
    }

    [Fact]
    public void Tuple_ClosedSubject_BareHead_SPY0345_NeverFills()
    {
        var result = CompileAndExecute(@"
def check(t: tuple[int, int]) -> None:
    match t:
        case tuple():
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    check((1, 2))
");
        result.Success.Should().BeFalse(
            "tuple is a structural ValueTuple and never fills — even a closed subject is SPY0345 for a bare head");
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345);
    }

    [Fact]
    public void Tuple_ClosedSubject_ExplicitHead_Runs()
    {
        var result = CompileAndExecute(@"
def check(t: tuple[int, int]) -> None:
    match t:
        case tuple[int, int]():
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    check((1, 2))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("hit");
    }

    // ── Group Refused-names: names that head no generic type test ─────────────────────────────

    public static IEnumerable<object[]> RefusedNameCells()
    {
        // Not registered types: the head names nothing to test against.
        yield return new object[] { "bytearray()", SPY0202 };
        yield return new object[] { "range()", SPY0202 };
        // Generic with no filling source on an object subject: honest SPY0345 (no erasure interface).
        yield return new object[] { "tuple()", SPY0345 };
        yield return new object[] { "frozenset()", SPY0345 };
    }

    [Theory]
    [MemberData(nameof(RefusedNameCells))]
    public void RefusedName_ReportsItsCode(string head, string expectedCode)
    {
        var source = @"
def check(o: object) -> None:
    match o:
        case @P@:
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    check(1)
".Replace("@P@", head, StringComparison.Ordinal);
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"`case {head}:` names no testable type. Output: {result.StandardOutput}");
        result.RawDiagnostics.Should().Contain(d => d.Code == expectedCode,
            $"`case {head}:` is refused with {expectedCode}. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
    }

    // ── Group Nested-position: the verdict does not depend on where the head is written ────────
    // A closed nested subject fills at any depth; an open element is refused at any depth (the same
    // SPY0345 as at the top level).

    [Fact]
    public void NestedPosition_ClosedElement_Fills_Runs()
    {
        var result = CompileAndExecute(@"
def seq(xs: list[list[int]]) -> None:
    match xs:
        case [list(inner)]:
            n: int = inner[0]
            print(""seq"", n)
        case _:
            print(""miss"")

def main() -> None:
    seq([[9]])
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("seq 9",
            "the element type list[int] fills the nested bare head; the capture keeps its element type");
    }

    [Fact]
    public void NestedPosition_OpenElement_SPY0345()
    {
        var result = CompileAndExecute(@"
def seq(xs: list[object]) -> None:
    match xs:
        case [list(inner)]:
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    xs: list[object] = [[1]]
    seq(xs)
");
        result.Success.Should().BeFalse("the element type is object — the nested bare head is refused");
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345,
            "an open head is refused at the nested position exactly as at the top level");
    }

    // ── Group Capture: what the capture is TYPED as (the annotated destination is the probe) ───
    // On a CLOSED subject the vector is filled FROM THE SUBJECT, so the capture keeps its elements —
    // ys[0] is an int and `n: int = ys[0]` type-checks (the erased list[object] capture is gone).

    [Fact]
    public void Capture_OnClosed_KeepsElementType()
    {
        var result = CompileAndExecute(@"
def check(xs: list[int]) -> None:
    match xs:
        case list(ys):
            ys[0] += 10
            print(ys)
        case _:
            print(""miss"")

def main() -> None:
    check([1, 2, 3])
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("[11, 2, 3]",
            "an erased list[object] capture could not compile `ys[0] += 10`; the reified list[int] does");
    }

    [Fact]
    public void Capture_OnObject_Refused_NoErasedSurface()
    {
        // The old erased path bound `xs: list[object]` and RAN on an object subject; now the bare
        // head is refused, so there is no capture to mistype. Positive control for "no erased read".
        var result = CompileAndExecute(@"
def check(o: object) -> None:
    match o:
        case list(xs):
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    xs: list[int] = [1, 2]
    o: object = xs
    check(o)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345);
    }

    // ══ Preserved non-collection groups (these arms are unaffected by reification) ════════════

    // ── Self-matching primitives bind the whole subject ──
    public static IEnumerable<object[]> SelfMatchingCells()
    {
        yield return new object[] { "int", "42", "int 43" };
        yield return new object[] { "str", "\"hi\"", "str HI" };
    }

    [Theory]
    [MemberData(nameof(SelfMatchingCells))]
    public void SelfMatching_OnObject_Runs(string typeName, string value, string expected)
    {
        var source = typeName == "int"
            ? $@"
def describe(x: object) -> str:
    match x:
        case int(n):
            m: int = n + 1
            return f""int {{m}}""
        case _:
            return ""other""

def main() -> None:
    print(describe({value}))
"
            : $@"
def describe(x: object) -> str:
    match x:
        case str(s):
            t: str = s.upper()
            return f""str {{t}}""
        case _:
            return ""other""

def main() -> None:
    print(describe({value}))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be(expected);
    }

    // ── User generic fill ──
    [Fact]
    public void UserGeneric_BoxOnBoxInt_Filled_Runs()
    {
        const string source = @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

def main() -> None:
    b: Box[int] = Box[int](7)
    match b:
        case Box():
            print(""box filled"")
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("box filled");
    }

    // ── Subsumption: a total earlier arm makes a later refutable arm unreachable (SPY0700) ──
    public static IEnumerable<object[]> SubsumingEarlierArmCells()
    {
        yield return new object[] { "int()" };
        yield return new object[] { "int(n)" };
        yield return new object[] { "int() as n" };
    }

    [Theory]
    [MemberData(nameof(SubsumingEarlierArmCells))]
    public void Subsumption_EarlierArmCoversLaterLiteral_SPY0700(string earlierPattern)
    {
        var source = @"
def check(o: object) -> None:
    match o:
        case @EARLIER@:
            print(""int"")
        case 99:
            print(""ninety-nine"")
        case _:
            print(""other"")

def main() -> None:
    check(1)
".Replace("@EARLIER@", earlierPattern, StringComparison.Ordinal);
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse(
            $"`case {earlierPattern}:` matches every int, so `case 99:` is unreachable. Output: {result.StandardOutput}");
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0700);
    }

    public static IEnumerable<object[]> SubsumptionPositiveControlCells()
    {
        yield return new object[]
        {
            "case 99:\n            print(\"ninety-nine\")\n        case int():\n            print(\"int\")",
            "int",
        };
        yield return new object[]
        {
            "case int() if always():\n            print(\"int\")\n        case 99:\n            print(\"ninety-nine\")",
            "int",
        };
        yield return new object[]
        {
            "case float():\n            print(\"float\")\n        case 1:\n            print(\"one\")",
            "one",
        };
    }

    [Theory]
    [MemberData(nameof(SubsumptionPositiveControlCells))]
    public void Subsumption_PositiveControls_Run(string arms, string expected)
    {
        var source = @"
def always() -> bool:
    return True

def check(o: object) -> None:
    match o:
        @ARMS@
        case _:
            print(""other"")

def main() -> None:
    check(1)
".Replace("@ARMS@", arms, StringComparison.Ordinal);
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            "this arm order is reachable and must NOT be refused. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.StandardOutput.TrimEnd().Should().Be(expected);
    }

    // ── As-pattern capture ──
    [Fact]
    public void AsPatternCapture_StrOnObject_TypedAsStr()
    {
        const string source = @"
def main() -> None:
    x: object = ""hello""
    match x:
        case str() as s:
            t: str = s.upper()
            print(t)
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("HELLO");
    }
}
