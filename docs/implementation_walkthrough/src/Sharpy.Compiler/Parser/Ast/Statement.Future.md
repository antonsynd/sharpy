# Walkthrough: Statement.Future.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs`

---

## Overview

`Statement.Future.cs` contains **placeholder AST node definitions** for advanced statement-level language features planned for Sharpy v0.2.x. This file follows the "future features" pattern used throughout the Sharpy compiler, where AST nodes are defined early to establish immutable data structure contracts before implementing parser, semantic analysis, or code generation logic.

### Role in the Compiler Pipeline

**Position**: AST Node Definitions (Parser stage)  
**Status**: Placeholder definitions — parser logic **not yet implemented**  
**Target Version**: v0.2.x

```
Source (.spy) → Lexer → Parser (creates these AST nodes) → Semantic → CodeGen → C#
                              ↑ NOT YET IMPLEMENTED
```

This file currently contains:
1. **MatchStatement** - Pattern matching as a statement (control flow, no return value)
2. **MatchCase** - Individual cases within a match statement
3. **UnionDef** - Tagged union / algebraic data type definitions
4. **UnionCaseDef** - Individual variants in a union type
5. **UnionCaseField** - Fields within a union variant

These definitions exist to:
- Establish the immutable record-based contract before implementation begins
- Enable semantic analysis and code generation design to proceed in parallel
- Serve as authoritative documentation for the planned feature structure
- Allow type-safe references across the codebase before runtime support exists

---

## File Organization

The file uses clear `#region` markers to group related features by version:

```csharp
#region Pattern Matching (v0.2.x)
// MatchStatement, MatchCase
#endregion

#region Tagged Unions / ADTs (v0.2.x)
// UnionDef, UnionCaseDef, UnionCaseField
#endregion
```

This structure mirrors the companion file `Expression.Future.cs` which contains future expression nodes (like `MatchExpression`).

### Relationship to Other AST Files

| File | Purpose |
|------|---------|
| `Statement.cs` | **Current** implemented statement nodes (if, while, for, class, function, etc.) |
| `Statement.Future.cs` | **Future** statement nodes planned for v0.2.x+ |
| `Expression.Future.cs` | Future expression nodes (await, match expressions) |
| `Pattern.cs` | Pattern matching AST nodes used by both match statements and expressions |
| `Node.cs` | Base `Node` class with source location tracking |

---

## Class/Type Structure

### Base Type: `Statement`

All nodes in this file inherit from `Statement`, which inherits from `Node`:

```
Node (abstract record)
 ├─ Statement (abstract record)
 │   ├─ MatchStatement (future)
 │   ├─ UnionDef (future)
 │   └─ ... (if, while, for, class, etc. in Statement.cs)
 ├─ Expression (abstract record)
 └─ Pattern (abstract record)
```

**Key inheritance properties from `Node`**:
- `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd` - Source location tracking (line/column based)
- `Span: TextSpan?` - Optional character-offset span for precise error messages and IDE tooling

---

## Pattern Matching: MatchStatement and MatchCase

### Why Two Forms of Match?

Sharpy distinguishes between:
- **MatchExpression** (in `Expression.Future.cs`) - Returns a value, used in expressions
- **MatchStatement** (this file) - Executes code blocks, used for control flow

This mirrors Python's distinction between expression forms and statement forms (e.g., lambda vs. def, conditional expressions vs. if statements).

### MatchStatement

**Purpose**: Represents a `match` statement that branches execution based on pattern matching.

```csharp
public record MatchStatement : Statement
{
    public Expression Scrutinee { get; init; } = null!;
    public ImmutableArray<MatchCase> Cases { get; init; } = ImmutableArray<MatchCase>.Empty;
}
```

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `Scrutinee` | `Expression` | The value being matched against (e.g., `x` in `match x:`) |
| `Cases` | `ImmutableArray<MatchCase>` | The ordered list of match cases to try |

**Example Sharpy syntax** (planned):
```python
match value:
    case Pattern1:
        print("matched pattern 1")
    case Pattern2 when x > 10:
        print("matched pattern 2 with guard")
    case _:
        print("default case")
```

**Key Design Decisions**:
- Uses `ImmutableArray` instead of `List` for thread-safety and immutability
- `Scrutinee` name follows F#/OCaml terminology (the value being "scrutinized")
- Cases evaluated **in order** (first match wins), not like C# switch expressions
- Inherits source location from `Node` base class

### MatchCase

**Purpose**: Represents a single case within a match statement (pattern + optional guard + body).

```csharp
public record MatchCase
{
    public Pattern Pattern { get; init; } = null!;
    public Expression? Guard { get; init; }
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    
    // Source location fields
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}
```

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `Pattern` | `Pattern` | The pattern to match (from `Pattern.cs`) |
| `Guard` | `Expression?` | Optional `when` clause for additional conditions |
| `Body` | `ImmutableArray<Statement>` | Statements to execute if pattern matches |

**Why Not Inherit from `Node`?**

`MatchCase` is a **record** but **not** a `Node` subclass. This is a deliberate design decision:
- It's a **structural component** of `MatchStatement`, not a standalone AST node
- Similar to `Parameter`, `Decorator`, or `ExceptHandler` in `Statement.cs`
- Still tracks source location via explicit properties for error reporting

**Pattern Matching Flow**:
1. Evaluate `Scrutinee` to get value
2. For each `MatchCase` in order:
   - Test if `Pattern` matches value
   - If pattern matches and `Guard` is null OR `Guard` evaluates to true:
     - Execute `Body` statements
     - Exit match statement
3. If no cases match, behavior depends on language design (error or no-op)

**Guard Clauses**:
```python
match x:
    case n when n > 0:      # Guard: n > 0
        print("positive")
    case n when n < 0:      # Guard: n < 0
        print("negative")
    case _:                  # No guard
        print("zero")
```

Guards enable powerful filtering beyond structural pattern matching, allowing arbitrary boolean expressions to refine matches.

---

## Tagged Unions / ADTs: UnionDef and Related Types

### What are Tagged Unions?

Tagged unions (also called algebraic data types or sum types) allow defining a type that can be one of several variants, each potentially carrying different data. They're common in functional languages (Rust's `enum`, F#'s discriminated unions, Haskell's ADTs).

**Example** (planned Sharpy syntax):
```python
union Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

union Option[T]:
    case Some(value: T)
    case None
```

This enables type-safe error handling and eliminates entire classes of null reference bugs.

### UnionDef

**Purpose**: Defines a union type with multiple named cases/variants.

```csharp
public record UnionDef : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeParameterDef> TypeParameters { get; init; } = ImmutableArray<TypeParameterDef>.Empty;
    public ImmutableArray<UnionCaseDef> Cases { get; init; } = ImmutableArray<UnionCaseDef>.Empty;
    public ImmutableArray<Decorator> Decorators { get; init; } = ImmutableArray<Decorator>.Empty;
    public string? DocString { get; init; }
}
```

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | The union type name (e.g., `Result`, `Option`) |
| `TypeParameters` | `ImmutableArray<TypeParameterDef>` | Generic type parameters (e.g., `[T, E]` in `Result[T, E]`) |
| `Cases` | `ImmutableArray<UnionCaseDef>` | The union variants/cases |
| `Decorators` | `ImmutableArray<Decorator>` | Applied decorators (e.g., `@frozen`, `@dataclass`) |
| `DocString` | `string?` | Documentation string |

**Why is UnionDef a Statement?**

In Python (and Sharpy), type definitions are **statements** not expressions. They appear at module or class level, not within expressions. This follows the same pattern as `ClassDef`, `InterfaceDef`, `EnumDef` in `Statement.cs`.

**Design Parallels**:
- Mirrors `EnumDef` structure (name, cases, decorators, docstring)
- Supports generics like `ClassDef` (via `TypeParameters`)
- Will integrate with Sharpy's type system and pattern matching

### UnionCaseDef

**Purpose**: Represents a single variant within a union type.

```csharp
public record UnionCaseDef
{
    public string Name { get; init; } = "";
    public ImmutableArray<UnionCaseField> Fields { get; init; } = ImmutableArray<UnionCaseField>.Empty;
    
    // Source location fields
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}
```

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Case name (e.g., `Ok`, `Err`, `None`) |
| `Fields` | `ImmutableArray<UnionCaseField>` | Data carried by this case (empty for singletons) |

**Three Flavors of Union Cases**:

1. **Singleton cases** (no data):
   ```python
   case None
   ```
   - `Fields` is empty
   - Acts like an enum variant

2. **Named field cases** (records):
   ```python
   case Ok(value: T)
   case Person(name: str, age: int)
   ```
   - Each field has a name and type
   - Acts like a lightweight class

3. **Positional field cases** (tuples):
   ```python
   case Tuple(int, str)
   ```
   - Fields have types but no explicit names
   - Acts like a tagged tuple

**Not a Node Subclass**:
Like `MatchCase`, `UnionCaseDef` is a structural component of `UnionDef`, not a standalone AST node. It still tracks source location for error reporting but doesn't inherit from `Node`.

### UnionCaseField

**Purpose**: Represents a field within a union case variant.

```csharp
public record UnionCaseField
{
    public string? Name { get; init; }
    public TypeAnnotation Type { get; init; } = null!;
    
    // Source location fields
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}
```

**Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string?` | Field name (null for positional fields) |
| `Type` | `TypeAnnotation` | Field type annotation |

**Name is Nullable**:
- Named fields: `Name` is not null → `Ok(value: T)` → `Name = "value"`
- Positional fields: `Name` is null → `Tuple(int, str)` → first field has `Name = null`

This design allows representing both named and positional parameters within the same structure, similar to how function parameters work.

---

## Dependencies

### Direct Dependencies

```csharp
using System.Collections.Immutable;
namespace Sharpy.Compiler.Parser.Ast;
```

- **System.Collections.Immutable**: Required for `ImmutableArray<T>` (immutable AST pattern)
- **Sharpy.Compiler.Parser.Ast**: Namespace containing all AST node types

### References to Other AST Types

| Type | Defined In | Usage |
|------|-----------|-------|
| `Statement` | `Statement.cs` | Base class for `MatchStatement`, `UnionDef` |
| `Expression` | `Expression.cs` | Used in `MatchStatement.Scrutinee`, `MatchCase.Guard` |
| `Pattern` | `Pattern.cs` | Used in `MatchCase.Pattern` |
| `TypeAnnotation` | `Types.cs` | Used in `UnionCaseField.Type` |
| `TypeParameterDef` | `Statement.cs` | Used in `UnionDef.TypeParameters` |
| `Decorator` | `Statement.cs` | Used in `UnionDef.Decorators` |
| `Text.TextSpan` | `Sharpy.Compiler.Text` | Optional span tracking |

### Pattern.cs Deep Dive

The `Pattern.cs` file defines the pattern AST nodes used by `MatchCase`:
- `WildcardPattern` - `_` matches anything
- `BindingPattern` - `x` or `x: int` binds to variable
- `LiteralPattern` - `42`, `"hello"` matches exact value
- `TypePattern` - `is SomeType` type checking
- `UnionCasePattern` - `Ok(value)` destructuring union cases
- `TuplePattern` - `(a, b, _)` tuple destructuring
- `ListPattern` - `[head, ...tail]` list destructuring
- `OrPattern` - `pattern1 | pattern2` alternatives
- `AndPattern` - `pattern1 and pattern2` conjunction
- `GuardPattern` - `pattern when condition` (note: guards are also on `MatchCase`)

This rich pattern system will enable powerful structural decomposition once implemented.

---

## Patterns and Design Decisions

### 1. Immutable Record Pattern

**All AST nodes are immutable records**:
```csharp
public record MatchStatement : Statement
{
    public Expression Scrutinee { get; init; } = null!;
    public ImmutableArray<MatchCase> Cases { get; init; } = ImmutableArray<MatchCase>.Empty;
}
```

**Why?**
- **Thread-safety**: AST can be shared across compiler phases safely
- **Structural equality**: Records provide value-based equality for free
- **Immutability**: Once created, AST cannot be modified (prevents bugs)
- **Init-only properties**: Can only be set during construction

**Trade-off**: Cannot modify AST after creation. Transformations require creating new nodes (but this is the intended pattern).

### 2. ImmutableArray vs List

All collections use `ImmutableArray<T>`:
```csharp
public ImmutableArray<MatchCase> Cases { get; init; } = ImmutableArray<MatchCase>.Empty;
```

**Why?**
- More efficient than `ImmutableList<T>` (array-backed, not linked list)
- Prevents accidental modifications
- Value-type semantics when empty (no allocation)
- Pattern matches Roslyn's AST design

**Note**: Initialize with `ImmutableArray<T>.Empty`, not `default` (which creates null, not empty).

### 3. Null-Forgiving Operator (!)

```csharp
public Expression Scrutinee { get; init; } = null!;
```

The `= null!` pattern appears frequently. This is **intentional**:
- Properties are set via init-only setters during construction
- Null-forgiving operator `!` tells compiler "trust me, this will be set"
- Alternative would be making properties nullable (`Expression?`), but these are **required** fields
- Parser must ensure required properties are set; null here indicates a parser bug

**When to use nullable?**
- Optional properties like `MatchCase.Guard` use `Expression?` (nullable)
- Required properties use `= null!` pattern

### 4. Placeholder Status Pattern

Every definition includes XML documentation clearly marking placeholder status:

```csharp
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: v0.2.x
/// </remarks>
public record MatchStatement : Statement
```

This serves multiple purposes:
- Prevents confusion about feature availability
- Documents planned timeline
- Appears in IDE tooltips and generated documentation
- Makes code review easier

### 5. Not Everything is a Node

Types like `MatchCase`, `UnionCaseDef`, `UnionCaseField` are **records** but **not** `Node` subclasses:

```csharp
public record MatchCase  // Not: public record MatchCase : Node
{
    public Pattern Pattern { get; init; } = null!;
    // ... but still has LineStart, ColumnStart, etc.
}
```

**Why separate?**
- These are **structural components** of their parent nodes
- Not standalone entities visited by AST visitors
- Reduces AST complexity and visitor implementations
- Similar to `Parameter`, `ExceptHandler`, `EnumMember` in `Statement.cs`

**Source location still tracked**: These types manually declare location fields for error reporting, but don't participate in the visitor pattern.

### 6. Source Location Strategy

Two complementary approaches:

**Line/Column tracking** (always present):
```csharp
public int LineStart { get; init; }
public int ColumnStart { get; init; }
public int LineEnd { get; init; }
public int ColumnEnd { get; init; }
```

**Character-offset span** (optional):
```csharp
public Text.TextSpan? Span { get; init; }
```

**Why both?**
- Line/Column: Human-readable error messages
- TextSpan: Precise IDE tooling (go-to-definition, refactoring)
- Span is nullable for backward compatibility

---

## Debugging Tips

### 1. These Nodes Don't Parse Yet

If you hit these nodes during debugging, something is wrong:
```
❌ Parser creating MatchStatement → Bug! Parser not implemented yet
✅ Semantic analyzer referencing MatchStatement → OK (forward planning)
✅ Test code constructing MatchStatement → OK (unit testing)
```

These definitions exist for planning and testing, not runtime use (yet).

### 2. Inspect with AstDumper

The `AstDumper` class (in `Parser/AstDumper.cs`) can visualize AST structure:

```csharp
var dumper = new AstDumper();
var output = dumper.Dump(matchStatement);
Console.WriteLine(output);
```

This helps verify AST structure during development.

### 3. Source Location Debugging

If error messages show wrong locations:
```csharp
// Check all location fields are set correctly
Console.WriteLine($"MatchStatement: {node.LineStart}:{node.ColumnStart} - {node.LineEnd}:{node.ColumnEnd}");

// Check span if using character offsets
if (node.Span.HasValue)
    Console.WriteLine($"Span: {node.Span.Value.Start} - {node.Span.Value.End}");
```

### 4. ImmutableArray Gotchas

```csharp
// ❌ Wrong - creates null, not empty
ImmutableArray<MatchCase> cases = default;
cases.IsEmpty; // NullReferenceException!

// ✅ Correct - creates empty array
ImmutableArray<MatchCase> cases = ImmutableArray<MatchCase>.Empty;
cases.IsEmpty; // true

// ✅ Also correct - initialize from collection
ImmutableArray<MatchCase> cases = ImmutableArray.Create(case1, case2);
```

Always check `IsDefault` vs `IsEmpty`:
- `IsDefault`: true if uninitialized (null)
- `IsEmpty`: true if initialized but has no elements

### 5. Pattern Type Confusion

When implementing semantic analysis, be careful with pattern types:
```csharp
// MatchCase.Pattern is from Pattern.cs (structural patterns)
// Don't confuse with:
//   - System.Text.RegularExpressions.Pattern (wrong!)
//   - Design patterns (conceptual, not code)
```

Fully qualify if needed: `Sharpy.Compiler.Parser.Ast.Pattern`

---

## Contribution Guidelines

### When to Modify This File

You should modify `Statement.Future.cs` when:

1. **Adding new statement-level future features**
   - New control flow constructs
   - New definition forms (trait, protocol, etc.)
   - New declarative statements

2. **Refining planned feature shapes**
   - Adding/removing fields based on design discussions
   - Updating documentation with examples
   - Adjusting type signatures

3. **Promoting features to current**
   - When implementing a feature, **move** its definition from `Statement.Future.cs` to `Statement.cs`
   - Update all references
   - Remove "PLACEHOLDER" remarks

### What NOT to Change

❌ **Don't add parser logic here** - This file is AST definitions only  
❌ **Don't add semantic analysis** - That goes in `Semantic/` directory  
❌ **Don't add code generation** - That goes in `CodeGen/` directory  
❌ **Don't break immutability** - Keep all properties `init`-only  
❌ **Don't use mutable collections** - Stick with `ImmutableArray`

### Adding a New Future Statement Type

**Step 1**: Define the record in appropriate region:
```csharp
#region Your Feature (vX.X.x)

/// <summary>
/// Brief description of what this statement does.
/// </summary>
/// <example>
/// sharpy_syntax_here()
/// </example>
/// <remarks>
/// PLACEHOLDER: Parser support not yet implemented.
/// Target version: vX.X.x
/// </remarks>
public record YourStatement : Statement
{
    public YourProperty Property { get; init; } = null!;
}

#endregion
```

**Step 2**: Add XML documentation:
- Summary (what it does)
- Example (planned syntax)
- Remarks (placeholder status + version)

**Step 3**: Define supporting types if needed:
```csharp
public record YourStatementClause
{
    // Not a Node subclass if it's a structural component
    public string Name { get; init; } = "";
    
    // Source location fields
    public int LineStart { get; init; }
    // ... etc.
}
```

**Step 4**: Update cross-references:
- Update this walkthrough document
- Update language specification if needed
- Reference in planning documents

**Step 5**: Write placeholder tests:
```csharp
[Fact(Skip = "TODO: Implement YourStatement. See issue #XXX")]
public void TestYourStatement_BasicCase()
{
    // Test code showing expected behavior
}
```

### Implementing a Future Feature

When ready to implement a feature defined here:

**Step 1**: Move definition to `Statement.cs`:
```bash
# Cut the record definition and paste into Statement.cs
# Keep it in the appropriate region (control flow, definitions, etc.)
```

**Step 2**: Remove placeholder remarks:
```csharp
/// <summary>
/// Your statement description.
/// </summary>
// Remove the PLACEHOLDER remarks
public record YourStatement : Statement
```

**Step 3**: Implement parser support:
```csharp
// In Parser.Statements.cs
public YourStatement ParseYourStatement()
{
    // Parsing logic here
}
```

**Step 4**: Implement semantic analysis:
```csharp
// In TypeChecker.cs or appropriate semantic analyzer
public void VisitYourStatement(YourStatement node)
{
    // Type checking logic
}
```

**Step 5**: Implement code generation:
```csharp
// In RoslynEmitter.Statements.cs
private StatementSyntax EmitYourStatement(YourStatement node)
{
    // C# generation using SyntaxFactory
}
```

**Step 6**: Add integration tests:
```
tests/Integration/TestFixtures/your_feature/
├── basic.spy
├── basic.expected
├── edge_case.spy
└── edge_case.expected
```

**Step 7**: Update documentation:
- Update this walkthrough to remove "future" status
- Update language specification with implemented feature
- Update CHANGELOG.md

---

## Cross-References

### Related AST Files

- **[Statement.md](Statement.md)** - Current implemented statement nodes (if, while, for, class, function, import, etc.)
- **[Expression.Future.md](Expression.Future.md)** - Future expression nodes (await, match expressions)
- **[Pattern.cs](../../../../../src/Sharpy.Compiler/Parser/Ast/Pattern.cs)** - Pattern matching AST nodes used by `MatchCase`
- **[Node.md](Node.md)** - Base `Node` class and `Module` root node
- **[Expression.md](Expression.md)** - Current expression nodes
- **[Types.md](Types.md)** - Type annotation nodes (`TypeAnnotation`, `FunctionType`, etc.)

### Parser Documentation

- **[Parser.md](../Parser.md)** - Main parser walkthrough
- **[Parser.Statements.md](../Parser.Statements.md)** - Statement parsing logic (will eventually parse these nodes)

### Semantic Analysis

- **Semantic/** - Type checking and analysis (will validate these nodes when implemented)

### Language Specification

- **`docs/language_specification/`** - Authoritative language spec (will document these features when implemented)

### Related Instructions

- **`.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`** - Compiler contribution guidelines
- **`.github/agents/parser-expert.md`** - Parser-specific agent guidance

---

## Summary

`Statement.Future.cs` is a **forward-looking** AST definition file that establishes the shape of planned Sharpy language features before implementation. It demonstrates:

- **Immutable record pattern** for AST nodes
- **Separation of concerns** between AST structure and compiler logic
- **Clear placeholder documentation** to prevent confusion
- **Thoughtful design** for pattern matching and tagged unions

The file serves as both a contract for downstream implementation and documentation for planned features. When features are ready, definitions migrate to `Statement.cs` and the full compiler pipeline is implemented.

**Key Takeaway**: These are **blueprints, not implementations**. The real work happens in the parser, semantic analyzer, and code generator—but having these definitions early enables parallel development and ensures all components agree on the AST structure.
