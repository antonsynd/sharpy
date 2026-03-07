# Issue Report: compilation_failed

**Timestamp:** 2026-03-07T04:01:21.597244
**Type:** compilation_failed
**Feature Focus:** maybe_expression
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test maybe expression with complex logic using only supported features
# maybe converts T | None to T? (Optional)

enum CacheStatus:
    HIT = 1
    MISS = 2

interface ICache[T]:
    def get(self, key: str) -> T | None: ...

class SimpleCache(ICache[str]):
    _data: dict[str, str]

    def __init__(self):
        self._data = {"key1": "value1"}

    def get(self, key: str) -> str | None:
        if key in self._data:
            return self._data[key]
        return None

def determine_status[T](raw: T | None) -> CacheStatus:
    opt: T? = maybe raw
    if opt.is_some():
        return CacheStatus.HIT
    return CacheStatus.MISS

def format_result[T](raw: T | None) -> str:
    opt: T? = maybe raw
    mapped: str? = opt.map(lambda x: str(x))
    return mapped.unwrap_or("null")

def main():
    cache: SimpleCache = SimpleCache()
    
    raw1: str | None = cache.get("key1")
    status1: CacheStatus = determine_status(raw1)
    print(status1.name)
    print(format_result(raw1))
    
    raw2: str | None = cache.get("unknown")
    status2: CacheStatus = determine_status(raw2)
    print(status2.name)
    print(format_result(raw2))

```

## Error

```
Assembly compilation failed:

error[CS0452]: The type 'T' must be a reference type in order to use it as parameter 'T' in the generic type or method 'Optional.From<T>(T?)'
  --> /tmp/tmpg9o96tul/dogfood_test.spy:23:51
    |
 23 |     opt: T? = maybe raw
    |                        ^
    |

error[CS0452]: The type 'T' must be a reference type in order to use it as parameter 'T' in the generic type or method 'Optional.From<T>(T?)'
  --> /tmp/tmpg9o96tul/dogfood_test.spy:29:51
    |
 29 |     opt: T? = maybe raw
    |                        ^
    |


```

## Generated C#

```csharp
Generated C# code written to: /tmp/tmpg9o96tul/dogfood_test.cs

```

## Timing

- Generation: 612.10s
- Execution: 4.59s
