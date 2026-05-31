# functools

Higher-order functions and operations on callable objects.

```python
import functools
```

## Functions

### `functools.reduce(func: (T, T) -> T, iterable: Iterable[T], initial: T) -> T`

Apply function of two arguments cumulatively to the items of iterable, with an initial value.

### `functools.cmp_to_key(func: (T, T) -> int) -> Comparer[T]`

Transform a comparison function into a key function for use with sorted() and friends.

### `functools.reduce(func: (T, T) -> T, iterable: list[T]) -> T`

Apply function of two arguments cumulatively to the items of iterable, so as to reduce the iterable to a single value.

### `functools.cache_info(hits: int, misses: int, max_size: int | None, current_size: int) -> record`

Snapshot of cache statistics returned by
`LruCache{TKey, TResult}.CacheInfo`.

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
