using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.9: Generics.
/// These tests verify the full compilation pipeline for generic types including:
/// - Generic class definitions with [T] syntax
/// - Generic method/function definitions
/// - Type parameter usage in fields and methods
/// - Type parameter constraints (interface, class, struct)
/// - Multiple type parameters
/// - Generic instantiation with concrete types
/// - Code generation from Sharpy [T] to C# &lt;T&gt;
///
/// NOTE: All tests are currently skipped because Phase 0.1.9 only implemented
/// parsing and code generation. Semantic analysis doesn't yet understand:
/// 1. Type parameters (T, U, V) as valid types in NameResolver/TypeChecker
/// 2. Generic type instantiation syntax (Box[int]) in expressions
///
/// See: #112 (semantic analysis for generic types)
/// </summary>
[Collection("HeavyCompilation")]
public class Phase019IntegrationTests : IntegrationTestBase
{
    public Phase019IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    #region Basic Generic Class Tests

    [Fact]
    public void GenericClass_SingleTypeParameter_CompilesAndRuns()
    {
        // Arrange: Generic class with single type parameter
        var source = @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def get(self) -> T:
        return self.value

def main():
    box = Box[int](42)
    print(box.get())
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_MultipleTypeParameters_CompilesAndRuns()
    {
        // Arrange: Generic class with two type parameters
        var source = @"
class Pair[T, U]:
    first: T
    second: U

    def __init__(self, first: T, second: U):
        self.first = first
        self.second = second

    def get_first(self) -> T:
        return self.first

    def get_second(self) -> U:
        return self.second

def main():
    p = Pair[int, str](42, ""hello"")
    print(p.get_first())
    print(p.get_second())
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\nhello\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_StringTypeArgument_CompilesAndRuns()
    {
        // Arrange: Generic class instantiated with string
        var source = @"
class Container[T]:
    item: T

    def __init__(self, item: T):
        self.item = item

    def display(self) -> None:
        print(self.item)

def main():
    c = Container[str](""test"")
    c.display()
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("test\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_BoolTypeArgument_CompilesAndRuns()
    {
        // Arrange: Generic class instantiated with bool
        var source = @"
class Wrapper[T]:
    data: T

    def __init__(self, data: T):
        self.data = data

    def get_data(self) -> T:
        return self.data

def main():
    w = Wrapper[bool](True)
    print(w.get_data())
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("True\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_MultipleInstances_DifferentTypeArguments()
    {
        // Arrange: Multiple instances of the same generic class with different type arguments
        var source = @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def get(self) -> T:
        return self.value

def main():
    int_box = Box[int](100)
    str_box = Box[str](""data"")
    print(int_box.get())
    print(str_box.get())
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("100\ndata\n", result.StandardOutput);
    }

    #endregion

    #region Generic Method Tests

    [Fact]
    public void GenericMethod_SingleTypeParameter_CompilesAndRuns()
    {
        // Arrange: Generic function with single type parameter
        var source = @"
def identity[T](value: T) -> T:
    return value

def main():
    x = identity[int](42)
    y = identity[str](""test"")
    print(x)
    print(y)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\ntest\n", result.StandardOutput);
    }

    [Fact]
    public void GenericMethod_MultipleTypeParameters_CompilesAndRuns()
    {
        // Arrange: Generic function with two type parameters
        var source = @"
def create_pair[T, U](first: T, second: U) -> str:
    return f""{first}, {second}""

def main():
    result = create_pair[int, str](10, ""value"")
    print(result)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10, value\n", result.StandardOutput);
    }

    [Fact]
    public void GenericMethod_ReturnTypeParameter_CompilesAndRuns()
    {
        // Arrange: Generic method that returns the type parameter
        var source = @"
def get_default[T](value: T) -> T:
    return value

def main():
    val1 = get_default[int](0)
    val2 = get_default[str](""empty"")
    print(val1)
    print(val2)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("0\nempty\n", result.StandardOutput);
    }

    #endregion

    #region Generic Struct Tests

    [Fact]
    public void GenericStruct_SingleTypeParameter_CompilesAndRuns()
    {
        // Arrange: Generic struct with single type parameter
        var source = @"
struct Point[T]:
    x: T
    y: T

    def __init__(self, x: T, y: T):
        self.x = x
        self.y = y

def main():
    p = Point[int](10, 20)
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
    public void GenericStruct_FloatTypeArgument_CompilesAndRuns()
    {
        // Arrange: Generic struct with float type argument
        var source = @"
struct Vector[T]:
    x: T
    y: T

    def __init__(self, x: T, y: T):
        self.x = x
        self.y = y

def main():
    v = Vector[float](3.5, 4.5)
    print(v.x)
    print(v.y)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("3.5\n4.5\n", result.StandardOutput);
    }

    [Fact]
    public void GenericStruct_MultipleTypeParameters_CompilesAndRuns()
    {
        // Arrange: Generic struct with two type parameters
        var source = @"
struct Tuple[T, U]:
    first: T
    second: U

    def __init__(self, first: T, second: U):
        self.first = first
        self.second = second

def main():
    t = Tuple[int, str](42, ""answer"")
    print(t.first)
    print(t.second)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\nanswer\n", result.StandardOutput);
    }

    #endregion

    #region Type Constraint Tests

    [Fact]
    public void GenericClass_InterfaceConstraint_CompilesAndRuns()
    {
        // Arrange: Generic class with interface constraint
        var source = @"
interface IComparable:
    def compare_to(self, other: int) -> int:
        ...

class Comparer[T: IComparable]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def get_value(self) -> T:
        return self.value

# Note: We can't easily test interface implementation in this simple test,
# but we can verify the constraint is parsed and generated correctly.
# The C# compilation will fail if the constraint syntax is wrong.
def main():
    print(""OK"")
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("OK\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_ClassConstraint_CompilesAndRuns()
    {
        // Arrange: Generic class with 'class' constraint (reference type)
        // str is System.String (reference type), but we use a user-defined class for testing
        var source = @"
class Wrapper:
    text: str

    def __init__(self, text: str):
        self.text = text

    def __str__(self) -> str:
        return self.text

class Container[T: class]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def get(self) -> T:
        return self.value

def main():
    w = Wrapper(""test"")
    c = Container[Wrapper](w)
    print(c.get())
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("test\n", result.StandardOutput);
    }

    [Fact]
    public void GenericStruct_StructConstraint_CompilesAndRuns()
    {
        // Arrange: Generic struct with 'struct' constraint (value type)
        var source = @"
struct Wrapper[T: struct]:
    value: T

    def __init__(self, value: T):
        self.value = value

def main():
    w = Wrapper[int](100)
    print(w.value)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("100\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_MultipleConstraints_CompilesAndRuns()
    {
        // Arrange: Generic class with multiple constraints
        var source = @"
interface IDisposable:
    def dispose(self) -> None:
        pass

class Manager[T: class, IDisposable]:
    resource: T

    def __init__(self, resource: T):
        self.resource = resource

    def get_resource(self) -> T:
        return self.resource

# Just verify compilation succeeds with multiple constraints
def main():
    print(""OK"")
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("OK\n", result.StandardOutput);
    }

    [Fact]
    public void GenericMethod_InterfaceConstraint_CompilesAndRuns()
    {
        // Arrange: Generic method with interface constraint
        var source = @"
interface IComparable:
    def compare_to(self, other: int) -> int:
        ...

def find_max[T: IComparable](a: T, b: T) -> T:
    # Simple implementation - just return first argument
    return a

# Verify compilation succeeds
def main():
    print(""OK"")
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("OK\n", result.StandardOutput);
    }

    #endregion

    #region Complex Generic Scenarios

    [Fact]
    public void GenericClass_NestedGenericUsage_CompilesAndRuns()
    {
        // Arrange: Generic class containing a field of another generic type
        var source = @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def get(self) -> T:
        return self.value

class Container[T]:
    item: T

    def __init__(self, item: T):
        self.item = item

    def get_item(self) -> T:
        return self.item

def main():
    inner = Box[int](42)
    outer = Container[Box[int]](inner)
    print(outer.get_item().get())
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_MethodReturnsGenericType_CompilesAndRuns()
    {
        // Arrange: Generic class with method that returns the generic type
        var source = @"
class Factory[T]:
    default_value: T

    def __init__(self, default_value: T):
        self.default_value = default_value

    def create(self) -> T:
        return self.default_value

def main():
    f = Factory[str](""default"")
    result = f.create()
    print(result)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("default\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_ThreeTypeParameters_CompilesAndRuns()
    {
        // Arrange: Generic class with three type parameters
        var source = @"
class Triple[T, U, V]:
    first: T
    second: U
    third: V

    def __init__(self, first: T, second: U, third: V):
        self.first = first
        self.second = second
        self.third = third

def main():
    t = Triple[int, str, bool](1, ""two"", True)
    print(t.first)
    print(t.second)
    print(t.third)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1\ntwo\nTrue\n", result.StandardOutput);
    }

    [Fact]
    public void GenericMethod_WithGenericClassParameter_CompilesAndRuns()
    {
        // Arrange: Generic method that takes a generic class as parameter
        var source = @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def get(self) -> T:
        return self.value

def extract[T](box: Box[T]) -> T:
    return box.get()

def main():
    b = Box[int](99)
    result = extract[int](b)
    print(result)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("99\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_MultipleMethodsUsingTypeParameter_CompilesAndRuns()
    {
        // Arrange: Generic class with multiple methods using the type parameter
        var source = @"
class Repository[T]:
    data: T

    def __init__(self, data: T):
        self.data = data

    def get_data(self) -> T:
        return self.data

    def set_data(self, new_data: T) -> None:
        self.data = new_data

    def display(self) -> None:
        print(self.data)

def main():
    repo = Repository[str](""initial"")
    repo.display()
    repo.set_data(""updated"")
    repo.display()
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("initial\nupdated\n", result.StandardOutput);
    }

    #endregion

    #region Generic Interface Tests

    [Fact]
    public void GenericInterface_SingleTypeParameter_CompilesAndRuns()
    {
        // Arrange: Generic interface definition
        var source = @"
interface IContainer[T]:
    def get(self) -> T:
        ...
    def set(self, value: T) -> None:
        ...

# Just verify the interface compiles
def main():
    print(""OK"")
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("OK\n", result.StandardOutput);
    }

    [Fact]
    public void GenericInterface_MultipleTypeParameters_CompilesAndRuns()
    {
        // Arrange: Generic interface with two type parameters
        var source = @"
interface IMapper[T, U]:
    def map(self, input: T) -> U:
        ...

# Just verify the interface compiles
def main():
    print(""OK"")
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("OK\n", result.StandardOutput);
    }

    #endregion

    #region Edge Cases and Special Scenarios

    [Fact]
    public void GenericClass_TypeParameterAsReturnAndParameter_CompilesAndRuns()
    {
        // Arrange: Generic class with type parameter used in both parameters and return types
        var source = @"
class Transformer[T]:
    def transform(self, input: T) -> T:
        return input

def main():
    t = Transformer[int]()
    result = t.transform(55)
    print(result)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("55\n", result.StandardOutput);
    }

    [Fact]
    public void GenericStruct_WithMethod_CompilesAndRuns()
    {
        // Arrange: Generic struct with methods
        var source = @"
struct Cell[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def swap(self, new_value: T) -> T:
        old = self.value
        self.value = new_value
        return old

def main():
    c = Cell[int](10)
    print(c.value)
    old = c.swap(20)
    print(old)
    print(c.value)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("10\n10\n20\n", result.StandardOutput);
    }

    [Fact]
    public void GenericMethod_ThreeTypeParameters_CompilesAndRuns()
    {
        // Arrange: Generic function with three type parameters
        var source = @"
def combine[T, U, V](a: T, b: U, c: V) -> str:
    return f""{a}, {b}, {c}""

def main():
    result = combine[int, str, bool](1, ""test"", False)
    print(result)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("1, test, False\n", result.StandardOutput);
    }

    [Fact]
    public void GenericClass_FieldsOfDifferentGenericTypes_CompilesAndRuns()
    {
        // Arrange: Class with multiple fields using the same type parameter
        var source = @"
class Pair[T]:
    first: T
    second: T

    def __init__(self, first: T, second: T):
        self.first = first
        self.second = second

    def swap(self) -> None:
        temp = self.first
        self.first = self.second
        self.second = temp

def main():
    p = Pair[str](""A"", ""B"")
    print(p.first)
    print(p.second)
    p.swap()
    print(p.first)
    print(p.second)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("A\nB\nB\nA\n", result.StandardOutput);
    }

    #endregion

    #region Comprehensive Integration Tests

    [Fact]
    public void ComprehensiveTest_GenericClassAndMethod_Together()
    {
        // Arrange: Combination of generic class and generic method
        var source = @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value

    def get(self) -> T:
        return self.value

def unwrap[T](box: Box[T]) -> T:
    return box.get()

def main():
    int_box = Box[int](42)
    str_box = Box[str](""hello"")

    print(unwrap[int](int_box))
    print(unwrap[str](str_box))
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("42\nhello\n", result.StandardOutput);
    }

    [Fact]
    public void ComprehensiveTest_GenericStructWithConstraints()
    {
        // Arrange: Generic struct with constraints
        var source = @"
struct ValueWrapper[T: struct]:
    data: T
    is_valid: bool

    def __init__(self, data: T):
        self.data = data
        self.is_valid = True

    def invalidate(self) -> None:
        self.is_valid = False

def main():
    w = ValueWrapper[int](100)
    print(w.data)
    print(w.is_valid)
    w.invalidate()
    print(w.is_valid)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("100\nTrue\nFalse\n", result.StandardOutput);
    }

    [Fact]
    public void ComprehensiveTest_MultipleGenericClasses_Interacting()
    {
        // Arrange: Multiple generic classes working together
        var source = @"
class Container[T]:
    item: T

    def __init__(self, item: T):
        self.item = item

    def get(self) -> T:
        return self.item

class Processor[T]:
    def process(self, container: Container[T]) -> T:
        return container.get()

def main():
    c = Container[int](99)
    p = Processor[int]()
    result = p.process(c)
    print(result)
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("99\n", result.StandardOutput);
    }

    [Fact]
    public void ComprehensiveTest_GenericClassWithMixedTypeParameters()
    {
        // Arrange: Complex scenario with mixed type usage
        var source = @"
class Cache[TKey, TValue]:
    key: TKey
    value: TValue

    def __init__(self, key: TKey, value: TValue):
        self.key = key
        self.value = value

    def get_key(self) -> TKey:
        return self.key

    def get_value(self) -> TValue:
        return self.value

    def display(self) -> None:
        print(f""{self.key}: {self.value}"")

def main():
    cache = Cache[str, int](""count"", 42)
    cache.display()
    print(cache.get_key())
    print(cache.get_value())
";

        // Act
        var result = CompileAndExecute(source);

        // Assert
        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Equal("count: 42\ncount\n42\n", result.StandardOutput);
    }

    #endregion
}
