# Walkthrough: TypeMapper.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

---

## Overview

`TypeMapper` is the **type translation bridge** in the Sharpy compiler's code generation phase. It converts Sharpy type representations into C# Roslyn `TypeSyntax` nodes, enabling the final emission of valid C# code. Think of it as a sophisticated dictionary lookup combined with structural transformation—it knows how `list[int]` becomes `global::Sharpy.Core.List<int>` and how `float` becomes C# `double`.

### Key Responsibilities

1. **Primitive Type Mapping**: `int` → C# `int`, `float` → C# `double`, `str` → C# `string`
2. **Collection Type Mapping**: `list[T]` → `global::Sharpy.Core.List<T>`, `dict[K,V]` → `global::Sharpy.Core.Dict<K,V>`
3. **Generic Type Translation**: Handle type parameters and type arguments
4. **Nullable Types**: `int?` → C# `int?`
5. **Function Types**: Map to C# `Func<>`/`Action<>` delegates
6. **Tuple Types**: Convert to C# `ValueTuple<>`
7. **Type Inference**: Infer types from literal expressions when annotations are missing
8. **Module-Qualified Types**: Handle cross-file and cross-module type references

### Pipeline Position

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → CodeGen (TypeMapper) → C#
                                ↓                              ↓
                         TypeAnnotation                  TypeSyntax
                         SemanticType                  (Roslyn nodes)
```

**Upstream**: Receives `TypeAnnotation` (from AST) and `SemanticType` (from semantic analysis)  
**Downstream**: Produces `TypeSyntax` consumed by `RoslynEmitter` to generate C# code

---

## Class/Type Structure

### Main Class: `TypeMapper`

```csharp
public class TypeMapper
{
    private readonly CodeGenContext _context;
    private static readonly Dictionary<string, string> _builtinTypeMap;
    
    public TypeMapper(CodeGenContext context) { ... }
}
```

**Design Characteristics:**
- **Instance-based**: Each mapper is tied to a specific `CodeGenContext` for accessing:
  - `SymbolTable` (for type lookups)
  - `BuiltinRegistry` (for builtin type checks)
  - `SourceFilePath` (for cross-file reference resolution)
  - `ProjectNamespace` (for multi-file compilation namespacing)
- **Static Type Catalog**: `_builtinTypeMap` is populated once at startup from `PrimitiveCatalog`
- **Stateless**: All public methods are pure transformations—no side effects

### Built-in Type Map

The static constructor initializes `_builtinTypeMap` with two categories:

1. **Primitives** (from `PrimitiveCatalog`):
   - `int` → `int`
   - `long` → `long`
   - `float` → `double` (Sharpy's 64-bit float)
   - `float32` → `float` (C# 32-bit)
   - `bool` → `bool`
   - `str` → `string`

2. **Runtime Collections**:
   - `list` → `global::Sharpy.Core.List`
   - `dict` → `global::Sharpy.Core.Dict`
   - `set` → `global::Sharpy.Core.Set`
   - `tuple` → `System.ValueTuple`

**Why `global::`?** Prevents name collisions when the output namespace contains "Sharpy" (e.g., a user creates `namespace Sharpy.MyApp`).

---

## Key Functions/Methods

### 1. `MapSemanticType(SemanticType type)` → `TypeSyntax`

**Purpose**: The primary workhorse for translating semantic analysis types to Roslyn syntax.

**When Called**: After type checking, when the compiler has fully resolved types and needs to generate C# code.

**Logic Flow**:
```csharp
return type switch
{
    null or UnknownType => object,
    VoidType => void,
    BuiltinType => (match singletons or lookup in map),
    GenericType => MapGenericSemanticType(...),
    NullableType => NullableType(MapSemanticType(underlying)),
    UserDefinedType => ParseTypeName(GetMappedTypeNameFromSymbol(...)),
    TypeParameterType => IdentifierName(typeParam.Name),
    FunctionType => MapSemanticFunctionType(...),
    TupleType => MapSemanticTupleType(...),
    _ => object
};
```

**Special Cases**:
- **Builtin Singletons**: Uses reference equality checks (`type == SemanticType.Int`) for performance
- **Unknown Types**: Falls back to `object` (defensive coding)
- **Type Parameters**: Directly emits the generic parameter name (e.g., `T` in `class Box[T]`)

**Example**:
```csharp
// SemanticType: GenericType { Name = "list", TypeArguments = [BuiltinType("int")] }
var result = mapper.MapSemanticType(listIntType);
// Result: global::Sharpy.Core.List<int>
```

---

### 2. `MapType(TypeAnnotation? type)` → `TypeSyntax`

**Purpose**: Translates AST type annotations (before full semantic analysis) to C# types.

**When Called**: During code generation when working directly with AST nodes that may not have semantic type info.

**Key Features**:
- **Type Alias Expansion**: Looks up type aliases in the symbol table and recursively expands them
- **Nullable Handling**: Applies `?` modifier at the usage site
- **Generic Type Arguments**: Recursively maps each type argument
- **Default to `object`**: If no annotation exists

**Algorithm**:
```csharp
1. If type == null → return object
2. Check for type alias → expand and recursively map
3. If generic → map base type + type arguments
4. If nullable → wrap in NullableType(...)
5. Return ParseTypeName(mapped name)
```

**Example**:
```python
# Sharpy code
type IntList = list[int]
x: IntList = [1, 2, 3]
```

```csharp
// MapType(IntList annotation) resolves:
// 1. Lookup IntList → finds alias to list[int]
// 2. Expand → list[int]
// 3. Map → global::Sharpy.Core.List<int>
```

---

### 3. `GetMappedTypeName(string sharpyTypeName)` → `string`

**Purpose**: Core name resolution logic—converts Sharpy type names to C# type names.

**Resolution Order**:
1. **Built-in types**: Check `_builtinTypeMap` (fast path)
2. **Registry builtins**: Check `_context.IsBuiltinType()` → return `Sharpy.{name}`
3. **Cross-file/module types**: Look up in symbol table, check `DefiningFilePath`/`DefiningModule`
4. **Current file types**: Apply `NameMangler.ToPascalCase()` (snake_case → PascalCase)

**Example**:
```csharp
GetMappedTypeName("int")          // → "int"
GetMappedTypeName("list")         // → "global::Sharpy.Core.List"
GetMappedTypeName("my_class")     // → "MyClass" (name mangling)
GetMappedTypeName("SomeClass")    // → "SomeClass" (already PascalCase)
```

---

### 4. `GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)` → `string`

**Purpose**: Constructs fully qualified C# names for types from other modules or files.

**Namespace Construction**:
```csharp
// For imported modules:
"{ProjectNamespace}.{ModuleNamespace}.Exports.{TypeName}"

// Example:
// import animal
// class Dog: animal.Animal → "MyProject.Animal.Exports.Dog"
```

**Algorithm**:
1. Determine module namespace from `typeSymbol.DefiningModule` or derive from `DefiningFilePath`
2. Convert module path to namespace (e.g., `"lib.animal"` → `"Lib.Animal"`)
3. Mangle type name to PascalCase
4. Build qualified name with project namespace prefix if present

**Why "Exports"?** Sharpy wraps module-level code in a static `Exports` class, so types are nested within it.

---

### 5. `MapGenericSemanticType(GenericType generic)` → `TypeSyntax`

**Purpose**: Handles generic type instantiations like `list[int]` or `dict[str, float]`.

**Implementation**:
```csharp
var baseTypeName = GetMappedTypeName(generic.Name);  // "list" → "global::Sharpy.Core.List"
var typeArgs = generic.TypeArguments
    .Select(MapSemanticType)  // Recursively map each argument
    .ToArray();

return GenericName(Identifier(baseTypeName))
    .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
```

**Result**: Produces Roslyn nodes equivalent to `List<int>` syntax tree.

---

### 6. `MapSemanticFunctionType(FunctionType funcType)` → `TypeSyntax`

**Purpose**: Converts function types to C# delegates (`Func<>` or `Action<>`).

**Decision Logic**:
```csharp
if (returnType is VoidType) {
    if (no parameters) → System.Action
    else → System.Action<T1, T2, ...>
} else {
    if (no parameters) → System.Func<TResult>
    else → System.Func<T1, T2, ..., TResult>
}
```

**Examples**:
```python
# Sharpy
callback: (int, str) -> None     # → System.Action<int, string>
processor: (int) -> str          # → System.Func<int, string>
factory: () -> int               # → System.Func<int>
```

**Why This Matters**: Enables first-class functions and lambda support.

---

### 7. `MapSemanticTupleType(TupleType tupleType)` → `TypeSyntax`

**Purpose**: Maps Sharpy tuples to C# `ValueTuple<>`.

**Edge Cases**:
- **Empty tuple**: `()` → `System.ValueTuple` (zero-element)
- **Single element**: `(int,)` → Just `int` (not a tuple in C#)
- **Multiple elements**: `(int, str)` → `System.ValueTuple<int, string>`

**Implementation**:
```csharp
if (tupleType.ElementTypes.Count == 0)
    return ParseTypeName("System.ValueTuple");

var elementTypes = tupleType.ElementTypes.Select(MapSemanticType).ToArray();

if (elementTypes.Length == 1)
    return elementTypes[0];  // Not a tuple!

return GenericName("System.ValueTuple")
    .WithTypeArgumentList(TypeArgumentList(SeparatedList(elementTypes)));
```

---

### 8. `InferElementType(IEnumerable<Expression> expressions)` → `TypeSyntax`

**Purpose**: Type inference for collection literals when no explicit annotation exists.

**Strategy**:
```csharp
1. If empty collection → object
2. Infer type of first element
3. Check if all elements have the same type
4. If yes → use that type
5. If no → fall back to object
```

**Example**:
```python
# Sharpy
numbers = [1, 2, 3]  # No type annotation
# InferElementType([1, 2, 3]) → all are IntegerLiteral → int
```

**Limitations**: Only handles simple literal inference. Complex expression type inference relies on semantic analysis.

---

### 9. `InferExpressionType(Expression expr)` → `string`

**Purpose**: Simple pattern matching on AST nodes to infer types from literals.

**Mapping**:
```csharp
IntegerLiteral → "int"
FloatLiteral (with suffix) → "float" (f), "decimal" (m), or "double" (default)
StringLiteral → "string"
BooleanLiteral → "bool"
NoneLiteral → "object"
ListLiteral → "list"
DictLiteral → "dict"
SetLiteral → "set"
TupleLiteral → "tuple"
_ → "object"
```

**Used By**: `InferElementType()` and `InferTypeFromExpression()`

---

### 10. `MapTypeFromExpression(Expression expr)` → `TypeSyntax`

**Purpose**: Handles a special case—generic type instantiation where type arguments are parsed as expressions.

**Problem**:
```python
# Sharpy
box = Box[int](42)  # "int" is parsed as Identifier expression, not TypeAnnotation!
```

**Solution**:
```csharp
if (expr is Identifier id)
    return MapType(new TypeAnnotation { Name = id.Name });

if (expr is IndexAccess indexAccess)  // Nested generics: Container[Box[int]]
    return GenericName(...)
        .WithTypeArgumentList(MapTypeArgumentsFromExpression(indexAccess.Index));
```

**Why This Exists**: Parser limitation—`Box[int]` is initially parsed as index access, not a type. This method "re-interprets" it as a type during code generation.

---

### 11. Helper Methods

#### `CreateCollectionType(string collectionName, TypeSyntax elementType)` → `TypeSyntax`
- **Purpose**: Convenience method for creating `List<T>`, `Set<T>`, etc.
- **Example**: `CreateCollectionType("list", intType)` → `global::Sharpy.Core.List<int>`

#### `CreateDictType(TypeSyntax keyType, TypeSyntax valueType)` → `TypeSyntax`
- **Purpose**: Specialized method for dictionary creation
- **Example**: `CreateDictType(stringType, intType)` → `global::Sharpy.Core.Dict<string, int>`

#### `MakeNullable(TypeSyntax type)` → `TypeSyntax`
- **Purpose**: Wraps any type in nullable syntax
- **Example**: `MakeNullable(intType)` → `int?`

#### `MakeArrayType(TypeSyntax elementType, int rank = 1)` → `TypeSyntax`
- **Purpose**: Creates C# array types (for potential future use or .NET interop)
- **Example**: `MakeArrayType(intType, 2)` → `int[,]`

---

## Dependencies

### Internal Dependencies

1. **`CodeGenContext`** (`CodeGen/CodeGenContext.cs`):
   - Provides access to `SymbolTable`, `BuiltinRegistry`, file paths, and project namespace
   - Essential for resolving type names and cross-file references

2. **`NameMangler`** (`CodeGen/NameMangler.cs`):
   - Converts `snake_case` → `PascalCase`
   - Handles C# keyword escaping (e.g., `class` → `@class`)
   - Maps dunder methods (`__str__` → `ToString`)

3. **`SemanticType`** (`Semantic/SemanticType.cs`):
   - Family of types representing resolved semantic information
   - Key subtypes: `BuiltinType`, `GenericType`, `NullableType`, `UserDefinedType`, `FunctionType`, `TupleType`

4. **`TypeAnnotation`** (`Parser/Ast/*.cs`):
   - AST representation of type syntax
   - Contains `Name`, `TypeArguments`, `IsNullable`

5. **`PrimitiveCatalog`** (`Semantic/PrimitiveCatalog.cs`):
   - Registry of primitive types with their C# names
   - Used to populate `_builtinTypeMap` at startup

### External Dependencies

- **Microsoft.CodeAnalysis.CSharp**: Roslyn API for building C# syntax trees
  - `SyntaxFactory`: Factory methods for creating syntax nodes
  - `TypeSyntax`: Base class for all type syntax nodes
  - `GenericName`, `NullableType`, `PredefinedType`, etc.

---

## Patterns and Design Decisions

### 1. **Immutable Syntax Trees**
All Roslyn syntax nodes are immutable. TypeMapper creates new nodes rather than modifying existing ones. This is thread-safe and aligns with functional programming principles.

```csharp
// Wrong (doesn't exist):
typeSyntax.IsNullable = true;

// Correct:
var nullableType = NullableType(typeSyntax);
```

### 2. **Pattern Matching for Type Dispatch**
Heavy use of C# 9's pattern matching makes the code concise and exhaustive:

```csharp
return type switch
{
    BuiltinType builtin when type == SemanticType.Int => ...,
    GenericType generic => ...,
    NullableType nullable => ...,
    _ => ...  // Defensive fallback
};
```

**Benefit**: Compile-time exhaustiveness checking when new `SemanticType` subclasses are added.

### 3. **Two-Phase Mapping**
TypeMapper supports mapping from both:
- **AST types** (`TypeAnnotation`) — early in pipeline, may have aliases
- **Semantic types** (`SemanticType`) — after full resolution, no aliases

This dual interface allows flexibility in code generation without forcing semantic analysis everywhere.

### 4. **Global Qualification for Runtime Types**
Using `global::Sharpy.Core.List` instead of just `List` or `Sharpy.Core.List` prevents namespace collisions:

```csharp
// Without global::
namespace Sharpy.MyApp {  // User's namespace
    using Sharpy.Core;    // Collision! Sharpy is ambiguous
    class MyClass { ... }
}

// With global::
namespace Sharpy.MyApp {
    class MyClass {
        global::Sharpy.Core.List<int> items;  // Unambiguous
    }
}
```

### 5. **Singleton Optimization for Primitives**
Primitive types use singleton instances (`SemanticType.Int`, `SemanticType.Str`), allowing fast reference equality checks instead of structural equality:

```csharp
// Fast path:
if (type == SemanticType.Int)  // Reference comparison
    return PredefinedType(Token(SyntaxKind.IntKeyword));

// Avoid:
if (type is BuiltinType bt && bt.Name == "int")  // Slower
```

### 6. **Recursive Descent for Complex Types**
Generic types, nullable types, and tuples are handled recursively:

```csharp
// list[dict[str, int?]]
MapSemanticType(listType)
  → MapGenericSemanticType
    → MapSemanticType(dictType)
      → MapGenericSemanticType
        → MapSemanticType(str)
        → MapSemanticType(nullable_int)
          → NullableType(MapSemanticType(int))
```

This mirrors the nested structure of type annotations naturally.

---

## Debugging Tips

### 1. **Use CLI Emit Commands**
Debug type mapping without running full compilation:

```bash
# See generated C# types
dotnet run --project src/Sharpy.Cli -- emit csharp myfile.spy

# Check AST to verify what parser produces
dotnet run --project src/Sharpy.Cli -- emit ast myfile.spy
```

### 2. **Add Logging in TypeMapper**
Insert temporary debug output:

```csharp
public TypeSyntax MapSemanticType(SemanticType type)
{
    Console.WriteLine($"[TypeMapper] Mapping: {type.GetDisplayName()}");
    var result = type switch { ... };
    Console.WriteLine($"[TypeMapper] Result: {result}");
    return result;
}
```

### 3. **Check SemanticType vs TypeAnnotation**
If a type maps incorrectly, determine which entry point is being used:
- `MapSemanticType()` → Check `TypeChecker` and `TypeResolver`
- `MapType()` → Check `Parser` AST node structure

### 4. **Verify PrimitiveCatalog**
If a primitive type isn't mapping:

```csharp
// In unit test or interactive debugger:
var primitives = PrimitiveCatalog.GetAllPrimitives();
foreach (var (name, info) in primitives) {
    Console.WriteLine($"{name} → {info.CSharpName}");
}
```

### 5. **Test with Python**
Always verify expected behavior against Python first:

```bash
python3 -c "
x: list[int] = [1, 2, 3]
print(type(x))
"
```

Then ensure Sharpy generates equivalent C# semantics.

### 6. **Inspect Roslyn Output**
Use Roslyn's built-in visualization:

```csharp
var typeSyntax = mapper.MapSemanticType(myType);
Console.WriteLine(typeSyntax.ToFullString());  // Prints C# syntax
```

### 7. **Common Issues**

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| "Type not found" error | `GetMappedTypeName()` returning wrong name | Check symbol table lookup, verify `DefiningModule` |
| Generic types missing `<>` | `MapGenericSemanticType()` not called | Check type switch in `MapSemanticType()` |
| Nullable `?` missing | `IsNullable` not propagated | Trace nullable handling through recursive calls |
| Cross-file types unresolved | Module namespace calculation wrong | Debug `GetFullyQualifiedTypeName()` |
| Keywords not escaped | Name mangling not applied | Check if type goes through `NameMangler` |

---

## Contribution Guidelines

### When to Modify TypeMapper

1. **Adding New Primitive Types**: Update `PrimitiveCatalog`, not TypeMapper (auto-populates)
2. **New Collection Types**: Add to `_builtinTypeMap` static initializer
3. **New Semantic Type Classes**: Add case to `MapSemanticType()` pattern match
4. **Fixing Type Resolution**: Usually fix `GetMappedTypeName()` or `GetFullyQualifiedTypeName()`
5. **Interop with .NET Types**: Potentially add special cases or expand builtin map

### What NOT to Change

- **Don't bypass Roslyn**: Always use `SyntaxFactory` methods, never string concatenation
- **Don't perform type checking**: That's `TypeChecker`'s job. Assume types are valid.
- **Don't store state**: Keep methods pure. Use `CodeGenContext` for lookups, not mutations.
- **Don't hardcode namespaces**: Use `_context.ProjectNamespace` for multi-file projects

### Testing Changes

1. **Unit Tests**: Add to `Sharpy.Compiler.Tests/CodeGen/TypeMapperTests.cs`
2. **Integration Tests**: Add file-based test to `TestFixtures/` if it affects end-to-end output
3. **Python Verification**: Always verify expected behavior with Python interpreter first

**Example Test**:
```csharp
[Fact]
public void MapSemanticType_ListOfInt_ReturnsGlobalSharpyCoreList()
{
    var context = new CodeGenContext(...);
    var mapper = new TypeMapper(context);
    var listIntType = new GenericType 
    { 
        Name = "list", 
        TypeArguments = [SemanticType.Int] 
    };
    
    var result = mapper.MapSemanticType(listIntType);
    
    Assert.Equal("global::Sharpy.Core.List<int>", result.ToString());
}
```

### Code Style Guidelines

- **Use pattern matching**: Prefer `switch` expressions over `if`/`else` chains
- **Recursive methods**: Keep single responsibility—one method per concern
- **Naming**: `Map*` for transformations, `Get*` for lookups, `Create*` for factories
- **Comments**: Explain *why* (design decisions), not *what* (code is self-documenting)

---

## Cross-References

### Related Source Files

1. **[CodeGenContext.md](./CodeGenContext.md)**: Understanding the context object TypeMapper depends on
2. **[NameMangler.md](./NameMangler.md)**: How Sharpy names are converted to C# naming conventions
3. **[RoslynEmitter.md](./RoslynEmitter.md)**: How TypeMapper's output is used to emit full C# code
4. **[../Semantic/SemanticType.md](../Semantic/SemanticType.md)**: The type system that TypeMapper translates from
5. **[../Semantic/TypeChecker.md](../Semantic/TypeChecker.md)**: How semantic types are resolved before mapping
6. **[../Parser/Ast/TypeAnnotation.md](../Parser/Ast/TypeAnnotation.md)**: AST representation of types

### Related Specifications

- **`docs/language_specification/type_annotations.md`**: Defines Sharpy's type annotation syntax
- **`docs/language_specification/type_casting.md`**: Type conversion rules
- **`docs/language_specification/type_hierarchy.md`**: Type system structure
- **`docs/language_specification/generics.md`**: Generic type semantics

### Related Partial Classes

TypeMapper is a standalone class (not split across files), but it heavily interacts with:
- **RoslynEmitter** partial classes in `CodeGen/`:
  - `RoslynEmitter.Expressions.cs` — Uses TypeMapper for expression typing
  - `RoslynEmitter.TypeDeclarations.cs` — Uses TypeMapper for class/method signatures
  - `RoslynEmitter.Statements.cs` — Uses TypeMapper for variable declarations

---

## Appendix: Complete Type Mapping Reference

### Primitive Type Mappings

| Sharpy Type | C# Type | Notes |
|-------------|---------|-------|
| `int` | `int` | 32-bit signed integer |
| `long` | `long` | 64-bit signed integer |
| `float` | `double` | Sharpy's default float is 64-bit |
| `float32` | `float` | Explicit 32-bit float |
| `double` | `double` | Same as `float` |
| `bool` | `bool` | Boolean |
| `str` | `string` | String |
| `None` | `void` | In return types only |

### Collection Type Mappings

| Sharpy Type | C# Type | Runtime Implementation |
|-------------|---------|------------------------|
| `list[T]` | `global::Sharpy.Core.List<T>` | Wraps `List<T>` |
| `dict[K,V]` | `global::Sharpy.Core.Dict<K,V>` | Wraps `Dictionary<K,V>` |
| `set[T]` | `global::Sharpy.Core.Set<T>` | Wraps `HashSet<T>` |
| `tuple[T1, T2, ...]` | `System.ValueTuple<T1, T2, ...>` | .NET value type |

### Function Type Mappings

| Sharpy Type | C# Type | Example |
|-------------|---------|---------|
| `() -> None` | `System.Action` | No-op callback |
| `(T1, T2) -> None` | `System.Action<T1, T2>` | Void function |
| `() -> T` | `System.Func<T>` | Factory function |
| `(T1, T2) -> TResult` | `System.Func<T1, T2, TResult>` | Transformation |

### Special Cases

| Sharpy Type | C# Type | Notes |
|-------------|---------|-------|
| `int?` | `int?` | Nullable value type |
| `str?` | `string?` | Nullable reference type |
| `T` (type parameter) | `T` | Generic type parameter |
| `object` | `object` | Fallback for unknown types |

---

## Summary

`TypeMapper` is the **Rosetta Stone** of the Sharpy compiler's code generation phase. It bridges two type systems—Sharpy's Pythonic types and C#'s strongly-typed system—with precision and flexibility. By understanding this file, you gain insight into:

1. How Sharpy maintains Python's feel while generating idiomatic C#
2. The challenges of multi-language type system translation
3. The importance of namespacing and qualification in code generation
4. How generic programming works across language boundaries

When debugging type-related issues in generated code, TypeMapper is your first stop. When adding new language features that involve types, TypeMapper is where you'll implement the translation logic. Master this file, and you'll understand the heart of how Sharpy becomes C#.

---

**Next Steps**: Read `RoslynEmitter.md` to see how TypeMapper's output is integrated into complete C# compilation units.
