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
/// host (<c>class Outer: class Inner: inner</c>) — the module-level walk never visits it. The
/// escaped twins (declaration AND every use backtick-escaped) RUN: the positive control that the
/// refusal is keyed on the emitted name, not the source spelling. The escaped-declaration-only twin
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

    public static IEnumerable<object[]> EscapedTwins() => new[]
    {
        new object[] { "class_field", "class Q:\n    `q`: int = 1\n", "print(Q().`q`)" },
        new object[] { "struct_field", "struct Q:\n    `q`: int = 1\n", "print(Q().`q`)" },
        new object[] { "dataclass_field", "@dataclass\nclass Q:\n    `q`: int = 1\n", "print(Q().`q`)" },
        new object[] { "class_method", "class Q:\n    def `q`(self) -> int:\n        return 1\n", "print(Q().`q`())" },
        new object[] { "union_method", "union Q:\n    case A()\n    case B()\n\n    def `q`(self) -> int:\n        return 1\n",
            "v: Q = Q.A()\n    print(v.`q`())" },
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
        EscapedTwins().Should().HaveCount(5);
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
