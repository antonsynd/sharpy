<!-- DRAFT, not verified: written by /create-plan on 2026-11-13 @ 9fce3ecc4; revised by /create-plan on 2026-11-13 @ 907aad013 before the first pass; no /verify-plan pass has stamped this text. -->

# Membership operator totality: user iterables, chain links, operand order

## Context

Three class trackers, one group (charter §3.20w): #2645 `f() in g()` evaluates the container first; #2648 a chain refuses an `in` or `is` link at code generation; #2653 `in` on a class that only iterates is refused.

## Current State

Product @ 7be6704c4 (`run`); oracle CPython 3.15.2; stage 0 @ 9fce3ecc4, its scorecard baseline @ 907aad013.

- **Order.** `n() in c()` prints `container`, `needle` for every plain-path container (list, tuple, dict, set, range, `str`, `bytes`, a `__contains__` class); `not in` too; a lifted `T | None` needle goes first. `n() * s()` with the `str` on the right also evaluates right first. The other operators hold (38 probed, then the generator).
- **Chain.** `x == x in [a, c]`, `c is d is e`, `a < x is not None`: SPY0519 from `RoslynEmitter.Expressions.Operators.cs:784`. The checker's chain site runs a subset of the binary site's checks and both validators visit `BinaryOp` only: `x == x in k` (`k` no container) is SPY0519 where the binary twin is SPY0320; `a == c == a` over a class without `__eq__` compiles (prints `False`, as CPython) where `a == c` is SPY0402; over a struct it reaches Roslyn (SPY0908). `c is Color.RED is d` is SPY0349: `Parser.Expressions.cs:556` drops the head's identity operand.
- **User iterables.** A class with `__iter__` and no `__contains__` (plain or generic) is SPY0320 (`ProtocolValidator.cs:201`) while `for x in b` and `1 in list(b)` run; it emits as `IEnumerable<T>` with `System.Linq` imported. A class with only `__len__` + `__getitem__` is SPY0320 (and SPY0320 under `for`); its generic form also resolves a needle slot, so a mismatched needle draws SPY0222.
- **Generator.** Membership matrix: 50024 cases, 23146 divergent in 30 rows. Sequence matrix: 29600 sequences, 6505 divergent in 2 rows. 32 allowlist rows (23 before).

## Scope

- **Group:** P93 — membership operator totality (one non-chore group; no chores)
- **Tier:** full (c: the seam is the `in` lowering, under `Semantic/` and `CodeGen/`) — from charter §3.20w (contract §12)
- **Alone:** yes — D2 changes the emitted form of `in` where an operand has an evaluation (0 of 178 `.expected.cs` snapshots: 3 hold `.Contains(`, each with a literal or a name; spy-stdlib C# is measured at T1)
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger; the lead files by class (contract §10); trackers named at the end of this plan
- **Override:** none
- **Scorecard baseline:** `allowlist_rows_outside_seed` 541 → 550 for stage 0's nine rows, raised @ 907aad013 (contract §11) — owner, 2026-11-13, #2645 comment 6865268833: "Raise baseline to 550 (Recommended)"; the allowlist is not registered as a seed harness

## Defect Class

- **Violated contract:** `x in c` is answered for every container the iteration ring accepts, in every position a comparison may stand, a chain link included; a chain link gets its binary twin's verdict; every binary operator evaluates its left operand first.
- **Meta-class:** Position/route missing a shared check (the chain site, the user-class arm) and silent wrong behavior (order) → **standard cure:** the shared seam, and a generator over the sibling cells.
- **Known member cells:** #2645, #2648, #2653 and their comments.
- **Sibling cells this plan must also cover:** none; see Generator.
- **Lane / reach:** silent-wrong · program (#2645); false-refusal · probe (#2648); false-refusal · program (#2653). **Fidelity-scope row:** COMPARED ("value of an expression"; "which programs run vs. are refused": `expressions.md` › Expression Evaluation Order, `operator_precedence.md` › Comparison chaining).
- **Standing harness that should have caught it:** `OptionalMembershipDifferentialTests` (P92): its chain cells expected SPY0519, it observed no order and generated no user iterable. Stage 0 extends it.
- **Generator:** stage 0 @ 9fce3ecc4 (tip 907aad013 adds the scorecard baseline, no test code), three facts of `OptionalMembershipDifferentialTests`, 3 passed (10.1 min; the longest fact 3 m 56 s).
  - The membership matrix: P92's six axes plus the container kinds `IterDunder`, `GenericIterDunder`, `GetItemLen` (`RuledContainerKinds`: R-NZ, R-OA; red when the spec gains the row, until it moves to `SpecRowMap`) and the observation {value, order}: 31 payloads, 50024 cases (35552 compared, 14472 hand-expected). `OptionalNeedle_EveryContainerOperatorPosition_MatchesCPython` runs the 29960 value cases, `Membership_EveryContainerOperatorPosition_EvaluatesItsOperandsLeftToRight` the 20064 order cases. A chain link expects its expression twin's expectation; `GetItemLen` expects SPY0320. Known red 23146: `Order/*` 14888, `UserIterable/*` 2640, `ChainLink/*` 4682, `GetItemLen/NoneNeedle` 24; kept: 21 container rows 378 (#2674), `CounterOfOptional/*` 534 (#2651).
  - `OperatorSequences_OverEffectfulOperands_AreTheirDesugaring`: payload × form (5) × {every spelled `BinaryOperator`: 25 of 27; every ordered pair of `ComparisonOperator` links: 100}, plus pivot types on either side in the plain form; operand types closed against `OperatorLoweringKind` by reflection (27 recorded, 9 excluded with reasons). An expression over traced operands must print what its desugaring prints and be refused by a code its desugaring is refused by. 32 payloads, 29600 sequences; 4707 run in both forms, 19729 are refused in both. Known red 6505: `Sequence/Binary` 257, `Sequence/Chain` 6248.
  - Excluded axes, each with its reason in the test: the literal-`None` needle has no order observation; `@` and `|>`; a `Self` payload's sequences are generated, a constrained type parameter only in the sequence fact. Not enumerable, hand fixtures: the operand SPELLING after `is` (D4) and a literal-only needle (D2).
  - Turned green (inferred from each D against the verdicts measured at stage 0; no product change was applied): D1 `UserIterable/*` and 18 `GetItemLen` cells; D2 `Order/*` (with D1 and D3 for its user-iterable and chain cells), `Sequence/Binary`; D3 `ChainLink/*`, `Sequence/Chain`. Cells whose value or expression twin is red for #2651 / #2674 stay, re-pinned and cited to that issue alone (T7 measures the count).
- **Owner rulings:** R-NZ (#2653 comment 6826244591: "accept as CPython, record it as a ruling"); R-OA (comment 6826280413: "maybe let's say no on __getitem__ with __len__"); R-OE (charter §4: one group on P92's generator); chain links, unlettered until the next batching call (#2648 comment 6865267508, 2026-11-13: "Refuse, as the twin (Recommended)").

## Adversarial Review (pre-mortem)

- **Alternative root cause:** `ProtocolMembership.Has(__contains__)` could answer yes for a class that iterates. Rejected: its other readers ask whether the dunder exists, and R-OA needs the no.
- **Alternative layer:** a Core helper that takes the needle first for every `in` (P92's shape) costs a delegate call per test and moves every snapshot; a synthesized `Contains` on each iterable class changes its public C# surface (Axiom 1) where LINQ already answers.
- **How the fix could be inert:** the checker accepts an `in` link and no validator visits it (12 chains reach Roslyn today); the binary arm is fixed and the link keeps its own copy (D3 leaves one).
- **Before/after by direction:** D1 and D2 refuse nothing new. D3: SPY0519 → the twin's value or code; **worked → refused** for a chain whose `==` / `!=` link the binary form refuses (class without `__eq__`, interface, generic class, delegate, union: 36 generator cells print CPython's answer today): the owner ruled it refused, as the twin (Owner rulings).
- **Blast radius:** Compiler.Tests (fixtures, Differential, GapDiscovery, `SinkPushingTotalityTests`, `OperatorLoweringCorpusCensusTests`, the Pretty / unparse round trips for the new AST field), Stdlib.Tests, Cli.Tests, LSP `FrontEndParityTests`.
- **Generated artifacts touched:** spy-stdlib and spy-test C# (D2), stdlib docs, the oracle ledger (D5): regenerated at T1 and after T6.

## Design Decisions

- **D1. A class that iterates answers `in` (R-NZ); the legacy sequence pair does not (R-OA).** Rung 3: the runtime body is LINQ's `Enumerable.Contains` over the emitted `IEnumerable<T>`; only the acceptance is new, a checker fact.
  - `ProtocolMembership` gains one question, `AnswersMembership(type)`: `Has(type, __contains__)`, or a user-declared host (plain or constructed generic) whose base chain declares `__iter__` (`HasDunderInChain`). `ProtocolValidator.ValidateMembership` asks it; SPY0320 stays for a host with neither.
  - `ResolveMembershipElementType` (`TypeChecker.Expressions.Operators.cs:1780`): after the `__contains__` arms, the iterate arm (`:1843`, `GenericType` only today) takes every user host with `__iter__`, and no user host's slot comes from `__getitem__`.
  - `ClassifyMembership` is unchanged: a value-type `T | None` needle into a user iterable of `T` is lifted like any equality scan.
  - **Predicate:** `1 in IterBag(1, 2)` and `1 in GIterBag[int]([1, 2])` print `True`; `1 in SeqBag(1, 2)` is SPY0320; `None` into a generic `SeqBag[T]` is SPY0320, not SPY0222.
- **D2. The left operand is evaluated first (#2645).** Membership: CodeGen, the emitter's own splice order (no type decision; the precedent is #1719's reflected comparison, `:622`). String repeat: rung 2.
  - One emitter helper binds the left operand with `is var` before a form that reads its right operand first: `x is var t && y.Contains(t)`, negated whole for `not in`. `GenerateMembershipTest` (`:345`) and the reflected comparison use it; D3's membership link is the same call.
  - Not bound: a literal-only left operand (a literal, or operators over literals: it has no evaluation, and `(1 + 1) in xs` over `list[uint64]` converts only as a C# constant), or two trivial operands (`IsTrivialExpression`). A bare name is bound when the container is not trivial.
  - `StringRepeatStrRight` (`:260`) emits a Core overload that takes the count first, `StringHelpers.Repeat(count, str)` for `int` and `long`, beside the existing `(str, count)` pair.
  - **Predicate:** no emission in `RoslynEmitter.Expressions.Operators.cs` places the right operand's syntax before a non-trivial left operand's, outside the helper.
- **D3. A chain link is its binary twin (#2648).** Semantic and CodeGen; no lower rung holds a chain.
  - Checker: the part of `CheckBinaryOp` that decides a comparison, identity or membership operator becomes one link routine, called by the binary site and by each link of `CheckComparisonChain` (`:1387`). It returns the link's facts: `ClassifyMembership`, `ClassifyComparisonLowering`, `ClassifyNoneTest`, `ClassifyEnumIdentity`, the void-comparand gate. The binary site records them as today; the chain in its `ComparisonLinkLowering` (`_comparisonChainLowerings` is in `MergeFrom`).
  - Validators: `OperatorValidator` and `ProtocolValidator` validate an operator site (operator, left, right); `VisitBinaryOp` passes one and a new `VisitComparisonChain` passes each link.
  - Emitter: `BuildLink` in `GenerateComparisonChain` (`:728`) emits through the binary arm's emitters (`GenerateMembershipTest`, `GenerateNoneTest`, `GenerateIdentityComparison`, `GenerateLoweredComparison`), which take the recorded kind and the operand nodes instead of a `BinaryOp`. `MapComparisonOperator` loses its `SyntaxKind.None` arm and the chain its SPY0519.
  - **Ruled (owner, 2026-11-13, #2648 comment 6865267508: "Refuse, as the twin (Recommended)"):** a link the binary form refuses is refused with the same code, so `a == c == a` over a class without `__eq__` becomes SPY0402 as `a == c` is: a deliberate worked → refused change (contract §3), listed by direction at T1.
  - P82 tranche 4 moves `_comparisonChainLowerings` to the IR after this lands and reads this shape.
  - **Predicate:** `CheckBinaryOp` and `CheckComparisonChain` call the link routine and no classifier of their own; `CodeGen/` holds no `UnsupportedOperator` for a chain.
- **D4. A chain head spelled like a type.** Parser and Semantic. When a comparison operator follows a `TypeCheck` head that has an `IdentityOperand`, `ParseComparison` builds the chain over `[Value, IdentityOperand, …]` with `Is` first and records the head's `TypeAnnotation` on the chain (a new `ComparisonChain` field). The link routine gives link 0 `CheckTypeCheck`'s rule (`:2487`), shared: an enum member is an identity link, anything else SPY0349 with the same steer. A head with no dotted-name reading stays as it is. #2681 (P16) later replaces the spelling rule at both sites.
  - **Predicate:** `c is Color.RED is d` prints `True` for three `RED`; `x is Dog is d` is SPY0349.
- **D5. Spec and deviation.** `docs/language_specification/membership_operators.md`: a Container kinds row for a class with `__iter__` and no `__contains__` (slot: the iterator's element type); the Validation paragraph and the *Implementation* note corrected (needle first). `docs/language_specification/comparison_chaining.md`: `in` and `is` links, with examples that differ under the bug. `docs/deviations.yaml`: a row for R-OA with the owner's reason. The generator's R-NZ entry moves to `SpecRowMap`.

## Implementation

### Phase 1: seams, lowering, spec

**Goal:** the six P93 row families drain to their #2651 / #2674 cells.

**Acceptance (class-level, measurable):**
- Matrix/sweep that goes green: the three facts of `OptionalMembershipDifferentialTests`, with `Sequence/*` gone and `UserIterable/*`, `ChainLink/*`, `Order/*`, `GetItemLen/NoneNeedle` holding only cells cited to #2651 or #2674.
- Guard mutation:
  - M1: `AnswersMembership` ignores `__iter__` → `UserIterable/*` red (SPY0320). Its twin: it also accepts `__getitem__` → the `GetItemLen` cells red (they stop being refused); the positive control is every `GetItemLen` cell refused SPY0320 at the restored tree.
  - M2: the membership bind removed → `Order/*` and the `in` cells of `Sequence/Binary` red; the count-first overload reverted → its `*` cells red; a literal-only needle bound → the D2 fixture red (SPY0908).
  - M3: `VisitComparisonChain` removed from both validators → the `Sequence/Chain` cells whose desugaring is SPY0402 or SPY0320 red; an `is` link sent to `GenerateLoweredComparison` → the identity chains red.
  - M4: the parser drops the head's identity operand again → the D4 fixture red.
  - M5: a slot from `__getitem__` restored → the 18 `GetItemLen` literal-`None` cells red (SPY0222).
- Allowlist entries that drain: `Sequence/Binary`, `Sequence/Chain` whole; the others to their twin-red cells; `scorecard_metrics.py --lower`.
- Execution evidence the close-out will cite: the three facts @ sha, the D5 spec blocks (`check_spec_blocks.sh`), the whole-solution gate.

#### Tasks

- **T1. Probe and regenerate** — each D's predicate by `run` at the base; spy-stdlib, spy-test, stdlib-docs and oracle-ledger regeneration (§7); the spy-stdlib sites D2 will move, counted.
  - Acceptance: base behaviour recorded @ sha; the chains D3's ruling turns from working to refused listed by direction (contract §3).
- **T2. Parser** (D4) — `Parser.Expressions.cs`, `Parser/Ast/Expression.cs`, the Pretty and dump visitors of `ComparisonChain`.
  - Acceptance: M4's fixture parses to a three-operand chain; the round-trip tests pass.
- **T3. Checker** (D1, D3, D4) — `TypeChecker.Expressions.Operators.cs`, `ProtocolMembership.cs`.
  - Acceptance: M5; the `Semantic` tests of the link routine.
- **T4. Validators** (D1, D3) — `ProtocolValidator.cs`, `OperatorValidator.cs`.
  - Acceptance: M1, M3 (validator half).
- **T5. Core** (D2) — `src/Sharpy.Core/StringHelpers.cs` (C# 9 safe) and its Core.Tests.
- **T6. Emitter** (D2, D3) — `RoslynEmitter.Expressions.Operators.cs`; sink rows for the new `is var` sites; regeneration again.
  - Acceptance: M2, M3 (emitter half).
- **T7. Spec, deviation, fixtures, drain** (D5) — the spec edits, the `deviations.yaml` row, the fixtures below, the `SpecRowMap` move, the allowlist re-pinned and re-cited, `--lower`.
  - Acceptance: the Phase 1 sweep bullet; the leftover count measured.

## Testing Strategy

- Stage 0 is the matrix. Fixtures under `operators/`: a user iterable (plain, generic, inherited `__iter__`, a `T | None` needle) with an `.error` twin for the `__getitem__` + `__len__` class; operand order (`in`, `not in`, `int * str`, a literal-only needle into `list[uint64]` that must keep running); chain links (`in`, `not in`, `is`, `is not`, a refused link's code, `c is Color.RED is d`).
- Outputs discriminate: each order fixture prints the trace, so it differs under the bug.
- Spec: every new example of D5 runs under `check_spec_blocks.sh`.
- Regeneration at T1 and after T6; the gate is the whole solution.

## Issues to Close

- #2645 — evaluation order — closed by T5, T6; **close criterion:** `Sequence/Binary` gone and no `Order/*` cell cited to #2645 @ sha, the gate.
- #2648 — chain links — closed by T2–T6; **close criterion:** `Sequence/Chain` gone, no `ChainLink/*` cell cited to #2648, the D4 fixture @ sha.
- #2653 — user iterables — closed by T3, T4, T7; **close criterion:** no `UserIterable/*` or `GetItemLen` cell cited to #2653, the spec row and the deviation row @ sha.

## Class trackers this plan may post cells to

- #2645, #2648, #2653 — this contract
- #2644 — conditional and walrus needles; ordering on `T | None`
- #2651 — `Counter[T | None]`
- #2674 — value-ness
- #2680 — enum comparison
- #2681 — `x is A.B` decided by spelling

## Revision log

- @ 907aad013 — revised before the first pass: the owner's two rulings of 2026-11-13 recorded. D3's unruled question is decided (a chain link is refused as its binary twin is; #2648 comment 6865267508) and stage 0's scorecard baseline is raised 541 → 550 (#2645 comment 6865268833). No decision, predicate, mutation or count changed.
