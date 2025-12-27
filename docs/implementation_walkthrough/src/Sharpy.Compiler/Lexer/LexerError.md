# Walkthrough: LexerError.cs

**Source File**: `src/Sharpy.Compiler/Lexer/LexerError.cs`

---

## 1. Overview

`LexerError.cs` defines a specialized exception class used exclusively during the lexical analysis (tokenization) phase of the Sharpy compiler. This file is deceptively simple but plays a critical role in the compiler's error reporting pipeline.

**Purpose**: When the Lexer encounters invalid syntax at the character/token level (unterminated strings, invalid escape sequences, indentation errors, etc.), it throws a `LexerError` with precise location information to help developers quickly identify and fix issues in their Sharpy source code.

**Role in Compilation Pipeline**:
```
Source (.spy) → [LEXER] → Tokens → Parser → AST → ...
                   ↑
                   └── Throws LexerError if tokenization fails
```

---

## 2. Class Structure

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

**Design Characteristics**:
- **Inheritance**: Extends `System.Exception`, making it compatible with standard .NET exception handling
- **Immutability**: `Line` and `Column` properties are read-only (`get;` only), set once during construction
- **Location-aware**: Unlike generic exceptions, this captures the exact position in source code where the error occurred

---

## 3. Key Components

### Properties

#### `Line` (int)
- **Purpose**: The 1-based line number where the lexer error occurred
- **Usage**: Used by the compiler to format error messages in standard format: `file.spy(line,column): error: message`
- **Note**: Line counting starts at 1 (not 0), following standard editor conventions

#### `Column` (int)
- **Purpose**: The 1-based column number (character position) within the line
- **Usage**: Pinpoints the exact character where tokenization failed
- **Note**: Also 1-based to match most text editor displays

### Constructor

```csharp
public LexerError(string message, int line, int column)
```

**Parameters**:
- `message`: A descriptive error message (e.g., "Unterminated string literal")
- `line`: The line number where the error occurred
- `column`: The column number where the error occurred

**Implementation Details**:
- Calls the base `Exception` constructor with a formatted message
- Message format: `"Lexer error at line {line}, column {column}: {message}"`
- This format makes errors immediately readable without additional formatting
- Stores `line` and `column` in properties for programmatic access

**Example Exception Message**:
```
Lexer error at line 42, column 15: Unterminated string literal
```

---

## 4. Dependencies

### Internal Dependencies
- **`Sharpy.Compiler.Lexer` namespace**: Part of the lexer subsystem
- **Used by**: `Lexer.cs` (throws this exception ~30+ times for various error conditions)
- **Caught by**: `Compiler.cs` in the main compilation driver

### External Dependencies
- **`System.Exception`**: Standard .NET base exception class
- No other external dependencies (intentionally lightweight)

### Dependency Graph
```
LexerError.cs
    ↓ (thrown by)
Lexer.cs
    ↓ (caught by)
Compiler.cs → Formats for user display
```

---

## 5. Usage Patterns

### Where It's Thrown (Examples from Lexer.cs)

The `LexerError` is thrown in numerous scenarios throughout the lexer. Here are the most common patterns:

#### 1. **Unterminated Strings**
```csharp
// In Lexer.cs, when a string literal isn't closed
throw new LexerError("Unterminated string literal", _line, _column);
```

#### 2. **Indentation Errors**
```csharp
// When mixing tabs and spaces
throw new LexerError("Mixed tabs and spaces in indentation", _line, 1);

// When using tabs instead of spaces
throw new LexerError("Tabs are not allowed for indentation. Use 4 spaces.", _line, 1);

// When indentation isn't a multiple of 4
throw new LexerError($"Indentation must be multiple of 4 spaces (found {indent})", _line, 1);
```

#### 3. **Invalid Escape Sequences**
```csharp
// Invalid hex escape
throw new LexerError($"Invalid hex escape sequence: \\x{hex}", _line, _column);

// Invalid unicode escape
throw new LexerError($"Invalid unicode escape sequence: \\u{hex}", _line, _column);
```

#### 4. **F-String Errors**
```csharp
// Unterminated f-string expressions
throw new LexerError("Unterminated f-string expression", _line, _column);

// Unmatched braces in f-strings
throw new LexerError("Unmatched '}' in f-string", _line, _column);
```

### How It's Caught (Example from Compiler.cs)

```csharp
try
{
    // Lexer tokenization happens here
    var lexer = new Lexer(sourceCode);
    var tokens = lexer.Tokenize();
    // ... continue parsing
}
catch (LexerError ex)
{
    // Format error in MSBuild-compatible format
    allErrors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
}
```

**Error Output Format**:
```
calculator.spy(42,15): error: Lexer error at line 42, column 15: Unterminated string literal
```

This format is compatible with:
- Visual Studio error list
- MSBuild output
- Most IDE error parsers
- Standard compiler error conventions

---

## 6. Design Decisions & Patterns

### Why Inherit from Exception?

**Rationale**: 
- Integrates seamlessly with .NET's exception handling mechanisms
- Allows `catch` blocks to filter by specific error types
- Enables structured error recovery in the compiler pipeline

**Alternative Considered**: Using a result/error return type (e.g., `Result<Tokens, LexerError>`)
- **Rejected because**: Exceptions are idiomatic for compilers; they allow clean separation of happy path and error path

### Why Store Line/Column Separately?

**Rationale**:
- Programmatic access: The compiler needs to extract location info for error formatting
- Structured logging: Tools can parse and index errors by location
- IDE integration: Editors can jump to exact error positions

**Pattern**: This follows the compiler design principle of **"Location-Aware Errors"**
- Similar classes exist: `ParserError`, `SemanticError`
- All use the same pattern: inherit from `Exception` + store location data

### Why Format in Constructor vs. On-Demand?

**Design Choice**: Format the error message immediately in the constructor

**Advantages**:
- ✅ Simplicity: Single point of formatting
- ✅ Consistency: All LexerErrors have uniform message format
- ✅ Performance: Format once, not repeatedly when accessing `Message` property

**Trade-off**: 
- ❌ Less flexibility: Can't change format after construction
- ✅ But this is acceptable: Error messages shouldn't change after creation

### Immutability

**Pattern**: Properties are read-only
```csharp
public int Line { get; }  // No setter
public int Column { get; }  // No setter
```

**Rationale**:
- Errors should be immutable once created
- Prevents accidental modification during error handling
- Thread-safe by design

---

## 7. Debugging Tips

### When Debugging Lexer Issues

1. **Set Breakpoints on LexerError Construction**
   - In Visual Studio/Rider: Set breakpoint in `LexerError` constructor
   - Catches ALL lexer errors at the source
   - Inspect `message`, `line`, `column` to understand context

2. **Check Line/Column Tracking in Lexer**
   - If errors report wrong locations, the issue is likely in `Lexer.cs`
   - The lexer maintains `_line` and `_column` private fields
   - Search for: `_line++`, `_column++`, `_column = 1` to verify tracking logic

3. **Common Pitfall: Off-by-One Errors**
   ```csharp
   // ❌ Wrong: 0-based when should be 1-based
   throw new LexerError("Error", _line, _column);
   
   // ✅ Verify _line and _column are already 1-based in Lexer
   ```

4. **Test with Minimal Reproducible Examples**
   ```python
   # Save as test.spy
   print("unterminated string
   ```
   ```bash
   dotnet run --project src/Sharpy.Cli -- build test.spy
   ```
   Should output:
   ```
   test.spy(1,7): error: Lexer error at line 1, column 7: Unterminated string literal
   ```

### Understanding Error Context

When you see a `LexerError` in the logs:
1. **Check the line/column**: Navigate to exact position in source file
2. **Read the message**: Usually describes what the lexer expected vs. found
3. **Inspect surrounding code**: Errors often stem from previous lines (e.g., unclosed braces)

### Adding New Error Types

When adding new lexer errors:
```csharp
// In Lexer.cs
if (someInvalidCondition)
{
    throw new LexerError(
        "Clear, actionable message",  // ✅ Tell user what's wrong
        _line,                         // ✅ Current line
        _column                        // ✅ Current column
    );
}
```

**Message Best Practices**:
- ✅ Be specific: "Unterminated string literal" not "String error"
- ✅ Suggest fixes: "Use 4 spaces, not tabs"
- ✅ Match Python conventions (Sharpy is Pythonic)

---

## 8. Contribution Guidelines

### When to Modify This File

**Rarely!** This class is intentionally minimal. You would only modify it if:

1. **Adding More Location Context**
   ```csharp
   // Potential enhancement
   public string SourceFile { get; }  // Track which file the error is from
   public string SourceLine { get; }  // Include actual source line for better errors
   ```

2. **Adding Error Codes**
   ```csharp
   // For better tooling integration
   public string ErrorCode { get; }  // E.g., "LEX001"
   ```

3. **Supporting Error Recovery**
   ```csharp
   // For IDE scenarios where you want to continue after errors
   public ErrorSeverity Severity { get; }  // Warning vs. Error
   ```

### When to Use This Class (in other code)

**DO** throw `LexerError` when:
- ✅ You're in `Lexer.cs` and encounter invalid token-level syntax
- ✅ The error is about character sequences, strings, numbers, indentation
- ✅ You have precise line/column information

**DON'T** throw `LexerError` when:
- ❌ You're in the Parser (use `ParserError` instead)
- ❌ You're in semantic analysis (use `SemanticError` or diagnostics)
- ❌ The error is about type mismatches, undefined names, etc.

### Testing Considerations

When adding new lexer error scenarios:

1. **Add test in `Sharpy.Compiler.Tests/Lexer/LexerTests.cs`**:
   ```csharp
   [Fact]
   public void TestLexer_ThrowsOnInvalidSyntax()
   {
       var lexer = new Lexer("invalid code here");
       var ex = Assert.Throws<LexerError>(() => lexer.Tokenize());
       Assert.Equal(expectedLine, ex.Line);
       Assert.Equal(expectedColumn, ex.Column);
       Assert.Contains("expected message fragment", ex.Message);
   }
   ```

2. **Test error message format**:
   ```csharp
   [Fact]
   public void TestLexerError_FormatsMessage()
   {
       var error = new LexerError("Test message", 42, 15);
       Assert.Equal("Lexer error at line 42, column 15: Test message", error.Message);
   }
   ```

### Code Review Checklist

When reviewing code that throws `LexerError`:

- [ ] Is the error message clear and actionable?
- [ ] Are line/column numbers accurate (1-based)?
- [ ] Is this the right error type? (Not ParserError or SemanticError?)
- [ ] Is there a test that verifies this error is thrown?
- [ ] Does the error message follow Sharpy's Pythonic conventions?

---

## 9. Related Files

### Must-Read Companions

- **`Lexer.cs`**: The primary consumer; throws ~30+ LexerErrors for various conditions
- **`Token.cs`**: Defines token types; understanding tokens helps understand when errors occur
- **`Compiler.cs`**: Shows how LexerErrors are caught and formatted for end users

### Similar Error Classes

- **`Parser/ParserError.cs`**: Same pattern for parsing errors
- **`Semantic/SemanticError.cs`**: Same pattern for semantic analysis errors

All three follow the **Location-Aware Exception Pattern**:
```csharp
public class CompilerPhaseError : Exception
{
    public int Line { get; }
    public int Column { get; }
    public CompilerPhaseError(string message, int line, int column) { ... }
}
```

---

## 10. Real-World Examples

### Example 1: Indentation Error

**Sharpy Code** (`bad_indent.spy`):
```python
def hello():
      print("Hello")  # 6 spaces (should be 4)
```

**Error**:
```
bad_indent.spy(2,1): error: Lexer error at line 2, column 1: Indentation must be multiple of 4 spaces (found 6)
```

### Example 2: Unterminated String

**Sharpy Code** (`bad_string.spy`):
```python
message = "Hello, World
print(message)
```

**Error**:
```
bad_string.spy(1,11): error: Lexer error at line 1, column 11: Unterminated string literal
```

### Example 3: Invalid Escape Sequence

**Sharpy Code** (`bad_escape.spy`):
```python
path = "C:\Users\name"  # Invalid \U escape
```

**Error**:
```
bad_escape.spy(1,8): error: Lexer error at line 1, column 8: Invalid unicode escape sequence: \Use
```

---

## 11. Future Enhancements

Potential improvements to consider:

### 1. Error Recovery
Add capability to report multiple errors in one pass:
```csharp
public bool IsRecoverable { get; }  // Can lexer continue after this error?
```

### 2. Rich Error Context
Include source code snippets:
```csharp
public string SourceSnippet { get; }  // The actual line of code with the error
public string Suggestion { get; }     // Potential fix suggestion
```

### 3. Error Codes
Standardized error codes for tooling:
```csharp
public static class LexerErrorCodes
{
    public const string UnterminatedString = "LEX001";
    public const string InvalidIndentation = "LEX002";
    // ...
}
```

### 4. Internationalization
Support for localized error messages:
```csharp
public LexerError(string messageKey, int line, int column, params object[] args)
{
    // Look up localized message
}
```

---

## Summary

`LexerError.cs` is a small but essential part of the Sharpy compiler's error reporting infrastructure. Its simplicity is a strength: by focusing solely on location-aware exception handling, it provides a clean, consistent way to report tokenization failures throughout the lexer.

**Key Takeaways**:
- 🎯 Single responsibility: Report lexer errors with precise location
- 📍 Always includes line and column (1-based)
- 🔄 Follows standard exception handling patterns
- 🛠️ Rarely needs modification (by design)
- 📝 Clear, actionable error messages are crucial

When working with the lexer or debugging tokenization issues, understanding this class helps you quickly locate and fix problems in Sharpy source code.
