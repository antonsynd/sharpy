using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The iteration ring route matrix (#1783, #1771): <b>source × route × slot</b>.
///
/// <para><b>Contract under test:</b> every iterable source kind that the ring accepts — tuple
/// literal, tuple variable, float-mix tuple, nested-with-unpacking, and the list/str/range
/// controls — iterates identically at every consumer route (for, comprehension, membership,
/// sorted, list constructor, sum, min) through ONE fact (<c>IterableArgumentProjection</c>)
/// recorded by <c>ClassifyIterableSource</c> and applied by every emitter consumer. The slot
/// axis (none vs receiver-element) decides the tuple element type through
/// <c>BestCommonType</c>.</para>
///
/// <para><b>Refusal cells:</b> a heterogeneous tuple at a slot-less route is arm-3 SPY0227;
/// a non-field member on a tuple receiver is SPY0203 with the <c>list(t)</c> steer;
/// <c>t.item1</c> resolves to the element (positive control).</para>
///
/// <para>Deferred to later phases: <c>tuple | None</c> receiver wrapper (Phase 6, #1792);
/// slot-passing at argument positions (explicit-generic and declared-target slots —
/// currently null is passed at argument positions); range membership (Phase 2, #1778);
/// <c>class-mix</c> via <c>(Dog(), Animal())</c> (arm-2 with user classes).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class IterationRingRouteMatrixTests : IntegrationTestBase
{
    public IterationRingRouteMatrixTests(ITestOutputHelper output) : base(output) { }

    // ───────────────────────────── axes ─────────────────────────────

    private sealed record Source(string Id, string Setup, string Expr, string ElementType, bool IsControl);
    private sealed record Route(string Id, string Label);
    private sealed record Slot(string Id, string Label);

    private static readonly Source[] Sources =
    {
        new("tuple-literal", "", "(1, 2, 3)", "int", false),
        new("tuple-var", "    t: tuple[int, int, int] = (1, 2, 3)\n", "t", "int", false),
        new("float-mix", "", "(1, 2.5)", "float", false),
        new("nested-unpack", "", "((1, 2), (3, 4))", "tuple[int,int]", false),
        new("list-control", "    xs: list[int] = [1, 2, 3]\n", "xs", "int", true),
        new("str-control", "    s: str = \"abc\"\n", "s", "str", true),
        new("range-control", "", "range(3)", "int", true),
    };

    private static readonly Route[] Routes =
    {
        new("for", "for loop"),
        new("list-comp", "list comprehension"),
        new("set-comp", "set comprehension"),
        new("dict-comp", "dict comprehension"),
        new("in", "membership in"),
        new("not-in", "membership not in"),
        new("sorted", "sorted()"),
        new("list-ctor", "list()"),
        new("sum", "sum()"),
        new("min", "min()"),
    };

    private static readonly Slot[] Slots =
    {
        new("none", "no slot"),
        new("receiver-element", "receiver element type"),
    };

    // ───────────────────────────── N/A ─────────────────────────────

    private sealed record NaCell(string Source, string Route, string Slot, string Reason);

    private static NaCell[] BuildNaCells()
    {
        var cells = new List<NaCell>();
        foreach (var src in Sources)
            foreach (var route in Routes)
                foreach (var slot in Slots)
                {
                    var reason = NaReason(src, route, slot);
                    if (reason != null)
                        cells.Add(new NaCell(src.Id, route.Id, slot.Id, reason));
                }
        return cells.ToArray();
    }

    private static string? NaReason(Source src, Route route, Slot slot)
    {
        // The receiver-element slot only applies to routes that have a typed target
        // (extend/+= — those are separate tests, not matrix rows, because they need
        // a receiver declaration). All other routes pass no slot.
        if (slot.Id == "receiver-element")
            return "receiver-element slot is tested in dedicated extend/+= tests, not the matrix routes";

        // nested-unpack only makes sense with for (unpacking target) and comprehension
        if (src.Id == "nested-unpack" && route.Id is not ("for" or "list-comp"))
            return "nested unpacking is only meaningful with for and list comprehension iterators";

        // sum requires numeric elements
        if (route.Id == "sum" && src.Id is "str-control")
            return "sum does not accept string elements — no numeric accumulator for str";

        // min requires comparable elements — nested tuples are not directly comparable
        if (route.Id == "min" && src.Id == "nested-unpack")
            return "min of nested tuples is not directly comparable in Sharpy (no tuple ordering)";

        // float-mix at argument positions (sorted, list-ctor, sum, min): the slot is not
        // passed at argument positions yet (null is passed) so the projection element stays
        // int, producing CS1503 — deferred until slot-passing at argument positions is wired
        if (src.Id == "float-mix" && route.Id is "sorted" or "list-ctor" or "sum" or "min")
            return "float-mix tuple at argument positions deferred — slot is null at RecordIterableArgumentMarks";

        // range membership is Phase 2 (#1778) — RangeIterator has no Contains yet
        if (src.Id == "range-control" && route.Id is "in" or "not-in")
            return "range membership is Phase 2 (#1778) — RangeIterator.Contains not implemented yet";

        return null;
    }

    // ───────────────────────────── program generation ─────────────────────────────

    private sealed class SourceBuilder
    {
        private readonly List<string> _lines = new();
        public int Add(string line) { _lines.Add(line); return _lines.Count; }
        public string Text => string.Join("\n", _lines) + "\n";
    }

    private sealed record Cell(
        string Id,
        string Source,
        string Route,
        string Slot,
        bool Accepted,
        int Line,
        string Expected,
        string? Code);

    private sealed record MatrixProgram(
        string Id, string Source, bool ShouldCompile, IReadOnlyList<Cell> Cells);

    private static string FormatOutput(Source src, Route route)
    {
        if (src.Id == "tuple-literal" || src.Id == "tuple-var")
        {
            return route.Id switch
            {
                "for" => "1\n2\n3",
                "list-comp" => "[1, 2, 3]",
                "set-comp" => "{1, 2, 3}",
                "dict-comp" => "{1: 1, 2: 2, 3: 3}",
                "in" => "True",
                "not-in" => "False",
                "sorted" => "[1, 2, 3]",
                "list-ctor" => "[1, 2, 3]",
                "sum" => "6",
                "min" => "1",
                _ => throw new ArgumentOutOfRangeException(nameof(route)),
            };
        }

        if (src.Id == "float-mix")
        {
            return route.Id switch
            {
                "for" => "1.0\n2.5",
                "list-comp" => "[1.0, 2.5]",
                "set-comp" => "{1.0, 2.5}",
                "dict-comp" => "{1.0: 1.0, 2.5: 2.5}",
                "in" => "False",
                "not-in" => "True",
                "sorted" => "[1.0, 2.5]",
                "list-ctor" => "[1.0, 2.5]",
                "sum" => "3.5",
                "min" => "1.0",
                _ => throw new ArgumentOutOfRangeException(nameof(route)),
            };
        }

        if (src.Id == "nested-unpack")
        {
            return route.Id switch
            {
                "for" => "1 2\n3 4",
                "list-comp" => "[(1, 2), (3, 4)]",
                _ => throw new ArgumentOutOfRangeException(nameof(route)),
            };
        }

        if (src.Id == "list-control")
        {
            return route.Id switch
            {
                "for" => "1\n2\n3",
                "list-comp" => "[1, 2, 3]",
                "set-comp" => "{1, 2, 3}",
                "dict-comp" => "{1: 1, 2: 2, 3: 3}",
                "in" => "True",
                "not-in" => "False",
                "sorted" => "[1, 2, 3]",
                "list-ctor" => "[1, 2, 3]",
                "sum" => "6",
                "min" => "1",
                _ => throw new ArgumentOutOfRangeException(nameof(route)),
            };
        }

        if (src.Id == "str-control")
        {
            return route.Id switch
            {
                "for" => "a\nb\nc",
                "list-comp" => "['a', 'b', 'c']",
                "set-comp" => "{'a', 'b', 'c'}",
                "dict-comp" => "{'a': 'a', 'b': 'b', 'c': 'c'}",
                "in" => "True",
                "not-in" => "False",
                "sorted" => "['a', 'b', 'c']",
                "list-ctor" => "['a', 'b', 'c']",
                "min" => "a",
                _ => throw new ArgumentOutOfRangeException(nameof(route)),
            };
        }

        if (src.Id == "range-control")
        {
            return route.Id switch
            {
                "for" => "0\n1\n2",
                "list-comp" => "[0, 1, 2]",
                "set-comp" => "{0, 1, 2}",
                "dict-comp" => "{0: 0, 1: 1, 2: 2}",
                "in" => "True",
                "not-in" => "False",
                "sorted" => "[0, 1, 2]",
                "list-ctor" => "[0, 1, 2]",
                "sum" => "3",
                "min" => "0",
                _ => throw new ArgumentOutOfRangeException(nameof(route)),
            };
        }

        throw new ArgumentOutOfRangeException(nameof(src));
    }

    private static string Needle(Source src)
    {
        return src.Id switch
        {
            "tuple-literal" or "tuple-var" or "list-control" or "range-control" => "2",
            "float-mix" => "2",
            "str-control" => "\"b\"",
            _ => throw new ArgumentOutOfRangeException(nameof(src)),
        };
    }

    private static MatrixProgram BuildProgram(Source src, Route route)
    {
        var sb = new SourceBuilder();
        sb.Add("def main() -> None:");
        if (src.Setup.Length > 0)
        {
            foreach (var setupLine in src.Setup.TrimEnd('\n').Split('\n'))
                sb.Add(setupLine);
        }

        var id = $"{src.Id}/{route.Id}/none";
        int line;

        switch (route.Id)
        {
            case "for":
                if (src.Id == "nested-unpack")
                {
                    sb.Add($"    for a, b in {src.Expr}:");
                    line = sb.Add("        print(a, b)");
                }
                else
                {
                    sb.Add($"    for x in {src.Expr}:");
                    line = sb.Add("        print(x)");
                }
                break;

            case "list-comp":
                if (src.Id == "nested-unpack")
                    line = sb.Add($"    print([t for t in {src.Expr}])");
                else
                    line = sb.Add($"    print([x for x in {src.Expr}])");
                break;

            case "set-comp":
                line = sb.Add($"    print({{x for x in {src.Expr}}})");
                break;

            case "dict-comp":
                line = sb.Add($"    print({{x: x for x in {src.Expr}}})");
                break;

            case "in":
                line = sb.Add($"    print({Needle(src)} in {src.Expr})");
                break;

            case "not-in":
                line = sb.Add($"    print({Needle(src)} not in {src.Expr})");
                break;

            case "sorted":
                line = sb.Add($"    print(sorted({src.Expr}))");
                break;

            case "list-ctor":
                line = sb.Add($"    print(list({src.Expr}))");
                break;

            case "sum":
                line = sb.Add($"    print(sum({src.Expr}))");
                break;

            case "min":
                line = sb.Add($"    print(min({src.Expr}))");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(route));
        }

        var expected = FormatOutput(src, route);
        return new MatrixProgram(id, sb.Text, true,
            new[] { new Cell(id, src.Id, route.Id, "none", true, line, expected, null) });
    }

    private static IReadOnlyList<MatrixProgram> BuildPrograms()
    {
        var programs = new List<MatrixProgram>();
        var naCells = BuildNaCells();

        foreach (var src in Sources)
            foreach (var route in Routes)
                foreach (var slot in Slots)
                {
                    if (naCells.Any(n => n.Source == src.Id && n.Route == route.Id && n.Slot == slot.Id))
                        continue;

                    if (slot.Id == "none")
                        programs.Add(BuildProgram(src, route));
                }

        return programs;
    }

    public static IEnumerable<object[]> Programs =>
        BuildPrograms().Select(p => new object[] { p.Id });

    // ───────────────────────────── runner ─────────────────────────────

    [Theory]
    [MemberData(nameof(Programs))]
    public void MatrixCellBehavesAsTheRulePredicts(string programId)
    {
        var program = BuildPrograms().Single(p => p.Id == programId);
        var result = CompileAndExecute(program.Source);

        if (program.ShouldCompile)
        {
            result.Success.Should().BeTrue(
                $"{programId} is admitted by the rule but failed: "
                + string.Join("; ", result.CompilationErrors) + "\n" + program.Source);

            var printed = result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            var expectedLines = program.Cells[0].Expected
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            printed.Should().Equal(expectedLines,
                $"cell {program.Cells[0].Id} value\n{program.Source}");

            return;
        }

        result.Success.Should().BeFalse($"{programId} must be refused\n{program.Source}");

        foreach (var cell in program.Cells)
        {
            result.RawDiagnostics.Should().Contain(
                d => d.Code == cell.Code
                    && d.Message.Contains(cell.Expected, StringComparison.Ordinal),
                $"cell {cell.Id} must be refused with {cell.Code} naming "
                + $"'{cell.Expected}'; got "
                + string.Join(" | ", result.RawDiagnostics.Select(
                    d => $"{d.Code}:{d.Message}"))
                + "\n" + program.Source);
        }
    }

    // ───────────────────────────── totality pin ─────────────────────────────

    [Fact]
    public void MatrixIsTotalOverItsAxes()
    {
        var programs = BuildPrograms();
        var live = programs.SelectMany(p => p.Cells).ToList();
        var naCells = BuildNaCells();

        live.Select(c => c.Id).Should().OnlyHaveUniqueItems("each cell appears once");

        foreach (var src in Sources)
            foreach (var route in Routes)
                foreach (var slot in Slots)
                {
                    var isNa = naCells.Any(
                        n => n.Source == src.Id && n.Route == route.Id && n.Slot == slot.Id);
                    if (isNa)
                        continue;

                    live.Should().Contain(
                        c => c.Source == src.Id && c.Route == route.Id && c.Slot == slot.Id,
                        $"{src.Id} × {route.Id} × {slot.Id} is a cell of the matrix");
                }

        naCells.Should().OnlyContain(n => n.Reason.Length >= 20,
            "every N/A cell states why (≥ 20 chars)");

        var total = Sources.Length * Routes.Length * Slots.Length;
        (live.Count + naCells.Length).Should().Be(total,
            $"live ({live.Count}) + N/A ({naCells.Length}) = "
            + $"{Sources.Length} × {Routes.Length} × {Slots.Length}");
    }

    // ──────────── heterogeneous refusal (arm-3 SPY0227) ────────────

    [Theory]
    [InlineData("for x in (1, \"a\"):\n        print(x)", "for iterator")]
    [InlineData("print([x for x in (1, \"a\")])", "list comprehension")]
    [InlineData("print(2 in (1, \"a\"))", "membership container")]
    public void HeterogeneousTupleIsRefusedAtSlotLessRoutes(string body, string siteNoun)
    {
        var result = CompileAndExecute($@"
def main() -> None:
    {body}
");
        result.Success.Should().BeFalse(
            $"heterogeneous tuple at {siteNoun} must be refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.CannotInferType
                 && d.Message.Contains("best common type", StringComparison.Ordinal),
            "the arm-3 refusal names best-common-type; got: "
            + string.Join(" | ", result.RawDiagnostics.Select(
                d => $"{d.Code}:{d.Message}")));
    }

    [Fact]
    public void HeterogeneousTupleWithAnnotatedTargetPrints()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[object] = [1, ""a""]
    print(len(xs))
    for x in xs:
        print(x)
");
        result.Success.Should().BeTrue(
            "the annotated positive control compiles: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Should().Equal("2", "1", "a");
    }

    // ──────────── tuple member refusal (SPY0203 with steer) ────────────

    [Theory]
    [InlineData("count", "a tuple is not a list; use 'list(t).count(...)'")]
    [InlineData("index", "a tuple is not a list; use 'list(t).index(...)'")]
    public void TupleMemberRefusalWithSteer(string member, string steer)
    {
        var result = CompileAndExecute($@"
def main() -> None:
    t: tuple[int, int, int] = (1, 2, 3)
    t.{member}(1)
");
        result.Success.Should().BeFalse(
            $"t.{member}() must be refused with SPY0203");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.UndefinedMember
                 && d.Message.Contains(steer, StringComparison.Ordinal),
            $"the refusal steers to list(t).{member}(); got: "
            + string.Join(" | ", result.RawDiagnostics.Select(
                d => $"{d.Code}:{d.Message}")));
    }

    [Fact]
    public void TupleItemAccessResolves()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    t: tuple[int, int, int] = (1, 2, 3)
    print(t.item1)
    print(t.item2)
    print(t.item3)
");
        result.Success.Should().BeTrue(
            "t.item1/2/3 must resolve: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Should().Equal("1", "2", "3");
    }

    // ──────────── extend and += (slot = receiver element) ────────────

    [Fact]
    public void ExtendWithTupleLiteral()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[int] = [0]
    xs.extend((1, 2))
    print(xs)
");
        result.Success.Should().BeTrue(
            "extend with tuple: " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("[0, 1, 2]");
    }

    [Fact]
    public void AugmentedAssignWithTupleLiteral()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[int] = [0]
    xs += (1, 2)
    print(xs)
");
        result.Success.Should().BeTrue(
            "+= with tuple: " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("[0, 1, 2]");
    }

    [Fact]
    public void ExtendWithHomogeneousTupleVar()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    xs: list[int] = [0]
    t: tuple[int, int] = (1, 2)
    xs.extend(t)
    print(xs)
");
        result.Success.Should().BeTrue(
            "extend with tuple var: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("[0, 1, 2]");
    }

    // ──────────── float-mix membership (2 in (1, 2.5) → False) ────────────

    [Fact]
    public void FloatMixMembershipNeedleConverts()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    print(2 in (1, 2.5))
    print(1 in (1, 2.5))
    print(2.5 in (1, 2.5))
    print(3 not in (1, 2.5))
");
        result.Success.Should().BeTrue(
            "float-mix membership: " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Should().Equal("False", "True", "True", "True");
    }

    // ──────────── enumerate and join routes ────────────

    [Fact]
    public void EnumerateOverTuple()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    for i, x in enumerate((10, 20)):
        print(i)
        print(x)
");
        result.Success.Should().BeTrue(
            "enumerate with tuple: " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Should().Equal("0", "10", "1", "20");
    }

    [Fact]
    public void JoinWithStrTuple()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    print("","".join((""a"", ""b"", ""c"")))
");
        result.Success.Should().BeTrue(
            "join with str tuple: " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("a,b,c");
    }

    // ──────────── any/all routes ────────────

    [Fact]
    public void AnyAllWithBoolTuple()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    print(any((True, False)))
    print(all((True, False)))
    print(any((False, False)))
    print(all((True, True)))
");
        result.Success.Should().BeTrue(
            "any/all with bool tuple: " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Should().Equal("True", "False", "False", "True");
    }

    // ──────────── class hierarchy (arm-2: Dog → Animal) ────────────

    [Fact]
    public void ClassMixTupleIteratesAsBaseType()
    {
        var result = CompileAndExecute(@"
class Animal:
    name: str

    def __init__(self, name: str) -> None:
        self.name = name

    def __str__(self) -> str:
        return self.name

class Dog(Animal):
    def __init__(self) -> None:
        super().__init__(""Dog"")

def main() -> None:
    for x in (Dog(), Animal(""Cat"")):
        print(x)
");
        result.Success.Should().BeTrue(
            "class-mix tuple iteration: "
            + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Should().Equal("Dog", "Cat");
    }

    // ──────────── max and sum argument-route controls ────────────

    [Fact]
    public void MaxOverTupleVar()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    t: tuple[int, int, int] = (3, 1, 2)
    print(max(t))
");
        result.Success.Should().BeTrue(
            "max(tuple var): " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("3");
    }

    [Fact]
    public void SumOverHomogeneousTuple()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    print(sum((1, 2, 3)))
    t: tuple[int, int, int] = (4, 5, 6)
    print(sum(t))
");
        result.Success.Should().BeTrue(
            "sum(tuple): " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Should().Equal("6", "15");
    }
}
