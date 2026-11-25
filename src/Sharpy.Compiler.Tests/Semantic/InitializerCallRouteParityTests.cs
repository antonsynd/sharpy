using System.Collections.Concurrent;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The differential for INITIALIZER CALLS over an overloaded <c>__init__</c>: <b>an initializer
/// call binds against the target's whole overload set, through one selector, on every route and in
/// every declaration order</b>. The routes are a direct construction <c>C(args)</c>, a construction
/// with written type arguments <c>C[int](args)</c>, the chaining call <c>self.__init__(args)</c> and
/// the chaining call <c>super().__init__(args)</c>; each cell gives all four the SAME candidate set —
/// <c>Z</c> (no parameters) plus a shape's pair <c>A</c>/<c>B</c> — declared with the chain's target
/// either below the constructor that calls it (<see cref="Order.TargetLater"/>) or above it
/// (<see cref="Order.TargetFirst"/>).
///
/// <para>Axes: route (4) × order (2) × shape (8) = 64 cells, none excluded. A shape either states
/// its verdict outright (an output, or a refusal code) or, for an argument whose type the call's
/// context decides, states only that every route answers as the construction route does: the
/// construction route's answer for those is itself an open gap (an empty list or <c>None()</c>
/// into an overloaded constructor is SPY0227), and a differential must not pin it.</para>
///
/// <para>No cell may leak SPY0908: before the fix (aa21bedb) the self route was checked against the
/// first-declared <c>__init__</c> alone, and the super, written-generic and plain construction
/// routes left a wrong count or type to Roslyn.</para>
/// </summary>
public class InitializerCallRouteParityTests : IntegrationTestBase
{
    private const int RouteCount = 4;
    private const int OrderCount = 2;
    private const int ShapeCount = 8;

    /// <summary>The construction route's verdict per (order, shape), which the context shapes compare against.</summary>
    private static readonly ConcurrentDictionary<(Order, Shape), string> ConstructionVerdicts = new();

    public InitializerCallRouteParityTests(ITestOutputHelper output) : base(output) { }

    public enum Route
    {
        /// <summary><c>C(args)</c> in <c>main</c>.</summary>
        Construction,

        /// <summary><c>C[int](args)</c> in <c>main</c>, on <c>class C[T]</c>: the written-binding construction route.</summary>
        WrittenGenericConstruction,

        /// <summary><c>self.__init__(args)</c> as the body of <c>Z</c>; <c>main</c> runs <c>C()</c>.</summary>
        SelfInit,

        /// <summary><c>super().__init__(args)</c> in a derived class's constructor, over <c>Base</c>'s set.</summary>
        SuperInit,
    }

    public enum Order
    {
        /// <summary><c>Z</c> first, then <c>A</c> and <c>B</c>: the target is declared below the chain.</summary>
        TargetLater,

        /// <summary><c>A</c> and <c>B</c> first, then <c>Z</c>: the target is declared above the chain.</summary>
        TargetFirst,
    }

    public enum Shape
    {
        /// <summary>Only <c>A</c> accepts two arguments.</summary>
        ArityPick,

        /// <summary><c>A</c> and <c>B</c> take one argument each; the argument's type picks <c>B</c>.</summary>
        TypePick,

        /// <summary>Keyword arguments, written out of order, into <c>A</c>.</summary>
        Keywords,

        /// <summary>The one one-argument overload rejects the argument's type.</summary>
        WrongType,

        /// <summary>No overload accepts three arguments.</summary>
        NoCountFits,

        /// <summary>Two overloads accept the call equally well.</summary>
        Ambiguous,

        /// <summary>An empty list literal: its type comes from the call's context.</summary>
        ContextEmptyList,

        /// <summary><c>None()</c>: its type comes from the call's context.</summary>
        ContextNoneCall,
    }

    /// <summary>The self-free parameter lists of <c>A</c> and <c>B</c>, and the call's arguments.</summary>
    private static (string A, string B, string Args) ShapeOf(Shape shape) => shape switch
    {
        Shape.ArityPick => ("n: int, name: str", "s: str", "1, \"a\""),
        Shape.TypePick => ("s: str", "n: int", "7"),
        Shape.Keywords => ("n: int, name: str", "s: str", "name=\"k\", n=4"),
        Shape.WrongType => ("n: int", "a: int, b: int", "\"s\""),
        Shape.NoCountFits => ("n: int", "a: int, b: int", "1, 2, 3"),
        Shape.Ambiguous => ("n: int, m: int = 0", "n: int, s: str = \"q\"", "1"),
        Shape.ContextEmptyList => ("xs: list[str]", "xs: list[int], m: int", "[], 1"),
        Shape.ContextNoneCall => ("s: str", "v: int?, m: int", "None(), 2"),
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    /// <summary>
    /// The verdict a shape states outright — <c>out:</c> the program's output, or <c>code:</c> its
    /// first error — or null for a context shape, whose verdict is "as the construction route".
    /// </summary>
    private static string? StatedVerdict(Shape shape) => shape switch
    {
        Shape.ArityPick => "out:A",
        Shape.TypePick => "out:B",
        Shape.Keywords => "out:A",
        Shape.WrongType => "code:" + DiagnosticCodes.Semantic.TypeMismatch,
        Shape.NoCountFits => "code:" + DiagnosticCodes.Semantic.NoMatchingOverload,
        Shape.Ambiguous => "code:" + DiagnosticCodes.Semantic.AmbiguousOverload,
        Shape.ContextEmptyList or Shape.ContextNoneCall => null,
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    /// <summary>
    /// The program for one cell. <c>A</c> prints <c>A</c> and <c>B</c> prints <c>B</c>, so a run
    /// says which overload the call bound to; <c>Z</c> is the chaining constructor on the self
    /// route and an ordinary one (printing <c>Z</c>) on the other two, so the candidate set is the
    /// same on every route.
    /// </summary>
    private static string Program(Route route, Order order, Shape shape)
    {
        var (a, b, args) = ShapeOf(shape);
        var zBody = route == Route.SelfInit ? $"self.__init__({args})" : "print(\"Z\")";
        var z = $"    def __init__(self):\n        {zBody}\n\n";
        var ab = $"    def __init__(self, {a}):\n        print(\"A\")\n\n"
            + $"    def __init__(self, {b}):\n        print(\"B\")\n\n";
        var cls = route switch
        {
            Route.SuperInit => "Base",
            Route.WrittenGenericConstruction => "C[T]",
            _ => "C",
        };
        var body = order == Order.TargetLater ? z + ab : ab + z;
        var source = $"class {cls}:\n{body}";

        return route switch
        {
            Route.Construction => source + $"def main() -> None:\n    C({args})\n",
            Route.WrittenGenericConstruction => source + $"def main() -> None:\n    C[int]({args})\n",
            Route.SelfInit => source + "def main() -> None:\n    C()\n",
            Route.SuperInit => source
                + $"class D(Base):\n    def __init__(self):\n        super().__init__({args})\n\n"
                + "def main() -> None:\n    D()\n",
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };
    }

    private (string Verdict, string Seen) VerdictOf(Route route, Order order, Shape shape)
    {
        var source = Program(route, order, shape);
        var result = CompileAndExecute(source);
        var seen = result.FailureSummary() + "\n--- source ---\n" + source;

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"an initializer call is decided by the checker, never leaked to Roslyn: {route}/{order}/{shape}: {seen}");

        if (result.Success)
            return ("out:" + result.StandardOutput.Trim(), seen);

        var firstError = result.RawDiagnostics.FirstOrDefault(d => d.IsError);
        return ("code:" + (firstError?.Code ?? "<none>"), seen);
    }

    public static TheoryData<Route, Order, Shape> Cells()
    {
        var data = new TheoryData<Route, Order, Shape>();
        foreach (Route r in Enum.GetValues<Route>())
        {
            foreach (Order o in Enum.GetValues<Order>())
            {
                foreach (Shape s in Enum.GetValues<Shape>())
                    data.Add(r, o, s);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void InitializerCall_BindsOverTheWholeSet_OnEveryRouteAndOrder(Route route, Order order, Shape shape)
    {
        var (verdict, seen) = VerdictOf(route, order, shape);

        // Stated shapes: the verdict is the shape's own. Context shapes: whatever the construction
        // route answers in the SAME order — one rule for every route, without pinning that answer.
        // The construction cell of a context shape is the reference itself; it asserts only the
        // SPY0908 rule above.
        var expected = StatedVerdict(shape);
        if (expected == null)
        {
            if (route == Route.Construction)
            {
                ConstructionVerdicts[(order, shape)] = verdict;
                return;
            }

            expected = ConstructionVerdicts.GetOrAdd((order, shape),
                key => VerdictOf(Route.Construction, key.Item1, key.Item2).Verdict);
        }

        verdict.Should().Be(expected, $"{route}/{order}/{shape}: {seen}");
    }

    // ── the axes are anchored ────────────────────────────────────────────────────────────

    /// <summary>
    /// The cell count is anchored to LITERALS: a count derived from the same enums the generator
    /// walks would agree with any generator, including one that dropped an axis.
    /// </summary>
    [Fact]
    public void Axes_AreAnchored()
    {
        Enum.GetValues<Route>().Length.Should().Be(RouteCount);
        Enum.GetValues<Order>().Length.Should().Be(OrderCount);
        Enum.GetValues<Shape>().Length.Should().Be(ShapeCount);
        Cells().Count.Should().Be(64, "4 routes x 2 orders x 8 shapes, as the class remark states");
    }
}
