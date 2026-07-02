# Subsystem 1: Code Coverage Infrastructure

> **Status:** Draft design — 2026-07-02
> **Priority:** 1 of 6 (lowest effort, highest immediate visibility)
> **Index:** [README.md](README.md) · **Proposal:** [../test-harness-hardening-proposal.md](../test-harness-hardening-proposal.md)

## Goal

Measure what the test suite exercises, enforce coverage gates in CI, and feed coverage data into the mutation-testing and fuzzing subsystems (prioritization and corpus selection).

## Current state (verified)

- **No coverage tooling exists anywhere.** No `coverlet.*` package references, no `.runsettings`, no `--collect` usage in CI or scripts.
- Test packages in use: `Microsoft.NET.Test.Sdk 18.6.0`, `xunit 2.9.3`, `xunit.runner.visualstudio 3.1.5`, `CsCheck 4.7.0`, `FluentAssertions 8.10.0`.
- CI (`dotnet10.yml`) runs five per-project `dotnet test` steps with `--filter "Category!=Benchmark"` on `ubuntu-latest`. No caching, no artifacts, no `timeout-minutes`.
- No central package management (`Directory.Packages.props` does not exist); package versions are inline per-csproj.

## The subprocess blind spot (critical constraint)

`IntegrationTestBase.CompileAndExecute` compiles generated C# in-memory, then **executes it out-of-process** via `dotnet exec` on a temp-dir assembly (`src/Sharpy.TestInfrastructure/Integration/IntegrationTestBase.cs`). Consequences:

1. **Compiler coverage is accurate.** Lexer → Parser → Semantic → RoslynEmitter all run inside the test process and are seen by coverlet.
2. **`Sharpy.Core`/`Sharpy.Stdlib` *runtime* execution by integration fixtures is invisible.** The 2,185 fixtures exercise `Sharpy.List<T>.append`, builtins, stdlib modules — inside the child process, where no collector is attached.
3. `ProjectCompilationHelper` is the exception — it executes in-process via `Assembly.LoadFrom` + reflection, so its executions *are* counted.

**Decision:** accept the blind spot in Phase 1. `Sharpy.Core` and `Sharpy.Stdlib` gates are satisfied by their dedicated in-process unit test projects (`Sharpy.Core.Tests`, `Sharpy.Stdlib.Tests`), which is where semantic coverage of collections/builtins belongs anyway. A Phase 3 extension can wrap the child process with `dotnet-coverage collect` (which supports child-process session collection) and merge; this is explicitly out of scope for the initial rollout.

## Architecture

```
                       ┌────────────────────────────────────────────┐
                       │ dotnet test <proj> --collect:"XPlat Code    │
  5 test projects ────▶│ Coverage" --settings coverage.runsettings  │──▶ TestResults/**/coverage.cobertura.xml
                       └────────────────────────────────────────────┘
                                                                            │
                                            ┌───────────────────────────────▼──────────────┐
                                            │ reportgenerator (dotnet tool, local manifest)│
                                            │ merge → HTML + Cobertura + JsonSummary       │
                                            └───────────────────────────────┬──────────────┘
                                                                            │
                       ┌───────────────────────────┐      ┌────────────────▼─────────────────┐
   mainline baseline ─▶│ harness coverage gate     │◀─────│ artifacts/coverage/Summary.json  │
   (CI artifact)       │ (thresholds + delta)      │      └──────────────────────────────────┘
                       └─────────────┬─────────────┘
                                     │ exit code + SubsystemReport JSON (see README: Unified Reporting)
                                     ▼
                        PR sticky comment + CI artifact + gate pass/fail
```

Components:

- **Collection** — `coverlet.collector` added as a `PackageReference` to the five test projects. Configured via a repo-root `coverage.runsettings`.
- **Merge/report** — `dotnet-reportgenerator-globaltool` pinned in a new local tool manifest `.config/dotnet-tools.json` (also used by Stryker, see [04-mutation-testing.md](04-mutation-testing.md)).
- **Gate** — `harness coverage gate`, a verb on the new `Sharpy.TestHarness` console app (see README: Shared Architecture). Parses ReportGenerator's `Summary.json`, applies per-component thresholds and the PR delta rule, emits a `SubsystemReport`.

## Data models

```csharp
namespace Sharpy.TestHarness.Coverage;

/// <summary>
/// Coverage for one gated component. Components map 1:1 to production
/// projects (<c>Sharpy.Compiler</c>, <c>Sharpy.Core</c>, …), aggregated
/// from the merged Cobertura report by assembly name.
/// </summary>
public sealed record ComponentCoverage
{
    /// <summary>Assembly name, e.g. "Sharpy.Compiler".</summary>
    public required string Component { get; init; }

    /// <summary>Covered lines / coverable lines, in [0, 1].</summary>
    public required double LineRate { get; init; }

    /// <summary>Covered branches / total branches, in [0, 1].</summary>
    public required double BranchRate { get; init; }

    public required int CoverableLines { get; init; }
    public required int CoveredLines { get; init; }
}

/// <summary>Aggregate result of one collection run (one profile).</summary>
public sealed record CoverageSummary
{
    public required string Profile { get; init; }              // "gate" | "full"
    public required string GitSha { get; init; }
    public required DateTimeOffset CollectedAtUtc { get; init; }
    public required IReadOnlyList<ComponentCoverage> Components { get; init; }
    public double OverallLineRate =>
        Components.Sum(c => c.CoveredLines) / (double)Math.Max(1, Components.Sum(c => c.CoverableLines));
}

/// <summary>Outcome of applying thresholds + delta rules to a summary.</summary>
public sealed record CoverageGateResult
{
    public required bool Passed { get; init; }
    public required IReadOnlyList<CoverageGateViolation> Violations { get; init; }
    /// <summary>Null when no baseline was available (delta rule skipped).</summary>
    public double? DeltaVsBaseline { get; init; }
}

public sealed record CoverageGateViolation(
    string Component, string Rule, double Actual, double Required);
```

```csharp
namespace Sharpy.TestHarness.Coverage;

/// <summary>
/// Applies configured thresholds to a merged coverage summary.
/// Pure function of (summary, baseline, options) — no I/O — so it is
/// trivially unit-testable with fixture JSON.
/// </summary>
public interface ICoverageGate
{
    /// <param name="baseline">The most recent mainline summary, or null when
    /// unavailable (first run, artifact expired). When null the delta rule is
    /// skipped and the result records <c>DeltaVsBaseline = null</c> — the gate
    /// must not fail merely because history is missing.</param>
    CoverageGateResult Evaluate(
        CoverageSummary summary,
        CoverageSummary? baseline,
        CoverageOptions options);
}
```

## Configuration

Section of `sharpy-test-harness.json` (root schema in README):

```jsonc
{
  "coverage": {
    // Per-assembly minimum line rate. Absent assembly ⇒ not gated.
    "thresholds": {
      "Sharpy.Core": 0.90,
      "Sharpy.Compiler": 0.80,
      "Sharpy.Stdlib": 0.75,
      "Sharpy.Lsp": 0.70,
      "Sharpy.Cli": 0.60
    },
    // PR may not drop overall line rate by more than this (fraction).
    "maxDeltaDrop": 0.01,
    // Which profile the gate runs on (see "Profiles" below).
    "gateProfile": "gate",
    "reportDirectory": "artifacts/coverage"
  }
}
```

Corresponding options record:

```csharp
namespace Sharpy.TestHarness.Configuration;

public sealed record CoverageOptions
{
    public required IReadOnlyDictionary<string, double> Thresholds { get; init; }
    public double MaxDeltaDrop { get; init; } = 0.01;
    public string GateProfile { get; init; } = "gate";
    public string ReportDirectory { get; init; } = "artifacts/coverage";
}
```

### `coverage.runsettings` (repo root)

```xml
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <!-- Only measure production assemblies. -->
          <Include>[Sharpy.Compiler]*,[Sharpy.Core]*,[Sharpy.Stdlib*]*,[Sharpy.Cli]*,[Sharpy.Lsp]*</Include>
          <!-- Test infra, generators, benchmarks are not gated code. -->
          <Exclude>[*.Tests]*,[Sharpy.TestInfrastructure]*,[Sharpy.Fuzz]*,[Sharpy.TestHarness]*</Exclude>
          <ExcludeByAttribute>GeneratedCodeAttribute,CompilerGeneratedAttribute,ExcludedFromCodeCoverageAttribute</ExcludeByAttribute>
          <SkipAutoProps>true</SkipAutoProps>
          <DeterministicReport>true</DeterministicReport>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Excluding generated code

Spy-sourced stdlib modules have committed C# generated by `build_tools/regenerate_spy_stdlib.sh`. Two mechanisms, in preference order:

1. **Recommended (durable):** teach `RoslynEmitter` (behind the existing `--emit-cs-to` path used by the regenerate script) to stamp module classes with `[System.CodeDom.Compiler.GeneratedCode("sharpyc", "<version>")]`. `ExcludeByAttribute` then handles it forever, and it also benefits Stryker exclusions. This is a small codegen change and regenerated files must be checked in (the `/push` staleness gate already covers this).
2. **Interim:** an `ExcludeByFile` glob list in `coverage.runsettings` derived from the `MODULES` mapping in `regenerate_spy_stdlib.sh`, with a conformance test in `Sharpy.TestHarness.Tests` asserting the two lists agree (same pattern as the existing staleness checks).

### Conditional compilation

Coverage is collected on the `net10.0` target only. `#if NET10_0_OR_GREATER` alternates for `netstandard2.1` show as uncovered *in that TFM* but the net10.0 report never sees them; no special handling needed. Documented limitation: the `netstandard2.1` code paths of Core/Stdlib are exercised only by compilation, not measured.

## Profiles: keeping property/fuzz tests out of the gate

Random-input tests inflate line coverage without asserting much per line, and their coverage varies run-to-run. Two profiles:

| Profile | Filter | Used for |
|---------|--------|----------|
| `gate` | `Category!=Benchmark&Category!=Property&Category!=RandomProperty&Category!=GapDiscovery` | CI gate, PR comments, baseline |
| `full` | `Category!=Benchmark` | Informational; nightly; input to mutation/fuzz prioritization |

File-based integration fixtures carry no `Category` trait, so they remain in the `gate` profile — deliberately, since they are deterministic and are the primary exerciser of the compiler.

## Local developer experience

```bash
# via the skill (preferred)
/coverage                    # collect (gate profile), merge, print per-component table, open HTML
/coverage full               # same with the full profile

# raw equivalent
.claude/scripts/dotnet-serialized test --collect:"XPlat Code Coverage" --settings coverage.runsettings
dotnet tool run reportgenerator -reports:"src/**/TestResults/**/coverage.cobertura.xml" \
    -targetdir:artifacts/coverage -reporttypes:"Html;JsonSummary;Cobertura"
dotnet run --project src/Sharpy.TestHarness -- coverage gate
```

### Skill definition — `.claude/skills/coverage/SKILL.md`

```markdown
---
name: coverage
description: Collect code coverage, merge into an HTML report, and evaluate thresholds
argument-hint: "[gate|full] [project-filter]"
---

Collect code coverage across test projects, merge with ReportGenerator, and run the harness gate.

**Usage:** /coverage [gate|full] [project]

**Behavior:**
- Builds first, then runs tests with `--collect:"XPlat Code Coverage"` using `.claude/scripts/dotnet-serialized`
- Profile defaults to `gate` (excludes Property/RandomProperty/GapDiscovery categories)
- Merges Cobertura files with `dotnet tool run reportgenerator` into `artifacts/coverage/`
- Runs `dotnet run --project src/Sharpy.TestHarness -- coverage gate` and prints the per-component table
- Never modifies thresholds to make the gate pass — report violations instead

**Log location:** `.claude/tmp/last-coverage.log`

## Steps
1. Profile = first arg or `gate`; map to the category filter documented in docs/design/test-harness/01-code-coverage.md
2. `.claude/scripts/dotnet-serialized test [src/<project>] --collect:"XPlat Code Coverage" --settings coverage.runsettings --filter "<profile filter>" 2>&1 | tail -20`
3. `dotnet tool run reportgenerator -reports:"src/**/TestResults/**/coverage.cobertura.xml" -targetdir:artifacts/coverage -reporttypes:"Html;JsonSummary;Cobertura"`
4. `dotnet run --project src/Sharpy.TestHarness -- coverage gate` — show the table and any violations
5. Tell the user the HTML report is at `artifacts/coverage/index.html`
```

## CI integration

Changes to `dotnet10.yml` (additive; the five test steps gain two flags each):

```yaml
      - name: Test Compiler
        run: >
          dotnet test src/Sharpy.Compiler.Tests --no-build --verbosity normal
          --filter "Category!=Benchmark&Category!=Property&Category!=RandomProperty&Category!=GapDiscovery"
          --collect:"XPlat Code Coverage" --settings coverage.runsettings
      # ... same for the other four test steps ...

      - name: Merge coverage
        run: |
          dotnet tool restore
          dotnet tool run reportgenerator \
            -reports:"src/**/TestResults/**/coverage.cobertura.xml" \
            -targetdir:artifacts/coverage -reporttypes:"Html;JsonSummary;Cobertura"

      - name: Download mainline coverage baseline
        if: github.event_name == 'pull_request'
        continue-on-error: true    # missing baseline must not fail the gate
        run: |
          gh run download --repo "$GITHUB_REPOSITORY" \
            $(gh run list --repo "$GITHUB_REPOSITORY" --workflow dotnet10.yml \
              --branch mainline --status success --limit 1 --json databaseId -q '.[0].databaseId') \
            --name coverage-summary --dir artifacts/coverage-baseline
        env:
          GH_TOKEN: ${{ github.token }}

      - name: Coverage gate
        run: >
          dotnet run --project src/Sharpy.TestHarness -- coverage gate
          --summary artifacts/coverage/Summary.json
          --baseline artifacts/coverage-baseline/Summary.json

      - name: Upload coverage report
        if: always()
        uses: actions/upload-artifact@v7
        with:
          name: coverage-report
          path: artifacts/coverage/
          retention-days: 30

      - name: Upload coverage baseline
        if: github.ref == 'refs/heads/mainline'
        uses: actions/upload-artifact@v7
        with:
          name: coverage-summary
          path: artifacts/coverage/Summary.json
          retention-days: 90
```

Notes:

- Full-profile property tests move to the nightly harness workflow (see README: CI topology) so PR wall-time doesn't grow; the gate profile is a strict subset of what runs today.
- The PR comment (coverage delta + uncovered new lines) is produced by the shared `harness report comment` step described in README — coverage contributes its `SubsystemReport` JSON; no coverage-specific comment logic.
- `TreatWarningsAsErrors` is unaffected — coverlet is a collector, not an analyzer.

## Cross-subsystem feeds

| Consumer | What it reads | How |
|----------|---------------|-----|
| Mutation testing (04) | Per-file line rates from merged Cobertura | Low-coverage files get *lower* mutation priority (mutants there die to "no coverage" trivially; fix coverage first). High-coverage + low-mutation-score files are the real test-gap signal. |
| Fuzzing (05) | Per-method hit counts | `harness fuzz seed-corpus` selects the ~500 fixtures maximizing distinct compiler-method coverage (greedy set cover over Cobertura data). |
| Unified report (README) | `SubsystemReport` from the gate | PR comment, weekly health report, trend tracking. |

## Test plan (for the harness itself)

In `Sharpy.TestHarness.Tests/Coverage/`:

- `CoverageGateTests` — pure-function tests: thresholds pass/fail boundaries, delta rule with/without baseline, unknown components ignored, empty summary rejected.
- `CobertuaParsingTests` — parse fixture `Summary.json`/Cobertura XML checked into `TestData/`; malformed input → clear error, not exception.
- `RunsettingsConformanceTests` — asserts `coverage.runsettings` excludes exactly the generated-file list derived from `regenerate_spy_stdlib.sh` (until the `GeneratedCode` attribute lands).
- CI smoke: the gate step itself running on every PR is the integration test.

## Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Instrumentation slows the ~8 min suite by 20–40% | Longer PR CI | Gate profile excludes the slowest random categories; measure and, if needed, collect coverage on a single merged step rather than 5 |
| Core/Stdlib gates misread as "runtime is well tested" despite subprocess blind spot | False confidence | Documented here + in report footnote; Phase 3 `dotnet-coverage` child-process merge; mutation testing (04) independently measures Core test effectiveness |
| Baseline artifact expired/missing on old mainline | Delta gate silently skipped | Gate records `DeltaVsBaseline = null` and the PR comment says "no baseline"; 90-day retention on mainline summaries |
| Coverage ratchet gamed by adding low-value tests | Metric decay | Mutation score (04) is the counter-metric; report shows both side by side |
| Threshold too aggressive for Stdlib (60 modules, some thin wrappers) | Chronic red gate | Thresholds live in config, tuned during Phase 1 burn-in (gate warns, doesn't fail, for the first two weeks) |

## Implementation roadmap

| Phase | Work | Exit criteria |
|-------|------|---------------|
| 1a (days 1–2) | Add `coverlet.collector` to 5 test csprojs; `coverage.runsettings`; tool manifest with ReportGenerator; local `/coverage` skill | `/coverage` produces HTML locally on macOS |
| 1b (days 3–4) | `Sharpy.TestHarness` project skeleton + `coverage gate` verb + unit tests | Gate passes/fails correctly on fixture data |
| 1c (days 5–7) | CI wiring (collection flags, merge, gate in warn-only mode, artifacts, baseline) | Green PR run with coverage artifact; numbers reviewed |
| 1d (week 2) | Flip gate to enforcing; tune thresholds; `GeneratedCode` attribute in emitter (or interim glob list + conformance test) | Gate enforcing on PRs; generated code excluded |

Dependencies: none (first subsystem). Produces: tool manifest, `Sharpy.TestHarness` skeleton, and reporting plumbing that subsystems 2–6 reuse.
