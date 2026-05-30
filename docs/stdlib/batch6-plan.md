<!-- Verified by /verify-plan on 2026-05-29 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Stdlib Batch 6: configparser, ipaddress

## Context

Implement the two "config + networking" stdlib modules from the [Tier 2 roadmap](roadmap.md) Batch 6. `configparser` provides INI-style configuration file parsing and writing (custom implementation, no .NET equivalent). `ipaddress` wraps `System.Net.IPAddress` and `System.Net.IPNetwork` for IPv4/IPv6 address and network manipulation.

**GitHub issues:**
- [#744](https://github.com/antonsynd/sharpy/issues/744) — configparser module (INI file parsing)
- [#748](https://github.com/antonsynd/sharpy/issues/748) — ipaddress module (IP address manipulation)

## Current State

- **33+ stdlib modules** exist in `src/Sharpy.Stdlib/` (31 original + Toml + Yaml; earlier batches may add more by the time this plan executes)
- Neither configparser nor ipaddress exists yet
- Module infrastructure is mature: `[SharpyModule]`/`[SharpyModuleType]` attributes, `ModuleRegistry` discovery, `.spy` source files, per-module `.csproj` files
- No NuGet dependencies needed — configparser is custom, ipaddress uses BCL (`System.Net.IPAddress`, `System.Net.IPNetwork`)
- `Bytes` type exists in `Sharpy.Core/Bytes.cs` — ipaddress uses bytes for `packed` property
- Existing Toml module (`src/Sharpy.Stdlib/Toml/`) is a good structural reference for configparser (both are config-file parsers, hand-written C#)
- `System.Net.IPNetwork` is available on .NET 8+ (covers our `net10.0` target); netstandard2.1 target needs manual prefix/mask arithmetic

## Design Decisions

1. **Both modules are hand-written C#** (not `.spy`-generated). Rationale: configparser requires a custom INI parser with interpolation, multiline values, and section management — too complex for the current Sharpy compiler. ipaddress wraps .NET BCL types with Python-compatible API surface. Follow the pattern of Hashlib/Toml.

2. **configparser is implemented first** (self-contained, no external dependencies, more commonly used). ipaddress is implemented second (depends on BCL types, has cross-target complexity with `IPNetwork`).

3. **configparser exception hierarchy matches Python's**:
   - `ConfigparserError : Exception` — base for all configparser errors
   - `NoSectionError : ConfigparserError` — section not found
   - `NoOptionError : ConfigparserError` — option not found in section
   - `DuplicateSectionError : ConfigparserError` — duplicate section name
   - `DuplicateOptionError : ConfigparserError` — duplicate key in section
   - `ParsingError : ConfigparserError` — malformed INI syntax
   - `MissingSectionHeaderError : ParsingError` — data before first section header
   - `InterpolationError : ConfigparserError` — base for interpolation errors
   All annotated with `[SharpyModuleType("configparser", "...")]`.

4. **ConfigParser is the primary class** (matching Python 3 recommendation). `RawConfigParser` is NOT implemented — it's deprecated in Python and rarely used. `SafeConfigParser` is also skipped (alias for `ConfigParser` since Python 3.2).

5. **BasicInterpolation is the default** (matching Python). `%(key)s` syntax for value substitution. `ExtendedInterpolation` (`${section:key}` syntax) is available as an alternative. Both implement a common `IInterpolation` interface.

6. **Case-insensitive keys by default** (matching Python). Keys are lowercased on storage. Section names are case-sensitive (matching Python). The `optionxform` behavior is hardcoded to lowercase — no customization in v1.

7. **DEFAULT section provides fallback values** (matching Python). When getting a key, if not found in the specified section, fall back to DEFAULT. `defaults()` returns the DEFAULT section's values.

8. **Multiline values are supported** (matching Python). Continuation lines start with whitespace. Value is joined with newlines.

9. **Both `=` and `:` are delimiters** (matching Python defaults). Inline comments via `#` and `;` are supported when `inline_comment_prefixes` is set (disabled by default, matching Python).

10. **Dict-like access via indexer** — `config["section"]["key"]` maps to `SectionProxy.__getitem__`. `SectionProxy` is a lightweight wrapper around a section's data, returned by `ConfigParser.__getitem__`.

11. **ipaddress factory functions auto-detect version** — `ip_address("...")`, `ip_network("...")`, `ip_interface("...")` return the appropriate IPv4/IPv6 type based on input.

12. **ipaddress types are sealed classes** (C# 9.0 compatibility for netstandard2.1):
    - `IPv4Address` / `IPv6Address` — wrap `System.Net.IPAddress`
    - `IPv4Network` / `IPv6Network` — manual implementation on netstandard2.1, `System.Net.IPNetwork` on net10.0
    - `IPv4Interface` / `IPv6Interface` — combine address + network

13. **Address arithmetic** — `IPv4Address + int`, comparison operators (`<`, `>`, `==`), and `int()` conversion are supported. Implemented via operator overloads on the wrapper types.

14. **Network iteration** — `IPv4Network` and `IPv6Network` implement `IEnumerable<IPv4Address>` / `IEnumerable<IPv6Address>` for iteration over all addresses. `hosts()` excludes network and broadcast addresses (matching Python).

15. **`strict` parameter** — `ip_network("192.168.1.1/24")` throws `ValueError` by default (host bits set). `ip_network("192.168.1.1/24", strict=False)` masks off host bits (matching Python exactly).

16. **C# 9.0 compatibility** for `netstandard2.1` target. No file-scoped namespaces, no record structs, no global usings. Use `#if NET10_0_OR_GREATER` for `IPNetwork` usage.

17. **No new NuGet dependencies.** configparser is pure string parsing. ipaddress uses BCL types only.

## Implementation

Module implementation order: configparser (self-contained, ~700 lines) → ipaddress (~600 lines). Each module follows the standard stdlib pattern.

### Phase 1: configparser Module — Core Infrastructure

**Goal:** Scaffold configparser module, implement exception hierarchy and interpolation interfaces.

#### Tasks

1. **Create configparser module directory and registration** — `src/Sharpy.Stdlib/Configparser/__Init__.cs`
   - Create `Configparser/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("configparser")]` on `public static partial class ConfigparserModule`
   - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
   - Acceptance: `ConfigparserModule` class compiles with `[SharpyModule]` attribute
   - Commit: `feat(stdlib): scaffold configparser module registration`

2. **Implement configparser exception hierarchy** — `src/Sharpy.Stdlib/Configparser/Errors.cs`
   - Create exception classes (all `[SharpyModuleType("configparser", "...")]`):
     - `ConfigparserError : Exception` — base; message pass-through constructor. [CORRECTED: Python name is `configparser.Error`, not `ConfigparserError`. Use `[SharpyModuleType("configparser", "Error")]` so the Python-facing name is `Error`.]
     - `NoSectionError : ConfigparserError` — `NoSectionError(string section)` with message `"No section: '{section}'"`
     - `NoOptionError : ConfigparserError` — `NoOptionError(string option, string section)` with message `"No option '{option}' in section: '{section}'"`
     - `DuplicateSectionError : ConfigparserError` — `DuplicateSectionError(string section)` with message `"Section '{section}' already exists"`
     - `DuplicateOptionError : ConfigparserError` — `DuplicateOptionError(string section, string option)` with message `"Option '{option}' in section '{section}' already exists"`
     - `ParsingError : ConfigparserError` — `ParsingError(string message)` with line tracking
     - `MissingSectionHeaderError : ParsingError` — `MissingSectionHeaderError(string filename, int lineno, string line)` with message `"File contains no section headers.\nfile: '{filename}', line: {lineno}\n'{line}'"`
     - `InterpolationError : ConfigparserError` — base for interpolation errors
   - Acceptance: exception hierarchy compiles, matches Python's hierarchy
   - Commit: `feat(stdlib): implement configparser exception hierarchy`

3. **Implement interpolation** — `src/Sharpy.Stdlib/Configparser/Interpolation.cs`
   - Define interface:
     ```csharp
     public interface IInterpolation
     {
         string BeforeGet(ConfigParser parser, string section, string option, string rawValue);
         string BeforeSet(ConfigParser parser, string section, string option, string value);
     }
     ```
   - `BasicInterpolation : IInterpolation`:
     - `BeforeGet`: replace `%(key)s` with value from same section (or DEFAULT). Max 10 interpolation depth (throw `InterpolationError` on cycle/overflow). Look up `key` via `parser.Get(section, key, raw: true, fallback: ...)` to get the raw value, then recursively interpolate.
     - `BeforeSet`: return value as-is (no transformation on set)
   - `ExtendedInterpolation : IInterpolation`:
     - `BeforeGet`: replace `${section:key}` with value from named section, or `${key}` with value from current section. Same depth limit.
     - `BeforeSet`: return value as-is
   - `RawInterpolation : IInterpolation` (no-op, for `raw=True` mode internally):
     - Both methods return value as-is
   - Acceptance: interpolation classes compile with correct substitution behavior
   - Commit: `feat(stdlib): implement configparser interpolation (Basic, Extended, Raw)`

### Phase 2: configparser Module — ConfigParser Class

**Goal:** Implement the `ConfigParser` class with full read/write/access functionality.

#### Tasks

4. **Implement SectionProxy** — `src/Sharpy.Stdlib/Configparser/SectionProxy.cs`
   - Create `[SharpyModuleType("configparser", "SectionProxy")]` sealed class:
     - Internal constructor: `SectionProxy(ConfigParser parser, string section)`
     - Indexer: `string this[string key]` — delegates to `_parser.Get(_section, key)`
     - Setter indexer: delegates to `_parser.Set(_section, key, value)`
     - `bool ContainsKey(string key)` — delegates to `_parser.HasOption(_section, key)`
     - `IEnumerable<string> Keys()` — delegates to `_parser.Options(_section)`
     - `IEnumerable<KeyValuePair<string, string>> Items()` — delegates to `_parser.Items(_section)`
     - `string GetOrDefault(string key, string fallback)` — delegates to `_parser.Get(_section, key, fallback: fallback)`
   - Acceptance: SectionProxy provides dict-like access to section data
   - Commit: `feat(stdlib): implement configparser SectionProxy`

5. **Implement ConfigParser — data storage and access** — `src/Sharpy.Stdlib/Configparser/ConfigParser.cs`
   - Create `[SharpyModuleType("configparser", "ConfigParser")]` sealed class:
     - Constructor: `ConfigParser(IInterpolation? interpolation = null, bool allowNoValue = false)`
       - Default interpolation: `BasicInterpolation`
       - Internal storage: `Dictionary<string, Dictionary<string, string?>>` (section → key → value)
       - DEFAULT section always exists (keyed as `"DEFAULT"` internally)
     - Section management:
       - `List<string> Sections()` — return all section names (excluding DEFAULT)
       - `void AddSection(string section)` — add empty section; throw `DuplicateSectionError` if exists; throw `ValueError` if section is "DEFAULT" (case-insensitive)
       - `bool HasSection(string section)` — check if section exists (excluding DEFAULT)
       - `bool RemoveSection(string section)` — remove section, return true if existed
     - Option access:
       - `string Get(string section, string option, string? fallback = null, bool raw = false)` — get value with interpolation. If not found: try DEFAULT, then use fallback, then throw `NoOptionError`. If `raw=true`, skip interpolation.
       - `int GetInt(string section, string option, int? fallback = null)` — parse as int; throw `ValueError` on parse failure
       - `double GetFloat(string section, string option, double? fallback = null)` — parse as double
       - `bool GetBoolean(string section, string option, bool? fallback = null)` — recognize: `1/yes/true/on` → true, `0/no/false/off` → false (case-insensitive); throw `ValueError` otherwise
       - `void Set(string section, string option, string value)` — set value; throw `NoSectionError` if section doesn't exist. Key is lowercased.
       - `bool HasOption(string section, string option)` — check if option exists in section or DEFAULT
       - `bool RemoveOption(string section, string option)` — remove option, return true if existed
       - `List<string> Options(string section)` — return all option names in section (merged with DEFAULT)
       - `List<(string, string)> Items(string section)` — return all (key, value) pairs (merged with DEFAULT, interpolated)
     - Dict-like access:
       - `SectionProxy this[string section]` — return `SectionProxy` for section; throw `KeyNotFoundException` wrapping `NoSectionError` if not found
     - Utility:
       - `Dictionary<string, string> Defaults()` — return DEFAULT section's key-value pairs
   - Implementation notes:
     - All key lookups use `option.ToLowerInvariant()` (matching Python's `optionxform`)
     - DEFAULT section is merged into every section for lookups but not for `Sections()` listing
   - Acceptance: all access methods compile and handle DEFAULT fallback correctly
   - Commit: `feat(stdlib): implement configparser ConfigParser core`

6. **Implement ConfigParser — reading and writing** — `src/Sharpy.Stdlib/Configparser/ConfigParser.IO.cs`
   - Add to `ConfigParser` as partial class:
     - `void ReadString(string content, string source = "<string>")` — parse INI from string:
       - Line-by-line parsing with state tracking
       - Section headers: `[section]` (whitespace inside brackets is preserved, matching Python — `[ section ]` → section name ` section `) [CORRECTED: Python does NOT strip whitespace inside brackets]
       - Key-value pairs: `key = value` or `key : value` or `key=value`
       - Comments: lines starting with `#` or `;` (after stripping leading whitespace)
       - Multiline values: continuation lines start with whitespace (tab or space)
       - Empty lines: skip
       - `allow_no_value=True`: keys with no value (no `=` or `:`) are stored with `null` value
       - Throw `MissingSectionHeaderError` if key-value pair appears before any section header
       - Throw `ParsingError` on malformed lines (not a section, not a key-value, not a comment, not blank)
     - `void Read(string filename)` — read file contents, delegate to `ReadString` with filename as source
     - `void ReadDict(Dictionary<string, Dictionary<string, string>> dictionary)` — bulk load from nested dict
     - `void Write(System.IO.TextWriter writer, bool spaceAroundDelimiters = true)` — write INI format:
       - Delimiter: ` = ` if `spaceAroundDelimiters`, `=` otherwise
       - DEFAULT section written first (if non-empty)
       - Sections written in insertion order
       - Blank line between sections
       - Format: `[section]\nkey = value\n`
     - `void WriteToFile(string filename, bool spaceAroundDelimiters = true)` — convenience wrapper that opens file and calls `Write`
   - Implementation notes:
     - Parser is a simple line-by-line state machine: track current section, accumulate multiline values
     - Regex for section header: `^\[([^\]]+)\]\s*$`
     - Regex for key-value: `^([^=:]+?)\s*[=:]\s*(.*)$`
   - Acceptance: can parse and round-trip INI files with all features
   - Commit: `feat(stdlib): implement configparser read/write operations`

### Phase 3: configparser Module — Project, Spy Stub, Tests

**Goal:** Wire up the module and add comprehensive tests.

#### Tasks

7. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Configparser.csproj`
   - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
   - Set `<AssemblyName>Sharpy.Stdlib.Configparser</AssemblyName>`
   - Set `<Compile Include="../Configparser/**/*.cs" />`
   - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Configparser.csproj` succeeds
   - Commit: `build(stdlib): add Sharpy.Stdlib.Configparser project file`

8. **Create spy stub file** — `src/Sharpy.Stdlib/spy/configparser_module.spy`
   - Write Sharpy source defining the module-level type exports:
     - `ConfigParser` class with constructor, section/option access methods
     - `SectionProxy` class
     - `BasicInterpolation`, `ExtendedInterpolation` classes
     - Exception classes: `ConfigparserError`, `NoSectionError`, `NoOptionError`, `DuplicateSectionError`, `DuplicateOptionError`, `ParsingError`, `MissingSectionHeaderError`, `InterpolationError`
   - Acceptance: file defines all type and function signatures with correct types
   - Commit: `feat(stdlib): add configparser module spy source`

9. **Add configparser tests** — `src/Sharpy.Stdlib.Tests/ConfigparserTests.cs`
   - Test `ReadString` and `Get`:
     - Basic key-value: `"[section]\nkey = value"` → `Get("section", "key")` returns `"value"`
     - Colon delimiter: `"[section]\nkey : value"` → `Get("section", "key")` returns `"value"`
     - No spaces: `"[section]\nkey=value"` → `Get("section", "key")` returns `"value"`
     - Multiline value: `"[section]\nkey = line1\n  line2"` → `Get("section", "key")` returns `"line1\nline2"`
     - Comments: `"[section]\n# comment\nkey = value"` → only `key` exists
     - Semicolon comments: `"[section]\n; comment\nkey = value"` → only `key` exists
     - Multiple sections: parse and access each independently
     - Empty value: `"[section]\nkey ="` → `Get("section", "key")` returns `""`
     - Whitespace around section name: `"[ section ]"` → section name is `" section "` (whitespace preserved, matching Python) [CORRECTED: Python preserves whitespace inside brackets]
   - Test DEFAULT fallback:
     - `"[DEFAULT]\nfallback = yes\n[section]\nkey = value"` → `Get("section", "fallback")` returns `"yes"`
     - Section value overrides DEFAULT: `"[DEFAULT]\nkey = default\n[section]\nkey = override"` → returns `"override"`
     - `Defaults()` returns DEFAULT section contents
   - Test case insensitivity:
     - `Set("section", "MyKey", "val")` → `Get("section", "mykey")` returns `"val"`
     - Section names remain case-sensitive
   - Test BasicInterpolation:
     - `"[section]\nbase = /opt\npath = %(base)s/bin"` → `Get("section", "path")` returns `"/opt/bin"`
     - Recursive interpolation: `"[section]\na = 1\nb = %(a)s2\nc = %(b)s3"` → `c` is `"123"`
     - Interpolation from DEFAULT: `"[DEFAULT]\nroot = /\n[section]\npath = %(root)setc"` → `"/etc"`
     - Circular interpolation: throw `InterpolationError`
   - Test ExtendedInterpolation:
     - Cross-section: `"[paths]\nhome = /opt\n[build]\npath = ${paths:home}/bin"` → `"/opt/bin"`
     - Same section: `"[section]\na = 1\nb = ${a}2"` → `"12"`
   - Test `raw=True`:
     - Returns raw value without interpolation: `%(key)s` is literal
   - Test typed getters:
     - `GetInt`: `"42"` → `42`, `"notint"` → throws `ValueError`
     - `GetFloat`: `"3.14"` → `3.14`
     - `GetBoolean`: `"yes"` → `true`, `"no"` → `false`, `"1"` → `true`, `"0"` → `false`, `"true"` → `true`, `"false"` → `false`, `"on"` → `true`, `"off"` → `false`, `"maybe"` → throws `ValueError`
   - Test section management:
     - `AddSection` + `HasSection`
     - `AddSection` duplicate → throws `DuplicateSectionError`
     - `AddSection("DEFAULT")` → throws `ValueError`
     - `RemoveSection` + verify removed
     - `RemoveOption` + verify removed
   - Test `Write`:
     - Round-trip: ReadString → Write → ReadString → values match
     - Space around delimiters option
     - DEFAULT section written first
   - Test `Read` from file:
     - Write temp INI file, read it, verify values (use `Path.GetTempFileName()`)
   - Test dict-like access:
     - `config["section"]["key"]` returns value
     - `config["nosection"]` throws `KeyNotFoundException`
   - Test error cases:
     - `Get` on nonexistent section → `NoSectionError`
     - `Get` on nonexistent option (no fallback) → `NoOptionError`
     - `Set` on nonexistent section → `NoSectionError`
     - Data before section header → `MissingSectionHeaderError`
     - `Get` with fallback on missing key → returns fallback
   - Acceptance: all tests pass
   - Commit: `test(stdlib): add configparser module tests`

### Phase 4: ipaddress Module — Address Types

**Goal:** Implement IPv4Address and IPv6Address with all properties and operations.

#### Tasks

10. **Create ipaddress module directory and registration** — `src/Sharpy.Stdlib/Ipaddress/__Init__.cs`
    - Create `Ipaddress/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("ipaddress")]` on `public static partial class IpaddressModule`
    - Acceptance: `IpaddressModule` class compiles with `[SharpyModule]` attribute
    - Commit: `feat(stdlib): scaffold ipaddress module registration`

11. **Implement IPv4Address** — `src/Sharpy.Stdlib/Ipaddress/IPv4Address.cs`
    - Create `[SharpyModuleType("ipaddress", "IPv4Address")]` sealed class:
      - Internal: wraps `System.Net.IPAddress`
      - Constructor: `IPv4Address(string address)` — parse via `IPAddress.Parse`, throw `ValueError` if not valid IPv4
      - Constructor: `IPv4Address(long address)` — create from integer (0–4294967295)
      - Constructor: `IPv4Address(Bytes packed)` — create from 4 bytes
      - Properties:
        - `int Version` → `4`
        - `int MaxPrefixlen` → `32`
        - `bool IsPrivate` — check against RFC 1918 ranges (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16)
        - `bool IsLoopback` — 127.0.0.0/8
        - `bool IsMulticast` — 224.0.0.0/4
        - `bool IsReserved` — 240.0.0.0/4
        - `bool IsLinkLocal` — 169.254.0.0/16
        - `bool IsGlobal` — not private, not loopback, not link-local, not multicast, not reserved, not unspecified
        - `bool IsUnspecified` — 0.0.0.0
        - `Bytes Packed` → 4 bytes big-endian
        - `string Compressed` → same as `ToString()` for IPv4
      - Methods:
        - `override string ToString()` → standard dotted-decimal notation
        - `override bool Equals(object? obj)` and `override int GetHashCode()` — value equality
        - `long ToInt()` — convert to integer
      - Operators:
        - `operator +` (IPv4Address + int → IPv4Address)
        - `operator -` (IPv4Address - int → IPv4Address)
        - `operator <`, `>`, `<=`, `>=` — comparison by integer value
        - `operator ==`, `!=` — value equality
      - Implementation notes:
        - Store address as `uint` internally for arithmetic. Convert to/from `IPAddress` for string parsing and property checks.
        - Use `IPAddress.IsLoopback` static method where available, otherwise check manually.
        - Python verified behavior: `ip_address("192.168.1.1").is_private` → `True`, `ip_address("127.0.0.1").is_loopback` → `True`
    - Acceptance: IPv4Address compiles with all properties, operators, and methods
    - Commit: `feat(stdlib): implement ipaddress IPv4Address`

12. **Implement IPv6Address** — `src/Sharpy.Stdlib/Ipaddress/IPv6Address.cs`
    - Create `[SharpyModuleType("ipaddress", "IPv6Address")]` sealed class:
      - Internal: wraps `System.Net.IPAddress`
      - Constructor: `IPv6Address(string address)` — parse via `IPAddress.Parse`, throw `ValueError` if not valid IPv6
      - Constructor: `IPv6Address(System.Numerics.BigInteger address)` — create from integer
      - Constructor: `IPv6Address(Bytes packed)` — create from 16 bytes
      - Properties (same as IPv4Address but for IPv6 ranges):
        - `int Version` → `6`
        - `int MaxPrefixlen` → `128`
        - `bool IsPrivate`, `IsLoopback` (::1), `IsMulticast` (ff00::/8), `IsReserved`, `IsLinkLocal` (fe80::/10), `IsGlobal`, `IsUnspecified` (::)
        - `bool IsSiteLocal` — fec0::/10 (deprecated but in Python API)
        - `Bytes Packed` → 16 bytes
        - `string Compressed` → compressed form (e.g., `::1`)
        - `string Exploded` → full expanded form (e.g., `0000:0000:0000:0000:0000:0000:0000:0001`)
        - `IPv4Address? Ipv4Mapped` → extract IPv4 from ::ffff:x.x.x.x, or null
      - Methods: `ToString()`, `Equals()`, `GetHashCode()`, `ToInt()` (returns `BigInteger`)
      - Operators: `+`, `-`, `<`, `>`, `<=`, `>=`, `==`, `!=`
      - Implementation notes:
        - Use `System.Numerics.BigInteger` for arithmetic (128-bit addresses overflow long)
        - `#if NET10_0_OR_GREATER` for any APIs not available on netstandard2.1
    - Acceptance: IPv6Address compiles with all properties and operations
    - Commit: `feat(stdlib): implement ipaddress IPv6Address`

### Phase 5: ipaddress Module — Network and Interface Types

**Goal:** Implement network and interface types, plus module-level factory functions.

#### Tasks

13. **Implement IPv4Network** — `src/Sharpy.Stdlib/Ipaddress/IPv4Network.cs`
    - Create `[SharpyModuleType("ipaddress", "IPv4Network")]` sealed class implementing `IEnumerable<IPv4Address>`:
      - Constructor: `IPv4Network(string address, bool strict = true)`:
        - Parse `"192.168.1.0/24"` format
        - If `strict=true` and host bits are set, throw `ValueError("{address} has host bits set")`
        - If `strict=false`, mask off host bits
        - Also accept `"192.168.1.0"` (no prefix → /32)
      - Properties:
        - `IPv4Address NetworkAddress` — first address in network
        - `IPv4Address BroadcastAddress` — last address in network
        - `IPv4Address Netmask` — e.g., `255.255.255.0`
        - `IPv4Address Hostmask` — inverse of netmask
        - `int Prefixlen` — CIDR prefix length
        - `long NumAddresses` — total addresses in network (2^(32-prefixlen)); must be `long` because /0 = 4294967296 which overflows `int` [CORRECTED: `int` → `long` to avoid overflow for /0 network]
        - `bool IsPrivate`, `IsLoopback`, `IsMulticast`, `IsReserved`, `IsLinkLocal`, `IsGlobal`
        - `string WithPrefixlen` → `"192.168.1.0/24"`
        - `string WithNetmask` → `"192.168.1.0/255.255.255.0"`
        - `string WithHostmask` → `"192.168.1.0/0.0.0.255"`
      - Methods:
        - `IEnumerable<IPv4Address> Hosts()` — all usable host addresses. For prefixes <= /30: excludes network and broadcast addresses. For /31 (RFC 3021 point-to-point): includes all 2 addresses. For /32 (host route): includes the single address. [CORRECTED: /31 and /32 are special cases that include all addresses, matching Python]
        - `IEnumerator<IPv4Address> GetEnumerator()` — iterate all addresses (including network and broadcast)
        - `bool Contains(IPv4Address address)` — membership test (used as `address in network` via `__contains__`)
        - `bool Overlaps(IPv4Network other)` — check if networks share any addresses
        - `List<IPv4Network> Subnets(int prefixlenDiff = 1, int? newPrefix = null)` — split into subnets
        - `IPv4Network Supernet(int prefixlenDiff = 1, int? newPrefix = null)` — parent network
        - `bool SubnetOf(IPv4Network other)` — is this a subnet of other?
        - `bool SupernetOf(IPv4Network other)` — is this a supernet of other?
        - `override string ToString()` → CIDR notation
        - `Equals()`, `GetHashCode()` — value equality on network address + prefixlen
      - Operators: `<`, `>`, `<=`, `>=`, `==`, `!=` — compare by (network_address, prefixlen)
    - Acceptance: IPv4Network compiles with full property and method set
    - Commit: `feat(stdlib): implement ipaddress IPv4Network`

14. **Implement IPv6Network** — `src/Sharpy.Stdlib/Ipaddress/IPv6Network.cs`
    - Create `[SharpyModuleType("ipaddress", "IPv6Network")]` sealed class implementing `IEnumerable<IPv6Address>`:
      - Same API surface as IPv4Network but for IPv6 (128-bit prefixes, BigInteger arithmetic)
      - Key differences:
        - `MaxPrefixlen` is 128
        - `NumAddresses` returns `BigInteger` (can be astronomically large)
        - `Hosts()` excludes only the Subnet-Router anycast address (first address) for prefixes < /127; for /127 and /128, include all
        - No broadcast address concept in IPv6 (BroadcastAddress still provided for API compatibility, returns last address)
      - Implementation notes:
        - On netstandard2.1: manual mask computation using `BigInteger`
        - On net10.0: can optionally use `System.Net.IPNetwork` for validation but still maintain custom implementation for consistency
    - Acceptance: IPv6Network compiles with IPv6-specific behavior
    - Commit: `feat(stdlib): implement ipaddress IPv6Network`

15. **Implement IPv4Interface and IPv6Interface** — `src/Sharpy.Stdlib/Ipaddress/Interfaces.cs`
    - `[SharpyModuleType("ipaddress", "IPv4Interface")]` sealed class:
      - Constructor: `IPv4Interface(string address)` — parse `"192.168.1.1/24"` format
      - Properties:
        - `IPv4Address Ip` — the host address (with host bits preserved)
        - `IPv4Network Network` — the associated network (host bits masked)
        - Inherits all address properties from the `Ip` address
        - `string WithPrefixlen`, `WithNetmask`, `WithHostmask` — format with the interface address (not network address)
      - `ToString()` → `"192.168.1.1/24"`
    - `[SharpyModuleType("ipaddress", "IPv6Interface")]` sealed class:
      - Same pattern for IPv6
    - Acceptance: both interface types compile and provide address + network access
    - Commit: `feat(stdlib): implement ipaddress IPv4Interface and IPv6Interface`

16. **Implement module-level factory functions** — `src/Sharpy.Stdlib/Ipaddress/IpaddressFunctions.cs`
    - Add to `public static partial class IpaddressModule`:
      - `object IpAddress(string address)` — return `IPv4Address` or `IPv6Address` based on input. Throw `ValueError("'{address}' does not appear to be an IPv4 or IPv6 address")` on invalid input. Return type is `object` because Sharpy union types are v0.2.x — callers use pattern matching.
      - `object IpNetwork(string address, bool strict = true)` — return `IPv4Network` or `IPv6Network`
      - `object IpInterface(string address)` — return `IPv4Interface` or `IPv6Interface`
      - `List<object> CollapseAddresses(List<object> addresses)` — merge adjacent/overlapping networks into minimal set. Input is list of IPv4Network or IPv6Network (must be all same version). Matches Python's `ipaddress.collapse_addresses()`.
      - `List<object> SummarizeAddressRange(object first, object last)` — compute minimal set of networks spanning the range. `first` and `last` must be same type (both IPv4Address or both IPv6Address).
    - Implementation notes:
      - Auto-detection: try `IPAddress.Parse`, check `AddressFamily` to decide IPv4 vs IPv6
      - For network input: split on `/`, parse the address portion to detect version
    - Acceptance: factory functions auto-detect version and return correct types
    - Commit: `feat(stdlib): implement ipaddress module-level functions`

### Phase 6: ipaddress Module — Project, Spy Stub, Tests

**Goal:** Wire up the module and add comprehensive tests.

#### Tasks

17. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Ipaddress.csproj`
    - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
    - Set `<AssemblyName>Sharpy.Stdlib.Ipaddress</AssemblyName>`
    - Set `<Compile Include="../Ipaddress/**/*.cs" />`
    - Acceptance: `dotnet build src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Ipaddress.csproj` succeeds
    - Commit: `build(stdlib): add Sharpy.Stdlib.Ipaddress project file`

18. **Create spy stub file** — `src/Sharpy.Stdlib/spy/ipaddress_module.spy`
    - Write Sharpy source defining the module-level function signatures and type exports:
      - Types: `IPv4Address`, `IPv6Address`, `IPv4Network`, `IPv6Network`, `IPv4Interface`, `IPv6Interface`
      - Functions: `ip_address`, `ip_network`, `ip_interface`, `collapse_addresses`, `summarize_address_range`
    - Acceptance: file defines all signatures with correct types
    - Commit: `feat(stdlib): add ipaddress module spy source`

19. **Add ipaddress tests** — `src/Sharpy.Stdlib.Tests/IpaddressTests.cs`
    - Test `IPv4Address`:
      - Construction from string: `"192.168.1.1"` → version 4, correct string representation
      - Construction from int: `3232235777` → `"192.168.1.1"`
      - Construction from bytes: `[192, 168, 1, 1]` → correct address
      - Invalid address: `"invalid"` → throws `ValueError`
      - IPv6 address to IPv4 constructor → throws `ValueError`
      - Properties: `IsPrivate` (192.168.x.x → true, 8.8.8.8 → false), `IsLoopback` (127.0.0.1 → true), `IsMulticast` (224.0.0.1 → true), `IsReserved` (240.0.0.1 → true), `IsLinkLocal` (169.254.1.1 → true), `IsGlobal` (8.8.8.8 → true), `IsUnspecified` (0.0.0.0 → true)
      - `Packed` returns 4 bytes big-endian
      - `ToInt()` returns correct integer
      - Arithmetic: `ip + 1` gives next address, `ip - 1` gives previous
      - Comparison: `ip("1.0.0.0") < ip("2.0.0.0")` → true
      - Equality: `ip("1.1.1.1") == ip("1.1.1.1")` → true
      - Overflow: `ip("255.255.255.255") + 1` → throws `ValueError` (Python throws `AddressValueError`, a subclass of `ValueError`) [CORRECTED: verified — Python throws, does not wrap]
    - Test `IPv6Address`:
      - Construction: `"::1"`, `"2001:db8::1"`, `"fe80::1"`
      - Properties: `IsLoopback` (::1), `IsLinkLocal` (fe80::), `IsMulticast` (ff00::)
      - `Compressed` vs `Exploded` formatting
      - `Ipv4Mapped`: `"::ffff:192.168.1.1"` → returns `IPv4Address("192.168.1.1")`
      - Arithmetic and comparison (same pattern as IPv4)
    - Test `IPv4Network`:
      - Construction: `"192.168.1.0/24"` — NetworkAddress, BroadcastAddress, Netmask, Prefixlen
      - `NumAddresses`: `/24` → 256, `/32` → 1, `/0` → 4294967296
      - `Hosts()`: `/24` → 254 hosts, `/31` → 2 hosts, `/32` → 1 host
      - Iteration: iterates all addresses including network and broadcast
      - `Contains`: `"192.168.1.5" in "192.168.1.0/24"` → true, `"10.0.0.1"` → false
      - `Overlaps`: `"192.168.1.0/24"` overlaps `"192.168.1.128/25"` → true
      - `Subnets(prefixlen_diff=1)` → two /25 networks
      - `Supernet()` → /23 network
      - `SubnetOf` / `SupernetOf`
      - Strict mode: `"192.168.1.1/24"` with `strict=true` → throws `ValueError("192.168.1.1/24 has host bits set")`
      - Strict mode: `"192.168.1.1/24"` with `strict=false` → creates `192.168.1.0/24`
      - Single address (no prefix): `"192.168.1.1"` → `/32` network
    - Test `IPv6Network`:
      - Basic construction and properties (parallel to IPv4Network)
      - Large NumAddresses (BigInteger)
    - Test `IPv4Interface` / `IPv6Interface`:
      - `"192.168.1.1/24"` → `Ip` is `192.168.1.1`, `Network` is `192.168.1.0/24`
    - Test factory functions:
      - `IpAddress("192.168.1.1")` → `IPv4Address`
      - `IpAddress("::1")` → `IPv6Address`
      - `IpAddress("invalid")` → throws `ValueError`
      - `IpNetwork("192.168.1.0/24")` → `IPv4Network`
      - `IpInterface("192.168.1.1/24")` → `IPv4Interface`
    - Test `CollapseAddresses`:
      - `["192.168.1.0/25", "192.168.1.128/25"]` → `["192.168.1.0/24"]`
    - Test `SummarizeAddressRange`:
      - `("192.168.1.0", "192.168.1.255")` → `["192.168.1.0/24"]`
    - Acceptance: all tests pass
    - Commit: `test(stdlib): add ipaddress module tests`

### Phase 7: Documentation

**Goal:** Add batch plan doc for reference.

#### Tasks

20. **Add Batch 6 plan to docs** — `docs/stdlib/batch6-plan.md`
    - Save this plan (cleaned up) as the batch plan document in the docs directory
    - Follow the same format as existing batch plans
    - Acceptance: document exists with correct content
    - Commit: `docs(stdlib): add Batch 6 implementation plan for configparser, ipaddress`

## Testing Strategy

### New test fixtures needed

- `src/Sharpy.Stdlib.Tests/ConfigparserTests.cs` — ~35 tests covering read/write, interpolation, DEFAULT, typed getters, error handling
- `src/Sharpy.Stdlib.Tests/IpaddressTests.cs` — ~45 tests covering address types, network types, interfaces, factory functions, arithmetic, membership

### Edge cases to cover

**configparser:**
- Empty INI file → no sections, no errors
- Section with no keys
- Whitespace-only values
- Values with `=` or `:` in them (only first delimiter splits key from value)
- Unicode in keys and values
- Very deeply nested interpolation (depth limit of 10)
- Multiline values with mixed indentation (tabs and spaces)
- Inline comments disabled by default (value `"foo # bar"` includes `# bar`)
- `allow_no_value=True` with keys that have no `=`

**ipaddress:**
- Boundary addresses: `0.0.0.0`, `255.255.255.255`, `::`, `::1`, `ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff`
- `/0` network (entire address space)
- `/32` and `/128` (single host networks)
- `/31` networks (point-to-point links — special handling in Python)
- IPv4-mapped IPv6 addresses (`::ffff:192.168.1.1`)
- Mixed version errors (e.g., collapse_addresses with mixed IPv4 and IPv6)
- Large IPv6 integer values (BigInteger edge cases)

### Negative test cases

**configparser:**
- `Get` on nonexistent section → `NoSectionError`
- `Get` on nonexistent option (no fallback) → `NoOptionError`
- `AddSection` with duplicate name → `DuplicateSectionError`
- `AddSection("DEFAULT")` → `ValueError`
- Data before first section header → `MissingSectionHeaderError`
- Circular interpolation → `InterpolationError`
- `GetBoolean` with invalid value → `ValueError`
- `GetInt` with non-numeric value → `ValueError`

**ipaddress:**
- Invalid address string → `ValueError`
- Network with host bits set (strict mode) → `ValueError`
- Address arithmetic overflow
- Mixed version in `CollapseAddresses` → `TypeError`
- Mixed version in `SummarizeAddressRange` → `TypeError`

## Issues to Close

- #744 — configparser module (closed by Phase 2, Task 5 — full ConfigParser implementation)
- #748 — ipaddress module (closed by Phase 5, Task 16 — full module with factory functions)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-05-29
**Plan file:** ~/.claude/plans/plan-5c8a3f.md

### Corrections Made

1. **ConfigparserError Python name** (Task 2): Python's base class is `configparser.Error`, not `configparser.ConfigparserError`. Added note to use `[SharpyModuleType("configparser", "Error")]` so the Python-facing name matches.

2. **Section header whitespace** (Task 6, Task 9): Plan said "strip whitespace inside brackets" but Python preserves whitespace — `[ section ]` → section name ` section `. Corrected both the parser description and the test assertion.

3. **NumAddresses type** (Task 13): Changed `int` to `long`. A `/0` network has 4,294,967,296 addresses which overflows `int` (max 2,147,483,647).

4. **IPv4Network.hosts() for /31 and /32** (Task 13): Plan said "excludes network and broadcast for /31 and larger" but Python includes all addresses for /31 (RFC 3021 point-to-point) and /32 (host route). Only prefixes <= /30 exclude network and broadcast.

5. **IPv4 overflow behavior** (Task 19): Plan said "throws (or wraps — verify Python behavior)". Verified: Python throws `AddressValueError` (subclass of `ValueError`).

### Warnings

1. **InterpolationDepthError not modeled**: Python has `InterpolationDepthError`, `InterpolationMissingOptionError`, and `InterpolationSyntaxError` as subclasses of `InterpolationError`. Plan only models `InterpolationError`. Acceptable v1 simplification, but implementers should be aware.

2. **Factory function return types**: `collapse_addresses` and `summarize_address_range` return generators in Python. Plan returns `List<object>`, which is a reasonable C# adaptation (generators → materialized lists is standard for Sharpy stdlib).

3. **SharpyModuleType two-arg form**: Plan uses `[SharpyModuleType("configparser", "...")]` throughout. Existing stdlib modules (Toml, Hashlib, Unittest) use the single-arg form when the C# class name matches the Python name. The two-arg form is only needed when they differ (e.g., `ConfigparserError` → Python name `Error`). For types like `NoSectionError` where C# name = Python name, single-arg `[SharpyModuleType("configparser")]` is sufficient and more consistent with existing patterns.

4. **IPv6Network.hosts() description**: Plan says "excludes only the Subnet-Router anycast address (first address)" — verified correct for regular prefixes. For /126: 4 total, 3 hosts (excludes first). For /127 and /128: includes all. Description matches Python behavior.

### Missing Steps Added

None — the plan covers all required phases (module scaffolding, implementation, project files, spy stubs, tests, docs).

### Unchecked Claims

1. **GitHub issues #744 and #748**: Not verified against GitHub (would require API call). Assumed correct based on roadmap cross-reference.
2. **`System.Net.IPNetwork` availability on .NET 8+**: Claimed but not verified against .NET API docs. Known to be accurate per .NET 8 release notes.
