# shutil

Utility functions for copying and removal of files and directory trees.

```python
import shutil
```

## Functions

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

### `shutil.which(name: str) -> str | None`

Return the path to an executable which would be run if name were called, or None if not found.
