using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Super-dunder lowering (#1740): <c>super().__op__(args)</c> inside an operator dunder lowers to
/// a CAST operator application — <c>((Base)receiver) op args</c> — valid in EVERY host, because C#
/// operator resolution is static, so the cast alone selects the base's own operator with no
/// <c>base</c> keyword (never a legal bare operand, CS0175). The kind-enumerating
/// <c>ContainsSuperExpression</c> AST walker this replaced is gone; the emitter reads two
/// materialized facts instead: the node-keyed <c>OperatorLowering(SuperOperatorApplication)</c>
/// (selects the cast route) and the symbol-keyed <c>CodeGenInfo.RequiresInstanceImpl</c> (keeps the
/// private-instance-method split alive ONLY for a super call to a non-operator dunder/regular
/// method inside a body that may be inlined as a static C# operator).
///
/// <para><b>Locks in p3-codegen's ad-hoc probes (694b29156) as a standing execution matrix.</b>
/// Every cell EXECUTES and asserts python3's value — see the class remarks below for the axes.</para>
///
/// <para><b>Axis A — walker-missed hosts (12 cells, s01-s08/s17/s18/s20/s22).</b> The SAME
/// <c>super().__add__(other)</c> call, inside <c>Derived.__add__</c>, embedded in every AST shape
/// the deleted walker's kind switch used to miss: return, list element, for-loop body, keyword
/// argument, comprehension, index store, ternary, try body, lambda body, if body,
/// assign-then-return, with-body. The node-keyed fact fires regardless of the AST shape
/// surrounding the call — that is the entire point of replacing a walker with a fact — so every
/// cell prints the SAME arithmetic (<c>self.v + other.v</c>) it would print inlined directly,
/// mostly <c>1</c> (chosen so python3 and Sharpy agree trivially); s01/s02 use v=1,other.v=2 → 3
/// (the plan's own named values) and s05 (comprehension, two iterations) uses v=5,other.v=5 → 20.</para>
///
/// <para><b>Axis B — Base-typed receiver, comparison/unary family (4 cells, s11-s14).</b> A
/// variable declared <c>Base</c> (not <c>Derived</c>) holds a <c>Derived</c> instance; the operator
/// call site dispatches virtually to <c>Derived</c>'s override, which applies
/// <c>super().__op__(...)</c> via the cast — proving the lowering is correct through every operator
/// FAMILY (comparison, unary, bitwise, arithmetic), not just <c>__add__</c>.</para>
///
/// <para><b>Axis C — the split-honesty cell (s21).</b> A method-lowered super call
/// (<c>super().__str__()</c>, NOT cast-eligible — <c>DunderMapping</c> has no operator syntax for
/// it) INSIDE an operator dunder (<c>__lt__</c>) still needs <c>RequiresInstanceImpl</c> to keep the
/// private-instance-method split alive, because <c>base.ToString()</c> is legal only in instance
/// context and this method may be inlined as a static C# <c>operator &lt;</c>. This is the cell the
/// #1740 MergeFrom bug (8f291a489) blocked — the per-file→project <c>SemanticBinding</c> merge
/// dropped <c>_requiresInstanceImpl</c> silently, so this cell is exactly what keeps that fix
/// honest against a regression.</para>
///
/// <para><b>Axis D — same-dunder positive controls (s10, s16).</b> A super call from a dunder to
/// ITSELF (<c>super().__eq__(...)</c> inside <c>__eq__</c>, <c>super().__str__()</c> inside
/// <c>__str__</c>) always worked — these stay green as the "not every cell is a new fix" control.</para>
///
/// <para><b>Axis E — the context refusal (s15).</b> <c>super()</c> inside a REGULAR method (not
/// <c>__init__</c>, not <c>@override</c>, not a dunder) is refused by name (SPY0287) — unrelated to
/// #1740's lowering (a static context check, <c>ValidateSuperContextRules</c> Case 4), kept as the
/// falsifiability control that this matrix does not merely assert "everything runs".</para>
///
/// <para><b>Axis F — the DunderMapping-narrowing regression risk (3 cells).</b> The checker's
/// <c>SuperOperatorApplication</c> tag is deliberately a SUPERSET of the cast-lowerable family
/// (<c>OperatorRegistry.IsOperatorDunder</c>) — it also covers <c>__eq__</c>/<c>__matmul__</c>/
/// <c>__implicit__</c>/<c>__explicit__</c>, which are ordinary (sometimes virtual) instance methods
/// or conversions, not real C# operator overloads. Casting there would not bypass virtual dispatch,
/// turning a working <c>base.Equals(x)</c> into <c>((Base)this).Equals(x)</c> — still
/// virtual-dispatching back to the override that made the call, i.e. INFINITE RECURSION. The
/// emitter narrows cast-vs-<c>base.Method()</c> through <c>DunderMapping</c>'s OWN operator-syntax
/// registry (design note #1905), not the semantic fact alone: <c>__eq__</c> with a <c>Base</c>-typed
/// parameter (a real <c>operator==</c> overload) and with an <c>object</c>-typed parameter (a
/// virtual <c>Equals</c> override) both fall through to <c>base.Equals(other)</c>, never the cast —
/// each cell also asserts the emitted C# to prove the ROUTE, not merely the printed value, since a
/// vacuous route (chance-correct output through the wrong emission) is exactly what an infinite
/// recursion would NOT produce (it would hang/stack-overflow, not silently print the right answer) —
/// the C# assertion is what makes a future regression visible even before it manifests at runtime.
/// <c>__matmul__</c> needs <c>--enable-feature=matmul</c> (feature-flagged operator, #1650/#1740
/// unrelated).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class SuperDunderHostMatrixTests : IntegrationTestBase
{
    public SuperDunderHostMatrixTests(ITestOutputHelper output) : base(output) { }

    private sealed record Cell(
        string Name,
        string Source,
        string ExpectedOutput,
        string? MustContainInCSharp = null,
        string? MustMatchRegexInCSharp = null,
        FeatureFlags? Features = null);

    // ── Axis A: walker-missed hosts, operator-lowered super().__add__(other) ──────────────────
    private const string AddBasePrelude =
        "class Base:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n\n"
        + "    def __add__(self, other: Base) -> int:\n        return self.v + other.v\n\n";

    private static readonly Cell[] Cells =
    {
        new("s01_Return",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        return super().__add__(other)\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(2))\n",
            "3\n",
            // The cast route, locked in for the cell the plan names as the primary acceptance
            // example: `(global::...Base)left + right` — never a bare `base` operand (CS0175
            // pre-#1740).
            MustMatchRegexInCSharp: @"\(global::[\w.]+\.Base\)\s*left"),

        new("s02_ListElement",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        xs = [super().__add__(other)]\n        return xs[0]\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(2))\n",
            "3\n"),

        new("s03_ForBody",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        result = 0\n        for i in range(1):\n"
                + "            result = super().__add__(other)\n        return result\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(0))\n",
            "1\n"),

        new("s04_KeywordArgument",
            AddBasePrelude
                + "def identity(*, value: int) -> int:\n    return value\n\n"
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        return identity(value=super().__add__(other))\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(0))\n",
            "1\n"),

        new("s05_Comprehension",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        return sum([super().__add__(other) for _ in range(2)])\n\n"
                + "def main() -> None:\n    print(Derived(5) + Derived(5))\n",
            "20\n"),

        new("s06_IndexStore",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        xs = [0]\n        xs[0] = super().__add__(other)\n        return xs[0]\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(0))\n",
            "1\n"),

        new("s07_Ternary",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        return super().__add__(other) if True else 999\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(0))\n",
            "1\n"),

        new("s08_TryBody",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        try:\n            return super().__add__(other)\n"
                + "        except Exception:\n            return -1\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(0))\n",
            "1\n"),

        new("s17_Lambda",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        f = lambda: super().__add__(other)\n        return f()\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(0))\n",
            "1\n"),

        new("s18_IfBody",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        result = 0\n        if True:\n"
                + "            result = super().__add__(other)\n        return result\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(0))\n",
            "1\n"),

        new("s20_AssignThenReturn",
            AddBasePrelude
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        result = super().__add__(other)\n        return result\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(0))\n",
            "1\n"),

        new("s22_WithBody",
            AddBasePrelude
                + "class Ctx:\n    def __enter__(self) -> Self:\n        return self\n\n"
                + "    def __exit__(self) -> None:\n        pass\n\n"
                + "class Derived(Base):\n    def __add__(self, other: Base) -> int:\n"
                + "        with Ctx():\n            return super().__add__(other)\n\n"
                + "def main() -> None:\n    print(Derived(1) + Derived(0))\n",
            "1\n"),

        // ── Axis B: Base-typed receiver, comparison/unary/bitwise family ───────────────────────
        new("s11_LessThan_BaseTypedReceiver",
            "class Base:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n\n"
                + "    def __lt__(self, other: Base) -> bool:\n        return self.v < other.v\n\n"
                + "class Derived(Base):\n    def __lt__(self, other: Base) -> bool:\n"
                + "        return super().__lt__(other)\n\n"
                + "def main() -> None:\n    b1: Base = Derived(1)\n    b2: Base = Derived(2)\n"
                + "    print(b1 < b2)\n",
            "True\n"),

        new("s12_Neg_BaseTypedReceiver",
            "class Base:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n\n"
                + "    def __neg__(self) -> int:\n        return -self.v\n\n"
                + "class Derived(Base):\n    def __neg__(self) -> int:\n        return super().__neg__()\n\n"
                + "def main() -> None:\n    b: Base = Derived(-2)\n    print(-b)\n",
            "2\n"),

        new("s13_And_BaseTypedReceiver",
            "class Base:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n\n"
                + "    def __and__(self, other: Base) -> int:\n        return self.v & other.v\n\n"
                + "class Derived(Base):\n    def __and__(self, other: Base) -> int:\n"
                + "        return super().__and__(other)\n\n"
                + "def main() -> None:\n    b1: Base = Derived(5)\n    b2: Base = Derived(5)\n"
                + "    print(b1 & b2)\n",
            "5\n"),

        new("s14_Sub_BaseTypedReceiver",
            "class Base:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n\n"
                + "    def __sub__(self, other: Base) -> int:\n        return self.v - other.v\n\n"
                + "class Derived(Base):\n    def __sub__(self, other: Base) -> int:\n"
                + "        return super().__sub__(other)\n\n"
                + "def main() -> None:\n    b1: Base = Derived(10)\n    b2: Base = Derived(3)\n"
                + "    print(b1 - b2)\n",
            "7\n"),

        // ── Axis C: the split-honesty cell — method-lowered super() INSIDE an operator dunder ──
        new("s21_MethodLoweredInsideOperatorDunder",
            "class Base:\n    def __str__(self) -> str:\n        return \"B\"\n\n"
                + "class Derived(Base):\n    def __lt__(self, other: Derived) -> bool:\n"
                + "        return super().__str__() == \"B\"\n\n"
                + "def main() -> None:\n    print(Derived() < Derived())\n",
            "True\n",
            // The private-instance-method split, not a cast: `super().__str__()` is not
            // cast-lowerable (DunderMapping has no operator syntax for __str__), so
            // RequiresInstanceImpl must still be true here.
            MustContainInCSharp: "base.ToString()"),

        // ── Axis D: same-dunder positive controls (always worked; falsifiability, not #1740) ───
        new("s10_EqInOwnDunder",
            "class Base:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n\n"
                + "    def __eq__(self, other: Base) -> bool:\n        return self.v == other.v\n\n"
                + "class Derived(Base):\n    def __eq__(self, other: Base) -> bool:\n"
                + "        return super().__eq__(other)\n\n"
                + "def main() -> None:\n    print(Derived(1) == Derived(1))\n",
            "True\n"),

        new("s16_StrInOwnDunder",
            "class Base:\n    def __str__(self) -> str:\n        return \"Base\"\n\n"
                + "class Derived(Base):\n    def __str__(self) -> str:\n"
                + "        return \"Derived+\" + super().__str__()\n\n"
                + "def main() -> None:\n    print(Derived())\n",
            "Derived+Base\n"),
    };

    private static Cell C(string name) => Cells.Single(c => c.Name == name);

    public static IEnumerable<object[]> CellNames => Cells.Select(c => new object[] { c.Name });

    [Theory]
    [MemberData(nameof(CellNames))]
    public void Cell_RunsAndPrintsThePythonMatchingValue(string name)
    {
        var cell = C(name);

        var result = CompileAndExecute(cell.Source, features: cell.Features);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError,
            $"[{name}] a super-dunder lowering must never produce SPY0908/SPY0909 — that is the exact "
            + $"defect class #1740 fixes. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{cell.Source}");
        result.Success.Should().BeTrue(
            $"[{name}] must compile and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{cell.Source}");
        result.StandardOutput.Should().Be(cell.ExpectedOutput,
            $"[{name}] must print the python3-matching value\n{cell.Source}");

        if (cell.MustContainInCSharp != null)
        {
            result.GeneratedCSharp.Should().Contain(cell.MustContainInCSharp,
                $"[{name}] the emitted C# must show the lowering's shape\n{result.GeneratedCSharp}");
        }

        if (cell.MustMatchRegexInCSharp != null)
        {
            result.GeneratedCSharp.Should().MatchRegex(cell.MustMatchRegexInCSharp,
                $"[{name}] the emitted C# must show the lowering's shape\n{result.GeneratedCSharp}");
        }
    }

    // ── Axis E: the context refusal — super() in a REGULAR method (unrelated to #1740) ────────
    [Fact]
    public void S15_SuperInRegularMethod_IsRefusedByName()
    {
        var source =
            "class Base:\n    def helper(self) -> int:\n        return 1\n\n"
            + "class Derived(Base):\n    def other(self) -> int:\n        return super().helper()\n\n"
            + "def main() -> None:\n    print(Derived().other())\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[s15] must be refused\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.InvalidSuperUsage,
            $"[s15] must report SPY0287. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        result.RawDiagnostics.First(d => d.Code == DiagnosticCodes.Semantic.InvalidSuperUsage).Message
            .Should().Contain("regular methods", $"[s15] names the refused context\n{source}");
    }

    // ── Axis F: DunderMapping-narrowing regression risk — cast would recurse infinitely ────────

    /// <summary>
    /// <c>__eq__(self, other: Base)</c> — a CONCRETE parameter type, so Derived's own <c>__eq__</c>
    /// is emitted as a real <c>operator==</c> overload. Even so, <c>super().__eq__(other)</c> is NOT
    /// cast-lowered (DunderMapping's own registry has no operator syntax for <c>__eq__</c>) — it
    /// falls through to <c>base.Equals(other)</c>, virtual-dispatch-bypassing and non-recursive.
    /// </summary>
    [Fact]
    public void SuperEq_BaseTypedParameter_FallsThroughToBaseEquals_NoRecursion()
    {
        var source =
            "class Base:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n\n"
            + "    def __eq__(self, other: Base) -> bool:\n        print(\"Base.__eq__\")\n"
            + "        return self.v == other.v\n\n"
            + "class Derived(Base):\n    def __eq__(self, other: Base) -> bool:\n"
            + "        print(\"Derived.__eq__\")\n        return super().__eq__(other)\n\n"
            + "def main() -> None:\n    print(Derived(1) == Derived(1))\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("Derived.__eq__\nBase.__eq__\nTrue\n",
            "exactly one Base.__eq__ print proves no infinite recursion — an accidental cast "
            + $"would virtual-redispatch back into Derived.__eq__ forever\n{source}");
        result.GeneratedCSharp.Should().Contain("base.Equals(other)",
            $"the route itself, not just the output, must be base.Method() — never a cast, which "
            + $"would not bypass virtual dispatch here\n{result.GeneratedCSharp}");
        result.GeneratedCSharp.Should().NotMatchRegex(@"\(global::[\w.]+\.Base\)\s*(this|left|right)",
            $"must never take the cast route for __eq__\n{result.GeneratedCSharp}");
    }

    /// <summary>
    /// <c>__eq__(self, other: object)</c> — the object-shaped override (<c>IsEqualsObjectOverload</c>):
    /// Derived's own <c>__eq__</c> is emitted as a VIRTUAL <c>Equals(object)</c> override, so a cast
    /// here would be catastrophic — <c>((Base)this).Equals(x)</c> still virtual-dispatches to
    /// Derived's own override (a cast changes the STATIC type, never the RUNTIME type a virtual
    /// call dispatches on), i.e. infinite recursion / stack overflow, not merely a wrong answer.
    /// </summary>
    [Fact]
    public void SuperEq_ObjectTypedParameter_FallsThroughToBaseEquals_NoRecursion()
    {
        var source =
            "class Base:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n\n"
            + "    def __eq__(self, other: object) -> bool:\n        print(\"Base.__eq__\")\n"
            + "        if isinstance(other, Base):\n            return self.v == (other as! Base).v\n"
            + "        return False\n\n    def __hash__(self) -> int:\n        return self.v\n\n"
            + "class Derived(Base):\n    def __eq__(self, other: object) -> bool:\n"
            + "        print(\"Derived.__eq__\")\n        return super().__eq__(other)\n\n"
            + "def main() -> None:\n    print(Derived(1) == Derived(1))\n";

        var result = CompileAndExecute(source, executionTimeoutMs: 15000);

        result.Success.Should().BeTrue(
            $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("Derived.__eq__\nBase.__eq__\nTrue\n",
            "exactly one Base.__eq__ print (not a hang/stack-overflow) proves no infinite "
            + $"recursion\n{source}");
        result.GeneratedCSharp.Should().Contain("base.Equals(other)",
            $"the route must be base.Method(), never a cast\n{result.GeneratedCSharp}");
        result.GeneratedCSharp.Should().NotMatchRegex(@"\(global::[\w.]+\.Base\)\s*(this|left|right)",
            $"must never take the cast route for the object-shaped __eq__ override\n{result.GeneratedCSharp}");
    }

    /// <summary>
    /// <c>__matmul__</c> is never a real C# operator (no <c>operator @</c>) — DunderMapping has no
    /// binary-expression-kind entry for it, so <c>super().__matmul__(other)</c> always falls
    /// through to <c>base.MatMul(other)</c>. Feature-gated (<c>matmul</c>, #1650) — unrelated to
    /// #1740's routing decision, only to whether <c>@</c>/<c>__matmul__</c> is accepted at all.
    /// </summary>
    [Fact]
    public void SuperMatMul_FallsThroughToBaseMatMul_UnderTheMatmulFeature()
    {
        var source =
            "class Base:\n    x: int\n\n    def __init__(self, x: int) -> None:\n        self.x = x\n\n"
            + "    def __matmul__(self, other: Base) -> int:\n        print(\"Base.__matmul__\")\n"
            + "        return self.x * other.x\n\n"
            + "class Derived(Base):\n    def __matmul__(self, other: Base) -> int:\n"
            + "        print(\"Derived.__matmul__\")\n        return super().__matmul__(other)\n\n"
            + "def main() -> None:\n    print(Derived(6) @ Derived(7))\n";

        var features = FeatureFlags.None.Enable("matmul");
        var result = CompileAndExecute(source, features: features);

        result.Success.Should().BeTrue(
            $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("Derived.__matmul__\nBase.__matmul__\n42\n");
        result.GeneratedCSharp.Should().Contain("base.MatMul(other)",
            $"the route must be base.Method(), never a cast (no C# operator @ exists)\n{result.GeneratedCSharp}");

        // Ungated twin: the same program without the feature is refused by the gate, not by
        // this seam — proves the feature flag, not the lowering, is what toggles this cell.
        CompileAndExecute(source).RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled);
    }

    // ── Totality ─────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Matrix_IsTotalOverItsCells()
    {
        Cells.Length.Should().Be(19, "12 walker-missed hosts (Axis A) + 4 Base-typed-receiver "
            + "operator families (Axis B) + 1 split-honesty cell (Axis C) + 2 same-dunder positive "
            + "controls (Axis D)");
        Cells.Select(c => c.Name).Should().OnlyHaveUniqueItems();
    }
}
