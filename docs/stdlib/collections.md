# collections

A ChainMap groups multiple dictionaries together to create a single, updateable view.
Like Python's collections.ChainMap.

```python
import collections
```

## ChainMap

A ChainMap groups multiple dictionaries together to create a single, updateable view.
Like Python's collections.ChainMap.

### `new_child(m: dict[K, V]? = null) -> ChainMap[K, V]`

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

### `elements() -> Iterable[T]`

Elements are returned in arbitrary order. Each element is repeated count times.

### `update(iterable: Iterable[T])`

Update counts from an iterable or another mapping.

### `get(key: TKey, default_value: TValue = default!) -> TValue`

Get the value for a key, or return a default value if the key is not present.

## OrderedDict

A dictionary that remembers the order in which items were inserted.
Like Python's collections.OrderedDict.

### `pop(key: K) -> V`

Remove the specified key and return its value.

### `pop(key: K, @default: V) -> V`

Remove the specified key and return its value, or return default if not found.

### `move_to_end(key: K, last: bool = true)`

Move an existing key to either end of an ordered dictionary.
If last is true, move to the end; if false, move to the beginning.

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
