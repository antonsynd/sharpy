<!-- Verified by /verify-plan on 2026-10-04 @ 9af04978b -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Adequacy: CLASS -->
<!-- Scope: ONE-GROUP · Generator: not-required -->
# P41 — test-suite throughput: measured parallelism and a per-program cost that is the program's own

## Context

The whole-solution gate is paid at least twice by every remaining group of the round (about forty), and it is ~70 min locally / ~109 min on CI (profile of 2026-10-02; the CI matrix #2178 has since landed). Two issues carry the class:

- **#2179** (class tracker) — `maxParallelThreads: 1` and one blanket `HeavyCompilation` serial collection date from May 2026 (#649), when the suite was 9.9k tests; ~11.5k independent `CompileAndExecute` tests now run serially (~28 min, filer's log of 2026-09-23).
- **#2180** — every `CompileAndExecute` pays a fixed tax: temp dir, copy of `Sharpy.Core.dll` + runtime closure + native assets, a `dotnet exec` process, a recursive delete, and a forced blocking Gen2 GC.

Charter: `.claude/plans/remediation-round-2026-09-20-batching.md` §3.37 (order N28; **R-DM**: P41 runs first after chores; lands alone). Dependencies #2176, #2177 and #2178 are closed (2026-10-03 chores round). [CORRECTED: both issues say "ordered AFTER #2176, #2177 and #2178"; #2177 was not named — it is CLOSED]

## Current State

Read @ 21ca52b5d (nothing below is a timing measured by this plan; timings are the filers' and are re-measured in Phase 1):

- `src/Sharpy.Compiler.Tests/xunit.runner.json` and `src/Sharpy.Stdlib.Tests/xunit.runner.json`: `maxParallelThreads: 1`, `parallelizeAssembly: false`, `parallelizeTestCollections: true`. xUnit 2.9.3 + `xunit.runner.visualstudio` 4.0.0. Core/Cli/Lsp test projects have no runner json (xUnit defaults).
- `src/Sharpy.TestInfrastructure/TestCollections.cs:5`: `[CollectionDefinition("HeavyCompilation", DisableParallelization = true)]`; applied to 215 Compiler.Tests files and 1 Stdlib.Tests file @ 9af04978b (grep `Collection("HeavyCompilation")` by directory: Semantic 69, Integration 48 (+1 Stdlib), Properties 45, CodeGen 20, Conformance 18, Project 8, Fuzz 3, Analysis/Helpers/Parser/Stress 1 each). [CORRECTED: was 212+1 @ 21ca52b5d; the roster grows with every round, so Phase 4's guard must anchor to the count measured at its own sha]
- Other serial collections: `Sequential` (`Compiler.Tests/CodeGen/SequentialCollection.cs`; 15 classes), `HostTimeZone` (`Stdlib.Tests/StrftimeDirectiveTests.cs:250`) and `ConsoleCapture` (`Stdlib.Tests/UnittestCapturedOutputTests.cs:10`), one class each.
- `FileBasedIntegrationTests` (Compiler and Stdlib) is one `[Theory]` class over `DiscoverTestFixtures(...)`, in `HeavyCompilation`. xUnit v2 never parallelises inside a class.
- `IntegrationTestBase.cs` has **three** execute arms, each with its own forced GC, and **two** copy-and-spawn blocks: `CompileAndExecuteCore` (l.190; copy+spawn l.335–469; GC l.185) has its own; `CompileAndExecuteProjectCore` (l.552; GC l.547) and `CompileAndExecuteEntryFileCore` (l.692; GC l.687) already share `EmitAndRunProjectAssembly` (l.866; callers l.640, l.750; copy+spawn ~l.940–1060). [CORRECTED: the second block was attributed to the project arm alone; the file has exactly two `new ProcessStartInfo` (l.379, l.972)] `CompileAndExecuteWithGC` (l.161) adds a second GC on top. `CopyRuntimeClosure` (l.1096) resolves the closure and copies every managed + native asset per call.
- The spawn comment (l.333) cites ".NET 10 on Linux x64 reflection/interpreted mode issues" with no issue or measurement.
- `ProjectCompilationHelper.cs:521–600` already runs multi-file programs in-process in a collectible `AssemblyLoadContext` under `TestHelpers.ConsoleLock`.
- `src/Sharpy.Compiler.Tests/runtimeconfig.template.json`: `System.GC.HeapHardLimit` = 5 GB (testhost only; spawned programs are separate processes).
- Process-global users found by grep (the Phase 2 audit must extend this, it is not complete): `Directory.SetCurrentDirectory` in `Semantic/ModuleResolverTests.cs:233,394`; `TestHelpers.ConsoleLock` users (`ProjectCompilationHelper`, `Cli.Tests/CliTestHarness.cs`); `Console.SetOut` in `Cli.Tests/CliTestHarness.cs`, `Core.Tests/PrintTests.cs`; `Environment.SetEnvironmentVariable` in `Lsp.Tests/AnalysisLatencyLogTests.cs` and `Stdlib.Tests/StrftimeDirectiveTests.cs`; **process-global mutation through product code called in-process** [ADDED: adequacy] — `os.Chdir`/`os.putenv` in the spy-generated `Stdlib.Tests/Spy/generated/os_module_tests.cs` (l.406–430; class `OsModuleTestsModuleTests`, in NO collection today), `Sharpy.Stdlib/Unittest/CapturedOutput.cs`/`CapturedStderr.cs` (`Console.SetOut`; used by `UnittestCapturedOutputTests` and the generated `logging_*_tests.cs`), `Sharpy.Compiler/Services/ReplSession.cs` (`Console.SetOut`; `Compiler.Tests/Services/ReplSessionTests.cs`), `Sharpy.Cli/Services/CompileServer.cs:286` (`SetCurrentDirectory`; `Cli.Tests/E2E/CompileServerE2ETests.cs`), sockets in the generated `socket_module_tests.cs`; sweeps writing `.claude/tmp/*-report.json` (9 Compiler.Tests classes, 4 Lsp.Tests files); the `UPDATE_SNAPSHOTS` writer in `FileBasedIntegrationTestsBase.cs:289`.
- `.claude/scripts/test-memory-harness` samples tree RSS each second and kills at a threshold; it has **no thread-count parameter**.
- CI (`.github/workflows/dotnet10.yml`): Compiler.Tests runs as 7 namespace shards; shard 1 is `FullyQualifiedName~Sharpy.Compiler.Tests.Integration.FileBasedIntegrationTests.` (trailing dot). `compiler-shard-partition` and `CiFilterCoverageConformanceTests` prove the shards partition the main filter.

## Scope

- **Group:** P41 — test-suite throughput (one non-chore group; chores riding along: none)
- **Alone:** yes (charter: lands alone; it changes how every other group's gate runs)
- **Gates:** 1
- **Filing:** cells outside the contract (a test that turns out to be order-dependent for a product reason, a product defect exposed by concurrency) go to the findings ledger; the lead files by class (contract §10)
- **Override:** none
- **Verify cost / prober budget:** [ADDED: missing step] whole-Compiler.Tests runs this plan asks for — Phase 0 (1), Phase 1 (up to 3 more + the worktree subset), count identity after Phases 2, 3 and 4 (3), Phase 5 (3 + 1 perturbed) — about a dozen, each over an hour until Phase 4 lands (derived from the plan's own task list, not measured). No sibling-cell prober: the matrix is enumerated by the harness; budget for unplanned axes = 2 (contract §10).

## Defect Class

- **Violated contract:** the suite's degree of parallelism is a measured memory budget, and the per-program cost of `CompileAndExecute` is the program's own compile and run.
- **Meta-class:** a historical constant standing in for a measurement → **standard cure:** measure first, choose every step from the measurement, keep the instrument as a standing check.
- **Known member cells:** #2179, #2180.
- **Sibling cells this plan must also cover:** all three execute arms of `IntegrationTestBase` (not only `CompileAndExecuteCore`); process-global state reached INDIRECTLY through product code a test calls in-process (`os.chdir`, `os.putenv`, `CapturedOutput`, `ReplSession`, `CompileServer`), not only the direct BCL calls [ADDED: adequacy]; the 119 spy-generated Stdlib test classes, which are each their own collection today and become concurrent the moment the Stdlib cap moves [ADDED: adequacy]; both runner jsons (Compiler, Stdlib); both `FileBasedIntegrationTests` classes; every `DisableParallelization` collection; the CI shard filters and the local skills/docs that name `FileBasedIntegrationTests`; the `dotnet-serialized` watchdog's silence window (a faster, parallel run changes log cadence).
- [ADDED: implement, lead 2026-10-05] **In-contract sibling cell:** any single-class fixture-corpus Theory serialises its corpus (xUnit v2 has no intra-class parallelism) — `FileBasedIntegrationTests` (Compiler + Stdlib, P4.2) and `LetEmissionInvarianceTests` (Compiler Conformance, 2402 rows, 576 s at N=1, P4.2b). Same cure: K hash shards + totality test.
- **Lane / reach:** tooling · probe; **fidelity-scope row:** N/A (no Python surface).
- **Standing harness that should have caught it:** `.claude/scripts/test-memory-harness` (exists; gains a thread-count axis in Phase 1).
- **Generator:** not required — the matrix is thread count {1, 2, 4, 8} × observation {peak RSS, wall clock, counts}, and cost component {pipeline, Roslyn emit, file copies, process start, cleanup, forced GC}; both are measurements, enumerated in full by the harness, not program shapes a generator could vary.
- **Owner rulings:** R-DM (P41 first after chores) — https://github.com/antonsynd/sharpy/issues/2179 batching comment 2026-10-03.

## Adversarial Review (pre-mortem)

- **Alternative root cause:** the 80–300 ms band may be dominated by the pipeline + Roslyn emit (CPU-bound, shared-state-free), not by spawn/copy/GC. Then #2180's cures buy little and all of the win is #2179. The Phase 1 breakdown discriminates; Phase 3's scope is decided by it and may shrink to "drop the GC".
- **Alternative layer:** N/A for the language ladder (test infrastructure only). The infrastructure analogue: prefer a runner-configuration change over new harness code wherever the measurement allows.
- **How the fix could be inert:** (1) raising `maxParallelThreads` while 212 classes stay in one `DisableParallelization` collection changes nothing for them; (2) sharding the fixture Theory while the shards stay in `HeavyCompilation` changes nothing; (3) staging the runtime once in one arm while the project arm keeps copying; (4) an in-process route serialised on `ConsoleLock` removes the spawn but re-serialises execution. Each phase's acceptance is a measured wall-clock delta, so an inert change is visible.
- **Before/after by direction:** every step compares `passed/failed/skipped` per assembly against the Phase 0 control at the same sha. A step that speeds the suite up by silently dropping tests (a shard that selects nothing, a filter that no longer matches) is caught by the count identity and by the shard-totality test.
- **Blast radius:** all test projects that derive from `IntegrationTestBase` (Compiler.Tests, Stdlib.Tests), CI workflow, `.claude/scripts/*`, the `/run-tests`-family skills and CLAUDE.md lines that quote runtimes or filters. No product code.
- **Generated artifacts touched:** none (spy-test C# is generated into Stdlib.Tests but not edited here; run `check_spy_tests_staleness.sh` once at the end as a control).
- **Flakiness risk:** concurrency-only failures (port binds, fixed temp names, time-zone/culture mutation, console capture). Phase 2 isolates the known ones BEFORE the cap is raised; Phase 5 runs the suite three times at the chosen setting.
- **Machine-memory risk:** the operational contract "concurrent `dotnet test` runs each consume 5–10 GB" assumes one thread. The chosen thread count must keep one run's peak tree RSS (testhost + spawned children) within the budget recorded in Phase 1; the serialized lock stays.

## Design Decisions

1. **Measure, then choose.** No thread count, shard count or cure is fixed by this plan. Phase 1 produces two tables; Phases 3–5 read them. The plan gives decision rules, not numbers.
2. **Isolation before speed.** Process-global users move to named serial collections (or lose the global) before any cap is raised, so a red after the raise is attributable.
3. **Shard by collection, not by project.** xUnit v2 parallelises across collections only. `HeavyCompilation` is replaced by per-class default collections where a class has no shared mutable state, and by a small number of named serial collections where it does. The memory reason for the blanket collection (#649: per-class BCL metadata) was removed by `SharedReferences`/`SharedBaseCompilation`; Phase 1 verifies that with RSS at 2/4/8 threads rather than assuming it.
4. **Fixture Theory → K partition classes** selecting rows by a stable hash of the fixture's relative path (ordinal, culture-invariant — not `string.GetHashCode`, which is randomised per process). K is a constant chosen in Phase 4 from the Phase 1 table; a totality test asserts union = discovery and pairwise-disjoint. Class names keep the prefix `FileBasedIntegrationTests` so `--filter "FullyQualifiedName~FileBasedIntegrationTests"` and `DisplayName~test_name` keep working; the CI shard-1 filter (trailing dot) is updated in the same commit and re-proved by `compiler-shard-partition` and `CiFilterCoverageConformanceTests`.
5. **Per-test tax — decision rule** (applied to the Phase 1 breakdown, medians over ≥300 fixtures):
   - forced GC ≥ 10% of the per-test median, or RSS at the chosen thread count stays under budget without it → remove the per-call GC from all three arms (keep `CompileAndExecuteWithGC` for the property loops that ask for it by name).
   - copies + delete ≥ 10% → **stage once**: one per-process staging dir (under `SharedTestCacheDir`) holding `Sharpy.Core.dll`, the closure and native assets; each test writes only its own assembly + runtimeconfig into its own dir and probes the staged dir. The probing mechanism (`additionalProbingPaths` vs. launching with a `.deps.json`/`APP_PATHS`, vs. hard links) is chosen by a spike in Phase 3 Task 1 and must be proved on a fixture that loads a native asset (sqlite) and on one with `GetAdditionalReferenceAssemblyPaths()`.
   - process start is the dominant remainder → **record it, do not cure it here.** In-process execution trades a process for `ConsoleLock` serialisation, loses kill-on-timeout, exit-code mapping and static-state isolation of `Sharpy.Core`, and has an unverified Linux-x64 caveat. It is a separate decision for the owner with the numbers in hand; this plan's close-out posts the measured split on #2180 and, if process start exceeds half the per-test median after Phases 3–5, asks for that ruling instead of taking the route silently.
6. **Result record unchanged:** 30 s timeout, `TimedOut`, exit-code mapping, `#line`-mapped diagnostics, `ExecutionResult` shape.
7. **CI and local use the same runner json**; CI's 4-vCPU runners are bounded by `maxParallelThreads` expressed as a multiplier or by a CI-side override only if Phase 5's CI run shows a regression.

## Implementation Checklist

Base sha `9af04978b` (verified); implementation started @ `116c6e29d` (no plan work landed in between — those 9 commits are #2234/#2235/#2237/#2238 chores). Lead = /implement-plan session 2026-10-05.

- [x] P0 control run — lead — `.claude/tmp/p41-control.log` + `.claude/tmp/p41-control/{P}.{log,trx,samples}` @ `116c6e29d` (measured 2026-10-05 00:05–01:38, qwen loaded, machine used ≈24.0 GB before, swap 2072 MB):

  | project | passed/failed/skipped | wall s | peak tree RSS MB | peak testhost MB |
  |---|---|---|---|---|
  | Core | 3989/0/0 | 5 | 332 | — |
  | Lsp | 1092/0/0 | 107 | 6461 | — |
  | Cli | 441/0/1 | 31 | 1644 | — |
  | Stdlib | 4162/0/0 | 66 | 824 | — |
  | Compiler | 48189/0/3 | 5325 | 7106 | 6140 |

  Compiler `FileBasedIntegrationTests` rows (trx testName match): 3569 passed. Peak machine used during Compiler run 29.5 GB of 36 GB; swap flat (2072→2048). **Budget finding:** budget = 36864 − ~24000 − 4096 ≈ 8.7 GB at this reading (higher than the plan's 6 GB reading); the N=1 Compiler cell (7.1 GB tree, 6.1 GB testhost vs 5 GB `HeapHardLimit` — testhost RSS includes non-GC memory) fits, with ~1.6 GB headroom. Lsp at xUnit defaults (14-way) already 6.5 GB.
- [x] P1.1 harness `--threads N` + summary line — lane A — `cd0ebb326` (RunSettings override honoured: N=2 max_concurrent=2 / 2696 cross-class overlaps; N=1 → 1 / 0)
- [x] P1.2 opt-in per-call timing breakdown (3 arms) — lane A — `b9d542c8f`; switch OFF FileBased 3569/0/0 = control; coverage ≥0.9989 all arms; mutation (process recording dropped) red in all 3 arms, **re-run by lead**: broken 0.729/0.936/0.747 → restored 0.9986/0.9987/0.9994. Single-arm medians (ms): forced-GC 63.2, process 48.2, pipeline (incl. C# gen) 5.4, write+copy 1.9, cleanup 1.2, emit 1.0, total 121.8 → **GC ≈ 52% (≥10% → drop), copies+delete ≈ 2.5% (<10% → no stage-once; P3.2 skipped), process ≈ 40%**.
- [x] P1.3 measurement matrix — lead — done 2026-10-05 02:20–04:49: (b) worktree `sharpy.worktrees/p41-measure-b` @ b9d542c8f with `[Collection("HeavyCompilation")]` stripped from the 48 `Integration/*.cs` files (list: `scratchpad/p41-b-stripped.txt`), filter `Integration.&!~FileBasedIntegrationTests` at N=1/2/4/8; (a) worktree `p41-lane-a` FileBased N=2 + whole Compiler.Tests N=2 (N=1 = P0). Out: `.claude/tmp/p41-matrix/`
- [x] P2.1 process-global audit table — lane B — done 2026-10-05, `scratchpad/p41-audit.md` (session ab59078d). Key findings carried forward:
  - **S1 (code-confirmed by lead, run-unconfirmed):** `HeavyCompilation`'s only `[CollectionDefinition]` is in `Sharpy.TestInfrastructure`; xUnit v2 reads definitions from the test assembly only → `DisableParallelization` has been INERT since #679; it is a plain named collection (serial within, parallel with others once the cap > 1). Phase 1 (a) at N=2 is therefore NOT expected to be flat. Every new serial collection is DEFINED in its members' own test assembly; the P2.3 guard checks by reflection that each allow-listed collection is defined in that assembly with `DisableParallelization = true`.
  - S2 `ReplSessionTests` (no collection) does `Console.SetOut` on a thread-pool thread without `ConsoleLock`; S3 `AnalysisLatencyLogTests` sets per-thread `CurrentCulture`, not env — the guard bans only `DefaultThreadCurrent*Culture`; S5 `Sequential`'s NameMangler reason is stale; S6 `random` module state is one shared `System.Random` → `PrngState` collection (mutual only) for 4 Stdlib classes.
  - Proposed: Compiler `ProcessCwd`(ModuleResolverTests), `ConsoleCapture`(ReplSessionTests), `PropertySerial`(44 CsCheck); Stdlib `ProcessCwd`(os), existing `ConsoleCapture`(+2 logging), `HostTimeZone`, `PrngState`(4 random); `RepoReportWriters` dropped (one writer per file). 7 partial twins, in `Spy/SpyTestCollections.cs` (NOT `Spy/generated/` — the regen script deletes orphans there).
  - Control side-note: Lsp.Tests (xUnit defaults, 14-way) peaked at 6461 MB tree RSS @ 116c6e29d — already over the ~6 GB budget on its own.
- [x] P2.2 named serial collections (+ 7 partial twins in `Spy/SpyTestCollections.cs`) — lane B — `bb02f610f` (Core 3989/0/0, Stdlib 4162/0/0 = P0; spy-tests staleness exit 0)
- [x] P2.3 process-global scan guard `ProcessGlobalStateConformanceTests` (27 tests) — lane B — `0aecaa128`; mutations: ModuleResolverTests un-collected → 2 red; os twin un-collected → 2 red; ProcessCwd definition moved to TestInfrastructure (S1) → 2 red; each restored → 27 green. Conformance filter 17363/0/0. **Lead mutation re-run** (worktree @0aecaa128, ProcessCwd definition moved to Sharpy.TestInfrastructure): 27/0/0 → broken 2 failed/25 passed (both ModuleResolverTests SetCurrentDirectory sites: "no [CollectionDefinition] in src/Sharpy.Compiler.Tests") → restored (cmp) 27/0/0. Reproduces.
- [x] P3.1 single execute helper `RunEmittedProgram` + guard `IntegrationTestBaseExecutePathTests` — lane A — `b05761027` (3 block differences accounted; mutation pre-fold file → red 2≠1, restored → green; counts by name×outcome = P0: FileBased 3569, 14 project/entry-file classes 837, Stdlib FileBased 241; sqlite fixture `stdlib_sqlite3_basic` passes). **Lead mutation re-run** (worktree @0aecaa128, IntegrationTestBase.cs replaced by its b9d542c8f pre-fold version): 3/0/0 → broken 2 failed/1 passed (Expected 1) → restored (cmp) 3/0/0. Reproduces.
- [~] P3.2 stage-once — SKIPPED by decision rule (copies+delete ≈2.5% < 10%, P1.2 @ b9d542c8f)
- [x] P3.3 forced GC removal + spawn comment — lane A — `3c91f6ade` (IntegrationTestBase.cs only; `CompileAndExecuteWithGC` keeps its own GC). Back-to-back A/B FileBased (b05761027 vs +change, load avg 3.7–8.3, read as a pair): single-arm median 139.77 → 68.94 ms (p90 387 → 147), wall 703 → 259 s; **process share after = 83% (57.4/68.9; quieter 14-class run 44.1/54.2 = 81%) > 50% → in-process question to owner at P5.3**. Counts name×outcome = P0: FileBased 3569, 14 classes 837 (+3 guard), Stdlib FileBased 241 (median 91.4 → 84.5; Stdlib GC ≈10 ms). Lead re-measure (no GC, HeavyCompilation dissolved) b-subset N=1/2/4/8 @ 3c91f6ade in worktree p41-measure-c (full sln build) → `.claude/tmp/p41-matrix-nogc/` (measured 08:55–09:09, load avg 3–7, 14 cores = 10P+4E):
  | N | wall (no GC) | wall (GC, P1.3) | peak tree / testhost MB | counts |
  |---|---|---|---|---|
  | 1 | 248 s | 669 s | 2444 / 1878 | 5169/0/0 |
  | 2 | 172 s | 458 s | 2334 / 1765 | 5169/0/0 |
  | 4 | 160 s | 509 s | 2581 / 2067 | 5169/0/0 |
  | 8 | 179 s | 527 s | 2973 / 2500 | 5169/0/0 |
  (5169 = 5163 + the 6 `PerModuleAssemblyTests` that pass on a full-solution build — P1.3's 6 reds were the project-scoped build, confirmed.) GC removal alone: −63% at N=1. Scaling knee N=2–4 on this subset: at N=4 the trx shows 112 of 154 s at concurrency 4, but per-class busy time inflates ×1.6–2 (BlockScopeRedeclarationMatrixTests 85 → 133 s; summed busy ≈2.2× N=1) — CPU contention from the per-test `dotnet exec` spawns, plus a single-class tail (BlockScopeRedeclaration, 1833 rows, ends last at every N). Memory is not binding here (≤3.0 GB tree at N=8 vs ≈12–14 GB thresholds). Whole-suite N choice is P5.1's.
- P1.3 interim (b) cells @ b9d542c8f+strip (GC still forced): N=1/2/4/8 wall 669/458/509/527 s, peak tree 1962/1742/1845/1824 MB, counts 5163/6/0 at every N (the 6 = `PerModuleAssemblyTests`, environmental: project-scoped build of worktree b lacks per-module Stdlib assemblies, #1479 deliberate failure). Scaling stalls after N=2 — consistent with the blocking per-call Gen2 GC; re-measure after P3.3. (a) FileBased N=2: 3569/0/0, 416 s, 1553 MB (single Theory class: no intra-class parallelism expected).
- P1.3 (a) whole Compiler.Tests N=2 @ 116c6e29d (worktree p41-lane-a, collections as at P0): **48189/0/3 (= P0)**, wall **5742 s vs 5325 s at N=1**, peak tree 6731 MB / testhost 6168 MB (P0 7106/6140), threshold 10425 MB not reached, swap 2048→2048. trx profile (`scratchpad/p41-trx-profile.py`): span 5731 s = 4493 s at concurrency 1 + 1236 s at 2 — the 173 `[Collection("HeavyCompilation")]` classes form ONE serial collection (definition inert per S1 but the attribute still groups them), so N=2 buys ≈22% overlap. In that overlap `LetEmissionInvarianceTests` (2402 tests, one class) ran 1350 s vs 576 s at N=1 (×2.3) and `FileBasedIntegrationTests` 767 vs 657 s — the per-call blocking `GC.Collect` in one thread stalls the other. Consequence for P4.2: after P4.1, **LetEmissionInvarianceTests (576 s single class at N=1) is the long pole next to FileBased (657 s)** — the totality/partition design must cover it (it inherits `FileBasedIntegrationTestsBase`), or P5.1's floor is ≈576 s.
- [x] P1.3 tables posted on #2179 (comment 5991201864) and #2180 (comment 5991202098) — lead
- [x] P4.1 dissolve HeavyCompilation by directory group — lane B — 5/5 landed (counts by name×outcome vs P0 trx, walls N=1→N=4, GC still forced): de2797921 rest+Stdlib (Compiler 3428/0/0 80→49 s; Stdlib 4162/0/0 57→86 s), edd66da5e CodeGen+Conformance + Sequential dissolved (18129/0/0 = P0 +27 ProcessGlobalState guard; 820→742 s), 984a2fe5e Semantic (11196/0/1 = P0; 801→646 s), 7385408a9 Integration (8738/0/0 = P0; 985→907 s). f692cd789 Properties (MetamorphicCorpusSweep + HeavyCompilationCollection definition removed; TestCollections.cs comment-only): N=1 = P0; N=4 run 3065/1/2 — the red is ILCompiles fresh seed 8sLgyTw-IOM3 = open #2139 (None-literal member access, CS0023), reproduced at N=1 by lead (`.claude/tmp/lead-ilseed-n1.log`), not concurrency; ledger row (duplicate).
- [x] P4.2 partition fixture Theory — lane A — `ebe4523bb` (namespace `Integration.FileBasedIntegrationTests` + abstract base + Shard0..7 Compiler / Shard0..3 Stdlib; FNV-1a over the '/'-relative path; discovery cached per root (ConcurrentDictionary<Lazy>); unsharded `DiscoverTestFixtures(path)` kept uncached as the totality oracle; CI filters unchanged, shard-1 comment fixed). Totality guards `FixtureShardTotalityTests` (8) + `StdlibFixtureShardTotalityTests` (4): literal K, union = unsharded by testName multiset, pairwise disjoint, none empty, FNV vector pinned. Mutations: modulus 9 in Shard3 + index 0 in Stdlib Shard1 → 2 red each; GetHashCode + Shard7 deleted → 6 red; restored → 8/8, 4/4. CI partition logic local: main 48044 = union 48044. UPDATE_SNAPSHOTS: 165 rewritten, zero diff. Fixture rows name×outcome = P0 at N=4 and N=1. Walls: Compiler FileBased N=1 193 s → N=4 101 s; Stdlib 22 → 11 s. **Recorded deviation:** `--list-tests` 3576/244 vs 3569/241 — the fixture rows are unchanged (3568/240); +7/+3 = the base class's `[Fact] TestFixturesDirectory_Exists` now inherited per shard class (explained positive delta, no row lost; the union test already fails on an empty corpus).
- [x] P4.2b [ADDED: in-contract sibling cell, lead 2026-10-05] shard `LetEmissionInvarianceTests` corpus Theory (same cure as P4.2: fixture-corpus Theory serialised by one class; 576 s at N=1 @116c6e29d, 1350 s at N=2 under contention, no execute so P3.3 does not shrink it; after P4.2 it is the suite floor) — lane A → `af6f30901`: namespace `Conformance.LetEmissionInvarianceTests` = Controls (4 Facts + inherited directory Fact, once) + abstract LetTwinShard (not derived from the FileBased base, no Fact replication) + Shard0..7 (Theory only, same ShardOf) + internal static Instrument; `FileBasedIntegrationTestsBase.NormalizeCSharp` protected → public. **Root of most of the cost was per-cell rediscovery** (`ComparableFixtures().Single(...)` walked the corpus once per cell, ~2400 walks) → one cached walk: unsharded class N=1 466 s (re-measured in lane-a2) → 57 s at N=1, 43 s at N=4. Ratchet across shards: 3 temp allowlist lines → exactly 3 failures, each once (identical twin, no site, stale entry in Controls.Census); restored (cp, zero diff). `LetEmissionShardTotalityTests` (literal 8, union = unsharded fresh walk, disjoint, none empty): Shard3 → shard 2's cells → 2 red, restored → 4/4. `--list-tests ~LetEmissionInvarianceTests` 2402 = 2402 (name multiset identical after fold); CI partition 48048 = 48048, shard 6 holds all 2402; name×outcome = P0 at N=4 and N=1. Lead grep: no other corpus Theory rediscovers per cell (Unparser round-trip passes the path; FormatterEmitInvariance's `.Single` is a one-off control).
- [x] P4.3 serial-collection roster guard — lane B — `b335dd6a3` (extends ProcessGlobalStateConformanceTests, 31 tests: literal roster of 7 DisableParallelization collections × project × reason × 52 members; definitions = roster, members = roster, positive control on pinned counts, no inert `[Collection]` without a same-project definition). Mutations: un-rostered class joins ProcessCwd → 2 red; extra definition → 2 red; roster row deleted → 3 red; `[Collection("HeavyCompilation")]` re-added without definition → 1 red; each restored (cp+cmp) → 31/0/0. Conformance 17367/0/0. **Lead mutation re-run** (worktree p41-measure-c @b335dd6a3, Core ConsoleCapture roster row deleted, rebuilt): 31/0/0 → broken 3 failed/28 passed (PositiveControl, EveryRunAloneCollection_IsRostered…, EveryMemberOfARunAloneCollection_IsRostered) → restored (cmp, rebuilt) 31/0/0. Reproduces.
- [x] P5.1 re-measure + raise cap (Compiler, then Stdlib in its own commit) — blocked by P2–P4
- P5.1 Compiler.Tests whole project (`Category!=Benchmark`) @ af6f30901, worktree p41-measure-c, full sln build, heap limit 5 GB → `.claude/tmp/p41-p51/` (measured 10:03–12:57; trx span is the xUnit time, harness wall includes host start):

  | N | trx span | harness wall | peak tree / testhost MB | threshold MB | counts | time at full concurrency |
  |---|---|---|---|---|---|---|
  | 1 | 2042 s | 2053 s | 8200 / 6743 | 15289 | 48241/1/3 | — |
  | 2 | **1641 s** | 1652 s | 7197 / 6090 | 14926 | 48241/1/3 | 1208 of 1641 s |
  | 4 | 1981 s | 1994 s | 7126 / 6083 | 8840 | 48241/1/3 | 1567 of 1981 s |
  | 8 | 4707 s | 4717 s | 7447 / 6349 | 18040 | 48240/2/3 | 1262 of 4707 s |

  - vs P0 N=1 5325 s: −62% at N=1 (GC removal + LetEmission cache + no HeavyCompilation tail), −69% at N=2.
  - The 1 red at every N = `DispatchSiteInventoryTests.GuardCitations_ResolveToRealTests…`: REGRESSION from ebe4523bb (23 refusal-net citations of `FileBasedIntegrationTests`, now a namespace; resolver matches `Type.Name` only). Missed by lane pre-commit filters. Fix → lane A.
  - N=8 extra red: `Fuzz.SemanticPropertyTests.Compilation_IsDeterministic(seed: 2025)` — same input, 5 vs 1 diagnostics within one process under concurrency → product finding (ledger row), not a test-infra defect; passed at N=1/2/4.
  - Oversubscription at N≥4: classes with internal parallelism inflate — ReparseEquivalenceConformanceTests busy 65 s (N=1) → 114 (N=2) → 654 (N=4) → 779 (N=8); at N=8 `Properties.Algebraic.BooleanPropertyTests` (PropertySerial, run-alone phase) took 2063 s for 2 tests (CsCheck Sample under xUnit's MaxConcurrencySyncContext). N=4 is slower than N=2; N=8 is 2.3× N=1.
  - **Chosen: N=2** (best span, peak tree 7.2 GB inside every recorded threshold). Heap-limit axis at N=2 (built runtimeconfig edited): 5 GB 1641 s 7197/6090 MB; 4 GB 1789 s (+9%) 5959/5077 MB; 3 GB 1877 s (+14%) 5466/4160 MB; zero `System.OutOfMemoryException:` at every value (the 487 log mentions are ImportResolver debug symbol lists, same in all three) → **5 GB kept** (rule: ≤~5% wall loss). Compiler cap committed `a24387bbf` (maxParallelThreads 1 → 2). Stdlib cap: lane B measuring.
- [x] P5.1-fix guard-citation regression from ebe4523bb — lane A — `56d638837` (DispatchSiteInventoryTests.cs only): a citation X resolves as a type name (unchanged) OR as a shard group = namespace whose last segment is X, holding ≥1 concrete test class, all deriving from one abstract base declared there (strict; LetEmissionInvarianceTests fails it because of Controls, nothing cites it); a guarded-by row citing a group is refused with its own message. 42 refusal-net rows cite FileBasedIntegrationTests (not 23). Synthetic control: FileBasedIntegrationTests resolves; Transforms, Integration, LetEmissionInvarianceTests refused; guarded-by group refused — 4 violations. Mutation name-only → 2 of 3 GuardCitations red (real roster 42 violations + control) → restored 3/3. **Lead mutation re-run** (worktree p41-measure-c @56d638837, `!testGroups.Contains(cited)` → `true`, rebuilt): 3/0/0 → 2 failed/1 passed (43 distinct FileBasedIntegrationTests violations = 42 real + 1 synthetic, each printed twice) → restored (cmp, rebuilt) 3/0/0. Reproduces.
- [x] P5.1-Stdlib — lane B — `8c4465934` (WallClockWindow run-alone collection + Spy twin for ThreadingModuleTestsModuleTests: `test_timer_fires_after_interval` asserts a fixed 200 ms window for a 50 ms Timer whose callback runs on the thread pool (`Threading/Timer.cs:31`); red once at N=8, 0/4 alone with/without CPU load; roster 8 collections / 53 members; mutations twin un-collected → 2 red, roster row deleted → 3 red, restored → 31/0/0) and `8df7f3275` (Stdlib maxParallelThreads 1 → 4): N=1/2/4/8 142/33/26/23 s, tree RSS <1 GB, swap flat; 3×N=4 repeats 23/23/25 s 4169/0/0; N=8 repeat clean; perturbed N=3 aggressive 28 s clean; every run = P0 + 7 explained rows (4 totality tests, 3 replicated directory Facts); json-only confirmation 4169/0/0 in 21 s.
- [x] P5.2 three full runs + one perturbed-order run — lead — @ 8df7f3275, worktree p41-measure-c, full sln build, `dotnet test sharpy.sln --filter "Category!=Benchmark"` at the committed caps (Compiler 2, Stdlib 4) → `.claude/tmp/p41-p52/` (measured 14:44–16:38):
  - walls (whole solution, wrapper start→exit): full1 1885 s, full2 1670 s, full3 1678 s, perturbed (`-- xUnit.MaxParallelThreads=3 xUnit.ParallelAlgorithm=aggressive`) 1583 s — ≈ 28 min (P0: Compiler.Tests alone 5325 s).
  - counts every run: Compiler 48243/0/3, Stdlib 4169/0/0, Core 3989/0/0, Lsp 1092/0/0, Cli 441/0/1 → **whole solution 57934/0/4 @ 8df7f3275 (measured)** (P0 57873/0/4 @ 116c6e29d).
  - name × outcome vs P0 (`scratchpad/p41-trx-compare.py`, folds shard namespaces and truncated checkout paths): only_in_control = 0 for every project in every run; only_in_run = Compiler 54 (ProcessGlobalState 31 + execute-path 3 + FixtureShardTotality 8 + LetEmissionShardTotality 4 + citation synthetic 1 + replicated `TestFixturesDirectory_Exists` 7), Stdlib 7 (totality 4 + replicated Fact 3), Cli/Lsp/Core 0. Identical across the three runs and the perturbed one → no order dependence observed.
- [~] P5.3 docs half — lane A — `1cf19a13c` (8 files): "~22 min" → "~30 min (Compiler.Tests ~27 at maxParallelThreads 2 @ af6f30901)" in CLAUDE.md, contract §6, implement-plan SKILL ×2, compiler-audit SKILL, implementer.md, verification-expert.md; CLAUDE.md GapDiscovery advice reworded (GapDiscovery-tagged classes = 509 of 2850 s busy at N=2, ≤17.9%); dotnet-serialized TIMEOUT 2700 → 4500 s (gate ~1850 s + one N=1 Compiler.Tests 2042 s ≈ 3900 s, +15%), comment sourced; bench_ab.py comment; "5–10 GB" kept (7.2 GB peak at N=2). `bash -n` clean; test_dotnet_serialized_lock.py 35/35. Deferred: CI shard-balance comment (needs the CI run).
- [x] P5.3 CI PR run — owner said push (2026-10-05): merged origin/dev (bot benchmark commit 56da0e04c, merge 86825825e — merge, not rebase, keeps the shas cited on #2179/#2180/#2250), whole-solution format clean, pushed dev 56da0e04c..86825825e, **PR #2252 dev → mainline** opened. CI run 37378225480 green (13.5 min vs 16.0 on mainline @ 116c6e29d; shard 5 Properties the long pole at 554 s) → owner asked for a re-balance: `7fbe6389f` (shard 3 = all Semantic, 4 = Properties.Parser, 5 = Properties rest; partition replay 48051 = 48051, mutation shows 60 double-run rows; CiFilterCoverage 3/0/0) → run 37381104594 green, **10.8 min**, Compiler shard jobs 161–364 s. #2179 comment posted.
- [x] Findings filed (lead, contract §10): ledger 9 rows → 2 class issues + 2 tracker comments + 1 duplicate: **#2250** (class-tracker, lane:silent-wrong, reach:probe — process-wide mutable state under concurrent in-process use: compile nondeterminism 5 vs 1 diagnostics @ af6f30901 N=8, module-level random static System.Random, CrashBundleWriter check-then-create); **#2251** (class-tracker, lane:false-refusal, reach:program — threading.Timer is a pool timer, not a Thread: isinstance False, join SPY0203, starvation; absorbs the fixed-window spy-test row and the thread-pool deviation row, so no deviations.yaml row); #2139 cell comment (ILCompiles seed 8sLgyTw-IOM3, duplicate); #2179 cell comment (OverloadIndexCache.ClearAll separator-less prefix, benign). `dada5fddf` makes the WallClockWindow twin + roster reason cite #2251 (guard 31/0/0, ThreadingModuleTests 43/0/0).
- [x] Final read-only checks @ 8df7f3275: spy staleness (check_spy_staleness.sh exit 0, check_spy_tests_staleness.sh exit 0, 119 files; SHARPYC = worktree binary at the same sha); `dotnet format whitespace sharpy.sln --verify-no-changes` exit 0; product code untouched (`git diff 116c6e29d..HEAD -- src/Sharpy.{Compiler,Core,Stdlib,Cli,Lsp}` empty). Standalone verification-expert (opus, report `scratchpad/p41-verification-report.md`): **REFUTED none**. CONFIRMED count identity (own multiset diff; shard fold = exactly 5971 renamed rows), shard-totality falsifiable (own mutation Shard3→FixturesOfShard(2): 12/0/0 → 10/2 → 12/0/0; K literal; FNV values re-derived), CI partition (48049 = 48049 at 8df7f3275, +1 = the 56d638837 synthetic Fact), collections/caps/heap/GC, doc figures. Gaps: **2b** a `Skip`/`Trait` on one shard's Theory leaves every totality guard green → in-contract, lane A fixing; **L1** stdlib module-level setters (socket.setdefaulttimeout, calendar.setfirstweekday, random.seed) not in the process-global roster, latent (readers in one class) + **L2** socket closed-port race → cells on #2250; **P1** LiteralStringSurfaceMatrixTests (parallel) calls CompileAndExecuteWithGC 13× → cell on #2179. Caveats recorded: "5–10 GB" never measured for a whole-solution run; P5.1 N=4 vs N=2 ran under different memory baselines (N=2 still supported by per-class slowdown).
- [x] Verifier 2b closed — lane A — `a1504a484` (`FixtureShardScan.ShardMethodViolations`: exactly one [Theory] with empty Skip, one [MemberData] naming the class's own source, no trait attribute beyond the shard base's; new Fact `EveryShard_RunsItsRowsAsOneUnskippedUncategorisedTheory` in the three totality classes, count = literal K). Mutations: Compiler Shard3 `Skip="x"` → only the new Fact red (union/disjoint/non-empty stayed green — gap confirmed); Stdlib Shard1 `[Trait("Category","Benchmark")]` → new Fact red; restored → 651/651, 5/5. Filter Compiler 702/0/0, Stdlib 5/0/0.
- [x] #2180 close-out posted (comment 6002795121) incl. the in-process question (options a/b/c) for the owner.
- [x] Final: whole-solution gate **57937/0/4 @ a1504a484 (measured)**, 1925 s, build 0 warnings; name×outcome vs P0: only_in_control = 0 in all 5 projects, only_in_run Compiler 56 / Stdlib 8 (guards + replicated directory Facts) / others 0. Spy staleness ×2 exit 0, format clean, verification-expert REFUTED none (gaps closed or filed). Worktrees p41-lane-a, p41-lane-a2, p41-measure-b, p41-measure-c, p41-lane-b-stdlib removed after cmp-confirming every local modification equals a committed file (measure-b: 48 measurement-only attribute strips).

## Implementation

### Phase 0: control

**Goal:** the count baseline every later step is compared with.

**Acceptance:** per-assembly `passed/failed/skipped @ <sha> (measured)` and wall clock recorded in `.claude/tmp/p41-control.log` (private path, verbose console logger so the wrapper watchdog sees output).

#### Tasks
1. **Control run** — no file changes.
   - `.claude/scripts/dotnet-serialized test --filter "Category!=Benchmark" --logger "console;verbosity=normal"` per test project, Compiler.Tests as its own background call (it exceeds an hour). Record counts, wall clock, and the per-test durations (trx or verbose log) used later for medians.
   - No commit.

### Phase 1: instruments and the two measurement tables (#2179 step 1, #2180 step 1)

**Goal:** thread count × {peak RSS, wall clock, counts} and the per-call cost split, both reproducible by one command each.

**Acceptance (class-level, measurable):**
- `test-memory-harness --threads N` reports peak tree RSS, wall clock and counts for N ∈ {1, 2, 4, 8} on (a) the current collections and (b) a representative un-collected subset, so the table separates "threads raised" from "collection removed".
- The per-call breakdown prints median and p90 per component {pipeline, Roslyn emit, write+copy, process start-to-exit, cleanup delete, forced GC} over ≥300 fixtures, for all three execute arms.
- Guard mutation: with the timing switch OFF, an existing fixture test's output and the suite's counts are unchanged (control); with it ON, the report file exists and its per-component sums are within 5% of the per-test wall time (an instrument that drops a component is caught).
- Execution evidence: both tables pasted into the commit body and posted on #2179 / #2180.

- [ADDED: owner input 2026-10-04] **Memory budget (this machine, as worked on):** 36 GB physical; ~26 GB in use before any test run with the local qwen model loaded (owner's reading: 28.4 GB committed, 6.5 GB compressed, 1.8 GB swap already used). Budget for the whole test tree = physical − baseline sampled by the harness at start − 4 GB margin ≈ 6 GB at that reading. Memory, not cores, is the binding constraint. Rules:
  - every cell is measured with qwen loaded (the condition every gate runs in); the harness records the baseline it sampled and runs with `--threshold` = the budget, so an over-budget cell kills itself instead of swapping;
  - each cell records TWO peaks — the testhost's own RSS and tree RSS (testhost + spawned children) — plus swap used before/after;
  - the N=1 cell on today's tree is itself checked against the budget: CLAUDE.md's "5–10 GB per run" says the current serial gate may already exceed it. If it does, the first finding is that, and the heap-limit axis below comes before any thread is added;
  - heap-limit axis: at the best N, re-run with `System.GC.HeapHardLimit` at 3, 4 and 5 GB. The limit is shared by all threads in the testhost, so lowering it trades GC time for RSS; choose the lowest value with zero `OutOfMemoryException` reds and no wall-clock loss over ~5%. The limit is never RAISED to cure out-of-memory reds — N is lowered;
  - the chosen N is the largest with zero out-of-memory reds, testhost peak ≤ 80% of the limit, tree peak inside the budget, and no growth in swap used across the run.

#### Tasks
1. **Thread-count axis for the memory harness** — `.claude/scripts/test-memory-harness`
   - Add `--threads N`, passed to the run as the xUnit RunSettings override (`-- xUnit.MaxParallelThreads=N`); verify by a run at N=2 that the override is honoured over `xunit.runner.json` (two tests observed overlapping in the verbose log). If the adapter ignores it, fall back to an environment variable read by an MSBuild step that rewrites the copied `xunit.runner.json` in `bin/` — state which route was taken.
   - Print a final summary line: `threads=N peak_rss_mb=… wall_s=… passed=… failed=… skipped=…`.
   - Commit: `chore(test): thread-count axis and summary line for test-memory-harness (refs #2179)`
2. **Per-call stopwatch breakdown** — `src/Sharpy.TestInfrastructure/Integration/IntegrationTestBase.cs`
   - Behind an environment switch (`SHARPY_TEST_TIMING=<path>`), record the six components per call in all three arms and append aggregate rows at process exit. Zero overhead and zero behaviour change when unset.
   - Commit: `chore(test): opt-in per-call cost breakdown in IntegrationTestBase (refs #2180)`
3. **Run the matrix and record it** — no committed source change
   - Harness at 1/2/4/8 on `FullyQualifiedName~FileBasedIntegrationTests` and on the whole Compiler.Tests main filter; breakdown over the fixture suite. Each cell is one serialized run; do them back to back with nothing else holding the lock, and note the machine state.
   - [ADDED: missing step] Table (b), "a representative un-collected subset", cannot be measured on the committed tree: the classes outside `HeavyCompilation` today are the light ones (Lexer, Parser), and every `CompileAndExecute` class is inside it. Measure (b) in a throwaway worktree under `sharpy.worktrees/` with the attribute stripped from a named set of Integration/Semantic matrix classes (name them in the table); nothing from that worktree is committed.
   - [ADDED: missing step] Cost: the whole-Compiler.Tests filter at four thread counts is four runs of more than an hour each at today's speed. The N=1 cell IS the Phase 0 control (do not re-run it); if (a) shows no change from N=1 to N=2 on the current collections (expected — one serial collection), record that and skip N=4/8 for (a), spending the runs on (b).
   - Post both tables as comments on #2179 and #2180 (lead posts).

### Phase 2: isolate the process-global users (#2179 step 4)

**Goal:** every test that touches process-global state is in a named serial collection or no longer touches it, before any cap moves.

**Acceptance:**
- An audit table in the commit body: global {current directory, `Console.Out/Error`, environment variables, culture/time zone, fixed repo paths (`.claude/tmp/*-report.json`, snapshot writes), fixed temp names, sockets/ports, static caches with mutation} × the classes that use it × the isolation chosen.
- A conformance test fails when a test class calls `Directory.SetCurrentDirectory`, `Console.SetOut/SetError`, `Environment.SetEnvironmentVariable`, or `CultureInfo.Default*` setters outside an allow-listed serial collection (source scan over the test projects). Mutation: remove `ModuleResolverTests` from its collection → red; restore → green. Positive control: the scan finds the known `SetCurrentDirectory` sites.
- [ADDED: adequacy] The scan's token list also carries the product entry points that mutate a process global when called in-process — a literal roster (`os.Chdir`, `os.Putenv`/environ writes, `CapturedOutput`, `CapturedStderr`, `ReplSession`, `CompileServer`) — and it scans `Spy/generated/*.cs`. A scan limited to the four BCL calls passes while `OsModuleTestsModuleTests` changes the working directory under every concurrently running Stdlib test. Second mutation: remove the `os_module_tests` class from its serial collection → red. The roster's own completeness is checked once in the audit by grepping `src/Sharpy.Core`, `src/Sharpy.Stdlib`, `src/Sharpy.Compiler`, `src/Sharpy.Cli` for the four BCL setters and listing every hit as rostered or justified.
- Counts identical to Phase 0.

#### Tasks
1. **Audit** — grep + read; produce the table. Include Stdlib.Tests (spy-generated tests: sockets, `os.chdir`, tmp paths, time zone) and Cli.Tests/Lsp.Tests (already parallel by default — record whether they have latent collisions).
2. **Named collections** — `TestCollections.cs` and the classes found
   - [ADDED: missing step] spy-generated classes are generated C# and are never hand-edited: put one in a collection with a hand-written `partial class` twin in Stdlib.Tests carrying the `[Collection(...)]` attribute (the generated classes are `public partial`, e.g. `OsModuleTestsModuleTests`); no generator or compiler change, so "no product code" holds and `check_spy_tests_staleness.sh` stays green. `ModuleResolverTests` is in no collection today (it is not in `HeavyCompilation`) — it only runs alone because the cap is 1.
   - e.g. `ProcessCwd`, `ConsoleCapture` (reuse the Stdlib one), `RepoReportWriters`; CsCheck property classes stay serial with their current iteration counts (#2179's instruction) in their own `PropertySerial` collection.
   - Commit: `test: isolate process-global test state in named serial collections (refs #2179)`
3. **The scan guard** — new conformance test beside `CiFilterCoverageConformanceTests`
   - Commit: `test: guard process-global mutation outside serial collections (refs #2179)`

### Phase 3: remove the per-call tax the breakdown attributes (#2180)

**Goal:** apply Design Decision 5 to all three arms through ONE shared execute helper, so the arms cannot drift again.

**Acceptance:**
- The copy+spawn block exists once (a private `ExecuteAssembly(...)` used by the single-file arm and by `EmitAndRunProjectAssembly`, which the project and entry-file arms already share — two blocks fold into one, not three [CORRECTED]); a source-shape test or simple grep guard asserts one `new ProcessStartInfo` in the file. Mutation: re-inline one arm → red.
- `FileBasedIntegrationTests` per-test median before/after, `ms/test @ sha (measured)`, from the same instrument as Phase 1.
- Counts identical to Phase 0; a fixture that uses a native asset (sqlite) and one with additional reference assemblies both pass (name them in the commit).
- If stage-once is taken: the staging dir is created once per process, is safe under concurrent first use (`Lazy<T>` with `ExecutionAndPublication`), and is deleted at process exit; a test asserts a per-test dir contains no `Sharpy.Core.dll` (positive control: it contains the test assembly).

#### Tasks
1. **Extract the single execute helper** (no behaviour change; counts identical). Fold the block in `CompileAndExecuteCore` (l.335–469) and the one in `EmitAndRunProjectAssembly` (~l.940–1060) into one; diff the two blocks first and list every difference (environment, timeout handling, additional references, cleanup) in the commit body — a difference is either preserved by a parameter or recorded as a deliberate unification. [CORRECTED]
   - Commit: `refactor(test): one execute path for the three IntegrationTestBase arms (refs #2180)`
2. **Probing spike, then stage once** — only if copies + delete ≥ 10% in Phase 1.
   - Commit: `perf(test): stage the runtime closure once per test process (refs #2180)`
3. **Forced GC** — remove per-call GC from the three arms per the decision rule; re-run the harness at the Phase 1 thread counts and record RSS with and without.
   - Commit: `perf(test): drop the per-call forced Gen2 GC (refs #2180)`
4. **Spawn comment** — replace the uncited Linux-x64 sentence with what is known (measured split; in-process deferred to an owner ruling).
   - Rides in Task 3's commit.

### Phase 4: shard the collections (#2179 steps 2–3)

**Goal:** the independent mass can actually run concurrently.

**Acceptance:**
- `HeavyCompilation` no longer exists, or holds only classes the Phase 2 audit lists with a reason. A guard fails when a class joins a `DisableParallelization` collection that is not in the audited roster (anchored to a literal list). Mutation: add the attribute to an un-rostered class → red.
- Fixture partitions: totality test (union of the K partitions' rows = `DiscoverTestFixtures`, no overlap, none empty), for Compiler and Stdlib. Mutation: change the modulus in one partition → red.
- `UPDATE_SNAPSHOTS=true`, `.skip`, `.features`, `.warning`, multi-file fixtures behave as before (run the snapshot regeneration once: zero diffs).
- CI shard filters updated; `compiler-shard-partition` logic and `CiFilterCoverageConformanceTests` green; every doc/skill that names the class still selects all rows (`--list-tests` count equals the pre-change row count).
- Counts identical to Phase 0 (the row total is unchanged; only class names differ).

#### Tasks
1. **Dissolve `HeavyCompilation`** by directory, one commit per directory group (Semantic, Integration, Properties, CodeGen+Conformance, rest), each followed by a filtered run of that namespace at the Phase 1 thread counts.
   - Commit: `test: drop the blanket HeavyCompilation collection from <area> (refs #2179)`
2. **Partition the fixture Theory** — `FileBasedIntegrationTestsBase.cs`, both `FileBasedIntegrationTests.cs`, `.github/workflows/dotnet10.yml`, skills/docs naming the class.
   - Commit: `test: partition FileBasedIntegrationTests into K hash shards (refs #2179)`
3. **Roster guard + totality tests.**
   - Commit: `test: guard serial-collection roster and fixture-shard totality (refs #2179)`

### Phase 5: raise the cap, prove order-independence, re-measure (#2179 step 5)

**Goal:** the chosen degree of parallelism, with the evidence that chose it.

**Acceptance:**
- `maxParallelThreads` in both runner jsons set to the value whose Phase-1-style re-measurement (after Phases 2–4) gives the best wall clock with peak tree RSS inside the recorded budget; the table (threads × RSS × wall × counts) is in the commit body. The heap limit is re-tuned only if a cell hits it, with the cell named.
- Three consecutive full runs at the chosen setting: identical counts, zero reds. Plus one run with collection order perturbed (a different K, or a reversed `ITestCollectionOrderer` behind an env switch) agreeing with them.
- One CI run on a PR against mainline (the R-EF pattern): per-job counts equal the last mainline run's; job durations recorded before/after.
- `dotnet-serialized` watchdog: a full Compiler.Tests run completes under the wrapper without tripping the silence window; adjust the documented logger advice if cadence changed.
- CLAUDE.md's quoted figures (`~22 min wall clock`, "5–10 GB RAM per run") updated to the measured ones — rules text otherwise untouched. [ADDED: missing step] The same `~22 min` figure is quoted in `docs/design/verification-contract.md` §6 (l.154) and `.claude/skills/implement-plan/SKILL.md` (l.126); `grep -rn "22 min" CLAUDE.md docs .claude/skills` must return only measured figures after the commit.
- Close-out comments on #2179 and #2180 with before/after wall clock (local and CI), per-test median, RSS, and — if process start still exceeds half the per-test median — the question for the owner on in-process execution.

#### Tasks
1. **Re-measure and set the cap.** Commit: `perf(test): raise maxParallelThreads to the measured budget (refs #2179)`
2. **Order-independence runs** (no commit unless a test is fixed; a product-side cause goes to the ledger).
3. **CI PR run and docs.** Commit: `docs: record measured gate time and memory budget (refs #2179)`

## Testing Strategy

- The plan's "tests" are (a) count identity against the Phase 0 control at every step, (b) the standing instruments (harness `--threads`, timing switch), and (c) four guards: process-global scan, serial-collection roster, fixture-shard totality, single-execute-path. Each guard has its mutation named above.
- Absence assertions and their positive controls: "per-test dir has no Core dll" ↔ "has the test assembly"; "no un-rostered serial collection" ↔ "the rostered ones are found"; "no global mutation outside a serial collection" ↔ "the known sites are detected".
- Discriminating outputs: wall clock alone can improve by dropping tests; every timing is reported WITH counts. RSS is tree RSS (testhost + children), sampled by the harness, never derived.
- Flake policy: a red that appears only under concurrency is attributed by a re-run at `--threads 1` of that class; a test-side cause is fixed in Phase 2's terms (isolate or remove the global), a product-side cause goes to the findings ledger.
- Blast radius: full solution gate once, at the end, at the chosen setting (that run is also one of the three Phase 5 runs); `check_spy_tests_staleness.sh` and `check_spy_staleness.sh` as controls; no product code changes, so the GapDiscovery sweeps are run for counts only.

## Issues to Close

- #2179 — restore measured parallelism — closed by Phase 5 Task 3; **close criterion:** threads × RSS × wall × counts table @ sha, three identical full runs, CI PR run with equal counts, guards green with mutations recorded.
- #2180 — per-test process/file-copy/GC tax — closed by Phase 3 (and Phase 5's re-measure); **close criterion:** per-component breakdown before/after @ sha, `FileBasedIntegrationTests` per-test median before/after, single execute path guard. If process start remains dominant, #2180 closes with the measured split and the in-process question is put to the owner at the next `/batch-issues` rather than left implicit.

## Class trackers this plan may post cells to

- #2179 — test-suite throughput (class tracker) — any further throughput cell (a slow single test, a sweep that should move to its own CI job).
- Product defects exposed by concurrency or ordering go to the findings ledger; the lead files or routes them by class. No implementer runs `gh issue create`.

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Adequacy:** CLASS
**Verified on:** 2026-10-04 @ 9af04978b
**Plan file:** .claude/plans/plan-5c21c8.md

### Corrections Made
- `HeavyCompilation` roster: "212 Compiler.Tests classes + 1 Stdlib" → 215 + 1 files @ 9af04978b, with the per-directory breakdown re-measured (Semantic 69, Integration 48, Project 8, four single-class directories).
- Execute arms: "three arms … (for two of them) its own copy-and-spawn block", second block attributed to `CompileAndExecuteProjectCore` → two blocks; the project and entry-file arms already share `EmitAndRunProjectAssembly` (l.866). Phase 3 acceptance and Task 1 reworded to "two blocks fold into one".
- Context: #2177 added to the closed dependencies (both issues order P41 after it).
- Process-global users: `CliTestHarness` added as a `ConsoleLock` user; `Environment.SetEnvironmentVariable` sites added; the indirect in-process mutators added (see Adequacy).

### Adequacy
- **Contract:** named — "the suite's degree of parallelism is a measured memory budget, and the per-program cost of `CompileAndExecute` is the program's own compile and run."
- **Matrix:** larger than the issues' lists after correction. The verifier added one axis the plan's guard did not reach: process-global mutation through product code called in-process (`os.Chdir`/`os.putenv` in spy-generated tests, `CapturedOutput`, `ReplSession`, `CompileServer`), and the 119 spy-generated Stdlib classes that are already separate collections. Tried: grep of the four BCL setters over `src/` (hits in `Sharpy.Stdlib/Os/Os.cs:167`, `Unittest/CapturedOutput.cs`, `Compiler/Services/ReplSession.cs`, `Cli/Services/CompileServer.cs:286`) and of their callers in the test projects.
- **Cure:** harness + seam — measure first (standing instrument), one execute helper, roster/totality/scan guards. Meta-class: historical constant standing in for a measurement.
- **Harness:** named — `test-memory-harness` gains `--threads` in Phase 1 (confirmed: no thread parameter today; options are `--threshold`, `--filter`, `--project`). No allowlist delta.
- **Layer:** N/A for the language ladder (test infrastructure). The one place a product change could have crept in — putting generated spy-test classes into a collection — is resolved with a hand-written `partial` twin, no generator change.

### Scope & convergence
- **Group:** one (P41) · **Alone:** yes — required: yes (charter §3.37, R-DM) · **Gates:** 1
- **Generator:** not required: both axes (thread count × observation; cost component) are measurements enumerated in full by the harness, not program shapes.
- **Trackers named:** #2179 (class tracker; R-DM comment of 2026-10-03 read) · **Fidelity rows:** N/A (no Python surface)

### Falsifiability
- Timing instrument (Phase 1): mutation named (switch OFF → counts and output unchanged; ON → component sums within 5% of wall time); exemption not subject; positive control yes; by-direction n/a.
- Process-global scan (Phase 2): mutation named (remove `ModuleResolverTests` from its collection; second mutation added for the generated `os_module_tests` class); exemption not subject (the allow-list is the serial-collection roster, the subject is the call site); positive control yes (known `SetCurrentDirectory` sites). Before correction the scan was vacuous for indirect mutators.
- Single execute path (Phase 3): mutation named (re-inline one arm → red). Note the guard must count `new ProcessStartInfo`, not `ProcessStartInfo` — l.92 names the type in a comment.
- Stage-once absence assertion (Phase 3): positive control yes (per-test dir contains the test assembly).
- Serial-collection roster guard (Phase 4): mutation named; anchored to a literal list.
- Fixture-shard totality (Phase 4): mutation named (change one modulus → red); "none empty" is the control against a shard that selects nothing.
- Count identity vs the Phase 0 control: discriminating (a dropped shard changes counts).

### Warnings
- **Stdlib.Tests is the higher concurrency risk, not Compiler.Tests.** Only one Stdlib class is in `HeavyCompilation`; the generated classes run serially today only because the cap is 1. Raise the Stdlib cap in its own commit after the Compiler one, so a red is attributable.
- **`HeapHardLimit` is per testhost, not per thread.** At N threads the 5 GB managed-heap limit is shared; the first symptom of too many threads is `OutOfMemoryException` reds inside the testhost, not tree RSS over budget. Phase 1's table should record the testhost's own peak beside tree RSS so the two are not confused.
- **K partition classes each call `DiscoverTestFixtures`** (a disk walk) at discovery and again at execution; with K classes that is K walks. Cache the discovery result in a static `Lazy` in the base, or the partitioning adds discovery time it then has to win back.
- **Name collision (harmless, but grep-confusing):** `ExecuteAssembly` already exists in `Compiler.Tests/CodeGen/LineDirectiveRuntimeTests.cs:74`. Pick another name for the shared helper or the single-path guard's grep will need a file scope.
- **Other spawn sites are outside the contract as worded** (`DifferentialExecutionTests` ×2, `FormatSpecDifferentialTests` ×2, `CPythonDifferentialParseTests` ×2, `LineDirectiveRuntimeTests`, eight Cli.Tests E2E classes). They do not go through `CompileAndExecute`. If Phase 1's per-class durations show one of them is material, it is a cell for the #2179 tracker, not for this plan.
- **Line numbers** in Current State were read @ 21ca52b5d and re-checked @ 9af04978b for `IntegrationTestBase.cs` (l.161, 185, 190, 333–334, 547, 552, 687, 692, 1096 hold). Re-read before editing; the chores in flight do not touch this file.

### Missing Steps Added
- Verify cost and prober budget line in `## Scope`.
- Phase 1 Task 3: how table (b) is measured (throwaway worktree), and the rule that trims redundant whole-suite cells.
- Phase 2: indirect-mutator roster in the scan guard, with its second mutation; the `partial`-twin mechanism for generated classes.
- Phase 3 Task 1: diff the two spawn blocks and account for every difference in the commit body.
- Phase 5: the `~22 min` figure in `verification-contract.md` §6 and the implement-plan skill.

### Unchecked Claims
- `-- xUnit.MaxParallelThreads=N` overrides `xunit.runner.json` under `xunit.runner.visualstudio` 4.0.0 — not run (the dotnet lock is held by the chores round). Phase 1 Task 1 already carries the overlap probe and a fallback; that probe is the verification.
- `maxParallelThreads` accepts a multiplier string (`"0.5x"`) in xUnit 2.9.3's runner json (Design Decision 7) — not run. Verify with one `--list-tests`-free filtered run before relying on it in Phase 5.
- "The memory reason for the blanket collection was removed by `SharedReferences`/`SharedBaseCompilation`" — both exist (`IntegrationTestBase.cs:40`); whether they removed the cause is exactly what Phase 1 measures. The filer's timings (28 min band, 151 ms/test) are the filer's, as the plan says.
- All timings and the "about a dozen runs" cost are derived, not measured by this verification.
