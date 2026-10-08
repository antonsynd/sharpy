# functools

Higher-order functions and operations on callable objects.

```python
import functools
```

## Functions

### `functools.reduce(func: (T, T) -> T, iterable: list[T]) -> T`

Apply function of two arguments cumulatively to the items of iterable, so as to reduce the iterable to a single value.

### `functools.reduce(func: (T, T) -> T, iterable: list[T], initial: T) -> T`

Apply function of two arguments cumulatively to the items of iterable, starting with initial value.

### `functools.cmp_to_key(cmp: (T, T) -> int) -> IComparer[T]`

Convert a comparison function into a key function for sorting.
The comparison function should return a negative number for less-than,
zero for equality, or a positive number for greater-than.
