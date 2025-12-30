# Collection Types

| Sharpy Type | .NET Type | Notes |
|-------------|-----------|-------|
| `list[T]` | `System.Collections.Generic.List<T>` | Mutable list |
| `dict[K, V]` | `Sharpy.Collections.Generic.Dictionary<K, V>` | Hash map |
| `set[T]` | `Sharpy.Collections.Generic.HashSet<T>` | Unique elements |
| `tuple[T1, T2, ...]` | `System.ValueTuple<T1, T2, ...>` | Fixed-size tuple |

Sharpy uses the corresponding .NET collection types. At a later stage, Sharpy will provide Pythonic wrappers for these types.

*Implementation*
- *🔄 Lowered - Sharpy collections are aliases to the corresponding .NET collections.*
