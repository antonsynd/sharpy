<!-- Verified by /verify-plan on 2026-05-29 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Stdlib Batch 1: uuid, base64, secrets, hmac

## Context

Implement the four "quick win" stdlib modules from the [Tier 1 roadmap](docs/stdlib/roadmap.md) Batch 1. These are all small (<200 lines each), thin wrappers over .NET BCL types with no NuGet dependencies required.

**GitHub issues:**
- [#733](https://github.com/antonsynd/sharpy/issues/733) — uuid module (UUID generation and parsing)
- [#734](https://github.com/antonsynd/sharpy/issues/734) — base64 module (base16/32/64/85 encoding)
- [#741](https://github.com/antonsynd/sharpy/issues/741) — hmac module (keyed-hash message authentication)
- secrets module — **needs issue creation** (no existing issue; roadmap incorrectly listed #738 which is a duplicate hmac issue)

## Current State

- **33 stdlib modules** exist in `src/Sharpy.Stdlib/` [CORRECTED: was 31; Toml and Yaml were added recently]
- None of these four modules exist yet
- `hashlib` module is already implemented (hmac depends on it for algorithm names and `HashObject` pattern)
- `Bytes` type exists in `Sharpy.Core/Bytes.cs` — all four modules operate on bytes
- Module infrastructure is mature: `[SharpyModule]`/`[SharpyModuleType]` attributes, `ModuleRegistry` discovery, `.spy` source files, per-module `.csproj` files

## Design Decisions

1. **All modules are hand-written C#** (not `.spy`-generated). Rationale: these modules wrap .NET cryptographic/encoding APIs that require `using System.Security.Cryptography` and byte-level manipulation — the Sharpy compiler can't express this. Follow the pattern of `Hashlib` where `__Init__.cs` + hand-written classes coexist with a `.spy` file that only defines the module-level function signatures.

2. **`Bytes` for all binary I/O** (Axiom 1 — .NET compatibility). Python's `bytes` maps to `Sharpy.Bytes`. All encode/decode functions accept and return `Bytes`, matching Python semantics while wrapping .NET `byte[]` internally.

3. **hmac reuses hashlib's algorithm names** but does NOT depend on `HashObject` directly. HMAC has its own `HmacObject` class wrapping `System.Security.Cryptography.HMAC*` (separate from `HashAlgorithm`). The algorithm name strings ("sha256", etc.) are consistent across both modules.

4. **uuid4 only uses `Guid.NewGuid()`**. UUID1 (time-based) uses `Guid.CreateVersion7()` on .NET 10 (closest available — true UUIDv1 with MAC address is not available in .NET). UUID3/5 use custom byte-level MD5/SHA1 hashing per RFC 4122. This is the standard approach used by all .NET UUID libraries.

5. **secrets uses `RandomNumberGenerator`** (not `RNGCryptoServiceProvider` which is obsolete in .NET 10). `RandomNumberGenerator.GetBytes()` is the modern, thread-safe CSPRNG API.

6. **No new NuGet dependencies.** All four modules use only BCL types from `System.Security.Cryptography`, `System.Convert`, and `System`.

7. **C# 9.0 compatibility** for `netstandard2.1` target. No file-scoped namespaces, no record structs, no global usings. Use `#if NET10_0_OR_GREATER` for .NET 10-only APIs (e.g., `Guid.CreateVersion7`).

## Implementation

Module implementation order: secrets (simplest, no dependencies) → base64 (standalone) → uuid (standalone, medium complexity) → hmac (depends on hashlib pattern familiarity). Each module follows the same 5-step pattern.

### Phase 1: secrets Module

**Goal:** Implement `secrets` — cryptographically secure random generation. Simplest of the four (~80 lines).

#### Tasks

1. **Create secrets module directory and registration** — `src/Sharpy.Stdlib/Secrets/__Init__.cs`
   - Create `Secrets/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("secrets")]` on `public static partial class SecretsModule`
   - Follow exact pattern from `src/Sharpy.Stdlib/Hashlib/__Init__.cs`
   - Commit: `feat(stdlib): scaffold secrets module registration`

2. **Implement secrets module functions** — `src/Sharpy.Stdlib/Secrets/Secrets.cs`
   - Implement as `public static partial class SecretsModule`:
     - `TokenBytes(int nbytes = 32)` → returns `Bytes` (wraps `RandomNumberGenerator.GetBytes`)
     - `TokenHex(int nbytes = 32)` → returns hex string of random bytes
     - `TokenUrlsafe(int nbytes = 32)` → returns URL-safe base64 string of random bytes
     - `Randbelow(int exclusiveUpperBound)` → returns random int in `[0, n)` using `RandomNumberGenerator.GetInt32`
     - `Choice<T>(List<T> sequence)` → returns random element using secure random index
     - `CompareDigest(string a, string b)` → constant-time string comparison
     - `CompareDigest(Bytes a, Bytes b)` → constant-time bytes comparison (overload)
   - Use `RandomNumberGenerator` (static methods, thread-safe, no instance needed)
   - `CompareDigest` uses `CryptographicOperations.FixedTimeEquals` on .NET 10, manual constant-time loop on `netstandard2.1`
   - Commit: `feat(stdlib): implement secrets module functions`

3. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Secrets.csproj`
   - Copy pattern from `Sharpy.Stdlib.Hashlib.csproj`
   - Set `<AssemblyName>Sharpy.Stdlib.Secrets</AssemblyName>`
   - Set `<Compile Include="../Secrets/**/*.cs" />`
   - Commit: `build(stdlib): add Sharpy.Stdlib.Secrets project file`

4. **Create spy stub file** — `src/Sharpy.Stdlib/spy/secrets_module.spy`
   - Write Sharpy source defining the module-level function signatures
   - Functions: `token_bytes`, `token_hex`, `token_urlsafe`, `randbelow`, `choice`, `compare_digest`
   - This file is used for documentation and C# regeneration, not for compilation
   - Commit: `feat(stdlib): add secrets module spy source`

5. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
   - `stdlib_secrets.spy` + `stdlib_secrets.expected` — test `token_hex` length, `randbelow` bounds, `compare_digest` results
   - `stdlib_from_secrets.spy` + `stdlib_from_secrets.expected` — test `from secrets import token_hex, compare_digest`
   - Tests must be deterministic: test output lengths and boolean results, not random values
   - Acceptance: `dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"` passes with new fixtures
   - Commit: `test(stdlib): add secrets module integration tests`

### Phase 2: base64 Module

**Goal:** Implement `base64` — binary-to-text encoding (base16/32/64/85). ~150 lines.

#### Tasks

6. **Create base64 module directory and registration** — `src/Sharpy.Stdlib/Base64/__Init__.cs`
   - Create `Base64/` directory under `src/Sharpy.Stdlib/`
   - Add `__Init__.cs` with `[SharpyModule("base64")]` on `public static partial class Base64Module`
   - Commit: `feat(stdlib): scaffold base64 module registration`

7. **Implement base64 module functions** — `src/Sharpy.Stdlib/Base64/Base64Module.cs`
   - Implement as `public static partial class Base64Module`:
     - `B64encode(Bytes s)` → `Bytes` — standard base64 encoding via `Convert.ToBase64String`
     - `B64decode(Bytes s)` → `Bytes` — standard base64 decoding via `Convert.FromBase64String`
     - `UrlsafeB64encode(Bytes s)` → `Bytes` — URL-safe base64 (replace `+/` with `-_`)
     - `UrlsafeB64decode(Bytes s)` → `Bytes` — URL-safe base64 decode
     - `B32encode(Bytes s)` → `Bytes` — Base32 encoding (custom implementation, no .NET built-in)
     - `B32decode(Bytes s)` → `Bytes` — Base32 decoding
     - `B16encode(Bytes s)` → `Bytes` — Base16/hex encoding (via `BitConverter` or manual)
     - `B16decode(Bytes s)` → `Bytes` — Base16/hex decoding
     - `B85encode(Bytes b)` → `Bytes` — Base85 (RFC 1924) encoding (custom implementation)
     - `B85decode(Bytes b)` → `Bytes` — Base85 decoding
     - `A85encode(Bytes b)` → `Bytes` — Ascii85 encoding (custom implementation)
     - `A85decode(Bytes b)` → `Bytes` — Ascii85 decoding
   - Also accept `string` overloads for `B64decode`/`UrlsafeB64decode` (Python accepts both `bytes` and `str`)
   - Use `System.Convert` for base64, custom implementations for base32/85/ascii85
   - Raise `ValueError` for invalid input (matching Python's `binascii.Error`)
   - Commit: `feat(stdlib): implement base64 module functions`

8. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Base64.csproj`
   - Same pattern as Hashlib csproj
   - Commit: `build(stdlib): add Sharpy.Stdlib.Base64 project file`

9. **Create spy stub file** — `src/Sharpy.Stdlib/spy/base64_module.spy`
   - Function signatures for all encode/decode functions
   - Commit: `feat(stdlib): add base64 module spy source`

10. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
    - `stdlib_base64.spy` + `stdlib_base64.expected` — test b64, urlsafe, b32, b16 encode/decode roundtrips
    - `stdlib_from_base64.spy` + `stdlib_from_base64.expected` — test from-import
    - Test known values: `b64encode(b"hello world")` → `b"aGVsbG8gd29ybGQ="`
    - Acceptance: all test fixtures pass
    - Commit: `test(stdlib): add base64 module integration tests`

### Phase 3: uuid Module

**Goal:** Implement `uuid` — UUID generation and parsing. ~200 lines (medium — custom UUID3/5 logic).

#### Tasks

11. **Create uuid module directory and registration** — `src/Sharpy.Stdlib/Uuid/__Init__.cs`
    - Create `Uuid/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("uuid")]` on `public static partial class UuidModule`
    - Commit: `feat(stdlib): scaffold uuid module registration`

12. **Implement UUID class** — `src/Sharpy.Stdlib/Uuid/UUID.cs`
    - Create `[SharpyModuleType("uuid")]` class `UUID` wrapping `System.Guid`:
      - Constructor `UUID(string hex)` — parse from string (various formats: with/without hyphens)
      - Internal constructor from `Guid` and from `byte[]` with version/variant override
      - Properties:
        - `Hex` → 32-char lowercase hex string (no hyphens)
        - `Int` → `long` representation (note: Python returns arbitrary-precision int, Sharpy uses long — document limitation)
        - `UuidBytes` → `Bytes` (16-byte representation) — named `UuidBytes` to avoid collision with C# `Bytes` type as parameter
        - `Version` → int (1, 3, 4, 5, or 0 for parsed UUIDs)
        - `Variant` → string ("specified in RFC 4122", "reserved for NCS compatibility", etc.)
        - `TimeLow`, `TimeMid`, `TimeHiVersion`, `ClockSeqHiVariant`, `ClockSeqLow`, `Node` → int fields per RFC 4122
      - `ToString()` → standard 8-4-4-4-12 hyphenated format
      - `Equals`, `GetHashCode` — delegate to internal `Guid`
      - `CompareTo` — for ordering
    - Commit: `feat(stdlib): implement UUID class`

13. **Implement uuid module functions** — `src/Sharpy.Stdlib/Uuid/UuidModule.cs`
    - Implement as `public static partial class UuidModule`:
      - `Uuid4()` → `UUID` — wraps `Guid.NewGuid()`
      - `Uuid1()` → `UUID` — time-based UUID. On .NET 10, use `Guid.CreateVersion7()` (UUIDv7 is the modern replacement for UUIDv1). On `netstandard2.1`, generate manually with timestamp + random node.
      - `Uuid3(UUID namespace_, string name)` → `UUID` — MD5-based UUID per RFC 4122 section 4.3
      - `Uuid5(UUID namespace_, string name)` → `UUID` — SHA1-based UUID per RFC 4122 section 4.3
    - Namespace constants as `public static readonly UUID` fields:
      - `NamespaceDns` = `UUID("6ba7b810-9dad-11d1-80b4-00c04fd430c8")`
      - `NamespaceUrl` = `UUID("6ba7b811-9dad-11d1-80b4-00c04fd430c8")`
      - `NamespaceOid` = `UUID("6ba7b812-9dad-11d1-80b4-00c04fd430c8")`
      - `NamespaceX500` = `UUID("6ba7b814-9dad-11d1-80b4-00c04fd430c8")`
    - Commit: `feat(stdlib): implement uuid module factory functions and constants`

14. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Uuid.csproj`
    - Same pattern as Hashlib csproj
    - Commit: `build(stdlib): add Sharpy.Stdlib.Uuid project file`

15. **Create spy stub file** — `src/Sharpy.Stdlib/spy/uuid_module.spy`
    - Define `UUID` class usage patterns and module-level functions
    - Include namespace constants: `NAMESPACE_DNS`, `NAMESPACE_URL`, `NAMESPACE_OID`, `NAMESPACE_X500`
    - Commit: `feat(stdlib): add uuid module spy source`

16. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
    - `stdlib_uuid.spy` + `stdlib_uuid.expected` — test uuid4 format, UUID parsing, uuid3/uuid5 with known namespace+name
    - `stdlib_from_uuid.spy` + `stdlib_from_uuid.expected` — test from-import
    - Test deterministic values:
      - `UUID("12345678-1234-5678-1234-567812345678")` string roundtrip
      - `uuid3(NAMESPACE_DNS, "example.com")` → `"9073926b-929f-31c2-abc9-fad77ae3e8eb"`
      - `uuid5(NAMESPACE_DNS, "example.com")` → `"cfbff0d1-9375-5685-968c-48ce8b15ae17"`
      - uuid4 format validation (36 chars, correct hyphen positions)
    - Commit: `test(stdlib): add uuid module integration tests`

### Phase 4: hmac Module

**Goal:** Implement `hmac` — keyed-hash message authentication. ~120 lines. Depends on familiarity with hashlib patterns.

#### Tasks

17. **Create hmac module directory and registration** — `src/Sharpy.Stdlib/Hmac/__Init__.cs`
    - Create `Hmac/` directory under `src/Sharpy.Stdlib/`
    - Add `__Init__.cs` with `[SharpyModule("hmac")]` on `public static partial class HmacModule`
    - Commit: `feat(stdlib): scaffold hmac module registration`

18. **Implement HmacObject class** — `src/Sharpy.Stdlib/Hmac/HmacObject.cs`
    - Create `[SharpyModuleType("hmac")]` class `HmacObject`:
      - Constructor takes `byte[] key`, `string digestmod`, optional initial `byte[] msg`
      - Internal state: `HMAC` instance (kept open for incremental updates) + accumulated `List<byte>` for data
      - Properties:
        - `DigestSize` → int (hash output size in bytes)
        - `Name` → string (e.g., "hmac-sha256")
      - Methods:
        - `Update(string msg)` — append UTF-8 encoded data
        - `Update(Bytes msg)` — append raw bytes
        - `Hexdigest()` → string (hex-encoded HMAC)
        - `Digest()` → `List<int>` (raw bytes as ints, matching hashlib pattern)
        - `Copy()` → `HmacObject` (copy with same key + accumulated data)
    - Algorithm factory: map "sha256" → `new HMACSHA256(key)`, etc.
    - Support: md5, sha1, sha256, sha384, sha512 (same as hashlib)
    - Commit: `feat(stdlib): implement HmacObject class`

19. **Implement hmac module functions** — `src/Sharpy.Stdlib/Hmac/HmacModule.cs`
    - Implement as `public static partial class HmacModule`:
      - `New(Bytes key, Bytes? msg = null, string digestmod = "sha256")` → `HmacObject`
      - `New(string key, string? msg = null, string digestmod = "sha256")` → `HmacObject` (string overload)
      - `Digest(Bytes key, Bytes msg, string digest)` → `Bytes` — one-shot HMAC computation
      - `Digest(string key, string msg, string digest)` → `Bytes` — string overload
      - `CompareDigest(Bytes a, Bytes b)` → `bool` — constant-time comparison via `CryptographicOperations.FixedTimeEquals`
      - `CompareDigest(string a, string b)` → `bool` — string overload
    - Note: `New` is a C# keyword — the name mangler handles `new` → `New` since the Python function is `hmac.new()`. Verify this works with the existing name mangling infrastructure.
    - Commit: `feat(stdlib): implement hmac module functions`

20. **Create per-module project file** — `src/Sharpy.Stdlib/modules/Sharpy.Stdlib.Hmac.csproj`
    - Same pattern as Hashlib csproj
    - Commit: `build(stdlib): add Sharpy.Stdlib.Hmac project file`

21. **Create spy stub file** — `src/Sharpy.Stdlib/spy/hmac_module.spy`
    - Define `hmac.new()`, `hmac.digest()`, `hmac.compare_digest()` signatures
    - Commit: `feat(stdlib): add hmac module spy source`

22. **Add integration test fixtures** — `src/Sharpy.Stdlib.Tests/Integration/TestFixtures/`
    - `stdlib_hmac.spy` + `stdlib_hmac.expected` — test HMAC-SHA256 with known key/message, verify against Python's output:
      - `hmac.new(b"secret", b"message", "sha256").hexdigest()` → `"8b5f48702995c1598c573db1e21866a9b825d4a794d169d7060a03605796360b"`
    - `stdlib_from_hmac.spy` + `stdlib_from_hmac.expected` — test from-import pattern
    - Test `compare_digest` with matching and non-matching inputs
    - Test incremental `update()` produces same result as single-message constructor
    - Commit: `test(stdlib): add hmac module integration tests`

### Phase 5: Documentation and Cleanup

**Goal:** Update roadmap, create secrets issue, ensure all modules build in monolith.

#### Tasks

23. **Create GitHub issue for secrets module**
    - Create issue via `gh issue create` with title "feat(stdlib): add secrets module — cryptographically secure random" and label `enhancement,stdlib`
    - Follow the same format as #733/#734/#741
    - Commit: (no commit — GitHub issue only)

24. **Update roadmap** — `docs/stdlib/roadmap.md`
    - Fix issue references in Batch 1 table: #738 → secrets issue number, #739 → #734
    - Update "Current Modules" count from 31 to 37 and add `toml`, `yaml`, and all four new modules to the list [CORRECTED: current actual count is 33 (toml + yaml already exist), not 31; 33 + 4 = 37]
    - Update Batch 3 status: yaml (#731) is implemented, toml (#732) is implemented — mark as complete
    - Commit: `docs(stdlib): update roadmap for Batch 1 completion`

25. **Verify monolith build** — `src/Sharpy.Stdlib/Sharpy.Stdlib.csproj`
    - Run `dotnet build` and `dotnet test` to ensure all new modules compile and all tests pass
    - No csproj changes needed — new directories are auto-included by default SDK glob
    - Acceptance: zero warnings, zero test failures
    - Commit: (no commit — verification only)

## Testing Strategy

### Integration Test Fixtures (8 new files)
Each module gets two fixture pairs:
- `stdlib_{module}.spy` + `.expected` — tests `import {module}` usage
- `stdlib_from_{module}.spy` + `.expected` — tests `from {module} import ...` usage

### Deterministic Test Design
Since three of these modules involve randomness (secrets, uuid) or variable output (hmac with random keys), tests must use:
- **Known inputs** for encode/decode roundtrips (base64, hmac)
- **Format validation** for random outputs (uuid4 string length = 36, token_hex length = 2*nbytes)
- **Boolean assertions** for bounds checks (randbelow result >= 0 and < n)
- **Exact known outputs** for deterministic operations (uuid3/uuid5 with known namespace+name, hmac with fixed key)

### Edge Cases to Cover
- Empty input (empty bytes to encode, empty string to hash)
- Invalid input (malformed UUID string, invalid base64)
- Boundary values (randbelow(1) always returns 0, token_bytes(0) returns empty bytes)
- Algorithm name validation (unsupported algorithm → ValueError)

### Negative Test Cases
- `uuid.UUID("not-a-uuid")` → error
- `base64.b64decode("!!!invalid!!!")` → error
- `hmac.new(key, digestmod="unsupported")` → error
- `secrets.randbelow(0)` → error
- `secrets.choice([])` → error

## Issues to Close

- #733 — uuid module (closed by Phase 3, Tasks 11-16)
- #734 — base64 module (closed by Phase 2, Tasks 6-10)
- #741 — hmac module (closed by Phase 4, Tasks 17-22)
- (new issue) — secrets module (closed by Phase 1, Tasks 1-5)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-05-29
**Plan file:** `~/.claude/plans/plan-91286d.md`

### Corrections Made

1. **Module count** (Current State section): Changed "31 stdlib modules" → "33 stdlib modules". Toml and Yaml modules were recently added to `src/Sharpy.Stdlib/` (commits fc65907d, f16b5b59).

2. **Roadmap update count** (Task 24): Changed "from 31 to 35" → "from 31 to 37". The actual current stdlib directory count is 33 (not 31), so adding 4 new modules yields 37. Also added step to update Batch 3 status since yaml/toml are already implemented.

### Warnings

1. **HmacObject constructor signature** (Task 18): Constructor is specified as `byte[] key` but the Sharpy public API convention uses `Bytes` (the Sharpy wrapper type). Consider using `Bytes key` in the constructor and extracting the internal `byte[]` there, consistent with how module functions in Task 19 accept `Bytes`.

2. **CLAUDE.md module count**: The plan doesn't mention updating CLAUDE.md which also states "31 stdlib modules". After implementation, CLAUDE.md should be updated to reflect 37 modules.

3. **Roadmap Batch 3 staleness**: The roadmap's Batch 3 lists yaml (#731) and toml (#732) as future work, but both are already implemented. Task 24 should update Batch 3 to reflect completion.

### Verified Claims

| Claim | Status | Method |
|-------|--------|--------|
| Hashlib `__Init__.cs` + `[SharpyModule]` pattern | ✅ | Read `Hashlib/__Init__.cs` |
| `[SharpyModuleType]` attribute on `HashObject` | ✅ | Read `Hashlib/HashObject.cs` |
| Per-module csproj pattern (`<Compile Include>`) | ✅ | Read `modules/Sharpy.Stdlib.Hashlib.csproj` |
| `Bytes` type exists in `Sharpy.Core/Bytes.cs` | ✅ | Read file — `readonly partial struct Bytes` |
| Monolith auto-includes new directories | ✅ | Read `Sharpy.Stdlib.csproj` — default SDK glob |
| `new` keyword via PascalCase mangling | ✅ | `CSharpKeywords.All` includes `new`; mangler produces `New` |
| HMAC-SHA256 expected value | ✅ | `python3 -c "import hmac; ..."` matches `8b5f487...` |
| UUID3 expected value | ✅ | `python3 -c "import uuid; ..."` matches `9073926b-...` |
| UUID5 expected value | ✅ | `python3 -c "import uuid; ..."` matches `cfbff0d1-...` |
| base64 expected value | ✅ | `python3 -c "import base64; ..."` matches `aGVsbG8gd29ybGQ=` |
| Issue #733 (uuid) exists, OPEN | ✅ | `gh issue view 733` |
| Issue #734 (base64) exists, OPEN | ✅ | `gh issue view 734` |
| Issue #741 (hmac) exists, OPEN | ✅ | `gh issue view 741` |
| Issue #738 is duplicate hmac (not secrets) | ✅ | `gh issue view 738` — title "hmac", CLOSED |
| No secrets issue exists | ✅ | `gh issue list --search "secrets"` — empty |
| Spy stub file pattern | ✅ | `spy/hashlib_module.spy` exists |
| Stdlib test fixture path and naming | ✅ | `stdlib_hashlib.spy` + `.expected` exists in `Stdlib.Tests/Integration/TestFixtures/` |
| `Directory.Build.props` in modules/ | ✅ | C# 9.0/14 multi-target, `EnableDefaultCompileItems=false` |

### Missing Steps Added

- Task 24 should also update CLAUDE.md's stdlib module count (currently says "31 modules")
- Task 24 should mark roadmap Batch 3 (yaml/toml) as complete (both are already implemented)

### Unchecked Claims

- `Guid.CreateVersion7()` availability on .NET 10 — likely correct (.NET 9+ API) but not verified against the actual SDK. The plan's `#if NET10_0_OR_GREATER` guard is the correct approach regardless.
- `CryptographicOperations.FixedTimeEquals` availability on `netstandard2.1` — plan correctly proposes a manual fallback for this target.
