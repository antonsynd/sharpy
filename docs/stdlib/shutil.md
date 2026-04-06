# shutil

High-level file operations, similar to Python's shutil module.
Provides functions for copying, moving, and removing files and directory trees.

```python
import shutil
```

## Functions

### `shutil.copy(src: str, dst: str) -> str`

Copy a file to a destination. If  is a directory,
the file is copied into that directory with its original name.
Similar to Python's shutil.copy().

**Parameters:**

- `src` (str) -- Source file path.
- `dst` (str) -- Destination file or directory path.

**Returns:** The path to the destination file.

```python
shutil.copy("src.txt", "dst.txt")       # copy to file
shutil.copy("src.txt", "/tmp/")          # copy into directory
```

**Raises:**

- `OSError` -- Thrown if the source file does not exist or copying fails.

### `shutil.copy2(src: str, dst: str) -> str`

Copy a file to a destination, preserving file metadata (timestamps).
If  is a directory, the file is copied into that directory.
Similar to Python's shutil.copy2().

**Parameters:**

- `src` (str) -- Source file path.
- `dst` (str) -- Destination file or directory path.

**Returns:** The path to the destination file.

```python
shutil.copy2("src.txt", "dst.txt")    # copy with timestamps
```

**Raises:**

- `OSError` -- Thrown if the source file does not exist or copying fails.

### `shutil.copytree(src: str, dst: str) -> str`

Recursively copy a directory tree from  to .
The destination directory must not already exist.
Similar to Python's shutil.copytree().

**Parameters:**

- `src` (str) -- Source directory path.
- `dst` (str) -- Destination directory path (must not exist).

**Returns:** The path to the destination directory.

```python
shutil.copytree("src_dir", "dst_dir")    # recursive copy
```

**Raises:**

- `OSError` -- Thrown if the source does not exist, destination exists, or copying fails.

### `shutil.rmtree(path: str)`

Recursively delete a directory tree.
Similar to Python's shutil.rmtree().

**Parameters:**

- `path` (str) -- The directory to remove.

```python
shutil.rmtree("/tmp/mydir")    # delete directory and all contents
```

**Raises:**

- `OSError` -- Thrown if the directory does not exist or removal fails.

### `shutil.move(src: str, dst: str) -> str`

Move a file or directory to another location.
Similar to Python's shutil.move().

**Parameters:**

- `src` (str) -- Source file or directory path.
- `dst` (str) -- Destination path.

**Returns:** The destination path.

```python
shutil.move("old.txt", "new.txt")         # rename/move file
shutil.move("old_dir", "new_dir")         # rename/move directory
```

**Raises:**

- `OSError` -- Thrown if the source does not exist or moving fails.

### `shutil.which(name: str) -> str?`

Return the path to an executable which would be run if the given command
was called. Returns null if no executable is found.
Similar to Python's shutil.which().

**Parameters:**

- `name` (str) -- The command name to search for.

**Returns:** The full path to the executable, or null if not found.

```python
shutil.which("python")    # "/usr/bin/python"
shutil.which("nonexist")  # None
```
