# Walkthrough: TypeChecker.Statements.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`

---

## Overview

This file is part of the `TypeChecker` partial class and contains the statement-level type checking logic for the Sharpy compiler. It handles verification of assignment statements, variable declarations, control flow statements (if/while/for), return statements, exception handling (try/except/raise), and assertions.

**Role in Compiler Pipeline**:
- **Upstream**: Receives AST from the Parser
- **Downstream**: Populates `SemanticInfo` for use by the RoslynEmitter code generator
- **Position**: Semantic Analysis phase (after name resolution, before code generation)

This file is one of five partial class files that make up the complete `TypeChecker` class:
- `TypeChecker.cs` - Main class, constructor, and orchestration
- `TypeChecker.Definitions.cs` - Type checking for function/class definitions
- `TypeChecker.Expressions.cs` - Type checking for expressions
- **`TypeChecker.Statements.cs`** - Type checking for statements (this file)
- `TypeChecker.Utilities.cs` - Helper methods and utilities

## Class Structure

This file defines methods as part of the `public partial class TypeChecker` within the `Sharpy.Compiler.Semantic` namespace.

### Key Instance Variables (from main TypeChecker.cs)

The statement checking methods rely on these instance variables:

- `_symbolTable` - Symbol table for variable/function lookup
- `_semanticInfo` - Stores type information for AST nodes (consumed by code generator)
- `_typeResolver` - Resolves type annotations to semantic types
- `_operatorValidator` - Validates operator usage (e.g., augmented assignments)
- `_protocolValidator` - Validates protocol compliance (e.g., iteration protocol)
- `_currentFunctionReturnType` - Tracks expected return type in current function context
- `_narrowedTypes` - Maps variable names to narrowed types in conditional contexts
- `_inExceptBlock` - Tracks whether we're inside an exception handler (for bare `raise` validation)
- `_controlFlowDepth` - Nesting depth of control flow structures

## Key Methods

### 1. CheckAssignment(Assignment assignment)

**Purpose**: Validates assignment statements and handles type checking for both simple and augmented assignments.

**Key Features**:

#### Assignment Target Validation
```csharp
if (!IsValidAssignmentTarget(assignment.Target))
{
    AddError($"Cannot assign to {GetAssignmentTargetDescription(assignment.Target)}", ...);
}
```
- Valid targets: `Identifier`, `MemberAccess`, `IndexAccess`, `TupleLiteral`
- Invalid targets: `FunctionCall`, `Literal`, `BinaryExpression`, etc.
- Special case: Cannot reassign `self` (line 23-29)

#### Tuple Unpacking (lines 32-99)
```python
# Example: x, y = some_tuple
```
- Validates that the value is a `TupleType`
- Ensures element count matches
- For each element, either:
  - Creates a new variable symbol (for `Identifier` targets)
  - Validates type compatibility (for complex targets like attributes)

**Key Insight**: Tuple unpacking creates **new variable versions** in Sharpy, enabling Python-like dynamic typing.

#### Simple Assignment to Identifier (lines 101-145)
```python
# Example: x = 42
```
- Checks for constant reassignment (both current and parent scopes)
- **Important Design Decision**: Simple assignments in Sharpy create **new variable versions**
- This enables variables to be reassigned to different types (Python-like behavior)
- Creates a fresh `VariableSymbol` with the inferred type

```csharp
// Create a new variable symbol with the inferred type (or redefine existing)
var newSymbol = new VariableSymbol
{
    Name = targetId.Name,
    Type = inferredType,  // Inferred from the right-hand side
    IsConstant = false,
    // ...
};
_symbolTable.Define(newSymbol);  // Redefines if already exists
```

#### Augmented Assignments (lines 151-178)
```python
# Example: x += 5
```
- Operators: `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=`, `&=`, `|=`, `^=`, `<<=`, `>>=`
- Cannot use augmented assignment on constants
- Delegates to `OperatorValidator.ValidateAugmentedAssignment()` which:
  - Prefers in-place dunder methods (e.g., `__iadd__`)
  - Falls back to binary operators (e.g., `__add__`)
  - Verifies result type is assignable to target

#### Regular Assignment Type Checking (lines 180-195)
- Validates that value type is assignable to target type
- Special error message for assigning `None` to non-nullable types

**Debugging Tip**: If you see unexpected assignment errors, check if the variable is a constant. Constant checking happens at multiple levels (current scope and parent scopes).

---

### 2. CheckVariableDeclaration(VariableDeclaration varDecl)

**Purpose**: Type checks variable declarations with optional type annotations and initializers.

**Supported Forms**:
```python
x: int = 42          # Explicit type with initializer
x: int               # Explicit type, no initializer
x: auto = 42         # Type inference
const PI: float = 3.14  # Constant
```

**Flow**:

1. **Resolve declared type** (line 199):
   ```csharp
   var declaredType = _typeResolver.ResolveTypeAnnotation(varDecl.Type);
   ```

2. **Handle initializer** (lines 201-229):
   - If `auto` is used, infer type from initializer
   - Otherwise, validate that initializer type is assignable to declared type
   - Special error for `None` assigned to non-nullable types

3. **Validate `auto` without initializer** (lines 230-234):
   ```python
   x: auto  # ERROR: auto requires initializer
   ```

4. **Constant vs Variable Handling** (lines 236-264):
   - **Module-level constants**: Already created by `NameResolver`, just update type
   - **Function-level constants**: Created here with `IsConstant = true`
   - **Regular variables**: Can be redefined in same scope (Python-like)

**Key Design Decision**: Variables can be redefined with different types in the same scope, but constants cannot be redefined at all.

**Debugging Tip**: If you see "Cannot redefine constant variable" errors, remember that `const` variables are immutable once declared. This is different from Python's behavior.

---

### 3. CheckReturn(ReturnStatement returnStmt)

**Purpose**: Validates return statements match the current function's return type.

**Validation**:
- Ensures return is inside a function (checks `_currentFunctionReturnType != null`)
- If value is provided, checks type compatibility with function's return type
- If no value (bare `return`), ensures function returns `void`

**Example Errors**:
```python
def get_number() -> int:
    return "hello"  # ERROR: Cannot return type 'str' from function expecting 'int'

def get_number() -> int:
    return  # ERROR: Function expects return type 'int' but got no return value
```

---

### 4. CheckIf(IfStatement ifStmt)

**Purpose**: Type checks if/elif/else statements with support for type narrowing.

**Key Features**:

#### Condition Type Checking
- Requires boolean condition (line 322-327)
- Allows `UnknownType` to avoid cascading errors

#### Type Narrowing Support (lines 329-392)
This is a critical feature for Sharpy's type system!

```python
x: int | None = get_value()
if x is not None:
    # Inside this block, x is narrowed to 'int'
    print(x + 5)  # OK!
else:
    # Inside else block, x is narrowed to 'None'
    pass
```

**Implementation**:
1. Extract narrowed types for then-branch: `ExtractNarrowedTypes(ifStmt.Test, true)`
2. Extract narrowed types for else-branch: `ExtractNarrowedTypes(ifStmt.Test, false)`
3. Save current narrowed types
4. Apply narrowed types for each branch before checking statements
5. Restore original narrowed types after if statement

**Scope Management**:
- Each branch (`if-then`, `elif`, `if-else`) gets its own scope
- `_controlFlowDepth` is incremented/decremented to track nesting

**Related Spec**: See `docs/language_specification/type_narrowing.md`

---

### 5. CheckWhile(WhileStatement whileStmt)

**Purpose**: Type checks while loops with type narrowing support.

**Similar to CheckIf**:
- Validates boolean condition
- Applies type narrowing in loop body (based on loop condition)
- Creates `"while-body"` scope for loop statements

```python
x: int | None = get_value()
while x is not None:
    # x is narrowed to 'int' inside loop
    process(x)
    x = get_next_value()
```

---

### 6. CheckFor(ForStatement forStmt)

**Purpose**: Type checks for loops with support for iteration protocol and tuple unpacking.

**Key Features**:

#### Iteration Protocol Validation (lines 428-435)
```csharp
var elementType = _protocolValidator.ValidateIteration(
    iterType,
    forStmt.Iterator.LineStart,
    forStmt.Iterator.ColumnStart);
```
- Delegates to `ProtocolValidator` which checks for `__iter__` protocol
- Returns the element type that will be yielded

#### Scope Management
**Important**: The `"for-body"` scope is entered BEFORE defining loop variables (line 439). This ensures loop variables are scoped to the loop.

#### Tuple Unpacking in For Loops (lines 442-494)
```python
for x, y in list_of_tuples:
    print(x, y)
```
- Validates that element type is a `TupleType`
- Checks element count matches
- Defines loop variables inside the for-body scope
- Stores type information in `_semanticInfo`

#### Simple Loop Variables (lines 500-521)
```python
for item in items:
    print(item)
```
- Creates `VariableSymbol` with inferred type from iterator
- Adds to current scope (the `for-body` scope)

**Debugging Tip**: Loop variables are scoped to the loop body. They won't be accessible after the loop exits (unlike Python 2, but like Python 3).

---

### 7. CheckRaise(RaiseStatement raiseStmt)

**Purpose**: Validates raise statements for exception throwing.

**Validation**:
- Bare `raise` (re-raising exception) only valid inside `except` block
- Checks `_inExceptBlock` flag to enforce this

```python
try:
    risky_operation()
except Exception:
    raise  # OK: inside except block

raise  # ERROR: Bare 'raise' can only be used inside an exception handler
```

---

### 8. CheckTry(TryStatement tryStmt)

**Purpose**: Type checks try/except/finally blocks.

**Scope Management**:
- `try` block: Creates `"try"` scope
- Each `except` handler: Creates `"except"` scope
- `finally` block: Creates `"finally"` scope

**Exception Handler Context** (lines 563-566):
```csharp
_inExceptBlock = true;
foreach (var stmt in handler.Body)
    CheckStatement(stmt);
_inExceptBlock = false;
```
This flag enables validation of bare `raise` statements.

**Control Flow Depth**: Each block increments/decrements `_controlFlowDepth` for proper nesting tracking.

---

### 9. CheckAssert(AssertStatement assertStmt)

**Purpose**: Type checks assert statements.

**Simple Validation**:
- Checks the test expression type
- If message is provided, checks the message expression type
- No specific type requirements (unlike if conditions which must be bool)

```python
assert x > 0, "x must be positive"
```

---

## Dependencies

### Internal Sharpy Dependencies
- **`SymbolTable`** - Variable/function lookup and scope management
- **`SemanticInfo`** - Stores type information for AST nodes
- **`TypeResolver`** - Resolves type annotations
- **`OperatorValidator`** - Validates operators in augmented assignments
- **`ProtocolValidator`** - Validates iteration protocol in for loops

### AST Types (from `Sharpy.Compiler.Parser.Ast`)
- `Assignment`, `VariableDeclaration`, `ReturnStatement`
- `IfStatement`, `WhileStatement`, `ForStatement`
- `TryStatement`, `RaiseStatement`, `AssertStatement`
- `Identifier`, `TupleLiteral`, `Expression`

### Semantic Types
- `SemanticType`, `VoidType`, `TupleType`, `NullableType`, `UnknownType`
- `VariableSymbol`, `TypeSymbol`

---

## Patterns and Design Decisions

### 1. Python-Like Variable Redefinition
**Design**: Variables can be reassigned to different types, creating new "versions" in the symbol table.

```python
x = 42        # x: int
x = "hello"   # x: str (new version)
```

This is implemented by calling `_symbolTable.Define()` which replaces existing symbols:
```csharp
var newSymbol = new VariableSymbol { Name = targetId.Name, Type = inferredType, ... };
_symbolTable.Define(newSymbol);  // Replaces if exists
```

### 2. Scope Management Pattern
Every control flow construct follows this pattern:
```csharp
_symbolTable.EnterScope("scope-name");
_controlFlowDepth++;
// ... check statements ...
_controlFlowDepth--;
_symbolTable.ExitScope();
```

### 3. Type Narrowing with Save/Restore
Conditional branches use a save/restore pattern for narrowed types:
```csharp
var savedNarrowedTypes = new Dictionary<string, SemanticType>(_narrowedTypes);
// ... apply narrowed types for branch ...
// ... check branch statements ...
_narrowedTypes = savedNarrowedTypes;  // Restore
```

### 4. Constant Immutability
Constants are enforced at multiple levels:
- Cannot reassign constants (checked in both current and parent scopes)
- Cannot use augmented assignment on constants
- Cannot redefine constants with new declarations

### 5. Error Recovery
The type checker continues after errors (configurable via `ContinueAfterError` property). This allows finding multiple errors in a single pass.

---

## Debugging Tips

### 1. Assignment Errors
If you see unexpected assignment errors:
- Check if the variable is declared as `const`
- Check if you're assigning to an invalid target (e.g., function call)
- For tuple unpacking, verify element counts match

### 2. Scope Issues
If variables aren't found or have wrong types:
- Check `_controlFlowDepth` - are you in the right nesting level?
- Use `_symbolTable.Lookup(name, searchParents: true/false)` - is `searchParents` correct?
- Remember that loop variables are scoped to the loop body

### 3. Type Narrowing Issues
If narrowed types aren't working:
- Verify that `ExtractNarrowedTypes()` (defined in `TypeChecker.Utilities.cs`) recognizes the pattern
- Check that narrowed types are properly saved/restored around branches
- Remember narrowing only works within control flow branches

### 4. Augmented Assignment Failures
If augmented assignments fail:
- Check `OperatorValidator` logs for operator support details
- Verify the type supports the in-place dunder method (e.g., `__iadd__`)
- Fallback to binary operator (e.g., `__add__`) should work if in-place doesn't exist

### 5. Adding Logging
Use `_logger.LogDebug()` to add diagnostic output:
```csharp
_logger.LogDebug($"Checking assignment: {assignment.Target} = {assignment.Value}");
```

---

## Contribution Guidelines

### When to Modify This File

Add or modify code in this file when:
1. **Adding new statement types** - Follow the pattern of existing `Check*` methods
2. **Enhancing type narrowing** - Modify `CheckIf`/`CheckWhile` logic
3. **Improving error messages** - Update `AddError()` calls with clearer messages
4. **Adding new assignment operators** - Update `CheckAssignment` augmented assignment handling

### Coding Conventions

1. **Method Naming**: Use `Check*` prefix for statement checking methods
2. **Scope Pattern**: Always use `EnterScope`/`ExitScope` with matching `_controlFlowDepth` tracking
3. **Error Handling**: Use `AddError()` with line/column information
4. **Type Checking**: Always check expression types before using type-specific properties
5. **Symbol Table**: Use `searchParents: true/false` explicitly for clarity

### Testing Considerations

When modifying this file, ensure you have tests for:
- Valid statements (happy path)
- Invalid assignment targets
- Constant reassignment errors
- Tuple unpacking (matching and mismatched counts)
- Type narrowing in if/while branches
- Scope behavior for control flow constructs
- Exception handling context (`_inExceptBlock`)

### Common Pitfalls

1. **Forgetting to exit scopes** - Always match `EnterScope` with `ExitScope`
2. **Not checking `_currentFunctionReturnType` for null** - Could be outside function
3. **Modifying `_narrowedTypes` without save/restore** - Will leak into other branches
4. **Not incrementing `_controlFlowDepth`** - Breaks control flow analysis
5. **Checking types before resolving** - Always call `CheckExpression()` first

---

## Cross-References

### Related TypeChecker Partial Classes
- [`TypeChecker.cs`](TypeChecker.md) - Main class structure, dependencies, and entry point
- [`TypeChecker.Definitions.cs`](TypeChecker.Definitions.md) - Function and class definition type checking
- [`TypeChecker.Expressions.cs`](TypeChecker.Expressions.md) - Expression type checking (called by `CheckExpression()`)
- [`TypeChecker.Utilities.cs`](TypeChecker.Utilities.md) - Helper methods including `ExtractNarrowedTypes()`, `IsValidAssignmentTarget()`, `IsAssignable()`

### Related Validators
- [`OperatorValidator.md`](OperatorValidator.md) - Validates augmented assignments and operators
- [`ProtocolValidator.md`](ProtocolValidator.md) - Validates iteration protocol for for loops
- [`ControlFlowValidator.md`](ControlFlowValidator.md) - Validates control flow (break/continue)

### Related Core Components
- [`SymbolTable.md`](SymbolTable.md) - Symbol table and scope management
- [`SemanticInfo.md`](SemanticInfo.md) - Storage for type information
- [`Symbol.md`](Symbol.md) - Symbol types including `VariableSymbol`

### Downstream Consumers
- [`RoslynEmitter.Statements.md`](../CodeGen/RoslynEmitter.Statements.md) - Code generation for statements

### Language Specifications
- `docs/language_specification/type_narrowing.md` - Type narrowing rules
- `docs/language_specification/type_annotations.md` - Type annotation syntax
- `docs/language_specification/type_casting.md` - Type casting and conversion
