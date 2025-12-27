# Walkthrough: CodeGenException.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/CodeGenException.cs`

---

## Overview

`CodeGenException` is a custom exception class that represents errors occurring during the code generation phase of the Sharpy compiler. When the compiler transforms the Abstract Syntax Tree (AST) into C# code using Roslyn, it may encounter situations where valid Sharpy code cannot be translated to C# or where internal compiler errors occur. This exception provides a structured way to report those failures with detailed source location information.

**Key Purpose**: Provide rich error context to help developers (both compiler contributors and Sharpy end-users) understand exactly where and why code generation failed.

**Pipeline Context**: The exception is thrown in the **CodeGen** phase, which occurs after:
1. Lexer (tokenization)
2. Parser (AST construction)
3. Semantic Analysis (type checking, name resolution)

---

## Class Structure

### Inheritance Hierarchy

```csharp
System.Exception
    └── CodeGenException
```

`CodeGenException` extends the standard .NET `Exception` class, making it compatible with standard C# exception handling patterns.

### Properties

| Property | Type | Purpose |
|----------|------|---------|
| `Line` | `int?` | 1-based line number where the error occurred (nullable) |
| `Column` | `int?` | 1-based column number where the error occurred (nullable) |
| `Node` | `Node?` | Reference to the AST node that caused the error (nullable) |
| `Message` | `string` | Error description (inherited from `Exception`) |
| `InnerException` | `Exception?` | Optional wrapped exception (inherited from `Exception`) |

**Design Note**: All location properties are nullable (`int?`, `Node?`) because not all code generation errors have a specific source location. For example, an error in overall assembly structure might not map to a single line of code.

---

## Constructor Variants

The class provides five constructor overloads to handle different error reporting scenarios:

### 1. Message-Only Constructor

```csharp
public CodeGenException(string message) : base(message)
```

**Use Case**: Generic code generation errors without source location.

**Example**:
```csharp
throw new CodeGenException("Failed to initialize Roslyn workspace");
```

This is rare in practice because most code generation errors relate to specific source code constructs.

---

### 2. Message with Line/Column Constructor

```csharp
public CodeGenException(string message, int line, int column) : base(message)
{
    Line = line;
    Column = column;
}
```

**Use Case**: When you have location information but not the AST node reference.

**Example**:
```csharp
throw new CodeGenException("Invalid operator for type", 42, 15);
```

This might be used when working with cached location info rather than the original AST node.

---

### 3. Message with AST Node Constructor (Most Common)

```csharp
public CodeGenException(string message, Node node) : base(message)
{
    Node = node;
    Line = node.LineStart;
    Column = node.ColumnStart;
}
```

**Use Case**: The most common pattern during code generation. When processing an AST node and encountering an error, pass the node directly.

**Example**:
```csharp
// In RoslynEmitter.cs
var funcDef = (FunctionDef)node;
if (funcDef.ReturnType == null && !CanInferReturnType(funcDef))
{
    throw new CodeGenException(
        "Cannot infer return type for function", 
        funcDef
    );
}
```

**Why This Pattern?**
- Automatically extracts `LineStart` and `ColumnStart` from the node
- Preserves the node reference for debugging
- Simplifies error throwing code

---

### 4. Message with Inner Exception Constructor

```csharp
public CodeGenException(string message, Exception innerException) 
    : base(message, innerException)
```

**Use Case**: Wrapping lower-level exceptions (e.g., Roslyn API errors, reflection errors).

**Example**:
```csharp
try
{
    var syntaxTree = CSharpSyntaxTree.ParseText(generatedCode);
}
catch (Exception ex)
{
    throw new CodeGenException(
        "Failed to parse generated C# code", 
        ex
    );
}
```

This preserves the full exception chain for debugging.

---

### 5. Message with Line/Column and Inner Exception Constructor

```csharp
public CodeGenException(string message, int line, int column, Exception innerException)
    : base(message, innerException)
{
    Line = line;
    Column = column;
}
```

**Use Case**: Wrapping exceptions while preserving source location.

**Example**:
```csharp
catch (TypeResolutionException ex)
{
    throw new CodeGenException(
        $"Cannot resolve type '{typeName}' during code generation",
        currentLine,
        currentColumn,
        ex
    );
}
```

---

## Key Method: ToString()

```csharp
public override string ToString()
{
    if (Line.HasValue && Column.HasValue)
    {
        return $"CodeGen Error at {Line}:{Column}: {Message}";
    }
    return $"CodeGen Error: {Message}";
}
```

### Purpose

Provides user-friendly error messages in compiler output. The format matches typical compiler error conventions (e.g., `file.spy:42:15: error message`).

### Behavior

**With Location**:
```
CodeGen Error at 42:15: Cannot generate code for unsupported operator
```

**Without Location**:
```
CodeGen Error: Assembly generation failed
```

### Usage Context

This method is typically called when:
- The compiler CLI catches and displays exceptions
- Logging systems format error messages
- Developers inspect exceptions in debuggers

**Implementation Note**: The method checks for `Line` and `Column` together. If only one is present, it still uses the generic format. This prevents malformed output like "Error at 42:" without column.

---

## Dependencies

### Internal Dependencies

```csharp
using Sharpy.Compiler.Parser.Ast;
```

**Key Dependency**: `Node` class from the Parser namespace.

- All AST nodes inherit from `Node`
- `Node` provides `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd` properties
- These properties are set during parsing based on token positions

### External Dependencies

- **System.Exception**: Base class from .NET BCL
- No other external dependencies (intentionally lightweight)

---

## Patterns and Design Decisions

### 1. Nullable Location Properties

**Decision**: `Line`, `Column`, and `Node` are all nullable.

**Rationale**:
- Not all code generation errors map to specific source locations
- Assembly-level errors (e.g., missing references) don't have meaningful line numbers
- Makes the exception usable in more contexts

**Alternative Considered**: Separate exception classes (`CodeGenExceptionWithLocation`, `CodeGenExceptionWithNode`). Rejected as over-engineering for this use case.

---

### 2. AST Node Reference Preservation

**Decision**: Store the full `Node` reference, not just location numbers.

**Rationale**:
- Enables richer debugging (inspect node type, properties, children)
- Can reconstruct original source snippets for error messages
- Useful for IDE integrations and language server protocol (LSP)

**Memory Trade-off**: Exceptions may hold references to potentially large AST subtrees, but exceptions are only created on error paths (not performance-critical).

---

### 3. Constructor Overload Strategy

**Decision**: Five specific constructors rather than optional parameters or builder pattern.

**Rationale**:
- Clear, self-documenting API
- Follows .NET exception conventions
- No ambiguity about which parameters are set
- Compile-time checking of required parameters

**C# Pattern**: This mirrors the standard `Exception` class design (multiple constructors for different scenarios).

---

### 4. 1-Based Line/Column Numbers

**Decision**: Line and column numbers are 1-based (as documented in XML comments).

**Rationale**:
- Matches user expectations (text editors show line 1, not line 0)
- Consistent with most compiler error formats (GCC, Clang, C#, Python)
- Parser/Lexer already use 1-based positions in tokens

**Critical**: Always verify that upstream components (Lexer, Parser) are also using 1-based indexing to prevent off-by-one errors in error reporting.

---

### 5. No Stack Trace Manipulation

**Decision**: Does not override `StackTrace` or manipulate call stacks.

**Rationale**:
- Preserve genuine stack trace for debugging compiler issues
- Exception location properties (`Line`/`Column`) refer to *source code*, not stack frames
- Simpler implementation with standard .NET behavior

---

## Debugging Tips

### Catching CodeGenException

When debugging code generation issues:

```csharp
try
{
    var emitter = new RoslynEmitter();
    var csharpCode = emitter.Emit(astNode);
}
catch (CodeGenException ex)
{
    Console.WriteLine($"Error: {ex}");
    Console.WriteLine($"Node Type: {ex.Node?.GetType().Name}");
    Console.WriteLine($"Full Stack: {ex.StackTrace}");
    
    // If there's an inner exception, that's usually the root cause
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Root Cause: {ex.InnerException}");
    }
}
```

### Inspecting in Debugger

**Useful Watch Expressions**:
- `ex.Node` - See the AST node that failed
- `ex.Node.GetType()` - Identify the specific AST node type
- `ex.InnerException` - Check for wrapped exceptions
- `ex.Line + ":" + ex.Column` - Quick location format

### Common Debugging Scenarios

**Scenario 1: Exception Without Location**
```
CodeGen Error: Type resolution failed
```
- Problem: Can't pinpoint where in source code
- Solution: Add `Node` parameter when throwing:
  ```csharp
  // Before (bad)
  throw new CodeGenException("Type resolution failed");
  
  // After (good)
  throw new CodeGenException("Type resolution failed", currentNode);
  ```

**Scenario 2: Wrong Line Numbers in Errors**
- Check that Lexer/Parser are setting location properties correctly
- Verify 1-based vs 0-based indexing consistency
- Look for off-by-one errors in token positions

**Scenario 3: Lost Inner Exception Context**
- Always pass `innerException` when catching and rethrowing
- Use debugger to inspect full `InnerException` chain
- Check if Roslyn exceptions have useful `Data` dictionary entries

---

## Testing Strategy

The test file `CodeGenExceptionTests.cs` demonstrates best practices:

### Test Coverage Areas

1. **Basic Construction** - Each constructor variant works
2. **Property Initialization** - Location properties set correctly
3. **Node Location Extraction** - Line/column extracted from AST nodes
4. **ToString Formatting** - Location appears in formatted output
5. **Inner Exception Preservation** - Exception chaining works

### Example Test Pattern

```csharp
[Fact]
public void CodeGenException_WithNode_IncludesNodeLocation()
{
    // Arrange - Create a minimal AST node with location
    var node = new IntegerLiteral
    {
        Value = "42",
        LineStart = 15,
        ColumnStart = 10
    };

    // Act - Create exception with node
    var exception = new CodeGenException("Invalid integer", node);

    // Assert - Verify location extraction
    exception.Node.Should().Be(node);
    exception.Line.Should().Be(15);
    exception.Column.Should().Be(10);
    exception.ToString().Should().Contain("15:10");
}
```

**Testing Philosophy**: Test the public API contract (constructor behavior, property values, output format) rather than implementation details.

---

## Contribution Guidelines

### When to Throw CodeGenException

**DO** throw `CodeGenException` when:
- Encountering unsupported language constructs during emission
- Roslyn API calls fail during C# generation
- Type mapping fails (Sharpy type → C# type)
- Name mangling produces invalid C# identifiers
- Generated C# code would be syntactically or semantically invalid

**DON'T** throw `CodeGenException` for:
- Lexer errors (use `LexerException` if it exists, or create one)
- Parser errors (should be caught in parsing phase)
- Semantic analysis errors (type checking, name resolution)
- Runtime errors in generated code (that's user code bugs)

### Adding New Constructors

If you need to add a new constructor variant:

1. **Justify the need** - Can existing constructors be used?
2. **Follow the pattern** - Call base constructor, set properties
3. **Add XML documentation** - Describe the use case
4. **Add tests** - Test the new constructor in `CodeGenExceptionTests.cs`
5. **Update this walkthrough** - Document the new variant

**Example: Adding a Constructor for Multiple Nodes**

```csharp
/// <summary>
/// Create a code generation exception for errors spanning multiple nodes
/// </summary>
public CodeGenException(string message, Node startNode, Node endNode) 
    : base(message)
{
    Node = startNode; // Store primary node
    Line = startNode.LineStart;
    Column = startNode.ColumnStart;
    // Could add EndLine/EndColumn properties if needed
}
```

### Enhancing Error Messages

Consider contributing improvements to error messages:

**Current**:
```csharp
throw new CodeGenException("Invalid type", node);
// Output: CodeGen Error at 42:15: Invalid type
```

**Enhanced**:
```csharp
throw new CodeGenException(
    $"Invalid type '{typeName}': expected reference type but got value type",
    node
);
// Output: CodeGen Error at 42:15: Invalid type 'int': expected reference type but got value type
```

**Best Practices for Error Messages**:
- Include specific values that caused the error
- Explain *why* it's an error (not just *what* failed)
- Suggest fixes when possible
- Use consistent terminology with Sharpy language specification

### Extending Location Information

Potential enhancements to consider:

**1. End Position Tracking**
```csharp
public int? LineEnd { get; }
public int? ColumnEnd { get; }

// In constructor with node:
LineEnd = node.LineEnd;
ColumnEnd = node.ColumnEnd;
```

Use case: Multi-line constructs where both start and end are important.

**2. Source File Path**
```csharp
public string? SourceFile { get; }
```

Use case: Multi-file projects where line numbers alone are ambiguous.

**3. Source Code Snippet**
```csharp
public string? SourceSnippet { get; }
```

Use case: Including the actual problematic code line in error output (like Python's tracebacks).

### Code Style Alignment

Follow existing patterns in this file:
- XML documentation on all public members
- Nullable reference types (`Node?`, `int?`)
- Constructor chaining (`:base(...)`)
- Concise property initializers (`{ get; }`)
- Consistent formatting (see `.editorconfig`)

---

## Related Code to Explore

After understanding `CodeGenException`, explore these related components:

### 1. RoslynEmitter.cs
**Location**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

This is where most `CodeGenException` instances are thrown. It transforms AST nodes into Roslyn syntax trees.

**Key Pattern**:
```csharp
public SyntaxNode EmitExpression(Expression expr)
{
    return expr switch
    {
        BinaryOp binOp => EmitBinaryOp(binOp),
        FunctionCall call => EmitFunctionCall(call),
        _ => throw new CodeGenException(
            $"Unsupported expression type: {expr.GetType().Name}",
            expr
        )
    };
}
```

### 2. TypeMapper.cs
**Location**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

Maps Sharpy types to C# types. Throws `CodeGenException` when mappings fail.

### 3. Node.cs (AST Base Class)
**Location**: `src/Sharpy.Compiler/Parser/Ast/Node.cs`

Defines the base `Node` class with location properties that `CodeGenException` depends on.

### 4. Compiler.cs
**Location**: `src/Sharpy.Compiler/Compiler.cs`

The main compiler pipeline that catches and handles `CodeGenException` instances.

---

## Real-World Usage Example

Here's a realistic example of how `CodeGenException` fits into the code generation flow:

```csharp
// In RoslynEmitter.cs
private SyntaxNode EmitFunctionCall(FunctionCall call)
{
    // Resolve the function being called
    var functionSymbol = _symbolTable.Lookup(call.FunctionName);
    
    if (functionSymbol == null)
    {
        // This should have been caught in semantic analysis, but defense in depth
        throw new CodeGenException(
            $"Undefined function '{call.FunctionName}'",
            call
        );
    }
    
    // Map Sharpy types to C# types
    TypeSyntax returnType;
    try
    {
        returnType = _typeMapper.MapType(functionSymbol.ReturnType);
    }
    catch (Exception ex)
    {
        throw new CodeGenException(
            $"Cannot map return type of function '{call.FunctionName}'",
            call,
            ex  // Preserve the original error
        );
    }
    
    // Generate argument list
    var arguments = new List<ArgumentSyntax>();
    foreach (var arg in call.Arguments)
    {
        var argSyntax = EmitExpression(arg);  // Recursive - may throw
        arguments.Add(SyntaxFactory.Argument(argSyntax));
    }
    
    // Build the invocation expression
    return SyntaxFactory.InvocationExpression(
        SyntaxFactory.IdentifierName(
            _nameMangler.MangleName(call.FunctionName)
        ),
        SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(arguments)
        )
    );
}
```

**Error Propagation Flow**:
1. AST node passed to emitter method
2. Error detected (missing symbol, type mapping failure, etc.)
3. `CodeGenException` thrown with node reference
4. Exception propagates up through recursive emit calls
5. Top-level `Compiler.cs` catches and formats for user display

---

## Frequently Asked Questions

### Q: Why not use standard .NET exceptions like `InvalidOperationException`?

**A**: Custom exceptions provide:
- Domain-specific semantics (clearly a compiler error, not a general .NET error)
- Structured location information (line/column/node)
- Consistent error formatting across the compiler
- Easier filtering in exception handlers (catch only compiler errors)

### Q: Should I always include a Node when throwing?

**A**: Include it when available. Only use the message-only constructor for:
- Assembly-level errors without corresponding source code
- Internal compiler errors not related to specific AST nodes
- Errors during initialization before AST is constructed

### Q: What if I have a Node but no specific error location in that node?

**A**: Still pass the node. The exception will use the node's start position, which is better than no location. Users can navigate to the general area of the problem.

### Q: Can I modify the exception after creation?

**A**: No, the properties are `get`-only. This is intentional—exceptions should be immutable once created. If you need different information, create a new exception instance.

### Q: How do I format multi-line error messages?

**A**: Just use newlines in the message string:
```csharp
throw new CodeGenException(
    "Type mismatch in function call:\n" +
    $"  Expected: {expectedType}\n" +
    $"  Got: {actualType}",
    call
);
```

The `ToString()` method will include the full message.

---

## Summary

`CodeGenException` is a purpose-built exception type for the Sharpy compiler's code generation phase. Its design reflects key compiler engineering principles:

- **Location-aware error reporting** for better user experience
- **AST node preservation** for debugging and tooling
- **Exception chaining** for root cause analysis
- **Flexible construction** for various error scenarios
- **Standard .NET patterns** for familiarity and interoperability

When working with code generation, think of this exception as your primary communication channel for reporting "I can't generate valid C# code for this Sharpy construct." Always provide as much context as possible (especially the AST node) to help users and fellow developers understand and fix the issue.

---

**Next Steps**: 
- Explore `RoslynEmitter.cs` to see `CodeGenException` in action
- Review `CodeGenExceptionTests.cs` for testing patterns
- Study the `Node` class hierarchy in `Parser/Ast/`
- Read the code generation architecture documentation (if available)

**Questions or Improvements?** This walkthrough is a living document. If you find gaps or have suggestions, contribute updates alongside your code changes!
