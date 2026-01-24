# Walkthrough: SourceText.cs

**Source File**: `src/Sharpy.Compiler/Text/SourceText.cs`

---

## Overview

`SourceText` is a foundational utility class that represents source code with efficient line/column lookup capabilities. It sits at the very beginning of the Sharpy compiler pipeline, providing the text abstraction that all other compiler stages (Lexer, Parser, Semantic Analysis) build upon.

**Key responsibilities:**
- Store immutable source text from `.spy` files
- Provide O(log n) conversion between character positions and line/column coordinates
- Enable efficient error reporting with human-readable locations
- Support text extraction via spans

**Pipeline position:** First component — consumed by Lexer and referenced throughout compilation for error diagnostics.

---

## Class Structure

### Main Class: `SourceText`

```csharp
public sealed class SourceText
{
    private readonly string _text;           // The actual source code
    private readonly int[] _lineStarts;      // Precomputed line start positions
    public string? FilePath { get; }         // Optional file path for diagnostics
    public int Length => _text.Length;       // Total character count
    public int LineCount => _lineStarts.Length;  // Total line count
}
```

**Design decisions:**
- **Sealed class**: Cannot be inherited, enabling compiler optimizations
- **Immutable state**: All fields are `readonly`, allowing safe sharing across compilation stages
- **Eager line computation**: Line starts are calculated once in the constructor, trading upfront cost for fast lookups

---

## Key Methods

### 1. Construction

#### `SourceText(string text, string? filePath = null)`

Creates a SourceText from a string.

```csharp
public SourceText(string text, string? filePath = null)
{
    _text = text ?? throw new ArgumentNullException(nameof(text));
    FilePath = filePath;
    _lineStarts = ComputeLineStarts(text);  // Eager preprocessing
}
```

**Important:** Line starts are computed immediately, making construction O(n) in text length.

#### `FromFile(string filePath)` (Static Factory)

Convenience method for loading source from disk.

```csharp
public static SourceText FromFile(string filePath)
{
    var text = File.ReadAllText(filePath);
    return new SourceText(text, filePath);
}
```

**Usage in compiler:** Typically called by CLI or project compiler when loading `.spy` files.

---

### 2. Text Access

#### `char this[int position]` (Indexer)

Direct character access via zero-based position.

```csharp
public char this[int position] => _text[position];
```

**Used by:** Lexer for scanning characters during tokenization.

#### `GetText(TextSpan span)`

Extracts a substring using a span.

```csharp
public string GetText(TextSpan span)
{
    if (span.Start < 0 || span.End > _text.Length)
        throw new ArgumentOutOfRangeException(nameof(span));
    return _text.Substring(span.Start, span.Length);
}
```

**Used by:** Token creation, diagnostic formatting, semantic analysis to extract specific code regions.

---

### 3. Line/Column Conversion (Core Functionality)

#### `GetLineNumber(int position)` — Character Position → Line Number

**Algorithm:** Binary search over `_lineStarts` array.

```csharp
public int GetLineNumber(int position)
{
    // Binary search to find the line containing the position
    int low = 0;
    int high = _lineStarts.Length - 1;

    while (low <= high)
    {
        int mid = low + (high - low) / 2;
        int lineStart = _lineStarts[mid];

        if (position < lineStart)
            high = mid - 1;
        else if (mid + 1 < _lineStarts.Length && position >= _lineStarts[mid + 1])
            low = mid + 1;
        else
            return mid + 1;  // Found! Convert to 1-based
    }

    return _lineStarts.Length;  // Position is on last line
}
```

**Complexity:** O(log n) where n is the number of lines.

**Why binary search?**
- Source files can have thousands of lines
- Error reporting happens frequently during compilation
- Linear scan would be O(n) per lookup, binary search is O(log n)

**Important detail:** Returns 1-based line numbers to match editor conventions.

#### `GetColumnNumber(int position)` — Character Position → Column Number

```csharp
public int GetColumnNumber(int position)
{
    int lineNumber = GetLineNumber(position);           // O(log n)
    int lineStart = _lineStarts[lineNumber - 1];        // Convert back to 0-based
    return position - lineStart + 1;                    // 1-based column
}
```

**Complexity:** O(log n) since it delegates to `GetLineNumber`.

**Note:** Column is calculated as offset from the line start, +1 for 1-based indexing.

#### `GetLineAndColumn(int position)` — Optimized Combined Lookup

```csharp
public (int Line, int Column) GetLineAndColumn(int position)
{
    int line = GetLineNumber(position);
    int lineStart = _lineStarts[line - 1];
    int column = position - lineStart + 1;
    return (line, column);
}
```

**Optimization:** Avoids double binary search by reusing `lineStart` from line lookup.

**Usage:** Primary method for error reporting — provides both line and column in one call.

---

### 4. Reverse Mapping

#### `GetPosition(int line, int column)` — Line/Column → Character Position

```csharp
public int GetPosition(int line, int column)
{
    if (line < 1 || line > _lineStarts.Length)
        throw new ArgumentOutOfRangeException(nameof(line));
    if (column < 1)
        throw new ArgumentOutOfRangeException(nameof(column));

    int lineStart = _lineStarts[line - 1];  // Convert 1-based to 0-based
    int position = lineStart + column - 1;   // 0-based position

    // Clamp to end of text (handles columns beyond line length)
    if (position > _text.Length)
        position = _text.Length;

    return position;
}
```

**Complexity:** O(1) — direct array lookup.

**Clamping behavior:** If column exceeds line length, returns end of text rather than throwing. This makes it safe for tools/IDEs.

---

### 5. Line Extraction

#### `GetLineText(int lineNumber)` — Extract Full Line Without Newline

```csharp
public string GetLineText(int lineNumber)
{
    int lineStart = _lineStarts[lineNumber - 1];
    int lineEnd;

    if (lineNumber < _lineStarts.Length)
    {
        // Not last line: find end by looking at next line start
        lineEnd = _lineStarts[lineNumber];
        // Trim trailing \r\n or \n
        while (lineEnd > lineStart &&
               (_text[lineEnd - 1] == '\n' || _text[lineEnd - 1] == '\r'))
        {
            lineEnd--;
        }
    }
    else
    {
        // Last line: goes to end of text
        lineEnd = _text.Length;
    }

    return _text.Substring(lineStart, lineEnd - lineStart);
}
```

**Usage:** Error reporting, diagnostic displays (e.g., showing context around an error).

**Newline handling:** Strips both `\r\n` (Windows) and `\n` (Unix) from line end.

---

### 6. Line Start Computation (Internal)

#### `ComputeLineStarts(string text)` — Preprocessing Algorithm

```csharp
private static int[] ComputeLineStarts(string text)
{
    var lineStarts = new List<int> { 0 };  // First line always starts at 0

    for (int i = 0; i < text.Length; i++)
    {
        char c = text[i];
        if (c == '\n')
        {
            // Start of next line is after this newline
            lineStarts.Add(i + 1);
        }
        else if (c == '\r')
        {
            // Handle \r\n as single line ending
            if (i + 1 < text.Length && text[i + 1] == '\n')
            {
                lineStarts.Add(i + 2);
                i++;  // Skip the \n
            }
            else
            {
                // Standalone \r (old Mac style)
                lineStarts.Add(i + 1);
            }
        }
    }

    return lineStarts.ToArray();
}
```

**Line ending support:**
- `\n` (Unix/Linux/macOS modern)
- `\r\n` (Windows)
- `\r` (Classic Mac OS, legacy)

**Edge case:** Empty file has one line (the array always starts with `{ 0 }`).

**Example:**
```
Text: "hello\nworld\n"
Positions: h=0, e=1, l=2, l=3, o=4, \n=5, w=6, o=7, r=8, l=9, d=10, \n=11

lineStarts: [0, 6, 12]
Line 1: "hello" (positions 0-5)
Line 2: "world" (positions 6-11)
Line 3: "" (empty line after final \n)
```

---

## Dependencies

### Internal Dependencies

1. **`TextSpan`** (`src/Sharpy.Compiler/Text/TextSpan.cs`)
   - Represents ranges in source text
   - Used by `GetText(TextSpan)` method
   - See [TextSpan.md](./TextSpan.md) for details

### External (Framework) Dependencies

- `System.IO.File` — for `FromFile()` method
- Standard C# collections (`List<int>`, arrays)

### Consumed By

- **Lexer** — uses `SourceText` to scan characters and track positions
- **Parser** — indirectly via tokens that reference positions
- **Semantic Analysis** — for error reporting with line/column info
- **Diagnostics** — uses `GetLineAndColumn()` to format error messages

---

## Design Patterns and Decisions

### 1. Immutability Pattern

All state is immutable after construction. This enables:
- **Thread safety**: Multiple compiler stages can access the same `SourceText`
- **Caching**: Intermediate results can safely reference positions without worrying about text changes
- **Predictability**: No hidden mutations during compilation

### 2. Precomputation Strategy

Line starts are computed once during construction rather than on-demand. This is a classic space-time tradeoff:

**Cost:** O(n) time + O(lines) space upfront
**Benefit:** O(log lines) lookups instead of O(position) for each query

**Justification:** Compilers perform many line/column lookups (every error, every token in some modes), so eager preprocessing pays off.

### 3. 1-Based vs 0-Based Indexing

| Type | Indexing | Rationale |
|------|----------|-----------|
| Character positions | 0-based | Standard for C# strings, easier internal calculations |
| Line numbers | 1-based | Matches editor conventions, user-facing |
| Column numbers | 1-based | Matches editor conventions, user-facing |

**Internal conversion:** Methods like `GetLineNumber` handle conversion at the boundary.

### 4. Defensive Bounds Checking

Almost every public method validates input ranges:
- Prevents crashes from invalid positions
- Provides clear error messages
- Important for robustness when integrating with external tools

---

## Debugging Tips

### 1. Investigating Line/Column Mismatches

If error messages show wrong line/column numbers:

```csharp
// Check what line starts were computed
var sourceText = new SourceText(code);
for (int i = 0; i < sourceText.LineCount; i++)
{
    Console.WriteLine($"Line {i+1} starts at position {sourceText.GetPosition(i+1, 1)}");
}
```

### 2. Verifying Binary Search Logic

The binary search is tricky. To debug issues:

```csharp
// Add logging to GetLineNumber (in a copy):
Console.WriteLine($"Looking for position {position}");
Console.WriteLine($"LineStarts: [{string.Join(", ", _lineStarts)}]");
// ... then step through binary search iterations
```

### 3. Common Pitfalls

**Pitfall 1: Off-by-one in position-to-line conversion**
```csharp
// Position 0 should be line 1, column 1
var (line, col) = sourceText.GetLineAndColumn(0);
Debug.Assert(line == 1 && col == 1);
```

**Pitfall 2: End-of-file position**
```csharp
// Position == text.Length is valid (points just after last character)
int eofPosition = sourceText.Length;
int eofLine = sourceText.GetLineNumber(eofPosition);  // Should work
```

**Pitfall 3: Unicode characters**
```csharp
// SourceText uses string positions, which count UTF-16 code units
// Emoji and some Unicode chars may be 2 code units
// "Hello 😀" has Length=8 not 7
```

---

## Contribution Guidelines

### When to Modify This File

**Add features for:**
- New line ending formats (unlikely but possible)
- Performance optimizations (e.g., caching last lookup)
- Additional text operations needed by compiler stages

**Don't modify for:**
- AST-level changes (those belong in Parser)
- Semantic information (use `SemanticInfo` annotations)
- Token-specific logic (belongs in Lexer)

### Testing Checklist

When modifying `SourceText`, ensure tests cover:
- Empty files
- Single-line files
- Files with all line ending types (`\n`, `\r\n`, `\r`)
- Mixed line endings (Windows file edited on Unix)
- Unicode content (emoji, multi-byte characters)
- Boundary conditions (position 0, position == Length)
- Very long files (performance regression tests)

**Test location:** `src/Sharpy.Compiler.Tests/Text/` (create if doesn't exist)

### Performance Considerations

**Current performance:**
- Construction: O(n) in text length
- Line lookup: O(log lines)
- Column lookup: O(log lines)
- Text extraction: O(span length)

**If you need to optimize:**
1. **Cache last lookup** — 90% of lookups are sequential during lexing
2. **Use `Span<T>`** — for substring operations without allocation
3. **Segment large files** — if supporting multi-gigabyte files

---

## Common Use Cases

### Use Case 1: Error Reporting

```csharp
var sourceText = SourceText.FromFile("example.spy");
int errorPosition = 42;
var (line, column) = sourceText.GetLineAndColumn(errorPosition);
Console.WriteLine($"Error at {sourceText.FilePath}:{line}:{column}");
```

### Use Case 2: Extracting Token Text

```csharp
var span = new TextSpan(start: 10, length: 5);
string tokenText = sourceText.GetText(span);
```

### Use Case 3: Displaying Error Context

```csharp
int errorPosition = 100;
int line = sourceText.GetLineNumber(errorPosition);
string lineText = sourceText.GetLineText(line);
int column = sourceText.GetColumnNumber(errorPosition);

Console.WriteLine(lineText);
Console.WriteLine(new string(' ', column - 1) + "^");  // Point to error
```

Output:
```
    result = calculate(x, y)
                          ^
```

---

## Cross-References

### Related Files

- **[TextSpan.md](./TextSpan.md)** — Companion type for representing text ranges
- **Lexer documentation** — Consumer of `SourceText` for tokenization
- **DiagnosticReporter** (`src/Sharpy.Compiler/Services/DiagnosticReporter.cs`) — Uses line/column info for error formatting

### External Resources

- [Roslyn SourceText](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Text/SourceText.cs) — Inspiration for this design (Sharpy's version is simplified)
- [C# String Indexing](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/strings/) — Understanding UTF-16 code units

---

## Summary

`SourceText` is a deceptively simple class that provides critical infrastructure:
- **Immutable** text storage with optional file path
- **Efficient** O(log n) line/column lookups via binary search
- **Flexible** bidirectional conversion between positions and coordinates
- **Robust** cross-platform line ending support

As a newcomer, focus on understanding:
1. The 0-based vs 1-based indexing conventions
2. How `_lineStarts` enables fast binary search
3. Why immutability matters for compiler architecture

This class rarely needs modification but is referenced throughout the codebase — mastering it will help you understand error reporting and token positioning throughout Sharpy.
