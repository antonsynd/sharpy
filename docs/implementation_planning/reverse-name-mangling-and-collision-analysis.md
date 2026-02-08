# Reverse Name Mangling & Collision Analysis

## Context

Sharpy's name mangling system has two directions:

1. **Forward (Sharpy → C#):** `snake_case` → `PascalCase` for generated code. Implemented in `NameMangler`, `NameFormDetector`, `DunderMapping`, `CodeGenInfoComputer`. Well-tested, context-aware via `NameContext`.

2. **Reverse (C# → Sharpy):** `PascalCase` → `snake_case` for .NET symbol discovery. Currently limited to `OverloadIndexBuilder.GetFunctionName()` for Sharpy.Core discovery only. Uses a single context-free regex.

This document analyzes edge cases, collisions, and gaps in both directions, and records design decisions for expanding the reverse direction to support general .NET interop.

## Current State

### Forward Direction (Sharpy → C#)

Context-dependent dispatch via `NameContext`:

| Context | Target | `get_user` | `HTTP` | `camelCase` | `PascalCase` |
|---------|--------|-----------|--------|-------------|--------------|
| Method/Function/Field | PascalCase | `GetUser` | `HTTP` (passthrough) | `camelCase` (passthrough) | `PascalCase` (passthrough) |
| Variable/Parameter | camelCase | `getUser` | `http` (fully lowered) | `camelCase` (passthrough) | `pascalCase` (first char lowered) |
| Constant | PascalCase | `GetUser` | `Http` (title-cased) | `camelCase` (passthrough) | `PascalCase` (passthrough) |
| Type/Interface | Preserved | `get_user` (!) | `HTTP` | `camelCase` | `PascalCase` |
| EnumMember | PascalCase | `GetUser` | `Http` | — | — |

### Reverse Direction (C# → Sharpy)

Single algorithm in `OverloadIndexBuilder.GetFunctionName()`, currently using 2 passes (to be expanded to 3):

```csharp
// Pass 1: Acronym boundaries (XMLParser → XML_Parser)
name = Regex.Replace(name, "([A-Z]+)([A-Z][a-z])", "$1_$2");
// Pass 2 (new): Digit→word boundaries (Base64Encoder → Base64_Encoder)
name = Regex.Replace(name, "([0-9])([A-Z][a-z])", "$1_$2");
// Pass 3: camelCase boundaries (getUserName → get_User_Name)
name = Regex.Replace(name, "([a-z])([A-Z])", "$1_$2");
return name.ToLowerInvariant();
```

Trace examples (with all 3 passes):

| C# Name | After Pass 1 | After Pass 2 | After Pass 3 | Result |
|---------|-------------|-------------|-------------|--------|
| `GetUserName` | — | — | `Get_User_Name` | `get_user_name` |
| `XMLParser` | `XML_Parser` | — | — | `xml_parser` |
| `ToString` | — | — | `To_String` | `to_string` |
| `HTTPSConnection` | `HTTPS_Connection` | — | — | `https_connection` |
| `SHA256Managed` | — | `SHA256_Managed` | — | `sha256_managed` |
| `Base64Encoder` | — | `Base64_Encoder` | — | `base64_encoder` |
| `ReadAllText` | — | — | `Read_All_Text` | `read_all_text` |
| `Int32` | — | — | — | `int32` |
| `Vector3D` | — | — | — | `vector3d` |

## Collision Analysis

### Forward Collisions (two Sharpy names → same C# name)

**Collision 1: snake_case method + PascalCase method in same class.**
```python
class Foo:
    def foo_bar(self): ...   # → FooBar
    def FooBar(self): ...    # → FooBar (passthrough)
```
Both produce `FooBar`. The spec says the compiler detects this and errors, but **collision detection is not currently implemented** in `CodeGenInfoComputer`.

**Non-collision: camelCase vs snake_case.** `fooBar` (passthrough) vs `foo_bar` → `FooBar`. These produce *different* results because camelCase is preserved as-is. No collision in the same `NameContext`.

**Non-collision: same name, different contexts.** A method `FooBar` and a type `FooBar` live in different C# namespaces (method vs type declaration), so they don't collide even with the same output string.

### Reverse Collisions (two C# names → same Sharpy name)

**Collision 1: Acronym casing ambiguity.**
```
ABCDef  → ABC_Def → abc_def
AbcDef  → Abc_Def → abc_def    ← SAME
```

**Collision 2: Trailing acronym ambiguity.**
```
GetIO  → Get_IO  → get_io
GetIo  → Get_Io  → get_io    ← SAME
```

**Collision 3: Digit-adjacent acronym ambiguity.**
```
SHA256  → sha256
Sha256  → sha256    ← SAME
```

**Practical impact:** These collisions are theoretical. .NET naming guidelines (and real-world APIs) don't produce both `ABCDef` and `AbcDef` or both `GetIO` and `GetIo` in the same type. The .NET Framework Design Guidelines specify consistent acronym casing (e.g., `IOStream` not `IoStream`), so both forms don't coexist.

### Round-Trip Fidelity

Round-trip (`snake_case` → `PascalCase` → `snake_case`) is preserved for the common case:

| Input | Forward | Reverse | Round-trip? |
|-------|---------|---------|-------------|
| `get_user_name` | `GetUserName` | `get_user_name` | Yes |
| `max_size` | `MaxSize` | `max_size` | Yes |
| `xml_parser` | `XmlParser` | `xml_parser` | Yes |

Round-trip **fails** for non-snake_case inputs:

| Input | Forward | Reverse | Round-trip? |
|-------|---------|---------|-------------|
| `httpClient` (camelCase) | `httpClient` (passthrough) | `http_client` | No |
| `HTTP` (constant) | `Http` | `http` | No |
| `XMLParser` (PascalCase) | `XMLParser` (passthrough) | `xml_parser` | No |

**This is acceptable.** The primary authoring flow is Sharpy developers writing `snake_case`, which round-trips perfectly. The non-round-trip cases are interop scenarios where backtick escaping is available.

## Design Decisions

### Decision 1: Reverse mangling must be symbol-aware

**Status:** Decided

The reverse direction currently applies a single context-free regex regardless of symbol kind. This produces incorrect results for enum members and constants:

| C# Symbol | Current reverse | Correct Sharpy form |
|-----------|----------------|-------------------|
| Enum member `DarkBlue` | `dark_blue` | `DARK_BLUE` |
| Constant `MaxRetryCount` | `max_retry_count` | `MAX_RETRY_COUNT` |
| Method `GetUserName` | `get_user_name` | `get_user_name` (correct) |
| Type `StringBuilder` | `string_builder` | `StringBuilder` (preserve) |

The reverse mangling must mirror the forward direction's `NameContext`-dependent dispatch:

| C# Symbol Kind | Reverse Target | Algorithm |
|----------------|---------------|-----------|
| Method | `snake_case` | Word-split → `.ToLowerInvariant()` → join with `_` |
| Enum member | `SCREAMING_SNAKE_CASE` | Word-split → `.ToUpperInvariant()` → join with `_` |
| Type/Class | Preserved | Pass through as-is |
| Interface | Preserved | Pass through as-is |
| Property | `snake_case` | Same as method |
| Constant / static readonly | `SCREAMING_SNAKE_CASE` | Same as enum member |
| Parameter | `snake_case` | Same as method |

**Implementation note:** The existing word-splitting regex is correct for all targets — the only difference is the final casing step (`.ToLowerInvariant()` vs `.ToUpperInvariant()`) and whether to join with `_` or pass through. This is a structural change (passing symbol kind through to the name conversion), not an algorithmic one.

**Impact on `OverloadIndex` data model:** `DiscoveredTypeInfo` already carries `TypeKind` (Class/Struct/Enum/Interface) and `FunctionSignature` is inherently for methods. However, the index doesn't currently distinguish properties from methods or track constants as a separate category. The discovery model will need to be extended to carry symbol kind for context-aware reverse mangling.

### Decision 2: Keep interface `I` prefix

**Status:** Decided

The `I` prefix on C# interfaces (e.g., `IComparable`, `IDisposable`) must be preserved as-is during reverse mangling.

**Rationale:** Stripping the `I` prefix would cause collisions with interfaces that don't follow the C# convention. While most .NET interfaces use the `I` prefix, third-party libraries and user code may have interfaces without it. Stripping `I` from `IFoo` would collide with a type `Foo`.

**Result:** `IComparable` remains `IComparable` in Sharpy (interfaces preserve casing, matching the forward direction where `ToInterfaceName` preserves the author's exact casing).

### Decision 3: Split at digit→word boundaries only

**Status:** Decided

The reverse mangling algorithm inserts an underscore at digit→uppercase-word boundaries using the pattern `([0-9])([A-Z][a-z])`. This splits where a digit sequence is followed by a new PascalCase word (uppercase then lowercase), but does NOT split when digits are followed by an abbreviation suffix (uppercase only) or at letter→digit boundaries.

**The three-pass reverse mangling algorithm:**
```
Pass 1: ([A-Z]+)([A-Z][a-z])   — acronym boundaries (XMLParser → XML_Parser)
Pass 2: ([0-9])([A-Z][a-z])    — digit→word boundaries (Base64Encoder → Base64_Encoder)
Pass 3: ([a-z])([A-Z])          — camelCase boundaries (getUserName → get_User_Name)
```

**Examples — splits correctly:**

| C# Name | Match | Result |
|---------|-------|--------|
| `Base64Encoder` | `4` + `En` → yes | `base64_encoder` |
| `Int32Converter` | `2` + `Co` → yes | `int32_converter` |
| `Utf8JsonReader` | `8` + `Js` → yes | `utf8_json_reader` |
| `X509Certificate` | `9` + `Ce` → yes | `x509_certificate` |
| `Win32Error` | `2` + `Er` → yes | `win32_error` |
| `SHA256Managed` | `6` + `Ma` → yes | `sha256_managed` |
| `Log2Ceil` | `2` + `Ce` → yes | `log2_ceil` |
| `Dx11Renderer` | `1` + `Re` → yes | `dx11_renderer` |
| `H264Decoder` | `4` + `De` → yes | `h264_decoder` |

**Examples — correctly does NOT split:**

| C# Name | Why no match | Result |
|---------|-------------|--------|
| `Vector3D` | `3` + `D` — not followed by lowercase | `vector3d` |
| `Color4F` | `4` + `F` — not followed by lowercase | `color4f` |
| `CRC32C` | `2` + `C` — not followed by lowercase | `crc32c` |
| `_4E` | `4` + `E` — not followed by lowercase | `_4e` |
| `Matrix4x4` | `4` + `x` — `x` is lowercase, not `[A-Z]` | `matrix4x4` |

**Examples — no letter→digit splitting (by design):**

| C# Name | Result | NOT |
|---------|--------|-----|
| `Base64` | `base64` | `base_64` |
| `Int32` | `int32` | `int_32` |
| `SHA256` | `sha256` | `sha_256` |

**Rationale:** The `[A-Z][a-z]` lookahead is the key — it detects a new PascalCase word beginning, which is a strong word-boundary signal in .NET naming. This is the same heuristic Pass 1 uses for acronym boundaries. Single uppercase letters or all-caps suffixes after digits (like `3D`, `4F`, `32C`) are abbreviations attached to the preceding token and should not be split. Letter→digit boundaries (`e` → `6` in `Base64`) are never split because digits are part of the preceding word — matching Python convention (`base64`, `sha256`, `int32`).

### Decision 4: Add forward collision detection

**Status:** Decided, not yet implemented

**Priority:** High

`CodeGenInfoComputer` must detect when two symbols in the same scope produce the same `CSharpName` and emit a compiler error. Currently, two members like `foo_bar` (→ `FooBar`) and `FooBar` (→ `FooBar` passthrough) silently collide.

**Implementation approach:**
- After computing `CodeGenInfo` for all members of a class/struct/module, collect all `CSharpName` values.
- If duplicates exist, emit an error on the second declaration, e.g.:
  ```
  SPY0XXX: Name collision: 'foo_bar' and 'FooBar' both compile to 'FooBar'.
  Rename one or use backtick escaping.
  ```
- Scope the check per containing type (class/struct) and per module (top-level functions).
- Exclude different symbol kinds that can coexist in C# (e.g., a method `FooBar` and a nested type `FooBar` don't collide).

### Decision 5: Add builtin shadowing warnings

**Status:** Decided, not yet implemented

**Priority:** Medium

When a .NET symbol's reverse-mangled name matches a Sharpy builtin (`print`, `len`, `range`, `type`, `input`, `str`, `int`, `float`, `bool`, `list`, `dict`, `set`, `tuple`, `enumerate`, `zip`, `map`, `filter`, `sorted`, `reversed`, `min`, `max`, `sum`, `abs`, `round`, `hash`, `id`, `isinstance`, `issubclass`, `super`, `object`, `None`, `True`, `False`), the compiler should emit a warning or require qualification.

**Scope:** This applies to the reverse direction (discovering .NET symbols for use in Sharpy code). When a .NET method like `Print(...)` reverse-mangles to `print`, it shadows the Sharpy builtin `print()`.

**Implementation approach:**
- Maintain a set of Sharpy builtin names (already partially exists in `BuiltinRegistry`).
- During reverse mangling / symbol discovery, check if the result shadows a builtin.
- If so, require module-qualified access: `my_module.print(...)` instead of bare `print(...)`.
- Consider emitting a warning at import time if an unqualified import would shadow a builtin.

### Decision 6: Document round-trip limitations

**Status:** Decided, not yet implemented

**Priority:** Low

The language specification and/or developer documentation should explicitly state:

- Round-trip fidelity (`name → mangle → unmangle == name`) holds for `snake_case` ↔ `PascalCase`.
- Round-trip does NOT hold for `camelCase` inputs (passthrough in forward direction, split in reverse).
- Round-trip does NOT hold for `SCREAMING_SNAKE_CASE` constants → `PascalCase` → `snake_case` (loses the all-caps intent). With symbol-aware reverse mangling (Decision 1), this is recoverable for constants/enum members.
- Backtick escaping (`\`ExactName\``) is the universal escape hatch when automatic mangling produces incorrect results.

## Considerations for General .NET Interop

If/when automatic `snake_case` access to arbitrary .NET types is implemented (beyond Sharpy.Core discovery), additional considerations apply:

### Discoverability

Developers must be able to predict the Sharpy name for a given C# symbol. For simple names (`ReadAllText` → `read_all_text`), the transformation is intuitive. For acronym-heavy names (`HTTPSClientCertificate` → `https_client_certificate`), it's less obvious. **IDE tooling (autocomplete, hover docs showing both names) will be essential.**

### Properties vs Methods

C# properties (`Name { get; set; }`) surface as `get_Name` / `set_Name` methods via reflection. The current discovery code (`OverloadIndexBuilder`) filters these out. For general interop, properties should be exposed as Sharpy attributes (field-like access), not as snake_cased getter/setter methods.

### Indexers and Operators

C# indexers (`this[int index]`) and operators (`operator+`) need special handling in reverse — they should map to dunders (`__getitem__`, `__add__`), not to snake_case method names. The forward direction's `ProtocolRegistry` and `DunderMapping` already define these mappings; the reverse direction needs to consume them.

### Namespace Mapping

`ModuleRegistry` already maps `system.collections.generic` → `System.Collections.Generic` for known namespaces. For arbitrary third-party namespaces, a consistent convention is needed:
- `Newtonsoft.Json` → `import newtonsoft.json` (dot-separated, lowercased)
- Namespace segments are NOT joined with underscores (unlike module file imports)

### Overloaded Methods

Multiple C# overloads of the same method (e.g., `ToString()` and `ToString(string format)`) all reverse-mangle to the same Sharpy name (`to_string`). This is fine — Sharpy/Python handles this via optional parameters and the existing overload resolution in `OverloadIndex`.

## Appendix: Word-Splitting Regex Traces

The reverse mangling algorithm uses three regex passes:

```
Pass 1: ([A-Z]+)([A-Z][a-z])   — acronym boundaries
Pass 2: ([0-9])([A-Z][a-z])    — digit→word boundaries
Pass 3: ([a-z])([A-Z])          — camelCase boundaries
```

### Trace: `HTTPSClientCertificate`

1. Pass 1 `([A-Z]+)([A-Z][a-z])`: `HTTPS` + `Cl` → `HTTPS_ClientCertificate`
2. Pass 2 `([0-9])([A-Z][a-z])`: no digits in remaining positions → no change
3. Pass 3 `([a-z])([A-Z])`: `t` + `C` → `HTTPS_Client_Certificate`
4. `.ToLowerInvariant()` → `https_client_certificate`

### Trace: `SHA256Managed`

1. Pass 1 `([A-Z]+)([A-Z][a-z])`: `SHA` is `[A-Z]+`, then `2` (digit) breaks the sequence. `M` is `[A-Z]+` (1 char), then `a` is `[a-z]` but `Ma` needs `[A-Z][a-z]` which requires 2 chars starting with uppercase — `M` is the uppercase and `a` is the lowercase → that IS `[A-Z][a-z]`! But `M` was already consumed as `[A-Z]+`. The regex needs `[A-Z]+` (1+ chars) then `[A-Z][a-z]` (2 chars). So minimum: `[A-Z][A-Z][a-z]` = 3 uppercase-related chars. `M` alone as `[A-Z]+` then `an` as `[A-Z][a-z]`? `a` is lowercase, not `[A-Z]`. No match.
   Result: `SHA256Managed` (unchanged)
2. Pass 2 `([0-9])([A-Z][a-z])`: `6` + `Ma` → `SHA256_Managed`
3. Pass 3 `([a-z])([A-Z])`: no `[a-z][A-Z]` boundaries remain → no change
4. `.ToLowerInvariant()` → `sha256_managed`

### Trace: `Base64Encoder`

1. Pass 1: no multi-cap sequences → no change
2. Pass 2 `([0-9])([A-Z][a-z])`: `4` + `En` → `Base64_Encoder`
3. Pass 3 `([a-z])([A-Z])`: no remaining `[a-z][A-Z]` boundaries → no change
4. `.ToLowerInvariant()` → `base64_encoder`

### Trace: `Utf8JsonReader`

1. Pass 1: no multi-cap sequences → no change
2. Pass 2 `([0-9])([A-Z][a-z])`: `8` + `Js` → `Utf8_JsonReader`
3. Pass 3 `([a-z])([A-Z])`: `n` + `R` → `Utf8_Json_Reader`
4. `.ToLowerInvariant()` → `utf8_json_reader`

### Trace: `Vector3D`

1. Pass 1: no multi-cap sequences → no change
2. Pass 2 `([0-9])([A-Z][a-z])`: `3` + `D` — `D` is at end of string, not followed by lowercase → no match
3. Pass 3 `([a-z])([A-Z])`: `r` + `3`? No, `3` is a digit. No match.
4. `.ToLowerInvariant()` → `vector3d`

### Trace: `H2OParser`

1. Pass 1 `([A-Z]+)([A-Z][a-z])`: in `H2OParser`, `O` is `[A-Z]+` (1 char) then `Pa` is `[A-Z][a-z]`? No — `[A-Z]+` needs to be followed by `[A-Z][a-z]`. `O` as `[A-Z]+`, then need `[A-Z][a-z]` = `Pa`. `P` is `[A-Z]`, `a` is `[a-z]` → match! But `O` was consumed by `[A-Z]+`. So group 1 = `O`, group 2 = `Pa` → `H2O_Parser`. Correct — Pass 1 handles this without needing Pass 2.
2. Pass 2: `2` + `OP`? `O` is `[A-Z]`, `P` is not `[a-z]` → no match (already split by Pass 1 anyway)
3. Pass 3: no remaining boundaries
4. `.ToLowerInvariant()` → `h2o_parser`

### Trace: `_4E` (user's edge case)

1. Pass 1: no multi-cap sequences → no change
2. Pass 2 `([0-9])([A-Z][a-z])`: `4` + `E` — `E` is at end of string, not followed by lowercase → no match
3. Pass 3: no `[a-z][A-Z]` boundaries
4. `.ToLowerInvariant()` → `_4e`

### Comprehensive results table

| C# Name | Pass 1 | Pass 2 | Pass 3 | Final |
|---------|--------|--------|--------|-------|
| `GetUserName` | — | — | `Get_User_Name` | `get_user_name` |
| `XMLParser` | `XML_Parser` | — | — | `xml_parser` |
| `HTTPSConnection` | `HTTPS_Connection` | — | — | `https_connection` |
| `Base64Encoder` | — | `Base64_Encoder` | — | `base64_encoder` |
| `Int32Converter` | — | `Int32_Converter` | — | `int32_converter` |
| `SHA256Managed` | — | `SHA256_Managed` | — | `sha256_managed` |
| `Utf8JsonReader` | — | `Utf8_JsonReader` | `Utf8_Json_Reader` | `utf8_json_reader` |
| `X509Certificate` | — | `X509_Certificate` | — | `x509_certificate` |
| `Win32Error` | — | `Win32_Error` | — | `win32_error` |
| `Log2Ceil` | — | `Log2_Ceil` | — | `log2_ceil` |
| `H264Decoder` | — | `H264_Decoder` | — | `h264_decoder` |
| `Dx11Renderer` | — | `Dx11_Renderer` | — | `dx11_renderer` |
| `H2OParser` | `H2O_Parser` | — | — | `h2o_parser` |
| `CO2Level` | — | `CO2_Level` | — | `co2_level` |
| `Vector3D` | — | — | — | `vector3d` |
| `Color4F` | — | — | — | `color4f` |
| `CRC32C` | — | — | — | `crc32c` |
| `Matrix4x4` | — | — | — | `matrix4x4` |
| `_4E` | — | — | — | `_4e` |
| `Base64` | — | — | — | `base64` |
| `Int32` | — | — | — | `int32` |
| `SHA256` | — | — | — | `sha256` |
| `ToString` | — | — | `To_String` | `to_string` |
| `ReadAllText` | — | — | `Read_All_Text` | `read_all_text` |
