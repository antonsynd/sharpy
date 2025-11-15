using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for basic Sharpy programs demonstrating core v0.5 features.
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
        var source = @"
def main():
    print(""Hello, World!"")

main()
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

    [Fact(Skip = "Type inference without type annotation not yet fully implemented")]
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
}
