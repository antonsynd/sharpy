# os

Result of os.stat(), similar to Python's os.stat_result.

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

### `os.getenv(key: str, default_: str) -> str`

### `os.stat(path: str) -> StatResult`

### `os.remove(path: str)`

### `os.rename(src: str, dst: str)`

### `os.mkdir(path: str)`

### `os.makedirs(path: str, exist_ok: bool = false)`

### `os.rmdir(path: str)`

### `os.listdir(path: str = ".") -> list[str]`

### `os.getcwd() -> str`

### `os.chdir(path: str)`

### `os.getenv(key: str) -> Optional[str]`

### `os.putenv(key: str, value: str)`

### `os.path_exists(path: str) -> bool`

### `os.join(a: str, b: str, c: str) -> str`

### `os.join(a: str, b: str, c: str, d: str) -> str`

### `os.normpath(path: str) -> str`

### `os.join(a: str, b: str) -> str`

### `os.exists(path: str) -> bool`

### `os.isfile(path: str) -> bool`

### `os.isdir(path: str) -> bool`

### `os.isabs(path: str) -> bool`

### `os.basename(path: str) -> str`

### `os.dirname(path: str) -> str`

### `os.abspath(path: str) -> str`

### `os.realpath(path: str) -> str`

### `os.getsize(path: str) -> long`

### `os.expanduser(path: str) -> str`
