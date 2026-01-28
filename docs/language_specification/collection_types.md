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

*Implementation*
- *🔄 Lowered - Sharpy collections are aliases to the corresponding .NET collections.*
