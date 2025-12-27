# Walkthrough: ControlFlowValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs`

---

## 1. Overview

The `ControlFlowValidator` is a critical component of the Sharpy compiler's semantic analysis phase. It ensures that code follows proper control flow rules and catches common programming errors before code generation.

**Primary Responsibilities:**
- **Unreachable Code Detection**: Identifies code that can never be executed (e.g., statements after `return`)
- **Return Path Validation**: Ensures functions that declare a return type actually return values on all execution paths
- **Loop Statement Validation**: Verifies that `break` and `continue` statements only appear inside loops
- **Control Flow Analysis**: Tracks how execution flows through conditional branches, loops, and exception handlers

**Where It Fits:**
In the compiler pipeline, this validator runs during the semantic analysis phase, after type checking but before code generation:
```
Lexer → Parser → NameResolver → TypeResolver → TypeChecker → ControlFlowValidator → RoslynEmitter
```

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
    
    public IReadOnlyList<SemanticError> Errors => _errors;
}
```

**State Management:**
- `_errors`: Accumulates all control flow errors found during validation
- `_loopDepth`: Tracks nesting level of loops (incremented on loop entry, decremented on exit)
- `_inFunction`: Boolean flag indicating whether we're currently analyzing function body code
- `_logger`: For debug output during validation

**Design Note:** The validator uses instance state to track context while traversing the AST. This allows recursive validation methods to know their context (e.g., whether they're inside a loop) without passing context parameters everywhere.

---

## 3. Key Methods

### 3.1 `ValidateFunction` - Entry Point

```csharp
public void ValidateFunction(FunctionDef functionDef, SemanticType returnType)
```

**Purpose:** Main entry point for validating a function's control flow.

**Parameters:**
- `functionDef`: The AST node representing the function to validate
- `returnType`: The semantic type this function is expected to return (used to check if all paths return)

**What It Does:**
1. Sets `_inFunction = true` to indicate we're in function scope
2. Validates the function's body block recursively
3. Checks if the function declares a non-void return type but doesn't return on all paths
4. Resets `_inFunction = false` when done

**Key Logic:**
```csharp
if (returnType != SemanticType.Void && !alwaysReturns)
{
    AddError($"Function '{functionDef.Name}' must return a value...");
}
```

This is the critical check that catches Python-style functions where you might forget a return statement in some branches.

**Example Caught Error:**
```python
def calculate(x: int) -> int:
    if x > 0:
        return x * 2
    # Missing return for x <= 0 case!
```

---

### 3.2 `ValidateBlock` - Block Analysis

```csharp
private (bool, bool) ValidateBlock(List<Statement> statements)
```

**Purpose:** Validates a sequence of statements, tracking whether the block always returns and detecting unreachable code.

**Return Values:**
- Tuple of `(alwaysReturns, hasUnreachableCode)`
- `alwaysReturns`: `true` if execution cannot continue past this block
- `hasUnreachableCode`: `true` if any statements were detected as unreachable

**Algorithm:**
1. Iterate through each statement in order
2. Once we encounter a statement that always exits (return/raise/break/continue), flag all subsequent statements as unreachable
3. Track whether any statement causes the entire block to always return

**Key Implementation Detail:**
```csharp
if (alwaysExits && i < statements.Count)
{
    if (!hasUnreachableCode)
    {
        AddError("Unreachable code detected", ...);
        hasUnreachableCode = true;
    }
    continue; // Skip validating unreachable statements
}
```

The `hasUnreachableCode` flag ensures we only report the error once per block, not for every unreachable statement.

**Important Distinction:**
- `alwaysReturns`: Only true for actual `return` statements
- `alwaysExits`: True for return, raise, break, or continue (any statement that prevents fall-through)

---

### 3.3 `ValidateStatement` - Statement Dispatcher

```csharp
private (bool, bool) ValidateStatement(Statement statement)
```

**Purpose:** Central dispatcher that routes each statement type to its appropriate validation logic.

**Return Values:** `(alwaysReturns, alwaysExits)` for the statement

**Key Pattern:**
Uses a switch expression on statement type to delegate to specialized validators:
```csharp
case IfStatement ifStmt:
    return ValidateIf(ifStmt);
case WhileStatement whileStmt:
    return ValidateWhile(whileStmt);
// ... etc
```

**Terminal Statements:**
- `ReturnStatement`: Returns `(true, true)` - both returns and exits
- `RaiseStatement`: Returns `(false, true)` - exits but doesn't count as a return
- `BreakStatement` / `ContinueStatement`: Check `_loopDepth` and return `(false, true)`

**Design Decision:** Nested function definitions don't affect the outer function's control flow, so they return `(false, false)`. The nested function will be validated separately when its turn comes.

---

### 3.4 `ValidateIf` - Conditional Branch Analysis

```csharp
private (bool, bool) ValidateIf(IfStatement ifStmt)
```

**Purpose:** Determines if an if-statement guarantees a return on all possible execution paths.

**Critical Logic:**
All branches must return for the if-statement to "always return":
```csharp
allBranchesReturn = thenReturns;  // Start with 'then' branch
// AND with each elif branch
foreach (var elifClause in ifStmt.ElifClauses)
{
    var (elifReturns, _) = ValidateBlock(elifClause.Body);
    allBranchesReturn = allBranchesReturn && elifReturns;
}
// AND with else branch (if present)
if (ifStmt.ElseBody != null)
{
    var (elseReturns, _) = ValidateBlock(ifStmt.ElseBody);
    allBranchesReturn = allBranchesReturn && elseReturns;
}
else
{
    allBranchesReturn = false;  // No else means not all paths covered
}
```

**Why This Matters:**
```python
def calculate(x: int) -> int:
    if x > 0:
        return 1
    elif x < 0:
        return -1
    else:
        return 0  # This else is REQUIRED for function to be valid
```

Without the `else`, the function wouldn't guarantee a return value.

---

### 3.5 `ValidateWhile` and `ValidateFor` - Loop Analysis

```csharp
private (bool, bool) ValidateWhile(WhileStatement whileStmt)
private (bool, bool) ValidateFor(ForStatement forStmt)
```

**Purpose:** Validate loop bodies while tracking loop depth for break/continue validation.

**Key Pattern:**
```csharp
_loopDepth++;
var (bodyReturns, _) = ValidateBlock(loopBody);
_loopDepth--;
return (false, false);  // Loops never guarantee execution
```

**Why Always Return `(false, false)`:**
Loops cannot guarantee they execute at all:
- While loops depend on their condition being true initially
- For loops might iterate over an empty collection

```python
def example() -> int:
    while False:
        return 42  # This loop body never runs!
    # No return here - error!
```

Even if the loop body always returns, the loop itself doesn't guarantee execution, so the validator correctly reports missing return paths.

**Loop Depth Tracking:**
The `_loopDepth` counter enables proper validation of nested loops:
```python
for i in range(10):
    for j in range(10):
        if i == j:
            break  # Valid - loopDepth = 2
    # Still valid - loopDepth = 1

break  # ERROR - loopDepth = 0
```

---

### 3.6 `ValidateTry` - Exception Handler Analysis

```csharp
private (bool, bool) ValidateTry(TryStatement tryStmt)
```

**Purpose:** Validates try-except-finally blocks with complex return path analysis.

**Complex Logic:**
```csharp
bool allPathsReturn = finallyReturns || (tryReturns && allHandlersReturn);
```

This encodes the rule: "All paths return if..."
1. The `finally` block returns (overrides everything), OR
2. Both the `try` block AND all exception handlers return

**Why Finally Overrides:**
In Python and Sharpy, a `return` in a `finally` block overrides any return in the `try` or handler blocks:
```python
def tricky() -> int:
    try:
        return 1
    finally:
        return 2  # This is what actually gets returned!
```

**All Handlers Must Return:**
If the try block returns but any handler doesn't, a path exists without a return:
```python
def incomplete() -> int:
    try:
        return 1
    except ValueError:
        return 2
    except KeyError:
        print("error")  # Missing return - error!
```

---

### 3.7 `AddError` - Error Recording

```csharp
private void AddError(string message, int? line, int? column)
```

**Purpose:** Helper method to record control flow errors with source location information.

Creates a `SemanticError` object that includes:
- Human-readable error message
- Optional line number
- Optional column number

Errors are accumulated in the `_errors` list and can be retrieved via the `Errors` property after validation completes.

---

## 4. Dependencies

### Internal Dependencies

**AST Nodes** (`Sharpy.Compiler.Parser.Ast`):
- `Statement` and all its subclasses: `ReturnStatement`, `IfStatement`, `WhileStatement`, `ForStatement`, `TryStatement`, `BreakStatement`, `ContinueStatement`, `RaiseStatement`
- `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`

**Semantic Types** (`Sharpy.Compiler.Semantic`):
- `SemanticType`: Represents types in the semantic model
- `SemanticError`: Exception type for semantic errors

**Logging** (`Sharpy.Compiler.Logging`):
- `ICompilerLogger`: Interface for compiler logging
- `NullLogger`: No-op logger implementation

### External Dependencies
- Standard .NET collections (`List<T>`)

---

## 5. Design Patterns & Decisions

### 5.1 Visitor-Like Pattern Without Formal Visitor

The validator uses a switch-based dispatch pattern rather than implementing a formal visitor pattern. This is simpler and more maintainable for the use case:
```csharp
switch (statement)
{
    case ReturnStatement: ...
    case IfStatement ifStmt: return ValidateIf(ifStmt);
    // ...
}
```

**Why Not Formal Visitor:**
- Fewer interfaces and classes to maintain
- Pattern matching is more readable
- Easy to add new statement types
- No need to modify AST classes

### 5.2 Stateful Traversal

The validator maintains mutable state (`_loopDepth`, `_inFunction`) during traversal rather than passing context objects. This is pragmatic:
- Simpler method signatures
- State is only relevant within a single validation pass
- No risk of parallel execution (compiler is single-threaded per file)

### 5.3 Tuple Return Values

Methods return tuples like `(bool alwaysReturns, bool alwaysExits)` to communicate multiple pieces of information:
```csharp
private (bool, bool) ValidateBlock(List<Statement> statements)
```

**Alternative Considered:** A `ControlFlowInfo` class could make this more explicit but adds ceremony for what's essentially a simple pair of booleans.

### 5.4 Error Collection vs. Exceptions

Errors are collected in a list rather than thrown immediately:
```csharp
private readonly List<SemanticError> _errors = new();
```

**Benefits:**
- Validator can report multiple errors in one pass
- Caller can decide how to handle errors
- Validation continues after first error (helpful for IDE tooling)

### 5.5 Distinction Between "Returns" and "Exits"

The code carefully distinguishes:
- **alwaysReturns**: Function will return a value (only `return` statements)
- **alwaysExits**: Execution cannot continue (return, raise, break, continue)

This distinction is crucial for different validation concerns:
- Return path validation needs `alwaysReturns`
- Unreachable code detection needs `alwaysExits`

---

## 6. Debugging Tips

### 6.1 Enable Debug Logging

The validator logs debug information when validating functions:
```csharp
_logger.LogDebug($"Validating control flow for function: {functionDef.Name}");
```

To see this output during development, ensure the logger is configured to show debug messages.

### 6.2 Common Issues

**Issue: False positive "function must return" error**
- Check if all if/elif/else branches are accounted for
- Verify that loops correctly return `(false, false)` since they don't guarantee execution
- Look for missing `else` clauses in conditional logic

**Issue: "break/continue outside loop" not caught**
- Check that `_loopDepth` is being correctly incremented/decremented
- Ensure loop validation methods properly wrap the recursive call

**Issue: Unreachable code not detected**
- Verify the statement's validator returns `(_, true)` for `alwaysExits`
- Check that `ValidateBlock` is properly checking `alwaysExits`

### 6.3 Testing Strategy

When testing control flow validation:

1. **Test terminal statements in isolation:**
   ```python
   def test_return():
       return 42
       print("unreachable")  # Should error
   ```

2. **Test all branches of conditionals:**
   ```python
   def test_if(x: int) -> int:
       if x > 0:
           return 1
       # Missing else - should error
   ```

3. **Test nested loops:**
   ```python
   for i in range(10):
       for j in range(10):
           break  # Valid
       break  # Valid
   break  # Should error - not in loop
   ```

4. **Test try-except-finally combinations:**
   ```python
   def test_try() -> int:
       try:
           return 1
       except:
           return 2
       # All paths return - valid
   ```

### 6.4 Debugging Walkthrough

If you encounter unexpected behavior:

1. **Add temporary logging:**
   ```csharp
   Console.WriteLine($"Validating {statement.GetType().Name} at line {statement.LineStart}");
   Console.WriteLine($"  alwaysReturns={alwaysReturns}, alwaysExits={alwaysExits}");
   ```

2. **Check state variables:**
   ```csharp
   Console.WriteLine($"_loopDepth={_loopDepth}, _inFunction={_inFunction}");
   ```

3. **Trace recursive calls:**
   Add a depth parameter to track recursion level:
   ```csharp
   private (bool, bool) ValidateBlock(List<Statement> statements, int depth = 0)
   {
       var indent = new string(' ', depth * 2);
       Console.WriteLine($"{indent}ValidateBlock: {statements.Count} statements");
       // ...
   }
   ```

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

**Add New Control Flow Statements:**
If Sharpy adds new control flow constructs (e.g., `match` statement, `with` statement), you'll need to:
1. Add a new case in `ValidateStatement`
2. Implement a dedicated validation method
3. Determine the correct `(alwaysReturns, alwaysExits)` semantics

**Enhance Validation Rules:**
Examples of enhancements:
- Detect infinite loops with constant conditions: `while True: ...`
- Warn about suspicious patterns: `if x: return 1; else: pass`
- Track reachability more precisely through boolean expressions

**Add New Contextual Restrictions:**
Similar to `_loopDepth` for break/continue:
- Add context tracking fields
- Increment/decrement appropriately
- Check context in relevant statement validators

### 7.2 Testing Requirements

All changes to control flow validation **must** include:
1. **Positive tests**: Valid code that should pass validation
2. **Negative tests**: Invalid code that should produce specific errors
3. **Edge cases**: Nested structures, empty blocks, etc.

Test location: `src/Sharpy.Compiler.Tests/Semantic/ControlFlowValidatorTests.cs`

### 7.3 Maintaining Python Compatibility

When adding or modifying validation rules, always verify behavior matches Python:
```bash
python3 -c "
def test():
    if True:
        return 1
    # No else - check if Python requires return here
"
```

**Principle:** Sharpy should reject code that would cause runtime errors in Python, but not be stricter unless there's a compelling reason.

### 7.4 Error Message Quality

When adding new errors:
- Be specific about what's wrong
- Include relevant names/types in the message
- Provide location information (line/column)
- Consider what would help a user fix the issue

**Good:**
```csharp
AddError($"'break' statement outside loop", statement.LineStart, statement.ColumnStart);
```

**Bad:**
```csharp
AddError("Invalid statement", null, null);
```

### 7.5 Common Contribution Scenarios

**Scenario 1: Add Match Statement Support**
```csharp
case MatchStatement matchStmt:
    return ValidateMatch(matchStmt);

private (bool, bool) ValidateMatch(MatchStatement matchStmt)
{
    bool allCasesReturn = true;
    foreach (var caseClause in matchStmt.Cases)
    {
        var (caseReturns, _) = ValidateBlock(caseClause.Body);
        allCasesReturn = allCasesReturn && caseReturns;
    }
    // Must have catch-all case to guarantee all paths return
    bool hasCatchAll = matchStmt.Cases.Any(c => c.Pattern is WildcardPattern);
    return (allCasesReturn && hasCatchAll, allCasesReturn && hasCatchAll);
}
```

**Scenario 2: Detect Infinite Loops**
```csharp
private (bool, bool) ValidateWhile(WhileStatement whileStmt)
{
    _loopDepth++;
    var (bodyReturns, _) = ValidateBlock(whileStmt.Body);
    _loopDepth--;
    
    // NEW: Detect infinite loops
    if (whileStmt.Condition is BooleanLiteral { Value: true })
    {
        // while True: ... can guarantee execution
        return (bodyReturns, bodyReturns);
    }
    
    return (false, false);
}
```

**Scenario 3: Add Async Function Support**
Add tracking for async context to validate `await` usage:
```csharp
private bool _inAsyncFunction = false;

public void ValidateFunction(FunctionDef functionDef, SemanticType returnType)
{
    _inAsyncFunction = functionDef.IsAsync;
    // ... rest of validation
    _inAsyncFunction = false;
}

case AwaitExpression awaitExpr:
    if (!_inAsyncFunction)
    {
        AddError("'await' outside async function", 
                 awaitExpr.LineStart, awaitExpr.ColumnStart);
    }
    return (false, false);
```

---

## 8. Related Components

### Upstream (Inputs)
- **Parser/AST**: Provides the abstract syntax tree to validate
- **TypeResolver**: Resolves `SemanticType` instances used for return type checking
- **SymbolTable**: Provides function definitions and their signatures

### Downstream (Outputs)
- **Compiler**: Consumes validation errors and decides whether to proceed with code generation
- **IDE/Language Server**: Can use errors for real-time diagnostics and error squiggles

### Sibling Components in Semantic Analysis
- **TypeChecker**: Validates type compatibility and inference
- **NameResolver**: Ensures all names are properly defined before use
- **ProtocolValidator**: Validates protocol implementations
- **AccessValidator**: Checks access modifiers and visibility rules

---

## 9. Future Enhancements

Potential improvements to consider:

### 9.1 More Precise Reachability Analysis
- Track boolean expressions to detect dead branches: `if False: ...`
- Use constant folding to identify guaranteed execution paths
- Detect contradictory conditions: `if x > 0 and x < 0: ...`

### 9.2 Data Flow Integration
- Combine with definite assignment analysis
- Track which variables are initialized on all paths
- Warn about using variables that might not be set

### 9.3 Loop Analysis Improvements
- Detect provably infinite loops: `while True:` with no break
- Track loop exit conditions more precisely
- Analyze range-based for loops that always execute at least once

### 9.4 Better Error Recovery
- Continue validation after errors to report multiple issues
- Suggest fixes for common mistakes
- Provide "did you mean" suggestions

### 9.5 Performance Optimizations
- Cache validation results for unchanged functions
- Short-circuit validation when possible
- Profile and optimize hot paths

---

## Summary

The `ControlFlowValidator` is a focused, single-purpose component that ensures Sharpy programs follow proper control flow rules. Its design prioritizes:
- **Correctness**: Catches real bugs before runtime
- **Clarity**: Simple, readable code using pattern matching
- **Python Compatibility**: Behavior aligned with Python semantics
- **Maintainability**: Easy to extend with new statement types

Understanding this validator is key to understanding how Sharpy ensures type-safe control flow, a critical aspect of the language's safety guarantees. When working on this code, always think about the execution paths through user code and whether all possibilities are properly handled.
