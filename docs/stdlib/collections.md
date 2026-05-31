# collections

Specialized container datatypes: ChainMap, Counter, Deque, DefaultDict, OrderedDict.

```python
import collections
```

## ChainMap

A ChainMap groups multiple dictionaries together to create a single, updateable view.
Like Python's collections.ChainMap.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `maps` | `list[dict[K, V]]` | The list of underlying mappings. |

### `new_child(m: dict[K, V] | None = None) -> ChainMap[K, V]`

Return a new ChainMap with a new map followed by all previous maps.
If no map is provided, an empty dict is used.

### `get(key: K, @default: V = default!) -> V`

Get a value, searching through all maps.

### `keys() -> Iterable[K]`

Return all unique keys across all maps.

### `pop(key: K) -> V`

Remove key from the first mapping. Raises KeyError if not found in first mapping.

### `clear()`

Clear the first mapping.

## Deque

A deque (double-ended queue) is a generalization of stacks and queues
that supports adding and removing elements from either end.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `count` | `int` | Gets the number of elements in the deque. |

### `append(x: T)`

Add x to the right side of the deque.

### `appendleft(x: T)`

Add x to the left side of the deque.

### `pop() -> T`

Remove and return an element from the right side of the deque.
If no elements are present, raises an IndexError.

### `popleft() -> T`

Remove and return an element from the left side of the deque.
If no elements are present, raises an IndexError.

### `clear()`

Remove all elements from the deque.

### `extend(iterable: Iterable[T])`

Extend the right side of the deque by appending elements from the iterable.

### `extendleft(iterable: Iterable[T])`

Extend the left side of the deque by appending elements from the iterable.

## Counter

A deque (double-ended queue) is a generalization of stacks and queues
that supports adding and removing elements from either end.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `keys` | `Iterable[T]` | The keys of the counter. |

### `elements() -> Iterable[T]`

Elements are returned in arbitrary order. Each element is repeated count times.

### `update(iterable: Iterable[T])`

Update counts from an iterable or another mapping.

### `subtract(iterable: Iterable[T])`

Subtract counts. Elements are subtracted from an iterable.
Counts can go below zero.

### `subtract(other: Counter[T])`

Subtract counts from another Counter.
Counts can go below zero.

### `copy() -> Counter[T]`

Return a shallow copy of this counter.

### `total() -> int`

Return the sum of all counts.

### `clear()`

Remove all elements from the counter.

### `contains(key): T = > ContainsKey(key) -> bool`

Check if the counter contains a key (alias for ContainsKey).
Used by the `in` operator.

## DefaultDict

A deque (double-ended queue) is a generalization of stacks and queues
that supports adding and removing elements from either end.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `keys` | `Iterable[TKey]` | The keys of the dictionary. |
| `values` | `Iterable[TValue]` | The values of the dictionary. |
| `default_factory` | `() -> TValue` | The default factory function used for missing keys. |
| `count` | `int` | The number of items in the defaultdict. |

### `get(key: TKey, default_value: TValue = default!) -> TValue`

Get the value for a key, or return a default value if the key is not present.

### `contains(key): TKey = > ContainsKey(key) -> bool`

Check if the dictionary contains a key (alias for ContainsKey).
Used by the `in` operator: `"x" in d` → `d.Contains("x")`.

### `copy() -> DefaultDict[TKey, TValue]`

Return a shallow copy of this defaultdict, preserving the default factory.

### `clear()`

Remove all items from the defaultdict.

### `pop(key: TKey) -> TValue`

Remove the specified key and return its value.
Raises `KeyError` if the key is not found.

### `pop(key: TKey, default_value: TValue) -> TValue`

Remove the specified key and return its value.
If the key is not found, return *defaultValue*.

### `update(other: IDictionary[TKey, TValue])`

Update the defaultdict with key-value pairs from another dictionary.

### `update(other: Iterable[tuple[TKey, TValue]])`

Update the defaultdict with key-value pairs from an iterable of tuples.

### `set_default(key: TKey, default_value: TValue) -> TValue`

If *key* is in the dictionary, return its value.
If not, insert *key* with *defaultValue*
and return *defaultValue*.

### `remove(key: TKey)`

Removes the item with the specified key from the defaultdict.

**Raises:**

- `KeyError` -- Thrown if the key does not exist.

### `to_dictionary() -> Dictionary[TKey, TValue]`

Convert to a standard .NET Dictionary.

## OrderedDict

A dictionary that remembers the order in which items were inserted.
Like Python's collections.OrderedDict.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `count` | `int` | Gets the number of key/value pairs. |

### `pop(key: K) -> V`

Remove the specified key and return its value.

### `pop(key: K, @default: V) -> V`

Remove the specified key and return its value, or return default if not found.

### `move_to_end(key: K, last: bool = True)`

Move an existing key to either end of an ordered dictionary.
If last is True, move to the end; if False, move to the beginning.

### `clear()`

Remove all items from the dictionary.

### `keys() -> Iterable[K]`

Return the keys in insertion order.

### `values() -> Iterable[V]`

Return the values in insertion order.

### `copy() -> OrderedDict[K, V]`

Return a shallow copy.

### `get(key: K, @default: V = default!) -> V`

Get the value for a key, or a default.
