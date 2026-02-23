# Async Programming

> **Implementation status:** `async def` declarations are **partially implemented** (v0.2.x).
> - `async def` is parsed and emits C# `async` methods returning `Task` or `Task<T>`.
> - `await` expressions are **not yet implemented** — the `await` keyword is reserved but not parsed.
> - Async generators (`async def` with `yield`) are rejected at compile time (SPY0358).
> - Async constructors (`async def __init__`) are rejected at compile time (SPY0358).
> - `async for`, `async with`, and `asyncio` are not yet implemented.
>
> See [generators.md](generators.md) for synchronous generator support (`yield`/`yield from`).

## Async Functions

```python
async def fetch_data(url: str) -> str:
    await asyncio.sleep(1.0)
    return f"Data from {url}"

async def main():
    result = await fetch_data("https://example.com")
    print(result)
```

*Implementation: ⚠️ Partially implemented — `async def` is parsed and emits C# `async Task<T>` methods. `await` expressions are not yet parsed (planned for Phase 10, v0.2.4). Without `await`, async functions can be called with `.Result` or `.Wait()` from synchronous code.*

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
async def count_up(n: int) -> AsyncIterator[int]:
    for i in range(n):
        await asyncio.sleep(0.1)
        yield i

async def process():
    async for num in count_up(5):
        print(f"Number: {num}")
```

*Implementation: ❌ Not yet implemented — requires `async def`, `await`, and `async for`. Will map to `IAsyncEnumerable<T>` (C# 8+).*

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
| `yield` in `async def` | `AsyncIterator[T]` → `IAsyncEnumerable<T>` | ❌ Not yet implemented |
| `yield from` | Same as yielded iterator | ✅ Implemented |

```python
# ✅ Synchronous generator (implemented)
def fibonacci(n: int) -> int:
    a, b = 0, 1
    for _ in range(n):
        yield a
        a, b = b, a + b

# ❌ Async generator (not yet implemented)
async def stream_data(url: str) -> AsyncIterator[bytes]:
    async with http_client.stream(url) as response:
        async for chunk in response:
            yield chunk
```

See [generators.md](generators.md) for complete synchronous generator documentation.

## Async Context Managers

```python
async def use_resource():
    async with AsyncResource() as resource:
        await resource.process()
```

*Implementation: ❌ Not yet implemented — requires `async with`. Will map to `await using (var r = resource) { ... }`.*

---
