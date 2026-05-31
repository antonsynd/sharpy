# gzip

```python
import gzip
```

## Functions

### `gzip.open(filename: str, mode: str = "rb", compresslevel: int = 9) -> GzipFile`

### `gzip.compress(data: Bytes, compresslevel: int = 9) -> Bytes`

### `gzip.decompress(data: Bytes) -> Bytes`

## BadGzipFile

## GzipFile

### Properties

| Name | Type | Description |
|------|------|-------------|
| `name` | `str` |  |
| `mode` | `int` |  |

### `read(size: int = -1) -> Bytes`

### `write(data: Bytes) -> int`

### `close()`

### `readable() -> bool`

### `writable() -> bool`

### `seekable() -> bool`
