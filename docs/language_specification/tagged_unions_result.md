# Result Type

The `Result[T, E]` type is a special tagged union provided by the Sharpy standard library for representing operations that can either succeed with a value of type `T` or fail with an error of type `E`. This is similar to Rust's `Result` type.

`Result[T, E]` is a **struct** — no heap allocation for returning result values.

## Definition

```python
union Result[T, E]:
    case Ok(value: T)
    case Err(error: E)
```

The `Result` type is part of the standard library and provides special syntax and operators for ergonomic error handling.

## Shorthand Syntax

The `T !E` syntax is sugar for `Result[T, E]` in **return type annotations**:

```python
# Shorthand (in return type annotations)
def parse(s: str) -> int !ValueError:
    ...

# Equivalent explicit form
def parse(s: str) -> Result[int, ValueError]:
    ...
```

**`!E` is recommended for top-level return types only.** For nested or complex cases, use the explicit `Result[T, E]` form:

```python
# ✅ Good - shorthand for simple return types
def read_file(path: str) -> str !IOError:
    ...

# ✅ Good - explicit form for nested/complex types
def batch_parse(items: list[str]) -> list[Result[int, ValueError]]:
    ...

# ❌ Avoid - shorthand in non-return-type positions
cache: dict[str, int !ValueError] = {}  # Use Result[int, ValueError] instead
```

### Precedence

`!E` binds tighter than `| None`:

```python
# !E binds tighter than | None
int !ValueError | None  →  (int !ValueError) | None  →  Result[int, ValueError] | None
```

This means a function can return an optional result:

```python
def try_parse(s: str) -> int !ValueError | None:
    # Returns Result[int, ValueError] | None
    # None means "no input", Err means "bad input", Ok means "success"
    ...
```

## Creating Result Values

```python
# Success case
success: Result[int, str] = Ok(42)

# Error case
failure: Result[int, str] = Err("Something went wrong")
```

## Constructor Shorthand

When the expected type is known, you can use `Ok(value)` and `Err(error)`
without qualifying with the type name:

```python
# With type annotation - shorthand works
x: int !str = Ok(42)
y: int !str = Err("failed")

# Function return - shorthand works
def parse(s: str) -> int !str:
    if not s:
        return Err("empty string")
    return Ok(42)

# Without type context - error (type cannot be inferred)
x = Ok(42)    # Error: Cannot infer type for 'Ok()'
x = Err("e")  # Error: Cannot infer type for 'Err()'
```

The compiler infers the full type from context. The shorthand is equivalent to
calling `Result<T, E>.Ok(value)` or `Result<T, E>.Err(error)`.

## Pattern Matching

Use pattern matching to handle both success and error cases:

```python
def divide(a: float, b: float) -> float !str:
    if b == 0:
        return Err("Division by zero")
    return Ok(a / b)

result = divide(10, 2)
match result:
    case Ok(value):
        print(f"Success: {value}")
    case Err(error):
        print(f"Error: {error}")
```

## Common Methods

The `Result` type provides several useful methods:

```python
union Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

    def is_ok(self) -> bool:
        """Returns True if the result is Ok"""
        match self:
            case Ok(_):
                return True
            case Err(_):
                return False

    def is_err(self) -> bool:
        """Returns True if the result is Err"""
        return not self.is_ok()

    def unwrap(self) -> T:
        """Returns the Ok value or raises an exception"""
        match self:
            case Ok(value):
                return value
            case Err(error):
                raise Exception(f"Called unwrap on Err: {error}")

    def unwrap_or(self, default: T) -> T:
        """Returns the Ok value or the default"""
        match self:
            case Ok(value):
                return value
            case Err(_):
                return default

    def unwrap_or_else(self, f: (E) -> T) -> T:
        """Returns the Ok value or calls f with the error"""
        match self:
            case Ok(value):
                return value
            case Err(error):
                return f(error)
```

## Stdlib Conventions

The standard library follows these conventions for when to use `Result` vs exceptions:

| Category | Style | Example |
|----------|-------|---------|
| Parsing/conversion | `Result` | `int.parse(s: str) -> int !ValueError` |
| File/network open | `Result` | `open(path: str) -> File !IOError` |
| Collection "get" | `Optional` | `dict.get(key: K) -> V?` |
| Collection index | Exception | `list[i]` throws `IndexError` |
| Type casting | `Result` | `obj to Dog` returns `Result` |

**Guiding principle:** Exceptions are for bugs. Results are for expected failures.

## Comparison with `T?` (Optional)

| Feature | `Result[T, E]` / `T !E` | `T?` / `Optional[T]` |
|---------|--------------------------|----------------------|
| Success case | `Ok(value)` | `Some(value)` |
| Failure case | `Err(error)` | `Nothing` |
| Error information | ✅ Typed error `E` | ❌ No error info |
| Pattern matching | `case Ok(v):` | `case Some(v):` |
| Use case | Operations that can fail with details | Optional values without error details |
| Heap allocation | **No** (struct) | **No** (struct) |

## Examples

### File Operations

```python
def read_config(path: str) -> Config !str:
    if not file_exists(path):
        return Err(f"File not found: {path}")

    try:
        content = read_file(path)
        config = parse_config(content)
        return Ok(config)
    except Exception as e:
        return Err(f"Failed to read config: {e}")

# Using the result
result = read_config("config.yaml")
match result:
    case Ok(config):
        app.configure(config)
    case Err(error):
        print(f"Configuration error: {error}")
```

### Chaining Operations

```python
def process_user_input(input: str) -> int !str:
    # Validate input
    if not input:
        return Err("Input cannot be empty")

    # Parse to number
    try:
        value = int(input)
    except:
        return Err("Input must be a number")

    # Validate range
    if value < 0 or value > 100:
        return Err("Value must be between 0 and 100")

    return Ok(value)
```

*Implementation*
- *🔄 Lowered - Struct-based tagged union (no heap allocation). See [Tagged Unions](tagged_unions.md) for implementation details.*

## Implementation Details

`Result[T, E]` is implemented as a C# `readonly struct` in `Sharpy.Core`:

```csharp
public readonly struct Result<T, E>
{
    // Three fields: the value, the error, and an isOk flag
    // Zero heap allocation
}
```

The static helpers `Ok(value)` and `Err(error)` are available at module scope
for convenient construction.

## See Also

- [Tagged Unions](tagged_unions.md) - General tagged union syntax and implementation
- [Optional Type](tagged_unions_optional.md) - The `T?` / `Optional[T]` type for optional values
- [Try Expressions](try_expressions.md) - Special syntax for wrapping exceptions in Result
- [Exception Handling](exception_handling.md) - Traditional exception-based error handling
- [Pattern Matching](match_statement.md) - Pattern matching syntax
