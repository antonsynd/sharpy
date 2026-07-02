# Subsystem 5: Coverage-Guided Fuzzing Infrastructure

> **Status:** Draft design — 2026-07-02
> **Priority:** 5 of 6 (finds deep bugs random testing misses)
> **Index:** [README.md](README.md) · **Proposal:** [../test-harness-hardening-proposal.md](../test-harness-hardening-proposal.md)

## Goal

Replace "seeded randomness in xUnit" as the deepest bug-finding layer with corpus-driven, coverage-guided fuzzing: a mutating fuzzer that watches which compiler code paths each input reaches and evolves inputs toward unexplored paths.

## Current state (verified)

- Existing "fuzzing" is `SharpyFuzzer` (`src/Sharpy.Compiler.Tests/Fuzz/SharpyFuzzer.cs`) — a hand-rolled seeded source generator driven by `FuzzTests.cs` via `[Theory]`/`[InlineData(seed)]`, ~100 iterations per seed, 2 s per-iteration timeout. Plus CsCheck property tests (`LexerPropertyTests`, `SemanticPropertyTests`, `CodeGenPropertyTests` in the same directory). None of it is coverage-guided.
- No SharpFuzz/libFuzzer packages anywhere.
- **These assets are complementary, not replaced:** `SharpyFuzzer`'s generators become corpus *seeders* and its `MutateProgram` becomes one of the fallback-mode mutators. The xUnit fuzz tests stay as fast PR smoke tests.

## Framework decision

**SharpFuzz** (IL-instrumentation for AFL/libFuzzer) is the choice:

- It is the only maintained coverage-guided option for .NET. The prompt asks to evaluate `Microsoft.Testing.Extensions.Fuzz` — no such shipped package exists as of .NET 10; there is no in-box coverage-guided fuzzer. A fully custom coverage-guided harness (coverlet-based feedback) would be an order of magnitude slower per exec than IL-level edge instrumentation and is not worth building.
- Execution modes: `libfuzzer-dotnet` (Linux/Windows — used in CI) and plain AFL-style forkserver. macOS (primary dev machine) gets a **fallback dumb mode**: the same `IFuzzTarget` binaries run corpus + random mutations in-process without coverage feedback — good enough for local repro/triage; discovery runs happen in CI.

```
                 ┌───────────────────────────────────────────────────┐
                 │ src/Sharpy.Fuzz (console, net10.0, NOT in test run)│
                 │                                                     │
   corpus/ ─────▶│  Program.Main(target, mode)                        │
   crashes/ ◀────│   ├─ LibFuzzer mode: SharpFuzz.Fuzzer.LibFuzzer.Run │◀── libfuzzer-dotnet (CI, Linux)
                 │   ├─ Dumb mode: corpus + mutators, no feedback     │◀── local macOS
                 │   └─ Repro mode: run one input, print diagnostics  │
                 └───────────────────────────────────────────────────┘
                            │ instrumented Sharpy.Compiler.dll (sharpfuzz IL rewrite, CI only)
                            ▼
                 Lexer / Parser / Semantic / CodeGen / RoundTrip targets
```

## Fuzz targets

```csharp
namespace Sharpy.Fuzz;

/// <summary>
/// One fuzzing entry point. Contract: Execute must be deterministic,
/// must not write to disk or spawn processes, and must THROW (not report)
/// on invariant violation — the fuzzer's crash detector is the exception.
/// Diagnostics (including parse errors) are normal outputs, never crashes.
/// </summary>
public interface IFuzzTarget
{
    string Name { get; }
    /// <summary>Raw fuzzer bytes. Targets decode as UTF-8 with replacement
    /// characters (the lexer must survive arbitrary text, not arbitrary bytes
    /// pretending to be text — invalid UTF-8 is normalized at this boundary).</summary>
    void Execute(ReadOnlySpan<byte> data);
}
```

| Target | Pipeline exercised | Invariants asserted (throw on violation) |
|--------|--------------------|------------------------------------------|
| `LexerTarget` | `new Lexer(text).TokenizeAll()` | No unhandled exception; token spans within source bounds; concatenated spans monotonic |
| `ParserTarget` | lex + `Parser.ParseModule()` | No unhandled exception (diagnostics fine); AST spans within bounds |
| `SemanticTarget` | full front-end (lex→parse→resolve→typecheck→validate) | No unhandled exception; **determinism**: run pipeline twice, diagnostic codes+spans byte-equal (catches dictionary-order and caching bugs) |
| `CodeGenTarget` | full pipeline → `RoslynEmitter` | No unhandled exception; when Sharpy reports zero errors, emitted C# has **zero Roslyn syntax errors** (`CSharpSyntaxTree.ParseText`); full Roslyn *semantic* compile is nightly-only (10× cost) |
| `RoundTripTarget` | parse → `Pretty.Unparser.Unparse` → reparse | Reuses the existing `ParserRoundTripPropertyTests` structural-equality logic; both parses agree |
| `ProjectTarget` (Phase 3) | multi-file: input split on `\x00` into virtual files fed to `ProjectCompiler` | Import-graph handling (circular imports terminate; deterministic diagnostics) |

Memory/time policy per target: libFuzzer flags `-rss_limit_mb=4096 -timeout=5 -malloc_limit_mb=2048`. OOM and timeout **are reportable bugs** (the prompt's open question — answered: yes; they land in `crashes/` tagged `oom-`/`slow-` and get triaged like crashes, since compiler hangs/explosions on adversarial input are real defects). Inputs capped at `-max_len=65536` — beyond 64 KiB, findings are pathological-input DoS, tracked separately from correctness.

## Corpus management

```
src/Sharpy.Fuzz/
├── corpus/
│   ├── lexer/          # committed, plain .spy text files (small, diffable)
│   ├── parser/
│   ├── semantic/
│   ├── codegen/
│   └── roundtrip/
├── crashes/            # NOT committed (CI artifacts); repro fixtures graduate to TestFixtures
└── dict/sharpy.dict    # libFuzzer dictionary: keywords, operators, dunder names
```

- **Seeding:** `harness fuzz seed-corpus --max 500` performs greedy set-cover over per-fixture coverage data (subsystem 1's Cobertura output, collected once per fixture via a batched instrumented run) to pick ~500 of the 2,185 fixtures maximizing distinct method coverage; plus ~50 outputs of the existing `SharpyFuzzer` generators for shapes fixtures don't cover (garbage tokens, deep nesting). Committed as plain files — total well under 1 MiB, diffable in review (the proposal's "compressed" suggestion is dropped: compression breaks diffability and the size doesn't warrant it).
- **Evolution:** nightly CI merges new coverage-increasing entries (`libfuzzer -merge=1`), uploaded as the `fuzz-corpus` artifact and restored by the next run via `actions/cache` keyed on corpus hash; a monthly manual `harness fuzz corpus promote` PR commits the accumulated keepers back to the repo (keeps repo churn low, reproducibility high).
- **Dictionary:** generated once from `TokenType` + keyword tables by `harness fuzz dict` — regenerated when the lexer changes (staleness-checked like other generated artifacts).

## Crash pipeline

```csharp
namespace Sharpy.TestHarness.Fuzzing;

/// <summary>Post-processes raw crash inputs from a fuzz run.</summary>
public interface ICrashTriager
{
    /// <summary>Dedupe key: SHA-256 of (exception type + top 3 non-framework
    /// stack frames, module+method only, no line numbers — line numbers churn
    /// across builds and split identical bugs).</summary>
    string ComputeBucket(CrashInfo crash);

    /// <summary>Line-based then token-based ddmin using the real Lexer;
    /// must preserve the crash bucket at every reduction step.</summary>
    Task<string> MinimizeAsync(string input, string targetName, string expectedBucket, CancellationToken ct);
}

public sealed record CrashInfo(
    string TargetName, string InputPath, string ExceptionType,
    string StackTrace, CrashKind Kind);

public enum CrashKind { Exception, Timeout, OutOfMemory, DeterminismViolation, RoundTripMismatch, InvalidCSharpEmitted }
```

Flow: crash file → bucket → if bucket unseen: minimize → `harness fuzz triage --create-issues` opens a GitHub issue (`gh issue create`, title `[fuzz] <ExceptionType> in <Stage>: <first frame>`, body = minimized repro + stack + target + corpus provenance, label `fuzz-crash`). Known buckets increment a counter, no duplicate issues.

**Crash-to-fixture pipeline:** when a crash's fix lands, `harness fuzz promote <bucket>` writes the minimized input to `src/Sharpy.Compiler.Tests/Integration/TestFixtures/fuzz_regressions/<bucket>.spy` with an `.error` or `.expected` sidecar reflecting the now-correct behavior — permanent regression protection through the *existing* fixture mechanism, and the fixture automatically enters the oracle universe (subsystem 2) and the seed corpus. The issue is closed by the fix PR referencing the fixture.

## Configuration

```jsonc
{
  "fuzzing": {
    "targets": ["lexer", "parser", "semantic", "codegen", "roundtrip"],
    "corpusRoot": "src/Sharpy.Fuzz/corpus",
    "prSecondsPerTarget": 120,
    "nightlySecondsPerTarget": 1800,
    "rssLimitMb": 4096, "timeoutSeconds": 5, "maxLen": 65536,
    // Nightly time split favors cheap high-throughput targets where
    // coverage-guidance pays off most; full-pipeline targets are slower per exec.
    "nightlyWeights": { "lexer": 1, "parser": 2, "semantic": 3, "codegen": 3, "roundtrip": 1 }
  }
}
```

## CI integration

**PR job** (`test-harness-pr.yml`, path-filtered to `src/Sharpy.Compiler/**`, `src/Sharpy.Fuzz/**`):

```yaml
  fuzz-pr:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    strategy: { fail-fast: false, matrix: { target: [lexer, parser, semantic, codegen, roundtrip] } }
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v5
        with: { dotnet-version: 10.0.x }
      - run: dotnet tool restore   # sharpfuzz CLI pinned in the tool manifest
      - name: Build + instrument
        run: |
          dotnet build src/Sharpy.Fuzz -c Release
          dotnet tool run sharpfuzz src/Sharpy.Fuzz/bin/Release/net10.0/Sharpy.Compiler.dll
      - name: Restore evolved corpus
        uses: actions/cache/restore@v4
        with: { path: fuzz-corpus, key: fuzz-corpus-${{ matrix.target }} }
      - name: Fuzz 2 minutes
        run: >
          ./libfuzzer-dotnet --target_path=dotnet
          --target_arg="src/Sharpy.Fuzz/bin/Release/net10.0/Sharpy.Fuzz.dll ${{ matrix.target }} libfuzzer"
          -max_total_time=120 -rss_limit_mb=4096 -timeout=5
          -dict=src/Sharpy.Fuzz/dict/sharpy.dict
          fuzz-corpus/${{ matrix.target }} src/Sharpy.Fuzz/corpus/${{ matrix.target }}
      - name: Upload crashes
        if: failure()
        uses: actions/upload-artifact@v7
        with: { name: fuzz-crashes-${{ matrix.target }}, path: "crash-*", retention-days: 90 }
```

**Nightly job** (`test-harness-nightly.yml`): same shape, 30 min/target weighted by `nightlyWeights`, followed by `-merge=1` corpus minimization, cache save, and `harness fuzz triage --create-issues`. Crash presence → `SubsystemReport` failure status → weekly health report.

The existing xUnit `FuzzTests` (seeded, 2 s budget) remain in the normal test run as smoke tests — they are fast and deterministic; no reason to remove them.

## Local developer experience & skill

```
harness fuzz run <target> [--seconds N]     # dumb mode locally (macOS), libfuzzer on Linux
harness fuzz repro <crash-file> [--target t]  # deterministic single-input replay w/ full diagnostics
harness fuzz seed-corpus / dict / triage / promote <bucket>
```

### Skill definition — `.claude/skills/fuzz/SKILL.md`

```markdown
---
name: fuzz
description: Run fuzz targets locally, reproduce and minimize crash inputs, promote fixed crashes to fixtures
argument-hint: "[target|repro <file>|triage|promote <bucket>]"
---

Drive the coverage-guided fuzzing infrastructure.

**Usage:** /fuzz [lexer|parser|semantic|codegen|roundtrip] | /fuzz repro <file> | /fuzz triage | /fuzz promote <bucket>

**Behavior:**
- Local runs use dumb mode (no instrumentation needed on macOS); discovery happens in CI
- `repro` replays one input deterministically and prints the full exception + stage
- Crash investigation: minimize first (`harness fuzz repro --minimize`), then debug the minimized input
  with /spy-emit — never debug the raw fuzzer blob
- `promote` turns a FIXED crash into a TestFixtures/fuzz_regressions/ fixture; verify the fixture
  passes before committing; create/close GitHub issues per the triage flow
- Build via `.claude/scripts/dotnet-serialized build src/Sharpy.Fuzz -c Release`

**Log location:** `.claude/tmp/last-fuzz.log`

## Steps
1. Build the fuzz project (serialized wrapper)
2. `dotnet run --project src/Sharpy.TestHarness -- fuzz <args> 2>&1 | tee .claude/tmp/last-fuzz.log | tail -40`
3. On crash: minimize, then reproduce via /spy-emit diagnostics on the minimized source; summarize stage + root cause
4. If asked to fix: fix the compiler (never special-case the fuzzer input), add the promoted fixture, close the issue
```

## Test plan (for the fuzz infrastructure itself)

- `FuzzTargetTests` (in `Sharpy.TestHarness.Tests`, `[Trait("Category","FuzzSmoke")]`) — every target executes the entire committed corpus without crashing; this is the fast deterministic proxy that runs on every PR and catches "target broken by compiler refactor" immediately.
- `CrashTriagerTests` — bucket stability (same bug, different line numbers → same bucket; different exception type → different bucket); minimizer preserves bucket and terminates on fixed-point; minimizer handles already-minimal input.
- `Utf8BoundaryTests` — invalid UTF-8 byte sequences normalize identically in libfuzzer and dumb modes (mode-equivalence of the decode boundary).
- `DictStalenessTest` — regenerated dictionary equals committed dictionary (same pattern as spy-stdlib staleness checks).
- CI mode-check: nightly job asserts instrumentation actually applied (SharpFuzz marker type present in rewritten assembly) so a silent instrumentation failure can't masquerade as "no coverage growth".

## Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Low throughput on full-pipeline targets (semantic ≈ tens of execs/s) | Coverage guidance starves | Weighted time split; lexer/parser get the raw path exploration, semantic/codegen inherit their corpus (cross-pollination: parser corpus seeds semantic) |
| SharpFuzz/libfuzzer-dotnet maintenance risk (small-community tooling) | Toolchain rot | `IFuzzTarget` is framework-agnostic; dumb mode keeps targets useful standalone; pinned tool versions in the manifest |
| Crash noise from environmental limits (GC pressure in CI) misfiled as bugs | Triage fatigue | OOM/timeout buckets separated from exception buckets; repro required on a second machine class before issue creation (`repro` in the triage step re-runs with 2× limits) |
| Corpus rot (compiler evolves, inputs stop reaching deep paths) | Declining value | Nightly merge keeps only coverage-contributing entries; monthly promote PR reviewed; corpus size + edge-count trends in weekly report |
| Fixture-derived corpus overfits to what tests already cover | Few new findings | `SharpyFuzzer` generative seeds + dictionary give the mutator raw material beyond fixtures; coverage guidance does the rest |
| Instrumented assembly accidentally shipped/tested | Wrong perf/behavior data | Instrumentation happens only inside the fuzz jobs on a Release copy under `Sharpy.Fuzz/bin`; never in `dotnet10.yml` |

## Implementation roadmap

| Phase | Work | Exit criteria |
|-------|------|---------------|
| 5a (week 1) | `Sharpy.Fuzz` project, five targets, dumb mode, repro mode, UTF-8 boundary, FuzzSmoke tests | All targets survive the raw fixture corpus locally |
| 5b (week 1–2) | SharpFuzz + libfuzzer-dotnet wiring on Linux (containerized locally for verification), dictionary generation, seed-corpus set-cover | First coverage-guided run finds new edges over seeds |
| 5c (week 2) | Crash triage (bucket/minimize/issue), `harness fuzz` verbs, `/fuzz` skill | A synthetic planted bug is found, minimized, issued end-to-end |
| 5d (weeks 2–3) | PR + nightly CI jobs, corpus cache/merge cycle, promote flow, report integration | One week of clean nightly runs; corpus growth visible |

Dependencies: tool manifest (subsystem 1); corpus seeding is better with coverage data (1) but degrades to "all fixtures ≤ 4 KiB" without it. Feeds: crashes → fixtures → oracle universe (2); fuzz findings inform mutation priorities (4) indirectly via where bugs cluster.
