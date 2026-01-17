using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.8: Structs and Enums.
/// These tests verify the full compilation pipeline for value types including:
/// - Struct definitions with fields and methods
/// - Struct constructors with mandatory field initialization
/// - Struct instantiation and assignment (value semantics)
/// - Enum definitions with explicit values
/// - Enum usage in code (accessing enum members)
/// - Int and string enum values (with type consistency)
/// </summary>
public class Phase018IntegrationTests : IntegrationTestBase
{
    public Phase018IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Basic Struct Tests

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

    [Fact]
    public void BasicStruct_CompilesAndRuns()
    {
        var source = @"
struct Point:
    x: int
    y: int

p = Point()
p.x = 10
p.y = 20
print(p.x)
print(p.y)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n20\n", result.StandardOutput);
    }

    [Fact]
    public void StructWithMultipleFields_AllTypesWork()
    {
        var source = @"
struct Person:
    name: str
    age: int
    height: float
    active: bool

    def __init__(self, name: str, age: int, height: float, active: bool):
        self.name = name
        self.age = age
        self.height = height
        self.active = active

p = Person(""Alice"", 30, 5.5, True)
print(p.name)
print(p.age)
print(p.height)
print(p.active)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Alice\n30\n5.5\nTrue\n", result.StandardOutput);
    }

    [Fact]
    public void StructFieldAccess_WorksCorrectly()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p = Point(100, 200)
x_val = p.x
y_val = p.y
print(x_val)
print(y_val)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("100\n200\n", result.StandardOutput);
    }

    [Fact]
    public void StructWithComputedField_CompilesAndRuns()
    {
        var source = @"
struct Vector2:
    x: float
    y: float
    length: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y
        self.length = (x * x + y * y) ** 0.5

v = Vector2(3.0, 4.0)
print(v.x)
print(v.y)
print(v.length)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3\n4\n5\n", result.StandardOutput);
    }

    #endregion

    #region Struct Assignment and Value Semantics Tests

    [Fact]
    public void StructAssignment_CopiesValue()
    {
        // Structs have value semantics in C#, so assignment should copy
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p1 = Point(10, 20)
p2 = p1
p2.x = 100
print(p1.x)
print(p2.x)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // In value semantics, p1.x should still be 10 because p2 is a copy
        Assert.Equal("10\n100\n", result.StandardOutput);
    }

    [Fact]
    public void StructReassignment_WorksCorrectly()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p = Point(1, 2)
print(p.x)
p = Point(3, 4)
print(p.x)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n3\n", result.StandardOutput);
    }

    [Fact]
    public void StructFieldMutation_WorksCorrectly()
    {
        var source = @"
struct Counter:
    count: int

    def __init__(self):
        self.count = 0

    def increment(self) -> None:
        self.count = self.count + 1

c = Counter()
print(c.count)
c.increment()
print(c.count)
c.increment()
print(c.count)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n", result.StandardOutput);
    }

    #endregion

    #region Struct Method Tests

    [Fact]
    public void StructMethod_CanAccessFields()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def distance_from_origin(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5

p = Point(3, 4)
print(p.distance_from_origin())
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n", result.StandardOutput);
    }

    [Fact]
    public void StructMethod_CanModifyFields()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def move(self, dx: int, dy: int) -> None:
        self.x = self.x + dx
        self.y = self.y + dy

p = Point(10, 20)
print(p.x)
print(p.y)
p.move(5, 3)
print(p.x)
print(p.y)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n20\n15\n23\n", result.StandardOutput);
    }

    [Fact]
    public void StructMethod_WithMultipleParameters()
    {
        var source = @"
struct Calculator:
    result: int

    def __init__(self):
        self.result = 0

    def add(self, a: int, b: int) -> int:
        self.result = a + b
        return self.result

    def multiply(self, a: int, b: int) -> int:
        self.result = a * b
        return self.result

calc = Calculator()
print(calc.add(5, 3))
print(calc.result)
print(calc.multiply(4, 7))
print(calc.result)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("8\n8\n28\n28\n", result.StandardOutput);
    }

    [Fact]
    public void StructWithMethods_CompilesAndRuns()
    {
        var source = @"
struct Rectangle:
    width: int
    height: int

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height

    def area(self) -> int:
        return self.width * self.height

    def perimeter(self) -> int:
        return 2 * (self.width + self.height)

r = Rectangle(5, 10)
print(r.area())
print(r.perimeter())
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("50\n30\n", result.StandardOutput);
    }

    #endregion

    #region Basic Enum Tests

    [Fact]
    public void BasicIntEnum_CompilesAndRuns()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    INACTIVE = 2

s = Status.ACTIVE
print(s)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n", result.StandardOutput);
    }

    [Fact]
    public void BasicStringEnum_CompilesAndRuns()
    {
        var source = @"
enum Color:
    RED = ""red""
    GREEN = ""green""
    BLUE = ""blue""

c = Color.RED
print(c)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("red\n", result.StandardOutput);
    }

    [Fact]
    public void EnumUsage_MultipleMembers()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    INACTIVE = 2

print(Status.PENDING)
print(Status.ACTIVE)
print(Status.INACTIVE)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n", result.StandardOutput);
    }

    [Fact]
    public void EnumAssignment_WorksCorrectly()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    INACTIVE = 2

s1 = Status.PENDING
print(s1)
s1 = Status.ACTIVE
print(s1)
s1 = Status.INACTIVE
print(s1)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n", result.StandardOutput);
    }

    [Fact]
    public void StringEnum_AllMembers()
    {
        var source = @"
enum LogLevel:
    DEBUG = ""debug""
    INFO = ""info""
    WARNING = ""warning""
    ERROR = ""error""

print(LogLevel.DEBUG)
print(LogLevel.INFO)
print(LogLevel.WARNING)
print(LogLevel.ERROR)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("debug\ninfo\nwarning\nerror\n", result.StandardOutput);
    }

    [Fact]
    public void EnumWithNegativeValues_Works()
    {
        var source = @"
enum Direction:
    UP = -1
    NEUTRAL = 0
    DOWN = 1

print(Direction.UP)
print(Direction.NEUTRAL)
print(Direction.DOWN)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("-1\n0\n1\n", result.StandardOutput);
    }

    [Fact]
    public void EnumWithLargeValues_Works()
    {
        var source = @"
enum Flags:
    FLAG_A = 1000000
    FLAG_B = 2000000
    FLAG_C = 3000000

print(Flags.FLAG_A)
print(Flags.FLAG_B)
print(Flags.FLAG_C)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1000000\n2000000\n3000000\n", result.StandardOutput);
    }

    #endregion

    #region Enum with Conditionals Tests

    [Fact]
    public void EnumInConditional_Works()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    INACTIVE = 2

s = Status.ACTIVE
if s == Status.ACTIVE:
    print(""Active"")
else:
    print(""Not Active"")
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Active\n", result.StandardOutput);
    }

    [Fact]
    public void EnumComparison_MultipleConditions()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    INACTIVE = 2

s = Status.PENDING
if s == Status.PENDING:
    print(""Pending"")
elif s == Status.ACTIVE:
    print(""Active"")
else:
    print(""Inactive"")
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Pending\n", result.StandardOutput);
    }

    #endregion

    #region Struct and Enum Integration Tests

    [Fact]
    public void StructWithEnumField_CompilesAndRuns()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    INACTIVE = 2

struct Task:
    name: str
    status: int

    def __init__(self, name: str, status: int):
        self.name = name
        self.status = status

t = Task(""My Task"", Status.ACTIVE)
print(t.name)
print(t.status)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("My Task\n1\n", result.StandardOutput);
    }

    [Fact]
    public void StructMethod_UsingEnum()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETE = 2

struct Task:
    status: int

    def __init__(self):
        self.status = Status.PENDING

    def activate(self) -> None:
        self.status = Status.ACTIVE

    def complete(self) -> None:
        self.status = Status.COMPLETE

t = Task()
print(t.status)
t.activate()
print(t.status)
t.complete()
print(t.status)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n1\n2\n", result.StandardOutput);
    }

    [Fact]
    public void MultipleStructs_WithEnum()
    {
        var source = @"
enum Priority:
    LOW = 1
    MEDIUM = 2
    HIGH = 3

struct Task:
    name: str
    priority: int

    def __init__(self, name: str, priority: int):
        self.name = name
        self.priority = priority

struct Project:
    title: str
    default_priority: int

    def __init__(self, title: str):
        self.title = title
        self.default_priority = Priority.MEDIUM

p = Project(""My Project"")
t = Task(""Important Task"", Priority.HIGH)
print(p.title)
print(p.default_priority)
print(t.name)
print(t.priority)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("My Project\n2\nImportant Task\n3\n", result.StandardOutput);
    }

    #endregion

    #region Complex Struct Tests

    [Fact]
    public void NestedStructs_AsFields()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

struct Line:
    start_x: int
    start_y: int
    end_x: int
    end_y: int

    def __init__(self, start_x: int, start_y: int, end_x: int, end_y: int):
        self.start_x = start_x
        self.start_y = start_y
        self.end_x = end_x
        self.end_y = end_y

    def length(self) -> float:
        dx = self.end_x - self.start_x
        dy = self.end_y - self.start_y
        return (dx * dx + dy * dy) ** 0.5

p1 = Point(0, 0)
p2 = Point(3, 4)
line = Line(p1.x, p1.y, p2.x, p2.y)
print(line.length())
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n", result.StandardOutput);
    }

    [Fact]
    public void StructWithMultipleMethods_ComplexLogic()
    {
        var source = @"
struct Rectangle:
    width: int
    height: int

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height

    def area(self) -> int:
        return self.width * self.height

    def perimeter(self) -> int:
        return 2 * (self.width + self.height)

    def is_square(self) -> bool:
        return self.width == self.height

    def scale(self, factor: int) -> None:
        self.width = self.width * factor
        self.height = self.height * factor

r = Rectangle(4, 4)
print(r.is_square())
print(r.area())
r.scale(2)
print(r.area())
print(r.perimeter())
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("True\n16\n64\n32\n", result.StandardOutput);
    }

    #endregion

    #region Multiple Enums Tests

    [Fact]
    public void MultipleEnums_IndependentUsage()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1

enum Priority:
    LOW = 10
    HIGH = 20

s = Status.ACTIVE
p = Priority.HIGH
print(s)
print(p)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n20\n", result.StandardOutput);
    }

    [Fact]
    public void MixedEnumTypes_IntAndString()
    {
        var source = @"
enum IntEnum:
    A = 1
    B = 2

enum StrEnum:
    X = ""x""
    Y = ""y""

i = IntEnum.A
s = StrEnum.X
print(i)
print(s)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\nx\n", result.StandardOutput);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void Error_StructConstructor_MissingFieldInitialization()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int):
        self.x = x
        # ERROR: Field 'y' is not initialized
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for missing field initialization");
        Assert.NotEmpty(result.CompilationErrors);
        Assert.Contains(result.CompilationErrors, e => e.Contains("Struct 'Point' constructor must initialize all fields"));
    }

    // Enum validation tests - these should work as they test semantic errors, not runtime behavior
    [Fact]
    public void Error_EnumMemberWithoutValue()
    {
        var source = @"
enum Status:
    PENDING
    ACTIVE = 1
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for enum member without value");
        Assert.NotEmpty(result.CompilationErrors);
        Assert.Contains(result.CompilationErrors, e => e.Contains("Enum member 'PENDING' requires an explicit value"));
    }

    [Fact]
    public void Error_EnumMixedIntAndString()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = ""active""
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for mixed enum value types");
        Assert.NotEmpty(result.CompilationErrors);
        Assert.Contains(result.CompilationErrors, e => e.Contains("All enum values must be the same type"));
    }

    [Fact]
    public void Error_EnumInvalidValueType()
    {
        var source = @"
enum Status:
    PENDING = 3.14
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for invalid enum value type");
        Assert.NotEmpty(result.CompilationErrors);
        Assert.Contains(result.CompilationErrors, e => e.Contains("Enum values must be int or str"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_StructWithNoFields()
    {
        var source = @"
struct Empty:
    def __init__(self):
        pass

e = Empty()
print(""Created"")
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Created\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_SingleMemberEnum()
    {
        var source = @"
enum Single:
    ONLY = 42

s = Single.ONLY
print(s)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_StructFieldsInDifferentOrder()
    {
        var source = @"
struct Point:
    x: int
    y: int
    z: int

    def __init__(self, a: int, b: int, c: int):
        self.z = c
        self.x = a
        self.y = b

p = Point(1, 2, 3)
print(p.x)
print(p.y)
print(p.z)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n3\n", result.StandardOutput);
    }

    #endregion

    #region Comprehensive Integration Tests

    [Fact]
    public void ComprehensiveTest_StructsAndEnums_Together()
    {
        var source = @"
enum Priority:
    LOW = 1
    MEDIUM = 2
    HIGH = 3

enum Status:
    TODO = 0
    IN_PROGRESS = 1
    DONE = 2

struct Task:
    title: str
    priority: int
    status: int
    points: int

    def __init__(self, title: str):
        self.title = title
        self.priority = Priority.MEDIUM
        self.status = Status.TODO
        self.points = 0

    def start(self) -> None:
        self.status = Status.IN_PROGRESS

    def complete(self, points: int) -> None:
        self.status = Status.DONE
        self.points = points

    def escalate(self) -> None:
        if self.priority == Priority.MEDIUM:
            self.priority = Priority.HIGH

t = Task(""Build feature"")
print(t.title)
print(t.priority)
print(t.status)
t.start()
print(t.status)
t.escalate()
print(t.priority)
t.complete(5)
print(t.status)
print(t.points)
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Build feature\n2\n0\n1\n3\n2\n5\n", result.StandardOutput);
    }

    [Fact]
    public void ComprehensiveTest_MultipleStructs_WorkingTogether()
    {
        var source = @"
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

struct Circle:
    center_x: int
    center_y: int
    radius: int

    def __init__(self, center_x: int, center_y: int, radius: int):
        self.center_x = center_x
        self.center_y = center_y
        self.radius = radius

    def area(self) -> float:
        return 3.14159 * self.radius * self.radius

    def contains_point(self, px: int, py: int) -> bool:
        dx = px - self.center_x
        dy = py - self.center_y
        distance_squared = dx * dx + dy * dy
        return distance_squared <= self.radius * self.radius

p = Point(5, 5)
c = Circle(0, 0, 10)
print(c.area())
print(c.contains_point(p.x, p.y))
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        // Area of circle with radius 10: π * 100 ≈ 314.159
        // Point (5,5) distance from origin: sqrt(50) ≈ 7.07, which is < 10, so True
        Assert.Contains("314.159", result.StandardOutput);
        Assert.Contains("True", result.StandardOutput);
    }

    [Fact]
    public void ComprehensiveTest_StructWithStringEnum()
    {
        var source = @"
enum LogLevel:
    DEBUG = ""DEBUG""
    INFO = ""INFO""
    WARNING = ""WARNING""
    ERROR = ""ERROR""

struct LogEntry:
    message: str
    level: str

    def __init__(self, message: str, level: str):
        self.message = message
        self.level = level

    def format(self) -> str:
        return f""[{self.level}] {self.message}""

log = LogEntry(""System started"", LogLevel.INFO)
print(log.format())
";
        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("[INFO] System started\n", result.StandardOutput);
    }

    #endregion
}
