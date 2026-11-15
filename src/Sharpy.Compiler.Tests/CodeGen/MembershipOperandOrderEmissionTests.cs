using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The emitter half of P93 (plan-67a01f T6, D1–D3; #2645, #2648, #2653), run and compared with CPython.
/// Every expected output below is <c>python3.15</c>'s (CPython 3.15.2) for the same program, the trace
/// functions printing their label when called, so an operand evaluated out of order prints a different
/// trace.
/// <list type="bullet">
/// <item>D1 (R-NZ): a Sharpy class with <c>__iter__</c> and no <c>__contains__</c> is tested by the fully
/// named LINQ <c>Enumerable.Contains&lt;E&gt;</c>, never by a member the class calls <c>contains</c>.</item>
/// <item>D2: a membership needle the checker recorded a binding for is evaluated before its container —
/// bound through a cast to its binding type, hoisted ahead of a hoisting container or a tuple's array
/// bridge, a bare name included; <c>n * s</c> takes the count first.</item>
/// <item>D3: an <c>in</c>, <c>not in</c>, <c>is</c> or <c>is not</c> chain link lowers through its binary
/// twin's emitter.</item>
/// </list>
/// </summary>
public class MembershipOperandOrderEmissionTests : IntegrationTestBase
{
    public MembershipOperandOrderEmissionTests(ITestOutputHelper output) : base(output) { }

    private const string Trace =
        "def tr[V](label: str, v: V) -> V:\n    print(label)\n    return v\n\n"
        + "def keep[V, W](v: V, xs: W) -> W:\n    return xs\n\n";

    private const string IterBags = """
        class IterBag:
            items: list[int]

            def __init__(self, a: int, b: int) -> None:
                self.items = [a, b]

            def __iter__(self) -> int:
                for v in self.items:
                    yield v

        class CBag:
            items: list[int]

            def __init__(self, a: int, b: int) -> None:
                self.items = [a, b]

            def __iter__(self) -> int:
                for v in self.items:
                    yield v

            def contains(self, v: int) -> bool:
                return False


        """;

    private void AssertRuns(string source, params string[] expectedLines)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"{string.Join(" | ", result.CompilationErrors)}\n{result.StandardError}\n{source}\n{result.GeneratedCSharp}");
        result.StandardOutput.Should().Be(string.Join("\n", expectedLines) + "\n", $"\n{source}\n{result.GeneratedCSharp}");
    }

    /// <summary>D1, M1's emitter half: <c>CBag.contains</c> answers False; CPython tests by iteration.</summary>
    [Fact]
    public void IterationContainer_IsTestedByLinq_NotByAUserContains()
    {
        var source = IterBags + "def main() -> None:\n"
            + "    print(1 in IterBag(1, 2))\n    print(3 in IterBag(1, 2))\n"
            + "    print(1 in CBag(1, 2))\n    print(1 not in CBag(1, 2))\n";
        AssertRuns(source, "True", "False", "True", "False");

        var result = CompileAndExecute(source);
        result.GeneratedCSharp.Should().Contain("global::System.Linq.Enumerable.Contains<int>(");
    }

    /// <summary>D1 with a bound needle: the needle still goes first, into LINQ's second argument.</summary>
    [Fact]
    public void IterationContainer_NeedleIsEvaluatedFirst()
    {
        AssertRuns(IterBags + Trace + "def main() -> None:\n"
            + "    print(tr(\"needle\", 1) in tr(\"container\", CBag(1, 2)))\n",
            "needle", "container", "True");
    }

    /// <summary>D2, the bind (M2): the plain form reads its container first.</summary>
    [Fact]
    public void Needle_IsEvaluatedBeforeContainer()
    {
        AssertRuns(Trace + "def main() -> None:\n    xs: list[int] = [1, 2]\n"
            + "    print(tr(\"needle\", 1) in tr(\"container\", xs))\n"
            + "    print(tr(\"needle\", 3) not in tr(\"container\", xs))\n"
            + "    v = tr(\"needle\", 2) in tr(\"container\", xs)\n    print(v)\n",
            "needle", "container", "True", "needle", "container", "True", "needle", "container", "True");
    }

    /// <summary>D2, the cast (M2): a lambda and a conditional with a <c>None</c> arm have no C# type of their
    /// own; the bind gives them their slot's, beside a container that evaluates and one that hoists.</summary>
    [Fact]
    public void BoundNeedle_KeepsTheTypeOfItsSlot()
    {
        AssertRuns(Trace + "def main() -> None:\n    xs: list[int] = [1, 2]\n"
            + "    fs: list[(int) -> int] = []\n    ys: list[int | None] = [1, None]\n    flag: bool = len(xs) > 0\n"
            + "    print((lambda w: w) in tr(\"container\", fs))\n"
            + "    print((tr(\"needle\", 1) if flag else None) in tr(\"container\", ys))\n"
            + "    print((tr(\"needle\", 1) if flag else None) in [q for q in tr(\"container\", ys)])\n",
            "container", "False", "needle", "container", "True", "needle", "container", "True");
    }

    /// <summary>D2, the hoist ahead of a tuple (M2): the array bridge captures a non-name tuple into a
    /// statement, which must not run ahead of the needle.</summary>
    [Fact]
    public void Needle_IsHoistedAheadOfATuplesArrayBridge()
    {
        AssertRuns(Trace + "def main() -> None:\n"
            + "    print(tr(\"needle\", 2) in (tr(\"c1\", 1), 2))\n"
            + "    print(tr(\"needle\", 3) not in (tr(\"c1\", 1), 2))\n",
            "needle", "c1", "True", "needle", "c1", "True");
    }

    /// <summary>D2, the bare-name arm (M2): a container that rebinds the name must not be read first.</summary>
    [Fact]
    public void BareNameNeedle_IsReadBeforeTheContainerRebindsIt()
    {
        AssertRuns(Trace + "def main() -> None:\n    xs: list[int] = [5, 2]\n    x: int = 1\n"
            + "    print(x in [(x := 5), 2])\n"
            + "    y: int = 1\n    print(y in keep((y := 5), xs))\n"
            + "    z: int = 1\n    print(z in [*keep((z := 5), xs)])\n",
            "False", "False", "False");
    }

    /// <summary>
    /// The bare-name arm for every binary operator and chain link (lead ruling on T6's sibling cell): a
    /// name read on the left is captured before a right operand's hoist (a spread) whose walrus rebinds
    /// it — a binary operator, a chain's head and middle operand, and a call argument. Read from the
    /// emitted syntax alone (<c>HoistRebindsARead</c>).
    /// </summary>
    [Fact]
    public void BareNameLeftOperand_IsReadBeforeARightOperandsHoistRebindsIt()
    {
        AssertRuns(Trace + "def two(a: int, b: int) -> int:\n    return a * 10 + b\n\n"
            + "def main() -> None:\n"
            + "    x: int = 1\n    print(x == len([*keep((x := 5), [5])]))\n"
            + "    x = 1\n    print(x + len([*keep((x := 5), [5])]))\n"
            + "    x = 1\n    print(x < len([*keep((x := 5), [5, 6])]))\n"
            + "    x = 1\n    print(x != len([*keep((x := 5), [5])]))\n"
            + "    y: int = 1\n    print(0 < y == len([*keep((y := 5), [5, 6, 7, 8, 9])]))\n"
            + "    y = 1\n    print(0 < y < len([*keep((y := 5), [5, 6])]))\n"
            + "    x = 1\n    print(x == len([*keep((x := 5), [5])]) == 1)\n"
            + "    x = 1\n    print(two(x, len([*keep((x := 5), [5])])))\n",
            "True", "2", "True", "False", "False", "True", "True", "11");
    }

    /// <summary>D2, the count-first overload (M2): <c>n * s</c> evaluates <c>n</c> first.</summary>
    [Fact]
    public void StringRepeat_WithTheStringOnTheRight_EvaluatesTheCountFirst()
    {
        AssertRuns(Trace + "def main() -> None:\n"
            + "    print(tr(\"count\", 2) * tr(\"str\", \"ab\"))\n"
            + "    print(tr(\"str\", \"ab\") * tr(\"count\", 2))\n",
            "count", "str", "abab", "str", "count", "abab");
    }

    /// <summary>D3 (M3's emitter half): each link is its binary twin, an identity link included.</summary>
    [Fact]
    public void ChainLinks_AreTheirBinaryTwins()
    {
        AssertRuns("""
            enum Color:
                RED = 1
                GREEN = 2

            class Obj:
                pass

            class AlwaysEqual:
                def __eq__(self, other: AlwaysEqual) -> bool:
                    return True

            def main() -> None:
                a: int = 1
                c: int = 3
                x: int = 1
                print(x == x in [a, c])
                print(x != x not in [a, c])
                o1 = Obj()
                d = o1
                e = o1
                print(o1 is d is e)
                print(o1 is d is not Obj())
                y: int | None = 4
                print(a < y is not None)
                print(None is not y is not None)
                k = Color.RED
                m = Color.RED
                print(k is Color.RED is m)
                print(k is Color.RED == m)
                p = AlwaysEqual()
                q = AlwaysEqual()
                print(p == q == p)
                print(p is q is p)
                print(p is p is not q)

            """,
            "True", "False", "True", "True", "True", "True", "True", "True", "True", "False", "True");
    }

    /// <summary>D2 and D3 together: a chain's membership link takes the needle-first bind, a middle
    /// operand captured through its binding, the tuple hoist and the bare-name hoist.</summary>
    [Fact]
    public void ChainMembershipLinks_EvaluateLeftToRight()
    {
        AssertRuns(Trace + "def main() -> None:\n    xs: list[int] = [1, 2]\n"
            + "    print(tr(\"h\", 1) == tr(\"needle\", 1) in tr(\"container\", xs))\n"
            + "    print(tr(\"h\", 2) == tr(\"needle\", 2) in (tr(\"c1\", 1), 2))\n"
            + "    print(tr(\"h\", 1) < tr(\"m\", 2) not in tr(\"container\", xs))\n"
            + "    print(tr(\"needle\", 1) in tr(\"container\", xs) == tr(\"r\", xs))\n"
            + "    x: int = 1\n    print(0 < x in [(x := 5), 2])\n"
            + "    w: int = 1\n    print(0 < w in [*keep((w := 5), [5, 2])])\n",
            "h", "needle", "container", "True",
            "h", "needle", "c1", "True",
            "h", "m", "container", "False",
            "needle", "container", "r", "True",
            "False", "False");
    }

    /// <summary>D3: a middle operand read by a comparison link and then bound as the next link's needle is
    /// compared at its own type (the twin <c>a == mk("a")</c>), the needle cast applied only at the
    /// membership read: a <c>str</c> cast to <c>object</c> would compare by reference, a <c>long</c> cast to
    /// <c>float</c> would round 2**53 + 1.</summary>
    [Fact]
    public void ChainMiddleOperand_IsComparedAtItsOwnType_NotItsNeedleBinding()
    {
        AssertRuns("def mk(s: str) -> str:\n    return s + \"b\"\n\n"
            + "def big() -> long:\n    return 9007199254740993\n\n"
            + "def main() -> None:\n    a: str = \"ab\"\n    objs: list[object] = [a]\n"
            + "    print(a == mk(\"a\") in objs)\n"
            + "    print(a != mk(\"a\") in objs)\n"
            + "    print(a == mk(\"a\") == mk(\"a\") in objs)\n"
            + "    n: long = 9007199254740992\n    fs: list[float] = [9007199254740992.0]\n"
            + "    print(n == big() in fs)\n",
            "True", "False", "True", "False");
    }

    /// <summary>D3 ruling: a middle needle the checker typed only by its slot (a conditional with a
    /// <c>None</c> arm) is captured at the slot's type, its nullable lift — a cast that cannot change the
    /// link reading it first — so the capture compiles; the comparison links still read their own types.</summary>
    [Fact]
    public void ChainMiddleNeedle_TypedOnlyByItsSlot_IsCapturedAtItsLift()
    {
        AssertRuns(Trace + "def main() -> None:\n    flag: bool = True\n    a: int | None = 1\n"
            + "    ys: list[int | None] = [1, None]\n"
            + "    print(a == (tr(\"m\", 1) if flag else None) in ys)\n"
            + "    print(a != (tr(\"m\", 2) if flag else None) not in ys)\n",
            "m", "True", "m", "True");
    }

    /// <summary>D2: a needle over literals whose operator can raise (<c>//</c>, <c>%</c>, <c>/</c>) is an
    /// evaluation, so it raises before its container is read.</summary>
    [Fact]
    public void RaisingLiteralNeedle_IsEvaluatedBeforeContainer()
    {
        AssertRuns(Trace + "def main() -> None:\n    xs: list[int] = [1, 2]\n    fs: list[float] = [1.0]\n"
            + "    try:\n        print((1 // 0) in tr(\"container\", xs))\n    except ZeroDivisionError:\n        print(\"zde\")\n"
            + "    try:\n        print((1 % 0) not in tr(\"container\", xs))\n    except ZeroDivisionError:\n        print(\"zde\")\n"
            + "    try:\n        print((1 / 0) in tr(\"container\", fs))\n    except ZeroDivisionError:\n        print(\"zde\")\n",
            "zde", "zde", "zde");
    }
}
