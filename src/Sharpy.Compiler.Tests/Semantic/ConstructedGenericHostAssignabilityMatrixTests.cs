using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Constructed-generic-host assignability matrix (#1865, Phase 1 Task 2).
///
/// <para><b>Contract.</b> A constructed generic host <c>G[A]</c> is assignable to every interface
/// its declaration reaches — explicit or synthesized, generic OR non-generic — through substitution.
/// Before <c>6a94d26b6</c> (Phase 1 Task 1), <see cref="Sharpy.Compiler.Semantic.TypeChecker"/>'s
/// <c>IsAssignable</c> only entered its variance walk when the TARGET was itself a
/// <see cref="Sharpy.Compiler.Semantic.GenericType"/>: a non-generic interface target (<c>ISized</c>,
/// <c>IBoolConvertible</c>, an explicit non-generic user interface) drew SPY0220 for every host shape,
/// even though the identical GENERIC-interface target already worked. This matrix is the class-level
/// ratchet for that fix.</para>
///
/// <para><b>Axes.</b> Host (7, <see cref="GenericHostAxis.Hosts"/> — the ONE roster this matrix,
/// <c>TypeDenotingReceiverMatrixTests</c> and Phase 2's dunder-route matrix all consume, #1145):
/// {Plain (control), ConstructedClass, NestedConstructed, ConstructedStruct, ConstructedInterface
/// (N/A — cannot be instantiated), DerivedOfGeneric (member on the BASE, inherited), GenericDerived
/// (member on the DERIVED itself)} × Interface (7): {ISized (<c>__len__</c>), IBoolConvertible
/// (<c>__bool__</c>), IReverseEnumerable[T] (<c>__reversed__</c>, generic-target control),
/// IEnumerable[T] (generator <c>__iter__</c>, generic-target control), IEquatable[T] (N/A — needs
/// <c>from System import IEquatable</c>, out of scope here), an explicit non-generic user interface
/// (<c>IShape</c>), an explicit <c>ISized</c> + <c>__len__</c> overlap} × Position (6): {annotated
/// store, parameter, return, list element, isinstance (control), <c>as!</c> (control)}.</para>
///
/// <para><b>Why every applicable cell EXECUTES.</b> An acceptance-only claim ("compiles") cannot
/// distinguish the fix from a vacuously-permissive change; every cell prints an observable value
/// (<c>len(s)</c>, <c>bool(s)</c>, the yielded elements, <c>s.area()</c>) and the assertion is on
/// stdout, not on <c>result.Success</c> alone.</para>
///
/// <para><b>N/A cells, by rule:</b> (1) <c>ConstructedInterface</c> × any interface × any position —
/// SPY0280, an interface cannot be instantiated, so there is no instance to test assignability from.
/// (2) any host × <c>IEquatable[T]</c> × any position — needs <c>from System import IEquatable</c>,
/// out of scope for this from-import-free matrix. (3) any host × {IReverseEnumerable[T],
/// IEnumerable[T]} × <c>isinstance</c> — <c>isinstance</c>'s second argument must name a non-generic
/// type; a bracketed generic interface argument is refused by SPY0344/SPY0200, independent of #1865
/// (measured: <c>isinstance(b, IReverseEnumerable[int])</c> and the bare <c>IReverseEnumerable</c>
/// spelling both fail this way). <c>as!</c> has no such restriction — it accepts a bracketed generic
/// interface target and keeps its own runtime CLR test, untouched by #1865 (Design Decision 2).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ConstructedGenericHostAssignabilityMatrixTests : IntegrationTestBase
{
    public ConstructedGenericHostAssignabilityMatrixTests(ITestOutputHelper output) : base(output) { }

    private const int HostCount = 7;
    private const int InterfaceCount = 7;
    private const int PositionCount = 6;
    private const int NotApplicableCellCount = 90;

    // The one host roster (#1145) — never a private copy.
    private static readonly string[] HostNames = GenericHostAxis.Hosts.Select(h => h.Name).ToArray();

    private static readonly string[] Interfaces =
    {
        "ISized", "IBoolConvertible", "IReverseEnumerable", "IEnumerable", "IEquatable",
        "ExplicitNonGeneric", "ExplicitOverlap",
    };

    private static readonly string[] Positions =
    {
        "AnnotatedStore", "Parameter", "Return", "ListElement", "Isinstance", "AsBang",
    };

    private static string Key(string host, string iface, string position) => $"{host}×{iface}×{position}";

    // ── Per-interface fragments ─────────────────────────────────────────────────────────────

    /// <summary>The dunder (or explicit-interface method) body placed on whichever class the host
    /// shape uses as its "member holder" — the base for DerivedOfGeneric, the derived class for
    /// GenericDerived, the class/struct itself otherwise. Every body is deliberately independent of
    /// the host's own type parameter T (a fixed literal return) — Phase 1 is about ASSIGNABILITY, not
    /// about substituting T into a dunder's return type (that is Phase 2's #1859 concern).</summary>
    private static string DunderMember(string interfaceKind) => interfaceKind switch
    {
        "ISized" or "ExplicitOverlap" => "    def __len__(self) -> int:\n        return 6\n",
        "IBoolConvertible" => "    def __bool__(self) -> bool:\n        return True\n",
        "IReverseEnumerable" => "    def __reversed__(self) -> int:\n        yield 5\n        yield 4\n",
        "IEnumerable" => "    def __iter__(self) -> int:\n        yield 1\n        yield 2\n",
        "ExplicitNonGeneric" => "    def area(self) -> int:\n        return 4\n",
        _ => throw new ArgumentOutOfRangeException(nameof(interfaceKind), interfaceKind, "no member for this interface kind (IEquatable is N/A)"),
    };

    private static string BaseListSuffix(string interfaceKind) => interfaceKind switch
    {
        "ExplicitOverlap" => "(ISized)",
        "ExplicitNonGeneric" => "(IShape)",
        _ => "",
    };

    /// <summary>Combines a generic base-class reference with an optional explicit-interface suffix
    /// (itself already parenthesized, e.g. <c>"(ISized)"</c>) into ONE base-list clause —
    /// <c>(HBase2[T], ISized)</c>, never two separate parenthesized groups.</summary>
    private static string DerivedBaseList(string baseClassRef, string interfaceSuffix)
        => interfaceSuffix.Length == 0
            ? baseClassRef
            : $"{baseClassRef}, {interfaceSuffix[1..^1]}";

    /// <summary>Source prepended once, ahead of the host declaration — only the explicit
    /// non-generic interface needs one declared (ISized/IBoolConvertible/IEnumerable/
    /// IReverseEnumerable are Sharpy.Core protocol interfaces, always in scope).</summary>
    private static string Prelude(string interfaceKind) => interfaceKind == "ExplicitNonGeneric"
        ? "interface IShape:\n    def area(self) -> int:\n        ...\n\n\n"
        : "";

    private static string InterfaceTypeSpelling(string interfaceKind) => interfaceKind switch
    {
        "ISized" or "ExplicitOverlap" => "ISized",
        "IBoolConvertible" => "IBoolConvertible",
        "IReverseEnumerable" => "IReverseEnumerable[int]",
        "IEnumerable" => "IEnumerable[int]",
        "ExplicitNonGeneric" => "IShape",
        _ => throw new ArgumentOutOfRangeException(nameof(interfaceKind), interfaceKind, "no type spelling for this interface kind (IEquatable is N/A)"),
    };

    private static string Assertion(string interfaceKind, string receiver) => interfaceKind switch
    {
        "ISized" or "ExplicitOverlap" => $"print(len({receiver}))",
        "IBoolConvertible" => $"print(bool({receiver}))",
        "IReverseEnumerable" => $"for x in reversed({receiver}):\n        print(x)",
        "IEnumerable" => $"for x in {receiver}:\n        print(x)",
        "ExplicitNonGeneric" => $"print({receiver}.area())",
        _ => throw new ArgumentOutOfRangeException(nameof(interfaceKind), interfaceKind, "no assertion for this interface kind (IEquatable is N/A)"),
    };

    private static string ExpectedOutput(string interfaceKind) => interfaceKind switch
    {
        "ISized" or "ExplicitOverlap" => "6\n",
        "IBoolConvertible" => "True\n",
        "IReverseEnumerable" => "5\n4\n",
        "IEnumerable" => "1\n2\n",
        "ExplicitNonGeneric" => "4\n",
        _ => throw new ArgumentOutOfRangeException(nameof(interfaceKind), interfaceKind, "no expected output for this interface kind (IEquatable is N/A)"),
    };

    // ── Per-host declaration, with the interface's member on the member-holder class ──────────

    /// <summary>Returns null for the one host that cannot be built at all (ConstructedInterface).</summary>
    private static (string Declaration, string ConstructExpr)? HostDeclaration(string hostName, string interfaceKind)
    {
        var member = DunderMember(interfaceKind);
        var baseSuffix = BaseListSuffix(interfaceKind);

        return hostName switch
        {
            "Plain" =>
                ($"class HBag{baseSuffix}:\n    def __init__(self) -> None:\n        pass\n\n{member}",
                 "HBag()"),

            "ConstructedClass" =>
                ($"class HBox[T]{baseSuffix}:\n    def __init__(self) -> None:\n        pass\n\n{member}",
                 "HBox[int]()"),

            "NestedConstructed" =>
                ($"class HBox[T]{baseSuffix}:\n    def __init__(self) -> None:\n        pass\n\n{member}",
                 "HBox[HBox[int]]()"),

            "ConstructedStruct" =>
                ($"struct HBoxS[T]{baseSuffix}:\n    def __init__(self) -> None:\n        pass\n\n{member}",
                 "HBoxS[int]()"),

            "ConstructedInterface" => null,

            // Member on the BASE; the derived class is a bare `pass` — the member is reached only
            // through the base-chain walk in EnumerateSupertypes (#1865's inherited-dunder cell).
            "DerivedOfGeneric" =>
                ($"class HBase[T]{baseSuffix}:\n    def __init__(self) -> None:\n        pass\n\n{member}"
                 + "\n\nclass HDerived[T](HBase[T]):\n    pass\n",
                 "HDerived[int]()"),

            // Member on the DERIVED class itself — the derived symbol's OWN Interfaces/member list
            // carries it, not only something reached by walking through the base. One base list, not
            // two parenthesized groups: `(HBase2[T], IShape)`, matching a normal multi-base clause.
            "GenericDerived" =>
                ("class HBase2[T]:\n    def __init__(self) -> None:\n        pass\n\n\n"
                 + $"class HDerived2[T]({DerivedBaseList("HBase2[T]", baseSuffix)}):\n    def __init__(self) -> None:\n        pass\n\n{member}",
                 "HDerived2[int]()"),

            _ => throw new ArgumentOutOfRangeException(nameof(hostName), hostName, "unknown host"),
        };
    }

    // ── Position wrapping ───────────────────────────────────────────────────────────────────

    private static string ComposeSource(string interfaceKind, string position, string declaration, string constructExpr)
    {
        var pre = Prelude(interfaceKind);
        var typeSpelling = InterfaceTypeSpelling(interfaceKind);

        return position switch
        {
            "AnnotatedStore" => pre + declaration
                + $"\n\ndef main() -> None:\n    s: {typeSpelling} = {constructExpr}\n    {Assertion(interfaceKind, "s")}\n",

            "Parameter" => pre + declaration
                + $"\n\ndef f(s: {typeSpelling}) -> None:\n    {Assertion(interfaceKind, "s")}\n\n\n"
                + $"def main() -> None:\n    f({constructExpr})\n",

            "Return" => pre + declaration
                + $"\n\ndef make() -> {typeSpelling}:\n    return {constructExpr}\n\n\n"
                + $"def main() -> None:\n    s = make()\n    {Assertion(interfaceKind, "s")}\n",

            "ListElement" => pre + declaration
                + $"\n\ndef main() -> None:\n    xs: list[{typeSpelling}] = [{constructExpr}]\n"
                + $"    s = xs[0]\n    {Assertion(interfaceKind, "s")}\n",

            // isinstance's second argument must be a BARE (non-generic) type name — see the class
            // doc's N/A rule (3); this arm is reached only for the four bare-eligible interfaces.
            "Isinstance" => pre + declaration
                + $"\n\ndef main() -> None:\n    b = {constructExpr}\n    print(isinstance(b, {typeSpelling}))\n",

            // `as!` keeps its runtime CLR test — untouched by #1865 (Design Decision 2) — and, unlike
            // isinstance, accepts a bracketed generic interface target.
            "AsBang" => pre + declaration
                + $"\n\ndef main() -> None:\n    b = {constructExpr}\n    s = b as! {typeSpelling}\n    {Assertion(interfaceKind, "s")}\n",

            _ => throw new ArgumentOutOfRangeException(nameof(position), position, "unknown position"),
        };
    }

    // ── N/A roster ──────────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> NotApplicableCells = BuildNotApplicableCells();

    private static Dictionary<string, string> BuildNotApplicableCells()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);

        // Rule 1: ConstructedInterface cannot be instantiated (SPY0280) — no instance to test.
        foreach (var interfaceKind in Interfaces)
        foreach (var position in Positions)
        {
            dict[Key("ConstructedInterface", interfaceKind, position)] =
                "SPY0280: an interface cannot be instantiated — no instance exists to test assignability from";
        }

        // Rule 2: IEquatable[T] needs `from System import IEquatable` — out of scope here.
        foreach (var hostName in HostNames.Where(h => h != "ConstructedInterface"))
        foreach (var position in Positions)
        {
            dict[Key(hostName, "IEquatable", position)] =
                "IEquatable[T] needs `from System import IEquatable`; out of scope for this from-import-free matrix";
        }

        // Rule 3: isinstance refuses a bracketed generic interface argument (SPY0344/SPY0200),
        // independent of #1865.
        foreach (var hostName in HostNames.Where(h => h != "ConstructedInterface"))
        foreach (var interfaceKind in new[] { "IReverseEnumerable", "IEnumerable" })
        {
            dict[Key(hostName, interfaceKind, "Isinstance")] =
                "isinstance's second argument must name a non-generic type; SPY0344/SPY0200 refuse a "
                + "bracketed generic interface argument, independent of #1865";
        }

        return dict;
    }

    public static IEnumerable<object[]> ApplicableCells =>
        from h in HostNames
        from i in Interfaces
        from p in Positions
        where !NotApplicableCells.ContainsKey(Key(h, i, p))
        select new object[] { h, i, p };

    [Theory]
    [MemberData(nameof(ApplicableCells))]
    public void Cell_ConstructedGenericHostIsAssignableToTheInterface(string hostName, string interfaceKind, string position)
    {
        var label = Key(hostName, interfaceKind, position);
        var built = HostDeclaration(hostName, interfaceKind);
        built.Should().NotBeNull($"[{label}] must have a buildable declaration (N/A hosts are excluded by the roster)");
        var (declaration, constructExpr) = built!.Value;

        var source = ComposeSource(interfaceKind, position, declaration, constructExpr);
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"[{label}] must compile and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");

        var expected = position == "Isinstance" ? "True\n" : ExpectedOutput(interfaceKind);
        result.StandardOutput.Should().Be(expected, $"[{label}]\n{source}");
    }

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        // Axis sizes anchored to LITERALS, and the host axis anchored to the ONE shared roster.
        GenericHostAxis.Hosts.Length.Should().Be(GenericHostAxis.HostCount);
        GenericHostAxis.HostCount.Should().Be(HostCount);
        HostNames.Length.Should().Be(HostCount);
        Interfaces.Length.Should().Be(InterfaceCount);
        Positions.Length.Should().Be(PositionCount);

        HostNames.Should().OnlyHaveUniqueItems();
        Interfaces.Should().OnlyHaveUniqueItems();
        Positions.Should().OnlyHaveUniqueItems();

        var product = HostCount * InterfaceCount * PositionCount;
        var applicable = ApplicableCells.Count();
        var notApplicable = NotApplicableCells.Count;

        notApplicable.Should().Be(NotApplicableCellCount, "every N/A cell is written down with its reason");

        var allKeys = (from h in HostNames from i in Interfaces from p in Positions select Key(h, i, p)).ToHashSet();
        NotApplicableCells.Keys.Should().OnlyContain(k => allKeys.Contains(k), "an N/A key must name a real cell");

        (applicable + notApplicable).Should().Be(product,
            $"applicable ({applicable}) + N/A ({notApplicable}) must be the whole product "
            + $"({HostCount} × {InterfaceCount} × {PositionCount})");
    }
}
