# Walkthrough: ValidationPipelineFactory.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs`

---

## Overview

The `ValidationPipelineFactory` is a static factory class that provides pre-configured validation pipelines for the Sharpy compiler's semantic analysis phase. It acts as a **configuration hub**, offering different pipeline configurations optimized for various scenarios: standard compilation, fast IDE/LSP scenarios, CFG-based analysis, and minimal testing.

**Role in the Compiler Pipeline:**
- **Position**: Semantic Analysis phase (after parsing, before code generation)
- **Purpose**: Provides standardized, ready-to-use validation pipelines without requiring clients to manually assemble validators
- **Upstream**: Called by components that need to validate AST modules (e.g., `Compiler.cs:132`, `ProjectCompiler`, test harnesses)
- **Downstream**: Creates `ValidationPipeline` instances that will execute various validators

Think of this as the "menu" for validation configurations - it knows which validators to include, in what order, and for what purpose.

---

## Class Structure

### Static Factory Class

```csharp
public static class ValidationPipelineFactory
```

This is a **pure static factory** with no state or instance members. All methods are static and return pre-configured `ValidationPipeline` instances.

**Design Decision**: Using a static class (rather than instance-based factory) is appropriate here because:
- No per-instance configuration is needed
- No dependency injection required at the factory level (DI happens at the pipeline level via logger)
- Simple API: `ValidationPipelineFactory.CreateDefault()` is cleaner than `new ValidationPipelineFactory().CreateDefault()`

---

## Key Methods

### 1. `CreateDefault(ICompilerLogger? logger = null)`

**Purpose**: Creates the **standard, production-quality** validation pipeline used for normal compilation.

```csharp
public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
{
    return new ValidationPipeline(logger)
        // Order values determine execution sequence
        .AddValidator(new ModuleLevelValidator())       // Order: 50 (earliest, validates module structure)
        .AddValidator(new SignatureValidator())         // Order: 150 (early, validates dunder signatures)
        .AddValidator(new DefaultParameterValidator())  // Order: 250
        .AddValidator(new ControlFlowValidator())       // Order: 400 (AST-walking, handles unreachable code)
        .AddValidator(new AccessValidator())            // Order: 450
        .AddValidator(new ProtocolValidator())          // Order: 500
        .AddValidator(new OperatorValidator())          // Order: 500
        ;
}
```

**Key Implementation Details:**

1. **Fluent Builder Pattern**: Uses method chaining via `ValidationPipeline.AddValidator()` which returns `this`
2. **Auto-Sorting**: Validators are automatically sorted by their `Order` property (lower numbers run first)
3. **Validator Sequence** (in execution order):
   - **ModuleLevelValidator** (50): Validates module-level structure (earliest validator, ensures module is well-formed)
   - **SignatureValidator** (150): Validates special method signatures (dunder methods like `__init__`, `__str__`)
   - **DefaultParameterValidator** (250): Checks default parameter rules (no mutable defaults, positional before keyword, etc.)
   - **ControlFlowValidator** (400): AST-walking control flow analysis
   - **AccessValidator** (450): Validates member access (private/protected/public)
   - **ProtocolValidator** (500): Validates protocol implementations
   - **OperatorValidator** (500): Validates operator overloading

**Why This Order Matters:**
- Module-level validation runs **first** because structural errors can cascade into other validators
- Signature validation runs **early** because invalid signatures can cause cascading errors
- Control flow runs **before** access validation (need to understand code structure first)
- Protocol and operator validation run **last** (they depend on earlier validations)

**Important Comments in Code:**
```csharp
// Uses AST-walking control flow analysis (V2) which correctly handles
// unreachable code detection (V3 CFG-based approach can't detect unreachable
// code because the CFG builder skips statements after terminators).
```

This explains a **critical architectural decision**: V2 is chosen over V3 for unreachable code detection capabilities, even though V3 might be faster.

---

### 2. `CreateWithCfgControlFlow(ICompilerLogger? logger = null)`

**Purpose**: Alternative pipeline using **CFG-based control flow analysis** (V3) instead of AST-walking (V2).

```csharp
public static ValidationPipeline CreateWithCfgControlFlow(ICompilerLogger? logger = null)
{
    return new ValidationPipeline(logger)
        .AddValidator(new ModuleLevelValidator())       // Order: 50 (earliest)
        .AddValidator(new SignatureValidator())
        .AddValidator(new DefaultParameterValidator())
        .AddValidator(new ControlFlowValidator())       // CFG-based validator
        .AddValidator(new AccessValidator())
        .AddValidator(new ProtocolValidator())
        .AddValidator(new OperatorValidator())
        ;
}
```

**When to Use This:**
- Scenarios where **performance matters** more than completeness
- When unreachable code detection is not critical
- Experimentation with CFG-based approaches

**Trade-off**: V3 is faster but **cannot detect unreachable code** because the CFG builder skips statements after terminators (return, break, continue, etc.).

**Example of What V3 Misses:**
```python
def foo():
    return 42
    print("unreachable")  # V2 detects this, V3 doesn't
```

---

### 3. `CreateMinimal(ICompilerLogger? logger = null)`

**Purpose**: Creates an **empty pipeline** with no validators.

```csharp
public static ValidationPipeline CreateMinimal(ICompilerLogger? logger = null)
{
    return new ValidationPipeline(logger);
}
```

**When to Use This:**
- Unit testing individual validators (add just the one you're testing)
- Building custom validation pipelines programmatically
- Debugging scenarios where you want to isolate specific validators

**Example Usage in Tests:**
```csharp
var pipeline = ValidationPipelineFactory.CreateMinimal()
    .AddValidator(new MyCustomValidator());  // Test only this validator
```

---

### 4. `CreateFast(ICompilerLogger? logger = null)`

**Purpose**: Creates a **minimal, fast pipeline** optimized for IDE/LSP scenarios where speed is critical.

```csharp
public static ValidationPipeline CreateFast(ICompilerLogger? logger = null)
{
    return new ValidationPipeline(logger)
        .AddValidator(new ControlFlowValidator());  // V2 is faster for quick checks
    // Skip signature validators, protocol validators, etc.
}
```

**Design Philosophy:**
- **Speed over completeness**: Only include validators that provide high value for IDE scenarios
- Currently includes only `ControlFlowValidator` (catches unreachable code and missing returns)
- Skips expensive validators like module-level validation, protocol checking, operator validation, signature validation

**Use Case**: Language Server Protocol (LSP) scenarios where the IDE needs fast feedback:
- Real-time diagnostics as you type
- Quick checks during autocomplete
- Background validation while editing

**Trade-off**: Users might see some errors only during full compilation, not in the IDE.

**Comment Note**: The inline comment says "V2 is faster for quick checks" - this refers to V2 being the appropriate choice for fast scenarios, not that V2 is faster than V3 (V3 is actually faster, but V2 provides better error coverage).

---

## Dependencies

### Direct Dependencies

1. **Sharpy.Compiler.Logging**:
   - `ICompilerLogger` - Optional logging interface passed to pipelines
   - Used for diagnostic output during validation

### Indirect Dependencies (Validators)

All validators referenced in this factory:
- `ModuleLevelValidator`
- `SignatureValidator`
- `DefaultParameterValidator`
- `ControlFlowValidator`
- `ControlFlowValidator`
- `AccessValidator`
- `ProtocolValidator`
- `OperatorValidator`

Each implements `ISemanticValidator` interface.

### Relationship with ValidationPipeline

The factory creates instances of `ValidationPipeline`, which:
- Stores validators in a sorted list
- Executes them in order during `Validate()`
- Manages logging and error collection
- Provides early termination if error limits are reached

---

## Patterns and Design Decisions

### 1. **Static Factory Pattern**

**Why Static?**
- No state to maintain
- Simple, discoverable API
- No need for dependency injection at factory level (logging is handled at pipeline level)

**Alternative Considered**: Instance-based factory with constructor injection
**Rejected Because**: Adds unnecessary complexity for a stateless factory

---

### 2. **Named Factory Methods (Intention-Revealing)**

Each factory method name clearly communicates **intent**:
- `CreateDefault()` → "Give me the standard pipeline"
- `CreateFast()` → "I need speed over completeness"
- `CreateWithCfgControlFlow()` → "I want the CFG-based variant"
- `CreateMinimal()` → "Give me an empty slate"

**Benefit**: Clients don't need to understand validator details to choose the right pipeline.

---

### 3. **Fluent Builder Delegation**

```csharp
return new ValidationPipeline(logger)
    .AddValidator(...)
    .AddValidator(...);
```

The factory **delegates** the actual builder pattern to `ValidationPipeline` itself. This keeps the factory simple and leverages the pipeline's fluent API.

---

### 4. **Explicit Ordering Comments**

```csharp
.AddValidator(new ModuleLevelValidator())       // Order: 50 (earliest, validates module structure)
.AddValidator(new SignatureValidator())         // Order: 150 (early, validates dunder signatures)
.AddValidator(new DefaultParameterValidator())  // Order: 250
```

**Why Comments?**
- Validators self-report their order via the `Order` property
- Comments make the execution sequence visible **without running the code**
- Helps reviewers understand dependencies between validators
- Inline descriptions clarify each validator's purpose

**Important**: The actual order comes from `ISemanticValidator.Order`, not these comments. Comments are documentation only.

---

### 5. **Optional Logging Parameter**

All factory methods accept `ICompilerLogger? logger = null`:
- **Null by default**: Simple usage doesn't require passing a logger
- **Optional injection**: Advanced scenarios can provide custom logging
- **Null handling**: `ValidationPipeline` defaults to `NullLogger.Instance` if null

---

## Debugging Tips

### 1. **Validating Factory Output**

To see which validators are actually in a pipeline:

```csharp
var pipeline = ValidationPipelineFactory.CreateDefault();
foreach (var validator in pipeline.Validators)
{
    Console.WriteLine($"{validator.Name} (Order: {validator.Order})");
}
```

The `Validators` property exposes the internal list for inspection.

---

### 2. **Testing Different Configurations**

If you suspect a specific validator is causing issues:

```csharp
// Start with minimal and add validators one by one
var pipeline = ValidationPipelineFactory.CreateMinimal()
    .AddValidator(new ModuleLevelValidator())
    .AddValidator(new SignatureValidator())
    .AddValidator(new ControlFlowValidator());
```

---

### 3. **Logging Validator Execution**

Pass a logger to see what's happening:

```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
var pipeline = ValidationPipelineFactory.CreateDefault(logger);
// Logs will show:
// "Running validator: ModuleLevelValidator (order: 50)"
// "Running validator: SignatureValidator (order: 150)"
// etc.
```

---

### 4. **V2 vs V3 Control Flow Debugging**

If you're debugging unreachable code detection:
- Use `CreateDefault()` (includes V2) for full unreachable code analysis
- Use `CreateWithCfgControlFlow()` (uses V3) if you suspect V2 has false positives

**Symptom**: Code flagged as unreachable but it's actually reachable?
- Likely a bug in `ControlFlowValidator`'s AST walking

**Symptom**: Unreachable code NOT detected?
- You might be using V3 (check which factory method was called)
- Or the code might actually be reachable through a path V2 missed

---

### 5. **Performance Profiling**

To measure validator performance:

```csharp
var pipeline = ValidationPipelineFactory.CreateDefault(new StopwatchLogger());
// StopwatchLogger logs time between validators
```

Compare with `CreateFast()` to see which validators are expensive.

---

## Contribution Guidelines

### When to Modify This File

1. **Adding a New Validator**:
   - Add it to `CreateDefault()` at the appropriate order position
   - Consider whether it belongs in `CreateFast()` (usually no, unless it's critical for IDE scenarios)
   - Consider whether it should be in `CreateWithCfgControlFlow()` (usually yes, if it's not the control flow validator itself)
   - Update comments with the validator's order value and brief description

2. **Creating a New Pipeline Configuration**:
   - Add a new static method (e.g., `CreateForLsp()`, `CreateForTesting()`)
   - Document the purpose clearly in XML comments
   - Explain trade-offs in the method summary
   - Consider whether this configuration should include `ModuleLevelValidator` (almost always yes)

3. **Changing Validator Order**:
   - Update the order comment if the validator's `Order` property changes
   - Consider dependencies between validators (does A depend on B's output?)
   - Run full test suite to catch ordering issues
   - Verify that `ModuleLevelValidator` remains first (Order: 50)

4. **Deprecating a Validator**:
   - Don't remove it immediately (breaks existing code)
   - Mark it with `[Obsolete]` in the validator class
   - Provide migration guidance in comments
   - Create a new factory method without the deprecated validator

---

### What NOT to Change

1. **Don't Add Configuration Parameters**:
   - Keep factory methods simple and parameterless (except logger)
   - If you need configurability, create a new factory method instead
   - Example: Don't add `CreateDefault(bool skipProtocolValidation)`, instead create `CreateWithoutProtocols()`

2. **Don't Add Conditional Logic**:
   ```csharp
   // BAD: Don't do this
   if (someFlag) {
       pipeline.AddValidator(new FooValidator());
   }

   // GOOD: Create separate factory methods
   CreateWithFoo() { ... }
   CreateWithoutFoo() { ... }
   ```

3. **Don't Modify Validator Instances**:
   - Validators should be stateless and immutable
   - Don't try to configure validators here (do it in their constructors)

4. **Don't Remove ModuleLevelValidator**:
   - It's the foundation validator that ensures module structure is sound
   - It must always be first (Order: 50)
   - Other validators may depend on its guarantees

---

### Testing Your Changes

After modifying this file:

1. **Build the Solution**:
   ```bash
   dotnet build sharpy.sln
   ```

2. **Run Semantic Tests**:
   ```bash
   dotnet test --filter "FullyQualifiedName~Semantic"
   ```

3. **Run Validation Pipeline Tests**:
   ```bash
   dotnet test --filter "FullyQualifiedName~ValidationPipeline"
   ```

4. **Run Integration Tests**:
   ```bash
   dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
   ```

5. **Manual Smoke Test**:
   ```bash
   dotnet run --project src/Sharpy.Cli -- run test.spy
   ```

---

## Cross-References

### Related Documentation Files

- **[ValidationPipeline.md](ValidationPipeline.md)** - The pipeline orchestrator that this factory creates
- **[ISemanticValidator.md](ISemanticValidator.md)** - The validator interface and base class
- **[SemanticContext.md](SemanticContext.md)** - The context passed to validators during validation

### Validators Referenced in This Factory

- **[ModuleLevelValidator.md](ModuleLevelValidator.md)** - Validates module-level structure (runs first)
- **[SignatureValidator.md](SignatureValidator.md)** - Validates special method signatures
- **[DefaultParameterValidator.md](DefaultParameterValidator.md)** - Checks default parameter rules
- **[ControlFlowValidator.md](ControlFlowValidator.md)** - AST-walking control flow analysis (default)
- **[ControlFlowValidator.md](ControlFlowValidator.md)** - CFG-based control flow analysis (alternative)
- **[AccessValidator.md](AccessValidator.md)** - Member access validation
- **[ProtocolValidator.md](ProtocolValidator.md)** - Protocol implementation validation
- **[OperatorValidator.md](OperatorValidator.md)** - Operator overloading validation

### Upstream Components (Callers)

To find where factory methods are called:

```bash
# Find all usages
grep -r "ValidationPipelineFactory" src/ --include="*.cs"
```

**Known Callers:**
- **Compiler.cs:132** - Uses `CreateDefault()` for single-file compilation
- **ProjectCompiler** - Uses `CreateDefault()` for multi-file projects
- **Test harnesses** - Use `CreateMinimal()` and `CreateDefault()`
- **LSP server** (future) - Might use `CreateFast()` for real-time analysis

---

## Architecture Notes

### Why Not Use Dependency Injection?

**Question**: Why not inject a factory interface and register configurations in DI?

**Answer**: This factory is **intentionally simple**:
- Configurations are **compile-time constants** (not runtime config)
- No per-request or per-user configuration needed
- Static methods are easier to discover and use
- DI would add complexity without benefit
- The logger (the only configurable dependency) is already injectable at the pipeline level

**If This Changes**: If you need runtime configuration (e.g., user settings for which validators to enable), consider:
1. Creating a configuration object (`ValidationPipelineOptions`)
2. Building pipelines from that configuration
3. Still keeping these static factories as "presets"

---

### Why ValidationPipeline Has Its Own CreateDefault()

**Note**: `ValidationPipeline` class also has a `CreateDefault()` static method (see line 100-104 in ValidationPipeline.cs). However, it's marked with a comment saying "Note: This will be populated as validators are migrated".

**Current Status**: The factory in `ValidationPipelineFactory.CreateDefault()` is the **authoritative** default configuration. The one in `ValidationPipeline` appears to be legacy/transitional.

**Best Practice**: Always use `ValidationPipelineFactory.CreateDefault()`, not `ValidationPipeline.CreateDefault()`.

---

### Future Evolution: Plugin System

**Current State**: All validators are hard-coded in this factory.

**Future Possibility**: Plugin-based validators
```csharp
// Hypothetical future API
public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
{
    var pipeline = new ValidationPipeline(logger);

    // Load built-in validators
    pipeline.AddValidator(new ModuleLevelValidator());
    pipeline.AddValidator(new SignatureValidator());
    // ...

    // Load plugins from assemblies
    var pluginValidators = ValidatorPluginLoader.LoadPlugins();
    pipeline.AddValidators(pluginValidators);

    return pipeline;
}
```

This would enable third-party validators without modifying Sharpy core.

---

## Common Pitfalls

### 1. **Forgetting to Sort Validators**

**Symptom**: Validators run in wrong order

**Cause**: `ValidationPipeline.AddValidator()` sorts automatically, but if you manually manipulate the list (not recommended), you'll break ordering.

**Solution**: Always use `AddValidator()` or `AddValidators()`, never access `_validators` directly.

---

### 2. **Validator Order Dependencies**

**Symptom**: Validator A fails because it depends on Validator B's output, but B runs after A.

**Example**:
```csharp
// BAD: AccessValidator needs control flow info
.AddValidator(new AccessValidator())        // Order: 450
.AddValidator(new ControlFlowValidator())   // Order: 400 (runs AFTER access!)
```

**Solution**: Check validator `Order` values. Earlier validators (lower numbers) run first.

---

### 3. **Over-Using CreateFast()**

**Symptom**: Users report errors during full build that weren't shown in IDE.

**Cause**: `CreateFast()` skips many validators (including `ModuleLevelValidator`, signatures, protocols, operators, access, defaults).

**Solution**: Use `CreateFast()` only for IDE/LSP scenarios, never for final builds.

---

### 4. **Assuming V3 = Better**

**Symptom**: Unreachable code not detected.

**Cause**: Using `CreateWithCfgControlFlow()` which uses V3.

**Fact**: V3 is faster but less complete. V2 is the default for a reason.

---

### 5. **Skipping ModuleLevelValidator**

**Symptom**: Cryptic errors in later validators about malformed AST

**Cause**: Creating custom pipelines without including `ModuleLevelValidator`

**Solution**: Almost every pipeline should start with `ModuleLevelValidator` (Order: 50)

---

## Summary

The `ValidationPipelineFactory` is a **simple, focused factory** that provides pre-configured validation pipelines for different scenarios. Key takeaways:

1. **Four Configurations**:
   - `CreateDefault()`: Standard, production-quality validation (7 validators)
   - `CreateWithCfgControlFlow()`: CFG-based variant (faster, less complete)
   - `CreateFast()`: Minimal validation for IDE/LSP (1 validator only)
   - `CreateMinimal()`: Empty pipeline for testing (0 validators)

2. **Design Principles**:
   - Static factory (stateless)
   - Intention-revealing method names
   - Delegates builder pattern to `ValidationPipeline`
   - Well-documented trade-offs

3. **Validator Ordering Matters**:
   - Module-level → Signature → Defaults → Control Flow → Access → Protocols/Operators
   - Earlier validators catch problems that could cause cascading errors
   - `ModuleLevelValidator` (Order: 50) is the foundation that always runs first

4. **V2 vs V3 Control Flow**:
   - V2 (default): AST-walking, detects unreachable code
   - V3 (optional): CFG-based, faster but misses unreachable code

5. **Usage Pattern**:
   ```csharp
   // In Compiler.cs
   var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
   var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger, pipeline);
   ```

When in doubt, use `CreateDefault()` - it's battle-tested and covers all standard validation scenarios.
