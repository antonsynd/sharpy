# Walkthrough: ILocatable.cs

**Source File**: `src/Sharpy.Compiler/Text/ILocatable.cs`

---

## 1. Overview

`ILocatable.cs` defines a simple but critical interface that provides a unified contract for tracking source code locations throughout the Sharpy compiler. This interface is implemented by all major compiler artifacts that correspond to source code positions: AST nodes, lexer tokens, and semantic symbols.

**Key Responsibilities:**
- Establishes a standard way to query source location information across the compiler
- Enables uniform error reporting, IDE features, and debugging capabilities
- Provides abstraction over location tracking, allowing implementers flexibility in how they store location data

**Role in the Compiler Pipeline:**
```
Source Code → Lexer (Tokens) → Parser (AST) → Semantic Analysis → Code Generation
               ↓ ILocatable     ↓ ILocatable    ↓ ILocatable
               Token            Node            Symbol
```

`ILocatable` is a horizontal concern that spans the entire pipeline—any compiler artifact that has a meaningful source location implements this interface.

---

## 2. Interface Structure

### 2.1 The `ILocatable` Interface

```csharp
public interface ILocatable
{
    /// <summary>
    /// The span of this element in the source text.
    /// May be null if location is not tracked.
    /// </summary>
    TextSpan? Span { get; }
}
```

**Design Philosophy:**

This interface is intentionally **minimal**—it contains just one property. This simplicity is by design:

- **Single Responsibility**: The interface has one job—provide a source span
- **No Coupling**: It doesn't depend on specific AST or token types
- **Flexibility**: The nullable `TextSpan?` allows implementers to opt out of location tracking when needed
- **Future-Proof**: Additional location metadata can be added to `TextSpan` without breaking the interface

### 2.2 The `TextSpan` Type

`TextSpan` is a value type (struct) defined in `src/Sharpy.Compiler/Text/TextSpan.cs` that represents a contiguous range of characters in source code using **zero-based character offsets**.

**Key Properties:**
```csharp
public readonly struct TextSpan
{
    public int Start { get; }    // Zero-based character offset where span starts
    public int Length { get; }   // Number of characters in the span
    public int End => Start + Length;  // Exclusive end position
}
```

**Example:**
```python
# Source: "hello world"
# TextSpan(0, 5) represents "hello"
# TextSpan(6, 5) represents "world"
```

**Why Character Offsets Instead of Line/Column?**

Character offsets provide several advantages:
- **Efficient range queries**: "Give me all tokens between positions 100-200"
- **Simple arithmetic**: Combining spans is just min/max operations
- **No line-ending ambiguity**: Works consistently across `\n`, `\r\n`, or `\r`
- **IDE-friendly**: Most editors internally use character offsets

That said, line/column coordinates are still maintained separately (e.g., in `Node.LineStart`, `Node.ColumnStart`) for human-readable error messages.

### 2.3 Why Nullable?

The `TextSpan?` property is nullable for backward compatibility and flexibility:

```csharp
TextSpan? Span { get; }
```

**Scenarios where `Span` might be null:**
1. **Backward compatibility**: Existing code that predates `TextSpan` tracking
2. **Synthetic nodes**: Compiler-generated AST nodes that don't correspond to source code
3. **Performance optimization**: Skipping location tracking in hot paths where it's not needed
4. **Testing**: Unit tests can create simplified nodes without location data

**Best Practice**: Always null-check before using:
```csharp
if (node.Span.HasValue)
{
    var span = node.Span.Value;
    // Use span.Start, span.Length, etc.
}
```

---

## 3. Key Implementers

`ILocatable` is implemented by three major compiler types:

### 3.1 Lexer Tokens (`Token` struct)

**Location**: `src/Sharpy.Compiler/Lexer/Token.cs`

```csharp
public readonly struct Token : ILocatable
{
    public TokenType Type { get; }
    public string Text { get; }
    public TextSpan? Span { get; }
    // ...
}
```

**Usage**: Tokens track the exact character range they occupy in source text. This enables:
- Precise syntax error messages: `"Unexpected token '+' at line 5:10"`
- Syntax highlighting in IDEs
- Go-to-definition for identifiers

### 3.2 AST Nodes (`Node` abstract record)

**Location**: `src/Sharpy.Compiler/Parser/Ast/Node.cs`

```csharp
public abstract record Node : ILocatable
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public TextSpan? Span { get; init; }
}
```

**Dual Tracking**: AST nodes maintain both line/column (for error messages) and `TextSpan` (for efficient queries). This is a pragmatic compromise between human readability and programmatic efficiency.

**All AST nodes inherit from `Node`**, including:
- `Module` (root of the AST)
- `FunctionDef`, `ClassDef`, `StructDef`
- `BinaryOp`, `UnaryOp`, `Call`, `Attribute`
- `IfStatement`, `WhileStatement`, `ForStatement`

### 3.3 Semantic Symbols (Future)

**Expected Location**: `src/Sharpy.Compiler/Semantic/Symbol.cs` (not yet fully implemented)

Semantic symbols (variable declarations, type definitions, imports) will implement `ILocatable` to enable:
- "Find all references" in IDEs
- Renaming variables across files
- Navigation from usage to definition

---

## 4. Dependencies

### 4.1 Direct Dependencies

**Depends On:**
- `TextSpan` struct (`src/Sharpy.Compiler/Text/TextSpan.cs`)

**No Other Dependencies**: The interface is intentionally isolated to avoid circular dependencies.

### 4.2 Consumers (Who Uses ILocatable?)

The following components accept or work with `ILocatable` implementations:

1. **Error Reporting** (Future):
   ```csharp
   void ReportError(string message, ILocatable location)
   {
       if (location.Span.HasValue)
       {
           // Use location.Span to highlight error in source
       }
   }
   ```

2. **AST Visitors**: When traversing the AST, visitors can track location context
3. **Code Generation**: `RoslynEmitter` uses location data for generating C# `#line` directives
4. **Diagnostic Tools**: AST dumpers, pretty-printers, and debuggers

---

## 5. Patterns and Design Decisions

### 5.1 Marker Interface Pattern

`ILocatable` is a **marker interface**—it marks types as having location information without imposing implementation details. This allows:
- **Uniform Handling**: Generic code can accept `ILocatable` and query location
- **Type Safety**: Compiler checks that you're only querying location on types that support it
- **Documentation**: The interface serves as self-documenting API

### 5.2 Separation of Concerns

Notice that `ILocatable` lives in `Sharpy.Compiler.Text` namespace, not `Sharpy.Compiler.Parser.Ast` or `Sharpy.Compiler.Lexer`. This is intentional:

```
Sharpy.Compiler.Text/          ← Foundation layer (no dependencies)
├── TextSpan.cs
└── ILocatable.cs

Sharpy.Compiler.Lexer/         ← Depends on Text
└── Token.cs : ILocatable

Sharpy.Compiler.Parser.Ast/    ← Depends on Text
└── Node.cs : ILocatable
```

By keeping `ILocatable` in a low-level namespace, both the lexer and parser can implement it without creating circular dependencies.

### 5.3 Value vs. Reference Semantics

- `ILocatable` is an **interface** (reference type)
- `TextSpan` is a **struct** (value type)

**Why?**
- `TextSpan` is small (two integers) and benefits from stack allocation
- `ILocatable` provides polymorphic behavior across different implementers
- This combination gives both efficiency (structs) and flexibility (interfaces)

### 5.4 Immutability by Convention

The interface defines `Span` as a read-only property (`{ get; }`), enforcing immutability at the interface level. Implementers should also make location data immutable:

```csharp
// Good - immutable with init
public TextSpan? Span { get; init; }

// Bad - mutable with set
public TextSpan? Span { get; set; }  // Violates AST immutability principle
```

---

## 6. Debugging Tips

### 6.1 Visualizing Spans

When debugging, it's helpful to see the actual text a span covers:

```csharp
void DebugPrintSpan(ILocatable locatable, string sourceText)
{
    if (locatable.Span.HasValue)
    {
        var span = locatable.Span.Value;
        var text = sourceText.Substring(span.Start, span.Length);
        Console.WriteLine($"Span: {span} -> \"{text}\"");
    }
    else
    {
        Console.WriteLine("Span: (not tracked)");
    }
}
```

### 6.2 Common Pitfalls

**Pitfall 1: Null Reference Exceptions**
```csharp
// Bad - will throw if Span is null
var start = node.Span.Value.Start;  // ❌ NullReferenceException

// Good - defensive check
if (node.Span.HasValue)
{
    var start = node.Span.Value.Start;  // ✅ Safe
}
```

**Pitfall 2: Off-by-One Errors**
```csharp
// TextSpan.End is EXCLUSIVE (like C# ranges)
// To get the last character position, use End - 1
var span = new TextSpan(0, 5);  // Covers positions 0, 1, 2, 3, 4
Assert.Equal(5, span.End);      // End is 5 (exclusive)
```

**Pitfall 3: Confusing Line/Column with Character Offsets**
```csharp
// These are DIFFERENT coordinate systems!
node.LineStart     // 1-based line number (e.g., 5)
node.Span?.Start   // 0-based character offset (e.g., 142)
```

### 6.3 Debugging Missing Spans

If you're getting `null` spans when you expect them, check:

1. **Parser initialization**: Is the parser creating `TextSpan` instances?
2. **Token creation**: Does the lexer set `Span` on tokens?
3. **AST node construction**: Are you passing `Span` through when building nodes?

Search for `new TextSpan(` or `Span = ` to trace where spans are created.

---

## 7. Contribution Guidelines

### 7.1 When to Implement ILocatable

**Do implement `ILocatable` when:**
- Your type represents something that appears in source code
- Users might want to know "where did this come from?"
- Error messages might reference this type

**Don't implement `ILocatable` when:**
- Your type is purely internal (e.g., a caching data structure)
- It has no meaningful source location (e.g., compiler-generated metadata)

### 7.2 Extending the Interface

**Scenario**: You want to add a new method to `ILocatable`, like `GetLineColumn()`.

**DON'T extend the interface directly**—this would break all existing implementers. Instead:

**Option 1: Extension Method (Recommended)**
```csharp
public static class ILocatableExtensions
{
    public static (int Line, int Column)? GetLineColumn(this ILocatable loc, string source)
    {
        // Implementation using loc.Span
    }
}
```

**Option 2: Helper Class**
```csharp
public static class LocationHelper
{
    public static (int Line, int Column) SpanToLineColumn(TextSpan span, string source)
    {
        // Implementation
    }
}
```

### 7.3 Common Changes

**Types of changes you might make to this file:**

1. **Documentation updates**: Clarifying the contract or usage patterns
2. **Adding constraints**: E.g., marking the interface as `IEquatable` if needed
3. **Performance optimization notes**: Adding XML doc comments about efficiency

**Changes you should NOT make:**
- Adding new required members (breaks implementers)
- Changing `TextSpan?` to `TextSpan` (breaks backward compatibility)
- Moving the file to a different namespace (breaks all imports)

### 7.4 Testing Changes

If you modify `ILocatable` or `TextSpan`, verify:

1. **Lexer tests**: `dotnet test --filter "FullyQualifiedName~Lexer"`
2. **Parser tests**: `dotnet test --filter "FullyQualifiedName~Parser"`
3. **Error reporting**: Check that error messages still show correct locations
4. **Build**: Ensure no compiler errors across the entire solution

---

## 8. Cross-References

### 8.1 Related Files

**Core Dependencies:**
- [`TextSpan.cs`](./TextSpan.md) - The value type that represents character ranges
- [`Node.cs`](../Parser/Ast/Node.md) - Base AST node that implements `ILocatable`
- [`Token.cs`](../Lexer/Token.md) - Lexer token that implements `ILocatable`

**Usage Examples:**
- [`Parser.cs`](../Parser/Parser.md) - Creates AST nodes with location information
- [`Lexer.cs`](../Lexer/Lexer.md) - Creates tokens with location information
- [`DiagnosticBag.cs`](../Diagnostics/DiagnosticBag.md) - May use `ILocatable` for error reporting (future)

### 8.2 Language Specification

**Relevant Spec Sections:**
- The language specification doesn't directly address `ILocatable` (it's an implementation detail)
- However, error reporting requirements imply the need for location tracking

### 8.3 External References

**Inspiration from other compilers:**
- **Roslyn (C# compiler)**: Uses `SyntaxNode.Span` (similar concept)
- **TypeScript compiler**: Uses `Node.pos` and `Node.end` (character offsets)
- **Python's AST module**: Uses `lineno`, `col_offset`, `end_lineno`, `end_col_offset`

Sharpy's design combines the efficiency of character offsets (Roslyn/TypeScript) with the human readability of line/column coordinates (Python).

---

## 9. Architectural Context

### 9.1 Why This Matters

`ILocatable` is a foundational abstraction that enables:

1. **Quality Error Messages**: Users see exactly where problems occur
2. **IDE Integration**: Future language server can provide rich editing features
3. **Debugging**: Developers can trace compiler behavior back to source
4. **Testing**: Assertion failures can point to exact code locations

### 9.2 Future Enhancements

Potential improvements to location tracking:

1. **Source File Association**: Track which file a span belongs to (for multi-file projects)
2. **Virtual Locations**: Support for macro expansion or generated code
3. **Range Adjustments**: Track how code transformations affect locations
4. **Precise Trivia Tracking**: Include whitespace and comments in location data

### 9.3 Performance Considerations

**Memory Footprint:**
- `TextSpan` is 8 bytes (two `int` fields)
- Making it nullable adds 1 byte overhead per instance
- For a 10,000-node AST, this adds ~90KB (negligible)

**CPU Cost:**
- Reading a `TextSpan?` involves a null check (1 CPU instruction)
- The nullable overhead is insignificant compared to AST traversal

**Verdict**: The flexibility of nullable spans far outweighs the tiny performance cost.

---

## Summary

`ILocatable` is a simple but powerful abstraction that unifies location tracking across the Sharpy compiler. By implementing this interface, AST nodes, tokens, and symbols gain the ability to answer the question: "Where in the source code did this come from?"

**Key Takeaways:**
- **Minimal by design**: Just one property keeps the interface flexible
- **Nullable for flexibility**: Optional tracking supports backward compatibility and synthetic nodes
- **Character offsets over line/column**: Efficient for programmatic operations (though AST nodes track both)
- **Foundation for quality tooling**: Enables great error messages and IDE features

When in doubt, remember: if something appears in source code, it should probably implement `ILocatable`.
