# Roadmap 2026-07 — Tasks from the Strategic Review

> **Date:** 2026-07-09
> **Source:** [Strategic Review 2026-07-09](../audits/strategic-review-2026-07-09.md).
> Section references (§) below point into that document.
> **Status legend:** each task should become a GitHub issue (or link an existing one)
> before work starts, per the project's TODO/issue rule.

Five workstreams, three phases. Phases are ordered by leverage: Phase 1 items are
prerequisites or force-multipliers for Phase 2; Phase 3 is gated on measured results
from Phase 2.

---

## Workstream A — Correctness mechanisms (§1)

Turn the five recurring bug classes into machine-checked invariants.

| ID | Task | Addresses | Done when |
|----|------|-----------|-----------|
| A1 | **Generated CLR interop conformance suite.** Reflect over every public member of every stdlib assembly, generate a `.spy` snippet per member/usage-position (call, index, annotate, match, subclass), compile all in CI. Extend `/gap-analysis` from curated to exhaustive. | §1.1; the ~40% interop bug class (#880–#891, #912–#922, #940–#959, #965–#977, #824–#833) | CI fails the day a bridge regression makes any public stdlib member unusable from `.spy`. |
| A2 | **"No CS leaks" invariant.** (a) Property test: semantic-clean programs produce zero CS diagnostics; (b) last-chance handler mapping escaped CS errors to one SPY internal-error code with AST provenance; (c) corpus of all historical leaks. | §1.2 (#980, #917, #912, #902, #900, #873, #867, #866, #960, #961, #949, #921, #846, #816) | Users can never see a raw CS error; property test runs in the property-stress harness. |
| A3 | **Determinism harness.** Compile every multi-file fixture twice with shuffled file order; diff diagnostics and emitted C# byte-for-byte. Add an analyzer banning static mutable state in compiler projects. | §1.5 (#1032, #895, #792, #1017, #1031, #1033, #630) | Shuffled-order double-compile is a CI gate; analyzer enforced via TreatWarningsAsErrors. |
| A4 | **CPython golden oracle.** Execute existing issue #1030: port a curated CPython test subset as behavioral-parity tests. | §1.6 (#1030; parity bugs #896, #905, #906) | First curated tranche (str/list/dict/int semantics) running in CI with a documented porting recipe. |
| A5 | **Grammar fuzzing on ambiguity hotspots.** Extend the unparse round-trip property test with grammar-directed fuzzing of lambda bodies, subscripts, and tuples in argument position; differential-parse the shared subset against CPython `ast`. | §1.7 (#1015, #1011, #899, #888, #872, #847, #870, #1001) | Hotspot fuzzers in the property suite; each historical parser bug reproduced by a generator, not just a fixture. |

## Workstream B — Architecture simplification (§3)

| ID | Task | Addresses | Done when |
|----|------|-----------|-----------|
| B1 | **Unify run mode and project mode.** Make `run` a degenerate project-of-one-file; delete the second pipeline. | §3.1 (#940, #814, #862) | One code path from source to assembly; mode-divergence fixtures pass identically in both CLI commands. |
| B2 | **Emitter stops reasoning about types.** Move every type decision the emitter makes into semantic analysis, materialized via `SemanticInfo`/`CodeGenInfo`; emitter becomes a pure translator. Precondition for Workstream E. | §3.2 (#973, #974, #972 pattern) | No calls to inference services or reflection from `RoslynEmitter*`; grep-able rule documented in copilot-instructions. |
| B3 | **Single bidirectional name-mapping authority.** Consolidate all casing/mangling into `NameMangler`, add round-trip property tests both directions. | §3.3 (#942, #863, #861, #820, #823, #891, #897, #898) | One mangling call site per direction; round-trip tests in the property suite. |
| B4 | **Consolidate the type bridge.** Merge `ClrTypeMapper`, `TypeSyntaxMapper`, and `OverloadIndex` responsibilities into one owned `Discovery`/bridge component; attach the A1 conformance suite to it. | §3.4 | The CLR↔Sharpy boundary has a single component, a single test suite, and a named owner in the agent registry. |
| B5 | **CFG-based type narrowing.** Replace syntactic narrowing special cases with dataflow facts propagated over the existing `Analysis/ControlFlow` CFG. | §1.3 (#979, #978, #854, #817, #882, #848) | All historical narrowing fixtures pass through the single dataflow engine; new narrowing forms need facts, not new syntax cases. |
| B6 | **Formal overload resolution spec.** Write betterness rules into `docs/language_specification/`, make the implementation table-driven, add order-independence and determinism property tests. | §1.4 (#975, #1002, #1003, #965, #966, #954, #833, #828, #890, #810–#814) | Spec page merged; shuffled-candidate property test green; #975 closed by construction. |

## Workstream C — Feature gating (§2)

| ID | Task | Addresses | Done when |
|----|------|-----------|-----------|
| C1 | **FeatureFlags infrastructure.** `FeatureFlags` record threaded through `CompilerServices`; `--enable-feature=<name>` CLI flag; `[features]` in `.spyproj`; optional `from __future__ import <name>` spelling. | §2 | A feature can be merged disabled-by-default and enabled per-project/per-file. |
| C2 | **Gated-syntax diagnostics.** Parser always recognizes gated syntax; disabled features produce "requires experimental feature X (enable with …)" instead of a generic parse error. New SPY diagnostic code for it. | §2 | Every gated construct has a helpful diagnostic fixture. |
| C3 | **Fixture feature declarations.** File-based tests can declare required features (e.g. a `.features` sidecar or header comment) so gated features get full integration coverage pre-graduation. | §2 | Test harness enables declared features per fixture. |
| C4 | **Graduation/deletion policy + drain the "Evaluate" backlog.** Document the lifecycle (experimental → stable | deleted). Implement the gate-friendly backlog behind flags: `defer` #1023, `@` matmul #989, `as?`/`as!` #1029 (with `TransitionWarningValidator` migration for `to`), free unions #992, refinement types #1021, units #1028, lazy imports #993, nominal aliases #1020, placement decorators #1027, structured parallelism #1026, typing PEPs #995–#997, const eval #640, property observers #416. | §2 | Each "Evaluate" issue is resolved to: shipped-behind-flag, graduated, or closed-won't-do. #1025 explicitly needs no gate (stdlib module). |

## Workstream D — Performance & observability (§3.5, §4.1–§4.2)

| ID | Task | Addresses | Done when |
|----|------|-----------|-----------|
| D1 | **Per-phase compile metrics.** Surface wall time + allocations per phase (startup, discovery, lex, parse, semantic, emit) in `--verbose` and in benchmark JSON. Do this **first** — it arbitrates every other perf claim. | §3.5, §4.1 | Cross-language benchmark report decomposes Sharpy compile time by phase. |
| D2 | **Compiler server / warm CLI.** Keep-alive compiler process (VBCSCompiler model) with cached `MetadataReference`s and `OverloadIndex`; and/or ReadyToRun / Native AOT publish of `sharpyc`. | §4.2(1)(3) | Warm compile of a small file < 200ms in the benchmark harness. |
| D3 | **Eliminate any text round-trip.** Audit the emit path; if trees are normalized to text and reparsed before `CSharpCompilation.Create`, feed `SyntaxTree`s directly. | §4.2(2); BASELINE.md "codegen ~60%" | Measured emit-phase time drop recorded in BASELINE.md. |
| D4 | **Sharpy.Core hot paths.** Struct enumerators, comparer devirtualization, `Span<T>` fast paths, allocation-free comprehension building. Targets: beat Python on list_comprehensions and sorting (currently 1.54x slower). | §4.1 | Spy/Py < 1.0 on all five cross-language benchmarks. |
| D5 | **Flat 2D arrays / NdArray completion.** Unblock the deferred NdArray work; lower numeric hot paths to raw arrays. Target: matrix_multiply from 4.0x to < 1.5x vs C#. | §4.1 (#955–#959, #968–#972) | Benchmark target met; deferred numpy files re-enabled. |
| D6 | **Benchmark the warm/incremental path.** Wire `--incremental` and the compiler server into the cross-language benchmark so published numbers reflect real usage. | §4.2(4) | latest.md reports cold and warm compile columns. |
| D7 | **Diagnostic provenance + internal-error policy.** Tag each diagnostic with its producing pass/validator; SPY0907-class crashes dump a minimal repro. | §3.5 (#1013-style bugs, #912) | Provenance visible in `--verbose` diagnostics and LSP; crash reports actionable without a live repro. |

## Workstream E — Lowering IR and the backend decision (§4.3)

Sequenced after B2 (emitter purity) and D1 (metrics), which it depends on.

| ID | Task | Addresses | Done when |
|----|------|-----------|-----------|
| E1 | **Design the lowering IR.** An `IOperation`-inspired Sharpy middle-end IR between semantic analysis and emission; design doc in `docs/design/` reviewed before implementation. | §4.3, §4.4 | Design doc merged with explicit non-goals and the backend seam identified. |
| E2 | **Port the emitter to consume IR.** RoslynEmitter translates IR → C# syntax; snapshot tests (`.expected.cs`) guard the migration. | §4.3 | All fixtures green with IR in the middle; emitter LOC decreases. |
| E3 | **First optimization passes on IR.** Const folding (#640), comprehension fusion/preallocation, devirtualization decisions, escape analysis for collection literals. | §4.1, §4.3 | Each pass individually toggleable (Workstream C flags) with benchmark deltas recorded. |
| E4 | **Backend decision gate.** Only after E3 plateaus: evaluate direct IL emission (`MetadataBuilder`) against explicit criteria — (a) semantics C# can't express efficiently, (b) sub-100ms cold compiles unreachable with a warm server, (c) self-hosting. Otherwise stay on Roslyn: it is the test oracle (§1.2), the `#line` debugging path (#609), and free interop verification. | §4.3 | A written go/no-go decision with measurements attached. Default expectation: **no-go**. |

---

## Phasing

**Phase 1 — now (v0.2.x).** The force-multipliers and the cheap wins:
D1 (metrics first), A2, A3, B3, D2, D3, A1, C1–C2.
*Rationale:* A2/A3 stop the two noisiest bug classes from regressing while other work
proceeds; D1–D3 resolve the "compilation is slow" complaint without touching
architecture; C1–C2 unblock merging any experimental feature work that's already
in flight.

**Phase 2 — next (v0.3).** The structural work:
B1, B2, B4, B5, B6, A4, A5, C3–C4, D4, D5, D6, D7.
*Rationale:* B2 must land before E; B5/B6 retire their bug classes permanently; D4/D5
are the actual fixes for the execution benchmarks; C4 drains the "Evaluate" backlog
behind the now-existing gates.

**Phase 3 — later (v0.4+).** The middle-end:
E1 → E2 → E3 → E4.
*Rationale:* optimization headroom lives in the IR, and the Roslyn-bypass question is
answered with data at E4 rather than speculation now.

**Ongoing, all phases:** the Roslyn borrowing list (§4.4) — lazy semantic binding for
LSP, pooling/allocation gates, determinism as contract, pull-based incremental
memoization, diagnostics-with-fixes.
