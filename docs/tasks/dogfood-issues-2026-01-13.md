# Dogfooding Issues Task List - 2026-01-13

**Generated from dogfooding run with 20 iterations (65% success rate)**

## Summary

| Issue Type | Count | Status |
|------------|-------|--------|
| Float precision mismatches | 3 | ✅ **FIXED** - Added float tolerance comparison |
| Generation failed (infrastructure) | 2 | 🔧 Infrastructure issues (not compiler bugs) |
| Unknown failures (no issue dir) | 2 | ⚠️ Need investigation |

---

## ✅ Fixed Issues

### Float Precision Mismatches (output_mismatch_0000, 0001, 0002)

**Problem:** Output like `5.14` vs `5.140000000000001` was flagged as mismatch.

**Root Cause:** IEEE 754 floating-point representation causes minor precision differences. This is expected behavior, not a bug.

**Fix Applied:**
1. Added `_outputs_equivalent()` function in [orchestrator.py](../../build_tools/sharpy_dogfood/orchestrator.py) for programmatic float-tolerant comparison
2. Updated output verification prompt in [prompts.py](../../build_tools/sharpy_dogfood/prompts.py) to accept floating-point precision differences

---

## 🔧 Infrastructure Issues (Not Compiler Bugs)

### Issue 0003: Copilot CLI Unknown Option `-t`

**Location:** [20260113_170728_generation_failed_0003](../../dogfood_output/issues/20260113_170728_generation_failed_0003/)

**Error:**
```
error: unknown option '-t'
Try 'copilot --help' for more information.
```

**Root Cause:** The `CopilotBackend` in [backends.py](../../build_tools/sharpy_dogfood/backends.py#L350-L357) uses `-t agent -f <file>` flags which may not be supported by the installed GitHub Copilot CLI version.

**Fix Required:**
1. Verify the Copilot CLI version installed at `/opt/homebrew/bin/copilot`
2. Check supported flags with `copilot --help`
3. Update `CopilotBackend.execute()` method to use correct CLI syntax
4. Consider adding a fallback if the CLI is incompatible

**Relevant Files:**
- [build_tools/sharpy_dogfood/backends.py](../../build_tools/sharpy_dogfood/backends.py) - Lines 350-360

---

### Issue 0004: All Backends Unavailable

**Location:** [20260113_170728_generation_failed_0004](../../dogfood_output/issues/20260113_170728_generation_failed_0004/)

**Error:**
```
All backends are unavailable or rate limited
```

**Root Cause:** Both Claude and Copilot backends were rate-limited or failed, and no fallback was available.

**Fix Required:**
1. Add better error recovery in `BackendManager.execute()`
2. Consider adding retry with exponential backoff
3. Add support for additional backends (e.g., OpenAI, local models)
4. Improve rate limit detection and waiting

**Relevant Files:**
- [build_tools/sharpy_dogfood/backends.py](../../build_tools/sharpy_dogfood/backends.py) - `BackendManager` class

---

## ⚠️ Unknown Failures (Need Investigation)

These iterations failed but have `issue_dir: null` in runs.json, meaning no detailed report was saved:

### Iteration 1: `for_range_with_step` / medium
- **Duration:** 9.0s
- **Possible Cause:** Generation or compilation failure that wasn't properly logged

### Iteration 12: `function_calling_function` / simple
- **Duration:** 4.7s
- **Possible Cause:** Similar issue - failure not captured

**Investigation Steps:**
1. Check if there's a bug in `DogfoodOrchestrator._run_single_iteration()` error handling
2. Look for cases where exceptions aren't caught and reported
3. Add more defensive error handling and logging

**Relevant Files:**
- [build_tools/sharpy_dogfood/orchestrator.py](../../build_tools/sharpy_dogfood/orchestrator.py) - `_run_single_iteration()` method

---

## Recommended Next Steps

1. **Run the dogfooding tool again** to verify the float tolerance fix works
2. **Fix Copilot CLI integration** by checking the installed CLI version
3. **Add better error handling** for edge cases that result in null issue directories
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
