using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance matrix for truthiness (#1558): position x type. The per-alternative guard pattern
/// (#2237) is a position: in the refusal roster and the executed guard-position matrix.
/// Each cell asserts either accepted (truth-testable) or refused (SPY0220/SPY0241).
/// Adding a new truth position or type without updating this matrix is a loud failure.
/// </summary>
[Collection("HeavyCompilation")]
public class TruthinessConformanceTests : StdlibAwareIntegrationTestBase
{
    public TruthinessConformanceTests(ITestOutputHelper output) : base(output) { }

    // `import collections` + the Stdlib reference (StdlibAwareIntegrationTestBase): Stdlib
    // collections are subjects too — their truthiness is discovered from the same CLR surface
    // (ISized) as Core's, so a Stdlib row guards the class, not just the builtins (#1972).
    private const string Preamble = @"
import collections

class HasBool:
    def __bool__(self) -> bool:
        return True

class HasLen:
    _items: list[int]
    def __init__(self) -> None:
        self._items = [1, 2]
    def __len__(self) -> int:
        return len(self._items)

class PlainObject:
    pass

class Box[T]:
    _v: T
    def __init__(self, v: T) -> None:
        self._v = v
";

    // Types that ARE truth-testable (have a falsy case)
    public static IEnumerable<object[]> TruthTestableTypes()
    {
        yield return new object[] { "bool", "x: bool = True" };
        yield return new object[] { "int", "x: int = 42" };
        yield return new object[] { "float", "x: float = 3.14" };
        yield return new object[] { "long", "x: long = 42L" };
        yield return new object[] { "str", "x: str = \"hello\"" };
        yield return new object[] { "bytes", "x: bytes = b\"data\"" };
        yield return new object[] { "list", "x: list[int] = [1, 2]" };
        yield return new object[] { "dict", "x: dict[str, int] = {\"a\": 1}" };
        yield return new object[] { "set", "x: set[int] = {1, 2}" };
        yield return new object[] { "Optional", "x: int? = Some(42)" };
        yield return new object[] { "None", "x: int? = None()" };
        yield return new object[] { "UDT __bool__", "x: HasBool = HasBool()" };
        yield return new object[] { "UDT __len__", "x: HasLen = HasLen()" };
        // Stdlib: Deque<T> spells __len__ as ISized (#1972) — before that it was only
        // IReadOnlyCollection<T>, so len() worked and every truth position was SPY0220.
        yield return new object[] { "collections.deque", "x: collections.deque[int] = collections.deque[int]([1])" };
    }

    // Types that are NOT truth-testable (no falsy case). The two GENERIC subjects are the positive
    // controls for the constructed-generic arm of ClassifyTruthiness, which answers from the CLR
    // identity (ISized / IBoolConvertible) rather than a name list (#1933 sibling): a user generic
    // host with no dunder, and a CLR-backed generic (Sharpy.Iterator<T>) that carries neither
    // interface, must both still be refused — otherwise "discovered" would mean "always yes".
    public static IEnumerable<object[]> NonTruthTestableTypes()
    {
        yield return new object[] { "function", "def f() -> int:\n        return 1\n    x = f" };
        yield return new object[] { "plain object", "x: PlainObject = PlainObject()" };
        yield return new object[] { "generic plain object", "x: Box[int] = Box[int](1)" };
        yield return new object[] { "CLR generic, not sized", "x: Iterator[int] = iter([1, 2])" };
    }

    // --- if position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void If_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    if x:
        print(""ok"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'if' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void If_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    if x:
        print(""fail"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'if' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'if' refusal of {typeName} should produce SPY0220");
    }

    // --- while position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void While_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    while x:
        break
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'while' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void While_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    while x:
        break
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'while' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'while' refusal of {typeName} should produce SPY0220");
    }

    // --- assert position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void Assert_AcceptsTruthTestableType(string typeName, string decl)
    {
        // assert of an always-falsy value (None) compiles but throws at runtime — check
        // compilation only (no SPY0220), don't assert on execution success.
        var source = Preamble + $@"
def main() -> None:
    {decl}
    assert x
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0220",
            $"'assert' should accept truth-testable type {typeName} (no SPY0220)");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void Assert_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    assert x
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'assert' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'assert' refusal of {typeName} should produce SPY0220");
    }

    // --- ternary position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void Ternary_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: int = 1 if x else 2
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"ternary should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void Ternary_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: int = 1 if x else 2
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"ternary should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"ternary refusal of {typeName} should produce SPY0220");
    }

    // --- and position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void And_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = x and True
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'and' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void And_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = x and True
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'and' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'and' refusal of {typeName} should produce SPY0220");
    }

    // --- or position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void Or_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = x or False
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'or' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void Or_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = x or False
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'or' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'or' refusal of {typeName} should produce SPY0220");
    }

    // --- not position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void Not_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = not x
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"'not' should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void Not_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    y: bool = not x
    print(y)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"'not' should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"'not' refusal of {typeName} should produce SPY0220");
    }

    // --- match guard position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void MatchGuard_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    val: int = 1
    match val:
        case v if x:
            print(""guarded"")
        case _:
            print(""fallback"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"match guard should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void MatchGuard_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    val: int = 1
    match val:
        case v if x:
            print(""fail"")
        case _:
            print(""fallback"")
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"match guard should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220" || d.Code == "SPY0241",
            $"match guard refusal of {typeName} should produce SPY0220 or SPY0241");
    }

    // --- comprehension filter position ---

    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void ComprehensionFilter_AcceptsTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def check(val: bool) -> list[int]:
    return [1 for _ in range(1) if val]

def main() -> None:
    {decl}
    result: list[int] = [1 for _ in range(1) if x]
    print(result)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"comprehension filter should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void ComprehensionFilter_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    result: list[int] = [1 for _ in range(1) if x]
    print(result)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"comprehension filter should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0241",
            $"comprehension filter refusal of {typeName} should produce SPY0241");
    }

    // --- generator-expression filter position (#2225) ---

    /// <summary>
    /// The generator-expression row of the position axis. Same checker site as the comprehension
    /// filter (<c>CheckComprehensionIfClause</c>), different lowering (a LINQ <c>Where</c>, not an
    /// imperative <c>if</c>) — before #2225 that lowering skipped the recorded wrap, so every
    /// non-bool subject was SPY0908 CS0029. Asserts the generator AGREES with its list-comprehension
    /// twin in the same program, not just that it compiles (<c>x: int? = None()</c> is the falsy cell).
    /// </summary>
    [Theory]
    [MemberData(nameof(TruthTestableTypes))]
    public void GeneratorFilter_AcceptsTruthTestableType_AndAgreesWithListComprehension(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    print([1 for _ in range(1) if x])
    print(list(1 for _ in range(1) if x))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"generator filter should accept truth-testable type {typeName}: {string.Join(", ", result.CompilationErrors)}");
        var lines = result.StandardOutput.Trim().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        lines.Should().HaveCount(2, $"two prints for {typeName}");
        lines[1].Should().Be(lines[0], $"the generator filter must agree with its list-comprehension twin for {typeName}");
    }

    [Theory]
    [MemberData(nameof(NonTruthTestableTypes))]
    public void GeneratorFilter_RefusesNonTruthTestableType(string typeName, string decl)
    {
        var source = Preamble + $@"
def main() -> None:
    {decl}
    print(list(1 for _ in range(1) if x))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"generator filter should refuse non-truth-testable type {typeName}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0241",
            $"generator filter refusal of {typeName} should produce SPY0241");
    }

    // ═════════ #2225: comprehension-filter positions × operand type, EXECUTED against python3 ═════════

    private const string FilterMatrixPreamble = @"
class FlagBool:
    _v: bool
    def __init__(self, v: bool) -> None:
        self._v = v
    def __bool__(self) -> bool:
        return self._v

class CountLen:
    _n: int
    def __init__(self, n: int) -> None:
        self._n = n
    def __len__(self) -> int:
        return self._n

";

    /// <summary>
    /// The operand axis: three values, FALSY first, so every position's filter keeps indices 1 and 2
    /// — the expected output is a function of the position alone. A dropped filter keeps index 0, an
    /// inverted one keeps only index 0, and a missing wrap is SPY0908; all three are red. <c>bool</c>
    /// is the control (no wrap needed: it ran before #2225).
    /// </summary>
    public static IEnumerable<object[]> FilterOperandTypes()
    {
        yield return new object[] { "bool", "xs: list[bool] = [False, True, True]" };
        yield return new object[] { "int", "xs: list[int] = [0, 1, 2]" };
        yield return new object[] { "float", "xs: list[float] = [0.0, 1.5, 2.5]" };
        yield return new object[] { "str", "xs: list[str] = [\"\", \"a\", \"b\"]" };
        yield return new object[] { "list", "xs: list[list[int]] = [[], [1], [2]]" };
        yield return new object[] { "Optional", "xs: list[int?] = [None(), Some(1), Some(2)]" };
        yield return new object[] { "nullable", "xs: list[str | None] = [None, \"a\", \"b\"]" };
        yield return new object[] { "UDT __bool__", "xs: list[FlagBool] = [FlagBool(False), FlagBool(True), FlagBool(True)]" };
        yield return new object[] { "UDT __len__", "xs: list[CountLen] = [CountLen(0), CountLen(1), CountLen(2)]" };
    }

    private const string E = "for i, x in enumerate(xs) if x";

    /// <summary>
    /// The position axis. Each body is spelled identically in Python; each expected line is
    /// python3 3.12's output for the same text over <c>[falsy, truthy, truthy]</c> (measured
    /// 2026-10-04). The three imperative forms are the twins the generator rows must agree with.
    /// </summary>
    private static readonly (string Id, string Body, string Expected)[] FilterPositions =
    {
        ("listcomp", $"print([i {E}])", "[1, 2]"),
        ("setcomp", $"print({{i {E}}})", "{1, 2}"),
        ("dictcomp", $"print({{i: i {E}}})", "{1: 1, 2: 2}"),
        ("listcomp-two-ifs", $"print([i {E} if x])", "[1, 2]"),
        ("genexp-list", $"print(list(i {E}))", "[1, 2]"),
        ("genexp-set", $"print(set(i {E}))", "{1, 2}"),
        ("genexp-sum", $"print(sum(i {E}))", "3"),
        ("genexp-any", $"print(any(i == 0 {E}))", "False"),
        ("genexp-all", $"print(all(i > 0 {E}))", "True"),
        ("genexp-sorted", $"print(sorted(i {E}))", "[1, 2]"),
        ("genexp-bound", $"g = (i {E})\n    for v in g:\n        print(v)", "1\n2"),
        ("genexp-name-target", "print(len(list(1 for x in xs if x)))", "2"),
        ("genexp-two-ifs", $"print(list(i {E} if x))", "[1, 2]"),
        ("genexp-then-int-if", $"print(list(i {E} if i))", "[1, 2]"),
        ("genexp-if-after-inner-for", "print(list(i for i, x in enumerate(xs) for _ in range(1) if x))", "[1, 2]"),
        ("genexp-in-genexp-element", $"print(list(list(j for j in range(1) if x) {E}))", "[[0], [0]]"),
        ("genexp-in-genexp-iterator", "print(list(i for i in (j for j, x in enumerate(xs) if x)))", "[1, 2]"),
        ("genexp-in-listcomp-filter", "print([i for i, x in enumerate(xs) if sum(1 for _ in range(1) if x)])", "[1, 2]"),
        ("listcomp-in-genexp-filter", "print(list(i for i, x in enumerate(xs) if [1 for _ in range(1) if x]))", "[1, 2]"),
    };

    [Theory]
    [MemberData(nameof(FilterOperandTypes))]
    public void FilterPositions_ExecuteAsPython(string typeName, string decl)
    {
        var source = FilterMatrixPreamble + "def main() -> None:\n    " + decl + "\n"
            + string.Concat(FilterPositions.Select(p => $"    print(\"{p.Id}\")\n    {p.Body}\n"));
        var expected = string.Join("\n", FilterPositions.Select(p => p.Id + "\n" + p.Expected));

        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"every comprehension-filter position must accept {typeName}: "
            + string.Join(", ", result.CompilationErrors) + "\n" + source);
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be(expected,
            $"every comprehension-filter position must agree with python3 for {typeName}\n{source}");
    }

    // ═════════ #2237 (R-ER): guard positions × operand type × match form, EXECUTED ═════════

    private const string GuardMatrixPreamble = @"
class Pt:
    a: int
    b: int
    def __init__(self, a: int, b: int):
        self.a = a
        self.b = b

";

    /// <summary>
    /// The guard-position axis: every pattern form the parser admits a guard pattern <c>(p if g)</c>
    /// in (a parenthesized single pattern: an or-alternative in any slot, top level, under <c>as</c>,
    /// a tuple / list element, a class positional or keyword sub-pattern, another guard pattern),
    /// with the arm guard <c>case p if g</c> as the twin. Every subject matches the GUARDED pattern,
    /// so Python's answer is the arm guard's: the arm is taken exactly when the operand is truthy.
    /// </summary>
    // No cell has the subject match an UNGUARDED alternative (`(2 if x) | 0` with subject 0): a
    // per-alternative guard is applied to the whole or-pattern today (`case 2 or 0 when x`), a
    // separate guard-scope defect recorded as #2237's follow-up. These cells pin the truthiness rule
    // only, so each discriminates on the guarded alternative.
    private static readonly (string Id, string Subject, string Pattern)[] GuardPositions =
    {
        ("arm", "n", "2 if x"),
        ("alt-first", "n", "(2 if x) | 9"),
        ("alt-second", "n", "9 | (2 if x)"),
        ("alt-middle", "n", "8 | (2 if x) | 9"),
        ("alt-both", "n", "(2 if x) | (9 if x)"),
        ("bare", "n", "(2 if x)"),
        ("as", "n", "(2 if x) as v"),
        ("tuple-element", "t", "((2 if x), 1)"),
        ("tuple-element-alt", "t", "((2 if x) | 9, 1)"),
        ("list-element", "l", "[(2 if x), 1]"),
        ("class-positional", "p", "Pt((2 if x), 1)"),
        ("class-keyword", "p", "Pt(a=(2 if x))"),
        ("guard-in-guard", "n", "((2 if x) if x)"),
        ("element-and-arm", "t", "((2 if x), 1) if x"),
    };

    /// <summary>
    /// Operand axis = <see cref="FilterOperandTypes"/> (falsy first): each cell runs the guard with
    /// the falsy then the truthy operand and must print <c>other</c> then <c>A</c> — a dropped guard
    /// prints <c>A A</c>, an inverted one <c>A other</c>, a missing wrap is SPY0908, and the pre-#2237
    /// exactly-bool rule refused every non-bool row with SPY0220.
    /// </summary>
    [Theory]
    [MemberData(nameof(FilterOperandTypes))]
    public void GuardPositions_ExecuteAsPython(string typeName, string decl)
    {
        var body = new System.Text.StringBuilder();
        var expected = new List<string>();
        for (var i = 0; i < GuardPositions.Length; i++)
        {
            var (id, subject, pattern) = GuardPositions[i];
            pattern = pattern.Replace(" as v", $" as v{i}", StringComparison.Ordinal);
            body.Append($"    print(\"{id}-stmt\")\n    for x in xs[:2]:\n        match {subject}:\n"
                + $"            case {pattern}:\n                print(\"A\")\n"
                + "            case _:\n                print(\"other\")\n");
            body.Append($"    print(\"{id}-expr\")\n    for x in xs[:2]:\n        r{i}: str = match {subject}:\n"
                + $"            case {pattern}: \"A\"\n            case _: \"other\"\n        print(r{i})\n");
            expected.Add($"{id}-stmt\nother\nA\n{id}-expr\nother\nA");
        }

        var source = FilterMatrixPreamble + GuardMatrixPreamble + "def main() -> None:\n    " + decl + "\n"
            + "    n: int = 2\n    t: tuple[int, int] = (2, 1)\n    l: list[int] = [2, 1]\n    p: Pt = Pt(2, 1)\n"
            + body;

        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"every guard position must accept {typeName}: " + string.Join(", ", result.CompilationErrors) + "\n" + source);
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be(string.Join("\n", expected),
            $"every guard position must take the arm exactly when the {typeName} operand is truthy\n{source}");
    }

    // ════════════════════ #1861, R-AU: RefusalNamesTheFact ════════════════════

    /// <summary>
    /// One AXIS VALUE per SITE (16), not per human-readable "position" (13) — <c>and</c> and
    /// <c>or</c> each cover TWO sites (their left and right operand), and the guard-mutation
    /// acceptance criterion ("bypass the helper at ONE site... proves per-site totality") needs each
    /// of the 14 original <c>AddError</c> call sites independently exercised, or a mutation at, say, the
    /// <c>or</c> RIGHT operand specifically would slip past a matrix that only tests the left. The
    /// three guard positions (arm guard under each match form, per-alternative guard pattern under
    /// each form) share one call, <c>CheckGuardCondition</c>, since #2237.
    /// </summary>
    private sealed record Position(string Id, string Code, Func<string, string> Body);

    private static readonly Position[] Positions =
    {
        new("if", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    if {x}:\n        print(\"t\")\n"),
        new("elif", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    if False:\n        pass\n    elif {x}:\n        print(\"t\")\n"),
        new("while", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    while {x}:\n        break\n"),
        new("assert", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    assert {x}\n"),
        new("not", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    y: bool = not {x}\n    print(y)\n"),
        new("and-left", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    y: bool = {x} and True\n    print(y)\n"),
        new("and-right", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    y: bool = True and {x}\n    print(y)\n"),
        new("or-left", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    y: bool = {x} or False\n    print(y)\n"),
        new("or-right", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    y: bool = False or {x}\n    print(y)\n"),
        new("ternary-condition", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    y: int = 1 if {x} else 2\n    print(y)\n"),
        // R-K distributed: BOTH branches are classified independently at compile time (regardless
        // of the ternary's own condition), so the SAME subject on both branches reports the fact
        // TWICE — Contain (not ContainSingle) is the correct assertion here.
        new("ternary-branch", DiagnosticCodes.Semantic.TypeMismatch,
            x => $"    if {x} if True else {x}:\n        print(\"t\")\n"),
        new("comprehension-filter", DiagnosticCodes.Semantic.ConditionNotBoolean,
            x => $"    result: list[int] = [1 for _ in range(1) if {x}]\n    print(result)\n"),
        new("match-stmt-guard", DiagnosticCodes.Semantic.ConditionNotBoolean,
            x => "    val: int = 1\n    match val:\n"
                + $"        case v if {x}:\n            print(\"t\")\n        case _:\n            print(\"f\")\n"),
        new("match-expr-guard", DiagnosticCodes.Semantic.ConditionNotBoolean,
            x => "    val: int = 1\n    y: int = match val:\n"
                + $"        case v if {x}: 1\n        case _: 2\n    print(y)\n"),
        // #2237 (R-ER): the per-alternative guard pattern `(p if g)` is the same guard rule —
        // same code, same message — not the exactly-`bool` SPY0220 it was before.
        new("match-stmt-alt-guard", DiagnosticCodes.Semantic.ConditionNotBoolean,
            x => "    val: int = 1\n    match val:\n"
                + $"        case (1 if {x}) | 2:\n            print(\"t\")\n        case _:\n            print(\"f\")\n"),
        new("match-expr-alt-guard", DiagnosticCodes.Semantic.ConditionNotBoolean,
            x => "    val: int = 1\n    y: int = match val:\n"
                + $"        case (1 if {x}) | 2: 1\n        case _: 2\n    print(y)\n"),
    };

    /// <param name="Id">Cell id fragment.</param>
    /// <param name="Preamble">Module-level declarations the subject needs (a type alias, a class, a function).</param>
    /// <param name="Setup">The `x: T = value` (or inferred `x = value`) declaration, indented for main()'s body.</param>
    /// <param name="IsTuple">Whether the fact sentence is the tuple-specific one or the generic "has no falsy case".</param>
    /// <param name="NonEmpty">For a tuple subject: whether it is non-empty ("is always truthy") or the zero-arity `()` ("is always falsy (empty)").</param>
    private sealed record Subject(string Id, string Preamble, string Setup, bool IsTuple, bool NonEmpty);

    private static readonly Subject[] Subjects =
    {
        new("tuple-fixed", "", "    x: tuple[int, int] = (1, 2)\n", true, true),
        new("tuple-named", "type Point = tuple[a: int, b: int]\n\n", "    x: Point = (a=1, b=2)\n", true, true),
        // No annotation: `x: tuple[] = ()` parses the `tuple[]` spelling as an array shorthand, not
        // the empty-tuple type — the zero-arity tuple has no nameable annotation, only the inferred
        // type of the `()` literal itself (measured; #1861 probing).
        new("tuple-empty", "", "    x = ()\n", true, false),
        new("plain-class", "class Plain:\n    pass\n\n", "    x: Plain = Plain()\n", false, false),
        new("function-ref", "def f() -> int:\n    return 1\n\n", "    x = f\n", false, false),
        // Positive controls for the constructed-generic arm (#1933 sibling): the arm discovers
        // ISized/IBoolConvertible from the CLR identity, so a generic that carries neither — a user
        // generic host with no dunder, a CLR-backed Iterator<T> — must still name the fact.
        new("generic-plain-class",
            "class Box[T]:\n    _v: T\n    def __init__(self, v: T) -> None:\n        self._v = v\n\n",
            "    x: Box[int] = Box[int](1)\n", false, false),
        new("clr-generic-not-sized", "", "    x: Iterator[int] = iter([1, 2])\n", false, false),
    };

    private static string ExpectedFact(Subject subject)
        => subject.IsTuple
            ? (subject.NonEmpty ? "is always truthy" : "is always falsy (empty)")
            : "has no falsy case";

    public static IEnumerable<object[]> RefusalCells =>
        from p in Positions from s in Subjects select new object[] { p.Id, s.Id };

    [Theory]
    [MemberData(nameof(RefusalCells))]
    public void RefusalNamesTheFact(string positionId, string subjectId)
    {
        var position = Positions.Single(p => p.Id == positionId);
        var subject = Subjects.Single(s => s.Id == subjectId);

        var source = subject.Preamble + "def main() -> None:\n" + subject.Setup + position.Body("x");

        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"{positionId}\u00d7{subjectId} must be refused\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == position.Code && d.Message.Contains(ExpectedFact(subject), StringComparison.Ordinal),
            $"{positionId}\u00d7{subjectId} must be refused with {position.Code} naming the fact "
            + $"'{ExpectedFact(subject)}'; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}"))
            + "\n" + source);
    }

    [Fact]
    public void RefusalMatrixIsTotalOverItsAxes()
    {
        Positions.Select(p => p.Id).Should().BeEquivalentTo(
            new[]
            {
                "if", "elif", "while", "assert", "not", "and-left", "and-right", "or-left", "or-right",
                "ternary-condition", "ternary-branch", "comprehension-filter", "match-stmt-guard", "match-expr-guard",
                "match-stmt-alt-guard", "match-expr-alt-guard",
            },
            "the 16 truthiness-refusal SITES (13 human-readable positions, and/or each split into two, the "
            + "per-alternative guard under both match forms)");
        Subjects.Select(s => s.Id).Should().BeEquivalentTo(
            new[]
            {
                "tuple-fixed", "tuple-named", "tuple-empty", "plain-class", "function-ref",
                "generic-plain-class", "clr-generic-not-sized",
            },
            "the subject axis");

        (Positions.Length * Subjects.Length).Should().Be(112, "16 sites x 7 subjects");
        RefusalCells.Count().Should().Be(112, "every cell is live — no N/A in this matrix");
    }

    // ──────── controls: the wrapper families and non-tuple collections still run ────────

    [Theory]
    [InlineData("tuple-or-none", "", "x: tuple[int, int] | None = (1, 2)")]
    [InlineData("tuple-strict", "", "x: tuple[int, int]? = Some((1, 2))")]
    [InlineData("list", "", "x: list[int] = [1, 2]")]
    // The constructed-generic arm answers from the CLR identity (#1933 sibling): every Core
    // wrapper carrying ISized runs, including the ones the old name list never spelled (a dict view).
    [InlineData("frozenset", "", "x: frozenset[int] = frozenset([1, 2])")]
    [InlineData("dict-keys-view", "", "d: dict[str, int] = {\"a\": 1}\n    x = d.keys()")]
    [InlineData("len-class", "class Sized:\n    def __len__(self) -> int:\n        return 1\n\n", "x: Sized = Sized()")]
    public void RefusalControls_StillRun(string id, string preamble, string setup)
    {
        var source = preamble + "def main() -> None:\n" + $"    {setup}\n" + "    if x:\n        print(\"t\")\n";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"{id} must still run at a truthiness position (the wrapper/collection cases R-AU never "
            + "touches): " + string.Join("; ", result.CompilationErrors) + "\n" + source);
    }

    // ──────── source scan: no hand-written truthiness message survives outside the helper ────────

    private sealed record LiteralSite(string File, string Method, int Line, string Text, string? Callee);

    /// <summary>
    /// Every ARGUMENT EXPRESSION in TypeChecker*.cs whose SOURCE TEXT contains one of the three
    /// truthiness-refusal phrases, with the name of the INVOCATION it belongs to (or null when it
    /// sits in some other position). Scans <c>.ToString()</c> of the whole argument rather than
    /// walking <see cref="LiteralExpressionSyntax"/> alone — a PLAIN string literal
    /// (<c>"If condition must be boolean"</c>, what every site uses post-fix) and an INTERPOLATED one
    /// (<c>$"If condition must be boolean, got '{x}'"</c>, what every site used pre-fix) are different
    /// syntax kinds, and a scan that only recognizes one would miss a regression to the other — the
    /// literal-only version of this scan passed VACUOUSLY against a mutation that reverted one site
    /// back to its original interpolated form (mutation-tested, see commit body). A phrase surviving
    /// OUTSIDE a <c>ReportNotTruthTestable(...)</c> argument is a hand-written message the helper
    /// missed — the defect class Rule 2/Decision 6 exist to close.
    /// </summary>
    private static IReadOnlyList<LiteralSite> FindPhraseLiterals()
    {
        // "must be bool" (not "must be boolean"): the per-alternative guard's own exactly-bool
        // message, "Guard expression must be bool, got ...", slipped past the longer phrase (#2237).
        string[] phrases = { "must be bool", "must be truth-testable", "must be a boolean expression" };
        var results = new List<LiteralSite>();

        foreach (var file in Directory.GetFiles(FindCompilerSemanticDirectory(), "TypeChecker*.cs"))
        {
            var text = File.ReadAllText(file);
            var root = CSharpSyntaxTree.ParseText(text).GetRoot();

            foreach (var arg in root.DescendantNodes()
                .OfType<ArgumentSyntax>()
                .Where(a => phrases.Any(p => a.Expression.ToString().Contains(p, StringComparison.Ordinal))))
            {
                var callee = arg.Parent?.Parent switch
                {
                    InvocationExpressionSyntax { Expression: IdentifierNameSyntax id } => id.Identifier.ValueText,
                    _ => null,
                };

                results.Add(new LiteralSite(
                    Path.GetFileName(file), EnclosingMemberName(arg),
                    arg.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    arg.Expression.ToString(), callee));
            }
        }

        return results;
    }

    private static string EnclosingMemberName(SyntaxNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax method)
                return method.Identifier.ValueText;
        }

        return "<unknown>";
    }

    private static string FindCompilerSemanticDirectory()
        => Path.Combine(FindRepoRoot(), "src", "Sharpy.Compiler", "Semantic");

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, "src", "Sharpy.Compiler", "Semantic")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("repository root not found from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void NoHandWrittenTruthinessMessageSurvivesOutsideTheHelper()
    {
        var sites = FindPhraseLiterals();

        // The exception-filter's OWN, DIFFERENT check (TypeChecker.Statements.cs) requires the
        // filter's type be EXACTLY bool (`filterType != BuiltinType.Bool`) and never routes through
        // CheckTruthinessTest/ClassifyTruthiness at all — it shares the substring "must be a
        // boolean expression" with the match-guard sites by coincidence, not by mechanism. Excluded
        // by NAME, not silently, and not counted toward either total below.
        var exceptionFilterSites = sites.Where(s => s.Callee == "AddError"
            && s.Text.Contains("Exception filter must be a boolean expression", StringComparison.Ordinal)).ToList();
        exceptionFilterSites.Should().HaveCount(1, "the exception-filter site must still exist, unmoved, outside this refactor's scope");

        var truthinessSites = sites.Except(exceptionFilterSites).ToList();

        var violations = truthinessSites.Where(s => s.Callee != "ReportNotTruthTestable").ToList();
        violations.Should().BeEmpty(
            "every truthiness-refusal phrase must be routed through ReportNotTruthTestable — a "
            + "literal found elsewhere is a hand-written message the helper missed. Found: "
            + string.Join("; ", violations.Select(v => $"{v.File}:{v.Line} in {v.Method}(): {v.Text}")));

        // Positive control: the scan must find the 13 real sites (not zero, which would make the
        // BeEmpty assertion above pass vacuously) — anchored to the 14 literals Decision 6 measured,
        // less the two arm-guard sites that #2237 folded into CheckGuardCondition with the
        // per-alternative guard (14 - 2 + 1).
        truthinessSites.Should().HaveCount(13,
            "13 truthiness-refusal sites, each now passing its message through ReportNotTruthTestable; got: "
            + string.Join("; ", truthinessSites.Select(v => $"{v.File}:{v.Line} in {v.Method}(): {v.Text}")));
    }
}
