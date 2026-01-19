# Task List: Consolidate Validators into Validation Pipeline

**Recommendation #3 from Architecture Review**
**Target:** Transform scattered validator classes into a unified validation pipeline
**Estimated Total Effort:** 3-4 days (junior engineer) / 1-2 days (experienced engineer)

---

## Overview

### Current State
The compiler has **8+ validator classes** with inconsistent patterns:
- `TypeChecker` (main coordinator, 4 partial files)
- `NameResolver` (declaration pass)
- `TypeResolver` (type annotation resolution)
- `OperatorValidator` (binary/unary operator checking)
- `ProtocolValidator` (__len__, __iter__, etc.)
- `AccessValidator` (public/private/protected)
- `ControlFlowValidator` (break/continue/return)
- `DefaultParameterValidator` (default arg checking)
- `OperatorSignatureValidator` (dunder signature checking)
- `ProtocolSignatureValidator` (protocol signature checking)

**Problems:**
- Error collection differs: some use `List<SemanticError>`, TypeChecker aggregates from all
- Some validators cache results, others don't
- `TypeChecker.Errors` combines 7+ error sources with custom getter logic
- Shared context is passed inconsistently (some via constructor, some via method params)

### Target State
- Single `ISemanticValidator` interface for all validators
- `SemanticContext` providing shared services and caches
- `ValidationPipeline` orchestrating validator execution
- `DiagnosticBag` as unified error collection
- All validators follow the same pattern

### Future Feature Considerations
This design must support:
- **LSP (v0.2.x+):** Error-tolerant validation, incremental re-validation
- **Parallel compilation (v0.2.x+):** Thread-safe context, no shared mutable state
- **ADTs/Pattern matching (v0.2.x+):** Exhaustiveness checking validator
- **Async/await (v0.2.x+):** CFG-based validation passes

---

## Implementation Strategy

### Guiding Principles
1. **Incremental migration:** Keep existing validators working while introducing new infrastructure
2. **Test continuously:** Run tests after each major step
3. **Two-way doors preferred:** Design decisions that can be revised later
4. **Commit at checkpoints:** Allow easy rollback and progress tracking

### Migration Order (by coupling and risk)
1. Infrastructure (new files, no changes to existing code)
2. DiagnosticBag (replace SemanticError collection)
3. SemanticContext (shared services container)
4. ISemanticValidator interface
5. ValidationPipeline (orchestrator)
6. Migrate validators one-by-one (lowest coupling first)
7. Update TypeChecker to delegate to pipeline
8. Clean up legacy patterns

---

## Phase 1: Infrastructure Foundation

**Goal:** Create new infrastructure without modifying existing code. All existing tests should pass.

### Task 1.1: Create DiagnosticBag Class
**File:** `src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs`
**Effort:** ~30 minutes
**Dependencies:** None

Create a unified error/warning collection that will replace the scattered `List<SemanticError>` patterns.

```csharp
namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Severity level for diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info,
    Hint
}

/// <summary>
/// A single diagnostic message with location and severity.
/// </summary>
public record Diagnostic(
    string Message,
    DiagnosticSeverity Severity,
    int? Line = null,
    int? Column = null,
    string? FilePath = null,
    string? Code = null  // e.g., "SHP001" for future error codes
)
{
    public bool IsError => Severity == DiagnosticSeverity.Error;

    public override string ToString()
    {
        var prefix = Severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "info",
            DiagnosticSeverity.Hint => "hint",
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
}

/// <summary>
/// Thread-safe collection of diagnostics.
/// Supports future parallel compilation scenarios.
/// </summary>
public class DiagnosticBag
{
    private readonly List<Diagnostic> _diagnostics = new();
    private readonly object _lock = new();

    public void Add(Diagnostic diagnostic)
    {
        lock (_lock)
        {
            _diagnostics.Add(diagnostic);
        }
    }

    public void AddError(string message, int? line = null, int? column = null, string? filePath = null)
    {
        Add(new Diagnostic(message, DiagnosticSeverity.Error, line, column, filePath));
    }

    public void AddWarning(string message, int? line = null, int? column = null, string? filePath = null)
    {
        Add(new Diagnostic(message, DiagnosticSeverity.Warning, line, column, filePath));
    }

    public void AddRange(IEnumerable<Diagnostic> diagnostics)
    {
        lock (_lock)
        {
            _diagnostics.AddRange(diagnostics);
        }
    }

    /// <summary>
    /// Merge diagnostics from another bag (useful for aggregating from sub-validators).
    /// </summary>
    public void Merge(DiagnosticBag other)
    {
        AddRange(other.GetAll());
    }

    public bool HasErrors => _diagnostics.Any(d => d.IsError);

    public int ErrorCount => _diagnostics.Count(d => d.IsError);

    public int WarningCount => _diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

    public IReadOnlyList<Diagnostic> GetAll()
    {
        lock (_lock)
        {
            return _diagnostics.ToList();
        }
    }

    public IReadOnlyList<Diagnostic> GetErrors()
    {
        lock (_lock)
        {
            return _diagnostics.Where(d => d.IsError).ToList();
        }
    }

    public IReadOnlyList<Diagnostic> GetWarnings()
    {
        lock (_lock)
        {
            return _diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _diagnostics.Clear();
        }
    }

    /// <summary>
    /// Convert legacy SemanticErrors to Diagnostics.
    /// Use during migration period.
    /// </summary>
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

    /// <summary>
    /// Convert Diagnostics to legacy SemanticErrors.
    /// Use during migration period.
    /// </summary>
    public IReadOnlyList<Semantic.SemanticError> ToSemanticErrors()
    {
        return GetErrors()
            .Select(d => new Semantic.SemanticError(d.Message, d.Line, d.Column))
            .ToList();
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] Run: `dotnet build src/Sharpy.Compiler`
- [ ] All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

**Design Decisions:**
- **Two-way door:** Thread-safe via locking (can optimize later with ConcurrentBag if needed)
- **Two-way door:** Includes `Code` field for future error codes (not used yet)
- **Two-way door:** Conversion methods to/from SemanticError for gradual migration

---

### Task 1.2: Create ISemanticValidator Interface
**File:** `src/Sharpy.Compiler/Semantic/Validation/ISemanticValidator.cs`
**Effort:** ~15 minutes
**Dependencies:** Task 1.1

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Interface for all semantic validation passes.
/// Each validator performs a specific aspect of semantic analysis.
///
/// Design notes for future features:
/// - LSP: Validators can be re-run incrementally on changed nodes
/// - Parallel: Validators should not hold state between calls
/// - ADTs: New validators (e.g., ExhaustivenessValidator) can be added
/// </summary>
public interface ISemanticValidator
{
    /// <summary>
    /// Unique identifier for this validator (for logging/debugging).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Order hint for pipeline execution (lower = earlier).
    /// NameResolution: 100, TypeResolution: 200, TypeChecking: 300, etc.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Validates the AST and reports diagnostics.
    /// </summary>
    /// <param name="module">The AST module to validate</param>
    /// <param name="context">Shared context with symbols, types, and caches</param>
    void Validate(Module module, SemanticContext context);
}

/// <summary>
/// Base class providing common functionality for validators.
/// Validators can inherit from this or implement ISemanticValidator directly.
/// </summary>
public abstract class SemanticValidatorBase : ISemanticValidator
{
    public abstract string Name { get; }
    public abstract int Order { get; }

    public abstract void Validate(Module module, SemanticContext context);

    /// <summary>
    /// Convenience method to add an error to the context's diagnostics.
    /// </summary>
    protected void AddError(SemanticContext context, string message, int? line = null, int? column = null)
    {
        context.Diagnostics.AddError(message, line, column, context.CurrentFilePath);
    }

    /// <summary>
    /// Convenience method to add a warning to the context's diagnostics.
    /// </summary>
    protected void AddWarning(SemanticContext context, string message, int? line = null, int? column = null)
    {
        context.Diagnostics.AddWarning(message, line, column, context.CurrentFilePath);
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] Run: `dotnet build src/Sharpy.Compiler`
- [ ] All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

**Design Decisions:**
- **Two-way door:** `Order` property allows reordering validators without code changes
- **Two-way door:** Base class is optional (validators can implement interface directly)
- **Future-ready:** Module-level validation (can be extended to statement/expression-level for LSP)

---

### Task 1.3: Create SemanticContext Class
**File:** `src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs`
**Effort:** ~30 minutes
**Dependencies:** Tasks 1.1, 1.2

```csharp
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Shared context for all semantic validators.
/// Contains symbols, types, caches, and diagnostics.
///
/// Design notes for future features:
/// - LSP: Context can be snapshotted and compared for incremental validation
/// - Parallel: Context is designed to be shared across validators (caches are thread-safe)
/// - Incremental: Context can track which parts have changed since last validation
/// </summary>
public class SemanticContext
{
    // Core semantic data
    public SymbolTable SymbolTable { get; }
    public SemanticInfo SemanticInfo { get; }
    public TypeResolver TypeResolver { get; }

    // Shared caches (avoid duplicate work across validators)
    public ClrMemberCache ClrCache { get; }

    // Diagnostics collection
    public DiagnosticBag Diagnostics { get; }

    // Logging
    public ICompilerLogger Logger { get; }

    // File context (for multi-file compilation)
    public string? CurrentFilePath { get; set; }

    // Configuration
    public bool ContinueAfterErrors { get; set; } = true;
    public int MaxErrors { get; set; } = 100;

    // State tracking for validators
    public TypeSymbol? CurrentClass { get; set; }
    public FunctionSymbol? CurrentFunction { get; set; }
    public bool InLoop { get; set; }
    public int LoopDepth { get; set; }

    public SemanticContext(
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        TypeResolver typeResolver,
        ICompilerLogger? logger = null)
    {
        SymbolTable = symbolTable;
        SemanticInfo = semanticInfo;
        TypeResolver = typeResolver;
        Logger = logger ?? NullLogger.Instance;

        Diagnostics = new DiagnosticBag();
        ClrCache = new ClrMemberCache();
    }

    /// <summary>
    /// Create a context with shared infrastructure but fresh diagnostics.
    /// Useful for validating individual files in a project.
    /// </summary>
    public SemanticContext CreateForFile(string filePath)
    {
        return new SemanticContext(SymbolTable, SemanticInfo, TypeResolver, Logger)
        {
            CurrentFilePath = filePath,
            ContinueAfterErrors = ContinueAfterErrors,
            MaxErrors = MaxErrors,
            // Share the ClrCache across files for efficiency
        };
    }

    /// <summary>
    /// Check if we should continue validation (based on error count and configuration).
    /// </summary>
    public bool ShouldContinue()
    {
        if (!ContinueAfterErrors && Diagnostics.HasErrors)
            return false;
        if (Diagnostics.ErrorCount >= MaxErrors)
            return false;
        return true;
    }

    /// <summary>
    /// Merge diagnostics from a legacy validator's error list.
    /// Use during migration period.
    /// </summary>
    public void MergeFromLegacyErrors(IEnumerable<SemanticError> errors)
    {
        foreach (var error in errors)
        {
            // Extract message without the "Semantic error at line X:" prefix
            var message = error.Message;
            if (message.StartsWith("Semantic error"))
            {
                var colonIdx = message.IndexOf(": ");
                if (colonIdx >= 0)
                    message = message.Substring(colonIdx + 2);
            }
            Diagnostics.AddError(message, error.Line, error.Column, CurrentFilePath);
        }
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] Run: `dotnet build src/Sharpy.Compiler`
- [ ] All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

**Design Decisions:**
- **Two-way door:** State tracking properties (CurrentClass, InLoop) can be extended
- **Two-way door:** `CreateForFile` supports per-file contexts for project compilation
- **Future-ready:** ClrCache shared across validators and files

---

### Task 1.4: Create ValidationPipeline Class
**File:** `src/Sharpy.Compiler/Semantic/Validation/ValidationPipeline.cs`
**Effort:** ~30 minutes
**Dependencies:** Tasks 1.1-1.3

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Orchestrates semantic validation by running validators in order.
///
/// Design notes for future features:
/// - LSP: Pipeline can skip unchanged validators based on change tracking
/// - Parallel: Validators at same order level could potentially run in parallel
/// - Extensibility: New validators can be registered at runtime
/// </summary>
public class ValidationPipeline
{
    private readonly List<ISemanticValidator> _validators = new();
    private readonly ICompilerLogger _logger;

    public ValidationPipeline(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Add a validator to the pipeline.
    /// Validators are automatically sorted by their Order property.
    /// </summary>
    public ValidationPipeline AddValidator(ISemanticValidator validator)
    {
        _validators.Add(validator);
        _validators.Sort((a, b) => a.Order.CompareTo(b.Order));
        return this;
    }

    /// <summary>
    /// Add multiple validators to the pipeline.
    /// </summary>
    public ValidationPipeline AddValidators(params ISemanticValidator[] validators)
    {
        foreach (var validator in validators)
        {
            AddValidator(validator);
        }
        return this;
    }

    /// <summary>
    /// Remove a validator by type.
    /// </summary>
    public ValidationPipeline RemoveValidator<T>() where T : ISemanticValidator
    {
        _validators.RemoveAll(v => v is T);
        return this;
    }

    /// <summary>
    /// Get all registered validators (for testing/debugging).
    /// </summary>
    public IReadOnlyList<ISemanticValidator> Validators => _validators.AsReadOnly();

    /// <summary>
    /// Run all validators on the module.
    /// </summary>
    /// <param name="module">The AST module to validate</param>
    /// <param name="context">The semantic context</param>
    /// <returns>The diagnostics collected during validation</returns>
    public DiagnosticBag Validate(Module module, SemanticContext context)
    {
        _logger.LogInfo($"Starting validation pipeline with {_validators.Count} validators");

        foreach (var validator in _validators)
        {
            if (!context.ShouldContinue())
            {
                _logger.LogInfo($"Stopping validation pipeline (error limit reached or errors found)");
                break;
            }

            _logger.LogDebug($"Running validator: {validator.Name} (order: {validator.Order})");

            var errorsBefore = context.Diagnostics.ErrorCount;
            validator.Validate(module, context);
            var errorsAfter = context.Diagnostics.ErrorCount;

            if (errorsAfter > errorsBefore)
            {
                _logger.LogDebug($"Validator {validator.Name} reported {errorsAfter - errorsBefore} error(s)");
            }
        }

        _logger.LogInfo($"Validation pipeline completed. Total errors: {context.Diagnostics.ErrorCount}");
        return context.Diagnostics;
    }

    /// <summary>
    /// Create a pipeline with the standard set of validators.
    /// This is the default configuration matching current behavior.
    /// </summary>
    public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
    {
        // Note: This will be populated as validators are migrated
        return new ValidationPipeline(logger);
    }

    /// <summary>
    /// Create a minimal pipeline for testing specific validators.
    /// </summary>
    public static ValidationPipeline CreateEmpty(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger);
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] Run: `dotnet build src/Sharpy.Compiler`
- [ ] All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

**Design Decisions:**
- **Two-way door:** Fluent API allows easy configuration
- **Two-way door:** Validators can be added/removed at runtime
- **Future-ready:** Order-based sorting allows grouping validators for parallel execution

---

### Task 1.5: Commit Checkpoint - Infrastructure Foundation
**Action:** Git commit
**Message:** `feat(semantic): add validation pipeline infrastructure`

```bash
git add src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs
git add src/Sharpy.Compiler/Semantic/Validation/
git commit -m "feat(semantic): add validation pipeline infrastructure

- Add DiagnosticBag for unified error collection
- Add ISemanticValidator interface and SemanticValidatorBase
- Add SemanticContext for shared validator state
- Add ValidationPipeline for orchestrating validators

This is infrastructure only - no existing behavior changed.
All existing tests should pass."
```

**Verification:**
- [ ] All changes committed
- [ ] All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

---

## Phase 2: Add Tests for New Infrastructure

**Goal:** Add unit tests for the new infrastructure before integrating with existing code.

### Task 2.1: Create DiagnosticBag Tests
**File:** `src/Sharpy.Compiler.Tests/Diagnostics/DiagnosticBagTests.cs`
**Effort:** ~20 minutes
**Dependencies:** Task 1.1

```csharp
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.Diagnostics;

public class DiagnosticBagTests
{
    [Fact]
    public void AddError_AddsErrorDiagnostic()
    {
        var bag = new DiagnosticBag();

        bag.AddError("Test error", 10, 5);

        Assert.True(bag.HasErrors);
        Assert.Equal(1, bag.ErrorCount);
        var errors = bag.GetErrors();
        Assert.Single(errors);
        Assert.Equal("Test error", errors[0].Message);
        Assert.Equal(10, errors[0].Line);
        Assert.Equal(5, errors[0].Column);
    }

    [Fact]
    public void AddWarning_AddsWarningDiagnostic()
    {
        var bag = new DiagnosticBag();

        bag.AddWarning("Test warning", 10, 5);

        Assert.False(bag.HasErrors);
        Assert.Equal(1, bag.WarningCount);
        var warnings = bag.GetWarnings();
        Assert.Single(warnings);
        Assert.Equal("Test warning", warnings[0].Message);
    }

    [Fact]
    public void Merge_CombinesDiagnostics()
    {
        var bag1 = new DiagnosticBag();
        bag1.AddError("Error 1");

        var bag2 = new DiagnosticBag();
        bag2.AddError("Error 2");
        bag2.AddWarning("Warning 1");

        bag1.Merge(bag2);

        Assert.Equal(2, bag1.ErrorCount);
        Assert.Equal(1, bag1.WarningCount);
    }

    [Fact]
    public void FromSemanticErrors_ConvertsLegacyErrors()
    {
        var legacyErrors = new List<SemanticError>
        {
            new SemanticError("Error 1", 10, 5),
            new SemanticError("Error 2", 20, 10)
        };

        var bag = DiagnosticBag.FromSemanticErrors(legacyErrors);

        Assert.Equal(2, bag.ErrorCount);
    }

    [Fact]
    public void ToSemanticErrors_ConvertsToLegacyFormat()
    {
        var bag = new DiagnosticBag();
        bag.AddError("Error 1", 10, 5);
        bag.AddError("Error 2", 20, 10);
        bag.AddWarning("Warning 1", 30, 15); // Should be excluded

        var legacyErrors = bag.ToSemanticErrors();

        Assert.Equal(2, legacyErrors.Count);
    }

    [Fact]
    public void Clear_RemovesAllDiagnostics()
    {
        var bag = new DiagnosticBag();
        bag.AddError("Error 1");
        bag.AddWarning("Warning 1");

        bag.Clear();

        Assert.False(bag.HasErrors);
        Assert.Equal(0, bag.ErrorCount);
        Assert.Equal(0, bag.WarningCount);
    }

    [Fact]
    public void DiagnosticToString_FormatsCorrectly()
    {
        var diagnostic = new Diagnostic(
            "Test message",
            DiagnosticSeverity.Error,
            Line: 10,
            Column: 5,
            FilePath: "test.spy"
        );

        var result = diagnostic.ToString();

        Assert.Contains("test.spy", result);
        Assert.Contains("10", result);
        Assert.Contains("5", result);
        Assert.Contains("error", result);
        Assert.Contains("Test message", result);
    }
}
```

**Verification:**
- [ ] Tests compile and run
- [ ] All new tests pass: `dotnet test src/Sharpy.Compiler.Tests --filter DiagnosticBagTests`

---

### Task 2.2: Create ValidationPipeline Tests
**File:** `src/Sharpy.Compiler.Tests/Semantic/Validation/ValidationPipelineTests.cs`
**Effort:** ~30 minutes
**Dependencies:** Tasks 1.1-1.4

```csharp
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class ValidationPipelineTests
{
    private SemanticContext CreateTestContext()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);
        return new SemanticContext(symbolTable, semanticInfo, typeResolver);
    }

    private Module CreateEmptyModule()
    {
        return new Module { Body = new List<Statement>() };
    }

    [Fact]
    public void AddValidator_AddsToList()
    {
        var pipeline = new ValidationPipeline();
        var validator = new TestValidator("Test", 100);

        pipeline.AddValidator(validator);

        Assert.Single(pipeline.Validators);
    }

    [Fact]
    public void AddValidator_SortsByOrder()
    {
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new TestValidator("Third", 300));
        pipeline.AddValidator(new TestValidator("First", 100));
        pipeline.AddValidator(new TestValidator("Second", 200));

        Assert.Equal("First", pipeline.Validators[0].Name);
        Assert.Equal("Second", pipeline.Validators[1].Name);
        Assert.Equal("Third", pipeline.Validators[2].Name);
    }

    [Fact]
    public void Validate_RunsValidatorsInOrder()
    {
        var executionOrder = new List<string>();
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new OrderTrackingValidator("Third", 300, executionOrder));
        pipeline.AddValidator(new OrderTrackingValidator("First", 100, executionOrder));
        pipeline.AddValidator(new OrderTrackingValidator("Second", 200, executionOrder));

        var context = CreateTestContext();
        pipeline.Validate(CreateEmptyModule(), context);

        Assert.Equal(new[] { "First", "Second", "Third" }, executionOrder);
    }

    [Fact]
    public void Validate_StopsOnMaxErrors()
    {
        var executionOrder = new List<string>();
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new ErrorProducingValidator("First", 100, executionOrder, 10));
        pipeline.AddValidator(new OrderTrackingValidator("Second", 200, executionOrder));

        var context = CreateTestContext();
        context.MaxErrors = 5;
        pipeline.Validate(CreateEmptyModule(), context);

        // Second validator should not run because max errors exceeded
        Assert.Single(executionOrder);
        Assert.Equal("First", executionOrder[0]);
    }

    [Fact]
    public void Validate_ContinuesAfterErrorsIfConfigured()
    {
        var executionOrder = new List<string>();
        var pipeline = new ValidationPipeline();

        pipeline.AddValidator(new ErrorProducingValidator("First", 100, executionOrder, 1));
        pipeline.AddValidator(new OrderTrackingValidator("Second", 200, executionOrder));

        var context = CreateTestContext();
        context.ContinueAfterErrors = true;
        context.MaxErrors = 100;
        pipeline.Validate(CreateEmptyModule(), context);

        Assert.Equal(2, executionOrder.Count);
    }

    [Fact]
    public void RemoveValidator_RemovesByType()
    {
        var pipeline = new ValidationPipeline();
        pipeline.AddValidator(new TestValidator("Test1", 100));
        pipeline.AddValidator(new TestValidator("Test2", 200));

        pipeline.RemoveValidator<TestValidator>();

        Assert.Empty(pipeline.Validators);
    }

    // Test helper classes
    private class TestValidator : ISemanticValidator
    {
        public string Name { get; }
        public int Order { get; }

        public TestValidator(string name, int order)
        {
            Name = name;
            Order = order;
        }

        public void Validate(Module module, SemanticContext context) { }
    }

    private class OrderTrackingValidator : ISemanticValidator
    {
        private readonly List<string> _executionOrder;

        public string Name { get; }
        public int Order { get; }

        public OrderTrackingValidator(string name, int order, List<string> executionOrder)
        {
            Name = name;
            Order = order;
            _executionOrder = executionOrder;
        }

        public void Validate(Module module, SemanticContext context)
        {
            _executionOrder.Add(Name);
        }
    }

    private class ErrorProducingValidator : ISemanticValidator
    {
        private readonly List<string> _executionOrder;
        private readonly int _errorCount;

        public string Name { get; }
        public int Order { get; }

        public ErrorProducingValidator(string name, int order, List<string> executionOrder, int errorCount)
        {
            Name = name;
            Order = order;
            _executionOrder = executionOrder;
            _errorCount = errorCount;
        }

        public void Validate(Module module, SemanticContext context)
        {
            _executionOrder.Add(Name);
            for (int i = 0; i < _errorCount; i++)
            {
                context.Diagnostics.AddError($"Error {i}");
            }
        }
    }
}
```

**Verification:**
- [ ] Tests compile and run
- [ ] All new tests pass: `dotnet test src/Sharpy.Compiler.Tests --filter ValidationPipelineTests`
- [ ] All existing tests still pass: `dotnet test src/Sharpy.Compiler.Tests`

---

### Task 2.3: Commit Checkpoint - Infrastructure Tests
**Action:** Git commit
**Message:** `test(semantic): add tests for validation pipeline infrastructure`

```bash
git add src/Sharpy.Compiler.Tests/Diagnostics/
git add src/Sharpy.Compiler.Tests/Semantic/Validation/
git commit -m "test(semantic): add tests for validation pipeline infrastructure

- Add DiagnosticBagTests
- Add ValidationPipelineTests

Tests verify the new infrastructure works correctly before integration."
```

---

## Phase 3: Create Adapter for Legacy Validators

**Goal:** Create an adapter that wraps existing validators to work with the new pipeline.

### Task 3.1: Create LegacyValidatorAdapter
**File:** `src/Sharpy.Compiler/Semantic/Validation/LegacyValidatorAdapter.cs`
**Effort:** ~30 minutes
**Dependencies:** Phase 1

This adapter allows existing validators to work with the new pipeline without modification. This enables gradual migration.

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Adapter that wraps a legacy validator action to work with the new pipeline.
/// This enables gradual migration of existing validators.
/// </summary>
public class LegacyValidatorAdapter : ISemanticValidator
{
    private readonly Action<Module, SemanticContext> _validateAction;
    private readonly Func<IReadOnlyList<SemanticError>>? _getErrors;

    public string Name { get; }
    public int Order { get; }

    public LegacyValidatorAdapter(
        string name,
        int order,
        Action<Module, SemanticContext> validateAction,
        Func<IReadOnlyList<SemanticError>>? getErrors = null)
    {
        Name = name;
        Order = order;
        _validateAction = validateAction;
        _getErrors = getErrors;
    }

    public void Validate(Module module, SemanticContext context)
    {
        _validateAction(module, context);

        // If the legacy validator has an error collection, merge them
        if (_getErrors != null)
        {
            context.MergeFromLegacyErrors(_getErrors());
        }
    }

    /// <summary>
    /// Create an adapter for ControlFlowValidator.
    /// </summary>
    public static LegacyValidatorAdapter ForControlFlowValidator(
        ControlFlowValidator validator,
        ICompilerLogger? logger = null)
    {
        return new LegacyValidatorAdapter(
            "ControlFlowValidator",
            400, // Run after type checking
            (module, context) =>
            {
                // ControlFlowValidator validates functions individually
                // We need to traverse the module and validate each function
                foreach (var stmt in module.Body)
                {
                    if (stmt is FunctionDef funcDef)
                    {
                        var returnType = context.SemanticInfo.GetReturnType(funcDef) ?? SemanticType.Void;
                        validator.ValidateFunction(funcDef, returnType);
                    }
                    else if (stmt is ClassDef classDef)
                    {
                        foreach (var member in classDef.Body)
                        {
                            if (member is FunctionDef methodDef)
                            {
                                var returnType = context.SemanticInfo.GetReturnType(methodDef) ?? SemanticType.Void;
                                validator.ValidateFunction(methodDef, returnType);
                            }
                        }
                    }
                }
            },
            () => validator.Errors
        );
    }

    /// <summary>
    /// Create an adapter for AccessValidator.
    /// Note: AccessValidator is typically called during expression type-checking,
    /// so this adapter is mainly for testing.
    /// </summary>
    public static LegacyValidatorAdapter ForAccessValidator(
        AccessValidator validator,
        ICompilerLogger? logger = null)
    {
        return new LegacyValidatorAdapter(
            "AccessValidator",
            350, // Run during/after type checking
            (module, context) =>
            {
                // AccessValidator is called on-demand during type checking
                // This adapter is mainly for completeness
            },
            () => validator.Errors
        );
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] Run: `dotnet build src/Sharpy.Compiler`
- [ ] All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

**Design Decisions:**
- **Two-way door:** Adapters can be replaced with native validators one at a time
- **Explicit order values:** Make it clear where each validator runs in the pipeline

---

### Task 3.2: Commit Checkpoint - Legacy Adapter
**Action:** Git commit
**Message:** `feat(semantic): add legacy validator adapter for gradual migration`

```bash
git add src/Sharpy.Compiler/Semantic/Validation/LegacyValidatorAdapter.cs
git commit -m "feat(semantic): add legacy validator adapter for gradual migration

Enables wrapping existing validators to work with the new pipeline
without modifying them."
```

---

## Phase 4: Migrate ControlFlowValidator (Simplest First)

**Goal:** Migrate the simplest validator to the new pattern as proof-of-concept.

### Task 4.1: Create ControlFlowValidatorV2
**File:** `src/Sharpy.Compiler/Semantic/Validation/ControlFlowValidatorV2.cs`
**Effort:** ~45 minutes
**Dependencies:** Phases 1-3

Create a new version of ControlFlowValidator that implements `ISemanticValidator`.

```csharp
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates control flow in Sharpy code:
/// - Detects unreachable code
/// - Validates return paths
/// - Ensures break/continue are only in loops
///
/// This is the pipeline-compatible version of ControlFlowValidator.
/// </summary>
public class ControlFlowValidatorV2 : SemanticValidatorBase
{
    public override string Name => "ControlFlowValidator";
    public override int Order => 400; // After type checking (300)

    private ICompilerLogger _logger = NullLogger.Instance;
    private SemanticContext _context = null!;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting control flow validation");

        foreach (var stmt in module.Body)
        {
            ValidateTopLevelStatement(stmt);
        }
    }

    private void ValidateTopLevelStatement(Statement stmt)
    {
        switch (stmt)
        {
            case FunctionDef funcDef:
                ValidateFunction(funcDef);
                break;
            case ClassDef classDef:
                ValidateClass(classDef);
                break;
            case StructDef structDef:
                ValidateStruct(structDef);
                break;
            // Other top-level statements don't need control flow validation
        }
    }

    private void ValidateClass(ClassDef classDef)
    {
        foreach (var member in classDef.Body)
        {
            if (member is FunctionDef methodDef)
            {
                ValidateFunction(methodDef);
            }
        }
    }

    private void ValidateStruct(StructDef structDef)
    {
        foreach (var member in structDef.Body)
        {
            if (member is FunctionDef methodDef)
            {
                ValidateFunction(methodDef);
            }
        }
    }

    private void ValidateFunction(FunctionDef funcDef)
    {
        _logger.LogDebug($"Validating control flow for function: {funcDef.Name}");

        // Skip control flow validation for abstract methods
        bool hasAbstractDecorator = funcDef.Decorators.Any(d => d.Name == "abstract");
        bool hasEllipsisBody = funcDef.Body.Count == 1
            && funcDef.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };

        if (hasAbstractDecorator || hasEllipsisBody)
        {
            return;
        }

        // Get return type from semantic info
        var returnType = _context.SemanticInfo.GetReturnType(funcDef) ?? SemanticType.Void;

        var (alwaysReturns, _) = ValidateBlock(funcDef.Body, loopDepth: 0);

        if (returnType != SemanticType.Void && !alwaysReturns)
        {
            AddError(_context,
                $"Function '{funcDef.Name}' must return a value of type '{returnType.GetDisplayName()}' in all code paths",
                funcDef.LineStart, funcDef.ColumnStart);
        }
    }

    /// <summary>
    /// Validates a block of statements.
    /// Returns (alwaysReturns, hasUnreachableCode).
    /// </summary>
    private (bool, bool) ValidateBlock(List<Statement> statements, int loopDepth)
    {
        bool alwaysReturns = false;
        bool alwaysExits = false;
        bool hasUnreachableCode = false;

        for (int i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];

            if (alwaysExits && i < statements.Count)
            {
                if (!hasUnreachableCode)
                {
                    AddError(_context, "Unreachable code detected",
                        statement.LineStart, statement.ColumnStart);
                    hasUnreachableCode = true;
                }
                continue;
            }

            var (stmtReturns, stmtExits) = ValidateStatement(statement, loopDepth);

            if (stmtReturns) alwaysReturns = true;
            if (stmtExits) alwaysExits = true;
        }

        return (alwaysReturns, hasUnreachableCode);
    }

    /// <summary>
    /// Validates a single statement.
    /// Returns (alwaysReturns, alwaysExits).
    /// </summary>
    private (bool, bool) ValidateStatement(Statement statement, int loopDepth)
    {
        switch (statement)
        {
            case ReturnStatement:
                return (true, true);

            case RaiseStatement:
                return (false, true);

            case BreakStatement:
                if (loopDepth == 0)
                {
                    AddError(_context, "'break' statement outside loop",
                        statement.LineStart, statement.ColumnStart);
                }
                return (false, true);

            case ContinueStatement:
                if (loopDepth == 0)
                {
                    AddError(_context, "'continue' statement outside loop",
                        statement.LineStart, statement.ColumnStart);
                }
                return (false, true);

            case IfStatement ifStmt:
                return ValidateIf(ifStmt, loopDepth);

            case WhileStatement whileStmt:
                return ValidateWhile(whileStmt, loopDepth);

            case ForStatement forStmt:
                return ValidateFor(forStmt, loopDepth);

            case TryStatement tryStmt:
                return ValidateTry(tryStmt, loopDepth);

            case FunctionDef:
                // Nested function validated separately
                return (false, false);

            case ClassDef:
            case StructDef:
            case InterfaceDef:
            case EnumDef:
                return (false, false);

            default:
                return (false, false);
        }
    }

    private (bool, bool) ValidateIf(IfStatement ifStmt, int loopDepth)
    {
        var (thenReturns, _) = ValidateBlock(ifStmt.ThenBody, loopDepth);
        bool allBranchesReturn = thenReturns;

        foreach (var elifClause in ifStmt.ElifClauses)
        {
            var (elifReturns, _) = ValidateBlock(elifClause.Body, loopDepth);
            allBranchesReturn = allBranchesReturn && elifReturns;
        }

        if (ifStmt.ElseBody != null && ifStmt.ElseBody.Count > 0)
        {
            var (elseReturns, _) = ValidateBlock(ifStmt.ElseBody, loopDepth);
            allBranchesReturn = allBranchesReturn && elseReturns;
        }
        else
        {
            allBranchesReturn = false;
        }

        return (allBranchesReturn, allBranchesReturn);
    }

    private (bool, bool) ValidateWhile(WhileStatement whileStmt, int loopDepth)
    {
        ValidateBlock(whileStmt.Body, loopDepth + 1);
        return (false, false); // Loop doesn't guarantee execution
    }

    private (bool, bool) ValidateFor(ForStatement forStmt, int loopDepth)
    {
        ValidateBlock(forStmt.Body, loopDepth + 1);
        return (false, false); // Loop doesn't guarantee execution
    }

    private (bool, bool) ValidateTry(TryStatement tryStmt, int loopDepth)
    {
        var (tryReturns, _) = ValidateBlock(tryStmt.Body, loopDepth);

        bool allHandlersReturn = true;
        foreach (var handler in tryStmt.Handlers)
        {
            var (handlerReturns, _) = ValidateBlock(handler.Body, loopDepth);
            allHandlersReturn = allHandlersReturn && handlerReturns;
        }

        bool finallyReturns = false;
        if (tryStmt.FinallyBody != null && tryStmt.FinallyBody.Count > 0)
        {
            var (finReturns, _) = ValidateBlock(tryStmt.FinallyBody, loopDepth);
            finallyReturns = finReturns;
        }

        bool allPathsReturn = finallyReturns || (tryReturns && allHandlersReturn);
        return (allPathsReturn, allPathsReturn);
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] Run: `dotnet build src/Sharpy.Compiler`
- [ ] All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

---

### Task 4.2: Create Tests for ControlFlowValidatorV2
**File:** `src/Sharpy.Compiler.Tests/Semantic/Validation/ControlFlowValidatorV2Tests.cs`
**Effort:** ~30 minutes
**Dependencies:** Task 4.1

```csharp
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class ControlFlowValidatorV2Tests
{
    private (Module module, SemanticContext context) Parse(string code)
    {
        var lexer = new Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        // Run name resolution
        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Run type checking to populate semantic info
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void Function_WithoutReturn_ReportsError()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return a value"));
    }

    [Fact]
    public void Function_WithReturn_NoError()
    {
        var code = @"
def foo() -> int:
    return 5
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BreakOutsideLoop_ReportsError()
    {
        var code = @"
def foo() -> None:
    break
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("'break' statement outside loop"));
    }

    [Fact]
    public void BreakInsideLoop_NoError()
    {
        var code = @"
def foo() -> None:
    while True:
        break
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void UnreachableCode_ReportsError()
    {
        var code = @"
def foo() -> int:
    return 5
    x = 10
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("Unreachable code"));
    }

    [Fact]
    public void AbstractMethod_SkipsValidation()
    {
        var code = @"
@abstract
class Base:
    @abstract
    def foo(self) -> int:
        ...
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void IfElseAllBranchesReturn_NoError()
    {
        var code = @"
def foo(x: int) -> int:
    if x > 0:
        return 1
    else:
        return -1
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidatorV2();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void IfWithoutElse_MissingReturn_ReportsError()
    {
        var code = @"
def foo(x: int) -> int:
    if x > 0:
        return 1
";
        var (module, context) = Parse(code);

        var validator = new ControlFlowValidatorV2();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return a value"));
    }
}
```

**Verification:**
- [ ] Tests compile and run
- [ ] New tests pass: `dotnet test src/Sharpy.Compiler.Tests --filter ControlFlowValidatorV2Tests`
- [ ] All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

---

### Task 4.3: Commit Checkpoint - ControlFlowValidator Migration
**Action:** Git commit
**Message:** `feat(semantic): migrate ControlFlowValidator to pipeline pattern`

```bash
git add src/Sharpy.Compiler/Semantic/Validation/ControlFlowValidatorV2.cs
git add src/Sharpy.Compiler.Tests/Semantic/Validation/ControlFlowValidatorV2Tests.cs
git commit -m "feat(semantic): migrate ControlFlowValidator to pipeline pattern

- Add ControlFlowValidatorV2 implementing ISemanticValidator
- Add comprehensive tests for the new validator

The old ControlFlowValidator is kept for now to avoid breaking changes.
Both can coexist during migration."
```

---

## Phase 5: Integrate Pipeline with TypeChecker (Backward Compatible)

**Goal:** Make TypeChecker use the pipeline while maintaining backward compatibility.

### Task 5.1: Add Pipeline Integration to TypeChecker
**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
**Effort:** ~45 minutes
**Dependencies:** Phases 1-4

Modify TypeChecker to optionally use the ValidationPipeline.

**Changes:**
1. Add optional constructor parameter for pipeline
2. Add method to create pipeline-aware context
3. Keep existing behavior as default

```csharp
// Add to TypeChecker.cs - at the top of the class, add new fields:

private readonly ValidationPipeline? _validationPipeline;
private readonly bool _usePipeline;

// Modify the constructor to accept optional pipeline:

public TypeChecker(
    SymbolTable symbolTable,
    SemanticInfo semanticInfo,
    TypeResolver typeResolver,
    ICompilerLogger? logger = null,
    ValidationPipeline? validationPipeline = null)
{
    _symbolTable = symbolTable;
    _semanticInfo = semanticInfo;
    _typeResolver = typeResolver;
    _logger = logger ?? NullLogger.Instance;
    _validationPipeline = validationPipeline;
    _usePipeline = validationPipeline != null;

    _controlFlowValidator = new ControlFlowValidator(_logger);
    _accessValidator = new AccessValidator(_symbolTable, _semanticInfo, _logger);

    var sharedClrCache = new ClrMemberCache();
    _protocolValidator = new ProtocolValidator(_symbolTable, _logger, sharedClrCache);
    _operatorValidator = new OperatorValidator(_symbolTable, _logger, _protocolValidator, sharedClrCache);
    _defaultParameterValidator = new DefaultParameterValidator(_symbolTable, _typeResolver, _logger);
}

// Add new method to create SemanticContext:

/// <summary>
/// Creates a SemanticContext for use with the validation pipeline.
/// </summary>
public SemanticContext CreateSemanticContext()
{
    return new SemanticContext(_symbolTable, _semanticInfo, _typeResolver, _logger);
}

// Modify CheckModule to optionally use pipeline:

/// <summary>
/// Type check all statements in a module
/// </summary>
public void CheckModule(Module module)
{
    _logger.LogInfo("Type checking module");

    foreach (var statement in module.Body)
    {
        CheckStatement(statement);
    }

    // If pipeline is configured, run additional validators
    if (_usePipeline && _validationPipeline != null)
    {
        var context = CreateSemanticContext();
        context.MergeFromLegacyErrors(_errors);
        context.MergeFromLegacyErrors(_typeResolver.Errors);

        _validationPipeline.Validate(module, context);

        // Merge pipeline diagnostics back to legacy error list
        foreach (var error in context.Diagnostics.GetErrors())
        {
            if (!_errors.Any(e => e.Message == error.Message && e.Line == error.Line))
            {
                _errors.Add(new SemanticError(error.Message, error.Line, error.Column));
            }
        }
    }
}

// Update Errors property to include pipeline errors:

public IReadOnlyList<SemanticError> Errors
{
    get
    {
        var allErrors = new List<SemanticError>(_errors);
        allErrors.AddRange(_typeResolver.Errors);

        if (!_usePipeline)
        {
            // Legacy behavior - collect from individual validators
            allErrors.AddRange(_controlFlowValidator.Errors);
            allErrors.AddRange(_accessValidator.Errors);
            allErrors.AddRange(_operatorValidator.Errors);
            allErrors.AddRange(_protocolValidator.Errors);
            allErrors.AddRange(_defaultParameterValidator.Errors);
        }
        // When using pipeline, errors are already merged in CheckModule

        return allErrors;
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] Run: `dotnet build src/Sharpy.Compiler`
- [ ] **Critical:** All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

---

### Task 5.2: Add Integration Test for Pipeline Mode
**File:** `src/Sharpy.Compiler.Tests/Semantic/Validation/TypeCheckerPipelineIntegrationTests.cs`
**Effort:** ~30 minutes
**Dependencies:** Task 5.1

```csharp
using Xunit;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class TypeCheckerPipelineIntegrationTests
{
    private (SymbolTable symbolTable, SemanticInfo semanticInfo, TypeResolver typeResolver, Module module)
        SetupWithNameResolution(string code)
    {
        var lexer = new Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        return (symbolTable, semanticInfo, typeResolver, module);
    }

    [Fact]
    public void LegacyMode_StillWorks()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        // No pipeline (legacy mode)
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module);

        // Legacy mode still works - control flow errors should be present
        Assert.True(typeChecker.Errors.Any(e => e.Message.Contains("must return")));
    }

    [Fact]
    public void PipelineMode_WithControlFlowValidator()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        var pipeline = new ValidationPipeline()
            .AddValidator(new ControlFlowValidatorV2());

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver,
            validationPipeline: pipeline);
        typeChecker.CheckModule(module);

        // Pipeline mode should also report control flow errors
        Assert.True(typeChecker.Errors.Any(e => e.Message.Contains("must return")));
    }

    [Fact]
    public void PipelineMode_CombinesWithTypeErrors()
    {
        var code = @"
def foo() -> int:
    x: str = 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        var pipeline = new ValidationPipeline()
            .AddValidator(new ControlFlowValidatorV2());

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver,
            validationPipeline: pipeline);
        typeChecker.CheckModule(module);

        // Should have both type error AND control flow error
        var errors = typeChecker.Errors;
        Assert.True(errors.Any(e => e.Message.Contains("type")),
            "Should have type error");
        Assert.True(errors.Any(e => e.Message.Contains("must return")),
            "Should have control flow error");
    }

    [Fact]
    public void EmptyPipeline_DisablesLegacyControlFlowValidator()
    {
        var code = @"
def foo() -> int:
    x = 5
";
        var (symbolTable, semanticInfo, typeResolver, module) = SetupWithNameResolution(code);

        // Empty pipeline - no ControlFlowValidator added
        var pipeline = new ValidationPipeline();

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver,
            validationPipeline: pipeline);
        typeChecker.CheckModule(module);

        // With empty pipeline, legacy control flow validator is not used
        // So no control flow errors should be present
        Assert.False(typeChecker.Errors.Any(e => e.Message.Contains("must return")));
    }
}
```

**Verification:**
- [ ] Tests compile and run
- [ ] New tests pass: `dotnet test src/Sharpy.Compiler.Tests --filter TypeCheckerPipelineIntegrationTests`
- [ ] **Critical:** All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

---

### Task 5.3: Commit Checkpoint - Pipeline Integration
**Action:** Git commit
**Message:** `feat(semantic): integrate validation pipeline with TypeChecker`

```bash
git add src/Sharpy.Compiler/Semantic/TypeChecker.cs
git add src/Sharpy.Compiler.Tests/Semantic/Validation/TypeCheckerPipelineIntegrationTests.cs
git commit -m "feat(semantic): integrate validation pipeline with TypeChecker

- Add optional ValidationPipeline parameter to TypeChecker
- Maintain full backward compatibility (no pipeline = legacy behavior)
- Add integration tests for pipeline mode

This enables gradual migration to pipeline-based validation."
```

---

## Phase 6: Migrate Remaining Validators (One at a Time)

For each remaining validator, follow this pattern:

### Template for Each Validator Migration

1. **Create V2 version** implementing `ISemanticValidator`
2. **Add tests** for the V2 version
3. **Add to default pipeline** configuration
4. **Run full test suite** to verify no regressions
5. **Commit** the migration

### Task 6.1: Migrate OperatorValidator
**Files:**
- `src/Sharpy.Compiler/Semantic/Validation/OperatorValidatorV2.cs`
- `src/Sharpy.Compiler.Tests/Semantic/Validation/OperatorValidatorV2Tests.cs`

**Note:** OperatorValidator is called during expression type-checking, so its migration requires:
1. Keep the existing class (used by TypeChecker during expression checking)
2. Create V2 that can run as a post-pass for additional validation
3. Ensure no duplicate errors

**Effort:** ~1 hour

---

### Task 6.2: Migrate AccessValidator
**Similar pattern to Task 6.1**
**Effort:** ~45 minutes

---

### Task 6.3: Migrate ProtocolValidator
**Similar pattern to Task 6.1**
**Effort:** ~45 minutes

---

### Task 6.4: Migrate DefaultParameterValidator
**Similar pattern to Task 6.1**
**Effort:** ~30 minutes

---

### Task 6.5: Migrate OperatorSignatureValidator
**Similar pattern to Task 6.1**
**Effort:** ~30 minutes

---

### Task 6.6: Migrate ProtocolSignatureValidator
**Similar pattern to Task 6.1**
**Effort:** ~30 minutes

---

### Task 6.7: Commit Checkpoint - All Validators Migrated
**Action:** Git commit
**Message:** `feat(semantic): complete validator migration to pipeline`

---

## Phase 7: Update Default Pipeline and Clean Up

### Task 7.1: Create Default Pipeline Factory
**File:** `src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs`
**Effort:** ~20 minutes

```csharp
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Factory for creating pre-configured validation pipelines.
/// </summary>
public static class ValidationPipelineFactory
{
    /// <summary>
    /// Create the default pipeline with all standard validators.
    /// This matches the behavior of the pre-pipeline TypeChecker.
    /// </summary>
    public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            // Order values determine execution sequence
            .AddValidator(new ControlFlowValidatorV2())
            // Add other V2 validators as they are migrated:
            // .AddValidator(new OperatorValidatorV2())
            // .AddValidator(new AccessValidatorV2())
            // .AddValidator(new ProtocolValidatorV2())
            // .AddValidator(new DefaultParameterValidatorV2())
            // .AddValidator(new OperatorSignatureValidatorV2())
            // .AddValidator(new ProtocolSignatureValidatorV2())
            ;
    }

    /// <summary>
    /// Create a minimal pipeline for testing.
    /// </summary>
    public static ValidationPipeline CreateMinimal(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger);
    }

    /// <summary>
    /// Create pipeline for fast compilation (skip expensive validators).
    /// Useful for IDE/LSP scenarios where speed matters more than completeness.
    /// </summary>
    public static ValidationPipeline CreateFast(ICompilerLogger? logger = null)
    {
        return new ValidationPipeline(logger)
            .AddValidator(new ControlFlowValidatorV2());
            // Skip signature validators, protocol validators, etc.
    }
}
```

---

### Task 7.2: Update Compiler to Use Pipeline by Default
**File:** `src/Sharpy.Compiler/Compiler.cs`
**Effort:** ~20 minutes

Modify `Compiler.Compile()` to use the pipeline:

```csharp
// In Compiler.Compile(), update the type checking phase:

// Pass 2: Type resolution and type checking
metrics.StartPhase("Type Resolution");
var typeResolver = new TypeResolver(symbolTable, semanticInfo, _logger);
metrics.EndPhase();

metrics.StartPhase("Type Checking");
var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger, pipeline);
typeChecker.CheckModule(module);
metrics.EndPhase();
```

**Verification:**
- [ ] File compiles without errors
- [ ] **Critical:** All existing tests pass: `dotnet test src/Sharpy.Compiler.Tests`

---

### Task 7.3: Deprecate Legacy Validator Error Collection
**Files:** Multiple validator files
**Effort:** ~30 minutes

Add `[Obsolete]` attributes to legacy error collection patterns:

```csharp
// In each legacy validator:
[Obsolete("Use DiagnosticBag via SemanticContext instead. This property will be removed in v0.2.0")]
public IReadOnlyList<SemanticError> Errors => _errors;
```

---

### Task 7.4: Final Commit - Pipeline Complete
**Action:** Git commit
**Message:** `feat(semantic): complete validation pipeline migration`

```bash
git add .
git commit -m "feat(semantic): complete validation pipeline migration

- Add ValidationPipelineFactory for common configurations
- Update Compiler to use pipeline by default
- Deprecate legacy error collection patterns
- All existing tests pass with new pipeline

The validation system now uses a unified pipeline architecture that:
- Follows a consistent ISemanticValidator interface
- Uses DiagnosticBag for unified error collection
- Shares SemanticContext across validators
- Enables easy addition of new validators
- Prepares for LSP, parallel, and incremental compilation"
```

---

## Verification Checklist

### After Each Phase
- [ ] `dotnet build src/Sharpy.Compiler` succeeds
- [ ] `dotnet test src/Sharpy.Compiler.Tests` - all tests pass
- [ ] Code committed with descriptive message

### Final Verification
- [ ] All original tests pass
- [ ] New infrastructure tests pass
- [ ] No breaking API changes (pipeline is additive)
- [ ] Documentation updated (if applicable)
- [ ] Performance is not degraded (pipeline overhead is minimal)

---

## Design Decisions Summary

### Two-Way Doors (Reversible)
1. **ISemanticValidator interface** - Can add properties/methods later
2. **SemanticContext fields** - Can add more shared state
3. **Order values** - Can reorder validators by changing numbers
4. **Pipeline configuration** - Can add/remove validators at runtime
5. **DiagnosticBag vs List<SemanticError>** - Coexistence during migration

### One-Way Doors (Committed)
1. **Validator naming convention** - V2 suffix pattern
2. **Order magnitude convention** - 100s increments
3. **SemanticContext as the shared context type** - All validators depend on this

### Future Feature Enablement
| Feature | How This Helps |
|---------|---------------|
| **LSP** | Validators can be re-run on changed nodes |
| **Parallel** | DiagnosticBag is thread-safe, context is shareable |
| **Incremental** | Context can track changes, validators can be selective |
| **ADTs** | ExhaustivenessValidator can be added as new ISemanticValidator |
| **Async** | AsyncValidatorV2 can analyze await points using CFG |

---

## Rollback Plan

If issues are discovered:

1. **Per-validator rollback:** Remove V2 from pipeline, legacy validator still works
2. **Full rollback:** Remove `validationPipeline` parameter from TypeChecker constructor
3. **Git revert:** Each phase has a commit checkpoint for easy revert

The incremental approach ensures that at any point, the compiler remains functional.
