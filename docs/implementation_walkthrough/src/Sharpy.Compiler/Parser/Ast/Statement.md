# Walkthrough: Statement.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

---

## Overview

`Statement.cs` defines all statement node types for the Sharpy Abstract Syntax Tree (AST). It sits at the heart of the parser output, representing the executable actions of Sharpy programs—from simple assignments and control flow to complex type definitions and imports.

**Role in Pipeline**:
- **Upstream**: Parser constructs these nodes when parsing `.spy` source files
- **Downstream**: Semantic analysis enriches them with type information; RoslynEmitter converts them to C# code
- **Design Philosophy**: Immutable record types that follow the visitor pattern for traversal

This file is organized into four major regions: Simple Statements, Compound Statements, Definitions, and Import Statements.

---

## Class/Type Structure

### Base Type Hierarchy

```csharp
abstract record Node : ILocatable           // src/Sharpy.Compiler/Parser/Ast/Node.cs
    └── abstract record Statement : Node
```

All statement nodes inherit from `Statement`, which inherits from `Node`. This gives every statement:
- Source location tracking (line/column start/end)
- Optional `TextSpan` for character-level offsets
- Immutability via C# record types

### The Four Regions

| Region | Purpose | Examples |
|--------|---------|----------|
| **Simple Statements** | Single-line actions | `Assignment`, `ReturnStatement`, `BreakStatement` |
| **Compound Statements** | Multi-block control flow | `IfStatement`, `WhileStatement`, `TryStatement` |
| **Definitions** | Type and function declarations | `FunctionDef`, `ClassDef`, `StructDef`, `EnumDef` |
| **Import Statements** | Module imports | `ImportStatement`, `FromImportStatement` |

---

## Key Classes and Design Patterns

### 1. Simple Statements

#### ExpressionStatement
Wraps an expression used as a statement (e.g., `print("hello")`, `foo.bar()`).

```csharp
public record ExpressionStatement : Statement
{
    public Expression Expression { get; init; } = null!;
}
```

**Design Note**: The `null!` pattern tells the compiler "this will be initialized by the parser, trust me." This is standard for AST nodes where initialization is guaranteed externally.

#### Assignment
Handles all assignment operators, from simple `=` to compound assignments like `+=` and `//=`.

```csharp
public record Assignment : Statement
{
    public Expression Target { get; init; } = null!;
    public Expression Value { get; init; } = null!;
    public AssignmentOperator Operator { get; init; } = AssignmentOperator.Assign;
}
```

**Key Operators**:
- `Assign` (`=`) — Direct assignment
- `PlusAssign` (`+=`), `MinusAssign` (`-=`) — Arithmetic compound
- `DoubleSlashAssign` (`//=`) — Integer division (Python-style)
- `NullCoalesceAssign` (`??=`) — .NET-style null-coalescing assignment

**Downstream Impact**: The semantic analyzer validates that `Target` is assignable (lvalue check). CodeGen translates compound operators to their C# equivalents.

#### VariableDeclaration
Declares a new variable with optional type annotation and initialization.

```csharp
public record VariableDeclaration : Statement
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public Expression? InitialValue { get; init; }
    public bool IsConst { get; init; }
}
```

**Key Scenarios**:
- `x = 5` — Type inferred from `InitialValue` (Type is null)
- `x: int = 5` — Explicit type annotation
- `const PI = 3.14` — `IsConst` = true

**Semantic Analysis**: If `Type` is null, the analyzer infers the type from `InitialValue`. Const variables receive special handling to ensure immutability.

#### Control Flow Statements

Simple one-liners for flow control:
- **PassStatement** — Python's no-op placeholder (`pass`)
- **BreakStatement** — Exit a loop
- **ContinueStatement** — Skip to next loop iteration
- **ReturnStatement** — Exit function with optional value

**Special Case: BreakWithFlagStatement**

```csharp
public record BreakWithFlagStatement : Statement
{
    public string FlagName { get; init; } = "";
}
```

This is an **internal compiler node** generated to support Python's loop-else semantics:

```python
for item in items:
    if should_break(item):
        break
else:
    print("completed")  # Only runs if no break occurred
```

The compiler transforms this into:
1. A boolean flag initialized to `true`
2. `BreakWithFlagStatement` that sets the flag to `false` before breaking
3. An `if (flag)` check around the else block

### 2. Compound Statements

These statements contain bodies (blocks of sub-statements).

#### IfStatement
Python-style if-elif-else chains.

```csharp
public record IfStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public ImmutableArray<Statement> ThenBody { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<ElifClause> ElifClauses { get; init; } = ImmutableArray<ElifClause>.Empty;
    public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;
}
```

**Design Decision**: `elif` is modeled as an array of `ElifClause` rather than nesting `IfStatement` nodes. This preserves Python's syntax structure and simplifies code generation.

**ElifClause Structure**:
```csharp
public record ElifClause
{
    public Expression Test { get; init; } = null!;
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    // ... source location tracking ...
}
```

Notice `ElifClause` is NOT a `Statement`—it's a helper record. It includes its own source location tracking for error reporting.

#### WhileStatement & ForStatement
Both support Python's optional `else` clause (executed if loop completes without `break`).

```csharp
public record WhileStatement : Statement
{
    public Expression Test { get; init; } = null!;
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;
}
```

**ForStatement Nuance**:
```csharp
public record ForStatement : Statement
{
    public Expression Target { get; init; } = null!;  // Loop variable(s)
    public Expression Iterator { get; init; } = null!;
    // ...
}
```

`Target` can be:
- Simple identifier: `for x in range(10)`
- Tuple unpacking: `for (a, b) in pairs`

The semantic analyzer handles type checking and unpacking validation.

#### TryStatement
Comprehensive exception handling with try-except-else-finally.

```csharp
public record TryStatement : Statement
{
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<ExceptHandler> Handlers { get; init; } = ImmutableArray<ExceptHandler>.Empty;
    public ImmutableArray<Statement> ElseBody { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Statement> FinallyBody { get; init; } = ImmutableArray<Statement>.Empty;
}
```

**ExceptHandler**:
```csharp
public record ExceptHandler
{
    public TypeAnnotation? ExceptionType { get; init; }
    public string? Name { get; init; }  // except Exception as e:
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
}
```

**Key Cases**:
- `except:` — Bare except (catches all), `ExceptionType` = null
- `except ValueError:` — Catch specific type, `Name` = null
- `except ValueError as e:` — Catch with binding

**Code Generation**: Maps to C# `try-catch-finally` with careful scoping of the exception variable (`Name`).

### 3. Definitions

Type and function definitions that create new symbols in the scope.

#### FunctionDef

```csharp
public record FunctionDef : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
    public ImmutableArray<Parameter> Parameters { get; init; } = ImmutableArray<Parameter>.Empty;
    public TypeAnnotation? ReturnType { get; init; }
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
    public string? DocString { get; init; }
}
```

**Type Parameters**: Supports generics with constraints:
```python
def sort[T: IComparable](items: list[T]) -> list[T]:
    pass
```

**Parameter Details**:
```csharp
public record Parameter
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public Expression? DefaultValue { get; init; }
    public bool IsVariadic { get; init; }  // *args -> params T[]
}
```

**Variadic Parameters**: `IsVariadic = true` maps to C# `params` arrays:
```python
def foo(*args: int):  # Becomes: void Foo(params int[] args)
```

#### ClassDef vs StructDef vs InterfaceDef

All three share similar structure but map to different .NET types:

| Sharpy | C# Target | Semantics |
|--------|-----------|-----------|
| `class` | `class` | Reference type, heap-allocated |
| `struct` | `struct` | Value type, stack/inline allocation |
| `interface` | `interface` | Contract definition only |

```csharp
public record StructDef : Statement
{
    public ImmutableArray<TypeAnnotation> BaseClasses { get; init; } = ...;  // Interfaces only
}
```

**Design Constraint**: Structs can only inherit interfaces, not classes. This is enforced during semantic analysis (see comment at src/Sharpy.Compiler/Parser/Ast/Statement.cs:229).

#### EnumDef

```csharp
public record EnumDef : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<EnumMember> Members { get; init; } = ImmutableArray<EnumMember>.Empty;
}

public record EnumMember
{
    public string Name { get; init; } = "";
    public Expression? Value { get; init; }  // Optional explicit value
}
```

**v0.5 Limitation**: Only simple enums supported (no associated data, no methods) as noted at src/Sharpy.Compiler/Parser/Ast/Statement.cs:248.

**Code Generation**:
- If all `Value` properties are null, auto-number from 0
- Otherwise, use explicit values

#### TypeAlias

```csharp
public record TypeAlias : Statement
{
    public string Name { get; init; } = "";
    public TypeAnnotation? Type { get; init; }
    public FunctionType? FunctionType { get; init; }
}
```

**Invariant**: Exactly one of `Type` or `FunctionType` must be set (see src/Sharpy.Compiler/Parser/Ast/Statement.cs:276).

**Examples**:
- `type UserId = int` — `Type` is set
- `type Callback = (int, str) -> bool` — `FunctionType` is set

**Semantic Role**: Introduces a type synonym in the symbol table. Unlike C# `using` aliases, these are first-class type names in Sharpy.

#### Type Parameters & Constraints

```csharp
public record TypeParameterDef
{
    public string Name { get; init; } = "";
    public ImmutableArray<ConstraintClause> Constraints { get; init; } = ...;
}

public abstract record ConstraintClause;
public record TypeConstraint : ConstraintClause { ... }       // T: IComparable
public record ClassConstraint : ConstraintClause;              // T: class
public record StructConstraint : ConstraintClause;             // T: struct
public record NewConstraint : ConstraintClause;                // T: new()
```

**C# Mapping**:
```python
def foo[T: class, T: new()](x: T):  # where T : class, new()
```

Maps directly to C# generic constraints.

#### Decorator

```csharp
public record Decorator
{
    public string Name { get; init; } = "";
    // Note: v0.3 only supports simple identifier decorators
}
```

**Current Limitation**: Only simple names like `@override` or `@staticmethod`. No arguments or dotted paths in v0.3 (see src/Sharpy.Compiler/Parser/Ast/Statement.cs:339-340).

**Semantic Handling**: Decorators are validated against a known set (e.g., `@override`, `@staticmethod`, `@property`). Unknown decorators emit warnings.

### 4. Import Statements

#### ImportStatement

```csharp
public record ImportStatement : Statement
{
    public ImmutableArray<ImportAlias> Names { get; init; } = ...;
}

public record ImportAlias
{
    public string Name { get; init; } = "";       // module.submodule
    public string? AsName { get; init; }          // Optional alias
}
```

**Examples**:
- `import sys` — `Names = [ImportAlias("sys", null)]`
- `import sys as s` — `Names = [ImportAlias("sys", "s")]`
- `import sys, os` — `Names = [ImportAlias("sys", null), ImportAlias("os", null)]`

#### FromImportStatement

```csharp
public record FromImportStatement : Statement
{
    public string Module { get; init; } = "";
    public ImmutableArray<ImportAlias> Names { get; init; } = ...;
    public bool ImportAll { get; init; }  // from module import *

    // Semantic analysis enrichment:
    public string? ResolvedModulePath { get; set; }
    public Dictionary<string, Semantic.Symbol>? ReExportedSymbols { get; set; }
}
```

**Key Mutable Properties**:

This is one of the few places where the AST is **mutated** after parsing:

1. **ResolvedModulePath**: Set during semantic analysis when resolving relative imports (src/Sharpy.Compiler/Parser/Ast/Statement.cs:418-421):
   ```python
   from .helpers import foo  # Resolves to "mypackage.helpers"
   ```

2. **ReExportedSymbols**: Tracks symbols re-exported for module's public API (src/Sharpy.Compiler/Parser/Ast/Statement.cs:423-429):
   ```python
   from .internal import Thing  # Thing is now part of this module's exports
   ```

**Design Justification**: While AST is generally immutable, import resolution requires cross-module information only available during semantic analysis. Storing this in the AST node simplifies code generation.

---

## Dependencies

### Internal Dependencies

- **`Expression.cs`**: All `Expression` properties reference this sibling file
- **`Types.cs`**: `TypeAnnotation`, `FunctionType` definitions
- **`Node.cs`**: Base `Node` class and `ILocatable` interface
- **`Semantic/Symbol.cs`**: Used in `FromImportStatement.ReExportedSymbols`

### External Dependencies

- **`System.Collections.Immutable`**: All collections use `ImmutableArray<T>` for safety and performance
- **`Sharpy.Compiler.Text`**: `TextSpan` for character-level source spans

---

## Patterns and Design Decisions

### 1. Immutable Record Types

All AST nodes use C# 9.0 `record` types with `init` properties:

```csharp
public record IfStatement : Statement
{
    public Expression Test { get; init; } = null!;
    // ...
}
```

**Benefits**:
- Structural equality (useful for testing and AST comparison)
- Immutability by default (prevents accidental mutation during traversal)
- Concise syntax with automatic `ToString()` implementations

**Exception**: `FromImportStatement` has mutable `ResolvedModulePath` and `ReExportedSymbols` for semantic enrichment.

### 2. ImmutableArray for Collections

Every collection uses `ImmutableArray<T>`:
- **Performance**: Faster iteration than `List<T>`, no bounds checking overhead
- **Safety**: Cannot be modified after creation
- **Default Values**: Empty arrays via `ImmutableArray<T>.Empty`

### 3. Source Location Tracking

Every AST node and helper record (like `ElifClause`, `Parameter`) tracks source location:
- Line/column (1-indexed, for error messages)
- Optional `TextSpan` (character offsets, for editor integrations)

This enables high-quality error diagnostics throughout the pipeline.

### 4. Helper Records vs. Statement Subclasses

Some constructs are **not** statements themselves:
- `ElifClause` — part of `IfStatement`
- `ExceptHandler` — part of `TryStatement`
- `EnumMember` — part of `EnumDef`
- `ImportAlias`, `Parameter`, `Decorator`, etc.

These are plain records, not `Statement` subclasses, because they don't stand alone in the AST.

### 5. null! vs. Optional Types

Pattern usage:
- `= null!` — Required property that parser guarantees to initialize
- `= null` or `?` — Optional property that may be absent

Example:
```csharp
public Expression Test { get; init; } = null!;      // Always present
public Expression? Value { get; init; }             // May be null
```

### 6. Python Fidelity vs. .NET Semantics

**Loop Else Clauses**: Python's `for-else` and `while-else` are preserved in the AST, then compiled using `BreakWithFlagStatement` lowering.

**Compound Assignments**: Python's `//=` (floor division) is preserved as `DoubleSlashAssign`, then mapped to the correct C# operation.

**Exception Handling**: Python's `else` clause in try-except (runs if no exception) is supported natively.

---

## Debugging Tips

### 1. Inspecting the AST

Use the `emit ast` command to visualize parsed AST:
```bash
dotnet run --project src/Sharpy.Cli -- emit ast file.spy
```

This shows the full AST structure, including all properties and nested nodes.

### 2. Common Pitfalls

**Null Reference Exceptions**: If a required property like `Test` or `Body` is null, the parser likely has a bug. Check the parser's statement parsing methods.

**Empty Bodies**: Statements with empty bodies (`ImmutableArray<Statement>.Empty`) are valid (e.g., `pass` as the only statement), but may indicate incomplete parsing.

**Missing Source Locations**: If `LineStart` is 0, the parser didn't set location info. This breaks error reporting.

### 3. Tracing Semantic Changes

For `FromImportStatement`, set breakpoints where `ResolvedModulePath` and `ReExportedSymbols` are assigned:
- Module resolution phase in `SemanticAnalyzer`
- Symbol table population

### 4. Immutable Array Gotchas

```csharp
var arr = ImmutableArray<Statement>.Empty;
arr.Add(stmt);  // WRONG! Returns new array, doesn't modify arr
arr = arr.Add(stmt);  // Correct
```

When building ASTs in tests or tools, remember to reassign immutable collections.

---

## Contribution Guidelines

### When to Modify This File

1. **Adding New Statement Types**: New language features (e.g., `match` statement, `with` statement)
2. **Extending Existing Statements**: Adding properties to support new semantic info
3. **Constraints**: Adding new constraint types for generics

### When NOT to Modify This File

1. **Type Annotations**: Those belong in `Types.cs`
2. **Expression Nodes**: Those belong in `Expression.cs`
3. **Semantic Metadata**: Add to `SemanticInfo` class, not AST nodes (except for import resolution)

### Adding a New Statement Type

1. Choose the appropriate region (Simple, Compound, Definitions, Imports)
2. Inherit from `Statement`
3. Add necessary properties with `init` accessors
4. Use `ImmutableArray<T>` for collections
5. Set default values (`= null!`, `= ImmutableArray<T>.Empty`, etc.)
6. Add XML doc comment explaining the statement's purpose
7. Update parser to construct the new node type
8. Update RoslynEmitter to generate C# code for it
9. Add tests in `Sharpy.Compiler.Tests`

### Example: Adding a `WithStatement`

```csharp
/// <summary>
/// With statement for context managers (with resource as r:)
/// </summary>
public record WithStatement : Statement
{
    public Expression ContextManager { get; init; } = null!;
    public string? TargetName { get; init; }  // Optional 'as' binding
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
}
```

Then:
1. Update `Parser.cs` to parse `with` keyword
2. Update `RoslynEmitter.cs` to generate `using` statement
3. Update semantic analyzer for scope handling
4. Add integration tests

### Code Style

- Match existing formatting (4-space indent, K&R braces)
- XML doc comments for all public types
- Group related types (e.g., `IfStatement` and `ElifClause`)
- Use `#region` markers for organization

### Backwards Compatibility

AST nodes are serialization boundaries. Changing property names or removing properties is **breaking**. When making changes:
- Add new optional properties (`= null`)
- Deprecate old properties with `[Obsolete]`
- Coordinate with serialization code if AST is persisted

---

## Cross-References

### Related AST Files
- **[Node.md](Node.md)** — Base `Node` class and `ILocatable` interface
- **[Expression.md](Expression.md)** — Expression node types referenced by statements
- **[Types.md](Types.md)** — `TypeAnnotation` and type system nodes
- **[Pattern.md](Pattern.md)** — Pattern matching nodes (for future `match` statement)
- **[Statement.Future.md](Statement.Future.md)** — Planned future statement types

### Parser Integration
- **Parser.cs** — Constructs these statement nodes from tokens
- **ParserStatements.cs** — Specific parsing logic for each statement type

### Semantic Analysis
- **SemanticAnalyzer.cs** — Enriches AST with type info
- **SymbolTable.cs** — Stores symbols from definitions

### Code Generation
- **[RoslynEmitter.md](../../CodeGen/RoslynEmitter.md)** — Converts statements to C# syntax trees
- **RoslynEmitter.Statements.cs** — Statement-specific emission logic

### Testing
- **ParserTests.cs** — Unit tests for statement parsing
- **FileBasedIntegrationTests.cs** — End-to-end tests using `.spy` files

---

## Quick Reference Table

| Statement Type | Python Example | Key Properties | Special Notes |
|----------------|----------------|----------------|---------------|
| `ExpressionStatement` | `print("hi")` | `Expression` | Wraps any expression |
| `Assignment` | `x += 5` | `Target`, `Value`, `Operator` | 14 operator types |
| `VariableDeclaration` | `x: int = 5` | `Name`, `Type`, `InitialValue`, `IsConst` | Type inference if `Type` is null |
| `IfStatement` | `if x:\n  pass\nelif y:\n  pass` | `Test`, `ThenBody`, `ElifClauses`, `ElseBody` | Elif is array, not nested ifs |
| `WhileStatement` | `while x:\n  pass\nelse:\n  pass` | `Test`, `Body`, `ElseBody` | Else runs if no break |
| `ForStatement` | `for x in y:\n  pass` | `Target`, `Iterator`, `Body`, `ElseBody` | Target can be tuple |
| `TryStatement` | `try:\n  pass\nexcept E as e:\n  pass` | `Body`, `Handlers`, `ElseBody`, `FinallyBody` | Full Python semantics |
| `FunctionDef` | `def foo[T](x: T) -> T:\n  pass` | `Name`, `TypeParameters`, `Parameters`, `ReturnType`, `Body` | Supports generics |
| `ClassDef` | `class Foo(Bar):\n  pass` | `Name`, `TypeParameters`, `BaseClasses`, `Body` | Reference type |
| `StructDef` | `struct Point:\n  pass` | `Name`, `BaseClasses`, `Body` | Value type, interfaces only |
| `InterfaceDef` | `interface IFoo:\n  pass` | `Name`, `BaseInterfaces`, `Body` | Contract only |
| `EnumDef` | `enum Color:\n  Red\n  Green` | `Name`, `Members` | Simple enums only |
| `TypeAlias` | `type UserId = int` | `Name`, `Type`, `FunctionType` | Exactly one type set |
| `ImportStatement` | `import sys as s` | `Names` (array of `ImportAlias`) | Multi-import support |
| `FromImportStatement` | `from os import path` | `Module`, `Names`, `ImportAll`, `ResolvedModulePath` | Mutable resolution fields |

---

## Summary

`Statement.cs` is the data model for all Sharpy statements. It's a pure, immutable representation of program structure that flows through the compiler pipeline. As a newcomer:

1. **Start here** when understanding how Sharpy syntax maps to AST structure
2. **Reference this** when writing parsers, analyzers, or code generators
3. **Extend this** carefully, following the established patterns
4. **Remember**: This is just data - the interesting logic happens in the components that consume these nodes

**Key Takeaways**:
- 30+ statement types organized into 4 categories
- Immutable records with `init` properties (except for import resolution)
- Source location tracking on all nodes
- Support for Python features like loop-else, exception chaining, and generic constraints
- Clean separation between AST (structure) and semantic info (types, symbols)

For deeper understanding, explore the related files (Parser.cs, TypeChecker.cs, RoslynEmitter.cs) to see how these statement nodes are created, validated, and transformed.

---

**Last Updated**: 2026-01-23
