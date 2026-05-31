# gzip

Support for gzip compressed files.

```python
import gzip
```

## Functions

### `gzip.open(filename: str, mode: str = "rb", compresslevel: int = 9) -> GzipFile`

Opens a gzip file in binary mode.

### `gzip.compress(data: Bytes, compresslevel: int = 9) -> Bytes`

Compresses bytes into gzip format.

### `gzip.decompress(data: Bytes) -> Bytes`

Decompresses gzip-compressed bytes.

## BadGzipFile

Raised when gzip data is invalid or unreadable.

## GzipFile

Provides file-like access to gzip-compressed data.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `name` | `str` | Gets the original file name passed to the gzip file. |
| `mode` | `int` | Gets the internal read or write mode flag. |

### `read(size: int = -1) -> Bytes`

Reads decompressed bytes from the gzip stream.

### `write(data: Bytes) -> int`

Writes bytes to the gzip stream and returns the number written.

### `close()`

Closes the gzip stream and its underlying file object.

### `readable() -> bool`

Returns True if the gzip file was opened for reading.

### `writable() -> bool`

Returns True if the gzip file was opened for writing.

### `seekable() -> bool`

Returns False because this gzip wrapper is not seekable.
