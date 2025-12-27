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

**Example Sharpy Code:**
```python
x = 5           # Simple assignment
y += 10         # Compound assignment
items[0] = "a"  # Index assignment
```

#### `VariableDeclaration`
Represents typed variable declarations, distinct from assignments.

```csharp
public record VariableDeclaration : Statement
{
    public string Name { get; init; } = "";
    public TypeAnnotation Type { get; init; } = null!;
    public Expression? InitialValue { get; init; }
    public bool IsConst { get; init; }
}
```

**Why separate from Assignment?**
- Sharpy requires type annotations on first declaration: `x: int = 5`
- Plain assignment like `x = 5` requires prior declaration
- This distinction helps catch undeclared variable errors early

**Example Sharpy Code:**
```python
x: int = 10           # Declaration with initialization
y: str                # Declaration without initialization
const MAX: int = 100  # Constant declaration
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

#### `ForStatement`
Represents for-in loops over iterables.

```csharp
public record ForStatement : Statement
{
    public Expression Target { get; init; } = null!;  // Loop variable(s)
    public Expression Iterator { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
}
```

**Key Points:**
- `Target` can be a simple name (`x`) or tuple pattern (`x, y`) for destructuring
- `Iterator` is any expression that implements the iterable protocol
- No Python-style `else` clause on loops (not in Sharpy v0.5)

**Example Sharpy Code:**
```python
for item in items:
    print(item)

for key, value in dict.items():
    print(f"{key}: {value}")
```

#### `TryStatement`
Represents exception handling blocks.

```csharp
public record TryStatement : Statement
{
    public List<Statement> Body { get; init; } = new();
    public List<ExceptHandler> Handlers { get; init; } = new();
    public List<Statement> FinallyBody { get; init; } = new();
}
```

**Design Notes:**
- Can have multiple `except` handlers (stored in `Handlers` list)
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
    public List<Parameter> Parameters { get; init; } = new();
    public TypeAnnotation? ReturnType { get; init; }
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    public string? DocString { get; init; }
}
```

**Important Details:**
- `ReturnType` is optional - if omitted, type inference will attempt to determine it
- `DocString` captures the first string literal in the body (Python convention)
- `Decorators` are applied before the function (e.g., `@staticmethod`)
- Parameters can have default values and type annotations

**Example Sharpy Code:**
```python
@decorator
def greet(name: str, greeting: str = "Hello") -> str:
    """Greet someone with a custom greeting."""
    return f"{greeting}, {name}!"
```

#### `ClassDef`
Defines reference types (classes).

```csharp
public record ClassDef : Statement
{
    public string Name { get; init; } = "";
    public List<string> TypeParameters { get; init; } = new();
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
    public List<string> TypeParameters { get; init; } = new();
    public List<TypeAnnotation> BaseClasses { get; init; } = new();  // Interfaces only
    public List<Statement> Body { get; init; } = new();
    public List<Decorator> Decorators { get; init; } = new();
    public string? DocString { get; init; }
}
```

**Struct vs Class:**
- Structs compile to C# value types (`struct` in C#)
- Can only implement interfaces, not inherit from classes
- Useful for small, immutable data types

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

**Example Sharpy Code:**
```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3
```

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
From-import style imports.

```csharp
public record FromImportStatement : Statement
{
    public string Module { get; init; } = "";
    public List<ImportAlias> Names { get; init; } = new();
    public bool ImportAll { get; init; }  // from module import *
}
```

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

**Important:** Semantic information (like resolved types) is stored separately in `SemanticInfo`, not on AST nodes themselves. This maintains immutability.

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

**Note:** Some helper records (like `ElifClause`, `ExceptHandler`, `Parameter`) include their own location fields since they don't inherit from `Node`.

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

### 6.3 Type System Integration

When debugging type-checking issues:

1. **Check `SemanticInfo`** - Types are NOT stored on AST nodes
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

1. **Check `RoslynEmitter.cs`** - Finds the `Visit*` method for your statement type
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

**DO:**
- Add new optional properties
- Add helper methods (but consider if they belong elsewhere)
- Improve documentation comments

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
- **Semantic analyzer architecture** (`docs/architecture/semantic-analyzer-architecture.md`) if validation changes
- **Code samples** (`samples/`) to demonstrate new features

---

## 8. Related Files

Understanding Statement.cs is easier with context from these related files:

| File | Purpose |
|------|---------|
| `Node.cs` | Base class, defines source location tracking |
| `Expression.cs` | Expression nodes used within statements |
| `Types.cs` | Type annotation nodes |
| `Parser.cs` | Creates these statement instances |
| `TypeChecker.cs` | Validates statement semantics |
| `RoslynEmitter.cs` | Converts statements to C# |
| `AstDumper.cs` | Debugging utility for visualizing AST |

---

## 9. Quick Reference

### Statement Categories

```
Simple Statements:
  - ExpressionStatement    (expr as statement)
  - Assignment            (x = value, x += value)
  - VariableDeclaration   (x: int = 5)
  - AssertStatement       (assert condition)
  - PassStatement         (pass)
  - BreakStatement        (break)
  - ContinueStatement     (continue)
  - ReturnStatement       (return value)
  - RaiseStatement        (raise exception)

Compound Statements:
  - IfStatement           (if-elif-else)
  - WhileStatement        (while loop)
  - ForStatement          (for-in loop)
  - TryStatement          (try-except-finally)

Definitions:
  - FunctionDef           (def name():)
  - ClassDef              (class Name:)
  - StructDef             (struct Name:)
  - InterfaceDef          (interface IName:)
  - EnumDef               (enum Name:)

Imports:
  - ImportStatement       (import module)
  - FromImportStatement   (from module import name)
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

---

## 10. FAQ

**Q: Why are statements immutable?**
A: Immutability prevents bugs from unexpected modifications, enables safe concurrent processing, and supports functional-style transformations.

**Q: Where are statement types stored?**
A: Types are resolved by the semantic analyzer and stored in `SemanticInfo`, not on the AST nodes themselves.

**Q: Can I add methods to statement records?**
A: Technically yes, but avoid it. AST nodes should be pure data. Put behavior in visitors, analyzers, or emitters.

**Q: Why not use inheritance for similar statements?**
A: Each statement type has unique properties. Flattening the hierarchy keeps the design simple and explicit.

**Q: How do I add a new statement type?**
A: Follow the pattern: define the record here → update parser → add semantic checks → implement code generation → write tests.

**Q: Where's the logic for executing statements?**
A: Sharpy is a compiled language. Statements are transformed to C# by `RoslynEmitter`, then .NET executes the resulting IL.

---

## Summary

`Statement.cs` is the data model for all Sharpy statements. It's a pure, immutable representation of program structure that flows through the compiler pipeline. As a newcomer:

1. **Start here** when understanding how Sharpy syntax maps to AST structure
2. **Reference this** when writing parsers, analyzers, or code generators
3. **Extend this** carefully, following the established patterns
4. **Remember**: This is just data - the interesting logic happens in the components that consume these nodes

For deeper understanding, explore the related files (Parser.cs, TypeChecker.cs, RoslynEmitter.cs) to see how these statement nodes are created, validated, and transformed.
