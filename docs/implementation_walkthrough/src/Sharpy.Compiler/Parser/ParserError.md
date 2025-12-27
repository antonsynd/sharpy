# Walkthrough: ParserError.cs

**Source File**: `src/Sharpy.Compiler/Parser/ParserError.cs`

---

## Overview

`ParserError.cs` defines a custom exception type used throughout the Sharpy compiler's parsing phase. This simple but critical class serves as the error reporting mechanism when the parser encounters syntactically invalid Sharpy code. 

**Role in the compiler pipeline:**
```
Source (.spy) → Lexer → Parser → [ParserError thrown here] → Semantic Analysis → CodeGen
```

When the parser detects invalid syntax (unexpected tokens, missing required elements, malformed expressions), it throws a `ParserError` with precise location information (line and column numbers). This allows the compiler to provide helpful error messages to developers.

---

## Class Structure

### ParserError Class

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

**Inheritance**: `ParserError` extends the standard .NET `Exception` class, making it compatible with standard C# exception handling patterns.

**Properties:**
- `Line` (int): The 1-based line number where the parsing error occurred
- `Column` (int): The 1-based column number where the parsing error occurred

Both properties are read-only (`get`-only), ensuring error location information cannot be modified after the exception is created.

---

## Key Implementation Details

### Constructor

```csharp
public ParserError(string message, int line, int column)
    : base($"Parser error at line {line}, column {column}: {message}")
```

**Purpose**: Creates a new parser error with location context.

**Parameters:**
- `message` (string): A human-readable description of what went wrong (e.g., "Expected identifier, got '}'")
- `line` (int): The line number from the source file where the error occurred
- `column` (int): The column number on that line

**Key Design Choice**: The constructor uses base class initialization (`: base(...)`) to format the complete error message. This means:
- The full error message is stored in the inherited `Exception.Message` property
- The format is standardized: `"Parser error at line X, column Y: [message]"`
- The `Line` and `Column` properties store the raw location data for programmatic use

**Example formatted message:**
```
Parser error at line 42, column 15: Expected identifier, got TokenType.RightBrace
```

---

## Dependencies

### Direct Dependencies

**None** - This class has zero external dependencies beyond the standard .NET `System` namespace (for the `Exception` base class).

### Dependent Code (Who Uses ParserError)

1. **Parser.cs** - The main parser throws `ParserError` extensively:
   ```csharp
   // Example from Parser.cs line 2341
   throw new ParserError($"Expected {type}, got {Current.Type}", Current.Line, Current.Column);
   
   // Example from Parser.cs line 2348
   throw new ParserError($"Expected identifier, got {Current.Type}", Current.Line, Current.Column);
   ```

2. **Compiler.cs** - Catches `ParserError` to format compilation errors:
   ```csharp
   catch (ParserError ex)
   {
       allErrors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
       projectMetrics.AddFileMetrics(fileMetrics);
   }
   ```

### Token Information Source

The `Line` and `Column` values passed to `ParserError` come from `Token` objects (defined in the Lexer). The parser tracks the current token position and uses `Current.Line` and `Current.Column` when throwing errors.

---

## Patterns and Design Decisions

### 1. **Exception-Based Error Handling**

**Pattern**: Uses exceptions for control flow during parsing errors.

**Rationale:**
- Parsing errors are exceptional cases (not expected during normal execution)
- Exceptions naturally unwind the call stack, simplifying error propagation through recursive descent parsing
- The parser can throw an error deep in the parse tree without manually returning error codes up through multiple function calls

**Alternative Approaches Not Used:**
- ❌ Error codes/Result types: Would require threading error state through every parser method
- ❌ Error accumulation: Parser stops at the first error (fail-fast approach)

### 2. **Immutable Error Information**

**Pattern**: `Line` and `Column` are read-only properties.

**Rationale:**
- Error location should never change after creation
- Prevents accidental modification during error handling
- Makes the error object's state predictable and thread-safe

### 3. **Simple, Focused Class**

**Pattern**: Single responsibility - just store and format parsing errors.

**Rationale:**
- Keeps the error type lightweight and easy to understand
- No complex error recovery logic embedded in the exception
- Separation of concerns: Parser decides *when* to throw, `ParserError` decides *how* to format

### 4. **Location-Aware Errors**

**Pattern**: Always includes line and column information.

**Rationale:**
- Essential for IDE integration (syntax highlighting, error markers)
- Helps developers quickly locate and fix syntax errors
- Matches conventions from other compilers (C#, TypeScript, etc.)

---

## How It Fits Into the Broader Codebase

### Parsing Workflow

```
1. Lexer tokenizes source → List<Token> (each token has Line/Column)
2. Parser processes tokens recursively
3. When invalid syntax detected:
   - Parser identifies the problematic token
   - Throws ParserError with token's location and descriptive message
4. Compiler.cs catches ParserError
5. Error formatted for display to user
```

### Example Flow

**Source code:**
```python
def greet(name
    print(f"Hello, {name}")
```

**What happens:**
1. Lexer produces tokens including `TokenType.LeftParen` after `name` (line 1, col 15)
2. Parser expects `)` or type annotation after parameter name
3. Parser finds `TokenType.Newline` instead
4. Parser throws: `new ParserError("Expected ')', got Newline", 1, 15)`
5. Compiler catches and formats: `"file.spy(1,15): error: Parser error at line 1, column 15: Expected ')', got Newline"`

### Error Message Flow

```
ParserError.Message (formatted)
    ↓
Compiler.cs (wraps with file location)
    ↓
Console output or IDE error panel
    ↓
Developer sees: "samples/hello.spy(1,15): error: Parser error at line 1, column 15: Expected ')'"
```

---

## Common Usage Patterns in Parser.cs

### 1. **Unexpected Token Errors**
```csharp
throw new ParserError($"Expected {expectedType}, got {Current.Type}", Current.Line, Current.Column);
```
Most common pattern - parser expected one token type but found another.

### 2. **Invalid Construction Errors**
```csharp
throw new ParserError("Decorators can only be applied to functions, classes, or structs", Current.Line, Current.Column);
```
Semantic constraints violated during parsing (though these could arguably be in semantic analysis).

### 3. **Missing Required Elements**
```csharp
throw new ParserError($"Enum '{name}' must have at least one member", startLine, startColumn);
```
Language rules require certain structures (e.g., non-empty enum bodies).

### 4. **Context-Specific Errors**
```csharp
throw new ParserError("Tuple expression not allowed as a statement", Current.Line, Current.Column);
```
Some expressions are valid in certain contexts but not others.

---

## Debugging Tips

### 1. **Finding Where Errors Are Thrown**

**Quick search:**
```bash
grep -n "throw new ParserError" src/Sharpy.Compiler/Parser/Parser.cs
```

**Common locations:**
- Token expectation methods (`Expect()`, `ExpectIdentifier()`)
- Statement parsing (invalid statement structures)
- Expression parsing (malformed expressions)

### 2. **Understanding Error Context**

When debugging a `ParserError`:
1. **Check the line/column**: Look at the source code at that exact position
2. **Look at the current token**: What did the parser see? (`Current.Type`, `Current.Value`)
3. **Check the call stack**: Which parsing method threw the error? (e.g., `ParseFunctionDef`, `ParseExpression`)
4. **Trace backwards**: What tokens led to this state?

### 3. **Common Error Scenarios**

| Error Pattern | Likely Cause |
|--------------|--------------|
| `Expected 'X', got 'Y'` | Missing punctuation, wrong token order |
| `Expected identifier` | Using keyword where name expected, or typo |
| `Expected newline` | Missing line break (indentation issue) |
| `Unexpected token` | Extra token where none expected |

### 4. **Testing Error Cases**

When writing tests for parser errors:
```csharp
[Fact]
public void TestMissingClosingParen()
{
    var source = "def foo(x:\n    pass";
    var ex = Assert.Throws<ParserError>(() => ParseModule(source));
    Assert.Equal(1, ex.Line);  // Verify correct location
    Assert.Contains("Expected ')'", ex.Message);  // Verify descriptive message
}
```

### 5. **Debugging Parser State**

When a `ParserError` is thrown, examine:
- `_position`: Current token index in the token list
- `Current`: The token that caused the error
- `Previous`: The token before the error
- `Peek()`: The next token (if looking ahead)

---

## Contribution Guidelines

### When to Add New ParserError Throws

Add a new `throw new ParserError(...)` when:
- ✅ Implementing a new language feature that needs validation
- ✅ Improving error messages for existing confusing cases
- ✅ Adding stricter syntax validation

**Example PR:**
```csharp
// BEFORE: Generic error
throw new ParserError("Unexpected token", Current.Line, Current.Column);

// AFTER: Specific, helpful error
throw new ParserError($"Match expressions require at least one case clause, found {Current.Type}", Current.Line, Current.Column);
```

### When NOT to Modify ParserError.cs

**Don't modify this file when:**
- ❌ Adding semantic errors (use semantic analyzer errors instead)
- ❌ Adding runtime errors (use different exception types)
- ❌ Changing error message format (update Compiler.cs error formatting instead)
- ❌ Adding error recovery logic (that belongs in Parser.cs)

### Improving Error Messages

**Guidelines for error messages:**
1. **Be specific**: Say what was expected and what was found
2. **Be concise**: One sentence is usually enough
3. **Use technical terms correctly**: Reference token types, not raw characters when possible
4. **Provide context**: Include relevant identifiers or values when helpful

**Good error messages:**
```csharp
✅ "Expected parameter name after '(', got TokenType.RightParen"
✅ "Class 'MyClass' cannot inherit from sealed class 'SealedBase'"
✅ "Type parameters must be declared before function parameters"
```

**Poor error messages:**
```csharp
❌ "Syntax error"  // Too vague
❌ "Invalid" // No context
❌ "Expected )" // Missing what was found
```

### Testing Considerations

**When adding new parser error cases, add tests:**
```csharp
// In Sharpy.Compiler.Tests/Parser/ParserErrorTests.cs
[Fact]
public void TestNewFeature_InvalidSyntax_ThrowsParserError()
{
    var source = "invalid new feature syntax";
    var ex = Assert.Throws<ParserError>(() => parser.ParseModule());
    
    // Verify error location is accurate
    Assert.Equal(expectedLine, ex.Line);
    Assert.Equal(expectedColumn, ex.Column);
    
    // Verify error message is helpful
    Assert.Contains("expected syntax element", ex.Message, StringComparison.OrdinalIgnoreCase);
}
```

### Related Files to Review

When working with `ParserError`, also consider:

1. **Parser.cs**: Where errors are thrown - understand parsing logic
2. **Token.cs**: Source of line/column information
3. **Compiler.cs**: How errors are caught and formatted for output
4. **LexerError.cs**: Parallel error type for lexing phase (similar pattern)

---

## Advanced Topics

### Error Recovery (Future Enhancement)

Currently, the parser uses a **fail-fast** approach: throw on first error. A future enhancement could implement **error recovery**:

```csharp
// Hypothetical future implementation
public class ParserError : Exception
{
    public int Line { get; }
    public int Column { get; }
    public RecoveryStrategy? SuggestedRecovery { get; }  // Future addition
    
    // Constructor with recovery hint
    public ParserError(string message, int line, int column, RecoveryStrategy? recovery = null)
        : base($"Parser error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
        SuggestedRecovery = recovery;
    }
}
```

This would allow the parser to collect multiple errors in one pass (like how TypeScript's tsc works).

### IDE Integration

When integrated with an IDE or language server, `ParserError` information maps to:
- **Line/Column → Error Squiggle Position**: Red underline in editor
- **Message → Hover Tooltip**: Error explanation on hover
- **Error List**: Clickable error in problems panel

---

## Summary Checklist for New Contributors

When working with `ParserError`:

- [ ] Understand it's thrown only during **parsing phase** (not lexing or semantic analysis)
- [ ] Always provide accurate **line and column** numbers from the current token
- [ ] Write **descriptive, specific** error messages
- [ ] Add **tests** for new error cases
- [ ] Consider the **developer experience**: Will the error message help fix the problem?
- [ ] Review similar errors in `Parser.cs` for consistency
- [ ] Don't catch `ParserError` in parser code (let it bubble up to `Compiler.cs`)

---

## Quick Reference

### Constructor Signature
```csharp
ParserError(string message, int line, int column)
```

### Common Throw Patterns
```csharp
// From current token
throw new ParserError($"Expected X, got {Current.Type}", Current.Line, Current.Column);

// From saved position (for multi-token constructs)
throw new ParserError($"Invalid construct", startLine, startColumn);

// With context
throw new ParserError($"'{identifier}' is not valid here", token.Line, token.Column);
```

### Catching in Compiler
```csharp
try {
    var ast = parser.ParseModule();
} catch (ParserError ex) {
    LogError($"{filename}({ex.Line},{ex.Column}): {ex.Message}");
}
```

---

**Last Updated**: 2025-12-27  
**Maintainer**: Sharpy Compiler Team  
**Related Docs**: 
- `docs/architecture/semantic-analyzer-architecture.md`
- `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
