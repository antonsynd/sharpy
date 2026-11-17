using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// <c>x in y</c> on a CLR-backed container takes the needles the written call <c>y.Contains(x)</c>
/// takes (plan-4e2867 D3, #2647): a needle C# converts into a <c>Contains</c> formal by
/// <c>op_Implicit</c> RUNS, and one nothing converts is refused by the checker (SPY0222), never by
/// Roslyn (SPY0908). The analysis-level rows are in <c>OptionalNeedleMembershipTests</c>; this class
/// executes them.
/// </summary>
/// <remarks>
/// Not a <c>.spy</c> fixture: <c>RectangleF</c> lives in System.Drawing.Primitives, which the shared
/// fixture harness does not reference (<c>sharpyc run</c> does).
/// </remarks>
public class ClrMembershipCallConversionTests : IntegrationTestBase
{
    public ClrMembershipCallConversionTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(typeof(System.Drawing.PointF).Assembly.Location);

    private const string Prelude =
        "from System.Drawing import Point, PointF, Rectangle, RectangleF\n\n"
        + "def main() -> None:\n"
        + "    rf = RectangleF(0.0f, 0.0f, 10.0f, 10.0f)\n"
        + "    r = Rectangle(0, 0, 10, 10)\n";

    [Fact]
    public void ImplicitlyConvertedNeedles_Run_AndAnswerAsTheWrittenCallDoes()
    {
        var source = Prelude
            + "    print(Point(1, 1) in rf)\n"
            + "    print(rf.Contains(Point(1, 1)))\n"
            + "    print(Point(50, 50) in rf)\n"
            + "    print(Point(1, 1) not in rf)\n"
            + "    print(Rectangle(1, 1, 2, 2) in rf)\n"
            + "    print(Rectangle(1, 1, 50, 50) in rf)\n"
            + "    print(PointF(1.0f, 1.0f) in rf)\n"
            + "    print(Point(1, 1) in r)\n"
            + "    print(Rectangle(1, 1, 2, 2) in r)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Split('\n').Select(line => line.Trim())
            .Should().Equal("True", "True", "False", "False", "True", "False", "True", "True", "True");
    }

    /// <summary>The refusal's direction: the checker's own code, not Roslyn's CS1503 behind SPY0908.
    /// The positive control of the test above — these stay refused with and without the conversion
    /// arm.</summary>
    [Theory]
    [InlineData("\"x\" in rf")]
    [InlineData("1 in rf")]
    [InlineData("None in rf")]
    [InlineData("PointF(1.0f, 1.0f) in r")]
    public void NeedleNothingConverts_IsRefusedByTheChecker(string expression)
    {
        var result = CompileAndExecute(Prelude + $"    print({expression})\n");

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Where(d => d.IsError)
            .Should().ContainSingle()
            .Which.Code.Should().Be(DiagnosticCodes.Semantic.InvalidBinaryOperation);
    }
}
