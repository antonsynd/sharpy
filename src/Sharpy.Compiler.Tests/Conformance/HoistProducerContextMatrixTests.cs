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

        Output.WriteLine($"Hoist cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var na in NotApplicableCells())
            Output.WriteLine($"  N/A {na.label}: {na.reason}");
        foreach (var f in failures)
            Output.WriteLine($"  {f}");

        Assert.True(failures.Count == 0,
            $"Hoist producer × context matrix (#1680, #1739): {failures.Count} of {cells.Count} cells failed.\n" +
            string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static IEnumerable<(string label, string reason)> NotApplicableCells()
    {
        yield return ("P8-lambda-with-defaults×while",
            "a lambda-with-defaults inside a while test is not expressible — the lambda " +
            "is declared once, not per-iteration");
        yield return ("P9-partial-arg×while",
            "partial application in a while test would require functools import — " +
            "the ordering axis covers the argument-capture mechanic");
        yield return ("P10-?×lambda",
            "? inside a lambda body is refused by name (SPY0704) — Design Decision 8");
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

        yield return new Cell("P1.and-rhs",
            @"def main() -> None:
    xs: list[int] = [1, 2, 3]
    if True and len([v for v in xs]) > 0:
        print(""yes"", len(xs))",
            "yes 3");

        yield return new Cell("P1.or-rhs",
            @"def main() -> None:
    xs: list[int] = [1, 2]
    if False or len([v for v in xs]) > 0:
        print(""taken"", len(xs))",
            "taken 2");

        yield return new Cell("P1.ternary",
            @"def main() -> None:
    xs: list[int] = [1, 2, 3]
    empty: list[int] = []
    r = [v for v in xs] if True else empty
    print(len(r))",
            "3");

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
    if True and len([0, *make()]) > 0:
        print(""yes"")",
            "spread\nyes");

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
    if True and len((0, *make())) > 0:
        print(""yes"")",
            "spread\nyes");

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

        yield return new Cell("ordering.args-after-side-effect",
            @"def f(a: int, b: list[int]) -> None:
    print(a, b)

def main() -> None:
    xs: list[int] = [1, 2, 3]
    f(xs.pop(0), [v for v in xs])",
            "1 [2, 3]");

        yield return new Cell("ordering.binary-operands",
            @"def side() -> int:
    print(""left"")
    return 1

def main() -> None:
    r = side() + len([x for x in range(3)])
    print(r)",
            "left\n4");

        yield return new Cell("ordering.list-elements",
            @"def main() -> None:
    xs: list[int] = [1, 2, 3]
    a: int = xs.pop(0)
    b = [v for v in xs]
    print(a, b)",
            "1 [2, 3]");

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
