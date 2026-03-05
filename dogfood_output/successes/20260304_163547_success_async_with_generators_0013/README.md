# Successful Dogfood Run

**Timestamp:** 2026-03-04T16:32:52.345253
**Feature Focus:** async_with_generators
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Async generator function combining async/await with yield
# An async generator yields values with await between them
import asyncio

async def async_count() -> int:
    n = 0
    while n < 3:
        await asyncio.sleep(0.001)
        yield n
        n += 1

async def collect_values() -> list[int]:
    result: list[int] = []
    async for value in async_count():
        result.append(value)
    return result

def main():
    # Use Task.Result pattern since asyncio.run() is not available
    task = collect_values()
    values = task.Result
    for v in values:
        print(v)

```

## Output

```
0
1
2
```

## Timing

- Generation: 158.16s
- Execution: 5.18s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
