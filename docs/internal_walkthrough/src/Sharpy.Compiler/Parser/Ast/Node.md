# Walkthrough: Node.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Node.cs`

---

## Overview

`Node.cs` defines the foundational base types for Sharpy's Abstract Syntax Tree (AST). This file is the cornerstone of the parser output—every piece of Sharpy code that gets parsed (expressions, statements, declarations) ultimately inherits from the types defined here.

**Key Responsibilities:**
- Provides the base `Node` record that all AST nodes inherit from
- Establishes location tracking (line/column information) for all parsed elements
- Defines the root `Module` node that represents a complete Sharpy source file

**Role in the Compilation Pipeline:**
```
Source Code → Lexer → Parser → AST (starts here!) → Semantic Analyzer → Code Generator
```

This file sits at the beginning of the AST hierarchy. When the Parser reads tokens and constructs an AST, every node it creates is ultimately a `Node` or one of its descendants.

---

## Class/Type Structure

### `Node` - The Universal Base Class

```csharp
public abstract record Node
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**What it is:**
- An `abstract record` - cannot be instantiated directly, only through derived types
- Uses C# 9.0+ record syntax for immutability and value-based equality

**Why it's a record:**
- **Immutability**: AST nodes should never change after creation (functional programming principle)
- **Value equality**: Two nodes with the same content should be considered equal
- **Conciseness**: Records provide automatic `ToString()`, equality, and deconstruction

**Properties explained:**

| Property | Purpose | Example |
|----------|---------|---------|
| `LineStart` | The line number where this AST element begins | `3` (for code on line 3) |
| `ColumnStart` | The column number where this element starts | `8` (8th character on the line) |
| `LineEnd` | The line number where this element ends | `5` (multi-line element) |
| `ColumnEnd` | The column number where this element ends | `15` |

**Why location tracking matters:**
1. **Error reporting**: "Syntax error at line 42, column 8"
2. **IDE integration**: Jump to definition, hover tooltips
3. **Source mapping**: Mapping generated C# back to original Sharpy code
4. **Debugging**: Stack traces that point to the right place

**Design choice - `init` accessors:**
- Properties can only be set during object initialization
- Enforces immutability after construction
- Prevents accidental modifications during semantic analysis or code generation

---

### `Module` - The Root of the AST

```csharp
public record Module : Node
{
    public List<Statement> Body { get; init; } = new();
    public string? DocString { get; init; }
}
```

**What it represents:**
A `Module` is the top-level AST node for an entire Sharpy source file. When you compile `hello.spy`, the parser returns a `Module` instance.

**Properties:**

#### `Body: List<Statement>`
- Contains all top-level statements in the file
- Each element is a `Statement` (function definitions, class declarations, assignments, etc.)
- Order matters—statements execute/are processed in sequence

**Example Sharpy code:**
```python
# hello.spy
x: int = 42
def greet(name: str) -> None:
    print(f"Hello, {name}!")

greet("World")
```

**Corresponding Module.Body:**
```
Module {
  Body = [
    Assign(target=x, value=42),           // Line 1
    FunctionDef(name=greet, ...),         // Lines 2-3
    ExprStmt(Call(func=greet, args=...))  // Line 5
  ]
}
```

#### `DocString: string?`
- Nullable string for module-level documentation
- Extracted from the first statement if it's a string literal
- Used for generating documentation and help text

**Example with docstring:**
```python
"""
This module provides greeting utilities.
"""
def greet(name: str) -> None:
    print(f"Hello, {name}!")
```

The parser would set `Module.DocString = "This module provides greeting utilities."`

---

## Key Design Patterns

### 1. **Composite Pattern**

The AST is a classic composite pattern implementation:
- `Node` is the component
- `Module` is a composite (contains other nodes via `Body`)
- All other AST nodes (in other files) follow this pattern

```
Node (abstract base)
  ├─ Module (composite)
  │   └─ Body: List<Statement>
  ├─ Statement (abstract, in other files)
  │   ├─ FunctionDef
  │   ├─ ClassDef
  │   └─ Assign
  └─ Expression (abstract, in other files)
      ├─ BinaryOp
      ├─ Call
      └─ Literal
```

### 2. **Immutability by Default**

Using `record` types enforces immutability:
- AST nodes are constructed once and never modified
- Transformations create new nodes rather than mutating existing ones
- Thread-safe by design
- Easier to reason about during multi-pass compilation

### 3. **Source Location Preservation**

Every node tracks its source location from the start. This is critical for:
- **Quality error messages**: Show users exactly where problems are
- **IDE features**: Accurate go-to-definition and refactoring
- **Debugging**: Map runtime errors back to source

---

## Dependencies

### What `Node.cs` depends on:
- **None!** This file has no dependencies on other Sharpy code
- Only uses .NET base types: `int`, `string`, `List<T>`

### What depends on `Node.cs`:
- **Everything in the AST**: All expression, statement, and declaration types
- **Parser**: Creates instances of `Module` and other `Node` descendants
- **Semantic Analyzer**: Traverses the AST by visiting `Node` instances
- **Code Generator**: Reads the AST to generate C# code
- **AST Visitors**: Any visitor pattern implementation walks `Node` hierarchies

**Example from Parser:**
```csharp
// Parser.cs (simplified)
public Module Parse()
{
    var statements = new List<Statement>();
    while (!IsAtEnd())
    {
        statements.Add(ParseStatement());
    }
    
    return new Module
    {
        Body = statements,
        LineStart = 1,
        ColumnStart = 1,
        LineEnd = CurrentToken.Line,
        ColumnEnd = CurrentToken.Column
    };
}
```

---

## Important Implementation Details

### Why `List<Statement>` and not `ImmutableList<Statement>`?

While `Node` uses `init` for immutability, `Body` is a mutable `List<T>`. This is a pragmatic choice:

**Reasons for `List<T>`:**
- **Parser construction**: The parser builds the list incrementally as it parses
- **Performance**: Creating intermediate immutable lists is expensive
- **Pattern**: The `init` accessor means the list reference can't be changed, but contents can during initialization
- **Convention**: Once construction is complete, the list shouldn't be modified

**Best practice:**
```csharp
// During parsing (OK)
var statements = new List<Statement>();
statements.Add(ParseStatement());  // Build incrementally

var module = new Module { Body = statements };

// After construction (DON'T DO THIS)
module.Body.Add(newStatement);  // Violates immutability principle
```

### Location Tracking Strategy

The `LineStart/ColumnStart/LineEnd/ColumnEnd` pattern appears simple but enables sophisticated error reporting:

```csharp
// Error reporting example
void ReportError(Node node, string message)
{
    if (node.LineStart == node.LineEnd)
    {
        // Single-line error
        Console.Error.WriteLine(
            $"Error at line {node.LineStart}, columns {node.ColumnStart}-{node.ColumnEnd}: {message}");
    }
    else
    {
        // Multi-line error
        Console.Error.WriteLine(
            $"Error spanning lines {node.LineStart}-{node.LineEnd}: {message}");
    }
}
```

---

## Debugging Tips

### 1. **Inspecting the AST**

When debugging parser issues, inspect the `Module` object:

```csharp
// In your debugger or test
var module = parser.Parse();

// Use the debugger to expand module.Body and see the structure
// Or use AstDumper (if available)
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

### 2. **Location Tracking Issues**

If error messages point to the wrong location:
1. Check that the parser is setting `LineStart/ColumnStart/LineEnd/ColumnEnd` correctly
2. Verify that multi-line constructs (functions, classes) have correct end positions
3. Look at the lexer's token position tracking

**Common mistake:**
```csharp
// Wrong: Using current token for both start and end
new FunctionDef
{
    LineStart = currentToken.Line,
    ColumnStart = currentToken.Column,
    LineEnd = currentToken.Line,      // Should be last token's line!
    ColumnEnd = currentToken.Column   // Should be last token's column!
}
```

### 3. **Empty Module.Body**

If `Module.Body` is empty but you expected statements:
- Check parser's statement parsing logic
- Verify that `ParseStatement()` is being called
- Look for parsing errors that might cause early exit

### 4. **DocString not being captured**

The parser must explicitly extract the docstring from the first statement:

```csharp
// Expected pattern in Parser
var firstStatement = statements.FirstOrDefault();
string? docstring = null;

if (firstStatement is ExprStmt { Value: StringLiteral literal })
{
    docstring = literal.Value;
    statements.RemoveAt(0);  // Don't include docstring in body
}

return new Module { Body = statements, DocString = docstring };
```

---

## Common Patterns in the Codebase

### Visitor Pattern Usage

While not in `Node.cs` itself, AST nodes are designed for the Visitor pattern:

```csharp
public interface IAstVisitor<T>
{
    T Visit(Module node);
    T Visit(FunctionDef node);
    T Visit(ClassDef node);
    // ... etc
}

// Usage
public class TypeChecker : IAstVisitor<Type>
{
    public Type Visit(Module node)
    {
        foreach (var stmt in node.Body)
        {
            Visit(stmt);  // Polymorphic dispatch
        }
        return typeof(void);
    }
}
```

### AST Transformation

When transforming AST nodes, always create new instances:

```csharp
// ✅ CORRECT: Create new node
Module TransformModule(Module original)
{
    var transformedStatements = original.Body
        .Select(stmt => TransformStatement(stmt))
        .ToList();
    
    return original with { Body = transformedStatements };
}

// ❌ WRONG: Mutate existing node
Module TransformModule(Module original)
{
    original.Body.Clear();  // VIOLATION of immutability!
    original.Body.AddRange(newStatements);
    return original;
}
```

---

## Contribution Guidelines

### Potential Changes to `Node.cs`

This file is intentionally minimal. Most changes should be avoided, but here are scenarios where modifications might be appropriate:

#### ✅ **Appropriate Changes:**

1. **Adding source mapping information**
   ```csharp
   public abstract record Node
   {
       // Existing location properties...
       public string? SourceFile { get; init; }  // Track which file this came from
   }
   ```

2. **Adding parent node tracking** (for easier tree traversal)
   ```csharp
   public abstract record Node
   {
       // Existing properties...
       public Node? Parent { get; init; }  // Link to parent node
   }
   ```

3. **Adding metadata for compiler phases**
   ```csharp
   public abstract record Node
   {
       // Existing properties...
       public Dictionary<string, object>? Metadata { get; init; }  // Extensible metadata
   }
   ```

4. **Module-level additions** (imports, exports, type annotations)
   ```csharp
   public record Module : Node
   {
       public List<Statement> Body { get; init; } = new();
       public string? DocString { get; init; }
       public List<ImportStmt> Imports { get; init; } = new();  // Track all imports
       public List<string> Exports { get; init; } = new();      // Public API
   }
   ```

#### ❌ **Changes to Avoid:**

1. **Making properties mutable** - breaks immutability contract
2. **Adding behavior/methods** - AST nodes should be data, not behavior (use visitors)
3. **Adding type-specific properties to `Node`** - keep the base minimal
4. **Removing location tracking** - critical for error reporting

### Testing Changes

When modifying `Node.cs`, ensure:

1. **Parser tests pass**: `dotnet test --filter "FullyQualifiedName~Parser"`
2. **Semantic analyzer tests pass**: Type checking depends on correct AST structure
3. **Code generation tests pass**: Generator reads the AST
4. **Location tracking works**: Error messages still point to correct locations

### Example: Adding Source File Tracking

If you want to track which file each node came from:

```csharp
// 1. Modify Node.cs
public abstract record Node
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public string? SourceFile { get; init; }  // NEW
}

// 2. Update Parser to set this property
public class Parser
{
    private readonly string _sourceFile;
    
    public Parser(string source, string sourceFile)
    {
        _sourceFile = sourceFile;
        // ...
    }
    
    private Module CreateModule(List<Statement> body)
    {
        return new Module
        {
            Body = body,
            SourceFile = _sourceFile,  // Set on all nodes
            // ...
        };
    }
}

// 3. Update all node creation sites
// 4. Add tests to verify SourceFile is set correctly
```

---

## Related Files

To fully understand how `Node.cs` fits into the compiler:

1. **`src/Sharpy.Compiler/Parser/Ast/`** - All other AST node types (expressions, statements, etc.)
2. **`src/Sharpy.Compiler/Parser/Parser.cs`** - Creates `Module` and other nodes
3. **`src/Sharpy.Compiler/Semantic/SemanticAnalyzer.cs`** - Traverses the AST
4. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`** - Reads the AST to generate code
5. **`src/Sharpy.Compiler/Parser/AstDumper.cs`** - Visualizes AST structure for debugging

---

## Quick Reference

### Creating a Module
```csharp
var module = new Module
{
    Body = statements,
    DocString = "Optional documentation",
    LineStart = 1,
    ColumnStart = 1,
    LineEnd = 100,
    ColumnEnd = 50
};
```

### Traversing a Module
```csharp
foreach (var statement in module.Body)
{
    ProcessStatement(statement);
}
```

### Checking for DocString
```csharp
if (!string.IsNullOrWhiteSpace(module.DocString))
{
    GenerateDocumentation(module.DocString);
}
```

---

## Summary

`Node.cs` is deceptively simple but fundamentally important:
- **Base for all AST nodes** - establishes immutability and location tracking
- **Module as the root** - represents complete source files
- **Minimal by design** - complexity lives in derived types, not the base
- **Enables the entire compilation pipeline** - from parsing to code generation

When working with the Sharpy compiler, you'll interact with `Node` descendants constantly, even if you never modify `Node.cs` itself. Understanding this foundation makes the rest of the AST hierarchy much clearer.
