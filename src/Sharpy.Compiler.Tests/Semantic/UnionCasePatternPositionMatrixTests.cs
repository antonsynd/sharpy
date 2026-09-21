using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Union-case pattern matrix (P5, #1703). A union-case pattern head names its case ONCE, and the
/// verdict does not depend on the case's form or the position it is written in. Total over
/// <c>union {Result, Optional, user generic union, non-generic union}
/// × form {C(), C(v), C() as n, C(field=…)} × position {top, tuple element, list element,
/// match expression}</c> — all 64 cells execute (the #1890 property-form × match-expression N/A
/// roster is drained: Phase 9 counts the property form in exhaustiveness, Phase 12 omits a redundant
/// trailing discard under the provable lowering). Rostered in <c>gap-discovery-contracts.md</c>.
/// <para>
/// The two lowering facts are pinned by <see cref="Result_LowersViaDeconstruct"/> /
/// <see cref="Optional_LowersViaDeconstruct"/> (Result and Optional are Core STRUCTS with a synthetic
/// <c>Deconstruct</c> — <c>case Ok():</c> lowers to <c>case (true, …)</c>, NOT a nested case type,
/// which would be CS0426) and by <see cref="UserGenericUnion_LowersViaClosedCaseType"/> /
/// <see cref="NonGenericUnion_LowersViaClosedCaseType"/> (user unions lower to the CLOSED nested case
/// type <c>Tree&lt;int&gt;.Node</c> / <c>Shape.Circle</c> — the substituted vector, not
/// <c>Tree&lt;T&gt;.Node</c>, which would be CS0246). This is the Decision-5 pivot recorded in the
/// plan (Phase 3 close).
/// </para>
/// </summary>
[Collection("HeavyCompilation")]
public class UnionCasePatternPositionMatrixTests : IntegrationTestBase
{
    public UnionCasePatternPositionMatrixTests(ITestOutputHelper output) : base(output) { }

    // OtherHead is the union's SECOND case, covered zero-form as a second arm so the finite-union
    // positions (top, expr) are exhaustive WITHOUT a wildcard — the direct #1890 assertion that the
    // tested head (including the property form) is counted by exhaustiveness.
    private sealed record Union(
        string Name, string Decl, string ValueAnno, string Ctor, string Head, string Field,
        int CaptureVal, string OtherHead);

    private const string TreeDecl = "union Tree[T]:\n    case Leaf()\n    case Node(value: T)\n\n";
    private const string ShapeDecl = "union Shape:\n    case Circle(radius: int)\n    case Square(side: int)\n\n";

    private static readonly Union[] Unions =
    {
        new("Result", "", "int !ValueError", "Ok(5)", "Ok", "value", 5, "Err"),
        new("Optional", "", "int?", "Some(5)", "Some", "value", 5, "None"),
        new("UserGeneric", TreeDecl, "Tree[int]", "Tree.Node(7)", "Node", "value", 7, "Leaf"),
        new("NonGeneric", ShapeDecl, "Shape", "Shape.Circle(3)", "Circle", "radius", 3, "Square"),
    };

    private static readonly string[] Forms = { "zero", "capture", "asbind", "property" };
    private static readonly string[] Positions = { "top", "tuple", "list", "expr" };

    private static Union U(string name) => Unions.First(u => u.Name == name);

    // #1890 drained: the property form is now counted by exhaustiveness (Phase 9) and a redundant
    // trailing discard is omitted under the provable lowering (Phase 12), so (property × expr)
    // executes like every other cell — the IsRostered N/A predicate is gone.
    public static IEnumerable<object[]> MatrixCells()
    {
        foreach (var u in Unions)
            foreach (var f in Forms)
                foreach (var p in Positions)
                    yield return new object[] { u.Name, f, p };
    }

    private static string Pat(Union u, string form) => form switch
    {
        "zero" => $"{u.Head}()",
        "capture" => $"{u.Head}(w)",
        "asbind" => $"{u.Head}() as n",
        "property" => $"{u.Head}({u.Field}=w)",
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    private static string StmtBody(string form, bool withK) => form switch
    {
        "zero" => withK ? "print(\"hit\", k)" : "print(\"hit\")",
        "asbind" => withK ? "print(\"asn\", k)" : "print(\"asn\")",
        "capture" or "property" => withK ? "print(\"cap\", w, k)" : "print(\"cap\", w)",
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    private static string ExprBody(string form) => form switch
    {
        "zero" => "\"hit\"",
        "asbind" => "\"asn\"",
        "capture" or "property" => "f\"cap {w}\"",
        _ => throw new ArgumentOutOfRangeException(nameof(form)),
    };

    private static string ExpectedOutput(Union u, string form, string position)
    {
        var baseOut = form switch
        {
            "zero" => "hit",
            "asbind" => "asn",
            _ => $"cap {u.CaptureVal}",
        };
        return position == "tuple" ? baseOut + " 9" : baseOut;
    }

    private static string SourceFor(Union u, string form, string position)
    {
        var pat = Pat(u, form);
        return position switch
        {
            // Finite union scrutinee: BOTH cases covered, NO wildcard. The absence of SPY0463 here is
            // the direct #1890 assertion (the tested head — property form included — is counted).
            "top" => $@"
{u.Decl}def check(v: {u.ValueAnno}) -> None:
    match v:
        case {pat}:
            {StmtBody(form, withK: false)}
        case {u.OtherHead}():
            print(""other"")

def main() -> None:
    v: {u.ValueAnno} = {u.Ctor}
    check(v)
",
            "tuple" => $@"
{u.Decl}def check(p: tuple[{u.ValueAnno}, int]) -> None:
    match p:
        case ({pat}, k):
            {StmtBody(form, withK: true)}
        case _:
            print(""miss"")

def main() -> None:
    v: {u.ValueAnno} = {u.Ctor}
    check((v, 9))
",
            "list" => $@"
{u.Decl}def check(items: list[{u.ValueAnno}]) -> None:
    match items:
        case [{pat}]:
            {StmtBody(form, withK: false)}
        case _:
            print(""miss"")

def main() -> None:
    v: {u.ValueAnno} = {u.Ctor}
    check([v])
",
            // Finite union scrutinee in EXPRESSION position: BOTH arms, NO wildcard. For Result/Optional
            // C# proves the deconstruct-to-bool exhaustive; for user unions the closed case types are
            // covered. The redundant-trailing-wildcard variant (D5) is TrailingWildcardExpr_* below.
            "expr" => $@"
{u.Decl}def d(v: {u.ValueAnno}) -> str:
    return match v:
        case {pat}: {ExprBody(form)}
        case {u.OtherHead}(): ""other""

def main() -> None:
    v: {u.ValueAnno} = {u.Ctor}
    print(d(v))
",
            _ => throw new ArgumentOutOfRangeException(nameof(position)),
        };
    }

    [Theory]
    [MemberData(nameof(MatrixCells))]
    public void UnionCasePattern_ByFormByPosition(string unionName, string form, string position)
    {
        var u = U(unionName);
        var source = SourceFor(u, form, position);
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"[{unionName}/{form}/{position}] must compile and run. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.TrimEnd().Should().Be(ExpectedOutput(u, form, position),
            $"[{unionName}/{form}/{position}] the case head names its case in every form and position\n{source}");

        // #1890 direct: the finite-union positions (top, expr) cover BOTH cases with NO wildcard, so a
        // spurious SPY0463 "Missing cases" would fire if the tested head (property form included) went
        // uncounted. The positive control PropertyFormTop_OneCaseOmitted_WarnsSPY0463 proves the
        // assertion is not vacuous.
        if (position is "top" or "expr")
        {
            result.RawDiagnostics.Should().NotContain(d => d.Code == "SPY0463",
                $"[{unionName}/{form}/{position}] both cases are covered — exhaustive, no SPY0463\n{source}");
        }
    }

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        Unions.Length.Should().Be(4);
        Forms.Length.Should().Be(4);
        Positions.Length.Should().Be(4);

        var product = Unions.Length * Forms.Length * Positions.Length; // 64
        var executing = MatrixCells().Count();

        // #1890 drained: every cell executes now (property × expr included). No N/A roster remains.
        executing.Should().Be(product, "the #1890 (property × expr) N/A roster is drained");
    }

    // ── #1890 positive control: the absence-of-SPY0463 assertion above is not vacuous ─────────────
    // Omit the second case (no wildcard) → a genuinely non-exhaustive match MUST warn SPY0463.

    [Fact]
    public void PropertyFormTop_OneCaseOmitted_WarnsSPY0463()
    {
        var source = @"
union Tree[T]:
    case Leaf()
    case Node(value: T)

def check(t: Tree[int]) -> None:
    match t:
        case Node(value=v):
            print(""node"", v)

def main() -> None:
    t: Tree[int] = Tree.Node(7)
    check(t)
";
        var result = CompileAndExecute(source);
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0463",
            $"omitting the Leaf case (no wildcard) is non-exhaustive — SPY0463 must fire\n{source}");
    }

    // ── D5 (#1957): a redundant trailing wildcard in an EXPRESSION ────────────────────────────────
    // Both cases + a trailing `case _:`. For Result/Optional the deconstruct-to-bool lowering proves
    // the discard unreachable and it is OMITTED (no CS8510); for user unions it is kept. Both RUN.

    [Theory]
    [InlineData("Result")]
    [InlineData("Optional")]
    [InlineData("UserGeneric")]
    [InlineData("NonGeneric")]
    public void TrailingWildcardExpr_BothArmsPlusDiscard_Runs(string unionName)
    {
        var u = U(unionName);
        var source = $@"
{u.Decl}def d(v: {u.ValueAnno}) -> str:
    return match v:
        case {u.Head}(): ""hit""
        case {u.OtherHead}(): ""other""
        case _: ""wild""

def main() -> None:
    v: {u.ValueAnno} = {u.Ctor}
    print(d(v))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"[{unionName}] both arms + a redundant trailing wildcard must run (D5 omits it for the "
            + $"provable lowerings, keeps it otherwise). Diagnostics: "
            + $"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.TrimEnd().Should().Be("hit",
            $"[{unionName}] the constructed primary case is taken\n{source}");
    }

    // ── Property × or-pattern: N/A for SPY0359 (binding or-patterns), with a reachable literal twin ─
    // A binding or-pattern (`Circle(r=r) | Square(s=r)`) is SPY0359 by design (#1663 family), so the
    // (property × or-pattern) cell is N/A for THAT reason — not a classifier gap. The literal-sub
    // or-pattern below is the reachable twin proving the or-pattern head itself works.

    [Fact]
    public void PropertyOrPattern_BindingAlternatives_RefusedSPY0359()
    {
        var source = @"
union Shape:
    case Circle(radius: int)
    case Square(side: int)

def check(s: Shape) -> None:
    match s:
        case Circle(radius=r) | Square(side=r):
            print(""either"", r)
        case _:
            print(""other"")

def main() -> None:
    check(Shape.Circle(3))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeFalse($"a binding or-pattern is SPY0359 by design\n{source}");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0359",
            $"the (property × or-pattern) cell is N/A citing SPY0359\n{source}");
    }

    [Fact]
    public void LiteralOrPattern_ReachableTwin_Runs()
    {
        var source = @"
union Shape:
    case Circle(radius: int)
    case Square(side: int)

def check(s: Shape) -> None:
    match s:
        case Circle(radius=1) | Square(side=2):
            print(""matched"")
        case _:
            print(""other"")

def main() -> None:
    check(Shape.Circle(1))
    check(Shape.Square(5))
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"a literal-sub or-pattern over property forms is reachable and runs. Diagnostics: "
            + $"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be("matched\nother",
            $"the or-pattern matches Circle(radius=1), the wildcard catches Square(side=5)\n{source}");
    }

    // ── The two lowering facts (Decision-5 pivot) ─────────────────────────────────────────────

    [Fact]
    public void Result_LowersViaDeconstruct()
    {
        var result = CompileAndExecute(@"
def describe(r: int !ValueError) -> None:
    match r:
        case Ok():
            print(""ok"")
        case Err(e):
            print(""errv"", e)

def main() -> None:
    describe(Ok(5))
    describe(Err(ValueError(""bad"")))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be("ok\nerrv bad");
        result.GeneratedCSharp.Should().NotBeNull();
        result.GeneratedCSharp!.Should().Contain("case (true,",
            "Result is a Core struct: `case Ok():` lowers to a Deconstruct tuple pattern, not a nested case type");
        result.GeneratedCSharp.Should().NotContain("Result<int, global::Sharpy.ValueError>.Ok _",
            "a nested case type `Result<…>.Ok _` would be CS0426 — the mutation-B failure the Deconstruct lowering avoids");
    }

    [Fact]
    public void Optional_LowersViaDeconstruct()
    {
        var result = CompileAndExecute(@"
def describe(x: int?) -> None:
    match x:
        case Some():
            print(""some"")
        case None():
            print(""none"")

def main() -> None:
    describe(Some(5))
    describe(None())
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be("some\nnone");
        result.GeneratedCSharp.Should().NotBeNull();
        result.GeneratedCSharp!.Should().Contain("case (true,",
            "Optional is a Core struct: `case Some():` lowers to a Deconstruct tuple pattern");
    }

    [Fact]
    public void UserGenericUnion_LowersViaClosedCaseType()
    {
        var result = CompileAndExecute(@"
union Tree[T]:
    case Leaf()
    case Node(value: T)

def describe(t: Tree[int]) -> None:
    match t:
        case Leaf():
            print(""leaf"")
        case Node(v):
            print(""node"", v)

def main() -> None:
    a: Tree[int] = Tree.Leaf()
    b: Tree[int] = Tree.Node(7)
    describe(a)
    describe(b)
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be("leaf\nnode 7");
        result.GeneratedCSharp.Should().NotBeNull();
        result.GeneratedCSharp!.Should().Contain("Tree<int>.Leaf",
            "a user generic union lowers to the SUBSTITUTED closed case type `Tree<int>.Leaf` "
            + "(mutation A: `Tree<T>.Leaf` from the unsubstituted vector would be CS0246)");
        result.GeneratedCSharp.Should().Contain("Tree<int>.Node");
    }

    [Fact]
    public void NonGenericUnion_LowersViaClosedCaseType()
    {
        var result = CompileAndExecute(@"
union Shape:
    case Circle(radius: int)
    case Square(side: int)

def describe(s: Shape) -> None:
    match s:
        case Circle(r):
            print(""circle"", r)
        case Square(side):
            print(""square"", side)

def main() -> None:
    describe(Shape.Circle(3))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be("circle 3");
        result.GeneratedCSharp.Should().NotBeNull();
        result.GeneratedCSharp!.Should().Contain("Shape.Circle",
            "a non-generic union lowers to the closed nested case type `Shape.Circle`");
    }

    // ── Discriminating outputs the plan names ─────────────────────────────────────────────────

    [Fact]
    public void ResultOkPrintsOk_ErrPrintsErrvBad()
    {
        var result = CompileAndExecute(@"
def describe(r: int !ValueError) -> str:
    return match r:
        case Ok(): ""ok""
        case Err(e): f""errv {e}""

def main() -> None:
    print(describe(Ok(5)))
    print(describe(Err(ValueError(""bad""))))
");
        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Replace("\r\n", "\n").Trim().Should().Be("ok\nerrv bad");
    }
}
