# tempfile

Generate temporary files and directories.

```python
import tempfile
```

## Functions

### `tempfile.gettempdir() -> str`

Return the name of the directory used for temporary files.

### `tempfile.gettempprefix() -> str`

Return the filename prefix used to create temporary files.

### `tempfile.mkdtemp(prefix: str = "tmp") -> str`

Create and return a unique temporary directory.

### `tempfile.mkstemp(prefix: str = "tmp", suffix: str = "") -> tuple[int, str]`

Create and return a unique temporary file.

## NamedTemporaryFile

A temporary file with a visible name, deleted on close by default.

### `write(data: str) -> int`

Write a string to the file, returning the number of characters written.

### `read() -> str`

Read the entire contents of the file.

### `close()`

Close the file, deleting it if delete is True.

### `enter() -> NamedTemporaryFile`

### `exit()`

## TemporaryDirectory

A temporary directory, recursively deleted on cleanup or context exit.

### `cleanup()`

Recursively delete the temporary directory and its contents.

### `enter() -> str`

### `exit()`

## SpooledTemporaryFile

A temporary file kept in memory until it exceeds max_size, then written to disk.

### `rollover()`

Write the in-memory buffer to a real temporary file on disk.

### `write(data: str) -> int`

Write a string to the spooled file, rolling over to disk if max_size is exceeded.

### `read() -> str`

Read the entire contents of the spooled file.

### `close()`

Close the spooled file, deleting any on-disk file.

### `enter() -> SpooledTemporaryFile`

### `exit()`
