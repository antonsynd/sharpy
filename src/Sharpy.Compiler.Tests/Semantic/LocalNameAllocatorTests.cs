using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1560 D1 §3 / #1647: the <see cref="LocalNameAllocator"/> assigns every local's C# spelling
/// at <c>CodeGenInfoComputer.ComputeForModule</c>. These are the only spelling pins in the repo:
/// the cells 00a447709 deleted from <c>NameResolutionServiceTests</c> (the <c>ResolveLocalName</c>
/// / <c>ComputeNextVersion</c> region) and <c>RoslynEmitterVariableRedefinitionTests</c> are
/// re-expressed here against <see cref="CodeGenInfo"/>, with the same expected spellings, plus
/// the cells the ledger/fold model adds.
/// Mutation (recorded in the commit body): making <c>LocalNameAllocator.Conflicts</c> return false
/// (claims never conflict) turns every versioned cell here red.
/// </summary>
public class LocalNameAllocatorTests
{
    private static LocalBindingTestHarness.Analysis Analyze(string mainBody, string prelude = "")
        => LocalBindingTestHarness.Analyze(prelude + "def main() -> None:\n" + mainBody, computeCodeGenInfo: true);

    // ---- relocated from RoslynEmitterVariableRedefinitionTests (emitted `var x = 1; var x_1 = 2; var x_2 = 3; return x_2`)

    [Fact]
    public void Redeclaration_Versions_x_x1_x2()
    {
        var a = Analyze("    x = 1\n    x: int = 2\n    x: int = 3\n    print(x)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1", "x_2");
    }

    [Fact]
    public void AnnotatedRedeclaration_Versions()
    {
        var a = Analyze("    x: int = 1\n    x: int = 2\n    print(x)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1");
    }

    [Fact]
    public void DifferentTypeRedeclaration_Versions()
    {
        var a = Analyze("    x = 1\n    x: str = \"hello\"\n    print(x)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1");
    }

    [Fact]
    public void TupleRebinding_SharesOneSpelling()
    {
        // `var(x, y) = (1, 2); (x, y) = (3, 4);` — an update, never `var(x_1`.
        var a = Analyze("    x, y = 1, 2\n    x, y = 3, 4\n    print(x)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x");
        a.Spellings("function:main", "y").Should().Equal("y", "y");
    }

    [Fact]
    public void AugmentedAssignment_UsesTheCurrentVersion()
    {
        // `var x = 1; x = x + 1; var x_1 = 10; x_1 = x_1 + 5; return x_1;`
        var a = Analyze("    x = 1\n    x += 1\n    x: int = 10\n    x += 5\n    print(x)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1");
    }

    // ---- relocated from NameResolutionServiceTests (ResolveLocalName / ComputeNextVersion)

    [Fact]
    public void ThirdRedeclaration_IsCounter2()
    {
        // ResolveName_LocalDeclarationWithVersioning_WorksCorrectly: version 1 held → "counter_2".
        var a = Analyze("    counter = 0\n    counter: int = 1\n    counter: int = 2\n    print(counter)\n");
        a.Spellings("function:main", "counter").Should().Equal("counter", "counter_1", "counter_2");
    }

    [Fact]
    public void SourceName_x_1_Coexists_AndTheVersionSkipsIt()
    {
        // ResolveLocalName_CollisionWithSourceName_SkipsToNextVersion: user declared x_1 → "x_2".
        // The source `x_1` itself camelCases to `x1` (GenerateFunction_UserDeclaredX1_NoCollision).
        var a = Analyze("    x = 1\n    x_1 = \"user\"\n    x: int = 2\n    print(x)\n    print(x_1)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_2");
        a.Spellings("function:main", "x_1").Should().Equal("x1");
    }

    [Fact]
    public void MultipleSourceCollisions_SkipAll()
    {
        // ResolveLocalName_MultipleCollisions_SkipsAllCollisions: x_1, x_2, x_3 held → "x_4".
        var a = Analyze("    x = 1\n    `x_1` = 1\n    `x_2` = 2\n    `x_3` = 3\n    x: int = 2\n    print(x)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_4");
    }

    [Fact]
    public void EscapedSource_x_1_IsVerbatim_AndTheVersionSkipsIt()
    {
        var a = Analyze("    x = 1\n    `x_1` = \"user\"\n    x: int = 2\n    print(x)\n    print(`x_1`)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_2");
        a.Spellings("function:main", "x_1").Should().Equal("x_1");
    }

    [Fact]
    public void SnakeCase_IsCamelCased()
    {
        // ResolveLocalName_SnakeCaseName_ConvertsToCamelCase.
        var a = Analyze("    my_var = 1\n    print(my_var)\n");
        a.Spellings("function:main", "my_var").Should().Equal("myVar");
    }

    [Fact]
    public void TrailingUnderscore_IsPreserved_AndVersionsAfterIt()
    {
        var a = Analyze("    x_ = 1\n    x_: int = 2\n    print(x_)\n");
        a.Spellings("function:main", "x_").Should().Equal("x_", "x__1");
    }

    [Fact]
    public void LocalConst_KeepsConstantCasing()
    {
        var a = Analyze("    const MAX_VALUE: int = 100\n    print(MAX_VALUE)\n");
        a.Spellings("function:main", "MAX_VALUE").Should().Equal("MAX_VALUE");
        a.Binding.GetCodeGenInfo(a.Ledger("function:main").Entries[0].Symbol)!.IsConstant.Should().BeTrue();
    }

    // ---- the ledger / fold model's own cells

    [Fact]
    public void SiblingBlocks_AreVersioned()
    {
        var a = Analyze("    if True:\n        x = 1\n    if True:\n        x = 2\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1");
    }

    [Fact]
    public void ClosedChildBlock_ThenOuter_VersionsTheOuter()
    {
        var a = Analyze("    if True:\n        x = 1\n    x = 5\n    print(x)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1");
    }

    [Fact]
    public void SiblingLocalConsts_AreVersioned()
    {
        var a = Analyze("    if True:\n        const K: int = 1\n    if True:\n        const K: int = 2\n");
        a.Spellings("function:main", "K").Should().Equal("K", "K_1");
    }

    [Fact]
    public void TwoMatchArms_CapturingTheSameName_AreVersioned()
    {
        var a = Analyze("    match 1:\n        case 1:\n            n = 1\n        case _:\n            n = 2\n");
        a.Spellings("function:main", "n").Should().Equal("n", "n_1");
    }

    [Fact]
    public void ChainMembers_ShareTheHeadsSpelling()
    {
        var a = Analyze("    x = 1\n    if True:\n        x = 2\n    x = 3\n    print(x)\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x", "x");
    }

    [Fact]
    public void NestedDefParameter_CollidingWithAnOuterLocal_IsVersioned()
    {
        var a = Analyze("    x = 1\n    def g(x: int) -> int:\n        return x + 1\n    print(g(x))\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1");
    }

    [Fact]
    public void LambdaParameter_CollidingWithARebindedOuterLocal_IsVersioned()
    {
        // #1647: `x = 10; if True: x = 20` is one chain spelled `x`; the lambda's own `x` is a
        // parameter in a scope the function scope encloses, so it takes the next free spelling.
        var a = Analyze("    x = 10\n    if True:\n        x = 20\n    f: (int) -> int = lambda x: x + 1\n    print(f(100))\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x", "x_1");
    }

    [Fact]
    public void LambdaParameter_OverAnOuterLocalDeclaredAfterIt_VersionsTheOuter()
    {
        var a = Analyze("    f: (int) -> int = lambda x: x + 1\n    x = 5\n    print(f(x))\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1");
    }

    [Fact]
    public void SiblingLambdas_KeepTheSameParameterSpelling()
    {
        // Two lambdas' parameter scopes are siblings, which C# allows — and the .expected.cs
        // corpus pins `lst.Where(x => ...)` followed by `lst.Select(x => ...)`.
        var a = Analyze("    f: (int) -> int = lambda x: x + 1\n    g: (int) -> int = lambda x: x * 2\n    print(f(1) + g(2))\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x");
    }

    [Fact]
    public void InterveningLambda_DoesNotHideAClosedBlocksClaim()
    {
        // The C1 cell: `x = 5` after the lambda must still see the closed if-block's `x`.
        var a = Analyze("    if True:\n        x = 1\n    f: (int) -> int = lambda y: y + 1\n    x = 5\n    print(x + f(1))\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1");
    }

    [Fact]
    public void InterveningNestedDef_DoesNotHideAClosedBlocksClaim()
    {
        var a = Analyze("    if True:\n        x = 1\n    def g(y: int) -> int:\n        return y + 1\n    x = 5\n    print(x + g(1))\n");
        a.Spellings("function:main", "x").Should().Equal("x", "x_1");
    }

    [Fact]
    public void NestedDefLocal_ThenOuterLocal_VersionsTheOuter()
    {
        var a = Analyze("    def g() -> int:\n        y = 1\n        return y\n    y = 5\n    print(y + g())\n");
        a.Spellings("function:main", "y").Should().Equal("y", "y_1");
    }

    [Fact]
    public void SetterLocalNamedValue_IsVersionedPastTheImplicitValue()
    {
        var a = LocalBindingTestHarness.Analyze(
            "class C:\n    _v: int\n    def __init__(self):\n        self._v = 0\n"
            + "    property get v(self) -> int:\n        return self._v\n"
            + "    property set v(self, incoming: int) -> None:\n        if True:\n            value = incoming * 2\n            self._v = value\n"
            + "        if True:\n            value = incoming * 3\n            self._v = self._v + value\n",
            computeCodeGenInfo: true);
        a.Spellings("property:v:Set", "value").Should().Equal("value_1", "value_2");
        a.Spellings("property:v:Set", "incoming").Should().Equal("incoming");
    }

    [Fact]
    public void GetterLocalNamedValue_IsNotReserved()
    {
        var a = LocalBindingTestHarness.Analyze(
            "class C:\n    _v: int\n    def __init__(self):\n        self._v = 0\n"
            + "    property get v(self) -> int:\n        value = self._v\n        return value\n",
            computeCodeGenInfo: true);
        a.Spellings("property:v:Get", "value").Should().Equal("value");
    }

    [Fact]
    public void EveryLedgerVariable_HasCodeGenInfo()
    {
        var a = Analyze(
            "    x = 1\n    if True:\n        x = 2\n        y: int = 3\n    f: (int) -> int = lambda z: z + x\n"
            + "    def g(w: int) -> int:\n        return w\n    for i in range(2):\n        pass\n    print(f(1) + g(2) + y)\n");
        foreach (var ledger in a.SymbolTable.AllLedgers.Values)
        {
            foreach (var entry in ledger.Entries)
            {
                if (entry.Symbol is VariableSymbol)
                    a.Binding.GetCodeGenInfo(entry.Symbol).Should().NotBeNull($"{entry.Symbol.Name} in {ledger.OwnerScopeName}");
            }
        }
    }
}
