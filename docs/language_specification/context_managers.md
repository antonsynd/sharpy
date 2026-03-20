# Context Managers

The `with` statement manages resources:

```python
with open("file.txt", "r") as f:
    content = f.read()
# f.close() called automatically

# Multiple resources
with open("in.txt") as input, open("out.txt", "w") as output:
    output.write(input.read())
```

## Supported Protocols

Sharpy supports two context manager protocols:

### 1. Dunder Protocol (`__enter__`/`__exit__`)

Classes can implement `__enter__` and `__exit__` to define context manager behavior:

```python
class Resource:
    def __enter__(self) -> Resource:
        print("entering")
        return self

    def __exit__(self):
        print("exiting")

def main():
    with Resource() as r:
        print("using resource")
    # prints: entering, using resource, exiting
```

**Protocol methods:**
- `__enter__(self) -> T` — Called on block entry. The return value is bound to the `as` variable.
- `__exit__(self)` — Called on block exit (in a `finally` clause), handles cleanup.

**C# emission:**
```csharp
var __ctx_0 = new Resource();
var r = __ctx_0.Enter();
try {
    System.Console.WriteLine("using resource");
} finally {
    __ctx_0.Exit();
}
```

### 2. IDisposable Protocol

Objects implementing .NET's `IDisposable` interface can be used directly in `with` statements:

```python
with open("file.txt", "r") as f:
    content = f.read()
```

**C# emission:**
```csharp
using (var f = Builtins.Open("file.txt", "r")) {
    var content = f.Read();
}
```

## Async Context Managers

The `async with` statement supports async resource management:

### 1. Async Dunder Protocol (`__aenter__`/`__aexit__`)

```python
class AsyncResource:
    async def __aenter__(self) -> AsyncResource:
        print("entering")
        return self

    async def __aexit__(self):
        print("exiting")

async def main():
    async with AsyncResource() as r:
        print("inside")
```

**Protocol methods:**
- `async def __aenter__(self) -> T` — Async enter, return value bound to `as` variable.
- `async def __aexit__(self)` — Async cleanup.

**C# emission:**
```csharp
var __ctx_0 = new AsyncResource();
var r = await __ctx_0.AenterAsync();
try {
    System.Console.WriteLine("inside");
} finally {
    await __ctx_0.AexitAsync();
}
```

### 2. IAsyncDisposable Protocol

Objects implementing .NET's `IAsyncDisposable` are emitted as `await using`:

```csharp
await using (var r = expr) {
    // body
}
```

## Protocol Priority

When a type implements both protocols, dunders take priority:

| Statement | Priority | Fallback |
|-----------|----------|----------|
| `with` | `__enter__`/`__exit__` | `IDisposable` |
| `async with` | `__aenter__`/`__aexit__` | `IAsyncDisposable` |

If neither protocol is implemented, a compile-time error is reported (SPY0332).

*Implementation*
- *✅ `with` statement: Dunder protocol → try/finally with Enter()/Exit(); IDisposable → C# `using`*
- *✅ `async with` statement: Async dunder protocol → try/finally with await AenterAsync()/AexitAsync(); IAsyncDisposable → C# `await using`*
- *✅ Multiple resources in a single `with` statement are supported*

---

## RFC: `__exit__` Signature Variants

**Status:** RFC — implementation deferred

### Current Behavior

Sharpy currently requires the no-arg form (self-only) for `__exit__` and `__aexit__`:

```python
class Resource:
    def __enter__(self) -> Resource:
        return self

    def __exit__(self):        # self-only — the only accepted signature
        self.cleanup()
```

The `ProtocolRegistry` enforces `ExpectedParamCount: 1` (just `self`) for both `__exit__` and `__aexit__`. The `__exit__` method maps directly to `IDisposable.Dispose()` via `ClrMethodName: "Dispose"`, and `__aexit__` maps to `IAsyncDisposable.DisposeAsync()`.

This means Sharpy context managers have no way to inspect or suppress exceptions that occur within the `with` block.

### Proposed Addition

In Python, `__exit__` accepts three additional parameters for exception context:

```python
def __exit__(self, exc_type: type | None, exc_val: BaseException | None, exc_tb: TracebackType | None) -> bool:
    if exc_val is not None:
        print(f"Suppressing {exc_type}: {exc_val}")
        return True   # suppress the exception
    return False       # propagate
```

This RFC proposes supporting both signatures:

1. **No-arg form** (current): `def __exit__(self):` — cleanup only, no exception awareness
2. **3-arg form** (proposed): `def __exit__(self, exc_type, exc_val, exc_tb):` — receives exception context, can suppress exceptions by returning `True`

The same applies to the async variants (`__aexit__`).

### Design Options

#### Option A: Always Use IDisposable, Ignore Exception Args

Accept the 3-arg signature syntactically but still emit `IDisposable.Dispose()`. The exception parameters would be unused and the return value ignored.

- **Pro:** Simplest implementation — `ProtocolRegistry` just accepts param count 1 or 4; codegen unchanged.
- **Con:** Misleading — users write exception-handling code that silently does nothing. Violates Axiom 3 (type safety) by accepting parameters that are never populated.

#### Option B: Custom `IContextManager<T>` Interface

Define a Sharpy.Core interface:

```csharp
public interface IContextManager<T>
{
    T Enter();
    bool Exit(Type? excType, Exception? excVal, object? excTb);
}
```

The 3-arg `__exit__` would emit an implementation of `IContextManager<T>.Exit(...)`. The `with` statement codegen would detect which interface is implemented and emit the appropriate call pattern.

- **Pro:** Clean .NET interop — types are explicit, suppression semantics are clear. Aligns with Axiom 1 (.NET first).
- **Con:** Introduces a new interface into Sharpy.Core. The `exc_tb` parameter has no direct .NET equivalent (Python's traceback object has no CLR counterpart). The `bool` return for exception suppression diverges from `IDisposable` conventions.

#### Option C: Codegen Wraps in Try/Catch/Finally

For the 3-arg form, the emitter generates a try/catch/finally pattern that captures exception information and passes it to `Exit()`:

```csharp
var __ctx_0 = new Resource();
var r = __ctx_0.Enter();
Exception? __exc_0 = null;
try {
    // body
} catch (Exception __e_0) {
    __exc_0 = __e_0;
    var __suppress = __ctx_0.Exit(__e_0.GetType(), __e_0, null);
    if (!__suppress) throw;
} finally {
    if (__exc_0 == null) __ctx_0.Exit(null, null, null);
}
```

- **Pro:** Full Python semantics — exception suppression works. No new interface needed; the emitter handles the dispatch difference.
- **Con:** More complex codegen. The `exc_tb` parameter is always `null` (no CLR traceback). Generated code is harder to debug. Performance overhead from the try/catch even when no exception occurs.

### Recommendation

Option C is the most faithful to Python semantics and does not require new Sharpy.Core interfaces. However, the `exc_tb` parameter should be typed as `object?` (always `None`) since .NET has no traceback equivalent — this should be documented clearly to avoid user confusion. Option B is the cleanest from a .NET-first perspective but requires more design work around the interface shape.

A hybrid approach is also possible: use Option C for codegen but define the `IContextManager<T>` interface from Option B as the public contract, allowing .NET consumers to implement context managers naturally.

### Open Questions

1. Should the `exc_tb` parameter be omitted entirely (making it a 2-arg form: `exc_type`, `exc_val`) since .NET has no traceback equivalent?
2. Should `__exit__` returning `True` suppress exceptions, matching Python semantics exactly?
3. How should this interact with `IDisposable` types — should a type implementing both `__exit__(self, ...)` and `IDisposable` prefer the dunder protocol (current priority rule)?
4. Should `__aexit__` support the same 3-arg variant with identical semantics?
