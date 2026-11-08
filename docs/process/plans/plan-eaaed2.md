<!-- Verified by /verify-plan on 2026-11-08 @ 5d0187ecd -->
<!-- Verification result: CONVERGED -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: existing -->

# P82 tranche 1 — three pure lowering tables move onto IR nodes (refs #2443)

## Context
#2443 (class tracker, R-LK): the lowering IR is the emitter's only input. Charter §3.20o items 3–5 have landed (`Freeze()` before `BuildLoweringIr`, equal-value `MergeEntry`, pin 88). This first tranche of items 1–2 moves three node-keyed lowering tables onto IR nodes and deletes them. Owner ruling D2 (`docs/process/handoff-2026-11-06-remediation.md` §7, 2026-11-08: option 1, option-B addendum @ b3dddb111, S2 addendum @ e3cc7d16c) fixed it: `_augmentedAssignMutations`, `_optionalStoreWraps`, `_lambdaBodyLowerings`. Basis: the W2.1 census (#2443, 2026-11-08) and the verify findings.

## Current State (@ ef6039e33; counts re-measured @ 5d0187ecd, unchanged)
- Pin `SemanticInfoMergeConformanceTests.cs:61` = 88.
- `_semanticInfo.Set\w*Lowering(` in `TypeChecker*.cs` = 72; of the three only `SetLambdaBodyLowering` matches (`SetAugmentedAssignMutation` ×1, `SetOptionalStoreWrap` ×5 do not).
- Emitter reads: `grep -rnoE "Get(AugmentedAssignMutation|LambdaBodyLowering|OptionalStoreWrap)\(" src/Sharpy.Compiler/CodeGen` = 12 (4 + 3 + 5); no caller outside `CodeGen/` but `SemanticInfo.cs`.
- The pass (`LoweringPass.cs:64`, `:154`) wraps assignments, lambdas, tuple literals as opaque nodes; `IrCall` has no pass arm. `Lower(modules, SemanticInfo, SymbolTable)` has 3 callers.
- `CodeGenContext.Ir` is `required` (`CodeGenContext.cs:141`); 45 unit contexts use `IrCompilation.Empty`.
- `IrTreeRewriter.cs:68` does not descend into typed nodes; the flag-gated opt passes run in no snapshot inside a lambda, augmented or wrapped store.
- 166 `.expected.cs` (`find . -name '*.expected.cs' -not -path '*/bin/*' -not -path '*/obj/*'`); 0 exercise the three tables (census §3; pass-1 verify).
- `Optional<T>` keeps an implicit `T → Optional<T>` (`src/Sharpy.Core/Optional.cs:127`): an ABSENT store wrap compiles and prints the same (`StoreTargetMatrixTests.cs:1538-1543`: 1123/0 with the read dropped), not the census's CS0029/CS1503; only emitted text and `EmittedOptionalConstructionConformanceTests` (#1720) see it.

## Scope
- **Group:** P82 (charter §3.20o, row 6c⁶⁷″), tranche 1; no chores. **Tier:** full (c), per charter §3.20o. **Alone:** yes. **Gates:** 1 (`/ci-gate` @ PR tip, run by the lead). **W2.3** follows this gate (its counts are measured after the tranche).
- **Tranche:** `_augmentedAssignMutations`, `_optionalStoreWraps`, `_lambdaBodyLowerings`; pin 88 → 85. Out: `_inferredTypeArguments` (a type fact; W2.3 input) and `_matchScrutineeLowerings` (tranche 2). See D1.
- **Filing:** ledger only.
- **override:** owner 2026-11-08, option 1 "your rec" (tranche) + option B "until then option b" + "S2": the ONE output change is the walrus-into-narrowed-Optional SHAPE, two cells: the tuple-element collision (`(Some(42), 1)` / CS0029 → Python's `(42, 1)`) and the own-site wrap (`(c = 42).Unwrap()` → `(c = Optional<int>.Some(42)).Unwrap()`, stdout same). No committed `.expected.cs` holds either (`grep -rlE "\(\w+ = [^;]*\)\.Unwrap\(\)" --include=*.expected.cs src` = 0; the HEAD walrus snapshot: 5); all others byte-identical. Lands alone (§11).

## Defect Class
- **Violated contract:** #2443 item 2: the pass is the only producer of IR lowering properties; the checker records types, symbols and diagnostics.
- **Meta-class:** SPY0908 as a net. Cure: the lowering is materialized on the IR node.
- **Member cells:**
  - Writers: `TypeChecker.Statements.cs:237,432,578,696`, `TypeChecker.Statements.Patterns.cs:1725`, `TypeChecker.Expressions.cs:988`, `TypeChecker.Expressions.Access.Lambdas.cs:185`.
  - Readers: `RoslynEmitter.Statements.Assignments.cs:89,140,181,221,235,337,950,997`, `RoslynEmitter.Expressions.Literals.cs:275`, `RoslynEmitter.Expressions.Access.Calls.cs:55,120,226`.
- **Sibling cells (rule):** a reader of a deleted getter does not compile; the grep goes 12 → 0, quoted in each commit.
- **Lane / reach:** ice · program (ruled walrus cells: silent-wrong · program, ledger rows 14, 17); no refusal moves.
- **Harness:** `FileBasedIntegrationTests`, `IrTotalityTests`, `StoreTargetMatrixTests`, the pin.
- **Generator:** EXISTS: the `.expected.cs` corpus, spy-stdlib/spy-test regen byte-identity, `ILCompilesPropertyTests`/`CsCleanPropertyTests`.
- **Rulings:** R-LK; D2 (+ option B).

## Adversarial Review
- **Inert risk:** a derivation agreeing only on the corpus; countered by a per-phase agreement sweep (temporary throw when pass ≠ table, every fixture) and a discriminating cell per conjunct. A missing store wrap compiles, so its controls are the Phase 0 snapshot and `StoreTargetMatrixTests.cs:1536`.
- **Direction:** no checker verdict moves; any `.expected`/`.error` change other than the ruled cell (Phase 3 fixture) is a STOP.
- **Blast radius:** Compiler.Tests, Stdlib.Tests (spy C#), Cli.Tests, LSP parity, interop, metamorphic, differential, warm/cold.
- **Generated artifacts:** Phase 0 adds 3 `.expected.cs`, 2 `.spy`, 2 `.expected`; Phase 3 one of each. Spy-stdlib/spy-test C# regenerate byte-identical after each relocation.

## Design Decisions
**D1 — source of each fact.** No rung: each fact comes from the AST kind plus recorded type facts; no new name-keyed rule.
- `_augmentedAssignMutations` MOVES: `AugmentedCollectionAssignment.Classify(assign, GetExpressionType(assign.Target))?.ClrName`.
  - Exact: the write (`TypeChecker.Statements.cs:432`) stores `Classify(assignment, targetType)` (`:407`), `targetType` being `CheckExpression`'s return recorded at `TypeChecker.Expressions.cs:158`. Every earlier exit reports an error or has no `Classify` arm (`??=` returns at `:358`).
  - `Classify` is shared, not moved (the checker keeps it for RHS validation); `AugmentedCollectionClassifyTotalityTests` pins its arms; rung unchanged (`AugmentedCollectionAssignment.cs:60`).
- `_lambdaBodyLowerings` MOVES: `AstHelper.UnwrapParenthesized(lambda.Body) is NoneLiteral && GetExpressionType(lambda) is FunctionType { ReturnType: VoidType }`. The checker's condition (`TypeChecker.Expressions.Access.Lambdas.cs:183`) over the type it returns (`:195`); sole caller `TypeChecker.Expressions.cs:56`.
- `_optionalStoreWraps` MOVES. The value is the slot's `OptionalType o`, passed to `WrapInOptionalSome` (`RoslynEmitter.Expressions.Access.Calls.cs:346`). Five writer arms:
  - **(a) Plain `=`**, on `Assignment` (`TypeChecker.Statements.cs:237`).
    - Predicate: `assign.Operator == Assign ∧ assign.Target is Identifier t ∧ GetIdentifierSymbol(t) is VariableSymbol s ∧ Slot(s) is OptionalType o ∧ GetExpressionType(t) is not (OptionalType or UnknownType)`.
    - `Slot(s)` ((a), (c), (d), (e)) = `DeclaredBindingType` (`binding.GetVariableType(GetRootBinding(s))`, else `.Type`; `src/Sharpy.Compiler/Semantic/TypeChecker.cs:509-523`). `SemanticInfo` has no binding accessor, so `LoweringPass.Lower` gains a required `SemanticBinding` parameter (an input, not a table): `_projectModel.SemanticBinding` at `ProjectCompiler.CodeGen.cs:242`/`ProjectCompiler.Generators.cs:134`, its own at `EmitterTestPipeline.cs:82`; frozen at `ProjectCompiler.cs:349`.
    - Unknown: the checker skips the store on an Unknown value (`TypeChecker.Statements.cs:222`) and records Unknown on `t` (`:292`).
    - Evidence: verdict recorded on `t` (symbol `:289`, type `:292`); under an Optional slot it is non-Optional only on the payload branch (`:231-243`), which writes the wrap. The else branch (`:254`; `StoreSlotType` keeps an Optional slot whole, `TypeChecker.StoreConversion.cs:287-294`), the void branch and the `IsAssignable` short-circuit leave it Optional.
    - A fresh binding is its own root, so the type conjuncts exclude it.
    - [CORRECTED: slot accessor, applies to (a), (c), (d) — the checker's slot is `DeclaredBindingType(s)` = `SemanticBinding.GetVariableType(GetRootBinding(s))`, falling back to `.Type` only when unset (`TypeChecker.cs:509-523`; `Symbol.cs:245` "Readers should prefer SemanticBinding.GetVariableType"). `LoweringPass.Lower(modules, SemanticInfo, SymbolTable)` (`ProjectCompiler.CodeGen.cs:242`) has no `SemanticBinding`; pass `_projectModel.SemanticBinding` (as `ProjectCompiler.CodeGen.cs:102` and `ProjectCompiler.Generators.cs:157` do for the emitter) and read it binding-first, or measure that `root.Type` agrees. Measured @ 5d0187ecd.]
  - **(b) Tuple element**, on the RHS element node (`TypeChecker.Statements.Patterns.cs:1725`).
    - Predicate: (a)'s on the target element `t`, paired by position with a spread-free `TupleLiteral` value's elements (`TupleLiteralElements`, `:1611`), recursing as `CheckTupleUnpackingElements` does.
    - Evidence: same facts on `t` (`:1793`, `:1805`).
    - **Known ICE cell:** mixed unpacking (`p` narrowed `int?`, `q` fresh: `p, q = 45, 4`) wraps an element of a `ValueTuple<int, int>` temp → SPY0908 CS0029 @ 5d0187ecd (ledger); derivation exact, no `.expected`.
  - **(c) Augmented**, on `Assignment` (`TypeChecker.Statements.cs:578`).
    - Predicate: `assign.Operator ∉ {Assign, NullCoalesceAssign} ∧ assign.Target is Identifier t ∧ no IrCollectionMutation ∧ Slot(GetIdentifierSymbol(t)) is OptionalType o`. The read symbol is recorded at `TypeChecker.Expressions.cs:674,682`.
    - No narrowing input: an un-narrowed augmented store into `T?` is SPY0222. The mutation path returns at `:433`, before `:578`, so the pass tests it first.
    - **Known non-exact cell:** `x: int?`, `if isinstance(x, int): x += 1` passes the checker unwrapped and ICEs at base (SPY0908 CS0030 on `((int)x!)`; ledger); the derivation wraps it, it still ICEs, no `.expected` holds it. If the sweep meets it: STOP.
  - **(d) `??=`**, on `Assignment` (`TypeChecker.Statements.cs:696`).
    - Predicate: `assign.Operator == NullCoalesceAssign ∧ slot is OptionalType o ∧ GetExpressionType(assign.Value) is not OptionalType`.
    - The slot is `Slot(GetIdentifierSymbol(t))` for an identifier (`DeclaredBindingType`, `:645`); otherwise it is `GetExpressionType(assign.Target)`, the declared slot `:627` records under `_plainStoreTarget`.
    - Evidence: the arm tests `!IsAssignable(valueType, o)` after `CheckStore` admitted the value (`:684-696`). Into an `OptionalType`, `IsAssignable` (`TypeChecker.Utilities.cs:1016-1204`) is true only via `OptionalType.IsAssignableTo` (Optional source), `Equals`, `UnknownType` or the CLR fallback; the sweep measures exactness off those arms.
  - **(e) Walrus**, on `WalrusExpression` (`TypeChecker.Expressions.cs:988`; ruling: D2 second addendum "S2", e3cc7d16c, PR #2485) [CORRECTED: the S2 text is on open PR #2490 (head f9d70abf4); #2485 merged at 48c6afb3e, which holds option B but not e3cc7d16c (`git merge-base --is-ancestor` false) @ 5d0187ecd]. The write recorded the right fact; both readers were wrong: the tuple-literal collision (an unparenthesized walrus element IS the `elem` key at `RoslynEmitter.Expressions.Literals.cs:275`: `(c := 42, 1)` prints `(Some(42), 1)`, `t = (c := 43, 2)` is CS0029; option B) and the own site, which never applied it and rides the implicit operator #1720 forbids (S2).
    - Predicate (own site): `GetWalrusSymbol(w) is VariableSymbol s ∧ Slot(s) is OptionalType o ∧ GetExpressionType(w) is not (OptionalType or UnknownType)`: (a)'s, on the walrus's recorded type (the returned `bindingType`, `:1038`: payload on the wrap branch, the slot on void/else, Unknown on error). Carried on the walrus's IR node; nothing derived for a walrus as a tuple element; the checker write is deleted.
    - As an unpacking element (`p, q = (c := 45, 4)`, all narrowed `int?`; today prints `45 4 45` = python3): (b) derives `p`'s slot on the element, the walrus arm `c`'s: two IR nodes on ONE AST key (`IrIndex`, one node per key, `LoweringPass.cs:428`), nested by D2. Fixture cell 4.
  - **Shared helpers: none move.**
    - `IsAssignable` reads the symbol table, the `TypeResolver` variance walk and CLR reflection (`TryGetDelegateInvokeMethod`, `IsGenericAssignableWithVariance`, `TryGetClrType`); `ClassifyStore` reads `IsLiteralDerived` and the constant resolver. Neither is needed: (a)–(c) read the checker's recorded verdict, (d) is the value-type predicate; both stay in the checker.
    - A sweep disagreement on any fixture is a STOP; the fallback (a checker-recorded assignability fact) re-opens D2.
- `_inferredTypeArguments` does NOT move (W2.3 input).
  - 2 of 4 writers are inference-only (`TypeChecker.Expressions.Access.Calls.cs:2989,3116`; `_callTargets` holds the open `FunctionSymbol`, `:6448`; `:3116` folds in `ResolvePendingOverloadSelections`). `:6305` is carried by the call's recorded `GenericType { TypeArguments }` (`TypeChecker.Expressions.Access.Calls.Construction.cs:334-339`), read first at `RoslynEmitter.Expressions.Access.cs:1304`; `:1931` (min/max) is derivable.
  - `ISemanticQuery.GetInferredTypeArguments` (`Services/ISemanticQuery.cs:51`) has test readers (`SemanticQueryTests.cs:110,125`, `MasterRegistryImmutabilityTests.cs:1041`): not deletable. A type fact the census filed as lowering.
- `_matchScrutineeLowerings`: tranche 2 (the census's ranked list); not touched here.

**D2 — carriers; absent unrepresentable.** Each fact rides the IR node of its table's key (lowering-ir.md §6 2a) as a node KIND with required positional parameters (omitted = CS7036; `null` string = CS8625, an error under Rule 9):
- `IrCollectionMutation(string ClrMethodName, TextSpan Span, ImmutableArray<IrNode> Children) : IrStatement` (`src/Sharpy.Compiler/Lowering/IrStatements.cs`), for an `Assignment` whose `Classify` is non-null; otherwise the node stays opaque. It is `string`, not `AugmentedMutation`, because `EmitterCarrierOnlyConformanceTests` denies `Semantic` types to CodeGen.
- `IrElidedNoneBody(SemanticType? Type, TextSpan Span, ImmutableArray<IrNode> Children) : IrExpression`, for `lambda.Body`.
- `IrOptionalStoreWrap(OptionalType WrapAs, TextSpan Span, ImmutableArray<IrNode> Children) : IrStatement`, for an `Assignment` under D1 (a), (c) and (d).
- `IrOptionalWrappedElement(OptionalType WrapAs, SemanticType? Type, TextSpan Span, ImmutableArray<IrNode> Children) : IrExpression` (`IrExpressions.cs`), for a tuple-literal element under (b).
- `IrOptionalWalrusStore(OptionalType WrapAs, SemanticType? Type, TextSpan Span, ImmutableArray<IrNode> Children) : IrExpression`, for a `WalrusExpression` under D1 (e): a `LowerExpression` arm before the opaque default (`LoweringPass.cs:195`); read by `GenerateWalrusExpression`'s rebind arm (`RoslynEmitter.Expressions.Literals.cs:782`), wrapping `value` (`:762`). If (b) also wraps it, the index holds the element node with the walrus node as `Children[0]`. [CORRECTED: so a by-key `IrNodeFor<IrOptionalWalrusStore>(walrus)` at `:769` misses on cell 4 and drops its own-site wrap; the walrus read looks through `IrOptionalWrappedElement { Children: [IrOptionalWalrusStore] }`. Deferred row F1; the #1720 guard catches the miss (scratch: 1 site, `:24 on: 45`)]
- Precedence on `Assignment`: mutation, then wrap, else opaque. `OptionalType` sits under the `SemanticType` carrier root the conformance test admits (`EmitterCarrierOnlyConformanceTests.cs:102`).
- `Children` = `LowerChildren(node)` in order (totality). `IrElidedNoneBody` wraps only a `NoneLiteral`. The other four kinds hold foldable operands, so `IrTreeRewriter` gains one arm each, mirroring the opaque arms.
- Emitter: all 12 sites already hold the keyed AST node, so none needs the Appendix's side-lookup routing ("Phase-2 deviations").
  - Store-wrap sites: `assign` at `:140,:221,:997`; `assignNode` at `:950` (`GenerateAugmentedValue`'s `Assignment?`, passed `assign` from `:306`); `elem` at `RoslynEmitter.Expressions.Literals.cs:275`.
  - One generic helper, `IrNodeFor<TIr>(Node ast)`, over `_context.Ir.Index` (the `IrIndex` precedent). An index miss reads as "not this kind", which keeps today's `SemanticInfo?` null semantics for the 45 `IrCompilation.Empty` unit contexts (hand-built ASTs in `RoslynEmitterStatementTests.cs`, `RoslynEmitterExpressionTests.cs`). Production totality is `IrTotalityTests`' job.
  - The `??=` conjunct at `RoslynEmitter.Statements.Assignments.cs:89` is deleted, not ported: `Classify` has no `NullCoalesceAssign` arm, so it is always true. The `:221` operator test is control flow and stays.
  - No type decision moves into the emitter.

**D3 — same-commit deletion:** field, setter, getter, `MergeFrom` stanza, checker write(s); Phase 2 also the `LambdaBodyLowering` record and enum; Phase 3 also the `cref` at `TypeChecker.StoreConversion.cs:78` (dangling `cref` = warning = error). No `ISemanticQuery` member or test reads the three. Pin literals 87, 86, 85 (re-measured by reflection; the literal is the ceiling).

**D4 — snapshots first:** Phase 0 commits the positive controls at base.

## Implementation

### Phase 0: positive-control snapshots (test-only)
**Acceptance:** every read site's shape is in a committed `.expected.cs` generated at base, and each relocation phase's first M falsifies it.
1. Existing fixture `src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/lambda_none_void_delegate_1514.spy` covers the untyped, typed and hoisted lambdas (`RoslynEmitter.Expressions.Access.Calls.cs:55,120,226`).
   New `basics/augmented_collection_mutation_targets.spy` + `.expected`: the 7 `Classify` arms on identifiers; `self.items += [2]` (`:337`), `m["k"] += [5]` (`:235`); narrowed `list[int]?`, `list[int] | None` receivers; alias prints; rebind control `n += 2`.
   [ADDED: missing step — a narrowed MEMBER receiver, `self.o: list[int] | None` under `if self.o is not None: self.o += [2]` (emits `this.O!.Extend(`, prints `[1, 2]`, measured); the derivation reads the target's recorded narrowed type at `:337` too, and no fixture has it. Check count becomes `Extend(` ×6.]
   Stdout via python3 3.13.18 (CI 3.12): `[1, 2, 3, 1, 2, 3]`, `[2, 4, 9]`, `{'a': 1, 'b': 2}`, `[1, 2]`, `[1, 5]`, `[1, 2]`, `[7, 8]`, `3`.
   Check: `Extend(` ×5, `InPlaceRepeat(`, `Update(` ×2, `IntersectionUpdate(`, `DifferenceUpdate(`, `SymmetricDifferenceUpdate(`, two block-bodied `() =>` and `void h(` [CORRECTED: block-bodied `() =>` and `(int n) =>` (the typed site, `:120`, prints its parameter list) and `void h(int n = 3)` — measured, `emit csharp -o` of `lambda_none_void_delegate_1514.spy` @ df0030a06].
2. New `src/Sharpy.Compiler.Tests/Integration/TestFixtures/type_system/optional_store_wrap_sites.spy` + `.expected`. It declares `type OI = int?` and a `class Box` with `o: int?`, and has these cells:
   - wrap cells, under a RemoveNone narrowing where needed: `d = 5` (`:140`); `x += 5` (`:221`); `y ??= 42`, `b.o ??= 3` (`:997`); `arr: array[OI] = array[OI](2)`, `arr[0] ??= 9`, `arr[1] ??= 8` (`:950`); `p, q = 30, 40` (`RoslynEmitter.Expressions.Literals.cs:275`); `w ??= a`, `a` narrowed (`Some(a.Unwrap())`)
   - no-wrap controls: `z ??= Some(7)`, `v = Some(4)` (Optional values); `g, k = Some(5), 1`; `n: int | None` with `n += 1; n = 7` (nullable slot);
     - the walrus `(c := 42)` MOVES to the Phase 3 walrus fixture (S2 changes its text; Phase 0 stays byte-identical) [CORRECTED: recorded but unread at its own site — see D1 (e); the tuple-element shape `(c := 42, 1)` reaches `:275` and is the open cell]
   Stdout (python3 3.13.18 = Sharpy @ df0030a06): `6`, `15`, `42`, `7`, `3`, `9`, `2`, `70`, `6`, `3`, `7`, `4` (12, measured without the walrus).
   Check:
   - wrapped: `d = Optional<int>.Some(5);`, `x = Optional<int>.Some(x.Unwrap() + 5);`, `y = Optional<int>.Some(42);`, `b.O = Optional<int>.Some(3);`, `: Optional<int>.Some(9))`, `: Optional<int>.Some(8))`, `(Optional<int>.Some(30), Optional<int>.Some(40))`, `w = Optional<int>.Some(a.Unwrap());`
   - unwrapped: `z = Optional<int>.Some(7);` (the `Some` call itself), `g = __t0.Item1;`, `n = n.Value + 1;`
   Seed each `.expected.cs` with `// Snapshot:`, then `UPDATE_SNAPSHOTS=true` filtered to the three (`/regenerate-snapshots` step 6).
   - Commit: `test(codegen): snapshot the mutation, None-lambda, Optional store-wrap shapes`

### Phase 1: `_augmentedAssignMutations` → `IrCollectionMutation`
1. Node, `LowerStatement` arm, rewriter arm, `IrNodeFor`; sites `:181,:235,:337` read `IrNodeFor<IrCollectionMutation>(assign)?.ClrMethodName`; `:89` conjunct, checker write, table deleted; pin 87. Agreement sweep first (temporary throw; `FullyQualifiedName~FileBasedIntegrationTests` + both spy regen scripts; 0 throws quoted; not committed). [ADDED: missing step — positive control for the 0-throws absence: invert one conjunct of the derivation once, run the Phase 0 fixture, quote ≥1 throw, restore.] Tests: `IrTotalityTests` case over the Phase 0 source (total; one node per mutating store, none for `n += 2`); `ConstFoldPassTests` case: `xs += [2 + 3]` folds under `opt_const_fold`.
   - **M1** never build → the Phase 0 fixture red (alias prints `[1, 2]`) + the IR case; **M2** delete the rewriter arm → the ConstFold case red (1). Restore → green.
   - Commit: `refactor(lowering): augmented collection mutation rides the IR`

### Phase 2: `_lambdaBodyLowerings` → `IrElidedNoneBody`
1. Node, `LambdaExpression` arm (body child substituted, order kept); 3 sites; writer (`Lambdas.cs:183-187`), table, record, enum deleted; pin 86; sweep. Cells: `f = lambda: None` → node; `g: () -> str | None = lambda: None` → none (emits `() => null`, prints `True`); `k = lambda: (None)` → node on the parenthesized body. [ADDED: missing step — name the files: an `IrTotalityTests` case asserting node/none per cell AND a run fixture holding `g` (M4's CS1643 is visible only under `run`). Also one cell for a `lambda: None` ARGUMENT re-checked through `ClearExpressionType` (`TypeChecker.Expressions.Access.Calls.Overloads.cs:540`, `TypeChecker.Expressions.Access.Calls.cs:4203`): the table is `Put`, never forgotten, so a first void visit can leave an entry the final type contradicts — construct one or record that none is reachable.]
   - **M3** never build → `lambda_none_void_delegate_1514` red (snapshot; SPY0908 on run); **M4** drop the `VoidType` conjunct → the `str | None` cell red (CS1643). Restore → green.
   - Commit: `refactor(lowering): None-bodied lambda elision rides the IR`

### Phase 3: `_optionalStoreWraps` → `IrOptionalStoreWrap` / `IrOptionalWrappedElement`
1. Both kinds; the `Assignment` arm (after the mutation arm), the tuple-element arm, two rewriter arms.
   - The 5 sites read `IrNodeFor<IrOptionalStoreWrap>(assign)?.WrapAs` / `IrNodeFor<IrOptionalWrappedElement>(elem)?.WrapAs`.
   - Delete the five writes (`TypeChecker.Statements.cs:237,578,696`, `TypeChecker.Statements.Patterns.cs:1725`, `TypeChecker.Expressions.cs:988`; payload branches still type the rebinding), field, setter, getter, `MergeFrom` stanza (`SemanticInfo.cs:104,1029,1034,2397`), and the `cref`. Pin 85.
   - Agreement sweep first (the walrus fixture lands first): a temporary throw when the pass's `(node, WrapAs)` set (`OptionalType` by value) differs from the table's on any key; a walrus key compares with the walrus node's `WrapAs` (one fact under S2), a nested key (cell 4) with either. Walrus keys counted: expected 0 without the walrus fixture (each would be a #1720 site; both arms green), 5 with it. [CORRECTED: holds for `FileBasedIntegrationTests` only; `StoreTargetMatrixTests` generates Walrus × `T?` × PayloadConstant cells under `if x is not None:` (`print((x := 5))` → `((x = 5).Unwrap())` @ 5d0187ecd), which hold table walrus entries the guard never scans. Quote the count per filter: fixtures 0 → 5, matrix N (measured). Deferred row F2] Over `FullyQualifiedName~FileBasedIntegrationTests`, `FullyQualifiedName~StoreTargetMatrixTests`, both spy regen scripts; quote 0 throws and the count; not committed.
     - Positive control: invert (a)'s `is not OptionalType`, run the Phase 0 store-wrap fixture, quote ≥1 throw, restore. A throw on any fixture is a STOP, never a conjunct tuned to it.
     - [CORRECTED: as specified the sweep throws on its own Phase 0 fixture — the table holds the walrus entry for `(c := 42)` (fixture line 58, narrowed) and the pass derives none for a walrus. The revision states how walrus-keyed entries are compared (follows from the D1 (e) decision).]
   - Tests:
     - `IrTotalityTests` over the Phase 0 store-wrap source: `IrOptionalStoreWrap` on the 7 wrapped `Assignment`s, `IrOptionalWrappedElement` on `30`, `40`, neither on the controls; walrus fixture: `IrOptionalWalrusStore` on all 5, cell 4's under `IrOptionalWrappedElement`, no element wrap on 1–3;
     - over the Phase 0 mutation source: a narrowed `list[int]?` `+=` is `IrCollectionMutation`, not a wrap;
     - `ConstFoldPassTests`: `p, q = 2 + 3, 4` folds under `opt_const_fold`, wrap kept.
   - **M5** never build the two kinds → the store-wrap snapshot and `StoreTargetMatrixTests.NarrowedStore_PayloadStoreIsEmittedAsSomeConstruction_NotTheImplicitOperator` red, stdout green (implicit operator); ≥2, quote the count.
   - **M6** drop (a)'s `is not OptionalType` → `v = Some(4)` emits `Some(Some(4))`: SPY0908 CS1503 on the fixture run + the IR case red (expected, unmeasured: every corpus Optional-by-Optional rebind too).
   - **M7** add a `ConcurrentDictionary` field to `SemanticInfo` → `SideTableCount_IsPinnedAtOrBelowTheE2Ratchet` red (1).
   - **Ruled cells (§3):** `type_system/optional_walrus_tuple_element.spy` + `.expected` + `.expected.cs` (`/add-test-fixture`, `/regenerate-snapshots`: the ONE other new snapshot); `c: int? = Some(1)` narrowed: (1) `print((c := 42, 1))`; (2) `t = (c := 43, 2)`, `print(t)`; (3) `print(((c := 44), 3))`; (4) `p`, `q` narrowed `int?`, `p, q = (c := 45, 4)`, `print(p, q, c)`; (5) `if (c := 46) > 0: print(c)`. `.expected` (python3 3.12.3 = 3.13.18): `(42, 1)`, `(43, 2)`, `(44, 3)`, `45 4 45`, `46`. By direction @ 5d0187ecd (`run`): 1 wrong output → Python's; 2 ICE → runs; 3–5 worked → worked, text changes (`(c = Optional<int>.Some(4x))` ×5).
     - #1720 guard (`SnapshotCorpus_NoImplicitConversionToOptional` + GapDiscovery arm; built test DLL, scratch snapshots): HEAD 5 sites, S2 shape 0, Phase 0 0 — no allowlist row; the positive control that the own-site wrap is applied.
   - **M8** never build `IrOptionalWalrusStore` → the guard 5 sites red + 5 snapshot lines (stdout green). **M9** derive `IrOptionalWrappedElement` for a non-unpacking walrus element → that fixture red (`(Some(42), 1)`, cell 2 SPY0908). Expected, not measured.
   - Restore → green.
   - Commit: `fix(lowering): Optional store wrap rides the IR, walrus included`

### Phase 4: docs
1. Update the docs:
   - `docs/architecture.md:53` carrier list: the five new kinds ride the lowering IR beside `BinaryOpLowering` (it lists none of the three today; `MatchScrutineeLowering` stays).
   - `docs/design/lowering-ir.md` Appendix: tranche-1 rows and emitter LOC (`wc -l src/Sharpy.Compiler/CodeGen/RoslynEmitter*.cs` = 26987 @ base; reported, not forced).
   - Stale comment `ProjectCompiler.CodeGen.cs:226`.
   - [ADDED: missing step — `StoreTargetMatrixTests.cs:1541` names `GetOptionalStoreWrap` in a comment; reword when the getter is deleted (Phase 3).]
   - Commit: `docs(lowering): record P82 tranche 1 carriers and counts`

Each body cites `refs #2443` (no closing verb before an issue number) and ends with the trailer below (Phase 1 shown; the attribution lines are the ones the implementing session's harness provides) [CORRECTED: dropped a second `Co-Authored-By` line the harness does not give]:
```
Cruft: 1 #2443 · 2 −1 SemanticInfo table, −1 checker write, −4 emitter SemanticInfo reads · 3 M1–M2 red/green · 4 none · 5 none · 6 issue+recap
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01LGMKZYTnPZhvmJyZw5sgYV
```

## Testing Strategy
- Local, `Sharpy.Compiler.Tests` filtered: `IrTotalityTests|ConstFoldPassTests|SemanticInfoMergeConformanceTests|EmitterCarrierOnlyConformanceTests|EmittedOptionalConstructionConformanceTests|AugmentedCollectionClassifyTotalityTests|StoreTargetMatrixTests|OptionalResultCodeGenTests|RoslynEmitter|FileBasedIntegrationTests`, `--logger "console;verbosity=normal"`.
- Gate (lead, `/ci-gate` @ PR tip): the whole solution (incl. `ILCompilesPropertyTests`, `CsCleanPropertyTests`, Stdlib.Tests, Cli.Tests); exit 3 → `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`.
- STOP, never regenerate: any diff in an existing `.expected.cs`, spy-stdlib or spy-test C#.

## Issues to Close
- #2443 stays OPEN (class tracker). Cells, with evidence @ the Phase 3 sha:
  - node-keyed tables 88 → 85 (pin + reflection count);
  - checker `Set\w*Lowering(` 72 → 71 (grep, Current State);
  - emitter `SemanticInfo` reads of the three 12 → 0; IR read sites +12 (the walrus site is new).
- The lead posts the recap.

## Class trackers
- #2443: out-of-contract cells go to the ledger; the lead posts.

### Critical Files for Implementation
- src/Sharpy.Compiler/Lowering/ (LoweringPass.cs, IrStatements.cs, IrExpressions.cs, IrTreeRewriter.cs)
- src/Sharpy.Compiler/Semantic/ (SemanticInfo.cs, TypeChecker.Statements.cs, TypeChecker.Statements.Patterns.cs, TypeChecker.Expressions.cs, TypeChecker.Expressions.Access.Lambdas.cs)
- src/Sharpy.Compiler/CodeGen/ (RoslynEmitter.Statements.Assignments.cs, RoslynEmitter.Expressions.Access.Calls.cs, RoslynEmitter.Expressions.Literals.cs)
- src/Sharpy.Compiler.Tests/Semantic/SemanticInfoMergeConformanceTests.cs

## Implementation Checklist
Board for `/implement-plan` (lead: this session; workers: fresh Opus agents, one per phase, sequential — every phase edits `LoweringPass.cs`, `IrTreeRewriter.cs`, `SemanticInfo.cs` and the pin). Base `48c6afb3e` (dev; `src/` identical to the verified `5d0187ecd`).
- [x] P0 — Phase 0 positive-control fixtures (test-only; 3 `.expected.cs`) — worker P0 — LANDED 9b4552c18 (3/3 fixtures; #1720 guard 4/4, 0 sites; python3 = sharpyc on all 3)
- [x] P1 — `_augmentedAssignMutations` → `IrCollectionMutation`; agreement sweep + its positive control; pin 87; M1, M2; spy-stdlib/spy-test regen byte-identical (the regen task, first Lowering/CodeGen touch) — worker P1 — LANDED a7346ea97 (sweep 3667/3667 + 119 regen files, 0 throws, control 1 throw; greps 7 → 0; M1 2 red / M2 1 red; lead re-ran M1: broken 2 failed, restored 12/12; M1 reds as SPY0908 CS8598 rather than the predicted alias print)
- [x] P2 — `_lambdaBodyLowerings` → `IrElidedNoneBody`; pin 86; M3, M4 — worker P2 — LANDED 88e147aec (sweep 3667/3667 + 119 regen files, 0 throws, control 2 red; `Set\w*Lowering(` 72 → 71; `LambdaBodyLowering` 14 → 0; M3 2 red (CS0201, not the predicted CS1643) / M4 2 red; the ADDED ClearExpressionType cell: no contradicting entry reachable, traced; lead re-ran M3: broken 2 failed, restored 12/12)
- [ ] P3 — `_optionalStoreWraps` → `IrOptionalStoreWrap` / `IrOptionalWrappedElement` / `IrOptionalWalrusStore`; walrus fixture (ruled cells); sweep incl. deferred F1 (walrus read looks through the wrapped element) and F2 (walrus count per filter); pin 85; M5–M9 — worker P3 — blocked by P2
- [ ] P4 — docs (`architecture.md`, `lowering-ir.md` Appendix, stale comments) — worker P4 — blocked by P3
- [ ] Lead per wave: `git diff --stat` scope check; re-run the largest recorded mutation; ledger de-dup
- [ ] Final — build, `/ci-gate` @ PR tip vs baseline, blast-radius sweeps, staleness gates, `format whitespace --verify-no-changes`, standalone `verification-expert` — lead — blocked by P0–P4

## Revision log
- @ ef6039e33 — revised after the 1st pass (NEEDS REVISION, Dimension 4): applied owner ruling D2 option 1 (2026-11-08). Tranche is `_augmentedAssignMutations`/`_optionalStoreWraps`/`_lambdaBodyLowerings`. D1: `_optionalStoreWraps` becomes a MOVE (five arms; walrus has no reader; no shared helper); `_inferredTypeArguments` stays NOT-MOVE on the corrected facts; `_matchScrutineeLowerings` moves to tranche 2. D2: store-wrap carriers. Phase 0: store-wrap fixture. Phase 3: replaced. Counts: 72 → 71, 12 → 0. Mutations renumbered M1–M7.
- @ 5d0187ecd — revised after the 2nd pass (NEEDS REVISION, Dimension 4): owner option B (D2 addendum): the walrus write's tuple-literal reader is a key collision (the bug), fixed. (a)/(c)/(d) read `Slot(s)` via a new `SemanticBinding` input; (a) adds `not UnknownType`. Kept departure from D2's sketch ("become shared helpers"): none, argued in D1. S2: own-site wrap carried (`IrOptionalWalrusStore`, D2 second addendum); Phase 0's walrus moves to the walrus fixture; the sweep compares walrus keys.

## Verification Summary

**Result:** NEEDS REVISION (Dimension 4: a refuted `## Design Decisions` predicate behind an unconfirmed departure from owner ruling D2)
**Adequacy:** CLASS
**Verified on:** 2026-11-08 @ ef6039e33 (checkout df0030a06; compiler identical; `sharpyc` built at df0030a06)
**Plan file:** `.claude/plans/plan-eaaed2.md` (draft; NEEDS REVISION, nothing committed)

The revision is small: the owner rules the swap on the corrected D1 facts (keep it, or restore the ruled tranche `_optionalStoreWraps`/`_inferredTypeArguments`), and the two D1 rows and the Scope override line are rewritten to match. Everything else checked out.

### Corrections Made
- Scope `Override: none` → flagged: the swap departs from D2's tranche and its acceptance row (Optional-wrap `.expected.cs` first); pending owner.
- D1 `_inferredTypeArguments`: "3 of 4 writers" → 2 of 4 (`:6305` is carried by the call's recorded `GenericType`, read first at `RoslynEmitter.Expressions.Access.cs:1304`; `:1931` derivable); added the `ISemanticQuery.cs:51` reader the D2 premise missed.
- D1 `_optionalStoreWraps`: four writers → five (augmented `:578`); `??=` consults no narrowing; narrowing implied by error-free check (SPY0604/SPY0222); common obstacle is `IsAssignable`/`ClassifyStore`.
- Phase 0 check: second `() =>` → `(int n) =>`.
- Phase 0: added narrowed-member cell (`Extend(` ×6).
- Phase 1: positive control for the agreement sweep's 0-throws.
- Phase 2: name the cells' files; add the re-checked-argument lambda cell.
- Phase 3: `match o` cell must be an IR/snapshot assertion; add `match (None)`.
- Commit trailer: dropped the `Claude Fable 5.1` co-author line (harness-provided attribution only).

### Dimension verdicts (each with the probe tried)
1. **Structural — PASS.** `check_plan_refs.py`: 9 paths, 21 file:line, 1 SPY code, 48 symbols OK; the 4 unknown symbols are the proposed new nodes/helper. #2443 OPEN (`gh api`). By hand: `CodeGenContext.cs:141` `required`; 45 `IrCompilation.Empty` (17 test files); pin const `SemanticInfoMergeConformanceTests.cs:61` = 88; 72 `Set*Lowering(`; 26987 emitter LOC; `IrTreeRewriter.cs:68` default arm; `:358` `??=` return; 9 read sites.
2. **Conventions — PASS (1 warning).** No AST mutation; emitter reads an IR property (SyntaxFactory unchanged); `string` carrier respects `EmitterCarrierOnlyConformanceTests` (`AugmentedMutation` is an internal nested Semantic record, outside `SemanticInfo`'s public closure). Warning: trailer line (corrected).
3. **Architecture — PASS.** Pass runs post-merge (`BuildLoweringIr`, and the generator partition at `ProjectCompiler.Generators.cs:134`); both production `CodeGenContext`s get a real IR (`ProjectCompiler.CodeGen.cs:93`, `.Generators.cs:148`); `IrNodeFor` follows 7 existing `Ir.Index.TryGetValue(…) && node is T` sites; tables are deleted with their `MergeFrom` stanzas (no new dictionary).
4. **Correctness — NEEDS REVISION.** Swap rationales: see the D1 corrections. MOVE rows confirmed: mutation `targetType` is the value `CheckExpression` caches at `TypeChecker.Expressions.cs:158`; lambda `returnType` is the returned `FunctionType.ReturnType` (no early return in `CheckLambda`, no overwrite of a lambda's type); match writes `NullableType(object)` with the lowering (`Patterns.cs:44-49`). Residual: `Put` without `Forget` under argument re-check (Phase 2 cell added). Python (3.13.18): all 8 Phase 0 lines reproduced by the described program; Sharpy @ df0030a06 prints the same 8 and emits exactly the plan's call counts.
5. **Completeness — PASS (warnings).** Close criterion present; 3 tests per relocation; docs phase.
6. **Adequacy — CLASS.** Contract named (#2443 item 2). Read-site roster closed by construction: deleting the getter turns any missed reader into a build error. 9 sites measured by repo-wide grep; no reader outside `CodeGen/`; no test writes the three tables. The derivation axis is closed by the agreement sweep (pass vs table over every fixture + both regen scripts). `Classify` arms: 7 ops (list `+=` `*=`; set `|=` `&=` `-=` `^=`; dict `|=`) × 3 target forms (Identifier/MemberAccess/IndexAccess) × receiver narrowing (none / Optional / nullable; isinstance refused by SPY at `Statements.cs:409`). Phase 0 covers all 7 ops on identifiers, `+=` on member and index, and narrowing on identifiers; the narrowed member cell is added. The remaining cells (6 ops × member/index) go through the same `GetExpressionType(target)` read and the same string-valued emitter arm, so the sweep is their catcher. Corpus aperture measured: ~15 fixtures with a collection augmented assignment, 1 each for `lambda: None` / `match None`.
7. **Falsifiability — PASS (warnings).** See below. Inert-fix finding: NOT an inert path.
8. **Blast radius — PASS (warnings).** Sweeps named; regen after each relocation; gate is `/ci-gate`.
9. **Scope — PASS.** One group (P82 tranche 1), alone, 1 gate, W2.3 follows; D2/R-LK not re-asked; ledger-only filing; no Python-parity cell (refactor; Phase 0 Python lines are fixture expectations only).

### Adequacy
- **Contract:** named — the pass is the only producer of IR lowering properties (#2443 item 2).
- **Axes:** read sites roster-closed (deleted getter = build error; grep 9 → 0 quoted); derivation agreement closed by the per-phase sweep; Phase 0 hand cells are the positive controls, not the matrix.
- **Cure:** seam (fact materialized on the IR node) — SPY0908-as-net row.
- **Harness:** `FileBasedIntegrationTests` + `IrTotalityTests` + pin; no allowlist moves.
- **Layer:** no rung-4 rule; D1 predicates are AST kind + recorded types.

### Scope & convergence
- **Group:** one · **Alone:** yes (required: no output change, Lowering/CodeGen seam) · **Gates:** 1
- **Generator:** existing (snapshot corpus + regen byte-identity + agreement sweep); property generators' reach into these shapes unmeasured (`GenStatements.Assign` is identifier-only `+= -= *=`).
- **Trackers named:** #2443 · **Fidelity rows:** n/a

### Falsifiability
- Inert-fix (central): every one of the 9 sites emits a DIFFERENT shape when the fact is absent, and none re-derives the fact one call later. Mutation `:181/:235/:337` → read-modify-write rebind (aliasing lost: stdout AND snapshot red). Lambda `:55/:120/:226` → `() => null` / expression-bodied local function (CS error → SPY0908). Match `:32/:1036` → `switch (null)` without cast (CS error). `:89` is deleted (always true). So "index miss = not this kind" cannot leave all snapshots green, given Phase 0 covers every site (it does).
- M1 never build → Phase 0 fixture stdout + snapshot red: real. M2 delete rewriter arm → ConstFold red: real (`IrTreeRewriter` default returns typed nodes unchanged; today the opaque `Assignment` is descended, so the case is green at base). M3/M5 never build → existing fixtures red under `run`: real. M4 drop `VoidType` → `g` cell CS1643: real only in a run fixture (file now named). M6 drop `NoneLiteral` → real only as an IR assertion (corrected). M7 extra table → pin red: real (`BeLessThanOrEqualTo`).
- Agreement sweep: absence assertion (0 throws) needed a positive control (added).

### Warnings
- The swap must be ruled by the owner before `/implement-plan`; D2's acceptance row also requires the Optional-wrap positive-control snapshot. A wrap snapshot could land in Phase 0 regardless (test-only, base-generated) and is cheap.
- Filing budget / prober budget not stated (contract §10); `/ci-gate` exit-3 fallback (`.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"`) not named.
- `IrTotalityTests.OpaqueCensus_PinsTheSetOfUnmigratedAstTypes` is unaffected (its source has no mutation/lambda/match); its walker recurses `Children` generically, so the new kinds stay total.

### Missing Steps Added
- Narrowed-member Phase 0 cell; agreement-sweep positive control; Phase 2 file naming + re-check cell; Phase 3 IR-assertion cells + `match (None)`.

### Unchecked Claims
- Exactness of the derivations off-corpus (only the agreement sweep measures it); whether a `lambda: None` argument re-check can leave a stale table entry (two generic probes were refused at SPY0237); property-generator coverage of these shapes.

### Pass 2

**Result:** NEEDS REVISION (Dimension 4: D1 (e)'s "walrus has no reader" predicate is refuted; a new finding that changes a `## Design Decisions` row and an output direction, so it is not deferrable under contract §12 and the plan has not converged)
**Adequacy:** CLASS
**Verified on:** 2026-11-08 @ 5d0187ecd (compiler and `Sharpy.Core/Optional.cs` identical to ef6039e33; the drift is `Sharpy.Core` Unicode files only; `sharpyc` built at 5d0187ecd, 0 warnings)
**Plan file:** `.claude/plans/plan-eaaed2.md` (draft; NEEDS REVISION, nothing committed)

Pass-1's finding (Dimension 4, the tranche swap) is FIXED: D2 option 1 is applied, the override is recorded, and `_inferredTypeArguments` is out on the corrected facts. The new finding is in the `_optionalStoreWraps` row that the fix brought in.

#### Corrections Made
- D1 (e): marked REFUTED with the probe; the decision stays with the author/owner (carry the walrus-element wrap, or rule the fix in as an output change).
- D1 (a)/(c)/(d) slot accessor: `GetRootBinding(s).Type` → the checker's `DeclaredBindingType` (`SemanticBinding` first); the pass has no `SemanticBinding` input today.
- Phase 0 walrus control: "no reader" → recorded but unread at its own site.
- Phase 3 sweep: as specified it throws on its own Phase 0 fixture (walrus entry); the comparison must name walrus keys.
- Phase 4: `StoreTargetMatrixTests.cs:1541` comment names the deleted getter.

#### Dimension verdicts (each with the probe tried)
1. **Structural — PASS.** `check_plan_refs.py --issues`: 12 paths, 46 file:line (2 ambiguous short names, both resolved by context), 4 SPY codes, 85 symbols OK; the 6 not found are the proposed nodes/helper. `gh` lookups failed in-script; `gh api`: #2443 OPEN, PR #2484 merged (da6e8fabd).
2. **Conventions — PASS.** No AST mutation; the emitter reads IR properties only; `string`/`OptionalType` carriers sit under the conformance roots; trailer has harness-only attribution.
3. **Architecture — PASS (1 correction).** Tables deleted with their `MergeFrom` stanzas; `IrNodeFor` follows `Ir.Index.TryGetValue` (`RoslynEmitter.Expressions.Access.cs:2183`); the pass runs after `Freeze()` (`ProjectCompiler.CodeGen.cs:44-45`). Its inputs lack `SemanticBinding` (corrected).
4. **Correctness — NEEDS REVISION.** (e) refuted: walrus as an unparenthesized tuple element hits `Literals.cs:275`. `print((c := 42, 1))` prints `(Some(42), 1)` (py `(42, 1)`); `t = (c := 42, 1)` gives SPY0908 CS0029. Held: (a): `IsAssignable(non-Optional, OptionalType)` has no true arm but Unknown (CLR `IsAssignableFrom` ignores the implicit operator); parameter-rooted and closure write-through cells wrap at HEAD. (c): un-narrowed `+= <<= *=` into `int? str? float? list[int]?` are all SPY0222; `while`/early-return/`assert` narrowings wrap. (d): contextual `Some(1)` into `float?`/`object?` types as the slot (no wrap). Phase 0 store-wrap draft: 13 stdout lines equal sharpyc and the python3 3.13.18 transliteration; each of the 13 check strings occurs once in `emit csharp -o /dev/stdout`.
5. **Completeness — PASS (1 step added).** Phase 4 stale test comment.
6. **Adequacy — CLASS.** The read-site roster is closed by build (deleted getter); the derivation is closed by the sweep. The walrus finding is a reader-key-shape cell the roster missed because the key is an `Expression`, not a statement.
7. **Falsifiability — PASS (warnings).** M5: `Optional.cs:127` makes the absent wrap inert for value types (reference `null` → None is unreachable from a non-nullable payload), so stdout cannot discriminate. But a refactor's contract IS byte-identical text, so the new `.expected.cs` plus `StoreTargetMatrixTests.cs:1536` (`Contain("d = Optional<int>.Some(5);")`) are discriminating guards; ≥2 reds real. M6 real (`v = Some(4)` → `Some(Some(4))` CS1503). Sweep positive control real (the same inversion). The sweep's 0-throws is unreachable as written (walrus entry), corrected.
8. **Blast radius — PASS.** Sweeps, regen byte-identity and the `/ci-gate` fallback are named.
9. **Scope — PASS (1 warning).** One group P82 tranche 1, alone, 1 gate, W2.3 after; override `owner 2026-11-08, option 1: "your rec"` present; no owner question re-asked.

#### Warnings
- D2 ruling text (handoff §7 @ da6e8fabd) says the assignability and store classifier "become shared helpers"; D1 says none move. Keep that departure (it is argued) but name it in the Revision log/override so the owner sees it.
- (a)'s `GetExpressionType(t) is not OptionalType` is true for `UnknownType`, where the checker skips the store; add `∧ not UnknownType` or show that lowering never sees Unknown.
- The pass-1 ledger row (walrus "writer does not fire") has the mechanism wrong: it fires and is unread at its own site.

#### Unchecked Claims
- Whether `VariableSymbol.Type` ever differs from `SemanticBinding.GetVariableType` for an Optional root on the corpus (the sweep measures it once the input is named).
- Measurements: plan 41677 bytes after this pass; counts re-measured @ 5d0187ecd: pin 88 at `:61`, `Set\w*Lowering(` 72 (one lambda), emitter reads 4+3+5 = 12, 166 `.expected.cs`, 26987 emitter LOC, 46 `IrCompilation.Empty` occurrences (45 contexts + the definition).

### Pass 3

**Result:** CONVERGED (pass 2 stamped NEEDS REVISION on Dimension 4; both earlier findings are fixed; the two new findings are deferrable, and each names a catcher)
**Adequacy:** CLASS
**Verified on:** 2026-11-08 @ 5d0187ecd (`sharpyc` and `Sharpy.Compiler.Tests` built at 5d0187ecd, exit 0)
**Plan file:** `.claude/plans/plan-eaaed2.md` (the brief says commit nothing; the lead commits it to `docs/process/plans/`)

Earlier findings: pass 1 (tranche swap): FIXED, since D2 option 1 is applied. Pass 2 (D1 (e) "walrus has no reader"): FIXED under option B and S2: the collision cell is fixed, the own-site wrap is carried, the Phase 0 walrus is moved, the sweep compares walrus keys, `Slot` reads `SemanticBinding`, (a) excludes `UnknownType`, and the helper departure is in the revision log and the handoff addendum.

#### Corrections Made
- D1 (e): the S2 ruling's PR is #2490 (open, f9d70abf4), not #2485.
- D2 `IrOptionalWalrusStore`: the by-key lookup misses the nested node (F1).
- Phase 3 sweep: the "0 walrus keys before the fixture" claim holds for FileBased only (F2).

#### Dimension verdicts (each with the probe tried)
1. **Structural: PASS (1 correction).** `check_plan_refs.py`: 9 paths, 62 file:line, 4 SPY codes, 92 symbols OK; the 7 not found are proposed. The 4 ambiguous file:line refs resolve by context. `gh api`: #2443 OPEN, #1720 closed (cited only as the guard's issue), #2485 merged, #2490 open. By hand: `LoweringPass.cs:195` is the opaque default and `:198` the index write; `Literals.cs:762` value, `:777` Rebinds, `:782` assignment; `TypeChecker.Expressions.cs:1038` `return bindingType`; `SemanticInfo.cs:104,1029,1034,2397`; `StoreConversion.cs:78` cref; `ProjectCompiler.CodeGen.cs:242`, `.Generators.cs:134`, `ProjectCompiler.cs:349`.
2. **Conventions: PASS.** No AST mutation; the emitter reads IR fields only; `OptionalType` is under the `SemanticType` carrier root (`EmitterCarrierOnlyConformanceTests.cs:102`).
3. **Architecture: PASS.** No new dictionary; three tables leave `MergeFrom` together with their stanzas; `SemanticBinding` is an input, frozen at `:349`; the `Lower(` callers are 3.
4. **Correctness: CONVERGED (F1).** Claim 1: the rebind arm holds the walrus AST, and no other code wraps once the write is deleted (no fallback). Claim 2: `IrIndex` keeps one node per key (`state.Index[expression] = ir`), so a by-key read finds one of cell 4's two nodes. The plan gives no look-through, which is F1. Claim 3: `bindingType` is non-Optional under an Optional root only on the wrap branch. The void branch gives the slot, the assignable branch the value's type (Optional or Unknown, both excluded), the else branch the slot, and an error `return Unknown`. `GetExpressionType(w)` is cached at `:158`, `GetWalrusSymbol` is in `SemanticInfo`, and both survive `Freeze()`. Probes (`run`): cells 1–5 give `(Some(42), 1)`, SPY0908 CS0029, `(44, 3)`, `45 4 45`, `46`; python3 gives `(42, 1)`, `(43, 2)`, `(44, 3)`, `45 4 45`, `46`. By direction: wrong → Python, ICE → runs, worked ×3, as the plan says. Claim 7: `p, q = 45, 4` and `p, q = (c := 45, 4)` with `q` fresh are both SPY0908 CS0029 (python3 `45 4`, `45 4 45`). The plan carries this as a known cell under (b) with ledger row 19, and cell 4 narrows both targets.
5. **Completeness: PASS.** Each relocation has an IR case, a fixture and a mutation; the Phase 4 docs and the test comment are named.
6. **Adequacy: CLASS.** Read-site roster closed by the build (deleted getter); derivation closed by the sweep. No committed snapshot holds the shape: the override grep is 0, and stronger, both #1720 arms are green at HEAD with empty allowlists, so the result does not depend on spelling. A walrus target is a bare name only (SPY0143), so there is no member or subscript spelling. Stdlib and spy-test sources hold 0 `:=`.
7. **Falsifiability: PASS.** #1720 reproduced on the built test DLL, copied to scratch, whose `FixturesRoot` (`../../../../Sharpy.Compiler.Tests/Integration/TestFixtures`) held scratch snapshots only. HEAD walrus: 5 sites (`:20–26`, 42…46). S2 shape: 0. Phase 0 (my transliteration): 0. Cell-4-miss shape: 1. M8 = HEAD shape = 5. M9 is plausible (`:275` re-reads an element node).
8. **Blast radius: PASS.** Sweeps, `/ci-gate` and its fallback are named; regen runs only for the new fixtures, and any existing `.expected.cs` diff is a STOP.
9. **Scope: PASS.** One group, alone, 1 gate, W2.3 after. The override quotes "your rec", "until then option b" and "S2" (verified at 48c6afb3e and f9d70abf4). No owner question is re-asked; filing is ledger-only.

#### Deferred findings
- **F1:** D2 walrus read for the nested key ← `IrIndex` one-node-per-key (`LoweringPass.cs:198`). Fix: at `RoslynEmitter.Expressions.Literals.cs:769`, read `IrNodeFor<IrOptionalWalrusStore>(walrus)`, else `IrNodeFor<IrOptionalWrappedElement>(walrus)?.Children[0] as IrOptionalWalrusStore`. Proved by: the #1720 guard (snapshot and executing arms) on fixture cell 4, which reads 1 site (`on: 45`) when the look-through is absent, measured; plus the Phase 3 `IrTotalityTests` nesting assertion.
- **F2:** sweep walrus-key count ← `StoreTargetMatrixTests`' generated Walrus × Optional × PayloadConstant narrowed cells. Fix: quote the count per filter (FileBased 0 → 5; matrix N). Proved by: the sweep's own count run. Throws are unaffected, because these keys compare against the walrus node.

#### Warnings
- These matrix cells' emitted text also moves to `(x = Optional<int>.Some(5))`. They are not committed and no assertion pins them (`Contain(` grep), and stdout is unchanged. This is within the override's "SHAPE".
- The Phase 0 stdout list is the author's program. My transliteration reaches all 11 check strings once each, and all 5 read sites (`g = __t0.Item1;` needs `g` declared and `k` fresh).

#### Unchecked Claims
- The executing #1720 arm on the new fixture (I ran only the snapshot arm). The M9 red count. The N in F2.
- Measurements @ 5d0187ecd: pin 88 (`:61`), `Set\w*Lowering(` 72, emitter reads 4+3+5 = 12, IR read sites 3+3+5+1 = 12, 166 `.expected.cs`, 59 walrus `.spy`.
