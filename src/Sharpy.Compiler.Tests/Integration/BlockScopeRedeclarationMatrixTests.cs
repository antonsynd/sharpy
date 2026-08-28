using System.Linq;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Block-kind × cell matrix for the local-binding system (#1560, #1647).
/// Rows: the 15 statement-body block kinds of <see cref="BlockKinds.BodyKinds"/> plus the two
/// expression binders (lambda parameter, comprehension target). Columns: the nine scoping cells
/// of the plan, then a binder axis (walrus, inline <c>out</c>, local const, setter <c>value</c>,
/// intervening lambda). Every cell is a generated program compiled through the full pipeline and
/// asserted on its stdout VALUES or its diagnostic code — no cell is skipped; the cells red for a
/// filed class defect are carried by the ratchet in <see cref="BlockKinds.Cell"/>.
/// </summary>
/// <remarks>
/// Mutations (recorded in the commit body): (1) an allocator that never sees a conflict
/// (<c>LocalNameAllocator.Conflicts</c> returning false) turns the outer-redeclare-after cells red
/// (CS0136 behind SPY0908); (2) dropping the cross-scope predecessor link in
/// <c>TypeChecker.CheckAssignment</c> turns the write-through cells red (the block declares a
/// fresh local and the outer value is printed). A <c>defer</c> block runs at scope exit, so its
/// cells expect the deferred lines after the rest.
/// </remarks>
[Collection("HeavyCompilation")]
public class BlockScopeRedeclarationMatrixTests : IntegrationTestBase
{
    public BlockScopeRedeclarationMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines);

    /// <summary>Output order when the first block's lines are printed before the trailing lines, or after for <c>defer</c>.</summary>
    private static string Ordered(string kind, string blockLines, string afterLines)
        => BlockKinds.RunsAtExit(kind) ? Lines(afterLines, blockLines) : Lines(blockLines, afterLines);

    private ExecutionResult Run(string kind, string mainBody)
        => CompileAndExecute(BlockKinds.Program(mainBody), features: BlockKinds.FeaturesFor(kind));

    private static void AssertOutput(string kind, ExecutionResult result, string expected)
    {
        Assert.True(result.Success, $"[{kind}] should compile and run.\n{result.StandardOutput}\n{result.StandardError}\n{string.Join("\n", result.CompilationErrors)}");
        Assert.Equal(expected, result.StandardOutput.Trim().Replace("\r\n", "\n"));
    }

    private static void AssertDiagnostic(string kind, ExecutionResult result, string code)
    {
        Assert.False(result.Success, $"[{kind}] should be refused with {code}.\n{result.StandardOutput}");
        Assert.True(result.RawDiagnostics.Any(d => d.Code == code),
            $"[{kind}] should report {code}. Got: {string.Join(", ", result.RawDiagnostics.Select(d => d.Code))}.\nErrors: {string.Join("\n", result.CompilationErrors)}");
    }

    public static TheoryData<string> BodyKinds()
    {
        var data = new TheoryData<string>();
        foreach (var kind in BlockKinds.BodyKinds)
            data.Add(kind);
        return data;
    }

    // ================================================================
    // Cells 1–8 over every statement-body block kind
    // ================================================================

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void SiblingRedeclareBare(string kind)
    {
        var result = Run(kind, BlockKinds.Wrap(kind, 1, "x = 1\nprint(x)") + BlockKinds.Wrap(kind, 2, "x = 2\nprint(x)"));
        BlockKinds.Cell(kind, "SiblingRedeclareBare", () => AssertOutput(kind, result, Ordered(kind, "1", "2")));
    }

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void SiblingRedeclareAnnotated_DifferentTypes(string kind)
    {
        var result = Run(kind, BlockKinds.Wrap(kind, 1, "x: int = 1\nprint(x)") + BlockKinds.Wrap(kind, 2, "x: str = \"s\"\nprint(x)"));
        BlockKinds.Cell(kind, "SiblingRedeclareAnnotated", () => AssertOutput(kind, result, Ordered(kind, "1", "s")));
    }

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void OuterRedeclareAfterBare(string kind)
    {
        var result = Run(kind, BlockKinds.Wrap(kind, 1, "x = 1\nprint(x)") + "    x = 99\n    print(x)\n");
        BlockKinds.Cell(kind, "OuterRedeclareAfterBare", () => AssertOutput(kind, result, Ordered(kind, "1", "99")));
    }

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void OuterRedeclareAfterAnnotated(string kind)
    {
        var result = Run(kind, BlockKinds.Wrap(kind, 1, "x: int = 1\nprint(x)") + "    x: str = \"z\"\n    print(x)\n");
        BlockKinds.Cell(kind, "OuterRedeclareAfterAnnotated", () => AssertOutput(kind, result, Ordered(kind, "1", "z")));
    }

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void OuterDeclaredReassignInside_WritesThrough(string kind)
    {
        var result = Run(kind, "    x = 10\n" + BlockKinds.Wrap(kind, 1, "x = 20") + "    print(x)\n");
        // A deferred block has not run when the print executes; every other kind (the nested def
        // included, per the owner's write-through ruling) has assigned 20 by then.
        BlockKinds.Cell(kind, "WriteThrough", () => AssertOutput(kind, result, BlockKinds.RunsAtExit(kind) ? "10" : "20"));
    }

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void ReadAfterBlock_SPY0200(string kind)
    {
        var result = Run(kind, BlockKinds.Wrap(kind, 1, "y = 42") + "    print(y)\n");
        BlockKinds.Cell(kind, "ReadAfterBlock", () => AssertDiagnostic(kind, result, "SPY0200"));
    }

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void NestedThenSibling(string kind)
    {
        var nested = "try:\n    x = 1\n    print(x)\nexcept Exception:\n    pass";
        var result = Run(kind, BlockKinds.Wrap(kind, 1, nested) + BlockKinds.Wrap(kind, 2, "x = 2\nprint(x)"));
        BlockKinds.Cell(kind, "NestedThenSibling", () => AssertOutput(kind, result, Ordered(kind, "1", "2")));
    }

    /// <summary>Kinds whose body has definitely executed when a sibling that follows it runs (C# DA: <c>finally</c>, <c>using</c>, a called local function).</summary>
    // `for-else`/`while-else`: a loop that ends without `break` always runs its else body, and
    // Sharpy's DA proves it; the emitter gives the proved-assigned local a definite initializer
    // (`= default!`) so C#'s weaker DA agrees (#1656, R8 of the 2026-08-27 round).
    // `except`: the template's try body always raises (`raise ValueError(...)`), so the handler is
    // definitely run, and a handler is entered from the try statement's ENTRY state — a local the
    // first handler assigned is definitely assigned when the second handler reads it (#1664).
    private static readonly string[] DefinitelyRunBeforeSibling = { "finally", "with", "nested-def", "for-else", "while-else", "except" };

    [Theory]
    [MemberData(nameof(BodyKinds))]
    public void UseBeforeAssignInSibling_SPY0600(string kind)
    {
        // `x` is declared bare; the first block assigns it, the sibling reads it. Only a body that
        // is definitely executed before the sibling leaves x definitely assigned.
        var result = Run(kind, "    x: int\n" + BlockKinds.Wrap(kind, 1, "x = 1") + BlockKinds.Wrap(kind, 2, "print(x)"));
        BlockKinds.Cell(kind, "UseBeforeAssign", () =>
        {
            if (DefinitelyRunBeforeSibling.Contains(kind))
                AssertOutput(kind, result, "1");
            else
                AssertDiagnostic(kind, result, "SPY0600");
        });
    }

    // ================================================================
    // Cell 9: the kind's own binder spelled like a rebound outer local (#1647)
    // ================================================================

    public static TheoryData<string, string, string> BinderOverRebindedOuterData()
    {
        var data = new TheoryData<string, string, string>();
        const string outer = "    x = 10\n    if True:\n        x = 20\n";
        data.Add("for", outer + "    for x in range(3):\n        pass\n    print(x)\n", "20");
        data.Add("except", outer + "    try:\n        raise ValueError(\"e\")\n    except ValueError as x:\n        pass\n    print(x)\n", "20");
        data.Add("with", outer + "    with Resource(\"r\") as x:\n        pass\n    print(x)\n", "20");
        data.Add("match-arm", outer + "    match 7:\n        case x:\n            print(x)\n    print(x)\n", "7\n20");
        data.Add("nested-def", outer + "    def inner1(x: int) -> None:\n        print(x + 1)\n    inner1(100)\n    print(x)\n", "101\n20");
        data.Add("lambda-param", outer + "    f: (int) -> int = lambda x: x + 1\n    print(f(100))\n    print(x)\n", "101\n20");
        data.Add("comprehension-target", outer + "    ys: list[int] = [x * 2 for x in range(3)]\n    print(ys)\n    print(x)\n", "[0, 2, 4]\n20");
        return data;
    }

    [Theory]
    [MemberData(nameof(BinderOverRebindedOuterData))]
    public void BinderOverRebindedOuter_BindsTheInner(string kind, string mainBody, string expected)
    {
        AssertOutput(kind, Run(kind, mainBody), expected);
    }

    // ================================================================
    // The expression binders: lambda parameter, comprehension target
    // ================================================================

    public static TheoryData<string, string, string> ExpressionBinderData()
    {
        var data = new TheoryData<string, string, string>();
        data.Add("lambda-param/sibling",
            "    f: (int) -> int = lambda x: x + 1\n    g: (int) -> int = lambda x: x * 2\n    print(f(1))\n    print(g(2))\n", "2\n4");
        data.Add("lambda-param/outer-after",
            "    f: (int) -> int = lambda x: x + 1\n    x = 99\n    print(f(1))\n    print(x)\n", "2\n99");
        data.Add("lambda-param/outer-before-shadowed",
            "    x = 10\n    f: (int) -> int = lambda x: x + 1\n    print(f(1))\n    print(x)\n", "2\n10");
        data.Add("comprehension-target/sibling",
            "    a: list[int] = [x for x in range(2)]\n    b: list[int] = [x for x in range(2)]\n    print(a)\n    print(b)\n", "[0, 1]\n[0, 1]");
        data.Add("comprehension-target/outer-after",
            "    a: list[int] = [x for x in range(2)]\n    x = 99\n    print(a)\n    print(x)\n", "[0, 1]\n99");
        data.Add("comprehension-target/outer-before-not-leaked",
            "    x = 99\n    a: list[int] = [x for x in range(3)]\n    print(x)\n    print(a)\n", "99\n[0, 1, 2]");
        return data;
    }

    [Theory]
    [MemberData(nameof(ExpressionBinderData))]
    public void ExpressionBinders(string cell, string mainBody, string expected)
    {
        AssertOutput(cell, Run("if", mainBody), expected);
    }

    [Theory]
    [InlineData("lambda-param", "    f: (int) -> int = lambda y: y + 1\n    print(y)\n")]
    [InlineData("comprehension-target", "    a: list[int] = [y for y in range(2)]\n    print(y)\n")]
    public void ExpressionBinder_ReadAfter_SPY0200(string kind, string mainBody)
    {
        AssertDiagnostic(kind, Run("if", mainBody), "SPY0200");
    }

    // ================================================================
    // Binder axis: walrus, inline out, local const, setter value, intervening lambda
    // ================================================================

    [Fact]
    public void Walrus_RebindsAndDeclares()
    {
        var result = Run("if",
            "    x = 10\n    if (x := 30) > 0:\n        print(x)\n    print(x)\n"
            + "    if (y := 1) > 0:\n        print(y)\n    if (y := 2) > 0:\n        print(y)\n"
            + "    n: int = 0\n    while (x := n) < 2:\n        n += 1\n    print(x)\n");
        AssertOutput("walrus", result, "30\n30\n1\n2\n2");
    }

    [Fact]
    public void InlineOut_SiblingRebinds()
    {
        var source = BlockKinds.Prelude
            + "def try_parse(s: str, result: out int) -> bool:\n    result = len(s)\n    return True\n\n"
            + "def main() -> None:\n    if try_parse(\"ab\", out v: int):\n        print(v)\n    if try_parse(\"abc\", out v: int):\n        print(v)\n";
        AssertOutput("inline-out", CompileAndExecute(source), "2\n3");
    }

    [Fact]
    public void LocalConst_SiblingBlocks()
    {
        var result = Run("if", "    if True:\n        const K: int = 1\n        print(K)\n    if True:\n        const K: int = 2\n        print(K)\n");
        AssertOutput("local-const", result, "1\n2");
    }

    [Fact]
    public void SetterLocalNamedValue()
    {
        var source =
            "class C:\n    _v: int\n    def __init__(self):\n        self._v = 0\n"
            + "    property get v(self) -> int:\n        return self._v\n"
            + "    property set v(self, incoming: int) -> None:\n        if True:\n            value = incoming * 2\n            self._v = value\n"
            + "        if True:\n            value = incoming * 3\n            self._v = self._v + value\n\n"
            + "def main() -> None:\n    c: C = C()\n    c.v = 5\n    print(c.v)\n";
        AssertOutput("setter-value", CompileAndExecute(source), "25");
    }

    [Fact]
    public void InterveningLambda_ThenOuterRedeclare()
    {
        var result = Run("if", "    if True:\n        x = 1\n        print(x)\n    f: (int) -> int = lambda y: y + 1\n    x = 5\n    print(x)\n    print(f(1))\n");
        AssertOutput("intervening-lambda", result, "1\n5\n2");
    }
}
