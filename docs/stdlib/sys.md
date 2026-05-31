# sys

System-specific parameters and functions.

```python
import sys
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `platform` | `str` | This string contains a platform identifier. |
| `stdin` | `TextReader` | The standard input stream. |
| `maxsize` | `int` | An integer giving the maximum value a variable of type int can take. Note: Sharpy's int is 32-bit (max 2,147,483,647), unlike Python's sys.maxsize which is 2^63-1 on 64-bit. Use long for larger values. |
| `stdout` | `TextWriter` | The standard output stream. |
| `stderr` | `TextWriter` | The standard error stream. |

## Functions

### `sys.exit(code: int = 0)`

Exit the program with the given status code.

**Parameters:**

- `code` (int) -- The exit code (default is 0).

```python
sys.exit()     # exit with code 0
sys.exit(1)    # exit with code 1
```

### `sys.getsizeof(obj: object | None) -> int`

Return the size of an object in bytes. Best-effort estimate.
Returns -1 if the size cannot be determined.
