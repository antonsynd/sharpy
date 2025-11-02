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

| Sharpy | Notes |
| ------ | ----- |
| `and` | Boolean and |
| `as` | Aliasing for imports, assignments, etc. |
| `assert` | Assertion |
| `async` | Async modifier on functions, iterators, etc. |
| `auto` | Inferred type declaration |
| `await` | Await for async operations |
| `break` | Break statement for loops |
| `class` | Keyword for classes (reference types) |
| `const` | Constant declaration |
| `continue` | Continue statement for loops |
| `def` | Function definition |
| `del` | Delete statement for dictionaries |
| `elif` | Else if block for conditionals |
| `else` | Else block for conditionals and loops |
| `enum` | Enumeration (tagged union) |
| `except` | Except block for exception handling |
| `False` | Boolean false literal |
| `finally` | Finally block for exception handling |
| `for` | For loop |
| `from` | Selective imports and generator delegation |
| `if` | If block for conditionals |
| `import` | Import statement for modules |
| `in` | Membership check in collections |
| `interface` | Keyword for interfaces |
| `is` | Reference equality operator |
| `lambda` | Lambda expression |
| `match` | Match block for expressive, structural matching |
| `None` | None literal |
| `not` | Boolean not |
| `or` | Boolean or |
| `pass` | No-op placeholder statement |
| `raise` | Raise statement for exceptions |
| `return` | Return statement |
| `struct` | Keyword for structs (value types) |
| `True` | Boolean true literal |
| `try` | Try block for exception handling |
| `while` | While loop |
| `with` | With block for context managers |
| `yield` | Yield for generators |

### Soft Keywords (Context-Dependent)

The following are soft keywords that are only treated as keywords in specific contexts:

| Sharpy | Notes |
| ------ | ----- |
| `case` | Case block for match blocks |
| `defer` | Deferral of cleanup expressions |
| `event` | Event handler definition |
| `get` | Property getter definition |
| `property` | Property definition |
| `set` | Property setter definition |
| `type` | Type alias |
| `_` | Wildcard in match blocks |

### Operators

**Standard Python operators:**
- Arithmetic: `+`, `-`, `*`, `/`, `//`, `%`, `**`
- Comparison: `==`, `!=`, `<`, `>`, `<=`, `>=`
- Logical: `and`, `or`, `not`
- Bitwise: `&`, `|`, `^`, `~`, `<<`, `>>`
- Matrix: `@`
- Membership: `in`, `not in`
- Identity: `is`, `is not`
- Assignment: `=`, `+=`, `-=`, `*=`, `/=`, `:=`, etc.

**Sharpy-specific operators:**
- `?.` - None (null) conditional member access
- `??` - None (null) coalescing operator

## Literals and Special Values

### Special Literals

| Sharpy | Python Equivalent | C# Equivalent | Notes |
|--------|-------------------|---------------|-------|
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
pi = 3.14159        # Inferred as double
```

### Generic Types

```python
# Collection types
numbers: list[int] = [1, 2, 3]
mapping: dict[str, int] = {"a": 1, "b": 2}
unique: set[str] = {"x", "y", "z"}

# Nested generics
matrix: list[list[double]] = [[1.0, 2.0], [3.0, 4.0]]
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

## Variable Assignment and Scope

Sharpy allows variables to be declared without assignment. However, they must be given a value before their first use. Note that the first declaration of a variable in a scope does not need an explicit type annotation if it can be inferred by an accompanying assignment in the same declaration.

```python
x: int

print(x)  # Compile-time error, cannot use x without assignment
```

```python
x: int

x = 5

print(x)  # Ok
```

Unlike Python, variables are scoped by block.

```python
x = 5

if x > 4:
    y = 4

print(y)  # Compile-time error, y does not exist
```

All code paths prior to a variable's first use must result in that variable being assigned a value.

```python
x = 5
y: int

if x > 4:
    y = 4


print(y)  # Compile-time error, y might not be assigned
```

```python
x = 5
y: int

if x > 4:
    y = 4
else:
    y = x

print(y)  # Ok
```

### Variable Shadowing

Variables can be redeclared in the same scope with a different type. **Type annotation is required** for shadowing to distinguish it from simple assignment:

```python
x: int = 5              # Initial declaration
x = 10                  # Assignment (same type)
x: str = "hello"        # Shadowing (new type, explicit annotation required)
x = "world"             # Assignment to shadowed variable

# Type inference with auto
x: int = 5
x: auto = "hello"       # Shadowing with inferred type (saves keystrokes)
x: auto = [x]           # Type inferred as list[str]
```

**Rules:**
- **Simple assignment** (`x = value`) assigns to existing variable, type must match
- **Shadowing** requires type annotation (explicit type or `auto`)
- **Type mismatch** without annotation is a compile-time error

```python
x: int = 5
x = "hello"             # ERROR: Type mismatch (str cannot assign to int)
x: str = "hello"        # OK: Shadowing with explicit type
x: auto = "hello"       # OK: Shadowing with type inference
```

**The `auto` keyword:**

Use `auto` to shadow a variable when you want the compiler to infer the new type:

```python
x: int = 5
x: str = str(x)                          # Explicit type
x: auto = [x]                            # Inferred as list[str]
x: auto = {"value": x}                   # Inferred as dict[str, str]
x: auto = very_long_generic_type_here()  # Saves typing the long type name
```

`auto` is particularly useful when:
- The type is obvious from the expression
- The type name is very long or complex
- You want to acknowledge the type change without spelling it out

## Constants

Constants are immutable values declared with the `const` keyword. They must be initialized with a compile-time constant expression and cannot be reassigned.

### Module-Level Constants

```python
const PI: double = 3.14159
const MAX_SIZE: int = 1000
const APP_NAME: str = "MyApp"
const DEBUG_MODE: bool = False

# Type inference works with const
const MAX_RETRIES = 3           # Inferred as int
const DEFAULT_TIMEOUT = 30.0    # Inferred as double
```

### Class Constants

```python
class MathConstants:
    const E: double = 2.71828
    const PHI: double = 1.61803
    const TAU: double = 2 * PI  # Compile-time expression

    @static
    def circle_area(radius: double) -> double:
        return PI * radius ** 2

class Config:
    const VERSION = "1.0.0"
    const API_BASE_URL = "https://api.example.com"
```

### Constant Rules

**Must be initialized at declaration:**
```python
const X = 10           # OK
const Y: double        # ERROR: Must be initialized
```

**Value must be compile-time constant:**
```python
const MAX = 100                 # OK - literal
const DOUBLE_MAX = MAX * 2      # OK - compile-time expression
const RUNTIME = get_value()     # ERROR: Not compile-time constant
```

**Cannot be reassigned:**
```python
const LIMIT = 100
LIMIT = 200            # ERROR: Cannot assign to constant
```

**Type can be inferred or explicit:**
```python
const X = 42           # Inferred as int
const Y: double = 42   # Explicit type (with conversion)
```

### Naming Convention

By convention (not requirement), constants use `CAPS_SNAKE_CASE`:

```python
const MAX_RETRIES = 3
const DEFAULT_TIMEOUT = 30
const API_BASE_URL = "https://api.example.com"

# Also valid, but not conventional
const maxRetries = 3
const default_timeout = 30
```

**Note:** The `CAPS_SNAKE_CASE` naming is only a convention. Variables using this naming style without the `const` keyword are **not** constants and can be reassigned:

```python
# This is NOT a constant - can be reassigned
MAX_VALUE: int = 100
MAX_VALUE = 200  # OK - no const keyword

# This IS a constant - cannot be reassigned
const MAX_VALUE: int = 100
MAX_VALUE = 200  # ERROR - declared with const
```

## Modules and Imports

Sharpy modules map to .NET namespaces with static classes for module-level members.

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
const VERSION: str = "1.0.0"
const MAX_SIZE: int = 1000

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

Class members must be declared along with their type.

```python
class Person:
    """A person with a name and age."""
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    def greet(self) -> str:
        return f"Hello, I'm {self.name}"
```

Constructor methods do not have return types, but if a type annotation is required, it is `None`:

```python
class Person:
    name: str
    age: int

   def __init__(self, name: str, age: int) -> None:
        self.name = name
        self.age = age
```

### Inheritance

```python
class Employee(Person):
    """An employee extends Person."""
    employee_id: str

    def __init__(self, name: str, age: int, employee_id: str):
        super().__init__(name, age)
        self.employee_id = employee_id

    def greet(self) -> str:
        return f"Hello, I'm {self.name}, employee #{self.employee_id}"
```

### Interface Implementation

```python
class SomeSerializableObject(ISerializable, IComparable):
    """Class implementing multiple interfaces."""

    def serialize(self) -> str:
        # Implementation
        pass

    def compare_to(self, other) -> int:
        # Implementation
        pass
```

### Constructor Overloading

Multiple `__init__` methods with different parameter signatures are allowed:

```python
class Point:
    """A 2D point with multiple constructors."""
    x: double
    y: double

    def __init__(self):
        """Default constructor - origin point."""
        self.x = 0.0
        self.y = 0.0

    def __init__(self, x: double, y: double):
        """Construct from coordinates."""
        self.x = x
        self.y = y

    def __init__(self, other: Point):
        """Copy constructor."""
        self.x = other.x
        self.y = other.y

# Usage
p1 = Point()              # Calls parameterless constructor
p2 = Point(10.0, 20.0)    # Calls (double, double) constructor
p3 = Point(p2)            # Calls copy constructor
```

**Overload Resolution:**
- Based on parameter **count** and **types** only
- Parameter **names** do not affect overload resolution

**Default Parameters:**
Default parameters generate a **single** constructor with optional parameters, not multiple overloads:

```python
class Point:
    x: double
    y: double

    # Generates ONE constructor
    def __init__(self, x: double = 0.0, y: double = 0.0):
        self.x = x
        self.y = y
```

**Compile Errors:**
As mentioned above, parameter names do not count towards overload resolution as they are ultimately not part of the function's signature.

```python
class Point:
    def __init__(self, x: double, y: double): pass
    def __init__(self, a: double, b: double): pass  # ERROR: Duplicate signature
```

## Structs

Structs are value types that do not support inheritance but can implement interfaces.

See [Type System - Structs](type_system.md#structs-value-types) for runtime behavior.

```python
struct Vector2:
    """A 2D vector value type."""
    x: double
    y: double

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def magnitude(self) -> double:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

## Interfaces

Interfaces define structural contracts that types must satisfy.

See [Type System - Interfaces](type_system.md#interfaces-and-interfaces) for type checking rules.

### Interface Definition

```python
interface IDrawable:
    """Interface for drawable objects."""

    def draw(self) -> None:
        """Draw the object."""
        ...

    def get_bounds(self) -> tuple[double, double, double, double]:
        """Get bounding box (x, y, width, height)."""
        ...
```

### Interface Inheritance

```python
interface ISerializable:
    def serialize(self) -> str: ...

interface IJSONSerializable(ISerializable):
    """Extends Serializable interface."""
    def to_json(self) -> str: ...
```

### Interface with Default Implementation

```python
interface ILogger:
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

    # Read-only auto-property with default value
    get property id: int = 0

    # Write-only auto-property
    set property _internal_value: int
```

### Explicit Properties

```python
class Temperature:
    """Temperature with Celsius/Fahrenheit conversion."""

    __celsius: double = 0.0

    # Explicit getter and setter
    property celsius(self) -> double:
        return self.__celsius

    property celsius(self, value: double):
        self.__celsius = value

    # Computed property (read-only)
    property fahrenheit(self) -> double:
        return self.__celsius * 9/5 + 32
```

### Abstract Properties (in Interfaces)

```python
interface Measurable:
    # Abstract property requiring both getter and setter
    property length: double

    # Abstract read-only property
    get property width: double

    # Explicit abstract properties
    property height(self) -> double: ...
    property height(self, value: double): ...
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

    # Decorator always overrides inferred access modifier
    # via naming convention
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

TODO: Should behave like C# annotations.

## Functions

### Function Definition

```python
def greet(name: str) -> str:
    """Greet a person by name."""
    return f"Hello, {name}!"

# With default arguments
def power(base: double, exponent: double = 2.0) -> double:
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

def process(value: double) -> str:
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
    _value: T

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
interface IComparable[T]:
    def __lt__(self, other: T) -> bool: ...

def find_max[T: IComparable[T]](items: list[T]) -> T:
    """Find the maximum item (must be comparable)."""
    max_item = items[0]

    for item in items:
        if max_item < item:
            max_item = item

    return max_item
```

## Tuples

Tuples are immutable, fixed-size collections that can hold values of different types.

### Tuple Creation

```python
# Tuple literals
point = (10, 20)
triple = (1, "hello", True)

# Type annotations
point: tuple[int, int] = (10, 20)
triple: tuple[int, str, bool] = (1, "hello", True)

# Single-element tuple (trailing comma required)
single = (42,)

# Empty tuple
empty = ()
```

### Tuple Unpacking

```python
# Basic unpacking
x, y = point
a, b, c = triple

# Partial unpacking with wildcard
first, *rest = (1, 2, 3, 4, 5)      # first = 1, rest = [2, 3, 4, 5]
first, *middle, last = (1, 2, 3, 4)  # first = 1, middle = [2, 3], last = 4
*start, last = (1, 2, 3)             # start = [1, 2], last = 3
```

### Tuple Indexing

```python
triple = (1, "hello", True)

# Index access (returns object?, requires cast for type safety)
first = triple[0]   # Returns object?

# Typed property access (preferred)
x_val: int = point.item1
y_val: int = point.item2
```

### Tuple Iteration

```python
for item in triple:
    print(item)

# With enumeration
for i, value in enumerate(triple):
    print(f"Index {i}: {value}")
```

See [Type System - Tuples](type_system.md#tuples) for implementation details.

## Collection Literals

### Lists

```python
# List literals
numbers = [1, 2, 3]
empty: list[int] = []

# Type annotations
values: list[str] = ["a", "b", "c"]

# Nested lists
matrix: list[list[int]] = [[1, 2], [3, 4]]
```

### Dictionaries

```python
# Dict literals
mapping = {"a": 1, "b": 2}
empty_dict: dict[str, int] = {}

# Type annotations
scores: dict[str, double] = {"Alice": 95.5, "Bob": 87.0}

# Nested dicts
config: dict[str, dict[str, int]] = {
    "server": {"port": 8080, "timeout": 30}
}
```

### Sets

```python
# Set literals
unique = {1, 2, 3}
words = {"apple", "banana", "cherry"}

# Empty set (special syntax to distinguish from empty dict)
empty_set: set[int] = {/}

# Type annotations
numbers: set[int] = {1, 2, 3, 4, 5}
```

## String Formatting

### F-Strings (Interpolated Strings)

```python
name = "Alice"
age = 30

# Basic interpolation
greeting = f"Hello, {name}!"

# Expressions in interpolation
message = f"{name} is {age} years old"
calculation = f"Next year you'll be {age + 1}"

# Format specifiers
pi = 3.14159
formatted = f"Pi is approximately {pi:.2f}"  # "Pi is approximately 3.14"

# Alignment and padding
value = 42
aligned = f"{value:>10}"   # Right-align in 10 characters
padded = f"{value:0>5}"    # Pad with zeros: "00042"
```

### Raw Strings

```python
# Raw strings (backslashes not escaped)
path = r"C:\Users\Alice\Documents"
regex = r"\d+\.\d+"
```

### Multi-line Strings

```python
# Triple-quoted strings
text = """
This is a
multi-line string
with preserved whitespace
"""

# Triple-quoted f-strings
report = f"""
Name: {name}
Age: {age}
Status: Active
"""
```

## Comprehensions

Comprehensions provide concise syntax for creating collections.

### List Comprehensions

```python
# Basic list comprehension
squares = [x**2 for x in range(10)]
# Result: [0, 1, 4, 9, 16, 25, 36, 49, 64, 81]

# With condition
evens = [x for x in range(10) if x % 2 == 0]
# Result: [0, 2, 4, 6, 8]

# With transformation and condition
doubled_evens = [x * 2 for x in range(10) if x % 2 == 0]
# Result: [0, 4, 8, 12, 16]

# Nested comprehensions
matrix = [[i * j for j in range(3)] for i in range(3)]
# Result: [[0, 0, 0], [0, 1, 2], [0, 2, 4]]

# Multiple for clauses
pairs = [(x, y) for x in range(3) for y in range(3)]
# Result: [(0,0), (0,1), (0,2), (1,0), (1,1), (1,2), (2,0), (2,1), (2,2)]
```

### Dict Comprehensions

```python
# Basic dict comprehension
square_dict = {x: x**2 for x in range(5)}
# Result: {0: 0, 1: 1, 2: 4, 3: 9, 4: 16}

# With condition
even_squares = {x: x**2 for x in range(10) if x % 2 == 0}
# Result: {0: 0, 2: 4, 4: 16, 6: 36, 8: 64}

# From existing dict
prices = {"apple": 1.0, "banana": 0.5, "cherry": 2.0}
discounted = {k: v * 0.9 for k, v in prices.items()}
```

### Set Comprehensions

```python
# Basic set comprehension
unique_lengths = {len(word) for word in ["apple", "banana", "cherry"]}
# Result: {5, 6}

# With condition
consonants = {c for c in "hello world" if c not in "aeiou "}
# Result: {'h', 'l', 'w', 'r', 'd'}
```

## Slicing

Slicing extracts portions of sequences (lists, tuples, strings).

### Basic Slicing

```python
numbers = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]

# Basic slice [start:end] (end is exclusive)
subset = numbers[1:4]      # [1, 2, 3]
prefix = numbers[:3]       # [0, 1, 2] (start defaults to 0)
suffix = numbers[7:]       # [7, 8, 9] (end defaults to length)
full_copy = numbers[:]     # [0, 1, 2, ..., 9]

# Negative indices (count from end)
last_three = numbers[-3:]  # [7, 8, 9]
all_but_last = numbers[:-1] # [0, 1, 2, ..., 8]
```

### Slicing with Step

```python
numbers = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]

# [start:end:step]
evens = numbers[::2]       # [0, 2, 4, 6, 8] (every 2nd element)
odds = numbers[1::2]       # [1, 3, 5, 7, 9]
reversed = numbers[::-1]   # [9, 8, 7, 6, 5, 4, 3, 2, 1, 0]

# Subset with step
every_third = numbers[2:9:3]  # [2, 5, 8]
```

### String Slicing

```python
text = "Hello, World!"

# Same syntax works for strings
greeting = text[:5]        # "Hello"
world = text[7:12]         # "World"
reversed = text[::-1]      # "!dlroW ,olleH"
```

## Operator Overloading

Classes can define special methods (dunder methods) to customize operator behavior.

### Arithmetic Operators

```python
class Vector:
    x: double
    y: double

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def __add__(self, other: Vector) -> Vector:
        """Overload + operator."""
        return Vector(self.x + other.x, self.y + other.y)

    def __sub__(self, other: Vector) -> Vector:
        """Overload - operator."""
        return Vector(self.x - other.x, self.y - other.y)

    def __mul__(self, scalar: double) -> Vector:
        """Overload * operator for scalar multiplication."""
        return Vector(self.x * scalar, self.y * scalar)

    def __neg__(self) -> Vector:
        """Overload unary - operator."""
        return Vector(-self.x, -self.y)

# Usage
v1 = Vector(1.0, 2.0)
v2 = Vector(3.0, 4.0)
v3 = v1 + v2           # Vector(4.0, 6.0)
v4 = v1 * 2.0          # Vector(2.0, 4.0)
v5 = -v1               # Vector(-1.0, -2.0)
```

### Comparison Operators

```python
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __eq__(self, other: Point) -> bool:
        """Overload == operator."""
        return self.x == other.x and self.y == other.y

    def __lt__(self, other: Point) -> bool:
        """Overload < operator (for sorting)."""
        return (self.x, self.y) < (other.x, other.y)

    def __le__(self, other: Point) -> bool:
        """Overload <= operator."""
        return self < other or self == other

# Usage
p1 = Point(1, 2)
p2 = Point(1, 2)
p3 = Point(2, 3)

print(p1 == p2)  # True
print(p1 < p3)   # True
```

### Container Operators

```python
class Grid:
    _data: list[list[int]]

    def __init__(self, width: int, height: int):
        self._data = [[0] * width for _ in range(height)]

    def __getitem__(self, key: tuple[int, int]) -> int:
        """Overload indexing: grid[x, y]."""
        x, y = key
        return self._data[y][x]

    def __setitem__(self, key: tuple[int, int], value: int):
        """Overload assignment: grid[x, y] = value."""
        x, y = key
        self._data[y][x] = value

# Usage
grid = Grid(10, 10)
grid[5, 3] = 42
value = grid[5, 3]  # 42
```

See [Type System - Dunder Methods](type_system.md#dunder-methods) for complete list.

## Type Aliases and Enums

### Type Aliases

Type aliases create readable names for complex types:

```python
# Simple aliases
type UserId = int
type Coordinate = tuple[double, double]

# Generic aliases
type Matrix = list[list[double]]

# Callback type aliases
type Callback[T] = (T) -> None
type Comparator[T] = (T, T) -> int

# Usage
def process_user(id: UserId) -> None:
    pass

def distance(p1: Coordinate, p2: Coordinate) -> double:
    pass
```

Note that union types e.g. `int | str | None` are not allowed in Sharpy.

### Enumerations

Enums define a set of named constants:

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# Usage
favorite = Color.RED
print(favorite)  # Color.RED

# Comparison
if favorite == Color.RED:
    print("Red is your favorite")

# Enums with methods
enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETED = 2

    def is_done(self) -> bool:
        return self == Status.COMPLETED

# Usage
status = Status.ACTIVE
if not status.is_done():
    print("Still processing")
```

### Enum with String Values

```python
enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"

# Usage
method = HttpMethod.GET
request_type = method.value  # "GET"
```

## Assertions

Assertions verify conditions during development:

```python
def divide(a: double, b: double) -> double:
    assert b != 0, "Division by zero"
    return a / b

def process_list(items: list[int]):
    assert len(items) > 0, "List must not be empty"
    assert all(x >= 0 for x in items), "All items must be non-negative"
    # Process items...
```

**Behavior:**
- In debug builds: Throws `AssertionError` if condition is false
- In release builds: Assertions may be removed for performance

## Del Statement

The `del` statement removes items from collections:

```python
# Remove dictionary entries
d = {"a": 1, "b": 2, "c": 3}
del d["b"]
# d is now {"a": 1, "c": 3}

# Remove list elements by index
numbers = [1, 2, 3, 4, 5]
del numbers[2]
# numbers is now [1, 2, 4, 5]

# Remove slices
numbers = [1, 2, 3, 4, 5]
del numbers[1:3]
# numbers is now [1, 4, 5]
```

**Note:** Unlike Python, `del` in Sharpy:
- Only works for objects that define the `__delitem__()` dunder method, e.g. dictionaries, lists, etc.
- Does not trigger `__del__` destructors (use interface `IDisposable` instead)
- Cannot delete class attributes or module-level names

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

# Else block after loops (both for and while) that don't
# exit via break
for i in some_values:
    if i is None:
        break
else:
    print("None value not found")
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

Objects created in the initial expression and/or in the body are scoped to the `with`-block. If an object is passed into the `with`-block's initializer expression and it implements `IContextManager` (and therefore has the `__enter__()` and/or `__exit__()` dunder methods), its `__enter__()` method will be invoked to produce an object (could be itself) that is scoped to the `with`-block with an optional alias via the `as`-statement. If the object returned here implements `IDisposable` and therefore implements a `dispose()` method, that will be invoked when the `with`-block exits. At the end of the `with`-block, the `__exit__()` method on the object passed in will be invoked.

The order of `dispose()` and `__exit__()` calls is as follows: for every pair of a returned object from `__enter__()` and its originating context manager (the object that had the `__enter__()` method), in reverse order of declaration in the `with`-block (LIFO order), invoke `dispose()` on the returned object from `__enter__()` if that object implements `IDisposable`, then invoke `__exit__()` on its context manager.

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

| Identifier Type | Sharpy Convention | Compiled Form | Notes |
|----------------|-------------------|---------------|-------|
| Module | `snake_case` | `PascalCase` | - |
| Class | `PascalCase` | (unchanged) | - |
| Struct | `PascalCase` | (unchanged) | - |
| Interface | `IPascalCase` | (unchanged) | - |
| Members | `snake_case` | `PascalCase` | - |
| Enum | `PascalCase` | (unchanged) | - |
| Enum values | `CAPS_SNAKE_CASE` | `PascalCase` | - |
| Function | `snake_case` | `PascalCase` | - |
| Parameters | `snake_case` | `camelCase` | - |
| Local variables | `snake_case` | (unchanged) | - |
| Constants | `CAPS_SNAKE_CASE` | (unchanged) | **Convention only** - use `const` keyword for true constants |

**Note on Constants:** The `CAPS_SNAKE_CASE` naming style is a convention for readability, but does **not** make a variable constant. Only the `const` keyword creates true compile-time constants:

```python
# Convention suggests this is constant, but it's NOT - can be reassigned
MAX_SIZE: int = 100
MAX_SIZE = 200  # OK - no const keyword

# This IS a constant - cannot be reassigned
const MAX_SIZE: int = 100
MAX_SIZE = 200  # ERROR - const keyword used
```

### Examples

```python
# Module: my_module.spy -> namespace MyModule
# Function: add_numbers() -> AddNumbers()
# Parameter: user_name -> userName
# Constant: const MAX_VALUE -> const MAX_VALUE

def calculate_total(item_count: int, price_per_item: double) -> double:
    """Calculate total price."""
    const TAX_RATE = 0.08  # By convention, use CAPS_SNAKE_CASE
    return item_count * price_per_item * (1 + TAX_RATE)
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
