# tempfile

Temporary file and directory creation, similar to Python's tempfile module.

```python
import tempfile
```

## Functions

### `tempfile.gettempdir() -> str`

Return the name of the directory used for temporary files.
Similar to Python's tempfile.gettempdir().

**Returns:** The path to the system temporary directory, without a trailing separator.

```python
tempfile.gettempdir()    # "/tmp" on Unix, "C:\Users\...\Temp" on Windows
```

### `tempfile.mkdtemp(prefix: str = "tmp") -> str`

Create a temporary directory and return its absolute pathname.
Similar to Python's tempfile.mkdtemp().

**Parameters:**

- `prefix` (str) -- Prefix for the directory name. Defaults to "tmp".

**Returns:** The absolute path of the created temporary directory.

```python
tempfile.mkdtemp()           # "/tmp/tmpabcdefgh"
tempfile.mkdtemp("myapp_")   # "/tmp/myapp_abcdefgh"
```

**Raises:**

- `OSError` -- Thrown if the directory could not be created.
