# Async Programming

> **Implementation status:** ✅ Implemented (v0.2.x).
> - `async def` is parsed and emits C# `async` methods returning `Task` or `Task<T>`.
> - `await` expressions are **implemented** — `await` can be used inside `async def` functions to unwrap `Task<T>` results.
> - `async for` is **implemented** — maps to `await foreach` over `IAsyncEnumerable<T>`.
> - `async with` is **implemented** — supports both dunder protocol and `IAsyncDisposable`, including multiple context managers.
> - `asyncio.gather()` is **implemented** — maps to `Task.WhenAll()`.
> - Async generators (`async def` with `yield`) emit `IAsyncEnumerable<T>` return type.
> - `yield from` in async generators is **implemented** (Sharpy extension beyond Python).
> - Async constructors (`async def __init__`) are rejected at compile time (SPY0358).
> - Async comprehensions (`[x async for x in ...]`, `[await f(x) for x in ...]`) are a **deliberate non-feature** — see [Async Comprehensions](#async-comprehensions--deliberate-non-feature) below.
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

*Implementation: ✅ Implemented — `asyncio.gather(*tasks)` maps to `await Task.WhenAll(tasks)`.*

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

### Async Comprehensions — Deliberate Non-Feature

Sharpy **intentionally does not support** async comprehensions (`async for` inside comprehensions) and **will not add them**. This is a deliberate design decision, not a missing feature or planned work item.

**What is excluded:**

```python
# ❌ NOT SUPPORTED — async for inside comprehension
results = [x async for x in async_iterator()]

# ❌ NOT SUPPORTED — async for with filter
results = [x async for x in async_iterator() if await predicate(x)]

# ❌ NOT SUPPORTED — await inside comprehension
results = [await fetch(url) for url in urls]
```

**Why this is intentional:**

1. **C# LINQ does not support `await` in query expressions.** Comprehensions lower to LINQ (`.Select()`, `.Where()`, `.SelectMany()`), and C# lambdas passed to LINQ methods cannot use `await` without significant transformation.
2. **Lowering to explicit loops would be misleading.** Async comprehensions in Python execute sequentially, but look like they could be concurrent. Requiring explicit loops makes the sequential nature visible.
3. **`asyncio.gather` is the right pattern for concurrency.** When concurrent execution is desired, `asyncio.gather` maps directly to `Task.WhenAll()` and expresses intent clearly.

**Workarounds:**

```python
# ✅ Use explicit async loop for sequential async iteration
results: list[T] = []
async for x in async_iterator():
    results.append(x)

# ✅ With condition
results: list[T] = []
async for x in async_iterator():
    if await predicate(x):
        results.append(x)

# ✅ Use asyncio.gather for concurrent async calls
async def fetch_all(urls: list[str]) -> list[str]:
    return await asyncio.gather(*[fetch(url) for url in urls])
```

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

**Multiple context managers** are supported in a single `async with` statement, just like synchronous `with`:

```python
async def main():
    async with ResourceA() as a, ResourceB() as b:
        print(f"using {a} and {b}")
```

Each context manager is nested inside-out in the generated code (last item wraps the body, first item wraps everything), ensuring proper cleanup order.

See [context_managers.md](context_managers.md) for full details on both sync and async context manager protocols.

*Implementation: ✅ Implemented — async dunder protocol emits try/finally; IAsyncDisposable emits `await using`. Multiple context managers in a single `async with` are supported.*

---
