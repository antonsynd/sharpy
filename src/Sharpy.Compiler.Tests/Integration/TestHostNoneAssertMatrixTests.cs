using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The None-test matrix under the <c>@test</c> host AND under the ordinary lowering (#2171).
/// Contract: what an <c>is None</c> / <c>== None</c> test means is the operand's RECORDED lowering
/// (OptionalNoneTest, the NoneCheck equality lowering), never the syntactic shape of the test. The
/// <c>@test</c> host's Xunit rewrite and the ordinary <c>sharpyc run</c> lowering must agree.
/// </summary>
/// <remarks>
/// <para>Axes: operand {reference <c>str | None</c>, CLR <c>Nullable&lt;int&gt;</c> from
/// <c>int | None</c>, Optional struct local <c>int?</c>, Optional struct of a reference element
/// <c>str?</c>, Optional struct returned by a call <c>dict.get</c>} × spelling {<c>is None</c>,
/// <c>is not None</c>, <c>== None</c>, <c>!= None</c>} × side {None on the right, None on the left}.</para>
/// <para>The verdict table (<see cref="IsAdmitted"/>) is spelled by hand, not derived: the checker
/// refuses <c>==</c>/<c>!=</c> <c>None</c> on a Nullable or Optional operand with SPY0222 ("did you
/// mean 'is None'?"), and that refusal is pinned here as a recorded outcome.</para>
/// <para>Every admitted cell is executed three ways: the ordinary lowering prints the test's value on
/// an absent and a present subject; the test host runs the assertion where it HOLDS (the test must
/// pass); and where it is VIOLATED (the test must fail with an xUnit assertion exception). The failing
/// direction is what proves the assertion is not vacuous: before #2171 an Optional subject lowered to
/// <c>Xunit.Assert.NotNull(optional)</c>, which a boxed struct can never fail, and
/// <c>None is x</c> on an Optional printed False under <c>sharpyc run</c>.</para>
/// </remarks>
public class TestHostNoneAssertMatrixTests : IntegrationTestBase
{
    public TestHostNoneAssertMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// The test-host assembly calls into Xunit.Assert at run time, so the assertion library must sit
    /// next to it; the shared harness copies only the Sharpy runtime closure.
    /// </summary>
    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(typeof(Xunit.Assert).Assembly.Location);

    public enum Operand { Reference, ClrNullable, OptionalLocal, OptionalOfReference, OptionalCall }

    public enum Spelling { IsNone, IsNotNone, EqNone, NotEqNone }

    public enum Side { NoneRight, NoneLeft }

    /// <summary>Declarations for the subject in the absent / present state.</summary>
    private static string Declaration(Operand operand, bool present) => operand switch
    {
        Operand.Reference => present ? "v: str | None = \"x\"" : "v: str | None = None",
        Operand.ClrNullable => present ? "v: int | None = 5" : "v: int | None = None",
        Operand.OptionalLocal => present ? "v: int? = Some(5)" : "v: int? = None()",
        Operand.OptionalOfReference => present ? "v: str? = Some(\"x\")" : "v: str? = None()",
        Operand.OptionalCall => "d: dict[str, int] = {\"a\": 1}",
        _ => throw new ArgumentOutOfRangeException(nameof(operand)),
    };

    private static string Subject(Operand operand, bool present) => operand switch
    {
        Operand.OptionalCall => present ? "d.get(\"a\")" : "d.get(\"zz\")",
        _ => "v",
    };

    private static string Test(Operand operand, bool present, Spelling spelling, Side side)
    {
        var subject = Subject(operand, present);
        var op = spelling switch
        {
            Spelling.IsNone => "is",
            Spelling.IsNotNone => "is not",
            Spelling.EqNone => "==",
            Spelling.NotEqNone => "!=",
            _ => throw new ArgumentOutOfRangeException(nameof(spelling)),
        };
        return side == Side.NoneRight ? $"{subject} {op} None" : $"None {op} {subject}";
    }

    /// <summary>
    /// Hand-spelled verdicts (measured at the base commit): the identity spellings are admitted on
    /// every operand; the equality spellings only on a reference (the NoneCheck lowering, #901) —
    /// on Nullable and Optional operands they are SPY0222.
    /// </summary>
    private static bool IsAdmitted(Operand operand, Spelling spelling)
        => spelling is Spelling.IsNone or Spelling.IsNotNone || operand == Operand.Reference;

    /// <summary>Whether the test is true for a present (vs absent) subject.</summary>
    private static bool HoldsWhenPresent(Spelling spelling)
        => spelling is Spelling.IsNotNone or Spelling.NotEqNone;

    public static TheoryData<Operand, Spelling, Side> AdmittedCells()
    {
        var data = new TheoryData<Operand, Spelling, Side>();
        foreach (var operand in Enum.GetValues<Operand>())
            foreach (var spelling in Enum.GetValues<Spelling>())
                foreach (var side in Enum.GetValues<Side>())
                    if (IsAdmitted(operand, spelling))
                        data.Add(operand, spelling, side);
        return data;
    }

    public static TheoryData<Operand, Spelling, Side> RefusedCells()
    {
        var data = new TheoryData<Operand, Spelling, Side>();
        foreach (var operand in Enum.GetValues<Operand>())
            foreach (var spelling in Enum.GetValues<Spelling>())
                foreach (var side in Enum.GetValues<Side>())
                    if (!IsAdmitted(operand, spelling))
                        data.Add(operand, spelling, side);
        return data;
    }

    [Fact]
    public void Matrix_CoversEveryCell()
    {
        // Anchored to literals, not to the enums the generators enumerate: 5 operands × 4 spellings
        // × 2 sides, of which the 4 Nullable/Optional operands × 2 equality spellings × 2 sides are
        // refusals.
        AdmittedCells().Count().Should().Be(24);
        RefusedCells().Count().Should().Be(16);
    }

    private static string Indent(string line, int spaces) => new string(' ', spaces) + line;

    /// <summary>The ordinary lowering (not a @test body): the test's value on absent and present subjects.</summary>
    [Theory]
    [MemberData(nameof(AdmittedCells))]
    public void OrdinaryLowering_PrintsThePythonTruthValue(Operand operand, Spelling spelling, Side side)
    {
        var source = string.Join("\n", new[]
        {
            "def check_absent() -> None:",
            Indent(Declaration(operand, present: false), 4),
            Indent($"print({Test(operand, false, spelling, side)})", 4),
            "",
            "def check_present() -> None:",
            Indent(Declaration(operand, present: true), 4),
            Indent($"print({Test(operand, true, spelling, side)})", 4),
            "",
            "def main() -> None:",
            "    check_absent()",
            "    check_present()",
            "",
        });

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join("; ", result.CompilationErrors));
        var presentHolds = HoldsWhenPresent(spelling);
        result.StandardOutput.Trim().Should().Be(
            $"{(presentHolds ? "False" : "True")}\n{(presentHolds ? "True" : "False")}");
    }

    private ExecutionResult RunTestHostCell(Operand operand, Spelling spelling, Side side, bool holds)
    {
        // The subject is in the state that makes the test true (holds) or false (violated).
        var present = holds == HoldsWhenPresent(spelling);
        var source = string.Join("\n", new[]
        {
            "class Cell:",
            "    @test",
            "    def test_cell(self) -> None:",
            Indent(Declaration(operand, present), 8),
            Indent($"assert {Test(operand, present, spelling, side)}", 8),
            "",
            "def main() -> None:",
            "    Cell().test_cell()",
            "    print(\"pass\")",
            "",
        });
        return CompileAndExecute(source);
    }

    /// <summary>Assertion holds → the test host's assertion passes.</summary>
    [Theory]
    [MemberData(nameof(AdmittedCells))]
    public void TestHost_AssertionThatHolds_Passes(Operand operand, Spelling spelling, Side side)
    {
        var result = RunTestHostCell(operand, spelling, side, holds: true);

        result.GeneratedCSharp.Should().Contain("Xunit.Assert.", "the cell must exercise the test-host rewrite");
        result.Success.Should().BeTrue(
            $"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be("pass");
    }

    /// <summary>
    /// Assertion violated → the test FAILS with an xUnit assertion exception. This is the
    /// non-vacuity direction: an assertion that cannot fail passes the holding cell too.
    /// </summary>
    [Theory]
    [MemberData(nameof(AdmittedCells))]
    public void TestHost_AssertionThatIsViolated_Fails(Operand operand, Spelling spelling, Side side)
    {
        var result = RunTestHostCell(operand, spelling, side, holds: false);

        result.GeneratedCSharp.Should().Contain("Xunit.Assert.", "the cell must exercise the test-host rewrite");
        result.CompilationErrors.Should().NotBeEmpty("the violated assertion must stop the program");
        result.StandardOutput.Should().NotContain("pass");
        result.StandardError.Should().Contain("Xunit.Sdk.",
            "the failure must be the xUnit assertion, not a crash elsewhere");
    }

    /// <summary>The equality spellings on a Nullable / Optional operand stay SPY0222 refusals.</summary>
    [Theory]
    [MemberData(nameof(RefusedCells))]
    public void TestHost_EqualityNoneOnValueOperand_IsRefused(Operand operand, Spelling spelling, Side side)
    {
        var present = !HoldsWhenPresent(spelling);
        var source = string.Join("\n", new[]
        {
            "class Cell:",
            "    @test",
            "    def test_cell(self) -> None:",
            Indent(Declaration(operand, present), 8),
            Indent($"assert {Test(operand, present, spelling, side)}", 8),
            "",
            "def main() -> None:",
            "    Cell().test_cell()",
            "",
        });

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation);
    }
}
