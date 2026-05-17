using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0-1-2: Minimal programs demonstrating
/// the most basic compilation scenarios.
/// </summary>
[Collection("HeavyCompilation")]
public class Phase012IntegrationTests : IntegrationTestBase
{
    public Phase012IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Pass Statement Tests

    [Fact]
    public void MinimalProgram_Pass_CompilesAndRuns()
    {
        var source = @"
def main():
    pass
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void MinimalProgram_PassWithNewline_CompilesAndRuns()
    {
        var source = @"
def main():
    pass
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_MultiplePassStatements_CompilesAndRuns()
    {
        var source = @"
def main():
    pass
    pass
    pass
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion

    #region Expression Statement Tests

    [Fact]
    public void MinimalProgram_IntegerLiteral_CompilesAndRuns()
    {
        var source = @"
def main():
    42
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_SimpleAddition_CompilesAndRuns()
    {
        var source = @"
def main():
    42 + 8
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_SimpleSubtraction_CompilesAndRuns()
    {
        var source = @"
def main():
    100 - 42
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_SimpleMultiplication_CompilesAndRuns()
    {
        var source = @"
def main():
    6 * 7
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_SimpleDivision_CompilesAndRuns()
    {
        var source = @"
def main():
    84 // 2
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_ComplexExpression_CompilesAndRuns()
    {
        var source = @"
def main():
    (10 + 20) * 3 - 5
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_MultipleExpressionStatements_CompilesAndRuns()
    {
        var source = @"
def main():
    42
    100 - 58
    6 * 7
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion

    #region Binary Operator Tests

    [Fact]
    public void MinimalProgram_AllArithmeticOperators_CompilesAndRuns()
    {
        var source = @"
def main():
    10 + 5
    10 - 5
    10 * 5
    10 // 5
    10 % 3
    2 ** 8
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_ComparisonOperators_CompilesAndRuns()
    {
        var source = @"
def main():
    5 < 10
    5 <= 10
    5 > 10
    5 >= 10
    5 == 5
    5 != 10
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_LogicalOperators_CompilesAndRuns()
    {
        var source = @"
def main():
    True and False
    True or False
    not True
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion

    #region Assignment Tests

    [Fact]
    public void MinimalProgram_SimpleAssignment_CompilesAndRuns()
    {
        var source = @"
def main():
    x = 42
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_TypedAssignment_CompilesAndRuns()
    {
        var source = @"
def main():
    x: int = 42
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_MultipleAssignments_CompilesAndRuns()
    {
        var source = @"
def main():
    x = 10
    y = 20
    z = x + y
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_AugmentedAssignment_CompilesAndRuns()
    {
        var source = @"
def main():
    x = 10
    x += 5
    x -= 3
    x *= 2
    x //= 4
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion

    #region Mixed Statement Tests

    [Fact]
    public void MinimalProgram_PassAndExpressions_CompilesAndRuns()
    {
        var source = @"
def main():
    pass
    42
    pass
    100 - 58
    pass
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_AssignmentAndExpressions_CompilesAndRuns()
    {
        var source = @"
def main():
    x = 10
    x + 5
    y = x * 2
    y - 10
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_AllStatementTypes_CompilesAndRuns()
    {
        var source = @"
def main():
    pass
    42
    x = 10
    x + 5
    pass
    y: int = 20
    y * 2
    pass
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion

    #region Comment Tests

    [Fact]
    public void MinimalProgram_OnlyComment_CompilesAndRuns()
    {
        var source = @"
def main():
    # This is a comment
    pass
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_PassWithComment_CompilesAndRuns()
    {
        var source = @"
def main():
    # This does nothing
    pass
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_ExpressionWithInlineComment_CompilesAndRuns()
    {
        var source = @"
def main():
    42 + 8  # The answer plus eight
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_AssignmentWithComments_CompilesAndRuns()
    {
        var source = @"
def main():
    # Initialize x
    x = 10  # Set to 10
    # Add 5
    x += 5  # Now x is 15
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion

    #region Literal Tests

    [Fact]
    public void MinimalProgram_IntegerLiterals_CompilesAndRuns()
    {
        var source = @"
def main():
    0
    42
    -17
    1000000
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_FloatLiterals_CompilesAndRuns()
    {
        var source = @"
def main():
    3.14
    -2.5
    0.0
    1.23e10
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_BooleanLiterals_CompilesAndRuns()
    {
        var source = @"
def main():
    True
    False
    True and False
    True or False
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_StringLiterals_CompilesAndRuns()
    {
        var source = @"
def main():
    ""hello""
    'world'
    """"
    ''
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_NoneLiteral_CompilesAndRuns()
    {
        var source = @"
def main():
    None
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_AllLiteralTypes_CompilesAndRuns()
    {
        var source = @"
def main():
    42
    3.14
    True
    False
    ""hello""
    None
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion

    #region Empty/Whitespace Tests

    [Fact]
    public void MinimalProgram_EmptyFile_RequiresMain()
    {
        // Entry point files require a main() function
        var source = @"";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Empty file should fail compilation as entry point");
        Assert.Contains(result.CompilationErrors, e => e.Contains("main"));
    }

    [Fact]
    public void MinimalProgram_OnlyWhitespace_RequiresMain()
    {
        // Entry point files require a main() function
        var source = @"


";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Whitespace-only file should fail compilation as entry point");
        Assert.Contains(result.CompilationErrors, e => e.Contains("main"));
    }

    [Fact]
    public void MinimalProgram_OnlyNewlines_RequiresMain()
    {
        // Entry point files require a main() function
        var source = "\n\n\n\n";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Newlines-only file should fail compilation as entry point");
        Assert.Contains(result.CompilationErrors, e => e.Contains("main"));
    }

    #endregion

    #region Type Annotations Tests

    [Fact]
    public void MinimalProgram_IntTypeAnnotation_CompilesAndRuns()
    {
        var source = @"
def main():
    x: int = 42
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_FloatTypeAnnotation_CompilesAndRuns()
    {
        var source = @"
def main():
    x: float = 3.14
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_StrTypeAnnotation_CompilesAndRuns()
    {
        var source = @"
def main():
    x: str = ""hello""
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_BoolTypeAnnotation_CompilesAndRuns()
    {
        var source = @"
def main():
    x: bool = True
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_MultipleTypedAssignments_CompilesAndRuns()
    {
        var source = @"
def main():
    a: int = 42
    b: float = 3.14
    c: str = ""hello""
    d: bool = True
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion

    #region Operator Precedence Tests

    [Fact]
    public void MinimalProgram_OperatorPrecedence_AdditionMultiplication_CompilesAndRuns()
    {
        var source = @"
def main():
    2 + 3 * 4
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_OperatorPrecedence_WithParentheses_CompilesAndRuns()
    {
        var source = @"
def main():
    (2 + 3) * 4
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_OperatorPrecedence_Complex_CompilesAndRuns()
    {
        var source = @"
def main():
    2 + 3 * 4 - 5 // 2
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_OperatorPrecedence_NestedParentheses_CompilesAndRuns()
    {
        var source = @"
def main():
    ((2 + 3) * (4 - 1)) // 5
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion

    #region Unary Operator Tests

    [Fact]
    public void MinimalProgram_UnaryMinus_CompilesAndRuns()
    {
        var source = @"
def main():
    -42
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_UnaryPlus_CompilesAndRuns()
    {
        var source = @"
def main():
    +42
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_UnaryNot_CompilesAndRuns()
    {
        var source = @"
def main():
    not True
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public void MinimalProgram_MultipleUnaryOperators_CompilesAndRuns()
    {
        var source = @"
def main():
    -42
    +17
    not False
    not not True
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Empty(result.StandardOutput);
    }

    #endregion
}
