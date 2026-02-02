# Walkthrough: ISemanticValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/ISemanticValidator.cs`

---

## Overview

`ISemanticValidator.cs` defines the core interface and base class for Sharpy's pluggable semantic validation system. This file is the foundation of the **ValidationPipeline** architecture, which enables modular, extensible semantic analysis after the main type-checking phase.

### Role in the Compiler Pipeline

```
Parser (AST) → NameResolver → TypeResolver → TypeChecker → ValidationPipeline → RoslynEmitter
                                                                    ↑
                                                     ISemanticValidator (this file)
```

After the TypeChecker completes its multi-pass analysis (declarations → inheritance → types), the ValidationPipeline runs a series of specialized validators. Each validator implements `ISemanticValidator` to perform focused semantic checks like:
- Operator type compatibility (`OperatorValidator`)
- Protocol method validation (`ProtocolValidator`)
- Member access validation (`AccessValidator`)
- Control flow analysis (`ControlFlowValidator/V3`)
- Function signature checks (`SignatureValidator`)

This design separates concerns: the TypeChecker handles core type inference and checking, while validators handle more specialized rules.

---

## Class/Type Structure

### 1. `ISemanticValidator` Interface

The primary contract that all semantic validators must implement.

```csharp
public interface ISemanticValidator
{
    string Name { get; }
    int Order { get; }
    void Validate(Module module, SemanticContext context);
}
```

**Key Properties:**

- **`Name`**: A unique identifier for the validator (used in logging and debugging)
  - Example: `"OperatorValidator"`, `"ProtocolValidator"`
  - Helps trace which validator reported specific errors

- **`Order`**: Execution priority in the pipeline (lower numbers run first)
  - Standard ordering conventions:
    - `100` - Name Resolution
    - `200` - Type Resolution  
    - `300` - Type Checking
    - `400` - Access Validation
    - `500` - Operator/Protocol Validation
    - `600+` - Control Flow Analysis
  - Ensures dependencies are satisfied (e.g., types must be resolved before operators can be validated)

**Key Method:**

- **`Validate(Module module, SemanticContext context)`**: Entry point for validation
  - Receives the complete AST (`Module`) after type-checking
  - Accesses shared compiler state via `SemanticContext` (symbol table, type info, diagnostics)
  - Reports errors by adding to `context.Diagnostics`
  - No return value - validators communicate results through the `DiagnosticBag`

### 2. `SemanticValidatorBase` Abstract Class

A convenience base class providing common functionality for validators.

```csharp
public abstract class SemanticValidatorBase : ISemanticValidator
{
    public abstract string Name { get; }
    public abstract int Order { get; }
    public abstract void Validate(Module module, SemanticContext context);
    
    protected void AddError(SemanticContext context, string message, int? line = null, int? column = null);
    protected void AddWarning(SemanticContext context, string message, int? line = null, int? column = null);
}
```

**Purpose:**
- Reduces boilerplate for common validator operations
- Provides helper methods for error/warning reporting
- Validators can inherit from this or implement `ISemanticValidator` directly

---

## Key Functions/Methods

### `AddError(SemanticContext context, string message, int? line, int? column)`

**Purpose**: Convenience method to report errors to the diagnostics system.

**Parameters:**
- `context` - The semantic context containing the `DiagnosticBag`
- `message` - Human-readable error description
- `line` (optional) - Source line number where the error occurred
- `column` (optional) - Column position in the source line

**Implementation:**
```csharp
protected void AddError(SemanticContext context, string message, int? line = null, int? column = null)
{
    context.Diagnostics.AddError(message, line, column, context.CurrentFilePath);
}
```

**Key Details:**
- Automatically includes the current file path from the context
- Line/column are optional - useful when exact source location isn't available
- Errors are collected in `context.Diagnostics` (a `DiagnosticBag`)
- Multiple validators can report errors for the same code

**Example Usage in a Validator:**
```csharp
public override void Validate(Module module, SemanticContext context)
{
    foreach (var stmt in module.Body)
    {
        if (stmt is BinaryOp binOp)
        {
            var leftType = context.SemanticInfo.GetType(binOp.Left);
            var rightType = context.SemanticInfo.GetType(binOp.Right);
            
            if (!AreTypesCompatible(leftType, rightType, binOp.Op))
            {
                AddError(context, 
                    $"Cannot apply operator '{binOp.Op}' to types '{leftType}' and '{rightType}'",
                    binOp.Line, 
                    binOp.Column);
            }
        }
    }
}
```

### `AddWarning(SemanticContext context, string message, int? line, int? column)`

**Purpose**: Similar to `AddError`, but reports a warning instead of an error.

**Key Difference:**
- Warnings don't stop compilation (errors do if `ContinueAfterErrors` is false)
- Useful for style issues, deprecated features, or potential problems that aren't fatal

**Implementation:**
```csharp
protected void AddWarning(SemanticContext context, string message, int? line = null, int? column = null)
{
    context.Diagnostics.AddWarning(message, line, column, context.CurrentFilePath);
}
```

---

## Dependencies

### Internal Sharpy Dependencies

**Direct imports:**
```csharp
using Sharpy.Compiler.Parser.Ast;      // AST node types (Module, Statement, Expression)
using Sharpy.Compiler.Diagnostics;      // DiagnosticBag, error reporting
```

**Indirect dependencies through SemanticContext:**
- `SemanticInfo` - Type annotations for AST nodes
- `SymbolTable` - Name resolution results
- `TypeResolver` - Type resolution utilities
- `ClrMemberCache` - .NET reflection caching
- `AstTraversalContext` - Stack-based scope tracking

### External Dependencies

- **None** - This interface has no external package dependencies
- Pure .NET framework types only

### Related Files

**Core Pipeline Files:**
- [`ValidationPipeline.cs`](./ValidationPipeline.md) - Orchestrates validator execution
- [`ValidationPipelineFactory.cs`](./ValidationPipelineFactory.md) - Creates configured pipelines
- [`SemanticContext.cs`](./SemanticContext.md) - Shared context object
- [`AstTraversalContext.cs`](./AstTraversalContext.md) - AST traversal state

**Concrete Validators (V2 = current generation):**
- [`OperatorValidator.cs`](./OperatorValidator.md) - Binary/unary operator validation
- [`ProtocolValidator.cs`](./ProtocolValidator.md) - Protocol method validation
- [`AccessValidator.md`](./AccessValidator.md) - Member access validation
- [`SignatureValidator.cs`](./SignatureValidator.md) - Function signature validation
- [`ControlFlowValidator.md`](./ControlFlowValidator.md) / [`V3.md`](./ControlFlowValidator.md) - Control flow analysis
- [`DefaultParameterValidator.cs`](./DefaultParameterValidator.md) - Default parameter validation

**Legacy Support:**
- `LegacyValidatorAdapter.cs` - Wraps old-style validators for backward compatibility

---

## Patterns and Design Decisions

### 1. **Pluggable Architecture Pattern**

The validator system uses a **Strategy Pattern** variant where each validator is an interchangeable strategy for semantic analysis:

```
ValidationPipeline (Context)
    ↓
[ISemanticValidator] ← Strategy Interface
    ↓
[OperatorValidator] [ProtocolValidator] [AccessValidator] ← Concrete Strategies
```

**Benefits:**
- **Extensibility**: Add new validators without modifying existing code
- **Testability**: Test each validator in isolation
- **Maintainability**: Each validator has a single responsibility
- **Flexibility**: Validators can be added, removed, or reordered dynamically

### 2. **Order-Based Execution**

Validators declare an `Order` property rather than explicit dependencies:

```csharp
public int Order => 500; // Runs after validators with Order < 500
```

**Rationale:**
- Simpler than a full dependency graph system
- Sufficient for the linear dependencies in semantic analysis
- Easy to understand and debug (just sort by number)

**Convention:**
- Use round numbers (100, 200, 300) for standard phases
- Leave gaps for future validators (e.g., 450 between 400 and 500)

### 3. **Stateless Validator Design**

Validators should not hold state between `Validate()` calls:

```csharp
// ❌ Bad: Instance state persists between calls
public class BadValidator : ISemanticValidator
{
    private List<Symbol> _symbols = new(); // State persists!
    
    public void Validate(Module module, SemanticContext context)
    {
        _symbols.Add(...); // Will accumulate across modules!
    }
}

// ✅ Good: Local state only
public class GoodValidator : ISemanticValidator
{
    public void Validate(Module module, SemanticContext context)
    {
        var symbols = new List<Symbol>(); // Local to this call
        // ... validation logic
    }
}
```

**Rationale:**
- **LSP support**: Enables incremental validation by re-running validators on changed nodes
- **Parallelism**: Future versions could run validators in parallel
- **Predictability**: Each validation run is independent

**Note:** The `_context` and `_logger` fields in concrete validators (like `OperatorValidator`) are set at the start of each `Validate()` call, effectively making them call-scoped.

### 4. **Separation of Type Inference and Validation**

The `ISemanticValidator` interface is designed for **post-pass validation**, not type inference:

- **Type inference** happens in `TypeChecker` and `TypeResolver`
- **Validation** happens in `ValidationPipeline` after types are known

**Historical Note:**
Early validators (V1) performed both type inference and validation, creating circular dependencies and complexity. The V2 architecture cleanly separates these concerns.

Example:
```csharp
// Legacy OperatorValidator (V1): Infers types AND validates
// Modern OperatorValidator: Only validates (types already inferred by TypeChecker)
```

### 5. **Base Class as Optional Convenience**

`SemanticValidatorBase` is provided but not required:

```csharp
// Option 1: Use base class for convenience methods
public class MyValidator : SemanticValidatorBase
{
    public override void Validate(Module module, SemanticContext context)
    {
        AddError(context, "Problem!", line, column); // Helper method
    }
}

// Option 2: Implement interface directly
public class MyValidator : ISemanticValidator
{
    public void Validate(Module module, SemanticContext context)
    {
        context.Diagnostics.AddError("Problem!", line, column, context.CurrentFilePath);
    }
}
```

**Guidance:** Use the base class unless you need a different base class or have specific reasons to avoid it.

---

## Debugging Tips

### 1. **Tracing Validator Execution**

Each validator reports its name via the `Name` property. Enable debug logging to see validator execution order:

```csharp
// In your validator
public override void Validate(Module module, SemanticContext context)
{
    context.Logger.LogDebug($"Starting {Name} validation");
    // ... validation logic
    context.Logger.LogDebug($"Completed {Name} validation");
}
```

**CLI Usage:**
```bash
dotnet run --project src/Sharpy.Cli -- build --verbose myfile.spy
# Shows: "Starting OperatorValidator validation"
#        "Completed OperatorValidator validation"
```

### 2. **Isolating Which Validator Reported an Error**

Check the diagnostic message or use the `CurrentFilePath` context:

```csharp
// Add validator name to error messages during development
AddError(context, $"[{Name}] Invalid operator usage", line, column);
```

### 3. **Temporarily Disabling Validators**

In `ValidationPipelineFactory.cs`, comment out specific validators:

```csharp
public static ValidationPipeline CreateDefault(ICompilerLogger logger)
{
    var pipeline = new ValidationPipeline(logger);
    pipeline.AddValidator(new OperatorValidator());
    // pipeline.AddValidator(new ProtocolValidator()); // Temporarily disabled
    pipeline.AddValidator(new AccessValidator());
    return pipeline;
}
```

### 4. **Checking Validator Order Issues**

If validation seems inconsistent, print the validator execution order:

```csharp
// In ValidationPipeline.Run()
foreach (var validator in _validators)
{
    _logger.LogDebug($"Running {validator.Name} (Order: {validator.Order})");
    validator.Validate(module, context);
}
```

**Expected Order:**
```
Running AccessValidator (Order: 400)
Running OperatorValidator (Order: 500)
Running ProtocolValidator (Order: 500)
Running ControlFlowValidator (Order: 600)
```

### 5. **Inspecting SemanticContext State**

Use the debugger to inspect what information is available:

```csharp
public override void Validate(Module module, SemanticContext context)
{
    // Set breakpoint here
    var symbolTable = context.SymbolTable;      // All declared symbols
    var semanticInfo = context.SemanticInfo;    // Type annotations
    var diagnostics = context.Diagnostics;       // Errors/warnings so far
    
    // Check if types are available
    foreach (var stmt in module.Body)
    {
        if (stmt is ExpressionStatement exprStmt)
        {
            var exprType = context.SemanticInfo.GetType(exprStmt.Expression);
            // If null, type checking hasn't run or failed
        }
    }
}
```

### 6. **Common Issues and Solutions**

| Issue | Symptom | Solution |
|-------|---------|----------|
| Validator runs too early | Types not available in `SemanticInfo` | Increase `Order` value |
| Validator runs too late | Errors already reported by other validators | Decrease `Order` value |
| Missing diagnostics | Errors not appearing in output | Check if `AddError` is called, verify `DiagnosticBag` is passed correctly |
| Duplicate errors | Same error from multiple validators | Coordinate with other validators or check `DiagnosticBag` before adding |

---

## Contribution Guidelines

### Adding a New Validator

**When to add a new validator:**
- You're implementing a new language feature that needs semantic checks
- You're splitting complex logic from an existing validator
- You need to validate a cross-cutting concern (e.g., exhaustiveness checking)

**Step-by-step process:**

1. **Create the validator class:**

```csharp
// src/Sharpy.Compiler/Semantic/Validation/MyFeatureValidator.cs
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates [specific semantic rule or feature].
/// 
/// Design notes:
/// - [Key design decisions]
/// - [Future extensibility considerations]
/// </summary>
public class MyFeatureValidator : SemanticValidatorBase
{
    public override string Name => "MyFeatureValidator";
    
    // Choose appropriate order based on dependencies
    public override int Order => 500; // After access, before control flow
    
    public override void Validate(Module module, SemanticContext context)
    {
        // Implementation
    }
}
```

2. **Register in the pipeline:**

```csharp
// src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs
public static ValidationPipeline CreateDefault(ICompilerLogger logger)
{
    var pipeline = new ValidationPipeline(logger);
    // ... existing validators
    pipeline.AddValidator(new MyFeatureValidator());
    return pipeline;
}
```

3. **Add tests:**

```csharp
// src/Sharpy.Compiler.Tests/Semantic/Validation/MyFeatureValidatorTests.cs
public class MyFeatureValidatorTests : IntegrationTestBase
{
    [Fact]
    public void ValidCode_NoErrors()
    {
        var result = CompileAndExecute(@"
            # Valid Sharpy code using the feature
        ");
        Assert.True(result.Success);
    }
    
    [Fact]
    public void InvalidCode_ReportsError()
    {
        var result = CompileAndExecute(@"
            # Invalid Sharpy code
        ");
        Assert.False(result.Success);
        Assert.Contains("expected error message", result.Errors);
    }
}
```

4. **Update documentation:**
- Add entry to `Semantic/Validation/README.md`
- Create walkthrough document (like this one) if the validator is complex

### Modifying Existing Validators

**When to modify a validator:**
- Fixing a bug in validation logic
- Adding support for new AST node types
- Improving error messages
- Optimizing performance

**Guidelines:**
- ✅ **Do:** Add tests for new cases before modifying logic
- ✅ **Do:** Update the validator's XML documentation comments
- ❌ **Don't:** Change the `Order` property without considering dependencies
- ❌ **Don't:** Add stateful fields (keep validators stateless)
- ❌ **Don't:** Modify `ISemanticValidator` interface without discussing with maintainers

### Deprecating Validators

When replacing an old validator with a new version:

1. Implement the new validator (e.g., `V2` or `V3` suffix)
2. Add both validators to the pipeline temporarily
3. Verify the new validator catches all cases
4. Remove the old validator from `ValidationPipelineFactory`
5. Keep the old validator class file for reference (mark as `[Obsolete]`)
6. Remove after a few releases

Example:
```csharp
[Obsolete("Use OperatorValidator instead. This will be removed in v2.0.")]
public class OperatorValidator : ISemanticValidator { ... }
```

### Testing Checklist

Before submitting a validator change:

- [ ] Unit tests pass for the validator in isolation
- [ ] Integration tests pass (`dotnet test --filter Integration`)
- [ ] File-based tests include both valid and invalid cases
- [ ] Error messages are clear and include source locations
- [ ] Performance is acceptable (run on large files)
- [ ] Documentation is updated (at minimum, XML comments)

---

## Future Extensibility

The interface includes design notes for planned features:

### 1. **LSP (Language Server Protocol) Support**

**Current Design Consideration:**
```csharp
/// Design notes for future features:
/// - LSP: Validators can be re-run incrementally on changed nodes
```

**Implication:**
- Validators must be stateless
- Validators should be fast enough to run on keystroke
- Future: `Validate()` might receive a `ChangedNodes` parameter to skip unchanged code

### 2. **Parallel Execution**

**Current Design Consideration:**
```csharp
/// - Parallel: Validators should not hold state between calls
```

**Implication:**
- Multiple validators at the same `Order` level could run concurrently
- `SemanticContext` caches are designed to be thread-safe
- Future: `ValidationPipeline` might use `Parallel.ForEach` for validators with the same order

### 3. **ADT Exhaustiveness Checking**

**Current Design Consideration:**
```csharp
/// - ADTs: New validators (e.g., ExhaustivenessValidator) can be added
```

**Implication:**
- When Sharpy adds algebraic data types (enums with associated values)
- A new `ExhaustivenessValidator` can ensure all cases are handled in pattern matching
- No changes to `ISemanticValidator` needed - just implement and register

---

## Related Documentation

### Validation System
- [Validation README](../../../../../src/Sharpy.Compiler/Semantic/Validation/README.md) - Overview of the validation architecture
- [ValidationPipeline.md](./ValidationPipeline.md) - Pipeline orchestration
- [SemanticContext.md](./SemanticContext.md) - Shared context structure
- [AstTraversalContext.md](./AstTraversalContext.md) - AST traversal utilities

### Concrete Validators
- [OperatorValidator.md](./OperatorValidator.md) - Operator type checking
- [ProtocolValidator.md](./ProtocolValidator.md) - Protocol validation
- [AccessValidator.md](./AccessValidator.md) - Member access checks
- [ControlFlowValidator.md](./ControlFlowValidator.md) - Control flow analysis

### Semantic Analysis Core
- [TypeChecker.md](../TypeChecker.md) - Core type checking (runs before validators)
- [NameResolver.md](../NameResolver.md) - Name resolution (runs before validators)
- [TypeResolver.md](../TypeResolver.md) - Type resolution (runs before validators)
- [SemanticInfo.md](../SemanticInfo.md) - Type annotation storage

### Testing
- [Semantic Test Guide](.github/instructions/Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md) - How to test validators

### Language Specification
- [Semantic Rules](../../../../../docs/language_specification/) - Formal language semantics that validators enforce

---

## Summary

`ISemanticValidator.cs` is a small but critical file that enables Sharpy's modular semantic analysis architecture. By defining a simple interface with just three members (`Name`, `Order`, `Validate`), it allows the compiler to orchestrate complex validation logic through a clean, extensible pipeline.

**Key Takeaways for New Engineers:**

1. **This is a contract, not an implementation** - The real work happens in concrete validators
2. **Order matters** - Validators depend on earlier passes (type resolution, name resolution)
3. **Keep validators stateless** - Enables future LSP and parallel execution
4. **Use `SemanticValidatorBase`** - Unless you have a reason not to
5. **Report clear errors** - Include source locations and actionable messages
6. **Test thoroughly** - Validators are the last line of defense before code generation

When in doubt, look at existing validators like `OperatorValidator` or `AccessValidator` as templates for your own implementations.
