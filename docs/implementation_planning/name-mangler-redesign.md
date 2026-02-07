# Name Mangling System Refactor

## Context

The `NameMangler` class has several edge cases that cause silent name collisions (`foo__bar` = `foo_bar`), destroy casing (`httpClient` → `Httpclient`), overzealously escape keywords (`String` → `@String`), strip the `__` private prefix, and deviate from the spec on constants. Additionally, dunder method mapping is a codegen concern wrongly living in a naming utility, and `TransformEnumMemberName` is duplicated outside NameMangler.

### Bug demonstrations (verify these first to build intuition)

```
Current ToPascalCase("httpClient") = "Httpclient"  ← destroys casing (should passthrough)
Current ToPascalCase("foo__bar")   = "FooBar"      ← same as "foo_bar" → silent collision!
Current ToPascalCase("__private")  = "Private"     ← double-underscore prefix lost
Current ToConstantCase("MAX_SIZE") = "MAX_SIZE"    ← spec says "MaxSize"
Current EscapeKeywordIfNeeded("Class") = "@Class"  ← "Class" is NOT a keyword; only "class" is
```

## Changes Overview

1. **Name form detection** — only mangle well-formed `snake_case` / `SCREAMING_SNAKE_CASE`; pass through `PascalCase`, `camelCase`, and unrecognized forms
2. **Preserve `__` private prefix** — `__private_field` → `__PrivateField`
3. **Case-sensitive keyword escaping** — only escape exact lowercase C# keywords
4. **Constants → PascalCase** — `MAX_SIZE` → `MaxSize` (match spec and C# convention)
5. **Extract dunder mapping** from NameMangler into codegen-owned `DunderMapping` class
6. **Move `TransformEnumMemberName`** into NameMangler as `ToEnumMemberName`
7. **Add `NamingConventionValidator`** — warn (SPY0453) about consecutive underscores inside names

---

## Phase 1: New Infrastructure (additive only, no behavior changes)

> **Goal**: Add all new types, files, and stubs without changing any existing behavior. Everything in this phase is purely additive — the existing test suite should pass without modification.

### 1a. Create `NameFormDetector` — name classification utility

**New file**: `src/Sharpy.Compiler/CodeGen/NameFormDetector.cs`

```csharp
internal enum NameForm
{
    SnakeCase,            // get_user_name (all lowercase + digits + single underscores)
    PascalCase,           // HttpClient (starts uppercase, no underscores)
    CamelCase,            // httpClient (starts lowercase, no underscores, has uppercase)
    ScreamingSnakeCase,   // MAX_SIZE (all uppercase + digits + single underscores)
    SingleWordLower,      // hello (all lowercase, no underscores)
    SingleWordUpper,      // HTTP (all uppercase, no underscores)
    Dunder,               // __init__ (double underscore bookends)
    Literal,              // `backtick_escaped`
    Unrecognized          // foo__bar, Foo_bar, mixed patterns
}
```

**Checklist**:

- [x] Create enum `NameForm` with all 9 variants listed above
- [x] Create `internal static class NameFormDetector`
- [x] Implement `public static NameForm Detect(string nameBody)`:
  - **Important**: This receives the *body* only — callers strip `_`/`__` prefixes and trailing underscores before calling. The one exception is dunders and literals, which should be detectable from the full name.
  - Return `Dunder` if starts with `__` AND ends with `__` AND length > 4
  - Return `Literal` if starts with `` ` `` AND ends with `` ` ``
  - If the body contains `__` (consecutive underscores), return `Unrecognized`
  - If contains no underscores:
    - All lowercase (+ digits) → `SingleWordLower`
    - All uppercase (+ digits) → `SingleWordUpper`
    - Starts uppercase, has no underscores → `PascalCase`
    - Starts lowercase, has uppercase somewhere → `CamelCase`
  - If contains single underscores:
    - All lowercase segments (+ digits) → `SnakeCase`
    - All uppercase segments (+ digits) → `ScreamingSnakeCase`
    - Otherwise → `Unrecognized` (mixed case like `Foo_bar`)
- [x] Implement `public static bool HasConsecutiveUnderscores(string nameBody)`:
  - Simple: `nameBody.Contains("__")`
  - **Gotcha**: Must NOT flag dunder bookends. Callers should strip dunder bookends before calling, OR this method should only check the *inner* body. Document which convention you pick and be consistent.
- [x] Implement `public static bool IsConstantCaseName(string name)`:
  - Port the logic from `CodeGenInfoComputer.cs:189` — `name.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c)) && name.Any(char.IsUpper)`
  - Add a null/empty guard (return `false`) — the `RoslynEmitter.CompilationUnit.cs:372` version already has this
  - This consolidates the 3 private copies: `RoslynEmitter.CompilationUnit.cs:372` (for-loop version with null check), `CodeGenInfoComputer.cs:189` (LINQ), `ExecutionOrderAnalyzer.cs:333` (LINQ)
  - **Decision**: This is kept as a separate convenience method (not just `Detect() == ScreamingSnakeCase || Detect() == SingleWordUpper`) because existing call sites use it as a boolean check and it matches both `ScreamingSnakeCase` and `SingleWordUpper`

> **Fork-in-the-road**: Should `Detect()` handle empty strings or null? **Decision**: Return `Unrecognized` for null/empty. Callers already guard against null before reaching here. Defensive but not overcomplicated.

> **Fork-in-the-road**: What about single-character names like `x` or `X`? `x` → `SingleWordLower`, `X` → `SingleWordUpper`. Digit-only names like `_1` → `Unrecognized`. Names starting with digits shouldn't reach here (lexer rejects them).

### 1b. Create `DunderMapping` — codegen-owned dunder→C# mapping

**New file**: `src/Sharpy.Compiler/CodeGen/DunderMapping.cs`

Copy the 11-entry `_dunderMethodMap` from NameMangler here (actual deletion from NameMangler happens in Phase 5).

**Checklist**:

- [x] Create `internal static class DunderMapping`
- [x] Copy the `_dunderMethodMap` dictionary from `NameMangler.cs:30-46`:
  ```
  __init__     → Constructor
  __str__      → ToString
  __repr__     → ToString
  __eq__       → Equals
  __hash__     → GetHashCode
  __getitem__  → GetItem
  __setitem__  → SetItem
  __len__      → Length
  __contains__ → Contains
  __iter__     → GetEnumerator
  __bool__     → ToBoolean
  ```
- [x] Implement `public static string? GetCSharpName(string dunderName)` — dictionary lookup, returns null if not found
- [x] Implement `public static bool HasMapping(string dunderName)` — `_map.ContainsKey(dunderName)`
- [x] Implement `public static bool IsDunderMethod(string name)` — same logic as current `NameMangler.IsDunderMethod`: `name.StartsWith("__") && name.EndsWith("__") && name.Length > 5`
  - **Note**: Current NameMangler uses `name.Length > 5` (line 263). `__x__` (length 5) is excluded. Python considers `__a__` a valid dunder, but the existing test `IsDunderMethod_InvalidDunder_ReturnsFalse` expects `"__x__"` → `false`. Keep `> 5` for backward compat; revisit in Phase 5 if needed.
  - **Note**: `NameFormDetector.Detect()` uses `> 4` for its `Dunder` form classification (purely syntactic — `__a__` *looks* like a dunder). This difference is harmless: if `Detect()` classifies something as `Dunder` that `IsDunderMethod` rejects, the `ToPascalCase` algorithm handles it via the `__` prefix path (step 3) or passthrough, not the dunder map.
- [x] Implement `public static string TransformUnknownDunder(string name)` — for operator dunders not in the map:
  - Strip leading/trailing `__`, split middle on `_`, capitalize each segment, rejoin with `__` bookends
  - Example: `__add__` → `__Add__`, `__custom_method__` → `__CustomMethod__`
  - Port this logic from `NameMangler.ToPascalCase` lines 114-116
- [x] Add `#if DEBUG` static constructor that verifies consistency with `ProtocolRegistry`, mirroring the one in `NameMangler.cs:62-76`

> **Rationale**: Dunder→C# mapping is a *codegen* concern (it decides what C# method names to emit), not a *naming convention* concern. `NameMangler` should be about `snake_case` → `PascalCase`; what `__str__` means in C# is codegen policy.

### 1c. Add diagnostic code SPY0453

**Checklist**:

- [x] **File**: `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` (line 177, after `UnusedImport`)
  - Add: `public const string NamingConventionWarning = "SPY0453";`
  - This goes in the `Validation` class, in the `// Warnings (SPY0450-SPY0499)` section
  - SPY0453 is the next available warning number after SPY0452 (UnusedImport)
- [x] **File**: `src/Sharpy.Compiler/Diagnostics/DiagnosticExplanations.cs`
  - Add an explanation entry in the `// Validation warnings (SPY0450-SPY0499)` section (~line 667)
  - Follow the existing pattern using the `Add()` helper:
    ```csharp
    Add(DiagnosticCodes.Validation.NamingConventionWarning,
        "Naming Convention Warning",
        "Naming",
        "Identifier contains consecutive underscores which may cause name collision...",
        "x: int = 1\nfoo__bar: int = 2  # warning: consecutive underscores",
        "Rename the identifier or use backtick escaping: `foo__bar`");
    ```
- [x] **Note**: `SPY0453` already appears in `ProjectCompilationTests.cs:1361` as a test value for `<NoWarn>` parsing. That test is just validating the NoWarn mechanism with arbitrary codes — it will still pass and will now coincidentally suppress a real warning. No conflict.

### 1d. Add `ToEnumMemberName` stub to NameMangler

**Checklist**:

- [x] **File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`
  - Add `EnumMember` to the `NameContext` enum (after `Constant`, before the closing brace)
- [x] Add `public static string ToEnumMemberName(string name)` method:
  - Port the logic from `RoslynEmitter.TypeDeclarations.cs:715-731`:
    ```csharp
    public static string ToEnumMemberName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        if (name.StartsWith("`") && name.EndsWith("`"))
            return name[1..^1];
        var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var capitalizedParts = parts.Select(part =>
            string.IsNullOrEmpty(part) ? part :
            char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant());
        return string.Join("", capitalizedParts);
    }
    ```
  - **Note**: This uses `StringSplitOptions.RemoveEmptyEntries` (unlike `ToPascalCase` which does not), which means consecutive underscores are silently consumed. This is existing behavior — preserve it for now.
- [x] Wire `EnumMember` in the `Transform` method switch: `NameContext.EnumMember => ToEnumMemberName(name),`
- [x] **Do NOT** change any call sites yet — that's Phase 4. This phase is additive only.

### 1e. Tests for new infrastructure

**Checklist**:

- [x] **New file**: `src/Sharpy.Compiler.Tests/CodeGen/NameFormDetectorTests.cs`
  - Add `[Collection("Sequential")]` attribute (match existing test conventions)
  - Test `Detect()` for each `NameForm`:
    - [x] `"get_user_name"` → `SnakeCase`
    - [x] `"a_b_c"` → `SnakeCase`
    - [x] `"item1_count"` → `SnakeCase` (digits allowed)
    - [x] `"HttpClient"` → `PascalCase`
    - [x] `"XMLParser"` → `PascalCase` (starts uppercase, no underscores)
    - [x] `"httpClient"` → `CamelCase`
    - [x] `"iPhone"` → `CamelCase`
    - [x] `"MAX_SIZE"` → `ScreamingSnakeCase`
    - [x] `"HTTP_STATUS_2XX"` → `ScreamingSnakeCase` (digits allowed)
    - [x] `"hello"` → `SingleWordLower`
    - [x] `"HTTP"` → `SingleWordUpper`
    - [x] `"__init__"` → `Dunder`
    - [x] `` "`some_name`" `` → `Literal`
    - [x] `"foo__bar"` → `Unrecognized` (consecutive underscores)
    - [x] `"Foo_bar"` → `Unrecognized` (mixed case with underscores)
    - [x] `""` → `Unrecognized`
  - Test `HasConsecutiveUnderscores()`:
    - [x] `"foo__bar"` → `true`
    - [x] `"foo_bar"` → `false`
    - [x] `"___"` → `true`
  - Test `IsConstantCaseName()`:
    - [x] `"MAX_SIZE"` → `true`
    - [x] `"HTTP"` → `true`
    - [x] `"MAX_2"` → `true`
    - [x] `"hello"` → `false`
    - [x] `"HttpClient"` → `false`
    - [x] `"_"` → `false` (no uppercase chars)

- [x] **New file**: `src/Sharpy.Compiler.Tests/CodeGen/DunderMappingTests.cs`
  - Add `[Collection("Sequential")]` attribute
  - Test `GetCSharpName()`:
    - [x] `"__init__"` → `"Constructor"`
    - [x] `"__str__"` → `"ToString"`
    - [x] `"__eq__"` → `"Equals"`
    - [x] `"__hash__"` → `"GetHashCode"`
    - [x] `"__getitem__"` → `"GetItem"`
    - [x] `"__setitem__"` → `"SetItem"`
    - [x] `"__len__"` → `"Length"`
    - [x] `"__contains__"` → `"Contains"`
    - [x] `"__iter__"` → `"GetEnumerator"`
    - [x] `"__bool__"` → `"ToBoolean"`
    - [x] `"__repr__"` → `"ToString"`
    - [x] `"__unknown__"` → `null`
  - Test `TransformUnknownDunder()`:
    - [x] `"__add__"` → `"__Add__"`
    - [x] `"__sub__"` → `"__Sub__"`
    - [x] `"__custom_method__"` → `"__CustomMethod__"`
  - Test `IsDunderMethod()`:
    - [x] `"__init__"` → `true`
    - [x] `"init"` → `false`
    - [x] `"_private"` → `false`

### Phase 1 verification

- [x] `dotnet build sharpy.sln` — must compile
- [x] `dotnet test` — ALL existing tests must still pass (no behavior changes)
- [x] New test files pass

---

## Phase 2: Keyword Escaping Fix (small, isolated)

> **Goal**: Fix the over-eager keyword escaping that adds `@` to valid identifiers like `Class`, `String`, `Int`. This is a small, self-contained change.

### 2a. Make `EscapeKeywordIfNeeded` case-sensitive

> **Rationale**: C# keywords are all lowercase (`class`, `string`, `int`). After `ToPascalCase` converts `"class"` → `"Class"`, the result is a valid C# identifier — `Class` is NOT a keyword. The current code lowercases before checking, which wrongly escapes `"Class"` → `"@Class"`.

**Checklist**:

- [x] **File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs` (line 293)
  - Change: `_csharpKeywords.Contains(name.ToLowerInvariant())` → `_csharpKeywords.Contains(name)`
  - The `_csharpKeywords` set already contains only lowercase strings, so this is correct
- [x] **File**: `src/Sharpy.Compiler/CodeGen/NameResolutionService.cs` (line 197)
  - Change: `CSharpKeywords.Contains(name.ToLowerInvariant())` → `CSharpKeywords.Contains(name)`
  - Same fix, same rationale. The `CSharpKeywords` set is also all-lowercase
- [x] **File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs` (line 399)
  - Change: `new(StringComparer.OrdinalIgnoreCase)` → `new()` (remove the case-insensitive comparer)
  - This is a **third copy** of the same bug, using a different mechanism: `OrdinalIgnoreCase` makes the HashSet match case-insensitively, so `CSharpKeywords.Contains("Class")` returns `true`. Removing the comparer makes it use default ordinal comparison (case-sensitive), which is correct since the set contains only lowercase keywords.
  - The `EscapeCSharpKeyword` method at line 418 needs no change — it already does a plain `CSharpKeywords.Contains(name)` without lowercasing.

> **Why all three files?** `NameMangler.EscapeKeywordIfNeeded` is used during name mangling. `NameResolutionService.EscapeCSharpKeyword` is used during name resolution for module symbols. `RoslynEmitter.CompilationUnit.EscapeCSharpKeyword` is used for module path escaping during code generation. All three have the same over-eager case-insensitive escaping bug, achieved via different mechanisms (`.ToLowerInvariant()` in the first two, `StringComparer.OrdinalIgnoreCase` in the third).

### 2b. Update tests

**Checklist**:

- [x] **File**: `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs`
  - Update the `ToPascalCase_CSharpKeyword_EscapesWithAt` test (line 203):
    - `"class"` → `"Class"` (was `"@Class"`)
    - `"if"` → `"If"` (was `"@If"`)
    - `"while"` → `"While"` (was `"@While"`)
    - `"return"` → `"Return"` (was `"@Return"`)
    - `"namespace"` → `"Namespace"` (was `"@Namespace"`)
    - `"static"` → `"Static"` (was `"@Static"`)
  - Rename this test method to something like `ToPascalCase_CSharpKeyword_CapitalizesWithoutEscaping` to match new behavior
  - Add a new test: `ToPascalCase_CSharpKeyword_AsSnakeCase_NoEscape` — e.g., `"for_each"` → `"ForEach"` (no `@`)
  - The `ToCamelCase_CSharpKeyword_EscapesWithAt` tests (line 219) should remain UNCHANGED:
    - `"class"` → `"@class"` — still correct, because `ToCamelCase("class")` produces `"class"` (lowercase single word), which IS a keyword
  - Update `ToPascalCase_NotKeyword_NoEscaping` (line 232): `"myClass"` currently produces `"Myclass"` — this test will be updated in Phase 3 when camelCase passthrough is added. Leave it for now.
- [x] Run full test suite: `dotnet test`
- [x] Search for `@String`, `@Class`, `@Int`, `@Object`, `@Void` in `.expected` and `.expected.cs` snapshot files — these may need updating if any test fixtures use keywords as identifiers that get PascalCased
  - Command: `grep -r "@String\|@Class\|@Int\b\|@Object\|@Void" src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
  - If matches found, update the `.expected` / `.expected.cs` files, or regenerate snapshots with `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`

### Phase 2 verification

- [x] `dotnet build sharpy.sln`
- [x] `dotnet test` — all pass
- [x] `dotnet run --project src/Sharpy.Cli -- emit csharp snippets/hello.spy` — sanity check

---

## Phase 3: Core Mangling Refactor (largest change)

> **Goal**: Rewrite the core `ToPascalCase`, `ToCamelCase`, and `ToConstantCase` methods to use form detection. This fixes the casing destruction bug, the consecutive-underscore collision bug, and the `__` prefix loss.
>
> **This is the riskiest phase.** Many call sites indirectly depend on these methods. Expect snapshot updates.

### 3a. Refactor `ToPascalCase`

**File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

**New algorithm** (replace lines 97-156):

1. Handle null/empty, backtick literals (unchanged)
2. **Dunders**: Route to `DunderMapping.GetCSharpName()` ?? `DunderMapping.TransformUnknownDunder()` (temporary bridge — Phase 5 removes this)
3. **`__` prefix**: Recognize `__foo` (starts `__`, does NOT end `__`) as double-private prefix. Strip, mangle body, re-attach.
4. **`_` prefix**: Recognize `_foo` (starts `_`, not `__`) as single-private prefix. Strip, mangle body, re-attach. (unchanged behavior)
5. Strip trailing underscores (unchanged)
6. **Detect name form** via `NameFormDetector.Detect(cleanName)`:
   - `SnakeCase` / `SingleWordLower` → split on `_`, capitalize each segment (preserving: `char.ToUpper(first) + rest`), join
   - `ScreamingSnakeCase` → split on `_`, title-case each segment (normalizing: `char.ToUpper(first) + rest.ToLower()`), join. `MAX_SIZE` → `MaxSize`
   - `PascalCase` / `SingleWordUpper` → pass through as-is
   - `CamelCase` → pass through as-is (no warning from NameMangler)
   - `Unrecognized` → pass through as-is
   - `Dunder` → already handled in step 2
7. Restore prefix + trailing underscores
8. Case-sensitive keyword escaping (from Phase 2)

**Checklist**:

- [ ] Add two private helper methods to replace the single `Capitalize`:
  ```csharp
  // For snake_case: rest is already lowercase, just capitalize first char
  private static string CapitalizePreserving(string word)
  {
      if (string.IsNullOrEmpty(word)) return word;
      return char.ToUpperInvariant(word[0]) + word[1..];
  }

  // For SCREAMING_SNAKE_CASE: normalize rest to lowercase
  private static string CapitalizeNormalizing(string word)
  {
      if (string.IsNullOrEmpty(word)) return word;
      return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
  }
  ```
  - **Note**: For snake_case inputs, `CapitalizePreserving` and `CapitalizeNormalizing` produce the same result (rest is already lowercase). The distinction matters for SCREAMING_SNAKE_CASE where `CapitalizeNormalizing("HTTP")` → `"Http"` but `CapitalizePreserving("HTTP")` → `"HTTP"`.
- [ ] Rewrite `ToPascalCase` following the algorithm above
- [ ] **Critical behavioral changes to verify**:
  - `ToPascalCase("httpClient")` → `"httpClient"` (camelCase passthrough; was `"Httpclient"`)
  - `ToPascalCase("foo__bar")` → `"foo__bar"` (Unrecognized passthrough; was `"FooBar"`)
  - `ToPascalCase("__private_field")` → `"__PrivateField"` (preserves `__` prefix; was `"PrivateField"`)
  - `ToPascalCase("MAX_SIZE")` → `"MaxSize"` (SCREAMING_SNAKE → PascalCase; was `"MaxSize"` already via `Capitalize` normalizing — **actually no change here**)
  - `ToPascalCase("HTTP")` → `"HTTP"` (SingleWordUpper passthrough)
  - `ToPascalCase("PascalCase")` → `"PascalCase"` (passthrough; unchanged)
  - `ToPascalCase("get_user_name")` → `"GetUserName"` (unchanged)

> **Fork-in-the-road**: Should `ToPascalCase("ALREADY_UPPER")` change? Currently it produces `"AlreadyUpper"` (Capitalize normalizes each segment). With the new code, `ALREADY_UPPER` is detected as `ScreamingSnakeCase`, split on `_`, and `CapitalizeNormalizing` each segment → `"AlreadyUpper"`. **Same result — no change.** Existing test at line 345 still passes.

> **Fork-in-the-road**: What about `ToPascalCase("myClass")` (currently `"Myclass"`)? With form detection, `"myClass"` has no underscores, starts lowercase, has uppercase → `CamelCase` → passthrough → `"myClass"`. The existing test at line 236 expects `"Myclass"` — **this test must be updated**.

### 3b. Refactor `ToCamelCase`

**File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

Same form detection approach. Replace lines 161-205.

**Checklist**:

- [ ] Rewrite `ToCamelCase` using `NameFormDetector.Detect()`:
  - `SnakeCase` / `SingleWordLower` → first segment lowercase, rest capitalized (preserving). `"get_user_name"` → `"getUserName"` (unchanged)
  - `ScreamingSnakeCase` → first segment fully lowered, rest title-cased (normalizing). `"MAX_SIZE"` → `"maxSize"`
  - `PascalCase` → first char to lower, rest preserved. `"HttpClient"` → `"httpClient"`
  - `CamelCase` → pass through. `"httpClient"` → `"httpClient"`
  - `SingleWordUpper` → fully lowercase. `"HTTP"` → `"http"`
  - `SingleWordLower` → pass through. `"hello"` → `"hello"`
  - `Unrecognized` → pass through. `"foo__bar"` → `"foo__bar"`
- [ ] Extend prefix handling to include `__` (double-private prefix), matching Phase 3a step 3:
  - `__` prefix: Recognize `__foo` (starts `__`, does NOT end `__`). Strip `__`, mangle body as camelCase, re-attach `__`. Example: `__private_count` → `__privateCount`
  - `_` prefix: Unchanged behavior. Example: `_private_var` → `_privateVar`
- [ ] Keep trailing underscore handling (unchanged)
- [ ] **Critical behavioral change**: `ToCamelCase("HttpClient")` → `"httpClient"` (was `"httpclient"` — all lowercase). Now properly lowercases just the first char.

> **Fork-in-the-road**: What about `ToCamelCase("ALREADY_UPPER")`? Currently → `"alreadyUpper"` (first part lowered, rest capitalized). With detection: `ScreamingSnakeCase` → first segment `"already"` (fully lowered), rest `"Upper"` (title-cased) → `"alreadyUpper"`. **Same result.**

### 3c. Refactor `ToConstantCase`

**File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

Now converts SCREAMING_SNAKE_CASE to PascalCase (matching the spec at `docs/language_specification/name_mangling.md:13`).

**Checklist**:

- [ ] Rewrite `ToConstantCase` (lines 210-221):
  - `ScreamingSnakeCase` → PascalCase via normalizing capitalize. `MAX_SIZE` → `MaxSize`
  - `SingleWordUpper` → title-case. `HTTP` → `Http`
  - `SnakeCase` → PascalCase (same as `ToPascalCase` for snake_case)
  - `PascalCase` / `CamelCase` → pass through
  - `Unrecognized` → pass through
- [ ] **Key difference from `ToPascalCase`**: `SingleWordUpper` becomes `Http` here (constants normalize) vs staying `HTTP` in `ToPascalCase` (types preserve). This distinction matters because:
  - A type named `HTTP` should stay `HTTP` (it's the user's chosen casing)
  - A constant named `HTTP` should become `Http` (C# convention for constants is PascalCase, and single all-caps words normalize)

### 3d. Simplify `IsConstantCaseName` call sites

> **Rationale**: Currently, many call sites have this branching pattern:
> ```csharp
> var name = IsConstantCaseName(original) ? NameMangler.ToConstantCase(original) : NameMangler.ToPascalCase(original);
> ```
> After this refactor, `ToPascalCase` handles SCREAMING_SNAKE_CASE → PascalCase correctly, so most of these can be simplified. The only difference between `ToPascalCase` and `ToConstantCase` is the `SingleWordUpper` case (`HTTP` → `HTTP` vs `Http`).

**3 private `IsConstantCaseName` method definitions to remove/replace**:
- `RoslynEmitter.CompilationUnit.cs:372-394`
- `CodeGenInfoComputer.cs:189-194`
- `ExecutionOrderAnalyzer.cs:333-337`

All 3 can be deleted and replaced with `NameFormDetector.IsConstantCaseName()` where still needed.

**Checklist for each call site**:

- [ ] `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs:835` — `IsConstantCaseName(memberAccess.Member)` ternary
  - **Context**: Member access on module-level symbols. Constants should use `ToConstantCase`, non-constants use `ToPascalCase`.
  - Replace: `var mangledMemberName = NameFormDetector.IsConstantCaseName(memberAccess.Member) ? NameMangler.ToConstantCase(memberAccess.Member) : NameMangler.ToPascalCase(memberAccess.Member);`
  - Or if the member's symbol is available, check `symbol.IsConstant` instead of guessing from naming convention
- [ ] `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs:994` — same pattern
- [ ] `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs:1015` — same pattern
- [ ] `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs:515` — `varDecl.IsConst || IsConstantCaseName(varDecl.Name)`
  - Replace: `varDecl.IsConst || NameFormDetector.IsConstantCaseName(varDecl.Name)`
- [ ] `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs:164` — `!varRedefinition.IsConst && !IsConstantCaseName(varRedefinition.Name)`
  - Replace: Use `NameFormDetector.IsConstantCaseName()`
- [ ] `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs:484,488` — property name mangling
  - Replace: Use `NameFormDetector.IsConstantCaseName()`
- [ ] `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs:182` — `DetermineCSharpNameForFromImport`
  - Replace: Use `NameFormDetector.IsConstantCaseName()`
  - Delete the private `IsConstantCaseName` method at lines 189-194
- [ ] `src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs:90` — constant detection in execution order analysis
  - Replace: Use `NameFormDetector.IsConstantCaseName()`
  - Delete the private `IsConstantCaseName` method at lines 333-337
- [ ] `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs:372` — delete the private method definition (lines 372-394)

> **Decision guideline**: At each call site, ask: "Is this site distinguishing constants from non-constants for *mangling* purposes, or for *codegen* purposes?" If for mangling (choosing `ToConstantCase` vs `ToPascalCase`), use `NameFormDetector.IsConstantCaseName`. If for codegen (deciding `static readonly` vs `var`), the existing semantic info (`varDecl.IsConst`, etc.) is better.

### 3e. Update tests

**Checklist**:

- [ ] **File**: `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs` — Major updates:
  - [ ] Update `ToPascalCase_NonSnakeCase_PreservesOrConverts` (line 345):
    - `"ALREADY_UPPER"` → `"AlreadyUpper"` (unchanged)
    - `"MixedCase"` → `"MixedCase"` (unchanged — PascalCase passthrough)
    - `"PascalCase"` → `"PascalCase"` (unchanged)
  - [ ] Update `ToPascalCase_NotKeyword_NoEscaping` (line 232):
    - `"myClass"` now produces `"myClass"` (camelCase passthrough), NOT `"Myclass"`
    - Update the assertion accordingly
  - [ ] Update `ToConstantCase_CapsSnakeCase_RemainsUnchanged` (line 86):
    - `"MAX_SIZE"` → `"MaxSize"` (was `"MAX_SIZE"`)
    - `"PI"` → `"Pi"` (was `"PI"` — SingleWordUpper normalizes in constant context)
    - `"DEFAULT_TIMEOUT"` → `"DefaultTimeout"` (was `"DEFAULT_TIMEOUT"`)
    - Rename this test to `ToConstantCase_CapsSnakeCase_ConvertsToPascalCase`
  - [ ] Update `Transform_WithContext_TransformsCorrectly` (line 259):
    - `NameContext.Constant, "MAX_SIZE"` → `"MaxSize"` (was `"MAX_SIZE"`)
  - [ ] Add new test cases:
    - [ ] `ToPascalCase("httpClient")` → `"httpClient"` (camelCase passthrough)
    - [ ] `ToPascalCase("foo__bar")` → `"foo__bar"` (Unrecognized passthrough)
    - [ ] `ToPascalCase("__private_field")` → `"__PrivateField"` (double-underscore prefix preserved)
    - [ ] `ToPascalCase("__private")` → `"__Private"` (double-underscore prefix preserved)
    - [ ] `ToPascalCase("HTTP")` → `"HTTP"` (SingleWordUpper preserved in PascalCase context)
    - [ ] `ToCamelCase("HttpClient")` → `"httpClient"` (PascalCase → camelCase)
    - [ ] `ToCamelCase("HTTP")` → `"http"` (SingleWordUpper → all lower)
    - [ ] `ToCamelCase("MAX_SIZE")` → `"maxSize"` (SCREAMING → camelCase)
    - [ ] `ToConstantCase("HTTP")` → `"Http"` (SingleWordUpper normalized)
    - [ ] `ToEnumMemberName("RED")` → `"Red"`
    - [ ] `ToEnumMemberName("DARK_BLUE")` → `"DarkBlue"`
- [ ] Run full test suite: `dotnet test`
- [ ] Fix broken integration tests — the main impact will be:
  - `.expected` files where constant names change from `MAX_SIZE` → `MaxSize`
  - `.expected.cs` snapshot files with SCREAMING_SNAKE_CASE output
  - Use: `grep -r "MAX_SIZE\|DEFAULT_\|SCREAMING" src/Sharpy.Compiler.Tests/Integration/TestFixtures/ --include="*.expected*"` to find candidates
- [ ] Regenerate C# snapshots: `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`

### Phase 3 verification

- [ ] `dotnet build sharpy.sln`
- [ ] `dotnet test` — all pass after updates
- [ ] `dotnet run --project src/Sharpy.Cli -- emit csharp snippets/hello.spy` — sanity check
- [ ] Manually test a file with constants: `dotnet run --project src/Sharpy.Cli -- emit csharp` on a snippet with `MAX_SIZE = 100` and verify it emits `MaxSize`

---

## Phase 4: Move TransformEnumMemberName into NameMangler

> **Goal**: Small refactor — delete the private `TransformEnumMemberName` from RoslynEmitter and wire callers to the `NameMangler.ToEnumMemberName` added in Phase 1d.

### 4a. Wire up `ToEnumMemberName` in call sites

**Checklist**:

- [ ] **File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`
  - Line 701: Change `TransformEnumMemberName(member.Name)` → `NameMangler.ToEnumMemberName(member.Name)`
  - Lines 715-731: Delete the private `TransformEnumMemberName` method entirely
- [ ] **File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`
  - Line 805: Change `TransformEnumMemberName(memberAccess.Member)` → `NameMangler.ToEnumMemberName(memberAccess.Member)`
  - **Note**: The local method reference `TransformEnumMemberName` is defined in the `TypeDeclarations` partial class. After deleting it there, this call in `Expressions` will fail to compile — that's the signal to update it.

### 4b. Add tests

**Checklist**:

- [ ] **File**: `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs`
  - Add a new `#region Enum Member Name Tests` section:
    - [ ] `ToEnumMemberName("RED")` → `"Red"`
    - [ ] `ToEnumMemberName("DARK_BLUE")` → `"DarkBlue"`
    - [ ] `ToEnumMemberName("MAX_RETRY_COUNT")` → `"MaxRetryCount"`
    - [ ] `ToEnumMemberName("already_lower")` → `"AlreadyLower"` (enum members are typically SCREAMING but should handle lowercase)
    - [ ] `ToEnumMemberName("`ExactName`")` → `"ExactName"` (backtick passthrough)
    - [ ] `ToEnumMemberName("")` → `""` (edge case)
  - Add a `Transform` test: `Transform("RED", NameContext.EnumMember)` → `"Red"`

### Phase 4 verification

- [ ] `dotnet build sharpy.sln` — verify no compile errors (the deleted method must not be referenced elsewhere)
- [ ] `dotnet test`
- [ ] Grep for any remaining `TransformEnumMemberName` references: should find zero outside tests/docs

---

## Phase 5: Extract Dunder Mapping from NameMangler (most delicate)

> **Goal**: Remove dunder method mapping from `NameMangler.ToPascalCase` and have callers use `DunderMapping` directly. After this phase, `NameMangler.ToPascalCase("__str__")` returns `"__str__"` (passthrough), not `"ToString"`.
>
> **This is the most delicate phase** because dunder name resolution flows through two paths:
> 1. **Precomputed** via `CodeGenInfoComputer` → stored in `CodeGenInfo.CSharpName` → read by RoslynEmitter
> 2. **Direct** via `NameMangler.Transform()` calls in RoslynEmitter (fallback when CodeGenInfo isn't available)
>
> Both paths must be updated.

### 5a. Remove dunder handling from `ToPascalCase`

**File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

**Checklist**:

- [ ] Remove the dunder bridge block added in Phase 3 (the `DunderMapping.GetCSharpName() ?? DunderMapping.TransformUnknownDunder()` call inside `ToPascalCase`)
  - After removal, a dunder name hitting `ToPascalCase` will be classified by `NameFormDetector.Detect()` as... what? The dunder will have been caught by the `__` prefix check (step 3 of the algorithm). Wait — `__str__` starts with `__` but also ends with `__`. The prefix check should NOT catch dunders (step 3 says "starts `__`, does NOT end `__`"). So `__str__` falls through to form detection → `NameFormDetector.Detect("__str__")` → `Dunder` → but there's no handler in step 6 for `Dunder` anymore. **Solution**: Add a `Dunder` case to step 6 that returns the name as-is (passthrough).
- [ ] Remove the `_dunderMethodMap` dictionary (lines 30-46)
- [ ] Remove the `#if DEBUG` static constructor (lines 61-76)
- [ ] Remove the `GetDunderMethodMapping` method (lines 269-272)
- [ ] For `IsDunderMethod` (line 261): Either delegate to `DunderMapping.IsDunderMethod` or remove entirely
  - **Decision guideline**: If there are callers outside `CodeGen/` that shouldn't depend on `DunderMapping`, keep `IsDunderMethod` as a thin wrapper. If all callers are in `CodeGen/`, remove and update callers. Check with grep: `grep -rn "NameMangler.IsDunderMethod" src/`
  - Current callers: `RoslynEmitter.ClassMembers.cs:60,78` — both in CodeGen. **Remove from NameMangler; update callers to use `DunderMapping.IsDunderMethod`.**
- [ ] Keep `_listMethodMap` and `GetListMethodMapping` — these are unrelated to this refactor

### 5b. Wire `DunderMapping` into `CodeGenInfoComputer`

> **Rationale**: `CodeGenInfoComputer` precomputes the `CSharpName` for all symbols during semantic analysis. For dunder methods, it must use `DunderMapping` instead of `NameMangler.ToPascalCase`.

**Checklist**:

- [ ] **File**: `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs`
  - Add `using Sharpy.Compiler.CodeGen;` at the top (for `DunderMapping`)
  - **Line 301** (`ProcessMethodDef`): Replace:
    ```csharp
    // Before:
    CSharpName = NameMangler.ToPascalCase(funcDef.Name),
    // After:
    CSharpName = DunderMapping.IsDunderMethod(funcDef.Name)
        ? (DunderMapping.GetCSharpName(funcDef.Name) ?? DunderMapping.TransformUnknownDunder(funcDef.Name))
        : NameMangler.ToPascalCase(funcDef.Name),
    ```
  - **Line 315** (`ProcessFunctionDef`): Same pattern — top-level functions shouldn't be dunders, but be defensive:
    ```csharp
    CSharpName = DunderMapping.IsDunderMethod(funcDef.Name)
        ? (DunderMapping.GetCSharpName(funcDef.Name) ?? DunderMapping.TransformUnknownDunder(funcDef.Name))
        : NameMangler.ToPascalCase(funcDef.Name),
    ```

> **Fork-in-the-road**: Should we extract a helper like `DunderMapping.ResolveName(string name)` that encapsulates the `IsDunder ? (GetCSharpName ?? TransformUnknown) : null` pattern? **Yes** — this pattern appears 5+ times. Add a convenience method:
> ```csharp
> public static string? ResolveCSharpName(string name)
> {
>     if (!IsDunderMethod(name)) return null;
>     return GetCSharpName(name) ?? TransformUnknownDunder(name);
> }
> ```
> Then call sites become: `CSharpName = DunderMapping.ResolveCSharpName(funcDef.Name) ?? NameMangler.ToPascalCase(funcDef.Name)`

### 5c. Wire `DunderMapping` into RoslynEmitter

**Checklist**:

- [ ] **File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`
  - Line 60: `NameMangler.IsDunderMethod(fd.Name)` → `DunderMapping.IsDunderMethod(fd.Name)`
  - Line 78: `NameMangler.IsDunderMethod(funcDef.Name)` → `DunderMapping.IsDunderMethod(funcDef.Name)`
  - Line 331 (`GenerateClassMethod`): The call `NameMangler.Transform(func.Name, NameContext.Method)` now returns `__str__` for dunders (passthrough). Fix:
    ```csharp
    var mangledName = DunderMapping.ResolveCSharpName(func.Name)
        ?? NameMangler.Transform(func.Name, NameContext.Method);
    ```
    **Important**: Most class methods will already have a `CSharpName` set via `CodeGenInfo` (from CodeGenInfoComputer). The `NameMangler.Transform` fallback here is for cases where CodeGenInfo is missing. But to be safe, handle dunders correctly in the fallback path too.
  - Line 604 (`GenerateInterfaceMethod`): Same pattern as line 331

- [ ] **File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs`
  - Lines 109, 155, 188: These generate operator overloads that delegate to the instance method. The instance method name for `__add__` should be `__Add__` (the unknown dunder transform). Update:
    ```csharp
    var methodName = DunderMapping.ResolveCSharpName(funcDef.Name)
        ?? NameMangler.Transform(funcDef.Name, NameContext.Method);
    ```
    - For `__add__`: `ResolveCSharpName` returns `null` (not in map) → wait, `TransformUnknownDunder("__add__")` returns `"__Add__"`. So `ResolveCSharpName` would return `"__Add__"` (it tries `GetCSharpName` first → null, then `TransformUnknownDunder` → `"__Add__"`). **Correct**.
    - For `__eq__`: `ResolveCSharpName` returns `"Equals"`. But operator `==` delegates to the `__Eq__` instance method, not `Equals`. Wait — let me re-check. Looking at `RoslynEmitter.Operators.cs:109`, the operator overload calls the *instance method* by name. The instance method for `__eq__` is named `Equals` (from the dunder map). So `operator ==(left, right)` calls `left.Equals(right)`. **This is correct behavior.**

    **But wait**: For operators like `__add__` that are NOT in the dunder map, the instance method is named `__Add__`. So `operator +(left, right)` calls `left.__Add__(right)`. The `GenerateClassMethod` (line 331) emits the method as `__Add__` because `ProcessMethodDef` in CodeGenInfoComputer sets `CSharpName = DunderMapping.TransformUnknownDunder("__add__")` → `"__Add__"`. **Consistent.**

> **Safety check**: After this phase, `NameResolutionService.ResolveBySymbolKind` (line 275-276) has `NameMangler.ToPascalCase(symbol.Name)` for functions and `NameMangler.ToPascalCase(symbol.Name)` for types. This is the fallback when CodeGenInfo is unavailable. For dunder methods, CodeGenInfo should ALWAYS be available (set by CodeGenInfoComputer). If it's not, the fallback would now return `__str__` instead of `ToString`. **Verify**: grep for cases where a FunctionSymbol with a dunder name might not have CodeGenInfo set. If CodeGenInfoComputer processes all method definitions, this should be safe.

### 5d. Update tests

**Checklist**:

- [ ] **File**: `src/Sharpy.Compiler.Tests/CodeGen/RegistryConsistencyTests.cs`
  - Rewrite to test `DunderMapping` instead of `NameMangler.Transform` for dunder→C# mappings
  - Test that `DunderMapping.GetCSharpName` returns the expected value for each dunder in `ProtocolRegistry`
  - Test that `DunderMapping.IsDunderMethod` recognizes all protocol dunders
- [ ] **File**: `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs`
  - Remove or update dunder mapping tests in `#region Dunder Method Tests`:
    - `ToPascalCase("__init__")` → now returns `"__init__"` (passthrough), not `"Constructor"`
    - `ToPascalCase("__str__")` → now returns `"__str__"`, not `"ToString"`
    - `ToPascalCase("__add__")` → now returns `"__add__"`, not `"__Add__"`
    - Move these tests to `DunderMappingTests.cs` to test the correct API
  - Remove `GetDunderMethodMapping` tests (method no longer exists on NameMangler)
  - Keep `IsDunderMethod` tests but update to test `DunderMapping.IsDunderMethod` instead
- [ ] Run full test suite
  - The dunder method C# names should still flow correctly through `CodeGenInfo.CSharpName`
  - Any integration test that compiles a class with `__str__`, `__eq__`, etc. should still produce the correct C# output because CodeGenInfoComputer now uses DunderMapping directly

### Phase 5 verification

- [ ] `dotnet build sharpy.sln`
- [ ] `dotnet test` — all pass
- [ ] `dotnet run --project src/Sharpy.Cli -- emit csharp` on a snippet with a class using `__str__`, `__eq__`, `__add__` — verify the emitted C# has `ToString()`, `Equals()`, `__Add__()`
- [ ] Grep for any remaining `NameMangler.*dunder` or `NameMangler.*Dunder` references (should be zero except comments)

---

## Phase 6: Add NamingConventionValidator

> **Goal**: Add a new validator that warns about identifiers with consecutive underscores (which would produce name collisions or be passed through as `Unrecognized`).

### 6a. Create validator

**New file**: `src/Sharpy.Compiler/Semantic/Validation/NamingConventionValidator.cs`

**Checklist**:

- [ ] Create class extending `SemanticValidatorBase`:
  ```csharp
  internal sealed class NamingConventionValidator : SemanticValidatorBase
  {
      public override string Name => "NamingConvention";
      public override int Order => 55; // After ModuleLevelValidator (50), before DecoratorValidator (60)
  }
  ```
- [ ] Implement `Validate(Module module, SemanticContext context)`:
  - Walk `module.Body` checking names on:
    - [ ] `FunctionDef` — check `funcDef.Name`
    - [ ] `ClassDef` — check `classDef.Name`
    - [ ] `StructDef` — check `structDef.Name`
    - [ ] `InterfaceDef` — check `interfaceDef.Name`
    - [ ] `EnumDef` — check `enumDef.Name` and each member name
    - [ ] `VariableDeclaration` — check `varDecl.Name`
    - [ ] Parameters on function/method defs — check `param.Name`
  - For class/struct/interface bodies, recurse into their `Body` statements to check nested definitions
- [ ] Name checking logic:
  - Skip backtick-escaped names (start and end with `` ` ``)
  - Skip dunder names (`__name__` pattern) — consecutive underscores in bookends are expected
  - Strip `_`/`__` prefix and trailing underscores
  - Check body with `NameFormDetector.HasConsecutiveUnderscores()`
  - If found, emit warning using `AddWarning()`:
    ```csharp
    AddWarning(context,
        $"Identifier '{name}' contains consecutive underscores, which may cause name mangling collisions. Use backtick escaping or rename.",
        line: node.LineStart,
        column: node.ColumnStart,
        code: DiagnosticCodes.Validation.NamingConventionWarning,
        span: node.Span);
    ```

> **Fork-in-the-road**: Should this validator walk into function bodies (checking local variable names)? **Decision**: Yes — local variables with consecutive underscores also collide after mangling. But only check *declarations*, not *references* (to avoid duplicate warnings).

> **Fork-in-the-road**: Should this be an error or warning? **Decision**: Warning (SPY0453). Users can suppress with `--nowarn SPY0453` or `<NoWarn>SPY0453</NoWarn>` in `.spyproj`. It's not a hard error because the code still compiles — just with potential confusion.

### 6b. Register in pipeline

**Checklist**:

- [ ] **File**: `src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs`
  - Add to the `CreateDefault` method, between ModuleLevelValidator and DecoratorValidator:
    ```csharp
    .AddValidator(new ModuleLevelValidator())       // Order: 50
    .AddValidator(new NamingConventionValidator())   // Order: 55 (naming convention warnings)
    .AddValidator(new DecoratorValidator())          // Order: 60
    ```
  - **Do NOT** add to `CreateMinimal()` or `CreateFast()` — this is a nice-to-have warning, not a correctness check

### 6c. Tests

**Checklist**:

- [ ] **New file**: `src/Sharpy.Compiler.Tests/Semantic/Validation/NamingConventionValidatorTests.cs`
  - Add `[Collection("Sequential")]` attribute
  - Test cases (use `IntegrationTestBase.CompileAndExecute` or construct a `SemanticContext` directly — follow the pattern of existing validator tests like `UnusedVariableValidatorTests`):
    - [ ] `foo__bar: int = 1` → warns (SPY0453)
    - [ ] `foo_bar: int = 1` → no warning
    - [ ] `def my__func(): pass` → warns
    - [ ] `def my_func(): pass` → no warning
    - [ ] Dunder `def __init__(self): pass` → no warning (dunder exemption)
    - [ ] Private `_private_var: int = 1` → no warning
    - [ ] Backtick `` `foo__bar`: int = 1 `` → no warning (backtick exemption)
    - [ ] Class with consecutive underscores in name → warns
    - [ ] Enum member with consecutive underscores → warns

- [ ] **New integration fixture**: Create test fixture files
  - `src/Sharpy.Compiler.Tests/Integration/TestFixtures/naming_convention_warning.spy`:
    ```python
    foo__bar: int = 1
    ```
  - `src/Sharpy.Compiler.Tests/Integration/TestFixtures/naming_convention_warning.warning`:
    ```
    consecutive underscores
    ```
    (Substring match — the `.warning` file lines are expected substrings of warning messages)

### Phase 6 verification

- [ ] `dotnet build sharpy.sln`
- [ ] `dotnet test` — all pass including new tests
- [ ] Test warning suppression: verify that `--nowarn SPY0453` suppresses the warning
- [ ] Verify that existing code with consecutive underscores (if any in test fixtures) either doesn't trigger the warning (because it's in the right context) or that the warning is expected

---

## Phase 7: Update Spec

> **Goal**: Update the language specification to match the new implementation behavior.

**File**: `docs/language_specification/name_mangling.md`

**Checklist**:

- [ ] Update the overview table (line 7-14):
  - Constants row already says `PascalCase` — verify it matches. Currently says `CAPS_SNAKE_CASE constants | PascalCase | MAX_SIZE → MaxSize`. **This is already correct in the spec!** The implementation was deviating.
- [ ] Add a row for `camelCase` passthrough:
  - `camelCase identifiers | Preserved | httpClient → httpClient`
- [ ] Add a row for `__` private prefix:
  - `__name fields | __PascalCase | __private_field → __PrivateField`
- [ ] Update Step 1 table (line 23-27): Verify `__name` row says "Keep double `__`" and preservation is documented
- [ ] Add a section about `Unrecognized` forms:
  - Document that names with consecutive underscores (`foo__bar`) are passed through as-is with a compiler warning (SPY0453)
  - Document that mixed-case names with underscores (`Foo_bar`) are passed through as-is
- [ ] Add section documenting `camelCase` passthrough:
  - `httpClient` → `httpClient` (preserved in both PascalCase and camelCase contexts)
  - Rationale: camelCase names are already valid C# — mangling them destroys the author's intent
- [ ] Update Complete Examples table (line 77-91):
  - Add: `httpClient | Variable | httpClient (preserved)`
  - Add: `__private_count | Private field | __privateCount`
  - Verify all existing examples still match the new behavior
- [ ] Add note about SPY0453 warning:
  - Identifiers with consecutive underscores trigger a compiler warning
  - Use backtick escaping to suppress: `` `foo__bar` ``
- [ ] Update Implementation note at bottom (line 198-201): Remove or update implementation status markers

### Phase 7 verification

- [ ] Review the spec for consistency — all examples should match what `NameMangler` actually produces
- [ ] Optionally: run a few examples through the CLI to verify spec claims

---

## File Summary

| Phase | Modified | Created |
|-------|----------|---------|
| 1 | NameMangler.cs, DiagnosticCodes.cs, DiagnosticExplanations.cs | NameFormDetector.cs, DunderMapping.cs, NameFormDetectorTests.cs, DunderMappingTests.cs |
| 2 | NameMangler.cs, NameResolutionService.cs, RoslynEmitter.CompilationUnit.cs, NameManglerTests.cs | — |
| 3 | NameMangler.cs, CodeGenInfoComputer.cs, RoslynEmitter.{Expressions,Statements,ModuleClass,CompilationUnit}.cs, ExecutionOrderAnalyzer.cs, NameManglerTests.cs, ~N snapshots | — |
| 4 | RoslynEmitter.TypeDeclarations.cs, RoslynEmitter.Expressions.cs, NameManglerTests.cs | — |
| 5 | NameMangler.cs, CodeGenInfoComputer.cs, RoslynEmitter.{ClassMembers,Operators}.cs, RegistryConsistencyTests.cs, NameManglerTests.cs | — |
| 6 | ValidationPipelineFactory.cs | NamingConventionValidator.cs, NamingConventionValidatorTests.cs, fixture files |
| 7 | name_mangling.md | — |

## Verification

After each phase:
1. `dotnet build sharpy.sln` — must compile
2. `dotnet test` — all tests pass (or expected failures are fixed)
3. `dotnet run --project src/Sharpy.Cli -- emit csharp snippets/hello.spy` — sanity check

After all phases:
- Run `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"` to regenerate any remaining stale C# snapshots
- Run `dotnet format whitespace` before committing
- Manually verify a few `.spy` files that use constants, dunders, private members, and camelCase names
