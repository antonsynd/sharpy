# Sharpy Compiler Implementation Phases (v0.1.x Series)

This document outlines the phased implementation plan for the Sharpy compiler, from v0.1.0 through v0.1.15+. Each phase builds incrementally on the previous, targeting a **minimally viable language** capable of:

- Basic control flow and expressions
- Classes, structs, interfaces, and enums
- Static and instance methods (no variadic/params args)
- Keyword arguments
- Type aliases
- Module system and entry points
- Minimal standard library (collections, I/O, .NET interop)
- Essential dunder methods only

**Deferred to v0.2.x+:** Tagged unions/ADTs, events, context managers, async/await, generators, variadic args, pattern matching, partial application, properties (beyond basic fields).

---

## Phase Overview

| Version | Theme | Key Deliverables |
|---------|-------|------------------|
| 0.1.0 | Lexer Foundation | Token types, keyword recognition, indentation (INDENT/DEDENT) |
| 0.1.1 | Parser Foundation | AST nodes, module structure, literals, identifiers |
| 0.1.2 | Code Generation Bootstrap | Roslyn emission, entry point, primitive types |
| 0.1.3 | Variables & Expressions | Variable declarations, assignment, arithmetic/comparison ops |
| 0.1.4 | Control Flow | if/elif/else, while, for (with range) |
| 0.1.5 | Functions | Function definitions, positional/default params, return, keyword args |
| 0.1.6 | Classes | Class definitions, fields, `__init__`, instance methods |
| 0.1.7 | Inheritance & Interfaces | Single inheritance, abstract classes, interfaces, decorators |
| 0.1.8 | Structs & Enums | Value types, enumerations |
| 0.1.9 | Type System Enhancements | Nullable types (`T?`), type aliases, basic generics |
| 0.1.10 | Module System | Imports, module resolution, multi-file compilation |
| 0.1.11 | Collections & Stdlib | `list`, `dict`, `set`, basic operations, `print()` |
| 0.1.12 | .NET Interop | Importing .NET types, calling .NET methods |
| 0.1.13 | Exception Handling | try/except/finally, raise |
| 0.1.14 | Lambdas & Delegates | Lambda expressions, function types, Action/Func |
| 0.1.15 | Essential Dunders | `__str__`, `__eq__`, `__hash__`, `__len__`, `__getitem__`, operators |

---

## Phase 0.1.0: Lexer Foundation

**Goal:** Tokenize Sharpy source code with Python-style indentation handling.

### Features

1. **Token Types**
   - Keywords: `def`, `class`, `struct`, `interface`, `enum`, `if`, `elif`, `else`, `while`, `for`, `in`, `return`, `pass`, `True`, `False`, `None`, `and`, `or`, `not`, `is`, `import`, `from`, `as`, `raise`, `try`, `except`, `finally`, `lambda`, `type`, `const`, `assert`, `auto`, `case`, `event`, `match`, `maybe`, `property`, `to`, `with`, `yield`, `async`, `await`, `del`
   - Soft Keywords: `_`, `get`, `set`, `init`
   - Operators: `+`, `-`, `*`, `/`, `//`, `%`, `**`, `=`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `+=`, `-=`, etc.
   - Delimiters: `(`, `)`, `[`, `]`, `{`, `}`, `:`, `,`, `.`, `->`, `?`, `??`
   - Literals: integers, floats, strings (including f-strings basic), booleans
   - Identifiers: standard naming rules

2. **Indentation Handling**
   - Track indentation levels
   - Emit `INDENT` tokens on indentation increase
   - Emit `DEDENT` tokens on indentation decrease
   - Support spaces (4-space standard) and tabs (discouraged but allowed)

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

## Phase 0.1.1: Parser Foundation

**Goal:** Parse Sharpy source into an Abstract Syntax Tree (AST).

### Features

1. **Module Structure**
   - Module as top-level container
   - Support for top-level statements

2. **AST Node Types**
   - `Module`: root node containing statements
   - `ExpressionStatement`: standalone expressions
   - `Identifier`: variable/function names
   - `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `BooleanLiteral`, `NoneLiteral`
   - `BinaryExpression`: `a + b`, `a == b`, etc.
   - `UnaryExpression`: `-x`, `not x`
   - `PassStatement`: placeholder

3. **Expression Parsing (Precedence Climbing)**
   - Lowest: Conditional (`x if test else y`) — *deferred*
   - Null coalesce: `??`
   - Logical: `or`, `and`, `not`
   - Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, `is`
   - Bitwise: `|`, `^`, `&`, `<<`, `>>`
   - Arithmetic: `+`, `-`, `*`, `/`, `//`, `%`
   - Power: `**` (right-associative)
   - Unary: `-x`, `+x`, `~x`, `not x`
   - Primary: literals, identifiers, parenthesized expressions

4. **Type Annotations (Parsing Only)**
   - Simple types: `int`, `str`, `bool`, `float`
   - Nullable: `int?`
   - Generic placeholder: `list[int]` (parse structure, defer semantics)

### Test Cases

```python
# Expressions
42
-x
a + b * c
(a + b) * c
not True and False

# Pass statement
pass
```

### Exit Criteria

- AST correctly represents expression precedence
- Parentheses override precedence
- Type annotations parsed but not validated
- Module structure captured

---

## Phase 0.1.2: Code Generation Bootstrap

**Goal:** Generate executable C# code via Roslyn for minimal programs.

### Features

1. **Entry Point Generation**
   - Top-level statements wrapped in `Main()` method
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

## Phase 0.1.3: Variables & Expressions

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
   x = 42        # Inferred as int
   y = 3.14      # Inferred as double
   ```

3. **Assignment Operators**
   - Simple: `=`
   - Augmented: `+=`, `-=`, `*=`, `/=`, `//=`, `%=`

4. **Constant Declaration**
   ```python
   const PI: float = 3.14159
   const MAX_SIZE: int = 100
   ```

5. **Semantic Analysis (Phase 1)**
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

## Phase 0.1.4: Control Flow

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

5. **Control Flow Validation**
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

---

## Phase 0.1.5: Functions

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

6. **Function Overloading** (Optional for 0.1.5)
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

## Phase 0.1.6: Classes

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
       def square(x: int) -> int:
           return x * x
   ```

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

## Phase 0.1.7: Inheritance & Interfaces

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
       @override
       def draw(self) -> None:
           pass  # drawing logic
   ```

4. **Multiple Interface Implementation**
   ```python
   interface ISerializable:
       def serialize(self) -> str:
           ...

   class Data(IDrawable, ISerializable):
       @override
       def draw(self) -> None:
           pass

       @override
       def serialize(self) -> str:
           return "{}"
   ```

5. **Decorators**
   - `@virtual`: Method can be overridden
   - `@override`: Method overrides base
   - `@abstract`: Method must be overridden
   - `@static`: Static method
   - `@final`: Cannot be overridden (on methods) or inherited (on classes)

6. **Access Modifiers**
   - `@public` (default)
   - `@private`
   - `@protected`
   - `@internal`

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

    @override
    def compare_to(self, other: object) -> int:
        if not isinstance(other, Integer):
            return -1
        return self.value - other.value
```

### Exit Criteria

- Single inheritance works
- `super()` calls parent constructor/methods
- Abstract classes cannot be instantiated
- Abstract methods must be overridden
- Interfaces define contracts
- Multiple interfaces supported
- Decorator modifiers apply correctly

---

## Phase 0.1.8: Structs & Enums

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
       PENDING
       ACTIVE
       COMPLETED
   ```

4. **Enum with Methods** (Basic)
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

5. **Flags Enum** (Optional)
   ```python
   @flags
   enum Permissions:
       READ = 1
       WRITE = 2
       EXECUTE = 4
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

## Phase 0.1.9: Type System Enhancements

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

5. **Basic Generics** (For Collections)
   - Parse generic syntax: `list[T]`, `dict[K, V]`
   - Instantiate with concrete types
   - Defer user-defined generics to later

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

---

## Phase 0.1.10: Module System

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

3. **Module Resolution**
   - Search current directory
   - Search project directories
   - Search standard library paths

4. **Multi-File Compilation**
   - Compile multiple `.spy` files
   - Resolve cross-module references
   - Handle circular imports (type annotations only)

5. **Package Structure**
   ```
   project/
       main.spy
       utils/
           __init__.spy
           helpers.spy
   ```

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

- `import` loads module symbols
- `from ... import` selectively imports
- Module aliases work
- Multi-file projects compile
- Circular type imports don't crash

---

## Phase 0.1.11: Collections & Standard Library

**Goal:** Built-in collections and minimal standard library.

### Features

1. **List**
   ```python
   numbers: list[int] = [1, 2, 3]
   numbers.append(4)
   first = numbers[0]
   length = len(numbers)

   # List comprehension (basic)
   squares = [x * x for x in range(10)]
   ```

2. **Dictionary**
   ```python
   ages: dict[str, int] = {"Alice": 30, "Bob": 25}
   ages["Charlie"] = 35
   age = ages.get("Alice", 0)
   ```

3. **Set**
   ```python
   unique: set[int] = {1, 2, 3, 2, 1}  # {1, 2, 3}
   unique.add(4)
   ```

4. **Built-in Functions**
   - `print(*args)`: Output to console
   - `len(collection)`: Collection length
   - `range(stop)`, `range(start, stop)`, `range(start, stop, step)`
   - `str(value)`, `int(value)`, `float(value)`: Type conversion
   - `isinstance(obj, type)`: Type checking
   - `type(obj)`: Get type (limited)

5. **String Operations**
   ```python
   s = "hello"
   upper = s.upper()
   contains = "ell" in s
   formatted = f"Value: {x}"
   ```

### Code Generation

```python
items: list[int] = [1, 2, 3]
```

Generates:

```csharp
Sharpy.Core.List<int> items = new Sharpy.Core.List<int> { 1, 2, 3 };
```

### Test Cases

```python
# Working with collections
numbers = [1, 2, 3, 4, 5]
doubled = [n * 2 for n in numbers]
print(doubled)  # [2, 4, 6, 8, 10]

person = {"name": "Alice", "age": "30"}
print(f"Name: {person['name']}")

unique = {1, 2, 2, 3}
print(len(unique))  # 3
```

### Exit Criteria

- `list`, `dict`, `set` literals work
- Collection methods available
- `print()` outputs to console
- `len()` works on collections
- F-string interpolation works
- List comprehensions work (basic)

---

## Phase 0.1.12: .NET Interop

**Goal:** Import and use .NET types and methods.

### Features

1. **Importing .NET Namespaces**
   ```python
   from System import Console, DateTime
   from System.Collections.Generic import Dictionary
   from System.IO import File, Path
   ```

2. **Using .NET Types**
   ```python
   from System import DateTime

   now = DateTime.Now
   year = now.Year
   formatted = now.ToString("yyyy-MM-dd")
   ```

3. **Calling .NET Static Methods**
   ```python
   from System.IO import File

   content = File.ReadAllText("file.txt")
   File.WriteAllText("output.txt", content)
   ```

4. **Name Mangling (Bidirectional)**
   - Sharpy `snake_case` → C# `PascalCase`
   - C# `PascalCase` → Sharpy `snake_case` (for discoverability)
   - Backtick escapes: `` `PascalCase` `` for exact names

5. **Generic .NET Types**
   ```python
   from System.Collections.Generic import List

   names: List[str] = List[str]()
   names.Add("Alice")
   ```

### Test Cases

```python
from System import Console, DateTime, Math

Console.WriteLine("Hello from Sharpy!")

now = DateTime.Now
Console.WriteLine(f"Current time: {now}")

sqrt_val = Math.Sqrt(16.0)
Console.WriteLine(f"sqrt(16) = {sqrt_val}")
```

### Exit Criteria

- .NET namespaces importable
- Static methods callable
- Instance methods callable
- Generic .NET types work
- Name mangling transparent

---

## Phase 0.1.13: Exception Handling

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

4. **Raise Statement**
   ```python
   def validate(x: int) -> None:
       if x < 0:
           raise ValueError("x must be non-negative")
   ```

5. **Exception Types**
   - Map to .NET exceptions
   - `Exception` → `System.Exception`
   - `ValueError` → `System.ArgumentException`
   - `TypeError` → `System.InvalidCastException`
   - `IOError` → `System.IO.IOException`

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

- Try/except catches exceptions
- Exception binding (`as e`) works
- Multiple except clauses checked in order
- Finally always executes
- Raise creates and throws exceptions

---

## Phase 0.1.14: Lambdas & Delegates

**Goal:** Lambda expressions and function type handling.

### Features

1. **Lambda Expressions**
   ```python
   square = lambda x: x * x
   add = lambda a, b: a + b

   # With type annotation
   transform: (int) -> int = lambda x: x * 2
   ```

2. **Function Types**
   ```python
   def apply(func: (int) -> int, value: int) -> int:
       return func(value)

   result = apply(lambda x: x * 2, 5)  # 10
   ```

3. **Higher-Order Functions**
   ```python
   def map_list(items: list[int], f: (int) -> int) -> list[int]:
       result: list[int] = []
       for item in items:
           result.append(f(item))
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

5. **Method References** (Optional)
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

- Lambda expressions compile
- Function type annotations work
- Lambdas passed as arguments
- Action/Func delegates generated correctly
- Method references work (if implemented)

---

## Phase 0.1.15: Essential Dunders

**Goal:** Core dunder methods for operators and protocols.

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

4. **Comparison Operators**
   ```python
   class Number:
       value: int

       def __lt__(self, other: Number) -> bool:
           return self.value < other.value

       def __le__(self, other: Number) -> bool:
           return self.value <= other.value

       # Compiler synthesizes __gt__, __ge__ from __lt__, __le__
   ```

5. **Dunder → C# Mapping**
   | Dunder | C# Generation |
   |--------|---------------|
   | `__init__` | Constructor |
   | `__str__` | `ToString()` override |
   | `__eq__` | `Equals()` + `operator ==` |
   | `__hash__` | `GetHashCode()` override |
   | `__add__` | `operator +` |
   | `__len__` | `Length` property or method |
   | `__getitem__` | Indexer `this[...]` get |
   | `__setitem__` | Indexer `this[...]` set |
   | `__contains__` | Used by `in` operator |

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

- `__str__` generates `ToString()`
- `__eq__`/`__hash__` generate proper overrides
- Arithmetic dunders generate operators
- `__len__`/`__getitem__`/`__setitem__` work
- Operator synthesis (e.g., `!=` from `==`)
- `in` operator uses `__contains__`

---

## Summary: What's Included vs Deferred

### Included in v0.1.x

| Category | Features |
|----------|----------|
| **Types** | Primitives, classes, structs, interfaces, enums, nullable (`T?`), type aliases |
| **Functions** | Definitions, default params, keyword args, lambdas, return |
| **OOP** | Single inheritance, interfaces, abstract classes, `@virtual`/`@override`/`@abstract`/`@final` |
| **Control Flow** | if/elif/else, while, for, break, continue |
| **Collections** | `list`, `dict`, `set`, basic comprehensions |
| **Dunders** | `__init__`, `__str__`, `__eq__`, `__hash__`, `__len__`, `__getitem__`, `__setitem__`, `__contains__`, arithmetic operators |
| **Exceptions** | try/except/finally, raise |
| **Modules** | import, from import, multi-file |
| **Interop** | .NET type imports, method calls |

### Deferred to v0.2.x+

| Category | Features |
|----------|----------|
| **Types** | Tagged unions (ADTs), Result/Optional types, user-defined generics |
| **Functions** | Variadic args (`*args`), partial application |
| **OOP** | Properties (full), events, delegates (custom definition) |
| **Control Flow** | Pattern matching (`match`), loop else, context managers (`with`) |
| **Collections** | Dict/set comprehensions, advanced comprehension filters |
| **Dunders** | `__iter__`/`__next__`, `__enter__`/`__exit__`, `__call__` |
| **Async** | async/await, generators, yield |
| **Advanced** | Conversion operators, extension methods definition |

---

## Implementation Order Rationale

The phases are ordered to maximize **incremental testability**:

1. **0.1.0-0.1.2**: Foundation layers must come first (no shortcuts)
2. **0.1.3-0.1.4**: Variables and control flow enable meaningful programs
3. **0.1.5**: Functions unlock code organization
4. **0.1.6-0.1.7**: Classes and inheritance for OOP
5. **0.1.8**: Structs/enums are simpler than classes but depend on type system
6. **0.1.9**: Type enhancements needed before advanced features
7. **0.1.10**: Module system for real projects
8. **0.1.11**: Collections make the language practical
9. **0.1.12**: .NET interop for ecosystem access
10. **0.1.13**: Exception handling for robustness
11. **0.1.14**: Lambdas enable functional patterns
12. **0.1.15**: Dunders for Pythonic feel and operator customization

Each phase produces a **working compiler** that can compile and run programs using features from that phase and all previous phases.
