<!-- Verified by /verify-plan on 2026-03-16 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Implementation: COMPLETE (2026-03-16) -->

# Stdlib Phase 2 Fixes, New Features, Test Infrastructure & LSP Cross-File Fix

## Context

This plan addresses 14 GitHub issues (#394–#407) that span stdlib Phase 2 gaps, compiler infrastructure fixes, new stdlib features, test infrastructure improvements, and an LSP cross-file navigation bug. Many issues are duplicates or tightly coupled — the plan deduplicates them and sequences work by dependency order.

**Issue Deduplication:**
- #394, #402, #404 are the same problem (const-only module imports) → grouped
- #395, #401, #405 are the same problem (CsvReader iteration) → grouped
- #400, #403 are the same problem (CopyModule reflection) → grouped

## Current State

- **Const-only modules**: `import string` fails with SPY0300 because `ImportResolver.TryResolveNetModule()` rejects modules with 0 functions and 0 types (line 768). `OverloadIndexBuilder` detects const fields but doesn't store them. 3 test fixtures skipped.
- **CsvReader iteration**: `CsvReader` implements `IEnumerable<List<string>>` (BCL type, not Sharpy), and `TypeInferenceService.InferIterableElementType()` doesn't extract element types from CLR `IEnumerable<T>` on `UserDefinedType` symbols. 1 test fixture skipped.
- **CopyModule**: Uses ~37 reflection call sites (`GetMethod`, `Invoke`, `Activator.CreateInstance`, `IsGenericType`, etc.) for deep copy. Fragile and slow. [CORRECTED: actual count is ~37, not ~68; located at `src/Sharpy.Core/Copy/CopyModule.cs`]
- **time module**: `gmtime()` and `localtime()` exist but don't accept an optional timestamp parameter.
- **heapq module**: All functions implemented except `merge()`.
- **csv module**: `reader()`/`writer()` work; `DictReader`/`DictWriter` not implemented.
- **functools**: `reduce()` and `cmp_to_key()` work; `partial` and `lru_cache` require significant compiler work (deferred).
- **Test infrastructure**: `.error` fixtures only test compile-time errors; no support for runtime error testing. 1 test fixture skipped.
- **LSP**: `textDocument/implementation` returns empty for cross-file scenarios due to `ReferenceEqualityComparer` in `TypeHierarchyIndex` causing reference identity mismatches.

## Design Decisions

1. **Const field discovery uses the same discovery pipeline as functions** — add `Fields` to `ModuleOverloads` and flow through `CachedModuleDiscovery` → `ModuleRegistry` → `ImportResolver`. This keeps the architecture consistent.

2. **IEnumerable<T> element type inference handled in TypeInferenceService** — when a `UserDefinedType` has a `ClrType` implementing `IEnumerable<T>`, extract `T` via reflection. This is the minimal fix that unblocks all `[SharpyModuleType]` classes with `IEnumerable<T>`.

3. **Interface-based deep copy (`IDeepCopyable`)** replaces reflection — each collection type implements `DeepCopy(Dictionary<object, object> memo)`, giving type-safe dispatch with zero reflection. C# 9.0 compatible.

4. **`.runtime-error` test fixture type** — extends existing file-based infrastructure. Compilation succeeds, execution fails, and error message substrings are matched against stderr. Simpler and more scalable than programmatic tests.

5. **LSP fix uses name-based canonicalization in `TypeHierarchyIndex.Build()`** — before inserting into the `_subtypes` dictionary, normalize `BaseType` and `InterfaceReference.Definition` references through a name→symbol lookup built from the current symbol table. This is minimally invasive and fixes the root cause.

6. **functools.partial and lru_cache are deferred** — `partial` needs a runtime callable wrapper; `lru_cache` needs decorator-to-wrapper codegen (not just attribute generation). Both require significant compiler architecture changes beyond the scope of this plan.

## Implementation

### Phase 1: Compiler Infrastructure — Const-Only Module Imports

**Goal:** Enable `import string` and `from string import ascii_letters` for modules that export only `const` fields.

#### Tasks

1. **Add `FieldSignature` and `Fields` to `ModuleOverloads`** — `src/Sharpy.Compiler/Discovery/Caching/OverloadIndex.cs`
   - Add `FieldSignature` record with `Name` (string), `FieldType` (Type), and `IsConst` (bool) properties
   - Add `Dictionary<string, FieldSignature> Fields` property to `ModuleOverloads`
   - Acceptance: `ModuleOverloads` has a `Fields` dictionary; `FieldSignature` record exists
   - Commit: `feat(compiler): add FieldSignature and Fields to ModuleOverloads`

2. **Discover and store fields in `OverloadIndexBuilder`** — `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs`
   - In `DiscoverModuleFunctions()`, after function discovery, enumerate `exportType.GetFields(BindingFlags.Public | BindingFlags.Static)` (filtering out compiler-generated `<` fields)
   - Create `FieldSignature` for each, store in `moduleOverloads.Fields`
   - The existing `hasPublicFields` check (line 56-58) already gates module registration; now the field data is preserved
   - Acceptance: `OverloadIndex.Modules["string"].Fields` contains all 9 string constants
   - Commit: `feat(compiler): discover and store const fields in OverloadIndexBuilder`

3. **Add `GetModuleFields()` to `CachedModuleDiscovery` and `ModuleRegistry`** — `src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs`, `src/Sharpy.Compiler/Semantic/Registry/ModuleRegistry.cs`
   - `CachedModuleDiscovery.GetModuleFields(string moduleName)`: look up `OverloadIndex.Modules[moduleName].Fields`, convert each `FieldSignature` to a `(string Name, SemanticType Type, bool IsConst)` tuple using `ClrTypeMapper`
   - `ModuleRegistry.GetModuleFields(string moduleName)`: delegate to `CachedModuleDiscovery.GetModuleFields()`
   - Acceptance: `ModuleRegistry.GetModuleFields("string")` returns 9 entries with correct names and `BuiltinType.Str` types
   - Commit: `feat(compiler): add GetModuleFields API to discovery and registry`

4. **Update `ImportResolver.TryResolveNetModule()` to accept const-only modules** — `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
   - Call `_moduleRegistry.GetModuleFields(moduleName)` alongside existing `GetModuleFunctions` and `GetModuleTypes` calls (around line 765)
   - Change early return condition (line 768) to: `if (functions.Count == 0 && types.Count == 0 && fields.Count == 0) return null;`
   - After the existing type export loop (line 798), add a field export loop creating `VariableSymbol` entries in `moduleInfo.ExportedSymbols` for each field (with appropriate `SemanticType` and `IsConst` flag)
   - Acceptance: `import string` resolves successfully; `string.digits` accessible; `from string import ascii_letters` works; `from string import nonexistent` produces SPY0302
   - Commit: `feat(compiler): support const-only module imports in ImportResolver`

5. **Remove skip files and verify test fixtures** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
   - Delete `stdlib_string.skip`, `stdlib_from_string.skip`, `stdlib_import_nonexistent_from_string.skip`
   - Run the 3 test fixtures and verify they pass
   - Acceptance: All 3 string module integration tests pass
   - Commit: `test: enable string module integration tests (#394, #402, #404)`

### Phase 2: Compiler Infrastructure — IEnumerable<T> Element Type Inference

**Goal:** Enable `for` loops over `[SharpyModuleType]` classes that implement `IEnumerable<T>`.

#### Tasks

6. **Add CLR IEnumerable<T> element type extraction to `TypeInferenceService`** — `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`
   - In `InferIterableElementType()`, after the existing `UserDefinedType` `__iter__()` method check (lines 644–660), add a fallback: if the `UserDefinedType` has a `ClrType`, check if it implements `IEnumerable<T>` via reflection, extract `T`, and map it to a `SemanticType` using `ClrTypeMapper` [CORRECTED: line reference updated from "around line 660" to "lines 644–660"]
   - Must handle nested Sharpy types: if `T` is `Sharpy.List<string>`, map to `GenericType("list", [BuiltinType.Str])`
   - Acceptance: `InferIterableElementType(csvReaderType)` returns `GenericType("list", [BuiltinType.Str])`
   - Commit: `feat(compiler): infer element type from IEnumerable<T> on CLR-backed UserDefinedTypes`

7. **Refactor CsvReader to yield `Sharpy.List<string>`** — `src/Sharpy.Core/Csv/CsvReader.cs`
   - CsvReader currently uses BCL `System.Collections.Generic.List<string>` (confirmed at lines 56, 112). Change the generic parameter from BCL `System.Collections.Generic.List<string>` to `Sharpy.List<string>` in the `IEnumerable<>` declaration and `GetEnumerator()` return type [CORRECTED: CsvReader does NOT already use Sharpy.List — refactoring is required, not just verification]
   - Update `ParseLine()` to return `Sharpy.List<string>` instead of BCL `List<string>`
   - Acceptance: `CsvReader` implements `IEnumerable<Sharpy.List<string>>`; unit tests pass
   - Commit: `fix(core): refactor CsvReader to yield Sharpy.List<string> (#401)`

8. **Remove skip file and verify CSV test fixture** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
   - Delete `stdlib_csv.skip`
   - Run `stdlib_csv` test fixture and verify it passes
   - Acceptance: CSV integration test passes with correct output
   - Commit: `test: enable CSV integration test (#395, #405)`

### Phase 3: Core Library — Replace Reflection in CopyModule

**Goal:** Eliminate all reflection from `CopyModule` deep copy using interface-based dispatch.

#### Tasks

9. **Add `IDeepCopyable` interface** — `src/Sharpy.Core/Copy/IDeepCopyable.cs` (new file) [CORRECTED: placed in Copy/ subdirectory alongside CopyModule.cs]
   - Define `public interface IDeepCopyable { object DeepCopy(System.Collections.Generic.Dictionary<object, object> memo); }`
   - Must use `System.Collections.Generic.Dictionary` explicitly (not `Sharpy.Dict`) since memo is internal infrastructure
   - C# 9.0 compatible, netstandard2.1/2.0
   - Acceptance: Interface compiles under netstandard2.1/2.0
   - Commit: `feat(core): add IDeepCopyable interface for type-safe deep copy`

10. **Implement `IDeepCopyable` on `List<T>`** — `src/Sharpy.Core/Partial.List/List.cs`
    - Add `IDeepCopyable` to the interface list on `List<T>`
    - Implement `DeepCopy(Dictionary<object, object> memo)`: create new `List<T>`, register in memo, iterate elements, recursively deep-copy each (check `IDeepCopyable` first, fall back to identity for value types)
    - Access internal `_list` field directly instead of reflection
    - Acceptance: `new List<int>(new[]{1,2,3}).DeepCopy(new())` returns a distinct List with same elements
    - Commit: `feat(core): implement IDeepCopyable on List<T>`

11. **Implement `IDeepCopyable` on `Dict<K,V>`** — `src/Sharpy.Core/Dict.cs`
    - Add `IDeepCopyable` to the interface list on `Dict<K,V>`
    - Implement `DeepCopy(Dictionary<object, object> memo)`: create new `Dict<K,V>`, register in memo, iterate `_dict` entries directly, recursively deep-copy keys and values
    - No reflection needed — direct access to internal `_dict`
    - Acceptance: Nested dict deep copy produces independent copy
    - Commit: `feat(core): implement IDeepCopyable on Dict<K,V>`

12. **Implement `IDeepCopyable` on `Set<T>`** — `src/Sharpy.Core/Partial.Set/Set.cs`
    - Add `IDeepCopyable` to the interface list on `Set<T>`
    - Implement `DeepCopy(Dictionary<object, object> memo)`: create new `Set<T>`, register in memo, iterate `_set` entries directly
    - Acceptance: Set deep copy produces independent copy
    - Commit: `feat(core): implement IDeepCopyable on Set<T>`

13. **Refactor `CopyModule` to use `IDeepCopyable`** — `src/Sharpy.Core/Copy/CopyModule.cs` [CORRECTED: path includes Copy/ subdirectory]
    - In `Copy()`: replace `type.GetMethod("Copy")` + `Invoke()` with `is Sharpy.List<>` / `Sharpy.Dict<,>` / `Sharpy.Set<>` type checks and direct `.Copy()` calls (these already exist on each type)
    - In `DeepCopyInternal()`: check `x is IDeepCopyable copyable` first → `copyable.DeepCopy(memo)`. Remove `DeepCopyList()`, `DeepCopyDict()`, `DeepCopySet()` helper methods entirely
    - Keep `MemberwiseClone` fallback for non-collection types (still needs reflection, but that's the .NET-blessed way)
    - Keep `IdentityEqualityComparer` — it is still needed for the `memo` dictionary in `Deepcopy()` (line 99) to detect circular references via identity equality [CORRECTED: cannot be removed; used only in CopyModule but still required for memo dict]
    - Acceptance: All existing `copy` module unit tests pass; no `GetMethod`/`Invoke` calls remain for collection types; `DeepCopyList`/`DeepCopyDict`/`DeepCopySet` methods deleted
    - Commit: `refactor(core): replace reflection with IDeepCopyable in CopyModule (#400, #403)`

### Phase 4: Core Library — New Stdlib Features

**Goal:** Add missing stdlib functions: `time.gmtime(secs)`, `time.localtime(secs)`, `heapq.merge()`, `csv.DictReader`, `csv.DictWriter`.

#### Tasks

14. **Add timestamp parameter overloads to `time.gmtime()` and `time.localtime()`** — `src/Sharpy.Core/Time/TimeModule.cs`
    - Add overload `Gmtime(double seconds)`: convert Unix timestamp to `DateTime` via `new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds)`, then `StructTime.FromDateTime()` (note: `DateTime.UnixEpoch` is .NET Core 2.1+ / not available in netstandard2.0, so use the explicit constructor)
    - Add overload `Localtime(double seconds)`: same conversion but to local time via `TimeZoneInfo.ConvertTimeFromUtc()`
    - Verify Python behavior: `python3 -c "import time; t=time.gmtime(0); print(t.tm_year, t.tm_mon, t.tm_mday)"` → `1970 1 1`
    - Acceptance: `time.gmtime(0)` returns struct_time for epoch; `time.localtime(86400)` returns correct local time
    - Commit: `feat(core): add timestamp parameter to time.gmtime() and time.localtime() (#399)`

15. **Add test fixtures for time.gmtime/localtime with timestamps** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
    - Create `stdlib_time_gmtime.spy` testing `time.gmtime(0)` fields (year=1970, mon=1, mday=1, etc.)
    - Create `stdlib_time_gmtime.expected` with expected output
    - Acceptance: Test fixture passes
    - Commit: `test: add integration tests for time.gmtime(secs) and time.localtime(secs)`

16. **Implement `heapq.merge()`** — `src/Sharpy.Core/Heapq/Heapq.cs`
    - Add `public static IEnumerable<T> Merge<T>(params Sharpy.List<T>[] iterables) where T : IComparable<T>` using `yield return` for lazy evaluation
    - Algorithm: initialize min-heap of `(value, iteratorIndex)` from first element of each iterable; repeatedly yield min, advance that iterator, re-heapify
    - Use internal `SiftDown`/`SiftUp` helpers (already exist) on a `System.Collections.Generic.List<(T value, int index)>` heap
    - Handle edge cases: empty iterables, single iterable, all empty
    - Defer `key` and `reverse` parameters (mention in TODO with issue number)
    - Verify Python: `python3 -c "import heapq; print(list(heapq.merge([1,3,5],[2,4,6])))"` → `[1, 2, 3, 4, 5, 6]`
    - Acceptance: `heapq.merge([1,3,5],[2,4,6])` yields `[1,2,3,4,5,6]`; empty inputs handled
    - Commit: `feat(core): implement heapq.merge() for k-way sorted merge (#397)`

17. **Add test fixture for heapq.merge** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
    - Create `stdlib_heapq_merge.spy` testing basic merge of two sorted lists
    - Create `stdlib_heapq_merge.expected` with expected output
    - Acceptance: Test fixture passes
    - Commit: `test: add integration test for heapq.merge()`

18. **Implement `csv.DictReader`** — `src/Sharpy.Core/Csv/CsvDictReader.cs` (new file)
    - `[SharpyModuleType("csv")]` sealed class implementing `IEnumerable<Sharpy.Dict<string, string>>`
    - Constructor takes `IEnumerable<string>` lines and optional `Sharpy.List<string>? fieldnames = null`
    - If `fieldnames` is null, use first row as field names
    - Each subsequent row maps to `Sharpy.Dict<string, string>` (field_name → value)
    - Handle mismatched column counts: extra values ignored, missing values get empty string
    - Expose `Fieldnames` property
    - Acceptance: `DictReader` iterates over dicts with correct field mapping
    - Commit: `feat(core): implement csv.DictReader (#398)`

19. **Implement `csv.DictWriter`** — `src/Sharpy.Core/Csv/CsvDictWriter.cs` (new file)
    - `[SharpyModuleType("csv")]` sealed class
    - Constructor takes `System.IO.TextWriter` and `Sharpy.List<string> fieldnames`
    - `Writeheader()`: writes fieldnames as CSV row
    - `Writerow(Sharpy.Dict<string, string> row)`: writes values in fieldname order, empty string for missing keys
    - `Writerows(IEnumerable<Sharpy.Dict<string, string>> rows)`: batch write
    - Acceptance: `DictWriter` writes correctly formatted CSV with headers
    - Commit: `feat(core): implement csv.DictWriter (#398)`

20. **Add factory methods for DictReader/DictWriter to CsvModule** — `src/Sharpy.Core/Csv/CsvModule.cs`
    - Add `DictReader(IEnumerable<string> lines, Sharpy.List<string>? fieldnames = null)` returning `CsvDictReader`
    - Add `DictWriter(System.IO.TextWriter output, Sharpy.List<string> fieldnames)` returning `CsvDictWriter`
    - Acceptance: `csv.DictReader(lines)` and `csv.DictWriter(output, fieldnames)` accessible from Sharpy
    - Commit: `feat(core): add DictReader/DictWriter factory methods to csv module`

21. **Add test fixtures for csv.DictReader/DictWriter** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
    - Create `stdlib_csv_dictreader.spy` and `.expected` testing basic dict-based CSV reading
    - Create `stdlib_csv_dictwriter.spy` and `.expected` testing dict-based CSV writing
    - Acceptance: Both test fixtures pass
    - Commit: `test: add integration tests for csv.DictReader and csv.DictWriter`

### Phase 5: Test Infrastructure — Runtime Error Testing

**Goal:** Support `.runtime-error` test fixtures for testing runtime exceptions.

#### Tasks

22. **Add `.runtime-error` fixture support to `FixtureDiscoveryHelper`** — `src/Sharpy.Compiler.Tests/Integration/FixtureDiscoveryHelper.cs`
    - Detect `.runtime-error` files alongside `.error` files during fixture discovery
    - Add `RuntimeErrorFile` property to the fixture info yielded by discovery
    - Ensure `.runtime-error` fixtures are not excluded by the existing `.error` logic
    - Acceptance: `FixtureDiscoveryHelper` discovers fixtures with `.runtime-error` files
    - Commit: `feat(tests): add runtime-error fixture discovery to FixtureDiscoveryHelper`

23. **Add runtime error test flow to `FileBasedIntegrationTests`** — `src/Sharpy.Compiler.Tests/Integration/FileBasedIntegrationTests.cs`
    - When `.runtime-error` file exists: assert compilation succeeds (`result.Success == true` OR check that the execution failed with non-zero exit code)
    - Match each non-empty, non-comment line in `.runtime-error` file as case-insensitive substring against `result.StandardError`
    - If program exits successfully when `.runtime-error` expected, fail the test
    - Acceptance: A `.runtime-error` fixture correctly validates runtime exception messages
    - Commit: `feat(tests): add runtime error assertion flow to FileBasedIntegrationTests (#406)`

24. **Convert `stdlib_statistics_empty` to runtime-error fixture** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
    - Delete `stdlib_statistics_empty.skip`
    - Delete `stdlib_statistics_empty.error` (compile-time error file)
    - Create `stdlib_statistics_empty.runtime-error` containing `StatisticsError`
    - Run test and verify it passes
    - Acceptance: `stdlib_statistics_empty` test passes as a runtime error test
    - Commit: `test: convert stdlib_statistics_empty to runtime-error fixture (#406)`

### Phase 6: LSP — Cross-File Implementation Navigation

**Goal:** Fix `textDocument/implementation` to correctly find cross-file implementations.

#### Tasks

25. **Canonicalize symbol references in `TypeHierarchyIndex.Build()`** — `src/Sharpy.Lsp/TypeHierarchyIndex.cs`
    - Before building the `_subtypes` dictionary, create a `Dictionary<string, TypeSymbol>` mapping symbol names to their canonical instances from the current symbol table
    - When inserting `type.BaseType` as a key, look up the canonical instance by name first; use it if found, otherwise fall back to the original reference
    - When inserting `iface.Definition` as a key, same canonicalization
    - Keep `ReferenceEqualityComparer` on the dictionary (it's correct once all references are canonical)
    - Acceptance: `TypeHierarchyIndex` correctly maps interfaces to implementations even when `InterfaceReference.Definition` points to a different object instance
    - Commit: `fix(lsp): canonicalize symbol references in TypeHierarchyIndex.Build() (#407)`

26. **Also canonicalize the queried symbol in `GetDirectSubtypes()`** — `src/Sharpy.Lsp/TypeHierarchyIndex.cs`
    - Add a `_nameToSymbol` dictionary (built during `Build()`) as an instance field
    - In `GetDirectSubtypes(TypeSymbol type)`, first try canonical lookup by name, then fall back to direct reference lookup
    - This handles the case where the caller passes a symbol from a different compilation context
    - Acceptance: `GetDirectSubtypes(anyIServiceInstance)` returns implementations regardless of which compilation created the symbol
    - Commit: `fix(lsp): add name-based fallback lookup in TypeHierarchyIndex.GetDirectSubtypes()`

27. **Update `TypeHierarchySubtypesHandler` for consistency** — `src/Sharpy.Lsp/Handlers/TypeHierarchySubtypesHandler.cs`
    - Verify it also uses `TypeHierarchyIndex.Build()` with the project-wide symbol table
    - If it has the same vulnerability, apply the same fix (should already be fixed by tasks 25-26 since it shares `TypeHierarchyIndex`)
    - Acceptance: `typeHierarchy/subtypes` works correctly for cross-file scenarios
    - Commit: `fix(lsp): verify TypeHierarchySubtypesHandler uses canonical symbol references`

28. **Update E2E test to verify cross-file implementation results** — `src/Sharpy.Lsp.Tests/E2E/MultiFileTests.cs`
    - Update `MultiFile_Implementation_CrossFile` test to assert that it returns `MyService` location (not just non-null/non-crash)
    - Acceptance: E2E test verifies correct implementation location is returned
    - Commit: `test(lsp): strengthen MultiFile_Implementation_CrossFile to verify results (#407)`

## Testing Strategy

### New Test Fixtures
- `stdlib_string.spy` / `.expected` — verify `import string; string.digits` (unskip existing)
- `stdlib_from_string.spy` / `.expected` — verify `from string import ascii_letters` (unskip existing)
- `stdlib_import_nonexistent_from_string.spy` / `.error` — verify error for nonexistent import (unskip existing)
- `stdlib_csv.spy` / `.expected` — verify `for row in csv.reader(...)` (unskip existing)
- `stdlib_time_gmtime.spy` / `.expected` — verify `time.gmtime(0)` fields
- `stdlib_heapq_merge.spy` / `.expected` — verify `heapq.merge()` basic case
- `stdlib_csv_dictreader.spy` / `.expected` — verify dict-based CSV reading
- `stdlib_csv_dictwriter.spy` / `.expected` — verify dict-based CSV writing
- `stdlib_statistics_empty.runtime-error` — verify runtime `StatisticsError` (convert from skipped)

### Edge Cases
- Const-only imports: `from string import nonexistent` → proper error
- CsvReader: empty input, single row, quoted fields with commas
- heapq.merge: empty iterables, single iterable, all empty
- DictReader: mismatched column counts, empty input
- DictWriter: missing keys in row dict
- Deep copy: nested collections, circular reference detection (memo dict)
- LSP: cross-file implementation with multiple implementors, diamond inheritance

### Negative Tests
- Import non-existent const from string module
- Deep copy of non-copyable types (should fall back to shallow)

## Issues to Close

- #394 — `feat(compiler): support const-only module import resolution` (closed by Phase 1, Task 5)
- #395 — `feat(core): add __iter__ support to CsvReader` (closed by Phase 2, Task 8)
- #396 — `feat(core): add functools.partial and lru_cache` — **DEFERRED** (requires significant compiler changes; create follow-up issue)
- #397 — `feat(core): add heapq.merge` (closed by Phase 4, Task 17)
- #398 — `feat(core): add csv.DictReader and csv.DictWriter` (closed by Phase 4, Task 21)
- #399 — `feat(core): implement time.gmtime() and time.localtime()` (closed by Phase 4, Task 15)
- #400 — `fix(core): replace reflection in CopyModule.Deepcopy` (closed by Phase 3, Task 13)
- #401 — `fix(core): CsvReader should yield Sharpy.List<string>` (closed by Phase 2, Task 7)
- #402 — `fix(compiler): support const-only module imports` (closed by Phase 1, Task 5; duplicate of #394)
- #403 — `refactor(core): replace reflection in CopyModule` (closed by Phase 3, Task 13; duplicate of #400)
- #404 — `feat(compiler): support field discovery for const-only modules` (closed by Phase 1, Task 5; duplicate of #394)
- #405 — `feat(compiler): support __iter__ protocol on module types` (closed by Phase 2, Task 8; duplicate of #395)
- #406 — `feat(tests): support runtime error testing` (closed by Phase 5, Task 24)
- #407 — `LSP: cross-file implementation reference identity mismatch` (closed by Phase 6, Task 28)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-03-16
**Plan file:** `docs/implementation_planning/stdlib_phase2_fixes_394_407.md`

### Corrections Made

1. **Reflection count** (Current State): Changed "~68 reflection call sites" → "~37 reflection call sites". The original count likely double-counted `IsGenericType`/`IsValueType` checks or included non-reflection operations.

2. **CopyModule path** (Tasks 9, 13): Changed `src/Sharpy.Core/CopyModule.cs` → `src/Sharpy.Core/Copy/CopyModule.cs`. CopyModule lives in the `Copy/` subdirectory. Also corrected `IDeepCopyable.cs` path to `Copy/IDeepCopyable.cs`.

3. **Task 7 wording** (CsvReader): Changed from "Verify that CsvReader already uses Sharpy.List" to "Refactor CsvReader to use Sharpy.List". CsvReader currently uses BCL `System.Collections.Generic.List<string>` (confirmed at lines 56, 112). The Current State section correctly identified this, but the task description contradicted it.

4. **TypeInferenceService line reference** (Task 6): Changed "around line 660" → "lines 644–660". The `__iter__()` check begins at line 644 with the `UserDefinedType` pattern match, specific `__iter__` lookup at line 647, and the block closes at line 660.

5. **IdentityEqualityComparer** (Task 13): Changed "remove if possible" → "keep — still needed for memo dict". The identity comparer is used at line 99 for circular reference detection in `Deepcopy()` and remains required even after the IDeepCopyable refactor.

### Warnings

- **Task 22 (runtime-error fixture discovery)**: Implementation details are underspecified. Should mention adding `RuntimeErrorFile` property to `TestFixtureInfo` record (currently has `ExpectedFile`, `ErrorFile`, `WarningFile`, `ExpectedCsFile` but no runtime error property).
- **DictReader/DictWriter signatures**: Python's `csv.DictReader` has `restkey`/`restval` parameters and `csv.DictWriter` has `restval`/`extrasaction`. The plan only implements minimal signatures. This is acceptable for Phase 2 but should be noted as a follow-up.
- **heapq.merge `key`/`reverse` parameters**: Plan correctly defers these but notes "mention in TODO with issue number" — ensure the GitHub issue is created per project convention.

### Missing Steps Added

None — all pipeline phases are covered and the implementation order is correct.

### Unchecked Claims

- **Issue deduplication** (#394/#402/#404, #395/#401/#405, #400/#403): Could not verify these are true duplicates without reading the GitHub issues (not accessible from codebase alone). The grouping appears reasonable based on descriptions.
- **"3 test fixtures skipped" for string module**: Verified skip files exist but did not run the tests to confirm they actually fail without the fix.
- **`string` module has exactly 9 constants**: Not verified against actual `StringModule` CLR type.
