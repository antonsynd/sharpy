# Walkthrough: ValidationPipeline.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/ValidationPipeline.cs`

---

## Overview

The `ValidationPipeline` is the orchestration layer for semantic validation in the Sharpy compiler. It's a simple but critical component that manages and executes validators in a specific order to perform semantic analysis on the AST.

**Role in the compiler pipeline:**
- **Upstream**: Receives an AST from the Parser
- **Processing**: Runs multiple validators in sequence to perform name resolution, type checking, and other semantic analyses
- **Downstream**: Produces validated semantic information and diagnostics for the CodeGen phase (RoslynEmitter)

Think of it as a conveyor belt where each validator is a quality control station that inspects the AST and adds metadata or reports errors.

---

## Class/Type Structure

### Main Class: `ValidationPipeline`

```csharp
public class ValidationPipeline
{
    private readonly List<ISemanticValidator> _validators = new();
    private readonly ICompilerLogger _logger;
}
```

This class follows a **builder pattern** with fluent API for configuration and a simple **pipeline pattern** for execution.

**Key responsibilities:**
1. Manage a collection of validators
2. Automatically sort validators by execution order
3. Execute validators sequentially
4. Aggregate diagnostics from all validators
5. Support early termination on errors

---

## Key Methods

### 1. Constructor

```csharp
public ValidationPipeline(ICompilerLogger? logger = null)
{
    _logger = logger ?? NullLogger.Instance;
}
```

**Purpose**: Initialize an empty pipeline with optional logging.

**Design note**: Uses null-object pattern (`NullLogger.Instance`) to avoid null checks throughout the code.

---

### 2. AddValidator (Fluent API)

```csharp
public ValidationPipeline AddValidator(ISemanticValidator validator)
{
    _validators.Add(validator);
    _validators.Sort((a, b) => a.Order.CompareTo(b.Order));
    return this;
}
```

**Purpose**: Register a validator and maintain sorted order.

**Key implementation details:**
- Automatically re-sorts the validator list after each addition
- Validators are sorted by their `Order` property (lower = earlier)
- Returns `this` for method chaining (builder pattern)

**Order convention** (from `ISemanticValidator` docs):
- NameResolution: 100
- TypeResolution: 200
- TypeChecking: 300
- etc.

**Performance note**: Sorting on every add is fine for compiler initialization (typically 5-10 validators), but could be optimized if validators are added dynamically.

---

### 3. AddValidators (Bulk Registration)

```csharp
public ValidationPipeline AddValidators(params ISemanticValidator[] validators)
{
    foreach (var validator in validators)
    {
        AddValidator(validator);
    }
    return this;
}
```

**Purpose**: Convenience method for registering multiple validators at once.

**Usage example:**
```csharp
var pipeline = new ValidationPipeline()
    .AddValidators(
        new NameResolutionValidator(),
        new TypeResolutionValidator(),
        new TypeCheckingValidator()
    );
```

---

### 4. RemoveValidator (Dynamic Configuration)

```csharp
public ValidationPipeline RemoveValidator<T>() where T : ISemanticValidator
{
    _validators.RemoveAll(v => v is T);
    return this;
}
```

**Purpose**: Remove all validators of a specific type.

**Use cases:**
- Testing: Remove validators to isolate specific validation logic
- Customization: Allow users to disable certain checks
- Migration: Temporarily disable legacy validators during refactoring

---

### 5. Validate (Core Execution Logic)

```csharp
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
```

**Purpose**: Execute all validators in order and collect diagnostics.

**Algorithm:**
1. Iterate through validators in sorted order
2. Before each validator, check if we should continue (based on error threshold)
3. Track errors before and after each validator
4. Log validator execution and error counts
5. Return aggregated diagnostics

**Key parameters:**
- `module`: The AST root node to validate
- `context`: Shared semantic context containing:
  - `SymbolTable`: Name bindings
  - `SemanticInfo`: Type annotations
  - `TypeResolver`: Type lookup utilities
  - `Diagnostics`: Error/warning collection
  - Configuration flags (error limits, continue-after-errors)

**Early termination logic:**
```csharp
if (!context.ShouldContinue())
```

This checks:
- `ContinueAfterErrors` flag (default: true)
- `MaxErrors` threshold (default: 100)
- Current error count

**Design rationale**: Validators share mutable state through `context`, allowing later validators to use results from earlier ones (e.g., type checking depends on name resolution).

---

### 6. Factory Methods

#### CreateDefault

```csharp
public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
{
    // Note: This will be populated as validators are migrated
    return new ValidationPipeline(logger);
}
```

**Purpose**: Create a standard production pipeline.

**Current state**: Empty during migration period. Will eventually include all standard validators.

**Future vision:**
```csharp
return new ValidationPipeline(logger)
    .AddValidators(
        new NameResolutionValidator(),
        new TypeResolutionValidator(),
        new TypeCheckingValidator(),
        new ProtocolValidator(),
        new OperatorValidator(),
        // etc.
    );
```

#### CreateEmpty

```csharp
public static ValidationPipeline CreateEmpty(ICompilerLogger? logger = null)
{
    return new ValidationPipeline(logger);
}
```

**Purpose**: Create an empty pipeline for testing or custom configurations.

---

## Dependencies

### Internal Sharpy Dependencies

1. **`ISemanticValidator` interface** (`src/Sharpy.Compiler/Semantic/Validation/ISemanticValidator.cs`)
   - Contract that all validators must implement
   - Defines `Name`, `Order`, and `Validate()` method

2. **`SemanticContext` class** (`src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs`)
   - Shared context passed to all validators
   - Contains symbol table, semantic info, type resolver, diagnostics

3. **`Module` (AST node)** (`Sharpy.Compiler.Parser.Ast`)
   - Root node of the abstract syntax tree
   - Represents a complete `.spy` source file

4. **`DiagnosticBag`** (`Sharpy.Compiler.Diagnostics`)
   - Collection of errors and warnings

5. **`ICompilerLogger`** (`Sharpy.Compiler.Logging`)
   - Logging abstraction for debugging and diagnostics

### Related Validators

Current validators in the system (as of this walkthrough):
- `AccessValidatorV2` - Validates access modifiers
- `DefaultParameterValidatorV2` - Validates default parameter rules
- `OperatorValidatorV2` - Validates operator overloading
- `ProtocolValidatorV2` - Validates protocol (interface) compliance
- `SignatureValidatorV2` - Validates function signatures
- `ControlFlowValidatorV3` - Validates control flow (return paths, unreachable code)
- `LegacyValidatorAdapter` - Adapts old validators to new pipeline

---

## Patterns and Design Decisions

### 1. **Pipeline Pattern**
The core architectural pattern. Each validator is a stage that:
- Receives the same AST
- Mutates shared context
- Reports errors independently
- Can depend on earlier stages

**Benefits:**
- Clear separation of concerns
- Easy to add/remove validators
- Natural ordering for dependencies

### 2. **Builder/Fluent API**
Methods return `this` for method chaining:
```csharp
var pipeline = new ValidationPipeline()
    .AddValidator(v1)
    .AddValidator(v2)
    .RemoveValidator<OldValidator>();
```

### 3. **Automatic Ordering**
Validators declare their order via the `Order` property. The pipeline automatically sorts them, preventing ordering bugs.

**Alternative considered (rejected)**: Manual ordering by registration sequence.
**Why rejected**: Error-prone, especially as validator count grows.

### 4. **Shared Mutable Context**
All validators share a single `SemanticContext` instance.

**Benefits:**
- Later validators can use results from earlier ones
- Avoids copying large data structures
- Natural for multi-pass analysis

**Trade-offs:**
- Validators are not independent (side effects)
- Order matters significantly
- Harder to parallelize (though comments suggest future parallel execution for same-order validators)

### 5. **Null Object Pattern**
```csharp
_logger = logger ?? NullLogger.Instance;
```

Avoids null checks throughout the code.

### 6. **Error Budget / Circuit Breaker**
```csharp
if (!context.ShouldContinue())
    break;
```

Prevents wasted work when errors exceed threshold. Particularly important for:
- Large files with early fatal errors
- Cascading errors (one early error triggers many downstream errors)

---

## Future Extensibility (from Design Notes)

The class includes forward-looking comments for future features:

### 1. **LSP (Language Server Protocol) Support**
```csharp
// LSP: Pipeline can skip unchanged validators based on change tracking
```

**Vision**: When a file changes, only re-run validators affected by the change.

**Example**: If only a function body changes, skip name resolution and re-run only type checking.

### 2. **Parallel Execution**
```csharp
// Parallel: Validators at same order level could potentially run in parallel
```

**Vision**: Validators with the same `Order` value could run concurrently (if they don't conflict).

**Example**: Module-level and class-level validators might run in parallel if they operate on different AST subtrees.

**Requirement**: Validators must be stateless/thread-safe.

### 3. **Runtime Registration**
```csharp
// Extensibility: New validators can be registered at runtime
```

**Vision**: Plugin system where third-party validators can be added.

**Example**: Custom linting rules, project-specific validation logic.

---

## Debugging Tips

### 1. **Enable Debug Logging**
Pass a logger to see validator execution:
```csharp
var logger = new ConsoleLogger { MinLevel = LogLevel.Debug };
var pipeline = new ValidationPipeline(logger);
```

Output will show:
- Which validators are running
- Execution order
- Errors reported by each validator

### 2. **Inspect Validator Order**
Use the `Validators` property to verify registration and ordering:
```csharp
foreach (var v in pipeline.Validators)
{
    Console.WriteLine($"{v.Order}: {v.Name}");
}
```

### 3. **Isolate Validators**
To debug a specific validator:
```csharp
var pipeline = ValidationPipeline.CreateEmpty()
    .AddValidator(new SuspiciousValidator());
```

### 4. **Set Breakpoint in Validate Loop**
Place a conditional breakpoint at line 72 (start of foreach loop):
```csharp
// Break when running a specific validator
validator.Name == "OperatorValidator"
```

### 5. **Check Error Counts**
If you see unexpected error counts, add logging:
```csharp
var errorsBefore = context.Diagnostics.ErrorCount;
validator.Validate(module, context);
var errorsAfter = context.Diagnostics.ErrorCount;
Console.WriteLine($"{validator.Name}: +{errorsAfter - errorsBefore} errors");
```

### 6. **Verify ShouldContinue Logic**
If the pipeline stops early unexpectedly:
```csharp
Console.WriteLine($"Continue after errors: {context.ContinueAfterErrors}");
Console.WriteLine($"Max errors: {context.MaxErrors}");
Console.WriteLine($"Current errors: {context.Diagnostics.ErrorCount}");
```

---

## Contribution Guidelines

### When to Modify ValidationPipeline

1. **Adding core pipeline functionality**
   - Example: Add support for validator dependencies (explicit ordering)
   - Example: Implement parallel execution for same-order validators

2. **Changing pipeline behavior**
   - Example: Add skip-on-error semantics for certain validators
   - Example: Implement validator caching for LSP

3. **Updating factory methods**
   - **Most common**: Update `CreateDefault()` when new validators are ready for production

### What NOT to Change Here

1. **Validator logic** - That belongs in individual validator classes
2. **Semantic analysis algorithms** - Those are in `TypeChecker`, `TypeResolver`, etc.
3. **AST structure** - That's in `Sharpy.Compiler.Parser.Ast`

### Adding a New Validator

**Steps:**
1. Create a new validator class implementing `ISemanticValidator` (or extending `SemanticValidatorBase`)
2. Assign an appropriate `Order` value (follow existing conventions)
3. Implement `Validate()` method
4. Add to `CreateDefault()` factory method (when ready for production)
5. Add tests using `CreateEmpty().AddValidator(new YourValidator())`

**Example:**
```csharp
public class ExhaustivenessValidator : SemanticValidatorBase
{
    public override string Name => "ExhaustivenessValidator";
    public override int Order => 400; // After type checking (300)

    public override void Validate(Module module, SemanticContext context)
    {
        // Check match expressions for exhaustiveness
        // ...
    }
}
```

Then update `CreateDefault()`:
```csharp
public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
{
    return new ValidationPipeline(logger)
        .AddValidators(
            // ... existing validators ...
            new ExhaustivenessValidator()
        );
}
```

### Testing Guidelines

1. **Unit test individual validators** in isolation:
   ```csharp
   var pipeline = ValidationPipeline.CreateEmpty()
       .AddValidator(new MyValidator());
   ```

2. **Integration test the full pipeline**:
   ```csharp
   var pipeline = ValidationPipeline.CreateDefault();
   ```

3. **Test validator ordering**:
   ```csharp
   var pipeline = new ValidationPipeline()
       .AddValidator(new OrderTestValidator(300))
       .AddValidator(new OrderTestValidator(100));

   Assert.Equal(100, pipeline.Validators[0].Order);
   ```

---

## Cross-References

### Related Documentation
- [ISemanticValidator.cs](./ISemanticValidator.md) - Validator interface and base class
- [SemanticContext.cs](./SemanticContext.md) - Shared context for validators
- [AstTraversalContext.md](./AstTraversalContext.md) - AST traversal state management

### Related Validators
- `OperatorValidatorV2.md` - Example validator implementation
- `ProtocolValidatorV2.md` - Example validator implementation
- `LegacyValidatorAdapter.md` - Adapter for legacy validators

### Upstream/Downstream
- **Upstream**: `Parser` (produces AST)
- **Downstream**: `RoslynEmitter` (consumes validated AST + SemanticInfo)
- **Side input**: `TypeChecker`, `TypeResolver` (populate SemanticContext before validation)

---

## Migration Status

This file is part of a **v2 validation architecture**. The codebase is currently in a migration period:

**Legacy approach:**
- Monolithic `TypeChecker` class with all validation logic
- Direct mutation of AST nodes
- Ad-hoc error collection

**New approach (ValidationPipeline):**
- Modular validators implementing `ISemanticValidator`
- Immutable AST with separate `SemanticInfo` annotations
- Centralized `DiagnosticBag`

**Current state:**
- `CreateDefault()` is empty (validators being migrated incrementally)
- `LegacyValidatorAdapter` bridges old and new systems
- Validators with "V2" or "V3" suffix are new pipeline-compatible implementations

**Watch for:**
- Comments referencing "migration period"
- Methods marked `[Obsolete]` in `SemanticContext`
- Validators in both legacy and V2 versions
