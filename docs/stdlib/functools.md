# functools

Thread-safe memoization cache backing the `@functools.lru_cache` and
`@functools.cache` decorators.

```python
import functools
```

## Functions

### `functools.reduce(func: Func[T, T, T], iterable: Iterable[T], initial: T) -> T`

### `functools.cmp_to_key(func: Func[T, T, int]) -> IComparer[T]`

### `functools.reduce(func: global::System.Func<T, T, T>, iterable: list[T]) -> T`

### `functools.cache_info(hits: int, misses: int, max_size: int?, current_size: int) -> record`

Snapshot of cache statistics returned by
`LruCache{TKey, TResult}.CacheInfo`.

### `functools.get_or_add(key: TKey, factory: Func[TKey, TResult]) -> TResult`

Looks up the cached value for *key*, or computes it
via *factory* and stores the result.

**Parameters:**

- `key` (TKey) -- The cache key.
- `factory` (Func[TKey, TResult]) -- A factory invoked on miss to compute the value.

**Returns:** The cached or freshly-computed value.

### `functools.cache_info() -> CacheInfo`

Returns a snapshot of cache statistics.

### `functools.cache_clear()`

Clears all cached entries and resets the hit/miss counters.
