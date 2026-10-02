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

### `functools.get_or_add(key: TKey, factory: (TKey) -> TResult) -> TResult`

Looks up the cached value for *key*, or computes it
via *factory* and stores the result.

**Parameters:**

- `key` (TKey) -- The cache key.
- `factory` ((TKey) -> TResult) -- A factory invoked on miss to compute the value.

**Returns:** The cached or freshly-computed value.

### `functools.cache_info() -> CacheInfo`

Returns a snapshot of cache statistics.

### `functools.cache_clear()`

Clears all cached entries and resets the hit/miss counters.
