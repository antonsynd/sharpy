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
}
