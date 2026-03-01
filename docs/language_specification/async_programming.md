# Async Programming

> **Implementation status:** `async def` declarations are **partially implemented** (v0.2.x).
> - `async def` is parsed and emits C# `async` methods returning `Task` or `Task<T>`.
> - `await` expressions are **implemented** — `await` can be used inside `async def` functions to unwrap `Task<T>` results.
> - Async generators (`async def` with `yield`) emit `IAsyncEnumerable<T>` return type.
> - `yield from` in async generators is **implemented** (Sharpy extension beyond Python).
> - Async constructors (`async def __init__`) are rejected at compile time (SPY0358).
>
> See [generators.md](generators.md) for synchronous and async generator documentation.

## Async Functions

```python
async def fetch_data(url: str) -> str:
    await asyncio.sleep(1.0)
    return f"Data from {url}"

async def main():
    result = await fetch_data("https://example.com")
    print(result)
```

*Implementation: ✅ Implemented — `async def` is parsed and emits C# `async Task<T>` methods. `await` expressions unwrap `Task<T>` to `T`. Using `await` outside `async def` or on non-Task types produces compile errors (SPY0273, SPY0274).*

## Concurrent Execution

```python
async def fetch_all(urls: list[str]) -> list[str]:
    tasks = [fetch_data(url) for url in urls]
    results = await asyncio.gather(*tasks)
    return results
```

*Implementation: ❌ Not yet implemented — requires `async def` and `await`. Will map to `Task.WhenAll()`.*

## Async Iteration

```python
async def count_up(n: int) -> int:
    for i in range(n):
        yield i

async def process():
    async for num in count_up(5):
        print(f"Number: {num}")
```

*Implementation: ✅ Implemented — `async def` with `yield` emits `IAsyncEnumerable<T>`. `async for` maps to `await foreach`. `yield from` in async generators auto-detects sync vs async iterables (Sharpy extension beyond Python).*

**No Async Comprehensions:**

Sharpy does not support async comprehensions (`async for` inside comprehensions). C# 9.0's LINQ doesn't natively support `IAsyncEnumerable` in query syntax, making this feature complex to implement.

```python
# ❌ NOT SUPPORTED - async comprehension
results = [x async for x in async_iterator()]

# ❌ NOT SUPPORTED - async comprehension with filter
results = [x async for x in async_iterator() if await predicate(x)]
```

**Await in synchronous comprehension (inside async function):**

Using `await` inside a regular comprehension within an async function is also **not supported**:

```python
async def fetch_all(urls: list[str]) -> list[str]:
    # ❌ NOT SUPPORTED - await inside comprehension
    results = [await fetch(url) for url in urls]

    # ✅ Use explicit loop instead
    results: list[str] = []
    for url in urls:
        results.append(await fetch(url))
    return results

    # ✅ Or use asyncio.gather for concurrent execution
    tasks = [fetch(url) for url in urls]  # Create tasks (no await)
    results = await asyncio.gather(*tasks)
    return results
```

**Rationale:** C# LINQ expressions don't support `await` inside query expressions. While it's technically possible to lower this to explicit loops, we've chosen to reject it for clarity and to encourage the use of `asyncio.gather` for concurrent operations.

**Workarounds:**

```python
# ✅ Use explicit async loop instead
results: list[T] = []
async for x in async_iterator():
    results.append(x)

# ✅ Or with condition
results: list[T] = []
async for x in async_iterator():
    if await predicate(x):
        results.append(x)

# ✅ Use asyncio.gather for concurrent async calls
async def fetch_all(urls: list[str]) -> list[str]:
    return await asyncio.gather(*[fetch(url) for url in urls])
```

Async comprehensions may be added in a future version when better runtime support is available or if user demand justifies the implementation complexity.

**Generator Return Types:**

Functions using `yield` have special return type annotations:

| Pattern | Return Type | Implementation Status |
|---------|-------------|----------------------|
| `yield` in function | `-> T` (compiler infers `IEnumerable<T>`) | ✅ Implemented |
| `yield` in `async def` | `-> T` (compiler infers `IAsyncEnumerable<T>`) | ✅ Implemented |
| `yield from` in function | Same as yielded iterator | ✅ Implemented |
| `yield from` in `async def` | Auto-selects `foreach` or `await foreach` | ✅ Implemented (Sharpy extension) |

```python
# ✅ Synchronous generator
def fibonacci(n: int) -> int:
    a, b = 0, 1
    for _ in range(n):
        yield a
        a, b = b, a + b

# ✅ Async generator
async def async_count(n: int) -> int:
    for i in range(n):
        yield i

# ✅ yield from in async generator (Sharpy extension — not valid Python)
async def combined() -> int:
    yield 1
    yield from sync_items()  # sync iterable → foreach
    yield 2
```

See [generators.md](generators.md) for complete synchronous generator documentation.

## Async Context Managers

```python
class AsyncResource:
    async def __aenter__(self) -> AsyncResource:
        print("entering")
        return self

    async def __aexit__(self):
        print("exiting")

async def main():
    async with AsyncResource() as resource:
        print("using resource")
```

`async with` supports two protocols:
- **Async dunder protocol**: Classes with `__aenter__`/`__aexit__` → try/finally with `await AenterAsync()`/`await AexitAsync()`
- **`IAsyncDisposable`**: .NET types → `await using`

See [context_managers.md](context_managers.md) for full details on both sync and async context manager protocols.

*Implementation: ✅ Implemented — async dunder protocol emits try/finally; IAsyncDisposable emits `await using`.*

---
