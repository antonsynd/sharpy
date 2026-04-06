# functools

Higher-order functions and operations on callable objects, similar to
Python's functools module.

```python
import functools
```

## Functions

### `functools.reduce(func: Func[T, T, T], iterable: Iterable[T]) -> T`

Apply a function of two arguments cumulatively to the items of
, from left to right, so as to reduce
the iterable to a single value.

**Parameters:**

- `func` (Func[T, T, T]) -- A function of two arguments.
- `iterable` (Iterable[T]) -- The iterable to reduce.

**Returns:** The single result of the cumulative application.

```python
functools.reduce(lambda x, y: x + y, [1, 2, 3, 4, 5])    # 15
```

**Raises:**

- `TypeError` -- Thrown if the iterable is empty.

### `functools.reduce(func: Func[T, T, T], iterable: Iterable[T], initial: T) -> T`

Apply a function of two arguments cumulatively to the items of
, from left to right, starting with
, so as to reduce the iterable to a
single value.

**Parameters:**

- `func` (Func[T, T, T]) -- A function of two arguments.
- `iterable` (Iterable[T]) -- The iterable to reduce.
- `initial` (T) -- The initial (seed) value.

**Returns:** The single result of the cumulative application.

```python
functools.reduce(lambda x, y: x + y, [1, 2, 3, 4, 5], 10)    # 25
```

### `functools.cmp_to_key(func: Func[T, T, int]) -> IComparer[T]`

Convert a comparison function into a key function suitable for use
with sorting. Returns an  that wraps the
comparison function.

**Parameters:**

- `func` (Func[T, T, int]) -- A comparison function that returns a negative number
for less-than, zero for equal, or a positive number for greater-than.

**Returns:** An  wrapping the comparison function.

```python
comparer = functools.cmp_to_key(lambda a, b: a - b)
sorted([3, 1, 2], key=comparer)    # [1, 2, 3]
```

### `functools.compare(x: T, y: T) -> int`
