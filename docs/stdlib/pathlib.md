# pathlib

Object-oriented filesystem path, similar to Python's pathlib.Path.
Immutable — all mutation methods return new Path instances.

```python
import pathlib
```

## Path

Object-oriented filesystem path, similar to Python's pathlib.Path.
Immutable — all mutation methods return new Path instances.

### `exists() -> bool`

Whether the path exists on the filesystem.

**Returns:** `true` if the path exists.

```python
Path("/tmp").exists()        # True
Path("/no/such").exists()    # False
```

### `is_file() -> bool`

Whether the path points to a regular file.

### `is_dir() -> bool`

Whether the path points to a directory.

### `read_text(encoding: str = "utf-8") -> str`

Read the file as text.

**Parameters:**

- `encoding` (str) -- Text encoding (default: "utf-8").

**Returns:** The file contents as a string.

```python
p = Path("hello.txt")
text = p.read_text()    # "Hello, world!"
```

### `write_text(data: str, encoding: str = "utf-8")`

Write text to the file.

### `read_bytes() -> list[byte]`

Read the file as bytes.

### `write_bytes(data: list[byte])`

Write bytes to the file.

### `mkdir(parents: bool = false, exist_ok: bool = false)`

Create the directory. Optionally create parents.

### `rmdir()`

Remove the directory (must be empty).

### `iterdir() -> Iterable[Path]`

Iterate over the directory entries.

### `glob(pattern: str) -> Iterable[Path]`

Glob for matching paths relative to this directory.

### `rename(target: str) -> Path`

Rename the file or directory.

### `unlink(missing_ok: bool = false)`

Remove the file.

### `replace(target: str) -> Path`

Rename, replacing the target if it exists.

### `resolve() -> Path`

Make the path absolute, resolving any symlinks.

### `with_name(name: str) -> Path`

Return a new path with the name changed.

### `with_stem(stem: str) -> Path`

Return a new path with the stem changed.

### `with_suffix(suffix: str) -> Path`

Return a new path with the suffix changed.

### `relative_to(other: str) -> Path`

Return a relative path from this path to other.
