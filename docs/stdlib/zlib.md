# zlib

Compression and decompression using zlib.

```python
import zlib
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `MAX_WBITS` | `int` | Gets the largest supported window size. |
| `DEFLATED` | `int` | Gets the DEFLATE compression method identifier. |
| `DEF_MEM_LEVEL` | `int` | Gets the default memory level for compression. |
| `DEF_BUF_SIZE` | `int` | Gets the default buffer size used by zlib helpers. |
| `Z_DEFAULT_COMPRESSION` | `int` | Gets the default compression level sentinel. |
| `Z_NO_COMPRESSION` | `int` | Gets the constant for no compression. |
| `Z_BEST_SPEED` | `int` | Gets the fastest compression level constant. |
| `Z_BEST_COMPRESSION` | `int` | Gets the best compression level constant. |
| `Z_DEFAULT_STRATEGY` | `int` | Gets the default compression strategy constant. |
| `Z_FILTERED` | `int` | Gets the filtered compression strategy constant. |
| `Z_HUFFMAN_ONLY` | `int` | Gets the Huffman-only compression strategy constant. |
| `Z_RLE` | `int` | Gets the run-length encoding strategy constant. |
| `Z_FIXED` | `int` | Gets the fixed-Huffman compression strategy constant. |
| `Z_NO_FLUSH` | `int` | Gets the constant for no flush. |
| `Z_PARTIAL_FLUSH` | `int` | Gets the constant for partial flush. |
| `Z_SYNC_FLUSH` | `int` | Gets the constant for synchronous flush. |
| `Z_FULL_FLUSH` | `int` | Gets the constant for full flush. |
| `Z_FINISH` | `int` | Gets the constant for finishing a stream. |
| `Z_BLOCK` | `int` | Gets the constant for block flush mode. |
| `Z_TREES` | `int` | Gets the constant for tree flush mode. |

## Functions

### `zlib.crc32(data: bytes, value: int64 = 0) -> int64`

Computes the CRC-32 checksum of the data.

### `zlib.adler32(data: bytes, value: int64 = 1) -> int64`

Computes the Adler-32 checksum of the data.

### `zlib.compress(data: bytes, level: int = 6) -> bytes`

Compresses data using zlib format.

### `zlib.decompress(data: bytes, wbits: int = 15, bufsize: int = 16384) -> bytes`

Decompresses zlib, raw deflate, or gzip data depending on wbits.

### `zlib.compressobj(level: int = 6, method: int = 8, wbits: int = 15, mem_level: int = 8, strategy: int = 0) -> CompressObj`

Creates an incremental compressor object.

### `zlib.decompressobj(wbits: int = 15) -> DecompressObj`

Creates an incremental decompressor object.

## CompressObj

Provides incremental compression like zlib.compressobj.

### `compress(data: bytes) -> bytes`

Buffers data for later compression.

### `flush(mode: int = 4) -> bytes`

Finishes compression and returns the compressed output.

## DecompressObj

Provides incremental decompression like zlib.decompressobj.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `unconsumed_tail` | `bytes` | Gets compressed data that was not consumed. |
| `eof` | `bool` | Gets a value indicating whether the stream has been finished. |

### `decompress(data: bytes, max_length: int = 0) -> bytes`

Buffers compressed data for later decompression.

### `flush(length: int = 16384) -> bytes`

Finishes decompression and returns the remaining output.

## error

Represents the base exception for zlib errors.
