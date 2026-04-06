# collections

A ChainMap groups multiple dictionaries together to create a single, updateable view.
Like Python's collections.ChainMap.

```python
import collections
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `maps` | `System.Collections.Generic.List[dict[K, V]]` | The list of underlying mappings. |
| `count` | `int` | Gets the number of elements in the deque. |
| `keys` | `Iterable[TKey]` | The keys of the dictionary. |
| `values` | `Iterable[TValue]` | The values of the dictionary. |
| `count` | `int` | Gets the number of key/value pairs. |

## Functions

### `collections.new_child(m: dict[K, V]? = null) -> ChainMap[K, V]`

Return a new ChainMap with a new map followed by all previous maps.
If no map is provided, an empty dict is used.

### `collections.get(key: K, @default: V = default!) -> V`

Get a value, searching through all maps.

### `collections.keys() -> Iterable[K]`

Return all unique keys across all maps.

### `collections.pop(key: K) -> V`

Remove key from the first mapping. Raises KeyError if not found in first mapping.

### `collections.clear()`

Clear the first mapping.

### `collections.append(x: T)`

Add x to the right side of the deque.

### `collections.appendleft(x: T)`

Add x to the left side of the deque.

### `collections.pop() -> T`

Remove and return an element from the right side of the deque.
If no elements are present, raises an IndexError.

### `collections.popleft() -> T`

Remove and return an element from the left side of the deque.
If no elements are present, raises an IndexError.

### `collections.clear()`

Remove all elements from the deque.

### `collections.extend(iterable: Iterable[T])`

Extend the right side of the deque by appending elements from the iterable.

### `collections.extendleft(iterable: Iterable[T])`

Extend the left side of the deque by appending elements from the iterable.

### `collections.elements() -> Iterable[T]`

Elements are returned in arbitrary order. Each element is repeated count times.

### `collections.update(iterable: Iterable[T])`

Update counts from an iterable or another mapping.

### `collections.get(key: TKey, default_value: TValue = default!) -> TValue`

Get the value for a key, or return a default value if the key is not present.

### `collections.pop(key: K) -> V`

Remove the specified key and return its value.

### `collections.pop(key: K, @default: V) -> V`

Remove the specified key and return its value, or return default if not found.

### `collections.move_to_end(key: K, last: bool = true)`

Move an existing key to either end of an ordered dictionary.
If last is true, move to the end; if false, move to the beginning.

### `collections.clear()`

Remove all items from the dictionary.

### `collections.keys() -> Iterable[K]`

Return the keys in insertion order.

### `collections.values() -> Iterable[V]`

Return the values in insertion order.

### `collections.copy() -> OrderedDict[K, V]`

Return a shallow copy.

### `collections.get(key: K, @default: V = default!) -> V`

Get the value for a key, or a default.
