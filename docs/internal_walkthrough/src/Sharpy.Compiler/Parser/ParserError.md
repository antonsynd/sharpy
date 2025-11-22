# Walkthrough: ParserError.cs

**Source File**: `src/Sharpy.Compiler/Parser/ParserError.cs`

---

## 1. Overview

`ParserError.cs` defines a simple yet crucial exception class used throughout the Sharpy compiler's parsing phase. When the parser encounters syntactically invalid Sharpy code, it throws a `ParserError` to report the problem with precise location information (line and column numbers).

**Role in the Project:**
- **Error Reporting**: Provides structured error information during syntax analysis
- **Compilation Pipeline**: Part of the Lexer → Parser → Semantic Analyzer → Code Generator flow
- **User Feedback**: Enables the compiler to report syntax errors with exact source locations to developers

This is one of three primary error types in the compiler:
- `LexerError` - Tokenization errors (invalid characters, malformed literals)
- **`ParserError`** - Syntax/grammar errors (invalid statement structure, unexpected tokens)
- `SemanticError` - Type checking and semantic validation errors

---

## 2. Class/Type Structure

### `ParserError` Class

```csharp
public class ParserError : Exception
{
    public int Line { get; }
    public int Column { get; }

    public ParserError(string message, int line, int column)
        : base($"Parser error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
    }
}
```

**Key Characteristics:**
- **Inheritance**: Extends `System.Exception`, making it a standard .NET exception type
- **Immutability**: Properties are get-only, set during construction
- **Location Tracking**: Captures exact position in source code where error occurred

**Properties:**

| Property | Type | Purpose |
|----------|------|---------|
| `Line` | `int` | The line number (1-based) where the parser error occurred |
| `Column` | `int` | The column number (1-based) where the parser error occurred |
| `Message` | `string` | (Inherited) Formatted error message including location and description |

---

## 3. Key Functions/Methods

### Constructor

```csharp
public ParserError(string message, int line, int column)
    : base($"Parser error at line {line}, column {column}: {message}")
```

**Purpose**: Creates a new parser error with location and description.

**Parameters:**
- `message` - Human-readable description of what went wrong (e.g., "Expected identifier", "Invalid decorator target")
- `line` - The line number in the source file (1-based indexing)
- `column` - The column position in that line (1-based indexing)

**Implementation Details:**
- Calls base `Exception` constructor with formatted message
- Format: `"Parser error at line {line}, column {column}: {message}"`
- Stores line and column for programmatic access by error handlers

**Example Usage in Parser:**
```csharp
// From Parser.cs - checking for valid decorator name
if (Current.Type != TokenType.Identifier)
    throw new ParserError("Expected decorator name", Current.Line, Current.Column);

// From Parser.cs - validating type annotation target
if (expr is not Identifier id)
    throw new ParserError("Invalid type annotation target", Current.Line, Current.Column);

// From Parser.cs - ensuring enum has members
if (members.Count == 0)
    throw new ParserError($"Enum '{name}' must have at least one member", startLine, startColumn);
```

**How It Fits Into the Codebase:**
1. **Parser throws** `ParserError` when encountering invalid syntax
2. **Compiler catches** the error in `Compiler.cs`:
   ```csharp
   catch (ParserError ex)
   {
       allErrors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
   }
   ```
3. **User sees** formatted error message like:
   ```
   calculator.spy(15,8): error: Parser error at line 15, column 8: Expected decorator name
   ```

---

## 4. Dependencies

### Internal Dependencies
- **None** - This is a pure data class with no dependencies on other Sharpy components

### External Dependencies
- `System.Exception` (Base class from .NET BCL)

### Used By
- **`Parser.cs`** - Main parser implementation throws `ParserError` throughout parsing
- **`Compiler.cs`** - Catches and formats parser errors for display
- **Test files** - Test code validates parser errors are thrown correctly

---

## 5. Patterns and Design Decisions

### Design Pattern: Structured Exception

**Rationale:**
- Separates error **type** (parser vs lexer vs semantic) from error **details**
- Enables specific catch blocks for different compilation phases
- Provides structured data (line/column) instead of just a string message

**Consistency Across Compiler:**
All three error types follow the same pattern:

```csharp
// LexerError.cs - Required location
public LexerError(string message, int line, int column)
    : base($"Lexer error at line {line}, column {column}: {message}")

// ParserError.cs - Required location  
public ParserError(string message, int line, int column)
    : base($"Parser error at line {line}, column {column}: {message}")

// SemanticError.cs - Optional location (some errors are file-level)
public SemanticError(string message, int? line = null, int? column = null)
    : base(FormatMessage(message, line, column))
```

**Why Line and Column are Required:**
- Parser always knows current token position
- Every syntax error has a specific location
- Unlike semantic errors (which may be file-wide), parser errors are always tied to a token

### Immutability

Properties are read-only after construction:
- Prevents accidental modification during error propagation
- Makes error objects safe to pass through multiple layers
- Aligns with functional programming principles

### String Interpolation in Base Message

The formatted message is computed once in the constructor:
```csharp
: base($"Parser error at line {line}, column {column}: {message}")
```

**Benefits:**
- `ex.Message` includes all information
- Works with existing .NET error logging infrastructure
- No need to special-case Sharpy errors in generic error handlers

**Trade-off:**
- Duplicates information (Message contains Line/Column as text, plus properties)
- Ensures readability even if consumer doesn't check properties

---

## 6. Debugging Tips

### When You See a ParserError

**Step 1: Locate the Error**
The error message includes precise location:
```
Parser error at line 42, column 15: Expected identifier
```

**Step 2: Check the Source**
Look at line 42, column 15 in your Sharpy source file. The error is at that exact position.

**Step 3: Understand Context**
Parser errors usually mean:
- Missing token (expected `:`, got newline)
- Wrong token type (expected identifier, got number)
- Invalid syntax structure (decorator on wrong statement type)

### Debugging Parser Error Throwing

**Set breakpoint** in `Parser.cs` where `ParserError` is thrown:

```csharp
// Add breakpoint on this line
throw new ParserError("Expected decorator name", Current.Line, Current.Column);
```

**Inspect Variables:**
- `Current` - The current token being processed
- `_tokens` - All tokens from the lexer
- `_position` - Current position in token stream

**Common Patterns:**

```csharp
// Pattern 1: Expected specific token type
if (Current.Type != TokenType.Identifier)
    throw new ParserError("Expected identifier", Current.Line, Current.Column);

// Pattern 2: Invalid AST node type
if (expr is not Identifier id)
    throw new ParserError("Invalid target", Current.Line, Current.Column);

// Pattern 3: Structural validation
if (members.Count == 0)
    throw new ParserError("Must have members", startLine, startColumn);
```

### Testing Parser Errors

**Write tests that expect errors:**

```csharp
[Fact]
public void TestInvalidDecorator_ThrowsParserError()
{
    var source = "@\ndef foo(): pass";  // Missing decorator name
    var parser = new Parser(source);
    
    var ex = Assert.Throws<ParserError>(() => parser.Parse());
    Assert.Equal(1, ex.Line);
    Assert.Contains("Expected decorator name", ex.Message);
}
```

**Key testing practices:**
- Verify error is thrown
- Check line/column numbers are correct
- Validate error message is helpful

---

## 7. Contribution Guidelines

### When to Add/Modify ParserError

**You should rarely need to modify this class.** It's a simple, stable data structure.

**When you MIGHT modify:**

1. **Adding Additional Context** (hypothetical):
   ```csharp
   public class ParserError : Exception
   {
       public int Line { get; }
       public int Column { get; }
       public string? SourceContext { get; }  // NEW: Show surrounding code
       
       public ParserError(string message, int line, int column, string? context = null)
           : base(FormatMessage(message, line, column, context))
       {
           Line = line;
           Column = column;
           SourceContext = context;
       }
   }
   ```

2. **Adding Error Codes** (for categorization):
   ```csharp
   public enum ParserErrorCode
   {
       UnexpectedToken,
       MissingToken,
       InvalidDecoratorTarget,
       // ...
   }
   
   public ParserErrorCode Code { get; }
   ```

### More Common Contributions

**Adding New Parser Error Throws:**

When adding new syntax to Sharpy:

1. **Identify validation points** in `Parser.cs`
2. **Throw `ParserError`** with clear message:
   ```csharp
   if (!IsValidNewSyntax())
       throw new ParserError("Clear description of what's wrong", Current.Line, Current.Column);
   ```
3. **Write tests** to verify error is thrown correctly
4. **Ensure error message** is actionable for users

**Error Message Best Practices:**

✅ **Good Messages:**
```csharp
"Expected identifier after 'def' keyword"
"Decorators can only be applied to functions, classes, or structs"
"Enum 'Color' must have at least one member"
```

❌ **Bad Messages:**
```csharp
"Unexpected token"  // Too vague
"Syntax error"      // Doesn't explain what's wrong
"Error"             // Completely unhelpful
```

### Testing Changes

If you modify `ParserError`:

1. **Run parser tests:**
   ```bash
   dotnet test --filter "FullyQualifiedName~Parser"
   ```

2. **Run integration tests:**
   ```bash
   dotnet test --filter "FullyQualifiedName~Integration"
   ```

3. **Test error formatting:**
   ```bash
   dotnet run --project src/Sharpy.Cli -- build invalid_syntax.spy
   ```
   Verify error messages display correctly.

### Related Files to Review

When working with `ParserError`:

- **`src/Sharpy.Compiler/Parser/Parser.cs`** - See how errors are thrown
- **`src/Sharpy.Compiler/Compiler.cs`** - See how errors are caught and formatted
- **`src/Sharpy.Compiler/Lexer/LexerError.cs`** - Similar pattern for lexer phase
- **`src/Sharpy.Compiler/Semantic/SemanticError.cs`** - Similar pattern for semantic phase
- **`src/Sharpy.Compiler.Tests/Parser/ParserTests.cs`** - Test examples

---

## Summary

`ParserError` is a focused, well-designed exception class that:
- ✅ Provides precise error location (line and column)
- ✅ Follows consistent pattern with other compiler errors
- ✅ Integrates seamlessly with .NET exception handling
- ✅ Enables clear, actionable error messages for Sharpy developers

Its simplicity is a strength—it does one thing well and requires minimal maintenance.
