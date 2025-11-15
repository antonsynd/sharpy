# Sharpy Type System

## Overview

Sharpy uses a static type system with full type inference. All types are known at compile time, with optional runtime type checking for boundary cases.

For syntax details, see [Language Reference](language_reference.md).
For implementation details, see [Compiler Design](compiler_design.md).

## Design Principles

1. **Static typing by default**: All types are statically known at compile time
2. **Interface-oriented design**: Interfaces over inheritance
3. **Zero-cost abstractions**: Python semantics compile to efficient .NET IL
4. **Automatic conversions at boundaries**: Seamless interop at .NET/Sharpy boundaries
5. **Dunder method synthesis**: Automatic generation of .NET operator overloads from Python-style dunder methods

## Type Hierarchy

All Sharpy types derive from the .NET type hierarchy with `System.Object` at the root:

```
System.Object
├── System.ValueType (for structs)
│   ├── int, float, double, bool, byte, etc.
│   ├── Sharpy.Optional<T>
│   └── [User-defined structs]
└── Reference Types (for classes)
    ├── System.String, System.Array, etc.
    ├── Sharpy.List<T>
    ├── Sharpy.Dict<K, V>
    └── [User-defined classes]
```

## Built-in Types

### Type Mapping Table

| Sharpy | Python Equivalent | C# Implementation | .NET Type | Notes |
|--------|------------------|-------------------|-----------|-------|
| `int` | `int` | `int` | `System.Int32` | 32-bit signed integer (default) |
| `long` | `int` | `long` | `System.Int64` | 64-bit signed integer |
| `short` | `int` | `short` | `System.Int16` | 16-bit signed integer |
| `byte` | `int` | `byte` | `System.Byte` | 8-bit unsigned integer |
| `sbyte` | `int` | `sbyte` | `System.SByte` | 8-bit signed integer |
| `uint` | `int` | `uint` | `System.UInt32` | 32-bit unsigned integer |
| `ulong` | `int` | `ulong` | `System.UInt64` | 64-bit unsigned integer |
| `ushort` | `int` | `ushort` | `System.UInt16` | 16-bit unsigned integer |
| `float` | `float` | `float` | `System.Single` | 32-bit floating point |
| `double` | `float` | `double` | `System.Double` | 64-bit floating point |
| `decimal` | `float` | `decimal` | `System.Decimal` | 128-bit decimal |
| `bool` | `bool` | `bool` | `System.Boolean` | Boolean value |
| `char` | `int` | `char` | `System.Char` | UTF-16 character |
| `str` | `str` | `Sharpy.Str` | (wraps `System.String`) | Immutable string |
| `bytes` | `bytes` | `Sharpy.Bytes` | (wraps `byte[]`) | Byte array |
| `bytearray` | `bytearray` | `Sharpy.ByteArray` | (wraps `List<byte>`) | Mutable byte array |
| `list[T]` | `list[T]` | `Sharpy.List<T>` | (wraps `List<T>`) | Generic list |
| `dict[K, V]` | `dict[K, V]` | `Sharpy.Dict<K, V>` | (wraps `OrderedDictionary<K,V>`) | Generic dictionary |
| `set[T]` | `set[T]` | `Sharpy.Set<T>` | (wraps `HashSet<T>`) | Generic set |
| `frozenset[T]` | `frozenset[T]` | `Sharpy.FrozenSet<T>` | (wraps `FrozenSet<T>`) | Immutable set |
| `tuple[...]` | `tuple[...]` | `(...)` | `System.ValueTuple<...>` | Fixed-size tuple |
| `array[T]` | `list[T]` | `T[]` | `System.Array<T>` | Fixed-size array |
| `complex` | `complex` | `Sharpy.Complex` | (wraps `System.Numerics.Complex`) | Complex number |
| `T?` | `Optional[T]` | `Sharpy.Optional<T>` | Custom struct | True optional type |
| `None` | `None` | `void` | `System.Void` | No return value (functions only) |
| `None` | `None` | `null` | - | Null reference (values) |
| `object` | `object` | `Sharpy.Object` | (extends `System.Object`) | Base for all classes |
| `Exception` | `Exception` | `Sharpy.Exception` | (extends `System.Exception`) | Base exception |
| `slice` | `slice` | `Sharpy.Slice` | Custom struct | Slice object |
| `Ellipsis` | `Ellipsis` | `Sharpy.Ellipsis` | Singleton | Ellipsis literal `...` |

### Numeric Types

Sharpy provides a rich set of numeric types with different sizes and precision:

- **Integers**: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`
- **Floating point**: `float` (32-bit), `double` (64-bit), `decimal` (128-bit)
- **Complex**: `complex` for complex number arithmetic

Type inference for numeric literals:
```python
x = 42          # int
y = 42L         # long
z = 3.14f       # float
u = 3.14        # double
v = 3.14m       # decimal
w = 3 + 4j      # complex
```

### String Type

`Sharpy.Str` wraps `System.String` and provides Python-style string methods:

```python
text: str = "hello"
upper = text.upper()        # "HELLO"
parts = text.split(",")     # List[str]
```

**Common string methods**:
- `upper()`, `lower()`, `capitalize()`, `title()`
- `split(sep)`, `join(items)`, `replace(old, new)`
- `startswith(prefix)`, `endswith(suffix)`, `contains(substring)`
- `strip()`, `lstrip()`, `rstrip()`
- `find(substring)`, `index(substring)`

### Collection Types

#### List[T]

Dynamic-size generic list:
```python
numbers: list[int] = [1, 2, 3]
numbers.append(4)
numbers.pop()
length = len(numbers)
```

#### Dict[K, V]

Ordered dictionary preserving insertion order:
```python
mapping: dict[str, int] = {"a": 1, "b": 2}
mapping["c"] = 3
value = mapping.get("a", default=0)
```

#### Set[T]

Unordered collection of unique elements:
```python
unique: set[str] = {"a", "b", "c"}
unique.add("d")
unique.remove("a")
```

#### Tuple[T1, T2, ...]

Fixed-size, immutable collection of heterogeneous types:
```python
point: tuple[int, int] = (10, 20)
x, y = point
first = point[0]
```

See [Tuples](#tuples) section for implementation details.

## Nullable and Optional Types

Sharpy distinguishes between nullable reference types (C# style) and true optional types (Rust/Swift style).

### Nullable References (`T?` for reference types)

C#'s nullable reference types are enabled by default. Reference types can be null unless explicitly marked as non-nullable:

```python
# Nullable string (can be None/null)
maybe_name: str? = None

# Non-nullable string (must have value)
name: str = "Alice"

# Compiler enforces null checking
if maybe_name is not None:
    print(maybe_name.upper())  # Safe: compiler knows it's not null
```

### Optional Type (`Sharpy.Optional<T>`)

`Sharpy.Optional<T>` is a true optional type that can distinguish between "no value" and "null value" for reference types:

```python
from sharpy import Optional

# No value vs null value
opt1: Optional[str] = Optional()       # No value
opt2: Optional[str] = Optional(None)   # Has value: null

opt1.has_value()  # False
opt2.has_value()  # True
```

**Key differences**:

| Feature | `str?` (Nullable) | `Optional[str]` |
|---------|------------------|-----------------|
| Can represent null | ✅ Yes | ✅ Yes |
| Can distinguish "no value" from "null" | ❌ No | ✅ Yes |
| Works with value types | Via `Nullable<T>` | ✅ Yes |
| Memory overhead | None for refs | 1 bool + value |
| Use case | C# interop | Rust/Swift-style optionals |

**Optional[T] implementation**:

```csharp
public partial struct Optional<T>
{
    private T? _value;
    private bool _hasValue;

    public readonly T Value()
    {
        if (!_hasValue)
            throw new InvalidOperationException("Optional has no value");
        return _value!;
    }

    public readonly bool HasValue() => _hasValue;

    public readonly T ValueOr(T defaultValue) => _hasValue ? _value! : defaultValue;

    public readonly Optional<U> MapValue<U>(Func<T, U> func)
    {
        return _hasValue ? new Optional<U>(func(_value!)) : new Optional<U>();
    }
}
```

## Modules

Sharpy modules map Python-style module systems to C# namespaces with static classes for module-level members.

For import syntax, see [Language Reference - Modules](language_reference.md#modules-and-imports).

### Module Structure

Each Sharpy source file becomes:
1. A **C# namespace** based on the file path
2. A **static class** named after the module for module-level members

**Mapping table**:

| Sharpy File | C# Namespace | Static Class | Module Name |
|------------|--------------|--------------|-------------|
| `foo.spy` | `Sharpy.Modules` | `Foo` | `foo` |
| `foo/bar.spy` | `Sharpy.Modules.Foo` | `Bar` | `foo.bar` |
| `foo/bar/baz.spy` | `Sharpy.Modules.Foo.Bar` | `Baz` | `foo.bar.baz` |

### Module Members

Module-level constants and functions become static members of the module class:

**Sharpy (`math.spy`)**:
```python
"""Math utilities module."""

PI: float = 3.14159

def square(x: int) -> int:
    return x * x
```

**Generated C#**:
```csharp
namespace Sharpy.Modules;

public static class Math
{
    public const string __name__ = "math";
    public const string __file__ = "/path/to/math.spy";
    public static readonly string __doc__ = "Math utilities module.";

    public const double PI = 3.14159;

    public static int square(int x) => x * x;
}
```

### Module Special Variables

| Variable | Type | Description | Implementation |
|----------|------|-------------|----------------|
| `__name__` | `str` | Module name | `public const string` |
| `__file__` | `str` | Source file path | `public const string` |
| `__doc__` | `str` | Module docstring | `public static readonly string` |

### Import Resolution

| Sharpy Import | C# Using Directive | Effect |
|--------------|-------------------|--------|
| `import foo` | `using Foo = Sharpy.Modules.Foo;` | Access via `Foo.member` |
| `import foo.bar` | `using Bar = Sharpy.Modules.Foo.Bar;` | Access via `Bar.member` |
| `import foo.bar as fb` | `using fb = Sharpy.Modules.Foo.Bar;` | Access via `fb.member` |
| `from foo import bar` | `using static Sharpy.Modules.Foo;` | Access `bar` directly |
| `from foo import *` | `using static Sharpy.Modules.Foo;` | All public members in scope |

## Classes and Inheritance

Classes are reference types supporting single inheritance and multiple interface implementation.

For class syntax, see [Language Reference - Classes](language_reference.md#classes).

### Inheritance Rules

- **Single inheritance**: A class can inherit from exactly one base class
- **Multiple interface implementation**: A class can implement multiple interfaces
- **Interface-only multiple inheritance**: Multiple "bases" are allowed if all but one are interfaces

```python
# Single inheritance
class Employee(Person):
    pass

# Base class + interfaces
class JSONEmployee(Employee, Serializable, Comparable):
    pass

# Interfaces only (no base class)
class Point(Drawable, Comparable):
    pass
```

### Method Resolution Order (MRO)

Sharpy uses C3 linearization (same as Python) for method resolution when multiple interfaces are involved. Since C# only supports single class inheritance, the MRO primarily affects interface method resolution.

```python
class A:
    def method(self) -> str:
        return "A"

class B(A):
    def method(self) -> str:
        return "B"

# MRO: B -> A -> Object
```

### Virtual and Override Methods

Methods are virtual by default to support polymorphism:

```python
class Animal:
    def speak(self) -> str:
        return "..."

class Dog(Animal):
    @override
    def speak(self) -> str:
        return "Woof!"
```

**Generated C#**:
```csharp
public class Animal : Sharpy.Object
{
    public virtual string speak() => "...";
}

public class Dog : Animal
{
    public override string speak() => "Woof!";
}
```

### Abstract Classes

Abstract classes use the `@abstractmethod` decorator:

```python
from abc import ABC, abstractmethod

class Shape(ABC):
    @abstractmethod
    def area(self) -> float:
        pass

    @abstractmethod
    def perimeter(self) -> float:
        pass
```

**Generated C#**:
```csharp
public abstract class Shape : Sharpy.Object
{
    public abstract double area();
    public abstract double perimeter();
}
```

## Structs (Value Types)

Structs are value types that do not support inheritance but can implement interfaces.

For struct syntax, see [Language Reference - Structs](language_reference.md#structs).

### Value Semantics

Structs have value semantics (copy-by-value):

```python
struct Point:
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

p1 = Point(1.0, 2.0)
p2 = p1  # Copies the struct
p2.x = 5.0
print(p1.x)  # Still 1.0 (not affected by p2)
```

### Struct Constraints

- **No inheritance**: Structs cannot inherit from classes or other structs
- **Interface implementation**: Structs can implement interfaces
- **Immutability**: Structs should be immutable (readonly) for safety
- **Size**: Keep structs small (< 16 bytes recommended)

```python
struct Vector2(Comparable):
    x: float
    y: float

    def compare_to(self, other: Vector2) -> int:
        if self.x != other.x:
            return -1 if self.x < other.x else 1
        return -1 if self.y < other.y else (1 if self.y > other.y else 0)
```

## Interfaces

Interfaces define structural contracts that types must satisfy. They enable duck typing at compile time and map to C# interfaces.

For interface syntax, see [Language Reference - Interfaces](language_reference.md#interfaces).

### Structural Typing

Interfaces use structural typing - a type satisfies a interface if it implements all required methods, regardless of explicit declaration:

```python
interface Drawable:
    def draw(self) -> None: ...
    def get_bounds(self) -> tuple[float, float, float, float]: ...

# Circle satisfies Drawable without explicit declaration
class Circle:
    def draw(self) -> None:
        print("Drawing circle")

    def get_bounds(self) -> tuple[float, float, float, float]:
        return (self.x - self.r, self.y - self.r, self.r * 2, self.r * 2)

# Type checking works
def render(obj: Drawable):
    obj.draw()

render(Circle(0, 0, 10))  # ✅ Circle satisfies Drawable
```

### Interface Implementation

**Sharpy interface**:
```python
interface Drawable:
    def draw(self) -> None: ...
```

**Generated C# interface**:
```csharp
public interface IDrawable
{
    void draw();
}
```

### Interface with Default Implementations

Interfaces can provide default implementations using C# default interface methods:

```python
interface Logger:
    def log(self, message: str) -> None:
        """Must be implemented."""
        ...

    def log_error(self, error: str) -> None:
        """Default implementation provided."""
        self.log(f"ERROR: {error}")
```

**Generated C#**:
```csharp
public interface ILogger
{
    void log(string message);

    void log_error(string error)
    {
        this.log($"ERROR: {error}");
    }
}
```

### Common Interfaces

| Interface | Methods | .NET Interface | Purpose |
|----------|---------|----------------|---------|
| `Iterable[T]` | `__iter__() -> Iterator[T]` | `IEnumerable<T>` | Can be iterated |
| `Iterator[T]` | `__next__() -> T` | `IEnumerator<T>` | Produces values |
| `Sized` | `__len__() -> int` | - | Has length |
| `Container[T]` | `__contains__(T) -> bool` | - | Membership test |
| `Callable[[Args], Ret]` | `__call__(Args) -> Ret` | `Func<Args, Ret>` | Can be called |
| `Comparable[T]` | `__lt__`, `__le__`, `__eq__`, `__ne__`, `__gt__`, `__ge__` | `IComparable<T>` | Can be ordered |
| `Hashable` | `__hash__() -> int`, `__eq__` | - | Can be hashed |
| `ContextManager[T]` | `__enter__() -> T`, `__exit__()` | `IDisposable` | With statement |

## Generic Types

Generic types enable type-safe parameterization of classes, structs, interfaces, and functions.

For generic syntax, see [Language Reference - Generics](language_reference.md#generics).

### Type Parameters

```python
class Box[T]:
    def __init__(self, value: T):
        self._value = value

    def get(self) -> T:
        return self._value
```

**Generated C#**:
```csharp
public class Box<T> : Sharpy.Object
{
    private T _value;

    public Box(T value) => this._value = value;

    public T get() => this._value;
}
```

### Type Constraints

Constraints limit what types can be used as type arguments:

```python
# Interface constraint
def find_max[T: Comparable](items: list[T]) -> T:
    max_item = items[0]
    for item in items:
        if max_item < item:
            max_item = item
    return max_item

# Multiple constraints
class Cache[K: Hashable, V: Serializable]:
    pass

# Class constraint
class Repository[T: Entity]:
    pass
```

**Generated C#**:
```csharp
public static T find_max<T>(List<T> items) where T : IComparable<T>
{
    // ...
}

public class Cache<K, V> : Sharpy.Object
    where K : IHashable
    where V : ISerializable
{
}

public class Repository<T> : Sharpy.Object where T : Entity
{
}
```

### Variance

Sharpy supports covariance and contravariance for generic types:

**Covariance (out)**:
```python
from typing import TypeVar

T_co = TypeVar('T_co', covariant=True)

class Producer[T_co]:
    """Can produce T (covariant)."""
    def get(self) -> T_co: ...
```

**Contravariance (in)**:
```python
T_contra = TypeVar('T_contra', contravariant=True)

class Consumer[T_contra]:
    """Can consume T (contravariant)."""
    def accept(self, value: T_contra) -> None: ...
```

**Generated C#**:
```csharp
public interface IProducer<out T>
{
    T get();
}

public interface IConsumer<in T>
{
    void accept(T value);
}
```

### Generic Inference

Type arguments can be inferred from usage:

```python
def identity[T](value: T) -> T:
    return value

# Type argument inferred
x = identity(42)        # T inferred as int
y = identity("hello")   # T inferred as str

# Explicit type argument
z = identity[float](3.14)
```

## Tuples

Tuples are fixed-size, immutable collections implemented as custom wrappers around .NET's `ValueTuple<...>`.

### Tuple Implementation

```csharp
namespace Sharpy.Core;

public interface ITuple
{
    int Length { get; }
    object? this[int index] { get; }
}

public readonly struct Tuple<T1, T2> : ITuple, IEnumerable<object?>
{
    private readonly (T1, T2) _value;

    public Tuple(T1 item1, T2 item2) => _value = (item1, item2);

    public T1 Item1 => _value.Item1;
    public T2 Item2 => _value.Item2;

    public int Length => 2;

    public object? this[int index] => index switch
    {
        0 => Item1,
        1 => Item2,
        _ => throw new IndexError($"tuple index out of range: {index}")
    };

    public IEnumerator<object?> GetEnumerator()
    {
        yield return Item1;
        yield return Item2;
    }

    // Implicit conversion to/from ValueTuple
    public static implicit operator (T1, T2)(Tuple<T1, T2> tuple) => tuple._value;
    public static implicit operator Tuple<T1, T2>((T1, T2) tuple) => new(tuple.Item1, tuple.Item2);
}
```

### Tuple Features

**Creation and unpacking**:
```python
# Creation
point = (10, 20)
triple = (1, "hello", True)

# Unpacking
x, y = point
a, b, c = triple

# Indexing (dynamic, returns object?)
first = triple[0]  # Returns object?, requires cast

# Typed access (preferred)
x_val: int = point.Item1
y_val: int = point.Item2
```

**Iteration**:
```python
for item in triple:
    print(item)
```

## Properties

Properties provide computed access to object state. They can be auto-implemented or explicit.

For property syntax, see [Language Reference - Properties](language_reference.md#properties).

### Property Implementation

**Auto property**:
```python
class Person:
    property name: str = "Unknown"
```

**Generated C#**:
```csharp
public class Person : Sharpy.Object
{
    public string name { get; set; } = "Unknown";
}
```

**Explicit property**:
```python
class Temperature:
    __celsius: float = 0.0

    property celsius(self) -> float:
        return self.__celsius

    property celsius(self, value: float):
        self.__celsius = value
```

**Generated C#**:
```csharp
public class Temperature : Sharpy.Object
{
    private double __celsius = 0.0;

    public double celsius
    {
        get => this.__celsius;
        set => this.__celsius = value;
    }
}
```

## Type Inference

Sharpy employs sophisticated type inference to reduce verbosity while maintaining type safety.

### Assignment Inference

```python
# Type inferred from literal
x = 42              # int
y = 3.14            # float (double)
name = "Alice"      # str
flag = True         # bool

# Type inferred from function return
def get_value() -> int:
    return 42

result = get_value()  # int
```

### Generic Type Inference

```python
# List type inferred from elements
numbers = [1, 2, 3]  # list[int]

# Dict type inferred from entries
mapping = {"a": 1, "b": 2}  # dict[str, int]

# Function generic type inferred from arguments
def first[T](items: list[T]) -> T:
    return items[0]

x = first([1, 2, 3])     # T inferred as int
y = first(["a", "b"])    # T inferred as str
```

### Expression Type Inference

```python
# Binary operations
a: int = 5
b: int = 10
c = a + b           # int (inferred from operands)

x: float = 3.14
y = x * 2           # float (widening int -> float)

# Conditional expressions
age = 25
category = "adult" if age >= 18 else "minor"  # str
```

## Async Programming

Sharpy supports Python's async/await syntax mapping to C#'s Task-based asynchronous pattern.

For async syntax, see [Language Reference - Async Programming](language_reference.md#async-programming).

### Async Type Mapping

| Sharpy | C# |
|--------|-----|
| `async def func() -> T` | `async Task<T> func()` |
| `async def func() -> None` | `async Task func()` |
| `AsyncIterator[T]` | `IAsyncEnumerable<T>` |
| `AsyncContextManager[T]` | `IAsyncDisposable` |

### Async Return Types

```python
async def fetch_data() -> str:
    await asyncio.sleep(1.0)
    return "data"
```

**Generated C#**:
```csharp
public static async Task<string> fetch_data()
{
    await Task.Delay(TimeSpan.FromSeconds(1.0));
    return "data";
}
```

### Async Iteration

```python
async def count_up(n: int):
    for i in range(n):
        await asyncio.sleep(0.1)
        yield i
```

**Generated C#**:
```csharp
public static async IAsyncEnumerable<int> count_up(int n)
{
    for (int i = 0; i < n; i++)
    {
        await Task.Delay(TimeSpan.FromSeconds(0.1));
        yield return i;
    }
}
```

## See Also

- [Language Reference](language_reference.md) - Syntax and usage examples
- [Compiler Design](compiler_design.md) - Implementation and code generation details
