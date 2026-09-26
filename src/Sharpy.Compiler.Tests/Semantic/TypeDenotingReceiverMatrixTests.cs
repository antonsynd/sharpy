using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The type-denoting receiver matrix (#1817, #1864). One classifier
/// (<c>TypeChecker.ClassifyTypeDenotingReceiver</c>) recognizes every expression that denotes a TYPE
/// in member-access receiver position and records the type it denotes; the emitter spells the
/// receiver of a static member from the recorded type (Rule 2), never from the AST shape.
///
/// <para><b>Axes.</b> Spelling (9): {BareTypeName, BareGenericTypeName, BuiltinAlias, ImportedType,
/// ModuleQualified, NestedTypeChain, BracketedGeneric, TypeAlias, AliasedImport}. Member kind (10):
/// {StaticField, StaticProperty, StaticMethod, Const, NestedType, EnumMember, Constructor,
/// InstanceField, InstanceProperty, InstanceMethod}. Position (6): {Value, BoolStore, Argument,
/// Callee, Annotation, Isinstance}. The bracketed-generic spelling is expanded over
/// <see cref="GenericHostAxis.Hosts"/> (7 host shapes) by <see cref="BracketedGenericHost_StaticConstReads"/>.</para>
///
/// <para><b>Cells.</b> The curated cell list below covers every axis value at least once — the
/// discriminating combinations of the two issues. Each LIVE cell EXECUTES (asserts stdout) or names a
/// diagnostic code. Two refusal families: the nested-chain <c>bool</c> store (SPY0220, the typed
/// refusal that replaced an SPY0908 ICE) and the INSTANCE member reached through a type-denoting
/// receiver (SPY0290). The second is the class contract of #1817 Decision 3 — <b>every</b> spelling of
/// the receiver reaches the same verdict, so the instance kinds are crossed with all five ways to name
/// the type (bare, bare generic, constructed, nested-under-constructed, alias) and the const/@static
/// cells on the same hosts are the positive controls that must still PRINT.
/// <see cref="Cells_CoverEveryAxisOnce"/> anchors the axis sizes to LITERAL counts (9, 10, 6), not to
/// the cell list, so the coverage claim cannot be vacuous.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class TypeDenotingReceiverMatrixTests : IntegrationTestBase
{
    public TypeDenotingReceiverMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── Axis sizes, anchored to literals ─────────────────────────────────────────────────────
    private const int SpellingCount = 9;
    private const int MemberKindCount = 10;
    private const int PositionCount = 6;

    private static readonly string[] Spellings =
    {
        "BareTypeName", "BareGenericTypeName", "BuiltinAlias", "ImportedType", "ModuleQualified",
        "NestedTypeChain", "BracketedGeneric", "TypeAlias", "AliasedImport",
    };

    private static readonly string[] MemberKinds =
    {
        "StaticField", "StaticProperty", "StaticMethod", "Const",
        "NestedType", "EnumMember", "Constructor",
        "InstanceField", "InstanceProperty", "InstanceMethod",
    };

    private static readonly string[] Positions =
    {
        "Value", "BoolStore", "Argument", "Callee", "Annotation", "Isinstance",
    };

    /// <summary>A matrix cell: its axis triple, source, and expected outcome (output OR code).</summary>
    private sealed record Cell(
        string Spelling, string MemberKind, string Position, string Source,
        string? ExpectedOutput = null, string? ExpectedCode = null, string? At = null);

    private static readonly Cell[] Cells =
    {
        // BuiltinAlias × StaticField × Value
        new("BuiltinAlias", "StaticField", "Value",
            "def main() -> None:\n    print(int.max_value)\n",
            ExpectedOutput: "2147483647\n"),

        // ImportedType × StaticProperty × Value
        new("ImportedType", "StaticProperty", "Value",
            "from system import DateTime\n\ndef main() -> None:\n    print(DateTime.max_value.year)\n",
            ExpectedOutput: "9999\n"),

        // ModuleQualified × StaticProperty × Value
        new("ModuleQualified", "StaticProperty", "Value",
            "import system\n\ndef main() -> None:\n    print(system.DateTime.max_value.year)\n",
            ExpectedOutput: "9999\n"),

        // NestedTypeChain × EnumMember × Value — Environment.SpecialFolder.Desktop
        new("NestedTypeChain", "EnumMember", "Value",
            "from system import Environment\n\ndef main() -> None:\n"
            + "    print(Environment.SpecialFolder.Desktop)\n",
            ExpectedOutput: "SpecialFolder.Desktop\n"),

        // NestedTypeChain × EnumMember × BoolStore — the typed refusal (SPY0220), not SPY0908
        new("NestedTypeChain", "EnumMember", "BoolStore",
            "from system import Environment\n\ndef main() -> None:\n"
            + "    f: bool = Environment.SpecialFolder.Desktop\n    print(f)\n",
            ExpectedCode: DiagnosticCodes.Semantic.TypeMismatch),

        // NestedTypeChain × EnumMember × Annotation
        new("NestedTypeChain", "EnumMember", "Annotation",
            "from system import Environment\n\ndef main() -> None:\n"
            + "    f: Environment.SpecialFolder = Environment.SpecialFolder.Desktop\n    print(f)\n",
            ExpectedOutput: "SpecialFolder.Desktop\n"),

        // NestedTypeChain × EnumMember × Isinstance — o is int, not the enum
        new("NestedTypeChain", "EnumMember", "Isinstance",
            "from system import Environment\n\ndef main() -> None:\n"
            + "    o = 5\n    print(isinstance(o, Environment.SpecialFolder))\n",
            ExpectedOutput: "False\n"),

        // NestedTypeChain × StaticMethod × Argument — the nested-chain enum member is the argument to
        // a static method. Assert the call returns a str (portable), NOT that the path is non-empty:
        // GetFolderPath(Desktop) is "" on a headless Linux runner but a real path on a dev box, so a
        // `!= ""` oracle is environment-dependent (green on macOS, red in CI).
        new("NestedTypeChain", "StaticMethod", "Argument",
            "from system import Environment\n\ndef main() -> None:\n"
            + "    print(isinstance(Environment.get_folder_path(Environment.SpecialFolder.Desktop), str))\n",
            ExpectedOutput: "True\n"),

        // BracketedGeneric × Const × Value
        new("BracketedGeneric", "Const", "Value",
            "class G[T]:\n    const K: int = 3\n\n\ndef main() -> None:\n    print(G[int].K)\n",
            ExpectedOutput: "3\n"),

        // BracketedGeneric × StaticField × Value
        new("BracketedGeneric", "StaticField", "Value",
            "class G[T]:\n    @static\n    n: int = 5\n\n\ndef main() -> None:\n    print(G[int].n)\n",
            ExpectedOutput: "5\n"),

        // BracketedGeneric × StaticMethod × Callee
        new("BracketedGeneric", "StaticMethod", "Callee",
            "class G[T]:\n    @static\n    def make() -> int:\n        return 7\n\n\ndef main() -> None:\n    print(G[int].make())\n",
            ExpectedOutput: "7\n"),

        // BracketedGeneric × NestedType × Value — the nested type is a type reference (G[int].Inner.V)
        new("BracketedGeneric", "NestedType", "Value",
            "class G[T]:\n    class Inner:\n        const V: int = 9\n\n\ndef main() -> None:\n    print(G[int].Inner.V)\n",
            ExpectedOutput: "9\n"),

        // BracketedGeneric × Constructor × Callee — new G<int>.Inner()
        new("BracketedGeneric", "Constructor", "Callee",
            "class G[T]:\n    class Inner:\n        def m(self) -> int:\n            return 4\n\n\ndef main() -> None:\n    x = G[int].Inner()\n    print(x.m())\n",
            ExpectedOutput: "4\n"),

        // TypeAlias × Const × Value — type A = G[int] then A.K
        new("TypeAlias", "Const", "Value",
            "class G[T]:\n    const K: int = 3\n\n\ntype A = G[int]\n\n\ndef main() -> None:\n    print(A.K)\n",
            ExpectedOutput: "3\n"),

        // AliasedImport × StaticMethod × Callee — from system import Guid as Gd
        new("AliasedImport", "StaticMethod", "Callee",
            "from system import Guid as Gd\n\ndef main() -> None:\n"
            + "    print(len(Gd.new_guid().to_string()) > 0)\n",
            ExpectedOutput: "True\n"),

        // ── INSTANCE members × every receiver spelling: one SPY0290 each (#1817, Decision 3) ──────
        // The class contract: an instance member does not bind without an instance, whichever way the
        // type is spelled. Before this, only the two BARE spellings were refused; the constructed,
        // nested-constructed and alias spellings typed the member and reached Roslyn as CS0120/CS1503
        // behind SPY0908. Each cell below is paired with a const/@static control on the SAME host
        // further down, which must still print — the verdict is about the MEMBER, not the receiver.

        // BareTypeName × InstanceField × Value
        new("BareTypeName", "InstanceField", "Value",
            HostH + "def main() -> None:\n    print(H.k)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // BareTypeName × InstanceProperty × Value
        new("BareTypeName", "InstanceProperty", "Value",
            HostH + "def main() -> None:\n    print(H.p)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // BareTypeName × InstanceMethod × Callee — the CALL spelling is refused too (CS0120 in C#).
        new("BareTypeName", "InstanceMethod", "Callee",
            HostH + "def main() -> None:\n    print(H.m())\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // BareTypeName × InstanceField × BoolStore — the member refusal wins over the store check.
        new("BareTypeName", "InstanceField", "BoolStore",
            HostH + "def main() -> None:\n    f: bool = H.k\n    print(f)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // BareGenericTypeName × InstanceField × Value — `G.k`, the open generic name.
        new("BareGenericTypeName", "InstanceField", "Value",
            HostG + "def main() -> None:\n    print(G.k)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // BareGenericTypeName × InstanceMethod × Value
        new("BareGenericTypeName", "InstanceMethod", "Value",
            HostG + "def main() -> None:\n    print(G.m)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // BracketedGeneric × InstanceField × Value — the constructed reference (was SPY0908 CS0120).
        new("BracketedGeneric", "InstanceField", "Value",
            HostG + "def main() -> None:\n    print(G[int].k)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // BracketedGeneric × InstanceProperty × Value
        new("BracketedGeneric", "InstanceProperty", "Value",
            HostG + "def main() -> None:\n    print(G[int].p)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // BracketedGeneric × InstanceMethod × Callee (was SPY0908 CS0120)
        new("BracketedGeneric", "InstanceMethod", "Callee",
            HostG + "def main() -> None:\n    print(G[int].m())\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // BracketedGeneric × InstanceField × Argument
        new("BracketedGeneric", "InstanceField", "Argument",
            HostG + "def take(n: int) -> int:\n    return n\n\n\n"
            + "def main() -> None:\n    print(take(G[int].k))\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // NestedTypeChain × InstanceField × Value — `G[int].Inner.k`, the nested-under-constructed
        // spelling. The chain is left untyped by the member arms (the emitter has no carrier for the
        // closed owner, #1941), so the verdict comes from the classifier walking the chain.
        new("NestedTypeChain", "InstanceField", "Value",
            HostGInner + "def main() -> None:\n    print(G[int].Inner.k)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // NestedTypeChain × InstanceMethod × Value — the plain (non-generic) nested chain.
        new("NestedTypeChain", "InstanceMethod", "Value",
            "class Outer:\n    class Inner:\n        def m(self) -> int:\n            return 4\n\n\n"
            + "def main() -> None:\n    print(Outer.Inner.m)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // TypeAlias × InstanceField × Value — `type A = G[int]` then `A.k`.
        new("TypeAlias", "InstanceField", "Value",
            HostG + "type A = G[int]\n\n\ndef main() -> None:\n    print(A.k)\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // TypeAlias × InstanceMethod × Callee
        new("TypeAlias", "InstanceMethod", "Callee",
            HostG + "type A = G[int]\n\n\ndef main() -> None:\n    print(A.m())\n",
            ExpectedCode: DiagnosticCodes.Semantic.InstanceFieldViaTypeName),

        // ── Positive controls on the SAME hosts: the const and the @static member still PRINT. ─────

        // BareTypeName × Const × Value — H declares the instance field k AND the const C.
        new("BareTypeName", "Const", "Value",
            HostH + "def main() -> None:\n    print(H.C)\n",
            ExpectedOutput: "7\n"),

        // BareTypeName × StaticMethod × Callee — the @static sibling of the refused instance method.
        new("BareTypeName", "StaticMethod", "Callee",
            HostH + "def main() -> None:\n    print(H.make())\n",
            ExpectedOutput: "11\n"),

        // BracketedGeneric × StaticField × Value — the @static sibling on the generic host.
        new("BracketedGeneric", "StaticField", "Value",
            HostG + "def main() -> None:\n    print(G[int].n)\n",
            ExpectedOutput: "5\n"),

        // NestedTypeChain × Const × Value — the nested-under-constructed const still reads.
        new("NestedTypeChain", "Const", "Value",
            HostGInner + "def main() -> None:\n    print(G[int].Inner.C)\n",
            ExpectedOutput: "9\n"),
    };

    /// <summary>
    /// A non-generic host declaring one member of each instance kind (field, property, method) beside
    /// a <c>const</c>, a <c>@static</c> field and a <c>@static</c> method. One host for the refused
    /// cells and their positive controls, so a control cannot differ from its refusal in anything but
    /// the member named.
    /// </summary>
    private const string HostH =
        "class H:\n"
        + "    const C: int = 7\n"
        + "    k: int = 3\n"
        + "    @static\n"
        + "    n: int = 5\n\n"
        + "    property get p(self) -> int:\n"
        + "        return 4\n\n"
        + "    def m(self) -> int:\n"
        + "        return 4\n\n"
        + "    @static\n"
        + "    def make() -> int:\n"
        + "        return 11\n\n\n";

    /// <summary>The generic twin of <see cref="HostH"/> — same members, one type parameter.</summary>
    private const string HostG =
        "class G[T]:\n"
        + "    const C: int = 7\n"
        + "    k: int = 3\n"
        + "    @static\n"
        + "    n: int = 5\n\n"
        + "    property get p(self) -> int:\n"
        + "        return 4\n\n"
        + "    def m(self) -> int:\n"
        + "        return 4\n\n"
        + "    @static\n"
        + "    def make() -> int:\n"
        + "        return 11\n\n\n";

    /// <summary>A generic host with a NESTED type carrying the same instance/const pair.</summary>
    private const string HostGInner =
        "class G[T]:\n"
        + "    class Inner:\n"
        + "        const C: int = 9\n"
        + "        k: int = 3\n\n"
        + "        def m(self) -> int:\n"
        + "            return 4\n\n\n";

    public static IEnumerable<object[]> CellData =>
        Cells.Select((c, i) => new object[] { i, $"{c.Spelling}×{c.MemberKind}×{c.Position}" });

    [Theory]
    [MemberData(nameof(CellData))]
    public void Cell_ResolvesAndEmits(int index, string label)
    {
        var cell = Cells[index];
        var result = CompileAndExecute(cell.Source);

        if (cell.ExpectedOutput != null)
        {
            result.Success.Should().BeTrue(
                $"[{label}] a type-denoting receiver must resolve and emit. Diagnostics: "
                + $"{string.Join(" | ", result.CompilationErrors)}\n{cell.Source}");
            result.StandardOutput.Should().Be(cell.ExpectedOutput, $"[{label}]\n{cell.Source}");
            result.RawDiagnostics.Should().NotContain(
                d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
                $"[{label}] must never produce SPY0908\n{cell.Source}");
        }
        else
        {
            result.Success.Should().BeFalse($"[{label}] must be refused\n{cell.Source}");
            result.RawDiagnostics.Should().Contain(d => d.Code == cell.ExpectedCode,
                $"[{label}] must report {cell.ExpectedCode}. Got: "
                + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{cell.Source}");
            result.RawDiagnostics.Should().NotContain(
                d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
                $"[{label}] a typed refusal must never be SPY0908\n{cell.Source}");
        }
    }

    /// <summary>
    /// The bracketed-generic spelling expanded over every host shape (#1817): a <c>const</c> reached
    /// through the constructed reference prints its value on the class, struct, interface, nested-
    /// constructed and derived hosts, and on the plain (non-generic) control.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostData))]
    public void BracketedGenericHost_StaticConstReads(string hostName)
    {
        var host = GenericHostAxis.Hosts.Single(h => h.Name == hostName);
        var source = $"{host.Declaration}\n\ndef main() -> None:\n    print({host.Receiver}.K)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"[{hostName}] `{host.Receiver}.K` must resolve and emit the closed type. Diagnostics: "
            + $"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("3\n", $"[{hostName}]\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{hostName}] must never produce SPY0908\n{source}");
    }

    public static IEnumerable<object[]> HostData =>
        GenericHostAxis.Hosts.Select(h => new object[] { h.Name });

    // ── Totality ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cells_CoverEveryAxisOnce()
    {
        // Axis sizes anchored to LITERALS, not derived from the arrays they name.
        Spellings.Length.Should().Be(SpellingCount);
        MemberKinds.Length.Should().Be(MemberKindCount);
        Positions.Length.Should().Be(PositionCount);
        GenericHostAxis.Hosts.Length.Should().Be(GenericHostAxis.HostCount);
        // Bumped 6 -> 7 when #1865 (P10 Phase 1 Task 2) added the GenericDerived host shape to the
        // shared roster; this test picks it up automatically through HostData/BracketedGenericHost_StaticConstReads.
        GenericHostAxis.HostCount.Should().Be(7);

        Spellings.Should().OnlyHaveUniqueItems();
        MemberKinds.Should().OnlyHaveUniqueItems();
        Positions.Should().OnlyHaveUniqueItems();

        // Every cell names a value that is on its axis.
        foreach (var cell in Cells)
        {
            Spellings.Should().Contain(cell.Spelling);
            MemberKinds.Should().Contain(cell.MemberKind);
            Positions.Should().Contain(cell.Position);
        }

        // Every axis VALUE appears in at least one cell (bracketed spelling is also host-expanded).
        Cells.Select(c => c.Spelling).Distinct().Should().BeEquivalentTo(Spellings);
        Cells.Select(c => c.MemberKind).Distinct().Should().BeEquivalentTo(MemberKinds);
        Cells.Select(c => c.Position).Distinct().Should().BeEquivalentTo(Positions);
    }
}
