using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Unpacking position × target shape × source kind × arity matrix (#1692, #1733):
/// the four tuple-unpacking positions {assignment, for, comprehension for-clause, with-as}
/// share one target grammar (list display ≡ tuple via parser normalization), one arity rule
/// (a star absorbs ≥ 0 elements), and one binder/lowering.
///
/// <para>
/// Axes:
/// <list type="bullet">
/// <item><b>position</b> — assignment, for statement, comprehension for-clause, with-as</item>
/// <item><b>target shape</b> — flat tuple, starred first/middle/last, list display (normalized)</item>
/// <item><b>source kind</b> — tuple (fixed arity), list[T] (starred-only; non-starred stays SPY0239)</item>
/// <item><b>arity</b> — exact, star with 0 rest, star with n rest, mismatch (refused SPY0239)</item>
/// </list>
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class UnpackingPositionMatrixTests : IntegrationTestBase
{
    public UnpackingPositionMatrixTests(ITestOutputHelper output) : base(output) { }

    private enum Expect { Runs, SPY0239, SPY0225, KnownRed }

    /// <param name="Expected">
    /// <see cref="Expect.KnownRed"/> is a cell the contract says must RUN and that does not yet:
    /// it asserts the cell is still refused and that <paramref name="Reason"/> names an OPEN issue,
    /// so the entry fails the moment the defect is fixed and has to be deleted (drain on fix). It
    /// never pins a refusal as correct — <see cref="Expect.SPY0239"/> / <see cref="Expect.SPY0225"/>
    /// are for cells whose refusal IS the contract.
    /// </param>
    private sealed record Cell(
        string Label, string Source, Expect Expected, string? ExpectedOutput = null, string? Reason = null);

    [Fact]
    [Trait("Category", "Conformance")]
    public void UnpackingPositionMatrix_AllCellsPass()
    {
        var failures = new List<string>();
        var cells = GenerateCells().ToList();

        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        foreach (var cell in cells)
        {
            var result = CompileAndExecute(cell.Source, executionTimeoutMs: 10_000);

            switch (cell.Expected)
            {
                case Expect.Runs:
                    if (!result.Success)
                    {
                        failures.Add($"{cell.Label}: expected to run but failed: {string.Join("; ", result.CompilationErrors)}");
                        continue;
                    }
                    if (cell.ExpectedOutput != null)
                    {
                        var actual = result.StandardOutput.Trim();
                        if (actual != cell.ExpectedOutput)
                            failures.Add($"{cell.Label}: output mismatch — expected '{cell.ExpectedOutput}', got '{actual}'");
                    }
                    break;

                case Expect.SPY0239:
                    if (result.Success)
                    {
                        failures.Add($"{cell.Label}: expected SPY0239 but compiled and ran");
                        continue;
                    }
                    if (!result.RawDiagnostics.Any(d => d.Code == DiagnosticCodes.Semantic.InvalidTupleUnpacking))
                        failures.Add($"{cell.Label}: expected SPY0239 but got: {string.Join("; ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}");
                    break;

                case Expect.SPY0225:
                    if (result.Success)
                    {
                        failures.Add($"{cell.Label}: expected SPY0225 but compiled and ran");
                        continue;
                    }
                    if (!result.RawDiagnostics.Any(d => d.Code == DiagnosticCodes.Semantic.InvalidAssignmentTarget))
                        failures.Add($"{cell.Label}: expected SPY0225 but got: {string.Join("; ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}");
                    break;

                case Expect.KnownRed:
                    // Drain on fix: the contract says this cell runs. When it starts running the
                    // entry is stale and must be deleted, so a PASS here is the failure.
                    if (result.Success)
                    {
                        failures.Add(
                            $"{cell.Label}: parked as KnownRed ({cell.Reason}) but now compiles and runs — "
                            + "delete the entry and give the cell its real expectation (drain on fix)");
                    }
                    break;
            }
        }

        Output.WriteLine($"Unpacking cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var na in NotApplicableCells())
            Output.WriteLine($"  N/A {na.label}: {na.reason}");
        foreach (var f in failures)
            Output.WriteLine($"  {f}");

        Assert.True(failures.Count == 0,
            $"Unpacking position matrix (#1692, #1733): {failures.Count} of {cells.Count} cells failed.\n" +
            string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// Every parked cell names an issue, so none can be parked on a private opinion and every one
    /// has a place to be deleted from (§8, drain on fix).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void KnownRedCells_CiteAnIssue()
    {
        var parked = GenerateCells().Where(c => c.Expected == Expect.KnownRed).ToList();
        Assert.NotEmpty(parked);
        foreach (var cell in parked)
        {
            Assert.False(string.IsNullOrWhiteSpace(cell.Reason),
                $"Known-red cell '{cell.Label}' has no reason.");
            Assert.Contains("#", cell.Reason!, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Cells that cannot be expressed at all. Empty: the two entries this roster used to carry
    /// were not inexpressible, they were unwritten — a non-starred target over a <c>list[T]</c>
    /// source in the <c>with-as</c> position, and an augmented list-display target. Both are now
    /// cells (<c>with-as.list.non-starred</c>, <c>augmented.list-display</c>), which is what an
    /// N/A entry that describes a real, measurable cell always means (§8, drain on fix).
    /// </summary>
    private static IEnumerable<(string label, string reason)> NotApplicableCells()
        => Enumerable.Empty<(string, string)>();

    private static IEnumerable<Cell> GenerateCells()
    {
        // ── Assignment position ──

        yield return new Cell("assign.tuple.flat.exact",
            "def main() -> None:\n    a, b, c = (1, 2, 3)\n    print(a, b, c)",
            Expect.Runs, "1 2 3");

        yield return new Cell("assign.tuple.starred-last.0-rest",
            "def main() -> None:\n    a, b, c, *rest = (1, 2, 3)\n    print(a, b, c, rest)",
            Expect.Runs, "1 2 3 []");

        yield return new Cell("assign.tuple.starred-last.n-rest",
            "def main() -> None:\n    a, *rest = (1, 2, 3)\n    print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("assign.tuple.starred-first",
            "def main() -> None:\n    *rest, c = (1, 2, 3)\n    print(rest, c)",
            Expect.Runs, "[1, 2] 3");

        yield return new Cell("assign.tuple.starred-middle",
            "def main() -> None:\n    a, *mid, c = (1, 2, 3, 4)\n    print(a, mid, c)",
            Expect.Runs, "1 [2, 3] 4");

        yield return new Cell("assign.tuple.mismatch",
            "def main() -> None:\n    a, b = (1, 2, 3)",
            Expect.SPY0239);

        yield return new Cell("assign.list-display.flat.exact",
            "def main() -> None:\n    [a, b] = (1, 2)\n    print(a, b)",
            Expect.Runs, "1 2");

        yield return new Cell("assign.list.starred",
            "def main() -> None:\n    a, *rest = [1, 2, 3]\n    print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("assign.list.non-starred",
            "def main() -> None:\n    a, b = [1, 2]",
            Expect.SPY0239);

        // ── Assignment position: the starred element in every SPELLING of the group (#1841) ──
        // The `for`/`with-as` positions reach the store-target element parser, which builds a
        // StarExpression; an assignment's left side is parsed by the EXPRESSION parser, which
        // builds a SpreadElement. Both canonicalize to StarExpression at the one store-target seam,
        // so the four spellings below bind exactly as `a, *rest = t` does.

        yield return new Cell("assign.tuple.starred-parenthesized",
            "def main() -> None:\n    (a, *rest) = (1, 2, 3)\n    print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("assign.list.starred-parenthesized",
            "def main() -> None:\n    (a, *rest) = [1, 2, 3]\n    print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("assign.tuple.starred-list-display",
            "def main() -> None:\n    [a, *rest] = (1, 2, 3)\n    print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("assign.list.starred-list-display",
            "def main() -> None:\n    [a, *rest] = [1, 2, 3]\n    print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("assign.tuple.starred-parenthesized-first",
            "def main() -> None:\n    (*rest, c) = (1, 2, 3)\n    print(rest, c)",
            Expect.Runs, "[1, 2] 3");

        // The sole starred element of a group stays refused: `(*a)` (python SyntaxError) and
        // `(*a,)` / `[*a]` (legal python) all parse to the same one-element tuple (#1845).
        yield return new Cell("assign.sole-star-parenthesized",
            "def main() -> None:\n    (*a,) = (1, 2)\n    print(a)",
            Expect.SPY0225);

        yield return new Cell("assign.sole-star-list-display",
            "def main() -> None:\n    [*a] = (1, 2)\n    print(a)",
            Expect.SPY0225);

        // ── For-statement position ──

        yield return new Cell("for.tuple.flat.exact",
            "def main() -> None:\n    for a, b in [(1, 2), (3, 4)]:\n        print(a, b)",
            Expect.Runs, "1 2\n3 4");

        yield return new Cell("for.tuple.starred-last",
            "def main() -> None:\n    for a, *rest in [(1, 2, 3)]:\n        print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("for.tuple.starred-first",
            "def main() -> None:\n    for *rest, c in [(1, 2, 3)]:\n        print(rest, c)",
            Expect.Runs, "[1, 2] 3");

        yield return new Cell("for.tuple.mismatch",
            "def main() -> None:\n    for a, b in [(1, 2, 3)]:\n        print(a, b)",
            Expect.SPY0239);

        yield return new Cell("for.list.starred",
            "def main() -> None:\n    for a, *rest in [[1, 2, 3]]:\n        print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("for.list.non-starred",
            "def main() -> None:\n    for a, b in [[1, 2]]:\n        print(a, b)",
            Expect.SPY0239);

        yield return new Cell("for.list-display-target.flat",
            "def main() -> None:\n    for [a, b] in [(1, 2), (3, 4)]:\n        print(a, b)",
            Expect.Runs, "1 2\n3 4");

        // ── Comprehension for-clause position ──

        yield return new Cell("comp.tuple.flat.exact",
            "def main() -> None:\n    r = [a + b for a, b in [(1, 2), (3, 4)]]\n    print(r)",
            Expect.Runs, "[3, 7]");

        yield return new Cell("comp.tuple.starred-last",
            "def main() -> None:\n    r = [a for a, *rest in [(1, 2, 3)]]\n    print(r)",
            Expect.Runs, "[1]");

        yield return new Cell("comp.tuple.mismatch",
            "def main() -> None:\n    r = [a for a, b in [(1, 2, 3)]]\n    print(r)",
            Expect.SPY0239);

        yield return new Cell("comp.list.starred",
            "def main() -> None:\n    r = [a for a, *rest in [[1, 2, 3]]]\n    print(r)",
            Expect.Runs, "[1]");

        yield return new Cell("comp.list.non-starred",
            "def main() -> None:\n    r = [a + b for a, b in [[1, 2]]]\n    print(r)",
            Expect.SPY0239);

        yield return new Cell("comp.list-display-target",
            "def main() -> None:\n    r = [a + b for [a, b] in [(1, 2), (3, 4)]]\n    print(r)",
            Expect.Runs, "[3, 7]");

        // ── With-as position ──

        yield return new Cell("with-as.tuple.flat",
            @"class CM:
    vals: tuple[int, int]
    def __init__(self, a: int, b: int):
        self.vals = (a, b)
    def __enter__(self) -> tuple[int, int]:
        return self.vals
    def __exit__(self) -> None:
        pass

def main() -> None:
    with CM(5, 6) as (a, b):
        print(a, b)",
            Expect.Runs, "5 6");

        yield return new Cell("with-as.tuple.starred",
            @"class CM:
    vals: tuple[int, int, int]
    def __init__(self, a: int, b: int, c: int):
        self.vals = (a, b, c)
    def __enter__(self) -> tuple[int, int, int]:
        return self.vals
    def __exit__(self) -> None:
        pass

def main() -> None:
    with CM(1, 2, 3) as (a, *rest):
        print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("with-as.tuple.mismatch",
            @"class CM:
    def __enter__(self) -> tuple[int, int, int]:
        return (1, 2, 3)
    def __exit__(self) -> None:
        pass

def main() -> None:
    with CM() as (a, b):
        print(a, b)",
            Expect.SPY0239);

        yield return new Cell("with-as.list-display-target",
            @"class CM:
    vals: tuple[int, int]
    def __init__(self, a: int, b: int):
        self.vals = (a, b)
    def __enter__(self) -> tuple[int, int]:
        return self.vals
    def __exit__(self) -> None:
        pass

def main() -> None:
    with CM(7, 8) as [a, b]:
        print(a, b)",
            Expect.Runs, "7 8");

        // ── Nested target shapes ──

        yield return new Cell("assign.nested-tuple",
            "def main() -> None:\n    (a, (b, c)) = (1, (2, 3))\n    print(a, b, c)",
            Expect.Runs, "1 2 3");

        yield return new Cell("for.nested-tuple",
            "def main() -> None:\n    for a, (b, c) in [(1, (2, 3))]:\n        print(a, b, c)",
            Expect.Runs, "1 2 3");

        // The star absorbs at depth 0 only: the nested arm compares arities with no star
        // awareness, so these four cells are refused where python3 binds (#1846).
        const string Nested1846 =
            "#1846 — the arity rule ignores a star at nested depth (python3 binds all four)";

        yield return new Cell("assign.nested-tuple.starred-inner",
            "def main() -> None:\n    (a, (b, *c)) = (1, (2, 3, 4))\n    print(a, b, c)",
            Expect.KnownRed, Reason: Nested1846);

        yield return new Cell("assign.nested-tuple.starred-outer-element",
            "def main() -> None:\n    (a, *rest), b = ((1, 2, 3), 4)\n    print(a, rest, b)",
            Expect.KnownRed, Reason: Nested1846);

        yield return new Cell("for.nested-tuple.starred-inner",
            "def main() -> None:\n    for a, (b, *c) in [(1, (2, 3, 4))]:\n        print(a, b, c)",
            Expect.KnownRed, Reason: Nested1846);

        yield return new Cell("comp.nested-tuple.starred-inner",
            "def main() -> None:\n    r = [a for a, (b, *c) in [(1, (2, 3, 4))]]\n    print(r)",
            Expect.KnownRed, Reason: Nested1846);

        // Drained from NotApplicableCells: a non-starred target over a list[T] source has no
        // static arity and stays SPY0239 in the with-as position too — one refusal in all four.
        yield return new Cell("with-as.list.non-starred",
            @"class CM:
    def __enter__(self) -> list[int]:
        return [1, 2]
    def __exit__(self) -> None:
        pass

def main() -> None:
    with CM() as (a, b):
        print(a, b)",
            Expect.SPY0239);

        // ── Augmented assignment stays refused ──

        yield return new Cell("augmented.tuple-display",
            "def main() -> None:\n    a: int = 1\n    b: int = 2\n    (a, b) += (3, 4)",
            Expect.SPY0225);

        // Drained from NotApplicableCells: the list-display spelling of the same refusal, so the
        // parser's display normalization cannot accidentally admit one and refuse the other.
        yield return new Cell("augmented.list-display",
            "def main() -> None:\n    a: int = 1\n    b: int = 2\n    [a, b] += [3, 4]",
            Expect.SPY0225);
    }
}
