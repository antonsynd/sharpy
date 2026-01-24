# Walkthrough: ITypeResolver.cs

**Source File**: `src/Sharpy.Compiler/Services/ITypeResolver.cs`

---

## Overview

`ITypeResolver` is a **service interface** that provides a clean abstraction for resolving type annotations from the AST into semantic types. This interface is part of Sharpy's new **services architecture**, which aims to decouple compiler components and improve testability.

**Role in the Compiler Pipeline:**
```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → CodeGen
                                              ↑
                                    ITypeResolver sits here
```

The type resolver sits in the **Semantic Analysis** phase and acts as a bridge between:
- **Input**: `TypeAnnotation` nodes from the AST (what the programmer wrote)
- **Output**: `SemanticType` instances (how the compiler understands those types)

This is a **read-only service** that doesn't modify the AST. It queries the symbol table and caches results for efficiency.

## Interface Structure

```csharp
public interface ITypeResolver
{
    SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation);
    IReadOnlyList<SemanticError> Errors { get; }
}
```

The interface is deliberately **minimal and focused**, with just two members:

### 1. `ResolveTypeAnnotation` Method

**Purpose**: Converts a type annotation from the parse tree into a semantic type.

**Key Characteristics:**
- **Thread-safe**: Designed for parallel compilation scenarios
- **Cached**: Results are stored in `SemanticInfo` to avoid redundant resolution
- **Nullable-aware**: Handles `null` annotations (returns `SemanticType.Unknown`)
- **Error-tolerant**: Returns `Unknown` type on errors to prevent cascading failures

**Common Usage Pattern:**
```csharp
// In type checker or validator
var annotation = variableDecl.TypeAnnotation;
SemanticType resolvedType = typeResolver.ResolveTypeAnnotation(annotation);

if (resolvedType == SemanticType.Unknown)
{
    // Type resolution failed, check Errors property
}
```

**What Gets Resolved:**
- Built-in primitives (`int`, `str`, `bool`, `float`, etc.)
- User-defined types (classes, structs, interfaces)
- Generic types (`list[int]`, `dict[str, float]`)
- Type parameters (`T` in `class Box[T]`)
- Type aliases (`type UserId = int`)
- Function types (`(int, str) -> bool`)
- Nullable types (`int?`, `str?`)
- Tuple types (`tuple[int, str, bool]`)

### 2. `Errors` Property

**Purpose**: Collects all errors encountered during type resolution.

**Key Points:**
- Read-only collection of `SemanticError` instances
- Each error includes location information (line, column)
- Errors accumulate across multiple `ResolveTypeAnnotation` calls
- Must be checked after resolution to detect failures

**Example Usage:**
```csharp
var type = typeResolver.ResolveTypeAnnotation(annotation);

if (typeResolver.Errors.Any())
{
    foreach (var error in typeResolver.Errors)
    {
        Console.WriteLine($"Line {error.Line}: {error.Message}");
    }
}
```

## Dependencies

### Internal Dependencies

`ITypeResolver` depends on these Sharpy namespaces:

1. **`Sharpy.Compiler.Parser.Ast`**
   - `TypeAnnotation`: The input type (AST node representing `int`, `list[str]`, etc.)
   - Location: `src/Sharpy.Compiler/Parser/Ast/Types.cs:8`

2. **`Sharpy.Compiler.Semantic`**
   - `SemanticType`: The output type (resolved type information)
   - `SemanticError`: Error reporting type
   - Location: `src/Sharpy.Compiler/Semantic/SemanticType.cs:43`

### Related Types

**TypeAnnotation (Input)**
```csharp
public record TypeAnnotation
{
    public string Name { get; init; }                    // "int", "list", "MyClass"
    public ImmutableArray<TypeAnnotation> TypeArguments  // For list[int], dict[K,V]
    public bool IsNullable { get; init; }                // For T? syntax
    public int LineStart, ColumnStart, LineEnd, ColumnEnd
}
```

**SemanticType (Output)**
```csharp
public abstract record SemanticType : ITypeInfo
{
    // Singleton instances for common types
    public static readonly SemanticType Int;
    public static readonly SemanticType Str;
    public static readonly SemanticType Bool;
    // ... other built-ins

    public abstract string GetDisplayName();
    public virtual bool IsAssignableTo(SemanticType other);
}
```

Concrete semantic types include:
- `BuiltinType` - Primitives like `int`, `bool`, `str`
- `UserDefinedType` - Classes, structs, interfaces
- `GenericType` - Parameterized types like `list[int]`
- `NullableType` - Optional types like `int?`
- `TupleType` - Tuples like `tuple[int, str]`
- `FunctionType` - Function signatures like `(int) -> str`
- `TypeParameterType` - Generic parameters like `T`

**SemanticError (Error Reporting)**
```csharp
public class SemanticError : Exception
{
    public int? Line { get; }
    public int? Column { get; }
    // Formatted message with location
}
```

## Implementation: TypeResolverAdapter

The actual implementation is `TypeResolverAdapter` (in `Services/TypeResolverAdapter.cs:10`), which wraps the legacy `TypeResolver` class:

```csharp
public class TypeResolverAdapter : ITypeResolver
{
    private readonly TypeResolver _typeResolver;

    public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
    {
        return _typeResolver.ResolveTypeAnnotation(annotation);
    }

    public IReadOnlyList<SemanticError> Errors => _typeResolver.Errors;
}
```

**Why an Adapter?**
This is part of a **gradual migration strategy**. The `TypeResolver` class (`Semantic/TypeResolver.cs:9`) predates the services architecture. The adapter allows new code to use the clean `ITypeResolver` interface while old code continues working with `TypeResolver` directly.

### How TypeResolver Works Internally

The underlying `TypeResolver` implementation:

1. **Checks cache first** (via `SemanticInfo.GetTypeAnnotation`)
2. **Resolves the type** based on annotation name:
   - Built-in types (`int`, `str`, etc.) → Returns singleton instances
   - Type aliases → Expands recursively
   - Generic types → Resolves base type and type arguments
   - Type parameters → Creates `TypeParameterType`
   - User-defined types → Looks up in symbol table
3. **Handles nullable modifier** (`?` suffix)
4. **Caches result** (via `SemanticInfo.SetTypeAnnotation`)
5. **Returns resolved type** (or `Unknown` on error)

**Example Resolution Flow:**

```python
# Source code
items: list[str]? = None
```

```
TypeAnnotation { Name="list", TypeArguments=[TypeAnnotation{Name="str"}], IsNullable=true }
    ↓
1. Not in cache
2. Look up "list" in symbol table → GenericType symbol
3. Resolve type arguments:
   - TypeAnnotation{Name="str"} → SemanticType.Str
4. Create GenericType { Name="list", TypeArguments=[Str] }
5. Apply nullable: NullableType { UnderlyingType=GenericType{...} }
6. Cache result
7. Return NullableType
```

## Integration with CompilerServices

`ITypeResolver` is registered in `CompilerServices` (the central dependency container):

```csharp
public class CompilerServices
{
    public ITypeResolver TypeResolver { get; }

    // Convenience wrapper
    public SemanticType ResolveType(TypeAnnotation? annotation)
    {
        return TypeResolver.ResolveTypeAnnotation(annotation);
    }
}
```

**Usage in Validators:**
```csharp
public class SomeValidator
{
    private readonly CompilerServices _services;

    void ValidateSomething(VariableDeclaration decl)
    {
        var type = _services.ResolveType(decl.TypeAnnotation);
        // Use resolved type...
    }
}
```

## Patterns and Design Decisions

### 1. **Service Interface Pattern**

The interface separates **what** the service does from **how** it does it:
- Callers depend on `ITypeResolver` (stable interface)
- Implementation can be swapped (for testing, optimization, etc.)
- Easier to mock in unit tests

### 2. **Caching Strategy**

Type resolution is expensive (symbol table lookups, recursive resolution). The service caches results in `SemanticInfo`:
- First call: Full resolution + cache store
- Subsequent calls: Instant cache lookup
- Cache key: The `TypeAnnotation` AST node instance

**Performance Impact:**
- Without cache: O(n) lookups per annotation
- With cache: O(1) after first lookup
- Critical for expressions like `x: list[dict[str, int]]` which nest annotations

### 3. **Error Recovery**

Returns `SemanticType.Unknown` on errors instead of throwing exceptions:
- **Prevents cascading errors**: Unknown types are assignable to everything
- **Allows compilation to continue**: Gather all errors in one pass
- **Explicit error checking**: Callers check the `Errors` property

Example:
```python
# Typo in type name
x: Stirng = "hello"  # Should be "str"
```
```
Resolution: SemanticType.Unknown
Errors: ["Type 'Stirng' not found"]
Compilation continues: Gather more errors before failing
```

### 4. **Thread Safety**

Documented as thread-safe for future parallel compilation:
- Immutable `SemanticType` instances
- Cache uses thread-safe collections
- No mutable shared state in resolution logic

### 5. **Nullable Type Handling**

The interface handles nullability at the semantic level:
```python
x: int   # SemanticType: BuiltinType{int}
y: int?  # SemanticType: NullableType{UnderlyingType=BuiltinType{int}}
```

This encoding enables:
- Type checking: `int` cannot be assigned `None`, but `int?` can
- Code generation: Emit `int` vs `Nullable<int>` in C#
- Diagnostic messages: "Expected int, got int?"

## Debugging Tips

### 1. **Type Not Found Errors**

If `ResolveTypeAnnotation` returns `Unknown`:

```csharp
var type = typeResolver.ResolveTypeAnnotation(annotation);
if (type == SemanticType.Unknown)
{
    // Check what went wrong
    foreach (var error in typeResolver.Errors)
    {
        Console.WriteLine($"Resolution failed: {error.Message}");
    }

    // Inspect the annotation
    Console.WriteLine($"Trying to resolve: {annotation.Name}");
    Console.WriteLine($"Type arguments: {annotation.TypeArguments.Length}");
}
```

**Common Causes:**
- Typo in type name (`Stirng` instead of `str`)
- Type not imported (`from mymodule import MyClass`)
- Generic type without arguments (`list` instead of `list[int]`)

### 2. **Inspect Cached Results**

To see what's in the cache:

```csharp
// Access SemanticInfo directly (available via CompilerServices)
var cachedType = services.SemanticInfo.GetTypeAnnotation(annotation);
if (cachedType != null)
{
    Console.WriteLine($"Cached: {cachedType.GetDisplayName()}");
}
```

### 3. **Trace Resolution**

Add logging in the `TypeResolver` implementation:

```csharp
public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
{
    _logger.LogDebug($"Resolving type: {annotation?.Name}");

    // ... resolution logic ...

    _logger.LogDebug($"Resolved to: {result.GetDisplayName()}");
    return result;
}
```

### 4. **Generic Type Mismatches**

For generic types, check:
- Is the base type actually generic? (`list` is, `int` isn't)
- Do type argument counts match? (`dict` needs 2, `list` needs 1)

```csharp
if (result is GenericType genericType)
{
    Console.WriteLine($"Base: {genericType.Name}");
    Console.WriteLine($"Args: {string.Join(", ",
        genericType.TypeArguments.Select(t => t.GetDisplayName()))}");
}
```

### 5. **Symbol Table Issues**

If types aren't found, verify the symbol table:

```csharp
var symbol = services.SymbolTable.LookupType("MyClass");
if (symbol == null)
{
    Console.WriteLine("MyClass not in symbol table!");
    // Check if it was registered during AST processing
}
```

## Contribution Guidelines

### When to Modify This Interface

**DO modify** if:
- Adding new fundamental type resolution capabilities (e.g., union types)
- Changing error reporting strategy (e.g., returning diagnostics instead of exceptions)
- Supporting new language features that affect type annotations

**DON'T modify** if:
- Changing internal resolution logic (modify `TypeResolver` implementation)
- Adding validation rules (use separate validators)
- Improving performance (optimize implementation, not interface)

### Adding a New Method

If you need to add a method to `ITypeResolver`:

1. **Ensure it's truly type resolution**
   - Is this converting annotations to semantic types?
   - Or is it type checking/validation? (Use separate service)

2. **Update both interface and adapter**
   ```csharp
   // ITypeResolver.cs
   public interface ITypeResolver
   {
       SemanticType ResolveCustomType(SomeAnnotation annotation);
   }

   // TypeResolverAdapter.cs
   public SemanticType ResolveCustomType(SomeAnnotation annotation)
   {
       return _typeResolver.ResolveCustomType(annotation);
   }
   ```

3. **Implement in TypeResolver**
   ```csharp
   // TypeResolver.cs
   public SemanticType ResolveCustomType(SomeAnnotation annotation)
   {
       // Implementation
   }
   ```

### Testing

When testing code that uses `ITypeResolver`:

```csharp
// Create a mock for unit tests
var mockResolver = new Mock<ITypeResolver>();
mockResolver
    .Setup(r => r.ResolveTypeAnnotation(It.IsAny<TypeAnnotation>()))
    .Returns(SemanticType.Int);

// Or use the real implementation with a test symbol table
var symbolTable = new SymbolTable();
var semanticInfo = new SemanticInfo();
var resolver = new TypeResolver(symbolTable, semanticInfo);
var adapter = new TypeResolverAdapter(resolver);
```

### Migration Path

If you're working on code that still uses `TypeResolver` directly:

**Before (old code):**
```csharp
public class SomeComponent
{
    private readonly TypeResolver _typeResolver;

    public SomeComponent(TypeResolver typeResolver)
    {
        _typeResolver = typeResolver;
    }
}
```

**After (new code):**
```csharp
public class SomeComponent
{
    private readonly ITypeResolver _typeResolver;

    public SomeComponent(CompilerServices services)
    {
        _typeResolver = services.TypeResolver;
    }
}
```

Or use the convenience wrapper:
```csharp
public class SomeComponent
{
    private readonly CompilerServices _services;

    void Process(TypeAnnotation annotation)
    {
        var type = _services.ResolveType(annotation);  // Shorthand
    }
}
```

## Cross-References

### Related Interfaces (Services Architecture)

- **`ISymbolLookup`** (`Services/ISymbolLookup.cs`) - Symbol table queries
- **`IClrTypeMapper`** (`Services/IClrTypeMapper.cs`) - Map Sharpy types to .NET types
- **`IDiagnosticReporter`** (`Services/IDiagnosticReporter.cs`) - Error reporting

### Implementation Files

- **TypeResolverAdapter** (`Services/TypeResolverAdapter.cs:10`) - Adapter implementation
- **TypeResolver** (`Semantic/TypeResolver.cs:9`) - Actual resolution logic
- **CompilerServices** (`Services/CompilerServices.cs:18`) - Service container

### Documentation

- **SemanticType walkthrough** (`docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/SemanticType.md`)
- **SymbolTable walkthrough** (`docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/SymbolTable.md`)
- **Type Annotations spec** (`docs/language_specification/type_annotations.md`)

### Key AST Types

- **TypeAnnotation** (`Parser/Ast/Types.cs:8`) - Input AST node
- **Types walkthrough** (`docs/implementation_walkthrough/src/Sharpy.Compiler/Parser/Ast/Types.md`)

### Semantic Analysis

For components that **use** `ITypeResolver`:
- TypeChecker (validates type correctness)
- ValidationPipeline (runs semantic validators)
- OperatorValidator (checks operator type compatibility)
- ProtocolValidator (verifies protocol implementation)

All of these depend on `ITypeResolver` to convert AST type annotations into semantic types for analysis.

## Summary

`ITypeResolver` is a **clean abstraction** for type resolution that:

1. **Bridges AST and semantics**: Converts parsed type annotations to semantic types
2. **Enables dependency injection**: Components depend on interface, not implementation
3. **Supports caching**: Avoids redundant expensive lookups
4. **Handles errors gracefully**: Returns Unknown type + collects errors
5. **Designed for evolution**: Part of services architecture for future improvements

When working with types in the Sharpy compiler, this is your **go-to service** for turning what the programmer wrote (`int`, `list[str]`, `MyClass?`) into what the compiler understands (`SemanticType` instances with full type information).
