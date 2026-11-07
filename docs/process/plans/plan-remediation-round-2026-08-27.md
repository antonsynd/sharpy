<!-- Verified by /verify-plan on 2026-08-27 @ e02f39fbb -->
<!-- Verification result: PASS WITH CORRECTIONS (re-stamped 2026-08-27 after owner rulings R1' and R12' — see Verification Summary) -->
<!-- Adequacy: CLASS -->

# Remediation round plan — 2026-08-27

Scope: **27 open issues** — the 24 open issues in the #1633+ range (#1020/#1021 parked by owner; #1099/#1101/#1102/#1103/#1592/#1611/#1614/#1619 stay parked per the 2026-08-25 round) plus three filed during batching: **#1663** (or-pattern `as` → CS8780/CS0165), **#1664** (except-handler DA false positive), **#1665** (index double-evaluation, #1226 sibling). Every issue was read in full — bodies and comments — and eight explorer censuses plus eleven `run` probes were taken at **`e02f39fbb`** (all "measured" below means `sharpyc run` at that sha unless stated).

Batches retire a **defect class**, not the member cells; each section names the contract, the cell matrix, the standing harness (or the phase that adds one), and the mutation that turns every new guard red. Read `docs/design/verification-contract.md` first; §9's shared-tree block goes verbatim into every agent prompt.

Six issue bodies were corrected during batching (comments posted 2026-08-27; the comment is authoritative):

| Issue | Correction |
|---|---|
| #1653 | CPython accepts `case int(n):` — PEP 634 self-matching builtins bind `n` to the whole subject (measured, python3 3.12) |
| #1635 | `GeneratorExpression` does not exist; the class is 25 non-leaf expression misses + 5 missing writes + 3 structurally unreachable read sources |
| #1656 | Half stale (6eaa7a288 visits else bodies); residue is "Sharpy's DA is more precise than C#'s" — `for-else` cell → CS0165 behind SPY0908 (measured) |
| #1657 | Its own repro already fires SPY0600 (measured); residue is reads *inside* a defer body (measured: CS0165 behind SPY0908) |
| #1633 | FBIT asserts `Success` before stdout; bundles come from the **Analyze** path; vector is `CompilerApi._analysisCache` (instance), not a static; sequential reuse suffices |
| #1640 | Flat `Stack[int]().peek()` is Unknown too (measured) — class is "any instance member on an imported CLR generic receiver" |

---

## 1. Owner rulings — ALL 12 RESOLVED 2026-08-27

Posted as comments on the issues (authoritative; do not re-ask):

| # | Issue | Ruling |
|---|---|---|
| R1 | #1651 | **Refuse**: type-based totality recorded as a node-keyed fact; non-trailing total class pattern → SPY0700; spec Arm Ordering rewritten. **R1′ (owner, 2026-08-27, in session): narrowed** — a total class pattern is SPY0700 only when a *refutable* arm follows it; trailing irrefutable arms (`_`, bare capture, `as` over irrefutable) after it stay legal (they lower to `default:`, never CS8120). No `worked → refused` row. |
| R2 | #1653 | **Adopt PEP 634 self-matching list** verbatim; other builtins → SPY0363/0364; arity check no longer gated on "has a symbol" |
| R3(i) | #1637 | **Every primitive width callable** (a1): Core conversion per width, `OverflowError` on range; callability keyed on **CLR identity** |
| R3(ii) | #1636 | **`lib.Handle` ≡ `Handle`** in value position; spec sentence; sweep gains value-position + canonical-spelling axes |
| R4 | #1650 | **Honest border**: kwargs on a `FunctionType` value → SPY0277-shaped refusal; no `ParameterNames` |
| R5 | #1640 | **Widen the bridge fallback** to honest `GenericType`; typed member seam; fidelity sweep axis + `DeliberatelyPermissive` ratchet; own batch, gate before/after |
| R6 | #1646 | **Non-nullable `CodeGenContext.Ir`**; both construction sites build IR |
| R7 | #1634 | **Remove SPY0487**; guard "every Active code has an emission site" |
| R8 | #1656 | **Keep Sharpy's precision**: loop-else definitely executed; definite initializer from a materialized fact; **after Batch A** |
| R9 | #1633 | **Clone the registry per compilation** (a); binding-keyed `CodeGenInfo` (b) may follow later |
| R10 | #1661 | **numpy semantics**: integer index squeezes; `SliceSpec.At(i)`; fixtures/spy-test regen as a ruled change |
| R11 | #1662 | **One integer predicate, no carve-out**; add `FloorDiv/FloorMod(ulong, ulong)` |
| R12 | #1649 | **Controls first**: `/verify-implementation` pair vs plan-c6ae1b; `/implement-plan` dry run = Batch H; `/compiler-audit` census vs this plan. **R12′ (owner, 2026-08-27, in session): write the control pair as files first** — `.claude/plans/controls/control-cell.md` and `control-class.md`, each a plan built on facts read at a named sha with its known verdict recorded in its header — then run Wave 0 against them and against `~/.claude/plans/plan-c6ae1b.md`. |

## 2. Recurring meta-classes and their standard cures

| Meta-class | Instances this round | Standard cure |
|---|---|---|
| **SPY0908 as a net** | #1651 #1663 #1650 #1638 #1640 #1656 #1657 #1637 #1660 [CORRECTED: #1636 is a wrong *refusal* (SPY0220, measured) and #1652 a parse refusal (SPY0104, measured) — neither is an ICE; both stay under their other rows] | Semantic-time refusal or a materialized lowering that names its check |
| **Silent wrong behavior** | #1662 (wrong remainder), #1655 (dropped stores), #1654 (lost indexer), #1639 (stale element type), #1645 (wrong indexer type), #1661/#1665 (ndim, double eval), #1664 (false SPY0600), #1646 (silent degradation) | Fix first, then a conformance sweep over the sibling cells |
| **Qualified/bare & alias divergence** | #1636 #1637 | ONE CLR-identity-keyed resolver; sweep allowlist drains |
| **Position/route missing a shared check** | #1635 (DA switches), #1653 (arity gate), #1655 (six store dispatches), #1660 (five globs), #1641 (no format gate), #1634 (no emission-site guard) | Move the check to the shared seam; matrix so a new position cannot skip it |
| **A compilation mutates state it does not own** (new this round) | #1633 | Per-compilation ownership by construction; instance-cache audit |
| **Instruments that pass vacuously** | `PatternMatchPropertyTests` (`passed > total/3`), `ArityUniformOverloadSets_…` (states a falsified claim), `InteropConformanceTests` (blind to permissive Unknown) | Fail loudly; mutation-test the guard |

## 3. Batch structure and sequencing

| Wave | Batches | Why this order |
|---|---|---|
| 0 | **K** (#1649 controls only) | Verify the instrument before it grades anything |
| 1 | **H** (#1633 #1660) | Both poison measurement: shared-registry ICE bundles + crash-bundle re-ingestion |
| 2 | **A** (#1635 #1664 #1657 #1656) | R8's definite initializer may only hide what a *total* collector proved |
| 3 | **B, C, D, E, G, I** — independent, parallelizable under §9 | |
| 4 | **F** (#1640 #1645) | Largest blast radius; whole gate + 3 sweeps before/after |
| 5 | **J** (#1661 #1665), **K** remainder (#1634 #1648) | J's `At(i)` helper serves both issues |

Regen (verification-contract §7): the first Semantic/CodeGen-touching commit of each wave is followed by a regen commit (spy-stdlib C#, spy-test C#, stdlib docs) or "regen: no diff @ sha". Batch J's regen is a *ruled* diff (ndim lines flip).

Commit gate for every batch: `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark"` (whole solution) @ the closing sha, plus the three GapDiscovery sweeps for Batches A–F.

---

## Batch H — Infrastructure: #1633, #1660

### Context
Every Analyze-path caller (LSP parity, diagnostic sweeps, metamorphic corpus) writes ICE bundles because one `BuiltinRegistry` is shared across compilations (#1633); every multi-file compile then re-ingests those bundles' `sources/*.spy` as program sources (#1660). Fixing either alone leaves the other feeding it.

### Current State (@ e02f39fbb)
- `CompilerApi._analysisCache` (`CompilerApi.cs:40`) → `GetOrBuildAnalysisContext` (`:601`) memoizes a `BuiltinRegistry` → `ProjectCompiler._sharedBuiltinRegistry` (`ProjectCompiler.cs:165`, `Initialization.cs:14`) → `SymbolTable.PopulateBuiltins` (`SymbolTable.cs:68`, define at `:87`) puts the registry's **own** `FunctionSymbol` instances in every global scope. `MaterializeCodeGenInfo` writes `symbol.CodeGenInfo` (`SemanticBinding.cs:481`); `DualWriteAssertions.AssertCodeGenInfoConsistency` (`:127`, throw `:142-145`) [CORRECTED: line offsets] fires in the next compilation. 22,718 `report.md` bundles on disk; 3,937/4,000 sampled are `Symbol 'len'` with the Analyze stack; one bundle's source has no `len` and no imports (cross-compilation proven). `BuiltinRegistry.IsBuiltinSymbol` (`:937`) is reference-identity by design (#1241).
- Glob: `IntegrationTestBase.cs:553` `GetFiles(projectDir, "*.spy", AllDirectories)` → `ProjectConfig.SourceFiles` (`:581`); siblings `DiagnosticSweepTests.cs:187`, `ReparseEquivalenceConformanceTests.cs:645`, `MetamorphicCorpusSweepTests.cs:482`; **production** `ProjectConfig.ResolveGlobPattern` (`ProjectConfig.cs:353,366,470-493`) and `Formatting/FormatRunner.cs:81`. [CORRECTED: the census is **19** `"*.spy"` + `AllDirectories` globs @ e02f39fbb, not six. The 13 unlisted: `EntryFilePathMultiFileTests.cs:65`, `FormatterIntegrationTests.cs:47,:126` (Compiler.Tests) and `:29,:84` (Stdlib.Tests), `MultiFileDeterminismTests.cs:106`, `CompletionFuzzTests.cs:38`, `HoverFuzzTests.cs:38`, `SemanticTokenCoverageTests.cs:42`, `FrontEndParityTests.cs:348`, `NuGetModuleCoverageTests.cs:53`, `FileBasedIntegrationTestsBase.cs:45`, `FixtureDiscoveryHelper.cs:110,:121` — the last three already route through `IsNonCorpus`; H2 Task 2 must classify every one of the 13 as filtered/unfiltered and apply the predicate to the unfiltered ones.] Crash root = `<OutputDirectory>/.sharpy-crash/<stamp>/` (`CrashBundleWriter.cs:79,130-138`; `ResolveCrashOutputDirectory` `ProjectCompiler.cs:458-480`), inside the project tree by default. `FixtureDiscoveryHelper.NonCorpusSegments` (`:73`) / `IsNonCorpus` (`:81-84`) are `private static` in TestInfrastructure (which references the compiler, not vice versa). **30,498** stray `.spy` under fixture `bin/` today (measured `find src -path '*/bin/*' -name '*.spy'` @ e02f39fbb) [CORRECTED: was ~10k]; fixture `bin/` is never cleaned.

### Defect Class
- **Violated contract (H1):** a compilation mutates only state it owns; anything shared across compilations is immutable after discovery.
- **Violated contract (H2):** a source glob never selects files under an output or crash-bundle directory — in the harness, the compiler, or the formatter.
- **Meta-class:** H1 new ("a compilation mutates state it does not own"); H2 position/route missing a shared check → move the predicate to one seam.
- **Known member cells:** #1633, #1660.
- **Sibling cells:** H1 — every mutable `Symbol` field (`Symbol.cs:23,133,151,162,187,215,237,248,260,271-319,368-380,430,440,441` + `Methods/Fields/Interfaces/Constructors/MethodOverloads`) × every shared owner (`BuiltinRegistry._types/_functions/_interfaceSymbols/_clrTypeCache`, `CachedModuleDiscovery._moduleTypeSymbols/_skeletonTypeParams/_loadedIndices`, `ClrTypeBridge._typeCache/_interfaceSymbolCache`) × entry point (Analyze / AnalyzeDocument / AnalyzeProject / Compile). H2 — **19** globs [CORRECTED: was six; list in Current State] × {harness fixture, production `.spyproj`, formatter, LSP fuzz/parity harness} × planted path {`bin/`, `obj/`, `.sharpy-crash/`}.
- **Standing harness that should have caught it:** `StaticStateConformanceTests` (static fields only; exempts `ConcurrentDictionary`; frames the risk as determinism) — Phase H1.3 extends it. `FixtureCorpusBoundaryTests` (discovery only) — Phase H2.2 adds the compile-side twin.
- **Owner rulings:** #1633 R9 comment; #1660 widening comment.

### Adversarial Review
- **Alternative root cause:** concurrency. Discriminator: two *sequential* analyses through one `CompilerApi` with `builtin_shadow_function_called.spy` then any file → the second throws. No threads involved.
- **How the fix could be inert:** cloning symbols but leaving `_clrTypeCache` (holds `TypeSymbol`) shared; or cloning in `Initialization.cs:14` but `CodeGenInfoComputer` resolving through a path that still reaches the master (`ProcessFromImport` `:228-247`). Probe: after the fix, assert every master instance's `CodeGenInfo` is still null after two analyses.
- **Before/after by direction:** no program is newly refused. Positive control: the same two analyses through `Compile` never threw before or after.
- **Blast radius:** LSP (`Sharpy.Lsp.Tests` runs at core count, singleton `CompilerApi` at `Program.cs:91`), all Analyze-path sweeps; every multi-file fixture for H2.
- **Generated artifacts:** none.

### Design Decisions
- Clone the **registry**, not the symbols in isolation, so `IsBuiltinSymbol` keeps answering by identity (#1241). Discovery DTOs (`OverloadIndex` graph) are data and stay shared.
- The crash-bundle predicate lives in `Sharpy.Compiler` (internal; `InternalsVisibleTo("Sharpy.TestInfrastructure")` exists) so the compiler, the formatter and the harness share one spelling.

### Phase H1: per-compilation registry ownership (#1633)
**Goal:** no compilation can observe another's writes to a builtin symbol.
**Acceptance:**
- Sequential-reuse test: one `CompilerApi`, `AnalyzeDocument` on a `len`-shadowing source then on any source → no throw, no bundle. Positive control: pre-fix this pair throws (record the throw in the commit body).
- Master-untouched assertion: after N analyses, every symbol in the master registry has `CodeGenInfo == null`.
- `StaticStateConformanceTests` extended: any field reachable from `AnalysisCacheEntry` that holds a `Symbol` must be per-compilation or immutable; the audit enumerates by reflection. Mutation: mark `_clrTypeCache` shared again → red.
- Bundle census: `find src -path '*/.sharpy-crash/*' -name report.md -newer <fix commit>` is empty after a whole-solution run.
#### Tasks
1. **`BuiltinRegistry.CloneForCompilation()`** — `Semantic/Registry/BuiltinRegistry.cs`: deep-copies `_types`, `_functions`, `_interfaceSymbols`, `_clrTypeCache` symbol instances (record `with`, then re-link `TypeSymbol.Methods/BaseType/Interfaces` to the cloned instances via a symbol map); `IsBuiltinSymbol` answers from the clone's map. Commit: `fix(semantic): builtin registry is cloned per compilation; shared master is never defined into a scope (#1633)`
2. **Wire the clone** — `Project/ProjectCompiler.Initialization.cs:14` clones the shared registry; `ProjectCompiler.Phases.cs:38` receives the clone; `CompilerApi.cs:592-599` comment rewritten to cover `MaterializeCodeGenInfo`. Commit: `fix(api): analysis cache hands out a per-compilation registry clone (#1633)`
3. **Guards** — `Sharpy.Compiler.Tests/Project/SharedRegistryOwnershipTests.cs` (sequential-reuse + master-untouched); extend `Diagnostics/StaticStateConformanceTests.cs` to instance caches under `AnalysisCacheEntry`; measure clone cost on `AnalyzeDocument` (report ms @ sha; fallback to no-sharing only if measurable). Commit: `test(project): shared-registry ownership guard and instance-cache audit (#1633)`
4. **Cache audit** — `ClrTypeHelper.cs:67,97,150`, `ClrExtensionMethodResolver.cs:73`: return copies or `ImmutableArray`; `OverloadIndexCache.s_inMemoryIndices` DTO setters → init. Commit: `fix(discovery): shared caches hand out immutable views (#1633)`

### Phase H2: one crash-bundle predicate for every source glob (#1660)
**Acceptance:**
- Compile-side twin of `FixtureCorpusBoundaryTests`: a temp project dir with real `main.spy` + planted `bin/Debug/net10.0/.sharpy-crash/x/sources/main.spy`, `obj/x.spy` → compiles exactly the real files, via (a) `IntegrationTestBase` multi-file, (b) a real `.spyproj` with `**/*.spy`, (c) `FormatRunner`. Mutation: remove the predicate from `ResolveGlobPattern` → cell (b) red (CS0017).
- All 19 globs [CORRECTED: was six] call the one predicate (grep count of `AllDirectories` `*.spy` globs without it = 0; the test asserts this by reflection over the call sites is not possible — use a `Sharpy.Compiler.Tests/Infrastructure/SourceGlobConformanceTests` that scans the source text for the pattern and requires the helper name on the same statement).
- The ~10k stray copies are removed and the FBIT harness cleans a fixture's `bin/` before compile.
#### Tasks
1. **Predicate** — `Diagnostics/CrashBundleWriter.cs`: `internal static bool IsNonSourceSegment(string relativePath)` next to `CrashRootDirectoryName`; `FixtureDiscoveryHelper` delegates to it; `DiagnosticExplanations.Infrastructure.cs:78` references the constant. Fix the dead ternary `:132-136`. Commit: `refactor(diagnostics): one crash-bundle/output-directory predicate shared by discovery and globs (#1660)`
2. **Apply** — `ProjectConfig.ResolveGlobPattern`, `FormatRunner.cs:81`, `IntegrationTestBase.cs:553`, `DiagnosticSweepTests.cs:187`, `ReparseEquivalenceConformanceTests.cs:645`, `MetamorphicCorpusSweepTests.cs:482`, **plus the 13 sites named in Current State** (LSP fuzz/coverage/parity harnesses, formatter tests, determinism, NuGet coverage) [ADDED: missing step — the source-scan guard in Task 4 would otherwise go red on day one]. Commit: `fix(project,format,test-infra): source globs never select output or crash-bundle files (#1660)`
3. **Root the bundle outside the project tree when compiling a test fixture** (`ResolveCrashOutputDirectory` honours an explicit `CrashRoot` in `ProjectConfig`; harness sets it to the temp dir). Commit: `fix(test-infra): fixture crash bundles land in the temp root, not next to the fixture (#1660)`
4. **Guards + cleanup** — `SourceGlobConformanceTests`, compile-side boundary twin, harness cleans `bin/` pre-compile; delete stray trees with a one-off script under `build_tools/` (not `rm` in an agent prompt). Commit: `test(test-infra): compile-side corpus boundary guard; fixture bin/ cleaned before compile (#1660)`

### Testing Strategy
Positive controls: a fixture whose `bin/` contains a *legitimate* nested `.spy`? None exist — the harness never puts sources under `bin/`; document that in the test. Blast radius: every multi-file FBIT row, `Sharpy.Cli.Tests` project builds, `Sharpy.Lsp.Tests`.

### Issues to Close
- #1633 — closed by H1 Tasks 1–4; **criterion:** sequential-reuse test green, pre-fix throw recorded, master-untouched green, zero new bundles after a whole-solution run @ sha.
- #1660 — closed by H2 Tasks 1–4; **criterion:** compile-side twin green in all three cells with the `ResolveGlobPattern` mutation recorded red; whole-solution run with no CS0017 @ sha.

---

## Batch A — Definite assignment models the emitted flow: #1635, #1664, #1657, #1656

### Current State (@ e02f39fbb, measured)
- `except_fp`: `x: int; x = 1; try: pass; except Exception: print(x)` → spurious SPY0600 (#1664); control without `try` clean. [CORRECTED: the declaration must be **bare** — with an initialized `x = 1` DA does not track `x` and the program compiles clean (measured @ e02f39fbb, both shapes).]
- `defer_body` (#1657's own repro) → SPY0600 (correct). `defer_sibling` (matrix shape) → CS0165 behind SPY0908.
- `forelse` (matrix shape) → CS0165 behind SPY0908; no SPY0600 because Sharpy's CFG proves `x`.
- Structure: `Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs` (235 lines; name-keyed `bareDecls` `:24`; fixpoint `:72-107` zeroes in-set on `ExceptionPredecessors` `:83-85`; report pass `:109-154` reads `Predecessors` only `:116-125`; `CollectReads` `:173` 5 arms, `CollectReadsFromExpr` `:197` 8 arms, `CollectAssignedNames` `:159` 2 arms — all no-default). `ControlFlowGraphBuilder.BuildStatement` `:166` — no `DeferStatement` arm, quiet `default:` `:239-242` (adds the statement as straight-line code, no throw) [CORRECTED: lines]; `BasicBlock.MatchSubject` `:75` / `MatchExpressionSubjects` `:85` never read by DA; with-context expressions and `MatchCase.Guard` stored nowhere. `Node.GetChildNodes()` `Parser/Ast/Node.cs:92` exists; CFG builder uses it correctly at `:276/:288` (overrides defined at `:268/:283`) [CORRECTED: lines]; **no reflection completeness test for it** (siblings: `AstVisitorTests.cs:196/:219`, `UnparserExhaustivenessTests.cs:69`). `BlockKinds.cs:86-91` KnownRed: for-else, while-else, defer × UseBeforeAssign; comment `:79-85` misdiagnoses.

### Defect Class
- **Violated contract (A1):** every read and write of a local reachable from a function body is seen by DA — the collector is total over the AST.
- **Violated contract (A2):** the CFG models the **emitted** control flow (defer runs at scope exit; a break-free loop's else always runs; a handler is entered from the try's state).
- **Meta-class:** position/route missing a shared check (hand-enumerated switches with silent defaults) → generic child enumeration + one block expression list; **silent wrong behavior** (#1664).
- **Known member cells:** #1635, #1664, #1657, #1656.
- **Sibling cells:** read-position × node-kind (41 expression kinds + `Raise`/`Yield`/`Defer`/`Assignment.Target`/with-context/match-scrutinee/match-guard) — 25 non-leaf expression misses named in the #1635 comment; write-position × binder (for-target, with-as, except-as, walrus, initialized decl — false-positive direction); block-kind × {read-in, write-in, read-after} for `try/except/else/finally`, `defer`, `for-else`, `while-else`, `match`, `with`. Ten other silent hand-enumerated switches (meta-class): `UnusedVariableValidator.cs:122` (no Defer/Yield), `PropertyValidator.cs:797` (no Match; #1461 precedent), `TypeChecker.Statements.cs:1792 CheckDeferBodyControlFlow` (no Match → raw CS0157), `ExecutionOrderAnalyzer.cs:217`, `DefaultParameterValidator.cs:118` (false negatives), `Shared/StatementWalker.cs:47-118`, `TypeChecker.Utilities.cs:109` vs `NarrowingFlowAnalysis.cs:122,158`, `AugmentedCollectionAssignment.cs:144-150`, `CodeGenInfoComputer.cs` ×6 / `NameResolver.Declarations.cs` / `ModuleLoader.cs`, `ExhaustivenessHelper.cs:66`.
- **Standing harness:** `BlockScopeRedeclarationMatrixTests` + `BlockKinds` ratchet (position axis only, no read-kind axis) — Phase A1 adds the DA position × node-kind matrix; `DefiniteAssignmentTests.cs` (cells).
- **Owner rulings:** R8 on #1656.

### Adversarial Review
- **Alternative root cause for #1664:** the handler block has no predecessors because `ConnectException` is wrong, not the report pass. Discriminator: the fixpoint (which handles it) and the report pass disagree on the same CFG — one function for both makes the question moot.
- **Inert risk:** replacing the switch with `GetChildNodes()` but keeping a stop-list that omits `LambdaExpression` → lambda bodies counted as reads (false positives); or a `GetChildNodes()` override that is itself incomplete (that is why the completeness test is the keystone).
- **Before/after by direction:** every new SPY0600 is listed with the prior commit's behaviour; the 25 expression kinds each get a "read inside, unassigned" cell (prior: CS0165 behind SPY0908 or silent `default`) and a "read inside, assigned" cell (prior and after: runs).
- **Blast radius:** every fixture with a bare declaration; `Sharpy.Stdlib.Tests` — **43 bare declarations in `src/Sharpy.Stdlib/spy/*.spy` (measured)**, so A3's `= default` initializer changes the generated stdlib C# and the regen is mandatory, not conditional [CORRECTED: question answered]; LSP diagnostics parity.
- **Generated artifacts:** spy-stdlib C# / spy-test C# if any spy source gains a definite initializer (R8) — regen after A3.

### Design Decisions
- Generic walk via `GetChildNodes()`, stop at `LambdaExpression`/`FunctionDef`/`ClassDef`; assignment target: walk sub-expressions but not the bound identifier; augmented target walked fully.
- One `BasicBlock.Expressions` ordered list replaces `Terminator.Condition` reads, `MatchSubject`, `MatchExpressionSubjects`; builder fills it for with-context, match scrutinee, match guard.
- Defer body = scope-exit block reachable only from the exit path.
- R8: `SemanticInfo.DefinitelyAssignedBareLocals` (node-keyed on `VariableDeclaration`; **joins `MergeFrom`**) → emitter emits `= default` for those declarations only.
- Quiet `default:` in `ControlFlowGraphBuilder` becomes `throw new InvalidOperationException` guarded by a totality test over statement kinds (mirrors `IrTotalityTests.cs:68` `EveryExpressionAndStatement_LowersToExactlyOneIrNode`; `:212` is its doc comment) [CORRECTED: line].

### Phase A1: total collectors + block expression list (#1635, #1664)
**Acceptance:**
- `GetChildNodesCompletenessTests`: every concrete `Node` type's `GetChildNodes()` yields every `Node`-typed property/collection (reflection). Mutation: drop one child from one override → red. [ADDED: sizing — 116 concrete `Node` types, 75 overrides in 6 files, and `Node.cs:92`'s default returns `Empty`; the guard must treat a leaf type with no `Node`-typed members as complete-by-default and flag every non-leaf type with no override, so Task 1's fix wave is potentially large — budget it before Task 2 depends on it.]
- DA matrix `DefiniteAssignmentMatrixTests`: (node kind ∈ 41 expressions + 6 statements) × (unassigned read → SPY0600; assigned read → runs) × (write kinds → no SPY0600 after). Mutation: reintroduce the switch with one arm removed → that row red.
- `BlockExpressionTotalityTests`: every `Expression` reachable from a function body appears in exactly one block's list (with-context, match scrutinee/guard included). Mutation: skip guards → red.
- #1664 cell: assigned-before-try / read-in-handler → clean; assigned-inside-try / read-in-handler → SPY0600; positive control recorded.
#### Tasks
1. **Completeness guard first** — `Sharpy.Compiler.Tests/Parser/Ast/GetChildNodesCompletenessTests.cs`; fix any override it finds incomplete. Commit: `test(parser): GetChildNodes reflection completeness guard (#1635)`
2. **Collectors** — `DefiniteAssignmentAnalysis.cs`: `CollectReads*`/`CollectAssignedNames` → generic walk; add the five write kinds. Commit: `fix(analysis): definite assignment collects reads and writes by generic child enumeration (#1635)`
3. **Block expression list** — `BasicBlock.cs` + `ControlFlowGraphBuilder.cs` (with `:752`, match `:858`, guard); DA reads the list; delete the three slots. Commit: `fix(analysis): one ordered expression list per basic block; with-context and match guards reach DA (#1635)`
4. **Report pass shares the fixpoint's in-set function** — `:109-154`. Commit: `fix(analysis): DA report pass applies the exception-predecessor rule; no spurious SPY0600 in handlers (#1664)`
5. **Matrix + totality tests**; quiet-default → throw + statement-kind totality test. Commit: `test(analysis): DA position × node-kind matrix, block expression totality, CFG statement totality (#1635, #1664)`
6. **Meta-class sweep** of the ten switches: each gets either generic enumeration or a `default:` that reports/throws, with a totality test where an arm list is load-bearing (`UnusedVariableValidator`, `PropertyValidator.GetChildStatements`, `CheckDeferBodyControlFlow`, `DefaultParameterValidator`, `StatementWalker`). Commit per file: `fix(semantic): <walker> enumerates statement kinds totally (#1635 meta-class)`

### Phase A2: defer is a scope-exit block (#1657)
**Acceptance:** `defer` KnownRed cell drains (SPY0600 for the sibling read); read-inside-defer of a local assigned later in the enclosing scope → clean (LIFO order per `experimental/defer_gated.expected`); mutation: remove the arm → cell red again (ratchet fires).
#### Tasks
1. `ControlFlowGraphBuilder.BuildDefer` — body blocks hung off the exit path. Commit: `fix(analysis): defer bodies flow at scope exit in the CFG (#1657)`
2. Drain `BlockKinds.KnownRed["defer"]`; correct `:79-85`. Commit: `test(integration): defer UseBeforeAssign cell drains (#1657)`

### Phase A3: definite initializer from a materialized fact (#1656, R8)
**Acceptance:** `for-else`/`while-else` join `DefinitelyRunBeforeSibling`; cells expect output `1` and pass; `SemanticInfo.DefinitelyAssignedBareLocals` in `MergeFrom` (MergeFrom guard test); emitter reads only the fact; mutation: drop the dictionary from `MergeFrom` → multi-file twin of the cell red. Direction: no newly refused program; list the programs newly *accepted*.
#### Tasks
1. Record the fact in `DefiniteAssignmentValidator`/`SemanticInfo`; `MergeFrom` + a `MergeFrom_Carries…` executing twin in `SemanticInfoMergeConformanceTests` (its `EverySemanticInfoSideTable_IsReferencedInMergeFrom` source-scan is the existing guard) [CORRECTED: `SemanticInfoMergeFromTests` does not exist]. Commit: `feat(semantic): record bare locals proved definitely assigned as a node-keyed fact (#1656)`
2. Emitter: `RoslynEmitter.Statements.Assignments.cs` bare `VariableDeclaration` → `= default` when tagged. Commit: `fix(codegen): bare locals proved assigned by Sharpy DA get a definite initializer (#1656)`
3. Matrix update + KnownRed drain + multi-file twin. Commit: `test(integration): loop-else UseBeforeAssign cells flip to definitely-run and drain (#1656)`

### Testing Strategy
Fixtures: `definite_assignment/` — one per (node kind × read/write) is too many as files; use the programmatic matrix and keep ~8 representative FBIT fixtures incl. `except_handler_reads_pre_try_ok_1664.spy`, `defer_sibling_read_spy0600_1657.spy`, `for_else_assigns_then_read_ok_1656.spy` (+ `.expected.cs` for the last: shows `= default`). Spec: `docs/language_specification/variable_declaration.md` §"Bare Declarations" (already exists at `:66-90`, SPY0600 example included) is **extended** with the loop-else, defer and except-handler rules, examples executed [CORRECTED: the section exists — extend, don't add a second].

### Issues to Close
- #1635 — A1 Tasks 1–6; **criterion:** completeness guard + matrix green with recorded mutations @ sha.
- #1664 — A1 Task 4; **criterion:** handler cells green, positive control recorded.
- #1657 — A2; **criterion:** KnownRed entry deleted, cell green, mutation recorded.
- #1656 — A3; **criterion:** cells flipped and green; `MergeFrom` mutation recorded; whole-solution gate @ sha.

---

## Batch B — Match patterns: #1652, #1663, #1651, #1653

### Current State (@ e02f39fbb)
`as` accepted only after `T()` (`Parser.Statements.cs:1708-1726`, `:1752-1773`); modelled solely as `TypePattern.BindingName` (`Pattern.cs:97`). `IsIrrefutable` (`ExhaustivenessHelper.cs:139-151`) syntactic. `CheckTypePattern` (`TypeChecker.Statements.Patterns.cs:688-690`) computes both assignability directions, emits SPY0361 only when neither. `CheckPositionalPattern` (`:888`) arity checks gated on `typeSymbol != null` (builtins skip `:973-979`). SPY0359 (`:295-307`) sees only `BindingPattern`. `MatchArmOrderValidator` (Order 406, SPY0700) discards the scrutinee (`:21/:28`). Emitter `RoslynEmitter.Patterns.cs` TypePattern arm `:378-386`, positional `:525-599`. Spec `match_statement.md` table `:152-166`, Arm Ordering `:387-423`. Measured: #1651 shapes → CS8120; `A() | B() as v` → CS8780 + CS0165; `case int(n):` unvalidated.

### Defect Class
- **Violated contract:** pattern semantics (what binds, what is total, what arity is legal) are decided once in semantic analysis from the pattern kind × scrutinee static type, and the emitter reads the decision.
- **Meta-class:** SPY0908 as a net (#1651, #1663, #1652), position/route missing a shared check (#1653 arity gate), instruments that pass vacuously (`PatternMatchPropertyTests.cs` `passed > total/3`).
- **Known member cells:** #1651 #1652 #1663 #1653.
- **Sibling cells:** pattern kind {wildcard, capture, literal, value, class-empty, class-positional, class-keyword, or, as-on-each, sequence, mapping(absent), union-case} × scrutinee {int, str, object, user class, Optional, Result, union} × arm position {trailing, non-trailing} × statement vs match-expression. Unfiled but covered: positional/keyword class patterns with `as`; `case (A() as v) | B():` → SPY0359; mapping patterns remain unsupported (document as SPY0104 with a spec row — not scheduled).
- **Standing harness:** none — Phase B3 adds `PatternConformanceMatrixTests`.
- **Owner rulings:** R1 (#1651), R2 (#1653).

### Adversarial Review
- **Alternative root cause (#1651):** the emitter could add a discard guard — rejected by R1. Discriminator irrelevant.
- **Inert risk:** recording totality but `MatchArmOrderValidator` keeping its own syntactic predicate (`ExhaustivenessHelper` is shared, so delete the local one); `AsPattern` parsed but the emitter still reading `TypePattern.BindingName` (delete the field).
- **Before/after by direction:** newly refused: non-trailing total class patterns (prior: CS8120 ICE) and `as`-on-one-alternative (prior: CS8780 ICE). Newly accepted: `case _ as y`, `case 5 as n`, `case A(x) as v`, `case int(n)` (prior: SPY0104 / ICE / unvalidated). ~~None that worked before is refused.~~ [CORRECTED: refuted by `run` @ e02f39fbb — `case int() as n:` **and** `case int():` followed by `case _:` on an `int` scrutinee compile and print `int` today (the trailing wildcard lowers to `default:`, which C# never flags CS8120), — resolved by **R1′** (owner, 2026-08-27): the totality refusal fires only when a *refutable* arm follows, so these programs remain accepted and there is no `worked → refused` row. Newly refused set is therefore exactly: total class pattern followed by a refutable arm (prior: CS8120 ICE), and `as` on one alternative (prior: CS8780 ICE).]
- **Blast radius:** every `match` fixture; match expression shares the checker; LSP semantic tokens for the `as` binder; `MetamorphicCorpus` sweep.
- **Generated artifacts:** spy-stdlib/spy-test if any `.spy` uses `as` patterns — regen after B1.

### Design Decisions
- New `AsPattern(Pattern Inner, string Name)` node; `TypePattern.BindingName` deleted (one representation).
- New `SemanticInfo.PatternTotality` (node-keyed, `MergeFrom`) written by `CheckTypePattern`; consumed by `IsIrrefutable(pattern, semanticInfo)`.
- PEP 634 self-matching list lives in one static table in `Shared/` consulted by checker (arity) and emitter (lowering `case T n`).

### Phase B1: `as` is a general pattern combinator (#1652, #1663)
**Acceptance:** `<closed-pattern> as NAME` parses for every closed pattern kind; `AsPattern` over an or-pattern binds for every alternative (`describe(A())`/`describe(B())` print `bound A`/`bound B`, matching python3); SPY0359 fires for `as` on a single alternative; mutation: drop the `AsPattern` arm from the uniformity check → that cell red; parser round-trip (unparser) covers `AsPattern`.
#### Tasks
1. Parser: `ParseAsPattern` wrapping `ParseClosedPattern`; delete the two special sites. Commit: `feat(parser): as-pattern is a general combinator over any closed pattern (#1652)`
2. Semantic: `CheckAsPattern` (binds Name to inner's matched type; irrefutability inherits inner's); SPY0359 over every binder kind; `CheckTypePattern` no longer reads `BindingName`. Commit: `fix(semantic): as-pattern binding checked once for every inner pattern; or-alternative uniformity covers as (#1652, #1663)`
3. Emitter: `GenerateMatchPattern` `AsPattern` arm → C# `inner-pattern var name` / declaration pattern for type inner. Commit: `fix(codegen): as-pattern lowers uniformly over its inner pattern (#1652, #1663)`
4. Unparser/structural comparer arms + LSP handlers naming the binder. Commit: `feat(lsp,parser): AsPattern round-trips and highlights its binder (#1652)`

### Phase B2: type-based totality is a recorded fact (#1651)
**Acceptance:** total class pattern followed by a **refutable** arm → SPY0700 (with and without `as`, statement and expression); total class pattern followed only by irrefutable arms (`case _:`, `case x:`) → runs and prints the total arm (positive control: `case int() as n: … case _:` prints `int` today and after, measured @ e02f39fbb) [R1′]; trailing → runs; `PatternTotality` in `MergeFrom` (guard); mutation: make `IsIrrefutable` ignore the fact → the `case int() as n:` cell regresses to CS8120 (red). Spec Arm Ordering rewritten and its examples executed.
#### Tasks
1. `SemanticInfo.PatternTotality` + `MergeFrom` + write in `CheckTypePattern`. Commit: `feat(semantic): record class-pattern totality for the scrutinee's static type (#1651)`
2. `ExhaustivenessHelper.IsIrrefutable(pattern, info)`; `MatchArmOrderValidator` passes the scrutinee and info. Commit: `fix(semantic): non-trailing total class pattern is SPY0700, never CS8120 (#1651)`
3. Spec `match_statement.md` Arm Ordering. Commit: `docs(spec): class-pattern totality and arm ordering (#1651)`

### Phase B3: builtin positional patterns (#1653) + conformance matrix
**Acceptance:** `case int(n):` binds the subject for the eleven self-matching builtins (`run` prints match python3 for int/str/float/bool/list/tuple/dict/set); `case bytes(a, b):`/`case range(x):` → SPY0363/0364; matrix `PatternConformanceMatrixTests` over the axes above with every cell asserting output or a diagnostic code (no proportion); mutation: remove the self-matching table entry for `str` → its cell red. `PatternMatchPropertyTests` threshold replaced by the matrix or made exact — **all six** `passed > total / 3` sites (`:46,:75,:104,:133,:162,:191`), not one [CORRECTED: count].
#### Tasks
1. `Shared/SelfMatchingBuiltins.cs`; `CheckPositionalPattern` validates arity for builtins. Commit: `fix(semantic): builtin class patterns validate arity; PEP 634 self-matching builtins bind the subject (#1653)`
2. Emitter lowering `case T n`. Commit: `fix(codegen): self-matching builtin pattern lowers to a declaration pattern (#1653)`
3. Matrix + spec table row. Commit: `test(semantic,codegen): pattern conformance matrix; spec table row for self-matching builtins (#1651, #1652, #1653, #1663)`

### Testing Strategy
Fixtures under `pattern_matching/`: `as_on_or_pattern_binds_each_1663`, `as_on_literal_ok_1652`, `as_on_positional_class_ok_1652`, `as_on_one_alternative_spy0359_1663.error`, `total_class_pattern_non_trailing_spy0700_1651.error` (+ trailing positive control), `self_matching_int_n_1653` (+ `bytes_two_positionals_spy0364_1653.error`). Every `.expected` compared to python3.

### Issues to Close
- #1652 — B1; **criterion:** every closed-pattern × `as` cell green; round-trip guard.
- #1663 — B1 Task 2; **criterion:** both outputs match python3; SPY0359 cell + mutation recorded.
- #1651 — B2; **criterion:** SPY0700 cells + trailing controls; `MergeFrom` guard; spec examples executed @ sha.
- #1653 — B3; **criterion:** eleven self-matching cells match python3; arity cells; matrix mutation recorded.

---

## Batch C — Primitive callability and alias transparency: #1637, #1636

### Current State (@ e02f39fbb)
Six spelling-keyed lookups: `ResolveTypeAliasInExpressionPosition` (`TypeChecker.Expressions.cs:605`), `ResolveModuleExportedAlias` (`:630`; `MarkTypeReference` gated on UDT at `Access.cs:265-267`), `TryResolveTypeSymbolFromMemberAccess` (`Calls.cs:2248/:2308`), `ClassifyConstructorReference` (`Calls.Overloads.cs:862`, alias arm `:898-909`, qualified arms `:876-880` builtin / `:883-893` user-qualified) [CORRECTED: lines], inline callee expansion (`Calls.cs:301-317/:330`), emitter `RoslynEmitter.Expressions.Access.cs:75-105` (call at `:117`) + `CodeGenContext.IsBuiltinFunction` (`:154`) [CORRECTED: file/lines]. `SynthesizePrimitiveFunctionType` (`Expressions.cs:656`) [CORRECTED: line] keys `GetFunctionOverloads(ts.Name)`. `BuiltinRegistry.RegisteredPrimitiveNames` (`:43-46`) hardcodes ten names; `_functions` is reflected from `Sharpy.Builtins` (`LoadBuiltinFunctions` `:213-246`) [CORRECTED: line] — `Int`, `Long`, `Float`, `Bool`, `Str`, `Bytes` exist; no `Int8/Int16/UInt*/Float32`. `PrimitiveCatalog` (`:67-106`) has canonical + alias rows keyed by CLR type, no conversion-function field. Spec: `type_aliases.md:31-40` transparency; `primitive_types.md` has **no conversions section**.

### Defect Class
- **Violated contract:** which conversion a primitive type name denotes, and whether an alias is callable, is a function of the **CLR type** — never of the spelling (bare/canonical/alias/module-qualified).
- **Meta-class:** qualified/bare & alias divergence → ONE resolver.
- **Known member cells:** #1636, #1637.
- **Sibling cells:** spelling {python-facing, canonical, C# alias (`long`/`double`/`sbyte`…), user alias of each, module-qualified alias of each} × position {call, value (constructor reference), `isinstance` arg, annotation} × width {all 14 numeric + bool/str}. Today `long("42")` works, `int64("42")` ICEs (CS1729 behind SPY0908), `int8("42")` → SPY0200 undefined identifier — no function at all (all three measured @ e02f39fbb).
- **Standing harness:** `Conformance/QualifiedBareSpellingConformanceTests` (22 scenarios × 2 source sets; allowlist empty) — Phase C2 adds the canonical-spelling and value-position-alias axes.
- **Owner rulings:** R3(i) #1637 a1; R3(ii) #1636.

### Adversarial Review
- **Alternative root cause (#1636):** `IsTypeReference` gate only. Discriminator: after keying on CLR identity the gate is unnecessary; if the qualified value cell still fails, the emitter's `Access.cs:83-99` name check is the second decision point (delete it in favour of a materialized `ConstructorReferenceLowering`).
- **Inert risk:** adding Core functions without the resolver change (`int8(x)` still name-keyed → works by accident for canonical only); or the resolver without `RegisteredPrimitiveNames` being replaced by the catalog (aliases of new widths unregistered).
- **Direction:** newly accepted: `int32(x)`, `int8(x)`, `lib.Handle` as value. Newly refused: none. Overflow now throws `OverflowError` at runtime (was: no function).
- **Blast radius:** every fixture calling `int(`/`float(`; `Sharpy.Core.Tests`; stdlib docs (Core surface grows) — regen; interop sweep.
- **Generated artifacts:** stdlib docs; spy-stdlib C# if any module calls a new conversion.

### Design Decisions
- `PrimitiveInfo` gains `ConversionFunction` (e.g. `"Int8"`); `PrimitiveCatalog.GetByClrType` is the only entry; `RegisteredPrimitiveNames` deleted — the registry registers every catalog row.
- `Semantic/PrimitiveConversionResolver.cs`: `TypeSymbol → (ConversionFamily, IReadOnlyList<FunctionSymbol>)`; all six sites call it; result materialized in `ConstructorReferenceLowering` for value positions.
- Core: `Int8.cs`… one file per width, C# 9.0, Python parse rules via the shared `IntParse.cs`, range check → `OverflowError`; `float32` via `Float` then `(float)` with no range error [CORRECTED: python3 `float("1e40")` prints `1e+40` (fits a double); `float("1e400")` is `inf` — Python's `float()` never raises on overflow, so `float32("1e40")` → `inf` (C# `(float)1e40` is ∞) is consistent with Python. Document with the corrected example.]

### Phase C1: per-width conversion functions (Core)
**Acceptance:** `Sharpy.Core.Tests` table test: every width × input kind {bool, each int width, float, double, decimal, string} → expected value or `OverflowError`/`ValueError`, expected computed from python3 `int()` then a range check; mutation: drop the range check in `Int8` → `int8("200")` cell red.
#### Tasks
1. `src/Sharpy.Core/Int8.cs, Int16.cs, UInt8.cs, UInt16.cs, UInt32.cs, UInt64.cs, Float32.cs` + tests. Commit: `feat(core): per-width primitive conversion builtins with Python parse rules and OverflowError (#1637)`

### Phase C2: one CLR-identity resolver (#1637, #1636)
**Acceptance:** the sweep gains scenarios {canonical call, C#-alias call, user-alias-of-canonical call, module-qualified alias value, module-qualified alias call} and stays allowlist-empty; mutation: revert `ResolveModuleExportedAlias` to name-keyed → the value cell red; ~~`grep -c 'GetFunctionOverloads(' Semantic/` = 1 (the resolver).~~ [CORRECTED: 24 call sites in 11 files @ e02f39fbb, most of them general overload resolution unrelated to primitives — a global count is the wrong instrument. Scope it: a source-scan test asserts that none of the six spelling-keyed primitive sites named in Current State calls `GetFunctionOverloads(`/`RegisteredPrimitiveNames` directly, and that `PrimitiveConversionResolver` is the only file that does so *with a primitive name*.]
#### Tasks
1. `PrimitiveCatalog.ConversionFunction`; registry registers every row. Commit: `refactor(semantic): primitive catalog carries the conversion family; registry stops hardcoding names (#1637)`
2. `PrimitiveConversionResolver`; replace the six sites; emitter reads `ConstructorReferenceLowering` only. Commit: `fix(semantic,codegen): primitive callability and alias pinning keyed on CLR identity at one seam (#1636, #1637)`
3. Sweep axes + spec (`primitive_types.md` "Conversions"; `type_aliases.md` qualified sentence), examples executed. Commit: `test(conformance),docs(spec): canonical-spelling and qualified-value axes; conversions section (#1636, #1637)`

### Issues to Close
- #1637 — C1 + C2; **criterion:** `bint("42")` for `type bint = int32` runs; all width cells; sweep allowlist empty @ sha.
- #1636 — C2; **criterion:** `conv: (str) -> int = lib.Handle; conv("99")` prints 99; sweep cell + mutation recorded.

---

## Batch D — Callable references and keyword arguments: #1638, #1650

### Current State (@ e02f39fbb)
`RoslynEmitter.Expressions.cs:350-365` emits a raw `Sharpy.Builtins.X` method group for builtin function/type names; only fact is `ConstructorReferenceLowering`. #1589 deferral gate `CandidateAritiesDiverge` (`Calls.Overloads.cs:2037`, called at `:1977`) [CORRECTED: line] is arity-only; `len`'s six overloads (`Len.cs:24-108`) are all arity 1. Four failure shapes: boxing struct→interface (`Bytes` readonly struct, explicit `ISized.Count`), generic inference off a struct, CS0121 ambiguity (`Reversed<T>`), optional/params elided. `ParenthesizedCalleeNormalizationTests.cs:392-404` asserts a claim #1638 falsifies. #1650: `FunctionType` has no names (`SemanticType.cs:608-667`); `CheckLambdaCall` (`Calls.cs:2890`, count-only `:2915-2957`); emitter copies `name:` from AST (`Access.Calls.cs:788/:812`); SPY0277 precedent `:2930-2937` [CORRECTED: lines].

### Defect Class
- **Violated contract (D1):** a function-typed value built from a builtin or overloaded function is emitted in a form C# always accepts — the conversion is decided in semantic analysis and materialized.
- **Violated contract (D2):** every argument-binding rule (arity, kwargs, defaults) fires for every callee kind, or refuses honestly where the callee kind cannot support it.
- **Meta-class:** SPY0908 as a net (both).
- **Known member cells:** #1638, #1650.
- **Sibling cells:** D1 — builtin function {len, reversed, sorted, str, repr, abs, print…} × element type {struct-implementing-interface, class, generic} × target {`(T) -> R` local, argument to `map`/`sorted(key=)`, return value}. D2 — callee kind {def, lambda value, aliased def, `(T)->R` parameter, bound method value, constructor reference} × argument form {positional, keyword, default-omitted, `*args`}.
- **Standing harness:** `ParenthesizedCalleeNormalizationTests` (arity axis only) — Phase D1 adds the element-type × conversion-kind matrix; kwargs: the #1591 one-spelling matrix — Phase D2 adds the callee-kind axis.
- **Owner rulings:** R4 (#1650).

### Adversarial Review
- **Alternative root cause (#1638):** #1589 deferral could be extended to parameter-type divergence — but shape (A) boxing fails even with one overload; eta-expansion is the only cure covering all four. Discriminator: `Bytes`-only `len` reference still CS0123 under deferral.
- **Inert risk:** lambda emitted only for the builtin-name path while user overloaded functions (`def f(int)`/`def f(str)` as a value) keep the method group — include user overload sets in the matrix.
- **Direction:** newly refused: kwargs on function-typed values (prior: CS1746 ICE). Newly accepted: `f: (bytes) -> int = len` etc. No regressions by construction (a lambda is accepted wherever a method group was).
- **Blast radius:** every fixture passing a builtin as a value (`sorted(key=len)`, `map(str, …)`), `Sharpy.Stdlib.Tests`.
- **Generated artifacts:** spy-stdlib/spy-test C# (lambdas replace method groups in snapshots) — regen after D1 Task 2; `.expected.cs` snapshots.

### Phase D1: materialized callable-reference lowering (#1638)
**Acceptance:** matrix `CallableReferenceConformanceTests` (function × element type × target) executes and compares values with python3; `.expected.cs` shows lambdas; `ArityUniformOverloadSets_…` rewritten to assert the *lowering*, not the method group; mutation: emit the method group for `Bytes` → cell red (CS0123 behind SPY0908).
#### Tasks
1. `SemanticInfo.CallableReferenceLowering` (node-keyed; `MergeFrom`) recorded wherever a builtin/overloaded function name is used as a value, carrying the selected overload's parameter types. Commit: `feat(semantic): record callable-reference lowering for builtin and overloaded function values (#1638)`
2. Emitter reads the fact → eta-expanded lambda; delete `:350-365`. Commit: `fix(codegen): function references lower to eta-expanded lambdas from the recorded fact (#1638)`
3. Matrix + snapshot regen + test rewrite. Commit: `test(codegen): callable-reference conformance matrix; falsified arity claim replaced (#1638)`

### Phase D2: kwargs on function-typed values refuse honestly (#1650)
**Acceptance:** callee-kind × argument-form matrix; `(T)->R`/lambda/aliased-def × keyword → new SPY02xx (free @ e02f39fbb: **SPY0279**, SPY0292–SPY0399 [ADDED: allocation]; `DiagnosticExplanations` entry; steer "pass positionally"); def × keyword unchanged; mutation: remove the check → CS1746 behind SPY0908 (red). Spec sentence in the function-type section, example executed.
#### Tasks
1. `CheckLambdaCall` refuses keywords; new code + explanation. Commit: `fix(semantic): keyword arguments on a function-typed value are refused with a steer (#1650)`
2. Matrix + spec. Commit: `test(semantic),docs(spec): callee-kind × argument-form matrix; function-typed values are positional-only (#1650)`

### Issues to Close
- #1638 — D1; **criterion:** `f: (bytes) -> int = len; f(b"abc")` prints 3; matrix green; mutation recorded.
- #1650 — D2; **criterion:** diagnostic cells + positive controls; spec example executed @ sha.

---

## Batch E — Codegen seams: #1639, #1655, #1654, #1641

### Current State (@ e02f39fbb)
- #1639: `GenerateListLiteral` (`Literals.cs:29`) prefers `_targetTypeContext` (`:34-39`) over the recorded type (`:41-47`) [CORRECTED: line]; `GenerateWithNestedTargetType` (`:1338-1360`) refines List/Set/Dict elements only; `GenerateDictLiteral` prefers `_targetTypeContext` for key/value types (`:101-102`) and never pushes a nested target type into `GenerateExpression(entry.Value)` (`:136-137`) [CORRECTED: precision]; context cleared for call args (`Access.Calls.cs:1238-1241`).
- #1655: `GenerateAssignment` (`Assignments.cs:19`; Identifier `:62`, IndexAccess `:200`, MemberAccess `:268`, TupleLiteral `:325`, else `EmitNotImplementedStatement` `:527` [CORRECTED: line]); `GenerateRecursiveTupleUnpacking` (`:1387`; else silently dropped `:1424`); `GenerateStarUnpacking` (`:1162`, Identifier only); for-loop targets (`ControlFlow.cs:516/:617/:634`); comprehension targets. Semantic accepts all (`CheckTupleUnpackingElements` `:1339`).
- #1654: `ClassMembers.cs:107-147` single `getItemFunc`/`setItemFunc` slots; overloads kept in `TypeSymbol.ProtocolMethods`; dunders excluded from `MethodOverloads` (`Symbol.cs:483`) → no duplicate-signature check.
- #1641: `GenerateSliceSpec` (`Access.cs:2261-2266`) `CastExpression(NullableType(PredefinedType(int)))` → `(int? )` ×7 in `generated/ndarray_slicing_tests.cs`; no `dotnet format` in regen or CI.

### Defect Class
- **Violated contracts:** (E1) the emitter reads the **recorded** type of every literal, never the enclosing annotation; (E2) every syntactic store target is handled by one routine reachable from every store position; (E3) every dunder overload the checker accepted is emitted (one indexer per key signature); (E4) generated C# is format-stable.
- **Meta-class:** silent wrong behavior (E1–E3), position/route missing a shared check (E2, E4).
- **Known member cells:** #1639 #1655 #1654 #1641.
- **Sibling cells:** E1 — literal kind {list, set, dict-key, dict-value, tuple element} × nesting depth {1,2,3} × context {annotated decl, call arg, return, augmented}. E2 — target kind {identifier, index, member, nested tuple, star, attribute-of-index} × store position {assignment, tuple unpack, star unpack, for target, comprehension target, with-as, except-as, walrus}. E3 — dunder {`__getitem__`, `__setitem__`, `__delitem__`, `__contains__`, `__call__`} × overload count {1,2,3} × key types. E4 — every emitter site building `NullableType`/`CastExpression`.
- **Standing harness:** `ReparseEquivalenceConformanceTests` (would catch E4 if it formatted) — Phase E4 adds the format gate; none for E1–E3 — phases add matrices.

### Adversarial Review
- **Inert risk (E1):** removing the preference in `GenerateListLiteral` while `GenerateWithNestedTargetType` still re-derives from the annotation — delete the context mechanism, don't reorder it. **(E2):** a shared routine that the for-loop path doesn't call. **(E3):** emitting two indexers with the same C# signature (CS0111) when key types map to one CLR type — the semantic duplicate check must compare CLR-mapped signatures.
- **Direction:** newly refused: duplicate dunder signatures (prior: silent last-wins). Newly accepted: `a.x, b[0] = …` (prior: silent drop / `NotImplemented`).
- **Blast radius:** every literal fixture, `.expected.cs` snapshots, `Sharpy.Stdlib.Tests` (numpy generated tests for E4).
- **Generated artifacts:** spy-test C# (E4 fixes the 7 `(int? )` sites — regen is the evidence), snapshots.

### Phases
**E1 (#1639):** delete `_targetTypeContext` for literals; emitter reads `GetExpressionType` (semantic already records the contextual type via bidirectional inference — verify with `b: bool = [(1, "a")]` probe: SPY0220 expected); refine tuple + dict values. Acceptance: literal matrix executes and compares with python3; mutation: restore the annotation preference → nested tuple cell red. Commits: `fix(codegen): literals lower from the recorded type, never the enclosing annotation (#1639)`; `test(codegen): nested literal conformance matrix (#1639)`.
**E2 (#1655):** `GenerateStore(Expression target, ExpressionSyntax value)` in `RoslynEmitter.Statements.Assignments.cs` used by all eight store positions; `EmitNotImplementedStatement` for stores deleted. Acceptance: target-kind × store-position matrix executes; mutation: make tuple-unpack bypass `GenerateStore` → member-target cell red. Commits: `fix(codegen): one store routine for every target kind at every store position (#1655)`; `test(codegen): store target × position matrix (#1655)`.
**E3 (#1654):** semantic `DunderSignatureValidator` (duplicate CLR-mapped signature → new **SPY049x** overflow code (SPY0400–0449 is fully allocated) + `DiagnosticExplanations` entry; registered in `ValidationPipelineFactory` at **`Order` 407** (free; 402 DA, 405, 406 `MatchArmOrderValidator` are taken) and the `ValidationPipelineTests` validator count updated [ADDED: missing step]); emitter groups `ProtocolMethods` by key signature → one indexer each (get/set paired by signature; unpaired set → indexer with setter only). Acceptance: fixture `user_getitem_overloads_ok_1620` (int and str keys both dispatch); duplicate → diagnostic; mutation: restore single slots → overload cell red. Commits: `fix(semantic,codegen): dunder overloads emit one indexer per key signature; duplicate signatures refused (#1654)`.
**E4 (#1641):** fix `GenerateSliceSpec` trivia (`NullableType` with no trailing space — use `SyntaxFactory.NullableType(type)` without `WithTrailingTrivia`); regen spy-test C# (diff = the 7 lines); CI + `/push` gate `dotnet format whitespace --verify-no-changes --include generated/`. Acceptance: gate green; mutation: reintroduce the space → gate red. Commits: `fix(codegen): nullable cast emits no stray trivia (#1641)`; `ci: dotnet format whitespace gate over generated C# (#1641)`.

### Issues to Close
- #1639 — E1; #1655 — E2; #1654 — E3; #1641 — E4 — each **criterion:** matrix/gate green with the recorded mutation, regen diff read as findings @ sha.

---

## Batch F — CLR member signature fidelity: #1640, #1645

### Current State (@ e02f39fbb, measured)
Flat and nested `Stack[int]` members are Unknown (CS0029/CS1061 behind SPY0908). `MapGenericTypeCore` (`ClrTypeBridge.cs:365-762`) hardcoded arms, fallback `UnmappedClrType` `:757`, comment `:742-745`; `IsObjectType` (`TypeChecker.Expressions.Operators.cs:1468`) [CORRECTED: file/line]; member seam `Access.cs:305/:1307/:662→1330/:676/:1051` (gate `:1053` BuiltinType only) `/:728/:761-764 DeliberatelyPermissive`; `CreateTypeSymbolFromClrType` (`ModuleRegistry.cs:398`) populates no Methods; `CheckClrInstanceMethodCall` (`Calls.cs:4857-4953`) returns void; `InferClrIndexerReturnType` (`TypeInferenceService.cs:1516-1525`, `FirstOrDefault`; second site `TypeChecker.Expressions.Literals.cs:822` — semantic, not the emitter [CORRECTED: file]); `ClrParameterAccepts` (`Calls.cs:5471-5496`) [CORRECTED: range]; `ReportIndexerKeyMismatch` (`Access.cs:2176`); no `DeliberatelyPermissive` ratchet; sweeps `InteropConformanceTests` (binds-in-Roslyn verdict), `WarmColdDifferentialTests`, `BclMemberAbsenceTests`, `ClrOriginProvenanceTests`.

### Defect Class
- **Violated contract:** every member access on a CLR-origin receiver has the **reflected** semantic type (return type with type arguments substituted; indexer selected by key), so a mistyped destination is SPY0220 and never CS0029.
- **Meta-class:** SPY0908 as a net; silent wrong behavior (#1645 wrong indexer type).
- **Known member cells:** #1640, #1645.
- **Sibling cells:** receiver {mapped generic (List/Dict), unmapped generic (Stack/Queue/LinkedList/SortedSet/…), nested generic, non-generic BCL class, struct} × member {method, property, indexer (by key type), event?} × destination {correct type, wrong type (SPY0220), untyped `var`}.
- **Standing harness:** `InteropConformanceTests` — Phase F2 adds the fidelity axis (`x: WrongType = recv.member()` must SPY0220) and the `DeliberatelyPermissive` ratchet.
- **Owner rulings:** R5.

### Adversarial Review
- **Control run:** whole gate + 3 sweeps at the base sha *before* F1 — the red set after is classified per red: newly-typed latent user bug (SPY0220 now, wrong output before) vs mapping regression (worked before).
- **Inert risk:** honest `GenericType` but the member seam still hitting `:1053`'s BuiltinType gate → still Unknown. Probe `b: bool = s.peek()` after each task.
- **Direction:** newly refused: programs that were wrong and silent. Every red is listed with prior behaviour.
- **Blast radius:** maximal — `Sharpy.Stdlib.Tests` (numpy/sqlite use unmapped generics), Cli, LSP hover/completion, all three sweeps, warm/cold.
- **Generated artifacts:** spy-stdlib C# (typed members may change emitted casts) — regen after F1.

### Phases
**F1 (#1640):** `MapGenericTypeCore` fallback → `GenericType` over `CreateTypeSymbolFromClrType` (Methods populated lazily via `ClrTypeHelper`); `CheckClrInstanceMethodCall` returns the substituted return type; widen `:1053`; rewrite `:742-745`. Acceptance: `a: str = Stack[int]().peek()` → SPY0220; `n: int = …peek()` runs; nested `Dict[str, Stack[int]]` cell; mutation: restore `UnmappedClrType` fallback → CS0029 cell red. Commits: `fix(discovery,semantic): unrecognized constructed BCL generics map to honest GenericType; CLR member calls return their reflected type (#1640)`.
**F2 (#1645):** `InferClrIndexerReturnType` selects by key via `ClrParameterAccepts`; both sites; mismatch → `ReportIndexerKeyMismatch`. Acceptance: multi-indexer receiver cells (e.g. `DataRow[int]`/`[string]`); mutation: restore `FirstOrDefault` → string-key cell red. Commit: `fix(semantic): CLR indexer type selected by key type at both inference sites (#1645)`.
**F3 (guards):** fidelity axis in `InteropConformanceTests`; `DeliberatelyPermissiveRatchetTests` (count per site file, allowlist with issue refs, drains). Commit: `test(conformance): semantic-type-fidelity axis and DeliberatelyPermissive ratchet (#1640, #1645)`.

### Issues to Close
- #1640 — F1+F3; **criterion:** fidelity cells green; control-vs-after red classification recorded; whole gate + 3 sweeps @ sha.
- #1645 — F2; **criterion:** key-selection cells + mutation recorded.

---

## Batch G — Non-nullable IR: #1646
`CodeGenContext.Ir` (`CodeGenContext.cs:131`) nullable; constructed with IR at `ProjectCompiler.CodeGen.cs:85/:96`, without at `ProjectCompiler.Generators.cs:138` and `EmitterTestPipeline.cs:56/:81-88`; 17 `_context.Ir?.` reads. **Contract:** the emitter always has the IR it was designed against. **Cure (R6):** `Ir` non-nullable; both other sites run `LoweringPass`; the 17 reads become plain. **Acceptance:** compiles (type system is the guard); `EmitterTestPipeline` runs lowering; a test asserting `typeof(CodeGenContext).GetProperty("Ir")` has no nullable annotation (mutation: make it nullable → red). Commit: `fix(codegen): CodeGenContext.Ir is non-nullable; every construction site lowers first (#1646)`. **Blast:** `EmitterTestPipeline` consumers, generators path. **Close criterion:** annotation guard green; whole gate @ sha.

---

## Batch I — Floored arithmetic for every integer width: #1662
Measured: `int8` `-7 % 2 → -1`, `-7 // 2 → -4`, `divmod → (-4, 1)`; python3 `1, -4, (-4, 1)`; identity broken. [ADDED: measured @ e02f39fbb by the verifier] `int16` same class (`-4 -1 (-4, 1)`); **`uint64`: `7 // 2` prints `3.0`** — the expression is typed `uint64` (`b: bool = u // v` → SPY0220 names `uint64`) but lowers through the `double` `FloorDiv` overload: recorded ≠ applied, prior = *wrong output*, a cell the `(ulong, ulong)` overload fixes and the matrix must carry as a value-**and**-type row (`q: uint64 = u // v` runs and prints `3`) — **owner-confirmed 2026-08-27: assert the type**; `divmod(uint64, uint64)` → SPY0354 (refused today, accepted after); `uint32`/`uint8` rows already correct (`3 1 (3, 1)`). `ClassifyFlooredArithmetic` (`Operators.cs:316-337`; `%` gated by `IsFlooredNumeric` `:346` → `IsSharpyInteger` Int/Long only, `PrimitiveCatalog.cs:152`); `PrimitiveCatalog.IsInteger` (`:156`) includes uint64; `FloorDiv.cs`/`FloorMod.cs` `(int,long,double,float)`; `DivMod.cs:41`; pins `OperatorLoweringRecordingTests.cs:419/:512`; matrix `operators/floor_div_modulo_lowering_matrix_1658` (no narrow rows). **Contract:** `//`, `%`, `divmod` floor for every integer width and the identity holds. **Cure (R11):** one predicate `IsInteger || IsFloatingPoint`; `FloorDiv/FloorMod(ulong, ulong)` (+ `uint`→ widens to long already? verify `uint32` lowering path in the matrix); invert the two pins; matrix rows for int8/int16/uint8/uint16/uint32/uint64 × sign combos with the identity printed. **Acceptance:** every row matches python3 (`python3 -c` per row in the fixture header); mutation: restore the `IsSharpyInteger` gate → int8 row red. Commits: `feat(core): FloorDiv/FloorMod for ulong (#1662)`; `fix(semantic): // % divmod share one integer predicate — every width floors (#1662)`; `test(integration): narrow-int rows with the divmod identity join the #1658 matrix (#1662)`. **Close criterion:** matrix rows @ sha match python3; pins inverted with recorded mutation. [ADDED: missing step] Spec: `arithmetic_operators.md` `//` result-type table (`:33-40`) lists only int/long/float/decimal rows — add the narrow-width and `uint64` rows (result type per the `:315-320` promotion rules), examples executed; the `%` table (`:149`) already promises "any integer type … sign of the divisor".

---

## Batch J — numpy multi-axis integer index: #1661, #1665
Measured: `arr[idx(), :]` prints `idx called` twice, `ndim` 2; emitted `arr.Slice(SliceSpec.Range(Idx(), Idx() + 1), SliceSpec.All)` (`Access.cs:2201-2218`, `:2206/:2207`). `NdArray.Slicing.cs:187` keeps ndim; fact `MultiAxisAccessLowering` (`SemanticInfo.cs:2209`) recorded at `Literals.cs:806-808`; fixtures `ndarray_multi_axis_slice_1621`, `ndarray_single_axis_slice_1608` (Stdlib.Tests), `Spy/numpy/ndarray_slicing_tests.spy`. **Contracts:** every sub-expression is evaluated once (#1226 class); an integer index squeezes its axis (R10). **Cure:** `SliceSpec.At(int)` in `src/Sharpy.Stdlib/Numpy/NdArray.Slicing.cs` (`SliceSpec` is a Stdlib struct at `:9`, not Core) [CORRECTED: location] (single evaluation, marks squeeze); `NdArray.Slice` squeezes `At` axes; emitter emits `At(idx)` for integer dimension kind read from the fact. **Acceptance:** `idx called` once and `ndim` 1 (python3/numpy comparison in fixture header); the evaluation-count harness from #1226 gains this row; fixtures' ndim lines flip (ruled); spy-test C# regen diff = those lines + `At`; mutation: restore `Range(idx, idx+1)` → both cells red. Commits: `feat(stdlib): SliceSpec.At squeezes an integer-indexed axis (#1661)` [CORRECTED: scope]; `fix(codegen): integer index in multi-axis access lowers to SliceSpec.At — evaluated once (#1661, #1665)`; `test(stdlib),regen: ndim expectations flip to numpy semantics; spy-test C# regenerated (#1661)`; stdlib docs regen. **Close criteria:** #1661 — ndim cells match numpy @ sha, regen committed; #1665 — single `idx called` with the recorded mutation.

---

## Batch K — Process: #1649, #1634, #1648
- **#1649 (R12, Wave 0):** run the `/verify-implementation` control pair against `~/.claude/plans/plan-c6ae1b.md` [CORRECTED: path — it is not under `.claude/plans/`]; [CORRECTED: `control-cell.md` / `control-class.md` exist nowhere under `.claude/`, `~/.claude/plans/` or the skills — the 2026-08-26 pair was two synthetic plans run in-band (#1649 comment, 2026-08-26). Wave 0 must first **write** the pair as files — **R12′ rules option (a)**: `.claude/plans/controls/control-cell.md` and `control-class.md`, facts read at a named sha, known verdict in each header;] record the verdicts (expected: cell-graded plan flagged, class-graded plan passes); `/implement-plan` dry run on Batch H; `/compiler-audit` census cross-check against this file. **Close criterion:** the three transcripts' verdicts recorded on the issue; any instrument that mis-grades the control becomes a new issue before the round proceeds.
- **#1634 (R7):** delete `DiagnosticCodes.cs:735` + `DiagnosticExplanations.Validation.cs:789`; `DiagnosticCodeEmissionSiteTests`: every code whose status is Active is referenced from at least one non-test source file outside `Diagnostics/` (Reserved/Retired accepted by label); mutation: relabel a Retired code Active → red. Commit: `chore(diagnostics): remove never-emitted SPY0487; guard that every Active code has an emission site (#1634)`. **Close criterion:** guard green + mutation recorded.
- **#1648:** close with a comment pointing at 7ba7ae94a..322cba98b (skills hardening) — no code.

---

## Testing Strategy (round)
- Every batch's guard commit body records "broken → red (N), restored → green (N)"; absence assertions carry positive controls; refusals list prior-commit behaviour per newly refused program.
- Regen after the first codegen-touching commit of each wave; Batch J's regen is the ruled diff.
- Gate: whole solution + the 3 sweeps for A–F; counts reported `passed/failed/skipped @ sha (measured)`; a red is attributed only after a control at the base sha.
- Spec deliverables (executed examples): `match_statement.md` (B), `primitive_types.md` Conversions + `type_aliases.md` (C), function-type positional-only sentence (D), definite-assignment paragraph (A).

## Issues to Close (index)
H: #1633 #1660 · A: #1635 #1664 #1657 #1656 · B: #1652 #1663 #1651 #1653 · C: #1637 #1636 · D: #1638 #1650 · E: #1639 #1655 #1654 #1641 · F: #1640 #1645 · G: #1646 · I: #1662 · J: #1661 #1665 · K: #1649 #1634 #1648. Parked unchanged: #1020 #1021 #1099 #1101 #1102 #1103 #1592 #1611 #1614 #1619.

---

## Verification Summary

**Result:** PASS WITH CORRECTIONS (initially NEEDS REVISION; re-stamped the same day after the owner ruled W1 → R1′ narrow, W2 → R12′ write the pair, W4 → assert the type; W5 all-19 confirmed)
**Adequacy:** CLASS
**Verified on:** 2026-08-27 @ e02f39fbb
**Plan file:** `.claude/plans/plan-remediation-round-2026-08-27.md`

Method: all 37 issues (27 in scope + 10 parked) read via `gh` — every one OPEN; all 12 owner rulings confirmed as comments dated 2026-08-27 with wording matching §1; ~300 code references checked by five read-only verifiers (tables in the session scratchpad) and every contested refutation re-checked by the lead against the tree; 26 `run` probes on a fresh `sharpyc` Debug build @ e02f39fbb (single-file plus one two-file `.spyproj` for #1636); 12 `python3` (3.12, numpy 2.5.1) probes. Working tree clean before and after (`git status` reported by every agent).

### Corrections Made
- §2 meta-class table: #1636 (SPY0220 wrong refusal) and #1652 (SPY0104 parse refusal) removed from the "SPY0908 as a net" row — measured, neither is an ICE.
- Batch H: glob census **6 → 19** sites (13 named; three already filtered via `IsNonCorpus`); H2 sibling matrix, acceptance and Task 2 widened to 19; stray `.spy` count ~10k → **30,498** (measured); `AssertCodeGenInfoConsistency` `:126/:140-145` → `:127/:142-145`.
- Batch A: #1664 `Current State` shape needs a **bare** declaration (`x: int; x = 1`) — the initialized form compiles clean; CFG quiet default `:236-239` → `:239-242`; `GetChildNodes` usage `:268/:283` → `:276/:288`; `IrTotalityTests.cs:212` → `:68`; `SemanticInfoMergeFromTests` → `SemanticInfoMergeConformanceTests` (exists; `EverySemanticInfoSideTable_IsReferencedInMergeFrom`); spec paragraph → *extend* the existing `variable_declaration.md` §Bare Declarations; blast-radius question answered (43 bare decls in spy-sourced stdlib → regen mandatory); `GetChildNodes` guard sized (116 node types / 75 overrides / `Empty` default).
- Batch B: "None that worked before is refused" **struck** — `case int() as n:` / `case int():` + trailing `case _:` on `int` runs today and R1 refuses it (see W1); `PatternMatchPropertyTests` has six `passed > total / 3` sites, not one.
- Batch C: `SynthesizePrimitiveFunctionType` `:654` → `:656`; `ClassifyConstructorReference` arms `:901-909/:886-893` → `:898-909` / `:876-880` + `:883-893`; emitter file is `RoslynEmitter.Expressions.Access.cs:75-105`; `LoadBuiltinFunctions` `:211` → `:213`; the `grep -c … = 1` acceptance replaced (24 call sites in 11 files, mostly unrelated) by a scoped source-scan; `float("1e40")` = `1e+40`, not `inf` (Python never raises on float overflow; the float32 → inf design stands, the example was wrong); `int8("42")` is SPY0200, `int64("42")` is CS1729 (measured).
- Batch D: `CandidateAritiesDiverge` `:1987` → `:2037` (call `:1977`); `CheckLambdaCall` ranges; free diagnostic codes named (SPY0279, SPY0292–0399).
- Batch E: `GenerateListLiteral` recorded branch `:40-46` → `:41-47`; `GenerateDictLiteral` claim made precise (`:101-102` prefers context; `:136-137` no nested refinement); `EmitNotImplementedStatement` `:526` → `:527`; E3 validator given `Order` 407, an SPY049x code, `DiagnosticExplanations` + `ValidationPipelineTests` count.
- Batch F: `IsObjectType` → `TypeChecker.Expressions.Operators.cs:1468`; second indexer site is `TypeChecker.Expressions.Literals.cs:822` (semantic, not the emitter); `ClrParameterAccepts` ends `:5496`.
- Batch I: measured cells added — `int16` same class; **`uint64 // uint64` prints `3.0`** while typed `uint64` (recorded ≠ applied; prior = wrong output); `divmod(uint64, uint64)` → SPY0354; spec `//` table rows added as a missing step.
- Batch J: `SliceSpec` is a **Stdlib** struct (`Numpy/NdArray.Slicing.cs:9`) — "Core `SliceSpec.At`" and `feat(core)` → Stdlib.
- Batch K: `plan-c6ae1b` path is `~/.claude/plans/plan-c6ae1b.md`; `control-cell.md` / `control-class.md` do not exist anywhere — Wave 0 must write them first.

### Adequacy
- **Contract:** named — every batch has a Defect Class section with a one-sentence contract (H1/H2, A1/A2, B, C, D1/D2, E1–E4, F, G, I, J); all checked against the touched sites.
- **Matrix:** larger than repro lists for every batch. Cells the verifier found the plan did not name: H2 — 13 further glob sites (now listed); B — the *trailing-arm kind* axis (`_`/irrefutable vs literal/refutable) that decides whether C# emits `default:` or CS8120 (now the W1 row); I — the `uint64 //` recorded≠applied cell (now listed); A — none beyond the plan's own.
- **Cure:** seam/sweep/harness in every batch (one registry clone, one glob predicate, generic child enumeration + block expression list, `AsPattern` + recorded totality + self-matching table, CLR-identity resolver, materialized callable-reference lowering, one store routine, honest `GenericType`, one integer predicate, `SliceSpec.At`) — no N-patches. Meta-class rows cited per batch.
- **Harness:** named per batch with the allowlist/KnownRed deltas (`BlockKinds.KnownRed` ×3 drain; `QualifiedBareSpellingConformanceTests` gains two axes; `InteropConformanceTests` fidelity axis + `DeliberatelyPermissive` ratchet; `StaticStateConformanceTests` extended; B3/D1/E1/E2 add matrices where none existed).

### Falsifiability
- `SharedRegistryOwnershipTests` (H1): mutation named (share `_clrTypeCache` → red; pre-fix throw recorded); exemption not subject; positive control **yes** (same pair through `Compile` never threw); by-direction n/a (no refusal).
- `SourceGlobConformanceTests` + compile-side boundary twin (H2): mutation named (remove predicate from `ResolveGlobPattern` → cell (b) CS0017); positive control **yes** (planted bundle sources); the plan's own note that no legitimate nested `.spy` under `bin/` exists is the documented absence-control.
- `GetChildNodesCompletenessTests` (A1): mutation named (drop one child); exemption risk: leaf-by-default rule must not exempt a non-leaf with no override — parameterize on "has `Node`-typed members".
- `DefiniteAssignmentMatrixTests` / `BlockExpressionTotalityTests` / CFG statement totality (A1): mutations named per row; positive controls (assigned → runs) in every cell; by-direction: each new SPY0600 listed against prior (CS0165-ICE or silent) — **specified**.
- #1664 handler cells: mutation named (drop `ExceptionPredecessors` arm from the shared function); positive control recorded (`x: int; x = 1; print(x)` runs, measured).
- A2 `defer` cell: mutation = remove the CFG arm → KnownRed ratchet fires; positive control = LIFO fixture.
- A3 `DefinitelyAssignedBareLocals`: mutation named (drop from `MergeFrom` → multi-file twin red); executing cell prints `1`; `.expected.cs` shows `= default` (discriminating).
- B1 `AsPattern`: mutation named (drop the arm from SPY0359 uniformity); outputs `bound A`/`bound B` match python3 (measured); by-direction **specified** (CS8780 → SPY0359).
- B2 `PatternTotality`: mutation named (ignore the fact → CS8120 back); by-direction **incomplete** — see W1.
- B3 self-matching table: mutation named (drop `str` → cell red); python3 outputs measured for int/str/float/list; arity refusals match CPython's `TypeError` shapes (measured).
- C1 width table: mutation named (drop `Int8` range check → `int8("200")` red).
- C2 resolver sweep axes: mutation named (revert `ResolveModuleExportedAlias` → value cell red); the `= 1` grep replaced by a scoped scan (was not falsifiable as written).
- D1 `CallableReferenceConformanceTests`: mutation named (method group for `Bytes` → CS0123); `ArityUniformOverloadSets_…` rewrite specified.
- D2 kwargs refusal: mutation named (remove check → CS1746 ICE); by-direction **specified** (prior = ICE, measured).
- E1–E3 matrices: mutations named; E3 duplicate-signature refusal by-direction **specified** (prior = silent last-wins, measured CS1503).
- E4 format gate: mutation named (reintroduce the space → gate red); positive control = the 7 `(int? )` lines today.
- F1/F2/F3: mutations named (restore `UnmappedClrType` → CS0029; restore `FirstOrDefault` → string-key cell); control run before F1 **specified**; `b: bool = s.peek()` probe measured silent → Unknown (untyped), so F1 correctly targets the mapping, not a lowering.
- G annotation guard: mutation named (make `Ir` nullable → red); inert-fix risk low (type system).
- I matrix rows: mutation named (restore `IsSharpyInteger` gate → int8 row red); uint64 row needs the `(ulong, ulong)` overload as its own mutation (remove → `3.0` returns).
- J: mutation named (restore `Range(idx, idx+1)` → both cells red); `idx called` count is discriminating.
- K `DiagnosticCodeEmissionSiteTests`: mutation named (relabel a Retired code Active → red); exemption (Reserved/Retired by label) is not the subject as long as the test also asserts the label set is closed.

### Warnings
- **W1 — RESOLVED by R1′ (narrow).** Original finding: `case int() as n:` and `case int():` followed by `case _:` on an `int` scrutinee **run correctly today** (measured: print `int`) and are refused with SPY0700 under R1 as written — a "regression wearing a diagnostic" per verification-contract §3, and CPython accepts both. R1 is not re-asked; the plan must either list these rows as `worked → refused` and the owner accept that explicitly, or narrow the predicate to "a total class pattern may be followed only by *irrefutable* arms" (a trailing wildcard lowers to `default:`, which C# never marks CS8120, so this keeps R1's intent and the CS8120 class closed). Decide before B2.
- **W2 — RESOLVED by R12′ (write the pair first, under `.claude/plans/controls/`).** Original finding: (`control-cell.md` / `control-class.md`). Since Wave 0 gates the whole round, the pair must be written (or the existing #1649 in-band synthetic plans re-materialized as files) before anything is graded.
- **W3 — H2 census (owner confirmed: apply to all 19).** The plan's "six globs" was a third of the class (19). Corrected inline; the implementer must classify the 13 as filtered/unfiltered rather than trust the count.
- **W4 — RESOLVED (owner: assert the type).** `uint64 //` is a type-lowering mismatch (typed `uint64`, executed as `double`), not a floor-vs-truncate error; the `(ulong, ulong)` overload fixes it, but the matrix row must assert the *type* too (`q: uint64 = u // v`), or the fix could land the value and leave a widening.
- **W5 — `GetChildNodes` default is `Empty`.** A1 Task 1's guard will find every non-leaf override that is missing; with 116 node types and 75 overrides the fix wave may be large and Task 2 depends on it entirely. Size it first.
- **W6 — Batch A regen is not conditional.** 43 bare declarations in spy-sourced stdlib means A3 changes the generated C#; the regen commit is mandatory (§7).
- **W7 — E3 validator.** New validator needs an `Order` (407 suggested), a code in the SPY0490–0499 overflow range (0400–0449 is full), a `DiagnosticExplanations` entry, and the `ValidationPipelineTests` count — none were in the plan; added inline.
- **W8 — D2 allocation.** Free Semantic codes are SPY0279 and SPY0292–0399; whichever is chosen needs its `DiagnosticExplanations` entry (Rule 10).
- **W9 — C2 acceptance instrument.** The global `grep -c … = 1` would have failed forever (24 callers); replaced by a scoped scan — keep it scoped or it is vacuous in the other direction.
- **W10 — Batch B `PatternMatchPropertyTests`.** Six proportion assertions, not one; replacing one leaves five vacuous instruments.

### Missing Steps Added
- H2 Task 2: apply the predicate to the 13 additional glob sites (or record each as already-filtered).
- E3: `Order` 407 registration, SPY049x code, `DiagnosticExplanations`, `ValidationPipelineTests` count.
- I: spec `arithmetic_operators.md` `//` result-type rows for narrow widths and `uint64`, executed.
- A: regen commit after A3 (mandatory — 43 stdlib bare decls); spec extends the existing §Bare Declarations.
- K: write the control pair before Wave 0 runs.
- Round-level (already in the plan, confirmed present): whole-solution gate command, three GapDiscovery sweeps for A–F (CI runs them as separate steps — `dotnet10.yml:93,104,115`, confirmed), regen after the first codegen-touching commit per wave, allowlist drains named.

### Unchecked Claims
- `QualifiedBareSpellingConformanceTests` "22 scenarios × 2 source sets" — count not measured (allowlist confirmed empty).
- The nested `Stack[Queue[int]]` cell (#1640) — only the flat `Stack[int]` cell was measured (CS0029; `b: bool` probe silent → Unknown).
- F2's multi-indexer receiver cells (`DataRow[int]`/`[string]`) — not measured; the plan's acceptance assumes such a BCL type is reachable from Sharpy source.
- Nine of the "ten other silent switches" in Batch A (`PropertyValidator.cs:797`, `TypeChecker.Statements.cs:1792`, `ExecutionOrderAnalyzer.cs:217`, `DefaultParameterValidator.cs:118`, `StatementWalker.cs:47-118`, `TypeChecker.Utilities.cs:109`, `NarrowingFlowAnalysis.cs:122,158`, `AugmentedCollectionAssignment.cs:144-150`, `ExhaustivenessHelper.cs:66`) — line references not individually verified (only `UnusedVariableValidator.cs:122` was); verify each before Task 6.
- H1 clone cost on the LSP path — unmeasured by design (the plan measures it in Task 3).
- LSP handler files that must gain an `AsPattern` arm (B1 Task 4) — not enumerated; `grep -rl TypePattern src/Sharpy.Lsp` before starting.
- #1633's "3,937/4,000 sampled" and the Analyze-stack attribution — taken from the issue comment, not re-sampled (the 22,718 bundle count was re-measured and matches).

---

## Implementation Checklist

### Wave 0: Batch K controls (#1649)
- [x] Write `.claude/plans/controls/control-cell.md` — lead
- [x] Write `.claude/plans/controls/control-class.md` — lead
- [ ] Run `/verify-plan` on control-cell → expect CELL — lead (deferred to after implementation)
- [ ] Run `/verify-plan` on control-class → expect CLASS — lead (deferred to after implementation)
- [ ] `/implement-plan` dry run on Batch H — lead (dry run is this session)
- [ ] Record verdicts on #1649 — lead

### Wave 1: Batch H — Infrastructure
- [x] H1.1 `BuiltinRegistry.CloneForCompilation()` (#1633) — lead — a295809bc
- [x] H1.2 Wire the clone in CompilerApi (#1633) — lead — a295809bc
- [x] H1.3 Guards: SharedRegistryOwnershipTests (#1633) — lead — f70beaa25
- [ ] H1.4 Cache audit: ClrTypeHelper/ClrExtensionMethodResolver return immutable views (#1633) — deferred (defense-in-depth, not blocking)
- [x] H2.1 `CrashBundleWriter.IsNonSourceSegment` predicate (#1660) — lead — a8e63f6dc
- [x] H2.2 Apply predicate to all 19 glob sites (#1660) — fork agent — f0c682db2
- [x] H2.3 Root crash bundles outside project tree for test fixtures (#1660) — lead — fd4811fc8
- [x] H2.4 Guards: SourceGlobConformanceTests + CompileSideCorpusBoundaryTests (#1660) — lead — 22a6ea483
- [x] Regen after first codegen-touching commit — lead (H touches no codegen; N/A)
- [x] Wave 1 gate: 22,890/0/2 @ f0c682db2 (measured) — baseline +3 (SharedRegistryOwnershipTests)
- [x] CrashRoot CompilerOptionsFactory exemption fix — lead — 6c1b68d80
- [x] Baseline at HEAD: 22,895/0/2 @ 6c1b68d80 (measured)

### Wave 2: Batch A — Definite Assignment
- [x] A1.1 GetChildNodes completeness guard (#1635) — semantic-expert — 239fe6dd5
- [x] A1.2 Generic collectors via GetChildNodes (#1635) — semantic-expert — c88014044
- [x] A1.3 Block expression list (#1635) — semantic-expert — 37ffa26d3
- [x] A1.4 DA report pass shares fixpoint's in-set function (#1664) — semantic-expert — 99d411492
- [x] A1.5 Matrix + totality tests (#1635, #1664) — test-expert — 22cdf5053
- [ ] A1.6 Meta-class sweep of ten switches (#1635) — semantic-expert — in progress
- [x] A2.1 CFG builder: defer as scope-exit block (#1657) — semantic-expert — f2d5270b7
- [x] A2.2 Drain BlockKinds.KnownRed[defer] (#1657) — lead — f1f3572bc
- [x] A3.1 Record DefinitelyAssignedBareLocals fact (#1656) — semantic-expert — 9103dec72
- [x] A3.2 Emitter reads fact → `= default!` (#1656) — codegen-expert — 6ec3f5853 + 4d3fd4a4e (CS8600 fix)
- [ ] A3.3 Matrix update + KnownRed drain (#1656) — DEFERRED (for-else/while-else cells still red; DA CFG does not yet model for-else as DefinitelyRunBeforeSibling)
- [x] Regen after A3 (mandatory) — lead — 6b8f8549f + 4d3fd4a4e (2 ReModule.cs sites)
- [x] A1.6 Meta-class sweep (5 switches) — semantic-expert — b619a5355..edcb1dc31
- [x] Wave 2 gate: 22,929/0/3 @ 4d3fd4a4e (measured) — +39 passed (new tests), +1 skipped, 0 regressions

### Wave 3: Independent Batches
- [x] B1.1–B1.4 AsPattern parser/semantic/emitter/LSP (#1652, #1663) — wave3-B — e763662b8
- [x] B2.1–B2.3 PatternTotality + MatchArmOrderValidator (#1651) — wave3-B — 6bcf393cc
- [x] B3.1–B3.2 Self-matching builtins (#1653) — wave3-B — 6761f2915 (B3.3 matrix + spec deferred; TypePattern.BindingName retained deprecated)
- [x] C1.1 Per-width conversion functions in Core (#1637) — wave3-C — add01be2a
- [x] C2.1–C2.3 CLR-identity resolver + sweep (#1636, #1637) — wave3-C — b2907a761, 46665fc75, 54c652842 (NOTE: uint* emitter mangling gap is pre-existing)
- [x] D1.1–D1.2 CallableReferenceLowering (#1638) — wave3-D — f0105d05a (NOTE: emitter code landed in 46665fc75 via shared tree)
- [x] D1.3 + D2.2 conformance matrices + fixtures (#1638, #1650) — wave3-D — b8440e9c9 (NOTE: commit includes E3 files via shared index)
- [x] D2.1 kwargs refusal SPY0279 (#1650) — wave3-D — ba6a56e49
- [x] E1 Literal type from recorded type (#1639) — wave3-E123 — code in 6369e036b (G's commit), test in 2e05ba075
- [x] E2 One store routine (#1655) — wave3-E123 — 8b4e3cf6c
- [x] E3 Dunder overload indexers (#1654) — wave3-E123 — code in b8440e9c9 (D's commit via shared tree), SPY0701 + validator Order 407
- [x] E4 Format trivia + gate (#1641) — wave3-E4 — 74a03d80f (spy-test regen blocked by spy source errors from C/D changes — deferred to Wave 3 regen)
- [x] G CodeGenContext.Ir non-nullable (#1646) — wave3-G — 6369e036b (NOTE: commit includes peer hunks from E1/D in 3 emitter files)
- [x] I FloorDiv/FloorMod for all integer widths (#1662) — wave3-I — 076c5adc7, 10af86c1c, efc50e367
- [x] Wave 3 regen — lead — spy-stdlib: no diff, spy-test: no diff @ 74a03d80f
- [x] Wave 3 gate fixes — lead — 8ab0be0b2 (AsPattern GetChildNodes, kwargs scope, static state), cf0b7440d (CLR-type dedup, DefineAs, PrimitiveConversionResolver, E1 SemanticInfo, I fixture), 8e79fcc31 (uint32 section removal)
- [x] Wave 3 gate — 23,120/6/3 @ 00d6ed89e (measured). 6 remaining are C-batch narrow-type alias regressions (#1667). Filed #1666 (narrow augmented //=/%=). 5653ff530 (snapshot regen), 00d6ed89e (covariant annotation fallback) cleared 6 more from prior 12.
- [x] #1667 fix: narrow-type aliases resolve to SemanticType singletons — lead — 48e33ecf4. Gate: 15,598/1/3 Compiler.Tests (measured). Sole failure pre-existing MetamorphicCorpusSweep ParensWrapCallee.

### Wave 4: Batch F — CLR Fidelity
- [x] F1 Honest GenericType for unmapped BCL generics (#1640) — lead — 4ee7e49d3
- [x] F2 CLR indexer type by key (#1645) — lead — 4ee7e49d3
- [x] F3 Fidelity axis + DeliberatelyPermissive ratchet (#1640, #1645) — lead — 03664fb5f
- [x] Control run before F1 — N/A (F1 changes are in the same commit; pre-existing reds classified per Wave 3 gate)
- [x] Wave 4 gate + 3 sweeps — lead — merged into final gate (23,132/1/3 @ 8ce73eab0)

### Wave 5: Batches J + K remainder
- [x] J SliceSpec.At + integer index lowering (#1661, #1665) — wave5-J agent — 3b14fcbd4
- [x] J regen (ruled diff: whitespace only) — lead — cca4e64a2
- [x] K.1 Remove SPY0487 + emission-site guard (#1634) — lead — 98e8b070f
- [x] K.2 Close #1648 with comment — lead — closed with comment
- [x] Wave 5 gate — lead — merged into final gate

### Final
- [x] Final whole-solution gate — lead — 23,132/1/3 @ 8ce73eab0 (measured); sole failure pre-existing MetamorphicCorpusSweep ParensWrapCallee SPY0279; +1 J fact-switch test fixed (8ce73eab0)
- [x] Staleness gates — lead — spy-stdlib: no diff, spy-test: whitespace (cca4e64a2)
- [x] Spec examples executed — lead — numpy squeeze verified via python3/numpy 2.5.1
- [x] dotnet format whitespace — lead — 01ad37542
- [x] Close issues — lead — 26 issues closed (#1633–#1665 excluding parked)

---

## Implementation State — 2026-08-27T18:33 EDT

**Session:** `session_01TKiswVTjkTXZpAGDuTeuMv`
**Branch:** `dev` at 22a6ea483
**Base sha:** e02f39fbb (plan verified against this)
**Baseline:** 22,887/0/2 @ e02f39fbb (measured)
**Wave 1 gate:** 22,890/0/2 @ f0c682db2 (measured) — +3 new tests, 0 regressions

### Completed
- **Wave 0:** Control pair files written (`.claude/plans/controls/control-cell.md`, `control-class.md`). Instrument verification (`/verify-plan` on each) deferred.
- **Wave 1 (Batch H):** 6 commits, all green.
  - H1: `BuiltinRegistry.CloneForCompilation()` isolates per-compilation `CodeGenInfo` writes (#1633). `SharedRegistryOwnershipTests` guard (3 tests). H1.4 cache audit deferred (defense-in-depth).
  - H2: `CrashBundleWriter.IsNonSourceSegment` shared by all 19 glob sites (#1660). `ProjectConfig.CrashRoot` routes fixture crash bundles to temp. `SourceGlobConformanceTests` + `CompileSideCorpusBoundaryTests` guards (6 tests).

### Not started
- **Wave 2 (Batch A):** DA total collectors, block expression list, defer CFG, definite initializer — 14 tasks, most complex batch.
- **Waves 3–5:** All batches pending.
- **Wave 0 remainder:** `/verify-plan` instrument runs on control pair, `/compiler-audit` cross-check.

### Resume notes
- Tree is clean at 22a6ea483.
- The 6 commits (a295809bc..22a6ea483) are on `dev`, not pushed.
- H1.4 (cache audit) is deferred but tracked — the caches in `ClrTypeHelper` and `ClrExtensionMethodResolver` return mutable collections; callers don't mutate them in practice.
- Wave 2 starts with A1.1 (GetChildNodes completeness guard) — 116 node types, 75 overrides, potentially large fix wave. Read `src/Sharpy.Compiler/Analysis/ControlFlow/DefiniteAssignmentAnalysis.cs` and `src/Sharpy.Compiler/Parser/Ast/Node.cs:92` before starting.
- The plan sequences Wave 2 before Wave 3 ("R8's definite initializer may only hide what a total collector proved"). The simpler independent batches (G, I, E4) could be done in parallel if the sequencing constraint is relaxed.
- Regen is not needed yet — Batch H touches no Semantic/CodeGen paths that change generated output.
