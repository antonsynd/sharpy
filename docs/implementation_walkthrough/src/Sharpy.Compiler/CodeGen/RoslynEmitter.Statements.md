# Walkthrough: RoslynEmitter.Statements.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`

---

## Overview

This partial class file handles **statement-level code generation** in the Sharpy compiler. It transforms Sharpy's typed AST statements into C# Roslyn syntax trees, covering:

- **Control flow**: `if`, `while`, `for`, `try/catch/finally`
- **Assignments**: Simple, augmented (`+=`, `//=`, etc.), and tuple unpacking
- **Variable declarations**: Local variables, module-level fields, and const values
- **Special statements**: `assert`, `raise`, `pass`, `break`, `continue`, `return`
- **Python-specific features**: Loop `else` clauses, `try/else`, floor division semantics

This is the **final stage** of the compiler pipeline. The generated Roslyn syntax trees are serialized to C# source code, which is then compiled by the .NET compiler.

### Compiler Pipeline Position

```
Typed AST (from Semantic Analysis)
    ↓
RoslynEmitter.Statements ← YOU ARE HERE
    ↓
C# Roslyn Syntax Tree
    ↓
C# Source Code
    ↓
.NET Compiler → Assembly
```

## Class Structure

This is a **partial class** extending `RoslynEmitter`. The class is split across multiple files:

- **RoslynEmitter.cs** - Main class definition, fields, utilities
- **RoslynEmitter.Statements.cs** - Statement generation (this file)
- **RoslynEmitter.Expressions.cs** - Expression generation
- **RoslynEmitter.Operators.cs** - Binary/unary operators
- **RoslynEmitter.ClassMembers.cs** - Class methods, properties, etc.
- **RoslynEmitter.ModuleClass.cs** - Module-level code organization
- **RoslynEmitter.CompilationUnit.cs** - Top-level compilation unit
- **RoslynEmitter.TypeDeclarations.cs** - Class, interface, enum definitions

### Important Dependencies

- **Roslyn APIs**: `Microsoft.CodeAnalysis.CSharp.SyntaxFactory` for building syntax nodes
- **AST Types**: `Sharpy.Compiler.Parser.Ast` (Statement, Expression, etc.)
- **Type Mapping**: `_typeMapper` (TypeMapper instance) converts Sharpy types → C# types
- **Name Mangling**: `NameMangler` converts snake_case → PascalCase/camelCase
- **Semantic Info**: Type information from semantic analysis phase

## Key Methods

### 1. GenerateBodyStatement - Main Dispatcher

```csharp
private StatementSyntax? GenerateBodyStatement(Statement stmt)
```

**Purpose**: Routes each AST statement type to its specific generator method.

**How it works**: Pattern matching switch expression that dispatches to specialized methods:
- `ReturnStatement` → `GenerateReturn()`
- `Assignment` → `GenerateAssignment()`
- `IfStatement` → `GenerateIf()`
- `ForStatement` → `GenerateFor()`
- etc.

**Returns**: `StatementSyntax?` - Roslyn syntax node or `null` if statement type not supported

**Design Note**: This is the **entry point** for converting statement AST nodes. All statement generation flows through here.

---

### 2. GenerateExpressionStatement - Handling Expression Validity

```csharp
private StatementSyntax GenerateExpressionStatement(ExpressionStatement exprStmt)
```

**Purpose**: Converts Sharpy expression statements to valid C# statements.

**Challenge**: C# is more restrictive than Python about what expressions can be statements. Only method calls, assignments, `new`, and increment/decrement are valid.

**Solution Strategy**:
1. **Special cases**:
   - `None` → Empty statement (no-op)
   - `...` (Ellipsis) → `throw new NotImplementedException()`
2. **Valid C# expressions** → Direct `ExpressionStatement`
3. **Invalid expressions** → Wrapped in discard: `_ = expr;`

**Example**:
```csharp
// Sharpy: print("hello")  → C#: print("hello");  ✓ Valid
// Sharpy: 42              → C#: _ = 42;          ✓ Wrapped
// Sharpy: x + y           → C#: _ = x + y;       ✓ Wrapped
```

**Why this matters**: Allows Python-like expression statements while maintaining C# semantics.

---

### 3. GenerateAssignment - Complex Assignment Logic

```csharp
private StatementSyntax GenerateAssignment(Assignment assign)
```

**Purpose**: Handles all forms of assignment in Sharpy.

**Assignment Types Supported**:

#### a) Simple Identifier Assignment (`x = value`)
- **First declaration**: Generates `var x = value;` and tracks in `_declaredVariables`
- **Reassignment**: Generates `x = value;` (updates existing variable)
- **Module-level variables**: Checks `_moduleVariables` and assigns without redeclaring

**Key Decision Logic** (lines 131-154):
```csharp
if (_variableVersions.ContainsKey(baseName) ||
    _moduleVariables.Contains(name.Name) ||
    _moduleConstVariables.Contains(name.Name))
{
    // Update existing variable
} else {
    // First declaration with var
}
```

#### b) Augmented Assignment (`x += value`, `x //= y`, etc.)
- Converts to: `x = x op value`
- **Special handling** for Python semantics:
  - `/=` (true division) - Always casts to `double` if operands are integers
  - `//=` (floor division) - Uses `Math.Floor()` for Python-style floor semantics
  - `**=` (power) - Calls `System.Math.Pow()`

#### c) Index Assignment (`arr[0] = value`)
- Generates: `arr[index] = value;` using `ElementAccessExpression`
- Supports augmented: `arr[0] += 5` → `arr[0] = arr[0] + 5;`

#### d) Member Assignment (`obj.field = value`)
- Generates: `obj.field = value;` using `MemberAccessExpression`
- Supports augmented operators

#### e) Tuple Unpacking (`x, y = 1, 2`)
- Generates C# tuple deconstruction: `var (x, y) = (1, 2);`
- Marks all identifiers as newly declared
- **Limitation**: Only works with identifier targets, not nested patterns

**Design Pattern**: The method follows a **chain of responsibility** pattern, trying each assignment type in order.

---

### 4. GenerateAugmentedValue - Python Operator Semantics

```csharp
private ExpressionSyntax GenerateAugmentedValue(
    AssignmentOperator op,
    ExpressionSyntax left,
    ExpressionSyntax right,
    Expression? targetAst = null,
    Expression? valueAst = null)
```

**Purpose**: Implements Python's arithmetic semantics in C#.

**Critical Semantic Differences**:

| Sharpy Operator | Python Behavior | C# Translation |
|-----------------|-----------------|----------------|
| `**=` | Power operation | `System.Math.Pow(x, y)` |
| `/=` | Always float division | Cast to `double` if integers |
| `//=` | Floor toward -∞ | `Math.Floor(x / y)` |
| `??=` | Null coalescing | `x ?? y` |

**Example - Floor Division**:
```csharp
// Sharpy: x //= 3
// If x is float: Math.Floor(x / 3)
// If x is int:   (long)Math.Floor((double)x / 3)
```

**Why AST parameters matter**: The `targetAst` and `valueAst` parameters allow type inference to determine if operands are floats or integers, which changes the C# code generation.

---

### 5. GenerateVariableDeclaration - Local Variables

```csharp
private StatementSyntax GenerateVariableDeclaration(VariableDeclaration varDecl)
```

**Purpose**: Generates local variable declarations inside functions/methods.

**Key Features**:

1. **Const handling**:
   - Tracks const variables in `_constVariables` set
   - Uses CONSTANT_CASE naming: `MAX_SIZE`
   - Generates `const` keyword for compile-time literals

2. **Type inference**:
   - `auto` type → `var` in C#
   - `const` without type → Infers from initializer
   - Explicit types → Maps via `_typeMapper`

3. **Target type context** (lines 397-408):
   - Sets `_targetTypeContext` before generating initializer
   - Allows collection literals to infer element types
   - Example: `list[int] nums = [1, 2, 3]` → Collection literal knows to use `int`

4. **Modifiers**:
   - `const` declarations → `const` keyword
   - Regular variables → No modifiers

**Example**:
```sharpy
const PI: float = 3.14159  # → const double PI = 3.14159;
x: auto = get_value()      # → var x = get_value();
```

---

### 6. GenerateModuleLevelField - Module Fields

```csharp
private FieldDeclarationSyntax? GenerateModuleLevelField(VariableDeclaration varDecl)
```

**Purpose**: Generates static fields for module-level variables.

**Execution Order Handling**: One of the trickiest parts of the compiler!

Sharpy allows **execution-order-dependent** module code:
```sharpy
print(x)  # Used before declared!
x = 10    # Declared after use
x = 20    # Redeclared with same type
```

**Solution Strategy**:
1. Pre-analysis identifies variables with execution issues (`_variablesWithExecutionOrderIssues`)
2. Such variables are **skipped** here (return `null`)
3. They're handled as **executable statements** in the module's `Main()` method instead

**Naming Convention**:
```csharp
// Explicitly const OR ALL_CAPS name → CONSTANT_CASE
const MAX_SIZE = 100  → public const int MAX_SIZE = 100;
DEFAULT_TIMEOUT = 30  → public const int DEFAULT_TIMEOUT = 30;  // Inferred const style

// Other names → PascalCase
version = "1.0"       → public static string Version = "1.0";
```

**Field Modifiers** (lines 524-546):
- Compile-time const: `public const`
- Runtime const: `public static readonly`
- Regular variables: `public static`

**Redefinition Detection**:
- Tracks generated field names in `_moduleFieldNames`
- Returns `null` for redefinitions (handled as executable statements)

---

### 7. GenerateIf - Conditional Statements

```csharp
private StatementSyntax GenerateIf(IfStatement ifStmt)
```

**Purpose**: Generates `if`/`elif`/`else` chains.

**Implementation Strategy**: Builds nested `if-else` structure from **bottom-up**:

1. Start with final `else` block (if exists)
2. Process `elif` clauses in **reverse order**
3. Each `elif` becomes a nested `IfStatement` in the previous `else` clause
4. Finally, attach to main `if` condition

**Why reverse order?** To correctly nest the structure:
```csharp
if (a)      { ... }
elif (b)    { ... }  →  if (a) { ... }
elif (c)    { ... }      else if (b) { ... }
else        { ... }        else if (c) { ... }
                             else { ... }
```

**Code Location**: Lines 609-647

---

### 8. GenerateWhile - While Loops with Else

```csharp
private StatementSyntax GenerateWhile(WhileStatement whileStmt)
```

**Purpose**: Handles `while` loops, including Python's `else` clause.

**Simple Case** (no `else`):
```csharp
while (condition) { body }
```

**With Else Clause** - The Tricky Part:

Python's `while/else` executes the `else` block **only if the loop completes without `break`**.

**C# Implementation Pattern**:
```csharp
bool __loopCompleted = true;
while (condition) {
    // ... body ...
    if (should_break) {
        __loopCompleted = false;  // Flag set by BreakWithFlagStatement
        break;
    }
}
if (__loopCompleted) {
    // else block
}
```

**How break is transformed**: See `TransformLoopBodyForElse()` (not in this file) which replaces `break` statements with `BreakWithFlagStatement`.

**Design Note**: This pattern preserves Python semantics using only standard C# constructs.

---

### 9. GenerateFor - Foreach Loops

```csharp
private StatementSyntax GenerateFor(ForStatement forStmt)
```

**Purpose**: Converts Python `for-in` loops to C# `foreach`.

**Challenges**:

1. **Python allows modifying loop variables**:
```python
for i in range(10):
    i += 100  # This is allowed in Python!
```

2. **C# foreach variables are read-only**:
```csharp
foreach (var i in range) {
    i += 100;  // ERROR: Cannot assign to foreach iteration variable
}
```

**Solution** - Always use a temporary variable pattern (lines 736-784):
```csharp
foreach (var __loopVar in items) {
    var i = __loopVar;  // Mutable copy
    // ... body can now modify 'i'
}
```

**Tuple Unpacking Support**:
```sharpy
for x, y in pairs:  # Sharpy
```
↓
```csharp
foreach (var (x, y) in pairs) {  // C#
```

**Else Clause Handling**: Same boolean flag pattern as `while` loops.

**Variable Tracking** (lines 741-749):
- Pre-registers loop variables in `_declaredVariables` **before** generating body
- This ensures assignments to loop variable are treated as updates, not new declarations

---

### 10. GenerateTry - Exception Handling

```csharp
private StatementSyntax GenerateTry(TryStatement tryStmt)
```

**Purpose**: Generates `try/catch/finally` blocks.

**Standard Case**:
```csharp
try { ... }
catch (ExceptionType ex) { ... }
catch { ... }  // Catch-all
finally { ... }
```

**Try/Else Pattern**: Python's `try/else` executes `else` only if **no exception was raised**.

**C# Implementation** (lines 891-955):
```csharp
bool __trySucceeded = false;
try {
    // ... original body ...
    __trySucceeded = true;  // Set flag at end
}
catch (Exception ex) {
    // ... handler ...
}
finally {
    // ... finally block ...
}
if (__trySucceeded) {
    // else block
}
```

**Catch Clause Variants**:
- Named exception: `catch (ValueError ex)`
- Anonymous: `catch (ValueError)`
- Catch-all: `catch` (no type)

**Type Mapping**: Exception types are mapped via `_typeMapper.MapType()`

---

### 11. GenerateAssert - Assertions

```csharp
private StatementSyntax GenerateAssert(AssertStatement assert)
```

**Purpose**: Converts Python-style `assert` to C# `Debug.Assert()`.

**Mapping**:
```sharpy
assert x > 0                    → Debug.Assert(x > 0);
assert x > 0, "x must be positive"  → Debug.Assert(x > 0, "x must be positive");
```

**Why Debug.Assert?**
- Removed in Release builds (like Python's `-O` flag)
- Matches Python's assertion behavior
- Can be configured with `TRACE` and `DEBUG` defines

---

### 12. GenerateRaise - Exception Throwing

```csharp
private StatementSyntax GenerateRaise(RaiseStatement raise)
```

**Purpose**: Converts `raise` to `throw`.

**Two Forms**:
1. `raise Exception("message")` → `throw Exception("message");`
2. `raise` (inside catch block) → `throw;` (re-throw)

---

## Helper Methods & Utilities

### IsValidCSharpStatementExpression
Determines if an expression can stand alone as a C# statement. Currently only method calls (`FunctionCall`) are valid; all others need the discard wrapper (`_ = expr;`).

### GetAugmentedAssignmentOperator
Maps Sharpy's `AssignmentOperator` enum to Roslyn's `SyntaxKind`. Returns `SyntaxKind.None` for operators needing special handling (`/=`, `//=`, `**=`).

### GenerateTrueDivisionAugmented
Implements Python's true division semantics (`/`). Casts integers to `double` before division.

### IsCompileTimeLiteral
Checks if an expression is a compile-time constant that can be used with C# `const` keyword. Only basic literals qualify (int, float, string, bool, None).

### GenerateBreakWithFlag
Generates the flag-setting pattern for loop `else` clauses:
```csharp
{
    flagName = false;
    break;
}
```

---

## State Management & Context Tracking

This file relies heavily on **mutable state** tracked in the `RoslynEmitter` class:

### Variable Tracking
- **`_declaredVariables`**: Set of variable names declared in current scope
- **`_variableVersions`**: Maps base variable name → version number (for redeclaration tracking)
- **`_constVariables`**: Set of const variable names (original Sharpy names)
- **`_moduleVariables`**: Module-level variable names
- **`_moduleConstVariables`**: Module-level const variable names

### Module-Level Tracking
- **`_moduleFieldNames`**: Set of generated field names (detects redefinitions)
- **`_variablesWithExecutionOrderIssues`**: Variables that can't be simple fields due to execution order

### Type Context
- **`_targetTypeContext`**: Current target type for collection literal inference
- Set/restored around initializer generation to propagate type information

---

## Patterns and Design Decisions

### 1. Bottom-Up Construction
Methods build complex statements from innermost to outermost (e.g., `elif` chains, nested loops).

### 2. Temporary Variable Patterns
Used extensively to bridge semantic differences:
- Loop `else` clauses → Boolean flags
- Foreach modification → Temporary loop variables
- Try `else` → Success flags

### 3. Semantic Preservation
Python semantics are preserved through careful code generation:
- Floor division uses `Math.Floor()` with proper type conversions
- True division always casts to `double`
- Power operator uses `Math.Pow()`

### 4. Defensive Null Handling
Many methods return `StatementSyntax?` (nullable) to handle unsupported cases gracefully. The dispatcher filters nulls with `.OfType<StatementSyntax>()`.

### 5. Roslyn Fluent API
Heavy use of Roslyn's builder methods:
```csharp
IfStatement(condition, thenBlock, elseClause)
WhileStatement(condition, body)
TryStatement(tryBlock, catchClauses, finallyClause)
```

---

## Debugging Tips

### 1. Statement Not Generating?
- Check if the statement type is handled in `GenerateBodyStatement` switch
- Look for `null` returns (unsupported statement types are filtered out)
- Add breakpoint in the dispatcher to see what's being called

### 2. Wrong Variable Name?
- Check `_declaredVariables` and `_variableVersions` state
- Verify `GetMangledVariableName()` logic
- Module-level vs. local variable confusion?

### 3. Type Mismatch in Generated Code?
- Examine `_targetTypeContext` - is it being set correctly?
- Check `_typeMapper.MapType()` and `_typeMapper.InferTypeFromExpression()`
- Look at augmented assignment type coercion logic

### 4. Execution Order Issues?
- Check if variable is in `_variablesWithExecutionOrderIssues`
- These should be handled in module's `Main()`, not as fields
- See `RoslynEmitter.ModuleClass.cs` for Main() generation

### 5. Loop Else Not Working?
- Verify `TransformLoopBodyForElse()` is being called
- Check that `break` statements are being replaced with `BreakWithFlagStatement`
- Look at flag variable generation and naming

### 6. Inspecting Generated Roslyn Syntax
Add this debug helper:
```csharp
var syntax = GenerateBodyStatement(stmt);
Console.WriteLine(syntax?.ToFullString());  // See exact C# code
```

---

## Common Modification Scenarios

### Adding a New Statement Type

1. **Add case to `GenerateBodyStatement`**:
```csharp
MyNewStatement myStmt => GenerateMyStatement(myStmt),
```

2. **Implement generator method**:
```csharp
private StatementSyntax GenerateMyStatement(MyNewStatement stmt)
{
    // Use SyntaxFactory methods to build C# syntax
    return ...;
}
```

3. **Update tests** to verify generation

### Supporting New Operators

1. **Add to `GetAugmentedAssignmentOperator`** if simple binary op
2. **Add special case to `GenerateAugmentedValue`** if needs semantic translation
3. **Consider type coercion** (integer vs. float handling)

### Changing Name Mangling

1. **Modify `NameMangler` class** (separate file)
2. **Update variable tracking logic** in assignments
3. **Test module-level and local variables separately**

### Handling New Python Semantics

1. **Identify C# equivalent** (if exists)
2. **Design translation pattern** (may need temporary variables or helper methods)
3. **Document semantic differences** in code comments
4. **Add integration tests** with both Sharpy and expected C# output

---

## Cross-References

### Related RoslynEmitter Partial Classes
- [RoslynEmitter.cs](RoslynEmitter.md) - Main class definition, utilities, state management
- [RoslynEmitter.Expressions.md](RoslynEmitter.Expressions.md) - Expression generation (called by `GenerateExpression()`)
- [RoslynEmitter.Operators.md](RoslynEmitter.Operators.md) - Binary/unary operator handling
- [RoslynEmitter.ClassMembers.md](RoslynEmitter.ClassMembers.md) - Method/property generation
- [RoslynEmitter.ModuleClass.md](RoslynEmitter.ModuleClass.md) - Module-level code organization (Main() method)
- [RoslynEmitter.CompilationUnit.md](RoslynEmitter.CompilationUnit.md) - Top-level file structure

### Helper Classes
- [TypeMapper.md](TypeMapper.md) - Type conversion (Sharpy → C#)
- [NameMangler.md](NameMangler.md) - Naming convention conversion
- [CodeGenContext.md](CodeGenContext.md) - Shared code generation state

### Upstream Components
- AST Definitions: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`
- Semantic Analysis: `src/Sharpy.Compiler/Semantic/`

### Language Specification
- [.NET Interop Specification](../../../../language_specification/dotnet_interop.md) - How Sharpy maps to C#/.NET

---

## Contribution Guidelines

### Code Style
- Follow existing patterns for new statement types
- Use Roslyn's fluent API consistently
- Add XML doc comments for public/complex methods
- Keep methods focused on single statement type

### Testing Requirements
- Add unit tests for new statement types
- Include integration tests for Python semantic differences
- Test both simple and complex cases (nested loops, tuple unpacking, etc.)
- Verify name mangling and variable tracking

### Performance Considerations
- Roslyn syntax tree construction is already efficient
- Avoid unnecessary allocations in hot paths
- Consider caching generated temporary variable names

### Documentation
- Update this walkthrough when adding major features
- Document semantic translation patterns in code comments
- Note any Python/C# semantic differences clearly

### Review Checklist
- [ ] Does it preserve Python semantics correctly?
- [ ] Are variable names properly mangled?
- [ ] Does it handle edge cases (null values, empty collections)?
- [ ] Are temporary variables uniquely named?
- [ ] Does it integrate with existing state tracking?
- [ ] Is the generated C# code idiomatic?

---

## Summary

This file is the **heart of statement-level code generation** in Sharpy. It bridges the gap between Python's flexible statement semantics and C#'s more restrictive rules, using clever patterns like:

- Boolean flags for loop/try `else` clauses
- Temporary variables for mutable foreach iteration
- Type coercion for arithmetic operators
- Discard assignments for expression statements

Understanding this file is crucial for:
- Adding new statement types
- Debugging code generation issues
- Implementing Python semantic features
- Maintaining the compiler's correctness

The key insight: **Many Python features require multi-statement patterns in C#**, and this file contains the recipes for those translations.
