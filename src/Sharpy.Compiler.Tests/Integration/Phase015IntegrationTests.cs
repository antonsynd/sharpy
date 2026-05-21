using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.5: Function definitions and calls.
/// These tests verify the full compilation pipeline for function-related features.
/// </summary>
[Collection("HeavyCompilation")]
public class Phase015IntegrationTests : IntegrationTestBase
{
    public Phase015IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Spec Example Tests

    [Fact]
    public void SpecExample_AddFunction_CompilesAndComputesCorrectly()
    {
        // From spec: basic function with two parameters and return type
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def main():
    x = add(2, 3)
    print(x)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n", result.StandardOutput);
    }

    [Fact]
    public void SpecExample_MultiplyWithDefaultParameter_CompilesAndComputesCorrectly()
    {
        // From spec: function with default parameter value
        var source = @"
def multiply(a: int, b: int = 1) -> int:
    return a * b

def main():
    y = multiply(4)
    print(y)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("4\n", result.StandardOutput);
    }

    [Fact]
    public void SpecExample_MultiplyWithKeywordArgument_CompilesAndComputesCorrectly()
    {
        // From spec: function call with keyword argument
        var source = @"
def multiply(a: int, b: int = 1) -> int:
    return a * b

def main():
    z = multiply(4, b=5)
    print(z)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("20\n", result.StandardOutput);
    }

    [Fact]
    public void SpecExample_AllFunctionCalls_CompilesAndComputesCorrectly()
    {
        // Combined spec example
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def multiply(a: int, b: int = 1) -> int:
    return a * b

def main():
    x = add(2, 3)
    y = multiply(4)
    z = multiply(4, b=5)
    print(x)
    print(y)
    print(z)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n4\n20\n", result.StandardOutput);
    }

    #endregion

    #region Recursive Function Tests

    [Fact]
    public void RecursiveFunction_Factorial_CompilesAndComputesCorrectly()
    {
        // From spec: recursive factorial function
        var source = @"
def factorial(n: int) -> int:
    if n <= 1:
        return 1
    return n * factorial(n - 1)

def main():
    print(factorial(5))
    print(factorial(0))
    print(factorial(1))
    print(factorial(10))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("120\n1\n1\n3628800\n", result.StandardOutput);
    }

    [Fact]
    public void RecursiveFunction_Fibonacci_CompilesAndComputesCorrectly()
    {
        var source = @"
def fib(n: int) -> int:
    if n <= 1:
        return n
    return fib(n - 1) + fib(n - 2)

def main():
    print(fib(0))
    print(fib(1))
    print(fib(5))
    print(fib(10))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n5\n55\n", result.StandardOutput);
    }

    [Fact]
    public void RecursiveFunction_SumToN_CompilesAndComputesCorrectly()
    {
        var source = @"
def sum_to_n(n: int) -> int:
    if n <= 0:
        return 0
    return n + sum_to_n(n - 1)

def main():
    print(sum_to_n(0))
    print(sum_to_n(1))
    print(sum_to_n(5))
    print(sum_to_n(10))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n15\n55\n", result.StandardOutput);
    }

    [Fact]
    public void RecursiveFunction_Power_CompilesAndComputesCorrectly()
    {
        var source = @"
def power(base: int, exp: int) -> int:
    if exp == 0:
        return 1
    return base * power(base, exp - 1)

def main():
    print(power(2, 0))
    print(power(2, 1))
    print(power(2, 10))
    print(power(3, 4))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n1024\n81\n", result.StandardOutput);
    }

    #endregion

    #region Default Parameter Tests

    [Fact]
    public void DefaultParameter_SingleDefault_UsedWhenOmitted()
    {
        var source = @"
def greet(name: str, greeting: str = ""Hello"") -> str:
    return f""{greeting}, {name}!""

def main():
    print(greet(""Alice""))
    print(greet(""Bob"", ""Hi""))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello, Alice!\nHi, Bob!\n", result.StandardOutput);
    }

    [Fact]
    public void DefaultParameter_MultipleDefaults_AllUsed()
    {
        var source = @"
def calc(a: int, b: int = 10, c: int = 100) -> int:
    return a + b + c

def main():
    print(calc(1))
    print(calc(1, 2))
    print(calc(1, 2, 3))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("111\n103\n6\n", result.StandardOutput);
    }

    [Fact]
    public void DefaultParameter_BoolDefault_WorksCorrectly()
    {
        var source = @"
def check(value: int, verbose: bool = False) -> int:
    if verbose:
        print(""Checking..."")
    return value * 2

def main():
    print(check(5))
    print(check(5, True))
    print(check(5, False))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\nChecking...\n10\n10\n", result.StandardOutput);
    }

    [Fact]
    public void DefaultParameter_FloatDefault_WorksCorrectly()
    {
        var source = @"
def scale(value: float, factor: float = 1.0) -> float:
    return value * factor

def main():
    print(scale(10.0))
    print(scale(10.0, 2.0))
    print(scale(10.0, 0.5))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10.0\n20.0\n5.0\n", result.StandardOutput);
    }

    #endregion

    #region Keyword Argument Tests

    [Fact]
    public void KeywordArgument_SingleKeyword_WorksCorrectly()
    {
        var source = @"
def divide(a: int, b: int) -> int:
    return a // b

def main():
    print(divide(a=10, b=2))
    print(divide(b=2, a=10))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n5\n", result.StandardOutput);
    }

    [Fact]
    public void KeywordArgument_MixedPositionalAndKeyword_WorksCorrectly()
    {
        var source = @"
def compute(a: int, b: int, c: int) -> int:
    return a + b * c

def main():
    print(compute(1, b=2, c=3))
    print(compute(1, 2, c=3))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("7\n7\n", result.StandardOutput);
    }

    [Fact]
    public void KeywordArgument_WithDefaults_SkipsMiddleParameter()
    {
        var source = @"
def configure(name: str, size: int = 10, color: str = ""blue"") -> str:
    return f""{name}: {size}, {color}""

def main():
    print(configure(""item""))
    print(configure(""item"", color=""red""))
    print(configure(""item"", size=20))
    print(configure(""item"", size=20, color=""green""))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("item: 10, blue\nitem: 10, red\nitem: 20, blue\nitem: 20, green\n", result.StandardOutput);
    }

    [Fact]
    public void KeywordArgument_AllKeywords_AnyOrder()
    {
        var source = @"
def point(x: int, y: int, z: int) -> str:
    return f""({x}, {y}, {z})""

def main():
    print(point(x=1, y=2, z=3))
    print(point(z=3, y=2, x=1))
    print(point(y=2, x=1, z=3))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("(1, 2, 3)\n(1, 2, 3)\n(1, 2, 3)\n", result.StandardOutput);
    }

    #endregion

    #region Void Function Tests

    [Fact]
    public void VoidFunction_NoReturnType_WorksCorrectly()
    {
        var source = @"
def say_hello():
    print(""Hello!"")

def main():
    say_hello()
    say_hello()
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello!\nHello!\n", result.StandardOutput);
    }

    [Fact]
    public void VoidFunction_WithParameters_WorksCorrectly()
    {
        var source = @"
def greet(name: str):
    print(f""Hello, {name}!"")

def main():
    greet(""Alice"")
    greet(""Bob"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello, Alice!\nHello, Bob!\n", result.StandardOutput);
    }

    /// <summary>
    /// Tests that the 'global' keyword is NOT supported in Sharpy.
    /// Sharpy deliberately does not include Python's global keyword.
    /// Instead, module-level variables can be accessed directly from functions,
    /// but cannot be reassigned (they shadow the outer variable).
    /// </summary>
    [Fact]
    public void GlobalKeyword_NotSupported_ProducesParseError()
    {
        var source = @"
counter: int = 0

def increment():
    global counter
    counter += 1

def main():
    increment()
    print(counter)
";

        var result = CompileAndExecute(source);

        // Sharpy does not support the 'global' keyword by design
        Assert.False(result.Success, "Expected compilation to fail for unsupported 'global' keyword");
        Assert.True(result.CompilationErrors.Count > 0,
            $"Expected parse or compilation error for unsupported 'global' keyword, but got no errors");
    }

    [Fact]
    public void VoidFunction_EarlyReturn_WorksCorrectly()
    {
        var source = @"
def print_if_positive(n: int):
    if n <= 0:
        return
    print(n)

def main():
    print_if_positive(5)
    print_if_positive(-3)
    print_if_positive(0)
    print_if_positive(10)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n10\n", result.StandardOutput);
    }

    #endregion

    #region Function Composition Tests

    [Fact]
    public void FunctionComposition_NestedCalls_WorksCorrectly()
    {
        var source = @"
def double_val(x: int) -> int:
    return x * 2

def add_one(x: int) -> int:
    return x + 1

def main():
    print(double_val(add_one(5)))
    print(add_one(double_val(5)))
    print(double_val(double_val(3)))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("12\n11\n12\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionComposition_CallsOtherFunctions_WorksCorrectly()
    {
        var source = @"
def square(x: int) -> int:
    return x * x

def sum_of_squares(a: int, b: int) -> int:
    return square(a) + square(b)

def main():
    print(sum_of_squares(3, 4))
    print(sum_of_squares(5, 12))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("25\n169\n", result.StandardOutput);
    }

    [Fact]
    public void FunctionComposition_ChainedHelper_WorksCorrectly()
    {
        var source = @"
def is_even(n: int) -> bool:
    return n % 2 == 0

def is_odd(n: int) -> bool:
    return not is_even(n)

def classify(n: int) -> str:
    if is_even(n):
        return ""even""
    return ""odd""

def main():
    print(classify(4))
    print(classify(7))
    print(is_odd(4))
    print(is_odd(7))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("even\nodd\nFalse\nTrue\n", result.StandardOutput);
    }

    #endregion

    #region Local Variable Scope Tests

    [Fact]
    public void LocalScope_VariablesShadowGlobals_WorksCorrectly()
    {
        var source = @"
x: int = 10

def test():
    x: int = 20
    print(x)

def main():
    test()
    print(x)
";

        var result = CompileAndExecute(source, fileName: "main.spy");

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("20\n10\n", result.StandardOutput);
    }

    [Fact]
    public void LocalScope_ParametersShadowGlobals_WorksCorrectly()
    {
        var source = @"
x: int = 100

def show_x(x: int):
    print(x)

def main():
    show_x(5)
    print(x)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n100\n", result.StandardOutput);
    }

    [Fact]
    public void LocalScope_MultipleLocalVariables_WorksCorrectly()
    {
        var source = @"
def compute(n: int) -> int:
    a: int = n + 1
    b: int = n * 2
    c: int = a + b
    return c

def main():
    print(compute(5))
    print(compute(10))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("16\n31\n", result.StandardOutput);
    }

    #endregion

    #region Control Flow in Functions Tests

    [Fact]
    public void ControlFlowInFunction_IfElse_WorksCorrectly()
    {
        var source = @"
def max_val(a: int, b: int) -> int:
    if a > b:
        return a
    else:
        return b

def main():
    print(max_val(5, 3))
    print(max_val(3, 5))
    print(max_val(5, 5))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n5\n5\n", result.StandardOutput);
    }

    [Fact]
    public void ControlFlowInFunction_WhileLoop_WorksCorrectly()
    {
        var source = @"
def count_digits(n: int) -> int:
    if n == 0:
        return 1
    if n < 0:
        n = -n
    count: int = 0
    while n > 0:
        count += 1
        n //= 10
    return count

def main():
    print(count_digits(0))
    print(count_digits(5))
    print(count_digits(123))
    print(count_digits(-9999))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n1\n3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void ControlFlowInFunction_ForLoop_WorksCorrectly()
    {
        var source = @"
def sum_range(start: int, end: int) -> int:
    total: int = 0
    for i in range(start, end):
        total += i
    return total

def main():
    print(sum_range(1, 6))
    print(sum_range(0, 11))
    print(sum_range(5, 5))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("15\n55\n0\n", result.StandardOutput);
    }

    [Fact]
    public void ControlFlowInFunction_EarlyReturnInLoop_WorksCorrectly()
    {
        var source = @"
def find_first_multiple(limit: int, divisor: int) -> int:
    for i in range(1, limit + 1):
        if i % divisor == 0:
            return i
    return -1

def main():
    print(find_first_multiple(10, 3))
    print(find_first_multiple(10, 7))
    print(find_first_multiple(5, 10))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3\n7\n-1\n", result.StandardOutput);
    }

    #endregion

    #region Multiple Return Paths Tests

    [Fact]
    public void MultipleReturnPaths_IfElifElse_WorksCorrectly()
    {
        var source = @"
def grade(score: int) -> str:
    if score >= 90:
        return ""A""
    elif score >= 80:
        return ""B""
    elif score >= 70:
        return ""C""
    elif score >= 60:
        return ""D""
    else:
        return ""F""

def main():
    print(grade(95))
    print(grade(85))
    print(grade(75))
    print(grade(65))
    print(grade(50))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("A\nB\nC\nD\nF\n", result.StandardOutput);
    }

    [Fact]
    public void MultipleReturnPaths_NestedIf_WorksCorrectly()
    {
        var source = @"
def classify(x: int, y: int) -> str:
    if x > 0:
        if y > 0:
            return ""Q1""
        else:
            return ""Q4""
    else:
        if y > 0:
            return ""Q2""
        else:
            return ""Q3""

def main():
    print(classify(1, 1))
    print(classify(-1, 1))
    print(classify(-1, -1))
    print(classify(1, -1))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Q1\nQ2\nQ3\nQ4\n", result.StandardOutput);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void Error_UndefinedFunction_ReportsError()
    {
        var source = @"
def main():
    result = undefined_function(5)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for undefined function");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_WrongArgumentCount_TooFew_ReportsError()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def main():
    result = add(5)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for wrong argument count");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_WrongArgumentCount_TooMany_ReportsError()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def main():
    result = add(1, 2, 3)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for too many arguments");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_WrongArgumentType_ReportsError()
    {
        var source = @"
def square(x: int) -> int:
    return x * x

def main():
    result = square(""hello"")
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for wrong argument type");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_ReturnTypeMismatch_ReportsError()
    {
        var source = @"
def get_number() -> int:
    return ""not a number""
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for return type mismatch");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_DuplicateParameterName_ReportsError()
    {
        var source = @"
def bad_func(x: int, x: int) -> int:
    return x
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for duplicate parameter name");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_DuplicateKeywordArgument_ReportsError()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def main():
    result = add(a=1, a=2)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for duplicate keyword argument");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_UnknownKeywordArgument_ReportsError()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def main():
    result = add(a=1, c=2)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for unknown keyword argument");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_PositionalAfterKeyword_ReportsError()
    {
        var source = @"
def add(a: int, b: int) -> int:
    return a + b

def main():
    result = add(a=1, 2)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for positional after keyword argument");
        Assert.NotEmpty(result.CompilationErrors);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_EmptyFunction_WorksCorrectly()
    {
        var source = @"
def do_nothing():
    pass

def main():
    do_nothing()
    print(""done"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("done\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_SingleLineFunction_WorksCorrectly()
    {
        var source = @"
def identity(x: int) -> int:
    return x

def main():
    print(identity(42))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_ManyParameters_WorksCorrectly()
    {
        var source = @"
def sum_all(a: int, b: int, c: int, d: int, e: int) -> int:
    return a + b + c + d + e

def main():
    print(sum_all(1, 2, 3, 4, 5))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("15\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_FunctionCalledMultipleTimes_WorksCorrectly()
    {
        var source = @"
def increment(x: int) -> int:
    return x + 1

def main():
    result: int = 0
    result = increment(result)
    result = increment(result)
    result = increment(result)
    result = increment(result)
    result = increment(result)
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_FunctionReturningBool_WorksCorrectly()
    {
        var source = @"
def is_positive(n: int) -> bool:
    return n > 0

def main():
    print(is_positive(5))
    print(is_positive(-5))
    print(is_positive(0))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("True\nFalse\nFalse\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_FunctionReturningFloat_WorksCorrectly()
    {
        var source = @"
def average(a: float, b: float) -> float:
    return (a + b) / 2.0

def main():
    print(average(10.0, 20.0))
    print(average(0.0, 100.0))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("15.0\n50.0\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_FunctionReturningString_WorksCorrectly()
    {
        var source = @"
def make_greeting(name: str) -> str:
    return f""Hello, {name}!""

def main():
    print(make_greeting(""World""))
    print(make_greeting(""Sharpy""))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello, World!\nHello, Sharpy!\n", result.StandardOutput);
    }

    #endregion

    #region Complex Algorithm Tests

    [Fact]
    public void Algorithm_GCD_Recursive_WorksCorrectly()
    {
        var source = @"
def gcd(a: int, b: int) -> int:
    if b == 0:
        return a
    return gcd(b, a % b)

def main():
    print(gcd(48, 18))
    print(gcd(100, 35))
    print(gcd(17, 13))
    print(gcd(1071, 462))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("6\n5\n1\n21\n", result.StandardOutput);
    }

    [Fact]
    public void Algorithm_LCM_UsingGCD_WorksCorrectly()
    {
        var source = @"
def gcd(a: int, b: int) -> int:
    if b == 0:
        return a
    return gcd(b, a % b)

def lcm(a: int, b: int) -> int:
    return (a * b) // gcd(a, b)

def main():
    print(lcm(4, 6))
    print(lcm(3, 5))
    print(lcm(12, 18))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("12\n15\n36\n", result.StandardOutput);
    }

    [Fact]
    public void Algorithm_IsPrime_WorksCorrectly()
    {
        var source = @"
def is_prime(n: int) -> bool:
    if n < 2:
        return False
    if n == 2:
        return True
    if n % 2 == 0:
        return False
    i: int = 3
    while i * i <= n:
        if n % i == 0:
            return False
        i += 2
    return True

def main():
    print(is_prime(2))
    print(is_prime(17))
    print(is_prime(18))
    print(is_prime(97))
    print(is_prime(100))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("True\nTrue\nFalse\nTrue\nFalse\n", result.StandardOutput);
    }

    [Fact]
    public void Algorithm_BinarySearch_Style_WorksCorrectly()
    {
        // Simplified version: find square root using binary search approach
        var source = @"
def int_sqrt(n: int) -> int:
    if n < 0:
        return -1
    if n == 0:
        return 0
    low: int = 1
    high: int = n
    result: int = 0
    while low <= high:
        mid: int = (low + high) // 2
        if mid * mid == n:
            return mid
        elif mid * mid < n:
            result = mid
            low = mid + 1
        else:
            high = mid - 1
    return result

def main():
    print(int_sqrt(0))
    print(int_sqrt(1))
    print(int_sqrt(4))
    print(int_sqrt(16))
    print(int_sqrt(17))
    print(int_sqrt(100))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n4\n4\n10\n", result.StandardOutput);
    }

    #endregion
}
