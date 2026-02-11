# Sharpy Compiler Implementation Phases (v0.1.x Series)

This document outlines the phased implementation plan for the Sharpy compiler, from v0.1.0 through v0.1.15. **All 16 phases are now completed.** The v0.1.x series delivers a minimally viable language capable of:

- Basic control flow and expressions
- Classes, structs, interfaces, and enums
- Static and instance methods, variadic args (`*args` → `params`)
- Keyword arguments, lambdas, and function types
- Type aliases and generics
- Module system, multi-file compilation, and entry points
- .NET collections directly (List, Dictionary, HashSet) for bootstrapping
- 29 dunder methods with implicit interface synthesis
- Exception handling with custom Sharpy exception types
- .NET interop via CLR type discovery and namespace imports
- Sharpy.Core standard library with Pythonic collection wrappers

**Deferred to v0.2.x+:** Tagged unions/ADTs, events, context managers, async/await, generators, pattern matching, partial application, properties (beyond basic fields), wiring Sharpy.Core wrappers as default codegen target.

---

## Phase Overview

| Version | Theme | Key Deliverables |
|---------|-------|------------------|
| 0.1.0 | Lexer Foundation ✅ | Token types, keyword recognition, indentation (INDENT/DEDENT) |
| 0.1.1 | Parser Foundation ✅ | AST nodes, module structure, literals, identifiers |
| 0.1.2 | Code Generation Bootstrap ✅ | Roslyn emission, entry point, primitive types |
| 0.1.3 | Variables & Expressions ✅ | Variable declarations, assignment, arithmetic/comparison ops |
| 0.1.4 | Control Flow ✅ | if/elif/else, while, for (with range) |
| 0.1.5 | Functions ✅ | Function definitions, positional/default params, return, keyword args |
| 0.1.6 | Classes ✅ | Class definitions, fields, `__init__`, instance methods |
| 0.1.7 | Inheritance & Interfaces ✅ | Single inheritance, abstract classes, interfaces, decorators |
| 0.1.8 | Structs & Enums ✅ | Value types, enumerations |
| 0.1.9 | Type System Enhancements ✅ | Nullable types (`T?`), type aliases, basic generics |
| 0.1.10 | Module System ✅ | Imports, module resolution, multi-file compilation |
| 0.1.11 | Collections (.NET) ✅ | `list`/`dict`/`set` syntax → .NET generics, `print()`, `len()`, comprehensions |
| 0.1.12 | .NET Interop ✅ | Importing .NET types, calling .NET methods |
| 0.1.13 | Exception Handling ✅ | try/except/else/finally, raise, custom exception types |
| 0.1.14 | Lambdas & Delegates ✅ | Lambda expressions, function types, Action/Func |
| 0.1.15 | Essential Dunders ✅ | 29 dunders with implicit interface synthesis |

---

## Phase 0.1.0: Lexer Foundation ✅ COMPLETED

**Goal:** Tokenize Sharpy source code with Python-style indentation handling.

### Features

1. **Token Types**
   - Keywords: `def`, `class`, `struct`, `interface`, `enum`, `if`, `elif`, `else`, `while`, `for`, `in`, `break`, `continue`, `return`, `pass`, `True`, `False`, `None`, `and`, `or`, `not`, `is`, `import`, `from`, `as`, `raise`, `try`, `except`, `finally`, `lambda`, `type`, `const`, `assert`, `auto`, `case`, `event`, `match`, `maybe`, `property`, `to`, `with`, `yield`, `async`, `await`, `del`
   - Soft Keywords: `_`, `get`, `set`, `init`
   - Operators: `+`, `-`, `*`, `/`, `//`, `%`, `**`, `=`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `+=`, `-=`, `|>`, `??`, etc.
   - Delimiters: `(`, `)`, `[`, `]`, `{`, `}`, `:`, `,`, `.`, `?.`, `->`, `?`
   - Literals: integers, floats, strings (including f-strings basic), booleans
   - Identifiers: standard naming rules

2. **Indentation Handling**
   - Track indentation levels
   - Emit `INDENT` tokens on indentation increase
   - Emit `DEDENT` tokens on indentation decrease
   - Supports spaces (4-space required per tab) but no actual tabs (disallowed)

3. **Comments**
   - Single-line comments: `# comment`
   - Skip docstrings (triple-quoted strings) as comments initially

4. **Numeric Literals**
   - Integer: `42`, `0x2A`, `0b101010`, `0o52`
   - Float: `3.14`, `1e10`, `2.5e-3`
   - Type suffixes: `42L`, `3.14f`, `3.14d`, `100m`

5. **String Literals**
   - Single/double quotes: `'hello'`, `"world"`
   - Triple-quoted: `'''multiline'''`, `"""multiline"""`
   - Raw strings: `r"path\to\file"`
   - F-strings (basic): `f"Hello {name}"` (parse as tokens, defer interpolation to parser)

### Test Cases

```python
# Basic tokenization
x = 42
y = 3.14
name = "Alice"

# Indentation
def foo():
    if True:
        return 1
    return 0
```

### Exit Criteria

- All token types recognized correctly
- INDENT/DEDENT emitted at correct positions
- Numeric literals with suffixes parsed
- String literals (all variants) tokenized
- Comments stripped

---

## Phase 0.1.1: Parser Foundation ✅ COMPLETED

**Goal:** Parse Sharpy source into an Abstract Syntax Tree (AST).

### Features

1. **Module Structure**
   - Module as top-level container
   - Support for top-level declarations

2. **AST Node Types**
   - `Module`: root node containing statements
   - `ExpressionStatement`: standalone expressions
   - `Identifier`: variable/function names
   - `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `BooleanLiteral`, `NoneLiteral`
   - `BinaryExpression`: `a + b`, `a == b`, etc.
   - `UnaryExpression`: `-x`, `not x`
   - `PassStatement`: placeholder

3. **Expression Parsing (Precedence Climbing)**
   - Highest: Primary (literals, identifiers, parenthesized expressions, member access `.`, `?.`)
   - Unary: `-x`, `+x`, `~x`, `not x`
   - Power: `**` (right-associative)
   - Multiplicative: `*`, `/`, `//`, `%`
   - Additive: `+`, `-`
   - Bitwise shifts: `<<`, `>>`
   - Bitwise: `&`, `^`, `|`
   - Pipe: `|>` (left-to-right function application)
   - Type coercion: `to` (e.g., `animal to Dog`, `value to int?`)
   - Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, `is` (chained)
   - Logical: `not`, `and`, `or`
   - Null coalesce: `??`
   - Lowest: Conditional (`x if test else y`) — *deferred*

4. **Type Annotations (Parsing Only)**
   - Simple types: `int32` (or `int`), `str`, `bool`, `float64` (or `float`)
   - Nullable: `int32?`
   - Generic placeholder: `list[int32]` (parse structure, defer semantics)

### Test Cases

```python
# Expressions
42
-x
a + b * c
(a + b) * c
not True and False

# Comparison chaining (special parsing)
a < b < c  # Equivalent to (a < b) and (b < c), b evaluated once

# Pass statement
pass
```

### Exit Criteria

- AST correctly represents expression precedence
- Parentheses override precedence
- Type annotations parsed but not validated
- Module structure captured

---

## Phase 0.1.2: Code Generation Bootstrap ✅ COMPLETED

**Goal:** Generate executable C# code via Roslyn for minimal programs.

### Features

1. **Entry Point Generation**
   - `main()` function required in entry point files
   - Module-level declarations become static fields
   - Generated class structure for module

2. **Type Mapping (Primitives)**
   | Sharpy | C# |
   |--------|-----|
   | `int8` | `sbyte` |
   | `int16` | `short` |
   | `int` or `int32` | `int` |
   | `int64` | `long` |
   | `uint8` | `byte` |
   | `uint16` | `ushort` |
   | `uint32` | `uint` |
   | `uint64` | `ulong` |
   | `float32` | `float` |
   | `float` or `float64` | `double` |
   | `decimal` | `decimal` |
   | `bool` | `bool` |
   | `str` | `string` |
   | `object` | `object` |
   | `array[T]` | `T[]` |
   | `None` | `void` (return) / `null` (value) |

3. **Roslyn AST Generation**
   - `CompilationUnitSyntax` for file
   - `ClassDeclarationSyntax` for module wrapper
   - `MethodDeclarationSyntax` for `Main()`
   - `LiteralExpressionSyntax` for constants
   - `BinaryExpressionSyntax` for operators

4. **Compilation Pipeline**
   - Lex → Parse → Emit Roslyn AST → Compile to IL → Output DLL/EXE

### Test Cases

```python
# Minimal program (should compile and run)
pass
```

```python
# Expression evaluation (compile only, no output yet)
42 + 8
```

### Exit Criteria

- `pass` compiles to empty `Main()` and runs
- Primitive literals compile correctly
- Binary expressions generate correct C# operators
- Output is a valid .NET assembly

---

## Phase 0.1.3: Variables & Expressions ✅ COMPLETED

**Goal:** Variable declarations, assignments, and full expression support.

### Features

1. **Variable Declaration**
   ```python
   x: int = 42
   name: str = "Alice"
   flag: bool = True
   ```

2. **Type Inference (Optional Annotation)**
   ```python
   x = 42        # Inferred as int32
   y = 3.14      # Inferred as float64
   ```

3. **Explicit Type Inference (`auto`)**
   ```python
   x: auto = compute_value()  # Explicit inference, useful for shadowing
   ```

   **Note:** `auto` explicitly requests type inference. This is useful when re-declaring a variable with a potentially different type (variable shadowing).

4. **Assignment Operators**
   - Simple: `=`
   - Augmented: `+=`, `-=`, `*=`, `/=`, `//=`, `%=`

5. **Constant Declaration**
   ```python
   const PI: float = 3.14159
   const MAX_SIZE: int = 100
   ```

6. **Semantic Analysis (Phase 1)**
   - Symbol table for variable tracking
   - Type checking for assignments
   - Error on undefined variables

### Test Cases

```python
x: int = 10
y: int = 20
z = x + y
z += 5

const MAX: int = 100
# MAX = 50  # Should error: cannot reassign const
```

### Exit Criteria

- Variables declared and used correctly
- Type annotations validated
- Augmented assignment operators work
- Constants enforced as immutable
- Type mismatch errors reported

---

## Phase 0.1.4: Control Flow ✅ COMPLETED

**Goal:** Conditional and loop statements.

### Features

1. **If Statement**
   ```python
   if condition:
       statement
   elif other_condition:
       statement
   else:
       statement
   ```

2. **While Loop**
   ```python
   while condition:
       statement
   ```

3. **For Loop (with range)**
   ```python
   for i in range(10):
       statement

   for i in range(0, 10):
       statement

   for i in range(0, 10, 2):
       statement
   ```

4. **Break and Continue**
   ```python
   while True:
       if done:
           break
       if skip:
           continue
   ```

5. **Loop Else Clauses**
   ```python
   for item in items:
       if matches(item):
           break
   else:
       # Runs only if loop completed without break
       print("no match found")
   ```

   Loop else generates a boolean flag pattern: `_loopCompleted` is set to `true`, then `false` before any `break`. After the loop, `if (_loopCompleted)` guards the else body.

6. **Control Flow Validation**
   - `break`/`continue` only inside loops
   - Unreachable code detection (basic)

### Test Cases

```python
# Factorial
n: int = 5
result: int = 1
while n > 1:
    result *= n
    n -= 1

# FizzBuzz
for i in range(1, 101):
    if i % 15 == 0:
        pass  # print("FizzBuzz") - deferred
    elif i % 3 == 0:
        pass  # print("Fizz")
    elif i % 5 == 0:
        pass  # print("Buzz")
    else:
        pass  # print(i)
```

### Exit Criteria

- If/elif/else chains work correctly
- While loops execute until condition false
- For loops with range iterate correctly
- Break exits innermost loop
- Continue skips to next iteration
- Error on break/continue outside loop
- Loop else clauses work for both while and for loops

---

## Phase 0.1.5: Functions ✅ COMPLETED

**Goal:** Function definitions with parameters and return values.

### Features

1. **Function Definition**
   ```python
   def add(x: int, y: int) -> int:
       return x + y

   def greet(name: str) -> None:
       pass  # print deferred
   ```

2. **Default Parameters**
   ```python
   def greet(name: str, greeting: str = "Hello") -> str:
       return f"{greeting}, {name}!"
   ```

   **Note:** Default parameter values must be compile-time constants: numeric/string/boolean literals, `None` (for nullable types only), enum values, or `const` references. Mutable defaults like `[]` or `{}` are compile errors.

3. **Keyword Arguments**
   ```python
   result = greet(name="Alice", greeting="Hi")
   result = greet("Bob", greeting="Hey")
   ```

4. **Return Statement**
   ```python
   def factorial(n: int) -> int:
       if n <= 1:
           return 1
       return n * factorial(n - 1)
   ```

5. **Return Type Validation**
   - All paths must return compatible type
   - `-> None` functions may omit return

6. **Variadic Parameters**
   ```python
   def sum_all(*args: int) -> int:
       result: int = 0
       for x in args:
           result += x
       return result
   ```

   **Note:** Only one variadic parameter allowed, must be the last parameter, and cannot have a default value. Maps to C# `params T[]`.

7. **Function Overloading** (Optional for 0.1.5)
   - Same name, different signatures
   - Resolved at compile time

### Test Cases

```python
def add(a: int, b: int) -> int:
    return a + b

def multiply(a: int, b: int = 1) -> int:
    return a * b

x = add(2, 3)           # 5
y = multiply(4)          # 4
z = multiply(4, b=5)     # 20
```

### Exit Criteria

- Functions compile to C# methods
- Parameters type-checked at call sites
- Default values work correctly
- Keyword arguments resolve to correct parameters
- Return type validation enforced

---

## Phase 0.1.6: Classes ✅ COMPLETED

**Goal:** Basic class definitions with fields, constructors, and methods.

### Features

1. **Class Definition**
   ```python
   class Point:
       x: int
       y: int
   ```

2. **Constructor (`__init__`)**
   ```python
   class Point:
       x: int
       y: int

       def __init__(self, x: int, y: int):
           self.x = x
           self.y = y
   ```

3. **Instance Methods**
   ```python
   class Point:
       x: int
       y: int

       def __init__(self, x: int, y: int):
           self.x = x
           self.y = y

       def distance_from_origin(self) -> float:
           return (self.x ** 2 + self.y ** 2) ** 0.5
   ```

4. **Static Methods**
   ```python
   class Math:
       # Static method - no `self` parameter
       def square(x: int) -> int:
           return x * x
   ```

   **Note:** Static methods have no `self` parameter. The compiler detects this and emits the C# `static` keyword automatically.

5. **Field Access**
   ```python
   p = Point(3, 4)
   x_val = p.x
   p.y = 10
   ```

### Code Generation

```python
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
```

Generates:

```csharp
public class Point
{
    public int X;
    public int Y;

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}
```

### Test Cases

```python
class Counter:
    value: int

    def __init__(self, start: int = 0):
        self.value = start

    def increment(self) -> None:
        self.value += 1

    def get(self) -> int:
        return self.value

c = Counter(10)
c.increment()
result = c.get()  # 11
```

### Exit Criteria

- Classes compile to C# classes
- Fields declared and accessible
- `__init__` compiles to constructor
- Instance methods have correct `this` binding
- Static methods work without instance
- Name mangling: `snake_case` → `PascalCase`

---

## Phase 0.1.7: Inheritance & Interfaces ✅ COMPLETED

**Goal:** Class inheritance, abstract classes, and interfaces.

### Features

1. **Single Inheritance**
   ```python
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
   ```

   **`super()` Restrictions:** `super()` can only be called in:
   - `__init__` to call `super().__init__(...)`
   - Dunder methods to call `super().__dunder__(...)`
   - `@override` methods to call `super().method()`

   Calling `super()` in regular methods or free functions is a compile error.

2. **Abstract Classes**
   ```python
   @abstract
   class Shape:
       @abstract
       def area(self) -> float:
           ...

   class Circle(Shape):
       radius: float

       def __init__(self, radius: float):
           self.radius = radius

       @override
       def area(self) -> float:
           return 3.14159 * self.radius ** 2
   ```

3. **Interfaces**
   ```python
   interface IDrawable:
       def draw(self) -> None:
           ...

   class Circle(IDrawable):
       def draw(self) -> None:
           pass  # drawing logic
   ```

   **Note:** Interface method implementations do NOT use `@override`. The `@override` decorator is only for overriding virtual/abstract methods from base classes.

4. **Multiple Interface Implementation**
   ```python
   interface ISerializable:
       def serialize(self) -> str:
           ...

   class Data(IDrawable, ISerializable):
       def draw(self) -> None:
           pass

       def serialize(self) -> str:
           return "{}"
   ```

5. **Decorators**
   - `@virtual`: Method can be overridden
   - `@override`: Method overrides base class virtual/abstract method
   - `@abstract`: Method must be overridden (class must also be `@abstract`)
   - `@final`: Cannot be overridden (on methods) or inherited (on classes)

   **Note:** There is no `@static` decorator. Static methods are identified by the absence of a `self` parameter. There is no `@public` decorator; public is the default visibility.

6. **Access Modifiers**
   - (default): public - no decorator needed
   - `@private` or `__name`: Declaring class only
   - `@protected` or `_name`: Class and derived
   - `@internal`: Same assembly

### Test Cases

```python
interface IComparable:
    def compare_to(self, other: object) -> int:
        ...

@abstract
class Number(IComparable):
    value: int

    def __init__(self, value: int):
        self.value = value

    @abstract
    def double(self) -> int:
        ...

class Integer(Number):
    @override
    def double(self) -> int:
        return self.value * 2

    # Interface implementation - no @override needed
    def compare_to(self, other: object) -> int:
        if not isinstance(other, Integer):
            return -1
        return self.value - other.value
```

### Exit Criteria

- Single inheritance works
- `super()` calls parent constructor/methods (only in allowed contexts)
- `super()` errors in regular methods and free functions
- Abstract classes cannot be instantiated
- Abstract methods must be overridden
- Interfaces define contracts
- Interface implementations don't require `@override`
- Multiple interfaces supported
- Decorator modifiers apply correctly

---

## Phase 0.1.8: Structs & Enums ✅ COMPLETED

**Goal:** Value types (structs) and enumerations.

### Features

1. **Struct Definition**
   ```python
   struct Point:
       x: int
       y: int

       def __init__(self, x: int, y: int):
           self.x = x
           self.y = y
   ```

2. **Value Semantics**
   - Structs are copied on assignment
   - Passed by value (unless `ref`)
   - Cannot be `None` (use `Point?` for nullable)

3. **Enum Definition**
   ```python
   enum Color:
       RED = 1
       GREEN = 2
       BLUE = 3

   enum Status:
       PENDING = 0
       ACTIVE = 1
       COMPLETED = 2
   ```

   **Note:** All enum cases must have explicit constant values (no auto-numbering). Values must all be the same type (integer or `str`).

4. **Enum Usage**
   ```python
   enum Direction:
       NORTH = 0
       EAST = 1
       SOUTH = 2
       WEST = 3

   # Usage
   d = Direction.NORTH
   if d == Direction.NORTH:
       pass
   ```

### Code Generation

```python
struct Vector2:
    x: float
    y: float
```

Generates:

```csharp
public struct Vector2
{
    public float X;
    public float Y;
}
```

### Test Cases

```python
struct Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

enum Status:
    PENDING = 0
    ACTIVE = 1
    DONE = 2

p1 = Point(1, 2)
p2 = p1           # Copy, not reference
p2.x = 10         # p1.x still 1

status = Status.ACTIVE
```

### Exit Criteria

- Structs compile to C# `struct`
- Value semantics enforced (copy on assign)
- Enums compile to C# `enum`
- Enum values accessible via dot notation
- Enum comparison works

---

## Phase 0.1.9: Type System Enhancements ✅ COMPLETED

**Goal:** Nullable types, type aliases, and basic generics.

### Features

1. **Nullable Types**
   ```python
   name: str? = None
   count: int? = 42

   if name is not None:
       # name is str here (type narrowing)
       pass
   ```

2. **Null Coalescing**
   ```python
   value = name ?? "default"
   ```

3. **Null Conditional Access**
   ```python
   length = name?.upper()  # None if name is None
   ```

4. **Type Aliases**
   ```python
   type UserId = int
   type Point2D = tuple[float, float]
   type Handler = (int) -> None

   user: UserId = 42
   ```

5. **Basic Generics**
   - Parse generic syntax: `list[T]`, `dict[K, V]`
   - Instantiate with concrete types
   - User-defined generic classes and functions:
   ```python
   class Box[T]:
       value: T

       def __init__(self, value: T):
           self.value = value

       def get(self) -> T:
           return self.value

   def identity[T](value: T) -> T:
       return value
   ```

6. **Type Constraints** (Basic)
   ```python
   def find_max[T: IComparable[T]](items: list[T]) -> T:
       ...
   ```

### Test Cases

```python
type StringList = list[str]

def find_first(items: list[str], predicate: (str) -> bool) -> str?:
    for item in items:
        if predicate(item):
            return item
    return None

result = find_first(["a", "bb", "ccc"], lambda s: len(s) > 2)
value = result ?? "not found"
```

### Exit Criteria

- `T?` compiles to `Nullable<T>` or reference `T?`
- Null coalescing `??` works
- Null conditional `?.` works
- Type aliases expand correctly
- Generic collection types instantiate
- User-defined generic classes/functions compile
- Type constraints validated

---

## Phase 0.1.10: Module System ✅ COMPLETED

**Goal:** Import statements and multi-file compilation.

### Features

1. **Import Statement**
   ```python
   import math
   import collections.utils
   import mymodule as mm
   ```

2. **From Import**
   ```python
   from math import sqrt, pow
   from collections import list, dict
   from mymodule import MyClass as MC
   ```

3. **Wildcard Import**
   ```python
   from mymodule import *
   ```

4. **Module Resolution**
   - Search current directory
   - Search project directories
   - Search standard library paths

5. **Multi-File Compilation**
   - Compile multiple `.spy` files via `.spyproj` project files
   - Resolve cross-module references via shared `SymbolTable`
   - Handle circular imports (detected and reported with chain path)
   - Incremental compilation with transitive dependency tracking

6. **Package Structure**
   ```
   project/
       main.spy
       utils/
           __init__.spy
           helpers.spy
   ```

   `__init__.spy` supports re-exports (e.g., `from .helpers import greet` re-exports `greet` at the package level).

### Test Cases

```python
# utils/math_helpers.spy
def square(x: int) -> int:
    return x * x

# main.spy
from utils.math_helpers import square

result = square(5)
```

### Exit Criteria

- ✅ `import` loads module symbols
- ✅ `from ... import` selectively imports
- ✅ `from ... import *` wildcard imports
- ✅ Module aliases work
- ✅ Multi-file projects compile (with `.spyproj`)
- ✅ Circular imports detected and reported

---

## Phase 0.1.11: Collections (.NET) ✅ COMPLETED

**Goal:** Collection syntax using .NET types directly for bootstrapping.

**Current state:** Code generation maps `list`, `dict`, `set` to .NET `List<T>`, `Dictionary<K,V>`, `HashSet<T>` directly (via `CodeGen/TypeMapper.cs`). Sharpy.Core wrapper classes (`Sharpy.List<T>`, `Sharpy.Dict<K,V>`, `Sharpy.Set<T>`) exist in the standard library with full Pythonic APIs and implement `ISized`, `IEquatable<T>`, and standard .NET collection interfaces. Wiring Sharpy.Core wrappers as the default codegen target is deferred to v0.2.x+.

### Features

1. **List** → `System.Collections.Generic.List<T>`
   ```python
   numbers: list[int] = [1, 2, 3]
   numbers.add(4)          # .NET: Add()
   first = numbers[0]
   length = len(numbers)   # Via ISized protocol or ICollection.Count

   # List comprehension
   squares = [x * x for x in range(10)]
   ```

2. **Dictionary** → `System.Collections.Generic.Dictionary<K,V>`
   ```python
   ages: dict[str, int] = {"Alice": 30, "Bob": 25}
   ages["Charlie"] = 35

   if ages.contains_key("Alice"):
       age = ages["Alice"]
   ```

3. **Set** → `System.Collections.Generic.HashSet<T>`
   ```python
   unique: set[int] = {1, 2, 3, 2, 1}  # {1, 2, 3}
   unique.add(4)  # .NET: Add()
   ```

4. **All Comprehension Types**
   ```python
   squares = [x * x for x in range(10)]              # list comprehension
   even_set = {x for x in range(10) if x % 2 == 0}   # set comprehension
   mapping = {k: v for k, v in pairs}                 # dict comprehension
   ```

5. **Built-in Functions**
   - `print(*values)`: Output to console (variadic via `params object?[]`)
   - `len(collection)`: Collection length (dispatches via `ISized` or `ICollection`)
   - `range(stop)`, `range(start, stop)`, `range(start, stop, step)`
   - `str(value)`, `int(value)`, `float(value)`, `double(value)`: Type conversion
   - `list(iterable)`, `set(iterable)`, `tuple(iterable)`, `frozenset(iterable)`: Collection conversion
   - `isinstance(obj, Type)`: Type checking (single type only, no tuples like Python)
   - `type(obj)`: Get type (limited)
   - `bool(value)`: Boolean conversion (dispatches via `IBoolConvertible`)
   - `hash(value)`: Hash computation
   - `repr(obj)`: String representation
   - `format(value, spec)`: String formatting
   - `input()`, `input(prompt)`: Read from stdin
   - `pow(base, exp)`, `pow(base, exp, mod)`: Exponentiation
   - `divmod(a, b)`: Division with remainder
   - `round(x)`, `round(x, n)`: Rounding
   - `min(iterable)`, `max(iterable)`: Min/max
   - `sum(iterable)`: Summation
   - `all(iterable)`, `any(iterable)`: Logical aggregation
   - `sorted(iterable)`, `reversed(iterable)`: Ordering
   - `enumerate(iterable)`, `zip(iter1, iter2)`: Iteration helpers
   - `map(func, iterable)`, `filter(func, iterable)`: Functional transforms
   - `iter(iterable)`, `next(iterator)`: Iterator protocol
   - `issubclass(cls, Type)`: Type hierarchy check

   **Note:** `isinstance(x, (int, str))` is NOT supported. Use `isinstance(x, int) or isinstance(x, str)` instead. Also, `isinstance(x, list[int])` is a compile error due to type erasure. All builtins are discovered via reflection from `Sharpy.Core.Builtins`.

6. **String Operations**
   ```python
   s = "hello"
   upper = s.upper()
   contains = "ell" in s
   formatted = f"Value: {x}"
   ```

### Sharpy.Core Wrapper Types (stdlib, not yet default codegen target)

The standard library provides wrapper types with Pythonic APIs:

- **`Sharpy.List<T>`**: `Append()`, `Pop()`, `Remove()`, `Insert()`, `Extend()`, `Sort()`, `Reverse()`, `Index()`, `Count()`, `Copy()`, `Contains()`
- **`Sharpy.Dict<K,V>`**: `Get()`, `Pop()`, `PopItem()`, `Keys()`, `Values()`, `Items()`, `SetDefault()`, `Update()`, `Merge()`, `Copy()`, `ContainsKey()`
- **`Sharpy.Set<T>`**: `Add()`, `Discard()`, `Remove()`, `Pop()`, `Union()`, `Intersection()`, `Difference()`, `SymmetricDifference()`, `IsSubset()`, `IsSuperset()`, `IsDisjoint()`, `Copy()`

All implement `ISized`, `IEquatable<T>`, and standard .NET collection interfaces (`IList<T>`, `IDictionary<K,V>`, `ISet<T>`, etc.).

### Code Generation

```python
items: list[int] = [1, 2, 3]
```

Generates:

```csharp
List<int> items = new List<int> { 1, 2, 3 };
// With implicit: using System.Collections.Generic;
```

### Exit Criteria

- ✅ `list`, `dict`, `set` literals compile to .NET generic types
- ✅ .NET collection methods accessible (with name mangling)
- ✅ `print()` outputs to console (variadic)
- ✅ `len()` works on collections (via `ISized` protocol and `ICollection.Count`)
- ✅ F-string interpolation works
- ✅ List, dict, and set comprehensions work (including filtered)

---

## Phase 0.1.12: .NET Interop ✅ COMPLETED

**Goal:** Import and use .NET types and methods.

### Features

1. **Importing .NET Namespaces**
   ```python
   from system import Console, DateTime, IComparable
   from system.collections.generic import Dictionary
   from system.io import File, Path
   ```

   Mapped namespaces (via `ModuleRegistry.MapModuleToNamespace()`): `system` → `System`, `system.io` → `System.IO`, `system.text` → `System.Text`, `system.linq` → `System.Linq`, `system.threading` → `System.Threading`, `system.threading.tasks` → `System.Threading.Tasks`, `system.net` → `System.Net`, `system.net.http` → `System.Net.Http`, `system.collections` → `System.Collections`, `system.collections.generic` → `System.Collections.Generic`.

2. **Using .NET Types**
   ```python
   from system import DateTime

   now = DateTime.now
   year = now.year
   formatted = now.to_string("yyyy-MM-dd")
   ```

3. **Calling .NET Static Methods**
   ```python
   from system.io import File

   content = File.read_all_text("file.txt")
   File.write_all_text("output.txt", content)
   ```

4. **Name Mangling (Forward Only)**
   - Sharpy `snake_case` → C# `PascalCase` (via `NameMangler`)
   - No reverse mapping (CLR types accessed via their PascalCase names after mangling)
   - Backtick escapes: `` `PascalCase` `` for exact names

5. **Generic .NET Types**
   ```python
   from system.collections.generic import List

   names: List[str] = List[str]()
   names.add("Alice")
   ```

6. **CLR Type Discovery**
   - `CachedModuleDiscovery` reflects loaded assemblies for type/method information
   - `Discovery/TypeMapper.cs` maps CLR types back to Sharpy `SemanticType` instances
   - `BuiltinRegistry` fallback searches well-known namespaces (`System`, `System.Collections.Generic`, `System.IO`, `System.Text`)
   - On-disk cache (`OverloadIndexCache`) with SHA-256 invalidation for incremental builds

### Test Cases

```python
from system import Console, DateTime, Math

Console.write_line("Hello from Sharpy!")

now = DateTime.now
Console.write_line(f"Current time: {now}")

sqrt_val = Math.sqrt(16.0)
Console.write_line(f"sqrt(16) = {sqrt_val}")
```

### Exit Criteria

- ✅ .NET namespaces importable (10 mapped namespace prefixes)
- ✅ Static methods callable
- ✅ Instance methods callable
- ✅ Generic .NET types work
- ✅ Name mangling transparent (forward direction; Sharpy → C#)

---

## Phase 0.1.13: Exception Handling ✅ COMPLETED

**Goal:** Try/except/finally and raise statements.

### Features

1. **Try/Except**
   ```python
   try:
       result = risky_operation()
   except ValueError as e:
       handle_error(e)
   except Exception:
       handle_generic()
   ```

2. **Try/Finally**
   ```python
   try:
       resource = acquire()
       use(resource)
   finally:
       release(resource)
   ```

3. **Try/Except/Finally**
   ```python
   try:
       data = load()
   except IOError as e:
       log_error(e)
       data = default_data()
   finally:
       cleanup()
   ```

4. **Try/Except/Else/Finally**
   ```python
   try:
       result = operation()
   except ValueError as e:
       handle_error(e)
   else:
       print(f"Success: {result}")  # Only runs if no exception
   finally:
       cleanup()
   ```

   The `else` clause uses a flag-based code generation pattern: a `bool __trySucceeded` flag is set to `true` at the end of the try block, and the else body runs in an `if (__trySucceeded)` block after the try/catch/finally.

5. **Raise Statement**
   ```python
   def validate(x: int) -> None:
       if x < 0:
           raise ValueError("x must be non-negative")
   ```

   Bare `raise` (re-throw) is supported inside `except` blocks only. Using bare `raise` outside an except block is a compile error (`DiagnosticCodes.Semantic.InvalidRaise`).

6. **Exception Types**
   Custom Sharpy exception classes defined in `Sharpy.Core` (all inherit from `System.Exception`):
   - `ValueError` → `Sharpy.ValueError`
   - `TypeError` → `Sharpy.TypeError`
   - `RuntimeError` → `Sharpy.RuntimeError`
   - `NotImplementedError` → `Sharpy.NotImplementedError`
   - `AttributeError` → `Sharpy.AttributeError`
   - `ZeroDivisionError` → `Sharpy.ZeroDivisionError`
   - `OverflowError` → `Sharpy.OverflowError`
   - `IndexError` → `Sharpy.IndexError`
   - `KeyError` → `Sharpy.KeyError`
   - `StopIteration` → `Sharpy.StopIteration` (used by iterator protocol)
   - `UnicodeEncodeError` → `Sharpy.UnicodeEncodeError`
   - `Exception` → `System.Exception` (base)

### Test Cases

```python
def safe_divide(a: int, b: int) -> int:
    if b == 0:
        raise ValueError("Cannot divide by zero")
    return a // b

try:
    result = safe_divide(10, 0)
except ValueError as e:
    print(f"Error: {e}")
finally:
    print("Done")
```

### Exit Criteria

- ✅ Try/except catches exceptions
- ✅ Exception binding (`as e`) works
- ✅ Multiple except clauses checked in order
- ✅ Else clause runs only when no exception raised (flag-based pattern)
- ✅ Finally always executes
- ✅ Raise creates and throws exceptions
- ✅ Bare raise re-throws inside except blocks
- ✅ Type narrowing works in except blocks

---

## Phase 0.1.14: Lambdas & Delegates ✅ COMPLETED

**Goal:** Lambda expressions and function type handling.

### Features

1. **Lambda Expressions**
   ```python
   square = lambda x: x * x
   add = lambda a, b: a + b

   # With type annotation
   transform: (int) -> int = lambda x: x * 2
   ```

   **Note:** Lambda parameters have no type annotations; types are inferred bidirectionally from context (expected `FunctionType`). Lambdas in ambiguous contexts (e.g., `g = lambda x, y: x + y` without type annotation) are compile errors. Lambda scope is isolated from enclosing type narrowing.

2. **Function Types**
   ```python
   def apply(func: (int) -> int, value: int) -> int:
       return func(value)

   result = apply(lambda x: x * 2, 5)  # 10
   ```

   **Note:** Function types returning void must explicitly include `-> None` (e.g., `(int) -> None`), unlike function definitions where `-> None` can be omitted.

3. **Higher-Order Functions**
   ```python
   def map_list(items: list[int], f: (int) -> int) -> list[int]:
       result: list[int] = []
       for item in items:
           result.add(f(item))  # .NET: Add()
       return result

   doubled = map_list([1, 2, 3], lambda x: x * 2)
   ```

4. **C# Delegate Mapping**
   | Sharpy | C# |
   |--------|-----|
   | `() -> None` | `Action` |
   | `(T) -> None` | `Action<T>` |
   | `() -> R` | `Func<R>` |
   | `(T) -> R` | `Func<T, R>` |
   | `(T1, T2) -> R` | `Func<T1, T2, R>` |

5. **Method References**
   ```python
   def double(x: int) -> int:
       return x * 2

   f: (int) -> int = double
   ```

### Test Cases

```python
numbers = [1, 2, 3, 4, 5]

# Filter with lambda
evens = [n for n in numbers if n % 2 == 0]

# Map with function type
def transform_all(items: list[int], f: (int) -> int) -> list[int]:
    return [f(x) for x in items]

squared = transform_all(numbers, lambda x: x * x)
print(squared)  # [1, 4, 9, 16, 25]
```

### Exit Criteria

- ✅ Lambda expressions compile (single-param → `SimpleLambdaExpression`, multi-param → `ParenthesizedLambdaExpression`)
- ✅ Function type annotations work (with variance: contravariant params, covariant return)
- ✅ Lambdas passed as arguments with bidirectional type inference
- ✅ Action/Func delegates generated correctly
- ✅ Method references work

---

## Phase 0.1.15: Essential Dunders ✅ COMPLETED

**Goal:** Core dunder methods for operators and protocols (29 total).

**Critical Rule:** Dunder methods cannot be invoked directly by user code. `x.__eq__(y)` is a compile error. Dunders are only callable:
- Within another dunder method: `self.__other_dunder__()`
- Via super in a dunder: `super().__dunder__()`

### Features

1. **Object Protocol**
   ```python
   class Point:
       x: int
       y: int

       def __init__(self, x: int, y: int):
           self.x = x
           self.y = y

       @override
       def __str__(self) -> str:
           return f"Point({self.x}, {self.y})"

       @override
       def __eq__(self, other: object) -> bool:
           if not isinstance(other, Point):
               return False
           return self.x == other.x and self.y == other.y

       @override
       def __hash__(self) -> int:
           return hash((self.x, self.y))
   ```

2. **Arithmetic Operators**
   ```python
   class Vector:
       x: float
       y: float

       def __add__(self, other: Vector) -> Vector:
           return Vector(self.x + other.x, self.y + other.y)

       def __sub__(self, other: Vector) -> Vector:
           return Vector(self.x - other.x, self.y - other.y)

       def __neg__(self) -> Vector:
           return Vector(-self.x, -self.y)
   ```

3. **Container Protocol**
   ```python
   class Container:
       items: list[int]

       def __len__(self) -> int:
           return len(self.items)

       def __getitem__(self, index: int) -> int:
           return self.items[index]

       def __setitem__(self, index: int, value: int) -> None:
           self.items[index] = value

       def __contains__(self, item: int) -> bool:
           return item in self.items
   ```

4. **Iterator Protocol**
   ```python
   class Range:
       current: int
       stop: int

       def __iter__(self) -> Range:
           return self

       def __next__(self) -> int:
           if self.current >= self.stop:
               raise StopIteration()
           val = self.current
           self.current += 1
           return val
   ```

5. **Comparison Operators**
   ```python
   class Number:
       value: int

       def __lt__(self, other: Number) -> bool:
           return self.value < other.value

       def __le__(self, other: Number) -> bool:
           return self.value <= other.value

       # Compiler synthesizes __gt__, __ge__ from __lt__, __le__
   ```

6. **Boolean Conversion**
   ```python
   class Container:
       items: list[int]

       def __bool__(self) -> bool:
           return len(self.items) > 0
   ```

7. **Dunder → C# Mapping (29 dunders)**
   | Dunder | C# Generation | Synthesized Interface |
   |--------|---------------|----------------------|
   | `__init__` | Constructor | — |
   | `__str__` | `ToString()` override | — |
   | `__eq__` | `Equals()` + `operator ==`/`!=` + `IEquatable<T>` | — |
   | `__ne__` | `operator !=` (explicit, otherwise synthesized from `__eq__`) | — |
   | `__hash__` | `GetHashCode()` override | — |
   | `__add__` | `operator +` | — |
   | `__sub__` | `operator -` | — |
   | `__mul__` | `operator *` | — |
   | `__div__` | `operator /` | — |
   | `__mod__` | `operator %` | — |
   | `__neg__` | `operator -` (unary) | — |
   | `__pos__` | `operator +` (unary) | — |
   | `__invert__` | `operator ~` | — |
   | `__lt__`/`__le__`/`__gt__`/`__ge__` | `operator <`/`<=`/`>`/`>=` | — |
   | `__len__` | `Count` property | **`ISized`** |
   | `__getitem__` | Indexer `this[...]` get | — |
   | `__setitem__` | Indexer `this[...]` set | — |
   | `__contains__` | `Contains()` method | — |
   | `__bool__` | `operator true`/`operator false` | **`IBoolConvertible`** |
   | `__iter__` | `GetEnumerator()` → `IEnumerable<T>` | — |
   | `__next__` | `MoveNext()`/`Current` → `IEnumerator<T>` | — |
   | `__and__`/`__or__`/`__xor__` | `operator &`/`\|`/`^` | — |
   | `__lshift__`/`__rshift__` | `operator <<`/`>>` | — |

   **Interface synthesis notes:**
   - **Bold** = Sharpy.Core interface exists and is synthesized by the emitter (`ISized`, `IBoolConvertible`)
   - `__iter__`/`__next__` synthesize .NET `IEnumerable<T>`/`IEnumerator<T>` directly
   - `__eq__` synthesizes .NET `IEquatable<T>` when the parameter type is not `object`
   - Implicit interface synthesis emits SPY1001 info diagnostic when adding an interface to a type's base list
   - Other dunders (`__getitem__`, `__setitem__`, `__contains__`, `__str__`, `__hash__`) generate correct C# code without Sharpy protocol interfaces — .NET equivalents suffice

8. **Unsupported Dunders**
   - `__pow__` — `**` not overloadable in C#
   - `__floordiv__` — `//` not overloadable in C#
   - `__repr__` — use `__str__` instead
   - `__call__` — no callable object protocol in C#

### Test Cases

```python
class Fraction:
    num: int
    den: int

    def __init__(self, num: int, den: int):
        self.num = num
        self.den = den

    def __add__(self, other: Fraction) -> Fraction:
        return Fraction(
            self.num * other.den + other.num * self.den,
            self.den * other.den
        )

    @override
    def __str__(self) -> str:
        return f"{self.num}/{self.den}"

    @override
    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Fraction):
            return False
        return self.num * other.den == other.num * self.den

a = Fraction(1, 2)
b = Fraction(1, 3)
c = a + b
print(c)        # 5/6
print(a == b)   # False
```

### Exit Criteria

- ✅ `__str__` generates `ToString()`
- ✅ `__eq__`/`__hash__` generate proper overrides (+ `IEquatable<T>` synthesis)
- ✅ Arithmetic dunders generate operators (5 binary + 3 unary)
- ✅ Bitwise dunders generate operators (5 total)
- ✅ Comparison dunders generate operators (6 total)
- ✅ `__len__`/`__getitem__`/`__setitem__` work (`ISized` synthesized)
- ✅ `__contains__` works
- ✅ `__bool__` generates `operator true`/`operator false` (with `IBoolConvertible` synthesis)
- ✅ `__iter__`/`__next__` work (synthesizes .NET `IEnumerable<T>`/`IEnumerator<T>`)
- ✅ Operator synthesis (e.g., `!=` from `==`)
- ✅ `in` operator uses `__contains__`
- ✅ Null-safe equality dispatch for comparison operators

---

## Summary: What's Included vs Deferred

### Included in v0.1.x (all phases completed)

| Category | Features |
|----------|----------|
| **Types** | Primitives, classes, structs, interfaces, enums, nullable (`T?`), optional (`T?` via `maybe`), result (`T !E` via `try`), type aliases, generics |
| **Functions** | Definitions, default params, keyword args, variadic (`*args` → `params`), lambdas, return, function types |
| **OOP** | Single inheritance, interfaces, abstract classes, `@virtual`/`@override`/`@abstract`/`@final` |
| **Control Flow** | if/elif/else, while, for, break, continue, loop else (for/while else clauses) |
| **Collections** | `list`, `dict`, `set` syntax → .NET `List<T>`, `Dictionary<K,V>`, `HashSet<T>`; list/dict/set comprehensions (including filtered) |
| **Dunders** | `__init__`, `__str__`, `__eq__`/`__ne__`/`__hash__`, `__bool__`, `__len__`, `__getitem__`/`__setitem__`, `__contains__`, `__iter__`/`__next__`, arithmetic/bitwise/comparison/unary operators (29 total) |
| **Exceptions** | try/except/else/finally, raise, bare re-raise, custom Sharpy exception types |
| **Modules** | import, from import, from import *, multi-file via `.spyproj`, `__init__.spy` packages, incremental compilation |
| **Interop** | .NET namespace imports (10 mapped prefixes), CLR type discovery, assembly loading |
| **Stdlib** | Sharpy.Core wrapper collections (List, Dict, Set) with Pythonic APIs, protocol interfaces (`ISized`, `IBoolConvertible`), custom exceptions (11 types), 35+ builtins (print, len, range, str, int, float, double, bool, hash, repr, format, input, pow, divmod, round, min, max, sum, all, any, sorted, reversed, enumerate, zip, map, filter, iter, next, isinstance, type, issubclass, list, set, tuple, frozenset) |
| **Synthesis** | Implicit interface synthesis from dunders (SPY1001), `IEquatable<T>` from `__eq__`, `IEnumerable<T>`/`IEnumerator<T>` from `__iter__`/`__next__`, interface conflict detection |

### Deferred to v0.2.x+

| Category | Features |
|----------|----------|
| **Types** | Tagged unions (ADTs) |
| **Functions** | Partial application |
| **OOP** | Properties (full), events, delegates (custom definition) |
| **Control Flow** | Pattern matching (`match`), context managers (`with`) |
| **Collections** | Wiring Sharpy.Core wrappers as default codegen target (currently codegen emits .NET types directly) |
| **Dunders** | `__enter__`/`__exit__`, `__call__` |
| **Protocol Interfaces** | **Dropped** (not deferred). `IContainer`, `ISequence`, `IMutableSequence`, `IStrConvertible`, `IHashable`, `IIterable` — all unnecessary. .NET already provides equivalent functionality (`IList<T>`, `ICollection<T>`, `ToString()`, `GetHashCode()`, `IEnumerable<T>`). Names remain registered in `ProtocolRegistry` for dunder→C# mapping but no Sharpy.Core interfaces will be created. |
| **Async** | async/await, generators, yield |
| **Advanced** | Conversion operators, extension methods definition |

---

## Implementation Order Rationale

The phases were ordered to maximize **incremental testability**:

1. **0.1.0-0.1.2**: Foundation layers must come first (no shortcuts)
2. **0.1.3-0.1.4**: Variables and control flow enable meaningful programs
3. **0.1.5**: Functions unlock code organization
4. **0.1.6-0.1.7**: Classes and inheritance for OOP
5. **0.1.8**: Structs/enums are simpler than classes but depend on type system
6. **0.1.9**: Type enhancements needed before advanced features
7. **0.1.10**: Module system for real projects
8. **0.1.11**: .NET collections for bootstrapping (Sharpy.Core wrappers exist, not yet default codegen target)
9. **0.1.12**: .NET interop for ecosystem access
10. **0.1.13**: Exception handling for robustness
11. **0.1.14**: Lambdas enable functional patterns
12. **0.1.15**: Dunders for Pythonic feel and operator customization

All phases are now complete. The compiler can compile and run programs using all v0.1.x features. Note that some features originally scoped for v0.2.x were implemented during v0.1.x development: variadic args, loop else clauses, and Optional/Result types with `try`/`maybe` expressions. The next milestone is v0.2.x which will focus on tagged unions, pattern matching, async/await, context managers, and wiring Sharpy.Core wrappers as the default codegen target for collections.
