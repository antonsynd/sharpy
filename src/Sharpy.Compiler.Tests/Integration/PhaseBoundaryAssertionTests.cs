using System.Linq;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Tests that verify internal consistency assertions at phase boundaries
/// do not fire for valid compilations. These tests exercise the code paths
/// in AssertNoDuplicateTypeNames, AssertNoUnresolvedInheritance, and
/// WarnIfUnknownTypes (always-on, emit SHP0904 diagnostics).
/// </summary>
public class PhaseBoundaryAssertionTests
{
    private static CompilationResult CompileSuccessfully(string source)
    {
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");
        Assert.True(result.Success,
            "Compilation failed: " + string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        return result;
    }

    /// <summary>
    /// Compile and assert that no SHP0904 invariant violation warnings are emitted.
    /// Only use for programs that do not involve class member access patterns
    /// (which can produce aspirational UnknownType gaps tracked by WarnIfUnknownTypes).
    /// </summary>
    private static CompilationResult CompileWithNoInvariantViolations(string source)
    {
        var result = CompileSuccessfully(source);

        var invariantViolations = result.Diagnostics.GetWarnings()
            .Where(w => w.Code == "SHP0904")
            .ToList();
        Assert.Empty(invariantViolations);

        return result;
    }

    [Fact]
    public void NoDuplicateTypeNames_MultipleClasses_NoAssertionFires()
    {
        var source = @"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

class Dog:
    breed: str

    def __init__(self, breed: str):
        self.breed = breed

class Cat:
    color: str

    def __init__(self, color: str):
        self.color = color

def main():
    a: Animal = Animal(""Rex"")
    d: Dog = Dog(""Shepherd"")
    c: Cat = Cat(""black"")
";
        CompileSuccessfully(source);
    }

    [Fact]
    public void NoDuplicateTypeNames_ClassAndInterface_NoAssertionFires()
    {
        var source = @"
interface IGreeter:
    def greet(self) -> str

class Person(IGreeter):
    name: str

    def __init__(self, name: str):
        self.name = name

    def greet(self) -> str:
        return ""Hello, "" + self.name

def main():
    p: Person = Person(""Alice"")
    msg: str = p.greet()
";
        CompileSuccessfully(source);
    }

    [Fact]
    public void NoUnresolvedInheritance_SimpleInheritance_NoAssertionFires()
    {
        var source = @"
class Base:
    x: int

    def __init__(self):
        self.x = 0

class Derived(Base):
    y: int

    def __init__(self):
        super().__init__()
        self.y = 1

def main():
    d: Derived = Derived()
";
        CompileSuccessfully(source);
    }

    [Fact]
    public void NoUnresolvedInheritance_InterfaceImplementation_NoAssertionFires()
    {
        var source = @"
interface IShape:
    def area(self) -> float

class Circle(IShape):
    radius: float

    def __init__(self, radius: float):
        self.radius = radius

    def area(self) -> float:
        return 3.14 * self.radius * self.radius

def main():
    c: Circle = Circle(5.0)
    a: float = c.area()
";
        CompileSuccessfully(source);
    }

    [Fact]
    public void NoUnknownTypes_SimpleExpressions_NoAssertionFires()
    {
        // Single-file, no imports, no class member access — verifies no SHP0904 warnings
        var source = @"
def compute(x: int, y: int) -> int:
    z: int = x + y
    return z * 2

def main():
    result: int = compute(3, 4)
";
        CompileWithNoInvariantViolations(source);
    }

    [Fact]
    public void NoUnknownTypes_VariousLiterals_NoAssertionFires()
    {
        var source = @"
def main():
    a: int = 42
    b: float = 3.14
    c: str = ""hello""
    d: bool = True
    e: list[int] = [1, 2, 3]
";
        CompileWithNoInvariantViolations(source);
    }

    [Fact]
    public void AllAssertions_ComplexProgram_NoAssertionFires()
    {
        // Exercises all three assertion paths in a single compilation
        var source = @"
interface IAnimal:
    def speak(self) -> str

class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

class Dog(Animal, IAnimal):
    breed: str

    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

    def speak(self) -> str:
        return ""Woof!""

def main():
    dog: Dog = Dog(""Rex"", ""Shepherd"")
    greeting: str = dog.speak()
    x: int = 42
    y: float = 3.14
";
        CompileSuccessfully(source);
    }

    [Fact]
    public void StatementsHaveSpans_AllStatementTypes_NoAssertionFires()
    {
        // Exercises AssertStatementsHaveSpans — simple function, no class member access
        var source = @"
def factorial(n: int) -> int:
    if n <= 1:
        return 1
    return n * factorial(n - 1)

def main():
    x: int = factorial(5)
";
        CompileWithNoInvariantViolations(source);
    }

    [Fact]
    public void AllSymbolsHaveNames_NestedScopes_NoAssertionFires()
    {
        // Exercises AssertAllSymbolsHaveNames with functions and classes
        var source = @"
class Counter:
    count: int

    def __init__(self):
        self.count = 0

    def increment(self) -> int:
        self.count = self.count + 1
        return self.count

def main():
    c: Counter = Counter()
    c.increment()
    result: int = c.increment()
";
        CompileSuccessfully(source);
    }

    [Fact]
    public void ErrorCompilation_NoInvariantViolationWarnings()
    {
        // When compilation has semantic errors, unknown types from error recovery
        // should NOT trigger SHP0904 invariant violation warnings
        var source = @"
def main():
    x: int = ""not_an_int""
    y: int = True + ""string""
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        // Compilation should fail (semantic errors expected)
        Assert.False(result.Success);

        // But no SHP0904 invariant violation warnings should appear
        var invariantViolations = result.Diagnostics.GetWarnings()
            .Where(w => w.Code == "SHP0904")
            .ToList();
        Assert.Empty(invariantViolations);
    }
}
