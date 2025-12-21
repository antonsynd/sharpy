# Async Programming

## Async Functions

```python
async def fetch_data(url: str) -> str:
    await asyncio.sleep(1.0)
    return f"Data from {url}"

async def main():
    result = await fetch_data("https://example.com")
    print(result)
```

*Implementation: ✅ Native - `async` method returning `Task<T>`.*

## Concurrent Execution

```python
async def fetch_all(urls: list[str]) -> list[str]:
    tasks = [fetch_data(url) for url in urls]
    results = await asyncio.gather(*tasks)
    return results
```

*Implementation: ✅ Native - `Task.WhenAll()`*

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

**No Async Comprehensions:**

Sharpy does not support async comprehensions (`async for` inside comprehensions). C# 9.0's LINQ doesn't natively support `IAsyncEnumerable` in query syntax, making this feature complex to implement.

```python
# ❌ Not supported - async comprehension
results = [x async for x in async_iterator()]
results = [x async for x in async_iterator() if await predicate(x)]

# ✅ Use explicit async loop instead
results: list[T] = []
async for x in async_iterator():
    results.append(x)

# ✅ Or with condition
results: list[T] = []
async for x in async_iterator():
    if await predicate(x):
        results.append(x)
```

Async comprehensions may be added in a future version when better runtime support is available.

**Generator Return Types:**

Functions using `yield` have special return type annotations:

| Pattern | Return Type | Notes |
|---------|-------------|-------|
| `yield` in function | `Iterator[T]` | Synchronous generator |
| `yield` in `async def` | `AsyncIterator[T]` | Asynchronous generator |
| `yield from` | Same as yielded iterator | Delegation |

```python
# Synchronous generator
def fibonacci(n: int) -> Iterator[int]:
    a, b = 0, 1
    for _ in range(n):
        yield a
        a, b = b, a + b

# Async generator
async def stream_data(url: str) -> AsyncIterator[bytes]:
    async with http_client.stream(url) as response:
        async for chunk in response:
            yield chunk
```

*Implementation: ✅ Native - `IAsyncEnumerable<T>` (C# 8+)*

## Async Context Managers

```python
async def use_resource():
    async with AsyncResource() as resource:
        await resource.process()
```

*Implementation: 🔄 Lowered - `await using (var r = resource) { ... }`*

---
