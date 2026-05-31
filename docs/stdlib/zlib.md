# zlib

```python
import zlib
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `max_wbits` | `int` |  |
| `deflated` | `int` |  |
| `def_mem_level` | `int` |  |
| `def_buf_size` | `int` |  |
| `z_default_compression` | `int` |  |
| `z_no_compression` | `int` |  |
| `z_best_speed` | `int` |  |
| `z_best_compression` | `int` |  |
| `z_default_strategy` | `int` |  |
| `z_filtered` | `int` |  |
| `z_huffman_only` | `int` |  |
| `z_rle` | `int` |  |
| `z_fixed` | `int` |  |
| `z_no_flush` | `int` |  |
| `z_partial_flush` | `int` |  |
| `z_sync_flush` | `int` |  |
| `z_full_flush` | `int` |  |
| `z_finish` | `int` |  |
| `z_block` | `int` |  |
| `z_trees` | `int` |  |

## Functions

### `zlib.crc32(data: Bytes, value: long = 0) -> long`

### `zlib.adler32(data: Bytes, value: long = 1) -> long`

### `zlib.compress(data: Bytes, level: int = 6) -> Bytes`

### `zlib.decompress(data: Bytes, wbits: int = 15, bufsize: int = 16384) -> Bytes`

### `zlib.compressobj(level: int = 6, method: int = 8, wbits: int = 15, mem_level: int = 8, strategy: int = 0) -> CompressObj`

### `zlib.decompressobj(wbits: int = 15) -> DecompressObj`

## CompressObj

### `compress(data: Bytes) -> Bytes`

### `flush(mode: int = 4) -> Bytes`

## DecompressObj

### Properties

| Name | Type | Description |
|------|------|-------------|
| `unconsumed_tail` | `Bytes` |  |
| `eof` | `bool` |  |

### `decompress(data: Bytes, max_length: int = 0) -> Bytes`

### `flush(length: int = 16384) -> Bytes`

## error
