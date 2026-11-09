<!-- Verified by /verify-plan on 2026-11-08 @ b5bd25495 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: existing (axis 8, extended first in Phase 1) -->

# P85 — `__init__` top-level augmented store (#2439)

## Context

An augmented store `t op= v` means `t = t op v` at every position; directly in `__init__`'s body, `self.<m> op= v` is emitted as `this.M = v` (read and operator dropped), for every operator and every host with an explicit `__init__`. Class tracker #2439 (lane:silent-wrong · reach:program). Charter `docs/process/rounds/remediation-round-2026-09-20-batching.md` §1 row 6c⁶⁷‴, §3.20p; ruling R-LL (a): https://github.com/antonsynd/sharpy/issues/2439#issuecomment-6724244460.

## Current State

Measured with `sharpyc run` @ b5bd25495 (HEAD build; python 3.13 reference; probes in `<scratchpad>/lanes/p85/`). "Top" = directly in `__init__`; "nested" = the same statements under `if True:`.

| cell | top (Sharpy) | nested / method body (Sharpy) | python |
|---|---|---|---|
| 12 scalar ops `+= -= *= /= //= %= **= &= \|= ^= <<= >>=` + `str +=` + `list +=` on `self.<field>`, **class** | `7 7 7 8.0 7 7 5 10 3 5 2 3 cd [2, 3]` | `27 13 140 2.5 2 6 32 8 15 9 12 8 abcd [1, 2, 3]` | `27 13 140 2.5 2 6 32 8 15 9 12 8 abcd [1, 2, 3]` |
| same, **struct** and **@dataclass** (explicit `__init__`) | identical wrong line | n/a | same |
| issue row 3 (`self.y = 2; self.x -= 1; self.y *= 7`) | `1` / `7` | `2` / `14` | `2` / `14` |
| `Classify`'s 7 collection arms: list `+=`, set `\|= &= -= ^=`, dict `\|=` | `[2] [3] [2, 3] [2] [2, 3] {'m': 2}` | python's | `[1, 2] [1, 2, 3] [2] [1] [1, 3] {'k': 1, 'm': 2}` |
| list `*= 2` on `self.b` | **ICE** SPY0908 CS0029 (`this.B = 2`) | `[1, 2, 1, 2]` | `[1, 2, 1, 2]` |
| property: `property p` `+= 9`, `property get q` `*= 4` | `9 4` | `12 20` | `12 20` |
| `self.o ??= 5; self.p ??= 7` (`int \| None`, o=None, p=3) | `5 7` | `5 3` | n/a (Sharpy-only) |
| event: `self.pinged += on_ping; self.pinged += on_pong` | `pong` | `ping` / `pong` | n/a (.NET event) |
| gated `self.v @= Vec(4)` (`matmul` feature) | `4` | — | `13` |
| `@static x` through `Box.x += 9` | `12` ✓ | — | `12` |
| `self.xs[0] += 9; self.d["a"] *= 7` (index) | `[10, 2] {'a': 7}` ✓ | — | same |
| `self.g.x += 9` (deeper receiver) | `12` ✓ | — | `12` |
| `self.a, self.b += 1` (tuple) | SPY0225 ✓ | — | SyntaxError |
| `@static x` through `self.x += 9` | **ICE** SPY0908 CS0176 | method: `12` (#2435) | `3` |

Emitted C#: event cell `this.Pinged = …OnPing; this.Pinged = …OnPong;`; `??=` cell `this.O = 5; this.P = 7;`; property cell `this.P = 9; this.Q = 4;`.

**Where top-level `__init__` statements go.** Every host with an explicit `__init__` reaches `GenerateConstructor` through one call (`RoslynEmitter.ClassMembers.cs:309`; the synthesized dataclass constructor at `:299` runs only without `__init__`). It sends the body through `GenerateSuite` with its own statement generator (`RoslynEmitter.ClassMembers.Constructors.cs:206`): `GenerateConstructorBodyStatement` (`Constructors.cs:132`) intercepts at `:140` any `Assignment` targeting `self.<member>`, for every `Operator`, and builds a `SimpleAssignmentExpression` from `assign.Value` alone; everything else falls through to `GenerateBodyStatements` (`:194`). A nested block is an `IfStatement`, so its inner assignments reach `GenerateAssignment` (`RoslynEmitter.Statements.Assignments.cs:38`) via `CodeGen/RoslynEmitter.Statements.cs:206` — why nesting "fixes" the bug. The lowering pass records `IrCollectionMutation` / `IrOptionalStoreWrap` (`Lowering/LoweringPass.cs:110`, `:124`) at every position; the constructor arm never reads them.

## Scope

- **Group:** P85 — `__init__` top-level augmented store (one group; no chores riding).
- **Tier:** full (c: the cure seam is under `CodeGen/`), from charter §3.20p (contract §12).
- **Alone:** no. No ruled output change: a corpus scan @ b5bd25495 finds **0** top-level `self.<m> op=` statements in `__init__` across `src/`, `docs/`, `build_tools/` `.spy` files and markdown blocks (positive control: 51/51 planted lines found, nested twins skipped), so no `.expected`, `.expected.cs` or spy-stdlib C# moves.
- **Gates:** 1 (the lead's `/ci-gate` on the pushed tip); lands before P82 tranche 2's layout-moving PR, no shared seam (R-LT).
- **Filing:** cells outside the contract go to the lane ledger; the lead files by class (contract §10); trackers at the end.
- **Override:** none.

## Defect Class

- **Violated contract:** an augmented store `t op= v` evaluates `t = t op v` (with the in-place collection mutator, the `??=` absent-guard and the event `+=` subscription where they apply) identically at every statement position; `__init__` top level is the one position where it does not.
- **Meta-class:** silent wrong behavior (plus one ICE cell). **Standard cure:** fix at the seam, then a conformance sweep over the siblings.
- **Known member cells:** #2439's six.
- **Entry routes into the arm** [ADDED: adequacy]: a bare top-level statement, a `@suppress`-wrapped one (unwrapped at `Constructors.cs:136`), one after a `defer` (threaded through `FillSuite`/`GenerateScopeGuard`), one before `super().__init__()` (same `constructorBody` list). D1's predicate sits after all four. The `@suppress` and `defer` cells are live (9 vs 12 @ b5bd25495); `StoreNesting` does not enumerate them, so Phase 2's fixture carries them.
- **Sibling cells this plan covers (the generator, Phase 1).** Each axis is the image of a compiler enumeration, closed by a totality assertion:
  - **host** = `Roster.ImageOf(NameResolutionRosters.HostKinds)` = {class, dataclass, struct}, closed by `HostKindsRoster_IsTotalOverTypeKind`;
  - **position** = `Enum.GetValues<StoreNesting>()` = {Top, InsideIf}, reason `StoreNestingReason`;
  - **target** = the new roster `AugmentedInitTargets`, total over `AssignmentTargetShapes`' keys (the image of `FirstInvalidAssignmentTarget`'s switch) × `AttributeStoreMembers`' keys × the receivers `AttributeStoreBodies` generates at `InitBody` ({SelfDot, TypeName}; Alias, DerivedType, InstanceLocal are pruned there). In: SelfDot×InstanceField, SelfDot×InstanceProperty, TypeName×StaticField, TypeName×StaticProperty, IndexAccess (`self.<list>[0]`), TupleLiteral (refusal). Out, with reasons: SelfDot×StaticField (#2435's cells, R-KU; D3); SelfDot×StaticProperty (#2435's class: ICE CS0176 at `__init__` top and in a method alike @ b5bd25495; no Augmented cell; ledger); consts (`Ice # #2435` Assign cells; `self.K op=` is ICE CS0176 at every position; ledger); methods (SPY0222); NestedType (pruned); Constructor; Identifier (STORE's bare family); StarExpression (parser); event (`ExcludedAxis.EventsSplitPropertiesTupleStarStores`, no python twin → hand fixture);
  - **operator** = the new roster `AugmentedOperators`, total over `Enum.GetValues<AssignmentOperator>()`: 12 scalar rows (slot type, initial, operand, printed value; `/=` on `float`). Out: Assign (the existing form cells), NullCoalesceAssign (no python spelling → hand fixture), MatMulAssign (gated, `GatedConstruct.cs:58` → hand fixture with `.features`). The collection rows are the (operator, list/set/dict) pairs on which `AugmentedCollectionAssignment.Classify` (`Semantic/AugmentedCollectionAssignment.cs:59`) returns non-null, computed by calling it.
  
  **Verdict rule:** TupleLiteral → MustRefuse SPY0225; every other target → MustRun with the operator rows' printed values in row order as the model. The operator axis is packed inside each cell (one slot per row, one `print`); scalar and collection rows are separate cells, so the list `*=` ICE cannot mask the silent-wrong scalar line.
- **Lane / reach:** silent-wrong · program. **Fidelity row:** "Value of an expression; stdout of print" (COMPARED).
- **Standing harness that should have caught it:** the name-resolution differential, axis 8 (`NameResolutionDifferentialTests.Axes.cs:1124`); it caught the `+=` cell (allowlist row `store/attribute/SelfDot/InstanceField/Augmented/InitBody SilentWrong # #2439`) but its product (`Axes.cs:1133`) has no operator, host or nesting axis and its Augmented × InitBody loop covers only `AttributeFormMembers` = {InstanceField, StaticField} (`Axes.cs:1126`).
- **Generator:** EXISTS (charter §3.20p, §6); Phase 1 extends axis 8 before any hand fixture; hand fixtures cover only the three values the generator cannot render in python: `??=`, event `+=`/`-=`, gated `@=`.
- **Owner rulings:** R-LL (a) (linked above); D3 ruled (a).

## Adversarial Review (pre-mortem)

- **Alternative root cause:** a checker that types the top-level store differently. Ruled out: the nested-`if` twin of every cell runs correctly under the same semantic analysis, and the emitted C# drops `this.X op` syntactically.
- **Alternative layer:** none: an emitter dispatch predicate, not a Python behaviour (no ladder rung, no name-keyed rule); the facts the general path needs already exist on the IR and in `SemanticInfo`.
- **How the fix could be inert:** a second interceptor of top-level `__init__` statements. Census @ b5bd25495: `GenerateSuite` (`CodeGen/RoslynEmitter.Statements.cs:37`) has one caller passing a statement generator (`Constructors.cs:206`); the other `Assignment` dispatchers (`CodeGen/RoslynEmitter.Statements.cs:206`, `RoslynEmitter.ModuleClass.cs:981`) call `GenerateAssignment`; the struct auto-constructor and synthesized dataclass constructor emit only declaration defaults. Phase 2's mutation (restore the old predicate) proves the fixture and sweep cells fail without the fix.
- **Before/after by direction:** no program becomes refused; list `*=` and gated `@=` go from ICE/wrong to correct; the `@static`-through-`self` cell goes **ICE (SPY0908 CS0176) → SilentWrong (12, python 3)**, matching its method-body twin — a §3 regression in kind, ruled in D3.
- **Blast radius:** Compiler.Tests only (`NameResolutionDifferentialTests`, the new fixtures; the LSP comment-census and code-action pins a new fixture moves (+1 each) are re-pinned in the landing commit, charter §6). No Stdlib, Core, LSP or interop source changes; `ILCompilesPropertyTests`/`CsCleanPropertyTests` are the regression net.
- **Generated artifacts touched:** none (scan above); anything the `/push` regen staleness gate moves is a finding, not a regen.

## Design Decisions

**D1 — The cure: the constructor arm takes plain `=` only.** At `Constructors.cs:140`, add the conjunct `assign.Operator == AssignmentOperator.Assign`; every augmented operator then falls through `GenerateBodyStatements` to `GenerateAssignment`'s member arm (`RoslynEmitter.Statements.Assignments.cs:335`), the seam method bodies and nested blocks already use, carrying `IrCollectionMutation` (`:337`), native event `+=`/`-=` (`:352`), `??=` via `GenerateNullCoalesceAssignStatement` (`:987`) and every operator via `GenerateAugmentedValue` (`:829`) — all measured correct in the nested/method-body columns. Rule 2 holds: the predicate reads `Assignment.Operator` (AST syntax); no `SemanticInfo` table (pin 85) and no inspection-allowlist kind is touched. The arm's comment says why it is `=`-only, citing #2439.
- *Rejected:* (b) deleting the arm so `=` also takes `GenerateAssignment` — moves `=` emission (parameter/field-name mapping) in every constructor, an emitted-layout change that would make the group alone, outside R-LL; (c) teaching the arm every operator — duplicates `GenerateAssignment`, the parallel-site class (contract §1).

**D2 — Generator first: extend axis 8.** Add the `AugmentedInitTargets` and `AugmentedOperators` rosters (`NameResolutionDifferentialTests.Rosters.cs`, beside `AttributeStoreMembers` at `:1341`). Each has a totality fact in the style of `AttributeStoreRosters_AreTotalOverTheirSources`:
- `AugmentedOperators` over `Enum.GetValues<AssignmentOperator>()`, plus the collection rows equal to `Classify`'s non-null set over operator × {`list[int]`, `set[int]`, `dict[str, int]`};
- `AugmentedInitTargets` over `AssignmentTargetShapes` × `AttributeStoreMembers`.

Add a product `AttributeAugmentedProduct()`: host × position × target × slot family {scalar, collection} at `InitBody`, plus a `MethodBody` control at (class, Top) per target. Add a cell builder that hosts the cell as `class H:` / `struct H:` / `@dataclass class H:`, and a fed-failure check (`AttributeStoreFedFailures`' pattern) so a product tuple without a cell fails the sweep. **Size:** 5 MustRun targets + 1 refusal, with 2 slot families on the two field targets = 8 per (host × position) → 48 cells + 8 method-body controls = **56** (an estimate; the generated count is measured and recorded). `CellBudget` (`NameResolutionDifferentialTests.cs:151`, 8900; pool 8889 @ 87ac3853e per the #2439 drain comment) is raised to the measured pool after Phase 1; the commit body records base pool, generated count and new budget. The budget is a ceiling (`pool.Count > CellBudget`, `:705`) and cannot catch an over-estimate; the fed-failure check catches a missing cell.

**D3 — RULED (a), owner 2026-11-08: the `self.<@static> op=` InitBody row.** After D1, `store/attribute/SelfDot/StaticField/Augmented/InitBody` (allowlist, today `Ice # #2435`) takes the method-body route and prints python's-wrong `12` (`…/MethodBody SilentWrong # #2435`). Options:
- **(a, recommended)** Re-pin the row `Ice` → `SilentWrong`, still citing #2435, lane token `missing-refusal` like its `…/MethodBody` twin, in the fixing commit; InitBody then agrees with MethodBody and P30's R-KU refusal drains both. Post the cell to #2435.
- **(b)** Exclude `@static` members from D1 to keep the ICE: a position-specific special case re-creating the divergence this contract forbids.
- **(c)** Hold P85 until R-KU lands: breaks R-LT's order.

(a) is the two-way door: the ICE was never a refusal (SPY0908 is a net, `spy0908-policy.md`).

## Implementation

### Phase 1: generator (test-only; red cells pinned)

**Goal:** axis 8 enumerates the §3.20p matrix; cells red at base are pinned as allowlist rows citing #2439.

**Acceptance:**
- Matrix: the D2 cells generate (56 by the In list; the measured count is recorded), and each roster's totality fact passes.
- Guard mutations (recorded in the commit body):
  1. Delete the `RightShiftAssign` row from `AugmentedOperators` → the totality fact goes red; restore → green.
  2. Remove `struct` from the product's host loop → the fed-failure check goes red ("… has no cell"); restore → green.
  3. Positive control: at base the SelfDot × {InstanceField, InstanceProperty} × Top cells on all three hosts are red (scalar → SilentWrong; field collection → Ice, from the list `*=` line); the sweep reports them unallowlisted until pinned.
- Allowlist: the red-at-base cells are added as `… SilentWrong|Ice # #2439 silent-wrong` rows at the class the sweep reports. Every TypeName, IndexAccess, TupleLiteral, InsideIf and MethodBody cell must be green at base; a red one is a ledger finding, not a pin.
- Evidence: the `NameResolutionDifferential_Sweep` count line @ sha (pool, run, allowlisted).

#### Tasks

1. **Rosters + totality facts** in `src/Sharpy.Compiler.Tests/Conformance/NameResolutionDifferentialTests.Rosters.cs`: add `AugmentedOperators` (rows carry python spelling, slot type, initial, operand and printed value; collection rows come from `Classify`) and `AugmentedInitTargets` with out-reasons, plus the two facts.
   - Commit: `test(name-resolution): axis 8 operator and augmented-target rosters, total over AssignmentOperator and FirstInvalidAssignmentTarget (#2439)`
2. **Product, cells, fed check, budget** in `NameResolutionDifferentialTests.Axes.cs` (beside `AttributeStoreProduct`, `:1133`): the product and cell builder (host spelling via `HostKinds`' image; `InsideIf` wraps the statements in `if True:`), registered in `GeneratedCells()`; raise `CellBudget`; run the sweep filtered and pin the red cells in `src/Sharpy.Compiler.Tests/Conformance/name-resolution-differential-allowlist.txt`.
   - Commit: `test(name-resolution): axis 8 generates host × position × target × operator for the augmented store (#2439)`

### Phase 2: the cure, the fixtures, the drain

**Goal:** every cell prints python's value; every #2439 allowlist row (the R-LL row plus Phase 1's) is deleted; D3 applied as ruled.

**Acceptance:**
- Matrix: the sweep is green with **0** rows citing #2439 (`grep -c '#2439' …allowlist.txt` → 0); the D3 row is re-pinned per the ruling and the ratchet's STALE/moved report names only it.
- Guard mutation: revert the D1 conjunct → the fixture red plus N sweep cells red (all D2 Top × SelfDot cells); restore → green; both counts in the commit body.
- Fixture outputs: python's own values, from `python3` on each fixture's twin, recorded in the commit body; `??=` and event cells follow the spec (`null_coalescing_assignment.md`, `events.md`).
- Evidence: the fixture runs and the sweep count line @ sha; every §Current-State "top" cell re-run with `sharpyc run` prints the python column.

#### Tasks

1. **D1** in `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.Constructors.cs:140`: the conjunct and its comment (operator-blind interception was #2439).
   - Commit: `fix(codegen): a constructor-body self.<m> op= v takes the general augmented-assignment path (#2439)`
   - Same commit: delete the #2439 allowlist rows, apply D3.
2. **Fixtures** (the axes the generator cannot render, plus charter §3.20p's done-when matrix):
   - `src/Sharpy.Compiler.Tests/Integration/TestFixtures/class_with_init/init_augmented_store_2439.spy` + `.expected`: class, struct and dataclass hosts; each `__init__` runs, at top level, the 12 scalar operators, the 7 `Classify` collection arms, `??=` (absent and present), two event `+=` subscriptions plus one `-=`, then prints one line per host. The expected text comes from the nested twin and python, never from the fixed binary alone.
   - [ADDED: adequacy] The same fixture runs the arm's other entry routes: a `@suppress("SPY0451")`-wrapped `self.x += 9` (the `DecoratedStatement` unwrap, `Constructors.cs:136`; measured 9 @ b5bd25495, python 12) and a store before `super().__init__()` in a derived host (the initializer split).
   - `…/experimental/init_augmented_matmul_2439.spy` + `.features` (`matmul`, `defer`) + `.expected` (`13`), and its ungated twin `init_augmented_matmul_ungated_2439.spy` + `.error` (`SPY0331`). [ADDED: adequacy] It also carries a `self.x += 9` after a `defer:` block, which reaches the arm through `GenerateScopeGuard`'s threaded generator (measured `d` / 9 @ b5bd25495, python 12).
   - Commit: `test(fixtures): __init__ top-level augmented store across host × operator, ??=, event and gated @= (#2439)` (re-pins any per-fixture count pin the run reports, per charter §6).

## Testing Strategy

- Generator first (Phase 1); hand fixtures cover only `??=`, events and `@=` (no python twin, or gated) and the done-when `.expected` matrix.
- Discriminating outputs: each operator row's initial and operand make `v`, `t op v` and `t` print different values; collection rows start non-empty (replacement ≠ mutation); event cells subscribe twice (overwrite ≠ combine); `??=` checks a present and an absent slot.
- Positive controls: InsideIf, MethodBody and TypeName cells are green at base and stay green; the ungated `@=` twin keeps SPY0331.
- Local runs (the lead runs `/ci-gate`): `FullyQualifiedName~NameResolutionDifferential` and `DisplayName~init_augmented`, each with `--logger "console;verbosity=normal"`.

## Issues to Close

- #2439 — closed by Phase 2, Task 1. **Close criterion:** every §Current-State top cell prints python's value under `sharpyc run` @ the landing sha; `grep -c '#2439'` on the allowlist is 0; the sweep and `init_augmented*` fixtures are green in the gate @ sha; the D1 revert mutation's red/green counts are in the commit body.

## Class trackers this plan may post cells to

- #2439 — this class.
- #2435 — the `self.<@static>` store (P30, R-KU). The D3 row's move is posted there. No implementer runs `gh issue create`; findings go to the ledger.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-11-08 @ b5bd25495 (sharpyc built from HEAD, 16:59; `build sharpy.sln` up to date, 0 warnings). Python claims: python 3.13.18.
**Plan file:** this draft, to be copied by the lead into `docs/process/plans/` under the same stem (the lead commits it; this pass did not commit). D3 is graded as written under (a), and is still OPEN with the owner.
**Body size:** 19,985 bytes, excluding the stamp and this summary. That is the lead's R-LW condensation (from 22,289) plus one out-reason clause this pass restored (StaticProperty: no Augmented cell). Within the 20 KB budget.

### Corrections Made
- Defect Class › target roster: the source is `AssignmentTargetShapes` × `AttributeStoreMembers` **× `AttributeStoreBodies`' InitBody receivers**. The draft omitted the receiver enumeration. Its {SelfDot, TypeName} pick is that roster's image (Alias, DerivedType and InstanceLocal are `Pruned` at InitBody, `Rosters.cs:1381–1387`).
- Defect Class › out-reasons: consts were "refused for every form". They are `Ice # #2435` in the existing Assign cells, and `self.K op=` is ICE CS0176 at every position (verify1/c_*.spy). "SelfDot×@static (#2435's existing cells)" now splits into StaticField (existing cells, D3) and StaticProperty. No Augmented × StaticProperty cell exists, and it is ICE at both init top and method body (verify1/sprop*.spy).
- D2 › size: "6 MustRun … 9 … 54 … 63" became 5 MustRun + 1 refusal → 8 per (host × position) → 48 + 8 = **56**, measured and recorded. `CellBudget` is a ceiling (`NameResolutionDifferentialTests.cs:705`), so "the budget assertion is the catcher" was wrong. The fed-failure check is the catcher. Phase 1's acceptance "63" changed to match.
- D3 (a): the re-pinned row's lane token is now named `missing-refusal`, to match the MethodBody twin.
- Phase 2 acceptance: the `??=` spec page is `null_coalescing_assignment.md`, not `assignment_operators.md` (0 `??` hits there).
- [ADDED: adequacy] Defect Class "Entry routes into the arm" and Phase 2 fixture rows: `@suppress`-wrapped store, a store after `defer:` (gated, rides the experimental fixture with `.features` += `defer`), and a store before `super().__init__()`. The `@suppress` and `defer` cells are live (each prints 9, should be 12 @ b5bd25495). D1 reaches them, but no planned test named them.

### Adequacy
- **Contract:** named. An augmented store means `t = t op v` (with mutator, `??=` and event semantics) at every statement position.
- **Matrix:** larger than the repro lists. The touched dispatch is `GenerateConstructorBodyStatement`'s `self.<m>` arm (`Constructors.cs:140`). Its sibling axes: operator (15 `AssignmentOperator` members: Assign + 12 scalar + `??=` + `@=`, all placed), member kind (field, property, event, @static field/property, const, method, all placed after the corrections), host (one call site, `ClassMembers.cs:309`), and entry route (4, added above). I probed the cells the plan does not list: inherited field, `__p` name-mangled, generic host `G[T]`, a comprehension/walrus operand, an `init`-only property, and frozen `@dataclass`. Each is wrong at top and right nested, so D1 reaches them (verify1/inh*.spy, initp*.spy, frozen*.spy). Struct-without-default refuses SPY0252 at both positions.
- **Cure:** seam (one conjunct on the one interceptor). Inert-fix census confirmed: `GenerateSuite` has exactly one generator-passing caller (`Constructors.cs:206`) out of 30. `GenerateAssignment` is the only `Assignment` dispatch elsewhere in CodeGen/Lowering.
- **Harness:** the name-resolution differential axis 8. It drains 1 row today (`…/SelfDot/InstanceField/Augmented/InitBody SilentWrong # #2439`, allowlist line 2382) plus Phase 1's pins.
- **Layer:** an emitter predicate on AST syntax (`Assignment.Operator`). No ladder rung, no `SemanticInfo` table (pin 85), no inspection kind. Holds.

### Scope & convergence
- **Group:** one (P85) · **Alone:** no, and not required. My independent corpus scan found 0 top-level `self.<m> op=` in `__init__` over 4518 `.spy` files and the md blocks in src/docs/build_tools; the planted-file control hit the top-level line and skipped the nested twin · **Gates:** 1.
- **Generator:** existing (axis 8), extended in Phase 1 before any fixture. Axes: host ← `HostKinds` image; position ← `StoreNesting` (closed, `StoreNestingReason`); operator ← `AssignmentOperator`; collections ← `Classify` (callable, `(Assignment, SemanticType?)`); target ← corrected 3-source product. Every axis is roster-closed.
- **Trackers named:** #2439, #2435 · **Fidelity rows:** "Value of an expression; stdout of print" (COMPARED, `python-fidelity-scope.md:26`).

### Falsifiability
- `AugmentedOperators` totality: mutation named (delete `RightShiftAssign`). Exemption not subject. Positive control: n/a.
- `AugmentedOperators` collection rows = `Classify` non-null set: **mutation MISSING**. Expected: drop the `(OrAssign, dict)` row, the fact goes red, restore.
- `AugmentedInitTargets` totality: **mutation MISSING**. Expected: delete the `SelfDot×InstanceProperty` row (or the `StaticProperty` out-reason), the fact goes red, restore.
- Fed-failure check: mutation named (drop `struct` from the product loop). Positive control: yes (base reds pinned).
- Phase 2 D1: inert-fix mutation named (revert the conjunct, then the fixture plus N sweep cells go red). By-direction probe specified (no refusal added; D3's ICE→SilentWrong is recorded).
- Fixtures: outputs discriminate `v` vs `t op v` vs `t`, overwrite vs combine (events), absent vs present (`??=`). Gaps are warnings 2–3.

### Warnings
1. Name the two missing totality mutations above in Phase 1's acceptance.
2. Collection rows show values, not identity, so a rebind (`t = t + v`) prints the same as an in-place `extend`. Add an aliased row (`self.xs = src; self.xs += [2]; self.xs *= 2`, then print `src`). Nested (= post-fix path) and python both print `[1, 2, 1, 2] True` @ b5bd25495.
3. `//=` and `%=` use positive operands (20, 7), so a truncating emission prints the same as a floored one. Add a negative row (`-20 //= 7` → -3, `-20 %= 7` → 1, python 3.13.18).
4. D2 deliberately keeps the collection family off property targets. A collection `op=` through a custom-setter property skips the setter at every position (ledger row). Do not add that cell to P85's fixture as "python's value".
5. "Tuple element" in the charter matrix is read as the tuple-literal target (SPY0225). `self.t[0] += 1` on a `tuple` field is the other reading; it is an IndexAccess cell and stays position-uniform.
6. D3 heading says "owner ruling before `/verify-plan`". The ruling is still open; `/implement-plan` must not start until it is recorded.

### Missing Steps Added
- Gate: `/ci-gate` on the pushed tip, with fallback `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"` (contract §6). Any red is attributed only after a control run at the base sha. CodeGen reaches Stdlib.Tests, Cli.Tests, FrontEndParity, InteropConformance, MetamorphicCorpus, DifferentialExecution and warm/cold. The gate covers them; name them in the commit's evidence line.
- Prober budget (§10): the matrix + ≤ 2 axis changes. Cells outside the contract go to `<scratchpad>/lanes/p85/findings-ledger.md`. This pass appended 4 rows: setter-skip (silent-wrong), `self.<@static property> op=` ICE, `self.<const> op=` ICE, frozen-`__init__` deviation candidate.

### If D3 is ruled (b)
D1 gains a member-kind conjunct. The only fact on hand is `SemanticInfo.GetMemberAccessResolution(memberAccess)` pattern-matched to a static `VariableSymbol`, as `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Access.cs:625` does for methods. That is emitter symbol inspection: either a new row in the both-way `emitter-inspection-allowlist.txt` or a new materialized fact (pin 85). Both conflict with this plan's standing facts, so (b) sends the plan back to the author (Dimension 3). Phase 2's acceptance also changes: allowlist line 2385 stays `Ice`, the ratchet reports nothing moved, and the @static exemption becomes the subject of D1's guard and needs its own falsifiable arm (Dimension 7). Under (c), the plan is held and nothing in it changes.

### Unchecked Claims
- The red-at-base classes Phase 1 predicts (scalar SilentWrong, field collection Ice). Only the generator's first run measures cell classes; my `run` probes agree cell-for-cell.
- LSP comment-census and code-action pin moves (+1 each). Measured only when the fixtures land.
- Issue #2435's state and comments were not read (`--issues` not run, per the lead). #2439 is open, and comment 6724244460 is the owner's R-LL placement as cited.

## Implementation Checklist

- [x] Phase 1 T1: `AugmentedOperators` + `AugmentedInitTargets` rosters and their totality facts — implementer — blocked by none
- [x] Phase 1 T2: product, cells, fed-failure check, `CellBudget`, red-at-base pins (M1–M3 recorded) — implementer — blocked by Phase 1 T1
- [x] Phase 2 T1: D1 conjunct at `Constructors.cs:140`, every `#2439` allowlist row deleted, D3 (a) re-pin (D1-revert mutation recorded) — implementer — blocked by Phase 1 T2
- [x] Regen: `regenerate_spy_stdlib.sh` + snapshot regen, diff read as findings (predicted: no movement) — implementer — blocked by Phase 2 T1
- [x] Phase 2 T2: `init_augmented_store_2439` + experimental `@=`/`defer` fixtures and the ungated twin — implementer — blocked by Phase 2 T1
- [x] Gate: `/ci-gate` on the pushed tip, PR — lead — blocked by every task above (PR #2504, merge 8d0367e0a; `84015/0/9 @ 210570a26 (measured, run 42524091317)`)

## Implementation Verification Record

**Plan:** `docs/process/plans/plan-5cfc2b.md` (stamp: PASS WITH CORRECTIONS; Adequacy: CLASS)
**Tier:** full (c) — Agents 1–4 (contract §12). Deviation: the session had no Agent tool, so the lead ran all four auditor briefs in sequence, in one context; Agent 5 was not spawned (the fixtures' python claims were checked with python3 directly).
**Branch:** `claude/p85-verify`
**Scope:** `be11fca77..8d0367e0a` (base = parent of e5f6c26eb; 6 implementation commits e5f6c26eb, a80b65996, ea19a9749, ac9fe9170, 6e2f83b26, 210570a26; 0 remediation commits; the range also carries the unrelated merge 4d22e0007 (#2502: `docs/deviations.yaml`, oracle ledger), which is outside this audit)
**Verified on:** 2026-11-08

### Completeness (Agent 1)

| Status | Count |
|--------|-------|
| DONE (evidence cited) | 14 |
| Was PARTIAL (now fixed) | 0 |
| Was MISSING (now fixed) | 0 |
| DIVERGED (acceptable, why) | 1 |
| Unresolved | 0 |

- DONE: the `AugmentedOperators` and `AugmentedInitTargets` rosters plus their two totality facts (`Rosters.cs`); the product, cells, fed-failure check and `CellBudget` 8945 (`Axes.cs`; pool 8889 → 8945 = +56, matching D2's 56); M1/M1b/M1c/M2/M3 recorded in the a80b65996/ea19a9749 bodies; the D1 conjunct with its #2439 comment (`Constructors.cs:145`); 0 `#2439` allowlist rows; the D3 row re-pinned `SilentWrong # #2435 missing-refusal`; the D1-revert record in the ac9fe9170 body; the regen record with zero files moved; the main fixture (3 hosts × 12 scalar + str `+=` + 7 collection arms + an aliased list + `??=` absent/present + event `+=`/`+=`/`-=`, plus the `@suppress` and before-`super().__init__()` routes); the experimental `@=` + `defer` fixture with `.features`; the ungated SPY0331 twin (`!count 2`); LSP pins re-pinned with controls; the D3 move posted on #2435 (2026-11-08T21:32Z); the verify-plan warnings 2 (aliased list, `is src` → True) and 3 (`//=`/`%=` on −20) addressed in both roster and fixture.
- DIVERGED, accepted: `SampledRangeLineCeilingPercent[Wide]` 54 → 55 (not predicted by the plan; owner ruling D5 (a), corpus growth from one sampled-in stem, controlled at ac9fe9170 with the stems absent).
- Census deltas agree with the product: Store × MustRefuse × kind:SPY0225 28 → 35 (+7 tuple cells), Store × MustRun × N7 18 → 67 (+49 = 56 − 7).

Issues to Close:
- **#2439.** Criterion (a): every §Current-State top cell prints python's value under `sharpyc run`. Re-run @ 8d0367e0a with the 15 original probes (`lanes/p85/{ops_class,ops_struct,ops_dataclass,t_coll,t_coll2,t_prop,t_coalesce,t_event,t_matmul (--enable-feature matmul),t_static_type,t_index,t_deep,t_tuple,t_static_self,t_struct_mixed}.spy`): each prints the python/nested column. The values are `27 13 140 2.5 2 6 32 8 15 9 12 8 abcd [1, 2, 3]` on all three hosts; the collections; `12 20`; `5 3`; `ping`/`pong`; `13`; `12`; `[10, 2] {'a': 7}`; `12`; SPY0225; `12` (D3); and `2`/`14`. Criterion (b): `grep -c '#2439'` on the allowlist → 0 @ 8d0367e0a. Criterion (c): the sweep and the `init_augmented*` fixtures are green in the gate `84015/0/9 @ 210570a26 (measured, run 42524091317)`, whose "Sweep name-resolution-differential" step is a success. Criterion (d): the D1-revert counts are in the ac9fe9170 body and reproduced here (below). **CLOSEABLE @ 8d0367e0a.**

### Regressions (Agent 2)

- **Baseline @ HEAD:** `84015/0/9 @ 210570a26 (measured, run 42524091317)`. The merge 8d0367e0a differs from 210570a26 only by #2502's `docs/deviations.yaml` and `build_tools/cpython_oracle/ledger.yaml`. Build @ 8d0367e0a succeeds with 0 warnings. `format whitespace --verify-no-changes` exits 0 @ 8d0367e0a.
- **Post-remediation @ FINAL:** no remediation, so FINAL = HEAD for code; the round's only commit is this record.
- **Regressions found (red @ HEAD, green @ BASE):** 0. The gate has 0 failed.
- **Pre-existing reds (red at both):** 0.
- **Expectation changes in range:** 0 `.expected` modified, 0 `.skip` added. Added: 2 `.expected` and 1 `.error`.
- **Sweeps @ 210570a26 (run 42524091317):** interop-conformance, differential-execution, format-spec, name-resolution, formatter-emit-invariance and metamorphic-corpus are all success. The "Stdlib .spy compile + staleness gates" job (spy-stdlib C# and spy-test C# staleness) is success.
- **Allowlist delta:** name-resolution: −1 R-LL `#2439` row; +9 then −9 Phase 1 `#2439` rows; 1 re-pinned (D3); +7 `#2503` rows; 0 rows without an issue. code-action-shape: 15 digest re-pins, controlled. FormattingRouteParity: 4 comment pins +1 each; wide ceiling 54 → 55 (D5).
- **Representative execution @ 8d0367e0a (`sharpyc run`):** `init_augmented_store_2439` and `init_augmented_matmul_2439` (with `--enable-feature matmul --enable-feature defer`) match their `.expected` byte for byte; the ungated twin emits both SPY0331s.

### Attribution (control run)

- **Control @ BASE:** `83975/0/9 @ 64e17a7f2 (measured, run 42498119732)`. No compiler source changed between 64e17a7f2 and be11fca77. The +40 passes at HEAD are the range's new facts, cells and fixtures; no red needed attribution.

### Class contract status (Agents 3 + 4)

- **Violated contract:** an augmented store `t op= v` evaluates `t = t op v` (with the collection mutator, `??=` and event semantics) identically at every statement position (plan).
- **Standing harness:** the name-resolution differential, axis 8, extended by this plan (+56 cells: host × position × target × operator). Green @ 210570a26 in the gate and 252/0/0 @ 8d0367e0a (measured, filtered `DisplayName~init_augmented|FullyQualifiedName~NameResolutionDifferential`).
- **Allowlist delta:** −10 `#2439` (drained), +7 `#2503` (filed, labelled), 1 re-pinned per D3, 0 stale.
- **Cells probed / passed / failed:** 30 / 30 / 0 in contract (matrix source: the plan). That is the 15 Current-State top cells and 15 sibling probes over the plan's matrix plus two axis changes:
  - **Axis 1, operand form** (the bypassed arm carried the parameter mapping, `FlushIntoStatement` hoisting and the Optional-delegate conversion): parameter `27`; C#-keyword parameters `checked`/`lock` `54`; comprehension `[1, 0, 2, 4] 7`; walrus `24 4`; empty literals `[1] {'k': 1}`; f-string then `*=` `a3-6a3-6`; `??=` with a method group onto `Ping | None` (`ping`/`pong`, equal to its nested twin).
  - **Axis 2, host form:** generic `G[T]` `13 [5, 5]`; inherited field `2`; name-mangled `__p` `6`; nested class `80`; defaulted parameter `21`/`25`.
  - **Entry route × operator family:** `@suppress` over a collection `*=` and `??=` `[1, 2, 1, 2] 2`; `defer` with event `+=` and `//=` `-3`/ping/pong; a store before `self.__init__(7)` (see the ledger: the operator is right, the order is not).
  - Not reached: the LSP/warm route (the blast radius names neither), a multi-file base class, a property with a custom setter (already ledger row 2).
- **Seam-or-arm verdicts:** 1 SEAM, 0 ARM. `GenerateConstructorBodyStatement` is `GenerateSuite`'s only generator-passing caller (`Constructors.cs:211`) and the only `self.<m>` store interceptor in CodeGen. The conjunct sits after all four entry routes. Rule 1b: `Name == BuiltinNames.*` count is 101 at both base and HEAD; Semantic/ and Lowering/ are untouched; no SemanticInfo carrier was added.
- **Refusals by direction:** 0 newly emitted. D3 is ICE (SPY0908 CS0176) → SilentWrong, as ruled. List `*=` and gated `@=` go ICE/wrong → correct.

### Guards mutation-tested (Agent 3)

- **Red when broken:** the D1 revert was reproduced here in the shared tree, restored from a `cp` copy. Broken → red: 249/3/0 @ 8d0367e0a+mutation (measured). The 3 red are `init_augmented_store_2439`, `init_augmented_matmul_2439` and `NameResolutionDifferential_Sweep`, with "non-allowlisted 10, moved 1 (D3 row), python agreed 8945/8945" (log `dotnet-serialized-20261108-220721-1330.log`). Restored → green: 252/0/0 @ 8d0367e0a (measured; log `dotnet-serialized-20261108-221555-19312.log`). This matches the ac9fe9170 record (3 red; 10 cells + 1 moved). M1/M1b/M1c/M2/M3 were read from the commit bodies and not re-run.
- **Vacuous / inverted / inert:** 0. The fixture outputs discriminate: pre-fix, the main fixture is SPY0908 CS0029 and the gated one prints 4/d/9. The ungated twin stays green either way, as a refusal twin should.
- **Absence assertions given a positive control:** n/a. The sweep's `non-allowlisted 0` has M3 at base as its positive control.

### Architectural Review (Agent 3, Section B)

- **Critical:** 0 · **Warnings:** 0 · **Suggestions:** 1. `ImplementingIssue`'s pre-existing arm (`NameResolutionDifferentialTests.cs:605–607`, 86af02155) routes every must-run SilentWrong attribute store to `#2439`. Once #2439 closes, a red of a different class, such as ledger row 2's setter-skip, would be proposed against a closed issue. Rerouting it is a choice for whoever files the next attribute-store class; no row takes the arm today. No TODO/FIXME or debug leftovers in the added lines; no emitter type decision; the predicate reads AST syntax only.

### Generated artifacts

- Regen recorded at ac9fe9170 (spy-stdlib, spy-test, snapshots): 0 files moved. CI staleness gates are green @ 210570a26.

### Fixes Applied

None. No regression, vacuous guard, missing deliverable, stale artifact or formatting finding.

### Issues Created (one per class — contract §10)

None by this round. Two out-of-contract rows were appended to the lane ledger (`<scratchpad>/lanes/p85/findings-ledger.md`) for the lead to file:
- A statement ahead of a constructor-initializer call (`self.__init__(…)` / `super().__init__()`) runs after the hoisted `: this(…)` / `: base(…)`. This is operator-blind: `self.x *= 3; self.__init__(7)` prints 81 against python's 67, `=` prints 5 against 12, and `+= 5; super().__init__()` prints 105 against 100. `constructors.md:45` requires `self.__init__` first and nothing refuses it. Lane: missing-refusal / silent-wrong · reach:program.
- `super().__init__()` on a base with no explicit `__init__` is SPY0203. Lane: false-refusal · reach:program.
- Ledger: 6 → 8 rows. Round ratio: filed 0 / closed 0 by this round; #2439 is CLOSEABLE.

### Unresolved Items

None.

### Files Changed

`git diff --stat be11fca77..8d0367e0a`: 20 files, +776 −27 (2 from #2502), plus this record.
