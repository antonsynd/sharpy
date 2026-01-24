# Walkthrough: CompilerServicesBuilder.cs

**Source File**: `src/Sharpy.Compiler/Services/CompilerServicesBuilder.cs`

---

## Overview

`CompilerServicesBuilder` is a **fluent builder pattern** implementation that constructs properly initialized `CompilerServices` instances. It acts as the central assembly point for the compiler's dependency injection system, ensuring all required components are available before compilation begins.

**Role in the Pipeline**: This builder sits at the **initialization stage** before the pipeline starts. It wires together:
- Logging infrastructure
- Symbol tables and semantic information
- Type resolution services
- CLR interop caches
- Diagnostic reporting

Think of it as the "composition root" that prevents invalid compiler states by enforcing required dependencies at build time.

---

## Class Structure

### Main Class: `CompilerServicesBuilder`

A mutable builder class that accumulates configuration and dependencies, then produces an immutable `CompilerServices` instance.

```csharp
public class CompilerServicesBuilder
{
    // Configuration (has defaults)
    private CompilerServicesConfiguration _config = CompilerServicesConfiguration.Default;

    // Optional dependency
    private ICompilerLogger? _logger;

    // Required dependencies (enforced in Build())
    private SymbolTable? _symbolTable;
    private SemanticInfo? _semanticInfo;

    // Optional dependencies (auto-created if missing)
    private TypeResolver? _typeResolver;
    private ClrMemberCache? _clrCache;
}
```

**Key Design Decision**: The builder distinguishes between:
1. **Required dependencies** (`SymbolTable`, `SemanticInfo`) - must be provided or `Build()` throws
2. **Optional dependencies** (`ICompilerLogger`, `TypeResolver`, `ClrMemberCache`) - have sensible defaults
3. **Configuration** (`CompilerServicesConfiguration`) - defaults to `CompilerServicesConfiguration.Default`

---

## Key Methods

### Configuration Methods (Fluent API)

All configuration methods follow the same pattern:
1. Accept a parameter
2. Store it in a private field
3. Return `this` for method chaining
4. Validate non-null (for required deps)

#### `WithConfiguration(CompilerServicesConfiguration config)`

```csharp
public CompilerServicesBuilder WithConfiguration(CompilerServicesConfiguration config)
{
    _config = config ?? throw new ArgumentNullException(nameof(config));
    return this;
}
```

Sets compiler behavior flags:
- `MaxErrors` - error threshold before stopping
- `ContinueAfterErrors` - whether to collect all errors
- `VerboseLogging` - diagnostic verbosity
- `InitialFilePath` - starting file for error context

**When to use**: Override defaults when running tests (lower `MaxErrors`) or during IDE integration (disable `ContinueAfterErrors` for faster failure).

#### `WithLogger(ICompilerLogger? logger)`

Sets the logging implementation. Nullable because `Build()` defaults to `NullLogger.Instance` (a no-op logger).

**When to use**: Wire up console logging for CLI, or custom loggers for IDE integration.

#### `WithSymbolTable(SymbolTable symbolTable)` ⚠️ Required

```csharp
public CompilerServicesBuilder WithSymbolTable(SymbolTable symbolTable)
{
    _symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
    return this;
}
```

Provides the symbol table that tracks all declared types, functions, and variables.

**Critical**: Must be called before `Build()`, or you'll get an `InvalidOperationException`.

#### `WithSemanticInfo(SemanticInfo semanticInfo)` ⚠️ Required

Provides the semantic annotation container that stores type information for AST nodes.

**Critical**: Also required before `Build()`.

#### `WithTypeResolver(TypeResolver typeResolver)` (Optional)

```csharp
public CompilerServicesBuilder WithTypeResolver(TypeResolver typeResolver)
{
    _typeResolver = typeResolver;
    return this;
}
```

Provide a pre-configured `TypeResolver`. If not set, `Build()` will create one automatically:

```csharp
var typeResolver = _typeResolver ?? new TypeResolver(_symbolTable, _semanticInfo, logger);
```

**When to use**: When you need fine-grained control over type resolution, or when sharing a `TypeResolver` across multiple compilation units.

#### `WithClrCache(ClrMemberCache clrCache)` (Optional)

Provide a pre-warmed CLR member cache. If not set, `Build()` creates a fresh one.

**When to use**: When compiling multiple files in succession, reusing the cache improves performance by avoiding repeated reflection.

---

### Building the Services

#### `Build()` - The Assembly Point

This is where the builder validates requirements and constructs the final `CompilerServices` instance.

**Step-by-step breakdown**:

```csharp
public CompilerServices Build()
{
    // 1. VALIDATION - Enforce required dependencies
    if (_symbolTable == null)
        throw new InvalidOperationException("SymbolTable is required. Call WithSymbolTable() before Build().");
    if (_semanticInfo == null)
        throw new InvalidOperationException("SemanticInfo is required. Call WithSemanticInfo() before Build().");

    // 2. DEFAULTS - Create missing optional components
    var logger = _logger ?? NullLogger.Instance;
    var clrCache = _clrCache ?? new ClrMemberCache();
    var typeResolver = _typeResolver ?? new TypeResolver(_symbolTable, _semanticInfo, logger);

    // 3. ADAPTERS - Wrap concrete types in interface adapters
    var typeResolverAdapter = new TypeResolverAdapter(typeResolver);
    var symbolLookupAdapter = new SymbolLookupAdapter(_symbolTable);
    var clrMapperAdapter = new ClrTypeMapperAdapter(clrCache);
    var diagnosticReporter = new DiagnosticReporter(logger);

    // 4. CONSTRUCTION - Assemble the final services object
    return new CompilerServices(
        _config,
        logger,
        _symbolTable,
        _semanticInfo,
        typeResolverAdapter,
        symbolLookupAdapter,
        clrMapperAdapter,
        diagnosticReporter);
}
```

**Why adapters?** The builder creates adapter objects (`TypeResolverAdapter`, `SymbolLookupAdapter`, etc.) to wrap concrete types in interface contracts. This enables:
- **Migration flexibility**: Legacy code can access `.UnderlyingResolver` while new code uses the interface
- **Testability**: Interfaces are easier to mock
- **Encapsulation**: Interfaces expose only what's needed for each concern

---

### Testing Support

#### `CreateForTesting(ICompilerLogger? logger = null)` - Static Factory

```csharp
public static CompilerServices CreateForTesting(ICompilerLogger? logger = null)
{
    var builtinRegistry = new BuiltinRegistry();
    var symbolTable = new SymbolTable(builtinRegistry);
    var semanticInfo = new SemanticInfo();

    return new CompilerServicesBuilder()
        .WithLogger(logger)
        .WithSymbolTable(symbolTable)
        .WithSemanticInfo(semanticInfo)
        .Build();
}
```

**Purpose**: Provides a one-liner to create a minimal, working `CompilerServices` for unit tests.

**What it initializes**:
- Fresh `SymbolTable` with built-in types (int, str, bool, etc.)
- Empty `SemanticInfo` ready to be populated
- Optional logger (defaults to `NullLogger`)

**When to use**:
- Unit tests that need type checking or code generation
- Integration tests that don't require custom configuration

**Example usage**:
```csharp
var services = CompilerServicesBuilder.CreateForTesting();
var typeChecker = new TypeChecker(services);
// ... test type checking logic
```

---

## Dependencies

### Internal Dependencies

1. **`Sharpy.Compiler.Logging`**
   - `ICompilerLogger` interface
   - `NullLogger` (silent fallback)

2. **`Sharpy.Compiler.Semantic`**
   - `SymbolTable` - symbol storage
   - `SemanticInfo` - AST annotations
   - `TypeResolver` - type annotation resolution
   - `ClrMemberCache` - reflection cache
   - `BuiltinRegistry` - built-in type definitions

### Constructed Types

The builder creates instances of:
- `TypeResolverAdapter` - wraps `TypeResolver` in `ITypeResolver`
- `SymbolLookupAdapter` - wraps `SymbolTable` in `ISymbolLookup`
- `ClrTypeMapperAdapter` - wraps `ClrMemberCache` in `IClrTypeMapper`
- `DiagnosticReporter` - wraps `ICompilerLogger` in `IDiagnosticReporter`

---

## Patterns and Design Decisions

### 1. Fluent Builder Pattern

```csharp
var services = new CompilerServicesBuilder()
    .WithLogger(consoleLogger)
    .WithSymbolTable(symbolTable)
    .WithSemanticInfo(semanticInfo)
    .Build();
```

**Benefits**:
- Readable, self-documenting initialization
- Method chaining reduces boilerplate
- Impossible to forget required dependencies (enforced by `Build()`)

### 2. Fail-Fast Validation

The builder validates requirements **at build time**, not at first use:

```csharp
if (_symbolTable == null)
    throw new InvalidOperationException("SymbolTable is required...");
```

**Why this matters**: Catching configuration errors early prevents cryptic `NullReferenceException`s deep in the compilation pipeline.

### 3. Adapter Pattern for Migration

The builder creates adapter wrappers around concrete types:

```csharp
var typeResolverAdapter = new TypeResolverAdapter(typeResolver);
```

**Migration strategy**: This allows gradual refactoring:
- Old code calls `services.TypeResolver.UnderlyingResolver.LegacyMethod()`
- New code calls `services.TypeResolver.ResolveTypeAnnotation(annotation)`
- Eventually, all code migrates to interfaces and `.UnderlyingResolver` can be removed

### 4. Immutable Configuration

`CompilerServicesConfiguration` uses C# 9's `init` properties:

```csharp
public int MaxErrors { get; init; } = 100;
```

Once built, the configuration cannot be modified, preventing accidental state corruption during compilation.

### 5. Service Locator Alternative

Instead of a global service locator (an anti-pattern), `CompilerServices` is explicitly passed to components:

```csharp
var typeChecker = new TypeChecker(services);
var emitter = new RoslynEmitter(services);
```

This makes dependencies explicit and testable.

---

## Debugging Tips

### Problem: `InvalidOperationException` on `Build()`

**Symptom**:
```
InvalidOperationException: SymbolTable is required. Call WithSymbolTable() before Build().
```

**Solution**: Ensure you call both `.WithSymbolTable()` and `.WithSemanticInfo()` before `.Build()`:

```csharp
// ❌ WRONG - missing required dependencies
var services = new CompilerServicesBuilder().Build();

// ✅ CORRECT
var services = new CompilerServicesBuilder()
    .WithSymbolTable(symbolTable)
    .WithSemanticInfo(semanticInfo)
    .Build();
```

### Problem: Tests passing with wrong logger

**Symptom**: Tests are passing but you're not seeing expected log output.

**Cause**: The default logger is `NullLogger.Instance`, which discards all log messages.

**Solution**: Explicitly wire up a test logger:

```csharp
var testLogger = new TestLogger();
var services = CompilerServicesBuilder.CreateForTesting(testLogger);
// ... run test ...
Assert.Contains("Expected log message", testLogger.Messages);
```

### Problem: Performance degradation in multi-file compilation

**Symptom**: Compiling multiple files is slower than expected.

**Cause**: A new `ClrMemberCache` is created for each file, repeating expensive reflection.

**Solution**: Reuse the cache across files:

```csharp
var sharedCache = new ClrMemberCache();

foreach (var file in files)
{
    var services = new CompilerServicesBuilder()
        .WithClrCache(sharedCache)  // ← Reuse cache
        .WithSymbolTable(symbolTable)
        .WithSemanticInfo(semanticInfo)
        .Build();

    // ... compile file ...
}
```

### Debugging Adapter Issues

If you suspect an adapter is losing information, you can access the underlying concrete type during debugging:

```csharp
var adapter = services.TypeResolver as TypeResolverAdapter;
var underlying = adapter?.UnderlyingResolver;
// Set breakpoint here to inspect the concrete TypeResolver
```

**Note**: Avoid this in production code - it defeats the purpose of the interface abstraction.

---

## Contribution Guidelines

### When to Modify This File

1. **Adding new required dependencies**:
   - Add a private field (e.g., `private NewService? _newService;`)
   - Add a `WithNewService()` method
   - Add validation in `Build()` if required
   - Wire it into the `CompilerServices` constructor

2. **Adding new optional dependencies**:
   - Add a private field
   - Add a `WithNewService()` method
   - Provide a default in `Build()` (e.g., `_newService ?? new NewService()`)

3. **Changing initialization logic**:
   - Modify the `Build()` method
   - Update `CreateForTesting()` if the change affects test setup

### Code Style Guidelines

**Null checking**: Use the `?? throw` pattern for required parameters:

```csharp
_symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
```

**Return `this`**: All configuration methods must return `this` for fluent chaining:

```csharp
public CompilerServicesBuilder WithLogger(ICompilerLogger? logger)
{
    _logger = logger;
    return this;  // ← Don't forget!
}
```

**Order in `Build()`**: Follow the existing pattern:
1. Validate required dependencies
2. Create defaults for optional dependencies
3. Create adapters
4. Construct `CompilerServices`

### Testing Considerations

When adding new dependencies:
1. Add a test that verifies `Build()` throws if the dependency is required but missing
2. Add a test that verifies the default is used if the dependency is optional
3. Update `CreateForTesting()` if the dependency should be included in test setups

---

## Cross-References

### Related Files

- **[CompilerServices.md](./CompilerServices.md)** - The services container that this builder constructs
- **[CompilerServicesConfiguration.md](./CompilerServicesConfiguration.md)** - Configuration object details
- **[TypeResolverAdapter.md](./TypeResolverAdapter.md)** - Adapter pattern implementation for type resolution
- **[SymbolLookupAdapter.md](./SymbolLookupAdapter.md)** - Adapter for symbol table access
- **[DiagnosticReporter.md](./DiagnosticReporter.md)** - Error reporting infrastructure

### Upstream Dependencies (Inputs)

- **`SymbolTable`** (`src/Sharpy.Compiler/Semantic/SymbolTable.cs`) - Required
- **`SemanticInfo`** (`src/Sharpy.Compiler/Semantic/SemanticInfo.cs`) - Required
- **`TypeResolver`** (`src/Sharpy.Compiler/Semantic/TypeResolver.cs`) - Optional
- **`ICompilerLogger`** (`src/Sharpy.Compiler/Logging/ICompilerLogger.cs`) - Optional

### Downstream Usage (Outputs)

This builder is typically used in:
- **`ProjectCompiler`** (`src/Sharpy.Compiler/Project/ProjectCompiler.cs`) - Creates services for each compilation unit
- **Test fixtures** (`src/Sharpy.Compiler.Tests/**/*Tests.cs`) - Uses `CreateForTesting()`
- **CLI entry point** (`src/Sharpy.Cli/Program.cs`) - Wires up console logging

---

## Summary

`CompilerServicesBuilder` is the **gateway to the Sharpy compilation pipeline**. It enforces that all required infrastructure is available before any compilation work begins, preventing hard-to-debug null reference errors.

**Key takeaways**:
- **Fluent builder pattern** for readable initialization
- **Required vs. optional dependencies** enforced at build time
- **Adapter pattern** enables gradual migration to interface-based architecture
- **`CreateForTesting()` static factory** for minimal test setups
- **Immutable configuration** prevents runtime state corruption

When debugging compilation issues, verify that the builder is configured correctly - many seemingly mysterious errors trace back to missing or misconfigured services.
