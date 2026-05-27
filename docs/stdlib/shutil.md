# shutil

```python
import shutil
```

## Functions

### `shutil.which(name: str) -> str?`

Given a command, return the path which conforms to the given mode on the PATH, or `null` if no such file exists.

### `shutil.copy(src: str, dst: str) -> str`

Copy data and mode bits ("cp src dst"). Return the file's destination.

### `shutil.copy2(src: str, dst: str) -> str`

Copy data and metadata. Return the file's destination.

### `shutil.copytree(src: str, dst: str) -> str`

Recursively copy a directory tree and return the destination directory.

### `shutil.rmtree(path: str)`

Recursively delete a directory tree.

### `shutil.move(src: str, dst: str) -> str`

Recursively move a file or directory to another location.

### `shutil._resolve_destination(src: str, dst: str) -> str`

### `shutil._copy_directory_recursive(src: str, dst: str)`
