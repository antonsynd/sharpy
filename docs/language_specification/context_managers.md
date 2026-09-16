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

## `as` Targets

The `as` clause accepts any **store target**, not only a name — the same set the `for` statement
accepts: a name, an attribute, an index, or a tuple of targets.

```python
class CMPair:
    def __enter__(self) -> tuple[int, str]:
        return (1, "two")

    def __exit__(self) -> None:
        pass


def main() -> None:
    with CMPair() as (a, b):     # tuple target: binds a = 1, b = "two"
        print(a)
        print(b)
```

A tuple target requires `__enter__` (or `__aenter__`) to return a tuple of the same arity;
anything else is `SPY0239` (*invalid tuple unpacking*). A **starred** target (`as *rest`) is not
a target at all — Python rejects it as a `SyntaxError` and so does Sharpy.

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

If neither protocol is implemented, a compile-time error is reported (SPY0324).

*Implementation*
- *✅ `with` statement: Dunder protocol → try/finally with Enter()/Exit(); IDisposable → C# `using`*
- *✅ `async with` statement: Async dunder protocol → try/finally with await AenterAsync()/AexitAsync(); IAsyncDisposable → C# `await using`*
- *✅ Multiple resources in a single `with` statement are supported*

---

## Exit Shapes

The `__exit__` / `__aexit__` parameter count determines the **exit shape** — how the compiler
models the `with` block's control flow and what the emitter generates.

| Parameters | Exit shape | C# lowering | Can suppress? |
|------------|------------|-------------|---------------|
| `self` only | **Simple** | `try / finally { Exit() }` | No |
| `self` + 3 (`exc_type`, `exc_val`, `exc_tb`) | **Suppression-capable** | `try / catch { suppress = Exit(…); if (!suppress) throw; } / finally { … }` | Yes |
| `IDisposable` | **Simple** | `using (…) { … }` | No |
| `IAsyncDisposable` | **Simple** | `await using (…) { … }` | No |

The exit shape is a **materialized semantic fact** — it is decided during type checking and
carried to code generation on the IR. The emitter never inspects `__exit__` itself.

### Suppression-capable `with` and reachability

When a `with` body's only exit is a `return`, `raise`, or `?` early return, the code after the
`with` is still reachable if the manager is suppression-capable: `__exit__` returning `True`
suppresses the exception and falls through. For a simple manager, the code after the `with` is
unreachable and must not contain a `return` path.

```python
class Suppressor:
    def __enter__(self) -> Suppressor:
        return self

    def __exit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool:
        return True

def f() -> int:
    with Suppressor() as s:
        return 9
    # error SPY0266: function 'f' must return a value of type 'int32' in all code paths
```

A function returning `None` whose body raises inside a suppressing `with` falls through —
`__exit__` suppresses the exception:

```python
class Suppressor:
    def __enter__(self) -> Suppressor:
        return self

    def __exit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool:
        return True

def g() -> None:
    with Suppressor() as s:
        raise ValueError("test")
    print("after")   # reachable — __exit__ suppresses the exception

def main() -> None:
    g()
```

```
after
```

### Definite assignment across a suppression-capable `with`

Because `__exit__` may return `True` and swallow an exception raised **anywhere** in the body, the
statement after the `with` is reachable with the body only partly executed — a bare local assigned
inside the body may or may not have been assigned when control reaches the read. Sharpy resolves this
**at runtime, like Python** ([#1839](https://github.com/antonsynd/sharpy/issues/1839)): such a read
is *runtime-checked*, not refused. When the body completes normally the read finds the value; when a
suppressed exception skipped the assignment the read raises `UnboundLocalError` — nothing that runs
in Python is refused, and nothing prints a silent default.

```python
class Suppressor:
    def __enter__(self) -> int:
        return 1

    def __exit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool:
        return True

def main() -> None:
    n: int
    with Suppressor():
        n = 5
    print(n)
```

```
5
```

If the body raises before the assignment and `__exit__` swallows it, the read after the `with` finds
`n` unset and raises `UnboundLocalError` (Python 3.12's exact wording), caught here to show it:

```python
class Suppressor:
    def __enter__(self) -> int:
        return 1

    def __exit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool:
        return True

def maybe_raise(flag: bool) -> None:
    if flag:
        raise ValueError("boom")

def main() -> None:
    n: int
    with Suppressor():
        maybe_raise(True)
        n = 5
    try:
        print(n)
    except UnboundLocalError as e:
        print(str(e))
```

```
cannot access local variable 'n' where it is not associated with a value
```

The cost is one `bool` per runtime-checked local (its assigned-flag). A manager that **cannot**
suppress has no suppression edge, so a body assignment is unconditional and its read after the
`with` runs with no flag at all. A read that is unassigned even ignoring suppression — a
*conditional* assignment, say — is a plain definite-assignment hole and is still refused (SPY0600),
under every manager:

<!-- spec-sweep: error SPY0600 -->
```python
class Suppressor:
    def __enter__(self) -> int:
        return 1

    def __exit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool:
        return True

def main() -> None:
    c: bool = True
    n: int
    with Suppressor():
        if c:
            n = 5
    print(n)   # error SPY0600: variable 'n' is used before being assigned
```

> **`as` targets are block-scoped.** Unlike Python, a `with … as name` target does not persist after
> the block (a fresh `as` target read outside is SPY0200). A pre-declared local rebound by
> `with … as n` therefore is **not** assigned after the block, and reading it is an ordinary
> use-before-assign — the runtime check above applies to BODY assignments, not to the `as` target.

### `yield` inside a suppression-capable `with` (SPY0703)

A `yield` inside a `with` whose `__exit__` can suppress is refused. The C# iterator state machine
cannot resume inside a `try/catch`, and the suppression edge makes the successor reachable:

```python
class Suppressor:
    def __enter__(self) -> Suppressor:
        return self

    def __exit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool:
        return True

def gen() -> int:
    with Suppressor() as s:
        yield 1   # error SPY0703: 'yield' cannot be used inside a 'with' block
                  # whose '__exit__' is suppression-capable
```

A simple (1-parameter) `__exit__` poses no issue — the `with` lowers to `try/finally`,
and C# allows `yield return` inside a finally-free try body:

```python
class Simple:
    def __enter__(self) -> Simple:
        return self

    def __exit__(self):
        pass

def gen() -> int:
    with Simple() as s:
        yield 1   # OK — simple exit shape
    yield 2
```

## `__exit__` Signature Variants

**Status:** Implemented

### Supported Signatures

Sharpy supports two forms for `__exit__` and `__aexit__`:

1. **No-arg form**: `def __exit__(self):` — cleanup only, no exception awareness (simple exit shape)
2. **3-arg form**: `def __exit__(self, exc_type, exc_val, exc_tb):` — receives exception context, can suppress exceptions by returning `True` (suppression-capable exit shape)

The same applies to the async variants (`__aexit__`).

```python
class Resource:
    def __enter__(self) -> Resource:
        return self

    # No-arg form — cleanup only
    def __exit__(self):
        self.cleanup()
```

```python
class SuppressingResource:
    def __enter__(self) -> SuppressingResource:
        return self

    # 3-arg form — exception-aware
    def __exit__(self, exc_type: object?, exc_val: Exception?, exc_tb: object?) -> bool:
        if exc_val is not None:
            print(f"Suppressing {exc_type}: {exc_val}")
            return True   # suppress the exception
        return False       # propagate
```

The `ProtocolRegistry` enforces `ExpectedParamCount: 1` (just `self`) for both `__exit__` and `__aexit__`, with `AlternateParamCount: 4` for the 3-arg exception-aware form. The no-arg `__exit__` method maps directly to `IDisposable.Dispose()` via `ClrMethodName: "Dispose"`, and `__aexit__` maps to `IAsyncDisposable.DisposeAsync()`.

### Codegen

For the no-arg form, the emitter generates a simple try/finally:

```csharp
var __ctx_0 = new Resource();
var r = __ctx_0.Enter();
try {
    // body
} finally {
    __ctx_0.Exit();
}
```

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

**Note:** The `exc_tb` parameter is always `null` since .NET has no direct equivalent of Python's traceback object. Stack trace information is available via the `Exception` object itself.
