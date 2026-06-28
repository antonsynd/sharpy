# list

A mutable sequence of elements, similar to Python's `list`.
Supports negative indexing, slicing, and Python-style methods.

## Properties

| Name | Type | Description |
|------|------|-------------|
| `length` | `int` | The number of elements. Mirrors the (explicitly-implemented) ICollection.Count and is exposed publicly so that C# list patterns over Sharpy lists are "countable" (e.g. \`case [a, b, *rest]\`). Sharpy code should prefer \`len(x)\`. |
| `is_read_only` | `bool` | Gets a value indicating whether the list is read-only. |

## Methods

### `add(item): T = > _list.Add(item)`

!!! note
    For collection initializers. Also a part of the
    ICollection interface.

### `copy() -> list[T]`

Return a shallow copy of the list.

**Returns:** A new list with the same elements.

```python
x = [1, 2, 3]
y = x.copy()    # [1, 2, 3]
```

### `sort(reverse: bool = False)`

Sort the items of the list in place (the arguments can be used for
sort customization, see Sorted() for their explanation).

**Parameters:**

- `reverse` (bool) -- If `True`, sort in descending order.

```python
x = [3, 1, 2]
x.sort()             # [1, 2, 3]
x.sort(reverse=True) # [3, 2, 1]
```

!!! note
    This is not a stable sort.

### `sort(key: (T) -> TKey, reverse: bool = False)`

Sort the items of the list in place (the arguments can be used for
sort customization, see Sorted() for their explanation).

!!! note
    This is not a stable sort.

### `to_list() -> list[T]`

Creates a shallow copy this list as a .NET list.

### `append(x): T = > _list.Add(x)`

Add an item to the end of the list. Similar to a[len(a):] = [x].

```python
x = [1, 2, 3]
x.append(4)    # [1, 2, 3, 4]
```

### `extend(enumerable: Iterable[T])`

Extend the list by appending all the items from the iterable.
Similar to a[len(a):] = iterable.

**Parameters:**

- `enumerable` (Iterable[T]) -- The iterable whose items are appended.

```python
x = [1, 2]
x.extend([3, 4])    # [1, 2, 3, 4]
```

### `clear())`

Remove all items from the list.

```python
x = [1, 2, 3]
x.clear()    # []
```

### `insert(i: int, x: T)`

Insert an item at a given position. The first argument is the
index of the element before which to insert, so a.Insert(0, x)
inserts at the front of the list, and a.Insert(Len(a), x) is
equivalent to a.Append(x).

**Parameters:**

- `i` (int) -- Index before which to insert.
- `x` (T) -- The item to insert.

```python
x = [1, 2, 3]
x.insert(0, 0)    # [0, 1, 2, 3]
x.insert(-1, 9)   # [0, 1, 2, 9, 3]
```

### `pop(i: int = -1) -> T`

Remove the item at the given position in the list, and return it.
If no index is specified, a.Pop() removes and returns the last
item in the list. It raises an IndexError if the list is empty or
the index is outside the list range.

**Parameters:**

- `i` (int) -- Index of the item to remove (default: -1, the last item).

**Returns:** The removed item.

```python
x = [1, 2, 3]
x.pop()     # 3, x is [1, 2]
x.pop(0)    # 1, x is [2]
```

**Raises:**

- `IndexError` -- Thrown if the list is empty or the index is out of range.

### `remove(x: T)`

Remove the first item from the list whose value is equal to x. It
raises a ValueError if there is no such item.

**Parameters:**

- `x` (T) -- The value to remove.

```python
x = [1, 2, 3, 2]
x.remove(2)    # [1, 3, 2]
```

**Raises:**

- `ValueError` -- Thrown if the value is not found.

### `reverse())`

Reverse the elements of the list in place.

```python
x = [1, 2, 3]
x.reverse()    # [3, 2, 1]
```

### `count(x: T) -> int`

Return the number of times x appears in the list.

**Parameters:**

- `x` (T) -- The value to count.

**Returns:** The number of occurrences.

```python
x = [1, 2, 2, 3]
x.count(2)    # 2
x.count(5)    # 0
```

### `index(x: T, start: int = 0, end: int = -1) -> int`

Return zero-based index in the list of the first item whose value
is equal to x. Raises a `ValueError` if there is no
such item.

**Parameters:**

- `x` (T) -- The value to search for.
- `start` (int) -- Start of the slice to search (default: 0).
- `end` (int) -- End of the slice to search (default: -1, end of list).

**Returns:** The zero-based index of the first matching item.

```python
x = [1, 2, 3, 2]
x.index(2)       # 1
x.index(2, 2)    # 3
```

!!! note
    The optional arguments start and end are interpreted as
    in the slice notation and are used to limit the search to a
    particular subsequence of the list. The returned index is computed
    relative to the beginning of the full sequence rather than the
    start argument.

**Raises:**

- `ValueError` -- Thrown if the value is not found.

### `contains(x): T = > _list.Contains(x) -> bool`

Returns whether the item is in the list.

**Returns:** `True` if the item is found; otherwise `False`.

```python
x = [1, 2, 3]
2 in x    # True
5 in x    # False
```

### `get(index: int) -> Optional[T]`

Return the element at *index* wrapped in an
`Optional{T}`, or `Optional{T}.None` if the
index is out of range. Supports Python-style negative indexing.

**Parameters:**

- `index` (int) -- The index of the element to retrieve.

**Returns:** `Optional{T}.Some(T)` containing the element at
*index*, or `Optional{T}.None` if the
index is out of range.

```python
x = [10, 20, 30]
x.get(0)     # Some(10)
x.get(-1)    # Some(30)
x.get(5)     # None
```

### `get(index: int, default_: T) -> T`

Return the element at *index*, or
*default_* if the index is out of range. Supports
Python-style negative indexing.

**Parameters:**

- `index` (int) -- The index of the element to retrieve.
- `default_` (T) -- The fallback value when the index is out of range.

**Returns:** The element at *index*, or *default_*
if the index is out of range.

```python
x = [10, 20, 30]
x.get(0, -1)     # 10
x.get(5, -1)     # -1
```

### `get_slice(slice: Slice) -> list[T]`

Returns a slice of the list.

**Raises:**

- `ValueError` -- Thrown if slice step is zero.

### `set_slice(slice: Slice, other: Iterable[T])`

Sets a slice of the list from an enumerable.

### `set_slice(slice: Slice, other: list[T])`

Sets a slice of the list from another list.

**Raises:**

- `TypeError` -- Thrown if *other* is null.
- `ValueError` -- Thrown if slice step is zero or assignment size mismatches extended slice.

### `delete_at(index: int)`

Deletes the element at the specified index.

### `delete_slice(slice: Slice)`

Deletes a slice of the list.

**Raises:**

- `ValueError` -- Thrown if slice step is zero.

### `copy_to(array: list[T], array_index): int = > _list.CopyTo(array, arrayIndex)`

Copies the elements of the list to an array.
