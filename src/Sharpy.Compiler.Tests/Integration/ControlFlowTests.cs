using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for control flow statements (if/elif/else, while, for).
/// </summary>
[Collection("HeavyCompilation")]
public class ControlFlowTests : IntegrationTestBase
{
    public ControlFlowTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void IfStatement_SimpleCondition_WorksCorrectly()
    {
        var source = @"
def main():
    x: int = 10

    if x > 5:
        print(""x is greater than 5"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("x is greater than 5\n", result.StandardOutput);
    }

    [Fact]
    public void IfElseStatement_WorksCorrectly()
    {
        var source = @"
def check_number(x: int):
    if x > 0:
        print(""positive"")
    else:
        print(""non-positive"")

def main():
    check_number(5)
    check_number(-3)
    check_number(0)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("positive\nnon-positive\nnon-positive\n", result.StandardOutput);
    }

    [Fact]
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

def main():
    classify_number(5)
    classify_number(-3)
    classify_number(0)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("positive\nnegative\nzero\n", result.StandardOutput);
    }

    [Fact]
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

def main():
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

    [Fact]
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

def main():
    check_range(5)
    check_range(15)
    check_range(-5)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("single digit positive\nmulti digit positive\nnon-positive\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_SimpleCount_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    while i < 5:
        print(i)
        i = i + 1
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_WithBreak_WorksCorrectly()
    {
        var source = @"
def main():
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

    [Fact]
    public void WhileLoop_WithContinue_WorksCorrectly()
    {
        var source = @"
def main():
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

    [Fact]
    public void ForLoop_WithRange_WorksCorrectly()
    {
        var source = @"
def main():
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
def main():
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
def main():
    for i in range(0, 10, 2):
        print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n2\n4\n6\n8\n", result.StandardOutput);
    }

    [Fact]
    public void ForLoop_WithBreak_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(10):
        if i == 5:
            break
        print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void ForLoop_WithContinue_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(6):
        if i == 3:
            continue
        print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n4\n5\n", result.StandardOutput);
    }

    [Fact]
    public void NestedLoops_WorkCorrectly()
    {
        var source = @"
def main():
    for i in range(3):
        for j in range(2):
            print(f""{i},{j}"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0,0\n0,1\n1,0\n1,1\n2,0\n2,1\n", result.StandardOutput);
    }

    [Fact]
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

def main():
    test_logical(5, 3)
    test_logical(5, -3)
    test_logical(-5, 3)
    test_logical(-5, -3)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("both positive\nat least one positive\nat least one positive\nnone positive\n", result.StandardOutput);
    }

    [Fact]
    public void NotOperator_WorksCorrectly()
    {
        var source = @"
def main():
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

    #region While Loop Edge Cases

    [Fact]
    public void WhileLoop_MultipleVariableUpdates_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    j: int = 10
    while i < 5:
        i = i + 1
        j = j - 1
        print(i)
        print(j)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n9\n2\n8\n3\n7\n4\n6\n5\n5\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_NestedLoops_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    while i < 3:
        j: int = 0
        while j < 2:
            print(i)
            print(j)
            j = j + 1
        i = i + 1
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n0\n0\n1\n1\n0\n1\n1\n2\n0\n2\n1\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_ParameterUpdate_InFunction_WorksCorrectly()
    {
        var source = @"
def countdown(n: int):
    while n > 0:
        print(n)
        n = n - 1
    print(""done"")

def main():
    countdown(3)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3\n2\n1\ndone\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_AugmentedAssignment_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 1
    while i < 100:
        print(i)
        i += i  # Double each iteration
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n4\n8\n16\n32\n64\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_ComplexCondition_WithMultipleUpdates_WorksCorrectly()
    {
        var source = @"
def main():
    x: int = 0
    y: int = 10
    while x < 5 and y > 0:
        x = x + 1
        y = y - 2
        print(x)
        print(y)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n8\n2\n6\n3\n4\n4\n2\n5\n0\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_BreakWithCondition_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    sum: int = 0
    while True:
        i = i + 1
        sum = sum + i
        if sum > 10:
            break
    print(sum)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("15\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_ContinueSkipsRemainingIterations_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    while i < 5:
        i = i + 1
        if i == 2 or i == 4:
            continue
        print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n3\n5\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_VariableRedefinitionWithTypeChange_InsideLoop_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    while i < 3:
        x: auto = i
        print(x)
        x: auto = ""iteration""
        print(x)
        i = i + 1
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\niteration\n1\niteration\n2\niteration\n", result.StandardOutput);
    }

    [Fact]
    public void ForLoop_VariableUpdateInsideLoop_WorksCorrectly()
    {
        var source = @"
def main():
    sum: int = 0
    for i in range(5):
        sum = sum + i
    print(sum)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionParameter_MultipleUpdates_WorksCorrectly()
    {
        var source = @"
def modify_param(x: int) -> int:
    print(x)
    x = x * 2
    print(x)
    x = x + 10
    print(x)
    return x

def main():
    result: int = modify_param(5)
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n10\n20\n20\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionParameter_ConditionalUpdate_WorksCorrectly()
    {
        var source = @"
def absolute_value(x: int) -> int:
    if x < 0:
        x = -x
    return x

def main():
    print(absolute_value(-5))
    print(absolute_value(3))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n3\n", result.StandardOutput);
    }

    #endregion

    #region Variable Assignment Edge Cases

    [Fact]
    public void Variable_FirstAssignmentThenRedefinition_WorksCorrectly()
    {
        var source = @"
def main():
    x = 5
    print(x)
    x: auto = 10
    print(x)
    x: auto = ""hello""
    print(x)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n10\nhello\n", result.StandardOutput);
    }

    [Fact]
    public void Variable_AssignmentInNestedScopes_WorksCorrectly()
    {
        var source = @"
def outer():
    x: int = 1
    print(x)

    if True:
        x = 2
        print(x)

    print(x)

def main():
    outer()
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n2\n", result.StandardOutput);
    }

    [Fact]
    public void Variable_MultipleParametersWithUpdates_WorksCorrectly()
    {
        var source = @"
def swap_and_add(a: int, b: int) -> int:
    temp: int = a
    a = b
    b = temp
    return a + b

def main():
    print(swap_and_add(3, 7))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n", result.StandardOutput);
    }

    [Fact]
    public void Variable_UpdateInWhileWithIfElse_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    count: int = 0
    while i < 10:
        i = i + 1
        if i % 2 == 0:
            count = count + 1
        else:
            count = count + 2
    print(count)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("15\n", result.StandardOutput);
    }

    #endregion

    #region Loop Else Clause

    [Fact]
    public void ForElse_NoBreak_ElseExecutes()
    {
        var source = @"
def main():
    items: list[int] = [1, 2, 3]

    for item in items:
        print(item)
    else:
        print(""completed"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n3\ncompleted\n", result.StandardOutput);
    }

    [Fact]
    public void ForElse_WithBreak_ElseSkipped()
    {
        var source = @"
def main():
    items: list[int] = [1, 2, 3, 4, 5]

    for item in items:
        if item == 3:
            print(""found"")
            break
        print(item)
    else:
        print(""not found"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\nfound\n", result.StandardOutput);
    }

    [Fact]
    public void WhileElse_NoBreak_ElseExecutes()
    {
        var source = @"
def main():
    i: int = 0

    while i < 3:
        print(i)
        i += 1
    else:
        print(""completed"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\ncompleted\n", result.StandardOutput);
    }

    [Fact]
    public void WhileElse_WithBreak_ElseSkipped()
    {
        var source = @"
def main():
    i: int = 0

    while i < 10:
        if i == 3:
            print(""found"")
            break
        print(i)
        i += 1
    else:
        print(""not found"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\nfound\n", result.StandardOutput);
    }

    [Fact]
    public void ForElse_EmptyIterable_ElseExecutes()
    {
        // Use a range that produces zero iterations
        var source = @"
def main():
    for item in range(0):
        print(item)
    else:
        print(""empty range"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("empty range\n", result.StandardOutput);
    }

    [Fact]
    public void WhileElse_FalseCondition_ElseExecutes()
    {
        var source = @"
def main():
    x: int = 10

    while x < 5:
        print(x)
        x += 1
    else:
        print(""never entered"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("never entered\n", result.StandardOutput);
    }

    [Fact]
    public void ForElse_NestedIfBreak_ElseSkipped()
    {
        var source = @"
def find_target(items: list[int], target: int) -> str:
    for item in items:
        if item > 0:
            if item == target:
                break
    else:
        return ""not found""
    return ""found""

def main():
    print(find_target([1, 2, 3], 2))
    print(find_target([1, 2, 3], 5))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("found\nnot found\n", result.StandardOutput);
    }

    [Fact]
    public void ForElse_NestedLoop_IndependentElse()
    {
        // Inner loop's break should not affect outer loop's else
        var source = @"
def main():
    outer_items: list[int] = [1, 2]
    inner_items: list[int] = [10, 20]

    for outer in outer_items:
        for inner in inner_items:
            if inner == 10:
                break
        else:
            print(""inner else"")
        print(outer)
    else:
        print(""outer else"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // Inner loop breaks, so inner else is skipped. Outer loop completes, so outer else runs.
        Assert.Equal("1\n2\nouter else\n", result.StandardOutput);
    }

    #endregion
}
