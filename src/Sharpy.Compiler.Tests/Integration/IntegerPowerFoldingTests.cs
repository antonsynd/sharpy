using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Tests for constant folding of integer exponentiation (<c>**</c>): non-negative constant
/// integer powers are folded at semantic time, widening the result type to <c>long</c> when it
/// no longer fits <c>int</c>, and emitting SPY0328 when it exceeds <c>long</c> (#905).
/// </summary>
[Collection("HeavyCompilation")]
public class IntegerPowerFoldingTests : IntegrationTestBase
{
    public IntegerPowerFoldingTests(ITestOutputHelper output) : base(output)
    {
    }

    private static bool HasCode(IntegrationTestBase.ExecutionResult result, string code)
        => result.RawDiagnostics.Exists(d => d.Code == code);

    [Fact]
    public void Power_ResultExceedsLong_EmitsSpy0328()
    {
        var result = CompileAndExecute(@"
def main():
    x = 10 ** 50
    print(x)
");

        Assert.False(result.Success);
        Assert.True(HasCode(result, "SPY0328"),
            $"Expected SPY0328, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Power_JustOverLongMax_EmitsSpy0328()
    {
        // 2 ** 63 = 9223372036854775808 = long.MaxValue + 1 → overflow.
        var result = CompileAndExecute(@"
def main():
    x = 2 ** 63
    print(x)
");

        Assert.False(result.Success);
        Assert.True(HasCode(result, "SPY0328"),
            $"Expected SPY0328, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Power_FitsLong_NoOverflowDiagnostic()
    {
        // 2 ** 62 = 4611686018427387904 fits long (and exceeds int) → must widen, not error.
        var result = CompileAndExecute(@"
def main():
    x = 2 ** 62
    print(x)
");

        Assert.False(HasCode(result, "SPY0328"),
            $"Did not expect SPY0328 for an in-long-range power. Errors: {string.Join(", ", result.CompilationErrors)}");
        Assert.True(result.Success,
            $"Expected success, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Power_LongAnnotationWiden_Compiles()
    {
        // y: long = 10 ** 18 — result exceeds int but fits long; must type-check and compile.
        var result = CompileAndExecute(@"
def main():
    y: long = 10 ** 18
    print(y)
");

        Assert.False(HasCode(result, "SPY0328"),
            $"Did not expect SPY0328. Errors: {string.Join(", ", result.CompilationErrors)}");
        Assert.True(result.Success,
            $"Expected success, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Power_SmallConstant_StaysIntAndRuns()
    {
        var result = CompileAndExecute(@"
def main():
    z = 2 ** 10
    print(z)
");

        Assert.True(result.Success,
            $"Expected success, got: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1024", result.StandardOutput.Trim());
    }

    [Fact]
    public void Power_NegativeExponent_NotFolded_NoOverflowDiagnostic()
    {
        // Negative exponents keep the existing (non-folded) path; folding must not fire,
        // and certainly must not emit SPY0328.
        var result = CompileAndExecute(@"
def main():
    z = 2 ** -1
    print(z)
");

        Assert.False(HasCode(result, "SPY0328"),
            $"Negative exponent must not be folded/overflow-checked. Errors: {string.Join(", ", result.CompilationErrors)}");
    }
}
