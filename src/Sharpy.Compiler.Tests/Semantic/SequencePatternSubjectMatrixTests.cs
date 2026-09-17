using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Sequence-pattern subject matrix (P5, #1702, R-D). A sequence pattern (<c>[a]</c>, <c>[a, *rest]</c>,
/// <c>[]</c>, <c>[1, 2]</c>) decides its subject through the same type-test classifier as a class
/// pattern: a closed <c>list[T]</c> (or <c>list[T] | None</c>) subject <b>fills</b> and runs at any
/// depth and in any position; an <b>open</b> subject (<c>object</c>, a type parameter) is refused
/// <b>SPY0345</b> with the <c>case list[int]([1, 2])</c> steer; an <c>Optional</c> subject
/// (<c>list[T]?</c>) is refused <b>SPY0498</b>, steering to the constructor cases.
/// <para>
/// Total over <c>subject {list[int], list[int] | None, list[int]?, object, type parameter}
/// × spelling {bare, explicit (case list[int]([...]))} × shape {single, star, empty, literals}</c>,
/// re-run at the match-expression position for the running combinations, plus the depth and
/// interop cells the issue names (nested fill, nested open, class-positional nesting, array[int]
/// interop, and the non-sequence refusals str / tuple → SPY0220).
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class SequencePatternSubjectMatrixTests : IntegrationTestBase
{
    public SequencePatternSubjectMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string SPY0345 = DiagnosticCodes.Semantic.OpenGenericTypeTest;
    private const string SPY0498 = DiagnosticCodes.Validation.PayloadTypePatternOverUnion;
    private const string SPY0361 = DiagnosticCodes.Semantic.TypePatternIncompatible;
    private const string SPY0220 = DiagnosticCodes.Semantic.TypeMismatch;

    private static readonly string[] Subjects = { "closed", "tnone", "optional", "object", "typeparam" };
    private static readonly string[] Spellings = { "bare", "explicit" };
    private static readonly string[] Shapes = { "single", "star", "empty", "literals" };

    private sealed record ShapeInfo(string Pattern, string PrintStmt, string Expr, string Output, string Value);

    private static ShapeInfo Shape(string s) => s switch
    {
        "single" => new("[a]", "print(\"single\", a)", "f\"single {a}\"", "single 7", "[7]"),
        "star" => new("[a, *rest]", "print(\"star\", a, rest)", "f\"star {a} {rest}\"", "star 3 [4, 5]", "[3, 4, 5]"),
        "empty" => new("[]", "print(\"empty\")", "\"empty\"", "empty", "[]"),
        "literals" => new("[1, 2]", "print(\"seq\")", "\"seq\"", "seq", "[1, 2]"),
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };

    private sealed record Verdict(string? Output, string? Code);

    private static Verdict VerdictFor(string subject, string spelling, ShapeInfo shape) => subject switch
    {
        "closed" or "tnone" => new(shape.Output, null),
        "optional" => new(null, SPY0498),
        "object" => spelling == "explicit" ? new(shape.Output, null) : new(null, SPY0345),
        // typeparam × explicit is rostered (#1889); only bare reaches here.
        "typeparam" => new(null, SPY0345),
        _ => throw new ArgumentOutOfRangeException(nameof(subject)),
    };

    // typeparam × explicit: the explicit closed head on a type-parameter subject is SPY0361 on the
    // pattern side (isinstance runs) — the same #1889 divergence as the class matrix. Rostered N/A.
    private static bool IsRostered(string subject, string spelling)
        => subject == "typeparam" && spelling == "explicit";

    public static IEnumerable<object[]> MatrixCells()
    {
        foreach (var subj in Subjects)
            foreach (var sp in Spellings)
                foreach (var sh in Shapes)
                    if (!IsRostered(subj, sp))
                        yield return new object[] { subj, sp, sh };
    }

    private static string PatternText(string spelling, ShapeInfo shape)
        => spelling == "explicit" ? $"list[int]({shape.Pattern})" : shape.Pattern;

    private static string StatementSource(string subject, string spelling, ShapeInfo shape)
    {
        var pat = PatternText(spelling, shape);
        return subject switch
        {
            "closed" => $@"
def check(xs: list[int]) -> None:
    match xs:
        case {pat}:
            {shape.PrintStmt}
        case _:
            print(""miss"")

def main() -> None:
    xs: list[int] = {shape.Value}
    check(xs)
",
            "tnone" => $@"
def check(xs: list[int] | None) -> None:
    match xs:
        case {pat}:
            {shape.PrintStmt}
        case _:
            print(""miss"")

def main() -> None:
    xs: list[int] = {shape.Value}
    check(xs)
",
            "optional" => $@"
def check(xs: list[int]?) -> None:
    match xs:
        case {pat}:
            {shape.PrintStmt}
        case _:
            print(""miss"")

def main() -> None:
    check(Some({shape.Value}))
",
            "object" => $@"
def check(o: object) -> None:
    match o:
        case {pat}:
            {shape.PrintStmt}
        case _:
            print(""miss"")

def main() -> None:
    xs: list[int] = {shape.Value}
    o: object = xs
    check(o)
",
            "typeparam" => $@"
def check[T](v: T) -> None:
    match v:
        case {pat}:
            {shape.PrintStmt}
        case _:
            print(""miss"")

def main() -> None:
    xs: list[int] = {shape.Value}
    check[list[int]](xs)
",
            _ => throw new ArgumentOutOfRangeException(nameof(subject)),
        };
    }

    private void AssertVerdict(ExecutionResult result, Verdict expected, string label, string source)
    {
        if (expected.Code is null)
        {
            result.Success.Should().BeTrue(
                $"[{label}] must fill and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.TrimEnd().Should().Be(expected.Output, $"[{label}]\n{source}");
        }
        else
        {
            result.Success.Should().BeFalse($"[{label}] must be refused. Output: {result.StandardOutput}\n{source}");
            result.RawDiagnostics.Should().Contain(d => d.Code == expected.Code,
                $"[{label}] must report {expected.Code}. Diagnostics: "
                + $"{string.Join(" | ", result.RawDiagnostics.Select(d => d.Code))}\n{source}");
        }
    }

    // ── Statement position: subject × spelling × shape ────────────────────────────────────────

    [Theory]
    [MemberData(nameof(MatrixCells))]
    public void SequencePattern_StatementPosition(string subject, string spelling, string shapeName)
    {
        var shape = Shape(shapeName);
        var source = StatementSource(subject, spelling, shape);
        var result = CompileAndExecute(source);
        AssertVerdict(result, VerdictFor(subject, spelling, shape), $"{subject}/{spelling}/{shapeName}", source);
    }

    // ── Steer text: the open-subject refusal steers to the closed sequence spelling ───────────

    [Fact]
    public void OpenSubject_Bare_SteersToClosedSequenceSpelling()
    {
        var result = CompileAndExecute(StatementSource("object", "bare", Shape("star")));
        result.Success.Should().BeFalse();
        string.Join("\n", result.CompilationErrors).Should().Contain("case list[int]([1, 2])",
            "R-D: the open-sequence refusal steers to the closed spelling that tests the element type");
    }

    [Fact]
    public void OptionalSubject_SteersToConstructorCases()
    {
        var result = CompileAndExecute(StatementSource("optional", "bare", Shape("star")));
        result.Success.Should().BeFalse();
        string.Join("\n", result.CompilationErrors).Should().Contain("Some",
            "SPY0498 steers a sequence pattern on an Optional to the constructor cases");
    }

    // ── Match-expression position: the running combinations agree with the statement form ──────

    public static IEnumerable<object[]> RunningExprCells()
    {
        // (subject, spelling) pairs that RUN, at every shape.
        var running = new (string Subject, string Spelling)[]
        {
            ("closed", "bare"), ("closed", "explicit"),
            ("tnone", "bare"), ("tnone", "explicit"),
            ("object", "explicit"),
        };
        foreach (var (subj, sp) in running)
            foreach (var sh in Shapes)
                yield return new object[] { subj, sp, sh };
    }

    private static string ExpressionSource(string subject, string spelling, ShapeInfo shape)
    {
        var pat = PatternText(spelling, shape);
        return subject switch
        {
            "closed" => $@"
def d(xs: list[int]) -> str:
    return match xs:
        case {pat}: {shape.Expr}
        case _: ""miss""

def main() -> None:
    xs: list[int] = {shape.Value}
    print(d(xs))
",
            "tnone" => $@"
def d(xs: list[int] | None) -> str:
    return match xs:
        case {pat}: {shape.Expr}
        case _: ""miss""

def main() -> None:
    xs: list[int] = {shape.Value}
    print(d(xs))
",
            "object" => $@"
def d(o: object) -> str:
    return match o:
        case {pat}: {shape.Expr}
        case _: ""miss""

def main() -> None:
    xs: list[int] = {shape.Value}
    o: object = xs
    print(d(o))
",
            _ => throw new ArgumentOutOfRangeException(nameof(subject)),
        };
    }

    [Theory]
    [MemberData(nameof(RunningExprCells))]
    public void SequencePattern_MatchExpressionPosition(string subject, string spelling, string shapeName)
    {
        var shape = Shape(shapeName);
        var source = ExpressionSource(subject, spelling, shape);
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"[expr {subject}/{spelling}/{shapeName}] must run. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.TrimEnd().Should().Be(shape.Output,
            $"[expr {subject}/{spelling}/{shapeName}] the verdict does not depend on statement vs expression\n{source}");
    }

    // ── Totality ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        Subjects.Length.Should().Be(5);
        Spellings.Length.Should().Be(2);
        Shapes.Length.Should().Be(4);

        var product = Subjects.Length * Spellings.Length * Shapes.Length; // 40
        var executing = MatrixCells().Count();
        var rostered = product - executing;

        // (typeparam × explicit) is rostered at each shape: 4 cells, #1889.
        rostered.Should().Be(Shapes.Length,
            "the only N/A cells are (type parameter × explicit) — pattern SPY0361 vs isinstance run (#1889)");
        (executing + rostered).Should().Be(product);
    }

    // ── Depth: filling and refusal are the same at any depth ──────────────────────────────────

    [Fact]
    public void NestedFill_ClosedElement_Runs()
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
        result.StandardOutput.TrimEnd().Should().Be("seq 9");
    }

    [Fact]
    public void NestedOpen_ObjectElement_SPY0345()
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
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0345);
    }

    [Fact]
    public void NestedInClassPositional_ClosedSubject_Runs()
    {
        var result = CompileAndExecute(@"
class Box:
    items: list[int]

    def __init__(self, items: list[int]):
        self.items = items

def d(b: Box) -> None:
    match b:
        case Box([a, *rest]):
            print(""star"", a, rest)
        case _:
            print(""other"")

def main() -> None:
    d(Box([3, 4, 5]))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("star 3 [4, 5]",
            "the sequence subject fills from the class field's list[int] type at the nested position");
    }

    // ── Interop: an array[int] subject fills (list-pattern interop) ───────────────────────────

    [Fact]
    public void ArraySubject_ListPattern_Fills()
    {
        var result = CompileAndExecute(@"
def d(xs: array[int]) -> None:
    match xs:
        case [a, *rest]:
            print(""star"", a, rest)
        case _:
            print(""other"")

def main() -> None:
    xs: array[int] = array[int](3)
    xs[0] = 3
    xs[1] = 4
    xs[2] = 5
    d(xs)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("star 3 [4, 5]",
            "array[int] is a sequence subject and fills the element type like list[int]");
    }

    // ── Non-sequence subjects: str and tuple → SPY0220 (documented deviations) ─────────────────

    [Fact]
    public void StrSubject_SequencePattern_SPY0220()
    {
        var result = CompileAndExecute(@"
def d(s: str) -> None:
    match s:
        case [a, b]:
            print(""seq"")
        case _:
            print(""other"")

def main() -> None:
    d(""hi"")
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0220,
            "a str is not a sequence subject — SPY0220 non-sequence, not SPY0345");
    }

    [Fact]
    public void TupleSubject_SequencePattern_SPY0220()
    {
        var result = CompileAndExecute(@"
def d(t: tuple[int, int]) -> None:
    match t:
        case [a, b]:
            print(""seq"")
        case _:
            print(""other"")

def main() -> None:
    d((1, 2))
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0220,
            "a Sharpy tuple is a ValueTuple with no Length — case (1, 2) is the tuple spelling (documented deviation)");
    }

    // ── d10: a nullable-list subject fills, and None takes the fallthrough ────────────────────

    [Fact]
    public void NullableListSubject_LiteralSequence_FillsAndFallsThrough()
    {
        var result = CompileAndExecute(@"
def check(xs: list[int] | None) -> None:
    match xs:
        case [1, 2]:
            print(""seq"")
        case _:
            print(""other"")

def main() -> None:
    check([1, 2])
    check(None)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be("seq\nother",
            "d10: list[int] | None fills the sequence; None falls to the wildcard (was a wrong SPY0220 refusal)");
    }
}
