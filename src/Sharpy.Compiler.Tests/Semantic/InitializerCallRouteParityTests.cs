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
/// every declaration order</b>. The routes are a direct construction <c>C(args)</c>, a qualified one
/// <c>Outer.C(args)</c>, a construction with written type arguments <c>C[int](args)</c>, the chaining
/// call <c>self.__init__(args)</c> and the chaining call <c>super().__init__(args)</c>; each cell
/// gives all five the SAME candidate set —
/// <c>Z</c> (no parameters) plus a shape's pair <c>A</c>/<c>B</c> — declared with the chain's target
/// either below the constructor that calls it (<see cref="Order.TargetLater"/>) or above it
/// (<see cref="Order.TargetFirst"/>).
///
/// <para>Axes: route (5) × order (2) × shape (12) = 120 cells, none excluded. A shape either states
/// its verdict outright (an output, or a refusal code) or, for an argument whose type the call's
/// context decides AMONG several overloads accepting the count, states only that every route
/// answers as the construction route does: a candidate set gives such an argument no type
/// (<c>overload_resolution.md</c>, #1671), and a differential must not pin which refusal follows.
/// When the count leaves ONE overload, that overload is a resolved target and types the argument
/// (an empty list, <c>None()</c>) on every route, so those shapes state their verdict.</para>
///
/// <para>No cell may leak SPY0908: before the fix (aa21bedb) the self route was checked against the
/// first-declared <c>__init__</c> alone, and the super, written-generic and plain construction
/// routes left a wrong count or type to Roslyn.</para>
/// </summary>
public class InitializerCallRouteParityTests : IntegrationTestBase
{
    private const int RouteCount = 5;
    private const int OrderCount = 2;
    private const int ShapeCount = 12;

    /// <summary>The construction route's verdict per (order, shape), which the context shapes compare against.</summary>
    private static readonly ConcurrentDictionary<(Order, Shape), string> ConstructionVerdicts = new();

    public InitializerCallRouteParityTests(ITestOutputHelper output) : base(output) { }

    public enum Route
    {
        /// <summary><c>C(args)</c> in <c>main</c>.</summary>
        Construction,

        /// <summary><c>Outer.C(args)</c> in <c>main</c>, on a class nested in <c>Outer</c>.</summary>
        QualifiedConstruction,

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

        /// <summary>An empty list literal, typed by the one overload that accepts the count.</summary>
        ContextEmptyList,

        /// <summary><c>None()</c>, typed by the one overload that accepts the count.</summary>
        ContextNoneCall,

        /// <summary>An empty list literal where two overloads accept the count: the set types nothing.</summary>
        ContextEmptyListAmongPeers,

        /// <summary><c>None()</c> where two overloads accept the count: the set types nothing.</summary>
        ContextNoneCallAmongPeers,

        /// <summary>The one overload accepting three arguments is variadic, and it takes them.</summary>
        VariadicPick,

        /// <summary>The one overload accepting the count is variadic, and rejects the argument's type.</summary>
        VariadicWrongType,
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
        Shape.ContextEmptyListAmongPeers => ("xs: list[str]", "xs: list[int]", "[]"),
        Shape.ContextNoneCallAmongPeers => ("v: str?", "v: int?", "None()"),
        Shape.VariadicPick => ("*xs: int", "a: str, b: str", "1, 2, 3"),
        Shape.VariadicWrongType => ("*xs: int", "a: str, b: str", "\"x\""),
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    /// <summary>
    /// The verdict a shape states outright — <c>out:</c> the program's output, or <c>code:</c> its
    /// first error — or null for a context shape among peers, whose verdict is "as the construction
    /// route".
    /// </summary>
    private static string? StatedVerdict(Shape shape) => shape switch
    {
        Shape.ArityPick => "out:A",
        Shape.TypePick => "out:B",
        Shape.Keywords => "out:A",
        Shape.WrongType => "code:" + DiagnosticCodes.Semantic.TypeMismatch,
        Shape.NoCountFits => "code:" + DiagnosticCodes.Semantic.NoMatchingOverload,
        Shape.Ambiguous => "code:" + DiagnosticCodes.Semantic.AmbiguousOverload,
        Shape.VariadicPick => "out:A",
        Shape.VariadicWrongType => "code:" + DiagnosticCodes.Semantic.TypeMismatch,
        Shape.ContextEmptyList => "out:B",
        Shape.ContextNoneCall => "out:B",
        Shape.ContextEmptyListAmongPeers or Shape.ContextNoneCallAmongPeers => null,
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
            Route.QualifiedConstruction => "class Outer:\n"
                + string.Concat(source.Split('\n').Select(line => line.Length == 0 ? "\n" : "    " + line + "\n"))
                + $"def main() -> None:\n    Outer.C({args})\n",
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
        Cells().Count.Should().Be(120, "5 routes x 2 orders x 12 shapes, as the class remark states");
        Enum.GetValues<GenericRoute>().Length.Should().Be(5);
        Enum.GetValues<Slot>().Length.Should().Be(2);
        Enum.GetValues<Constraint>().Length.Should().Be(2);
        GenericBindingCells().Count.Should().Be(80,
            "5 generic routes x 2 orders x 2 slots x 2 constraints x 2 arguments");
        RigidSelectionCells().Count.Should().Be(4, "2 routes x 2 orders");
        UnwrittenGenericSetCells().Count.Should().Be(8, "2 spellings x 2 orders x 2 context arguments");
    }

    // ── the type-parameter binding axis ──────────────────────────────────────────────────
    //
    // The cells above never mention a type parameter, so they cannot see how a route BINDS one.
    // Here the target A is `__init__(self, v: <slot>, n: int)` with the slot `T` or `list[T]`:
    // inside the class (the self route) and through a forwarding base clause `D[T](Base[T])`, T is
    // the class's own and rigid; through `Base[str]` (direct or through a non-generic middle class)
    // and a written construction it is `str`. Either way an `int` into `v` is SPY0220 and a right
    // argument runs A. A constraint on T (`T: class`) is part of T's identity, so `list[T]` must
    // still meet `list[T]` with it.

    public enum GenericRoute
    {
        /// <summary><c>self.__init__(w, 1)</c> inside <c>class C[T]</c>, <c>w: T</c>.</summary>
        SelfInit,

        /// <summary><c>super().__init__(.., 1)</c> in <c>class D(Base[str])</c>.</summary>
        SuperDirect,

        /// <summary><c>super().__init__(.., 1)</c> in <c>class D(Mid)</c>, <c>class Mid(Base[str])</c>.</summary>
        SuperViaMiddle,

        /// <summary><c>super().__init__(w, 1)</c> in <c>class D[T](Base[T])</c>, <c>w: T</c>.</summary>
        SuperForwardsOwnParameter,

        /// <summary><c>C[str](.., 1)</c>.</summary>
        WrittenGenericConstruction,
    }

    public enum Slot
    {
        /// <summary><c>v: T</c>.</summary>
        Bare,

        /// <summary><c>v: list[T]</c>: a type parameter compared inside a constructed type.</summary>
        ListOf,
    }

    public enum Constraint
    {
        /// <summary><c>[T]</c>.</summary>
        None,

        /// <summary><c>[T: class]</c> on every declaration of T.</summary>
        Class,
    }

    private static string GenericProgram(GenericRoute route, Order order, Slot slot, Constraint constraint, bool rightArgument)
    {
        var ownParameter = route is GenericRoute.SelfInit or GenericRoute.SuperForwardsOwnParameter;
        var right = ownParameter ? "w" : "\"q\"";
        var arg = !rightArgument ? "42" : slot == Slot.ListOf ? $"[{right}]" : right;
        var slotType = slot == Slot.ListOf ? "list[T]" : "T";
        var t = constraint == Constraint.Class ? "T: class" : "T";

        var z = route == GenericRoute.SelfInit
            ? $"    def __init__(self, w: T, tag: str, extra: str):\n        self.__init__({arg}, 1)\n\n"
            : "    def __init__(self):\n        print(\"Z\")\n\n";
        var ab = $"    def __init__(self, v: {slotType}, n: int):\n        print(\"A\")\n\n"
            + "    def __init__(self, s: str):\n        print(\"B\")\n\n";
        var body = order == Order.TargetLater ? z + ab : ab + z;
        var cls = route is GenericRoute.SelfInit or GenericRoute.WrittenGenericConstruction ? "C" : "Base";
        var source = $"class {cls}[{t}]:\n{body}";

        return route switch
        {
            GenericRoute.SelfInit => source + "def main() -> None:\n    C[str](\"q\", \"t\", \"e\")\n",
            GenericRoute.SuperDirect => source
                + $"class D(Base[str]):\n    def __init__(self):\n        super().__init__({arg}, 1)\n\n"
                + "def main() -> None:\n    D()\n",
            GenericRoute.SuperViaMiddle => source
                + "class Mid(Base[str]):\n    pass\n\n"
                + $"class D(Mid):\n    def __init__(self):\n        super().__init__({arg}, 1)\n\n"
                + "def main() -> None:\n    D()\n",
            GenericRoute.SuperForwardsOwnParameter => source
                + $"class D[{t}](Base[T]):\n    def __init__(self, w: T):\n        super().__init__({arg}, 1)\n\n"
                + "def main() -> None:\n    D[str](\"q\")\n",
            GenericRoute.WrittenGenericConstruction => source + $"def main() -> None:\n    C[str]({arg}, 1)\n",
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };
    }

    public static TheoryData<GenericRoute, Order, Slot, Constraint, bool> GenericBindingCells()
    {
        var data = new TheoryData<GenericRoute, Order, Slot, Constraint, bool>();
        foreach (GenericRoute r in Enum.GetValues<GenericRoute>())
        {
            foreach (Order o in Enum.GetValues<Order>())
            {
                foreach (Slot sl in Enum.GetValues<Slot>())
                {
                    foreach (Constraint c in Enum.GetValues<Constraint>())
                    {
                        data.Add(r, o, sl, c, true);
                        data.Add(r, o, sl, c, false);
                    }
                }
            }
        }
        return data;
    }

    private string RunVerdict(string source, string cell)
    {
        var result = CompileAndExecute(source);
        var seen = result.FailureSummary() + "\n--- source ---\n" + source;

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"a type-parameter binding is decided by the checker, never leaked to Roslyn: {cell}: {seen}");

        return (result.Success
            ? "out:" + result.StandardOutput.Trim()
            : "code:" + (result.RawDiagnostics.FirstOrDefault(d => d.IsError)?.Code ?? "<none>"))
            + "\n" + seen;
    }

    [Theory]
    [MemberData(nameof(GenericBindingCells))]
    public void InitializerCall_BindsTypeParameters_OnEveryRoute(
        GenericRoute route, Order order, Slot slot, Constraint constraint, bool rightArgument)
    {
        var cell = $"{route}/{order}/{slot}/{constraint}/{(rightArgument ? "right" : "wrong")}";
        var outcome = RunVerdict(GenericProgram(route, order, slot, constraint, rightArgument), cell);
        var verdict = outcome[..outcome.IndexOf('\n')];

        verdict.Should().Be(rightArgument ? "out:A" : "code:" + DiagnosticCodes.Semantic.TypeMismatch,
            $"{cell}: {outcome}");
    }

    // ── rigid parameters SELECT as they validate ─────────────────────────────────────────
    //
    // A = `(v: T, n: int)`, B = `(s: str, n: object)`, called with `("s", 1)` where T is the
    // class's own. T is a concrete type there, not a wildcard, so only B accepts a `str` — C#'s
    // `: this("s", 1)` / `: base("s", 1)` binds B. Treating T as a wildcard in selection while
    // validation reads it rigidly made A and B tie (SPY0353).

    public enum RigidRoute
    {
        /// <summary><c>self.__init__("s", 1)</c> inside <c>class C[T]</c>.</summary>
        SelfInit,

        /// <summary><c>super().__init__("s", 1)</c> in <c>class D[T](Base[T])</c>.</summary>
        SuperForwardsOwnParameter,
    }

    public static TheoryData<RigidRoute, Order> RigidSelectionCells()
    {
        var data = new TheoryData<RigidRoute, Order>();
        foreach (RigidRoute r in Enum.GetValues<RigidRoute>())
        {
            foreach (Order o in Enum.GetValues<Order>())
                data.Add(r, o);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(RigidSelectionCells))]
    public void RigidTypeParameter_SelectsAsItValidates(RigidRoute route, Order order)
    {
        var z = route == RigidRoute.SelfInit
            ? "    def __init__(self):\n        self.__init__(\"s\", 1)\n\n"
            : "    def __init__(self):\n        print(\"Z\")\n\n";
        var ab = "    def __init__(self, v: T, n: int):\n        print(\"A\")\n\n"
            + "    def __init__(self, s: str, n: object):\n        print(\"B\")\n\n";
        var body = order == Order.TargetLater ? z + ab : ab + z;
        var source = route == RigidRoute.SelfInit
            ? $"class C[T]:\n{body}def main() -> None:\n    C[int]()\n"
            : $"class Base[T]:\n{body}"
              + "class D[T](Base[T]):\n    def __init__(self):\n        super().__init__(\"s\", 1)\n\n"
              + "def main() -> None:\n    D[int]()\n";

        var cell = $"{route}/{order}";
        var outcome = RunVerdict(source, cell);
        outcome[..outcome.IndexOf('\n')].Should().Be("out:B", $"{cell}: {outcome}");
    }

    // ── an UNWRITTEN generic construction over a set never leaks ─────────────────────────
    //
    // `b: Box[int] = Box(args)` over `Box[T]` with several `__init__`: the arity-chosen overload's
    // formals are OPEN here (`list[T]`, `T?`), and a set has neither inference nor the closed
    // re-check a single `__init__` gets, so pushing them recorded an argument as `List<T>` /
    // `Optional<T>` (CS0246 behind SPY0908). Whatever the checker answers, it answers itself.

    public static TheoryData<bool, Order, bool> UnwrittenGenericSetCells()
    {
        var data = new TheoryData<bool, Order, bool>();
        foreach (var qualified in new[] { false, true })
        {
            foreach (Order o in Enum.GetValues<Order>())
            {
                data.Add(qualified, o, true);
                data.Add(qualified, o, false);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(UnwrittenGenericSetCells))]
    public void UnwrittenGenericConstructionOverASet_NeverLeaks(bool qualified, Order order, bool emptyList)
    {
        var (target, args) = emptyList ? ("xs: list[T], n: int", "[], 3") : ("v: T, w: T?", "5, None()");
        var z = "    def __init__(self):\n        print(\"Z\")\n\n";
        var a = $"    def __init__(self, {target}):\n        print(\"A\")\n\n";
        var cls = "class Box[T]:\n" + (order == Order.TargetLater ? z + a : a + z);
        var source = qualified
            ? "class Outer:\n" + string.Concat(cls.Split('\n').Select(l => l.Length == 0 ? "\n" : "    " + l + "\n"))
              + $"def main() -> None:\n    b: Outer.Box[int] = Outer.Box({args})\n"
            : cls + $"def main() -> None:\n    b: Box[int] = Box({args})\n";

        var result = CompileAndExecute(source);
        var seen = $"{(qualified ? "qualified" : "bare")}/{order}/{(emptyList ? "[]" : "None()")}: "
            + result.FailureSummary() + "\n--- source ---\n" + source;
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, seen);

        // Positive control: the checker reached the argument and answered — it runs, or it refuses
        // the context argument itself (an absence of SPY0908 alone would also hold for a program
        // that stopped parsing).
        var firstError = result.RawDiagnostics.FirstOrDefault(d => d.IsError)?.Code;
        (result.Success || firstError == (emptyList
                ? DiagnosticCodes.Semantic.CannotInferType
                : DiagnosticCodes.Semantic.InvalidNoneConstructor))
            .Should().BeTrue(seen);
    }

    /// <summary>
    /// A chaining call the context checks refuse is not also run through overload selection: one
    /// mistake, one diagnostic, and no SPY0354 for an argument count no constructor was ever going
    /// to be asked about. Two refusals: outside a constructor (SPY0350), and after another chaining
    /// call in the same constructor (SPY0351).
    /// </summary>
    [Theory]
    [InlineData("outside-constructor")]
    [InlineData("after-conflicting-initializer")]
    public void RefusedChainingCall_IsNotAlsoSelected(string refusal)
    {
        var (source, code) = refusal switch
        {
            "outside-constructor" => (
                "class C:\n"
                + "    def __init__(self, n: int):\n        pass\n\n"
                + "    def __init__(self):\n        pass\n\n"
                + "    def reset(self) -> None:\n        self.__init__(1, 2, 3)\n\n"
                + "def main() -> None:\n    C()\n",
                DiagnosticCodes.Semantic.SelfInitOutsideConstructor),
            "after-conflicting-initializer" => (
                "class B:\n    def __init__(self, a: int):\n        pass\n\n"
                + "class C(B):\n"
                + "    def __init__(self, a: int):\n        super().__init__(a)\n\n"
                + "    def __init__(self, a: str, b: str):\n        super().__init__(1)\n\n"
                + "    def __init__(self, f: bool):\n        if f:\n            super().__init__(1)\n"
                + "        self.__init__(1, 2, 3)\n\n"
                + "def main() -> None:\n    print(1)\n",
                DiagnosticCodes.Semantic.ConflictingConstructorInitializers),
            _ => throw new ArgumentOutOfRangeException(nameof(refusal)),
        };
        var result = CompileAndExecute(source);
        var codes = result.RawDiagnostics.Select(d => d.Code).ToList();

        codes.Should().Contain(code, result.FailureSummary());
        codes.Should().NotContain(DiagnosticCodes.Semantic.NoMatchingOverload, result.FailureSummary());
    }
}
