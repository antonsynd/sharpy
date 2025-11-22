# Walkthrough: SemanticError.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SemanticError.cs`

---

## Overview

`SemanticError.cs` defines the exception type used throughout the Sharpy compiler's semantic analysis phase. While this is a small, focused file (only ~30 lines), it plays a critical role in the compiler's error reporting infrastructure.

**Purpose**: This class represents errors discovered during semantic analysis—the phase where the compiler checks that your Sharpy code makes sense beyond just syntax. These include:
- Type mismatches (e.g., assigning a string to an integer variable)
- Undefined variable references
- Access violations (e.g., accessing private members from outside a class)
- Invalid inheritance relationships
- Control flow issues (e.g., unreachable code, missing return statements)

**When it's used**: `SemanticError` is thrown or collected by various semantic analysis components:
- `TypeChecker` - validates type correctness
- `NameResolver` - ensures all names are defined before use
- `ControlFlowValidator` - checks control flow patterns
- `AccessValidator` - validates access modifiers
- `ImportResolver` - validates import statements
- `TypeResolver` - resolves type annotations
- `ModuleRegistry` - validates module structure

## Class Structure

### SemanticError Class

```csharp
public class SemanticError : Exception
{
    public int? Line { get; }
    public int? Column { get; }
    
    public SemanticError(string message, int? line = null, int? column = null)
        : base(FormatMessage(message, line, column))
    {
        Line = line;
        Column = column;
    }
    
    private static string FormatMessage(string message, int? line, int? column)
    {
        // Formatting logic...
    }
}
```

**Inheritance**: Extends `System.Exception`, making it a proper .NET exception that can be caught and handled using standard exception mechanisms.

**Key Design Decision**: Using `int?` (nullable integers) for `Line` and `Column` allows errors to be reported even when position information isn't available (e.g., module-level validation errors).

### Properties

#### `Line` (int?)
- **Purpose**: Stores the line number where the semantic error occurred
- **Nullable**: Yes - not all errors have a specific line (e.g., module-level errors)
- **Read-only**: Set via constructor, cannot be modified after creation
- **Example**: For `x: int = "hello"` on line 5, `Line` would be `5`

#### `Column` (int?)
- **Purpose**: Stores the column position where the error occurred
- **Nullable**: Yes - some errors may only have line information
- **Read-only**: Set via constructor, cannot be modified after creation
- **Example**: For an error at position 12 on a line, `Column` would be `12`

## Key Methods

### Constructor

```csharp
public SemanticError(string message, int? line = null, int? column = null)
    : base(FormatMessage(message, line, column))
{
    Line = line;
    Column = column;
}
```

**Parameters**:
- `message` (string): The core error message describing what went wrong
- `line` (int?, optional): Line number where the error occurred
- `column` (int?, optional): Column position where the error occurred

**How it works**:
1. Calls `FormatMessage()` to create a formatted error string
2. Passes the formatted message to the base `Exception` constructor
3. Stores the raw line and column values in properties for later access

**Design note**: The formatted message is stored in the base exception's `Message` property, while raw position data is preserved in `Line` and `Column` for structured error reporting.

**Usage examples**:
```csharp
// Error with full position information
throw new SemanticError("Type mismatch: expected int, got str", 42, 15);

// Error with only line information
throw new SemanticError("Undefined variable 'x'", 10);

// Error without position information (module-level)
throw new SemanticError("Cannot import module 'nonexistent'");
```

### FormatMessage (Private Static)

```csharp
private static string FormatMessage(string message, int? line, int? column)
{
    if (line.HasValue && column.HasValue)
    {
        return $"Semantic error at line {line}, column {column}: {message}";
    }
    if (line.HasValue)
    {
        return $"Semantic error at line {line}: {message}";
    }
    return $"Semantic error: {message}";
}
```

**Purpose**: Creates a human-readable error message with optional position information.

**Logic flow**:
1. **Both line and column available**: `"Semantic error at line 42, column 15: Type mismatch"`
2. **Only line available**: `"Semantic error at line 42: Type mismatch"`
3. **No position info**: `"Semantic error: Type mismatch"`

**Why it's static**: This is a pure formatting function with no instance state dependency. Being static allows it to be called from the constructor before instance initialization completes.

**Why it's private**: Formatting is an internal implementation detail. External code should use the constructor, which automatically formats the message.

## Dependencies

### Upstream Dependencies (What This File Uses)
- `System.Exception`: Base class for all exceptions
- None beyond the .NET standard library

### Downstream Dependencies (What Uses This File)

**Direct consumers** (files that throw or collect `SemanticError`):
- `TypeChecker.cs` - Collects errors in `_errors` list, checks type validity
- `NameResolver.cs` - Collects errors during name resolution
- `ControlFlowValidator.cs` - Creates errors for control flow violations
- `AccessValidator.cs` - Creates errors for access violations
- `ImportResolver.cs` - Creates errors for invalid imports
- `TypeResolver.cs` - Creates errors when types can't be resolved
- `ModuleRegistry.cs` - Creates errors for module-level issues
- `Scope.cs` - Throws errors for duplicate symbol definitions

**Usage patterns**:

1. **Throw immediately** (strict validation):
```csharp
// In Scope.cs
if (_symbols.ContainsKey(symbol.Name))
{
    throw new SemanticError($"Symbol '{symbol.Name}' is already defined in this scope");
}
```

2. **Collect and continue** (gather all errors):
```csharp
// In TypeChecker.cs
private readonly List<SemanticError> _errors = new();

private void ReportError(SemanticError error)
{
    _errors.Add(error);
    if (!ContinueAfterError || _errors.Count >= MaxErrors)
    {
        throw new AggregateException(_errors);
    }
}
```

## Patterns and Design Decisions

### 1. **Nullable Position Information**
**Why**: Not all semantic errors can be pinpointed to a specific line/column:
- Module-level configuration errors
- Cross-module dependency issues
- Abstract validation errors

**Alternative considered**: Always require position info → Rejected because it would force callers to provide fake/meaningless positions.

### 2. **Immutable Error Objects**
**Why**: Once created, errors shouldn't change. This makes them safe to pass around, collect in lists, and report without worrying about mutation.

**Implementation**: Properties are get-only, values set in constructor.

### 3. **Separation of Concerns**
**Formatting vs. Storage**: Raw position data stored separately from formatted message:
- `Message` property (inherited): Formatted, human-readable
- `Line`/`Column` properties: Raw data for structured reporting (IDE integration, JSON output, etc.)

This allows the same error to be:
- Displayed to users: `Console.WriteLine(error.Message)`
- Logged as structured data: `logger.Log(error.Line, error.Column, error.Message)`
- Sent to IDE language server: `{ line: error.Line, column: error.Column, message: ... }`

### 4. **Error Collection Strategy**
The compiler supports two error handling modes:

**Fail-fast** (throw immediately):
```csharp
throw new SemanticError("Critical error");
```

**Error accumulation** (collect all errors):
```csharp
_errors.Add(new SemanticError("Issue 1", 10));
_errors.Add(new SemanticError("Issue 2", 20));
// Continue checking, report all errors at the end
```

Most semantic analyzers use accumulation to provide better developer experience (fix multiple errors at once rather than one-at-a-time).

## Debugging Tips

### 1. **Finding Where an Error Originated**

When you see a `SemanticError`, look at the stack trace to identify which analyzer component created it:

```csharp
// Example stack trace:
//   at Sharpy.Compiler.Semantic.TypeChecker.CheckAssignment(...)
//   at Sharpy.Compiler.Semantic.TypeChecker.VisitAssignStmt(...)
```

This tells you the error came from type checking during assignment statement validation.

### 2. **Checking Position Information**

When debugging error reporting:
```csharp
// In debugger or logs
Console.WriteLine($"Error: {error.Message}");
Console.WriteLine($"Line: {error.Line?.ToString() ?? "unknown"}");
Console.WriteLine($"Column: {error.Column?.ToString() ?? "unknown"}");
```

### 3. **Understanding Error vs. Exception**

- **`SemanticError` thrown**: Compiler stops immediately (fail-fast mode)
- **`SemanticError` added to list**: Compiler continues (accumulation mode)

When debugging, check if the code path throws or collects:
```csharp
// Throws (stops immediately)
throw new SemanticError("Cannot proceed");

// Collects (continues)
_errors.Add(new SemanticError("Non-critical issue"));
```

### 4. **Common Error Patterns**

**Missing position info**: If errors don't show line numbers, check that the AST node has location information:
```csharp
// Good: Extract position from AST node
var error = new SemanticError(
    "Type mismatch", 
    node.Line,      // AST nodes should have Line property
    node.Column
);

// Bad: Hardcoded or missing position
var error = new SemanticError("Type mismatch");  // No position!
```

### 5. **Testing Error Messages**

When writing tests for semantic errors:
```csharp
// Test that error is thrown
var ex = Assert.Throws<SemanticError>(() => analyzer.Analyze(badCode));
Assert.Contains("expected keyword", ex.Message.ToLower());
Assert.Equal(42, ex.Line);

// Test that error is collected
analyzer.Analyze(badCode);
Assert.Single(analyzer.Errors);
Assert.Equal(42, analyzer.Errors[0].Line);
```

## Contribution Guidelines

### When to Modify This File

**You should modify `SemanticError.cs` if**:
- Adding new position information (e.g., `FileName`, `EndLine`, `EndColumn` for ranges)
- Changing error formatting conventions
- Adding categorization (e.g., `ErrorCode`, `Severity` levels)
- Supporting internationalization (different languages)

**You should NOT modify this file for**:
- Adding new types of semantic errors (those go in analyzer classes)
- Changing how errors are reported to users (that's in the CLI or diagnostic reporter)
- Adding validation logic (that belongs in semantic analyzer components)

### Potential Enhancements

Here are some changes that might be valuable:

#### 1. **Add Error Codes**
```csharp
public class SemanticError : Exception
{
    public string ErrorCode { get; }  // e.g., "SE001", "SE042"
    
    public SemanticError(string code, string message, int? line = null, int? column = null)
        : base(FormatMessage(code, message, line, column))
    {
        ErrorCode = code;
        Line = line;
        Column = column;
    }
}
```

**Benefits**: Users can google error codes, documentation can reference specific codes, IDE can provide quick fixes per code.

#### 2. **Add Severity Levels**
```csharp
public enum ErrorSeverity { Error, Warning, Info, Hint }

public class SemanticError : Exception
{
    public ErrorSeverity Severity { get; }
    
    // Warnings don't stop compilation
    // Errors do
}
```

**Benefits**: Support warnings alongside errors, enable "treat warnings as errors" mode.

#### 3. **Add Source Context**
```csharp
public class SemanticError : Exception
{
    public string? FileName { get; }
    public int? EndLine { get; }      // For multi-line errors
    public int? EndColumn { get; }    // For range highlighting
    public string? SourceSnippet { get; }  // The actual source line
}
```

**Benefits**: Better error messages showing the actual problematic code, support for IDE integrations.

#### 4. **Add Suggested Fixes**
```csharp
public class SemanticError : Exception
{
    public List<string>? Suggestions { get; }
    
    // e.g., "Did you mean 'length' instead of 'lenght'?"
}
```

**Benefits**: More helpful error messages, potential for auto-fix features.

### Testing Guidelines

When modifying `SemanticError`:

1. **Test all formatting paths**:
   - With line and column
   - With line only
   - With neither

2. **Test property preservation**:
   - Ensure `Line` and `Column` are correctly stored
   - Ensure `Message` is correctly formatted

3. **Test integration**:
   - Ensure analyzers can still throw/collect errors
   - Ensure CLI displays errors correctly

### Code Style

Follow existing conventions:
- Nullable types for optional position info (`int?`, not `Nullable<int>`)
- XML doc comments for public members
- Private members for internal implementation details
- Descriptive parameter names (`message`, not `msg`)

## Related Files

To fully understand error handling in the semantic analysis phase, also review:

- **`TypeChecker.cs`**: Main consumer, shows error collection pattern
- **`NameResolver.cs`**: Shows first-pass error detection
- **`ControlFlowValidator.cs`**: Shows specialized validation errors
- **`Diagnostics/`**: Error formatting and reporting for end users
- **Test files**: `src/Sharpy.Compiler.Tests/Semantic/` - examples of expected errors

## Summary

`SemanticError` is a small but essential building block in the Sharpy compiler:
- **Simple design**: Just three pieces of data (message, line, column)
- **Flexible usage**: Can be thrown or collected
- **Clear responsibility**: Represents semantic analysis errors, nothing more
- **Extensible**: Easy to add new features (error codes, severity, etc.) without breaking existing code

While the file itself is straightforward, understanding how it fits into the broader semantic analysis pipeline is key to effectively working with the compiler's error reporting system.
