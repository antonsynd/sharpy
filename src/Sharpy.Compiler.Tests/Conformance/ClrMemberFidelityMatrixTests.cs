using Microsoft.Extensions.Logging.Abstractions;
using Sharpy.Compiler.Api;
using Sharpy.Compiler.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// CLR member fidelity matrix (#1640): every member access on a CLR-origin receiver
/// has the reflected semantic type, in both Pythonic and CLR-cased spellings, so a
/// mistyped destination is SPY0220 — never Unknown (SPY0908).
///
/// Axes: receiver × member-kind × spelling × destination.
/// </summary>
public class ClrMemberFidelityMatrixTests
{
    private readonly ITestOutputHelper _output;

    public ClrMemberFidelityMatrixTests(ITestOutputHelper output) => _output = output;

    private sealed record Cell(string Label, string Source, bool ExpectsError, string? ErrorSubstring = null);

    [Fact]
    [Trait("Category", "Conformance")]
    public void ClrMemberFidelityMatrix_AllCellsPass()
    {
        var (corePath, stdlibPath) = ResolveStdlibAssemblyPaths();
        var api = new CompilerApi(NullLogger.Instance, new[] { corePath, stdlibPath });

        var failures = new List<string>();
        var cells = GenerateCells().ToList();

        foreach (var cell in cells)
        {
            CompileResult result;
            try
            {
                result = api.Compile(cell.Source, new CompilerOptions { OutputType = "library" });
            }
            catch (Exception ex)
            {
                failures.Add($"{cell.Label}: crashed — {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var errors = result.Diagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
                .ToList();

            if (errors.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError))
            {
                failures.Add($"{cell.Label}: SPY0908 — the member resolved to Unknown instead of its reflected type");
                continue;
            }

            if (cell.ExpectsError)
            {
                if (errors.Count == 0)
                {
                    failures.Add($"{cell.Label}: expected error but compiled clean — the member type is still Unknown");
                    continue;
                }

                if (cell.ErrorSubstring != null
                    && !errors.Any(d => d.Message.Contains(cell.ErrorSubstring, StringComparison.Ordinal)))
                {
                    failures.Add(
                        $"{cell.Label}: expected '{cell.ErrorSubstring}', got {errors[0].Code}: {errors[0].Message}");
                }
            }
            else
            {
                if (errors.Count > 0)
                    failures.Add($"{cell.Label}: expected to compile but drew {errors[0].Code}: {errors[0].Message}");
            }
        }

        _output.WriteLine($"Fidelity cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var f in failures)
            _output.WriteLine($"  {f}");

        Assert.True(failures.Count == 0,
            $"CLR member fidelity (#1640): {failures.Count} of {cells.Count} cells failed.\n" +
            string.Join("\n", failures.Select(f => "  " + f)));
    }

    private static IEnumerable<Cell> GenerateCells()
    {
        // ── Unmapped generic: Stack[int] ──

        // Method: peek() — Pythonic spelling
        yield return new Cell("Stack[int].peek()-correct-pythonic",
            Src("Stack", "s = Stack[int]()\n    s.push(1)\n    n: int = s.peek()"),
            false);

        yield return new Cell("Stack[int].peek()-wrong-pythonic",
            Src("Stack", "s = Stack[int]()\n    s.push(1)\n    x: str = s.peek()"),
            true, "Cannot assign type 'int32'");

        // Method: Peek() — CLR-cased spelling
        yield return new Cell("Stack[int].Peek()-correct-clr",
            Src("Stack", "s = Stack[int]()\n    s.push(1)\n    n: int = s.Peek()"),
            false);

        yield return new Cell("Stack[int].Peek()-wrong-clr",
            Src("Stack", "s = Stack[int]()\n    s.push(1)\n    x: str = s.Peek()"),
            true, "Cannot assign type 'int32'");

        // Property: count — Pythonic spelling
        yield return new Cell("Stack[int].count-correct-pythonic",
            Src("Stack", "s = Stack[int]()\n    n: int = s.count"),
            false);

        yield return new Cell("Stack[int].count-wrong-pythonic",
            Src("Stack", "s = Stack[int]()\n    x: str = s.count"),
            true);

        // Property: Count — CLR-cased spelling
        yield return new Cell("Stack[int].Count-correct-clr",
            Src("Stack", "s = Stack[int]()\n    n: int = s.Count"),
            false);

        yield return new Cell("Stack[int].Count-wrong-clr",
            Src("Stack", "s = Stack[int]()\n    x: str = s.Count"),
            true);

        // ── Unmapped generic: Queue[int] ──

        yield return new Cell("Queue[int].peek()-correct",
            Src("Queue", "q = Queue[int]()\n    q.enqueue(1)\n    n: int = q.peek()"),
            false);

        yield return new Cell("Queue[int].peek()-wrong",
            Src("Queue", "q = Queue[int]()\n    q.enqueue(1)\n    x: str = q.peek()"),
            true, "Cannot assign type 'int32'");

        yield return new Cell("Queue[int].count-correct",
            Src("Queue", "q = Queue[int]()\n    n: int = q.count"),
            false);

        yield return new Cell("Queue[int].count-wrong",
            Src("Queue", "q = Queue[int]()\n    x: str = q.count"),
            true);

        // ── Nested generic: Stack[Queue[int]] ──

        yield return new Cell("Stack[Queue[int]].peek-nested-correct",
            "from system.collections.generic import Stack, Queue\n\ndef _use() -> None:\n    s = Stack[Queue[int]]()\n    s.push(Queue[int]())\n    _q = s.peek()\n",
            false);

        // ── Mapped generic: List[int] (Sharpy builtin — CLR members via bridge) ──

        yield return new Cell("List[int].count-correct",
            "def _use() -> None:\n    xs: list[int] = [1, 2, 3]\n    n: int = xs.count\n",
            false);

        yield return new Cell("List[int].count-wrong",
            "def _use() -> None:\n    xs: list[int] = [1, 2, 3]\n    x: str = xs.count\n",
            true);
    }

    private static string Src(string type, string body) =>
        $"from system.collections.generic import {type}\n\ndef _use() -> None:\n    {body}\n";

    private static (string CorePath, string StdlibPath) ResolveStdlibAssemblyPaths()
    {
        var baseDir = Path.GetDirectoryName(typeof(ClrMemberFidelityMatrixTests).Assembly.Location)!;
        var core = Path.Combine(baseDir, "Sharpy.Core.dll");
        var stdlib = Path.Combine(baseDir, "Sharpy.Stdlib.dll");
        return (core, stdlib);
    }
}
