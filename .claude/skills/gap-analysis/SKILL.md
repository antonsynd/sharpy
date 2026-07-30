---
name: gap-analysis
description: Run all gap discovery tests and present a unified summary
argument-hint: ""
---

Run all gap discovery tests (fuzz, coverage, diagnostic sweep) and present a unified summary of results.

**Usage:** `/gap-analysis`

**Behavior:**
- Runs all tests in the GapDiscovery category
- Reads JSON reports from `.claude/tmp/`
- Presents a unified summary: crash count, anomaly count, coverage metrics
- Shows last 80 lines on failure + points to full log

**Log location:** `.claude/tmp/last-gap-analysis.log`

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-gap-analysis.log`
3. Build first: `.claude/scripts/dotnet-serialized build sharpy.sln > .claude/tmp/last-gap-analysis.log 2>&1`
   - If build fails: Print "=== BUILD FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-gap-analysis.log`, then echo "=== Full log: .claude/tmp/last-gap-analysis.log ===". Stop.
   - (Raw `dotnet build`/`dotnet test` are blocked by the serialization hook — always use the wrapper here.)
4. Run: `.claude/scripts/dotnet-serialized test --filter "Category=GapDiscovery" --no-build >> .claude/tmp/last-gap-analysis.log 2>&1`
5. Check exit code:
   - Exit 0: Print "=== GAP ANALYSIS PASSED ===" then `tail -100 .claude/tmp/last-gap-analysis.log`
   - Exit non-zero: Print "=== GAP ANALYSIS FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-gap-analysis.log`
6. Read and summarize any JSON reports in `.claude/tmp/`:
   - `hover-fuzz-report.json` — crash count, null symbol/type count, unknown type count, coverage %
   - `completion-fuzz-report.json` — crash count, null/unknown receiver count, missing member count, coverage %
   - `diagnostic-sweep-report.json` — pass/fail/crash counts, unexpected diagnostics, advisory warnings
   - `semantic-token-coverage-report.json` — AST node coverage, unused token types, low-coverage files
   - `interop-conformance-report.json` (#1034) — members enumerated, snippets generated, pass/fail/crash per usage position, non-allowlisted failures (the ratchet), and `byPosition`/`byModule` breakdowns. The interop sweep is heavier (~1–2 min); it is excluded from the fast Compiler step and runs in its own CI step. When it fails, the offending `module::kind::member::position` keys need either a bridge fix (file an issue) or a justified entry in `src/Sharpy.Compiler.Tests/Conformance/interop-allowlist.txt`.
   - `generic-reference-conformance-report.json` (#1143) — the 158-cell `callee[T,...]` matrix (callee kind × usage form × arity): cells by outcome (`ok`/`deliberateDiagnostic`/`ice`/`subscriptMisfire`/`csLeak`/`wrongOutput`) and `nonAllowlistedFailures` (the ratchet). Failing cell keys need a fix issue or a justified entry in `src/Sharpy.Compiler.Tests/Conformance/generic-reference-allowlist.txt`.
   - `frontend-parity-report.json` (#1144) — fixtures × entry points (Analyze/Compile/REPL/LSP): matches, justified-normalized skips, and violations (the ratchet, `src/Sharpy.Lsp.Tests/Conformance/frontend-parity-allowlist.txt`). Every normalization rule cites its tracking issue; a new violation means an entry point drifted.
   - `metamorphic-corpus-report.json` (#1157) — every executing fixture × the 9 semantics-preserving transforms (~14,600 cells): cells by outcome (`ok`/`notApplicable`/`diagRegression`/`ice`/`csLeak`/`crash`), a per-transform breakdown, `nonAllowlistedFailures` and `staleAllowlistEntries` (the ratchet, `src/Sharpy.Compiler.Tests/Conformance/metamorphic-allowlist.txt`), plus `wallSeconds` against the ≤5-minute budget. A violating cell means a program the compiler handles stopped compiling — or started mis-emitting — purely because it was rewritten into an equivalent form. `staleAllowlistEntries` is the drain-on-fix signal: those lines must be deleted. Its execution counterpart (`MetamorphicCorpusInvarianceTests`) is `Category=RandomProperty` and runs under `/property-stress`, not here.
   - `differential-exec-report.json` — Sharpy-compiled stdout vs `python3` over the shared subset: matches, subset-skips, divergences (the ratchet, `src/Sharpy.Compiler.Tests/Conformance/differential-exec-allowlist.txt`; entries are either an issue reference or `DESIGNED` citing `docs/deviations.yaml`). Requires `python3` on PATH — the test soft-skips without it, so a missing report here means the oracle didn't run, not that it passed.

   Ratchet triage (all sweeps): a non-allowlisted failure is either fixed now or filed as an issue and allowlisted citing that issue ("drain on fix"). Allowlists must trend to empty — an empty allowlist is the class contract (#1143–#1146) fully enforced. Never add an entry without an issue reference.
7. Present a unified summary table with all metrics
8. Echo "Full log: .claude/tmp/last-gap-analysis.log"
