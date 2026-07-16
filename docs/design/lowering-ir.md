# The Sharpy Lowering IR — Design

> **Status:** Design — 2026-07-14 (E1)
> **Issue:** [#1055](https://github.com/antonsynd/sharpy/issues/1055) (Workstream E, Phase 3).
> **Depends on:** B2 emitter purity ([#1039](https://github.com/antonsynd/sharpy/issues/1039)) and
> D1 per-phase metrics ([#1048](https://github.com/antonsynd/sharpy/issues/1048)) — both landed.
> **Sequences into:** E2 emitter port ([#1056](https://github.com/antonsynd/sharpy/issues/1056)),
> E3 optimization passes ([#1057](https://github.com/antonsynd/sharpy/issues/1057)),
> E4 backend decision ([#1058](https://github.com/antonsynd/sharpy/issues/1058)).
> **Roadmap:** [roadmap-2026-07.md](roadmap-2026-07.md) (Workstream E, §4.3–§4.4).
> This page is a **design**, not policy and not code. It is the reviewed artifact the E1 done-when
> requires ("design doc merged with explicit non-goals and the backend seam identified"); E2 is the
> first change that writes IR code, and it is gated on this review.

The **lowering IR** is a proposed typed, immutable middle-end tree that sits between semantic analysis
and code generation:

```
Source (.spy) → Lexer → Parser (AST) → Semantic → ValidationPipeline → [ Lowering → IR ] → RoslynEmitter → C# → .NET IL
```

Today the bracketed stage does not exist as a data structure. The lowering decisions it would hold are
instead scattered across two materialization channels feeding the emitter, plus a set of structural
transforms open-coded inside the emitter itself. This document argues for giving those decisions a
single explicit home, describes its shape, names the boundary it defines, and sketches how E2 migrates
onto it one side-table at a time.

## 1. Motivation

### 1.1 The emitter reads decisions; it must not make them

Critical Rule 2 makes `RoslynEmitter` a pure translator: it emits C# via `SyntaxFactory` and makes no
type or lowering decisions of its own. Every such decision is made during semantic analysis and
**materialized** for the emitter to read, through exactly one of two patterns:

- **Symbol-keyed** — frozen onto `Symbol.CodeGenInfo` at the `MaterializeCodeGenInfo()` boundary
  (`SemanticBinding.cs`), used when a discovered `Symbol` owns the fact (names, casing, module-level
  vs. local, constness, import kind — see `CodeGenInfo.cs`).
- **Node-keyed** — stored in a `SemanticInfo` dictionary keyed by AST-node identity
  (`ReferenceEqualityComparer`), merged at `SemanticInfo.MergeFrom` (`SemanticInfo.cs`), used when the
  fact belongs to an AST node that has no owning symbol.

This works, and B2 made it grep-enforceable. But the node-keyed channel has grown into a wide set of
parallel dictionaries, each keyed by the identity of the same AST nodes, each hand-threaded through
one merge method. That is the structure an IR replaces.

### 1.2 The node-keyed side-table inventory

`SemanticInfo` holds **24** `ConcurrentDictionary` annotation fields. One is symbol-keyed
(`_symbolReferences`, keyed by `Symbol` — find-references / rename, not a codegen fact). The other
**23 are node-keyed** — keyed by an `Expression`, `Identifier`, `FunctionCall`, `TypeAnnotation`,
`MemberAccess`, `FunctionDef`, `Pattern`, `WithItem`, or `Statement`. They are the material an IR
would subsume:

> **[Updated 2026-07-16, E2]** This table is the E1 design-time snapshot and is two entries stale.
> It already omitted Wave 3's `_staticExtensionDispatches` (`Expression` → `StaticExtensionDispatch`),
> so the **pre-E2** count was **24 node-keyed + 1 symbol-keyed = 25 dicts**, not the 24 stated above.
> **Post-E2** (this workstream) the count is **23 node-keyed + 1 symbol-keyed = 24 dicts**: E2 Phase 2
> physically deleted `_foldedIntegerConstants` (row 21), while five Phase-2 facts and the
> `_generatorFunctions` marker (row 8) are **retained as doc-commented transports** (their CodeGen
> consumers now read the lowering IR; the dicts stay for the validators / the lowering pass, deletion
> deferred to [§6.4](#6-migration-plan-sketch-e2)). See the [E2 migration state appendix](#appendix-state-as-of-e2-2026-07-16) for the full per-fact ledger.

| # | Field | Key node | What it records | In `MergeFrom`? |
|---|-------|----------|-----------------|:---:|
| 1 | `_expressionTypes` | `Expression` | resolved `SemanticType` per expression | ✅ |
| 2 | `_identifierSymbols` | `Identifier` | resolved `Symbol` per identifier | ✅ |
| 3 | `_callTargets` | `FunctionCall` | resolved `FunctionSymbol` per call | ✅ |
| 4 | `_typeAnnotations` | `TypeAnnotation` | resolved `SemanticType` per annotation | ✅ |
| 5 | `_narrowedExpressionTypes` | `Expression` | narrowed type at a usage site | ✅ |
| 6 | `_inferredTypeArguments` | `FunctionCall` | inferred generic type arguments | ✅ |
| 7 | `_memberAccessResolutions` | `MemberAccess` | `(owner, member)` for static/const access | ✅ |
| 8 | `_generatorFunctions` | `FunctionDef` | marker: function is a generator | ✅ |
| 9 | `_eventAccessNodes` | `Expression` | marker: emit `+=`/`-=` for an event | ✅ |
| 10 | `_typeReferenceNodes` | `Expression` | marker: expression denotes a type, not a value | ✅ |
| 11 | `_patternUnionCases` | `Pattern` | resolved union-case `TypeSymbol` | ✅ |
| 12 | `_patternConstants` | `Pattern` | pattern binds a module-level constant | ✅ |
| 13 | `_patternTypes` | `Pattern` | fully-resolved pattern `SemanticType` | ✅ |
| 14 | `_errorRecoveryNodes` | `Expression` | marker: `UnknownType` is expected (error recovery) | ✅ |
| 15 | `_contextManagerKinds` | `Expression` | `ContextManagerKind` for a `with`-item | ✅ |
| 16 | `_withItemSymbols` | `WithItem` | `as` variable symbol (scope already exited) | ✅ |
| 17 | `_narrowingDecisions` | `Expression` | `NarrowingDecision` for a conditional test | ✅ |
| 18 | `_binaryOpLowerings` | `Expression` | `BinaryOpLowering` strategy for `==`/`!=` | ✅ |
| 19 | `_indexAccessLowerings` | `Expression` | `IndexAccessLowering` strategy for `obj[i]` | ✅ |
| 20 | `_resolvedClrMemberNames` | `Expression` | original CLR member name (acronym casing) | ✅ |
| 21 | `_foldedIntegerConstants` | `Expression` | constant-folded integer value | ✅ |
| 22 | `_generatedStatements` | `Statement` | marker: statement produced by a source generator | ✅ |
| 23 | `_generatorBindings` | `Statement` | source-generator bindings (bracket attributes) | ⚠️ **no** |

The three broad shapes here — **resolutions** (1–7, 11–13, 16, 20), **markers** (8–10, 14, 22),
and **lowering strategies** (15, 17–19, 21, 23) — are all facts *about a specific node* that the
emitter reads back by node identity. That is precisely what a node in a typed IR carries in its own
fields, without a side dictionary and without an identity-keyed lookup.

### 1.3 The `MergeFrom` fragility, made concrete

Every one of these 23 tables must be copied, by hand, inside `SemanticInfo.MergeFrom`. That method
runs at the per-file → project boundary: since B1 unified run mode into project mode, **every** compile
goes through this merge, so a table that is written per-file but not listed in `MergeFrom` is silently
dropped before code generation reads it. There is no compiler error for the omission — the fact simply
vanishes.

The live example is entry 23: **`_generatorBindings` is written by the type checker but is absent from
`MergeFrom`.** Its aggregate accessor `GetAllGeneratorBindings()` has three consumers —
`ProjectCompiler.Generators.cs` (the generator sub-pipeline), `SourceGeneratorValidator.cs` (validation),
and `Sharpy.Lsp/DiagnosticPublisher.cs` (LSP) — so whether the omission is a live bug depends on whether
those read per-file or merged `SemanticInfo`. (A separate change is determining and, if needed, fixing
that.) For this document the point is structural, not diagnostic: a hand-maintained merge over 23
parallel dictionaries is *exactly the drift an IR makes impossible*. A single IR tree per compilation is
either constructed or it is not; there is no per-table copy step to forget. The conformance test that
asserts "every side-table appears in `MergeFrom`" (added alongside Phase 3) is the interim guardrail;
the IR is the structural cure that retires the guardrail's reason to exist.

### 1.4 The transforms the emitter still open-codes

The side tables are only half the picture. Beyond reading materialized facts, the emitter still performs
genuine **structural lowering** itself — turning one Sharpy construct into a differently-shaped tree of
C# statements:

- **Comprehension lowering** — `[f(x) for x in xs if p(x)]` into a loop or LINQ chain
  (`RoslynEmitter.Expressions.Comprehensions.cs`).
- **`defer` → `try`/`finally`** — scope-exit registration lowered to a `try`/`finally` envelope
  (`RoslynEmitter.Statements.ControlFlow.cs`).
- **Iterator / generator rewriting** — `yield`-bearing functions into iterator members
  (`RoslynEmitter.ClassMembers.Iterators.cs`).
- **Dataclass synthesis** — generated constructors, equality, and members
  (`RoslynEmitter.ClassMembers.Dataclass.cs`).

These are lowerings with no home in the side-table model, so they live as open-coded Roslyn tree-building
across the emitter's **25 partial files, ~22,000 lines** (`RoslynEmitter*.cs`). They are the reason the
emitter is large: much of it is not "translate this node to that syntax" but "restructure this construct
into this other shape." An IR is where that restructuring belongs — as explicit lowering nodes produced
before emission — leaving the emitter to do only the final, mechanical syntax mapping.

### 1.5 Thesis

An explicit lowering IR replaces (a) 23 node-identity-keyed dictionaries hand-merged in one method and
(b) structural transforms open-coded across ~22,000 emitter lines with **one typed tree** whose nodes
carry their own resolved types, bound symbols, source spans, and lowering strategies. It is the missing
home for decisions B2 already forces semantic analysis to make. It is also, not incidentally, where
optimization headroom (E3) and a potential non-Roslyn backend (E4) attach — but those are deliberately
out of scope here (see [§5](#5-explicit-non-goals)).

## 2. Shape

The IR is modeled on Roslyn's own `IOperation`: a **bound, typed, immutable tree** that sits above raw
syntax and below IL. Concretely:

- **Bound, not syntactic.** Every reference is to a resolved `Symbol` / `SemanticType`, never to a
  string to be re-resolved. Where today the emitter reads `_identifierSymbols[id]` or
  `_resolvedClrMemberNames[expr]`, the IR node instead *holds* the bound symbol or the resolved CLR
  member name in a field. Names become strings only at the very end, in the emitter, via the single
  `NameMangler` authority (B3).
- **Typed.** Every value node carries its `SemanticType` (the answer `_expressionTypes` /
  `_narrowedExpressionTypes` hold today). Type information is read from the tree, never recomputed —
  the emitter-purity property is preserved by construction because the facts are *in* the node.
- **Immutable and value-constructed.** IR nodes are immutable records built once by the lowering pass,
  mirroring the immutable-AST discipline (Critical Rule 3). No pass mutates a node in place; a
  transform produces a new node. This is what makes E3's optimization passes tractable and what keeps
  determinism (A3) a construction property rather than a runtime check.
- **Source-spanned.** Every node retains the originating `TextSpan` so diagnostics, `#line` mapping,
  and future debugging keep pointing at Sharpy source, not generated C#.
- **Explicit lowering nodes.** The IR vocabulary is *lowered* constructs, not surface syntax. A tuple
  `==` is not a generic `BinaryOp` node the emitter must re-strategize; it is an equality node that
  already carries `EqualsCallInstance` (the current `BinaryOpLowering` value). A comprehension is a
  lowered loop/projection node, not a `Comprehension` AST node the emitter must expand.

### 2.1 A worked contrast

Tuple equality today:

```
// TypeChecker records the strategy, node-keyed:
semanticInfo.SetBinaryOpLowering(node, BinaryOpLowering.EqualsCallInstance);
// … survives MergeFrom … emitter reads it back by identity:
switch (semanticInfo.GetBinaryOpLowering(node)) { case EqualsCallInstance: /* emit left.Equals(right) */ }
```

The same fact as an IR node:

```
// Lowering pass emits a node that *is* the decision:
new IrEqualityComparison(
    Left: loweredLeft, Right: loweredRight,
    Strategy: EqualityStrategy.EqualsCallInstance,
    Type: BuiltinType.Bool, Span: node.Span);
// Emitter switches on Strategy — reading a field, not a side table.
```

No dictionary, no `MergeFrom` line, no `ReferenceEqualityComparer` lookup. The strategy is a field on the
node that holds the operands. The enum values (`BinaryOpLowering`, `IndexAccessLowering`,
`ContextManagerKind`) carry over almost verbatim — the IR gives them a structural carrier rather than a
parallel table.

## 3. Boundary — the backend seam

The IR defines two interfaces, and the second one is the E4 backend seam named explicitly.

**Input boundary (Lowering pass):**

```
(post-TypeChecker AST, SemanticInfo, SymbolTable)  ──Lowering──▶  IR
```

The lowering pass runs after type checking and validation, consuming the fully-annotated AST plus the
merged `SemanticInfo` and the `SymbolTable`. It reads each materialized fact once and folds it into the
IR node it belongs to. After this pass, the AST and `SemanticInfo` side-tables are no longer consulted
by code generation (during migration they still exist for un-migrated tables — see [§6](#6-migration-plan-sketch-e2)).

**Output boundary (the backend seam):**

```
IR  ──▶  ICodeGenBackend  ──▶  { RoslynEmitter → C# syntax → IL   |   (future) direct IL via MetadataBuilder }
```

**This IR → backend interface is the seam E4 evaluates.** A backend consumes the IR and produces an
assembly; it makes no semantic or lowering decisions, because the IR already encodes them. `RoslynEmitter`
becomes the *first and, by default, only* backend — one that translates IR nodes to `SyntaxFactory`
calls. A hypothetical `MetadataBuilder` IL backend (E4's "evaluate direct IL emission" option) would be a
*second* implementation of the same interface, plugged in at the identical seam, with no changes above the
IR. The value E1 delivers for E4 is precisely this: the optionality of a non-Roslyn backend exists as a
named interface, without any commitment to build one. Per the roadmap, the default expectation at E4
remains **no-go** — Roslyn stays the test oracle, the `#line` path, and free interop verification — and
nothing in this design changes that expectation. It only makes the seam concrete enough to measure against.

## 4. What moves into IR nodes first

E2 does not move all 23 tables and all four transforms at once. The first tranche is the set of facts
that are *already* "a decision materialized for the emitter" — moving them is a mechanical re-home with a
snapshot guard, and each has a single emitter consumer:

1. **`BinaryOpLowering`** (`_binaryOpLowerings`) — strategy field on an equality node.
2. **`IndexAccessLowering`** (`_indexAccessLowerings`) — strategy field on an index node.
3. **`ContextManagerKind`** (`_contextManagerKinds`) — kind field on a `with`-item node.
4. **Static-extension dispatch** — the tag Phase 2 adds alongside `_resolvedClrMemberNames` so shadowed
   `str` methods dispatch to `Sharpy.StringExtensions` statically. It is born as a node-keyed table; in
   the IR it is a field on the (member-)call node. This is the cleanest demonstration that new lowering
   facts should land as IR node fields rather than table #24.
5. **Resolved CLR member names** (`_resolvedClrMemberNames`) and **folded integer constants**
   (`_foldedIntegerConstants`) — value/name fields on the relevant call and literal nodes.

Then the **structural transforms**, which benefit most because they currently have *no* side-table home
and are pure open-coded emitter logic:

6. **Comprehension lowering** — becomes an explicit lowered-loop/projection IR node.
7. **`defer` → `try`/`finally`** — becomes an explicit scope-guard IR node.
8. **Iterator/generator rewriting** — becomes explicit iterator-member IR nodes.

Dataclass synthesis and the resolution/marker tables (types, symbols, patterns, narrowing) migrate later;
they are larger or more cross-cutting and are sequenced after the mechanical wins prove the pattern.

## 5. Explicit non-goals

This is a v1 middle-end for code generation. It deliberately does **not** include:

- **No optimization passes.** Const folding beyond what already exists, comprehension fusion,
  devirtualization, and escape analysis are **E3** ([#1057](https://github.com/antonsynd/sharpy/issues/1057)),
  not E1/E2. The v1 IR is a *faithful* lowering — same emitted behavior, guarded by `.expected.cs`
  snapshots — with zero semantic optimization. The IR is *shaped* to make E3 possible (immutable,
  pass-friendly), but E2 adds no pass that changes output.
- **No IL backend commitment.** Naming the backend seam ([§3](#3-boundary-the-backend-seam)) is not a
  decision to build a `MetadataBuilder` backend. That evaluation is **E4**
  ([#1058](https://github.com/antonsynd/sharpy/issues/1058)), gated on E3 plateauing, with a written
  go/no-go and a default expectation of no-go. E1 commits only to the *interface*, not a second
  implementation.
- **No LSP consumption in v1.** The IR is a codegen middle-end. The LSP keeps reading the AST and
  `SemanticInfo` as it does today (hover, completion, semantic tokens, find-references all live above
  the lowering boundary). Feeding IR to tooling — the roadmap's "lazy semantic binding for LSP"
  borrowing item — is future work, explicitly out of this design.
- **No new type system.** Types come from the existing `TypeChecker` unchanged; the IR *carries* them,
  it does not infer them.
- **No wholesale AST or `SemanticInfo` replacement.** Both remain. The IR is additive and the migration
  is incremental ([§6](#6-migration-plan-sketch-e2)); there is no big-bang cutover.

## 6. Migration plan sketch (E2)

E2 is executed **side-table-by-side-table, snapshot-guarded** — the same non-disruption-by-construction
discipline the test-harness rollout uses. The `.expected.cs` snapshot corpus (~100 representative
fixtures) is the invariant: emitted C# must not change while a fact moves from a side table into an IR
node.

1. **Stand up the IR and the Lowering pass, emitting nothing new.** Introduce the IR node types and a
   lowering pass that builds them, but keep the emitter reading the existing side-tables. The IR is
   constructed and discarded (or asserted structurally) until a consumer moves — this proves
   construction is deterministic (A3) before any behavior depends on it.
2. **Migrate one table at a time.** For each side-table, in the [§4](#4-what-moves-into-ir-nodes-first)
   order:
   a. Add the fact as a field on the owning IR node; have the lowering pass populate it from the
      side-table (or directly from the same semantic computation).
   b. Switch that table's single emitter consumer from `SemanticInfo.Get…(node)` to the IR node field.
   c. **Delete the side-table and its `MergeFrom` line in the same commit** — the asymmetry that makes
      the lifecycle cheap: nothing is left half-wired.
   d. Run the snapshot suite; byte-identical emitted C# is the acceptance test. Any diff is either a bug
      or an intended change that updates the snapshot deliberately (never to mask — Critical Rule 1).
3. **Migrate the structural transforms.** Replace the open-coded comprehension/`defer`/iterator lowering
   in the emitter with lowering-pass code that produces explicit IR nodes; the emitter's job shrinks to
   translating those nodes. This is where **emitter LOC decreases** — E2's done-when.
4. **Retire the guardrails.** When the last node-keyed table is gone, the "every side-table merges"
   conformance test has nothing left to guard and is removed with its final subject; `MergeFrom` sheds
   its node-keyed copies. Symbol-keyed `CodeGenInfo` may remain (it is a different, per-symbol channel)
   or fold into the IR later — an open question for E2, not decided here.

The ordering property that keeps every step green: a table is only deleted once its consumer reads the
IR, and the snapshot suite proves the read is equivalent. No phase leaves the tree with a fact written
nowhere or read from two places permanently.

## 7. Phase and metrics slot

Add a **`Lowering`** constant to `CompilerPhaseNames` (`Diagnostics/CompilerPhaseNames.cs`), sequenced
between `TypeChecking` and `CodeGeneration`:

```
… → Type Checking → Lowering → Code Generation → C# Parsing → …
```

The lowering pass wraps its work in `CompilationMetrics.StartPhase(CompilerPhaseNames.Lowering)`, so D1's
per-phase decomposition reports it for free in `--verbose` and benchmark JSON. This gives E2 and E3 an
honest, machine-readable measurement of what the middle-end costs and — as transforms move out of the
emitter — how the `Code Generation` phase shrinks in exchange, mirroring the before/after discipline D3
applies to the `C# Parsing` phase.

## 8. Risks

- **Double-materialization during migration.** Between steps 2a and 2c for any table, the fact exists in
  *both* the side-table and the IR node. Mitigation: the delete-in-the-same-commit rule (2c) keeps the
  window to a single commit per table, and the snapshot suite proves the two sources agree before the
  table is removed. The lowering pass should populate the IR *from* the semantic computation, not by
  re-deriving — no new inference enters codegen (B2 stays intact).
- **`MergeFrom` interplay / where the IR is built.** While side-tables and the IR coexist, the IR must
  observe the *merged* project-level `SemanticInfo`, not a per-file slice — otherwise it reintroduces the
  very drift it removes. **Decision:** build the IR *after* `MergeFrom`, over the merged `SemanticInfo`,
  once per project. This yields a single IR with no IR-level merge step, and it means the un-migrated
  side-tables are read at their already-correct merged values during the transition.
- **Node identity and single-visit.** The side-tables key on AST-node reference identity; the lowering
  pass must visit each AST node exactly once and produce exactly one IR node per lowered construct, or
  facts attach to the wrong node. Mitigation: a single deterministic top-down lowering walk, with the
  determinism harness (A3) double-compiling shuffled inputs to catch any identity leak.
- **Snapshot corpus coverage gaps.** The migration is only as safe as the `.expected.cs` corpus is
  representative. Mitigation: before migrating a transform whose output no snapshot covers, add a snapshot
  fixture for it first (the harness's "fixtures before the change" pattern), so the guard exists when the
  code moves.
- **Scope creep into E3/E4.** The temptation to "just fold this constant while I'm here" during E2 would
  silently turn a faithful re-home into an optimization, breaking snapshots and blurring the phase
  boundary. Mitigation: the non-goals ([§5](#5-explicit-non-goals)) are the review checklist — any output
  change in an E2 commit is a red flag, not a win.

## Summary

The lowering IR gives the decisions B2 forces into semantic analysis a single structural home: a bound,
typed, immutable, source-spanned tree that replaces 23 node-identity-keyed `SemanticInfo` side-tables
(and their fragile hand-maintained `MergeFrom`) and absorbs the structural transforms currently
open-coded across ~22,000 emitter lines. Its input boundary is the post-TypeChecker AST plus
`SemanticInfo`; its output boundary is the `IR → backend` interface that is exactly the seam E4 would
evaluate a non-Roslyn backend against — named here, committed to nowhere. v1 is a faithful,
snapshot-guarded lowering with no optimization passes, no IL-backend commitment, and no LSP consumption;
E2 migrates onto it one side-table at a time, and only then does E3 add passes and E4 weigh backends.

## Appendix: state as of E2 (2026-07-16)

E2 (#1056) is complete. This appendix records what actually shipped, where it deviated from the §6
sketch, and what is deliberately deferred. The acceptance test throughout was byte-identical emitted
C#: the `FileBasedIntegrationTests` `.expected.cs` snapshot corpus stayed identical across every
migration commit, and the full `Sharpy.Compiler.Tests` suite (~11,460 tests) stayed green.

### Facts migrated (7) and the dictionary ledger

Seven node/marker facts had their **code-generation consumers** moved onto the IR:

| Fact | IR carrier | Dict disposition |
|------|-----------|------------------|
| `_binaryOpLowerings` | `IrEqualityComparison.Strategy` | retained transport |
| `_indexAccessLowerings` | `IrIndexAccess.Strategy` | retained transport |
| `_contextManagerKinds` | `IrWithItem.Kind` (side-lookup) | retained transport |
| `_staticExtensionDispatches` | `IrMemberAccess.ExtensionDispatch` | retained transport |
| `_resolvedClrMemberNames` | `IrMemberAccess.ResolvedClrMemberName` | retained transport |
| `_foldedIntegerConstants` | `IrConstant` | **physically deleted** |
| `_generatorFunctions` | `IrCompilation.IsGenerator` (side-lookup) | retained transport |

**Physically deleted dicts: 1** (`_foldedIntegerConstants` — a pure computation the lowering pass
re-derives via `IntegerConstantEvaluator`, so nothing else needs it). **Retained transports: 6** (the
five Phase-2 facts above plus `_generatorFunctions`). "Retained transport" means the CodeGen-facing
accessor was deleted or renamed with a `ForIr` suffix (a `grep` of `CodeGen/` for the old accessor
returns zero), but the dictionary and its `MergeFrom` line stay because a **non-codegen** reader still
needs them. Physical deletion of the six is deferred to the [§6.4](#6-migration-plan-sketch-e2)
guardrail-retirement step, so the "23 node-keyed + 1 symbol-keyed = 24 dicts" count in
[§1.2](#12-the-node-keyed-side-table-inventory) is a post-E2 fact, not the end state.

### The generator-routing fallback (Option A)

The plan's letter called for **deleting** `_generatorFunctions` in E2 (Option B), which required
repointing its two pre-lowering validators (`ControlFlowValidator`, `GeneratorValidator`) from
`SemanticInfo.IsGenerator(FunctionDef)` to the symbol-keyed `FunctionSymbol.IsGenerator`. That rewire
has **no clean channel**: those validators walk raw `FunctionDef` nodes out of class / struct / module
bodies, and the only `FunctionDef → FunctionSymbol` resolution (`TypeChecker.Definitions.cs`) depends on
`_currentClass` traversal state and line-based overload disambiguation the validators do not have;
building a new resolution map would just re-add a side-table. The team-lead's pre-authorized **Option A
fallback** was taken: the seven codegen consumers read `IrCompilation.IsGenerator`; the dict is retained
as the validators' + lowering pass's input. Its commit (39bf3e121) is titled "…`_generatorFunctions`
retained as validator transport", accurately deviating from the plan's "…retired".

### Phase-2 deviations (recorded)

- **Tables 4–5** (`_staticExtensionDispatches`, `_resolvedClrMemberNames`) landed on **`IrMemberAccess`**,
  not the `IrCall` the §4 sketch named — both facts are keyed by the member-access node, so the member
  node is their natural carrier. (The table-4 commit message says "IR call node"; the implementation
  uses `IrMemberAccess`.)
- **`_withItemSymbols`** (the `with … as` variable) was **not** folded onto `IrWithItem`: it has no
  codegen consumer (it is an LSP / `ISemanticQuery` fact), so `IrWithItem.AsVar` is left `null` and the
  dict is untouched.
- **`IrWithItem` and the generator fact are side-lookups**, not fields on a typed `with`/function IR
  node (`WithItem` and the routing marker are not `Expression`/`Statement` AST nodes). Typed carriers
  wait on suite/definition-level IR — post-E2.
- One semantic unit test was repointed from a deleted accessor to its `ForIr`-suffixed sibling.

### Structural transforms migrated (2)

- **Comprehensions → `IrLoweredLoop`.** The lowering pass now owns the decisions the emitter used to
  compute inline: collection kind, result type, the single-`for` clause, the D4 sized-source capacity
  decision (`Capacity: IrExpression?`), and the spread-vs-add flag; `SingleForClause` and
  `IsSizedComprehensionSource` moved to `LoweringPass.Comprehensions.cs`.
- **`defer` → `IrScopeGuard`.** A `DeferStatement` now lowers to an explicit scope-exit guard instead
  of an opaque wrapper; the emitter emits the `try { remainder } finally { deferred }` envelope.

**Phase-3 scoping — what moved vs. what stayed.** Only the **decisions** (what to emit) moved onto the
IR. The **mechanical, state-dependent emission** stays in the emitter: temp-variable naming
(`GenerateTempVarName`), statement hoisting (`_hoistedStatements`), loop-variable scope versioning
(`SaveScope`/`RestoreScope`), the suite-split + LIFO nesting for `defer`, and every sub-expression
`GenerateExpression`. That split is exactly what keeps output byte-identical. A **full** mechanical
migration — folding the guarded remainder onto `IrScopeGuard`, and folding temp naming onto the IR so
the emitter is a pure translator — requires **IR-owned name allocation** (suite-level lowering and a
naming authority in the middle-end), a deliberate post-E2 question.

### Null-IR codegen paths

The IR is built once per project in the main compile path. The REPL, the source-generator sub-pipeline,
and the direct-emitter unit tests construct a `CodeGenContext` **without** an IR. For the migrated
structural transforms the emitter therefore reads the decision from the IR **when present** and
otherwise recomputes it via the shared `LoweringPass` helpers (the same single source of truth the
lowering pass uses), so those paths stay byte-identical too. Giving every codegen entry point an IR is
future work.

### The `IrIndex` scaffolding and its exit criterion

While the emitter still dispatches over the AST, each migrated fact is read by looking the AST node up
in the reference-keyed `IrCompilation.Index` (plus the `WithItems` / `GeneratorFunctions` side-lookups
for non-`Node` facts). This is transitional scaffolding, not side-table #25: it is owned by the
`Lowering` component, built once post-`MergeFrom`, and never itself merged. Its **exit criterion** is
IR-driven emitter dispatch per construct category (the emitter switching on IR node type rather than
AST node type); Phase 3 starts that for the migrated transforms but the emitter still enters through
the AST, so the index remains.

### Emitter LOC — the "decrease" done-when is not met (and why)

| Point | `wc -l src/Sharpy.Compiler/CodeGen/RoslynEmitter*.cs` |
|-------|------|
| pre-E2 baseline | **22,241** |
| post-Phase-2 | **22,456** (+215) |
| post-Phase-3 (this workstream) | **22,457** |

#1056's "emitter LOC decreases" done-when is **not met**: the final count exceeds the baseline. The
cause is structural, not a shortfall of effort. Phase 2 **added** ~11 `GetIr*` reader helpers to the
emitter (the cost of reading facts from the IR by node identity), which outweigh the decision logic the
transforms removed (`SingleForClause`, `IsSizedComprehensionSource`, and the inline capacity block are
~60 lines). Under E2's twin constraints — **byte-identical output** and **decisions-only migration**
(stateful emission stays emitter-side) — the emitter cannot shrink: it still performs all the mechanical
emission, and now also reads the IR. The genuine decrease path is the **thin-translator** end state
(IR-owned name allocation, emitter switching on IR nodes), which is deliberately post-E2. Recorded here
rather than forced: no deletions were made to fake the number.

### Not done in E2 (post-E2 follow-ups)

Resolution/marker/narrowing side-tables (types, symbols, patterns, narrowing) and dataclass synthesis
are unmigrated; the six retained transports await §6.4 physical deletion; typed `with`/function/`defer`
IR nodes (folding the side-lookups and the guarded remainder onto the tree) and IR-owned name
allocation are the path to a genuinely thinner emitter and the emitter-LOC decrease.
