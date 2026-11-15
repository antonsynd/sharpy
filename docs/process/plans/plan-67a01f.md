<!-- Verified by /verify-plan on 2026-11-15 @ 821f9d249 -->
<!-- Verification result: CONVERGED -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: built-first -->

# Membership operator totality: user iterables, chain links, operand order

## Context

Three class trackers, one group (charter §3.20w): #2645 `f() in g()` evaluates the container first; #2648 a chain refuses an `in` or `is` link at code generation; #2653 `in` on a class that only iterates is refused.

## Current State

Product @ 7be6704c4 (`run`); oracle CPython 3.15.2; stage 0 @ de55a06a0.

- **Order.** `n() in c()` prints `container`, `needle` for every plain-path container; `not in` too; a lifted `T | None` needle goes first. `x in [(x := 5), 2]` prints `True` (CPython `False`). `n() * s()` with the `str` on the right also evaluates right first.
- **Chain.** `x == x in [a, c]`, `c is d is e`, `a < x is not None`: SPY0519 from `RoslynEmitter.Expressions.Operators.cs:784`. The chain site runs a subset of the binary site's checks and both validators visit `BinaryOp` only, so a link's verdict is not its twin's (SPY0519 for SPY0320, `False` for SPY0402, Roslyn for a struct). `c is Color.RED is d` is SPY0349: `Parser.Expressions.cs:556` drops the head's identity operand.
- **User iterables.** A class with `__iter__` and no `__contains__` is SPY0320 (`ProtocolValidator.cs:201`) while `for x in b` and `1 in list(b)` run. It emits as `IEnumerable<T>` only when `__iter__` is a generator or names a producer (`__iter__ -> list[int]` gets no such base, #1915), and a method `contains` emits as `Contains`. A class with only `__len__` + `__getitem__` is SPY0320; its generic form also resolves a needle slot, so a mismatched needle draws SPY0222.

## Scope

- **Group:** P93 — membership operator totality (one non-chore group; no chores)
- **Tier:** full (c: the seam is the `in` lowering, under `Semantic/` and `CodeGen/`) — from charter §3.20w (contract §12)
- **Alone:** yes — D2 changes the emitted form of `in` where an operand has an evaluation (0 of 178 `.expected.cs` snapshots: 3 hold `.Contains(`, each with a literal or a name; spy-stdlib C# is measured at T1)
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger; the lead files by class (contract §10); trackers named at the end of this plan
- **Budget:** verify cost: the stage-0 facts and one whole-solution gate; the sibling-cell prober: the generator's matrix plus at most two axis changes (contract §10)
- **Override:** none
- **Scorecard baseline:** `allowlist_rows_outside_seed` 541 → 550 @ 907aad013 (owner, #2645 comment 6865268833: "Raise baseline to 550 (Recommended)") → 586 @ 3833e96e1 → 606 @ a547f91ad → 614 @ ed5642202, each one row per fact and cause (owner, comments 6870463430, 6873755292, 6882571713, 6885522237); not a seed harness

## Defect Class

- **Violated contract:** `x in c` is answered for every container the iteration ring accepts, in every position a comparison may stand, a chain link included; a chain link gets its binary twin's verdict; every binary operator evaluates its left operand first, whatever the operand's form.
- **Meta-class:** Position/route missing a shared check (the chain site, the user-class arm) and silent wrong behavior (order) → **standard cure:** the shared seam, and a generator over the sibling cells.
- **Known member cells:** #2645, #2648, #2653 and their comments.
- **Sibling cells this plan must also cover:** none; see Generator.
- **Lane / reach:** silent-wrong · program (#2645); false-refusal · probe (#2648); false-refusal · program (#2653). **Fidelity-scope row:** COMPARED ("value of an expression"; "which programs run vs. are refused": `expressions.md` › Expression Evaluation Order, `operator_precedence.md` › Comparison chaining).
- **Standing harness that should have caught it:** `OptionalMembershipDifferentialTests` (P92): its chain cells expected SPY0519, it observed no order and generated no user iterable. Stage 0 extends it.
- **Generator:** stage 0, 35 facts of `MembershipOperatorDifferentialBase` in 25 classes on nine shards (#2690), 35 passed / 0 failed / 0 skipped @ de55a06a0 on CI; 236323 known-red cells in 96 rows.
  - Cases: 74344 membership cells by value and order (12 iterating classes); 497250 operator sequences in every form: 16150 of one operator, 17000 of two links, 47600 pivots, 42500 in further positions, 68000 behind four hoist kinds, 170000 of three links, 136000 of three links behind a hoist; 2520 left-operand cells.
  - Known red, a row per fact and cause: matrix 46378 cells in 58 rows, sequences 188693 in 34, left-operand 1252 in 4.
  - Excluded, in the test: the literal-`None` needle's order; `@`, `|>`. Ruled out by cost: the blocks crossed together (3.76 million sequences), longer chains. Not enumerable, fixtures: the operand SPELLING after `is` (D4), a class's other members (D1).
  - Turned green: Phase 1's rows. Measured on a prototype of D1 and D2's bind: @ 1304d2af1 order alone → 0, `UserIterable` → 96 (#2674), `Sequence/Binary` → its 11 `*` cells; @ dfb8331c3 `LeftForm/Binary` 552 → 36, SPY0908: a conditional or a lambda typed by its slot, beside a hoisting container (the 18 tuple-literal cells run). Inferred: those 36 (D2's hoist ahead of a hoisting container), D3, D4, the `*` overload.
- **Owner rulings:** R-NZ (#2653 comment 6826244591, 2026-11-12: "accept as CPython, record it as a ruling"); R-OA (comment 6826280413: "maybe let's say no on __getitem__ with __len__"); R-OE (charter §4: one group on P92's generator). 2026-11-13: a chain link is refused as its twin (#2648 comment 6865267508: "Refuse, as the twin (Recommended)") and reaches Roslyn where its twin does (comment 6867353616: "Same as the twin (Recommended)"); #2645 comment 6867355702: bulk rows "Accept, pin the residue (Recommended)", the extended generator's allowlist "Accept the shared file (Recommended)", the left-operand form "Close the axis (Recommended)". 2026-11-14: the `__iter__` shape (#2653 comment 6870461621: "Widen the main matrix"); #2645 comment 6870463430: finding 5's four axes "Generate all four", rows "One row per fact and cause, 580", the two crashing left-operand forms "Known-red rows, baseline 552". Second batch: comment 6873755292, "Yes, 586 (Recommended)", "Cross everything", CI time "Accept, and track a CI shard issue"; #2653 comment 6873754974, "Generate all three". Third batch: #2645 comment 6882571713, "Add a shard, then restore it", "Yes, pairwise is it (Recommended)", "Yes, 606 (Recommended)", "Accept, verifier checks them (Recommended)" (superseded by the fourth). Fourth batch: comment 6885522237, "Yes, 614 (Recommended)", "Generate the two cells", "Raise PR limit to 40 (Recommended)".

## Adversarial Review (pre-mortem)

- **Alternative root cause:** `ProtocolMembership.Has(__contains__)` could answer yes for a class that iterates. Rejected: its other readers ask whether the dunder exists, and R-OA needs the no.
- **Alternative layer:** a Core helper taking the needle first for every `in` costs a delegate call per test and still needs the slot's type; a synthesized `Contains` changes a class's public C# surface (Axiom 1).
- **How the fix could be inert:** no validator visits an accepted `in` link; the link keeps its own copy of the binary arm; `b.Contains(x)` binds a user's `contains` (D1 names LINQ); a tuple container hoists above the bind (D2).
- **Before/after by direction:** D1: SPY0320 → runs. D2: nothing running is refused; SPY0908 → runs for #2644's conditional of constants into a wider slot and a slot-typed needle beside a hoisting container (54 cells). D3: SPY0519 → the twin's value or code; **worked → refused** (SPY0402) where the binary form refuses the `==` / `!=` link (488 sequences print); **refused → SPY0908** where the twin reaches Roslyn (10516 sequences). Both ruled, listed at T1.
- **Blast radius:** Compiler.Tests (fixtures, Differential, GapDiscovery, `SinkPushingTotalityTests`, `OperatorLoweringCorpusCensusTests`, the Pretty / unparse round trips), Stdlib.Tests, Cli.Tests, LSP `FrontEndParityTests`, the warm/cold harness.
- **Generated artifacts touched:** spy-stdlib and spy-test C# (D2), stdlib docs, the oracle ledger (D5): regenerated at T1 and after T6.

## Design Decisions

- **D1. A class the iteration ring enumerates answers `in` (R-NZ); the legacy sequence pair does not (R-OA).** Rung 3: LINQ's `Enumerable.Contains` over the class's own `IEnumerable<T>`, named in full; a checker fact on the container node.
  - One checker question, `IterationMembershipElement(host)`: a Sharpy-declared host (`TryGetGenericHost`: plain or constructed generic) with no `__contains__` in its chain answers with the element of the `IEnumerable[T]` `SynthesisAnalyzer` gives it, through the host view: `__next__`'s return type beside an `__iter__`, else the unpeeled return of a generator or producer `__iter__` (`IterableElementDecider.IsRecognizedProducerType`). No other `__iter__` answers.
  - `ResolveMembershipElementType` (`TypeChecker.Expressions.Operators.cs:1780`) returns it after the `__contains__` arms; a Sharpy-declared host takes no slot from `__getitem__`.
  - The membership site records it on the container node (`SetIterableProjection`: `Direct`, the element; in `MergeFrom`). `ProtocolValidator.ValidateMembership` reports SPY0320 only with neither `__contains__` nor that record. `GenerateContainsCall` emits `global::System.Linq.Enumerable.Contains<E>(container, needle)` for it. `ClassifyIterableSourceCore` is unchanged: `list(b)` and `sum(b)` are P15's cells.
  - `ClassifyMembership` is unchanged: a value-type `T | None` needle into a user iterable of `T` is lifted like any equality scan.
  - **Predicate:** `1 in IterBag(1, 2)` and `1 in GIterBag[int]([1, 2])` print `True`, as does a member of a class that also declares `contains` (returning `False`); `1 in SeqBag(1, 2)` and `1 in LB()` (`__iter__ -> list[int]`) are SPY0320; `None` into a generic `SeqBag[T]` is SPY0320, not SPY0222.
- **D2. The left operand is evaluated first (#2645).** Membership: a checker fact, the emitter's splice. String repeat: rung 2.
  - `ClassifyMembership` records, keyed on the needle node, the type the needle is bound as (a new `SemanticInfo` dictionary, in `MergeFrom`): the first slot `IsArgumentAssignable` takes it into, or the needle's own type when that is `T | None`, so the cast below never unwraps (#2674's value-type needles keep their SPY0908). No record, and today's emission: a container with no slot (#2249), a needle only the CLR call seam takes.
  - One emitter helper binds a recorded needle before a form that reads its right operand first: `((Slot)x) is var t && y.Contains(t)`, negated whole for `not in`. The cast gives C# the type the argument position gave (`is var` alone cannot type a tuple of narrower literals, a `None` arm, a lambda, a function name). Where the container hoists (a tuple's array bridge; a comprehension, a spread, `?`), the needle is hoisted first, into a local of that type. `GenerateMembershipTest` (`:345`) uses the helper; D3's membership link is the same call, and a chain's capture of a membership needle casts the same way. The reflected comparison (`:622`) keeps its plain `is var`: its left operand is typed alone.
  - Not bound: a literal-only left operand (a literal, or operators over literals: no evaluation), or two trivial operands (`IsTrivialExpression`). A bare name is bound when the container is not trivial.
  - `StringRepeatStrRight` (`:260`) emits a Core overload that takes the count first, `StringHelpers.Repeat(count, str)` for `int` and `long`, beside the existing `(str, count)` pair.
  - **Predicate:** no emission in `RoslynEmitter.Expressions.Operators.cs` places the right operand's syntax, or a statement it hoists, before a non-trivial left operand's, outside the helper and the slot-less case above.
- **D3. A chain link is its binary twin (#2648).** Semantic and CodeGen; no lower rung holds a chain.
  - Checker: the part of `CheckBinaryOp` that decides a comparison, identity or membership operator becomes one link routine, called by the binary site and by each link of `CheckComparisonChain` (`:1387`). It returns the link's facts: `ClassifyMembership`, `ClassifyComparisonLowering`, `ClassifyNoneTest`, `ClassifyEnumIdentity`, the void-comparand gate. The binary site records them as today; the chain in its `ComparisonLinkLowering` (`_comparisonChainLowerings` is in `MergeFrom`). The chain keeps what only a chain has: a needle typed under the next operand's slot, and the `None` operand's slot cast both its readers agree on (`noneSlots`).
  - Validators: `OperatorValidator` and `ProtocolValidator` validate an operator site (operator, left, right); `VisitBinaryOp` passes one and a new `VisitComparisonChain` passes each link.
  - Emitter: `BuildLink` in `GenerateComparisonChain` (`:728`) emits through the binary arm's emitters (`GenerateMembershipTest`, `GenerateNoneTest`, `GenerateIdentityComparison`, `GenerateLoweredComparison`), which take the recorded kind and the operand nodes instead of a `BinaryOp`. `MapComparisonOperator` loses its `SyntaxKind.None` arm and the chain its SPY0519.
  - **Ruled (Owner rulings):** a link is its twin in every outcome, a refusal and an internal error included: the two deliberate changes of direction of the Adversarial Review (contract §3).
  - **Predicate:** `CheckBinaryOp` and `CheckComparisonChain` call the link routine and no classifier of their own; `CodeGen/` holds no `UnsupportedOperator` for a chain.
- **D4. A chain head spelled like a type.** Parser and Semantic. When a comparison operator follows a `TypeCheck` head that has an `IdentityOperand`, `ParseComparison` builds the chain over `[Value, IdentityOperand, …]` with `Is` first and records the head's `CheckType` (its `TypeAnnotation`) on the chain (a new `ComparisonChain` field). The link routine gives link 0 `CheckTypeCheck`'s rule (`:2487`), shared: an enum member is an identity link, anything else SPY0349 with the same steer, its unchecked operand recorded `Unknown`, the type every walker of a chain already skips. A head with no dotted-name reading stays as it is. #2681 (P16) later replaces the spelling rule at both sites.
  - **Predicate:** `c is Color.RED is d` and `c is Color.RED == d` print `True` for three `RED`; `x is Dog is d` and `x is Outer.Inner is d` are SPY0349.
- **D5. Spec and deviation.** `docs/language_specification/membership_operators.md`: a Container kinds row for a class with `__iter__` and no `__contains__` (slot: the iterator's element type); the Validation paragraph and the *Implementation* note corrected (needle first). `docs/language_specification/comparison_chaining.md`: `in` and `is` links, with examples that differ under the bug. `docs/deviations.yaml`: a row for R-OA with the owner's reason.

## Implementation

### Phase 1: seams, lowering, spec

**Goal:** the P93 row families drain to their #2651 / #2674 cells.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: the stage-0 facts, their P93 rows drained as below; no `BulkCensus` entry left.
- Guard mutation:
  - M1: `IterationMembershipElement` ignores `__iter__` → `UserIterable/*` red; accepts `__getitem__` → the `GetItemLen` cells red (positive control: each SPY0320 at the restored tree); drops its producer gate → the `__iter__ -> list[int]` fixture red (SPY0908). `GenerateContainsCall` emits `y.Contains(x)` for the record → the `contains` fixture red (`False`).
  - M2, each removed → red: the bind → the order-alone rows and the membership cells of the `…/Binary` rows; its cast → the `@slot` left-operand cells (SPY0908); the hoist ahead of a tuple → the `Tuple` order cells; the bare-name arm → the `Identifier/Rebind` cells; the count-first overload → its `*` cells.
  - M3: `VisitComparisonChain` removed from `OperatorValidator` → the `Sequence/Chain` cells whose desugaring is SPY0402 red; from `ProtocolValidator` → those whose desugaring is SPY0320 red; an `is` link sent to `GenerateLoweredComparison` → the identity chains red.
  - M4: the parser drops the head's identity operand again → the D4 fixture red.
  - M5: a slot from `__getitem__` restored → the 18 `GetItemLen` literal-`None` cells red (SPY0222).
- Allowlist entries that drain: the order-alone and `…/Binary` rows whole; the chain, `UserIterable` and `GetItemLen` rows to pin lines cited to #2651, #2674 or a twin's tracker; `LeftForm/Ellipsis` and `LeftForm/Float32` stay; `scorecard_metrics.py --lower`.
- Execution evidence the close-out will cite: the stage-0 facts @ sha, the D5 spec blocks (`check_spec_blocks.sh`), the whole-solution gate.

#### Tasks

- **T1. Probe and regenerate** — each D's predicate by `run` at the base; spy-stdlib, spy-test, stdlib-docs and oracle-ledger regeneration (§7); the spy-stdlib sites D2 will move, counted.
  - Acceptance: base behaviour @ sha; by direction (contract §3): D3's chains from working to SPY0402 and from SPY0519 to SPY0908 (with each twin's tracker); what D1 and D2 move to running.
- **T2. Parser** (D4) — `Parser.Expressions.cs`, `Parser/Ast/Expression.cs`, the Pretty and dump visitors of `ComparisonChain`.
  - Acceptance: M4's fixture parses to a three-operand chain; the round-trip tests pass.
- **T3. Checker** (D1–D4) — `TypeChecker.Expressions.Operators.cs`, `SemanticInfo.cs`.
  - Acceptance: M5; the `Semantic` tests of the link routine.
- **T4. Validators** (D1, D3) — `ProtocolValidator.cs`, `OperatorValidator.cs`.
  - Acceptance: M1, M3 (validator halves).
- **T5. Core** (D2) — `src/Sharpy.Core/StringHelpers.cs` (C# 9 safe) and its Core.Tests.
- **T6. Emitter** (D1–D3) — `RoslynEmitter.Expressions.Operators.cs`; sink rows for the new `is var` sites; regeneration again.
  - Acceptance: M1 (emitter half), M2, M3 (emitter half).
- **T7. Spec, deviation, fixtures, drain** (D5) — the spec edits, the `deviations.yaml` row, the fixtures below, the `SpecRowMap` move, #2653's `OptionalElementControlExcluded` entries deleted, each drained portion's `BulkCensus` entry deleted, its residue pinned and re-cited, `--lower`.
  - Acceptance: the Phase 1 sweep bullet; the leftover count measured.

## Testing Strategy

- Stage 0 is the matrix. Fixtures under `operators/`: a class that iterates and declares `contains`, `.error` twins for the `__getitem__` + `__len__` class and `__iter__ -> list[int]`; order (`in`, `not in`, `int * str`, `x in [(x := 5), 2]` printing `False`); chains (`in`, `not in`, `is`, `is not`, a refused link's code, D4's four programs).
- Outputs discriminate: each order fixture prints the trace, so it differs under the bug.
- Regeneration at T1 and after T6; the gate is the whole solution (`/ci-gate` at the pushed tip, contract §6), counts `@ sha (measured)`, a red attributed only after a control run at the base.

## Issues to Close

- #2645 — evaluation order — closed by T3, T5, T6; **close criterion:** the order-alone and `…/Binary` rows gone @ sha, the gate.
- #2648 — chain links — closed by T2–T6; **close criterion:** no chain-row cell cited to #2648, the D4 fixture @ sha.
- #2653 — user iterables — closed by T3, T4, T6, T7; **close criterion:** no `UserIterable` or `GetItemLen` cell cited to #2653, the spec row and the deviation row @ sha.

## Class trackers this plan may post cells to

- #2645, #2648, #2653 — this contract
- #2644 — conditional and walrus needles; ordering on `T | None`
- #2651 — `Counter[T | None]`
- #2674 — value-ness
- #2680 — enum comparison
- #2681 — `x is A.B` decided by spelling
- #1915 — an `__iter__` that names no producer
- #1931 — `...` as a value

## Revision log

- @ 907aad013 — revised before the first pass: the owner's two rulings of 2026-11-13 recorded (#2648 comment 6865267508; #2645 comment 6865268833). No decision, predicate, mutation or count changed.
- @ 7cf933cec — revised after the 1st pass (NEEDS REVISION, Dimension 4, Dimension 6, Dimension 7): stage 0 @ 1304d2af1 answers findings 1, 2 (the axis), 4 and the bulk ruling; @ 3833e96e1, finding 5's four axes and the `__iter__` shape, one row per fact and cause; @ a547f91ad, the second batch: producer types, hosts, forms, hoist kinds; @ ed5642202, the third: three links behind a hoist, 35 facts, nine shards; @ de55a06a0, the fourth: both pivot sides. D1: the synthesized `IEnumerable[T]`, LINQ named (finding 3). D2: a recorded binding type, cast, tuple hoist (finding 2). D3: both direction changes ruled (finding 6). D4: `Unknown` for a refused head. M1–M3 split.
- deferred findings resolved in code @ 05f4871f6

## Verification Summary

**Result:** CONVERGED (pass 2 of 2; Dimension 6; contract §12 Convergence)
**Adequacy:** CLASS
**Verified on:** 2026-11-15 @ 821f9d249 (stage 0 @ de55a06a0; product code unchanged since 7be6704c4; every probe by `sharpyc run` on a build of this tip; oracle CPython 3.15.2)
**Plan file:** `docs/process/plans/plan-67a01f.md` (committed at every result)

Probe programs, mutation scripts and logs are in the session scratchpad under `p93/verify2/` (`pr/`, `mutTa.py`, `mutTb.py`, `mutK1.py`, `mutK3.py`, `logs/`).

Pass 1's six findings are answered, read against the generator and the plan: the `ChainLink` expectation is keyed on the measured head twin (1); the left operand's form is an axis sourced from the `Expression` records by reflection, and D2 binds through a cast to the recorded type (2); D1 names LINQ and takes the ring's producer gate (3); the sequence fact has its own payload exclusions and gains `object` and `char` (4); the four axes are generated (5); both changes of direction are in the Adversarial Review and at T1 (6).

### Corrections Made
- None. The body is at its cap (20,476 of 20,480 bytes before this record), and the design sections are not revised after a CONVERGED stamp (contract §12). Every finding of this pass is a row below.

### Adequacy
- **Contract:** named — the Defect Class sentence.
- **Axes:** all roster-closed, each with its totality assertion; one exclusion entry of the reflection-sourced lowering-kind roster is rejected (Deferred finding 1). Read from the test and recomputed from its enumerations: 74344 membership cells (31 payloads, 34 containers, 12 iterating classes from `ProducerElementRoster` × shape × host); 497250 sequences (`BinaryOperator` through `SpellingOf`, no default arm; `ComparisonOperator` pairs and triples; `Position` through `SequencePositionOf`; the hoist kinds; seven pivots closed against `OperatorLoweringKind` and `BinaryOpLowering` by census; `SequenceForm`, a closed literal set of five); 2520 left-operand cells (every non-abstract `Expression` record, by reflection, spelled or in `LeftFormUnspellable` with its program). The expected result is a rule: CPython's answer or the owner-ruled one for the matrix, the measured head twin for a chain link, the desugaring for a sequence.
- **Cure:** seam + generator — Position/route missing a shared check; silent wrong behavior.
- **Harness:** named (`MembershipOperatorDifferentialBase`, stage 0 @ de55a06a0); the allowlist delta is stated by row family (Acceptance: what drains whole, what drains to pin lines, what stays).
- **Layer:** every decision names its rung; the new `SemanticInfo` dictionary is said to join `MergeFrom`.

### Scope & convergence
- **Group:** one (P93; no chores; charter §3.20w, tier full (c)) · **Alone:** yes — required? yes (D2 moves emitted C#) · **Gates:** 1
- **Generator:** stage 0 @ de55a06a0, mutations re-run by `cp`, each restore proved by `cmp` and by sha256 against the copy taken before the first mutation:
  - Path rule, `git diff --name-only a7b6067d3..de55a06a0 -- ':!docs/process/plans/'`: `build_tools/scorecard-baseline.json`, 26 files under `src/Sharpy.Compiler.Tests/`, `src/Sharpy.TestInfrastructure/Integration/IntegrationTestBase.cs`. All test code or the baseline. `de55a06a0..821f9d249` changes the plan only.
  - Totality broken, cells dropped in four places with no literal touched: 8 of 8 facts failed in 17.8 s ("the generator made 74100 cells; the axes give 74344" on the four matrix facts; "made 47593 sequences; the axes give 47600"; "made 20340 … give 20400"; "made 13560 … give 13600"; "made 2512 left-operand cells; the axes give 2520").
  - Totality broken, a map loses a value or gains an undeclared exclusion: 4 of 4 failed in 10.0 s (the iterating-class roster against its product; "the Expression records have no left-operand form entry for [WalrusExpression]"; "SpellingOf excludes 3 operators; 2 are pinned", twice).
  - A passing case listed as known-red in three rows, header digests kept consistent, and one pivot removed: 4 of 4 failed in 6.07 min ("removed … it now meets its expectation" in `LeftForm/Binary`, `CounterOfOptional/Plain` and `SequencePivot/Binary`; "bulk portion (bulk=575:…) does not match its 574 divergent cells"; "no generated sequence records these equality strategies …: [EqualsCallStatic]").
  - A row no fact owns: 1 of 1 failed in 2.16 min ("the row Stray/Row is owned by 0 facts"); `scorecard_metrics.py --check` failed on 615 > 614.
  - Restored: 35 passed / 0 failed / 0 skipped @ 821f9d249 in 1.03 h, run together (the filter also ran 101 neighbouring tests, all passed); each of the 35 proposed allowlists byte-identical to the committed file; `scorecard_metrics.py --check` OK at 614; `git status --short` empty.
  - Counts as the plan cites them, measured: 35 facts in 25 classes; 96 rows and 236323 known-red cells (matrix 46378 in 58, sequences 188693 in 34, left-operand 1252 in 4); by direction over the 30 sequence reports of the restored run, 488 sequences print where the desugaring is SPY0402 and 10516 are SPY0519 where it is SPY0908.
  - CI @ 821f9d249: every job green except `review-line`, which is expected red before the review line exists.
- **Convergence:** the Revision log carries `(NEEDS REVISION, Dimension 4, Dimension 6, Dimension 7)`. This pass has one error, on Dimension 6, and it is deferrable: an exclusion entry of a reflection-sourced roster, with its catcher re-run. Dimensions 4 and 7 have warnings only. No third pass is asked for.
- **Trackers named:** #2645, #2648, #2653, #2644, #2651, #2674, #2680, #2681, #1915, #1931 (all open, read with `gh api`) · **Fidelity rows:** named (COMPARED); R-OA's deviation row is D5's.

### Falsifiability
- Stage-0 totality assertions: mutation re-run, red (two shapes, above); exemption not subject; positive controls in the test.
- Stage-0 allowlist pin and row ownership: mutation re-run, red, for a pin line, a bulk row, a new row and an unowned row.
- Stage-0 lowering-kind and equality-strategy census: mutation re-run, red, both (the pivot removed; an exclusion entry deleted). `SequenceExcludedKinds` has no pinned count, so an entry added with any reason passes: its reasons are held by review only (see the deferred finding).
- M1 (`IterationMembershipElement`, `GenerateContainsCall`): four mutations named, each with its red cells or fixture; positive control yes (each SPY0320 at the restored tree); by-direction specified (SPY0320 → runs).
- M2 (the bind, its cast, the tuple hoist, the bare-name arm, the count-first overload): five mutations named, each with its cells.
- M3: one mutation per validator and one for the emitter's identity link, each with its cells.
- M4 (the parser), M5 (the `__getitem__` slot): named, with fixture and cells.
- By-direction probe: specified at T1 for D1, D2 and D3, both of D3's changes with counts re-measured here. D4's direction is not written; read from the parser and two probes, every program D4 touches is SPY0349 today, so nothing running changes.

### Deferred findings
- `SequenceExcludedKinds[OptionalNoneTest]`, and with it the operand typing of the none-last form (`SequenceForm`) ← `OperatorLoweringKind`: the three None-test kinds `ClassifyNoneTest` records (`TypeChecker.Expressions.Operators.cs:513`) — fix: delete the entry (`OptionalMembershipDifferentialTests.cs:2369`) and generate the none-last sequences over a tagged `T?` subject too, inside the form so that every block's product takes it, then re-measure the totals, the `BulkCensus` constants and the `…/NoneLast/…` rows; a `T | None` subject of a value type rides the same change — proved by the one-operator fact's census assertion ("no generated sequence records these lowering kinds on a binary operator", `:2661`), re-run here by mutation: entry deleted → 1 failed in 1.6 min naming `[OptionalNoneTest]`; restored `cmp`-identical → 1 passed.
  - Why it is an error and not a note: the entry's reason is that the generator's none-last form types its subject as the payload, which describes the gap. The other eight entries name a structural reason or the fact that holds the value. `a == y is None` over `int?` is SPY0519 today and its desugaring prints `0`, `1`, `False` (`pr/ont3.spy`, `pr/ont3d.spy`); the same over `int | None` is SPY0519 (`pr/ont4.spy`). Both are cells of #2648 inside this contract that no fact generates and no row pins, and D3's `GenerateNoneTest` link has three arms of which the chain blocks reach two.
  - It changes no decision, predicate, mutation, tier, scope or ruling. Done inside the existing `NoneLast` facts it adds no fact and no row; if it does add a row, `allowlist_rows_outside_seed` needs the owner's word again (contract §11).

### Warnings
- **The D1 prototype figure is older than the axis it describes** (Dimension 4). "`UserIterable` → 96 (#2674)" was measured @ 1304d2af1, when the generator had two of the twelve iterating classes; the ten added since are not prototyped and are not in the "Inferred:" list. Read from `sharpyc emit csharp` on seven shapes (a producer of each roster type, a generator, a `__next__` pair, an inherited `__iter__`, a generic `__next__` pair), each class emits `IEnumerable<T>` and `for` runs, which is D1's premise; `__iter__ -> list[int]` emits no such base. T7 measures the residue ("the leftover count measured"), so the number to expect is that one, not 96.
- **Current State, "`1 in list(b)` run"** does not hold for a `__next__` pair: `list(NextBag(1, 2))` is SPY0320 (P15's cell, as D1 says of `list(b)`). No decision reads it.
- **The sequence and left-operand facts have no CPython oracle**, by design: the reference is Sharpy's desugaring. A defect the desugaring shares is not seen. The `ChainLink` cells of the membership matrix have the oracle.
- **By direction, a third movement the plan does not list:** 156 sequences are SPY0908 today where the desugaring is SPY0402; D3 turns an internal error into the twin's refusal. A gain, counted from the committed pin lines.
- **The `*=` twin of the string repeat** (`RoslynEmitter.Statements.Assignments.cs:914`) is outside the file D2's predicate names. Its right-operand-first arm is dead: `n *= "ab"` is SPY0220 (measured). No task needed.
- **The count-first overload** is inferred from the emitted C#: `int`, `int64`, `int8` and `uint32` on the left of a `str`, and `int` and `int64` on the left of a `char`, emit `StringHelpers.Repeat(str, count)` with the count typed `int`, `long`, `sbyte` or `uint`, and `char` is `string` in C#. `Repeat(int, string)` and `Repeat(long, string)` take each by overload resolution. Not compiled.
- **Testing Strategy** names `/ci-gate` and contract §6 and no longer spells the local fallback command (CLAUDE.md › Testing has it).
- **D4 adds a field and an operand to `ComparisonChain`** and the task list has no LSP step; `FrontEndParityTests` is in the blast radius and is the check.
- **Scope › Scorecard baseline:** 606 and 614 are cited at the commits where they were measured for the owner (a547f91ad, ed5642202); the file was set to them in dfb8331c3 and in 53ad47620 / e26e311f3. The numbers hold at the cited commits and at de55a06a0.
- **Process:** the fact classes sit in namespaces chosen by CI shard, not by subject (#2690 is open for the shard balance). The Revision log holds one line for pass 1, amended each round; that is one line per pass, and the pass count reads 2 of 2.

### Missing Steps Added
- T1's by-direction list: also the 156 sequences that go from SPY0908 to SPY0402, and D4's programs (SPY0349 → value, or SPY0349 kept).
- T2: an LSP check of the new `ComparisonChain` field (hover and semantic tokens over `c is Color.RED is d`), or the line that says `FrontEndParityTests` holds it.
- Testing Strategy: the local fallback of the gate, as CLAUDE.md › Testing spells it.

### Unchecked Claims
- The D1 and D2 prototypes were not re-run (a product change in a shared tree). Read from the reviser's reports: the left-operand fact @ dfb8331c3 goes from 1252 divergent cells to 736. The 36 slot-typed needle cells beside a hoisting container, D3, D4 and the `*` overload are inferred, as the plan says.
- The twins of the two pivot cells the fourth ruling generated (11900 tagged-left, 5950 none-last on the right) were not re-run as a comparison; the reviser's verdict files read 11900 of 11900 and 5950 of 5950 with the same two verdicts, and the pivot fact's proposed allowlist was byte-identical in my run.
- The mutation records of the stage-0 commit bodies were read (each test-changing commit carries them), not re-run as written; the five mutations above are my own.
- The spy-stdlib sites D2 moves: not counted (T1). Snapshots re-measured: 178 files, 3 with `.Contains(`, none with a needle D2 binds.
- `check_plan_refs.py --issues` cannot look issues up here (`gh issue view` is refused); the ten trackers and #2690 were read with `gh api` and are open.
- **Owner question** (optional; the stamp does not wait for it): the deferred finding is resolved in code by `/implement-plan`'s first task. If you would rather keep `OptionalNoneTest` excluded, say so and the row is struck; the exclusion then wants a reason that is not the generator's own limit.

## Implementation Checklist

Base cd4071554 (`origin/dev`, the plan's merge); branch `claude/membership-totality`. Lead: this session. Tasks are commit-sized; owners are background agents unless marked lead.
Baseline: 84888/0/10 @ cd4071554 (measured, CI run 43666691859). Build @ cd4071554: 0 warnings.

- [x] T0 — Deferred finding 1: delete `SequenceExcludedKinds[OptionalNoneTest]`, none-last sequences over a tagged `T?` subject, re-measure totals, `BulkCensus` and `…/NoneLast/…` rows — test-expert — blocked by none — 05f4871f6 (deferred finding 1 resolved in code; split by subject typing, baseline 614 → 616 per owner ruling; mutation: broken → red 2 of 2, restored → green 2; re-run by the lead @ 05f4871f6: `OptionalNoneTest` re-excluded → red with the stale-exclusion message, restored → green 1 passed; known-red sequence cells 188693 → 208989, rows 96 → 98)
  - Lead ruling (C): tagged `T?` for every payload, `T | None` for the value-type payloads (measured by the emitted CLR type, 19). Triple/NoneLast/ComparisonFirst then ran 9m24s locally against CI's 10 min hang timeout; **owner ruling, 2026-11-15** (AskUserQuestion): "Split, baseline 616 (Recommended)" — the two Triple/NoneLast facts split by subject typing, 2 new rows, `allowlist_rows_outside_seed` 614 → 616.
- [x] T1 — Probe each D's predicate by `run` at the base; by-direction lists (D3 both moves, the 156 SPY0908 → SPY0402, D4's programs); count the spy-stdlib sites D2 moves — lead — blocked by none
  - @ cd4071554 (`run`, CPython 3.15.2): D1 `IterBag`/`GIterBag[int]`/generator+`contains` SPY0320 → to run; `SeqBag`, `LB` SPY0320 kept; generic `SeqBag` `None` SPY0222+SPY0320 → SPY0320. D2 `n() in c()` / `not in` print container first, `k() * s()` str first, walrus `True` (CPython needle/count first, `False`): wrong output → CPython, nothing refused. D3 `in`/`is`/`is not` links SPY0519 → twin (488 worked → SPY0402, 10516 SPY0519 → SPY0908, both ruled; 156 SPY0908 → SPY0402, gain). D4 `c is Color.RED is d` / `== d` SPY0349 → value; `x is Dog is d`, `x is Outer.Inner is d` SPY0349 kept. Spy-stdlib sites D2 moves: read from the T3r/T6r regeneration diff.
- [x] T2 — Parser (D4) + Pretty/dump of `ComparisonChain`; LSP check over `c is Color.RED is d` — parser-expert — blocked by none — 435c51bc1 (M4: red 4 of 16, restored green 16; `FrontEndParityTests` compares diagnostics only, so direct LSP tests added). Leaves `errors/enum_member_is_chain_head_refused_2669` and 72 `EnumIdentityDifferentialTests` chain-head cells red (SPY0349 → SPY0519) until T3 gives link 0 its rule.
- [x] T5 — Core `StringHelpers.Repeat(count, str)` for `int` and `long` + Core.Tests — core-library-expert — blocked by none — 740030e8e (mutation: red 6 failed, restored green 11; re-run by the lead: 6 failed → 11 passed)
- [ ] T3 — Checker: D1 `IterationMembershipElement`, D2 needle-binding record (`MergeFrom`), D3 link routine, D4 link 0; M5 — semantic-expert — blocked by T2
- [ ] T4 — Validators: SPY0320 gate on D1's record; `VisitComparisonChain` in both validators; M1/M3 validator halves — semantic-expert — blocked by T3
- [ ] T3r — Regeneration after the first Semantic commit (spy-stdlib, spy-test, stdlib docs, oracle ledger); diff read as findings — lead — blocked by T3
- [ ] T6 — Emitter: D1 LINQ `Contains`, D2 bind helper + hoist + count-first repeat, D3 `BuildLink` through binary emitters; sink rows; M1/M2/M3 emitter halves — codegen-expert — blocked by T4, T5
- [ ] T6r — Regeneration again after T6 — lead — blocked by T6
- [ ] T7 — Spec (D5), `deviations.yaml` R-OA row, fixtures, `SpecRowMap`, `OptionalElementControlExcluded` entries, drain allowlist to pin lines, `BulkCensus` entries, `scorecard_metrics.py --lower` — test-expert — blocked by T6r
- [ ] TF — Final verification: build, Infrastructure|Conformance filter, whitespace, commit subjects, `/ci-gate`, GapDiscovery sweeps, staleness gates, spec examples, `verification-expert` standalone — lead — blocked by all
- [ ] TW — `dotnet format whitespace` — lead — blocked by all
