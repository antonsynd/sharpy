# Implementation Plan: Task R-0.1.4.1 - Loop `else` Clause

## Summary

**Surprising Finding**: The loop `else` clause feature is **already fully implemented** in the codebase. The AST, parser, and code generation all support `for...else` and `while...else` constructs. However, **no tests exist** to verify this functionality.

## Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| AST Definition | ✅ Complete | `Statement.cs:136-152` - `WhileStatement.ElseBody` and `ForStatement.ElseBody` |
| Parser | ✅ Complete | `Parser.cs:766-845` - Both `ParseWhileStatement` and `ParseForStatement` handle `else` |
| Code Generation | ✅ Complete | `RoslynEmitter.cs:1681-1755` - Boolean flag pattern implemented |
| Helper Methods | ✅ Complete | `RoslynEmitter.cs:2983-3029` - `TransformLoopBodyForElse`, `BreakWithFlagStatement` |
| Tests | ❌ Missing | No integration or unit tests for loop else |

## Spec Reference

From `docs/language_specification/loop_else.md`:
- The `else` clause executes **only if the loop completes normally** (without `break`)
- Does NOT run if loop exits via `return` or exception
- Implementation uses boolean flag pattern:
  ```csharp
  bool _loopCompleted = true;
  foreach (var item in items) {
      if (item == target) { _loopCompleted = false; break; }
  }
  if (_loopCompleted) { /* else body */ }
  ```

## Step-by-Step Implementation

### Step 1: Verify Existing Implementation Works (Manual Test)

Before writing tests, manually verify the feature compiles and runs:

```python
# Test file: test_loop_else.spy
for i in range(3):
    print(i)
else:
    print("completed")
```

Run with the Sharpy compiler to confirm it produces expected output.

### Step 2: Add Integration Tests

File: `src/Sharpy.Compiler.Tests/Integration/ControlFlowTests.cs`

Add the following test cases:

1. **ForLoop_WithElse_NoBreak_ElseExecutes**
   - For loop completes normally → else runs

2. **ForLoop_WithElse_WithBreak_ElseDoesNotExecute**
   - For loop exits via break → else does NOT run

3. **WhileLoop_WithElse_NoBreak_ElseExecutes**
   - While loop condition becomes false → else runs

4. **WhileLoop_WithElse_WithBreak_ElseDoesNotExecute**
   - While loop exits via break → else does NOT run

5. **ForLoop_WithElse_BreakInNestedIf_ElseDoesNotExecute**
   - Break inside `if` statement still prevents else

6. **NestedLoops_WithElse_InnerBreakDoesNotAffectOuterElse**
   - Break in inner loop only affects inner loop's else, not outer

7. **ForLoop_WithElse_ContinueDoesNotAffectElse**
   - `continue` does NOT prevent else from executing

8. **WhileLoop_WithElse_EmptyLoop_ElseExecutes**
   - Loop body never executes (condition false initially) → else runs

9. **MultipleLoopsWithElse_UniqueFlagNames**
   - Multiple loops with else in same scope work correctly

### Step 3: Add Parser Tests (Optional)

File: `src/Sharpy.Compiler.Tests/Parser/ParserTests.cs`

Add tests to verify AST structure:
- `ParseWhileStatement_WithElseClause_ParsesElseBody`
- `ParseForStatement_WithElseClause_ParsesElseBody`

### Step 4: Run Tests and Fix Any Issues

```bash
cd src/Sharpy.Compiler.Tests
dotnet test --filter "ControlFlowTests"
```

## Key Files

1. **`src/Sharpy.Compiler/Parser/Ast/Statement.cs`** (lines 136-152)
   - `WhileStatement` and `ForStatement` records with `ElseBody` property

2. **`src/Sharpy.Compiler/Parser/Parser.cs`** (lines 766-845)
   - `ParseWhileStatement()` and `ParseForStatement()` methods

3. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`**
   - `GenerateWhile()` (lines 1681-1718)
   - `GenerateFor()` (lines 1720-1755)
   - `TransformLoopBodyForElse()` (lines 2983-3029)
   - `GenerateBreakWithFlag()` (lines 1532-1541)

4. **`src/Sharpy.Compiler.Tests/Integration/ControlFlowTests.cs`**
   - Test file where new tests should be added

## Tests to Verify

### Required Integration Tests

```csharp
// 1. For loop else - no break
[Fact]
public void ForLoop_WithElse_NoBreak_ElseExecutes()
{
    var source = @"
for i in range(3):
    print(i)
else:
    print(""done"")
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    Assert.Equal("0\n1\n2\ndone\n", result.StandardOutput);
}

// 2. For loop else - with break
[Fact]
public void ForLoop_WithElse_WithBreak_ElseDoesNotExecute()
{
    var source = @"
for i in range(5):
    if i == 2:
        break
    print(i)
else:
    print(""done"")
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    Assert.Equal("0\n1\n", result.StandardOutput);  // No "done"
}

// 3. While loop else - no break
[Fact]
public void WhileLoop_WithElse_NoBreak_ElseExecutes()
{
    var source = @"
i: int = 0
while i < 3:
    print(i)
    i = i + 1
else:
    print(""done"")
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    Assert.Equal("0\n1\n2\ndone\n", result.StandardOutput);
}

// 4. While loop else - with break
[Fact]
public void WhileLoop_WithElse_WithBreak_ElseDoesNotExecute()
{
    var source = @"
i: int = 0
while True:
    if i >= 2:
        break
    print(i)
    i = i + 1
else:
    print(""done"")
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    Assert.Equal("0\n1\n", result.StandardOutput);  // No "done"
}

// 5. Nested loops - inner break doesn't affect outer else
[Fact]
public void NestedLoops_WithElse_InnerBreakDoesNotAffectOuterElse()
{
    var source = @"
for i in range(2):
    for j in range(5):
        if j == 1:
            break
        print(f""{i},{j}"")
    else:
        print(f""inner done {i}"")
else:
    print(""outer done"")
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    // Inner else never runs (break), but outer else runs
    Assert.Equal("0,0\n1,0\nouter done\n", result.StandardOutput);
}

// 6. Continue does not affect else
[Fact]
public void ForLoop_WithElse_ContinueDoesNotAffectElse()
{
    var source = @"
for i in range(4):
    if i == 2:
        continue
    print(i)
else:
    print(""done"")
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
    Assert.Equal("0\n1\n3\ndone\n", result.StandardOutput);
}
```

## Potential Risks and Questions

### Risks

1. **Edge case with empty iterables**: Need to verify else runs when loop body never executes (e.g., `for i in range(0)`).

2. **Interaction with `return`**: The boolean flag pattern naturally handles this (flag check is unreachable), but should add test in a function context.

3. **Nested loop flag naming**: The `GenerateTempVarName("loopCompleted")` uses a counter, so multiple loops should get unique names (`__loopCompleted_0`, `__loopCompleted_1`, etc.). Need to verify.

4. **Match statement**: If `break` can appear inside a `match` statement within a loop, `TransformLoopBodyForElse` may need to handle `MatchStatement` recursively (currently not handled).

### Questions

1. **Is there a `MatchStatement` AST node?** If so, breaks inside match cases may need transformation.

2. **Are there any existing E2E tests that might already cover this?** The grep search didn't find any, but worth double-checking.

3. **Should parser tests verify error cases?** (e.g., `else` without preceding `for`/`while`)

## Conclusion

The primary work is **writing comprehensive tests** for the already-implemented feature. The implementation appears complete and follows the spec correctly. Once tests pass, the task can be marked complete.
