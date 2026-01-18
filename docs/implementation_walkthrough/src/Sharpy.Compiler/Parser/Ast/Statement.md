# Walkthrough: Statement.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

---

## 1. Overview

`Statement.cs` is a fundamental component of the Sharpy compiler's Abstract Syntax Tree (AST). It defines all the statement node types that can appear in a Sharpy program, from simple assignments and returns to complex control flow like `if`, `for`, and `try-except` blocks.

**Role in the Compiler Pipeline:**
```
Source Code (.spy) → Lexer (Tokens) → Parser (AST) → Semantic Analysis → Code Generation
                                           ↑
                                    Statement.cs lives here
```

This file is a **pure data model** - it contains no logic or behavior. Each statement type is represented as an immutable C# record that captures the syntactic structure of the source code. These AST nodes are then:
- Validated by the semantic analyzer
- Type-checked by the type checker
- Transformed into C# code by the code generator

---

## 2. Class/Type Structure

### Base Class Hierarchy

```csharp
Node (base for all AST nodes)
  ↓
Statement (base for all statements)
  ↓
  ├── Simple Statements (ExpressionStatement, Assignment, Return, etc.)
  ├── Compound Statements (IfStatement, WhileStatement, ForStatement, etc.)
  ├── Definitions (FunctionDef, ClassDef, StructDef, InterfaceDef, EnumDef)
  └── Import Statements (ImportStatement, FromImportStatement)
```

All statement classes inherit from `Statement`, which in turn inherits from `Node`. The `Node` base class (defined in `Node.cs`) provides source location tracking:

```csharp
public abstract record Node
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

### Categories of Statements

The file organizes statements into four logical regions:

1. **Simple Statements** - Single-line operations (assignment, return, break, etc.)
2. **Compound Statements** - Control flow with bodies (if, while, for, try-except)
3. **Definitions** - Type and function declarations
4. **Import Statements** - Module and name imports

---

## 3. Key Classes and Their Purpose

### 3.1 Simple Statements

#### `Assignment`
Represents assignment operations with various operators.

```csharp
public record Assignment : Statement
{
    public Expression Target { get; init; } = null!;
    public Expression Value { get; init; } = null!;
    public AssignmentOperator Operator { get; init; } = AssignmentOperator.Assign;
}
```

**Key Points:**
- `Target` can be any assignable expression (variable, property, index)
- Supports compound assignments (`+=`, `-=`, `*=`, etc.) via the `Operator` enum
- The `null!` syntax is a C# feature that suppresses null warnings - the parser ensures these are always set
- The `AssignmentOperator` enum includes 14 different operators, including Python-style operators like `//=` (integer division assign) and `**=` (power assign), plus C#-specific ones like `??=` (null coalesce assign)

**Example Sharpy Code:**
```python
x = 5           # Simple assignment
y += 10         # Compound assignment
items[0] = "a"  # Index assignment
z **= 2         # Power assignment
w ??= default   # Null coalesce assignment
```

#### `VariableDeclaration`
Represents typed variable declarations, distinct from assignments.

```csharp
public record VariableDeclaration : Statement
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public Expression? InitialValue { get; init; }
    public bool IsConst { get; init; }
}
```

**Why separate from Assignment?**
- Sharpy requires type annotations on first declaration: `x: int = 5`
- Plain assignment like `x = 5` requires prior declaration
- This distinction helps catch undeclared variable errors early
- **Note**: `Type` is nullable to support type inference when an initial value is provided

**Example Sharpy Code:**
```python
x: int = 10           # Declaration with initialization
y: str                # Declaration without initialization
const MAX: int = 100  # Constant declaration
z = 5                 # Type inferred from initial value
```

#### `ReturnStatement`
Exits from a function, optionally with a value.

```csharp
public record ReturnStatement : Statement
{
    public Expression? Value { get; init; }
}
```

**Note:** `Value` is nullable because Python-style functions can `return` without a value (equivalent to `return None`).

#### `RaiseStatement`
Throws exceptions, mirroring Python's `raise` syntax.

```csharp
public record RaiseStatement : Statement
{
    public Expression? Exception { get; init; }
    public Expression? Cause { get; init; }  // raise ... from cause
}
```

**Supports:**
- `raise` - re-raises current exception
- `raise Exception("message")` - raises new exception
- `raise NewError() from original_error` - exception chaining

#### `BreakWithFlagStatement`
A specialized break statement used internally for loop-else clause support.

```csharp
public record BreakWithFlagStatement : Statement
{
    public string FlagName { get; init; } = "";
}
```

**Important Implementation Detail:**
- This is an **internal/generated statement** type, not directly written by users
- Generated by the compiler to support Python-style `for...else` and `while...else` clauses
- Sets a boolean flag to false before breaking, allowing the else clause to detect whether the loop completed normally or was broken out of
- Example: A `for` loop with an `else` clause generates a flag variable, and any `break` statements are transformed to `BreakWithFlagStatement` nodes

### 3.2 Compound Statements

#### `IfStatement`
Represents if-elif-else control flow.

```csharp
public record IfStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public List<Statement> ThenBody { get; init; } = new();
    public List<ElifClause> ElifClauses { get; init; } = new();
    public List<Statement> ElseBody { get; init; } = new();
}
```

**Design Decision:**
- `elif` clauses are separate `ElifClause` records, not nested `IfStatement` nodes
- This makes code generation simpler and preserves the source structure
- Empty lists represent missing branches (no elif/else)

**Example Sharpy Code:**
```python
if x > 0:
    print("positive")
elif x < 0:
    print("negative")
else:
    print("zero")
```

#### `WhileStatement`
Represents while loops with optional else clause.

```csharp
public record WhileStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
    public List<Statement> ElseBody { get; init; } = new();
}
```

**Python-style Else Clause:**
- `ElseBody` executes if the loop completes **without** encountering a `break`
- If empty, no else clause was present in the source
- This is a Python feature that Sharpy supports

**Example Sharpy Code:**
```python
while x < 10:
    if found(x):
        break
    x += 1
else:
    print("Not found")  # Runs only if break was never executed
```

#### `ForStatement`
Represents for-in loops over iterables with optional else clause.

```csharp
public record ForStatement : Statement
{
    public Expression Target { get; init; } = null!;  // Loop variable(s)
    public Expression Iterator { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
    public List<Statement> ElseBody { get; init; } = new();
}
```

**Key Points:**
- `Target` can be a simple name (`x`) or tuple pattern (`x, y`) for destructuring
- `Iterator` is any expression that implements the iterable protocol
- `ElseBody` supports Python-style for-else (runs if loop completes without break)

**Example Sharpy Code:**
```python
for item in items:
    print(item)

for key, value in dict.items():
    print(f"{key}: {value}")

for x in range(10):
    if is_prime(x):
        break
else:
    print("No primes found")
```

#### `TryStatement`
Represents exception handling blocks.

```csharp
public record TryStatement : Statement
{
    public List<Statement> Body { get; init; } = new();
    public List<ExceptHandler> Handlers { get; init; } = new();
    public List<Statement> ElseBody { get; init; } = new();
    public List<Statement> FinallyBody { get; init; } = new();
}
```

**Design Notes:**
- Can have multiple `except` handlers (stored in `Handlers` list)
- `ElseBody` is a Python feature - runs if no exception was raised in the try block
- `FinallyBody` is optional (empty list means no finally clause)
- Each handler can specify an exception type and optional binding name

**Example Sharpy Code:**
```python
try:
    risky_operation()
except ValueError as e:
    handle_value_error(e)
except Exception:
    handle_any_error()
else:
    print("No errors!")  # Runs if no exception was raised
finally:
    cleanup()
```

### 3.3 Definitions

#### `FunctionDef`
Defines functions and methods.

```csharp
public record FunctionDef : Statement
{
    public string Name { get; init; } = "";
    public List<TypeParameterDef> TypeParameters { get; init; } = new();
    public List<Parameter> Parameters { get; init; } = new();
    public TypeAnnotation? ReturnType { get; init; }
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    public string? DocString { get; init; }
}
```

**Important Details:**
- `TypeParameters` support generic functions: `def swap[T](a: T, b: T) -> tuple[T, T]:`
- `ReturnType` is optional - if omitted, type inference will attempt to determine it
- `DocString` captures the first string literal in the body (Python convention)
- `Decorators` are applied before the function (e.g., `@staticmethod`)
- Parameters can have default values and type annotations

**Example Sharpy Code:**
```python
@decorator
def greet[T](name: str, greeting: str = "Hello") -> str:
    """Greet someone with a custom greeting."""
    return f"{greeting}, {name}!"
```

#### `ClassDef`
Defines reference types (classes).

```csharp
public record ClassDef : Statement
{
    public string Name { get; init; } = "";
    public List<TypeParameterDef> TypeParameters { get; init; } = new();
    public List<TypeAnnotation> BaseClasses { get; init; } = new();
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    public string? DocString { get; init; }
}
```

**Key Features:**
- `TypeParameters` support generics: `class Container[T]:`
- `BaseClasses` allows inheritance and interface implementation
- `Body` contains methods, fields, and nested type definitions

**Example Sharpy Code:**
```python
class Animal:
    """Base animal class."""

    def __init__(self, name: str):
        self.name: str = name

    def speak(self) -> str:
        pass
```

#### `StructDef`
Defines value types (structs).

```csharp
public record StructDef : Statement
{
    public string Name { get; init; } = "";
    public List<TypeParameterDef> TypeParameters { get; init; } = new();
    public List<TypeAnnotation> BaseClasses { get; init; } = new();  // Interfaces only
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    public string? DocString { get; init; }
}
```

**Struct vs Class:**
- Structs compile to C# value types (`struct` in C#)
- Can only implement interfaces, not inherit from classes (comment on line 217 clarifies this)
- Useful for small, immutable data types

#### `InterfaceDef`
Defines interface types.

```csharp
public record InterfaceDef : Statement
{
    public string Name { get; init; } = "";
    public List<TypeParameterDef> TypeParameters { get; init; } = new();
    public List<TypeAnnotation> BaseInterfaces { get; init; } = new();
    public List<Statement> Body { get; init; } = new();
    public string? DocString { get; init; }
}
```

**Note:** Unlike classes and structs, interfaces use `BaseInterfaces` instead of `BaseClasses`, making the intent clearer (interfaces can only extend other interfaces).

#### `EnumDef`
Defines enumerations.

```csharp
public record EnumDef : Statement
{
    public string Name { get; init; } = "";
    public List<EnumMember> Members { get; init; } = new();
    public string? DocString { get; init; }
}

public record EnumMember
{
    public string Name { get; init; } = "";
    public Expression? Value { get; init; }  // Optional explicit value
    // ... source location fields
}
```

**Version Note:** The comment on line 236 indicates this is for "simple enums only in v0.5" - more complex enum features may be added in future versions.

**Example Sharpy Code:**
```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3
```

#### `TypeAlias`
Declares type aliases for both simple types and function types.

```csharp
public record TypeAlias : Statement
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public FunctionType? FunctionType { get; init; }
}
```

**Important Constraint:**
- The comment on line 259 states: "Exactly one of Type or FunctionType must be set"
- This means you use `Type` for regular type aliases OR `FunctionType` for callable/function type aliases, but never both

**Example Sharpy Code:**
```python
type UserId = int
type Callback = (int, str) -> bool
```

#### Generic Type Constraints
Sharpy supports C#-style generic constraints through a set of constraint clause types:

```csharp
public record TypeParameterDef
{
    public string Name { get; init; } = "";
    public List<ConstraintClause> Constraints { get; init; } = new();
}

// Base type for all constraints
public abstract record ConstraintClause;

// Specific constraint types
public record TypeConstraint : ConstraintClause        // T: IComparable
public record ClassConstraint : ConstraintClause       // T: class
public record StructConstraint : ConstraintClause      // T: struct
public record NewConstraint : ConstraintClause         // T: new()
```

**Example Usage:**
```python
def sort[T: IComparable](items: list[T]) -> list[T]:
    # T must implement IComparable
    ...

class Factory[T: new()]:
    # T must have a parameterless constructor
    def create(self) -> T:
        return T()
```

#### `Decorator`
Represents decorators applied to functions, classes, or structs.

```csharp
public record Decorator
{
    public string Name { get; init; } = "";
    // Note: v0.3 only supports simple identifier decorators
    // No arguments or dotted names in v0.3

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**Version Limitation:** Currently only simple identifier decorators are supported (e.g., `@staticmethod`), not decorators with arguments (e.g., `@decorator(arg)`) or dotted names (e.g., `@module.decorator`).

#### `Parameter`
Represents function/method parameters with support for variadic parameters.

```csharp
public record Parameter
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public Expression? DefaultValue { get; init; }
    /// <summary>
    /// True if this parameter is variadic (*args). Maps to C# params T[].
    /// </summary>
    public bool IsVariadic { get; init; }

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**Variadic Parameters:**
- `IsVariadic` flag indicates Python-style `*args` parameters
- Maps to C# `params T[]` arrays
- Allows functions to accept variable numbers of arguments

### 3.4 Import Statements

#### `ImportStatement`
Standard module imports.

```csharp
public record ImportStatement : Statement
{
    public List<ImportAlias> Names { get; init; } = new();
}

public record ImportAlias
{
    public string Name { get; init; } = "";  // module.submodule
    public string? AsName { get; init; }  // Optional alias
    // ... source location fields
}
```

**Example Sharpy Code:**
```python
import sys
import os.path as ospath
import collections, itertools
```

#### `FromImportStatement`
From-import style imports with semantic analysis support.

```csharp
public record FromImportStatement : Statement
{
    public string Module { get; init; } = "";
    public List<ImportAlias> Names { get; init; } = new();
    public bool ImportAll { get; init; }  // from module import *

    /// <summary>
    /// The resolved module path relative to the project root, set during semantic analysis.
    /// For example, ".helpers" in package "mypackage" resolves to "mypackage.helpers".
    /// This is used during code generation to generate correct namespace references.
    /// </summary>
    public string? ResolvedModulePath { get; set; }

    /// <summary>
    /// Symbols that are re-exported from this from-import statement, set during semantic analysis.
    /// Maps the local name (possibly aliased) to the symbol information.
    /// This is used during code generation to generate delegating members in the Exports class.
    /// </summary>
    public Dictionary<string, Semantic.Symbol>? ReExportedSymbols { get; set; }
}
```

**Important Design Point:**
- This is one of the **few mutable AST nodes** - notice the `get; set;` on the last two properties
- `ResolvedModulePath` and `ReExportedSymbols` are populated during semantic analysis
- This mutation is acceptable because it happens in a single, well-defined phase
- These properties are essential for cross-module code generation

**Example Sharpy Code:**
```python
from typing import List, Dict
from collections import *
from .local_module import helper
```

---

## 4. Dependencies

### Direct Dependencies

**Within the Parser.Ast namespace:**
- `Node.cs` - Base class for all AST nodes
- `Expression.cs` - All expression types (used in `Test`, `Target`, `Value` properties)
- `Types.cs` - Type annotation nodes (used in `TypeAnnotation` properties)

**Key Relationship:**
```
Statement (this file)
    ↓ uses
Expression (Expression.cs) - for conditions, values, targets
    ↓ uses
TypeAnnotation (Types.cs) - for type declarations
```

**External Dependencies:**
- `Sharpy.Compiler.Semantic` namespace - for the `Symbol` type used in `FromImportStatement.ReExportedSymbols`

### Downstream Consumers

These parts of the compiler depend on Statement.cs:

1. **Parser (`Parser.cs`)** - Creates instances of these statement records
2. **Semantic Analyzer** - Validates statements, resolves names, checks types
3. **Code Generator (`RoslynEmitter.cs`)** - Transforms statements into C# syntax trees
4. **AST Visitors** - Any code that walks the AST tree (e.g., for analysis or transformation)

---

## 5. Patterns and Design Decisions

### 5.1 Immutable Records

All AST nodes use C# 9's `record` types with `init` properties:

```csharp
public record Assignment : Statement
{
    public Expression Target { get; init; } = null!;
    public Expression Value { get; init; } = null!;
}
```

**Why records?**
- **Immutability**: Once created, AST nodes cannot be modified (thread-safe, easier to reason about)
- **Value semantics**: Two nodes with identical properties are considered equal
- **Concise syntax**: Reduces boilerplate compared to traditional classes
- **Pattern matching**: Enables clean switch expressions and patterns

**Exception:** `FromImportStatement` has two mutable properties (`ResolvedModulePath` and `ReExportedSymbols`) that are populated during semantic analysis. This is a pragmatic choice to avoid creating new AST nodes after parsing.

### 5.2 Location Tracking

Every statement inherits source location from `Node`:

```csharp
public int LineStart { get; init; }
public int ColumnStart { get; init; }
public int LineEnd { get; init; }
public int ColumnEnd { get; init; }
```

**Used for:**
- Error messages: "Error at line 15, column 8: ..."
- IDE features: Jump to definition, hover info
- Debugging: Source mapping for stack traces

**Note:** Some helper records (like `ElifClause`, `ExceptHandler`, `Parameter`, `EnumMember`, `Decorator`, `ImportAlias`) include their own location fields since they don't inherit from `Node`.

### 5.3 Nullable vs Non-Nullable Properties

The file carefully distinguishes optional and required properties:

```csharp
// Required (will never be null after parsing)
public Expression Test { get; init; } = null!;

// Optional (may be absent in valid code)
public Expression? Value { get; init; }
```

**Pattern:**
- `null!` - Required property, parser ensures it's set
- `?` - Optional property, represents absence in source code

### 5.4 Empty Lists vs Null

For collections, empty lists represent absence, not null:

```csharp
public List<Statement> Body { get; init; } = new();
```

**Why not `List<Statement>?`?**
- Simplifies consumer code (no null checks needed)
- Makes intent clear: a list that may be empty
- Consistent with how the parser works

### 5.5 Helper Records

Several helper types (`ElifClause`, `ExceptHandler`, `Parameter`, etc.) don't inherit from `Statement`:

```csharp
public record ElifClause  // NOT : Statement
{
    public Expression Test { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
    // ... location fields
}
```

**Rationale:**
- These aren't standalone statements
- They're components of compound statements
- This prevents them from appearing where they shouldn't

### 5.6 Enum-Based Operators

The `AssignmentOperator` enum provides a type-safe way to represent the 14 different assignment operators:

```csharp
public enum AssignmentOperator
{
    Assign,        // =
    PlusAssign,    // +=
    MinusAssign,   // -=
    StarAssign,    // *=
    SlashAssign,   // /=
    DoubleSlashAssign,  // //=  (integer division)
    PercentAssign, // %=
    PowerAssign,   // **=  (exponentiation)
    AndAssign,     // &=
    OrAssign,      // |=
    XorAssign,     // ^=
    LeftShiftAssign,  // <<=
    RightShiftAssign,  // >>=
    NullCoalesceAssign // ??=
}
```

This approach is cleaner than storing operator strings and enables exhaustive pattern matching.

---

## 6. Debugging Tips

### 6.1 Inspecting AST Structures

When debugging parser issues, use the `AstDumper` utility:

```csharp
var module = parser.Parse();
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

This prints a tree representation showing all statement nodes.

### 6.2 Common Issues

**Problem:** `NullReferenceException` when accessing statement properties
- **Cause:** Parser bug that didn't initialize a required property
- **Fix:** Check parser code for the statement type, ensure all required fields are set

**Problem:** Missing source locations in error messages
- **Cause:** Forgot to set `LineStart`, `ColumnStart`, etc. when constructing the node
- **Fix:** Ensure parser's `CurrentToken` position is captured before advancing

**Problem:** Statements appearing in wrong contexts
- **Cause:** Parser accepts statements that should only be in specific contexts (e.g., `return` outside function)
- **Fix:** This is typically caught by semantic analysis, not in the AST itself

**Problem:** Loop-else clauses not working correctly
- **Cause:** `BreakWithFlagStatement` nodes not being generated properly
- **Fix:** Check the desugaring logic that transforms `BreakStatement` to `BreakWithFlagStatement` for loops with else clauses

### 6.3 Type System Integration

When debugging type-checking issues:

1. **Check `SemanticInfo`** - Types are NOT stored on AST nodes (except for the special mutable properties in `FromImportStatement`)
2. **Look at `TypeChecker.cs`** - Validates statement semantics
3. **Trace narrowing** - `if x is not None:` narrows types in the then-branch

```csharp
// In TypeChecker
var ifStmt = (IfStatement)stmt;
CheckExpression(ifStmt.Test);  // Check condition type

// Apply type narrowing in ThenBody
foreach (var thenStmt in ifStmt.ThenBody)
    CheckStatement(thenStmt);
```

### 6.4 Code Generation Debugging

When generated C# is wrong:

1. **Check `RoslynEmitter.cs`** - Find the `Visit*` method for your statement type
2. **Verify property access** - Ensure all statement properties are being read correctly
3. **Check name mangling** - Python `snake_case` → C# `PascalCase` conversion

Example for debugging assignment code generation:

```csharp
// In RoslynEmitter.cs
private StatementSyntax EmitAssignment(Assignment assignment)
{
    var left = EmitExpression(assignment.Target);   // Check this
    var right = EmitExpression(assignment.Value);   // And this
    return SyntaxFactory.AssignmentExpression(...);
}
```

### 6.5 Import Resolution Debugging

When debugging import issues:

1. **Check `ResolvedModulePath`** - Was the module path resolved correctly during semantic analysis?
2. **Inspect `ReExportedSymbols`** - Do the re-exported symbols have the correct mappings?
3. **Trace module discovery** - Use the `CachedModuleDiscovery` to understand how modules are found

---

## 7. Contribution Guidelines

### 7.1 Adding a New Statement Type

When adding a new statement to Sharpy:

1. **Define the record** in the appropriate region of this file:
   ```csharp
   /// <summary>
   /// With statement for context management
   /// </summary>
   public record WithStatement : Statement
   {
       public Expression ContextManager { get; init; } = null!;
       public string? TargetName { get; init; }
       public List<Statement> Body { get; init; } = new();
   }
   ```

2. **Update the parser** (`Parser.cs`) to recognize and construct the node

3. **Add semantic checks** (`TypeChecker.cs`, `NameResolver.cs`)

4. **Implement code generation** (`RoslynEmitter.cs`)

5. **Write tests**:
   - Lexer tests (if new keywords)
   - Parser tests (AST construction)
   - Semantic tests (type checking, scoping)
   - Integration tests (end-to-end)

### 7.2 Modifying Existing Statements

**DON'T:**
- Change the meaning of existing properties
- Remove properties (breaks backward compatibility)
- Make required properties optional
- Add mutable properties (except in very rare cases like `FromImportStatement`)

**DO:**
- Add new optional properties
- Add helper methods (but consider if they belong elsewhere)
- Improve documentation comments
- Add new constraint types or enum values

### 7.3 Code Style

Follow these conventions:

```csharp
/// <summary>
/// Clear, concise description of what this statement represents
/// </summary>
public record MyStatement : Statement
{
    // Required properties first (non-nullable)
    public Expression Target { get; init; } = null!;

    // Optional properties second (nullable)
    public Expression? OptionalValue { get; init; }

    // Collections last
    public List<Statement> Body { get; init; } = new();
}
```

### 7.4 Testing Requirements

Every statement type should have:

1. **Parser tests** - Verify AST structure is created correctly
2. **Semantic tests** - Test error detection (e.g., `break` outside loop)
3. **Code generation tests** - Verify C# output
4. **Integration tests** - Compile and run real Sharpy code

Example test structure:

```csharp
[Fact]
public void TestParseWithStatement()
{
    var source = """
        with open("file.txt") as f:
            print(f.read())
        """;

    var module = Parse(source);
    Assert.IsType<WithStatement>(module.Body[0]);
}
```

### 7.5 Documentation

Update these documents when modifying statements:

- **This walkthrough** (if structure changes significantly)
- **Language spec** (`docs/language_specification/`) for user-facing changes
- **Semantic analyzer architecture** if validation changes
- **Code samples** (`samples/`) to demonstrate new features

---

## 8. Cross-References

Understanding Statement.cs is easier with context from these related files:

### Related AST Files
- [Node.md](Node.md) - Base class documentation, source location tracking
- [Expression.md](Expression.md) - Expression nodes used within statements
- [Types.md](Types.md) - Type annotation nodes

### Compiler Pipeline Files
- **Parser.cs** - Creates these statement instances
- **Semantic Analyzer** - Validates statement semantics
- [RoslynEmitter.md](../../CodeGen/RoslynEmitter.md) - Converts statements to C#
- [TypeMapper.md](../../CodeGen/TypeMapper.md) - Maps Sharpy types to C# types

### Related Modules
For a better understanding of how statements flow through the compiler:
- [Compiler.md](../../Compiler.md) - Overall compilation pipeline
- [Lexer.md](../../Lexer/Lexer.md) - Tokenization before parsing

---

## 9. Quick Reference

### Statement Categories

```
Simple Statements:
  - ExpressionStatement    (expr as statement)
  - Assignment            (x = value, x += value, x **= value, etc.)
  - VariableDeclaration   (x: int = 5, const MAX = 100)
  - AssertStatement       (assert condition, message)
  - PassStatement         (pass)
  - BreakStatement        (break)
  - BreakWithFlagStatement (internal - for loop-else support)
  - ContinueStatement     (continue)
  - ReturnStatement       (return value)
  - RaiseStatement        (raise exception, raise ... from cause)

Compound Statements:
  - IfStatement           (if-elif-else)
  - WhileStatement        (while loop with optional else)
  - ForStatement          (for-in loop with optional else)
  - TryStatement          (try-except-else-finally)

Definitions:
  - FunctionDef           (def name[T](...): with generics)
  - ClassDef              (class Name[T]:)
  - StructDef             (struct Name[T]:)
  - InterfaceDef          (interface IName[T]:)
  - EnumDef               (enum Name:)
  - TypeAlias             (type UserId = int)

Imports:
  - ImportStatement       (import module, import module as alias)
  - FromImportStatement   (from module import name, from module import *)
```

### Common Patterns

**Checking statement type:**
```csharp
if (stmt is Assignment assignment)
{
    // Work with assignment
}
```

**Visiting all statements:**
```csharp
foreach (var stmt in functionDef.Body)
{
    ProcessStatement(stmt);
}
```

**Creating a statement (in parser):**
```csharp
return new ReturnStatement
{
    Value = valueExpr,
    LineStart = startToken.Line,
    ColumnStart = startToken.Column,
    LineEnd = currentToken.Line,
    ColumnEnd = currentToken.Column
};
```

**Handling loop-else:**
```csharp
// In semantic analyzer or code gen
if (forStmt.ElseBody.Count > 0)
{
    // Transform break statements to BreakWithFlagStatement
    var flagName = GenerateUniqueFlagName();
    // ... transformation logic
}
```

---

## 10. FAQ

**Q: Why are statements immutable?**
A: Immutability prevents bugs from unexpected modifications, enables safe concurrent processing, and supports functional-style transformations. The exception is `FromImportStatement`, which has mutable properties set during semantic analysis.

**Q: Where are statement types stored?**
A: Types are resolved by the semantic analyzer and stored in `SemanticInfo`, not on the AST nodes themselves (except for import resolution data on `FromImportStatement`).

**Q: Can I add methods to statement records?**
A: Technically yes, but avoid it. AST nodes should be pure data. Put behavior in visitors, analyzers, or emitters.

**Q: Why not use inheritance for similar statements?**
A: Each statement type has unique properties. Flattening the hierarchy keeps the design simple and explicit.

**Q: How do I add a new statement type?**
A: Follow the pattern: define the record here → update parser → add semantic checks → implement code generation → write tests.

**Q: Where's the logic for executing statements?**
A: Sharpy is a compiled language. Statements are transformed to C# by `RoslynEmitter`, then .NET executes the resulting IL.

**Q: What's the difference between `BreakStatement` and `BreakWithFlagStatement`?**
A: `BreakStatement` is the standard break. `BreakWithFlagStatement` is an internal/generated statement used to implement Python-style loop-else clauses, setting a flag before breaking so the else clause can detect whether the loop completed normally.

**Q: Why does `FromImportStatement` have mutable properties?**
A: `ResolvedModulePath` and `ReExportedSymbols` are set during semantic analysis to avoid creating new AST nodes. This is a pragmatic trade-off between immutability and practicality.

**Q: Can type parameters have multiple constraints?**
A: Yes! The `TypeParameterDef.Constraints` property is a list, allowing multiple constraints like `T: class, IComparable, new()`.

---

## Summary

`Statement.cs` is the data model for all Sharpy statements. It's a pure, immutable representation of program structure that flows through the compiler pipeline. As a newcomer:

1. **Start here** when understanding how Sharpy syntax maps to AST structure
2. **Reference this** when writing parsers, analyzers, or code generators
3. **Extend this** carefully, following the established patterns
4. **Remember**: This is just data - the interesting logic happens in the components that consume these nodes

**Key Takeaways:**
- 30+ statement types organized into 4 categories
- Immutable records with `init` properties (except for import resolution)
- Source location tracking on all nodes
- Support for Python features like loop-else, exception chaining, and generic constraints
- Clean separation between AST (structure) and semantic info (types, symbols)

For deeper understanding, explore the related files (Parser.cs, TypeChecker.cs, RoslynEmitter.cs) to see how these statement nodes are created, validated, and transformed.
