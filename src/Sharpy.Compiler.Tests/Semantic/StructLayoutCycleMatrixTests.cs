using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// A struct whose field graph holds the struct itself BY VALUE is refused at semantic time —
/// SPY0616 at the field that starts the cycle, naming the chain — instead of reaching Roslyn's
/// CS0523 behind SPY0908 (#2354). Every refused cell below was SPY0908 CS0523 at the prior commit
/// (measured with the built CLI); every control holds the struct through a reference, or is a
/// finite generic instantiation, and must keep running.
///
/// <para>The matrix is the route a value is held through {own type, another struct, two structs
/// in a ring, a tuple, <c>T?</c>, <c>Optional[T]</c>, <c>T | None</c>, <c>Result[T, E]</c>, a
/// generic struct's type argument, a generic struct holding itself} × the reference-typed
/// controls {list, dict, array, class field, class host} plus the finite generic
/// <c>Box[Box[int]]</c> and the static members C# allows.</para>
/// </summary>
public class StructLayoutCycleMatrixTests : IntegrationTestBase
{
    public StructLayoutCycleMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Main = "\ndef main() -> None:\n    print(\"built\")\n";

    // cell, source, the (line, column, struct, field) of every expected SPY0616.
    public static IEnumerable<object[]> RefusedCells() => new[]
    {
        Cell("own-type", "struct Node:\n    value: int\n    nxt: Node\n", (3, 5, "Node", "nxt")),
        Cell("tuple", "struct Node:\n    pair: tuple[Node, int]\n", (2, 5, "Node", "pair")),
        Cell("question-optional", "struct Node:\n    value: int\n    nxt: Node?\n", (3, 5, "Node", "nxt")),
        Cell("named-optional", "struct Node:\n    value: int\n    nxt: Optional[Node]\n", (3, 5, "Node", "nxt")),
        Cell("or-none", "struct Node:\n    value: int\n    nxt: Node | None\n", (3, 5, "Node", "nxt")),
        Cell("result", "struct Node:\n    r: Result[Node, str]\n", (2, 5, "Node", "r")),
        Cell("generic-argument", "struct Box[T]:\n    v: T\n\nstruct Node:\n    b: Box[Node]\n", (5, 5, "Node", "b")),
        Cell("generic-self", "struct Box[T]:\n    v: T\n    again: Box[T]\n", (3, 5, "Box", "again")),
        Cell("ring", "struct A:\n    b: B\n\nstruct B:\n    a: A\n", (2, 5, "A", "b"), (5, 5, "B", "a")),
        Cell("through-struct", "struct Inner:\n    outer: Outer\n\nstruct Outer:\n    inner: Inner\n    x: int\n",
            (2, 5, "Inner", "outer"), (5, 5, "Outer", "inner")),
    };

    private static object[] Cell(string name, string types, params (int Line, int Column, string Struct, string Field)[] expected)
        => new object[] { name, types + Main, expected };

    [Theory]
    [MemberData(nameof(RefusedCells))]
    public void AStructHoldingItselfByValue_IsRefusedAtTheField(
        string cell, string source, (int Line, int Column, string Struct, string Field)[] expected)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{cell}]");
        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();
        var report = string.Join(" | ", errors.Select(d => $"{d.Code} @{d.Line}:{d.Column} {d.Message}"));
        errors.Select(d => (d.Code, d.Line, d.Column)).Should().BeEquivalentTo(
            expected.Select(e => ((string?)DiagnosticCodes.SemanticOverflow.StructLayoutCycle, (int?)e.Line, (int?)e.Column)),
            $"[{cell}] {report}");
        foreach (var e in expected)
        {
            errors.Should().Contain(d => d.Message.StartsWith(
                $"Struct '{e.Struct}' contains itself by value through field '{e.Field}' (", StringComparison.Ordinal),
                $"[{cell}] {report}");
        }
    }

    [Fact]
    public void TheRefusalNamesTheChainAndTheSteer()
    {
        var result = CompileAndExecute("struct Box[T]:\n    v: T\n\nstruct Node:\n    b: Box[Node]\n" + Main);

        result.RawDiagnostics.Single(d => d.Severity == CompilerDiagnosticSeverity.Error).Message.Should().Be(
            "Struct 'Node' contains itself by value through field 'b' (Node.b -> Box.v -> Node); " +
            "store a reference type such as 'list[Node]' or make 'Node' a class");
    }

    public static IEnumerable<object[]> Controls() => new[]
    {
        new object[] { "list", "struct Node:\n    value: int\n    kids: list[Node]\n\ndef main() -> None:\n    k: list[Node] = []\n    print(Node(1, k).value)\n", "1" },
        new object[] { "dict-array", "struct Node:\n    d: dict[str, Node]\n    a: array[Node]\n" + Main, "built" },
        new object[] { "class-field", "class Holder:\n    n: Node\n\nstruct Node:\n    h: Holder\n" + Main, "built" },
        new object[] { "class-host", "class Node:\n    value: int\n    nxt: Node?\n\n    def __init__(self, value: int):\n        self.value = value\n        self.nxt = None()\n\ndef main() -> None:\n    print(Node(1).value)\n", "1" },
        new object[] { "finite-generic", "struct Box[T]:\n    v: T\n\ndef main() -> None:\n    b: Box[Box[int]] = Box[Box[int]](Box[int](1))\n    print(b.v.v)\n", "1" },
        new object[] { "static-field", "struct P:\n    x: int\n    @static\n    zero: P = P(0)\n\ndef main() -> None:\n    print(P.zero.x)\n", "0" },
        new object[] { "static-method", "struct P:\n    x: int\n\n    @static\n    def origin() -> P:\n        return P(0)\n\ndef main() -> None:\n    print(P.origin().x)\n", "0" },
    };

    [Theory]
    [MemberData(nameof(Controls))]
    public void AStructHeldThroughAReference_KeepsRunning(string cell, string source, string expected)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"[{cell}] {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        result.StandardOutput.Trim().Should().Be(expected, $"[{cell}]");
    }
}
