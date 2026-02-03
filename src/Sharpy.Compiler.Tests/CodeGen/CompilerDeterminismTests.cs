using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Verifies that the compiler produces identical C# output for the same input across
/// multiple compilations. Determinism is a prerequisite for content-hash-based
/// incremental compilation (5.1).
/// </summary>
public class CompilerDeterminismTests
{
    private readonly ITestOutputHelper _output;

    public CompilerDeterminismTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Compiles source code N times and returns all generated C# outputs.
    /// </summary>
    private List<string> CompileNTimes(string source, string fileName, int n)
    {
        var results = new List<string>();
        for (int i = 0; i < n; i++)
        {
            var compiler = new Compiler();
            var result = compiler.Compile(source, fileName);
            Assert.True(result.Success, $"Compilation {i + 1} failed: {string.Join("; ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
            Assert.NotNull(result.GeneratedCSharpCode);
            results.Add(result.GeneratedCSharpCode!);
        }
        return results;
    }

    private void AssertDeterministic(string source, string fileName = "test.spy", int iterations = 3)
    {
        var outputs = CompileNTimes(source, fileName, iterations);
        for (int i = 1; i < outputs.Count; i++)
        {
            if (outputs[0] != outputs[i])
            {
                _output.WriteLine($"Compilation 1 and {i + 1} produced different output.");
                _output.WriteLine($"=== Compilation 1 (first 500 chars) ===");
                _output.WriteLine(outputs[0][..System.Math.Min(500, outputs[0].Length)]);
                _output.WriteLine($"=== Compilation {i + 1} (first 500 chars) ===");
                _output.WriteLine(outputs[i][..System.Math.Min(500, outputs[i].Length)]);
            }
            Assert.Equal(outputs[0], outputs[i]);
        }
    }

    [Fact]
    public void HelloWorld_IsDeterministic()
    {
        AssertDeterministic("""
            def main():
                print("Hello, World!")
            """);
    }

    [Fact]
    public void ArithmeticExpressions_IsDeterministic()
    {
        AssertDeterministic("""
            def main():
                x: int = 10
                y: int = 20
                z: int = x + y * 2 - 3
                print(z)
            """);
    }

    [Fact]
    public void FunctionDefinitions_IsDeterministic()
    {
        AssertDeterministic("""
            def add(a: int, b: int) -> int:
                return a + b

            def greet(name: str = "World") -> str:
                return f"Hello, {name}!"

            def main():
                print(add(1, 2))
                print(greet())
                print(greet("Sharpy"))
            """);
    }

    [Fact]
    public void ClassWithInit_IsDeterministic()
    {
        AssertDeterministic("""
            class Point:
                x: int
                y: int

                def __init__(self, x: int, y: int):
                    self.x = x
                    self.y = y

                @override
                def __str__(self) -> str:
                    return f"({self.x}, {self.y})"

            def main():
                p: Point = Point(3, 4)
                print(p)
            """);
    }

    [Fact]
    public void Inheritance_IsDeterministic()
    {
        AssertDeterministic("""
            class Animal:
                name: str

                def __init__(self, name: str):
                    self.name = name

                @virtual
                def speak(self) -> str:
                    return "..."

            class Dog(Animal):
                def __init__(self, name: str):
                    super().__init__(name)

                @override
                def speak(self) -> str:
                    return "Woof!"

            def main():
                d: Dog = Dog("Rex")
                print(d.speak())
            """);
    }

    [Fact]
    public void ListComprehension_IsDeterministic()
    {
        AssertDeterministic("""
            def main():
                numbers: list[int] = [1, 2, 3, 4, 5]
                squares: list[int] = [x * x for x in numbers]
                evens: list[int] = [x for x in numbers if x % 2 == 0]
                print(squares)
                print(evens)
            """);
    }

    [Fact]
    public void DictComprehension_IsDeterministic()
    {
        AssertDeterministic("""
            def main():
                names: list[str] = ["a", "bb", "ccc"]
                lengths: dict[str, int] = {n: len(n) for n in names}
                print(lengths)
            """);
    }

    [Fact]
    public void FStrings_IsDeterministic()
    {
        AssertDeterministic("""
            def main():
                name: str = "World"
                count: int = 42
                msg: str = f"Hello {name}, count={count}"
                print(msg)
            """);
    }

    [Fact]
    public void ForLoopAndWhile_IsDeterministic()
    {
        AssertDeterministic("""
            def main():
                total: int = 0
                for i in range(10):
                    total = total + i
                print(total)

                x: int = 5
                while x > 0:
                    x = x - 1
                print(x)
            """);
    }

    [Fact]
    public void IfElifElse_IsDeterministic()
    {
        AssertDeterministic("""
            def main():
                x: int = 10
                if x > 20:
                    print("big")
                elif x > 5:
                    print("medium")
                else:
                    print("small")
            """);
    }

    [Fact]
    public void StaticMethods_IsDeterministic()
    {
        AssertDeterministic("""
            class MathUtil:
                def square(x: int) -> int:
                    return x * x

            def main():
                print(MathUtil.square(5))
            """);
    }

    [Fact]
    public void TypeAliases_IsDeterministic()
    {
        AssertDeterministic("""
            type Name = str
            type Age = int

            def greet(name: Name, age: Age) -> str:
                return f"{name} is {age}"

            def main():
                print(greet("Alice", 30))
            """);
    }

    [Fact]
    public void Enums_IsDeterministic()
    {
        AssertDeterministic("""
            enum Color:
                RED = 1
                GREEN = 2
                BLUE = 3

            def main():
                c: Color = Color.RED
                print(c == Color.RED)
            """);
    }

    [Fact]
    public void Structs_IsDeterministic()
    {
        AssertDeterministic("""
            struct Point:
                x: int
                y: int

                def __init__(self, x: int, y: int):
                    self.x = x
                    self.y = y

            def main():
                p1: Point = Point(1, 2)
                p2: Point = p1
                print(p1.x)
                print(p2.y)
            """);
    }

    [Fact]
    public void Generics_IsDeterministic()
    {
        AssertDeterministic("""
            class Box[T]:
                value: T

                def __init__(self, value: T):
                    self.value = value

                def get(self) -> T:
                    return self.value

            def main():
                b: Box[int] = Box[int](42)
                print(b.get())
            """);
    }

    [Fact]
    public void MultipleFeaturesCombined_IsDeterministic()
    {
        AssertDeterministic("""
            type Score = int

            class Player:
                name: str
                score: Score

                def __init__(self, name: str, score: Score):
                    self.name = name
                    self.score = score

                @override
                def __str__(self) -> str:
                    return f"{self.name}: {self.score}"

            class Team:
                players: list[Player]

                def __init__(self):
                    self.players = []

                def add(self, p: Player):
                    self.players.append(p)

                def total_score(self) -> int:
                    total: int = 0
                    for p in self.players:
                        total = total + p.score
                    return total

            def main():
                team: Team = Team()
                team.add(Player("Alice", 10))
                team.add(Player("Bob", 20))

                scores: list[int] = [p.score for p in team.players]
                high: list[int] = [s for s in scores if s > 15]

                if len(high) > 0:
                    print(f"High scores: {high}")
                else:
                    print("No high scores")

                print(f"Total: {team.total_score()}")
            """);
    }
}
