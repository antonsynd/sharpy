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
3. Build first: `dotnet build sharpy.sln > .claude/tmp/last-gap-analysis.log 2>&1`
   - If build fails: Print "=== BUILD FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-gap-analysis.log`, then echo "=== Full log: .claude/tmp/last-gap-analysis.log ===". Stop.
4. Run: `dotnet test --filter "Category=GapDiscovery" --no-build >> .claude/tmp/last-gap-analysis.log 2>&1`
5. Check exit code:
   - Exit 0: Print "=== GAP ANALYSIS PASSED ===" then `tail -100 .claude/tmp/last-gap-analysis.log`
   - Exit non-zero: Print "=== GAP ANALYSIS FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-gap-analysis.log`
6. Read and summarize any JSON reports in `.claude/tmp/`:
   - `hover-fuzz-report.json` — crash count, null symbol/type count, unknown type count, coverage %
   - `completion-fuzz-report.json` — crash count, null/unknown receiver count, missing member count, coverage %
   - `diagnostic-sweep-report.json` — pass/fail/crash counts, unexpected diagnostics, advisory warnings
   - `semantic-token-coverage-report.json` — AST node coverage, unused token types, low-coverage files
7. Present a unified summary table with all metrics
8. Echo "Full log: .claude/tmp/last-gap-analysis.log"
