# zipfile

Read and write ZIP archive files.

```python
import zipfile
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `zip_stored` | `int` | Stores entries without compression. |
| `zip_deflated` | `int` | Compresses entries with the deflate method. |

## Functions

### `zipfile.write(filename: str, arcname: str | None = None, compress_type: int | None = None)`

Adds a file from disk to the archive.

### `zipfile.writestr(zinfo: ZipInfo, data: Bytes, compress_type: int | None = None)`

Writes bytes to the archive using the supplied ZipInfo metadata.

### `zipfile.writestr(arcname: str, data: Bytes, compress_type: int | None = None)`

Writes bytes to a named archive member.

### `zipfile.writestr(arcname: str, data: str, compress_type: int | None = None)`

Writes UTF-8 text to a named archive member.

### `zipfile.extract(member: str, path: str | None = None) -> str`

Extracts one archive member to a target directory.

### `zipfile.extractall(path: str | None = None, members: list[str] | None = None)`

Extracts all members, or the selected members, to a target directory.

### `zipfile.mkdir(zinf_or_arcname: str)`

Creates a directory entry in the archive.

### `zipfile.is_zipfile(filename: str) -> bool`

Returns True if the file is a readable ZIP archive.

### `zipfile.is_zipfile(data: Bytes) -> bool`

Returns True if the bytes contain a readable ZIP archive.

## ZipFile

Represents a ZIP archive for reading, writing, and extracting members.

### `namelist() -> list[str]`

Returns the names of all archive members.

### `infolist() -> list[ZipInfo]`

Returns ZipInfo objects for all archive members.

### `getinfo(name: str) -> ZipInfo`

Returns metadata for a named archive member.

### `read(name: str) -> Bytes`

Reads an archive member and returns its bytes.

### `open(name: str, mode: str = "r") -> Stream`

Opens a stream for a named archive member.

### `close()`

Closes the archive and releases its underlying resources.

## ZipInfo

Stores metadata describing a ZIP archive member.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `filename` | `str` | Gets or sets the archive member name. |
| `date_time` | `list[int]` | Gets or sets the last modified timestamp as a date-time sequence. |
| `compress_type` | `int` | Gets or sets the ZIP compression method. |
| `comment` | `Bytes` | Gets or sets the per-entry comment bytes. |
| `extra` | `Bytes` | Gets or sets the extra field bytes. |
| `create_system` | `int` | Gets or sets the originating system identifier. |
| `create_version` | `int` | Gets or sets the ZIP version that created the entry. |
| `extract_version` | `int` | Gets or sets the ZIP version needed to extract the entry. |
| `file_size` | `long` | Gets or sets the uncompressed file size. |
| `compress_size` | `long` | Gets or sets the compressed file size. |
| `crc` | `long` | Gets or sets the CRC-32 checksum. |
| `external_attr` | `int` | Gets or sets the external file attributes. |
| `internal_attr` | `int` | Gets or sets the internal file attributes. |
| `flag_bits` | `int` | Gets or sets the ZIP general purpose flag bits. |

### `is_dir() -> bool`

Returns True if the entry represents a directory.

## BadZipFile

Raised when a ZIP archive is invalid or unreadable.

## LargeZipFile

Raised when a ZIP archive is invalid or unreadable.
