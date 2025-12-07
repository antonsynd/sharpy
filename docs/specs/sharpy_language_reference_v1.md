# Sharpy Language Reference

## Introduction

Sharpy is a modern, statically-typed Pythonic language targeting .NET. While Python code will not run in Sharpy without modifications, the additions and changes in Sharpy over Python should be intuitive to and welcomed by all Python developers.

### Goals

* Provide a statically-typed and modern Pythonic language for the .NET CLI
* Seamless bidirectional interop with other .NET libraries
* Target C# 9.0 for maximum compatibility (Unity, .NET 5+)

### Guiding Principles

These principles are ordered in descending order of importance and should guide decisions when conflicts or ambiguities arise:

1. Sharpy is a .NET language at its core, inheriting and preferring design choices from the .NET CLI
2. Sharpy is a Pythonic language second, inheriting syntax, semantics, and standard library where possible from Python
3. Where the preceding two principles conflict, a preference for .NET will prevail, unless the conflict can be resolved within the compiler as intrinsics or clear, predictable, implicit conversions at .NET ABI boundaries, with zero-cost abstractions

### Philosophy

Sharpy believes that static typing is key to writing safe, predictable, and performant programs, at both the development stage and at runtime.

---

## Version Guide

Features are marked with their target version. Each version builds upon the previous, with core features in earlier versions and ergonomic/advanced features in later versions.

| Version | Focus Area | Key Features |
|---------|------------|--------------|
| **v0.1** | Core Language | Basic types, functions, classes, control flow, exceptions |
| **v0.2** | Nullability & Collections | Nullable types, `?.`, `??`, list/dict/set/tuple |
| **v0.3** | Structs, Interfaces, OOP | Structs, interfaces, inheritance, decorators |
| **v0.4** | Generics | Generic classes/structs/interfaces/methods, constraints |
| **v0.5** | Enums & Operators | Simple enums, operator overloading via dunders |
| **v0.6** | Extended Syntax | F-strings, extended literals, comparison chaining |
| **v0.7** | Pattern Matching | Match statements, patterns, guards |
| **v0.8** | Type Aliases & ADTs | Type aliases, tagged unions |
| **v0.9** | Comprehensions & Properties | List/dict/set comprehensions, walrus, properties |
| **v1.0** | Resources & Async | Context managers, defer, events, async/await |
| **v2.0+** | Future | Features requiring C# 11+ or .NET 7+ |

### Target Compatibility

| Sharpy Version | Target Runtime | C# Version | Notes |
|----------------|----------------|------------|-------|
| **v0.1 - v1.0** | .NET 5+ / Unity | C# 9.0 | Maximum compatibility |
| **v2.0+** | .NET 7+ | C# 11+ | Full modern features |

### Transpilation Legend

Throughout this document, implementation notes use these indicators:

| Status | Meaning |
|--------|---------|
| ✅ **Native** | Maps directly to C# 9.0 |
| 🔄 **Lowered** | Requires compiler transformation |
| ❌ **v2.0** | Requires C# 11+ / .NET 7+; deferred |

---

## Lexical Structure **[v0.1]**

### Source Files

- File extension: `.spy`
- Encoding: UTF-8 (required)
- Line endings: LF (`\n`) or CRLF (`\r\n`)
- Byte Order Mark (BOM): Optional but not recommended

*Implementation: ✅ Native - Source encoding handled by .NET's text processing.*

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

*Implementation: 🔄 Lowered - The lexer tracks indentation levels via an indentation stack, emitting INDENT/DEDENT tokens. These are converted to C# braces `{ }` during code generation.*

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

*Implementation: ✅ Native - Converted to `//` comments in C#. Docstrings become `///` XML documentation or are discarded.*

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

| Type | Convention | Example | C# Transformation |
|------|------------|---------|-------------------|
| Local variable | `snake_case` | `user_name` | `userName` (camelCase) |
| Function/method | `snake_case` | `calculate_total` | `CalculateTotal` (PascalCase) |
| Class | `PascalCase` | `UserAccount` | (unchanged) |
| Struct | `PascalCase` | `Vector2` | (unchanged) |
| Interface | `IPascalCase` | `IDrawable` | (unchanged) |
| Constant | `CAPS_SNAKE_CASE` | `MAX_SIZE` | (unchanged) |
| Module | `snake_case` | `user_service` | `UserService` (PascalCase namespace) |
| Enum type | `PascalCase` | `Color` | (unchanged) |
| Enum value | `CAPS_SNAKE_CASE` | `RED` | `Red` (PascalCase) |

### Literal Names (Backtick Escaping)

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

*Implementation: ✅ Native - Backtick names map to `@name` in C# when needed, or exact casing is preserved.*

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

### Newline Significance

Newlines are significant in Sharpy and terminate statements:

**Rules:**
1. Newlines are significant and terminate statements
2. Except inside brackets `()`, `[]`, `{}`
3. Except after backslash `\` continuation
4. Newlines inside string literals are literal newlines

```python
# Newline terminates statement
x = 42
y = 10

# Implicit continuation in brackets
result = (
    value1 +
    value2
)

# Newline inside string literal is literal newline
multi = """Line 1
Line 2"""
```

*Implementation: ✅ Native - Continuation is handled at lex time; resulting logical lines transpile normally.*

---

## Keywords **[v0.1]**

### Hard Keywords

The following are reserved keywords in Sharpy v0.1-v1.0:

| Keyword | Version | Notes |
|---------|---------|-------|
| `and` | v0.1 | Boolean AND |
| `as` | v0.1 | Aliasing for imports |
| `assert` | v0.1 | Assertion statement |
| `auto` | v0.8 | Inferred type for shadowing |
| `break` | v0.1 | Break statement for loops |
| `case` | v0.7 | Pattern matching case |
| `class` | v0.1 | Class declaration |
| `const` | v0.1 | Constant declaration |
| `continue` | v0.1 | Continue statement for loops |
| `def` | v0.1 | Function/method definition |
| `defer` | v1.0 | Deferred execution block |
| `elif` | v0.1 | Else-if block |
| `else` | v0.1 | Else block |
| `enum` | v0.5 | Enumeration declaration |
| `event` | v1.0 | Event declaration |
| `except` | v0.1 | Exception handler |
| `False` | v0.1 | Boolean false literal |
| `finally` | v0.1 | Finally block |
| `for` | v0.1 | For loop |
| `from` | v0.1 | Selective imports |
| `if` | v0.1 | Conditional |
| `import` | v0.1 | Import statement |
| `in` | v0.1 | Membership test |
| `interface` | v0.3 | Interface declaration |
| `is` | v0.1 | Identity comparison |
| `lambda` | v0.4 | Lambda expression |
| `match` | v0.7 | Pattern matching |
| `None` | v0.1 | None/null literal |
| `not` | v0.1 | Boolean NOT |
| `or` | v0.1 | Boolean OR |
| `pass` | v0.1 | No-op placeholder |
| `property` | v0.9 | Property declaration |
| `raise` | v0.1 | Raise exception |
| `return` | v0.1 | Return statement |
| `struct` | v0.3 | Struct declaration |
| `True` | v0.1 | Boolean true literal |
| `try` | v0.1 | Try block |
| `type` | v0.8 | Type alias declaration |
| `while` | v0.1 | While loop |
| `with` | v1.0 | Context manager |

### Reserved for v2.0+

These keywords are reserved but not implemented until v2.0:
- `async`, `await` - Async programming (v1.0)
- `yield` - Generators (v2.0)
- `del` - Delete statement (v2.0)

### Soft Keywords (Context-Dependent)

| Keyword | Context | Notes |
|---------|---------|-------|
| `_` | Pattern matching | Wildcard pattern |
| `get` | Properties | Property getter |
| `set` | Properties | Property setter |

---

## Literals **[v0.1]**

### Integer Literals

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

**Note:** Like C#, there are no literal suffixes for `short`, `ushort`, `byte`, or `sbyte`. Use type annotations or explicit casts:

```python
# Type annotation
s: short = 42
b: byte = 255
sb: sbyte = -128

# Explicit casting [v0.4]
s = cast[short](42)
b = cast[byte](255)
```

*Implementation: ✅ Native - Direct mapping to C# integer literals.*

### Float Literals

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

*Implementation: ✅ Native - Direct mapping to C# float literals.*

### Extended Numeric Literals **[v0.6]**

```python
# Binary literals
binary = 0b1010        # 10 in decimal
flags = 0b1111_0000

# Hexadecimal literals
hex_value = 0xFF       # 255 in decimal
color = 0xRRGGBB

# Octal literals
permissions = 0o755    # 493 in decimal

# Scientific notation
avogadro = 6.022e23
planck = 6.626e-34
```

*Implementation:*
- *Binary/Hex: ✅ Native - Direct C# support (C# 7.0+)*
- *Octal: 🔄 Lowered - Converted to decimal at compile time*
- *Scientific: ✅ Native - Direct C# support*

### String Literals

```python
# Single-quoted strings
name = 'Alice'
greeting = 'Hello, World!'

# Double-quoted strings
message = "Hello, World!"
quote = "She said, 'Hello'"

# Multi-line strings (triple-quoted)
multi = """
This is a
multi-line string
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
| `\xHH` | Character with hex value HH |
| `\uHHHH` | Unicode 16-bit |
| `\UHHHHHHHH` | Unicode 32-bit |

*Implementation: ✅ Native - Single quotes become double quotes; escape sequences map directly.*

### Raw Strings

```python
# Raw strings (backslashes not escaped)
path = r"C:\Users\Alice\Documents"
regex = r"\d+\.\d+"
```

*Implementation: ✅ Native - Maps to C# verbatim strings `@"..."`.*

### F-Strings (Formatted String Literals) **[v0.6]**

```python
name = "Alice"
age = 30
msg = f"My name is {name} and I'm {age} years old"

# Expressions in f-strings
calculation = f"Result: {x * 2}"

# Format specifiers
pi = 3.14159
formatted = f"Pi: {pi:.2f}"  # "Pi: 3.14"

# Multi-line f-strings
report = f"""
Name: {name}
Age: {age}
Status: Active
"""
```

*Implementation: ✅ Native - Maps to C# interpolated strings `$"..."`.*

### Boolean Literals

```python
is_ready = True
is_complete = False
```

*Implementation: ✅ Native - `True` → `true`, `False` → `false`.*

### None Literal

```python
value: str? = None
```

- `None` maps to `null` in the compiled output
- Can be assigned to any nullable type

### None Type Semantics

| Context | Sharpy Syntax | Meaning | Compiled Form |
|---------|---------------|---------|---------------|
| Return type | `-> None` | No return value | `void` |
| Variable type | `x: None` | Not allowed | - |
| Nullable variable | `x: str?` | Can be null | Nullable reference |
| None assignment | `x = None` | Assign null | Requires `T?` type |

```python
def do_work() -> None:           # OK - return type
    pass

x: str? = None                    # OK - nullable variable
y = None                          # ERROR - cannot infer type from None alone
z: None = None                    # ERROR - None is not a valid variable type
```

*Implementation: ✅ Native - Direct mapping to `null`.*

### Special Literals

| Sharpy | Notes |
|--------|-------|
| `...` | Ellipsis - placeholder for unimplemented code |
| `{/}` | Empty set literal |

```python
# Ellipsis usage
interface IDrawable:
    def draw(self) -> None:
        ...  # Abstract method

def todo_function():
    ...  # Placeholder for implementation
```

*Implementation:*
- *Ellipsis: 🔄 Lowered - Nothing for abstract methods or interface methods without a default implementation, otherwise `throw new NotImplementedException()`*
- *Empty set: 🔄 Lowered - `new HashSet<T>()`*

---

## Types **[v0.1]**

### Built-in Primitive Types

| Sharpy Type | .NET Type | Size | Notes |
|-------------|-----------|------|-------|
| `int` | `System.Int32` | 32-bit | Default integer type |
| `long` | `System.Int64` | 64-bit | Large integers |
| `short` | `System.Int16` | 16-bit | Small integers |
| `byte` | `System.Byte` | 8-bit | Unsigned byte |
| `uint` | `System.UInt32` | 32-bit | Unsigned 32-bit |
| `ulong` | `System.UInt64` | 64-bit | Unsigned 64-bit |
| `ushort` | `System.UInt16` | 16-bit | Unsigned 16-bit |
| `sbyte` | `System.SByte` | 8-bit | Signed byte |
| `float` | `System.Single` | 32-bit | Single-precision |
| `double` | `System.Double` | 64-bit | Double-precision (default) |
| `decimal` | `System.Decimal` | 128-bit | High-precision decimal |
| `bool` | `System.Boolean` | - | `True` or `False` |
| `str` | `System.String` | - | Immutable Unicode string |
| `char` | `System.Char` | 16-bit | Single Unicode character |
| `object` | `System.Object` | - | Base type for all types |

*Implementation: ✅ Native - Direct mapping to .NET types.*

### Type Annotations

```python
# Simple types
x: int = 42
name: str = "Alice"
flag: bool = True
pi: double = 3.14159

# Type inference (annotation optional when initializer present)
y = 42              # Inferred as int
pi = 3.14159        # Inferred as double
```

*Implementation: ✅ Native - Direct mapping to C# type declarations.*

### Type Hierarchy and Object Model **[v0.1]**

#### Universal Base Type

The `object` type (mapping to `System.Object`) is the universal base type for all Sharpy types. All primitives (`int`, `str`, `bool`, etc.) and all Sharpy-defined types are assignable to `object`:

```python
# object accepts any value
x: object = 42
x = "hello"
x = [1, 2, 3]
x = MyClass()

# Useful for heterogeneous collections
items: list[object] = [1, "hello", True, SomeClass()]

# Function accepting any type
def process(value: object) -> str:
    return str(value)
```

**Type Hierarchy:**
- `object` is the implicit base type of all classes
- Primitives (`int`, `str`, `bool`, etc.) are assignable to `object` via boxing
- Structs are assignable to `object` via boxing
- `None` is assignable to `object?` but not to `object`

*Implementation: ✅ Native - `System.Object` is the universal base in .NET.*

---

## Nullable Types **[v0.2]**

Nullable types allow variables to hold either a value or `None` (null):

```python
# Nullable type annotations (type followed by ?)
result: int? = get_value()
optional_name: str? = None

# Non-nullable by default
exists: bool = False  # Cannot be None
count: int = 42       # Cannot be None

# Assigning None requires nullable type
value: int? = None    # OK
other: int = None     # ERROR: Cannot assign None to non-nullable type
```

*Implementation: ✅ Native - Maps to C# nullable reference types with `#nullable enable`.*

### Null-Conditional Access **[v0.2]**

```python
# Short-circuits if None
result = obj?.method()       # Returns None if obj is None
value = obj?.field           # Returns None if obj is None
nested = obj?.field?.nested  # Chains null checks
```

*Implementation: ✅ Native - Maps to C# `?.` operator (C# 6.0+).*

### Null-Coalescing Operator **[v0.2]**

```python
# Provide default for None values
name = user_name ?? "Anonymous"
count = get_count() ?? 0

# Chaining
result = first ?? second ?? default_value
```

This contrasts with the `or` operator which tests for truthiness (via `__true__()` and `__false__()` dunders if defined, or `__bool__()` otherwise), rather than `None`.

```python
name = "" ?? "Anonymous"    # name = ""
name = "" or "Anonymous"    # name = "Anonymous"

name = None ?? "Anonymous"  # name = "Anonymous"
name = None or "Anonymous"  # name = "Anonymous"
```

*Implementation: ✅ Native - Maps to C# `??` operator.*

### Type Narrowing **[v0.2]**

Sharpy performs type narrowing in conditional branches:

```python
value: str? = get_optional_string()

if value is not None:
    # Inside this block, 'value' is narrowed from 'str?' to 'str'
    print(value.upper())  # OK - value is str, not str?
else:
    print("No value provided")

# isinstance() narrowing
obj: object = get_value()

if isinstance(obj, str):
    # obj is narrowed to str
    print(obj.upper())
```

**Narrowing Rules:**
- `is not None` narrows nullable type (`T?`) to non-nullable (`T`)
- `is None` narrows to never-type in the `if` branch
- `isinstance(x, Type)` narrows `x` to `Type` in the `if` branch
- Narrowing only affects the scope of the conditional block

*Implementation: ✅ Native - C# supports flow analysis for nullable types.*

---

## Collection Types **[v0.2]**

| Sharpy Type | .NET Type | Notes |
|-------------|-----------|-------|
| `list[T]` | `Sharpy.Core.List<T>` | Mutable list |
| `dict[K, V]` | `Sharpy.Core.Dict<K, V>` | Hash map |
| `set[T]` | `Sharpy.Core.Set<T>` | Unique elements |
| `tuple[T1, T2, ...]` | `Sharpy.Core.Tuple<T1, T2, ...>` | Fixed-size tuple |

Collection types use a Sharpy-specific implementation by default. These are bidi-convertible with the native .NET `System.Collections.Generic` equivalents, `List<T>`, `Dictionary<K, V>`, and `HashSet<T>` and use them underneath as storage.

### Collection Literals

```python
# Empty list (type annotation required)
empty: list[int] = []

# List with elements
numbers = [1, 2, 3, 4, 5]
names = ["Alice", "Bob", "Charlie"]

# Explicit type annotation
scores: list[double] = [95.5, 87.0, 92.3]

# Dictionary literals
ages = {"Alice": 30, "Bob": 25}
empty_dict: dict[str, int] = {}

# Set literals
unique = {1, 2, 3}
empty_set: set[int] = {/}  # Special syntax for empty set

# Tuple literals
point = (10, 20)
single = (42,)  # Single element requires trailing comma
empty = ()
```

*Implementation: 🔄 Lowered*
- *List: `new Sharpy.Core.List<T> { 1, 2, 3 }`*
- *Dict: `new Sharpy.Core.Dict<K, V> { ["key"] = value }`*
- *Set: `new Sharpy.Core.Set<T> { 1, 2, 3 }`*
- *Tuple: ✅ Native ValueTuple syntax*

### Tuple Unpacking

```python
# Basic unpacking
x, y = point
a, b, c = triple

# Partial unpacking with wildcard
first, *rest = (1, 2, 3, 4, 5)      # first = 1, rest = [2, 3, 4, 5]
*start, last = (1, 2, 3)            # start = [1, 2], last = 3

# Partial unpacking is checked at compile time against the length
# of the tuple
first, *middle, last = (1, 2, 3, 4, 5)      # first = 1, middle = [2, 3, 4], last = 5
```

*Implementation: ✅ Native - C# supports tuple deconstruction.*

### Slicing **[v0.2]**

```python
numbers = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]

# Basic slice [start:end] (end is exclusive)
subset = numbers[1:4]      # [1, 2, 3]
prefix = numbers[:3]       # [0, 1, 2]
suffix = numbers[7:]       # [7, 8, 9]
full_copy = numbers[:]     # Copy all

# Negative indices
last_three = numbers[-3:]  # [7, 8, 9]

# With step
evens = numbers[::2]       # [0, 2, 4, 6, 8]
reversed_list = numbers[::-1]  # Reverse
```

*Implementation: 🔄 Lowered - Generated as helper method calls or LINQ expressions.*

---

## Operators **[v0.1]**

### Arithmetic Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `+` | Addition | `+` |
| `-` | Subtraction | `-` |
| `*` | Multiplication | `*` |
| `/` | Division (always returns float) | `/` (with cast) |
| `//` | Floor division | `(int)(x / y)` |
| `%` | Modulo | `%` |
| `**` | Exponentiation | `Math.Pow(x, y)` |

*Implementation:*
- *Standard: ✅ Native*
- *`**`: 🔄 Lowered to `Math.Pow()`*
- *`//`: 🔄 Lowered to integer cast after division*

### Comparison Operators

| Operator | Description |
|----------|-------------|
| `==` | Equality |
| `!=` | Inequality |
| `<` | Less than |
| `>` | Greater than |
| `<=` | Less than or equal |
| `>=` | Greater than or equal |

*Implementation: ✅ Native - Direct mapping.*

### Comparison Chaining **[v0.6]**

```python
# Chained comparisons
a < b < c           # Equivalent to: a < b and b < c
x == y == z         # Equivalent to: x == y and y == z
1 <= value <= 100   # Range check
```

*Implementation: 🔄 Lowered - Expanded to `a < b && b < c` with single evaluation of middle expression.*

### Logical Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `and` | Logical AND (short-circuit) | `&&` |
| `or` | Logical OR (short-circuit) | `\|\|` |
| `not` | Logical NOT | `!` |

*Implementation: ✅ Native - Direct mapping.*

### Bitwise Operators

| Operator | Description |
|----------|-------------|
| `&` | Bitwise AND |
| `\|` | Bitwise OR |
| `^` | Bitwise XOR |
| `~` | Bitwise NOT |
| `<<` | Left shift |
| `>>` | Right shift |

*Implementation: ✅ Native - Direct mapping.*

### Membership Operators

| Operator | Description |
|----------|-------------|
| `in` | Membership test |
| `not in` | Negated membership |

```python
if item in collection:
    print("Found")
```

*Implementation: 🔄 Lowered - Maps to `collection.Contains(item)`.*

### Identity Operators

| Operator | Description | C# Mapping |
|----------|-------------|------------|
| `is` | Identity comparison | `object.ReferenceEquals()` |
| `is not` | Negated identity | `!object.ReferenceEquals()` |
| `is None` | None check | `== null` |
| `is not None` | Non-None check | `!= null` |

*Implementation: ✅ Native for None checks; 🔄 Lowered for general identity.*

### Assignment Operators

| Operator | Description |
|----------|-------------|
| `=` | Simple assignment |
| `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=` | Augmented arithmetic |
| `&=`, `\|=`, `^=`, `<<=`, `>>=` | Augmented bitwise |

*Implementation: ✅ Native - Direct mapping (except `**=` which is lowered).*

### Operator Precedence

Operators listed from highest to lowest precedence:

| Precedence | Operators | Description |
|------------|-----------|-------------|
| 1 | `()`, `[]`, `.`, `?.` | Grouping, indexing, member access |
| 2 | `**` | Exponentiation (right-associative) |
| 3 | `+x`, `-x`, `~x` | Unary operators |
| 4 | `*`, `/`, `//`, `%` | Multiplicative |
| 5 | `+`, `-` | Additive |
| 6 | `<<`, `>>` | Bitwise shifts |
| 7 | `&` | Bitwise AND |
| 8 | `^` | Bitwise XOR |
| 9 | `\|` | Bitwise OR |
| 10 | `in`, `not in`, `is`, `is not`, `<`, `<=`, `>`, `>=`, `!=`, `==` | Comparisons |
| 11 | `not` | Logical NOT |
| 12 | `and` | Logical AND |
| 13 | `or` | Logical OR |
| 14 | `??` | Null coalescing |
| 15 | `x if c else y` | Conditional expression |
| 16 | `lambda` | Lambda expression |

---

## Expressions **[v0.1]**

### Primary Expressions

```python
# Literals
42                  # Integer
3.14                # Float
"hello"             # String
True                # Boolean
None                # None

# Identifiers
x
my_variable

# Parenthesized
(x + y)
(2 + 3) * 4
```

### Member Access

```python
# Standard access
obj.field
obj.method()

# Null-conditional [v0.2]
obj?.field
obj?.method()
```

### Index Access

```python
arr[0]              # First element
arr[-1]             # Last element
arr[i]              # Element at index i
matrix[i, j]        # Multi-dimensional
```

### Function Calls

```python
print("Hello")
calculate_total(100, 0.08)
obj.method(arg1, arg2)

# Generic instantiation [v0.4]
container = ListContainer[str]()
```

### Conditional Expression (Ternary)

```python
result = x if x > 0 else -x         # Absolute value
status = "even" if n % 2 == 0 else "odd"
```

*Implementation: ✅ Native - Maps to `condition ? trueVal : falseVal`.*

### Lambda Expressions **[v0.4]**

```python
# Single expression lambda
square = lambda x: x ** 2
add = lambda x, y: x + y

# As function argument
result = apply(10, lambda x: x ** 2)
```

**Lambda Rules:**
- Single expression only (no statements)
- Parameter types inferred from context
- Expression result is automatically returned

*Implementation: ✅ Native - Maps to `(x, y) => expr`.*

### Expression Evaluation Order

Expressions are evaluated left-to-right:

```python
# Left-to-right evaluation
result = f1() + f2() * f3()
# Order: f1(), f2(), f3(), then operators by precedence

# Short-circuit evaluation
result = cheap() and expensive()
# If cheap() is False, expensive() is never called

# Argument evaluation
func(first(), second(), third())
# Order: first(), second(), third(), then func() called
```

**Rules:**
1. Expressions evaluated left-to-right
2. Operator precedence determines grouping, not evaluation order
3. Short-circuit operators (`and`, `or`, `??`, `?.`) stop early when possible
4. Function arguments evaluated left-to-right before call

---

## Statements **[v0.1]**

### Expression Statement

Any expression can be a statement:

```python
print("Hello")
obj.method()
list.append(item)
```

### Variable Declaration

```python
# With type annotation
x: int = 42
name: str = "Alice"

# Without initializer (must assign before use)
count: int
count = 10

# Type inference (when initializer present)
y = 42  # Inferred as int

# Constant declaration
const PI: double = 3.14159
const MAX_SIZE = 1000  # Type inferred
```

**Rules:**
- Variables declared without initializer must be assigned before use
- Constants must be initialized at declaration with compile-time values

*Implementation: ✅ Native - Direct mapping to C# declarations.*

## Variable Scoping Rules [v0.1]

**Block-Scoped Constructs** (variable doesn't leak):
- For loop variables
- Comprehension variables
- Exception binding (`except E as e`)

**Containing-Scope Constructs** (variable persists):
- Regular declarations (`x = value`, `var x = value`, `x: type = value`)
- Walrus operator (`x := value`)

### Example:

```python
x = "outer"

for x in range(5):      # New 'x' shadows outer, block-scoped
    print(x)            # Prints 0, 1, 2, 3, 4

print(x)                # ERROR: loop 'x' doesn't exist
                        # Outer 'x' is shadowed but not modified
```

### To modify outer variable:

```python
x = 0
for i in range(5):      # 'i' is block-scoped
    x += i              # Modifies outer 'x'
print(x)                # 10
```

### Assignment Statement

```python
# Simple assignment
x = 10

# Multiple assignment (unpacking)
x, y = 10, 20

# Augmented assignment
x += 5
count *= 2
```

### Variable Shadowing **[v0.8]**

Variables can be redeclared in the same scope with a different type using explicit type annotation:

```python
x: int = 5              # Initial declaration
x = 10                  # Assignment (same type)
x: str = "hello"        # Shadowing (new type, requires annotation)

# With auto keyword for type inference
x: int = 5
x: auto = "hello"       # Shadowing with inferred type
```

*Implementation: 🔄 Lowered - Generates versioned variable names (`x`, `x_1`, `x_2`).*

### Pass Statement

```python
def todo():
    pass  # Placeholder

class Placeholder:
    pass
```

*Implementation: ✅ Native - Empty statement or empty body.*

### Break and Continue

```python
for item in items:
    if item == target:
        break       # Exit loop
    if should_skip(item):
        continue    # Skip to next iteration
```

*Implementation: ✅ Native - Direct mapping.*

### Return Statement

```python
def add(x: int, y: int) -> int:
    return x + y

def print_message() -> None:
    print("Hello")
    return  # Optional
```

*Implementation: ✅ Native - Direct mapping.*

### Assert Statement

```python
assert condition
assert x > 0, "Value must be positive"
```

*Implementation: 🔄 Lowered - `System.Diagnostics.Debug.Assert(condition, message)`.*

---

## Control Flow **[v0.1]**

### If Statement

```python
if x > 0:
    print("positive")
elif x < 0:
    print("negative")
else:
    print("zero")
```

*Implementation: ✅ Native - Direct mapping to `if`/`else if`/`else`.*

### While Loop

```python
count = 0
while count < 10:
    print(count)
    count += 1
```

*Implementation: ✅ Native - Direct mapping.*

### For Loop

```python
# Iterate over collection
for name in names:
    print(name)

# Iterate with range
for i in range(10):
    print(i)

# Enumerate for index and value
for index, name in enumerate(names):
    print(f"{index}: {name}")
```

*Implementation:*
- *Collection: ✅ Native - `foreach (var item in collection)`*
- *`range()`: 🔄 Lowered - `for (int i = 0; i < n; i++)`*
- *`enumerate()`: 🔄 Lowered - `.Select((x, i) => (i, x))`*

### Loop Else Clause **[v0.6]**

```python
for item in items:
    if item == target:
        break
else:
    # Executed only if loop completes without break
    print("Not found")
```

*Implementation: 🔄 Lowered - Boolean flag pattern:*
```csharp
bool _loopCompleted = true;
foreach (var item in items) {
    if (item == target) { _loopCompleted = false; break; }
}
if (_loopCompleted) { Console.WriteLine("Not found"); }
```

---

## Exception Handling **[v0.1]**

### Try/Except/Finally

```python
try:
    result = risky_operation()
except ValueError as e:
    print(f"Invalid value: {e}")
except Exception as e:
    print(f"Error: {e}")
else:
    # Executed if no exception
    print(f"Success: {result}")
finally:
    # Always executed
    cleanup()
```

*Implementation:*
- *try/except/finally: ✅ Native - `try`/`catch`/`finally`*
- *else clause: 🔄 Lowered - Boolean flag pattern*

### Raise Statement

```python
# Raise exception
raise ValueError("Invalid input")

# Re-raise current exception
except Exception as e:
    log_error(e)
    raise

# Raise with cause
raise RuntimeError("Failed") from original_error
```

*Implementation:*
- *raise: ✅ Native - `throw new Exception()`*
- *bare raise: ✅ Native - `throw;`*
- *raise from: 🔄 Lowered - Inner exception constructor*

---

## Functions **[v0.1]**

### Function Definition

```python
def greet(name: str) -> str:
    """Greet a person by name."""
    return f"Hello, {name}!"

def print_message(message: str) -> None:
    print(message)

# With default parameters
def power(base: double, exponent: double = 2.0) -> double:
    return base ** exponent

# Multiple return values via tuple
def min_max(values: list[int]) -> tuple[int, int]:
    return (min(values), max(values))
```

**Rules:**
- All parameters must have type annotations
- Return type annotation required if function returns a value
- Return type can be omitted for `-> None` functions
- Parameters with defaults must come after required parameters

### Default Parameter Evaluation

Default parameter values are evaluated **once** at function definition time, not per call. This means mutable default parameters share the same instance across calls:

```python
# DANGER: Mutable default parameter
def append_to(item: int, target: list[int] = []) -> list[int]:
    target.append(item)
    return target

list1 = append_to(1)  # [1]
list2 = append_to(2)  # [1, 2] - SAME list as list1!
```

**Best Practice:** Never use mutable objects (lists, dicts, sets) as default parameter values. Use `None` and create the instance inside the function:

```python
# CORRECT: Use None as default
def append_to(item: int, target: list[int]? = None) -> list[int]:
    if target is None:
        target = []
    target.append(item)
    return target

list1 = append_to(1)  # [1]
list2 = append_to(2)  # [2] - separate list
```

*Implementation: ✅ Native - Direct mapping to C# methods.*

### Function Overloading **[v0.3]**

```python
def process(value: int) -> str:
    return f"Integer: {value}"

def process(value: str) -> str:
    return f"String: {value}"

def process(value: int, multiplier: int) -> str:
    return f"Result: {value * multiplier}"
```

**Rules:**
- Overloads resolved by parameter count and types
- Parameter names do not affect resolution

*Implementation: ✅ Native - C# supports method overloading.*

---

## Classes **[v0.1]**

### Basic Class Definition

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

    def celebrate_birthday(self) -> None:
        self.age += 1
```

**Rules:**
- All instance fields must be declared at class level with type annotations
- The `self` parameter is required for instance methods
- The `self` parameter is not type-annotated
- `__init__` return type is implicitly `None`

*Implementation: ✅ Native - Direct mapping to C# class.*

### Constructor Overloading **[v0.3]**

```python
class Point:
    x: double
    y: double

    def __init__(self):
        self.x = 0.0
        self.y = 0.0

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def __init__(self, other: Point):
        self.x = other.x
        self.y = other.y
```

*Implementation: ✅ Native - Multiple C# constructors.*

---

## Imports **[v0.1]**

### Import Statement

```python
# Import entire module
import math
result = math.sqrt(16.0)

# Import with alias
import math as m
result = m.sqrt(16.0)
```

*Implementation: ✅ Native - `using Namespace;` or `using Alias = Namespace;`*

### From-Import Statement

```python
# Import specific names
from math import sqrt, pi
result = sqrt(16.0)

# Import with alias
from math import sqrt as square_root

# Import all (use sparingly)
from math import *
```

*Implementation: ✅ Native - `using static` or direct reference.*

### Module Resolution

- Module names converted: `snake_case` → `PascalCase`
- Common acronyms uppercased: `io`, `ui`, `xml`, `http`, `api`, `sql`
- Example: `system.collections.generic` → `System.Collections.Generic`

### Package Structure

Packages are directories containing an optional `__init__.spy` file:

```
project/
    utils/
        __init__.spy      # Optional, can be empty
        helpers.spy
        math/
            __init__.spy
            vectors.spy
```

The `__init__.spy` file can re-export symbols for convenient imports:

```python
# utils/__init__.spy
from utils.helpers import format_string, parse_input
from utils.math.vectors import Vector2, Vector3
```

### Circular Import Handling

Circular imports are resolved through forward declarations:

```python
# module_a.spy
from module_b import ClassB  # Forward reference for type annotation

class ClassA:
    other: ClassB  # OK - used only as type annotation

    def use_b(self, b: ClassB) -> None:
        b.method()
```

**Rules:**
- Circular references are allowed for type annotations
- Circular references for base classes are **not** allowed
- Import order matters: import for type hints processed before code execution

---

## Structs **[v0.3]**

Structs are value types that do not support inheritance but can implement interfaces.

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

    def __add__(self, other: Vector2) -> Vector2:
        return Vector2(self.x + other.x, self.y + other.y)
```

**Struct Rules:**
- All fields must be declared at struct level
- Must have a constructor that initializes all fields
- Cannot inherit from other structs or classes
- Can implement interfaces
- Value semantics: copied when assigned or passed

**When to Use Structs:**
- Small data structures (typically < 16 bytes)
- Immutable value types (Vector2, Point, Color)
- Types that benefit from value semantics

*Implementation: ✅ Native - Direct mapping to C# `struct`.*

---

## Interfaces **[v0.3]**

Interfaces define contracts that types must satisfy.

```python
interface IDrawable:
    """Interface for drawable objects."""

    def draw(self) -> None:
        ...

    def get_bounds(self) -> tuple[double, double, double, double]:
        ...

# Implementation
class Circle(IDrawable):
    radius: double
    x: double
    y: double

    def __init__(self, x: double, y: double, radius: double):
        self.x = x
        self.y = y
        self.radius = radius

    def draw(self) -> None:
        print(f"Drawing circle at ({self.x}, {self.y})")

    def get_bounds(self) -> tuple[double, double, double, double]:
        return (self.x - self.radius, self.y - self.radius,
                self.radius * 2, self.radius * 2)
```

**Interface Rules:**
- Methods use `...` (ellipsis) for body
- All methods are implicitly abstract
- Implementing types must provide all methods

*Implementation: ✅ Native - Direct mapping to C# `interface`.*

### Generic Interfaces **[v0.4]**

```python
interface IContainer[T]:
    def add(self, item: T) -> None: ...
    def get(self, index: int) -> T: ...
    def count(self) -> int: ...
```

### Interface Inheritance **[v0.3]**

Interfaces can extend other interfaces:

```python
interface ISerializable:
    """Base interface for serialization."""
    def serialize(self) -> str: ...

interface IJSONSerializable(ISerializable):
    """Extends ISerializable with JSON-specific methods."""
    def to_json(self) -> str: ...
    def from_json(self, json: str) -> None: ...

class User(IJSONSerializable):
    """Must implement all methods from both interfaces."""
    username: str

    def serialize(self) -> str:
        return self.username

    def to_json(self) -> str:
        return f'{{"username": "{self.username}"}}'

    def from_json(self, json: str) -> None:
        pass  # Parse and update
```

---

## Inheritance **[v0.3]**

### Single Class Inheritance

```python
class Employee(Person):
    employee_id: str

    def __init__(self, name: str, age: int, employee_id: str):
        super().__init__(name, age)
        self.employee_id = employee_id

    def greet(self) -> str:
        return f"Hello, I'm {self.name}, employee #{self.employee_id}"
```

*Implementation: ✅ Native - `: BaseClass`; `super().__init__()` → `: base()` or `base.Method()`*

### Multiple Interface Implementation

```python
class JSONEmployee(Employee, ISerializable, IComparable):
    def serialize(self) -> str:
        # Implementation
        pass

    def compare_to(self, other: object) -> int:
        # Implementation
        pass
```

**Rules:**
- Single class inheritance allowed
- Multiple interface implementation allowed
- Base class (if present) must come first

*Implementation: ✅ Native - `: BaseClass, IInterface1, IInterface2`*

---

## Decorators **[v0.3]**

Decorators modify the behavior of functions, methods, and classes.

### Access Modifiers

| Decorator | C# Equivalent | Visibility |
|-----------|---------------|------------|
| (default) | `public` | Everyone |
| `@protected` or `_name` | `protected` | Class and derived |
| `@private` or `__name` | `private` | Declaring class only |
| `@internal` | `internal` | Same assembly |

```python
class Example:
    @private
    def internal_method(self) -> None:
        pass

    # Naming convention also works
    def _protected_method(self) -> None:
        pass

    def __private_method(self) -> None:
        pass
```

*Implementation: ✅ Native - Direct mapping to C# access modifiers.*

### Method Modifiers

| Decorator | C# Equivalent |
|-----------|---------------|
| `@static` | `static` |
| `@override` | `override` |
| `@virtual` | `virtual` |
| `@abstract` | `abstract` |
| `@final` (method) | `sealed override` |
| `@final` (class) | `sealed class` |

```python
class Calculator:
    @static
    def add(x: int, y: int) -> int:
        return x + y

    # Virtual method (can be overridden by subclasses)
    @virtual
    def compute(self, x: int) -> int:
        return x * 2

    @override
    def __str__(self) -> str:
        return "Calculator"

class ScientificCalculator(Calculator):
    """Subclass that overrides virtual method."""

    @override
    def compute(self, x: int) -> int:
        return x ** 2

@final
class SealedClass:
    pass

# Usage
result = Calculator.add(5, 3)       # Static method call
calc = ScientificCalculator()
calc.compute(4)                      # Returns 16 (overridden method)
```

*Implementation: ✅ Native - Direct mapping to C# keywords.*

---

## Generics **[v0.4]**

### Generic Classes

```python
class Box[T]:
    """A container for a single value."""
    _value: T

    def __init__(self, value: T):
        self._value = value

    def get(self) -> T:
        return self._value

    def set(self, value: T) -> None:
        self._value = value

# Usage
int_box = Box[int](42)
str_box = Box[str]("hello")
```

*Implementation: ✅ Native - `class Box<T>`*

### Generic Functions

```python
def identity[T](value: T) -> T:
    return value

def first[T](items: list[T]) -> T:
    return items[0]
```

*Implementation: ✅ Native - `T Identity<T>(T value)`*

### Type Constraints **[v0.4]**

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

| Constraint | C# Equivalent |
|------------|---------------|
| `T: Interface` | `where T : Interface` |
| `T: class` | `where T : class` |
| `T: struct` | `where T : struct` |
| `T: new()` | `where T : new()` |

*Implementation: ✅ Native - Direct mapping to C# generic constraints.*

---

## Enumerations **[v0.5]**

### Simple Enums

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"

# Usage
favorite = Color.RED
if favorite == Color.RED:
    print("Red is your favorite")

# Access underlying value
value = favorite.value  # 1
name = favorite.name    # "RED"
```

**Rules:**
- All cases must have explicit constant values
- Values must be integers or strings
- Enums must have at least one variant

*Implementation:*
- *Integer enums: ✅ Native - C# `enum`*
- *String enums: 🔄 Lowered - Static class with string constants*
- *`.name` property: 🔄 Lowered - `Enum.GetName()` or lookup*

---

## Operator Overloading **[v0.5]**

Classes can define dunder methods (double-underscore methods like `__add__`, `__eq__`) to customize how operators and built-in functions behave with their instances. **Dunder methods are a definition mechanism only**—they specify *how* a type behaves, but users invoke that behavior through operators and built-in functions, not by calling dunders directly.

### Dunder Invocation Rules **[v0.1]**

#### Dunders Are Definition-Only

Dunder methods exist to **define** how a type behaves with operators and built-in functions. **Explicit dunder invocation by user code is a compile error:**

```python
x = 5
x.__eq__(3)         # ERROR: Cannot invoke dunder methods directly
x.__repr__()        # ERROR: Cannot invoke dunder methods directly

my_list = [1, 2, 3]
my_list.__len__()   # ERROR: Cannot invoke dunder methods directly

obj = MyClass()
obj.__str__()       # ERROR: Cannot invoke dunder methods directly
```

#### Correct Usage

Use operators for operator dunders:

```python
x == y              # ✅ Correct — compiler uses __eq__ internally
x + y               # ✅ Correct — compiler uses __add__ internally
-x                  # ✅ Correct — compiler uses __neg__ internally
x < y               # ✅ Correct — compiler uses __lt__ internally
x[0]                # ✅ Correct — compiler uses __getitem__ internally
```

Use built-in functions for protocol dunders:

```python
repr(x)             # ✅ Correct — uses __repr__ internally
len(x)              # ✅ Correct — uses __len__ internally
hash(x)             # ✅ Correct — uses __hash__ internally
str(x)              # ✅ Correct — uses __str__ internally
```

#### Rationale

- **Uniform syntax**: `repr(x)` and `x == y` work on any type, whether primitive or Sharpy-defined
- **.NET interop**: Primitives from .NET (`int`, `str`, `bool`) don't have dunder methods—the compiler handles dispatch
- **Zero overhead**: No wrapper types or boxing required for polymorphic dispatch
- **Consistency**: Same syntax works whether the type defines a dunder or uses native behavior

*Implementation: The compiler emits different code based on static type:*
- *For primitives: direct C# operator or method call*
- *For Sharpy types with dunder: call to the generated method*
- *For built-in functions: type-appropriate dispatch (e.g., `len()` calls `.Count` or `__len__`)*

### Dunder Inheritance and Internal Calls **[v0.1]**

While user code cannot call dunders directly, there are specific contexts where dunder calls are permitted.

#### Dunder Inheritance

Dunder methods are inherited like any other method:

```python
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    def __repr__(self) -> str:
        return f"Animal({self.name})"

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    # Inherits __repr__ from Animal

dog = Dog("Buddy")
print(repr(dog))  # Output: Animal(Buddy)
```

#### Overriding Dunders

Dunder methods can be overridden using `@override`:

```python
class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    @override
    def __repr__(self) -> str:
        return f"Dog({self.name})"

dog = Dog("Buddy")
print(repr(dog))  # Output: Dog(Buddy)
```

#### Base Class Dunder Calls

Within a dunder method, you may call the base class implementation via `super()`:

```python
class Child(Parent):
    @override
    def __repr__(self) -> str:
        return super().__repr__() + " (child)"  # ✅ OK

    @override
    def __eq__(self, other: object) -> bool:
        if not super().__eq__(other):           # ✅ OK
            return False
        # Additional checks...
        return True
```

#### Cross-Dunder Calls for Synthesis

Within a dunder method, you may call other dunders on `self` for synthesizing related operations:

```python
class Ordered:
    value: int

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Ordered):
            return False
        return self.value == other.value

    def __lt__(self, other: Ordered) -> bool:
        return self.value < other.value

    def __le__(self, other: Ordered) -> bool:
        return self.__lt__(other) or self.__eq__(other)  # ✅ OK

    def __ge__(self, other: Ordered) -> bool:
        return not self.__lt__(other)                    # ✅ OK

    def __ne__(self, other: object) -> bool:
        return not self.__eq__(other)                    # ✅ OK

    def __gt__(self, other: Ordered) -> bool:
        return not self.__le__(other)                    # ✅ OK
```

#### Restrictions

Dunder calls on `self` or `super()` are **only** permitted:
- Within a dunder method body
- As immediate call expressions (cannot be captured or passed)

```python
class Example:
    def __repr__(self) -> str:
        func = self.__eq__              # ❌ ERROR: Cannot capture dunder
        return str(self.__hash__())     # ✅ OK: Immediate call, cross-dunder

    def regular_method(self):
        self.__repr__()                 # ❌ ERROR: Not inside a dunder
        print(repr(self))               # ✅ OK: Use built-in function

    def __eq__(self, other: object) -> bool:
        return other.__eq__(self)       # ❌ ERROR: Not self or super()
```

#### Child Objects Use Built-in Functions

For calling dunder-like behavior on other objects (including fields), use operators or built-in functions:

```python
class Node:
    left: Node?
    right: Node?
    value: int

    def __repr__(self) -> str:
        left_repr = repr(self.left) if self.left is not None else "None"
        right_repr = repr(self.right) if self.right is not None else "None"
        return f"Node({self.value}, {left_repr}, {right_repr})"
        # NOT: self.left.__repr__()  # ❌ Would be error anyway

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Node):
            return False
        return self.value == other.value  # ✅ Use == operator
        # NOT: self.value.__eq__(other.value)  # ❌ Error
```

#### Summary Table

| Call Site | `self.__dunder__()` | `super().__dunder__()` | `other.__dunder__()` |
|-----------|--------------------|-----------------------|---------------------|
| Inside dunder method | ✅ Immediate only | ✅ Immediate only | ❌ Use operator/built-in |
| Outside dunder method | ❌ Error | ❌ Error | ❌ Use operator/built-in |

### Arithmetic Operators

```python
class Vector:
    x: double
    y: double

    def __init__(self, x: double, y: double):
        self.x = x
        self.y = y

    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)

    def __sub__(self, other: Vector) -> Vector:
        return Vector(self.x - other.x, self.y - other.y)

    def __mul__(self, scalar: double) -> Vector:
        return Vector(self.x * scalar, self.y * scalar)

    def __neg__(self) -> Vector:
        return Vector(-self.x, -self.y)
```

| Operator | Dunder Method | C# Operator |
|----------|---------------|-------------|
| `+` | `__add__` | `operator +` |
| `-` | `__sub__` | `operator -` |
| `*` | `__mul__` | `operator *` |
| `/` | `__truediv__` | `operator /` |
| `//` | `__floordiv__` | (method call) |
| `%` | `__mod__` | `operator %` |
| `**` | `__pow__` | (method call) |
| `-x` | `__neg__` | `operator -` (unary) |
| `+x` | `__pos__` | `operator +` (unary) |

*Implementation: ✅ Native - Generates both dunder method and C# operator overload.*

### Comparison Operators

```python
class Point:
    x: int
    y: int

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Point):
            return False
        return self.x == other.x and self.y == other.y

    def __lt__(self, other: Point) -> bool:
        return (self.x ** 2 + self.y ** 2) < (other.x ** 2 + other.y ** 2)
```

| Operator | Dunder Method | C# Method |
|----------|---------------|-----------|
| `==` | `__eq__` | `operator ==` + `Equals()` |
| `!=` | `__ne__` | `operator !=` |
| `<` | `__lt__` | `operator <` |
| `<=` | `__le__` | `operator <=` |
| `>` | `__gt__` | `operator >` |
| `>=` | `__ge__` | `operator >=` |

### Special Methods

| Method | Purpose | C# Mapping | Invoked Via |
|--------|---------|------------|-------------|
| `__str__` | String representation | `ToString()` override | `str(x)` |
| `__repr__` | Debug representation | Custom method | `repr(x)` |
| `__hash__` | Hash value | `GetHashCode()` override | `hash(x)` |
| `__len__` | Length | `Count` property | `len(x)` |
| `__contains__` | Membership test | `Contains()` method | `x in collection` |
| `__iter__` | Iteration | `GetEnumerator()` | `for x in obj` |
| `__getitem__` | Index access | Indexer `this[...]` | `obj[key]` |
| `__setitem__` | Index assignment | Indexer `this[...]` | `obj[key] = value` |
| `__delitem__` | Index deletion | (method call) | `del obj[key]` |

**Note:** Users invoke these behaviors through the "Invoked Via" syntax, not by calling the dunder methods directly. See [Dunder Invocation Rules](#dunder-invocation-rules-v01) for details.

### Hashable Objects

For objects to be used as dictionary keys or in sets, they must implement `__hash__` and `__eq__`:

```python
class Coordinate:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

    def __eq__(self, other: object) -> bool:
        if not isinstance(other, Coordinate):
            return False
        return self.x == other.x and self.y == other.y

    def __hash__(self) -> int:
        return hash((self.x, self.y))

# Now usable as dict key or in sets
locations: dict[Coordinate, str] = {}
coord = Coordinate(10, 20)
locations[coord] = "Home"  # Works because __hash__ and __eq__ defined
```

**Rules for Hashable Objects:**
- If `__eq__` is defined, `__hash__` must also be defined, and vice versa
- If `a == b`, then `hash(a) == hash(b)` must be true
- Hash value should not change during object lifetime
- Mutable objects should not implement `__hash__`

*Implementation: ✅ Native or 🔄 Lowered depending on the method.*

---

## Pattern Matching **[v0.7]**

### Match Statement

```python
def describe(value: object) -> str:
    match value:
        case 0:
            return "zero"
        case 1:
            return "one"
        case int() as n if n > 0:
            return "positive integer"
        case int() as n:
            return "negative integer"
        case str() as s:
            return f"string: {s}"
        case _:
            return "unknown"
```

*Implementation: ✅ Native - Maps to C# `switch` expression/statement (C# 8+).*

### Supported Patterns

| Pattern | Syntax | C# 9.0 Mapping |
|---------|--------|----------------|
| Literal | `case 0:` | `case 0:` |
| Type | `case int():` | `case int:` |
| Type with binding | `case int() as n:` | `case int n:` |
| Wildcard | `case _:` | `default:` or `_` |
| Guard | `case int() as n if n > 0:` | `case int n when n > 0:` |
| OR | `case "a" \| "b":` | `case "a" or "b":` |
| Tuple | `case (0, 0):` | Direct support |
| Property | `case Point(x=0):` | `case Point { X: 0 }:` |
| Relational | `case > 0:` | Direct support (C# 9) |

*Implementation: ✅ Native - All patterns map to C# 9.0 pattern matching.*

### Tuple Patterns

```python
match point:
    case (0, 0):
        print("Origin")
    case (0, y):
        print(f"On Y-axis at {y}")
    case (x, 0):
        print(f"On X-axis at {x}")
    case (x, y):
        print(f"Point at ({x}, {y})")
```

### Property Patterns

```python
match shape:
    case Point(x=0, y=0):
        print("Origin point")
    case Point(x=x, y=0):
        print(f"On X-axis at {x}")
```

### Exhaustiveness Checking

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# WARNING: Non-exhaustive match (missing BLUE)
match color:
    case Color.RED:
        print("Red")
    case Color.GREEN:
        print("Green")

# OK: Exhaustive with wildcard
match color:
    case Color.RED:
        print("Red")
    case _:
        print("Other color")
```

---

## Type Aliases **[v0.8]**

Type aliases create readable names for complex types:

```python
# Module-level aliases
type UserId = int
type Coordinate = tuple[double, double]
type Matrix = list[list[double]]

# Generic aliases
type Callback[T] = (T) -> None
type Result[T, E] = Result[T, E]

# Class-level aliases
class Geometry:
    type Point3D = tuple[double, double, double]

    def distance(self, p1: Point3D, p2: Point3D) -> double:
        dx, dy, dz = p1[0] - p2[0], p1[1] - p2[1], p1[2] - p2[2]
        return (dx**2 + dy**2 + dz**2) ** 0.5

# Function-level aliases
def process_data[T, E](items: dict[str, list[Result[T, E]]]) -> dict[str, list[Result[T, E]]]:
    type DataMap = dict[str, list[Result[T, E]]]
    result: DataMap = {}
    # ...
    return result
```

*Implementation: 🔄 Lowered - Inline expansion at use sites; `using` directive where possible.*

---

## Tagged Unions (Algebraic Data Types) **[v0.8]**

Tagged unions allow cases to carry associated data:

```python
# Generic Result type (like Rust's Result)
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

# Option type
enum Option[T]:
    case Some(value: T)
    case None()

# Tree structure
enum BinaryTree[T]:
    case Leaf(value: T)
    case Node(left: BinaryTree[T], right: BinaryTree[T])
```

### Creating Values

```python
success = Result.Ok(42)
failure = Result.Err("Something went wrong")

# Factory methods (lowercase convention)
success = Result.ok(42)
failure = Result.err("Something went wrong")
```

### Pattern Matching

```python
def divide(a: double, b: double) -> Result[double, str]:
    if b == 0:
        return Result.err("Division by zero")
    return Result.ok(a / b)

result = divide(10, 2)
match result:
    case Result.Ok(value):
        print(f"Success: {value}")
    case Result.Err(error):
        print(f"Error: {error}")
```

### Methods on Tagged Unions

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

    def is_ok(self) -> bool:
        match self:
            case Result.Ok():
                return True
            case Result.Err():
                return False

    def unwrap(self) -> T:
        match self:
            case Result.Ok(value):
                return value
            case Result.Err(error):
                raise Exception(f"Called unwrap on Err: {error}")

    def unwrap_or(self, default: T) -> T:
        match self:
            case Result.Ok(value):
                return value
            case Result.Err():
                return default
```

*Implementation: 🔄 Lowered - Abstract base class + sealed nested case classes:*

```csharp
public abstract class Result<T, E> {
    private Result() { }

    public sealed class Ok : Result<T, E> {
        public T Value { get; }
        public Ok(T value) => Value = value;
        public void Deconstruct(out T value) => value = Value;
    }

    public sealed class Err : Result<T, E> {
        public E Error { get; }
        public Err(E error) => Error = error;
        public void Deconstruct(out E error) => error = Error;
    }

    public static Result<T, E> ok(T value) => new Ok(value);
    public static Result<T, E> err(E error) => new Err(error);
}
```

---

## Comprehensions **[v0.9]**

### List Comprehensions

```python
# Basic
squares = [x**2 for x in range(10)]

# With condition
evens = [x for x in range(10) if x % 2 == 0]

# With transformation and condition
doubled_evens = [x * 2 for x in range(10) if x % 2 == 0]

# Nested
matrix = [[i * j for j in range(3)] for i in range(3)]

# Multiple for clauses
pairs = [(x, y) for x in range(3) for y in range(3)]
```

*Implementation: 🔄 Lowered - LINQ expressions:*
- `[expr for x in iter]` → `.Select(x => expr).ToList()`
- `[expr for x in iter if cond]` → `.Where(x => cond).Select(x => expr).ToList()`

### Dict Comprehensions

```python
square_dict = {x: x**2 for x in range(5)}
# Result: {0: 0, 1: 1, 2: 4, 3: 9, 4: 16}
```

*Implementation: 🔄 Lowered - `.ToDictionary(x => key, x => value)`*

### Set Comprehensions

```python
unique_lengths = {len(word) for word in ["apple", "banana", "cherry"]}
# Result: {5, 6}
```

*Implementation: 🔄 Lowered - `.Select(...).ToHashSet()`*

### Comprehension Variable Scope

Variables declared in comprehensions are scoped to that comprehension and do not leak into the outer scope:

```python
# List comprehension
squares = [i ** 2 for i in range(10)]
print(i)  # ERROR: 'i' does not exist in this scope

# Dict comprehension
ages = {name: age for name, age in pairs}
print(name)  # ERROR: 'name' does not exist in this scope

# For loop (same behavior)
for x in items:
    process(x)
print(x)  # ERROR: 'x' does not exist in this scope
```

Variables declared inside comprehensions shadow any from the outer scope:

```python
x = 100
squares = [x ** 2 for x in range(5)]  # This 'x' shadows outer 'x'
print(x)  # ERROR: 'x' does not exist (shadowing doesn't leak)
```

---

## Walrus Operator **[v0.9]**

The walrus operator `:=` allows assignment within expressions:

```python
# Capture value in conditional
if (match := pattern.search(text)) is not None:
    print(f"Found match at {match.start()}")

# Reuse computed value
results = [y for x in data if (y := transform(x)) is not None]

# Avoid repeated calls
while (line := file.read_line()) is not None:
    process(line)
```

*Implementation: 🔄 Lowered - Hoisted variable declaration:*

```python
# Sharpy
if (match := pattern.search(text)) is not None:
    print(match.group())
```
```csharp
// C# 9.0
var match = pattern.Match(text);
if (match.Success) {
    Console.WriteLine(match.Value);
}
```

---

## Properties **[v0.9]**

Properties provide computed access to object state.

### Auto Properties

```python
class Person:
    # Auto-property with default
    property name: str = "Unknown"

    # Read-only auto-property
    get property id: int = 0

    # Write-only auto-property (rare)
    set property _internal: int
```

*Implementation: 🔄 Lowered - Generate backing field + accessors.*

### Explicit Properties

```python
class Temperature:
    __celsius: double = 0.0

    # Explicit getter and setter
    property celsius(self) -> double:
        return self.__celsius

    property celsius(self, value: double):
        self.__celsius = value

    # Computed read-only property
    property fahrenheit(self) -> double:
        return self.__celsius * 9/5 + 32
```

*Implementation: ✅ Native - Maps to C# property accessors.*

---

## Context Managers **[v1.0]**

The `with` statement manages resources:

```python
with open("file.txt", "r") as f:
    content = f.read()
# f.close() called automatically

# Multiple resources
with open("in.txt") as input, open("out.txt", "w") as output:
    output.write(input.read())
```

**Protocol:**
- Object passed to `with` should implement `IDisposable`
- `__enter__()` called on entry (returns object for `as` binding)
- `__exit__()` called on exit (or `Dispose()`)

*Implementation: ✅ Native - `using (var r = resource) { ... }`*

---

## Defer Statement **[v1.0]**

The `defer` statement schedules code to execute when the current scope exits:

```python
def process_file(path: str) -> str:
    file = open(path, "r")
    defer:
        file.close()

    # file.close() called when function exits
    return process(file.read())
```

### Multiple Defers (LIFO Order)

```python
def nested_resources():
    print("Opening A")
    defer:
        print("Closing A")

    print("Opening B")
    defer:
        print("Closing B")

    print("Processing")

# Output:
# Opening A
# Opening B
# Processing
# Closing B  (last defer first)
# Closing A
```

### Defer with Exceptions

Defer blocks execute even when exceptions are raised:

```python
def risky_operation():
    resource = acquire()
    defer:
        resource.release()  # Always executed

    dangerous_work(resource)  # May throw
```

*Implementation: 🔄 Lowered - `try`/`finally` pattern:*

```csharp
string ProcessFile(string path) {
    var file = File.OpenRead(path);
    try {
        return Process(file.ReadToEnd());
    } finally {
        file.Close();
    }
}
```

*Multiple defers become nested try/finally blocks.*

---

## Events **[v1.0]**

Events provide a publish-subscribe pattern:

```python
class Button:
    # Event declaration
    event clicked: (object, EventArgs) -> None

    def click(self):
        if self.clicked is not None:
            self.clicked(self, EventArgs())

# Subscription
button = Button()

def on_clicked(sender: object, args: EventArgs):
    print("Button clicked!")

button.clicked += on_clicked  # Subscribe
button.click()                 # Triggers event
button.clicked -= on_clicked  # Unsubscribe
```

### Custom EventArgs

```python
class ValueChangedArgs(EventArgs):
    old_value: int
    new_value: int

    def __init__(self, old_value: int, new_value: int):
        self.old_value = old_value
        self.new_value = new_value

class Counter:
    event value_changed: (object, ValueChangedArgs) -> None
    _value: int = 0

    property value(self) -> int:
        return self._value

    property value(self, new_value: int):
        old = self._value
        self._value = new_value
        if self.value_changed is not None:
            self.value_changed(self, ValueChangedArgs(old, new_value))
```

**Event Rules:**
- Events can only be invoked from the declaring class
- `+=` subscribes, `-=` unsubscribes
- Multiple subscribers are called in subscription order

*Implementation: ✅ Native - `event EventHandler Name;`*

---

## Async Programming **[v1.0]**

### Async Functions

```python
async def fetch_data(url: str) -> str:
    await asyncio.sleep(1.0)
    return f"Data from {url}"

async def main():
    result = await fetch_data("https://example.com")
    print(result)
```

*Implementation: ✅ Native - `async` method returning `Task<T>`.*

### Concurrent Execution

```python
async def fetch_all(urls: list[str]) -> list[str]:
    tasks = [fetch_data(url) for url in urls]
    results = await asyncio.gather(*tasks)
    return results
```

*Implementation: ✅ Native - `Task.WhenAll()`*

### Async Iteration

```python
async def count_up(n: int):
    for i in range(n):
        await asyncio.sleep(0.1)
        yield i

async def process():
    async for num in count_up(5):
        print(f"Number: {num}")
```

*Implementation: ✅ Native - `IAsyncEnumerable<T>` (C# 8+)*

### Async Context Managers

```python
async def use_resource():
    async with AsyncResource() as resource:
        await resource.process()
```

*Implementation: 🔄 Lowered - `await using (var r = resource) { ... }`*

---

## Built-in Functions **[v0.1+]**

Built-in functions provide polymorphic access to type behavior. They work uniformly on all types—primitives, .NET types, and Sharpy-defined types—by internally dispatching to the appropriate implementation:

- **For Sharpy types**: If the type defines the corresponding dunder method, the built-in function calls it
- **For primitives and .NET types**: The built-in function uses the native .NET operation
- **Fallback behavior**: Some functions provide sensible defaults when no custom implementation exists

This design allows code like `len(x)`, `str(x)`, and `repr(x)` to work consistently regardless of whether `x` is a list, a string, or a custom class.

### Type Conversion [v0.1]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `int(x)` | Convert to integer | `(int)x` or `Convert.ToInt32(x)` |
| `double(x)` | Convert to double | `(double)x` |
| `str(x)` | Convert to string | Calls `__str__` if defined, else `.ToString()` |
| `bool(x)` | Convert to boolean | Truthiness check |

**`str(x)`** returns a human-readable string representation:
- For Sharpy types with `__str__`: calls `__str__`
- For all types: falls back to `.ToString()`

### Type Checking [v0.1]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `isinstance(x, T)` | Check type | `x is T` |
| `type(x)` | Get type | `x.GetType()` |

### Collection Functions [v0.1]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `len(x)` | Get length | Calls `__len__` if defined, else `.Count` or `.Length` |
| `min(iter)` | Minimum value | `.Min()` or `Math.Min()` |
| `max(iter)` | Maximum value | `.Max()` or `Math.Max()` |
| `sum(iter)` | Sum values | `.Sum()` |
| `sorted(iter)` | Sort collection | `.OrderBy()` |
| `reversed(iter)` | Reverse | `.Reverse()` |
| `enumerate(iter)` | Index + value | `.Select((x, i) => (i, x))` |
| `zip(a, b)` | Combine iterables | `.Zip()` |
| `range(n)` | Number sequence | `Enumerable.Range()` |
| `filter(pred, iter)` | Filter | `.Where()` |
| `map(func, iter)` | Transform | `.Select()` |
| `all(iter)` | All truthy | `.All()` |
| `any(iter)` | Any truthy | `.Any()` |

**`len(x)`** returns the number of items in a container:
- For Sharpy types with `__len__`: calls `__len__`
- For collections: uses `.Count` property
- For strings/arrays: uses `.Length` property

### I/O Functions [v0.1]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `print(x)` | Print to console | `Console.WriteLine()` |
| `input(prompt)` | Read from console | `Console.ReadLine()` |

### Mathematical Functions [v0.1]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `abs(x)` | Absolute value | `Math.Abs()` |
| `pow(x, y)` | Power | `Math.Pow()` |
| `round(x, n)` | Round | `Math.Round()` |
| `divmod(a, b)` | Quotient + remainder | `(a / b, a % b)` |

### Object Functions [v0.1]

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `repr(x)` | Debug representation | Calls `__repr__` if defined, else `__str__`, else `.ToString()` |
| `hash(x)` | Hash code | Calls `__hash__` if defined, else `.GetHashCode()` |
| `id(x)` | Object identity | `RuntimeHelpers.GetHashCode()` |

**`repr(x)`** returns a string representation suitable for debugging:
- For Sharpy types with `__repr__`: calls `__repr__`
- Fallback: tries `__str__`, then `.ToString()`
- Typically includes type name and distinguishing attributes

**`hash(x)`** returns the hash code for use in dictionaries and sets:
- For Sharpy types with `__hash__`: calls `__hash__`
- For all types: falls back to `.GetHashCode()`
- If `__eq__` is defined, `__hash__` must also be defined (and vice versa)

*Implementation: 🔄 Lowered - Generated as method calls or type-appropriate dispatch.*

---

## .NET Interop **[v0.1]**

### Importing .NET Types

```python
from system.collections.generic import List, Dictionary
from system.io import File, Path

# Use .NET types directly
items = List[int]()
items.add(42)

content = File.read_all_text("data.txt")
```

### .NET Properties

.NET properties accessed like Sharpy properties:

```python
from system.io import FileInfo

file = FileInfo("data.txt")
size = file.length
name = file.name
```

### Extension Methods

.NET extension methods work naturally:

```python
from system.linq import Enumerable

numbers = [1, 2, 3, 4, 5]
evens = numbers.where(lambda x: x % 2 == 0)
doubled = numbers.select(lambda x: x * 2)
```

### IDisposable Pattern

.NET's `IDisposable` integrates with `with`:

```python
from system.io import FileStream, FileMode

with FileStream("output.dat", FileMode.create) as stream:
    stream.write(data, 0, len(data))
```

---

## Naming Conventions Summary **[v0.1]**

| Identifier Type | Sharpy Convention | Compiled C# Form |
|-----------------|-------------------|------------------|
| Module | `snake_case` | `PascalCase` namespace |
| Class | `PascalCase` | (unchanged) |
| Struct | `PascalCase` | (unchanged) |
| Interface | `IPascalCase` | (unchanged) |
| Method/Function | `snake_case` | `PascalCase` |
| Parameter | `snake_case` | `camelCase` |
| Local variable | `snake_case` | (unchanged) |
| Constant | `CAPS_SNAKE_CASE` | (unchanged) |
| Enum type | `PascalCase` | (unchanged) |
| Enum value | `CAPS_SNAKE_CASE` | `PascalCase` |

---

## Program Entry Point **[v0.1]**

The entry point is either a file with top-level statements or a `main()` function:

```python
# Option 1: Top-level statements
print("Hello, World!")

# Option 2: main() function
def main():
    print("Hello, World!")
```

**Note:** The Python idiom `if __name__ == "__main__":` does not exist in Sharpy.

*Implementation: 🔄 Lowered*
- *Top-level statements wrapped in generated `Main()` method*
- *Module code wrapped in `public static class Exports`*

---

## Features Deferred to v2.0+

The following features require .NET 7+ runtime or C# 11+ and cannot be supported when targeting Unity or .NET 5/6:

| Feature | Required C# | Required .NET | Reason |
|---------|-------------|---------------|--------|
| `@file` access modifier | C# 11 | .NET 6+ | File-scoped types |
| List patterns `case [a, b]:` | C# 11 | Any | Compiler feature |
| Static abstract interface members | C# 11 | .NET 7 | Runtime support |
| Generic math constraints | C# 11 | .NET 7 | BCL interfaces |
| `required` members | C# 11 | .NET 7 | Attribute + compiler |
| Record structs | C# 10 | Any | Compiler feature |
| `field` keyword in properties | C# 13 | Any | Compiler feature |
| Extension properties/operators | C# 14 | Any | Compiler feature |
| User-defined `+=` operators | C# 14 | Any | Compiler feature |
| `yield` (generators) | v2.0 | Any | Deferred |
| `del` statement | v2.0 | Any | Deferred |

---

## Version Summary

| Version | Key Additions |
|---------|---------------|
| **v0.1** | Core syntax, primitives, functions, classes, exceptions, imports, type hierarchy (`object` base), dunder invocation rules |
| **v0.2** | Nullable types (`T?`), `?.`, `??`, collections, slicing |
| **v0.3** | Structs, interfaces, inheritance, decorators, access modifiers |
| **v0.4** | Generics, type constraints, lambdas |
| **v0.5** | Enums, operator overloading via dunders |
| **v0.6** | F-strings, extended literals, comparison chaining, loop else |
| **v0.7** | Pattern matching (`match`/`case`), guards, all pattern types |
| **v0.8** | Type aliases, tagged unions (ADTs), variable shadowing |
| **v0.9** | Comprehensions, walrus operator, properties |
| **v1.0** | Context managers, defer, events, async/await |
| **v2.0+** | Features requiring C# 11+ / .NET 7+ |

---

## See Also

- **Type System** - Detailed type semantics, interfaces, and generics
- **Compiler Design** - Implementation details and code generation
- **C# 9.0 Compatibility Matrix** - Full transpilation reference
