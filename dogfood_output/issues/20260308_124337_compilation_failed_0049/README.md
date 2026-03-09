# Issue Report: compilation_failed

**Timestamp:** 2026-03-08T12:39:43.897114
**Type:** compilation_failed
**Feature Focus:** async_for
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
import asyncio

interface IAsyncProcessor[T]:
    async def process(self, item: T) -> T: ...

class AsyncDataSource:
    @virtual
    async def fetch_batch(self, start: int, count: int) -> int:
        yield 0

    async def sum_items(self, items: list[int]) -> int:
        total = 0
        for item in items:
            total += item
        return total

class FilteredDataSource(AsyncDataSource):
    threshold: int

    def __init__(self, threshold: int):
        self.threshold = threshold

    @override
    async def fetch_batch(self, start: int, count: int) -> int:
        i = 0
        while i < count:
            value = start + i
            if value > self.threshold:
                yield value
            i += 1
            await asyncio.sleep(0.001)

class DoublingProcessor(IAsyncProcessor[int]):
    async def process(self, item: int) -> int:
        await asyncio.sleep(0.001)
        return item * 2

async def main():
    source: FilteredDataSource = FilteredDataSource(5)
    processor: DoublingProcessor = DoublingProcessor()

    # First async for: process and print filtered values
    results: list[int] = []
    async for value in source.fetch_batch(1, 10):
        processed = await processor.process(value)
        results.append(processed)

    for r in results:
        print(r)

    # Second async for on a fresh stream with different logic
    data2 = source.fetch_batch(1, 10)
    count = 0
    total = 0
    async for val in data2:
        count += 1
        total += val
        if count >= 3:
            break

    print(count)
    print(total)

```

## Error

```
Assembly compilation failed:

error[CS0738]: 'DogfoodTest.DoublingProcessor' does not implement interface member 'DogfoodTest.IAsyncProcessor<int>.Process(int)'. 'DogfoodTest.DoublingProcessor.Process(int)' cannot implement 'DogfoodTest.IAsyncProcessor<int>.Process(int)' because it does not have the matching return type of 'int'.
  --> /tmp/tmpe9fpmimn/dogfood_test.spy:25:38
    |
 25 |         i = 0
    |              ^
    |


```

## Compiler Output

```
warning[SPY0451]: Local variable 'processor' is assigned but never used
  --> /tmp/tmpe9fpmimn/dogfood_test.spy:40:5
    |
 40 |     processor: DoublingProcessor = DoublingProcessor()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'asyncio' is never used
  --> /tmp/tmpe9fpmimn/dogfood_test.spy:1:8
    |
  1 | import asyncio
    |        ^^^^^^^
    |


```

## Generated C#

```csharp
warning[SPY0451]: Local variable 'processor' is assigned but never used
  --> /tmp/tmpe9fpmimn/dogfood_test.spy:40:5
    |
 40 |     processor: DoublingProcessor = DoublingProcessor()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

warning[SPY0452]: Imported name 'asyncio' is never used
  --> /tmp/tmpe9fpmimn/dogfood_test.spy:1:8
    |
  1 | import asyncio
    |        ^^^^^^^
    |

Generated C# code written to: /tmp/tmpe9fpmimn/dogfood_test.cs

```

## Timing

- Generation: 212.83s
- Execution: 4.84s
