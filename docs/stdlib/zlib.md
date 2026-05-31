# zlib

Compression and decompression using zlib.

```python
import zlib
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `max_wbits` | `int` | Gets the largest supported window size. |
| `deflated` | `int` | Gets the DEFLATE compression method identifier. |
| `def_mem_level` | `int` | Gets the default memory level for compression. |
| `def_buf_size` | `int` | Gets the default buffer size used by zlib helpers. |
| `z_default_compression` | `int` | Gets the default compression level sentinel. |
| `z_no_compression` | `int` | Gets the constant for no compression. |
| `z_best_speed` | `int` | Gets the fastest compression level constant. |
| `z_best_compression` | `int` | Gets the best compression level constant. |
| `z_default_strategy` | `int` | Gets the default compression strategy constant. |
| `z_filtered` | `int` | Gets the filtered compression strategy constant. |
| `z_huffman_only` | `int` | Gets the Huffman-only compression strategy constant. |
| `z_rle` | `int` | Gets the run-length encoding strategy constant. |
| `z_fixed` | `int` | Gets the fixed-Huffman compression strategy constant. |
| `z_no_flush` | `int` | Gets the constant for no flush. |
| `z_partial_flush` | `int` | Gets the constant for partial flush. |
| `z_sync_flush` | `int` | Gets the constant for synchronous flush. |
| `z_full_flush` | `int` | Gets the constant for full flush. |
| `z_finish` | `int` | Gets the constant for finishing a stream. |
| `z_block` | `int` | Gets the constant for block flush mode. |
| `z_trees` | `int` | Gets the constant for tree flush mode. |

## Functions

### `zlib.crc32(data: Bytes, value: long = 0) -> long`

Computes the CRC-32 checksum of the data.

### `zlib.adler32(data: Bytes, value: long = 1) -> long`

Computes the Adler-32 checksum of the data.

### `zlib.compress(data: Bytes, level: int = 6) -> Bytes`

Compresses data using zlib format.

### `zlib.decompress(data: Bytes, wbits: int = 15, bufsize: int = 16384) -> Bytes`

Decompresses zlib, raw deflate, or gzip data depending on wbits.

### `zlib.compressobj(level: int = 6, method: int = 8, wbits: int = 15, mem_level: int = 8, strategy: int = 0) -> CompressObj`

Creates an incremental compressor object.

### `zlib.decompressobj(wbits: int = 15) -> DecompressObj`

Creates an incremental decompressor object.

## CompressObj

Provides incremental compression like zlib.compressobj.

### `compress(data: Bytes) -> Bytes`

Buffers data for later compression.

### `flush(mode: int = 4) -> Bytes`

Finishes compression and returns the compressed output.

## DecompressObj

Provides incremental decompression like zlib.decompressobj.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `unconsumed_tail` | `Bytes` | Gets compressed data that was not consumed. |
| `eof` | `bool` | Gets a value indicating whether the stream has been finished. |

### `decompress(data: Bytes, max_length: int = 0) -> Bytes`

Buffers compressed data for later decompression.

### `flush(length: int = 16384) -> Bytes`

Finishes decompression and returns the remaining output.

## error

Represents the base exception for zlib errors.
