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
| `None` | `None` | Only for nullable parameter types |
| Enum values | `Color.RED`, `HttpMethod.GET` | |
| Constant references | `MAX_SIZE`, `DEFAULT_NAME` | Must reference a `const` declaration |

## Examples

```python
# ✅ Valid default parameters
def process(
    name: str = "default",
    count: int = 0,
    factor: float = 1.0,
    enabled: bool = True,
    mode: Mode = Mode.NORMAL,
    callback: Callable? = None
) -> None:
    pass

# ✅ Using None for optional parameters (recommended pattern)
def search(query: str, limit: int? = None, offset: int? = None) -> list[Result]:
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
```

## Pattern for Optional Mutable Arguments

Use `None` as the default and create the mutable object inside the function:

```python
def append_to(item: int, target: list[int]? = None) -> list[int]:
    if target is None:
        target = []
    target.append(item)
    return target

# Each call gets a fresh list
list1 = append_to(1)  # [1]
list2 = append_to(2)  # [2] - separate list, not [1, 2]
```

*Implementation*
- *✅ Native - Direct mapping to C# optional parameters.*

## See Also

- [Function Parameters](function_parameters.md) - Overview of all parameter types
- [Function Variadic Arguments](function_variadic_arguments.md) - Variable-length argument lists (*args)
- [Function Definition](function_definition.md) - Basic function syntax
