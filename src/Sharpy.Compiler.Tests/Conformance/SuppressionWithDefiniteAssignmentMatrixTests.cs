using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Definite assignment across a suppression-capable <c>with</c> = Python at runtime (#1839, R-AI):
/// body × read × declaration × manager. A bare local assigned in a suppression-capable body and read
/// after is RUNTIME-CHECKED, not refused — an unset read raises <c>UnboundLocalError</c> (Python's
/// exact wording), nothing Python runs is refused, nothing prints a default silently.
///
/// <para>Axes:
/// <list type="bullet">
/// <item><b>body</b> — no-raise, raise-before the assignment, raise-after it, may-raise(True/False),
///   conditional assignment (control: refused SPY0600 under BOTH managers — a plain DA hole).</item>
/// <item><b>read</b> — after, later branch, loop, augmented <c>n += 1</c>, nested <c>def</c>, lambda,
///   none (control: no flag emitted).</item>
/// <item><b>declaration</b> — bare (<c>n: int</c>), initialized (control: never runtime-checked).</item>
/// <item><b>manager</b> — suppressing (4-arg <c>__exit__</c> → True), non-suppressing (control: a
///   body assignment is unconditional, so a read after runs), suppressing-with-<c>as</c>.</item>
/// </list>
/// Every cell is Python-pinned: value printed / <c>UnboundLocalError</c> at runtime / SPY0600.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class SuppressionWithDefiniteAssignmentMatrixTests : IntegrationTestBase
{
    public SuppressionWithDefiniteAssignmentMatrixTests(ITestOutputHelper output) : base(output) { }

    private enum Expect { Prints, RaisesUnbound, SPY0600 }

    private sealed record Cell(string Label, string MainBody, Expect Expected, string? Output = null);

    private const string Preamble =
        "class Sup:\n" +
        "    def __enter__(self) -> int:\n" +
        "        return 1\n" +
        "    def __exit__(self, t: object?, v: Exception?, b: object?) -> bool:\n" +
        "        return True\n" +
        "class Plain:\n" +
        "    def __enter__(self) -> int:\n" +
        "        return 1\n" +
        "    def __exit__(self) -> None:\n" +
        "        pass\n" +
        "def maybe_raise(flag: bool) -> None:\n" +
        "    if flag:\n" +
        "        raise ValueError(\"boom\")\n";

    [Fact]
    [Trait("Category", "Conformance")]
    public void SuppressionWithDefiniteAssignmentMatrix_AllCellsPass()
    {
        var failures = new List<string>();
        var cells = GenerateCells().ToList();

        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        foreach (var cell in cells)
        {
            var result = CompileAndExecute(Preamble + cell.MainBody, executionTimeoutMs: 10_000);

            switch (cell.Expected)
            {
                case Expect.Prints:
                    if (!result.Success)
                        failures.Add($"{cell.Label}: expected to print '{cell.Output}' but failed: "
                            + string.Join("; ", result.CompilationErrors) + " | stderr: " + result.StandardError);
                    else if (result.StandardOutput.Trim() != cell.Output)
                        failures.Add($"{cell.Label}: expected '{cell.Output}', got '{result.StandardOutput.Trim()}'");
                    break;

                case Expect.RaisesUnbound:
                    // Compiles, then raises UnboundLocalError at runtime (exit != 0). NOT a refusal:
                    // no SPY0600 diagnostic, and the Python message on stderr.
                    if (result.RawDiagnostics.Any(d => d.Code == DiagnosticCodes.SemanticOverflow.UseBeforeAssignment))
                        failures.Add($"{cell.Label}: expected a runtime UnboundLocalError but was REFUSED (SPY0600)");
                    else if (result.Success)
                        failures.Add($"{cell.Label}: expected a runtime UnboundLocalError but printed '{result.StandardOutput.Trim()}'");
                    else if (!result.StandardError.Contains("cannot access local variable"))
                        failures.Add($"{cell.Label}: expected the UnboundLocalError message on stderr, got: {result.StandardError}");
                    break;

                case Expect.SPY0600:
                    if (result.Success)
                        failures.Add($"{cell.Label}: expected SPY0600 but compiled and ran ('{result.StandardOutput.Trim()}')");
                    else if (!result.RawDiagnostics.Any(d => d.Code == DiagnosticCodes.SemanticOverflow.UseBeforeAssignment))
                        failures.Add($"{cell.Label}: expected SPY0600 but got: "
                            + string.Join("; ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
                    break;
            }
        }

        Output.WriteLine($"Suppression DA cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var f in failures)
            Output.WriteLine($"  {f}");

        Assert.True(failures.Count == 0,
            $"Suppression-with definite-assignment matrix (#1839): {failures.Count} of {cells.Count} cells failed.\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static IEnumerable<Cell> GenerateCells()
    {
        // ── manager = suppressing, declaration = bare, read = after ──
        yield return new Cell("sup.bare.no-raise.after",
            "def main() -> None:\n    n: int\n    with Sup():\n        n = 5\n    print(n)",
            Expect.Prints, "5");

        yield return new Cell("sup.bare.raise-before.after",
            "def main() -> None:\n    n: int\n    with Sup():\n        maybe_raise(True)\n        n = 5\n    print(n)",
            Expect.RaisesUnbound);

        yield return new Cell("sup.bare.raise-after.after",
            "def main() -> None:\n    n: int\n    with Sup():\n        n = 5\n        maybe_raise(True)\n    print(n)",
            Expect.Prints, "5");

        yield return new Cell("sup.bare.may-raise-true.after",
            "def main() -> None:\n    n: int\n    with Sup():\n        maybe_raise(True)\n        n = 5\n    print(n)",
            Expect.RaisesUnbound);

        yield return new Cell("sup.bare.may-raise-false.after",
            "def main() -> None:\n    n: int\n    with Sup():\n        maybe_raise(False)\n        n = 5\n    print(n)",
            Expect.Prints, "5");

        // conditional assignment is a plain DA hole — refused under BOTH managers, control.
        yield return new Cell("sup.bare.conditional.after",
            "def main() -> None:\n    c: bool = True\n    n: int\n    with Sup():\n        if c:\n            n = 5\n    print(n)",
            Expect.SPY0600);

        // ── read shapes (suppressing, bare) ──
        yield return new Cell("sup.bare.no-raise.later-branch",
            "def main() -> None:\n    c: bool = True\n    n: int\n    with Sup():\n        n = 5\n    if c:\n        print(n)",
            Expect.Prints, "5");

        yield return new Cell("sup.bare.no-raise.loop",
            "def main() -> None:\n    n: int\n    with Sup():\n        n = 5\n    for _ in range(1):\n        print(n)",
            Expect.Prints, "5");

        yield return new Cell("sup.bare.no-raise.augmented",
            "def main() -> None:\n    n: int\n    with Sup():\n        n = 5\n    n += 1\n    print(n)",
            Expect.Prints, "6");

        yield return new Cell("sup.bare.raise-before.augmented",
            "def main() -> None:\n    n: int\n    with Sup():\n        maybe_raise(True)\n        n = 5\n    n += 1\n    print(n)",
            Expect.RaisesUnbound);

        yield return new Cell("sup.bare.no-raise.nested-def",
            "def main() -> None:\n    n: int\n    with Sup():\n        n = 5\n    def inner() -> int:\n        return n\n    print(inner())",
            Expect.Prints, "5");

        yield return new Cell("sup.bare.raise-before.nested-def",
            "def main() -> None:\n    n: int\n    with Sup():\n        maybe_raise(True)\n        n = 5\n    def inner() -> int:\n        return n\n    print(inner())",
            Expect.RaisesUnbound);

        yield return new Cell("sup.bare.no-raise.lambda",
            "def main() -> None:\n    n: int\n    with Sup():\n        n = 5\n    f = lambda: n\n    print(f())",
            Expect.Prints, "5");

        yield return new Cell("sup.bare.raise-before.lambda",
            "def main() -> None:\n    n: int\n    with Sup():\n        maybe_raise(True)\n        n = 5\n    f = lambda: n\n    print(f())",
            Expect.RaisesUnbound);

        // ── manager = non-suppressing (control): a body assignment is unconditional, so a read
        // after always runs; the conditional hole is still SPY0600. ──
        yield return new Cell("plain.bare.no-raise.after",
            "def main() -> None:\n    n: int\n    with Plain():\n        n = 5\n    print(n)",
            Expect.Prints, "5");

        yield return new Cell("plain.bare.conditional.after",
            "def main() -> None:\n    c: bool = True\n    n: int\n    with Plain():\n        if c:\n            n = 5\n    print(n)",
            Expect.SPY0600);

        // ── declaration = initialized (control): never runtime-checked, always prints. ──
        yield return new Cell("sup.init.no-raise.after",
            "def main() -> None:\n    n: int = 0\n    with Sup():\n        n = 5\n    print(n)",
            Expect.Prints, "5");

        yield return new Cell("sup.init.raise-before.after",
            "def main() -> None:\n    n: int = 0\n    with Sup():\n        maybe_raise(True)\n        n = 5\n    print(n)",
            Expect.Prints, "0");

        // ── manager = suppressing-with-as (owner ruling, refined-A). Two cells:
        //
        // PRIMARY (the #1839 content — "the as-target cell RUNS"): a body assignment to the read
        // variable, inside a suppressing `with` that HAS an as-target `s`, is runtime-checked exactly
        // like plain-suppressing — the as-target's mere presence does not disturb the two-pass.
        yield return new Cell("sup-as.bare.body-assign.after",
            "def main() -> None:\n    n: int\n    with Sup() as s:\n        n = 5\n    print(n)",
            Expect.Prints, "5");

        yield return new Cell("sup-as.bare.body-assign.raise-before",
            "def main() -> None:\n    n: int\n    with Sup() as s:\n        maybe_raise(True)\n        n = 5\n    print(n)",
            Expect.RaisesUnbound);

        // CONTROL (governed by block-scoping, NOT #1839): reading a pre-declared outer local rebound
        // by `with … as n` after the block is a genuine use-before-assign (SPY0600). Sharpy
        // BLOCK-SCOPES the `as` target — it does not persist (reading a fresh as-target outside its
        // block is SPY0200 "block-scoped … unlike Python"; Axiom 1), so DD4's "the binding precedes
        // the edge → definitely assigned" premise is VOID here. Not runtime-checked.
        yield return new Cell("sup-as.bare.as-target.after",
            "def main() -> None:\n    n: int\n    with Sup() as n:\n        pass\n    print(n)",
            Expect.SPY0600);
    }
}
