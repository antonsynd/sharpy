# collections

Specialized container datatypes: ChainMap, Counter, Deque, DefaultDict, OrderedDict.

```python
import collections
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `deque_type` | `Type` | The Deque type. |
| `counter_type` | `Type` | The Counter type. |
| `default_dict_type` | `Type` | The DefaultDict type. |
| `ordered_dict_type` | `Type` | The OrderedDict type. |
| `chain_map_type` | `Type` | The ChainMap type. |

## ChainMap

A ChainMap groups multiple dictionaries together to create a single, updateable view.
Like Python's collections.ChainMap.

!!! note
    Implements `ISized` (`__len__` → `len(cm)`, the number of unique keys)
    and `IEnumerable{T}` over the KEYS (`__iter__` → `for k in cm`,
    `list(cm)`). Keys are the only generic `IEnumerable` the type exposes so
    `list(cm)` binds `Builtins.List<K>(IEnumerable<K>)`; pairs are reached
    through `Items` only (#1933). Keys/values/items iterate the maps in CPython's
    REVERSED order — the last map is walked first and dedup keeps the first occurrence in that
    reversed walk (for maps `{n,x},{n,y}` CPython yields keys `n, y, x`) — while VALUE
    lookup stays first-map-wins via the indexer.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `maps` | `list[dict[K, V]]` | The list of underlying mappings. |

### `new_child(m: dict[K, V] | None = None) -> ChainMap[K, V]`

Return a new ChainMap with a new map followed by all previous maps.
If no map is provided, an empty dict is used.

### `get(key: K, default: V = None) -> V`

Get a value, searching through all maps.

### `keys() -> list[K]`

Return all unique keys across all maps as a sized list, in CPython's merge order.

### `values() -> list[V]`

Return the values for the unique keys as a sized list, in CPython's merge key order.
Each value is the first-map-wins lookup for its key.

### `items() -> list[tuple[K, V]]`

Return the (key, value) pairs as a sized list, in CPython's merge key order.
Each value is the first-map-wins lookup for its key.

### `pop(key: K) -> V`

Remove key from the first mapping. Raises KeyError if not found in first mapping.

### `clear()`

Clear the first mapping.

### `__str__() -> str`

`repr()` uses the same method. Python's `repr(cm)`/`str(cm)`: `ChainMap({...}, {...})`, one
`Dict{K, V}` repr per underlying map in `Maps` order. An empty
ChainMap holds one empty map, so it prints `ChainMap({})` as CPython does.

## Deque

A deque (double-ended queue) is a generalization of stacks and queues
that supports adding and removing elements from either end.

!!! note
    Implements `ISized` (`__len__` → `len(d)` and truth testing:
    `if d:` is False for an empty deque). `IReadOnlyCollection{T}` alone gave
    `len()` a count but left every truth position refused (SPY0220), because the truth
    classifier reads the dunder table's spelling, `ISized` (#1972).

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

### `__str__() -> str`

`repr()` uses the same method. Python's `repr` of the deque: `deque([1, 2])`, and `deque([])` when empty.
There is no `maxlen` suffix — Sharpy's deque is unbounded, and CPython prints none
for `maxlen=None`.

## Counter

A Counter is a dict subclass for counting hashable objects.

!!! note
    Implements `ISized` (`__len__` → `len(c)`, the number of distinct
    elements) and `IEnumerable{T}` over the KEYS in first-seen order (`__iter__`
    → `for k in c`, `list(c)`). Keys are the only generic `IEnumerable` the type
    exposes so `list(c)` binds `Builtins.List<T>(IEnumerable<T>)` (#1933).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `count` | `int` | The number of distinct elements. Python's \`len(c)\`; ISized's \`__len__\`. |

### `most_common(n: int | None = None) -> list[tuple[T, int]]`

Return a list of the n most common elements and their counts.
Elements with equal counts keep first-seen order, as CPython's stable sort does
(`Counter("cba").most_common()` is `[('c', 1), ('b', 1), ('a', 1)]`, #1979).

### `__str__() -> str`

`repr()` uses the same method. Python's `repr(c)`/`str(c)`: `Counter({...})` in most-common order, or
`Counter()` when empty (CPython 3.12). The braces are `Dict{K, V}`'s own
repr, so there is one spelling of the mapping rule.

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

### `keys() -> list[T]`

The keys of the counter. Python: `c.keys()`. Returns a copy, not a live view.

### `values() -> list[int]`

The counts of the counter, in first-seen key order. Python: `c.values()`.

### `items() -> list[tuple[T, int]]`

The (element, count) pairs, in first-seen key order. Python: `c.items()`.

### `contains(key: T) -> bool`

Check if the counter contains a key (alias for ContainsKey).
Used by the `in` operator.

## DefaultDict

Dictionary with default values for missing keys.

!!! note
    Implements `ISized` (`__len__` → `len(dd)`) and
    `IEnumerable{T}` over the KEYS in insertion order (`__iter__` →
    `for k in dd`, `list(dd)`), delegating both to the composed `Dict{K, V}`.
    Keys are the only generic `IEnumerable` the type exposes so `list(dd)` binds
    `Builtins.List<TKey>(IEnumerable<TKey>)` (#1933).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `default_factory` | `() -> TValue` | The default factory function used for missing keys. |
| `count` | `int` | The number of items in the defaultdict. |

### `get(key: TKey, default_value: TValue = None) -> TValue`

Get the value for a key, or return a default value if the key is not present.

### `contains(key: TKey) -> bool`

Check if the dictionary contains a key (alias for ContainsKey).
Used by the `in` operator: `"x" in d` → `d.Contains("x")`.

### `keys() -> DictKeyView[TKey, TValue]`

The keys of the dictionary. Python: `d.keys()`. A live view: later mutations of the
defaultdict are reflected.

### `values() -> DictValuesView[TKey, TValue]`

The values of the dictionary. Python: `d.values()`. A live view: later mutations of
the defaultdict are reflected.

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

### `items() -> DictItemsView[TKey, TValue]`

The (key, value) pairs of the dictionary. Python: `d.items()`. A live view, like
`Keys` and `Values`: later mutations of the defaultdict are reflected.

### `update(other: IDictionary[TKey, TValue])`

Update the defaultdict with key-value pairs from another dictionary.

### `update(other: Iterable[tuple[TKey, TValue]])`

Update the defaultdict with key-value pairs from an iterable of tuples.

### `set_default(key: TKey, default_value: TValue) -> TValue`

If *key* is in the dictionary, return its value.
If not, insert *key* with *defaultValue*
and return *defaultValue*.

### `pop_item(last: bool = True) -> tuple[TKey, TValue]`

Remove and return a (key, value) pair. If *last* is True,
pairs are returned in LIFO order; otherwise in FIFO order.

**Raises:**

- `KeyError` -- Thrown if the defaultdict is empty.

### `remove(key: TKey)`

Removes the item with the specified key from the defaultdict.

**Raises:**

- `KeyError` -- Thrown if the key does not exist.

### `to_dictionary() -> Dictionary[TKey, TValue]`

Convert to a standard .NET Dictionary.

### `__str__() -> str`

`repr()` uses the same method. Python's `repr(dd)`/`str(dd)`: `defaultdict(<factory>, {...})`, the
pairs rendered by the composed `Dict{K, V}`'s own repr.

!!! note
    Documented deviation (owner ruling R-BN, #1968): CPython prints the factory's repr —
    `defaultdict(<class 'int'>, {'n': 1})` — but a Sharpy factory is a .NET delegate
    with no Python-meaningful repr, so the factory slot is always the literal placeholder
    `<factory>`. The mapping part matches CPython exactly.

## OrderedDict

A dictionary that remembers the order in which items were inserted.
Like Python's collections.OrderedDict.

!!! note
    Implements `ISized` (`__len__` → `len(od)`) and
    `IEnumerable{T}` over the KEYS in insertion order (`__iter__` → `for k in od`,
    `list(od)`). This is the ONLY generic `IEnumerable` the type exposes so that
    `list(od)` binds `Builtins.List<K>(IEnumerable<K>)` unambiguously (#1933).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `count` | `int` | Gets the number of key/value pairs. |

### `pop(key: K) -> V`

Remove the specified key and return its value.

### `pop(key: K, default: V) -> V`

Remove the specified key and return its value, or return default if not found.

### `popitem(last: bool = True) -> tuple[K, V]`

Remove and return a (key, value) pair. If last is True, pairs are returned in LIFO order;
if False, in FIFO order.

### `move_to_end(key: K, last: bool = True)`

Move an existing key to either end of an ordered dictionary.
If last is True, move to the end; if False, move to the beginning.

### `clear()`

Remove all items from the dictionary.

### `keys() -> list[K]`

Return the keys in insertion order as a sized list (a copy, matching Python's view length
and order; `len`/`list`/iteration all work).

### `values() -> list[V]`

Return the values in insertion order as a sized list.

### `items() -> list[tuple[K, V]]`

Return the (key, value) pairs in insertion order as a sized list.

### `copy() -> OrderedDict[K, V]`

Return a shallow copy.

### `get(key: K, default: V = None) -> V`

Get the value for a key, or a default.

### `__str__() -> str`

`repr()` uses the same method. Python's `repr(od)`/`str(od)`: `OrderedDict({...})` with the pairs in
insertion order, or `OrderedDict()` when empty (CPython 3.12). The braces are
`Dict{K, V}`'s own repr, so there is one spelling of the mapping rule.
