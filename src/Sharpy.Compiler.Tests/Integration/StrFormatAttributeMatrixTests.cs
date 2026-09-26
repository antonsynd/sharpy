using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
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
/// Cells: host {class, dataclass, struct, union case} × member {snake_case field <c>n_items</c>,
/// single-word field <c>n</c>, property <c>total</c>, backtick-escaped field <c>`raw_name`</c>, an
/// indexed chain through a snake_case field <c>my_list[1]</c>} × chain {<c>{0.m}</c>, through another
/// object <c>{0.inner.m}</c>, with a spec <c>{0.m:&gt;4}</c>}; plus the missing-member
/// AttributeError, the <c>{0[1]}</c> item control and the recorded CLR-spelling leak. A union body
/// declares no property (the parser refuses one, pinned below), so the union-case host has no
/// <c>total</c> cell. Stdlib members (<c>{0.year}</c>, <c>{0.days}</c>) are cells of
/// <c>StrFormatAttributeStdlibTests</c>, where the stdlib resolves. Expected values are python3's on
/// the same program (the union case's twin: a class with the same attributes).
/// </remarks>
public class StrFormatAttributeMatrixTests : IntegrationTestBase
{
    public StrFormatAttributeMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Members = """
            n_items: int
            n: int
            `raw_name`: int
            my_list: list[int]
        """;

    private const string Property = """

            property get total(self) -> int:
                return 6
        """;

    private static readonly Dictionary<string, (string Declaration, string Type, string Construction)> Hosts = new()
    {
        ["class"] = ("class H:\n" + Members + "\n\n    def __init__(self) -> None:\n        self.n_items = 3\n        self.n = 4\n        self.`raw_name` = 5\n        self.my_list = [1, 2]\n" + Property + "\n", "H", "H()"),
        ["dataclass"] = ("@dataclass\nclass H:\n" + Members + "\n" + Property + "\n", "H", "H(3, 4, 5, [1, 2])"),
        ["struct"] = ("struct H:\n" + Members + "\n\n    def __init__(self, a: int, b: int, c: int, d: list[int]) -> None:\n        self.n_items = a\n        self.n = b\n        self.`raw_name` = c\n        self.my_list = d\n" + Property + "\n", "H", "H(3, 4, 5, [1, 2])"),
        ["union_case"] = ("union U:\n    case H(n_items: int, n: int, `raw_name`: int, my_list: list[int])\n", "U", "U.H(3, 4, 5, [1, 2])"),
    };

    // A format field names the member as python does: the escape is source syntax only.
    private static readonly (string Field, string Value)[] MemberFields =
    {
        ("n_items", "3"), ("n", "4"), ("total", "6"), ("raw_name", "5"), ("my_list[1]", "2"),
    };

    // The one host × member cell with no program: a union declares no property.
    private static bool HasCell(string host, string field) => !(host == "union_case" && field == "total");

    public static IEnumerable<object[]> HostCells() => Hosts.Keys.Select(h => new object[] { h });

    [Theory]
    [MemberData(nameof(HostCells))]
    public void AttributeField_ResolvesTheEmittedMember(string host)
    {
        var (declaration, type, construction) = Hosts[host];
        var lines = new List<string>();
        var expected = new List<string>();
        foreach (var (field, value) in MemberFields.Where(m => HasCell(host, m.Field)))
        {
            // Each cell reports on its own line, so one unresolved member cannot mask the rest.
            lines.Add(Cell($"\"{{0.{field}}}\".format(h)"));
            lines.Add(Cell($"\"{{0.inner.{field}}}\".format(w)"));
            lines.Add(Cell($"\"[{{0.{field}:>4}}]\".format(h)"));
            expected.Add(value);
            expected.Add(value);
            expected.Add("[   " + value + "]");
        }

        var source = declaration + "\n"
            + $"class W:\n    inner: {type}\n\n    def __init__(self, inner: {type}) -> None:\n        self.inner = inner\n\n"
            + "def main() -> None:\n"
            + $"    h = {construction}\n"
            + "    w = W(h)\n"
            + string.Join("", lines)
            + Cell("\"{0.missing}\".format(h)");
        expected.Add("AttributeError: 'H' object has no attribute 'missing'");

        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue($"[{host}] {result.StandardError}\n{source}");
        result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n')
            .Should().Equal(expected, $"[{host}]\n{source}");
    }

    private static string Cell(string expression) =>
        $"    try:\n        print({expression})\n    except AttributeError as e:\n        print(\"AttributeError:\", e)\n";

    /// <summary>
    /// The exemption above is falsifiable: a property in a union body is a parse error today. When a
    /// union can declare one, this fails and the union-case host gains its <c>total</c> cell.
    /// </summary>
    [Fact]
    public void UnionBody_DeclaresNoProperty()
    {
        var result = CompileAndExecute("union U:\n    case H(n: int)\n\n    property get total(self) -> int:\n        return 6\n\n"
            + "def main() -> None:\n    print(\"{0.total}\".format(U.H(1)))\n");
        result.Success.Should().BeFalse(result.StandardOutput);
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Parser.ExpectedToken);
    }

    /// <summary>
    /// The recorded deviation <c>str-format-attr-clr-spelling-leak</c> (docs/deviations.yaml): the
    /// forward rule is not injective, so a field spelled as the member's emitted CLR name finds it —
    /// <c>{0.N}</c>, <c>{0.NItems}</c> and <c>{0.MyList}</c> print the value where python3 raises
    /// <c>AttributeError: 'H' object has no attribute 'N'</c>. A spelling that is neither the python
    /// name nor the emitted one still raises (the control).
    /// </summary>
    [Fact]
    public void ClrSpelledField_IsTheRecordedLeak()
    {
        var result = CompileAndExecute("class H:\n    n: int\n    n_items: int\n    my_list: list[int]\n\n"
            + "    def __init__(self) -> None:\n        self.n = 4\n        self.n_items = 3\n        self.my_list = [1, 2]\n\n"
            + "def main() -> None:\n    h = H()\n"
            + Cell("\"{0.N}\".format(h)")
            + Cell("\"{0.NItems}\".format(h)")
            + Cell("\"{0.MyList[1]}\".format(h)")
            + Cell("\"{0.Missing}\".format(h)"));
        result.Success.Should().BeTrue(result.StandardError);
        result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n')
            .Should().Equal("4", "3", "2", "AttributeError: 'H' object has no attribute 'Missing'");
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
        HostCells().Should().HaveCount(4);
        MemberFields.Should().HaveCount(5);
        Hosts.Keys.SelectMany(h => MemberFields.Where(m => !HasCell(h, m.Field))).Should().HaveCount(1);
    }
}
