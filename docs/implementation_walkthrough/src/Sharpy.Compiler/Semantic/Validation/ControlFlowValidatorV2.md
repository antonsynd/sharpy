# Walkthrough: ControlFlowValidatorV2.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/ControlFlowValidatorV2.cs`

---

## Overview

`ControlFlowValidatorV2` is a semantic validator that ensures control flow correctness in Sharpy code. It runs **after type checking** (order 400) in the validation pipeline and performs three key validations:

1. **Return Path Validation**: Ensures all non-void functions return a value on all code paths
2. **Loop Context Validation**: Ensures `break` and `continue` statements only appear inside loops
3. **Unreachable Code Detection**: Warns about code that can never be executed

This is a "V2" validator, meaning it implements the new `ISemanticValidator` interface and integrates with the `ValidationPipeline` architecture. It's designed to be pipeline-compatible, stateless between calls, and suitable for future incremental/LSP scenarios.

### Role in Compiler Pipeline

```
Source → Lexer → Parser (AST) → Semantic Analysis → ValidationPipeline → CodeGen
                                 └─> NameResolver (100)
                                 └─> TypeResolver (200)
                                 └─> TypeChecker (300)
                                 └─> ControlFlowValidatorV2 (400) ← We are here
                                 └─> Other validators...
```

The validator receives:
- **Input**: A fully type-checked AST (`Module`) with populated `SemanticInfo`
- **Output**: Diagnostics (errors/warnings) added to `SemanticContext.Diagnostics`

---

## Class Structure

### Inheritance Hierarchy

```csharp
SemanticValidatorBase (abstract)
    └─> ControlFlowValidatorV2
```

The validator extends `SemanticValidatorBase`, which provides:
- `AddError()` and `AddWarning()` helper methods
- Implementation of `ISemanticValidator` interface

### Key Properties

```csharp
public override string Name => "ControlFlowValidator";
public override int Order => 400;
```

- **Name**: Used for logging and debugging (identifies this validator)
- **Order**: Execution order in the pipeline (400 = after type checking at 300)

### Instance State

```csharp
private ICompilerLogger _logger = NullLogger.Instance;
private SemanticContext _context = null!;
```

⚠️ **Important**: These fields are **not thread-safe** and should only be set during `Validate()`. The validator is designed to be instantiated once per validation run, not shared across threads.

---

## Key Methods

### Entry Point: `Validate()`

```csharp
public override void Validate(Module module, SemanticContext context)
```

**Purpose**: Main entry point called by `ValidationPipeline`. Validates all top-level statements in the module.

**Implementation Pattern**:
1. Store context and logger in instance fields
2. Iterate through module body
3. Delegate to `ValidateTopLevelStatement()` for each statement

**Why this design?**: Top-level statements in Python/Sharpy can include functions, classes, structs, and executable code. Only functions/methods need control flow validation.

---

### Statement Type Dispatch

#### `ValidateTopLevelStatement()`

```csharp
private void ValidateTopLevelStatement(Statement stmt)
```

Dispatches to appropriate validators based on statement type:
- `FunctionDef` → `ValidateFunction()`
- `ClassDef` → `ValidateClass()` (validates methods inside)
- `StructDef` → `ValidateStruct()` (validates methods inside)
- Others → No validation needed

**Design Note**: Classes and structs act as containers—only their methods need control flow validation.

---

### Function Validation: `ValidateFunction()`

```csharp
private void ValidateFunction(FunctionDef funcDef)
```

**Core Logic**:
1. **Skip abstract methods**: Methods with `@abstract` decorator or ellipsis body (`...`) don't need control flow validation
2. **Get return type**: Look up function's declared return type from semantic info
3. **Validate body**: Recursively check all code paths
4. **Check return requirement**: If function is non-void and doesn't always return, report error

**Example Error**:
```python
def calculate(x: int) -> int:
    if x > 0:
        return x * 2
    # Missing return in else branch!
```

Error: `Function 'calculate' must return a value of type 'int' in all code paths`

#### Return Type Resolution

```csharp
private SemanticType GetFunctionReturnType(FunctionDef funcDef)
```

Retrieves the function's return type using a two-tier cache strategy:
1. **Fast path**: Check `SemanticInfo.GetTypeAnnotation()` cache
2. **Fallback**: Call `TypeResolver.ResolveTypeAnnotation()`

**Why the cache?**: Type resolution can be expensive. By validation time, types have already been resolved by `TypeChecker`, so we reuse them.

---

### Block Validation: `ValidateBlock()`

```csharp
private (bool alwaysReturns, bool hasUnreachableCode) ValidateBlock(
    IReadOnlyList<Statement> statements, 
    int loopDepth)
```

**Core Algorithm**: Sequential statement analysis with exit tracking.

**Return Values**:
- `alwaysReturns`: `true` if all code paths in this block return
- `hasUnreachableCode`: `true` if unreachable code was detected

**Key Logic**:
```csharp
bool alwaysExits = false;  // Tracks if control flow has exited (return/raise/break/continue)

for each statement:
    if alwaysExits and more statements remain:
        // Everything after an exit is unreachable
        Report "Unreachable code detected"
        continue
    
    Check statement
    Update alwaysExits if statement always exits
```

**Design Pattern**: Uses a state machine approach where `alwaysExits` tracks whether execution can continue past the current point.

---

### Statement-Level Validation: `ValidateStatement()`

```csharp
private (bool alwaysReturns, bool alwaysExits) ValidateStatement(
    Statement statement, 
    int loopDepth)
```

**Pattern Matching Dispatch**: Uses C# `switch` expression to handle different statement types.

#### Simple Statements

| Statement | Returns | Exits | Notes |
|-----------|---------|-------|-------|
| `return` | ✅ | ✅ | Always returns and exits |
| `raise` | ❌ | ✅ | Exits via exception (doesn't count as return) |
| `break` | ❌ | ✅ | Validates `loopDepth > 0` |
| `continue` | ❌ | ✅ | Validates `loopDepth > 0` |

#### Loop Context Validation

```csharp
case BreakStatement:
    if (loopDepth == 0)
        AddError("'break' statement outside loop")
    return (false, true);
```

**The `loopDepth` Parameter**: Tracks nesting level of loops
- `0` = Not in a loop
- `1` = One level deep
- `2+` = Nested loops

**Why track depth instead of boolean?**: Future extensibility for validations like "break with labels" or "maximum nesting depth" warnings.

---

### Control Flow Branch Analysis

#### `ValidateIf()`

```csharp
private (bool, bool) ValidateIf(IfStatement ifStmt, int loopDepth)
```

**Algorithm**: All branches must return for the if-statement to guarantee return.

```csharp
allBranchesReturn = 
    thenReturns 
    && elifReturns  // All elif branches
    && elseReturns  // Must have else!
```

**Example**:
```python
def example(x: int) -> int:
    if x > 0:
        return 1
    elif x < 0:
        return -1
    else:
        return 0  # This else is required!
```

Without the `else`, not all branches return → error.

**Key Insight**: If there's no `else` clause, control can "fall through" the if-statement without returning.

---

#### `ValidateWhile()` and `ValidateFor()`

```csharp
private (bool, bool) ValidateWhile(WhileStatement whileStmt, int loopDepth)
{
    ValidateBlock(whileStmt.Body, loopDepth + 1);
    return (false, false); // Loop doesn't guarantee execution
}
```

**Why `(false, false)`?**: Loops never guarantee execution:
- Loop condition might be false on first iteration
- Loop body might be empty collection

Even if the loop body returns, the function doesn't guarantee return because the loop might not run at all.

**loopDepth + 1**: Increment depth when entering loop body so nested `break`/`continue` statements pass validation.

---

#### `ValidateTry()`

```csharp
private (bool, bool) ValidateTry(TryStatement tryStmt, int loopDepth)
```

**Complex Logic**: Try-except-finally blocks have intricate control flow.

**Return Guarantee Rules**:
1. If `finally` block returns → entire try-statement returns (finally always executes)
2. Otherwise, **both** try block **and all** exception handlers must return

```python
def example() -> int:
    try:
        return 1
    except ValueError:
        return 2
    except Exception:
        return 3
    # All paths return → OK
```

**Why this complexity?**: Exception handlers are alternative code paths. If try block succeeds, handlers don't run. If exception occurs, one handler runs. For guaranteed return, all paths must return.

---

## Dependencies

### Internal (Sharpy.Compiler)

| Dependency | Purpose |
|------------|---------|
| `Parser.Ast.*` | AST node types (`FunctionDef`, `IfStatement`, etc.) |
| `SemanticContext` | Shared validation state (symbols, types, diagnostics) |
| `SemanticInfo` | Type annotation cache |
| `TypeResolver` | Resolve type annotations |
| `SemanticType` | Type representation (checking for `Void`) |
| `ICompilerLogger` | Debug logging |

### External

- `System.Linq` (implicitly used for `.Any()`)

### Related Validators

- **OperatorValidatorV2**: Validates operator usage
- **ProtocolValidatorV2**: Validates protocol implementations
- **AccessValidatorV2**: Validates access modifiers
- **TypeChecker**: Runs before this validator (order 300)

---

## Patterns and Design Decisions

### 1. **Tuple Return Pattern**

```csharp
private (bool alwaysReturns, bool hasUnreachableCode) ValidateBlock(...)
```

**Why tuples?**: Clean way to return multiple boolean flags without creating a struct. C# 7+ tuple support makes this idiomatic.

**Alternative considered**: Creating a `ValidationResult` struct—rejected as overkill for simple boolean flags.

---

### 2. **Recursive Descent with State**

The validator mirrors the recursive structure of the AST:
- `Module` → `Class`/`Function` → `Block` → `Statement`

**State Threading**: `loopDepth` parameter is threaded through recursive calls rather than stored in instance fields.

**Why?**: Allows for potential future parallelization and makes the recursion easier to reason about.

---

### 3. **Conservative Analysis**

The validator uses **conservative control flow analysis**:
- Loops: Assume might not execute
- Exceptions: Assume might be thrown
- Boolean conditions: Don't analyze values

**Example**:
```python
def foo() -> int:
    while True:  # Infinite loop!
        return 1
```

Even though `while True` always executes, the validator reports missing return. This is intentional—static analysis can't prove loop terminates.

**Why conservative?**: Correctness over precision. False negatives (missing errors) are worse than false positives (spurious errors).

---

### 4. **Skip Abstract Methods**

```csharp
bool hasAbstractDecorator = funcDef.Decorators.Any(d => d.Name == "abstract");
bool hasEllipsisBody = funcDef.Body.Length == 1 
    && funcDef.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
```

**Python Convention**: Abstract methods use either:
- `@abstract` decorator (explicit)
- `...` (ellipsis) as body (implicit)

Both signal "implementation provided by subclass"—no control flow to validate.

---

### 5. **Error Message Precision**

Error messages include:
- Function name: `Function 'calculate' must return...`
- Type information: `...type 'int'...`
- Location: Line and column numbers

**Goal**: Help developers quickly locate and fix issues.

---

## Debugging Tips

### 1. **Enable Debug Logging**

```csharp
_logger.LogDebug($"Validating control flow for function: {funcDef.Name}");
```

Set logger to debug level to see which functions are being validated.

**How to enable**: Pass a logger with `LogLevel.Debug` when creating `SemanticContext`.

---

### 2. **Check Validator Order**

If control flow errors seem wrong, verify:
1. Type checking ran first (order 300 < 400)
2. `SemanticInfo` is populated
3. Return type annotations are resolved

**Common Issue**: If type checking fails, return types might not be resolved → validator might use `Unknown` type.

---

### 3. **Trace Recursive Calls**

Add temporary logging in `ValidateBlock()`:
```csharp
_logger.LogDebug($"ValidateBlock: {statements.Count} statements, loopDepth={loopDepth}");
```

Helps understand recursion depth and which blocks are analyzed.

---

### 4. **Test Isolated Cases**

When debugging, create minimal reproduction:
```python
def test_case() -> int:
    if condition:
        return 1
    # Add variations here
```

Run through parser → type checker → validator in isolation (see test examples).

---

### 5. **Common Pitfalls**

| Issue | Symptom | Solution |
|-------|---------|----------|
| `NullReferenceException` in `_context` | Validator not initialized | Ensure `Validate()` called before helper methods |
| Wrong return type | Void function reported as missing return | Check `GetFunctionReturnType()` logic |
| Loop validation fails | `break` allowed outside loop | Verify `loopDepth` threading in recursion |
| Unreachable code not detected | After `return`, no error | Check `alwaysExits` logic in `ValidateBlock()` |

---

## Testing Strategy

### Unit Tests Location
`src/Sharpy.Compiler.Tests/Semantic/Validation/ControlFlowValidatorV2Tests.cs`

### Test Pattern

```csharp
[Fact]
public void TestName_Scenario_ExpectedBehavior()
{
    var code = @"
def foo() -> int:
    x = 5
";
    var (module, context) = Parse(code);  // Helper method
    
    var validator = new ControlFlowValidatorV2();
    validator.Validate(module, context);
    
    Assert.True(context.Diagnostics.HasErrors);
    Assert.Contains(context.Diagnostics.GetErrors(),
        e => e.Message.Contains("must return a value"));
}
```

### Test Categories

1. **Return Path Tests**
   - Function without return
   - Function with return on all paths
   - If-elif-else return coverage

2. **Loop Context Tests**
   - `break` outside loop (error)
   - `break` inside loop (OK)
   - `continue` outside loop (error)
   - Nested loop scenarios

3. **Unreachable Code Tests**
   - Code after `return`
   - Code after `raise`
   - Code after `break`

4. **Edge Cases**
   - Abstract methods (no validation)
   - Void functions (no return required)
   - Empty functions
   - Try-except-finally combinations

---

## Contribution Guidelines

### When to Modify This File

1. **Adding new control flow statements**
   - Example: `match` statement (pattern matching)
   - Add case in `ValidateStatement()`
   - Implement validation logic

2. **Improving analysis precision**
   - Example: Detect `while True:` infinite loops
   - Modify `ValidateWhile()` logic
   - Add conservative bailout for complex cases

3. **New error categories**
   - Example: Warn on multiple returns in finally
   - Add detection logic
   - Use `AddWarning()` instead of `AddError()`

4. **Bug fixes**
   - Check existing issues for control flow validation
   - Add regression test first
   - Fix logic, verify test passes

### What NOT to Change

❌ **Don't modify** the `Order` property without consulting team—validator ordering is critical

❌ **Don't add mutable shared state**—validator must remain stateless between calls

❌ **Don't make breaking changes** to return value tuples—other code may depend on signature

### Code Style

- Follow existing pattern: recursive descent with tuple returns
- Add debug logging for new validation paths
- Keep methods focused—extract helper if method > 50 lines
- Document complex logic with comments

### Testing Requirements

**Before submitting PR:**
1. ✅ Add unit test for new validation
2. ✅ Add integration test in `TestFixtures/` if applicable
3. ✅ Verify existing tests still pass
4. ✅ Add test for edge case behavior

**Test naming convention**:
```csharp
[Fact]
public void Component_Scenario_ExpectedBehavior()
```

Example: `Function_WithReturnInTryButNotExcept_ReportsError`

---

## Cross-References

### Related Documentation

- **[ISemanticValidator.cs](ISemanticValidator.md)**: Interface definition and validator architecture
- **[ValidationPipeline.cs](ValidationPipeline.md)**: How validators are orchestrated
- **[SemanticContext.cs](SemanticContext.md)**: Shared validation state and services
- **[TypeChecker.cs](../TypeChecker.md)**: Upstream validator (runs before control flow)

### Related Source Files

**Validation Infrastructure**:
- `ISemanticValidator.cs` - Interface this class implements
- `SemanticValidatorBase.cs` - Base class providing helpers
- `ValidationPipeline.cs` - Orchestrates validator execution
- `SemanticContext.cs` - Shared context passed to validators

**AST Nodes** (all in `Parser/Ast/`):
- `Statement.cs` - Base statement type
- `FunctionDef.cs` - Function definitions
- `IfStatement.cs`, `WhileStatement.cs`, `ForStatement.cs` - Control flow
- `ReturnStatement.cs`, `BreakStatement.cs`, `ContinueStatement.cs` - Exit statements
- `TryStatement.cs` - Exception handling

**Semantic Analysis**:
- `TypeChecker.cs` - Runs before this validator
- `SemanticInfo.cs` - Type annotation cache
- `SemanticType.cs` - Type representation
- `TypeResolver.cs` - Resolves type annotations

### Legacy Code

⚠️ **Note**: There's a `ControlFlowValidator.cs` in `src/Sharpy.Compiler/Semantic/` that predates the validation pipeline architecture. It's being phased out. This V2 version is the current implementation.

**Key Differences**:
- V2: Implements `ISemanticValidator`, integrates with pipeline
- Legacy: Standalone class with different error reporting
- V2: Uses `DiagnosticBag` for errors
- Legacy: Uses `List<SemanticError>`

When working on control flow validation, **always modify V2**, not the legacy version.

---

## Advanced Topics

### Control Flow Graph (CFG)

This validator performs **local analysis**—it validates control flow within function bodies but doesn't build a full CFG.

**For CFG-based analysis**, see:
- `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraph.cs`
- `src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs`

These provide more sophisticated analysis (dominators, reachability) used for:
- Dataflow analysis
- Optimization
- Advanced diagnostics

### Future Enhancements (v0.2+)

**Potential improvements**:

1. **Definite Assignment Analysis**
   ```python
   def foo(x: int) -> int:
       if x > 0:
           y = 5
       return y  # Error: y might not be assigned
   ```

2. **Exhaustiveness Checking** (for match statements)
   ```python
   def handle(x: Option[int]) -> int:
       match x:
           case Some(n): return n
           # Error: None case not handled
   ```

3. **Async/Await Control Flow**
   ```python
   async def example() -> int:
       await delay()
       return 1
   ```

4. **Generator Functions** (`yield` statements)

**Design Note**: These would likely be separate validators in the pipeline rather than extending this one.

---

## Summary

`ControlFlowValidatorV2` is a **defensive, conservative validator** that ensures:
- Functions return when they should
- Loops contain valid break/continue
- Developers are warned about unreachable code

It's designed to be:
- **Fast**: Single-pass recursive descent
- **Maintainable**: Clear separation of concerns via helper methods
- **Extensible**: Easy to add new statement types
- **Pipeline-friendly**: Stateless, ordered execution

When debugging control flow errors in Sharpy, start here. When adding new control structures to the language, update this validator to handle them.

**Key Takeaway**: This validator is about *safety*—preventing runtime errors by catching control flow issues at compile time. It's conservative by design, preferring false positives to missed errors.
