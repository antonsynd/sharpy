# Remediation round plan — 2026-08-18

Scope: **60 open issues** (all open issues excluding #1020/#1021, parked by owner). Every issue
was read in full — bodies and all comments — before batching. Includes #1553–#1556 (filed from
the 2026-08-18 Mojo/KGEN review) and #1557 (split out of #1518 by owner ruling). #1100 was
closed by owner ruling on its recorded negative result.

Batches are chosen so that fixing a batch **retires the defect class**, not just the member
cells. Each batch section names the class and its standard cure; the acceptance bullets are the
class-level guards, not per-issue spot checks.

---

## 1. Owner rulings — ALL 13 RESOLVED 2026-08-18

All rulings are posted as comments on the issues (do not re-ask; the issue comment is the
authoritative text). Summary ledger:

| # | Subject | Ruling |
|---|---|---|
| 1 | Untyped `None` (#1514/#1516/#1526) | Per-shape: `return None`→`return;`, `match None:` lowers typed, untyped `x = None` **refused** with annotate-steer. Batch 1 runs first; no sweep quarantine. |
| 2 | #1531 `array[T]`→`list[T]` | **Refuse**; seam matrix flips to expected-refusals with `list(...)` steer. Standing line: .NET-declared conversions (widening, §10.2.11, `op_Implicit`) are honored; compiler-invented conversions do not exist. |
| 3 | #1530 ambiguous-arity CLR results | Argument-driven unique-candidate selection types the result; still-ambiguous calls get a destination-vs-candidate-set refusal. Measured against the #1451 conservatism control. |
| 4 | #1527 (+closed #1489) type aliases | **Transparency** — symbol-keyed emission paths; #1489's import-alias refusal is superseded (comment posted there). |
| 5 | #1532 isinstance | isinstance's 2nd argument is a **type position**; `(A, B)` = `tuple[A, B]` (structural `ValueTuple` test, narrows). SPY0344 repurposed to refuse non-type expressions incl. `int or str`. Spec-first (Rule 7) + deviations entry (same-spelling-different-meaning class). |
| 6 | #1515 (+#1415) builtin exceptions | Bridge **allowlisted** to the Python surface after a `System.Exception` member audit; `.message`-only carve-out rejected. Close #1415+#1515 together after re-measuring `str(e)` at HEAD. |
| 7 | #1534 UnmappedClr | Distinct `UnmappedClrType` carrying the failed CLR `Type`; `IsUnmappedClrObject` deleted; `SemanticType`-switch sweep included. |
| 8 | #1543 cross-type hash | Deviation entry with the **measured** pair matrix; adoption structurally unavailable (CLR primitive hashing). |
| 9 | #1517 + seam policy | **Honest borders**: concrete CLR collection types keep honest identities; iterable positions still project; `IEnumerable<T>`→`list[T]` provenance mapping untouched this round (load-bearing for #1518). |
| 10 | #1542 yaml quoting | One shared PyYAML-rules predicate for both dump surfaces; re-baseline accepted. |
| 11 | #1518 split | Declaration-order constraint split out as **#1557**. |
| 12 | #1146 | Convert to permanent policy doc; verify #1139/#1141 closes at HEAD first. |
| 13 | Scope | Park #1099/#1101–#1103; **#1100 closed**; #1140(a) optional rider on Batch 9. |

## 2. Recurring meta-classes and their standard cures

| Meta-class | Instances this round | Standard cure |
|---|---|---|
| **SPY0908 as a net** (#1146 contract) | #1514/#1516/#1526, #1527, #1530, #1532, #1533, #1535, #1347's `map(bytes, xs)` cell | Semantic-time refusal or correct lowering; never document-and-close. The #1146 contract text becomes a permanent policy doc this round (Batch 13). |
| **Two parallel representations of one fact** | #1519/#1520/#1521 (emitter re-derives), #1525 (path-string agreement), #1542 (two quoting predicates), #1513 (two door validators), #1356 (two singletons), #1517/#1538 (provenance lost on one path) | Single authority; agreement sweep with drain-on-fix allowlist; provenance carried, never reconstructed. |
| **Qualified/bare spelling divergence** | #1446, #1445-residue, #1523, #1524, #1463, #1527 | One resolution walk; "a qualifier changes lookup scope, never meaning"; sweep cells per position × in/outside source set. |
| **Sibling-arm missing check** | #1536/#1537, #1512, #1528, #1539 | Move the check to the shared seam; conformance cell per route so a new route cannot skip it. |
| **Silent-degradation instruments** | #1544, #1522, #1511, #1541, #1556 | Instruments must fail loudly and verify their own selection; mutation-test every new guard. |
| **Serializer/config under-representation** (warm ≠ cold, build ≠ editor) | #1553, #1554, #1555, #1538 | Inverted-allowlist census over the full member/option surface; every member mirrored, rationale'd non-fact, or Dropped-with-issue. |

## 3. Batch structure and sequencing

Waves: **0 and 1 first** (instrument + gate-flake protection for everything after), with **2b**
alongside (independent files, user-visible correctness). Then 2/3/4/5 (compiler classes,
parallelizable across owners — but see the shared-tree contract in §5), then 6/7, then 8 (the
dependency chain), then 9/10, with 11 interleaved anywhere and 12/13 last.

| Batch | Theme | Issues | Size |
|---|---|---|---|
| 0 | Instruments | #1544 #1522 #1511 #1556 | 4 |
| 1 | Untyped-`None` family | #1514 #1516 #1526 | 3 |
| 2 | CLR-seam typing (honest borders) | #1530 #1531 #1533 #1534 #1517 | 5 |
| 2b | Cache & config fidelity | #1553 #1554 #1555 #1538 | 4 |
| 3 | Qualified/nested name resolution | #1446 #1445 #1523 #1524 #1528 #1527 #1463 #1347 | 8 |
| 4 | Call-target seam | #1537 #1536 #1525 | 3 |
| 5 | Emitter purity (Rule 2) + isinstance | #1519 #1520 #1521 #1535 #1532 | 5 |
| 6 | Core Python fidelity | #1415 #1515 #1543 | 3 |
| 7 | Serialization doors | #1487 #1513 #1542 | 3 |
| 8 | Genericity & laziness chain | #1460 → #1464 → #1518 → #1354, + #1557 | 5 |
| 9 | LSP integrity | #1541 #1540 #1539 #1454 #1360 (+#1140a optional) | 5–6 |
| 10 | Type identity & diagnostic text | #1356 #1512 | 2 |
| 11 | Quick closes | #1529 #1412 #1506 | 3 |
| 12 | Gated feature | #1428 | 1 |
| 13 | Policy doc | #1146 conversion | 1 |
| — | Parked | #1099 #1101 #1102 #1103 | 4 |

---

## Batch 0 — Instruments (do first; protects every other batch's gates)

Class: instruments that can report green without measuring.

- **#1544** — `regenerate_spy_tests.sh` swallows compiler errors; failed regen leaves the suite
  green against stale checked-in C#. Surface error diagnostics, fail before the success line,
  make `--check` assert the `.spy` still compiles. Class move: audit every regen script
  (`regenerate_spy_stdlib.sh`, `check_spy_tests_staleness.sh`, `check_spy_staleness.sh`) for the
  same swallow.
- **#1522** — property-test line corruption uses unseeded `new Random()`; printed CsCheck seeds
  don't reproduce. Draw the index from the CsCheck generator (`ModuleWithSeed` pattern). Class
  move: widen the standing `GetHashCode()` guard grep over `Properties/` to reject `new Random(`.
- **#1511** — `test-memory-harness` bypasses the dotnet-serialized lock. Route through the
  wrapper AND re-point RSS sampling at `$LOCK_DIR/child` (naive substitution silently measures
  the wrong process tree). Class move: grep `.claude/scripts/` for other raw `dotnet build|test`.
- **#1556** — `InteropConformanceHelperTests` selected by no CI step. Rename it off the
  `InteropConformance` substring (fast helper unit tests belong in the main lane); done —
  now `TypeArgumentSynthesisTests`. Class move:
  land the R12 CI-filter conformance guard — parse the workflow's `--filter` expressions,
  evaluate the vstest filter grammar against the reflected assembly, assert every test is
  selected by ≥1 step (rationale-annotated exemptions only).

Acceptance: a deliberately broken `.spy` test module turns the regen script AND `--check` red;
a property failure replays from its printed seed; the harness measures a real dotnet PID under
the lock; the CI-filter guard fails when a test class is orphaned (mutation-test it: re-orphan
the class, watch it fail).

## Batch 1 — Untyped-`None` family (ruling 1)

Class: a bare `None` with no contextual type reaches the emitter untyped. Fixing this retires
the ~1-in-3 full-suite gate flake (see memory: ILCompiles gate non-determinism) — every later
batch's gates depend on it.

- **#1514** — `return None` in a void-returning function → lower to bare `return;`.
- **#1526** — `match None:` → lower the scrutinee over a typed null.
- **#1516** — untyped `x = None` → semantic-time refusal: "cannot infer a type for a local
  initialized to None; annotate it" (new SPY02xx + DiagnosticExplanations entry, Rule 10).

Acceptance: the sibling matrix from #1514 (explicit `-> None` + `return None`, lambda-`None`
against void delegate, generator/`async def`, nested def) all compile-or-refuse deliberately;
six consecutive full-suite runs with fresh seeds show no `None`-family ILCompiles failure.

## Batch 2 — CLR-seam typing under honest borders (rulings 2, 3, 7, 9)

Class: the seam either lies about a type's identity or stays silent and leaks CS errors.
Policy home: #1517's ruling comment (honest borders — concrete types keep honest identities,
iterable positions project, `IEnumerable<T>` mapping untouched this round).

- **#1517** — `ClrTypeBridge` stops spelling `HashSet<T>`/`Dictionary<K,V>`/SCG `List<T>` as
  `set`/`dict`/`list`; honest module-qualified CLR names; `x: set[int] = s.to_hash_set()` becomes
  a refusal; five path-agreement allowlist cells drain as correct refusals. **Sweep the corpus
  for assimilation-dependent annotations before landing** (blast-radius measurement).
- **#1531** — type array-returning static calls honestly (delete the #1451 `TODO(#1531)`
  exclusion); refuse `array[T]`→`list[T]` with a `list(...)` steer; flip
  `interop/clr_sequence_seam_matrix` cells to expected-refusals.
- **#1530** — unique-candidate-by-argument result typing + destination-vs-candidate-set refusal
  for still-ambiguous calls; conservatism control fixture stays green.
- **#1533** — imported-CLR-generic absence proof: enumerate all member-resolution routes, teach
  the proof structurally, then widen #1344's gate ("teach the proof, then move the gate").
- **#1534** — add `UnmappedClrType` carrying the failed CLR `Type`; delete `IsUnmappedClrObject`;
  sweep every hand-rolled `SemanticType` switch for the new arm; serializer channel in Batch 2b's
  vocabulary.

Acceptance: path-agreement and qualified/seam allowlists drain; no value holds two identities
(one conformance cell per formerly-assimilated concrete type × {annotation, operator, argument}
positions); `b: bool = Math.floor(1.5)` draws SPY0220; interop GapDiscovery sweep green (it is a
separate CI step — do not rely on FBIT; see memory).

## Batch 2b — Cache & config fidelity (runs parallel to Batches 1–3; lives in `Project/`)

Class: the cache/config plumbing silently under-represents compiler state, so warm ≠ cold or
build ≠ editor. Cure: census-guarded totality.

- **#1553** — serialize per-file diagnostics into `FileCacheEntry` and replay for skipped units
  (warm ≡ cold; schema bump). The alternative (document the divergence) is rejected.
- **#1554** — `ForProject` unions `config.Features` (mirror the LSP factory; CLI flags layer on
  top); with/without-override test. Class move: a census asserting every semantics-affecting
  `ProjectConfig` member reaches `CompilerOptions` or carries a rationale exemption.
- **#1555** — extend `SymbolFactMirrorConformanceTests` into `CodeGenInfo`: every member
  mirrored / rationale'd non-fact / Dropped-with-issue; per-member reachability determination for
  the six missing; extend serializer totality to all `SemanticType` subclasses in the same change
  (this also permanently guards #1538's shape and #1534's new case).
- **#1538** — `ClrOriginTypeName`-style provenance in the builtin channel (the `GenericType`
  precedent); CLR-backed specimen added to the totality test.

Acceptance: cold build == warm rebuild on diagnostics (measured over the `.warning` fixture
corpus scaffolded incrementally); a `.spyproj`-declared feature compiles gated code in
`sharpyc project`; the CodeGenInfo census fails on an unmirrored member (mutation-test: comment
out one encoder arm).

## Batch 3 — Qualified/nested name resolution (ruling 4)

Class: "a module qualifier changes lookup scope, never meaning"; plus name-keyed emission paths.

- **#1446** — re-land the dotted-name normalization **gated on the resolved symbol not being
  CLR-backed** (the `0565545c8` revert lesson — CLR-discovered module types keep qualified
  names); fix the cross-module path (`ConvertTypeAnnotationToSemanticType`) incl. the base-chain
  question the SPY0399 cell exposed; **verify with the interop GapDiscovery sweep, not just FBIT**.
- **#1445** — re-attribute the 3 residual allowlist cells to #1446's cross-module half; close.
- **#1523** — `WalkToOwningModule` hands off into `TypeSymbol.NestedTypes` when an intermediate
  segment is a type; nested-type cell added to the sweep.
- **#1524** — value-pattern chains resolve like annotations (consume module segments first);
  value-pattern cell added to the sweep.
- **#1528** — fix nested-declaration resolution upstream in `ResolveClassInheritance` (+ struct/
  interface/circularity arms) via enclosing `NestedTypes`; delete `PropertyValidator`'s guard.
- **#1527** — alias transparency: `TypeResolver` primitive switch and `TypeSyntaxMapper` become
  symbol-keyed; both `type` alias and import alias (#1489, superseded) work in call position.
- **#1463** — (ruled earlier) route `builtins.<primitive>` through the constructor-reference
  tiers via `PrimitiveCatalog`; refuse `builtins.None` with the literal-not-member message; drain
  the 9 `KnownValueDisagreements` entries.
- **#1347** — (ruled earlier) helper-class route: `bytes` static members get their own class
  mirroring `IntParse`; then the `Builtins.Bytes` overloads + family grant; **re-measure**
  SPY0336 after, don't predict. Fallback if stalled: SPY0342 in argument positions WITH the
  exception-type control cells (#1170 hazard), which also serves #1490.

Acceptance: qualified-bare allowlist empty; the sweep has cells for every position (annotation,
class pattern, value pattern, value, call, constraint, except, isinstance target) × in/outside
source set; `map(bytes, xs)` no longer leaks CS0119 behind a clean emit.

## Batch 4 — Call-target seam

Class: decorator facts silently skipped because a call-resolution route bypasses the recording
seam.

- **#1537** — route single-instance-method resolution through `ValidateFunctionSymbolCall`
  (measure the default-param/delegate blast radius first).
- **#1536** — run `CheckDeprecatedUsage` on the resolved `__init__` at the resolution point;
  answer the `@must_use`-on-`__init__` sibling explicitly (same treatment or documented
  not-applicable).
- **#1525** — shadow decision by symbol identity / recorded import binding; null path becomes an
  assertion, not a silent skip.

Class guard (the actual deliverable): a conformance test enumerating every call-resolution route
(free fn, overloaded method, single method, ctor single/overloaded, lambda/FunctionType,
imported, qualified) asserting each records a call target or is exempt with rationale.

## Batch 5 — Emitter purity (Rule 2) + the isinstance language change (ruling 5)

Class: semantic decisions taken emitter-side. Guard already exists
(`EmitterCarrierOnlyConformanceTests` ratchet); this batch is the drain. #1519–#1521 share the
`CalleeRouting`/`MergeFrom` vocabulary — one owner does all three. **Any new node-keyed
`SemanticInfo` dictionary must join `SemanticInfo.MergeFrom`** (Rule 2).

- **#1519** — calls read a materialized `FunctionSymbol` fact; hierarchy walks read
  `MaterializeInheritance` output (7 sites).
- **#1520** — functools.partial classified in the type checker, materialized into `CalleeRouting`.
- **#1521** — `SynthesisAnalyzer` runs in semantic analysis; synthesized interfaces frozen onto
  `CodeGenInfo`; SPY1001 emitted there.
- **#1535** — run `ExecuteGeneratorPipeline` before the `MaterializeTypeInfo` freeze; add the
  missing non-empty-generator end-to-end test.
- **#1532** — **spec first** (Rule 7): rewrite `builtin_functions.md:121–140` + deviations entry,
  then: isinstance's argument parses as a type position; `(A, B)` = `tuple[A, B]` structural test
  (lowers to a `ValueTuple` check; narrows); SPY0344 repurposed for non-type expressions with the
  two-steer message; Xunit tuple special case deleted; ported corpus swept (incl. the
  `isinstance(x, (int))` single-element cell); test-class emission gated on `TargetsTestHost`.

Acceptance: `EmitterCarrierOnly` ratchet entries for 1519/1520/1521 drain (stale entries fail);
a generator emitting a new top-level declaration compiles and runs; the isinstance fixture set
(structural + narrow, impossible-test refusal, SPY0344-on-`or`, `(int)` cell, `.error` twins) is
green and every spec example is executed against HEAD.

## Batch 6 — Core Python fidelity (rulings 6, 8)

- **#1415** — FIRST re-measure `str(e)` at HEAD (owner comments contradict each other); then
  route `str(exception)`/`__str__` through the same `Builtins.Str` path as the f-string half.
- **#1515** — audit `System.Exception`'s reachable snake_case surface on builtin exceptions;
  flip the bridge to a Python-surface allowlist; steers + deviations entries; user-declared
  `message: str` unaffected. Close #1415+#1515 together.
- **#1543** — deviations entry with the measured cross-type hash pair matrix; no code change to
  hashing.

## Batch 7 — Serialization doors (ruling 10; #1487 ruled earlier)

- **#1487** — implement the ruled both-doors-refuse for lone surrogates (`JSONDecodeError` +
  position; deviations entry); drain the three exempted agreement-corpus rows.
- **#1513** — one recursive traversal in `TypedLoadContract` over an abstract mapping view
  (each door supplies `JsonElement` / YamlDotNet mapping); dotted-path errors; nested rows added
  to the agreement corpus.
- **#1542** — the shared PyYAML `analyze_scalar` predicate consulted by both dump surfaces;
  delete `NeedsQuoting`'s hand rule; force per-scalar `ScalarStyle`; re-baseline with PyYAML as
  oracle; drain the pinned residue test.

## Batch 8 — Genericity & laziness chain (strictly ordered; rulings 3-adjacent, 11)

`#1460 → #1464 → #1518 → #1354`, with **#1557** as an independent rider (may land any time).

- **#1460** — (ruled earlier) `VariableSymbol.ConstantValue` folded at declaration
  (const-eligible integer cases); §10.2.11 conversion site consults it; `static readonly`-backed
  const stays refused.
- **#1464** — add the §12.6.4.5 better-conversion row (exact beats constant conversion); flip
  `allowConstantConversion` at the ranking site with the two-small-width-candidates ambiguity
  cells measured first; update `overload_resolution.md`.
- **#1518** — teach `ProjectedArgumentType` (iterable-position projection) about open formals;
  the name-comparison relaxation is a measured dead end — do not re-attempt it.
- **#1354** — (ruled earlier: flip everything to `Iterator[T]`) flip the ~22 generic producers
  (`TODO(#1518)` sites in `spy/itertools.spy`), regenerate spy stdlib, differential-oracle cells,
  re-enable acceptance; close when every producer is lazy.
- **#1557** — forward reference to a generator typed from its annotation; both declaration orders
  pinned; determine whether it shares machinery with #1518's projection (on #1557's number).

## Batch 9 — LSP integrity (+optional #1140a, ruling 13)

- **#1541** — `ValidateReferenceSet`-style startup assertion for the stdlib reference; sweep the
  LSP for other silent `File.Exists`-guarded capability drops.
- **#1540** — fix the field-extraction arm so `math.pi`/`math.e` reach `Exports`; land the class
  guard: every member the type checker resolves through the dot appears in `Exports`.
- **#1539** — lift `SharpyRenameHandler.ResolveSymbol` into a shared declaration-cursor resolver
  used by references/highlight/rename.
- **#1454** — convert the remaining `Name.Length`+backtick consumers (HoverService,
  InlineProvider, ConvertFormsProvider, CallHierarchy) to the recorded extent; the
  `ConvertFormsProvider:365` cell needs its red-first H1/H2/H3 measurement before conversion.
  Close condition: no LSP consumer reconstructs a name extent.
- **#1360** — parser error-recovery to a partial AST so mid-edit files answer member completion;
  close when a mid-edit `math.` offers members including constants (after #1540).
- **#1140 (optional rider)** — cached per-call-invariant analysis context (ModuleRegistry +
  BuiltinRegistry/SymbolTable) keyed on reference list + file identity; re-measure after.

## Batch 10 — Type identity & diagnostic text (one re-baseline wave)

- **#1356** — (ruled 2026-08-15: precise-name canonical) collapse to one singleton per CLR type,
  `float64`/`int32` canonical, integer family folded in; ~70-fixture re-baseline; land the
  `OneSingletonPerClrType` guard; gate on a green 5-project measure.
- **#1512** — both event arms read one delegate-type authority; SPY0373 renders via
  `GetDisplayName()`; grep all diagnostic construction for raw `SemanticType` interpolation and
  fix every hit; consider a conformance test banning it.

## Batch 11 — Quick closes (interleave anywhere)

- **#1529** — already fixed with fixture; verify `indexer_setitem_value_param_1529` at HEAD, close.
- **#1412** — ruled park-and-close; land the pointer comment at `FillTypeArgumentsFromSubject`
  naming the trigger condition, close.
- **#1506** — write the iterator-returning spec section (three content bullets specified in the
  issue); every example executed against HEAD (`builtin_iterator_reprs_1354.spy` is ready
  evidence).

## Batch 12 — Gated feature (#1428, ruled earlier)

Experimental gate for alias-mutating augmented assignment on collections: register in
`FeatureFlags.KnownFeatures`, gate via `FeatureGateChecker` (ungated → SPY0331), shared
classifier with #1394's transition hint (one-door), gated/ungated fixture twins. No TFM
semantics fork; the C#14-vs-mutation-call mechanism question is resolved during the gated
period. Schedule last; deferrable past the round.

## Batch 13 — #1146 conversion (ruling 12)

Verify #1139/#1141 closes by running their repros at HEAD; write the SPY0908 policy doc under
`docs/design/` (contract, review rule, enforcement-seam inventory, current allowlist census as
the starting ratchet); reference it from copilot-instructions; close #1146 pointing at the doc.

## Parked this round (ruling 13)

#1099 (incremental frontend), #1101 (pull-based cache), #1102/#1103 (E3 fusion/escape-free
positions) — roadmap, each needs a design phase, none blocks the round. #1100 closed 2026-08-18
on its recorded negative result; future allocation work is profile-first under #1099.

---

## 5. Round mechanics (process guardrails, from this repo's own memory)

- **Serialized dotnet** via `.claude/scripts/dotnet-serialized` only; `dangerouslyDisableSandbox`
  required (sandboxed callers exit 125). No blanket pkill. Read `.claude/tmp/*.log` instead of
  re-running suites; `-latest.log` may rotate to a peer's run.
- **Until Batch 1 lands**, the full suite is ~1-in-3 red at baseline from the `None` family —
  triage ILCompiles failures against #1514/#1516/#1526 before attributing them to a batch.
- **Shared working tree**: agent prompts enumerate the git prohibitions (no
  checkout/restore/clean/stash/reset/rm), REPORT status rather than "must be clean", explicit
  per-file pathspecs when staging, commit lead work in small slices.
- **Regenerate early**: spy-stdlib C#, spy-test C#, stdlib docs — read the regen diff as
  findings, not noise. `/push` runs the staleness gates.
- **Spec-first for language changes** (Batch 5's isinstance, Batch 12's gate): spec text with
  executed examples lands with or before the implementation (Rule 7 + docs-need-execution-
  evidence).
- **Interop sweep is a separate CI step** — Batch 2/3 changes that touch CLR-type naming must be
  verified against the GapDiscovery interop sweep, not just FileBasedIntegration (the exact hole
  the #1446 revert came through).
- **Mutation-test every new guard** (Batch 0's regen/CI-filter guards, Batch 2b's censuses,
  Batch 4's route enumeration): break the guarded thing, watch the guard fail.
- **Close-out discipline**: a closed issue is not a finished issue — closes cite execution
  evidence at a named sha; every fix that drains an allowlist entry deletes it in the same
  commit; batch completion is `/verify-implementation`-audited before its issues close.
- **Per-batch implementation plans** are created with `/create-plan` at implementation time,
  referencing this document and the ruling comments on the issues.
