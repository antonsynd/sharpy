using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Store-position × target-kind matrix: every combination of store position
/// (assignment, tuple element, starred, for statement, comprehension, with-as,
/// except-as, walrus) and target kind (identifier, attribute, index, tuple,
/// starred) either compiles and runs or is refused with the expected diagnostic.
/// Guards the ParseStoreTarget / GenerateStore routing (#1672 E2).
/// </summary>
[Collection("HeavyCompilation")]
public class StoreTargetMatrixTests : IntegrationTestBase
{
    public StoreTargetMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Preamble = @"
class Box:
    value: int
    def __init__(self, v: int) -> None:
        self.value = v

class CM:
    def __enter__(self) -> int:
        return 42
    def __exit__(self) -> None:
        pass

class CMPair:
    def __enter__(self) -> tuple[int, str]:
        return (1, ""two"")
    def __exit__(self) -> None:
        pass
";

    // ── Assignment position ──

    [Fact]
    public void Assignment_Identifier()
    {
        var source = Preamble + @"
def main() -> None:
    x: int = 10
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("10");
    }

    [Fact]
    public void Assignment_Attribute()
    {
        var source = Preamble + @"
def main() -> None:
    b: Box = Box(0)
    b.value = 99
    print(b.value)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("99");
    }

    [Fact]
    public void Assignment_Index()
    {
        var source = Preamble + @"
def main() -> None:
    xs: list[int] = [0, 0, 0]
    xs[1] = 42
    print(xs[1])
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("42");
    }

    [Fact]
    public void Assignment_Tuple()
    {
        var source = Preamble + @"
def main() -> None:
    a: int = 0
    c: int = 0
    a, c = 1, 2
    print(a, c)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("1 2");
    }

    [Fact]
    public void TupleElement_Attribute()
    {
        // Not N/A: a tuple unpack target is any store target, not only a name — python3 binds
        // p.x=1, p.y=2 here and Sharpy has always emitted it through GenerateStore's recursive
        // unpacking. The "tuple unpack targets are names" premise was simply false (#1672 E2).
        var source = Preamble + @"
def main() -> None:
    p: Box = Box(0)
    q: Box = Box(0)
    p.value, q.value = 1, 2
    print(p.value, q.value)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Contain("1 2");
    }

    [Fact]
    public void TupleElement_Index()
    {
        var source = Preamble + @"
def main() -> None:
    xs: list[int] = [0, 0]
    xs[0], xs[1] = 3, 4
    print(xs[0], xs[1])
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Contain("3 4");
    }

    [Fact]
    public void Assignment_Starred()
    {
        var source = Preamble + @"
def main() -> None:
    first: int = 0
    rest: list[int] = []
    first, *rest = [10, 20, 30]
    print(first, rest)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("10");
        result.StandardOutput.Should().Contain("[20, 30]");
    }

    // ── For statement position ──

    [Fact]
    public void ForStatement_Identifier()
    {
        var source = Preamble + @"
def main() -> None:
    for i in [1, 2, 3]:
        print(i)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("1");
        result.StandardOutput.Should().Contain("2");
        result.StandardOutput.Should().Contain("3");
    }

    [Fact]
    public void ForStatement_Attribute()
    {
        var source = Preamble + @"
def main() -> None:
    b: Box = Box(0)
    for b.value in [1, 2, 3]:
        print(b.value)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("1");
        result.StandardOutput.Should().Contain("3");
    }

    [Fact]
    public void ForStatement_Index()
    {
        var source = Preamble + @"
def main() -> None:
    xs: list[int] = [0, 0, 0]
    for xs[0] in [10, 20, 30]:
        print(xs[0])
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("10");
        result.StandardOutput.Should().Contain("30");
    }

    [Fact]
    public void ForStatement_Tuple()
    {
        var source = Preamble + @"
def main() -> None:
    for a, b in [(1, 2), (3, 4)]:
        print(a, b)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("1 2");
        result.StandardOutput.Should().Contain("3 4");
    }

    // For statement + starred: N/A — Python doesn't support `for *rest, last in ...`

    // ── Comprehension position ──

    [Fact]
    public void Comprehension_Identifier()
    {
        var source = Preamble + @"
def main() -> None:
    result: list[int] = [x for x in [1, 2, 3]]
    print(result)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("[1, 2, 3]");
    }

    [Fact]
    public void Comprehension_Attribute()
    {
        var source = Preamble + @"
def main() -> None:
    b: Box = Box(0)
    result: list[int] = [b.value for b.value in [1, 2, 3]]
    print(result)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("[1, 2, 3]");
    }

    [Fact]
    public void Comprehension_Index()
    {
        var source = Preamble + @"
def main() -> None:
    xs: list[int] = [0]
    result: list[int] = [xs[0] for xs[0] in [10, 20, 30]]
    print(result)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("[10, 20, 30]");
    }

    [Fact]
    public void Comprehension_Tuple()
    {
        var source = Preamble + @"
def main() -> None:
    result: list[int] = [a + b for a, b in [(1, 2), (3, 4)]]
    print(result)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("[3, 7]");
    }

    // Comprehension + starred: N/A — Python doesn't support starred targets in comprehension for-clauses

    // ── With-as position ──

    [Fact]
    public void WithAs_Identifier()
    {
        var source = Preamble + @"
def main() -> None:
    with CM() as v:
        print(v)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("42");
    }

    [Fact]
    public void WithAs_Attribute()
    {
        var source = Preamble + @"
def main() -> None:
    b: Box = Box(0)
    with CM() as b.value:
        print(b.value)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("42");
    }

    [Fact]
    public void WithAs_Index()
    {
        var source = Preamble + @"
def main() -> None:
    xs: list[int] = [0]
    with CM() as xs[0]:
        print(xs[0])
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("42");
    }

    [Fact]
    public void WithAs_Tuple()
    {
        // "Uncommon" was never a reason to call a cell N/A — python3 binds a=1, b="two" here when
        // __enter__ returns the pair, and the parser has accepted the target since fbfe27503. The
        // cell was N/A on a false premise while the binder reported SPY0200 on every name (#1672 E2).
        var source = Preamble + @"
def main() -> None:
    with CMPair() as (a, b):
        print(a)
        print(b)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Contain("1");
        result.StandardOutput.Should().Contain("two");
    }

    [Fact]
    public void WithAs_Tuple_ArityMismatch_Refused()
    {
        var source = Preamble + @"
def main() -> None:
    with CMPair() as (a, b, c):
        print(a)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.InvalidTupleUnpacking,
            "a tuple target whose arity does not match __enter__'s tuple is refused, not bound");
    }

    [Fact]
    public void WithAs_Tuple_NonTupleEnter_Refused()
    {
        var source = Preamble + @"
def main() -> None:
    with CM() as (a, b):
        print(a)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.InvalidTupleUnpacking,
            "__enter__ returns int here, so there is nothing to unpack");
    }

    // With-as + starred: N/A — not a meaningful pattern

    // ── Except-as position — REFUSED ──

    [Fact]
    public void ExceptAs_Identifier()
    {
        var source = Preamble + @"
def main() -> None:
    try:
        raise Exception(""test"")
    except Exception as e:
        print(e)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("test");
    }

    [Theory]
    [InlineData("b.value", "attribute")]
    [InlineData("xs[0]", "index")]
    public void ExceptAs_NonName_Refused_SPY0142(string target, string desc)
    {
        var source = Preamble + $@"
def main() -> None:
    b: Box = Box(0)
    xs: list[int] = [0]
    try:
        pass
    except Exception as {target}:
        pass
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"except-as with {desc} target should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Parser.ExceptAsRequiresName,
            $"except-as with {desc} target should produce SPY0142");
    }

    // Except-as + tuple/starred: N/A — Python itself refuses these

    // ── Walrus position — REFUSED for non-names ──

    [Fact]
    public void Walrus_Identifier()
    {
        var source = Preamble + @"
def main() -> None:
    if (n := 42) > 0:
        print(n)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue();
        result.StandardOutput.Should().Contain("42");
    }

    [Theory]
    [InlineData("b.value", "attribute")]
    [InlineData("xs[0]", "index")]
    public void Walrus_NonName_Refused_SPY0143(string target, string desc)
    {
        var source = Preamble + $@"
def main() -> None:
    b: Box = Box(0)
    xs: list[int] = [0]
    x: int = ({target} := 5)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"walrus with {desc} target should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Parser.WalrusTargetRequiresName,
            $"walrus with {desc} target should produce SPY0143");
    }

    // Walrus + tuple/starred: N/A — walrus is always a single-name binding

    // ── Del position — REFUSED ──

    [Theory]
    [InlineData("x", "name")]
    [InlineData("b.value", "attribute")]
    [InlineData("xs[0]", "index")]
    public void Del_Refused_SPY0144(string target, string desc)
    {
        var source = Preamble + $@"
def main() -> None:
    x: int = 1
    b: Box = Box(0)
    xs: list[int] = [0]
    del {target}
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"del {desc} should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Parser.DelStatementNotSupported,
            $"del {desc} should produce SPY0144");
    }

    [Fact]
    public void Del_Slice_Refused_SPY0144_WithRebuildSteer()
    {
        // python3: `xs = [1, 2, 3, 4]; del xs[1:3]` leaves [1, 4]. A slice is not a subscript
        // pop, so the steer names the rebuild — not .pop(), and not slice assignment, which is
        // itself refused (SPY0225).
        var source = Preamble + @"
def main() -> None:
    xs: list[int] = [1, 2, 3, 4]
    del xs[1:3]
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("del of a slice should be refused");
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Parser.DelStatementNotSupported
            && d.Message.Contains("rebuilding the list", StringComparison.Ordinal),
            "a slice target steers to a rebuild, not to .pop()");
    }

    [Fact]
    public void Del_TargetList_ReportsOnePerTarget_WithPerTargetSteers()
    {
        // `del` takes a target *list*: python3 runs `del a, xs[0]` as two deletions. One
        // diagnostic chosen from the outer node would steer one of the two targets wrongly,
        // so each target is reported at its own span with its own steer (#1672).
        var source = Preamble + @"
def main() -> None:
    a: int = 1
    xs: list[int] = [10, 20]
    del a, xs[0]
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("del of a target list should be refused");

        var delDiagnostics = result.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.Parser.DelStatementNotSupported)
            .ToList();

        delDiagnostics.Should().HaveCount(2, "one SPY0144 per target, not one for the statement");
        delDiagnostics.Should().Contain(d => d.Message.Contains("unbinding a name", StringComparison.Ordinal),
            "the name target keeps the Axiom 1 steer");
        delDiagnostics.Should().Contain(d => d.Message.Contains(".pop(index)", StringComparison.Ordinal),
            "the index target gets the pop steer even though it is not the first target");
    }

    [Fact]
    public void Del_TargetList_IndexFirst_StillSteersEachTarget()
    {
        // The mirror ordering: picking the steer from the outer node made the order decide the
        // message, so both orderings are cells.
        var source = Preamble + @"
def main() -> None:
    a: int = 1
    xs: list[int] = [10, 20]
    del xs[0], a
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse("del of a target list should be refused");

        var delDiagnostics = result.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.Parser.DelStatementNotSupported)
            .ToList();

        delDiagnostics.Should().HaveCount(2);
        delDiagnostics.Should().Contain(d => d.Message.Contains(".pop(index)", StringComparison.Ordinal));
        delDiagnostics.Should().Contain(d => d.Message.Contains("unbinding a name", StringComparison.Ordinal));
    }

    // Del + starred: N/A — `del *a` is a SyntaxError in python3 as well

    // ── N/A cells documentation ──
    // The following cells are N/A (not applicable) and intentionally untested. "Uncommon" is not a
    // reason — an N/A must be a cell that cannot exist. Three former N/A entries (tuple element ×
    // attribute, tuple element × index, with-as × tuple) were N/A on false premises and are now
    // running cells above (#1672 E2).
    //   - Starred × attribute/index: starred unpack target is always a name
    //   - For statement + starred: Python does not support `for *rest, last in ...`
    //   - Comprehension + starred: Python does not support starred targets in comprehension for-clauses
    //   - With-as + starred: `with CM() as *rest` is a SyntaxError in Python too
    //   - Except-as + tuple/starred: Python itself refuses these
    //   - Walrus + tuple/starred: walrus is always a single-name binding
    //   - Del + starred: `del *a` is a SyntaxError in python3 as well

    // ── Declared-type stores (#1706): the generated product ──
    //
    // Contract: a value entering a typed slot is admitted or refused against the slot's DECLARED
    // type. The assigned value's type and control-flow facts are READ narrowings and must not
    // change what a store admits.
    //
    // Axes (the product is generated, not hand-listed, so a dropped arm cannot hide):
    //   target  {local, self.field, obj.field, tuple-element attribute, walrus, index}
    //   source  {prior store, assert x is not None, if isinstance(x, T)}   — how the slot narrowed
    //   store   {None into a `T | None` slot, a wider subtype into an `object`-typed slot}
    // 6 × 3 × 2 = 36 cells, each a complete program that RUNS and prints a discriminating value:
    // with the defect present the cell does not print, it reports SPY0229/SPY0220.
    //
    // All 36 cells are live. Three index cells were known-red under #1756 (the index position
    // checked the store against the predicate-narrowed element type) until 8633bcfbb gave
    // CheckIndexAccess the same plain-store-target arm CheckMemberAccess has; the ratchet drained
    // in the same round. `MatrixIsTotalOverItsAxes` counts live + N/A against the declared axes,
    // so the list cannot silently shrink.
    //
    // Shapes that had to be chosen, with the reason (each is a defect elsewhere, not the property
    // under test):
    //   - the tuple-element cells store a `str | None`/`object` VARIABLE rather than a bare `None`
    //     literal: `b.v, n = None, 1` emits `var __t = (null, 1)` and ICEs with CS0815 behind
    //     SPY0908 (#1707), which would make the cell measure tuple-literal emission instead.
    //   - the walrus cells read the target AFTER the narrowing block ends; a read INSIDE the block
    //     used to cast to the stale narrowed type (#1757, fixed at 8633bcfbb — NarrowingFlowAnalysis
    //     now kills a walrus target's facts). `WalrusStoreInvalidatesNarrowing_1757` below is the
    //     inside-the-block cell.

    private enum StoreTargetKind { Local, SelfField, ObjField, TupleElementAttribute, Walrus, Index }

    private enum NarrowingSourceKind { PriorStore, Assert, IsInstance }

    private enum StoreShapeKind { NoneIntoNullable, WiderIntoObject }

    private sealed record StoreShape(
        string Slot,
        string Seed,
        string Stored,
        string IsInstanceType,
        string WalrusTest,
        string TupleVarDecl,
        string TupleVar,
        string ExpectedOutput)
    {
        public string Probe(string name) =>
            Slot == "object" ? name : name + " is None";
    }

    private static StoreShape ShapeOf(StoreShapeKind kind) => kind switch
    {
        StoreShapeKind.NoneIntoNullable => new StoreShape(
            Slot: "str | None", Seed: "\"a\"", Stored: "None", IsInstanceType: "str",
            WalrusTest: "is None", TupleVarDecl: "empty: str | None = None", TupleVar: "empty",
            ExpectedOutput: "True"),
        StoreShapeKind.WiderIntoObject => new StoreShape(
            Slot: "object", Seed: "1", Stored: "\"s\"", IsInstanceType: "int",
            WalrusTest: "is not None", TupleVarDecl: "wide: object = \"s\"", TupleVar: "wide",
            ExpectedOutput: "s"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private sealed record StoreProductCell(
        StoreTargetKind Target,
        NarrowingSourceKind Source,
        StoreShapeKind Store,
        string Program,
        string ExpectedOutput)
    {
        public string Id => $"{Target}/{Source}/{Store}";
    }

    /// <summary>N/A cells: none. Every (target, source, store) triple is expressible.</summary>
    private static readonly (string Cell, string Reason)[] StoreProductNaCells = Array.Empty<(string, string)>();

    private static void AppendNarrowAndStore(
        List<string> lines, NarrowingSourceKind source, string name, StoreShape shape,
        IEnumerable<string> storeLines, string indent)
    {
        // Every source starts from the same prior store, so the three cells of a row differ only
        // in the narrowing construct layered on top of it.
        lines.Add($"{indent}{name} = {shape.Seed}");
        var storeIndent = indent;
        switch (source)
        {
            case NarrowingSourceKind.PriorStore:
                break;
            case NarrowingSourceKind.Assert:
                lines.Add($"{indent}assert {name} is not None");
                break;
            case NarrowingSourceKind.IsInstance:
                lines.Add($"{indent}if isinstance({name}, {shape.IsInstanceType}):");
                storeIndent = indent + "    ";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }

        foreach (var line in storeLines)
            lines.Add(storeIndent + line);
    }

    private static string BuildStoreProductProgram(
        StoreTargetKind target, NarrowingSourceKind source, StoreShapeKind store)
    {
        var s = ShapeOf(store);
        var lines = new List<string>();

        switch (target)
        {
            case StoreTargetKind.Local:
                lines.Add("def main() -> None:");
                lines.Add($"    x: {s.Slot} = {s.Seed}");
                AppendNarrowAndStore(lines, source, "x", s, new[] { $"x = {s.Stored}" }, "    ");
                lines.Add($"    print({s.Probe("x")})");
                break;

            case StoreTargetKind.SelfField:
                lines.Add("class NBox:");
                lines.Add($"    v: {s.Slot} = {s.Seed}");
                lines.Add("");
                lines.Add("    def mutate(self) -> None:");
                AppendNarrowAndStore(lines, source, "self.v", s,
                    new[] { $"self.v = {s.Stored}" }, "        ");
                lines.Add("");
                lines.Add("def main() -> None:");
                lines.Add("    b: NBox = NBox()");
                lines.Add("    b.mutate()");
                lines.Add($"    print({s.Probe("b.v")})");
                break;

            case StoreTargetKind.ObjField:
                lines.Add("class NBox:");
                lines.Add($"    v: {s.Slot} = {s.Seed}");
                lines.Add("");
                lines.Add("def main() -> None:");
                lines.Add("    b: NBox = NBox()");
                AppendNarrowAndStore(lines, source, "b.v", s, new[] { $"b.v = {s.Stored}" }, "    ");
                lines.Add($"    print({s.Probe("b.v")})");
                break;

            case StoreTargetKind.TupleElementAttribute:
                lines.Add("class NBox:");
                lines.Add($"    v: {s.Slot} = {s.Seed}");
                lines.Add("");
                lines.Add("def main() -> None:");
                lines.Add("    b: NBox = NBox()");
                lines.Add("    n: int = 0");
                lines.Add($"    {s.TupleVarDecl}");
                AppendNarrowAndStore(lines, source, "b.v", s,
                    new[] { $"b.v, n = {s.TupleVar}, 1" }, "    ");
                lines.Add($"    print({s.Probe("b.v")})");
                lines.Add("    print(n)");
                break;

            case StoreTargetKind.Walrus:
                lines.Add("def main() -> None:");
                lines.Add($"    x: {s.Slot} = {s.Seed}");
                AppendNarrowAndStore(lines, source, "x", s,
                    new[] { $"if (x := {s.Stored}) {s.WalrusTest}:", "    pass" }, "    ");
                lines.Add($"    print({s.Probe("x")})");
                break;

            case StoreTargetKind.Index:
                lines.Add("def main() -> None:");
                lines.Add($"    d: dict[str, {s.Slot}] = {{\"k\": {s.Seed}}}");
                AppendNarrowAndStore(lines, source, "d[\"k\"]", s,
                    new[] { $"d[\"k\"] = {s.Stored}" }, "    ");
                lines.Add($"    print({s.Probe("d[\"k\"]")})");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }

        return string.Join("\n", lines) + "\n";
    }

    private static IReadOnlyList<StoreProductCell> BuildStoreProduct()
    {
        var cells = new List<StoreProductCell>();

        foreach (var target in Enum.GetValues<StoreTargetKind>())
            foreach (var source in Enum.GetValues<NarrowingSourceKind>())
                foreach (var store in Enum.GetValues<StoreShapeKind>())
                {
                    cells.Add(new StoreProductCell(
                        target, source, store,
                        BuildStoreProductProgram(target, source, store),
                        ShapeOf(store).ExpectedOutput));
                }

        return cells;
    }

    public static IEnumerable<object[]> LiveStoreProductCells() =>
        BuildStoreProduct().Select(c => new object[] { c.Id });

    private static StoreProductCell CellById(string id) =>
        BuildStoreProduct().Single(c => c.Id == id);

    [Theory]
    [MemberData(nameof(LiveStoreProductCells))]
    public void DeclaredTypeStore_ProductCell_AdmitsTheStoreAgainstTheDeclaredType(string id)
    {
        var cell = CellById(id);
        var result = CompileAndExecute(cell.Program);

        result.Success.Should().BeTrue(
            $"{id} stores a value of the slot's DECLARED type\n{cell.Program}\n"
            + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Contain(cell.ExpectedOutput,
            $"{id} prints the stored value; the defect would print nothing and report SPY0229/SPY0220");
    }

    [Fact]
    public void DeclaredTypeStore_MatrixIsTotalOverItsAxes()
    {
        var cells = BuildStoreProduct();
        var targets = Enum.GetValues<StoreTargetKind>().Length;
        var sources = Enum.GetValues<NarrowingSourceKind>().Length;
        var stores = Enum.GetValues<StoreShapeKind>().Length;

        cells.Select(c => c.Id).Should().OnlyHaveUniqueItems("each cell appears once");

        // The axis sizes are anchored to literals ON PURPOSE. Deriving both the cell list and the
        // expected count from the same enums would make this assertion vacuous: deleting a member
        // of StoreTargetKind would shrink both sides and stay green. Widening or narrowing an axis
        // is a deliberate change to the class and must be made here as well.
        targets.Should().Be(6,
            "targets = local, self.field, obj.field, tuple-element attribute, walrus, index");
        sources.Should().Be(3, "sources = prior store, assert, isinstance");
        stores.Should().Be(2, "stores = None into a nullable slot, a wider subtype into an object slot");
        (cells.Count + StoreProductNaCells.Length).Should().Be(36,
            "live + N/A covers the declared axes; a dropped arm must fail here, not "
            + "leave the survivors green");
        (targets * sources * stores).Should().Be(36, "the axes and the cell count agree");

        foreach (var target in Enum.GetValues<StoreTargetKind>())
            foreach (var source in Enum.GetValues<NarrowingSourceKind>())
                foreach (var store in Enum.GetValues<StoreShapeKind>())
                    cells.Should().Contain(
                        c => c.Target == target && c.Source == source && c.Store == store,
                        $"{target} × {source} × {store} is a cell of the matrix");

        StoreProductNaCells.Should().OnlyContain(n => n.Reason.Length > 20,
            "every N/A cell states why, and 'uncommon' is not a reason");

        // The two shapes must actually differ in what they store, or the store axis is one axis.
        ShapeOf(StoreShapeKind.NoneIntoNullable).Stored.Should()
            .NotBe(ShapeOf(StoreShapeKind.WiderIntoObject).Stored);
    }

    [Fact]
    public void DeclaredTypeStore_Control_IndexStoreWithoutNarrowingIsAdmitted()
    {
        // Control for the three index cells that were #1756's known-reds: the same store, same
        // container, no narrowing — the product's index cells measure the narrowing leak, not the
        // store itself.
        var result = CompileAndExecute(@"
def main() -> None:
    d: dict[str, str | None] = {""k"": ""a""}
    d[""k""] = None
    print(d[""k""] is None)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Contain("True");
    }

    [Fact]
    public void DeclaredTypeStore_WalrusStoreInvalidatesNarrowing_1757()
    {
        // #1757 (fixed at 8633bcfbb): a walrus store re-versions its target AND kills the
        // isinstance narrowing, exactly as the statement form `x = "s"` does, so the read one
        // line later sees the new value instead of casting to the stale `int` and throwing.
        var result = CompileAndExecute(@"
def main() -> None:
    x: object = 1
    if isinstance(x, int):
        y: object = (x := ""s"")
        print(x)
");
        result.Success.Should().BeTrue(
            "#1757: the walrus store invalidates the narrowing\n" + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("s",
            "the read after the walrus sees the stored value, not a stale cast (InvalidCastException before the fix)");
    }

    [Fact]
    public void DeclaredTypeStore_Control_NonNullableDeclarationStillRefusesNone()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    y: str = ""a""
    y = None
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.NullabilityViolation,
            "the declared type is `str`; None is not a value of it");
    }

    [Fact]
    public void DeclaredTypeStore_Control_ReadsNarrowToTheStoredValue()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    x: str | None = None
    x = ""a""
    n: int = x
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch && d.Message.Contains("'str' to"),
            "after `x = \"a\"` a READ of x is the narrowed `str`, not the declared `str | None`");
    }

    // ── Mistyped-store product (#1768, #1755 B; plan-757fbb Phase 1 Task 6) ──
    //
    // Contract (Defect Class Row 1): a value entering a typed slot is admitted or refused by ONE
    // seam (TypeChecker.ClassifyStore / CheckStore) consulted at every store position, WHATEVER
    // SCOPE declared the target; the seam's verdict is what the user sees (SPY0220 / SPY0604 /
    // SPY0229), and SPY0908 — Roslyn refusing the generated C# — is never the refusing party.
    // Before the fix the CheckStore call in the identifier arm was gated on a SAME-SCOPE
    // predecessor, so a store to a name from an enclosing block, the enclosing function (closure)
    // or the module never met the seam: `d: int = 1; if True: d = "s"` ICEd CS0029,
    // `x: int = 42; def bump(): x = "s"` ICEd CS0029, and `d: int? = Some(10); if True: d = 5`
    // ran through the retired implicit Optional operator (an R-G violation).
    //
    // Axes (the product is generated; totality is anchored to literals in
    // MistypedStore_MatrixIsTotalOverItsAxes):
    //   target  {Local, SelfField, ObjField, TupleElementAttribute, Walrus, Index}         — 6
    //   scope   {Same, NestedIf, NarrowingBlock, WhileBody, ForBody, TryBody, WithBody,
    //            ElseBody, NestedDef (closure store to the enclosing function's name),
    //            ModuleLevel (a module-level name stored from inside a def)}               — 10
    //   family  {Optional `T?`, Nullable `T | None`, Plain `T`}                            — 3
    //   value   {PayloadConstant 5, Mistyped "s", NoneLiteral, NoneCall None(), SomeCall
    //            Some(5), NarrowedRead (a RemoveNone-narrowed `a` of the slot's own type)}  — 6
    // 6 × 10 × 3 × 6 = 1080 = live + N/A + known-red.
    //
    // Expected verdict — ONE function (MistypedStoreExpectationOf), so a cell cannot be hand-tuned:
    //   PayloadConstant  T?: SPY0604 (R-G — construct with Some), EXCEPT an identifier store (Local,
    //                        Walrus) inside `if x is not None:`, where the R-T payload rule re-wraps
    //                        and the cell prints 5. Member/index/tuple-element stores write the
    //                        declared slot even under a narrowing (spec: "self.x = Some(v) inside
    //                        if self.x is not None:" sees the declared slot).
    //                    T | None, T: prints 5
    //   Mistyped         T?: SPY0604; T | None, T: SPY0220
    //   NoneLiteral      T?: SPY0604 (bare None is not an Optional); T | None: prints None; T: SPY0229
    //   NoneCall         T?: prints None; T | None, T: SPY0244 — `None()` is classified BEFORE the
    //                        seam by CheckNoneConstruction against the pushed slot ("'None()' can
    //                        only construct Optional types, not 'int32 | None'"); plan-757fbb Row 1
    //                        keeps that refusal for every position
    //   SomeCall         T?: prints 5;    T | None, T: SPY0220 (an Optional into a non-Optional slot)
    //   NarrowedRead     T?, T | None: prints 1 — the wrapper passes through (R-T pass-through,
    //                        StoreVerdict.AcceptedNarrowedPassThrough); T: prints 1 (no wrapper to
    //                        narrow; a plain store — live, because "trivial" is not an N/A reason)
    //
    // N/A cells (declared with reasons): the scope axis names the scope that declared the TARGET
    // NAME. A field or element slot is declared by its class or container, so it has no
    // module-level cell: ModuleLevel × {SelfField, ObjField, TupleElementAttribute, Index} = 72.
    //
    // Binder forms are NOT cells: `for d in …`, `except … as d`, `match … case d` declare a fresh
    // block-scoped name (variable_scoping.md §Block Scoping) — the outer value prints after the
    // block (measured) — so there is no store into the outer slot for the seam to classify.
    //
    // Known-red cells (ratchet: each row cites its issue and is deleted when the issue is fixed —
    // MistypedStore_KnownRedCell_IsStillRedAsObserved goes red first, then the row goes and
    // MistypedStoreKnownRedCount is decremented). Measured @ 852bf488b + this change:
    //   #1784 (94): SomeCall × {T | None, T} × every target but the tuple element × every scope —
    //     `Some(5)` under a non-Optional expected type is refused SPY0230 "'Some' must be called
    //     as a function" by CheckIdentifier's bare-constructor arm, not by the seam (contract SPY0220).
    //   #1785 (90): the tuple-UNPACKING element position — (a) 54: × {NoneCall, SomeCall} × every
    //     family: the element slot is not pushed as the expected type, so Some(5)/None() cannot
    //     infer (SPY0227); (b) 27: × T? × {PayloadConstant, Mistyped, NoneLiteral} and (c) 9:
    //     × T × NoneLiteral: the position reports its own generic SPY0220 "… in tuple unpacking"
    //     instead of the seam's coded verdict (SPY0604 / SPY0229) — element twin of #1759.
    //   #1707 (9): TupleElementAttribute × NoneLiteral × T | None — `b.v, n = None, 1` emits
    //     `var __t = (null, 1)`, CS0815 behind SPY0908.
    //
    // Runtime note (measured @ 852bf488b + this change): the 1121 tests of this class ran in 66 s.

    private enum MistypedStoreScope
    {
        Same,
        NestedIf,
        NarrowingBlock,
        WhileBody,
        ForBody,
        TryBody,
        WithBody,
        ElseBody,
        NestedDef,
        ModuleLevel,
    }

    private enum SlotFamily { Optional, Nullable, Plain }

    private enum StoredValueKind { PayloadConstant, Mistyped, NoneLiteral, NoneCall, SomeCall, NarrowedRead }

    /// <summary>What a live cell must do: print <see cref="Output"/> (Code null) or be refused with <see cref="Code"/>.</summary>
    private sealed record MistypedStoreExpectation(string? Output, string? Code)
    {
        public static MistypedStoreExpectation Runs(string output) => new(output, null);
        public static MistypedStoreExpectation Refused(string code) => new(null, code);
    }

    private sealed record KnownRedStoreCell(string Issue, string ObservedCode, string Contract);

    private enum MistypedStoreCellKind { Live, NotApplicable, KnownRed }

    private sealed record MistypedStoreCell(
        StoreTargetKind Target,
        MistypedStoreScope Scope,
        SlotFamily Family,
        StoredValueKind Value,
        MistypedStoreCellKind Kind,
        string Program,
        MistypedStoreExpectation Expected,
        string? NaReason,
        KnownRedStoreCell? KnownRed)
    {
        public string Id => $"{Target}/{Scope}/{Family}/{Value}";
    }

    private const int MistypedStoreNaCount = 72;
    private const int MistypedStoreKnownRedCount = 193; // #1784: 94, #1785: 90, #1707: 9

    private static string SlotTypeOf(SlotFamily family) => family switch
    {
        SlotFamily.Optional => "int?",
        SlotFamily.Nullable => "int | None",
        SlotFamily.Plain => "int",
        _ => throw new ArgumentOutOfRangeException(nameof(family)),
    };

    private static string SeedOf(SlotFamily family, int value)
        => family == SlotFamily.Optional ? $"Some({value})" : value.ToString();

    private static string StoredValueSpelling(StoredValueKind value) => value switch
    {
        StoredValueKind.PayloadConstant => "5",
        StoredValueKind.Mistyped => "\"s\"",
        StoredValueKind.NoneLiteral => "None",
        StoredValueKind.NoneCall => "None()",
        StoredValueKind.SomeCall => "Some(5)",
        StoredValueKind.NarrowedRead => "a",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static MistypedStoreExpectation MistypedStoreExpectationOf(
        StoreTargetKind target, MistypedStoreScope scope, SlotFamily family, StoredValueKind value)
    {
        // The R-T payload rule is an IDENTIFIER-store rule (plain, walrus, tuple-element NAME,
        // augmented, ??=): member and index stores write the declared slot under a narrowing.
        var identifierStore = target is StoreTargetKind.Local or StoreTargetKind.Walrus;
        var payloadRuleApplies = scope == MistypedStoreScope.NarrowingBlock && identifierStore;

        return (value, family) switch
        {
            (StoredValueKind.PayloadConstant, SlotFamily.Optional) => payloadRuleApplies
                ? MistypedStoreExpectation.Runs("5")
                : MistypedStoreExpectation.Refused(DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction),
            (StoredValueKind.PayloadConstant, _) => MistypedStoreExpectation.Runs("5"),

            (StoredValueKind.Mistyped, SlotFamily.Optional)
                => MistypedStoreExpectation.Refused(DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction),
            (StoredValueKind.Mistyped, _) => MistypedStoreExpectation.Refused(DiagnosticCodes.Semantic.TypeMismatch),

            (StoredValueKind.NoneLiteral, SlotFamily.Optional)
                => MistypedStoreExpectation.Refused(DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction),
            (StoredValueKind.NoneLiteral, SlotFamily.Nullable) => MistypedStoreExpectation.Runs("None"),
            (StoredValueKind.NoneLiteral, SlotFamily.Plain)
                => MistypedStoreExpectation.Refused(DiagnosticCodes.Semantic.NullabilityViolation),

            (StoredValueKind.NoneCall, SlotFamily.Optional) => MistypedStoreExpectation.Runs("None"),
            (StoredValueKind.NoneCall, _) => MistypedStoreExpectation.Refused(DiagnosticCodes.Semantic.InvalidNoneConstructor),

            (StoredValueKind.SomeCall, SlotFamily.Optional) => MistypedStoreExpectation.Runs("5"),
            (StoredValueKind.SomeCall, _) => MistypedStoreExpectation.Refused(DiagnosticCodes.Semantic.TypeMismatch),

            (StoredValueKind.NarrowedRead, _) => MistypedStoreExpectation.Runs("1"),

            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
    }

    private static string? MistypedStoreNaReasonOf(StoreTargetKind target, MistypedStoreScope scope)
    {
        if (scope == MistypedStoreScope.ModuleLevel
            && target is StoreTargetKind.SelfField or StoreTargetKind.ObjField
                or StoreTargetKind.TupleElementAttribute or StoreTargetKind.Index)
        {
            return "the scope axis names the scope that declared the TARGET NAME; a field or element "
                + "slot is declared by its class or container, so 'a module-level name stored from a "
                + "def' has no cell for this target — the receiver's own scope does not enter the seam";
        }

        return null;
    }

    private static KnownRedStoreCell? MistypedStoreKnownRedOf(
        StoreTargetKind target, MistypedStoreScope scope, SlotFamily family, StoredValueKind value)
    {
        if (target == StoreTargetKind.TupleElementAttribute)
        {
            // The tuple-UNPACKING element position (#1785): the slot is not pushed, and the
            // refusal is the position's own SPY0220 "… in tuple unpacking", not the seam's verdict.
            if (value is StoredValueKind.NoneCall or StoredValueKind.SomeCall)
            {
                return new KnownRedStoreCell(
                    Issue: "#1785",
                    ObservedCode: DiagnosticCodes.Semantic.CannotInferType,
                    Contract: family == SlotFamily.Optional
                        ? "runs — the element slot is int? and Some(5)/None() infer from it"
                        : "SPY0220 — an Optional into a non-Optional element slot");
            }

            if (value == StoredValueKind.NoneLiteral && family == SlotFamily.Nullable)
            {
                return new KnownRedStoreCell(
                    Issue: "#1707",
                    ObservedCode: DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
                    Contract: "runs and prints None — `var __t = (null, 1)` is CS0815 today");
            }

            if (family == SlotFamily.Optional
                && value is StoredValueKind.PayloadConstant or StoredValueKind.Mistyped or StoredValueKind.NoneLiteral)
            {
                return new KnownRedStoreCell(
                    Issue: "#1785",
                    ObservedCode: DiagnosticCodes.Semantic.TypeMismatch,
                    Contract: "SPY0604 — the seam's strict-Optional verdict with the Some(...) steer, not a generic SPY0220");
            }

            if (family == SlotFamily.Plain && value == StoredValueKind.NoneLiteral)
            {
                return new KnownRedStoreCell(
                    Issue: "#1785",
                    ObservedCode: DiagnosticCodes.Semantic.TypeMismatch,
                    Contract: "SPY0229 — None into a non-nullable slot is the nullability refusal, not a generic SPY0220");
            }

            return null;
        }

        if (value == StoredValueKind.SomeCall && family != SlotFamily.Optional)
        {
            return new KnownRedStoreCell(
                Issue: "#1784",
                ObservedCode: DiagnosticCodes.Semantic.NotCallable,
                Contract: "SPY0220 — the seam refuses an Optional into a non-Optional slot; "
                    + "'Some' must be called as a function is the wrong arm");
        }

        return null;
    }

    /// <summary>
    /// Wraps <paramref name="storeLines"/> in the scope construct. <paramref name="targetRead"/> is
    /// the target as a read (for the narrowing header); <paramref name="indent"/> is the indentation
    /// of the host body. ModuleLevel is a host shape (the store sits directly in a def whose name
    /// was declared at module level), so it wraps nothing here.
    /// </summary>
    private static void AppendScopedStore(
        List<string> lines, MistypedStoreScope scope, string targetRead,
        IReadOnlyList<string> storeLines, string indent)
    {
        var inner = indent + "    ";
        void Body(string bodyIndent)
        {
            foreach (var line in storeLines)
                lines.Add(bodyIndent + line);
        }

        switch (scope)
        {
            case MistypedStoreScope.Same:
            case MistypedStoreScope.ModuleLevel:
                Body(indent);
                break;
            case MistypedStoreScope.NestedIf:
                lines.Add($"{indent}if True:");
                Body(inner);
                break;
            case MistypedStoreScope.NarrowingBlock:
                lines.Add($"{indent}if {targetRead} is not None:");
                Body(inner);
                break;
            case MistypedStoreScope.WhileBody:
                lines.Add($"{indent}k: int = 0");
                lines.Add($"{indent}while k < 1:");
                Body(inner);
                lines.Add($"{inner}k += 1");
                break;
            case MistypedStoreScope.ForBody:
                lines.Add($"{indent}for i in range(1):");
                Body(inner);
                break;
            case MistypedStoreScope.TryBody:
                lines.Add($"{indent}try:");
                Body(inner);
                lines.Add($"{indent}except Exception:");
                lines.Add($"{inner}pass");
                break;
            case MistypedStoreScope.WithBody:
                lines.Add($"{indent}with CM() as w:");
                Body(inner);
                break;
            case MistypedStoreScope.ElseBody:
                lines.Add($"{indent}k: int = 0");
                lines.Add($"{indent}if k > 5:");
                lines.Add($"{inner}pass");
                lines.Add($"{indent}else:");
                Body(inner);
                break;
            case MistypedStoreScope.NestedDef:
                lines.Add($"{indent}def inner() -> None:");
                Body(inner);
                lines.Add($"{indent}inner()");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scope));
        }
    }

    private static string BuildMistypedStoreProgram(
        StoreTargetKind target, MistypedStoreScope scope, SlotFamily family, StoredValueKind value)
    {
        var slot = SlotTypeOf(family);
        // The target's seed (3) differs from every stored value (5 / None / 1) so a dropped store
        // prints a value no cell expects.
        var seed = SeedOf(family, 3);
        var narrowedSeed = SeedOf(family, 1);
        var v = StoredValueSpelling(value);
        var needsNarrowedRead = value == StoredValueKind.NarrowedRead;
        var lines = new List<string>();

        void DeclareNarrowedRead(string indent)
        {
            if (!needsNarrowedRead)
                return;
            lines.Add($"{indent}a: {slot} = {narrowedSeed}");
            lines.Add($"{indent}assert a is not None");
        }

        switch (target)
        {
            case StoreTargetKind.Local:
            case StoreTargetKind.Walrus:
                {
                    var storeLine = target == StoreTargetKind.Local ? $"x = {v}" : $"print((x := {v}))";
                    if (scope == MistypedStoreScope.ModuleLevel)
                    {
                        lines.Add($"x: {slot} = {seed}");
                        if (needsNarrowedRead)
                            lines.Add($"a: {slot} = {narrowedSeed}");
                        lines.Add("");
                        lines.Add("def store() -> None:");
                        if (needsNarrowedRead)
                            lines.Add("    assert a is not None");
                        lines.Add($"    {storeLine}");
                        lines.Add("");
                        lines.Add("def main() -> None:");
                        lines.Add("    store()");
                        lines.Add("    print(x)");
                    }
                    else
                    {
                        lines.Add("def main() -> None:");
                        lines.Add($"    x: {slot} = {seed}");
                        DeclareNarrowedRead("    ");
                        AppendScopedStore(lines, scope, "x", new[] { storeLine }, "    ");
                        lines.Add("    print(x)");
                    }
                    break;
                }

            case StoreTargetKind.SelfField:
                lines.Add("class NBox:");
                lines.Add($"    v: {slot} = {seed}");
                lines.Add("");
                lines.Add("    def mutate(self) -> None:");
                DeclareNarrowedRead("        ");
                AppendScopedStore(lines, scope, "self.v", new[] { $"self.v = {v}" }, "        ");
                lines.Add("");
                lines.Add("def main() -> None:");
                lines.Add("    b: NBox = NBox()");
                lines.Add("    b.mutate()");
                lines.Add("    print(b.v)");
                break;

            case StoreTargetKind.ObjField:
            case StoreTargetKind.TupleElementAttribute:
                {
                    lines.Add("class NBox:");
                    lines.Add($"    v: {slot} = {seed}");
                    lines.Add("");
                    lines.Add("def main() -> None:");
                    lines.Add("    b: NBox = NBox()");
                    if (target == StoreTargetKind.TupleElementAttribute)
                        lines.Add("    n: int = 0");
                    DeclareNarrowedRead("    ");
                    var store = target == StoreTargetKind.ObjField ? $"b.v = {v}" : $"b.v, n = {v}, 1";
                    AppendScopedStore(lines, scope, "b.v", new[] { store }, "    ");
                    lines.Add("    print(b.v)");
                    break;
                }

            case StoreTargetKind.Index:
                lines.Add("def main() -> None:");
                lines.Add($"    d: dict[str, {slot}] = {{\"k\": {seed}}}");
                DeclareNarrowedRead("    ");
                AppendScopedStore(lines, scope, "d[\"k\"]", new[] { $"d[\"k\"] = {v}" }, "    ");
                lines.Add("    print(d[\"k\"])");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }

        return Preamble + string.Join("\n", lines) + "\n";
    }

    private static IReadOnlyList<MistypedStoreCell> BuildMistypedStoreProduct()
    {
        var cells = new List<MistypedStoreCell>();

        foreach (var target in Enum.GetValues<StoreTargetKind>())
            foreach (var scope in Enum.GetValues<MistypedStoreScope>())
                foreach (var family in Enum.GetValues<SlotFamily>())
                    foreach (var value in Enum.GetValues<StoredValueKind>())
                    {
                        var naReason = MistypedStoreNaReasonOf(target, scope);
                        var knownRed = naReason == null ? MistypedStoreKnownRedOf(target, scope, family, value) : null;
                        var kind = naReason != null ? MistypedStoreCellKind.NotApplicable
                            : knownRed != null ? MistypedStoreCellKind.KnownRed
                            : MistypedStoreCellKind.Live;

                        cells.Add(new MistypedStoreCell(
                            target, scope, family, value, kind,
                            Program: naReason == null ? BuildMistypedStoreProgram(target, scope, family, value) : string.Empty,
                            Expected: MistypedStoreExpectationOf(target, scope, family, value),
                            NaReason: naReason,
                            KnownRed: knownRed));
                    }

        return cells;
    }

    public static IEnumerable<object[]> LiveMistypedStoreCells()
        => BuildMistypedStoreProduct().Where(c => c.Kind == MistypedStoreCellKind.Live).Select(c => new object[] { c.Id });

    public static IEnumerable<object[]> KnownRedMistypedStoreCells()
        => BuildMistypedStoreProduct().Where(c => c.Kind == MistypedStoreCellKind.KnownRed).Select(c => new object[] { c.Id });

    private static MistypedStoreCell MistypedStoreCellById(string id)
        => BuildMistypedStoreProduct().Single(c => c.Id == id);

    /// <summary>
    /// SPY0908 is raised at the C# compile stage; IntegrationTestBase surfaces that stage's failure as
    /// Roslyn's own `error CSxxxx` strings with no Sharpy RawDiagnostics. Either spelling is an ICE.
    /// </summary>
    private static bool IsIce(ExecutionResult result)
        => !result.Success
            && (result.RawDiagnostics.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)
                || result.CompilationErrors.Any(e => e.Contains("error CS", StringComparison.Ordinal)
                    || e.Contains("SPY0908", StringComparison.Ordinal)));

    private static string LastOutputLine(ExecutionResult result)
        => result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? string.Empty;

    [Theory]
    [MemberData(nameof(LiveMistypedStoreCells))]
    public void MistypedStore_ProductCell_IsDecidedByTheSeam(string id)
    {
        var cell = MistypedStoreCellById(id);
        var result = CompileAndExecute(cell.Program);

        IsIce(result).Should().BeFalse(
            $"{id}: SPY0908 is never the refusing party — the seam decides at every scope\n{cell.Program}\n"
            + string.Join("\n", result.CompilationErrors));

        if (cell.Expected.Code == null)
        {
            result.Success.Should().BeTrue(
                $"{id} is an admitted store and must run\n{cell.Program}\n" + string.Join("\n", result.CompilationErrors));
            // The LAST line is the target read after the store (the walrus host prints the value
            // first); a dropped store would print the seed 3, which no cell expects.
            LastOutputLine(result).Should().Be(cell.Expected.Output,
                $"{id} prints the stored value through the declared slot\n{cell.Program}");
        }
        else
        {
            result.Success.Should().BeFalse($"{id} is a refused store\n{cell.Program}");
            result.RawDiagnostics.Should().Contain(d => d.Code == cell.Expected.Code,
                $"{id} is refused by the seam with {cell.Expected.Code}, not by Roslyn\n{cell.Program}\n"
                + string.Join("\n", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
        }
    }

    /// <summary>
    /// The ratchet. A known-red row must still fail the way it was observed; when its issue is
    /// fixed this goes red, the row is deleted and MistypedStoreKnownRedCount is decremented.
    /// </summary>
    [Theory]
    [MemberData(nameof(KnownRedMistypedStoreCells))]
    public void MistypedStore_KnownRedCell_IsStillRedAsObserved(string id)
    {
        var cell = MistypedStoreCellById(id);
        var red = cell.KnownRed!;
        var result = CompileAndExecute(cell.Program);

        var observed = red.ObservedCode == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
            ? IsIce(result)
            : result.RawDiagnostics.Any(d => d.Code == red.ObservedCode);

        observed.Should().BeTrue(
            $"{id} is known red under {red.Issue} with {red.ObservedCode} (contract: {red.Contract}). "
            + "If this fails the issue is fixed: delete the row from MistypedStoreKnownRedOf and "
            + $"decrement MistypedStoreKnownRedCount\n{cell.Program}\n"
            + string.Join("\n", result.CompilationErrors));
    }

    [Fact]
    public void MistypedStore_MatrixIsTotalOverItsAxes()
    {
        var cells = BuildMistypedStoreProduct();
        var targets = Enum.GetValues<StoreTargetKind>().Length;
        var scopes = Enum.GetValues<MistypedStoreScope>().Length;
        var families = Enum.GetValues<SlotFamily>().Length;
        var values = Enum.GetValues<StoredValueKind>().Length;

        cells.Select(c => c.Id).Should().OnlyHaveUniqueItems("each cell appears once");

        // Literals ON PURPOSE (see DeclaredTypeStore_MatrixIsTotalOverItsAxes): deriving the count
        // from the enums would let a deleted axis member shrink both sides and stay green.
        targets.Should().Be(6, "targets = local, self.field, obj.field, tuple-element attribute, walrus, index");
        scopes.Should().Be(10,
            "scopes = same, nested if, narrowing block, while, for, try, with, else, nested def, module level");
        families.Should().Be(3, "families = T?, T | None, T");
        values.Should().Be(6, "values = payload constant, mistyped, None, None(), Some(v), narrowed read");
        (targets * scopes * families * values).Should().Be(1080, "the axes and the cell count agree");

        var live = cells.Count(c => c.Kind == MistypedStoreCellKind.Live);
        var na = cells.Count(c => c.Kind == MistypedStoreCellKind.NotApplicable);
        var red = cells.Count(c => c.Kind == MistypedStoreCellKind.KnownRed);
        na.Should().Be(MistypedStoreNaCount, "N/A = ModuleLevel × the four field/element targets × 3 × 6");
        red.Should().Be(MistypedStoreKnownRedCount, "known-red rows drain on fix (#1784: 94, #1785: 90, #1707: 9)");
        cells.Count(c => c.Kind == MistypedStoreCellKind.KnownRed && c.KnownRed!.Issue == "#1784").Should().Be(94);
        cells.Count(c => c.Kind == MistypedStoreCellKind.KnownRed && c.KnownRed!.Issue == "#1785").Should().Be(90);
        cells.Count(c => c.Kind == MistypedStoreCellKind.KnownRed && c.KnownRed!.Issue == "#1707").Should().Be(9);
        (live + na + red).Should().Be(1080,
            "live + N/A + known-red covers the declared axes; a dropped arm must fail here");

        foreach (var target in Enum.GetValues<StoreTargetKind>())
            foreach (var scope in Enum.GetValues<MistypedStoreScope>())
                foreach (var family in Enum.GetValues<SlotFamily>())
                    foreach (var value in Enum.GetValues<StoredValueKind>())
                        cells.Should().Contain(
                            c => c.Target == target && c.Scope == scope && c.Family == family && c.Value == value,
                            $"{target} × {scope} × {family} × {value} is a cell of the matrix");

        cells.Where(c => c.Kind == MistypedStoreCellKind.NotApplicable)
            .Should().OnlyContain(c => c.NaReason!.Length > 40, "every N/A cell states why, and 'uncommon' is not a reason");
        cells.Where(c => c.Kind == MistypedStoreCellKind.KnownRed)
            .Should().OnlyContain(c => System.Text.RegularExpressions.Regex.IsMatch(c.KnownRed!.Issue, "^#[0-9]+$"),
                "every known-red row cites an issue");

        // The expectation function must vary along every axis, or an axis is decoration.
        var expectations = cells.Where(c => c.Kind == MistypedStoreCellKind.Live).Select(c => c.Expected).Distinct().ToList();
        expectations.Should().Contain(e => e.Code == DiagnosticCodes.Semantic.TypeMismatch);
        expectations.Should().Contain(e => e.Code == DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction);
        expectations.Should().Contain(e => e.Code == DiagnosticCodes.Semantic.NullabilityViolation);
        expectations.Should().Contain(e => e.Output == "5");
        expectations.Should().Contain(e => e.Output == "None");
        expectations.Should().Contain(e => e.Output == "1");
        cells.Where(c => c.Kind == MistypedStoreCellKind.Live && c.Value == StoredValueKind.PayloadConstant
                && c.Family == SlotFamily.Optional)
            .Select(c => c.Expected.Code == null).Distinct().Should().HaveCount(2,
                "the narrowing-block scope changes the payload-constant verdict for identifier stores");
    }

    /// <summary>
    /// Positive control for the ICE probe: <see cref="IsIce"/> must fire on a program that DOES ICE
    /// today (#1707), or the "SPY0908 in no cell" assertion is vacuous. When #1707 is fixed this
    /// control needs a new known ICE or is retired with the row.
    /// </summary>
    [Fact]
    public void MistypedStore_Control_IceProbeFiresOnAKnownIce_1707()
    {
        var result = CompileAndExecute(@"
class NBox:
    v: int | None = 1

def main() -> None:
    b: NBox = NBox()
    n: int = 0
    b.v, n = None, 1
    print(b.v)
");
        IsIce(result).Should().BeTrue(
            "#1707: `var __t = (null, 1)` is CS0815 behind SPY0908 — the probe must see it\n"
            + string.Join("\n", result.CompilationErrors));
    }

    // ── Survival cells (R-T): the narrowing outlives a payload store ──

    [Fact]
    public void NarrowedStore_AugmentedStoreKeepsTheNarrowing_ReadIsThePayload()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    x: int? = Some(10)
    if x is not None:
        x += 5
        n: int = x
        print(n)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("15");
    }

    [Fact]
    public void NarrowedStore_PayloadStoreRewrapsAndTheNarrowingSurvives()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    d: int? = Some(1)
    if d is not None:
        d = 5
        e: int = d + 1
        print(e)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("6");
    }

    [Fact]
    public void NarrowedStore_PayloadStoreIsEmittedAsSomeConstruction_NotTheImplicitOperator()
    {
        // Recorded ≠ applied — and EXECUTING is not enough for this fact: Sharpy.Optional<T> still
        // carries the implicit T→Optional<T> operator, so a bare `d = 5;` compiles and prints 6 with
        // the OptionalStoreWrap fact unread. Mutation (b) of plan-757fbb Phase 1 (drop the
        // GetOptionalStoreWrap read in GenerateAssignment's plain-store path) left every executing
        // cell of this class green (measured @ 852bf488b + this change: 1123/0). The fact's applied
        // form is the emitted construction, so this cell reads the generated C# for both the plain
        // payload store and its augmented twin.
        var result = CompileAndExecute(@"
def main() -> None:
    d: int? = Some(1)
    if d is not None:
        d = 5
        e: int = d + 1
        print(e)
    x: int? = Some(10)
    if x is not None:
        x += 5
        print(x)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Split('\n').Should().Equal(new[] { "6", "15" });
        result.GeneratedCSharp.Should().NotBeNull();
        result.GeneratedCSharp!.Should().Contain("d = Optional<int>.Some(5);",
            "the plain payload store re-wraps through the recorded OptionalStoreWrap fact");
        result.GeneratedCSharp.Should().Contain("x = Optional<int>.Some(x.Unwrap() + 5);",
            "the augmented store re-wraps through the same fact");
        result.GeneratedCSharp.Should().NotContain("d = 5;",
            "a bare payload assignment relies on the retired implicit operator (R-G)");
    }

    [Fact]
    public void NarrowedStore_RebindingNarrowedReadOfValueTypeNullable_CarriesItsAccessor()
    {
        // Pre-existing CS0266 before the read-side accessor (Decision 3): the rebinding version is
        // `int`, the C# slot is `int?`, and the read needs `.Value`.
        var result = CompileAndExecute(@"
def main() -> None:
    n: int | None = None
    n = 5
    m: int = n
    print(m)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("5");
    }

    [Fact]
    public void NarrowedStore_PassThroughDoesNotNarrowTheTarget()
    {
        // `b = a` passes the Optional through (b's slot is int?); b is NOT narrowed by it, so a
        // payload read of b is SPY0220.
        var result = CompileAndExecute(@"
def main() -> None:
    a: int? = Some(1)
    b: int? = None()
    if a is not None:
        b = a
        c: int = b
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "b holds an Optional; reading it as int is a type mismatch, not an ICE");
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction,
            "the store itself is admitted — the wrapper passes through");
    }

    [Fact]
    public void NarrowedStore_ReadAfterTheBlockIsTheJoin_Spy0220()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    x: int? = Some(10)
    if x is not None:
        x += 5
    n: int = x
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "after the block x may be None again — the join drops the narrowing");
    }

    // ── R-T pass-through: a narrowed read into a slot of its own declared type (executing) ──

    public static IEnumerable<object[]> PassThroughPositions() => new[]
    {
        new object[] { "declaration", "        y: int? = a\n        print(y)" },
        new object[] { "plain store", "        b: int? = None()\n        b = a\n        print(b)" },
        new object[] { "argument", "        take(a)" },
        new object[] { "walrus", "        w: int? = None()\n        if (w := a) is not None:\n            print(w)" },
        new object[] { "self.field", "        h: H = H()\n        h.put(a)\n        print(h.v)" },
        new object[] { "index", "        d: dict[str, int?] = {\"k\": None()}\n        d[\"k\"] = a\n        print(d[\"k\"])" },
    };

    [Theory]
    [MemberData(nameof(PassThroughPositions))]
    public void NarrowedRead_IntoItsOwnDeclaredWrapper_PassesTheOptionalThrough(string position, string body)
    {
        // type_narrowing.md §Stores Use the Declared Type: "A narrowed read stored into a slot of
        // its own declared type passes the Optional through". At BASE (dff55b2cd) only the
        // nested-block LOCAL ran, through the seam skip; declaration, return, argument, self.field
        // and walrus were SPY0604 there too. One verdict (AcceptedNarrowedPassThrough) in
        // ClassifyStore covers every position; ApplyAcceptedVerdict drops the read's accessor so
        // the emitter prints `b = a`. Executing on purpose: recorded ≠ applied.
        var source = @"
class H:
    v: int? = None()
    def put(self, a: int?) -> None:
        if a is not None:
            self.v = a

def take(v: int?) -> None:
    print(v)

def ret(a: int?) -> int?:
    if a is not None:
        return a
    return None()

def main() -> None:
    a: int? = Some(1)
    if a is not None:
" + body + @"
    print(ret(Some(2)))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"[{position}] the wrapper passes through\n{source}\n" + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Split('\n').Should().Equal(new[] { "1", "2" }, $"[{position}] prints the passed value, then the returned one");
    }

    [Fact]
    public void NarrowedRead_IntoItsOwnDeclaredNullable_PassesThrough()
    {
        var result = CompileAndExecute(@"
def main() -> None:
    a: int | None = 1
    b: int | None = None
    if a is not None:
        b = a
    print(b)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("1");
    }

    // ── Block kind under a narrowing: the payload store survives in every body ──

    // Each body stores the payload and then READS it as the payload in the same body (`e: int = d + 1`):
    // the read is what needs the narrowing to have survived the store. A read after a try/except
    // is not a cell — the except edge contributes no facts, so the join after the statement is
    // empty by the dataflow's exception rule, store or no store.
    public static IEnumerable<object[]> NarrowedPayloadStoreBlockKinds() => new[]
    {
        new object[] { "try", "        try:\n            d = 5\n            e: int = d + 1\n            print(e)\n        except Exception:\n            pass" },
        new object[] { "with", "        with CM() as w:\n            d = 5\n            e: int = d + 1\n            print(e)" },
        new object[] { "for", "        for i in range(2):\n            d = 5\n            e: int = d + 1\n            print(e)" },
        new object[] { "while", "        k: int = 0\n        while k < 2:\n            d = 5\n            e: int = d + 1\n            print(e)\n            k += 1" },
        new object[] { "else", "        k: int = 0\n        if k > 5:\n            pass\n        else:\n            d = 5\n            e: int = d + 1\n            print(e)" },
        new object[] { "nested if", "        if True:\n            d = 5\n            e: int = d + 1\n            print(e)" },
        new object[] { "for + walrus", "        for i in range(2):\n            print((d := 5))\n            e: int = d + 1\n            print(e)" },
        new object[] { "for, read after the loop", "        for i in range(2):\n            d = 5\n        e: int = d + 1\n        print(e)" },
        new object[] { "while, read after the loop", "        k: int = 0\n        while k < 2:\n            d = 5\n            k += 1\n        e: int = d + 1\n        print(e)" },
    };

    [Theory]
    [MemberData(nameof(NarrowedPayloadStoreBlockKinds))]
    public void NarrowedPayloadStore_SurvivesEveryBlockKind(string block, string body)
    {
        // Measured @ 852bf488b: `try`/`with` bodies ran, `for`/`while` bodies were SPY0604 at the
        // STORE — the back edge killed the RemoveNone fact at the loop head, so the payload rule did
        // not apply on the second visit. NarrowingFlowAnalysis.Kill now keeps a RemoveNone fact
        // across a store whose value is definitely not None (a literal here). All ran at BASE
        // (dff55b2cd) — through the implicit operator, not the payload rule — so a red here is a
        // regression by direction.
        var source = Preamble + @"
def main() -> None:
    d: int? = Some(10)
    if d is not None:
" + body + @"
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"[{block}] the payload store keeps d narrowed\n{source}\n" + string.Join("\n", result.CompilationErrors));
        LastOutputLine(result).Should().Be("6", $"[{block}] d is 5 after the store and still narrowed");
    }

    public static IEnumerable<object[]> NarrowingEndingStores() => new[]
    {
        new object[] { "call returning int?", "d = g()" },
        new object[] { "Some(...)", "d = Some(5)" },
        new object[] { "None()", "d = None()" },
        new object[] { "un-narrowed int? name", "d = other" },
    };

    [Theory]
    [MemberData(nameof(NarrowingEndingStores))]
    public void NarrowedStore_OfAPossiblyNoneValue_EndsTheNarrowing_Spy0220(string shape, string store)
    {
        // The positive control for the survival rule: a value-blind survival would keep the fact
        // across `d = g()` and the next read would `.Unwrap()` a None at runtime. The dataflow keeps
        // the fact only for syntactically non-None values; these four are not, so `n: int = d` is
        // SPY0220 — the checker has an opinion (the read is int?), not an Unknown.
        var source = @"
def g() -> int?:
    return None()

def main() -> None:
    other: int? = None()
    d: int? = Some(1)
    if d is not None:
        " + store + @"
        n: int = d
        print(n)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"[{shape}] the narrowing ends\n{source}");
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            $"[{shape}] the read after the store is the declared int?\n" + string.Join("\n", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
    }

    // ── Nested-block value shapes: run or refuse exactly as the same-scope twin does ──

    public static IEnumerable<object[]> NestedBlockRunningShapes() => new[]
    {
        new object[] { "float32 literal", "f: float32 = 0.0", "f = 0.5", "f", "0.5" },
        new object[] { "int8 conditional of constants", "d: int8 = 1\n    c: bool = True", "d = 7 if c else 8", "d", "7" },
        new object[] { "int8 in-range constant", "d: int8 = 1", "d = 7", "d", "7" },
        new object[] { "literal into LiteralString", "s: LiteralString = \"a\"", "s = \"b\"", "s", "b" },
        new object[] { "None into T | None", "n: int | None = 1", "n = None", "n", "None" },
    };

    [Theory]
    [MemberData(nameof(NestedBlockRunningShapes))]
    public void NestedBlockStore_ValueShape_RunsAsTheSameScopeTwinDoes(string shape, string decl, string store, string read, string expected)
    {
        // @ dff55b2cd: float32 literal → CS0664, int8 conditional → CS0266 (no branch cast) —
        // the seam's constant/float32/conditional arms never ran for an enclosing-block target.
        var source = $@"
def main() -> None:
    {decl}
    if True:
        {store}
    print({read})
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"[{shape}]\n{source}\n" + string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be(expected, $"[{shape}]");
    }

    public static IEnumerable<object[]> NestedBlockRefusedShapes() => new[]
    {
        new object[] { "out-of-range constant into int8", "d: int8 = 1", "d = 300", DiagnosticCodes.Semantic.TypeMismatch },
        new object[] { "str variable into LiteralString", "s: LiteralString = \"a\"\n    v: str = \"b\"", "s = v", DiagnosticCodes.Semantic.TypeMismatch },
        new object[] { "None into int", "d: int = 1", "d = None", DiagnosticCodes.Semantic.NullabilityViolation },
        new object[] { "str into int", "d: int = 1", "d = \"s\"", DiagnosticCodes.Semantic.TypeMismatch },
        new object[] { "bare int into int?", "d: int? = Some(10)", "d = 5", DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction },
    };

    [Theory]
    [MemberData(nameof(NestedBlockRefusedShapes))]
    public void NestedBlockStore_ValueShape_IsRefusedAsTheSameScopeTwinIs(string shape, string decl, string store, string code)
    {
        // @ dff55b2cd: `d = 300` → CS0031, `s = v` → RAN (a non-literal str silently admitted into a
        // LiteralString slot), `d = "s"` → CS0029, `d = 5` into int? → ran via the implicit operator.
        var source = $@"
def main() -> None:
    {decl}
    if True:
        {store}
    print(0)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"[{shape}]\n{source}");
        IsIce(result).Should().BeFalse($"[{shape}] the seam refuses, not Roslyn\n" + string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().Contain(d => d.Code == code,
            $"[{shape}]\n" + string.Join("\n", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
    }

    // ── Module-level names stored from a function: the write-through cells of Part A ──

    [Fact]
    public void ModuleLevelName_MistypedStoreFromAFunction_IsSpy0220NotAnIce()
    {
        // @ dff55b2cd and @ 852bf488b: SPY0908 / CS0029 — the checker treated the store as a new
        // local while the emitter assigned the static field (variable_scoping.md §Write-Through).
        var result = CompileAndExecute(@"
x: int = 42

def bump() -> None:
    x = ""s""

def main() -> None:
    bump()
    print(x)
");
        result.Success.Should().BeFalse();
        IsIce(result).Should().BeFalse(string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch);
    }

    [Fact]
    public void ModuleLevelName_BarePayloadIntoOptionalFromAFunction_IsSpy0604()
    {
        // @ dff55b2cd and @ 852bf488b: ran and printed 5 through the retired implicit operator (R-G);
        // the same store in a nested block was already SPY0604.
        var result = CompileAndExecute(@"
d: int? = Some(1)

def f() -> None:
    d = 5

def main() -> None:
    f()
    print(d)
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.SemanticOverflow.StrictOptionalConstruction);
    }

    [Fact]
    public void ModuleLevelName_AnnotatedDeclarationInAFunction_Shadows()
    {
        // The spec's shadowing form; unchanged at all three binaries.
        var result = CompileAndExecute(@"
x: int = 42

def f() -> None:
    x: str = ""s""
    print(x)

def main() -> None:
    f()
    print(x)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Split('\n').Should().Equal(new[] { "s", "42" });
    }

    // ── Multi-file: the node-keyed facts survive SemanticInfo.MergeFrom ──

    [Fact]
    public void MultiFile_OptionalStoreWrapAndPassThroughFacts_SurviveMergeFrom()
    {
        // The wrap fact (SetOptionalStoreWrap) and the pass-through record are node-keyed
        // SemanticInfo dictionaries; a dictionary missing from MergeFrom is silently dropped in
        // the per-file → project merge code generation reads from (CLAUDE.md Rule 2). Recorded ≠
        // applied: the program runs and prints.
        using var helper = new ProjectCompilationHelper(Output);
        helper.WithRootNamespace("StoreSeamMerge");
        helper.AddSourceFile("lib.spy",
            "def bump(x: int?) -> int:\n"
            + "    if x is not None:\n"
            + "        x += 5\n"
            + "        return x\n"
            + "    return 0\n"
            + "\n"
            + "def pass_through(a: int?) -> int?:\n"
            + "    if a is not None:\n"
            + "        return a\n"
            + "    return None()\n");
        helper.AddSourceFile("main.spy",
            "from lib import bump, pass_through\n"
            + "\n"
            + "def main() -> None:\n"
            + "    print(bump(Some(10)))\n"
            + "    print(pass_through(Some(7)))\n");
        helper.WithEntryPoint("main.spy");

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Trim().Split('\n').Should().Equal(new[] { "15", "7" });
    }
}
