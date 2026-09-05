using System.Text;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance matrix: mutator × RHS shape.
/// Each cell compiles and runs a Sharpy program that calls a collection mutator with a given
/// RHS shape, prints the result, and compares against CPython 3.12. Augmented twins assert
/// identical output where Python has a twin (+=, |=, &=, -=, ^=).
///
/// The matrix exercises <c>GetMemberIterableKeyPositions</c> (the iterable-projection ring for
/// method calls): tuple, str, and dict-keys RHS shapes require the projection to compile.
/// </summary>
[Collection("HeavyCompilation")]
public class MutatorIterableArgumentMatrixTests : IntegrationTestBase
{
    public MutatorIterableArgumentMatrixTests(ITestOutputHelper output) : base(output) { }

    private record Cell(
        string Mutator,
        string RhsShape,
        string Source,
        bool ExpectsRefusal,
        string? ExpectedOutput,
        string? TwinLabel,
        string? TwinSource,
        string PythonEvidence);

    private static readonly string[] Mutators =
    {
        "list.extend", "set.update", "set.intersection_update",
        "set.difference_update", "set.symmetric_difference_update", "dict.update"
    };

    private static readonly string[] RhsShapes =
    {
        "list", "tuple_literal", "tuple_var", "str", "dict", "range", "set"
    };

    private static readonly HashSet<(string Mutator, string RhsShape)> NACells = new()
    {
        // str iterates to single characters (str elements), not (K, V) pairs
        ("dict.update", "str"),
        // range iterates to int, not (K, V) pairs
        ("dict.update", "range"),
        // set of scalars doesn't produce (K, V) pairs; set[tuple] is too unusual
        ("dict.update", "set"),
    };

    private static IReadOnlyList<Cell> BuildCells()
    {
        var cells = new List<Cell>();

        // ════════════════════════════════════════════════
        // list.extend × 7 shapes, each paired with +=
        // ════════════════════════════════════════════════

        cells.Add(new Cell("list.extend", "list",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs.extend([3, 4])
    print(len(xs))
",
            false, "4\n", "list +=",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs += [3, 4]
    print(len(xs))
",
            "python3 -c \"xs=[1,2]; xs.extend([3,4]); print(len(xs))\" => 4"));

        cells.Add(new Cell("list.extend", "tuple_literal",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs.extend((3, 4))
    print(len(xs))
",
            false, "4\n", "list +=",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs += (3, 4)
    print(len(xs))
",
            "python3 -c \"xs=[1,2]; xs.extend((3,4)); print(len(xs))\" => 4"));

        cells.Add(new Cell("list.extend", "tuple_var",
            @"
def main() -> None:
    t: tuple[int, int] = (3, 4)
    xs: list[int] = [1, 2]
    xs.extend(t)
    print(len(xs))
",
            false, "4\n", "list +=",
            @"
def main() -> None:
    t: tuple[int, int] = (3, 4)
    xs: list[int] = [1, 2]
    xs += t
    print(len(xs))
",
            "python3 -c \"t=(3,4); xs=[1,2]; xs.extend(t); print(len(xs))\" => 4"));

        cells.Add(new Cell("list.extend", "str",
            @"
def main() -> None:
    xs: list[str] = [""a""]
    xs.extend(""bc"")
    print(len(xs))
",
            false, "3\n", "list +=",
            @"
def main() -> None:
    xs: list[str] = [""a""]
    xs += ""bc""
    print(len(xs))
",
            "python3 -c \"xs=['a']; xs.extend('bc'); print(len(xs))\" => 3"));

        cells.Add(new Cell("list.extend", "dict",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs.extend({3: ""a"", 4: ""b""})
    print(len(xs))
",
            false, "4\n", "list +=",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs += {3: ""a"", 4: ""b""}
    print(len(xs))
",
            "python3 -c \"xs=[1,2]; xs.extend({3:'a',4:'b'}); print(len(xs))\" => 4"));

        cells.Add(new Cell("list.extend", "range",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs.extend(range(3, 6))
    print(len(xs))
",
            false, "5\n", "list +=",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs += range(3, 6)
    print(len(xs))
",
            "python3 -c \"xs=[1,2]; xs.extend(range(3,6)); print(len(xs))\" => 5"));

        cells.Add(new Cell("list.extend", "set",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs.extend({3, 4})
    print(len(xs))
",
            false, "4\n", "list +=",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs += {3, 4}
    print(len(xs))
",
            "python3 -c \"xs=[1,2]; xs.extend({3,4}); print(len(xs))\" => 4"));

        // ════════════════════════════════════════════════
        // set.update × 7 shapes, |= twin for set RHS only
        // ════════════════════════════════════════════════

        AddSetMutatorCells(cells, "set.update", "update",
            "{1, 2}", "{1, 2}", 4, "|=", "set |=");

        // ════════════════════════════════════════════════
        // set.intersection_update × 7 shapes, &= twin for set RHS only
        // ════════════════════════════════════════════════

        AddSetMutatorCells(cells, "set.intersection_update", "intersection_update",
            "{1, 2, 3}", "{1, 2, 3}", 2, "&=", "set &=",
            listRhs: "[2, 3]", tupleLitRhs: "(2, 3)", tupleVarInit: "(2, 3)",
            strRhsInit: @"{""a"", ""b"", ""c""}", strRhs: @"""bc""", strExpected: 2,
            dictRhs: @"{2: ""a"", 3: ""b""}", rangeRhs: "range(2, 4)", setRhs: "{2, 3}",
            expectedLen: 2);

        // ════════════════════════════════════════════════
        // set.difference_update × 7 shapes, -= twin for set RHS only
        // ════════════════════════════════════════════════

        AddSetMutatorCells(cells, "set.difference_update", "difference_update",
            "{1, 2, 3}", "{1, 2, 3}", 2, "-=", "set -=",
            listRhs: "[1]", tupleLitRhs: "(1, 1)", tupleVarInit: "(1, 1)",
            strRhsInit: @"{""a"", ""b"", ""c""}", strRhs: @"""a""", strExpected: 2,
            dictRhs: @"{1: ""a""}", rangeRhs: "range(1, 2)", setRhs: "{1}",
            expectedLen: 2);

        // ════════════════════════════════════════════════
        // set.symmetric_difference_update × 7 shapes, ^= twin for set RHS only
        // ════════════════════════════════════════════════

        AddSetMutatorCells(cells, "set.symmetric_difference_update", "symmetric_difference_update",
            "{1, 2, 3}", "{1, 2, 3}", 3, "^=", "set ^=",
            listRhs: "[3, 4]", tupleLitRhs: "(3, 4)", tupleVarInit: "(3, 4)",
            strRhsInit: @"{""a"", ""b"", ""c""}", strRhs: @"""cd""", strExpected: 3,
            dictRhs: @"{3: ""a"", 4: ""b""}", rangeRhs: "range(3, 6)", setRhs: "{3, 4}",
            expectedLen: 3);

        // ════════════════════════════════════════════════
        // dict.update × 4 shapes (str/range/set are N/A), |= twin for all
        // ════════════════════════════════════════════════

        cells.Add(new Cell("dict.update", "dict",
            @"
def main() -> None:
    d: dict[str, int] = {""a"": 1}
    d.update({""b"": 2})
    print(len(d))
",
            false, "2\n", "dict |=",
            @"
def main() -> None:
    d: dict[str, int] = {""a"": 1}
    d |= {""b"": 2}
    print(len(d))
",
            "python3 -c \"d={'a':1}; d.update({'b':2}); print(len(d))\" => 2"));

        cells.Add(new Cell("dict.update", "list",
            @"
def main() -> None:
    d: dict[str, int] = {""a"": 1}
    d.update([(""b"", 2)])
    print(len(d))
",
            false, "2\n", "dict |=",
            @"
def main() -> None:
    d: dict[str, int] = {""a"": 1}
    d |= [(""b"", 2)]
    print(len(d))
",
            "python3 -c \"d={'a':1}; d.update([('b',2)]); print(len(d))\" => 2"));

        cells.Add(new Cell("dict.update", "tuple_literal",
            @"
def main() -> None:
    d: dict[str, int] = {""a"": 1}
    d.update(((""b"", 2), (""c"", 3)))
    print(len(d))
",
            false, "3\n", "dict |=",
            @"
def main() -> None:
    d: dict[str, int] = {""a"": 1}
    d |= ((""b"", 2), (""c"", 3))
    print(len(d))
",
            "python3 -c \"d={'a':1}; d.update((('b',2),('c',3))); print(len(d))\" => 3"));

        cells.Add(new Cell("dict.update", "tuple_var",
            @"
def main() -> None:
    t: tuple[tuple[str, int], tuple[str, int]] = ((""b"", 2), (""c"", 3))
    d: dict[str, int] = {""a"": 1}
    d.update(t)
    print(len(d))
",
            false, "3\n", "dict |=",
            @"
def main() -> None:
    t: tuple[tuple[str, int], tuple[str, int]] = ((""b"", 2), (""c"", 3))
    d: dict[str, int] = {""a"": 1}
    d |= t
    print(len(d))
",
            "python3 -c \"t=(('b',2),('c',3)); d={'a':1}; d.update(t); print(len(d))\" => 3"));

        // ════════════════════════════════════════════════
        // Mistyped controls: SPY0220 on both routes
        // ════════════════════════════════════════════════

        cells.Add(new Cell("list.extend", "mistyped",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs.extend((""a"", ""b""))
",
            true, null, "list +=",
            @"
def main() -> None:
    xs: list[int] = [1, 2]
    xs += (""a"", ""b"")
",
            "SPY0220: str not assignable to int"));

        return cells;
    }

    private static void AddSetMutatorCells(
        List<Cell> cells, string mutator, string method,
        string intInit, string intInitForSetTwin, int setTwinLen,
        string twinOp, string twinLabel,
        string? listRhs = null, string? tupleLitRhs = null, string? tupleVarInit = null,
        string? strRhsInit = null, string? strRhs = null, int? strExpected = null,
        string? dictRhs = null, string? rangeRhs = null, string? setRhs = null,
        int? expectedLen = null)
    {
        int len = expectedLen ?? 4;
        listRhs ??= "[3, 4]";
        tupleLitRhs ??= "(3, 4)";
        tupleVarInit ??= "(3, 4)";
        strRhsInit ??= @"{""a"", ""b""}";
        strRhs ??= @"""cd""";
        strExpected ??= 4;
        dictRhs ??= @"{3: ""a"", 4: ""b""}";
        rangeRhs ??= "range(3, 6)";
        setRhs ??= "{3, 4}";

        int rangeLen = mutator == "set.update" ? 5
            : mutator == "set.symmetric_difference_update" ? 4
            : len;

        // list RHS
        cells.Add(new Cell(mutator, "list",
            $@"
def main() -> None:
    s: set[int] = {intInit}
    s.{method}({listRhs})
    print(len(s))
",
            false, $"{len}\n", null, null,
            $"python3 verified: {len}"));

        // tuple literal RHS
        cells.Add(new Cell(mutator, "tuple_literal",
            $@"
def main() -> None:
    s: set[int] = {intInit}
    s.{method}({tupleLitRhs})
    print(len(s))
",
            false, $"{len}\n", null, null,
            $"python3 verified: {len}"));

        // tuple var RHS (always 2-element tuple)
        cells.Add(new Cell(mutator, "tuple_var",
            $@"
def main() -> None:
    t: tuple[int, int] = {tupleVarInit}
    s: set[int] = {intInit}
    s.{method}(t)
    print(len(s))
",
            false, $"{len}\n", null, null,
            $"python3 verified: {len}"));

        // str RHS (uses set[str])
        cells.Add(new Cell(mutator, "str",
            $@"
def main() -> None:
    s: set[str] = {strRhsInit}
    s.{method}({strRhs})
    print(len(s))
",
            false, $"{strExpected}\n", null, null,
            $"python3 verified: {strExpected}"));

        // dict keys RHS
        cells.Add(new Cell(mutator, "dict",
            $@"
def main() -> None:
    s: set[int] = {intInit}
    s.{method}({dictRhs})
    print(len(s))
",
            false, $"{len}\n", null, null,
            $"python3 verified: {len}"));

        // range RHS
        cells.Add(new Cell(mutator, "range",
            $@"
def main() -> None:
    s: set[int] = {intInit}
    s.{method}({rangeRhs})
    print(len(s))
",
            false, $"{rangeLen}\n", null, null,
            $"python3 verified: {rangeLen}"));

        // set RHS (with augmented twin)
        cells.Add(new Cell(mutator, "set",
            $@"
def main() -> None:
    s: set[int] = {intInitForSetTwin}
    s.{method}({setRhs})
    print(len(s))
",
            false, $"{setTwinLen}\n", twinLabel,
            $@"
def main() -> None:
    s: set[int] = {intInitForSetTwin}
    s {twinOp} {setRhs}
    print(len(s))
",
            $"python3 verified: {setTwinLen}"));
    }

    [Fact]
    public void TotalityCoverage()
    {
        var cells = BuildCells();
        var cellKeys = cells
            .Where(c => c.RhsShape != "mistyped")
            .Select(c => (c.Mutator, c.RhsShape))
            .ToHashSet();

        var missing = new List<string>();
        foreach (var mutator in Mutators)
        {
            foreach (var shape in RhsShapes)
            {
                var key = (mutator, shape);
                if (!cellKeys.Contains(key) && !NACells.Contains(key))
                    missing.Add($"{mutator} x {shape}");
            }
        }

        Assert.True(missing.Count == 0,
            $"Every (mutator x RHS shape) triple must be a cell or declared N/A. Missing:\n  "
            + string.Join("\n  ", missing));
    }

    [Fact]
    public void NACells_AreNotInMatrix()
    {
        var cells = BuildCells();
        var cellKeys = cells.Select(c => (c.Mutator, c.RhsShape)).ToHashSet();

        var overlap = NACells.Where(na => cellKeys.Contains(na)).ToList();
        Assert.True(overlap.Count == 0,
            $"N/A cells must not also appear as matrix cells: {string.Join(", ", overlap.Select(o => $"{o.Mutator} x {o.RhsShape}"))}");
    }

    [Fact]
    public void AllCells_MatchCPython()
    {
        var cells = BuildCells();
        var failures = new StringBuilder();
        int passed = 0;

        foreach (var cell in cells)
        {
            var label = $"[{cell.Mutator} x {cell.RhsShape}]";
            var result = CompileAndExecute(cell.Source);

            if (cell.ExpectsRefusal)
            {
                if (result.Success)
                {
                    failures.AppendLine($"{label} expected refusal but compiled successfully");
                    continue;
                }

                var hasMismatch = result.RawDiagnostics.Any(d =>
                    d.Code == DiagnosticCodes.Semantic.TypeMismatch
                    || d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation);

                if (!hasMismatch)
                {
                    failures.AppendLine(
                        $"{label} expected SPY0220/SPY0354 but got: "
                        + string.Join("; ", result.CompilationErrors));
                    continue;
                }

                passed++;

                // Mistyped twin
                if (cell.TwinSource != null)
                {
                    var twinResult = CompileAndExecute(cell.TwinSource);
                    if (twinResult.Success)
                    {
                        failures.AppendLine(
                            $"{label} twin ({cell.TwinLabel}) expected refusal but compiled");
                        continue;
                    }

                    passed++;
                }
            }
            else
            {
                if (!result.Success)
                {
                    failures.AppendLine(
                        $"{label} expected success but got: "
                        + string.Join("; ", result.CompilationErrors));
                    continue;
                }

                if (result.StandardOutput != cell.ExpectedOutput)
                {
                    failures.AppendLine(
                        $"{label} output mismatch: expected {Repr(cell.ExpectedOutput)} got {Repr(result.StandardOutput)}");
                    continue;
                }

                passed++;

                // Augmented twin
                if (cell.TwinSource != null)
                {
                    var twinResult = CompileAndExecute(cell.TwinSource);
                    if (!twinResult.Success)
                    {
                        failures.AppendLine(
                            $"{label} twin ({cell.TwinLabel}) failed: "
                            + string.Join("; ", twinResult.CompilationErrors));
                        continue;
                    }

                    if (twinResult.StandardOutput != cell.ExpectedOutput)
                    {
                        failures.AppendLine(
                            $"{label} twin ({cell.TwinLabel}) output mismatch: "
                            + $"expected {Repr(cell.ExpectedOutput)} got {Repr(twinResult.StandardOutput)}");
                        continue;
                    }

                    passed++;
                }
            }
        }

        Output.WriteLine($"Passed: {passed} assertions");
        Assert.True(failures.Length == 0,
            $"Failures:\n{failures}");
    }

    private static string Repr(string? s)
        => s == null ? "(null)" : $"\"{s.Replace("\n", "\\n").Replace("\r", "\\r")}\"";
}
