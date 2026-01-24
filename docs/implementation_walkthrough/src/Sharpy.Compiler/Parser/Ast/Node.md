# Walkthrough: Node.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Node.cs`

---

## 1. Overview

`Node.cs` is the foundational file of Sharpy's Abstract Syntax Tree (AST). It defines the base types that represent the syntactic structure of Sharpy programs after parsing.

**Key Responsibilities:**
- Provides the abstract base `Node` record that ALL AST nodes inherit from
- Defines the `Module` record representing the root of a parsed Sharpy program
- Tracks source location information (line/column) for every syntactic element

**Role in the Compiler Pipeline:**
```
Source Code → Lexer (Tokens) → Parser (AST) → Semantic Analysis → Code Generation
                                      ↑
                                  Node.cs lives here
```

This file is minimal by design—it establishes the contract for what all AST nodes must provide, while the actual variety of nodes (expressions, statements, etc.) are defined in sibling files.

---

## 2. Class/Type Structure

### 2.1 `Node` (Abstract Record)

```csharp
public abstract record Node : ILocatable
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    
    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// This is optional for backward compatibility - existing code that only
    /// sets Line/Column properties will continue to work.
    /// </summary>
    public TextSpan? Span { get; init; }
}
```

**Purpose**: The universal base type for all AST nodes in Sharpy.

**Design Choice - Why a Record?**
- **Immutability**: Records in C# are immutable by default, which aligns with Sharpy's philosophy that AST nodes shouldn't be modified after creation
- **Value Semantics**: Records provide structural equality, useful for comparing AST structures in tests
- **Concise Syntax**: Record syntax reduces boilerplate for simple data carriers

**Properties:**
- `LineStart` / `ColumnStart`: 1-based indices marking where this syntactic element begins in source code
- `LineEnd` / `ColumnEnd`: 1-based indices marking where this element ends
- `Span` (nullable): Character offset-based span from `Sharpy.Compiler.Text`. Optional for backward compatibility.

**Why Track Location Two Ways?**

Sharpy uses a **dual location tracking system**:

1. **Line/Column** (1-based): Traditional, human-readable coordinates
   - Used in error messages: `"error.spy:5:10: Type mismatch"`
   - Easy to communicate to developers
   - Matches most text editors' coordinate systems

2. **TextSpan** (0-based character offsets): Efficient for programmatic operations
   - Used for precise range queries: "What's at character 142?"
   - Faster for IDE features (go-to-definition, highlighting)
   - Enables efficient text slicing and manipulation

**The `ILocatable` Interface**:
```csharp
// From Sharpy.Compiler.Text.ILocatable
public interface ILocatable
{
    TextSpan? Span { get; }
}
```

This interface is shared across AST nodes, tokens, and symbols, providing a uniform way to query source locations throughout the compiler.

**Why Track Location?**
- **Error Reporting**: When semantic analysis or codegen finds issues, precise error messages need line/column info
- **IDE Features**: Future language server protocol (LSP) support needs location data for go-to-definition, hover, etc.
- **Debugging**: `AstDumper` uses these coordinates to create human-readable AST dumps
- **Refactoring Tools**: TextSpan enables precise code transformations

**Important Pattern**: The `init` keyword means these properties can only be set during object initialization (constructor or object initializer), enforcing immutability.

### 2.2 `Module` (Concrete Record)

```csharp
public record Module : Node
{
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
    public string? DocString { get; init; }
}
```

**Purpose**: Represents a complete Sharpy source file (`.spy` file) after parsing.

**Properties:**
- `Body`: An immutable array of top-level statements/declarations (functions, classes, imports, variables, etc.)
- `DocString`: Optional module-level documentation string (if the first statement is a string literal)

**Why `ImmutableArray<Statement>`?**

This uses `System.Collections.Immutable.ImmutableArray<T>`, providing:
- **True immutability**: Once created, the array contents cannot be changed
- **Performance**: More efficient than `List<T>` for read-heavy workloads (no bounds checking overhead after construction)
- **Consistency**: Aligns with the immutable design philosophy of record types
- **Thread-safety**: Can be safely shared across threads without locking

The parser builds up a regular list during parsing, then converts it to an `ImmutableArray` when constructing the `Module`.

**Typical Structure:**
```python
# example.spy
"""This is a module docstring"""  # Becomes Module.DocString

import sys                         # Statement in Body[0]

def main():                        # Statement in Body[1]
    print("Hello")

main()                            # Statement in Body[2]
```

The parser creates one `Module` instance per `.spy` file, with all top-level constructs in `Body`.

---

## 3. Key Concepts and Patterns

### 3.1 The AST Hierarchy

While `Node.cs` only defines two types, it's the root of a large hierarchy:

```
Node (abstract)
├── Statement (abstract) - defined in Statement.cs
│   ├── ExpressionStatement
│   ├── Assignment
│   ├── FunctionDef
│   ├── ClassDef
│   ├── IfStmt
│   ├── WhileStmt
│   └── ... (many more)
├── Expression (abstract) - defined in Expression.cs
│   ├── IntegerLiteral
│   ├── StringLiteral
│   ├── Identifier
│   ├── BinaryOp
│   ├── FunctionCall
│   └── ... (many more)
├── Module - defined here
└── ComprehensionClause (abstract) - defined in Expression.cs
```

**Why Split Across Files?**
- `Node.cs`: Minimal base + root module
- `Statement.cs`: All statement types (30+ records)
- `Expression.cs`: All expression types (20+ records)
- `Types.cs`: Type annotation nodes

This organization keeps files manageable and groups related concepts.

### 3.2 Immutable AST, Separate Semantic Info

**CRITICAL DESIGN DECISION:**

Sharpy's AST nodes are **pure syntax**—they contain no type information, symbol table references, or semantic data. Instead:

1. **Parser creates immutable AST** (just structure + locations)
2. **Semantic analyzer creates separate `SemanticInfo` object** that maps AST nodes to types/symbols
3. **Code generator uses both** AST (structure) + SemanticInfo (meaning)

**Why This Design?**
- **Separation of Concerns**: Parsing doesn't need to know about types; semantic analysis doesn't mutate the AST
- **Multiple Passes**: Different semantic passes (name resolution, type checking, control flow) can each add data without conflicting
- **Testability**: Can test parser output without running semantic analysis

**Example:**
```csharp
// Parser creates:
var assignment = new Assignment {
    Target = new Identifier { Name = "x" },
    Value = new IntegerLiteral { Value = "42" },
    LineStart = 5, ColumnStart = 1, ...
};

// Later, TypeChecker adds to separate store:
semanticInfo.SetType(assignment.Value, PrimitiveType.Int);
semanticInfo.SetSymbol(assignment.Target, localVariableSymbol);
```

### 3.3 Location Tracking in Practice

The parser sets location properties during node construction:

```csharp
// From Parser.cs (simplified):
var expr = new BinaryOp {
    Left = leftExpr,
    Operator = op,
    Right = rightExpr,
    LineStart = leftExpr.LineStart,      // Start of left operand
    ColumnStart = leftExpr.ColumnStart,
    LineEnd = rightExpr.LineEnd,         // End of right operand
    ColumnEnd = rightExpr.ColumnEnd,
    Span = TextSpan.FromBounds(leftExpr.Span.Start, rightExpr.Span.End)
};
```

**Usage in Error Reporting:**
```csharp
// From TypeChecker.cs:
AddError(
    $"Cannot assign type '{valueType}' to variable of type '{targetType}'",
    assignment.LineStart,
    assignment.ColumnStart
);
// Produces: "error.spy:5:10: Cannot assign type 'str' to variable of type 'int'"
```

**Usage with TextSpan for IDE Features:**
```csharp
// Finding what symbol is at cursor position:
public Symbol? GetSymbolAtPosition(int characterOffset)
{
    foreach (var node in GetAllNodes())
    {
        if (node.Span?.Contains(characterOffset) == true)
        {
            return _semanticInfo.GetSymbol(node);
        }
    }
    return null;
}
```

### 3.4 Understanding TextSpan

The `TextSpan` struct from `Sharpy.Compiler.Text` provides efficient character-based location tracking:

```csharp
public readonly struct TextSpan : IEquatable<TextSpan>
{
    public int Start { get; }    // Zero-based character offset
    public int Length { get; }   // Number of characters
    public int End => Start + Length;
    public bool IsEmpty => Length == 0;
    
    // Methods: Contains(), OverlapsWith(), Intersection(), Union()
}
```

**Creating TextSpans:**
```csharp
// Constructor form:
var span1 = new TextSpan(start: 10, length: 5);  // Characters 10-14

// From bounds:
var span2 = TextSpan.FromBounds(start: 10, end: 15);  // Characters 10-14

// Empty span:
var span3 = TextSpan.Empty;  // Start=0, Length=0
```

**Common Operations:**
```csharp
var code = "def foo(): pass";
var funcSpan = new TextSpan(0, 15);   // Entire function
var nameSpan = new TextSpan(4, 3);    // "foo"

// Extract text:
string funcText = code.Substring(funcSpan.Start, funcSpan.Length);

// Check containment:
bool contains = funcSpan.Contains(nameSpan);  // true

// Check overlap:
var bodySpan = new TextSpan(11, 4);  // "pass"
bool overlaps = nameSpan.OverlapsWith(bodySpan);  // false

// Find intersection:
var unionSpan = nameSpan.Union(bodySpan);  // Covers "foo(): pass"
```

**Why Zero-Based Offsets?**
- Matches C#'s `string.Substring(start, length)` conventions
- Efficient for array/string indexing
- Standard in most programming language tooling

---

## 4. Dependencies

### 4.1 Outbound Dependencies

`Node.cs` has **minimal dependencies**:
- `System.Collections.Immutable` (for `ImmutableArray<T>`)
- `Sharpy.Compiler.Text` (for `ILocatable` interface and `TextSpan` struct)

This is intentional—AST nodes are data structures that should stand alone with minimal coupling.

### 4.2 Inbound Dependencies (Who Uses Node?)

**Direct Consumers:**
1. **Parser (`Parser/Parser.cs`)**: Creates all AST nodes
2. **Semantic Analyzers (`Semantic/*`)**: Traverse AST nodes
   - `NameResolver`: Walks the tree to find declarations
   - `TypeResolver`: Resolves type annotations
   - `TypeChecker`: Validates type compatibility
   - `ImportResolver`: Processes import statements
3. **Code Generator (`CodeGen/RoslynEmitter.cs`)**: Translates AST to C# via Roslyn
4. **AST Dumper (`Parser/AstDumper.cs`)**: Creates human-readable representation for debugging

**Traversal Pattern:**
Most code uses pattern matching on `Node` instances:

```csharp
// Typical visitor pattern usage:
void Visit(Statement stmt)
{
    switch (stmt)
    {
        case FunctionDef funcDef:
            // Handle function...
            break;
        case ClassDef classDef:
            // Handle class...
            break;
        case Assignment assign:
            // Handle assignment...
            break;
        // ... etc
    }
}
```

---

## 5. Patterns and Design Decisions

### 5.1 Record Types with Init-Only Properties

**Pattern:**
```csharp
public record SomeNode : Node
{
    public string Name { get; init; } = "";
    public Expression Value { get; init; } = null!;
}
```

**Benefits:**
- Immutability: Can't accidentally modify after creation
- Clear construction sites: All initialization happens at object creation
- Thread-safe: Immutable objects can be safely shared across threads

**The `= null!` Pattern:**
You'll see this frequently:
```csharp
public Expression Expression { get; init; } = null!;
```

This tells the C# null-checking analyzer "trust me, this will be initialized"—it's always set by the parser through object initializers. The default is null for compiler happiness, but it's never actually null in practice.

### 5.1.1 ImmutableArray Collections Pattern

**Pattern:**
```csharp
public record SomeNode : Node
{
    public ImmutableArray<Statement> Body { get; init; } = ImmutableArray<Statement>.Empty;
}
```

**Why `ImmutableArray<T>` instead of `List<T>`?**
- **True immutability**: Aligns with record design philosophy
- **Performance**: No bounds checking after construction, more efficient for read-heavy workloads
- **Thread-safety**: Safe to share across threads
- **Memory efficiency**: No extra capacity buffer like `List<T>`

**Parser Pattern for Building ImmutableArray:**
```csharp
// In Parser.cs:
var statements = new List<Statement>();  // Mutable builder
while (!IsAtEnd())
{
    statements.Add(ParseStatement());
}

return new Module
{
    Body = statements.ToImmutableArray(),  // Convert to immutable
    LineStart = start.Line,
    // ...
};
```

**Common Operations:**
```csharp
// Iteration (efficient):
foreach (var stmt in module.Body)
{
    ProcessStatement(stmt);
}

// Indexing (efficient, no bounds check overhead):
var firstStmt = module.Body[0];

// Checking emptiness:
bool isEmpty = module.Body.IsEmpty;  // Use IsEmpty, not .Length == 0

// LINQ works:
var functions = module.Body.OfType<FunctionDef>().ToList();
```

### 5.2 Separation of Syntax and Semantics

**Key Philosophy:** AST represents "what was written," not "what it means."

**What Goes in AST:**
- ✅ Source structure (classes, functions, statements, expressions)
- ✅ Source locations (line/column)
- ✅ Literal values (strings, numbers)
- ✅ Identifiers (names as written)

**What Does NOT Go in AST:**
- ❌ Resolved types (`int`, `str`, `List<T>`)
- ❌ Symbol table entries (which variable/function is this?)
- ❌ Control flow information (reachability, definite assignment)
- ❌ Generated code (C# syntax trees)

These live in `SemanticInfo`, `SymbolTable`, and other analysis structures.

### 5.3 Abstract vs. Concrete Records

- **Abstract (`Node`, `Statement`, `Expression`)**: Never instantiated directly; exist for polymorphism
- **Concrete (`Module`, `FunctionDef`, `IntegerLiteral`)**: Actual nodes the parser creates

This hierarchy enables:
```csharp
// Can work with any expression type:
void ProcessExpression(Expression expr) { ... }

// Can work with any statement type:
void ProcessStatement(Statement stmt) { ... }
```

---

## 6. Debugging Tips

### 6.1 Inspecting the AST

Use `AstDumper` to see the parsed tree structure:

```csharp
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

Output example:
```
Module @ L1:C1
  FunctionDef 'main' @ L1:C1
    Parameters: []
    Body:
      ExpressionStatement @ L2:C5
        FunctionCall @ L2:C5
          Function: Identifier 'print' @ L2:C5
          Arguments: [StringLiteral "Hello" @ L2:C11]
```

### 6.2 Common Issues

**Problem: "Null reference exception on AST node property"**
- **Cause**: Parser didn't set a required property (e.g., `Expression = null!`)
- **Debug**: Add breakpoint in parser where the node is created; verify all properties set
- **Look for**: Object initializer missing a property

**Problem: "Wrong line/column in error message"**
- **Cause**: Parser set incorrect `LineStart/ColumnStart/LineEnd/ColumnEnd`
- **Debug**: Print the node's location; compare to actual source code position
- **Common mistake**: Using current token instead of start/end tokens

**Problem: "TextSpan is null when expected"**
- **Cause**: Parser didn't set the optional `Span` property
- **Debug**: Check if code relies on `Span` being non-null; add null checks or ensure parser sets it
- **Fix**: Either make code handle `Span` nullability or update parser to always set `Span`

**Problem: "Pattern matching not exhaustive"**
- **Cause**: Added a new statement/expression type but didn't handle it everywhere
- **Fix**: Search codebase for `switch (stmt)` or `switch (expr)` and add your case

### 6.3 Useful Queries

Find all locations where a specific node type is created:
```bash
grep -r "new FunctionDef" src/Sharpy.Compiler/Parser/
```

Find all locations where statements are pattern-matched:
```bash
grep -r "case.*Statement" src/Sharpy.Compiler/
```

Find error messages that use node locations:
```bash
grep -r "\.LineStart.*\.ColumnStart" src/Sharpy.Compiler/Semantic/
```

---

## 7. Contribution Guidelines

### 7.1 When to Modify Node.cs

**Rarely!** This file should be very stable. Consider modifying if:

1. **Adding universal metadata to all nodes**
   - Example: Adding `public SourceText? Source { get; init; }` to preserve reference to source file
   - Requires updating Parser, AstDumper, and potentially tests

2. **Changing location tracking**
   - Example: Making `TextSpan` required instead of optional (breaking change)
   - Example: Adding byte offsets in addition to character offsets
   - Impacts parser and all error reporting

3. **Adding a new root-level AST concept** (very unlikely)
   - Example: If Sharpy adds "notebook cells" as a concept above modules
   - Would require extensive changes across the compiler

**Recent Changes:**
- Added `TextSpan? Span` property for character offset tracking
- Added `ILocatable` interface implementation for unified location queries
- Changed `Module.Body` from `List<Statement>` to `ImmutableArray<Statement>` for true immutability

### 7.2 What NOT to Do

❌ **Don't add semantic information to Node**
```csharp
// BAD - don't do this:
public abstract record Node
{
    public SymbolType? ResolvedType { get; set; }  // NO!
}
```
Use `SemanticInfo` instead.

❌ **Don't make nodes mutable**
```csharp
// BAD - don't do this:
public string Name { get; set; }  // NO! Should be 'init'
```
Immutability is a core design principle.

❌ **Don't add behavior/logic to nodes**
```csharp
// BAD - don't do this:
public record FunctionDef : Statement
{
    public bool IsAsync { get; init; }
    
    public void Execute() { ... }  // NO! Nodes are data, not behavior
}
```
AST nodes are pure data structures. Behavior belongs in visitors/analyzers.

### 7.3 Adding a New AST Node Type (Outside This File)

When adding a new language feature:

1. **Decide hierarchy**: Is it a Statement, Expression, or something else?
2. **Add record to appropriate file**:
   ```csharp
   // In Statement.cs:
   public record MyNewStatement : Statement
   {
       public Expression Condition { get; init; } = null!;
       public List<Statement> Body { get; init; } = new();
   }
   ```
3. **Update Parser** to recognize and create it
4. **Update AstDumper** to display it (for debugging)
5. **Handle in semantic analyzers**:
   - NameResolver (if it declares names)
   - TypeChecker (always)
   - ControlFlowValidator (if it affects control flow)
6. **Handle in RoslynEmitter** to generate C# code
7. **Add tests** at each layer (parser, semantic, codegen, integration)

### 7.4 Testing Node.cs Changes

Since `Node` is so foundational, changes affect the entire compiler:

```bash
# Run all tests:
dotnet test

# Specific areas:
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~CodeGen"
dotnet test --filter "FullyQualifiedName~Integration"
```

**Manual Testing:**
```bash
# Create a test .spy file and parse it:
echo "def foo(): pass" > test.spy
dotnet run --project src/Sharpy.Cli -- build test.spy

# Check for errors or crashes
```

---

## 8. Advanced Topics

### 8.1 Why Not Use Roslyn's Syntax Nodes?

You might wonder: "Why create a custom AST when Roslyn (C#'s compiler) already has one?"

**Reasons:**
1. **Python-like syntax vs. C# syntax**: Sharpy's syntax is significantly different (indentation, no semicolons, different keywords)
2. **Semantic differences**: Python's dynamic features (even in a typed variant) don't map 1:1 to C#
3. **Separation of concerns**: Parsing Sharpy → Sharpy AST → C# Roslyn AST keeps each stage clean
4. **Error messages**: Sharpy errors should reference Sharpy constructs, not C# ones

### 8.2 Future Evolution: Attributes/Decorators

If Sharpy adds decorator support at the syntax level:

```python
@dataclass
class Point:
    x: int
    y: int
```

You might see:
```csharp
public record ClassDef : Statement
{
    public List<Decorator> Decorators { get; init; } = new();  // Added
    public string Name { get; init; } = "";
    // ...
}

public record Decorator : Node
{
    public Expression Expression { get; init; } = null!;
}
```

This would be added to `Statement.cs`, not `Node.cs`.

### 8.3 Performance Considerations

**AST nodes are allocated frequently** during parsing. Performance tips:

- Records are allocated on the heap (they're reference types)
- Large source files = many node instances (tens of thousands)
- `ImmutableArray<T>` has zero-overhead after construction (no bounds checking)
- GC pressure is generally not a problem for typical compilation workloads

**Location Tracking Performance:**
- `TextSpan` is a lightweight struct (two ints: Start and Length)
- The nullable `TextSpan?` becomes a `Nullable<TextSpan>` (12 bytes on 64-bit)
- Line/column integers (4 ints × 4 bytes = 16 bytes per node)
- Total location overhead: ~28 bytes per node (acceptable for compiler workloads)

**Memory Layout Example** (approximate):
```
Module node allocation:
- Object header: 16 bytes
- Location data: 28 bytes (4 ints + nullable TextSpan)
- Body reference: 8 bytes
- DocString reference: 8 bytes
- ImmutableArray internal: varies with statement count
Total: ~60 bytes + statements
```

**Micro-optimization opportunities** (not currently implemented):
- Use `ArrayPool<T>` for temporary collections during parsing
- Use structs for very small nodes (like `Identifier`), but this complicates the hierarchy
- Implement AST node interning for common patterns (probably not worth the complexity)

---

## 9. Related Files and Further Reading

### Key Related Files:
- **`Statement.cs`**: All statement node types (30+ records) - [See walkthrough](Statement.md)
- **`Expression.cs`**: All expression node types (20+ records) - [See walkthrough](Expression.md)
- **`Types.cs`**: Type annotation nodes (`List[int]`, `Dict[str, str]`, etc.) - [See walkthrough](Types.md)
- **`Parser/Parser.cs`**: Creates these nodes via recursive descent parsing - [See walkthrough](../Parser.md)
- **`Parser/AstDumper.cs`**: Debugging tool that pretty-prints AST - [See walkthrough](../AstDumper.md)
- **`Semantic/SemanticInfo.cs`**: Where type/symbol info is stored (separate from AST)
- **`Text/TextSpan.cs`**: Character offset-based location tracking
- **`Text/ILocatable.cs`**: Interface for source-located elements

### Cross-References in Sharpy.Compiler.Text:
The `Text` namespace contains utilities for source location tracking:
- **`ILocatable`**: Interface implemented by `Node` for uniform location queries
- **`TextSpan`**: Efficient character offset-based spans
- **`SourceText`**: Represents source file contents with line/column mapping

These types work together to enable precise error reporting and IDE features.

### Documentation:
- **Project README**: `README.md` (root) - Architecture overview
- **Semantic Analyzer Architecture**: `docs/architecture/semantic-analyzer-architecture.md`
- **Custom Instructions**: `.github/copilot-instructions.md` - Design philosophy

### Next Steps for Learning:
1. Read `Statement.cs` to see concrete statement types
2. Read `Expression.cs` to see concrete expression types
3. Look at `Parser.cs` method `ParseStatement()` to see how nodes are created
4. Examine `TypeChecker.cs` to see how AST is traversed and validated
5. Study `RoslynEmitter.cs` to see how AST becomes C# code

---

## 10. Quick Reference

### Key Takeaways:
- `Node` is the abstract base for all AST elements
- `Module` represents a complete `.spy` file
- All nodes track source location (line/column)
- AST is immutable (use `init` properties)
- Semantic info lives separately, not in AST nodes
- Records provide value semantics and immutability

### Common Commands:
```bash
# Build compiler:
dotnet build

# Test parser:
dotnet test --filter "FullyQualifiedName~Parser"

# Dump AST for debugging (add to code):
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));

# Compile a .spy file:
dotnet run --project src/Sharpy.Cli -- build file.spy
```

### Pattern Matching Template:
```csharp
void ProcessNode(Node node)
{
    switch (node)
    {
        case Module module:
            foreach (var stmt in module.Body)
                ProcessNode(stmt);
            break;
        
        case FunctionDef funcDef:
            // Handle function
            break;
        
        case ExpressionStatement exprStmt:
            ProcessNode(exprStmt.Expression);
            break;
        
        // ... add cases as needed
        
        default:
            throw new InvalidOperationException($"Unknown node type: {node.GetType()}");
    }
}
```

---

**Welcome to the Sharpy compiler team! Start by exploring the AST structure, and soon you'll be parsing and compiling Python-esque code like a pro.** 🚀
