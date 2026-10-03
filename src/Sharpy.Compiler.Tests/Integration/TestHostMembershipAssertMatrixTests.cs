using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The membership half of the #2171 matrix (#2174). Contract: the <c>@test</c> host's <c>assert</c>
/// rewrites lower to the same semantics as the ordinary lowering of the same condition — an arm must
/// consume the facts the ordinary lowering reads, not the operand's syntactic shape. Before #2174 the
/// <c>in</c> / <c>not in</c> arm emitted <c>Xunit.Assert.Contains(x, c)</c> / <c>DoesNotContain</c>
/// straight from the AST: a tuple skipped the #1771 iterable projection (CS1503), a dict was ambiguous
/// between xUnit's two dictionary overloads (CS0121), both surfacing as SPY0908; and a user class whose
/// <c>__contains__</c> and <c>__iter__</c> disagree was searched by enumeration, so a violated
/// <c>assert 5 in bag</c> PASSED. Ruling R-EE: every container kind lowers to
/// <c>Xunit.Assert.True(&lt;the ordinary membership lowering&gt;)</c>, and the emitted test file carries
/// <c>#pragma warning disable xUnit2009, xUnit2017</c>.
/// </summary>
/// <remarks>
/// <para>Axes: container {list, tuple, dict, set, str, user class with <c>__contains__</c>, CLR
/// collection} × spelling {<c>in</c>, <c>not in</c>}. The user class also iterates — <c>__iter__</c>
/// yields 5 while <c>__contains__</c> answers <c>x == 1</c> — so its absent needle (5) tells python3's
/// <c>__contains__</c> dispatch apart from an enumeration-based assertion.</para>
/// <para>Every cell is executed three ways, as <see cref="TestHostNoneAssertMatrixTests"/> does: the
/// ordinary lowering prints the test's value for a present and an absent needle (python3's output); the
/// test host runs the assertion where it HOLDS (the test must pass) and where it is VIOLATED (the test
/// must fail with an xUnit assertion exception — the non-vacuity direction).</para>
/// </remarks>
[Collection("HeavyCompilation")]
public class TestHostMembershipAssertMatrixTests : IntegrationTestBase
{
    public TestHostMembershipAssertMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(typeof(Xunit.Assert).Assembly.Location);

    public enum Container { List, Tuple, Dict, Set, Str, UserContains, ClrCollection }

    public enum Spelling { In, NotIn }

    private const string Prelude =
        "from system.collections.generic import HashSet\n\n"
        + "class Bag:\n"
        + "    def __contains__(self, x: int) -> bool:\n"
        + "        return x == 1\n\n"
        + "    def __iter__(self) -> Iterator[int]:\n"
        + "        return iter([5])\n\n";

    /// <summary>Statements that bind the container <c>c</c>.</summary>
    private static string[] Declaration(Container container) => container switch
    {
        Container.List => new[] { "c: list[int] = [1, 2]" },
        Container.Tuple => new[] { "c: tuple[int, int] = (1, 2)" },
        Container.Dict => new[] { "c: dict[str, int] = {\"a\": 1}" },
        Container.Set => new[] { "c: set[int] = {1, 2}" },
        Container.Str => new[] { "c: str = \"abc\"" },
        Container.UserContains => new[] { "c: Bag = Bag()" },
        Container.ClrCollection => new[] { "c: HashSet[int] = HashSet[int]()", "c.add(1)", "c.add(2)" },
        _ => throw new ArgumentOutOfRangeException(nameof(container)),
    };

    /// <summary>A needle the container holds, and one it does not (python3: <c>in</c> is True / False).</summary>
    private static string Needle(Container container, bool present) => container switch
    {
        Container.Dict => present ? "\"a\"" : "\"b\"",
        Container.Str => present ? "\"b\"" : "\"z\"",
        Container.UserContains => present ? "1" : "5",
        _ => present ? "1" : "3",
    };

    private static string Test(Container container, bool present, Spelling spelling)
        => $"{Needle(container, present)} {(spelling == Spelling.In ? "in" : "not in")} c";

    public static TheoryData<Container, Spelling> Cells()
    {
        var data = new TheoryData<Container, Spelling>();
        foreach (var container in Enum.GetValues<Container>())
            foreach (var spelling in Enum.GetValues<Spelling>())
                data.Add(container, spelling);
        return data;
    }

    [Fact]
    public void Matrix_CoversEveryCell()
    {
        // Anchored to literals: 7 containers × 2 spellings.
        Cells().Count().Should().Be(14);
    }

    private static string Indent(string line, int spaces) => new string(' ', spaces) + line;

    /// <summary>The ordinary lowering prints python3's value for a present and an absent needle.</summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void OrdinaryLowering_PrintsThePythonTruthValue(Container container, Spelling spelling)
    {
        var lines = new List<string> { "def main() -> None:" };
        lines.AddRange(Declaration(container).Select(l => Indent(l, 4)));
        lines.Add(Indent($"print({Test(container, true, spelling)})", 4));
        lines.Add(Indent($"print({Test(container, false, spelling)})", 4));

        var result = CompileAndExecute(Prelude + string.Join("\n", lines) + "\n");

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be(spelling == Spelling.In ? "True\nFalse" : "False\nTrue");
    }

    private ExecutionResult RunTestHostCell(Container container, Spelling spelling, bool holds)
    {
        // `in` holds for a present needle; `not in` for an absent one.
        var present = holds == (spelling == Spelling.In);
        var lines = new List<string> { "class Cell:", "    @test", "    def test_cell(self) -> None:" };
        lines.AddRange(Declaration(container).Select(l => Indent(l, 8)));
        lines.Add(Indent($"assert {Test(container, present, spelling)}", 8));
        lines.Add("");
        lines.Add("def main() -> None:");
        lines.Add("    Cell().test_cell()");
        lines.Add("    print(\"pass\")");
        return CompileAndExecute(Prelude + string.Join("\n", lines) + "\n");
    }

    /// <summary>Assertion holds → the test host's assertion passes.</summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void TestHost_AssertionThatHolds_Passes(Container container, Spelling spelling)
    {
        var result = RunTestHostCell(container, spelling, holds: true);

        result.GeneratedCSharp.Should().Contain("Xunit.Assert.", "the cell must exercise the test-host rewrite");
        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be("pass");
    }

    /// <summary>
    /// Assertion violated → the test FAILS with an xUnit assertion exception (non-vacuity direction).
    /// </summary>
    [Theory]
    [MemberData(nameof(Cells))]
    public void TestHost_AssertionThatIsViolated_Fails(Container container, Spelling spelling)
    {
        var result = RunTestHostCell(container, spelling, holds: false);

        result.GeneratedCSharp.Should().Contain("Xunit.Assert.", "the cell must exercise the test-host rewrite");
        result.CompilationErrors.Should().NotBeEmpty("the violated assertion must stop the program");
        result.CompilationErrors.Should().NotContain(e => e.Contains("SPY0908"), "the program must compile");
        result.StandardOutput.Should().NotContain("pass");
        result.StandardError.Should().Contain("Xunit.Sdk.",
            "the failure must be the xUnit assertion, not a crash elsewhere");
    }

    private const string XunitMembershipPragma = "#pragma warning disable xUnit2009, xUnit2017";

    /// <summary>
    /// A file that emitted a test-host assertion carries the xUnit analyzer suppression (R-EE): its
    /// <c>Assert.True(c.Contains(x))</c> is what xUnit2017/xUnit2009 flag, and an analyzer-enabled,
    /// warnings-as-errors test project would not build without it.
    /// </summary>
    [Fact]
    public void TestHostFile_CarriesTheXunitMembershipSuppression()
    {
        var result = RunTestHostCell(Container.List, Spelling.In, holds: true);

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.GeneratedCSharp.Should().Contain(XunitMembershipPragma);
    }

    /// <summary>
    /// A file with no test-host assertion carries no xUnit suppression — ordinary output names no
    /// analyzer of a framework it does not reference. The positive control is
    /// <see cref="TestHostFile_CarriesTheXunitMembershipSuppression"/>: the same harness, the same
    /// spelling probe, on a file that does emit one.
    /// </summary>
    [Fact]
    public void NonTestFile_CarriesNoXunitSuppression()
    {
        var result = CompileAndExecute(
            "def main() -> None:\n    c: list[int] = [1, 2]\n    assert 1 in c\n    print(\"pass\")\n");

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.GeneratedCSharp.Should().NotContain("Xunit.Assert.", "the program has no @test function");
        result.GeneratedCSharp.Should().NotContain("xUnit2009");
        result.GeneratedCSharp.Should().NotContain("xUnit2017");
    }
}
