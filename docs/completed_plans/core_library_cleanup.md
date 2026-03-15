<!-- Verified by /verify-plan on 2026-03-13 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Sharpy.Core Library Cleanup & Consistency

## Context

The Sharpy.Core standard library has grown organically and needs a consistency pass before adding more modules. Key issues:
- Inconsistent `[SharpyModule]` placement (some in `__Init__.cs`, some in implementation files)
- Large files that could benefit from partial class splitting for context window friendliness
- API surface that mixes nullable returns and exception throwing when Optional/Result types are available
- Print function split across multiple files with redundant overloads
- Builtins module organization questions (namespace vs top-level)
- XML doc comment gaps and inconsistencies

This plan establishes a **consistent, safe, Python-familiar** foundation aligned with Sharpy's axioms before expanding the library.

## Current State

### Module annotation inconsistency
- **9 modules have `__Init__.cs`** with `[SharpyModule]`: Argparse, Json, Math, Operator, Os, Pathlib, Random, Re, Sys
- **4 modules are missing `__Init__.cs`**: Builtins (attr in `Builtins.cs`), Collections (attr in `Collections.cs`), Datetime (attr in `Datetime.cs`), Itertools (attr in `Additional.cs`)

### Large files needing splitting
| File | Lines | Notes |
|------|-------|-------|
| `StringExtensions.cs` | 1,744 | 10 logical sections, 62 methods | [CORRECTED: grep count yields 62 public static methods, not 63+]
| `Dict.cs` | 631 | Could split into partials |
| `Argparse/ArgumentParser.cs` | 586 | Moderate |
| `Math/Math.cs` | 468 | Moderate |
| `Json/JsonParser.cs` | 460 | Moderate |

### Print function organization
- `Builtins/Builtins.cs`: `Print(params object?[] values)` + `PrintWithOptions()` + `FormatValue()` (119 lines)
- `Print.cs`: `Print(PrintArguments<T>)` overload + `_Print()` helper + `PrintArguments<T>` class (95 lines)
- Two separate files with different printing approaches — `Builtins.cs` inlines logic, `Print.cs` uses `_Print()` helper [CORRECTED: `_Print()` only exists in `Print.cs`, not duplicated in `Builtins.cs`]

### API consistency gaps
- `Dict.Get(K key)` returns `Optional<V>` — un-Pythonic, Python's `dict.get()` returns `V | None`
- `Os.GetEnv(string key)` returns `string?` — correct Pythonic pattern (returns None when missing)
- Stdlib mixes Optional returns with nullable returns inconsistently
- Optional/Result types exist but are user-facing power tools, not stdlib patterns

### Builtins auto-import
- Builtins are available via `using global::Sharpy;` in every compilation unit
- Builtin calls emit as `global::Sharpy.Builtins.MethodName()` — the `Builtins` class qualifier is hardcoded in the emitter
- **Decision needed**: Keep `Builtins` as a static class (current) vs. promote builtins to top-level namespace functions

## Design Decisions

### D1: Keep `Builtins` as a static class container
**Rationale**: The emitter already hardcodes `global::Sharpy.Builtins.X()` for all builtin calls. Moving builtins to top-level would mean standalone static methods in the `Sharpy` namespace, which isn't possible in C# 9.0 (top-level statements aren't static methods). A static class is the idiomatic C# pattern. The `Builtins` qualifier is invisible to Sharpy users — it's a codegen detail. **No change needed.**

### D2: Standardize `__Init__.cs` pattern for all modules
Every module directory gets a `__Init__.cs` with the `[SharpyModule]` attribute. The attribute is *removed* from implementation files. This mirrors Python's `__init__.py` and makes module registration discoverable.

### D3: Python-style stdlib, user-facing safety types
**Philosophy**: Sharpy inherits from Python (not inherently safe) running on .NET (not Rust). The stdlib should mirror Python's patterns — exceptions and nullable returns — so it feels familiar to Python developers. Optional and Result exist as first-class types that users can adopt in *their own* APIs when they want Rust-like safety.

- **Exceptions everywhere Python uses them**: `TypeError`, `ValueError`, `KeyError`, `IndexError`, etc. — all Python-style exceptions stay as-is
- **Nullable for "might not exist"**: `dict.get(key)` returns `V?` (not `Optional<V>`), `os.getenv()` returns `str?` — matches Python returning `None`
- **Optional/Result are user tools**: Available as first-class types with compiler support (pattern matching, type narrowing), but not imposed by the stdlib
- **Trade-off acknowledged**: Sharpy can't be as safe as Rust while staying familiar to Python developers. The compromise is: inherit Python patterns, but provide safer building blocks for users who choose them
- **Implicit conversions bridge the gap**: `Optional<T>` is implicitly convertible from `T` (null → None, value → Some), so users can gradually adopt safety types without friction

### D4: Print consolidation approach
Merge all Print-related code into a single file `Builtins/Print.cs`. The `PrintArguments<T>` overload can be deprecated/removed if the compiler no longer emits it. The `*values` + keyword-only parameter pattern (`def print(*values, /, sep=" ", ...)`) already works in Sharpy's compiler — the C# side just needs `params object?[]` with named parameters.

### D5: File splitting threshold
Files over **400 lines** get split into partial classes along logical boundaries. This improves context window usage and code navigation.

### D6: XML doc comment standard
All public API members must have:
- `<summary>` — what it does (one sentence)
- `<param>` — for each parameter
- `<returns>` — what it returns
- `<remarks>` — Python equivalent syntax (when different from Sharpy)
- `<example>` — for non-obvious usage (optional but encouraged for complex methods)
- `<exception>` — for thrown exceptions with conditions

This feeds directly into the LSP hover/completion via `SymbolFormatter.FormatSymbolWithDocs()`.

## Implementation

### Phase 1: Module Annotation Standardization

**Goal:** Every module directory has a `__Init__.cs` with `[SharpyModule]` and the attribute is removed from implementation files.

#### Tasks

1. **Create `Builtins/__Init__.cs`** — `src/Sharpy.Core/Builtins/`
   - Create `__Init__.cs` with `[SharpyModule("builtins")]` on `public static partial class Builtins`
   - Remove `[SharpyModule("builtins")]` from `Builtins/Builtins.cs`
   - Verify the partial class declaration matches (same namespace, same modifiers)
   - Commit: `refactor(core): move SharpyModule annotation to __Init__.cs for builtins`

2. **Create `Collections/__Init__.cs`** — `src/Sharpy.Core/Collections/`
   - Create `__Init__.cs` with `[SharpyModule("collections")]` on `public static partial class Collections`
   - Remove `[SharpyModule("collections")]` from `Collections/Collections.cs`
   - Commit: `refactor(core): move SharpyModule annotation to __Init__.cs for collections`

3. **Create `Datetime/__Init__.cs`** — `src/Sharpy.Core/Datetime/`
   - Create `__Init__.cs` with `[SharpyModule("datetime")]`
   - Remove `[SharpyModule("datetime")]` from `Datetime/Datetime.cs`
   - Determine class name pattern: use `DatetimeModule` (following `PathlibModule`, `ArgparseModule` convention) since `Datetime` is already a type name in the file
   - Commit: `refactor(core): move SharpyModule annotation to __Init__.cs for datetime`

4. **Create `Itertools/__Init__.cs`** — `src/Sharpy.Core/Itertools/`
   - Create `__Init__.cs` with `[SharpyModule("itertools")]`
   - Remove `[SharpyModule("itertools")]` from `Itertools/Additional.cs`
   - Use `ItertoolsModule` or match existing class name pattern
   - Commit: `refactor(core): move SharpyModule annotation to __Init__.cs for itertools`

5. **Verify all modules** — Run `dotnet build` and `dotnet test` to ensure module discovery still works
   - Verify that `CachedModuleDiscovery` / `BuiltinRegistry` still finds all modules
   - Run file-based integration tests that use `import math`, `import os`, etc.
   - Commit: (no commit, verification only)

### Phase 2: Print Function Consolidation

**Goal:** Single-file Print implementation, remove redundant overloads.

#### Tasks

1. **Audit Print usage in codegen** — `src/Sharpy.Compiler/CodeGen/`
   - Search for how the emitter generates Print calls
   - Determine if `PrintArguments<T>` is still emitted anywhere
   - Check if any test fixtures rely on `PrintArguments<T>` directly
   - (Research only, no commit)

2. **Consolidate Print into `Builtins/Print.cs`** — `src/Sharpy.Core/Builtins/Print.cs`, `src/Sharpy.Core/Print.cs`
   - Move `Print(params object?[])`, `PrintWithOptions()`, `FormatValue()` from `Builtins/Builtins.cs` to new `Builtins/Print.cs`
   - Move `_Print()` helper from root `Print.cs` to `Builtins/Print.cs`
   - If `PrintArguments<T>` is unused by codegen, deprecate with `[Obsolete]` or remove
   - Delete root `Print.cs` (or keep only `PrintArguments<T>` if still needed)
   - Ensure `Builtins/Builtins.cs` only contains non-Print builtin functions (`Len`, `FormatValue` if not Print-specific, etc.)
   - Commit: `refactor(core): consolidate Print implementation into Builtins/Print.cs`

3. **Verify Print behavior** — Run print-related test fixtures
   - `dotnet test --filter "DisplayName~print"` or `DisplayName~Print`
   - Ensure `print("hello")`, `print(1, 2, sep=",")`, `print(x, end="")` all work
   - Commit: (no commit, verification only)

### Phase 3: Large File Splitting

**Goal:** Split files over 400 lines into partial classes along logical boundaries.

#### Tasks

1. **Split `StringExtensions.cs` (1,744 lines)** — `src/Sharpy.Core/`
   - Create `Partial.String/` directory (following existing `Partial.List/`, `Partial.Set/` convention)
   - Split into logical partials:
     - `StringExtensions.cs` — Core methods (Upper, Lower, Strip, Capitalize, etc.)
     - `StringExtensions.CaseFolding.cs` — Case-fold table and implementation (~600 lines)
     - `StringExtensions.Split.cs` — Split, Rsplit, Partition, Rpartition
     - `StringExtensions.Search.cs` — Find, Rfind, Index, Rindex, Count, Startswith, Endswith
     - `StringExtensions.Format.cs` — Expandtabs, Maketrans, Translate, encoding
   - All files: `public static partial class StringExtensions` in `namespace Sharpy`
   - Commit: `refactor(core): split StringExtensions into partial files under Partial.String/`

2. **Split `Dict.cs` (631 lines)** — `src/Sharpy.Core/`
   - Create `Partial.Dict/` directory
   - Split into:
     - `Dict.cs` — Class declaration, constructors, indexers, core properties
     - `Dict.Methods.cs` — Python dict methods (get, keys, values, items, pop, update, etc.)
     - `Dict.Interfaces.cs` — Interface implementations (IDictionary, IEnumerable, ISized, etc.)
   - Commit: `refactor(core): split Dict into partial files under Partial.Dict/`

3. **Split `Math/Math.cs` (468 lines)** — `src/Sharpy.Core/Math/`
   - Split into:
     - `Math.cs` — Constants and basic functions (abs, ceil, floor, etc.)
     - `Math.Trigonometry.cs` — Trig functions (sin, cos, tan, etc.)
     - `Math.Advanced.cs` — Advanced functions (factorial, gcd, lcm, comb, perm, etc.)
   - Commit: `refactor(core): split Math module into partial files`

4. **Evaluate remaining large files** — Decide on `ArgumentParser.cs` (586), `JsonParser.cs` (460), `Path.cs` (419)
   - These are borderline at 400-600 lines. Split only if they have clear logical sections.
   - `JsonParser.cs` and `JsonSerializer.cs` are separate concerns already, likely fine as-is
   - `ArgumentParser.cs` could split into `ArgumentParser.cs` + `ArgumentParser.Parsing.cs`
   - `Path.cs` could split into `Path.cs` + `Path.Operations.cs`
   - Commit: `refactor(core): split remaining large module files into partials`

### Phase 4: API Consistency — Python-Style Stdlib

**Goal:** Align stdlib API to use Python patterns (nullable + exceptions). Remove `Optional<T>` from stdlib return types. Optional/Result remain available as user-facing types.

#### Tasks

1. **Convert `Dict.Get(K key)` from `Optional<V>` to `V?`** — `src/Sharpy.Core/Dict.cs`
   - Change return type from `Optional<V>` to `V?` (nullable)
   - Python's `dict.get(key)` returns `None` when key is missing — match that behavior
   - **[WARNING: C# 9.0 unconstrained generic nullable limitation]** `Dict<K, V>` has unconstrained `V`. In C# 9.0, `V?` for unconstrained generics does NOT produce `Nullable<V>` for value types — it stays `V` with a nullable annotation. This means `return default;` would return `0` for `Dict<str, int>.Get("missing")` instead of a proper None/null. Options: (a) add `where V : class` constraint (breaks value-type dicts), (b) keep `Optional<V>` for this method (current, correct), (c) use separate overloads/attribute approach. **Needs architectural decision.**
   - Update `Dict.Get(K key, V @default)` if needed (this one already returns `V`, should be fine)
   - `ClrTypeMapper` already handles nullable types correctly (lines 73-81, 209-216) — no changes needed there
   - Update all tests that assert on `Optional<V>` return from `Dict.Get()`
   - Commit: `refactor(core): dict.get() returns nullable instead of Optional to match Python`

2. **Audit all `Optional<T>` returns in stdlib** — `src/Sharpy.Core/`
   - Search for methods returning `Optional<T>` in public API
   - Convert each to `T?` (nullable) to match Python semantics
   - Internal/private usage of Optional is fine
   - Commit: `refactor(core): replace Optional returns with nullable in stdlib API`

3. **Verify `os.getenv` stays nullable** — `src/Sharpy.Core/Os/Os.cs`
   - Confirm `Os.GetEnv(string key)` returns `string?` — this is already correct Python behavior
   - No change needed, just verify
   - (No commit, verification only)

4. **Add `<exception>` XML doc tags** — `src/Sharpy.Core/`
   - For methods that throw, add `<exception cref="ValueError">...</exception>` tags
   - This helps LSP users understand what exceptions to expect
   - Priority files: `StringExtensions`, `Dict`, `List`, `Math`, `Random`
   - Commit: `docs(core): add exception documentation to public API methods`

5. **Document Optional/Result as user-facing types** — `src/Sharpy.Core/Optional.cs`, `Result.cs`
   - Update XML docs to position these as opt-in safety types for user code
   - Add `<remarks>` explaining: "Use Optional/Result in your own APIs for explicit error handling. The Sharpy stdlib uses Python-style nullable returns and exceptions."
   - Include examples of when and how to use them in user code
   - Commit: `docs(core): position Optional/Result as user-facing safety types`

### Phase 5: XML Documentation Enhancement

**Goal:** Ensure all public API members have complete, LSP-friendly XML documentation.

#### Tasks

1. **Audit doc comment completeness** — `src/Sharpy.Core/`
   - Run a search for public methods/properties missing `<summary>` tags
   - Categorize gaps by file/module
   - (Research only, no commit)

2. **Add missing docs to builtin functions** — `src/Sharpy.Core/Builtins/`
   - Ensure all builtins (print, len, range, enumerate, zip, map, filter, etc.) have:
     - `<summary>` with Python equivalent
     - `<param>` for each parameter
     - `<returns>` description
     - `<example>` with Sharpy usage
   - Commit: `docs(core): complete XML documentation for builtin functions`

3. **Add missing docs to collection types** — `src/Sharpy.Core/Partial.List/`, `Partial.Set/`, `Partial.Dict/`
   - Fill gaps in List, Set, Dict method documentation
   - Ensure `<remarks>` notes Python equivalent behavior where it differs
   - Commit: `docs(core): complete XML documentation for collection types`

4. **Add missing docs to module functions** — All module directories
   - Priority: Math, Os, Random, Json (most commonly used)
   - Ensure parameter types and return types are documented
   - Add `<example>` blocks for non-obvious usage
   - Commit: `docs(core): complete XML documentation for stdlib modules`

5. **Add docs to Optional and Result** — `src/Sharpy.Core/Optional.cs`, `Result.cs`
   - These are key safety types — documentation should be comprehensive
   - Include pattern matching examples, unwrap vs safe access patterns
   - Note: These already have good docs, enhance with more examples
   - Commit: `docs(core): enhance Optional and Result documentation with usage examples`

### Phase 6: Cleanup & Verification

**Goal:** Final consistency pass and full test suite verification.

#### Tasks

1. **Remove dead code** — `src/Sharpy.Core/`
   - Check for unused internal methods, commented-out code, TODO comments without issues
   - For any TODO/FIXME found without issue references, create GitHub issues
   - Commit: `chore(core): remove dead code and link TODOs to issues`

2. **Verify naming conventions** — `src/Sharpy.Core/`
   - Ensure all public methods follow PascalCase (C# convention)
   - Ensure Python-facing API names match Python equivalents (snake_case mapped via NameMangler)
   - Commit: `fix(core): align naming conventions`

3. **Full test suite run** — Verify no regressions
   - `dotnet test` — all 8,747+ tests must pass
   - `dotnet format whitespace --verify-no-changes` — formatting check
   - Commit: (no commit, verification only)

4. **Run dogfooding** — Test with real Sharpy programs
   - `/dogfood-run` to verify no regressions in practical usage
   - Commit: (no commit, verification only)

## Testing Strategy

- **No new test fixtures needed** for Phase 1-3 (refactoring, no behavior change)
- **Phase 4**: Update test fixtures that use `dict.get()` and assert on `Optional<V>` — these now return `V?`. Update any `.spy` fixtures that test `dict.get()` return type behavior.
- **Phase 5**: No code changes, documentation only
- **Phase 6**: Full regression suite
- **Edge cases**: Ensure module discovery works after `__Init__.cs` changes (critical path)
- **Negative tests**: Verify that removed `PrintArguments<T>` doesn't break compilation of existing code (if removed)

## Open Questions for Discussion

1. **PrintArguments<T> deprecation**: Not used by anyone — remove outright. *(Resolved)*

2. **API philosophy**: Stdlib uses Python patterns (exceptions + nullable). Optional/Result are user-facing opt-in safety types. *(Resolved)*

3. **Partial.String/ naming**: Use `Partial.String/` — the convention extends to any type needing partials, not just Sharpy wrapper types. *(Resolved)*

4. **FormatAlign.cs scope**: Leave in `Builtins/` — it's small and correctly placed. *(Resolved)*

## Issues to Close

- No existing GitHub issues identified for this work (GitHub API was unavailable during planning). Search for related issues before starting implementation.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-03-13
**Plan file:** `docs/implementation_planning/core_library_cleanup.md`

### Corrections Made
1. **Line counts** — StringExtensions.cs has 62 public static methods, not "63+" (changed inline)
2. **Print redundancy claim** — `_Print()` only exists in `Print.cs`, not duplicated in `Builtins/Builtins.cs`. The two files use different approaches (inlined logic vs helper method), not "redundant `_Print()` logic" (changed inline)
3. **ClrTypeMapper update** — Plan originally said "Update compiler's ClrTypeMapper if it has special handling for `Optional<V>` returns from Dict". Verification shows ClrTypeMapper already handles both nullable and Optional types (lines 73-81 and 209-216) — no changes are needed in the mapper itself (changed inline)

### Warnings
1. **`V?` for unconstrained generics (Phase 4, Task 1)** — This is the most significant issue. `Dict<K, V>` has unconstrained `V`. In C# 9.0, `V?` on unconstrained generics does NOT wrap value types in `Nullable<V>` — it just annotates them. A `Dict<string, int>.Get("missing")` would return `default(int)` = `0`, not a proper null/None. The current `Optional<V>` approach is actually more correct for all type arguments. This needs an architectural decision before implementing Phase 4 Task 1.
2. **Breaking change scope** — Phase 4 Task 1 is a breaking change for any Sharpy code that pattern-matches on `Optional<V>` from `dict.get()`. The plan acknowledges test updates but should also consider if user-facing Sharpy programs would break.

### Missing Steps Added
- None required — the plan covers all necessary phases comprehensively.

### Unchecked Claims
- **"The `*values` + keyword-only parameter pattern already works in Sharpy's compiler"** (D4) — Not verified; would require testing the compiler's handling of `def print(*values, /, sep=" ", ...)` syntax.
- **SymbolFormatter LSP integration** (D6) — Verified that `SymbolFormatter.FormatSymbolWithDocs()` exists at `src/Sharpy.Lsp/SymbolFormatter.cs:27`, but not verified that XML doc comments flow through to LSP hover responses end-to-end.

### Verified Claims (All Accurate)
- 9 modules with `__Init__.cs`: All confirmed with exact locations and `[SharpyModule]` attributes
- 4 modules missing `__Init__.cs`: All confirmed with correct attribute locations (Builtins:12, Collections:286, Datetime:227, Itertools/Additional:289)
- File line counts: StringExtensions (1,744), Dict (631), ArgumentParser (586), Math (468), JsonParser (460), Path (419) — all exact
- 7 existing `Partial.*` directories: Complex, Iterator, List, ListIterator, ListReverseIterator, Set, SetIterator
- `Dict.Get(K key)` returns `Optional<V>` — confirmed at Dict.cs:124
- `Os.Getenv(string key)` returns `string?` — confirmed at Os.cs:176
- `Optional<T>` implicit conversion — confirmed at Optional.cs:84-90
- Emitter generates `global::Sharpy.Builtins.{PascalCase}()` for builtins — confirmed at RoslynEmitter.Expressions.Access.cs:128
- `CachedModuleDiscovery` and `BuiltinRegistry` exist and handle module discovery — confirmed
- `SharpyModuleAttribute` defined at `SharpyModuleAttribute.cs:10`
- Sharpy.Core targets `netstandard2.1;netstandard2.0` with `LangVersion 9.0` and `Nullable enable` — confirmed in .csproj
