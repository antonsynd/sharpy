# Dogfood Skipped Cases Analysis

**Date:** 2026-01-24
**Status:** ✅ RESOLVED (5 of 7 cases fixed)

## Executive Summary

Of the 7 skipped dogfood cases, **4 actually compile and run correctly** with the current compiler. The skips were due to issues in the dogfood testing infrastructure (Python expected output validation), not compiler limitations.

| Category | Count | Status |
|----------|-------|--------|
| Infrastructure Issue (works now) | 4 | ✅ Converted to test fixtures |
| Compiler Bug | 1 | ✅ Fixed and added as test fixture |
| Unsupported Features | 2 | ⏳ Pending (requires feature work) |

---

## Cases That Work Now

These 4 cases were skipped due to "Invalid expected output after 3 attempts (Python says: )" — the dogfood infrastructure failed to validate expected output, but the code compiles and runs correctly.

### 1. `skip_class_field_access_0001` ✅ FIXED
**Source:** `dogfood_output/skips/20260124_183320_skip_class_field_access_0001/source.spy`

- Tests basic class field access patterns with a Person class
- **Output matches expected**: Alice, 25, True, 26, False
- **Action:** ✅ Added as `classes/class_person_field_mutation.spy`

### 2. `skip_logical_operators_0002` ✅ FIXED
**Source:** `dogfood_output/skips/20260124_183347_skip_logical_operators_0002/source.spy`

- Tests logical operators (and, or, not) with boolean expressions
- **Output matches expected**: False, True, False, True, True, True, True
- **Action:** ✅ Added as `control_flow/logical_operators_simple.spy`

### 3. `skip_generic_function_0004` ✅ FIXED
**Source:** `dogfood_output/skips/20260124_183853_skip_generic_function_0004/source.spy`

- Tests function calls with different types (identity functions)
- **Output matches expected**: 42, hello, True
- **Action:** ✅ Added as `functions/identity_functions.spy`

### 4. `skip_logical_operators_0005` ✅ FIXED
**Source:** `dogfood_output/skips/20260124_193220_skip_logical_operators_0005/source.spy`

- Tests logical operators with class (LogicGate)
- **Output matches expected**: False, False, False, True, True, True, False, True, False, True
- **Action:** ✅ Added as `classes/logic_gate_class.spy`
- **Note:** Original expected output had an error (line 9 was True, should be False per Python semantics)

---

## Compiler Bug: Keyword Argument Names Not Escaped ✅ FIXED

### `skip_function_keyword_args_0000` ✅ FIXED
**Source:** `dogfood_output/skips/20260124_183254_skip_function_keyword_args_0000/source.spy`

**Problem:** When the code uses `base` as a parameter name and calls the function with keyword arguments:
```python
def calculate_score(base: int, bonus: int = 10, multiplier: int = 1, penalty: int = 0) -> int:
    ...

score1: int = calculate_score(base=50)  # <-- "base" not escaped in call
```

The generated C# correctly escapes the parameter declaration (`@base`), but did NOT escape the keyword argument name:
```csharp
// Parameter declaration was correct:
public static int CalculateScore(int @base, int bonus = 10, ...)

// But keyword argument call was WRONG:
int score1 = CalculateScore(base: 50);  // Should be @base: 50
```

**Root Cause:** In `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`, lines 111, 128, 173, and 394 used `kwarg.Name` directly without calling `NameMangler` to escape C# keywords.

**Fix Applied:**
- Changed all 4 locations to use `NameMangler.ToCamelCase(kwarg.Name)` which handles keyword escaping
- Now generates: `CalculateScore(@base: 50)` correctly

**Action:** ✅ Added as `functions/keyword_args_with_defaults.spy`

---

## Unsupported Features

### `skip_module_imports_multifile_0003` - Multi-file Test
**Source:** `dogfood_output/skips/20260124_183629_skip_module_imports_multifile_0003/`

**Skip Reason:** "analyzer.spy invalid per spec"

**Issue:** The multi-file test has circular dependencies or other import resolution issues. Currently the compiler doesn't resolve imports from the module path correctly when run via CLI.

**Files:**
- `main.spy` - Entry point
- `shapes.spy` - Imports from geometry
- `geometry.spy` - Base shapes and interfaces
- `analyzer.spy` - Imports from shapes and geometry

**Action Options:**
1. Fix multi-file test infrastructure to properly pass module paths
2. Simplify the test to avoid complex import chains
3. Document as known limitation for v0.1.x

### `skip_module_imports_multifile_0006` - List Type Annotation
**Source:** `dogfood_output/skips/20260124_193258_skip_module_imports_multifile_0006/`

**Skip Reason:** "Unsupported feature in shapes.spy: Line 46: list type annotation (v0.1.11)"

**Issue:** The code uses `list[Shape]` type annotation which is not supported:
```python
def calculate_total_area(shapes: list[Shape]) -> float:
    ...
```

And union types in main.spy:
```python
shapes: list[Circle | Rectangle] = [circle, rectangle]
```

**Action Options:**
1. Wait for list type annotation support (planned feature)
2. Simplify the test to avoid list type annotations
3. Document as known limitation

---

## Actions Taken

### ✅ Completed
1. **Added 5 working tests as test fixtures:**
   - `classes/class_person_field_mutation.spy` (from skip_class_field_access_0001)
   - `control_flow/logical_operators_simple.spy` (from skip_logical_operators_0002)
   - `functions/identity_functions.spy` (from skip_generic_function_0004)
   - `classes/logic_gate_class.spy` (from skip_logical_operators_0005)
   - `functions/keyword_args_with_defaults.spy` (from skip_function_keyword_args_0000)

2. **Fixed keyword argument escaping bug:**
   - File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`
   - Lines: 111, 128, 173, 394
   - Applied `NameMangler.ToCamelCase()` for consistent keyword escaping

### ⏳ Remaining (Future work)
3. **List type annotations:** Track as a feature request for `list[T]` syntax
4. **Multi-file CLI support:** Improve `-m` module path handling for complex import chains

---

## Appendix: Test File Locations

| Case | Source Path |
|------|-------------|
| class_field_access | `dogfood_output/skips/20260124_183320_skip_class_field_access_0001/source.spy` |
| logical_operators_simple | `dogfood_output/skips/20260124_183347_skip_logical_operators_0002/source.spy` |
| identity_functions | `dogfood_output/skips/20260124_183853_skip_generic_function_0004/source.spy` |
| logical_operators_class | `dogfood_output/skips/20260124_193220_skip_logical_operators_0005/source.spy` |
| keyword_args (bug) | `dogfood_output/skips/20260124_183254_skip_function_keyword_args_0000/source.spy` |
| multi_file_0003 | `dogfood_output/skips/20260124_183629_skip_module_imports_multifile_0003/` |
| multi_file_0006 | `dogfood_output/skips/20260124_193258_skip_module_imports_multifile_0006/` |
