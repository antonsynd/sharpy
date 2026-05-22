using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for pipe forward operator (|>).
/// Tests end-to-end compilation and execution of pipe expressions.
/// </summary>
[Collection("HeavyCompilation")]
public class PipeOperatorTests : IntegrationTestBase
{
    public PipeOperatorTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void PipeForward_SimpleFunction_PassesAsFirstArgument()
    {
        // x |> f → f(x)
        var source = @"
def double_value(x: int) -> int:
    return x * 2

def main():
    result: int = 5 |> double_value
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n", result.StandardOutput);
    }

    [Fact]
    public void PipeForward_FunctionWithExistingArgs_PrependsToArguments()
    {
        // x |> f(y) → f(x, y)
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def main():
    result: int = 3 |> add(7)
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n", result.StandardOutput);
    }

    [Fact]
    public void PipeForward_Chained_GeneratesNestedCalls()
    {
        // x |> f |> g → g(f(x))
        var source = @"
def double_value(x: int) -> int:
    return x * 2

def add_one(x: int) -> int:
    return x + 1

def main():
    result: int = 5 |> double_value |> add_one
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // 5 |> double_value → 10, 10 |> add_one → 11
        Assert.Equal("11\n", result.StandardOutput);
    }

    [Fact]
    public void PipeForward_ChainedWithArgs_GeneratesNestedCallsWithArgs()
    {
        // x |> f(y) |> g(z) → g(f(x, y), z)
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def multiply(a: int, b: int) -> int:
    return a * b

def main():
    result: int = 2 |> add(3) |> multiply(4)
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // 2 |> add(3) → add(2, 3) = 5
        // 5 |> multiply(4) → multiply(5, 4) = 20
        Assert.Equal("20\n", result.StandardOutput);
    }

    [Fact]
    public void PipeForward_WithMultipleArgs_PrependsToPArgumentList()
    {
        // x |> f(y, z) → f(x, y, z)
        var source = @"
def sum_three(a: int, b: int, c: int) -> int:
    return a + b + c

def main():
    result: int = 1 |> sum_three(2, 3)
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("6\n", result.StandardOutput);
    }

    [Fact]
    public void PipeForward_WithStringFunction_WorksCorrectly()
    {
        var source = @"
def greet(name: str) -> str:
    return f""Hello, {name}!""

def exclaim(text: str) -> str:
    return text + ""!!""

def main():
    message: str = ""World"" |> greet |> exclaim
    print(message)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello, World!!!\n", result.StandardOutput);
    }

    [Fact]
    public void PipeForward_InExpression_WorksWithOtherOperators()
    {
        var source = @"
def double_value(x: int) -> int:
    return x * 2

def main():
    result: int = (5 |> double_value) + 3
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("13\n", result.StandardOutput);
    }

    [Fact]
    public void PipeForward_WithVariableInput_WorksCorrectly()
    {
        var source = @"
def square(x: int) -> int:
    return x * x

def main():
    value: int = 7
    result: int = value |> square
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("49\n", result.StandardOutput);
    }

    [Fact]
    public void PipeForward_LongChain_WorksCorrectly()
    {
        var source = @"
def add_one(x: int) -> int:
    return x + 1

def double_value(x: int) -> int:
    return x * 2

def square(x: int) -> int:
    return x * x

def main():
    result: int = 1 |> add_one |> double_value |> square |> add_one
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // 1 |> add_one → 2
        // 2 |> double_value → 4
        // 4 |> square → 16
        // 16 |> add_one → 17
        Assert.Equal("17\n", result.StandardOutput);
    }
}
