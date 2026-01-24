# Walkthrough: DiagnosticBag.cs

**Source File**: `src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs`

---

## Overview

`DiagnosticBag.cs` is the centralized error and warning collection system for the Sharpy compiler. It provides a **thread-safe** container for accumulating diagnostic messages (errors, warnings, info, hints) throughout the compilation pipeline.

### Role in the Compiler Pipeline

```
Source (.spy) → Lexer → Parser → Semantic → ValidationPipeline → RoslynEmitter
                  ↓        ↓         ↓              ↓                ↓
              DiagnosticBag accumulates messages from all stages
```

Each compilation stage reports issues to a `DiagnosticBag` instance, which aggregates them for presentation to the user. This is a **migration target** from the legacy `SemanticError` exception-based system.

### Key Responsibilities

- **Collect** errors, warnings, and other diagnostics from all compiler phases
- **Thread-safe storage** for future parallel compilation scenarios
- **Format** diagnostic messages with file location information
- **Bridge** between legacy `SemanticError` exceptions and the new diagnostic system

---

## Class/Type Structure

The file defines three main types:

### 1. `CompilerDiagnosticSeverity` (Enum)

```csharp
public enum CompilerDiagnosticSeverity
{
    Error,    // Compilation-blocking issues
    Warning,  // Potential problems that don't block compilation
    Info,     // Informational messages
    Hint      // Suggestions for code improvements
}
```

**Design Note**: Named with "Compiler" prefix to avoid namespace collision with `Microsoft.CodeAnalysis.DiagnosticSeverity` from Roslyn.

### 2. `CompilerDiagnostic` (Record)

An immutable record representing a single diagnostic message:

```csharp
public record CompilerDiagnostic(
    string Message,
    CompilerDiagnosticSeverity Severity,
    int? Line = null,
    int? Column = null,
    string? FilePath = null,
    string? Code = null  // e.g., "SHP001" for future structured error codes
)
```

**Key Features**:
- **Immutable**: Once created, cannot be modified (C# record semantics)
- **Optional location**: Line/Column/FilePath can be null for diagnostics without specific location
- **Future-proof**: `Code` field reserved for structured error codes like "SHP001"
- **Convenience property**: `IsError` returns true for Error severity

### 3. `DiagnosticBag` (Class)

Thread-safe collection of diagnostics with convenience methods for adding and querying.

---

## Key Methods and Implementation Details

### Adding Diagnostics

#### `Add(CompilerDiagnostic diagnostic)`

```csharp
public void Add(CompilerDiagnostic diagnostic)
{
    lock (_lock)
    {
        _diagnostics.Add(diagnostic);
    }
}
```

**Thread-Safety Pattern**: All mutations use `lock (_lock)` to ensure thread-safe access. This prepares for future scenarios where multiple files might be compiled in parallel.

#### `AddError(...)` and `AddWarning(...)`

Convenience methods that construct and add diagnostics in one call:

```csharp
public void AddError(string message, int? line = null, int? column = null, string? filePath = null)
{
    Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Error, line, column, filePath));
}
```

**Usage Pattern**: These are the most commonly used methods throughout the compiler:

```csharp
diagnostics.AddError("Undefined variable 'x'", line: 42, column: 10, filePath: "main.spy");
diagnostics.AddWarning("Unused import", line: 1);
```

### Aggregating Diagnostics

#### `AddRange(IEnumerable<CompilerDiagnostic>)` 

Adds multiple diagnostics in a single locked operation (more efficient than multiple `Add` calls).

#### `Merge(DiagnosticBag other)`

```csharp
public void Merge(DiagnosticBag other)
{
    AddRange(other.GetAll());
}
```

**Critical Pattern**: Used extensively in validation pipeline where sub-validators (like `OperatorValidatorV2`) collect their own diagnostics and merge them into a parent bag.

**Example from ValidationPipeline**:
```csharp
// Each validator has its own diagnostic bag
var validatorBag = new DiagnosticBag();
validator.Validate(context, validatorBag);

// Merge into the main diagnostic collection
mainDiagnostics.Merge(validatorBag);
```

### Querying Diagnostics

#### `HasErrors` and Count Properties

```csharp
public bool HasErrors => _diagnostics.Any(d => d.IsError);
public int ErrorCount => _diagnostics.Count(d => d.IsError);
public int WarningCount => _diagnostics.Count(d => d.Severity == CompilerDiagnosticSeverity.Warning);
```

**Performance Note**: These use LINQ queries without locking, which is safe for reads. However, they're not atomic with respect to concurrent modifications.

#### `GetAll()`, `GetErrors()`, `GetWarnings()`

Return **defensive copies** of the diagnostic list:

```csharp
public IReadOnlyList<CompilerDiagnostic> GetAll()
{
    lock (_lock)
    {
        return _diagnostics.ToList();  // Returns a copy, not a reference
    }
}
```

**Design Decision**: Returning copies prevents external code from modifying the internal list, maintaining thread-safety guarantees.

### Diagnostic Formatting

#### `CompilerDiagnostic.ToString()`

Produces compiler-style output similar to GCC/Clang:

```csharp
public override string ToString()
{
    var prefix = Severity switch
    {
        CompilerDiagnosticSeverity.Error => "error",
        CompilerDiagnosticSeverity.Warning => "warning",
        CompilerDiagnosticSeverity.Info => "info",
        CompilerDiagnosticSeverity.Hint => "hint",
        _ => "diagnostic"
    };

    var location = Line.HasValue && Column.HasValue
        ? $"({Line},{Column})"
        : Line.HasValue
            ? $"({Line})"
            : "";

    var file = !string.IsNullOrEmpty(FilePath) ? $"{FilePath}" : "";
    var code = !string.IsNullOrEmpty(Code) ? $" {Code}:" : ":";

    return $"{file}{location}: {prefix}{code} {Message}";
}
```

**Output Examples**:
```
main.spy(42,10): error: Undefined variable 'x'
lib.spy(15): warning: Unused import
(5): error SHP001: Type mismatch
error: No entry point found
```

**Format Pattern**: `[file][location]: [severity][ code]: [message]`

This matches standard compiler output conventions, making it easy to integrate with IDEs and build tools.

---

## Legacy Integration (Migration Bridge)

### The Migration Story

Sharpy is transitioning from exception-based error handling (`SemanticError`) to diagnostic collection (`DiagnosticBag`). During this transition, the codebase needs to support both systems.

### `FromSemanticErrors(IEnumerable<SemanticError>)`

Converts legacy `SemanticError` exceptions to diagnostics:

```csharp
public static DiagnosticBag FromSemanticErrors(IEnumerable<Semantic.SemanticError> errors)
{
    var bag = new DiagnosticBag();
    foreach (var error in errors)
    {
        bag.AddError(error.Message.Replace("Semantic error: ", "")
            .Replace($"Semantic error at line {error.Line}: ", "")
            .Replace($"Semantic error at line {error.Line}, column {error.Column}: ", ""),
            error.Line, error.Column);
    }
    return bag;
}
```

**String Cleaning Logic**: The `SemanticError` class prefixes messages with "Semantic error: " and location info. This method strips those prefixes to avoid duplication in the diagnostic output.

**Legacy SemanticError Format**:
```
"Semantic error at line 42, column 10: Undefined variable 'x'"
```

**Cleaned Diagnostic Message**:
```
"Undefined variable 'x'"
```

### `ToSemanticErrors()`

The reverse conversion for code that still expects `SemanticError` objects:

```csharp
public IReadOnlyList<Semantic.SemanticError> ToSemanticErrors()
{
    return GetErrors()
        .Select(d => new Semantic.SemanticError(d.Message, d.Line, d.Column))
        .ToList();
}
```

**Migration Path**: As compiler stages are updated, they will:
1. Stop throwing `SemanticError` exceptions
2. Accept a `DiagnosticBag` parameter
3. Report issues via `diagnostics.AddError(...)` instead of throwing

---

## Dependencies

### Internal Dependencies

- **`Semantic.SemanticError`** (`src/Sharpy.Compiler/Semantic/SemanticError.cs`): Legacy error type being migrated away from

### Usage Throughout the Compiler

`DiagnosticBag` is used by:

1. **`CompilationUnit`** (`Model/CompilationUnit.cs`): Every compilation unit has its own diagnostic bag
2. **`ProjectModel`** (`Model/ProjectModel.cs`): Aggregates diagnostics across multiple files via `GlobalDiagnostics`
3. **`SemanticContext`** (`Semantic/Validation/SemanticContext.cs`): Carries diagnostics through validation pipeline
4. **`ValidationPipeline`** (`Semantic/Validation/ValidationPipeline.cs`): Collects diagnostics from multiple validators
5. **`DiagnosticReporter`** (`Services/DiagnosticReporter.cs`): Wraps `DiagnosticBag` with logging capabilities

### Cross-References

Related walkthrough documents:
- [`CompilationMetrics.md`](./CompilationMetrics.md) - Sibling diagnostic infrastructure file
- [`SemanticError.md`](../Semantic/SemanticError.md) - Legacy error system (if exists)
- [`ValidationPipeline.md`](../Semantic/Validation/ValidationPipeline.md) - Primary consumer of diagnostics

---

## Patterns and Design Decisions

### 1. Thread-Safety by Default

**Decision**: Use locking for all mutations, even though current compilation is single-threaded.

**Rationale**: Future-proofs for parallel compilation scenarios where multiple files might be processed concurrently.

**Trade-off**: Minimal performance overhead in single-threaded scenarios, but enables zero-refactoring parallel compilation later.

### 2. Immutable Diagnostics

**Decision**: Use C# `record` for `CompilerDiagnostic`, making instances immutable.

**Benefits**:
- Thread-safe by nature (no mutation means no data races)
- Predictable behavior (diagnostic can't change after creation)
- Value semantics (equality by content, not reference)

### 3. Defensive Copying on Retrieval

**Decision**: `GetAll()`, `GetErrors()`, `GetWarnings()` return copies via `.ToList()`

**Rationale**: Prevents external code from:
- Modifying the internal list
- Holding references that bypass thread-safety
- Breaking encapsulation

**Trade-off**: Small allocation overhead, but ensures safety.

### 4. Nullable Location Information

**Decision**: `Line`, `Column`, `FilePath` are nullable (`int?`, `string?`)

**Rationale**: 
- Not all diagnostics have specific source locations
- Example: "No entry point found" is a project-level error without a specific line
- Module-level imports might report diagnostics before files are parsed

### 5. Separation from Roslyn Types

**Decision**: Define `CompilerDiagnostic` and `CompilerDiagnosticSeverity` instead of using Roslyn's types directly.

**Rationale**:
- Keeps Sharpy's diagnostics independent of Roslyn implementation details
- Allows custom fields (like `Code` for future error numbering)
- Prevents accidental coupling to Roslyn's diagnostic system

---

## Debugging Tips

### Tracking Down Where Errors Originate

**Problem**: An error appears in the output, but you don't know which compiler stage produced it.

**Solution 1 - Add Breakpoint with Condition**:
```csharp
// In DiagnosticBag.Add()
if (diagnostic.Message.Contains("your error substring"))
{
    // Breakpoint here - inspect call stack to see where it came from
}
```

**Solution 2 - Add Stack Trace to Diagnostic**:
```csharp
// Temporarily modify Add() to capture stack traces
#if DEBUG
diagnostic = diagnostic with { Message = $"{diagnostic.Message}\n{Environment.StackTrace}" };
#endif
```

### Checking If Diagnostics Are Being Lost

**Problem**: You're adding an error, but it's not showing up in output.

**Checklist**:
1. Verify you're adding to the right bag (not a temporary one)
2. Check if the bag is being merged into the parent bag
3. Confirm `HasErrors` returns true after adding
4. Ensure the CLI is calling `GetAll()` or `GetErrors()` to retrieve them

### Investigating Performance Issues

**Problem**: Large projects show slowdown in error collection.

**Debugging**:
```csharp
// Check if excessive querying is happening
public bool HasErrors
{
    get
    {
        Console.WriteLine($"HasErrors called - Count: {_diagnostics.Count}");
        return _diagnostics.Any(d => d.IsError);
    }
}
```

**Common Issue**: Code repeatedly calling `HasErrors` in tight loops instead of caching the result.

### Thread-Safety Verification

**Problem**: Suspected data race in diagnostic collection.

**Debugging**:
1. Enable all exceptions in debugger (Ctrl+Alt+E in Visual Studio)
2. Run with ThreadSanitizer or similar tools
3. Add assertions:
```csharp
private void AssertLockHeld()
{
    if (!Monitor.IsEntered(_lock))
        throw new InvalidOperationException("Lock not held!");
}
```

---

## Contribution Guidelines

### When to Modify DiagnosticBag

**DO modify when**:
- Adding new severity levels (e.g., `Note`, `Suggestion`)
- Adding new fields to `CompilerDiagnostic` (e.g., `Span` for multi-line ranges)
- Improving thread-safety or performance
- Adding new convenience methods for specific diagnostic patterns

**DON'T modify when**:
- You want to add a specific error message → Do that in the caller
- You need custom diagnostic formatting → Extend `ToString()` or format at display time
- You want to filter diagnostics → Do that in display/reporting layer

### Adding New Severity Levels

1. Add to `CompilerDiagnosticSeverity` enum
2. Update `ToString()` switch expression
3. Add convenience method (e.g., `AddInfo()`, `AddHint()`)
4. Add query methods (e.g., `GetHints()`, `HintCount`)
5. Update tests

### Adding Structured Error Codes

**Current**: The `Code` field exists but isn't widely used.

**Future Enhancement**:
```csharp
public static class DiagnosticCodes
{
    public const string UndefinedVariable = "SHP001";
    public const string TypeMismatch = "SHP002";
    // ...
}

// Usage:
diagnostics.Add(new CompilerDiagnostic(
    "Undefined variable 'x'",
    CompilerDiagnosticSeverity.Error,
    Line: 42,
    Code: DiagnosticCodes.UndefinedVariable
));
```

### Migrating Code from SemanticError to DiagnosticBag

**Before** (exception-based):
```csharp
public void Validate(Module module)
{
    if (someError)
        throw new SemanticError("Error message", line, column);
}
```

**After** (diagnostic-based):
```csharp
public void Validate(Module module, DiagnosticBag diagnostics)
{
    if (someError)
        diagnostics.AddError("Error message", line, column);
}
```

**Migration Checklist**:
- [ ] Change method signature to accept `DiagnosticBag` parameter
- [ ] Replace `throw new SemanticError(...)` with `diagnostics.AddError(...)`
- [ ] Remove `try/catch` blocks that caught `SemanticError`
- [ ] Update callers to pass diagnostic bag
- [ ] Update tests to check diagnostics instead of expecting exceptions

### Performance Considerations

**Current Design**: Lock-based thread-safety is simple but could become a bottleneck with highly parallel compilation.

**Future Optimization Ideas**:
- Use `ConcurrentBag<T>` for lock-free parallel adds
- Partition diagnostics by file for parallel collection
- Lazy formatting (only call `ToString()` when diagnostics are displayed)

**Don't Optimize Prematurely**: Current design is fine for typical projects (< 1000 files). Only optimize if profiling shows it's a bottleneck.

---

## Common Usage Patterns

### Pattern 1: Single-Stage Validation

```csharp
public class TypeChecker
{
    public void Check(Module module, DiagnosticBag diagnostics)
    {
        foreach (var statement in module.Statements)
        {
            if (HasTypeError(statement))
            {
                diagnostics.AddError(
                    "Type mismatch", 
                    statement.Location.Line,
                    statement.Location.Column,
                    module.FilePath
                );
            }
        }
    }
}
```

### Pattern 2: Multi-Stage Validation with Merging

```csharp
public class ValidationPipeline
{
    public DiagnosticBag Validate(Module module)
    {
        var diagnostics = new DiagnosticBag();
        
        // Run validators in sequence, accumulating diagnostics
        foreach (var validator in _validators)
        {
            var validatorBag = new DiagnosticBag();
            validator.Validate(module, validatorBag);
            diagnostics.Merge(validatorBag);
            
            // Early exit if errors found
            if (diagnostics.HasErrors)
                break;
        }
        
        return diagnostics;
    }
}
```

### Pattern 3: Project-Level Aggregation

```csharp
public class ProjectCompiler
{
    public CompilationResult Compile(Project project)
    {
        var globalDiagnostics = new DiagnosticBag();
        
        foreach (var file in project.Files)
        {
            var fileResult = CompileFile(file);
            globalDiagnostics.Merge(fileResult.Diagnostics);
        }
        
        if (globalDiagnostics.HasErrors)
        {
            return CompilationResult.Failure(globalDiagnostics);
        }
        
        return CompilationResult.Success(globalDiagnostics);
    }
}
```

### Pattern 4: Conditional Error Collection

```csharp
public void ValidateWithWarnings(Node node, DiagnosticBag diagnostics, bool treatWarningsAsErrors)
{
    if (IsDeprecated(node))
    {
        if (treatWarningsAsErrors)
        {
            diagnostics.AddError("Use of deprecated feature", node.Line);
        }
        else
        {
            diagnostics.AddWarning("Use of deprecated feature", node.Line);
        }
    }
}
```

---

## Testing Considerations

### Testing Diagnostic Collection

```csharp
[Fact]
public void TestTypeChecker_ReportsError()
{
    var diagnostics = new DiagnosticBag();
    var checker = new TypeChecker();
    
    checker.Check(invalidModule, diagnostics);
    
    Assert.True(diagnostics.HasErrors);
    Assert.Contains("Type mismatch", diagnostics.GetErrors().First().Message);
}
```

### Testing Thread-Safety

```csharp
[Fact]
public void TestDiagnosticBag_ThreadSafe()
{
    var bag = new DiagnosticBag();
    var tasks = new List<Task>();
    
    for (int i = 0; i < 100; i++)
    {
        int index = i;
        tasks.Add(Task.Run(() => 
            bag.AddError($"Error {index}")
        ));
    }
    
    Task.WaitAll(tasks.ToArray());
    
    Assert.Equal(100, bag.ErrorCount);
}
```

### Testing Legacy Integration

```csharp
[Fact]
public void TestFromSemanticErrors_StripsPrefix()
{
    var errors = new[]
    {
        new SemanticError("Test error", 42, 10)
    };
    
    var bag = DiagnosticBag.FromSemanticErrors(errors);
    
    var diagnostic = bag.GetErrors().First();
    Assert.Equal("Test error", diagnostic.Message);
    Assert.DoesNotContain("Semantic error:", diagnostic.Message);
}
```

---

## Future Enhancements

### 1. Rich Location Information

**Current**: Simple line/column pairs
**Future**: Source spans with start/end positions

```csharp
public record SourceSpan(int StartLine, int StartColumn, int EndLine, int EndColumn);

public record CompilerDiagnostic(
    // ... existing fields ...
    SourceSpan? Span = null
);
```

### 2. Diagnostic Categories

**Current**: Flat severity levels
**Future**: Hierarchical categories

```csharp
public enum DiagnosticCategory
{
    Syntax,
    Type,
    NameResolution,
    Semantic,
    CodeGen,
    Performance,
    Style
}
```

### 3. Fix Suggestions

**Current**: Only report problems
**Future**: Suggest automated fixes

```csharp
public record DiagnosticFix(
    string Description,
    TextEdit[] Edits
);

public record CompilerDiagnostic(
    // ... existing fields ...
    IReadOnlyList<DiagnosticFix>? Fixes = null
);
```

### 4. Diagnostic Suppression

**Future**: Allow suppressing specific warnings

```csharp
// #pragma warning disable SHP042
diagnostics.AddWarning("...", code: "SHP042");
// #pragma warning restore SHP042
```

---

## Summary

`DiagnosticBag` is the **central error collection infrastructure** for the Sharpy compiler. It provides:

✅ **Thread-safe** diagnostic collection for future parallel compilation  
✅ **Standardized** error/warning formatting matching GCC/Clang conventions  
✅ **Migration bridge** from legacy exception-based errors  
✅ **Aggregation** capabilities for multi-stage validation  
✅ **Defensive design** with immutable diagnostics and defensive copying  

**Key Takeaway for Contributors**: When adding new compiler checks or validations, always accept a `DiagnosticBag` parameter and report issues via `AddError()`/`AddWarning()` instead of throwing exceptions. This enables better error recovery, batch error reporting, and future parallel compilation.
