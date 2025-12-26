# Result Type

The `Result[T, E]` type is a special tagged union provided by the Sharpy standard library for representing operations that can either succeed with a value of type `T` or fail with an error of type `E`. This is similar to Rust's `Result` type.

## Definition

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)
```

The `Result` type is part of the standard library and provides special syntax and operators for ergonomic error handling.

## Creating Result Values

```python
# Success case
success: Result[int, str] = Result.Ok(42)

# Error case
failure: Result[int, str] = Result.Err("Something went wrong")
```

## Pattern Matching

Use pattern matching to handle both success and error cases:

```python
def divide(a: double, b: double) -> Result[double, str]:
    if b == 0:
        return Result.Err("Division by zero")
    return Result.Ok(a / b)

result = divide(10, 2)
match result:
    case Result.Ok(value):
        print(f"Success: {value}")
    case Result.Err(error):
        print(f"Error: {error}")
```

## Common Methods

The `Result` type provides several useful methods:

```python
enum Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

    def is_ok(self) -> bool:
        """Returns True if the result is Ok"""
        match self:
            case Result.Ok():
                return True
            case Result.Err():
                return False

    def is_err(self) -> bool:
        """Returns True if the result is Err"""
        return not self.is_ok()

    def unwrap(self) -> T:
        """Returns the Ok value or raises an exception"""
        match self:
            case Result.Ok(value):
                return value
            case Result.Err(error):
                raise Exception(f"Called unwrap on Err: {error}")

    def unwrap_or(self, default: T) -> T:
        """Returns the Ok value or the default"""
        match self:
            case Result.Ok(value):
                return value
            case Result.Err():
                return default

    def unwrap_or_else(self, f: (E) -> T) -> T:
        """Returns the Ok value or calls f with the error"""
        match self:
            case Result.Ok(value):
                return value
            case Result.Err(error):
                return f(error)
```

## Try Expressions

Result types have special integration with try expressions for ergonomic error handling:

```python
def process_data() -> Result[str, str]:
    # If any operation returns Err, the function returns early
    data = try? fetch_data()        # Returns Err early if fetch fails
    validated = try? validate(data) # Returns Err early if validation fails
    return Result.Ok(validated)
```

See [Try Expressions](try_expressions.md) for more details on this special syntax.

## Comparison with Nullable Types

| Feature | `Result[T, E]` | `T?` (Nullable) |
|---------|----------------|-----------------|
| Success/Some case | `Result.Ok(value)` | `value` |
| Failure/None case | `Result.Err(error)` | `None` |
| Error information | ✅ Typed error `E` | ❌ No error info |
| Pattern matching | `case Result.Ok(v):` | `if x is not None:` |
| Use case | Operations that can fail with details | Optional values without error details |

## Examples

### File Operations

```python
def read_config(path: str) -> Result[Config, str]:
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
def process_user_input(input: str) -> Result[int, str]:
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
- *🔄 Lowered - Abstract base class + sealed nested case classes (see [Tagged Unions](tagged_unions.md) for implementation details)*

## See Also

- [Tagged Unions](tagged_unions.md) - General tagged union syntax and implementation
- [Optional Type](tagged_unions_optional.md) - The Optional type for representing optional values
- [Try Expressions](try_expressions.md) - Special syntax for Result types
- [Exception Handling](exception_handling.md) - Traditional exception-based error handling
- [Pattern Matching](match_statement.md) - Pattern matching syntax
