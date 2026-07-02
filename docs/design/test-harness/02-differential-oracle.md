# Subsystem 2: Hallucination-Resistant Differential Oracle

> **Status:** Draft design — 2026-07-02
> **Priority:** 2 of 6 (directly addresses hallucination risk; leverages the existing 2,185 fixtures)
> **Index:** [README.md](README.md) · **Proposal:** [../test-harness-hardening-proposal.md](../test-harness-hardening-proposal.md)

## Goal

Independently verify `.expected` fixture outputs by transpiling each `.spy` fixture to Python 3, executing both, and comparing. A hallucinated or simply wrong `.expected` file cannot persist undetected: either Python agrees with it (verified), disagrees (divergent → triage), or the fixture is explicitly outside Python's semantic reach (skip, with a machine-readable reason).

**Hard constraint (from the prompt):** the transpiler is rule-based, never LLM-based. A verification tool must be deterministic. Where translation is uncertain, the correct output is `SKIP_ORACLE`, not a guess.

## Existing assets (verified)

- **`docs/deviations.yaml` already exists** — 41 KB, ~55 entries, machine-parseable schema: `id`, `code`, `category` (scoping|types|operators|syntax|semantics|stdlib), `audience`, `severity`, `python_behavior`, `sharpy_behavior`, `spec_ref`, `existing_diagnostic`, `planned_diagnostic`, `example.python`, `example.sharpy`. The oracle consumes this file; it does not invent a new catalog.
- Fixture corpus: 2,185 `.spy`, 1,523 `.expected`, 520 `.error` under `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` (plus the Stdlib.Tests fixture root). Discovery logic already exists in `FixtureDiscoveryHelper` (`src/Sharpy.TestInfrastructure/Integration/FixtureDiscoveryHelper.cs`) including multi-file root detection — the oracle reuses it rather than re-scanning.
- Sharpy-side execution: `IntegrationTestBase.CompileAndExecute` (in-memory Roslyn compile → `dotnet exec` subprocess, 30 s timeout). The oracle reuses the same pipeline via a thin non-xUnit wrapper.
- Python subprocess precedent: `benchmarks/cross-language/run_benchmarks.py` already shells `python3` with `timeout` + `capture_output`. CI already pins `actions/setup-python@v6` / `python-version: '3.12'`.
- Sharpy source in docs/fixtures is Python-syntax-compatible by design (spec code fences are tagged ` ```python `), which is what makes rule-based transpilation tractable.

## Architecture

```
 FixtureDiscoveryHelper ──▶ fixture universe (single- and multi-file, sidecars)
        │
        ▼
 ┌───────────────────┐   parse (existing Lexer+Parser)   ┌──────────────────────┐
 │ OracleRunner       │──────────────────────────────────▶│ SpyToPythonTranspiler │
 │ (orchestrator,     │                                   │ (AST visitor,         │
 │  parallel over     │◀──────────────────────────────────│  closed whitelist)    │
 │  fixtures)         │   PythonSource | Skip(reason)     └──────────────────────┘
 └────┬─────────┬────┘
      │         │
      ▼         ▼
 ┌─────────┐ ┌──────────────┐
 │ Sharpy   │ │ PythonExecutor│   python3 -I -B, PYTHONHASHSEED=0, timeout, output cap
 │ executor │ │ (subprocess) │
 └────┬────┘ └──────┬───────┘
      │             │
      ▼             ▼
 ┌───────────────────────────┐     ┌──────────────────┐     ┌───────────────────┐
 │ OutputComparer             │────▶│ DeviationCatalog  │────▶│ OracleVerdict      │
 │ (normalizer chain)         │     │ (docs/deviations  │     │ per fixture        │
 └───────────────────────────┘     │  .yaml matcher)   │     └─────────┬─────────┘
                                   └──────────────────┘               │
                                              baseline diff ◀─────────┤
                                   ┌──────────────────────────────────▼─────┐
                                   │ OracleReport (aggregate, trends, gate) │
                                   └────────────────────────────────────────┘
```

## Trust classification

```csharp
namespace Sharpy.TestHarness.Oracle;

/// <summary>Per-fixture trust status. Ordering matters for gating:
/// only <see cref="Divergent"/> is a failure state.</summary>
public enum OracleStatus
{
    /// <summary>Sharpy output == Python output (after configured normalization).</summary>
    Verified,
    /// <summary>Outputs differ, but the difference matches a cataloged entry
    /// in docs/deviations.yaml (or a fixture-level deviation annotation).</summary>
    VerifiedWithDeviation,
    /// <summary>Outputs differ and no cataloged deviation explains it.
    /// Potential compiler bug OR wrong .expected file. Gate-failing.</summary>
    Divergent,
    /// <summary>Fixture uses Sharpy-only constructs; transpiler declined by policy.</summary>
    SkipOracle,
    /// <summary>Transpiler attempted translation but could not produce valid Python.
    /// Signals a transpiler gap, not a fixture problem.</summary>
    TranslationFailed,
    /// <summary>Translated Python raised where Sharpy succeeded (or vice versa in a
    /// non-error fixture). Usually a transpiler bug; triaged separately from Divergent.</summary>
    PythonError,
}
```

## Key interfaces

```csharp
namespace Sharpy.TestHarness.Oracle;

using Sharpy.Compiler.Parser.Ast;

/// <summary>
/// Rule-based Sharpy→Python translator operating on the parsed AST
/// (never on source text). Implements a CLOSED WHITELIST: every AST node
/// kind is either explicitly supported or produces a skip — there is no
/// best-effort fallback path. Determinism contract: identical Module input
/// yields byte-identical Python output.
/// </summary>
public interface ISpyToPythonTranspiler
{
    /// <summary>
    /// Translates a parsed module. Never throws for unsupported constructs;
    /// unsupported nodes yield <see cref="TranslationResult.Skipped"/> with the
    /// node kind and source span so skip statistics are attributable.
    /// </summary>
    TranslationResult Translate(Module module, TranslationContext context);
}

/// <summary>Success carries Python source; skip carries a structured reason.</summary>
public sealed record TranslationResult
{
    public required bool Success { get; init; }
    /// <summary>Valid Python 3.12 source. Non-null iff Success.</summary>
    public string? PythonSource { get; init; }
    public SkipReason? Skip { get; init; }
    /// <summary>Non-fatal notes (e.g. "stripped type annotations", "mapped
    /// Sharpy re → Python re"). Surfaced in the report for auditability.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record SkipReason(SkipCategory Category, string NodeKind, string Detail, int Line);

public enum SkipCategory
{
    DotNetInterop,        // from System..., clr references
    SharpyOnlyType,       // struct, Result<T,E> (!E), Optional/maybe, events
    SharpyOnlySyntax,     // constructs with no Python analog
    StdlibNoEquivalent,   // module/function not in the import map
    ExplicitAnnotation,   // .oracle sidecar says skip
}

/// <summary>
/// Runs Python source in a hardened subprocess:
/// `python3 -I -B` (isolated mode, no site-packages, no .pyc),
/// PYTHONHASHSEED=0, configurable timeout (default 10 s),
/// stdout/stderr capped (default 1 MiB) to survive runaway loops.
/// </summary>
public interface IPythonExecutor
{
    Task<PythonExecutionResult> ExecuteAsync(
        string pythonSource, PythonExecutionOptions options, CancellationToken ct);
}

public sealed record PythonExecutionResult(
    int ExitCode, string StandardOutput, string StandardError,
    bool TimedOut, bool OutputTruncated, TimeSpan Elapsed);

/// <summary>
/// One step in the normalization chain. Comparers are pure and ordered;
/// the chain for a fixture = global defaults + fixture sidecar additions.
/// </summary>
public interface IOutputNormalizer
{
    /// <summary>Stable id referenced from config and .oracle sidecars,
    /// e.g. "exact", "trailing-whitespace", "float-tolerance", "error-category".</summary>
    string Id { get; }
    string Normalize(string output);
}

/// <summary>
/// Loads and matches docs/deviations.yaml. Matching is two-stage:
/// (1) fixture-level: an .oracle sidecar names deviation ids explicitly;
/// (2) heuristic: the divergence signature (category inferred from the
///     fixture's AST, e.g. uses `//` ⇒ operator deviations) proposes
///     candidate entries, which are recorded as *suggestions* in the report
///     but never auto-accepted. Only explicit annotations flip a fixture to
///     VerifiedWithDeviation — heuristics propose, humans dispose.
/// </summary>
public interface IDeviationCatalog
{
    IReadOnlyList<DeviationEntry> Entries { get; }
    DeviationEntry? GetById(string id);
    IReadOnlyList<DeviationEntry> SuggestFor(FixtureAstFacts facts);
}

/// <summary>Mirrors the existing docs/deviations.yaml schema (subset the oracle needs).</summary>
public sealed record DeviationEntry(
    string Id, string? Code, string Category,
    string PythonBehavior, string SharpyBehavior, string? SpecRef);
```

```csharp
namespace Sharpy.TestHarness.Oracle;

/// <summary>Orchestrates one fixture end-to-end. Thread-safe; the runner
/// fans out across fixtures with bounded parallelism (default: cores/2,
/// because each verification spawns up to two subprocesses).</summary>
public interface IDifferentialOracle
{
    Task<OracleVerdict> VerifyAsync(FixtureCase fixture, CancellationToken ct);
}

public sealed record FixtureCase(
    string Name, string SpyPath, bool IsMultiFile,
    string? ExpectedPath, string? ErrorPath, OracleSidecar? Sidecar);

public sealed record OracleVerdict
{
    public required string Fixture { get; init; }
    public required OracleStatus Status { get; init; }
    public SkipReason? SkipReason { get; init; }
    public string? DeviationId { get; init; }
    /// <summary>Unified diff of normalized outputs; only for Divergent/PythonError.</summary>
    public string? Diff { get; init; }
    /// <summary>Ids of deviation entries the heuristic matcher suggests for triage.</summary>
    public IReadOnlyList<string> SuggestedDeviations { get; init; } = [];
    /// <summary>Extra signal: did Python also match the committed .expected file?
    /// Verified + PythonMatchesExpected=false means Sharpy and Python agree with
    /// each other but NOT with .expected — the .expected file is wrong.</summary>
    public bool? PythonMatchesExpected { get; init; }
}
```

The `PythonMatchesExpected` field is the hallucination detector's sharpest edge: the three-way comparison (Sharpy vs Python vs `.expected`) distinguishes "compiler bug" (Sharpy ≠ Python = expected), "wrong expected file" (Sharpy = Python ≠ expected — the existing xUnit test is also failing, so this is mostly a cross-check), and "both wrong in the same way" is the only escape, which requires the fixture author and CPython to make the same mistake.

## Translation scope (v1 whitelist)

**Translate:** module-level statements, `def` (positional/keyword/default args), classes (methods, `__init__`, single inheritance), assignments and augmented assignments, `if/elif/else`, `while`, `for`, `match`, `try/except/finally/raise`, comprehensions, lambdas, f-strings, literals (list/dict/set/tuple), slicing/indexing, boolean/arithmetic/comparison operators, `print`/`len`/`range`/`enumerate`/`zip`/`sorted`/`str`/`int`/`float`/`bool` builtins, imports from the mapped stdlib set (`math`, `json`, `re`, `os`, `sys`, `random`*, `itertools`, `functools`, `collections`, `textwrap`, `string`, `heapq`, `bisect`, `datetime`*). Type annotations are **preserved, not stripped** where Python 3.12 accepts them (they're inert at runtime) and dropped only where Sharpy-specific (e.g. `T?`, `T !E`, `Self`).

**Skip (SharpyOnlyType/Syntax):** `struct`, `!E` result types, `maybe`/`Optional` narrowing, events, `.NET` interop imports, decorators outside a small known set (`@property`, `@staticmethod`, `@dataclass`), generics beyond erasable annotations, source-generator attributes.

\* `random`/`datetime` translate syntactically but are **nondeterministic across runtimes**; fixtures using them need a sidecar (below) or get `SkipOracle` by a builtin-usage check.

## Per-fixture sidecar: `.oracle`

Follows the existing sidecar convention (`.expected`, `.error`, `.warning`, `.skip`). Optional YAML next to the `.spy` file:

```yaml
# arithmetic_floor_div.spy.oracle
status: deviation            # skip | deviation | normalize (default: automatic)
deviations: [op-floordiv-truncation]   # ids from docs/deviations.yaml
normalizers: [float-tolerance]         # extra normalizers for this fixture
reason: "// truncates toward zero in Sharpy (Axiom 1); Python floors"
```

Precedence: `status: skip` > `.skip` file (already excluded from discovery) > automatic classification.

## Non-determinism handling

| Source | Strategy |
|--------|----------|
| `PYTHONHASHSEED` (set/dict iteration in Python) | Pinned to 0; additionally a `set-order` normalizer sorts lines the fixture marks as order-insensitive |
| Float repr differences (`1e-07` vs `1e-07`, `repr` precision) | `float-tolerance` normalizer: tokenize numbers, compare with relative epsilon 1e-9 |
| `id()`/hash values, addresses | Not translated (skip category) |
| `random`, `time`, `datetime.now` | Sidecar `skip`, or fixture rewritten to seed/fixed values (preferred long-term) |
| Dict ordering | Both languages preserve insertion order; no normalization by default |

## Error-fixture comparison (`.error` fixtures)

For the 520 error fixtures, exact message equality is meaningless. The comparison is categorical: translate, run Python, and require *both* to fail, mapping Python's exception class to a Sharpy diagnostic category via a small static table (`TypeError`/`AttributeError` → semantic type errors SPY02xx, `SyntaxError` → parser SPY01xx, `NameError` → name resolution). Many Sharpy compile-time errors are Python *runtime* errors — that's fine; the assertion is "Python also considers this program wrong," not "identical phase." Fixtures rejecting Python-legal code (intentional restrictions) must carry a `deviations:` sidecar pointing at the relevant catalog entry — the catalog's *hard-rejected Python syntax* section already enumerates these.

## Configuration

```jsonc
{
  "oracle": {
    "pythonPath": "python3",          // resolved on PATH; CI pins 3.12
    "minPythonVersion": "3.12",
    "timeoutSeconds": 10,
    "outputCapBytes": 1048576,
    "parallelism": 0,                  // 0 = cores/2
    "deviationCatalog": "docs/deviations.yaml",
    "baseline": "src/Sharpy.TestHarness/Oracle/oracle-baseline.json",
    "defaultNormalizers": ["trailing-whitespace"],
    "fixtureRoots": [
      "src/Sharpy.Compiler.Tests/Integration/TestFixtures",
      "src/Sharpy.Stdlib.Tests/Integration/TestFixtures"
    ]
  }
}
```

## Baseline & gating

`oracle-baseline.json` — committed, one line per fixture: `{ "fixture": "...", "status": "Verified", "deviationId": null }`. Rules:

- **PR gate:** any fixture moving `Verified|VerifiedWithDeviation → Divergent` fails. New fixtures entering as `Divergent` fail. Movements *into* `Verified` auto-update nothing (nightly owns the baseline).
- **Nightly:** full run; commits an updated baseline to `dev` as `github-actions[bot]` — same pattern the cross-language benchmark workflow already uses for `history.json`. Trend metrics (verified %, skip %, per-skip-category counts) append to `oracle-history.json`.
- Target from the proposal: >70% `Verified` among non-interop fixtures; tracked, not gated, in Phase 2.

## CLI verbs & skill

```
harness oracle run [--changed-only] [--fixture <glob>] [--json out.json]
harness oracle explain <fixture>      # show translation, both outputs, diff, suggestions
harness oracle baseline update        # regenerate baseline from last run
```

### Skill definition — `.claude/skills/oracle/SKILL.md`

```markdown
---
name: oracle
description: Verify .spy fixtures against Python ground truth via the differential oracle
argument-hint: "[fixture-name-or-glob | --changed-only]"
---

Run the differential oracle: transpile fixtures to Python, execute both, compare.

**Usage:** /oracle [fixture-glob | --changed-only]

**Behavior:**
- Runs `dotnet run --project src/Sharpy.TestHarness -- oracle run` with the given scope
- DIVERGENT results: show the diff and the three-way verdict (Sharpy vs Python vs .expected);
  NEVER "fix" a divergence by editing .expected to match Sharpy — investigate which side is wrong
- If Python and Sharpy agree but .expected differs, the .expected file is wrong — say so explicitly
- SKIP/TRANSLATION_FAILED: report counts by category, do not chase individually unless asked

**Log location:** `.claude/tmp/last-oracle.log`

## Steps
1. Build first via `.claude/scripts/dotnet-serialized build src/Sharpy.TestHarness`
2. `dotnet run --project src/Sharpy.TestHarness -- oracle run $ARGUMENTS 2>&1 | tee .claude/tmp/last-oracle.log | tail -40`
3. For each DIVERGENT fixture, run `... -- oracle explain <fixture>` and summarize which side is wrong and why
4. Report verified/deviation/skip/divergent counts and any baseline regressions
```

## CI integration

Two touch points (workflow topology in README):

**PR job** (in `test-harness-pr.yml`, path-filtered to `**/TestFixtures/**`, `src/Sharpy.Compiler/**`, `docs/deviations.yaml`):

```yaml
  oracle-pr:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
        with: { fetch-depth: 0 }
      - uses: actions/setup-dotnet@v5
        with: { dotnet-version: 10.0.x }
      - uses: actions/setup-python@v6
        with: { python-version: '3.12' }
      - run: dotnet build src/Sharpy.TestHarness
      - name: Oracle (changed fixtures + full-if-compiler-changed)
        run: dotnet run --project src/Sharpy.TestHarness --no-build -- oracle run --changed-only --base origin/mainline --json artifacts/oracle/report.json
      - uses: actions/upload-artifact@v7
        with: { name: oracle-report, path: artifacts/oracle/, retention-days: 30 }
```

`--changed-only` resolves to: fixtures whose files changed, **plus the full translatable set when compiler source changed** (compiler changes can flip any fixture; the full run is the point). Full-run wall time governs feasibility — see risks.

**Nightly job** (in `test-harness-nightly.yml`, cron `0 4 * * *`): full run, baseline + history commit to `dev`, `SubsystemReport` artifact.

## Test plan (for the oracle itself)

- **Transpiler golden tests** — `Sharpy.TestHarness.Tests/Oracle/TranspilerTests`: curated `.spy` → expected `.py` snapshot pairs (same UPDATE_SNAPSHOTS discipline as `.expected.cs`); plus a determinism test (translate twice, byte-equal).
- **Whitelist completeness test** — walk every AST node type (reflection over the `Sharpy.Compiler.Parser.Ast` records); assert the transpiler either handles it or maps it to a `SkipCategory` — no node kind may fall through to an exception.
- **Executor tests** — timeout kills the process tree; output cap truncates and flags; `-I` really isolates (a `import site`-dependent script fails predictably).
- **Normalizer unit tests** — float tolerance boundaries, idempotence (`Normalize(Normalize(x)) == Normalize(x)`).
- **Catalog tests** — parse the real `docs/deviations.yaml`; schema conformance test doubles as a lint for catalog edits.
- **End-to-end** — 20–30 hand-picked fixtures with known statuses (incl. one deliberately wrong `.expected` in test data proving `Divergent` + `PythonMatchesExpected=false` fires).
- **Self-check in CI:** the oracle running nightly is its own soak test; `TranslationFailed`/`PythonError` counts trending up = transpiler regression alarm in the weekly report.

## Risks & mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Transpiler bug produces wrong Python that *happens* to match wrong `.expected` | Missed bug (rare double-fault) | Closed whitelist + golden transpiler tests + `PythonError` triage lane; prefer skip over cleverness |
| Full-run wall time (2,185 × compile+2 execs; est. 1.5–2 h serial, ~15–20 min at 8× parallel) | Nightly budget; PR full-run too slow | Bounded parallelism (harness CLI is not bound by the `HeavyCompilation` xUnit serialization); PR full-run only when compiler source changed, else changed-fixtures-only; per-run `--budget-minutes` cutoff reporting partial coverage honestly |
| Deviation catalog drift (new intentional deviations not cataloged) | False `Divergent` noise | Divergence report suggests catalog candidates; adding a deviation requires a `docs/deviations.yaml` PR — same review lane as spec changes |
| Python 3.13+ behavior drift | Statuses flip without Sharpy changes | Pin 3.12 in CI; `minPythonVersion` check locally with a warning, not a hard fail |
| Fixtures increasingly use Sharpy-only features | Verified % decays | Skip categories tracked per-category in trends; `SharpyOnlyType` growth is expected and fine, `StdlibNoEquivalent` growth means the import map needs extending |
| Untrusted-code concern (fixtures run as subprocesses) | Low — fixtures are repo-reviewed | `-I` isolated mode, no network use in fixtures by convention, timeout + output cap |

## Implementation roadmap

| Phase | Work | Exit criteria |
|-------|------|---------------|
| 2a (week 1) | `PythonExecutor`, normalizer chain, `DeviationCatalog` loader against real YAML, models | Unit tests green; catalog parses |
| 2b (weeks 1–2) | Transpiler v1 whitelist (statements/expressions/builtins), golden tests, whitelist-completeness test | ≥40% of fixtures translate |
| 2c (week 2) | Runner + three-way comparison + `oracle run/explain` verbs + `/oracle` skill | Full local run completes; report readable |
| 2d (week 3) | Baseline, PR + nightly workflows, sidecar support, first triage pass over all `Divergent` results | Baseline committed; gate enforcing; zero untriaged `Divergent` |

Dependencies: `Sharpy.TestHarness` skeleton from subsystem 1. Feeds: fixture trust statuses into the unified report; wrong-`.expected` findings become normal PRs fixing fixtures (Critical Rule 1: fix the implementation — or here, the evidence decides which side gets fixed).
