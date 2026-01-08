# Function Variadic Arguments (`*args`)

Sharpy supports a limited form of variadic arguments using the `*args` syntax. Unlike Python's fully dynamic `*args`, Sharpy's variadic arguments are **homogeneously typed**: all arguments must be of the same type `T`.

## Syntax

```python
def function_name(*args: T) -> ReturnType:
    # args is an array[T] inside the function
    pass
```

## Examples

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

## Rules and Restrictions

### Homogeneous typing

All variadic arguments must be of the same declared type `T`:

```python
def process(*items: int) -> int:
    return sum(items)

process(1, 2, 3)              # OK: all ints
process(1, "two", 3)          # ERROR: "two" is str, not int
```

### Position requirement

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

### Only one `*args` per function

```python
# ❌ Invalid - multiple *args
def broken(*a: int, *b: str) -> None:  # ERROR
    pass
```

## Type of `*args` Inside the Function

Inside the function body, the `*args` parameter has type `array[T]`, mapping to C#'s `params T[]`:

```python
def analyze(*values: float) -> tuple[float, float]:
    # values: array[float]
    if len(values) == 0:
        return (0.0, 0.0)
    return (min(values), max(values))
```

## Unpacking Iterables with `*`

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

### Type checking for unpacking

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

## C# Interop

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

### Calling C# `params` methods from Sharpy

```python
from system import String

# String.Format has params signature: Format(string format, params object[] args)
result = String.format("Hello {0}, you have {1} messages", "Alice", 42)

# Or unpack from a collection
args = ["Bob", 10]
result = String.format("Hello {0}, you have {1} messages", *args)
```

### Calling Sharpy `*args` functions from C#

```csharp
// Individual arguments (compiler creates array)
var total = SumAll(1, 2, 3, 4, 5);

// Explicit array
var numbers = new int[] { 1, 2, 3, 4, 5 };
var total = SumAll(numbers);
```

## Function Type Compatibility

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

## See Also

- [Function Parameters](function_parameters.md) - Overview of all parameter types
- [Function Default Parameters](function_default_parameters.md) - Default parameter values
- [Spread Operator](spread_operator.md) - More about unpacking with `*`
- [Function Definition](function_definition.md) - Basic function syntax
