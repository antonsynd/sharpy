# os

Miscellaneous operating system interfaces.

```python
import os
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `st_size` | `long` | Size in bytes (0 for directories). |
| `st_mtime` | `float` | Time of last modification (Unix timestamp). |
| `st_ctime` | `float` | Time of creation (Unix timestamp). |
| `st_atime` | `float` | Time of last access (Unix timestamp). |
| `st_mode` | `int` | File mode / attributes. |

## Functions

### `os.remove(path: str)`

Remove a file (same as unlink).

### `os.rename(src: str, dst: str)`

Rename a file or directory.

### `os.mkdir(path: str)`

Create a directory.

### `os.makedirs(path: str, exist_ok: bool = False)`

Super-mkdir; create a leaf directory and all intermediate ones.

### `os.rmdir(path: str)`

Remove a directory.

### `os.listdir(path: str = ".") -> list[str]`

Return a list containing the names of the entries in the directory.

### `os.getcwd() -> str`

Return a string representing the current working directory.

### `os.chdir(path: str)`

Change the current working directory to the specified path.

### `os.getenv(key: str) -> str | None`

Get an environment variable, return None if it doesn't exist.

### `os.getenv(key: str, @default: str) -> str`

Get an environment variable, return default if it doesn't exist.

### `os.putenv(key: str, value: str)`

Change or add an environment variable.

### `os.path_exists(path: str) -> bool`

Test whether a path exists.

### `os.join(a: str, b: str) -> str`

Join two pathname components, inserting '/' as needed.

### `os.join(a: str, b: str, c: str) -> str`

Join three pathname components, inserting '/' as needed.

### `os.join(a: str, b: str, c: str, d: str) -> str`

Join four pathname components, inserting '/' as needed.

### `os.normpath(path: str) -> str`

Normalize a pathname, eliminating double slashes and resolving '.'/'..' references.

### `os.exists(path: str) -> bool`

Test whether a path exists.

### `os.isfile(path: str) -> bool`

Test whether a path is a regular file.

### `os.isdir(path: str) -> bool`

Return True if the pathname refers to an existing directory.

### `os.isabs(path: str) -> bool`

Test whether a path is absolute.

### `os.basename(path: str) -> str`

Return the final component of a pathname.

### `os.dirname(path: str) -> str`

Return the directory component of a pathname.

### `os.abspath(path: str) -> str`

Return an absolute path.

### `os.realpath(path: str) -> str`

Return the canonical path of the specified filename, eliminating any symbolic links.

### `os.getsize(path: str) -> long`

Return the size of a file, reported by os.stat().

### `os.expanduser(path: str) -> str`

Expand ~ and ~user constructions.
