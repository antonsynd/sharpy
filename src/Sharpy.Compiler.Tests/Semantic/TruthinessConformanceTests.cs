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
/// Conformance matrix for truthiness (#1558): position (9) x type (15).
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

    // ════════════════════ #1861, R-AU: RefusalNamesTheFact ════════════════════

    /// <summary>
    /// One AXIS VALUE per SITE (14), not per human-readable "position" (12) — <c>and</c> and
    /// <c>or</c> each cover TWO sites (their left and right operand), and the guard-mutation
    /// acceptance criterion ("bypass the helper at ONE site... proves per-site totality") needs each
    /// of the 14 <c>AddError</c> call sites independently exercised, or a mutation at, say, the
    /// <c>or</c> RIGHT operand specifically would slip past a matrix that only tests the left.
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
            },
            "the 14 truthiness-refusal SITES (12 human-readable positions, and/or each split into two)");
        Subjects.Select(s => s.Id).Should().BeEquivalentTo(
            new[]
            {
                "tuple-fixed", "tuple-named", "tuple-empty", "plain-class", "function-ref",
                "generic-plain-class", "clr-generic-not-sized",
            },
            "the subject axis");

        (Positions.Length * Subjects.Length).Should().Be(98, "14 sites x 7 subjects");
        RefusalCells.Count().Should().Be(98, "every cell is live — no N/A in this matrix");
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
        string[] phrases = { "must be boolean", "must be truth-testable", "must be a boolean expression" };
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

        // Positive control: the scan must find the 14 real sites (not zero, which would make the
        // BeEmpty assertion above pass vacuously) — anchored to the pre-fix literal count Decision 6
        // measured.
        truthinessSites.Should().HaveCount(14,
            "14 truthiness-refusal sites, each now passing its message through ReportNotTruthTestable; got: "
            + string.Join("; ", truthinessSites.Select(v => $"{v.File}:{v.Line} in {v.Method}(): {v.Text}")));
    }
}
