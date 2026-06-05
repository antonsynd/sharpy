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

### `tempfile.write(data: str) -> int`

Write a string to the file, returning the number of characters written.

### `tempfile.read() -> str`

Read the entire contents of the file.

### `tempfile.close()`

Close the file, deleting it if delete is True.

### `tempfile.enter() -> NamedTemporaryFile`

### `tempfile.exit()`

### `tempfile.cleanup()`

Recursively delete the temporary directory and its contents.

### `tempfile.enter() -> str`

### `tempfile.exit()`

### `tempfile.rollover()`

Write the in-memory buffer to a real temporary file on disk.

### `tempfile.write(data: str) -> int`

Write a string to the spooled file, rolling over to disk if max_size is exceeded.

### `tempfile.read() -> str`

Read the entire contents of the spooled file.

### `tempfile.close()`

Close the spooled file, deleting any on-disk file.

### `tempfile.enter() -> SpooledTemporaryFile`

### `tempfile.exit()`
