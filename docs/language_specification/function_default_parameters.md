# Function Default Parameters

Functions can specify default values for parameters. Parameters with defaults must come after required parameters.

```python
def greet(name: str, greeting: str = "Hello") -> str:
    return f"{greeting}, {name}!"

def connect(host: str, port: int = 8080, timeout: float = 30.0) -> Connection:
    # ...
```

## Compile-Time Constant Requirement

Default parameter values must be compile-time constants, matching C# semantics. This eliminates the "mutable default argument" pitfall from Python; the pattern simply isn't expressible in Sharpy.

## Allowed default values

| Type | Examples | Notes |
|------|----------|-------|
| Numeric literals | `42`, `3.14`, `0xFF`, `1_000_000` | Any numeric literal |
| String literals | `"hello"`, `'world'`, `r"path\to\file"` | Including raw strings |
| Boolean literals | `True`, `False` | |
| `None` | `None` | Only for nullable parameter types (`T \| None`) |
| `None()` | `None()` | Only for Optional parameter types (`T?`) |
| Enum values | `Color.RED`, `HttpMethod.GET` | |
| Constant references | `MAX_SIZE`, `DEFAULT_NAME` | Must reference a `const` declaration |
| Negated literals | `-1`, `-3.14` | |
| Conditional of constants | `1 if DEBUG else 0` | Both branches must be constants |

## Examples

```python
# ✅ Valid default parameters
def process(
    name: str = "default",
    count: int = 0,
    factor: float = 1.0,
    enabled: bool = True,
    mode: Mode = Mode.NORMAL,
    callback: Callable | None = None
) -> None:
    pass

# ✅ Using None for optional parameters (recommended pattern)
def search(query: str, limit: int | None = None, offset: int | None = None) -> list[Result]:
    actual_limit = limit ?? 100
    actual_offset = offset ?? 0
    # ...

# ✅ Referencing constants
const DEFAULT_TIMEOUT: float = 30.0
const DEFAULT_RETRIES: int = 3

def fetch(url: str, timeout: float = DEFAULT_TIMEOUT, retries: int = DEFAULT_RETRIES) -> Response:
    # ...

# ❌ Invalid: mutable default values
def broken(items: list[int] = []) -> int:              # ERROR: [] is not a compile-time constant
    return sum(items)

def also_broken(config: dict[str, str] = {}) -> None:  # ERROR: {} is not a compile-time constant
    pass

def still_broken(point: Point = Point(0, 0)) -> None:  # ERROR: constructor call is not constant
    pass

# ❌ Invalid: tagged union case constructors are not compile-time constants
def bad_opt(x: int? = Some(42)) -> None:     # ERROR SPY0401: use None() default, assign with ??=
    pass

def bad_result(r: int!str = Ok(1)) -> None:  # ERROR SPY0401
    pass

def bad_tuple(t: tuple[int, int] = (1, 2)) -> None:  # ERROR SPY0401
    pass
```

The pattern for `Some`/`Ok`/`Err` defaults is to declare the parameter with `None()` and assign
inside the body:

```python
def f(x: int? = None()) -> None:
    x ??= Some(42)   # assigns Some(42) only when x is None()
    print(x)

def main():
    f()         # 42
    f(Some(1))  # 1
```

*Implementation*
- *A future lowering (option C: forward-overload synthesis) may lift this restriction for
  `Some`/`Ok`/`Err` defaults; the `??=` pattern is the stable idiom.*

## Pattern for Optional Mutable Arguments

Declare the parameter as an optional (`T?`, see [Optional Type](tagged_unions_optional.md)) with
`None()` as its default, and build the mutable object in a **local** — the optional parameter is
the *input*, the local is the list you work with:

```python
def append_to(item: int, target: list[int]? = None()) -> list[int]:
    result: list[int] = []
    if target is not None:
        result = target
    result.append(item)
    return result

def main():
    print(append_to(1))              # [1]
    print(append_to(2))              # [2] — a separate list, not [1, 2]
    shared: list[int] = [0]
    print(append_to(3, Some(shared)))  # [0, 3]
    print(shared)                    # [0, 3] — the caller's list, mutated in place
```

Each call that omits `target` gets a fresh list, which is the point of the pattern; a caller that
passes one gets it mutated, exactly as in Python.

**Do not rebind the parameter itself.** `target = []` inside the function does not turn `target`
into a list: the name keeps its declared type `list[int]?` for the rest of the body, so the
following `target.append(item)` is SPY0229 (`Type 'list[int32]?' has no member 'append'`), and the
bare `[]` is not an `Optional` value either. Rebinding it as `target = Some(fresh)` does not help —
a store never re-narrows the name, so `len(target)` after it is SPY0326. Read the optional into a
local of the payload type instead, as above; the `is not None` branch narrows on the read.

Callers pass `Some(value)`, not a bare value, because `T?` is constructed only by `Some(…)`/`None()`
(see [Creating Optional Values](tagged_unions_optional.md#creating-optional-values)). Use
`target: list[int] | None = None` instead only when the parameter exists to talk to C# nullable
APIs — [Nullable Types](nullable_types.md) has that split.

*Implementation*
- *✅ Native - Direct mapping to C# optional parameters.*

## See Also

- [Function Parameters](function_parameters.md) - Overview of all parameter types
- [Function Variadic Arguments](function_variadic_arguments.md) - Variable-length argument lists (*args)
- [Function Definition](function_definition.md) - Basic function syntax
