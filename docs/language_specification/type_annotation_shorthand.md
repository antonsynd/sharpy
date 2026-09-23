# Type Annotation Shorthand Syntax

Sharpy provides shorthand syntax for common collection type annotations, inspired by Swift and TypeScript. These are pure syntactic sugar - the parser normalizes them to the canonical AST representation, so both forms are semantically identical.

## Overview

| Shorthand | Canonical | Description |
|-----------|-----------|-------------|
| `[T]` | `list[T]` | List type |
| `{T}` | `set[T]` | Set type |
| `{K: V}` | `dict[K, V]` | Dictionary type |
| `()` | `tuple[()]` | Empty/unit tuple |
| `(T)` | `tuple[T]` | Single-element tuple |
| `(T,)` | `tuple[T]` | Single-element tuple (explicit) |
| `(T, U)` | `tuple[T, U]` | Multi-element tuple |
| `T[]` | array | .NET array (postfix syntax) |

## List Shorthand

The `[T]` syntax is shorthand for `list[T]`:

```python
# These are equivalent:
items: [int] = [1, 2, 3]
items: list[int] = [1, 2, 3]

# Function parameters
def process(data: [str]) -> [str]:
    return [s.upper() for s in data]

# Equivalent to (the same signature, shown under another name):
def process_canonical(data: list[str]) -> list[str]:
    return [s.upper() for s in data]
```

### Nested Lists

```python
# List of lists
matrix: [[int]] = [[1, 2], [3, 4]]

# Equivalent to:
matrix: list[list[int]] = [[1, 2], [3, 4]]
```

## Set Shorthand

The `{T}` syntax is shorthand for `set[T]`:

```python
# These are equivalent:
unique_ids: {int} = {1, 2, 3}
unique_ids: set[int] = {1, 2, 3}

# Function using set types
def unique_items(items: [str]) -> {str}:
    return set(items)
```

## Dict Shorthand

The `{K: V}` syntax is shorthand for `dict[K, V]`:

```python
# These are equivalent:
scores: {str: int} = {"alice": 100, "bob": 85}
scores: dict[str, int] = {"alice": 100, "bob": 85}

# Complex key/value types
cache: {str: [int]} = {"evens": [2, 4, 6]}

# Equivalent to:
cache: dict[str, list[int]] = {"evens": [2, 4, 6]}
```

### Distinguishing Set vs Dict

- `{T}` (single type) → `set[T]`
- `{K: V}` (colon separating two types) → `dict[K, V]`

```python
x: {int}        # set[int]
y: {str: int}   # dict[str, int]
```

## Tuple Shorthand

Parentheses can be used for tuple types:

```python
# Empty tuple (unit type)
unit: () = ()

# Single-element tuple
single: (int) = (42,)
single: (int,) = (42,)   # Trailing comma also accepted

# Multi-element tuple
point: (int, int) = (10, 20)
record: (str, int, bool) = ("Alice", 30, True)

# Trailing comma is optional for multi-element
point: (int, int,) = (10, 20)  # Also valid
```

### Single-Element Tuples

Both `(T)` and `(T,)` represent a single-element tuple in type annotation context:

```python
# Both are tuple[int]:
x: (int) = (42,)
y: (int,) = (42,)
```

**Note:** In expression context, `(x)` is grouping while `(x,)` is a single-element tuple. In *type* annotation context, there is no ambiguity - `(T)` always means tuple. This applies to all type positions, including `isinstance`'s second argument: `isinstance(x, (int, str))` tests `tuple[int, str]`, not any-of.

### The Empty Tuple

`()` and `tuple[()]` are two spellings of the same zero-arity tuple type in every annotation position — return, parameter, variable, a type argument (`list[tuple[()]]`), `?`, and nested (`tuple[tuple[()]]` is a one-element tuple whose element is the empty tuple). The same holds for a type argument written in expression position: `list[()]()` and `list[tuple[()]]()` construct a list of empty tuples, `Box[()](())` and `ident[tuple[()]](())` bind `T` to the empty tuple, and `dict[str, ()]()` takes it as an element. `isinstance(x, tuple[()])` tests for the empty tuple, but neither spelling is yet accepted as the operand of `as?`/`as!` or as a `case` pattern head ([#1981](https://github.com/antonsynd/sharpy/issues/1981)):

```spy
def unit() -> tuple[()]:
    return ()

def main() -> None:
    a: () = ()
    b: tuple[()] = ()                  # the canonical spelling of the same type
    nested: tuple[tuple[()]] = ((),)   # one element: the empty tuple
    print(len(a), len(b), len(unit()), len(nested))   # 0 0 0 1
```

`tuple[]` is not the empty tuple: a postfix `[]` is the [array suffix](#array-shorthand), so `tuple[]` is an array whose element type is the bare `tuple`.

### Function Types vs Tuple Shorthand

The presence of `->` distinguishes function types from tuple shorthand:

```python
# Function type (has ->)
callback: (int) -> str

# Tuple type (no ->)
single: (int)
```

## Array Shorthand

The postfix `[]` syntax creates .NET array types:

```python
# These represent .NET arrays (System.Array)
buffer: int[]
matrix: int[][]      # Array of arrays

# Can combine with other shorthand
list_array: [int][]  # Array of list[int]
```

## Nullability and Result Syntax

### Optional (`T?`)

The `T?` suffix creates an `Optional[T]` (safe tagged union):

```python
name: str? = Some("Alice")
empty: int? = None()
```

All shorthand forms support the `?` suffix:

```python
items: [int]? = None()       # Optional[list[int]]
unique: {str}? = None()      # Optional[set[str]]
lookup: {str: int}? = None() # Optional[dict[str, int]]
pair: (int, str)? = None()   # Optional[tuple[int, str]]
buffer: int[]? = None()      # Optional[int[]]
```

### C# Nullable (`T | None`)

The `T | None` suffix marks a type as C# nullable (for .NET interop):

```python
from System import Environment

raw: str | None = Environment.GetEnvironmentVariable("HOME")
```

**Note:** `| None` is the only valid inline union. Free unions like `int | str` are not supported. Use `union` declarations for custom sum types.

All shorthand forms support `| None`:

```python
items: [int] | None = None      # list[int] | None (C# nullable)
lookup: {str: int} | None = None
data: int[] | None = None
```

### Result Type (`T !E`)

The `T !E` suffix creates a `Result[T, E]`:

```python
def parse(s: str) -> int !ValueError:
    ...
```

All shorthand forms support `!E`:

```python
parsed: [int] !Error = Ok([1, 2, 3])
lookup: {str: int} !IOError = Err(IOError("file not found"))
```

### Precedence

`!E` binds tighter than `| None`:

```python
r: int !ValueError | None = None  # Result[int, ValueError] | None
```

## Nesting

Shorthand forms can be freely nested:

```python
# List of tuples
pairs: [(int, str)] = [(1, "a"), (2, "b")]

# Dict with list values
grouped: {str: [int]} = {"evens": [2, 4], "odds": [1, 3]}

# Set of tuples
coordinates: {(int, int)} = {(0, 0), (1, 1)}

# Complex nesting
data: {str: [(int, bool)]} = {"flags": [(1, True), (2, False)]}
```

## Mixed Syntax

Both shorthand and canonical forms can be used in the same codebase. They produce identical AST:

```python
# These are equivalent and can coexist:
def process(items: [int], lookup: dict[str, int]) -> {str}:
    ...
```

## Formatting Conventions

**Recommended conventions:**

1. **Single-element tuples:** Use `(T,)` with trailing comma for clarity
2. **Multi-element tuples:** Trailing comma optional, but recommended for multi-line
3. **Consistency:** Choose one style (shorthand or canonical) and use it consistently within a file

```python
# Single-element tuple - trailing comma recommended for clarity
x: (int,) = (42,)

# Multi-line tuple - trailing comma recommended
point: (
    int,
    int,
    int,
) = (1, 2, 3)
```

## Error Cases

<!-- spec-sweep: error SPY0114 -->
```python
# ERROR: Empty list type requires element type
x: [] = []  # Invalid - use list or specify element type

# ERROR: Empty braces are ambiguous
x: {} = {}  # Invalid - use dict or set explicitly
```

## Implementation Notes

- All shorthand forms are parsed and normalized to the canonical AST representation
- Type checking, code generation, and other compiler phases work with canonical types
- The shorthand is purely syntactic sugar with no semantic differences
- Source position tracking includes the shorthand syntax for accurate error reporting
