using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for type declaration code generation (RoslynEmitter.TypeDeclarations.cs).
/// Focuses on gaps not covered by RoslynEmitterDefinitionTests.TypeTests:
/// generic classes, sealed/abstract modifiers, enums with values, interfaces with methods,
/// and class inheritance.
/// </summary>
[Collection("HeavyCompilation")]
public class RoslynEmitterTypeDeclarationTests : IntegrationTestBase
{
    public RoslynEmitterTypeDeclarationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void GenericClass_WithTypeParameter_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
class Cell[T]:
    value: T

    def __init__(self, initial: T):
        self.value = initial

    def get(self) -> T:
        return self.value

    def set(self, new_value: T) -> None:
        self.value = new_value

def main():
    c = Cell[int](10)
    print(c.get())
    c.set(20)
    print(c.get())
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("10\n20\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_GeneratesTypeParameterInCSharp()
    {
        var result = CompileAndExecute(@"
class Box[T]:
    item: T

    def __init__(self, item: T):
        self.item = item

    def unwrap(self) -> T:
        return self.item

def main():
    b = Box[str](""hello"")
    print(b.unwrap())
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Contains("class Box<T>", result.GeneratedCSharp);
        Assert.Equal("hello\n", result.StandardOutput);
    }

    [Fact]
    public void StructDefinition_WithFieldsAndMethods_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
struct Point:
    x: int
    y: int

    def manhattan_distance(self) -> int:
        result: int = self.x + self.y
        if result < 0:
            result = -result
        return result

def main():
    p: Point = Point()
    p.x = 3
    p.y = 4
    print(p.manhattan_distance())
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("7\n", result.StandardOutput);
    }

    [Fact]
    public void EnumDefinition_WithExplicitValues_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
enum Priority:
    LOW = 10
    MEDIUM = 20
    HIGH = 30

def main():
    p = Priority.MEDIUM
    print(p.value)
    h = Priority.HIGH
    print(h.value)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("20\n30\n", result.StandardOutput);
    }

    [Fact]
    public void InterfaceDefinition_WithMethods_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
interface IGreeter:
    def greet(self) -> str:
        ...

class HelloGreeter(IGreeter):
    def greet(self) -> str:
        return ""Hello!""

class WorldGreeter(IGreeter):
    def greet(self) -> str:
        return ""World!""

def say(g: IGreeter) -> None:
    print(g.greet())

def main():
    say(HelloGreeter())
    say(WorldGreeter())
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("Hello!\nWorld!\n", result.StandardOutput);
    }

    [Fact]
    public void FinalClass_GeneratesSealedModifierInCSharp()
    {
        var result = CompileAndExecute(@"
@final
class Config:
    name: str

    def __init__(self, name: str):
        self.name = name

    def describe(self) -> str:
        return self.name

def main():
    c = Config(""production"")
    print(c.describe())
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Contains("sealed", result.GeneratedCSharp);
        Assert.Contains("class Config", result.GeneratedCSharp);
        Assert.Equal("production\n", result.StandardOutput);
    }

    [Fact]
    public void ClassInheritance_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    @virtual
    def speak(self) -> str:
        return self.name + "" says nothing""

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    @override
    def speak(self) -> str:
        return self.name + "" says woof""

def main():
    d = Dog(""Rex"")
    print(d.speak())
    a: Animal = d
    print(a.speak())
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("Rex says woof\nRex says woof\n", result.StandardOutput);
    }

    [Fact]
    public void EnumDefinition_WithNameAccess_ProducesCorrectOutput()
    {
        var result = CompileAndExecute(@"
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

def main():
    c = Color.GREEN
    print(c.name)
    print(c.value)
");
        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("Green\n2\n", result.StandardOutput);
    }
}
