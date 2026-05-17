using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration.SpecDeviations;

/// <summary>
/// Tests documenting spec deviation: integer division should produce float64.
/// Spec: docs/language_specification/arithmetic_operators.md
/// "Integer types only | float64 | Always promotes to float64"
///
/// Current behavior: int / int = int (C# semantics)
/// Expected behavior: int / int = float64 (Python semantics)
/// </summary>
[Collection("HeavyCompilation")]
public class DivisionDeviationTests : IntegrationTestBase
{
    public DivisionDeviationTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Per spec: 5 / 2 should equal 2.5 (float64), not 2 (int).
    /// This is the core Python division semantics.
    /// </summary>
    [Fact]
    public void IntegerDivision_ShouldProduceFloat64Value()
    {
        var source = @"
def main():
    result = 5 / 2
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // Expected per spec: 2.5 (float64)
        // Current behavior: 2 (int) - DEVIATION
        Assert.Equal("2.5\n", result.StandardOutput);
    }

    /// <summary>
    /// Per spec: 10 / 4 should equal 2.5, demonstrating non-truncating division.
    /// </summary>
    [Fact]
    public void IntegerDivision_TenDividedByFour_ShouldBeTwoPointFive()
    {
        var source = @"
def main():
    x: int = 10
    y: int = 4
    result = x / y
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("2.5\n", result.StandardOutput);
    }

    /// <summary>
    /// Per spec: 10 / 3 should produce approximately 3.333...
    /// </summary>
    [Fact]
    public void IntegerDivision_TenDividedByThree_ShouldProduceRepeatingDecimal()
    {
        var source = @"
def main():
    result = 10 / 3
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // Should produce 3.3333... (float64 representation)
        Assert.StartsWith("3.333", result.StandardOutput);
    }

    /// <summary>
    /// Per spec: -7 / 2 should equal -3.5 (true division).
    /// Note: This differs from floor division (-7 // 2 = -4).
    /// </summary>
    [Fact]
    public void IntegerDivision_NegativeSeven_DividedByTwo_ShouldBeNegativeThreePointFive()
    {
        var source = @"
def main():
    result = -7 / 2
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // Expected per spec: -3.5 (true division)
        // Current behavior: -3 (truncating division) - DEVIATION
        Assert.Equal("-3.5\n", result.StandardOutput);
    }

    /// <summary>
    /// Per spec: result of int / int should be assignable to float without cast.
    /// </summary>
    [Fact]
    public void IntegerDivision_ShouldBeAssignableToFloat()
    {
        var source = @"
def main():
    x: int = 5
    y: int = 2
    result: float = x / y
    print(result)
";

        var result = CompileAndExecute(source);

        // This should compile without error because x / y produces float64
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("2.5\n", result.StandardOutput);
    }

    /// <summary>
    /// Contrast test: Floor division (//) should still return integer.
    /// This test should PASS with current implementation.
    /// </summary>
    [Fact]
    public void FloorDivision_ShouldReturnInteger()
    {
        var source = @"
def main():
    result = 5 // 2
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // Floor division produces integer
        Assert.Equal("2\n", result.StandardOutput);
    }

    /// <summary>
    /// Contrast test: Negative floor division rounds toward negative infinity.
    /// -7 // 2 = -4 (not -3).
    /// </summary>
    [Fact]
    public void FloorDivision_NegativeSeven_DividedByTwo_ShouldBeNegativeFour()
    {
        var source = @"
def main():
    result = -7 // 2
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // Floor division rounds toward negative infinity
        Assert.Equal("-4\n", result.StandardOutput);
    }

    /// <summary>
    /// Contrast test: Division with float operand should work correctly.
    /// Uses typed variable to avoid float literal code generation issues.
    /// </summary>
    [Fact]
    public void FloatDivision_ShouldProduceFloat()
    {
        var source = @"
def main():
    x: float = 5.0
    y: int = 2
    result = x / y
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("2.5\n", result.StandardOutput);
    }

    /// <summary>
    /// Contrast test: Division with float operand (right side).
    /// Uses typed variable to avoid float literal code generation issues.
    /// </summary>
    [Fact]
    public void FloatDivision_IntDividedByFloat_ShouldProduceFloat()
    {
        var source = @"
def main():
    x: int = 5
    y: float = 2.0
    result = x / y
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("2.5\n", result.StandardOutput);
    }

    /// <summary>
    /// Floor division with float operands should compile and produce float result.
    /// Regression test for CS0121 ambiguity between Math.Floor(double) and Math.Floor(decimal).
    /// </summary>
    [Fact]
    public void FloatFloorDivision_ShouldNotCauseAmbiguity()
    {
        var source = @"
def main():
    x: float = 7.0
    y: float = 2.0
    result: float = x // y
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3.0\n", result.StandardOutput);
    }
}
