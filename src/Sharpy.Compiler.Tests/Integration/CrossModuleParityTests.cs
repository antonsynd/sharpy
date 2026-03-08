using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Tests that verify parity between single-file and multi-file compilation modes.
/// Each test runs in both modes and asserts identical output.
/// </summary>
public class CrossModuleParityTests : DualModeIntegrationTestBase
{
    public CrossModuleParityTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void Inheritance_BasicClassHierarchy(string mode)
    {
        var source = @"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    def speak(self) -> str:
        return self.name

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    def speak(self) -> str:
        return f""{self.name} says Woof""

def main():
    d = Dog(""Rex"")
    print(d.speak())
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("Rex says Woof\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void Interface_ImplementsProtocol(string mode)
    {
        var source = @"
class Measurable:
    value: int

    def __init__(self, value: int):
        self.value = value

    def __len__(self) -> int:
        return self.value

def main():
    m = Measurable(42)
    print(len(m))
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("42\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void Properties_AutoProperty(string mode)
    {
        var source = @"
class Person:
    property name: str
    property age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

def main():
    p = Person(""Alice"", 30)
    print(p.name)
    print(p.age)
    p.name = ""Bob""
    p.age = 25
    print(p.name)
    print(p.age)
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("Alice\n30\nBob\n25\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void Generics_GenericFunction(string mode)
    {
        var source = @"
def identity[T](x: T) -> T:
    return x

def main():
    print(identity(42))
    print(identity(""hello""))
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("42\nhello\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void Comprehensions_ListComprehension(string mode)
    {
        var source = @"
def main():
    squares: list[int] = [x * x for x in range(5)]
    print(squares)
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("[0, 1, 4, 9, 16]\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void StrMethod_DunderStr(string mode)
    {
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __str__(self) -> str:
        return f""({self.x}, {self.y})""

def main():
    p = Point(3, 4)
    print(p)
    print(str(p))
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("(3, 4)\n(3, 4)\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void VoidReturn_NoneReturn(string mode)
    {
        var source = @"
def greet(name: str) -> None:
    print(f""Hello, {name}!"")

def main():
    greet(""World"")
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("Hello, World!\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void Enums_BasicEnum(string mode)
    {
        var source = @"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

def main():
    c: Color = Color.RED
    print(c == Color.RED)
    print(c == Color.BLUE)
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("True\nFalse\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void ControlFlow_MatchStatement(string mode)
    {
        var source = @"
def describe(x: int) -> str:
    match x:
        case 1:
            return ""one""
        case 2:
            return ""two""
        case _:
            return ""other""

def main():
    print(describe(1))
    print(describe(2))
    print(describe(99))
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("one\ntwo\nother\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void Collections_DictOperations(string mode)
    {
        var source = @"
def main():
    d: dict[str, int] = {""a"": 1, ""b"": 2, ""c"": 3}
    print(len(d))
    print(d[""b""])
    d[""d""] = 4
    print(len(d))
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("3\n2\n4\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void Lambdas_BasicLambda(string mode)
    {
        var source = @"
def main():
    nums: list[int] = [3, 1, 4, 1, 5]
    sorted_nums = sorted(nums)
    print(sorted_nums)
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("[1, 1, 3, 4, 5]\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("multi")]
    public void Tuples_TupleUnpacking(string mode)
    {
        var source = @"
def swap(a: int, b: int) -> tuple[int, int]:
    return (b, a)

def main():
    x, y = swap(1, 2)
    print(x)
    print(y)
";
        var result = Execute(source, mode);
        Assert.True(result.Success, FormatErrors(result));
        Assert.Equal("2\n1\n", result.StandardOutput);
    }

    private ExecutionResult Execute(string source, string mode)
    {
        return mode switch
        {
            "single" => CompileAndExecuteSingleFile(source),
            "multi" => CompileAndExecuteMultiFile(source),
            _ => throw new ArgumentException($"Unknown mode: {mode}")
        };
    }

    private static string FormatErrors(ExecutionResult result)
    {
        return string.Join("\n", result.CompilationErrors);
    }
}
