# Sharpy Language Reference

## Version Guide

Features are marked with their target version:

- **[v0.5]** - P0: Core features required for C# interop and Unity development
- **[v1.0]** - P1: Enhanced features and Pythonic additions
- **[v1.5+]** - P2: Advanced features and language extensions

### Implementation Requirements for v0.5

**Lexical Rules:**
- Indentation: **Always 4 spaces**, no tabs allowed
- Line endings: LF (`\n`) or CRLF (`\r\n`)
- Encoding: UTF-8
- Comments: `#` starts single-line comment to end of line
- Multi-line strings: Triple quotes (`"""` or `'''`)

**Literals Supported in v0.5:**
- Integer literals: Decimal only (e.g., `42`, `0`, `-10`)
  - No binary (`0b1010`), octal (`0o777`), or hex (`0xFF`) in v0.5
- Float literals: Decimal only (e.g., `3.14`, `0.5`, `-2.718`)
  - No scientific notation (`1e10`) in v0.5
- String literals: Double-quoted (`"hello"`) or single-quoted (`'hello'`)
- Boolean literals: `True`, `False`
- None literal: `None`

**Features Deferred to v1.0:**
- Properties (both explicit and auto-generated)
- Special integer/float literal formats (binary, octal, hex, scientific)
- Type aliases
- Walrus operator (`:=`)
- Comprehensions
- Match statements/pattern matching
- Context managers (`with` statement)
- Del statement
- Events
- Tagged unions

**Features Deferred to v1.5+:**
- Async/await
- Defer statement
- Generators (`yield`)

---

## Lexical Structure **[v0.5]**

### Source Files

- File extension: `.spy`
- Encoding: UTF-8 (required)
- Line endings: LF (`\n`) or CRLF (`\r\n`)
- Byte Order Mark (BOM): Optional but not recommended

### Line Structure

Sharpy uses indentation to denote code blocks (like Python):

```python
if condition:
    statement1  # 4 spaces indentation
    statement2  # 4 spaces indentation
else:
    statement3  # 4 spaces indentation
```

**Indentation Rules:**
- **Exactly 4 spaces per indentation level** (enforced)
- Tabs are **not allowed** for indentation
- Mixed spaces and tabs cause a lexical error
- Indentation must be consistent within a file

### Comments

```python
# This is a single-line comment

x = 42  # Comment at end of line

# Comments can span multiple lines
# by using # at the start of each line

"""
This is a docstring, not a comment.
Docstrings are string literals used for documentation.
"""
```

**Comment Rules:**
- Single-line comments start with `#` and continue to end of line
- `#` inside string literals is not a comment
- No multi-line comment syntax (like `/* */` in C)
- Docstrings (triple-quoted strings) can serve as multi-line documentation

### Identifiers

Identifiers are names for variables, functions, classes, etc.

**Syntax:**
```
identifier ::= (letter | '_') (letter | digit | '_')*
letter     ::= 'a'..'z' | 'A'..'Z'
digit      ::= '0'..'9'
```

**Rules:**
- Must start with letter or underscore
- Can contain letters, digits, and underscores
- Case-sensitive: `myVar`, `myvar`, and `MYVAR` are different identifiers
- Cannot be a keyword
- Unicode letters are allowed but discouraged for interop

**Examples:**
```python
# Valid identifiers
x
my_variable
_private
ClassName
MAX_SIZE
value2
_internal_counter

# Invalid identifiers
2fast      # Cannot start with digit
my-var     # Hyphen not allowed
class      # Keyword
```

**Naming Conventions:**

| Type | Convention | Example |
|------|------------|---------|
| Local variable | `snake_case` | `user_name`, `item_count` |
| Function/method | `snake_case` | `calculate_total`, `get_user` |
| Class | `PascalCase` | `UserAccount`, `HttpClient` |
| Struct | `PascalCase` | `Vector2`, `Point3D` |
| Interface | `IPascalCase` | `IDrawable`, `ISerializable` |
| Constant | `CAPS_SNAKE_CASE` | `MAX_SIZE`, `PI` |
| Module | `snake_case` | `user_service`, `http_client` |
| Enum type | `PascalCase` | `Color`, `Status` |
| Enum value | `CAPS_SNAKE_CASE` | `RED`, `ACTIVE` |

### Whitespace

- Spaces (` `), tabs (`\t`), and form feeds (`\f`) are whitespace
- Whitespace is used to separate tokens
- Leading whitespace (indentation) is significant
- Trailing whitespace is ignored
- Blank lines are ignored

### Physical Lines vs Logical Lines

**Physical line:** A sequence of characters terminated by end-of-line

**Logical line:** A statement (may span multiple physical lines)

```python
# Single logical line
x = 42

# Logical line split across physical lines (explicit)
total = value1 + \
        value2 + \
        value3

# Implicit line continuation inside brackets
items = [
    1, 2, 3,
    4, 5, 6
]

# Function call with multiple arguments
result = function(
    arg1,
    arg2,
    arg3
)
```

**Line Continuation Rules:**
- Explicit: Use backslash `\` at end of line
- Implicit: Inside `()`, `[]`, `{}` brackets
- Cannot continue in middle of identifier or keyword
- Cannot continue inside single-line strings

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

The following are hard keywords in Sharpy v0.5 and are always reserved:

| Sharpy | Notes |
| ------ | ----- |
| `and` | Boolean and |
| `as` | Aliasing for imports, type casting |
| `assert` | Assertion statement |
| `auto` | Inferred type declaration |
| `break` | Break statement for loops |
| `class` | Class (reference type) declaration |
| `const` | Constant declaration |
| `continue` | Continue statement for loops |
| `def` | Function/method definition |
| `elif` | Else-if block for conditionals |
| `else` | Else block for conditionals and loops |
| `enum` | Enumeration declaration (simple enums only in v0.5) |
| `except` | Exception handler block |
| `False` | Boolean false literal |
| `finally` | Finally block for exception handling |
| `for` | For loop |
| `from` | Selective imports |
| `if` | If block for conditionals |
| `import` | Import statement |
| `in` | Membership test operator |
| `interface` | Interface declaration |
| `is` | Identity comparison operator |
| `lambda` | Lambda expression |
| `None` | None/null literal |
| `not` | Boolean not operator |
| `or` | Boolean or operator |
| `pass` | No-op placeholder statement |
| `raise` | Raise exception statement |
| `return` | Return statement |
| `struct` | Struct (value type) declaration |
| `True` | Boolean true literal |
| `try` | Try block for exception handling |
| `while` | While loop |

**Reserved for Future Use (v1.0+):**
These keywords are reserved but not implemented in v0.5:
- `async`, `await` - Async programming (v1.5+)
- `case`, `match` - Pattern matching (v1.0)
- `defer` - Deferred execution (v1.5+)
- `del` - Delete statement (v1.0)
- `event` - Event declarations (v1.0)
- `get`, `set`, `property` - Properties (v1.0)
- `type` - Type aliases (v1.0)
- `with` - Context managers (v1.0)
- `yield` - Generators (v1.5+)

### Soft Keywords (Context-Dependent) **[v0.5]**

The following identifiers have special meaning only in specific contexts:

| Sharpy | Context | Notes |
| ------ | ------- | ----- |
| `_` | Pattern matching, destructuring | Single underscore as wildcard (v1.0+) |

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

### Operators **[v0.5]**

**Arithmetic Operators:**
- `+` - Addition or unary plus
- `-` - Subtraction or unary minus
- `*` - Multiplication
- `/` - Division (always returns double)
- `//` - Floor division (integer division)
- `%` - Modulo (remainder)
- `**` - Exponentiation

**Comparison Operators:**
- `==` - Equality
- `!=` - Inequality
- `<` - Less than
- `>` - Greater than
- `<=` - Less than or equal
- `>=` - Greater than or equal

**Logical Operators:**
- `and` - Logical AND (short-circuit)
- `or` - Logical OR (short-circuit)
- `not` - Logical NOT

**Bitwise Operators:**
- `&` - Bitwise AND
- `|` - Bitwise OR
- `^` - Bitwise XOR
- `~` - Bitwise NOT (unary)
- `<<` - Left shift
- `>>` - Right shift

**Membership Operators:**
- `in` - Membership test (e.g., `x in list`)
- `not in` - Negated membership test

**Identity Operators:**
- `is` - Identity comparison (reference equality)
- `is not` - Negated identity comparison

**Assignment Operators:**
- `=` - Simple assignment
- `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=` - Augmented arithmetic assignment
- `&=`, `|=`, `^=`, `<<=`, `>>=` - Augmented bitwise assignment

**Sharpy-Specific Operators:**
- `?.` - Null-conditional member access (e.g., `obj?.method()`)
- `??` - Null coalescing (e.g., `value ?? default`)

**Member Access:**
- `.` - Member access (attribute, method)
- `[]` - Indexing and slicing
- `()` - Function/method call

**Not in v0.5:**
- `:=` - Walrus operator (v1.0)
- `@` - Matrix multiply (v1.0)

### Operator Precedence **[v0.5]**

Operators are listed from highest to lowest precedence:

| Precedence | Operators | Description | Associativity |
|------------|-----------|-------------|---------------|
| 1 | `()`, `[]`, `.`, `?.` | Grouping, indexing, member access, null-conditional | Left-to-right |
| 2 | `**` | Exponentiation | Right-to-left |
| 3 | `+x`, `-x`, `~x` | Unary plus, minus, bitwise NOT | Right-to-left |
| 4 | `*`, `/`, `//`, `%` | Multiplication, division, floor division, modulo | Left-to-right |
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
| 15 | `if`-`else` | Ternary conditional expression | Right-to-left |
| 16 | `lambda` | Lambda expression | N/A |

**Notes:**
- Comparison operators can be chained: `a < b < c` is equivalent to `a < b and b < c`
- The `??` operator has lower precedence than `or`, so `a or b ?? c` is `(a or b) ?? c`
- All binary operators except `**` are left-associative
- `**` is right-associative: `2 ** 3 ** 2` is `2 ** (3 ** 2)` = 512

## Literals and Special Values **[v0.5]**

### Integer Literals **[v0.5]**

In v0.5, only decimal integer literals are supported:

```python
# Decimal integers
x = 0
y = 42
z = -10
large = 1000000

# Underscores for readability (optional)
million = 1_000_000
billion = 1_000_000_000
```

**Type inference:**
- Integer literals are inferred as `int` (maps to `System.Int32`)
- Suffix notation for explicit sizing (optional):
  - `L` or `l` for `long` (System.Int64): `42L`
  - `u` or `U` for `uint` (System.UInt32): `42u`
  - `ul` or `UL` for `ulong` (System.UInt64): `42ul`

**Not in v0.5:**
- Binary literals (`0b1010`) - v1.0
- Octal literals (`0o755`) - v1.0
- Hexadecimal literals (`0xFF`, `0xff`) - v1.0

### Float Literals **[v0.5]**

In v0.5, only decimal float literals are supported:

```python
# Decimal floats (64-bit)
pi = 3.14159
half = 0.5
negative = -2.718

# Must have digit before or after decimal point
valid1 = 0.5
valid2 = 5.0
invalid = .5  # ERROR: Must have digit before decimal

# Underscores for readability (optional)
precise = 3.141_592_653
```

**Type inference:**
- Float literals with decimal point are inferred as `double` (System.Double)
- Suffix notation for explicit typing (optional):
  - `f` or `F` for `float` (System.Single): `3.14f`
  - `d` or `D` for `double` (System.Double): `3.14d` (redundant but allowed)
  - `m` or `M` for `decimal` (System.Decimal): `3.14m`

**Not in v0.5:**
- Scientific notation (`1.5e10`, `3.14E-5`) - v1.0
- Hexadecimal floats (`0x1.2p3`) - v1.0

### String Literals **[v0.5]**

```python
# Single-quoted strings
name = 'Alice'
greeting = 'Hello, World!'

# Double-quoted strings
message = "Hello, World!"
quote = "She said, 'Hello'"

# F-strings (formatted string literals)
name = "Alice"
age = 30
msg = f"My name is {name} and I'm {age} years old"

# Raw strings (backslashes not escaped)
path = r"C:\Users\Alice\Documents"
regex = r"\d+\.\d+"

# Multi-line strings (triple-quoted)
multi = """
This is a
multi-line string
"""

# Multi-line f-strings
report = f"""
Name: {name}
Age: {age}
Status: Active
"""
```

**Escape Sequences:**

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

### Boolean Literals **[v0.5]**

```python
is_ready = True
is_complete = False
```

- `True` maps to `true` (System.Boolean)
- `False` maps to `false` (System.Boolean)

### None Literal **[v0.5]**

```python
value: str? = None
result = None  # Compile-time error, cannot determine T for implied T? in this context
```

- `None` maps to `null` in the compiled output
- Can be assigned to any nullable type

### Special Literals **[v0.5]**

| Sharpy | Notes |
|--------|-------|
| `...` | Ellipsis - placeholder for unimplemented code (like `pass`) |
| `{/}` | Empty set literal |

**Ellipsis (`...`) Usage in v0.5:**

```python
# 1. Placeholder in interface method definitions
interface IDrawable:
    def draw(self) -> None:
        ...

# 2. Placeholder for unimplemented code (alternative to 'pass')
def todo_function():
    ...

# 3. Abstract method body
class AbstractBase:
    def abstract_method(self) -> int:
        ...  # Subclasses must implement
```

**Not in v0.5:**
- Ellipsis in slicing (`matrix[0, ...]`) - v1.0
- Ellipsis in type hints (`tuple[int, ...]`) - v1.0

### Literal Names **[v0.5]**

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

## Expressions **[v0.5]**

Expressions are combinations of values, variables, operators, and function calls that evaluate to a value.

### Primary Expressions **[v0.5]**

The simplest forms of expressions:

```python
# Literals
42                  # Integer literal
3.14                # Float literal
"hello"             # String literal
True                # Boolean literal
None                # None literal

# Identifiers (variables)
x                   # Variable reference
my_variable         # Variable reference

# Parenthesized expressions
(x + y)             # Grouping
(2 + 3) * 4         # Precedence control

# Collection literals
[1, 2, 3]           # List literal
{"a": 1, "b": 2}    # Dictionary literal
{1, 2, 3}           # Set literal
(1, 2)              # Tuple literal
```

### Member Access Expressions **[v0.5]**

Access members (fields, properties, methods) of objects:

```python
# Attribute access
obj.field           # Access field
obj.method          # Access method (returns function object)
obj.method()        # Call method

# Chained access
person.address.city
customer.get_account().get_balance()

# Null-conditional access (short-circuits if None)
obj?.method()       # Returns None if obj is None
obj?.field          # Returns None if obj is None
obj?.field?.nested  # Chains null checks

# Example with null-conditional
result = person?.address?.city  # None if person or address is None
```

**Member Access Rules:**
- `.` requires left operand to be non-None (runtime check)
- `?.` short-circuits to None if left operand is None
- Both work with fields, properties, and methods

### Indexing and Slicing Expressions **[v0.5]**

Access elements in collections:

```python
# Single index
arr[0]              # First element
arr[-1]             # Last element
arr[i]              # Element at index i

# Tuple indexing (multi-dimensional)
matrix[i, j]        # 2D indexing
grid[x, y, z]       # 3D indexing

# Slicing
arr[1:5]            # Elements 1 through 4
arr[:3]             # First 3 elements
arr[3:]             # From index 3 to end
arr[:]              # Copy all elements
arr[1:10:2]         # Every 2nd element from 1 to 9
arr[::-1]           # Reverse

# Slicing with negative indices
arr[-3:]            # Last 3 elements
arr[:-1]            # All but last element
```

### Function Call Expressions **[v0.5]**

Invoke functions and methods:

```python
# Simple function call
print("Hello")
len(items)
calculate_total(100, 0.08)

# Method calls
obj.method()
obj.method(arg1, arg2)

# Chained calls
get_object().get_value().to_string()

# Constructor calls (type instantiation)
Person("Alice", 30)
Vector2(10.0, 20.0)
List[int]()

# Generic type instantiation
container = ListContainer[str]()
dict = Dictionary[str, int]()
```

**Function Call Rules:**
- Arguments passed by position
- All required parameters must be provided
- Default parameters can be omitted
- No keyword arguments in v0.5
- No variadic arguments (`*args`, `**kwargs`) in v0.5

### Arithmetic Expressions **[v0.5]**

Binary and unary arithmetic operations:

```python
# Binary operators
x + y               # Addition
x - y               # Subtraction
x * y               # Multiplication
x / y               # Division (always returns float/double)
x // y              # Floor division (returns int if both int)
x % y               # Modulo (remainder)
x ** y              # Exponentiation (right-associative)

# Unary operators
+x                  # Unary plus (identity)
-x                  # Unary minus (negation)

# Examples
result = 2 + 3 * 4          # 14 (multiplication first)
power = 2 ** 3 ** 2         # 512 (right-associative: 2 ** (3 ** 2))
quotient = 10 / 3           # 3.333...
floor_quotient = 10 // 3    # 3
remainder = 10 % 3          # 1
```

### Comparison Expressions **[v0.5]**

Compare values, evaluate to bool:

```python
# Comparison operators
x == y              # Equal
x != y              # Not equal
x < y               # Less than
x <= y              # Less than or equal
x > y               # Greater than
x >= y              # Greater than or equal

# Identity operators
x is y              # Same object identity
x is not y          # Different object identity

# Membership operators
item in collection          # Contains
item not in collection      # Does not contain

# Comparison chaining
a < b < c           # Equivalent to: a < b and b < c
x == y == z         # Equivalent to: x == y and y == z
```

**Comparison Rules:**
- Comparisons return `bool` type
- Can chain comparisons (evaluated left-to-right)
- `is` checks object identity (reference equality)
- `==` checks value equality (calls `__eq__` if defined)

### Logical Expressions **[v0.5]**

Boolean logic operations:

```python
# Logical operators
x and y             # Logical AND (short-circuits)
x or y              # Logical OR (short-circuits)
not x               # Logical NOT

# Short-circuit evaluation
result = expensive_check() and another_check()  # second only if first is True
result = quick_check() or fallback_check()      # second only if first is False

# Examples
if is_valid and has_permission:
    do_something()

value = input_value or default_value  # Use default if input_value is falsy
```

**Logical Operator Behavior:**
- `and` returns first falsy value, or last value if all truthy
- `or` returns first truthy value, or last value if all falsy
- `not` always returns `bool`

### Bitwise Expressions **[v0.5]**

Bit-level operations on integers:

```python
# Bitwise operators
x & y               # Bitwise AND
x | y               # Bitwise OR
x ^ y               # Bitwise XOR
~x                  # Bitwise NOT (complement)
x << n              # Left shift
x >> n              # Right shift

# Examples
flags = FLAG_READ | FLAG_WRITE      # Combine flags
has_read = (flags & FLAG_READ) != 0 # Test flag
toggle = flags ^ FLAG_EXECUTE       # Toggle flag
mask = ~0xFF                        # Create mask
shifted = value << 8                # Shift left 8 bits
```

### Conditional Expressions **[v0.5]**

Ternary conditional (if-else expression):

```python
# Syntax: true_value if condition else false_value
result = x if x > 0 else -x         # Absolute value
status = "even" if n % 2 == 0 else "odd"
max_val = a if a > b else b

# Can be nested (but reduces readability)
category = "small" if x < 10 else ("medium" if x < 100 else "large")
```

**Rules:**
- Evaluates to one of two values based on condition
- Condition must be boolean expression
- Both branches must be present
- Only the selected branch is evaluated

### Lambda Expressions **[v0.5]**

Anonymous function expressions:

```python
# Syntax: lambda parameters: expression
square = lambda x: x ** 2
add = lambda x, y: x + y

# Used in higher-order functions
numbers = [1, 2, 3, 4, 5]
evens = filter(lambda x: x % 2 == 0, numbers)
doubled = map(lambda x: x * 2, numbers)

# As function arguments
def apply(value: int, func: (int) -> int) -> int:
    return func(value)

result = apply(10, lambda x: x ** 2)  # 100
```

**Lambda Rules:**
- Single expression only (no statements)
- Parameters types and return type cannot be specified and must be inferred from context
- Cannot have multi-line body
- Expression result is automatically returned

### Type Cast Expressions **[v0.5]**

Explicit type conversions:

```python
# Casting with type name (constructor-style)
x = int(3.14)           # 3
y = double(42)          # 42.0
s = str(123)            # "123"

# as operator for explicit casts
obj = value as BaseType
result = expression as TargetType

# Type test and narrowing
if isinstance(obj, SpecificType):
    # obj is narrowed to SpecificType here
    obj.specific_method()
```

### Null-Coalescing Expressions **[v0.5]**

Provide default for None values:

```python
# Syntax: value ?? default
name = user_name ?? "Anonymous"
count = get_count() ?? 0

# Chaining
result = first_option() ?? second_option() ?? default_value

# Left-associative
x ?? y ?? z  # Equivalent to: (x ?? y) ?? z
```

**Null-Coalescing Rules:**
- Returns left operand if not None
- Returns right operand if left is None
- Only evaluates right operand if needed (short-circuits)
- Lower precedence than `or`

### Type Check Expressions **[v0.5]**

Check and narrow types:

```python
# isinstance for type checking
if isinstance(obj, str):
    # obj is narrowed to str type here
    length = len(obj)

if isinstance(value, int):
    # value is narrowed to int
    squared = value ** 2

# Multiple types
if isinstance(obj, (str, int)):
    # obj is str or int
    process(obj)

# is None check (type narrowing)
if obj is not None:
    # obj type is narrowed (None removed)
    obj.method()  # Safe to call
```

**Type Narrowing:**
- `isinstance()` narrows type in true branch
- `is not None` removes `None` from nullable type
- `is None` narrows to `None` in true branch
- Narrowing applies within the conditional scope

### String Formatting Expressions **[v0.5]**

F-string interpolation:

```python
# Basic interpolation
name = "Alice"
greeting = f"Hello, {name}!"

# Expressions in f-strings
x = 10
message = f"The value is {x * 2}"

# Format specifiers
pi = 3.14159
formatted = f"Pi: {pi:.2f}"  # "Pi: 3.14"

# Multiple interpolations
result = f"{name} scored {score} points in {time:.1f} seconds"
```

### Expression Evaluation Order **[v0.5]**

**General Rules:**
1. Expressions evaluated left-to-right
2. Operator precedence determines sub-expression grouping
3. Short-circuit operators (`and`, `or`, `??`, `?.`) stop early when possible
4. Function arguments evaluated left-to-right before call
5. Chained comparisons evaluated left-to-right

**Examples:**

```python
# Left-to-right evaluation
result = f1() + f2() * f3()
# Order: f2(), f3(), multiply, f1(), add

# Short-circuit
result = cheap() and expensive()
# If cheap() is False, expensive() never called

# Argument evaluation
func(first(), second(), third())
# Order: first(), second(), third(), then func() called
```

**Not in v0.5:**
- Walrus operator (`:=`)
- Comprehensions (list/dict/set/generator)
- Generator expressions
- Await expressions
- Yield expressions

## Parsing Disambiguation **[v0.5]**

This section provides guidance for parser implementation when syntax is ambiguous.

### Tuple vs Parenthesized Expression **[v0.5]**

**Disambiguation Rule:** Presence of comma determines tuple vs expression.

```python
# Single element in parentheses - NOT a tuple
x = (42)             # Parenthesized expression, x is int

# Single element with trailing comma - IS a tuple
y = (42,)            # Tuple with one element, y is tuple[int]

# Multiple elements - IS a tuple (parentheses optional)
point = (10, 20)     # Tuple, point is tuple[int, int]
point = 10, 20       # Also a tuple (same as above)

# Empty parentheses - empty tuple
empty = ()           # Empty tuple, type is tuple[()]
```

**Parsing Strategy:**
1. If expression starts with `(`:
   - Look ahead for comma before closing `)`
   - If comma found (or empty): parse as tuple
   - If no comma: parse as parenthesized expression
2. If expression has comma at top level (not in nested brackets):
   - Parse as tuple without parentheses

### Type Annotation vs Generic Call **[v0.5]**

**Disambiguation Rule:** Context determines if `[]` is type parameter or indexing.

```python
# Type annotation context (after : or in type position)
x: list[int] = []         # list[int] is type annotation

# Generic type instantiation (constructor call)
items = list[int]()       # list[int] is generic instantiation
map = dict[str, int]()    # dict[str, int] is generic instantiation

# Indexing operation (on expression)
value = items[0]          # items[0] is indexing
subset = items[1:5]       # items[1:5] is slicing
```

**Parsing Strategy:**
1. After `:` in variable declaration → type annotation
2. After `:` in function signature → type annotation
3. After `()` (constructor/function call) → likely generic instantiation
4. After identifier in expression position → could be either:
   - If followed by `()`: generic instantiation
   - Otherwise: indexing/slicing

### Dictionary vs Set Literal **[v0.5]**

**Disambiguation Rule:** Presence of `:` determines dict vs set.

```python
# Empty braces with special syntax {/} - SET
empty_set: set[int] = {/}

# Empty braces {} - DICTIONARY
empty_dict: dict[str, int] = {}

# Braces with colon - DICTIONARY
mapping = {"a": 1, "b": 2}

# Braces without colon - SET
numbers = {1, 2, 3}
```

**Parsing Strategy:**
1. `{}` → empty dictionary
2. `{/}` → empty set
3. `{` followed by expression:
   - Look ahead for `:` before `,` or `}`
   - If `:` found: parse as dictionary
   - If no `:`: parse as set

### Function Call vs Type Cast **[v0.5]**

**Disambiguation Rule:** Type names can be called like functions for casting.

```python
# Type cast (function-like syntax)
x = int("42")         # Cast string to int
y = double(10)        # Cast int to double
s = str(42)           # Cast int to string

# Regular function call
result = calculate(10, 20)

# Cannot distinguish syntactically - both are function calls
# Semantic analysis determines if it's a type constructor
```

**Parsing Strategy:**
- Parse both as function calls
- Semantic analysis determines if calling a type constructor

### Assignment vs Comparison **[v0.5]**

**Disambiguation Rule:** Context determines `=` vs `==`.

```python
# Assignment (statement position)
x = 42

# Comparison (expression position)
if x == 42:
    pass

# ILLEGAL: Assignment in expression (no walrus operator in v0.5)
if x = 42:    # ERROR: Cannot use = in expression
```

**Parsing Strategy:**
- Statement position: `=` is assignment
- Expression position: `==` is comparison
- `=` in expression position is syntax error in v0.5

### Member Access Chain Parsing **[v0.5]**

**Associativity Rule:** All left-to-right.

```python
# Member access chains
obj.field.method().another_field

# Parsed as:
# ((obj.field).method()).another_field

# Null-conditional chains
obj?.method()?.field

# Short-circuits at first None:
# if obj is None: result is None
# elif obj.method() is None: result is None
# else: result is obj.method().field
```

**Parsing Strategy:**
- Parse left-to-right
- Each `.` or `?.` or `[]` or `()` extends the current expression
- Build AST as nested member access nodes

### Operator Chaining **[v0.5]**

**Comparison Chaining:**

```python
# Chained comparisons
a < b < c

# Parsed as: a < b and b < c (with b evaluated once)
# NOT: (a < b) < c
```

**Parsing Strategy:**
- Special case for comparison operators
- Transform `a < b < c` into `a < b and b < c`
- Ensure middle expression evaluated only once

### Lambda vs Expression Statement **[v0.5]**

**Disambiguation Rule:** `lambda` keyword starts lambda expression.

```python
# Lambda expression
func = lambda x: x * 2

# Cannot start statement with lambda
lambda x: x * 2     # ERROR: Lambda cannot be statement (assign or pass to function)
```

**Parsing Strategy:**
- `lambda` always starts lambda expression
- Lambda cannot appear at statement level (must be in expression context)

### Indentation and Block Parsing **[v0.5]**

**Critical Rules:**

1. **Indentation increases** → Start new block
2. **Indentation decreases** → End current block(s)
3. **Indentation must decrease to a previous level** (not arbitrary)

```python
if condition:         # Indent 0
    statement1        # Indent 4 (increase → start block)
    if nested:        # Indent 4
        statement2    # Indent 8 (increase → start nested block)
        statement3    # Indent 8 (same level)
    statement4        # Indent 4 (decrease → end nested block)
statement5            # Indent 0 (decrease → end all blocks)
```

**Parsing Strategy:**

1. Maintain stack of indentation levels
2. On each line:
   - Measure leading whitespace
   - Compare to current indentation level:
     - **Increase:** Push new level, emit INDENT token
     - **Same:** Continue current block
     - **Decrease:** Pop levels until match found, emit DEDENT tokens
     - **No match:** Indentation error
3. At end of file: Emit DEDENT for each remaining level

**Token Stream Example:**

```python
if x:        # INDENT expected after this
    y = 1    # INDENT emitted here
    z = 2
a = 3        # DEDENT emitted here
```

Becomes token stream:
```
IF IDENTIFIER COLON NEWLINE INDENT
IDENTIFIER EQUALS NUMBER NEWLINE
IDENTIFIER EQUALS NUMBER NEWLINE DEDENT
IDENTIFIER EQUALS NUMBER NEWLINE
```

### Newline Significance **[v0.5]**

**Rules:**

1. **Newlines are significant** and terminate statements
2. **Except** inside brackets `()`, `[]`, `{}`
3. **Except** after backslash `\` continuation

```python
# Statement per line
x = 1
y = 2

# Implicit continuation (inside brackets)
items = [
    1, 2, 3,
    4, 5, 6
]

# Explicit continuation (backslash)
total = value1 + \
        value2 + \
        value3

# Newline inside string literal is literal newline
text = """Line 1
Line 2"""
```

**Parsing Strategy:**

1. Track bracket depth: `()`, `[]`, `{}`
2. If depth > 0: ignore newlines
3. If backslash at end of line: ignore next newline
4. Otherwise: newline terminates statement

### F-String Parsing **[v0.5]**

**F-strings require special lexing:** Interpolated expressions must be parsed within string context.

```python
# F-string with expression
name = "Alice"
greeting = f"Hello, {name}!"

# F-string with complex expression
result = f"Result: {x + y * 2}"

# F-string with format specifier
pi = 3.14159
formatted = f"Pi: {pi:.2f}"

# Nested braces (literal braces)
text = f"Use {{braces}} for literal braces"  # "Use {braces} for literal braces"
```

**Parsing Strategy:**

1. **Recognize f-string prefix:** `f"..."` or `F"..."` or `fr"..."` etc.

2. **Scan string content** with two modes:
   - **Text mode:** Regular string content
   - **Expression mode:** Inside `{...}`

3. **Mode switching:**
   - `{` (not `{{`) → Enter expression mode, push on stack
   - `}` (not `}}`) → Exit expression mode, pop stack
   - `{{` → Literal `{` in text mode
   - `}}` → Literal `}` in text mode

4. **Expression parsing:**
   - Inside `{...}`, parse full Python expression
   - Nested `{...}` in nested f-strings is allowed
   - Format spec after `:` (e.g., `{value:.2f}`)

5. **Format specifier:**
   - After expression, optional `:` followed by format spec
   - Format spec extends to closing `}`
   - Format spec can itself contain `{...}` for dynamic formatting

**Token Generation:**

```python
f"Hello, {name}!"
```

Generates conceptual tokens:
```
F_STRING_START "Hello, "
LBRACE
IDENTIFIER "name"
RBRACE
F_STRING_END "!"
```

**Nested F-Strings:**

```python
# F-string inside f-string expression
width = 10
value = 42
result = f"{value:{width}}"  # Dynamic width
```

**Complex Example:**

```python
# Multiple expressions with format specs
x, y = 10, 20
text = f"Point({x:>3}, {y:>3}) = {x + y}"
# "Point( 10,  20) = 30"
```

**Implementation Note:**
- F-string parsing typically requires a multi-pass approach or sophisticated lexer state machine
- Consider lexing f-string as a whole, then parsing interpolated expressions
- Track brace depth to handle nested expressions

### Decorator Parsing **[v0.5]**

**Decorators precede** function, method, or class definitions.

```python
# Single decorator
@override
def method(self) -> str:
    return "overridden"

# Multiple decorators (applied bottom-to-top)
@decorator1
@decorator2
@decorator3
def function():
    pass

# Decorator with arguments (future - not in v0.5)
# @decorator(arg1, arg2)  # v1.0+
# def function():
#     pass
```

**Parsing Strategy:**

1. **Recognize decorator:** Line starting with `@` at statement level
2. **Parse decorator name:** Identifier after `@`
3. **Collect all decorators:** Before definition
4. **Parse definition:** `def`, `class`, `struct`, etc.
5. **Apply decorators:** Associate with definition (bottom-to-top order)

**Token Sequence:**

```python
@override
def method(self):
    pass
```

Becomes:
```
AT IDENTIFIER(override) NEWLINE
DEF IDENTIFIER(method) LPAREN IDENTIFIER(self) RPAREN COLON NEWLINE
INDENT PASS NEWLINE DEDENT
```

**Multiple Decorators:**

```python
@static
@override
def method():
    pass
```

Applied as: `override(static(method))`
(Bottom decorator applied first, then working up)

**Not in v0.5:**
- Decorator arguments: `@decorator(args)`
- Decorator expressions: `@module.decorator`
- Only simple identifier decorators supported in v0.5

3. If backslash at end of line: ignore next newline
4. Otherwise: newline terminates statement

## Type Syntax **[v0.5]**

See [Type System](type_system.md) for detailed type semantics.

### Built-in Types **[v0.5]**

Sharpy provides built-in types that map directly to .NET types:

**Numeric Types:**

| Sharpy Type | .NET Type | Size | Range | Notes |
|-------------|-----------|------|-------|-------|
| `int` | `System.Int32` | 32-bit | -2,147,483,648 to 2,147,483,647 | Default integer type |
| `long` | `System.Int64` | 64-bit | -9,223,372,036,854,775,808 to 9,223,372,036,854,775,807 | Large integers |
| `short` | `System.Int16` | 16-bit | -32,768 to 32,767 | Small integers |
| `byte` | `System.Byte` | 8-bit | 0 to 255 | Unsigned byte |
| `uint` | `System.UInt32` | 32-bit | 0 to 4,294,967,295 | Unsigned 32-bit |
| `ulong` | `System.UInt64` | 64-bit | 0 to 18,446,744,073,709,551,615 | Unsigned 64-bit |
| `ushort` | `System.UInt16` | 16-bit | 0 to 65,535 | Unsigned 16-bit |
| `sbyte` | `System.SByte` | 8-bit | -128 to 127 | Signed byte |
| `float` | `System.Single` | 32-bit | ±1.5 × 10^-45 to ±3.4 × 10^38 | Single-precision float |
| `double` | `System.Double` | 64-bit | ±5.0 × 10^-324 to ±1.7 × 10^308 | Double-precision float (default) |
| `decimal` | `System.Decimal` | 128-bit | ±1.0 × 10^-28 to ±7.9 × 10^28 | High-precision decimal |

**Other Built-in Types:**

| Sharpy Type | .NET Type | Notes |
|-------------|-----------|-------|
| `bool` | `System.Boolean` | `True` or `False` |
| `str` | `System.String` | Immutable Unicode string |
| `object` | `System.Object` | Base type for all types |
| `char` | `System.Char` | Single Unicode character |

The `str` type has a Sharpy-specific extension intrinsic to the compiler to expose a Pythonic API where possible, excluding dunders, which are mapped by the compiler to idiomatic C# constructs.

**Collection Types:**

| Sharpy Type | .NET Type | Notes |
|-------------|-----------|-------|
| `list[T]` | `System.Generic.List<T>` | Mutable list |
| `dict[K, V]` | `System.Collections.Generic.Dictionary<K, V>` | Hash map |
| `set[T]` | `System.Collections.Generic.HashSet<T>` | Unique elements |
| `tuple[T1, T2, ...]` | `System.Tuple<T1, T2, ...>` or `System.ValueTuple<...>` | Fixed-size tuple |

The collection types above have Sharpy-specific extensions intrinsic to the compiler to expose a Pythonic API where possible, excluding dunders, which are mapped by the compiler to idiomatic C# constructs.

### Basic Type Annotations **[v0.5]**

```python
# Simple types
x: int = 42
name: str = "Alice"
flag: bool = True
pi: double = 3.14159

# Explicit type conversions
x: int = 42
y: long = long(x)  # Convert int to long
z: double = double(x)  # Convert int to double

# Type inference
y = 42              # Inferred as int
pi = 3.14159        # Inferred as double
```

### Generic Types **[v0.5]**

Generic types use square brackets `[]` for type parameters:

```python
# Collection types with type parameters
numbers: list[int] = [1, 2, 3]
names: list[str] = ["Alice", "Bob", "Charlie"]

# Dictionary (key-value pairs)
mapping: dict[str, int] = {"a": 1, "b": 2}
scores: dict[str, double] = {"Alice": 95.5, "Bob": 87.0}

# Set (unique values)
unique: set[str] = {"x", "y", "z"}
ids: set[int] = {1, 2, 3}

# Nested generics
matrix: list[list[double]] = [[1.0, 2.0], [3.0, 4.0]]
complex_map: dict[str, list[int]] = {"evens": [2, 4, 6], "odds": [1, 3, 5]}

# Tuples with type parameters
point: tuple[int, int] = (10, 20)
person: tuple[str, int, bool] = ("Alice", 30, True)
```

**Type Parameter Syntax:**
- Type parameters use square brackets: `list[int]`, `dict[str, double]`
- Comma-separated for multiple parameters: `dict[K, V]`
- Can be nested: `list[list[T]]`, `dict[str, list[int]]`
- Spaces around brackets are allowed: `list [int]`, `list [ int ]`, `list[ int ]`, etc.

### Nullable Types **[v0.5]**

Nullable types allow variables to hold either a value or `None` (null):

```python
# Nullable type annotations (type followed by ?)
result: int? = get_value()
optional_score: double? = None

# Non-nullable by default
exists: bool = False  # Cannot be None
count: int = 42      # Cannot be None

# Assigning None requires nullable type
value: int? = None   # OK
other: int = None    # ERROR: Cannot assign None to non-nullable type

# Type narrowing via 'is None' check
if result is not None:
    # Inside this block, 'result' is narrowed from 'int?' to 'int'
    print(result + 10)  # Safe: result is definitely int here
else:
    # result is still int? here
    print("No value")
```

### Type Narrowing **[v0.5]**

Sharpy performs type narrowing in conditional branches for nullable types and `isinstance` checks:

```python
# Nullable type narrowing
value: str? = get_optional_string()

if value is not None:
    # Inside this block, 'value' is narrowed from 'str?' to 'str'
    print(value.upper())  # OK - value is str, not str?
    print(len(value))     # OK - can call str methods
else:
    # value is still str? here
    print("No value provided")

# isinstance() narrowing
obj: object = get_value()

if isinstance(obj, str):
    # Inside this block, 'obj' is narrowed from 'object' to 'str'
    print(obj.upper())  # OK - compiler knows obj is str
elif isinstance(obj, int):
    # Inside this block, 'obj' is narrowed to 'int'
    print(obj + 1)  # OK - compiler knows obj is int

# Combining conditions
data: object? = get_data()

if data is not None and isinstance(data, str):
    # Both narrowing rules apply
    # data is narrowed from 'object?' to 'str'
    print(data.upper())
```

**Narrowing Rules in v0.5:**
- `is not None` narrows nullable type (`T?`) to non-nullable (`T`)
- `is None` narrows to never-type in the `if` branch (value is definitely None)
- `isinstance(x, Type)` narrows `x` to `Type` in the `if` branch
- Narrowing only affects the scope of the conditional block
- Combined with `and`, both conditions must hold for narrowing

**Not in v0.5:**
- Pattern matching narrowing (requires match statements - v1.0)

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

Also, variables declared inside the comprehension, specifically the variables holding the iterator's items on each iteration, e.g. `x` in `x for x in ...`, shadow any from the outer scope.

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

Sharpy modules map to .NET namespaces and static classes. Each `.spy` file is a module, and the file system structure determines the namespace hierarchy.

See [Type System - Modules](type_system.md#modules) for implementation details.

### Module Files **[v0.5]**

```python
# File: my_package/my_module.spy
# Maps to namespace: MyPackage.MyModule

"""Module-level docstring.

This describes the purpose and contents of the module.
"""

# Module-level constants
const VERSION: str = "1.0.0"
const MAX_SIZE: int = 1000

# Module-level variables (avoid in v0.5, prefer constants or class fields)
__counter: int = 0  # Private module variable

# Module-level functions
def helper_function(x: int) -> int:
    """Double the input value."""
    return x * 2

def public_api(data: str) -> str:
    """Public module function."""
    return data.upper()

# Classes
class MyClass:
    """A class in this module."""
    pass

class __PrivateClass:
    """A private class (leading double underscore)."""
    pass
```

**Module Structure Rules:**
- File name determines module name: `my_module.spy` -> `MyModule`
- Directory structure determines namespace: `src/utils/helpers.spy` where `src` is the root of the source directory -> `Utils.Helpers`
- Module docstring should be the first statement (triple-quoted string)

### Import Statements **[v0.5]**

#### Import Entire Module

```python
# Import the math module
import math

# Use with qualified name
result = math.sqrt(16.0)
pi_value = math.pi

# Import with alias
import math as m
result = m.sqrt(16.0)
```

**Syntax:**
```python
import module_name
import module_name as alias
```

#### Import Specific Names

```python
# Import specific functions/classes
from math import sqrt, pi, cos
result = sqrt(16.0)

# Import with aliases
from math import sqrt as square_root
result = square_root(16.0)

# Import multiple with mixed aliases
from collections import defaultdict, Counter as CounterClass
```

**Syntax:**
```python
from module_name import name1, name2, ...
from module_name import name as alias
```

#### Import All Public Names **[v0.5]**

```python
# Import all public names
from math import *

# Now all public names are directly available
result = sqrt(16.0)
pi_value = pi
```

**Warning:** Wildcard imports can cause name collisions and reduce code clarity. Use sparingly.

### Module Resolution **[v0.5]**

**Standard Library Modules:**
- Built-in .NET types: `System`, `System.Collections.Generic`, etc.
- Sharpy standard library: Available without imports for built-in types

**Project Modules:**
```
project/
    main.spy              # import utils.helpers
    utils/
        __init__.spy      # Optional, can be empty
        helpers.spy       # Defines helpers module
        math/
            __init__.spy
            vector.spy    # import utils.math.vector
```

**Import Search Order:**
1. Sharpy built-in types (list, dict, int, str, etc.)
2. .NET standard library (System.*, etc.)
3. Project modules (relative to source root)
4. Installed packages

### Namespace Mapping **[v0.5]**

Sharpy imports map to .NET namespaces:

```python
# Sharpy import
import system.collections.generic

# Maps to .NET using directive
using System.Collections.Generic;

# Sharpy import
from system.io import File, Directory

# Maps to .NET
using System.IO;
// File and Directory are used directly
```

**Naming Conventions:**
- Sharpy uses snake_case for module names: `my_module`
- .NET uses PascalCase for namespaces: `MyModule`
- Compiler automatically converts between conventions

### Circular Import Handling **[v0.5]**

Circular imports are resolved through forward declarations:

```python
# module_a.spy
from module_b import ClassB  # Forward reference

class ClassA:
    def use_b(self, b: ClassB) -> None:
        b.method()

# module_b.spy
from module_a import ClassA  # Forward reference

class ClassB:
    def use_a(self, a: ClassA) -> None:
        a.method()
```

**Rules:**
- Imports at module level create forward declarations
- Types are resolved after all modules are parsed
- Circular references allowed for type annotations
- Circular references allowed for instance usage (not for base classes)

### Special Imports **[v0.5]**

```python
# Import from .NET assemblies
from system import Console, Environment
from system.collections.generic import List, Dictionary

# Use .NET types directly
Console.write_line("Hello from .NET!")
env_vars = Environment.get_environment_variables()

# Import .NET generic types
from system.collections.generic import List

# Instantiate with type parameter
numbers: List[int] = List[int]()
numbers.add(42)
```

**Not in v0.5:**
- Relative imports (`.` and `..` syntax)
- Import hooks
- Dynamic imports
- Package management beyond .NET NuGet

## Classes **[v0.5]**

Classes are reference types that support single inheritance and multiple interface implementation.

See [Type System - Classes](type_system.md#classes-and-inheritance) for details on the type hierarchy.

### Basic Class Definition **[v0.5]**

Class members must be declared at the class level with their types:

```python
class Person:
    """A person with a name and age."""

    # Field declarations (required)
    name: str
    age: int

    # Constructor
    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    # Instance method
    def greet(self) -> str:
        return f"Hello, I'm {self.name}"

    # Instance method with no return value
    def celebrate_birthday(self) -> None:
        self.age += 1
```

**Class Definition Rules:**
- All instance fields must be declared at class level with type annotations
- Fields can have initializers at class level
- The `self` parameter is required for instance methods
- The `self` parameter is **not** type-annotated
- Methods must have return type annotations if they return a value
- Methods returning no value do not require a return type annotation, but may specify `-> None` for consistency

**Field Declaration Examples:**
```python
class Example:
    # Valid field declarations
    name: str = "default"
    count: int
    items: list[str]
    mapping: dict[str, int]
    optional: str?

    def __init__(self):
        self.count = 0
        self.items = []
        self.mapping = {}
        self.optional = None
```

### Constructor (`__init__`) **[v0.5]**

The constructor is always named `__init__` and has no return type (or `-> None`):

```python
class Person:
    name: str
    age: int

    # Constructor with no return type annotation
    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    # Or explicitly annotated as -> None
    def __init__(self, name: str, age: int) -> None:
        self.name = name
        self.age = age
```

**Constructor Rules:**
- Must be named `__init__`
- Return type is omitted or `-> None`
- Can be overloaded with different parameter signatures (see Constructor Overloading)
- Must initialize all non-nullable fields before the constructor returns

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

Structs are value types that do not support inheritance but can implement interfaces. They are allocated on the stack and passed by value.

See [Type System - Structs](type_system.md#structs-value-types) for runtime behavior.

### Struct Definition **[v0.5]**

```python
struct Vector2:
    """A 2D vector value type."""

    # Field declarations (required)
    x: double
    y: double

    # Constructor
    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    # Instance methods
    def magnitude(self) -> double:
        """Calculate the length of the vector."""
        return (self.x ** 2 + self.y ** 2) ** 0.5

    def normalized(self) -> Vector2:
        """Return a unit vector in the same direction."""
        mag = self.magnitude()
        if mag == 0:
            return Vector2(0, 0)
        return Vector2(self.x / mag, self.y / mag)

    # Operator overloading
    def __add__(self, other: Vector2) -> Vector2:
        """Add two vectors."""
        return Vector2(self.x + other.x, self.y + other.y)

    def __str__(self) -> str:
        """String representation."""
        return f"Vector2({self.x}, {self.y})"

# Usage
v1 = Vector2(3.0, 4.0)
v2 = Vector2(1.0, 2.0)
v3 = v1 + v2           # Vector2(4.0, 6.0)
mag = v1.magnitude()   # 5.0
```

**Struct Rules:**
- All fields must be declared at struct level with type annotations
- Must have a constructor that initializes all fields
- Cannot inherit from other structs or classes
- Can implement interfaces
- Value semantics: copied when assigned or passed to functions
- Stack-allocated (better performance for small types)

### Struct with Interfaces **[v0.5]**

```python
interface IEquatable[T]:
    def equals(self, other: T) -> bool: ...

struct Point(IEquatable[Point]):
    """A point that can be compared for equality."""

    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def equals(self, other: Point) -> bool:
        """Implement IEquatable interface."""
        return self.x == other.x and self.y == other.y

    def __eq__(self, other: object) -> bool:
        """Overload == operator."""
        if not isinstance(other, Point):
            return False
        return self.equals(other)

    def __hash__(self) -> int:
        """Hash for use in dictionaries."""
        return hash((self.x, self.y))

# Usage
p1 = Point(10, 20)
p2 = Point(10, 20)
p3 = Point(30, 40)

print(p1 == p2)        # True
print(p1.equals(p3))   # False
```

**When to Use Structs:**
- Small data structures (typically < 16 bytes)
- Immutable value types (like Vector2, Point, Color)
- Frequently allocated temporary objects
- Types that benefit from value semantics

**When to Use Classes Instead:**
- Large data structures
- Objects with identity (need reference equality)
- Objects that need inheritance
- Mutable shared state

## Interfaces **[v0.5]**

Interfaces define structural contracts that types must satisfy. They specify method signatures that implementing types must provide.

See [Type System - Interfaces](type_system.md#interfaces-and-interfaces) for type checking rules.

### Interface Definition **[v0.5]**

```python
interface IDrawable:
    """Interface for drawable objects."""

    def draw(self) -> None:
        """Draw the object."""
        ...

    def get_bounds(self) -> tuple[double, double, double, double]:
        """Get bounding box (x, y, width, height)."""
        ...

# Implementation in class
class Circle(IDrawable):
    """A circle that implements IDrawable."""

    radius: double
    x: double
    y: double

    def __init__(self, x: double, y: double, radius: double):
        self.x = x
        self.y = y
        self.radius = radius

    def draw(self) -> None:
        """Implement the draw method."""
        print(f"Drawing circle at ({self.x}, {self.y}) with radius {self.radius}")

    def get_bounds(self) -> tuple[double, double, double, double]:
        """Implement the get_bounds method."""
        return (self.x - self.radius, self.y - self.radius,
                self.radius * 2, self.radius * 2)

# Usage
shape: IDrawable = Circle(10.0, 20.0, 5.0)
shape.draw()
bounds = shape.get_bounds()
```

**Interface Rules:**
- All methods must have `...` (ellipsis) as body
- Methods cannot have implementations in v0.5
- All methods are implicitly abstract
- Implementing types must provide all methods
- Use `...` instead of `pass` for abstract methods

### Multiple Interfaces **[v0.5]**

```python
interface ISerializable:
    """Interface for serializable objects."""
    def serialize(self) -> str: ...

interface IComparable[T]:
    """Interface for comparable objects."""
    def compare_to(self, other: T) -> int: ...

class Person(ISerializable, IComparable[Person]):
    """A person implementing multiple interfaces."""

    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    def serialize(self) -> str:
        """Implement ISerializable."""
        return f"{self.name},{self.age}"

    def compare_to(self, other: Person) -> int:
        """Implement IComparable."""
        if self.age < other.age:
            return -1
        elif self.age > other.age:
            return 1
        else:
            return 0

# Usage
person = Person("Alice", 30)
json_str = person.serialize()
```

### Interface Inheritance **[v0.5]**

```python
interface ISerializable:
    """Base interface for serialization."""
    def serialize(self) -> str: ...

interface IJSONSerializable(ISerializable):
    """Extends ISerializable with JSON-specific methods."""
    def to_json(self) -> str: ...
    def from_json(self, json: str) -> None: ...

class User(IJSONSerializable):
    """A user that supports JSON serialization."""

    username: str
    email: str

    def __init__(self, username: str, email: str):
        self.username = username
        self.email = email

    def serialize(self) -> str:
        """Implement base ISerializable."""
        return f"{self.username}|{self.email}"

    def to_json(self) -> str:
        """Implement IJSONSerializable."""
        return f'{{"username": "{self.username}", "email": "{self.email}"}}'

    def from_json(self, json: str) -> None:
        """Implement IJSONSerializable."""
        # Parse JSON and update fields
        pass  # Simplified for example
```

### Generic Interfaces **[v0.5]**

```python
interface IContainer[T]:
    """Generic container interface."""
    def add(self, item: T) -> None: ...
    def get(self, index: int) -> T: ...
    def count(self) -> int: ...

class ListContainer[T](IContainer[T]):
    """A list-based container."""

    _items: list[T]

    def __init__(self):
        self._items = []

    def add(self, item: T) -> None:
        self._items.append(item)

    def get(self, index: int) -> T:
        return self._items[index]

    def count(self) -> int:
        return len(self._items)

# Usage
container: IContainer[str] = ListContainer[str]()
container.add("hello")
container.add("world")
print(container.count())  # 2
```

### Interface Type Checking **[v0.5]**

```python
def draw_all(shapes: list[IDrawable]) -> None:
    """Draw all shapes in the list."""
    for shape in shapes:
        shape.draw()

# Usage with different types that implement IDrawable
shapes: list[IDrawable] = [
    Circle(10.0, 20.0, 5.0),
    Rectangle(0.0, 0.0, 100.0, 50.0),
]
draw_all(shapes)

# Type checking with isinstance
def process_shape(obj: object) -> None:
    if isinstance(obj, IDrawable):
        obj.draw()  # Type narrowing applies
        bounds = obj.get_bounds()
```

**Not in v0.5:**
- Default interface implementations
- Static interface members
- Interface properties (use getter/setter methods instead)

```python
interface ILogger:
    def log(self, error: str) -> None: ...

    def log_error(self, error: str) -> None:
        """Default implementation provided."""
        self.log(f"ERROR: {error}")
```

## Properties **[v1.0]**

**Note:** Properties are not implemented in v0.5. Use public fields with methods for getters/setters in v0.5.

### v0.5 Alternative: Fields with Methods

In v0.5, use public fields and explicit getter/setter methods:

```python
class Temperature:
    """Temperature in v0.5 style."""

    # Public field (direct access)
    celsius: double

    def __init__(self, celsius: double = 0.0):
        self.celsius = celsius

    # Getter method
    def get_fahrenheit(self) -> double:
        return self.celsius * 9/5 + 32

    # Setter method
    def set_fahrenheit(self, value: double):
        self.celsius = (value - 32) * 5/9

# Usage
temp = Temperature(25.0)
print(temp.celsius)  # Direct field access: 25.0
f = temp.get_fahrenheit()  # Method call: 77.0
temp.set_fahrenheit(32.0)
print(temp.celsius)  # 0.0
```

For simple encapsulation, use naming conventions:

```python
class Person:
    # Public fields (direct access allowed)
    name: str
    age: int

    # Protected field (naming convention)
    _internal_id: int

    # Private field (naming convention)
    __secret: str

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age
        self._internal_id = 0
        self.__secret = "hidden"

    # Explicit getter
    def get_secret(self) -> str:
        return self.__secret

    # Explicit setter with validation
    def set_age(self, value: int):
        if value < 0:
            raise ValueError("Age cannot be negative")
        self.age = value
```

### v1.0 Properties (Not in v0.5)

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

Decorators modify the behavior of functions, methods, and classes. They are applied using the `@decorator` syntax and map to .NET attributes.

### Built-in Decorators **[v0.5]**

#### Method Decorators

```python
class Calculator:
    """A calculator class demonstrating method decorators."""

    _value: int

    def __init__(self):
        self._value = 0

    # Static method (no self parameter)
    @static
    def add(x: int, y: int) -> int:
        """Static method, no instance required."""
        return x + y

    # Override base class method
    @override
    def __str__(self) -> str:
        """Override object's __str__ method."""
        return f"Calculator(value={self._value})"

# Usage
result = Calculator.add(5, 3)  # Call static method
calc = str(Calculator())  # Call overridden method
```

**Method Decorator Reference:**

| Decorator | Description | .NET Equivalent |
|-----------|-------------|-----------------|
| `@static` | Static method (no instance) | `static` keyword |
| `@override` | Explicitly marks method override | `override` keyword |
| `@virtual` | Method can be overridden | `virtual` keyword |
| `@abstract` | Method must be implemented by subclass | `abstract` keyword |
| `@final` | Method cannot be overridden | `sealed` keyword |

#### Class Decorators

```python
# Sealed class (cannot be inherited)
@final
class SealedClass:
    """This class cannot be subclassed."""
    pass

# Abstract class (cannot be instantiated)
@abstract
class AbstractShape:
    """Base class for shapes."""

    @abstract
    def area(self) -> double:
        """Calculate area (must be implemented by subclass)."""
        pass

# Attempt to subclass sealed class
class Derived(SealedClass):  # ERROR: Cannot inherit from sealed class
    pass

# Attempt to instantiate abstract class
shape = AbstractShape()  # ERROR: Cannot instantiate abstract class
```

**Class Decorator Reference:**

| Decorator | Description | .NET Equivalent |
|-----------|-------------|-----------------|
| `@final` | Class cannot be inherited | `sealed` keyword |
| `@abstract` | Class cannot be instantiated | `abstract` keyword |

### Decorator Syntax **[v0.5]**

```python
# Single decorator
@decorator
def function():
    pass

# Multiple decorators (applied bottom-to-top)
@decorator1
@decorator2
@decorator3
def function():
    pass

# Decorators with arguments (v1.0+)
@decorator(arg1, arg2)
def function():
    pass
```

**Decorator Application Order:**

When multiple decorators are applied, they execute from bottom to top:

```python
@first
@second
@third
def example():
    pass

# Equivalent to:
# example = first(second(third(example)))
```

### Custom Decorators **[v1.0]**

Custom decorators can be defined in v1.0. In v0.5, use only built-in decorators.

**Not in v0.5:**
- Custom decorator definitions
- Decorators with arguments
- Property decorators (deferred to v1.0)
- Parameterized decorators

## Functions **[v0.5]**

### Function Definition **[v0.5]**

Functions are defined using the `def` keyword:

```python
# Basic function with return type
def greet(name: str) -> str:
    """Greet a person by name."""
    return f"Hello, {name}!"

# Function with no return value
def print_greeting(name: str) -> None:
    print(f"Hello, {name}!")

# Function with multiple parameters
def add(x: int, y: int) -> int:
    return x + y

# Function with default arguments
def power(base: double, exponent: double = 2.0) -> double:
    return base ** exponent

# Multiple return values via tuple
def min_max(values: list[int]) -> tuple[int, int]:
    return (min(values), max(values))

# Unpack multiple return values
minimum, maximum = min_max([1, 5, 3, 9, 2])
```

**Function Signature Rules:**
- All parameters must have type annotations
- Return type annotation is required if the function returns a value
- Return type annotation can be omitted if the function returns no value, or can use `-> None` for consistency
- Parameters are comma-separated
- Default parameter values must be compile-time constants or literals
- Parameters with defaults must come after parameters without defaults

**Parameter Types:**

```python
# Required parameters
def required(x: int, y: str) -> None:
    pass

# Optional parameters (with defaults)
def optional(x: int, y: str = "default") -> None:
    pass

# Cannot mix order
def invalid(x: int = 0, y: str) -> None:  # ERROR: Required after optional
    pass
```

### Function Overloading **[v0.5]**

Functions can be overloaded with different parameter signatures:

```python
# Overload by parameter count
def process(value: int) -> str:
    return f"Integer: {value}"

def process(value: int, multiplier: int) -> str:
    return f"Result: {value * multiplier}"

# Overload by parameter type
def process(value: str) -> str:
    return f"String: {value}"

def process(value: double) -> str:
    return f"Float: {value:.2f}"

# Usage - compiler selects correct overload
print(process(42))           # Calls process(int)
print(process(42, 2))        # Calls process(int, int)
print(process("hello"))      # Calls process(str)
print(process(3.14))         # Calls process(double)
```

**Overload Resolution Rules:**
- Based on parameter count and types
- Parameter names do not affect overload resolution
- Most specific match is chosen
- Ambiguous overloads cause compile error

```python
# ERROR: Ambiguous overloads (same signature)
def duplicate(x: int, y: int) -> None:
    pass

def duplicate(a: int, b: int) -> None:  # ERROR: Same signature as above
    pass
```

### Lambda Expressions **[v0.5]**

Lambda expressions create anonymous functions:

```python
# Single expression lambda
square = lambda x: x ** 2

# Multiple parameters
add = lambda x, y: x + y
multiply = lambda x, y, z: x * y * z

# Type inference from context
numbers: list[int] = [1, 2, 3, 4, 5]
evens = filter(lambda x: x % 2 == 0, numbers)
doubled = map(lambda x: x * 2, numbers)

# Lambda as function argument
def apply(value: int, func: (int) -> int) -> int:
    return func(value)

result = apply(5, lambda x: x * x)  # 25
```

**Lambda Syntax:**
```
lambda parameters: expression
```

**Lambda Rules:**
- Single expression only (no statements)
- No return statement (expression value is returned)
- Type annotations on parameters and return value can be omitted if they can be inferred from context
- Can capture variables from enclosing scope
- Expression is evaluated and returned

**Not in v0.5:**
- Multi-line lambdas
- Lambda with statements
- Explicit type annotations in lambda parameters and return type

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

Sharpy provides literal syntax for creating lists, dictionaries, sets, and tuples.

### List Literals **[v0.5]**

```python
# Empty list (type annotation required)
empty: list[int] = []

# List with elements
numbers = [1, 2, 3, 4, 5]
names = ["Alice", "Bob", "Charlie"]

# Mixed types require common type
values: list[object] = [1, "hello", 3.14]  # All are objects

# Explicit type annotation
scores: list[double] = [95.5, 87.0, 92.3]

# Nested lists
matrix: list[list[int]] = [
    [1, 2, 3],
    [4, 5, 6],
    [7, 8, 9]
]

# List with trailing comma (allowed)
colors = [
    "red",
    "green",
    "blue",
]
```

**List Literal Syntax:**
```
[element1, element2, ...]
```

**List Operations:**
```python
numbers = [1, 2, 3]

# Append element
numbers.append(4)      # [1, 2, 3, 4]

# Insert at index
numbers.insert(0, 0)   # [0, 1, 2, 3, 4]

# Remove element
numbers.remove(2)      # [0, 1, 3, 4]

# Index access
first = numbers[0]     # 0
last = numbers[-1]     # 4

# Slicing
subset = numbers[1:3]  # [1, 3]

# Length
count = len(numbers)   # 4

# Iteration
for num in numbers:
    print(num)
```

### Dictionary Literals **[v0.5]**

```python
# Empty dictionary (type annotation required)
empty: dict[str, int] = {}

# Dictionary with entries
ages = {"Alice": 30, "Bob": 25, "Charlie": 35}

# Explicit type annotation
scores: dict[str, double] = {
    "Alice": 95.5,
    "Bob": 87.0,
    "Charlie": 92.3
}

# Nested dictionaries
config: dict[str, dict[str, int]] = {
    "server": {"port": 8080, "timeout": 30},
    "database": {"port": 5432, "pool_size": 10}
}

# Dictionary with trailing comma (allowed)
settings = {
    "debug": True,
    "verbose": False,
}
```

**Dictionary Literal Syntax:**
```
{key1: value1, key2: value2, ...}
```

**Dictionary Operations:**
```python
mapping = {"a": 1, "b": 2}

# Add/update entry
mapping["c"] = 3       # {"a": 1, "b": 2, "c": 3}
mapping["a"] = 10      # {"a": 10, "b": 2, "c": 3}

# Access value
value = mapping["a"]   # 10

# Get with default
value = mapping.get("d", 0)  # 0 (key not found)

# Check key existence
has_key = "a" in mapping  # True

# Remove entry
del mapping["b"]       # {"a": 10, "c": 3}

# Iteration over keys
for key in mapping:
    print(f"{key}: {mapping[key]}")

# Iteration over items
for key, value in mapping.items():
    print(f"{key}: {value}")
```

### Set Literals **[v0.5]**

```python
# Empty set (special syntax)
empty_set: set[int] = {/}

# Set with elements
numbers = {1, 2, 3, 4, 5}
words = {"apple", "banana", "cherry"}

# Explicit type annotation
primes: set[int] = {2, 3, 5, 7, 11}

# Set with trailing comma (allowed)
colors = {
    "red",
    "green",
    "blue",
}

# Note: {} creates an empty dict, not a set
empty_dict: dict[str, int] = {}  # This is a dict
empty_set: set[int] = {/}        # This is a set
```

**Set Literal Syntax:**
```
{element1, element2, ...}  # Set with elements
{/}                         # Empty set
```

**Set Operations:**
```python
numbers = {1, 2, 3}

# Add element
numbers.add(4)         # {1, 2, 3, 4}

# Remove element
numbers.remove(2)      # {1, 3, 4}

# Check membership
has_three = 3 in numbers  # True

# Set operations
odds = {1, 3, 5}
evens = {2, 4, 6}
all_nums = odds | evens      # Union: {1, 2, 3, 4, 5, 6}
intersection = odds & evens  # Intersection: set()
difference = odds - evens    # Difference: {1, 3, 5}
```

### Tuple Literals **[v0.5]**

```python
# Empty tuple
empty: tuple[()] = ()

# Single element tuple (note the comma)
single = (42,)
single_str = ("hello",)

# Multiple elements
point = (10, 20)
color = (255, 128, 0)

# Explicit type annotation
coordinates: tuple[int, int] = (100, 200)
rgb: tuple[int, int, int] = (255, 128, 64)

# Tuples with different types
mixed: tuple[str, int, bool] = ("Alice", 30, True)

# Nested tuples
matrix: tuple[tuple[int, int], tuple[int, int]] = (
    (1, 2),
    (3, 4)
)

# Parentheses are optional (except for empty tuple)
point = 10, 20         # Same as (10, 20)
color = 255, 128, 0    # Same as (255, 128, 0)
```

**Tuple Literal Syntax:**
```
()                # Empty tuple
(element,)        # Single element (comma required)
(elem1, elem2)    # Multiple elements
elem1, elem2      # Multiple elements (parentheses optional)
```

**Tuple Operations:**
```python
point = (10, 20)

# Index access
x = point[0]       # 10 (as object?, needs cast)
y = point[1]       # 20 (as object?, needs cast)

# Unpacking
x, y = point       # x=10, y=20

# Length
size = len(point)  # 2

# Immutable - cannot modify
point[0] = 30      # ERROR: tuple is immutable

# Concatenation creates new tuple
extended = point + (30,)  # (10, 20, 30)
```

### Collection Mutability **[v0.5]**

| Collection | Mutable? | Notes |
|------------|----------|-------|
| `list[T]` | ✅ Yes | Can append, remove, modify elements |
| `dict[K, V]` | ✅ Yes | Can add, remove, modify entries |
| `set[T]` | ✅ Yes | Can add, remove elements |
| `tuple[...]` | ❌ No | Immutable, fixed-size |
| `str` | ❌ No | Immutable string |

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
```

**Creating Immutable Copies:**

```python
# List to tuple (immutable)
numbers = [1, 2, 3]
immutable_numbers = tuple(numbers)

# Tuple to list (mutable)
point = (10, 20)
mutable_point = list(point)
```

**Not in v0.5:**
- `frozenset[T]` - Immutable set type
- `bytes` and `bytearray` - Byte sequence types
- List/dict/set comprehensions (deferred to v1.0)

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

The walrus operator creates or assigns to a variable in the **enclosing scope**, not a new scope, and shadows any conflicting variable from the outer scope:

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

Classes can define special methods (dunder methods) to customize operator behavior. Dunder methods (double-underscore methods) map to .NET operator overloading.

### Arithmetic Operators **[v0.5]**

```python
class Vector:
    """A 2D vector with operator overloading."""

    x: double
    y: double

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    # Binary arithmetic operators
    def __add__(self, other: Vector) -> Vector:
        """Overload + operator."""
        return Vector(self.x + other.x, self.y + other.y)

    def __sub__(self, other: Vector) -> Vector:
        """Overload - operator."""
        return Vector(self.x - other.x, self.y - other.y)

    def __mul__(self, scalar: double) -> Vector:
        """Overload * operator for scalar multiplication."""
        return Vector(self.x * scalar, self.y * scalar)

    def __truediv__(self, scalar: double) -> Vector:
        """Overload / operator for scalar division."""
        return Vector(self.x / scalar, self.y / scalar)

    # Unary operators
    def __neg__(self) -> Vector:
        """Overload unary - operator."""
        return Vector(-self.x, -self.y)

    def __pos__(self) -> Vector:
        """Overload unary + operator."""
        return Vector(self.x, self.y)

    # String representation
    def __str__(self) -> str:
        """String representation for print()."""
        return f"Vector({self.x}, {self.y})"

# Usage
v1 = Vector(1.0, 2.0)
v2 = Vector(3.0, 4.0)
v3 = v1 + v2           # Vector(4.0, 6.0)
v4 = v1 - v2           # Vector(-2.0, -2.0)
v5 = v1 * 2.0          # Vector(2.0, 4.0)
v6 = v1 / 2.0          # Vector(0.5, 1.0)
v7 = -v1               # Vector(-1.0, -2.0)
print(v1)              # "Vector(1.0, 2.0)"
```

**Arithmetic Dunder Methods:**

| Operator | Dunder Method | Description |
|----------|---------------|-------------|
| `+` | `__add__(self, other)` | Addition |
| `-` | `__sub__(self, other)` | Subtraction |
| `*` | `__mul__(self, other)` | Multiplication |
| `/` | `__truediv__(self, other)` | True division |
| `//` | `__floordiv__(self, other)` | Floor division |
| `%` | `__mod__(self, other)` | Modulo |
| `**` | `__pow__(self, other)` | Exponentiation |
| `-x` | `__neg__(self)` | Unary negation |
| `+x` | `__pos__(self)` | Unary plus |

### Comparison Operators **[v0.5]**

```python
class Point:
    """A point in 2D space with comparison support."""

    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __eq__(self, other: object) -> bool:
        """Overload == operator."""
        if not isinstance(other, Point):
            return False
        return self.x == other.x and self.y == other.y

    def __ne__(self, other: object) -> bool:
        """Overload != operator."""
        return not self.__eq__(other)

    def __lt__(self, other: Point) -> bool:
        """Overload < operator (for sorting by distance from origin)."""
        self_dist = self.x ** 2 + self.y ** 2
        other_dist = other.x ** 2 + other.y ** 2
        return self_dist < other_dist

    def __le__(self, other: Point) -> bool:
        """Overload <= operator."""
        return self < other or self == other

    def __gt__(self, other: Point) -> bool:
        """Overload > operator."""
        return not self <= other

    def __ge__(self, other: Point) -> bool:
        """Overload >= operator."""
        return not self < other

# Usage
p1 = Point(1, 2)
p2 = Point(1, 2)
p3 = Point(2, 3)

print(p1 == p2)  # True
print(p1 != p3)  # True
print(p1 < p3)   # True (closer to origin)
print(p3 > p1)   # True
```

**Comparison Dunder Methods:**

| Operator | Dunder Method | Description |
|----------|---------------|-------------|
| `==` | `__eq__(self, other)` | Equal to |
| `!=` | `__ne__(self, other)` | Not equal to |
| `<` | `__lt__(self, other)` | Less than |
| `<=` | `__le__(self, other)` | Less than or equal |
| `>` | `__gt__(self, other)` | Greater than |
| `>=` | `__ge__(self, other)` | Greater than or equal |

**Note:** Only `__eq__` and `__lt__` are required. The others can be derived, but explicit implementations are recommended for performance.

### Container Operators **[v0.5]**

```python
class Grid:
    """A 2D grid with indexing support."""

    _data: list[list[int]]
    _width: int
    _height: int

    def __init__(self, width: int, height: int):
        self._width = width
        self._height = height
        self._data = [[0 for _ in range(width)] for _ in range(height)]

    def __getitem__(self, key: tuple[int, int]) -> int:
        """Overload indexing: value = grid[x, y]."""
        x, y = key
        if x < 0 or x >= self._width or y < 0 or y >= self._height:
            raise IndexError(f"Index ({x}, {y}) out of bounds")
        return self._data[y][x]

    def __setitem__(self, key: tuple[int, int], value: int) -> None:
        """Overload assignment: grid[x, y] = value."""
        x, y = key
        if x < 0 or x >= self._width or y < 0 or y >= self._height:
            raise IndexError(f"Index ({x}, {y}) out of bounds")
        self._data[y][x] = value

    def __contains__(self, value: int) -> bool:
        """Overload 'in' operator: value in grid."""
        for row in self._data:
            if value in row:
                return True
        return False

    def __len__(self) -> int:
        """Overload len() function."""
        return self._width * self._height

# Usage
grid = Grid(10, 10)
grid[5, 3] = 42        # Calls __setitem__
value = grid[5, 3]     # Calls __getitem__, returns 42
has_42 = 42 in grid    # Calls __contains__, returns True
size = len(grid)       # Calls __len__, returns 100
```

**Container Dunder Methods:**

| Syntax | Dunder Method | Description |
|--------|---------------|-------------|
| `obj[key]` | `__getitem__(self, key)` | Index access |
| `obj[key] = value` | `__setitem__(self, key, value)` | Index assignment |
| `del obj[key]` | `__delitem__(self, key)` | Index deletion |
| `value in obj` | `__contains__(self, value)` | Membership test |
| `len(obj)` | `__len__(self)` | Length |
| `iter(obj)` | `__iter__(self)` | Iterator |

### String Representation **[v0.5]**

```python
class Person:
    """A person with string representations."""

    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

    def __str__(self) -> str:
        """User-friendly string representation (for print, str)."""
        return f"{self.name}, age {self.age}"

    def __repr__(self) -> str:
        """Developer-friendly representation (for debugging)."""
        return f"Person(name='{self.name}', age={self.age})"

# Usage
person = Person("Alice", 30)
print(person)          # Uses __str__: "Alice, age 30"
print(repr(person))    # Uses __repr__: "Person(name='Alice', age=30)"
```

**String Representation Methods:**

| Function | Dunder Method | Purpose |
|----------|---------------|---------|
| `str(obj)` | `__str__(self)` | User-friendly string |
| `repr(obj)` | `__repr__(self)` | Developer/debug string |
| `print(obj)` | `__str__(self)` | Uses `__str__` if available |

### Callable Objects **[v1.0]**

```python
class Multiplier:
    """A callable object that multiplies by a factor."""

    factor: int

    def __init__(self, factor: int):
        self.factor = factor

    def __call__(self, value: int) -> int:
        """Make the object callable like a function."""
        return value * self.factor

# Usage
times_three = Multiplier(3)
result = times_three(10)  # Calls __call__, returns 30
```

**Callable Dunder Method:**

| Syntax | Dunder Method | Description |
|--------|---------------|-------------|
| `obj(args)` | `__call__(self, args)` | Makes object callable |

### Hash and Equality **[v0.5]**

For objects to be used as dictionary keys or in sets, they must implement `__hash__` and `__eq__`:

```python
class Coordinate:
    """An immutable coordinate that can be used as a dict key."""

    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __eq__(self, other: object) -> bool:
        """Equality comparison."""
        if not isinstance(other, Coordinate):
            return False
        return self.x == other.x and self.y == other.y

    def __hash__(self) -> int:
        """Hash value for use in dicts/sets."""
        return hash((self.x, self.y))

# Usage
locations: dict[Coordinate, str] = {}
coord = Coordinate(10, 20)
locations[coord] = "Home"  # Works because __hash__ and __eq__ defined
```

**Rules for Hashable Objects:**
- If `__eq__` is defined, `__hash__` must also be defined, and vice versa
- If `a == b`, then `hash(a) == hash(b)` must be true
- Hash value should not change during object lifetime
- Mutable objects should not implement `__hash__`

### Complete Dunder Method Reference **[v0.5]**

See [Type System - Dunder Methods](type_system.md#dunder-methods) for complete list of all available dunder methods.

**Not in v0.5:**
- Right-hand operator overloads (`__radd__`, `__rmul__`, etc.)
- In-place operator overloads (`__iadd__`, `__imul__`, etc.)
- Context manager protocol (`__enter__`, `__exit__`)
- Async dunder methods (`__aiter__`, `__anext__`)

## Type Aliases and Enums **[v0.5]**

### Type Aliases **[v1.0]**

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
y = None                         # ERROR - cannot infer T from implied T? type from None assignment

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
- Enums must have at least one variant (empty enums are not allowed)
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

## Statements **[v0.5]**

Statements are instructions that perform actions. Unlike expressions, statements do not evaluate to a value.

### Statement Types **[v0.5]**

Sharpy supports several categories of statements:

1. **Simple Statements** - Single-line statements
2. **Compound Statements** - Multi-line statements with blocks
3. **Declaration Statements** - Define variables, constants, functions, classes

### Simple Statements **[v0.5]**

#### Expression Statements

Any expression can be a statement:

```python
# Function calls
print("Hello")
calculate_total()
obj.method()

# Assignments (see below)
x = 42

# Method calls with side effects
list.append(item)
dict.clear()
```

#### Assignment Statements **[v0.5]**

```python
# Simple assignment
x = 10
name = "Alice"

# Multiple assignment (unpacking)
x, y = 10, 20
first, second = get_pair()

# Tuple unpacking
point = (10, 20)
x, y = point

# List unpacking
values = [1, 2, 3]
a, b, c = values

# Augmented assignment
x += 5              # x = x + 5
count *= 2          # count = count * 2
total -= amount     # total = total - amount
value //= 3         # value = value // 3
flags |= new_flag   # flags = flags | new_flag

# Chained assignment
x = y = z = 0       # All assigned to 0
```

**Assignment Rules:**
- Left side must be a valid target (variable, index, attribute)
- Right side is evaluated first
- Type must match for existing variables
- Shadowing requires explicit type annotation
- Cannot assign to literals or expressions

#### Declaration Statements **[v0.5]**

```python
# Variable declaration (without assignment)
x: int
name: str
values: list[int]

# Variable declaration with assignment
x: int = 42
name: str = "Alice"
values: list[int] = []

# Type inference
x = 42              # Inferred as int
name = "Alice"      # Inferred as str

# Constant declaration
const PI: double = 3.14159
const MAX_SIZE = 1000  # Inferred as int
```

#### Assert Statements **[v0.5]**

```python
# Simple assertion
assert condition

# Assertion with message
assert x > 0, "Value must be positive"
assert len(items) > 0, "List cannot be empty"

# Multiple conditions
assert is_valid and is_ready, "Invalid state"
```

#### Pass Statement **[v0.5]**

Null statement that does nothing (placeholder):

```python
# Empty function
def todo():
    pass

# Empty class
class Placeholder:
    pass

# Empty exception handler
try:
    risky_operation()
except Exception:
    pass  # Ignore errors
```

#### Break and Continue **[v0.5]**

Loop control statements:

```python
# break - exit loop
for item in items:
    if item == target:
        break  # Exit the loop

# continue - skip to next iteration
for i in range(10):
    if i % 2 == 0:
        continue  # Skip even numbers
    print(i)
```

**Rules:**
- `break` exits the innermost loop
- `continue` skips to next iteration of innermost loop
- Only valid inside loops (`for`, `while`)
- Not valid in v0.5 to break out of nested loops

#### Return Statement **[v0.5]**

```python
# Return with value
def add(x: int, y: int) -> int:
    return x + y

# Return without value (None)
def print_message() -> None:
    print("Hello")
    return  # Optional

# Return tuple (multiple values)
def get_coordinates() -> tuple[int, int]:
    return (10, 20)

# Early return
def find_item(items: list[str], target: str) -> int:
    for i, item in enumerate(items):
        if item == target:
            return i  # Early return
    return -1  # Not found
```

**Return Rules:**
- Must match function return type
- Functions with `-> None` can omit `return`
- Functions with return type must return on all code paths
- Can return early from anywhere in function

#### Raise Statement **[v0.5]**

```python
# Raise exception with message
raise ValueError("Invalid input")

# Raise exception instance
raise ArgumentException("parameter", "Invalid value")

# Re-raise current exception
except Exception as e:
    log_error(e)
    raise  # Re-raise same exception

# Raise new exception from original
except IOError as e:
    raise RuntimeError("Failed to process") from e
```

### Compound Statements **[v0.5]**

Statements that contain other statements (blocks).

#### If Statement **[v0.5]**

```python
# Simple if
if condition:
    statement

# If-else
if condition:
    statement
else:
    statement

# If-elif-else
if condition1:
    statement1
elif condition2:
    statement2
elif condition3:
    statement3
else:
    statement4
```

**If Statement Rules:**
- Condition must be boolean expression
- Each block must be indented (4 spaces)
- `elif` and `else` are optional
- Evaluated top-to-bottom, first true condition executes

#### While Statement **[v0.5]**

```python
# Basic while loop
while condition:
    statements

# While with else
while condition:
    statements
else:
    # Executed if loop completes without break
    statements

# Infinite loop
while True:
    if should_exit:
        break
```

#### For Statement **[v0.5]**

```python
# Iterate over collection
for item in collection:
    statements

# For with range
for i in range(10):
    statements

# For with else
for item in items:
    if item == target:
        break
else:
    # Executed if loop completes without break
    print("Not found")

# For with enumerate
for index, value in enumerate(items):
    print(f"{index}: {value}")
```

**For Statement Rules:**
- Loop variable automatically declared
- Collection must be iterable
- `else` clause optional
- `break` and `continue` work as expected

#### Try Statement **[v0.5]**

```python
# Try-except
try:
    statements
except ExceptionType:
    handler

# Try-except with binding
try:
    statements
except ExceptionType as e:
    handler

# Multiple except clauses
try:
    statements
except ValueError as e:
    handler1
except TypeError as e:
    handler2
except Exception as e:
    handler3

# Try-except-else
try:
    statements
except Exception as e:
    handler
else:
    # Executed if no exception
    statements

# Try-except-finally
try:
    statements
except Exception as e:
    handler
finally:
    # Always executed
    cleanup

# Try-except-else-finally
try:
    statements
except Exception as e:
    handler
else:
    success_statements
finally:
    cleanup
```

**Try Statement Rules:**
- At least one `except` clause required
- `else` only runs if no exception occurred
- `finally` always runs (even with return/break)
- Exceptions checked in order, first match handles
- Bare `raise` only valid in except clause

### Declaration Statements **[v0.5]**

#### Function Definition **[v0.5]**

```python
def function_name(param1: type1, param2: type2) -> return_type:
    """Docstring."""
    statements
    return value
```

#### Class Definition **[v0.5]**

```python
class ClassName:
    """Class docstring."""

    # Field declarations
    field1: type1
    field2: type2

    # Methods
    def method(self) -> return_type:
        statements
```

#### Struct Definition **[v0.5]**

```python
struct StructName:
    """Struct docstring."""

    # Field declarations
    field1: type1
    field2: type2

    # Methods
    def method(self) -> return_type:
        statements
```

#### Interface Definition **[v0.5]**

```python
interface IInterfaceName:
    """Interface docstring."""

    def method(self, param: type) -> return_type:
        ...
```

#### Enum Definition **[v0.5]**

```python
enum EnumName:
    """Enum docstring."""

    VALUE1 = 1
    VALUE2 = 2
    VALUE3 = 3
```

### Statement Execution Order **[v0.5]**

**General Rules:**
1. Statements execute top-to-bottom in sequence
2. Control flow statements (`if`, `while`, `for`) can alter order
3. `break` exits innermost loop
4. `continue` skips to next iteration
5. `return` exits function immediately
6. `raise` transfers to exception handler
7. Block statements execute their bodies

**Not in v0.5:**
- `with` statement (context managers)
- `match` statement (pattern matching)
- `del` statement (collection item deletion)
- `yield` statement (generators)
- `async`/`await` statements
- Global/nonlocal declarations

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

### Conditional Statements **[v0.5]**

```python
# Basic if statement
if x > 0:
    print("positive")

# if/elif/else chain
if x > 0:
    print("positive")
elif x < 0:
    print("negative")
else:
    print("zero")

# Multiple elif branches
if score >= 90:
    grade = "A"
elif score >= 80:
    grade = "B"
elif score >= 70:
    grade = "C"
elif score >= 60:
    grade = "D"
else:
    grade = "F"

# Nested if statements
if user is not None:
    if user.is_active:
        print("Active user")
    else:
        print("Inactive user")

# Conditional expression (ternary operator)
result = "even" if x % 2 == 0 else "odd"

# Conditional expressions can be nested (though readability suffers)
category = "small" if x < 10 else ("medium" if x < 100 else "large")
```

**If Statement Syntax:**
```
if condition:
    statements
[elif condition:
    statements]*
[else:
    statements]
```

**Conditional Expression Syntax:**
```
true_value if condition else false_value
```

**Rules:**
- Condition must be a boolean expression
- Each branch must be indented (4 spaces)
- `elif` and `else` are optional
- Parentheses around condition are optional (but allowed)
- Conditional expressions evaluate to a value

### Loops **[v0.5]**

#### While Loops

```python
# Basic while loop
count = 0
while count < 10:
    print(count)
    count += 1

# While with condition using nullable types
current: Node? = head
while current is not None:
    print(current.value)
    current = current.next  # Type narrowing in effect

# Infinite loop (requires break to exit)
while True:
    command = input("Enter command: ")
    if command == "quit":
        break
    process(command)
```

**While Loop Syntax:**
```
while condition:
    statements
[else:
    statements]
```

#### For Loops

```python
# For loop with range (exclusive upper bound)
for i in range(10):           # 0 through 9
    print(i)

# Range with start and stop
for i in range(5, 10):        # 5 through 9
    print(i)

# Range with step
for i in range(0, 10, 2):     # 0, 2, 4, 6, 8
    print(i)

# Iterate over list
names: list[str] = ["Alice", "Bob", "Charlie"]
for name in names:
    print(name)

# Iterate over dictionary keys
scores: dict[str, int] = {"Alice": 95, "Bob": 87}
for name in scores:
    print(f"{name}: {scores[name]}")

# Enumerate for index and value
for index, name in enumerate(names):
    print(f"{index}: {name}")
```

**For Loop Syntax:**
```
for variable in iterable:
    statements
[else:
    statements]
```

**Iterable Types:**
- `range(stop)` or `range(start, stop)` or `range(start, stop, step)`
- Lists: `list[T]`
- Dictionaries: `dict[K, V]` (iterates over keys)
- Sets: `set[T]`
- Strings: `str` (iterates over characters)
- Tuples: `tuple[...]`

#### Loop Control **[v0.5]**

```python
# break - exit loop immediately
for i in range(100):
    if i == 50:
        break  # Exit the loop
    print(i)

# continue - skip to next iteration
for i in range(10):
    if i % 2 == 0:
        continue  # Skip even numbers
    print(i)  # Only prints odd numbers

# else clause - executes if loop completes without break
found = False
for item in items:
    if item == target:
        found = True
        break
else:
    # This runs only if break was NOT executed
    print("Target not found")

# else clause with while
attempts = 0
while attempts < 3:
    password = input("Enter password: ")
    if check_password(password):
        break
    attempts += 1
else:
    # Executes if all attempts failed (no break)
    print("Too many failed attempts")
```

**Loop Control Keywords:**
- `break`: Exit the innermost enclosing loop immediately
- `continue`: Skip to the next iteration of the innermost loop
- `else`: Executed when loop completes normally (without `break`)

**Rules:**
- `break` and `continue` only valid inside loops
- `break` exits only the innermost loop (no labeled breaks in v0.5)
- `else` clause is optional
- `else` clause skipped if loop exits via `break`

### Pattern Matching **[v1.0]**

Match statements are not available in v0.5. Use if/elif/else chains instead:

```python
# v0.5 approach using if/elif/else
def describe(value: object) -> str:
    if isinstance(value, int):
        if value == 0:
            return "zero"
        elif value == 1:
            return "one"
        else:
            return "other integer"
    elif isinstance(value, str):
        return f"string: {value}"
    else:
        return "unknown type"
```

**Not in v0.5:**
- `match` statements
- Pattern matching syntax
- Destructuring in match cases
- Guard clauses in patterns

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

Sharpy provides exception handling through try/except/finally blocks that map directly to .NET exception handling.

### Try/Except Blocks **[v0.5]**

```python
# Basic exception handling
try:
    result = risky_operation()
    print(f"Success: {result}")
except Exception as e:
    print(f"Error occurred: {e}")

# Multiple exception handlers
try:
    value = int(input("Enter a number: "))
    result = 100 / value
except ValueError as e:
    print(f"Invalid number: {e}")
except ZeroDivisionError as e:
    print(f"Cannot divide by zero: {e}")
except Exception as e:
    print(f"Unexpected error: {e}")

# Exception handler without binding
try:
    risky_operation()
except ValueError:
    print("Got a ValueError")
except (TypeError, KeyError):
    print("Got TypeError or KeyError")
```

**Try/Except Syntax:**
```
try:
    statements
except ExceptionType [as variable]:
    statements
[except ExceptionType [as variable]:
    statements]*
[else:
    statements]
[finally:
    statements]
```

### Finally Blocks **[v0.5]**

```python
# Finally always executes
try:
    file = open("data.txt", "r")
    data = file.read()
    process(data)
except FileNotFoundError:
    print("File not found")
finally:
    # Always executes, even if exception occurs
    if file is not None:
        file.close()

# Finally with successful execution
try:
    result = compute()
except Exception as e:
    print(f"Error: {e}")
finally:
    # Executes after try or except completes
    cleanup_resources()
```

**Finally Rules:**
- Executes after try block completes successfully
- Executes after except block handles exception
- Executes even if exception is not caught
- Executes even if return/break/continue in try/except

### Else Blocks **[v0.5]**

```python
# Else executes if no exception occurs
try:
    result = safe_operation()
except Exception as e:
    print(f"Error: {e}")
else:
    # Only runs if no exception in try block
    print(f"Success: {result}")
finally:
    cleanup()

# Common pattern for file processing
try:
    file = open("data.txt", "r")
except FileNotFoundError:
    print("File not found")
else:
    # File opened successfully
    data = file.read()
    process(data)
    file.close()
```

**Else Rules:**
- Executes only if try block completes without exception
- Executes before finally block
- Not executed if exception occurs (even if caught)

### Raising Exceptions **[v0.5]**

```python
# Raise built-in exception
def validate_age(age: int) -> None:
    if age < 0:
        raise ValueError("Age cannot be negative")
    if age > 150:
        raise ValueError("Age is unrealistic")

# Raise with exception instance
def check_value(value: int) -> None:
    if value < 0:
        raise ValueError(f"Invalid value: {value}")

# Re-raise current exception
try:
    risky_operation()
except Exception as e:
    log_error(e)
    raise  # Re-raise the same exception

# Raise different exception while preserving original
try:
    parse_data(data)
except ValueError as e:
    raise RuntimeError("Failed to parse data") from e
```

**Raise Syntax:**
```
raise ExceptionType(message)
raise exception_instance
raise  # Re-raise current exception
raise NewException(...) from original_exception
```

### Exception Types **[v0.5]**

Sharpy uses .NET exception types directly:

```python
# Common .NET exceptions
from system import (
    Exception,           # Base exception
    ArgumentException,   # Invalid argument
    ArgumentNullException,  # Null argument
    InvalidOperationException,  # Invalid state
    NotImplementedException,  # Not implemented
    NotSupportedException,  # Not supported
)

from system.io import (
    IOException,         # I/O error
    FileNotFoundException,  # File not found
    DirectoryNotFoundException,  # Directory not found
)

# Using .NET exceptions
def read_file(path: str) -> str:
    if path is None:
        raise ArgumentNullException("path")
    if path == "":
        raise ArgumentException("Path cannot be empty", "path")

    try:
        return File.ReadAllText(path)
    except FileNotFoundException as e:
        raise IOException(f"Cannot read file: {path}") from e
```

**Common Exception Hierarchy:**
- `Exception` - Base class for all exceptions
  - `SystemException` - System-level exceptions
    - `ArgumentException` - Invalid argument value
      - `ArgumentNullException` - Null argument
      - `ArgumentOutOfRangeException` - Argument out of range
    - `InvalidOperationException` - Invalid state for operation
    - `NotSupportedException` - Operation not supported
    - `NotImplementedException` - Not yet implemented
  - `IOException` - I/O errors
    - `FileNotFoundException` - File not found
    - `DirectoryNotFoundException` - Directory not found

### Custom Exceptions **[v0.5]**

```python
# Define custom exception
class ValidationError(Exception):
    """Raised when validation fails."""

    message: str
    field_name: str

    def __init__(self, message: str, field_name: str):
        self.message = message
        self.field_name = field_name
        super().__init__(f"{field_name}: {message}")

# Use custom exception
def validate_username(username: str) -> None:
    if len(username) < 3:
        raise ValidationError("Username too short", "username")
    if not username.isalnum():
        raise ValidationError("Username must be alphanumeric", "username")

# Catch custom exception
try:
    validate_username("ab")
except ValidationError as e:
    print(f"Validation failed: {e.message} (field: {e.field_name})")
```

**Custom Exception Rules:**
- Must inherit from `Exception` or a subclass
- Should call `super().__init__(message)` in constructor
- Can add custom fields and methods
- Follow .NET exception naming: end with "Exception"

### Exception Best Practices **[v0.5]**

```python
# Catch specific exceptions, not general Exception
try:
    value = int(user_input)
except ValueError:  # Good: specific exception
    print("Invalid number")

# Avoid catching Exception unless necessary
try:
    operation()
except Exception:  # Avoid: too broad
    pass

# Use finally for cleanup
file: File? = None
try:
    file = File.Open("data.txt")
    process(file)
finally:
    if file is not None:
        file.close()

# Provide context in exception messages
def withdraw(amount: double) -> None:
    if amount > self.balance:
        raise InvalidOperationException(
            f"Insufficient funds: requested {amount}, balance {self.balance}"
        )
```

**Not in v0.5:**
- Exception groups
- Exception notes
- `except*` syntax

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

## Built-in Functions **[v0.5]**

Sharpy provides a set of built-in functions that are always available without imports.

### Type Conversion Functions **[v0.5]**

```python
# Integer conversion
int("42")           # 42
int(3.14)           # 3
int(True)           # 1

# Float conversion
double("3.14")      # 3.14
double(42)          # 42.0
double(True)        # 1.0

# String conversion
str(42)             # "42"
str(3.14)           # "3.14"
str(True)           # "True"
str([1, 2, 3])      # "[1, 2, 3]"

# Boolean conversion
bool(1)             # True
bool(0)             # False
bool("")            # False
bool("text")        # True
bool([])            # False
bool([1])           # True

# Collection conversions
list((1, 2, 3))     # [1, 2, 3]
tuple([1, 2, 3])    # (1, 2, 3)
set([1, 2, 2, 3])   # {1, 2, 3}
```

### Type Checking Functions **[v0.5]**

```python
# Type checking
isinstance(obj, type)           # Check if obj is instance of type
isinstance(42, int)             # True
isinstance("hello", str)        # True
isinstance([1, 2], list)        # True

# Multiple types
isinstance(value, (list[int], set[int]))   # True if list[int] or set[int]

# Type information
type(obj)                       # Get type of object
type(42)                        # <class 'int'>
type("hello")                   # <class 'str'>
```

### Collection Functions **[v0.5]**

```python
# Length
len(collection)                 # Number of items
len([1, 2, 3])                 # 3
len("hello")                   # 5
len({"a": 1, "b": 2})          # 2

# Min/Max
min(iterable)                  # Smallest item
max(iterable)                  # Largest item
min([3, 1, 4, 2])             # 1
max([3, 1, 4, 2])             # 4

# Sum
sum(iterable)                  # Sum of all items
sum([1, 2, 3, 4])             # 10

# Sorting
sorted(iterable)               # Return sorted list
sorted([3, 1, 4, 2])          # [1, 2, 3, 4]
sorted(items, reverse=True)    # Descending order

# Reversed
reversed(sequence)             # Reverse iterator
list(reversed([1, 2, 3]))     # [3, 2, 1]

# Enumerate
enumerate(iterable)            # Iterator of (index, value) pairs
list(enumerate(["a", "b"]))   # [(0, "a"), (1, "b")]
for i, val in enumerate(items):
    print(f"{i}: {val}")

# Zip
zip(iter1, iter2)              # Combine iterables
list(zip([1, 2], ["a", "b"])) # [(1, "a"), (2, "b")]

# Range
range(stop)                    # 0 to stop-1
range(start, stop)             # start to stop-1
range(start, stop, step)       # With step
list(range(5))                 # [0, 1, 2, 3, 4]
list(range(2, 7))             # [2, 3, 4, 5, 6]
list(range(0, 10, 2))         # [0, 2, 4, 6, 8]

# Filter
filter(predicate, iterable)    # Filter items
evens = filter(lambda x: x % 2 == 0, [1, 2, 3, 4])
list(evens)                    # [2, 4]

# Map
map(function, iterable)        # Transform items
doubled = map(lambda x: x * 2, [1, 2, 3])
list(doubled)                  # [2, 4, 6]

# All/Any
all(iterable)                  # True if all items truthy
any(iterable)                  # True if any item truthy
all([True, True, True])        # True
any([False, False, True])      # True
```

### I/O Functions **[v0.5]**

```python
# Print to console
print(value)                   # Print value
print(val1, val2, val3)       # Print multiple values
print("Name:", name)           # Print with label
print(f"Value: {x}")          # Print f-string

# Input from console
input()                        # Read line from stdin
input("Enter name: ")         # Read with prompt
name = input("Name: ")        # Store input
```

### Mathematical Functions **[v0.5]**

```python
# Absolute value
abs(-5)                        # 5
abs(3.14)                      # 3.14

# Power
pow(2, 3)                      # 8 (2 ** 3)
pow(10, 2)                     # 100

# Round
round(3.14159)                 # 3
round(3.14159, 2)             # 3.14

# Division
divmod(10, 3)                  # (3, 1) - quotient and remainder
```

### Object Functions **[v0.5]**

```python
# Hash value
hash(obj)                      # Hash code for object
hash("hello")                  # Integer hash

# String representation
str(obj)                       # User-friendly string (__str__)
repr(obj)                      # Developer string (__repr__)
```

### Utility Functions **[v0.5]**

```python
# Object comparison
hash(obj)                      # Hash code (for dicts/sets)

# Assertions (see Assertions section)
assert condition
assert condition, "message"
```

### Built-in Constants **[v0.5]**

```python
# Boolean constants
True
False

# None
None

# Not a Number / Infinity (from math module)
# (These are in math module, not built-in in v0.5)
```

### Function Reference Table **[v0.5]**

| Function | Purpose | Example |
|----------|---------|---------|
| `int()` | Convert to integer | `int("42")` → `42` |
| `double()` | Convert to double | `double(42)` → `42.0` |
| `str()` | Convert to string | `str(42)` → `"42"` |
| `bool()` | Convert to boolean | `bool(1)` → `True` |
| `len()` | Get length | `len([1,2,3])` → `3` |
| `min()` | Minimum value | `min([1,2,3])` → `1` |
| `max()` | Maximum value | `max([1,2,3])` → `3` |
| `sum()` | Sum of values | `sum([1,2,3])` → `6` |
| `abs()` | Absolute value | `abs(-5)` → `5` |
| `round()` | Round number | `round(3.7)` → `4` |
| `sorted()` | Sort collection | `sorted([3,1,2])` → `[1,2,3]` |
| `reversed()` | Reverse sequence | `list(reversed([1,2]))` → `[2,1]` |
| `enumerate()` | Index+value pairs | `enumerate(["a","b"])` |
| `zip()` | Combine iterables | `zip([1,2], ["a","b"])` |
| `range()` | Number sequence | `range(5)` → 0..4 |
| `filter()` | Filter items | `filter(pred, items)` |
| `map()` | Transform items | `map(func, items)` |
| `all()` | All truthy | `all([True,True])` → `True` |
| `any()` | Any truthy | `any([False,True])` → `True` |
| `isinstance()` | Type check | `isinstance(x, int)` |
| `type()` | Get type | `type(42)` → `<class 'int'>` |
| `print()` | Output to console | `print("hello")` |
| `input()` | Read from console | `input("Name: ")` |
| `hash()` | Get hash code | `hash("text")` |
| `id()` | Object identity | `id(obj)` |
| `hasattr()` | Has attribute | `hasattr(obj, "x")` |
| `getattr()` | Get attribute | `getattr(obj, "x")` |
| `setattr()` | Set attribute | `setattr(obj, "x", 10)` |

**Note:** Additional mathematical functions (sin, cos, sqrt, etc.) are available in the `math` module, not as built-ins.

**Not in v0.5:**
- `open()` for file I/O (use .NET `System.IO` instead)

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
