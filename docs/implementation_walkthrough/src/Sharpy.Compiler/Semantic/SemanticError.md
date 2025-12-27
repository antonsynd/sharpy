# Walkthrough: SemanticError.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SemanticError.cs`

---

## 1. Overview

`SemanticError.cs` defines a custom exception class used throughout the Sharpy compiler's semantic analysis phase. When the compiler detects logical errors in your Sharpy code—like using an undefined variable, type mismatches, or violating language rules—it throws a `SemanticError` to halt compilation and provide meaningful feedback to the user.

**Key Responsibilities:**
- Represent semantic errors with contextual location information (line and column)
- Format error messages in a consistent, user-friendly way
- Provide a dedicated exception type that can be caught and handled separately from other exceptions

**Position in the Pipeline:**
```
Source Code → Lexer → Parser → [SEMANTIC ANALYSIS] → Code Generation
                                  ↑
                            SemanticError thrown here
```

This file is deliberately simple by design—it's a focused utility class that does one thing well: communicate semantic errors with location context.

---

## 2. Class Structure

### `SemanticError` Class

```csharp
public class SemanticError : Exception
{
    public int? Line { get; }
    public int? Column { get; }
    
    // Constructor and helper method...
}
```

**Inheritance:** Extends `System.Exception`, making it a first-class exception in the .NET ecosystem.

**Properties:**
- **`Line` (int?)**: The line number where the error occurred (1-indexed). Nullable because some errors may not have a specific location (e.g., module-level issues).
- **`Column` (int?)**: The column position on that line (1-indexed). Also nullable for the same reason.

**Design Choice:** The properties are nullable (`int?`) rather than required. This flexibility allows the compiler to throw semantic errors even when precise location information isn't available, while still encouraging location-aware error reporting when possible.

---

## 3. Key Methods

### Constructor

```csharp
public SemanticError(string message, int? line = null, int? column = null)
    : base(FormatMessage(message, line, column))
{
    Line = line;
    Column = column;
}
```

**Purpose:** Creates a new semantic error with an optional source location.

**Parameters:**
- `message`: The core error description (e.g., "Variable 'x' is not defined")
- `line`: Optional line number where the error occurred
- `column`: Optional column number for precise location

**How It Works:**
1. Calls the base `Exception` constructor with a formatted message (via `FormatMessage`)
2. Stores the `Line` and `Column` properties for programmatic access

**Example Usage:**
```csharp
// From somewhere in TypeChecker.cs
throw new SemanticError(
    "Cannot assign 'str' to variable of type 'int'", 
    line: 15, 
    column: 5
);
```

---

### `FormatMessage` (Private Static Helper)

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

**Purpose:** Formats the error message in a consistent way based on available location information.

**Logic Flow:**
1. **Full location available** (line AND column): `"Semantic error at line 15, column 5: Variable 'x' is not defined"`
2. **Only line available**: `"Semantic error at line 15: Variable 'x' is not defined"`
3. **No location info**: `"Semantic error: Variable 'x' is not defined"`

**Design Rationale:** This graduated approach ensures users always get useful feedback, even when the compiler can't pinpoint the exact location. The format is also grep-friendly and follows common compiler error conventions.

**Why Static?** The method doesn't need instance state, and it's called from the constructor before the object is fully initialized. Making it static is both efficient and necessary.

---

## 4. Dependencies

### Internal Dependencies

This file has **minimal dependencies** by design:

**Namespace Dependencies:**
- None—only uses `System` namespace implicitly for `Exception`

**Consumed By:**
The `SemanticError` class is thrown throughout the semantic analysis phase:

- **`NameResolver.cs`**: When undefined names are referenced or names are redefined
- **`TypeResolver.cs`**: When type annotations can't be resolved
- **`TypeChecker.cs`**: For type mismatches, invalid operations, and type constraint violations
- **`ControlFlowValidator.cs`**: For control flow issues (e.g., unreachable code)
- **`ProtocolValidator.cs`**: When protocol implementations are invalid
- **`OperatorValidator.cs`**: For operator overload violations
- **`ImportResolver.cs`**: When imports fail or are circular
- **`Scope.cs`**: When attempting to redefine symbols in the same scope

### AST Node Connection

Although not directly imported, `SemanticError` is closely tied to AST nodes:

```csharp
// Typical usage pattern in semantic analysis
var node = /* some AST node */;
throw new SemanticError(
    "Type mismatch error", 
    node.LineStart,  // AST nodes carry location info
    node.ColumnStart
);
```

All AST nodes (from `Parser/Ast/Node.cs`) include `LineStart`, `ColumnStart`, `LineEnd`, and `ColumnEnd` properties, making it easy to create location-aware errors.

---

## 5. Patterns and Design Decisions

### Exception-Based Error Handling

**Pattern:** The Sharpy compiler uses exceptions (rather than error return codes or error objects) to handle semantic errors.

**Why This Approach?**
- **Fail-fast philosophy**: Semantic errors are unrecoverable—compilation must stop
- **Clean control flow**: No need to check return values at every step
- **Stack trace preservation**: Helpful for debugging the compiler itself
- **Idiomatic C#**: Aligns with .NET conventions

**Trade-off:** This means the compiler stops at the first error rather than collecting multiple errors. This is a deliberate choice for simplicity in the initial implementation.

### Immutable Properties

```csharp
public int? Line { get; }  // Get-only property
public int? Column { get; }
```

Once created, a `SemanticError` cannot be modified. This immutability prevents bugs where error details might be accidentally changed during error handling.

### Nullable Location Information

**Design Decision:** Using `int?` instead of requiring `int` or using sentinel values (like `-1`).

**Benefits:**
- **Explicit intent**: `null` clearly means "no location info," not "invalid location"
- **Type safety**: Can't accidentally compare against wrong sentinel value
- **Flexibility**: Allows throwing errors even when location tracking fails

**Example Scenario:** A module-level semantic error (like a circular import) might not have a meaningful line/column, so both can be `null`.

### Single Responsibility

This class does **exactly one thing**: represent a semantic error. It doesn't:
- Log errors (that's handled by logger classes)
- Collect multiple errors (potential future enhancement)
- Format error messages with color/styling (that's the CLI's job)

This focused design makes it easy to test, understand, and maintain.

---

## 6. Debugging Tips

### Finding Where Errors Are Thrown

**Quick Search:**
```bash
# Find all places where SemanticError is thrown
grep -r "throw new SemanticError" src/Sharpy.Compiler/Semantic/
```

**Common Throwers:**
- `TypeChecker.cs`: Most type-related errors
- `NameResolver.cs`: Undefined name errors
- `Scope.cs`: Symbol redefinition errors

### Understanding Error Context

When debugging a `SemanticError`, look at:

1. **The exception message**: What semantic rule was violated?
2. **Line and Column**: Where in the source code did it occur?
3. **Stack trace**: Which compiler component threw it?
4. **AST context**: What AST node was being processed?

**Debugging Pattern:**
```csharp
try
{
    // Your semantic analysis code
    typeChecker.Check(node);
}
catch (SemanticError e)
{
    Console.WriteLine($"Error: {e.Message}");
    Console.WriteLine($"Location: Line {e.Line}, Column {e.Column}");
    Console.WriteLine($"Stack: {e.StackTrace}");
}
```

### Testing Error Cases

When writing tests for semantic analysis:

```csharp
[Fact]
public void TestUndefinedVariable_ThrowsSemanticError()
{
    var source = "print(undefined_var)";
    var ex = Assert.Throws<SemanticError>(() => Compile(source));
    
    // Verify error details
    Assert.Contains("undefined_var", ex.Message);
    Assert.NotNull(ex.Line);
    Assert.NotNull(ex.Column);
}
```

### Adding Diagnostic Information

If you need more context during debugging, consider temporarily adding data to the exception:

```csharp
// Not in the current design, but could be added:
// public Dictionary<string, object>? Context { get; init; }

throw new SemanticError(
    "Type mismatch",
    line: node.LineStart,
    column: node.ColumnStart
)
{
    Context = new Dictionary<string, object>
    {
        ["ExpectedType"] = expectedType,
        ["ActualType"] = actualType,
        ["NodeType"] = node.GetType().Name
    }
};
```

---

## 7. Contribution Guidelines

### When to Modify This File

You should **rarely** need to modify `SemanticError.cs`. Consider changes only if:

1. **Adding structured error data**: E.g., error codes, severity levels, or context objects
2. **Changing error format**: If the project adopts a new error message standard
3. **Supporting error recovery**: If moving from fail-fast to error collection

### What NOT to Change

**Don't:**
- Change the class name (many files import it)
- Remove the nullable location properties (backward compatibility)
- Add business logic here (keep it a simple data container)

### Potential Enhancements

**Error Codes:**
```csharp
public class SemanticError : Exception
{
    public string ErrorCode { get; }  // E.g., "SE001", "SE042"
    
    public SemanticError(string code, string message, int? line = null, int? column = null)
        : base(FormatMessage(code, message, line, column))
    {
        ErrorCode = code;
        Line = line;
        Column = column;
    }
}
```

**Severity Levels:**
```csharp
public enum ErrorSeverity { Error, Warning }

public class SemanticError : Exception
{
    public ErrorSeverity Severity { get; }
    // ...
}
```

**Source File Context:**
```csharp
public class SemanticError : Exception
{
    public string? SourceFile { get; }
    public string? SourceLine { get; }  // The actual line of code
    // ...
}
```

**Multiple Error Collection:**
Instead of throwing immediately, some compilers collect errors:
```csharp
public class ErrorCollector
{
    private List<SemanticError> _errors = new();
    
    public void Report(SemanticError error) => _errors.Add(error);
    
    public void ThrowIfAny()
    {
        if (_errors.Any())
            throw new SemanticErrorCollection(_errors);
    }
}
```

### Testing Your Changes

If you do modify this file:

1. **Run all semantic tests:**
   ```bash
   dotnet test --filter "FullyQualifiedName~Semantic"
   ```

2. **Check error message format consistency:**
   ```bash
   # Compile a deliberately broken file
   dotnet run --project src/Sharpy.Cli -- build test-broken.spy
   ```

3. **Verify backward compatibility:**
   - All existing catch blocks still work
   - Error messages are still parseable by tools
   - Location information is preserved

### Code Review Checklist

When reviewing changes to `SemanticError.cs`:

- [ ] Does it maintain the simple, focused design?
- [ ] Are location properties still nullable?
- [ ] Is the error format consistent across all cases?
- [ ] Does it break any existing catch handlers?
- [ ] Are there tests for new error scenarios?
- [ ] Is documentation updated?

---

## 8. Related Files

To fully understand how `SemanticError` fits into the compiler, explore these related files:

- **`Semantic/TypeChecker.cs`**: Primary consumer; throws most type-related errors
- **`Semantic/NameResolver.cs`**: Throws undefined name errors
- **`Semantic/Scope.cs`**: Example of a simpler class that throws `SemanticError`
- **`Parser/Ast/Node.cs`**: Base AST node with location properties used in errors
- **`Compiler.cs`**: Top-level compiler that catches `SemanticError` and reports to users

**Next Steps for Learning:**
1. Read `Scope.cs` to see a simple usage example
2. Explore `TypeChecker.cs` to see complex error scenarios
3. Look at integration tests in `Sharpy.Compiler.Tests/Semantic/` to see error expectations

---

## Summary

`SemanticError.cs` is a small but essential part of the Sharpy compiler. It provides a clean, consistent way to represent semantic errors with optional location information. Its simplicity is a strength—it does exactly what's needed without unnecessary complexity.

**Key Takeaways:**
- ✅ Custom exception for semantic analysis phase
- ✅ Carries optional line/column location info
- ✅ Formats error messages consistently
- ✅ Thrown throughout semantic analysis components
- ✅ Fail-fast design: stops at first error
- ✅ Minimal dependencies, maximum reusability

When you see `throw new SemanticError(...)` in the codebase, you now know exactly what it does and why it's designed that way. Happy compiling! 🚀
