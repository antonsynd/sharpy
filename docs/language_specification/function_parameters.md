# Function Parameters

## Default Parameters

Functions can specify default values for parameters. Parameters with defaults must come after required parameters.

```python
def greet(name: str, greeting: str = "Hello") -> str:
    return f"{greeting}, {name}!"

def connect(host: str, port: int = 8080, timeout: double = 30.0) -> Connection:
    # ...
```

### Compile-Time Constant Requirement

Default parameter values must be compile-time constants, matching C# semantics. This eliminates the "mutable default argument" pitfall from Python; the pattern simply isn't expressible in Sharpy.

### Allowed default values

| Type | Examples | Notes |
|------|----------|-------|
| Numeric literals | `42`, `3.14`, `0xFF`, `1_000_000` | Any numeric literal |
| String literals | `"hello"`, `'world'`, `r"path\to\file"` | Including raw strings |
| Boolean literals | `True`, `False` | |
| `None` | `None` | Only for nullable parameter types |
| Enum values | `Color.RED`, `HttpMethod.GET` | |
| Constant references | `MAX_SIZE`, `DEFAULT_NAME` | Must reference a `const` declaration |

### Examples

```python
# ✅ Valid default parameters
def process(
    name: str = "default",
    count: int = 0,
    factor: double = 1.0,
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
const DEFAULT_TIMEOUT: double = 30.0
const DEFAULT_RETRIES: int = 3

def fetch(url: str, timeout: double = DEFAULT_TIMEOUT, retries: int = DEFAULT_RETRIES) -> Response:
    # ...

# ❌ Invalid: mutable default values
def broken(items: list[int] = []) -> int:              # ERROR: [] is not a compile-time constant
    return sum(items)

def also_broken(config: dict[str, str] = {}) -> None:  # ERROR: {} is not a compile-time constant
    pass

def still_broken(point: Point = Point(0, 0)) -> None:  # ERROR: constructor call is not constant
    pass
```

### Pattern for Optional Mutable Arguments

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

## Named (Keyword) Arguments

Sharpy supports calling functions with named arguments, allowing callers to specify parameter values by name rather than position:

```python
def create_user(name: str, age: int, active: bool = True) -> User:
    pass

# Positional arguments
user1 = create_user("Alice", 30, False)

# Named arguments
user2 = create_user(name="Bob", age=25)
user3 = create_user(age=25, name="Bob")  # Order doesn't matter for named args

# Mixed: positional first, then named
user4 = create_user("Charlie", age=35, active=False)

# ❌ Invalid: named before positional
user5 = create_user(name="Dave", 40)  # ERROR: positional argument follows keyword argument
```

### Named Argument Rules

- Named arguments must follow all positional arguments
- Once a named argument is used, all subsequent arguments must be named
- A parameter cannot be specified both positionally and by name

*Implementation*
- *✅ Native - Direct mapping to C# named arguments.*

## Variadic Arguments (`*args`)

Sharpy supports a limited form of variadic arguments using the `*args` syntax. Unlike Python's fully dynamic `*args`, Sharpy's variadic arguments are **homogeneously typed**: all arguments must be of the same type `T`.

### Syntax

```python
def function_name(*args: T) -> ReturnType:
    # args is an array[T] inside the function
    pass
```

### Examples

```python
# Sum any number of integers
def sum_all(*numbers: int) -> int:
    result = 0
    for n in numbers:
        result += n
    return result

# Call with any number of arguments
total = sum_all(1, 2, 3)           # 6
total = sum_all(1, 2, 3, 4, 5)     # 15
total = sum_all()                   # 0 (empty tuple)

# Print multiple messages
def log_all(*messages: str) -> None:
    for msg in messages:
        print(msg)

log_all("Starting", "Processing", "Done")
```

### Rules and Restrictions

**Homogeneous typing:**

All variadic arguments must be of the same declared type `T`:

```python
def process(*items: int) -> int:
    return sum(items)

process(1, 2, 3)              # OK: all ints
process(1, "two", 3)          # ERROR: "two" is str, not int
```

**Position requirement:**

The `*args` parameter must be the last parameter in the function signature:

```python
# ✅ Valid - *args at the end
def greet(prefix: str, *names: str) -> None:
    for name in names:
        print(f"{prefix} {name}")

greet("Hello", "Alice", "Bob", "Charlie")

# ❌ Invalid - *args not at the end
def broken(*items: int, suffix: str) -> None:  # ERROR
    pass
```

**Only one `*args` per function:**

```python
# ❌ Invalid - multiple *args
def broken(*a: int, *b: str) -> None:  # ERROR
    pass
```

### Type of `*args` Inside the Function

Inside the function body, the `*args` parameter has type `array[T]`, mapping to C#'s `params T[]`:

```python
def analyze(*values: double) -> tuple[double, double]:
    # values: array[double]
    if len(values) == 0:
        return (0.0, 0.0)
    return (min(values), max(values))
```

### Unpacking Iterables with `*`

When calling a function with `*args`, you can unpack an iterable using the `*` operator:

```python
def sum_all(*numbers: int) -> int:
    result = 0
    for n in numbers:
        result += n
    return result

# Direct arguments
sum_all(1, 2, 3)              # 6

# Unpack a list
nums = [1, 2, 3, 4, 5]
sum_all(*nums)                # 15

# Unpack a homogenously-typed tuple
t = (10, 20, 30)
sum_all(*t)                   # OK: 60

# Mixed: direct args and unpacking
sum_all(1, 2, *[3, 4], 5)     # 15
```

**Type checking for unpacking:**

The unpacked iterable must contain elements of the correct type:

```python
def process(*items: int) -> int:
    return sum(items)

int_list: list[int] = [1, 2, 3]
str_list: list[str] = ["a", "b", "c"]
int_tuple = (10, 20, 30)
mixed_tuple = (10, "str", 30)

process(*int_list)            # OK
process(*str_list)            # ERROR: list[str] cannot unpack to *args: int
process(*int_tuple)           # OK
process(*mixed_tuple)         # ERROR: tuple[int, str, int] cannot unpack to *args: int
```

### C# Interop

Sharpy's `*args` maps directly to C#'s `params` arrays:

**Sharpy:**
```python
def format_message(template: str, *args: object) -> str:
    return template.format(*args)
```

**Generated C#:**
```csharp
public static string FormatMessage(string template, params object[] args) {
    return string.Format(template, args);
}
```

**Calling C# `params` methods from Sharpy:**

```python
from system import String

# String.Format has params signature: Format(string format, params object[] args)
result = String.format("Hello {0}, you have {1} messages", "Alice", 42)

# Or unpack from a collection
args = ["Bob", 10]
result = String.format("Hello {0}, you have {1} messages", *args)
```

**Calling Sharpy `*args` functions from C#:**

```csharp
// Individual arguments (compiler creates array)
var total = SumAll(1, 2, 3, 4, 5);

// Explicit array
var numbers = new int[] { 1, 2, 3, 4, 5 };
var total = SumAll(numbers);
```

### Function Type Compatibility

Function types cannot express variadic parameters. When you need a function type for a variadic function, use the non-variadic equivalent:

```python
def sum_all(*numbers: int) -> int:
    return sum(numbers)

# Cannot directly use sum_all as (int, int, int) -> int
# Instead, wrap it:
fixed_sum: (int, int, int) -> int = lambda a, b, c: sum_all(a, b, c)
```

*Implementation*
- *✅ Native - Maps to C# `params T[]` arrays.*

## No `**kwargs` Support

Sharpy does not support `**kwargs` (variadic keyword arguments), as .NET has no direct equivalent.

**Alternatives:**

```python
# Instead of: def configure(**kwargs) -> None

# Option 1: Named parameters with defaults
def configure(host: str = "localhost", port: int = 8080, debug: bool = False) -> None:
    pass

# Option 2: Typed configuration class
class Config:
    host: str = "localhost"
    port: int = 8080
    debug: bool = False

def configure(config: Config) -> None:
    pass

# Option 3: Dictionary parameter (loses type safety on values)
def configure(options: dict[str, object]) -> None:
    host = options.get("host") ?? "localhost"
    port = options.get("port") to int? ?? 8080
    # ...
```

## Positional-Only and Keyword-Only Parameters

Sharpy does not support Python's positional-only (`/`) or keyword-only (`*`) parameter markers. All parameters can be passed either positionally or by name.

```python
def process(value: int) -> str:
    return f"Integer: {value}"

def process(value: str) -> str:
    return f"String: {value}"

def process(value: int, multiplier: int) -> str:
    return f"Result: {value * multiplier}"
```

**Rules:**
- Overloads resolved by parameter count and types
- Parameter names do not affect resolution

*Implementation*
- *✅ Native - C# supports method overloading.*
