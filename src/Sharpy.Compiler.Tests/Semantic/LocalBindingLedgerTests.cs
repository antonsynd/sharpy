using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1560 D1 §1: <see cref="SymbolTable.Define"/> appends every local binding to the ledger of the
/// nearest function-like owner — structurally, so no binding kind can skip it. One test per
/// binding kind asserts the exact <c>(name, scope nesting, ordinal)</c> rows.
/// Mutation (recorded in the commit body): removing the <c>ledger.Append</c> in
/// <c>SymbolTable.Define</c> empties every ledger and turns every test here red.
/// </summary>
public class LocalBindingLedgerTests
{
    private static string InMain(string body) => "def main() -> None:\n" + body;

    [Fact]
    public void Parameters_AreTheFirstRowsOfTheirFunction()
    {
        var a = LocalBindingTestHarness.Analyze("def f(a: int, b: str) -> None:\n    pass\n");
        a.Rows(a.Ledger("function:f")).Should().Equal("a@#0", "b@#1");
    }

    [Fact]
    public void Assignment_EachRebindingIsItsOwnRow()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    x = 1\n    x = 2\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("x@#0", "x@#1");
    }

    [Fact]
    public void AnnotatedDeclaration_IsOneRow()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    x: int = 1\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("x@#0");
    }

    [Fact]
    public void TupleUnpacking_BindsEachElement()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    p, q = 1, 2\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("p@#0", "q@#1");
    }

    [Fact]
    public void StarUnpacking_BindsTheStarredName()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    xs: list[int] = [1, 2, 3]\n    head, *rest = xs\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("xs@#0", "head@#1", "rest@#2");
    }

    [Fact]
    public void ForIdentifier_IsBoundInTheLoopBodyScope()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    for i in range(3):\n        pass\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("i@for-body#0");
    }

    [Fact]
    public void ForTuple_BindsEachElementInTheLoopBodyScope()
    {
        var a = LocalBindingTestHarness.Analyze(InMain(
            "    d: dict[str, int] = {\"a\": 1}\n    for k, v in d.items():\n        pass\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("d@#0", "k@for-body#1", "v@for-body#2");
    }

    [Fact]
    public void ComprehensionTarget_IsBoundInTheComprehensionScope()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    ys: list[int] = [i for i in range(3)]\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("i@list-comprehension#0", "ys@#1");
    }

    [Fact]
    public void Walrus_IsBoundInTheScopeOfTheCondition()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    if (n := 3) > 0:\n        pass\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("n@#0");
    }

    [Fact]
    public void InlineOut_IsBoundInTheScopeOfTheCall()
    {
        var a = LocalBindingTestHarness.Analyze(
            "def tp(s: str, r: out int) -> bool:\n    r = 1\n    return True\n\n"
            + InMain("    if tp(\"1\", out v: int):\n        pass\n"));
        a.Rows(a.Ledger("function:tp")).Should().Equal("s@#0", "r@#1", "r@#2");
        a.Rows(a.Ledger("function:main")).Should().Equal("v@#0");
    }

    [Fact]
    public void WithAs_IsBoundInTheWithScope()
    {
        var a = LocalBindingTestHarness.Analyze(
            Integration.BlockKinds.Prelude + InMain("    with Resource(\"a\") as r:\n        pass\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("r@with#0");
    }

    [Fact]
    public void ExceptAs_IsBoundInTheExceptScope()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    try:\n        pass\n    except Exception as e:\n        pass\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("e@except#0");
    }

    [Fact]
    public void MatchCapture_BareName_IsBoundInTheCaseScope()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    match 7:\n        case n:\n            pass\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("n@match-case#0");
    }

    [Fact]
    public void MatchCapture_TypePatternAs_IsBoundInTheCaseScope()
    {
        var a = LocalBindingTestHarness.Analyze(InMain(
            "    v: object = 7\n    match v:\n        case int() as m:\n            pass\n        case _:\n            pass\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("v@#0", "m@match-case#1");
    }

    [Fact]
    public void MatchCapture_TupleElements_AreBoundInTheCaseScope()
    {
        var a = LocalBindingTestHarness.Analyze(InMain(
            "    t: tuple[int, int] = (1, 2)\n    match t:\n        case (p, q):\n            pass\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("t@#0", "p@match-case#1", "q@match-case#2");
    }

    [Fact]
    public void LambdaParameter_HasItsOwnNestedLedger()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    f: (int) -> int = lambda a: a + 1\n"));
        var main = a.Ledger("function:main");
        var lambda = a.Ledger("lambda");
        a.Rows(lambda).Should().Equal("a@#0");
        lambda.IsNested.Should().BeTrue();
        lambda.ParentOwnerScopeId.Should().Be(main.OwnerScopeId);
        a.Rows(main).Should().Equal("f@#0");
    }

    [Fact]
    public void NestedDefParameter_HasItsOwnNestedLedger()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    def inner(b: int) -> int:\n        return b\n    print(inner(1))\n"));
        var main = a.Ledger("function:main");
        var inner = a.Ledger("function:inner");
        a.Rows(inner).Should().Equal("b@#0");
        inner.IsNested.Should().BeTrue();
        inner.ParentOwnerScopeId.Should().Be(main.OwnerScopeId);
        // The nested def's own symbol is a (non-variable) row of the enclosing ledger.
        main.Entries.Select(e => e.Symbol.Name).Should().Contain("inner");
    }

    [Fact]
    public void LocalConst_IsARow()
    {
        var a = LocalBindingTestHarness.Analyze(InMain("    const K: int = 1\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("K@#0");
    }

    [Fact]
    public void AccessorValueParameter_AndSetterLocal_AreRowsOfTheSetterLedger()
    {
        var a = LocalBindingTestHarness.Analyze(
            "class C:\n    _v: int\n    def __init__(self):\n        self._v = 0\n"
            + "    property get v(self) -> int:\n        return self._v\n"
            + "    property set v(self, incoming: int) -> None:\n        value = incoming * 2\n        self._v = value\n");
        var setter = a.Ledger("property:v:Set");
        a.Rows(setter).Should().Equal("self@#0", "incoming@#1", "value@#2");
        setter.ReservesImplicitValue.Should().BeTrue();
        a.Ledger("property:v:Get").ReservesImplicitValue.Should().BeFalse();
    }

    [Fact]
    public void ExitingANestedOwner_RestoresTheEnclosingLedger()
    {
        // The C1 defect: after the lambda, `x = 5` went into the lambda's ledger.
        var a = LocalBindingTestHarness.Analyze(InMain(
            "    if True:\n        x = 1\n    f: (int) -> int = lambda y: y + 1\n    x = 5\n"));
        a.Rows(a.Ledger("function:main")).Should().Equal("x@if-then#0", "f@#1", "x@#2");
        a.Rows(a.Ledger("lambda")).Should().Equal("y@#0");
    }
}
