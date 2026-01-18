# Walkthrough: ControlFlowValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs`

---

## Overview

The `ControlFlowValidator` is a critical component in Sharpy's semantic analysis phase that ensures code has valid control flow patterns. It operates on the AST (Abstract Syntax Tree) produced by the parser and validates three main aspects:

1. **Unreachable Code Detection**: Identifies code that can never be executed
2. **Return Path Validation**: Ensures non-void functions return values in all code paths
3. **Loop Context Validation**: Verifies `break` and `continue` statements only appear inside loops

This validator runs after the parser produces an AST but before code generation begins. It's a pure validation pass—it doesn't modify the AST, just collects semantic errors.

**Pipeline Position**: Parser (AST) → **ControlFlowValidator** → Type Checker → RoslynEmitter

## Class/Type Structure

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

**State Management**:
- `_loopDepth`: Tracks how deeply nested we are in loops (for validating break/continue)
- `_inFunction`: Indicates whether we're currently inside a function body
- `_errors`: Accumulates all validation errors found during analysis
- `_logger`: Optional logger for debugging (uses `NullLogger` if none provided)

The validator is **stateful** during analysis but designed to be used once per function. After validating a function, you read the `Errors` property to get all issues found.

## Key Functions/Methods

### 1. `ValidateFunction(FunctionDef, SemanticType)` - Entry Point

**Purpose**: Main entry point for validating control flow in a function definition.

**Key Parameters**:
- `functionDef`: The AST node representing the function
- `returnType`: The expected return type (from semantic analysis)

**Important Logic**:

```csharp
bool hasAbstractDecorator = functionDef.Decorators.Any(d => d.Name == "abstract");
bool hasEllipsisBody = functionDef.Body.Count == 1
    && functionDef.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
```

The validator **skips** two special cases:
- Functions with `@abstract` decorator (just declarations)
- Functions with ellipsis-only bodies (`...`) which become `NotImplementedException` in C#

**Return Validation**:
After analyzing the function body, if the return type is non-void but not all paths return a value, an error is added:

```csharp
if (returnType != SemanticType.Void && !alwaysReturns)
{
    AddError($"Function '{functionDef.Name}' must return a value...");
}
```

**Source Reference**: Lines 30-58 in `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs:30-58`

### 2. `ValidateBlock(List<Statement>)` - Core Block Analysis

**Purpose**: Validates a sequence of statements (used for function bodies, if branches, loop bodies, etc.)

**Return Value**: `(bool alwaysReturns, bool hasUnreachableCode)`
- `alwaysReturns`: True if all execution paths through this block end with a return statement
- `hasUnreachableCode`: True if unreachable code was detected (for avoiding duplicate errors)

**Algorithm**:

```csharp
bool alwaysExits = false; // returns, raises, break, continue

for (int i = 0; i < statements.Count; i++)
{
    // Check for unreachable code
    if (alwaysExits && i < statements.Count)
    {
        // Report error once, then skip remaining statements
    }

    var (stmtReturns, stmtExits) = ValidateStatement(statement);
    // Track whether we've hit an exit point
}
```

**Key Insight**: The validator distinguishes between "returns" (function exit) and "exits" (any control flow terminator). A statement that always exits means subsequent statements are unreachable.

**Source Reference**: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs:64-96`

### 3. `ValidateStatement(Statement)` - Statement Dispatcher

**Purpose**: Routes validation to appropriate handler based on statement type.

**Return Value**: `(bool alwaysReturns, bool alwaysExits)`

**Statement Categories**:

**Terminators** (always exit):
- `ReturnStatement`: Returns `(true, true)` - exits AND returns
- `RaiseStatement`: Returns `(false, true)` - exits but doesn't return normally
- `BreakStatement`: Exits loop (validates loop context)
- `ContinueStatement`: Exits iteration (validates loop context)

**Control Flow**:
- `IfStatement`: Analyzed via `ValidateIf()`
- `WhileStatement`: Analyzed via `ValidateWhile()`
- `ForStatement`: Analyzed via `ValidateFor()`
- `TryStatement`: Analyzed via `ValidateTry()`

**Ignored**:
- Nested `FunctionDef`: Validated separately, doesn't affect outer control flow
- Type definitions (`ClassDef`, `StructDef`, etc.): Don't affect control flow

**Source Reference**: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs:102-154`

### 4. `ValidateIf(IfStatement)` - Conditional Branch Logic

**Purpose**: Determines if an if/elif/else chain always returns.

**Algorithm**:

```csharp
var (thenReturns, _) = ValidateBlock(ifStmt.ThenBody);
bool allBranchesReturn = thenReturns;

// Check all elif branches
foreach (var elifClause in ifStmt.ElifClauses)
{
    var (elifReturns, _) = ValidateBlock(elifClause.Body);
    allBranchesReturn = allBranchesReturn && elifReturns;
}

// Must have an else branch for ALL paths to return
if (ifStmt.ElseBody != null && ifStmt.ElseBody.Count > 0)
{
    var (elseReturns, _) = ValidateBlock(ifStmt.ElseBody);
    allBranchesReturn = allBranchesReturn && elseReturns;
}
else
{
    allBranchesReturn = false; // No else = not all paths covered
}
```

**Key Design Decision**: An if/elif chain only "always returns" if:
1. Every branch (if, all elifs, else) always returns
2. There IS an else branch (otherwise there's a path that skips all branches)

This is a conservative analysis—it doesn't consider conditions like `if true: return 1`.

**Source Reference**: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs:156-182`

### 5. `ValidateWhile(WhileStatement)` and `ValidateFor(ForStatement)`

**Purpose**: Validate loop bodies and track loop context.

**Implementation Pattern**:
```csharp
_loopDepth++;
var (bodyReturns, _) = ValidateBlock(whileStmt.Body);
_loopDepth--;

return (false, false); // Loops don't guarantee execution
```

**Why Loops Don't "Always Return"**:
- `while` loops: Condition might be false on first check
- `for` loops: Iterator might be empty

Even if the loop body always returns, the loop itself might never execute, so we return `(false, false)`.

**Loop Depth Tracking**: The `_loopDepth` counter enables validation of `break`/`continue`:

```csharp
case BreakStatement:
    if (_loopDepth == 0)
    {
        AddError("'break' statement outside loop", ...);
    }
```

**Source Reference**:
- `ValidateWhile`: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs:184-192`
- `ValidateFor`: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs:194-202`

### 6. `ValidateTry(TryStatement)` - Exception Handling Analysis

**Purpose**: Determines if try/except/finally blocks always return.

**Complex Logic**:

```csharp
var (tryReturns, _) = ValidateBlock(tryStmt.Body);

bool allHandlersReturn = true;
foreach (var handler in tryStmt.Handlers)
{
    var (handlerReturns, _) = ValidateBlock(handler.Body);
    allHandlersReturn = allHandlersReturn && handlerReturns;
}

bool finallyReturns = false;
if (tryStmt.FinallyBody != null)
{
    var (finReturns, _) = ValidateBlock(tryStmt.FinallyBody);
    finallyReturns = finReturns;
}

// All paths return if:
// - Finally returns (overrides everything), OR
// - Try returns AND all handlers return
bool allPathsReturn = finallyReturns || (tryReturns && allHandlersReturn);
```

**Key Insight**:
- If `finally` returns, it overrides any other return (this is how Python/C# work)
- Otherwise, both the try block AND all exception handlers must return
- If there's no handler for a possible exception, execution might not return through this block

**Source Reference**: `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs:204-228`

## Dependencies

### Internal Sharpy Dependencies

**From `Sharpy.Compiler.Logging`**:
- `ICompilerLogger`: Logging interface
- `NullLogger`: No-op logger implementation

**From `Sharpy.Compiler.Parser.Ast`**:
All statement and expression AST node types:
- `Statement` (base class)
- `ReturnStatement`, `RaiseStatement`, `BreakStatement`, `ContinueStatement`
- `IfStatement` (with `ElifClauses`, `ElseBody`)
- `WhileStatement`, `ForStatement`, `TryStatement`
- `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
- `ExpressionStatement`, `EllipsisLiteral`

**Semantic Types**:
- `SemanticType`: Type representation from semantic analysis
- `SemanticError`: Error structure for reporting issues

### Cross-References

This validator works closely with:
- **[Parser](../Parser/Parser.md)**: Consumes AST produced by parser
- **[Statement AST Nodes](../Parser/Ast/Statement.md)**: All statement types referenced
- **[SemanticError](./SemanticError.md)**: Error reporting structure
- **[SemanticType](./SemanticType.md)**: Type representation

## Patterns and Design Decisions

### 1. **Recursive Descent Validation**

The validator mirrors the structure of the AST with recursive methods:
- `ValidateBlock` → `ValidateStatement` → `ValidateIf/While/For/Try` → `ValidateBlock`

This makes the code easy to understand and maintain—each AST construct has a corresponding validation method.

### 2. **Conservative Analysis**

The validator uses **conservative static analysis**:
- Doesn't evaluate conditions (`if true: return 1` doesn't count as "always returns")
- Treats loops as possibly never executing
- Requires explicit `else` clause for if-statements to "always return"

**Why Conservative?**: It's better to occasionally require an extra `return` statement than to miss a case where a return is truly missing.

### 3. **Dual Return Values: `(alwaysReturns, alwaysExits)`**

Methods return two booleans:
- `alwaysReturns`: For checking function return requirements
- `alwaysExits`: For detecting unreachable code

Example: `raise` exits but doesn't return:
```python
def foo() -> int:
    raise ValueError("error")  # alwaysExits=true, alwaysReturns=false
    print("unreachable")       # Would be detected
```

### 4. **Stateful Validation with Depth Tracking**

The `_loopDepth` counter is incremented/decremented as we enter/exit loops:
```csharp
_loopDepth++;
ValidateBlock(loopBody);
_loopDepth--;
```

This is simpler than threading loop context through all method calls.

### 5. **Error Accumulation**

Instead of throwing on first error, the validator:
- Collects all errors in `_errors` list
- Continues validation to find multiple issues
- Exposes errors via read-only `Errors` property

This gives better developer experience (fix multiple issues at once).

### 6. **Special Case Handling for Abstract Methods**

The validator recognizes two patterns for abstract methods:

**Explicit decorator**:
```python
@abstract
def method(self) -> int:
    ...
```

**Ellipsis body** (implicit in `@abstract` class):
```python
class Foo:
    def method(self) -> int:
        ...
```

Both are skipped because they're not real implementations. The ellipsis pattern is also used in concrete classes to generate `NotImplementedException` at runtime, which also doesn't need return validation.

## Debugging Tips

### Common Issues and How to Debug Them

**1. False "missing return" errors**

If users report functions that clearly return in all paths but still get errors:

- Check `ValidateIf()` logic: Does the if-chain have an `else` clause?
- Check loop handling: Remember loops are treated as "might not execute"
- Add debug logging to see what `alwaysReturns` values are computed:

```csharp
_logger.LogDebug($"Block returns: {alwaysReturns}, exits: {alwaysExits}");
```

**2. Unreachable code not detected**

If unreachable code isn't being flagged:

- Verify `alwaysExits` is being set correctly in `ValidateStatement()`
- Check if the unreachable code is in a different block (each block validates separately)
- Look for edge cases in control flow combinators (try/finally, nested loops)

**3. Break/continue validation issues**

If `break`/`continue` errors are wrong:

- Check `_loopDepth` tracking: Is it incremented before validating loop body?
- Verify `_loopDepth--` happens even if validation throws
- Consider switch statements (Sharpy might add these in future)

**4. Abstract method validation bugs**

If abstract methods are being validated when they shouldn't:

- Check both the decorator check AND ellipsis check (lines 37-43)
- Verify `Decorators` collection is populated correctly by parser
- Test edge case: `@abstract` with non-ellipsis body (should still be skipped)

### Debugging Workflow

1. **Enable debug logging**:
   ```csharp
   var validator = new ControlFlowValidator(logger: myLogger);
   ```

2. **Check error details**:
   ```csharp
   foreach (var error in validator.Errors)
   {
       Console.WriteLine($"Line {error.Line}, Col {error.Column}: {error.Message}");
   }
   ```

3. **Test with minimal examples**:
   Create the smallest Sharpy code that reproduces the issue, then step through validation.

4. **AST inspection**:
   Use `AstDumper` (see [Parser documentation](../Parser/AstDumper.md)) to verify the AST structure matches expectations.

## Contribution Guidelines

### Kinds of Changes You Might Make

**1. New Statement Types**

When adding new control flow statements (e.g., `match`, `switch`):

```csharp
case MatchStatement matchStmt:
    return ValidateMatch(matchStmt);
```

Add a new validator method following the pattern:
```csharp
private (bool, bool) ValidateMatch(MatchStatement matchStmt)
{
    // Validate each case/pattern
    // Return true only if ALL cases covered AND all return
}
```

**2. New Control Flow Terminators**

If adding statements that exit (like `yield`, `goto`):
- Return `(false, true)` for non-returning exits
- Update `alwaysExits` tracking in `ValidateBlock()`
- Add context validation if needed (like `_loopDepth` for loops)

**3. Enhanced Analysis**

Potential improvements:
- **Constant folding**: Recognize `if true:` as always-taken
- **Type-based analysis**: `if x is None: return` followed by code treating `x` as non-None
- **Infinite loop detection**: `while true:` with return inside could count as "always returns"

**4. Better Error Messages**

Current errors are generic. You could:
- Add suggestions: "Did you forget to add an 'else' branch?"
- Show which branch is missing a return
- Highlight the specific path that doesn't return

**5. Testing**

When modifying this file:
- Add unit tests for new statement types
- Test edge cases (deeply nested loops, complex try/except)
- Verify error messages are helpful
- Test with real Sharpy code examples

### Code Style Guidelines

**Follow existing patterns**:
- Use tuple returns `(bool, bool)` for dual return values
- Increment/decrement depth counters immediately around block validation
- Use pattern matching in switch statements
- Add XML documentation comments for public methods

**Error reporting**:
- Always include line/column information
- Make messages user-friendly (avoid compiler jargon)
- Be specific about what's wrong and where

**Performance**:
- This is a single-pass analysis, keep it efficient
- Don't create unnecessary allocations
- Consider using `readonly` for fields that don't change

### Testing Your Changes

Create test cases in the semantic analysis test suite:

```csharp
[Fact]
public void DetectsUnreachableAfterReturn()
{
    var code = @"
def foo() -> int:
    return 42
    print('unreachable')  # Should error
";
    // Validate and check errors
}
```

Test both positive cases (valid code) and negative cases (should produce errors).

## Cross-References

### Related Files in Semantic Analysis

**Core Semantic Components**:
- **[SemanticError](./SemanticError.md)**: Error reporting structure used by this validator
- **[SemanticType](./SemanticType.md)**: Type representation, used for return type checking
- **[TypeChecker](./TypeChecker.md)**: Provides `returnType` parameter to `ValidateFunction()`
- **[SymbolTable](./SymbolTable.md)**: Manages scopes and function definitions
- **[NameResolver](./NameResolver.md)**: Resolves names before control flow validation

**Other Validators**:
- **[AccessValidator](./AccessValidator.md)**: Validates access modifiers
- **[ProtocolValidator](./ProtocolValidator.md)**: Validates protocol implementations
- **[OperatorValidator](./OperatorValidator.md)**: Validates operator overloading

**Upstream**:
- **[Parser](../Parser/Parser.md)**: Produces the AST this validator consumes
- **[Statement Nodes](../Parser/Ast/Statement.md)**: Statement types validated here
- **[Expression Nodes](../Parser/Ast/Expression.md)**: Expression types (like `EllipsisLiteral`)

**Downstream**:
- **[RoslynEmitter](../CodeGen/RoslynEmitter.md)**: Code generation (assumes valid control flow)
- **[Compiler](../Compiler.md)**: Orchestrates all compilation phases

---

## Summary

The `ControlFlowValidator` is a straightforward but essential component that ensures Sharpy code has valid control flow before code generation. Its recursive-descent design mirrors the AST structure, making it easy to understand and extend. The conservative analysis approach prioritizes correctness over cleverness, which is appropriate for a compiler validation pass.

Key takeaways for newcomers:
- **Stateful but single-use**: Create new validator for each function
- **Conservative**: Doesn't try to be too clever about analysis
- **Accumulates errors**: Finds all issues, doesn't stop at first error
- **Context-aware**: Tracks loop depth for break/continue validation
- **Pure validation**: Doesn't modify AST, just reports errors
- **Special handling**: Abstract methods and ellipsis bodies are skipped
