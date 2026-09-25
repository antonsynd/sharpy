using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// SPY0525 (#1871, R-AW): a member whose emitted C# name equals its enclosing class's or struct's
/// is CS0542, whatever the member kind — refused by name in <c>CodeGenInfoComputer</c> instead of
/// leaking the C# error as SPY0908 (the prior commit's behaviour on every refusal cell below).
///
/// <para><b>Cells.</b> member kind {field, property, method, const, nested type} × host {class,
/// struct, dataclass} → SPY0525 with the rename-or-escape steer; the union host {case field, case
/// named like its union} → SPY0525 with the rename-only steer (no escape hatch there); a nested
/// host (<c>class Outer: class Inner: inner</c>) — the module-level walk never visits it; the string-enum
/// host (a member field named like the enum, or an enum named like a synthesized member) → SPY0525
/// with a rename steer — plus the escape when the member's spelling is what collides (#2037) — beside the measured controls whose C# shape ALLOWS the name (int enum,
/// interface) and the union case spelled like its union (SPY0368 alone, never both). The
/// escaped twins (declaration AND every use backtick-escaped) RUN: the positive control that the
/// refusal is keyed on the emitted name, not the source spelling (for a string-enum member the
/// escaped declaration alone suffices — ruling 3 of plan-0ca7b7). The escaped-declaration-only twin
/// stays CS1061 behind SPY0908 — the closed #478 contract (an access site reads its own escape
/// flag), documented as ONE asserted cell, not a matrix row.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class MemberEnclosingTypeCollisionMatrixTests : IntegrationTestBase
{
    public MemberEnclosingTypeCollisionMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string EscapeSteer =
        "Rename the member, or backtick-escape the declaration AND every use (`q`, v.`q`) to keep the Python spelling.";

    private static readonly Dictionary<string, string> MemberKinds = new()
    {
        ["field"] = "    q: int = 1\n",
        ["property"] = "    property q: int = 1\n",
        ["method"] = "    def q(self) -> int:\n        return 1\n",
        ["const"] = "    const q: int = 1\n",
        ["nested_type"] = "    class q:\n        pass\n",
    };

    private static readonly Dictionary<string, string> Hosts = new()
    {
        ["class"] = "class Q:\n",
        ["struct"] = "struct Q:\n",
        ["dataclass"] = "@dataclass\nclass Q:\n",
    };

    public static IEnumerable<object[]> TypeHostCells()
        => from host in Hosts.Keys
           from member in MemberKinds.Keys
           select new object[] { host, member };

    [Theory]
    [MemberData(nameof(TypeHostCells))]
    public void MemberNamedLikeItsType_IsSpy0525_WithTheEscapeSteer(string host, string member)
    {
        var source = Hosts[host] + "    x: int = 0\n" + MemberKinds[member]
            + "\ndef main() -> None:\n    print(1)\n";

        AssertRefused(source, $"Member 'q' would be emitted as 'Q', the same name as its enclosing type 'Q'", EscapeSteer,
            line: host == "dataclass" ? 4 : 3);
    }

    public static IEnumerable<object[]> UnionAndNestedCells() => new[]
    {
        new object[] { "union_case_field", "union U:\n    case Q(q: int)\n    case R()\n",
            "Union case field 'q' would be emitted as 'Q', the same name as its enclosing type 'Q'",
            "Rename the field (a union case field cannot be backtick-escaped).", 2 },
        new object[] { "union_case_named_like_union", "union Q:\n    case q(v: int)\n    case R()\n",
            "Union case 'q' would be emitted as 'Q', the same name as its enclosing type 'Q'",
            "Rename the case (a union case name cannot be backtick-escaped).", 2 },
        // The union's OWN members (audit R4): the body is emitted into the union's base class.
        // A method is the only member kind a union body admits (property/const are SPY0104).
        new object[] { "union_method", "union Shape:\n    case Circle(r: float)\n    case Sq(s: float)\n\n    def shape(self) -> int:\n        return 1\n",
            "Member 'shape' would be emitted as 'Shape', the same name as its enclosing type 'Shape'",
            "Rename the member, or backtick-escape the declaration AND every use (`shape`, v.`shape`) to keep the Python spelling.", 5 },
        new object[] { "nested_host", "class Outer:\n    class Inner:\n        inner: int = 1\n",
            "Member 'inner' would be emitted as 'Inner', the same name as its enclosing type 'Inner'",
            "Rename the member, or backtick-escape the declaration AND every use (`inner`, v.`inner`) to keep the Python spelling.", 3 },
    };

    [Theory]
    [MemberData(nameof(UnionAndNestedCells))]
    public void UnionAndNestedHosts_AreSpy0525(string name, string declaration, string message, string steer, int line)
    {
        _ = name;
        AssertRefused(declaration + "\ndef main() -> None:\n    print(1)\n", message, steer, line);
    }

    // A string-enum member's escaped declaration is enough (#2037, ruling 3: the declaration governs
    // every reference) — offered unless the member is already spelled like the enum.
    private const string EnumMemberRenameSteer = "Rename the member.";
    private const string EnumMemberEscapeSteer =
        "Rename the member, or backtick-escape its declaration (`color`) to keep the Python spelling — every use follows the declaration.";
    private const string EnumNameSteer =
        "Rename the enum (a string enum's class synthesizes the members Name, Value, Values, ToString).";

    /// <summary>
    /// The enum host (plan-bf0244 verify): a STRING enum lowers to a sealed class, so a member whose
    /// singleton field is named like the enum — or an enum named like a member the class synthesizes —
    /// is CS0542. Prior commit: every cell is SPY0908 (CS0542) under <c>run</c>.
    /// </summary>
    public static IEnumerable<object[]> EnumCells() => new[]
    {
        new object[] { "string_enum_member", "enum Color:\n    Color = \"c\"\n    RED = \"r\"\n",
            "Enum member 'Color' would be emitted as 'Color', the same name as its enclosing type 'Color'", EnumMemberRenameSteer, 2 },
        new object[] { "string_enum_member_mangled", "enum Color:\n    color = \"c\"\n    RED = \"r\"\n",
            "Enum member 'color' would be emitted as 'Color', the same name as its enclosing type 'Color'", EnumMemberEscapeSteer, 2 },
        new object[] { "nested_string_enum_member", "class Outer:\n    enum Color:\n        Color = \"c\"\n        RED = \"r\"\n",
            "Enum member 'Color' would be emitted as 'Color', the same name as its enclosing type 'Color'", EnumMemberRenameSteer, 3 },
        new object[] { "string_enum_named_Value", "enum Value:\n    A = \"a\"\n",
            "String enum 'Value' would be emitted as the class 'Value', which declares the synthesized member 'Value'", EnumNameSteer, 1 },
        new object[] { "string_enum_named_name", "enum name:\n    A = \"a\"\n",
            "String enum 'name' would be emitted as the class 'Name', which declares the synthesized member 'Name'", EnumNameSteer, 1 },
        new object[] { "string_enum_named_Values", "enum Values:\n    A = \"a\"\n",
            "String enum 'Values' would be emitted as the class 'Values', which declares the synthesized member 'Values'", EnumNameSteer, 1 },
        new object[] { "string_enum_named_ToString", "enum ToString:\n    A = \"a\"\n",
            "String enum 'ToString' would be emitted as the class 'ToString', which declares the synthesized member 'ToString'", EnumNameSteer, 1 },
        new object[] { "nested_string_enum_named_value", "struct Outer:\n    x: int = 0\n    enum value:\n        A = \"a\"\n",
            "String enum 'value' would be emitted as the class 'Value', which declares the synthesized member 'Value'", EnumNameSteer, 3 },
    };

    [Theory]
    [MemberData(nameof(EnumCells))]
    public void StringEnumHost_IsSpy0525(string name, string declaration, string message, string steer, int line)
    {
        _ = name;
        AssertRefused(declaration + "\ndef main() -> None:\n    print(1)\n", message, steer, line);
    }

    /// <summary>
    /// Measured controls — hosts whose emitted C# shape ALLOWS a member named like the type, so a
    /// refusal there would be a false one: an int-backed enum is a C# <c>enum</c> (<c>enum Color {
    /// Color }</c> is legal), and an interface member may share its interface's name. Prior commit:
    /// all RUN with this output (unchanged).
    /// </summary>
    public static IEnumerable<object[]> LegalSameNameHosts() => new[]
    {
        new object[] { "int_enum", "enum Color:\n    Color = 1\n    RED = 2\n",
            "print(Color.Color.value)\n    print(Color.RED.value)", "1\n2\n" },
        new object[] { "nested_int_enum", "class Outer:\n    enum Color:\n        Color = 1\n        RED = 2\n",
            // Compared, not `.value`: a NESTED enum's `.value` is CS1061 today (#2038).
            "c = Outer.Color.Color\n    print(c == Outer.Color.RED)\n    print(c == Outer.Color.Color)", "False\nTrue\n" },
        new object[] { "interface_method", "interface Shape:\n    def shape(self) -> int: ...\n\nclass Sq(Shape):\n    def shape(self) -> int:\n        return 4\n",
            "s: Shape = Sq()\n    print(s.shape())", "4\n" },
    };

    [Theory]
    [MemberData(nameof(LegalSameNameHosts))]
    public void HostsWhoseCSharpShapeAllowsTheName_Run(string name, string declaration, string use, string expected)
    {
        var source = declaration + "\ndef main() -> None:\n    " + use + "\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"[{name}] {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.CodeGen.MemberEnclosingTypeCollision);
        result.StandardOutput.Should().Be(expected, $"[{name}]");
    }

    public static IEnumerable<object[]> UnionCaseSpelledLikeItsUnion() => new[]
    {
        new object[] { "top_level", "union Opt:\n    case Opt(v: int)\n    case Nothing()\n" },
        new object[] { "nested", "class Outer:\n    union Opt:\n        case Opt(v: int)\n        case Nothing()\n" },
    };

    [Theory]
    [MemberData(nameof(UnionCaseSpelledLikeItsUnion))]
    public void UnionCaseSpelledLikeItsUnion_IsSpy0368Once(string name, string declaration)
    {
        // One defect, one diagnostic: SPY0368 (UnionCaseNameConflict) owns the case spelled exactly
        // like its union; SPY0525's union arm owns only the case that collides after mangling
        // (`union Q: case q()`, the union_case_named_like_union cell). Prior commit: SPY0368 AND
        // SPY0525 on the same case.
        var source = declaration + "\ndef main() -> None:\n    print(1)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        var errors = result.RawDiagnostics.Where(d => d.IsError).ToList();
        errors.Should().ContainSingle($"[{name}] {string.Join(" | ", errors.Select(d => d.Code + " " + d.Message))}");
        errors[0].Code.Should().Be(DiagnosticCodes.Semantic.UnionCaseNameConflict);
    }

    public static IEnumerable<object[]> EscapedTwins() => new[]
    {
        new object[] { "class_field", "class Q:\n    `q`: int = 1\n", "print(Q().`q`)" },
        new object[] { "struct_field", "struct Q:\n    `q`: int = 1\n", "print(Q().`q`)" },
        new object[] { "dataclass_field", "@dataclass\nclass Q:\n    `q`: int = 1\n", "print(Q().`q`)" },
        new object[] { "class_method", "class Q:\n    def `q`(self) -> int:\n        return 1\n", "print(Q().`q`())" },
        new object[] { "union_method", "union Q:\n    case A()\n    case B()\n\n    def `q`(self) -> int:\n        return 1\n",
            "v: Q = Q.A()\n    print(v.`q`())" },
        // The string-enum member the escape steer names (#2037): the escaped DECLARATION alone is
        // enough — the plain use follows it. Prior commit: SPY0908 (CS0117, the use spelled `Color`).
        new object[] { "string_enum_member", "enum Color:\n    `color` = \"1\"\n    RED = \"r\"\n",
            "print(Color.color.value)" },
        new object[] { "nested_string_enum_member", "class Outer:\n    enum Color:\n        `color` = \"1\"\n        RED = \"r\"\n",
            "print(str(Outer.Color.color))" },
    };

    [Theory]
    [MemberData(nameof(EscapedTwins))]
    public void EscapedDeclarationAndUses_Run(string name, string declaration, string use)
    {
        // Positive control: the refusal keys on the EMITTED name — an escaped member emits `q`, which
        // no longer equals `Q`. Prior commit: RUNS as well (unchanged).
        var source = declaration + "\ndef main() -> None:\n    " + use + "\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"[{name}] {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("1\n", $"[{name}]");
    }

    [Fact]
    public void EscapedDeclarationOnly_StaysCs1061_TheClosed478Contract()
    {
        // The steer says "the declaration AND every use" because an unescaped use still spells the
        // PascalCased name: access sites read their own escape flag (#478, closed). One documented
        // cell, not a matrix row — SPY0908 here is the C# compiler's CS1061, as at the prior commit.
        var source = "class Q:\n    `q`: int = 1\n\ndef main() -> None:\n    v = Q()\n    print(v.q)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.CodeGen.MemberEnclosingTypeCollision);
        string.Join("\n", result.CompilationErrors).Should().Contain("CS1061");
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        TypeHostCells().Should().HaveCount(15, "3 hosts × 5 member kinds");
        UnionAndNestedCells().Should().HaveCount(4);
        EnumCells().Should().HaveCount(8, "3 member cells (top-level, mangled, nested) + 5 enum-name cells (4 synthesized names + nested)");
        LegalSameNameHosts().Should().HaveCount(3);
        UnionCaseSpelledLikeItsUnion().Should().HaveCount(2);
        EscapedTwins().Should().HaveCount(7);
    }

    private void AssertRefused(string source, string message, string steer, int line)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"the refusal is SPY0525, not CS0542 behind SPY0908\n{source}");
        var refusals = result.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.CodeGen.MemberEnclosingTypeCollision)
            .ToList();
        refusals.Should().ContainSingle($"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        refusals[0].Message.Should().StartWith(message);
        refusals[0].Message.Should().EndWith(steer);
        refusals[0].Line.Should().Be(line, source);
    }
}
