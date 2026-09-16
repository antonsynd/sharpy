using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Unpacking position × depth × star position × source kind × group spelling matrix
/// (#1692, #1733, #1845, #1846): the four tuple-unpacking positions {assignment, for,
/// comprehension for-clause, with-as} share ONE target grammar, ONE arity rule ("a star absorbs
/// ≥ 0 elements"), and ONE binder/lowering — at every depth.
///
/// <para>
/// Axes:
/// <list type="bullet">
/// <item><b>position</b> — assignment, for statement, comprehension for-clause, with-as</item>
/// <item><b>depth</b> — 0 (flat), 1 (one nested tuple), 2 (nested-in-nested)</item>
/// <item><b>star position</b> — none, first, middle, last, sole</item>
/// <item><b>source kind</b> — tuple (fixed arity), list[T] (starred-only; non-starred stays SPY0239)</item>
/// <item><b>group spelling</b> — paren+comma <c>(*a,)</c>, list display <c>[*a]</c>,
///   paren-no-comma <c>(*a)</c> (the one spelling Python refuses)</item>
/// </list>
/// Every legal cell's expected output is python3's; every refusal is by DIRECTION (the program
/// python runs is refused only where python refuses it).
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class UnpackingPositionMatrixTests : IntegrationTestBase
{
    public UnpackingPositionMatrixTests(ITestOutputHelper output) : base(output) { }

    private enum Expect { Runs, SPY0239, SPY0225, SPY0356, SPY0227, KnownRed }

    /// <param name="Expected">
    /// <see cref="Expect.KnownRed"/> is a cell the contract says must RUN and that does not yet:
    /// it asserts the cell is still refused and that <paramref name="Reason"/> names an OPEN issue,
    /// so the entry fails the moment the defect is fixed and has to be deleted (drain on fix). It
    /// never pins a refusal as correct — <see cref="Expect.SPY0239"/>, <see cref="Expect.SPY0225"/>
    /// and <see cref="Expect.SPY0356"/> are for cells whose refusal IS the contract.
    /// </param>
    /// <param name="MessageSubstring">
    /// When set, the refusal's diagnostic message must contain it — used to pin the POSITION suffix
    /// on SPY0239 (e.g. "in with statement" for the with-as position, #1846).
    /// </param>
    private sealed record Cell(
        string Label, string Source, Expect Expected,
        string? ExpectedOutput = null, string? Reason = null, string? MessageSubstring = null);

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
                    CheckRefusal(cell, result, DiagnosticCodes.Semantic.InvalidTupleUnpacking, "SPY0239", failures);
                    break;

                case Expect.SPY0225:
                    CheckRefusal(cell, result, DiagnosticCodes.Semantic.InvalidAssignmentTarget, "SPY0225", failures);
                    break;

                case Expect.SPY0356:
                    CheckRefusal(cell, result, DiagnosticCodes.Semantic.MultipleStarExpressions, "SPY0356", failures);
                    break;

                case Expect.SPY0227:
                    CheckRefusal(cell, result, DiagnosticCodes.Semantic.CannotInferType, "SPY0227", failures);
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
        foreach (var f in failures)
            Output.WriteLine($"  {f}");

        Assert.True(failures.Count == 0,
            $"Unpacking position matrix (#1845, #1846): {failures.Count} of {cells.Count} cells failed.\n" +
            string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static void CheckRefusal(
        Cell cell, ExecutionResult result, string code, string name, List<string> failures)
    {
        if (result.Success)
        {
            failures.Add($"{cell.Label}: expected {name} but compiled and ran");
            return;
        }
        if (!result.RawDiagnostics.Any(d => d.Code == code))
        {
            failures.Add($"{cell.Label}: expected {name} but got: "
                + string.Join("; ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
            return;
        }
        if (cell.MessageSubstring != null
            && !result.RawDiagnostics.Any(d => d.Code == code && d.Message.Contains(cell.MessageSubstring)))
        {
            failures.Add($"{cell.Label}: expected {name} message to contain '{cell.MessageSubstring}' but got: "
                + string.Join("; ", result.RawDiagnostics.Where(d => d.Code == code).Select(d => d.Message)));
        }
    }

    /// <summary>
    /// Every parked cell names an issue, so none can be parked on a private opinion and every one
    /// has a place to be deleted from (§8, drain on fix). The roster is allowed to be EMPTY — the
    /// #1846 rows all drained when the one unpacking rule landed; the type stays so a future
    /// parking is possible.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void KnownRedCells_CiteAnIssue()
    {
        var parked = GenerateCells().Where(c => c.Expected == Expect.KnownRed).ToList();
        foreach (var cell in parked)
        {
            Assert.False(string.IsNullOrWhiteSpace(cell.Reason),
                $"Known-red cell '{cell.Label}' has no reason.");
            Assert.Contains("#", cell.Reason!, StringComparison.Ordinal);
        }
    }

    // A context manager whose __enter__ returns the given tuple/list expression of the given type.
    private static string WithSource(string enterType, string enterExpr, string asTarget, string body) =>
        $@"class CM:
    def __enter__(self) -> {enterType}:
        return {enterExpr}
    def __exit__(self) -> None:
        pass

def main() -> None:
    with CM() as {asTarget}:
{body}";

    private static IEnumerable<Cell> GenerateCells()
    {
        // ══ Assignment position ══════════════════════════════════════════════════════════════

        // depth 0, star ∈ {none, last, first, middle}
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

        // The starred element in every SPELLING of the group (#1841): the for/with-as element parser
        // builds a StarExpression, the assignment expression parser a SpreadElement; both canonicalize
        // at the one store-target seam, so all spellings bind as `a, *rest = t` does.
        yield return new Cell("assign.tuple.starred-parenthesized",
            "def main() -> None:\n    (a, *rest) = (1, 2, 3)\n    print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("assign.list.starred-parenthesized",
            "def main() -> None:\n    (a, *rest) = [1, 2, 3]\n    print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("assign.tuple.starred-list-display",
            "def main() -> None:\n    [a, *rest] = (1, 2, 3)\n    print(a, rest)",
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("assign.tuple.starred-parenthesized-first",
            "def main() -> None:\n    (*rest, c) = (1, 2, 3)\n    print(rest, c)",
            Expect.Runs, "[1, 2] 3");

        // ── The sole-starred group, all three spellings (#1845) ──
        // `(*a,)` (paren+comma) and `[*a]` (list display) are legal python and BIND; the bare `(*a)`
        // (paren-no-comma) is a python SyntaxError and is refused SPY0225 "cannot use starred
        // expression here" — the same wording in all four positions.
        yield return new Cell("assign.sole-star.paren-comma",
            "def main() -> None:\n    (*a,) = (1, 2)\n    print(a)",
            Expect.Runs, "[1, 2]");

        yield return new Cell("assign.sole-star.list-display",
            "def main() -> None:\n    [*a] = (1, 2)\n    print(a)",
            Expect.Runs, "[1, 2]");

        yield return new Cell("assign.sole-star.paren-no-comma-refused",
            "def main() -> None:\n    (*a) = (1, 2)\n    print(a)",
            Expect.SPY0225);

        yield return new Cell("assign.sole-star.list-and-tail",
            "def main() -> None:\n    t: tuple[list[int], int] = ([1, 2], 3)\n    [*a], b = t\n    print(a, b)",
            Expect.Runs, "[1, 2] 3");

        // `(*a), b = xs` — the bare paren-no-comma group beside a tail is a python SyntaxError.
        yield return new Cell("assign.paren-no-comma-and-tail-refused",
            "def main() -> None:\n    (*a), b = [1, 2, 3]\n    print(a, b)",
            Expect.SPY0225);

        // ── depth 1: a star at nested depth (#1846) ──
        yield return new Cell("assign.nested.exact",
            "def main() -> None:\n    (a, (b, c)) = (1, (2, 3))\n    print(a, b, c)",
            Expect.Runs, "1 2 3");

        yield return new Cell("assign.nested.starred-inner-last",
            "def main() -> None:\n    (a, (b, *c)) = (1, (2, 3, 4))\n    print(a, b, c)",
            Expect.Runs, "1 2 [3, 4]");

        yield return new Cell("assign.nested.starred-inner-first",
            "def main() -> None:\n    (a, (*b, c)) = (1, (2, 3, 4))\n    print(a, b, c)",
            Expect.Runs, "1 [2, 3] 4");

        yield return new Cell("assign.nested.starred-inner-middle",
            "def main() -> None:\n    (a, (b, *c, d)) = (1, (2, 3, 4, 5))\n    print(a, b, c, d)",
            Expect.Runs, "1 2 [3, 4] 5");

        yield return new Cell("assign.nested.starred-outer-element",
            "def main() -> None:\n    (a, *rest), b = ((1, 2, 3), 4)\n    print(a, rest, b)",
            Expect.Runs, "1 [2, 3] 4");

        // ── a nested arity mismatch is SPY0239 with the "in nested tuple" marker ──
        yield return new Cell("assign.nested.arity-mismatch",
            "def main() -> None:\n    t: tuple[int, tuple[int, int, int]] = (1, (2, 3, 4))\n    a, (b, c) = t\n    print(a)",
            Expect.SPY0239, MessageSubstring: "in nested tuple");

        // ── two stars at the SAME depth → SPY0356, python "multiple starred expressions" ──
        yield return new Cell("assign.two-star.depth0.refused",
            "def main() -> None:\n    a, *b, *c = [1, 2, 3]\n    print(a)",
            Expect.SPY0356);

        yield return new Cell("assign.two-star.depth1.refused",
            "def main() -> None:\n    t: tuple[int, tuple[int, int, int]] = (1, (2, 3, 4))\n    a, (*b, *c) = t\n    print(a)",
            Expect.SPY0356);

        // ── a heterogeneous starred rest has no common element type → Type-Safety refusal (SPY0227),
        // never `list[object]` (R-W arm 3, #1846). Python ACCEPTS this (rest = ["x"]); Sharpy refuses
        // because Type Safety outranks Python Syntax (Axiom precedence) — the .spy fixture documents
        // the divergence. Homogeneous and one-accepts-all rests stay green (the Runs cells above).
        yield return new Cell("assign.hetero-star-rest",
            "def main() -> None:\n    a, *rest = (1, \"x\", 2)\n    print(a, rest)",
            Expect.SPY0227);

        yield return new Cell("for.hetero-star-rest",
            "def main() -> None:\n    xs: list[tuple[int, str, int]] = [(1, \"x\", 2)]\n    for a, *rest in xs:\n        print(a, rest)",
            Expect.SPY0227);

        yield return new Cell("assign.hetero-star-rest.nested",
            "def main() -> None:\n    t: tuple[int, tuple[int, str, int]] = (1, (2, \"x\", 3))\n    a, (b, *c) = t\n    print(a, b, c)",
            Expect.SPY0227);

        // ── depth 2 ──
        yield return new Cell("assign.depth2.starred-innermost",
            "def main() -> None:\n    a, (b, (c, *d)) = (1, (2, (3, 4, 5)))\n    print(a, b, c, d)",
            Expect.Runs, "1 2 3 [4, 5]");

        // ══ For-statement position ═══════════════════════════════════════════════════════════

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

        // sole-starred group in the for position: (*a,) and [*a] bind; bare (*a) refused.
        yield return new Cell("for.sole-star.paren-comma",
            "def main() -> None:\n    for (*a,) in [(1, 2), (3, 4)]:\n        print(a)",
            Expect.Runs, "[1, 2]\n[3, 4]");

        yield return new Cell("for.sole-star.list-display",
            "def main() -> None:\n    for [*a] in [(1, 2), (3, 4)]:\n        print(a)",
            Expect.Runs, "[1, 2]\n[3, 4]");

        yield return new Cell("for.sole-star.paren-no-comma-refused",
            "def main() -> None:\n    for (*a) in [(1, 2)]:\n        print(a)",
            Expect.SPY0225);

        // depth 1 in the for position (#1846)
        yield return new Cell("for.nested.exact",
            "def main() -> None:\n    for a, (b, c) in [(1, (2, 3))]:\n        print(a, b, c)",
            Expect.Runs, "1 2 3");

        yield return new Cell("for.nested.starred-inner",
            "def main() -> None:\n    for a, (b, *c) in [(1, (2, 3, 4))]:\n        print(a, b, c)",
            Expect.Runs, "1 2 [3, 4]");

        // depth 1 over a list[T] element source (#1846): the inner is a list, the star absorbs it.
        yield return new Cell("for.nested.list-source",
            "def main() -> None:\n    xs: list[tuple[int, list[int]]] = [(1, [2, 3, 4])]\n    for a, (b, *c) in xs:\n        print(a, b, c)",
            Expect.Runs, "1 2 [3, 4]");

        // ══ Comprehension for-clause position ════════════════════════════════════════════════

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

        // depth 1 in the comprehension position (#1846)
        yield return new Cell("comp.nested.starred-inner",
            "def main() -> None:\n    r = [a for a, (b, *c) in [(1, (2, 3, 4))]]\n    print(r)",
            Expect.Runs, "[1]");

        // ══ With-as position ═════════════════════════════════════════════════════════════════

        yield return new Cell("with-as.tuple.flat",
            WithSource("tuple[int, int]", "(5, 6)", "(a, b)", "        print(a, b)"),
            Expect.Runs, "5 6");

        yield return new Cell("with-as.tuple.starred",
            WithSource("tuple[int, int, int]", "(1, 2, 3)", "(a, *rest)", "        print(a, rest)"),
            Expect.Runs, "1 [2, 3]");

        yield return new Cell("with-as.tuple.mismatch",
            WithSource("tuple[int, int, int]", "(1, 2, 3)", "(a, b)", "        print(a, b)"),
            // The position suffix must read "in with statement", not "in for loop" (#1846).
            Expect.SPY0239, MessageSubstring: "in with statement");

        yield return new Cell("with-as.list-display-target",
            WithSource("tuple[int, int]", "(7, 8)", "[a, b]", "        print(a, b)"),
            Expect.Runs, "7 8");

        yield return new Cell("with-as.list.non-starred",
            WithSource("list[int]", "[1, 2]", "(a, b)", "        print(a, b)"),
            Expect.SPY0239);

        // depth 1 in the with-as position (#1846) — the nested arity refusal also carries the suffix.
        yield return new Cell("with-as.nested.starred-inner",
            WithSource("tuple[int, tuple[int, int, int]]", "(1, (2, 3, 4))", "(a, (b, *c))",
                "        print(a, b, c)"),
            Expect.Runs, "1 2 [3, 4]");

        yield return new Cell("with-as.nested.mismatch",
            WithSource("tuple[int, tuple[int, int, int]]", "(1, (2, 3, 4))", "(a, (b, c))",
                "        print(a, b, c)"),
            Expect.SPY0239, MessageSubstring: "in with statement");

        // ══ Augmented assignment stays refused, in both display spellings ════════════════════

        yield return new Cell("augmented.tuple-display",
            "def main() -> None:\n    a: int = 1\n    b: int = 2\n    (a, b) += (3, 4)",
            Expect.SPY0225);

        yield return new Cell("augmented.list-display",
            "def main() -> None:\n    a: int = 1\n    b: int = 2\n    [a, b] += [3, 4]",
            Expect.SPY0225);
    }
}
