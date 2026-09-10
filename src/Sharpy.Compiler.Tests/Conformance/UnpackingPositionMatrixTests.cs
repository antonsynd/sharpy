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

    private enum Expect { Runs, SPY0239, SPY0225 }

    private sealed record Cell(string Label, string Source, Expect Expected, string? ExpectedOutput = null);

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

    private static IEnumerable<(string label, string reason)> NotApplicableCells()
    {
        yield return ("non-starred-list-with-as",
            "non-starred target over a list[T] source stays SPY0239 in every position — " +
            "widening to a runtime-checked unpack is a spec decision no ruling made");
        yield return ("augmented-list-display",
            "[a, b] += [1, 2] stays refused (python SyntaxError for both list and tuple)");
        yield return ("with-as-starred",
            "with CM() as (a, *rest) — starred unpacking in with-as target not yet routed through " +
            "ParseForTarget; the parser reads * as multiply; currently SPY0239");
    }

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
            Expect.SPY0239);

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

        // ── Augmented assignment stays refused ──

        yield return new Cell("augmented.tuple-display",
            "def main() -> None:\n    a: int = 1\n    b: int = 2\n    (a, b) += (3, 4)",
            Expect.SPY0225);
    }
}
