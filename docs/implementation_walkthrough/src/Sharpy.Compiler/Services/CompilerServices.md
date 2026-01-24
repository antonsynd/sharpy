# Walkthrough: CompilerServices.cs

**Source File**: `src/Sharpy.Compiler/Services/CompilerServices.cs`

---

## Overview

`CompilerServices` is the **central service container** used throughout the Sharpy compiler. It provides a unified façade for accessing core compiler infrastructure like logging, diagnostics, type resolution, symbol lookup, and CLR type mapping. Think of it as a "one-stop shop" that compiler components (Parser, TypeChecker, RoslynEmitter, etc.) can pass around to access shared services.

**Role in the Compiler Pipeline:**
- **Initialized**: Early in compilation, built via `CompilerServicesBuilder`
- **Used by**: TypeChecker, RoslynEmitter, ValidationPipeline, and other semantic/codegen components
- **Provides**: Thread-safe, lazily-initialized access to compiler services with immutable configuration
- **Lifetime**: Lives for the duration of a single compilation unit

**Key Responsibilities:**
1. **Service access**: Exposes interfaces for type resolution, symbol lookup, CLR mapping, diagnostics
2. **Convenience wrappers**: Provides shorthand methods for common operations
3. **Configuration management**: Holds immutable compiler configuration
4. **Error tracking**: Centralizes diagnostic reporting and error count limits
5. **Context tracking**: Maintains current file path for error messages

**Design Philosophy:**
- **Thread-safe** for future parallel compilation support
- **Lazily initialized** where possible to minimize startup cost
- **Immutable configuration** after construction to prevent mid-compilation state changes
- **Abstraction barrier** between high-level services and low-level infrastructure

---

## Class Structure

### Main Class: `CompilerServices`

```csharp
public class CompilerServices
{
    // Configuration (immutable after construction)
    private readonly CompilerServicesConfiguration _config;

    // Core Services (exposed as interfaces)
    public ICompilerLogger Logger { get; }
    public IDiagnosticReporter DiagnosticReporter { get; }
    public ITypeResolver TypeResolver { get; }
    public ISymbolLookup SymbolLookup { get; }
    public IClrTypeMapper ClrMapper { get; }

    // Legacy Infrastructure (for migration compatibility)
    public SymbolTable SymbolTable { get; }
    public SemanticInfo SemanticInfo { get; }

    // Context tracking
    public string? CurrentFilePath { get; set; }
}
```

**Why interfaces?** The service properties expose **interfaces** (`ITypeResolver`, `ISymbolLookup`, etc.) rather than concrete types. This provides:
- **Testability**: Easy to mock services in unit tests
- **Flexibility**: Can swap implementations (e.g., caching vs. non-caching type resolver)
- **Decoupling**: Consumers depend on contracts, not concrete types

**Why expose SymbolTable/SemanticInfo directly?** These are the **underlying infrastructure** that components need during migration from legacy code. As the codebase modernizes, components should migrate to using the interface-based services instead.

---

## Key Services

### 1. Type Resolution (`ITypeResolver`)

**What it does:** Resolves `TypeAnnotation` AST nodes into concrete `SemanticType` instances.

**Example:**
```csharp
// AST has: def foo(x: int) -> str:
var annotation = functionDef.ReturnType;  // TypeAnnotation for "str"
var semanticType = services.ResolveType(annotation);  // SemanticType.Str
```

**Backed by:** `TypeResolverAdapter` wraps the legacy `TypeResolver` class.

**Used for:** Function signatures, variable declarations, cast expressions, generic instantiations.

---

### 2. Symbol Lookup (`ISymbolLookup`)

**What it does:** Looks up symbols (variables, functions, classes) by name in the current scope chain.

**Example:**
```csharp
// In: print(x)
var symbol = services.LookupSymbol("x");  // Returns VariableSymbol for 'x'
var symbol2 = services.LookupSymbol("print");  // Returns FunctionSymbol for 'print'
```

**Backed by:** `SymbolLookupAdapter` wraps the legacy `SymbolTable` class.

**Used for:** Identifier resolution, name binding, semantic analysis.

---

### 3. CLR Type Mapping (`IClrTypeMapper`)

**What it does:** Maps between Sharpy's `SemanticType` and .NET's `Type` system.

**Example:**
```csharp
// Sharpy type -> CLR type
var clrType = services.ClrMapper.MapType(SemanticType.Int);  // System.Int32

// Check if a CLR type supports protocols
var supportsList = services.ClrMapper.HasProtocol(clrType, "list");
```

**Backed by:** `ClrTypeMapperAdapter` wraps `ClrMemberCache`.

**Used for:** Code generation, CLR interop, protocol validation.

---

### 4. Diagnostic Reporting (`IDiagnosticReporter`)

**What it does:** Collects compilation errors and warnings with location information.

**Example:**
```csharp
// Report an error with AST location
services.ReportError("Type mismatch: expected 'int', got 'str'", expression);

// Report an error with line/column
services.ReportError("Syntax error", line: 42, column: 10);

// Check if compilation should stop
if (!services.ShouldContinue())
    return;  // Too many errors
```

**Backed by:** `DiagnosticReporter` class.

**Used for:** Error reporting, compilation termination logic.

---

### 5. Logging (`ICompilerLogger`)

**What it does:** Provides debug/info/warning logging for compiler internals.

**Example:**
```csharp
services.Logger.Debug("Resolving type annotation: {0}", annotation);
services.Logger.Info("Compilation completed with {0} errors", errorCount);
```

**Backed by:** Configurable logger implementation (can be `NullLogger` for tests).

**Used for:** Debugging, verbose output, compilation statistics.

---

## Construction: Builder Pattern

`CompilerServices` uses a **builder pattern** via `CompilerServicesBuilder` to ensure proper initialization.

### Why a builder?

1. **Complex dependencies**: The service requires 7+ dependencies; builder makes this manageable
2. **Required vs. optional**: Builder enforces that `SymbolTable` and `SemanticInfo` are provided
3. **Default fallbacks**: Builder creates default logger, CLR cache, TypeResolver if not provided
4. **Fluent API**: Readable construction: `new CompilerServicesBuilder().WithLogger(...).Build()`

### Typical construction flow:

```csharp
// In ProjectCompiler, TypeChecker, etc.
var services = new CompilerServicesBuilder()
    .WithConfiguration(config)
    .WithLogger(logger)
    .WithSymbolTable(symbolTable)
    .WithSemanticInfo(semanticInfo)
    .Build();  // Creates adapters, validates required components
```

### Test construction:

```csharp
// For unit tests
var services = CompilerServicesBuilder.CreateForTesting(logger);
// Creates fresh SymbolTable + SemanticInfo automatically
```

**See:** `src/Sharpy.Compiler/Services/CompilerServicesBuilder.cs:77` for the full `Build()` logic.

---

## Key Methods

### `ResolveType(TypeAnnotation?)`

**Purpose:** Convenience wrapper for type resolution.

```csharp
public SemanticType ResolveType(TypeAnnotation? annotation)
{
    return TypeResolver.ResolveTypeAnnotation(annotation);
}
```

**When to use:** Anytime you have an AST `TypeAnnotation` node and need the semantic type.

**Implementation note:** This is a thin wrapper. For performance-critical paths, you can call `TypeResolver` directly.

---

### `LookupSymbol(string)`

**Purpose:** Find a symbol by name in the current scope.

```csharp
public Symbol? LookupSymbol(string name)
{
    return SymbolLookup.Lookup(name);
}
```

**Returns:** `Symbol` (base class for `VariableSymbol`, `FunctionSymbol`, `TypeSymbol`, etc.) or `null` if not found.

**When to use:** Identifier resolution, name binding, validating references.

---

### `CanAssign(SemanticType from, SemanticType to)`

**Purpose:** Check if type `from` can be assigned to type `to`.

**Algorithm:**
1. **Null propagation**: If either type is `Unknown`, allow (for error recovery)
2. **Exact match**: If types are equal, allow
3. **Nullable assignment**: `T` can assign to `T?`
4. **None to nullable**: `None` (void) can assign to any `T?`
5. **Inheritance**: Check if `from` is a subtype of `to` (recursive)
6. **Numeric widening**: `int` → `long`, `float32` → `float64`, `int` → `float64`

**Example:**
```csharp
services.CanAssign(SemanticType.Int, SemanticType.Long);  // true (widening)
services.CanAssign(SemanticType.Str, SemanticType.Int);   // false
services.CanAssign(derivedType, baseType);                // true (inheritance)
```

**Used by:** Assignment validation, argument checking, return type validation.

**Implementation detail (lines 103-137):**
- Uses `IsSubtypeOf()` for inheritance checks (supports base classes + interfaces)
- Uses `IsNumericWidening()` for implicit numeric conversions
- Handles nullable types specially (`NullableType.UnderlyingType`)

---

### `ReportError(string, Node)` / `ReportError(string, int?, int?)`

**Purpose:** Report compilation errors with location information.

**Overload 1:** Takes an AST `Node` for automatic location extraction
```csharp
services.ReportError("Undefined variable: x", identifierNode);
```

**Overload 2:** Takes explicit line/column numbers
```csharp
services.ReportError("Parse error", line: 42, column: 10);
```

**Behind the scenes:** Delegates to `DiagnosticReporter`, which:
1. Increments error count
2. Associates error with `CurrentFilePath`
3. Stores in `Diagnostics` collection for later retrieval

---

### `ShouldContinue()`

**Purpose:** Check if compilation should continue based on error count.

```csharp
public bool ShouldContinue()
{
    if (!_config.ContinueAfterErrors && DiagnosticReporter.HasErrors)
        return false;
    if (DiagnosticReporter.Diagnostics.ErrorCount >= _config.MaxErrors)
        return false;
    return true;
}
```

**Typical usage pattern:**
```csharp
foreach (var module in modules)
{
    TypeCheckModule(module);
    if (!services.ShouldContinue())
        break;  // Stop processing more modules
}
```

**Configuration:**
- `ContinueAfterErrors`: If `false`, stop on first error
- `MaxErrors`: Hard limit (default 100) to prevent infinite error cascades

---

### `GetSemanticErrors()`

**Purpose:** Retrieve all diagnostics as legacy `SemanticError` objects.

```csharp
public IReadOnlyList<SemanticError> GetSemanticErrors()
{
    return DiagnosticReporter.Diagnostics.ToSemanticErrors();
}
```

**Why "legacy"?** This bridges the old `List<SemanticError>` pattern with the new `DiagnosticReporter` system. New code should use `DiagnosticReporter.Diagnostics` directly.

---

## Private Helper Methods

### `IsSubtypeOf(TypeSymbol?, TypeSymbol?)`

**Purpose:** Recursive inheritance check.

**Algorithm:**
1. Base case: If types are equal, return `true`
2. Recursive case 1: Check direct base type
3. Recursive case 2: Check implemented interfaces

**Example:**
```csharp
// class Dog(Animal): ...
IsSubtypeOf(dogSymbol, animalSymbol);  // true

// class Dog(Animal), Comparable: ...
IsSubtypeOf(dogSymbol, comparableSymbol);  // true (interface check)
```

**Used by:** `CanAssign()` for inheritance-based assignment compatibility.

**Implementation (lines 180-199):** Uses recursion on `TypeSymbol.BaseType` and `TypeSymbol.Interfaces`.

---

### `IsNumericWidening(SemanticType, SemanticType)`

**Purpose:** Check if numeric type `from` can widen to type `to`.

**Supported widenings:**
- `int` → `long`
- `float32` → `float64` / `double`
- `int` → `float64` / `double`

**Why not all conversions?** Only **widening** (no precision loss) is allowed implicitly. Narrowing (e.g., `long` → `int`) requires explicit cast.

**Used by:** `CanAssign()` for implicit numeric conversions.

---

## Configuration (`CompilerServicesConfiguration`)

The configuration is **immutable** after construction (C# `init` properties).

**Key settings:**
- `MaxErrors` (default: 100): Stop compilation after this many errors
- `ContinueAfterErrors` (default: `true`): Collect all errors vs. fail-fast
- `VerboseLogging` (default: `false`): Enable debug logging
- `InitialFilePath`: Set `CurrentFilePath` at construction time

**Example:**
```csharp
var config = new CompilerServicesConfiguration
{
    MaxErrors = 50,
    ContinueAfterErrors = false,  // Stop on first error
    VerboseLogging = true
};

var services = new CompilerServicesBuilder()
    .WithConfiguration(config)
    .Build();
```

**See:** `src/Sharpy.Compiler/Services/CompilerServicesConfiguration.cs` for full schema.

---

## Patterns and Design Decisions

### 1. **Dependency Injection via Constructor**

All dependencies are injected via the `internal` constructor. This makes testing easy (inject mocks) and ensures services are always fully initialized.

**Why `internal`?** Forces consumers to use `CompilerServicesBuilder`, which enforces required dependencies and creates adapters.

---

### 2. **Adapter Pattern**

The service interfaces (`ITypeResolver`, `ISymbolLookup`, etc.) are implemented as **adapters** that wrap legacy components:

- `TypeResolverAdapter` → wraps `TypeResolver`
- `SymbolLookupAdapter` → wraps `SymbolTable`
- `ClrTypeMapperAdapter` → wraps `ClrMemberCache`

**Why?** This allows gradual migration from legacy APIs to a cleaner service-oriented architecture without breaking existing code.

---

### 3. **Façade Pattern**

`CompilerServices` is a **façade** that provides a unified interface to multiple subsystems (logging, diagnostics, type resolution, etc.). This simplifies dependency management: pass one `CompilerServices` object instead of 7+ individual services.

---

### 4. **Thread Safety Preparation**

The class is designed to be thread-safe for future parallel compilation:
- Configuration is **immutable**
- Services are **stateless** or manage their own synchronization
- `CurrentFilePath` is the only mutable state (could use `ThreadLocal<string>` in the future)

---

### 5. **Error Recovery Strategy**

The `CanAssign()` method allows `Unknown` types to "match" any type. This is **intentional error recovery**: if type resolution fails (e.g., undefined type), we mark it as `Unknown` and allow the compiler to continue checking the rest of the file, rather than cascading errors.

---

## Debugging Tips

### 1. **Tracing type assignment failures**

Set a breakpoint in `CanAssign()` at line 103 and inspect:
- `from.ToString()` / `to.ToString()` for type names
- `IsSubtypeOf()` calls for inheritance chains
- `IsNumericWidening()` for implicit conversions

**Common issue:** Forgetting that `None` (Python's `None`) maps to `SemanticType.Void`, which can assign to `T?` but not `T`.

---

### 2. **Understanding error counts**

If compilation stops early, check:
- `services.ShouldContinue()` return value
- `_config.MaxErrors` setting
- `DiagnosticReporter.Diagnostics.ErrorCount` actual count

**Common issue:** Test has `MaxErrors = 10` but generates 15 errors; only first 10 are reported.

---

### 3. **Inspecting service initialization**

Set a breakpoint in `CompilerServicesBuilder.Build()` at line 96 to see:
- Which services are provided vs. auto-created
- Default vs. custom logger
- Configuration values

**Common issue:** Forgetting to call `.WithSymbolTable()` causes `InvalidOperationException` at build time.

---

### 4. **Logging service operations**

Enable verbose logging:
```csharp
var config = new CompilerServicesConfiguration { VerboseLogging = true };
```

The logger will output debug information for type resolution, symbol lookups, etc.

---

## Contribution Guidelines

### When to modify `CompilerServices`:

1. **Adding a new service interface**: Add property + update builder + create adapter
   - Example: Adding `IModuleResolver` for import resolution
2. **Adding convenience methods**: For commonly repeated operations across the codebase
   - Example: Adding `IsExactMatch(SemanticType, SemanticType)` if used in 5+ places
3. **Updating type compatibility rules**: Modify `CanAssign()`, `IsSubtypeOf()`, `IsNumericWidening()`
   - Example: Adding `float64` → `decimal` widening
4. **Changing configuration options**: Update `CompilerServicesConfiguration`
   - Example: Adding `WarningsAsErrors` flag

### When NOT to modify `CompilerServices`:

1. **Component-specific logic**: Put in the component itself (e.g., TypeChecker, RoslynEmitter)
2. **One-off utilities**: Create a separate helper class
3. **Stateful operations**: Services should be stateless; state goes in `SymbolTable`, `SemanticInfo`, etc.

---

## Testing

### Unit testing with `CompilerServices`:

```csharp
[Fact]
public void CanAssign_IntToLong_ReturnsTrue()
{
    var services = CompilerServicesBuilder.CreateForTesting();
    var result = services.CanAssign(SemanticType.Int, SemanticType.Long);
    Assert.True(result);
}
```

### Mocking services for integration tests:

```csharp
var mockTypeResolver = new Mock<ITypeResolver>();
mockTypeResolver.Setup(r => r.ResolveTypeAnnotation(It.IsAny<TypeAnnotation>()))
                .Returns(SemanticType.Int);

var services = new CompilerServicesBuilder()
    .WithTypeResolver(mockTypeResolver.Object)
    .Build();
```

**See:** `src/Sharpy.Compiler.Tests/` for examples.

---

## Cross-References

### Related Service Files:
- `CompilerServicesBuilder.cs` - Builder pattern for constructing CompilerServices
- `CompilerServicesConfiguration.cs` - Immutable configuration schema
- `DiagnosticReporter.cs` - Error/warning collection and reporting
- `ITypeResolver.cs` / `TypeResolverAdapter.cs` - Type annotation resolution
- `ISymbolLookup.cs` / `SymbolLookupAdapter.cs` - Symbol table queries
- `IClrTypeMapper.cs` / `ClrTypeMapperAdapter.cs` - CLR interop mapping

### Consumers of CompilerServices:
- `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/TypeChecker.md` - Uses for type checking
- `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/TypeResolver.md` - Wrapped by ITypeResolver
- `docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/RoslynEmitter.md` - Uses for code generation

### Infrastructure:
- `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/SemanticInfo.md` - AST annotation storage
- SymbolTable (not yet documented) - Symbol storage and lookup

---

## Summary

`CompilerServices` is the **"Swiss Army knife"** of the Sharpy compiler:

✅ **Centralized access** to all compiler services
✅ **Convenient wrappers** for common operations
✅ **Error tracking** and compilation termination logic
✅ **Type compatibility** checking (assignment, inheritance, widening)
✅ **Thread-safe design** for future parallelization
✅ **Builder pattern** for clean, validated construction
✅ **Adapter pattern** for gradual legacy migration

When in doubt, if you need to access logging, diagnostics, type resolution, or symbol lookup—reach for `CompilerServices`. It's designed to be the single dependency that unlocks the entire compiler infrastructure.
