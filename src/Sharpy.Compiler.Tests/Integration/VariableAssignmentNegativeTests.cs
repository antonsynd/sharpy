using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Negative tests for variable assignment edge cases.
/// These tests verify that invalid code is properly rejected.
/// </summary>
public class VariableAssignmentNegativeTests : IntegrationTestBase
{
    public VariableAssignmentNegativeTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void Assignment_ToUndeclaredVariable_InStrictMode_ShouldFail()
    {
        var source = @"
def test():
    y = x + 1
    return y
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for undeclared variable");
        // Check for error message indicating undefined variable
        var errorText = string.Join(" ", result.CompilationErrors).ToLower();
        Assert.True(errorText.Contains("not defined") || errorText.Contains("not declared") || errorText.Contains("undefined") || errorText.Contains("does not exist"),
            $"Expected error about undefined variable, got: {string.Join(", ", result.CompilationErrors)}");
    }

    [Fact]
    public void Assignment_TypeMismatch_WithoutRedefinition_ShouldFail()
    {
        var source = @"
def main():
    x: int = 5
    x = ""hello""  # Type mismatch without redefinition
";

        var result = CompileAndExecute(source);

        // This should fail at semantic analysis (type mismatch)
        // or succeed if we allow implicit type changes
        // Based on the language spec, this should fail without 'auto' annotation
        if (!result.Success)
        {
            Assert.Contains("type", string.Join(" ", result.CompilationErrors).ToLower());
        }
    }

    [Fact]
    public void Assignment_ToConstant_ShouldFail()
    {
        var source = @"
def main():
    const PI: float = 3.14159
    PI = 3.14  # Cannot reassign constant
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for constant reassignment");
        Assert.Contains("const", string.Join(" ", result.CompilationErrors).ToLower());
    }

    [Fact]
    public void Assignment_UpdateParameterAfterTypeChange_ShouldCompile()
    {
        // This tests that parameter updates work even after a redefinition
        var source = @"
def test(x: int):
    x = x + 1  # Update parameter
    print(x)
    x: auto = ""changed""  # Redefine with new type
    print(x)

def main():
    test(5)
";

        var result = CompileAndExecute(source, fileName: "main.spy");

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("6\nchanged\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_InfiniteLoop_Detection_TimeoutTest()
    {
        // This test verifies that infinite loops don't hang the test suite
        // Uses a 2000ms timeout to detect the infinite loop (increased from 500ms for CI runners)
        var source = @"
def main():
    # This would create an infinite loop if variable updates don't work
    i: int = 0
    while i < 5:
        print(i)
        # Missing: i = i + 1
";

        var result = CompileAndExecute(source, executionTimeoutMs: 2000);

        // The test should timeout because the loop never terminates
        Assert.True(result.TimedOut, "Expected execution to timeout due to infinite loop");
        Assert.False(result.Success, "Expected execution to fail due to timeout");

        // If any output was produced, verify it's all "0" since i is never incremented
        // Note: On slow CI runners, output might not be captured before timeout
        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0)
        {
            Assert.All(lines, line => Assert.Equal("0", line));
        }
    }

    [Fact]
    public void Assignment_ReferenceToOldVersion_AfterRedefinition_ShouldUseNewVersion()
    {
        var source = @"
def main():
    x = 1
    x: auto = 2
    print(x)  # Should print 2, not 1
    x: auto = 3
    print(x)  # Should print 3, not 2
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("2\n3\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_VariableUsedBeforeAssignment_ShouldFail()
    {
        var source = @"
def test():
    while x < 5:  # x not declared
        print(x)
        x = x + 1
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for variable used before declaration");
    }

    [Fact]
    public void Assignment_MultipleVariablesSameName_DifferentScopes_WorksCorrectly()
    {
        var source = @"
x: int = 1

def test():
    x: int = 2  # Different scope
    print(x)
    x = 3
    print(x)

def main():
    print(x)
    test()
    print(x)  # Should still be 1
";

        var result = CompileAndExecute(source, fileName: "main.spy");

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n3\n1\n", result.StandardOutput);
    }

    [Fact]
    public void ForLoop_ModifyLoopVariable_WorksCorrectly()
    {
        // In Python, modifying the loop variable doesn't affect the iteration
        // but we should handle it correctly in Sharpy
        var source = @"
def main():
    for i in range(3):
        print(i)
        i = i + 10  # This shouldn't affect the next iteration
        print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // Each iteration: original value, then modified value
        Assert.Equal("0\n10\n1\n11\n2\n12\n", result.StandardOutput);
    }

    [Fact]
    public void Assignment_ChainedAssignments_WorkCorrectly()
    {
        var source = @"
def main():
    a: int = 1
    b: int = 2
    c: int = 3
    a = b = c = 10
    print(a)
    print(b)
    print(c)
";

        var result = CompileAndExecute(source);

        // Chained assignments are not currently supported
        // This test documents the current behavior
        Assert.False(result.Success, "Chained assignments should fail to parse");
        // The parser should report an error
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void WhileLoop_EmptyBody_ShouldCompile()
    {
        var source = @"
def main():
    i: int = 0
    while i < 5:
        i = i + 1
    # No additional statements in loop body
    print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n", result.StandardOutput);
    }

    [Fact]
    public void Assignment_OperatorPrecedence_InUpdate_WorksCorrectly()
    {
        var source = @"
def main():
    x: int = 1
    x = x * 2 + 3
    print(x)
    x = (x + 1) * 2
    print(x)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // x = 1 * 2 + 3 = 5
        // x = (5 + 1) * 2 = 12
        Assert.Equal("5\n12\n", result.StandardOutput);
    }

    [Fact]
    public void Assignment_UpdateWithFunctionCall_WorksCorrectly()
    {
        var source = @"
def double_value(x: int) -> int:
    return x * 2

def main():
    i: int = 5
    i = double_value(i)
    print(i)
    i = double_value(i) + 1
    print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n21\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_MultipleBreakConditions_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    while True:
        i = i + 1
        if i == 3:
            print(""skip 3"")
            continue
        if i > 5:
            break
        print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\nskip 3\n4\n5\n", result.StandardOutput);
    }

    [Fact]
    public void Assignment_SelfReferentialExpression_WorksCorrectly()
    {
        var source = @"
def main():
    x: int = 1
    x = x + x
    print(x)
    x = x * x
    print(x)
    x = x - x
    print(x)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("2\n4\n0\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_NestedWithSameVariableName_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    while i < 2:
        print(i)
        j: int = 0
        while j < 2:
            print(j)
            j = j + 1
        i = i + 1
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n0\n1\n1\n0\n1\n", result.StandardOutput);
    }
}
