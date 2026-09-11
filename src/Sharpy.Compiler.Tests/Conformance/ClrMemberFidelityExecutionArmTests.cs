using System.Collections.Generic;
using System.Linq;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The reachable half of <see cref="ClrMemberFidelityMatrixTests"/>'s SPY0908 arm (#1837).
///
/// <para>
/// That harness compiles through <c>CompilerApi.Compile</c> with no output assembly, so the
/// generated C# never reaches Roslyn: its <c>Expect.Compiles</c> means "no SEMANTIC diagnostic", and
/// its SPY0908 check cannot fire at all. A member typed wrongly enough to produce CS1503 is green
/// there and red under <c>run</c>. The cells whose failure mode is a C#-level mismatch therefore
/// have to go through a harness that EMITS, which is this one.
/// </para>
///
/// <para>
/// <see cref="TheSpy0908Arm_CanFire"/> is the positive control, and nothing below it means anything
/// without it: an absence assertion over a channel that never carries the string passes for the
/// wrong reason, which is the whole of #1827 and #1837.
/// </para>
/// </summary>
public class ClrMemberFidelityExecutionArmTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public ClrMemberFidelityExecutionArmTests(ITestOutputHelper output) : base(output) => _output = output;

    /// <summary>
    /// The probe is capable of hitting: a program whose generated C# genuinely fails to compile
    /// reports SPY0908 on BOTH surfaces the fixture harness reads — <c>RawDiagnostics</c> (populated
    /// on the emit-failure paths) and <c>CompilationErrors</c> (rendered from the mapped diagnostics
    /// since #1827; it used to carry Roslyn's own text, in which the string "SPY0908" never appears).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void TheSpy0908Arm_CanFire()
    {
        // `list(x)` on a non-iterable falls back to Unknown type arguments -> CS0305.
        var result = CompileAndExecute("def main() -> None:\n    x: int = 42\n    items = list(x)\n    print(items)\n");

        Assert.False(result.Success);

        Assert.Contains(result.RawDiagnostics,
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);

        var joined = string.Join("\n", result.CompilationErrors);
        _output.WriteLine(joined);
        Assert.Contains("SPY0908", joined, System.StringComparison.Ordinal);

        // Roslyn's own id and wording survive inside the mapped message, which is what keeps a
        // sidecar that names a CS code matching.
        Assert.Contains("CS0305", joined, System.StringComparison.Ordinal);
    }

    public static TheoryData<string, string> MemberCells() => new()
    {
        // enum member read, compared against a member of the same enum (#1705).
        {
            "enum.compare",
            "from system import DateTime\nfrom system import DayOfWeek\n\n"
            + "def main() -> None:\n    print(DateTime(2020, 1, 6).day_of_week == DayOfWeek.Monday)\n"
        },
        // char member read projected to str (#1291).
        {
            "char.projection",
            "from system.io import Path\n\ndef main() -> None:\n"
            + "    print(Path.directory_separator_char == \"/\")\n"
        },
        // bare type-parameter member: still NOT nullable, so the value flows into a `str` slot.
        {
            "bare-T.not-nullable",
            "def main() -> None:\n    xs: list[str] = [\"a\"]\n    s: str = xs.pop(0)\n    print(s)\n"
        },
        // ANNOTATED type-parameter member: `T?` on a generic definition (#1828). The value is
        // `str | None`, which is the loose channel and still reaches a `str` slot at runtime.
        {
            "annotated-T.nullable",
            "def main() -> None:\n    xs: list[str] = [\"a\"]\n    v: str | None = xs.first_or_default()\n    print(v)\n"
        },
        // NESTED nullable type argument (#1847): the member's own type, read in a position that does
        // not cross the container-materialization gap #1866.
        {
            "nested-arg.member-read",
            "from system.diagnostics import ProcessStartInfo\n\ndef main() -> None:\n"
            + "    p = ProcessStartInfo()\n    print(p.argument_list.count)\n"
        }
    };

    /// <summary>
    /// Each cell REACHES Roslyn and compiles: the member's recorded type described the value well
    /// enough for the emitted C# to bind. This is the assertion the matrix harness cannot make.
    /// </summary>
    [Theory]
    [MemberData(nameof(MemberCells))]
    [Trait("Category", "Conformance")]
    public void MemberTypedFaithfully_TheGeneratedCSharpCompiles(string label, string source)
    {
        var result = CompileAndExecute(source);

        var ice = result.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError)
            .ToList();

        Assert.True(ice.Count == 0,
            $"{label}: reached Roslyn and failed — {string.Join(" | ", ice.Select(d => d.Message))}");

        Assert.True(result.Success,
            $"{label}: did not run — {string.Join(" | ", result.CompilationErrors.Take(3))}");
    }
}
