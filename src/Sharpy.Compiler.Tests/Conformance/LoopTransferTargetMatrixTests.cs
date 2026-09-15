using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Loop-transfer binding matrix (#1816): a <c>break</c>/<c>continue</c> targets the innermost
/// enclosing Python loop, and the loop's <c>else</c> flag runs exactly when Python's does, through
/// EVERY statement kind the emitter lowers between the transfer and the loop. Every cell is a program
/// that prints an iteration trace; the expected output is python3-verified (python3 3.12) and pinned
/// as a string constant, with the program body itself being the Python equivalent (identical
/// control-flow syntax; the <c>with</c> cells add a no-op context manager both sides).
///
/// <para>Axes: transfer {break, continue} × loop {for, while, for-else, while-else, nested
/// inner/outer} × host between the transfer and the loop {none/if, match plain arm, match guarded
/// (non-hoisting / hoisting), match nested-if, match-in-match, try/finally, with, try inside match,
/// exhaustive bool match}. The bug: a C# <c>switch</c> manufactured for a <c>match</c> captured a
/// bare <c>break</c> (0 1 3 done), and the loop-else flag was rewritten only under <c>if</c>
/// (0 1 else done through try/with/match). Both are now driven by the recorded LoopTransferTarget.</para>
///
/// <para>The <c>case 2 | 3</c> shape is excluded: at HEAD it printed <c>0 1 done</c> by coincidence
/// (the arm re-catches 3), so it does not discriminate the fix.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class LoopTransferTargetMatrixTests : IntegrationTestBase
{
    public LoopTransferTargetMatrixTests(ITestOutputHelper output) : base(output) { }

    private sealed record Cell(string Label, string Source, string ExpectedOutput);

    [Fact]
    [Trait("Category", "Conformance")]
    public void LoopTransferTargetMatrix_AllCellsPass()
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

            var actual = result.StandardOutput.Trim().Replace("\r\n", "\n").Replace("\n", " ");
            while (actual.Contains("  "))
                actual = actual.Replace("  ", " ");
            if (actual != cell.ExpectedOutput)
                failures.Add($"{cell.Label}: output mismatch — expected '{cell.ExpectedOutput}', got '{actual}'");
        }

        // Host-axis totality anchor: every host kind the emitter can lower a transfer through has at
        // least one break cell. A host added to the language must arrive with its cell.
        var breakHosts = new[]
        {
            "brk.for.if", "brk.for.match_plain", "brk.for.match_guard_nonhoist",
            "brk.for.match_guard_hoist", "brk.for.match_nested_if", "brk.for.match_in_match",
            "brk.for.try_finally", "brk.for.try_in_match", "brk.for.with", "brk.for.bool_match",
        };
        var labels = cells.Select(c => c.Label).ToHashSet();
        var missingHost = breakHosts.Where(h => !labels.Contains(h)).ToList();

        Output.WriteLine($"Loop-transfer cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var f in failures)
            Output.WriteLine($"  {f}");

        Assert.True(missingHost.Count == 0,
            "host kinds with no break cell (the host axis is not total): " + string.Join(", ", missingHost));
        Assert.True(failures.Count == 0,
            $"Loop-transfer binding matrix (#1816): {failures.Count} of {cells.Count} cells failed.\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static IEnumerable<Cell> GenerateCells()
    {
        yield return new Cell(
            "brk.for.if",
            @"def main() -> None:
    for i in range(4):
        if i == 2:
            break
        print(i)
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.for.match_plain",
            @"def main() -> None:
    for i in range(4):
        match i:
            case 2:
                break
            case _:
                print(i)
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.for.match_guard_nonhoist",
            @"def main() -> None:
    for i in range(4):
        match i:
            case x if x == 2:
                break
            case _:
                print(i)
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.for.match_guard_hoist",
            @"def main() -> None:
    for i in range(4):
        match i:
            case x if x in [v for v in [2]]:
                break
            case _:
                print(i)
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.for.match_nested_if",
            @"def main() -> None:
    for i in range(4):
        match i:
            case 2:
                if True:
                    break
            case _:
                print(i)
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.for.match_in_match",
            @"def main() -> None:
    for i in range(4):
        match i:
            case 2:
                match i:
                    case 2:
                        break
                    case _:
                        pass
            case _:
                print(i)
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.for.try_finally",
            @"def main() -> None:
    for i in range(4):
        try:
            if i == 2:
                break
            print(i)
        finally:
            pass
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.for.try_in_match",
            @"def main() -> None:
    for i in range(4):
        match i:
            case 2:
                try:
                    break
                finally:
                    pass
            case _:
                print(i)
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.for.with",
            @"class CM:
    def __enter__(self) -> Self:
        return self
    def __exit__(self) -> None:
        pass

def main() -> None:
    for i in range(4):
        with CM():
            if i == 2:
                break
            print(i)
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "cont.for.if",
            @"def main() -> None:
    for i in range(4):
        if i == 2:
            continue
        print(i)
    print(""done"")",
            "0 1 3 done");

        yield return new Cell(
            "cont.for.match_plain",
            @"def main() -> None:
    for i in range(4):
        match i:
            case 2:
                continue
            case _:
                print(i)
    print(""done"")",
            "0 1 3 done");

        yield return new Cell(
            "cont.for.try_finally",
            @"def main() -> None:
    for i in range(4):
        try:
            if i == 2:
                continue
            print(i)
        finally:
            pass
    print(""done"")",
            "0 1 3 done");

        yield return new Cell(
            "cont.for.with",
            @"class CM:
    def __enter__(self) -> Self:
        return self
    def __exit__(self) -> None:
        pass

def main() -> None:
    for i in range(4):
        with CM():
            if i == 2:
                continue
            print(i)
    print(""done"")",
            "0 1 3 done");

        yield return new Cell(
            "brk.forelse.if",
            @"def main() -> None:
    for i in range(4):
        if i == 2:
            break
        print(i)
    else:
        print(""else"")
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.forelse.match",
            @"def main() -> None:
    for i in range(4):
        match i:
            case 2:
                break
            case _:
                print(i)
    else:
        print(""else"")
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.forelse.try",
            @"def main() -> None:
    for i in range(4):
        try:
            if i == 2:
                break
            print(i)
        finally:
            pass
    else:
        print(""else"")
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.forelse.with",
            @"class CM:
    def __enter__(self) -> Self:
        return self
    def __exit__(self) -> None:
        pass

def main() -> None:
    for i in range(4):
        with CM():
            if i == 2:
                break
            print(i)
    else:
        print(""else"")
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.forelse.match_hoist",
            @"def main() -> None:
    for i in range(4):
        match i:
            case x if x in [v for v in [2]]:
                break
            case _:
                print(i)
    else:
        print(""else"")
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "cont.forelse.if",
            @"def main() -> None:
    for i in range(4):
        if i == 2:
            continue
        print(i)
    else:
        print(""else"")
    print(""done"")",
            "0 1 3 else done");

        yield return new Cell(
            "cont.forelse.match",
            @"def main() -> None:
    for i in range(4):
        match i:
            case 2:
                continue
            case _:
                print(i)
    else:
        print(""else"")
    print(""done"")",
            "0 1 3 else done");

        yield return new Cell(
            "brk.while.match",
            @"def main() -> None:
    i = 0
    while i < 4:
        match i:
            case 2:
                break
            case _:
                print(i)
        i += 1
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "brk.whileelse.with",
            @"class CM:
    def __enter__(self) -> Self:
        return self
    def __exit__(self) -> None:
        pass

def main() -> None:
    i = 0
    while i < 4:
        with CM():
            if i == 2:
                break
            print(i)
        i += 1
    else:
        print(""else"")
    print(""done"")",
            "0 1 done");

        yield return new Cell(
            "cont.whileelse.match",
            @"def main() -> None:
    i = 0
    while i < 4:
        match i:
            case 2:
                i += 1
                continue
            case _:
                print(i)
        i += 1
    else:
        print(""else"")
    print(""done"")",
            "0 1 3 else done");

        yield return new Cell(
            "brk.nested.inner_if",
            @"def main() -> None:
    for a in range(2):
        for b in range(3):
            if b == 1:
                break
            print(a, b)
        print(""row"", a)
    print(""done"")",
            "0 0 row 0 1 0 row 1 done");

        yield return new Cell(
            "brk.nested.inner_match",
            @"def main() -> None:
    for a in range(2):
        for b in range(3):
            match b:
                case 1:
                    break
                case _:
                    print(a, b)
        print(""row"", a)
    print(""done"")",
            "0 0 row 0 1 0 row 1 done");

        yield return new Cell(
            "brk.nested.outer_match",
            @"def main() -> None:
    for a in range(3):
        match a:
            case 2:
                break
            case _:
                for b in range(2):
                    print(a, b)
    print(""done"")",
            "0 0 0 1 1 0 1 1 done");

        yield return new Cell(
            "brk.for.bool_match",
            @"def main() -> None:
    for i in range(2):
        b = i == 0
        match b:
            case True:
                print(""t"", i)
            case False:
                break
    print(""done"")",
            "t 0 done");
    }
}
