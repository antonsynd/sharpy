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

- `_symbolTable: SymbolTable` - Symbol table for variable/function lookup and scope management
- `_semanticInfo: SemanticInfo` - Stores type information for AST nodes (consumed by code generator)
- `_typeResolver: TypeResolver` - Resolves type annotations to semantic types
- `_typeInference: TypeInferenceService` - Infers types for operators and iterations (extracted from validators)
- `_validationPipeline: ValidationPipeline` - Runs V2 validators after type checking (always enabled)
- `_currentFunctionReturnType: SemanticType?` - Tracks expected return type in current function context
- `_currentClass: TypeSymbol?` - Track current class being checked (for self parameter typing)
- `_narrowedTypes: Dictionary<string, SemanticType>` - Maps variable names to narrowed types in conditional contexts
- `_inExceptBlock: bool` - Tracks whether we're inside an exception handler (for bare `raise` validation)
- `_currentMethodName: string?` - Track current method context for super() validation
- `_currentMethodIsOverride: bool` - Whether current method overrides a base method
- `_currentMethodIsDunder: bool` - Whether current method is a dunder method
- `_controlFlowDepth: int` - Nesting depth of control flow structures
- `_superInitCalled: bool` - Track if super().__init__() was called
- `_logger: ICompilerLogger` - Logger for diagnostic output
- `_errors: List<SemanticError>` - Accumulated errors from type checking

## Architecture and Control Flow

### Statement Dispatcher

The main entry point for statement checking is `CheckStatement()` in `TypeChecker.cs` (lines 177-250), which dispatches to the appropriate method in this file:

```csharp
private void CheckStatement(Statement statement)
{
    switch (statement)
    {
        case Assignment assignment:
            CheckAssignment(assignment);
            break;
        case VariableDeclaration varDecl:
            CheckVariableDeclaration(varDecl);
            break;
        case ReturnStatement returnStmt:
            CheckReturn(returnStmt);
            break;
        case IfStatement ifStmt:
            CheckIf(ifStmt);
            break;
        // ... more cases
    }
}
```

### Type Checking Phases

The complete type checking pipeline in Sharpy follows these phases:

1. **Name Resolution** (`NameResolver.cs`) - Declares symbols, resolves imports, builds inheritance hierarchy
2. **Type Resolution** (`TypeResolver.cs`) - Resolves type annotations to SemanticTypes
3. **Type Checking** (`TypeChecker.cs` + partials) - **This file participates here**
   - Check definitions (classes, functions)
   - Check statements (assignments, control flow) ← **THIS FILE**
   - Check expressions (calls, operators, literals)
4. **Validation Pipeline** - Runs V2 validators for semantic rules
5. **Code Generation Info** - Computes metadata for emission

### How Statements Connect to Expressions

Statement checking frequently calls `CheckExpression()` (defined in `TypeChecker.Expressions.cs`):

```csharp
// In CheckAssignment
var valueType = CheckExpression(assignment.Value);  // Check the RHS expression

// In CheckIf
var condType = CheckExpression(ifStmt.Test);  // Check the condition expression

// In CheckReturn
var returnType = CheckExpression(returnStmt.Value);  // Check return value
```

This creates a bidirectional relationship:
- **Statements** contain **expressions** (assignments have RHS, if has condition, etc.)
- **Statement checking** delegates to **expression checking** for nested expressions

### SemanticInfo Population

As statements are checked, type information is stored in `SemanticInfo` for use by code generation:

```csharp
// Store symbol binding for identifier
_semanticInfo.SetIdentifierSymbol(targetId, newSymbol);

// Store inferred type for expression
_semanticInfo.SetExpressionType(targetId, inferredType);

// Later, RoslynEmitter reads this data:
var exprType = _semanticInfo.GetExpressionType(expression);
```

This separation of concerns keeps the type checker focused on **validation** while `SemanticInfo` provides **data** for downstream consumers.

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

#### Augmented Assignments (lines 151-184)
```python
# Example: x += 5
```
- Operators: `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=`, `&=`, `|=`, `^=`, `<<=`, `>>=`
- Cannot use augmented assignment on constants (checked at line 155-164)
- Uses `TypeInferenceService.InferAugmentedAssignmentType()` (line 170-173) which:
  - Prefers in-place dunder methods (e.g., `__iadd__`)
  - Falls back to binary operators (e.g., `__add__`)
  - Returns the result type of the operation
- Verifies result type is assignable to target type (line 176-182)
- **Note**: Detailed operator validation happens in the ValidationPipeline (V2 validators)

#### Regular Assignment Type Checking (lines 186-201)
- Validates that value type is assignable to target type using `IsAssignable()` helper
- Special error message for assigning `None` (VoidType) to non-nullable types (lines 190-194)
  ```python
  x: int = None  # ERROR: Cannot assign 'None' to non-nullable type 'int'
  ```
- Generic error for other type mismatches (lines 196-199)

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

1. **Resolve declared type** (line 205):
   ```csharp
   var declaredType = _typeResolver.ResolveTypeAnnotation(varDecl.Type);
   ```

2. **Handle initializer** (lines 207-235):
   - Check initializer expression type: `CheckExpression(varDecl.InitialValue)`
   - If `auto` is used (declaredType is UnknownType), infer type from initializer (lines 212-219)
   - Otherwise, validate that initializer type is assignable to declared type (lines 220-235)
   - Special error for `None` assigned to non-nullable types (lines 223-228)

3. **Validate `auto` without initializer** (lines 236-240):
   ```python
   x: auto  # ERROR: Variable 'x' declared with 'auto' must have an initializer
   ```

4. **Constant vs Variable Handling** (lines 242-299):
   - **Module-level constants** (lines 248-256): Already created by `NameResolver`, just update type
   - **Function-level constants** (lines 258-270): Created here with `IsConstant = true`
   - **Regular variables** (lines 272-298): Can be redefined in same scope (Python-like)
     - Check if existing symbol is a constant → error if trying to redefine
     - Otherwise allow redefinition with new type (line 286)

**Key Design Decision**: Variables can be redefined with different types in the same scope, but constants cannot be redefined at all.

**Debugging Tip**: If you see "Cannot redefine constant variable" errors, remember that `const` variables are immutable once declared. This is different from Python's behavior.

---

### 3. CheckReturn(ReturnStatement returnStmt)

**Purpose**: Validates return statements match the current function's return type.

**Validation**:
- Ensures return is inside a function (checks `_currentFunctionReturnType != null`) at line 303-308
- If value is provided, checks type compatibility with function's return type (lines 310-317)
  - Uses `IsAssignable()` to verify compatibility
- If no value (bare `return`), ensures function returns `void` (lines 319-323)

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
- Requires boolean condition (lines 328-333)
- Allows `UnknownType` to avoid cascading errors when earlier type resolution fails

#### Type Narrowing Support (lines 335-398)
This is a critical feature for Sharpy's type system!

```python
x: int | None = get_value()
if x is not None:
    # Inside this block, x is narrowed to 'int'
    print(x + 5)  # OK!
else:
    # Inside else block, type narrowing for 'else' branch would apply
    pass
```

**Implementation**:
1. Extract narrowed types for then-branch: `ExtractNarrowedTypes(ifStmt.Test, true)` (line 336)
2. Extract narrowed types for else-branch: `ExtractNarrowedTypes(ifStmt.Test, false)` (line 337)
3. Save current narrowed types (line 340)
4. Apply narrowed types for then-branch (lines 341-344)
5. Check then-branch statements in new scope (lines 347-352)
6. For each elif clause:
   - Reset to saved narrowed types (line 364)
   - Extract and apply elif narrowed types (lines 365-369)
   - Check elif statements in new scope (lines 371-376)
7. Apply narrowed types for else-branch (lines 380-384)
8. Check else-branch statements in new scope if present (lines 387-394)
9. Restore original narrowed types (line 397)

**Scope Management**:
- Each branch (`if-then`, `elif`, `if-else`) gets its own scope
- `_controlFlowDepth` is incremented/decremented to track nesting

**Related Spec**: See `docs/language_specification/type_narrowing.md`

---

### 5. CheckWhile(WhileStatement whileStmt)

**Purpose**: Type checks while loops with type narrowing support.

**Similar to CheckIf**:
- Validates boolean condition (lines 403-408)
- Applies type narrowing in loop body based on loop condition (lines 410-418)
- Creates `"while-body"` scope for loop statements (lines 421-426)
- Restores original narrowed types after the loop (line 429)

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

#### Element Type Inference (lines 434-437)
```csharp
var elementType = _typeInference.InferIterableElementType(iterType) ?? SemanticType.Unknown;
```
- Delegates to `TypeInferenceService` which infers the element type from the iterator
- Returns the element type that will be yielded by the iterator
- Errors for non-iterable types are reported by V2 validators in the pipeline

#### Scope Management
**Important**: The `"for-body"` scope is entered BEFORE defining loop variables (line 441). This ensures loop variables are scoped to the loop.

#### Tuple Unpacking in For Loops (lines 444-498)
```python
for x, y in list_of_tuples:
    print(x, y)
```
- Validates that element type is a `TupleType` (lines 447-451)
- Checks element count matches (lines 453-459)
- For each tuple element (lines 463-495):
  - If target is an `Identifier`: Creates `VariableSymbol` and adds to scope (lines 469-486)
  - For complex targets: Just checks the expression (lines 488-493)
- Stores type information in `_semanticInfo` (line 498)

#### Simple Loop Variables (lines 502-523)
```python
for item in items:
    print(item)
```
- Creates `VariableSymbol` with inferred type from iterator (lines 505-513)
- Only defines if not already in scope (lines 516-520)
- Adds to current scope (the `for-body` scope)
- Stores type information in `_semanticInfo` (line 522)

**Loop Body Checking** (lines 526-532):
- Increments/decrements `_controlFlowDepth`
- Checks all statements in the loop body
- Exits the `for-body` scope after completion

**Debugging Tip**: Loop variables are scoped to the loop body. They won't be accessible after the loop exits (unlike Python 2, but like Python 3).

---

### 7. CheckRaise(RaiseStatement raiseStmt)

**Purpose**: Validates raise statements for exception throwing.

**Validation**:
- Bare `raise` (re-raising exception) only valid inside `except` block (lines 538-542)
- Checks `_inExceptBlock` flag to enforce this
- If exception expression is provided, checks its type (lines 544-547)

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
- `try` block: Creates `"try"` scope (lines 553-558)
- Each `except` handler: Creates `"except"` scope (lines 562-571)
- `finally` block: Creates `"finally"` scope (lines 574-582)

**Exception Handler Context** (lines 565-568):
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
- Checks the test expression type (line 587)
- If message is provided, checks the message expression type (lines 588-591)
- No specific type requirements (unlike if conditions which must be bool)

```python
assert x > 0, "x must be positive"
```

**Note**: The actual comment at line 596 indicates this is where the `CheckExpression()` method signature begins in the TypeChecker (for expression type checking).

---

## Dependencies

### Internal Sharpy Dependencies
- **`SymbolTable`** - Variable/function lookup and scope management (enter/exit scopes)
- **`SemanticInfo`** - Stores type information for AST nodes via `SetExpressionType()`, `SetIdentifierSymbol()`
- **`TypeResolver`** - Resolves type annotations via `ResolveTypeAnnotation()`
- **`TypeInferenceService`** - Infers types for augmented assignments and iterable element types
- **`ValidationPipeline`** - Runs V2 validators after type checking (operator, protocol, control flow validation)

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
- Cannot reassign constants (checked in both current and parent scopes - lines 108-122, 155-164)
- Cannot use augmented assignment on constants (lines 155-164)
- Cannot redefine constants with new declarations (lines 276-281)

**Design Note**: This is stricter than Python, where there's no language-enforced constant concept. Sharpy adds compile-time constant enforcement for better type safety.

### 5. Error Recovery
The type checker continues after errors (configurable via `ContinueAfterError` property in main TypeChecker.cs, line 46). This allows finding multiple errors in a single pass, improving developer experience.

### 6. ValidationPipeline Integration
After statement checking completes, `CheckModule()` in TypeChecker.cs (lines 133-137) runs the ValidationPipeline which includes V2 validators:
- **OperatorValidator** - Validates operator usage
- **ProtocolValidator** - Validates protocol compliance  
- **ControlFlowValidator** - Validates break/continue/return
- **AccessValidator** - Validates access levels
- This separation keeps TypeChecker focused on type checking while validators handle semantic rules.

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
- Check that `TypeInferenceService` correctly infers the result type
- Verify the ValidationPipeline logs for detailed operator validation errors
- Check if the type supports the in-place dunder method (e.g., `__iadd__`)
- Fallback to binary operator (e.g., `__add__`) should work if in-place doesn't exist
- Remember: Augmented assignment on constants is caught early (line 155-164)

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
- [`OperatorSignatureValidator.md`](OperatorSignatureValidator.md) - Validates operator signatures in V2 pipeline
- [`ProtocolSignatureValidator.md`](ProtocolSignatureValidator.md) - Validates protocol signatures in V2 pipeline
- [`ProtocolValidator.md`](ProtocolValidator.md) - Legacy protocol validator (V2 version used in pipeline)
- [`ControlFlowValidator.md`](ControlFlowValidator.md) - Validates control flow (break/continue/return)
- [`AccessValidator.md`](AccessValidator.md) - Validates access levels
- [`TypeInferenceService.md`](TypeInferenceService.md) - Infers types for operators and iterations

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

---

## Summary: Key Takeaways

### What This File Does
`TypeChecker.Statements.cs` is responsible for **validating the semantic correctness of statement-level constructs** in Sharpy programs. It ensures:
- Assignments are valid (correct targets, type compatibility, constant immutability)
- Variables are properly declared and initialized
- Control flow is well-typed (boolean conditions, proper scoping)
- Return statements match function signatures
- Exception handling follows language rules
- Loop variables are correctly inferred from iterators

### Core Design Principles

1. **Python-Like Flexibility**: Variables can be reassigned to different types (creates new symbol versions)
2. **Static Type Safety**: All type mismatches are caught at compile-time
3. **Type Narrowing**: Conditional branches automatically narrow nullable types
4. **Proper Scoping**: Every control flow construct creates its own scope
5. **Constant Enforcement**: `const` variables are immutable (stricter than Python)

### Interaction Points

- **Upstream**: Receives AST from Parser, symbol table from NameResolver, resolved types from TypeResolver
- **Downstream**: Populates SemanticInfo for RoslynEmitter code generation
- **Horizontal**: Calls CheckExpression() for nested expressions, uses TypeInferenceService for type inference
- **Validation**: Delegates complex semantic rules to ValidationPipeline (V2 validators)

### When to Modify This File

- Adding new statement types to the language
- Enhancing type narrowing patterns
- Improving error messages for statement-level issues
- Fixing bugs in assignment validation, control flow, or scope management

### Quick Reference: Method-to-Statement Mapping

| Statement Type | Method | Key Features |
|----------------|--------|--------------|
| `x = value` | `CheckAssignment` | Type checking, tuple unpacking, constant checking |
| `x: int = 42` | `CheckVariableDeclaration` | Type inference, constant creation |
| `return expr` | `CheckReturn` | Return type validation |
| `if/elif/else` | `CheckIf` | Boolean conditions, type narrowing, scoping |
| `while cond:` | `CheckWhile` | Boolean conditions, type narrowing |
| `for x in items:` | `CheckFor` | Iterator element type inference, tuple unpacking |
| `raise` / `raise ex` | `CheckRaise` | Exception handler context validation |
| `try/except/finally` | `CheckTry` | Multi-scope management, exception context |
| `assert test, msg` | `CheckAssert` | Simple expression validation |

This file is a critical component of Sharpy's semantic analysis phase, bridging the gap between the syntactic structure (AST) and the type-safe code generation that follows.
