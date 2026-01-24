# Walkthrough: TextSpan.cs

**Source File**: `src/Sharpy.Compiler/Text/TextSpan.cs`

---

## Overview

`TextSpan` is a foundational value type that represents a contiguous span of characters in source code using zero-based character offsets. It's a lightweight, immutable struct designed for efficient memory usage and serves as the primary mechanism for tracking source locations throughout the Sharpy compiler pipeline.

**Role in Compiler Pipeline**: `TextSpan` is used from the **Lexer** stage onwards. Every token, AST node, and diagnostic error references a `TextSpan` to pinpoint exactly where in the source file it originated. This enables:
- Accurate error reporting with line/column information
- Source code highlighting in IDEs
- Code navigation and refactoring tools
- Semantic analysis tied to specific code locations

**Key Design Principle**: TextSpan uses a half-open interval `[Start, End)` where `Start` is inclusive and `End` is exclusive, following standard .NET conventions (similar to `string.Substring`).

---

## Class Structure

### Type Definition

```csharp
public readonly struct TextSpan : IEquatable<TextSpan>
```

**Design Decisions**:
- **`struct`**: Value type ensures zero heap allocation overhead. When passed around the compiler, spans are copied by value rather than by reference, avoiding GC pressure.
- **`readonly`**: Immutable once created, preventing accidental modification and enabling safe sharing across compilation stages.
- **`IEquatable<TextSpan>`**: Provides type-safe equality comparisons and enables efficient use in collections (dictionaries, hash sets).

### Core Properties

```csharp
public int Start { get; }      // Zero-based character offset where span starts
public int Length { get; }     // Number of characters in the span
public int End { get; }        // Exclusive end position (Start + Length)
public bool IsEmpty { get; }   // True if Length == 0
```

**Example**: For source text `"hello world"`:
- `TextSpan(0, 5)` represents `"hello"` → Start=0, Length=5, End=5
- `TextSpan(6, 5)` represents `"world"` → Start=6, Length=5, End=11
- `TextSpan(5, 0)` is an empty span at the space character

---

## Key Methods

### Construction

#### Primary Constructor

```csharp
public TextSpan(int start, int length)
```

**Usage**: Direct construction when you know the length of the span.

**Validation**:
- Throws `ArgumentOutOfRangeException` if `start < 0`
- Throws `ArgumentOutOfRangeException` if `length < 0`

**Example**:
```csharp
var tokenSpan = new TextSpan(position, 3);  // 3-character token
```

#### Factory Method: FromBounds

```csharp
public static TextSpan FromBounds(int start, int end)
```

**Usage**: Create a span when you know the start and end positions (common when parsing).

**Implementation Detail**: Internally converts to `(start, end - start)` constructor form.

**Validation**:
- Throws if `start < 0`
- Throws if `end < start`

**Example from Parser**:
```csharp
// Parser knows where a statement begins and ends
var statementSpan = TextSpan.FromBounds(statementStart, currentPosition);
```

**Why This Exists**: When parsing, you typically track "I started parsing at position X, now I'm at position Y" rather than tracking length. This method eliminates manual `end - start` arithmetic and potential off-by-one errors.

### Spatial Operations

#### Contains (Position)

```csharp
public bool Contains(int position)
```

**Semantics**: Returns `true` if `position >= Start && position < End`.

**Critical Detail**: The end position itself is **NOT** contained (half-open interval).

**Use Case**: Determining if a cursor position falls within a token or AST node for IDE features.

```csharp
var span = new TextSpan(10, 5);  // [10..15)
span.Contains(10);  // true  (start is inclusive)
span.Contains(14);  // true  (last character)
span.Contains(15);  // false (end is exclusive)
```

#### Contains (Span)

```csharp
public bool Contains(TextSpan other)
```

**Semantics**: Returns `true` if this span fully contains another span.

**Implementation**: `other.Start >= Start && other.End <= End`

**Use Case**: Checking if a child AST node is fully contained within a parent node's span.

```csharp
var functionSpan = new TextSpan(0, 100);
var parameterSpan = new TextSpan(10, 20);
functionSpan.Contains(parameterSpan);  // true
```

#### OverlapsWith

```csharp
public bool OverlapsWith(TextSpan other)
```

**Semantics**: Returns `true` if spans share at least one character position.

**Implementation**: `Start < other.End && other.Start < End`

**Why This Works**: This is the standard interval overlap test. Two intervals `[a,b)` and `[c,d)` overlap iff `a < d && c < b`.

**Use Case**: Detecting conflicting edits in code refactoring tools, or finding all nodes that intersect a selection.

```csharp
var span1 = new TextSpan(10, 10);  // [10..20)
var span2 = new TextSpan(15, 10);  // [15..25)
var span3 = new TextSpan(20, 10);  // [20..30)

span1.OverlapsWith(span2);  // true  (overlap at [15..20))
span1.OverlapsWith(span3);  // false (adjacent, but no overlap)
```

### Set Operations

#### Intersection

```csharp
public TextSpan? Intersection(TextSpan other)
```

**Semantics**: Returns the overlapping portion of two spans, or `null` if they don't overlap.

**Algorithm**:
```csharp
int intersectStart = Math.Max(Start, other.Start);
int intersectEnd = Math.Min(End, other.End);
return intersectStart < intersectEnd
    ? FromBounds(intersectStart, intersectEnd)
    : null;
```

**Use Case**: Finding the common portion of two selections or highlights.

**Example**:
```csharp
var span1 = new TextSpan(10, 10);  // [10..20)
var span2 = new TextSpan(15, 10);  // [15..25)
var intersection = span1.Intersection(span2);  // [15..20)
```

#### Union

```csharp
public TextSpan Union(TextSpan other)
```

**Semantics**: Returns the smallest span that contains both spans.

**Critical Detail**: Unlike set union, this **includes the gap** between disjoint spans.

**Algorithm**:
```csharp
int unionStart = Math.Min(Start, other.Start);
int unionEnd = Math.Max(End, other.End);
return FromBounds(unionStart, unionEnd);
```

**Use Case**: Combining spans for error reporting (e.g., "error from here to here"), or tracking the full range of a multi-part construct.

**Example**:
```csharp
var span1 = new TextSpan(10, 5);   // [10..15)
var span2 = new TextSpan(20, 5);   // [20..25)
var union = span1.Union(span2);    // [10..25) - includes gap!
```

**Gotcha**: This is NOT a true set union. If you have `[10..15)` and `[20..25)`, the union is `[10..25)` which includes positions 15-19 that weren't in either original span.

### Equality and Hashing

```csharp
public bool Equals(TextSpan other)
public override bool Equals(object? obj)
public override int GetHashCode()
public static bool operator ==(TextSpan left, TextSpan right)
public static bool operator !=(TextSpan left, TextSpan right)
```

**Implementation**: Spans are equal if both `Start` and `Length` match.

**Hash Code**: Uses `HashCode.Combine(Start, Length)` for efficient, collision-resistant hashing.

**Why This Matters**: Enables using `TextSpan` as dictionary keys or in hash sets for fast lookups (e.g., caching semantic information by source location).

### String Representation

```csharp
public override string ToString()
```

**Format**: `"[{Start}..{End})"` (e.g., `"[10..15)"`)

**Rationale**: The `[start..end)` notation visually reinforces the half-open interval semantics and matches C# range syntax.

---

## Dependencies

### Upstream (TextSpan depends on)

- **System.Math**: For `Min`/`Max` in set operations
- **System.HashCode**: For efficient hash code generation

### Downstream (Components that depend on TextSpan)

1. **`ILocatable` interface** (`Text/ILocatable.cs`):
   - Marker interface requiring `TextSpan? Span { get; }` property
   - Implemented by tokens, AST nodes, and symbols

2. **`SourceText` class** (`Text/SourceText.cs`):
   - Uses `TextSpan` in `GetText(TextSpan span)` to extract source text
   - Converts between TextSpan (character offsets) and line/column numbers

3. **Lexer** (`Lexer/Token.cs`):
   - Every `Token` has a `TextSpan Span` property tracking its location

4. **Parser** (`Parser/Ast/Node.cs`):
   - Base `Node` record implements `ILocatable` with `TextSpan? Span`
   - All AST nodes inherit this capability

5. **Semantic Analysis**:
   - Type checker and validators reference spans for error reporting
   - Semantic info stores span-to-type mappings

6. **Code Generation**:
   - RoslynEmitter uses spans to generate `#line` directives for C# debugging

7. **Diagnostics**:
   - Error messages include span information converted to line/column via `SourceText`

---

## Patterns and Design Decisions

### Value Type for Performance

**Pattern**: Using a `readonly struct` instead of a `class`.

**Rationale**:
- Spans are created frequently (every token, every AST node)
- Heap allocations would create GC pressure
- Value semantics mean no null checking needed
- Copying is cheap (two integers = 8 bytes on 64-bit)

**Trade-off**: Copying can be expensive if the struct were large, but at 8 bytes, it's optimal.

### Immutability

**Pattern**: No setters, all properties are `get`-only, struct is `readonly`.

**Rationale**:
- Compiler data structures are designed to be immutable for thread-safety
- Prevents accidental modification during multi-stage compilation
- Enables safe sharing across threads (future parallelization)

**Alternative Considered**: Mutable struct with setters was rejected due to risk of subtle bugs (struct copies don't share state).

### Half-Open Intervals

**Pattern**: `[Start, End)` where End is exclusive.

**Rationale**:
- Matches .NET conventions (`string.Substring`, array slicing)
- Empty spans are well-defined: `Start == End`
- Adjacent spans don't overlap: `[0..5)` followed by `[5..10)`
- Span length is always `End - Start` (no +1 adjustments)

**Python Note**: Python also uses half-open intervals for slicing (`s[0:5]`), maintaining language similarity.

### Nullable Return for Intersection

**Pattern**: `Intersection` returns `TextSpan?` instead of throwing or returning `Empty`.

**Rationale**:
- `null` clearly indicates "no intersection exists"
- Returning `Empty` would be ambiguous (is it an empty intersection or an actual empty span at some position?)
- Callers can use null-conditional operators: `intersection?.Start`

### Static Empty Sentinel

**Pattern**: `public static readonly TextSpan Empty = new(0, 0)`

**Rationale**:
- Common sentinel value for "no span" or "uninitialized"
- More explicit than relying on `default(TextSpan)` (which happens to be the same)
- Self-documenting in code: `TextSpan.Empty` vs `default`

---

## Debugging Tips

### Common Issues

**1. Off-by-One Errors**

**Symptom**: Span seems to include one extra character or miss the last character.

**Diagnosis**: Remember `End` is **exclusive**. For source `"hello"` at position 0, the span is `[0..5)` not `[0..4)`.

```csharp
var text = "hello";
var span = new TextSpan(0, text.Length);  // Correct: [0..5)
// NOT: new TextSpan(0, text.Length - 1)  // Wrong: [0..4) misses 'o'
```

**2. Empty Span Confusion**

**Symptom**: Empty span behaves unexpectedly in Contains checks.

**Diagnosis**: An empty span `[10..10)` has zero length but exists at position 10. It doesn't contain position 10:

```csharp
var empty = new TextSpan(10, 0);
empty.Contains(10);  // false! End is exclusive
```

**3. Negative Length from FromBounds**

**Symptom**: `ArgumentOutOfRangeException` when calling `FromBounds`.

**Diagnosis**: Check that end >= start. Common mistake is swapping parameters:

```csharp
TextSpan.FromBounds(endPos, startPos);  // Wrong! Will throw
TextSpan.FromBounds(startPos, endPos);  // Correct
```

### Debugging Techniques

**1. ToString() for Visual Inspection**

The `ToString()` output `[Start..End)` is very readable in the debugger watch window.

```csharp
// In debugger, hover over 'span' to see: [10..15)
var span = new TextSpan(10, 5);
```

**2. Extract Source Text for Context**

When debugging span issues, extract the actual text to see what's being referenced:

```csharp
var sourceText = new SourceText("hello world");
var span = new TextSpan(6, 5);
var text = sourceText.GetText(span);  // "world"
```

**3. Check Line/Column Mapping**

If span positions seem wrong, convert to line/column to verify:

```csharp
var (line, col) = sourceText.GetLineAndColumn(span.Start);
Console.WriteLine($"Span starts at line {line}, column {col}");
```

**4. Visualize Overlaps**

When debugging `OverlapsWith` or `Intersection`:

```csharp
Console.WriteLine($"Span1: {span1}");  // [10..20)
Console.WriteLine($"Span2: {span2}");  // [15..25)
Console.WriteLine($"Overlap: {span1.OverlapsWith(span2)}");  // true
Console.WriteLine($"Intersection: {span1.Intersection(span2)}");  // [15..20)
```

---

## Integration with Compiler Pipeline

### Lexer Stage

The **Lexer** creates a `TextSpan` for every token:

```csharp
// Simplified example from Lexer
var token = new Token(
    type: TokenType.Identifier,
    text: "variable",
    span: new TextSpan(currentPosition, 8)
);
```

### Parser Stage

The **Parser** combines token spans to create AST node spans:

```csharp
// Simplified example from Parser
var functionDef = new FunctionDef
{
    Name = "foo",
    // Span from 'def' keyword to end of function body
    Span = TextSpan.FromBounds(defKeywordToken.Span.Start, lastToken.Span.End)
};
```

### Semantic Analysis

The **TypeChecker** stores spans for error reporting:

```csharp
// Simplified example from TypeChecker
if (!isCompatible)
{
    diagnostics.Add(new Diagnostic(
        message: "Type mismatch",
        span: expression.Span,  // Points to the problematic expression
        severity: DiagnosticSeverity.Error
    ));
}
```

### Error Reporting

**Diagnostics** convert spans to human-readable locations:

```csharp
// Simplified error reporting
var (line, column) = sourceText.GetLineAndColumn(diagnostic.Span.Start);
Console.Error.WriteLine($"Error at line {line}, column {column}: {diagnostic.Message}");
Console.Error.WriteLine(sourceText.GetLineText(line));
Console.Error.WriteLine(new string(' ', column - 1) + new string('^', diagnostic.Span.Length));
```

**Output**:
```
Error at line 5, column 10: Type mismatch
    x = "hello" + 42
              ^^^^^^^
```

---

## Contribution Guidelines

### When to Modify TextSpan

**Rarely**. This is a foundational type that's heavily used. Changes require careful consideration.

**Valid Reasons to Modify**:
1. **Performance optimization**: E.g., if profiling shows hashing is a bottleneck
2. **New spatial operation**: Adding a legitimate missing operation (e.g., `Adjacent()` to check if spans are next to each other)
3. **Bug fix**: Correcting logic errors in existing operations

**Invalid Reasons**:
1. Adding domain-specific operations (those belong in helper classes)
2. Adding mutable state (breaks immutability contract)
3. Adding formatting or conversion logic (belongs in SourceText or Diagnostic classes)

### How to Add New Operations

If you need a new span operation:

1. **Check if it can be composed from existing operations**:
   ```csharp
   // Don't add Adjacent() method if you can do:
   bool adjacent = span1.End == span2.Start || span2.End == span1.Start;
   ```

2. **Consider adding an extension method** instead:
   ```csharp
   // In a separate TextSpanExtensions.cs file
   public static class TextSpanExtensions
   {
       public static bool IsAdjacentTo(this TextSpan span, TextSpan other)
       {
           return span.End == other.Start || other.End == span.Start;
       }
   }
   ```

3. **If adding to TextSpan itself**, follow the pattern:
   - Add XML documentation
   - Add validation if needed
   - Add comprehensive unit tests in `TextSpanTests.cs`
   - Update this walkthrough document

### Testing Requirements

**All changes to TextSpan must include tests**:

1. **Normal cases**: Typical usage scenarios
2. **Edge cases**: Empty spans, zero-length spans, adjacent spans
3. **Error cases**: Negative values, invalid bounds
4. **Boundary conditions**: Start and End positions, inclusive/exclusive behavior

**Example test structure**:
```csharp
[Fact]
public void NewOperation_NormalCase_ReturnsExpected()
{
    // Arrange
    var span1 = new TextSpan(10, 5);
    var span2 = new TextSpan(20, 5);

    // Act
    var result = span1.NewOperation(span2);

    // Assert
    Assert.Equal(expectedValue, result);
}
```

### Performance Considerations

**TextSpan is on the hot path** - it's created millions of times during compilation.

**Guidelines**:
- Avoid allocations (no new objects, no boxing)
- Keep methods small and inline-able
- Avoid virtual calls
- Prefer simple arithmetic over complex logic

**Example - Good**:
```csharp
public bool Contains(int position) => position >= Start && position < End;
```

**Example - Bad**:
```csharp
public bool Contains(int position)
{
    var range = Enumerable.Range(Start, Length);  // ALLOCATION!
    return range.Contains(position);
}
```

---

## Cross-References

### Related Documentation

- **[SourceText.md](./SourceText.md)** - Works with TextSpan to provide line/column mapping
- **[ILocatable.md](./ILocatable.md)** - Interface that requires TextSpan property
- **[Token walkthrough](../../Lexer/Token.md)** - Lexer tokens use TextSpan for location tracking
- **[Node walkthrough](../../Parser/Ast/Node.md)** - AST nodes implement ILocatable with TextSpan

### Related Source Files

- `src/Sharpy.Compiler/Text/SourceText.cs` - Converts spans to line/column numbers
- `src/Sharpy.Compiler/Text/ILocatable.cs` - Interface requiring TextSpan property
- `src/Sharpy.Compiler/Lexer/Token.cs` - Tokens have TextSpan Span property
- `src/Sharpy.Compiler/Parser/Ast/Node.cs` - Base AST node with TextSpan? Span

### Related Tests

- `src/Sharpy.Compiler.Tests/Text/TextSpanTests.cs` - Comprehensive unit tests
- `src/Sharpy.Compiler.Tests/Parser/ParserSpanTests.cs` - Tests span tracking in parsed AST

---

## Summary

`TextSpan` is a simple but critical value type that efficiently represents source locations using character offsets. Its immutable, value-type design ensures zero-allocation overhead across the compiler pipeline. The half-open interval semantics `[Start, End)` align with .NET conventions and prevent off-by-one errors.

**Key Takeaways**:
1. **Zero-based character offsets**: Not line/column numbers (that's SourceText's job)
2. **Half-open interval**: End is exclusive
3. **Value type**: Cheap to copy, no heap allocation
4. **Immutable**: Safe to share across compilation stages
5. **Foundational**: Used by every token, AST node, and diagnostic

Understanding `TextSpan` is essential for working with any part of the compiler that deals with source locations - which is virtually everything from the Lexer onwards.
