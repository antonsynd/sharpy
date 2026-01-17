using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for basic Sharpy programs demonstrating core v0.1 features.
/// </summary>
public class BasicProgramTests : IntegrationTestBase
{
    public BasicProgramTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void HelloWorld_PrintsCorrectly()
    {
        var source = @"
print(""Hello, World!"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello, World!\n", result.StandardOutput);
    }

    [Fact]
    public void HelloWorld_WithFunction_PrintsCorrectly()
    {
        // main() function is automatically invoked as the entry point
        // No explicit main() call is needed (or allowed) when main is defined
        var source = @"
def main():
    print(""Hello, World!"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello, World!\n", result.StandardOutput);
    }

    [Fact]
    public void Fibonacci_Recursive_ComputesCorrectly()
    {
        var source = @"
def fibonacci(n: int) -> int:
    if n <= 1:
        return n
    return fibonacci(n - 1) + fibonacci(n - 2)

result: int = fibonacci(10)
print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("55\n", result.StandardOutput);
    }

    [Fact]
    public void Fibonacci_Iterative_ComputesCorrectly()
    {
        var source = @"
def fibonacci(n: int) -> int:
    if n <= 1:
        return n

    a: int = 0
    b: int = 1
    i: int = 2

    while i <= n:
        temp: int = a + b
        a = b
        b = temp
        i = i + 1

    return b

result: int = fibonacci(10)
print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("55\n", result.StandardOutput);
    }

    [Fact]
    public void SimpleArithmetic_WorksCorrectly()
    {
        var source = @"
x: int = 10
y: int = 5

print(x + y)
print(x - y)
print(x * y)
print(x // y)
print(x % y)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        var expectedOutput = "15\n5\n50\n2\n0\n";
        Assert.Equal(expectedOutput, result.StandardOutput);
    }

    [Fact]
    public void TypeInference_WorksCorrectly()
    {
        var source = @"
x = 42
y = 3.14
z = ""hello""
flag = True

print(x)
print(y)
print(z)
print(flag)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        var expectedOutput = "42\n3.14\nhello\nTrue\n";
        Assert.Equal(expectedOutput, result.StandardOutput);
    }

    [Fact]
    public void VariableAssignment_WorksCorrectly()
    {
        var source = @"
x: int = 10
print(x)

x = 20
print(x)

x = x + 5
print(x)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        var expectedOutput = "10\n20\n25\n";
        Assert.Equal(expectedOutput, result.StandardOutput);
    }

    [Fact]
    public void AugmentedAssignment_WorksCorrectly()
    {
        var source = @"
x: int = 10

x += 5
print(x)

x -= 3
print(x)

x *= 2
print(x)

x //= 4
print(x)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        var expectedOutput = "15\n12\n24\n6\n";
        Assert.Equal(expectedOutput, result.StandardOutput);
    }

    [Fact]
    public void Comments_AreIgnored()
    {
        var source = @"
# This is a comment
x: int = 42  # inline comment
# Another comment
print(x)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n", result.StandardOutput);
    }

    [Fact]
    public void MultipleStatements_ExecuteInOrder()
    {
        var source = @"
print(""First"")
print(""Second"")
print(""Third"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("First\nSecond\nThird\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithIfBlockAndAssignment_NoReturn_CompilesAndExecutes()
    {
        var source = @"
def process(x: int):
    if x > 0:
        y = x * 2
        print(y)

process(5)
process(-1)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithIfElseBlocksAndAssignments_NoReturn_CompilesAndExecutes()
    {
        var source = @"
def categorize(x: int):
    if x > 0:
        print(""positive"")
    else:
        print(""non-positive"")

categorize(5)
categorize(-3)
categorize(0)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("positive\nnon-positive\nnon-positive\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithNestedIfAndAssignment_NoReturn_CompilesAndExecutes()
    {
        var source = @"
def nested_check(x: int, y: int):
    if x > 0:
        if y > 0:
            sum = x + y
            print(sum)
        else:
            print(""y not positive"")
    else:
        print(""x not positive"")

nested_check(3, 4)
nested_check(3, -1)
nested_check(-1, 4)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("7\ny not positive\nx not positive\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithIfBlockWithInlineComment_CompilesAndExecutes()
    {
        var source = @"
def compute(x: int):
    if x > 0:  # check if positive
        result = x * 2  # double the value
        print(result)  # output result

compute(7)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("14\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithMultipleIfBlocksAndComments_CompilesAndExecutes()
    {
        var source = @"
def analyze(x: int):  # analyze function
    if x > 0:  # positive case
        print(""positive"")  # output
    elif x < 0:  # negative case
        print(""negative"")  # output
    else:  # zero case
        print(""zero"")  # output

analyze(5)
analyze(-3)
analyze(0)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("positive\nnegative\nzero\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithWhileLoopAndComments_CompilesAndExecutes()
    {
        var source = @"
def countdown(n: int):  # countdown function
    while n > 0:  # loop condition
        print(n)  # output value
        n = n - 1  # decrement

countdown(3)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3\n2\n1\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithForLoopAndComments_CompilesAndExecutes()
    {
        var source = @"
def iterate():  # iterate function
    for i in range(3):  # loop through range
        print(i)  # output value

iterate()
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithComplexNestedStructuresAndComments_CompilesAndExecutes()
    {
        var source = @"
def complex(x: int):  # main function
    if x > 0:  # check positive
        for i in range(x):  # iterate
            if i % 2 == 0:  # even check
                print(i)  # output even numbers

complex(5)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n2\n4\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithIfBlockAndMultipleAssignments_NoReturn_CompilesAndExecutes()
    {
        var source = @"
def process_data(value: int):
    if value > 10:
        x = value * 2
        y = x + 5
        z = y - 3
        print(z)

process_data(15)
process_data(5)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("32\n", result.StandardOutput);
    }
}
