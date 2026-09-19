using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Constructed-generic-host DUNDER ROUTE matrix (#1859, Phase 2 Task 3).
///
/// <para><b>Contract.</b> <c>__call__</c>, <c>__len__</c>, <c>__bool__</c>, <c>__iter__</c>,
/// <c>__contains__</c>, <c>__getitem__</c> and <c>__reversed__</c> on a constructed generic host
/// resolve through the declaration (<see cref="Sharpy.Compiler.Semantic.TypeChecker.TryGetGenericHost"/>),
/// substituted — never through the receiver's own first type argument
/// (<c>GenericType.TypeArguments[0]</c>), which is a BUILTIN-CONTAINER convention that has no opinion
/// about a user declaration.</para>
///
/// <para><b>Axes.</b> Dunder (7) × Host (7, <see cref="GenericHostAxis.Hosts"/> — the ONE roster this
/// matrix, <c>ConstructedGenericHostAssignabilityMatrixTests</c> and <c>TypeDenotingReceiverMatrixTests</c>
/// all consume, #1145) × Consumer (3): {DirectRoute (the dunder's own call/operator), Comprehension
/// (the dunder exercised inside a comprehension body), TypeProbe (<c>b: bool = &lt;element&gt;</c> —
/// the DISCRIMINATING cell: a printing cell proves nothing once <c>var</c>/generic inference hides
/// the wrong-but-plausible type the pre-fix code guessed; the probe forces the checker to NAME it)}.
/// A non-V-typed (fixed <c>str</c>/<c>int</c>) dunder body is used throughout — matching the b04/b05/b06
/// shape the bug was found in — deliberately independent of the host's own type parameter, since
/// substituting T is Phase 1's assignability question, not this route question.</para>
///
/// <para><b>V-typed control (b12/b13's shape):</b> a supplementary fact,
/// <see cref="VTypedElementDunders_StillResolveCorrectly"/>, confirms the fix does not disturb the
/// ALREADY-correct case (a dunder typed <c>T</c> itself, substituted to the receiver's own argument).
/// Kept as four SEPARATE minimal hosts (one dunder each) rather than one combined class: combining a
/// generator <c>__iter__</c> and a generator <c>__reversed__</c> on the same class makes
/// <c>reversed()</c> ambiguous between its <c>IEnumerable&lt;T&gt;</c> and
/// <c>IReverseEnumerable&lt;T&gt;</c> overloads — CS0121, filed as #1914, unrelated to #1859 and not
/// worked around here.</para>
///
/// <para><b>N/A cells, by rule:</b> (1) <c>ConstructedInterface</c> × any dunder × any consumer —
/// SPY0280, an interface cannot be instantiated. (2) <c>Comprehension</c> × {<c>__call__</c>,
/// <c>__len__</c>, <c>__bool__</c>, <c>__contains__</c>} — none of the four is naturally exercised by
/// iterating OVER the receiver (a comprehension calling into one of these tests the exact same route
/// DirectRoute already does, with no new discriminating information). (3) <c>TypeProbe</c> ×
/// {<c>__bool__</c>, <c>__contains__</c>} — <c>__bool__</c>'s result assigned to its own declared type
/// (<c>bool</c>) is not a mismatch to probe, and <c>__contains__</c> has no element to store (its own
/// DirectRoute cell already is the discriminating assertion — a type mismatch on the NEEDLE, not a
/// storable element).</para>
///
/// <para><b>DerivedOfGeneric's inherited dunders (formerly N/A, #1913 — now live cells).</b>
/// <c>ProtocolMembership.Has</c>'s <c>GenericType</c> arm used to ask a constructed generic host's OWN
/// <c>ProtocolMethods</c>/<c>Methods</c> only, never its base chain, so an INHERITED
/// <c>__len__</c>/<c>__iter__</c>/<c>__getitem__</c>/<c>__contains__</c> (member on the BASE, derived
/// class a bare <c>pass</c>) was reported absent (SPY0320) even though the TYPE resolution this
/// matrix is about was already correct. Fixed by reusing <c>HasDunderInChain</c> — of the 12 cells
/// (4 dunders × 3 consumers) that WOULD apply, 9 are now live (asserting the SAME outcome every other
/// healthy host asserts for the identical dunder/consumer pair); the remaining 3
/// (<c>__len__</c>/<c>__contains__</c> × Comprehension, <c>__contains__</c> × TypeProbe) stay N/A for
/// the SAME reasons Rules 2/3 already give every OTHER host — unrelated to #1913.
/// <c>__call__</c>/<c>__bool__</c>/<c>__reversed__</c> each already had their own chain-aware route
/// and were unaffected (b08 is the <c>__call__</c> witness).</para>
///
/// <para><b>list()/set() constructor consumer — explicitly NOT an axis value here.</b> The
/// constructor-ring bridge for a user-declared iterable is Phase 3's synthesis fix (#1868, #1832): a
/// non-generator <c>__iter__</c>'s element type is now correctly computed (this phase), but
/// <c>list(c)</c> still ICEs (CS1503, no <c>IEnumerable&lt;ElementType&gt;</c> bridge yet) —
/// <c>b11</c>/<c>b14</c> in this plan's probe ledger. Adding it here would only ever be a red column;
/// Phase 3's own constructor matrix is where it is un-N/A'd.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ConstructedGenericHostDunderMatrixTests : IntegrationTestBase
{
    public ConstructedGenericHostDunderMatrixTests(ITestOutputHelper output) : base(output) { }

    private const int DunderCount = 7;
    private const int HostCount = 7;
    private const int ConsumerCount = 3;
    private const int NotApplicableCellCount = 57;

    private static readonly string[] Dunders =
    {
        "__call__", "__len__", "__bool__", "__iter__", "__contains__", "__getitem__", "__reversed__",
    };

    // The one shared host roster (#1145) — never a private copy.
    private static readonly string[] HostNames = GenericHostAxis.Hosts.Select(h => h.Name).ToArray();

    private static readonly string[] Consumers = { "DirectRoute", "Comprehension", "TypeProbe" };

    private static string Key(string host, string dunder, string consumer) => $"{host}\u00d7{dunder}\u00d7{consumer}";

    // ── Per-dunder fragments (non-V shape) ──────────────────────────────────────────────────

    private static string DunderMember(string dunder) => dunder switch
    {
        "__call__" => "    def __call__(self, x: int) -> int:\n        return x + 1\n",
        "__len__" => "    def __len__(self) -> int:\n        return 3\n",
        "__bool__" => "    def __bool__(self) -> bool:\n        return True\n",
        "__iter__" => "    def __iter__(self) -> Iterator[str]:\n        return iter([\"a\", \"b\"])\n",
        "__contains__" => "    def __contains__(self, x: str) -> bool:\n        return True\n",
        "__getitem__" => "    def __getitem__(self, i: int) -> str:\n        return \"a\"\n",
        "__reversed__" => "    def __reversed__(self) -> str:\n        yield \"b\"\n        yield \"a\"\n",
        _ => throw new ArgumentOutOfRangeException(nameof(dunder), dunder, "unknown dunder"),
    };

    // ── Per-host declaration, with the dunder on the member-holder class ───────────────────

    /// <summary>Returns null for the one host that cannot be built at all (ConstructedInterface).</summary>
    private static (string Declaration, string ConstructExpr)? HostDeclaration(string hostName, string dunder)
    {
        var member = DunderMember(dunder);

        return hostName switch
        {
            "Plain" =>
                ($"class HBag:\n    def __init__(self) -> None:\n        pass\n\n{member}", "HBag()"),

            "ConstructedClass" =>
                ($"class HBox[T]:\n    def __init__(self) -> None:\n        pass\n\n{member}", "HBox[int]()"),

            "NestedConstructed" =>
                ($"class HBox[T]:\n    def __init__(self) -> None:\n        pass\n\n{member}", "HBox[HBox[int]]()"),

            "ConstructedStruct" =>
                ($"struct HBoxS[T]:\n    def __init__(self) -> None:\n        pass\n\n{member}", "HBoxS[int]()"),

            "ConstructedInterface" => null,

            // Member on the BASE; the derived class is a bare `pass` — reached only via the base
            // chain (#1913, fixed: ProtocolMembership.Has now walks it for a constructed generic host).
            "DerivedOfGeneric" =>
                ($"class HBase[T]:\n    def __init__(self) -> None:\n        pass\n\n{member}"
                 + "\n\nclass HDerived[T](HBase[T]):\n    pass\n",
                 "HDerived[int]()"),

            // Member on the DERIVED class itself — own-declared, not inherited, so none of Rule 2's
            // chain-walk gap applies here.
            "GenericDerived" =>
                ("class HBase2[T]:\n    def __init__(self) -> None:\n        pass\n\n\n"
                 + $"class HDerived2[T](HBase2[T]):\n    def __init__(self) -> None:\n        pass\n\n{member}",
                 "HDerived2[int]()"),

            _ => throw new ArgumentOutOfRangeException(nameof(hostName), hostName, "unknown host"),
        };
    }

    // ── Consumer composition and expectations ──────────────────────────────────────────────

    private enum Outcome { Runs, Refused }

    /// <summary>One consumer cell's body plus its expectation. <c>ExpectedOutput</c> is set for
    /// <see cref="Outcome.Runs"/>; <c>ExpectedCode</c>/<c>MustName</c> for <see cref="Outcome.Refused"/>
    /// (the substring the message must name — the DECLARED type, mistyped not untyped).</summary>
    private sealed record Cell(string Main, Outcome Outcome, string? ExpectedOutput, string? ExpectedCode, string? MustName);

    private static Cell DirectRouteCell(string dunder, string construct) => dunder switch
    {
        "__call__" => new($"def main() -> None:\n    c = {construct}\n    print(c(1))\n", Outcome.Runs, "2\n", null, null),
        "__len__" => new($"def main() -> None:\n    c = {construct}\n    print(len(c))\n", Outcome.Runs, "3\n", null, null),
        "__bool__" => new($"def main() -> None:\n    c = {construct}\n    print(bool(c))\n", Outcome.Runs, "True\n", null, null),
        "__iter__" => new($"def main() -> None:\n    c = {construct}\n    for x in c:\n        print(x.upper())\n", Outcome.Runs, "A\nB\n", null, null),
        "__contains__" => new($"def main() -> None:\n    c = {construct}\n    print(1 in c)\n", Outcome.Refused, null, DiagnosticCodes.Semantic.InvalidBinaryOperation, "int32"),
        "__getitem__" => new($"def main() -> None:\n    c = {construct}\n    print(c[0].upper())\n", Outcome.Runs, "A\n", null, null),
        "__reversed__" => new($"def main() -> None:\n    c = {construct}\n    for x in reversed(c):\n        print(x.upper())\n", Outcome.Runs, "B\nA\n", null, null),
        _ => throw new ArgumentOutOfRangeException(nameof(dunder)),
    };

    private static Cell ComprehensionCell(string dunder, string construct) => dunder switch
    {
        "__iter__" => new($"def main() -> None:\n    c = {construct}\n    print([x.upper() for x in c])\n", Outcome.Runs, "['A', 'B']\n", null, null),
        "__getitem__" => new($"def main() -> None:\n    c = {construct}\n    print([c[i].upper() for i in range(1)])\n", Outcome.Runs, "['A']\n", null, null),
        "__reversed__" => new($"def main() -> None:\n    c = {construct}\n    print([x.upper() for x in reversed(c)])\n", Outcome.Runs, "['B', 'A']\n", null, null),
        _ => throw new ArgumentOutOfRangeException(nameof(dunder), dunder, "no comprehension consumer for this dunder (N/A)"),
    };

    private static Cell TypeProbeCell(string dunder, string construct) => dunder switch
    {
        "__call__" => new($"def main() -> None:\n    c = {construct}\n    b: bool = c(1)\n", Outcome.Refused, null, DiagnosticCodes.Semantic.TypeMismatch, "int32"),
        "__len__" => new($"def main() -> None:\n    c = {construct}\n    b: bool = len(c)\n", Outcome.Refused, null, DiagnosticCodes.Semantic.TypeMismatch, "int32"),
        "__iter__" => new($"def main() -> None:\n    c = {construct}\n    for x in c:\n        b: bool = x\n", Outcome.Refused, null, DiagnosticCodes.Semantic.TypeMismatch, "str"),
        "__getitem__" => new($"def main() -> None:\n    c = {construct}\n    b: bool = c[0]\n", Outcome.Refused, null, DiagnosticCodes.Semantic.TypeMismatch, "str"),
        "__reversed__" => new($"def main() -> None:\n    c = {construct}\n    for x in reversed(c):\n        b: bool = x\n", Outcome.Refused, null, DiagnosticCodes.Semantic.TypeMismatch, "str"),
        _ => throw new ArgumentOutOfRangeException(nameof(dunder), dunder, "no type probe for this dunder (N/A)"),
    };

    private static (string Source, Outcome Outcome, string? ExpectedOutput, string? ExpectedCode, string? MustName)
        Compose(string dunder, string consumer, string declaration, string construct)
    {
        var cell = consumer switch
        {
            "DirectRoute" => DirectRouteCell(dunder, construct),
            "Comprehension" => ComprehensionCell(dunder, construct),
            "TypeProbe" => TypeProbeCell(dunder, construct),
            _ => throw new ArgumentOutOfRangeException(nameof(consumer), consumer, "unknown consumer"),
        };

        return (declaration + "\n\n" + cell.Main, cell.Outcome, cell.ExpectedOutput, cell.ExpectedCode, cell.MustName);
    }

    // ── N/A roster ──────────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> NotApplicableCells = BuildNotApplicableCells();

    private static Dictionary<string, string> BuildNotApplicableCells()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var healthyHosts = HostNames.Where(h => h != "ConstructedInterface").ToArray();

        // Rule 2: none of these four is naturally exercised BY a comprehension (a comprehension
        // calling into them tests the same route DirectRoute already does).
        foreach (var hostName in healthyHosts)
        foreach (var dunder in new[] { "__call__", "__len__", "__bool__", "__contains__" })
            dict[Key(hostName, dunder, "Comprehension")] =
                "a comprehension calling into this dunder exercises the same route DirectRoute already does";

        // Rule 3: no element to probe.
        foreach (var hostName in healthyHosts)
        {
            dict[Key(hostName, "__bool__", "TypeProbe")] =
                "__bool__'s result assigned to its own declared type is not a mismatch to probe";
            dict[Key(hostName, "__contains__", "TypeProbe")] =
                "no element to store — __contains__'s DirectRoute cell (the needle mismatch) is the discriminating assertion";
        }

        // DerivedOfGeneric's inherited __len__/__iter__/__getitem__/__contains__ are LIVE cells now
        // (#1913 fixed) — no N/A row for them; Rules 2/3 above still apply to this host like any
        // other healthy one.

        // Rule 1 (broadest — always wins): an interface cannot be instantiated.
        foreach (var dunder in Dunders)
        foreach (var consumer in Consumers)
            dict[Key("ConstructedInterface", dunder, consumer)] =
                "SPY0280: an interface cannot be instantiated — no instance exists to route a dunder call through";

        return dict;
    }

    public static IEnumerable<object[]> ApplicableCells =>
        from h in HostNames
        from d in Dunders
        from c in Consumers
        where !NotApplicableCells.ContainsKey(Key(h, d, c))
        select new object[] { h, d, c };

    [Theory]
    [MemberData(nameof(ApplicableCells))]
    public void Cell_ConstructedGenericHostDunderRouteAsksTheDeclaration(string hostName, string dunder, string consumer)
    {
        var label = Key(hostName, dunder, consumer);
        var built = HostDeclaration(hostName, dunder);
        built.Should().NotBeNull($"[{label}] must have a buildable declaration (N/A hosts are excluded by the roster)");
        var (declaration, construct) = built!.Value;

        var (source, outcome, expectedOutput, expectedCode, mustName) = Compose(dunder, consumer, declaration, construct);
        var result = CompileAndExecute(source);
        var seen = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"));

        if (outcome == Outcome.Runs)
        {
            result.Success.Should().BeTrue(
                $"[{label}] must compile and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.Should().Be(expectedOutput, $"[{label}]\n{source}");
        }
        else
        {
            result.Success.Should().BeFalse($"[{label}] must be refused\n{source}");
            result.RawDiagnostics.Should().Contain(
                d => d.Code == expectedCode && d.Message.Contains(mustName!, StringComparison.Ordinal),
                $"[{label}] must report {expectedCode} naming '{mustName}' (the DECLARED type, mistyped not "
                + $"untyped). Got: {seen}\n{source}");
        }
    }

    /// <summary>
    /// b12/b13's shape: a dunder typed <paramref name="T"/> itself (not a fixed non-V type), which
    /// already worked before #1859 and must keep working after — substitution through the view is the
    /// identity here (T IS the receiver's own argument). Four separate hosts (see the class doc: #1914).
    /// </summary>
    [Fact]
    public void VTypedElementDunders_StillResolveCorrectly()
    {
        var getitem = CompileAndExecute(
            "class HBox[T]:\n    v: T\n    def __init__(self, v: T) -> None:\n        self.v = v\n"
            + "    def __getitem__(self, i: int) -> T:\n        return self.v\n\n\n"
            + "def main() -> None:\n    c = HBox[str](\"z\")\n    print(c[0].upper())\n");
        getitem.Success.Should().BeTrue(string.Join(" | ", getitem.CompilationErrors));
        getitem.StandardOutput.Should().Be("Z\n");

        var iter = CompileAndExecute(
            "class HBox[T]:\n    v: T\n    def __init__(self, v: T) -> None:\n        self.v = v\n"
            + "    def __iter__(self) -> T:\n        yield self.v\n\n\n"
            + "def main() -> None:\n    c = HBox[str](\"z\")\n    for x in c:\n        print(x.upper())\n");
        iter.Success.Should().BeTrue(string.Join(" | ", iter.CompilationErrors));
        iter.StandardOutput.Should().Be("Z\n");

        var reversed = CompileAndExecute(
            "class HBox[T]:\n    v: T\n    def __init__(self, v: T) -> None:\n        self.v = v\n"
            + "    def __reversed__(self) -> T:\n        yield self.v\n\n\n"
            + "def main() -> None:\n    c = HBox[str](\"z\")\n    for x in reversed(c):\n        print(x.upper())\n");
        reversed.Success.Should().BeTrue(string.Join(" | ", reversed.CompilationErrors));
        reversed.StandardOutput.Should().Be("Z\n");

        var contains = CompileAndExecute(
            "class HBox[T]:\n    v: T\n    def __init__(self, v: T) -> None:\n        self.v = v\n"
            + "    def __contains__(self, x: T) -> bool:\n        return x == self.v\n\n\n"
            + "def main() -> None:\n    c = HBox[str](\"z\")\n    print(\"z\" in c)\n");
        contains.Success.Should().BeTrue(string.Join(" | ", contains.CompilationErrors));
        contains.StandardOutput.Should().Be("True\n");
    }

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        GenericHostAxis.Hosts.Length.Should().Be(GenericHostAxis.HostCount);
        GenericHostAxis.HostCount.Should().Be(HostCount);
        Dunders.Length.Should().Be(DunderCount);
        HostNames.Length.Should().Be(HostCount);
        Consumers.Length.Should().Be(ConsumerCount);

        Dunders.Should().OnlyHaveUniqueItems();
        HostNames.Should().OnlyHaveUniqueItems();
        Consumers.Should().OnlyHaveUniqueItems();

        var product = DunderCount * HostCount * ConsumerCount;
        var applicable = ApplicableCells.Count();
        var notApplicable = NotApplicableCells.Count;

        notApplicable.Should().Be(NotApplicableCellCount, "every N/A cell is written down with its reason");

        var allKeys = (from h in HostNames from d in Dunders from c in Consumers select Key(h, d, c)).ToHashSet();
        NotApplicableCells.Keys.Should().OnlyContain(k => allKeys.Contains(k), "an N/A key must name a real cell");

        (applicable + notApplicable).Should().Be(product,
            $"applicable ({applicable}) + N/A ({notApplicable}) must be the whole product "
            + $"({DunderCount} \u00d7 {HostCount} \u00d7 {ConsumerCount})");
    }
}
