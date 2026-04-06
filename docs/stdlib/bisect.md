# bisect

Array bisection algorithm, similar to Python's bisect module.
Provides functions to maintain a list in sorted order without
having to sort the list after each insertion.

```python
import bisect
```

## Functions

### `bisect.bisect_left(a: IList[T], x: T, lo: int = 0, hi: int = -1) -> int`

Locate the leftmost insertion point for *x* in
*a* to maintain sorted order.

**Parameters:**

- `a` (IList[T]) -- A sorted list to search.
- `x` (T) -- The value to locate an insertion point for.
- `lo` (int) -- The lower bound of the slice to search (inclusive).
- `hi` (int) -- The upper bound of the slice to search (exclusive). -1 means len(a).

**Returns:** The leftmost index where *x* can be inserted.

```python
bisect.bisect_left([1, 2, 3, 4, 5], 3)    # 2
bisect.bisect_left([1, 1, 1], 1)           # 0
```

### `bisect.bisect_right(a: IList[T], x: T, lo: int = 0, hi: int = -1) -> int`

Locate the rightmost insertion point for *x* in
*a* to maintain sorted order.

**Parameters:**

- `a` (IList[T]) -- A sorted list to search.
- `x` (T) -- The value to locate an insertion point for.
- `lo` (int) -- The lower bound of the slice to search (inclusive).
- `hi` (int) -- The upper bound of the slice to search (exclusive). -1 means len(a).

**Returns:** The rightmost index where *x* can be inserted.

```python
bisect.bisect_right([1, 2, 3, 4, 5], 3)   # 3
bisect.bisect_right([1, 1, 1], 1)          # 3
```

### `bisect.bisect(a: IList[T], x: T, lo: int = 0, hi: int = -1) -> int`

Alias for `BisectRight{T}`. Locate the rightmost insertion point
for *x* in *a* to maintain sorted order.

**Parameters:**

- `a` (IList[T]) -- A sorted list to search.
- `x` (T) -- The value to locate an insertion point for.
- `lo` (int) -- The lower bound of the slice to search (inclusive).
- `hi` (int) -- The upper bound of the slice to search (exclusive). -1 means len(a).

**Returns:** The rightmost index where *x* can be inserted.

!!! note
    This is the Python `bisect.bisect()` function, which is an alias for
    `bisect_right`.

### `bisect.insort_left(a: IList[T], x: T, lo: int = 0, hi: int = -1)`

Insert *x* in *a* in sorted order,
inserting at the leftmost suitable position.

**Parameters:**

- `a` (IList[T]) -- A sorted list to insert into.
- `x` (T) -- The value to insert.
- `lo` (int) -- The lower bound of the slice to search (inclusive).
- `hi` (int) -- The upper bound of the slice to search (exclusive). -1 means len(a).

```python
a = [1, 3, 5]
bisect.insort_left(a, 3)    # a is now [1, 3, 3, 5]
```

### `bisect.insort_right(a: IList[T], x: T, lo: int = 0, hi: int = -1)`

Insert *x* in *a* in sorted order,
inserting at the rightmost suitable position.

**Parameters:**

- `a` (IList[T]) -- A sorted list to insert into.
- `x` (T) -- The value to insert.
- `lo` (int) -- The lower bound of the slice to search (inclusive).
- `hi` (int) -- The upper bound of the slice to search (exclusive). -1 means len(a).

```python
a = [1, 3, 5]
bisect.insort_right(a, 4)    # a is now [1, 3, 4, 5]
```

### `bisect.insort(a: IList[T], x: T, lo: int = 0, hi: int = -1)`

Alias for `InsortRight{T}`. Insert *x* in
*a* in sorted order.

**Parameters:**

- `a` (IList[T]) -- A sorted list to insert into.
- `x` (T) -- The value to insert.
- `lo` (int) -- The lower bound of the slice to search (inclusive).
- `hi` (int) -- The upper bound of the slice to search (exclusive). -1 means len(a).
