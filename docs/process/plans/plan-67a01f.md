<!-- DRAFT, not verified: written by /create-plan on 2026-11-13 @ 9fce3ecc4; revised by /create-plan on 2026-11-14 @ e335aa80c after pass 1; no /verify-plan pass has stamped this text. -->

# Membership operator totality: user iterables, chain links, operand order

## Context

Three class trackers, one group (charter §3.20w): #2645 `f() in g()` evaluates the container first; #2648 a chain refuses an `in` or `is` link at code generation; #2653 `in` on a class that only iterates is refused.

## Current State

Product @ 7be6704c4 (`run`); oracle CPython 3.15.2; stage 0 @ e335aa80c.

- **Order.** `n() in c()` prints `container`, `needle` for every plain-path container; `not in` too; a lifted `T | None` needle goes first. `x in [(x := 5), 2]` prints `True` (CPython `False`). `n() * s()` with the `str` on the right also evaluates right first.
- **Chain.** `x == x in [a, c]`, `c is d is e`, `a < x is not None`: SPY0519 from `RoslynEmitter.Expressions.Operators.cs:784`. The chain site runs a subset of the binary site's checks and both validators visit `BinaryOp` only, so a link's verdict is not its twin's (SPY0519 for SPY0320, `False` for SPY0402, Roslyn for a struct). `c is Color.RED is d` is SPY0349: `Parser.Expressions.cs:556` drops the head's identity operand.
- **User iterables.** A class with `__iter__` and no `__contains__` is SPY0320 (`ProtocolValidator.cs:201`) while `for x in b` and `1 in list(b)` run. It emits as `IEnumerable<T>` only when `__iter__` is a generator or names a producer (`__iter__ -> list[int]` gets no such base, #1915), and a method `contains` emits as `Contains`. A class with only `__len__` + `__getitem__` is SPY0320; its generic form also resolves a needle slot, so a mismatched needle draws SPY0222.

## Scope

- **Group:** P93 — membership operator totality (one non-chore group; no chores)
- **Tier:** full (c: the seam is the `in` lowering, under `Semantic/` and `CodeGen/`) — from charter §3.20w (contract §12)
- **Alone:** yes — D2 changes the emitted form of `in` where an operand has an evaluation (0 of 178 `.expected.cs` snapshots: 3 hold `.Contains(`, each with a literal or a name; spy-stdlib C# is measured at T1)
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger; the lead files by class (contract §10); trackers named at the end of this plan
- **Budget:** verify cost: the seven facts and one whole-solution gate; the sibling-cell prober: the generator's matrix plus at most two axis changes (contract §10)
- **Override:** none
- **Scorecard baseline:** `allowlist_rows_outside_seed` 541 → 550 @ 907aad013 (owner, 2026-11-13, #2645 comment 6865268833: "Raise baseline to 550 (Recommended)") → 586 @ 3833e96e1, the measured count of one row per fact and cause (owner, 2026-11-14, comment 6870463430); not a seed harness

## Defect Class

- **Violated contract:** `x in c` is answered for every container the iteration ring accepts, in every position a comparison may stand, a chain link included; a chain link gets its binary twin's verdict; every binary operator evaluates its left operand first, whatever the operand's form.
- **Meta-class:** Position/route missing a shared check (the chain site, the user-class arm) and silent wrong behavior (order) → **standard cure:** the shared seam, and a generator over the sibling cells.
- **Known member cells:** #2645, #2648, #2653 and their comments.
- **Sibling cells this plan must also cover:** none; see Generator.
- **Lane / reach:** silent-wrong · program (#2645); false-refusal · probe (#2648); false-refusal · program (#2653). **Fidelity-scope row:** COMPARED ("value of an expression"; "which programs run vs. are refused": `expressions.md` › Expression Evaluation Order, `operator_precedence.md` › Comparison chaining).
- **Standing harness that should have caught it:** `OptionalMembershipDifferentialTests` (P92): its chain cells expected SPY0519, it observed no order and generated no user iterable. Stage 0 extends it.
- **Generator:** stage 0, seven facts of `MembershipOperatorDifferentialBase` in four shard classes, 7 passed / 0 failed / 0 skipped @ e335aa80c (14.5 min locally; CI's longest fact 5 m 57 s, shard 13 min of 25); 67283 known-red cells in 68 rows.
  - Cases: 57320 membership cells by value and by order (four `__iter__` shapes); 92650 operator sequences (33150 binary and two-link, every `BinaryOpLowering` recorded; 8500 in further positions; 17000 with a hoisting last operand; 34000 of three links); 2520 left-operand cells (45 forms × 7 right operands, 4 hoisting).
  - Known red, one fact and one cause a row: order alone 8442; chains 6422 by value + 4970 by order, 35232 sequences, 616 left-operand; user iterables 5220 + 3660; binary order 405 sequences, 552 left-operand; `GetItemLen` 24 + 6; `LeftForm/Ellipsis` 42 (#1931), `LeftForm/Float32` 42 (#2644). A bulk portion is one cause at its census count (`BulkCensus`); any other cell is a pin line.
  - Excluded axes, in the test: the literal-`None` needle's order; `@`, `|>`. Not crossed (cost; put to the owner): three links, further positions and pivots outside the plain form; a sequence's hoisting last operand beyond a comprehension; a generic host beyond the producer shape. Not enumerable, fixtures: the operand SPELLING after `is` (D4), a class's other members (D1).
  - Turned green: D1 the `UserIterable` rows and 18 `GetItemLen` cells; D2 the order-alone and `…/Binary` rows; D3 the chain rows; twin-red cells (#2651, #2674) stay, as pin lines. Measured @ 1304d2af1 on a restored prototype of D1 and D2's bind: order alone → 0, `UserIterable` → 96 (#2674), `Sequence/Binary` → its 11 `*` cells; D3, D4 and the `*` overload are inferred.
- **Owner rulings:** R-NZ (#2653 comment 6826244591, 2026-11-12: "accept as CPython, record it as a ruling"); R-OA (comment 6826280413: "maybe let's say no on __getitem__ with __len__"); R-OE (charter §4: one group on P92's generator). Unlettered until the next batching call, 2026-11-13: a chain link is refused as its twin (#2648 comment 6865267508: "Refuse, as the twin (Recommended)") and reaches Roslyn where its twin does (comment 6867353616: "Same as the twin (Recommended)"); #2645 comment 6867355702: bulk rows "Accept, pin the residue (Recommended)", the extended generator's allowlist "Accept the shared file (Recommended)", the left-operand form "Close the axis (Recommended)". 2026-11-14: the `__iter__` shape (#2653 comment 6870461621: "Widen the main matrix"); #2645 comment 6870463430: finding 5's four axes "Generate all four", rows "One row per fact and cause, 580", the two crashing left-operand forms "Known-red rows, baseline 552".

## Adversarial Review (pre-mortem)

- **Alternative root cause:** `ProtocolMembership.Has(__contains__)` could answer yes for a class that iterates. Rejected: its other readers ask whether the dunder exists, and R-OA needs the no.
- **Alternative layer:** a Core helper taking the needle first for every `in` costs a delegate call per test and still needs the slot's type; a synthesized `Contains` changes a class's public C# surface (Axiom 1).
- **How the fix could be inert:** no validator visits an accepted `in` link; the link keeps its own copy of the binary arm; `b.Contains(x)` binds a user's `contains` (D1 names LINQ); a tuple container hoists above the bind (D2).
- **Before/after by direction:** D1: SPY0320 → runs. D2: nothing running is refused; SPY0908 → runs for #2644's conditional of constants into a wider slot and a slot-typed needle beside a hoisting container (54 cells). D3: SPY0519 → the twin's value or code; **worked → refused** (SPY0402) where the binary form refuses the `==` / `!=` link (160 sequences print); **refused → SPY0908** where the twin reaches Roslyn (1400 sequences). Both ruled, listed at T1.
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
  - One emitter helper binds a recorded needle before a form that reads its right operand first: `((Slot)x) is var t && y.Contains(t)`, negated whole for `not in`. The cast gives C# the type the argument position gave (`is var` alone cannot type a tuple of narrower literals, a `None` arm, a lambda, a function name). Where the container hoists (a tuple's array bridge; a comprehension, a spread, `?`: today's untyped capture is SPY0908 for a slot-typed needle), the needle is hoisted first, into a local of that type. `GenerateMembershipTest` (`:345`) uses the helper; D3's membership link is the same call, and a chain's capture of a membership needle casts the same way. The reflected comparison (`:622`) keeps its plain `is var`: its left operand is typed alone.
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
- Matrix/sweep that goes green: the seven facts, their P93 rows drained as below; no `BulkCensus` entry left.
- Guard mutation:
  - M1: `IterationMembershipElement` ignores `__iter__` → `UserIterable/*` red; accepts `__getitem__` → the `GetItemLen` cells red (positive control: each SPY0320 at the restored tree); drops its producer gate → the `__iter__ -> list[int]` fixture red (SPY0908). `GenerateContainsCall` emits `y.Contains(x)` for the record → the `contains` fixture red (`False`).
  - M2, each removed → red: the bind → the order-alone rows and the membership cells of the `…/Binary` rows; its cast → the `@slot` left-operand cells (SPY0908); the hoist ahead of a tuple → the `Tuple` order cells; the bare-name arm → the `Identifier/Rebind` cells; the count-first overload → its `*` cells.
  - M3: `VisitComparisonChain` removed from `OperatorValidator` → the `Sequence/Chain` cells whose desugaring is SPY0402 red; from `ProtocolValidator` → those whose desugaring is SPY0320 red; an `is` link sent to `GenerateLoweredComparison` → the identity chains red.
  - M4: the parser drops the head's identity operand again → the D4 fixture red.
  - M5: a slot from `__getitem__` restored → the 18 `GetItemLen` literal-`None` cells red (SPY0222).
- Allowlist entries that drain: the order-alone and `…/Binary` rows whole; the chain, `UserIterable` and `GetItemLen` rows to pin lines cited to #2651, #2674 or a twin's tracker; `LeftForm/Ellipsis` and `LeftForm/Float32` stay; `scorecard_metrics.py --lower`.
- Execution evidence the close-out will cite: the seven facts @ sha, the D5 spec blocks (`check_spec_blocks.sh`), the whole-solution gate.

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
- **T7. Spec, deviation, fixtures, drain** (D5) — the spec edits, the `deviations.yaml` row, the fixtures below, the `SpecRowMap` move, #2653's two `OptionalElementControlExcluded` entries deleted, each drained portion's `BulkCensus` entry deleted, its residue pinned and re-cited, `--lower`.
  - Acceptance: the Phase 1 sweep bullet; the leftover count measured.

## Testing Strategy

- Stage 0 is the matrix. Fixtures under `operators/`: a class that iterates and declares `contains`, `.error` twins for the `__getitem__` + `__len__` class and `__iter__ -> list[int]`; order (`in`, `not in`, `int * str`, `x in [(x := 5), 2]` printing `False`); chains (`in`, `not in`, `is`, `is not`, a refused link's code, D4's four programs).
- Outputs discriminate: each order fixture prints the trace, so it differs under the bug.
- Regeneration at T1 and after T6; the gate is the whole solution: `/ci-gate` at the pushed tip, the local run of contract §6 as its fallback, counts `@ sha (measured)`, a red attributed only after a control run at the base.

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

- @ 907aad013 — revised before the first pass: the owner's two rulings of 2026-11-13 recorded. D3's unruled question is decided (a chain link is refused as its binary twin is; #2648 comment 6865267508) and stage 0's scorecard baseline is raised 541 → 550 (#2645 comment 6865268833). No decision, predicate, mutation or count changed.
- @ 7cf933cec — revised after the 1st pass (NEEDS REVISION, Dimension 4, Dimension 6, Dimension 7): stage 0 @ 1304d2af1 answers findings 1, 2 (the axis), 4 and the bulk ruling; @ 3833e96e1, by the 2026-11-14 rulings, finding 5's four axes and the `__iter__` shape are generated, one row per fact and cause; @ e335aa80c four shard classes. D1: the synthesized `IEnumerable[T]`, LINQ named (finding 3). D2: a recorded binding type, cast, tuple hoist (finding 2). D3: both direction changes ruled (finding 6). D4: `Unknown` for a refused head. M1–M3 split.
