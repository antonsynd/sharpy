# Collection Types

| Sharpy Type | .NET Type | Notes |
|-------------|-----------|-------|
| `list[T]` | `Sharpy.Core.List<T>` | Mutable list |
| `dict[K, V]` | `Sharpy.Core.Dict<K, V>` | Hash map |
| `set[T]` | `Sharpy.Core.Set<T>` | Unique elements |
| `tuple[T1, T2, ...]` | `System.ValueTuple<T1, T2, ...>` | Fixed-size tuple |

Collection types use a Sharpy-specific implementation by default. These are bidirectional with the native .NET `System.Collections.Generic` equivalents, `List<T>`, `Dictionary<K, V>`, and `HashSet<T>` and use them underneath as storage.

## Collection Methods

Sharpy collections provide a Python-compatible API:

### `list[T]` Methods

| Method | Description | Python Equivalent |
|--------|-------------|-------------------|
| `append(item)` | Add item to end | `list.append()` |
| `extend(items)` | Add multiple items | `list.extend()` |
| `insert(index, item)` | Insert at index | `list.insert()` |
| `remove(item)` | Remove first occurrence | `list.remove()` |
| `pop()` | Remove and return last | `list.pop()` |
| `pop(index)` | Remove and return at index | `list.pop(i)` |
| `clear()` | Remove all items | `list.clear()` |
| `index(item)` | Find index of item | `list.index()` |
| `count(item)` | Count occurrences | `list.count()` |
| `sort()` | Sort in place | `list.sort()` |
| `reverse()` | Reverse in place | `list.reverse()` |
| `copy()` | Shallow copy | `list.copy()` |

### `dict[K, V]` Methods

| Method | Description | Python Equivalent |
|--------|-------------|-------------------|
| `get(key)` | Get value or None | `dict.get()` |
| `get(key, default)` | Get value or default | `dict.get(k, d)` |
| `keys()` | Get all keys | `dict.keys()` |
| `values()` | Get all values | `dict.values()` |
| `items()` | Get key-value pairs | `dict.items()` |
| `pop(key)` | Remove and return value | `dict.pop()` |
| `update(other)` | Merge another dict | `dict.update()` |
| `clear()` | Remove all items | `dict.clear()` |
| `setdefault(key, default)` | Get or set default | `dict.setdefault()` |

### `set[T]` Methods

| Method | Description | Python Equivalent |
|--------|-------------|-------------------|
| `add(item)` | Add item | `set.add()` |
| `remove(item)` | Remove item (error if missing) | `set.remove()` |
| `discard(item)` | Remove item (no error) | `set.discard()` |
| `pop()` | Remove and return arbitrary item | `set.pop()` |
| `clear()` | Remove all items | `set.clear()` |
| `union(other)` | Set union | `set.union()` |
| `intersection(other)` | Set intersection | `set.intersection()` |
| `difference(other)` | Set difference | `set.difference()` |
| `issubset(other)` | Subset test | `set.issubset()` |
| `issuperset(other)` | Superset test | `set.issuperset()` |

## Interop with .NET Collections

Sharpy collections can be converted to/from .NET collections:

```python
from system.collections.generic import List as DotNetList

# Sharpy list to .NET List
sharpy_list: list[int] = [1, 2, 3]
dotnet_list: DotNetList[int] = sharpy_list.take()  # Explicit conversion with ownership transfer of inner list
                                                   # (inner list is passed by reference, and then a new inner list is
                                                   # set internally in the Sharpy list)
dotnet_list: DotNetList[int] = sharpy_list.inner() # Explicit shared reference of inner list
dotnet_list: DotNetList[int] = sharpy_list         # Implicit copy of inner list via conversion operator

# .NET List to Sharpy list
imported_list: list[int] = list(dotnet_list)  # Constructor accepts IEnumerable
```

## `.inner()` and `.take()` Methods

These methods provide explicit control over the underlying .NET collection:

| Method | Behavior | Use Case |
|--------|----------|----------|
| `.inner()` | Returns a reference to the underlying .NET collection. Changes to either affect both. | When you need to pass the backing collection to .NET APIs that will read or modify it |
| `.take()` | Returns the underlying collection and replaces it internally with a new empty one. The original Sharpy collection becomes empty. | When you need ownership transfer—caller owns the returned .NET collection, Sharpy collection starts fresh |

```python
# .inner() - shared reference
sharpy_list = [1, 2, 3]
inner_ref = sharpy_list.inner()  # inner_ref and sharpy_list share the same backing list
inner_ref.Add(4)                  # Both now contain [1, 2, 3, 4]
print(len(sharpy_list))           # 4

# .take() - ownership transfer
sharpy_list = [1, 2, 3]
taken = sharpy_list.take()        # taken gets [1, 2, 3], sharpy_list is now empty
print(len(sharpy_list))           # 0
print(taken.Count)                # 3
```

The above is not an exhaustive enumeration of all conversion methods.

*Implementation: 🔄 Lowered - `Sharpy.Core` collections wrap .NET collections with Pythonic API.*
