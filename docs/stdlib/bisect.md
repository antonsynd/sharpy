# bisect

```python
import bisect
```

## Functions

### `bisect.bisect_left(a: list[T], x: T, lo: int = 0, hi: int = -1) -> int`

Locate the insertion point for x in a to maintain sorted order.

### `bisect.bisect_right(a: list[T], x: T, lo: int = 0, hi: int = -1) -> int`

Like bisect_left, but returns an insertion point which comes after any existing entries of x.

### `bisect.bisect(a: list[T], x: T, lo: int = 0, hi: int = -1) -> int`

Alias for bisect_right.

### `bisect.insort_left(a: list[T], x: T, lo: int = 0, hi: int = -1)`

Insert x in a in sorted order, inserting to the left of any existing entries of x.

### `bisect.insort_right(a: list[T], x: T, lo: int = 0, hi: int = -1)`

Insert x in a in sorted order, inserting to the right of any existing entries of x.

### `bisect.insort(a: list[T], x: T, lo: int = 0, hi: int = -1)`

Alias for insort_right.
