# Subsystem 4: Mutation Testing (Stryker.NET)

> **Status:** Draft design — 2026-07-02
> **Priority:** 4 of 6 (measures test *effectiveness*; the counter-metric to coverage)
> **Index:** [README.md](README.md) · **Proposal:** [../test-harness-hardening-proposal.md](../test-harness-hardening-proposal.md)

## Goal

Inject small faults (mutants) into `Sharpy.Compiler`, `Sharpy.Core`, and `Sharpy.Stdlib` and check whether tests catch them. Surviving mutants = concrete, actionable test gaps. Coverage (subsystem 1) says *what ran*; mutation score says *what would have been caught*.

## Current state (verified)

- No Stryker anywhere; greenfield.
- The full suite is ~9,600 tests / ~8 minutes, with integration tests that spawn `dotnet exec` subprocesses and are serialized via the `HeavyCompilation` xUnit collection — both facts dominate the feasibility math below.
- No `.config/dotnet-tools.json` exists yet; subsystem 1 creates it (ReportGenerator); Stryker joins it.

## Feasibility math (why the naive setup fails)

`Sharpy.Compiler` alone is ~50k+ lines; standard mutators would yield tens of thousands of mutants. At even 10 s of covering-tests per mutant that is days of CPU. The design therefore commits to four levers from day one:

1. **`coverage-analysis: perTest`** (Stryker default) — per mutant, run only the tests that cover it.
2. **Scoped `mutate` globs** — mutation targets are *directories on a rotation*, not whole projects (full-project runs are a quarterly event, not weekly).
3. **`--since` for PRs** — PR runs mutate only files changed vs `origin/mainline`.
4. **Test-set pruning** — the mutation run should exclude the slow random categories (`Property`, `RandomProperty`, `GapDiscovery`) and ideally the subprocess-spawning integration fixtures. **Open implementation check:** whether the installed Stryker.NET version exposes a VsTest test-case-filter option. If yes, use it (`Category!=Property&...`); if not, fall back to `perTest` coverage analysis (random tests rarely end up in a mutant's covering set for narrow mutants) plus a generous `additional-timeout`. This must be verified against the Stryker version at implementation time — do not assume the option exists.

## Architecture

```
 .config/dotnet-tools.json (dotnet-stryker, pinned)
        │
        ▼
 stryker/compiler.json ─┐                       ┌────────────────────────────┐
 stryker/core.json ─────┼──▶ dotnet stryker ───▶│ StrykerOutput/…/reports    │
 stryker/stdlib.json ───┘    (per target)       │  mutation-report.{html,json}│
        ▲                                       └──────────────┬─────────────┘
        │ generated/validated by                                │
 ┌──────┴──────────────┐                        ┌──────────────▼─────────────┐
 │ harness mutation     │                        │ harness mutation gate      │
 │ plan (rotation,      │                        │ (per-component thresholds, │
 │ coverage-informed)   │                        │  trend, suggestions)       │
 └─────────────────────┘                        └──────────────┬─────────────┘
                                                               │ SubsystemReport
                                                               ▼
                                          PR comment / weekly health report / artifact
```

Three Stryker config files (one per target project) because Stryker runs one project-under-test at a time and the test-project mapping differs:

| Config | Project under test | Test projects |
|--------|-------------------|---------------|
| `stryker/compiler.json` | `src/Sharpy.Compiler` | `Sharpy.Compiler.Tests` |
| `stryker/core.json` | `src/Sharpy.Core` | `Sharpy.Core.Tests` (+`Sharpy.Compiler.Tests` optional, slower) |
| `stryker/stdlib.json` | `src/Sharpy.Stdlib` | `Sharpy.Stdlib.Tests` |

Note the coverage blind spot from subsystem 1 cuts the other way here, usefully: because integration fixtures execute Core in a *subprocess*, mutants in `Sharpy.Core` are only killable by `Sharpy.Core.Tests`' in-process tests — so the Core mutation score directly measures the unit suite's strength, uncontaminated by integration-test smoke. *(Caveat: mutating Core changes the `Sharpy.Core.dll` that integration subprocesses copy — if `Sharpy.Compiler.Tests` is included as a test project, fixture tests **can** kill Core mutants via subprocess behavior changes; that mode is valuable but ~10× slower, hence optional.)*

## Configuration

`stryker/compiler.json` (representative; others differ in paths/thresholds):

```jsonc
{
  "stryker-config": {
    "project": "Sharpy.Compiler.csproj",
    "test-projects": ["../src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj"],
    "solution": "../sharpy.sln",
    "reporters": ["html", "json", "progress"],
    "coverage-analysis": "perTest",
    "concurrency": 4,
    "additional-timeout": 30000,          // subprocess-heavy tests; ms added to observed time
    "mutation-level": "Standard",         // not Complete; Complete is the quarterly mode
    "thresholds": { "high": 80, "low": 70, "break": 0 },   // break disabled: OUR gate decides
    "mutate": [
      // populated per-run by `harness mutation plan` (rotation or --since); defaults:
      "Semantic/**/*.cs",
      "CodeGen/**/*.cs",
      "!**/obj/**",
      "!**/*.Designer.cs"
    ],
    "ignore-methods": ["ToString", "GetHashCode", "*Log*", "Trace*"]
  }
}
```

Decisions embedded there:

- **Stryker's own `break` is disabled (0).** Per-component thresholds are enforced by `harness mutation gate` reading `mutation-report.json`, because Stryker scores per-run, while our policy is per-*component* (Semantic stricter than Lexer) and per-run scope varies with rotation. One gate, one place.
- **`mutation-level: Standard`** weekly; `Complete` only in the quarterly full run.
- **Equivalent mutants** are suppressed at the source with Stryker comment directives (`// Stryker disable once <mutator>: <reason>`) — reviewable, greppable, and counted by the gate so suppression volume is itself a tracked metric. Config-level `ignore-mutations` is reserved for systemic noise (e.g. string mutators in diagnostic-message templates).

Harness config section:

```jsonc
{
  "mutation": {
    "targets": ["compiler", "core", "stdlib"],
    "componentThresholds": {
      "Sharpy.Compiler/Semantic": 0.80,
      "Sharpy.Compiler/CodeGen": 0.75,
      "Sharpy.Compiler/Parser": 0.70,
      "Sharpy.Compiler/Lexer": 0.65,
      "Sharpy.Core": 0.80,
      "Sharpy.Stdlib": 0.65
    },
    "rotation": [
      ["compiler:Semantic", "core"],
      ["compiler:CodeGen", "stdlib"],
      ["compiler:Parser", "compiler:Lexer", "compiler:Project"]
    ],
    "prMode": { "since": "origin/mainline", "maxMutants": 400 },
    "baselineDirectory": "artifacts/mutation-baseline"
  }
}
```

## Gate & data models

```csharp
namespace Sharpy.TestHarness.Mutation;

/// <summary>Parsed subset of Stryker's mutation-report.json (schema v2).</summary>
public sealed record MutationRunReport
{
    public required string Target { get; init; }               // "compiler" | "core" | "stdlib"
    public required IReadOnlyList<FileMutationResult> Files { get; init; }
    public required DateTimeOffset RanAtUtc { get; init; }
    public required string GitSha { get; init; }
}

public sealed record FileMutationResult(
    string RelativePath,
    int Killed, int Survived, int Timeout, int NoCoverage, int Ignored,
    IReadOnlyList<SurvivingMutant> Survivors);

public sealed record SurvivingMutant(
    string MutatorName, int Line, string Original, string Mutated, string Status);

/// <summary>
/// Aggregates file results into components (directory-prefix mapping from
/// config), applies thresholds, and emits kill-suggestions for survivors.
/// Score convention: killed / (killed + survived + timeout); NoCoverage is
/// reported separately and cross-linked to the coverage report rather than
/// counted against the mutation score (uncovered code is subsystem 1's problem).
/// </summary>
public interface IMutationGate
{
    MutationGateResult Evaluate(
        IReadOnlyList<MutationRunReport> runs,
        MutationOptions options,
        MutationBaseline? baseline);
}

public sealed record MutationGateResult(
    bool Passed,
    IReadOnlyList<ComponentMutationScore> Components,
    IReadOnlyList<string> Violations,
    IReadOnlyList<TestSuggestion> Suggestions);

public sealed record ComponentMutationScore(
    string Component, double Score, int Killed, int Survived, int NoCoverage, int Ignored);

/// <summary>A human-readable pointer, not generated test code: file, line,
/// mutator, original vs mutated snippet, and which existing test class covers
/// the file (from coverage data) — "add a case to X asserting Y differs".</summary>
public sealed record TestSuggestion(
    string File, int Line, string Mutator, string Original, string Mutated,
    string? NearestTestClass);
```

`harness mutation suggest` prints suggestions grouped by file; the `/mutation` skill turns them into actual test cases with a human/agent in the loop (writing the killing test requires understanding intent — deliberately not automated).

## PR mode vs scheduled modes

| Mode | Trigger | Scope | Wall-clock budget |
|------|---------|-------|-------------------|
| PR | `test-harness-pr.yml`, path-filtered to the three target projects | `--since:origin/mainline`; capped at `maxMutants` (excess → reported as "deferred to weekly") | ≤ 25 min |
| Weekly | `test-harness-weekly.yml`, cron `0 3 * * 6` (Saturday, offset from the Monday crons) | Current rotation slot (config `rotation`), 3-job matrix | hours, off-peak |
| Quarterly | `workflow_dispatch` | Full projects, `mutation-level: Complete` | day-scale, manual |

PR gate policy: **changed files may not decrease their mutation score** relative to the weekly baseline (disk baseline artifact); new files must meet their component threshold. Whole-component thresholds are enforced only on weekly runs (a PR shouldn't fail for pre-existing debt elsewhere).

## CI workflow (weekly excerpt)

```yaml
  mutation-weekly:
    strategy:
      fail-fast: false
      matrix:
        target: [compiler, core, stdlib]
    runs-on: ubuntu-latest
    timeout-minutes: 360
    steps:
      - uses: actions/checkout@v7
        with: { fetch-depth: 0 }
      - uses: actions/setup-dotnet@v5
        with: { dotnet-version: 10.0.x }
      - run: dotnet tool restore
      - name: Plan mutation scope (rotation + coverage prioritization)
        run: dotnet run --project src/Sharpy.TestHarness -- mutation plan --target ${{ matrix.target }} --write stryker/${{ matrix.target }}.run.json
      - name: Run Stryker
        run: dotnet stryker --config-file stryker/${{ matrix.target }}.run.json
      - uses: actions/upload-artifact@v7
        with:
          name: mutation-report-${{ matrix.target }}
          path: StrykerOutput/**/reports/
          retention-days: 90

  mutation-gate:
    needs: mutation-weekly
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v5
        with: { dotnet-version: 10.0.x }
      - uses: actions/download-artifact@v8
        with: { pattern: mutation-report-*, path: artifacts/mutation }
      - run: dotnet run --project src/Sharpy.TestHarness -- mutation gate --reports artifacts/mutation --emit-subsystem-report
```

## Cross-subsystem integration

- **Coverage (1) → prioritization:** `harness mutation plan` reads the latest merged Cobertura data; files with <50% line coverage are *deprioritized* (their mutants mostly die to `NoCoverage`, which is coverage debt, not mutation signal) and flagged in the report as "fix coverage first".
- **Coverage (1) ↔ gate:** `NoCoverage` mutants are excluded from the score but reported beside the file's line rate — the "high coverage, low mutation score" quadrant is the actionable one.
- **Scaffolding (3):** `TestSuggestion`s reference the survivor's file/line so `/scaffold-tests`-style skeletons can host the killing test.
- **Unified report (README):** weekly `SubsystemReport` with per-component scores + trend; PR comments only in PR mode.

## Skill definition — `.claude/skills/mutation/SKILL.md`

```markdown
---
name: mutation
description: Run mutation testing on changed files (or a component) and triage surviving mutants
argument-hint: "[compiler|core|stdlib|--changed] [path-glob]"
---

Run Stryker.NET via the harness and turn surviving mutants into tests.

**Usage:** /mutation [target|--changed] [glob]

**Behavior:**
- `--changed` (default): mutates only files changed vs origin/mainline — fast local loop
- Runs through `dotnet tool run dotnet-stryker` (single dotnet process; still respect
  `.claude/scripts/dotnet-serialized` for the build step Stryker triggers is internal —
  do NOT run other dotnet builds/tests concurrently with a mutation run)
- After the run: `harness mutation suggest` and, for each survivor, either write a killing
  test, or add `// Stryker disable once <mutator>: <reason>` if genuinely equivalent —
  with a real reason, never to make the score pass (Critical Rule 1 applies to mutants too)

**Log location:** `.claude/tmp/last-mutation.log`

## Steps
1. `dotnet tool restore`
2. `dotnet run --project src/Sharpy.TestHarness -- mutation plan --changed --write stryker/local.run.json`
3. `dotnet stryker --config-file stryker/local.run.json 2>&1 | tee .claude/tmp/last-mutation.log | tail -30`
4. `dotnet run --project src/Sharpy.TestHarness -- mutation suggest --latest` — present survivors grouped by file
5. For each survivor the user wants killed: write the test, re-run scoped Stryker to confirm the kill
```

## Test plan (for the harness pieces)

- `MutationGateTests` — fixture `mutation-report.json` files (checked into `TestData/`, including a real small Stryker output): component aggregation by prefix, threshold boundaries, NoCoverage exclusion, baseline-delta rule, suggestion generation.
- `MutationPlanTests` — rotation arithmetic (slot selection is a pure function of ISO week % slots — deterministic, no `DateTime.Now` in core logic), `--since` glob emission, coverage-deprioritization given fixture Cobertura data.
- `StrykerConfigValidationTests` — the three committed configs parse and reference existing csproj paths (guards against project renames).
- Integration smoke (manual/quarterly): scoped 30-mutant run on `Shared/EditDistance.cs`-sized file to validate the toolchain end-to-end.

## Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Wall time explodes despite levers (subprocess tests in covering sets) | Weekly job overruns | `timeout-minutes: 360` + rotation shrinks scope; measure covering-set composition on first runs; if fixture tests dominate, invest in the test-case-filter check or exclude `Integration/` test classes from Stryker's test projects via a dedicated slim test csproj (last resort) |
| Test-case filtering unsupported in Stryker version | Property tests run per-mutant, slow + flaky kills | Verify at implementation; fallback documented above; property tests already excluded from *gate* coverage profile so their absence here is consistent |
| Equivalent mutants inflate "survivors" | Noise, alert fatigue | Comment directives with reasons; `Ignored` count tracked; weekly report shows suppression trend |
| Mutation of `Sharpy.Core` netstandard2.1 branches | Mutants in `#if` blocks Stryker can't attribute cleanly | Mutate net10.0 build only (matches coverage); document as shared limitation |
| Score gaming (deleting weak tests raises nothing; adding suppressions everywhere) | Metric decay | Suppressions require reasons + reviewed like code; gate counts them; coverage is the paired metric |
| First-run shock (score far below threshold) | Demoralizing red | Thresholds start in warn-only mode for two rotations; initial thresholds set to observed-baseline minus 5 pts, ratcheted upward |

## Implementation roadmap

| Phase | Work | Exit criteria |
|-------|------|---------------|
| 4a (week 1) | Tool manifest entry, three Stryker configs, first scoped manual runs (Semantic/, Core collections), timing data | End-to-end run completes; covering-set timing understood; test-filter question answered |
| 4b (week 1–2) | `mutation plan/gate/suggest` verbs + unit tests; component mapping; baseline format | Gate green on fixture data; suggestions readable |
| 4c (week 2) | Weekly matrix workflow + artifacts + warn-only gate; `/mutation` skill | First weekly run produces reports for all three targets |
| 4d (weeks 3–4) | PR mode (`--since`, maxMutants cap), thresholds enforcing after two clean rotations, trend in weekly health report | PR gate live; baseline ratchet documented |

Dependencies: tool manifest + coverage data from subsystem 1 (prioritization degrades gracefully to rotation-only without it).
