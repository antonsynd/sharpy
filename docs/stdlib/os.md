# os

OS-level operations, similar to Python's os module.
Wraps System.IO and System.Environment for file, directory, and environment operations.

```python
import os
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `sep` | `str` | Path separator character for the current OS. |
| `linesep` | `str` | Line separator for the current OS. |
| `name` | `str` | OS name: "posix" on Unix/macOS, "nt" on Windows. |
| `pathsep` | `str` | Separator used in PATH environment variable. |

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

Remove (delete) a file.

**Parameters:**

- `path` (str) -- The path to the file to remove.

**Raises:**

- `FileNotFoundError` -- Thrown if the file does not exist.
- `IsADirectoryError` -- Thrown if the path is a directory.
- `PermissionError` -- Thrown if permission is denied.

### `os.rename(src: str, dst: str)`

Rename a file or directory.

**Parameters:**

- `src` (str) -- The current path.
- `dst` (str) -- The new path.

**Raises:**

- `FileNotFoundError` -- Thrown if  does not exist.

### `os.mkdir(path: str)`

Create a directory.

**Parameters:**

- `path` (str) -- The directory path to create.

**Raises:**

- `FileExistsError` -- Thrown if the directory already exists.
- `FileNotFoundError` -- Thrown if the parent directory does not exist.

### `os.makedirs(path: str, exist_ok: bool = false)`

Create a directory and all intermediate directories.

**Parameters:**

- `path` (str) -- The directory path to create.
- `exist_ok` (bool) -- If true, do not raise an error if the directory already exists.

**Raises:**

- `FileExistsError` -- Thrown if the directory exists and  is false.

### `os.rmdir(path: str)`

Remove an empty directory.

**Parameters:**

- `path` (str) -- The directory path to remove.

**Raises:**

- `FileNotFoundError` -- Thrown if the directory does not exist.
- `IOError` -- Thrown if the directory is not empty.

### `os.listdir(path: str = ".") -> list[str]`

List directory contents. Returns a list of entry names.

**Parameters:**

- `path` (str) -- Directory path to list. Defaults to current directory.

**Returns:** A list of file and directory names in the given path.

```python
os.listdir(".")         # ["file.txt", "subdir"]
os.listdir("/tmp")      # ["a.log", "b.log"]
```

### `os.getcwd() -> str`

Get the current working directory.

**Returns:** The current working directory as a string.

```python
os.getcwd()    # "/home/user/project"
```

### `os.chdir(path: str)`

Change the current working directory.

**Parameters:**

- `path` (str) -- The directory to change to.

**Raises:**

- `FileNotFoundError` -- Thrown if the directory does not exist.

### `os.getenv(key: str) -> str?`

Get an environment variable, returning None if not set.

**Parameters:**

- `key` (str) -- The environment variable name.

**Returns:** The value, or null if not set.

```python
os.getenv("HOME")       # "/home/user"
os.getenv("MISSING")    # None
```

### `os.getenv(key: str, default_: str) -> str`

Get an environment variable with a default value.

**Parameters:**

- `key` (str) -- The environment variable name.
- `default_` (str) -- The value to return if the variable is not set.

**Returns:** The variable value, or  if not set.

### `os.putenv(key: str, value: str)`

Set an environment variable.

**Parameters:**

- `key` (str) -- The environment variable name.
- `value` (str) -- The value to set.

### `os.stat(path: str) -> StatResult`

Get file or directory status, similar to Python's os.stat().

**Parameters:**

- `path` (str) -- The file or directory path.

**Returns:** A  containing size, timestamps, and mode.

**Raises:**

- `FileNotFoundError` -- Thrown if the path does not exist.

### `os.path_exists(path: str) -> bool`

Check if a path exists (file or directory).

**Parameters:**

- `path` (str) -- The path to check.

**Returns:** true if the path exists; otherwise false.

### `os.join(a: str, b: str) -> str`

Join two or more path components.

**Parameters:**

- `a` (str) -- The first path component.
- `b` (str) -- The second path component.

**Returns:** The joined path.

```python
os.path.join("/home", "user")    # "/home/user"
os.path.join("a", "b")           # "a/b"
```

### `os.join(a: str, b: str, c: str) -> str`

Join multiple path components.

### `os.join(a: str, b: str, c: str, d: str) -> str`

Join multiple path components.

### `os.exists(path: str) -> bool`

Test whether a path exists.

**Parameters:**

- `path` (str) -- The path to test.

**Returns:** true if the path exists.

```python
os.path.exists("/tmp")         # True
os.path.exists("/no/such")     # False
```

### `os.isfile(path: str) -> bool`

Test whether a path is a regular file.

### `os.isdir(path: str) -> bool`

Test whether a path is a directory.

### `os.isabs(path: str) -> bool`

Test whether a path is absolute.

### `os.basename(path: str) -> str`

Return the base name of pathname path (final component).

**Parameters:**

- `path` (str) -- The pathname.

**Returns:** The final component of the path.

```python
os.path.basename("/home/user/file.txt")    # "file.txt"
os.path.basename("/home/user/")             # ""
```

### `os.dirname(path: str) -> str`

Return the directory name of pathname path.

### `os.abspath(path: str) -> str`

Return an absolute version of the path.

### `os.realpath(path: str) -> str`

Return the canonical path, resolving symlinks.

### `os.normpath(path: str) -> str`

Normalize a pathname by collapsing redundant separators and up-level references.

### `os.getsize(path: str) -> long`

Return the size, in bytes, of a file.

### `os.expanduser(path: str) -> str`

Expand ~ and ~user to the user's home directory.
