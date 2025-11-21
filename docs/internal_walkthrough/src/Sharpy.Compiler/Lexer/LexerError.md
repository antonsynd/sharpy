# Walkthrough: LexerError.cs

**Source File**: `src/Sharpy.Compiler/Lexer/LexerError.cs`

---

## 1. Overview

`LexerError.cs` defines a simple but critical exception class used throughout the Sharpy compiler's lexical analysis (tokenization) phase. When the lexer encounters invalid syntax at the character/token level, it throws a `LexerError` to halt compilation and report the precise location of the problem to the user.

**Role in the project:**
- **Error Reporting**: Provides structured error messages with line and column information
- **Fail-Fast Behavior**: Stops lexical analysis immediately when encountering invalid syntax
- **User Experience**: Helps developers quickly locate syntax errors in their Sharpy source code
- **Compiler Pipeline**: Caught by higher-level components (`Compiler.cs`) to format errors for display

This is one of the foundational error types in the compiler, alongside `ParserError` (for syntactic errors) and `SemanticException` (for type/semantic errors).

---

## 2. Class/Type Structure

### `LexerError` Class

```csharp
public class LexerError : Exception
{
    public int Line { get; }
    public int Column { get; }
    
    public LexerError(string message, int line, int column)
        : base($"Lexer error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
    }
}
```

**Inheritance:**
- Inherits from `System.Exception`, making it catchable with standard exception handling
- Can be caught specifically as `LexerError` or generically as `Exception`

**Properties:**
- **`Line`** (int, read-only): The line number where the error occurred (1-indexed)
- **`Column`** (int, read-only): The column number where the error occurred (1-indexed)

**Design Choice**: Simple, focused exception class with minimal overhead. It stores location information and formats a helpful message for the base `Exception.Message` property.

---

## 3. Key Functions/Methods

### Constructor

```csharp
public LexerError(string message, int line, int column)
    : base($"Lexer error at line {line}, column {column}: {message}")
{
    Line = line;
    Column = column;
}
```

**Purpose:**
Creates a new lexer error with location information and a descriptive message.

**Parameters:**
- **`message`** (string): Human-readable description of what went wrong (e.g., "Unterminated string literal", "Invalid hex escape sequence")
- **`line`** (int): The line number in the source file where the error occurred
- **`column`** (int): The column number on that line where the error occurred

**Implementation Details:**
1. Calls base exception constructor with a formatted string that includes location
2. The format is: `"Lexer error at line {line}, column {column}: {message}"`
3. Stores `line` and `column` as properties for programmatic access
4. This dual approach allows both human-friendly messages (via `Message` property) and structured error handling (via `Line`/`Column` properties)

**Example Error Messages:**
- `"Lexer error at line 5, column 12: Unterminated string literal"`
- `"Lexer error at line 10, column 1: Tabs are not allowed for indentation. Use 4 spaces."`
- `"Lexer error at line 23, column 7: Invalid hex escape sequence: \xZZ"`

---

## 4. Dependencies

### Internal Dependencies

**Direct Usage:**
- **`Lexer.cs`**: The primary consumer - throws `LexerError` 50+ times throughout tokenization
- **`Compiler.cs`**: Catches `LexerError` to format error messages for compilation reports
- **`Integration/IntegrationTestBase.cs`**: Catches and handles lexer errors during integration testing

**No External Dependencies:**
- Only depends on `System.Exception` from .NET BCL
- No dependencies on other Sharpy.Compiler types
- Completely self-contained

### Sibling Error Types

The Sharpy compiler uses a layered error system:

1. **`LexerError`** (this file) - Character/token-level errors
2. **`ParserError`** - Syntactic/grammar errors (wrong token sequences)
3. **`SemanticException`** - Type checking and semantic errors
4. **`CompilationException`** - Higher-level compilation failures

Each error type corresponds to a phase in the compilation pipeline:
```
Source Code → [Lexer → LexerError] → [Parser → ParserError] → [Semantic → SemanticException] → Assembly
```

---

## 5. Patterns and Design Decisions

### Design Pattern: Exception-Based Error Handling

**Why exceptions instead of result types?**
- Sharpy's compiler uses a **fail-fast** approach for syntax errors
- When a lexer error occurs, there's no point continuing tokenization
- Exceptions provide natural control flow for unrecoverable errors
- Stack unwinding automatically cleans up partial state

**Alternative considered:** Returning `Result<Token[], LexerError[]>` would allow collecting multiple errors, but:
- Lexer errors often make subsequent tokens meaningless
- Complicates the lexer's internal state management
- User experience: showing one error at a time is clearer for syntax issues

### Immutability

The `Line` and `Column` properties are **read-only** (get-only):
- Set in constructor only
- Cannot be modified after creation
- Prevents accidental mutation during error handling
- Makes error objects safe to pass around and log

### Informative Error Messages

The constructor automatically formats messages with location information:
```csharp
base($"Lexer error at line {line}, column {column}: {message}")
```

**Benefits:**
- Consistent format across all lexer errors
- Users always get line/column information in error messages
- IDEs can parse the format to jump to error locations
- CLI tools can display errors in standard compiler error format

**Example in practice:**
```bash
$ sharpyc build myfile.spy
myfile.spy(5,12): error: Lexer error at line 5, column 12: Unterminated string literal
```

### Simplicity Over Features

This class is intentionally minimal:
- No error codes or categories
- No recovery suggestions
- No inner exceptions
- No serialization support

**Rationale:** Keep it simple. Complex error reporting features can be added at higher levels (e.g., in the CLI or IDE integration) without cluttering the core error type.

---

## 6. Debugging Tips

### When You Encounter a LexerError

**1. Read the Error Message Carefully**
The message tells you exactly what the lexer expected vs. what it found:
```
Lexer error at line 23, column 7: Invalid hex escape sequence: \xZZ
                       ^^^^^^^^^^^
                       Go to this location in your source file
```

**2. Common LexerError Categories**

| Error Message Pattern | Likely Cause |
|----------------------|--------------|
| "Unterminated string literal" | Missing closing quote: `"hello` |
| "Unterminated f-string" | Missing closing quote in f-string: `f"hello {name}` |
| "Invalid hex escape sequence" | Bad escape code: `"\xZZ"` (not valid hex) |
| "Tabs are not allowed" | Used tabs instead of spaces for indentation |
| "Indentation must be multiple of 4" | Indented with 2 or 3 spaces instead of 4 |
| "Invalid binary literal: no digits after 0b" | Wrote `0b` without following digits |
| "Unexpected character: 'X'" | Used a character not in Sharpy's syntax (e.g., `$`, `@` outside strings) |

**3. Use Line and Column Properties**

When debugging in tests or catching errors programmatically:
```csharp
try
{
    var tokens = lexer.Tokenize();
}
catch (LexerError ex)
{
    Console.WriteLine($"Error at {ex.Line}:{ex.Column}");
    Console.WriteLine($"Message: {ex.Message}");
    
    // You can also access the source line if you kept it:
    var sourceLine = sourceCode.Split('\n')[ex.Line - 1];
    Console.WriteLine($"Source: {sourceLine}");
    Console.WriteLine($"        {new string(' ', ex.Column - 1)}^");
}
```

**4. Check the Lexer Source**

When investigating why a particular error occurred, search `Lexer.cs` for the error message:
```bash
grep -n "Unterminated string literal" src/Sharpy.Compiler/Lexer/Lexer.cs
```

This shows you the exact lexer logic that triggered the error, helping you understand:
- What characters led to this state
- What the lexer was expecting
- How to fix your source code

**5. Test Coverage**

The lexer error handling is extensively tested. To see examples of what triggers each error:
```bash
# See all lexer error test cases
grep -A 3 "Throw<LexerError>" src/Sharpy.Compiler.Tests/Lexer/*.cs
```

These tests serve as documentation for what inputs cause errors.

---

## 7. Contribution Guidelines

### When to Add New LexerError Throws

**Add a new `throw new LexerError(...)` when:**
- The lexer encounters syntactically invalid input at the token level
- The error is unrecoverable (can't produce valid tokens)
- The error relates to character-level or token-level syntax (not grammar)

**Examples:**
- ✅ Invalid escape sequence in a string
- ✅ Unterminated comment or string literal
- ✅ Invalid numeric literal format
- ✅ Character not recognized by the lexer
- ❌ Function defined without a name → Use `ParserError` (grammar issue)
- ❌ Type mismatch → Use `SemanticException` (semantic issue)

### Writing Good Error Messages

**Guidelines:**
1. **Be specific**: Tell the user exactly what's wrong
   - ❌ "Invalid syntax"
   - ✅ "Invalid hex escape sequence: \xZZ"

2. **Be prescriptive when possible**: Suggest how to fix it
   - ❌ "Tabs found"
   - ✅ "Tabs are not allowed for indentation. Use 4 spaces."

3. **Include context**: Show the problematic input when helpful
   - ❌ "Invalid numeric suffix"
   - ✅ "Invalid numeric suffix: 'f32' (did you mean 'f'?)"

4. **Keep it concise**: Users read these under stress; be brief
   - ❌ "The lexer has encountered an unexpected character that is not part of the Sharpy language specification..."
   - ✅ "Unexpected character: '@'"

### Testing Your Changes

**When adding a new error condition:**

1. **Add a positive test** (valid input that should work):
```csharp
[Fact]
public void Tokenize_ValidHexEscape_Succeeds()
{
    var source = @"s = ""\x41""";  // ASCII 'A'
    var tokens = new Lexer(source).Tokenize();
    // Assert token is correct...
}
```

2. **Add a negative test** (invalid input that should throw):
```csharp
[Fact]
public void Tokenize_InvalidHexEscape_ThrowsLexerError()
{
    var source = @"s = ""\xZZ""";  // Invalid hex digits
    var act = () => new Lexer(source).Tokenize();
    act.Should().Throw<LexerError>()
        .WithMessage("*Invalid hex escape sequence*");
}
```

**Test file locations:**
- `src/Sharpy.Compiler.Tests/Lexer/LexerTests.cs` - Main lexer tests
- `src/Sharpy.Compiler.Tests/Lexer/LexerNegativeTests.cs` - Error condition tests
- `src/Sharpy.Compiler.Tests/Lexer/LexerEdgeCaseTests.cs` - Edge cases

### Modifying LexerError Itself

**This file rarely needs changes.** It's intentionally simple and stable.

**When you MIGHT modify it:**
- ❓ Adding error recovery hints (e.g., `SuggestedFix` property)
- ❓ Adding error codes for categorization
- ❓ Supporting multiple error locations (e.g., "unclosed delimiter opened at line X")

**When you should NOT modify it:**
- ❌ Making `Line` or `Column` mutable
- ❌ Adding complex formatting logic (do that in `Compiler.cs` instead)
- ❌ Adding language-specific details (keep it general)

**Alternative:** If you need richer error reporting, consider:
- Creating a wrapper type that contains `LexerError` plus additional context
- Extending error formatting in `Compiler.cs` or the CLI layer
- Using diagnostic reporting systems for IDE integration

### Code Review Checklist

When reviewing changes involving `LexerError`:

- [ ] Error message is clear and specific
- [ ] Line and column are correctly tracked from lexer state
- [ ] Error is thrown at the right point (not too early, not too late)
- [ ] Test coverage includes both the error case and the fixed version
- [ ] Error message follows established patterns (see section 6.2 above)
- [ ] Documentation updated if error represents new language restriction

---

## 8. Real-World Examples

### Example 1: Unterminated String

**Source Code:**
```python
message = "Hello, world!
print(message)
```

**What Happens:**
1. Lexer encounters `"Hello, world!`
2. Scans until end of line looking for closing `"`
3. Reaches newline without finding it
4. Throws: `new LexerError("Unterminated string literal", 1, 11)`

**Error Displayed:**
```
myfile.spy(1,11): error: Lexer error at line 1, column 11: Unterminated string literal
```

**Fix:**
```python
message = "Hello, world!"  # Added closing quote
print(message)
```

### Example 2: Invalid Indentation

**Source Code:**
```python
def greet(name: str) -> None:
  print(f"Hello, {name}")  # Only 2 spaces (should be 4)
```

**What Happens:**
1. Lexer processes function definition
2. On next line, counts 2 spaces of indentation
3. Detects `2 % 4 != 0` (not a multiple of 4)
4. Throws: `new LexerError("Indentation must be multiple of 4 spaces (found 2)", 2, 1)`

**Error Displayed:**
```
myfile.spy(2,1): error: Lexer error at line 2, column 1: Indentation must be multiple of 4 spaces (found 2)
```

**Fix:**
```python
def greet(name: str) -> None:
    print(f"Hello, {name}")  # 4 spaces
```

### Example 3: Invalid Numeric Literal

**Source Code:**
```python
x = 0b  # Binary literal without digits
```

**What Happens:**
1. Lexer sees `0b` prefix for binary literal
2. Attempts to read binary digits (0 or 1)
3. Finds end of token without any digits
4. Throws: `new LexerError("Invalid binary literal: no digits after 0b", 1, 5)`

**Error Displayed:**
```
myfile.spy(1,5): error: Lexer error at line 1, column 5: Invalid binary literal: no digits after 0b
```

**Fix:**
```python
x = 0b1010  # Binary for 10
```

---

## 9. Integration with Compiler Pipeline

### Error Handling Flow

```
┌─────────────┐
│ Source Code │
└──────┬──────┘
       │
       ▼
┌─────────────┐     throws      ┌────────────┐
│   Lexer     │────────────────▶│ LexerError │
└──────┬──────┘                 └─────┬──────┘
       │                              │
       │ tokens                       │ caught
       ▼                              │
┌─────────────┐                       │
│   Parser    │                       │
└──────┬──────┘                       │
       │                              │
       ▼                              │
┌─────────────┐                       │
│  Compiler   │◀──────────────────────┘
│  (catches)  │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ Format &    │
│ Display     │
└─────────────┘
```

### How Compiler.cs Handles LexerError

From `src/Sharpy.Compiler/Compiler.cs`:

```csharp
catch (LexerError ex)
{
    // Format as: filename(line,column): error: message
    allErrors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
    projectMetrics.AddFileMetrics(fileMetrics);
}
```

**Key Points:**
- Error is caught at the file compilation level
- Formatted into standard compiler error format: `file(line,col): error: message`
- Added to `allErrors` collection for batch reporting
- Compilation continues with next file (in multi-file projects)
- Metrics are still recorded even when errors occur

### Integration Testing

From `src/Sharpy.Compiler.Tests/Integration/IntegrationTestBase.cs`:

```csharp
catch (LexerError ex)
{
    // Test infrastructure can inspect the error
    Assert.Fail($"Lexer error: {ex.Message} at {ex.Line}:{ex.Column}");
}
```

Tests can verify:
- Correct line/column numbers
- Expected error messages
- That certain inputs do/don't throw errors

---

## 10. Related Files

| File | Relationship |
|------|--------------|
| **`Lexer.cs`** | Primary thrower of `LexerError` - contains all tokenization logic |
| **`Token.cs`** | Defines the tokens that `Lexer.cs` produces (when no errors occur) |
| **`Compiler.cs`** | Catches and formats `LexerError` for display to users |
| **`ParserError.cs`** | Similar error class for parser phase (next pipeline stage) |
| **`Sharpy.Compiler.Tests/Lexer/LexerNegativeTests.cs`** | Tests that verify `LexerError` is thrown for invalid input |

---

## Summary

`LexerError` is a **simple, focused exception class** that serves a critical role in the Sharpy compiler:

**Key Takeaways:**
- ✅ Stores precise location information (line, column)
- ✅ Provides clear, formatted error messages
- ✅ Used exclusively for character/token-level syntax errors
- ✅ Caught and formatted by higher compiler layers
- ✅ Extensively tested with negative test cases
- ✅ Follows fail-fast error handling philosophy

**When working with lexer errors:**
1. Write clear, actionable error messages
2. Provide accurate line/column tracking
3. Add comprehensive test coverage
4. Consider if the error is truly a lexer issue (vs. parser/semantic)

**Philosophy:** Keep the error class simple; add sophistication at higher layers where needed.
