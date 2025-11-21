# Walkthrough: CodeGenException.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/CodeGenException.cs`

---

## 1. Overview

`CodeGenException` is a specialized exception class that represents errors occurring during the **code generation phase** of the Sharpy compiler. This is the final stage of compilation where the Abstract Syntax Tree (AST) is transformed into C# code using Roslyn.

### Purpose
- Provide meaningful error messages when code generation fails
- Capture source location information (line and column) to help users pinpoint issues
- Maintain a reference to the problematic AST node for debugging
- Support exception chaining for root cause analysis

### When Is This Thrown?
This exception is thrown by `RoslynEmitter` and related code generation components when they encounter:
- Unsupported language features or AST nodes
- Invalid type mappings
- Name collision issues that can't be resolved
- Internal errors during C# code synthesis
- Roslyn compilation failures

---

## 2. Class Structure

### Inheritance Hierarchy
```csharp
System.Exception
    └── CodeGenException
```

`CodeGenException` extends the standard .NET `Exception` class, making it compatible with existing error handling infrastructure while adding compiler-specific context.

### Properties

#### `Line` (int?)
```csharp
public int? Line { get; }
```
- **Purpose**: Captures the line number in the source Sharpy file where the error occurred
- **Type**: Nullable int (`int?`) - allows for cases where location is unknown
- **1-based indexing**: Line 1 is the first line of the file (matches user expectations and editor conventions)
- **Read-only**: Set via constructors only, ensuring immutability

#### `Column` (int?)
```csharp
public int? Column { get; }
```
- **Purpose**: Captures the column number (character position) in the source line
- **Type**: Nullable int (`int?`) - may be unavailable in some error scenarios
- **1-based indexing**: Column 1 is the first character of the line
- **Read-only**: Immutable after construction

#### `Node` (Node?)
```csharp
public Node? Node { get; }
```
- **Purpose**: Reference to the AST node that caused the code generation failure
- **Type**: Nullable `Node` from `Sharpy.Compiler.Parser.Ast`
- **Usage**: Provides full context for debugging - you can inspect the node type, children, attributes, etc.
- **Relationship to Line/Column**: When a `Node` is provided, `Line` and `Column` are automatically extracted from it

---

## 3. Constructors (Five Overloads)

The class provides five constructors to handle different error scenarios. This follows the **constructor chaining** pattern for flexibility.

### 3.1 Simple Message Constructor
```csharp
public CodeGenException(string message) : base(message)
```
**Use Case**: Basic errors where location is unknown or irrelevant
```csharp
throw new CodeGenException("Code generation not yet implemented for this feature");
```

### 3.2 Message + Location Constructor
```csharp
public CodeGenException(string message, int line, int column) : base(message)
```
**Use Case**: When you have location info but no AST node reference
```csharp
throw new CodeGenException("Invalid type mapping", 42, 15);
```
**Sets**: `Line` and `Column` properties

### 3.3 Message + AST Node Constructor (Most Common)
```csharp
public CodeGenException(string message, Node node) : base(message)
```
**Use Case**: The most common pattern - you have the problematic AST node
```csharp
throw new CodeGenException("Unsupported expression type", expressionNode);
```
**Automatically extracts**:
- `Line` from `node.LineStart`
- `Column` from `node.ColumnStart`
- Stores the `Node` reference for debugging

**Why This Is Important**: Most code generation code operates on AST nodes, so this constructor makes it easy to create rich exceptions without manual extraction of location data.

### 3.4 Message + Inner Exception Constructor
```csharp
public CodeGenException(string message, Exception innerException) : base(message, innerException)
```
**Use Case**: Wrapping exceptions from underlying systems (like Roslyn)
```csharp
catch (RoslynException ex)
{
    throw new CodeGenException("Failed to emit C# code", ex);
}
```
**Preserves**: The original exception stack trace and details

### 3.5 Message + Location + Inner Exception Constructor
```csharp
public CodeGenException(string message, int line, int column, Exception innerException)
    : base(message, innerException)
```
**Use Case**: Wrapping exceptions while adding source location context
```csharp
catch (InvalidOperationException ex)
{
    throw new CodeGenException("Type resolution failed", 25, 10, ex);
}
```
**Combines**: Location tracking with exception chaining

---

## 4. Key Methods

### `ToString()` - Formatted Error Output
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

**Purpose**: Provides a human-readable error message formatted for compiler output

**Output Examples**:
```
CodeGen Error at 42:15: Unsupported operator '@=' for type 'MyClass'
CodeGen Error: Internal compiler error during code generation
```

**Design Decision**: The method checks for location availability before including it, ensuring graceful handling of errors without source location.

**Why Override ToString()?**: While `Exception.Message` exists, `ToString()` is what gets called by default when printing exceptions, making this the right place for custom formatting.

---

## 5. Dependencies

### Direct Dependencies
- **`System.Exception`**: Base class providing core exception functionality
- **`Sharpy.Compiler.Parser.Ast.Node`**: AST node type for the `Node` property

### Indirect Dependencies (via usage)
The exception is **caught and thrown by**:
- **`RoslynEmitter`**: Main code generation engine
- **`TypeMapper`**: Type conversion logic (Sharpy → C#)
- **`NameMangler`**: Name collision resolution
- **`CodeValidator`**: Generated code validation
- **`AssemblyCompiler`**: Multi-file compilation orchestration

### Integration Points
```csharp
// Typical usage in RoslynEmitter
public override string Visit(SomeNode node)
{
    if (!IsSupported(node))
    {
        throw new CodeGenException(
            $"Unsupported {node.GetType().Name}", 
            node
        );
    }
    // ... code generation logic
}
```

---

## 6. Patterns and Design Decisions

### 6.1 Nullable Properties Pattern
```csharp
public int? Line { get; }
public Node? Node { get; }
```
**Rationale**: Not all errors have source location or AST node context (e.g., internal compiler errors, assembly-level failures). Nullability makes this explicit.

### 6.2 Immutability
All properties are `get`-only, set via constructors. This ensures:
- Thread safety (exception objects might be logged from multiple threads)
- Predictable state (exception details can't be accidentally modified)
- Cleaner reasoning about exception flow

### 6.3 Constructor Overloading for Convenience
Five constructors cover different scenarios without requiring callers to pass `null` for unused parameters. Compare:
```csharp
// With overloads (clean)
throw new CodeGenException("Error", node);

// Without overloads (messy)
throw new CodeGenException("Error", null, null, node, null);
```

### 6.4 Automatic Location Extraction
```csharp
Line = node.LineStart;
Column = node.ColumnStart;
```
The constructor extracts location from the node automatically, following the **DRY principle** (Don't Repeat Yourself).

### 6.5 Compiler Error Convention
The `ToString()` method follows a common compiler error format:
```
<ErrorType> at <Line>:<Column>: <Message>
```
This matches conventions from GCC, Clang, Rust, and other modern compilers.

---

## 7. Debugging Tips

### 7.1 Inspecting Thrown Exceptions
When debugging a `CodeGenException`, check these in order:

1. **Message**: What went wrong?
2. **Line/Column**: Where in the source file?
3. **Node**: What AST node caused it? (Inspect type, attributes, children)
4. **InnerException**: Was this wrapping another error?
5. **StackTrace**: Where in the compiler was it thrown?

### 7.2 Common Debugging Workflow
```csharp
catch (CodeGenException ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Location: {ex.Line}:{ex.Column}");
    
    if (ex.Node != null)
    {
        Console.WriteLine($"Node Type: {ex.Node.GetType().Name}");
        // Use AstDumper for detailed inspection
        var dumper = new AstDumper();
        Console.WriteLine(dumper.Dump(ex.Node));
    }
    
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Caused by: {ex.InnerException.Message}");
    }
}
```

### 7.3 Finding the Source
Use the `Line` and `Column` properties to jump directly to the problematic source code:
```bash
# In VS Code or similar editors
code --goto file.spy:42:15
```

### 7.4 AST Node Inspection
The `Node` property is invaluable for understanding **why** code generation failed:
- Check if the node type is expected
- Verify node attributes are set correctly
- Inspect child nodes for issues
- Use the AST dumper utility for visualization

### 7.5 Testing Error Cases
When writing tests for code generation errors:
```csharp
[Fact]
public void TestUnsupportedFeature_ThrowsCodeGenException()
{
    var source = "unsupported syntax";
    var ex = Assert.Throws<CodeGenException>(() => Compile(source));
    
    Assert.Contains("Unsupported", ex.Message);
    Assert.NotNull(ex.Line);  // Should have location
    Assert.NotNull(ex.Node);  // Should have AST context
}
```

---

## 8. Contribution Guidelines

### 8.1 When to Use This Exception
**DO use `CodeGenException` when**:
- A feature is not yet implemented in the code generator
- Type mapping fails
- Name mangling encounters unresolvable conflicts
- Roslyn rejects the generated C# code
- Internal code generation invariants are violated

**DON'T use `CodeGenException` for**:
- Lexer errors (use `LexerException`)
- Parser errors (use `ParserException`)
- Semantic analysis errors (use `SemanticException`)
- Runtime errors in compiled programs (those should be in the generated code)

### 8.2 Adding New Constructors
If you need a new constructor pattern:
1. Ensure it's not already covered by existing overloads
2. Follow the immutability pattern (set properties in constructor)
3. Chain to the base `Exception` constructor
4. Add XML documentation comments
5. Write tests demonstrating the new constructor's use case

### 8.3 Enhancing Error Messages
When improving error messages in the compiler:
```csharp
// ❌ Vague
throw new CodeGenException("Error", node);

// ✅ Specific and actionable
throw new CodeGenException(
    $"Operator '{op}' is not supported for type '{typeName}'. " +
    $"Consider implementing the '{GetDunderMethod(op)}' method.",
    node
);
```

### 8.4 Adding Properties
If you need to add properties (e.g., `ErrorCode`, `Severity`):
1. Make them nullable if not always applicable
2. Make them read-only (`get`-only)
3. Set via constructor parameters
4. Update `ToString()` if the property should appear in output
5. Update all constructor overloads consistently
6. Write tests for the new property

### 8.5 Testing Best Practices
When testing code that throws `CodeGenException`:
```csharp
[Fact]
public void TestErrorScenario()
{
    var source = "problematic code";
    
    var ex = Assert.Throws<CodeGenException>(() => 
        GenerateCode(source)
    );
    
    // Verify all relevant properties
    Assert.Contains("expected error text", ex.Message);
    Assert.NotNull(ex.Line);
    Assert.NotNull(ex.Column);
    Assert.NotNull(ex.Node);
}
```

**Never artificially make tests pass** by:
- Removing assertions
- Changing expected error messages to match bugs
- Commenting out failing tests

Always **fix the root cause** in the code generator.

### 8.6 Example: Adding a Feature
When adding a new code generation feature:

1. **Handle unsupported cases gracefully**:
```csharp
public override string Visit(NewFeatureNode node)
{
    if (!CanGenerate(node))
    {
        throw new CodeGenException(
            $"Feature '{node.FeatureName}' not yet supported in code generation",
            node
        );
    }
    // ... implementation
}
```

2. **Write tests for both success and failure**:
```csharp
[Fact]
public void TestNewFeature_Supported()
{
    var result = GenerateCode("valid new feature");
    Assert.Contains("expected C# code", result);
}

[Fact]
public void TestNewFeature_Unsupported_ThrowsCodeGenException()
{
    var ex = Assert.Throws<CodeGenException>(() => 
        GenerateCode("unsupported variant")
    );
    Assert.Contains("not yet supported", ex.Message);
}
```

---

## 9. Real-World Usage Examples

### Example 1: Unsupported AST Node
```csharp
public override string Visit(MatchStmt node)
{
    throw new CodeGenException(
        "Pattern matching (match statements) not yet implemented in code generation",
        node
    );
}
```

### Example 2: Type Mapping Failure
```csharp
public string MapType(TypeNode typeNode)
{
    var csType = TryMapType(typeNode);
    if (csType == null)
    {
        throw new CodeGenException(
            $"Cannot map Sharpy type '{typeNode.Name}' to C# type",
            typeNode
        );
    }
    return csType;
}
```

### Example 3: Wrapping Roslyn Errors
```csharp
try
{
    var compilation = CSharpCompilation.Create(...);
    var emitResult = compilation.Emit(stream);
    
    if (!emitResult.Success)
    {
        var errors = string.Join("\n", emitResult.Diagnostics);
        throw new CodeGenException(
            $"Roslyn compilation failed:\n{errors}",
            currentNode
        );
    }
}
catch (Exception ex) when (ex is not CodeGenException)
{
    throw new CodeGenException(
        "Internal error during C# code emission",
        currentNode,
        ex
    );
}
```

### Example 4: Assembly-Level Errors (No Location)
```csharp
if (modules.Count == 0)
{
    throw new CodeGenException(
        "Cannot generate code: no modules to compile"
    );
}
```

---

## 10. Related Files and Further Reading

### Related Exception Classes
- **`LexerException`**: Tokenization errors
- **`ParserException`**: Syntax parsing errors  
- **`SemanticException`**: Type checking and analysis errors
- **`CompilationException`**: Top-level compilation orchestration errors

### Related Code Generation Files
- **`RoslynEmitter.cs`**: Main code generator (primary thrower of this exception)
- **`TypeMapper.cs`**: Type system mapping
- **`NameMangler.cs`**: Name collision resolution
- **`CodeValidator.cs`**: Generated code validation

### Documentation
- **Compiler Guide**: `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
- **Testing Guide**: `.github/instructions/Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md`
- **Architecture**: `docs/architecture/`

### Tests
Look at `src/Sharpy.Compiler.Tests/CodeGen/` for examples of how this exception is tested and used in practice.

---

## Summary

`CodeGenException` is a simple but crucial component of the Sharpy compiler's error handling infrastructure. Its design prioritizes:

- **Developer experience**: Clear error messages with source locations
- **Debuggability**: AST node references for deep inspection  
- **Flexibility**: Multiple constructors for different scenarios
- **Consistency**: Standard compiler error format

When working with code generation in Sharpy, this exception will be your primary tool for reporting failures. Always include as much context as possible (especially the AST node) to make debugging easier for yourself and other contributors.
