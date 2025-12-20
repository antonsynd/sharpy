# Exception Handling **[v0.1.0]**

## Exception Type Hierarchy

Sharpy uses .NET's exception hierarchy directly:

| Sharpy Name | .NET Type | Notes |
|-------------|-----------|-------|
| `Exception` | `System.Exception` | Base class for all exceptions |
| `ValueError` | `System.ArgumentException` | Invalid argument value |
| `TypeError` | `System.InvalidCastException` | Type mismatch |
| `IndexError` | `System.IndexOutOfRangeException` | Index out of bounds |
| `KeyError` | `System.Collections.Generic.KeyNotFoundException` | Dict key not found |
| `RuntimeError` | `System.InvalidOperationException` | General runtime error |
| `IOError` | `System.IO.IOException` | I/O operation failed |
| `FileNotFoundError` | `System.IO.FileNotFoundException` | File not found |
| `ZeroDivisionError` | `System.DivideByZeroException` | Division by zero |
| `NotImplementedError` | `System.NotImplementedException` | Not yet implemented |
| `StopIteration` | `System.InvalidOperationException` | Iterator exhausted |

## Pythonic Aliases

Sharpy provides Pythonic aliases for common .NET exceptions. These are imported automatically:

```python
# These are equivalent:
raise ValueError("invalid")              # Pythonic alias
raise System.ArgumentException("invalid") # Direct .NET type
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

# Raise with cause
raise RuntimeError("Failed") from original_error

# Suppress exception chaining with 'from None'
raise NewError("Clean error") from None  # Hides the original exception
```

## Exception Chaining Semantics

The `raise X from Y` syntax sets the chained exception, mapping to C#'s inner exception:

| Sharpy | C# |
|--------|----|
| `raise NewError("msg") from original` | `throw new NewError("msg", original)` |
| `raise NewError("msg") from None` | `throw new NewError("msg", null)` |
| `raise NewError("msg")` (in except block) | Automatic chaining via `Exception.InnerException` |

## Accessing the Chained Exception

- In C# code: `.InnerException` property
- In Sharpy code: `.__cause__` attribute (maps to `.InnerException`)

```python
try:
    do_risky_operation()
except LowLevelError as e:
    raise HighLevelError("Operation failed") from e

# Later, when catching:
try:
    call_high_level()
except HighLevelError as e:
    print(f"Error: {e}")
    if e.__cause__ is not None:
        print(f"Caused by: {e.__cause__}")
```

## `from original_error` Context

The `from` clause can reference any in-scope exception variable, not just in `except` blocks:

```python
# In except block (common case)
except IOError as e:
    raise ConfigError("Failed to load config") from e

# Referencing stored exception
saved_error: Exception? = None
try:
    do_something()
except Exception as e:
    saved_error = e

if saved_error is not None:
    raise ProcessingError("Deferred error") from saved_error
```

## `raise ... from None`

Using `from None` suppresses the automatic exception chaining, hiding the original exception from tracebacks. This is useful when:
- The original exception is an implementation detail
- You want a cleaner error message for users
- Re-raising with a different exception type for API boundaries

```python
try:
    # Low-level operation
    result = parse_internal_format(data)
except InternalParseError as e:
    # Hide internal error, present clean API error
    raise ValueError("Invalid data format") from None
```

*Implementation:*
- *raise: ✅ Native - `throw new Exception()`*
- *bare raise: ✅ Native - `throw;`*
- *raise from: 🔄 Lowered - Inner exception constructor*
