# Generics

## Generic Classes

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

*Implementation*
- *✅ Native - `class Box<T>`*

## Generic Functions

```python
def identity[T](value: T) -> T:
    return value

def first[T](items: list[T]) -> T:
    return items[0]
```

*Implementation*
- *✅ Native - `T Identity<T>(T value)`*

## Generic Function Instantiation

Generic functions support both type inference and explicit type arguments at call sites:

**Type inference (preferred):**
```python
# Type inferred from argument
result = identity(42)           # T inferred as int
name = identity("hello")        # T inferred as str

items: list[int] = [1, 2, 3]
x = first(items)                # T inferred as int from list[int]
```

**Explicit type arguments:**
```python
# Explicitly specify type parameter
result = identity[int](42)      # T explicitly int
name = identity[str]("hello")   # T explicitly str

# Useful when inference is ambiguous or impossible
empty = create_empty_list[int]()  # No argument to infer from
```

**Multiple type parameters:**
```python
def convert[T, U](value: T, converter: (T) -> U) -> U:
    return converter(value)

# Explicit type arguments for multiple parameters
result = convert[str, int]("42", int.parse)

# Can also infer from arguments
result = convert("42", int.parse)  # T=str, U=int inferred
```

**Partial type argument specification is not supported:**
```python
# ❌ Cannot specify only some type parameters
result = convert[str](...)  # ERROR: must specify all or none
```

*Implementation*
- *✅ Native - `identity<int>(42)` in C#*
- *Type arguments use `[]` in Sharpy, lowered to `<>` in C#*

## Type Constraints

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

*Implementation*
- *✅ Native - Direct mapping to C# generic constraints.*

## Multiple Type Constraints

A type parameter can have multiple constraints using the `&` syntax:

```python
# Single constraint
def compare[T: IComparable[T]](a: T, b: T) -> int:
    return a.compare_to(b)

# Multiple constraints with &
def sort_and_hash[T: IComparable[T] & IHashable](items: list[T]) -> int:
    """Sort items and return combined hash."""
    sorted_items = sorted(items)
    return hash(tuple(sorted_items))

# Multiple constraints on class
class SortedSet[T: IComparable[T] & IEquatable[T]]:
    _items: list[T]

    def add(self, item: T):
        # Can use both comparison and equality
        pass
```

**Constraint combinations:**

| Sharpy Syntax | C# Equivalent |
|---------------|---------------|
| `T: IFoo` | `where T : IFoo` |
| `T: IFoo & IBar` | `where T : IFoo, IBar` |
| `T: class & IFoo` | `where T : class, IFoo` |
| `T: struct & IFoo` | `where T : struct, IFoo` |
| `T: class & IFoo & IBar` | `where T : class, IFoo, IBar` |

For variance annotations on type parameters (`out T` for covariance, `in T` for contravariance), see [Generic Variance](generic_variance.md).

**Order matters for `class`/`struct`:**

When combining `class` or `struct` with interface constraints, `class`/`struct` should come first:

```python
# ✅ Preferred: class/struct first
def process[T: class & IDisposable](item: T): ...

# ✅ Also valid: interfaces only
def process[T: IDisposable & ICloneable](item: T): ...

# The compiler reorders constraints to match C# requirements
```

*Implementation*
- *✅ Native - `&` constraints lower to comma-separated C# `where` clause*
- *`T: IFoo & IBar` → `where T : IFoo, IBar`*
