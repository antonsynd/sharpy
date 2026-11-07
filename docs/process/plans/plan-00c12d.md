<!-- Verified by /verify-plan on 2026-03-04 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Discovery Integration, Registry Adoption, and Caching Hardening

## Context

Six issues (#297–#302) from the plan-5908d3 audits and deferred tasks. They fall into three themes:

1. **Registry integration** (#297): `CollectionTypeRegistry` was created in #292 but not fully integrated into `RoslynEmitter.Expressions.Literals.cs` — the dict spread builder still hardcodes `"Update"` instead of using the registry.

2. **Static parse methods** (#298): Runtime `IntParse.Parse` and `DoubleParse.Parse` were added in #295 and are already wired end-to-end (semantic registration via `ApplyNonDiscoverableDefinitions`, codegen via `GetPrimitiveStaticCallTarget`, and test fixtures exist). The issue asks for further integration into the discovery system so they aren't hardcoded.

3. **Discovery system improvements** (#299–#302):
   - #299: Further refactoring to remove `ApplyNonDiscoverableDefinitions` fallbacks by making discovery cover more types.
   - #300: `ConcurrentDictionary.GetOrAdd` factory in `CachedModuleDiscovery.LoadAssembly` may execute multiple times under contention.
   - #301: `Thread.Sleep` in `OverloadIndexCache.TryLoad/Save` blocks the compiler thread.
   - #302: `GetTypeByName` uses case-insensitive matching — fragile naming contract.

GitHub issues: [#297](https://github.com/antonsynd/sharpy/issues/297), [#298](https://github.com/antonsynd/sharpy/issues/298), [#299](https://github.com/antonsynd/sharpy/issues/299), [#300](https://github.com/antonsynd/sharpy/issues/300), [#301](https://github.com/antonsynd/sharpy/issues/301), [#302](https://github.com/antonsynd/sharpy/issues/302)

## Current State

### CollectionTypeRegistry (#297)
- `CollectionTypeRegistry` exists at `src/Sharpy.Compiler/CodeGen/CollectionTypeRegistry.cs` with entries for `list`, `dict`, `set` (each with `AddMethodName`, `SpreadMethodName`, `CSharpTypeName`).
- **List literal**: Already calls `CollectionTypeRegistry.TryGet(BuiltinNames.List, ...)` for spread handling (line 59).
- **Set literal**: Already calls `CollectionTypeRegistry.TryGet(BuiltinNames.Set, ...)` for spread handling (line 153).
- **Dict literal**: Does NOT use the registry — `GenerateSpreadDictBuilder()` hardcodes `"Update"` on line 1355. The method also hardcodes `dict[key] = value` for non-spread entries instead of using `collInfo.AddMethodName`.
- **Comprehensions**: `GenerateImperativeComprehension()` already uses registry (line 335).
- **Non-spread dict/list/set paths**: The `CSharpTypeNames.*` constants and `BuiltinNames.*` checks are not the focus of this registry — those are type mapping, not method dispatch. The registry's purpose is to centralize spread/add method names.

### int.parse()/float.parse() (#298)
- **Runtime**: `IntParse.cs` and `DoubleParse.cs` exist in Sharpy.Core with `Parse(string s) → Result<T, ValueError>`.
- **Semantic**: `BuiltinRegistry.ApplyNonDiscoverableDefinitions()` manually creates `parse` as `FunctionSymbol(IsStatic: true)` for `int` and `float`.
- **Codegen**: `GetPrimitiveStaticCallTarget()` maps `("int", "parse") → "global::Sharpy.IntParse.Parse"` and `("float", "parse") → "global::Sharpy.DoubleParse.Parse"`.
- **Tests**: `int_parse.spy`/`.expected` and `float_parse.spy`/`.expected` exist and pass.
- **What's needed**: These static methods live on separate helper classes (`IntParse`, `DoubleParse`), not on the type itself in CLR. Discovery can't find them via reflection on `int`. The semantic+codegen wiring is already correct — what's needed is to consider whether `IntParse`/`DoubleParse` classes should be discovered as module functions and then routed as static methods on `int`/`float`, or if the current `ApplyNonDiscoverableDefinitions` approach is the right permanent design.

### Discovery system (#299)
- `BuiltinMethodDefinitions` has already been removed. Discovery is live.
- `OverloadIndexBuilder` discovers instance methods, operators, and protocols. Constructors and static factory methods on collection types are not yet discovered.
- `BuiltinRegistry.RegisterType()` calls `_discovery.GetTypeByName()` then `ApplyNonDiscoverableDefinitions()` for fallback.
- `ApplyNonDiscoverableDefinitions` currently handles: tuple operators/protocols, iterator/ienumerable protocols, int.parse, float.parse.
- Most of what #299 originally described is already done. The remaining gap is whether to discover constructors from `Sharpy.Core` types and whether tuple/parse need permanent non-discoverable definitions (they do — tuple maps to `System.ValueTuple` which has no methods, and parse is on helper classes).

### Caching issues (#300, #301, #302)
- **#300**: `_loadedIndices.GetOrAdd(identity.Name, _ => { ... })` — the factory includes disk I/O side effect (`_cache.Save`). `ConcurrentDictionary.GetOrAdd` does NOT guarantee single factory execution.
- **#301**: `OverloadIndexCache.TryLoad` and `Save` each have 3-iteration retry loops with `Thread.Sleep(RetryDelayMs * (attempt + 1))`, max blocking 600ms total. Currently single-threaded in practice.
- **#302**: `CachedModuleDiscovery.GetTypeByName()` uses `StringComparison.OrdinalIgnoreCase` to match Sharpy names (e.g., `"list"`) to CLR names (e.g., `"List"`). Works today but is brittle for future types.

## Design Decisions

1. **CollectionTypeRegistry is the right abstraction for spread method names** (#297). The dict spread builder should use `CollectionTypeRegistry.TryGet` like list and set already do. This eliminates the last hardcoded `"Update"` string.

2. **int.parse/float.parse are permanently non-discoverable** (#298). The CLR helper classes (`IntParse`, `DoubleParse`) are deliberately separate from the primitive types. Discovery operates on the actual CLR type surface. The `ApplyNonDiscoverableDefinitions` pattern is the correct permanent home for these. The issue should be resolved by documenting this design decision and ensuring test coverage is complete. No additional codegen support is needed — it already works.

3. **#299 is mostly done; close with documentation** (#299). The original scope called for removing `BuiltinMethodDefinitions` and replacing with discovery — that's complete. What remains are permanent non-discoverable definitions (tuple, parse, iterators). These should remain in `ApplyNonDiscoverableDefinitions` with clear doc comments explaining why. No further migration needed.

4. **Lazy<T> pattern for GetOrAdd** (#300). Axiom 1 (.NET compatibility) — use `ConcurrentDictionary<string, Lazy<OverloadIndex>>` with `GetOrAdd(key, new Lazy<T>(() => factory())).Value` to guarantee single factory execution.

5. **Replace Thread.Sleep with exponential backoff log-and-skip** (#301). The compiler is single-threaded today, so the practical impact is nil. But sleeping on a compiler thread is an anti-pattern. Replace retry loops with a try-once-then-log-warning approach: attempt the file operation once, on `IOException` log a debug message and return `null` (for `TryLoad`) or skip saving (for `Save`). Caching is best-effort; blocking is worse than missing a cache entry.

6. **Explicit name mapping table for GetTypeByName** (#302). Replace `OrdinalIgnoreCase` string comparison with a `static readonly Dictionary<string, string>` mapping Sharpy names to expected CLR type names (e.g., `"list" → "List"`, `"dict" → "Dict"`, `"set" → "Set"`, etc.). This makes the naming contract explicit and eliminates ambiguity.

## Implementation

### Phase 1: CollectionTypeRegistry Integration (#297)

**Goal:** Eliminate hardcoded spread method name in dict literal emission.

#### Tasks

1. **Use CollectionTypeRegistry in GenerateSpreadDictBuilder** — `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Literals.cs`
   - Refactor `GenerateDictLiteral()` to call `CollectionTypeRegistry.TryGet(BuiltinNames.Dict, out var dictInfo)` before the spread check (line ~103–106).
   - Pass `dictInfo!.SpreadMethodName` into `GenerateSpreadDictBuilder()` as a parameter (or refactor the method to accept it).
   - Replace the hardcoded `"Update"` on line 1355 with the parameter.
   - **Acceptance**: List, dict, and set spread literals all use `CollectionTypeRegistry.TryGet()`. No hardcoded method names remain in spread builder code.
   - Commit: `refactor(codegen): integrate CollectionTypeRegistry into dict spread emission`

2. **Verify existing spread tests pass** — no new files needed
   - Run `dotnet test --filter "DisplayName~spread"` to verify dict/list/set spread fixtures still pass (fixtures are named `spread_list.spy`, `spread_dict.spy`, `spread_set.spy` [CORRECTED: not `list_spread`, `dict_spread`, `set_spread`]).
   - **Acceptance**: All spread-related tests pass with no regressions.
   - Commit: (no commit — verification only)

### Phase 2: Caching Hardening (#300, #301, #302)

**Goal:** Fix three latent issues in the discovery caching layer.

#### Tasks

3. **Fix ConcurrentDictionary.GetOrAdd with Lazy pattern** — `src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs`
   - Change `_loadedIndices` from `ConcurrentDictionary<string, OverloadIndex>` to `ConcurrentDictionary<string, Lazy<OverloadIndex>>`.
   - Update `LoadAssembly()` to use `_loadedIndices.GetOrAdd(identity.Name, _ => new Lazy<OverloadIndex>(() => { ... })).Value`.
   - Update all other read sites (`GetModuleFunctions`, `GetModuleTypes`, `GetTypeByName`, `GetLoadedModules`) to call `.Value` on the `Lazy<OverloadIndex>`.
   - **Acceptance**: Factory only executes once per key regardless of thread contention. All existing discovery tests pass.
   - Commit: `fix(discovery): use Lazy<T> in GetOrAdd to prevent duplicate factory execution`

4. **Replace Thread.Sleep with try-once-then-skip in OverloadIndexCache** — `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexCache.cs`
   - In `TryLoad()`: Remove the retry loop. Attempt the file read once. On `IOException`, log a debug message and return `null`.
   - In `Save()`: Remove the retry loop. Attempt the write once. On `IOException`, log a debug message and return without saving.
   - Keep `MaxRetries` and `RetryDelayMs` removal clean — remove the constants too.
   - **Acceptance**: No `Thread.Sleep` calls remain in the file. Caching is best-effort. All discovery tests pass.
   - Commit: `fix(discovery): remove Thread.Sleep from cache retry loops`

5. **Replace case-insensitive matching with explicit name map** — `src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs`
   - Add a `private static readonly Dictionary<string, string>` mapping Sharpy names to CLR names: `"list" → "List"`, `"dict" → "Dict"`, `"set" → "Set"`, `"int" → "Int32"`, `"float" → "Double"`, `"str" → "String"`, `"bool" → "Boolean"`, `"long" → "Int64"`, `"double" → "Double"`, `"decimal" → "Decimal"`. Include all types that `BuiltinRegistry.RegisterType()` calls `GetTypeByName()` for.
   - In `GetTypeByName()`, first translate the Sharpy name via the map, then match with `StringComparison.Ordinal` (case-sensitive).
   - If the name isn't in the map, fall through with case-sensitive matching (no magic for unknown types).
   - **Acceptance**: `GetTypeByName("list")` finds CLR `"List"`. Case-insensitive matching is gone. All discovery tests pass.
   - Commit: `fix(discovery): replace case-insensitive GetTypeByName with explicit name map`

### Phase 3: Document Non-Discoverable Definitions (#298, #299)

**Goal:** Document the permanent design decisions and close the remaining scope.

#### Tasks

6. **Add doc comments to ApplyNonDiscoverableDefinitions explaining why each case is permanent** — `src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs`
   - Add XML doc explaining:
     - `Tuple`: maps to `System.ValueTuple` which has no instance methods/operators; CLR operators are compiler-synthesized, not discoverable.
     - `Iterator/IEnumerable/IEnumerator`: placeholder types with no CLR surface (registered with `typeof(object)`).
     - `int.parse / float.parse`: live on separate utility classes (`IntParse`, `DoubleParse`), not on `System.Int32`/`System.Double`. Discovery operates on type surfaces, not cross-type helpers.
   - **Acceptance**: Clear documentation on why each non-discoverable definition exists and why it should stay.
   - Commit: `docs(semantic): document permanent non-discoverable definitions in BuiltinRegistry`

7. **Add additional parse edge-case test fixtures** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
   - Create `int_parse_edge_cases.spy` + `.expected`: test empty string, whitespace, overflow, leading/trailing whitespace, negative numbers.
   - Create `float_parse_edge_cases.spy` + `.expected`: test scientific notation, inf, nan, negative zero.
   - Verify Python 3 behavior first: `python3 -c "print(int('  42  '))"` etc.
   - **Acceptance**: Edge cases test fixtures pass. Coverage of `int.parse`/`float.parse` semantics is comprehensive.
   - Commit: `test: add parse edge-case test fixtures for int and float`

### Phase 4: Verification

**Goal:** Full regression verification.

#### Tasks

8. **Run full test suite and format check**
   - `dotnet build sharpy.sln && dotnet test && dotnet format whitespace --verify-no-changes`
   - **Acceptance**: All tests pass, no formatting issues, no warnings.
   - Commit: (no commit — verification only)

## Testing Strategy

### Existing coverage (must not regress)
- `int_parse.spy` / `int_parse.expected` — basic int.parse success/failure
- `float_parse.spy` / `float_parse.expected` — basic float.parse success/failure
- `spread_list.spy`, `spread_dict.spy`, `spread_set.spy` — collection spread via `*`/`**` [CORRECTED: actual fixture names use `spread_` prefix, not `_spread` suffix]
- `DiscoveryAlignmentTests` — validates list/dict/set method discovery
- `OverloadIndexCacheTests` — validates cache load/save/invalidation

### New test fixtures (Phase 3, Task 7)
- `int_parse_edge_cases.spy` / `.expected` — empty, whitespace, overflow, negative
- `float_parse_edge_cases.spy` / `.expected` — scientific notation, edge values

### Edge cases to verify
- Dict literal with only spread entries (no regular key-value pairs)
- Dict literal with mixed spread and regular entries
- Concurrent compilation scenarios for Lazy<T> change (existing thread-safety tests)
- `GetTypeByName` with a name not in the mapping table → returns null

### Negative test cases
- `int.parse` with non-string argument → type error (existing coverage)
- Unknown type name via `GetTypeByName` → null return (verify in unit test)

## Issues to Close

- #297 — Integrate CollectionTypeRegistry into literal emission (closed by Phase 1, Task 1)
- #298 — Register int.parse()/float.parse() in semantic layer and add codegen support (closed by Phase 3, Tasks 6-7; already wired end-to-end, issue scope is resolved by documenting the permanent design and adding edge-case tests)
- #299 — Implement discovery-backed builtin method population (closed by Phase 3, Task 6; migration is complete, remaining non-discoverable definitions are permanent by design)
- #300 — Discovery: ConcurrentDictionary.GetOrAdd factory may execute multiple times (closed by Phase 2, Task 3)
- #301 — Discovery: Thread.Sleep in OverloadIndexCache blocks compiler hot path (closed by Phase 2, Task 4)
- #302 — Discovery: case-insensitive type name matching in GetTypeByName is fragile (closed by Phase 2, Task 5)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-03-04
**Plan file:** `.claude/plans/plan-00c12d.md`

### Corrections Made
- Test fixture names: `list_spread.spy`, `dict_spread.spy`, `set_spread.spy` → `spread_list.spy`, `spread_dict.spy`, `spread_set.spy` (actual fixture names use `spread_` prefix, not `_spread` suffix)

### Warnings
- **Dict non-spread entry pattern**: The Current State section observes that `GenerateSpreadDictBuilder` "hardcodes `dict[key] = value` for non-spread entries instead of using `collInfo.AddMethodName`". This is actually fine — `Dict.Add(K, V)` in Sharpy.Core delegates to indexer assignment (`_dict[key] = value`), so both approaches produce identical behavior. The plan's implementation task correctly only addresses the spread method name (`"Update"`), not the non-spread entry pattern.
- **Parse edge-case test overflow behavior**: Task 7 mentions testing "overflow" for `int.parse`. Note that Python has arbitrary-precision ints (no overflow) while .NET `int` is 32-bit. The Sharpy `IntParse.Parse` should return a `Result.Error` for overflow — verify this matches the `IntParse.cs` implementation before writing the fixture.

### Missing Steps Added
- (none — plan coverage is complete across all 6 issues)

### Unchecked Claims
- GitHub issue numbers (#297–#302) were not verified against the actual GitHub issues (would require API access)
