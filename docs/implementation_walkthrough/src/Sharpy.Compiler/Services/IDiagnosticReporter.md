# Walkthrough: IDiagnosticReporter.cs

**Source File**: `src/Sharpy.Compiler/Services/IDiagnosticReporter.cs`

---

## Overview

`IDiagnosticReporter` is a **service interface** that provides a standardized API for reporting compilation diagnostics (errors and warnings) throughout the Sharpy compiler pipeline. This interface is the primary abstraction for error reporting, allowing all compiler components to report issues in a consistent, centralized manner.

**Role in Compiler Pipeline:**
```
Lexer → Parser → Semantic Analysis → CodeGen
   ↓        ↓            ↓              ↓
   └────────┴────────────┴──────────────→ IDiagnosticReporter
                                              ↓
                                         DiagnosticBag
                                              ↓
                                         CLI Output
```

This interface sits **orthogonally** to the main compilation pipeline, acting as a service that all stages can consume via dependency injection.

---

## Class/Type Structure

### Interface Definition

```csharp
public interface IDiagnosticReporter
{
    // Error reporting methods
    void ReportError(string message, int? line = null, int? column = null);
    void ReportError(string message, Node node);

    // Warning reporting methods
    void ReportWarning(string message, int? line = null, int? column = null);
    void ReportWarning(string message, Node node);

    // Diagnostic access
    DiagnosticBag Diagnostics { get; }
    bool HasErrors { get; }

    // Context tracking
    string? CurrentFilePath { get; set; }
}
```

### Design Pattern: Interface Segregation

This is a **pure interface** with no implementation details. The concrete implementation is `DiagnosticReporter` (see cross-references below), which handles the actual storage and logging of diagnostics.

**Why an interface?**
- **Testability**: Easy to mock for unit tests
- **Flexibility**: Different implementations for different contexts (e.g., batch compiler, LSP server, test harness)
- **Dependency Injection**: Clean separation of concerns in the compiler services architecture

---

## Key Methods

### 1. `ReportError` (Position-Based)

```csharp
void ReportError(string message, int? line = null, int? column = null);
```

**Purpose**: Report a compilation error with optional source location.

**Parameters:**
- `message`: Human-readable error description
- `line`: 1-based line number (nullable for context-free errors)
- `column`: 1-based column number (nullable if only line is known)

**Usage Example:**
```csharp
reporter.ReportError("Expected ':' after 'if' condition", line: 42, column: 18);
```

**When to use:**
- When you have raw line/column information (e.g., from the lexer)
- For errors that don't map to a specific AST node
- During lexing or tokenization phases

---

### 2. `ReportError` (Node-Based)

```csharp
void ReportError(string message, Node node);
```

**Purpose**: Report an error at the location of an AST node.

**Parameters:**
- `message`: Error description
- `node`: Any AST node (extracts `LineStart` and `ColumnStart` automatically)

**Usage Example:**
```csharp
// In semantic analysis
if (!IsValidType(typeAnnotation))
{
    reporter.ReportError("Unknown type", typeAnnotation);
}
```

**When to use:**
- During parsing, semantic analysis, or code generation
- When you have an AST node representing the problematic code
- **Preferred approach** for pipeline stages that work with the AST

**Implementation Detail**: This is a convenience overload that extracts location from `node.LineStart` and `node.ColumnStart`.

---

### 3. `ReportWarning` Methods

```csharp
void ReportWarning(string message, int? line = null, int? column = null);
void ReportWarning(string message, Node node);
```

**Purpose**: Same as `ReportError`, but for non-fatal diagnostics.

**When to use:**
- Deprecated features
- Suspicious but valid code patterns
- Performance hints
- Style violations

**Key Difference**: Warnings don't prevent compilation from completing, but errors do (via `HasErrors` check).

---

### 4. `Diagnostics` Property

```csharp
DiagnosticBag Diagnostics { get; }
```

**Purpose**: Access the underlying collection of all diagnostics.

**Returns**: `DiagnosticBag` containing all errors, warnings, and other diagnostics reported so far.

**Usage Example:**
```csharp
if (reporter.HasErrors)
{
    foreach (var error in reporter.Diagnostics.GetErrors())
    {
        Console.WriteLine(error.ToString());
    }
}
```

**When to use:**
- At the end of compilation to display all diagnostics
- To serialize diagnostics for IDE integration
- To aggregate diagnostics from multiple modules

---

### 5. `HasErrors` Property

```csharp
bool HasErrors { get; }
```

**Purpose**: Quick check if any errors have been reported.

**Returns**: `true` if at least one error exists, `false` otherwise.

**Critical Usage**: This is the **gatekeeper** for continuing compilation:

```csharp
// Typical compiler flow
parser.Parse(tokens);
if (reporter.HasErrors) return; // Stop after parse errors

semanticAnalyzer.Analyze(ast);
if (reporter.HasErrors) return; // Stop after semantic errors

codeGenerator.Emit(ast);
```

**Performance**: Backed by `DiagnosticBag.HasErrors`, which uses a simple LINQ `Any()` check on the error list.

---

### 6. `CurrentFilePath` Property

```csharp
string? CurrentFilePath { get; set; }
```

**Purpose**: Track which source file is currently being compiled.

**Why mutable?**: A single reporter instance is often reused across multiple files in a project compilation.

**Usage Pattern:**
```csharp
foreach (var file in projectFiles)
{
    reporter.CurrentFilePath = file.Path;
    CompileFile(file, reporter);
}
```

**Effect**: The current file path is automatically included in diagnostics created while this property is set, enabling messages like:

```
src/main.spy(42,18): error: Expected ':' after 'if' condition
```

---

## Dependencies

### Internal Dependencies

1. **`Sharpy.Compiler.Diagnostics`**
   - `DiagnosticBag`: Thread-safe collection of diagnostics
   - `CompilerDiagnostic`: Individual diagnostic record
   - `CompilerDiagnosticSeverity`: Enum (Error, Warning, Info, Hint)

2. **`Sharpy.Compiler.Parser.Ast`**
   - `Node`: Base class for all AST nodes (provides `LineStart`, `ColumnStart`, etc.)

### Downstream Consumers

Every major compiler component uses this interface:
- **Lexer**: Tokenization errors (unexpected characters, unterminated strings)
- **Parser**: Syntax errors (missing tokens, malformed expressions)
- **Semantic Analyzer**: Type errors, undefined variables, signature mismatches
- **Code Generator**: Unsupported constructs, target limitations
- **Validation Pipeline**: Protocol conformance, axiom violations

---

## Patterns and Design Decisions

### 1. **Overload Pattern: Dual Reporting APIs**

The interface provides **two ways** to report each diagnostic type:
- **Raw coordinates**: `ReportError(message, line, column)`
- **AST node**: `ReportError(message, node)`

**Rationale**: Different compiler stages have different information available:
- **Lexer**: Only has token positions (raw coordinates)
- **Parser/Semantic**: Works with AST nodes (node-based is more convenient)

This design eliminates boilerplate like:
```csharp
// Without node overload (tedious)
reporter.ReportError("Type mismatch", expr.LineStart, expr.ColumnStart);

// With node overload (clean)
reporter.ReportError("Type mismatch", expr);
```

---

### 2. **Optional Parameters for Flexibility**

```csharp
void ReportError(string message, int? line = null, int? column = null);
```

**Why nullable?**
- Some errors are **context-free** (e.g., "Project file not found")
- Sometimes only line is known (e.g., end-of-file errors)
- Defaults to `null` for maximum flexibility

**Output Format:**
- `(line, column)`: Both specified → `file.spy(42,18): error: message`
- `(line)`: Line only → `file.spy(42): error: message`
- No location → `file.spy: error: message`

---

### 3. **Immutable AST Philosophy**

Notice that this interface **does not modify AST nodes**. The diagnostic system is completely separate from the AST structure.

**Why?** The Sharpy compiler follows an **immutable AST** principle:
- AST nodes are `record` types (immutable by default)
- Semantic information goes in `SemanticInfo` dictionaries, not AST annotations
- Diagnostics are stored externally in `DiagnosticBag`

This allows the same AST to be analyzed multiple times without side effects.

---

### 4. **Thread Safety (Future-Proofing)**

While not directly visible in the interface, the underlying `DiagnosticBag` is thread-safe (uses locks). This enables:
- **Parallel module compilation** (future optimization)
- **Async semantic checks** (LSP scenarios)
- **Concurrent validation pipelines**

The interface itself is designed to be thread-safe when used with a thread-safe implementation.

---

## Debugging Tips

### 1. **Trace Error Reporting**

When debugging why an error appears (or doesn't appear):

```csharp
// Add breakpoint here in DiagnosticReporter.ReportError
public void ReportError(string message, int? line = null, int? column = null)
{
    _diagnostics.AddError(message, line, column, CurrentFilePath); // ← Breakpoint
    _logger.LogError(message, line ?? 0, column ?? 0);
}
```

Call stack will show you exactly which compiler component reported the error.

---

### 2. **Check CurrentFilePath**

If diagnostics show wrong file paths:

```csharp
// Before compiling each file
Console.WriteLine($"Compiling: {reporter.CurrentFilePath}");
```

Ensure `CurrentFilePath` is set correctly before each file compilation.

---

### 3. **Distinguish Error vs. Warning**

```csharp
// At end of compilation
Console.WriteLine($"Errors: {reporter.Diagnostics.ErrorCount}");
Console.WriteLine($"Warnings: {reporter.Diagnostics.WarningCount}");
```

Remember: `HasErrors` only checks **errors**, not warnings. Warnings don't stop compilation.

---

### 4. **Missing Diagnostics**

If expected errors don't appear:
- Check if the code path actually calls `ReportError`
- Verify `reporter` instance is correctly injected (not a different instance)
- Ensure `DiagnosticBag` isn't cleared prematurely
- Check if error is reported but with wrong severity (warning instead of error)

---

### 5. **Duplicate Diagnostics**

If the same error appears multiple times:
- Each validation pass may report independently
- Check if multiple validators are analyzing the same node
- Consider deduplication logic in higher-level orchestration code

---

## Contribution Guidelines

### When to Modify This Interface

**DO modify** if you need to:
- Add new diagnostic severity levels (e.g., `ReportHint`, `ReportInfo`)
- Support structured error codes (e.g., `ReportError("SHP001", message, node)`)
- Add batch reporting methods (e.g., `ReportMultiple(IEnumerable<Diagnostic>)`)
- Extend location information (e.g., span ranges instead of single positions)

**DO NOT modify** for:
- Implementation-specific details (those go in `DiagnosticReporter.cs`)
- Logging behavior (handled by `ICompilerLogger`)
- Output formatting (handled by `CompilerDiagnostic.ToString()`)

---

### Adding New Diagnostic Methods

If adding a new method (e.g., `ReportInfo`):

1. **Add to interface**:
   ```csharp
   void ReportInfo(string message, int? line = null, int? column = null);
   void ReportInfo(string message, Node node);
   ```

2. **Update `DiagnosticReporter` implementation** (see cross-reference)

3. **Add to `DiagnosticBag`** if needed (e.g., `GetInfo()` method)

4. **Update tests**:
   - `DiagnosticReporterTests.cs`
   - `DiagnosticBagTests.cs`

---

### Maintaining Interface Stability

This interface is used **everywhere** in the compiler. Breaking changes require updating:
- All compiler stages (Lexer, Parser, Semantic, CodeGen)
- Validation pipeline components
- Test infrastructure
- CLI integration

**Best practice**: Add new methods instead of changing signatures. Use optional parameters for backward compatibility.

---

## Cross-References

### Related Files

1. **`DiagnosticReporter.cs`** ([docs](DiagnosticReporter.md))
   - Concrete implementation of this interface
   - Handles actual diagnostic storage and logger integration

2. **`DiagnosticBag.cs`** ([source](../../Diagnostics/DiagnosticBag.cs))
   - Thread-safe diagnostic collection
   - Contains `CompilerDiagnostic` record and `CompilerDiagnosticSeverity` enum

3. **`CompilerServices.cs`** ([docs](CompilerServices.md))
   - Service locator that provides `IDiagnosticReporter` instances
   - Dependency injection setup

4. **`Node.cs`** ([source](../../Parser/Ast/Node.cs))
   - Base AST node with location properties (`LineStart`, `ColumnStart`, etc.)
   - Used by the node-based reporting overloads

### Usage Examples in the Codebase

Search for `IDiagnosticReporter` to find real-world usage:
```bash
grep -r "IDiagnosticReporter" src/Sharpy.Compiler/
```

Common patterns:
- Constructor injection: `public TypeChecker(IDiagnosticReporter reporter)`
- Service locator: `services.DiagnosticReporter.ReportError(...)`
- Method parameters: `void Validate(..., IDiagnosticReporter reporter)`

---

## Summary

`IDiagnosticReporter` is a **foundational service interface** that centralizes error reporting across the entire Sharpy compiler. Key takeaways:

- **Two reporting styles**: Raw coordinates (lexer-friendly) and AST nodes (parser-friendly)
- **Severity levels**: Errors stop compilation, warnings don't
- **Context tracking**: `CurrentFilePath` for multi-file projects
- **Immutable design**: Diagnostics are separate from AST structure
- **Thread-safe**: Ready for future parallel compilation

For implementation details, see `DiagnosticReporter.cs`. For diagnostic storage, see `DiagnosticBag.cs`.
