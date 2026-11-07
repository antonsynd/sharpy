<!-- Verified by /verify-plan on 2026-08-24 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Remediation Round 2026-08-22 — Wave 4

Parent: [plan-remediation-round-2026-08-22.md](plan-remediation-round-2026-08-22.md)

## Context

Wave 4 of the 2026-08-22 remediation round. Covers:

- **Batch 11** — Infra/tooling quickies (#1566, #1567, #1606)
- **Batch 2 tail** — Slice builtin type (#1610) and tuple constant-bound slicing (#1609)
- **Batch 12** — Distribution/NuGet (#1580, #1581)
- **Batch 13** — Stdlib fidelity (#1597, #1598)

All owner rulings are posted on the issues (R9, R10, R11). Batches 0–10 and earlier
waves have already landed; #1608 (slice validation) shipped at `88fe74373` with the
`SliceLowering` carrier, `CheckSliceAccess`, `CheckSliceBound`, and `CheckIntIndex`.

## Current State

- **#1566/#1567**: Lock path `~/.claude/locks/dotnet.lock` hardcoded independently in
  `dotnet-serialized:33`, `test-memory-harness:40`, `test-memory-profile:27`. The
  harness kills dotnet-serialized's child but exits before the wrapper's EXIT trap
  removes the lock.
- **#1606**: `ProtocolTests` fixed (first-wait 30s, then drain). 8 remaining sites in
  `MultiFileTests.cs` and `LspEditSessionTraceHarness.cs` use short-timeout drain loops
  that need audit and either conversion or comment justification.
- **#1610**: `slice` is not a registered builtin type — `def __getitem__(self, s: slice)`
  fails SPY0202. `Slice.cs` exists in Core with `int` fields; Python's `slice` type
  exposes `start`/`stop`/`step` as `int?` properties. No `slice()` constructor builtin.
  `CheckSliceAccess` has no user-protocol arm.
- **#1609**: Tuple slicing refused by #1608 with steer to this issue. `TryGetConstantIntIndex`
  in `AstHelper.cs:17–33` handles literal ints and negation; const-reference folding
  (#1460) feeds indirectly via `IntegerConstantEvaluator` [CORRECTED: `ConstantFoldingVisitor` does not exist; the semantic-layer mechanism is `IntegerConstantEvaluator`; the lowering layer has `ConstFoldPass`].
- **#1580**: `NuGetRestorer` deletes the temp dir (including `obj/project.assets.json`)
  in `finally` before `NuGetResolver` runs. Resolver does exact-string version lookup.
  Cache miss reported via `LogWarning` — invisible at default verbosity.
- **#1581**: R11 = accept + document. The error message at `94f01b44c` is adequate.
- **#1597**: `JsonSurrogateEscapes.FirstUnpaired` scans escape sequences only, not raw
  UTF-16 surrogate code units. Both `Loads` doors call the pre-scan. The divergence is
  pinned in `JsonLoadsAgreementTests:321–334`.
- **#1598**: YamlDotNet has no public hook for per-character escape rendering in
  double-quoted scalars. Options are post-processing or custom emitter wrapper.

## Design Decisions

1. **#1610 Slice type surface** — Python `slice(1, None, None).start` returns `1`,
   `.stop` returns `None`. The existing `Slice` struct uses concrete `int` fields with
   defaults applied at construction. For the builtin type, add nullable `Start`/`Stop`/`Step`
   properties (each `int?`) that preserve the raw values, alongside the existing
   concrete-field constructor used by `GetSlice`. The `slice()` constructor builtin
   takes `(int?, int?, int?)` matching the Python signature. Registration follows the
   existing `RegisterType` pattern in `BuiltinRegistry.cs`.

2. **#1609 Tuple slicing — v1 scope** — Constant positive bounds only. Negative constant
   bounds are statically normalizable (arity is known) but add complexity; v1 refuses
   them with a steer ("use positive index"). Constant negative steps (reverse) also
   refused in v1. This matches the conservative subset: the refusal message can be
   relaxed later without breaking existing code.

3. **#1580 Version resolution** — Parse `obj/project.assets.json` (NuGet's own output)
   before deleting the temp dir. The `targets` → `libraries` section maps package names
   to actually-resolved versions. This is the class rule: never re-derive what restore
   already decided. Surface a dropped `PackageReference` as a real diagnostic (SPY09xx)
   visible at default verbosity, not a logger warning.

4. **#1597 Raw surrogate scanning** — Extend the existing `FirstUnpaired` pre-scan to
   detect raw surrogate code units (char in the U+D800–U+DFFF range without a proper
   pair). Both doors already call this scan, so the fix is one site.

5. **#1598 Cost-first** — Measure the YamlDotNet emitter hook cost before deciding the
   arm. If post-processing the emitter output (regex replace raw control bytes 0x00–0x1F
   with their YAML escapes) is cheap and correct, do that. Otherwise pin + document.

## Implementation

### Phase 1: Script lock-path consolidation (#1566)

**Goal:** Single source of truth for the dotnet lock path and layout.

#### Tasks

1. **Create `dotnet-lock-env.sh`** — `.claude/scripts/dotnet-lock-env.sh`
   - Define `LOCK_DIR`, `LOCK_PID_FILE`, `LOCK_CHILD_FILE` as exported variables
   - Commit: `fix(scripts): extract shared dotnet-lock-env.sh (#1566)`

2. **Source from `dotnet-serialized`** — `.claude/scripts/dotnet-serialized`
   - Replace the hardcoded `LOCK_DIR` at line 33 with `source "$(dirname "$0")/dotnet-lock-env.sh"`
   - Replace any `$LOCK_DIR/pid` and `$LOCK_DIR/child` references with the named vars
   - Acceptance: `dotnet-serialized echo hello` still acquires/releases the lock correctly
   - Commit: `fix(scripts): dotnet-serialized sources shared lock env (#1566)`

3. **Source from `test-memory-harness`** — `.claude/scripts/test-memory-harness`
   - Replace line 40's hardcoded path and the pid/child file references
   - Acceptance: harness's handshake loop still finds the wrapper's ownership files
   - Commit: `fix(scripts): test-memory-harness sources shared lock env (#1566)`

4. **Source from `test-memory-profile`** — `.claude/scripts/test-memory-profile`
   - Replace line 27's hardcoded path and pid/child references
   - Acceptance: profile script's lock wait still works
   - Commit: `fix(scripts): test-memory-profile sources shared lock env (#1566)`

5. **Mutation test** — move the lock path in `dotnet-lock-env.sh` to a bogus value,
   verify all three scripts fail loudly (not hang silently). Revert after confirming.

### Phase 2: Watchdog kill bounded wait (#1567)

**Goal:** test-memory-harness waits for the wrapper's EXIT trap before exiting.

#### Tasks

1. **Add bounded `wait`** — `.claude/scripts/test-memory-harness`
   - After the `kill_tree` + `KILLED=true` block (around line 122), before the `exit 4`
     path (around line 134):
     ```bash
     if [[ "$KILLED" == "true" ]]; then
       for i in $(seq 1 10); do
         kill -0 "$WRAPPER_PID" 2>/dev/null || break
         sleep 0.5
       done
     fi
     ```
   - This gives the wrapper up to 5s to run its EXIT trap and release the lock
   - Acceptance: after a threshold kill, the lock directory is removed before the harness exits
   - Commit: `fix(scripts): bounded wait on wrapper PID before exit (#1567)`

### Phase 3: LSP drain-loop audit (#1606)

**Goal:** Extract a shared helper; audit and classify each remaining site.

#### Tasks

1. **Extract `WaitFirstLoudlyThenDrain` helper** — `src/Sharpy.Lsp.Tests/E2E/` (new or existing test helper file) [CORRECTED: LSP E2E test helpers live under E2E/]
   - Signature: `async Task<List<T>> WaitFirstLoudlyThenDrain<T>(Func<Task<T>> waitForOne, TimeSpan firstTimeout, TimeSpan drainTimeout)`
   - First call throws on timeout (not silent); subsequent calls break on timeout
   - This is the pattern already in ProtocolTests at lines 251–266 and 360–375, extracted
   - Commit: `refactor(lsp-tests): extract WaitFirstLoudlyThenDrain helper (#1606)`

2. **Audit MultiFileTests sites** — `src/Sharpy.Lsp.Tests/E2E/MultiFileTests.cs` [CORRECTED: file is in E2E/ subdirectory]
   - Lines ~287–291, ~336–340, ~373–377, ~434–455, ~541–545 (at HEAD; locate by
     `WaitForNotificationAsync` grep)
   - Per the research: these are polling drain helpers inside a 30s outer `Stopwatch`
     retry loop — the 500ms timeout is between feature-request retries, not a first-wait.
     The outer loop provides the generous timeout. These are **likely fine as-is**
   - For each: add a comment justifying why the short drain is correct (e.g.,
     `// Drain between feature-request retries — the outer Stopwatch loop provides the generous first-wait timeout`)
   - Any site that IS a first-wait (no outer retry loop): convert to the shared helper
   - Acceptance: each of the 5 MultiFileTests sites has either a conversion or a
     justification comment citing #1606
   - Commit: `fix(lsp-tests): audit MultiFileTests drain loops (#1606)`

3. **Audit LspEditSessionTraceHarness sites** — `src/Sharpy.Lsp.Tests/E2E/LspEditSessionTraceHarness.cs` [CORRECTED: file is in E2E/ subdirectory]
   - Lines ~173–175 (`DrainAsync`) and ~194–197 (`CollectServerLatenciesAsync`)
   - Per the research: both are genuine quiescence drains (named methods, consuming
     already-queued notifications after the feature under test has completed)
   - Add justification comments citing #1606
   - Acceptance: both sites have justification comments
   - Commit: `fix(lsp-tests): justify trace-harness drain loops (#1606)`

4. **Mutation test the helper** — write a test that uses `WaitFirstLoudlyThenDrain` with
   a source that never produces a notification; verify it throws `TimeoutException` on
   the first wait (not a silent break). This proves the helper's first-wait-throws
   contract.
   - Commit: (fold into Task 1's commit)

### Phase 4: Slice builtin type (#1610)

**Goal:** Register `slice` as a builtin type; enable user `__getitem__(slice)` protocol.

#### Tasks

1. **Add nullable properties to `Slice`** — `src/Sharpy.Core/Slice.cs`
   - Add `int? Start { get; }`, `int? Stop { get; }`, `int? Step { get; }` properties
     that preserve the raw nullable values passed at construction
   - Add a constructor overload `Slice(int? start, int? stop, int? step)` that stores
     the raw values AND computes the concrete fields for `GetSlice` arithmetic
   - The existing `(int, int, int)` constructor continues to work for internal callers
   - Multi-target safe: nullable value types are C# 9.0 compatible
   - Commit: `feat(core): add nullable Start/Stop/Step properties to Slice (#1610)`

2. **Register `slice` as a builtin type** — `src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs`
   - Add `RegisterType("slice", typeof(Sharpy.Slice), ...)` following the pattern of
     `bytes`, `tuple`, etc.
   - The type should resolve in annotations: `s: slice` → `Sharpy.Slice`
   - Acceptance: `def f(s: slice) -> int: return s.start` no longer produces SPY0202
   - Commit: `feat(semantic): register 'slice' as a builtin type (#1610)`

3. **Register `slice()` constructor builtin** — `src/Sharpy.Compiler/Semantic/Registry/BuiltinRegistry.cs` or `Builtins/`
   - `slice(start, stop, step)` with all three `int?` parameters (matching Python:
     `slice(None, 5)` = `[:5]`, `slice(1, None, None)` = `[1:]`)
   - Python's `slice()` requires at least one argument: `slice()` → TypeError. Two forms:
     `slice(stop)` and `slice(start, stop[, step])`. Match that signature.
   - Verify against Python: `python3 -c "print(slice(1,5,2).start, slice(1,5,2).stop, slice(1,5,2).step)"`
   - Commit: `feat(semantic): register slice() constructor builtin (#1610)`

4. **Add user `__getitem__(slice)` arm to `CheckSliceAccess`** — `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Literals.cs`
   - After the existing receiver classifications (List/Str/Bytes/Array/NdArray) and
     before the SPY0320 refusal:
   - Check if the receiver type has a `__getitem__` method whose parameter type is
     `Sharpy.Slice` (i.e., `slice`)
   - If found: classify as `SliceLoweringKind.UserProtocol` (new enum member); record
     the return type as the slice result type
   - Codegen: new arm in the `SliceLowering.Kind` switch that emits
     `receiver.__getitem__(new Slice(start, stop, step))`
   - Acceptance: a class with `def __getitem__(self, s: slice) -> str` compiles and
     executes when sliced with `obj[1:5]`
   - Commit: `feat(semantic): user __getitem__(slice) sliceable arm (#1610)`

5. **Spec section** — `docs/language_specification/` [CORRECTED: `builtin_types.md` does not exist; use `primitive_types.md` or `type_hierarchy.md`, or create a new `builtin_types.md`]
   - Document the `slice` type: properties, constructor, nullable bound semantics
   - Reference the slicing spec section added in #1608's Wave 2
   - Execute every example at HEAD before committing (docs-need-execution-evidence)
   - Commit: `docs(spec): document slice builtin type (#1610)`

6. **Test fixtures** —
   - `slice_type_annotation_1610.spy` + `.expected` — `s: slice` annotation resolves
   - `slice_constructor_1610.spy` + `.expected` — `slice(1, 5, 2)` constructs; properties read back
   - `slice_user_getitem_1610.spy` + `.expected` — user class with `__getitem__(slice)` sliced
   - `slice_constructor_no_args_1610.spy` + `.error` — `slice()` with no args refused
   - Commit: `test: slice builtin type fixtures (#1610)`

### Phase 5: Tuple constant-bound slicing (#1609)

**Goal:** `t[1:]` on a tuple with constant bounds returns a narrower `TupleType`.

#### Tasks

1. **Implement constant-bound tuple slice classification** — `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Literals.cs`
   - In `CheckSliceAccess`, replace the tuple dedicated refusal with:
   - Try to resolve each present bound as a compile-time constant int (reuse the
     `TryGetConstantIntIndex` approach, extended to handle const-reference folding)
   - If ALL present bounds are constant non-negative ints: compute the result element
     positions from the tuple's known arity, construct a `TupleType` with the selected
     element types
   - If any bound is non-constant, negative, or a non-trivial step: refuse with a
     diagnostic steering to the constant-bound requirement
   - Record a new `SliceLoweringKind.Tuple` with the computed element indices
   - v1 scope: positive bounds only; step must be absent or `1` (no reverse slicing);
     negative bounds refused with steer
   - Acceptance: `t: tuple[int, str, float] = (1, "a", 3.14); x = t[1:]` has type
     `tuple[str, float]`
   - Commit: `feat(semantic): tuple constant-bound slicing (#1609)`

2. **Codegen for tuple slicing** — `src/Sharpy.Compiler/CodeGen/`
   - New arm in the `SliceLowering.Kind` switch for `Tuple`:
   - Lower to a `ValueTuple` construction from the selected `.ItemN` fields
   - e.g., `t[1:]` on `(int, str, float)` → `ValueTuple.Create(t.Item2, t.Item3)`
   - Acceptance: the tuple slice compiles to valid C# and executes
   - Commit: `feat(codegen): emit tuple slice as ValueTuple construction (#1609)`

3. **Update #1608's tuple refusal message** — `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Literals.cs`
   - The existing tuple refusal message steers to #1609. Now that constant-bound slicing
     works, the refusal should only fire for non-constant bounds and say something like
     "Tuple slicing requires constant integer bounds" (not "does not support slicing")
   - Commit: (fold into Task 1)

4. **Test fixtures** —
   - `tuple_slice_constant_1609.spy` + `.expected` — `t[1:]`, `t[:2]`, `t[1:3]` with verified output
   - `tuple_slice_non_constant_1609.spy` + `.error` — variable bound refused
   - `tuple_slice_negative_1609.spy` + `.error` — negative bound refused with steer
   - `tuple_slice_step_1609.spy` + `.error` — non-trivial step refused
   - `tuple_slice_const_ref_1609.spy` + `.expected` — `N: int = 1; t[N:]` works (const-ref folding)
   - Verify Python behavior: `python3 -c "t = (1, 'a', 3.14); print(t[1:], t[:2])"`
   - Commit: `test: tuple constant-bound slicing fixtures (#1609)`

5. **Spec update** — `docs/language_specification/collection_types.md`
   - Update the "Slicing" section's tuple paragraph: constant-bound slicing now works,
     non-constant/negative/step refused
   - Execute every example
   - Commit: `docs(spec): tuple constant-bound slicing (#1609)`

### Phase 6: NuGet version resolution (#1580, #1581)

**Goal:** Resolver reads restore's decided versions; dropped packages fail loudly.

#### Tasks

1. **Parse `project.assets.json` before cleanup** — `src/Sharpy.Compiler/Project/NuGetRestorer.cs`
   - In `RestorePackages`, after the `dotnet restore` call succeeds and before the
     `finally` cleanup:
   - Read `{tempDir}/obj/project.assets.json`
   - Parse the JSON to extract `libraries` keys (format: `"PackageName/Version"`)
   - Build a `Dictionary<string, string>` mapping lowercase package name to resolved version
   - Return this alongside the existing result, or add it to a return struct
   - Acceptance: `NuGetRestorer.RestorePackages` returns the resolved version even when the
     requested version doesn't exist exactly (NuGet resolves upward)
   - Commit: `fix(project): read project.assets.json for resolved versions (#1580)`

2. **Use resolved versions in `NuGetResolver`** — `src/Sharpy.Compiler/Project/NuGetResolver.cs`
   - `ResolvePackage` receives the resolved-version map from the restorer
   - Look up by lowercase package name → resolved version → build the cache path
   - If the package is in the requested set but NOT in the resolved map: it was
     dropped by restore — emit a real diagnostic (allocate SPY09xx, e.g., SPY0911 [CORRECTED: SPY0910 is already `ReferenceAcquisitionFailed`; next available is SPY0911]:
     "Package '{name}' version '{version}' was not resolved by NuGet restore")
   - The diagnostic goes through the compilation result, visible at default verbosity
   - Remove or downgrade the existing `LogWarning` to `LogDebug` (it's now redundant)
   - `DiagnosticExplanations` entry for the new code
   - Acceptance: `PackageReference Version="2.11.23"` for Humanizer.Core either resolves
     to the actual version (2.13.14) or fails loudly — never silent
   - Commit: `fix(project): use resolved versions and surface dropped packages (#1580)`

3. **Add floating-version test** —
   - Test with a `Version="2.*"` style floating reference to verify it resolves
   - Commit: (fold into Task 2)

4. **Document SDK requirement** — release notes + skill docs (#1581)
   - State plainly: `<PackageReference>` support requires the .NET SDK on PATH; the
     dotnet-tool install path always has it, self-contained archives do not
   - The existing error message at `94f01b44c` is adequate code-wise
   - Update `docs/` or `README.md` release notes section and the
     `.claude/skills/new-sharpy-project/` skill docs if they mention packages
   - Acceptance: the limitation is documented in at least two user-facing locations
   - Commit: `docs: document SDK requirement for PackageReference restore (#1581)`

### Phase 7: Stdlib fidelity — json lone surrogates (#1597)

**Goal:** Both `loads` doors refuse raw lone surrogates with one shared message.

#### Tasks

1. **Extend `FirstUnpaired` to scan raw code units** — `src/Sharpy.Stdlib/json/JsonSurrogateEscapes.cs`
   - Currently at `:20`, `FirstUnpaired(string json)` only scans `\uXXXX` escape sequences
   - Add a scan for raw `char` values in the U+D800–U+DFFF range: if `char.IsHighSurrogate(c)`
     without a following `char.IsLowSurrogate` (or vice versa), report it
   - This naturally covers both escaped and raw surrogates in one pass
   - Both `Loads` (untyped, `:94`) and `Loads<T>` (typed, `:397`) already call
     `FirstUnpaired`, so both doors refuse automatically
   - Shared error message: follow the #1487 pattern — the message text should match
     the existing unpaired-escape message style
   - Acceptance: `json.loads('"\ud800"')` (raw surrogate) returns `Err` with the shared message
     on BOTH doors — no longer diverges
   - Commit: `fix(stdlib/json): refuse raw lone surrogates in both loads doors (#1597)`

2. **Convert divergence pin to agreement pin** — `src/Sharpy.Stdlib.Tests/Conformance/JsonLoadsAgreementTests.cs`
   - `RawLoneSurrogateChar_UntypedAcceptsWhileTypedRefuses` at `:321–334`:
     rewrite as `RawLoneSurrogateChar_BothDoorsRefuse` — assert both return `Err`
     with the shared message
   - The self-destruct comment at `:331` directs exactly this
   - Drain the divergence cell from `EveryValueExemptionStillDiverges` at `:340–361`
     (if this cell was in that list — verify)
   - Acceptance: `JsonLoadsAgreementTests` all green with no divergence pins for this cell
   - Commit: `test(stdlib/json): convert surrogate divergence pin to agreement (#1597)`

### Phase 8: Stdlib fidelity — yaml escape rendering (#1598)

**Goal:** Match PyYAML escapes if cheap; otherwise pin + document deviation.

#### Tasks

1. **Measure the cost** — `src/Sharpy.Stdlib/yaml/YamlRoundtrip.cs`
   - YamlDotNet has no public hook for per-character escape rendering
   - Investigate two approaches:
     (a) Post-process: after `Emitter` writes to the `StringWriter`, regex-replace raw
         control bytes (0x00–0x1F except 0x0A/0x0D which are line breaks) with their
         YAML escape sequences (`\t`, `\0`, `\a`, `\b`, `\v`, `\f`, `\e`, `\xNN`)
     (b) Custom `IEmitter` wrapper that intercepts `Emit(ScalarEvent)` and re-escapes
   - Measure: run the yaml conformance tests with approach (a) and compare output to
     PyYAML oracle. Check edge cases: multi-line strings, already-escaped sequences,
     single-quoted scalars (which should NOT be touched)
   - Report which arm applies before writing the fix
   - Commit: (no commit — measurement only)

2. **If cheap: implement post-processing** — `src/Sharpy.Stdlib/yaml/YamlRoundtrip.cs`
   - The post-processing must ONLY replace raw control bytes inside double-quoted scalars
     (not in single-quoted, literal, folded, or unquoted)
   - This is tricky because the emitter output is a serialized YAML string, not a
     structured stream at this point
   - A simpler approach: intercept at the `Scalar` event level before the emitter writes,
     pre-escaping the value — but this requires YamlDotNet to preserve the escapes
   - Acceptance: `yaml.safe_dump({'k': 'tab\tchar'})` produces `k: "tab\tchar"` (escape,
     not raw byte)
   - Commit: `fix(stdlib/yaml): match PyYAML escape rendering in double-quoted scalars (#1598)`

3. **If NOT cheap: document deviation** —
   - Update the existing pinned test `KeyPositionStyling_MatchesPyYaml` with a comment
     documenting the measured cost and the permanent deviation
   - Add a deviation entry to `docs/language_specification/` or stdlib docs
   - Record the measured cost in the #1598 close comment
   - Commit: `docs(stdlib/yaml): document escape rendering deviation (#1598)`

4. **Oracle re-run** — re-run the PyYAML oracle on the escape cells to verify alignment
   (or document the remaining deviations if the pin arm was chosen)
   - Commit: (fold into the chosen arm's commit)

## Testing Strategy

- **Phase 1–2 (scripts):** Manual mutation tests — move lock path, verify scripts fail loudly;
  kill mid-run, verify lock released before exit
- **Phase 3 (#1606):** Unit test for `WaitFirstLoudlyThenDrain` — no-notification source
  throws on first wait. Existing LSP E2E tests as regression gate
- **Phase 4 (#1610):** 4+ file-based fixtures (annotation, constructor, user protocol,
  no-args error). Verify Python behavior for constructor semantics
- **Phase 5 (#1609):** 5+ file-based fixtures (positive slices, non-constant refused,
  negative refused, step refused, const-ref folding). Verify Python tuple slicing
- **Phase 6 (#1580):** Integration test with nonexistent version (resolves upward) and
  floating version. Verify diagnostic visible at default verbosity
- **Phase 7 (#1597):** Convert existing divergence pin to agreement pin. Run full
  `JsonLoadsAgreementTests`
- **Phase 8 (#1598):** PyYAML oracle re-run on escape cells. Round-trip tests stay green

## Issues to Close

- #1566 — scripts lock-path consolidation (closed by Phase 1, Tasks 1–4)
- #1567 — watchdog bounded wait (closed by Phase 2, Task 1)
- #1606 — LSP drain-loop audit (closed by Phase 3, Tasks 1–3)
- #1610 — slice builtin type (closed by Phase 4, Tasks 1–6)
- #1609 — tuple constant-bound slicing (closed by Phase 5, Tasks 1–5)
- #1580 — NuGet version resolution (closed by Phase 6, Tasks 1–3)
- #1581 — self-contained restore docs (closed by Phase 6, Task 4)
- #1597 — json lone surrogates (closed by Phase 7, Tasks 1–2)
- #1598 — yaml escape rendering (closed by Phase 8, arm-dependent)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-08-24
**Plan file:** `.claude/plans/plan-6e12b5.md`

### Corrections Made

1. **`ConstantFoldingVisitor` → `IntegerConstantEvaluator`** (Current State, #1609): `ConstantFoldingVisitor` does not exist. The semantic-layer constant folding mechanism is `IntegerConstantEvaluator` (`Semantic/IntegerConstantEvaluator.cs`); the lowering layer has `ConstFoldPass` (`Lowering/Passes/ConstFoldPass.cs`).

2. **LSP file paths** (Phase 3, Tasks 1–3): All three LSP test files are under `src/Sharpy.Lsp.Tests/E2E/`, not directly under `src/Sharpy.Lsp.Tests/`:
   - `MultiFileTests.cs` → `E2E/MultiFileTests.cs`
   - `LspEditSessionTraceHarness.cs` → `E2E/LspEditSessionTraceHarness.cs`
   - Helper extraction site → `E2E/`

3. **SPY0910 already allocated** (Phase 6, Task 2): SPY0910 is `ReferenceAcquisitionFailed`. Corrected example to SPY0911 (next available in the SPY0900–SPY0999 infra range).

4. **`docs/language_specification/builtin_types.md` doesn't exist** (Phase 4, Task 5): No such file. Existing type spec files: `primitive_types.md`, `type_hierarchy.md`, `collection_types.md`. Corrected to direct implementer to choose or create.

### Warnings

1. **Slice.cs `end` vs Python `stop`** — The existing `Slice` struct uses field names `start`, `end`, `step` (lowercase, and `end` not `stop`). Python's `slice` type uses `start`, `stop`, `step`. The plan proposes adding `Start`/`Stop`/`Step` nullable properties (matching Python). The implementer should note the `end` → `Stop` naming bridge explicitly.

2. **`SliceLowering` record carries only `Kind`** (Phase 4, Task 4) — The current record is `SliceLowering(SliceLoweringKind Kind)` with no additional data. The proposed `UserProtocol` arm needs codegen to know the resolved `__getitem__` method and its return type. The record will need additional fields (e.g., `Symbol? UserMethod`, `SemanticType? ReturnType`) or the data must be carried via a separate `SemanticInfo` dictionary.

3. **`TryGetConstantIntIndex` doesn't handle const-ref folding** (Phase 5, Task 1) — `AstHelper.TryGetConstantIntIndex` (lines 17–33) only handles `IntegerLiteral` and `UnaryOp(Minus, IntegerLiteral)`. It does NOT resolve `NameAccess` to constant symbols. For `N: int = 1; t[N:]` to work (fixture `tuple_slice_const_ref_1609.spy`), the implementation must integrate with `IntegerConstantEvaluator.TryEvaluate` or the TypeChecker's `TryResolveConstantIntExpression` (`:592` in `TypeChecker.Utilities.cs`).

4. **Raw lone surrogate has no `ValueExemptions` entry** (Phase 7, Task 2) — The plan says "Drain the divergence cell from `EveryValueExemptionStillDiverges`... (if this cell was in that list — verify)". Verified: the raw surrogate char deliberately has NO corpus row (source comment at `JsonLoadsAgreementTests.cs:155–158` explains why). The `ValueExemptions` dict contains only non-finite-related entries. The drainage step is a no-op — only the separate `[Fact]` test at `:321–334` needs rewriting.

5. **MultiFileTests drain-loop line estimates are approximate** — Plan line numbers (~287, ~336, ~373, ~434, ~541) drift from HEAD. Actual 500ms-drain-inside-30s-Stopwatch sites at HEAD: ~280–291, ~331–340, ~369–377, ~446–458, ~537–548 (5 sites confirmed). The plan's count of 5 is correct. The instruction to "locate by `WaitForNotificationAsync` grep" is the right approach.

### Missing Steps Added

None — the plan is complete in scope.

### Unchecked Claims

1. **#1580 `project.assets.json` structure** — The claim that `targets` → `libraries` maps package names to resolved versions is a NuGet format detail not verified against a live restore output. Implementer should verify the JSON structure against an actual `project.assets.json` file.

2. **#1598 YamlDotNet emitter hooks** — The claim "no public hook for per-character escape rendering" was not verified against the YamlDotNet source. The measurement step (Phase 8, Task 1) covers this.
