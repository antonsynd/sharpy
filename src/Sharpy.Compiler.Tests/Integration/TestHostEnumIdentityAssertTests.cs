using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// <c>assert a is b</c> / <c>assert a is not b</c> under the <c>@test</c> host when an operand is an enum
/// (#2669). The host rewrites an identity assertion to <c>Xunit.Assert.Same</c> / <c>NotSame</c>, which box
/// an int-backed enum: <c>Same</c> could never pass and <c>NotSame</c> could never fail. Contract: the host
/// asserts what the ordinary lowering computes — the recorded enum identity lowering, value equality.
/// </summary>
/// <remarks>
/// Axes: operand pair {same enum variables, variable against a member literal (the type-test spelling),
/// <c>E | None</c> against a variable, <c>object</c> against a variable, a string-backed pair (the
/// reference-singleton control, which keeps <c>Assert.Same</c>)} × operator {<c>is</c>, <c>is not</c>} ×
/// direction {the assertion holds, the assertion is violated}. The violated direction is what proves the
/// assertion is not vacuous.
/// </remarks>
public class TestHostEnumIdentityAssertTests : IntegrationTestBase
{
    public TestHostEnumIdentityAssertTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>The test-host assembly calls into Xunit.Assert at run time; see TestHostNoneAssertMatrixTests.</summary>
    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(typeof(Xunit.Assert).Assembly.Location);

    public enum Pair { SameEnumVariables, VariableAgainstMember, OptionalAgainstVariable, ObjectAgainstVariable, StringEnumVariables }

    /// <summary>The two operand declarations and spellings; <paramref name="same"/> picks equal or different members.</summary>
    private static (string[] Declarations, string Left, string Right) Operands(Pair pair, bool same) => pair switch
    {
        Pair.SameEnumVariables => (
            new[] { "a: Color = Color.RED", $"b: Color = Color.{(same ? "RED" : "GREEN")}" }, "a", "b"),
        Pair.VariableAgainstMember => (
            new[] { $"a: Color = Color.{(same ? "RED" : "GREEN")}" }, "a", "Color.RED"),
        Pair.OptionalAgainstVariable => (
            new[] { "a: Color | None = Color.RED", $"b: Color = Color.{(same ? "RED" : "GREEN")}" }, "a", "b"),
        Pair.ObjectAgainstVariable => (
            new[] { "a: object = Color.RED", $"b: Color = Color.{(same ? "RED" : "GREEN")}" }, "a", "b"),
        Pair.StringEnumVariables => (
            new[] { "a: Tone = Tone.A", $"b: Tone = Tone.{(same ? "A" : "B")}" }, "a", "b"),
        _ => throw new ArgumentOutOfRangeException(nameof(pair)),
    };

    public static TheoryData<Pair, bool> Cells()
    {
        var data = new TheoryData<Pair, bool>();
        foreach (var pair in Enum.GetValues<Pair>())
            foreach (var negated in new[] { false, true })
                data.Add(pair, negated);
        return data;
    }

    private ExecutionResult RunCell(Pair pair, bool negated, bool holds)
    {
        // `is` holds for the same member, `is not` for different members.
        var (declarations, left, right) = Operands(pair, same: holds != negated);
        var lines = new List<string>
        {
            "enum Color:",
            "    RED = 1",
            "    GREEN = 2",
            "",
            "enum Tone:",
            "    A = \"a\"",
            "    B = \"b\"",
            "",
            "class Cell:",
            "    @test",
            "    def test_cell(self) -> None:",
        };
        lines.AddRange(declarations.Select(d => "        " + d));
        lines.Add($"        assert {left} {(negated ? "is not" : "is")} {right}");
        lines.AddRange(new[] { "", "def main() -> None:", "    Cell().test_cell()", "    print(\"pass\")", "" });
        return CompileAndExecute(string.Join("\n", lines));
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void AssertionThatHolds_Passes(Pair pair, bool negated)
    {
        var result = RunCell(pair, negated, holds: true);

        result.GeneratedCSharp.Should().Contain("Xunit.Assert.", "the cell must exercise the test-host rewrite");
        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be("pass");
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void AssertionThatIsViolated_Fails(Pair pair, bool negated)
    {
        var result = RunCell(pair, negated, holds: false);

        result.GeneratedCSharp.Should().Contain("Xunit.Assert.", "the cell must exercise the test-host rewrite");
        result.CompilationErrors.Should().NotBeEmpty("the violated assertion must stop the program");
        result.StandardOutput.Should().NotContain("pass");
        result.StandardError.Should().Contain("Xunit.Sdk.", "the failure must be the xUnit assertion, not a crash elsewhere");
    }
}
