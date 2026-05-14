# Exception Handling

## Exception Type Hierarchy

Sharpy uses .NET's exception hierarchy directly:

| Sharpy Name | Base Type | Notes |
|-------------|-----------|-------|
| `Exception` | `System.Exception` | Base class for all exceptions |
| `ValueError` | `Exception` | Invalid argument value |
| `TypeError` | `Exception` | Type mismatch |
| `RuntimeError` | `Exception` | General runtime error |
| `NotImplementedError` | `Exception` | Not yet implemented |
| `AttributeError` | `Exception` | Attribute not found |
| `ZeroDivisionError` | `Exception` | Division by zero |
| `OverflowError` | `Exception` | Numeric overflow |
| `LookupError` | `Exception` | Base for key/index errors |
| `IndexError` | `Exception` | Index out of bounds |
| `KeyError` | `Exception` | Dict key not found |
| `IOError` | `IOException` | I/O operation failed |
| `OSError` | `IOError` | OS-level error (alias for IOError) |
| `FileNotFoundError` | `FileNotFoundException` | File not found |
| `FileExistsError` | `IOException` | File already exists |
| `IsADirectoryError` | `IOException` | Expected file, got directory |
| `PermissionError` | `UnauthorizedAccessException` | Permission denied |
| `StopIteration` | `Exception` | Iterator exhausted |
| `UnicodeEncodeError` | `Exception` | Unicode encoding failed |
| `ArgumentError` | `Exception` | Generic argument error |
| `SystemExit` | `Exception` | Program exit request |
| `JSONDecodeError` | `ValueError` | Invalid JSON (in `json` module) |
| `StatisticsError` | `Exception` | Statistics computation error (in `statistics` module) |

## Sharpy Exception Classes

Sharpy provides its own exception classes that mirror Python's exception names. These are distinct .NET types defined in `Sharpy.Core` that inherit from `System.Exception` — they are **not** aliases for existing .NET exception types.

```python
# ValueError is Sharpy.ValueError : System.Exception
raise ValueError("invalid")

# This is NOT the same as System.ArgumentException
# Sharpy exceptions are their own types in the Sharpy namespace
```

## No `BaseException`

Unlike Python which distinguishes `BaseException` from `Exception`, Sharpy follows .NET where `System.Exception` is the base for all exceptions. There is no separate hierarchy for system-level exceptions that shouldn't normally be caught.

## Try/Except/Finally

```python
try:
    result = risky_operation()
except ValueError as e:
    print(f"Invalid value: {e}")
except Exception as e:
    print(f"Error: {e}")
else:
    # Executed if no exception
    print(f"Success: {result}")
finally:
    # Always executed
    cleanup()
```

*Implementation:*
- *try/except/finally: ✅ Native - `try`/`catch`/`finally`*
- *else clause: 🔄 Lowered - Boolean flag pattern*

## Raise Statement

```python
# Raise exception
raise ValueError("Invalid input")

# Re-raise current exception
except Exception as e:
    log_error(e)
    raise
```

*Implementation:*
- *raise: ✅ Native - `throw new Exception()`*
- *bare raise: ✅ Native - `throw;`*

## Exception Filters (`when`)

Exception filters allow catch handlers to conditionally match exceptions based on a boolean expression, mapping directly to C#'s `catch ... when` syntax.

```python
try:
    raise ValueError("specific error")
except ValueError as e when e.message == "specific error":
    print("caught specific")
except ValueError:
    print("caught generic")
```

The `when` keyword is a **soft keyword** — it is only special after an `except` clause. It can be used as a variable name elsewhere without conflict.

### Syntax

```
except Type as name when condition:
except Type when condition:
except when condition:          # bare except with filter
```

### Rules

- The filter expression must evaluate to `bool`
- The filter is evaluated before entering the handler body; if it returns `False`, the exception propagates to the next handler
- Exception filters do not unwind the stack — the exception object remains valid during filter evaluation
- `except*` handlers do not support `when` filters

### C# Emission

```csharp
catch (ValueError e) when (e.Message == "specific error")
{
    // handler body
}
```

*Implementation: ✅ Native — `catch ... when (expr)`*

## Exception Groups and `except*`

Sharpy supports structured exception handling for multiple concurrent errors via `ExceptionGroup` and the `except*` syntax, inspired by Python PEP 654.

### ExceptionGroup

An `ExceptionGroup` bundles multiple exceptions into a single throwable:

```python
errors: list[Exception] = [ValueError("bad value"), TypeError("bad type")]
raise ExceptionGroup("multiple errors", errors)
```

### `except*` Syntax

Use `except*` to catch specific exception types from within an `ExceptionGroup`. Each `except*` handler receives the matching subset:

```python
errors: list[Exception] = [ValueError("bad value")]
try:
    raise ExceptionGroup("errors", errors)
except* ValueError as eg:
    print("caught ValueError group")
```

Multiple `except*` handlers can catch different types from the same group:

```python
errors: list[Exception] = [ValueError("bad value"), TypeError("bad type")]
try:
    raise ExceptionGroup("errors", errors)
except* ValueError as eg:
    print("caught ValueError group")
except* TypeError as eg:
    print("caught TypeError group")
```

### Rules

- **`except*` requires an exception type** — bare `except*:` is not allowed (unlike bare `except:`)
- **Cannot mix `except` and `except*`** — a `try` block must use either regular `except` handlers or `except*` handlers, not both

```python
# ERROR: bare except* not allowed
try:
    ...
except*:          # 'except*' requires an exception type
    ...

# ERROR: cannot mix forms
try:
    ...
except ValueError:     # regular except
    ...
except* TypeError:     # Cannot mix 'except' and 'except*'
    ...
```

*Implementation*
- *✅ Implemented — `ExceptHandler.IsExceptStar` property in AST*
- *Parser validates: bare `except*` rejected, mixing `except`/`except*` rejected*

## `raise ... from ...` Not Supported

Unlike Python, Sharpy does not support `raise ... from ...` (exception chaining via the `from` clause). This Python feature relies on runtime exception mutation that does not map cleanly to .NET's immutable inner exception model.

To set an inner exception in Sharpy, pass it as a constructor argument:

```python
except IOError as e:
    raise ConfigError("Failed to load config", e)  # inner exception via constructor
```
