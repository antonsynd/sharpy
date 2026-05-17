using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.6: Class definitions with fields, constructors, and methods.
/// These tests verify the full compilation pipeline for class-related features including:
/// - Field declarations with name mangling
/// - Constructor generation from __init__ methods
/// - Constructor overloading
/// - Instance methods
/// - Static methods
/// - Class instantiation
/// </summary>
[Collection("HeavyCompilation")]
public class Phase016IntegrationTests : IntegrationTestBase
{
    public Phase016IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Simple Class Tests

    [Fact]
    public void SimpleClass_EmptyClass_CompilesAndInstantiates()
    {
        // Empty class should compile and be instantiable
        var source = @"
class Empty:
    pass

def main():
    e = Empty()
    print(""created"")
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("created\n", result.StandardOutput);
    }

    [Fact]
    public void SimpleClass_SingleField_CompilesAndInitializes()
    {
        // Class with single field, no explicit constructor
        var source = @"
class Counter:
    count: int = 0

def main():
    c = Counter()
    print(c.count)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n", result.StandardOutput);
    }

    [Fact]
    public void SimpleClass_MultipleFields_CompilesAndInitializes()
    {
        // Class with multiple fields of different types
        var source = @"
class Person:
    name: str = """"
    age: int = 0
    active: bool = True

def main():
    p = Person()
    print(p.name)
    print(p.age)
    print(p.active)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("\n0\nTrue\n", result.StandardOutput);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_SimpleInit_CompilesAndRuns()
    {
        // Class with simple __init__ constructor
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p = Point(3, 4)
    print(p.x)
    print(p.y)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3\n4\n", result.StandardOutput);
    }

    [Fact]
    public void Constructor_DefaultConstructor_CompilesAndRuns()
    {
        // Class with parameterless __init__
        var source = @"
class Origin:
    x: int
    y: int

    def __init__(self):
        self.x = 0
        self.y = 0

def main():
    o = Origin()
    print(o.x)
    print(o.y)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n0\n", result.StandardOutput);
    }

    [Fact]
    public void Constructor_Overloading_TwoConstructors_CompilesAndRuns()
    {
        // Class with two __init__ constructors (overloading)
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self):
        self.x = 0
        self.y = 0

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p1 = Point()
    p2 = Point(5, 10)
    print(p1.x)
    print(p1.y)
    print(p2.x)
    print(p2.y)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n0\n5\n10\n", result.StandardOutput);
    }

    [Fact]
    public void Constructor_Overloading_ThreeConstructors_CompilesAndRuns()
    {
        // Class with three __init__ constructors
        var source = @"
class Rectangle:
    width: int
    height: int

    def __init__(self):
        self.width = 0
        self.height = 0

    def __init__(self, size: int):
        self.width = size
        self.height = size

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height

def main():
    r1 = Rectangle()
    r2 = Rectangle(5)
    r3 = Rectangle(10, 20)
    print(r1.width)
    print(r1.height)
    print(r2.width)
    print(r2.height)
    print(r3.width)
    print(r3.height)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n0\n5\n5\n10\n20\n", result.StandardOutput);
    }

    [Fact]
    public void Constructor_WithComputations_CompilesAndRuns()
    {
        // Constructor that performs computations
        var source = @"
class Circle:
    radius: int
    diameter: int

    def __init__(self, radius: int):
        self.radius = radius
        self.diameter = radius * 2

def main():
    c = Circle(5)
    print(c.radius)
    print(c.diameter)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("5\n10\n", result.StandardOutput);
    }

    #endregion

    #region Instance Method Tests

    [Fact]
    public void InstanceMethod_Simple_CompilesAndRuns()
    {
        // Simple instance method
        var source = @"
class Greeter:
    name: str

    def __init__(self, name: str):
        self.name = name

    def greet(self):
        print(f""Hello, {self.name}!"")

def main():
    g = Greeter(""World"")
    g.greet()
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello, World!\n", result.StandardOutput);
    }

    [Fact]
    public void InstanceMethod_WithParameters_CompilesAndRuns()
    {
        // Instance method with parameters
        var source = @"
class Calculator:
    value: int

    def __init__(self, initial: int):
        self.value = initial

    def add(self, amount: int):
        self.value = self.value + amount

    def get_value(self) -> int:
        return self.value

def main():
    calc = Calculator(10)
    calc.add(5)
    print(calc.get_value())
    calc.add(3)
    print(calc.get_value())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("15\n18\n", result.StandardOutput);
    }

    [Fact]
    public void InstanceMethod_WithReturnValue_CompilesAndRuns()
    {
        // Instance method returning a value
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def distance_from_origin(self) -> int:
        return self.x * self.x + self.y * self.y

def main():
    p = Point(3, 4)
    print(p.distance_from_origin())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("25\n", result.StandardOutput);
    }

    [Fact]
    public void InstanceMethod_MultipleParameters_CompilesAndRuns()
    {
        // Instance method with multiple parameters
        var source = @"
class Vector:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def set(self, new_x: int, new_y: int):
        self.x = new_x
        self.y = new_y

def main():
    v = Vector(1, 2)
    print(v.x)
    print(v.y)
    v.set(10, 20)
    print(v.x)
    print(v.y)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n10\n20\n", result.StandardOutput);
    }

    [Fact]
    public void InstanceMethod_CallingOtherMethods_CompilesAndRuns()
    {
        // Instance method calling another instance method
        var source = @"
class Counter:
    count: int

    def __init__(self):
        self.count = 0

    def increment(self):
        self.count = self.count + 1

    def increment_by(self, amount: int):
        i: int = 0
        while i < amount:
            self.increment()
            i = i + 1

def main():
    c = Counter()
    c.increment()
    print(c.count)
    c.increment_by(5)
    print(c.count)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n6\n", result.StandardOutput);
    }

    #endregion

    #region Static Method Tests

    [Fact]
    public void StaticMethod_WithoutDecorator_CompilesAndRuns()
    {
        // Static method without @static decorator (no self parameter)
        var source = @"
class MathHelper:
    def add(a: int, b: int) -> int:
        return a + b

def main():
    result = MathHelper.add(3, 4)
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("7\n", result.StandardOutput);
    }

    [Fact]
    public void StaticMethod_WithDecorator_CompilesAndRuns()
    {
        // Static method with @static decorator
        var source = @"
class MathHelper:
    @static
    def multiply(a: int, b: int) -> int:
        return a * b

def main():
    result = MathHelper.multiply(5, 6)
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("30\n", result.StandardOutput);
    }

    [Fact]
    public void StaticMethod_MultipleStaticMethods_CompilesAndRuns()
    {
        // Multiple static methods
        var source = @"
class Calculator:
    def add(a: int, b: int) -> int:
        return a + b

    def subtract(a: int, b: int) -> int:
        return a - b

    def multiply(a: int, b: int) -> int:
        return a * b

def main():
    print(Calculator.add(10, 5))
    print(Calculator.subtract(10, 5))
    print(Calculator.multiply(10, 5))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("15\n5\n50\n", result.StandardOutput);
    }

    [Fact]
    public void StaticMethod_MixedWithInstanceMethods_CompilesAndRuns()
    {
        // Class with both static and instance methods
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def distance_from_origin(self) -> int:
        return Point.square_sum(self.x, self.y)

    def square_sum(a: int, b: int) -> int:
        return a * a + b * b

def main():
    p = Point(3, 4)
    print(p.distance_from_origin())
    print(Point.square_sum(5, 12))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("25\n169\n", result.StandardOutput);
    }

    #endregion

    #region Name Mangling Tests

    [Fact]
    public void NameMangling_PublicField_ConvertsToPascalCase()
    {
        // Public field: snake_case -> PascalCase
        var source = @"
class Settings:
    user_count: int = 0
    max_connections: int = 100

def main():
    s = Settings()
    print(s.user_count)
    print(s.max_connections)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n100\n", result.StandardOutput);
    }

    [Fact]
    public void NameMangling_PrivateField_PrependedWithUnderscore()
    {
        // Private field: _name -> _name (already starts with underscore)
        var source = @"
class Account:
    _balance: int

    def __init__(self, initial: int):
        self._balance = initial

    def get_balance(self) -> int:
        return self._balance

def main():
    a = Account(100)
    print(a.get_balance())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("100\n", result.StandardOutput);
    }

    [Fact]
    public void NameMangling_InstanceMethod_ConvertsToPascalCase()
    {
        // Instance method: snake_case -> PascalCase
        var source = @"
class Formatter:
    def format_name(self, name: str) -> str:
        return f""Name: {name}""

def main():
    f = Formatter()
    print(f.format_name(""Alice""))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Name: Alice\n", result.StandardOutput);
    }

    [Fact]
    public void NameMangling_StaticMethod_ConvertsToPascalCase()
    {
        // Static method: snake_case -> PascalCase
        var source = @"
class StringHelper:
    def to_upper_case(s: str) -> str:
        return s

def main():
    result = StringHelper.to_upper_case(""hello"")
    print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("hello\n", result.StandardOutput);
    }

    [Fact]
    public void NameMangling_ParameterNames_ConvertToCamelCase()
    {
        // Parameter names: snake_case -> camelCase
        var source = @"
class Processor:
    def process_data(self, input_value: int, scale_factor: int) -> int:
        return input_value * scale_factor

def main():
    p = Processor()
    print(p.process_data(10, 3))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("30\n", result.StandardOutput);
    }

    #endregion

    #region Comprehensive Integration Tests

    [Fact]
    public void CompleteClass_Point_AllFeatures_CompilesAndRuns()
    {
        // Complete Point class with fields, constructors, instance methods, and static methods
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self):
        self.x = 0
        self.y = 0

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def move(self, dx: int, dy: int):
        self.x = self.x + dx
        self.y = self.y + dy

    def distance_squared(self) -> int:
        return self.x * self.x + self.y * self.y

    def create_origin() -> Point:
        return Point()

def main():
    p1 = Point()
    p2 = Point(3, 4)
    print(p1.x)
    print(p1.y)
    print(p2.x)
    print(p2.y)
    print(p2.distance_squared())
    p2.move(1, 1)
    print(p2.x)
    print(p2.y)
    print(p2.distance_squared())
    p3 = Point.create_origin()
    print(p3.x)
    print(p3.y)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\n0\n3\n4\n25\n4\n5\n41\n0\n0\n", result.StandardOutput);
    }

    [Fact]
    public void CompleteClass_Rectangle_WithArea_CompilesAndRuns()
    {
        // Rectangle class with area calculation
        var source = @"
class Rectangle:
    width: int
    height: int

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height

    def area(self) -> int:
        return self.width * self.height

    def perimeter(self) -> int:
        return 2 * (self.width + self.height)

    def scale(self, factor: int):
        self.width = self.width * factor
        self.height = self.height * factor

def main():
    r = Rectangle(10, 20)
    print(r.area())
    print(r.perimeter())
    r.scale(2)
    print(r.area())
    print(r.perimeter())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("200\n60\n800\n120\n", result.StandardOutput);
    }

    [Fact]
    public void CompleteClass_BankAccount_WithDeposit_CompilesAndRuns()
    {
        // BankAccount class simulating real-world usage
        var source = @"
class BankAccount:
    balance: int
    account_number: str

    def __init__(self, account_number: str):
        self.account_number = account_number
        self.balance = 0

    def deposit(self, amount: int):
        self.balance = self.balance + amount

    def withdraw(self, amount: int) -> bool:
        if amount <= self.balance:
            self.balance = self.balance - amount
            return True
        return False

    def get_balance(self) -> int:
        return self.balance

def main():
    account = BankAccount(""12345"")
    account.deposit(1000)
    print(account.get_balance())
    success = account.withdraw(300)
    print(success)
    print(account.get_balance())
    success = account.withdraw(800)
    print(success)
    print(account.get_balance())
    account.deposit(500)
    print(account.get_balance())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1000\nTrue\n700\nFalse\n700\n1200\n", result.StandardOutput);
    }

    [Fact]
    public void CompleteClass_Calculator_WithHistory_CompilesAndRuns()
    {
        // Calculator class with operation methods
        var source = @"
class Calculator:
    result: int

    def __init__(self):
        self.result = 0

    def add(self, value: int):
        self.result = self.result + value

    def subtract(self, value: int):
        self.result = self.result - value

    def multiply(self, value: int):
        self.result = self.result * value

    def reset(self):
        self.result = 0

    def get_result(self) -> int:
        return self.result

def main():
    calc = Calculator()
    calc.add(10)
    print(calc.get_result())
    calc.multiply(5)
    print(calc.get_result())
    calc.subtract(20)
    print(calc.get_result())
    calc.reset()
    print(calc.get_result())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n50\n30\n0\n", result.StandardOutput);
    }

    [Fact]
    public void CompleteClass_MultipleInstances_IndependentState_CompilesAndRuns()
    {
        // Test that multiple instances maintain independent state
        var source = @"
class Counter:
    count: int

    def __init__(self):
        self.count = 0

    def increment(self):
        self.count = self.count + 1

    def get_count(self) -> int:
        return self.count

def main():
    c1 = Counter()
    c2 = Counter()
    c1.increment()
    c1.increment()
    c2.increment()
    print(c1.get_count())
    print(c2.get_count())
    c1.increment()
    print(c1.get_count())
    print(c2.get_count())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("2\n1\n3\n1\n", result.StandardOutput);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void Error_UndefinedClass_ReportsError()
    {
        var source = @"
def main():
    obj = UndefinedClass()
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for undefined class");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_WrongConstructorArgumentCount_ReportsError()
    {
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p = Point(1)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for wrong constructor argument count");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_WrongMethodArgumentCount_ReportsError()
    {
        var source = @"
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

def main():
    calc = Calculator()
    result = calc.add(1)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for wrong method argument count");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_UndefinedField_ReportsError()
    {
        var source = @"
class Point:
    x: int
    y: int

def main():
    p = Point()
    print(p.z)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for undefined field");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_UndefinedMethod_ReportsError()
    {
        var source = @"
class Point:
    x: int
    y: int

def main():
    p = Point()
    p.undefined_method()
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for undefined method");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_StaticMethodCalledOnInstance_ReportsError()
    {
        var source = @"
class MathHelper:
    def add(a: int, b: int) -> int:
        return a + b

def main():
    helper = MathHelper()
    result = helper.add(1, 2)
";

        var result = CompileAndExecute(source);

        // This should fail because static methods cannot be called on instances
        Assert.False(result.Success, "Expected compilation to fail for static method called on instance");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_InstanceMethodCalledOnClass_ReportsError()
    {
        var source = @"
class Point:
    x: int

    def __init__(self, x: int):
        self.x = x

    def get_x(self) -> int:
        return self.x

def main():
    result = Point.get_x()
";

        var result = CompileAndExecute(source);

        // This should fail because instance methods cannot be called on the class
        Assert.False(result.Success, "Expected compilation to fail for instance method called on class");
        Assert.NotEmpty(result.CompilationErrors);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_ClassWithOnlyConstructor_CompilesAndRuns()
    {
        var source = @"
class Simple:
    value: int

    def __init__(self, value: int):
        self.value = value

def main():
    s = Simple(42)
    print(s.value)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_ClassWithOnlyMethods_CompilesAndRuns()
    {
        var source = @"
class Utility:
    def helper_one(self) -> int:
        return 1

    def helper_two(self) -> int:
        return 2

def main():
    u = Utility()
    print(u.helper_one())
    print(u.helper_two())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_NestedMethodCalls_CompilesAndRuns()
    {
        var source = @"
class Math:
    def double_value(self, x: int) -> int:
        return x * 2

    def triple(self, x: int) -> int:
        return x * 3

    def combo(self, x: int) -> int:
        return self.double_value(self.triple(x))

def main():
    m = Math()
    print(m.combo(5))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("30\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_FieldInitializedInConstructor_NotInDeclaration_CompilesAndRuns()
    {
        var source = @"
class Container:
    value: int

    def __init__(self, value: int):
        self.value = value

def main():
    c = Container(100)
    print(c.value)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("100\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_MultipleFieldsAndMethodsWithSnakeCase_CompilesAndRuns()
    {
        var source = @"
class DataProcessor:
    input_data: int
    output_data: int
    processing_count: int

    def __init__(self):
        self.input_data = 0
        self.output_data = 0
        self.processing_count = 0

    def process_single_item(self, item: int):
        self.input_data = item
        self.output_data = item * 2
        self.processing_count = self.processing_count + 1

    def get_processing_count(self) -> int:
        return self.processing_count

def main():
    dp = DataProcessor()
    dp.process_single_item(10)
    print(dp.output_data)
    print(dp.get_processing_count())
    dp.process_single_item(20)
    print(dp.output_data)
    print(dp.get_processing_count())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("20\n1\n40\n2\n", result.StandardOutput);
    }

    #endregion
}
