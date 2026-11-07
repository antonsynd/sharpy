<!-- Verified by /verify-plan on 2026-08-31 @ d45034f52 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->

# P1b — Instrument Residue: fail-loud corpus harness, display-name uniqueness, dispatch-roster totality (#1711, #1714, #1709)

Round: `.claude/plans/remediation-round-2026-08-30-batching.md`, plan group **P1b** (2026-08-31 addendum). Ordered **first** in the round's remaining work: #1711 lands EARLY (fresh-seed sweeps keep firing all round; every hit before the fix is an unactionable loss), #1714 before the batches whose triage reads diagnostics, #1709 before P5/P6 (Batch 6 may add AST node kinds; Batch 8 adds a guarded switch).

**Standing round rules (restated per charter):** fix the class, not the cell (Rule 11); every new guard mutation-tested with red/green recorded in the commit body (Rule 12); absence assertions need a positive control; refusals verified by direction with `run` against the prior commit; commit gate = whole solution via `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` (filtered runs are the edit loop only); spec is authoritative and spec edits ship with executed examples; regenerate generated artifacts early and read the diff as findings.

All measurements below **@ d45034f52** (clean tree) unless noted.

## Context

The plan-46b871 verify round (P1/Batch 0) closed #1694's dispatch-roster vacuity class for a 9-file roster and shipped `SwitchArmScan`. Its close-out and the first post-push CI run surfaced three residues of the *instrument* kind — none is a compiler-semantics bug, all three degrade the round's ability to see compiler-semantics bugs:

- **#1711** — `PropertyCorpus.AssertAllPassOrAllowed` NREs before naming its counterexample. A fresh-seed property hit (observed once: `FString_WithNestedCalls_CompilesClean`, gate @ d45034f52, 16121/1/4 measured) died as `NullReferenceException at PropertyCorpus.cs:line 37` with the offending generated source lost.
- **#1714** — `NullableType` and `OptionalType` both render as `{T}?`. This display collision misled the #1712 author AND this round's triage in the same week (owner ruling R-F on #1712 documents it). It is the #882 notation collision reproduced inside the compiler's own output.
- **#1709** — #1694's vacuity class is still open at 3 dispatch sites: `AstVisitor.Visit` (both overloads — the compiler's largest AST dispatch, guarded today only by a METHOD-TABLE reflection test that case-deletion cannot turn red), `ExecutionOrderAnalyzer.CollectDeclarationNames`, `ControlFlowGraphBuilder.CollectPatternBindingKeysInto`.

Issues: [#1709](https://github.com/antonsynd/sharpy/issues/1709), [#1711](https://github.com/antonsynd/sharpy/issues/1711), [#1714](https://github.com/antonsynd/sharpy/issues/1714). None has comments; the authoritative rulings live in the charter addendum and in R-F posted on #1712.

## Current State (measured @ d45034f52)

**#1711 — the NRE is a collection race, root-caused this planning pass:**
- `src/Sharpy.TestInfrastructure/PropertyCorpus.cs` — `CompileSample` can never return null and never a null `ErrorCodes` (its whole body is try/catch; a throw becomes `EXCEPTION:{type}` — verified by reading the source). Line 37 is `if (s.Passed)` in the sample loop ⇒ the NRE requires a **null slot in the `samples` list itself**.
- All 8 corpus callers (`AsyncPropertyTests`, `ClassInheritancePropertyTests`, `ContextManagerPropertyTests`, `ExceptionPropertyTests`, `FStringPropertyTests`, `InterfacePropertyTests`, `OperatorOverloadPropertyTests`, `VariancePropertyTests` under `src/Sharpy.Compiler.Tests/Properties/Semantic/`) accumulate into a plain `new List<PropertyCorpus.SampleResult>()` from inside a CsCheck `.Sample(...)` callback, **none passes `threads:`**.
- CsCheck 4.8.0 (the referenced version, `Sharpy.Compiler.Tests.csproj:23`): `Sample`'s `threads` parameter **defaults to the number of logical CPUs** (package XML doc). Concurrent `List<T>.Add` during a resize publishes null slots and loses adds — exactly the observed rare, non-reproducing NRE (3 immediate re-runs green, per the issue).
- The repo already knows this race: `DifferentialExecutionTests.cs:988` and `MetamorphicCorpusInvarianceTests.cs:87` [CORRECTED: were :986/:85; `threads: 1` sits at :988 and :87 @ d45034f52] pass `threads: 1` with comments naming non-concurrent collections; `DecoratorPropertyTests.cs:132–178` uses `ConcurrentBag` + `Interlocked`. The 8 corpus callers are the arms that missed the pattern.
- Consumers of `Sharpy.TestInfrastructure`: `Sharpy.Compiler.Tests`, `Sharpy.Lsp.Tests`, `Sharpy.Stdlib.Tests` (project references); `Sharpy.Compiler` appears via `InternalsVisibleTo` (`Sharpy.Compiler.csproj:22` grants TestInfrastructure access, not a consumption edge) [CORRECTED: was listed as a consumer] — `AssertAllPassOrAllowed`/`CompileSample` callers are only the 8 files above (repo grep, re-verified).

**#1714 — collision reproduced live:**
- `Semantic/SemanticType.cs:460` (`OptionalType`) and `:560` (`NullableType`) are byte-identical renderers: `$"{UnderlyingType.GetDisplayName()}?"`.
- Probe (run @ d45034f52, `emit diagnostics`):
  - `x: int | None = None` + `y: int = x` → `error SPY0220 (3:5): Cannot assign type 'int32?' to variable of type 'int32'`
  - `x: int? = Some(42)` + `y: int = x` → **byte-identical message**. Two behaviorally distinct types, one spelling — the discriminating before/after pair for the fix.
- `NullableType`'s own doc comment (:545–:559) names its distinct surface spelling and warns "NOT the same as OptionalType".
- Other wrapper renderers are already unique: `ResultType` → `{Ok} !{Err}` (:518), `TaskType` → `Task[T]` (:1057).
- Consumer surface: **25** `.error` fixtures contain `?'` (re-measured); **11** fixtures whose `.spy` contains `| None` have an `.error` twin [CORRECTED: was 10; re-count excluding `bin/` crash copies gives 11 — `question_mark_on_nullable`, `dict_iterable_optional_rejected`, `basics/untyped_none_binding_refused_1516`, `interop/nullable_value_payload_refused_1399`, `errors/nullable_lambda_narrowing_isolation`, `errors/lambda_free_union_param`, `errors/nullable_function_boundary_narrowing_isolation`, `errors/optional_argument_not_unwrapped_1397`, `errors/maybe_unconstrained_generic`, `slicing/slice_refuse_optional_bound_1608`, `type_system/as_cast_maybe`] (list in Phase 2 Task 3). Most `?'` occurrences are OptionalType (e.g. `optional_argument_not_unwrapped_1397.error` says "the argument is Optional[...]" in the same line) and will NOT churn. Known NullableType cells that WILL churn: `interop/nullable_value_payload_refused_1399.error` (`'int32?'` for a `Nullable<int>` payload — the #1399 shape). `dict_iterable_optional_rejected` and `slice_refuse_optional_bound_1608` must be classified by their annotation origin during the sweep.
- One display-name use in CodeGen: `RoslynEmitter.Expressions.cs:874` — `type.GetDisplayName() == "object"` (`IsObjectType`). Verified @ d45034f52: this is the ONLY `GetDisplayName() ==` predicate in all of `src/Sharpy.Compiler` production code, and `GetDisplayName` has zero hits under `Project/` — the incremental-cache type serializer does not round-trip display names, so no hidden consumer there. Unaffected by direction ("object?" ≠ "object" before; "object | None" ≠ "object" after) but it is a display-string used as a type predicate — verify unchanged, note the smell (Phase 2 Task 4).
- LSP: hover text renders via `ITypeInfo.DisplayName` ⇒ `GetDisplayName()`; `HoverTests` currently asserts only OptionalType spellings (`str?` at :248) — no existing NullableType hover assertion. `FrontEndParityTests` compares **diagnostic-code multisets**, not message text ⇒ parity-neutral.
- Spec: `nullable_types.md` / `null_coalescing_assignment.md` / `walrus_operator.md` already spell the type `int | None` in annotations; the diagnostic examples in `tagged_unions_optional.md` (:182–:184, :228) are OptionalType messages and keep `T?`. No spec text currently shows a NullableType *diagnostic* rendering — nothing to rewrite, but Phase 2 executes the touched examples anyway.

**#1709 — the three sites and why the current guards are vacuous:**
- `Parser/Ast/AstVisitor.cs` — void `Visit` (`:16`, `switch (node)`, **94** `case` arms + `default: DefaultVisit`) and generic `AstVisitor<T>.Visit` (`:561`, `node switch`, **94** arms + `_ => DefaultVisit`). Arm rosters extracted and diffed this pass: **identical** across the two overloads. Visit-methods-minus-arms = exactly the 4 abstract-base helpers (`VisitExpression`, `VisitStatement`, `VisitPattern`, `VisitComprehensionClause`) ⇒ today every concrete-node Visit method has an arm; `TypeAnnotation` has both (the #1235 arm). The only totality-shaped guards, `AstVisitorTests.AllConcreteNodeTypes_HaveVisitMethod` / `...InGenericVisitor` (`Parser/Ast/AstVisitorTests.cs:195` ff.), read `GetMethods()` — deleting a `case` leaves the method in the table and the guard green (the #1694 vacuity shape). A silently deleted arm routes that node to `DefaultVisit` = generic child traversal — wrong behavior with no signal, in every visitor subclass at once.
- **Scanner gap found this pass:** `SwitchArmScan.FindMethods` matches by method NAME across the whole file — a scan for `"Visit"` in `AstVisitor.cs` merges BOTH overloads' arms into one set, so deleting an arm from ONE switch while its twin keeps it stays green. The guard needs per-containing-type scanning (Phase 3 Task 1).
- `Semantic/ExecutionOrderAnalyzer.cs:74` `CollectDeclarationNames` (`switch (stmt)`): arms = {`ClassDef`, `StructDef`, `FunctionDef`, `EnumDef`, `InterfaceDef`, `VariableDeclaration when const/CONSTANT_CASE`}. Sibling `:221` [CORRECTED: was :219] `CollectReferencedIdentifiers` IS scanned (`ExecutionOrderAnalyzerTotalityTests`, the exemplar shape: reflection universe + roster classification + `SetEquals` scan cross-check). Two suspicious cells visible by reading: `DecoratedStatement` is unwrapped by `DetectBasicIssues` (#1024 comment) but NOT by `CollectDeclarationNames` (a `@suppress`-decorated module-level def's name is never collected); `UnionDef`/`DelegateDef`/`EventDef`/`TypeAlias` declare names but have no arm. These are classification probes for Phase 3 Task 2, not pre-judged fixes.
- `Analysis/ControlFlow/ControlFlowGraphBuilder.cs:919` `CollectPatternBindingKeysInto` (`switch (pattern)`): 11 binding arms (`BindingPattern`, `AsPattern`, `OrPattern`, `AndPattern`, `TuplePattern`, `ListPattern`, `StarPattern when Capture != null`, `PositionalPattern`, `PropertyPattern`, `GuardPattern`, `UnionCasePattern`); no-arm pattern kinds = {`LiteralPattern`, `MemberAccessPattern`, `RelationalPattern`, `TypePattern`, `WildcardPattern`} (bind nothing — the justified fall-through roster). Sibling `BuildStatement:175` [CORRECTED: was :177] IS scanned (`CfgStatementTotalityTests:119`).
- Documented-by-design family (NO action, per the issue): `IrTreeRewriter.RewriteNode` / `Lowering/Passes/ComprehensionFusionPass.cs:44` / `Lowering/Passes/StackCollectionsPass.cs:46` / `Lowering/Passes/ConstFoldPass.cs:44` (all verified: `switch (node)` at exactly those lines) — `Parser/Ast/Types.cs:~18–25` [CORRECTED: full path added; the "deliberately not reachable … none of which has an exhaustiveness guard" contract text spans ~:15–:28] records the deliberate no-guard contract. The 4 non-scanner `*TotalityTests` (`BlockExpression`, `Ir`, `BuiltinTypeSerializer`, `SemanticTypeSerializer`) were triaged NOT-vacuous by the verify round.
- Fresh dispatch-site re-scan @ d45034f52 (`grep -rn "switch (node)\|switch (stmt)\|switch (pattern)\|switch (expr)" src/Sharpy.Compiler --include='*.cs'`): 41 sites [CORRECTED: was "~40"; re-measured 41 @ d45034f52]. The delta beyond the issue's enumeration is sites already covered by existing totality guards (`CodeGenInfoComputerTotalityTests`, `ModuleLoaderTotalityTests`, `TypeCheckerClrTypeTotalityTests`, `NameResolverDeclarationsTotalityTests`, `ExhaustivenessHelperTotalityTests`, `NarrowingFlowAnalysisTotalityTests`, `CfgStatementTotalityTests`, …), validator walkers whose default-ignore is contractual, and emitter/parser sites with their own conformance nets — **hypothesis, reconciled site-by-site in Phase 3 Task 4**, which turns the reconciliation into a standing inventory guard so a new unguarded dispatch cannot appear silently.

## Defect Class

- **Violated contracts (one per issue):**
  - #1711: *an instrument fails loudly* — a property harness that hits names its counterexample (index + generated SOURCE); no path through the harness ends in an unattributed crash. (verification-contract §2, "instruments that pass vacuously" family.)
  - #1714: *a `SemanticType`'s display name is its unique Sharpy surface spelling* — two behaviorally distinct semantic types never render identically.
  - #1709: *a dispatch roster derives from the dispatch it documents, never hand-maintained (or reflected) beside it* — the #1694 contract, applied to the 3 sites outside the plan-46b871 roster.
- **Meta-class → standard cure** (verification-contract §1 table):
  - #1711 → "Instruments that pass vacuously" → fail loudly + a shared helper that makes the wrong pattern unwritable (thread-safe accumulation lives in the harness, not in 8 call sites).
  - #1714 → "Silent wrong behavior" → fix first, then a conformance sweep over the sibling cells (pairwise uniqueness over ALL wrapper kinds, not just the colliding pair).
  - #1709 → "Instruments that pass vacuously" → scanner-consuming totality guards + a standing site inventory so the class cannot recur.
- **Known member cells:** #1711 (`FStringPropertyTests`, 1 hit), #1714 (SPY0220 probe pair), #1709 (3 sites).
- **Sibling cells this plan must also cover** (the issues' repro lists are symptom reports):
  - #1711: all 8 corpus callers (not just FString) + an audit of every other `Properties/` harness that accumulates from a `.Sample` callback (the `ConcurrentBag`/`lock`/`threads:1` files are the pass-set; anything else is a cell).
  - #1714: all wrapper display renderers pairwise (`OptionalType`, `NullableType`, `ResultType`, `TaskType`, `GenericType`, `FunctionType`, `TupleType`, …) over the same underlying type — not just the one colliding pair; plus every display consumer surface (`.error` fixtures, programmatic message asserts, LSP hover, spec examples).
  - #1709: both `Visit` overloads independently (the scanner merges them today — a per-overload deletion is its own cell); the statement axis of `CollectDeclarationNames` including the `DecoratedStatement`-unwrap and declares-a-name-but-uncollected cells; the pattern axis of `CollectPatternBindingKeysInto`; and the repo-wide site inventory (Task 4) so the "one more sibling switch" cell class closes structurally.
- **Standing harness that should have caught it:** #1711 — none (Phase 1 makes the harness itself the guard, with its own tests); #1714 — none (Phase 2 adds the pairwise-uniqueness guard); #1709 — `SwitchArmScan`-consuming totality tests existed but only for the plan-46b871 roster; Phase 3 extends them and adds the standing `DispatchSiteInventory` guard.
- **Owner rulings (authoritative, do not re-ask):** charter addendum 2026-08-31 (placements + "land #1711 EARLY"); **R-F** posted on #1712 2026-08-31 — confirms the checker records `OptionalType` for `as?` and that the claimed checker/emitter disagreement was this display artifact; nullable stays `T | None`, Optional stays `T?` at the TYPE level (#882). R-F fixes #1714's direction: **NullableType moves to `{T} | None`; OptionalType keeps `{T}?`.**

## Adversarial Review (pre-mortem)

- **Alternative root causes & discriminating probes:**
  - #1711: *alternative:* `CompileSample` returns null on some rare path. *Refuted by reading:* its whole body is try/catch returning a constructed record; no return path yields null. *Alternative 2:* xUnit's `ITestOutputHelper` races. *Refuted:* the NRE stack is at the sample loop (`:37`), before any output write. The race hypothesis is *positively* discriminated by Phase 1's stress probe: plain-`List` accumulation under `threads: default` with high `iter` must reproduce count-loss/null-slots at measurable frequency; the thread-safe helper at the same `iter` must not (record both frequencies in the commit body). If the stress probe does NOT reproduce, say so in the commit body and keep the fail-loud guard anyway — the guard is load-bearing regardless of which writer produced the null.
  - #1714: *alternative:* the probe messages could differ for checker reasons (different SPY code) rather than display. *Refuted by measurement:* both probes emit SPY0220 with byte-identical text @ d45034f52.
  - #1709: *alternative:* the method-table tests are adequate because a deleted arm would break behavior tests. *Refuted by design:* a deleted arm falls into `DefaultVisit` = child traversal, which many visitors survive silently; the issue's code-evidence stands. The mutation records (arm-deletion → red) are the positive proof the NEW guards close it.
- **How the fix could be inert:**
  - #1711: guarding nulls in `AssertAllPassOrAllowed` but leaving the 8 `List` accumulators racy = the counterexample is now *named* but the sample was still lost/corrupted. Both halves ship; the helper makes the racy pattern unwritable at call sites.
  - #1714: changing the renderer but missing a consumer that formats the type itself (bypassing `GetDisplayName`) would leave ambiguous spellings. The fixture sweep greps message *text*, not renderers, so bypass sites surface as un-churned `?'` NullableType cells — classify each of the 25 files explicitly.
  - #1709: scanning `"Visit"` file-wide is the inert-guard trap (per-overload deletions invisible) — the containing-type scanner extension is a hard prerequisite, mutation-tested per overload.
- **Before/after by direction:** no user-visible refusal changes anywhere in this plan. #1714 changes diagnostic TEXT only (codes unchanged — FrontEndParity's multiset comparison proves no code drift); the probe pair goes from identical to distinct messages, both still errors. #1709's guards are test-only *unless* a classification probe in Phase 3 Task 2 turns up a live behavioral gap (e.g. the `DecoratedStatement` cell); any production arm added there is verified by direction: `run` the probe program at the prior commit vs after — a changed diagnostic set is reported with both readings, and anything that newly rejects a previously-working program is a finding to surface, not silently ship.
- **Blast radius:** `Sharpy.Compiler.Tests` (fixtures + property + totality suites), `Sharpy.Lsp.Tests` (hover displays; parity is code-based), `Sharpy.Stdlib.Tests` and `Sharpy.Cli.Tests` (compile Sharpy source — display text reaches any message asserts), the three GapDiscovery sweeps (message-substring allowlists could reference `T?` spellings — grep `interop-allowlist.txt` and peers during Phase 2). Whole-solution gate per phase-closing commit.
- **Generated artifacts touched:** none by construction (display names never reach emitted C#; the one CodeGen display use is a comparison verified unchanged). Run the spy-stdlib + spy-test regen once EARLY in Phase 2 and cite "regen: no diff @ sha" as evidence rather than assumption.
- **Mutation-test hygiene:** production-hunk mutations (arm deletions, renderer revert) are restored from `cp` copies, never git (§2/§9); after any mutation run that rebuilt the compiler, clear fixture `bin/` (stale-binary poison); mutation runs use the filtered edit loop, only the phase close cites the whole-solution gate.

## Design Decisions

1. **Thread safety lives in the harness, not the call sites** (#1711). New `PropertyCorpus.CompileAll(Gen<string> gen, int iter, string label)` runs the CsCheck sample loop internally with a concurrent collection and returns `IReadOnlyList<SampleResult>`; callers become one-liners. Rationale: the §1 cure "a shared helper makes the wrong pattern unwritable" — 8 hand-rolled accumulators is how the class formed. `threads: 1` per call site is rejected: it loses parallelism and survives only until the next copy-pasted test forgets it.
2. **Fail-loud is independent of the race fix** (#1711). `AssertAllPassOrAllowed` null-guards each slot and `ErrorCodes`, failing with the sample index and source (or "null sample slot at index N — concurrent collection bug in the caller" when the record itself is null). Defense in depth: a future caller that bypasses `CompileAll` still gets a named failure, never an NRE.
3. **`NullableType` renders `{T} | None`; `OptionalType` keeps `{T}?`** (#1714). Fixed by owner ruling R-F and the #882 notation memo; matches the spec's annotation spelling (`nullable_types.md`, `null_coalescing_assignment.md`). Critical Rule 1 waiver is explicit and scoped: `.error`/hover expectations that change do so because the *rendering contract* changes (documented in #1714 first), never to make a failing behavior test pass.
4. **Rosters derive from the dispatch; reflection provides the universe** (#1709, the #1694 pattern). Each guard asserts `SwitchArmScan` arms `SetEquals` (roster), and separately that roster ∪ justified-no-arm roster covers the reflection universe of the dispatched base type, with disjointness and phantom checks — the `ExecutionOrderAnalyzerTotalityTests` exemplar shape. For `AstVisitor` the handled roster is not hand-maintained: assert arms == (concrete `Node` universe − justified-default roster), per overload, where the justified-default roster is **empty today** (measured: 94 arms == every concrete node kind with a Visit method) and any future member needs a stated reason string.
5. **`SwitchArmScan` gains containing-type discrimination** rather than the guard asserting on merged overloads. An arm-count/appears-exactly-twice workaround was rejected: it cannot say *which* overload lost the arm and breaks the moment the overloads legitimately diverge.
6. **The class closes with a standing inventory, not another enumeration** (#1709 Task 4). A `DispatchSiteInventoryTests` guard greps the production tree for hand-rolled AST dispatch switches and requires every site to carry a rostered justification (guarded-by-X / documented-by-design / walker-default-contract). This is the harness that would have caught #1709 itself; without it the next sibling switch reopens the class.
7. **Axioms:** untouched — no user-facing language surface changes; Rule 2 unaffected (no semantic→codegen facts move). No new SPY codes ⇒ no `DiagnosticExplanations` entries (Rule 10 vacuously satisfied — state it in the close-out).

## Implementation

### Phase 1: #1711 — PropertyCorpus fails loudly and collects safely

**Goal:** a property-corpus hit can never again die unattributed; the racy accumulation pattern becomes unwritable.

**Acceptance (class-level, measurable):**
- New `PropertyCorpusTests`: a synthetic samples list with a null slot fails with a message naming the index (no NRE); positive control — a real failing sample's message contains its SOURCE; `EXCEPTION:` path covered.
- All 8 corpus callers use `CompileAll`; repo grep for `new List<PropertyCorpus.SampleResult>()` inside `Properties/` returns 0.
- Deterministic helper test: `CompileAll` over a trivial generator returns exactly `iter` non-null samples under default threads.
- Sibling audit table (every `Properties/` file that accumulates from a `.Sample` callback → pass-set or fixed) recorded in the commit body.
- Guard mutations recorded: (a) revert `CompileAll`'s internals to a plain `List` (cp-copy) → stress probe red (count-loss/null observed, frequency noted) → restore green; (b) disable the null-guard in `AssertAllPassOrAllowed` → null-slot test red (NRE) → restore green.
- Whole-solution gate green @ the phase-closing sha (counts cited `passed/failed/skipped @ sha (measured)`).

#### Tasks

1. **Fail-loud assert + harness tests** — `src/Sharpy.TestInfrastructure/PropertyCorpus.cs`, new `src/Sharpy.Compiler.Tests/Infrastructure/PropertyCorpusTests.cs`
   - Null-guard each `samples[i]` and `s.ErrorCodes` in `AssertAllPassOrAllowed`; a null slot or null codes list becomes a named failure (`"null sample slot at index {i} — concurrent collection bug in the caller"` / index + source for null codes). Keep the existing per-failure SOURCE report.
   - Tests: null-slot → named failure not NRE; null-`ErrorCodes` → named failure; positive control: failing sample names source; `CompileSample` of a throwing input yields `EXCEPTION:` code (positive control for the catch-all).
   - Acceptance: bullets 1 and mutation (b).
   - Commit: `test(infra): PropertyCorpus fails loudly — null-guarded assert names the sample index and source (#1711)`

2. **Thread-safe `CompileAll` + migrate the 8 callers** — `PropertyCorpus.cs`, the 8 files under `src/Sharpy.Compiler.Tests/Properties/Semantic/`
   - `CompileAll(Gen<string> gen, int iter, ...)`: internal `ConcurrentQueue<SampleResult>` (or `lock`ed add), snapshot to `IReadOnlyList` on return; callers collapse to `var samples = PropertyCorpus.CompileAll(GenFStrings.FStringWithNestedCalls(), iter: 50);` + the existing assert call.
   - Stress probe (commit-body evidence, not a permanent flaky test): plain-`List` variant under default threads at high `iter` — record observed loss/NRE frequency vs `CompileAll`'s 0; cite CsCheck 4.8.0's documented `threads` default.
   - Deterministic permanent test: `CompileAll` returns exactly `iter` non-null samples.
   - Acceptance: bullets 2–3 and mutation (a).
   - Commit: `test(infra): corpus samples accumulate thread-safely under CsCheck's parallel Sample; callers migrate to CompileAll (#1711)`

3. **Sibling harness audit** — `src/Sharpy.Compiler.Tests/Properties/**`
   - Inventory every `.Sample(`-callback accumulator: the known pass-set (`ConcurrentBag`/`lock` users: MetamorphicCorpusSweep, DeferRoundTrip, CsClean, ILCompiles, DiagnosticSpan, ErrorCompleteness, TypeSoundness, Decorator, Async†, SemanticInfoConsistency, Shrinker; `threads: 1` users: DifferentialExecution, MetamorphicCorpusInvariance — † Async appears in both lists, re-check which member it is) vs any remaining plain-`List`/plain-counter accumulators outside the 8; fix stragglers via `CompileAll` or `threads: 1`-with-comment, or record "none found".
   - [ADDED: missing step] Audit-space measurement @ d45034f52: **60** files under `Properties/` call `.Sample(`; **24** of them carry NO thread-safety marker at all (`ConcurrentBag`/`threads: 1`/`Interlocked`/`lock (`), including files outside the plan's named lists (e.g. the 9 `Algebraic/` suites, `IncrementalCompilation`, `NameMangling`, 3 `Lexer/` suites, `MetamorphicPropertyTests`, `CompoundStatementRoundTrip`, `ParserFromString`, `UnparseIdempotence`, `BuiltinShadowing`, `Determinism`, `MultiFileDeterminism`, `OverloadOrderIndependence`, `SymbolPosition`). Most likely assert inside the callback without accumulating (which is safe) — but each of the 24 must be explicitly classified in the audit table, not just the files the plan names. Several of the 8 racy callers also contain a safe accumulator in another test in the same file — classify per accumulation site, not per file.
   - Acceptance: bullet 4.
   - Commit: `test(infra): audit Sample-callback accumulators across Properties — inventory recorded, stragglers migrated (#1711)`

### Phase 2: #1714 — display-name uniqueness

**Goal:** `NullableType` renders `{T} | None`; no two distinct wrapper kinds render identically; every display consumer reflects the new contract.

**Acceptance (class-level, measurable):**
- The Phase-0 probe pair now prints DISTINCT messages @ the phase sha: `'int32 | None'` vs `'int32?'` in SPY0220 (execution evidence, `emit diagnostics` both cells).
- New pairwise-uniqueness guard over all wrapper kinds sharing an underlying type; **recorded red BEFORE the renderer fix** (the OptionalType-vs-NullableType pair is the positive control — equal today) and green after; mutation: revert `NullableType.GetDisplayName` (cp-copy) → red → restore.
- All 25 `?'`-containing `.error` fixtures classified Optional-vs-Nullable in the commit body; NullableType cells updated to `| None`; OptionalType cells untouched (they are the sweep's no-churn positive controls).
- LSP hover: a new `HoverTests` case for a `x: int | None` binding asserts `int | None`; existing OptionalType hover cases unchanged.
- GapDiscovery allowlists grepped for `?` spellings tied to NullableType messages — churn drained or "none found" recorded.
- `regen: no diff @ sha` for spy-stdlib C# + spy-test C# + stdlib docs (run EARLY in this phase).
- Whole-solution gate green @ the phase-closing sha.

#### Tasks

1. **Renderer + guard** — `src/Sharpy.Compiler/Semantic/SemanticType.cs:560`, new `src/Sharpy.Compiler.Tests/Semantic/SemanticTypeDisplayUniquenessTests.cs`
   - Write the guard FIRST and record its red (collision) run; then change `NullableType.GetDisplayName()` to `$"{UnderlyingType.GetDisplayName()} | None"`; guard green.
   - Guard shape: construct each wrapper kind over `int64` (`OptionalType`, `NullableType`, `ResultType` (both params `int64`), `TaskType`, `GenericType("list",[int64])`, …); assert all pairwise display names distinct; second fact asserts the two contract spellings verbatim (`"int64 | None"`, `"int64?"`).
   - Commit: `fix(semantic): NullableType displays as 'T | None', distinct from OptionalType's 'T?' — pairwise uniqueness guarded (#1714)`

2. **Early regen** — `build_tools/regenerate_spy_stdlib.sh`, spy-test regen, stdlib docs
   - Run immediately after Task 1; expected no diff (display text never reaches emitted C#); a non-empty diff is a FINDING to investigate before proceeding.
   - Commit (only if diff): `chore(regen): artifacts after NullableType display change (#1714)` — else cite "regen: no diff @ sha" in Task 3's body.

3. **Consumer sweep** — `TestFixtures/**/*.error`, programmatic message asserts, `src/Sharpy.Lsp.Tests/HoverTests.cs`, GapDiscovery allowlists
   - Classify each of the 25 `?'` `.error` files by the fixture's annotation/interop origin (`| None` or CLR `Nullable`/reference-nullable ⇒ churn; `T?`/`Some(...)` ⇒ control). Known churn cell: `interop/nullable_value_payload_refused_1399.error`; classify `dict_iterable_optional_rejected`, `slice_refuse_optional_bound_1608` explicitly. Grep programmatic asserts (`Compiler.Tests`, `Stdlib.Tests`, `Cli.Tests`) for affected substrings; add the `int | None` hover case; grep allowlist files for stale spellings.
   - Re-run the probe pair; paste both outputs in the commit body (the discriminating execution evidence).
   - Verify `RoslynEmitter.Expressions.cs:874` behavior unchanged (its `== "object"` compare); note the display-as-predicate smell in the commit body and file a small issue if it is load-bearing (`gh issue create`, referenced from a `// TODO(#NNN)` only if a comment is added — Rule 8).
   - Commit: `test(fixtures,lsp): NullableType display churn — 25 ?'-cells classified, Nullable cells assert 'T | None' (#1714)`

### Phase 3: #1709 — dispatch-roster totality at the three residual sites

**Goal:** case-deletion at any of the three sites turns a test red; the vacuity class gets its standing harness.

**Acceptance (class-level, measurable):**
- `SwitchArmScan` scans per containing type; self-tests cover the AstVisitor two-overload case (filtered scans return each overload's arms; wrong type name throws).
- New guards, each with the exemplar structure (non-empty positive control first; roster classification over the reflection universe; `SetEquals` against the scan; disjointness + phantom checks) and each with a recorded arm-deletion mutation (cp-restore, filtered red run, restore green, fixture `bin/` cleared after compiler-rebuilding mutations):
  - `AstVisitorTotalityTests`: per-overload arms == concrete `Node` universe − justified-default roster (empty today); mutations recorded for BOTH overloads independently (delete `case FStringLiteral n:` from the void switch only → red; delete `FStringLiteral n =>` from the generic switch only → red).
  - `ExecutionOrderAnalyzerTotalityTests` extension for `CollectDeclarationNames` (statement axis); mutation: delete the `EnumDef` arm → red.
  - `CfgPatternBindingTotalityTests` for `CollectPatternBindingKeysInto` (pattern axis); mutation: delete the `AsPattern` arm → red.
- The `DecoratedStatement` / `UnionDef` / `TypeAlias` / `DelegateDef` / `EventDef` cells of `CollectDeclarationNames` carry an explicit classification each (arm added, or justified-no-arm reason string), decided by behavioral probe (`run` a module-level program exercising the cell at the prior commit vs after); any probe that reveals silent wrong behavior larger than an arm-add is FILED (`gh issue create`), not squeezed into this plan.
- `DispatchSiteInventoryTests`: repo scan finds every `switch (node|stmt|pattern|expr)` site in `src/Sharpy.Compiler`; every site rostered with a justification; mutation: delete one roster row → red; positive control: each justification category non-empty.
- Whole-solution gate green @ the phase-closing sha.

#### Tasks

1. **Scanner extension** — `src/Sharpy.Compiler.Tests/Infrastructure/SwitchArmScan.cs`, `SwitchArmScanTests.cs`
   - Add `containingTypeName` overloads to `CaseTypeNames`/`ArmPatternTexts` (filter `FindMethods` by ancestor `TypeDeclarationSyntax`; throw when the type or method is absent). [CORRECTED: identifier alone cannot discriminate — `AstVisitor` (:9) and `AstVisitor<T>` (:561) share `Identifier.Text == "AstVisitor"`; the filter must match identifier **plus arity** (`TypeDeclarationSyntax.Arity`, e.g. accept `"AstVisitor"` vs ``"AstVisitor`1"`` metadata-style names, or an explicit arity parameter).] Self-tests: `("AstVisitor.cs","Visit","AstVisitor")` and ``(...,"AstVisitor`1")`` each return 94 distinct type names; unfiltered scan returns the union; wrong type throws.
   - Commit: `test(infra): SwitchArmScan scans per containing type — the two-overload Visit case is separable (#1709)`

2. **AstVisitor guard** — new `src/Sharpy.Compiler.Tests/Parser/Ast/AstVisitorTotalityTests.cs`
   - Universe: `assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Node)) && !t.IsAbstract)`. Assert per overload: scan arms `SetEquals` (universe − justified-default roster); roster empty with a comment defining the entry contract (name + reason); cross-overload `SetEquals` (they are identical today and must not drift silently); keep the existing method-table tests (they guard the override surface — complementary, per-file-noted).
   - Record both per-overload mutations (acceptance bullet).
   - Commit: `test(parser): AstVisitor dispatch totality — per-overload arms equal the concrete Node universe (#1709)`

3. **CollectDeclarationNames guard + cell classification** — `src/Sharpy.Compiler.Tests/Semantic/ExecutionOrderAnalyzerTotalityTests.cs`, possibly `src/Sharpy.Compiler/Semantic/ExecutionOrderAnalyzer.cs`
   - New facts in the existing file (same rosters style): `Collected` = the 6 arm types; `DeclaresNameNotCollected` and `NoDeclaredName` rosters over the concrete `Statement` universe; scan cross-check via the (now type-filtered where needed) scanner.
   - Behavioral probes decide the 5 open cells (module-level `@suppress`-decorated def referenced from an initializer; `UnionDef`/`TypeAlias`/`DelegateDef`/`EventDef` names referenced from initializers): `run` before/after any arm added; report direction per §3; oversize findings get filed issues and a justified-roster entry citing them.
   - Commit: `test(semantic): CollectDeclarationNames joins the ExecutionOrderAnalyzer totality guard; declaration-name cells classified (#1709)`

4. **Pattern-collector guard** — new `src/Sharpy.Compiler.Tests/Analysis/ControlFlow/CfgPatternBindingTotalityTests.cs`
   - `Binding` (11) / `NonBinding` (5) rosters over the concrete `Pattern` universe; scan `SetEquals`; note `StarPattern`'s `when Capture != null` guard and `PropertyPatternField` (a `Node`, reached via `PropertyPattern` — assert it stays out of the `Pattern` universe).
   - Commit: `test(analysis): CollectPatternBindingKeysInto joins the CFG totality guards (#1709)`

5. **Standing inventory** — new `src/Sharpy.Compiler.Tests/Infrastructure/DispatchSiteInventoryTests.cs`
   - Scan `src/Sharpy.Compiler/**/*.cs` for `switch (node)|switch (stmt)|switch (pattern)|switch (expr)`; key sites by `path :: enclosing method` (reuse Roslyn parsing from `SwitchArmScan`, not raw line regex, so line drift is harmless); roster maps each site → justification ∈ {guarded-by:<TestClass>, documented-by-design (`Parser/Ast/Types.cs:~18–25` family), walker-default-contract, refusal-net:<TestClass>}. The reconciliation of the ~40-site scan IS this roster; residue that fits no category gets an issue filed and an entry citing it (never a bare exemption).
   - Mutation: remove one roster row → red; positive control: every category non-empty; the guard's exemption is never its subject (the roster rows are data; the falsifiable arm is the scan-vs-roster `SetEquals`).
   - Commit: `test(infra): standing dispatch-site inventory — every hand-rolled AST switch carries a rostered justification (#1709)`

## Testing Strategy

- **New fixtures:** Phase 2's probe pair as permanent fixtures if not already covered — `nullable_display_spy0220.spy` + `.error` asserting `'int32 | None'` (churn direction) and its OptionalType twin asserting `'int32?'` (the no-churn positive control). Every negative expectation in the sweep keeps a passing positive-control sibling.
- **Edge cases beyond the repro lists:** #1711 — an `allowedCodes` caller path (null-`ErrorCodes` guard) though the observed hit passed `null`; #1714 — nested wrappers (`Optional[list[int]]` vs `list[int] | None`, `object?` at the CodeGen compare site); #1709 — per-overload deletions (the axis the merged scan cannot see), the `when`-guarded arms (`VariableDeclaration`, `StarPattern`) that a naive type-name scan could double-count.
- **Discriminating outputs:** the #1714 probe pair prints IDENTICAL messages with the bug present and DISTINCT messages after — cite both runs. A #1709 guard is only claimed after its recorded deletion-red; a #1711 claim cites the named-failure message text, not the absence of NREs.
- **Spec:** no spec section changes (the spec already spells nullable `T | None`; its Optional diagnostic examples keep `T?`); Phase 2 Task 3 executes the `tagged_unions_optional.md` :182–:184/:228 examples against HEAD as the no-churn check.
- **Blast radius & gates:** every phase-closing commit cites a whole-solution `dotnet-serialized test --filter "Category!=Benchmark"` run (background during read-only work; read `.claude/tmp/dotnet-serialized-*.log`, check binary freshness). Semantic-touching Phase 2 explicitly watches `Stdlib.Tests`, `Cli.Tests`, `FrontEndParityTests`, and the three GapDiscovery sweeps. Regen runs EARLY in Phase 2 (Task 2), not pre-push.
- **Attribution:** any red is attributed only after a control run at d45034f52 (worktree under `sharpy.worktrees/`); counts always `passed/failed/skipped @ sha (measured)`.

## Issues to Close

- **#1711** — PropertyCorpus NREs before naming its counterexample — closed by Phase 1, Tasks 1–3; **close criterion:** null-slot test red-under-mutation/green-restored recorded; 8 callers on `CompileAll` (grep count 0 for the old pattern); sibling inventory recorded; stress-probe frequencies (List vs CompileAll) in the commit body; whole-solution gate @ sha.
- **#1714** — NullableType/OptionalType display collision — closed by Phase 2, Tasks 1–3; **close criterion:** probe pair distinct @ sha (both outputs pasted); uniqueness guard red-before/green-after recorded; 25-file classification table recorded; hover case green; regen no-diff cited; whole-solution gate @ sha.
- **#1709** — vacuity class open at 3 dispatch sites — closed by Phase 3, Tasks 1–5; **close criterion:** four recorded arm-deletion mutations (2 AstVisitor overloads + EnumDef + AsPattern) red/green; the 5 declaration-name cells each carry a classification with probe evidence; `DispatchSiteInventoryTests` green with every site rostered and its own roster-row mutation recorded; whole-solution gate @ sha.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-08-31 @ d45034f52
**Plan file:** .claude/plans/plan-e31e76.md

### Corrections Made
- `DifferentialExecutionTests.cs:986` → `:988`; `MetamorphicCorpusInvarianceTests.cs:85` → `:87` (`threads: 1` line drift).
- `Sharpy.Compiler` removed from TestInfrastructure *consumers* — it is an `InternalsVisibleTo` grant (`Sharpy.Compiler.csproj:22`), not a consumption edge.
- `| None` fixture/`.error` twin count 10 → **11** (re-count excluding `bin/` crash copies; full list inserted).
- `CollectReferencedIdentifiers` `:219` → `:221`; `BuildStatement:177` → `:175`.
- `Types.cs:21–25` → `Parser/Ast/Types.cs:~18–25` (full path; contract text spans ~:15–:28) in both the documented-by-design bullet and Phase 3 Task 5.
- Dispatch-site scan "~40" → **41 (measured)**.
- **Phase 3 Task 1 (design-relevant):** filtering `FindMethods` "by ancestor `TypeDeclarationSyntax` identifier" cannot separate the two overload homes — `AstVisitor` (:9) and `AstVisitor<T>` (:561) share `Identifier.Text == "AstVisitor"`; the filter must use identifier **plus arity**. Task text corrected; self-tests unchanged in intent.

### Adequacy
- **Contract:** named — one per issue (fail-loud instrument; unique display spelling per SemanticType; roster-derives-from-dispatch), each traceable to verification-contract §1/§2.
- **Matrix:** larger than repro lists — 8 callers + full `Properties/` audit (#1711); 25-fixture sweep + pairwise ALL wrapper kinds + hover/allowlists/programmatic asserts (#1714); per-overload deletions + statement axis + pattern axis + 41-site standing inventory (#1709). Verifier-measured additions: the `.Sample(` audit space is 60 files / 24 with no thread-safety marker (added to Task 3); no sibling cells found that the plan fails to name.
- **Cure:** seam/harness in all three — shared `CompileAll` helper making the racy pattern unwritable; pairwise-uniqueness conformance guard; scanner-consuming totality guards + `DispatchSiteInventoryTests`. No N-patches anywhere.
- **Harness:** named per issue with the "none exists; this phase adds it" cases stated; #1709 extends the existing `SwitchArmScan` consumers and drains no allowlist (none exists for these sites — correct).
- **Owner rulings:** charter addendum 2026-08-31 (`remediation-round-2026-08-30-batching.md:176–204`) and R-F on #1712 both read and confirmed verbatim — R-F explicitly attributes the #1712 "disagreement" to this display collision, cites `SemanticType.cs:460`/`:560`, and fixes the direction (NullableType → `T | None`, OptionalType keeps `T?`, #882 type-level distinction). No ruled question is re-asked.

### Falsifiability
- `PropertyCorpusTests` (null-slot/null-codes): mutation named (disable null-guard → NRE red); positive control yes (failing sample names SOURCE; `EXCEPTION:` path has its own positive control); by-direction n/a (test-only).
- `CompileAll` + stress probe: mutation named (revert internals to plain `List` via cp-copy → count-loss/null red, frequency recorded); honesty clause present for a non-reproducing race; deterministic `iter`-count test is the permanent guard.
- `SemanticTypeDisplayUniquenessTests`: red-before-fix recorded (the colliding pair IS the positive control); mutation named (revert renderer via cp-copy); verbatim-spelling second fact pins both contract strings.
- Fixture sweep: OptionalType cells are the no-churn positive controls; the sweep greps message TEXT so bypass sites surface — not exemption-as-subject.
- `AstVisitorTotalityTests`: BOTH per-overload deletion mutations named with expected reds; cross-overload drift check included; existing method-table tests retained as complementary (their vacuity to case-deletion is the documented reason for the new guard).
- `ExecutionOrderAnalyzerTotalityTests` extension: `EnumDef`-arm deletion mutation named; open cells decided by `run` probes with by-direction reporting per §3.
- `CfgPatternBindingTotalityTests`: `AsPattern`-arm deletion mutation named; justified-no-arm roster (5 kinds) verified complete against the concrete `Pattern` universe.
- `DispatchSiteInventoryTests`: roster-row deletion mutation named; every justification category non-empty (positive control); exemption-not-subject argued explicitly in the plan (falsifiable arm is scan-vs-roster `SetEquals`).

### Verification evidence (probes run @ d45034f52)
- **#1714 probe pair reproduced live** (`emit diagnostics` via dotnet-serialized): both programs print byte-identical `error SPY0220 (3:5): Cannot assign type 'int32?' to variable of type 'int32'` — the collision is real and the discriminating pair is valid.
- `PropertyCorpus.cs:37` = `if (s.Passed)`; `CompileSample` body is whole-body try/catch returning `EXCEPTION:{type}` — can never return null. NRE ⇒ null slot in `samples`, as claimed.
- All 8 callers confirmed (27 plain-`List` accumulator sites); zero `threads:` anywhere under `Properties/Semantic/`; CsCheck 4.8.0 at `Sharpy.Compiler.Tests.csproj:23`; package XML doc confirms `threads` "default number logical CPUs".
- AstVisitor: void switch `:16` with **94** case arms; generic class at `:561` with **94** arms; rosters extracted and diffed — **identical**; 98 virtual Visit methods = 94 + the 4 abstract-base helpers; `TypeAnnotation` arm at `:328` (#1235). Method-table guards at `AstVisitorTests.cs:196/:220` read `GetMethods()` — vacuous to case-deletion, as claimed.
- `SwitchArmScan.FindMethods` (`:134`) filters by `Identifier.Text == methodName` only — merges overloads file-wide, confirming the scanner gap.
- `CollectDeclarationNames` (`switch (stmt)` at `:74`): exactly the 6 claimed arms; `DecoratedStatement` unwrap with #1024 comment at `:104–:106` in `DetectBasicIssues` only. `UnionDef`/`DelegateDef`/`EventDef` (`Statement.Future.cs`) and `TypeAlias` (`Statement.cs:792`) are concrete `Statement`s and are parsed (`Parser.cs:878` ff.) — the classification probes are executable.
- `CollectPatternBindingKeysInto` (`switch (pattern)` at `:919`): exactly the 11 claimed binding arms; concrete `Pattern` universe is exactly 16 = 11 + the 5 claimed no-arm kinds; `PropertyPatternField : Node` (`Pattern.cs:294`) — stays out of the universe, as claimed.
- All 13 `*TotalityTests` files exist incl. every one the plan names; `CfgStatementTotalityTests:119` scans `BuildStatement`; `ExecutionOrderAnalyzerTotalityTests` scans `CollectReferencedIdentifiers` only (SetEquals exemplar shape confirmed at `:144–:155`).
- 25 `?'` `.error` fixtures (measured); `nullable_value_payload_refused_1399.error` = `'int32?'` churn cell confirmed; `optional_argument_not_unwrapped_1397.error` contains `Optional[...]` control text; spec files all exist, `tagged_unions_optional.md:180–:184/:226–:230` are OptionalType examples that keep `T?`.
- `GetDisplayName() == ` predicate: exactly ONE in production (`RoslynEmitter.Expressions.cs:874`); zero `GetDisplayName` hits under `Project/` — no serializer round-trip of display names.
- `FrontEndParityTests` (`src/Sharpy.Lsp.Tests/Conformance/`) compares code multisets (`GroupBy` count dicts at `:489–:490`) — parity-neutral to message-text changes, as claimed. `HoverTests` has zero `| None` assertions; `str?` OptionalType assert near `:248`.
- Issues #1709/#1711/#1714 OPEN, titles match, zero comments; charter P1b addendum at `remediation-round-2026-08-30-batching.md:176–204`.

### Warnings
- **Arity discrimination** (corrected in Task 1, restated so the implementer doesn't miss it): a containing-type filter keyed on `Identifier.Text` alone returns BOTH overload sets for "AstVisitor" — the self-test "each return 94" would pass vacuously against a merged 94-arm union since the rosters are identical today. The self-test must also assert the two filtered scans see *different method declarations* (e.g., pattern-text shape `case X n:` vs `X n =>`), or the arity filter is unfalsifiable while the rosters coincide.
- **Fixture `bin/` crash litter**: `TestFixtures/bin/Debug/net10.0/.sharpy-crash/` holds hundreds of stale crash copies (observed during fixture greps). Any Phase 2 sweep grep must exclude `bin/` or counts will be wrong; the corrected 11-count above already does.
- The Phase 1 Task 3 pass-set list is indicative, not the inventory — the measured audit space (60 `.Sample(` files, 24 with no marker) was added to the task; classify per accumulation site, not per file.

### Missing Steps Added
- Phase 1 Task 3: audit-space measurement and per-site (not per-file) classification requirement (marked `[ADDED: missing step]`).
- No other gaps: sweeps, whole-solution gate, early regen, allowlist greps, and mutation hygiene (cp-restore, fixture `bin/` clearing) are all already specified in the plan.

## Implementation Checklist

- [x] P1-T1: Fail-loud assert + harness tests — LEAD — a3f38a513
- [x] P1-T2: Thread-safe CompileAll + migrate 8 callers — LEAD — a3f38a513
- [x] P1-T3: Sibling harness audit — LEAD — a3f38a513 (all 23 unmarked files SAFE)
- [x] P2-T1: Renderer + guard — LEAD — 615c3e5d7 (red-before: 2 failed, green-after: 3/0/0)
- [x] P2-T2: Early regen — LEAD — no diff @ a3f38a513 [NOTE from verify round: a3f38a513 PREDATES the renderer change; the covering evidence is the verify round's regen at f77a775d5 — no diff (measured)]
- [x] P2-T3: Consumer sweep — LEAD — 615c3e5d7 (3 churn cells, 23 controls, hover, probes) + f77a775d5 (programmatic asserts: InteropConformance, TypeResolverOptionalResult, ClrMemberFidelityMatrix — the sweep was NOT complete at 615c3e5d7)
- [x] P3-T1: Scanner extension — LEAD — 91367700f
- [x] P3-T2: AstVisitor guard — LEAD — 4c4e49ce3
- [x] P3-T3: CollectDeclarationNames guard + cell classification — LEAD — 4c4e49ce3
- [x] P3-T4: Pattern-collector guard — LEAD — 4c4e49ce3
- [x] P3-T5: Standing inventory — LEAD — 4edb4fdd2 (41 sites, 4 categories)

### Remediation round (/verify-implementation, 2026-08-31)

Scope `d45034f52..f77a775d5` audited by four refuting auditors; remediation commits:

- [x] R1: SharpySourceGenerator NullableType → 'T | None' (second #1714 formatter; wrote SOURCE meaning a different type) — 0d14e08cb (mutation: red 2/34 → green 36/0)
- [x] R2: TypeAnnotationHelper quotes nullable + result annotation modifiers (annotation display surface; SPY0320 quoted 'int' for 'int | None') + fixture pair nullable/optional_display_spy0220 (the Testing-Strategy promise) + exit-nullable discriminating fixture — 625d40fcd (mutation: red 3+1 → green 9/0)
- [x] R3: DispatchSiteInventoryTests → Roslyn parse per plan Task 5 spec (41→75 sites; expression form incl. AstVisitor<T>.Visit; exact-key both-direction roster; fossil rows drained: Types.cs comment-match, nonexistent IntegerConstantEvaluatorTotalityTests; pending-guard:#1716 ×14) — fcb8e3f18 (mutations: row-delete red, phantom-row red → green 2/0)
- [x] R4: SwitchArmScan.DispatchFormCounts + form-discriminating self-test (the verify-round warning) — fcb8e3f18 (mutation: filter neutralized → red 2/7 while roster tests stayed green; restored → green 9/0)
- [x] R5: DecoratedStatement reclassified DeclaresNameNotCollected with probe-backed masking reason — fcb8e3f18 (probe: decorated vs plain early-ref both print 1)
- [x] R6: standing absence guard for the racy accumulator + stress-probe frequencies (List: 10/10 loss, 660,933/1,000,000 deficit, 10/10 null slots; ConcurrentQueue: 0/10) — f838ef79b
- [x] Rule-12 ledger for the original guards (M1–M8, auditor-measured @ f77a775d5) recorded in fcb8e3f18's body
- [x] R7 (loop 2): roster citations become falsifiable — 19 emitter + 3 parser rows re-cited from EmitterCarrierOnlyConformanceTests/parser-tests (neither exercises dispatch) to FileBasedIntegrationTests; new fact GuardCitations_ResolveToRealTests_AndGuardedByTestsScanTheirSite makes an unbacked citation unwritable; SwitchArmScan CaseTypeNames overloads de-mirrored through shared CollectCaseTypeNames — 756e19028 (mutation: nonexistent-class arm + real-but-wrong-class arm → red 1/2, restored → green 3/0; consumer sweep 80/0/0)
- [x] PUSHED dev+mainline @ e523ceec3 2026-08-31 (12 commits rebased over automated bench commit fb164abbc; pushed tree differs from the verified tree 756e19028 only by benchmarks/cross-language/results/* data). Gate 23,704/0/4 @ 756e19028 (measured). #1709/#1711/#1714 closed with evidence comments.
- Issues filed: #1715 (inventory widening: typed-scrutinee census 164/127, LSP scope), #1716 (pending-guard debt, 14 sites), #1717 (pre-existing diamond-interface ICE, CS0738 family), #1718 (display-string-as-type-identity: 5 production sites; the "no refusal changes" claim refuted by execution — all measured directions favorable/neutral: SPY0908→SPY0323 ×2, SPY0204 drop on a still-refused program)

### Unchecked Claims
- **The #1711 gate observation** (`FString_WithNestedCalls_CompilesClean` NRE at `PropertyCorpus.cs:line 37`, 16121/1/4 @ d45034f52): the 3-slot `.claude/tmp/dotnet-serialized-*.log` rotation no longer holds that run — not reproducible from logs now. Sourced to issue #1711's own report (title matches the claim verbatim); the plan correctly treats reproduction as probabilistic (stress probe with an honesty clause), so nothing hinges on re-observing this exact hit.
- **CsCheck's parallel callback execution causing THIS NRE**: mechanism verified as plausible (threads default = logical CPUs per package XML; repo siblings guard against exactly this), but the causal link is the plan's own Phase 1 stress-probe deliverable — by design, not pre-verified.
- **"94 arms == every concrete Node kind"**: verified by consistency (94 arms + 98 = 94+4 Visit methods + the passing reflection guards asserting universe ⊆ methods), not by an independent reflection count; Phase 3 Task 2's guard construction computes the universe directly and would surface any discrepancy immediately.
