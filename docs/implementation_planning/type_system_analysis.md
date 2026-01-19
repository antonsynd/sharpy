# Type System Analysis

**Created:** 2026-01-19
**Purpose:** Document current type relationships before making improvements

## Current Type Representations

```
TypeAnnotation (AST - Parser)
    ├── SimpleTypeAnnotation ("int", "str", "MyClass")
    ├── GenericTypeAnnotation ("list[int]", "dict[K,V]")
    ├── NullableTypeAnnotation ("int?")
    └── FunctionTypeAnnotation ("(int) -> str")
           ↓ resolved by TypeResolver
SemanticType (Semantic Analysis)
    ├── BuiltinType (int, str, bool, float)
    │      └── ClrType: System.Type
    ├── UserDefinedType (classes, structs)
    │      └── Symbol: TypeSymbol
    ├── GenericType (list[int])
    │      ├── TypeArguments: List<SemanticType>
    │      └── GenericDefinition: TypeSymbol
    ├── NullableType (T?)
    │      └── UnderlyingType: SemanticType
    ├── FunctionType ((int) -> str)
    ├── TupleType ((int, str))
    ├── TypeParameterType (T in generic context)
    └── ... others
           ↓ backs declaration
TypeSymbol (Symbol Table)
    ├── Name, Kind (Class/Struct/Interface/Enum)
    ├── ClrType: System.Type (for interop)
    ├── Fields: List<VariableSymbol>
    ├── Methods: List<FunctionSymbol>
    ├── BaseType: TypeSymbol
    └── Interfaces: List<TypeSymbol>
```

## Pain Points

### 1. Dual Type References
- `UserDefinedType.Symbol` points to `TypeSymbol`
- `GenericType.GenericDefinition` also points to `TypeSymbol`
- Inconsistent naming and access patterns

### 2. CLR Type Duplication
- `BuiltinType.ClrType` stores the .NET type directly
- `TypeSymbol.ClrType` also stores CLR type for user types
- No single source of truth for "what CLR type does this map to"

### 3. Type vs Type Usage Confusion
- `SemanticType` represents "type usage" (e.g., `int?` in a variable declaration)
- `TypeSymbol` represents "type declaration" (e.g., `class MyClass`)
- This distinction is not clearly documented

### 4. Symbol Type Reference
- `Symbol.Type` is `SemanticType`
- But types themselves are represented by `TypeSymbol`
- Example: A variable's type is `SemanticType`, but we look up type info via `TypeSymbol`

## Current File Locations

- `TypeAnnotation`: `src/Sharpy.Compiler/Parser/Ast/TypeAnnotations.cs`
- `SemanticType`: `src/Sharpy.Compiler/Semantic/SemanticType.cs`
- `TypeSymbol`: `src/Sharpy.Compiler/Semantic/Symbol.cs`
- `TypeResolver`: `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

## Recommendations

### Approach: Two-Way Door
Instead of replacing `SemanticType` entirely, we will:
1. Add `ITypeInfo` interface for common operations
2. Add `TypeRegistry` for centralized type lookup
3. Add `TypeUtils` for common type operations
4. Add documentation for type system invariants

### Why Not Full Replacement?
- Risk: Large-scale changes could introduce subtle bugs
- Time: Full replacement would take significant effort
- Value: Current system works correctly, just lacks clarity
- Future: Can incrementally improve in v0.2.x

## Future Considerations (v0.2.x)

### Tagged Unions
- Need `UnionType : SemanticType`
- `TypeSymbol.TypeKind.Union`
- Case types as nested TypeSymbols

### Async/Await
- Need `TaskType : SemanticType` for `Task<T>` wrapping
- Async functions return `TaskType`

### Pattern Matching
- Types need to support exhaustiveness checking
- Union cases need to be enumerable
