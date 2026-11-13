<!-- Verified by /verify-plan on 2026-11-13 @ 7cf933cec -->
<!-- Verification result: NEEDS REVISION -->
<!-- Adequacy: CELL -->
<!-- Scope: ONE-GROUP · Generator: built-first -->

# Membership operator totality: user iterables, chain links, operand order

## Context

Three class trackers, one group (charter §3.20w): #2645 `f() in g()` evaluates the container first; #2648 a chain refuses an `in` or `is` link at code generation; #2653 `in` on a class that only iterates is refused.

## Current State

Product @ 7be6704c4 (`run`); oracle CPython 3.15.2; stage 0 @ 9fce3ecc4, its scorecard baseline @ 907aad013.

- **Order.** `n() in c()` prints `container`, `needle` for every plain-path container (list, tuple, dict, set, range, `str`, `bytes`, a `__contains__` class); `not in` too; a lifted `T | None` needle goes first. `n() * s()` with the `str` on the right also evaluates right first. Of 38 expressions probed, 11 evaluate the right operand first (10 membership, `int * str`) and 27 agree; then the generator.
- **Chain.** `x == x in [a, c]`, `c is d is e`, `a < x is not None`: SPY0519 from `RoslynEmitter.Expressions.Operators.cs:784`. The checker's chain site runs a subset of the binary site's checks and both validators visit `BinaryOp` only: `x == x in k` (`k` no container) is SPY0519 where the binary twin is SPY0320; `a == c == a` over a class without `__eq__` compiles (prints `False`, as CPython) where `a == c` is SPY0402; over a struct it reaches Roslyn (SPY0908). `c is Color.RED is d` is SPY0349: `Parser.Expressions.cs:556` drops the head's identity operand.
- **User iterables.** A class with `__iter__` and no `__contains__` (plain or generic) is SPY0320 (`ProtocolValidator.cs:201`) while `for x in b` and `1 in list(b)` run; it emits as `IEnumerable<T>` with `System.Linq` imported. A class with only `__len__` + `__getitem__` is SPY0320 (and SPY0320 under `for`); its generic form also resolves a needle slot, so a mismatched needle draws SPY0222.
- **Generator.** Membership matrix: 50024 cases, 23146 divergent in 30 rows. Sequence matrix: 29600 sequences, 6505 divergent in 2 rows. 32 allowlist rows (23 before).

## Scope

- **Group:** P93 — membership operator totality (one non-chore group; no chores)
- **Tier:** full (c: the seam is the `in` lowering, under `Semantic/` and `CodeGen/`) — from charter §3.20w (contract §12)
- **Alone:** yes — D2 changes the emitted form of `in` where an operand has an evaluation (0 of 178 `.expected.cs` snapshots: 3 hold `.Contains(`, each with a literal or a name; spy-stdlib C# is measured at T1)
- **Gates:** 1
- **Filing:** cells outside the contract go to the findings ledger; the lead files by class (contract §10); trackers named at the end of this plan
- **Budget:** verify cost: the three facts (about 11 min locally) and one whole-solution gate; the sibling-cell prober: the generator's matrix plus at most two axis changes (contract §10)
- **Override:** none
- **Scorecard baseline:** `allowlist_rows_outside_seed` 541 → 550 for stage 0's nine rows, raised @ 907aad013 (contract §11) — owner, 2026-11-13, #2645 comment 6865268833: "Raise baseline to 550 (Recommended)"; the allowlist is not registered as a seed harness

## Defect Class

- **Violated contract:** `x in c` is answered for every container the iteration ring accepts, in every position a comparison may stand, a chain link included; a chain link gets its binary twin's verdict; every binary operator evaluates its left operand first.
- **Meta-class:** Position/route missing a shared check (the chain site, the user-class arm) and silent wrong behavior (order) → **standard cure:** the shared seam, and a generator over the sibling cells.
- **Known member cells:** #2645, #2648, #2653 and their comments.
- **Sibling cells this plan must also cover:** none; see Generator.
- **Lane / reach:** silent-wrong · program (#2645); false-refusal · probe (#2648); false-refusal · program (#2653). **Fidelity-scope row:** COMPARED ("value of an expression"; "which programs run vs. are refused": `expressions.md` › Expression Evaluation Order, `operator_precedence.md` › Comparison chaining).
- **Standing harness that should have caught it:** `OptionalMembershipDifferentialTests` (P92): its chain cells expected SPY0519, it observed no order and generated no user iterable. Stage 0 extends it.
- **Generator:** stage 0 @ 9fce3ecc4 (tip 907aad013 adds the scorecard baseline, no test code), three facts of `OptionalMembershipDifferentialTests`, 3 passed / 0 failed / 0 skipped @ 7cf933cec (measured: 11.1 min locally, the facts 3 m 5 s, 4 m 6 s, 3 m 46 s; on CI run 43510923384, at the PR merge ref of 7cf933cec, 2 m 43 s, 3 m 49 s, 3 m 15 s).
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
- **D4. A chain head spelled like a type.** Parser and Semantic. When a comparison operator follows a `TypeCheck` head that has an `IdentityOperand`, `ParseComparison` builds the chain over `[Value, IdentityOperand, …]` with `Is` first and records the head's `CheckType` (its `TypeAnnotation`) on the chain (a new `ComparisonChain` field). The link routine gives link 0 `CheckTypeCheck`'s rule (`:2487`), shared: an enum member is an identity link, anything else SPY0349 with the same steer. A head with no dotted-name reading stays as it is. #2681 (P16) later replaces the spelling rule at both sites.
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
- Regeneration at T1 and after T6; the gate is the whole solution: `/ci-gate` at the pushed tip, the local run of contract §6 as its fallback, counts `@ sha (measured)`, a red attributed only after a control run at the base.

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

## Verification Summary

**Result:** NEEDS REVISION (pass 1 of 2; Dimensions 4, 6 and 7)
**Adequacy:** CELL
**Verified on:** 2026-11-13 @ 7cf933cec (product code = `origin/dev` @ 7be6704c4; every probe by `sharpyc run` on a build of this sha; oracle CPython 3.15.2)
**Plan file:** `docs/process/plans/plan-67a01f.md` (committed at every result, NEEDS REVISION included)

Probe programs and logs are in the session scratchpad under `p93/verify1/` (`pr/*.spy`, `cs/Program.cs`, `mut1-run.log`, `mut2-run.log`, `green-run.log`).

### Findings the revision answers

1. **Error (Dimensions 6, 7; a new stage-0 commit): the chain-link rule of the membership matrix contradicts the chain-link ruling.** `Expect` gives a `ChainLink` cell the expectation of its EXPRESSION twin (`x in c`) and ignores the head link. The head is `x == x`, and the binary `x == x` is SPY0402 for nine payloads: `K`, `SP`, `IFace`, `U`, `D`, `GK[int]`, `GS[int]`, `Self@SK`, `Self@SS` (`pr/eq_*.spy`). Under D3 as the owner ruled it, `x == x in [a, c]` over `K` is SPY0402; the cell `K/List/Plain/in/ChainLink` expects CPython's values. 1224 known-red cells (612 by value, 612 by order; 1152 expect values, 72 expect SPY0320) keep diverging after the plan, for a reason neither #2651 nor #2674 names; up to 252 cells that pass today (the fixed-slot containers, which expect exactly SPY0222) may gain a second code. So `Turned green: D3 ChainLink/*`, the Phase 1 sweep bullet and #2648's close criterion cannot hold as written. The rule has to be keyed on every link's twin (the head's verdict measured per payload and form, as the sequence fact measures its desugaring).
2. **Error (Dimensions 4, 6): D2's `is var` form cannot bind a left operand that C# types only against its slot, and the left operand's FORM is not an axis of the generator.** Each program below runs today; the emitted C# was read with `emit csharp`, and the `is var` form of the same needle was compiled with the .NET 10 SDK (`cs/Program.cs`):
   - `(1, 2) in g()` over `list[tuple[uint64, uint64]]` prints `True` (`G().Contains((1, 2))`); `(1, 2) is var t && G().Contains(t)` is CS1503.
   - `(f(), 2) in g()` over `list[tuple[int, uint64]]` prints `container`, `needle` (CPython: `needle`, `container`): a cell of #2645 that D2 must reorder; its `is var` form is CS1503.
   - `(f() if b else None) in g()` over `list[int | None]` prints `container`, `needle`; `(b ? F() : null) is var t` is CS0173.
   - `f1 in g()` (a function name: a bare `Identifier`, which D2 binds when the container is not trivial) and `(lambda v: v) in g()` over `list[(int) -> int]` run; both `is var` forms are CS0837.

   Each would go from running to SPY0908, and the second and third cannot be fixed by widening the exemption. The binding needs the slot type the checker already resolves, recorded for the emitter (Rule 2), or another form. Besides: `x in [(x := 5), 2]` prints `True` where CPython prints `False`, and `y not in [(y := 5), 2]` prints `False` where CPython prints `True` (`pr/w1.spy`). That is a wrong value inside this contract. D2's bare-name arm covers it, and no generated cell or fixture reaches it: a needle is a parameter name by value and a traced call by order.
3. **Error (Dimension 4): D1's predicate and its "no emitter change".**
   - A class with `__iter__` and a method `contains(self, v: int) -> bool` emits that method as `Contains(int)`. `1 in b` is SPY0320 today (`pr/it_m2.spy`). With D1, `b.Contains(1)` binds the user's method, not LINQ's: `cs/Program.cs` prints `False` for it and `True` for `Enumerable.Contains(b, 1)`; CPython prints `True`. Refused becomes silently wrong. D1 needs an emission that names LINQ, so a recorded lowering and an emitter arm.
   - `__iter__(self) -> list[int]` (the shape of #1915) emits a class with no `IEnumerable<T>` base (`pr/it_l.spy`). `1 in LB()` is SPY0320 today (`pr/it_l2.spy`); `HasDunderInChain(__iter__)` accepts it and `LB().Contains(1)` is CS1929: refused becomes SPY0908. The predicate should be the iteration ring's own (the producer gate of `IterableElementDecider`, the synthesized `IEnumerable[T]`), which is what the contract sentence says.
4. **Warning (Dimension 6; a stage-0 commit): the sequence fact inherits `ExcludedPrimitives`.** Its reasons are about a needle slot and the python twin; the sequence fact has neither. `o == p == o` over `object` is #2648's own cell (comment 6839978794; it compiles and runs today, the binary form is SPY0402) and no sequence generates it. `SequenceOnlyPayloads` gains `object` and `char`, or the fact states its own reason for each.
5. **Warning (Dimension 6): excluded axes the plan does not name.** Chain length (two links only: a middle link, with both operands captured, needs three); a hoisting operand (comprehension, spread, `?`, walrus), which reaches the second, imperative path of `GenerateComparisonChain`; `BinaryOpLowering`, the second enumeration `GenerateLoweredComparison` dispatches on (the census reads `_operatorLowerings` only); the sequence position (an assignment's right side only).
6. **Warning (Dimension 7): by direction, D3 also turns refused into ICE.** 456 sequences are SPY0519 today while their desugaring reaches Roslyn (SPY0908). With D3 they reach Roslyn too, and the fact counts them as agreeing (424 sequences are SPY0908 in both forms today). T1's by-direction list has to carry them, each with the tracker that owns its twin; see the owner question below.

### Corrections Made
- Current State › Order: "The other operators hold (38 probed, then the generator)" → "Of 38 expressions probed, 11 evaluate the right operand first (10 membership, `int * str`) and 27 agree" (`probes/ord3.spy` against `ord3.py`, re-run).
- Defect Class › Generator: "3 passed (10.1 min; the longest fact 3 m 56 s)" → the measured run @ 7cf933cec (11.1 min; 3 m 5 s, 4 m 6 s, 3 m 46 s) and the CI run 43510923384 (2 m 43 s, 3 m 49 s, 3 m 15 s). The commit body of 9fce3ecc4 says 10.3 min (3 m 16 s, 2 m 49 s, 4 m 5 s).
- Scope: a **Budget** line added (Dimension 9: verify cost and the prober's budget were not stated).
- D4: "the head's `TypeAnnotation`" → "the head's `CheckType` (its `TypeAnnotation`)", the property's name in `Parser/Ast/Expression.cs`.
- Testing Strategy: the gate named (`/ci-gate`, its contract §6 fallback, counts with their sha, a control run for a red).

### Adequacy
- **Contract:** named — the Defect Class sentence; its last clause ("every binary operator evaluates its left operand first") is #2645's own contract and the spec's rule (`expressions.md` › Expression Evaluation Order), and it is wider than what the generator shows (finding 2).
- **Axes:** open: left-operand form ← the `Expression` node records of `Parser/Ast/Expression.cs`, by reflection as `SemanticTypeCensus` does, each generated as a left operand or excluded with its reason (finding 2). The other axes are images of compiler enumerations or closed literal sets, and the totality assertions cover their maps. The expected map: the `ChainLink` rule is not keyed on the head link's twin (finding 1).
- **Cure:** seam + generator — Position/route missing a shared check; silent wrong behavior.
- **Harness:** named (`OptionalMembershipDifferentialTests`, stage 0); the allowlist delta is misstated for `ChainLink/*`, `UserIterable/*` and `Order/*` (finding 1).
- **Layer:** every decision names its rung; D1's predicate is keyed on the dunder where the ring's own fact is available (finding 3).

### Scope & convergence
- **Group:** one (P93; no chores) · **Alone:** yes — required? yes once D2 moves spy-stdlib C# (counted at T1, not before); the charter row does not carry the mark · **Gates:** 1
- **Generator:** stage 0 @ 9fce3ecc4 (baseline 907aad013), both mutations re-run by `cp` and restored `cmp`-identical:
  - Path rule, `git diff --name-only 7be6704c4..907aad013 -- ':!docs/process/plans/'`: `build_tools/scorecard-baseline.json`, the allowlist, the test, `src/Sharpy.TestInfrastructure/Integration/IntegrationTestBase.cs`. All test code or the baseline. The `MaxErrors` hook passes the existing `CompilerOptions.MaxErrors` (the CLI's `--max-errors`), default 0 as before: no product path.
  - Totality broken (the generator skips the chain-link cells of `GetItemLen` and the chains whose first link is `is`): 3 failed in 15 s ("made 49416 cells; the axes give 50024", "made 28000 sequences; the axes give 29600").
  - A passing case listed as known-red (a new row `Set/Plain` pinning `str/Set/Plain/not in/IfCondition`; the bulk count of `Order/NoneNeedle` raised by one; `int64/Plain/-` pinned in `Sequence/Binary`): 3 failed in 10.5 min ("ROW Set/Plain: no cell diverges any more", "removed … it now meets its expectation", "bulk header (cells=6807 …) does not match its 6806 divergent cells").
  - Restored: 3 passed / 0 failed / 0 skipped @ 7cf933cec, each proposed allowlist byte-identical to the committed one. Counts as the plan cites them: 50024 cases (35552 compared, 14472 hand-expected), 23146 divergent in 30 rows; 29600 sequences, 4707 run in both forms, 19729 refused in both, 6505 divergent in 2 rows; 32 allowlist rows; 27 lowering kinds recorded, 9 excluded.
- **Trackers named:** #2645, #2648, #2653, #2644, #2651, #2674, #2680, #2681 (all open) · **Fidelity rows:** named (COMPARED); R-OA's deviation row is D5's.

### Falsifiability
- Stage-0 totality assertions: mutation re-run, red; exemption not subject; positive controls in the test.
- Stage-0 allowlist pin (rows with pin lines and `bulk` rows): mutation re-run, red. A `bulk` row checks a count and a SHA-256 prefix over the sorted `cell -> verdict` lines, so a cell that passes, starts diverging or changes its verdict is red. What it loses is the review of a re-pin (two numbers change, not lines), and `ProposedAllowlist` keeps the mark on a kept row (`old?.Bulk ?? count > 400`), so a drained residue stays digest-only.
- M1 (`AnswersMembership`): mutation named, twin named; positive control yes. It does not cover finding 3.
- M2 (the membership bind, the count-first overload, the literal-only needle): mutations named. MISSING — expected: "a bare name is left unbound when the container is not trivial" → a fixture `x in [(x := 5), 2]` that prints `False` (it prints `True` today, so it discriminates).
- M3: named, but one mutation removes `VisitComparisonChain` from both validators; expected: one per validator (`OperatorValidator` → the SPY0402 cells, `ProtocolValidator` → the SPY0320 cells), or one half can be vacuous.
- M4 (the parser), M5 (the `__getitem__` slot): named. D4's fixture lacks a dotted TYPE head (`x is Outer.Inner is d`, SPY0349 today) and `c is Color.RED == d` (SPY0349 and SPY0222 today, CPython `True`).
- By-direction probe: specified for D3's 36 `==` / `!=` chains (re-measured: 36, all SPY0402); MISSING for refused → ICE (finding 6) and for D1 and D2 (findings 2, 3).

### Warnings
- The sequence fact has no oracle, by design. It cannot see: a defect its desugaring shares (#2686; more generally how often and whether the RIGHT operand is evaluated, since only the left is bound); a value; a sequence both forms of which are refused by one code, SPY0908 included; any operand that is not a traced call.
- D4 makes the head's identity operand a chain operand, so a child node. `TypeCheck.IdentityOperand` is kept out of `GetChildNodes` so that no generic walker meets an expression nothing type-checked; D4 has to say how a refused head (a dotted type name) keeps that.
- D3: the chain site holds logic a binary site has no twin for: an operand read by two links (`noneSlots`, a slot cast recorded only when both readers agree) and a needle typed against the NEXT operand's element type. Read @ 7cf933cec; the link routine must keep it.
- Stage 0 extended P92's allowlist where contract §11 says "a new `<Test>-allowlist.txt`", and raised the baseline one commit later (the scorecard check was red at 9fce3ecc4 and 2fa5ace8b). R-OE and the charter's call-43 note ("ONE generator") support the shared file; the commit that drains P93's last row then deletes no file.
- `Alone: yes` is stricter than the charter row, which carries no mark; the row gains it at the next batching call. The chain-link ruling is cited by comment id until it has its letter.
- Blast radius does not name the warm/cold harness; the new `ComparisonChain` field reaches whatever compares or caches the AST.

### Missing Steps Added
- T1: the by-direction list also carries SPY0519 → SPY0908 (456 sequences at stage 0) and whatever D1 and D2 move from refused or running to SPY0908.
- T7: delete the `IterDunder` and `GenericIterDunder` entries of `OptionalElementControlExcluded` (the value fact is red until then); no row keeps the `bulk` mark after the drain, so the residue shows its cells.
- Blast radius: the warm/cold harness, or the reason it is not reached.

### Unchecked Claims
- The spy-stdlib sites D2 moves: not counted (T1). Snapshots re-measured: 178 files, 3 with `.Contains(`, none with a needle D2 binds; one snapshot holds `__refl_` and stays only if the shared helper keeps that spelling.
- D3's single link routine, D4's parser shape, the `BuildLink` dispatch: read against the code, not prototyped. `_comparisonChainLowerings` is in `MergeFrom` (read). P82 tranche 4 has not landed (the emitter still reads `GetComparisonChainLowering`).
- The 252 cells of finding 1 that pass today: whether the validator's SPY0402 is reported in the compile round that removes them was not measured.
- `check_plan_refs.py --issues` could not look issues up here (`gh issue view` is refused); states read with `gh api`: #2644, #2645, #2648, #2651, #2653, #2674, #2680, #2681 open; #1719 closed (cited as a precedent only).
- **Owner questions** (each a decision the plan cannot make):
  1. A chain link whose binary twin reaches Roslyn (finding 6): (a) the link reaches Roslyn as its twin does and the cell belongs to the twin's tracker; (b) the chain keeps a refusal of its own there. Recommended: (a), listed by direction, so there is one seam.
  2. `bulk` allowlist rows (count and digest, no pin lines) as a form of contract §11's known-red row: (a) accept; (b) accept while a row's cells share one cause, with pin lines for any residue; (c) pin lines always. Recommended: (b).
  3. A stage 0 that extends an existing generator: (a) it extends that generator's allowlist, as here; (b) a file of its own. Recommended: (a), written into contract §11.
