using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2040 (R-CG): <c>str.format</c>'s <c>{0.attr}</c> field finds a member by the name the compiler
/// emitted for it. The runtime resolves <c>NameMangling.ToPascalCase(attr)</c> — Sharpy.Core's one
/// copy of the forward rule, which the compiler's <c>NameMangler</c> delegates to — and then the
/// verbatim spelling (a backtick-escaped member is emitted verbatim). Before the move the runtime
/// looked up the spelling as written, so every Sharpy-declared member but an escaped one raised
/// <c>AttributeError</c> (python prints the value).
/// </summary>
/// <remarks>
/// Cells: host {class, dataclass, struct} × member {snake_case field <c>n_items</c>, single-word
/// field <c>n</c>, property <c>total</c>, backtick-escaped field <c>`raw_name`</c>} × chain
/// {<c>{0.m}</c>, through another object <c>{0.inner.m}</c>, with a spec <c>{0.m:&gt;4}</c>}; plus
/// the missing-member AttributeError and the <c>{0[1]}</c> item control. Stdlib members
/// (<c>{0.year}</c>, <c>{0.days}</c>) are cells of <c>StrFormatAttributeStdlibTests</c>, where the
/// stdlib resolves. Expected values are python3's on the same program.
/// </remarks>
public class StrFormatAttributeMatrixTests : IntegrationTestBase
{
    public StrFormatAttributeMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Members = """
            n_items: int
            n: int
            `raw_name`: int
        """;

    private const string Property = """

            property get total(self) -> int:
                return 6
        """;

    private static readonly Dictionary<string, (string Declaration, string Construction)> Hosts = new()
    {
        ["class"] = ("class H:\n" + Members + "\n\n    def __init__(self) -> None:\n        self.n_items = 3\n        self.n = 4\n        self.`raw_name` = 5\n" + Property + "\n", "H()"),
        ["dataclass"] = ("@dataclass\nclass H:\n" + Members + "\n" + Property + "\n", "H(3, 4, 5)"),
        ["struct"] = ("struct H:\n" + Members + "\n\n    def __init__(self, a: int, b: int, c: int) -> None:\n        self.n_items = a\n        self.n = b\n        self.`raw_name` = c\n" + Property + "\n", "H(3, 4, 5)"),
    };

    private static readonly string[] MemberNames = { "n_items", "n", "total", "`raw_name`" };
    private static readonly string[] Values = { "3", "4", "6", "5" };

    public static IEnumerable<object[]> HostCells() => Hosts.Keys.Select(h => new object[] { h });

    [Theory]
    [MemberData(nameof(HostCells))]
    public void AttributeField_ResolvesTheEmittedMember(string host)
    {
        var (declaration, construction) = Hosts[host];
        var lines = new List<string>();
        var expected = new List<string>();
        for (var i = 0; i < MemberNames.Length; i++)
        {
            // A format field names the member as python does: the escape is source syntax only.
            var field = MemberNames[i].Trim('`');
            lines.Add($"    print(\"{{0.{field}}}\".format(h))");
            lines.Add($"    print(\"{{0.inner.{field}}}\".format(w))");
            lines.Add($"    print(\"[{{0.{field}:>4}}]\".format(h))");
            expected.Add(Values[i]);
            expected.Add(Values[i]);
            expected.Add("[   " + Values[i] + "]");
        }

        var source = declaration + "\n"
            + "class W:\n    inner: H\n\n    def __init__(self, inner: H) -> None:\n        self.inner = inner\n\n"
            + "def main() -> None:\n"
            + $"    h = {construction}\n"
            + "    w = W(h)\n"
            + string.Join("\n", lines) + "\n"
            + "    try:\n        print(\"{0.missing}\".format(h))\n    except AttributeError as e:\n        print(e)\n";
        expected.Add("'H' object has no attribute 'missing'");

        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"[{host}] {result.StandardError}\n{source}");
        result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n')
            .Should().Equal(expected, $"[{host}]\n{source}");
    }

    /// <summary>The item-access control: a <c>[key]</c> field is not an attribute and never mangled.</summary>
    [Fact]
    public void ItemField_IsUnaffected()
    {
        var result = CompileAndExecute("def main() -> None:\n    print(\"{0[1]}\".format([7, 8]))\n");
        result.Success.Should().BeTrue(result.StandardError);
        result.StandardOutput.Trim().Should().Be("8");
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        HostCells().Should().HaveCount(3);
        MemberNames.Should().HaveCount(4);
    }
}
