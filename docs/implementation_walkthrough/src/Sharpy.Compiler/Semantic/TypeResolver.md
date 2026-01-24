# Walkthrough: TypeResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

---

## Overview

The `TypeResolver` class is a critical component in the Sharpy compiler's semantic analysis phase. Its primary responsibility is to **resolve type annotations from the Abstract Syntax Tree (AST) into concrete semantic types** that can be used by the type checker and code generator.

Think of TypeResolver as a translator: it takes the raw type annotations written by the programmer (like `int`, `list[str]`, or `MyClass?`) and converts them into rich `SemanticType` objects that carry full type information, including whether the type is nullable, generic, user-defined, or built-in.

### Position in the Compiler Pipeline

```
Parser (AST with TypeAnnotations)
    → TypeResolver (converts to SemanticType)
        → TypeChecker (uses SemanticType for validation)
            → RoslynEmitter (generates C#)
```

The TypeResolver sits between the Parser and TypeChecker:
- **Input**: `TypeAnnotation` nodes from the AST (lightweight, just strings and flags)
- **Output**: `SemanticType` objects (rich type information with inheritance, generics, etc.)

---

## Class Structure

### Dependencies

The TypeResolver relies on three key dependencies injected through its constructor:

```csharp
public TypeResolver(
    SymbolTable symbolTable,      // Scope management and symbol lookup
    SemanticInfo semanticInfo,     // AST annotation cache
    ICompilerLogger? logger = null // Error logging
)
```

1. **`SymbolTable`**: Manages scopes and symbol lookups. TypeResolver uses this to:
   - Look up user-defined types (classes, structs, interfaces)
   - Find type aliases (e.g., `type IntList = list[int]`)
   - Resolve type parameters (e.g., `T` in `class Box[T]`)

2. **`SemanticInfo`**: Caches resolved types to avoid redundant work. Since AST nodes are immutable, TypeResolver can safely cache the mapping from `TypeAnnotation` → `SemanticType`.

3. **`ICompilerLogger`**: Logs semantic errors when types can't be resolved.

### Internal State

```csharp
private readonly List<SemanticError> _errors = new();
public IReadOnlyList<SemanticError> Errors => _errors;
```

The TypeResolver accumulates errors during resolution rather than throwing exceptions. This allows the compiler to continue and report multiple errors in a single compilation pass.

---

## Key Methods

### 1. `ResolveTypeAnnotation(TypeAnnotation? annotation)`

**Purpose**: The main entry point for type resolution. Converts a type annotation from the AST into a semantic type.

**Algorithm**:

```csharp
public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
{
    // 1. Handle null annotations
    if (annotation == null)
        return SemanticType.Unknown;

    // 2. Check cache (performance optimization)
    var cached = _semanticInfo.GetTypeAnnotation(annotation);
    if (cached != null)
        return cached;

    // 3. Resolve based on type category (see resolution logic below)
    SemanticType result = /* resolution logic */;

    // 4. Apply nullable modifier if needed
    if (annotation.IsNullable && /* conditions */)
        result = new NullableType { UnderlyingType = result };

    // 5. Cache and return
    _semanticInfo.SetTypeAnnotation(annotation, result);
    return result;
}
```

**Resolution Priority** (in order):

1. **`auto` keyword**: Returns `SemanticType.Unknown` to trigger type inference later
2. **Built-in types**: `int`, `float`, `str`, `bool`, etc. (see `TryResolveBuiltinType`)
3. **Type aliases**: Expands aliases recursively (e.g., `type IntList = list[int]`)
4. **Generic types**: Types with type arguments (e.g., `list[int]`, `dict[str, int]`)
5. **Type parameters**: Generic type parameters (e.g., `T` in `class Box[T]`)
6. **User-defined types**: Classes, structs, interfaces defined in the code

**Important Design Decision**: Type aliases are expanded during resolution, meaning they're compile-time only. The C# output never sees the alias name, only the underlying type.

**Nullable Handling**: The nullable modifier (`?`) is applied *after* resolving the base type, except for type aliases which handle nullability during expansion (src/Sharpy.Compiler/Semantic/TypeResolver.cs:88-94).

---

### 2. `TryResolveBuiltinType(string name, out SemanticType type)`

**Purpose**: Maps Sharpy's built-in type names to their corresponding `SemanticType` singletons.

**Key Mappings**:

```csharp
"int"     → SemanticType.Int      (C# int)
"long"    → SemanticType.Long     (C# long)
"float"   → SemanticType.Float    (C# double - 64-bit)
"float32" → SemanticType.Float32  (C# float - 32-bit)
"float64" → SemanticType.Double   (C# double)
"bool"    → SemanticType.Bool     (C# bool)
"str"     → SemanticType.Str      (C# string)
"None"    → SemanticType.Void     (C# void)
```

**Important Note**: Sharpy's `float` maps to C# `double` (64-bit), following Python's convention. Use `float32` for C# `float` (32-bit).

---

### 3. `ResolveGenericType(TypeAnnotation annotation)`

**Purpose**: Resolves generic types like `list[int]`, `dict[str, int]`, or `tuple[int, str, float]`.

**Special Case - Tuples**: Tuples have variable arity (can have any number of type arguments), unlike other generics which have a fixed number of type parameters:

```csharp
if (annotation.Name == "tuple")
{
    var elementTypes = annotation.TypeArguments
        .Select(ResolveTypeAnnotation)
        .ToList();
    return new TupleType { ElementTypes = elementTypes };
}
```

**Validation**: For non-tuple generics, the resolver validates that the number of type arguments matches the type's declared type parameters:

```csharp
if (typeSymbol.IsGeneric && typeArgs.Count != typeSymbol.TypeParameters.Count)
{
    AddError($"Type '{annotation.Name}' expects {typeSymbol.TypeParameters.Count}
              type arguments but got {typeArgs.Count}", null, null);
    return SemanticType.Unknown;
}
```

**Return Type**: `GenericType` containing:
- `Name`: The generic type name (e.g., "list")
- `TypeArguments`: Resolved type arguments (e.g., `[SemanticType.Int]`)
- `GenericDefinition`: The type symbol for the generic type definition

---

### 4. `ExpandTypeAlias(TypeAliasSymbol aliasSymbol, bool isNullable)`

**Purpose**: Recursively expands type aliases to their underlying types.

**Two Expansion Paths**:

1. **Type annotation aliases** (most common):
   ```sharpy
   type IntList = list[int]
   ```
   → Recursively calls `ResolveTypeAnnotation` on the alias's type annotation

2. **Function type aliases**:
   ```sharpy
   type Predicate = (int) -> bool
   ```
   → Calls `ResolveFunctionType` to create a `FunctionType`

**Nullable Modifier Application**: If the alias is used with `?` (e.g., `IntList?`), the nullable modifier is applied to the expanded type:

```csharp
if (isNullable && result != SemanticType.Unknown)
{
    result = new NullableType { UnderlyingType = result };
}
```

**Error Handling**: If the alias has neither a type annotation nor a function type, it's malformed:

```csharp
AddError($"Type alias '{aliasSymbol.Name}' has no type definition",
    aliasSymbol.DeclarationLine, aliasSymbol.DeclarationColumn);
return SemanticType.Unknown;
```

---

### 5. `ResolveFunctionType(Parser.Ast.FunctionType functionType)`

**Purpose**: Converts function type annotations into semantic function types.

**Example**:
```sharpy
type Comparator = (int, int) -> bool
```

**Implementation**:
```csharp
var paramTypes = functionType.ParameterTypes
    .Select(ResolveTypeAnnotation)
    .ToList();

var returnType = ResolveTypeAnnotation(functionType.ReturnType);

return new Semantic.FunctionType
{
    ParameterTypes = paramTypes,
    ReturnType = returnType
};
```

**Important**: Notice the namespace qualification: `Parser.Ast.FunctionType` (input) vs. `Semantic.FunctionType` (output). The resolver converts AST types to semantic types.

---

### 6. `AddError(string message, int? line, int? column)`

**Purpose**: Records a semantic error and logs it.

**Design Pattern**: The TypeResolver uses error accumulation rather than throwing exceptions:
- Allows reporting multiple errors in one pass
- Returns `SemanticType.Unknown` to enable error recovery
- Prevents cascading errors (Unknown is assignable to anything)

```csharp
var error = new SemanticError(message, line, column);
_errors.Add(error);
_logger.LogError(error.Message, line ?? 0, column ?? 0);
```

---

## Dependencies on Other Components

### Upstream: Parser AST

The TypeResolver consumes types from the `Parser.Ast` namespace:
- **`TypeAnnotation`** (src/Sharpy.Compiler/Parser/Ast/Types.cs:8): AST node representing a type annotation
  - `Name`: Type name (e.g., "int", "list")
  - `TypeArguments`: Generic type arguments
  - `IsNullable`: Whether the `?` modifier is present
- **`FunctionType`**: AST node for function type annotations

### Downstream: Semantic Analysis

The TypeResolver produces `SemanticType` objects used by:
- **TypeChecker**: Validates type compatibility, checks assignments
- **OperatorValidator**: Validates operator usage based on types
- **RoslynEmitter**: Generates C# code based on semantic types

### Peer Dependencies

- **`SymbolTable`** (src/Sharpy.Compiler/Semantic/SymbolTable.cs:6): Provides symbol lookup
- **`SemanticInfo`** (src/Sharpy.Compiler/Semantic/SemanticInfo.cs:10): Caches AST-to-semantic-type mappings
- **`Symbol` hierarchy** (src/Sharpy.Compiler/Semantic/Symbol.cs:8):
  - `TypeSymbol`: User-defined types
  - `TypeAliasSymbol`: Type aliases
  - `TypeParameterSymbol`: Generic type parameters

---

## Patterns and Design Decisions

### 1. **Caching with SemanticInfo**

TypeResolver aggressively caches resolved types to avoid redundant work:

```csharp
var cached = _semanticInfo.GetTypeAnnotation(annotation);
if (cached != null)
    return cached;
```

**Why?** AST nodes are immutable and may be visited multiple times during semantic analysis. Caching ensures O(1) lookups after the first resolution.

**Key Point**: `SemanticInfo` uses `ReferenceEqualityComparer` because AST nodes are records with value-based equality, but we need to distinguish different instances.

### 2. **Singleton Pattern for Common Types**

Common types like `Int`, `Str`, `Bool` are pre-allocated singletons in `SemanticType`:

```csharp
public static readonly SemanticType Int = new BuiltinType { Name = "int", ClrType = typeof(int) };
```

**Benefits**:
- Memory efficiency (one instance per type)
- Fast reference equality checks
- Improved cache performance

### 3. **Error Recovery with Unknown Type**

When a type can't be resolved, TypeResolver returns `SemanticType.Unknown` instead of throwing:

```csharp
AddError($"Type '{annotation.Name}' not found", null, null);
result = SemanticType.Unknown;
```

**Why?** `UnknownType` is designed to be assignable to anything, preventing cascading errors. The compiler can continue analyzing the rest of the code.

### 4. **Immutable AST, Mutable Annotations**

The AST is immutable, but semantic information is stored separately in `SemanticInfo`. This follows the **Visitor Pattern** where analysis results are kept outside the tree structure.

**Advantage**: Multiple analysis passes can annotate the same AST without conflicts.

### 5. **Recursive Resolution**

Type resolution is inherently recursive:
- Generic types recursively resolve their type arguments
- Type aliases recursively expand to their underlying types
- Nullable types wrap their underlying types

**Example**:
```sharpy
type IntList = list[int]
type NullableIntList = IntList?
```

Resolution chain: `NullableIntList` → `IntList` → `list[int]` → `list[SemanticType.Int]` → `NullableType(GenericType(...))`

---

## Debugging Tips

### 1. **Type Not Found Errors**

If you see "Type 'X' not found":
- Check if the type is defined in the current scope
- Verify the type was properly registered in the SymbolTable
- For imported types, ensure the import statement was processed
- Check spelling and casing (type names are case-sensitive)

### 2. **Generic Type Argument Mismatches**

Error: "Type 'X' expects N type arguments but got M"
- The type definition declares N type parameters
- The usage provides M type arguments
- Check the type definition: `class Box[T]` has 1 parameter
- Check the usage: `Box[int, str]` provides 2 arguments (error!)

### 3. **Nullable Type Issues**

If nullable types aren't being handled correctly:
- Check the `IsNullable` flag on the `TypeAnnotation`
- Verify that nullable wrapping happens *after* base type resolution
- For type aliases, nullable is applied in `ExpandTypeAlias`, not in the main method

### 4. **Caching Problems**

If types aren't being cached properly:
- Verify that `SemanticInfo` is shared across all resolver instances
- Check that you're using reference equality, not value equality
- Ensure the same AST node instance is being passed

### 5. **Logging and Debugging**

To trace type resolution:
```csharp
var logger = new ConsoleLogger();  // or your custom logger
var resolver = new TypeResolver(symbolTable, semanticInfo, logger);
```

Watch for:
- Which type resolution path is taken (builtin, generic, user-defined, etc.)
- Whether caching is working (should see cache hits on repeat visits)
- Error messages with line/column information

### 6. **Common Pitfalls**

- **Don't confuse `Parser.Ast.FunctionType` with `Semantic.FunctionType`**: They're different types with the same name!
- **Remember the float mapping**: Sharpy `float` → C# `double`, not `float`
- **Type aliases are expanded**: The C# output never sees the alias name
- **Tuples are special**: They don't validate type argument count like other generics

---

## Contribution Guidelines

### When to Modify TypeResolver

You'll need to modify TypeResolver when:

1. **Adding new built-in types**: Update `TryResolveBuiltinType` with the new type mapping
2. **Supporting new type syntax**: Add resolution logic for new type annotation forms
3. **Implementing new type features**: E.g., union types, intersection types
4. **Improving error messages**: Make type errors more helpful and specific
5. **Performance optimizations**: Improve caching or reduce allocations

### What NOT to Change

- **Don't add type checking logic**: That belongs in `TypeChecker`, not `TypeResolver`
- **Don't modify AST nodes**: Keep the AST immutable
- **Don't skip caching**: Always cache resolved types in `SemanticInfo`
- **Don't throw exceptions**: Use error accumulation instead

### Testing TypeResolver Changes

When modifying TypeResolver:

1. **Unit tests**: Test individual methods in isolation
   ```csharp
   var resolver = new TypeResolver(symbolTable, semanticInfo);
   var result = resolver.ResolveTypeAnnotation(annotation);
   Assert.Equal(expectedType, result);
   ```

2. **Integration tests**: Test full type resolution in real code
   - Create `.spy` files with various type annotations
   - Verify the resolved types match expectations
   - Check that errors are properly reported

3. **File-based tests**: Add test fixtures in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
   - `type_resolution.spy` + `type_resolution.expected` for success cases
   - `type_resolution_error.spy` + `type_resolution_error.error` for error cases

### Code Style

- Follow existing patterns (caching, error handling)
- Add XML doc comments for public methods
- Keep methods focused (single responsibility)
- Use descriptive variable names
- Add comments for non-obvious logic

---

## Cross-References

### Related Semantic Components

- **[SemanticType.md](SemanticType.md)**: Defines all semantic type classes (BuiltinType, GenericType, etc.)
- **[SymbolTable.md](SymbolTable.md)**: Manages scopes and symbol lookups
- **[SemanticInfo.md](SemanticInfo.md)**: Caches AST-to-semantic mappings
- **[Symbol.md](Symbol.md)**: Defines symbol types (TypeSymbol, TypeAliasSymbol, etc.)

### Related Parser Components

- **Parser/Ast/Types.cs**: Defines AST type annotation nodes

### Related TypeChecker Components

- **[TypeChecker.md](TypeChecker.md)**: Uses resolved types for validation
- **[OperatorValidator.md](OperatorValidator.md)**: Validates operators based on resolved types

### Language Specification

- [Type Annotations](../../../../language_specification/type_annotations.md): Syntax and semantics
- [Type Hierarchy](../../../../language_specification/type_hierarchy.md): Type relationships and conversions
- [Type Narrowing](../../../../language_specification/type_narrowing.md): Control flow type refinement
- [Type Casting](../../../../language_specification/type_casting.md): Explicit type conversions

---

## Summary

The TypeResolver is a focused, single-purpose component that bridges the gap between the Parser and TypeChecker. It converts textual type annotations into rich semantic types, handling built-ins, generics, user-defined types, type aliases, and nullability. Its design emphasizes caching, error recovery, and clean separation of concerns.

**Key Takeaways**:
- TypeResolver is a translator: `TypeAnnotation` → `SemanticType`
- Caching in `SemanticInfo` is critical for performance
- Error recovery with `SemanticType.Unknown` prevents cascading errors
- Type aliases are expanded at resolution time (compile-time only)
- Tuples are special (variable arity)
- Always maintain immutable AST + separate annotations pattern
