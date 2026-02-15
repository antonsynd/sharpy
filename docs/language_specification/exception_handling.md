# Exception Handling

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
raise ValueError("invalid")                # Pythonic alias
raise System.ArgumentException("invalid")  # Direct .NET type
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

## `raise ... from ...` Not Supported

Unlike Python, Sharpy does not support `raise ... from ...` (exception chaining via the `from` clause). This Python feature relies on runtime exception mutation that does not map cleanly to .NET's immutable inner exception model.

To set an inner exception in Sharpy, pass it as a constructor argument:

```python
except IOError as e:
    raise ConfigError("Failed to load config", e)  # inner exception via constructor
```
