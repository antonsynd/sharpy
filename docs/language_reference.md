# Sharpy Language Reference

## Introduction

Sharpy is a modern, statically-typed Pythonic language targeting .NET. While Python code will not run in Sharpy without modifications, the additions and changes in Sharpy over Python should be intuitive to and welcomed by all Python developers.

### Goals

* Provide a statically-typed and modern Pythonic language for the .NET CLI
* Seamless bidirectional interop with other .NET libraries

### Guiding Principles

These principles are ordered in descending order of importance and should guide decisions when conflicts or ambiguities arise:

1. Sharpy is a .NET language at its core, inheriting and preferring design choices from the .NET CLI
2. Sharpy is a Pythonic language second, inheriting syntax, semantics, and standard library where possible from Python
3. Where the preceding two principles conflict, a preference for .NET will prevail, unless the conflict can be resolved within the compiler as intrinsics or clear, predictable, implicit conversions at .NET ABI boundaries, with zero-cost abstractions

### Philosophy

Sharpy believes that static typing is key to writing safe, predictable, and performant programs, at both the development stage and at runtime.

## Keywords and Operators

### Hard Keywords

The following are hard keywords in Sharpy and are always reserved:

`and`, `as`, `assert`, `async`, `await`, `break`, `class`, `continue`, `def`, `del`, `elif`, `else`, `except`, `False`, `finally`, `for`, `from`, `if`, `import`, `in`, `interface`, `is`, `lambda`, `None`, `not`, `or`, `pass`, `raise`, `return`, `struct`, `True`, `try`, `while`, `with`, `yield`

### Soft Keywords (Context-Dependent)

The following are soft keywords that are only treated as keywords in specific contexts:

`case`, `defer`, `event`, `get`, `guard`, `match`, `property`, `set`, `type`, `_` (wildcard)

### Operators

**Standard Python operators:**
- Arithmetic: `+`, `-`, `*`, `/`, `//`, `%`, `**`
- Comparison: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Logical: `and`, `or`, `not`
- Bitwise: `&`, `|`, `^`, `~`, `<<`, `>>`
- Matrix: `@`
- Membership: `in`, `not in`
- Identity: `is`, `is not`
- Assignment: `=`, `+=`, `-=`, `*=`, `/=`, etc.

**Sharpy-specific operators:**
- `?.` - None (null) conditional member access
- `??` - None (null) coalescing operator

## Literals and Special Values

### Special Literals

| Sharpy | Python Equivalent | C# Equivalent | Notes |
|--------|------------------|---------------|-------|
| `...` | `...` | - | Ellipsis literal as a placeholder or for slices |
| `False` | `False` | `false` | Boolean false |
| `None` | `None` | `null` | Null/absence of value |
| `True` | `True` | `true` | Boolean true |
| `{/}` | `set()` | - | Empty set literal, borrowed from PEP 802 |

### Literal Names

Any symbol in Sharpy can be surrounded with backticks `` ` `` to tell the compiler not to transform the name during resolution. This is equivalent to C#'s `@` prefix:

```python
# Prevents case transformation
from foo_bar.`abc` import *

# Using a keyword as an identifier
def `class`():
    pass

# Using exact casing without transformation
def `ExactMethodName`():
    pass
```

## Type Syntax

See [Type System](type_system.md) for detailed type semantics.

### Basic Type Annotations

```python
# Simple types
x: int = 42
name: str = "Alice"
flag: bool = True

# Type inference
y = 42              # Inferred as int
pi = 3.14159        # Inferred as float
```

### Generic Types

```python
# Collection types
numbers: list[int] = [1, 2, 3]
mapping: dict[str, int] = {"a": 1, "b": 2}
unique: set[str] = {"x", "y", "z"}

# Nested generics
matrix: list[list[float]] = [[1.0, 2.0], [3.0, 4.0]]
```

### Nullable Types

```python
# Nullable types
maybe_name: str? = None
result: int? = get_value()

# Type narrowing via 'is None' check
if result is not None:
    print(result + 10)  # Safe: result is int here
```

### Qualified Types

```python
from system.collections.generic import hash_set

# Fully qualified type names
numbers = hash_set[int]()
```

## Modules and Imports

Sharpy modules map to C# namespaces with static classes for module-level members.

See [Type System - Modules](type_system.md#modules) for implementation details.

### Import Syntax

```python
# Import entire module
import math
result = math.sqrt(16)

# Import specific items
from math import sqrt, pi
result = sqrt(16)

# Import with alias
import collections as col
d = col.defaultdict()

# Import all public members
from math import *
result = sqrt(16)
```

### Module Structure

```python
# my_module.spy

"""Module docstring."""

# Module-level constants
VERSION: str = "1.0.0"
MAX_SIZE: int = 1000

# Module-level functions
def helper_function(x: int) -> int:
    return x * 2

# Classes
class MyClass:
    pass
```

## Classes

Classes are reference types that support single inheritance and multiple interface implementation.

See [Type System - Classes](type_system.md#classes-and-inheritance) for details on the type hierarchy.

### Basic Class Definition

```python
class Person:
    """A person with a name and age."""

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    def greet(self) -> str:
        return f"Hello, I'm {self.name}"
```

### Inheritance

```python
class Employee(Person):
    """An employee extends Person."""

    def __init__(self, name: str, age: int, employee_id: str):
        super().__init__(name, age)
        self.employee_id = employee_id

    def greet(self) -> str:
        return f"Hello, I'm {self.name}, employee #{self.employee_id}"
```

### Interface Implementation

```python
class JSONSerializable(Serializable, Comparable):
    """Class implementing multiple interfaces."""

    def serialize(self) -> str:
        # Implementation
        pass

    def compare_to(self, other) -> int:
        # Implementation
        pass
```

### Constructor Overloading

```python
class Point:
    """A 2D point with multiple constructors."""

    def __init__(self):
        self.x = 0.0
        self.y = 0.0

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def __init__(self, other: Point):
        self.x = other.x
        self.y = other.y
```

## Structs

Structs are value types that do not support inheritance but can implement interfaces.

See [Type System - Structs](type_system.md#structs-value-types) for runtime behavior.

```python
struct Vector2:
    """A 2D vector value type."""
    x: float
    y: float

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def magnitude(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

## Interfaces

Interfaces define structural contracts that types must satisfy. They compile to C# interfaces.

See [Type System - Interfaces](type_system.md#interfaces-and-interfaces) for type checking rules.

### Interface Definition

```python
interface Drawable:
    """Interface for drawable objects."""

    def draw(self) -> None:
        """Draw the object."""
        ...

    def get_bounds(self) -> tuple[float, float, float, float]:
        """Get bounding box (x, y, width, height)."""
        ...
```

### Interface Inheritance

```python
interface Serializable:
    def serialize(self) -> str: ...

interface JSONSerializable(Serializable):
    """Extends Serializable interface."""
    def to_json(self) -> str: ...
```

### Interface with Default Implementation

```python
interface Logger:
    """Interface with default behavior."""

    def log(self, message: str) -> None:
        """Must be implemented."""
        ...

    def log_error(self, error: str) -> None:
        """Default implementation provided."""
        self.log(f"ERROR: {error}")
```

## Properties

Properties provide computed access to object state with flexible syntax.

### Auto Properties

Auto properties generate getter/setter with compiler-managed backing fields:

```python
class Person:
    # Auto-property with default value
    property name: str = "Unknown"

    # Read-only auto-property
    get property id: int = 0

    # Write-only auto-property
    set property _internal_value: int
```

### Explicit Properties

```python
class Temperature:
    """Temperature with Celsius/Fahrenheit conversion."""

    __celsius: float = 0.0

    # Explicit getter and setter
    property celsius(self) -> float:
        return self.__celsius

    property celsius(self, value: float):
        self.__celsius = value

    # Computed property (read-only)
    property fahrenheit(self) -> float:
        return self.__celsius * 9/5 + 32
```

### Abstract Properties (in Interfaces)

```python
interface Measurable:
    # Abstract property requiring both getter and setter
    property length: float

    # Abstract read-only property
    get property width: float

    # Explicit abstract properties
    property height(self) -> float: ...
    property height(self, value: float): ...
```

## Access Modifiers

Access modifiers control visibility of members. They can be specified using decorators or underscore naming hints.

### Decorator Syntax

```python
class Calculator:
    # Public (default)
    def public_method(self): pass

    # Protected
    @protected
    def protected_method(self): pass

    # Private
    @private
    def private_method(self): pass

    # Internal (assembly-level)
    @internal
    def internal_method(self): pass

    # File-level
    @file
    def file_method(self): pass
```

### Underscore Naming Hints

```python
class Example:
    # Protected by naming convention
    def _helper(self): pass

    # Private by naming convention
    def __internal_state(self): pass

    # Decorator overrides naming
    @public
    def _actually_public(self): pass
```

### Access Level Summary

| Decorator | Underscore Hint | C# Equivalent | Visibility |
|-----------|-----------------|---------------|------------|
| *(default)* | `name` | `public` | Everyone |
| `@protected` | `_name` | `protected` | Class and derived classes |
| `@private` | `__name` | `private` | Only the declaring class |
| `@internal` | N/A | `internal` | Same assembly |
| `@file` | N/A | `file` | Same file |

## Decorators

Decorators modify the behavior of functions, methods, and classes.

### Built-in Decorators

```python
class Example:
    # Static method
    @static
    def static_method():
        pass

    # Override base class method
    @override
    def virtual_method(self):
        pass

    # Final (sealed) class
    @final
    class SealedClass:
        pass

    # Property decorators
    @property
    def computed_value(self) -> int:
        return self._value * 2
```

### Custom Decorators

```python
def trace(func):
    """Decorator that traces function calls."""
    def wrapper(*args, **kwargs):
        print(f"Calling {func.__name__}")
        result = func(*args, **kwargs)
        print(f"Result: {result}")
        return result
    return wrapper

@trace
def compute(x: int) -> int:
    return x * 2
```

## Functions

### Function Definition

```python
def greet(name: str) -> str:
    """Greet a person by name."""
    return f"Hello, {name}!"

# With default arguments
def power(base: float, exponent: float = 2.0) -> float:
    return base ** exponent

# Multiple return values (tuple)
def min_max(values: list[int]) -> tuple[int, int]:
    return (min(values), max(values))
```

### Function Overloading

```python
def process(value: int) -> str:
    return f"Integer: {value}"

def process(value: str) -> str:
    return f"String: {value}"

def process(value: float) -> str:
    return f"Float: {value}"
```

### Lambda Expressions

```python
# Single expression
square = lambda x: x ** 2

# Multiple parameters
add = lambda x, y: x + y

# In function calls
numbers = [1, 2, 3, 4, 5]
evens = filter(lambda x: x % 2 == 0, numbers)
```

## Generics

Classes, structs, interfaces, and functions can be generic with type parameters.

See [Type System - Generic Types](type_system.md#generic-types) for constraint details.

### Generic Classes

```python
class Box[T]:
    """A container for a single value."""

    def __init__(self, value: T):
        self._value = value

    def get(self) -> T:
        return self._value

    def set(self, value: T):
        self._value = value

# Usage
int_box = Box[int](42)
str_box = Box[str]("hello")
```

### Generic Functions

```python
def identity[T](value: T) -> T:
    """Returns the input value unchanged."""
    return value

def first[T](items: list[T]) -> T:
    """Returns the first item."""
    return items[0]
```

### Type Constraints

```python
from typing import Interface

class Comparable(Interface):
    def __lt__(self, other) -> bool: ...

def find_max[T: Comparable](items: list[T]) -> T:
    """Find the maximum item (must be comparable)."""
    max_item = items[0]
    for item in items:
        if max_item < item:
            max_item = item
    return max_item
```

## Control Flow

### Conditional Statements

```python
# if/elif/else
if x > 0:
    print("positive")
elif x < 0:
    print("negative")
else:
    print("zero")

# Conditional expression
result = "even" if x % 2 == 0 else "odd"
```

### Loops

```python
# while loop
count = 0
while count < 10:
    print(count)
    count += 1

# for loop with range
for i in range(10):
    print(i)

# for loop with collection
names = ["Alice", "Bob", "Charlie"]
for name in names:
    print(name)

# Loop control
for i in range(100):
    if i == 50:
        break  # Exit loop
    if i % 2 == 0:
        continue  # Skip to next iteration
```

### Match Statements

```python
def describe(value):
    match value:
        case 0:
            return "zero"
        case 1:
            return "one"
        case _:
            return "other"

# Pattern matching with types
match obj:
    case str():
        print("It's a string")
    case int():
        print("It's an int")
    case _:
        print("Unknown type")
```

## Exception Handling

```python
# Basic try/except
try:
    result = risky_operation()
except ValueError as e:
    print(f"ValueError: {e}")
except Exception as e:
    print(f"General error: {e}")
finally:
    cleanup()

# Raising exceptions
def validate(value: int):
    if value < 0:
        raise ValueError("Value must be non-negative")
```

## Context Managers

```python
# Using context managers
with open("file.txt", "r") as f:
    content = f.read()

# Multiple context managers
with open("input.txt") as input_file, open("output.txt", "w") as output_file:
    output_file.write(input_file.read())
```

## Async Programming

### Async Functions

```python
async def fetch_data(url: str) -> str:
    """Fetch data asynchronously."""
    await asyncio.sleep(1.0)
    return f"Data from {url}"

async def main():
    result = await fetch_data("https://example.com")
    print(result)
```

### Async Iteration

```python
async def count_up(n: int):
    """Async generator."""
    for i in range(n):
        await asyncio.sleep(0.1)
        yield i

async def process():
    async for num in count_up(5):
        print(f"Number: {num}")
```

### Async Context Managers

```python
async def use_resource():
    async with AsyncResource() as resource:
        await resource.process()
```

## Naming Conventions

Sharpy follows specific naming conventions with automatic case conversion for .NET interop.

| Identifier Type | Sharpy Convention | Compiled Form |
|----------------|-------------------|---------------|
| Module | `snake_case` | `PascalCase` |
| Class | `PascalCase` | (unchanged) |
| Struct | `PascalCase` | (unchanged) |
| Interface | `IPascalCase` | (unchanged) |
| Members | `snake_case` | `PascalCase` |
| Enum | `PascalCase` | (unchanged) |
| Enum values | `CAPS_SNAKE_CASE` | `PascalCase` |
| Function | `snake_case` | `PascalCase` |
| Parameters | `snake_case` | `camelCase` |
| Local variables | `snake_case` | (unchanged) |
| Constants | `CAPS_SNAKE_CASE` | (unchanged) |

### Examples

```python
# Module: my_module.spy → namespace MyModule
# Function: add_numbers() → AddNumbers()
# Parameter: user_name → userName

def calculate_total(item_count: int, price_per_item: float) -> float:
    """Calculate total price."""
    return item_count * price_per_item
```

## Program Entry Point

The entry point is either a file with top-level statements or a `main()` function:

```python
# Option 1: Top-level statements
print("Hello, World!")

# Option 2: main() function
def main():
    print("Hello, World!")
```

**Note**: The Python idiom `if __name__ == "__main__":` does not exist in Sharpy. The `__name__` variable is not available and attempting to use it causes a compilation error.

## See Also

- [Type System](type_system.md) - Detailed type semantics, interfaces, and generics
- [Compiler Design](compiler_design.md) - Implementation details and code generation
