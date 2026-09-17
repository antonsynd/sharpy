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
/// <para><b>Axes.</b> Spelling (7): {BuiltinAlias, ImportedType, ModuleQualified, NestedTypeChain,
/// BracketedGeneric, TypeAlias, AliasedImport}. Member kind (7): {StaticField, StaticProperty,
/// StaticMethod, Const, NestedType, EnumMember, Constructor}. Position (6): {Value, BoolStore,
/// Argument, Callee, Annotation, Isinstance}. The bracketed-generic spelling is expanded over
/// <see cref="GenericHostAxis.Hosts"/> (6 host shapes) by <see cref="BracketedGenericHost_StaticConstReads"/>.</para>
///
/// <para><b>Cells.</b> The curated cell list below covers every axis value at least once — the
/// discriminating combinations of the two issues. Each LIVE cell EXECUTES (asserts stdout) or names a
/// diagnostic code; the one refusal cell is the nested-chain <c>bool</c> store (SPY0220), the typed
/// refusal that replaced the SPY0908 ICE. <see cref="Cells_CoverEveryAxisOnce"/> anchors the axis
/// sizes to LITERAL counts (7, 7, 6), not to the cell list, so the coverage claim cannot be vacuous.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class TypeDenotingReceiverMatrixTests : IntegrationTestBase
{
    public TypeDenotingReceiverMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── Axis sizes, anchored to literals ─────────────────────────────────────────────────────
    private const int SpellingCount = 7;
    private const int MemberKindCount = 7;
    private const int PositionCount = 6;

    private static readonly string[] Spellings =
    {
        "BuiltinAlias", "ImportedType", "ModuleQualified", "NestedTypeChain",
        "BracketedGeneric", "TypeAlias", "AliasedImport",
    };

    private static readonly string[] MemberKinds =
    {
        "StaticField", "StaticProperty", "StaticMethod", "Const",
        "NestedType", "EnumMember", "Constructor",
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
            ExpectedOutput: "Desktop\n"),

        // NestedTypeChain × EnumMember × BoolStore — the typed refusal (SPY0220), not SPY0908
        new("NestedTypeChain", "EnumMember", "BoolStore",
            "from system import Environment\n\ndef main() -> None:\n"
            + "    f: bool = Environment.SpecialFolder.Desktop\n    print(f)\n",
            ExpectedCode: DiagnosticCodes.Semantic.TypeMismatch),

        // NestedTypeChain × EnumMember × Annotation
        new("NestedTypeChain", "EnumMember", "Annotation",
            "from system import Environment\n\ndef main() -> None:\n"
            + "    f: Environment.SpecialFolder = Environment.SpecialFolder.Desktop\n    print(f)\n",
            ExpectedOutput: "Desktop\n"),

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
    };

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
        GenericHostAxis.HostCount.Should().Be(6);

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
