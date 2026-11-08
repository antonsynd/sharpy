<!-- Verified by /verify-plan on 2026-11-08 @ b5bd25495 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: existing -->

# P82 tranche 2 — the match-scrutinee cast onto the IR; name-keyed type compares drained; inspection kinds closed (refs #2443)

## Context
#2443 (tracker, R-LK). Tranche 1 (plan-eaaed2; PR #2492, #2493): tables 88 → 85, `emitter-inspection-allowlist.txt` seeded. Measures the tranche-2 candidates (charter §3.20o call 40, D2's MIXED pair); takes what is clean.

## Current State (@ b5bd25495; `sharpyc` built at HEAD, 0 warnings)
- Pin `SemanticInfoMergeConformanceTests.cs:65` = 85 (`Dictionary<` fields of `SemanticInfo.cs`); setter floor `SemanticInfoFreezeTests.cs:35` = 89; `_semanticInfo.Set\w*Lowering(` in `TypeChecker*.cs` = 71; `.expected.cs` = 170 (`find src -name '*.expected.cs'`, bin/obj excluded); allowlist 166 rows = type-name 131 + type-read 17 + name-lookup 15 + declaration-line 3.
- CodeGen unchanged since W2.3 (`git diff --stat 0ea13e3a6 b5bd25495 -- src/Sharpy.Compiler/CodeGen` empty). Seven compares: `RoslynEmitter.Expressions.Literals.cs:35,86,167`, `RoslynEmitter.Expressions.Comprehensions.cs:365`, `RoslynEmitter.Expressions.cs:844,855`, `RoslynEmitter.Expressions.Access.Calls.cs:502` (two there); no kind counts them. Sibling `<hierarchy type> { Name: … }`: 11 more single-line sites outside `TypeSyntaxMapper.cs`, e.g. `RoslynEmitter.Statements.Unpacking.cs:184-185`.
- `type-read` counts two accessor names; CodeGen also reads `SemanticType`s via `GetDenotedType` ×3, `GetTypeAnnotation` ×4, ×1 each `GetPatternType`, `GetEffectiveType`, `GetArgumentSlotCast`, `GetConditionalBranchNarrowing`, `GetIntegerConstantCast`, `GetNullConditionalOptionalWrap`, `GetSequenceMaterialization`; lists via `GetInferredTypeArguments` ×4, `GetPatternUnionCaseTypeArguments` ×5; `SemanticBinding.GetVariableType` ×1 (`RoslynEmitter.cs:1079`).
- No `.expected.cs` holds a float32/decimal suffix, array bridge or `match None` (grep 0/0/0); run fixture `match/match_bare_none_scrutinee_1526` has no snapshot.
- `docs/design/lowering-ir.md:447-456` (REPL, generator sub-pipeline emit IR-less) is wrong: `ReplSession.cs:403-406` uses `ProjectCompiler`; `ProjectCompiler.Generators.cs:134` lowers its own IR; `CodeGenContext.Ir` is `required` (`CodeGenContext.cs:141`). Only direct-emitter unit tests use `IrCompilation.Empty` (recompute fallbacks `RoslynEmitter.Expressions.Comprehensions.cs:525,536`).

## Scope
- **Group:** P82 tranche 2 (§3.20o, row 6c⁶⁷″); no chores. **Tier:** full (c). **Alone:** yes. **Gates:** 1 (Testing Strategy). **Lane:** own PR series beside the Core band (R-LT); P85 lands first, no shared seam.
- **Tranche (D1):** IN `_matchScrutineeLowerings` (pin 85 → **84**), six compares, two kinds, look-through pins, Appendix paragraph; OUT five candidates.
- **Output:** byte-identical (R-LV). A diff in a committed `.expected.cs` or spy C# is an owner STOP; never regenerate. **Filing:** ledger only. **Override:** none.

## Defect Class
- **Violated contract:** #2443 items 1, 2, 6: the lowering IR is the emitter's only input (lowering decisions only from IR nodes; a `SemanticType` may go to `TypeSyntaxMapper`, never be inspected); the pass alone consumes node-keyed lowering facts; each inspection left is a counted allowlist row draining to 0.
- **Meta-class:** SPY0908 as a net; vacuous instruments (two hand-listed kinds). **Cure:** seam (IR node / mapper hand-off) + kinds closed against compiler enumerations.
- **Cells:** sole writer `TypeChecker.Statements.Patterns.cs:46`; readers `RoslynEmitter.Patterns.cs:32,1036`; `MergeFrom` `SemanticInfo.cs:2538`; record/enum `SemanticInfo.cs:3436-3442`; 0 test readers.
- **Sibling cells (rules):**
  - (a) The getter's deletion makes a missed reader a build error; reader grep 2 → 0.
  - (b) `type-name-compare`: each name compare on a receiver the scanner binds to an `InspectionDeniedNames` type: (i) `r.Name ==`/`!=` (also `r?.Name`), `r` a pattern designation, local or parameter of that type; (ii) its `RecursivePatternSyntax` (simple or qualified) with a `Name:` subpattern; per (file, shape), every compare. Excluded: `var`-bound receivers (`foreach (var p in funcSymbol.Parameters)`, `RoslynEmitter.Expressions.Access.Calls.cs:699-701`: typing needs a semantic model; `type-name` counts their declarations); `UnionCaseCompare` matches (`name-lookup`); AST receivers; `TypeSyntaxMapper.cs` (item 1).
  - (c) `type-read`: reflection over `SemanticInfo` and `SemanticBinding`, every public method returning a `SemanticType`-assignable type or an `IEnumerable<T>` of one, plus emitter helper `GetExpressionSemanticType`.
- **Lane / reach:** ice · program (absent cast: CS8119 under `run`); no refusal moves, no fidelity row.
- **Harness:** `FileBasedIntegrationTests`, `IrTotalityTests`, `EmitterCarrierOnlyConformanceTests`, the pin. **Generator:** EXISTS: `.expected.cs` corpus, spy regen byte-identity, `ILCompilesPropertyTests`/`CsCleanPropertyTests`, plan-eaaed2's agreement sweep (disagreement = STOP).
- **Rulings:** R-LK, R-LT, R-LV, R-LW, R-LX; handoff D2.

## Adversarial Review
- **Inert risk.** An `IrTypedNullScrutinee` miss reads "not this kind" (control: Phase 0 snapshot + `run`, CS8119). Per-site old-vs-new sweeps check the hand-offs (D4's suffix: equivalent, not verbatim). `IrLoweredLoop`'s look-through is output-inert (recompute fallback, `RoslynEmitter.Expressions.Comprehensions.cs:525`): constructed-IR pin (D5).
- **Alternative layer.** Element types and literal suffixes are type → syntax maps (`TypeSyntaxMapper`, item 1): no IR node. The array bridge is a conversion: OUT.
- **Direction:** no verdict moves.
- **Blast radius:** Compiler.Tests, Stdlib.Tests (spy C#), Cli.Tests, LSP parity, interop, metamorphic, differential, warm/cold. Pins moving: setter floor (D2); LSP fixture-stem censuses +1 per new fixture (re-pinned on landing); a `DispatchSiteInventoryTests.cs:253-259` row per new pass helper.
- **Generated:** Phase 0 adds 3 `.expected.cs`, 2 `.spy`, 2 `.expected`.

## Design Decisions
**D1 — tranche, measured (writers, readers, test readers, `MergeFrom`).**
- `_matchScrutineeLowerings` **IN** (cells: Defect Class): pure; plan-eaaed2 D1 deferred it.
- `_narrowedExpressionTypes` **OUT: a type fact with production readers** (the census grepped only `GetNarrowedType(`). `GetEffectiveType` (`SemanticInfo.cs:1254-1257`) reads it; callers `RoslynEmitter.Statements.ControlFlow.cs:378`, `MustUseValidator.cs:49`, 24 LSP sites (`grep -rho`). `ISemanticQuery.GetNarrowedType` (`ISemanticQuery.cs:22`): 10 `TypeCheckerTests` reads, 5 "not narrowed" absence asserts `GetExpressionType` can't express; removal is an `ISemanticQuery` surface change, not a migration.
- `_moduleLayouts` / `_fromImportBoundNames` **OUT: no deletion is reachable.**
  `_moduleLayouts`: non-CodeGen readers `ProjectCompiler.Utilities.cs:34` (publish entry), `ProjectCompiler.IncrementalCache.cs:66`, `ProjectCompiler.Phases.cs:1056` (diagnostic); writers `CodeGenInfoComputer.cs:199,416`, project re-key `ProjectCompiler.Generators.cs:562-563`. `_fromImportBoundNames`: read-modify-written at `CodeGenInfoComputer.cs:428-438` after `ProjectCompiler.Phases.cs:911` copies it in. An IR carrier moves 3 emitter reads (`RoslynEmitter.CompilationUnit.cs:398,418`, `RoslynEmitter.ModuleClass.cs:406`), both dictionaries stay (pin unchanged). `ModuleLayout` is an imported module's `ModuleSymbol.CodeGenInfo` fact, node-keyed only as the own module and a from-import lack a `ModuleSymbol`; classing it "symbol" for the done-when is an owner call (hand-back).
- `_operatorLowerings` / `_typeTestLowerings` **OUT: D2's precondition is unmet.** Non-emitter reads `ConstEligibility.cs:611`, `TypeChecker.Statements.cs:562`, `TypeChecker.Utilities.cs:905` (`TestType`, a type fact inside a lowering), 28 test reads (23 `GetOperatorLowering(` + 5 `GetTypeTestLowering(`). Splitting type half from lowering half is a design call.
- `RoslynEmitter.Expressions.Access.Calls.cs:502` **OUT.** `ApplyArrayBridge` (call sites `:864,930,963,1315`) is an argument-conversion decision (its `uint8` compare sits by three `{ Name: … }` patterns). Cure: one IR conversion property on the argument, shared with `_sequenceMaterializations` (`TypeChecker.Expressions.Access.Calls.cs:3755`, P18b's cell); stays as `type-name-compare` rows citing #2443.

**D2 — `IrTypedNullScrutinee(SemanticType? Type, TextSpan Span, ImmutableArray<IrNode> Children) : IrExpression`** (`src/Sharpy.Compiler/Lowering/IrExpressions.cs`), keyed on the scrutinee as written (the writer's key, Parenthesized included).
- **Predicate:** `AstHelper.UnwrapParenthesized(scrutinee) is NoneLiteral` (`MatchStatement`/`MatchExpression`): the writer's condition (`TypeChecker.Statements.Patterns.cs:44`, same loop via `TypeChecker.Utilities.cs:737`, so `match ((None)):` agrees) over a type `VoidType` for every `NoneLiteral`; the sweep measures exactness. No rung: AST kind only.
- **Construction:** as `LowerElidedNoneBodyLambda` (`LoweringPass.cs:374-390`): lower children, swap the scrutinee's index entry and child in place; arms in `LowerStatement` (`MatchStatement`), `LowerExpression` (`MatchExpression`); no `IrTreeRewriter` arm (nothing to fold).
- **Emitter:** `RoslynEmitter.Patterns.cs:32,1036` test `IrNodeFor<IrTypedNullScrutinee>(…Scrutinee) != null`, keeping the `(object?)` cast.
- **Same-commit deletion:** field, setter, getter, `MergeFrom` stanza, record, enum, the write at `:46-47`; the recorded `NullableType(object)` stays (arms check against it). Pin 84; floor 88; `Set\w*Lowering(` 71 → 70.

**D3 — inspection kinds (`EmitterCarrierOnlyConformanceTests.cs`; instrument only).** `type-name-compare` per rule (b), Roslyn syntax, printed `CARRIER-INSPECTION-CENSUS kind=type-name-compare`, own exempt count; `type-read` per rule (c), reflection replacing the two-name literal. Rows seeded at the scanner's counts, `cite=#2443`; header, doc updated.

**D4 — `TypeSyntaxMapper` hand-offs (item 1).**
- `IReadOnlyList<TypeSyntax>? MapBuiltinTypeArguments(SemanticType? type, string builtinName, int arity, bool requireKnownArguments)`: null unless `type` is a `GenericType` named `builtinName` with ≥ `arity` arguments, none of the first `arity` `UnknownType` if `requireKnownArguments`; callers fall back to `object` per argument.
  - Literals: `(List, 1, true)`, `(Dict, 2, true)` (either Unknown → both `object`, kept), `(Set, 1, true)`; dict-spread comprehension: `(Dict, 2, false)` (Unknown → `object`, `TypeSyntaxMapper.cs:33`).
- `string? RealLiteralSuffix(SemanticType? type)`: `"f"` float32, `"m"` decimal, else null; `GenerateFloatLiteral` switches on it (text assembly stays). Today's `is BuiltinType { Name: "float32" | "decimal" }` (`RoslynEmitter.Expressions.cs:842-855`) vs the mapper's record `==` (`TypeSyntaxMapper.cs:43-44`, also on `ClrType`): all construction sites (singletons, `SymbolSerializer.cs:1855`, `ClrTypeBridge.cs:272`, `PrimitiveCatalog.cs:286,294`) set the catalog `ClrType`, so equal @ b5bd25495, not by construction. Move the `Name` predicate verbatim into the (exempt) mapper, or keep `==` proved by the Phase 3 sweep (0 throws).
- Type reads unchanged (item 1 allows the hand-off). `type-name-compare` −6; `type-name` deltas quoted from the scanner.

**D5 — look-through pins over a closed roster.** Roster = `IrExpression` subclasses (reflection, minus `IrOptionalWrappedElement`) ∩ CodeGen's `IrNodeFor<T>` census = @ b5bd25495 the seven kinds below + `IrElidedNoneBody` (+ `IrTypedNullScrutinee` from Phase 2); `IrStackArray` (read only by `IrPassManager.cs:93`), `IrOpaqueExpression`, `IrCall` have no `IrNodeFor` read. Excluded, exactly: `IrElidedNoneBody` (lambda body only), `IrTypedNullScrutinee` (no read through the wrap). Totality: other members have pin rows; excluded entries are members.
- Already pinned: `IrConstant`, `IrOptionalWalrusStore` (`optional_walrus_tuple_element` cells 6, 4).
- New, by output (measured): `IrEqualityComparison` (`g, n = t == (1, "a"), 9` → `t.Equals((1, "a"))`; only `.expected.cs` discriminates: absent, `NativeOperator` emits tuple `==`, also `True 9`); `IrIndexAccess` (`p, q = t[0], d["k"]`, narrowed `int?` → `Optional<int>.Some(t.Item1)`, `TupleItem`, `RoslynEmitter.Expressions.Access.cs:2228`; absent, `Native` emits `t[0]` on a `ValueTuple`: CS0021 under `run`; `5 6` = python3).
- New, by constructed IR (a unit test indexes the element to `IrOptionalWrappedElement { Children: [K] }`, a fact the absent path cannot produce): `IrLoweredLoop` (sizing flag opposite to the recompute), `IrLoweredGenerator`, `IrMemberAccess` with `ResolvedClrMemberName: "Zz"` (no program discriminates it: `TypeChecker.Expressions.Access.cs:1439` records the spelled member's name).
- A row whose mutation stays green: STOP.

**D6 — docs.** Rewrite `lowering-ir.md:447-456` to the measured state; add Appendix "P82 tranche 2"; `docs/architecture.md:53`: move `MatchScrutineeLowering` out of the `SemanticInfo` carrier list, as `IrTypedNullScrutinee`, into the line's "rides the lowering IR, as do five kinds …" list (five → six, tranches 1–2).

## Implementation
Agreement sweeps (as plan-eaaed2, uncommitted): a temporary throw on disagreement over `FullyQualifiedName~FileBasedIntegrationTests` and both spy regen scripts; quote 0 throws and the control's ≥1.

### Phase 0: positive controls at base (test-only)
**Acceptance:** every migrated shape sits in a committed base `.expected.cs`; each later phase's first mutation reddens it. Python: 3.13.18.
- **Snapshots.**
  - `match/match_bare_none_scrutinee_1526.expected.cs`: `switch ((object?)null)` ×2, `(object?)null switch`.
  - New `basics/emitter_type_name_compare_controls.spy` (`main`):
    - `xs = [1, 2, 3]`; `ys: list[str] = []`; `d = {"a": 1, "b": 2}`; `s = {4, 5}`; `print(xs, ys, d, s)`;
    - `print({**src for src in [d, {"c": 3}]})`;
    - `f: float32 = 0.5`; `h: decimal = 1.25`; `k: decimal = 3.0`; `print(f, h, k)`;
    - `print(count_bytes(b"abcd"), count_ints([7, 8]))`, `def count_bytes(a: array[uint8]) -> int` / `count_ints(a: array[int])` returning `len(a)`;
    - `match (None):` statement (`case None: print("paren None")` / `case _: print("other")`), `v: int = match (None): case None: 1 / case _: 2`, `print(v)`.
    - Stdout @ b5bd25495: `[1, 2, 3] [] {'a': 1, 'b': 2} {4, 5}`, `{'a': 1, 'b': 2, 'c': 3}`, `0.5 1.25 3.0`, `4 2`, `paren None`, `1`. Python agrees on lines 1, 5 and 2 (as `{k: v for src in … for k, v in src.items()}`).
    - Check: `new Sharpy.` + `List<int>()`, `List<string>()`, `Dict<string, int>()`, `Set<int>()`; `var __comp_0 = new Sharpy.Dict<string, int>();`, `0.5f`, `1.25m`, `3.0m`, `.ToArray()` ×2, `switch ((object?)(null))`, `(object?)(null) switch`.
  - New `type_system/optional_wrapped_element_children.spy`, narrowed `a: list[int]?`, `g: bool?`, `n: int?`, `e: Iterator[int]?`, `xs = [1, 2, 3]`, `t: tuple[int, str] = (1, "a")`:
    - `a, n = [x * 2 for x in xs], 4` → `[2, 4, 6] 4`;
    - `g, n = t == (1, "a"), 9` → `True 9`;
    - [ADDED: missing step] narrowed `p, q: int?`, `d: dict[str, int] = {"k": 6}`: `p, q = t[0], d["k"]` → `5 6` (D5's `IrIndexAccess` pin);
    - `e, n = (x * 3 for x in xs), 5`; `print(list(e), n)` → `[3, 6, 9] 5`.
    - All measured = python3.
  - Seed `// Snapshot:`; `UPDATE_SNAPSHOTS=true` filtered to the three (`/regenerate-snapshots`).
  - Commit: `test(codegen): snapshot the match-scrutinee cast, name-compare and wrapped-element shapes`

### Phase 1: close the inspection kinds (instrument only)
- D3 + seeded rows. `SyntheticInspectionKinds_AreMeasured` gains cells scanned with `InspectionDeniedNames(SemanticUniverse())` (its scan passes an empty set today):
  - positives: `t is GenericType g && g.Name == "list"`, `x is BuiltinType { Name: "uint8" }`, `GetDenotedType(e)`;
  - negatives: `varDecl.Type.Name == "auto"`; `unionCase?.Name == "Some"` with `unionCase` a denied type (e.g. `TypeSymbol?`, so the exclusion is tested), once, as `name-lookup`; a mapper-file compare + a counted twin elsewhere.
  - **M1:** restore the two-name literal → new `type-read` rows STALE (red). **M2:** drop shape (ii) → `{ Name: … }` rows STALE (red). **M3:** `|| listSemType.Name == "tuple"` at `RoslynEmitter.Expressions.Literals.cs:35` → `type-name-compare` over count (red; no `type-name` row moves: why the kind exists). Restore → green.
  - Commit: `test(codegen): count name-keyed type compares and every SemanticType getter read (#2443 item 6)`

### Phase 2: `_matchScrutineeLowerings` → `IrTypedNullScrutinee`
- Node, two pass arms, two reads, the D2 deletions and re-pins, dispatch row(s).
  - **Sweep first:** throw when the node set differs from the table's keys; control: drop `UnwrapParenthesized` → the Phase 0 paren cells throw.
  - **Test:** `IrTotalityTests` case: the node on all five Phase 0 scrutinees; none on `x: object | None = None; match x:` (emits `switch (x)`, prints `none` = python3; measured).
  - **M4:** never build the node → snapshots red + `run` SPY0908. **M5:** drop `UnwrapParenthesized` → paren cells red. **M6:** add a `ConcurrentDictionary` field → pin red. Restore → green. Spy-stdlib/spy-test regen byte-identical.
  - Commit: `refactor(lowering): the match None-scrutinee cast rides the IR`

### Phase 3: the six compares → `TypeSyntaxMapper`
- D4's two methods, read by the six sites; re-pin measured counts.
  - **Sweep:** per site, build old and new syntax, throw on a text difference; control: `requireKnownArguments: false` at the dict literal throws on a `dict[str, Unknown]` unit cell (say if no fixture reaches it).
  - **Tests:** mapper cells (arity, Unknown, non-builtin name, `float32`/`decimal`/`float`).
  - **M7:** `RealLiteralSuffix` → null → `basics/float32_literal_annotation` `run` SPY0908 (CS0664 on `x: float32 = 0.1`, `z: float32 = 2.0`; all-suffixed `basics/float32_literal.spy` stays green) + Phase 0 snapshot red. **M8:** ignore `requireKnownArguments` → `dict[str, Unknown]` cell red (`<string, object>` ≠ `<object, object>`); ignore `builtinName` → non-builtin cell red. Restore → green. Spy regen byte-identical.
  - Commit: `refactor(codegen): collection element types and real-literal suffixes come from TypeSyntaxMapper`

### Phase 4: look-through roster (test-only)
- D5's totality test + rows (`src/Sharpy.Compiler.Tests/CodeGen/`).
  - **M9:** remove `IrNodeFor`'s look-through (`RoslynEmitter.Expressions.Access.cs:2186-2193`) → every row red (count quoted). **M10:** delete a roster or excluded entry → totality red, naming the subclass. Restore → green.
  - Commit: `test(lowering): pin the wrapped-element look-through for every IR expression kind the emitter reads`

### Phase 5: docs
- D6, with counts @ the Phase 4 sha: tables, floor, `Set\w*Lowering(`, `GetMatchScrutineeLowering(` 2 → 0; allowlist rows by kind; `.expected.cs` 170 → 173; emitter LOC (`wc -l src/Sharpy.Compiler/CodeGen/RoslynEmitter*.cs` = 26997 @ base; reported, not forced).
  - Commit: `docs(lowering): record P82 tranche 2 and correct the null-IR paragraph`

Bodies: `refs #2443`, cruft trailer (§5.2), attribution; e.g. Phase 2: `Cruft: 1 #2443 · 2 −1 SemanticInfo table, −1 checker write, −2 emitter SemanticInfo reads · 3 M4–M6 red/green · 4 none · 5 none · 6 issue+recap`.

## Testing Strategy
- Local: `Sharpy.Compiler.Tests` filtered, console logger normal: `IrTotalityTests|EmitterCarrierOnlyConformanceTests|SemanticInfoMergeConformanceTests|SemanticInfoFreezeTests|DispatchSiteInventoryTests|TypeSyntaxMapper|RoslynEmitter|FileBasedIntegrationTests`.
- Gate: lead's `/ci-gate` @ PR tip; exit 3 → `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`.

## Issues to Close
- #2443 stays OPEN (tracker); the lead's recap cites the Phase 5 counts and D1's five OUT reasons.

## Class trackers
- #2443; #2162 (P37: a planning ledger cell). Out-of-contract cells go to the ledger for the lead to post; no implementer runs `gh issue create`.

## Implementation Checklist
Board for the P82 tranche-2 lane (implementer: one Opus worker, sequential; lead pushes, runs `/ci-gate`, opens the PR). Base `be11fca77` (dev).
- [x] T1 — Phase 0 positive controls (3 `.expected.cs`, 2 `.spy`, 2 `.expected`) — implementer — blocked by none — LANDED 570cd145d (23/23; python3 = sharpyc; subject shortened to ≤ 72)
- [x] T2 — Phase 1 inspection kinds `type-name-compare` + reflection `type-read`; rows seeded; M1–M3 — implementer — blocked by T1 — LANDED 111eac007 (rows 166 → 192: type-name-compare 11 rows / 20 sites, type-read 17 → 32; M1 2 red / M2 2 red / M3 1 red; `is` + switch counted: `Assignments.cs:762` is the warned `r.Name is` spelling)
- [x] T3 — Phase 2 `_matchScrutineeLowerings` → `IrTypedNullScrutinee`; sweep + control; pin 84, floor 88, `Set*Lowering` 70; M4–M6 — implementer — blocked by T2 — LANDED 010773247 (sweep 3671/3671 + 2 regen scripts, 0 throws; control 1 throw `Parenthesized @ 27:11`; pin 84, floor 88, `Set*Lowering` 70; M4 3 red / M5 2 red / M6 2 red; no dispatch row needed)
- [x] T4 — Regen after Phase 2: spy stdlib + spy tests + snapshots byte-identical (STOP on any diff) — implementer — blocked by T3 — DONE @ 010773247: no diff (spy stdlib, spy tests, stdlib docs `--check`; every `.expected.cs` byte-identical)
- [x] T5 — Phase 3 six compares → `TypeSyntaxMapper`; sweep + control; M7–M8 — implementer — blocked by T4 — LANDED 4b8f36e89 (sweep 3671/3671 + regen, 0 throws; control: no fixture reaches `dict[K, Unknown]` → unit cell; `type-name-compare` 20 → 14 sites, `type-name` 407 → 398; M7 6 red / M8a 1 / M8b 1)
- [x] T6 — Regen after Phase 3: byte-identical (STOP on any diff) — implementer — blocked by T5 — DONE @ 4b8f36e89: no diff (as T4; blast radius 10526/10526, FrontEndParity 1/1)
- [x] T7 — Phase 4 look-through roster totality + rows; M9–M10 — implementer — blocked by T6 — LANDED f5c64af34 (roster 9 = 7 pinned + 2 excluded; M9 6 red, each row confirmed by emit; M10 1 red naming both entries)
- [x] T8 — Phase 5 docs (`lowering-ir.md`, `architecture.md:53`) with counts @ the Phase 4 sha — implementer — blocked by T7 — LANDED 15c2148d2 (emitter LOC 26,997 → 26,966; allowlist 166 → 188; `.expected.cs` 170 → 173)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-11-08 @ b5bd25495 (`sharpyc` built at HEAD, exit 0; python3 3.13.18)
**Plan file:** `plan-1faae3.md` under `docs/process/plans/` once the lead copies and commits it (this pass did not)

### Corrections Made
- D1 `_narrowedExpressionTypes`: 25 LSP sites → 24 (`grep -rho 'GetEffectiveType(' src/Sharpy.Lsp`).
- D1 `_operatorLowerings`/`_typeTestLowerings`: 34 test reads → 28 (23 + 5 direct calls); the OUT verdict stands on the 3 production reads, all confirmed.
- Rule (c): the reflection predicate is widened to `IEnumerable<SemanticType>` returns and to `SemanticBinding` (+`GetInferredTypeArguments` ×4, `GetPatternUnionCaseTypeArguments` ×5, `GetVariableType` ×1), so the kind counts what its commit claims.
- D2: the cite that the checker's `UnwrapParenthesized` is `AstHelper`'s loop (nested parens agree) is added.
- D4 `RealLiteralSuffix`: `== SemanticType.Float32` is record equality (`Name` + `ClrType`), not a verbatim move of `Name == "float32"`. It is equivalent at every construction site found; the sweep, not construction, is the proof.
- D5: the excluded set is reduced to {`IrElidedNoneBody`, `IrTypedNullScrutinee`}. `IrStackArray`/`IrOpaqueExpression` have no `IrNodeFor` read, so they are not in the roster.
- D5: `IrIndexAccess` moves from constructed IR to an output pin (`p, q = t[0], d["k"]` → `t.Item1`; absent → `t[0]` CS0021). Phase 0 gains that line [ADDED].
- D5: the `IrEqualityComparison` row discriminates only via `.expected.cs` (the `NativeOperator` default also prints `True 9`).
- D6: `architecture.md:53` now moves `MatchScrutineeLowering` out of the `SemanticInfo` carrier list into the same line's IR-kind list, instead of renaming it in place.
- Phase 0: the Python line-2 agreement is via the equivalent comprehension (`{**src …}` is not Python).
- Phase 1: rule-(b) synthetic cells must scan with the real denied set (the existing test passes `empty`). The `unionCase` negative needs a denied-type declaration, or the `UnionCaseCompare` exclusion is untested.
- Phase 3 M7: `basics/float32_literal` → `basics/float32_literal_annotation` (the former has only suffixed literals and stays green).

### Adequacy
- **Contract:** named — #2443 items 1, 2, 6 (issue body read @ open).
- **Matrix:** larger than repro lists. The scrutinee writer has 1 site and the readers 2 (`RoslynEmitter.Patterns.cs:32,1036`), both covered. The six compares match D4's arity/Unknown parameters arm by arm (`RoslynEmitter.Expressions.Literals.cs:35,86,167`, `RoslynEmitter.Expressions.Comprehensions.cs:365`, `RoslynEmitter.Expressions.cs:844,855`). Rules (b)/(c) are keyed on reflection enumerations (rule (c) was widened inline).
- **Cure:** seam (IR node + mapper hand-off) + kinds closed against enumerations.
- **Harness:** `.expected.cs` corpus, spy regen byte-identity, `IrTotalityTests`, `EmitterCarrierOnlyConformanceTests`, the pin; allowlist delta −6 `type-name-compare` (seeded in Phase 1), `type-name` deltas from the scanner.
- **Layer:** no rung-4 rule is added. The literal hand-offs are type → syntax maps on the mapper (item 1); D2 is an AST-kind predicate.

### Scope & convergence
- **Group:** one (P82 tranche 2) · **Alone:** yes — required? yes (charter row 6c⁶⁷″) · **Gates:** 1
- **Generator:** existing — the `.expected.cs` corpus + spy regen + agreement sweeps; the hand-written D5 roster is reflection-closed with a totality test.
- **Trackers named:** #2443, #2162 · **Fidelity rows:** n/a (no Python-visible change).

### Falsifiability
- Phase 0 snapshots: mutation via M4/M5/M7 named; positive control = the base `run` (measured: all six stdout lines, every claimed C# shape, 1526's `(object?)null` ×2 + `switch` ×1); inert-fix check yes.
- Phase 1 kinds: M1/M2/M3 named and discriminating (M3's `listSemType` is a `GenericType` designation, so rule (b)(i) binds it). Exemption: the `UnionCaseCompare` negative was the subject until corrected (receiver undeclared); positive control = the seeded rows.
- Phase 2 `IrTypedNullScrutinee`: M4/M5/M6 named. Negative `x: object | None` measured (`switch (x)`, prints `none`, = python3). The sweep's control (drop `UnwrapParenthesized`) is named. By-direction: no refusal moves.
- Phase 3 mapper: M7 (fixture corrected) and M8 named; the sweep control is the `dict[str, Unknown]` unit cell.
- Phase 4 roster: M9 (look-through removed → every row red) and M10 (totality) named. `IrLoweredLoop`/`IrLoweredGenerator` are output-inert by their AST/recompute fallbacks (`RoslynEmitter.Expressions.Comprehensions.cs:42,525`), so constructed IR is required for them.

### Warnings
- Rule (b)'s compare-shape axis is a hand pair (`==`/`!=`, `{ Name: }`). `r.Name is "…"`, `switch (r.Name)` and `string.Equals(r.Name, …)` on a bound receiver would escape it. No such site was measured @ b5bd25495, so this is latent. Either count every `.Name` member access on a bound receiver, or name the pair as closed with its reason.
- Plan body was 19,984 bytes before this pass and 23,783 with the inline corrections; it was then condensed (wording only) to 19,995 bytes ≤ the R-LW 20,000-byte budget, measured by `awk 'NR>4 && /^## Verification Summary/{exit} NR>4{print}' | wc -c`. Every fact, `file:line` citation (set diff empty), mutation M1–M10, commit message and `[ADDED]` marker is kept. The `[CORRECTED: …]` commentary was folded into the text; Corrections Made above records each one.
- The Testing Strategy's pipe-joined filter list needs `FullyQualifiedName~A|FullyQualifiedName~B` syntax when run.
- The charter §3.20o fact "`_narrowedExpressionTypes` has 0 production readers" is stale. D1 refutes it; the lead should correct the charter line at recap.

### Missing Steps Added
- Phase 0: the `t[0]`/`d["k"]` wrapped-element line (the `IrIndexAccess` output pin).
- Stdlib docs / oracle ledger regen are not named. Expected no diff (byte-identical claim); quote "regen: no diff @ sha" beside the spy regen.

### Unchecked Claims
- "0 throws" for both agreement sweeps (D2 exactness, D4 old-vs-new): measured only at implementation.
- `DispatchSiteInventoryTests` row count per new pass helper: depends on the helper's shape.
- The `type-name` deltas D4 quotes "from the scanner": measured at Phase 3.

## Implementation Verification Record

**Plan:** `docs/process/plans/plan-1faae3.md` (stamp: PASS WITH CORRECTIONS; Adequacy: CLASS) · **Tier:** full (c), Agents 1–4. No Agent tool was available to the lead, so the lead ran the four briefs in sequence (1 completeness, 2 regression, 3 class-cure, 4 sibling-cell prober).
**Scope:** P82's commits only, measured as `8d0367e0a..b42973a7e` (the PR merge's first parent is dev-with-P85, so this diff is exactly P82's; P85's merge 8c1fd2cbd is not audited). Base `be11fca77`; HEAD `b42973a7e` (tree = `a90f01448`'s); 1 remediation commit. **Verified on:** 2026-11-08.

### Baseline, controls, gate
- Gate @ HEAD: `84053/0/9 @ a90f01448 (measured, run 42528698154)`; the tree of `b42973a7e` equals `a90f01448`'s, so the gate covers HEAD.
- Controls: base `83975/0/9 @ 64e17a7f2 (run 42498119732)` (`src` identical to be11fca77); dev-with-P85 `84015/0/9 @ 210570a26 (run 42524091317)` (`src` identical to 8d0367e0a). +38 passes against dev, 0 reds. First dispatch `84010/3/9 @ bceae2da2 (run 42523772234)` is the direction record (two LSP pins, one scan red), all fixed by 0c4e6e950 / a90f01448.
- Local build @ b42973a7e: 0 warnings, 0 errors. `format whitespace --verify-no-changes`: clean @ HEAD and @ f464281d0.

### Completeness (Agent 1)
DONE, with evidence: Phase 0 (3 `.expected.cs`, 2 `.spy`, 2 `.expected`; every claimed C# string grepped in the snapshots; `.ToArray()` ×2 sit on one line); Phase 1 (kinds + seeded rows); Phase 2 (`IrTypedNullScrutinee`, two pass arms, two emitter reads, `MatchScrutineeLowering` 0 code hits in `src`, only a comment in `SemanticInfoFreezeTests`); Phase 3 (mapper methods, six sites); Phase 4 (roster + totality); Phase 5 (`lowering-ir.md` null-IR paragraph and tranche-2 appendix, `architecture.md:53`). Counts re-measured @ b42973a7e: pin 84; `Dictionary<` fields 84; floor 88; `Set\w*Lowering(` 71 → 70; `IrNodeFor<IrTypedNullScrutinee>` 2; allowlist 188 rows = type-name 130 + type-name-compare 8 + type-read 32 + name-lookup 15 + declaration-line 3, all `cite=#2443`; `.expected.cs` 173; emitter LOC 26,997 @ be11fca77 → 26,966 @ f5c64af34 (−31, P82) → 26,972 @ HEAD (dev's P85 adds +6, 27,003 @ 8d0367e0a). The `DispatchSiteInventoryTests` row was not needed: `TypeNullScrutinee` is an `if` with no dispatch default, and the inventory is green in the gate. Issues to Close: #2443 stays OPEN (tracker), as planned.

### Regressions (Agent 2)
- 0 regressions. 0 `.expected`/`.error`/`.warning`/`.skip` modified; 5 added (Phase 0).
- Regeneration @ b42973a7e: `check_spy_staleness.sh` all up to date; `check_spy_tests_staleness.sh` 119/119 up to date; `python3 -m build_tools stdlib generate --force` leaves the tree clean.
- Allowlists: `emitter-inspection-allowlist.txt` +22 rows, every one `cite=#2443`; `BuiltinTypeNameComparisonScanTests` drained its `GenerateFloatLiteral` exemption (0c4e6e950). `code-action-shape-allowlist.txt`: three rows each move by one cell (65→66, 94→95, 1→2) under their existing classes #2294/#2296 for the two new stems. This is a census move, not a P82 failure.

### Class contract status (Agents 3 + 4)
- **Contract:** #2443 items 1, 2, 6. **Seam or arm:** SEAM for all three hunks. Both match hosts go through one predicate (`IsTypedNullScrutinee`) and one swap (`TypeNullScrutinee`); the six compares go through two mapper methods; both kinds are closed against reflection/syntax enumerations. Old-vs-new equivalence was read arm by arm (arity, `Count` bound, Unknown handling, dict-spread's no-Unknown-check).
- **Rung (1b):** `Name == BuiltinNames.` in `Semantic/` 101 → 101; CodeGen name compares 47 → 41 (six moved into the exempt mapper, as D4 rules).
- **Rule 2 / tranche-1 failure kinds:** no `SemanticInfo` key was added (one table deleted with its `MergeFrom` stanza). The index swap replaces the scrutinee's own entry only. `IrTreeRewriter`'s default arm returns typed nodes unchanged, and with all three `opt_*` flags on, the three Phase 0 fixtures emit identical C# at both shas and match `.expected`. #1720: `EmittedOptionalConstructionConformanceTests` scans the whole `.expected.cs` corpus, including the 3 new snapshots, and is green in the gate.
- **Byte identity:** NOT REFUTED. Emitted at `8d0367e0a` vs `b42973a7e`: every fixture containing `match`, `float32`, `decimal` or `{**` (322): 244 identical C#, 78 identical refusals, 0 DIFF. Plus 14 scratch probes and a two-module project cold + warm `--incremental` (identical modulo `#line` paths; same stdout) and the REPL (identical).
- **Cells probed:** match-scrutinee shapes `None`/`(None)`/`((None))` × statement/expression × host (module initializer, function, method, generator, async, try/finally, nested match, loop, expression-in-parentheses, wrapped tuple element). Literal shapes × knownness (typed/untyped empty, nested, `T | None` element, `object` element, mixed int/float, user generic element, dict-spread over typed, empty and `list` values). Real literals × position (field, default, return, argument, list/dict/tuple element, exponent form). Axis changes (2): multi-file + warm `--incremental`; REPL. Not reached: the source-generator sub-pipeline host. 0 cells moved between shas; 0 FAIL attributable to P82.
- **Refusals by direction:** none; no diagnostic added or removed.

### Guards mutation-tested (Agent 3, re-run by the lead)
- **M7** (`RealLiteralSuffix` → `null`): broken → red, 12 failed / 3,698 passed / 3,710 @ b42973a7e+M7; restored → green, 3,710 / 0 (`FileBasedIntegrationTests|TypeSyntaxMapperTests`). The direction reproduces. The recorded "6" is narrower than this filter: the extra reds are other float32/decimal fixtures (`float32_literal_positions_1688`, `decimal_literal_exponent_forms`, `store_conversion_positions_1698`, …). Not vacuous.
- **M4** (`IsTypedNullScrutinee` → `false`, also the inert-fix check): broken → red, 3 failed / 3,680 passed / 3,683 @ b42973a7e+M4 (`IrTotalityTests` case, `match_bare_none_scrutinee_1526`, `emitter_type_name_compare_controls`); restored → green, 3,683 / 0 (`FileBasedIntegrationTests|IrTotalityTests`). Matches the record's 3.
- The other recorded pairs (M1–M3, M5, M6, M8a/b, M9, M10) were read, not re-run. Vacuous / inverted / inert: 0.

### Architectural review (Agent 3, Section B)
- Critical 0 · Warnings 1 (fixed) · Suggestions 0. 4b8f36e89 left `CreateCollectionType`'s `<summary>` above `MapBuiltinTypeArguments` (two summaries on one method, none on the other), and 010773247 left three double blank lines in `SemanticInfo.cs`. Both are fixed in f464281d0.

### Fixes applied
| Commit | Description | Category |
|---|---|---|
| f464281d0 | chore(codegen): restore CreateCollectionType's doc; drop stray blanks | cleanup |

### Ledger (no issues filed by this round)
- +1 row: `a: float32 = -0.5` / `decimal = -1.5` / `-(0.5)` / `+0.5` → SPY0220, while `a: int = -3` runs. Spec: "same rule as integer constants". false-refusal · program; pre-existing and identical at 8d0367e0a and HEAD; outside the plan's contract.
- +1 de-dup row: the `n, k = v, 3` CS0029 unpacking (narrowed `int?` + fresh name) is #2494 cell 3, re-measured; no filing.

### Acceptance
Met at HEAD; the remediation tip f464281d0 changes comments and whitespace only. Gate for f464281d0: the lead's `/ci-gate`.
