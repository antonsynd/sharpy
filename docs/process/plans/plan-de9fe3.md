<!-- Verified by /verify-plan on 2026-08-28 @ 6e2b68812 -->
<!-- Verification result: PASS WITH CORRECTIONS (re-stamped 2026-08-28 after owner rulings: C# augmented-narrowing rule; widened #1666 matrix confirmed) -->
<!-- Adequacy: CLASS (Batches 0–6); N/A for Batch 7 -->

# Remediation round plan — 2026-08-28

Scope: **10 open issues** — #1649 #1633 #1671 #1666 #1670 #1669 #1668 #1640 #1672 #1614 — batched per the owner rulings posted 2026-08-28 (§1). Parked, no work this round: #1099 #1101 #1102 #1103 #1592 #1611 #1619 (#1611/#1619 carry the `parked` label; #1020/#1021 skipped by owner). Every issue was read in full — bodies and comments — and four explorer censuses were taken at **`6e2b68812`** (dev, tree clean). "Measured" below means `sharpyc run` or a test count at that sha unless stated; "probe" means a measurement the batch must take before its first fix commit.

Batches retire a **defect class**, not the member cells; each section names the contract, the cell matrix, the standing harness (or the phase that adds one), and the mutation that turns every new guard red. Read `docs/design/verification-contract.md` first; §9's shared-tree block goes verbatim into every agent prompt.

Seven issue premises were corrected by the censuses (the plan section is authoritative over the issue body; post each as a comment when the batch opens):

| Issue | Correction |
|---|---|
| #1633 | Option (b) is **not** a rename: six emitter reads (`Constructors.cs:388`, `Methods.cs:153/162/188`, `TypeDeclarations.cs:736/802`) consume fields that exist only after `MaterializeCodeGenInfo` bridges them onto the Symbol; `SymbolSerializer` (static, no binding in reach) serializes `Symbol.CodeGenInfo`, and `ProjectCompiler.Initialization.cs:108-164` exists solely to re-establish the dual write on cache restore. `VariableSymbol.Type` (`Symbol.cs:187`, `MaterializeVariableTypes`) is the second cell of the same class. |
| #1666 | The class is the checker's promotion rule, not `//`/`%`: `PrimitiveCatalog.GetPromotedType` has no "at least `int32`" floor although `arithmetic_operators.md:322` Key Rule 1 states one, so `int8 + int8` is typed `int8` while C# produces `int` — every narrow store of `+ - * & \| ^ << >>`, of unary `-`/`~`, of plain `**`, and of mixed narrow widths (`int16 = int8 + int16`) has the same CS0266 shape (no cast is emitted for any operator: zero `SByteKeyword/ShortKeyword/UShortKeyword/UIntKeyword` in CodeGen). `divmod` has no compiler handling at all (binds `Divmod(int,int)`) — and is already honest: `q: tuple[int8, int8] = divmod(a, b)` is SPY0220 naming `tuple[int32, int32]` at HEAD. [CORRECTED: verifier measured `n: int8 = -a`, `~a`, `a >> b`, `a ** b`, `n: int16 = a8 + b16` all SPY0908/CS0266 @ 6e2b68812 (`run`); unary and `**` are typed by arms the binary-op floor does not reach — see Batch 1] |
| #1671 | Second cell is structural equality, not contextual typing: literal-inferred `list[int]` has `GenericDefinition = null` (`Literals.cs:108-112`), annotation-resolved has the symbol, `GenericType.Equals` compares it, and the **invariant** variance arm (`Utilities.cs:1144-1146`) rescues only one level — hence `dict[str, list[list[int]]]` fails while `list[list[list[int]]]` (covariant) passes. Tuple literals never consult `_expectedType`; `_expectedType` is never pushed into nested elements (`Literals.cs:82/139/200/265`); there is no `return`-position emitter setter. |
| #1670 | `ResolveTypeAnnotation(bare "list", bareGenericFillsFromContext: true)` returns a `UserDefinedType` wearing the **open** generic symbol (`TypeResolver.cs:286-300`), which `MapSemanticType` emits as `global::Sharpy.List` with no arguments — that is the CS0305; the self-matching positional arm (`Patterns.cs:452-476`) never reads `GetPatternType` anyway. `ClassifyTypeTestAnnotation` (`Utilities.cs:217`) already documents itself as covering "a match class pattern" but has no pattern caller. The emitter erasure switch (`Patterns.cs:833-838`) duplicates `TryMapBuiltinCollectionToNonGenericInterface` (`Access.cs:1202`), whose doc falsely claims to mirror it. |
| #1668 | Already modeled at HEAD: `for_else`/`while_else` blocks exist, `KnownRed` is empty (f2d5270b7), `DefinitelyRunBeforeSibling` lists both. Residue: `DefiniteAssignmentMatrixTests` has one axis (read-site kind) and no block-kind or write-kind axis, and no mutation record exists. |
| #1649 | Both control plans read facts @ e02f39fbb for #1641, which is fixed — `/verify-plan` Dimension 1 would refute both arms, so the pair is non-discriminating by the issue's own 2026-08-26 lesson. `compiler-audit` names 9 of the 10 allowlists (misses `deliberately-permissive-allowlist.txt`). |
| #1672 | H2b census @ HEAD is 9,545 bundles / 10,285 stray `.spy` (not 31,532); `TypePattern.BindingName` has **8** readers (adds `UnusedVariableValidator.cs:345`, `LocalNameCollisionValidator.cs:155`); 3 of the 4 "identifier-only" store positions are `string`-typed in the AST (parser SPY0101), only the comprehension target is an `Expression` (semantic SPY0239 under a tuple-unpacking code); `SourceGlobConformanceTests` is blind to `ProjectConfig.ResolveGlobPattern` (a `Matcher`, no literal); `DiagnosticCodeEmissionSiteTests` sees 369 of 386 codes (6 in neither arm incl. an undocumented `// Deprecated` label). |

---

## 1. Owner rulings — ALL 5 RESOLVED 2026-08-28

Posted as comments (authoritative; do not re-ask):

| # | Issue | Ruling |
|---|---|---|
| Q1 | #1670 — [comment](https://github.com/antonsynd/sharpy/issues/1670#issuecomment-5459159456) | Class patterns are **static**, like `isinstance` (C# model: static type in the pattern, runtime `is`, no reflection). **One classifier** shared by `isinstance` and class patterns: `object` subject → erased `Sharpy.IList` test + erased capture surface; `list[int]` subject → `Sharpy.List<int>`; impossible subject → static refusal; `case list[int](xs)` opens only with #1619. |
| Q2 | #1672 E2 — [comment](https://github.com/antonsynd/sharpy/issues/1672#issuecomment-5459159547) | Match Python: comprehension target and `with … as p.x` **supported** via `GenerateStore`; `except E as p.x` and `(p.x := 5)` **refused** with named parser diagnostics (python3 SyntaxError for both, verified). |
| Q3 | #1672 — same comment | `__call__` **yes** (node-keyed lowering; user code still cannot spell `obj.__call__()` per `dunder_invocation_rules.md`); `del` statement **refused** with a named parser diagnostic — `del x[k]` steers to `x.pop(k)`, `del name` refused (Axiom 1); spec row. |
| Q4 | #1614 — [comment](https://github.com/antonsynd/sharpy/issues/1614#issuecomment-5459159709) | **Graduate now.** Attribute/index targets in; `list *=` in; mutation call on every TFM (C# 14 operator only as an additive `#if NET10_0_OR_GREATER` nicety, not this round); isinstance-narrowed receivers stay refused with the #1615 steer; spec + SPY0478 retirement + feature-lifecycle species note at graduation. |
| Q5 | #1633 — [comment](https://github.com/antonsynd/sharpy/issues/1633#issuecomment-5459159833) | **Option (b) now**: delete `Symbol.CodeGenInfo`; `SemanticBinding.GetCodeGenInfo` is the only accessor; the dual-write assertion disappears. **Reproduce the poisoner first** by tracing `SetCodeGenInfo` on `IsBuiltinSymbol` targets across the Analyze-path suites with the clone removed. |

Standing rulings inherited from prior rounds and still binding here: R5 #1640 (widen the bridge, typed member seam, fidelity axis + DP ratchet, own batch, sweeps before/after), R8 #1656 (Sharpy's DA precision is kept), R11 #1662 (one integer predicate), R12′ #1649 (controls are files with facts read at a named sha and a known verdict).

## 2. Recurring meta-classes and their standard cures

| Meta-class | Instances this round | Standard cure |
|---|---|---|
| **A type decision made in CodeGen** (Rule 2) | #1671 (`_targetTypeContext`, 11 expression/pattern `MapType(annotation)` sites), #1670 emitter half (raw annotation in the self-matching arm; hand-rolled erasure switch), #1666 (emitter would have to inspect operand widths to cast) | The checker records the fact node-keyed (`MergeFrom`); the emitter reads it; a ratcheted scan makes the annotation route unwritable in expression/pattern generators |
| **Silent wrong behavior** | #1669 (finally before else), #1666 (narrow ops typed narrow, run wide), #1671 (false SPY0220 at depth 2), #1640 (Unknown on CLR properties / non-generic / struct / CLR-cased) | Fix first, then a conformance sweep over the sibling cells |
| **A compilation mutates state it does not own** | #1633 (materialization writes shared Symbols; the clone is a workaround whose guard is vacuous) | Per-compilation ownership by construction: the binding is the only store; a reflection-enumerated master-snapshot guard |
| **Position/route missing a shared check** | #1640 (three receiver arms end in `DeliberatelyPermissive`), #1670 (five pattern `ResolveTypeAnnotation` sites bypass the classifier), #1672 E2 (six store dispatches, four positions outside `GenerateStore`), `__call__` (two "not callable" arms), A1.6 (5 remaining switches) | Move the check to the shared seam; a matrix so a new position cannot skip it |
| **Hand-enumerated lists with no totality check** | `compiler-audit` allowlist roster, `DiagnosticCodeEmissionSiteTests` inclusion regex, `QualifiedBareSpellingConformanceTests` scenario roster, `CfgStatementTotalityTests` name sets, `IsExplicitlyMappedCollectionReceiver`, `StaticStateConformanceTests` rule 2 (`ConcurrentDictionary` blind spot) | Enumerate by reflection/glob/source-scan; N/A cells declared, never skipped |
| **Instruments that pass vacuously** | 29 `passed > total / 3` assertions (9 files), `SharedRegistryOwnershipTests` (green with the clone removed), `SemanticTypeFidelity` cells `Stack[int].count-correct` / `peek-nested` (Unknown satisfies them), `StaticStateConformanceTests` stale entries (soft warning), `RoslynEmitterStatementTests:965` (substring only; comment documents the wrong order as intended) | Fail loudly; positive control per absence assertion; mutation-test the guard |

## 3. Batch structure and sequencing

| Wave | Batches | Why this order |
|---|---|---|
| 0 | **Batch 0** (#1649 §0.1 control pair rebased on #1666, §0.2 census) | The instrument is calibrated on a defect that is still live — must run **before Batch 1 fixes #1666** |
| 1 | **Batch 5** (#1633 option b, #1672 H1.4/H2b) | Poisons every Analyze-path measurement the later batches rely on; the clone's identity invariant complicates every registry change |
| 1′ | **Batch 0 §0.3** (`/verify-implementation` control pair: clone commit = inert arm, Batch 5 = real arm) | Needs a landed real fix and a known-inert one |
| 2 | **Batch 1** (#1671, #1666, #1670 emitter half) | The annotation-consumption ratchet must exist before Batch 2 records pattern facts |
| 3 | **Batch 2** (#1670 checker half, #1651 subsumption sibling, pattern matrix, `BindingName`) | Drains the ratchet to 0 |
| 4 | **Batch 3** (#1669, #1668, E2 stores, `__call__`, `del`, DA axes, totality guards) and **Batch 6** rows C1–C7 in parallel under §9 (disjoint files: Batch 3 owns `Analysis/ControlFlow`, `Parser`, `CodeGen/*ControlFlow*`; Batch 6 owns test infrastructure, skills, `QualifiedBare*`) | |
| 5 | **Batch 4** (#1640) | Largest blast radius; whole gate + 3 sweeps before/after (before = control) |
| 6 | **Batch 7** (#1614 graduation) | Changes program output for alias cells — its regen and fixture diff are a *ruled* change, isolated last |

Regen (verification-contract §7): the first Semantic/CodeGen-touching commit of each wave is followed by a regen commit (spy-stdlib C#, spy-test C#, stdlib docs) or "regen: no diff @ sha". Batch 7's regen is a *ruled* diff.

Commit gate for every batch: `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` (whole solution) @ the closing sha, counts `passed/failed/skipped @ sha (measured)`, plus the three GapDiscovery sweeps (`InteropConformance`, `MetamorphicCorpus`, `DifferentialExecution`) for Batches 1–4 and 7. A red is attributed only after a control run at the base commit.

---

## Batch 0 — Calibrate the instruments: #1649

### Context
#1649 was reopened because the round was graded without Wave 0's calibration and 9 of 27 closes proved unsupported. The three deferred controls: `/verify-plan` on a known-CELL and known-CLASS plan, `/verify-implementation` on a known-inert and a known-real fix, and the `/compiler-audit` census against a hand `wc -l`.

### Current State (@ 6e2b68812)
- `.claude/plans/controls/control-cell.md` / `control-class.md`: facts read at e02f39fbb, subject #1641 (`GenerateSliceSpec` `(int? )` trivia) — **fixed since**; a verifier reading HEAD refutes both on Dimension 1, so neither arm exercises Dimension 6 (adequacy). Both-arms-red is as non-discriminating as both-arms-green (#1649 comment 2026-08-26).
- `.claude/skills/compiler-audit/SKILL.md:55-57,:200` enumerate nine allowlist paths by literal; ten `*allowlist*.txt` exist under `src/` — `src/Sharpy.Compiler.Tests/Conformance/deliberately-permissive-allowlist.txt` is uncounted (the same hand-list meta-class as §2 row 5).
- A known-inert fix with a vacuous guard exists in history: a295809bc + f70beaa25 (`CloneForCompilation`; the 2026-08-28 audit showed `SharedRegistryOwnershipTests` green 3/3 with the clone's only call site removed). A known-real fix will exist after Batch 5.

### Defect Class
- **Violated contract:** a control has a known answer at the sha it is run against.
- **Meta-class:** instruments that pass vacuously → controls first; hand-enumerated roster → enumerate by glob.
- **Sibling cells:** the three skills × {red arm, green arm}; the audit's roster × {every `*allowlist*.txt`}.
- **Standing harness:** none — the controls *are* the harness for the skills.
- **Owner rulings:** R12/R12′ on #1649.

### Adversarial Review
- **Alternative root cause:** the skills discriminate but the controls were never run. Discriminator: running them.
- **How this could be inert:** rebasing the control pair on a defect Batch 1 has already fixed. Order: §0.1 runs before Batch 1's first commit (Wave 0), on the tree at `6e2b68812` or the Batch 5 closing sha (Batch 5 does not touch arithmetic).
- **Blast radius:** none (no code change except the skill's roster line).

### Phase 0.1: `/verify-plan` control pair on a live defect
**Goal:** a CELL plan and a CLASS plan for #1666, facts read at HEAD, known verdicts in their headers; the skill returns CELL / CLASS respectively.
**Acceptance:**
- `control-cell.md` (#1666: "cast in the `%=` augmented arm only", header `Known verdict: CELL`) → `/verify-plan` reports **CELL** on Dimension 6.
- `control-class.md` (#1666 as Batch 1 §B below: promotion floor + `NarrowTo` + matrix + spec, header `Known verdict: CLASS`) → **CLASS**.
- Both transcripts' verdicts and the sha posted on #1649. If the two verdicts agree, the skill goes back to `/verify-plan`-style revision before it grades this round.
#### Tasks
1. **Rewrite the control pair on #1666 @ HEAD** — `.claude/plans/controls/control-cell.md`, `control-class.md` (facts: `Operators.cs:1250-1259` no cast, `PrimitiveCatalog.cs:218-255` no floor, `arithmetic_operators.md:322`). Commit: `chore(plans): control pair rebased on #1666 at HEAD (#1649)`
2. **Run `/verify-plan` on each; post verdicts on #1649.** No commit.

### Phase 0.2: `/compiler-audit` census cross-check
**Acceptance:** the skill's Class-contract table equals a hand `wc -l` (minus blank/comment lines) of **all ten** allowlists @ the same sha; the roster is a glob, not a list. Mutation: add an empty `x-allowlist.txt` under `src/` → the census count of files changes without a skill edit.
#### Tasks
1. **Roster by glob** — `.claude/skills/compiler-audit/SKILL.md:55-57,:200`: `find src -name '*allowlist*.txt' -not -path '*/bin/*' -not -path '*/obj/*'`; add `DeliberatelyPermissive ratchet` to the table row. Commit: `docs(skills): compiler-audit census enumerates allowlists by glob (#1649)`
2. **Run the audit; post the table + hand count on #1649.**

### Phase 0.3: `/verify-implementation` control pair (after Batch 5)
**Acceptance:** run in a worktree at the Batch 5 closing sha against a two-plan stub: arm A = a295809bc+f70beaa25 (Agent 3 must report the guard VACUOUS — clone call site removed, `SharedRegistryOwnershipTests` stays green); arm B = Batch 5 Phase 5.1–5.2 (Agent 3 must report `broken → red`). Both verdicts posted on #1649; if they agree, the mutation step in Agent 3's brief is vacuous and the skill is revised before it grades this round.

### Issues to Close
- #1649 — closed by Phases 0.1–0.3; **criterion:** three discriminating verdict pairs recorded on the issue with shas.

---

## Batch 5 — Compilation-state isolation: #1633 (option b), #1672 H1.4/H2b

### Context
`MaterializeCodeGenInfo` writes `symbol.CodeGenInfo` (`SemanticBinding.cs:481`) on whatever `Symbol` the per-file binding holds — including the shared registry's own `FunctionSymbol`/`TypeSymbol` instances that `SymbolTable.PopulateBuiltins` defines into every global scope and that `CodeGenInfoComputer.SetCodeGenInfo` (`:37`, no guard of any kind, 12 call sites) can target. The 2026-08-27 round papered over it with a per-compilation registry clone whose guard is vacuous and whose identity invariant (#1667) already bit once. Option (b) removes the mutable field so there is nothing to clone for.

### Current State (@ 6e2b68812)
- `Symbol.cs:162` `public CodeGenInfo? CodeGenInfo { get; internal set; }`. Readers by group (explorer A1): **G1** six emitter sites reading bridged-only fields (`ForwardingConstructors`, `OverridesClrBaseMember`, `StripsOverrideKeyword`, `ImplementsInterfaceMethod`, `SynthesizedInterfaces`, `SelfInterfaceBridges`) — `Constructors.cs:388`, `Methods.cs:153/162/188`, `TypeDeclarations.cs:736/802`, with `Constructors.cs:383-387` explaining why they bypass the binding; **G2** three binding-first fallbacks (`RoslynEmitter.cs:719`, `TypeSyntaxMapper.cs:521-522`, `:836-837`); **G3** `Patterns.cs:172` (no binding fallback); **G4** writers `SemanticBinding.cs:481`, `ImportResolver.Symbols.cs:214` (direct write on a re-export clone; `DualWriteAssertions` skips `IsReExport`, so unchecked), guard read `CodeGenInfoComputer.cs:129`; **G5** `Project/SymbolSerializer.cs` six serialize (`:150/:234/:265/:323/:351/:379`) + six deserialize (`:574/:644/:685/:808/:837/:869`) sites, codec `:483-503`/`:896-916` (12 of the record's fields round-trip; five do not — the three bridged collections `ForwardingConstructors`/`SelfInterfaceBridges`/`SynthesizedInterfaces` plus `OverridesClrBaseMember` and `IsCompileTimeConstant` [CORRECTED: verifier read `CodeGenInfo.cs` — 5 unserialized fields, not 4]), `ProjectCompiler.Initialization.cs:108-164` re-registers deserialized `CodeGenInfo` into the binding "to maintain dual-write consistency". `IncrementalCompilationCache.CurrentSchemaVersion = 30` (`:118`). LSP: zero readers.
- `MaterializeCodeGenInfo` (`SemanticBinding.cs:449-500`) folds five side-tables (`_clrBaseOverrides`, `_forwardingConstructors`, `_selfInterfaceBridges`, `_synthesizedInterfaces`, `FunctionSymbol.ClrMethodName`) into `effective` and writes it to the Symbol only — `_codeGenInfo` keeps the unbridged record. `MergeFrom` (`:508+`) carries all five tables, `TryAdd` (first writer wins).
- Sibling materialized fields: `VariableSymbol.Type` (`:411`, `_variableTypes`, `AssertVariableTypeConsistency`), `TypeSymbol.BaseType/BaseTypeRef/Interfaces` (`:396-402`, `AssertInheritanceConsistency`). `RemoveCodeGenInfo`/`RemoveVariableType` have zero callers.
- `DualWriteAssertions` (`:19/:127/:168`): one production caller each in `FileCompilationPipeline.cs:62/:178/:179`; ~25 test arms in `DualWriteAssertionsTests.cs`.
- Clone: `BuiltinRegistry.CloneForCompilation` (`:163`), copy ctor `:92-157` (deep-copies `_types/_functions/_interfaceSymbols`, empties `_clrTypeCache`, **shares** `_discovery` and `_clrTypeMapper` `:94-96` under a comment ("data-only after construction") that is false for `CachedModuleDiscovery._moduleTypeSymbols` (`:27`, lazily populated `TypeSymbol`s) and `ClrTypeBridge._interfaceSymbolCache` (`:765`, same). One production caller `CompilerApi.cs:612` (cache-hit path only — the **first** use hands out the master uncloned). `IsBuiltinSymbol` `:1182-1194` reference identity, 15 callers [CORRECTED: grep @ 6e2b68812 counts 15, not 16].
- Guards: `SharedRegistryOwnershipTests` (green with the clone removed — measured 2026-08-28); `SharedAnalysisContextDifferentialTests.cs:143` (4-way parallel vs fresh); `StaticStateConformanceTests` (static fields only; rule 2 excludes `ConcurrentDictionary`; stale entries are a **soft** `WriteLine`, `:206-218`). Analyze-path corpus drivers on one `CompilerApi`: `FrontEndParityTests.cs:145`, `MetamorphicCorpusSweepTests.cs:584-586`, `InteropConformanceTests.cs:87/:555/:1267`, `DiagnosticSweepTests.cs:15`.
- H1.4: `ClrTypeHelper.ResolveGenericInstanceMethods` (`:79`) returns the cached `MethodInfo[]` by reference; `GetMemberNameSurface` (`:114`) and `GetExtensionMethodNames` (`:160`) return the cached `HashSet<string>` behind an `IReadOnlyCollection` face; `ClrExtensionMethodResolver._byName` is a `Lazy<Dictionary<…>>` (rule 2 blind).
- H2b: 9,545 bundles / 10,285 stray `.spy` under fixture `bin/` (newest stamp 20260827-214702; both 2026-08-28 gates wrote none). No cleanup script under `build_tools/` (5 `.sh`, 12 `.py`); `IntegrationTestBase` never touches fixture `bin/` (all temp GUID dirs).

### Defect Class
- **Violated contract:** a compilation writes only state it owns; a `Symbol` instance is never a materialization target — the `SemanticBinding` is the only store for compilation-scoped facts.
- **Meta-class:** "a compilation mutates state it does not own" → per-compilation ownership by construction + reflection-enumerated snapshot guard.
- **Known member cells:** #1633; #1672 H1.4 (shared caches vend mutable symbols/arrays); H2b (census).
- **Sibling cells:** materialized field {`CodeGenInfo`, `VariableSymbol.Type`, `BaseType`, `BaseTypeRef`, `Interfaces`} × shared owner {`BuiltinRegistry._types/_functions/_interfaceSymbols/_clrTypeCache`, `CachedModuleDiscovery._moduleTypeSymbols`, `ClrTypeBridge._interfaceSymbolCache/_typeCache`, `ClrTypeHelper` three caches, `ClrExtensionMethodResolver._byName/_ancestorCache`} × entry point {`Analyze`, `AnalyzeDocument`, `AnalyzeProject`, `Compile`, first-use (uncloned master)}.
- **Standing harness that should have caught it:** `SharedRegistryOwnershipTests` (vacuous), `StaticStateConformanceTests` (wrong axis: static fields). Phase 5.1 adds `MasterRegistryImmutabilityTests`.
- **Owner rulings:** Q5 (#1633 comment 2026-08-28); R9 superseded.

### Adversarial Review
- **Alternative root cause:** concurrency. Discriminator (never run last round): two *sequential* `AnalyzeDocument` calls through one `CompilerApi` with the clone bypassed — `builtin_shadow_function_called.spy` then any file. Phase 5.1 runs it; if it does not throw, the trace names the real poisoner shape (a `from … import` re-export? `TryDefineFromImport` at `ImportResolver.Symbols.cs:240` [CORRECTED: no `ProcessFromImport` exists in that file]? a module-level property?) and the positive control is rewritten on that shape. **No fix commit lands until the guard is red.**
- **How the fix could be inert:** (i) deleting the field but keeping `MaterializeCodeGenInfo` writing bridged fields somewhere the emitter reads late (a new fallback); (ii) the cache-restore path re-introducing a Symbol-held copy; (iii) the clone left in place masks any remaining writer (`_moduleTypeSymbols`, `_interfaceSymbolCache`). Probe: the snapshot guard runs with the clone **removed** (Phase 5.3) and must stay green.
- **Before/after by direction:** no program is newly refused; every `.expected.cs` snapshot is byte-identical (the six G1 sites must read the same bridged values — a snapshot diff in `ForwardingConstructors`/`override`/`virtual`/interface lists is the tell).
- **Blast radius:** LSP (`Sharpy.Lsp.Tests`, singleton `CompilerApi`), every Analyze-path sweep, incremental cache (`WarmBuildDiagnosticFidelityTests`, `MultiFileDeterminismTests`, warm/cold harness), `Sharpy.Cli.Tests` project builds.
- **Generated artifacts:** none expected; regen commit or "no diff @ sha" after Phase 5.2.

### Design Decisions
- The bridged record is written **back into `_codeGenInfo`** (`_codeGenInfo[symbol] = effective`), so `GetCodeGenInfo` returns exactly what the six G1 sites read today. `MaterializeCodeGenInfo` keeps its name and its freeze point; its doc says "fold" not "write to Symbol".
- `VariableSymbol.Type` stays (the checker reads it everywhere); the class contract is enforced by the snapshot guard, not by deleting it. The guard covers every mutable Symbol field by reflection so `Type`/`BaseType`/`Interfaces` writes onto a master symbol fail the same way.
- The clone is retired **only** when the snapshot guard is green with it removed (Phase 5.3); if red, the red names the writer, the clone stays, and the writer gets its own issue — that outcome is reported, not hidden.
- `SymbolSerializer` gains a `SemanticBinding` parameter on both directions rather than a side-table return: the deserializer writes `binding.SetCodeGenInfo` itself, which deletes `Initialization.cs:108-164` entirely. Wire format unchanged → no schema bump; a warm-restore test proves the round-trip.

### Phase 5.1: reproduce the poisoner; the class guard goes red
**Goal:** a falsifiable guard that is red at HEAD-with-clone-bypassed and names the writer.
**Acceptance:**
- `MasterRegistryImmutabilityTests`: snapshot every `Symbol` reachable from `AnalysisCacheEntry` (reflection walk over `BuiltinRegistry`, `CachedModuleDiscovery`, `ClrTypeBridge`, `ModuleRegistry` instance fields whose element/value type derives from `Symbol`; every public+internal property of each symbol, recursively over `Methods/Fields/Constructors/Interfaces`) before and after N runs of {`Analyze`, `AnalyzeDocument`, `AnalyzeProject`, `Compile`} over the poisoner corpus through **one** `CompilerApi`, with `CloneForCompilation` bypassed via an `internal` test hook. Red @ HEAD: records the symbol, field, and the source that wrote it (commit body: "broken → red (N failed, `len.CodeGenInfo` written by `<file>`)").
- Sequential-reuse positive control: `AnalyzeDocument(shadow) ; AnalyzeDocument(any)` with clone bypassed → throws SPY0909 (or the traced shape does). Recorded.
- `SetCodeGenInfo` trace: a Debug-only `_logger.LogDebug` in `CodeGenInfoComputer.SetCodeGenInfo` when `_symbolTable.BuiltinRegistry.IsBuiltinSymbol(symbol)`; run `FrontEndParityTests`, `MetamorphicCorpusSweepTests`, `InteropConformanceTests`, `DiagnosticSweepTests` in a worktree with the clone bypassed; the distinct (call site, source) pairs are posted on #1633. The trace line stays (it is a legitimate debug log), the bypass hook is test-only.
#### Tasks
1. **Guard + trace + bypass hook** — `Sharpy.Compiler.Tests/Project/MasterRegistryImmutabilityTests.cs` (new), `Semantic/CodeGenInfoComputer.cs:37` (trace), `CompilerApi.cs:612` (`internal bool BypassRegistryCloneForTests`). Commit: `test(project): master-registry immutability guard reproduces the #1633 poisoner (red at HEAD with the clone bypassed)`

### Phase 5.2: option (b) — the binding is the only store
**Acceptance:**
- `Symbol.CodeGenInfo` does not exist; `grep -rn '\.CodeGenInfo\b' src --include='*.cs' | grep -v 'GetCodeGenInfo\|SetCodeGenInfo\|HasCodeGenInfo\|CachedCodeGenInfo'` = 0 outside `SemanticBinding.cs`.
- `MasterRegistryImmutabilityTests` green with the clone bypassed. `.expected.cs` snapshots byte-identical (`UPDATE_SNAPSHOTS` not used; a diff is a finding).
- Warm-restore round-trip: compile a two-file project twice (`--incremental`); on the second run every restored symbol with a serialized `CodeGenInfo` answers `GetCodeGenInfo` non-null; the emitted C# is identical to the cold build. Mutation: skip the deserializer's `SetCodeGenInfo` → red (CS-level or SPY0909-free silent name drift caught by the C# comparison).
- `AssertCodeGenInfoConsistency` and its `DualWriteAssertionsTests` arms deleted (not left vacuously green); `SemanticBinding.cs:44-46` comparer comment rewritten (the comparer stays: symbols are still reference-identity keys).
#### Tasks
1. **Fold into the binding** — `SemanticBinding.cs:449-500`: `_codeGenInfo[symbol] = effective`; `ImportResolver.Symbols.cs:214` → `SetCodeGenInfo` (thread the binding if not in reach); `CodeGenInfoComputer.cs:129` → `!HasCodeGenInfo`. Commit: `refactor(semantic): MaterializeCodeGenInfo folds bridge tables into the binding entry; no Symbol write (#1633)`
2. **Emitter reads the binding** — `RoslynEmitter.cs:719` (drop fallback), `Constructors.cs:388`, `Methods.cs:153/162/188`, `TypeDeclarations.cs:736/802`, `Patterns.cs:172`, `TypeSyntaxMapper.cs:521-522/:836-837` → `GetCodeGenInfo(symbol)`. Commit: `refactor(codegen): every CodeGenInfo read goes through SemanticBinding.GetCodeGenInfo (#1633)`
3. **Cache path** — `Project/SymbolSerializer.cs`: `Serialize(Symbol, string, SemanticBinding)` reads the binding; `Deserialize(…, SemanticBinding)` writes it; `IncrementalCompilationCache.SaveFileCache` and `ProjectCompiler.IncrementalCache.cs:80` thread `_projectModel.SemanticBinding`; delete `Initialization.cs:108-164`'s CodeGenInfo re-registration (keep `SetVariableType`/inheritance registration); warm-restore test in `Project/IncrementalCodeGenInfoRoundTripTests.cs`. Commit: `fix(project): incremental cache round-trips CodeGenInfo through the binding, not the Symbol (#1633)`
4. **Delete the field and the dual-write assertion** — `Symbol.cs:160-162`; `DualWriteAssertions.AssertCodeGenInfoConsistency` + `FileCompilationPipeline.cs:178`; `DualWriteAssertionsTests` CodeGenInfo arms; dead `RemoveCodeGenInfo`/`RemoveVariableType`; `ErrorRecovery`/explanation text for SPY0909 narrowed to the two remaining assertions. Commit: `refactor(semantic): delete Symbol.CodeGenInfo; SemanticBinding is the sole CodeGenInfo store (#1633)`

### Phase 5.3: retire the clone; audit the shared caches (H1.4)
**Acceptance:**
- `CloneForCompilation`, the copy ctor, the #1667 identity guard, and `SharedRegistryOwnershipTests` are deleted; `MasterRegistryImmutabilityTests` (no bypass hook needed any more) stays green across the four entry points **and** first-use. If red: the clone stays, the writer is filed, and the report says so.
- `StaticStateConformanceTests`: stale allowlist entries are a hard failure (`:206-218`); rule 3 — any instance field reachable from `AnalysisCacheEntry` whose value type derives from `Symbol` (incl. `ConcurrentDictionary`/`Lazy<>` payloads) must be listed with an issue or covered by the snapshot guard's enumeration (the test asserts the two rosters agree). Mutation: add a `ConcurrentDictionary<string, TypeSymbol>` field to `CachedModuleDiscovery` → red.
- H1.4: `ResolveGenericInstanceMethods` returns `ImmutableArray<MethodInfo>`; `GetMemberNameSurface`/`GetExtensionMethodNames` return `FrozenSet<string>`/`ImmutableHashSet<string>`; `_byName` values `IReadOnlyList<MethodInfo>`. Guard: a runtime assertion that each vended object's concrete type is not an array and not assignable to `ICollection<T>`/`ISet<T>`/`IList<T>` with `IsReadOnly == false` (reflection over the three cache accessors). Mutation: return the cached `HashSet`/array again → red. [CORRECTED: "compile error" is not a red test]
- Bundle census: `find src -path '*/.sharpy-crash/*' -name report.md -newer <Phase 5.2 commit file>` empty after the whole-solution gate.
#### Tasks
1. **Retire the clone** — `BuiltinRegistry.cs:88-177`, `CompilerApi.cs:592-612`, `SharedRegistryOwnershipTests.cs`. Commit: `refactor(semantic): retire the per-compilation registry clone — nothing writes a master symbol any more (#1633)`
2. **Instance-cache audit + hard stale rule** — `StaticStateConformanceTests.cs`. Commit: `test(diagnostics): static-state audit covers Symbol-vending instance caches; stale entries fail (#1633, #1672 H1.4)`
3. **Immutable views** — `Discovery/ClrTypeHelper.cs:79/:114/:160`, `ClrExtensionMethodResolver.cs:73`. Commit: `fix(discovery): shared reflection caches hand out immutable views (#1672 H1.4)`

### Phase 5.4: fixture `bin/` hygiene (H2b)
#### Tasks
1. **`build_tools/clean_fixture_bins.py`** — enumerates `src/**/TestFixtures/**/bin/**/.sharpy-crash` and stray `*.spy` under `bin/`, `--dry-run` default, prints a census; registered in `build_tools/cli.py`; run once and the census (before/after) posted on #1672. `IntegrationTestBase`: a fixture compile sets `ProjectConfig.CrashRoot` to the temp root (verify the prior round's H2 Task 3 actually did this — if so, cite the line instead). Commit: `chore(build_tools): fixture bin/ crash-bundle cleanup script + census (#1672 H2b)`
   > [ADDED: verifier note] The Q2 ruling names the cure as `build_tools/clean_fixture_bin.sh` + a **pre-compile `bin/` clean in `IntegrationTestBase`**. This task substitutes a `.py` script (fine — `build_tools/` is Python-first) and a `CrashRoot` redirect for the pre-compile clean. The redirect is the stronger cure (prevents the bundle instead of sweeping it) but it is a deviation from the ruling's wording: state it in the #1672 comment when the batch opens, (`ProjectConfig.CrashRoot` exists @ 6e2b68812 — `ProjectConfig.cs`, read by `ProjectCompiler.cs` and `CompilerOptionsFactory.cs`; whether `IntegrationTestBase` sets it is the thing to verify).

### Testing Strategy
Positive controls: `Compile` path never threw before or after; the snapshot guard's "unchanged" assertion is paired with a deliberately mutated run (the Phase 5.1 red). Blast radius: whole gate + LSP suites + warm/cold harness + three sweeps.

### Issues to Close
- #1633 — closed by Phases 5.1–5.3; **criterion:** guard red @ HEAD-bypassed with the poisoner named (commit body), green after 5.2, green with the clone deleted (or the writer filed and the clone retained — stated), zero new bundles after the gate @ sha.
- #1672 rows H1.4, H2b — deleted from the ledger by 5.3 Task 3 and 5.4; **criterion:** immutability mutation red/green recorded; census posted.

---

## Batch 1 — Rule-2 type materialization: #1671, #1666, #1670 (emitter half)

### Current State (@ 6e2b68812)
- **#1671.** `TypeChecker.cs:42` `_expectedType` single ambient slot; assigned at `Statements.cs:635-638/121-133/338-341/834-837`, `Definitions.cs:724-729/2079-2082/2252-2255`, `Calls.cs:3684-3730/3752-3765`, `Lambdas.cs:253-278/321/336`, cleared `Calls.cs:3819-3820`. `Literals.cs`: expected type wins only when the LCA is `object`/`Unmapped`/`Void` (list `:94-100`, dict `:153-164`, set `:211-217`; tuple never, `:265`); never pushed into nested elements (`:82/:139/:200/:265`). Literal-inferred types carry `GenericDefinition = null` (`:108-112/:171-175/:223-227`); `GenericType.Equals` compares `GenericDefinition`; `Utilities.cs:1144-1146` invariant arm = `Equals || (IsAssignableTo && IsAssignableTo)` (one-level tolerant); `list`/`set` are covariant by `BuiltinRegistry.cs:527-532`, `dict` invariant. Emitter: `RoslynEmitter.cs:100` `_targetTypeContext`; readers `Literals.cs:37-41/:93-97/:123-125/:134-138/:187-190`, propagator `GenerateWithNestedTargetType :1343-1362` (always index 0 — the dict-value hack exists because of it); 13 setter sites (`Assignments.cs:901/1097`, `Calls.cs:1239` clear, `Constructors.cs:171/280/321`, `Properties.cs:49/355/753/804`, `Dataclass.cs:61`, plus `Literals.cs:135` (dict-value hack) and `:1355` (inside the propagator) [CORRECTED: grep @ 6e2b68812 — 13 assignment lines, not 11; the two extra lie inside the reader/propagator ranges Phase 1.2 already deletes]); **no `return`-position setter** (`GenerateReturn` `:786-815` has none — and the cell is live: `return [Derived()]` is SPY0908/CS0029 @ 6e2b68812, as is the call-argument cell `g([Derived()])` SPY0908/CS1503, both measured with `run`). 74 `MapType(TypeAnnotation)` sites in CodeGen: 61 declaration positions (legitimate), **13 expression/pattern positions** (`Literals.cs:40/96/97/190`; `Operators.cs:785` fact-first fallback, `:936` `MapClassifiedTypeOperand` — the model; `Patterns.cs:435/497/805/848` fact-first fallbacks, `:462/468/473` raw) [CORRECTED: the list enumerates 13, not 11 — 12 ratcheted + the exempt model, matching Phase 1.1's `4 + 7 + 1`]. `EmitterCarrierOnlyConformanceTests` cannot see any of these (carrier is `Parser.Ast.TypeAnnotation`).
- **#1666.** `ClassifyFlooredArithmetic` (`TypeChecker.Expressions.Operators.cs:316-339`) tags every narrow width (`IsFlooredNumeric :348` includes them, and its doc at `:342-346` says so — "every integer width (int, long, int8, uint8, uint16, uint32)" [CORRECTED: the doc does not claim otherwise]); `InferBinaryOpType` → `TryInferBuiltinBinaryOp :328` → `GetPromotedType` (`PrimitiveCatalog.cs:218-255`, priority-ranked, no floor): `uint8 // uint8` typed `uint8`, C# `FloorDiv(int,int)` → `int`; `uint32 // uint32` → `long`. `RoslynEmitter.Operators.cs`: `GenerateFloorDivideValue :1170` → `GenerateFloorDivision` and `GenerateModuloValue :1019` → `GenerateFloorModulo` emit no cast [CORRECTED: file and lines]; the `IntegerPowInt/Long` arms (`:1114-1127`) cast the **operands** up to `int`/`long` from the recorded tag — a precedent for tag-driven casts, **not** for narrowing: `n: int8 = a ** b` is SPY0908/CS0266 @ 6e2b68812 [CORRECTED: measured with `run`]. Unary `-`/`~` are typed by `TypeInferenceService.InferUnaryOpTypeUncached :1044-1079` (`Plus or Minus => operand`, `BitwiseNot` at `:1071`) and `**` by `InferPowerResultType` (`:342`) — **separate arms the binary floor does not reach**; `n: int8 = -a`, `~a`, `a >> b` are SPY0908 @ 6e2b68812 [ADDED: adequacy]. Augmented `//=`/`%=` recorded at `Statements.cs:466-477` on the `Assignment` node; the augmented result check at `Statements.cs:388-395` (`resultType.IsAssignableTo(targetType)` → SPY0220 "Result type … of augmented assignment is not assignable") already refuses every augmented form whose RHS promotes wider than the target. **Measured augmented direction table @ 6e2b68812 (`run`):** `x8 //= y8`, `x8 += y8`, `x8 **= y8`, `x8 <<= y8` (narrow RHS) → SPY0908/CS0266; `x8 %= 2`, `x8 += 1`, `x8 += i` (`int` RHS) → SPY0220. The emitter desugars **every** augmented operator to `x = x op y` (`GenerateAugmentedBinaryExpression`, `Assignments.cs:797`; emitted `x8 = x8 + y8`), so no augmented form gets C#'s compound-assignment narrowing [CORRECTED: the issue body's `x8 %= 2` repro is SPY0220 at HEAD, not SPY0908]. `Divmod` overloads: int/long/ulong/double/float/decimal (`DivMod.cs:20-166`); no compiler handling and none needed — `divmod(a8, b8)` is typed `tuple[int32, int32]` (SPY0220 on a narrow destination). Spec: `arithmetic_operators.md:37-38` "Promoted type (typically `int32`)", `:318-323` Key Rule 1 "at least `int32`"; overflow policy `:279-284` unchecked/wrap; the spec is silent on unary `-`/`~` result widths and `assignment_operators.md` (12 lines) is silent on augmented narrowing.
- **#1670 emitter half.** `Patterns.cs:452-476` three `_typeMapper.MapType(positionalPattern.Type)` on the raw annotation, returning before the fact-first read at `:493-497`. Checker records `SetPatternType` at `Statements.Patterns.cs:1004` [CORRECTED: `:1002` is `selfMatchedType = scrutineeType;`] (an open `UserDefinedType` — wrong, Batch 2's cell).

### Defect Class
- **Violated contract:** the emitter reads the recorded semantic type of every expression and pattern node; it never consumes a `Parser.Ast.TypeAnnotation` in expression or pattern position (Rule 2); and the recorded type of an arithmetic expression **is** the CLR result type of the emitted expression.
- **Meta-class:** type decision in CodeGen → materialize + ratcheted scan; silent wrong behavior (#1666, #1671 depth-2) → fix then sweep.
- **Known member cells:** #1671 (covariant literal element type), #1671 second cell (depth-2 dict false SPY0220), #1666 (narrow `//`/`%` stores), #1670 emitter arm.
- **Sibling cells:** literals — kind {list, set, dict-key, dict-value, tuple-element} × depth {1, 2, 3} × direction {covariant `Derived→Base`, exact, mistyped → SPY0220} × context {annotated decl, assignment to existing, `return`, call argument, default parameter, class field init, dataclass default, auto-property default, ctor parameter default, `__init__` field assignment, observer-property default} — full 5×3×3 at the decl context (45) + all 11 contexts at list depth 2 covariant (11) + the `return` cell at every kind (5, **likely live**: no emitter setter, checker records the inferred type). Arithmetic — width {int8, int16, uint8, uint16, uint32} × operator {`+ - * & | ^ << >>`, unary `-`, unary `~`, `**`, `//`, `%`, `divmod`} × store {plain decl to the narrow type, augmented (narrow RHS **and** `int` RHS — two cells, they sit on different sides of the `Statements.cs:388` check today), argument to a narrow parameter, return from a narrow def, `print`} plus one mixed-width row (`int16 = int8 op int16`) (5×14×6 = 420, generated) [CORRECTED: adequacy — `>>`, unary `-`/`~`, plain `**` and mixed widths are live SPY0908 cells @ 6e2b68812 that the original 250-cell matrix and the binary-arm floor do not reach; the augmented store splits by RHS width because the two halves have different base-sha behaviour].
- **Standing harness that should have caught it:** `EmitterCarrierOnlyConformanceTests` (wrong carrier universe) → Phase 1.1 adds `EmitterTypeAnnotationConsumptionTests`; `DifferentialExecutionTests` (no narrow-width programs; python3 has no narrow ints — the oracle for #1666 is the spec, not CPython).
- **Owner rulings:** #1666 comment 2026-08-28 (class widened to every store); Q1 (#1670) for the pattern arm.
- **Sibling classes found by /verify-implementation 2026-08-30 (outside this batch's matrix — filed, not fixed here):** #1698 in-range integer constants convert only at declaration/argument positions (plain, attribute and index stores and `return` refuse them — same `IsImplicitConstantConversion` helper, three more call sites); #1699 mixed-signedness promotion (`uint32 ⊗ int16`, `uint64 ⊗ int32`) records a type the CLR does not produce (SPY0908/CS0266, CS0034) — a `GetPromotedType` row, not an augmented-store cell; #1700 `**` on uint64 operands evaluates in int64 (no `CheckedIntPow(ulong, ulong)` overload) — a Core cell the uint64 typing arm exposes. The #1666 matrix commit `9f3a49369` declares the #1698 and #1700 cells N/A with the issue cited (they drain when the issue closes); #1699 is filed only — no cell in the matrix names it yet, so its fix must add the mixed-signedness row.

### Adversarial Review
- **Alternative root cause (#1666):** an emitter-side cast at every store (the issue's own proposal). Refuted by the spec (Rule 7): `arithmetic_operators.md:322` says the promoted type is at least `int32`, so `q: uint8 = a // b` is a **type error**, not a cast site; the checker is wrong, not the emitter. The one place a cast is right is the augmented form, where native C# compound assignment narrows implicitly and our call lowering loses that — that is the ruled `NarrowTo`.
- **Direction (#1666):** newly refused programs are exactly those that were CS0266/CS1503 behind SPY0908 (never ran). The batch **measures** this: every program the matrix newly refuses is run at the prior commit and must show SPY0908 there; a program that ran and printed at the prior commit and is refused after is a regression and blocks the commit. `c: int = a + b`, `print(a + b)`, comparisons, list literals of narrow ints (`[a, b]` stays `list[int8]` — measured working @ 6e2b68812 and must stay so; the floor applies to the binary-op result, **not** to `GetPromotedType`, whose other consumer is `BuiltinReturnTypeInference.cs:142` (builtin return typing) — LCA is `FindLeastCommonAncestor` (`TypeChecker.cs:2086`), which never calls it [CORRECTED: "also serves LCA" refuted; conclusion unchanged]).
- **Direction — refused → accepted, RULED (owner, 2026-08-28 verify session; post on #1666 when the batch opens):** the augmented-narrowing rule is **C#'s** — `x op= y` on a narrow integer target narrows iff `y` is implicitly convertible to the target type (narrow-or-equal width, or an integer constant in the target's range); otherwise the `Statements.cs:388` SPY0220 stays. Consequences, each with its base-sha classification: `x8 //= y8`, `x8 += y8`, `x8 **= y8`, `x8 <<= y8` (narrow RHS) — ICE → runs; `x8 %= 2`, `x8 += 1` (in-range constant RHS) — SPY0220 → runs; `x8 += i`, `x8 %= i` (`int` variable RHS) — SPY0220 → **SPY0220 unchanged** (the rule's positive control); `x8 += 300` (out-of-range constant) — SPY0220 unchanged. Plain stores (`q: int8 = a // b`) stay refused by the floor (spec Key Rule 1). `assignment_operators.md` gains the rule with an executed example of each row. [ADDED: adequacy — direction by verification-contract §3; ruled]
- **How the fix could be inert (#1671):** deleting `_targetTypeContext` while the checker still records `list[Derived]` → `xs: list[Base] = [Derived()]` emits `List<Derived>` → CS0029 behind SPY0908. The fixture `nested_literal_types_1639` is order-blind (mutation-proven). Probe: the covariant cell must print at the closing sha and fail with the checker's contextual record reverted.
- **Blast radius:** every fixture with a collection literal (the whole FBIT), `.expected.cs` snapshots (element types may change spelling from `List<Derived>` to `List<Base>` — a ruled diff only for covariant cells; any other snapshot change is a finding), metamorphic corpus, spy-test C# regen (Stdlib tests use narrow ints), interop sweep.
- **Generated artifacts:** spy-test C#, stdlib docs if any Stdlib signature is typed narrow — regen after Task 1.2.

### Design Decisions
- **Bidirectional inference, kind-aware:** `_expectedType` becomes a push of the *element* expectation per kind — `ExpectedElementTypes(kind, expected)` yields (list/set: `[0]`; dict: key `[0]`, value `[1]`; tuple: per-index) and each element is checked under its own expectation. When the expected type is a same-kind collection of the same arity and every element is assignable to its expected element type, the **expected** type is recorded (the `object/Unmapped/Void` gate is deleted); otherwise the LCA is recorded and assignability decides (mistyped → SPY0220 unchanged).
- Literal-inferred collection types are stamped with their `GenericDefinition` (registry lookup) so `Equals` agrees with `GetDisplayName`; the invariant variance arm recurses through `IsAssignable` in both directions instead of `IsAssignableTo`/`Equals`.
- **#1666:** the integer promotion floor lives in `TryInferBuiltinBinaryOp`'s arithmetic/bitwise arms **and** in `InferUnaryOpTypeUncached` (`-`/`~` on a narrow operand → `int32`, C#'s own rule) **and** in `InferPowerResultType` (narrow `**` narrow → `int32`) [CORRECTED: adequacy — three arms, one contract; the spec gains the unary sentence] (spec Key Rule 1; Axiom 1 zero-cost — it is C#'s own rule). `OperatorLowering` gains `NarrowTo` (`SemanticType?`), recorded on the **augmented** `Assignment` node when the ruled C# rule admits the narrowing (RHS type narrow-or-equal to the target, or an in-range integer constant — reuse the constant-range check the spec's "constant `+ - * <<` does not widen" rule already needs); the emitter casts the desugared value (`(sbyte)Sharpy.Builtins.FloorMod(x8, 2)`, `(sbyte)(x8 + y8)`) from the tag for **every** augmented operator — native and lowered alike — because `GenerateAugmentedBinaryExpression` emits `x = x op y`, never C# compound assignment, and `x8 += y8` is SPY0908 at HEAD [CORRECTED: "native-operator augmented forms already narrow in C#" refuted by `emit csharp` (`x8 = x8 + y8`) and `run` (CS0266) @ 6e2b68812]. The `Statements.cs:388` check consults `NarrowTo` (recorded → assignable) so the augmented arm has one decision point. `divmod` on narrow operands is already typed `tuple[int32, int32]` at HEAD — honest, spec-consistent, no change.
- The pattern arm becomes fact-first now (reads `GetPatternType` via `MapSemanticType`); Batch 2 makes the fact correct. Its `MapType` fallback is counted by the ratchet and drained by Batch 2.

### Phase 1.1: the annotation-consumption ratchet
**Acceptance:**
- `EmitterTypeAnnotationConsumptionTests`: Roslyn-parses `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions*.cs` and `RoslynEmitter.Patterns.cs`; every invocation of `MapType(`/`MapTypeFromExpression(` whose argument expression's declared type is `TypeAnnotation` (resolved via the semantic model of a compilation over CodeGen) inside a method returning `ExpressionSyntax`/`PatternSyntax` is denied unless the enclosing method is `MapClassifiedTypeOperand`; ratchet file `CodeGen/emitter-annotation-consumption-allowlist.txt` (`file count issue`), initial `Literals.cs 4 #1671`, `Patterns.cs 7 #1670`, `Operators.cs 1 #1670`; stale-entry (drained) failure like `EmitterCarrierOnly`. Mutation: add `_typeMapper.MapType(list.ElementTypeHint)` to `GenerateListLiteral` → red; positive control `CarrierOnlySource_IsNotFlagged` twin.
#### Tasks
1. Commit: `test(codegen): ratchet — expression/pattern generators never consume a TypeAnnotation (#1671, #1670)`

### Phase 1.2: contextual literal typing (#1671) and `_targetTypeContext` deletion
**Acceptance:**
- `CollectionLiteralContextualTypingMatrixTests` (generated, 61 cells above): covariant cells print; mistyped cells SPY0220; exact cells print. The depth-2 dict cell compiles (issue's second cell). Mutation: revert the contextual record → covariant cells red (SPY0908/CS0029); revert the `GenericDefinition` stamp → depth-2 dict red.
- `_targetTypeContext`, `GenerateWithNestedTargetType`, all 11 setters and 5 readers deleted; ratchet `Literals.cs` entry drained to 0.
- `.expected.cs` diff reviewed: only covariant-cell element-type spellings change.
#### Tasks
1. **Checker** — `TypeChecker.Expressions.Literals.cs` (per-kind element expectation push; expected-type record; `GenericDefinition` stamp), `TypeChecker.Utilities.cs:1144-1146` (mutual `IsAssignable` recursion), `TypeChecker.Statements.cs:834` (`return` already pushes; verify the record lands). Commit: `fix(semantic): collection literals record the contextual type at every depth and kind; invariant type arguments compare structurally (#1671)`
2. **Emitter** — delete `RoslynEmitter.cs:100`, `Literals.cs` readers + `:1343-1362`, the 11 setters; `GenerateListLiteral/Dict/Set` read `GetExpressionSemanticType` only. Commit: `refactor(codegen): delete _targetTypeContext — literal element types are read from the recorded type (#1671)`
3. **Matrix + regen** — `Sharpy.Compiler.Tests/Semantic/CollectionLiteralContextualTypingMatrixTests.cs`; regen commit. Commit: `test(semantic): collection-literal contextual typing matrix — kind × depth × direction × context (#1671)`

### Phase 1.3: integer promotion floor + `NarrowTo` (#1666)
**Acceptance:**
- `NarrowWidthArithmeticMatrixTests` (generated, 420 cells [CORRECTED: see Sibling cells]): plain narrow stores of a binary, unary or `**` result → SPY0220 (message names the promoted width); augmented forms admitted by the C# rule print the Python-floored value (`x8 %= 2` on `-7` → `1` — **SPY0220 at HEAD**, a refused → accepted flip the commit body lists; `x8 //= y8` on `-7, 2` → `-4` — SPY0908 at HEAD; `x8 += y8` — SPY0908 at HEAD); augmented forms the rule refuses stay SPY0220 (`x8 += i`, `x8 += 300` — the rule's positive controls, SPY0220 at HEAD and after); `print(a + b)` prints; `c: int = a + b` prints; `xs: list[int8] = [a, b]` prints. Each cell carries its prior-commit classification (`ICE` / `SPY0220` / `worked`) from a script run at the base sha. Mutation: remove the binary floor → plain binary-store cells go from SPY0220 to SPY0908 (red); remove the unary floor → `-a`/`~a` cells red; remove the `**` floor → `**` cells red; remove the cast → every admitted augmented cell red (native operators included — `x8 += y8` is the tell).
- `ClassifyFlooredArithmetic` doc corrected; `#1662`'s uint32 section restored as the plan of that round intended (`long` destination) plus the narrow-destination cells here.
- `arithmetic_operators.md`: Key Rule 1 gets an executed example (narrow store refused; augmented narrows); `docs/deviations.yaml` unchanged.
#### Tasks
1. **Floor** — `Semantic/TypeInferenceService.cs:294-328` arithmetic/bitwise arms, `:1044-1079` unary arm, and `InferPowerResultType` (`:342`): integer result is `max(promoted, int32)` [CORRECTED: adequacy — three arms]. Commit: `fix(semantic): integer binary, unary and power operations promote to at least int32 per the spec (#1666)`
2. **`NarrowTo`** — `SemanticInfo.cs:2308` `OperatorLowering(Kind, SemanticType? NarrowTo = null)` (the record is `OperatorLowering(OperatorLoweringKind Kind)` today); `Statements.cs:388-395` + `:466-477` decide by the chosen rule and record it for **every** augmented operator on a narrow target, native or lowered; `RoslynEmitter.Statements.Assignments.cs:741-800` (`GenerateAugmentedValue` / `GenerateAugmentedBinaryExpression`) wraps the desugared value in the cast from the tag [CORRECTED: every operator, not only the lowered calls — `x8 += y8` is SPY0908 at HEAD]. Commit: `fix(codegen): augmented assignment on a narrow integer target narrows from the recorded lowering (#1666)`
3. **Matrix + spec** — `Sharpy.Compiler.Tests/Semantic/NarrowWidthArithmeticMatrixTests.cs`, base-sha evidence script under `build_tools/` or `.claude/tmp/` (not committed if one-off; the evidence goes in the commit body), `docs/language_specification/arithmetic_operators.md`. Commit: `test(semantic): narrow-width arithmetic matrix — width × operator × store; spec example executed (#1666)`

### Phase 1.4: pattern arm reads the recorded type (#1670 emitter half)
#### Tasks
1. `RoslynEmitter.Patterns.cs:452-476`: all three branches map `GetPatternType(positionalPattern)` via `MapSemanticType`; `MapType(annotation)` only as the ratcheted fallback (`Patterns.cs` ratchet count stays **7** — 3 raw become fallbacks, 4 existing fallbacks — until Batch 2 drains it). **No guard is claimed for this commit**: the recorded type is still the open `UserDefinedType` at this sha, so the fallback fires and behaviour is unchanged by design; the commit body says so instead of implying a test. [ADDED: missing step — verifier] Commit: `refactor(codegen): self-matching positional patterns read the recorded pattern type (#1670)`

### Testing Strategy
Positive controls: `b: list[str] = [1]` SPY0220 at every depth; `xs: list[Base] = [Derived()]` prints (mutation-proven); `c: int8 = a + b` at the prior commit = SPY0908 (recorded). Output discrimination: covariant cells print a `Base`-typed method result that differs between `Base` and `Derived` bindings only if the literal type is `Base` (`isinstance`-free — use a `list[Base].append(Base())` after the literal, which fails CS1503 on `List<Derived>`). Blast radius: whole gate + snapshots + spy-test regen + three sweeps.

### Issues to Close
- #1671 — Phase 1.2; **criterion:** matrix green @ sha with both mutations recorded red; ratchet `Literals.cs` at 0; snapshot diff limited to covariant spellings.
- #1666 — Phase 1.3; **criterion:** matrix green; every newly refused cell has prior-commit SPY0908 evidence and every newly accepted cell (`x8 %= 2` is SPY0220 at HEAD) its prior-commit SPY0220 evidence; the augmented-narrowing rule is spec'd and executed; `x8 //= y8` and `x8 += y8` run; spec example executed @ sha. [CORRECTED: direction both ways]
- #1670 — **stays open** until Batch 2 (the emitter half alone does not clear the ICE).

---

## Batch 2 — Pattern semantics decided once: #1670 (checker half), #1651 subsumption sibling, pattern matrix, `BindingName`

### Current State (@ 6e2b68812)
- Five pattern-site `ResolveTypeAnnotation(…, bareGenericFillsFromContext: true)` calls: `Statements.Patterns.cs:512, 623, 923, 1000, 1028`; the flag's doc (`TypeResolver.cs:66-73`) requires the caller to apply or refuse (SPY0345) — none does. `:893-898` defers erasure to the emitter instead of recording a `TypeTestLowering`.
- `ClassifyTypeTestAnnotation(annotation, lodgeOn, subjectType, siteNoun, CollectionErasure)` (`Utilities.cs:217-222`) — callers: `Statements.cs:1573/:1598` (except), `Operators.cs:1229` (`is`/`as?`/`as!`) [CORRECTED: lines @ 6e2b68812]; zero callers in `Statements.Patterns.cs` (grep); `ClassifyBareTypeNameOperand` (`Calls.cs:782-808`) → `ClassifyResolvedTypeOperand :825-861` (three outcomes; `IsErasableCollection` = list/set/dict, `BuiltinNames.cs:121`). Carrier `TypeTestLowering(Kind, TestType, Alternatives)` (`SemanticInfo.cs:1974`), `MergeFrom :1573`. Pattern carriers `_patternTypes/_patternTotality/_patternUnionCases` all in `MergeFrom`.
- Emitter: `GenerateTypePattern :795-849` hand-rolled erasure `:833-841` [CORRECTED: was `:831-838`] + array decision `:823-830`; `MapTypeTestTarget` (`Access.cs:1216-1226`) is the carrier-driven version; `TryMapBuiltinCollectionToNonGenericInterface :1202` doc claims to mirror the pattern path (false). Parser: `Type()` → `TypePattern`, `Type(sub)` → `PositionalPattern` (`Parser.Statements.cs:1789-1815`); `SPY0125 GenericTypeInPattern` exists.
- `TypePatternIncompatible` SPY0361 (`:773-782`, neither direction assignable); totality `:785-790`; positional self-matching totality recorded `:1004-1007` but `MatchArmOrderValidator.IsTypeTotalPattern` (`:70-78`) reads only `TypePattern`/`AsPattern{TypePattern}`. SPY0700 `IrrefutablePatternNotLast`; next free `SPY0702`.
- `SelfMatchingBuiltins.cs`: 11 names, no `range`; `range` → SPY0202. Spec `match_statement.md:289-322` documents only the zero-arg `case list()` spelling; no `case list(xs)`, no `range`.
- `PatternMatchPropertyTests.cs:46/75/104/133/162/191` six `passed > total / 3` (50 samples, exceptions swallowed). 29 such asserts in 9 files (the other 23 → Batch 6).
- `TypePattern.BindingName` (`Pattern.cs:99`): parser never sets it; 8 readers (`Statements.Patterns.cs:636-652/:799-816`, `UnusedVariableValidator.cs:345`, `LocalNameCollisionValidator.cs:155`, `AstNormalizer.cs:513`, `UnparseVisitor.Patterns.cs:31`, `StructuralEqualityComparer.cs:110`, `InlayHintHandler.cs:306`); `GetChildNodes` yields `BindingName` and not `Type` (`:116-117`).

### Defect Class
- **Violated contract:** pattern semantics — test type, erasure, capture type, totality, subsumption — are decided once in the checker from pattern kind × scrutinee static type, recorded node-keyed on the **pattern node**, and read by the validator and the emitter. `isinstance`, `is`/`as?`, `except`, and class patterns share one classifier.
- **Meta-class:** position/route missing the shared check (five pattern sites bypass `ClassifyTypeTestAnnotation`); Rule 2 (emitter erasure switch); instruments passing vacuously (6 proportion asserts).
- **Known member cells:** #1670 (5 generic builtins × object subject → CS0305/CS0723/CS0246; nested `[int(a), str(b)]` → CS8985), #1672 subsumption sibling of #1651.
- **Sibling cells:** builtin {list, dict, set, frozenset, bytearray} × subject {object, matching generic, non-matching generic, union} × position {top-level, nested in sequence, nested in class positional} × form {statement, expression} = 120; plus `case range(x)` (SPY0202 + spec row), `case list[int](xs)` (SPY0125 until #1619), impossible subject (`x: str` × `case list(xs)` → SPY0361), capture typing (`object` subject → `xs: list[object]`; `list[int]` subject → `list[int]`; union member → that member); subsumption `object` × earlier {`int()`, `int(n)`, `int() as n`, `99`} × later {`99`, `"s"` ok, `int()`, `str()` ok} × form (32).
- **Standing harness:** `PatternMatchPropertyTests` (vacuous) → replaced by `PatternConformanceMatrixTests` (every cell asserts stdout or a code). `QualifiedBareSpellingConformanceTests` `pattern` rows cover spelling only.
- **Owner rulings:** Q1; R1/R1′ (#1651) for the SPY0700 narrowing rule; R2 (#1653) self-matching list.

### Adversarial Review
- **Alternative root cause:** `TypeSyntaxMapper.MapType` cached-annotation path (`:307-314`) re-resolving the open type even after the checker records a closed one. Discriminator: after Batch 1.4 the arm reads `GetPatternType`; if CS0305 persists with a correct recorded type, the mapper is the second arm.
- **CS8985 (nested):** root cause not established; probe before the fix: emit `case [int(a), str(b)]` on a `list[object]` subject and read the C#. Two candidates: (i) the sequence pattern lowers against `Sharpy.IList` (no indexer/Count) after erasure; (ii) the nested self-matching arm emits an open type. The contract covers both; the cell decides.
- **Inert fix:** recording the `TypeTestLowering` but leaving `GenerateTypePattern`'s hand-rolled switch → passes for list/dict/set and still fails for frozenset/bytearray (not in the switch). Probe: the frozenset/bytearray cells.
- **Direction:** newly refused programs: `case list(xs)` on `x: str` (SPY0361 — measured @ 6e2b68812 with `run`: SPY0908/**CS0305**, the same open-generic ICE as the `object` subject, because the emitter never gets as far as CS8121 [CORRECTED: measured]), `case list[int](xs)` (SPY0125 already). No program that ran is refused. Positive controls measured @ 6e2b68812: `case list(xs)` on `x: object` → SPY0908/CS0305; `case int(): … case 99:` on `object` → SPY0908/CS8120 (#1651 sibling).
- **Blast radius:** every pattern fixture, `MatchArmOrderValidator` (SPY0700 on subsumed literals — measure the corpus for arms that become SPY0700: those were CS8120 ICEs), LSP inlay hints (`BindingName` reader), pretty-printer round-trip (`UnparseVisitor`, `StructuralEqualityComparer`, reparse-equivalence sweep).

### Design Decisions
- One classifier: the five pattern sites call `ClassifyTypeTestAnnotation(annotation, lodgeOn: pattern, subjectType: scrutinee, siteNoun: "class pattern", erasure: Allowed)`; the resulting `TypeTestLowering` is keyed on the **pattern node** (walkers never visit `TypePattern.Type` — a fact lodged on the annotation strands). `SetPatternType` records the capture type (erased → `list[object]`/scrutinee element type when known).
- Emitter: `GenerateTypePattern`, positional, and property arms map `MapTypeTestTarget(GetTypeTestLowering(pattern))`; the hand-rolled switch and the array decision (`:823-838`) are deleted — the array case becomes a `TypeTestLowering` kind or an `ErasedBuiltinCollection` with `TestType = array`. The `Patterns.cs`/`Operators.cs` ratchet entries drain to 0; remaining fact-first fallbacks become `throw InvalidOperationException` (a missing fact is an ICE, not a fallback).
- Subsumption: `SetPatternType` also recorded for literal patterns (the literal's builtin type) and for `AsPattern` (inner's); `MatchArmOrderValidator` reports SPY0700 when an earlier unguarded arm's recorded type ⊇ a later arm's, with R1′'s trailing-irrefutable exemption; `IsTypeTotalPattern` reads positional totality too.
- `range` stays SPY0202; spec row states class patterns range over registered types.
- `BindingName` deleted with its 8 readers; `GetChildNodes` yields `Type` — a `.expected.cs`/reparse-equivalence run proves nothing observable changes (positive control: a hand-built `TypePattern{BindingName}` no longer compiles).

### Phase 2.1: the classifier owns pattern types
#### Tasks
1. **Checker** — `Statements.Patterns.cs:512/623/923/1000/1028` via `ClassifyTypeTestAnnotation`; capture typing; SPY0361 for impossible subjects on the positional arm; `:893-898` records instead of deferring. Commit: `fix(semantic): class patterns are classified by the isinstance classifier — erased, closed, or refused (#1670)`
2. **Emitter** — `Patterns.cs` three arms read `MapTypeTestTarget`; delete `:823-838`; fix `Access.cs:1199` doc; ratchet → 0, fallbacks → throw. Commit: `refactor(codegen): pattern arms read the recorded type-test lowering; hand-rolled erasure deleted (#1670)`

### Phase 2.2: subsumption is a recorded fact (#1651 sibling)
#### Tasks
1. `Statements.Patterns.cs` literal/as pattern `SetPatternType`; `MatchArmOrderValidator.cs` subsumption rule + positional totality. Commit: `fix(validation): an earlier arm whose recorded type subsumes a later arm's is SPY0700, not CS8120 (#1672, #1651 sibling)`

### Phase 2.3: the pattern conformance matrix; retire the proportion asserts; delete `BindingName`
**Acceptance:**
- `PatternConformanceMatrixTests` (generated; 120 + 32 + 4 cells): each cell asserts stdout or a code. Mutation: revert 2.1 Task 2's arm change → the object-subject cells red; revert 2.2 → subsumption cells red (SPY0908).
- `PatternMatchPropertyTests` six asserts deleted (the generators are kept as a seed source for the matrix if useful; otherwise the file goes).
- `BindingName` absent; `grep -rn BindingName src` = 0; reparse-equivalence and front-end-parity sweeps unchanged.
- `match_statement.md`: `case list(xs)` section (erased on `object`, closed on `list[int]`, refused on impossible), `range` row, subsumption example under Arm Ordering — all executed.
#### Tasks
1. Commit: `test(semantic): pattern conformance matrix — builtin × subject × position × form; proportion asserts retired (#1670)`
2. Commit: `refactor(parser): delete TypePattern.BindingName and its eight readers; AsPattern is the one representation (#1672)`
3. Commit: `docs(spec): positional self-matching on generic builtins, range, subsumption (#1670)`

### Issues to Close
- #1670 — Phases 1.4 + 2.1 + 2.3; **criterion:** 120-cell matrix green @ sha with mutation records; ratchet at 0; spec examples executed.
- #1672 rows "subsumption sibling", "PatternMatchPropertyTests", "TypePattern.BindingName" — deleted by 2.2/2.3.

---

## Batch 3 — Control flow ≡ CFG: #1669, #1668, #1672 E2/DA/`__call__`/`del`/totality rows

### Current State (@ 6e2b68812)
- **#1669.** `GenerateTryWithElse` (`ControlFlow.cs:1370`) emits `try{body; ok=true}catch…finally{}` then `if(ok){else}`; the docblock states the wrong order as intent. `BuildTry` (`ControlFlowGraphBuilder.cs:670`) models else → finally correctly (`normalExit = elseExitBlock ?? tryExitBlock`). Loop-else uses the opposite polarity flag + AST rewrite (`Operators.cs:935-949`, `BreakWithFlagStatement`). Only two fixtures have both `else` and `finally` (`try_except_finally_0057/0006`), both order-blind and `ClassDef`-bearing (excluded from the differential pool). `RoslynEmitterStatementTests.cs:965/:1021` substring asserts, comment documents the wrong order. `DifferentialExecutionTests.HandPicked()` (`:334`, 45 probes [CORRECTED: agent count; plan said 26], none with try/with); `with` is rejected from the fixture arm (`:1091` `VisitWithStatement`) but the hand-picked arm bypasses the filter (`:318`). Spec `exception_handling.md:77-96` silent on ordering. python3 3.12: `try, else, finally`; exception → `except, finally`; `return` in else → `else, finally`.
- **#1668.** Modeled; `DefiniteAssignmentMatrixTests` one axis (12 read-site kinds), `"SPY0600"` hard-coded, positive twins present; no block-kind/write-kind axis. DA write kinds: `EntryRebinds` (for-target `:608`, with-as `:841`), plain `=` (`CollectAssignedNames`, no-ops on index/member), walrus; **not**: `except … as`, match captures, augmented, parameters. `BuildTry` sets neither `EntryRebinds` nor `Expressions`; `BlockExpressionTotalityTests` has no `TryStatement` row. `CfgStatementTotalityTests` compares three name sets to reflection, never to the switch; `DeferStatement` classified SIMPLE while `BuildDefer :320` does not `AddStatement`. `Expressions` (`BasicBlock.cs:92`) read by DA only; `MatchSubject :75`/`MatchExpressionSubjects :85` read by narrowing only; `ConditionalBranchTerminator.Condition` read by both; `EntryRebinds` doc `:107` stale ("consumed only by NarrowingFlowAnalysis").
- **E2.** `GenerateStore` (`Assignments.cs:1374`) arms Identifier/IndexAccess/MemberAccess/TupleLiteral/default SPY0518; callers `:527/:1238/:1343/:1369`, `ControlFlow.cs:640` (for target). `WithItem.Name` `string?` (`Statement.cs:505`, parser `:358-367` `ExpectIdentifier` — `with CM() as p.x:` dies as **SPY0104 "Expected Colon, got Dot"** @ 6e2b68812: the identifier `p` is consumed and the `.` trips the colon expectation [CORRECTED: measured; not SPY0101]); `ExceptHandler.Name` `string?` (`:409`, parser `:552-557` — `except E as p.x:` → **SPY0104 "Expected Colon, got Dot"** [CORRECTED: measured; the Q2 ruling says the same]); `WalrusExpression.Target` `string` (`Expression.cs:1016`, parser `Expressions.cs:130` guard — `(p.x := 5)` → **SPY0104 "Expected RightParen, got ColonAssign"** [CORRECTED: measured]); `ForClause.Target` `Expression` (`:348`) — but at HEAD the parser does **not** accept a non-name target anywhere: `[p.x for p.x in xs]` → SPY0104 "Expected In, got Dot", `[.. for ys[0] in xs]` → SPY0104 "Expected In, got LeftBracket", and the `for p.x in xs:` **statement** → SPY0104 "Expected In, got Dot" [CORRECTED: "parser accepts `obj.attr`/`xs[0]`" and "the statement is supported" are both refuted by `run` @ 6e2b68812; `Literals.cs:496-501` SPY0239 is reachable only for targets the parser does hand through (tuple/literal shapes) — re-read `ParseForTargetElement :218` at the batch's opening sha and record which target shapes reach it]; emitter `BindComprehensionLoopTarget :619` has no default arm. Next free parser code **SPY0142** (SPY0141 `LambdaNotAllowedAsOperand` is the last; SPY0142–SPY0199 reserved).
- **`__call__`.** No `DunderNames.Call`, no `DunderNameMapping` entry; refused at `Calls.cs:375` (identifier callee) and `:585` (expression callee) as SPY0201; `SPY0230 NotCallable` exists with an explanation that already mentions `__call__`; call targets must go through `RecordResolvedCallTarget` (`Calls.cs:4472`, #1438); `_callTargets` in `MergeFrom :1526`. Dunder synthesis registry `ClassMembers.cs:492-538`. Spec `dunder_methods.md:210` "Not supported"; `dunder_methods_recommendations.md:644-713` recommends B+D (`Invoke`); `dunder_invocation_rules.md` forbids user-facing `obj.__dunder__()`; LSP `DunderDocumentation.cs:99` already says callable. python3: `C()(3)` → 6.
- **`del`.** Lexer keyword (`Lexer.cs:128`), no parser arm (`Parser.cs:817-846`), falls to `ParsePrimary` default → SPY0100 "Unexpected token: Del"; `global`/`nonlocal` get SPY0134. Spec `del_statement.md` (3 lines), `keywords.md:53`, `operator_overloading.md:181`. python3: `del d['a']` removes in place; `del name` unbinds.

### Defect Class
- **Violated contract:** the emitted control flow of every compound statement is the flow the CFG models (else inside the finally-protected region; loop-else on normal exit); every binding position is a DA write kind; every store position routes through `GenerateStore` or is refused by a named parser diagnostic; every callable-object call and every `del` is decided once.
- **Meta-class:** silent wrong behavior (#1669) → fix + execution-order corpus; position/route missing a shared check (E2, `__call__` two arms, DA write kinds); instruments passing vacuously (`CfgStatementTotality`, `BlockExpressionTotality`, `RoslynEmitterStatementTests:965`).
- **Known member cells:** #1669, #1668 (residue), #1672 E2/DA/`__call__`/`del`/A1/totality rows.
- **Sibling cells:** `try` — {else only, finally only, else+finally} × {no exception, caught, uncaught, `return`/`break`/`continue` in try, exception in else, `return` in else} (21) — measured @ 6e2b68812: no-exception cell prints `try, finally, else` (python3: `try, else, finally`); caught cell prints `except, finally` (agrees); the `return`-in-else and exception-in-else cells are the ones that distinguish the nesting from a mere reorder + `with` — {1-arg exit, 4-arg exit, disposable} × {normal, exception suppressed, exception propagated, `return` in body} (12) + loop-else × {`break`, normal, exception} (6). DA — block kind (15 `BodyKinds`) × write kind {plain, tuple, starred, walrus, for-target, with-as, except-as, match capture, comprehension target (own scope)} × read site {after block, in sibling}. Stores — position {assignment, tuple element, starred, for statement, comprehension, with-as, except-as, walrus} × target {identifier, attribute, index, tuple, starred}. `__call__` — callee {identifier, attribute, call result, indexed} × arity {0, 1, overload pair} × form {statement, expression, argument}. `del` — target {name, index, attribute, slice, tuple of them}.
- **Standing harness:** `DifferentialExecutionTests` (had no try/with probes) — Phase 3.1 adds the execution-order corpus in the hand-picked arm; `DefiniteAssignmentMatrixTests` gains two axes; `CfgStatementTotalityTests`/`BlockExpressionTotalityTests` repaired.
- **Owner rulings:** Q2, Q3; R8.

### Adversarial Review
- **Alternative root cause (#1669):** none plausible — the emitter's own comment states the order. The nesting `try { try { body; ok = true } catch … ; if (ok) { else } } finally { fin }` keeps handlers off the else body (Python: an exception in else propagates uncaught by this statement's handlers) and keeps `return` in else inside the finally-protected region. The dead-code throw stays inside the outer try.
- **Inert:** a probe that prints only from `finally` and `else` in a fixture with a `ClassDef` never reaches the differential pool; the corpus goes in the hand-picked arm (key `handpicked::try_else_finally_order_*`).
- **Direction (E2):** `[… for p.x in xs]`, `[… for xs[0] in ys]`, `for p.x in xs:` (statement) and `with … as p.x` move from parser SPY0104 to running (support) [CORRECTED: all four measured SPY0104 @ 6e2b68812]; `except E as p.x` and `(p.x := 5)` from SPY0104 to named refusals — no working program is refused. `del …` from SPY0100 to a named refusal. `obj(args)` on a `__call__` class from SPY0201 (`'c' is not callable (type: C)` on an identifier callee; `Expression of type 'C' is not callable` on an expression callee — both measured) to running.
- **Blast radius:** every `try`/`with` fixture snapshot (`try_else_return.expected.cs` has no finally — unchanged; verify), `Sharpy.Stdlib.Tests` fixtures using try/else, LSP (`WithItem.Name` → `Target` touches the seven readers `HoverService.cs:75`, `DeclarationCursorResolver.cs:155` (`GetWithItemSymbol`), `InlineProvider.cs`, `SelectionAnalyzer.cs`, `InlayHintHandler.cs`, `FoldingRangeHandler.cs`, `SemanticTokensHandler.cs` [ADDED: agent grep @ 6e2b68812] — `FrontEndParityTests` is the sweep), pretty-printer/reparse sweep (new AST shape), `DefiniteAssignmentValidator`.
- **Generated artifacts:** spy-test C# (stdlib tests with try/else/finally) — regen after 3.1.

### Design Decisions
- `WithItem.Name: string?` becomes `WithItem.Target: Expression?` (parser accepts any store target, `ParseForTarget`-shaped); `GetWithItemSymbol` applies to identifier targets; non-identifier targets lower as `using (var __w = expr) { <GenerateStore(target, __w)>; body }` / protocol equivalent. `ForClause.Target` non-identifier: `BindComprehensionLoopTarget` gains a `GenerateStore` arm (and a default that throws). `except … as` and walrus keep `string` targets and gain named refusals `SPY0142 ExceptAsRequiresName` / `SPY0143 WalrusTargetRequiresName` (python3 SyntaxError for both). SPY0239's comprehension use moves to SPY0225 `InvalidAssignmentTarget` for the residue (literal targets).
- `__call__`: `DunderNames.Call`, mapping `__call__` → `Invoke`; the checker resolves `obj(args)` when the callee type's symbol declares `__call__` (overloads via the normal method overload path), records the target through `RecordResolvedCallTarget` and a node-keyed `CallableObjectDispatch` on the `FunctionCall` (`MergeFrom`); the emitter emits `obj.Invoke(args)` from the fact. Both "not callable" arms (`:375`, `:585`) route through one `TryResolveCallableObject` first and emit SPY0230 (whose explanation already fits) on miss. No `ICallable<>` this round (not ruled). Spec row flips; `dunder_invocation_rules.md` unchanged (explicit `obj.__call__()` still refused — positive control).
- `del`: parser arm parses `del` + target list and reports one `SPY0144 DelStatementNotSupported` per target with a shape-specific steer (index → `x.pop(k)`; name/attribute → not supported, Axiom 1); `del_statement.md` rewritten with the refusal and steers; `keywords.md` row; `__delitem__` row unchanged.
- DA: `BuildTry` sets `EntryRebinds` on handler blocks for `handler.Name`; `BuildMatch` sets capture names on case blocks; `Expressions` is the **one** "evaluated here" list DA reads (the builder also appends each `ConditionalBranchTerminator.Condition`; DA stops reading `Condition`); `MatchSubject`/`MatchExpressionSubjects` are documented as narrowing's role-typed views with a guard that every one of them also appears in `Expressions`. `EntryRebinds` doc corrected.
- Totality guards: `CfgStatementTotalityTests` derives the arm list from the `BuildStatement` switch by Roslyn scan and cross-checks reflection (a subtype with a dedicated arm must have a `Build*` method that either adds the statement or is listed as scope-exit); `BlockExpressionTotalityTests` enumerates every `Expression`-typed property of every `Statement` subtype by reflection and requires each to be classified {in a block's `Statements`, in `Expressions`, `EntryRebinds`-only, N/A with reason}.

### Phase 3.1: else runs before finally (#1669)
**Acceptance:** execution-order corpus (39 hand-picked probes) green in `DifferentialExecutionTests`; mutation: revert the nesting → 7 try cells red (CPython disagrees). `RoslynEmitterStatementTests:965` asserts structural nesting (else `if` inside the outer try block, before `finally`). `exception_handling.md` ordering sentence + executed example.
#### Tasks
1. Commit: `fix(codegen): try/else runs before finally — else nests inside the finally-protected region (#1669)`
2. Commit: `test(differential): execution-order corpus for try/with/loop-else in the hand-picked arm (#1669)`
3. Commit: `docs(spec): try/else/finally ordering sentence with executed example (#1669)`

### Phase 3.2: DA axes and totality guards (#1668, A1, CfgStatementTotality, BlockExpressionTotality)
**Acceptance:** `DefiniteAssignmentMatrixTests` gains block-kind × write-kind axes (135 cells + existing 23); `for-else`/`while-else` cells green with mutation record (cut the else edge in `BuildFor` → red). `CfgStatementTotalityTests` red when `case DeferStatement` is deleted (recorded). `BlockExpressionTotalityTests` red when the with-context `Expressions.Add` is removed (recorded) and when a new `Expression`-typed property is added to a statement without classification.
#### Tasks
1. Commit: `fix(analysis): except-as and match captures are DA write kinds; one block expression list (#1672 DA, #1668)`
2. Commit: `test(analysis): DA matrix gains block-kind × write-kind axes; CFG and block-expression totality derived from the code (#1668, #1672)`
3. [ADDED: missing step] **Lambda-captured never-assigned local** — the Q2 ruling (#1672 comment 2026-08-28) carries a ledger row the plan omitted: "`DefiniteAssignmentAnalysis.cs:45-48` now carries a lambda-reads rule — run the probe at HEAD and delete the row if green". Run the probe at the batch's opening sha; if green, delete the row with the probe in the comment; if red, it is a write-kind × read-site cell of this matrix (lambda body as read site) and joins Task 2. No separate commit.

### Phase 3.3: store positions match Python (E2)
**Acceptance:** store matrix (8 positions × 5 targets, N/A cells declared): `for`-statement, comprehension attribute/index targets and `with … as p.x` run (all four are parser SPY0104 at HEAD); `except E as p.x` → SPY0142; `(p.x := 5)` → SPY0143; identifier forms unchanged (positive controls). `#1226` evaluation-count harness gains the with-as/comprehension index-target rows. Mutations: delete the SPY0142 arm → the except-as cell reverts to SPY0104 (red, the cell asserts its code); revert the `GenerateStore` routing for with-as → the `with … as p.x` cell red. [ADDED: missing step — mutations; `for` statement is a new parser cell, not a supported one]
#### Tasks
1. Commit: `feat(parser,codegen): for-statement, comprehension and with-as targets accept any store target via GenerateStore (#1672 E2)` — parser: `ParseForTargetElement :218` and the with-as arm parse a store target (`ParsePrimary`-shaped with attribute/index suffixes), the `for` **statement** included [CORRECTED: the statement form is SPY0104 at HEAD, not supported]
2. Commit: `fix(parser): except-as and walrus targets that are not names are refused by name (#1672 E2)`
3. Commit: `test(codegen): store-position × target matrix with evaluation-count rows (#1672 E2)`

### Phase 3.4: `__call__` and `del` (Q3)
**Acceptance:** `__call__` matrix (4×3×3) green; overload pair resolves; `obj.__call__(1)` still refused (positive control); `del` matrix → SPY0144 with the steer text asserted per shape; `.error` fixtures for both; spec rows executed; `DiagnosticExplanations` entries for SPY0142–SPY0144. Mutations: bypass `TryResolveCallableObject` → every `__call__` cell red with SPY0201; delete the `del` parser arm → every `del` cell red (SPY0100 ≠ SPY0144). [ADDED: missing step — mutations]
#### Tasks
1. Commit: `feat(semantic,codegen): callable objects — obj(args) dispatches to __call__ via a recorded lowering (#1672)`
2. Commit: `fix(parser): del statement is refused by name with a pop(k) steer for index targets (#1672)`
3. Commit: `docs(spec): __call__ supported; del refused with steers (#1672)`

### Issues to Close
- #1669 — 3.1; **criterion:** corpus green @ sha, reversion red recorded, spec example executed.
- #1668 — 3.2; **criterion:** for-else/while-else DA cells green with the edge-cut mutation red; closed with a note that the CFG already modeled them at 6e2b68812.
- #1672 rows E2, DA write kinds, A1 four representations, CfgStatementTotality, BlockExpressionTotality, `__call__`/`del` — deleted by 3.2–3.4.

---

## Batch 4 — CLR seam fidelity: #1640

### Current State (@ 6e2b68812, measured by the prober 2026-08-28 unless noted)
- `BclMemberTypeOnBuiltinReceiver` (`Access.cs:1092-1200`): types **single-overload methods** on `BuiltinType` and on `GenericType` receivers that are not explicitly-mapped collections and whose def has no discovered `Methods`; properties on `BuiltinType` **only** (`:1185`), the stated blocker being codegen's zero-arg-call collapse. Bail-outs: overload groups (`methods.Count != 1`), `Unknown`-mapped parameter/return, char-returning, explicitly-mapped collections (`IsExplicitlyMappedCollectionReceiver :1031`, a hand list "keep in sync with `MapGenericTypeCore`").
- `CheckMemberAccessCore` arm order (explorer A1 table): three arms end in `DeliberatelyPermissive` → Unknown: `:401-405` (`UserDefinedType` with `ClrType` — every member of `StringBuilder`, `DateTime`; discovered symbols carry zero members, `ModuleRegistry.cs:398-501`), `:779-784` (general tail — every CLR **property** on a `GenericType`), `:884` (inherited CLR member). Allowlist `deliberately-permissive-allowlist.txt`: `Access.cs 4`, `Expressions.cs 2`, `Operators.cs 1`, all citing #1640; only three of the seven sites (`Access.cs:403/:782/:884`) are CLR-member cells — `:280` (module export kind), `Expressions.cs:299` (`auto`), `:590` (type ref in value position), `Operators.cs:1194` (conditional without LCA) are other classes mis-cited.
- Spellings: `ResolveClrMethodName`/`ResolveClrPropertyName` (`ClrTypeHelper.cs:39/:208`) accept only the reverse-mangled Pythonic name; verbatim `s.Peek()` resolves through neither and reaches codegen by `ToPascalCase`. The both-spellings rule exists only in the absence proof (`GetMemberNameSurface :114`) [CORRECTED: there is no `TaskType` switch at `ClrTypeHelper.cs:621-634` — the file is 589 lines; `TaskType` is constructed in `ClrTypeBridge.cs:284/:578` and carries no spelling rule].
- Measured cells: `Stack[int].peek()` Pythonic → SPY0220 ✔; `s.Peek()` → CS0029 ✘; every property (`Stack[int].count`, `SortedSet[int].min`, `StringBuilder.length`, `DateTime.year`) and every member on a non-generic class or struct (`StringBuilder.to_string()`, `DateTime.add_days()`) → CS0029 behind SPY0908 ✘; `NameValueCollection` multi-indexer ✔ (#1645).
- `CheckClrInstanceMethodCall` (`Calls.cs:4897`) returns `void`; runs only when `calleeType is UnknownType` (`:528-543`) — mutually exclusive with the typed seam; overload selection by `ClrArityFits` + `ClrParameterAccepts` (`:5520`), `fitting.Count > 1` → silence.
- Fidelity: `InteropConformanceTests.SemanticTypeFidelity_…` (`:1240-1320`) five cells — `Stack[int].count-correct` and `peek-nested` pass **vacuously** (unannotated / correct destination; Unknown satisfies). `WarmColdDifferentialTests` still uses the `list[Queue[int]]` workaround (`:55-89`). Fixtures `unmapped_bcl_generic_{ok,type_mismatch,nested}_1640`.
- `ResolveGenericModuleClassMember :1411` has the callee-vs-value precedent (`ReferenceEquals(memberAccess, _currentCallCallee)` at `:1451`) [CORRECTED: was `:1483`]; `SetClrPropertyCallLowering` (`SemanticInfo.cs:566`) already materializes the zero-arg collapse. Verifier measurements @ 6e2b68812 (`run`): `x: str = s.count` → SPY0908/CS0029; `x: str = s.Peek()` → SPY0908/CS0029 — both cells confirmed live. DP sites by grep: `Access.cs:280/403/782/884`, `Expressions.cs:299/590`, `Operators.cs:1194` = 7, matching the allowlist's `4 + 2 + 1`. `ClrTypeBridge.MapsToSharpyBuiltin` does **not** exist — Design Decision 4 introduces it (`MapGenericTypeCore` is `ClrTypeBridge.cs:365`, private). `ClrArityFits` is at `:5560`, `ClrParameterAccepts` at `:5520`.

### Defect Class
- **Violated contract:** every member access on a CLR-origin receiver has the reflected semantic type, in both spellings, in callee and value position, so a mistyped destination is SPY0220 and a bogus member is SPY0203 — never Unknown.
- **Meta-class:** position/route missing a shared check → one resolver at one seam; instruments passing vacuously (two fidelity cells); hand list (`IsExplicitlyMappedCollectionReceiver`).
- **Known member cells:** #1640 (reopened twice: CLR-cased spelling; properties / non-generic / struct).
- **Sibling cells:** receiver {mapped generic `List[int]`, unmapped generic `Stack[int]`, nested `Stack[Queue[int]]`, non-generic class `StringBuilder`, struct `DateTime`, user class inheriting CLR `class IntList(List[int])`} × member {single-overload method, overload group, property, field (`Vector2.X`, `DateTime.MaxValue`), indexer} × spelling {Pythonic, CLR-cased} × destination {correct, wrong → SPY0220, bogus member → SPY0203} = 180 nominal; N/A cells (no field on `Stack`, etc.) declared in the generator — expected ≈ 90 live cells. Plus callee-position property (`s.count()` → collapse, typed) × value-position property.
- **Standing harness:** `InteropConformanceTests` (blind to permissive Unknown — its verdict is "binds in Roslyn"); the fidelity method is the right instrument with the wrong cells. Phase 4.2 adds `ClrMemberFidelityMatrixTests` and the wrong-type twins. `DeliberatelyPermissiveRatchetTests` (count-only; drains).
- **Owner rulings:** R5 (2026-08-27).
- **Sibling class found by /verify-implementation 2026-08-30:** #1705 — a reflected `Type` is NRT-blind, and no reflection site read the member's declared `T?`; the #1640 cure carried that gap to stdlib receivers that used to fall through to the permissive channel (`s.Proxies = None`, declared `Dict<string,string>?`, went from accepted at BASE to SPY0229). Remediated at the resolver + call-seam return/parameter sites via `Discovery/ClrDeclaredNullability` with 12 `nrt.*` cells in `ClrMemberFidelityMatrixTests` (2 parameter cells declared N/A: a `None` argument is never checked against parameter nullability — the parameter-arm refusal is #1705's remaining piece); `TypeInferenceService`, `GenericReferenceResolver` and eager-discovery sites stay on #1705.

### Adversarial Review
- **Alternative root cause:** the emitter, not the checker (wrong C# name). Refuted: `RecordResolvedClrMemberName` already materializes `Length`/`Count`; the emitted C# is right and only the *type* is missing (CS0029, not CS1061).
- **Inert:** typing the member but leaving the DP arm reachable one call later for another shape (e.g., static receivers, `ref` params). The ratchet count is the tell: `Access.cs` #1640 entries must reach 0 and the matrix must cover every receiver kind the arm order enumerates.
- **Direction:** newly refused: wrong-typed destinations (were CS0029 behind SPY0908) and bogus members on now-typed receivers (were CS1061 behind SPY0908); `x: str = s.count` measured SPY0908 at the base sha per cell. Any program that ran at the base and is refused after is a regression — the sweeps before/after are the control; a false refusal on interop that .NET binds is "strictly worse than the ICE" (`Calls.cs` policy) and blocks the commit.
- **Blast radius:** every stdlib-interop fixture, `Sharpy.Stdlib.Tests` (its BUILD — Stdlib `.spy` sources reach CLR members), `Sharpy.Cli.Tests`, warm/cold harness, all three sweeps, LSP hover types.
- **Generated artifacts:** spy-stdlib C# and spy-test C# may change if a member type was Unknown in a Stdlib `.spy` — regen after 4.1 Task 1 and read the diff as findings.

### Design Decisions
- `Semantic/ClrMemberTypeResolver` (one class): input (constructed CLR receiver `Type`, member name, position ∈ {callee, value}); output `Resolved{MethodGroup(candidates), Property(type), Field(type), Indexer(candidates)} | Absent | Inconclusive`. Name matching accepts the reverse-mangled Pythonic spelling **or** the verbatim CLR name (one table, both directions — the `GetMemberNameSurface` rule promoted from the absence proof to the typing path). Every receiver arm (`BuiltinType`, `GenericType` mapped/unmapped/nested, `UserDefinedType` with `ClrType`, inherited CLR) calls it after the Sharpy-surface lookup misses; the three DP sites are replaced by `Absent → SPY0203`, `Inconclusive → DeliberatelyPermissive` with a **new** issue reference (only reflection-load failures land there).
- Callee-position property: resolver returns `Property(type)`; the call seam records `ClrPropertyCallLowering` and types the call node as the property type (the `_currentCallCallee` precedent) — `s.count()` stays legal and typed.
- Overload groups: the member seam returns a `MethodGroup`; `CheckClrInstanceMethodCall` gains a return type (the selected overload's mapped return, or Unknown-with-DP only when `fitting.Count > 1` — cited to a new issue for multi-candidate typing), replacing the `methods.Count != 1 → null` bail.
- `IsExplicitlyMappedCollectionReceiver` asks the bridge (`ClrTypeBridge.MapsToSharpyBuiltin(definition)`) instead of re-listing its arms.
- The four mis-cited DP sites get their own issue references (filed in this batch, one per class) or a typed answer where trivial; the allowlist's #1640 entries drain to 0.

### Phase 4.1: one resolver at one seam
#### Tasks
1. **Control run** — whole gate + three sweeps at the base sha; counts recorded. No commit.
2. **Resolver** — `Semantic/ClrMemberTypeResolver.cs` (new), `ClrTypeHelper.cs` both-spellings lookup, `Access.cs` arms `:330-405`, `:717`, `:779-784`, `:884`; `IsExplicitlyMappedCollectionReceiver` → bridge query; `Calls.cs:4897` returns a type. Commit: `fix(semantic): one CLR member-type resolver for every CLR-origin receiver, both spellings, callee and value position (#1640)`
3. **Allowlist** — drain the three #1640 sites; re-cite the four others to new issues (`gh issue create` per class; `:280` module export kinds, `:299` auto, `:590` type-ref value position, `:1194` conditional LCA). Commit: `chore(conformance): deliberately-permissive allowlist — #1640 entries drained; remaining sites cite their own classes (#1640)`
4. **Regen** commit.

### Phase 4.2: the fidelity matrix
**Acceptance:** `ClrMemberFidelityMatrixTests` (generated, ≈90 live cells, N/A declared) green; mutation: restore the `receiverType is BuiltinType` property guard → property cells red; restore Pythonic-only name matching → CLR-cased cells red. Fidelity cells `count-correct`/`peek-nested` gain wrong-type twins. `WarmColdDifferentialTests` nested cell uses `Stack[Queue[int]]` directly. Sweeps after = sweeps before (counts) or the delta is enumerated and every newly-red cell classified.
#### Tasks
1. Commit: `test(conformance): CLR member fidelity matrix — receiver × member × spelling × destination; vacuous fidelity cells get wrong-type twins (#1640)`
2. Commit: `test(project): warm/cold nested cell reaches the gate through Stack[Queue[int]] directly (#1640)`

### Issues to Close
- #1640 — 4.1–4.2; **criterion:** matrix green @ sha with both mutations recorded; allowlist has no #1640 entry; sweeps before/after posted with counts; every newly refused cell has base-sha SPY0908 evidence.

---

## Batch 6 — Instrument repair (#1672 ledger rows C1–C7, process rows)

### Current State (@ 6e2b68812)
- **C1** `SourceGlobConformanceTests.FindRepoRoot :82-92` requires a `.git` **directory** (worktrees have a file); regex `:28-30` needs `"*.spy"` + `AllDirectories` on one line — `ProjectConfig.ResolveGlobPattern :478-503` (a `Matcher`) is invisible to it; the compile-side twin covers it behaviourally.
- **C2** `DiagnosticCodeEmissionSiteTests`: inclusion regex `// Active` only; 369 Active / 9 Reserved / 2 Retired / **2 `// Deprecated`** / **4 bare** (`SPY0000`, `SPY0312`, `SPY0384`, `SPY0385`) of 386; tripwire `> 100`; emission-site check is a substring scan that counts comments.
- **C3** `AugmentedAssignmentSingleEvaluationTests` axis = `AssignmentOperator`; `SliceSpec.At` (#1665) has no evaluation-count row (only a constant-index substring assert at `RoslynEmitterFactSwitchTests.cs:114`).
- **C4** `CallableReferenceLowering` recorded for registry builtins only (`Overloads.cs:534-582`); `MergeFrom` membership **unverified**; D1.3 `CallableReferenceConformanceTests` does not exist.
- **C5** `QualifiedBareSpellingConformanceTests`: 20 hand-listed scenarios; `generic` shape missing for `typeTestExcept`, `nested`, `valuePattern`, `value`, `call`; `alias` shape only on `call`; no canonical/alias/user-alias axis; no totality guard; the two-file `lib.Handle` value-position cell (982fb8f14) is a manual probe. `PrimitiveConversionResolver` call sites: 7 in 2 files (`TypeChecker.Expressions.cs:575-657`, `Calls.cs:330`).
- **C6** A1.6: 5 of 10 switches untouched (`ExecutionOrderAnalyzer.cs:217`, `TypeChecker.Utilities.cs:109`/`NarrowingFlowAnalysis.cs:122,158`, `AugmentedCollectionAssignment.cs:144-150`, `CodeGenInfoComputer`×6/`NameResolver.Declarations`/`ModuleLoader`, `ExhaustivenessHelper.cs:66`); prior plan checklist `:552` vs `:559` contradict.
- **C7** 23 remaining `passed > total / 3` asserts in 8 files under `Properties/Semantic/`.
- **Process:** five guard commits without mutation records (f70beaa25, 22a6ea483, f1f3572bc, b8440e9c9, 54c652842); two commits carrying another batch's content. [verifier: `git log -1 --format=%B` on all five has zero hits for `broken|→ red|mutation` @ 6e2b68812 — not refuted; one auditing agent read a "three-test" note in f70beaa25's body, which is the guard #1633 reopened as vacuous, so a note is not a record.]

### Defect Class
- **Violated contract:** an instrument fails when the thing it guards is broken (§2); hand-enumerated rosters are total.
- **Standing harness:** these *are* the harnesses; the cure is the falsifiability record per row.

### Phase 6.1: source-scan guards enumerate by structure
#### Tasks
1. **One glob helper** — `Sharpy.Compiler/Project/SourceGlob.Enumerate(baseDir, pattern)` wrapping both `Directory.EnumerateFiles`/`GetFiles` and `Matcher` with the crash-bundle predicate; all 19+1 sites call it; `SourceGlobConformanceTests` scans for any `EnumerateFiles|GetFiles|new Matcher(` in `src/` outside the helper → 0; `FindRepoRoot` accepts a `.git` file. Mutation: a raw `Directory.GetFiles(…, "*.spy", AllDirectories)` in a test → red; delete the predicate from the helper → compile-side twin red. Commit: `refactor(project,test-infra): one source-glob helper; the scan is structural and worktree-safe (#1672 C1)`
2. **Closed label set** — every `SPY\d{4}` const carries exactly one of `Active|Reserved|Retired`; `Deprecated` folded into `Retired` with its note; the four bare codes labelled; tripwire = total consts − Reserved − Retired; emission-site check by Roslyn identifier scan (comments excluded). Mutation: remove a label → red; reference a code only in a comment → red. Commit: `test(diagnostics): every diagnostic code carries exactly one status; emission sites are identifiers, not text (#1672 C2)`

### Phase 6.2: rows C3–C5
#### Tasks
1. **Evaluation-count harness gains a subscript axis** — `IndexEvaluationCountTests` (or a second theory in the #1226 file): position {augmented target, multi-axis subscript read, multi-axis subscript store, slice bound} × index expression `probe()` with a runtime counter printed; `SliceSpec.At(probe())` evaluates once. Mutation: splice the index twice in `GenerateSliceSpec` → red. Commit: `test(codegen): evaluation-count harness covers multi-axis subscripts and slice bounds (#1665, #1672 C3)`
2. **`CallableReferenceConformanceTests`** — function {registry builtin, user def, user overload set, instance method, static method, CLR static, function-typed variable} × element type {int, str, user class} × target {`list[...]` element, call argument, default, return}; asserts output; verifies `_callableReferenceLowerings` in `MergeFrom` (confirmed present at `SemanticInfo.cs:1666-1667` @ 6e2b68812) with a **two-file** cell (the callable reference in `lib.spy`, the consumer in `main.spy`) so the `MergeFrom` row has a red state — dropping the merge line → that cell red; single-file cells never merge [ADDED: missing step]; plus the `SemanticInfoMergeConformanceTests` row. The user-overload-set cell records whether the lowering is needed (if C# resolves it, the cell documents that as the positive control, not an allowlist). Commit: `test(semantic): callable-reference conformance matrix; MergeFrom row (#1672 D1.3)`
3. **Qualified/bare axes + totality** — `QualifiedBareSpellingConformanceTests`: shape axis {plain, generic, canonical-alias, user-alias} × every position, N/A declared with a reason; the `lib.Handle` two-file value-position cell; a totality assertion that every (position × shape) pair is a scenario or an N/A; scoped source scan: `IsPrimitiveConversion(`/`ResolveOverloads(` appear only in the two files (count asserted). Mutation: drop a scenario → totality red. Commit: `test(conformance): qualified/bare matrix is total over position × shape; alias axes added; conversion-resolver call sites scanned (#1672 C2c)`

### Phase 6.3: A1.6 remainder and the proportion instruments
#### Tasks
1. **Five switches** — each gets generic enumeration or a `default:` that throws + a totality test where the arm list is load-bearing (mutation per file: delete one `case` → that file's totality test red; recorded in each commit body) [ADDED: missing step — mutation]; commit per file: `fix(semantic): <walker> enumerates statement kinds totally (#1635 meta-class, #1672 A1.6)`.
2. **Proportion asserts** — a shared `PropertyCorpus.AssertEveryStampleCompilesOrNamesItsCode(samples, allowlist)` in `TestInfrastructure`; the 23 asserts replaced; failures name the sample; a per-file allowlist with issue refs for genuinely unsupported shapes. Mutation: break one generator → red. Commit: `test(properties): property corpora assert every sample; proportion instruments retired (#1672 C7)`
3. **Process rows** — re-take the mutation for each of the five guards (worktree; `cp` restore) and post `broken → red (N) / restored → green (N) @ sha` for each on #1672; the two cross-batch commits get a traceability comment on #1654/#1639. No commit.

### Issues to Close
- #1672 — closes when the ledger is empty: rows H1.4/H2b (Batch 5), subsumption/PatternMatch/`BindingName` (Batch 2), E2/DA/A1/totality/`__call__`/`del` (Batch 3), C1–C7 + A1.6 + process (this batch), #1649 (Batch 0), #1665 row (6.2 Task 1). **Criterion:** every row's deletion cites its commit and mutation record.

---

## Batch 7 — Graduate `inplace_augassign`: #1614

### Current State (@ 6e2b68812)
- `FeatureFlags.cs:171-183` `inplace_augassign` `FeatureScope.Semantic`, behavioral (no `GatedConstruct`, no SPY0331). Single flag read `TypeChecker.Statements.cs:479-483`. Classifier `AugmentedCollectionAssignment.Classify :52-75` (identifier targets only; six rows; `list *=` excluded). Emitter `Assignments.cs:156-172` identifier branch only; Core mutators exist on both TFMs (`Extend`, `Update`, `DifferenceUpdate`, `IntersectionUpdate`, `SymmetricDifferenceUpdate`, dict `Update`); **no** `operator +=` and **no** `NET10_0_OR_GREATER` in Core. SPY0276 narrowed-receiver refusal fires in both modes (`:359-370`). SPY0478 hint (`TransitionWarningValidator.cs:359-397`) suppressed indirectly by the materialized entry; retirement touches `DiagnosticCodes.cs:700-703`, `DiagnosticExplanations.Validation.cs:605-615`, `DiagnosticPublisherTests.cs:204`, `TransitionHintIntegrationTests.cs:116-147`, `inplace_augassign_ungated.warning`. Precedent: `failable_cast` two-step (e3202465a → 8a0296eae). `FeatureFlagsTests:58-70` exact set (unchanged at step 1). Fixtures: `experimental/inplace_augassign_{gated,future_import,multifile,ungated}`, `narrowed_augassign_refused{,_inplace}`, `narrowed_augassign_rebind_workaround`. `InplaceAugassignTests` two ungated facts invert (`:19` expects "2", `:64` expects "3"). `assignment_operators.md` is 12 lines with no collection semantics; `feature-lifecycle.md` exit criteria require the spec page **before** graduation; the C#14 design doc header already records the mechanism as settled (mutation call). #1615 is CLOSED.

### Defect Class
- Defect Class: N/A — feature graduation (ruled). Contract enforced: augmented assignment on a mutable collection mutates the receiver in place through every alias, at every target position, on every TFM — matching CPython.
- **Sibling cells:** operator {`+=` list, `*=` list, `|=` set, `&=` set, `-=` set, `^=` set, `|=` dict} × target {identifier, attribute `self.xs`, index `d[k]`, nullable-narrowed, isinstance-narrowed (refused), frozenset (rebinds)} × alias observation {second local, parameter alias, field alias}.
- **Standing harness:** `DifferentialExecutionTests` — augmented-alias probes join the hand-picked corpus (CPython agreement is the graduation evidence). `#1226` harness gains the index-target rows.
- **Owner rulings:** Q4; #1428 ruling 2026-08-13 (mechanism).

### Adversarial Review
- **Direction:** this batch changes the output of programs that observed rebind semantics through an alias — every FBIT/metamorphic fixture whose `.expected` encodes the old value is a **ruled** diff and each is re-derived from `python3` (evidence in the commit body), never from the new Sharpy output. A fixture whose new output disagrees with python3 is a bug.
- **Inert:** flipping the default while the attribute/index arms stay identifier-only (`Classify :57`, emitter `:154`). The matrix's attribute/index cells are the tell.
- **Blast radius:** whole FBIT, metamorphic corpus, spy-test C# (stdlib tests use `+=` on lists), `Sharpy.Stdlib.Tests`, LSP transition-hint tests. Snapshot fixtures with `xs += …` change C# (`Extend` call) — ruled.

### Phase 7.1: spec first
#### Tasks
1. `docs/language_specification/assignment_operators.md`: "Augmented assignment on collections" — operator × type table, alias semantics, attribute/index targets, `*=`, frozenset rebinds, narrowed receivers (SPY0276 steer), executed examples; `feature-lifecycle.md`: pilot-table row, "behavioral-semantics flag" species note (proven by runtime `.expected` twins, not snapshots), exit criteria for semantics flags; `csharp14-…md` tracker closed (mutation call permanent; C#14 operator declined for the compiler path). Commit: `docs(spec): augmented assignment on collections mutates in place; feature-lifecycle species note (#1614)`

### Phase 7.2: scope extension, then the flip
**Acceptance:** `AugmentedCollectionMatrixTests` (7×6×3, N/A declared) prints CPython's values (python3 evidence per cell in the generator comments); `#1226` rows for `d[probe()] += v` evaluate once; `InplaceAugassignTests` pairs collapsed; SPY0478 retired (tombstone `// Retired (#1614) …`, explanation deleted, validator method + `IsAliasObservable/HasSecondBinding/Walk` deleted, LSP tests updated); `.features` sidecars dropped; `inplace_augassign_ungated` renamed `augassign_alias_mutates` with `.warning` empty (the "no hint" positive control) and python3-derived `.expected`; `narrowed_augassign_refused_inplace` deleted; `IsNoOp: true` with the graduated description; `FeatureFlagsTests` exact set unchanged; differential probes green. Mutation: restore the flag guard → matrix red.
#### Tasks
1. **Classifier + Core + emitter** — `Classify` accepts `MemberAccess`/`IndexAccess`; `list *=` → `Sharpy.List<T>.InPlaceRepeat(int)` (new, both TFMs, `[EditorBrowsable(Never)]`, python3-verified for 0 and negative); emitter mutation call for the two new target arms (receiver generated once). Commit: `feat(semantic,codegen,core): in-place augmented assignment covers attribute/index targets and list *= (#1614)`
2. **Flip + retire** — delete the flag guard; `IsNoOp`; SPY0478 retirement; fixtures/tests rewritten with python3 evidence; regen (ruled diff). Commit: `feat(compiler): graduate inplace_augassign — in-place augmented assignment is the language (#1614)`
3. **Matrix + probes** — `AugmentedCollectionMatrixTests`, hand-picked differential probes, `#1226` rows. Commit: `test(semantic): augmented-collection matrix and CPython probes (#1614)`
4. **Follow-up filed** — "remove `inplace_augassign` from `KnownFeatures` after one release" (step 2), referencing this batch's sha.

### Issues to Close
- #1614 — 7.1–7.2; **criterion:** every checkbox answered on the issue with its commit; matrix green @ sha with the flag-restore mutation red; every re-derived `.expected` cites python3; step-2 issue filed.

---

## Testing Strategy (round)
- Every guard commit body records `broken → red (N) / restored → green (N)`; absence assertions carry positive controls (§2); every newly refused program lists its prior-commit behaviour (§3) — Batches 1, 2, 4 script this against the base sha.
- Regen after the first Semantic/CodeGen commit of each wave; Batches 1 (covariant spellings) and 7 (alias values) carry *ruled* diffs, re-derived from the spec / python3, never from the new output.
- Gate: whole solution + the three sweeps for Batches 1–4 and 7; counts `passed/failed/skipped @ sha (measured)`; a red is attributed only after a control at the base sha; Batch 4's before-run is the control.
- Spec deliverables (executed examples, docs-need-execution-evidence): `arithmetic_operators.md` (1), `match_statement.md` (2), `exception_handling.md` + `del_statement.md` + `dunder_methods.md` (3), `assignment_operators.md` + `feature-lifecycle.md` (7).
- Agent prompts carry §9's shared-tree block verbatim; mutation restores via `cp`, never `git`; every plan-cited line number is re-read at the batch's opening sha before it is edited.

## Issues to Close (index)
| Issue | Batch | Close criterion (summary) |
|---|---|---|
| #1649 | 0 | three discriminating control-pair verdicts posted with shas |
| #1633 | 5 | guard red at HEAD-bypassed naming the poisoner; green after (b); clone deleted or its blocker filed; zero new bundles after the gate |
| #1671 | 1 | 61-cell literal matrix green; both mutations red; ratchet `Literals.cs` at 0 |
| #1666 | 1 | 420-cell width matrix green (incl. unary/`>>`/`**`/mixed-width rows) [CORRECTED]; every cell carries its base-sha classification (ICE / SPY0220 / worked); the augmented-narrowing rule is written into `assignment_operators.md` and executed; refused → accepted cells listed in the commit body |
| #1670 | 1+2 | 120-cell pattern matrix green; ratchet at 0; spec executed |
| #1669 | 3 | 39-probe execution-order corpus green; reversion red |
| #1668 | 3 | for/while-else DA cells with edge-cut mutation red; closed by note |
| #1640 | 4 | ≈90-cell fidelity matrix green; allowlist has no #1640 entry; sweeps before/after posted |
| #1672 | 2,3,5,6 | ledger empty; each row's deletion cites commit + mutation record |
| #1614 | 7 | matrix green; flag-restore mutation red; every re-derived `.expected` cites python3; step-2 issue filed |

## Verification Summary

**Result:** PASS WITH CORRECTIONS (first pass: NEEDS REVISION; re-stamped the same day after the owner ruled the two open items — see "Rulings applied")
**Adequacy:** CLASS (Batches 0–6); N/A for Batch 7 (ruled graduation)
**Verified on:** 2026-08-28 @ 6e2b68812

### Rulings applied (owner, 2026-08-28, in the verify session — post on #1666 / #1672 when the batches open)
1. **#1666 augmented narrowing = C#'s rule** — narrow iff the RHS is implicitly convertible to the target (narrow-or-equal width, or an in-range integer constant); `x8 += i` stays SPY0220. Written into Batch 1's Direction, Design Decisions and Phase 1.3 acceptance.
2. **#1666 widened matrix confirmed** — unary `-`/`~`, `>>`, `**`, mixed widths, augmented split by RHS kind (420 cells); three typing arms. Batch 1 is CLASS.
Also folded in on the verifier's authority (routine, no design choice): Phase 3.3 Task 1 covers the `for`-statement target (parser SPY0104 at HEAD); mutations named for the store, `__call__`, `del`, A1.6 guards; C4 gets a two-file cell; H1.4 gets a runtime assertion; Phase 1.4 claims no guard. Warnings 1–5 below are therefore **resolved**; 6–9 remain as notes.
**Plan file:** `.claude/plans/plan-de9fe3.md`

Method: every issue read (`gh issue view`, all 10 in scope OPEN; #1651/#1665/#1667/#1615/#1641/#1226/#1428 CLOSED as the plan states); all five Q1–Q5 ruling comments and the #1666 widening comment read in full and their URLs resolved; every python3 behaviour claim re-run (3.12); 46 Sharpy programs run with `sharpyc run` (never `emit`) against the CLI built at 6e2b68812; six read-only auditors refuted the line citations batch by batch; the arithmetic spec, the bitwise spec and `assignment_operators.md` quoted at the cited lines. The result is NEEDS REVISION because Dimension 6 fails for Batch 1's #1666 arm (the fix as designed does not reach three live arms and its augmented-narrowing rule reverses a standing refusal without saying so) and because Batch 3's E2 premises are wrong in a way that changes the parser scope. Every other batch survives with corrections.

### Corrections Made
- **Stamp + adequacy line** added at the top.
- **Premise table, #1666:** operator set widened to `>>`, unary `-`/`~`, plain `**`, mixed widths; `divmod` noted as already honest (SPY0220 naming `tuple[int32, int32]`). All measured with `run`.
- **Batch 5:** "four bridged collections" → five unserialized fields; `IsBuiltinSymbol` 16 → 15 callers; `ProcessFromImport :228-247` → `TryDefineFromImport :240`; H2b note on the deviation from the Q2 ruling's cure wording (`CrashRoot` exists on `ProjectConfig`).
- **Batch 1, #1671:** 11 → 13 `_targetTypeContext` setter lines (adds `Literals.cs:135/:1355`, already inside the deleted ranges); 11 → 13 annotation-consuming `MapType` sites (12 ratcheted + the exempt model — matches Phase 1.1's `4+7+1`); the `return` cell (`return [Derived()]` → SPY0908/CS0029) and the call-argument cell (`g([Derived()])` → SPY0908/CS1503) recorded as measured live.
- **Batch 1, #1666 (decision-changing):**
  - `IsFlooredNumeric` doc does *not* claim to exclude narrow widths (it lists them) — claim deleted.
  - `GetPromotedType` does not serve LCA (`FindLeastCommonAncestor :2086` never calls it; the other consumer is `BuiltinReturnTypeInference.cs:142`) — conclusion (floor in the operator arms, not in `GetPromotedType`) unchanged.
  - The `IntegerPow*` arms cast the **operands** up to `int`/`long`; they are not a narrowing precedent — `n: int8 = a ** b` is SPY0908 at HEAD.
  - "Native-operator augmented forms already narrow in C#; the cast is emitted only where the lowering is a call" — **refuted**: `emit csharp` shows `x8 = x8 + y8` (`GenerateAugmentedBinaryExpression`, `Assignments.cs:797`) and `run` gives CS0266 for `x8 += y8`, `x8 **= y8`, `x8 <<= y8`. `NarrowTo` must cover every augmented operator.
  - The issue's own repro `x8 %= 2` is **SPY0220 at HEAD** (`Statements.cs:388` augmented check), as are `x8 += 1` and `x8 += i`; Phase 1.3's acceptance flips them to accepted — a refused → accepted direction change the plan now has to rule (C# rule recommended) and spec.
  - Three typing arms named for the floor: binary (`:294-328`), unary (`InferUnaryOpTypeUncached :1044-1079`), power (`InferPowerResultType :342`).
  - Matrix 250 → 420 cells; acceptance, tasks, close criterion and the index row rewritten accordingly. File/line fixes: `ClassifyFlooredArithmetic` is in `TypeChecker.Expressions.Operators.cs:316-339`; floor-div/mod emitters are `RoslynEmitter.Operators.cs:1170/:1019`; `SetPatternType` at `:1004`.
- **Batch 2:** erasure switch `:833-841`; `ClassifyTypeTestAnnotation` callers `Statements.cs:1573/:1598`, `Operators.cs:1229`; Direction: `case list(xs)` on `x: str` is CS0305 at HEAD (not CS8121) — measured; positive controls recorded (CS0305 on `object`, CS8120 for the #1651 sibling).
- **Batch 3, E2 (scope-changing):** `with … as p.x`, `except E as p.x`, `(p.x := 5)`, `[… for p.x in xs]`, `[… for xs[0] in ys]` **and the `for p.x in xs:` statement** are all parser **SPY0104** at HEAD; "the parser accepts `obj.attr`/`xs[0]`" and "the `for` statement form is supported" are refuted. Direction rewritten from SPY0104. `__call__` refusal messages recorded for both arms. Q2's "lambda-captured never-assigned local" row added as Phase 3.2 Task 3.
- **Batch 4:** `ResolveGenericModuleClassMember :1411` (`_currentCallCallee` test `:1451`); `MapsToSharpyBuiltin` marked new; `ClrArityFits :5560`; both spelling cells recorded as measured SPY0908/CS0029.
- **Batch 6:** process-row note (zero `broken|red|mutation` hits in all five bodies).
- **Late auditor rows (folded in after the first stamp):** no `TaskType` switch exists in `ClrTypeHelper.cs` (Batch 4); `HandPicked()` has 45 probes, not 26 (Batch 3); the seven LSP `WithItem.Name` readers listed (Batch 3); `_callTargets` merge is at `SemanticInfo.cs:1540` (`:1526` is `MergeFrom`'s start). The `BasicBlock.cs:107` "consumed only by NarrowingFlowAnalysis" doc **is** stale as the plan says — `DefiniteAssignmentAnalysis.cs:57/:145` and `MustAssignDataflow.cs:56` read `EntryRebinds` (verifier grep).

### Adequacy
- **Contract:** named in every batch. Batch 1's arithmetic contract ("the recorded type of an arithmetic expression **is** the CLR result type") is right; the plan just did not enumerate every arm that produces an arithmetic expression.
- **Matrix:** Batch 1 #1666 — **smaller than the live class** at 6e2b68812: unary `-`/`~` (`InferUnaryOpTypeUncached`), `>>` (bitwise arm — present in `bitwise_operators.md` but absent from the operator list), plain `**` (`InferPowerResultType`), mixed narrow widths, and the augmented store split by RHS width (two base-sha behaviours). Drafted into the plan as `[CORRECTED: adequacy]`; the author confirms. Batch 1 #1671 — larger than the repro (5×3×3 + 11 contexts + the `return` cells; the `return` and call-argument cells are measured live). Batch 2 — 120 + 32 + 4 cells, larger than the repro; five call sites enumerated and grep-confirmed as the complete set. Batch 3 — try × 21, with × 12, loop-else × 6, DA 15 × 9 × 2, stores 8 × 5, `__call__` 4×3×3, `del` 5 — larger than the repro; but the store matrix's "for statement × attribute" cell is a *new* parser cell, not a supported one. Batch 4 — 180 nominal / ≈90 live, larger than the repro; the three DP arms are the complete CLR-member set (grep). Batch 5 — field × owner × entry-point matrix; complete against the reader grep (agent-confirmed 12 emitter/serializer sites + 2 writers + 1 guard read). Batch 6/7 — N/A / ruled.
- **Cure:** Batch 1 — materialize + ratchet (seam) for #1671; **promotion floor + `NarrowTo`** for #1666 — a seam, but only once it spans all three typing arms and every augmented operator. Batch 2 — one classifier (seam). Batch 3 — nesting fix + execution-order corpus; `GenerateStore` seam; one `TryResolveCallableObject`. Batch 4 — one resolver at one seam. Batch 5 — ownership by construction + reflection snapshot guard. Batch 6 — falsifiability record per row. All match the §1 table.
- **Harness:** named per batch with the allowlist delta: Batch 1 adds `EmitterTypeAnnotationConsumptionTests` (new ratchet `4/7/1` → `0/0/0` by Batch 2); Batch 2 replaces `PatternMatchPropertyTests` (6 of the 29 proportion asserts — count confirmed 29 in 9 files); Batch 3 extends `DifferentialExecutionTests` hand-picked arm; Batch 4 drains `deliberately-permissive-allowlist.txt` #1640 entries (`4+2+1` → the four non-#1640 sites re-cited); Batch 5 adds `MasterRegistryImmutabilityTests` and deletes `SharedRegistryOwnershipTests`. Batch 0 is the calibration of the skills themselves (controls confirmed: facts at e02f39fbb, `Known verdict` headers present, #1641 CLOSED — the pair is non-discriminating today exactly as the plan says; the audit roster names 9 of the 10 allowlists — glob confirmed).
- **Owner rulings:** Q1–Q5 URLs resolve to the owner's comments and the plan follows them. Two things to post when the batches open: (i) #1666 — the plan refuses plain narrow stores per spec Key Rule 1 (Rule 7) where the 2026-08-28 widening comment offered "cast at every store / Core overload per width"; state the spec basis and the augmented rule chosen; (ii) #1672 H2b — the `CrashRoot` redirect in place of the ruling's pre-compile clean.

### Falsifiability
- `MasterRegistryImmutabilityTests` (5.1): mutation [named: clone bypassed at HEAD → red naming symbol/field/writer; "no fix commit lands until the guard is red"]; exemption [not subject]; positive control [yes: sequential `AnalyzeDocument` reuse → SPY0909 or the traced shape]; by-direction [n/a]. Inert-fix probe named (guard green with the clone removed, 5.3).
- `IncrementalCodeGenInfoRoundTripTests` (5.2): mutation [named: skip the deserializer's `SetCodeGenInfo` → red by C# comparison]; positive control [cold vs warm C# identical]; snapshot byte-identity as the emitter-read control.
- `StaticStateConformanceTests` rule 3 (5.3): mutation [named: add a `ConcurrentDictionary<string, TypeSymbol>` field → red]; exemption [the allowlist — parameterize so the mutation is an *unlisted* field, otherwise the exemption is the subject].
- H1.4 immutable views (5.3): mutation [named as "downcast and mutate → compile error"] — **a compile error is not a test going red**; write the probe as a reflection-level assertion (`GetType().IsArray == false`, `IsAssignableTo(typeof(ISet<>))` false) so it has a red state.
- `EmitterTypeAnnotationConsumptionTests` (1.1): mutation [named: add `MapType(list.ElementTypeHint)` to `GenerateListLiteral` → red]; exemption [`MapClassifiedTypeOperand` by name — not the subject, the mutation lands outside it]; positive control [`CarrierOnlySource_IsNotFlagged` twin — yes]; stale-entry failure named.
- `CollectionLiteralContextualTypingMatrixTests` (1.2): mutation [named: revert the contextual record → covariant cells red; revert the `GenericDefinition` stamp → depth-2 dict red]; discriminating output [yes — `xs.append(Base())` after the literal fails CS1503 on `List<Derived>`]; by-direction [n/a — no new refusal; SPY0220 on mistyped cells is the existing behaviour, positive control measured].
- `NarrowWidthArithmeticMatrixTests` (1.3): mutations [named after correction: binary floor / unary floor / `**` floor / cast — four]; positive controls [`print(a + b)`, `c: int = a // b`, `xs: list[int8] = [a, b]` all measured working at HEAD]; by-direction [specified: base-sha script classifies every cell ICE / SPY0220 / worked — **now required both ways** because `x8 %= 2` flips SPY0220 → accepted].
- Phase 1.4 pattern arm: **no guard** — the fallback still fires until Batch 2, so the change is behaviour-preserving by design; the commit must say "no guard claimed; ratcheted count 3 unchanged" rather than imply a test.
- `PatternConformanceMatrixTests` (2.3): mutations [named: revert 2.1 Task 2 → object-subject cells red; revert 2.2 → subsumption cells red]; by-direction [specified and now measured: CS0305 / CS8120 at base]; positive control [`case list[int](xs)` SPY0125, `range` SPY0202].
- `BindingName` deletion (2.3): positive control [a hand-built `TypePattern{BindingName}` no longer compiles — compile-level; acceptable because the assertion is structural, pair it with the reparse-equivalence sweep count unchanged].
- Execution-order corpus (3.1): mutation [named: revert the nesting → 7 try cells red]; discriminating output [yes — prints the order; measured `try, finally, else` today]; oracle CPython.
- `RoslynEmitterStatementTests:965` structural assert (3.1): mutation [implicit: revert → red]; today's substring assert is documented as the vacuous one.
- DA matrix axes (3.2): mutation [named: cut the else edge in `BuildFor` → for-else cells red]; `CfgStatementTotalityTests` [named: delete `case DeferStatement` → red]; `BlockExpressionTotalityTests` [named: remove the with-context `Expressions.Add` → red; add an unclassified `Expression` property → red].
- Store matrix (3.3): positive controls [identifier forms unchanged — yes]; mutation [**MISSING** — expected: delete the SPY0142 arm → the except-as cell goes back to SPY0104 (red because the cell asserts its code); revert the `GenerateStore` routing → the `with … as p.x` cell red].
- `__call__` matrix (3.4): positive control [`obj.__call__(1)` still refused — yes]; mutation [**MISSING** — expected: bypass `TryResolveCallableObject` → SPY0201 on every cell].
- `del` matrix (3.4): mutation [implicit: delete the parser arm → SPY0100 ≠ SPY0144 red].
- `ClrMemberFidelityMatrixTests` (4.2): mutations [named: restore the `BuiltinType` property guard → property cells red; restore Pythonic-only matching → CLR-cased cells red]; by-direction [specified: `x: str = s.count` SPY0908 at base — measured]; the two vacuous fidelity cells get wrong-type twins (positive control for the absence assertion) — yes.
- Batch 6 C1 (source-glob helper): mutations [named: raw `Directory.GetFiles` in a test → red; predicate deleted → compile-side twin red]. C2 (label set): mutations [named: remove a label → red; comment-only reference → red]. C3 (`SliceSpec.At` evaluation count): mutation [named: splice twice → red]. C4 (`CallableReferenceConformanceTests`): mutation [**MISSING** — and the `MergeFrom` claim needs a **two-file** cell, otherwise dropping `_callableReferenceLowerings` from `MergeFrom` (confirmed present at `SemanticInfo.cs:1666-1667`) leaves the matrix green]. C5 (totality): mutation [named: drop a scenario → red]. C6 (A1.6 switches): mutation [vague — "arm list is load-bearing"; expected: delete one `case` → the totality test red, per file]. C7 (proportion asserts): mutation [named: break one generator → red].
- `AugmentedCollectionMatrixTests` (7.2): mutation [named: restore the flag guard → red]; inert-fix tell named (attribute/index cells); every re-derived `.expected` cites python3 — yes.
- Batch 0 controls: discriminating by construction (CELL vs CLASS, inert vs real); "if the two verdicts agree the skill is revised" — yes.

### Warnings
1. **Batch 1 / #1666 — author decision required before `/implement-plan`:** choose the augmented-narrowing rule (verifier recommends C#'s: narrow iff the RHS is implicitly convertible to the target; otherwise the `Statements.cs:388` SPY0220 stays), write it into `assignment_operators.md`, and list the refused → accepted cells (`x8 %= 2`, `x8 += 1`, `x8 += i` if rule (b)) in the commit body. Confirm the widened matrix (unary, `>>`, `**`, mixed widths). Until then the batch is CELL.
2. **Batch 3 / E2 — scope is larger than written:** at HEAD the parser accepts no non-name target in any of the five positions, including the `for` statement; `ForClause.Target: Expression` is a latent AST shape, not a supported one. Phase 3.3 Task 1 is parser + emitter work for `for` statements and comprehensions, not "comprehension and with-as only". Re-read `ParseForTargetElement :218` at the opening sha and record which shapes reach `Literals.cs:496-501`.
3. **Phase 1.4 is inert by design** until Batch 2 — fine, but no guard may be claimed for it and the ratchet count for `Patterns.cs` stays 7 (3 raw + 4 fallbacks), not 3, until 2.1 Task 2.
4. **H1.4 (5.3)** — "compile error" is not a red test; give the immutability probe a runtime assertion.
5. **C4 (6.2)** — add a two-file cell or the `MergeFrom` row is a source-scan only.
6. **Batch 5 Phase 5.2** — `Symbol.CodeGenInfo` has 15 non-accessor readers by the agents' grep (12 serializer + emitter/mapper sites) plus the LSP has zero; the acceptance grep is the right gate. `FrozenSet<string>`/`ImmutableArray` are fine (Sharpy.Compiler is `net10.0`-only).
7. **Batch 4** — `ClrTypeBridge.MapsToSharpyBuiltin` must be written (does not exist); `IsExplicitlyMappedCollectionReceiver`'s "keep in sync" comment at `Access.cs:1028` is the hand list to retire.
8. **Rulings to post on batch open:** #1666 spec-based refusal vs the widening comment's two cures; #1672 H2b `CrashRoot` redirect vs the pre-compile clean.
9. **Batch 7** — `InPlaceRepeat` must handle 0 and negative counts (python3: `[1,2] * -1 == []`, verified); the C#14 tracker already states "Mechanism is settled; semantics are not" — the spec page settles the semantics, so close the tracker only after the spec commit.

### Missing Steps Added
- Phase 3.2 Task 3: the Q2 "lambda-captured never-assigned local" probe/row (added inline).
- Phase 1.3 Task 1: unary and power typing arms (added inline); Task 2: `NarrowTo` for every augmented operator with the `Statements.cs:388` check consulting it (added inline).
- Phase 1.3 acceptance: base-sha classification for *accepted* cells, not only refused ones (added inline).
- Phase 3.3: parser work for non-name `for`-statement targets (Warning 2 — author to fold into Task 1).
- Phase 3.3 / 3.4 / 6.2 C4 / 6.3 C6: the named mutations above (Falsifiability rows) go into the acceptance bullets.
- Blast radius / gates: already named per batch (whole gate + three sweeps + LSP + warm/cold; regen after the first Semantic/CodeGen commit of each wave; Batch 4's before-run as control; Batch 1 and 7 ruled diffs). Nothing missing on Dimension 8; the plan also correctly puts a regen commit after Phase 1.2 (Stdlib `.spy` uses narrow ints and collection literals).

### Unchecked Claims
- H2b census numbers (9,545 bundles / 10,285 stray `.spy`) — not re-counted by the verifier; the Q2 ruling states the same numbers.
- `TypeSyntaxMapper.MapType :307-314` cached-annotation path (Batch 2 alternative root cause) — not read; the batch's discriminator (CS0305 persisting after 1.4) is the right probe.
- `QualifiedBareSpellingConformanceTests` "20 hand-listed scenarios" and the "19+1" source-glob sites — not counted; Phase 6.2/6.1 totality assertions will measure them.
- `DifferentialExecutionTests.HandPicked()` 26 probes / `:318` / `:1091` — agent report truncated before these rows; the `with` exclusion claim is the one that matters and is checked by the 3.1 corpus landing in the hand-picked arm.
- `ModuleRegistry.cs:398-501` "discovered symbols carry zero members" — file is not under `Semantic/`; not read. The measured cells (`StringBuilder`/`DateTime` members → CS0029) make the claim's consequence true regardless.
- Batch 5 "12 `SetCodeGenInfo` call sites" — agent-confirmed (`:134,166,187,218,241,279,298,317,337,372,387,489`); not independently re-counted.
- The five process-row commits — zero mutation-record hits by grep; not read in full.

---

<!-- Verified by /verify-implementation 2026-08-30 @ 14d4f4d43 — PASS after remediation (2 loops) -->
## Implementation Verification Report (2026-08-30)

**Plan:** `.claude/plans/plan-de9fe3.md` (stamp: PASS WITH CORRECTIONS; Adequacy: CLASS)
**Branch:** `dev` · **Scope:** `6e2b68812..14d4f4d43` (47 implementation commits `..c68a2683d`, 39 remediation commits) · **Verified on:** 2026-08-30

### Completeness (Agent 1)
42-item checklist. At c68a2683d: 0 of 10 issues closeable — MISSING: #1671 matrix, match_statement.md, SourceGlob helper, #1666 constant arm, warm-restore guard, Phase 0.3, ledger rows; GUTTED: MasterRegistryImmutabilityTests; VACUOUS: PatternConformanceMatrix, CallableReference MergeFrom cell, A1.6 totality; PARTIAL: Batch 7 cleanups, allowlist citations. After remediation every acceptance bullet has executing evidence @ 14d4f4d43 except the two deferred with an issue: SourceGlob helper + 3 A1.6 switches (Batch 6, F9/F10 — not started, cited in #1649's comment as residue), the #1705 remaining reflection sites. Issues to Close (owner closes): #1633 #1671 #1666 #1670 #1669 #1668 #1640 #1672 #1649 #1614 — each has an evidence comment naming commits, mutation records and the gate @ 14d4f4d43: CLOSEABLE.

### Regressions (Agent 2)
- Baseline @ HEAD: 23,249 / 3 / 2 @ c68a2683d (measured) — the 3 reds were `DefiniteAssignmentMatrixTests` (red since their own commit 8d2bebb28).
- Post-remediation @ FINAL: **23,611 / 0 / 4 @ 14d4f4d43 (measured)**; loop 1 gate @ 75b07aae5 was 23,606 / 5 / 4 (4 = #1705 helper on generic-parameter members, 1 = A4 del fixtures missing from the parity design allowlist) → fixed in 14d4f4d43.
- Regressions in kind found in the range: 4 (except-as DA leak 698d562bf; `case list(xs)` silent wrong branch; declaration-order-dependent overload selection 9aae978de; try/else/finally-without-except SPY0599 ICE 56a553b32) — all 4 fixed. Exposed pre-existing classes fixed: #1706 (store vs declared type), #1705 partial (NRT nullability).
- Pre-existing reds: none remaining; the 4 skips are the KnownRed `.skip`s incl. `exit_order_dunder4_sole_return` (#1691).
- Expectation changes in range: 1 renamed `.expected` (augassign_alias_mutates, python3-derived), 2 deleted (`narrowed_augassign_refused_inplace.error`, ungated `.warning`), 2 modified `.error`/`.warning` (del_rejected per-target steer; hint_augmented_assign_aliased_set — SPY0478 retired), 1 modified `.error` at 14d4f4d43 (`clr_object_return_destination_refused_1534`: `object` → `object?`, the declared type; refusal unchanged), 1 new `.skip` with reason (#1691). None to make a test pass.

### Attribution (control run)
Control @ BASE 6e2b68812: the 3 HEAD reds did not exist at BASE; test-level control at 8d2bebb28 = 37 / 3 / 0 (same names) — never green. Loop-1 reds: all attributed to remediation commits by content (no BASE control needed). Every refusal added by the range was run at BASE (`run`): ICE→diagnostic (SPY0145, del steers, SPY0361/0202 pattern refusals), wrong→diagnostic (`case tuple(v)` 0-tuple), worked→rejected: 1 (`s.Proxies = None`, #1705) → fixed.

### Class contract status (Agents 3 + 4)
- Standing harnesses @ FINAL: DefiniteAssignmentMatrix, PatternConformanceMatrix (positional cells, ratchet 0), NarrowWidthArithmeticMatrix (908 cells), CollectionLiteralContextualTypingMatrix (66), ClrMemberFidelityMatrix (98 incl. 12 nrt), StoreTargetMatrix (49 incl. 8 declared-type), CallableObjectDispatch, ComprehensionContextualTyping, MasterRegistryImmutability (snapshot), IncrementalCodeGenInfoRoundTrip, with-exit-order (14) — all green.
- Allowlist delta: emitter-annotation-consumption drained 8→0; deliberately-permissive re-cited per site (#1674/#1675/#1676/#1677/#1678); interop `in_place_repeat # #1625` drained (rule added), the 8 remaining `#1625` rows test-drained and found load-bearing (issue closed by allowlisting — reopen recommended); frontend-parity +4 del keys in the parse-error design block.
- Cells probed by the auditors at c68a2683d: 323 (4a: 215 pass / 27 fail / 33 ICE / 22 untyped) + 223 (4b: 125 match / 19 genuine findings). Seam-or-arm: 4 ARM verdicts (Batch 2 positional, #1640 :403/:884 arms, #1666 constant, #1671 expectation) → all fixed at the seam.

### Guards mutation-tested
Every remediation commit body carries `broken → red (N) / restored → green (N)`; lead guards: IsHiddenFromSurface (red 1 / green 3), `_is_hidden_from_surface` (red 1 / 168), ClrDeclaredNullability.Apply (red 6/98 / 98), DeclaredBindingType (red 3/41), `_plainStoreTarget` arm (red 3/41), generic-parameter rule (absent → 2 red in the loop-1 gate / present → green). Vacuous/inverted/inert found by the audit: 3 (MasterRegistryImmutability, PatternConformanceMatrix, CallableReference MergeFrom cell) → all replaced; 2 parameter-nullability cells declared N/A with the reason (#1705).

### Architectural Review
Rule 2 carriers merged (TypeTestLowering, NarrowTo, ClrPropertyCallLowering, CallableReferenceLowerings in MergeFrom); SyntaxFactory only; no new TODOs without issues; format clean @ FINAL.

### Generated artifacts
Regenerated at 5ec968424 (13 files, every hunk read: Batch 7 in-place lowering; `!` where `T | None` flows into `T`; `Builtins.Len(x)` → `x.Length` on now-typed members; static `StringExtensions.Isdigit`; checked `NumericCheckedCast.ToInt`); staleness gates @ 14d4f4d43: spy-stdlib clean, spy-tests clean, stdlib docs no diff, oracle ledger no diff.

### Fixes Applied

| Commit | Description | Category |
|--------|-------------|----------|
| efd301d5c, 64d53e659 | except-as names close where the handler ends (`RebindScopeEntries`); loop-else DA cells assert the spec | regression / guard-fix |
| f3348aba7, 2f093e4a7, ebd50d985 | `try/else` without `except` → SPY0145 (was SPY0599 ICE); `del` per-target steers; 14 with-exit-order cells | regression / missing-impl / guard-fix |
| 56f66b2b4, 940519d4a, de8d03d86 | every CLR-origin receiver arm reaches `ClrMemberTypeResolver`; fidelity matrix non-generic/struct/inherited/static; per-site allowlist citations | seam-fix / guard-fix / cleanup |
| 135414edc, b8ae85a27, 4cf25bce2 | master-registry immutability snapshot restored; 66-cell literal matrix; warm-restore round-trip | guard-fix / missing-impl |
| 1d2ebc670, 004588139, 053b54388, 0fbba76e0, 18e921475 | one pattern classifier + one lowering read (silent wrong branch fixed); subsumption SPY0700; positional cells; ratchet drained to 0; match_statement.md | regression / seam-fix / missing-impl |
| 9b43f896e, 89143b39f, 9f3a49369, 4c5424c5b | augmented narrowing constant + attribute/index arms; uint32/uint64 typing; 908-cell matrix; spec | missing-impl / seam-fix |
| 92a1e222d, ad2e695c2, 63a7bd328, ed78e9d5f, 795fcad24, adfe0e2d3, 56fd7b311, 06c0992e7, 08c15b4a5 | overload gate on the literal (declaration-order regression fixed); `__call__` via the shared method-call path; with-as tuple binder + hoist drain; comprehension contextual typing; matrices; merge fixup | regression / seam-fix / guard-fix |
| 6c165d837, 501debc00, 77b5e5a13, 1fb5de005, 6d09afcaf | inplace_augassign graduation finished; docs generator + interop sweep skip EditorBrowsable(Never); spec cite; KnownRed reason | cleanup / guard-fix |
| e7bd003ad, 14d4f4d43 | #1705 declared NRT nullability at the resolver + call seam; generic-parameter members keep the reflected type; steer names the cast target | seam-fix (class exposed by #1640) |
| 60c249cf2 | #1706 stores checked against the declared type; `self.field` special case subsumed | seam-fix (pre-existing class exposed) |
| 5ec968424, 75b07aae5 | regen (13 generated files, each hunk read); `dotnet format whitespace` (5 files) | regen / cleanup |

### Issues Created

| Issue | Title | Reason |
|-------|-------|--------|
| #1680–#1697 | pre-existing classes and vacuous guards named by the audit (incl. #1688 float32 ctor arg, #1690 control transfer out of `finally` — ruled: named SPY error, #1691 CS0161 KnownRed, #1693 tuple/frozenset patterns — closed by design per the 2026-08-30 ruling) | class issue / sibling cell |
| #1698, #1699, #1700 | constants convert only at decl/argument positions; mixed-signedness promotion; `**` on uint64 in int64 | sibling cell (B1) |
| #1701, #1702, #1703 | mistyped tuple-slot store ICE; sequence pattern on `object` ICE; `Ok()/Err()` open-generic emission | sibling cell (A2/A5) |
| #1704 | false SPY0450 after a suppressing `__exit__` | sibling cell (lead) |
| #1705 | reflected CLR member types drop declared NRT nullability (remaining sites: TypeInferenceService, GenericReferenceResolver, eager discovery; parameter-arm refusal) | class issue (partially cured here) |
| #1706 | store checked against rebound/narrowed type — cured here | class issue (cured) |
| #1707 | `None` element in a tuple-literal RHS → CS0815 behind SPY0908 | sibling cell |
| #1708 | reified class patterns — implementation of the 2026-08-30 ruling | deferred work (ruling) |
| #1625 (closed) | comment: allowlist-and-close; all 8 rows still red under test-drain — recommend reopen | closed ≠ finished |

### Unresolved Items
- #1705 remaining reflection sites (TypeInferenceService:252/770/1152/1553, GenericReferenceResolver, eager discovery) and the parameter-arm None refusal — evidence in the issue.
- Batch 6 residue: SourceGlob helper (F9), 3 A1.6 switch totality tests (F10) — not started; cited on #1649.
- #1625 closed by allowlisting; 8 rows still red under test-drain (comment posted; owner reopen).
- #1708 reified class patterns (ruling 2026-08-30) — follow-up round.
- Harness worktrees `.claude/worktrees/agent-*` (one locked) were not created by this session and were left in place.

### Files Changed
`git diff --stat 6e2b68812..14d4f4d43` — see `range-diffstat.txt` in the session scratchpad: 248 files changed, 13,539 insertions(+), 2,923 deletions(-).
