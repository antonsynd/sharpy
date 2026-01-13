# Dogfooding Issues Analysis

**Date:** 2026-01-13
**Analyzed by:** GitHub Copilot

## Summary

After analyzing 15 dogfooding failures, the issues fall into the following categories:

### FIXED Issues (13 of 15)

**Root Cause:** Invalid C# namespace generation when file paths contain numeric-starting directory names.

**Fix Applied:** Updated `SimpleToPascalCase` method in `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` to:
1. Sanitize invalid identifier characters (replace `.`, `-`, spaces with `_`)
2. Prefix identifiers starting with digits with `_` to make them valid C# identifiers

The following issues are now resolved:
- `20260113_161408_compilation_failed_0000`
- `20260113_161439_compilation_failed_0001`
- `20260113_161505_compilation_failed_0002`
- `20260113_161532_compilation_failed_0003`
- `20260113_161547_compilation_failed_0004`
- `20260113_161726_compilation_failed_0001`
- `20260113_161834_compilation_failed_0003`
- `20260113_161857_compilation_failed_0004`
- `20260113_162634_compilation_failed_0000`
- `20260113_162647_compilation_failed_0001`
- `20260113_162659_compilation_failed_0002`
- `20260113_162715_compilation_failed_0003`
- `20260113_162727_compilation_failed_0004`

---

## Remaining Issues (2 of 15)

### Issue 1: `20260113_161713_execution_failed_0000`

**Type:** AI Code Generation Issue (NOT a compiler bug)

**Error:**
```
Semantic error: Function 'print' expects 1 or 1-5 arguments but got 4
```

**Root Cause:** The AI-generated test code uses Python-style `print()` with multiple arguments:
```python
print("Analyzing range from", start, "to", end)
```

**Action Required:** Update the dogfooding prompts to explicitly document that Sharpy's `print()` function takes a single value, not multiple arguments. Use string concatenation or f-strings (when supported) instead.

**Location:** `build_tools/sharpy_dogfood/prompts.py`

**Priority:** Low (dogfood tooling, not compiler)

---

### Issue 2: `20260113_161818_compilation_failed_0002`

**Type:** Real Compiler Bug + AI Code Generation Issue

**Errors:**
1. List literal type inference creates `List<object>` instead of `List<int>`
2. AI-generated code uses f-strings which are beyond current phases

**Detailed Analysis:**

The generated Sharpy code contains:
```python
test_numbers: list[int] = [-4, -3, 0, 1, 2, 7, 15, 30]
```

The compiler emits:
```csharp
global::Sharpy.Core.List<int> testNumbers = new global::Sharpy.Core.List<object>() { -4, -3, ... };
```

**Bug:** The collection initializer uses `List<object>` instead of inferring `List<int>` from:
- The explicit type annotation on the left side (`list[int]`)
- Or the element types (all `int` literals)

**Location:** Type inference logic in `RoslynEmitter.cs` for list/collection literals.

**Search Pattern:** Look for `List<object>` in RoslynEmitter, specifically in collection literal generation.

**Priority:** Medium (affects typed collection literals)

**Action Items:**
1. When generating collection literals, prefer the target type annotation if available
2. If no target type, infer from element types
3. Only fall back to `object` when elements have incompatible types

---

## Recommendations

### Immediate (Done)
- ✅ Fix namespace sanitization for invalid C# identifiers

### Short-term (For Next Sprint)
1. Fix list literal type inference bug
2. Add unit tests for namespace generation with edge cases:
   - Paths starting with numbers
   - Paths with special characters
   - Nested numeric directories

### Medium-term (Dogfood Tooling) ✅ DONE
1. ✅ Updated dogfooding prompts to:
   - Explicitly document `print(value)` takes single argument only
   - Warn against f-strings (already had this, reinforced)
   - Explicitly forbid multi-argument print patterns
2. ✅ Added pre-validation regex in `orchestrator.py` to catch:
   - Multi-argument print: `print(a, b, c)` → detected before compilation
   - f-strings were already being caught

---

## Test Commands

```bash
# Verify namespace fix works
dotnet run --project src/Sharpy.Cli -- run dogfood_output/issues/20260113_162727_compilation_failed_0004/source.spy

# Run all compiler tests
dotnet test --filter "FullyQualifiedName~RoslynEmitter"

# Run integration tests
dotnet test --filter "FullyQualifiedName~Integration"
```
