using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Pattern-head member-access chain matrix (P5, #1799, #1735). A pattern head that names a nested-type
/// chain (<c>case Outer.Holder.A:</c>) resolves the LONGEST nested-type prefix and leaves exactly one
/// member segment (const / enum member / union case), at every depth — the same resolution the
/// annotation path uses, so a depth-2 chain no longer reports SPY0203/SPY0202. Total over
/// <c>member-kind {const, enum member} × depth {1, 2, 3}</c>, plus the class-pattern head, or-pattern
/// alternatives, and the expression-position control that pins the pattern route and the
/// <c>GenericReferenceResolver</c> route (isinstance) agree per chain.
/// <para>
/// Union-case heads nested in a class body at depth ≥ 2 (<c>case Outer.Shape.Circle():</c>) resolve
/// semantically since #1729 P1.2 (the SPY0202 drained), but are rostered N/A here because the codegen
/// emitter arm is P1.3 — they refuse with SPY0510 until it lands. The refusal is asserted so the
/// roster is falsifiable.
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class PatternHeadChainMatrixTests : IntegrationTestBase
{
    public PatternHeadChainMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string SPY0202 = DiagnosticCodes.Semantic.UndefinedType;
    private const string SPY0510 = DiagnosticCodes.CodeGen.UnrecognizedStatementType;

    // ── Const / enum-member chains at depth 1, 2, 3 (e01/e02/e03/e05, enum depths) ────────────
    // Each cell uses `const A: int = 4` (a compile-time const, not a plain field) or a nested enum,
    // and matches a value that HITS the chain — the resolver must bind the const/member at the
    // deepest nested type. Sources are pinned literally (indentation is load-bearing for nesting).

    public static IEnumerable<object[]> ChainCells()
    {
        // const, depth 1 (e02)
        yield return new object[]
        {
            "const/1",
            @"
class Holder:
    const A: int = 4

def main() -> None:
    v: int = 4
    match v:
        case Holder.A:
            print(""hit"")
        case _:
            print(""miss"")
",
            "hit",
        };
        // const, depth 2 (e01)
        yield return new object[]
        {
            "const/2",
            @"
class Outer:
    class Holder:
        const A: int = 4

def main() -> None:
    v: int = 4
    match v:
        case Outer.Holder.A:
            print(""hit"")
        case _:
            print(""miss"")
",
            "hit",
        };
        // const, depth 3 (e05)
        yield return new object[]
        {
            "const/3",
            @"
class Outer:
    class Mid:
        class Holder:
            const A: int = 4

def main() -> None:
    v: int = 4
    match v:
        case Outer.Mid.Holder.A:
            print(""hit"")
        case _:
            print(""miss"")
",
            "hit",
        };
        // enum member, depth 1
        yield return new object[]
        {
            "enum/1",
            @"
enum Color:
    RED = 1
    GREEN = 2

def main() -> None:
    c: Color = Color.RED
    match c:
        case Color.RED:
            print(""red"")
        case _:
            print(""other"")
",
            "red",
        };
        // enum member, depth 2 (e03)
        yield return new object[]
        {
            "enum/2",
            @"
class Holder:
    enum Color:
        RED = 1
        GREEN = 2

def main() -> None:
    c: Holder.Color = Holder.Color.RED
    match c:
        case Holder.Color.RED:
            print(""red"")
        case _:
            print(""other"")
",
            "red",
        };
        // enum member, depth 3
        yield return new object[]
        {
            "enum/3",
            @"
class Outer:
    class Holder:
        enum Color:
            RED = 1
            GREEN = 2

def main() -> None:
    c: Outer.Holder.Color = Outer.Holder.Color.RED
    match c:
        case Outer.Holder.Color.RED:
            print(""red"")
        case _:
            print(""other"")
",
            "red",
        };
    }

    [Theory]
    [MemberData(nameof(ChainCells))]
    public void HeadChain_ResolvesAtEveryDepth(string label, string source, string expected)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"[{label}] must resolve the chain and run (not SPY0203/SPY0202). "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.TrimEnd().Should().Be(expected, $"[{label}]\n{source}");
    }

    [Fact]
    public void ChainMatrix_IsTotalOverItsAxes()
    {
        // 2 member kinds × 3 depths = 6 executing chain cells. Union × depth ≥ 2 is rostered (#1729).
        ChainCells().Count().Should().Be(6,
            "const/enum × depth {1,2,3} — anchored to the literal axis, not to any enum's own count");
    }

    // ── Discriminating: the resolver binds the RIGHT const, not a wildcard ─────────────────────

    [Fact]
    public void HeadChain_NonMatchingValue_FallsThrough()
    {
        // A=4, v=5 → miss. A resolver that bound the wrong const (or a wildcard) would print "hit".
        var result = CompileAndExecute(@"
class Outer:
    class Holder:
        const A: int = 4

def main() -> None:
    v: int = 5
    match v:
        case Outer.Holder.A:
            print(""hit"")
        case _:
            print(""miss"")
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("miss",
            "the chain binds the const value 4; the scrutinee 5 does not match");
    }

    // ── Class-pattern head at depth 2 (e04) ───────────────────────────────────────────────────

    [Fact]
    public void ClassPatternHead_NestedType_Runs()
    {
        var result = CompileAndExecute(@"
class Outer:
    class Holder:
        x: int

        def __init__(self, x: int):
            self.x = x

def check(o: object) -> None:
    match o:
        case Outer.Holder():
            print(""hit"")
        case _:
            print(""miss"")

def main() -> None:
    check(Outer.Holder(3))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("hit",
            "a nested-type class-pattern head resolves (was SPY0202)");
    }

    // ── Or-pattern alternatives, both chains (e09) ────────────────────────────────────────────

    [Fact]
    public void OrPatternChainAlternatives_Resolve()
    {
        var result = CompileAndExecute(@"
class Outer:
    class Holder:
        const A: int = 4
        const B: int = 5

def main() -> None:
    v: int = 5
    match v:
        case Outer.Holder.A | Outer.Holder.B:
            print(""hit"")
        case _:
            print(""miss"")
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("hit",
            "both or-pattern alternatives resolve their nested chain (was SPY0203)");
    }

    // ── Expression-position control: the two routes agree (e08) ───────────────────────────────
    // The expression column runs through GenericReferenceResolver.LookupNestedTypeSymbol, a route
    // NOT unified with the pattern-head resolver. Asserting it here pins that the two agree per
    // chain — a future divergence would fail this cell.

    [Fact]
    public void ExpressionPosition_IsinstanceNestedType_AgreesWithPattern()
    {
        var result = CompileAndExecute(@"
class Outer:
    class Holder:
        x: int

        def __init__(self, x: int):
            self.x = x

def main() -> None:
    h: Outer.Holder = Outer.Holder(3)
    o: object = h
    print(isinstance(o, Outer.Holder))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("True",
            "isinstance resolves the same nested chain the pattern head does");
    }

    // ── Control: a qualified TOP-LEVEL union case resolves (e06) ──────────────────────────────

    [Fact]
    public void QualifiedTopLevelUnionCaseHead_Runs()
    {
        var result = CompileAndExecute(@"
union Shape:
    case Circle(radius: int)
    case Square(side: int)

def check(s: Shape) -> None:
    match s:
        case Shape.Circle(r):
            print(""circle"", r)
        case _:
            print(""other"")

def main() -> None:
    check(Shape.Circle(3))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("circle 3");
    }

    // ── Executes: union-case head nested in a class body at depth ≥ 2 (#1729) ──────────────────
    // #1729 Phase 1 Task 2 (P1.2) resolved the nested union at NAME RESOLUTION — the SPY0202 that
    // once rostered this cell drained there. Task 3 (P1.3) then added the CODEGEN emitter arm, so the
    // SPY0510 that briefly stood in for it drains too: the depth-2 nested-union head now BOTH resolves
    // and emits, and the whole program executes end to end (declaration, qualified construction, and
    // the nested-chain pattern head). The absence assertions keep the drain falsifiable — reintroduce
    // either refusal and this cell goes red.

    [Fact]
    public void NestedUnionCaseHead_Depth2_Runs_1729()
    {
        var result = CompileAndExecute(@"
class Outer:
    union Shape:
        case Circle(r: int)
        case Square(s: int)

def check(s: Outer.Shape) -> None:
    match s:
        case Outer.Shape.Circle(r):
            print(""circle"", r)
        case _:
            print(""other"")

def main() -> None:
    check(Outer.Shape.Circle(3))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0202,
            "the semantic refusal drained: name resolution resolves the nested union (#1729, P1.2)");
        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0510,
            "the codegen refusal drained: the emitter now emits the nested union as a member (#1729, P1.3)");
        result.StandardOutput.TrimEnd().Should().Be("circle 3",
            "the depth-2 nested union declares, constructs and matches through the full chain");
    }
}
