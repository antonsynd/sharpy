# Walkthrough: DiagnosticReporter.cs

**Source File**: `src/Sharpy.Compiler/Services/DiagnosticReporter.cs`

---

## Overview

The `DiagnosticReporter` is a simple yet critical service class that provides centralized, consistent error and warning reporting across the Sharpy compiler. It acts as a facade over the underlying `DiagnosticBag` collection, combining diagnostic collection with optional logging capabilities.

**Role in the Compiler Pipeline:**
- **Used By**: All compiler phases (Lexer, Parser, Semantic Analysis, Code Generation)
- **Purpose**: Centralized error/warning reporting with location tracking
- **Output**: Populated `DiagnosticBag` containing all errors and warnings
- **Key Benefit**: Decouples diagnostic collection from diagnostic formatting and logging

**Key Responsibilities:**
1. Report errors and warnings with source location information
2. Associate diagnostics with AST node positions automatically
3. Track the current file being compiled for diagnostic messages
4. Optionally log diagnostics to `ICompilerLogger` for debugging
5. Provide error status checks (`HasErrors`) for early termination

---

## Class Structure

### Main Class: `DiagnosticReporter`

```csharp
public class DiagnosticReporter : IDiagnosticReporter
{
    private readonly DiagnosticBag _diagnostics;
    private readonly ICompilerLogger _logger;

    public string? CurrentFilePath { get; set; }
    public DiagnosticBag Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.HasErrors;
}
```

**Design Pattern**: **Facade Pattern**
- Provides a simplified interface over `DiagnosticBag` and `ICompilerLogger`
- Hides the complexity of dual-tracking (collection + logging)
- Allows easy swapping of diagnostic storage implementations

**Thread Safety**: Inherits thread safety from `DiagnosticBag` (which uses locks)

---

## Key Methods

### Constructor Overloads

#### 1. Default Constructor (New Instance)

```csharp
public DiagnosticReporter(ICompilerLogger? logger = null)
{
    _diagnostics = new DiagnosticBag();
    _logger = logger ?? NullLogger.Instance;
}
```

**When to Use:**
- Creating a fresh compilation session
- Each phase needs independent error tracking
- Unit tests creating isolated reporters

**Example:**
```csharp
var reporter = new DiagnosticReporter(); // No logging
var reporter2 = new DiagnosticReporter(consoleLogger); // With logging
```

#### 2. Shared DiagnosticBag Constructor

```csharp
public DiagnosticReporter(DiagnosticBag diagnostics, ICompilerLogger? logger = null)
{
    _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    _logger = logger ?? NullLogger.Instance;
}
```

**When to Use:**
- Multiple compiler phases sharing the same diagnostic collection
- Validators need to aggregate errors into a parent bag
- Service container provides a centralized `DiagnosticBag`

**Example:**
```csharp
var sharedBag = new DiagnosticBag();
var lexerReporter = new DiagnosticReporter(sharedBag, logger);
var parserReporter = new DiagnosticReporter(sharedBag, logger);
// Both write to the same bag, aggregating all errors
```

**Why This Matters:**
- The **ValidationPipeline** uses this pattern extensively
- Each validator gets its own reporter wrapping the same bag
- Errors from all validators accumulate in one place

---

### Error Reporting Methods

#### 1. ReportError (with explicit location)

```csharp
public void ReportError(string message, int? line = null, int? column = null)
{
    _diagnostics.AddError(message, line, column, CurrentFilePath);
    _logger.LogError(message, line ?? 0, column ?? 0);
}
```

**Parameters:**
- `message`: Human-readable error description (e.g., "Undefined variable 'x'")
- `line`: Optional 1-based line number
- `column`: Optional 1-based column number

**Behavior:**
1. Adds error to the internal `DiagnosticBag` with `CurrentFilePath`
2. Logs error to `ICompilerLogger` (falls back to line/col 0 if null)

**When to Use:**
- Manual error reporting where you have line/column numbers
- Legacy code paths that don't use AST nodes
- Errors during initialization or file I/O

**Example:**
```csharp
reporter.CurrentFilePath = "program.spy";
reporter.ReportError("File not found", line: 1, column: 1);
```

#### 2. ReportError (with AST node)

```csharp
public void ReportError(string message, Node node)
{
    ReportError(message, node.LineStart, node.ColumnStart);
}
```

**Parameters:**
- `message`: Error description
- `node`: AST node where the error occurred

**Behavior:**
- Extracts `LineStart` and `ColumnStart` from the node
- Delegates to the first overload

**When to Use:**
- **Preferred method** in semantic analysis and type checking
- Any time you have an AST node reference
- Ensures accurate source locations

**Example:**
```csharp
// In TypeChecker
var variableExpr = (VariableExpression)node;
if (!symbolTable.IsDefined(variableExpr.Name))
{
    reporter.ReportError($"Undefined variable '{variableExpr.Name}'", variableExpr);
}
```

**Why This Is Better:**
- AST nodes carry precise location information
- No risk of off-by-one errors in manual line/column tracking
- Works seamlessly with parser-generated spans

---

### Warning Reporting Methods

#### 1. ReportWarning (with explicit location)

```csharp
public void ReportWarning(string message, int? line = null, int? column = null)
{
    _diagnostics.AddWarning(message, line, column, CurrentFilePath);
    _logger.LogWarning(message, line ?? 0, column ?? 0);
}
```

**Identical pattern to `ReportError`**, but:
- Uses `CompilerDiagnosticSeverity.Warning` instead of `.Error`
- Does NOT affect `HasErrors` property
- Compilation can succeed with warnings

**Example Warnings:**
- Unused variables
- Deprecated feature usage
- Missing type annotations (if using inference)
- Non-idiomatic code patterns

#### 2. ReportWarning (with AST node)

```csharp
public void ReportWarning(string message, Node node)
{
    ReportWarning(message, node.LineStart, node.ColumnStart);
}
```

Same convenience wrapper for AST-based warnings.

---

### Properties

#### CurrentFilePath

```csharp
public string? CurrentFilePath { get; set; }
```

**Purpose:**
- Tracks which `.spy` file is currently being compiled
- Automatically included in all diagnostics
- Allows multi-file compilation with clear error attribution

**Usage Pattern:**
```csharp
// In ProjectCompiler or CLI
foreach (var file in sourceFiles)
{
    reporter.CurrentFilePath = file.Path;
    var module = parser.Parse(file.Content);
    typeChecker.Check(module);
}
```

**Important:** Must be set **before** calling `ReportError`/`ReportWarning`, or diagnostics will have `FilePath = null`.

#### Diagnostics

```csharp
public DiagnosticBag Diagnostics => _diagnostics;
```

**Purpose:**
- Exposes the underlying diagnostic collection
- Allows downstream consumers to retrieve all errors/warnings
- Used by CLI to format and display results

**Common Operations:**
```csharp
var allDiagnostics = reporter.Diagnostics.GetAll();
var errorCount = reporter.Diagnostics.ErrorCount;
var warnings = reporter.Diagnostics.GetWarnings();
```

#### HasErrors

```csharp
public bool HasErrors => _diagnostics.HasErrors;
```

**Purpose:**
- Quick check for compilation failure
- Enables early termination (don't run codegen if semantic phase failed)

**Usage Pattern:**
```csharp
typeChecker.Check(module);
if (reporter.HasErrors)
{
    Console.WriteLine("Compilation failed due to errors.");
    return;
}
emitter.Emit(module); // Safe to proceed
```

---

## Dependencies

### 1. DiagnosticBag (`Sharpy.Compiler.Diagnostics`)

**What It Does:**
- Thread-safe collection of `CompilerDiagnostic` records
- Tracks errors, warnings, info, hints separately
- Provides filtering (`GetErrors()`, `GetWarnings()`)
- Supports merging from multiple sources

**Key Features:**
```csharp
public class DiagnosticBag
{
    void AddError(string message, int? line, int? column, string? filePath);
    void AddWarning(string message, int? line, int? column, string? filePath);
    void Merge(DiagnosticBag other); // Aggregate from sub-validators
    bool HasErrors { get; }
    IReadOnlyList<CompilerDiagnostic> GetAll();
}
```

**Thread Safety:**
- All operations use `lock (_lock)` internally
- Future-proof for parallel compilation

**Cross-Reference:** See [DiagnosticBag.cs](../Diagnostics/DiagnosticBag.md) (if exists)

### 2. ICompilerLogger (`Sharpy.Compiler.Logging`)

**What It Does:**
- Logs compiler operations for debugging
- Supports multiple verbosity levels (Error, Warning, Info, Debug, Trace)
- Separate from diagnostic collection (diagnostics are for users, logs are for developers)

**Null Object Pattern:**
```csharp
_logger = logger ?? NullLogger.Instance;
```

- If no logger provided, uses a no-op logger
- Avoids null checks throughout the code
- Performance: `NullLogger` methods are essentially free

**When Logging Matters:**
- Debugging parser issues (`--verbose` flag in CLI)
- Tracing type inference decisions
- Profiling compilation performance

**Cross-Reference:** See [ICompilerLogger.cs](../Logging/ICompilerLogger.md) (if exists)

### 3. AST Node (`Sharpy.Compiler.Parser.Ast`)

**Relevant Properties:**
```csharp
public abstract class Node
{
    public int LineStart { get; set; }
    public int ColumnStart { get; set; }
    public int LineEnd { get; set; }
    public int ColumnEnd { get; set; }
}
```

**Why LineStart/ColumnStart:**
- Most errors point to the **beginning** of a problematic construct
- "Error at `def`: Function name missing" points to `def` keyword
- More intuitive than end positions for most diagnostics

---

## Patterns and Design Decisions

### 1. Interface-Based Design (`IDiagnosticReporter`)

**Why Use an Interface?**
- Enables dependency injection and testability
- Allows mocking in unit tests
- Future alternative implementations (buffered, filtered, etc.)

**Example Test Mock:**
```csharp
public class TestReporter : IDiagnosticReporter
{
    public List<string> Errors = new();
    public void ReportError(string message, int? line, int? column)
        => Errors.Add(message);
    // ... simplified for testing
}
```

### 2. Dual Tracking (Collection + Logging)

**Design Decision:**
- **DiagnosticBag**: Permanent record for end users
- **ICompilerLogger**: Ephemeral debugging output for developers

**Why Both?**
- Users need structured diagnostics (JSON output, IDE integration)
- Developers need verbose trace logs during debugging
- Separation of concerns: reporting vs. observability

### 3. Optional Parameters with Null Coalescing

```csharp
_logger = logger ?? NullLogger.Instance;
_logger.LogError(message, line ?? 0, column ?? 0);
```

**Benefits:**
- Graceful degradation if logger/line/column not provided
- No null reference exceptions
- Clean API (callers don't need to check nulls)

### 4. File Path State Management

**Current Design:**
```csharp
public string? CurrentFilePath { get; set; }
```

**Alternative Considered:**
- Pass `filePath` to every `ReportError` call
- **Rejected because:** Tedious, error-prone, breaks API consistency

**Tradeoff:**
- **Pro:** Concise API, automatic file association
- **Con:** Must remember to set `CurrentFilePath` before each file
- **Mitigation:** Encapsulated in `ProjectCompiler` logic

---

## Common Usage Patterns

### Pattern 1: Single-File Compilation

```csharp
var reporter = new DiagnosticReporter();
reporter.CurrentFilePath = "example.spy";

var lexer = new Lexer(source, reporter);
var tokens = lexer.Tokenize();

if (reporter.HasErrors) return;

var parser = new Parser(tokens, reporter);
var ast = parser.Parse();

if (reporter.HasErrors) return;

// ... continue with semantic analysis
```

### Pattern 2: Multi-File Compilation

```csharp
var sharedBag = new DiagnosticBag();
var reporter = new DiagnosticReporter(sharedBag);

foreach (var file in project.SourceFiles)
{
    reporter.CurrentFilePath = file.Path;
    CompileFile(file, reporter);
}

// All errors from all files are in sharedBag
foreach (var error in sharedBag.GetErrors())
{
    Console.WriteLine(error.ToString());
}
```

### Pattern 3: Validation Pipeline (Advanced)

```csharp
// From ValidationPipeline.cs
public DiagnosticBag Validate(Module module)
{
    var diagnostics = new DiagnosticBag();
    var reporter = new DiagnosticReporter(diagnostics, _logger);

    foreach (var validator in _validators)
    {
        validator.Validate(module, reporter);
    }

    return diagnostics; // Aggregated from all validators
}
```

**Key Insight:**
- Each validator gets the same `reporter` instance
- All validators write to the shared `DiagnosticBag`
- No manual merging required

---

## Debugging Tips

### 1. Missing Diagnostics

**Problem:** Errors not appearing in output

**Check:**
```csharp
// Is CurrentFilePath set?
Console.WriteLine($"CurrentFilePath: {reporter.CurrentFilePath}");

// Are errors actually added?
Console.WriteLine($"Error count: {reporter.Diagnostics.ErrorCount}");

// Is the logger silencing output?
if (logger is ConsoleLogger cl)
{
    Console.WriteLine($"Log level: {cl.LogLevel}");
}
```

### 2. Wrong Source Locations

**Problem:** Errors point to incorrect line/column

**Check:**
```csharp
// Verify AST node positions
Console.WriteLine($"Node position: ({node.LineStart}, {node.ColumnStart})");

// Compare with expected location in source
var sourceLines = source.Split('\n');
Console.WriteLine($"Line content: {sourceLines[node.LineStart - 1]}");
```

**Common Mistake:**
- Line/column are 1-based in diagnostics, but 0-based in some internal representations
- Ensure consistency across Lexer, Parser, and AST

### 3. Duplicate Errors

**Problem:** Same error reported multiple times

**Root Causes:**
1. Multiple validators checking the same thing
2. Error recovery re-parsing the same node
3. Shared reporter instance not being reset between files

**Solution:**
```csharp
// Clear diagnostics between independent compilations
reporter.Diagnostics.Clear();

// Or create new reporter per file
var reporter = new DiagnosticReporter();
```

### 4. Logging Not Appearing

**Problem:** `_logger.LogError()` calls don't produce output

**Check:**
```csharp
// Is NullLogger being used?
Console.WriteLine(_logger.GetType().Name);

// Is log level high enough?
if (_logger is ConsoleLogger cl)
{
    Console.WriteLine($"Enabled for Error: {cl.IsEnabled(CompilerLogLevel.Error)}");
}
```

---

## Integration Points

### 1. Lexer

```csharp
// Example from Lexer.cs
public List<Token> Tokenize()
{
    while (!IsAtEnd())
    {
        if (!TryReadToken())
        {
            _reporter.ReportError("Unexpected character", _line, _column);
        }
    }
    return _tokens;
}
```

### 2. Parser

```csharp
// Example from Parser.cs
private Expression ParsePrimaryExpression()
{
    if (Match(TokenType.Identifier))
    {
        return new VariableExpression(Previous().Value);
    }

    _reporter.ReportError("Expected expression", Current());
    return new ErrorExpression(); // Recovery node
}
```

### 3. Type Checker

```csharp
// Example from TypeChecker.cs
private SemanticType CheckBinaryOp(BinaryOpExpression node)
{
    var leftType = CheckExpression(node.Left);
    var rightType = CheckExpression(node.Right);

    if (!AreTypesCompatible(leftType, rightType, node.Op))
    {
        _reporter.ReportError(
            $"Cannot apply operator '{node.Op}' to types '{leftType}' and '{rightType}'",
            node
        );
        return SemanticType.Error;
    }

    return InferResultType(leftType, node.Op);
}
```

### 4. CLI Output

```csharp
// Example from Program.cs
if (reporter.HasErrors)
{
    Console.ForegroundColor = ConsoleColor.Red;
    foreach (var error in reporter.Diagnostics.GetErrors())
    {
        Console.WriteLine(error.ToString());
        // Output: "file.spy(10,5): error: Undefined variable 'x'"
    }
    Console.ResetColor();
    return 1; // Exit code for failure
}
```

---

## Contribution Guidelines

### When to Modify This File

**Add New Methods If:**
- Need to report a new diagnostic severity (Info, Hint)
- Adding structured error codes (`SHP001`, `SHP002`)
- Supporting batch error reporting (`ReportErrors(IEnumerable<...>)`)

**Example Future Enhancement:**
```csharp
public void ReportError(string code, string message, Node node)
{
    var diagnostic = new CompilerDiagnostic(
        message,
        CompilerDiagnosticSeverity.Error,
        node.LineStart,
        node.ColumnStart,
        CurrentFilePath,
        code  // E.g., "SHP001"
    );
    _diagnostics.Add(diagnostic);
    _logger.LogError($"[{code}] {message}", node.LineStart, node.ColumnStart);
}
```

### When NOT to Modify This File

**Don't Change:**
- Diagnostic storage logic (modify `DiagnosticBag` instead)
- Logging behavior (modify `ICompilerLogger` implementations)
- Diagnostic formatting (modify `CompilerDiagnostic.ToString()`)

**Reason:** Separation of concerns. `DiagnosticReporter` is a thin facade.

### Testing Additions

**Always Add Tests For:**
- New reporting methods
- New diagnostic severities
- Edge cases (null file path, null logger, null node)

**Example Test:**
```csharp
[Fact]
public void ReportError_WithNode_ExtractsLocation()
{
    var bag = new DiagnosticBag();
    var reporter = new DiagnosticReporter(bag);
    var node = new VariableExpression("x") { LineStart = 10, ColumnStart = 5 };

    reporter.ReportError("Test error", node);

    var errors = bag.GetErrors();
    Assert.Single(errors);
    Assert.Equal(10, errors[0].Line);
    Assert.Equal(5, errors[0].Column);
}
```

---

## Migration Notes

### Legacy to V2 Validation

The `DiagnosticReporter` is central to the **V2 Validation Architecture** migration:

**Old Pattern (Direct SemanticError):**
```csharp
_errors.Add(new SemanticError("Type mismatch", node.LineStart));
```

**New Pattern (DiagnosticReporter):**
```csharp
_reporter.ReportError("Type mismatch", node);
```

**Compatibility:**
- `DiagnosticBag.ToSemanticErrors()` provides backward compatibility
- `DiagnosticBag.FromSemanticErrors()` converts legacy errors
- Gradual migration phase-by-phase (Lexer → Parser → Semantic)

**Cross-Reference:** See [ValidationPipeline.md](../Semantic/Validation/ValidationPipeline.md) for V2 architecture details.

---

## Performance Considerations

### 1. Lock Contention (Future)

**Current:** DiagnosticBag uses a single lock for all operations

**Future Optimization (if parallel compilation):**
- Use `ConcurrentBag<CompilerDiagnostic>` instead of `List<T>` + lock
- Trade-off: Slightly slower single-threaded, much faster parallel

### 2. String Allocation

**Current:** Every error creates new strings for message, file path

**Future Optimization:**
- Intern common error messages
- Use `string.Format` only when needed
- Pre-allocate buffers for diagnostic formatting

**When This Matters:**
- Large projects with thousands of errors
- IDE integration (real-time diagnostics)

### 3. Logger Overhead

**Current:** Logs every error even if logging is disabled

**Optimization:**
```csharp
public void ReportError(string message, Node node)
{
    _diagnostics.AddError(message, node.LineStart, node.ColumnStart, CurrentFilePath);

    if (_logger.IsEnabled(CompilerLogLevel.Error)) // Guard check
    {
        _logger.LogError(message, node.LineStart, node.ColumnStart);
    }
}
```

**When This Matters:**
- Production builds where logging is off
- Minimizing overhead in hot paths (type checking inner loops)

---

## Cross-References

### Related Files

1. **[IDiagnosticReporter.cs](./IDiagnosticReporter.md)** - Interface definition and contracts
2. **[DiagnosticBag.cs](../Diagnostics/DiagnosticBag.md)** - Underlying diagnostic collection
3. **[CompilerDiagnostic.cs](../Diagnostics/CompilerDiagnostic.md)** - Diagnostic record structure
4. **[ICompilerLogger.cs](../Logging/ICompilerLogger.md)** - Logging abstraction
5. **[ValidationPipeline.md](../Semantic/Validation/ValidationPipeline.md)** - V2 validation architecture
6. **[CompilerServices.md](./CompilerServices.md)** - Service container providing DiagnosticReporter

### Usage Examples

- **Lexer**: `src/Sharpy.Compiler/Lexer/Lexer.cs` - Tokenization errors
- **Parser**: `src/Sharpy.Compiler/Parser/Parser.cs` - Syntax errors
- **TypeChecker**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs` - Type errors
- **RoslynEmitter**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` - Code generation errors

---

## Summary

**DiagnosticReporter** is a simple but essential service that:

✅ **Centralizes** error reporting across all compiler phases
✅ **Simplifies** diagnostic collection with a clean API
✅ **Automates** file path and location tracking
✅ **Decouples** diagnostic storage from logging
✅ **Enables** easy testing and mocking

**Key Takeaways for Contributors:**

1. **Always use the Node overload** when you have an AST reference
2. **Set CurrentFilePath** before reporting errors for a new file
3. **Check HasErrors** before proceeding to the next compiler phase
4. **Use shared DiagnosticBag** when aggregating errors from multiple sources
5. **Modify DiagnosticBag or ICompilerLogger** for storage/logging changes, not this class

For questions about error reporting, consult the [Diagnostics](../Diagnostics/) namespace documentation.
