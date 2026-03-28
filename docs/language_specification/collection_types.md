# Collection Types

| Sharpy Type | Shorthand | .NET Type | Notes |
|-------------|-----------|-----------|-------|
| `list[T]` | `[T]` | `Sharpy.Core.List<T>` | Mutable list |
| `dict[K, V]` | `{K: V}` | `Sharpy.Core.Dict<K, V>` | Hash map |
| `set[T]` | `{T}` | `Sharpy.Core.Set<T>` | Unique elements |
| `tuple[T1, T2, ...]` | `(T1, T2, ...)` | `System.ValueTuple<T1, T2, ...>` | Fixed-size tuple; supports [positional access](#tuple-positional-access) |

With the exception of `tuple[...]`, Sharpy collection types use custom Pythonic wrappers around the corresponding .NET collection types.

## Shorthand Syntax

All collection types support [shorthand syntax](type_annotation_shorthand.md) for more concise type annotations:

```python
# These pairs are equivalent:
items: [int]           # items: list[int]
scores: {str: int}     # scores: dict[str, int]
unique: {int}          # unique: set[int]
point: (int, int)      # point: tuple[int, int]
```

## Optional and Error Handling Conventions

Collection access follows these conventions for optionality and error handling:

| Operation | Return Type | Behavior |
|-----------|------------|----------|
| `dict.get(key: K)` | `V?` | Returns `Some(value)` or `None()` |
| `dict[key]` | `V` | Throws `KeyError` if missing |
| `list[i]` | `T` | Throws `IndexError` if out of bounds |
| `list.get(index: int)` | `T?` | Returns `Some(value)` or `None()` |

```python
d: dict[str, int] = {"x": 1, "y": 2}

# Safe access - returns Optional
val: int? = d.get("x")      # Some(1)
val: int? = d.get("z")      # None()

# Direct access - throws on missing key
val: int = d["x"]           # 1
val: int = d["z"]           # KeyError

# List safe access
items: list[str] = ["a", "b", "c"]
item: str? = items.get(0)   # Some("a")
item: str? = items.get(99)  # None()

# List direct access - throws on out of bounds
item: str = items[0]        # "a"
item: str = items[99]       # IndexError
```

## Tuple Positional Access

Tuple elements can be accessed by position using integer literal subscript syntax:

```python
point: tuple[int, int, int] = (10, 20, 30)
x = point[0]   # 10
y = point[1]   # 20
z = point[2]   # 30
```

Named tuples also support positional access alongside named access:

```python
type Point = tuple[x: float, y: float]
p: Point = (x=3.0, y=4.0)
print(p[0])    # 3.0 (same as p.x)
print(p[1])    # 4.0 (same as p.y)
```

### Restrictions

| Rule | Behavior |
|------|----------|
| Indices must be integer literals | Variable indices are not supported (e.g., `t[i]` where `i` is a variable) |
| No negative indices | `t[-1]` produces a compile-time error (Python divergence) |
| Compile-time bounds checking | `t[3]` on a 3-element tuple produces a compile-time error |

### Python Divergence

Unlike Python, Sharpy does not support negative tuple indices. This is because tuple positional access is resolved at compile time (lowered to `.Item1`, `.Item2`, etc.), and negative indexing would require runtime tuple length information.

*Implementation*
- *🔄 Lowered - `tuple[i]` is lowered to `.Item{i+1}` (e.g., `tuple[0]` → `.Item1`, `tuple[1]` → `.Item2`).*

---

*Implementation*
- *🔄 Lowered - Sharpy collections are aliases to the corresponding .NET collections.*
