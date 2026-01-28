# Collection Types

| Sharpy Type | Shorthand | .NET Type | Notes |
|-------------|-----------|-----------|-------|
| `list[T]` | `[T]` | `Sharpy.Core.List<T>` | Mutable list |
| `dict[K, V]` | `{K: V}` | `Sharpy.Core.Dict<K, V>` | Hash map |
| `set[T]` | `{T}` | `Sharpy.Core.Set<T>` | Unique elements |
| `tuple[T1, T2, ...]` | `(T1, T2, ...)` | `System.ValueTuple<T1, T2, ...>` | Fixed-size tuple |

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
| `dict.get(key: K)` | `V?` | Returns `Some(value)` or `Nothing` |
| `dict[key]` | `V` | Throws `KeyError` if missing |
| `list[i]` | `T` | Throws `IndexError` if out of bounds |
| `list.get(index: int)` | `T?` | Returns `Some(value)` or `Nothing` |

```python
d: dict[str, int] = {"x": 1, "y": 2}

# Safe access - returns Optional
val: int? = d.get("x")      # Some(1)
val: int? = d.get("z")      # Nothing

# Direct access - throws on missing key
val: int = d["x"]           # 1
val: int = d["z"]           # KeyError

# List safe access
items: list[str] = ["a", "b", "c"]
item: str? = items.get(0)   # Some("a")
item: str? = items.get(99)  # Nothing

# List direct access - throws on out of bounds
item: str = items[0]        # "a"
item: str = items[99]       # IndexError
```

*Implementation*
- *🔄 Lowered - Sharpy collections are aliases to the corresponding .NET collections.*
