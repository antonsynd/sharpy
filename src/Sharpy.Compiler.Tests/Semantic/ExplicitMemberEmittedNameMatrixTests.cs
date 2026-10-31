using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// An explicit interface implementation claims no bare name at the emitted-name collision validators
/// (#2399, R-JX; R-JH's rule, #2390). C# keeps <c>int IShape.Area</c> out of the type's member
/// declaration space — no CS0102 against a sibling <c>Area</c>, no CS0542 inside a type named
/// <c>Area</c> — so SPY0522 / SPY0525 must not count it. P21h re-keyed by-name LOOKUPS to
/// <c>(Name, ExplicitInterface)</c>; the emitted-name walk is a projection and was outside that census.
/// </summary>
/// <remarks>
/// Closed axes: host {class, struct} × colliding sibling {nested type <c>Area</c>, method <c>Area</c>,
/// field <c>Area</c>, the enclosing type named <c>Area</c>} × order {property first, sibling first}
/// (one order for the enclosing type) × member {explicit auto get half <c>property get IShape.area:
/// int = 4</c>, explicit function-style getter, bare <c>property area: int = 4</c>} = 42 cells. Explicit
/// cells run and print the property through the interface; the bare twins are the positive control —
/// the same program with a bare property still draws SPY0522 / SPY0525 at the later declaration.
/// Measured @ 2c42408b9 before the fix: every explicit cell SPY0522 / SPY0525.
/// <para>The explicit spellings match the interface's get-only member. The issue's own spelling, a
/// get+SET auto property <c>property IShape.area: int = 4</c>, is CS0550 behind SPY0908 with no
/// sibling at all — an explicit implementation may not add an accessor the interface lacks; that
/// is the interface-member tracker's (#2383), not this projection's.</para>
/// <para>Mutation (recorded in the commit body): yield explicit properties again in
/// <c>EnumerateMemberNames</c> → the explicit cells red, the bare cells stay green.</para>
/// </remarks>
public class ExplicitMemberEmittedNameMatrixTests : IntegrationTestBase
{
    public ExplicitMemberEmittedNameMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string Interface = "interface IShape:\n    property get area(self) -> int\n\n";

    public static IEnumerable<object[]> Cells() =>
        from host in new[] { "class", "struct" }
        from sibling in new[] { "nested_type", "method", "field", "enclosing" }
        from order in sibling == "enclosing" ? new[] { "only" } : new[] { "property_first", "sibling_first" }
        from member in new[] { "explicit_auto", "explicit_function", "bare" }
        select new object[] { host, sibling, order, member };

    private static string Indent(string s) => string.Join("\n", s.Split('\n').Select(l => l.Length == 0 ? l : "    " + l));

    private static string CellSource(string host, string sibling, string order, string member)
    {
        string property = member switch
        {
            "explicit_auto" => "property get IShape.area: int = 4\n",
            "explicit_function" => "property get IShape.area(self) -> int:\n    return 4\n",
            "bare" => "property area: int = 4\n",
            _ => throw new ArgumentOutOfRangeException(nameof(member), member, null),
        };
        string? siblingText = sibling switch
        {
            "nested_type" => "class Area:\n    tag: int = 9\n",
            "method" => "def Area(self) -> int:\n    return 9\n",
            "field" => "Area: int = 9\n",
            "enclosing" => null,
            _ => throw new ArgumentOutOfRangeException(nameof(sibling), sibling, null),
        };
        string typeName = sibling == "enclosing" ? "Area" : "C";
        var members = siblingText is null ? new[] { property }
            : order == "property_first" ? new[] { property, siblingText } : new[] { siblingText, property };
        return Interface + $"{host} {typeName}(IShape):\n" + string.Join("\n", members.Select(Indent))
            + $"\ndef main() -> None:\n    s: IShape = {typeName}()\n    print(s.area)\n";
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void ExplicitInterfaceMember_ClaimsNoBareEmittedName(string host, string sibling, string order, string member)
    {
        var cell = $"{host} × {sibling} × {order} × {member}";
        var result = CompileAndExecute(CellSource(host, sibling, order, member));
        var all = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code} {d.Line}:{d.Column} {d.Message}"));
        var errors = result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();

        errors.Select(d => d.Code).Should().NotContain(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, $"[{cell}] never SPY0908");

        if (member != "bare")
        {
            result.Success.Should().BeTrue($"[{cell}] C# has no clash with an explicit implementation: {all}");
            result.StandardOutput.Trim().Should().Be("4", $"[{cell}]");
        }
        else
        {
            var expected = sibling == "enclosing"
                ? DiagnosticCodes.CodeGen.MemberEnclosingTypeCollision
                : DiagnosticCodes.CodeGen.MemberNameCollision;
            result.Success.Should().BeFalse($"[{cell}] the bare property does claim 'Area'");
            errors.Select(d => d.Code).Should().Equal(new[] { expected }, $"[{cell}] {all}");
        }
    }

    /// <summary>
    /// The position half of the projection (<c>FindMemberPosition</c>): with an explicit
    /// <c>IShape.area</c> written BEFORE a bare <c>area</c> and a nested <c>class Area</c>, the SPY0522
    /// names the bare property — line 6 — as the first declaration, never the explicit one on line 5.
    /// </summary>
    [Fact]
    public void CollisionPosition_NamesTheBareMember_NotAnEarlierExplicitOne()
    {
        var source = Interface
            + "class C(IShape):\n"
            + "    property get IShape.area: int = 4\n"
            + "    property area: int = 5\n"
            + "\n"
            + "    class Area:\n"
            + "        tag: int = 9\n"
            + "\ndef main() -> None:\n    print(C().area)\n";
        var result = CompileAndExecute(source);
        var all = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code} {d.Line}:{d.Column} {d.Message}"));

        var error = result.RawDiagnostics.Should().ContainSingle(d => d.Severity == CompilerDiagnosticSeverity.Error, all).Subject;
        error.Code.Should().Be(DiagnosticCodes.CodeGen.MemberNameCollision, all);
        // The collision is the nested class (line 8) against the bare property (line 6).
        error.Line.Should().Be(8, all);
        error.Message.Should().Contain("line 6", all).And.NotContain("line 5", all);
    }
}
