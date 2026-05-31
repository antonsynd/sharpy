# zipfile

Read and write ZIP archive files.

```python
import zipfile
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `zip_stored` | `int` |  |
| `zip_deflated` | `int` |  |

## Functions

### `zipfile.write(filename: str, arcname: str | None = None, compress_type: int | None = None)`

### `zipfile.writestr(zinfo: ZipInfo, data: Bytes, compress_type: int | None = None)`

### `zipfile.writestr(arcname: str, data: Bytes, compress_type: int | None = None)`

### `zipfile.writestr(arcname: str, data: str, compress_type: int | None = None)`

### `zipfile.extract(member: str, path: str | None = None) -> str`

### `zipfile.extractall(path: str | None = None, members: list[str] | None = None)`

### `zipfile.mkdir(zinf_or_arcname: str)`

### `zipfile.is_zipfile(filename: str) -> bool`

### `zipfile.is_zipfile(data: Bytes) -> bool`

## ZipFile

### `namelist() -> list[str]`

### `infolist() -> list[ZipInfo]`

### `getinfo(name: str) -> ZipInfo`

### `read(name: str) -> Bytes`

### `open(name: str, mode: str = "r") -> Stream`

### `close()`

## ZipInfo

### Properties

| Name | Type | Description |
|------|------|-------------|
| `filename` | `str` |  |
| `date_time` | `list[int]` |  |
| `compress_type` | `int` |  |
| `comment` | `Bytes` |  |
| `extra` | `Bytes` |  |
| `create_system` | `int` |  |
| `create_version` | `int` |  |
| `extract_version` | `int` |  |
| `file_size` | `long` |  |
| `compress_size` | `long` |  |
| `crc` | `long` |  |
| `external_attr` | `int` |  |
| `internal_attr` | `int` |  |
| `flag_bits` | `int` |  |

### `is_dir() -> bool`

## BadZipFile

## LargeZipFile
