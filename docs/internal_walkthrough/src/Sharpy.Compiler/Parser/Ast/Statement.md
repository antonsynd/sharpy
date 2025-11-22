# Walkthrough: Statement.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

---

## Overview

`Statement.cs` defines the **Abstract Syntax Tree (AST) node types for all statements** in the Sharpy programming language. This file is one of the core pieces of the compiler's parser component, representing the structural backbone of any Sharpy program.

**What it does:**
- Defines C# record types for every kind of statement in Sharpy (assignments, control flow, function definitions, etc.)
- Provides the data structures that the Parser creates and the Semantic Analyzer and Code Generator consume
- Uses modern C# records with init-only properties for immutability and pattern matching

**Role in the compiler pipeline:**
```
Source Code → Lexer (tokens) → Parser (AST) → Semantic Analyzer → Code Generator
                                      ↑
                               Statement.cs defines
                               these AST node types
```

---

## Architecture: The Statement Hierarchy

All statement nodes inherit from the base `Statement` record:

```csharp
public abstract record Statement : Node;
```

This inherits from `Node` (defined in `Node.cs`), which provides source location tracking:
- `LineStart`, `ColumnStart` - Where the statement begins in source code
- `LineEnd`, `ColumnEnd` - Where it ends

**Design Decision**: Using C# 9+ records makes AST nodes:
- **Immutable** - Once created, they can't be modified (thread-safe, easier to reason about)
- **Value-based equality** - Two nodes with same properties are equal
- **Pattern matching friendly** - Works great with switch expressions and is/as patterns

---

## Class/Type Structure

The file organizes statements into four logical groups:

### 1. Simple Statements (Lines 8-97)

These are single-line executable statements:

| Statement Type | Purpose | Key Properties |
|----------------|---------|----------------|
| `ExpressionStatement` | Expression used as statement (e.g., `print("hi")`) | `Expression` |
| `Assignment` | Variable assignment (`x = 5`, `x += 3`) | `Target`, `Value`, `Operator` |
| `VariableDeclaration` | Typed variable declaration (`x: int = 5`) | `Name`, `Type`, `InitialValue`, `IsConst` |
| `AssertStatement` | Runtime assertion (`assert x > 0, "msg"`) | `Test`, `Message` |
| `PassStatement` | No-op placeholder | (none) |
| `BreakStatement` | Exit loop | (none) |
| `ContinueStatement` | Skip to next iteration | (none) |
| `ReturnStatement` | Return from function | `Value` (nullable) |
| `RaiseStatement` | Raise exception | `Exception`, `Cause` |

**Important**: `Assignment` supports compound operators through the `AssignmentOperator` enum (13 different operators from `=` to `>>=`).

### 2. Compound Statements (Lines 99-166)

These are multi-line statements containing other statements:

| Statement Type | Purpose | Key Properties |
|----------------|---------|----------------|
| `IfStatement` | Conditional branching | `Test`, `ThenBody`, `ElifClauses`, `ElseBody` |
| `WhileStatement` | While loop | `Test`, `Body` |
| `ForStatement` | For-each loop | `Target`, `Iterator`, `Body` |
| `TryStatement` | Exception handling | `Body`, `Handlers`, `FinallyBody` |

**Key Detail**: `IfStatement` uses `List<ElifClause>` for elif branches, each with its own test and body. This allows unlimited elif chains.

### 3. Definitions (Lines 168-275)

These define new types, functions, or enums:

| Definition Type | Purpose | Key Properties |
|-----------------|---------|----------------|
| `FunctionDef` | Function/method definition | `Name`, `Parameters`, `ReturnType`, `Body`, `Decorators`, `DocString` |
| `ClassDef` | Reference type class | `Name`, `TypeParameters`, `BaseClasses`, `Body`, `Decorators`, `DocString` |
| `StructDef` | Value type struct | Same as ClassDef, but BaseClasses are interfaces only |
| `InterfaceDef` | Interface definition | `Name`, `TypeParameters`, `BaseInterfaces`, `Body`, `DocString` |
| `EnumDef` | Enumeration definition | `Name`, `Members`, `DocString` |

**Design Pattern**: All definition types support decorators (`@property`, `@staticmethod`, etc.) and docstrings for documentation.

### 4. Import Statements (Lines 277-309)

Module import statements:

| Import Type | Purpose | Example | Key Properties |
|-------------|---------|---------|----------------|
| `ImportStatement` | Import modules | `import sys as s` | `Names` (list of `ImportAlias`) |
| `FromImportStatement` | Import from module | `from math import pi` | `Module`, `Names`, `ImportAll` |

---

## Supporting Types and Helpers

### AssignmentOperator Enum (Lines 28-43)

Defines all 13 assignment operators:
```csharp
Assign,        // =
PlusAssign,    // +=
MinusAssign,   // -=
// ... and 10 more
```

**Usage**: The code generator maps these to their C# equivalents. Most map 1:1, but `PowerAssign` (`**=`) requires special handling since C# doesn't have a power operator.

### ElifClause (Lines 112-122)

Represents an `elif` branch:
```csharp
public record ElifClause
{
    public Expression Test { get; init; } = null!;
    public List<Statement> Body { get; init; } = new();
    // Source location tracking...
}
```

**Why separate?** Elif clauses aren't full statements themselves—they're part of an IfStatement. This design keeps the AST clean.

### ExceptHandler (Lines 153-164)

Represents a `catch` block in try-except:
```csharp
public record ExceptHandler
{
    public TypeAnnotation? ExceptionType { get; init; }
    public string? Name { get; init; }  // The "e" in "except ValueError as e:"
    public List<Statement> Body { get; init; } = new();
}
```

**Nullable fields**: Both `ExceptionType` and `Name` are nullable because:
- `except:` (bare except) has no type
- `except ValueError:` has type but no name binding

### EnumMember (Lines 231-241)

Individual enum member:
```csharp
public record EnumMember
{
    public string Name { get; init; } = "";
    public Expression? Value { get; init; }  // Optional explicit value
}
```

**Note**: The `Value` is nullable because enum members can be auto-numbered (`Red, Green, Blue`) or explicitly valued (`Red = 1, Green = 2`).

### Decorator (Lines 246-257)

Applied to functions, classes, or structs:
```csharp
public record Decorator
{
    public string Name { get; init; } = "";
    // Note: v0.5 only supports simple identifier decorators
}
```

**Current Limitation**: Version 0.5 only supports simple decorators (`@staticmethod`), not parameterized ones (`@decorator(arg)`) or dotted names (`@module.decorator`). This is documented in the code comment.

### Parameter (Lines 262-273)

Function parameter definition:
```csharp
public record Parameter
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public Expression? DefaultValue { get; init; }
}
```

**Nullable fields**:
- `Type` is nullable for duck-typed parameters (though Sharpy v0.5 requires types)
- `DefaultValue` is nullable for required parameters

### ImportAlias (Lines 287-297)

Used by both `ImportStatement` and `FromImportStatement`:
```csharp
public record ImportAlias
{
    public string Name { get; init; } = "";      // "System.Collections.Generic"
    public string? AsName { get; init; }         // "SCG" in "import ... as SCG"
}
```

---

## Key Design Patterns

### 1. Record Types with Init-Only Properties

**Pattern:**
```csharp
public record Assignment : Statement
{
    public Expression Target { get; init; } = null!;
    public Expression Value { get; init; } = null!;
}
```

**Why?**
- **Immutability**: Can't accidentally modify AST after creation
- **Object initializers**: Clean syntax for creating nodes
- **Thread safety**: Safe to share AST across multiple analyzer passes

**The `= null!` pattern**: This tells the compiler "I know this will be initialized via object initializer, trust me." The parser ensures these are always set.

### 2. Nullable vs Non-Nullable Design

The code carefully uses nullable types (`?`) to encode optionality:

```csharp
// Required
public Expression Test { get; init; } = null!;

// Optional
public Expression? InitialValue { get; init; }
public Expression? Message { get; init; }
```

**Guidelines**:
- Required fields → non-nullable with `= null!`
- Optional fields → nullable with `?`
- This self-documents the AST structure

### 3. Source Location Tracking

**Pattern**: Most helper types (not full Statements) manually track their source location:

```csharp
public record ElifClause
{
    // ... properties ...
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**Why not inherit from Node?** These aren't standalone AST nodes—they're components of larger statements. But we still need location info for error reporting.

### 4. List Properties for Multiplicities

Collections use `List<T>` with empty initializers:

```csharp
public List<Statement> Body { get; init; } = new();
public List<ElifClause> ElifClauses { get; init; } = new();
```

**Why `= new()`?** Prevents null reference exceptions. Empty lists are safer than nulls for "zero or more" relationships.

---

## Dependencies on Other Components

### Direct Dependencies

1. **`Node.cs`**: Base class providing source location tracking
2. **`Expression.cs`**: All expression types used in statements
   - `Test` expressions in if/while/for
   - `Value` expressions in assignments
   - `Target` expressions in assignments/for loops
3. **`Types.cs`**: `TypeAnnotation` for type declarations and annotations

### Consumer Components

Code that **uses** Statement.cs:

1. **Parser** (`Parser.cs`): Creates these statement instances
2. **Semantic Analyzer**: Traverses statements to check types and build symbol tables
3. **Code Generator** (`RoslynEmitter.cs`): Generates C# code from statements
4. **AstDumper**: Visualizes AST for debugging

---

## Usage Examples

### Creating an Assignment Node

```csharp
var assignment = new Assignment
{
    Target = new Identifier { Name = "x" },
    Value = new IntegerLiteral { Value = "42" },
    Operator = AssignmentOperator.Assign,
    LineStart = 1,
    ColumnStart = 0,
    LineEnd = 1,
    ColumnEnd = 5
};
```

### Creating a Function Definition

```csharp
var funcDef = new FunctionDef
{
    Name = "greet",
    Parameters = new List<Parameter>
    {
        new Parameter 
        { 
            Name = "name", 
            Type = new SimpleType { Name = "str" } 
        }
    },
    ReturnType = new SimpleType { Name = "None" },
    Body = new List<Statement>
    {
        new ExpressionStatement
        {
            Expression = new Call { /* ... */ }
        }
    },
    Decorators = new List<Decorator>(),
    LineStart = 1,
    // ...
};
```

### Pattern Matching on Statements

```csharp
// In semantic analyzer or code generator
Statement stmt = /* ... */;

var result = stmt switch
{
    Assignment a => HandleAssignment(a),
    IfStatement i => HandleIf(i),
    ForStatement f => HandleFor(f),
    ReturnStatement r => HandleReturn(r),
    _ => throw new NotImplementedException($"Unhandled statement: {stmt.GetType()}")
};
```

---

## Debugging Tips

### 1. Use AstDumper to Visualize

When debugging parser issues, use `AstDumper` to see the exact AST structure:

```csharp
var dumper = new AstDumper();
var astString = dumper.Dump(module);
Console.WriteLine(astString);
```

### 2. Check Source Locations

If errors are being reported at wrong locations, verify `LineStart/ColumnStart/LineEnd/ColumnEnd` are being set correctly in the parser.

### 3. Look for Null References

The `= null!` pattern can hide issues. If you see `NullReferenceException`, check:
- Is the parser setting all required properties?
- Did someone forget to initialize a property in the object initializer?

### 4. Understanding Body Lists

Many statements have `Body` properties (if, while, for, try, function defs, class defs). Remember:
- They're `List<Statement>`, not `Statement`
- Empty bodies are valid (`if x: pass` has a body with just `PassStatement`)
- Check `Body.Count` before accessing `Body[0]`

### 5. Elif vs Else Confusion

`IfStatement` has both `ElifClauses` (list) and `ElseBody` (list of statements):

```csharp
if (ifStmt.ElifClauses.Count > 0)
{
    // There are elif branches
}

if (ifStmt.ElseBody.Count > 0)
{
    // There's an else branch
}
```

### 6. Assignment Target vs Value

In `Assignment`, `Target` is the **left side** (what's being assigned to) and `Value` is the **right side** (what's being assigned):

```python
x = 5  # Target: Identifier("x"), Value: IntegerLiteral("5")
```

### 7. For Loop Target vs Iterator

```python
for item in collection:  # Target: item, Iterator: collection
```

Don't confuse these—`Target` is the loop variable, `Iterator` is what's being iterated.

---

## Common Pitfalls and Solutions

### Pitfall 1: Forgetting to Set Source Location

**Problem**: Parser creates node but forgets location info.

**Solution**: Every statement creation should set location:
```csharp
return new IfStatement
{
    Test = test,
    ThenBody = thenBody,
    LineStart = startToken.Line,
    ColumnStart = startToken.Column,
    // ... etc
};
```

### Pitfall 2: Mutating Statements

**Problem**: Trying to modify a statement after creation.

**Solution**: Records are immutable. Use `with` expressions:
```csharp
var newStmt = oldStmt with { Body = newBody };
```

### Pitfall 3: Null Bodies

**Problem**: Forgetting to initialize `Body` lists.

**Correct**:
```csharp
public List<Statement> Body { get; init; } = new();  // ✅ Always has value
```

**Wrong**:
```csharp
public List<Statement> Body { get; init; }  // ❌ Null by default
```

### Pitfall 4: Confusing Statement Types

**Problem**: Not all executable code is an `ExpressionStatement`.

Remember:
- `x = 5` → `Assignment`
- `print("hi")` → `ExpressionStatement` wrapping a `Call` expression
- `x: int = 5` → `VariableDeclaration`
- `return 42` → `ReturnStatement`

---

## Contribution Guidelines

### When to Modify This File

You should add/modify statement types when:

1. **Adding a new statement type to Sharpy**
   - Example: Adding `match` statement (pattern matching)
   - Add new record type inheriting from `Statement`
   - Follow existing patterns (location tracking, immutability)

2. **Extending existing statements**
   - Example: Adding `else` clause to `for` loops (Python-style)
   - Add new properties to existing records

3. **Adding metadata to statements**
   - Example: Adding async/await support
   - Add new properties (e.g., `IsAsync` flag)

### Adding a New Statement Type - Checklist

```csharp
/// <summary>
/// Match statement (pattern matching)
/// </summary>
public record MatchStatement : Statement
{
    public Expression Subject { get; init; } = null!;
    public List<MatchCase> Cases { get; init; } = new();
}

public record MatchCase
{
    public Pattern Pattern { get; init; } = null!;
    public Expression? Guard { get; init; }
    public List<Statement> Body { get; init; } = new();
    
    // Don't forget source location!
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**Then update**:
1. ✅ Parser to recognize and create the new statement
2. ✅ Semantic analyzer to type-check it
3. ✅ Code generator to emit C# code
4. ✅ AstDumper to visualize it
5. ✅ Tests for all components

### Coding Conventions

**Follow these patterns**:

1. **Use record types**: `public record MyStatement : Statement`
2. **Init-only properties**: `{ get; init; }`
3. **Non-nullable for required**: `= null!`
4. **Nullable for optional**: `?`
5. **Empty list defaults**: `= new()`
6. **XML doc comments**: `/// <summary>` for all public types
7. **Source location**: Always track location info
8. **Logical grouping**: Use `#region` for organization

### Testing Your Changes

After modifying `Statement.cs`:

```bash
# Test parser creates new nodes correctly
dotnet test --filter "FullyQualifiedName~Parser"

# Test semantic analyzer handles them
dotnet test --filter "FullyQualifiedName~Semantic"

# Test code generation works
dotnet test --filter "FullyQualifiedName~CodeGen"

# Test end-to-end
dotnet test --filter "FullyQualifiedName~Integration"
```

### Documentation Requirements

When adding statements:

1. **XML doc comments** on the record type
2. **Inline comments** for non-obvious properties
3. **Update language specs** in `docs/specs/`
4. **Add examples** in `samples/` or `snippets/`
5. **Update this walkthrough** if adding major features

---

## Related Files

**Sibling AST files**:
- `Expression.cs` - All expression types
- `Types.cs` - Type annotation nodes
- `Node.cs` - Base AST node with location tracking

**Parser files**:
- `Parser.cs` - Creates these statement instances
- `AstDumper.cs` - Visualizes statements for debugging

**Consumer files**:
- `Semantic/SemanticAnalyzer.cs` - Type checks statements
- `CodeGen/RoslynEmitter.cs` - Generates C# from statements

**Tests**:
- `Sharpy.Compiler.Tests/Parser/StatementTests.cs`
- `Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs`
- `Sharpy.Compiler.Tests/CodeGen/RoslynEmitterTests.cs`

---

## Quick Reference

### Statement Categories Summary

| Category | Statements |
|----------|------------|
| **Simple** | Expression, Assignment, VariableDeclaration, Assert, Pass, Break, Continue, Return, Raise |
| **Compound** | If, While, For, Try |
| **Definitions** | FunctionDef, ClassDef, StructDef, InterfaceDef, EnumDef |
| **Imports** | Import, FromImport |

### Most Commonly Used Statements

1. **Assignment** - Variable assignments
2. **ExpressionStatement** - Function calls, method calls
3. **IfStatement** - Conditionals
4. **ReturnStatement** - Function returns
5. **FunctionDef** - Function definitions
6. **ClassDef** - Class definitions
7. **ForStatement** - Loops

### Helper Types Quick Reference

- `AssignmentOperator` - 13 assignment operators
- `ElifClause` - Elif branch in if statement
- `ExceptHandler` - Catch block in try-except
- `EnumMember` - Individual enum value
- `Decorator` - Decorator applied to definitions
- `Parameter` - Function parameter
- `ImportAlias` - Import with optional alias

---

## Further Learning

**Next steps for understanding the compiler**:

1. Read `Expression.cs` to understand expression nodes
2. Look at `Parser.cs` to see how these statements are created
3. Study `SemanticAnalyzer.cs` to see how they're analyzed
4. Check `RoslynEmitter.cs` to see how they're generated into C#

**Helpful commands**:

```bash
# Find all usages of a statement type
grep -r "IfStatement" src/Sharpy.Compiler/

# See how parser creates statements
grep -A 10 "ParseIfStatement" src/Sharpy.Compiler/Parser/Parser.cs

# Find all statement visitors
grep -r "Visit.*Statement" src/Sharpy.Compiler/
```

---

## Glossary

- **AST (Abstract Syntax Tree)**: Tree representation of source code structure
- **Statement**: Executable unit (vs. expression which produces a value)
- **Compound Statement**: Statement containing other statements (if, for, while, etc.)
- **Simple Statement**: Single-line executable statement
- **Definition**: Statement that defines a new function, class, or type
- **Record**: C# 9+ feature for immutable reference types with value semantics
- **Init-only property**: Property that can only be set during object initialization
- **Nullable reference type**: Type that can be null (marked with `?`)

---

**Pro Tip**: When working with statements, always think about the complete flow: Parser creates → Semantic Analyzer validates → Code Generator emits. Understanding where in the pipeline you are helps debug issues faster!
