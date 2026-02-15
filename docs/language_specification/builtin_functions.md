# Built-in Functions

Built-in functions provide polymorphic access to type behavior. They work uniformly on all types—primitives, .NET types, and Sharpy-defined types—by internally dispatching to the appropriate implementation:

- **For Sharpy types**: If the type defines the corresponding dunder method, the built-in function calls it
- **For primitives and .NET types**: The built-in function uses the native .NET operation
- **Fallback behavior**: Some functions provide sensible defaults when no custom implementation exists

This design allows code like `len(x)`, `str(x)`, and `repr(x)` to work consistently regardless of whether `x` is a list, a string, or a custom class.

## Type Conversion

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `int(x)` | Convert to integer (32-bit) | `(int)x` or `Convert.ToInt32(x)` |
| `float(x)` | Convert to float (64-bit) | `(double)x` |
| `str(x)` | Convert to string | Calls `__str__` if defined, else `.ToString()` |
| `bool(x)` | Convert to boolean | Truthiness check |

### Result-Returning Variants

For user input and other expected-failure scenarios, the type conversion functions offer Result-returning variants via static `.parse()` methods:

```python
# Throwing version (Python-compatible)
n = int("42")  # Raises ValueError if invalid

# Result-returning version (recommended for user input)
result: Result[int, ValueError] = int.parse("42")
match result:
    case Ok(n):
        print(f"Parsed: {n}")
    case Err(e):
        print(f"Invalid input: {e}")

# Similarly for float
f: Result[float, ValueError] = float.parse("3.14")
```

**Guiding principle:** Use the throwing version (`int(x)`) when bad input is a bug. Use the Result version (`int.parse(x)`) when bad input is expected (e.g., user input).

**`str(x)`** returns a C# `string`:
- For all types, calls `.ToString()`
- Primitive overloads (`str(int)`, `str(double)`, etc.) avoid boxing

## Type Checking

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `isinstance(x, T)` | Check if `x` is an instance of type `T` | `x is T` |
| `type(x)` | Get runtime type of `x` | `x.GetType()` |

**`type(x)` Return Type:**

The `type()` function returns `System.Type`, the .NET reflection type:

```python
from system import Type

x = 42
t: Type = type(x)        # Returns System.Int32 type
print(t.name)            # "Int32"
print(t.full_name)       # "System.Int32"

# Type comparison
if type(x) == type(0):
    print("x is an integer")

# Prefer isinstance() for type checks
if isinstance(x, int):   # More idiomatic
    print("x is an integer")
```

**Note:** Unlike Python where `type(None)` returns `NoneType`, Sharpy's `type(None)` is a compile-time error because `None` is not a value with a type.

**`type()` on Primitive Literals:**

Unlike `type(None)`, calling `type()` on primitive literals is valid and returns the corresponding `System.Type`:

```python
# All of these are valid
t1 = type(42)        # System.Int32
t2 = type(3.14)      # System.Double
t3 = type("hello")   # System.String
t4 = type(True)      # System.Boolean
t5 = type([1, 2, 3]) # Sharpy.Core.List`1[System.Int32]

# Only type(None) is an error
t6 = type(None)      # ERROR: type(None) is not valid
```

This is because primitive literals are values with concrete runtime types, whereas `None` represents the absence of a value.

**`isinstance(x, T)`**

Checks whether `x` is an instance of type `T` at runtime. Returns `True` if `x` is an instance of `T` or any subclass of `T`.

```python
value: object = get_value()

if isinstance(value, str):
    # value is narrowed to str in this block
    print(value.upper())

if isinstance(value, MyClass):
    # value is narrowed to MyClass
    value.my_method()

# Works with interfaces too
if isinstance(value, IDrawable):
    # value is narrowed to IDrawable
    value.draw()
```

**Single Type Only:**

Unlike Python's `isinstance()` which accepts a tuple of types, Sharpy's `isinstance()` only accepts a single type argument. Sharpy does not have union types.

```python
# ✅ Valid - single type
if isinstance(x, int):
    pass

if isinstance(x, IDrawable):
    pass

# ❌ Invalid - multiple types not supported
if isinstance(x, (int, str)):      # ERROR: isinstance() takes exactly one type argument
    pass

if isinstance(x, int | str):       # ERROR: union types not supported
    pass
```

**To check multiple types**, use explicit `or`:

```python
if isinstance(x, int) or isinstance(x, str):
    # x could be int or str here
    # Note: no automatic type narrowing in this case
    pass
```

**Generic Type Limitation:**

Due to .NET type erasure for generics at runtime, `isinstance()` cannot check generic type arguments:

```python
# ✅ Valid - checks if x is any List<T>
if isinstance(x, list):
    pass  # x could be list[int], list[str], etc.

# ❌ Compile error - cannot check generic type arguments at runtime
if isinstance(x, list[int]):       # ERROR: Cannot check generic type arguments at runtime
    pass

if isinstance(x, dict[str, int]):  # ERROR: Cannot check generic type arguments at runtime
    pass
```

This limitation matches C#'s `is` operator behavior. At runtime, `List<int>` and `List<str>` are both just `List<T>`—the generic type argument is erased.

**Type Narrowing:**

When `isinstance()` is used in a conditional, the variable's type is narrowed within that branch:

```python
def process(value: object) -> str:
    if isinstance(value, str):
        return value.upper()      # OK: value is str
    if isinstance(value, int):
        return str(value * 2)     # OK: value is int
    return "unknown"
```

*Implementation: ✅ Native - Maps to C# `is` pattern matching with type narrowing.*

## Collection Functions

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `len(x)` | Get length | Calls `__len__` if defined, else `.Count` or `.Length` |
| `min(iter)` | Minimum value | `.Min()` or `Math.Min()` |
| `max(iter)` | Maximum value | `.Max()` or `Math.Max()` |
| `sum(iter)` | Sum values | `.Sum()` |
| `sorted(iter)` | Sort collection | `.OrderBy()` |
| `reversed(iter)` | Reverse | `.Reverse()` |
| `enumerate(iter)` | Index + value | `.Select((x, i) => (i, x))` |

**`enumerate()` Signature:**

The `enumerate()` function matches Python's signature:

```python
enumerate(iterable, start=0)
```

| Form | Description |
|------|-------------|
| `enumerate(items)` | Indices start at 0 |
| `enumerate(items, start=1)` | Indices start at 1 |
| `enumerate(items, start=n)` | Indices start at n |

```python
names = ["Alice", "Bob", "Charlie"]

# Default: start at 0
for i, name in enumerate(names):
    print(f"{i}: {name}")  # 0: Alice, 1: Bob, 2: Charlie

# Start at 1 (useful for 1-based numbering)
for i, name in enumerate(names, start=1):
    print(f"{i}. {name}")  # 1. Alice, 2. Bob, 3. Charlie
```

*Implementation: 🔄 Lowered - `.Select((x, i) => (i + start, x))`.*

| `zip(a, b)` | Combine iterables | `.Zip()` |
| `range(n)` | Number sequence | `Enumerable.Range()` |

**`range()` Signature:**

The `range()` function matches Python's signature exactly:

| Form | Description | Example |
|------|-------------|---------|
| `range(stop)` | 0 to stop-1 | `range(5)` → 0, 1, 2, 3, 4 |
| `range(start, stop)` | start to stop-1 | `range(2, 5)` → 2, 3, 4 |
| `range(start, stop, step)` | start to stop-1, by step | `range(0, 10, 2)` → 0, 2, 4, 6, 8 |

```python
# Single argument: 0 to n-1
for i in range(5):
    print(i)  # 0, 1, 2, 3, 4

# Two arguments: start to stop-1
for i in range(2, 7):
    print(i)  # 2, 3, 4, 5, 6

# Three arguments: start to stop-1, stepping by step
for i in range(0, 10, 2):
    print(i)  # 0, 2, 4, 6, 8

# Negative step for countdown
for i in range(10, 0, -1):
    print(i)  # 10, 9, 8, 7, 6, 5, 4, 3, 2, 1
```

*Implementation: 🔄 Lowered - Simple forms use `for (int i = start; i < stop; i += step)`, complex forms use `Enumerable.Range()` or generator.*

| `filter(pred, iter)` | Filter | `.Where()` |
| `map(func, iter)` | Transform | `.Select()` |
| `all(iter)` | All truthy | `.All()` |
| `any(iter)` | Any truthy | `.Any()` |

**`len(x)`** returns the number of items in a container:
- For Sharpy types with `__len__`: calls `__len__`
- For collections: uses `.Count` property
- For strings/arrays: uses `.Length` property

## I/O Functions

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `print(x)` | Print to console | `Console.WriteLine()` |
| `input(prompt)` | Read from console | `Console.ReadLine()` |

## Mathematical Functions

| Function | Purpose | C# Mapping |
|----------|---------|------------|
| `abs(x)` | Absolute value | `Math.Abs()` |
| `pow(x, y)` | Power | `Math.Pow()` |
| `round(x, n)` | Round | `Math.Round()` |
| `divmod(a, b)` | Quotient + remainder | `(a / b, a % b)` |

**`divmod()` Return Types:**

The `divmod()` function returns a tuple containing the quotient and remainder. The return type depends on the operand types, following the same numeric promotion rules as `/` and `//`:

| Operand Types | Return Type | Notes |
|---------------|-------------|-------|
| Both `int` (32-bit) | `tuple[int, int]` | Integer division and modulo |
| Any `int64` | `tuple[int64, int64]` | Promoted to int64 |
| Any `float32`/`float64` | `tuple[float64, float64]` | Float division |
| Any `decimal` | `tuple[decimal, decimal]` | Decimal division |

```python
divmod(17, 5)       # (3, 2) - tuple[int, int]
divmod(17L, 5)      # (3L, 2L) - tuple[int64, int64]
divmod(17.0, 5.0)   # (3.0, 2.0) - tuple[float, float]
divmod(17.0m, 5.0m) # (3.0m, 2.0m) - tuple[decimal, decimal]
```

## Object Functions

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

**Hashing Tuples:**

Tuples are automatically hashable if all their elements are hashable:

```python
# Tuples of hashable types can be hashed
point = (10, 20)
h = hash(point)          # OK: both int elements are hashable

# Use tuples to create composite hash keys
coord_to_name: dict[tuple[int, int], str] = {}
coord_to_name[(0, 0)] = "origin"
coord_to_name[(10, 20)] = "point A"

# Nested tuples work if all elements hashable
nested = ((1, 2), (3, 4))
h = hash(nested)         # OK

# Tuples containing unhashable types cannot be hashed
bad = ([1, 2], [3, 4])   # Tuple containing lists
h = hash(bad)            # ERROR: list is not hashable
```

*Implementation: 🔄 Lowered - Generated as method calls or type-appropriate dispatch.*

---
