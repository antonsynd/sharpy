# Walkthrough: ControlFlowValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs`

---

## Overview

`ControlFlowValidator` is a **deprecated** semantic analysis component that validates control flow correctness in Sharpy code. It performs three critical checks:

1. **Unreachable code detection** - Identifies statements that can never execute
2. **Return path validation** - Ensures functions with non-void return types return values on all code paths
3. **Loop context validation** - Verifies `break` and `continue` statements only appear inside loops

**⚠️ Deprecation Notice**: This class is being replaced by `ControlFlowValidatorV2` (located in `Semantic/Validation/`), which integrates with the new `ValidationPipeline` architecture. New code should use `ValidationPipelineFactory.CreateDefault()` instead of instantiating this class directly.

### Role in the Compiler Pipeline

```
Parser (AST) → Semantic Analysis → [THIS FILE] → CodeGen (RoslynEmitter)
                    ↓
         NameResolver → TypeResolver → TypeChecker → ControlFlowValidator
```

The validator runs **after type checking** completes, ensuring all control flow patterns are valid before code generation begins.

---

## Class Structure

### Main Class: `ControlFlowValidator`

```csharp
[Obsolete("Use ControlFlowValidatorV2 via ValidationPipelineFactory.CreateDefault() instead")]
public class ControlFlowValidator
```

**Key Fields:**

- `ICompilerLogger _logger` - Logs debug information during validation
- `List<SemanticError> _errors` - Accumulates validation errors
- `int _loopDepth` - Tracks nesting level of loops (0 = not in a loop)
- `bool _inFunction` - Tracks whether currently validating inside a function

**Public API:**

- `ControlFlowValidator(ICompilerLogger? logger = null)` - Constructor with optional logger
- `IReadOnlyList<SemanticError> Errors` - Read-only access to collected errors
- `ValidateFunction(FunctionDef, SemanticType)` - Entry point for function validation

---

## Key Methods

### 1. ValidateFunction (Entry Point)

```csharp
public void ValidateFunction(FunctionDef functionDef, SemanticType returnType)
```

**Purpose**: Main entry point for validating control flow within a function definition.

**Key Logic:**

1. **Skip abstract methods** - Functions with `@abstract` decorator or ellipsis-only bodies (`...`) don't need validation
2. **Validate the function body** - Analyzes all statements in the function
3. **Check return path coverage** - For non-void functions, ensures all code paths return a value

**Important Edge Cases:**

- **Ellipsis bodies** (`def foo(): ...`) are skipped because they either:
  - Generate `NotImplementedException` in concrete classes (no return needed)
  - Are abstract method declarations (no implementation to validate)
- **Void functions** don't require explicit returns on all paths

**Example:**

```python
# This will trigger an error:
def get_value() -> int:
    if condition:
        return 42
    # Missing return in else branch!
```

### 2. ValidateBlock (Core Analysis)

```csharp
private (bool, bool) ValidateBlock(IReadOnlyList<Statement> statements)
```

**Returns**: `(alwaysReturns, hasUnreachableCode)`

**Purpose**: Analyzes a sequence of statements to determine control flow properties.

**Algorithm:**

1. Iterate through each statement in order
2. Track whether the block:
   - **Always returns** - Every code path leads to a return statement
   - **Always exits** - Every code path exits (return, raise, break, continue)
3. Detect unreachable code - statements after an "always exits" statement
4. Once unreachable code is detected, report it only once (not for every subsequent statement)

**Key Insight**: The distinction between `alwaysReturns` and `alwaysExits`:
- `alwaysReturns` = function execution completes with a return value
- `alwaysExits` = control flow leaves the block (includes break/continue which don't return)

**Example:**

```python
def example():
    return 42      # alwaysExits = true, alwaysReturns = true
    print("never") # UNREACHABLE CODE (error reported here)
    print("never") # Still unreachable, but no duplicate error
```

### 3. ValidateStatement (Dispatcher)

```csharp
private (bool, bool) ValidateStatement(Statement statement)
```

**Returns**: `(alwaysReturns, alwaysExits)`

**Purpose**: Dispatches to appropriate validation logic based on statement type.

**Statement Handling:**

| Statement Type | Always Returns | Always Exits | Special Validation |
|----------------|----------------|--------------|-------------------|
| `ReturnStatement` | ✅ | ✅ | None |
| `RaiseStatement` | ❌ | ✅ | None |
| `BreakStatement` | ❌ | ✅ | Must be in loop (`_loopDepth > 0`) |
| `ContinueStatement` | ❌ | ✅ | Must be in loop (`_loopDepth > 0`) |
| `IfStatement` | Conditional | Conditional | Analyzed in `ValidateIf` |
| `WhileStatement` | ❌ | ❌ | Body analyzed with `_loopDepth++` |
| `ForStatement` | ❌ | ❌ | Body analyzed with `_loopDepth++` |
| `TryStatement` | Conditional | Conditional | Analyzed in `ValidateTry` |
| `FunctionDef` | ❌ | ❌ | Nested functions validated separately |
| Type definitions | ❌ | ❌ | Don't affect control flow |

**Design Decision**: Why loops don't "always return":
```python
# Even if loop body returns, the loop might not execute
for item in potentially_empty_list:
    return item  # Might never run if list is empty!
# Control flow continues here
```

### 4. ValidateIf (Conditional Logic)

```csharp
private (bool, bool) ValidateIf(IfStatement ifStmt)
```

**Purpose**: Determines if an if/elif/else chain guarantees a return on all paths.

**Algorithm:**

1. Validate the `then` branch
2. Validate all `elif` branches
3. Validate the `else` branch (if present)
4. Return true **only if**:
   - There IS an `else` branch, AND
   - Every branch (then, all elifs, else) returns

**Critical Rule**: Without an `else`, not all code paths are covered:

```python
# Does NOT always return:
def example(x: int) -> int:
    if x > 0:
        return 1
    elif x < 0:
        return -1
    # Missing else! What if x == 0?

# DOES always return:
def example2(x: int) -> int:
    if x > 0:
        return 1
    elif x < 0:
        return -1
    else:
        return 0  # All paths covered!
```

### 5. ValidateWhile & ValidateFor (Loop Validation)

```csharp
private (bool, bool) ValidateWhile(WhileStatement whileStmt)
private (bool, bool) ValidateFor(ForStatement forStmt)
```

**Purpose**: Validate loop bodies while tracking loop context.

**Key Pattern**:
```csharp
_loopDepth++;                     // Enter loop context
var (bodyReturns, _) = ValidateBlock(loop.Body);
_loopDepth--;                     // Exit loop context
return (false, false);            // Loops never guarantee execution
```

**Why `_loopDepth` matters**:
- `break` and `continue` are only valid when `_loopDepth > 0`
- Allows nested loops (each increments the counter)
- Prevents errors like this:

```python
def invalid():
    break  # ERROR: 'break' statement outside loop (_loopDepth == 0)
```

### 6. ValidateTry (Exception Handling)

```csharp
private (bool, bool) ValidateTry(TryStatement tryStmt)
```

**Purpose**: Determines if a try/except/finally block guarantees returns on all paths.

**Complex Logic**:

```csharp
bool allPathsReturn = finallyReturns || (tryReturns && allHandlersReturn);
```

**Cases:**

1. **Finally returns** → Always returns (finally always executes, overrides everything)
2. **No finally** → Returns if BOTH:
   - Try block returns, AND
   - Every exception handler returns

**Example:**

```python
# Always returns (finally overrides):
def example1():
    try:
        return 1
    except:
        return 2
    finally:
        return 3  # This is what actually returns

# Does NOT always return:
def example2():
    try:
        return 1
    except ValueError:
        return 2
    # No handler for other exceptions! Might not return.

# DOES always return:
def example3():
    try:
        return 1
    except:
        return 2  # Catch-all handler covers all paths
```

---

## Dependencies

### Internal Sharpy Dependencies

1. **`Sharpy.Compiler.Parser.Ast`** - All AST node types:
   - `Statement` base class and subclasses
   - `FunctionDef`, `IfStatement`, `WhileStatement`, etc.
   - Location information (`LineStart`, `ColumnStart`)

2. **`Sharpy.Compiler.Logging`** - Compiler logging infrastructure:
   - `ICompilerLogger` interface
   - `NullLogger.Instance` for optional logger pattern

3. **`SemanticError`** - Error data structure (defined in same namespace)

4. **`SemanticType`** - Type system representation:
   - `SemanticType.Void` constant
   - `GetDisplayName()` method for error messages

### Upstream Dependencies

**Parser (AST)** → Provides immutable AST nodes with location info

### Downstream Dependencies

**CodeGen** ← Assumes control flow validation has passed (no unreachable code, valid returns)

---

## Design Patterns and Decisions

### 1. Visitor-Style Pattern

The validator uses a **modified visitor pattern** without explicit visitor interfaces:

```csharp
ValidateStatement(stmt) → switch (stmt) { case IfStatement: ..., case WhileStatement: ... }
```

**Why not a full visitor?**
- Simpler for single-purpose validation
- Easier to understand for newcomers
- Less boilerplate than formal visitor pattern

### 2. Tuple Return Values

Methods return `(bool, bool)` tuples for dual properties:

```csharp
(bool alwaysReturns, bool alwaysExits) = ValidateStatement(stmt);
```

**Benefits:**
- Compact representation of two related flags
- Natural composition (combine results from multiple branches)
- Self-documenting variable names at call sites

### 3. State Tracking via Mutable Fields

The validator uses instance fields (`_loopDepth`, `_inFunction`) rather than passing state through parameters.

**Trade-offs:**
- ✅ Simpler method signatures
- ✅ Easier to add new context tracking
- ⚠️ Requires careful increment/decrement pairing (e.g., `_loopDepth++` / `_loopDepth--`)
- ⚠️ Not thread-safe (but validators are single-threaded by design)

### 4. Error Accumulation

Errors are collected in `_errors` list rather than throwing immediately:

**Rationale:**
- Reports ALL errors in one pass (better UX)
- Allows validation to continue after first error
- Caller can inspect all errors via `Errors` property

### 5. Conservative Analysis

When uncertain, the validator is **conservative** (assumes no guarantee):

```csharp
// Loops: might not execute → (false, false)
// If without else: might skip → false
// Try without catch-all: might not handle → false
```

This prevents false negatives (missing errors) at the cost of potential false positives (overly strict).

---

## Debugging Tips

### 1. Enable Debug Logging

Pass a logger to see validation flow:

```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
var validator = new ControlFlowValidator(logger);
validator.ValidateFunction(functionDef, returnType);
// Output: "Validating control flow for function: myFunction"
```

### 2. Check `_loopDepth` State

If seeing "break/continue outside loop" errors incorrectly:

1. Add breakpoint in `ValidateWhile`/`ValidateFor`
2. Verify `_loopDepth++` and `_loopDepth--` are balanced
3. Check that nested functions don't inherit parent's loop depth (they don't - correct behavior)

### 3. Trace Return Path Analysis

For "function must return" errors:

1. Breakpoint at `ValidateBlock` exit
2. Inspect `alwaysReturns` for each branch
3. For `if` statements, verify:
   - All branches including `else` are analyzed
   - `elseBody != null` check works correctly
   - `allBranchesReturn` logic combines correctly

### 4. Test Edge Cases

Common bugs to watch for:

```python
# Edge case 1: Empty else
if condition:
    return 1
else:
    pass  # Empty else body - should return false

# Edge case 2: Nested loops
for i in range(10):
    for j in range(10):
        break  # Should be valid (loopDepth == 2)

# Edge case 3: Finally with no return
try:
    return 1
finally:
    print("cleanup")  # Finally exists but doesn't return
```

### 5. Unreachable Code Detection

If unreachable code isn't detected:

1. Check `alwaysExits` is set correctly
2. Verify loop at line 82: `if (alwaysExits && i < statements.Count)`
3. Note: Only reports unreachable code once per block (by design)

---

## Contribution Guidelines

### What Kinds of Changes?

**⚠️ WARNING**: This file is deprecated! Changes should go to `ControlFlowValidatorV2` instead.

However, if maintaining this legacy code:

#### 1. Bug Fixes

**Safe changes:**
- Fix incorrect `alwaysReturns` / `alwaysExits` logic
- Correct loop depth tracking
- Fix unreachable code detection

**Example bug fix:**
```csharp
// Bug: Empty list check is wrong
if (ifStmt.ElseBody != null && ifStmt.ElseBody.Length > 0)
    // Should also check that statements aren't all pass/empty
```

#### 2. New Statement Types

If parser adds new statement types:

1. Add case to `ValidateStatement` switch
2. Determine return/exit semantics
3. Add tests for new statement type
4. Document behavior

**Example:**
```csharp
case MatchStatement matchStmt:
    return ValidateMatch(matchStmt);  // Similar to if/elif/else
```

#### 3. Enhanced Error Messages

Improve error clarity:

```csharp
// Before:
AddError("'break' statement outside loop", ...)

// After:
AddError("'break' statement can only be used inside 'for' or 'while' loops", ...)
```

### Testing Requirements

When making changes:

1. **Unit tests** - Add to `Sharpy.Compiler.Tests/Semantic/ControlFlowValidatorTests.cs`
2. **Integration tests** - Add `.spy` + `.expected` or `.error` files to `TestFixtures/`
3. **Verify against Python** - Ensure behavior matches Python semantics

**Example test:**
```csharp
[Fact]
public void BreakOutsideLoop_ReportsError()
{
    var validator = new ControlFlowValidator();
    var breakStmt = new BreakStatement { LineStart = 1, ColumnStart = 0 };
    var funcDef = new FunctionDef 
    { 
        Name = "test",
        Body = new[] { breakStmt }
    };
    
    validator.ValidateFunction(funcDef, SemanticType.Void);
    
    Assert.Single(validator.Errors);
    Assert.Contains("outside loop", validator.Errors[0].Message);
}
```

### Code Style

Follow existing patterns:

- Use tuple returns for dual boolean properties
- Increment/decrement depth counters symmetrically
- Add debug logging for major decisions
- Keep method sizes reasonable (< 50 lines)

---

## Cross-References

### Related Files

1. **`Semantic/Validation/ControlFlowValidatorV2.cs`** - Modern replacement implementing `ISemanticValidator`
   - Located in `Validation/` subdirectory
   - Integrates with `ValidationPipeline`
   - Should be used for new code

2. **`Semantic/ValidationPipeline.cs`** - Orchestrates V2 validators
   - Manages validator ordering
   - Provides unified error collection
   - Entry point for all semantic validation

3. **`Semantic/SemanticInfo.cs`** - Stores type and symbol information
   - Used by V2 but not this legacy validator
   - Contains `_narrowedTypes` for type narrowing

4. **`Semantic/TypeChecker.cs`** - Runs before control flow validation
   - Provides `SemanticType` information
   - Ensures expressions are type-correct

5. **`Parser/Ast/*.cs`** - AST node definitions
   - All statement types validated here
   - Contains location information for errors

### Migration Path

To migrate from V1 to V2:

```csharp
// OLD (deprecated):
var validator = new ControlFlowValidator(logger);
validator.ValidateFunction(funcDef, returnType);
var errors = validator.Errors;

// NEW (recommended):
var pipeline = ValidationPipelineFactory.CreateDefault();
var context = new SemanticContext(semanticInfo, logger, errors);
pipeline.Validate(module, context);
// Errors automatically added to context.Errors
```

---

## Summary

`ControlFlowValidator` is a straightforward but critical compiler component that ensures:

1. ✅ Functions return values on all code paths (when required)
2. ✅ No unreachable code exists
3. ✅ Loop control statements (`break`/`continue`) only appear in loops

**Key takeaways:**

- **Conservative analysis**: When uncertain, assumes no guarantee (safe default)
- **Depth tracking**: Uses `_loopDepth` counter for loop context validation
- **Tuple returns**: Methods return `(alwaysReturns, alwaysExits)` for composable analysis
- **Deprecated**: Use `ControlFlowValidatorV2` for new code

The validator's simple recursive descent over the AST makes it easy to understand and maintain, though the newer V2 architecture provides better integration with the broader validation pipeline.
