# Sharpy Language Reference

## Version Guide

Features are marked with their target version:

- **[v0.5]** - P0: Core features required for C# interop and Unity development
- **[v1.0]** - P1: Enhanced features and Pythonic additions
- **[v1.5+]** - P2: Advanced features and language extensions

---

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
## Keywords and Operators **[v0.5]**

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
| `defer` | Deferral of cleanup expressions |
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
| `case` | Case block for match statements |
| `event` | Event handler definition |
| `get` | Property getter definition |
| `match` | Match statement for pattern matching |
| `property` | Property definition |
| `set` | Property setter definition |
| `type` | Type alias declaration |
| `_` | Wildcard in match blocks |

## Type Aliases **[v1.0]**

Type aliases create readable names for complex types. They can be declared at module, class, or function level:

### Module-Level Type Aliases

```python
# Simple aliases
type UserId = int
type Coordinate = tuple[double, double]

# Generic aliases
type Matrix = list[list[double]]
type Callback[T] = (T) -> None
type Result[T, E] = Result[T, E]  # Re-export with friendly name
```

### Class-Level Type Aliases

```python
class Geometry:
    """Geometric calculations."""

    # Type alias scoped to class
    type Point3D = tuple[double, double, double]
    type Vector3D = Point3D

    def distance(p1: Point3D, p2: Point3D) -> double:
        dx, dy, dz = p1[0] - p2[0], p1[1] - p2[1], p1[2] - p2[2]
        return (dx**2 + dy**2 + dz**2) ** 0.5
```

### Function-Level Type Aliases

Function-level type aliases are scoped to the function and particularly useful for complex generic types:

```python
def process_data[T, E](
    items: dict[str, list[Result[T, E]]],
    transform: (T) -> Result[T, E]
) -> dict[str, list[Result[T, E]]]:

    # Function-local type aliases improve readability
    type DataMap = dict[str, list[Result[T, E]]]
    type Transform = (T) -> Result[T, E]
    type ItemResult = Result[T, E]

    result: DataMap = {}

    for key, values in items.items():
        transformed: list[ItemResult] = []
        for item_result in values:
            match item_result:
                case Result.Ok(value):
                    new_result: ItemResult = transform(value)
                    transformed.append(new_result)
                case Result.Err(error):
                    transformed.append(Result.err(error))
        result[key] = transformed

    return result
```

**Benefits of function-level type aliases:**
- Reduce repetition of complex generic types
- Improve readability of type signatures
- Document semantic meaning of types
- Can reference enclosing function's generic parameters

**Scope rules:**
- Type aliases are scoped to their declaration level (module/class/function)
- Function-level aliases can shadow module/class-level aliases
- Function-level aliases can reference the function's generic type parameters
- Type aliases are evaluated at compile time, not runtime

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

### Operator Precedence

Operators are listed from highest to lowest precedence:

| Precedence | Operators | Description | Associativity |
|------------|-----------|-------------|---------------|
| 1 | `()`, `[]`, `.`, `?.` | Grouping, indexing, attribute access, null-conditional access | Left-to-right |
| 2 | `**` | Exponentiation | Right-to-left |
| 3 | `+x`, `-x`, `~x` | Unary plus, minus, bitwise NOT | Right-to-left |
| 4 | `*`, `/`, `//`, `%`, `@` | Multiplication, division, floor division, modulo, matrix multiply | Left-to-right |
| 5 | `+`, `-` | Addition, subtraction | Left-to-right |
| 6 | `<<`, `>>` | Bitwise shifts | Left-to-right |
| 7 | `&` | Bitwise AND | Left-to-right |
| 8 | `^` | Bitwise XOR | Left-to-right |
| 9 | `\|` | Bitwise OR | Left-to-right |
| 10 | `in`, `not in`, `is`, `is not`, `<`, `<=`, `>`, `>=`, `!=`, `==` | Comparisons, membership, identity | Left-to-right |
| 11 | `not` | Logical NOT | Right-to-left |
| 12 | `and` | Logical AND | Left-to-right |
| 13 | `or` | Logical OR | Left-to-right |
| 14 | `??` | Null coalescing | Left-to-right |
| 15 | `if`-`else` | Conditional expression | Right-to-left |
| 16 | `lambda` | Lambda expression | N/A |
| 17 | `:=` | Walrus operator (assignment expression) | Right-to-left |

**Notes:**
- Comparison operators can be chained: `a < b < c` is equivalent to `a < b and b < c`
- The walrus operator `:=` allows assignment within expressions (see [Walrus Operator](#walrus-operator))
- The `??` operator has lower precedence than `or`, so `a or b ?? c` is `(a or b) ?? c`

## Literals and Special Values **[v0.5]**

### Special Literals

| Sharpy | Python Equivalent | C# Equivalent | Notes |
|--------|-------------------|---------------|-------|
| `...` | `...` | - | Ellipsis literal as a placeholder or for slices |
| `False` | `False` | `false` | Boolean false |
| `None` | `None` | `null` | Null/absence of value |
| `True` | `True` | `true` | Boolean true |
| `{/}` | `set()` | - | Empty set literal, borrowed from PEP 802 |

**Ellipsis (`...`) Usage:**

The ellipsis literal has several uses:

```python
# 1. Placeholder in interface/abstract method definitions
interface IDrawable:
    def draw(self) -> None:
        ...

# 2. Placeholder for unimplemented code (like 'pass')
def todo_function():
    ...

# 3. Open-ended slicing (take all remaining elements)
matrix = [[1, 2, 3], [4, 5, 6], [7, 8, 9]]
first_row = matrix[0, ...]  # [1, 2, 3]
first_col = matrix[..., 0]  # [1, 4, 7]

# 4. Type hint for variable-length homogeneous tuples
def process(values: tuple[int, ...]) -> None:
    # Accepts tuples of any length containing ints
    pass
```

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

## Type Syntax **[v0.5]**

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

### Type Narrowing

Sharpy performs type narrowing in conditional branches:

```python
# Nullable type narrowing
value: str? = get_optional_string()

if value is not None:
    # Inside this block, 'value' is narrowed to 'str'
    print(value.upper())  # OK - value is str, not str?

# isinstance() narrowing
obj: object = get_value()

if isinstance(obj, str):
    # Inside this block, 'obj' is narrowed to 'str'
    print(obj.upper())  # OK

# Pattern matching narrowing (see Match Statements)
match value:
    case int() as i:
        # 'i' is narrowed to 'int'
        print(i + 1)
    case str() as s:
        # 's' is narrowed to 'str'
        print(s.upper())
```

**Narrowing Rules:**
- `is not None` narrows nullable type to non-nullable
- `isinstance(x, Type)` narrows to that type
- Pattern matching with type patterns narrows to matched type
- Narrowing only affects the scope of the conditional block

### Qualified Types

```python
from system.collections.generic import hash_set

# Fully qualified type names
numbers = hash_set[int]()
```

## Variable Assignment and Scope **[v0.5]**

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

### Comprehension and Loop Variable Scope

Variables declared in comprehensions and loop expressions are scoped to that comprehension/loop and do not leak into the outer scope:

```python
# List comprehension
squares = [i ** 2 for i in range(10)]
print(i)  # ERROR: 'i' does not exist in this scope

# Dict comprehension
mapping = {k: v for k, v in items}
print(k)  # ERROR: 'k' does not exist in this scope

# For loop
for item in collection:
    process(item)
print(item)  # ERROR: 'item' does not exist outside loop

# Exception: Generator expressions evaluated lazily
gen = (x ** 2 for x in range(10))
# 'x' is scoped to generator execution, not outer scope
```

**Note:** This differs from Python 2 behavior where loop variables leaked into the outer scope. Sharpy follows Python 3+ scoping rules.

## Constants **[v0.5]**

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

### Static vs Instance Members

**Static members** belong to the class itself, not instances:

```python
class MathUtils:
    # Static field (class-level)
    const PI: double = 3.14159

    # Static method
    @static
    def square(x: double) -> double:
        return x ** 2

    # Static property
    @static
    property version() -> str:
        return "1.0.0"

# Access static members via class name
result = MathUtils.square(5)        # 25
pi = MathUtils.PI                   # 3.14159
version = MathUtils.version         # "1.0.0"

# Static members NOT accessible via instances
obj = MathUtils()
obj.square(5)  # ERROR - cannot access static member via instance
```

**Instance members** belong to each object instance:

```python
class Counter:
    # Instance field (per-object)
    count: int = 0

    # Instance method
    def increment(self):
        self.count += 1

    # Instance property
    property value(self) -> int:
        return self.count

# Each instance has its own state
c1 = Counter()
c2 = Counter()

c1.increment()
print(c1.value)  # 1
print(c2.value)  # 0 - separate instance
```

**Class attributes (shared state):**

To create shared state across all instances (like Python class variables), use static fields:

```python
class BankAccount:
    # Shared across all instances
    @static
    total_accounts: int = 0

    # Per-instance data
    balance: double

    def __init__(self, initial_balance: double):
        self.balance = initial_balance
        BankAccount.total_accounts += 1  # Increment shared counter

# All instances share total_accounts
acc1 = BankAccount(100.0)
acc2 = BankAccount(200.0)
print(BankAccount.total_accounts)  # 2
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

### Default Parameter Evaluation

Default parameter values are evaluated **once** at function definition time, not per call. This means mutable default parameters share the same instance across calls:

```python
# DANGER: Mutable default parameter
def append_to(item: int, target: list[int] = []) -> list[int]:
    target.append(item)
    return target

list1 = append_to(1)  # [1]
list2 = append_to(2)  # [1, 2] - SAME list as list1!

# SAFE: Use None and create new instance
def append_to_safe(item: int, target: list[int]? = None) -> list[int]:
    if target is None:
        target = []
    target.append(item)
    return target

list3 = append_to_safe(1)  # [1]
list4 = append_to_safe(2)  # [2] - different list
```

**Best Practice:** Never use mutable objects (lists, dicts, sets) as default parameter values. Use `None` and create the instance inside the function.

## Modules and Imports **[v0.5]**

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

## Classes **[v0.5]**

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

**Constructor Chaining:**

When a derived class defines a constructor, it **must** call the base class constructor using `super().__init__()`. The call to `super().__init__()` does not need to be the first statement, but all base class initialization must complete before accessing inherited members:

```python
class Base:
    value: int

    def __init__(self, value: int):
        self.value = value

class Derived(Base):
    multiplier: int

    def __init__(self, value: int, multiplier: int):
        # Setup can happen before super().__init__()
        self.multiplier = multiplier

        # Call base constructor
        super().__init__(value * multiplier)

        # Can access base members after super().__init__()
        print(f"Base value: {self.value}")
```

**Multiple Inheritance Rules:**

Sharpy supports single class inheritance plus multiple interface implementation:

```python
# OK: Single class inheritance
class Dog(Animal):
    pass

# OK: Class + multiple interfaces
class JSONEmployee(Employee, ISerializable, IComparable):
    pass

# OK: Multiple interfaces only (no base class)
class Point(IDrawable, IComparable):
    pass

# ERROR: Multiple class inheritance not allowed
class Invalid(ClassA, ClassB):  # ERROR: At most one base class allowed
    pass
```

**Inheritance Order:**
- If present, the base class **must** come first in the inheritance list
- Interfaces follow the base class
- Order of interfaces affects method resolution when ambiguous

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

## Structs **[v0.5]**

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

## Interfaces **[v0.5]**

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

## Properties **[v0.5]**

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

**Property Definition Rules:**

A property can have:
- **Getter only** (read-only property)
- **Setter only** (write-only property, rarely used)
- **Both getter and setter** (read-write property)

```python
class Example:
    _value: int = 0

    # Read-only property
    property read_only(self) -> int:
        return self._value

    # Write-only property
    property write_only(self, value: int):
        self._value = value

    # Read-write property
    property read_write(self) -> int:
        return self._value

    property read_write(self, value: int):
        self._value = value

# Usage
obj = Example()
obj.write_only = 42       # OK - has setter
x = obj.read_only         # OK - has getter
obj.read_only = 10        # ERROR - no setter
y = obj.write_only        # ERROR - no getter
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

## Access Modifiers **[v0.5]**

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

## Events **[v1.0]**

Events provide a publish-subscribe pattern for notifications. They are similar to properties but designed for multicast delegates.

### Event Declaration

```python
class Button:
    """A button that can be clicked."""

    # Event declaration
    event clicked: (object, EventArgs) -> None

    def click(self):
        """Simulate a button click."""
        # Raise the event if there are subscribers
        if self.clicked is not None:
            self.clicked(self, EventArgs())

# Event subscription
button = Button()

def on_button_clicked(sender: object, args: EventArgs):
    print("Button was clicked!")

# Subscribe to event
button.clicked += on_button_clicked

# Trigger event
button.click()  # Prints: "Button was clicked!"

# Unsubscribe from event
button.clicked -= on_button_clicked
```

### Event with Custom EventArgs

```python
class ValueChangedEventArgs(EventArgs):
    old_value: int
    new_value: int

    def __init__(self, old_value: int, new_value: int):
        self.old_value = old_value
        self.new_value = new_value

class Counter:
    event value_changed: (object, ValueChangedEventArgs) -> None
    _value: int = 0

    property value(self) -> int:
        return self._value

    property value(self, new_value: int):
        old = self._value
        self._value = new_value

        # Raise event with custom args
        if self.value_changed is not None:
            self.value_changed(self, ValueChangedEventArgs(old, new_value))

# Usage
counter = Counter()

def on_value_changed(sender: object, args: ValueChangedEventArgs):
    print(f"Value changed from {args.old_value} to {args.new_value}")

counter.value_changed += on_value_changed
counter.value = 42  # Prints: "Value changed from 0 to 42"
```

### Event Rules

- Events can only be invoked from within the declaring class
- Events support `+=` (subscribe) and `-=` (unsubscribe) operators
- Events can have multiple subscribers (multicast)
- Event handlers are invoked in subscription order
- If no subscribers exist, event is `None`

## Decorators **[v0.5]**

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

## Functions **[v0.5]**

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

## Generics **[v0.5]**

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

## Tuples **[v0.5]**

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

## Collection Literals **[v0.5]**

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

### Collection Mutability

Collections have different mutability characteristics:

| Collection | Mutable? | Notes |
|------------|----------|-------|
| `list[T]` | ✅ Yes | Can append, remove, modify elements |
| `dict[K, V]` | ✅ Yes | Can add, remove, modify entries |
| `set[T]` | ✅ Yes | Can add, remove elements |
| `tuple[...]` | ❌ No | Immutable, fixed-size |
| `frozenset[T]` | ❌ No | Immutable set |
| `str` | ❌ No | Immutable string |
| `bytes` | ❌ No | Immutable byte array |
| `bytearray` | ✅ Yes | Mutable byte array |

```python
# Mutable collections
numbers = [1, 2, 3]
numbers.append(4)      # OK - list is mutable
numbers[0] = 10        # OK - can modify elements

mapping = {"a": 1}
mapping["b"] = 2       # OK - dict is mutable

unique = {1, 2, 3}
unique.add(4)          # OK - set is mutable

# Immutable collections
point = (10, 20)
point[0] = 30          # ERROR - tuple is immutable

text = "hello"
text[0] = "H"          # ERROR - string is immutable

frozen = frozenset([1, 2, 3])
frozen.add(4)          # ERROR - frozenset is immutable
```

**Creating Immutable Copies:**

```python
# List to tuple (immutable)
numbers = [1, 2, 3]
immutable_numbers = tuple(numbers)

# Set to frozenset (immutable)
unique = {1, 2, 3}
immutable_unique = frozenset(unique)

# String is already immutable
text = "hello"  # Always immutable
```

## Walrus Operator **[v1.0]**

The walrus operator `:=` allows assignment within expressions. This is useful for capturing values in comprehensions, conditionals, and other expressions:

```python
# Capture value in conditional
if (match := pattern.search(text)) is not None:
    print(f"Found match at position {match.start()}")

# Reuse computed value in comprehension
results = [y for x in data if (y := transform(x)) is not None]

# Avoid repeated function calls
while (line := file.read_line()) is not None:
    process(line)

# In list comprehension
data = [1, 2, 3, 4, 5]
filtered = [y for x in data if (y := x * 2) > 5]
# Result: [6, 8, 10] (filtered where doubled value > 5)
```

**Scoping Rules:**

The walrus operator creates or assigns to a variable in the **enclosing scope**, not a new scope:

```python
# Variable created in function scope
def process():
    if (x := compute()) > 0:
        # x exists here
        print(x)
    # x still exists here
    print(x)

# In comprehension, variable scoped to comprehension
results = [y for item in data if (y := transform(item)) is not None]
print(y)  # ERROR: y does not exist outside comprehension
```

**Type Inference:**

The type of a walrus-assigned variable is inferred from the right-hand side expression:

```python
if (x := get_value()) > 0:  # x inferred as int (or whatever get_value returns)
    print(x + 1)
```

## String Formatting **[v0.5]**

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

**Escape Sequences in Regular Strings:**

Regular (non-raw) strings support standard escape sequences:

| Escape | Meaning |
|--------|---------|
| `\\` | Backslash |
| `\'` | Single quote |
| `\"` | Double quote |
| `\n` | Newline |
| `\r` | Carriage return |
| `\t` | Tab |
| `\b` | Backspace |
| `\f` | Form feed |
| `\0` | Null character |
| `\xHH` | Character with hex value HH (e.g., `\x41` = 'A') |
| `\uHHHH` | Unicode character with 16-bit hex value HHHH |
| `\UHHHHHHHH` | Unicode character with 32-bit hex value HHHHHHHH |

```python
# Escape sequences
newline = "Hello\nWorld"
tab = "Column1\tColumn2"
unicode_char = "Greek alpha: \u03B1"
emoji = "Rocket: \U0001F680"
hex_char = "Capital A: \x41"
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

## Comprehensions **[v1.0]**

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

## Slicing **[v0.5]**

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

## Operator Overloading **[v0.5]**

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

## Type Aliases and Enums **[v0.5]**

### Type Aliases **[v0.5]**

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

### None Type Semantics

`None` has different meanings depending on context:

**As a function return type:**

```python
def no_return() -> None:
    """Function that returns nothing."""
    print("Hello")
    # Implicit return None

def explicit_return() -> None:
    print("Hello")
    return  # Can explicitly return (without value)
```

**As a value (null reference):**

```python
# None as nullable reference
name: str? = None

# Checking for None
if name is None:
    print("No name provided")

if name is not None:
    print(f"Name: {name}")
```

**Type distinctions:**

| Context | Sharpy Syntax | Meaning | Compiled Form |
|---------|---------------|---------|---------------|
| Return type | `-> None` | No return value | `void` |
| Variable type | `x: None` | Not allowed | - |
| Nullable variable | `x: str?` | Can be null | Nullable reference |
| None literal | `None` | Null value | `null` |

```python
# Valid uses of None
def do_work() -> None:           # OK - return type
    pass

x: str? = None                   # OK - nullable variable with None value
y = None                         # OK - inferred as object? (nullable object)

# Invalid uses of None
def broken() -> None:
    return 42                     # ERROR - cannot return value from None function

z: None = None                   # ERROR - None is not a valid variable type
```

### Enumerations **[v0.5]**

Sharpy supports two kinds of enumerations: **simple enums** (C#-style) and **tagged unions** (Rust/Swift-style algebraic data types).

#### Simple Enums **[v0.5]**

Simple enums define a set of named constants and map directly to C# enums:

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# With string values
enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"

# Usage
favorite = Color.RED
method = HttpMethod.GET

# Comparison
if favorite == Color.RED:
    print("Red is your favorite")

# Getting underlying value
value = favorite.value  # 1
name = favorite.name    # "RED"
```

**Simple Enum Rules:**
- All cases must have explicit constant values
- Values must be integers or strings
- No associated data with cases
- Compiles to C# `enum` type

#### Tagged Unions (Algebraic Data Types) **[v1.0]**

Tagged unions allow cases to carry associated data, enabling type-safe representation of variants:

```python
# Generic Result type (like Rust's Result)
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Option type (like Rust's Option or Swift's Optional)
enum Option[T]:
    case Some(value: T)
    case None()

# Tree structure
enum BinaryTree[T]:
    case Leaf(value: T)
    case Node(left: BinaryTree[T], right: BinaryTree[T])

# JSON representation
enum JsonValue:
    case Null()
    case Bool(value: bool)
    case Number(value: double)
    case String(value: str)
    case Array(items: list[JsonValue])
    case Object(fields: dict[str, JsonValue])
```

**Creating Tagged Union Values:**

```python
# Using case constructors
success = Result.Ok(42)
failure = Result.Err("Something went wrong")

# Or using factory methods (lowercase convention)
success = Result.ok(42)
failure = Result.err("Something went wrong")

# Nested structures
tree = BinaryTree.Node(
    BinaryTree.Leaf(1),
    BinaryTree.Node(
        BinaryTree.Leaf(2),
        BinaryTree.Leaf(3)
    )
)
```

**Pattern Matching Tagged Unions:**

```python
def divide(a: double, b: double) -> Result[double, str]:
    if b == 0:
        return Result.err("Division by zero")
    return Result.ok(a / b)

# Exhaustive pattern matching
result = divide(10, 2)
match result:
    case Result.Ok(value):
        print(f"Success: {value}")
    case Result.Err(error):
        print(f"Error: {error}")

# Nested pattern matching
def sum_tree(tree: BinaryTree[int]) -> int:
    match tree:
        case BinaryTree.Leaf(value):
            return value
        case BinaryTree.Node(left, right):
            return sum_tree(left) + sum_tree(right)
```

**Tagged Union Methods:**

Tagged unions can have methods just like classes:

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

    def is_ok(self) -> bool:
        match self:
            case Result.Ok(): return True
            case Result.Err(): return False

    def is_err(self) -> bool:
        return not self.is_ok()

    def unwrap(self) -> T:
        """Get the Ok value or raise an exception."""
        match self:
            case Result.Ok(value): return value
            case Result.Err(error): raise Exception(f"Called unwrap on Err: {error}")

    def unwrap_or(self, default: T) -> T:
        """Get the Ok value or return default."""
        match self:
            case Result.Ok(value): return value
            case Result.Err(): return default

    def map[U](self, f: (T) -> U) -> Result[U, E]:
        """Transform the Ok value if present."""
        match self:
            case Result.Ok(value): return Result.ok(f(value))
            case Result.Err(error): return Result.err(error)

# Usage
result = divide(10, 2)
if result.is_ok():
    print(result.unwrap())

# Chaining operations
final = divide(10, 2).map(lambda x: x * 2).unwrap_or(0.0)
```

**Type Checks:**

```python
result = divide(10, 0)

# Using isinstance with case types
if isinstance(result, Result.Ok):
    print(f"Success: {result.value}")
elif isinstance(result, Result.Err):
    print(f"Error: {result.error}")

# Checking which variant
is_success = isinstance(result, Result.Ok)
```

**Tagged Union Rules:**
- Cases must have parameters (use `case Name()` for parameterless cases)
- Cannot mix simple enum cases and tagged union cases
- Compiles to abstract base class with nested sealed case classes
- Each case class has properties for associated data
- Provides `Deconstruct` methods for C# pattern matching

#### Enum Methods and Properties

Both simple enums and tagged unions can have methods:

```python
# Simple enum with methods
enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETED = 2
    CANCELLED = 3

    def is_done(self) -> bool:
        return self == Status.COMPLETED or self == Status.CANCELLED

    def can_cancel(self) -> bool:
        return self == Status.PENDING or self == Status.ACTIVE

# Usage
status = Status.ACTIVE
if status.can_cancel():
    status = Status.CANCELLED
```

#### Exhaustiveness Checking

The compiler enforces exhaustive pattern matching for both enum types:

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# ERROR: Non-exhaustive match (missing BLUE)
match color:
    case Color.RED: print("Red")
    case Color.GREEN: print("Green")

# OK: Exhaustive with all cases
match color:
    case Color.RED: print("Red")
    case Color.GREEN: print("Green")
    case Color.BLUE: print("Blue")

# OK: Exhaustive with wildcard
match color:
    case Color.RED: print("Red")
    case _: print("Other color")
```

#### Interoperability

**Simple Enums:**
- Directly compatible with C# enums
- Can use .NET enums in Sharpy code
- Preserve underlying type (int, string, etc.)

```python
from system.io import FileMode

# Use .NET enum
mode = FileMode.CREATE

match mode:
    case FileMode.CREATE: print("Creating")
    case FileMode.OPEN: print("Opening")
    case _: print("Other mode")
```

**Tagged Unions:**
- Compile to C# abstract class hierarchy
- C# code can pattern match using C# 8+ switch expressions
- Provide `Deconstruct` methods for C# deconstruction

```csharp
// C# consuming Sharpy tagged union
Result<int, string> result = SharpyModule.Divide(10, 2);

// C# pattern matching
var message = result switch
{
    Result<int, string>.Ok { Value: var v } => $"Success: {v}",
    Result<int, string>.Err { Error: var e } => $"Error: {e}",
    _ => "Unknown"
};

// Or with deconstruction
if (result is Result<int, string>.Ok(var value))
{
    Console.WriteLine(value);
}
```

## Assertions **[v0.5]**

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

## Del Statement **[v1.0]**

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

## Control Flow **[v0.5]**

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

### Match Statements **[v1.0]**

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

Match statements provide powerful pattern matching capabilities for structural matching and type discrimination.

### Pattern Matching Syntax

**Literal Patterns:**

```python
match status_code:
    case 200:
        print("OK")
    case 404:
        print("Not Found")
    case 500:
        print("Server Error")
    case _:
        print("Unknown status")
```

**Type Patterns:**

```python
match value:
    case int():
        print("It's an integer")
    case str():
        print("It's a string")
    case list():
        print("It's a list")
    case _:
        print("Unknown type")
```

**Type Patterns with Binding:**

```python
match value:
    case int() as i:
        # i is bound to value, narrowed to int
        print(f"Integer: {i + 1}")
    case str() as s:
        # s is bound to value, narrowed to str
        print(f"String: {s.upper()}")
    case list() as lst:
        print(f"List length: {len(lst)}")
```

**Destructuring Patterns:**

```python
# Tuple destructuring
match point:
    case (0, 0):
        print("Origin")
    case (0, y):
        print(f"On Y-axis at {y}")
    case (x, 0):
        print(f"On X-axis at {x}")
    case (x, y):
        print(f"Point at ({x}, {y})")

# List destructuring
match values:
    case []:
        print("Empty list")
    case [x]:
        print(f"Single element: {x}")
    case [x, y]:
        print(f"Two elements: {x}, {y}")
    case [first, *rest]:
        print(f"First: {first}, Rest: {rest}")
    case [*init, last]:
        print(f"Init: {init}, Last: {last}")

# Dict destructuring
match config:
    case {"host": host, "port": port}:
        print(f"Server at {host}:{port}")
    case {"path": path}:
        print(f"File path: {path}")
```

**Guard Clauses:**

Guard clauses add additional conditions to patterns:

```python
match value:
    case int() as i if i > 0:
        print("Positive integer")
    case int() as i if i < 0:
        print("Negative integer")
    case int():
        print("Zero")

match point:
    case (x, y) if x == y:
        print("On diagonal")
    case (x, y) if x > y:
        print("Above diagonal")
    case (x, y):
        print("Below diagonal")
```

**OR Patterns:**

Multiple patterns can match the same case using `|`:

```python
match command:
    case "quit" | "exit" | "q":
        print("Exiting...")
    case "help" | "h" | "?":
        print("Showing help...")
    case _:
        print("Unknown command")

match value:
    case 0 | 1 | 2:
        print("Small number")
    case int() | float():
        print("Numeric value")
```

**Class Patterns:**

Match against class instances with property matching:

```python
class Point:
    x: double
    y: double

match shape:
    case Point(x=0, y=0):
        print("Origin point")
    case Point(x=x, y=0):
        print(f"On X-axis at {x}")
    case Point(x=0, y=y):
        print(f"On Y-axis at {y}")
    case Point(x=x, y=y):
        print(f"Point at ({x}, {y})")
```

**Wildcard Pattern:**

The underscore `_` matches anything and discards the value:

```python
match response:
    case {"status": "ok", "data": data}:
        process(data)
    case {"status": "error", "message": msg}:
        print(f"Error: {msg}")
    case _:
        print("Unknown response format")

# Wildcards in destructuring
match point:
    case (x, _):  # Match any point, capture only x coordinate
        print(f"X coordinate: {x}")
```

**Match as Expression:**

Match statements can return values:

```python
result = match value:
    case int() as i if i > 0: "positive"
    case int() as i if i < 0: "negative"
    case _: "zero"

# More complex example
color = match status:
    case 200: "green"
    case 404: "yellow"
    case 500: "red"
    case _: "gray"
```

### Exhaustiveness Checking

The compiler warns if match patterns don't cover all possible cases for enum types:

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# WARNING: Non-exhaustive match
match color:
    case Color.RED:
        print("Red")
    case Color.GREEN:
        print("Green")
    # Missing Color.BLUE case

# OK: Exhaustive with wildcard
match color:
    case Color.RED:
        print("Red")
    case Color.GREEN:
        print("Green")
    case _:
        print("Other color")
```

## Exception Handling **[v0.5]**

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

## Context Managers **[v1.0]**

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

## Async Programming **[v1.5+]**

Async programming enables concurrent execution of I/O-bound operations without blocking.

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

### Running Async Code

Async functions must be awaited or run through an async runtime:

```python
# Top-level async (in async context)
async def main():
    result = await some_async_function()
    print(result)

# Running from synchronous code
import asyncio

def sync_main():
    # Run async function from sync context
    result = asyncio.run(main())
```

### Concurrent Execution

```python
async def fetch_all(urls: list[str]) -> list[str]:
    """Fetch multiple URLs concurrently."""
    # Create tasks for concurrent execution
    tasks = [fetch_data(url) for url in urls]

    # Wait for all tasks to complete
    results = await asyncio.gather(*tasks)
    return results

async def main():
    urls = [
        "https://api1.com",
        "https://api2.com",
        "https://api3.com"
    ]

    # All fetches run concurrently
    data = await fetch_all(urls)

    for item in data:
        print(item)
```

### Task Management

```python
async def background_work():
    """Long-running background task."""
    while True:
        await asyncio.sleep(1)
        print("Working...")

async def main():
    # Start background task
    task = asyncio.create_task(background_work())

    # Do other work
    await asyncio.sleep(5)

    # Cancel background task
    task.cancel()

    try:
        await task
    except asyncio.CancelledError:
        print("Background task cancelled")
```

### Timeouts

```python
async def fetch_with_timeout(url: str, timeout: double) -> str:
    """Fetch with timeout."""
    try:
        return await asyncio.wait_for(
            fetch_data(url),
            timeout=timeout
        )
    except asyncio.TimeoutError:
        raise Exception(f"Request to {url} timed out")
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

Async context managers use `async __aenter__()` and `async __aexit__()` methods:

```python
class AsyncResource:
    """Async resource with async context manager support."""

    async def __aenter__(self):
        """Called when entering async with block."""
        print("Acquiring resource...")
        await asyncio.sleep(0.1)
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Called when exiting async with block."""
        print("Releasing resource...")
        await asyncio.sleep(0.1)

    async def process(self):
        """Do some async work."""
        await asyncio.sleep(0.5)
        print("Processing...")

# Usage
async def main():
    async with AsyncResource() as res:
        await res.process()
```

## Defer Statement **[v1.5+]**

The `defer` statement schedules a block of code to execute when the current scope exits, regardless of how it exits (normal return, exception, etc.). This is useful for cleanup operations.

### Basic Defer

```python
def process_file(path: str):
    file = open(path, "r")
    defer:
        file.close()

    # Process file...
    # file.close() will be called when function exits
    return process(file.read())
```

### Multiple Defer Statements

Multiple `defer` statements execute in **reverse order** (LIFO - Last In, First Out):

```python
def nested_resources():
    print("Opening A")
    defer:
        print("Closing A")

    print("Opening B")
    defer:
        print("Closing B")

    print("Opening C")
    defer:
        print("Closing C")

    print("Processing")

# Output:
# Opening A
# Opening B
# Opening C
# Processing
# Closing C
# Closing B
# Closing A
```

### Defer with Exception Handling

Defer blocks execute even when exceptions are raised:

```python
def risky_operation():
    resource = acquire()
    defer:
        print("Cleanup happens even if exception raised")
        resource.release()

    # If this raises an exception, defer still executes
    dangerous_work(resource)
```

### Defer Scope Rules

Defer statements are scoped to the enclosing function or block:

```python
def example():
    defer:
        print("Function exit")

    if condition:
        defer:
            print("If block exit")  # Executes when if block exits

        do_work()
    # "If block exit" printed here (end of if block)

    do_more()
# "Function exit" printed here (end of function)
```

### Defer vs Context Managers

| Feature | `defer` | `with` (Context Manager) |
|---------|---------|-------------------------|
| Use case | Ad-hoc cleanup | Structured resource management |
| Syntax | Simple block | Requires `__enter__`/`__exit__` |
| Order | LIFO (reverse) | LIFO (reverse) |
| Exception safe | Yes | Yes |
| Can capture variables | Yes | Limited (via `__enter__` return) |

**When to use `defer`:**
- Quick cleanup operations
- Working with resources that don't have context managers
- Multiple cleanup steps in one function

**When to use `with`:**
- Established resource management patterns (files, locks, connections)
- Reusable resource management logic
- Standard library integration

## Naming Conventions **[v0.5]**

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

## Program Entry Point **[v0.5]**

The entry point is either a file with top-level statements or a `main()` function:

```python
# Option 1: Top-level statements
print("Hello, World!")

# Option 2: main() function
def main():
    print("Hello, World!")
```

**Note**: The Python idiom `if __name__ == "__main__":` does not exist in Sharpy. The `__name__` variable is not available and attempting to use it causes a compilation error.

## .NET Interop **[v0.5]**

Sharpy provides seamless interop with .NET libraries and frameworks.

### Importing .NET Types

```python
# Import .NET namespaces
from system.collections.generic import List, Dictionary
from system.io import File, Path
from system.linq import Enumerable

# Use .NET types directly
list = List[int]()
list.add(1)
list.add(2)

dict = Dictionary[str, int]()
dict["key"] = 42
```

### Using .NET Methods

```python
from system.io import File, Path

# Call static .NET methods
content = File.read_all_text("data.txt")
full_path = Path.get_full_path("file.txt")
exists = File.exists("config.json")

# Use .NET instance methods
from system.text import StringBuilder

sb = StringBuilder()
sb.append("Hello")
sb.append(" ")
sb.append("World")
result = sb.to_string()  # "Hello World"
result = str(sb)  # Also valid via str() which delegates to `to_string()` underlyingly
```

### .NET Properties

.NET properties are accessed like Sharpy properties:

```python
from system.io import FileInfo

file = FileInfo("data.txt")

# Access properties
size = file.length
name = file.name
dir = file.directory
readonly = file.is_read_only

# Set properties
file.is_read_only = True
```

### Extension Methods

.NET extension methods work naturally in Sharpy:

```python
from system.linq import Enumerable

numbers = [1, 2, 3, 4, 5]

# LINQ extension methods
evens = numbers.where(lambda x: x % 2 == 0)
doubled = numbers.select(lambda x: x * 2)
sum = numbers.sum()
first = numbers.first()
```

### Literal Names for Exact Casing

Use backticks to preserve exact casing when calling .NET APIs:

```python
# Without backticks, case conversion applies
from system.io import File

# With backticks, exact casing preserved
from `System.IO` import File

# Useful for calling .NET methods with exact names
result = File.`ReadAllText`("data.txt")
```

### Handling .NET Exceptions

.NET exceptions can be caught using Sharpy's exception handling:

```python
from system import ArgumentException, InvalidOperationException
from system.io import IOException

try:
    File.read_all_text("missing.txt")
except IOException as e:
    print(f"IO error: {e.message}")
except ArgumentException as e:
    print(f"Argument error: {e.message}")
```

### IDisposable Pattern

.NET's `IDisposable` pattern integrates with Sharpy's `with` statement:

```python
from system.io import FileStream, FileMode

with FileStream("output.dat", FileMode.create) as stream:
    stream.write(data, 0, len(data))
    # stream.Dispose() called automatically
```

### Nullable Reference Types

.NET's nullable reference types map to Sharpy's nullable syntax:

```python
# .NET method that returns string?
name: str? = get_optional_name()

if name is not None:
    print(name.to_upper())  # Safe - narrowed to str
```

### Attributes (Annotations)

.NET attributes can be applied using decorator-like syntax:

```python
# Custom attribute
@Serializable
class DataModel:
    @JsonProperty("user_name")
    property name: str

    @JsonIgnore
    property internal_id: int
```

### Generic .NET Types

```python
from system.collections.generic import List, Dictionary, HashSet

# Generic type instantiation
int_list = List[int]()
string_dict = Dictionary[str, str]()
unique_values = HashSet[double]()

# Nested generics
matrix = List[List[int]]()
```

## See Also

- [Type System](type_system.md) - Detailed type semantics, interfaces, and generics
- [Compiler Design](compiler_design.md) - Implementation details and code generation
