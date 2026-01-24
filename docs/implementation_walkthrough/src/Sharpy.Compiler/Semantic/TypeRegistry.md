# Walkthrough: TypeRegistry.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeRegistry.cs`

---

## Overview

`TypeRegistry` is a centralized registry for managing type information during semantic analysis in the Sharpy compiler. It serves as a **single source of truth** for accessing both builtin types (like `int`, `str`, `bool`) and user-defined types (like classes and interfaces).

### The Problem It Solves

Before `TypeRegistry`, type information was scattered across multiple places:
- `TypeAnnotation` (AST nodes representing type syntax)
- `SemanticType` (resolved type information)
- `TypeSymbol` (symbol table entries for type declarations)

This scattering made it difficult to:
- Get a canonical representation of a type by name
- Distinguish between builtin and user-defined types consistently
- Cache and reuse resolved type information

`TypeRegistry` addresses this by providing:
1. **Canonical type lookup** - One place to ask "what is type X?"
2. **Builtin type management** - Centralized registry of primitive types
3. **User-defined type tracking** - Registration and retrieval of classes/interfaces
4. **Type utility operations** - Common operations like nullable wrapping/unwrapping

### Position in the Compiler Pipeline

```
Parser → AST → [NameResolver] → [TypeResolver] → [TypeChecker] → CodeGen
                      ↓                ↓               ↓
                 TypeRegistry ← SemanticType ← SymbolTable
```

- **Used by**: `TypeResolver`, `TypeChecker`, and potentially other semantic analysis components
- **Depends on**: `SymbolTable` (for looking up user-defined types)
- **Works with**: `SemanticType` (returns resolved type instances)

---

## Class Structure

```csharp
public class TypeRegistry
{
    private readonly Dictionary<string, SemanticType> _builtinTypes;
    private readonly Dictionary<string, TypeSymbol> _userTypes;
    private readonly SymbolTable _symbolTable;
}
```

### Key Data Members

| Field | Type | Purpose |
|-------|------|---------|
| `_builtinTypes` | `Dictionary<string, SemanticType>` | Maps builtin type names to their canonical `SemanticType` instances |
| `_userTypes` | `Dictionary<string, TypeSymbol>` | Maps user-defined type names to their `TypeSymbol` declarations |
| `_symbolTable` | `SymbolTable` | Reference to the symbol table for dynamic lookups |

---

## Key Methods

### Constructor & Initialization

#### `TypeRegistry(SymbolTable symbolTable)`

**Purpose**: Initializes the registry with a reference to the symbol table and populates builtin types.

```csharp
public TypeRegistry(SymbolTable symbolTable)
{
    _symbolTable = symbolTable;
    InitializeBuiltinTypes();
}
```

**Key Detail**: The constructor immediately calls `InitializeBuiltinTypes()` to ensure builtins are available as soon as the registry is created.

#### `InitializeBuiltinTypes()`

**Purpose**: Populates the `_builtinTypes` dictionary with all Sharpy primitive types.

**Builtin Types Registered**:

| Sharpy Name(s) | C# Type | SemanticType Constant |
|---------------|---------|----------------------|
| `int`, `int32` | `int` | `SemanticType.Int` |
| `long`, `int64` | `long` | `SemanticType.Long` |
| `float`, `float64` | `double` | `SemanticType.Float` |
| `float32` | `float` | `SemanticType.Float32` |
| `double` | `double` | `SemanticType.Double` |
| `bool` | `bool` | `SemanticType.Bool` |
| `str`, `string` | `string` | `SemanticType.Str` |
| `object` | - | `SemanticType.Object` |

**Implementation Note**: 
- Multiple names can map to the same type (e.g., `int` and `int32` both map to `SemanticType.Int`)
- This supports both Python-style (`str`, `int`) and .NET-style (`string`, `int32`) naming
- The `float` type maps to C# `double` (64-bit), following Sharpy's specification

**Design Pattern**: Using singleton instances (`SemanticType.Int`, etc.) ensures type equality checks via reference equality work correctly.

---

### Type Lookup Methods

#### `GetType(string name) -> SemanticType?`

**Purpose**: The primary method for resolving a type by name. Checks builtins first, then falls back to user-defined types.

```csharp
public SemanticType? GetType(string name)
{
    // 1. Check builtin types (fast dictionary lookup)
    if (_builtinTypes.TryGetValue(name, out var builtin))
        return builtin;

    // 2. Check symbol table for user-defined types
    var symbol = _symbolTable.Lookup(name);
    if (symbol is TypeSymbol typeSymbol)
    {
        return new UserDefinedType { Name = name, Symbol = typeSymbol };
    }

    // 3. Type not found
    return null;
}
```

**Lookup Priority**:
1. **Builtins first** - Ensures primitive types are always resolved consistently
2. **User-defined types second** - Allows classes/interfaces defined by the user
3. **Null if not found** - Caller must handle missing types (typically a semantic error)

**Important**: This method creates a **new** `UserDefinedType` instance each time. Since `SemanticType` is immutable, this is safe, but be aware for identity checks (use `.Equals()`, not `==`).

**When to Use**: This is your go-to method when you have a type name string and need the corresponding `SemanticType`.

#### `GetBuiltinType(string name) -> SemanticType?`

**Purpose**: Explicitly query for a builtin type only, ignoring user-defined types.

```csharp
public SemanticType? GetBuiltinType(string name)
{
    return _builtinTypes.TryGetValue(name, out var builtin) ? builtin : null;
}
```

**When to Use**: 
- When you specifically need to distinguish builtins from user types
- For validation logic that only accepts primitive types
- When implementing type coercion rules (e.g., numeric widening)

---

### Type Registration

#### `RegisterType(TypeSymbol typeSymbol)`

**Purpose**: Registers a user-defined type (class, interface, etc.) with the registry.

```csharp
public void RegisterType(TypeSymbol typeSymbol)
{
    _userTypes[typeSymbol.Name] = typeSymbol;
}
```

**Called by**: `NameResolver` during the first pass of semantic analysis when it encounters class/interface definitions.

**Usage Example**:
```csharp
// In NameResolver when visiting a ClassDef AST node
var typeSymbol = new TypeSymbol 
{ 
    Name = classDef.Name, 
    Kind = SymbolKind.Type,
    TypeKind = TypeKind.Class 
};
_typeRegistry.RegisterType(typeSymbol);
```

**Note**: This method **overwrites** existing entries. In a well-formed program, each type name should be unique within a scope, so duplicate registration typically indicates a semantic error (name collision).

---

### Type Query Methods

#### `IsBuiltinType(string name) -> bool`

**Purpose**: Fast check if a name refers to a builtin type.

```csharp
public bool IsBuiltinType(string name) => _builtinTypes.ContainsKey(name);
```

**Use Case**: Validation and error reporting (e.g., "cannot extend builtin type 'int'").

#### `IsUserDefinedType(string name) -> bool`

**Purpose**: Fast check if a name refers to a user-defined type.

```csharp
public bool IsUserDefinedType(string name) => _userTypes.ContainsKey(name);
```

**Use Case**: Distinguishing between primitive and custom types in codegen or type checking logic.

#### `GetUserDefinedTypes() -> IEnumerable<TypeSymbol>`

**Purpose**: Returns all registered user-defined types.

```csharp
public IEnumerable<TypeSymbol> GetUserDefinedTypes() => _userTypes.Values;
```

**Use Case**: 
- Code generation (emitting all class definitions)
- Analysis passes that need to visit all types
- IDE tooling (listing available types)

#### `GetBuiltinTypes() -> IEnumerable<SemanticType>`

**Purpose**: Returns all builtin types (deduplicated).

```csharp
public IEnumerable<SemanticType> GetBuiltinTypes() => _builtinTypes.Values.Distinct();
```

**Note**: Uses `.Distinct()` because multiple names map to the same `SemanticType` instance (e.g., `int` and `int32`).

---

### Type Utility Methods

#### `AreEqual(SemanticType a, SemanticType b) -> bool`

**Purpose**: Structural equality check for types.

```csharp
public static bool AreEqual(SemanticType a, SemanticType b) => a.Equals(b);
```

**Why Static?**: This is a pure function that doesn't need registry state. Making it static allows usage in contexts where you have types but no registry instance.

**Design Note**: While this seems trivial (just delegates to `.Equals()`), it provides:
1. A semantic API ("`AreEqual`" reads better than `.Equals()` in type-checking code)
2. A centralized place to add more sophisticated equality logic if needed (e.g., considering type equivalence rules)

#### `MakeNullable(SemanticType type) -> SemanticType`

**Purpose**: Wraps a type in a `NullableType`, making it nullable (e.g., `int` → `int?`).

```csharp
public static SemanticType MakeNullable(SemanticType type)
{
    if (type is NullableType)
        return type;  // Already nullable, don't double-wrap
    return new NullableType { UnderlyingType = type };
}
```

**Idempotency**: Calling `MakeNullable` on an already-nullable type returns it unchanged. This prevents `int??` scenarios.

**When to Use**:
- Type inference with `None` literals (e.g., `x = None` → infer `T?`)
- Optional parameters (e.g., `def foo(x: int = None)` → `x` is `int?`)
- Nullable annotations (e.g., `x: int?`)

**Example**:
```csharp
var intType = SemanticType.Int;
var nullableInt = TypeRegistry.MakeNullable(intType);  // int?
var stillNullable = TypeRegistry.MakeNullable(nullableInt);  // Still int?, not int??
```

#### `UnwrapNullable(SemanticType type) -> SemanticType`

**Purpose**: Extracts the underlying type from a nullable type. Returns the type unchanged if not nullable.

```csharp
public static SemanticType UnwrapNullable(SemanticType type)
{
    if (type is NullableType nullable)
        return nullable.UnderlyingType;
    return type;
}
```

**When to Use**:
- Type narrowing after `None` checks (e.g., `if x is not None:` → unwrap `x?` to `x`)
- Type comparison ignoring nullability
- Code generation where you need the base type

**Example**:
```csharp
var nullableInt = new NullableType { UnderlyingType = SemanticType.Int };
var unwrapped = TypeRegistry.UnwrapNullable(nullableInt);  // SemanticType.Int
var unchanged = TypeRegistry.UnwrapNullable(SemanticType.Str);  // SemanticType.Str
```

**Note**: Only unwraps one level. For recursive unwrapping, see `TypeUtils.UnwrapAllNullable()`.

---

## Dependencies

### Upstream Dependencies

| Dependency | Purpose |
|------------|---------|
| `SymbolTable` | Provides lookup for user-defined type symbols |
| `SemanticType` | The type system representation (return values) |
| `TypeSymbol` | Symbol table entries for type declarations |

### Downstream Consumers

`TypeRegistry` is expected to be used by:

| Component | Usage |
|-----------|-------|
| `TypeResolver` | Resolving type annotations to `SemanticType` instances |
| `TypeChecker` | Validating type compatibility, type inference |
| `NameResolver` | Registering newly declared types |
| `CodeGenInfo` | Mapping Sharpy types to C# types |

**Current Status**: As of the latest commit, `TypeRegistry` was recently added but may not be fully integrated yet. Check for usage with `git grep "TypeRegistry"`.

---

## Patterns and Design Decisions

### 1. **Two-Tier Type Lookup**

The registry distinguishes between:
- **Builtin types**: Fast, in-memory dictionary (`_builtinTypes`)
- **User-defined types**: Delegated to `SymbolTable` for scope-aware lookup

**Rationale**: Builtins are global and immutable, so they're cached. User types respect scoping rules and are looked up dynamically.

### 2. **Singleton SemanticType Instances**

Builtin types use singleton instances (`SemanticType.Int`, etc.) defined in `SemanticType.cs`.

**Advantages**:
- **Reference equality**: `type1 == type2` works for builtins
- **Memory efficiency**: No redundant type objects
- **Performance**: Type comparison is pointer comparison

**Trade-off**: User-defined types create new `UserDefinedType` instances on each `GetType()` call. This is acceptable because:
- User types need to track their `TypeSymbol` reference
- Structural equality via `.Equals()` works correctly
- The number of lookups is typically small

### 3. **Static Utility Methods**

Methods like `MakeNullable`, `UnwrapNullable`, and `AreEqual` are static because:
- They're **pure functions** (no side effects)
- They don't need registry state
- They can be used without a registry instance (e.g., in `TypeUtils`)

This pattern is similar to utility classes in Java/C# (e.g., `Math.Max()`, `String.IsNullOrEmpty()`).

### 4. **Immutable Types**

`SemanticType` is an immutable record. Operations like `MakeNullable` return **new instances** rather than modifying existing ones.

**Benefits**:
- **Thread-safe**: No locking needed
- **Safe sharing**: Types can be cached and reused
- **Predictable**: No spooky action at a distance

### 5. **Null Safety**

Methods return `SemanticType?` (nullable) to force callers to handle the "type not found" case:

```csharp
var type = _typeRegistry.GetType("MyClass");
if (type == null)
{
    _errors.Add(new SemanticError($"Undefined type: MyClass"));
    return SemanticType.Unknown;
}
```

This is better than returning `SemanticType.Unknown` by default, which would mask errors.

---

## Debugging Tips

### Problem: Type Not Found

**Symptom**: `GetType("MyClass")` returns `null`.

**Debugging Steps**:
1. **Check if it's a builtin**: Call `IsBuiltinType("MyClass")` - maybe there's a typo
2. **Check registration**: Was `RegisterType()` called for this type? Set a breakpoint in `RegisterType()`
3. **Check symbol table**: Use `_symbolTable.Lookup("MyClass")` - is the symbol there?
4. **Check scope**: Types are scoped - are you looking in the right scope?
5. **Check order**: Is type resolution happening before type registration? (Name resolution should happen first)

### Problem: Wrong Type Returned

**Symptom**: Getting `SemanticType.Int` when expecting a user-defined type named "int".

**Cause**: Builtins are checked first. If a user type shadows a builtin name, the builtin wins.

**Solution**: 
- Rename the user type (recommended)
- Or use `IsBuiltinType()` to detect and report the conflict

### Problem: Type Equality Fails

**Symptom**: `type1.Equals(type2)` returns false for seemingly identical types.

**Debugging**:
```csharp
Console.WriteLine($"Type 1: {type1.GetType().Name} - {type1.GetDisplayName()}");
Console.WriteLine($"Type 2: {type2.GetType().Name} - {type2.GetDisplayName()}");
```

**Common Issues**:
- One is `NullableType`, the other isn't (use `UnwrapNullable` first)
- Generic types with different type arguments (`list[int]` vs `list[str]`)
- User-defined types with different `TypeSymbol` references

### Inspecting Registry State

Add this debug helper:

```csharp
public void DumpTypes()
{
    Console.WriteLine("=== Builtin Types ===");
    foreach (var (name, type) in _builtinTypes)
        Console.WriteLine($"  {name} -> {type.GetDisplayName()}");

    Console.WriteLine("\n=== User Types ===");
    foreach (var (name, symbol) in _userTypes)
        Console.WriteLine($"  {name} -> {symbol.TypeKind}");
}
```

Call this after `NameResolver` to verify all types are registered correctly.

---

## Contribution Guidelines

### When to Modify TypeRegistry

**Add a builtin type**:
1. Add the constant to `SemanticType.cs` (e.g., `public static readonly SemanticType Decimal = ...`)
2. Register it in `InitializeBuiltinTypes()`
3. Add test coverage in `TypeRegistryTests.cs`

**Change type lookup logic**:
- Be extremely careful - this affects the entire compiler
- Consider backward compatibility
- Add extensive tests (unit + integration)
- Document the behavior change

**Add new utility methods**:
- Keep them static if they don't need registry state
- Name them clearly (e.g., `IsGeneric`, `GetTypeArguments`)
- Add XML doc comments
- Consider adding to `TypeUtils.cs` instead if it's a complex query

### Testing Checklist

When modifying `TypeRegistry`:

- [ ] Unit test for builtin type lookup
- [ ] Unit test for user-defined type lookup
- [ ] Unit test for type not found (returns null)
- [ ] Test nullable wrapping/unwrapping
- [ ] Test type equality
- [ ] Integration test: full compilation with type resolution
- [ ] Test edge cases (empty string, `null`, duplicate names)

### Code Style

Follow existing patterns:
- **Terse property names**: `_builtinTypes`, not `_builtinTypeDictionary`
- **Null-safe returns**: Return `Type?`, not `Type` with `Unknown` fallback
- **Static when possible**: If it doesn't need state, make it static
- **XML comments**: Document public APIs with `<summary>`, `<param>`, etc.

---

## Cross-References

### Related Files

| File | Relationship |
|------|-------------|
| [`SemanticType.cs`](./SemanticType.md) | Defines the type system hierarchy (`SemanticType`, `BuiltinType`, `UserDefinedType`, `NullableType`, etc.) |
| [`Symbol.cs`](./Symbol.md) | Defines `TypeSymbol` and other symbol types |
| [`SymbolTable.cs`](./SymbolTable.md) | Manages scoped symbol lookups (used by `TypeRegistry`) |
| [`TypeUtils.cs`](./TypeUtils.md) | Additional type utility functions (companion to `TypeRegistry`) |
| [`TypeResolver.cs`](./TypeResolver.md) | Uses `TypeRegistry` to resolve type annotations |
| [`TypeChecker.cs`](./TypeChecker.md) | Uses `TypeRegistry` for type validation and inference |
| [`NameResolver.cs`](./NameResolver.md) | Calls `RegisterType()` during declaration pass |

### Specification Documents

- [`docs/language_specification/type_annotations.md`](../../../language_specification/type_annotations.md) - Sharpy type annotation syntax
- [`docs/language_specification/type_hierarchy.md`](../../../language_specification/type_hierarchy.md) - Type system design and relationships

### Architecture Context

```
┌─────────────────────────────────────────────┐
│  Semantic Analysis Pipeline                 │
├─────────────────────────────────────────────┤
│                                             │
│  NameResolver (Pass 1)                      │
│    ├─> Discovers type declarations         │
│    └─> Calls TypeRegistry.RegisterType()   │
│                                             │
│  TypeResolver (Pass 2)                      │
│    ├─> Resolves type annotations           │
│    └─> Calls TypeRegistry.GetType()        │
│                                             │
│  TypeChecker (Pass 3)                       │
│    ├─> Validates type usage                │
│    ├─> Infers types                         │
│    └─> Uses TypeRegistry utilities          │
│                                             │
└─────────────────────────────────────────────┘
        │
        ├─> TypeRegistry (Centralized Registry)
        │     ├─> _builtinTypes (Dictionary)
        │     ├─> _userTypes (Dictionary)
        │     └─> _symbolTable (Reference)
        │
        └─> SemanticType (Type Instances)
              ├─> BuiltinType
              ├─> UserDefinedType
              ├─> NullableType
              ├─> GenericType
              └─> ...
```

---

## Future Enhancements

Based on `SemanticType.cs` comments and Sharpy roadmap:

### v0.2.x - Type System Extensions

**Union Types**:
```csharp
// Future: Support for tagged unions / ADTs
public SemanticType CreateUnionType(params SemanticType[] types)
{
    return new UnionType { Members = types.ToList() };
}
```

**Task Types**:
```csharp
// Future: Support for async/await
public SemanticType CreateTaskType(SemanticType resultType)
{
    return new TaskType { ResultType = resultType };
}
```

### Performance Optimizations

**Type Interning**:
- Currently, `UserDefinedType` instances are created fresh on each lookup
- Future: Intern user-defined types to reduce allocations

**Lazy Builtin Registration**:
- Currently, all builtins are registered upfront
- Future: Lazy load uncommon types (e.g., `decimal`, unsigned integers)

### Enhanced Queries

**Type Hierarchy Queries**:
```csharp
// Future: Check inheritance relationships
public bool IsDerivedFrom(SemanticType derived, SemanticType baseType);

// Future: Get all subtypes of a type
public IEnumerable<SemanticType> GetDerivedTypes(SemanticType baseType);
```

---

## Summary

`TypeRegistry` is the **central authority** for type management in the Sharpy compiler's semantic analysis phase. It:

1. **Unifies type access** - One place to ask "what is type X?"
2. **Separates builtin from user types** - Different storage and lookup strategies
3. **Provides type utilities** - Nullable wrapping, equality, unwrapping
4. **Integrates with the symbol table** - Leverages existing scope/symbol infrastructure

**Key Principle**: Types are **looked up**, not stored in AST nodes. This keeps the AST clean and makes type information available during all semantic passes.

**When You'll Use It**:
- Implementing new type checking rules → `GetType()` to resolve type names
- Adding new language features → `RegisterType()` for new syntactic forms
- Debugging type errors → Inspect registry state to see what types are known

**Golden Rule**: If you're dealing with type names (strings), use `TypeRegistry`. If you're dealing with type instances (`SemanticType`), use `TypeUtils`.
