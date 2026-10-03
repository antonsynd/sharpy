using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The result type of every arithmetic operator is the spec table's (#2189,
/// <c>docs/language_specification/arithmetic_operators.md</c>): any <c>float32</c> operand with no
/// <c>float64</c>/<c>decimal</c> one yields <c>float32</c>, a <c>float64</c> operand yields <c>float64</c>.
/// Before #2189, <c>/</c> and <c>**</c> typed every float32 promotion as <c>float64</c> (SPY0220 at a
/// float32 store) while <c>+ - * // %</c> typed it float32.
/// </summary>
/// <remarks>
/// <para>Axes: operator {<c>+ - * / // % **</c>} × operand pair {f32·f32, f32·int, int·f32, f32·f64,
/// CLR <c>float</c> property (<c>PointF.X</c>) · f32}. Each cell asserts the TYPE (the expected width's
/// slot accepts the expression; a float64 cell's float32 slot refuses it with SPY0220) and the VALUE:
/// the program prints <c>float64(r)</c>, and the expected string is python3's, with every float32
/// operand and result rounded through <c>struct.pack('f')</c>. Operands <c>2.0f</c> and <c>0.3f</c> make a
/// float32 computation print differently from a float64 one (<c>2.0f + 0.3f</c> is
/// <c>2.299999952316284</c> in float32, <c>2.300000011920929</c> in float64), so the value pins that the
/// emitted C# computes in float, not only that the checker says so.</para>
/// </remarks>
public class Float32ArithmeticMatrixTests : IntegrationTestBase
{
    public Float32ArithmeticMatrixTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// <c>PointF</c> is forwarded to System.Drawing.Primitives, which the shared harness does not
    /// reference (<c>sharpyc run</c> does: it compiles against the whole shared framework).
    /// </summary>
    protected override IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => base.GetAdditionalReferenceAssemblyPaths().Append(typeof(System.Drawing.PointF).Assembly.Location);

    public enum Pair { F32F32, F32Int, IntF32, F32F64, ClrPropertyF32 }

    private static readonly string[] Operators = { "+", "-", "*", "/", "//", "%", "**" };

    private const string Prelude =
        "from system.drawing import PointF\n\n"
        + "def main() -> None:\n"
        + "    a: float32 = 2.0f\n"
        + "    b: float32 = 0.3f\n"
        + "    i: int = 3\n"
        + "    d: float64 = 0.3\n"
        + "    p: PointF = PointF(2.0f, 0.3f)\n";

    private static string Expression(Pair pair, string op) => pair switch
    {
        Pair.F32F32 => $"a {op} b",
        Pair.F32Int => $"a {op} i",
        Pair.IntF32 => $"i {op} a",
        Pair.F32F64 => $"a {op} d",
        Pair.ClrPropertyF32 => $"p.x {op} b",
        _ => throw new ArgumentOutOfRangeException(nameof(pair)),
    };

    /// <summary>The spec row: float32 unless a float64 operand is present.</summary>
    private static bool IsFloat32(Pair pair) => pair != Pair.F32F64;

    /// <summary>python3 values (float32 rounding via struct.pack('f')), hand-recorded per cell.</summary>
    private static readonly Dictionary<(Pair, string), string> Expected = new()
    {
        [(Pair.F32F32, "+")] = "2.299999952316284",
        [(Pair.F32F32, "-")] = "1.7000000476837158",
        [(Pair.F32F32, "*")] = "0.6000000238418579",
        [(Pair.F32F32, "/")] = "6.666666507720947",
        [(Pair.F32F32, "//")] = "6.0",
        [(Pair.F32F32, "%")] = "0.19999992847442627",
        [(Pair.F32F32, "**")] = "1.2311444282531738",
        [(Pair.F32Int, "+")] = "5.0",
        [(Pair.F32Int, "-")] = "-1.0",
        [(Pair.F32Int, "*")] = "6.0",
        [(Pair.F32Int, "/")] = "0.6666666865348816",
        [(Pair.F32Int, "//")] = "0.0",
        [(Pair.F32Int, "%")] = "2.0",
        [(Pair.F32Int, "**")] = "8.0",
        [(Pair.IntF32, "+")] = "5.0",
        [(Pair.IntF32, "-")] = "1.0",
        [(Pair.IntF32, "*")] = "6.0",
        [(Pair.IntF32, "/")] = "1.5",
        [(Pair.IntF32, "//")] = "1.0",
        [(Pair.IntF32, "%")] = "1.0",
        [(Pair.IntF32, "**")] = "9.0",
        [(Pair.F32F64, "+")] = "2.3",
        [(Pair.F32F64, "-")] = "1.7",
        [(Pair.F32F64, "*")] = "0.6",
        [(Pair.F32F64, "/")] = "6.666666666666667",
        [(Pair.F32F64, "//")] = "6.0",
        [(Pair.F32F64, "%")] = "0.20000000000000007",
        [(Pair.F32F64, "**")] = "1.2311444133449163",
        [(Pair.ClrPropertyF32, "+")] = "2.299999952316284",
        [(Pair.ClrPropertyF32, "-")] = "1.7000000476837158",
        [(Pair.ClrPropertyF32, "*")] = "0.6000000238418579",
        [(Pair.ClrPropertyF32, "/")] = "6.666666507720947",
        [(Pair.ClrPropertyF32, "//")] = "6.0",
        [(Pair.ClrPropertyF32, "%")] = "0.19999992847442627",
        [(Pair.ClrPropertyF32, "**")] = "1.2311444282531738",
    };

    public static TheoryData<Pair, string> Cells()
    {
        var data = new TheoryData<Pair, string>();
        foreach (var pair in Enum.GetValues<Pair>())
            foreach (var op in Operators)
                data.Add(pair, op);
        return data;
    }

    [Fact]
    public void Matrix_CoversEveryCell()
    {
        // Anchored to literals: 5 pairs × 7 operators, every one with a recorded python3 value.
        Cells().Count().Should().Be(35);
        Expected.Count.Should().Be(35);
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Cell_HasTheSpecWidth_AndComputesInIt(Pair pair, string op)
    {
        var width = IsFloat32(pair) ? "float32" : "float64";
        var source = Prelude
            + $"    r: {width} = {Expression(pair, op)}\n"
            + "    print(float64(r))\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be(Expected[(pair, op)]);
    }

    /// <summary>A float64 cell is NOT float32: its float32 slot refuses it (the type probe's positive control).</summary>
    [Theory]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("*")]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData("%")]
    [InlineData("**")]
    public void Float64Cell_IsRefusedAtAFloat32Slot(string op)
    {
        var result = CompileAndExecute(Prelude + $"    r: float32 = {Expression(Pair.F32F64, op)}\n    print(r)\n");

        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch);
    }

    /// <summary>The augmented forms go through the same inference: `/=` and `**=` on a float32 target.</summary>
    [Theory]
    [InlineData("a /= b", "6.666666507720947")]
    [InlineData("a **= b", "1.2311444282531738")]
    [InlineData("a /= i", "0.6666666865348816")]
    [InlineData("a **= i", "8.0")]
    public void AugmentedFloat32Target_StaysFloat32(string statement, string expected)
    {
        var result = CompileAndExecute(Prelude + $"    {statement}\n    print(float64(a))\n");

        result.Success.Should().BeTrue($"{string.Join("; ", result.CompilationErrors)} stderr: {result.StandardError}");
        result.StandardOutput.Trim().Should().Be(expected);
    }
}
