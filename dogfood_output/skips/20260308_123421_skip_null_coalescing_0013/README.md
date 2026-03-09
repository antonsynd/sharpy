# Skipped Dogfood Run

**Timestamp:** 2026-03-08T12:12:35.836210
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0220]: Cannot pass argument of type 'None' to parameter of type 'DataSource?'
  --> /tmp/tmpik98qbc0/dogfood_test.spy:85:28
    |
 85 |     print(get_cached_value(None))
    |                            ^^^^
    |


**Feature Focus:** null_coalescing
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex null coalescing test with class hierarchies, type aliases,
# chained fallbacks, and nullable collections.
type MaybeInt = int?

@abstract
class DataSource:
    @abstract
    def fetch(self) -> MaybeInt: ...

    def get_value_or_default(self, default: int) -> int:
        # Using null coalescing in base class method
        result: MaybeInt = self.fetch()
        return result ?? default

class MemoryCache(DataSource):
    _data: MaybeInt

    def __init__(self, initial: MaybeInt):
        self._data = initial

    @override
    def fetch(self) -> MaybeInt:
        return self._data

class FallbackCache(MemoryCache):
    _fallback: DataSource?

    def __init__(self, initial: MaybeInt, fallback: DataSource?):
        super().__init__(initial)
        self._fallback = fallback

    @override
    def fetch(self) -> MaybeInt:
        # First try own data via parent implementation
        own: MaybeInt = super().fetch()
        if own is not None:
            return own
        # Use null conditional access with coalescing for fallback chain
        return self._fallback?.fetch() ?? 0

def resolve_config_value(primary: MaybeInt, secondary: MaybeInt, tertiary: int) -> int:
    # Three-level fallback chain using null coalescing
    return primary ?? secondary ?? tertiary

def get_cached_value(cache: DataSource?) -> int:
    # Complex chain: cache access with null conditional, then coalescing
    value_from_cache: MaybeInt = cache?.fetch()
    return value_from_cache ?? -1

def main():
    # Test 1: Simple null coalescing with nullable int
    val1: MaybeInt = None()
    print(val1 ?? 100)

    # Test 2: Loop iteration with null coalescing accumulation
    # Must build list with .append() due to invariance
    items: list[MaybeInt] = []
    items.append(Some(1))
    items.append(None())
    items.append(Some(3))
    items.append(None())
    items.append(Some(5))
    total: int = 0
    for item in items:
        total = total + (item ?? 0)
    print(total)

    # Test 3: Method returning nullable with coalescing fallback
    mem: MemoryCache = MemoryCache(None())
    print(mem.get_value_or_default(50))

    # Test 4: Multi-level fallback chain (first non-null wins)
    print(resolve_config_value(None(), Some(25), 75))

    # Test 5: Complex object chaining with valid fallback
    inner: MemoryCache = MemoryCache(Some(99))
    outer: FallbackCache = FallbackCache(None(), inner)
    print(outer.fetch())

    # Test 6: Null cache with fallback to zero default
    empty: FallbackCache = FallbackCache(None(), None)
    print(empty.fetch())

    # Test 7: Function with nullable parameter (None)
    print(get_cached_value(None))

    # Test 8: Function with valid cache instance
    valid: MemoryCache = MemoryCache(Some(888))
    print(get_cached_value(valid))

```

## Timing

- Generation: 1288.71s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
