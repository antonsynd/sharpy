# Skipped Dogfood Run

**Timestamp:** 2026-03-10T10:16:42.489271
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0102]: Expected newline, got Class
  --> /tmp/tmp6zhmpc1e/dogfood_test.spy:5:11
    |
  5 | @abstract class DataSource:
    |           ^^^^^
    |


**Feature Focus:** class_inheritance
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Abstract class inheritance with constructor chaining
type CacheKey = str
type CacheValue = int

@abstract class DataSource:
    _cache: dict[str, int]

    def __init__(self):
        self._cache = {}

    @abstract
    def get_name(self) -> str:
        ...

    @virtual
    def get_size(self) -> int:
        return len(self._cache)

    @abstract
    def fetch(self, key: str) -> int:
        ...

    def has_key(self, key: str) -> bool:
        return key in self._cache

class StringCache(DataSource):
    _prefix: str

    def __init__(self, prefix: str):
        super().__init__()
        self._prefix = prefix

    @override
    def get_name(self) -> str:
        return self._prefix + ":StringCache"

    @override
    def fetch(self, key: str) -> int:
        return self._cache[key]

    def put(self, key: str, value: int) -> None:
        self._cache[key] = value

class NumericCache(StringCache):
    _scale: float

    def __init__(self, prefix: str, scale: float):
        super().__init__(prefix)
        self._scale = scale

    @override
    def get_name(self) -> str:
        return self._prefix + ":NumericCache"

    @override
    def get_size(self) -> int:
        base = super().get_size()
        return base + 1

    def put_scaled(self, key: str, value: float) -> None:
        scaled = int(value * self._scale)
        self.put(key, scaled)

def main():
    # Create instance with constructor chaining
    cache = NumericCache("metrics", 10.0)

    # Test inherited methods
    print(cache.get_name())
    print(cache.get_size())

    # Put values
    cache.put("a", 5)
    cache.put_scaled("b", 3.5)

    # Test has_key (inherited from base)
    print(cache.has_key("a"))
    print(cache.has_key("z"))

    # Test fetch and overridden size
    print(cache.fetch("a"))
    print(cache.fetch("b"))
    print(cache.get_size())

    # Verify type alias works
    k: CacheKey = "test_key"
    cache.put(k, 100)
    print(cache.has_key("test_key"))

    # Test polymorphism through abstract type
    source: DataSource = cache
    print(source.get_name())
    print(source.get_size())

```

## Timing

- Generation: 534.34s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
