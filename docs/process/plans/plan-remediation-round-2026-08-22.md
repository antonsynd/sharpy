# Remediation round plan — 2026-08-22

Scope: **45 open issues** (all open issues excluding #1020/#1021, parked by owner, and the
ruling-13 roadmap parks #1099/#1101/#1102/#1103 carried over from the 2026-08-18 round). Every
issue was read in full — bodies and all comments — before batching. Two issues were filed during
the batching session itself: **#1618** (materialization audit, joins Batch 0) and **#1619**
(reified isinstance — documented direction, not scheduled).

Batches are chosen so that fixing a batch **retires the defect class**, not just the member
cells. Each batch section names the class and its standard cure; the acceptance bullets are
class-level guards, not per-issue spot checks.

Facts established during batching (verify at implementation time, don't re-derive):

- **#1603's headline fix already landed** (4056c7bfe — ternary is truth-tested). Remaining work
  is close-out with execution evidence; its accepted-type set inherits ruling R1.
- **#1606 is half-done** (36ca27323 fixed the two ProtocolTests sites). Remaining scope: the 8
  sites at `MultiFileTests.cs:291,340,377,438,455,545` and `LspEditSessionTraceHarness.cs:175,197`
  (line numbers at 57cca6b40).
- **#1591, #1608, #1613 carry full owner rulings/diagnoses in issue comments** — ready to
  implement, nothing to re-ask.
- **#1615's seam is pre-wired**: gated/ungated emit identically for the isinstance-narrowed shape
  (equivalence pin at b08ed6651), so one fix covers both modes.

---

## 1. Owner rulings — ALL 13 RESOLVED 2026-08-22

All rulings are posted as comments on the issues (do not re-ask; the issue comment is the
authoritative text). Summary ledger:

| # | Subject | Ruling |
|---|---|---|
| R1 | #1558 truthiness (also #1570, #1603) | Unified at EVERY truth position for every operand form. Set = **"has a falsy case"**: bool, numbers, str/bytes, collections via `__len__`/ISized, None/Optional, user `__bool__`/`__len__`; vacuous-truthy types (plain objects, functions) refused. One predicate + one materialized lowering; `and`/`or` return `bool` per spec. Plus an **opt-in strictness lint** (new SPY045x, off by default) on any non-bool coercion. |
| R2 | #1559 bare declarations | **Blessed** (declare-then-assign is the block-scoping idiom); spec updated; **definite-assignment analysis as a hard error** (rides the #1042 CFG engine). |
| R3 | #1563 lambda defaults | Defaults belong to the FUNCTION, not the function type: delegate calls require full arity (clean diagnostic + steer); statically-pinned callees keep defaults. Plus a **warning at the conversion site** where a defaulted function/lambda becomes a function-typed value. |
| R4 | #1569 ambiguous CLR call | Honest borders: still-ambiguous-with-compatible-destination **refuses** with candidate set + argument-disambiguation steer. Destination-driven selection rejected. |
| R5 | #1573 lossy adjudication | Round-trip the unique survivor through `TryGetClrType` + real .NET assignability before committing; lossy mappings never adjudicate alone. |
| R6 | #1572 explicit interface members | **Materialize the interface-cast lowering** (node-keyed fact, joins `MergeFrom`). Rider: #1618 filed — census of unmaterialized lowering facts. |
| R7 | #1568 warm/cold identity | `GenericDefinition` = per-analysis state, never survives the cache; consumers fall back via `ClrOriginTypeName → ResolveOriginDefinition`. **Warm/cold differential harness is a required deliverable** (2nd occurrence of the class, #1553). |
| R8 | #1615 narrowed augassign | v1 = semantic-time **refusal** with steer (rebind through a typed local); both modes identical; mutation semantics decided at #1614 graduation. |
| R9 | #1597 json lone surrogates | **Both doors refuse** raw lone surrogates, one shared message (#1487 pattern). |
| R10 | #1598 yaml escapes | Match PyYAML **if** the YamlDotNet emitter hook is cheap; else pin + document deviation. Implementer measures cost first. |
| R11 | #1581 self-contained restore | Accept + document (restore needs the SDK); revisit `NuGet.Protocol` on real demand. |
| R12 | #1592 generated inheritance | **Parked** (label added); revisit when a real generator needs inheritance. |
| R13 | bare-generic isinstance | `isinstance(x, list)` **stays** (only dispatch tool over `object`; read surface is sound). Reified `isinstance(x, list[T])` approved as documented direction — **#1619**, not scheduled. |

## 2. Recurring meta-classes and their standard cures

| Meta-class | Instances this round | Standard cure |
|---|---|---|
| **SPY0908 as a net** (spy0908-policy.md) | #1558 #1559 #1560 #1561 #1562 #1563 #1565 #1569 #1570 #1572 #1573 #1587 #1588 #1608 #1615 #1617 | Semantic-time refusal or materialized lowering; never document-and-close. #1618 audits the remaining improvisation sites. |
| **Silent wrong behavior** (worse than ICE) | #1571 (LINQ append no-op), #1562 (capture pattern matches everything) | Fix first; add a conformance sweep so the sibling cells can't recur (mapping-table × Enumerable surface; pattern-position resolution matrix). |
| **Qualified/bare & alias divergence** | #1585 #1586 #1587 #1588 #1590 #1612 | ONE shared qualified-resolution helper + ONE alias-deref point re-entering the bare path; sweep allowlist drains on fix. |
| **Position/route missing a shared check** | #1558/#1570/#1603 (truthiness), #1593/#1594 (deprecation), #1591 (kwargs), #1599 (pre-pass) | Move the check to the shared seam; position × type (or route-table) conformance matrix so a new position cannot skip it. |
| **Warm ≠ cold divergence** | #1568 (3rd member: #1553, #1554-family) | Per-analysis state never trusted post-restore; warm/cold differential harness (required, R7). |
| **Instruments that pass vacuously** | #1606 (drain conflation), #1580 (invisible warning) | Fail loudly; shared helper makes the wrong pattern unwritable; mutation-test the guard. |

## 3. Batch structure and sequencing

Waves — everything is implementable now (no ruling gates remain):

- **Wave 1 (CI stability + silent wrongness, do first):** #1571 and #1562 (silent wrong
  behavior), Batch 2's #1608 and Batch 9's #1617 (ILCompiles fresh seeds re-roll these classes
  until they land), #1603 close-out, #1613, #1591. Batch 0's audit (#1618) runs alongside —
  read-only, informs later batches.
- **Wave 2 (large compiler classes, parallelizable across owners — shared-tree contract in §5):**
  Batch 1 (truthiness), Batch 3 (qualified/bare), Batch 4a + 4b remainder, Batch 8 (scoping/DA).
- **Wave 3:** Batches 5, 6, 7, 10, and the Batch 9 remainder.
- **Wave 4:** Batches 12, 13; Batch 11 interleaves anywhere; Batch 2's #1610/#1609 tail.

| Batch | Theme | Issues | Size |
|---|---|---|---|
| 0 | Materialization audit | #1618 | 1 |
| 1 | Truthiness surface | #1558 #1570 (+#1603 close-out) | 3 |
| 2 | Slice/subscript validation | #1608 → #1610 → #1609 | 3 |
| 3 | Qualified/bare + alias transparency | #1585 #1586 #1587 #1588 #1590 #1612 | 6 |
| 4a | CLR overload selection & inference | #1569 #1573 #1589 #1600 | 4 |
| 4b | CLR member binding | #1571 #1572 #1613 #1568 | 4 |
| 5 | Exception semantics | #1596 #1595 | 2 |
| 6 | Deprecation seam | #1593 #1594 | 2 |
| 7 | Declaration-order completeness | #1599 #1601 | 2 |
| 8 | Block scoping & definite assignment | #1559 #1560 | 2 |
| 9 | Call-shape & expression statements | #1561 #1563 #1565 #1617 #1562 #1591 | 6 |
| 10 | Narrowed-receiver mutation | #1615 | 1 |
| 11 | Infra/tooling quickies | #1566 #1567 #1606 | 3 |
| 12 | Distribution/NuGet | #1580 #1581 | 2 |
| 13 | Stdlib fidelity | #1597 #1598 | 2 |
| — | Not scheduled | #1611 (documented non-feature) #1614 (graduation tracker; #1615 is its only near-term dependency) #1619 (documented direction) #1592 (parked) | 4 |

---

## Batch 0 — Materialization audit (#1618)

Class: lowering facts codegen needs but semantic never materializes (the #1572 class,
generalized). Read-only sweep of CodeGen improvisation sites + the SPY0908 backlog as symptoms.
Deliverable: route-table census — every site materialized or exempt-with-rationale.
**Acceptance:** the census exists as a conformance artifact; each (c)-classified site has an
issue; findings feed later batches rather than being patched inline.

## Batch 1 — Truthiness surface (R1)

Class: one predicate + one lowering, consulted at every truth position for every operand form.
Today: `IsTruthTestable` = {bool, str, Unknown, user `__bool__`} consulted at some positions;
`and`/`or`/`not` operands unchecked (#1558); literal operands miss the lowering (#1570).

- **#1558** — enforce the R1 set on `and`/`or`/`not` operands; emit the truthiness coercion;
  `and`/`or` return `bool` per spec.
- **#1570** — literals route through the same unified lowering; float joins the set.
- **#1603** (rider) — verify 4056c7bfe at HEAD, close with execution evidence; widen its
  accepted set per R1.
- New opt-in strictness lint (SPY045x, off by default, flag-enabled): warns on any implicit
  non-bool coercion. `DiagnosticExplanations` entry (Rule 10); spec update with executed
  examples (Rule 6/7 — verify each cell against `python3` first, deviations documented).

**Acceptance:** position × type conformance matrix (if/elif/while/assert/ternary/comprehension
filter/match guard/and/or/not × every set member + a refused vacuous type + literal and variable
operand forms) — all green with no per-position special cases; lint mutation-tested (enable
flag, assert it fires; disable, assert silent).

## Batch 2 — Slice/subscript validation (#1608 ruled in-comment → #1610 → #1609)

Class: subscript surface accepts what codegen cannot lower. Implement exactly the owner-ruled
design in the #1608 comment (receiver classification into a node-keyed lowering fact joining
`MergeFrom`; unclassified refuses with reused SPY0320; sliceable v1 = list/str/bytes/array +
ndarray multi-axis; bounds assignable to `int?`; index positions plain `int`; tuple refused with
steer to #1609). `CheckMultiAxisAccess` dimensions included. Then #1610 (`slice` builtin type,
spec-first) enables the user `__getitem__(slice)` arm; #1609 (constant-bound tuple slices →
narrower TupleType) is the approved tail.

**Acceptance:** the #1608 probes all refuse cleanly; ILCompiles battery (fresh seeds) no longer
reds on the class; receiver × bound-type matrix fixtures; `DiagnosticExplanations` updated.

## Batch 3 — Qualified/bare + alias transparency (#1585 #1586 #1587 #1588 #1590 #1612)

Class: each syntactic position independently learned (or didn't) to consume module qualifiers
and dereference aliases. Cure: **stop teaching arms one at a time** — one shared
qualified-resolution helper (consume module segments → resolve symbol → re-enter the identical
bare-spelling path) and one alias-deref point (TypeAliasSymbol / BuiltinAliasOf → target) that
runs before any classification (constructor-reference tiers, callee resolution, pattern
resolution, value member-access NestedTypes walk). #1612 shows the divergence can point either
way (bare broken there), so the fix must be spelling-symmetric.

**Acceptance:** all six QualifiedBare sweep allowlist cells drain in the same commits (stale
entries fail the suite); the `__synth_T0` spelling never reaches a user message; no new
per-position arm added — greps confirm call sites route through the shared helper.

## Batch 4a — CLR overload selection & inference (#1569 R4, #1573 R5, #1589, #1600)

Class: adjudication must neither manufacture certainty (R5 round-trip verification; R4 refusal
of the still-ambiguous cell with candidate steer) nor lose certainty inference could grant
(#1589: defer target-typed constructor-overload selection until generic inference substitutes;
#1600: `ArgMatchesGenericShape` compares through constraints). #1600 unblocks the last #1354
itertools flip — verify groupby flips in the same batch.

**Acceptance:** the three #1530 cells each have a fixture (unique-select types, no-candidate
refuses, still-ambiguous refuses with steer); enum/op_Implicit false-uniqueness probes refuse;
`map(bytes, xs)` compiles and runs (the plan-Phase-7 goal line); constrained-vs-unconstrained
twin fixtures for every shape the provenance proof accepts.

## Batch 4b — CLR member binding (#1571, #1572 R6, #1613, #1568 R7)

- **#1571** — collection method mapping consulted for imported CLR receivers before extension
  binding (`append`→`Add`); class rule: a Python collection verb either maps or refuses, never
  binds a discarded pure extension. Sweep `GetCollectionMethodMapping`'s table against
  `Enumerable`'s surface as a conformance test.
- **#1572** — materialize the interface-cast lowering per R6 (node-keyed; joins `MergeFrom`).
- **#1613** — implement the recorded diagnosis: arity-aware CLR name-group resolution (probe
  `` Name`N `` by type-arg count, populate `TypeParameters` so the existing substitution engages)
  in both the bare-name and import channels; cell matrix across EventHandler/Action/Func.
- **#1568** — R7: consumers fall back via `ClrOriginTypeName`; warm/cold differential harness
  (required deliverable) with the cross-file cold-refuses→warm-must-refuse cell first.

**Acceptance:** `xs.append(7); len(xs)` on an imported `List[int]` prints 2 (executed);
`is_fixed_size` reads through the cast; every #1613 table cell green; warm/cold harness green
and mutation-tested (break the fallback, watch it fail).

## Batch 5 — Exception semantics (#1596, #1595)

- **#1596** — populate `BaseType` at discovery for CLR exception symbols (one authority), then
  **delete** `FindCommonExceptionBase`'s private CLR-chain workaround in the same change — a fix
  in one consumer plus a workaround in another is how this class recurs.
- **#1595** — both fixes: Core returns a Sharpy immutable collection (public-API convention;
  Python's `.exceptions` is a tuple) AND the bridge maps `ReadOnlyCollection<T>`/
  `IReadOnlyList<T>`. Class guard: API-surface conformance test over `Sharpy.Core` public
  signatures banning raw .NET collections (the CLAUDE.md anti-pattern, currently unenforced).

**Acceptance:** raise/except/isinstance matrix over subclasses of each builtin exception base
(executed fixtures); `len(eg.exceptions)` prints 2 (the line 63d2528cd silently dropped —
restore it); API-surface test mutation-tested.

## Batch 6 — Deprecation seam (#1593, #1594)

Class: `@deprecated` blind spots at exempt routes. #1593: check at the function-REFERENCE
channel (C# CS0618 / PEP 702 precedent) — covers all delegate flows. #1594: check inside
`ValidateInitAndEventCalls` where the delegation target is already resolved — no lifting of the
#1536/#1537 recording exemptions.

**Acceptance:** `CallTargetSeamConformanceTests` route table has zero deprecation-blind rows —
the two exempt-with-citation rows become checked rows with fixtures (reference site warns;
`super().__init__`/`self.__init__` warn).

## Batch 7 — Declaration-order completeness (#1599, #1601)

Class: facts computed at visit time that call sites need at pre-pass time.

- **#1599** — transplant the #1557 pre-pass pattern: `Task<T>` wrap in
  `ResolveFunctionSignatureInto` (double-wrap guarded against the generator wrap); audit
  class-method siblings (named suspect cells).
- **#1601** — thread the resolver into the declaration-time const fold with a cycle guard;
  adopt C# §10.2.11 semantics wholesale (order-irrelevant, cycles a clean diagnostic); same
  resolver in the warm/import path (`ModuleLoader`).

**Acceptance:** both-orders differential tests across function kinds (plain/async/generator/
async-generator × free/method/classmethod) — exactly one wrap each; chained-const fixture
(`BETA: int = ALPHA` then `uint8` site) compiles and prints; cycle fixture refuses cleanly;
warm and cold agree on both.

## Batch 8 — Block scoping & definite assignment (#1559 R2, #1560)

- **#1559** — bless bare declarations per R2; spec pages updated (`variable_declaration.md`,
  `statements.md` — they currently contradict the implementation and `structs.md`); implement
  definite-assignment as a hard error riding the #1042 CFG engine; `DiagnosticExplanations`
  entry.
- **#1560** — fix emitter scope tracking for sibling `try` re-declaration.

**Acceptance:** block-kind matrix (for/while/if/else/try/except/finally/with/match-arm) ×
(redeclare-in-sibling, read-after-block, use-before-assign) — the class is "some block kinds are
special-cased", and only the full matrix proves they aren't. DA analysis mutation-tested (remove
an assignment on one path, watch the error fire).

## Batch 9 — Call-shape & expression statements (#1561, #1563 R3, #1565, #1617, #1562, #1591)

- **#1561** — lower mixed positional+spread into one materialized array for `params T[]`
  targets; spread-kind × target-kind matrix (fixed/variadic/mixed/kwargs — #208–211 covered
  only the non-variadic half).
- **#1563** — implement R3: full arity through function-typed values (clean diagnostic + steer);
  conversion-site warning where defaults are erased; spec deviation entry.
- **#1565** — member access on ANY builtin-typed receiver resolves against the known surface or
  refuses with did-you-mean; unknown-member probe across every builtin literal receiver kind.
- **#1617** — semantic-time elision of side-effect-free bare member statements as a materialized
  lowering fact (CPython-faithful no-op); bare-statement × expression-kind matrix. CI-relevant
  (ILCompiles re-roll class) — pull into Wave 1.
- **#1562** — resolve bare names in union-scrutinee pattern position against the union's
  variants before the capture fallback (the mechanism payload variants and Optional's
  `case None:` already use); warn on a capture shadowing a variant name. Silent-wrongness —
  pull into Wave 1.
- **#1591** — implement the in-comment ruling: thread `kwargTypes` into `CheckLambdaCall`
  (unknown name, duplicate, positional-only); Python spelling canonical, CLR spelling refuses
  with did-you-mean; migrate `json_module_tests.spy:183,188` in the same change.

**Acceptance:** each fix lands with its class matrix, not a single-cell fixture; the
`tagged_unions.md` documented spelling `case Idle:` executes correctly; spec examples executed
at HEAD (docs-need-execution-evidence).

## Batch 10 — Narrowed-receiver mutation (#1615 R8)

Implement the R8 refusal (steer: rebind through a typed local; the steer may cite #1619's
`isinstance(x, list[int])` once that exists). Both modes must stay identical (the b08ed6651
equivalence pin is the guard). Post-fix, tick the #1614 graduation checkbox that depends on it.

**Acceptance:** the repro refuses cleanly under both gate states; equivalence pin stays green;
the explicit `x.extend(...)` spelling on a Cast-narrowed receiver gets a matching decision
(refuse or work — not ICE), since the issue names it suspect.

## Batch 11 — Infra/tooling quickies (#1566, #1567, #1606)

- **#1566** — shared `dotnet-lock-env.sh` sourced by all three scripts (path + pid/child layout).
- **#1567** — bounded `wait` on the wrapper PID before exiting 4.
- **#1606** — extract a shared `WaitFirstLoudlyThenDrain` helper; audit the 8 remaining sites;
  convert first-notification waits, comment-justify genuine quiescence drains.

**Acceptance:** mutation-test each guard (move the lock path → memory scripts fail loudly, not
hang; kill mid-run → lock released before exit; helper's first-wait throws on timeout — no
vacuous pass).

## Batch 12 — Distribution/NuGet (#1580, #1581 R11)

- **#1580** — resolver consumes restore's own output (`obj/project.assets.json`) for resolved
  versions — the class rule: never re-derive what restore already decided; a dropped
  `PackageReference` is a hard error visible at default verbosity.
- **#1581** — R11: document the SDK requirement in release notes + skill docs; no code beyond
  the existing plain error.

**Acceptance:** the nonexistent-version repro either builds against the actually-restored
version or fails loudly naming the divergence — never silent; floating-version cell covered.

## Batch 13 — Stdlib fidelity (#1597 R9, #1598 R10)

- **#1597** — both loads doors refuse raw lone surrogates with one shared message; the
  named-fact test (not a corpus row — the theory pipeline mangles lone surrogates) flips from
  divergence-pin to agreement-pin.
- **#1598** — measure the YamlDotNet emitter-hook cost first, then implement per R10's arm
  (match PyYAML escapes, or pin + document deviation).

**Acceptance:** `JsonLoadsAgreementTests` divergence cell drains; PyYAML oracle re-run on the
escape cells (or the deviation entry lands with the measured cost recorded).

---

## Not scheduled this round

- **#1611** — `__index__`: documented non-feature (owner-ruled); stays open as documentation.
- **#1614** — inplace_augassign graduation tracker; #1615 (Batch 10) is its only near-term
  dependency; remaining checklist items are graduation-time work.
- **#1619** — reified isinstance: documented direction (R13); natural slot after Batch 10 lands.
- **#1592** — generated inheritance: parked (R12, label applied).
- **#1099 #1101 #1102 #1103** — roadmap parks carried from ruling 13 of the 2026-08-18 round.
- **#1020 #1021** — owner-parked type-system features.

---

## 5. Round mechanics (process guardrails, from this repo's own memory)

- **Serialized dotnet** via `.claude/scripts/dotnet-serialized` only; `dangerouslyDisableSandbox`
  required (sandboxed callers exit 125). No blanket pkill. Read `.claude/tmp/*.log` instead of
  re-running suites; `-latest.log` may rotate to a peer's run.
- **Until Batches 1/2/9 land**, ILCompiles fresh seeds can re-roll the open truthiness, slice,
  and bare-statement classes — triage a red against #1558/#1608/#1617 before attributing it to
  a batch.
- **Shared working tree**: agent prompts enumerate the git prohibitions (no
  checkout/restore/clean/stash/reset/rm), REPORT status rather than "must be clean", explicit
  per-file pathspecs when staging, commit lead work in small slices.
- **Regenerate early**: spy-stdlib C#, spy-test C#, stdlib docs — read the regen diff as
  findings, not noise. `/push` runs the staleness gates.
- **Spec-first for language changes** (Batches 1, 2's #1610, 8, 9's #1563): spec text with
  executed examples lands with or before the implementation (Rule 7 + docs-need-execution-
  evidence). Verify Python behavior first (Rule 6) for every truthiness/scoping cell.
- **Interop sweep is a separate CI step** — Batch 4a/4b changes touching CLR-type naming must be
  verified against the GapDiscovery interop sweep, not just FileBasedIntegration.
- **Mutation-test every new guard** (Batch 0's census, 4b's warm/cold harness, 5's API-surface
  test, 8's DA analysis, 11's helpers): break the guarded thing, watch the guard fail; the
  exemption must not be the subject.
- **Close-out discipline**: a closed issue is not a finished issue — closes cite execution
  evidence at a named sha; every fix that drains an allowlist entry deletes it in the same
  commit; batch completion is `/verify-implementation`-audited before its issues close.
- **Per-batch implementation plans** are created with `/create-plan` at implementation time,
  referencing this document and the ruling comments on the issues (the issue comment is the
  authoritative ruling text).
