# Walkthrough: TypeResolverAdapter.cs

**Source File**: `src/Sharpy.Compiler/Services/TypeResolverAdapter.cs`

---

## Overview

`TypeResolverAdapter` is a simple **adapter pattern** implementation that wraps the existing `TypeResolver` class to implement the `ITypeResolver` interface. Its primary purpose is to enable **gradual migration** to Sharpy's new services architecture without disrupting existing code.

**Role in the Compiler Pipeline:**
```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C#
                                             ↑
                                      TypeResolverAdapter
                                      (wraps TypeResolver)
```

This adapter sits in the **Semantic Analysis** phase, providing type resolution services to components like `TypeChecker`, `ValidationPipeline`, and other semantic analyzers.

## Class/Type Structure

### TypeResolverAdapter

```csharp
public class TypeResolverAdapter : ITypeResolver
{
    private readonly TypeResolver _typeResolver;

    // Constructor, methods...
}
```

**Key Characteristics:**
- **Implements**: `ITypeResolver` interface (defined in `src/Sharpy.Compiler/Services/ITypeResolver.cs`)
- **Wraps**: `TypeResolver` (defined in `src/Sharpy.Compiler/Semantic/TypeResolver.cs`)
- **Pattern**: Classic **Adapter Pattern** (also known as **Wrapper Pattern**)
- **Immutability**: Once constructed, the wrapped resolver cannot be changed

### Dependencies

```
TypeResolverAdapter (Services layer)
    └─ implements ITypeResolver
    └─ wraps TypeResolver (Semantic layer)
        └─ uses SymbolTable
        └─ uses SemanticInfo
        └─ uses ICompilerLogger
```

## Key Functions/Methods

### Constructor

```csharp
public TypeResolverAdapter(TypeResolver typeResolver)
{
    _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
}
```

**Purpose**: Initializes the adapter with an existing `TypeResolver` instance.

**Parameters:**
- `typeResolver`: The existing type resolver to wrap. Must not be null.

**Important Details:**
- **Null checking**: Throws `ArgumentNullException` if `typeResolver` is null
- **Dependency injection**: Expects a fully-configured `TypeResolver` to be passed in
- The wrapped resolver is typically created by `CompilerServicesBuilder`

**Example Usage:**
```csharp
var symbolTable = new SymbolTable();
var semanticInfo = new SemanticInfo();
var typeResolver = new TypeResolver(symbolTable, semanticInfo);
var adapter = new TypeResolverAdapter(typeResolver);  // Wrap it
```

---

### ResolveTypeAnnotation

```csharp
public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
{
    return _typeResolver.ResolveTypeAnnotation(annotation);
}
```

**Purpose**: Resolves a type annotation from the AST into a semantic type representation.

**Parameters:**
- `annotation`: A `TypeAnnotation` node from the parsed AST (can be null)

**Return Value:**
- `SemanticType`: The resolved semantic type (returns `SemanticType.Unknown` if null or unresolvable)

**What It Does:**
This is a **pure delegation** method—it simply forwards the call to the wrapped `TypeResolver`. The heavy lifting happens in `TypeResolver.ResolveTypeAnnotation` (src/Sharpy.Compiler/Semantic/TypeResolver.cs:25), which:

1. Returns `SemanticType.Unknown` for null annotations
2. Checks the cache in `SemanticInfo` first
3. Resolves builtin types (`int`, `str`, `bool`, etc.)
4. Expands type aliases
5. Resolves generic types (e.g., `list[int]`, `dict[str, int]`)
6. Handles type parameters (e.g., `T` in `class Box[T]`)
7. Looks up user-defined types
8. Applies nullable modifiers (e.g., `int?`)
9. Caches the result

**Connection to Other Components:**
- **Upstream**: Called by `TypeChecker`, `ValidationPipeline`, and other semantic analyzers
- **Downstream**: Delegates to `TypeResolver`, which accesses `SymbolTable` and `SemanticInfo`

**Example:**
```csharp
// In TypeChecker or similar component
var annotation = variable.TypeAnnotation;  // From AST
var semanticType = typeResolver.ResolveTypeAnnotation(annotation);

if (semanticType == SemanticType.Unknown)
{
    // Type couldn't be resolved - check errors
}
```

---

### Errors Property

```csharp
public IReadOnlyList<SemanticError> Errors => _typeResolver.Errors;
```

**Purpose**: Exposes errors that occurred during type resolution.

**Return Value:**
- `IReadOnlyList<SemanticError>`: A read-only collection of semantic errors

**Important Details:**
- **Read-only**: Returns `IReadOnlyList` to prevent external modification
- **Delegation**: Simply forwards to the wrapped resolver's error collection
- **Accumulation**: Errors accumulate over the lifetime of the resolver
- Errors include messages like `"Type 'Foo' not found"` or `"Type 'list' expects 1 type argument but got 2"`

**Usage Pattern:**
```csharp
var semanticType = typeResolver.ResolveTypeAnnotation(annotation);

if (typeResolver.Errors.Any())
{
    foreach (var error in typeResolver.Errors)
    {
        Console.WriteLine($"Error at {error.Line}:{error.Column} - {error.Message}");
    }
}
```

---

### UnderlyingResolver Property

```csharp
/// <summary>
/// Get the underlying TypeResolver for cases that need direct access.
/// Use sparingly - prefer the interface methods.
/// </summary>
public TypeResolver UnderlyingResolver => _typeResolver;
```

**Purpose**: Provides **escape hatch** access to the wrapped `TypeResolver` for legacy code.

**When to Use:**
- During gradual migration when code needs direct `TypeResolver` access
- When calling methods not yet exposed by `ITypeResolver`

**⚠️ Important:**
- The XML comment explicitly says **"Use sparingly"**
- This is a **temporary migration aid**, not part of the long-term design
- Prefer using the interface methods whenever possible

**Example (Migration Scenario):**
```csharp
// Legacy code that needs TypeResolver directly
void LegacyMethod(TypeResolver resolver) { /* ... */ }

// Using the adapter
var adapter = services.TypeResolver as TypeResolverAdapter;
if (adapter != null)
{
    LegacyMethod(adapter.UnderlyingResolver);  // Temporary workaround
}
```

## Dependencies

### Internal Dependencies

**Direct Dependencies:**
- `Sharpy.Compiler.Parser.Ast.TypeAnnotation` - AST node representing type annotations
- `Sharpy.Compiler.Semantic.TypeResolver` - The wrapped resolver
- `Sharpy.Compiler.Semantic.SemanticType` - Return type for resolved types
- `Sharpy.Compiler.Semantic.SemanticError` - Error reporting type

**Indirect Dependencies (via TypeResolver):**
- `SymbolTable` - For looking up types and symbols
- `SemanticInfo` - For caching resolved types
- `ICompilerLogger` - For logging errors

### Related Files

**Same Directory (Services Layer):**
- `ITypeResolver.cs` - Interface this adapter implements
- `CompilerServices.cs` - Container that holds this service
- `CompilerServicesBuilder.cs` - Builder that creates this adapter
- `SymbolLookupAdapter.cs` - Similar adapter pattern for symbol lookup
- `ClrTypeMapperAdapter.cs` - Similar adapter pattern for CLR type mapping
- `README.md` - Architecture documentation

**External Dependencies:**
- `src/Sharpy.Compiler/Semantic/TypeResolver.cs` - The wrapped class
- `src/Sharpy.Compiler/Semantic/SymbolTable.cs` - Symbol table used by resolver
- `src/Sharpy.Compiler/Semantic/SemanticInfo.cs` - Cache for semantic data

## Patterns and Design Decisions

### 1. Adapter Pattern

This is a textbook implementation of the **Adapter Pattern**:

```
Client Code
    ↓ (uses)
ITypeResolver (interface)
    ↓ (implemented by)
TypeResolverAdapter
    ↓ (wraps)
TypeResolver (legacy class)
```

**Why this pattern?**
- Allows new code to depend on `ITypeResolver` interface
- Enables testing with mock implementations
- Facilitates future changes (e.g., LSP server, parallel compilation)
- Preserves backwards compatibility with existing `TypeResolver`

### 2. Gradual Migration Strategy

The adapter enables **strangler fig pattern** migration:

1. **Phase 1** (Current): New code uses `ITypeResolver`, adapter wraps old `TypeResolver`
2. **Phase 2** (Future): Slowly migrate `TypeResolver` logic into new implementation
3. **Phase 3** (Final): Replace `TypeResolverAdapter` with direct `ITypeResolver` implementation

Evidence: The XML comment explicitly mentions **"gradual migration"** (line 8).

### 3. Dependency Injection

The constructor accepts `TypeResolver` as a parameter rather than creating it internally:

```csharp
public TypeResolverAdapter(TypeResolver typeResolver)  // ✅ Injected
{
    // vs.
    // _typeResolver = new TypeResolver(...);  // ❌ Not doing this
}
```

**Benefits:**
- Easier testing (can inject test doubles)
- Follows **Inversion of Control** principle
- Configuration handled by `CompilerServicesBuilder`

### 4. Read-Only Interface

The `ITypeResolver` interface is **read-only** (no mutation methods):

```csharp
public interface ITypeResolver
{
    SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation);  // Query
    IReadOnlyList<SemanticError> Errors { get; }  // Query
    // No SetX, AddX, ClearX methods
}
```

This makes the service:
- **Thread-safe** (future parallel compilation)
- **Easier to reason about** (no hidden state changes)
- **Cacheable** (results can be cached safely)

### 5. Minimal Surface Area

The adapter exposes only what's needed:
- 1 method: `ResolveTypeAnnotation`
- 1 property: `Errors`
- 1 escape hatch: `UnderlyingResolver` (temporary)

This **minimalism** reduces coupling and makes testing simpler.

## Debugging Tips

### 1. Type Resolution Failures

**Symptom:** Getting `SemanticType.Unknown` for valid types

**Debug Steps:**
```csharp
// Set breakpoint in TypeResolverAdapter.ResolveTypeAnnotation
public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
{
    // 👈 Breakpoint here
    return _typeResolver.ResolveTypeAnnotation(annotation);
}

// Then step into TypeResolver.ResolveTypeAnnotation (line 25)
// Check:
// - Is annotation null? (line 27-28)
// - Is it cached? (line 31-33)
// - What's the annotation.Name value?
// - Is it a builtin type? (line 46-49)
// - Does symbol table have it? (line 72)
```

**Common Issues:**
- Type not in symbol table (forgot to import/define it)
- Typo in type name (case-sensitive)
- Generic type missing type arguments
- Type alias not expanded properly

### 2. Inspecting Errors

**Symptom:** Compilation fails but unclear why

**Debug Approach:**
```csharp
// After type resolution
var errors = adapter.Errors;
foreach (var error in errors)
{
    Console.WriteLine($"{error.Line}:{error.Column} - {error.Message}");
}

// Or use DiagnosticReporter in CompilerServices
if (services.DiagnosticReporter.HasErrors)
{
    var diagnostics = services.DiagnosticReporter.Diagnostics;
    // Inspect diagnostics
}
```

### 3. Tracing Delegations

**Symptom:** Unsure if adapter is being used

**Add logging:**
```csharp
public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
{
    _logger.LogDebug($"TypeResolverAdapter: Resolving {annotation?.Name}");
    var result = _typeResolver.ResolveTypeAnnotation(annotation);
    _logger.LogDebug($"TypeResolverAdapter: Resolved to {result}");
    return result;
}
```

### 4. Cache Issues

The underlying `TypeResolver` caches results in `SemanticInfo`. If you're seeing stale types:

```csharp
// Check if cached value exists
var cached = semanticInfo.GetTypeAnnotation(annotation);
if (cached != null)
{
    // This is what's being returned - is it stale?
}

// Clear cache if needed (be careful!)
// semanticInfo.Clear(); // Usually not necessary
```

### 5. Using the Escape Hatch

If you need to debug the underlying resolver:

```csharp
var adapter = (TypeResolverAdapter)services.TypeResolver;
var underlyingResolver = adapter.UnderlyingResolver;

// Now you can access TypeResolver's internals
var symbolTable = underlyingResolver._symbolTable;  // If made accessible
```

## Contribution Guidelines

### What Changes Might Be Made

#### 1. Adding New Interface Methods

If `ITypeResolver` gains new capabilities:

```csharp
public interface ITypeResolver
{
    SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation);
    IReadOnlyList<SemanticError> Errors { get; }

    // NEW: Batch resolution for performance
    IReadOnlyList<SemanticType> ResolveTypeAnnotations(
        IEnumerable<TypeAnnotation> annotations);
}
```

Then update the adapter:

```csharp
public class TypeResolverAdapter : ITypeResolver
{
    // ... existing methods ...

    public IReadOnlyList<SemanticType> ResolveTypeAnnotations(
        IEnumerable<TypeAnnotation> annotations)
    {
        return annotations
            .Select(a => _typeResolver.ResolveTypeAnnotation(a))
            .ToList();
    }
}
```

#### 2. Migration to Direct Implementation

Eventually, this adapter might be **replaced entirely**:

```csharp
// Before: TypeResolverAdapter wraps TypeResolver
var adapter = new TypeResolverAdapter(typeResolver);

// After: DirectTypeResolver implements ITypeResolver natively
var resolver = new DirectTypeResolver(symbolTable, semanticInfo);
```

When this happens, `TypeResolverAdapter.cs` would be **deleted**.

#### 3. Improving Error Handling

Currently errors are just collected. Future improvements:

```csharp
public interface ITypeResolver
{
    SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation);
    IReadOnlyList<SemanticError> Errors { get; }

    // NEW: Get errors for specific annotation
    IReadOnlyList<SemanticError> GetErrorsFor(TypeAnnotation annotation);
}
```

#### 4. Adding Caching Metrics

For performance monitoring:

```csharp
public interface ITypeResolver
{
    // ... existing methods ...

    // NEW: Cache statistics
    CacheStatistics GetCacheStatistics();
}
```

### Code Style Guidelines

When modifying this file:

1. **Keep it simple** - This is a thin adapter, don't add logic
2. **Pure delegation** - Methods should just forward to `_typeResolver`
3. **Null checking** - Validate constructor parameters
4. **XML comments** - Document any new public members
5. **Follow C# 9.0 conventions** - No newer language features

### Testing Considerations

When adding functionality:

```csharp
// Test that adapter forwards correctly
[Fact]
public void ResolveTypeAnnotation_ForwardsToUnderlyingResolver()
{
    // Arrange
    var mockResolver = new Mock<TypeResolver>();
    mockResolver
        .Setup(r => r.ResolveTypeAnnotation(It.IsAny<TypeAnnotation>()))
        .Returns(SemanticType.Int);

    var adapter = new TypeResolverAdapter(mockResolver.Object);
    var annotation = new TypeAnnotation { Name = "int" };

    // Act
    var result = adapter.ResolveTypeAnnotation(annotation);

    // Assert
    Assert.Equal(SemanticType.Int, result);
    mockResolver.Verify(r => r.ResolveTypeAnnotation(annotation), Times.Once);
}
```

### Don't Do These

❌ **Don't add business logic** to the adapter
```csharp
// ❌ BAD - Logic belongs in TypeResolver
public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
{
    if (annotation?.Name == "special_case")
        return SemanticType.Int;  // Don't do this!

    return _typeResolver.ResolveTypeAnnotation(annotation);
}
```

❌ **Don't cache** in the adapter (caching happens in `SemanticInfo`)
```csharp
// ❌ BAD - TypeResolver already caches
private readonly Dictionary<TypeAnnotation, SemanticType> _cache = new();

public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
{
    if (_cache.TryGetValue(annotation, out var cached))
        return cached;  // Don't add redundant caching!

    // ...
}
```

❌ **Don't modify** the wrapped resolver
```csharp
// ❌ BAD - Adapter should be read-only wrapper
public void ClearErrors()
{
    _typeResolver.Errors.Clear();  // Don't mutate!
}
```

## Cross-References

### Related Documentation

- **Interface Definition**: `ITypeResolver.cs` - Defines the contract this adapter implements
- **Wrapped Class**: `src/Sharpy.Compiler/Semantic/TypeResolver.cs` - The actual type resolution logic
- **Services Architecture**: `src/Sharpy.Compiler/Services/README.md` - Overview of the services layer
- **Builder**: `CompilerServicesBuilder.cs` - Creates and configures this adapter

### Related Adapters (Same Pattern)

- `SymbolLookupAdapter.cs` - Wraps `SymbolTable` to implement `ISymbolLookup`
- `ClrTypeMapperAdapter.cs` - Wraps CLR type mapping logic to implement `IClrTypeMapper`
- `DiagnosticReporter.cs` - Implements `IDiagnosticReporter` (not an adapter, but similar service)

### Usage Examples

**In CompilerServicesBuilder** (src/Sharpy.Compiler/Services/CompilerServicesBuilder.cs):
```csharp
public CompilerServices Build()
{
    // Create the underlying resolver
    var typeResolver = new TypeResolver(_symbolTable, _semanticInfo, _logger);

    // Wrap it in the adapter
    var typeResolverAdapter = new TypeResolverAdapter(typeResolver);

    // Inject into CompilerServices
    return new CompilerServices(
        typeResolver: typeResolverAdapter,
        // ... other services
    );
}
```

**In TypeChecker** (src/Sharpy.Compiler/Semantic/TypeChecker.cs):
```csharp
public class TypeChecker
{
    private readonly ITypeResolver _typeResolver;

    public TypeChecker(CompilerServices services)
    {
        _typeResolver = services.TypeResolver;  // Uses the adapter
    }

    private void CheckVariableDeclaration(VariableDeclarationStatement stmt)
    {
        var declaredType = _typeResolver.ResolveTypeAnnotation(stmt.TypeAnnotation);
        // ...
    }
}
```

### Specification References

While this file itself doesn't directly implement language features, the underlying `TypeResolver` handles:

- `docs/language_specification/type_annotations.md` - Type annotation syntax
- `docs/language_specification/type_hierarchy.md` - Type system structure
- `docs/language_specification/type_casting.md` - Type conversions (indirectly)
- `docs/language_specification/type_narrowing.md` - Type refinement (indirectly)

## Summary

`TypeResolverAdapter` is a **lightweight wrapper** that bridges the gap between the legacy `TypeResolver` class and the new `ITypeResolver` interface-based services architecture. It's a critical piece of Sharpy's gradual migration strategy, allowing new code to use modern dependency injection patterns while maintaining compatibility with existing semantic analysis code.

**Key Takeaways:**
- ✅ Simple adapter pattern - pure delegation, no business logic
- ✅ Enables gradual migration to services architecture
- ✅ Provides interface-based access to type resolution
- ✅ Maintains backwards compatibility via `UnderlyingResolver`
- ✅ Thread-safe and cacheable design
- ⚠️ Temporary migration aid - may be replaced in the future

When working with this file, remember: **keep it simple, delegate everything, and prefer the interface over the escape hatch**.
