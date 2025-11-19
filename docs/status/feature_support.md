# Sharpy Feature Support

## 1. Control flow

### 1.1. Conditionals

| Feature | Version | Notes |
| - | - | - |
| `case` | - | Reserved keyword but not implemented |
| `if` | 0.1+ | Fully implemented with type checking |
| `elif` | 0.1+ | Fully implemented with type checking |
| `else` | 0.1+ | Fully implemented with type checking |
| `match` | - | Reserved keyword but not implemented |

### 1.2. Iteration

| Feature | Version | Notes |
| - | - | - |
| `continue` | 0.1+ | Fully implemented |
| `break` | 0.1+ | Fully implemented |
| `else` | 0.1+ | Implemented for loops |
| `for..in` | 0.1+ | Fully implemented with type checking |
| `while` | 0.1+ | Fully implemented with type checking |

### 1.3. Exception handling

| Feature | Version | Notes |
| - | - | - |
| `except` | 0.1+ | Parsing implemented, semantic analysis partial |
| `finally` | 0.1+ | Parsing implemented, semantic analysis partial |
| `raise` | 0.1+ | Parsing implemented, semantic analysis partial |
| `try` | 0.1+ | Parsing implemented, semantic analysis partial |

### 1.4. Context managers

| Feature | Version | Notes |
| - | - | - |
| `with` | 0.1+ | Parsing implemented, semantic analysis partial |

## 2. Expressions

| Feature | Version | Notes |
| - | - | - |
| `return` | 0.1+ | Fully implemented with type checking |
| `x if y else z` | - | Not implemented |
| `yield` | 0.1+ | Parsing implemented, semantic analysis not complete |

## 3. Type system

### 3.1. Core objects

| Feature | Version | Notes |
| - | - | - |
| `class` | 0.1+ | Fully implemented with inheritance and access modifiers |
| `protocol` | 0.1+ | Fully implemented with inheritance |
| `struct` | 0.1+ | Fully implemented |

### 3.2. Polymorphism

| Feature | Version | Notes |
| - | - | - |
| Class inheritance | 0.1+ | Single inheritance fully implemented |
| Protocol implementation | 0.1+ | Multiple protocol implementation supported |
| Method overriding | 0.1+ | Parsing implemented, semantic validation partial |

### 3.3. Generic programming

| Feature | Version | Notes |
| - | - | - |
| Concrete parameters | 0.1+ | Basic generic syntax implemented |
| Protocol parameters | 0.1+ | Basic generic syntax implemented |
| Constraints on parameters | - | Not implemented |

### 3.4. Functions

| Feature | Version | Notes |
| - | - | - |
| `def` | 0.1+ | Fully implemented with type annotations |
| `decl` | - | Not implemented |
| `get` | 0.1+ | Implemented for property definitions |
| `set` | 0.1+ | Implemented for property definitions |

### 3.5. Properties

| Feature | Version | Notes |
| - | - | - |
| Auto properties | 0.1+ | Fully implemented with access modifiers |
| Explicit properties | 0.1+ | Fully implemented with getter/setter methods |
| Read-only properties | 0.1+ | `get property` syntax implemented |
| Write-only properties | 0.1+ | `set property` syntax implemented |

### 3.6. Access modifiers

| Feature | Version | Notes |
| - | - | - |
| `@public` | 0.1+ | Fully implemented (default) |
| `@protected` | 0.1+ | Fully implemented |
| `@private` | 0.1+ | Fully implemented |
| `@internal` | 0.1+ | Fully implemented |
| `@file` | 0.1+ | Fully implemented |
| Underscore naming hints | 0.1+ | `_protected` and `__private` conventions supported |

### 3.7. Semantic analysis

| Feature | Version | Notes |
| - | - | - |
| Multi-pass analysis | 0.1+ | 3-pass system: Declaration → Import → Type |
| Type inference | 0.1+ | Assignment, expression, and return type inference |
| Symbol resolution | 0.1+ | Cross-scope and cross-module symbol lookup |
| Type checking | 0.1+ | Comprehensive type compatibility checking |
| Attribute access analysis | 0.1+ | Method/property resolution on builtin types |
| Function call validation | 0.1+ | Argument and return type checking |
| Error reporting | 0.1+ | Detailed error messages with source locations |

### 3.8. Asynchronous code

| Feature | Version | Notes |
| - | - | - |
| `async def` | 0.1+ | Keywords reserved, not fully implemented |
| `await` | 0.1+ | Keywords reserved, not fully implemented |

## 4. Standard library

### 4.1. Built-in types

| Feature | Version | Notes |
| - | - | - |
| `bool` | 0.1+ | Fully implemented with semantic analysis |
| `byte` | 0.1+ | Basic type support |
| `bytearray` | 0.1+ | Basic type support |
| `bytes` | 0.1+ | Basic type support |
| `complex` | 0.1+ | Basic type support |
| `dict[K, V]` | 0.1+ | Fully implemented with generic support |
| `double` | 0.1+ | Basic type support |
| `decimal` | 0.1+ | Basic type support |
| `Exception` | 0.1+ | Basic type support |
| `float` | 0.1+ | Fully implemented with semantic analysis |
| `frozenset[T]` | 0.1+ | Basic generic type support |
| `int` | 0.1+ | Fully implemented with semantic analysis |
| `Iterable[T]` | 0.1+ | Basic generic type support |
| `Iterator[T]` | 0.1+ | Basic generic type support |
| `list[T]` | 0.1+ | Fully implemented with generic support and method resolution |
| `long` | 0.1+ | Basic type support |
| `memoryview` | 0.1+ | Basic type support |
| `None` | 0.1+ | Fully implemented |
| `range` | 0.1+ | Basic type support |
| `sbyte` | 0.1+ | Basic type support |
| `set[T]` | 0.1+ | Fully implemented with generic support |
| `short` | 0.1+ | Basic type support |
| `slice` | 0.1+ | Basic type support |
| `str` | 0.1+ | Fully implemented with method resolution (upper, lower, split, etc.) |
| `tuple[...]` | 0.1+ | Basic tuple type support |
| `uint` | 0.1+ | Basic type support |
| `ulong` | 0.1+ | Basic type support |
| `ushort` | 0.1+ | Basic type support |

### 4.2. Built-in exceptions

| Feature | Version | Notes |
| - | - | - |
| `ArgumentError` | 0.1+ | Basic type support |
| `IndexError` | 0.1+ | Basic type support |
| `StopIteration` | 0.1+ | Basic type support |
| `TypeError` | 0.1+ | Basic type support |
| `AttributeError` | 0.1+ | Used in semantic analysis for invalid attribute access |

### 4.3. Built-in constants

| Feature | Version | Notes |
| - | - | - |
| `...` | 0.1+ | Ellipsis literal fully implemented |
| `None` | 0.1+ | Fully implemented |
| `True` | 0.1+ | Fully implemented |
| `False` | 0.1+ | Fully implemented |

### 4.4. Built-in functions

For global functions that serve as constructor/conversion methods for built-in
types, see Built-in types in section 4.1.

| Feature | Version | Notes |
| - | - | - |
| `abs` | 0.1+ | Parsing support, semantic integration partial |
| `aiter` | - | Not implemented |
| `all` | - | Not implemented |
| `anext` | - | Not implemented |
| `any` | - | Not implemented |
| `ascii` | - | Not implemented |
| `bin` | - | Not implemented |
| `callable` | - | Not implemented |
| `chr` | - | Not implemented |
| `divmod` | - | Not implemented |
| `enumerate` | - | Not implemented |
| `filter` | - | Not implemented |
| `format` | - | Not implemented |
| `hash` | - | Not implemented |
| `hex` | - | Not implemented |
| `id` | 0.1+ | Parsing support, semantic integration partial |
| `input` | - | Not implemented |
| `isinstance` | - | Not implemented |
| `issubclass` | - | Not implemented |
| `iter` | 0.1+ | Parsing support, semantic integration partial |
| `len` | 0.1+ | Parsing support, semantic integration needed |
| `map` | - | Not implemented |
| `max` | 0.1+ | Parsing support, semantic integration partial |
| `min` | 0.1+ | Parsing support, semantic integration partial |
| `next` | 0.1+ | Parsing support, semantic integration partial |
| `oct` | 0.1+ | Parsing support, semantic integration partial |
| `open` | - | Not implemented |
| `ord` | 0.1+ | Parsing support, semantic integration partial |
| `pow` | 0.1+ | Parsing support, semantic integration partial |
| `print` | 0.1+ | Parsing support, semantic integration needed |
| `repr` | 0.1+ | Parsing support, semantic integration partial |
| `reversed` | 0.1+ | Parsing support, semantic integration partial |
| `round` | 0.1+ | Parsing support, semantic integration partial |
| `sorted` | 0.1+ | Parsing support, semantic integration partial |
| `sum` | 0.1+ | Parsing support, semantic integration partial |
| `super` | - | Not implemented |
| `type` | 0.1+ | Reserved as soft keyword |
| `zip` | - | Not implemented |

## 5. Language Features

### 5.1. Literals and Collections

| Feature | Version | Notes |
| - | - | - |
| Number literals | 0.1+ | Integer, float, imaginary fully supported |
| String literals | 0.1+ | Regular, raw, byte strings fully supported |
| F-string literals | 0.1+ | Formatted string literals with expression interpolation |
| List literals | 0.1+ | `[1, 2, 3]` syntax fully implemented |
| Dict literals | 0.1+ | `{"a": 1, "b": 2}` syntax fully implemented |
| Set literals | 0.1+ | `{1, 2, 3}` syntax fully implemented |
| Tuple literals | 0.1+ | `(1, 2, 3)` syntax fully implemented |

### 5.2. Operators

| Feature | Version | Notes |
| - | - | - |
| Arithmetic operators | 0.1+ | `+`, `-`, `*`, `/`, `//`, `%`, `**` with type checking |
| Comparison operators | 0.1+ | `==`, `!=`, `<`, `>`, `<=`, `>=` with type checking |
| Logical operators | 0.1+ | `and`, `or`, `not` fully implemented |
| Bitwise operators | 0.1+ | `&`, `|`, `^`, `~`, `<<`, `>>` basic support |
| Assignment operators | 0.1+ | `+=`, `-=`, `*=`, etc. basic support |
| Optional chaining | 0.1+ | `?.` lexed but not implemented |
| Null coalescing | 0.1+ | `??` lexed but not implemented |
| Matrix multiplication | 0.1+ | `@` operator lexed and parsed |

### 5.3. Import System

| Feature | Version | Notes |
| - | - | - |
| `import module` | 0.1+ | Fully implemented with semantic analysis |
| `from module import name` | 0.1+ | Fully implemented with semantic analysis |
| `import module as alias` | 0.1+ | Fully implemented with semantic analysis |
| `from module import name as alias` | 0.1+ | Fully implemented with semantic analysis |
| `from module import *` | 0.1+ | Fully implemented with semantic analysis |

### 5.4. Advanced Features

| Feature | Version | Notes |
| - | - | - |
| Lambda expressions | 0.1+ | Fully implemented with type inference |
| Decorators | 0.1+ | Basic decorator syntax, access modifiers implemented |
| List comprehensions | 0.1+ | Basic parsing support |
| Generator expressions | 0.1+ | Basic parsing support |
| Context managers | 0.1+ | `with` statement parsing, semantic analysis partial |

## 6. Known Limitations

### 6.1. Variable Scoping

**Issue**: Sharpy currently implements Python-style variable hoisting within function scopes. Variables declared inside control flow blocks (if/while/for) are accessible outside those blocks.

**Expected Behavior**: As a statically-typed language, Sharpy should follow C#-style block scoping where variables declared within a block are only accessible within that block and its nested blocks.

**Example of Current (Incorrect) Behavior**:
```python
def foo(x: int):
    if x > 0:
        result: str = "positive"  # Declared in if-block
    print(result)  # Currently accessible (should error)
```

**Workaround**: Declare variables in the outer scope before the control flow block:
```python
def foo(x: int):
    result: str = ""  # Declare in outer scope
    if x > 0:
        result = "positive"  # Assign in if-block
    print(result)  # Valid
```

**Status**: Documented in tests as skipped test cases. See `SemanticAnalyzerNegativeTests.cs` for examples.

**Priority**: Medium - This is a language design issue that should be addressed to ensure Sharpy maintains its statically-typed nature and doesn't inherit Python's dynamic scoping behavior.
