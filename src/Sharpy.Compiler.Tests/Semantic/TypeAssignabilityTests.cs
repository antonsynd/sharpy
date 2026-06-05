using Xunit;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// End-to-end tests for type assignability through implemented interfaces and
/// generic variance (#827): builtin collections assignable to CLR interfaces,
/// covariant widening, invariant rejection, and user-defined interface
/// implementations.
/// </summary>
[Collection("HeavyCompilation")]
public class TypeAssignabilityTests : IntegrationTestBase
{
    public TypeAssignabilityTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ListInt_AssignableTo_IEnumerableInt()
    {
        // Same type arguments: list[int] → IEnumerable[int] via list's interface list
        var source = @"
def total(items: IEnumerable[int]) -> int:
    s: int = 0
    for i in items:
        s += i
    return s

def main():
    xs: list[int] = [1, 2, 3]
    print(total(xs))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("6\n", result.StandardOutput);
    }

    [Fact]
    public void ListDog_AssignableTo_IEnumerableAnimal_Covariant()
    {
        // IEnumerable[out T] is covariant, so list[Dog] → IEnumerable[Animal]
        var source = @"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

def first_name(animals: IEnumerable[Animal]) -> str:
    for a in animals:
        return a.name
    return ""none""

def main():
    dogs: list[Dog] = [Dog(""Rex""), Dog(""Fido"")]
    print(first_name(dogs))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("Rex\n", result.StandardOutput);
    }

    [Fact]
    public void ListDog_NotAssignableTo_IListAnimal_Invariant()
    {
        // IList[T] is invariant in T, so list[Dog] must be rejected where
        // IList[Animal] is expected. The Sharpy type checker catches this at the
        // semantic level (SPY0220) via the tri-state variance check (#829).
        var source = @"
from system.collections.generic import IList

class Animal:
    pass

class Dog(Animal):
    pass

def mutate(items: IList[Animal]) -> None:
    pass

def main():
    dogs: list[Dog] = [Dog()]
    mutate(dogs)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail: IList[T] is invariant in T");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors).ToLowerInvariant();
        Assert.Contains("cannot pass argument", errorText);
    }

    [Fact]
    public void ListInt_NotAssignableTo_IListObject_Invariant()
    {
        // Invariant rejection at the Sharpy level: int → object would satisfy
        // covariance, but IList[T] is invariant, so list[int] → IList[object] fails.
        var source = @"
from system.collections.generic import IList

def mutate(items: IList[object]) -> None:
    pass

def main():
    nums: list[int] = [1, 2, 3]
    mutate(nums)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail: IList[T] is invariant in T");
        Assert.NotEmpty(result.CompilationErrors);
        var errorText = string.Join(" ", result.CompilationErrors);
        Assert.Contains("Cannot pass argument of type 'list[int]' to parameter of type 'IList[object]'", errorText);
    }

    [Fact]
    public void UserDefinedClass_AssignableTo_ImplementedInterface()
    {
        // A user-defined class is assignable to the Sharpy interface it implements
        var source = @"
interface IShape:
    def corners(self) -> int: ...

class Square(IShape):
    def __init__(self):
        pass

    @override
    def corners(self) -> int:
        return 4

def describe(shape: IShape) -> None:
    print(shape.corners())

def main():
    s = Square()
    describe(s)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("4\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_AssignableTo_ContravariantInterface()
    {
        // ISink[in T] is contravariant: Sink[Animal] (implements ISink[Animal])
        // is assignable where ISink[Dog] is expected
        var source = @"
interface ISink[in T]:
    def accept(self, value: T) -> None: ...

class Animal:
    pass

class Dog(Animal):
    pass

class Sink[T](ISink[T]):
    def __init__(self):
        pass

    @override
    def accept(self, value: T) -> None:
        print(""accepted"")

def main():
    s: ISink[Dog] = Sink[Animal]()
    s.accept(Dog())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("accepted\n", result.StandardOutput);
    }
}
