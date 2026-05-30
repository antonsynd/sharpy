<!-- Verified by /verify-plan on 2026-05-29 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Stdlib Batch 4: zlib, gzip, zipfile

## Context

Implement the three compression-related stdlib modules from the [Tier 1 roadmap](roadmap.md) Batch 4. These wrap .NET BCL compression APIs with no NuGet dependencies required.

**Note:** The original Batch 4 included `decimal`, but [#735](https://github.com/antonsynd/sharpy/issues/735) was closed as NOT_PLANNED — Sharpy already has `decimal` as a builtin type mapped to `System.Decimal`, and adding a module would create a naming conflict. The roadmap's `gzip` entry incorrectly referenced [#736](https://github.com/antonsynd/sharpy/issues/736), which is a duplicate of the `zlib` issue [#740](https://github.com/antonsynd/sharpy/issues/740) (closed as duplicate). A new issue is needed for `gzip`.

**GitHub issues:**
- [#740](https://github.com/antonsynd/sharpy/issues/740) — zlib module (deflate compression)
- [#737](https://github.com/antonsynd/sharpy/issues/737) — zipfile module (ZIP archive manipulation)
- [#788](https://github.com/antonsynd/sharpy/issues/788) — gzip module (gzip compression)

## Current State

- **33 stdlib modules** exist in `src/Sharpy.Stdlib/` (31 original + Toml + Yaml; Batches 1-2 may or may not be implemented by the time this plan executes)
- None of these three modules exist yet
- `Bytes` type exists in `Sharpy.Core/Bytes.cs` — zlib and gzip operate heavily on bytes. `Bytes` is an **immutable** `readonly struct` wrapping `byte[]`. Internal `Bytes.Wrap(byte[])` creates a `Bytes` without copying (for internal use where the caller transfers ownership).
- Module infrastructure is mature: `[SharpyModule]`/`[SharpyModuleType]` attributes, `ModuleRegistry` discovery, `.spy` source files, per-module `.csproj` files
- No NuGet dependencies needed — all three modules use only BCL types from `System.IO.Compression`

## Design Decisions

1. **All modules are hand-written C#** (not `.spy`-generated). Rationale: these modules wrap .NET compression streams (`ZLibStream`, `GZipStream`, `ZipArchive`) that require direct `System.IO` and `System.IO.Compression` interop the Sharpy compiler can't express. Follow the pattern of `Hashlib`/`Toml`/`Io`.

2. **zlib uses `ZLibStream` on .NET 10, `DeflateStream` on netstandard2.1** (Axiom 1 — .NET compatibility). `ZLibStream` was added in .NET 6 and handles zlib headers/checksums natively. On `netstandard2.1`, use `DeflateStream` with manual zlib header/trailer handling via `#if NET10_0_OR_GREATER`.

3. **Compression levels map to .NET `CompressionLevel`** with custom level support. Python's levels 0-9 map to: 0 → `NoCompression`, 1 → `Fastest`, 6 → `Optimal` (default), 9 → `SmallestSize` (.NET 10). Intermediate levels (2-5, 7-8) map to the nearest .NET equivalent. `Z_DEFAULT_COMPRESSION` (-1) maps to `Optimal`.

4. **gzip depends on zlib conceptually but not as a project dependency**. Both use `System.IO.Compression` directly. The gzip module wraps `GZipStream` while zlib wraps `ZLibStream`/`DeflateStream`. They share no code at the Sharpy level (unlike Python where `gzip` imports `zlib` internally).

5. **zipfile uses `System.IO.Compression.ZipArchive`** directly. Only `ZIP_STORED` and `ZIP_DEFLATED` compression methods are supported in v1 (no `ZIP_BZIP2` or `ZIP_LZMA` — .NET's `ZipArchive` doesn't support these natively). Document this limitation.

6. **Streaming compression objects** (`compressobj`/`decompressobj`) use internal `MemoryStream` + `ZLibStream`/`DeflateStream` pairs. The `Compress` class wraps a write-mode compression stream; `Decompress` wraps a read-mode decompression stream.

7. **CRC32 and Adler32 checksums**: CRC32 uses `System.IO.Hashing.Crc32` (.NET 7+); on `netstandard2.1`, use a lookup-table implementation. Adler32 requires a custom implementation (no .NET built-in) — simple modular arithmetic per RFC 1950.

8. **`Bytes` for all binary I/O** (Axiom 1). Compress returns `Bytes`, decompress accepts `Bytes`. Matches Python semantics where all compression functions operate on `bytes`.

9. **C# 9.0 compatibility** for `netstandard2.1` target. No file-scoped namespaces, no record structs, no global usings. Use `#if NET10_0_OR_GREATER` for .NET 10-only APIs.

10. **zipfile `ZipFile` class maps to `ZipFileArchive`** in C#. `ZipFile` conflicts with `System.IO.Compression.ZipFile` (a static helper class). Use `[SharpyModuleType("zipfile", "ZipFile")]` so Sharpy code uses `zipfile.ZipFile(...)` while the C# class is named `ZipFileArchive`.

11. **zipfile context manager support**: Implement `IDisposable` on `ZipFileArchive`. The Sharpy `with` statement compiles to `using` in C#, so `IDisposable` gives context manager semantics for free.

## Implementation

Module implementation order: zlib (foundational compression) → gzip (simpler, builds on same .NET APIs) → zipfile (most complex, archive manipulation). Each module follows the standard 5-step pattern.

### Phase 1: zlib Module

**Goal:** Implement `zlib` — deflate/zlib compression and checksums. Core compression module (~400 lines).

#### Tasks

1. **Create zlib module directory and registration** — `src/Sharpy.Stdlib/Zlib/__Init__.cs`
   - Create `Zlib/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("zlib")]` on `public static partial class ZlibModule`
   - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
   - Commit: `feat(stdlib): scaffold zlib module registration`

2. **Implement zlib module constants and error type** — `src/Sharpy.Stdlib/Zlib/ZlibError.cs`, `src/Sharpy.Stdlib/Zlib/ZlibConstants.cs`
   - Create `[SharpyModuleType("zlib")]` class `ZlibError : Exception` (Python's `zlib.error` inherits from `Exception`, not `ValueError` — same pattern as `struct.error`):
     - Simple exception subclass: `ZlibError(string message) : base(message) { }`
   - Create constants as `public static partial class ZlibModule`:
     - `MaxWbits` = 15 (maps to Python's `MAX_WBITS`)
     - `Deflated` = 8
     - `DefMemLevel` = 8
     - `DefBufSize` = 16384
     - `ZDefaultCompression` = -1
     - `ZNoCompression` = 0
     - `ZBestSpeed` = 1
     - `ZBestCompression` = 9
     - `ZDefaultStrategy` = 0
     - `ZFiltered` = 1
     - `ZHuffmanOnly` = 2
     - `ZRle` = 3
     - `ZFixed` = 4
     - `ZNoFlush` = 0
     - `ZPartialFlush` = 1
     - `ZSyncFlush` = 2
     - `ZFullFlush` = 3
     - `ZFinish` = 4
     - `ZBlock` = 5
     - `ZTrees` = 6
   - Commit: `feat(stdlib): implement zlib error type and constants`

3. **Implement zlib core compress/decompress functions** — `src/Sharpy.Stdlib/Zlib/ZlibModule.cs`
   - Implement as `public static partial class ZlibModule`:
     - `Compress(Bytes data, int level = 6)` → `Bytes`
       - Validate level: -1 or 0-9, raise `ZlibError` otherwise
       - Map level to `CompressionLevel`: 0 → `NoCompression`, 1 → `Fastest`, 6 → `Optimal`, 9 → `SmallestSize`
       - On .NET 10: use `ZLibStream` to write data to `MemoryStream`, return compressed bytes
       - On netstandard2.1: use `DeflateStream` with manual 2-byte zlib header (`0x78, 0x9C` for default level) and 4-byte Adler32 trailer
     - `Decompress(Bytes data, int wbits = 15, int bufsize = 16384)` → `Bytes`
       - `wbits` > 0: zlib format (header + trailer)
       - `wbits` < 0: raw deflate (no header/trailer), use `abs(wbits)`
       - `wbits` > 16: gzip format (wbits - 16), use `GZipStream`
       - On .NET 10: use `ZLibStream` for wbits > 0, `DeflateStream` for wbits < 0, `GZipStream` for wbits > 16
       - On netstandard2.1: use `DeflateStream` and manually skip/verify headers
       - Raise `ZlibError` on invalid/corrupt data
   - Commit: `feat(stdlib): implement zlib compress/decompress functions`

4. **Implement zlib checksum functions** — `src/Sharpy.Stdlib/Zlib/ZlibModule.Checksums.cs`
   - Implement as `public static partial class ZlibModule`:
     - `Crc32(Bytes data, long value = 0)` → `long`
       - On .NET 10: use `System.IO.Hashing.Crc32`
       - On netstandard2.1: implement CRC32 with standard polynomial (0xEDB88320) lookup table
       - `value` parameter is the running CRC (for incremental computation)
       - Return as unsigned 32-bit value cast to `long` (Python returns unsigned int)
     - `Adler32(Bytes data, long value = 1)` → `long`
       - Custom implementation: two 16-bit running sums (s1, s2), modulo 65521 per RFC 1950
       - `value` parameter for incremental computation: extract s1 from low 16 bits, s2 from high 16 bits
       - Return as unsigned 32-bit value cast to `long`
   - Verify against Python: `zlib.crc32(b"Hello")` = 4157704578, `zlib.adler32(b"Hello")` = 93061621
   - Commit: `feat(stdlib): implement zlib crc32 and adler32 checksum functions`

5. **Implement zlib streaming compression objects** — `src/Sharpy.Stdlib/Zlib/CompressObj.cs`, `src/Sharpy.Stdlib/Zlib/DecompressObj.cs`
   - Create `[SharpyModuleType("zlib")]` class `CompressObj`:
     - Internal state: `MemoryStream` + `ZLibStream` (or `DeflateStream`) in write mode
     - `Compress(Bytes data)` → `Bytes` — write data, return any available compressed output
     - `Flush(int mode = 4)` → `Bytes` — flush and return remaining compressed bytes. Mode `Z_FINISH` (4) finalizes the stream.
     - Implementation: write to the compression stream, drain the underlying `MemoryStream` for output
   - Create `[SharpyModuleType("zlib")]` class `DecompressObj`:
     - Internal state: buffered compressed input + lazy decompression
     - `Decompress(Bytes data, int maxLength = 0)` → `Bytes` — feed data, return decompressed output
     - `Flush(int length = 16384)` → `Bytes` — return any remaining decompressed data
     - `UnconsumedTail` → `Bytes` — compressed data not yet decompressed
     - `Eof` → `bool` — true if end of compressed stream reached
   - Add factory methods to `ZlibModule`:
     - `Compressobj(int level = 6, int method = 8, int wbits = 15, int memLevel = 8, int strategy = 0)` → `CompressObj`
     - `Decompressobj(int wbits = 15)` → `DecompressObj`
   - Commit: `feat(stdlib): implement zlib streaming compression objects`

6. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Zlib.csproj`
   - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
   - Set `<AssemblyName>Sharpy.Stdlib.Zlib</AssemblyName>`
   - Set `<Compile Include="../Zlib/**/*.cs" />`
   - No NuGet dependencies needed
   - **Add to solution**: `dotnet sln sharpy.sln add src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Zlib.csproj` [CORRECTED: missing step — per-module csprojs must be added to solution; existing Toml csproj is on disk but missing from solution]
   - Commit: `build(stdlib): add Sharpy.Stdlib.Zlib project file`

7. **Create spy stub file** — `src/Sharpy.Stdlib/spy/zlib_module.spy`
   - Define module-level function signatures: `compress`, `decompress`, `crc32`, `adler32`, `compressobj`, `decompressobj`
   - Define constants: `MAX_WBITS`, `DEFLATED`, `DEF_MEM_LEVEL`, `Z_DEFAULT_COMPRESSION`, `Z_NO_COMPRESSION`, `Z_BEST_SPEED`, `Z_BEST_COMPRESSION`
   - Reference types: `error` (ZlibError), `compressobj` result, `decompressobj` result
   - Commit: `feat(stdlib): add zlib module spy source`

8. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
   - `stdlib_zlib.spy` + `stdlib_zlib.expected` — test compress/decompress roundtrip, checksums:
     - `zlib.compress(b"Hello, World!" * 10)` → verify decompression roundtrip
     - `zlib.crc32(b"Hello")` → `4157704578`
     - `zlib.adler32(b"Hello")` → `93061621`
     - Test compression levels 0 and 9 both decompress correctly
     - Verify `len(compressed) < len(original)` for compressible data
   - `stdlib_zlib_streaming.spy` + `stdlib_zlib_streaming.expected` — test streaming objects:
     - `compressobj` + `compress` + `flush` roundtrip with `decompressobj`
     - Multi-chunk streaming compression
   - `stdlib_from_zlib.spy` + `stdlib_from_zlib.expected` — test `from zlib import compress, decompress, crc32`
   - Acceptance: `dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"` passes with new fixtures
   - Commit: `test(stdlib): add zlib module integration tests`

### Phase 2: gzip Module

**Goal:** Implement `gzip` — gzip format compression and decompression. Simpler than zlib (~250 lines) since it wraps `GZipStream` directly.

#### Tasks

9. **Create GitHub issue for gzip module**
   - Create issue via `gh issue create` with title "feat(stdlib): add gzip module — gzip compression" and labels `enhancement,stdlib`
   - Include proposed API, .NET backing (`GZipStream`), and note that #736 was a duplicate zlib issue
   - Record the new issue number for the closing section
   - Commit: (no commit — GitHub issue only)

10. **Create gzip module directory and registration** — `src/Sharpy.Stdlib/Gzip/__Init__.cs`
    - Create `Gzip/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("gzip")]` on `public static partial class GzipModule`
    - Commit: `feat(stdlib): scaffold gzip module registration`

11. **Implement gzip error type** — `src/Sharpy.Stdlib/Gzip/GzipError.cs`
    - Create `[SharpyModuleType("gzip")]` class `BadGzipFile : OSError`:
      - Python's `gzip.BadGzipFile` inherits from `OSError` (since Python 3.8)
      - Constructor: `BadGzipFile(string message) : base(message) { }`
    - Commit: `feat(stdlib): implement gzip BadGzipFile error type`

12. **Implement gzip core functions** — `src/Sharpy.Stdlib/Gzip/GzipModule.cs`
    - Implement as `public static partial class GzipModule`:
      - `Compress(Bytes data, int compresslevel = 9)` → `Bytes`
        - Validate level 0-9, raise `ValueError` otherwise
        - Write data through `GZipStream` in write mode to `MemoryStream`
        - Map compresslevel to `CompressionLevel` (same mapping as zlib)
        - Return gzip-formatted compressed bytes
      - `Decompress(Bytes data)` → `Bytes`
        - Create `MemoryStream` from data, read through `GZipStream`
        - Raise `BadGzipFile` if data is not valid gzip format
      - Both functions produce output compatible with Python's gzip module
    - Commit: `feat(stdlib): implement gzip compress/decompress functions`

13. **Implement GzipFile class** — `src/Sharpy.Stdlib/Gzip/GzipFile.cs`
    - Create `[SharpyModuleType("gzip")]` class `GzipFile : IDisposable`:
      - Constructor: `GzipFile(string filename = "", string mode = "rb", int compresslevel = 9, Stream? fileobj = null)` [CORRECTED: `BinaryFile` type does not exist in Sharpy.Core — only `TextFile` exists. Use `System.IO.Stream` for binary file object parameter, matching how GZipStream operates on streams internally.]
        - `mode` supports: `"rb"` (read binary), `"wb"` (write binary), `"ab"` (append binary)
        - If `fileobj` is provided, use it; otherwise open `filename` as `FileStream`
        - For read: wrap in `GZipStream` (read mode)
        - For write: wrap in `GZipStream` (write mode)
      - Properties:
        - `Name` → `string` — the filename
        - `Mode` → `int` — 1 for read, 2 for write
      - Methods:
        - `Read(int size = -1)` → `Bytes` — read and decompress. If `size` < 0, read all.
        - `Write(Bytes data)` → `int` — compress and write, return bytes written
        - `Close()` — flush and close streams
        - `Readable()` → `bool`
        - `Writable()` → `bool`
        - `Seekable()` → `bool` — always returns `false`
      - `Dispose()` delegates to `Close()`
    - Commit: `feat(stdlib): implement GzipFile class`

14. **Implement gzip.open convenience function** — `src/Sharpy.Stdlib/Gzip/GzipModule.Open.cs`
    - Implement as `public static partial class GzipModule`:
      - `Open(string filename, string mode = "rb", int compresslevel = 9)` → `GzipFile`
        - Create and return a `GzipFile` with the given filename and mode
        - Supports modes: `"rb"`, `"wb"`, `"ab"`, `"rt"`, `"wt"`, `"at"`
        - Text modes (`"rt"`, `"wt"`, `"at"`) are **not supported in v1** — raise `ValueError("text mode not supported")`. Document this limitation.
    - Commit: `feat(stdlib): implement gzip.open convenience function`

15. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Gzip.csproj`
    - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Gzip</AssemblyName>`
    - Set `<Compile Include="../Gzip/**/*.cs" />`
    - **Add to solution**: `dotnet sln sharpy.sln add src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Gzip.csproj`
    - Commit: `build(stdlib): add Sharpy.Stdlib.Gzip project file`

16. **Create spy stub file** — `src/Sharpy.Stdlib/spy/gzip_module.spy`
    - Define module-level function signatures: `compress`, `decompress`, `open`
    - Reference types: `GzipFile`, `BadGzipFile`
    - Commit: `feat(stdlib): add gzip module spy source`

17. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
    - `stdlib_gzip.spy` + `stdlib_gzip.expected` — test compress/decompress roundtrip:
      - `gzip.compress(b"Hello, World!" * 10)` → verify decompression roundtrip
      - Verify `len(compressed) < len(original)` for compressible data
      - Test different compression levels (1, 6, 9)
    - `stdlib_gzip_file.spy` + `stdlib_gzip_file.expected` — test GzipFile class:
      - Write to a gzip file, read it back, verify roundtrip
      - Test `with` statement (context manager) usage
      - Note: tests use `tempfile` module for temp file creation
    - `stdlib_from_gzip.spy` + `stdlib_from_gzip.expected` — test `from gzip import compress, decompress, GzipFile`
    - Acceptance: all test fixtures pass
    - Commit: `test(stdlib): add gzip module integration tests`

### Phase 3: zipfile Module

**Goal:** Implement `zipfile` — ZIP archive creation, reading, and extraction. Most complex of the three (~600 lines, includes multiple types).

#### Tasks

18. **Create zipfile module directory and registration** — `src/Sharpy.Stdlib/Zipfile/__Init__.cs`
    - Create `Zipfile/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("zipfile")]` on `public static partial class ZipfileModule`
    - Commit: `feat(stdlib): scaffold zipfile module registration`

19. **Implement zipfile error types and constants** — `src/Sharpy.Stdlib/Zipfile/ZipfileErrors.cs`, `src/Sharpy.Stdlib/Zipfile/ZipfileConstants.cs`
    - Create `[SharpyModuleType("zipfile")]` class `BadZipFile : Exception`:
      - Python's `BadZipFile` inherits from `Exception`
      - Constructor: `BadZipFile(string message) : base(message) { }`
    - Create `[SharpyModuleType("zipfile")]` class `LargeZipFile : Exception`:
      - For ZIP64 extension errors
      - Constructor: `LargeZipFile(string message) : base(message) { }`
    - Create constants as `public static partial class ZipfileModule`:
      - `ZipStored` = 0
      - `ZipDeflated` = 8
    - Commit: `feat(stdlib): implement zipfile error types and constants`

20. **Implement ZipInfo class** — `src/Sharpy.Stdlib/Zipfile/ZipInfo.cs`
    - Create `[SharpyModuleType("zipfile")]` class `ZipInfo`:
      - Read-only properties populated from `ZipArchiveEntry`:
        - `Filename` → `string`
        - `DateTime` → `Sharpy.Tuple` (6-element: year, month, day, hour, minute, second) — use `ValueTuple<int,int,int,int,int,int>` boxed or a `List<int>`. Since Python returns a tuple, use `Sharpy.List<int>` for consistency with struct module approach.
        - `CompressType` → `int` (0 = stored, 8 = deflated)
        - `Comment` → `Bytes` (entry comment, typically empty)
        - `Extra` → `Bytes` (extra field data)
        - `CreateSystem` → `int` (0 = Windows, 3 = Unix)
        - `CreateVersion` → `int`
        - `ExtractVersion` → `int`
        - `FileSize` → `long` (uncompressed size)
        - `CompressSize` → `long` (compressed size)
        - `Crc` → `long` (CRC-32 checksum)
        - `ExternalAttr` → `int`
        - `InternalAttr` → `int`
        - `FlagBits` → `int`
      - Constructor from `ZipArchiveEntry` (internal, maps .NET properties to Python-style ones)
      - Constructor `ZipInfo(string filename = "NoName")` for creating new entries
      - `ToString()` → `"<ZipInfo filename='{Filename}' compress_type={CompressType}>"` (matches Python repr)
      - `IsDir()` → `bool` — true if filename ends with `/`
    - Commit: `feat(stdlib): implement zipfile ZipInfo class`

21. **Implement ZipFileArchive class (core read operations)** — `src/Sharpy.Stdlib/Zipfile/ZipFileArchive.cs`
    - Create `[SharpyModuleType("zipfile", "ZipFile")]` class `ZipFileArchive : IDisposable`:
      - Internal state: `ZipArchive` instance + `FileStream` (if opened from path)
      - Constructor: `ZipFileArchive(string file, string mode = "r", int compression = 8, bool allowZip64 = true)`
        - `mode`: `"r"` (read), `"w"` (write/create), `"a"` (append)
        - Map to `ZipArchiveMode`: `"r"` → `Read`, `"w"` → `Create`, `"a"` → `Update`
        - `compression` sets default compression for new entries
        - Open `FileStream` and wrap in `ZipArchive`
        - Raise `BadZipFile` if file is not a valid ZIP and mode is `"r"`
      - Read methods:
        - `Namelist()` → `Sharpy.List<string>` — list of entry names
        - `Infolist()` → `Sharpy.List<ZipInfo>` — list of ZipInfo objects
        - `Getinfo(string name)` → `ZipInfo` — get info for specific entry; raise `KeyError` if not found
        - `Read(string name)` → `Bytes` — read and return entry contents
        - `Open(string name, string mode = "r")` → `Stream` — open entry as stream (wraps `ZipArchiveEntry.Open()`) [CORRECTED: `BinaryFile` does not exist — return `System.IO.Stream` directly]
      - `Close()` — dispose the archive
      - `Dispose()` delegates to `Close()`
    - Commit: `feat(stdlib): implement ZipFileArchive class with read operations`

22. **Implement ZipFileArchive write and extract operations** — `src/Sharpy.Stdlib/Zipfile/ZipFileArchive.Write.cs`
    - Implement as additional methods on `ZipFileArchive`:
      - `Write(string filename, string? arcname = null, int? compressType = null)` → void
        - Add a file from the filesystem to the archive
        - `arcname` is the name within the archive (defaults to `filename`)
        - `compressType` overrides the archive default
        - Map to: create `ZipArchiveEntry`, copy file contents
      - `Writestr(string zinfOrArcname, Bytes data, int? compressType = null)` → void
        - Write data directly to archive under given name
        - Also accept `ZipInfo` as first parameter (overload): `Writestr(ZipInfo zinfo, Bytes data, int? compressType = null)`
        - String overload: `Writestr(string arcname, string data, int? compressType = null)` — UTF-8 encode
      - `Extract(string member, string? path = null)` → `string`
        - Extract single member to `path` (or current directory)
        - Return the extracted file path
        - Sanitize paths to prevent zip-slip attacks (reject entries with `..` or absolute paths)
      - `Extractall(string? path = null, Sharpy.List<string>? members = null)` → void
        - Extract all (or specified) members to `path`
        - Same zip-slip protection
      - `Mkdir(string zinfOrArcname)` → void
        - Create a directory entry in the archive
    - Commit: `feat(stdlib): implement ZipFileArchive write and extract operations`

23. **Implement zipfile module-level functions** — `src/Sharpy.Stdlib/Zipfile/ZipfileModule.cs`
    - Implement as `public static partial class ZipfileModule`:
      - `IsZipfile(string filename)` → `bool`
        - Try to open as ZipArchive in read mode; return true on success
        - Return false on any exception
      - `IsZipfile(Bytes data)` → `bool` (overload)
        - Check if bytes represent a valid ZIP (try to open `MemoryStream` as `ZipArchive`)
    - Commit: `feat(stdlib): implement zipfile module-level functions`

24. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Zipfile.csproj`
    - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Zipfile</AssemblyName>`
    - Set `<Compile Include="../Zipfile/**/*.cs" />`
    - **Add to solution**: `dotnet sln sharpy.sln add src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Zipfile.csproj`
    - Commit: `build(stdlib): add Sharpy.Stdlib.Zipfile project file`

25. **Create spy stub file** — `src/Sharpy.Stdlib/spy/zipfile_module.spy`
    - Define module-level function signatures: `is_zipfile`
    - Define constants: `ZIP_STORED`, `ZIP_DEFLATED`
    - Reference types: `ZipFile`, `ZipInfo`, `BadZipFile`, `LargeZipFile`
    - Commit: `feat(stdlib): add zipfile module spy source`

26. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
    - `stdlib_zipfile.spy` + `stdlib_zipfile.expected` — test ZIP creation and reading:
      - Create a ZIP with `writestr`, read back with `read`, verify content
      - Test `namelist()`, `getinfo()`, `infolist()` on created archive
      - Verify `is_zipfile` on a valid ZIP file
      - Note: tests use `tempfile` module for temp file paths
    - `stdlib_zipfile_extract.spy` + `stdlib_zipfile_extract.expected` — test extraction:
      - Create ZIP, `extractall` to temp dir, verify files exist
      - Test `extract` for single file
    - `stdlib_from_zipfile.spy` + `stdlib_from_zipfile.expected` — test `from zipfile import ZipFile, ZipInfo, is_zipfile, ZIP_DEFLATED`
    - Acceptance: all test fixtures pass
    - Commit: `test(stdlib): add zipfile module integration tests`

### Phase 4: Documentation and Cleanup

**Goal:** Create gzip issue, update roadmap, ensure all modules build.

#### Tasks

27. **Update roadmap** — `roadmap.md`
    - Fix issue reference for gzip: #736 → new gzip issue number (since #736 is a duplicate zlib issue, closed)
    - Remove `decimal` from Batch 4 (add note that it was closed as NOT_PLANNED — already a builtin type)
    - Update "Current Modules" count to include all newly added modules
    - Mark Batch 3 as complete if not already done (yaml/toml are implemented)
    - Mark Batch 4 as complete
    - Commit: `docs(stdlib): update roadmap for Batch 4 completion`

28. **Verify monolith build** — `src/Sharpy.Stdlib/Sharpy.Stdlib.csproj`
    - Run `dotnet build` and `dotnet test` to ensure all new modules compile and all tests pass
    - No csproj changes needed for new directories — default SDK glob auto-includes them
    - Acceptance: zero warnings, zero test failures
    - Commit: (no commit — verification only)

## Testing Strategy

### Integration Test Fixtures (9 new fixture pairs)
Each module gets at least two fixture pairs:
- `stdlib_{module}.spy` + `.expected` — tests `import {module}` usage
- `stdlib_from_{module}.spy` + `.expected` — tests `from {module} import ...` usage
- zlib gets an additional pair for streaming objects
- gzip gets an additional pair for GzipFile class
- zipfile gets an additional pair for extract operations

### Deterministic Test Design
- **zlib**: Compress/decompress roundtrips are deterministic (same input → same output for same level). Checksums are fully deterministic — verify exact values against Python output.
- **gzip**: Gzip headers include timestamps; `gzip.compress()` defaults to `mtime=None` which embeds the current time (`time.time()`), so compressed output varies between calls. [CORRECTED: default is `mtime=None`, not `mtime=0`] Verify roundtrips (compress → decompress → equals original), not exact compressed bytes. Test file I/O with temp files.
- **zipfile**: ZIP archives include timestamps and entry ordering. Test by creating archives in memory, verifying names/content/sizes — not exact archive bytes. File operations use `tempfile` for isolation.

### Edge Cases to Cover
- **zlib**: Empty input (`compress(b"")`), large data, all compression levels (0-9), raw deflate (negative wbits), gzip format (wbits > 16), incremental CRC32/Adler32, streaming with multiple chunks
- **gzip**: Empty input, single-byte input, large data, all compression levels, GzipFile read/write lifecycle, invalid gzip data → `BadGzipFile`
- **zipfile**: Empty archive, nested directories, Unicode filenames, large files, stored vs deflated compression, `is_zipfile` on non-ZIP data, `getinfo` with nonexistent name → `KeyError`, zip-slip path traversal prevention

### Negative Test Cases
- `zlib.compress(b"data", level=10)` → error (invalid level)
- `zlib.decompress(b"not compressed")` → `zlib.error`
- `gzip.decompress(b"not gzip")` → `BadGzipFile`
- `zipfile.ZipFile("nonexistent.zip", "r")` → error
- `zipfile.ZipFile(valid_zip, "r").read("nonexistent.txt")` → `KeyError`
- `zipfile.is_zipfile(b"not a zip")` → `False`

## Issues to Close

- #740 — zlib module (closed by Phase 1, Tasks 1-8)
- #737 — zipfile module (closed by Phase 3, Tasks 18-26)
- (new issue) — gzip module (closed by Phase 2, Tasks 9-17)

## Roadmap Corrections Needed

The roadmap (`roadmap.md`) has several issues that should be fixed as part of Task 27:
1. **#736 is labeled as "gzip"** but the issue is actually titled "add zlib module" and is a duplicate of #740. The gzip entry needs a new issue.
2. **`decimal` (#735)** was closed as NOT_PLANNED (already a builtin type). Remove from Batch 4 or mark as dropped.
3. **Batch 4 description** says "decimal, zlib, gzip, zipfile" — should be updated to "zlib, gzip, zipfile" with a note about decimal.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-05-29
**Plan file:** `~/.claude/plans/plan-10449f.md`

### Corrections Made

1. **`BinaryFile` → `Stream`** (Tasks 13, 21): `BinaryFile` type does not exist in Sharpy. Only `TextFile` exists (`src/Sharpy.Core/TextFile.cs`). Changed GzipFile constructor `fileobj` parameter from `BinaryFile?` to `Stream?` and ZipFileArchive.Open return type from `BinaryFile` to `Stream`.

2. **gzip.compress mtime default** (Testing Strategy): Changed "defaults to mtime=0 on Python" to "defaults to `mtime=None` which embeds `time.time()`". Verified: Python's `gzip.compress()` signature is `(data, compresslevel=9, *, mtime=None)` and outputs differ after 1s delay.

3. **Missing `dotnet sln add` step** (Tasks 6, 15, 24): Per-module csproj files must be added to `sharpy.sln`. Discovery: Toml's `Sharpy.Stdlib.Toml.csproj` exists on disk but is NOT in the solution file. 32 csprojs on disk, only 31 in solution. Added `dotnet sln add` command to all three per-module csproj tasks.

### Warnings

1. **`System.IO.Hashing.Crc32` availability** (Task 4): The plan says "No NuGet dependencies needed." `System.IO.Hashing` is part of the .NET shared framework on .NET 8+, so this is correct for `net10.0`. The fallback manual CRC32 implementation for `netstandard2.1` is the right approach. Verify during implementation that `System.IO.Hashing.Crc32` is accessible without an explicit package reference on .NET 10.

2. **Existing Toml csproj not in solution**: The Toml module's per-module csproj (`Sharpy.Stdlib.Toml.csproj`) exists on disk but is missing from `sharpy.sln`. Consider adding it as part of Task 27's cleanup, or investigate whether this is intentional.

3. **Yaml module has no per-module csproj**: The `Yaml/` directory exists with source files but has no `Sharpy.Stdlib.Yaml.csproj` in the `modules/` directory at all. This is a pre-existing gap, not a plan issue.

4. **`ZlibError` naming vs Python**: Python's `zlib.error` is lowercase. The plan names it `ZlibError` in C#. This is consistent with the project's pattern (e.g., `TOMLDecodeError`, `StatisticsError`), but the `[SharpyModuleType]` registration should ensure the Python-facing name is `error` if needed.

### Verified Claims

- **Module count**: 33 stdlib directories confirmed (31 original + Toml + Yaml)
- **`Bytes` type**: Confirmed as `readonly partial struct` in `src/Sharpy.Core/Bytes.cs` with `Bytes.Wrap(byte[])` internal method
- **`SharpyModule`/`SharpyModuleType` attributes**: Pattern confirmed via Hashlib, Toml modules
- **`SharpyModuleType` two-arg constructor**: `(moduleName, pythonName)` confirmed — `[SharpyModuleType("zipfile", "ZipFile")]` is valid
- **Python checksum values**: `zlib.crc32(b"Hello")` = 4157704578, `zlib.adler32(b"Hello")` = 93061621 — confirmed
- **`zlib.error` base class**: `Exception` — confirmed via `python3 -c "import zlib; print(zlib.error.__bases__)"`
- **`BadGzipFile` base class**: `OSError` — confirmed. Sharpy's `OSError` exists at `src/Sharpy.Core/Builtins/Exceptions.cs:166`
- **`KeyError` exists**: Confirmed at `src/Sharpy.Core/KeyError.cs` — zipfile `getinfo()` can raise it
- **Hashlib csproj pattern**: Minimal — `<AssemblyName>`, `<Compile Include>`, no extra deps
- **`Directory.Build.props`**: Exists in `modules/` — handles target frameworks, `LangVersion`, Core project reference
- **Test fixture naming**: `stdlib_from_{module}.spy` pattern confirmed in existing fixtures
- **Roadmap claims**: #736 listed as gzip but is actually a duplicate zlib issue — confirmed. #735 (decimal) not yet marked as dropped in roadmap.
