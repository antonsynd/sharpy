# Test Harness Hardening — Design Doc Index

> **Status:** Draft designs — 2026-07-02
> **Origin:** [../test-harness-hardening-proposal.md](../test-harness-hardening-proposal.md) (infrastructure audit + sketches) and [../fable5-test-harness-prompt.md](../fable5-test-harness-prompt.md) (design brief). This directory is the response to that brief: one design doc per subsystem, with the shared decisions collected here.

## Doc map

| # | Doc | Subsystem | One-line summary |
|---|-----|-----------|------------------|
| 1 | [01-code-coverage.md](01-code-coverage.md) | Code coverage | coverlet + ReportGenerator + `harness coverage gate`; two profiles (gate/full); the subprocess blind spot and how gates route around it |
| 2 | [02-differential-oracle.md](02-differential-oracle.md) | Differential oracle | Rule-based AST-level Spy→Python transpiler (closed whitelist), three-way Sharpy/Python/`.expected` comparison, consumes the **existing** `docs/deviations.yaml` |
| 3 | [03-tdd-scaffolding.md](03-tdd-scaffolding.md) | TDD scaffolding | Markdig spec reader over `docs/language_specification/` conventions; per-layer skeleton generators; expected values only from spec or `/verify-python`, never invented |
| 4 | [04-mutation-testing.md](04-mutation-testing.md) | Mutation testing | Stryker.NET, three per-target configs, component rotation + `--since` PR mode; our gate (not Stryker's `break`) enforces per-component thresholds |
| 5 | [05-coverage-guided-fuzzing.md](05-coverage-guided-fuzzing.md) | Fuzzing | SharpFuzz/libfuzzer-dotnet in CI, dumb mode on macOS; five staged targets; crash→issue→fixture pipeline reusing existing fixture machinery |
| 6 | [06-visual-lsp-regression.md](06-visual-lsp-regression.md) | Visual LSP regression | Playwright-driven playground (VS Code Phase 2), pixel-diff-then-vision-LLM composite verifier, platform-keyed baselines, hard cost caps |

Read order for review: this page, then 01 → 06 (they are ordered by rollout priority and later docs assume the shared pieces earlier ones introduce).

## Shared architecture

Three new projects join `sharpy.sln` (all `net10.0`, `LangVersion latest`, `Nullable enable` — none is consumed by `Sharpy.Core`, so the `netstandard2.1`/C# 9.0 floor does **not** apply):

| Project | Kind | Contents |
|---------|------|----------|
| `src/Sharpy.TestHarness` | Console app (System.CommandLine, same stack as `Sharpy.Cli`) + library | `Configuration/`, `Reporting/`, `Coverage/`, `Oracle/`, `Mutation/`, `Fuzzing/` (triage side), `Scaffold/`, `Visual/` |
| `src/Sharpy.TestHarness.Tests` | xUnit | Tests for the harness itself (each subsystem doc has a test plan) |
| `src/Sharpy.Fuzz` | Console app | Fuzz targets + corpus; never runs under `dotnet test` |

One CLI, one verb family per subsystem:

```
harness coverage gate | mutation plan/gate/suggest | oracle run/explain/baseline
harness fuzz run/repro/triage/promote/seed-corpus/dict | scaffold generate/status/checklist
harness visual approve | report merge/comment
```

A local tool manifest `.config/dotnet-tools.json` (new) pins `dotnet-reportgenerator-globaltool`, `dotnet-stryker`, and `sharpfuzz`.

### Unified configuration

`sharpy-test-harness.json` at the repo root; one section per subsystem (full schemas live in each doc):

```jsonc
{
  "$schema": "docs/schemas/sharpy-test-harness.schema.json",
  "coverage":  { /* 01: thresholds, maxDeltaDrop, gateProfile */ },
  "oracle":    { /* 02: pythonPath, timeout, deviationCatalog, baseline */ },
  "scaffold":  { /* 03: specRoot, grammarFile, skipAttributeFormat */ },
  "mutation":  { /* 04: targets, componentThresholds, rotation, prMode */ },
  "fuzzing":   { /* 05: targets, corpusRoot, per-run seconds, limits */ },
  "visual":    { /* 06: driver, verifiers, confidence, budget, cache */ },
  "reporting": { "artifactDirectory": "artifacts", "prComment": true },
  "budgets":   { "visionCallsPerRun": 50, "fuzzCpuMinutesNightly": 180, "mutationCpuHoursWeekly": 18 }
}
```

Loaded by `HarnessConfig.Load()` (strict: unknown keys are errors); env-var overrides via `SHARPY_HARNESS__<section>__<key>` for CI tweaks without file edits. `Sharpy.TestHarness.Tests` includes a schema-conformance test so config drift breaks a unit test, not a nightly job.

### Unified reporting

Every subsystem run emits one `SubsystemReport` JSON to `artifacts/<subsystem>/report.json`:

```jsonc
{
  "schemaVersion": 1,
  "subsystem": "coverage",             // coverage|oracle|scaffold|mutation|fuzzing|visual
  "status": "pass",                    // pass|warn|fail|partial (budget/time-capped)
  "gitSha": "…", "branch": "…", "trigger": "pr|nightly|weekly|local",
  "metrics": { /* subsystem-specific flat key→number map, trended over time */ },
  "violations": [ "…" ],
  "artifacts": [ "artifacts/coverage/index.html" ],
  "cost": { "cpuMinutes": 12.5, "visionCalls": 0 }
}
```

- `harness report merge` combines whatever reports exist into one summary; `harness report comment` upserts a single sticky PR comment (marker-comment + `gh api`, no third-party actions — the repo's CI uses only `actions/*`).
- **Weekly health report:** the weekly workflow merges the week's nightly/weekly reports into a trend table (coverage %, mutation score per component, oracle verified %, fuzz corpus edges + open crash buckets, visual flake rate, scaffold skeleton-rot count) and publishes it as a `$GITHUB_STEP_SUMMARY` + artifact — same publication pattern as the existing cross-language benchmark summary.
- Trend storage follows the existing precedent (`benchmarks/cross-language/results/history.json` committed by CI): `test-harness/history/` on `dev`, appended by the nightly job, capped at 52 weeks.

### Cost management

Budgets live in the `budgets` config section; each runner checks before spending (vision calls, fuzz wall-clock, mutation scope) and **fails soft**: exceeding a budget converts remaining work to `partial`/`NeedsHuman` status in the report — visible, never silent, and never an unbounded bill. Per-run `cost` blocks aggregate into the weekly report so drift is caught in review, not on an invoice.

### Trait taxonomy & CI filter policy

New xUnit categories introduced by these subsystems, and who runs them:

| Trait | Runs in `dotnet10.yml`? | Runs where |
|-------|------------------------|-----------|
| *(none — normal tests)* | ✅ | Everywhere |
| `Category=Property`, `RandomProperty`, `GapDiscovery` | ✅ today → moves to full-profile nightly for coverage purposes; still in PR runs | PR + nightly |
| `Category=Visual` | ❌ (excluded) | Visual job only (browser required) |
| `Category=FuzzSmoke` | ✅ (fast, deterministic) | Everywhere |
| `Category=Oracle` *(optional xUnit wrapper)* | ❌ | Harness CLI owns oracle execution |

Action item embedded in subsystem 1's rollout: the five `dotnet test` steps' filters change from `Category!=Benchmark` to the explicit exclusion list, and the existing "verify all test projects are covered" guard step gains `Sharpy.TestHarness.Tests` in its known list.

### CI topology

| Workflow | Cadence | Jobs |
|----------|---------|------|
| `dotnet10.yml` (existing, modified) | PR/push | + coverage collection flags, merge, gate, artifacts, baseline upload |
| `test-harness-pr.yml` (new) | PR, path-filtered | `oracle-pr` (changed fixtures / full on compiler change), `fuzz-pr` (2 min × 5 targets), `mutation-pr` (`--since`, capped), `visual-regression` (on LSP/playground paths) |
| `test-harness-nightly.yml` (new) | cron `0 4 * * *` | oracle full + baseline commit, fuzz 30 min/target + corpus merge + triage, visual full run, full-profile coverage |
| `test-harness-weekly.yml` (new) | cron `0 3 * * 6` | mutation rotation matrix + gate, weekly health report, scaffold skeleton-rot check |

All `ubuntu-latest` (visual: pinned Playwright container); every job gets `timeout-minutes` (a first for this repo — the harness must never wedge CI).

### Cross-subsystem data flows

```
 coverage (1) ──per-file rates──▶ mutation plan (4)     "high coverage + low score" = real gap
 coverage (1) ──per-fixture hits─▶ fuzz seed-corpus (5)  greedy set-cover fixture selection
 fuzz crashes (5) ──promote──▶ TestFixtures ──▶ oracle universe (2) + seed corpus (5)
 scaffold (3) ──fixtures──▶ oracle universe (2);  mutation survivors (4) ──▶ scaffold-hosted killing tests
 all (1–6) ──SubsystemReport──▶ report merge ──▶ PR comment + weekly health report
```

## Migration plan (non-disruption by construction)

Nothing existing moves or is renamed; every subsystem is additive, opt-in, and starts non-blocking:

| Stage | Weeks | Lands | Blocking? |
|-------|-------|-------|-----------|
| 1 | 1–2 | Coverage (01) + harness/report/tool-manifest skeleton | Gate warn-only 2 weeks, then enforcing |
| 2 | 3–5 | Oracle (02); first divergence triage | PR gate enforcing after baseline triage hits zero `Divergent` |
| 3 | 5–6 | Scaffolding (03) | Never blocking (dev tool + weekly rot report) |
| 4 | 6–9 | Mutation (04) | Warn-only for two rotations, then PR-changed-files gate |
| 5 | 8–10 | Fuzzing (05) | PR job blocks only on *new* crashes (previously-unseen buckets) |
| 6 | 10–14 | Visual (06) | Nightly-only until <2% flake over two weeks; then path-filtered PR job |

Rollback story per stage: each is a workflow file + config section; disabling = removing the job (or `status: warn` in config). No existing test is modified by any stage; the only edits to `dotnet10.yml` are the coverage flags and filter strings.

## Developer experience walkthrough

Implementing a new language feature (say, spec page `assignment_expressions.md`, issue #123) with all six subsystems live:

1. `/scaffold-tests walrus operator --issue 123` → skeletons across 6 layers + `CHECKLIST.md`; integration fixtures carry `.skip` files, tests carry `Skip = "TODO(#123): …"`. Nothing is red; the checklist is the plan.
2. Implement lexer → parser → semantic → codegen (the usual order), un-skipping each layer's tests as they pass. `.expected` values marked `TODO` get filled from the spec or `/verify-python` — never guessed.
3. On PR: coverage gate confirms the new code is exercised (new-lines-uncovered shows in the sticky comment); the oracle runs the new fixtures against Python and one comes back `Divergent` — the `.expected` had a wrong newline; the three-way verdict says Python agrees with Sharpy, so the fixture is fixed, not the compiler. Mutation PR mode mutates the new files: two survivors in the narrowing logic → two extra semantic tests kill them.
4. `UPDATE_SNAPSHOTS=true` adds the codegen snapshot last; `/visual-review` confirms hover shows the inferred type at the walrus binding; the checklist is fully checked and pasted into the PR body.
5. That night, the fuzzer inherits the new fixtures into its corpus and hammers the new parse path for 30 minutes; a week later the weekly health report shows the feature's component scores unchanged — the feature landed *with* its safety net, not ahead of it.

## Constraints honored (from the brief)

.NET 10 / C# 14 for all new code; no Core consumption so no C# 9.0 constraint applies (called out explicitly in Shared Architecture); xUnit + CsCheck only; GitHub Actions only, `actions/*` + pinned containers, no third-party marketplace actions; macOS-local / Linux-CI split addressed per subsystem (fuzz dumb mode, platform-keyed visual baselines, everything else platform-neutral); vision backend fully abstract (`IVisionBackend`); scaffolded tests follow existing `src/Sharpy.Compiler.Tests` conventions; oracle transpiler rule-based by contract (closed whitelist, determinism tests).

Known deltas from the original proposal, found during design: `docs/deviations.yaml` already exists (oracle consumes rather than creates it); there are 8 metamorphic transforms, not 10; integration tests execute in a subprocess, which reshapes the coverage design (blind spot) and the mutation design (Core scores measure the unit suite); `Microsoft.Testing.Extensions.Fuzz` does not exist, settling the fuzz framework question in SharpFuzz's favor; `grammar.ebnf.txt` is a neglected initial-development artifact that understates the implemented grammar, so the scaffolder (03) treats it as hints only — the parser source is authoritative.
