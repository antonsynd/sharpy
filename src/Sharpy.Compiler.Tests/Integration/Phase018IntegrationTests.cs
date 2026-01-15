using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Phase 0.1.8 Integration Tests - Struct Instantiation and Value Semantics
/// Tests struct instantiation with both parameterized and default constructors.
/// </summary>
public class Phase018IntegrationTests : IntegrationTestBase
{
    public Phase018IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void StructInstantiation_WithArguments_CompilesAndRuns()
    {
        // Arrange: Struct with constructor that takes parameters
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p = Point(10, 20)
print(p.x)
print(p.y)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n20\n", result.StandardOutput);
    }

    [Fact]
    public void StructInstantiation_DefaultConstructor_CompilesAndRuns()
    {
        // Arrange: Struct with parameterless constructor
        var source = @"
struct Origin:
    x: int
    y: int

    def __init__(self):
        self.x = 0
        self.y = 0

o = Origin()
print(o.x)
print(o.y)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n0\n", result.StandardOutput);
    }

    [Fact]
    public void StructInstantiation_MultipleInstances_CompilesAndRuns()
    {
        // Arrange: Multiple struct instances
        var source = @"
struct Vector2:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

v1 = Vector2(1.0, 2.0)
v2 = Vector2(3.0, 4.0)
print(v1.x)
print(v1.y)
print(v2.x)
print(v2.y)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void StructInstantiation_WithMethod_CompilesAndRuns()
    {
        // Arrange: Struct with a method
        var source = @"
struct Rectangle:
    width: int
    height: int

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height

    def area(self) -> int:
        return self.width * self.height

r = Rectangle(5, 10)
print(r.area())
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("50\n", result.StandardOutput);
    }

    [Fact]
    public void StructInstantiation_ConstructorOverloading_CompilesAndRuns()
    {
        // Arrange: Struct with overloaded constructors
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self):
        self.x = 0
        self.y = 0

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p1 = Point()
p2 = Point(5, 7)
print(p1.x)
print(p1.y)
print(p2.x)
print(p2.y)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n0\n5\n7\n", result.StandardOutput);
    }

    [Fact]
    public void StructInstantiation_PascalCaseName_PreservesCasing()
    {
        // Arrange: Struct with PascalCase name (following Python/Sharpy conventions)
        var source = @"
struct GameState:
    score: int
    level: int

    def __init__(self, score: int, level: int):
        self.score = score
        self.level = level

state = GameState(100, 1)
print(state.score)
print(state.level)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("100\n1\n", result.StandardOutput);
    }

    [Fact]
    public void StructInstantiation_WithFloatFields_CompilesAndRuns()
    {
        // Arrange: Struct with float fields
        var source = @"
struct Circle:
    radius: float

    def __init__(self, radius: float):
        self.radius = radius

    def area(self) -> float:
        return 3.14159 * self.radius * self.radius

c = Circle(5.0)
print(c.radius)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n", result.StandardOutput);
    }

    [Fact]
    public void StructInstantiation_NestedInExpression_CompilesAndRuns()
    {
        // Arrange: Struct instantiation in an expression
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def get_x(self) -> int:
        return self.x

# Instantiate and immediately call method
x = Point(3, 4).get_x()
print(x)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3\n", result.StandardOutput);
    }
}
