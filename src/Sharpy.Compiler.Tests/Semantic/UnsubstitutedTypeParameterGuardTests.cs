using System;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// No diagnostic may render an UNSUBSTITUTED type parameter (#1728). A generic call whose
/// argument type is still <c>Unknown</c> — which is what a forward reference to a not-yet-checked
/// <c>const</c> produces — used to answer with the callee's raw return type, so
/// <c>const A: int = max(B, 1)</c> reported "Cannot assign type <c>'T'</c> to variable of type
/// <c>'int32'</c>": a message naming a type parameter of <c>Builtins.Max&lt;T&gt;</c>, which no
/// user program ever wrote. <c>InferGenericReturnType</c> now substitutes <c>Unknown</c> for any
/// parameter left unbound, and this is the guard that keeps it substituted.
///
/// <para><b>The absence assertion is paired with a positive control.</b> "No message contains
/// <c>'T'</c>" passes vacuously for a program that reports nothing at all, and it would also pass
/// if the scan looked for a token no message can contain. So the last test asserts the OPPOSITE
/// direction on a program where <c>T</c> is genuinely in scope: inside <c>class Box[T]</c>,
/// <c>x: int = self.value</c> must still name <c>'T'</c>, because there the type parameter IS the
/// user's own. Mutating <see cref="TypeParameterToken"/> to a token no diagnostic can hold turns
/// that control red while every absence assertion stays green — which is how this file was shown
/// falsifiable.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class UnsubstitutedTypeParameterGuardTests : IntegrationTestBase
{
    public UnsubstitutedTypeParameterGuardTests(ITestOutputHelper output) : base(output) { }

    /// <summary>The rendering the guard forbids: the display name of an unbound type parameter.</summary>
    private const string TypeParameterToken = "'T'";

    /// <summary>
    /// The cycle cells (#1728 e1, e2, e5): every one is refused by name — and the refusal is
    /// SPY0278 on both constants, never a type mismatch that names a callee's type parameter.
    /// </summary>
    [Theory]
    [InlineData("const A: int = max(B, 1)\nconst B: int = max(A, 1)\n", "e1: both sides through max()")]
    [InlineData("const A: int = max(B, 1)\nconst B: int = A + 1\n", "e2: one side through max()")]
    [InlineData("def ident(v: int) -> int:\n    return v\n\nconst A: int = ident(B)\nconst B: int = ident(A)\n",
        "e5: both sides through a user function")]
    [InlineData("const A: float = max(B, 1.0)\nconst B: float = max(A, 1.0)\n", "e1's float twin")]
    public void ConstCycle_IsRefusedBySPY0278_AndNoDiagnosticNamesATypeParameter(string decls, string cell)
    {
        var result = CompileAndExecute(decls + "\ndef main():\n    print(1)\n");

        result.Success.Should().BeFalse($"{cell} is a cycle");
        result.RawDiagnostics.Count(d => d.Code == DiagnosticCodes.Semantic.CircularConstantReference)
            .Should().Be(2, $"{cell} names both constants on the cycle");
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    /// <summary>
    /// The inference-failure cells: a generic callee whose argument is already <c>Unknown</c>
    /// (an undefined name) cannot bind its own type parameter, and the call's type must become
    /// <c>Unknown</c> rather than the raw <c>T</c> — otherwise the store one token later reports
    /// "Cannot assign type 'T' …". These are the cells the const forward references no longer
    /// reach (the const pre-pass types them by annotation), so they are what keeps
    /// <c>FinalizeCallReturnType</c> load-bearing: with it reverted, every row below renders 'T'
    /// (measured 2026-09-04 @ d5b4d4bb3 — both spellings leaked before the seam).
    /// </summary>
    [Theory]
    [InlineData("def main() -> None:\n    x: str = max(nope, 1)\n    print(x)\n",
        "builtin max: one Unknown argument beside a constant")]
    [InlineData("def main() -> None:\n    y: int = min(missing_a, missing_b)\n    print(y)\n",
        "builtin min: every argument Unknown")]
    [InlineData("def first[T](xs: list[T]) -> T:\n    return xs[0]\n\ndef main() -> None:\n    z: int = first(nope)\n    print(z)\n",
        "user generic function: Unknown argument")]
    public void GenericCallOverUnknownArgument_ReportsOnlyTheUndefinedName_NeverT(string program, string cell)
    {
        var result = CompileAndExecute(program);

        result.Success.Should().BeFalse($"{cell}: the undefined name is refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.UndefinedVariable,
            $"{cell}: the undefined name is the diagnostic (positive control that the program is checked)");
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    /// <summary>
    /// The opposite direction, and the reason the rule is scope-aware: a type parameter of the
    /// ENCLOSING class is in scope, so a generic call whose inference binds to it keeps that type
    /// and the mistyped store is refused by name — never erased to Unknown (which ICEd with CS0029
    /// between a1b22ed94 and this fix; SPY0220 naming 'U' at f7c7d3d97).
    /// </summary>
    [Fact]
    public void GenericCallBoundToAnEnclosingClassParameter_KeepsThatParameter_AndRefusesByName()
    {
        var result = CompileAndExecute(@"
def first[T](xs: list[T]) -> T:
    return xs[0]

class Box[U]:
    items: list[U]
    def __init__(self, items: list[U]) -> None:
        self.items = items
    def bad(self) -> int:
        x: int = first(self.items)
        return x

def main() -> None:
    b: Box[str] = Box([""a""])
    print(b.bad())
");
        result.Success.Should().BeFalse("storing a U into an int32 is a type mismatch");
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "the class parameter is in scope; erasing it to Unknown is what ICEd");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch && d.Message.Contains("'U'", StringComparison.Ordinal),
            "the refusal names the in-scope parameter; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
    }

    /// <summary>
    /// The forward-reference cells (#1728 e4 and its float twin): acyclic, so they RUN — the leak
    /// was a forward-reference defect, not a cycle-detection one, and the value proves the const's
    /// declared type carried the inference.
    /// </summary>
    [Theory]
    [InlineData("const A: int = max(B, 1)\nconst B: int = 4\n", "4 4", "e4: int forward reference")]
    [InlineData("const A: float = max(B, 1.0)\nconst B: float = 4.0\n", "4.0 4.0", "e4's float twin")]
    public void ConstForwardReference_Runs_AndNoDiagnosticNamesATypeParameter(
        string decls, string expected, string cell)
    {
        var result = CompileAndExecute(decls + "\ndef main():\n    print(A, B)\n");

        AssertNoDiagnosticNamesATypeParameter(result, cell);
        result.Success.Should().BeTrue(
            $"{cell} has no cycle: " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be(expected, cell);
    }

    /// <summary>
    /// The positive control. <c>T</c> in <c>class Box[T]</c> is the user's own type parameter, in
    /// scope at the store, so the message MUST name it — this is what makes the four absence
    /// assertions above measurements rather than vacuous truths, and what fails if the scanned
    /// token is changed to one no diagnostic can contain.
    /// </summary>
    [Fact]
    public void TypeParameterInScope_IsStillNamedByItsDiagnostic()
    {
        var result = CompileAndExecute(@"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def unwrap(self) -> None:
        x: int = self.value
        print(x)

def main():
    b: Box[int] = Box[int](3)
    b.unwrap()
");

        result.Success.Should().BeFalse("storing a T into an int32 is a type mismatch");
        result.RawDiagnostics.Should().Contain(
            d => d.Message.Contains(TypeParameterToken, StringComparison.Ordinal),
            "a type parameter that IS in scope is named by its diagnostic; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
    }

    private const string UnionBoxDecl =
        "union Box[T]:\n    case Full(v: T)\n    case Empty()\n\n";

    private const string UnionPairDecl =
        "union Pair[A, B]:\n    case Both(first: A, second: B)\n    case Neither()\n\n";

    /// <summary>
    /// The qualified-union route (#1770): a generic user union's case constructor infers its type
    /// arguments from the slot or the arguments and never types open. Before the fix,
    /// <c>Box.Full(1)</c> reported "expects 'T'" and <c>Box.Empty()</c> without a slot ICEd CS0305
    /// (open generic reached Roslyn). After the fix, every cell runs (or is refused by name with
    /// only closed types) and no diagnostic renders an unsubstituted type parameter.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    print(Box.Full(1))\n", "print: inference from argument")]
    [InlineData("def main():\n    b = Box.Full(1)\n    print(isinstance(b, Box))\n", "untyped local: inference from argument")]
    [InlineData("def mk() -> int:\n    return 99\n\ndef main():\n    c = Box.Full(mk())\n    print(isinstance(c, Box))\n",
        "nested-call arg: infers from return type")]
    [InlineData("def main():\n    f = Box.Full(lambda: 1)\n    print(isinstance(f, Box))\n",
        "lambda arg: infers Box[() -> int]")]
    [InlineData("def main():\n    d: Box[int] = Box.Full(42)\n    print(isinstance(d, Box))\n",
        "slot-based (existing path, positive control)")]
    public void QualifiedUnionCase_InfersClosedType_AndNoDiagnosticNamesATypeParameter(
        string body, string cell)
    {
        var result = CompileAndExecute(UnionBoxDecl + body);

        result.Success.Should().BeTrue(
            $"{cell}: " + string.Join("; ", result.CompilationErrors));
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    [Fact]
    public void QualifiedUnionCase_TwoParameterUnion_InfersClosedType()
    {
        var result = CompileAndExecute(
            UnionPairDecl + "def main():\n    p = Pair.Both(1, \"hello\")\n    print(isinstance(p, Pair))\n");

        result.Success.Should().BeTrue(
            "two-parameter inference: " + string.Join("; ", result.CompilationErrors));
        AssertNoDiagnosticNamesATypeParameter(result, "two-parameter union");
    }

    /// <summary>
    /// Wrong-slot cells: the diagnostic names only closed types, never the raw type parameter.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    x: bool = Box.Full(1)\n    print(x)\n",
        "bool slot: SPY0220 names a closed type, not T")]
    [InlineData("def main():\n    b: Box[int] = Box.Full(\"s\")\n    print(b)\n",
        "mistyped with slot: SPY0220 names int32, not T")]
    public void QualifiedUnionCase_WrongSlot_RefusesByClosedType_NeverT(string body, string cell)
    {
        var result = CompileAndExecute(UnionBoxDecl + body);

        result.Success.Should().BeFalse($"{cell}: the slot mismatch is refused");
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    /// <summary>
    /// <c>Box.Empty()</c> without a slot cannot infer type arguments (no fields to unify
    /// against) and must report SPY0227, not ICE with CS0305 (open generic reaching Roslyn).
    /// </summary>
    [Fact]
    public void QualifiedUnionCase_EmptyNoSlot_IsSPY0227_NotCS0305()
    {
        var result = CompileAndExecute(
            UnionBoxDecl + "def main():\n    e = Box.Empty()\n    print(e)\n");

        result.Success.Should().BeFalse("Empty() without a slot cannot infer type arguments");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.CannotInferType,
            "SPY0227 with annotation steer, not CS0305; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "the open generic must not reach Roslyn");
        AssertNoDiagnosticNamesATypeParameter(result, "Empty() no slot");
    }

    private const string UnionShapeDecl =
        "union Shape:\n    case Circle(r: int)\n    case Square(s: int)\n\n";

    /// <summary>A two-arm match on <c>b</c>: the discriminator of the inferred-union-local class.</summary>
    private const string MatchB =
        "    match b:\n        case Full(x):\n            print(x)\n        case Empty():\n            print(\"empty\")\n";

    /// <summary>
    /// An INFERRED union local is the union at every later use (#1770, plan-757fbb Decision 10). The
    /// checker records the closed union type for <c>b = Box.Full(7)</c>, but the emitted
    /// <c>var b = new Box&lt;int&gt;.Full(7);</c> let C# infer the CASE class, so a second match arm
    /// was CS8121 and a store of another case CS0029 — at every declaration site: plain assignment,
    /// tuple / mixed / star / nested unpacking, the walrus hoist, a conditional RHS, a nested-block
    /// store. BASE (dff55b2cd) hid the class behind SPY0220 "expects 'T'"; HEAD-before (852bf488b)
    /// ICEd SPY0908 on every cell here except the slot-present twin and the list/dict cells, whose
    /// element type was already the union. Every cell executes and its stdout is asserted (§4
    /// recorded ≠ applied) — the ICE class is refuted only by <c>run</c>, never by <c>emit</c>.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    b = Box.Full(7)\n" + MatchB, "7", "two-arm match on an inferred local")]
    [InlineData("def main():\n    b = Box.Full(7)\n    b = Box.Empty()\n" + MatchB, "empty", "store of the other case, then match")]
    [InlineData("def main():\n    b = Box.Full(7)\n" + MatchB + "    b = Box.Empty()\n" + MatchB, "7\nempty", "match, store, match")]
    [InlineData("def main():\n    b = Box.Full(7)\n    if len(\"ab\") == 2:\n        b = Box.Empty()\n" + MatchB, "empty", "nested-block store")]
    [InlineData("def main():\n    flag = len(\"a\") == 1\n    b = Box.Full(1) if flag else Box.Full(2)\n    b = Box.Empty()\n" + MatchB, "empty", "conditional RHS")]
    [InlineData("def mk() -> int:\n    return 99\n\ndef main():\n    b = Box.Full(mk())\n" + MatchB, "99", "nested-call argument")]
    [InlineData("def main():\n    xs = [Box.Full(1), Box.Full(2)]\n    xs.append(Box.Empty())\n    for b in xs:\n"
        + "        match b:\n            case Full(x):\n                print(x)\n            case Empty():\n                print(\"empty\")\n",
        "1\n2\nempty", "list literal joins to list[Box[int]]; append takes the slot")]
    [InlineData("def main():\n    xs: list[Box[int]] = [Box.Full(1), Box.Empty()]\n    for b in xs:\n"
        + "        match b:\n            case Full(x):\n                print(x)\n            case Empty():\n                print(\"empty\")\n",
        "1\nempty", "list with a slot")]
    [InlineData("def main():\n    d = {\"k\": Box.Full(1)}\n    for k, v in d.items():\n"
        + "        match v:\n            case Full(x):\n                print(k, x)\n            case Empty():\n                print(\"empty\")\n",
        "k 1", "dict value")]
    [InlineData("def main():\n    b = Box.Full(Box.Full(1))\n    match b:\n        case Full(inner):\n"
        + "            match inner:\n                case Full(x):\n                    print(x)\n                case Empty():\n                    print(\"inner empty\")\n"
        + "        case Empty():\n            print(\"empty\")\n",
        "1", "nested Box.Full(Box.Full(1))")]
    [InlineData("def main():\n    o: int? = Some(1)\n    b = Box.Full(o)\n    match b:\n        case Full(x):\n"
        + "            match x:\n                case Some(v):\n                    print(v)\n                case None():\n                    print(\"none\")\n"
        + "        case Empty():\n            print(\"empty\")\n",
        "1", "Optional payload: Box[int?]")]
    [InlineData("def main():\n    x = (b := Box.Full(1))\n    b = Box.Empty()\n" + MatchB, "empty", "walrus target")]
    [InlineData("def main():\n    a, b = Box.Full(1), Box.Full(2)\n    b = Box.Empty()\n"
        + "    match a:\n        case Full(x):\n            print(x)\n        case Empty():\n            print(\"empty\")\n" + MatchB,
        "1\nempty", "tuple deconstruction, all new")]
    [InlineData("def main():\n    a = Box.Full(1)\n    a, b = Box.Full(2), Box.Full(3)\n    b = Box.Empty()\n"
        + "    match a:\n        case Full(x):\n            print(x)\n        case Empty():\n            print(\"empty\")\n" + MatchB,
        "2\nempty", "tuple deconstruction, mixed new/existing")]
    [InlineData("def main():\n    first, *rest = Box.Full(1), Box.Full(2), Box.Full(3)\n    first = Box.Empty()\n"
        + "    match first:\n        case Full(x):\n            print(x)\n        case Empty():\n            print(\"empty\")\n    print(len(rest))\n",
        "empty\n2", "star unpacking")]
    [InlineData("def main():\n    (a, b), c = (Box.Full(1), Box.Full(2)), Box.Full(3)\n    a = Box.Empty()\n    c = Box.Empty()\n"
        + "    match a:\n        case Full(x):\n            print(x)\n        case Empty():\n            print(\"empty\")\n" + MatchB
        + "    match c:\n        case Full(x):\n            print(x)\n        case Empty():\n            print(\"empty\")\n",
        "empty\n2\nempty", "nested tuple unpacking")]
    [InlineData("def main():\n    b: Box[int] = Box.Full(7)\n    b = Box.Empty()\n" + MatchB, "empty", "slot-present twin (kept)")]
    public void InferredUnionLocal_IsTheUnionAtEveryLaterUse(string body, string expectedStdout, string cell)
    {
        var result = CompileAndExecute(UnionBoxDecl + body);

        result.Success.Should().BeTrue(
            $"{cell}: " + string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Should().Be(expectedStdout, cell);
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    /// <summary>
    /// The same seam for a NON-generic union and a two-parameter union: <c>var s = new Shape.Circle(1)</c>
    /// was <c>Shape.Circle</c>, so <c>s = Shape.Square(2)</c> ICEd CS0029 at BASE and HEAD alike —
    /// the class predates Phase 5, which only made it reachable for generic unions.
    /// </summary>
    [Fact]
    public void InferredUnionLocal_NonGenericUnion_TakesTheOtherCase()
    {
        var result = CompileAndExecute(UnionShapeDecl
            + "def main():\n    s = Shape.Circle(1)\n    s = Shape.Square(2)\n"
            + "    match s:\n        case Circle(r):\n            print(r)\n        case Square(side):\n            print(side)\n");

        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("2");
    }

    [Fact]
    public void InferredUnionLocal_TwoParameterUnion_TakesTheOtherCase()
    {
        var result = CompileAndExecute(UnionPairDecl
            + "def main():\n    p = Pair.Both(1, \"s\")\n    p = Pair.Neither()\n"
            + "    match p:\n        case Both(a, b):\n            print(a, b)\n        case Neither():\n            print(\"neither\")\n");

        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("neither");
        AssertNoDiagnosticNamesATypeParameter(result, "two-parameter union, other case");
    }

    /// <summary>
    /// <c>None</c> is a value with no type of its own: inferring <c>T := None</c> (or a tuple that
    /// contains it) printed <c>void</c> as a C# type argument — SPY0599 at HEAD-before, SPY0220
    /// "expects 'T'" at BASE. The refusal is SPY0227 with the nullable steer; its text quotes no type
    /// parameter and no message anywhere says <c>void</c>. The absence assertions have their positive
    /// control in the mutation record (disable <c>MentionsNoneType</c> → SPY0599 "Keyword 'void'").
    /// The slot-present twin <c>b: Box[int | None] = Box.Full(None)</c> runs.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    b = Box.Full(None)\n    print(b)\n", "bare None argument")]
    [InlineData("def main():\n    b = Box.Full((1, None))\n    print(b)\n", "None inside a tuple argument")]
    public void QualifiedUnionCase_NoneArgument_IsSPY0227WithTheNullableSteer_NeverVoid(string body, string cell)
    {
        var result = CompileAndExecute(UnionBoxDecl + body);

        result.Success.Should().BeFalse($"{cell}: None names no type argument");
        var refusal = result.RawDiagnostics.Should().ContainSingle(
            d => d.Code == DiagnosticCodes.Semantic.CannotInferType,
            $"{cell}: SPY0227 with the nullable steer; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}"))).Subject;
        refusal.Message.Should().Contain("| None", "the steer spells the payload as nullable");
        refusal.Message.Should().NotContain(TypeParameterToken, "the refusal quotes no type parameter");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError
              || d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "void must not reach the emitter");
        result.RawDiagnostics.Should().NotContain(
            d => d.Message.Contains("void", StringComparison.Ordinal),
            "no message says void");
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    [Fact]
    public void QualifiedUnionCase_NoneArgument_TwoParameterUnion_IsSPY0227()
    {
        var result = CompileAndExecute(UnionPairDecl + "def main():\n    p = Pair.Both(None, 1)\n    print(p)\n");

        result.Success.Should().BeFalse("None names no type argument");
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.CannotInferType);
        result.RawDiagnostics.Should().NotContain(d => d.Message.Contains("void", StringComparison.Ordinal));
        AssertNoDiagnosticNamesATypeParameter(result, "Pair.Both(None, 1)");
    }

    [Fact]
    public void QualifiedUnionCase_NoneArgument_WithNullableSlot_Runs()
    {
        var result = CompileAndExecute(UnionBoxDecl
            + "def main():\n    b: Box[int | None] = Box.Full(None)\n" + MatchB);

        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        result.StandardOutput.Trim().Should().Be("None");
    }

    /// <summary>
    /// <c>Box[str].Full("s")</c>: type arguments on the qualifier are not a spelling the language
    /// has (tagged_unions.md — the qualified form takes them from the annotation or the arguments).
    /// It reached Roslyn as <c>Box&lt;string&gt;.Full("s")</c>, CS1955, at BASE and HEAD alike; it is
    /// refused by name with the annotation steer.
    /// </summary>
    [Theory]
    [InlineData("def main():\n    b = Box[str].Full(\"s\")\n    print(b)\n", "type argument on Full")]
    [InlineData("def main():\n    b = Box[int].Empty()\n    print(b)\n", "type argument on Empty")]
    public void QualifiedUnionCase_TypeArgumentsOnTheQualifier_AreRefusedByName_NeverCS1955(string body, string cell)
    {
        var result = CompileAndExecute(UnionBoxDecl + body);

        result.Success.Should().BeFalse($"{cell}: the qualifier spelling is refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.UnsupportedFeature
                 && d.Message.Contains("Box[...].", StringComparison.Ordinal)
                 && d.Message.Contains("x: Box[T] = Box.", StringComparison.Ordinal),
            $"{cell}: SPY0358 naming the spelling and steering to the annotation; got "
            + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "the spelling must not reach Roslyn");
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    /// <summary>
    /// Arity is checked before inference, so <c>Box.Full(1, 2)</c> is SPY0224 with or without a slot.
    /// Without one it used to fall into inference and surface as SPY0227 "cannot infer".
    /// </summary>
    [Theory]
    [InlineData("def main():\n    b = Box.Full(1, 2)\n    print(b)\n", "no slot, too many")]
    [InlineData("def main():\n    b: Box[int] = Box.Full(1, 2)\n    print(b)\n", "slot, too many")]
    [InlineData("def main():\n    e = Box.Empty(1)\n    print(e)\n", "no slot, Empty with an argument")]
    [InlineData("def main():\n    e: Box[int] = Box.Empty(1)\n    print(e)\n", "slot, Empty with an argument")]
    public void QualifiedUnionCase_WrongArity_IsSPY0224_WithOrWithoutASlot(string body, string cell)
    {
        var result = CompileAndExecute(UnionBoxDecl + body);

        result.Success.Should().BeFalse($"{cell}: the arity error is refused");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount,
            $"{cell}: SPY0224; got " + string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}:{d.Message}")));
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Semantic.CannotInferType,
            $"{cell}: an arity error is not an inference failure");
        AssertNoDiagnosticNamesATypeParameter(result, cell);
    }

    private static void AssertNoDiagnosticNamesATypeParameter(ExecutionResult result, string cell)
    {
        // SPY0237 and SPY0227 ("Cannot infer type arguments …") are ABOUT the parameter they name
        // — the diagnostic whose subject is the type parameter itself, not a type that leaked one.
        var offenders = result.RawDiagnostics
            .Where(d => d.Code != DiagnosticCodes.Semantic.CannotInferGenericType
                     && d.Code != DiagnosticCodes.Semantic.CannotInferType)
            .Where(d => d.Message.Contains(TypeParameterToken, StringComparison.Ordinal))
            .Select(d => $"{d.Code}:{d.Message}")
            .ToList();

        offenders.Should().BeEmpty(
            $"{cell} must not report a message naming an unsubstituted type parameter");
    }
}
