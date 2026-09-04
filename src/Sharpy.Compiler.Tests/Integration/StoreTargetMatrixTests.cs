using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
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
}
