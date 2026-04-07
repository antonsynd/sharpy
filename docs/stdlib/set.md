# set

A mutable set of unique elements, similar to Python's `set`.
Supports set operations: union, intersection, difference, and symmetric difference.

## Properties

| Name | Type | Description |
|------|------|-------------|
| `count` | `int` | Gets the number of elements in the set. |
| `is_read_only` | `bool` | Gets a value indicating whether the set is read-only. |

## Methods

### `copy() -> set[T]`

Return a shallow copy of the set.

**Returns:** A new set with the same elements.

```python
s = {1, 2, 3}
t = s.copy()    # {1, 2, 3}
```

### `is_proper_subset(other: set[T]) -> bool`

Returns whether this set is a proper subset of other (subset but not equal).

**Parameters:**

- `other` (set[T]) -- The set to compare against

**Returns:** `true` if this set is a proper subset of *other*

### `is_subset(other: set[T]) -> bool`

Returns whether this set is a subset of other (all elements in other).

**Parameters:**

- `other` (set[T]) -- The set to compare against.

**Returns:** `true` if every element in this set is also in *other*.

```python
a = {1, 2}
b = {1, 2, 3}
a.issubset(b)    # True
```

### `is_proper_superset(other: set[T]) -> bool`

Returns whether this set is a proper superset of other (superset but not equal).

**Parameters:**

- `other` (set[T]) -- The set to compare against

**Returns:** `true` if this set is a proper superset of *other*

### `is_superset(other: set[T]) -> bool`

Returns whether this set is a superset of other (contains all elements of other).

**Parameters:**

- `other` (set[T]) -- The set to compare against.

**Returns:** `true` if every element in *other* is also in this set.

```python
a = {1, 2, 3}
b = {1, 2}
a.issuperset(b)    # True
```

### `union(other: set[T]) -> set[T]`

Returns a new set with elements from both sets.

**Parameters:**

- `other` (set[T]) -- The other set.

**Returns:** A new set containing elements from both sets.

```python
a = {1, 2}
b = {2, 3}
a.union(b)    # {1, 2, 3}
```

### `intersection(other: set[T]) -> set[T]`

Returns a new set with elements common to both sets.

**Parameters:**

- `other` (set[T]) -- The other set.

**Returns:** A new set containing only elements found in both sets.

```python
a = {1, 2, 3}
b = {2, 3, 4}
a.intersection(b)    # {2, 3}
```

### `difference(other: set[T]) -> set[T]`

Returns a new set with elements in this set but not in other.

**Parameters:**

- `other` (set[T]) -- The other set.

**Returns:** A new set with elements only in this set.

```python
a = {1, 2, 3}
b = {2, 3, 4}
a.difference(b)    # {1}
```

### `symmetric_difference(other: set[T]) -> set[T]`

Returns a new set with elements in either set but not both.

**Parameters:**

- `other` (set[T]) -- The other set.

**Returns:** A new set with elements in exactly one of the two sets.

```python
a = {1, 2, 3}
b = {2, 3, 4}
a.symmetric_difference(b)    # {1, 4}
```

### `to_hash_set() -> HashSet[T]`

Convert to a standard .NET HashSet.

### `add(x: T)`

Add an element to the set (no effect if already present).

**Parameters:**

- `x` (T) -- The element to add.

```python
s = {1, 2}
s.add(3)    # {1, 2, 3}
s.add(2)    # {1, 2, 3}  (no change)
```

!!! note
    For initializer literals and part of
    System.Collections.Generic.ICollection interface.

### `discard(x: T)`

Remove an element from the set if present (no error if not present).

**Parameters:**

- `x` (T) -- The element to discard.

```python
s = {1, 2, 3}
s.discard(2)    # {1, 3}
s.discard(9)    # {1, 3}  (no error)
```

### `clear()`

Remove all elements from the set.

```python
s = {1, 2, 3}
s.clear()    # set()
```

### `pop() -> T`

Remove and return an arbitrary element from the set.
Raises KeyError if the set is empty.

**Returns:** An arbitrary element from the set.

```python
s = {1, 2, 3}
s.pop()    # removes and returns an element
```

**Raises:**

- `KeyError` -- Thrown if the set is empty.

### `remove(x: T)`

Remove an element from the set.
Raises KeyError if the element is not present.

**Parameters:**

- `x` (T) -- The element to remove.

```python
s = {1, 2, 3}
s.remove(2)    # {1, 3}
```

**Raises:**

- `KeyError` -- Thrown if the element is not found.

### `contains(x: T) -> bool`

Returns whether the item is in the set.

**Parameters:**

- `x` (T) -- The element to check for.

**Returns:** `true` if the element is found; otherwise `false`.

```python
s = {1, 2, 3}
2 in s    # True
5 in s    # False
```

### `is_disjoint(other: set[T]) -> bool`

Returns whether this set has no elements in common with other.

**Parameters:**

- `other` (set[T]) -- The set to test against.

**Returns:** `true` if the sets have no common elements.

```python
a = {1, 2}
b = {3, 4}
a.isdisjoint(b)    # True
```

### `copy_to(array: list[T], array_index: int)`

Copies the elements of the set to an array.

### `except_with(other: Iterable[T])`

Removes all elements in the specified collection from the current set.

### `intersect_with(other: Iterable[T])`

Modifies the current set to contain only elements present in both sets.

### `is_proper_subset_of(other: Iterable[T]) -> bool`

Determines whether the current set is a proper subset of the specified collection.

### `is_proper_superset_of(other: Iterable[T]) -> bool`

Determines whether the current set is a proper superset of the specified collection.

### `is_subset_of(other: Iterable[T]) -> bool`

Determines whether the current set is a subset of the specified collection.

### `is_superset_of(other: Iterable[T]) -> bool`

Determines whether the current set is a superset of the specified collection.

### `overlaps(other: Iterable[T]) -> bool`

Determines whether the current set and a specified collection share common elements.

### `set_equals(other: Iterable[T]) -> bool`

Determines whether the current set and the specified collection contain the same elements.

### `symmetric_except_with(other: Iterable[T])`

Modifies the current set to contain only elements present in either the current set or the specified collection, but not both.

### `union_with(other: Iterable[T])`

Modifies the current set to contain all elements present in either the current set or the specified collection.
