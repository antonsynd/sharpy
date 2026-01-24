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
- **Code Generation Context**: `_context` (CodeGenContext) provides symbol lookup and semantic information
- **Semantic Info**: Type information from semantic analysis phase via `SemanticInfo` and `CodeGenInfo`

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
- **Module-level variables**: Checks via `CodeGenInfo.IsModuleLevel` and assigns without redeclaring

**Key Decision Logic** (lines 119-157):
```csharp
var symbol = _context.LookupSymbol(name.Name);
var existsAsModuleLevel = symbol?.CodeGenInfo?.IsModuleLevel == true;
var existsAsLocal = _variableVersions.ContainsKey(baseName);

if (existsAsModuleLevel || existsAsLocal)
{
    // Variable exists - just update it
    var currentName = GetMangledVariableName(name.Name, isNewDeclaration: false);
    return AssignmentStatement(currentName, value);
} else {
    // First declaration of this variable in this scope
    var varName = GetMangledVariableName(name.Name, isNewDeclaration: true);
    _declaredVariables.Add(varName);
    return LocalDeclarationStatement("var", varName, value);
}
```

**CodeGenInfo Integration**: The compiler now uses `CodeGenInfo` (computed during semantic analysis) to determine if a symbol is module-level, eliminating the need for separate tracking sets during code generation.

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
1. Pre-analysis (during semantic phase) identifies variables with execution issues via `CodeGenInfo.HasExecutionOrderIssues`
2. Such variables are **skipped** here (return `null`) - see lines 431-437
3. They're handled as **executable statements** in the module's `Main()` method instead

**Execution Order Check** (lines 431-437):
```csharp
var symbol = _context.LookupSymbol(varDecl.Name);
if (symbol != null && HasExecutionOrderIssues(symbol))
{
    return null;  // Will be handled in Main()
}
```

The `HasExecutionOrderIssues()` helper (defined in RoslynEmitter.cs:255-258) checks the `CodeGenInfo` computed during semantic analysis.

**Naming Convention** (lines 448-460):
```csharp
// Explicitly const OR ALL_CAPS name → CONSTANT_CASE
const MAX_SIZE = 100  → public const int MAX_SIZE = 100;
DEFAULT_TIMEOUT = 30  → public const int DEFAULT_TIMEOUT = 30;  // Inferred const style

// Other names → PascalCase
version = "1.0"       → public static string Version = "1.0";
```

The `IsConstantCaseName()` helper (defined in RoslynEmitter.CompilationUnit.cs:337-340) checks if a name is ALL_CAPS to support Python-style constant conventions.

**Field Modifiers** (lines 530-552):
- **Compile-time const**: `public const` - Only for compile-time literals (int, float, string, bool, None)
- **Runtime const**: `public static readonly` - For const values that aren't compile-time literals
- **Regular variables**: `public static` - Mutable module-level variables

```csharp
if (varDecl.IsConst && IsCompileTimeLiteral(varDecl.InitialValue))
{
    modifiers = TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.ConstKeyword));
}
else if (varDecl.IsConst)
{
    modifiers = TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword));
}
else
{
    modifiers = TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword));
}
```

**Redefinition Detection** (lines 462-473):
- Tracks generated field names in `_moduleFieldNames`
- Returns `null` for redefinitions (handled as executable statements in Main())

```csharp
if (_moduleFieldNames.Contains(varName))
{
    // This is a redefinition - handle as executable statement in Main
    return null;
}
_moduleFieldNames.Add(varName);
```

**Note on Execution Order**: If semantic analysis detected execution order issues via `CodeGenInfo.HasExecutionOrderIssues`, the field generation is skipped entirely and the variable becomes a local in Main().

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

**C# Implementation Pattern** (lines 655-691):
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

**How break is transformed**: The `TransformLoopBodyForElse()` helper (defined in RoslynEmitter.Operators.cs:333) walks the AST and replaces `BreakStatement` nodes with `BreakWithFlagStatement` nodes that set the flag before breaking.

**Temp Variable Generation** (line 670): Uses `GenerateTempVarName("loopCompleted")` to create unique flag names like `__loopCompleted_0`, `__loopCompleted_1`, etc., preventing collisions in nested loops.

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

**Solution** - Always use a temporary variable pattern (lines 740-790):
```csharp
foreach (var __loopVar_0 in items) {
    var i = __loopVar_0;  // Mutable copy
    // ... body can now modify 'i'
}
```

**Variable Scope Handling** (lines 746-780):
The code checks if the loop variable already exists in an outer scope:
- **Variable exists in outer scope**: Generates an assignment (`i = __loopVar_0;`)
- **New variable**: Generates a declaration (`var i = __loopVar_0;`)

This subtle distinction matters for closures and variable shadowing semantics.

**Tuple Unpacking Support** (lines 794-833):
```sharpy
for x, y in pairs:  # Sharpy
```
↓
```csharp
foreach (var (x, y) in pairs) {  // C# using tuple deconstruction
```

**Implementation**: Uses `ForEachVariableStatement` with `DeclarationExpression` containing a `ParenthesizedVariableDesignation`. This leverages C# 7.0+ tuple deconstruction syntax.

**Else Clause Handling**: Same boolean flag pattern as `while` loops.

**Variable Tracking** (lines 746-809):
- Pre-registers loop variables in `_declaredVariables` **before** generating body
- Also registers in `_variableVersions` with version 0
- This ensures assignments to loop variable are treated as updates, not new declarations
- Critical for nested scopes and variable shadowing correctness

```csharp
// Register the loop variable BEFORE generating the body
if (!varExistsInOuterScope)
{
    _declaredVariables.Add(loopVar);
}
_variableVersions[loopVar] = 0;

// Now generate the body - assignments to loopVar will be updates
var body = Block(bodyStatements.Select(GenerateBodyStatement).OfType<StatementSyntax>());
```

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

**C# Implementation** (lines 897-961):
```csharp
bool __trySucceeded_0 = false;
try {
    // ... original body ...
    __trySucceeded_0 = true;  // Set flag at end of try block
}
catch (Exception ex) {
    // ... handler ...
    // Flag remains false
}
finally {
    // ... finally block ...
}
if (__trySucceeded_0) {
    // else block only runs if no exception was thrown
}
```

**Key Insight**: The flag is set at the **end** of the try block (line 909-913). If an exception is thrown, the flag never gets set, and the else block is skipped.

**Catch Clause Variants** (lines 858-884):
- **Named exception**: `except ValueError as e:` → `catch (ValueError e)`
- **Anonymous**: `except ValueError:` → `catch (ValueError)`
- **Catch-all**: `except:` → `catch` (no type specified)

**Type Mapping**: Exception types are mapped via `_typeMapper.MapType()` to their C# equivalents (e.g., `ValueError` → C# exception class).

**Implementation Details**:
- Uses `CatchDeclaration()` for typed catches with optional identifier
- Empty `CatchClause()` for catch-all handlers
- Catch clauses are collected in a list and passed to `TryStatement()`

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

### IsValidCSharpStatementExpression (lines 89-102)
Determines if an expression can stand alone as a C# statement. Currently only method calls (`FunctionCall`) are valid; all others need the discard wrapper (`_ = expr;`).

**Future Expansion**: Could be extended to support other valid statement expressions like increment/decrement, await expressions, or assignments.

### GetAugmentedAssignmentOperator (lines 257-277)
Maps Sharpy's `AssignmentOperator` enum to Roslyn's `SyntaxKind` for binary operations. Returns `SyntaxKind.None` for operators needing special handling (`/=`, `//=`, `**=`).

### GenerateAugmentedValue (lines 289-325)
The semantic translation hub for augmented assignments. Dispatches to specialized handlers based on operator type.

### GenerateTrueDivisionAugmented (lines 331-346)
Implements Python's true division semantics (`/`). Casts integers to `double` before division to ensure floating-point results.

**Type Detection**: Uses the optional `targetAst` and `valueAst` parameters with `IsFloatExpression()` to determine if type coercion is needed.

### IsCompileTimeLiteral (lines 558-570)
Checks if an expression is a compile-time constant that can be used with C# `const` keyword. Only basic literals qualify:
- `IntegerLiteral`
- `FloatLiteral`
- `StringLiteral`
- `BooleanLiteral`
- `NoneLiteral` (maps to `null`)

**Note**: Complex expressions like `1 + 2` are NOT compile-time literals, even though mathematically constant.

### GenerateBreakWithFlag (lines 352-361)
Generates the flag-setting pattern for loop `else` clauses:
```csharp
{
    flagName = false;
    break;
}
```

Uses a block statement to group the flag assignment and break together.

### GenerateTempVarName
Defined in RoslynEmitter.Operators.cs:324-327. Generates unique temporary variable names with a counter:
```csharp
private string GenerateTempVarName(string prefix)
{
    return $"__{prefix}_{_tempVarCounter++}";
}
```

Creates names like `__loopCompleted_0`, `__trySucceeded_1`, etc.

### TransformLoopBodyForElse
Defined in RoslynEmitter.Operators.cs:333-370. Recursively walks statement AST and replaces `BreakStatement` with `BreakWithFlagStatement`. Does NOT transform nested loops (their breaks apply to their own scope).

### HasExecutionOrderIssues
Defined in RoslynEmitter.cs:255-258. Checks if a symbol has execution order issues using `CodeGenInfo`:
```csharp
private bool HasExecutionOrderIssues(Symbol symbol)
{
    return symbol.CodeGenInfo?.HasExecutionOrderIssues == true;
}
```

### IsConstantCaseName
Defined in RoslynEmitter.CompilationUnit.cs:337-340. Checks if a name is in ALL_CAPS format, supporting Python-style constant naming conventions.

---

## State Management & Context Tracking

This file relies heavily on **mutable state** tracked in the `RoslynEmitter` class (defined in RoslynEmitter.cs):

### Variable Tracking (Local Scope)
- **`_declaredVariables`**: `HashSet<string>` - Variable names declared in current scope
- **`_variableVersions`**: `Dictionary<string, int>` - Maps base variable name → version number (for redeclaration tracking with versioned names like `x_1`, `x_2`)
- **`_constVariables`**: `HashSet<string>` - Const variable names (original Sharpy names) within local scopes
- **`_moduleFieldNames`**: `HashSet<string>` - C# field names generated at module level (detects redefinitions)

### Context and Semantic Info
- **`_context`**: `CodeGenContext` - Provides symbol lookup via `LookupSymbol()`, accesses semantic information computed during analysis
- **`_typeMapper`**: `TypeMapper` - Converts Sharpy types to C# types
- **`NameMangler`**: Static utility for name conversions (snake_case → camelCase/PascalCase/CONSTANT_CASE)

### Type Context for Inference
- **`_targetTypeContext`**: `TypeAnnotation?` - Current target type for collection literal inference
- Set/restored around initializer generation (lines 400-411) to propagate type information
- Example: `list[int] nums = [1, 2, 3]` → Literal knows element type is `int`

### Code Generation Metadata
- **`_interfaceDefinitions`**: `Dictionary<string, InterfaceDef>` - Tracks interface definitions for abstract class stub generation
- **`_tempVarCounter`**: `int` - Counter for generating unique temporary variable names
- **`_isInAbstractClass`**: `bool` - Flag indicating if currently generating methods for an abstract class

### Semantic Analysis Results (via CodeGenInfo)
The `CodeGenInfo` class (computed during semantic analysis) provides:
- **`IsModuleLevel`**: Is this symbol at module scope?
- **`HasExecutionOrderIssues`**: Does this variable need special handling due to use-before-declaration?
- **`CSharpName`**: The mangled C# name for this symbol
- **`IsStringEnum`**: Is this type a string enum?

This eliminates the need for many tracking sets that were previously maintained during code generation.

---

## Important Implementation Details for Newcomers

### The Two-Phase Variable Resolution Strategy

Understanding variable resolution is **critical** to working with this code. The compiler uses a hybrid approach:

**Phase 1 - Semantic Analysis (Pre-CodeGen)**:
- Computes `CodeGenInfo` for **module-level symbols** (variables, functions, classes, imports)
- Identifies execution order issues, redefinitions, name conflicts
- Stores metadata like `IsModuleLevel`, `HasExecutionOrderIssues`, `CSharpName`

**Phase 2 - Code Generation (Runtime)**:
- Uses `CodeGenInfo` for module-level lookups
- Tracks **local variables** dynamically in `_declaredVariables` and `_variableVersions`
- Why? Local variable redeclarations happen during emission, not during semantic analysis

**Key Methods**:
```csharp
// Try to use CodeGenInfo first (for module-level symbols)
var csharpName = TryGetCSharpNameFromCodeGenInfo(sharpyName, isNewDeclaration);

// Fall back to local variable tracking
if (csharpName == null)
{
    csharpName = GetMangledVariableName(sharpyName, isNewDeclaration);
}
```

See `RoslynEmitter.cs` lines 92-120 for the full implementation.

### Why Some Variables Become Locals in Main()

Sharpy allows variables to be used before declaration or redeclared multiple times:
```sharpy
print(x)  # Use before declaration
x = 10    # First declaration
x = "hello"  # Redeclaration with different type
```

**Problem**: C# static fields can't be initialized in declaration order with such dependencies.

**Solution**:
1. Semantic analysis detects these patterns and sets `CodeGenInfo.HasExecutionOrderIssues = true`
2. `GenerateModuleLevelField()` returns `null` for these variables (lines 431-437)
3. `RoslynEmitter.ModuleClass.cs` collects these statements and puts them in `Main()`
4. In Main(), they become local variables with proper initialization order

This is why you see both field declarations and local variables in generated code.

### The Mutable Foreach Loop Variable Pattern

This is a **subtle but important** pattern (lines 740-790):

**Problem**: Python allows modifying loop variables:
```python
for i in range(10):
    i = i * 2  # Modify the loop variable
    print(i)
```

**C# Constraint**: Foreach iteration variables are read-only.

**Solution**: Always introduce a mutable copy:
```csharp
foreach (var __loopVar_0 in range)
{
    var i = __loopVar_0;  // Mutable copy
    i = i * 2;  // Now modification works
    print(i);
}
```

**Scope Handling**: The code checks if the variable already exists in an outer scope:
- If yes → Assignment: `i = __loopVar_0;`
- If no → Declaration: `var i = __loopVar_0;`

This preserves variable shadowing semantics correctly.

### Flag-Based Control Flow Translation

Three Python constructs require boolean flags in C#:

**1. Loop Else** (lines 655-691, 694-726):
```csharp
bool __loopCompleted_0 = true;
while (condition) {
    if (should_break) {
        __loopCompleted_0 = false;
        break;
    }
}
if (__loopCompleted_0) { /* else clause */ }
```

**2. Try Else** (lines 897-961):
```csharp
bool __trySucceeded_0 = false;
try {
    // ... code ...
    __trySucceeded_0 = true;  // At the END
} catch { }
if (__trySucceeded_0) { /* else clause */ }
```

**Key Difference**: Loop flag starts `true`, try flag starts `false`.

**3. Break Transformation**:
The `TransformLoopBodyForElse()` function (in RoslynEmitter.Operators.cs) walks the AST and replaces:
```
BreakStatement → BreakWithFlagStatement { FlagName = "__loopCompleted_0" }
```

Which generates:
```csharp
{
    __loopCompleted_0 = false;
    break;
}
```

### Target Type Context for Collection Literals

Lines 397-411 and 500-516 show the **target type context pattern**:

```csharp
var previousTargetType = _targetTypeContext;
_targetTypeContext = varDecl.Type;  // Set context
try
{
    var value = GenerateExpression(varDecl.InitialValue);
    // ... use value ...
}
finally
{
    _targetTypeContext = previousTargetType;  // Restore
}
```

**Why?** Collection literals like `[1, 2, 3]` need to know their element type:
```sharpy
nums: list[int] = [1, 2, 3]  # Element type is int
```

The expression generator checks `_targetTypeContext` to infer `int` and generate:
```csharp
new List<int> { 1, 2, 3 }
```

Without the context, it would have to use type inference or default to `object`.

### Understanding Variable Versions

When a variable is redeclared with a new type, Sharpy generates versioned names:

```sharpy
x = 42        # → int x = 42;
x = "hello"   # → string x_1 = "hello";  (version incremented)
print(x)      # → print(x_1);  (references latest version)
```

**Tracking**:
- `_variableVersions["x"] = 0` after first declaration
- `_variableVersions["x"] = 1` after second declaration
- `GetMangledVariableName("x", isNewDeclaration: false)` returns `"x_1"`

This allows Python's dynamic typing semantics while maintaining C# type safety.

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
Many methods return `StatementSyntax?` (nullable) to handle unsupported cases gracefully. The dispatcher filters nulls with `.OfType<StatementSyntax>()` when collecting statements.

**Example**: `GenerateModuleLevelField()` returns `null` for execution-order-problematic variables, allowing them to be handled differently in the Main() method.

### 5. Roslyn Fluent API
Heavy use of Roslyn's builder methods for readable syntax tree construction:
```csharp
IfStatement(condition, thenBlock, elseClause)
WhileStatement(condition, body)
TryStatement(tryBlock, catchClauses, finallyClause)
LocalDeclarationStatement(declaration).WithModifiers(modifiers)
```

The `SyntaxFactory` is imported with `using static`, allowing direct calls without qualification.

### 6. Immutable AST Pattern
The input AST nodes are immutable records. Semantic information is stored separately in `CodeGenInfo` (computed during semantic analysis), not in the AST nodes themselves. This separation of concerns keeps code generation focused on translation, not analysis.

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
- Check if variable is in `CodeGenInfo.HasExecutionOrderIssues` (via `_context.LookupSymbol()`)
- These should be handled in module's `Main()`, not as fields
- See `RoslynEmitter.ModuleClass.cs` for Main() generation
- Verify the semantic analysis phase is correctly identifying these cases

### 5. Loop Else Not Working?
- Verify `TransformLoopBodyForElse()` is being called (defined in RoslynEmitter.Operators.cs)
- Check that `break` statements are being replaced with `BreakWithFlagStatement` in the AST
- Look at flag variable generation and naming - use debugger to inspect `_tempVarCounter`
- Ensure the flag is initialized to `true` before the loop

### 6. Inspecting Generated Roslyn Syntax
Add this debug helper in your test code:
```csharp
var syntax = GenerateBodyStatement(stmt);
Console.WriteLine(syntax?.ToFullString());  // See exact C# code
Console.WriteLine(syntax?.GetType().Name);  // See Roslyn node type
```

### 7. Variable Version Tracking Issues?
- Check `_variableVersions` dictionary state
- Verify `GetMangledVariableName()` is being called with correct `isNewDeclaration` flag
- Look at the order of statements - are variables being registered before use?
- Use breakpoints in `GenerateAssignment()` to trace variable name resolution

### 8. CodeGenInfo Not Available?
- Ensure semantic analysis completed successfully
- Check that `CodeGenInfoComputer.Compute()` ran during semantic phase
- Verify symbol lookup: `var symbol = _context.LookupSymbol(name)`
- Look at `symbol?.CodeGenInfo` - is it null or populated?

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
- [RoslynEmitter.cs](RoslynEmitter.md) - Main class definition, utilities, state management, `GetMangledVariableName()`
- [RoslynEmitter.Expressions.md](RoslynEmitter.Expressions.md) - Expression generation (called by `GenerateExpression()`)
- [RoslynEmitter.Operators.md](RoslynEmitter.Operators.md) - Binary/unary operators, `TransformLoopBodyForElse()`, `GenerateTempVarName()`
- [RoslynEmitter.ClassMembers.md](RoslynEmitter.ClassMembers.md) - Method/property generation
- [RoslynEmitter.ModuleClass.md](RoslynEmitter.ModuleClass.md) - Module-level code organization (Main() method, executable statements)
- [RoslynEmitter.CompilationUnit.md](RoslynEmitter.CompilationUnit.md) - Top-level file structure, namespace generation, `IsConstantCaseName()`
- [RoslynEmitter.TypeDeclarations.md](RoslynEmitter.TypeDeclarations.md) - Class, interface, enum definitions

### Helper Classes
- [TypeMapper.md](TypeMapper.md) - Type conversion (Sharpy → C#), `MapType()`, `InferTypeFromExpression()`
- [NameMangler.md](NameMangler.md) - Naming convention conversion (ToCamelCase, ToPascalCase, ToConstantCase)
- [CodeGenContext.md](CodeGenContext.md) - Shared code generation state, `LookupSymbol()`, project/namespace info
- [CodeGenInfo.md](../Semantic/CodeGenInfo.md) - Per-symbol code generation metadata computed during semantic analysis

### Upstream Components
- **AST Definitions**: [Statement.cs](../../Parser/Ast/Statement.md) - All statement types
- **Semantic Analysis**: [TypeChecker.md](../Semantic/TypeChecker.md) - Provides typed AST
- **Code Gen Info Computation**: [CodeGenInfoComputer.md](../Semantic/CodeGenInfoComputer.md) - Computes CodeGenInfo during semantic phase

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

## Real-World Transformation Examples

To help newcomers understand how Sharpy code becomes C#, here are complete examples showing the full transformation:

### Example 1: Simple Variable Declaration and Assignment

**Sharpy Input**:
```sharpy
x: int = 10
x += 5
print(x)
```

**Generated C#**:
```csharp
int x = 10;
x = x + 5;
print(x);
```

**What happens**:
1. `GenerateVariableDeclaration()` → `int x = 10;`
2. `GenerateAssignment()` detects `x` exists → Updates without redeclaring
3. `GenerateAugmentedValue()` → Converts `+=` to `x + 5`

---

### Example 2: Variable Redeclaration with Type Change

**Sharpy Input**:
```sharpy
value = 42
value = "hello"
print(value)
```

**Generated C#**:
```csharp
var value = 42;
string value_1 = "hello";
print(value_1);
```

**What happens**:
1. First assignment: New declaration → `var value = 42;`
2. Second assignment: Different type detected → New versioned variable `value_1`
3. References use latest version → `print(value_1);`

---

### Example 3: While Loop with Else

**Sharpy Input**:
```sharpy
i = 0
while i < 5:
    if i == 3:
        break
    i += 1
else:
    print("completed")
```

**Generated C#**:
```csharp
int i = 0;
bool __loopCompleted_0 = true;
while (i < 5)
{
    if (i == 3)
    {
        __loopCompleted_0 = false;
        break;
    }
    i = i + 1;
}
if (__loopCompleted_0)
{
    print("completed");
}
```

**What happens**:
1. `GenerateWhile()` detects `else` clause
2. Creates flag: `bool __loopCompleted_0 = true;`
3. `TransformLoopBodyForElse()` replaces `break` → `{ __loopCompleted_0 = false; break; }`
4. Adds conditional: `if (__loopCompleted_0) { /* else block */ }`

---

### Example 4: For Loop with Mutable Variable

**Sharpy Input**:
```sharpy
for i in range(5):
    i *= 2
    print(i)
```

**Generated C#**:
```csharp
foreach (var __loopVar_0 in range(5))
{
    var i = __loopVar_0;
    i = i * 2;
    print(i);
}
```

**What happens**:
1. `GenerateFor()` creates temp variable: `__loopVar_0`
2. Declares mutable copy: `var i = __loopVar_0;`
3. Now `i` can be modified inside the loop

---

### Example 5: Try/Except with Else

**Sharpy Input**:
```sharpy
try:
    result = risky_operation()
except ValueError as e:
    print(f"Error: {e}")
else:
    print("Success!")
finally:
    cleanup()
```

**Generated C#**:
```csharp
bool __trySucceeded_0 = false;
try
{
    var result = risky_operation();
    __trySucceeded_0 = true;
}
catch (ValueError e)
{
    print($"Error: {e}");
}
finally
{
    cleanup();
}
if (__trySucceeded_0)
{
    print("Success!");
}
```

**What happens**:
1. `GenerateTryWithElse()` creates flag: `bool __trySucceeded_0 = false;`
2. Appends flag assignment at end of try block: `__trySucceeded_0 = true;`
3. Catch/finally blocks are generated normally
4. Else block wrapped in: `if (__trySucceeded_0) { }`

---

### Example 6: Floor Division Semantics

**Sharpy Input**:
```sharpy
x: int = 10
x //= 3
```

**Generated C#**:
```csharp
int x = 10;
x = (long)Math.Floor((double)x / 3);
```

**What happens**:
1. `GenerateAssignment()` detects `//=` operator
2. `GenerateAugmentedValue()` dispatches to floor division handler
3. `GenerateFloorDivision()` creates:
   - Cast to `double` for division
   - `Math.Floor()` for Python floor semantics
   - Cast back to `long` for integer result

---

### Example 7: Tuple Unpacking Assignment

**Sharpy Input**:
```sharpy
x, y = 1, 2
print(x, y)
```

**Generated C#**:
```csharp
var (x, y) = (1, 2);
print(x, y);
```

**What happens**:
1. `GenerateAssignment()` detects `TupleLiteral` as target
2. Creates `ParenthesizedVariableDesignation` for `(x, y)`
3. Uses C# 7.0+ tuple deconstruction syntax
4. Marks both variables as declared

---

### Example 8: Module-Level Field with Execution Order Issue

**Sharpy Input**:
```sharpy
print(CONFIG)  # Used before declaration
CONFIG = "production"
```

**Generated C# (simplified)**:
```csharp
public partial class Module
{
    // No field for CONFIG - has execution order issues
    
    static void Main()
    {
        print(CONFIG);  // ERROR: Use before declaration
        var CONFIG = "production";
        print(CONFIG);  // Now OK
    }
}
```

**What happens**:
1. Semantic analysis detects use-before-declaration
2. Sets `CodeGenInfo.HasExecutionOrderIssues = true`
3. `GenerateModuleLevelField()` returns `null` (line 434-436)
4. Variable becomes local in `Main()` to preserve execution order

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
