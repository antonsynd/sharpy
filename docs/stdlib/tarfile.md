# tarfile

Read and write tar archive files.

```python
import tarfile
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `regtype` | `int` |  |
| `dirtype` | `int` |  |
| `symtype` | `int` |  |
| `lnktype` | `int` |  |

## Functions

### `tarfile.open(name: str, mode: str = "r") -> TarFile`

### `tarfile.is_tarfile(name: str) -> bool`

## TarError

Base exception for tarfile errors.
Equivalent to Python's `tarfile.TarError`.

## ReadError

Base exception for tarfile errors.
Equivalent to Python's `tarfile.TarError`.

## CompressionError

Base exception for tarfile errors.
Equivalent to Python's `tarfile.TarError`.

## ExtractError

Base exception for tarfile errors.
Equivalent to Python's `tarfile.TarError`.

## TarFile

Tar archive for reading or writing.
Equivalent to Python's `tarfile.TarFile`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `name` | `str` |  |

### `getnames() -> list[str]`

### `getmembers() -> list[TarInfo]`

### `getmember(name: str) -> TarInfo`

### `extractfile(name: str) -> Bytes | None`

### `extractall(path: str | None = None, members: list[TarInfo] | None = None)`

### `extract(name: str, path: str | None = None)`

### `add(name: str, arcname: str | None = None, recursive: bool = True)`

### `addfile(tarinfo: TarInfo, fileobj: Stream | None = None)`

### `close()`

## TarInfo

Metadata about a tar archive member.
Equivalent to Python's `tarfile.TarInfo`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `name` | `str` |  |
| `size` | `long` |  |
| `mtime` | `float` |  |
| `mode` | `int` |  |
| `type` | `int` |  |
| `linkname` | `str` |  |
| `uid` | `int` |  |
| `gid` | `int` |  |
| `uname` | `str` |  |
| `gname` | `str` |  |

### `isfile() -> bool`

### `isdir() -> bool`

### `issym() -> bool`

### `islnk() -> bool`
