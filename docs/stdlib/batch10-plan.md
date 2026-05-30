<!-- Verified by /verify-plan on 2026-05-29 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Stdlib Batch 10: tarfile, http, email

## Context

Implement the three "niche" stdlib modules from the [Tier 2 roadmap](roadmap.md) Batch 10. These are the final Tier 2 modules: `tarfile` for tar archive handling, `http` for lower-level HTTP client access (complementing the existing `requests` module), and `email` for email message creation and parsing.

**GitHub issues:**
- [#755](https://github.com/antonsynd/sharpy/issues/755) — tarfile module (tar archive handling)
- [#747](https://github.com/antonsynd/sharpy/issues/747) — http module (HTTP client)
- [#749](https://github.com/antonsynd/sharpy/issues/749) — email module (email message handling)

## Current State

- **31+ stdlib modules** exist in `src/Sharpy.Stdlib/` (31 original + Toml + Yaml; earlier batches may add more by the time this plan executes)
- None of tarfile, http, or email exist yet
- Module infrastructure is mature: `[SharpyModule]`/`[SharpyModuleType]` attributes, `ModuleRegistry` discovery, `.spy` source files, per-module `.csproj` files
- **Existing `requests` module** (`src/Sharpy.Stdlib/Requests/`) provides high-level HTTP with `HttpClient`. The `http` module provides lower-level connection-oriented access.
- **`gzip`/`zlib` modules do NOT exist yet** — they are in Batch 4 (Tier 1). Tarfile depends on these for compressed archives (`r:gz`, `w:gz`, etc.). The tarfile module must handle this gracefully: support uncompressed tar (`r:`, `w:`) natively, and use `System.IO.Compression.GZipStream` directly for gzip support (bypassing the Sharpy gzip module). bz2/xz support deferred to when those modules exist.
- **`Sharpy.Bytes`** exists in Core — immutable byte sequence with Python-like API
- **`System.Formats.Tar`** available in .NET 7+ (net10.0 target) — `TarReader`, `TarWriter`, `TarEntry`
- **`System.Net.Http.HttpClient`** already used by the `requests` module — http module shares the same .NET backing
- **`System.Net.Mail.MailMessage`** and `System.Net.Mime` available in .NET for email

## Design Decisions

1. **Implementation order: http → tarfile → email.** Rationale: `http` is medium complexity with clear .NET backing (`HttpClient`), no dependencies on unimplemented modules, and its `HTTPStatus` enum is self-contained. `tarfile` is medium but requires careful handling of the missing gzip/zlib dependency. `email` is the largest and most complex (parsing, MIME, multipart).

2. **All modules are hand-written C#** (not `.spy`-generated). Rationale: all three wrap complex .NET types with significant adapter logic (connection state, archive I/O, RFC parsing). Better expressed in C# directly.

3. **http: Submodule structure — `http` namespace with `HTTPStatus`, `HTTPConnection`, `HTTPSConnection`, `HTTPResponse`.** Python's `http` is a package (`http.client`, `http.server`, `http.cookiejar`, `http.cookies`). For v1, Sharpy implements only `http.client` functionality as a flat `http` module. The `HTTPStatus` enum is a module-level type. `http.server` and `http.cookiejar` are deferred.

4. **http: `HTTPStatus` as a sealed class with int constants, not a C# enum.** Rationale: Python's `http.HTTPStatus` is an IntEnum with both a numeric value and a `phrase` property. C# enums can't carry a `phrase`. Use a sealed class with `int Value`, `string Name`, `string Phrase` properties and static readonly fields for each status code (e.g., `HTTPStatus.OK = new HTTPStatus(200, "OK", "OK")`). Provide implicit conversion to `int`.

5. **http: `HTTPConnection` wraps `HttpClient` with connection-level semantics.** Python's `HTTPConnection` is connection-oriented (connect → request → getresponse → close). .NET's `HttpClient` is stateless with connection pooling. Bridge: `HTTPConnection` stores the host/port/scheme, builds full URIs from paths, and delegates to a per-connection `HttpClient`. `HTTPSConnection` extends `HTTPConnection` with `https` scheme. The `request()` method sends immediately (no separate `connect()` needed, matching modern Python usage).

6. **http: `HTTPResponse` wraps `HttpResponseMessage`.** Properties: `status` (int), `reason` (string), `headers` (dict-like). Methods: `read()` → `Bytes`, `getheader(name)` → `string?`, `getheaders()` → `List<(string, string)>`. The response owns the `HttpResponseMessage` and disposes it on `close()`.

7. **http: Error hierarchy.** `HTTPException` extends `Exception` (matching Python). Subclasses: `InvalidURL`, `NotConnected`. Map from .NET exceptions: `UriFormatException` → `InvalidURL`, `HttpRequestException` → `HTTPException`.

8. **tarfile: Use `System.Formats.Tar` directly for tar operations.** `TarReader`/`TarWriter` handle the tar format. For gzip compression, use `System.IO.Compression.GZipStream` directly (no dependency on the Sharpy gzip module). bz2 and xz compression modes throw `ValueError("bz2/xz compression not yet supported")` until those modules are implemented. This avoids blocking on Batch 4.

9. **tarfile: `TarFile` class as the main entry point.** Constructor is internal; use `tarfile.open(name, mode)` factory function. Modes: `"r"` (read), `"r:"` (read uncompressed), `"r:gz"` (read gzip), `"w"` (write), `"w:"` (write uncompressed), `"w:gz"` (write gzip). `"r"` auto-detects compression. Implements `IDisposable` for `with` statement support.

10. **tarfile: `TarInfo` class for member metadata.** Properties: `name` (string), `size` (long), `mtime` (double — Unix timestamp), `mode` (int — permission bits), `type` (int — entry type constant), `linkname` (string), `uid` (int), `gid` (int), `uname` (string), `gname` (string), `isdir()`, `isfile()`, `issym()`. Maps from `System.Formats.Tar.TarEntry`.

11. **tarfile: `TarError` exception hierarchy.** `TarError` extends `Exception` (matching Python). Subclasses: `ReadError` (invalid tar), `CompressionError` (unsupported compression), `ExtractError` (extraction failure).

12. **email: `EmailMessage` class as the main type.** Wraps header storage + body content. Headers stored as `List<(string, string)>` to preserve order and allow duplicates (RFC 5322 allows multiple values for some headers). Dict-like access via `msg["Header"]` maps to `GetItem`/`SetItem` methods. Body stored as string (text) or `Bytes` (binary).

13. **email: Module-level parsing functions.** `message_from_string(string text)` → `EmailMessage` and `message_from_bytes(Bytes data)` → `EmailMessage`. Parsing uses a simple RFC 5322 parser: split on `\n\n` or `\r\n\r\n` for header/body boundary, parse headers as `Name: Value` lines with continuation line support (lines starting with whitespace).

14. **email: MIME support via `set_content()` and `add_attachment()`.** `set_content(text, subtype="plain")` sets the body and Content-Type header. `add_attachment(data, maintype, subtype, filename)` adds a MIME attachment — switches to multipart/mixed if not already. `as_string()` serializes to RFC 5322 format. For v1, support plain text, HTML, and binary attachments. Full MIME tree walking is deferred.

15. **email: Simplified error hierarchy.** `MessageError` extends `Exception`. Subclasses: `MessageParseError`, `HeaderParseError`. Keep it minimal for v1 — advanced defect tracking deferred.

16. **email: No SMTP sending.** Matching the GitHub issue: SMTP is out of scope. Users can use .NET `SmtpClient` directly for sending.

17. **C# 9.0 compatibility** for `netstandard2.1` target. No file-scoped namespaces, no record structs, no global usings. Use `#if NET10_0_OR_GREATER` where `System.Formats.Tar` or other .NET 7+ APIs are needed. The tarfile module will be net10.0-only since `System.Formats.Tar` requires .NET 7+.

18. **No new NuGet dependencies.** All three modules use BCL types.

## Implementation

Module implementation order: http (~600 lines) → tarfile (~700 lines) → email (~800 lines). Each module follows the standard stdlib pattern.

### Phase 1: http Module — HTTPStatus

**Goal:** Implement the HTTPStatus class with all standard HTTP status codes.

#### Tasks

1. **Create http module and HTTPStatus class** — `src/Sharpy.Stdlib/Http/__Init__.cs`, `src/Sharpy.Stdlib/Http/HTTPStatus.cs`
   - Create `Http/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("http")]` on `public static partial class HttpModule`
   - Add `HTTPStatus.cs` with `[SharpyModuleType("http", "HTTPStatus")]` sealed class:
     - Properties: `int Value`, `string Name`, `string Phrase`
     - Private constructor: `HTTPStatus(int value, string name, string phrase)`
     - `static HTTPStatus FromValue(int value)` — lookup by status code, throw `ValueError` if unknown
     - Implicit conversion: `public static implicit operator int(HTTPStatus status) => status.Value`
     - `override string ToString()` → `$"{Value}"`
     - `override bool Equals(object? obj)` / `override int GetHashCode()` — compare by Value
     - Static readonly fields for all 62 standard HTTP status codes (100–511), matching Python's `http.HTTPStatus`: [CORRECTED: Python 3.12 has 62, not 63]
       - `CONTINUE = 100`, `SWITCHING_PROTOCOLS = 101`, `PROCESSING = 102`, `EARLY_HINTS = 103`
       - `OK = 200`, `CREATED = 201`, `ACCEPTED = 202`, ... through `IM_USED = 226`
       - `MULTIPLE_CHOICES = 300`, ... through `PERMANENT_REDIRECT = 308`
       - `BAD_REQUEST = 400`, ... through `NETWORK_AUTHENTICATION_REQUIRED = 511`
     - Static dictionary `_valueMap` for `FromValue()` lookup
   - Verified Python behavior:
     - `HTTPStatus.OK` → `200`, `HTTPStatus.OK.phrase` → `'OK'`
     - `HTTPStatus.NOT_FOUND` → `404`, `HTTPStatus.NOT_FOUND.phrase` → `'Not Found'`
     - `HTTPStatus(200)` → `HTTPStatus.OK`
     - `int(HTTPStatus.OK)` → `200`
   - Acceptance: all 62 status codes compile, `FromValue()` round-trips correctly [CORRECTED: 62, not 63]
   - Commit: `feat(stdlib): implement http HTTPStatus class with all standard status codes`

2. **Implement http error classes** — `src/Sharpy.Stdlib/Http/Exceptions.cs`
   - `[SharpyModuleType("http", "HTTPException")]` class extending `Exception`:
     - Constructor: `HTTPException(string message)`, `HTTPException(string message, Exception innerException)`
   - `[SharpyModuleType("http", "InvalidURL")]` class extending `HTTPException`:
     - Constructor: `InvalidURL(string message)`
   - `[SharpyModuleType("http", "NotConnected")]` class extending `HTTPException`:
     - Constructor: `NotConnected(string message)`
   - Acceptance: all exception types compile and inherit correctly
   - Commit: `feat(stdlib): implement http exception hierarchy`

### Phase 2: http Module — HTTPConnection and HTTPResponse

**Goal:** Implement the connection-oriented HTTP client with response handling.

#### Tasks

3. **Implement HTTPResponse class** — `src/Sharpy.Stdlib/Http/HTTPResponse.cs`
   - `[SharpyModuleType("http", "HTTPResponse")]` sealed class implementing `IDisposable`:
     - Internal constructor: `HTTPResponse(HttpResponseMessage response)` — wraps the .NET response
     - Properties:
       - `int Status` — `(int)_response.StatusCode`
       - `string Reason` — `_response.ReasonPhrase ?? ""`
       - `int Version` — HTTP version as int (11 for 1.1, 20 for 2.0)
     - Methods:
       - `Bytes Read()` — read entire response body as `Bytes`. Cache the result for repeated calls.
       - `Bytes Read(int amt)` — read `amt` bytes (for streaming; simplified: read all then slice)
       - `string? Getheader(string name, string? default_ = null)` — get response header by name (case-insensitive)
       - `List<(string, string)> Getheaders()` — all response headers as (name, value) tuples
       - `void Close()` — dispose the underlying response
       - `void Dispose()` — calls `Close()`
     - Verified Python behavior:
       - `response.status` → int (e.g., 200)
       - `response.reason` → str (e.g., "OK")
       - `response.read()` → bytes
       - `response.getheader("Content-Type")` → str
       - `response.getheaders()` → list of (name, value) tuples
   - Acceptance: HTTPResponse compiles and exposes all properties/methods
   - Commit: `feat(stdlib): implement http HTTPResponse class`

4. **Implement HTTPConnection and HTTPSConnection** — `src/Sharpy.Stdlib/Http/HTTPConnection.cs`
   - `[SharpyModuleType("http", "HTTPConnection")]` class implementing `IDisposable`:
     - Constructor: `HTTPConnection(string host, int? port = null, double? timeout = null)`
       - Parse host (strip scheme if included)
       - Default port: 80 (HTTP)
       - Store timeout for per-request `CancellationTokenSource`
     - Properties:
       - `string Host` — the target host
       - `int Port` — the target port
     - Internal state:
       - `HttpClient _client` — created lazily on first `Request()`
       - `HTTPResponse? _lastResponse` — the most recent response (Python's `getresponse()` returns the last one)
     - Methods:
       - `void Request(string method, string url, string? body = null, Dict<string, string>? headers = null)`:
         - Build full URI: `{scheme}://{host}:{port}{url}`
         - Create `HttpRequestMessage` with method, URI, headers, body
         - Send synchronously via `_client.Send()` (.NET 5+) or `_client.SendAsync().GetAwaiter().GetResult()`
         - Store response in `_lastResponse`
         - Catch `UriFormatException` → throw `InvalidURL`
         - Catch `HttpRequestException` → throw `HTTPException`
         - Handle timeout via `CancellationTokenSource` with `_timeout`
       - `HTTPResponse Getresponse()`:
         - Return `_lastResponse` or throw `NotConnected("No response available")`
       - `void Close()` — dispose client
       - `void Dispose()` — calls `Close()`
   - `[SharpyModuleType("http", "HTTPSConnection")]` sealed class extending `HTTPConnection`:
     - Constructor: `HTTPSConnection(string host, int? port = null, double? timeout = null)` — calls base with https scheme
     - Default port: 443
   - Verified Python behavior:
     - `conn = HTTPConnection("example.com", 80)` → creates connection
     - `conn.request("GET", "/")` → sends request
     - `response = conn.getresponse()` → returns HTTPResponse
     - `conn.close()` → closes connection
   - Acceptance: HTTPConnection/HTTPSConnection compile, can send requests and receive responses
   - Commit: `feat(stdlib): implement http HTTPConnection and HTTPSConnection`

5. **Add module-level constants** — `src/Sharpy.Stdlib/Http/HttpConstants.cs`
   - Add to `public static partial class HttpModule`:
     - `int HTTP_PORT = 80`
     - `int HTTPS_PORT = 443`
   - Acceptance: constants accessible as `http.HTTP_PORT`, `http.HTTPS_PORT`
   - Commit: `feat(stdlib): add http module-level constants`

6. **Create http project file and spy stub** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Http.csproj`, `src/Sharpy.Stdlib/spy/http_module.spy`
   - Project file: copy pattern from `Sharpy.Stdlib.Hashlib.csproj`, set `<AssemblyName>Sharpy.Stdlib.Http</AssemblyName>`, `<Compile Include="../Http/**/*.cs" />`
   - Spy stub: define `HTTPStatus`, `HTTPConnection`, `HTTPSConnection`, `HTTPResponse` classes, exception types, and module-level constants
   - Add to `sharpy.sln` via `dotnet sln add` under the `Stdlib.Modules` solution folder (all 31 existing per-module csproj files are in the solution)
   - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Http.csproj` succeeds
   - Commit: `build(stdlib): add http project file and spy stub`

7. **Add http tests** — `src/Sharpy.Stdlib.Tests/HttpTests.cs`
   - Test HTTPStatus:
     - `HTTPStatus.OK.Value` → `200`
     - `HTTPStatus.OK.Phrase` → `"OK"`
     - `HTTPStatus.NOT_FOUND.Value` → `404`
     - `HTTPStatus.NOT_FOUND.Phrase` → `"Not Found"`
     - `HTTPStatus.FromValue(200)` → `HTTPStatus.OK` (reference equality)
     - `HTTPStatus.FromValue(999)` → throws `ValueError`
     - Implicit int conversion: `(int)HTTPStatus.OK` → `200`
     - `HTTPStatus.OK.ToString()` → `"200"`
     - All 62 status codes have non-null Phrase [CORRECTED: 62, not 63]
   - Test HTTPConnection construction:
     - `HTTPConnection("example.com")` — port defaults to 80
     - `HTTPConnection("example.com", 8080)` — custom port
     - `HTTPSConnection("example.com")` — port defaults to 443
   - Test HTTPResponse:
     - Mock `HttpResponseMessage` with status 200, headers, body
     - `response.Status` → `200`
     - `response.Reason` → `"OK"`
     - `response.Read()` → body bytes
     - `response.Getheader("Content-Type")` → correct value
     - `response.Getheaders()` → list of tuples
     - `response.Getheader("Missing", "default")` → `"default"`
   - Test exceptions:
     - `HTTPException` inherits from `Exception`
     - `InvalidURL` inherits from `HTTPException`
     - `NotConnected` inherits from `HTTPException`
   - Test error cases:
     - `Getresponse()` before `Request()` → `NotConnected`
   - Note: integration tests against real HTTP servers are not included (would require network access in CI). Unit tests use mocked `HttpResponseMessage`.
   - Acceptance: all tests pass
   - Commit: `test(stdlib): add http module tests`

### Phase 3: tarfile Module — TarInfo and Error Types

**Goal:** Implement the TarInfo metadata class and tarfile error hierarchy.

#### Tasks

8. **Create tarfile module and error hierarchy** — `src/Sharpy.Stdlib/Tarfile/__Init__.cs`, `src/Sharpy.Stdlib/Tarfile/Exceptions.cs`
   - Create `Tarfile/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("tarfile")]` on `public static partial class TarfileModule`
   - Add `Exceptions.cs`:
     - `[SharpyModuleType("tarfile", "TarError")]` class extending `Exception`:
       - Constructor: `TarError(string message)`, `TarError(string message, Exception innerException)`
     - `[SharpyModuleType("tarfile", "ReadError")]` class extending `TarError`:
       - Constructor: `ReadError(string message)`
     - `[SharpyModuleType("tarfile", "CompressionError")]` class extending `TarError`:
       - Constructor: `CompressionError(string message)`
     - `[SharpyModuleType("tarfile", "ExtractError")]` class extending `TarError`:
       - Constructor: `ExtractError(string message)`, `ExtractError(string message, Exception innerException)`
   - Acceptance: all error types compile and inherit correctly
   - Commit: `feat(stdlib): implement tarfile error hierarchy`

9. **Implement TarInfo class** — `src/Sharpy.Stdlib/Tarfile/TarInfo.cs`
   - `[SharpyModuleType("tarfile", "TarInfo")]` sealed class:
     - Properties (matching Python's `TarInfo`):
       - `string Name { get; set; }` — file name
       - `long Size { get; set; }` — file size in bytes
       - `double Mtime { get; set; }` — modification time as Unix timestamp
       - `int Mode { get; set; }` — permission bits (e.g., 0o755)
       - `int Type { get; set; }` — entry type (REGTYPE, DIRTYPE, SYMTYPE constants)
       - `string Linkname { get; set; }` — target of symbolic link
       - `int Uid { get; set; }` — user ID
       - `int Gid { get; set; }` — group ID
       - `string Uname { get; set; }` — user name
       - `string Gname { get; set; }` — group name
     - Methods:
       - `bool Isfile()` → `Type == REGTYPE`
       - `bool Isdir()` → `Type == DIRTYPE`
       - `bool Issym()` → `Type == SYMTYPE`
       - `bool Islnk()` → `Type == LNKTYPE`
       - `override string ToString()` → `$"<TarInfo '{Name}'>"` (matching Python repr)
     - Internal constructor: `TarInfo()` — defaults for all properties
     - Internal factory method `FromTarEntry(TarEntry entry)` (no `#if` guard needed — tarfile is net10.0-only like Sqlite3): [CORRECTED: removed #if guard since tarfile targets net10.0-only]
       - Map `TarEntry.Name` → `Name`
       - Map `TarEntry.Length` → `Size`
       - Map `TarEntry.ModificationTime.ToUnixTimeSeconds()` → `Mtime`
       - Map `TarEntry.Mode` → `Mode`
       - Map `TarEntryType` enum → integer type constant
       - Map `TarEntry.LinkName` → `Linkname`
   - Module-level constants on `TarfileModule` (note: Python uses bytes `b'0'`, `b'5'`, etc. — Sharpy uses int per Axiom 1):
     - `int REGTYPE = 0` (regular file)
     - `int DIRTYPE = 5` (directory)
     - `int SYMTYPE = 2` (symbolic link)
     - `int LNKTYPE = 1` (hard link)
   - Acceptance: TarInfo compiles, maps from .NET `TarEntry` correctly
   - Commit: `feat(stdlib): implement tarfile TarInfo metadata class`

### Phase 4: tarfile Module — TarFile Class

**Goal:** Implement the TarFile class for reading and writing tar archives.

#### Tasks

10. **Implement TarFile class for reading** — `src/Sharpy.Stdlib/Tarfile/TarFile.cs`
    - `[SharpyModuleType("tarfile", "TarFile")]` sealed class implementing `IDisposable`:
      - Internal constructor (users call `tarfile.open()`)
      - Internal state:
        - `Stream _stream` — the underlying file/gzip stream
        - `string _mode` — "r" or "w"
        - `string _name` — archive file name
        - `List<TarInfo> _members` — cached member list (read mode)
        - `bool _closed`
      - Read mode (no `#if` guard needed — tarfile is net10.0-only): [CORRECTED: consistent with net10.0-only targeting]
        - On construction: open file stream, wrap in `GZipStream` if `r:gz` mode, read all entries via `TarReader`, populate `_members`
        - `List<string> Getnames()` → `_members.Select(m => m.Name).ToList()` wrapped as `List<string>`
        - `List<TarInfo> Getmembers()` → return copy of `_members`
        - `TarInfo Getmember(string name)` → find by name, throw `KeyError` if not found
        - `Bytes? Extractfile(string name)` → re-read archive, find matching entry, read its data stream into `Bytes`. Returns `null` for directories.
        - `void Extractall(string? path = null, List<TarInfo>? members = null)`:
          - Default path: current directory
          - Re-read archive, extract each entry (or only specified members)
          - Regular files: create parent dirs + write file
          - Directories: create directory
          - Catch IO exceptions → wrap in `ExtractError`
        - `void Extract(string name, string? path = null)` → extract single member
      - `void Close()` / `void Dispose()` — close underlying streams
      - `override string ToString()` → `$"<TarFile '{_name}'>"` (matching Python repr, but won't implement `__repr__` since this is a module type)
    - Note: `TarReader` is forward-only, so `Extractfile` and `Extractall` re-open the stream. Cache the member list on first read.
    - Acceptance: TarFile can read tar and tar.gz archives, list members, extract files
    - Commit: `feat(stdlib): implement tarfile TarFile read operations`

11. **Implement TarFile write operations** — `src/Sharpy.Stdlib/Tarfile/TarFileWrite.cs`
    - Partial class extension of `TarFile`:
      - Write mode (no `#if` guard — net10.0-only): [CORRECTED: consistent with net10.0-only targeting]
        - On construction: open file stream, wrap in `GZipStream` if `w:gz` mode
        - `void Add(string name, string? arcname = null, bool recursive = true)`:
          - `arcname` defaults to `name` if null
          - If `name` is a file: create `TarEntry` from file, write via `TarWriter`
          - If `name` is a directory and `recursive`: walk directory tree, add each file/subdir
          - Catch IO exceptions → wrap in `TarError`
        - `void Addfile(TarInfo tarinfo, Stream? fileobj = null)`:
          - Create `TarEntry` from `TarInfo` metadata
          - Write entry with optional data stream
      - Close behavior: flush `TarWriter`, close streams
    - Acceptance: TarFile can create tar and tar.gz archives
    - Commit: `feat(stdlib): implement tarfile TarFile write operations`

12. **Implement module-level functions** — `src/Sharpy.Stdlib/Tarfile/TarfileFunctions.cs`
    - Add to `public static partial class TarfileModule`:
      - `TarFile Open(string name, string mode = "r")`:
        - Parse mode string: `"r"`, `"r:"`, `"r:gz"`, `"w"`, `"w:"`, `"w:gz"`
        - `"r"` auto-detects: try gzip first (check magic bytes `1f 8b`), fall back to plain tar
        - `"r:bz2"`, `"r:xz"`, `"w:bz2"`, `"w:xz"` → throw `CompressionError("bz2/xz compression not yet supported")`
        - Invalid mode → throw `ValueError($"mode '{mode}' is not valid")`
        - Construct and return `TarFile`
      - `bool IsTarfile(string name)`:
        - Try to open as tar archive, read first entry
        - Return `true` if successful, `false` on any error
        - Catch all exceptions, return `false`
    - Acceptance: `tarfile.open()` creates readable/writable archives, `is_tarfile()` validates
    - Commit: `feat(stdlib): implement tarfile module-level functions`

13. **Create tarfile project file and spy stub** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Tarfile.csproj`, `src/Sharpy.Stdlib/spy/tarfile_module.spy`
    - Project file: standard pattern, `<AssemblyName>Sharpy.Stdlib.Tarfile</AssemblyName>`
    - Override `<TargetFrameworks>net10.0</TargetFrameworks>` (not netstandard2.1) since `System.Formats.Tar` requires .NET 7+. Follow the Sqlite3 precedent — no `#if` guards needed. [CORRECTED: resolved ambiguity — use net10.0-only, matching Sqlite3 pattern]
    - Spy stub: define `TarFile`, `TarInfo` classes, error types, constants, and module-level functions
    - Add to `sharpy.sln` via `dotnet sln add` under the `Stdlib.Modules` solution folder
    - Acceptance: project builds successfully on net10.0
    - Commit: `build(stdlib): add tarfile project file and spy stub`

14. **Add tarfile tests** — `src/Sharpy.Stdlib.Tests/TarfileTests.cs`
    - Test TarInfo:
      - Default property values
      - `Isfile()`, `Isdir()`, `Issym()` based on type
      - `ToString()` format
    - Test error hierarchy:
      - `TarError` inherits from `Exception`
      - `ReadError` inherits from `TarError`
      - `CompressionError` inherits from `TarError`
      - `ExtractError` inherits from `TarError`
    - Test TarFile read (requires temp file creation):
      - Create a tar archive programmatically using `System.Formats.Tar.TarWriter`
      - Open with `tarfile.open("test.tar", "r")`
      - `getnames()` returns correct file names
      - `getmembers()` returns `TarInfo` objects with correct metadata
      - `getmember("file.txt")` returns specific member
      - `getmember("nonexistent")` throws `KeyError`
      - `extractfile("file.txt")` returns file contents as `Bytes`
      - `extractfile("subdir/")` returns `null` for directories
    - Test TarFile write:
      - Create archive with `tarfile.open("out.tar", "w")`
      - `add("file.txt")` adds file
      - `add("dir/", recursive=True)` adds directory recursively
      - Close and verify by reading back
    - Test gzip support:
      - Create tar.gz with `tarfile.open("out.tar.gz", "w:gz")`
      - Read back with `tarfile.open("out.tar.gz", "r:gz")`
      - Auto-detect: `tarfile.open("out.tar.gz", "r")` detects gzip
    - Test `is_tarfile()`:
      - Valid tar → `true`
      - Non-tar file → `false`
      - Non-existent file → `false`
    - Test unsupported modes:
      - `tarfile.open("x.tar.bz2", "r:bz2")` → `CompressionError`
      - `tarfile.open("x.tar.xz", "w:xz")` → `CompressionError`
    - Test `extractall()`:
      - Extract to temp directory, verify files exist
      - Extract specific members only
    - Test module constants:
      - `REGTYPE == 0`, `DIRTYPE == 5`, `SYMTYPE == 2`, `LNKTYPE == 1`
    - Acceptance: all tests pass
    - Commit: `test(stdlib): add tarfile module tests`

### Phase 5: email Module — EmailMessage Class

**Goal:** Implement the EmailMessage class with header management and body content.

#### Tasks

15. **Create email module and error hierarchy** — `src/Sharpy.Stdlib/Email/__Init__.cs`, `src/Sharpy.Stdlib/Email/Exceptions.cs`
    - Create `Email/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("email")]` on `public static partial class EmailModule`
    - Add `Exceptions.cs`:
      - `[SharpyModuleType("email", "MessageError")]` class extending `Exception`:
        - Constructor: `MessageError(string message)`, `MessageError(string message, Exception innerException)`
      - `[SharpyModuleType("email", "MessageParseError")]` class extending `MessageError`:
        - Constructor: `MessageParseError(string message)`
      - `[SharpyModuleType("email", "HeaderParseError")]` class extending `MessageError`:
        - Constructor: `HeaderParseError(string message)`
    - Acceptance: all error types compile
    - Commit: `feat(stdlib): implement email error hierarchy`

16. **Implement EmailMessage class — headers** — `src/Sharpy.Stdlib/Email/EmailMessage.cs`
    - `[SharpyModuleType("email", "EmailMessage")]` sealed class:
      - Internal state:
        - `System.Collections.Generic.List<(string name, string value)> _headers` — ordered list of headers (allows duplicates, preserves insertion order)
        - `string? _body` — text body content
        - `Bytes? _binaryBody` — binary body content
        - `System.Collections.Generic.List<Attachment> _attachments` — attachment list
        - `string _contentType` — defaults to `"text/plain"`
        - `string? _boundary` — MIME boundary for multipart
        - `bool _isMultipart` — whether this is a multipart message
      - Constructor: `EmailMessage()` — empty message
      - Header access (dict-like):
        - `string? GetItem(string name)` — get first header value by name (case-insensitive). This is what `msg["Subject"]` maps to.
        - `void SetItem(string name, string value)` — set header value. If header exists, replace first occurrence. If not, append.
        - `void DelItem(string name)` — remove all headers with this name
        - `bool Contains(string name)` — check if header exists
      - Header methods:
        - `List<string> Keys()` — all header names (with duplicates)
        - `List<string> Values()` — all header values
        - `List<(string, string)> Items()` — all (name, value) tuples
        - `List<string>? GetAll(string name)` — all values for a header name, `null` if none
        - `void AddHeader(string name, string value)` — always append (even if name exists)
        - `void ReplaceHeader(string name, string value)` — replace first occurrence or append
    - Acceptance: EmailMessage compiles with full header management
    - Commit: `feat(stdlib): implement email EmailMessage header management`

17. **Implement EmailMessage content and attachments** — `src/Sharpy.Stdlib/Email/EmailMessage.Content.cs`
    - Partial class extension of `EmailMessage`:
      - Content methods:
        - `void SetContent(string text, string subtype = "plain")`:
          - Set `_body = text`
          - Set Content-Type header to `text/{subtype}; charset="utf-8"`
        - `string GetContent()`:
          - Return `_body` or empty string
        - `string? GetPayload()` — alias for `GetContent()` (compat with older email.Message API)
        - `bool IsMultipart()` → `_isMultipart`
      - Attachment methods:
        - `void AddAttachment(Bytes data, string maintype = "application", string subtype = "octet-stream", string? filename = null)`:
          - If not already multipart, switch to multipart/mixed (generate boundary)
          - Create `Attachment` record with data, content type, filename
          - Add to `_attachments`
        - `List<Attachment> IterAttachments()` — return copy of attachments list
      - Internal `Attachment` class:
        - Properties: `Bytes Data`, `string ContentType`, `string? Filename`
      - Serialization:
        - `string AsString()`:
          - Build RFC 5322 formatted string
          - Headers: `Name: Value\r\n` for each header
          - If not multipart: `\r\n` + body
          - If multipart: `\r\n` + MIME boundary + parts (text body part + attachment parts)
          - Each MIME part: headers + `\r\n` + content (base64 for binary attachments)
        - `Bytes AsBytes()`:
          - Encode `AsString()` as UTF-8 bytes
      - `override string ToString()` → `AsString()`
    - Acceptance: EmailMessage can set content, add attachments, serialize to string
    - Commit: `feat(stdlib): implement email EmailMessage content and attachments`

### Phase 6: email Module — Parsing and Module Functions

**Goal:** Implement email parsing from strings/bytes and module-level convenience functions.

#### Tasks

18. **Implement email parser** — `src/Sharpy.Stdlib/Email/Parser.cs`
    - Internal `EmailParser` class:
      - `static EmailMessage ParseString(string text)`:
        - Split on first blank line (`\n\n` or `\r\n\r\n`) → headers portion + body portion
        - Parse headers:
          - Each line is `Name: Value`
          - Continuation lines (starting with whitespace) are appended to the previous header's value
          - If a line doesn't contain `:` and isn't a continuation → throw `HeaderParseError`
        - Set body via `SetContent()` (detect subtype from Content-Type header if present)
        - If Content-Type indicates multipart: parse MIME parts using boundary
          - For each part: parse sub-headers + body
          - Text parts → body content
          - Non-text parts → attachments
        - Return populated `EmailMessage`
      - `static EmailMessage ParseBytes(Bytes data)`:
        - Detect encoding from Content-Type charset parameter (default UTF-8)
        - Decode bytes to string
        - Delegate to `ParseString()`
      - Handle malformed input gracefully:
        - Empty string → empty `EmailMessage`
        - No headers (body only) → `EmailMessage` with body, no headers
        - No body (headers only) → `EmailMessage` with headers, empty body
    - Acceptance: parser handles well-formed and edge-case email strings
    - Commit: `feat(stdlib): implement email parser`

19. **Implement module-level functions** — `src/Sharpy.Stdlib/Email/EmailFunctions.cs`
    - Add to `public static partial class EmailModule`:
      - `EmailMessage MessageFromString(string text)`:
        - Delegate to `EmailParser.ParseString(text)`
      - `EmailMessage MessageFromBytes(Bytes data)`:
        - Delegate to `EmailParser.ParseBytes(data)`
    - Spy stub maps: `email.message_from_string()` → `MessageFromString()`, `email.message_from_bytes()` → `MessageFromBytes()`
    - Acceptance: module-level parsing functions work correctly
    - Commit: `feat(stdlib): implement email module-level parsing functions`

20. **Create email project file and spy stub** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Email.csproj`, `src/Sharpy.Stdlib/spy/email_module.spy`
    - Project file: standard pattern, `<AssemblyName>Sharpy.Stdlib.Email</AssemblyName>`, `<Compile Include="../Email/**/*.cs" />`
    - Spy stub: define `EmailMessage` class (with header access methods, content methods, attachment methods), error types, and module-level functions (`message_from_string`, `message_from_bytes`)
    - Add to `sharpy.sln` via `dotnet sln add` under the `Stdlib.Modules` solution folder
    - Acceptance: project builds successfully
    - Commit: `build(stdlib): add email project file and spy stub`

21. **Add email tests** — `src/Sharpy.Stdlib.Tests/EmailTests.cs`
    - Test EmailMessage construction:
      - Empty message has no headers, empty body
    - Test header management:
      - `msg["Subject"] = "Hello"` → `msg["Subject"]` returns `"Hello"`
      - `msg["From"] = "a@b.com"` → correct
      - Case-insensitive: `msg["subject"]` matches `msg["Subject"]`
      - `Contains("Subject")` → `true`
      - `Keys()` returns all header names
      - `Items()` returns all (name, value) tuples
      - `GetAll("Received")` returns multiple values for duplicate headers
      - `AddHeader("Received", "from server1")` + `AddHeader("Received", "from server2")` → both preserved
      - `ReplaceHeader("Subject", "New")` replaces first occurrence
      - `DelItem("Subject")` removes header
    - Test content:
      - `SetContent("Hello world")` → `GetContent()` returns `"Hello world"`
      - `SetContent("<b>HTML</b>", "html")` → Content-Type is `text/html`
      - `IsMultipart()` → `false` for simple messages
    - Test attachments:
      - Add binary attachment → `IsMultipart()` becomes `true`
      - `IterAttachments()` returns the attachment
      - Attachment has correct content type and filename
    - Test serialization:
      - Simple message: `AsString()` produces valid RFC 5322 format
      - Message with attachments: `AsString()` produces multipart MIME with boundaries
      - `AsBytes()` returns UTF-8 encoded version
    - Test parsing:
      - `message_from_string("Subject: test\nFrom: a@b.com\n\nBody")`:
        - `msg["Subject"]` → `"test"`
        - `msg["From"]` → `"a@b.com"`
        - `GetContent()` → `"Body"`
      - Continuation lines: `"Subject: long\n subject\n\nBody"` → Subject is `"long subject"`
      - Empty string → empty EmailMessage
      - Headers only (no body) → empty body
      - Body only (no headers) → empty headers, body set
      - Parse `message_from_bytes()` with UTF-8 encoded bytes
    - Test roundtrip:
      - Create message → `AsString()` → `message_from_string()` → compare headers and body
    - Test error hierarchy:
      - `MessageError` inherits from `Exception`
      - `MessageParseError` inherits from `MessageError`
      - `HeaderParseError` inherits from `MessageError`
    - Acceptance: all tests pass
    - Commit: `test(stdlib): add email module tests`

### Phase 7: Documentation

**Goal:** Add batch plan doc for reference.

#### Tasks

22. **Add Batch 10 plan to docs** — `docs/stdlib/batch10-plan.md`
    - Save this plan (cleaned up) as the batch plan document in the docs directory
    - Follow the same format as existing batch plans (batch1-plan.md, batch2-plan.md, batch4-plan.md through batch8-plan.md)
    - Acceptance: document exists with correct content
    - Commit: `docs(stdlib): add Batch 10 implementation plan for tarfile, http, email`

## Testing Strategy

### New test fixtures needed

- `src/Sharpy.Stdlib.Tests/HttpTests.cs` — ~30 tests covering HTTPStatus (all codes + round-trips), HTTPConnection construction, HTTPResponse (mocked), exceptions
- `src/Sharpy.Stdlib.Tests/TarfileTests.cs` — ~25 tests covering TarInfo, error hierarchy, read/write tar archives (temp files), gzip support, is_tarfile, extractall
- `src/Sharpy.Stdlib.Tests/EmailTests.cs` — ~30 tests covering header management, content, attachments, serialization, parsing, roundtrip, errors

### Edge cases to cover

**http:**
- HTTPStatus.FromValue with unknown code → ValueError
- HTTPConnection with explicit vs default port
- HTTPSConnection defaults to port 443
- Getresponse() before Request() → NotConnected
- HTTPResponse.Read() called multiple times returns same data (cached)
- HTTPResponse.Getheader() case-insensitive lookup
- HTTPResponse.Getheader() with default value for missing header

**tarfile:**
- Empty tar archive (no entries)
- Tar archive with directories only
- Tar archive with symbolic links
- Large files (> 4GB — TarEntry.Length is long)
- Unicode filenames in tar entries
- Auto-detect mode ("r") with both gzip and plain tar
- Extractall to non-existent directory (should create it)
- Extract with arcname different from original name
- is_tarfile on non-existent file → false
- is_tarfile on truncated/corrupt file → false

**email:**
- Empty EmailMessage serialization
- Headers with special characters (colons, newlines)
- Multiple values for same header name (e.g., Received)
- Very long header values with folding
- Multipart message with multiple attachments
- Parse email with \r\n vs \n line endings
- Parse email with no body (headers only)
- Parse email with no headers (body only)
- Binary attachment roundtrip (encode → serialize → parse → decode)

### Negative test cases

**http:**
- Invalid URL → `InvalidURL` exception
- Unknown HTTPStatus code → `ValueError`
- Response after connection closed → appropriate error

**tarfile:**
- Open non-existent file for reading → appropriate error
- Unsupported compression mode → `CompressionError`
- Invalid tar file → `ReadError`
- getmember() with non-existent name → `KeyError`
- Invalid mode string → `ValueError`

**email:**
- Malformed header line (no colon) → `HeaderParseError`
- Access non-existent header → `null` (not exception)
- GetAll for non-existent header → `null`

## Issues to Close

- #747 — http module (closed by Phase 2, Task 4 — HTTPConnection/HTTPSConnection complete)
- #755 — tarfile module (closed by Phase 4, Task 12 — TarFile + module functions complete)
- #749 — email module (closed by Phase 6, Task 19 — EmailMessage + parsing complete)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-05-29
**Plan file:** `~/.claude/plans/plan-83ddc9.md`

### Corrections Made
- **HTTPStatus count**: Changed "63 standard HTTP status codes" → "62" in 3 locations (Tasks 1, 7). Python 3.12 `len(HTTPStatus)` returns 62.
- **Tarfile targeting**: Resolved contradiction between "net10.0-only" (Design Decision 17) and `#if NET10_0_OR_GREATER` guards in Tasks 9–11. Corrected to consistently use net10.0-only targeting (override `<TargetFrameworks>net10.0</TargetFrameworks>` in csproj, no `#if` guards), matching the Sqlite3 precedent (`Sharpy.Stdlib.Sqlite3.csproj`).
- **Tarfile type constants**: Added note that Python uses bytes (`b'0'`, `b'5'`, `b'2'`, `b'1'`), not ints. Sharpy uses int per Axiom 1 (.NET types win).

### Warnings
- **Tarfile type constants**: Diverges from Python's `bytes` type for `REGTYPE`/`DIRTYPE`/`SYMTYPE`/`LNKTYPE`. Plan uses `int`, which is the correct Axiom 1 choice but should be noted in the spy stub's docstrings.
- **Module class naming**: Convention is inconsistent across stdlib (`MathModule` vs `Json` vs `Requests`). Plan uses `HttpModule`/`TarfileModule`/`EmailModule` which is reasonable but not mandated.

### Missing Steps Added
- **Solution file registration**: All 3 per-module `.csproj` files must be added to `sharpy.sln` via `dotnet sln add` under the `Stdlib.Modules` solution folder. Added to Tasks 6, 13, and 20.

### Unchecked Claims
- **HTTPConnection integration behavior** under network errors — plan defers integration tests (acceptable; unit tests use mocked `HttpResponseMessage`)
- **email multipart MIME roundtrip parsing** — complex RFC behavior; verified basic `message_from_string` works in Python but didn't verify full multipart parsing edge cases
- **GitHub issues #747, #749, #755** — verified all exist and are OPEN (correct)
