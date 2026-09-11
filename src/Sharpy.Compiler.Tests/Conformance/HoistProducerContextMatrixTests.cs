using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Hoist producer × context matrix (#1680, #1724, #1725, #1739, #1742):
/// lowering never changes evaluation cardinality, conditionality, timing, order, or scope.
/// Every cell is a program that prints a side-effect trace; the expected output is
/// python3-verified where Python has the construct and hand-derived otherwise.
///
/// <para>
/// Axes:
/// <list type="bullet">
/// <item><b>producer</b> — P1 comprehension, P2 walrus, P3/P4 spread (list/set/dict),
/// P5/P6 tuple spread (literal, call), P7 tuple_iter, P10 ? operator</item>
/// <item><b>context</b> — statement (control), while test, elif, and-rhs, or-rhs,
/// ternary then/else, match guard, lambda body, comprehension element/condition,
/// assert message, argument-after-side-effect, return, f-string hole</item>
/// </list>
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class HoistProducerContextMatrixTests : IntegrationTestBase
{
    public HoistProducerContextMatrixTests(ITestOutputHelper output) : base(output) { }

    private sealed record Cell(string Label, string Source, string ExpectedOutput);

    /// <summary>
    /// A cell whose program must be REFUSED, by the named diagnostic code. Separate from
    /// <see cref="Cell"/> because a refusal is not an output: asserting these as "runs and prints X"
    /// is how a silently-wrong value or an SPY0908 gets recorded as a pass.
    /// </summary>
    private sealed record RefusedCell(string Label, string Source, string ExpectedCode);

    [Fact]
    [Trait("Category", "Conformance")]
    public void HoistProducerContextMatrix_AllCellsPass()
    {
        var failures = new List<string>();
        var cells = GenerateCells().ToList();

        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        foreach (var cell in cells)
        {
            var result = CompileAndExecute(cell.Source, executionTimeoutMs: 15_000);

            if (!result.Success)
            {
                failures.Add($"{cell.Label}: failed to compile/run: {string.Join("; ", result.CompilationErrors)}");
                continue;
            }

            var actual = result.StandardOutput.Trim();
            if (actual != cell.ExpectedOutput)
                failures.Add($"{cell.Label}: output mismatch — expected '{cell.ExpectedOutput}', got '{actual}'");
        }

        var refused = RefusedCells().ToList();
        Assert.Equal(refused.Count, refused.Select(c => c.Label).Distinct().Count());

        foreach (var cell in refused)
        {
            var result = CompileAndExecute(cell.Source, executionTimeoutMs: 15_000);

            if (result.Success)
            {
                failures.Add($"{cell.Label}: expected {cell.ExpectedCode}, but the program compiled "
                    + $"and ran (stdout '{result.StandardOutput.Trim()}')");
                continue;
            }

            // Assert on the CODE, not the message: a message match would pass on a renamed code and
            // a code match survives any wording change (RawDiagnostics carries both).
            var codes = result.RawDiagnostics.Select(d => d.Code).Where(c => c != null).ToList();
            if (!codes.Contains(cell.ExpectedCode, StringComparer.Ordinal))
            {
                failures.Add($"{cell.Label}: expected {cell.ExpectedCode}, got codes "
                    + $"[{string.Join(", ", codes)}] / {string.Join("; ", result.CompilationErrors)}");
            }
        }

        Output.WriteLine($"Hoist cells: {cells.Count}  Refused cells: {refused.Count}  Failures: {failures.Count}");
        foreach (var na in NotApplicableCells())
            Output.WriteLine($"  N/A {na.label}: {na.reason}");
        foreach (var kr in KnownRedCells())
            Output.WriteLine($"  KNOWN-RED {kr.label}: {kr.reason}");
        foreach (var f in failures)
            Output.WriteLine($"  {f}");

        Assert.True(failures.Count == 0,
            $"Hoist producer × context matrix (#1680, #1739): {failures.Count} of "
            + $"{cells.Count + refused.Count} cells failed.\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// Every AST operand LIST whose elements are evaluated left to right, as a literal roster. This
    /// is the ordering axis's own totality check: the fix that started it integrated
    /// <c>GenerateExpressionsInOrder</c> into call argument generation ONLY, so the class stayed
    /// open for binary operands, display elements and comparison-chain operands. A position added
    /// to the language must arrive with its cell.
    /// </summary>
    private static readonly string[] OperandListPositions =
    {
        "call-arguments",
        "binary-operands",
        "list-elements",
        "tuple-elements",
        "dict-entries",
        "set-elements",
        "comparison-chain-operand",
        "with-items",
        "slice-bounds",
    };

    [Fact]
    [Trait("Category", "Conformance")]
    public void EveryOperandListPosition_HasAnOrderingCell()
    {
        Assert.Equal(9, OperandListPositions.Length);

        var labels = GenerateCells().Select(c => c.Label).ToList();
        var missing = OperandListPositions
            .Where(position => !labels.Any(l => l.Contains(position, StringComparison.Ordinal)))
            .ToList();

        Assert.True(missing.Count == 0,
            "Operand-list positions with no ordering cell (the ordering axis is not total): "
            + string.Join(", ", missing));
    }

    /// <summary>
    /// Totality pin. The counts are LITERALS, not derived from the generators — a pin that recounts
    /// the same source it guards cannot notice a cell being deleted. Raise them deliberately when a
    /// cell is added, and never lower one without saying which cell went away and why.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void Matrix_HasThePinnedCellCounts()
    {
        Assert.Equal(53, GenerateCells().Count());
        Assert.Equal(9, RefusedCells().Count());
        Assert.Equal(2, NotApplicableCells().Count());
    }

    /// <summary>
    /// The cells that must be REFUSED by name. Two are the definite-assignment rule reading a
    /// walrus that a conditional construct may never have evaluated (python3 raises
    /// UnboundLocalError on both, and both printed a silently wrong value at 311252e33); the third
    /// is `?` inside a lambda body, which the spec documents as SPY0462 and which was SPY0908
    /// (CS0029) before.
    /// </summary>
    private static IEnumerable<RefusedCell> RefusedCells()
    {
        yield return new RefusedCell("DA.walrus-ternary-arm-read-after",
            @"def f() -> int:
    return 3

def main() -> None:
    c: bool = False
    w: int
    v: int = (w := f()) if c else 0
    print(v, w)",
            "SPY0600");

        yield return new RefusedCell("DA.walrus-comparison-chain-read-after",
            @"def f() -> int:
    return 3

def main() -> None:
    w: int
    if 5 < 1 < (w := f()):
        print(""in"")
    print(""after"", w)",
            "SPY0600");

        // The constant-condition axis's other six cells: the walrus sits in the arm the constant
        // makes UNREACHABLE, so python3 leaves it unbound (UnboundLocalError) and SPY0600 is right.
        // Without these, the constant rules could be "fixed" by crediting every arm unconditionally.
        yield return new RefusedCell("DA.const-false-ternary-condition",
            @"def f() -> int:
    return 3

def main() -> None:
    w: int
    v: int = (w := f()) if False else 0
    print(v, w)",
            "SPY0600");

        yield return new RefusedCell("DA.const-not-true-ternary-condition",
            @"def f() -> int:
    return 3

def main() -> None:
    w: int
    v: int = (w := f()) if not True else 0
    print(v, w)",
            "SPY0600");

        yield return new RefusedCell("DA.const-false-and-lhs",
            @"def f() -> int:
    return 3

def main() -> None:
    w: int
    if False and (w := f()) > 0:
        pass
    print(""after"", w)",
            "SPY0600");

        yield return new RefusedCell("DA.const-not-true-and-lhs",
            @"def f() -> int:
    return 3

def main() -> None:
    w: int
    if not True and (w := f()) > 0:
        pass
    print(""after"", w)",
            "SPY0600");

        yield return new RefusedCell("DA.const-true-or-lhs",
            @"def f() -> int:
    return 3

def main() -> None:
    w: int
    if True or (w := f()) > 0:
        pass
    print(""after"", w)",
            "SPY0600");

        yield return new RefusedCell("DA.const-not-false-or-lhs",
            @"def f() -> int:
    return 3

def main() -> None:
    w: int
    if not False or (w := f()) > 0:
        pass
    print(""after"", w)",
            "SPY0600");

        yield return new RefusedCell("P10.lambda-body",
            @"def ok() -> Result[int, str]:
    return Ok(5)

def use() -> Result[int, str]:
    g = lambda: ok()? + 1
    return Ok(g())

def main() -> None:
    print(use())",
            "SPY0462");
    }

    /// <summary>
    /// Cells that are a live defect of an adjacent class, parked with their issue rather than
    /// asserted — asserting the current behaviour would PIN it (CLAUDE.md Rule 1), and asserting the
    /// correct behaviour would leave a red in the suite. Each reason names its issue and the entry is
    /// deleted in the commit that fixes it. <see cref="KnownRedCellReasons_CiteAnIssue"/> keeps the
    /// citation honest.
    /// </summary>
    private static IEnumerable<(string label, string reason)> KnownRedCells()
    {
        yield return ("with.suppression-capable-body-assigns-bare-local",
            "#1839 — a bare local assigned UNCONDITIONALLY in a suppression-capable with body and "
            + "read after the with is SPY0600. The suppression edge leaves from the with-body ENTRY "
            + "block, so a body with no raise is treated as possibly aborting before its first "
            + "statement. Ran at 5bac4cf71 and in python3 (prints 5); an owner ruling is pending on "
            + "whether to model the edge per raise-capable statement or adopt the C# CS0165 reading.");

    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void KnownRedCellReasons_CiteAnIssue()
    {
        foreach (var (label, reason) in KnownRedCells())
        {
            Assert.Contains("#", reason, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(reason), $"Known-red cell '{label}' has no reason.");
        }
    }

    private static IEnumerable<(string label, string reason)> NotApplicableCells()
    {
        yield return ("P8-lambda-with-defaults×while",
            "a lambda-with-defaults inside a while test is not expressible — the lambda " +
            "is declared once, not per-iteration");
        yield return ("P9-partial-arg×while",
            "partial application in a while test would require functools import — " +
            "the ordering axis covers the argument-capture mechanic");
        // P10 × lambda is NOT not-applicable: `?` in a lambda body is a refused cell, asserted by
        // RefusedCells below. The old reason was false twice over — it was not refused at all, and
        // SPY0704 is WalrusInProhibitedPosition, a different code.
    }

    private static IEnumerable<Cell> GenerateCells()
    {
        // ══════════════════════════════════════════════════════════════════════════
        // P1: Comprehension — the hoist is the imperative loop lowering
        // ══════════════════════════════════════════════════════════════════════════

        yield return new Cell("P1.statement",
            "def main() -> None:\n    r = [x * 2 for x in range(3)]\n    print(r)",
            "[0, 2, 4]");

        yield return new Cell("P1.while-test",
            @"def main() -> None:
    xs: list[int] = [3, 2, 1, 0]
    while len([v for v in xs]) > 0:
        xs.pop(0)
        print(""iter"", len(xs))
    print(""done"", len(xs))",
            "iter 3\niter 2\niter 1\niter 0\ndone 0");

        // `if True and …` evaluates the rhs on both the sunk and the unsunk lowering, so it cannot
        // discriminate. `if False and …` can: with no sink the comprehension runs anyway and its
        // side effect shows. Same for or-rhs below (`if True or …`) and the ternary (untaken arm).
        yield return new Cell("P1.and-rhs",
            @"def take(xs: list[int]) -> list[int]:
    print(""evaluated"")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    if False and len([v for v in take(xs)]) > 0:
        print(""yes"")
    print(""left"", len(xs))",
            "left 3");

        yield return new Cell("P1.or-rhs",
            @"def take(xs: list[int]) -> list[int]:
    print(""evaluated"")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    if True or len([v for v in take(xs)]) > 0:
        print(""taken"")
    print(""left"", len(xs))",
            "taken\nleft 3");

        yield return new Cell("P1.ternary",
            @"def take(xs: list[int]) -> list[int]:
    print(""evaluated"")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    empty: list[int] = []
    r = [v for v in take(xs)] if False else empty
    print(len(r))
    print(""left"", len(xs))",
            "0\nleft 3");

        yield return new Cell("P1.lambda-body",
            @"def main() -> None:
    f = lambda: [x * 2 for x in range(3)]
    print(f())",
            "[0, 2, 4]");

        yield return new Cell("P1.fstring-hole",
            @"def main() -> None:
    print(f""{[x for x in range(3)]}"")",
            "[0, 1, 2]");

        yield return new Cell("P1.return",
            @"def make() -> list[int]:
    return [x * 2 for x in range(3)]

def main() -> None:
    print(make())",
            "[0, 2, 4]");

        // ══════════════════════════════════════════════════════════════════════════
        // P2: Walrus — the hoist is the pre-declaration + inline assignment
        // ══════════════════════════════════════════════════════════════════════════

        yield return new Cell("P2.statement",
            @"def main() -> None:
    if (n := 42) > 0:
        print(n)",
            "42");

        yield return new Cell("P2.and-rhs",
            @"def probe() -> int:
    print(""evaluated"")
    return 5

def main() -> None:
    if True and (w := probe()) > 0:
        print(""yes"", w)
    print(""done"")",
            "evaluated\nyes 5\ndone");

        yield return new Cell("P2.and-rhs-shortcircuited",
            @"def probe() -> int:
    print(""evaluated"")
    return 5

def main() -> None:
    if False and (w := probe()) > 0:
        print(""yes"")
    print(""done"")",
            "done");

        yield return new Cell("P2.or-rhs",
            @"def probe() -> int:
    print(""evaluated"")
    return 5

def main() -> None:
    r: bool = False or (w := probe()) > 0
    print(""done"")",
            "evaluated\ndone");

        yield return new Cell("P2.elif",
            @"def probe() -> int:
    print(""evaluated"")
    return 5

def main() -> None:
    if False:
        pass
    elif (w := probe()) > 0:
        print(""elif"", w)
    print(""done"")",
            "evaluated\nelif 5\ndone");

        yield return new Cell("P2.ternary-then",
            @"def probe() -> int:
    print(""evaluated"")
    return 5

def main() -> None:
    r = (w := probe()) if True else 0
    print(r)",
            "evaluated\n5");

        yield return new Cell("P2.ternary-else-not-taken",
            @"def probe() -> int:
    print(""evaluated"")
    return 5

def main() -> None:
    r = 0 if True else (w := probe())
    print(r)",
            "0");

        yield return new Cell("P2.lambda-body",
            @"def main() -> None:
    f = lambda: (w := 10)
    print(f())",
            "10");

        yield return new Cell("P2.comp-element",
            @"def main() -> None:
    r = [(w := x) * 2 for x in range(3)]
    print(r)",
            "[0, 2, 4]");

        yield return new Cell("P2.comp-condition",
            @"def main() -> None:
    r = [x for x in range(5) if (w := x) > 2]
    print(r)",
            "[3, 4]");

        yield return new Cell("P2.fstring-hole",
            @"def main() -> None:
    print(f""{(w := 42)}"")",
            "42");

        // ══════════════════════════════════════════════════════════════════════════
        // P3/P4: List/set/dict spread
        // ══════════════════════════════════════════════════════════════════════════

        yield return new Cell("P3.statement",
            @"def main() -> None:
    xs: list[int] = [1, 2]
    r = [0, *xs, 3]
    print(r)",
            "[0, 1, 2, 3]");

        yield return new Cell("P3.and-rhs",
            @"def make() -> list[int]:
    print(""spread"")
    return [1, 2]

def main() -> None:
    if False and len([0, *make()]) > 0:
        print(""yes"")
    print(""done"")",
            "done");

        yield return new Cell("P3.lambda-body",
            @"def main() -> None:
    xs: list[int] = [1, 2]
    f = lambda: [0, *xs, 3]
    print(f())",
            "[0, 1, 2, 3]");

        yield return new Cell("P4.dict-spread.statement",
            @"def main() -> None:
    d: dict[str, int] = {""a"": 1}
    r = {**d, ""b"": 2}
    print(len(r))",
            "2");

        // ══════════════════════════════════════════════════════════════════════════
        // P5/P6: Tuple spread (literal, call)
        // ══════════════════════════════════════════════════════════════════════════

        yield return new Cell("P5.statement",
            @"def main() -> None:
    xs: tuple[int, int] = (1, 2)
    r = (0, *xs, 3)
    print(r)",
            "(0, 1, 2, 3)");

        yield return new Cell("P5.and-rhs",
            @"def make() -> tuple[int, int]:
    print(""spread"")
    return (1, 2)

def main() -> None:
    if False and len((0, *make())) > 0:
        print(""yes"")
    print(""done"")",
            "done");

        // ══════════════════════════════════════════════════════════════════════════
        // P10: ? operator — early return lowering
        // ══════════════════════════════════════════════════════════════════════════

        yield return new Cell("P10.statement",
            @"def parse(s: str) -> int !str:
    if s == ""ok"":
        return Ok(42)
    return Err(""bad"")

def run() -> int !str:
    v: int = parse(""ok"")?
    return Ok(v + 1)

def main() -> None:
    match run():
        case Ok(v):
            print(v)
        case Err(e):
            print(e)",
            "43");

        yield return new Cell("P10.and-rhs",
            @"def parse(s: str) -> int !str:
    if s == ""ok"":
        return Ok(42)
    return Err(""bad"")

def run(flag: bool) -> int !str:
    if flag and parse(""ok"")? > 0:
        return Ok(1)
    return Ok(0)

def main() -> None:
    match run(True):
        case Ok(v):
            print(v)
        case Err(e):
            print(e)",
            "1");

        yield return new Cell("P10.ternary",
            @"def parse(s: str) -> int !str:
    if s == ""ok"":
        return Ok(42)
    return Err(""bad"")

def run(flag: bool) -> int !str:
    v: int = parse(""ok"")? if flag else 0
    return Ok(v)

def main() -> None:
    match run(True):
        case Ok(v):
            print(v)
        case Err(e):
            print(e)",
            "42");

        // ══════════════════════════════════════════════════════════════════════════
        // Ordering axis: argument-after-side-effect
        // ══════════════════════════════════════════════════════════════════════════

        yield return new Cell("ordering.call-arguments-after-side-effect",
            @"def f(a: int, b: list[int]) -> None:
    print(a, b)

def main() -> None:
    xs: list[int] = [1, 2, 3]
    f(xs.pop(0), [v for v in xs])",
            "1 [2, 3]");

        // The comprehension must iterate an iterable the EARLIER operand mutates, or hoisting it
        // above that operand changes nothing and the cell is vacuous. python3: 1 + 2 == 3.
        yield return new Cell("ordering.binary-operands",
            @"def main() -> None:
    xs: list[int] = [1, 2, 3]
    r: int = xs.pop(0) + len([x for x in xs])
    print(r)
    print(""left"", len(xs))",
            "3\nleft 2");

        // A REAL list display: the label named a context the old source never constructed (it was
        // two separate statements, each already at its own boundary). python3: [1, 2].
        yield return new Cell("ordering.list-elements",
            @"def main() -> None:
    xs: list[int] = [1, 2, 3]
    t: list[int] = [xs.pop(0), len([v for v in xs])]
    print(t)
    print(""left"", len(xs))",
            "[1, 2]\nleft 2");

        yield return new Cell("ordering.tuple-elements",
            @"def main() -> None:
    xs: list[int] = [1, 2, 3]
    t: tuple[int, int] = (xs.pop(0), len([v for v in xs]))
    print(t[0], t[1])",
            "1 2");

        yield return new Cell("ordering.dict-entries",
            @"def main() -> None:
    xs: list[int] = [1, 2, 3]
    d: dict[int, int] = {xs.pop(0): len([v for v in xs])}
    print(d)",
            "{1: 2}");

        // Slice bounds are an operand list too — the position that stayed unordered after call
        // arguments, binary operands and displays were fixed (#1849). `lo` pops before the upper
        // bound is taken, so the slice is ys[0:3]; python3 prints [10, 20, 30].
        yield return new Cell("ordering.slice-bounds",
            @"def lo(xs: list[int]) -> int:
    xs.pop(0)
    return 0

def main() -> None:
    xs: list[int] = [1, 2, 3, 4]
    ys: list[int] = [10, 20, 30, 40]
    print(ys[lo(xs) : len([v for v in xs])])",
            "[10, 20, 30]");

        // Drained from KnownRedCells: `??=` now gives its right-hand side an evaluation sink, so
        // the rhs's hoists run only on the absent path (#1835). The present arm is the subject;
        // the absent arm is its positive control — the rhs MUST run there.
        yield return new Cell("coalesce-assign.rhs-hoist",
            @"def mark(xs: list[int]) -> list[int]:
    print(""rhs ran"")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    present: int? = Some(5)
    present ??= len([v for v in mark(xs)])
    print(present, len(xs))
    absent: int? = None()
    absent ??= len([v for v in mark(xs)])
    print(absent, len(xs))",
            "5 3\nrhs ran\n2 2");

        yield return new Cell("ordering.set-elements",
            @"def main() -> None:
    xs: list[int] = [1, 2, 3]
    s: set[int] = {xs.pop(0), len([v for v in xs])}
    print(sorted(s))",
            "[1, 2]");

        // ══════════════════════════════════════════════════════════════════════════
        // The six contexts P3 left unsunk, plus the `??` sibling this harness found
        // ══════════════════════════════════════════════════════════════════════════

        yield return new Cell("P1.comparison-chain-operand",
            @"def take(xs: list[int]) -> list[int]:
    print(""evaluated"")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    if 5 < 1 < len([v for v in take(xs)]):
        print(""yes"")
    print(""left"", len(xs))",
            "left 3");

        yield return new Cell("P1.comparison-chain-operand-taken",
            @"def take(xs: list[int]) -> list[int]:
    print(""evaluated"")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    if 1 < 5 < len([v for v in take(xs)]):
        print(""yes"")
    print(""left"", len(xs))",
            "evaluated\nleft 2");

        yield return new Cell("P1.match-statement-guard",
            @"def take(xs: list[int]) -> list[int]:
    print(""guard-eval"")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    m: int = 1
    match m:
        case 0 if len([v for v in take(xs)]) > 0:
            print(""zero"")
        case _:
            print(""other"")
    print(""left"", len(xs))",
            "other\nleft 3");

        yield return new Cell("P1.match-expression-guard",
            @"def take(xs: list[int]) -> list[int]:
    print(""guard-eval"")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    m: int = 1
    label: str = match m:
        case 0 if len([v for v in take(xs)]) > 0: ""zero""
        case _: ""other""
    print(label)
    print(""left"", len(xs))",
            "other\nleft 3");

        yield return new Cell("P1.match-expression-arm-result",
            @"def take(xs: list[int]) -> list[int]:
    print(""result-eval"")
    xs.pop(0)
    return xs

def main() -> None:
    xs: list[int] = [1, 2, 3]
    m: int = 1
    got: list[int] = match m:
        case 0: [v for v in take(xs)]
        case _: [10]
    print(got)
    print(""left"", len(xs))",
            "[10]\nleft 3");

        yield return new Cell("P1.with-items-context-order",
            @"class Appender:
    label: str
    src: list[int]

    def __init__(self, label: str, src: list[int]) -> None:
        self.label = label
        self.src = src

    def __enter__(self) -> str:
        print(""enter"", self.label)
        self.src.append(9)
        return self.label

    def __exit__(self) -> None:
        pass

class Counter:
    seen: list[int]

    def __init__(self, seen: list[int]) -> None:
        self.seen = seen

    def __enter__(self) -> int:
        return len(self.seen)

    def __exit__(self) -> None:
        pass

def main() -> None:
    src: list[int] = []
    with Appender(""a"", src) as first, Counter([v for v in src]) as count:
        print(""body"", count)",
            "enter a\nbody 1");

        yield return new Cell("P1.coalesce-rhs",
            @"def take(xs: list[int]) -> list[int]:
    print(""evaluated"")
    xs.pop(0)
    return xs

def find(n: int) -> str | None:
    if n > 0:
        return ""hit""
    return None

def main() -> None:
    xs: list[int] = [1, 2, 3]
    v: str = find(1) ?? str(len([q for q in take(xs)]))
    print(v)
    print(""left"", len(xs))",
            "hit\nleft 3");

        yield return new Cell("P1.coalesce-rhs-taken",
            @"def take(xs: list[int]) -> list[int]:
    print(""evaluated"")
    xs.pop(0)
    return xs

def find(n: int) -> str | None:
    if n > 0:
        return ""hit""
    return None

def main() -> None:
    xs: list[int] = [1, 2, 3]
    v: str = find(0) ?? str(len([q for q in take(xs)]))
    print(v)
    print(""left"", len(xs))",
            "evaluated\n2\nleft 2");

        yield return new Cell("P2.match-statement-guard-walrus",
            @"def f() -> int:
    print(""f"")
    return 3

def main() -> None:
    m: int = 1
    match m:
        case 1 if (w := f()) > 0:
            print(""one"", w)
        case _:
            print(""other"")",
            "f\none 3");

        // ══════════════════════════════════════════════════════════════════════════
        // Constant-condition axis (C# §9.4.4's constant rules for ?:, && and ||)
        //
        // When the deciding operand is a CONSTANT, one outcome is unreachable and the general
        // when-true/when-false formula still intersects against it, so a walrus in the arm that
        // ALWAYS runs was reported unassigned. Each cell's expectation is python3's: the
        // always-taken arm binds the walrus (these four RUN), the never-taken arm leaves it unbound
        // (those six are in RefusedCells).
        // ══════════════════════════════════════════════════════════════════════════

        yield return new Cell("DA.const-true-ternary-condition",
            @"def f() -> int:
    print(""f"")
    return 3

def main() -> None:
    w: int
    v: int = (w := f()) if True else 0
    print(v, w)",
            "f\n3 3");

        yield return new Cell("DA.const-not-false-ternary-condition",
            @"def f() -> int:
    print(""f"")
    return 3

def main() -> None:
    w: int
    v: int = (w := f()) if not False else 0
    print(v, w)",
            "f\n3 3");

        yield return new Cell("DA.const-true-and-lhs",
            @"def f() -> int:
    print(""f"")
    return 3

def main() -> None:
    w: int
    if True and (w := f()) > 0:
        pass
    print(""after"", w)",
            "f\nafter 3");

        yield return new Cell("DA.const-false-or-lhs",
            @"def f() -> int:
    print(""f"")
    return 3

def main() -> None:
    w: int
    if False or (w := f()) > 0:
        pass
    print(""after"", w)",
            "f\nafter 3");

        // ══════════════════════════════════════════════════════════════════════════
        // Walrus DA cells — positive controls for the when-true/when-false rule
        // ══════════════════════════════════════════════════════════════════════════

        yield return new Cell("DA.walrus-and-rhs-read-inside",
            @"def f() -> int:
    return 5

def main() -> None:
    xs: list[int] = [1, 2, 3]
    if (n := len(xs)) > 0 and n < 10:
        print(n)",
            "3");

        yield return new Cell("DA.walrus-unconditional-read-after-and",
            @"def f() -> int:
    return 1

def main() -> None:
    if (w := f()) > 0 and True:
        pass
    else:
        print(""else"", w)",
            "");

        yield return new Cell("DA.walrus-or-rhs-read-inside",
            @"def main() -> None:
    xs: list[int] = []
    if len(xs) > 0 or (w := 5) > 0:
        pass
    print(""done"")",
            "done");

        yield return new Cell("DA.walrus-elif",
            @"def main() -> None:
    if False:
        pass
    elif (w := 42) > 0:
        print(w)",
            "42");
    }
}
