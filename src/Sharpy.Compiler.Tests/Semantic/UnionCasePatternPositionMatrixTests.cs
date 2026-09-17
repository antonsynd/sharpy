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
/// match expression}</c> — 64 executing cells (minus the property-form × match-expression cells,
/// rostered N/A citing #1890).
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

    private sealed record Union(
        string Name, string Decl, string ValueAnno, string Ctor, string Head, string Field, int CaptureVal);

    private const string TreeDecl = "union Tree[T]:\n    case Leaf()\n    case Node(value: T)\n\n";
    private const string ShapeDecl = "union Shape:\n    case Circle(radius: int)\n    case Square(side: int)\n\n";

    private static readonly Union[] Unions =
    {
        new("Result", "", "int !ValueError", "Ok(5)", "Ok", "value", 5),
        new("Optional", "", "int?", "Some(5)", "Some", "value", 5),
        new("UserGeneric", TreeDecl, "Tree[int]", "Tree.Node(7)", "Node", "value", 7),
        new("NonGeneric", ShapeDecl, "Shape", "Shape.Circle(3)", "Circle", "radius", 3),
    };

    private static readonly string[] Forms = { "zero", "capture", "asbind", "property" };
    private static readonly string[] Positions = { "top", "tuple", "list", "expr" };

    private static Union U(string name) => Unions.First(u => u.Name == name);

    // The property form is not counted by the exhaustiveness/reachability analysis in match-EXPRESSION
    // position: it reports SPY0416 or, with a wildcard, an SPY0908 ICE (CS8510) — #1890. Rostered.
    private static bool IsRostered(string form, string position)
        => form == "property" && position == "expr";

    public static IEnumerable<object[]> MatrixCells()
    {
        foreach (var u in Unions)
            foreach (var f in Forms)
                foreach (var p in Positions)
                    if (!IsRostered(f, p))
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
            "top" => $@"
{u.Decl}def check(v: {u.ValueAnno}) -> None:
    match v:
        case {pat}:
            {StmtBody(form, withK: false)}
        case _:
            print(""miss"")

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
            "expr" => $@"
{u.Decl}def d(v: {u.ValueAnno}) -> str:
    return match v:
        case {pat}: {ExprBody(form)}
        case _: ""miss""

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
    }

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        Unions.Length.Should().Be(4);
        Forms.Length.Should().Be(4);
        Positions.Length.Should().Be(4);

        var product = Unions.Length * Forms.Length * Positions.Length; // 64
        var executing = MatrixCells().Count();
        var rostered = product - executing;

        // (property form × match-expression) is rostered per union: 4 cells, #1890.
        rostered.Should().Be(Unions.Length,
            "the only N/A cells are (property form × match expression) — SPY0416/SPY0908 (#1890)");
        (executing + rostered).Should().Be(product);
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
