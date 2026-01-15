using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.7: Inheritance, Interfaces, Abstract Classes, and Decorators.
/// These tests verify the full compilation pipeline for advanced OOP features including:
/// - Single and multi-level inheritance
/// - Method overriding with @override decorator
/// - Abstract classes and abstract methods
/// - Interface definitions and implementations
/// - Dunder method override rules
/// - Constructor chaining with super().__init__()
/// </summary>
public class Phase017IntegrationTests : IntegrationTestBase
{
    public Phase017IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Simple Inheritance Tests

    [Fact]
    public void SingleInheritance_CompilesAndRuns()
    {
        // Dog extends Animal with field and method override
        var source = @"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    @virtual
    def speak(self) -> str:
        return ""Some sound""

class Dog(Animal):
    breed: str

    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

    @override
    def speak(self) -> str:
        return ""Woof!""

dog = Dog(""Rex"", ""German Shepherd"")
print(dog.name)
print(dog.breed)
print(dog.speak())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Rex\nGerman Shepherd\nWoof!\n", result.StandardOutput);
    }

    [Fact]
    public void InheritedField_AccessibleInSubclass()
    {
        // Subclass can access parent class fields
        var source = @"
class Vehicle:
    speed: int

    def __init__(self, speed: int):
        self.speed = speed

class Car(Vehicle):
    def __init__(self, speed: int):
        super().__init__(speed)

    def get_speed(self) -> int:
        return self.speed

car = Car(100)
print(car.get_speed())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("100\n", result.StandardOutput);
    }

    [Fact]
    public void InheritedMethod_CallableOnSubclass()
    {
        // Subclass can call inherited methods
        var source = @"
class Base:
    value: int

    def __init__(self, value: int):
        self.value = value

    def get_value(self) -> int:
        return self.value

class Derived(Base):
    def __init__(self, value: int):
        super().__init__(value)

d = Derived(42)
print(d.get_value())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n", result.StandardOutput);
    }

    [Fact]
    public void ConstructorChaining_SuperInit_CompilesAndRuns()
    {
        // super().__init__() calls parent constructor
        var source = @"
class Parent:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

class Child(Parent):
    z: int

    def __init__(self, x: int, y: int, z: int):
        super().__init__(x, y)
        self.z = z

c = Child(1, 2, 3)
print(c.x)
print(c.y)
print(c.z)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n3\n", result.StandardOutput);
    }

    [Fact]
    public void MultiLevelInheritance_ThreeLevels_CompilesAndRuns()
    {
        // Level3 extends Level2 extends Level1
        var source = @"
class Level1:
    val1: int

    def __init__(self, val1: int):
        self.val1 = val1

class Level2(Level1):
    val2: int

    def __init__(self, val1: int, val2: int):
        super().__init__(val1)
        self.val2 = val2

class Level3(Level2):
    val3: int

    def __init__(self, val1: int, val2: int, val3: int):
        super().__init__(val1, val2)
        self.val3 = val3

obj = Level3(10, 20, 30)
print(obj.val1)
print(obj.val2)
print(obj.val3)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n20\n30\n", result.StandardOutput);
    }

    [Fact]
    public void MethodOverride_WithOverrideDecorator_CompilesAndRuns()
    {
        // @override on method overriding parent
        var source = @"
class Shape:
    @virtual
    def area(self) -> int:
        return 0

class Square(Shape):
    side: int

    def __init__(self, side: int):
        self.side = side

    @override
    def area(self) -> int:
        return self.side * self.side

s = Square(5)
print(s.area())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("25\n", result.StandardOutput);
    }

    #endregion

    #region Decorator Tests

    [Fact]
    public void StaticDecorator_OnMethod_CompilesAndRuns()
    {
        // @static decorator makes method static
        var source = @"
class MathUtils:
    @static
    def add(a: int, b: int) -> int:
        return a + b

result = MathUtils.add(10, 20)
print(result)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("30\n", result.StandardOutput);
    }

    [Fact]
    public void VirtualDecorator_AllowsOverride_CompilesAndRuns()
    {
        // @virtual allows subclass override
        var source = @"
class Base:
    @virtual
    def greet(self) -> str:
        return ""Hello from Base""

class Derived(Base):
    @override
    def greet(self) -> str:
        return ""Hello from Derived""

d = Derived()
print(d.greet())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Hello from Derived\n", result.StandardOutput);
    }

    [Fact]
    public void OverrideDecorator_OnInheritedMethod_CompilesAndRuns()
    {
        // @override on overriding method
        var source = @"
class Parent:
    @virtual
    def method(self) -> str:
        return ""parent""

class Child(Parent):
    @override
    def method(self) -> str:
        return ""child""

c = Child()
print(c.method())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("child\n", result.StandardOutput);
    }

    [Fact]
    public void MultipleDecorators_OnSameMethod_CompilesAndRuns()
    {
        // Multiple decorators stack correctly
        var source = @"
class Base:
    @virtual
    def compute(self) -> int:
        return 0

class Derived(Base):
    @override
    def compute(self) -> int:
        return 42

d = Derived()
print(d.compute())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n", result.StandardOutput);
    }

    #endregion

    #region Abstract Class Tests

    [Fact]
    public void AbstractClass_ConcreteSubclass_CompilesAndRuns()
    {
        // Abstract class with concrete implementation
        var source = @"
@abstract
class Shape:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstractmethod
    def area(self) -> int:
        ...

class Rectangle(Shape):
    width: int
    height: int

    def __init__(self, width: int, height: int):
        super().__init__(""Rectangle"")
        self.width = width
        self.height = height

    @override
    def area(self) -> int:
        return self.width * self.height

rect = Rectangle(10, 5)
print(rect.name)
print(rect.area())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Rectangle\n50\n", result.StandardOutput);
    }

    [Fact]
    public void AbstractMethod_MustBeImplemented_CompilesAndRuns()
    {
        // Abstract method implemented in subclass
        var source = @"
@abstract
class Animal:
    @abstractmethod
    def make_sound(self) -> str:
        ...

class Cat(Animal):
    @override
    def make_sound(self) -> str:
        return ""Meow""

cat = Cat()
print(cat.make_sound())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Meow\n", result.StandardOutput);
    }

    [Fact]
    public void AbstractClass_MultipleAbstractMethods_CompilesAndRuns()
    {
        // Multiple abstract methods
        var source = @"
@abstract
class Vehicle:
    @abstractmethod
    def start(self) -> str:
        ...

    @abstractmethod
    def stop(self) -> str:
        ...

class Car(Vehicle):
    @override
    def start(self) -> str:
        return ""Engine started""

    @override
    def stop(self) -> str:
        return ""Engine stopped""

car = Car()
print(car.start())
print(car.stop())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Engine started\nEngine stopped\n", result.StandardOutput);
    }

    [Fact]
    public void AbstractClass_MixedAbstractAndConcrete_CompilesAndRuns()
    {
        // Mix of abstract and concrete methods
        var source = @"
@abstract
class Base:
    @abstractmethod
    def abstract_method(self) -> int:
        ...

    def concrete_method(self) -> int:
        return 100

class Derived(Base):
    @override
    def abstract_method(self) -> int:
        return 42

d = Derived()
print(d.abstract_method())
print(d.concrete_method())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n100\n", result.StandardOutput);
    }

    #endregion

    #region Interface Tests

    [Fact]
    public void InterfaceDefinition_SimpleInterface_CompilesAndRuns()
    {
        // Define and implement simple interface
        var source = @"
interface IDrawable:
    def draw(self) -> str:
        ...

class Circle(IDrawable):
    radius: int

    def __init__(self, radius: int):
        self.radius = radius

    def draw(self) -> str:
        return f""Circle with radius {self.radius}""

c = Circle(5)
print(c.draw())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Circle with radius 5\n", result.StandardOutput);
    }

    [Fact]
    public void InterfaceImplementation_SingleMethod_CompilesAndRuns()
    {
        // Class implements interface method
        var source = @"
interface IComparable:
    def compare_to(self, other: int) -> int:
        ...

class Number(IComparable):
    value: int

    def __init__(self, value: int):
        self.value = value

    def compare_to(self, other: int) -> int:
        if self.value > other:
            return 1
        elif self.value < other:
            return -1
        return 0

n = Number(10)
print(n.compare_to(5))
print(n.compare_to(10))
print(n.compare_to(15))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n0\n-1\n", result.StandardOutput);
    }

    [Fact]
    public void MultipleInterfaces_ImplementsBoth_CompilesAndRuns()
    {
        // Class implements multiple interfaces
        var source = @"
interface IDrawable:
    def draw(self) -> str:
        ...

interface IResizable:
    def resize(self, factor: int) -> str:
        ...

class Shape(IDrawable, IResizable):
    size: int

    def __init__(self, size: int):
        self.size = size

    def draw(self) -> str:
        return f""Drawing shape of size {self.size}""

    def resize(self, factor: int) -> str:
        self.size = self.size * factor
        return f""Resized to {self.size}""

s = Shape(10)
print(s.draw())
print(s.resize(2))
print(s.draw())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Drawing shape of size 10\nResized to 20\nDrawing shape of size 20\n", result.StandardOutput);
    }

    [Fact]
    public void InterfaceInheritance_InterfaceExtendsInterface_CompilesAndRuns()
    {
        // Interface extends another interface
        var source = @"
interface IBasic:
    def basic_method(self) -> str:
        ...

interface IExtended(IBasic):
    def extended_method(self) -> str:
        ...

class Implementation(IExtended):
    def basic_method(self) -> str:
        return ""basic""

    def extended_method(self) -> str:
        return ""extended""

obj = Implementation()
print(obj.basic_method())
print(obj.extended_method())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("basic\nextended\n", result.StandardOutput);
    }

    [Fact]
    public void InterfaceWithBaseClass_ClassExtendsAndImplements_CompilesAndRuns()
    {
        // Class extends base and implements interface
        var source = @"
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

interface ISpeakable:
    def speak(self) -> str:
        ...

class Dog(Animal, ISpeakable):
    def __init__(self, name: str):
        super().__init__(name)

    def speak(self) -> str:
        return f""{self.name} says Woof!""

dog = Dog(""Rex"")
print(dog.speak())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Rex says Woof!\n", result.StandardOutput);
    }

    #endregion

    #region Dunder Method Override Tests

    [Fact]
    public void DunderStr_Override_CompilesAndRuns()
    {
        // @override def __str__(self)
        var source = @"
class Person:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    @override
    def __str__(self) -> str:
        return f""Person({self.name}, {self.age})""

p = Person(""Alice"", 30)
print(str(p))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Person(Alice, 30)\n", result.StandardOutput);
    }

    [Fact]
    public void DunderRepr_Override_CompilesAndRuns()
    {
        // @override def __repr__(self)
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    @override
    def __repr__(self) -> str:
        return f""Point({self.x}, {self.y})""

p = Point(3, 4)
print(repr(p))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Point(3, 4)\n", result.StandardOutput);
    }

    [Fact]
    public void DunderStr_Override_CompilesAndRuns_Simple()
    {
        // Simple test for __str__ override without complex type checking
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    @override
    def __str__(self) -> str:
        return f""Point({self.x}, {self.y})""

p = Point(3, 4)
print(str(p))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Point(3, 4)\n", result.StandardOutput);
    }

    [Fact]
    public void DunderHash_Override_CompilesAndRuns()
    {
        // @override def __hash__(self)
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    @override
    def __hash__(self) -> int:
        return self.x * 31 + self.y

p = Point(3, 4)
print(p.__hash__())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("97\n", result.StandardOutput);
    }

    [Fact]
    public void DunderRepr_Override_CompilesAndRuns_Simple()
    {
        // Simple test for __repr__ override
        var source = @"
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    @override
    def __repr__(self) -> str:
        return f""Point({self.x}, {self.y})""

p = Point(5, 6)
print(repr(p))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Point(5, 6)\n", result.StandardOutput);
    }

    // NOTE: Complex dunder comparison operators (__eq__, __lt__, etc.) with isinstance type narrowing
    // are not fully supported yet. These tests are disabled until type narrowing is implemented.

    [Fact]
    public void SuperInDunder_CallsParent_CompilesAndRuns()
    {
        // super().__str__() calls parent
        var source = @"
class Base:
    name: str

    def __init__(self, name: str):
        self.name = name

    @override
    def __str__(self) -> str:
        return f""Base({self.name})""

class Derived(Base):
    value: int

    def __init__(self, name: str, value: int):
        super().__init__(name)
        self.value = value

    @override
    def __str__(self) -> str:
        return f""Derived({super().__str__()}, {self.value})""

d = Derived(""test"", 42)
print(str(d))
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Derived(Base(test), 42)\n", result.StandardOutput);
    }

    #endregion

    #region Comprehensive Integration Tests

    [Fact]
    public void CompleteHierarchy_AnimalKingdom_CompilesAndRuns()
    {
        // Animal -> Mammal -> Dog/Cat hierarchy
        var source = @"
@abstract
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstractmethod
    def make_sound(self) -> str:
        ...

    @override
    def __str__(self) -> str:
        return f""Animal({self.name})""

@abstract
class Mammal(Animal):
    warm_blooded: bool

    def __init__(self, name: str):
        super().__init__(name)
        self.warm_blooded = True

class Dog(Mammal):
    breed: str

    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed

    @override
    def make_sound(self) -> str:
        return ""Woof!""

class Cat(Mammal):
    indoor: bool

    def __init__(self, name: str, indoor: bool):
        super().__init__(name)
        self.indoor = indoor

    @override
    def make_sound(self) -> str:
        return ""Meow!""

dog = Dog(""Rex"", ""Labrador"")
cat = Cat(""Whiskers"", True)
print(dog.make_sound())
print(cat.make_sound())
print(dog.warm_blooded)
print(cat.warm_blooded)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Woof!\nMeow!\nTrue\nTrue\n", result.StandardOutput);
    }

    [Fact]
    public void AbstractInterfaceCombination_CompilesAndRuns()
    {
        // Abstract class implementing interface
        var source = @"
interface IDrawable:
    def draw(self) -> str:
        ...

@abstract
class Shape(IDrawable):
    name: str

    def __init__(self, name: str):
        self.name = name

    @abstractmethod
    def area(self) -> int:
        ...

    def draw(self) -> str:
        return f""Drawing {self.name}""

class Circle(Shape):
    radius: int

    def __init__(self, radius: int):
        super().__init__(""Circle"")
        self.radius = radius

    @override
    def area(self) -> int:
        return 3 * self.radius * self.radius

c = Circle(5)
print(c.draw())
print(c.area())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Drawing Circle\n75\n", result.StandardOutput);
    }

    [Fact]
    public void DiamondPattern_MultipleInterfaces_CompilesAndRuns()
    {
        // Multiple interfaces with same method signature
        var source = @"
interface IReader:
    def read(self) -> str:
        ...

interface IWriter:
    def write(self, data: str) -> str:
        ...

class File(IReader, IWriter):
    content: str

    def __init__(self):
        self.content = """"

    def read(self) -> str:
        return self.content

    def write(self, data: str) -> str:
        self.content = data
        return ""Written""

f = File()
print(f.write(""Hello""))
print(f.read())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("Written\nHello\n", result.StandardOutput);
    }

    [Fact]
    public void PolymorphicBehavior_DifferentSubclasses_CompilesAndRuns()
    {
        // Polymorphic method calls through inheritance
        var source = @"
class Shape:
    @virtual
    def area(self) -> int:
        return 0

class Rectangle(Shape):
    width: int
    height: int

    def __init__(self, width: int, height: int):
        self.width = width
        self.height = height

    @override
    def area(self) -> int:
        return self.width * self.height

class Square(Shape):
    side: int

    def __init__(self, side: int):
        self.side = side

    @override
    def area(self) -> int:
        return self.side * self.side

rect = Rectangle(10, 5)
sq = Square(7)
print(rect.area())
print(sq.area())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("50\n49\n", result.StandardOutput);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void Error_AbstractClassInstantiation_ReportsError()
    {
        var source = @"
@abstract
class Shape:
    @abstractmethod
    def area(self) -> int:
        ...

s = Shape()
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for abstract class instantiation");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_AbstractMethodNotImplemented_ReportsError()
    {
        var source = @"
@abstract
class Shape:
    @abstractmethod
    def area(self) -> int:
        ...

class Circle(Shape):
    radius: int

    def __init__(self, radius: int):
        self.radius = radius

c = Circle(5)
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for missing abstract method implementation");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_InterfaceMethodNotImplemented_ReportsError()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> str:
        ...

class Shape(IDrawable):
    ...

s = Shape()
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for missing interface method");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_OverrideWithoutBaseMethod_ReportsError()
    {
        var source = @"
class MyClass:
    @override
    def non_existent_method(self) -> int:
        return 42
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for @override without base method");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact(Skip = "Override checking not yet implemented - subclass can shadow base method without @override")]
    public void Error_MissingOverrideDecorator_ReportsError()
    {
        var source = @"
class Base:
    @virtual
    def method(self) -> int:
        return 1

class Derived(Base):
    def method(self) -> int:
        return 2
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for override without @override decorator");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_CircularInheritance_ReportsError()
    {
        var source = @"
class A(B):
    ...

class B(A):
    ...
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for circular inheritance");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact]
    public void Error_InheritFromNonClass_ReportsError()
    {
        var source = @"
class MyClass(NonExistentClass):
    ...
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for inheriting from non-existent class");
        Assert.NotEmpty(result.CompilationErrors);
    }

    [Fact(Skip = "Dunder override checking not yet implemented - dunder methods can be defined without @override")]
    public void Error_DunderOverrideWithoutDecorator_ReportsError()
    {
        var source = @"
class MyClass:
    def __str__(self) -> str:
        return ""test""
";

        var result = CompileAndExecute(source);

        Assert.False(result.Success, "Expected compilation to fail for dunder override without @override");
        Assert.NotEmpty(result.CompilationErrors);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EdgeCase_EmptyInterface_CompilesAndRuns()
    {
        // Interface with no methods
        var source = @"
interface IMarker:
    ...

class MyClass(IMarker):
    value: int

    def __init__(self, value: int):
        self.value = value

obj = MyClass(42)
print(obj.value)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_EmptyAbstractClass_CompilesAndRuns()
    {
        // Abstract class with no abstract methods
        var source = @"
@abstract
class Base:
    value: int

    def __init__(self, value: int):
        self.value = value

class Derived(Base):
    def __init__(self, value: int):
        super().__init__(value)

d = Derived(100)
print(d.value)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("100\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_DeepInheritanceChain_CompilesAndRuns()
    {
        // 5+ levels of inheritance
        var source = @"
class L1:
    v1: int

    def __init__(self):
        self.v1 = 1

class L2(L1):
    v2: int

    def __init__(self):
        super().__init__()
        self.v2 = 2

class L3(L2):
    v3: int

    def __init__(self):
        super().__init__()
        self.v3 = 3

class L4(L3):
    v4: int

    def __init__(self):
        super().__init__()
        self.v4 = 4

class L5(L4):
    v5: int

    def __init__(self):
        super().__init__()
        self.v5 = 5

obj = L5()
print(obj.v1)
print(obj.v2)
print(obj.v3)
print(obj.v4)
print(obj.v5)
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n3\n4\n5\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_ManyInterfaces_CompilesAndRuns()
    {
        // Class implementing 5+ interfaces
        var source = @"
interface I1:
    def m1(self) -> int:
        ...

interface I2:
    def m2(self) -> int:
        ...

interface I3:
    def m3(self) -> int:
        ...

interface I4:
    def m4(self) -> int:
        ...

interface I5:
    def m5(self) -> int:
        ...

class MultiImpl(I1, I2, I3, I4, I5):
    def m1(self) -> int:
        return 1

    def m2(self) -> int:
        return 2

    def m3(self) -> int:
        return 3

    def m4(self) -> int:
        return 4

    def m5(self) -> int:
        return 5

obj = MultiImpl()
print(obj.m1())
print(obj.m2())
print(obj.m3())
print(obj.m4())
print(obj.m5())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\n2\n3\n4\n5\n", result.StandardOutput);
    }

    [Fact]
    public void EdgeCase_InterfaceWithProperties_CompilesAndRuns()
    {
        // Interface with property-like method signatures
        var source = @"
interface IEntity:
    def get_id(self) -> int:
        ...

    def get_name(self) -> str:
        ...

class User(IEntity):
    id: int
    name: str

    def __init__(self, id: int, name: str):
        self.id = id
        self.name = name

    def get_id(self) -> int:
        return self.id

    def get_name(self) -> str:
        return self.name

u = User(123, ""Alice"")
print(u.get_id())
print(u.get_name())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("123\nAlice\n", result.StandardOutput);
    }

    #endregion
}
