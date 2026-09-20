using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Or-pattern capture conformance matrix (#1920). C# cannot declare a designation inside an
/// <c>or</c> pattern (CS8780), so an or-pattern whose ALTERNATIVES bind a name has no lowering —
/// whatever the alternative's kind. Before the seam in
/// <c>TypeChecker.CheckAlternativeRefusingCaptures</c>, every kind but the two the #1663 work
/// covered reached Roslyn and died as <b>SPY0908</b> (CS8780 plus CS0165 on the body's read); at
/// 41dd19de7 the same sources were the blanket explicit-head refusal SPY0125 that P5/#1708
/// retired, so the regression turned a named refusal into an internal error.
/// <para>
/// The matrix is total over <c>alternative kind × {capture present, capture absent} ×
/// {statement match, match expression}</c>. Every capture-absent cell is the positive control: it
/// RUNS, so a refusal that fired on the whole or-pattern instead of on the capture would be
/// caught. The one capture shape that DOES lower is <c>as</c> on every alternative under the same
/// name (<c>case (int() as v) | (str() as v):</c>), which the emitter hoists outside the C#
/// <c>or</c> as <c>(A or B) and var v</c> — it is a capture-present cell that RUNS, so a seam that
/// simply refused every or-pattern with a name in it would fail here.
/// </para>
/// <para>
/// Python reference (python3 3.12): CPython ACCEPTS every capture-present kind below — the
/// alternatives bind the same name — and refuses only alternatives that bind DIFFERENT names
/// ("SyntaxError: alternative patterns bind different names"). Sharpy refuses the shared-name
/// forms too, because the name would need a different static type per alternative and
/// <c>SemanticInfo</c> records one type per AST node; the refusal names the rule and steers.
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class OrPatternCaptureMatrixTests : IntegrationTestBase
{
    public OrPatternCaptureMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string SPY0359 = DiagnosticCodes.Semantic.BindingInOrPattern;
    private const string SPY0908 = DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError;

    // ══ Axes (anchored to literals, not to any enum's own totality) ═══════════════════════════

    private static readonly string[] Kinds =
    {
        "reified",      // list[int](xs) | set[int](xs)        — the #1920 repro
        "classpos",     // Point(v) | Circle(v)
        "classprop",    // Point(x=v) | Circle(x=v)
        "sequence",     // [v] | [v, _]
        "star",         // [1, *v] | [2, *v]
        "tuple",        // (v, 0) | (0, v)
        "unionpayload", // Circle(v) | Square(v)
        "nestedas",     // (Point(v) as p) | (Circle(v) as p)  — capture UNDER the whole-subject as
        "wholeas",      // (int() as v) | (str() as v)         — the one shape that lowers
    };

    private static readonly string[] Captures = { "present", "absent" };
    private static readonly string[] Forms = { "statement", "expression" };

    private const string ShapePrelude = @"class Point:
    x: int
    def __init__(self, x: int) -> None:
        self.x = x

class Circle:
    x: int
    def __init__(self, x: int) -> None:
        self.x = x
";

    private const string UnionPrelude = @"union Shape:
    case Circle(r: int)
    case Square(s: int)
";

    /// <param name="Prelude">Type declarations the cell needs.</param>
    /// <param name="SubjectType">Declared type of the match subject.</param>
    /// <param name="SubjectExpr">Value given to the subject.</param>
    /// <param name="CapturePattern">Alternatives that bind a name.</param>
    /// <param name="CaptureUse">An expression READING the bound name — a cascade behind the
    /// refusal ("undefined identifier") would show up as a second diagnostic.</param>
    /// <param name="NoCapturePattern">The same alternatives with the capture removed.</param>
    private sealed record Cell(
        string Prelude,
        string SubjectType,
        string SubjectExpr,
        string CapturePattern,
        string CaptureUse,
        string NoCapturePattern);

    private static Cell CellFor(string kind) => kind switch
    {
        "reified" => new("", "object", "[1, 2]",
            "list[int](xs) | set[int](xs)", "str(len(xs))", "list[int]() | set[int]()"),
        "classpos" => new(ShapePrelude, "object", "Point(3)",
            "Point(v) | Circle(v)", "str(v)", "Point() | Circle()"),
        "classprop" => new(ShapePrelude, "object", "Point(3)",
            "Point(x=v) | Circle(x=v)", "str(v)", "Point(x=3) | Circle(x=3)"),
        "sequence" => new("", "list[int]", "[1]",
            "[v] | [v, _]", "str(v)", "[_] | [_, _]"),
        "star" => new("", "list[int]", "[1, 2, 3]",
            "[1, *v] | [2, *v]", "str(len(v))", "[1, *_] | [2, *_]"),
        "tuple" => new("", "tuple[int, int]", "(1, 0)",
            "(v, 0) | (0, v)", "str(v)", "(_, 0) | (0, _)"),
        "unionpayload" => new(UnionPrelude, "Shape", "Shape.Circle(3)",
            "Circle(v) | Square(v)", "str(v)", "Circle(_) | Square(_)"),
        "nestedas" => new(ShapePrelude, "object", "Point(3)",
            "(Point(v) as p) | (Circle(v) as p)", "str(v)", "(Point() as p) | (Circle() as p)"),
        "wholeas" => new("", "object", "42",
            "(int() as v) | (str() as v)", "str(v)", "int() | str()"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // The verdict, written out per kind rather than derived from the table above: a capture inside
    // an alternative is SPY0359; the whole-subject `as` on every alternative is the one capture
    // that lowers and RUNS; every capture-absent twin RUNS.
    private static bool IsRefused(string kind, string capture) => (kind, capture) switch
    {
        (_, "absent") => false,
        ("wholeas", "present") => false,
        ("reified", "present") => true,
        ("classpos", "present") => true,
        ("classprop", "present") => true,
        ("sequence", "present") => true,
        ("star", "present") => true,
        ("tuple", "present") => true,
        ("unionpayload", "present") => true,
        ("nestedas", "present") => true,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // The capture-present cells that RUN print the bound value; every other running cell prints
    // "hit". `wholeas` binds the whole subject (42) under both alternatives.
    private static string ExpectedOutput(string kind, string capture)
        => (kind, capture) == ("wholeas", "present") ? "42" : "hit";

    public static IEnumerable<object[]> GridCells()
    {
        foreach (var k in Kinds)
            foreach (var c in Captures)
                foreach (var f in Forms)
                    yield return new object[] { k, c, f };
    }

    // ── Source builders ──────────────────────────────────────────────────────────────────────

    private static string SourceFor(string kind, string capture, string form)
    {
        var cell = CellFor(kind);
        var pattern = capture == "present" ? cell.CapturePattern : cell.NoCapturePattern;
        var value = capture == "present" ? cell.CaptureUse : "\"hit\"";

        // A refused cell still READS the captured name, so a cascade behind the refusal is visible.
        var hasTail = kind != "unionpayload";
        var tail = hasTail ? "\n        case _:\n            print(\"miss\")" : "";
        var exprTail = hasTail ? "\n        case _: \"miss\"" : "";

        return form == "statement"
            ? $@"{cell.Prelude}
def main() -> None:
    o: {cell.SubjectType} = {cell.SubjectExpr}
    match o:
        case {pattern}:
            print({value}){tail}
"
            : $@"{cell.Prelude}
def describe(o: {cell.SubjectType}) -> str:
    result: str = match o:
        case {pattern}: {value}{exprTail}
    return result

def main() -> None:
    o: {cell.SubjectType} = {cell.SubjectExpr}
    print(describe(o))
";
    }

    // ── Group Grid: kind × capture × form ────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(GridCells))]
    public void Grid_RunsOrIsRefusedByName(string kind, string capture, string form)
    {
        var source = SourceFor(kind, capture, form);
        var result = CompileAndExecute(source);
        var label = $"{kind}/{capture}/{form}";

        if (IsRefused(kind, capture))
        {
            result.Success.Should().BeFalse(
                $"[{label}] a name bound by an or-pattern alternative has no C# lowering. "
                + $"Output was: {result.StandardOutput}\n{source}");

            var errorCodes = result.RawDiagnostics
                .Where(d => d.IsError)
                .Select(d => d.Code)
                .Distinct()
                .ToList();

            errorCodes.Should().Equal(new[] { SPY0359 },
                $"[{label}] must report SPY0359 and nothing else — never the SPY0908 internal "
                + $"error, and no 'undefined identifier' cascade from the body's read. "
                + $"Diagnostics: {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}\n{source}");
        }
        else
        {
            result.Success.Should().BeTrue(
                $"[{label}] must compile and run. "
                + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.TrimEnd().Should().Be(ExpectedOutput(kind, capture),
                $"[{label}] takes the or-pattern arm\n{source}");
        }
    }

    // ── Group Regression: the defect direction, stated as its own fact ───────────────────────

    /// <summary>
    /// The #1920 repro verbatim. Stated separately from the grid so the regression it cures is
    /// legible: at 41dd19de7 this was SPY0125, at 322fbd20e it was SPY0908 (CS8780 + CS0165).
    /// </summary>
    [Fact]
    public void ReifiedHeadsBindingTheSameName_AreRefusedByName_NotAnInternalError()
    {
        var source = @"
def main() -> None:
    o: object = [1, 2]
    match o:
        case list[int](xs) | set[int](xs):
            print(len(xs))
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == SPY0359);
        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0908,
            "a named refusal, never an internal error: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message)));
        result.CompilationErrors.Should().Contain(e => e.Contains("split the alternatives"),
            "the refusal steers to the cure");
    }

    /// <summary>
    /// No capture-bearing or-pattern of ANY alternative kind, in either form, reaches Roslyn. The
    /// positive control for this absence assertion is <see cref="Grid_RunsOrIsRefusedByName"/>'s
    /// capture-absent half: those same alternatives DO reach Roslyn and run.
    /// </summary>
    [Theory]
    [MemberData(nameof(GridCells))]
    public void Grid_NeverReachesRoslyn(string kind, string capture, string form)
    {
        var source = SourceFor(kind, capture, form);
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0908,
            $"[{kind}/{capture}/{form}] SPY0908 is an internal error, never a verdict. "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}\n{source}");
    }

    // ── Group Names: alternatives must bind the SAME name ────────────────────────────────────

    /// <summary>
    /// CPython: "SyntaxError: alternative patterns bind different names". Sharpy bound only the
    /// FIRST name, so the body's read of the second was SPY0200 with nothing naming the rule
    /// (#1920). Now it is the same SPY0359 as every other or-pattern capture violation.
    /// </summary>
    [Fact]
    public void AlternativesBindingDifferentNames_AreRefusedByName()
    {
        var source = @"
def main() -> None:
    x: object = 42
    match x:
        case (int() as a) | (str() as b):
            print(a)
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse("only one of the two names has a C# `var` to bind to");
        result.RawDiagnostics.Where(d => d.IsError).Select(d => d.Code).Distinct()
            .Should().Equal(new[] { SPY0359 },
                string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message)));
        result.CompilationErrors.Should().Contain(e => e.Contains("different names"));
    }

    /// <summary>
    /// The positive control for the test above: the SAME name on every alternative is the shape
    /// the emitter lowers, and it still runs.
    /// </summary>
    [Fact]
    public void AlternativesBindingTheSameName_StillRun()
    {
        var source = @"
def main() -> None:
    x: object = 42
    match x:
        case (int() as v) | (str() as v):
            print(v)
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("42");
    }

    // ── Group Cascade: the refusal is the ONLY diagnostic ────────────────────────────────────

    /// <summary>
    /// A bare name as an alternative — the shape SPY0359 has always covered. The refusal used to
    /// arrive with SPY0200 "undefined identifier" behind it, because the refused capture was never
    /// bound and the body still read it; the reader was pointed at the body instead of the pattern.
    /// The capture is now bound for recovery, so the refusal stands alone (#1920).
    /// </summary>
    [Fact]
    public void BareNameAlternative_IsRefusedByName_WithNoCascadeFromTheBody()
    {
        var source = @"
def main() -> None:
    o: int = 1
    match o:
        case x | y:
            print(x)
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Where(d => d.IsError).Select(d => d.Code).Distinct()
            .Should().Equal(new[] { SPY0359 },
                string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message)));
    }

    /// <summary>
    /// `as` on only SOME alternatives (#1663): the name is unbound when the other alternative
    /// matches. Same code, same single-diagnostic property as every other cell.
    /// </summary>
    [Fact]
    public void AsOnOnlySomeAlternatives_IsRefusedByName_WithNoCascadeFromTheBody()
    {
        var source = @"
def main() -> None:
    x: object = 42
    match x:
        case (int() as n) | str():
            print(n)
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Where(d => d.IsError).Select(d => d.Code).Distinct()
            .Should().Equal(new[] { SPY0359 },
                string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message)));
    }

    // ── Group Non-captures: a bare name that is NOT a capture is untouched ────────────────────

    /// <summary>
    /// The seam fires on what the alternative BINDS, not on what it looks like: a bare name that
    /// resolves to a union variant (#1562) defines nothing, so <c>case Red | Yellow:</c> is not a
    /// capture and still runs. A structural walk over binding patterns would refuse it.
    /// </summary>
    [Fact]
    public void UnitVariantAlternatives_AreNotCaptures_AndStillRun()
    {
        var source = @"
union Signal:
    case Red
    case Yellow
    case Green

def main() -> None:
    s: Signal = Signal.Yellow()
    match s:
        case Red | Yellow:
            print(""stop"")
        case Green:
            print(""go"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("stop");
    }

    /// <summary>
    /// A capture OUTSIDE the or-pattern is unaffected: <c>case 1 | 2 as n:</c> binds the whole
    /// or-pattern (PEP 634 puts <c>as</c> outermost), which lowers as <c>(1 or 2) and var n</c>.
    /// </summary>
    [Fact]
    public void CaptureOutsideTheOrPattern_StillRuns()
    {
        var source = @"
def main() -> None:
    x: int = 2
    match x:
        case 1 | 2 as n:
            print(n)
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("2");
    }

    /// <summary>
    /// An or-pattern NESTED inside a larger pattern keeps the rule local to the alternatives: the
    /// capture sits outside the <c>or</c>, so it lowers and runs.
    /// </summary>
    [Fact]
    public void CaptureBesideANestedOrPattern_StillRuns()
    {
        var source = @"
def main() -> None:
    p: tuple[int, int] = (1, 7)
    match p:
        case (1 | 2, v):
            print(v)
        case _:
            print(""other"")
";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("7");
    }
}
