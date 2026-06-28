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
> - Async comprehensions (`[x async for x in ...]`, `[await f(x) for x in ...]`) are **implemented** inside `async def` functions for list/set/dict comprehensions — see [Async Comprehensions](#async-comprehensions) below. Async *generator expressions* (`(x async for x in ...)`) remain unsupported.
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

### Async Comprehensions

Sharpy **supports** async comprehensions inside `async def` functions. List, set, and dict comprehensions may use:

- An **`async for` clause** to iterate an async iterable (an async generator or any `IAsyncEnumerable<T>`).
- **`await`** in the element expression, in a dict key or value, and in an `if` filter.

```python
async def gen() -> int:        # async generator — annotate its element type
    for i in range(3):
        yield i

async def scale(x: int) -> int:
    return x * 10

async def keep(x: int) -> bool:
    return x > 0

async def collect() -> list[int]:
    # async for clause
    items = [x async for x in gen()]                       # [0, 1, 2]

    # await in the element, plus an awaited filter
    scaled = [await scale(x) async for x in gen() if await keep(x)]  # [10, 20]

    # set and dict comprehensions work the same way
    seen = {x async for x in gen()}                        # {0, 1, 2}
    table = {x: await scale(x) async for x in gen()}       # {0: 0, 1: 10, 2: 20}

    return scaled
```

**Rules and semantics:**

1. **`async def`-only.** Async comprehensions (an `async for` clause or an `await` anywhere in a comprehension) are only valid inside an `async def` function — the same rule Python enforces.
2. **Sequential execution.** Faithful to Python, elements are produced one at a time in order; there is no implicit concurrency. When you want concurrency, use `asyncio.gather` (see [Concurrent Execution](#concurrent-execution)), which maps to `Task.WhenAll()`.
3. **Annotate async generators.** An async generator used in an `async for` clause should declare its element type (e.g. `async def gen() -> int:`) so the comprehension can infer the result type.

*Implementation: ✅ Implemented — each async comprehension lowers to a temporary collection populated by an `await foreach` (for an `async for` clause) or a `foreach` (when only `await` appears in the body), appending each element/entry in order; the temporary is the comprehension's result. Using an `async for` clause or `await` in a comprehension outside `async def` is a compile error.*

> Async **generator expressions** — `(x async for x in src)` — are **not** supported, because Sharpy has no generator-expression construct at all (synchronous generator expressions are likewise unavailable). Use a list/set/dict comprehension or an explicit `async for` loop instead.

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
