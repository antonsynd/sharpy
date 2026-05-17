using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.4: Control flow statements (if/elif/else, while, for).
/// These tests verify the full compilation pipeline for control flow features.
/// </summary>
[Collection("HeavyCompilation")]
public class Phase014IntegrationTests : IntegrationTestBase
{
    public Phase014IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Spec Example Tests

    [Fact]
    public void SpecExample_Factorial_WhileLoop_CompilesAndComputesCorrectly()
    {
        // From spec: factorial calculation using while loop
        var source = @"
def main():
    n: int = 5
    result: int = 1
    while n > 1:
        result *= n
        n -= 1
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("120\n", result.StandardOutput);
    }

    [Fact]
    public void SpecExample_Factorial_Function_CompilesAndComputesCorrectly()
    {
        // Factorial as a function
        var source = @"
def factorial(n: int) -> int:
    result: int = 1
    while n > 1:
        result *= n
        n -= 1
    return result

def main():
    print(factorial(5))
    print(factorial(0))
    print(factorial(1))
    print(factorial(6))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("120\n1\n1\n720\n", result.StandardOutput);
    }

    [Fact]
    public void SpecExample_FizzBuzz_CompilesAndRunsCorrectly()
    {
        // FizzBuzz-style logic using if/elif/else
        var source = @"
def main():
    for i in range(1, 16):
        if i % 15 == 0:
            print(""FizzBuzz"")
        elif i % 3 == 0:
            print(""Fizz"")
        elif i % 5 == 0:
            print(""Buzz"")
        else:
            print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        var expectedOutput = "1\n2\nFizz\n4\nBuzz\nFizz\n7\n8\nFizz\nBuzz\n11\nFizz\n13\n14\nFizzBuzz\n";
        Assert.Equal(expectedOutput, result.StandardOutput);
    }

    [Fact]
    public void SpecExample_FizzBuzz_AsFunction_CompilesAndRunsCorrectly()
    {
        // FizzBuzz as a reusable function
        var source = @"
def fizzbuzz(n: int):
    for i in range(1, n + 1):
        if i % 15 == 0:
            print(""FizzBuzz"")
        elif i % 3 == 0:
            print(""Fizz"")
        elif i % 5 == 0:
            print(""Buzz"")
        else:
            print(i)

def main():
    fizzbuzz(15)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        var expectedOutput = "1\n2\nFizz\n4\nBuzz\nFizz\n7\n8\nFizz\nBuzz\n11\nFizz\n13\n14\nFizzBuzz\n";
        Assert.Equal(expectedOutput, result.StandardOutput);
    }

    #endregion

    #region While Loop Tests

    [Fact]
    public void WhileLoop_CountDown_WorksCorrectly()
    {
        var source = @"
def main():
    n: int = 5
    while n > 0:
        print(n)
        n -= 1
    print(""Liftoff!"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n4\n3\n2\n1\nLiftoff!\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_SumOfNumbers_WorksCorrectly()
    {
        var source = @"
def main():
    n: int = 10
    sum: int = 0
    while n > 0:
        sum += n
        n -= 1
    print(sum)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("55\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_Power_WorksCorrectly()
    {
        // Calculate 2^10 using while loop
        var source = @"
def main():
    base: int = 2
    exp: int = 10
    result: int = 1
    while exp > 0:
        result *= base
        exp -= 1
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1024\n", result.StandardOutput);
    }

    [Fact]
    public void WhileLoop_GCD_EuclideanAlgorithm_WorksCorrectly()
    {
        var source = @"
def gcd(a: int, b: int) -> int:
    while b != 0:
        temp: int = b
        b = a % b
        a = temp
    return a

def main():
    print(gcd(48, 18))
    print(gcd(100, 35))
    print(gcd(17, 13))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("6\n5\n1\n", result.StandardOutput);
    }

    #endregion

    #region For Loop Tests

    [Fact]
    public void ForLoop_Sum1ToN_WorksCorrectly()
    {
        var source = @"
def main():
    sum: int = 0
    for i in range(1, 11):
        sum += i
    print(sum)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("55\n", result.StandardOutput);
    }

    [Fact]
    public void ForLoop_Multiplication_WorksCorrectly()
    {
        var source = @"
def main():
    product: int = 1
    for i in range(1, 6):
        product *= i
    print(product)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("120\n", result.StandardOutput);
    }

    [Fact]
    public void ForLoop_EvenNumbers_WorksCorrectly()
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
    public void ForLoop_OddNumbers_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(1, 10, 2):
        print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n3\n5\n7\n9\n", result.StandardOutput);
    }

    [Fact]
    public void ForLoop_CountSquares_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(1, 6):
        print(i * i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n4\n9\n16\n25\n", result.StandardOutput);
    }

    #endregion

    #region If/Elif/Else Tests

    [Fact]
    public void IfElse_AbsoluteValue_WorksCorrectly()
    {
        var source = @"
def abs_val(x: int) -> int:
    if x < 0:
        return -x
    else:
        return x

def main():
    print(abs_val(5))
    print(abs_val(-5))
    print(abs_val(0))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n5\n0\n", result.StandardOutput);
    }

    [Fact]
    public void IfElifElse_Sign_WorksCorrectly()
    {
        var source = @"
def sign(x: int) -> int:
    if x > 0:
        return 1
    elif x < 0:
        return -1
    else:
        return 0

def main():
    print(sign(10))
    print(sign(-10))
    print(sign(0))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n-1\n0\n", result.StandardOutput);
    }

    [Fact]
    public void IfElifElse_MaxOfThree_WorksCorrectly()
    {
        var source = @"
def max3(a: int, b: int, c: int) -> int:
    if a >= b and a >= c:
        return a
    elif b >= a and b >= c:
        return b
    else:
        return c

def main():
    print(max3(1, 2, 3))
    print(max3(3, 2, 1))
    print(max3(2, 3, 1))
    print(max3(5, 5, 5))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3\n3\n3\n5\n", result.StandardOutput);
    }

    [Fact]
    public void IfElifElse_MinOfThree_WorksCorrectly()
    {
        var source = @"
def min3(a: int, b: int, c: int) -> int:
    if a <= b and a <= c:
        return a
    elif b <= a and b <= c:
        return b
    else:
        return c

def main():
    print(min3(1, 2, 3))
    print(min3(3, 2, 1))
    print(min3(2, 3, 1))
    print(min3(5, 5, 5))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n1\n1\n5\n", result.StandardOutput);
    }

    #endregion

    #region Break and Continue Tests

    [Fact]
    public void Break_FindFirstMultiple_WorksCorrectly()
    {
        var source = @"
def first_multiple_of(target: int, divisor: int) -> int:
    for i in range(1, target + 1):
        if i % divisor == 0:
            return i
    return -1

def main():
    print(first_multiple_of(10, 3))
    print(first_multiple_of(20, 7))
    print(first_multiple_of(5, 10))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3\n7\n-1\n", result.StandardOutput);
    }

    [Fact]
    public void Break_SearchInRange_WorksCorrectly()
    {
        var source = @"
def main():
    target: int = 7
    found: bool = False
    i: int = 0
    while i < 10:
        if i == target:
            found = True
            break
        i += 1

    if found:
        print(""Found"")
    else:
        print(""Not found"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Found\n", result.StandardOutput);
    }

    [Fact]
    public void Continue_SkipMultiples_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(1, 11):
        if i % 3 == 0:
            continue
        print(i)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n4\n5\n7\n8\n10\n", result.StandardOutput);
    }

    [Fact]
    public void Continue_SumNonMultiples_WorksCorrectly()
    {
        var source = @"
def main():
    sum: int = 0
    for i in range(1, 11):
        if i % 2 == 0:
            continue
        sum += i
    print(sum)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("25\n", result.StandardOutput);
    }

    #endregion

    #region Nested Control Flow Tests

    [Fact]
    public void NestedLoops_MultiplicationTable_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(1, 4):
        for j in range(1, 4):
            print(i * j)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n3\n2\n4\n6\n3\n6\n9\n", result.StandardOutput);
    }

    [Fact]
    public void NestedLoops_TrianglePattern_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(1, 5):
        count: int = 0
        while count < i:
            print(""*"")
            count += 1
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("*\n*\n*\n*\n*\n*\n*\n*\n*\n*\n", result.StandardOutput);
    }

    [Fact]
    public void NestedIfInLoop_ClassifyNumbers_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(-2, 3):
        if i > 0:
            print(""positive"")
        elif i < 0:
            print(""negative"")
        else:
            print(""zero"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("negative\nnegative\nzero\npositive\npositive\n", result.StandardOutput);
    }

    [Fact]
    public void NestedWhileLoops_CountPairs_WorksCorrectly()
    {
        var source = @"
def main():
    count: int = 0
    i: int = 1
    while i <= 3:
        j: int = 1
        while j <= 3:
            if i != j:
                count += 1
            j += 1
        i += 1
    print(count)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("6\n", result.StandardOutput);
    }

    #endregion

    #region Complex Algorithm Tests

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
    for n in range(1, 20):
        if is_prime(n):
            print(n)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("2\n3\n5\n7\n11\n13\n17\n19\n", result.StandardOutput);
    }

    [Fact]
    public void Algorithm_Fibonacci_WhileLoop_WorksCorrectly()
    {
        var source = @"
def fib(n: int) -> int:
    if n <= 1:
        return n
    a: int = 0
    b: int = 1
    i: int = 2
    while i <= n:
        temp: int = a + b
        a = b
        b = temp
        i += 1
    return b

def main():
    for i in range(10):
        print(fib(i))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n1\n2\n3\n5\n8\n13\n21\n34\n", result.StandardOutput);
    }

    [Fact]
    public void Algorithm_CollatzConjecture_WorksCorrectly()
    {
        var source = @"
def collatz_length(n: int) -> int:
    steps: int = 0
    while n != 1:
        if n % 2 == 0:
            n //= 2
        else:
            n = 3 * n + 1
        steps += 1
    return steps

def main():
    print(collatz_length(6))
    print(collatz_length(7))
    print(collatz_length(27))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("8\n16\n111\n", result.StandardOutput);
    }

    [Fact]
    public void Algorithm_CountDigits_WorksCorrectly()
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
    print(count_digits(-9876))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n1\n3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void Algorithm_SumDigits_WorksCorrectly()
    {
        var source = @"
def sum_digits(n: int) -> int:
    if n < 0:
        n = -n
    sum: int = 0
    while n > 0:
        sum += n % 10
        n //= 10
    return sum

def main():
    print(sum_digits(123))
    print(sum_digits(9999))
    print(sum_digits(-456))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("6\n36\n15\n", result.StandardOutput);
    }

    [Fact]
    public void Algorithm_ReverseNumber_WorksCorrectly()
    {
        var source = @"
def reverse_num(n: int) -> int:
    reversed: int = 0
    negative: bool = n < 0
    if negative:
        n = -n
    while n > 0:
        reversed = reversed * 10 + n % 10
        n //= 10
    if negative:
        return -reversed
    return reversed

def main():
    print(reverse_num(123))
    print(reverse_num(9876))
    print(reverse_num(-456))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("321\n6789\n-654\n", result.StandardOutput);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_WhileLoopNeverExecutes_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 10
    while i < 5:
        print(""should not print"")
        i += 1
    print(""done"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("done\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_ForLoopEmptyRange_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(5, 5):
        print(""should not print"")
    print(""done"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("done\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_SingleIterationWhile_WorksCorrectly()
    {
        var source = @"
def main():
    i: int = 0
    while i < 1:
        print(""once"")
        i += 1
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("once\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_SingleIterationFor_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(1):
        print(""once"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("once\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_DeeplyNestedIf_WorksCorrectly()
    {
        var source = @"
def main():
    x: int = 5
    if x > 0:
        if x > 2:
            if x > 4:
                if x > 6:
                    print(""greater than 6"")
                else:
                    print(""between 5 and 6"")
            else:
                print(""between 3 and 4"")
        else:
            print(""between 1 and 2"")
    else:
        print(""non-positive"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("between 5 and 6\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_ImmediateBreak_WorksCorrectly()
    {
        var source = @"
def main():
    for i in range(10):
        break
    print(""done"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("done\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_ImmediateContinue_WorksCorrectly()
    {
        var source = @"
def main():
    count: int = 0
    for i in range(5):
        count += 1
        continue
    print(count)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n", result.StandardOutput);
    }

    #endregion
}
