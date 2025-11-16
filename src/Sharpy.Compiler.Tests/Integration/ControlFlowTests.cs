using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for control flow statements (if/elif/else, while, for).
/// </summary>
public class ControlFlowTests : IntegrationTestBase
{
    public ControlFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void IfStatement_SimpleCondition_WorksCorrectly()
    {
        var source = @"
x: int = 10

if x > 5:
    print(""x is greater than 5"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("x is greater than 5\n", result.StandardOutput);
    }

    [Fact(Skip = "Function call semantics issue - parameter count mismatch")]
    public void IfElseStatement_WorksCorrectly()
    {
        var source = @"
def check_number(x: int):
    if x > 0:
        print(""positive"")
    else:
        print(""non-positive"")

check_number(5)
check_number(-3)
check_number(0)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("positive\nnon-positive\nnon-positive\n", result.StandardOutput);
    }

    [Fact(Skip = "Function call semantics issue - parameter count mismatch")]
    public void IfElifElseStatement_WorksCorrectly()
    {
        var source = @"
def classify_number(x: int):
    if x > 0:
        print(""positive"")
    elif x < 0:
        print(""negative"")
    else:
        print(""zero"")

classify_number(5)
classify_number(-3)
classify_number(0)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("positive\nnegative\nzero\n", result.StandardOutput);
    }

    [Fact(Skip = "Function call semantics issue - parameter count mismatch")]
    public void MultipleElif_WorksCorrectly()
    {
        var source = @"
def grade(score: int):
    if score >= 90:
        print(""A"")
    elif score >= 80:
        print(""B"")
    elif score >= 70:
        print(""C"")
    elif score >= 60:
        print(""D"")
    else:
        print(""F"")

grade(95)
grade(85)
grade(75)
grade(65)
grade(55)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("A\nB\nC\nD\nF\n", result.StandardOutput);
    }

    [Fact(Skip = "Function call semantics issue - parameter count mismatch")]
    public void NestedIfStatements_WorkCorrectly()
    {
        var source = @"
def check_range(x: int):
    if x > 0:
        if x < 10:
            print(""single digit positive"")
        else:
            print(""multi digit positive"")
    else:
        print(""non-positive"")

check_range(5)
check_range(15)
check_range(-5)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("single digit positive\nmulti digit positive\nnon-positive\n", result.StandardOutput);
    }

    [Fact(Skip = "Variable assignment semantics - assignment creates new variable instead of modifying existing")]
    public void WhileLoop_SimpleCount_WorksCorrectly()
    {
        var source = @"
i: int = 0
while i < 5:
    print(i)
    i = i + 1
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n3\n4\n", result.StandardOutput);
    }

    [Fact(Skip = "Variable assignment semantics issue")]
    public void WhileLoop_WithBreak_WorksCorrectly()
    {
        var source = @"
i: int = 0
while True:
    if i >= 3:
        break
    print(i)
    i = i + 1
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n", result.StandardOutput);
    }

    [Fact(Skip = "print() builtin only accepts strings and variable assignment semantics issue")]
    public void WhileLoop_WithContinue_WorksCorrectly()
    {
        var source = @"
i: int = 0
while i < 5:
    i = i + 1
    if i == 3:
        continue
    print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n4\n5\n", result.StandardOutput);
    }

    [Fact(Skip = "For loop iteration over range not yet implemented")]
    public void ForLoop_WithRange_WorksCorrectly()
    {
        var source = @"
for i in range(5):
    print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void ForLoop_WithRangeStartStop_WorksCorrectly()
    {
        var source = @"
for i in range(2, 7):
    print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("2\n3\n4\n5\n6\n", result.StandardOutput);
    }

    [Fact]
    public void ForLoop_WithRangeStep_WorksCorrectly()
    {
        var source = @"
for i in range(0, 10, 2):
    print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n2\n4\n6\n8\n", result.StandardOutput);
    }

    [Fact(Skip = "For loop iteration over range not yet implemented")]
    public void ForLoop_WithBreak_WorksCorrectly()
    {
        var source = @"
for i in range(10):
    if i == 5:
        break
    print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n3\n4\n", result.StandardOutput);
    }

    [Fact(Skip = "For loop iteration over range not yet implemented - variable naming conflict")]
    public void ForLoop_WithContinue_WorksCorrectly()
    {
        var source = @"
for i in range(6):
    if i == 3:
        continue
    print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n4\n5\n", result.StandardOutput);
    }

    [Fact(Skip = "Nested for loops - variable naming and range iteration issues")]
    public void NestedLoops_WorkCorrectly()
    {
        var source = @"
for i in range(3):
    for j in range(2):
        print(f""{i},{j}"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0,0\n0,1\n1,0\n1,1\n2,0\n2,1\n", result.StandardOutput);
    }

    [Fact(Skip = "Function call semantics issue - parameter count mismatch")]
    public void LogicalOperators_InConditions_WorkCorrectly()
    {
        var source = @"
def test_logical(x: int, y: int):
    if x > 0 and y > 0:
        print(""both positive"")
    elif x > 0 or y > 0:
        print(""at least one positive"")
    else:
        print(""none positive"")

test_logical(5, 3)
test_logical(5, -3)
test_logical(-5, 3)
test_logical(-5, -3)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("both positive\nat least one positive\nat least one positive\nnone positive\n", result.StandardOutput);
    }

    [Fact(Skip = "Variable naming conflicts and multiple print call name mangling")]
    public void NotOperator_WorksCorrectly()
    {
        var source = @"
flag: bool = False
if not flag:
    print(""flag is false"")

flag = True
if not flag:
    print(""this should not print"")
else:
    print(""flag is true"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("flag is false\nflag is true\n", result.StandardOutput);
    }
}
