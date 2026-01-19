# Sharpy Type System Design

## Overview

The Sharpy compiler uses three complementary type representations:

1. **TypeAnnotation** (AST) - Syntax-level type expressions
2. **SemanticType** - Resolved types for type checking
3. **TypeSymbol** - Type declarations with members

## Type Representation Diagram

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

## When to Use Each Type Representation

| Scenario | Use |
|----------|-----|
| Parsing type syntax | TypeAnnotation |
| Type checking expressions | SemanticType |
| Looking up type members | TypeSymbol |
| Checking assignability | SemanticType.IsAssignableTo() |
| Code generation | SemanticType + CodeGenInfo |

## Design Invariants

### SemanticType Invariants

1. **Immutability**: SemanticType instances are immutable. Once created, they never change.
2. **Type Usage vs Declaration**: SemanticType represents TYPE USAGE, not TYPE DECLARATION. For declarations, use TypeSymbol.
3. **Symbol References**: UserDefinedType always references its declaring TypeSymbol via the `Symbol` property.
4. **Generic Arguments**: GenericType contains resolved type arguments, not parameters.

### TypeSymbol Invariants

1. **Declaration Only**: TypeSymbol represents type declarations (classes, structs, interfaces).
2. **Member Ownership**: TypeSymbol owns its members (Fields, Methods, Properties).
3. **Inheritance Chain**: BaseType and Interfaces form the inheritance hierarchy.

### TypeAnnotation Invariants

1. **Syntax Only**: TypeAnnotation is pure syntax with no semantic meaning.
2. **Resolution Required**: TypeAnnotation must be resolved to SemanticType before type checking.

## Key Type Relationships

### ITypeInfo Interface

All types implement `ITypeInfo`, providing a unified view:

```csharp
public interface ITypeInfo
{
    string DisplayName { get; }
    bool IsNullable { get; }
    bool IsValueType { get; }
    Type? ClrType { get; }
    TypeSymbol? DeclaringSymbol { get; }
    bool IsAssignableTo(ITypeInfo other);
    ITypeInfo MakeNullable();
    ITypeInfo UnwrapNullable();
}
```

### TypeRegistry

`TypeRegistry` provides centralized type lookup:

- Registers builtin types (int, str, bool, etc.)
- Caches user-defined type lookups
- Single point of truth for "is this a valid type name?"

### TypeUtils

`TypeUtils` provides common type operations:

- `IsNumeric(type)` - Check if type is numeric
- `IsInteger(type)` - Check if type is an integer type
- `IsFloatingPoint(type)` - Check if type is a floating-point type
- `IsString(type)` - Check if type is a string
- `IsCollection(type)` - Check if type is a collection (list, dict, set)
- `GetElementType(type)` - Get element type of a collection
- `GetKeyType(type)` - Get key type of a dict
- `UnwrapAllNullable(type)` - Unwrap all nullable wrappers
- `AreEquivalent(a, b)` - Check structural equivalence
- `GetCommonType(a, b)` - Find common type for numeric promotion

## Future Considerations

### Tagged Unions (v0.2.x)

- Will add `UnionType : SemanticType`
- `TypeSymbol.TypeKind.Union`
- Case types as nested TypeSymbols

Example:
```sharpy
type Result[T, E]:
    Ok(value: T)
    Err(error: E)
```

### Async/Await (v0.2.x)

- `TaskType : SemanticType` for `Task<T>` wrapping
- Async functions return `TaskType`

Example:
```sharpy
async def fetch_data() -> str:
    await http_get("https://example.com")
```

## Migration Notes

### From Legacy to CodeGenInfo

The emitter previously tracked type information in several legacy fields:
- `_variableVersions` - Variable name versioning
- `_moduleVariables` - Module-level variable names
- `_moduleConstVariables` - Module-level constant names
- `_constVariables` - Local constant names
- `_variablesWithExecutionOrderIssues` - Execution order detection

These are being migrated to use `Symbol.CodeGenInfo`, which provides:
- Pre-computed C# names
- Version information
- Module-level vs local detection
- Execution order issue flagging

See `TASK_precompute_codegen_info_COMPLETED.md` for migration status.
