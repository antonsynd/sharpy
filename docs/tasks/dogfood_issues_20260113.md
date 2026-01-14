# Dogfood Issues Analysis - January 13, 2026

## Summary

Analyzed 50 dogfooding iterations:
- **29 successful** (58%)
- **19 failed** (38%)
- **2 skipped** (4%)

### Issue Breakdown
| Type | Count | Status |
|------|-------|--------|
| `execution_failed` | 2 | ✅ Fixed (builtin shadowing) |
| `output_mismatch` | 2 | ❌ False positive (AI generated wrong expected output) |
| `generation_failed` | 15 | N/A (rate limiting, no data) |
| `skipped` | 2 | Expected (unsupported features) |

---

## ✅ Fixed Issues

### Issue: Function name conflicts with builtins (execution_failed)

**Status:** FIXED

**Affected tests:**
- `20260113_173959_execution_failed_0000` - `simple_function/medium`
- `20260113_174012_execution_failed_0001` - `simple_function/simple`

**Problem:** User-defined functions named `double` conflicted with the builtin `double()` type conversion function, causing:
```
Semantic error at line N, column 1: Function 'double' is already defined
```

**Root cause:** The `NameResolver` and `Scope.Define` were treating builtins the same as user-defined symbols, preventing shadowing.

**Fix applied:** Modified [src/Sharpy.Compiler/Semantic/NameResolver.cs](../../src/Sharpy.Compiler/Semantic/NameResolver.cs#L262-L274) and [src/Sharpy.Compiler/Semantic/Scope.cs](../../src/Sharpy.Compiler/Semantic/Scope.cs#L32-L39) to allow shadowing builtins (identified by `DeclarationLine == null`).

**Test added:** `AllowsShadowingBuiltinFunction` in [SemanticAnalyzerNegativeTests.cs](../../src/Sharpy.Compiler.Tests/Semantic/SemanticAnalyzerNegativeTests.cs)

---

## ❌ False Positive Issues (No Compiler Fix Needed)

### Issue: AI generated incorrect expected output (output_mismatch)

**Status:** NOT A BUG - Dogfood tool issue

**Affected tests:**
- `20260113_174158_output_mismatch_0002` - `bool_variables/medium`
- `20260113_174547_output_mismatch_0003` - `comparison_operators/medium`

**Analysis:**

#### Test 0002 (`bool_variables`)
The AI generated Sharpy code that works correctly, but the expected output was wrong:
- `check_conditions(22, False)` should return `True` (temperature 22 is comfortable, not raining)
- The expected output claimed it should return `False`

#### Test 0003 (`comparison_operators`)
The AI generated incorrect expected output for `check_relations(10, 5)`:
- Expected: `10100`
- Actual: `11100`
- Correct answer: `11100` because:
  - 10 < 5? No
  - 10 <= 5? No
  - 10 > 5? Yes → +100
  - 10 >= 5? Yes → +1000
  - 10 != 5? Yes → +10000
  - Total: 11100

**Recommendation:** These are dogfood tool/prompt issues, not compiler bugs. Consider:
1. Adding validation in the dogfood tool to sanity-check expected outputs
2. Running the Python equivalent to verify expected outputs
3. Adding a post-generation verification step

---

## Skipped Tests (Expected)

These were correctly skipped due to unsupported features:

1. **`loop_in_function/medium`** - Generated code invalid per spec
2. **`bool_variables/medium`** - Used `with` statement (not supported)

---

## Rate-Limited Tests (No Data)

15 tests failed with `generation_failed` due to API rate limiting. No issue folders were created for these. Re-run the dogfood suite to get actual results.

---

## ✅ Dogfood Tool Improvements (Implemented)

The following improvements have been made to the dogfood tool:

### 1. Python Verification for Expected Outputs
**File:** [orchestrator.py](../../build_tools/sharpy_dogfood/orchestrator.py)

Added `_verify_expected_with_python()` function that:
- Converts Sharpy code to Python (minimal transformation needed for basic features)
- Runs the Python code to get the actual expected output
- Compares against AI-generated expected output
- Skips iterations where the AI generated incorrect expected outputs

### 2. Builtin Name Collision Guidance
**File:** [prompts.py](../../build_tools/sharpy_dogfood/prompts.py)

Added explicit naming rules to the code generation prompt:
- Lists common builtins to avoid: `double`, `int`, `str`, `float`, `bool`, `len`, `print`, `range`, etc.
- Suggests descriptive alternatives like `double_value`, `multiply_by_two`

### 3. Rate Limit Handling Improvements
**File:** [orchestrator.py](../../build_tools/sharpy_dogfood/orchestrator.py), [reporting.py](../../build_tools/sharpy_dogfood/reporting.py)

- Added `GENERATION_RATE_LIMITED` issue type to distinguish from real generation failures
- Rate-limited iterations are now marked as `SKIPPED` instead of `FAILED`
- Existing exponential backoff retry logic (5s → 10s → 20s → 60s max) was already in place
