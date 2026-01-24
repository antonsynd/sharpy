# Walkthrough: SemanticContext.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs`

---

## Overview

`SemanticContext` is the **shared state container** for all semantic validators in the Sharpy compiler. Think of it as the "toolbox and workspace" that validators use during semantic analysis.

**Role in the Pipeline:**
- **Upstream**: Receives the AST from the Parser
- **Current**: Provides validators with symbols, types, diagnostics, and caches
- **Downstream**: Diagnostics flow to the user; SemanticInfo flows to CodeGen (RoslynEmitter)

**Core Responsibilities:**
1. **Centralized access** to `SymbolTable`, `SemanticInfo`, and `TypeResolver`
2. **Diagnostic collection** via `DiagnosticBag`
3. **Shared caching** (e.g., CLR member lookups) to avoid redundant work
4. **AST traversal state** tracking (current class, function, loop context)
5. **Multi-file compilation** support with file-scoped context creation
6. **Error budgeting** (max errors, continue-after-errors configuration)

This class is designed with **future extensibility** in mind:
- Thread-safe caches for parallel compilation
- Snapshotting support for LSP incremental validation
- Service-oriented architecture for centralized configuration

---

## Class Structure

### Main Class: `SemanticContext`

```csharp
public class SemanticContext
{
    // Core semantic data
    public SymbolTable SymbolTable { get; }
    public SemanticInfo SemanticInfo { get; }
    public TypeResolver TypeResolver { get; }

    // Shared caches
    public ClrMemberCache ClrCache { get; }

    // Diagnostics & Logging
    public DiagnosticBag Diagnostics { get; }
    public ICompilerLogger Logger { get; }

    // Configuration
    public bool ContinueAfterErrors { get; set; } = true;
    public int MaxErrors { get; set; } = 100;
    public string? CurrentFilePath { get; set; }

    // Optional service layer
    public CompilerServices? Services { get; }

    // AST traversal state (new pattern)
    public AstTraversalContext Traversal { get; }

    // Legacy state tracking (deprecated)
    [Obsolete] public TypeSymbol? CurrentClass { get; set; }
    [Obsolete] public FunctionSymbol? CurrentFunction { get; set; }
    [Obsolete] public bool InLoop { get; set; }
    [Obsolete] public int LoopDepth { get; set; }
}
```

**Key Design Decisions:**
- **Immutable core references**: `SymbolTable`, `SemanticInfo`, etc., are readonly to prevent accidental reassignment
- **Shared caches**: `ClrCache` is shared across validators to avoid duplicate CLR type reflection
- **Dual construction patterns**: Legacy constructor (direct dependencies) and modern constructor (`CompilerServices`)
- **Migration pattern**: Deprecated properties exist alongside new `Traversal` API

---

## Key Methods

### 1. Constructors

#### Legacy Constructor (Direct Dependencies)
```csharp
public SemanticContext(
    SymbolTable symbolTable,
    SemanticInfo semanticInfo,
    TypeResolver typeResolver,
    ICompilerLogger? logger = null)
```

**Purpose**: Create a context from individual components.

**Usage**: Existing code, tests, and components not yet migrated to `CompilerServices`.

**Implementation Details:**
- Creates a fresh `DiagnosticBag` (not shared)
- Creates a new `ClrMemberCache` instance
- Uses `NullLogger.Instance` if no logger provided

**Example:**
```csharp
var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
```

---

#### Modern Constructor (Service-Oriented)
```csharp
public SemanticContext(CompilerServices services)
```

**Purpose**: Create a context backed by centralized `CompilerServices`.

**Advantages:**
- Single source of truth for configuration
- Shared diagnostic bag across the entire compilation
- Shared `ClrMemberCache` for better performance
- Automatic configuration propagation (`ContinueAfterErrors`, `MaxErrors`)

**Implementation Details:**
- Extracts `TypeResolver` from `TypeResolverAdapter` (unwraps the adapter)
- Extracts `ClrMemberCache` from `ClrTypeMapperAdapter` (or creates new if unavailable)
- Shares the `DiagnosticBag` with `CompilerServices.DiagnosticReporter`

**Example:**
```csharp
var context = new SemanticContext(services);
```

**Error Handling:**
- Throws `ArgumentNullException` if `services` is null
- Throws `InvalidOperationException` if `TypeResolver` is not a `TypeResolverAdapter`

---

### 2. File-Scoped Context Creation

```csharp
public SemanticContext CreateForFile(string filePath)
```

**Purpose**: Create a **child context** for validating a single file in a multi-file project.

**Why This Exists:**
- In multi-file compilation, each file may need its own diagnostic collection
- However, you want to **share** the `SymbolTable`, `SemanticInfo`, and caches

**What Gets Shared:**
- `SymbolTable` (all files contribute to the same symbol table)
- `SemanticInfo` (annotations from all files are stored together)
- `TypeResolver` (same type resolution logic)
- `ClrCache` (avoid re-reflecting CLR types for each file)
- `Logger` (same logging infrastructure)

**What Gets Created Fresh:**
- `DiagnosticBag` (each file gets its own error list)
- File-specific configuration (`CurrentFilePath`)

**Example:**
```csharp
var projectContext = new SemanticContext(services);
var file1Context = projectContext.CreateForFile("module1.spy");
var file2Context = projectContext.CreateForFile("module2.spy");

// Each file has independent diagnostics, but shares symbols
```

---

### 3. Error Budget Management

```csharp
public bool ShouldContinue()
```

**Purpose**: Determine if validation should continue based on error count and configuration.

**Logic:**
1. If `ContinueAfterErrors == false` and any errors exist → **stop**
2. If error count ≥ `MaxErrors` → **stop**
3. Otherwise → **continue**

**Usage Pattern:**
```csharp
// In a validator's Visit method:
if (!context.ShouldContinue())
    return; // Bail out early

// Continue validation...
```

**Why This Matters:**
- **Performance**: Avoid wasting time validating when there are already fatal errors
- **User experience**: Don't overwhelm users with 10,000 cascading errors
- **Configurable**: CI/CD pipelines might want "fail fast" (`ContinueAfterErrors = false`)

---

### 4. Legacy Error Migration

```csharp
public void MergeFromLegacyErrors(IEnumerable<SemanticError> errors)
```

**Purpose**: Bridge old validators (that return `List<SemanticError>`) to the new `DiagnosticBag` system.

**Algorithm:**
1. For each `SemanticError`:
   - Extract the error message
   - Strip the `"Semantic error at line X:"` prefix (if present)
   - Add to `DiagnosticBag` with line/column/file information

**Example Input:**
```csharp
"Semantic error at line 42: Cannot assign 'str' to 'int'"
```

**Transformed To:**
```csharp
"Cannot assign 'str' to 'int'"
```

**Why This Exists:**
- Gradual migration path from legacy validators (`LegacyValidatorAdapter` pattern)
- Allows old and new validators to coexist during the transition

---

## Dependencies

### Internal Sharpy Dependencies

1. **`SymbolTable`** (`src/Sharpy.Compiler/Semantic/SymbolTable.cs`)
   - Manages scopes and symbol lookup
   - Provides `Lookup()`, `Define()`, `EnterScope()`, `ExitScope()`

2. **`SemanticInfo`** (`src/Sharpy.Compiler/Semantic/SemanticInfo.cs`)
   - Annotates AST nodes with semantic information
   - Maps expressions → types, identifiers → symbols, calls → targets
   - **Immutable AST Pattern**: AST nodes don't change; `SemanticInfo` stores all metadata externally

3. **`TypeResolver`** (`src/Sharpy.Compiler/Semantic/TypeResolver.cs`)
   - Resolves type annotations to `SemanticType`
   - Handles generics, unions, type aliases, etc.

4. **`ClrMemberCache`** (`src/Sharpy.Compiler/Services/ClrMemberCache.cs`)
   - Caches .NET type reflection results
   - Avoids repeatedly calling `typeof(List<>).GetMethods()` for every list operation

5. **`DiagnosticBag`** (`src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs`)
   - Thread-safe collection of errors/warnings
   - Formats diagnostics as `file(line,col): error: message`

6. **`CompilerServices`** (`src/Sharpy.Compiler/Services/CompilerServices.cs`)
   - Central service container (dependency injection pattern)
   - Provides configuration, logging, diagnostics, type resolution, symbol lookup

7. **`AstTraversalContext`** (`src/Sharpy.Compiler/Semantic/Validation/AstTraversalContext.cs`)
   - Stack-based tracking of current class/function/loop
   - See [AstTraversalContext.md](AstTraversalContext.md) for details

### External Dependencies

- **System Libraries**: `System.Collections.Generic`, `System.Linq`
- **Diagnostics Framework**: Custom (not Microsoft.CodeAnalysis)
- **Logging Framework**: Custom `ICompilerLogger` abstraction

---

## Patterns and Design Decisions

### 1. **Service Locator Pattern**
`SemanticContext` acts as a lightweight service locator for validators:
- Validators receive `SemanticContext` as a parameter
- They access `context.SymbolTable`, `context.Diagnostics`, etc.

**Alternative Considered:**
- Pass each dependency individually → **rejected** (too many parameters)

---

### 2. **Shared Mutable State with Controlled Access**
- `DiagnosticBag`, `SymbolTable`, `SemanticInfo` are all mutable
- However, validators **don't modify symbols directly** (they report errors instead)
- `SemanticInfo` is the exception: validators **do write** type annotations

**Thread Safety:**
- `DiagnosticBag` is thread-safe (locks internally)
- `ClrMemberCache` is thread-safe (uses `ConcurrentDictionary` internally)
- Other components are **not thread-safe** yet (future work)

---

### 3. **Migration-Friendly Architecture**

The class supports two usage patterns simultaneously:

**Old Pattern (Deprecated):**
```csharp
context.CurrentClass = classSymbol;
// ... validate class body ...
context.CurrentClass = null;
```

**New Pattern (Recommended):**
```csharp
using (context.Traversal.EnterClass(classSymbol))
{
    // ... validate class body ...
}
// Automatically restored via IDisposable
```

**Why Both Exist:**
- Large codebase with many validators
- Gradual migration reduces risk
- `[Obsolete]` attributes guide developers to new pattern

---

### 4. **Factory Method Pattern**
`CreateForFile()` is a factory method that clones context for multi-file scenarios:
- Avoids exposing complex construction logic
- Ensures consistency (always shares the right components)

---

### 5. **Fail-Fast Configuration**
`ShouldContinue()` implements configurable error budgets:
- **Development**: `ContinueAfterErrors = true`, `MaxErrors = 100` (see more errors)
- **CI/CD**: `ContinueAfterErrors = false` (fail immediately on first error)

---

## AST Traversal State Management

### The Problem
Validators need to know:
- "Am I inside a class?" (for validating `self` usage)
- "Am I inside a function?" (for validating `return` statements)
- "Am I inside a loop?" (for validating `break`/`continue`)

### The Solution: `AstTraversalContext`

**New Pattern (Stack-Based, Automatic Cleanup):**
```csharp
public void VisitClassDefinition(ClassDefinition node)
{
    var symbol = context.SymbolTable.LookupType(node.Name);

    using (context.Traversal.EnterClass(symbol))
    {
        // context.Traversal.CurrentClass is now set
        foreach (var member in node.Members)
            Visit(member);
    }
    // context.Traversal.CurrentClass is automatically restored
}
```

**Benefits:**
- **Exception-safe**: Even if validation throws, the stack is cleaned up
- **No manual cleanup**: No risk of forgetting to unset state
- **Nesting support**: Handles nested classes/functions correctly

**See Also:** [AstTraversalContext.md](AstTraversalContext.md) for detailed usage examples.

---

## Debugging Tips

### 1. **Check Diagnostic Count Early**
```csharp
// At the start of a validator:
var initialErrorCount = context.Diagnostics.ErrorCount;

// ... perform validation ...

// At the end:
if (context.Diagnostics.ErrorCount > initialErrorCount)
{
    context.Logger.LogDebug($"Validator added {context.Diagnostics.ErrorCount - initialErrorCount} errors");
}
```

---

### 2. **Inspect Symbol Table State**
```csharp
// In a debugger, evaluate:
context.SymbolTable.CurrentScope.GetAllSymbols()

// To see what's currently in scope
```

---

### 3. **Trace File Context**
```csharp
// If errors are missing file paths:
if (context.CurrentFilePath == null)
    throw new InvalidOperationException("CurrentFilePath not set!");
```

---

### 4. **Check Cache Effectiveness**
```csharp
// After compilation:
var cacheStats = context.ClrCache.GetStatistics();
context.Logger.LogInfo($"CLR cache hits: {cacheStats.Hits}, misses: {cacheStats.Misses}");
```

---

### 5. **Validate Traversal State**
```csharp
// Common mistake: forgetting to enter a scope
if (context.Traversal.CurrentFunction == null)
{
    context.Diagnostics.AddError("Internal error: no current function", node.Line);
    return;
}
```

---

## Contribution Guidelines

### When to Modify This File

1. **Adding new shared state** (e.g., "current module" for import resolution)
   - Add a property to `AstTraversalContext` (preferred)
   - Or add a property directly to `SemanticContext` (if not scope-based)

2. **Adding new caches** (e.g., caching import resolution results)
   - Add a property like `ClrCache`
   - Make it thread-safe (`ConcurrentDictionary` or manual locking)

3. **Adding new configuration** (e.g., "strict null checking mode")
   - Add to `CompilerServicesConfiguration` (see `src/Sharpy.Compiler/Services/CompilerServicesConfiguration.cs`)
   - Propagate in `SemanticContext(CompilerServices services)` constructor

4. **Migrating legacy state to `Traversal`**
   - Mark old property as `[Obsolete]`
   - Update validators to use `Traversal.EnterX()` pattern
   - Remove obsolete property in next major version

---

### Code Style Guidelines

1. **Use readonly for immutable state**
   ```csharp
   public SymbolTable SymbolTable { get; }  // Good
   public SymbolTable SymbolTable { get; set; }  // Bad
   ```

2. **Provide default values for configuration**
   ```csharp
   public bool ContinueAfterErrors { get; set; } = true;  // Good
   ```

3. **Null safety**
   ```csharp
   Logger = logger ?? NullLogger.Instance;  // Good: never null
   ```

4. **Factory methods return new instances**
   ```csharp
   public SemanticContext CreateForFile(string filePath)
   {
       return new SemanticContext(...);  // Good: new instance
   }
   ```

---

### Testing Considerations

**Unit Tests:**
- Test `ShouldContinue()` with various error counts and configurations
- Test `CreateForFile()` ensures proper sharing/isolation
- Test `MergeFromLegacyErrors()` message parsing

**Integration Tests:**
- Test that validators can share `SemanticContext` without conflicts
- Test multi-file compilation with separate diagnostic bags per file

**Performance Tests:**
- Measure cache hit rates (`ClrCache`)
- Ensure no memory leaks from `SemanticInfo` (AST nodes should be GC'd after compilation)

---

## Cross-References

### Related Files

1. **[AstTraversalContext.md](AstTraversalContext.md)** — Stack-based scope tracking (replacement for `CurrentClass`, `CurrentFunction`, etc.)
2. **[ISemanticValidator.md](ISemanticValidator.md)** — Interface that all validators implement
3. **[LegacyValidatorAdapter.md](LegacyValidatorAdapter.md)** — Adapter for old validators using this context

### Related Components

- **SymbolTable** (`src/Sharpy.Compiler/Semantic/SymbolTable.cs`) — Symbol and scope management
- **SemanticInfo** (`src/Sharpy.Compiler/Semantic/SemanticInfo.cs`) — AST annotation storage
- **TypeResolver** (`src/Sharpy.Compiler/Semantic/TypeResolver.cs`) — Type resolution logic
- **CompilerServices** (`src/Sharpy.Compiler/Services/CompilerServices.cs`) — Service container
- **DiagnosticBag** (`src/Sharpy.Compiler/Diagnostics/DiagnosticBag.cs`) — Diagnostic collection

### Validator Examples Using This Context

- **[ControlFlowValidatorV2.md](ControlFlowValidatorV2.md)** — Uses `Traversal.InLoop` to validate `break`/`continue`
- **[AccessValidatorV2.md](AccessValidatorV2.md)** — Uses `Traversal.CurrentClass` to validate `self` usage
- **[DefaultParameterValidatorV2.md](DefaultParameterValidatorV2.md)** — Uses `TypeResolver` to check default parameter types
- **[ProtocolValidatorV2.md](ProtocolValidatorV2.md)** — Uses `SymbolTable` to validate protocol implementations
- **[OperatorValidatorV2.md](OperatorValidatorV2.md)** — Uses `ClrCache` to validate operator overloads

---

## Summary

`SemanticContext` is the **central nervous system** of semantic validation:
- Provides **shared infrastructure** (symbols, types, diagnostics, caches)
- Supports **multi-file compilation** with proper state isolation
- Enables **gradual migration** from legacy patterns to modern patterns
- Designed for **future scalability** (LSP, parallel compilation, incremental validation)

**Key Takeaway:** When writing a new validator, inject `SemanticContext` and use it to access all compiler services. Prefer the `Traversal` API over direct state manipulation for scope tracking.
