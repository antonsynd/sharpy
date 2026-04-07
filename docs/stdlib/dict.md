# dict

A mutable mapping of keys to values, similar to Python's dict.
Supports Python-style methods like get(), pop(), items(), keys(), and values().

## Properties

| Name | Type | Description |
|------|------|-------------|
| `count` | `int` | Gets the number of key/value pairs in the dictionary. |

## Methods

### `copy() -> dict[K, V]`

Return a shallow copy of the dictionary.

**Returns:** A new dictionary with the same key-value pairs.

```python
d = {"a": 1, "b": 2}
e = d.copy()    # {"a": 1, "b": 2}
```

### `clear()`

Remove all items from the dictionary.

```python
d = {"a": 1, "b": 2}
d.clear()    # {}
```

### `contains(key: K) -> bool`

Check if *key* exists in the dictionary.
Used by the compiler for `key in dict` expressions.

### `get(key: K) -> Optional[V]`

Return the value for *key* if present, otherwise
`Optional{T}.None`.

**Parameters:**

- `key` (K) -- The key to look up.

**Returns:** An `Optional{T}` containing the value, or None.

```python
d = {"a": 1}
d.get("a")    # Some(1)
d.get("z")    # None
```

### `get(key: K, @default: V) -> V`

Return the value for *key* if present, otherwise
*default*.

**Parameters:**

- `key` (K) -- The key to look up.
- `@default` (V)

**Returns:** The value for the key, or the default.

```python
d = {"a": 1}
d.get("a", 0)    # 1
d.get("z", 0)    # 0
```

### `items() -> DictItemsView[K, V]`

Return a view of the dictionary's key-value pairs.

**Returns:** A view of `(key, value)` pairs.

```python
d = {"a": 1, "b": 2}
for k, v in d.items():
    print(k, v)
```

### `keys() -> DictKeyView[K, V]`

Return a view of the dictionary's keys.

**Returns:** A view of the keys.

```python
d = {"a": 1, "b": 2}
d.keys()    # ["a", "b"]
```

### `pop(key: K) -> V`

Remove the specified key and return the corresponding value.
Raises `KeyError` if the key is not found.

**Parameters:**

- `key` (K) -- The key to remove.

**Returns:** The value that was associated with the key.

```python
d = {"a": 1, "b": 2}
d.pop("a")    # 1, d is {"b": 2}
```

**Raises:**

- `KeyError` -- Thrown if the key is not found.

### `pop(key: K, @default: V) -> V`

Remove the specified key and return the corresponding value.
If the key is not found, return *default*.

**Parameters:**

- `key` (K) -- The key to remove.
- `@default` (V)

**Returns:** The removed value or the default.

```python
d = {"a": 1}
d.pop("z", 0)    # 0
```

### `set_default(key: K, @default: V) -> V`

If *key* is in the dictionary, return its value.
If not, insert *key* with *default*
and return *default*.

**Parameters:**

- `key` (K) -- The key to look up or insert.
- `@default` (V)

**Returns:** The existing or newly inserted value.

```python
d = {"a": 1}
d.setdefault("a", 0)    # 1
d.setdefault("b", 0)    # 0, d is {"a": 1, "b": 0}
```

### `update(other: IReadOnlyDictionary[K, V])`

Update the dictionary with key-value pairs from *other*,
overwriting existing keys.

**Parameters:**

- `other` (IReadOnlyDictionary[K, V]) -- A dictionary whose pairs are merged in.

```python
d = {"a": 1}
d.update({"a": 9, "b": 2})    # {"a": 9, "b": 2}
```

### `update(v: IEnumerable<(K,)`

Update the dictionary with key-value pairs from an iterable of tuples.

### `values() -> DictValuesView[K, V]`

Return a view of the dictionary's values.

**Returns:** A view of the values.

```python
d = {"a": 1, "b": 2}
d.values()    # [1, 2]
```

### `remove(key: K)`

Removes the item with the specified key from the dictionary.

**Raises:**

- `KeyError` -- Thrown if the key does not exist.

### `to_dictionary() -> Dictionary[K, V]`

Convert to a standard .NET Dictionary.

### `merge(other: dict[K, V]) -> dict[K, V]`

Returns a new dictionary that is the result of merging this dictionary with other.
Keys from other take precedence.

### `add(key: K, value: V)`

For collection initializers.
