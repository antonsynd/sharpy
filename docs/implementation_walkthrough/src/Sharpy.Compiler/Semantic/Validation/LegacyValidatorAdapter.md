# Walkthrough: LegacyValidatorAdapter.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/LegacyValidatorAdapter.cs`

---

## Overview

`LegacyValidatorAdapter` is a **bridge pattern** implementation that enables the gradual migration of older validation code into Sharpy's newer validation pipeline architecture. It wraps legacy validators (like `ControlFlowValidator` and `AccessValidator`) that predate the `ISemanticValidator` interface, allowing them to work seamlessly within the modern `ValidationPipeline`.

**Role in the Compiler Pipeline:**
- **Component**: Semantic Analysis (Validation Phase)
- **Upstream**: Receives AST from Parser via ValidationPipeline
- **Downstream**: Contributes diagnostics to SemanticContext for use by CodeGen
- **Transitional Nature**: This is explicitly marked as deprecated and exists only to support legacy code during migration

**Key Insight**: This adapter is a **temporary scaffolding** that enables the team to migrate validators incrementally rather than requiring a complete rewrite all at once. It's not intended for new code—all new validators should implement `ISemanticValidator` directly.

---

## Class Structure

### Main Class: `LegacyValidatorAdapter`

```csharp
public class LegacyValidatorAdapter : ISemanticValidator
```

The adapter implements the modern `ISemanticValidator` interface while internally delegating to legacy validation code that uses a different API style.

**Properties:**
- `Name` (string): Identifier for logging/debugging (e.g., "ControlFlowValidator")
- `Order` (int): Execution order hint for the pipeline (lower = earlier)

**Private Fields:**
```csharp
private readonly Action<Module, SemanticContext> _validateAction;
private readonly Func<IReadOnlyList<SemanticError>>? _getErrors;
```

- `_validateAction`: The actual validation logic to execute
- `_getErrors`: Optional callback to retrieve errors from the legacy validator's internal error collection

---

## Key Methods

### Constructor

```csharp
public LegacyValidatorAdapter(
    string name,
    int order,
    Action<Module, SemanticContext> validateAction,
    Func<IReadOnlyList<SemanticError>>? getErrors = null)
```

**Purpose**: Creates an adapter that wraps arbitrary validation logic.

**Parameters:**
- `name`: Validator identifier (shows up in logs)
- `order`: Pipeline execution order (e.g., 400 for control flow, which runs after type checking at 300)
- `validateAction`: Lambda/delegate containing the validation logic
- `getErrors`: Optional callback to retrieve errors from legacy validators that maintain their own error lists

**Design Rationale**: By accepting a delegate (`Action<Module, SemanticContext>`), this adapter can wrap any validation function without needing to know its internal implementation details.

---

### Validate Method

```csharp
public void Validate(Module module, SemanticContext context)
{
    _validateAction(module, context);

    // If the legacy validator has an error collection, merge them
    if (_getErrors != null)
    {
        context.MergeFromLegacyErrors(_getErrors());
    }
}
```

**Purpose**: Implements the `ISemanticValidator` interface by executing the wrapped validation logic.

**Flow:**
1. **Execute**: Call the wrapped validation action with the module and context
2. **Merge Errors**: If the legacy validator maintains its own error list (common in older code), extract those errors and merge them into the modern `DiagnosticBag` in `SemanticContext`

**Why Error Merging?** Legacy validators use `List<SemanticError>`, while the new pipeline uses `DiagnosticBag`. The adapter bridges this gap by converting between the two formats via `SemanticContext.MergeFromLegacyErrors()` (see line 42).

---

### Factory Method: ForControlFlowValidator

```csharp
public static LegacyValidatorAdapter ForControlFlowValidator(
    ControlFlowValidator validator,
    ICompilerLogger? logger = null)
```

**Purpose**: Creates a specialized adapter for the legacy `ControlFlowValidator`.

**Key Implementation Details:**

```csharp
return new LegacyValidatorAdapter(
    "ControlFlowValidator",
    400, // Run after type checking (order 300)
    (module, context) =>
    {
        // ControlFlowValidator validates functions individually
        // We need to traverse the module and validate each function
        foreach (var stmt in module.Body)
        {
            if (stmt is FunctionDef funcDef)
            {
                var returnType = GetFunctionReturnType(funcDef, context);
                validator.ValidateFunction(funcDef, returnType);
            }
            // ... similar handling for ClassDef and StructDef members
        }
    },
    () => validator.Errors
);
```

**What This Does:**
1. **AST Traversal**: The legacy `ControlFlowValidator` operates on individual functions, not entire modules. The adapter manually traverses the module's AST to find all functions (both top-level and within classes/structs).
2. **Return Type Resolution**: For each function, it resolves the return type annotation (line 64-65) using the helper method `GetFunctionReturnType()`.
3. **Validation**: Calls `validator.ValidateFunction()` for each discovered function.
4. **Error Collection**: Provides a callback to extract errors from the validator's internal `Errors` list (line 91).

**Why Order 400?** Control flow validation requires type information to be available, so it runs *after* type checking (which typically runs at order 300).

---

### Helper Method: GetFunctionReturnType

```csharp
private static SemanticType GetFunctionReturnType(FunctionDef funcDef, SemanticContext context)
{
    if (funcDef.ReturnType == null)
        return SemanticType.Void;

    // Try to get from semantic info cache first
    var cachedType = context.SemanticInfo.GetTypeAnnotation(funcDef.ReturnType);
    if (cachedType != null)
        return cachedType;

    // Fall back to resolving the type annotation
    return context.TypeResolver.ResolveTypeAnnotation(funcDef.ReturnType);
}
```

**Purpose**: Extracts the return type of a function from its type annotation.

**Performance Optimization**: First checks the `SemanticInfo` cache (line 104) before falling back to full type resolution (line 109). This avoids redundant work if type annotations have already been resolved in earlier pipeline stages.

**Handles Untyped Functions**: If no return type annotation exists, defaults to `SemanticType.Void` (line 101).

---

### Factory Method: ForAccessValidator

```csharp
public static LegacyValidatorAdapter ForAccessValidator(
    AccessValidator validator,
    ICompilerLogger? logger = null)
```

**Purpose**: Creates an adapter for the legacy `AccessValidator`.

**Important Note**: The implementation is mostly a no-op (line 125-128) because `AccessValidator` is called **on-demand during type checking** rather than as a standalone validation pass. This factory method exists primarily for:
- **Testing**: Allows tests to instantiate the validator through the pipeline
- **Completeness**: Provides a consistent API even for validators that don't fit the batch validation model

---

## Dependencies

### Internal Dependencies

| Dependency | Purpose |
|------------|---------|
| `ISemanticValidator` | Interface this adapter implements (see `ISemanticValidator.cs`) |
| `SemanticContext` | Shared context containing symbols, types, diagnostics |
| `ControlFlowValidator` | Legacy validator for control flow analysis |
| `AccessValidator` | Legacy validator for access level checking |
| `Module`, `FunctionDef`, `ClassDef`, `StructDef` | AST node types from `Sharpy.Compiler.Parser.Ast` |
| `SemanticType`, `SemanticError` | Semantic analysis types |

### Cross-References

**Related Documentation:**
- `ISemanticValidator.cs` — The modern validator interface
- `SemanticContext.cs` — The shared validation context (especially `MergeFromLegacyErrors()` at line 129)
- `ControlFlowValidator.cs` — Legacy validator being wrapped
- `AccessValidator.cs` — Legacy validator being wrapped
- `ValidationPipelineFactory.cs` — Shows how the default pipeline uses V2 validators instead of this adapter

**Migration Path:**
- `ControlFlowValidatorV3.cs` — Modern replacement for `ControlFlowValidator`
- `AccessValidator.cs` — Modern replacement for `AccessValidator`

---

## Patterns and Design Decisions

### 1. **Adapter Pattern**
The class is a textbook implementation of the Adapter pattern (GoF), converting the interface of legacy validators to match the `ISemanticValidator` interface expected by the new pipeline.

### 2. **Strategy Pattern**
By accepting validation logic as a delegate (`Action<Module, SemanticContext>`), the adapter uses the Strategy pattern to allow different validation behaviors without subclassing.

### 3. **Visitor Pattern (Implicit)**
The `ForControlFlowValidator()` method implements an implicit Visitor pattern by traversing the AST and dispatching to the appropriate validation method for each node type (FunctionDef, ClassDef, StructDef).

### 4. **Explicit Deprecation**
```csharp
[Obsolete("Use ControlFlowValidatorV3 via ValidationPipelineFactory.CreateDefault() instead")]
```
The code uses C# attributes and XML comments to clearly signal that this is transitional code. The `#pragma warning disable CS0618` (line 14) acknowledges intentional use of deprecated types during the migration period.

### 5. **Order-Based Execution**
The `Order` property (e.g., 400 for control flow) enables declarative sequencing of validators without tight coupling. The pipeline sorts validators by order before execution.

### 6. **Error Format Translation**
The adapter bridges two error reporting systems:
- **Legacy**: `List<SemanticError>` with formatted messages like "Semantic error at line X: ..."
- **Modern**: `DiagnosticBag` with structured diagnostics

The `SemanticContext.MergeFromLegacyErrors()` method (called at line 42) performs the translation, even stripping redundant prefixes from legacy error messages.

---

## Debugging Tips

### 1. **Verify Pipeline Order**
If validation seems to run in the wrong sequence, check:
- The `Order` value in the adapter constructor (should be 400 for control flow, 350 for access)
- Whether the pipeline is sorting validators correctly

### 2. **Check Error Merging**
If errors aren't appearing in diagnostics:
- Set a breakpoint at line 40-42 to verify `_getErrors` is not null
- Inspect `validator.Errors` to confirm the legacy validator is actually collecting errors
- Check `SemanticContext.MergeFromLegacyErrors()` to ensure format conversion is working

### 3. **AST Traversal Issues**
If functions aren't being validated:
- Verify the AST structure matches expectations (use `/emit ast file.spy` to inspect)
- Check if functions are nested in unexpected ways (e.g., lambda functions)
- Ensure `module.Body` contains the expected statement types

### 4. **Return Type Resolution Failures**
If control flow validation fails due to missing return types:
- Set a breakpoint in `GetFunctionReturnType()` at line 104 and 109
- Check if `SemanticInfo` cache is populated (should be after type checking)
- Verify `TypeResolver` is available in the context

### 5. **Missing Validators**
If expected validation isn't running:
- Check if `ValidationPipelineFactory.CreateDefault()` is being used (it should NOT use this adapter)
- Look for code still using `LegacyValidatorAdapter.ForControlFlowValidator()` directly (should be migrated)

---

## Contribution Guidelines

### ⚠️ DO NOT Add New Features to This File

This adapter is **deprecated** and should not be extended. Instead:

**For new validators:**
- Implement `ISemanticValidator` directly
- Use `SemanticValidatorBase` for convenience methods
- Add to `ValidationPipelineFactory.CreateDefault()`

**For bug fixes:**
- If the bug is in the adapter itself, fix it here
- If the bug is in the wrapped validator (e.g., `ControlFlowValidator`), consider migrating to the V2 version instead

### Migration Checklist

If you're migrating a legacy validator:

1. ✅ Create a new `{Name}ValidatorV2.cs` file in `Semantic/Validation/`
2. ✅ Implement `ISemanticValidator` (or extend `SemanticValidatorBase`)
3. ✅ Use `context.Diagnostics.AddError()` instead of maintaining your own error list
4. ✅ Add to `ValidationPipelineFactory.CreateDefault()`
5. ✅ Update tests to use the new validator
6. ✅ Mark the old validator with `[Obsolete]`
7. ✅ Remove any `LegacyValidatorAdapter.For{Name}Validator()` usage

### Testing Changes

When modifying this file:
```bash
# Run validation tests
dotnet test --filter "FullyQualifiedName~Semantic"

# Test control flow validation specifically
dotnet test --filter "FullyQualifiedName~ControlFlow"

# Test the integration tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

---

## Historical Context

**Why This Exists**: Early versions of Sharpy had validators tightly coupled to specific components (e.g., `TypeChecker` calling validators directly). The introduction of `ISemanticValidator` and `ValidationPipeline` created a more modular, testable architecture. This adapter allowed the migration to happen incrementally without breaking existing functionality.

**Migration Status** (as of this writing):
- ✅ `ControlFlowValidator` → Migrated to `ControlFlowValidatorV3` (AST-walking) and `ControlFlowValidatorV3` (CFG-based)
- ✅ `AccessValidator` → Migrated to `AccessValidator`
- ✅ Default pipeline uses V2 validators exclusively
- ⏳ Legacy validators still exist for backward compatibility but are marked obsolete

**Future**: Once all calling code is migrated to the new pipeline, this adapter and the legacy validators can be removed entirely (likely in v0.2 or v0.3).

---

## Common Misconceptions

### ❌ "This adapter makes validators work with the pipeline"
**✅ Reality**: The adapter makes *legacy* validators work. New validators should implement `ISemanticValidator` directly without needing an adapter.

### ❌ "Order 400 means it's the 400th validator"
**✅ Reality**: Order is just a sorting key. Common values are 100 (name resolution), 200 (type resolution), 300 (type checking), 400 (control flow), 500 (other semantic checks). The gaps allow inserting new validators between existing ones.

### ❌ "AccessValidator doesn't need traversal logic"
**✅ Reality**: `AccessValidator` handles traversal internally as part of the validator implementation. The adapter's no-op behavior (line 125-128) is specific to how the legacy `AccessValidator` was called on-demand rather than as a batch pass.

---

## Quick Reference

**When to Use This File:**
- ❌ Never for new code
- ✅ Only when maintaining legacy validator integration during migration

**Key Files to Understand:**
1. `ISemanticValidator.cs` — The interface this adapts to
2. `SemanticContext.cs` — The shared validation state
3. `ValidationPipeline.cs` — The orchestrator that uses these adapters
4. `ValidationPipelineFactory.cs` — Shows the modern approach (no adapters!)

**Compiler Command to Inspect Validation:**
```bash
# See all diagnostics (including validation errors)
dotnet run --project src/Sharpy.Cli -- run file.spy

# Enable verbose logging to see validator execution
SHARPY_LOG_LEVEL=Debug dotnet run --project src/Sharpy.Cli -- run file.spy
```
