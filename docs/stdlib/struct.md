# struct

Interpret bytes as packed binary data.

```python
import struct
```

## Functions

### `struct.pack(format: str, values: list[object]) -> Bytes`

Pack values according to the format string and return as Bytes.

### `struct.unpack(format: str, buffer: Bytes) -> list[object]`

Unpack binary data according to the format string.

### `struct.unpack_from(format: str, buffer: Bytes, offset: int = 0) -> list[object]`

Unpack binary data from a given offset according to the format string.

### `struct.calcsize(format: str) -> int`

Calculate the size (in bytes) of the struct described by the format string.

### `struct.iter_unpack(format: str, buffer: Bytes) -> Iterable[list[object]]`

Iteratively unpack from buffer according to the format string.

## Struct

Pre-compiled struct format for repeated packing/unpacking operations.
Corresponds to Python's struct.Struct class.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `format` | `str` | Gets the format string used to create this Struct instance. |
| `size` | `int` | Gets the calculated size of the struct in bytes. |

### `pack(values: list[object]) -> Bytes`

Pack values according to the pre-compiled format and return as Bytes.

### `unpack(buffer: Bytes) -> list[object]`

Unpack binary data according to the pre-compiled format.

### `unpack_from(buffer: Bytes, offset: int = 0) -> list[object]`

Unpack binary data from a given offset according to the pre-compiled format.

### `iter_unpack(buffer: Bytes) -> Iterable[list[object]]`

Iteratively unpack from buffer according to the pre-compiled format.

## StructError

Exception raised for errors in struct packing/unpacking operations.
Corresponds to Python's struct.error (inherits from Exception, not ValueError).
