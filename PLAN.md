# Implementation Plan: Task 0.1.4.6 - Phase 0.1.4 Integration Tests

## Executive Summary

**Task**: Create Phase 0.1.4 Integration Tests for control flow features.
**File**: `src/Sharpy.Compiler.Tests/Integration/Phase014IntegrationTests.cs`
**Status**: New file to be created

---

## Step-by-Step Implementation Approach

### Step 1: Create the Test File Structure

Create `Phase014IntegrationTests.cs` following the established pattern from `Phase013IntegrationTests.cs`:

```csharp
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.4: Control flow statements (if/elif/else, while, for, break, continue).
/// These tests verify the full compilation pipeline for control flow features.
/// </summary>
public class Phase014IntegrationTests : IntegrationTestBase
{
    public Phase014IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    // Test regions organized by feature
}
```

### Step 2: Implement Spec Example Tests

#### 2a. Factorial Example (from task description)
```python
n: int = 5
result: int = 1
while n > 1:
    result *= n
    n -= 1
# result should be 120
```
- This tests: while loop, comparison operators, augmented assignment, variable updates

#### 2b. FizzBuzz-Style Logic (from task description)
```python
for i in range(1, 16):
    if i % 15 == 0:
        pass  # "FizzBuzz"
    elif i % 3 == 0:
        pass  # "Fizz"
    elif i % 5 == 0:
        pass  # "Buzz"
    else:
        pass  # number
```
- This tests: for loop, range(), if/elif/else chains, modulo operator

### Step 3: Organize Test Regions

Following Phase013IntegrationTests pattern, create these regions:

1. **#region Spec Example Tests** - Task-specified examples
2. **#region While Loop Tests** - While loop functionality
3. **#region For Loop Tests** - For loop with range()
4. **#region If/Elif/Else Tests** - Conditional branching
5. **#region Break and Continue Tests** - Loop control statements
6. **#region Nested Control Flow Tests** - Combined constructs
7. **#region Error Cases** - Control flow validation errors

### Step 4: Implement Each Test Category

#### While Loop Tests
- Simple counter pattern
- Factorial calculation (verifies result via print)
- Multiple variable updates
- Complex conditions (and/or)

#### For Loop Tests
- range(n) - single argument
- range(start, stop) - two arguments
- range(start, stop, step) - three arguments
- Accumulator pattern (sum)

#### If/Elif/Else Tests
- Simple if condition
- if/else branch
- Multiple elif chains
- Nested if statements

#### Break and Continue Tests
- While loop with break
- While loop with continue
- For loop with break
- For loop with continue

#### Nested Control Flow Tests
- Nested for loops
- For loop inside while
- If statements inside loops
- Complex FizzBuzz-style logic

#### Error Cases
- Break outside loop → compilation error
- Continue outside loop → compilation error

---

## Key Files to Modify

| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler.Tests/Integration/Phase014IntegrationTests.cs` | **CREATE** | New test file |

---

## Tests to Verify

After implementation, run:
```bash
cd src/Sharpy.Compiler.Tests
dotnet test --filter "Phase014IntegrationTests"
```

All tests should pass. Additionally, verify no regressions:
```bash
dotnet test --filter "ControlFlowTests"
```

---

## Potential Risks or Questions

### Risks

1. **Overlap with ControlFlowTests.cs**: The existing `ControlFlowTests.cs` has extensive control flow coverage. Phase014IntegrationTests should:
   - Focus on spec examples explicitly from the task
   - Be organized by the Phase 0.1.4 structure
   - Not duplicate existing tests unnecessarily

2. **Output Verification**: Some tests (like factorial) produce no output unless we add print statements. Tests should either:
   - Print the result and assert on output
   - Or just verify compilation succeeds (for "compiles and runs" style tests)

3. **FizzBuzz Example Truncated**: The task description shows an incomplete FizzBuzz example (cut off at `elif`). I'll complete it following the standard FizzBuzz pattern:
   - i % 15 == 0 → "FizzBuzz"
   - i % 3 == 0 → "Fizz"
   - i % 5 == 0 → "Buzz"
   - else → number

### Questions

1. **Output vs Compilation-only Tests**: Should tests verify output (like Phase013 does for some tests) or just verify compilation succeeds?
   - **Recommendation**: Include output verification for spec examples to demonstrate correctness

2. **Completeness**: Should Phase014IntegrationTests be comprehensive or minimal (just spec examples)?
   - **Recommendation**: Implement spec examples plus representative tests for each control flow feature, avoiding duplication with ControlFlowTests.cs

---

## Implementation Order

1. Create file with class structure and constructor
2. Implement `#region Spec Example Tests` (factorial, FizzBuzz)
3. Implement `#region While Loop Tests`
4. Implement `#region For Loop Tests`
5. Implement `#region If/Elif/Else Tests`
6. Implement `#region Break and Continue Tests`
7. Implement `#region Nested Control Flow Tests`
8. Implement `#region Error Cases`
9. Run tests and verify all pass
10. Run full test suite to check for regressions

---

## Estimated Test Count

Based on the task requirements and Phase013IntegrationTests pattern:
- Spec Example Tests: 2-3 tests
- While Loop Tests: 4-6 tests
- For Loop Tests: 4-5 tests
- If/Elif/Else Tests: 4-5 tests
- Break and Continue Tests: 4-5 tests
- Nested Control Flow Tests: 2-3 tests
- Error Cases: 2-3 tests

**Total: ~25-30 tests**
