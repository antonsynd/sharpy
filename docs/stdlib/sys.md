# sys

Provides access to system-specific parameters and functions, similar to Python's sys module.

```python
import sys
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `stddev` | `uint` | File descriptor for standard input (0). |
| `stdout` | `uint` | File descriptor for standard output (1). |
| `stderr` | `uint` | File descriptor for standard error (2). |

## Properties

| Name | Type | Description |
|------|------|-------------|
| `platform` | `str` | This string contains a platform identifier. |
| `stdin` | `TextReader` | The standard input stream. |
| `maxsize` | `int` | An integer giving the maximum value a variable of type int can take. Note: Sharpy's int is 32-bit (max 2,147,483,647), unlike Python's sys.maxsize which is 2^63-1 on 64-bit. Use long for larger values. |

## Functions

### `sys.exit(code: int = 0)`

Exit the program with the given status code.

**Parameters:**

- `code` (int) -- The exit code (default is 0).

```python
sys.exit()     # exit with code 0
sys.exit(1)    # exit with code 1
```

### `sys.getsizeof(obj: object?) -> int`

Return the size of an object in bytes. Best-effort estimate.
Returns -1 if the size cannot be determined.
