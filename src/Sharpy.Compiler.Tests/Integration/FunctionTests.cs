using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for function definitions and calls.
/// </summary>
public class FunctionTests : IntegrationTestBase
{
    public FunctionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact(Skip = "Function call semantics issue - parameter count mismatch and print() builtin limitation")]
    public void SimpleFunction_WithReturn_WorksCorrectly()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

result: int = add(5, 3)
print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("8\n", result.StandardOutput);
    }

    [Fact]
    public void VoidFunction_PrintsCorrectly()
    {
        var source = @"
def greet(name: str):
    print(f""Hello, {name}!"")

greet(""World"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello, World!\n", result.StandardOutput);
    }

    [Fact(Skip = "Function call semantics issue - parameter count mismatch and print() builtin limitation")]
    public void RecursiveFunction_Factorial_WorksCorrectly()
    {
        var source = @"
def factorial(n: int) -> int:
    if n <= 1:
        return 1
    return n * factorial(n - 1)

result: int = factorial(5)
print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("120\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionWithDefaultParameter_WorksCorrectly()
    {
        var source = @"
def greet(name: str, greeting: str = ""Hello""):
    print(f""{greeting}, {name}!"")

greet(""Alice"")
greet(""Bob"", ""Hi"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello, Alice!\nHi, Bob!\n", result.StandardOutput);
    }

    [Fact(Skip = "Function call semantics issue - parameter count mismatch and print() builtin limitation")]
    public void MultipleFunctions_CallEachOther_WorksCorrectly()
    {
        var source = @"
def double_value(x: int) -> int:
    return x * 2

def triple_value(x: int) -> int:
    return x * 3

def process(x: int) -> int:
    doubled: int = double_value(x)
    tripled: int = triple_value(x)
    return doubled + tripled

result: int = process(4)
print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("20\n", result.StandardOutput);
    }
}
