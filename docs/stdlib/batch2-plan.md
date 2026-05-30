<!-- Verified by /verify-plan on 2026-05-29 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Stdlib Batch 2: struct, urllib, platform

## Context

Implement the three "essential utilities" stdlib modules from the [Tier 1 roadmap](roadmap.md) Batch 2. These are moderate-complexity modules with direct .NET BCL equivalents, no NuGet dependencies required.

**GitHub issues:**
- [#742](https://github.com/antonsynd/sharpy/issues/742) — urllib module (URL parsing and encoding)
- [#743](https://github.com/antonsynd/sharpy/issues/743) — platform module (platform identification)
- [#787](https://github.com/antonsynd/sharpy/issues/787) — struct module (binary data packing/unpacking)

## Current State

- **33 stdlib modules** exist in `src/Sharpy.Stdlib/` (31 original + Toml + Yaml; note: Batch 1 may or may not be implemented by the time this plan executes — it does not affect Batch 2) [CORRECTED: actual directory count is 33, not 35. The 31 original modules + Toml + Yaml = 33.]
- None of these three modules exist yet
- `Bytes` type exists in `Sharpy.Core/Bytes.cs` — struct module operates on bytes
- Module infrastructure is mature: `[SharpyModule]`/`[SharpyModuleType]` attributes, `ModuleRegistry` discovery, `.spy` source files, per-module `.csproj` files
- Multiple modules already export named types (e.g., `datetime` exports 5 types, `pathlib` exports `Path`) [CORRECTED: `os.StatResult` does NOT use `[SharpyModuleType]` attribute — it's a plain public class, not a module-exported type. Removed from examples.]

## Design Decisions

1. **All modules are hand-written C#** (not `.spy`-generated). Rationale: these modules wrap .NET system APIs (`BinaryPrimitives`, `System.Uri`, `RuntimeInformation`) that require direct CLR interop the Sharpy compiler can't express. Follow the pattern of `Hashlib`/`Datetime`/`Os` where `__Init__.cs` + hand-written classes coexist with a `.spy` file for documentation.

2. **struct module uses `BinaryPrimitives` and `BitConverter`** (Axiom 1 — .NET compatibility). Python's `struct` module packs/unpacks binary data via format strings. The .NET equivalent uses `System.Buffers.Binary.BinaryPrimitives` for endian-aware read/write and `BitConverter` for type conversions. Format string parsing is custom (no .NET equivalent).

3. **urllib is a flat module** (not `urllib.parse`). Sharpy uses flat module names — `import urllib` not `import urllib.parse`. All functions from Python's `urllib.parse` are exposed directly on the `urllib` module. `ParseResult` and `SplitResult` are exported as `[SharpyModuleType]` classes.

4. **platform replaces Python-specific functions** with Sharpy/.NET equivalents. `python_version()` → `sharpy_version()`, `python_implementation()` → `dotnet_implementation()`, `python_compiler()` → `dotnet_compiler()`. `uname()` returns a simple named-tuple-like class, not Python's `uname_result`.

5. **C# 9.0 compatibility** for `netstandard2.1` target. No file-scoped namespaces, no record structs, no global usings. Use `#if NET10_0_OR_GREATER` for .NET 10-only APIs where needed.

6. **No new NuGet dependencies.** All three modules use only BCL types.

7. **`Bytes` for all binary I/O in struct module** (Axiom 1). Pack returns `Bytes`, unpack accepts `Bytes`. Matches Python semantics where `struct.pack()` returns `bytes` and `struct.unpack()` accepts `bytes`.

8. **struct format string support**: Support the standard Python format characters: byte order (`@`, `<`, `>`, `!`, `=`), types (`x`, `b`, `B`, `h`, `H`, `i`, `I`, `l`, `L`, `q`, `Q`, `f`, `d`, `s`, `p`, `?`, `n`, `N`). The `@` (native) byte order uses the system's native endianness. Repeat counts (e.g., `3s`, `2i`) are supported.

## Implementation

Module implementation order: platform (simplest, pure read-only) → urllib (moderate, types + functions) → struct (most complex, format string parser + binary I/O).

### Phase 1: platform Module

**Goal:** Implement `platform` — platform detection and identification. Simplest of the three (~100 lines). All functions are pure reads of system information.

#### Tasks

1. **Create platform module directory and registration** — `src/Sharpy.Stdlib/Platform/__Init__.cs`
   - Create `Platform/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("platform")]` on `public static partial class PlatformModule`
   - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
   - Commit: `feat(stdlib): scaffold platform module registration`

2. **Implement platform module functions** — `src/Sharpy.Stdlib/Platform/PlatformModule.cs`
   - Implement as `public static partial class PlatformModule`:
     - `System()` → `string` — returns "Windows", "Linux", or "Darwin". Uses `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` etc. Returns "Unknown" if none match.
     - `Release()` → `string` — returns OS version string. Uses `Environment.OSVersion.Version.ToString()`.
     - `Version()` → `string` — returns OS version description. Uses `Environment.OSVersion.VersionString` or `RuntimeInformation.OSDescription`.
     - `Machine()` → `string` — returns architecture string ("x86_64", "arm64", "AMD64", etc.). Maps `RuntimeInformation.OSArchitecture` enum to standard strings: `Arm64` → "arm64", `X64` → "x86_64" (on macOS/Linux) or "AMD64" (on Windows), `X86` → "x86", `Arm` → "arm".
     - `Node()` → `string` — returns hostname. Uses `Environment.MachineName`.
     - `Processor()` → `string` — returns processor description. Uses `RuntimeInformation.OSArchitecture.ToString()` (best available — .NET has no direct processor name API).
     - `Platform(bool aliased = false, bool terse = false)` → `string` — returns human-readable platform string. Format: `"{OS}-{Version}-{Machine}"`. If `terse`, omit version.
     - `SharpyVersion()` → `string` — returns Sharpy compiler version. Read from `typeof(PlatformModule).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion` or fallback to assembly version.
     - `DotnetVersion()` → `string` — returns .NET runtime version. Uses `Environment.Version.ToString()`.
     - `DotnetImplementation()` → `string` — returns "CoreCLR" (always, since Sharpy targets .NET 10+ / .NET Core).
     - `DotnetCompiler()` → `string` — returns the runtime framework description. Uses `RuntimeInformation.FrameworkDescription`.
     - `Architecture()` → `tuple[str, str]` — returns `(bits, linkage)`. Bits from `RuntimeInformation.OSArchitecture` ("64bit" for X64/Arm64, "32bit" for X86/Arm). Linkage is always `""` (like Python on most platforms).
   - Add `using System.Runtime.InteropServices;` and `using System.Reflection;`
   - Commit: `feat(stdlib): implement platform module functions`

3. **Implement UnameResult class** — `src/Sharpy.Stdlib/Platform/UnameResult.cs`
   - Create `[SharpyModuleType("platform")]` class `UnameResult`:
     - Read-only properties: `System`, `Node`, `Release`, `Version`, `Machine` (all `string`)
     - Constructor takes all five values
     - `ToString()` → `"uname_result(system='{System}', node='{Node}', release='{Release}', version='{Version}', machine='{Machine}')"`
   - Add `Uname()` → `UnameResult` static method to `PlatformModule` returning populated result
   - Commit: `feat(stdlib): implement UnameResult class and uname() function`

4. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Platform.csproj`
   - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
   - Set `<AssemblyName>Sharpy.Stdlib.Platform</AssemblyName>`
   - Set `<Compile Include="../Platform/**/*.cs" />`
   - Commit: `build(stdlib): add Sharpy.Stdlib.Platform project file`

5. **Create spy stub file** — `src/Sharpy.Stdlib/spy/platform_module.spy`
   - Write Sharpy source defining the module-level function signatures
   - Functions: `system`, `release`, `version`, `machine`, `node`, `processor`, `platform`, `sharpy_version`, `dotnet_version`, `dotnet_implementation`, `dotnet_compiler`, `architecture`, `uname`
   - Commit: `feat(stdlib): add platform module spy source`

6. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
   - `stdlib_platform.spy` + `stdlib_platform.expected` — test that `system()`, `machine()`, `node()`, `dotnet_version()` return non-empty strings; test `architecture()` returns a tuple; test `uname()` has correct fields
   - `stdlib_from_platform.spy` + `stdlib_from_platform.expected` — test `from platform import system, machine`
   - Tests must be deterministic: verify non-empty returns and type shapes, not specific OS values
   - Design note: since platform values vary by machine, tests should verify structural properties (e.g., `len(platform.system()) > 0`, `platform.system() in ["Windows", "Linux", "Darwin"]`, `len(platform.architecture()) == 2`)
   - Acceptance: `dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"` passes with new fixtures
   - Commit: `test(stdlib): add platform module integration tests`

### Phase 2: urllib Module

**Goal:** Implement `urllib` — URL parsing, encoding, and manipulation. ~350 lines (medium — includes two result types and multiple encoding functions).

#### Tasks

7. **Create urllib module directory and registration** — `src/Sharpy.Stdlib/Urllib/__Init__.cs`
   - Create `Urllib/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("urllib")]` on `public static partial class UrllibModule`
   - Commit: `feat(stdlib): scaffold urllib module registration`

8. **Implement ParseResult and SplitResult classes** — `src/Sharpy.Stdlib/Urllib/ParseResult.cs`, `src/Sharpy.Stdlib/Urllib/SplitResult.cs`
   - Create `[SharpyModuleType("urllib", "ParseResult")]` class `ParseResult`:
     - Read-only properties: `Scheme`, `Netloc`, `Path`, `Params`, `Query`, `Fragment` (all `string`)
     - Derived properties: `Hostname` → `string?` (extracted from netloc), `Port` → `int?` (extracted from netloc), `Username` → `string?`, `Password` → `string?`
     - Constructor takes all six base components
     - `GetUrl()` → `string` — reassembles the URL
     - `ToString()` → `"ParseResult(scheme='{Scheme}', netloc='{Netloc}', path='{Path}', params='{Params}', query='{Query}', fragment='{Fragment}')"`
   - Create `[SharpyModuleType("urllib", "SplitResult")]` class `SplitResult`:
     - Read-only properties: `Scheme`, `Netloc`, `Path`, `Query`, `Fragment` (all `string`) — no `Params`
     - Same derived properties as ParseResult: `Hostname`, `Port`, `Username`, `Password`
     - Constructor takes five base components
     - `GetUrl()` → `string` — reassembles the URL
     - `ToString()` → similar format
   - Netloc parsing logic (shared between both classes via private helper):
     - Parse `user:pass@host:port` format from netloc
     - `Hostname`: lowercase hostname portion
     - `Port`: integer port or null if not specified
     - `Username`/`Password`: from userinfo portion, or null
   - Commit: `feat(stdlib): implement urllib ParseResult and SplitResult types`

9. **Implement urllib parsing functions** — `src/Sharpy.Stdlib/Urllib/UrllibModule.Parsing.cs`
   - Implement as `public static partial class UrllibModule`:
     - `Urlparse(string url, string scheme = "", bool allowFragments = true)` → `ParseResult`
       - Uses `System.Uri` for robust parsing when possible, with fallback manual parsing for edge cases
       - Extracts scheme, netloc (authority), path, params (`;` delimited in path), query, fragment
       - When `scheme` is non-empty and URL has no scheme, use the provided default
       - When `allowFragments` is false, fragment is included in the preceding component
     - `Urlsplit(string url, string scheme = "", bool allowFragments = true)` → `SplitResult`
       - Like urlparse but does NOT separate params from path
     - `Urlunparse(ParseResult components)` → `string` — reassembles URL from ParseResult
     - `Urlunsplit(SplitResult components)` → `string` — reassembles URL from SplitResult
     - `Urljoin(string baseUrl, string url, bool allowFragments = true)` → `string`
       - Uses `new Uri(new Uri(baseUrl), url).ToString()` for relative URL resolution
   - Commit: `feat(stdlib): implement urllib URL parsing functions`

10. **Implement urllib query string and encoding functions** — `src/Sharpy.Stdlib/Urllib/UrllibModule.Encoding.cs`
    - Implement as `public static partial class UrllibModule`:
      - `ParseQs(string qs, bool keepBlankValues = false)` → `Dict<string, List<string>>`
        - Parse query string into dict of lists (multi-value support)
        - Split on `&`, then split each part on `=`
        - URL-decode keys and values
      - `ParseQsl(string qs, bool keepBlankValues = false)` → `List<tuple[str, str]>`
        - Parse query string into list of (key, value) tuples
      - `Urlencode(Dict<string, string> query)` → `string`
        - Encode dict to query string: `key1=val1&key2=val2`
        - URL-encode keys and values
      - `Urlencode(List<tuple[str, str]> query)` → `string` (overload for list of tuples)
      - `Quote(string s, string safe = "/")` → `string`
        - Percent-encode string. Characters in `safe` are NOT encoded.
        - Uses `Uri.EscapeDataString()` as base, then un-escapes `safe` characters
      - `QuotePlus(string s, string safe = "")` → `string`
        - Like quote, but spaces become `+` instead of `%20`
      - `Unquote(string s)` → `string`
        - Decode percent-encoded string
        - Uses `Uri.UnescapeDataString()`
      - `UnquotePlus(string s)` → `string`
        - Like unquote, but also converts `+` to space
    - Note: `Dict` and `List` here refer to `Sharpy.Dict<K,V>` and `Sharpy.List<T>` from Sharpy.Core
    - Commit: `feat(stdlib): implement urllib query string and encoding functions`

11. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Urllib.csproj`
    - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Urllib</AssemblyName>`
    - Set `<Compile Include="../Urllib/**/*.cs" />`
    - Commit: `build(stdlib): add Sharpy.Stdlib.Urllib project file`

12. **Create spy stub file** — `src/Sharpy.Stdlib/spy/urllib_module.spy`
    - Define module-level function signatures: `urlparse`, `urlsplit`, `urlunparse`, `urlunsplit`, `urljoin`, `parse_qs`, `parse_qsl`, `urlencode`, `quote`, `quote_plus`, `unquote`, `unquote_plus` [CORRECTED: added missing `urlunparse` — defined in Task 9 but was omitted from this list]
    - Reference types: `ParseResult`, `SplitResult`
    - Commit: `feat(stdlib): add urllib module spy source`

13. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
    - `stdlib_urllib.spy` + `stdlib_urllib.expected` — test URL parsing with all components, urljoin, query parsing, encoding/decoding:
      - `urlparse("https://example.com:8080/path?q=1#frag")` → verify scheme, netloc, path, query, fragment, hostname, port
      - `urljoin("https://example.com/a/", "b/c")` → `"https://example.com/a/b/c"`
      - `parse_qs("a=1&b=2&b=3")` → dict with list values
      - `quote("/path with spaces")` → `"/path%20with%20spaces"`
      - `quote_plus("a b+c")` → `"a+b%2Bc"`
      - `unquote("%2Fpath%20with%20spaces")` → `"/path with spaces"`
    - `stdlib_from_urllib.spy` + `stdlib_from_urllib.expected` — test `from urllib import urlparse, quote, ParseResult`
    - `stdlib_urllib_roundtrip.spy` + `stdlib_urllib_roundtrip.expected` — test parse-unparse roundtrips and edge cases (empty components, unusual schemes, unicode)
    - Acceptance: all test fixtures pass
    - Commit: `test(stdlib): add urllib module integration tests`

### Phase 3: struct Module

**Goal:** Implement `struct` — binary data packing/unpacking via format strings. Most complex of the three (~500 lines including format string parser).

#### Tasks

14. **Create struct module directory and registration** — `src/Sharpy.Stdlib/Struct/__Init__.cs`
    - Create `Struct/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("struct")]` on `public static partial class StructModule`
    - Note: `struct` is a C# keyword but this is only a module name string, not a C# identifier — no conflict.
    - Commit: `feat(stdlib): scaffold struct module registration`

15. **Implement format string parser** — `src/Sharpy.Stdlib/Struct/FormatParser.cs`
    - Create internal class `FormatParser` with `Parse(string format)` → `FormatSpec`:
      - `FormatSpec` is an internal class containing:
        - `ByteOrder` — enum: `Native` (`@` — native order, native size, native alignment), `NativeStandard` (`=` — native byte order, standard sizes, no alignment), `LittleEndian` (`<`), `BigEndian` (`>`/`!`) [CORRECTED: `=` is NOT a separate byte order — it uses native byte order but with standard sizes and no alignment. Renamed from `Standard` to `NativeStandard` to avoid confusion]
        - `Fields` — list of `FormatField` (type char + repeat count)
      - `FormatField` contains: `char Type`, `int Count`
    - Parse the format string character by character:
      - First character determines byte order (default `@` if none specified)
      - Subsequent: optional repeat count (digits) + type character
      - Type characters: `x` (pad byte), `b` (int8), `B` (uint8), `h` (int16), `H` (uint16), `i`/`l` (int32), `I`/`L` (uint32), `q` (int64), `Q` (uint64), `f` (float32), `d` (float64), `s` (char[]), `p` (pascal string), `?` (bool), `n` (ssize_t/int), `N` (size_t/uint)
      - For `s` and `p`, the count is the byte length (not repeat count)
    - `CalcSize(FormatSpec spec)` → `int` — compute total byte size
    - Raise `StructError` for unknown format characters [CORRECTED: use StructError, not ValueError]
    - Commit: `feat(stdlib): implement struct format string parser`

16. **Implement struct pack/unpack operations** — `src/Sharpy.Stdlib/Struct/StructModule.Operations.cs`
    - Implement as `public static partial class StructModule`:
      - `Pack(string format, params object[] values)` → `Bytes`
        - Parse format, allocate byte array of `CalcSize`, write values in order
        - Use `BinaryPrimitives.WriteInt32BigEndian()` / `WriteInt32LittleEndian()` etc. for endian-aware writes
        - For `s` type: write `Bytes` or `string` (UTF-8 encoded) padded/truncated to count
        - For `?` type: write `0x00` (false) or `0x01` (true)
        - For `x` type: write zero bytes (padding)
        - Raise `StructError` if wrong number of values for format [CORRECTED: use StructError, not ValueError]
      - `Unpack(string format, Bytes buffer)` → `Sharpy.List<object>` [CORRECTED: `Sharpy.Tuple` does not exist. Sharpy uses `System.ValueTuple` for tuples, but struct.unpack returns variadic-arity tuples determined at runtime. Use `Sharpy.List<object>` as the return type, matching how Python returns a tuple of mixed types. Alternatively, consider a custom `StructResult` wrapper with indexer access.]
        - Parse format, read values from buffer in order
        - Return as list of objects (boxing required for value types)
        - Raise `StructError` if buffer length doesn't match `CalcSize` [CORRECTED: use StructError, not ValueError]
      - `PackInto(string format, Bytes buffer, int offset, params object[] values)` → void
        - Like Pack but writes into existing buffer at offset
        - Buffer must be a mutable `bytearray` equivalent — use `Bytes` with internal mutation support, or accept `List<int>` for mutability
        - **WARNING: `Bytes` is a `readonly struct` — it CANNOT be mutated in-place.** There is no `bytearray` equivalent in Sharpy.Core. `Bytes` copies its input array in the constructor and the `_data` field is readonly. Options: (a) accept `byte[]` directly as the buffer parameter (breaks Sharpy type abstraction but works), (b) skip `pack_into`/`unpack_from` for v1 (defer until `bytearray` type exists), (c) create a new `ByteArray` mutable type in Sharpy.Core. Recommended: option (a) with `byte[]` for now — it's internal plumbing that most users won't call directly.
      - `UnpackFrom(string format, Bytes buffer, int offset = 0)` → `Sharpy.List<object>` [CORRECTED: same as Unpack — no Sharpy.Tuple type]
        - Like Unpack but reads from offset within buffer
      - `Calcsize(string format)` → `int`
        - Return computed byte size for format string
      - `IterUnpack(string format, Bytes buffer)` → `IEnumerable<Sharpy.List<object>>` [CORRECTED: yields lists, not tuples]
        - Yield successive result lists by unpacking chunks of `Calcsize(format)` bytes
        - Raise `StructError` if buffer length is not a multiple of the struct size [CORRECTED: use StructError]
    - Type mapping for unpack return values:
      - `b`, `h`, `i`, `l` → `int`; `q` → `long`; `n` → `long` [CORRECTED: `n` is `ssize_t` = 8 bytes on 64-bit, maps to `long` not `int`]
      - `B`, `H`, `I`, `L` → `int`; `Q` → `long`; `N` → `long` [CORRECTED: `N` is `size_t` = 8 bytes on 64-bit, maps to `long` not `int`. Note: unsigned values > long.MaxValue would need special handling]
      - `f` → `double` (Sharpy's float is double)
      - `d` → `double`
      - `s`, `p` → `Bytes`
      - `?` → `bool`
    - Commit: `feat(stdlib): implement struct pack/unpack operations`

17. **Implement Struct class** — `src/Sharpy.Stdlib/Struct/StructClass.cs`
    - Create `[SharpyModuleType("struct")]` class `StructClass`:
      - Constructor `StructClass(string format)` — pre-parses the format string once
      - Properties:
        - `Format` → `string` (the original format string)
        - `Size` → `int` (computed byte size)
      - Methods:
        - `Pack(params object[] values)` → `Bytes`
        - `Unpack(Bytes buffer)` → `Sharpy.List<object>` [CORRECTED: no Sharpy.Tuple]
        - `PackInto(Bytes buffer, int offset, params object[] values)` → void
        - `UnpackFrom(Bytes buffer, int offset = 0)` → `Sharpy.List<object>` [CORRECTED: no Sharpy.Tuple]
        - `IterUnpack(Bytes buffer)` → `IEnumerable<Sharpy.List<object>>` [CORRECTED: no Sharpy.Tuple]
      - All methods delegate to the module functions but reuse the pre-parsed `FormatSpec`
    - Naming note: Python's class is `struct.Struct`. Since `Struct` is a C# keyword, name the class `StructClass`. The Sharpy name mangler will handle `Struct` → `StructClass` mapping, or use `[SharpyModuleType("struct", "Struct")]` with the two-parameter form to explicitly set the Sharpy-visible name.
    - Commit: `feat(stdlib): implement Struct class for pre-compiled format strings`

18. **Implement struct error type** — `src/Sharpy.Stdlib/Struct/StructError.cs`
    - Create `[SharpyModuleType("struct")]` class `StructError : Exception` [CORRECTED: Python's `struct.error` inherits from `Exception`, NOT `ValueError`. Verified: `struct.error.__mro__` = `[error, Exception, BaseException, object]`]:
      - Simple exception subclass matching Python's `struct.error`
      - Constructor `StructError(string message) : base(message) { }`
    - All struct operations raise `StructError` for format/data errors (not generic `Exception` subclasses)
    - Commit: `feat(stdlib): implement struct.error exception type`

19. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Struct.csproj`
    - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Struct</AssemblyName>`
    - Set `<Compile Include="../Struct/**/*.cs" />`
    - Commit: `build(stdlib): add Sharpy.Stdlib.Struct project file`

20. **Create spy stub file** — `src/Sharpy.Stdlib/spy/struct_module.spy`
    - Define module-level function signatures: `pack`, `unpack`, `pack_into`, `unpack_from`, `calcsize`, `iter_unpack`
    - Reference `Struct` class usage patterns
    - Commit: `feat(stdlib): add struct module spy source`

21. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
    - `stdlib_struct.spy` + `stdlib_struct.expected` — test basic pack/unpack with known values:
      - `struct.pack(">I", 1234)` → verify hex output `000004d2`
      - `struct.unpack(">I", packed)` → `(1234,)` tuple
      - `struct.calcsize(">I")` → `4`
      - `struct.pack("<2i3s", 1, 2, b"abc")` → verify hex
      - Multi-format roundtrip with different byte orders
    - `stdlib_struct_class.spy` + `stdlib_struct_class.expected` — test `Struct` class:
      - Pre-compiled format reuse
      - `s.size`, `s.format` properties
    - `stdlib_struct_iter.spy` + `stdlib_struct_iter.expected` — test `iter_unpack`:
      - Pack 3 values, iter_unpack yields 3 tuples
    - `stdlib_from_struct.spy` + `stdlib_from_struct.expected` — test `from struct import pack, unpack, Struct`
    - Acceptance: all test fixtures pass
    - Commit: `test(stdlib): add struct module integration tests`

### Phase 4: Documentation and Cleanup

**Goal:** Update roadmap, create struct issue, ensure all modules build in monolith.

#### Tasks

22. **Create GitHub issue for struct module**
    - Create issue via `gh issue create` with title "feat(stdlib): add struct module — binary data packing/unpacking" and labels `enhancement,stdlib`
    - Follow the same format as #742/#743
    - Include proposed API, .NET backing (`BinaryPrimitives`), and test plan
    - Commit: (no commit — GitHub issue only)

23. **Update roadmap** — `roadmap.md`
    - Fix issue reference for struct in Batch 2 table: #734 → new struct issue number (since #734 is actually the base64 issue)
    - Update "Current Modules" count to include all newly added modules
    - Mark Batch 1 as complete if it was implemented before this plan
    - Note: Batch 3 (yaml, toml) is already implemented — both `Toml/` and `Yaml/` directories exist. Update roadmap to reflect this if not already done.
    - Commit: `docs(stdlib): update roadmap for Batch 2 completion`

24. **Verify monolith build** — `src/Sharpy.Stdlib/Sharpy.Stdlib.csproj`
    - Run `dotnet build` and `dotnet test` to ensure all new modules compile and all tests pass
    - No csproj changes needed — new directories are auto-included by default SDK glob
    - Acceptance: zero warnings, zero test failures
    - Commit: (no commit — verification only)

## Testing Strategy

### Integration Test Fixtures (10 new files)
Each module gets at least two fixture pairs:
- `stdlib_{module}.spy` + `.expected` — tests `import {module}` usage
- `stdlib_from_{module}.spy` + `.expected` — tests `from {module} import ...` usage
- struct gets two additional pairs for `Struct` class and `iter_unpack`

### Deterministic Test Design
- **platform**: Cannot test exact output values (vary by machine). Test structural properties: non-empty strings, system in known set, architecture tuple has 2 elements, version strings contain digits.
- **urllib**: Fully deterministic — test with fixed URLs and verify exact parsed components, encoding outputs, and roundtrips.
- **struct**: Fully deterministic — binary pack/unpack with known values produces exact hex output. Verify against Python's output for correctness.

### Edge Cases to Cover
- **platform**: All three OS platforms (test structure, not values)
- **urllib**: Empty components, unusual schemes (`ftp://`, `file://`), unicode URLs, query strings with special characters (`&`, `=`, `%`), missing port/userinfo, IPv6 addresses in netloc
- **struct**: Empty format string, pad bytes (`x`), string fields (`s`), boolean fields (`?`), all byte orders, repeat counts, buffer too short/long, wrong number of values, `iter_unpack` with non-divisible buffer

### Negative Test Cases
- `struct.pack(">I", "not_a_number")` → error (type mismatch)
- `struct.unpack(">I", b"ab")` → error (buffer too short)
- `struct.calcsize("Z")` → error (unknown format char)
- `struct.iter_unpack(">H", b"\x00\x01\x02")` → error (3 bytes not divisible by 2)
- `urllib.urlparse("")` → empty ParseResult (not error — matches Python)

## Issues to Close

- #742 — urllib module (closed by Phase 2, Tasks 7-13)
- #743 — platform module (closed by Phase 1, Tasks 1-6)
- (new issue) — struct module (closed by Phase 3, Tasks 14-21)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-05-29
**Plan file:** `~/.claude/plans/plan-5b4b98.md`

### Corrections Made

1. **Module count**: 35 → 33. The actual count is 31 original + Toml + Yaml = 33 directories in `src/Sharpy.Stdlib/`.
2. **`os` exports `StatResult`**: Removed from examples — `StatResult` does NOT have `[SharpyModuleType]` attribute; it's a plain public class.
3. **`struct.error` base class**: `ValueError` → `Exception`. Python's `struct.error.__mro__` is `[error, Exception, BaseException, object]`. Changed `StructError : ValueError` to `StructError : Exception`.
4. **`Sharpy.Tuple` does not exist**: All references to `Sharpy.Tuple` replaced with `Sharpy.List<object>`. Sharpy uses `System.ValueTuple` for tuples, but `struct.unpack` returns variadic-arity results at runtime, so a fixed ValueTuple arity is impossible. `List<object>` is the pragmatic choice.
5. **`ValueError` → `StructError`**: All struct error raises corrected to use `StructError` consistently.
6. **Missing `urlunparse`**: Added to Task 12's spy stub function list (was defined in Task 9 but omitted from the stub).
7. **Issue #734 clarification**: Clarified that the roadmap label says "struct" but the actual issue #734 is "add base64 module".
8. **`n`/`N` type mapping**: `int` → `long`. `n` (ssize_t) and `N` (size_t) are 8 bytes on 64-bit platforms, verified via `struct.calcsize('n')` = 8.
9. **ByteOrder enum**: Renamed `Standard` (`=`) to `NativeStandard` with clarification that `=` means native byte order + standard sizes (not a separate byte order).
10. **Batch 3 note**: Added note that Batch 3 (yaml, toml) is already implemented.

### Warnings

1. **`pack_into` mutability problem**: `Bytes` is a `readonly struct` — cannot be mutated in-place. No `bytearray` equivalent exists in Sharpy.Core. Plan's proposed workaround of "add an internal mutation path" is infeasible. Suggested accepting `byte[]` directly for the buffer parameter as a pragmatic workaround.
2. **Unsigned 64-bit values**: `Q` and `N` format chars produce unsigned 64-bit values. Mapping to `long` loses the top bit for values > `long.MaxValue`. Consider whether `ulong` boxed as `object` is more correct.
3. **`struct.unpack` return type deviation**: Returning `List<object>` instead of a tuple is a semantic deviation from Python. Users expecting tuple unpacking syntax (`a, b = struct.unpack(...)`) will need Sharpy to support unpacking from lists, or a custom `StructResult` type that supports deconstruction.

### Missing Steps Added

- No missing pipeline phases (these are stdlib modules, not language features — Lexer→Parser→Semantic→CodeGen pipeline is not affected).
- Consider adding unit tests for `FormatParser` in a `StructModuleTests.cs` file (only integration tests are specified in the plan).

### Unchecked Claims

- `SharpyModuleType("struct", "Struct")` two-parameter form for naming: verified the attribute constructor exists and accepts `(string moduleName, string pythonName)` — claim is correct.
- Whether the Sharpy name mangler handles `Struct` → `StructClass` mapping: not verified (would require checking `NameMangler.cs`), but the plan's fallback of explicit `[SharpyModuleType("struct", "Struct")]` is the safer approach.
