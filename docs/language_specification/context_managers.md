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
