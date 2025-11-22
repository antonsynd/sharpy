# Walkthrough: ControlFlowValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs`

---

## 1. Overview

The `ControlFlowValidator` is a semantic analysis component responsible for validating the control flow of Sharpy programs. It ensures that code paths are logically sound and catches common programming errors at compile time.

**Primary Responsibilities:**
- **Unreachable Code Detection**: Identifies statements that can never be executed because all paths leading to them have already exited (via `return`, `raise`, `break`, or `continue`)
- **Return Path Validation**: Ensures functions that declare a return type actually return a value on all code paths
- **Loop Context Validation**: Verifies that `break` and `continue` statements only appear inside loops

**Position in the Compilation Pipeline:**
```
Lexer → Parser → Name Resolution → Type Checking → [Control Flow Validation] → Code Generation
```

The `ControlFlowValidator` is invoked by the `TypeChecker` after type checking each function, ensuring that control flow is validated in the context of already-resolved types.

---

## 2. Class Structure

### Main Class: `ControlFlowValidator`

```csharp
public class ControlFlowValidator
{
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();
    
    private int _loopDepth = 0;
    private bool _inFunction = false;
    
    // Public API
    public IReadOnlyList<SemanticError> Errors => _errors;
    public void ValidateFunction(FunctionDef functionDef, SemanticType returnType);
}
```

### State Tracking Fields

**`_loopDepth` (int):**
- Tracks nesting depth of loops (while, for)
- Incremented when entering a loop, decremented when exiting
- Used to determine if `break`/`continue` are valid at current location
- Example: `_loopDepth = 0` → not in loop, `_loopDepth = 2` → nested 2 loops deep

**`_inFunction` (bool):**
- Tracks whether we're currently validating inside a function body
- Set to `true` when entering `ValidateFunction`, `false` on exit
- Currently used for context tracking (may be extended for nested function handling)

**`_errors` (List\<SemanticError\>):**
- Accumulates all semantic errors found during validation
- Exposed publicly via the read-only `Errors` property
- Errors include line/column information for precise diagnostics

**`_logger` (ICompilerLogger):**
- Used for debug logging during validation
- Defaults to `NullLogger.Instance` if none provided
- Helps trace validation flow during development

---

## 3. Key Methods

### 3.1 Public API: `ValidateFunction`

```csharp
public void ValidateFunction(FunctionDef functionDef, SemanticType returnType)
{
    _logger.LogDebug($"Validating control flow for function: {functionDef.Name}");
    
    _inFunction = true;
    var (alwaysReturns, _) = ValidateBlock(functionDef.Body);
    _inFunction = false;
    
    // Check if function needs to return a value
    if (returnType != SemanticType.Void && !alwaysReturns)
    {
        AddError($"Function '{functionDef.Name}' must return a value of type '{returnType.GetDisplayName()}' in all code paths",
            functionDef.LineStart, functionDef.ColumnStart);
    }
}
```

**Purpose**: Entry point for validating a function's control flow.

**Parameters:**
- `functionDef`: The AST node representing the function to validate
- `returnType`: The semantic type this function must return (resolved by TypeChecker)

**Algorithm:**
1. Set `_inFunction = true` to track context
2. Validate the function body (a list of statements) using `ValidateBlock`
3. Check if the function always returns a value on all paths
4. If the function has a non-void return type but doesn't always return, report an error

**Key Insight**: The validator uses a **data flow analysis** approach. It tracks whether execution "always exits" (via return, raise, break, continue) to determine if all paths return.

**Example Valid Code:**
```python
def get_absolute(x: int) -> int:
    if x >= 0:
        return x
    else:
        return -x  # All paths return
```

**Example Invalid Code:**
```python
def maybe_return(x: int) -> int:
    if x > 0:
        return x
    # ERROR: Missing return on else path
```

---

### 3.2 Core Logic: `ValidateBlock`

```csharp
private (bool, bool) ValidateBlock(List<Statement> statements)
{
    bool alwaysReturns = false;
    bool alwaysExits = false;
    bool hasUnreachableCode = false;
    
    for (int i = 0; i < statements.Count; i++)
    {
        var statement = statements[i];
        
        // Check for unreachable code
        if (alwaysExits && i < statements.Count)
        {
            if (!hasUnreachableCode)
            {
                AddError("Unreachable code detected",
                    statement.LineStart, statement.ColumnStart);
                hasUnreachableCode = true;
            }
            continue;
        }
        
        var (stmtReturns, stmtExits) = ValidateStatement(statement);
        
        if (stmtReturns)
            alwaysReturns = true;
        
        if (stmtExits)
            alwaysExits = true;
    }
    
    return (alwaysReturns, hasUnreachableCode);
}
```

**Purpose**: Validates a sequence of statements (a block), tracking whether execution always returns or exits.

**Return Value**: Tuple of `(alwaysReturns, hasUnreachableCode)`
- `alwaysReturns`: `true` if all execution paths return a value
- `hasUnreachableCode`: `true` if unreachable code was detected (for reporting)

**Algorithm:**
1. Iterate through each statement in order
2. Before processing each statement, check if previous statements already guaranteed exit
3. If so, mark as unreachable (but only report once per block)
4. For each statement, determine if it returns or exits
5. Track cumulative state: if any statement exits, all subsequent statements are unreachable

**Important Design Decision**: The `hasUnreachableCode` flag ensures we only report **one** unreachable code error per block, even if many statements are unreachable. This prevents error spam.

**Example:**
```python
def example():
    return 42      # alwaysExits = true, alwaysReturns = true
    print("A")     # UNREACHABLE - reported
    print("B")     # UNREACHABLE - not reported (hasUnreachableCode already true)
    print("C")     # UNREACHABLE - not reported
```

---

### 3.3 Statement Dispatcher: `ValidateStatement`

```csharp
private (bool, bool) ValidateStatement(Statement statement)
{
    switch (statement)
    {
        case ReturnStatement:
            return (true, true);  // Returns AND exits
        
        case RaiseStatement:
            return (false, true); // Exits but doesn't return a value
        
        case BreakStatement:
            if (_loopDepth == 0)
                AddError("'break' statement outside loop", ...);
            return (false, true);
        
        case ContinueStatement:
            if (_loopDepth == 0)
                AddError("'continue' statement outside loop", ...);
            return (false, true);
        
        case IfStatement ifStmt:
            return ValidateIf(ifStmt);
        
        case WhileStatement whileStmt:
            return ValidateWhile(whileStmt);
        
        case ForStatement forStmt:
            return ValidateFor(forStmt);
        
        case TryStatement tryStmt:
            return ValidateTry(tryStmt);
        
        case FunctionDef:
            // Nested function - don't validate here
            return (false, false);
        
        case ClassDef:
        case StructDef:
        case InterfaceDef:
        case EnumDef:
            // Type definitions don't affect control flow
            return (false, false);
        
        default:
            return (false, false);
    }
}
```

**Purpose**: Dispatches statement validation based on statement type.

**Return Value**: Tuple of `(alwaysReturns, alwaysExits)`

**Key Behaviors:**

| Statement Type | Always Returns? | Always Exits? | Notes |
|---------------|-----------------|---------------|-------|
| `ReturnStatement` | ✅ Yes | ✅ Yes | Definitively returns a value |
| `RaiseStatement` | ❌ No | ✅ Yes | Exits but doesn't return normally |
| `BreakStatement` | ❌ No | ✅ Yes | Validates `_loopDepth > 0` |
| `ContinueStatement` | ❌ No | ✅ Yes | Validates `_loopDepth > 0` |
| `IfStatement` | ⚠️ Depends | ⚠️ Depends | Delegates to `ValidateIf` |
| `WhileStatement` | ❌ No | ❌ No | Loop may not execute |
| `ForStatement` | ❌ No | ❌ No | Loop may not execute |
| `TryStatement` | ⚠️ Depends | ⚠️ Depends | Complex logic in `ValidateTry` |
| `FunctionDef` (nested) | ❌ No | ❌ No | Not validated here |
| Type definitions | ❌ No | ❌ No | Don't affect control flow |
| Other statements | ❌ No | ❌ No | Assignment, expression, etc. |

**Why distinguish "returns" from "exits"?**
- **Returns**: Function completes with a value (satisfies return type requirement)
- **Exits**: Execution stops (makes subsequent code unreachable)
- A `raise` exits but doesn't return (exception path)
- A `return` both exits AND returns

---

### 3.4 Conditional Logic: `ValidateIf`

```csharp
private (bool, bool) ValidateIf(IfStatement ifStmt)
{
    var (thenReturns, _) = ValidateBlock(ifStmt.ThenBody);
    
    bool allBranchesReturn = thenReturns;
    
    // Check elif branches
    foreach (var elifClause in ifStmt.ElifClauses)
    {
        var (elifReturns, _) = ValidateBlock(elifClause.Body);
        allBranchesReturn = allBranchesReturn && elifReturns;
    }
    
    // Check else branch
    if (ifStmt.ElseBody != null && ifStmt.ElseBody.Count > 0)
    {
        var (elseReturns, _) = ValidateBlock(ifStmt.ElseBody);
        allBranchesReturn = allBranchesReturn && elseReturns;
    }
    else
    {
        // No else branch means not all paths return
        allBranchesReturn = false;
    }
    
    return (allBranchesReturn, allBranchesReturn);
}
```

**Purpose**: Determines if an if-elif-else statement guarantees return on all paths.

**Algorithm:**
1. Validate the `then` branch (if body)
2. For each `elif` branch, validate its body
3. If an `else` branch exists, validate it
4. **All branches must return** for the if statement to guarantee return
5. If there's no `else` branch, return is **not** guaranteed (condition might be false)

**Critical Logic**: The if statement only "always returns" when:
```python
if condition:
    return ...    # ✅ returns
elif other:
    return ...    # ✅ returns
else:
    return ...    # ✅ returns - ALL paths covered
```

**Example of Missing Return:**
```python
def example(x: int) -> int:
    if x > 0:
        return x
    # Missing else branch - not all paths return!
```

---

### 3.5 Loop Validation: `ValidateWhile` and `ValidateFor`

```csharp
private (bool, bool) ValidateWhile(WhileStatement whileStmt)
{
    _loopDepth++;
    var (bodyReturns, _) = ValidateBlock(whileStmt.Body);
    _loopDepth--;
    
    // While loop doesn't guarantee execution
    return (false, false);
}

private (bool, bool) ValidateFor(ForStatement forStmt)
{
    _loopDepth++;
    var (bodyReturns, _) = ValidateBlock(forStmt.Body);
    _loopDepth--;
    
    // For loop doesn't guarantee execution
    return (false, false);
}
```

**Purpose**: Validate loop bodies and track loop context for break/continue validation.

**Key Design Decision**: **Loops never guarantee return**, even if the body always returns.

**Why?**
- A `while` loop condition might be false on first check
- A `for` loop might iterate over an empty sequence
- Therefore, the loop body may **never execute**

**Example:**
```python
def example(items: list[int]) -> int:
    for item in items:
        return item  # Loop body returns, but...
    # If items is empty, this point is reached!
    # ERROR: Not all paths return
```

**Loop Depth Tracking**: The `_loopDepth` counter enables proper validation of break/continue:
```python
while x > 0:          # _loopDepth = 1
    for item in lst:  # _loopDepth = 2
        break         # ✅ Valid (_loopDepth > 0)
    continue          # ✅ Valid (_loopDepth > 0)

break                 # ❌ Error (_loopDepth = 0)
```

---

### 3.6 Exception Handling: `ValidateTry`

```csharp
private (bool, bool) ValidateTry(TryStatement tryStmt)
{
    var (tryReturns, _) = ValidateBlock(tryStmt.Body);
    
    bool allHandlersReturn = true;
    foreach (var handler in tryStmt.Handlers)
    {
        var (handlerReturns, _) = ValidateBlock(handler.Body);
        allHandlersReturn = allHandlersReturn && handlerReturns;
    }
    
    bool finallyReturns = false;
    if (tryStmt.FinallyBody != null && tryStmt.FinallyBody.Count > 0)
    {
        var (finReturns, _) = ValidateBlock(tryStmt.FinallyBody);
        finallyReturns = finReturns;
    }
    
    // All paths return if:
    // - Finally returns (overrides everything), OR
    // - Try returns AND all handlers return
    bool allPathsReturn = finallyReturns || (tryReturns && allHandlersReturn);
    
    return (allPathsReturn, allPathsReturn);
}
```

**Purpose**: Validate try-except-finally blocks with complex control flow.

**Algorithm:**
1. Validate the `try` block
2. Validate each `except` handler
3. Validate the `finally` block (if present)
4. Determine if all paths return based on Python semantics

**Critical Logic**: A try statement guarantees return if **either**:
- **The `finally` block returns** (overrides all other exits), OR
- **Both the `try` block AND all exception handlers return**

**Why this logic?**

**Case 1: Finally returns (overrides everything)**
```python
def example() -> int:
    try:
        do_something()
        # might return, might not
    except Exception:
        # might return, might not
    finally:
        return 42  # ✅ ALWAYS executes, guarantees return
```

**Case 2: Try + all handlers return**
```python
def example() -> int:
    try:
        return 1        # ✅ returns
    except ValueError:
        return 2        # ✅ returns
    except Exception:
        return 3        # ✅ returns - all paths covered
```

**Example of Missing Return:**
```python
def example() -> int:
    try:
        return 1
    except ValueError:
        return 2
    except Exception:
        pass  # ❌ This handler doesn't return!
    # ERROR: Not all exception paths return
```

---

## 4. Dependencies

### Internal Dependencies

**From `Sharpy.Compiler.Parser.Ast`:**
- `Statement` and all derived statement types (`IfStatement`, `ReturnStatement`, etc.)
- `FunctionDef` - Function AST node
- Provides the structure being validated

**From `Sharpy.Compiler.Semantic`:**
- `SemanticType` - Type information (especially `SemanticType.Void`)
- `SemanticError` - Error reporting with location info

**From `Sharpy.Compiler.Logging`:**
- `ICompilerLogger` - Debug logging interface
- `NullLogger` - Default no-op logger

### How It's Used

**Primary Consumer: `TypeChecker`**
```csharp
// In TypeChecker.CheckFunction():
_controlFlowValidator.ValidateFunction(functionDef, returnType);

// Errors are collected:
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        var allErrors = new List<SemanticError>(_errors);
        allErrors.AddRange(_controlFlowValidator.Errors);  // ← Added here
        allErrors.AddRange(_accessValidator.Errors);
        return allErrors;
    }
}
```

The `TypeChecker` instantiates a single `ControlFlowValidator` and reuses it for all functions in a module, accumulating errors across all validations.

---

## 5. Patterns and Design Decisions

### 5.1 Visitor-Like Pattern with Return Values

The validator uses a **recursive descent** approach similar to the Visitor pattern, but instead of just visiting nodes, it **propagates control flow information upward**:

```csharp
ValidateFunction
  └─ ValidateBlock
       └─ ValidateStatement
            └─ ValidateIf / ValidateWhile / etc.
                 └─ ValidateBlock (recursive)
```

Each method returns `(bool alwaysReturns, bool alwaysExits)`, allowing parent nodes to make decisions based on child behavior.

### 5.2 Single-Pass Analysis

The validator performs validation in a **single traversal** of the AST, accumulating errors as it goes. This is efficient but means:
- Errors are reported in source order
- No backtracking or multi-phase analysis
- Simple state tracking (`_loopDepth`, `_inFunction`)

### 5.3 Conservative Analysis

The validator is **conservative** (safe):
- **Loops**: Assumed they might not execute (even infinite loops)
- **Conditions**: Assumed runtime values could go either way
- **Exceptions**: All exception handlers must be covered

This prevents false positives but may require explicit else branches:
```python
# Must write:
if condition:
    return x
else:
    return y

# Can't write:
if condition:
    return x
return y  # Validator doesn't know condition is always false if we get here
```

### 5.4 Error Accumulation vs. Fail-Fast

The validator **accumulates errors** rather than failing on first error:
- All errors are collected in `_errors` list
- Validation continues even after finding issues
- Users see all problems at once (better UX)

However, **unreachable code** reporting is limited to **one error per block** to avoid spam.

### 5.5 Separation of Concerns

Control flow validation is **separate from**:
- **Type checking** (handled by `TypeChecker`)
- **Name resolution** (handled by `NameResolver`)
- **Access validation** (handled by `AccessValidator`)

This modular design makes each component simpler and more testable.

---

## 6. Debugging Tips

### 6.1 Enable Debug Logging

Pass a real logger to see validation flow:
```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
var validator = new ControlFlowValidator(logger);
```

You'll see output like:
```
[DEBUG] Validating control flow for function: calculate
```

### 6.2 Inspect Error Details

`SemanticError` includes precise location information:
```csharp
foreach (var error in validator.Errors)
{
    Console.WriteLine($"Line {error.Line}, Col {error.Column}: {error.Message}");
}
```

### 6.3 Common Issues and Fixes

**Issue**: Function reported as not returning, but it looks correct
```python
def example(x: int) -> int:
    if x > 0:
        return x
    # Missing else!
```
**Fix**: Add explicit `else` branch or unconditional return after if.

**Issue**: "Unreachable code" on code that looks reachable
```python
while True:
    if condition:
        break
    return x  # Looks reachable, but validator sees "return" before "break"
```
**Fix**: Reorder logic or restructure control flow.

**Issue**: "break/continue outside loop" on valid code
- **Check**: Is there a nested function? Loops don't span function boundaries.
- **Check**: Is `_loopDepth` being decremented properly? (Implementation bug)

### 6.4 Test with Minimal Examples

When debugging validation logic, create minimal test cases:
```csharp
[Fact]
public void TestIfWithoutElse_DoesNotGuaranteeReturn()
{
    var source = """
        def func(x: int) -> int:
            if x > 0:
                return x
        """;
    
    var errors = validator.Errors;
    Assert.Contains(errors, e => e.Message.Contains("must return"));
}
```

### 6.5 Watch for State Bugs

Common state-related bugs:
- Forgetting to increment/decrement `_loopDepth`
- Not resetting `_inFunction` on exception
- Reusing validator instance incorrectly (state pollution)

**Best Practice**: Create a fresh `ControlFlowValidator` per module or clear state between uses.

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

**Add new validation logic when:**
- New control flow constructs are added to Sharpy (e.g., `match` statement, `with` statement)
- New exit mechanisms are introduced (e.g., `yield`, `async`/`await`)
- Edge cases in existing logic are discovered

**Enhance validation when:**
- False positives are reported (too strict)
- False negatives are found (missing error detection)
- Better error messages are needed

### 7.2 How to Add a New Statement Type

**Example: Adding `match` statement validation**

1. **Add the case to `ValidateStatement`:**
```csharp
case MatchStatement matchStmt:
    return ValidateMatch(matchStmt);
```

2. **Implement the validation method:**
```csharp
private (bool, bool) ValidateMatch(MatchStatement matchStmt)
{
    bool allCasesReturn = true;
    
    foreach (var case in matchStmt.Cases)
    {
        var (caseReturns, _) = ValidateBlock(case.Body);
        allCasesReturn = allCasesReturn && caseReturns;
    }
    
    // Match only guarantees return if it's exhaustive
    // (This would require checking if all patterns are covered)
    bool isExhaustive = matchStmt.HasDefaultCase;
    
    return (isExhaustive && allCasesReturn, isExhaustive && allCasesReturn);
}
```

3. **Add tests** (in `Sharpy.Compiler.Tests/Semantic/ControlFlowValidatorTests.cs`):
```csharp
[Fact]
public void TestMatch_AllCasesReturn_GuaranteesReturn()
{
    var source = """
        def classify(x: int) -> str:
            match x:
                case 0:
                    return "zero"
                case _:
                    return "other"
        """;
    
    // Should not error - all cases return
    AssertNoErrors(source);
}
```

### 7.3 Testing Best Practices

**CRITICAL: Never artificially make tests pass!**
- ❌ Don't change test expectations to match bugs
- ❌ Don't skip failing tests without understanding why
- ✅ Fix the implementation to make tests pass
- ✅ Add tests for bug fixes and new features

**Test coverage should include:**
- **Positive cases**: Valid code that should pass
- **Negative cases**: Invalid code that should error
- **Edge cases**: Empty blocks, nested constructs, etc.
- **Regression tests**: For bugs that were fixed

### 7.4 Code Style Guidelines

**Follow existing patterns:**
- Use tuple returns for control flow state `(bool, bool)`
- Maintain immutability (don't modify AST nodes)
- Keep methods focused (single responsibility)
- Add XML doc comments for public methods

**Example of good style:**
```csharp
/// <summary>
/// Validates a with statement (context manager)
/// </summary>
/// <param name="withStmt">The with statement to validate</param>
/// <returns>Tuple of (alwaysReturns, alwaysExits)</returns>
private (bool, bool) ValidateWith(WithStatement withStmt)
{
    var (bodyReturns, _) = ValidateBlock(withStmt.Body);
    
    // With blocks always execute (unlike loops)
    return (bodyReturns, bodyReturns);
}
```

### 7.5 Performance Considerations

**Current performance is O(n)** where n = number of AST nodes.

**Keep it efficient:**
- Avoid repeated traversals
- Don't build auxiliary data structures unless necessary
- Use short-circuit evaluation (`&&`, `||`)

**If performance becomes an issue:**
- Consider caching validation results
- Profile before optimizing
- Document any optimizations with comments

### 7.6 Suggested Enhancements

**Future improvements could include:**

1. **Better unreachable code detection**:
   - Detect constant conditions (`if True:`, `while False:`)
   - Track definite assignment state

2. **More precise loop analysis**:
   - Detect provably infinite loops (`while True:` without break)
   - Handle `else` clauses on loops

3. **Exception flow analysis**:
   - Track which exceptions can be raised
   - Validate all exception types are handled

4. **Data flow integration**:
   - Detect uninitialized variable use
   - Track nullable type flow

5. **Warnings vs. Errors**:
   - Make some validations warnings instead of hard errors
   - Add severity levels to `SemanticError`

---

## Summary

The `ControlFlowValidator` is a focused, single-purpose component that ensures Sharpy programs have valid control flow:
- Functions return on all paths
- Unreachable code is detected
- Break/continue are only in loops

Its design is straightforward: recursive traversal with upward-propagating control flow state. The validator is conservative, accumulates errors, and integrates cleanly with the broader semantic analysis pipeline.

When contributing, focus on maintaining clarity, adding comprehensive tests, and following the existing patterns. Always fix root causes rather than masking symptoms in tests.
