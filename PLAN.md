# Implementation Plan: Task 0.1.5.6 - Return Type Validation

## Overview

Implement return type validation to ensure all code paths in non-void functions return a compatible type.

## Current State Analysis

### Existing Infrastructure
1. **ControlFlowValidator** (`src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs`):
   - Already validates that non-void functions return in all code paths
   - Tracks `(alwaysReturns, alwaysExits)` tuples through control flow
   - Reports error: "Function '{name}' must return a value of type '{type}' in all code paths"

2. **TypeChecker.CheckReturn()** (`src/Sharpy.Compiler/Semantic/TypeChecker.cs:637-660`):
   - Already validates individual return statement types against `_currentFunctionReturnType`
   - Handles bare `return` for void functions
   - Reports type mismatch errors

### What's Already Working
- Return type checking for individual return statements ✓
- Control flow analysis ensuring all paths return ✓
- `-> None` functions allowing bare return ✓
- `-> None` functions disallowing value returns ✓
- `__init__` methods treated as void ✓
- Functions without return annotation default to void ✓

## Analysis: Is a New File Needed?

**Recommendation: No new file required.**

The existing infrastructure already handles the task requirements:
- `ControlFlowValidator.ValidateFunction()` ensures all paths return
- `TypeChecker.CheckReturn()` validates return types

The task description may have anticipated needing a separate checker, but the existing architecture already addresses these concerns effectively.

## Implementation Steps

### Step 1: Verify Existing Coverage
**Files**: Tests in `TypeCheckerTests.cs`, `ControlFlowTests.cs`
**Action**: Confirm existing tests cover the required scenarios

Key test scenarios:
- [x] Missing return in non-void function (already covered by ControlFlowValidator)
- [x] Wrong return type (TypeChecker.CheckReturn)
- [x] Bare return in void function (allowed)
- [x] Value return in void function (error)

### Step 2: Add Missing Tests (if any)
**Files**: `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs`

Add tests for edge cases:
```python
# Test 1: Missing return on else path
def maybe_double(x: int) -> int:
    if x > 0:
        return x * 2
    # Error: missing return

# Test 2: All paths covered
def maybe_double(x: int) -> int:
    if x > 0:
        return x * 2
    else:
        return 0  # OK

# Test 3: Multiple return types must be compatible
def get_value(flag: bool) -> int:
    if flag:
        return 42
    else:
        return "hello"  # Error: incompatible type

# Test 4: -> None allows omitting return entirely
def greet(name: str) -> None:
    print(f"Hello, {name}")
    # OK: no return needed

# Test 5: -> None allows bare return
def maybe_greet(name: str, verbose: bool) -> None:
    if not verbose:
        return  # OK: bare return
    print(f"Hello, {name}")

# Test 6: -> None disallows value return
def bad_greeter() -> None:
    return "Hello"  # Error
```

### Step 3: Enhance Error Messages (Optional)
**Files**: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs`

Current message: "Function '{name}' must return a value of type '{type}' in all code paths"

Consider enhancing to specify which paths are missing returns (optional improvement).

## Key Files to Modify

| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs` | MODIFY | Add comprehensive tests |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | NO CHANGE | Already handles return type validation |
| `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs` | NO CHANGE | Already handles "all paths return" check |
| `src/Sharpy.Compiler/Semantic/ReturnTypeChecker.cs` | NOT NEEDED | Existing infrastructure sufficient |

## Tests to Verify

### Existing Tests to Validate
1. `TypeCheckerTests.ChecksFunctionReturnType` - Correct return type
2. `TypeCheckerTests.DetectsWrongReturnType` - Wrong return type detection
3. `TypeCheckerTests.FunctionWithNoneReturnTypeCannotReturnValue` - None cannot return value
4. `TypeCheckerTests.FunctionWithNoneReturnTypeCanHaveEmptyReturn` - None allows bare return

### New Tests to Add
1. `DetectsMissingReturnInElseBranch` - The example from the task description
2. `AllowsOmittedReturnInVoidFunction` - -> None functions need no return
3. `DetectsIncompatibleReturnTypesInDifferentBranches` - Multiple returns must match
4. `AllowsEarlyReturnInVoidFunction` - -> None allows bare return for early exit
5. `DetectsPartialReturnCoverage` - Complex control flow missing return

## Potential Risks/Questions

### Risks
1. **Edge case: Exceptions** - Does `raise` count as returning? (Yes, ControlFlowValidator marks it as `alwaysExits=true` but `alwaysReturns=false`, which is correct)

2. **Edge case: Infinite loops** - `while True:` with break only. ControlFlowValidator correctly handles this by not counting loops as guaranteed to execute.

3. **Edge case: Match statements** - If match is added, needs handling. Currently not in scope.

### Questions to Clarify
1. Should we create `ReturnTypeChecker.cs` as a separate file even though functionality exists?
   - **Recommendation**: No, keep it simple. The existing architecture works.

2. Should error messages be enhanced to show which specific paths are missing returns?
   - **Recommendation**: Optional improvement, not critical for this task.

## Verification Criteria

The implementation is complete when:
1. All existing return-related tests pass
2. New tests cover the scenarios from the task description
3. The example `maybe_double` produces an error about missing return
4. `-> None` functions work correctly with/without return statements
5. No regressions in the test suite

## Summary

**This task is essentially verifying and testing existing functionality rather than implementing new code.** The Sharpy compiler already has:
- Return type checking via `TypeChecker.CheckReturn()`
- "All paths return" checking via `ControlFlowValidator.ValidateFunction()`

The main work is:
1. Write comprehensive tests to verify these work as expected
2. Potentially enhance error messages for better developer experience
3. Document the behavior

**No new `ReturnTypeChecker.cs` file is needed** unless the design preference is to extract this logic into a separate class for modularity (which would be a refactoring task, not new functionality).
