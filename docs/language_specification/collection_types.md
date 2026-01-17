# Collection Types

| Sharpy Type | Shorthand | .NET Type | Notes |
|-------------|-----------|-----------|-------|
| `list[T]` | `[T]` | `System.Collections.Generic.List<T>` | Mutable list |
| `dict[K, V]` | `{K: V}` | `Sharpy.Collections.Generic.Dictionary<K, V>` | Hash map |
| `set[T]` | `{T}` | `Sharpy.Collections.Generic.HashSet<T>` | Unique elements |
| `tuple[T1, T2, ...]` | `(T1, T2, ...)` | `System.ValueTuple<T1, T2, ...>` | Fixed-size tuple |

Sharpy uses the corresponding .NET collection types. At a later stage, Sharpy will provide Pythonic wrappers for these types.

## Shorthand Syntax

All collection types support [shorthand syntax](type_annotation_shorthand.md) for more concise type annotations:

```python
# These pairs are equivalent:
items: [int]           # items: list[int]
scores: {str: int}     # scores: dict[str, int]
unique: {int}          # unique: set[int]
point: (int, int)      # point: tuple[int, int]
```

*Implementation*
- *🔄 Lowered - Sharpy collections are aliases to the corresponding .NET collections.*
