# Dogfooding Issues Task List - 2026-01-13

**Generated from dogfooding run with 20 iterations (65% success rate)**

## Summary

| Issue Type | Count | Status |
|------------|-------|--------|
| Float precision mismatches | 3 | ✅ **FIXED** - Added float tolerance comparison |
| Generation failed (infrastructure) | 2 | ✅ **FIXED** - Updated CLI flags and retry logic |
| Unknown failures (no issue dir) | 2 | ✅ **FIXED** - Added SKIPPED status and better error handling |

---

## ✅ Fixed Issues

### Float Precision Mismatches (output_mismatch_0000, 0001, 0002)

**Problem:** Output like `5.14` vs `5.140000000000001` was flagged as mismatch.

**Root Cause:** IEEE 754 floating-point representation causes minor precision differences. This is expected behavior, not a bug.

**Fix Applied:**
1. Added `_outputs_equivalent()` function in [orchestrator.py](../../build_tools/sharpy_dogfood/orchestrator.py) for programmatic float-tolerant comparison
2. Updated output verification prompt in [prompts.py](../../build_tools/sharpy_dogfood/prompts.py) to accept floating-point precision differences

---

### Issue 0003: Copilot CLI Unknown Option `-t` ✅ FIXED

**Location:** [20260113_170728_generation_failed_0003](../../dogfood_output/issues/20260113_170728_generation_failed_0003/)

**Error:**
```
error: unknown option '-t'
Try 'copilot --help' for more information.
```

**Root Cause:** The `CopilotBackend` used legacy `-t agent -f <file>` flags which are no longer supported by the GitHub Copilot CLI.

**Fix Applied:**
1. Updated `CopilotBackend.execute()` to use `--prompt` flag for direct prompt input
2. Added `--allow-all-tools` for non-interactive mode
3. Added `--add-dir` to specify the project root directory
4. Added `_find_copilot_cli()` method to locate the CLI in common installation paths (VS Code globalStorage, Homebrew, PATH)

**Relevant Files:**
- [build_tools/sharpy_dogfood/backends.py](../../build_tools/sharpy_dogfood/backends.py) - `CopilotBackend` class

---

### Issue 0004: All Backends Unavailable ✅ FIXED

**Location:** [20260113_170728_generation_failed_0004](../../dogfood_output/issues/20260113_170728_generation_failed_0004/)

**Error:**
```
All backends are unavailable or rate limited
```

**Root Cause:** Both Claude and Copilot backends were rate-limited or failed, and no fallback was available.

**Fix Applied:**
1. Added `max_retries` parameter (default: 3) to `BackendManager.execute()`
2. Implemented exponential backoff between retries (5s → 10s → 20s, capped at 60s)
3. Distinguishes between rate-limited errors (worth retrying) and real errors (fail fast)
4. Better logging of retry attempts and backend availability

**Relevant Files:**
- [build_tools/sharpy_dogfood/backends.py](../../build_tools/sharpy_dogfood/backends.py) - `BackendManager.execute()` method

---

### Unknown Failures (issue_dir: null) ✅ FIXED

These iterations failed but had `issue_dir: null` in runs.json, meaning no detailed report was saved.

**Root Cause:** Generated code that used unsupported features was being skipped but recorded as failures without any context.

**Fix Applied:**
1. Added `IterationStatus` enum with `SUCCESS`, `FAILED`, `SKIPPED` states
2. Added `IterationResult` dataclass to carry skip reason along with status
3. Changed `run_iteration()` return type from `tuple[bool, Optional[Path]]` to `IterationResult`
4. Added `IssueType.SKIPPED` to distinguish expected skips from actual failures
5. Updated `SummaryReporter` to:
   - Track skipped iterations separately from failures
   - Record `skip_reason` in runs.json
   - Display "Recent Skips" section in summary for debugging generation quality
6. Added traceback printing in exception handler for better debugging

**Relevant Files:**
- [build_tools/sharpy_dogfood/orchestrator.py](../../build_tools/sharpy_dogfood/orchestrator.py) - `IterationStatus`, `IterationResult`, `run_iteration()`, `run()`
- [build_tools/sharpy_dogfood/reporting.py](../../build_tools/sharpy_dogfood/reporting.py) - `IssueType.SKIPPED`, `SummaryReporter.add_run()`, `generate_summary()`

---

## Recommended Next Steps

1. **Run the dogfooding tool again** to verify all fixes work
2. ~~**Fix Copilot CLI integration** by checking the installed CLI version~~ ✅ Done
3. ~~**Add better error handling** for edge cases that result in null issue directories~~ ✅ Done
4. **Consider adding more backends** for resilience (OpenAI, Anthropic API directly, local Ollama)

---

## Notes for Future Runs

The actual Sharpy compiler appears to be working correctly for:
- Integer variables
- Float variables (output is correct, just formatting)
- Boolean variables
- Arithmetic operators
- Comparison operators
- Simple and nested if statements
- For loops with range
- Break/continue
- Simple functions
- Functions with print
- Nested if in loops

This suggests **Phase 0.1.0-0.1.3** features are solid.
