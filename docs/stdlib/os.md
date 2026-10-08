# os

Miscellaneous operating system interfaces.

```python
import os
```

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

### `os.getenv(key: str, default: str) -> str`

Get an environment variable, return default if it doesn't exist.

### `os.putenv(key: str, value: str)`

Change or add an environment variable.

### `os.get_environ() -> dict[str, str]`

Return a mapping object representing the string environment.

### `os.path_exists(path: str) -> bool`

Test whether a path exists.

### `os.stat(path: str) -> StatResult`

Perform the equivalent of a stat() system call on the given path.

### `os.walk(top: str) -> Iterator[tuple[str, list[str], list[str]]]`

Directory tree generator yielding (dirpath, dirnames, filenames) for each directory in the tree rooted at top.

## os.path

Common pathname manipulations (os.path equivalent).

```python
import os.path
```

### `os.path.join(a: str, b: str) -> str`

Join two pathname components, inserting '/' as needed.

### `os.path.join(a: str, b: str, c: str) -> str`

Join three pathname components, inserting '/' as needed.

### `os.path.join(a: str, b: str, c: str, d: str) -> str`

Join four pathname components, inserting '/' as needed.

### `os.path.normpath(path: str) -> str`

Normalize a pathname, eliminating double slashes and resolving '.'/'..' references.

### `os.path.exists(path: str) -> bool`

Test whether a path exists.

### `os.path.isfile(path: str) -> bool`

Test whether a path is a regular file.

### `os.path.isdir(path: str) -> bool`

Return True if the pathname refers to an existing directory.

### `os.path.isabs(path: str) -> bool`

Test whether a path is absolute.

### `os.path.basename(path: str) -> str`

Return the final component of a pathname.

### `os.path.dirname(path: str) -> str`

Return the directory component of a pathname.

### `os.path.split(path: str) -> tuple[str, str]`

Split a pathname. Return tuple (head, tail) where tail is everything after the final slash.

### `os.path.splitext(path: str) -> tuple[str, str]`

Split the extension from a pathname.

### `os.path.abspath(path: str) -> str`

Return an absolute path.

### `os.path.realpath(path: str) -> str`

Return the canonical path of the specified filename, eliminating any symbolic links.

### `os.path.getsize(path: str) -> int64`

Return the size of a file, reported by os.stat().

### `os.path.expanduser(path: str) -> str`

Expand ~ and ~user constructions.

## StatResult

Result of os.stat(), similar to Python's os.stat_result.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `st_size` | `int64` |  |
| `st_mtime` | `float` |  |
| `st_ctime` | `float` |  |
| `st_atime` | `float` |  |
| `st_mode` | `int` |  |
