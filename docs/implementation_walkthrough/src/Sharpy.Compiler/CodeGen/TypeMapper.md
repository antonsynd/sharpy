# Walkthrough: TypeMapper.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

---

## 1. Overview

### What Does This File Do?

`TypeMapper.cs` is the **type translation bridge** between Sharpy's type system and C#'s type system. When the Sharpy compiler generates C# code, it needs to convert every type annotation into the equivalent C# type syntax:

- **Sharpy side**: `int`, `str`, `list[int]`, `dict[str, int]`, `Optional[str]`
- **C# side**: `int`, `string`, `global::Sharpy.Core.List<int>`, `global::Sharpy.Core.Dict<string, int>`, `string?`

This file is critical because:
1. It maps primitive types (int, str, bool) to C# equivalents
2. It handles generic types with type arguments (list[T], dict[K,V])
3. It resolves user-defined types and generates fully-qualified names for cross-file references
4. It translates function types to C# delegates (Func/Action)
5. It handles nullable types and tuple types
6. It infers types from expressions when type annotations are missing

### Role in the Compiler Pipeline

```
Sharpy Source → Lexer → Parser (AST) → Semantic Analysis → CodeGen
                                             ↓                  ↓
                                        SemanticType      TypeAnnotation
                                             ↓                  ↓
                                             └─── TypeMapper ← You are here
                                                      ↓
                                               RoslynEmitter → C# Code
```

When `RoslynEmitter` generates C# syntax trees, it uses `TypeMapper` to convert both:
- **`TypeAnnotation`** objects from the AST (parsed type annotations in source code)
- **`SemanticType`** objects from semantic analysis (resolved, validated types)

---

## 2. Class/Type Structure

### `TypeMapper` (Instance Class)

Unlike `NameMangler` (which is static), `TypeMapper` is an **instance-based** class because it needs access to contextual information:

```csharp
public class TypeMapper
{
    private readonly CodeGenContext _context;
    private static readonly Dictionary<string, string> _builtinTypeMap;

    public TypeMapper(CodeGenContext context) { ... }
}
```

**Why instance-based?**
- Needs `CodeGenContext` to access the symbol table for type lookup
- Needs to know the current source file path for cross-file type resolution
- Needs access to project namespace configuration

**Why a static map?**
- `_builtinTypeMap` is static because primitive type mappings are universal and immutable
- Populated once at startup from `PrimitiveCatalog`

---

## 3. Key Functions/Methods

### Initialization and Built-in Type Mappings

#### Static Constructor - Building the Type Map

```csharp
static TypeMapper()
{
    _builtinTypeMap = new Dictionary<string, string>();

    // Add all primitives from PrimitiveCatalog
    foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
    {
        _builtinTypeMap.TryAdd(name, info.CSharpName);
    }

    // Add non-primitive type mappings (collections, etc.)
    _builtinTypeMap["list"] = "global::Sharpy.Core.List";
    _builtinTypeMap["dict"] = "global::Sharpy.Core.Dict";
    _builtinTypeMap["set"] = "global::Sharpy.Core.Set";
    _builtinTypeMap["tuple"] = "System.ValueTuple";
}
```

**What it does**:
1. Populates the map with primitive types from `PrimitiveCatalog` (int→int, str→string, etc.)
2. Adds Sharpy runtime collection types using `global::` prefix

**Why `global::`?**
If the output namespace is `namespace Sharpy.MyCode`, the name `Sharpy.Core.List` would be ambiguous. Using `global::Sharpy.Core.List` ensures we always get the runtime type.

**Design insight**: This two-phase initialization (primitives from catalog + manual collection types) separates .NET primitive types from Sharpy runtime types.

---

### Core Type Mapping: SemanticType → C# TypeSyntax

#### `MapSemanticType(SemanticType type)` - The Primary Mapping Method

This is the main entry point for mapping resolved types from semantic analysis:

```csharp
public TypeSyntax MapSemanticType(SemanticType type)
{
    return type switch
    {
        null or UnknownType => PredefinedType(Token(SyntaxKind.ObjectKeyword)),
        VoidType => PredefinedType(Token(SyntaxKind.VoidKeyword)),

        // Builtin types with singleton pattern matching
        BuiltinType builtin when type == SemanticType.Int =>
            PredefinedType(Token(SyntaxKind.IntKeyword)),
        BuiltinType builtin when type == SemanticType.Str =>
            PredefinedType(Token(SyntaxKind.StringKeyword)),
        // ... more singletons

        GenericType generic => MapGenericSemanticType(generic),
        NullableType nullable => NullableType(MapSemanticType(nullable.UnderlyingType)),
        UserDefinedType udt => ParseTypeName(GetMappedTypeNameFromSymbol(udt)),
        TypeParameterType typeParam => IdentifierName(typeParam.Name),
        Semantic.FunctionType funcType => MapSemanticFunctionType(funcType),
        Semantic.TupleType tupleType => MapSemanticTupleType(tupleType),

        _ => PredefinedType(Token(SyntaxKind.ObjectKeyword))
    };
}
```

**Key algorithm**:
1. Pattern match on the `SemanticType` discriminated union
2. For common built-in types, use singleton identity checks (`type == SemanticType.Int`)
3. For generic/nullable/user-defined types, delegate to specialized methods
4. Fall back to `object` for unknown types

**Why singleton checks?**
```csharp
// Efficient: uses reference equality
when type == SemanticType.Int

// vs. checking the Name property on every builtin
when builtin.Name == "int"
```

The semantic analysis phase uses singleton instances for common types, making identity checks faster than string comparisons.

**Connection to RoslynEmitter**: This method returns a `TypeSyntax` (Roslyn API), which can be directly inserted into method signatures, variable declarations, etc.

---

#### `MapGenericSemanticType(GenericType generic)` - Generic Type Handling

```csharp
private TypeSyntax MapGenericSemanticType(GenericType generic)
{
    var baseTypeName = GetMappedTypeName(generic.Name);
    var typeArgs = generic.TypeArguments
        .Select(MapSemanticType)  // Recursive!
        .ToArray();

    return GenericName(Identifier(baseTypeName))
        .WithTypeArgumentList(
            TypeArgumentList(SeparatedList(typeArgs)));
}
```

**What it does**: Maps `list[int]` → `global::Sharpy.Core.List<int>`

**Key insight**: This is **recursive**. For `list[dict[str, int]]`:
1. Map `dict[str, int]` first (recursively calls `MapSemanticType`)
2. Map `list[...]` with the result

**Roslyn pattern**: Uses `SyntaxFactory.GenericName()` to build `List<int>` syntax rather than string concatenation.

---

#### `MapSemanticFunctionType(Semantic.FunctionType funcType)` - Function Type to Delegate

Function types in Sharpy (e.g., `(int, str) -> bool`) map to C# delegates:

```csharp
private TypeSyntax MapSemanticFunctionType(Semantic.FunctionType funcType)
{
    var paramTypes = funcType.ParameterTypes
        .Select(MapSemanticType)
        .ToArray();

    var returnType = MapSemanticType(funcType.ReturnType);

    // If return type is void, use Action<T1, T2, ...>
    if (funcType.ReturnType is VoidType)
    {
        if (paramTypes.Length == 0)
            return ParseTypeName("System.Action");

        return GenericName("System.Action")
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(paramTypes)));
    }

    // Otherwise use Func<T1, T2, ..., TResult>
    var allTypes = paramTypes.Append(returnType).ToArray();

    return GenericName("System.Func")
        .WithTypeArgumentList(TypeArgumentList(SeparatedList(allTypes)));
}
```

**Mapping rules**:
- `() -> None` → `System.Action`
- `(int, str) -> None` → `System.Action<int, string>`
- `() -> int` → `System.Func<int>`
- `(int, str) -> bool` → `System.Func<int, string, bool>`

**Design decision**: Use .NET's built-in `Func`/`Action` delegates rather than defining custom delegate types. This makes generated code more interoperable with existing .NET libraries.

---

#### `MapSemanticTupleType(Semantic.TupleType tupleType)` - Tuple Type Mapping

```csharp
private TypeSyntax MapSemanticTupleType(Semantic.TupleType tupleType)
{
    if (tupleType.ElementTypes.Count == 0)
        return ParseTypeName("System.ValueTuple");

    var elementTypes = tupleType.ElementTypes
        .Select(MapSemanticType)
        .ToArray();

    // For single element, it's just the type (not a tuple)
    if (elementTypes.Length == 1)
        return elementTypes[0];

    // Use ValueTuple<T1, T2, ...>
    return GenericName("System.ValueTuple")
        .WithTypeArgumentList(TypeArgumentList(SeparatedList(elementTypes)));
}
```

**Mapping rules**:
- `()` → `System.ValueTuple` (unit/empty tuple)
- `(int,)` → `int` (single-element "tuple" is just the type)
- `(int, str)` → `System.ValueTuple<int, string>`

**Why ValueTuple?** C# 7.0+ has built-in tuple syntax `(int, string)`, but for code generation it's clearer to use the explicit `ValueTuple<T1, T2>` type. Roslyn will format it nicely anyway.

---

### AST Type Mapping: TypeAnnotation → C# TypeSyntax

#### `MapType(TypeAnnotation? type)` - AST Type Annotation Mapping

This is the entry point for mapping type annotations from the parsed AST (before or in parallel with semantic analysis):

```csharp
public TypeSyntax MapType(TypeAnnotation? type)
{
    // Default to object if no type specified
    if (type == null)
        return PredefinedType(Token(SyntaxKind.ObjectKeyword));

    // Check if this is a type alias and expand it
    var aliasSymbol = _context.SymbolTable.LookupTypeAlias(type.Name);
    if (aliasSymbol != null)
    {
        if (aliasSymbol.TypeAnnotation != null)
        {
            var expandedType = MapType(aliasSymbol.TypeAnnotation);
            return type.IsNullable ? NullableType(expandedType) : expandedType;
        }
        else if (aliasSymbol.FunctionType != null)
        {
            var expandedType = MapFunctionType(aliasSymbol.FunctionType);
            return type.IsNullable ? NullableType(expandedType) : expandedType;
        }
    }

    // Get base type name
    var baseTypeName = GetMappedTypeName(type.Name);

    // Handle generic type arguments
    if (type.TypeArguments.Length > 0)
    {
        var typeArgs = type.TypeArguments
            .Select(MapType)  // Recursive
            .ToArray();

        var result = GenericName(Identifier(baseTypeName))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));

        return type.IsNullable ? NullableType(result) : result;
    }

    // Handle nullable non-generic types
    var typeSyntax = ParseTypeName(baseTypeName);
    return type.IsNullable ? NullableType(typeSyntax) : typeSyntax;
}
```

**Key features**:
1. **Type alias expansion**: `type MyInt = int` gets expanded inline
2. **Nullable handling**: `Optional[str]` or `str?` becomes `string?`
3. **Generic type arguments**: Recursively maps nested generics
4. **Fallback to object**: Missing type annotations become `object`

**Design insight**: Type aliases are expanded at code generation time rather than stored in the symbol table, ensuring the generated C# uses concrete types.

---

### Type Name Resolution and Cross-File References

#### `GetMappedTypeName(string sharpyTypeName)` - The Type Name Resolver

This is where the magic of type resolution happens:

```csharp
private string GetMappedTypeName(string sharpyTypeName)
{
    // Check if it's a built-in type
    if (_builtinTypeMap.TryGetValue(sharpyTypeName, out var csharpType))
        return csharpType;

    // Check if it's a known builtin from the registry
    if (_context.IsBuiltinType(sharpyTypeName))
        return $"Sharpy.{sharpyTypeName}";

    // Check if it's a user-defined type from another file/module
    var typeSymbol = _context.SymbolTable.LookupType(sharpyTypeName);
    if (typeSymbol != null)
    {
        // Cross-file reference?
        if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath) &&
            !string.IsNullOrEmpty(_context.SourceFilePath) &&
            !string.Equals(typeSymbol.DefiningFilePath, _context.SourceFilePath,
                          StringComparison.OrdinalIgnoreCase))
        {
            return GetFullyQualifiedTypeName(typeSymbol, sharpyTypeName);
        }

        // External module reference?
        if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
        {
            return GetFullyQualifiedTypeName(typeSymbol, sharpyTypeName);
        }
    }

    // User-defined types in current module keep their PascalCase name
    return NameMangler.ToPascalCase(sharpyTypeName);
}
```

**Resolution algorithm** (priority order):
1. **Built-in primitives**: `int` → `int`, `str` → `string`
2. **Built-in runtime types**: `list` → `global::Sharpy.Core.List`
3. **Builtin registry types**: Custom Sharpy runtime types
4. **Cross-file user types**: Types from other `.spy` files in the same project
5. **External module types**: Types from imported modules
6. **Local types**: Types defined in the current file

**Why file path comparison?** When compiling multi-file projects, we need to know if a type reference is:
- Same file: Use simple name `MyClass`
- Different file: Use fully-qualified name `MyProject.OtherModule.MyClass`

---

#### `GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)` - Building Qualified Names

```csharp
private string GetFullyQualifiedTypeName(TypeSymbol typeSymbol, string sharpyTypeName)
{
    string moduleNamespace;

    if (!string.IsNullOrEmpty(typeSymbol.DefiningModule))
    {
        // Use DefiningModule (e.g., "animal" from import)
        moduleNamespace = ConvertModuleToNamespace(typeSymbol.DefiningModule);
    }
    else if (!string.IsNullOrEmpty(typeSymbol.DefiningFilePath))
    {
        // Derive module namespace from file path
        moduleNamespace = GetModuleNameFromFilePath(typeSymbol.DefiningFilePath);
    }
    else
    {
        return NameMangler.ToPascalCase(sharpyTypeName);
    }

    var typeName = NameMangler.ToPascalCase(sharpyTypeName);

    // Types are at namespace level (not nested in Exports)
    if (!string.IsNullOrEmpty(_context.ProjectNamespace))
    {
        return $"{_context.ProjectNamespace}.{moduleNamespace}.{typeName}";
    }
    return $"{moduleNamespace}.{typeName}";
}
```

**What it does**: Constructs fully-qualified type names for cross-module references.

**Example scenario**:
```python
# In file: src/models/animal.spy
class Dog:
    pass

# In file: src/main.spy
from models.animal import Dog

def create_dog() -> Dog:  # Need fully-qualified reference
    pass
```

Generated C#:
```csharp
namespace MyProject.Models.Animal
{
    public class Dog { }
}

namespace MyProject.Main
{
    public static class Exports
    {
        public static MyProject.Models.Animal.Dog CreateDog() { }
    }
}
```

**Critical design note**: The comment on line 287-288 explains a key architectural decision:
> Types are placed at namespace level (siblings to the module class), so we use Namespace.TypeName, not Namespace.Exports.TypeName.

This means types are NOT nested inside the `Exports` class, making them easier to reference and more idiomatic C#.

---

#### `GetModuleNameFromFilePath(string filePath)` - File Path to Namespace

```csharp
private static string GetModuleNameFromFilePath(string filePath)
{
    var fileName = Path.GetFileNameWithoutExtension(filePath);
    return SimpleToPascalCase(fileName);
}
```

Simple but important: `animal.spy` → `Animal` namespace.

---

### Type Inference and Expression Handling

#### `InferElementType(IEnumerable<Expression> expressions)` - Collection Element Type Inference

When you write `[1, 2, 3]` without an explicit type annotation, Sharpy needs to infer the element type:

```csharp
public TypeSyntax InferElementType(IEnumerable<Expression> expressions)
{
    var exprs = expressions.ToList();
    if (exprs.Count == 0)
        return PredefinedType(Token(SyntaxKind.ObjectKeyword));

    var firstExpr = exprs[0];
    var inferredType = InferExpressionType(firstExpr);

    // Check if all expressions have the same type
    if (exprs.All(e => TypesMatch(InferExpressionType(e), inferredType)))
    {
        return MapTypeFromInferredType(inferredType);
    }

    // Fall back to object
    return PredefinedType(Token(SyntaxKind.ObjectKeyword));
}
```

**Inference algorithm**:
1. Empty collection → `object`
2. All elements same type → Use that type
3. Mixed types → Fall back to `object`

**Example**:
- `[1, 2, 3]` → All `int` → `List<int>`
- `[1, "hello", true]` → Mixed → `List<object>`

**v0.1 limitation**: The comment on line 451 notes this is a "simple heuristic" for the initial version. Future versions might implement more sophisticated inference (common base type, union types, etc.).

---

#### `InferExpressionType(Expression expr)` - Literal Type Inference

```csharp
private string InferExpressionType(Expression expr)
{
    return expr switch
    {
        IntegerLiteral => "int",
        FloatLiteral floatLit => floatLit.Suffix?.ToLower() switch
        {
            "f" => "float",
            "m" => "decimal",
            _ => "double"
        },
        StringLiteral => "string",
        BooleanLiteral => "bool",
        NoneLiteral => "object",
        ListLiteral => "list",
        DictLiteral => "dict",
        SetLiteral => "set",
        TupleLiteral => "tuple",
        _ => "object"
    };
}
```

**What it does**: Infers types from literal expressions.

**Float literal handling**: The `FloatLiteral` case is sophisticated—it checks for suffixes:
- `3.14f` → `float`
- `3.14m` → `decimal`
- `3.14` → `double` (default)

This mirrors C# literal syntax!

---

### Utility Methods for Type Construction

#### `CreateCollectionType(string collectionName, TypeSyntax elementType)` - Helper for Collections

```csharp
public TypeSyntax CreateCollectionType(string collectionName, TypeSyntax elementType)
{
    var baseType = GetMappedTypeName(collectionName);

    return GenericName(Identifier(baseType))
        .WithTypeArgumentList(
            TypeArgumentList(SingletonSeparatedList(elementType)));
}
```

**Usage in RoslynEmitter**:
```csharp
var intType = PredefinedType(Token(SyntaxKind.IntKeyword));
var listType = typeMapper.CreateCollectionType("list", intType);
// Result: global::Sharpy.Core.List<int>
```

---

#### `CreateDictType(TypeSyntax keyType, TypeSyntax valueType)` - Helper for Dictionaries

```csharp
public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
{
    return GenericName("global::Sharpy.Core.Dict")
        .WithTypeArgumentList(
            TypeArgumentList(SeparatedList(new[] { keyType, valueType })));
}
```

**Why hardcoded `global::Sharpy.Core.Dict`?** Dictionaries are special because they have two type parameters. This helper ensures consistency.

---

#### `MakeNullable(TypeSyntax type)` - Nullable Type Constructor

```csharp
public TypeSyntax MakeNullable(TypeSyntax type)
{
    return NullableType(type);
}
```

Converts `string` → `string?`, `int` → `int?`. Simple wrapper around Roslyn's `NullableType()`.

---

#### `MakeArrayType(TypeSyntax elementType, int rank = 1)` - Array Type Constructor

```csharp
public TypeSyntax MakeArrayType(TypeSyntax elementType, int rank = 1)
{
    var rankSpecifier = ArrayRankSpecifier(
        SeparatedList<ExpressionSyntax>(
            Enumerable.Repeat(OmittedArraySizeExpression(), rank)));

    return ArrayType(elementType)
        .AddRankSpecifiers(rankSpecifier);
}
```

**What it does**:
- `rank=1`: `int[]`
- `rank=2`: `int[,]`
- `rank=3`: `int[,,]`

**Note**: The `OmittedArraySizeExpression()` generates the commas without sizes (C# array declaration syntax).

---

### Generic Type Instantiation from Expressions

#### `MapTypeFromExpression(Expression expr)` - Expression as Type

This handles a tricky parsing scenario in Sharpy:

```python
# Generic type instantiation syntax
box = Box[int](42)
#     ^^^^^^^^
#     This is parsed as IndexAccess expression, not a type!
```

```csharp
public TypeSyntax MapTypeFromExpression(Expression expr)
{
    if (expr is Identifier id)
    {
        var annotation = new TypeAnnotation { Name = id.Name };
        return MapType(annotation);
    }

    // Handle nested generic types (e.g., Box[int] in Container[Box[int]])
    if (expr is IndexAccess indexAccess && indexAccess.Object is Identifier nestedTypeName)
    {
        var typeArgs = MapTypeArgumentsFromExpression(indexAccess.Index);
        return GenericName(NameMangler.ToPascalCase(nestedTypeName.Name))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
    }

    return PredefinedType(Token(SyntaxKind.ObjectKeyword));
}
```

**The problem**: `Box[int]` parses as:
```
IndexAccess {
    Object: Identifier("Box"),
    Index: Identifier("int")
}
```

Not as a type annotation! This method bridges that gap.

---

#### `MapTypeArgumentsFromExpression(Expression expr)` - Type Arguments from Tuple

```csharp
public TypeSyntax[] MapTypeArgumentsFromExpression(Expression expr)
{
    // Handle multiple type arguments: Pair[int, str] parses as TupleLiteral
    if (expr is TupleLiteral tuple)
    {
        return tuple.Elements.Select(MapTypeFromExpression).ToArray();
    }

    // Handle single type argument
    return new[] { MapTypeFromExpression(expr) };
}
```

**Why TupleLiteral?** In the parser, `Pair[int, str]` becomes:
```
IndexAccess {
    Object: Identifier("Pair"),
    Index: TupleLiteral([Identifier("int"), Identifier("str")])
}
```

So multiple type arguments are represented as tuple elements.

---

## 4. Dependencies

### Internal Dependencies

1. **`CodeGenContext`** (src/Sharpy.Compiler/CodeGen/CodeGenContext.cs):
   - Provides `SymbolTable` for type lookups
   - Provides `BuiltinRegistry` for runtime type checks
   - Provides `SourceFilePath` for cross-file resolution
   - Provides `ProjectNamespace` for qualified name construction

2. **`NameMangler`** (src/Sharpy.Compiler/CodeGen/NameMangler.cs):
   - Used to convert Sharpy type names to C# PascalCase
   - Example: `my_type` → `MyType`

3. **`PrimitiveCatalog`** (src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs):
   - Source of truth for primitive type mappings
   - Provides Sharpy name → C# name mappings (int→int, str→string, etc.)

4. **`Sharpy.Compiler.Parser.Ast`**:
   - `TypeAnnotation`: AST representation of type annotations
   - `FunctionType`, `TupleType`: Specialized type annotations
   - Expression types for type inference

5. **`Sharpy.Compiler.Semantic`**:
   - `SemanticType` and all its subtypes (BuiltinType, GenericType, etc.)
   - `TypeSymbol`: Symbol table entries for types
   - `SymbolTable`: Type resolution

### External Dependencies

- **Microsoft.CodeAnalysis.CSharp**: Roslyn API for building C# syntax trees
  - `SyntaxFactory`: Factory methods for creating syntax nodes
  - `TypeSyntax`: Base type for all C# type syntax nodes
  - Various specific syntax types (GenericNameSyntax, PredefinedTypeSyntax, etc.)

### Callers of TypeMapper

- **`RoslynEmitter`** (and all its partial classes): Primary consumer
  - Uses `MapType()` for AST type annotations
  - Uses `MapSemanticType()` for resolved semantic types
  - Uses helper methods for collection/tuple/function types

---

## 5. Patterns and Design Decisions

### Design Pattern: Two-Phase Type Mapping

**The duality**:
```csharp
MapType(TypeAnnotation)      // From AST (parser output)
MapSemanticType(SemanticType) // From semantic analysis (resolved types)
```

**Why both?**

During code generation, we encounter types from two sources:
1. **AST annotations**: User wrote `def foo(x: int) -> str`
2. **Semantic types**: Type inference resolved an expression to `SemanticType.Int`

Having two entry points makes it clear which source we're working with and allows different resolution strategies.

**Trade-off**: Some code duplication (both methods handle generics, nullables, etc.), but the separation is worth it for clarity.

---

### Design Decision: Static Builtin Map + Instance Context

**The hybrid approach**:
```csharp
private static readonly Dictionary<string, string> _builtinTypeMap;  // Static
private readonly CodeGenContext _context;                            // Instance
```

**Why not all static?**
- Type resolution needs runtime context (which file are we compiling?)
- Symbol table is per-compilation, not global

**Why not all instance?**
- Primitive mappings are universal and immutable
- Static initialization is more efficient than re-creating the map for each instance

---

### Design Decision: Fully-Qualified Names with `global::`

**The problem**:
```csharp
namespace Sharpy.MyCode
{
    // Without global::, "Sharpy.Core.List" is ambiguous!
    // Is it MyCode.Sharpy.Core.List or global Sharpy.Core.List?
    public void Foo(Sharpy.Core.List<int> items) { }
}
```

**The solution**:
```csharp
namespace Sharpy.MyCode
{
    public void Foo(global::Sharpy.Core.List<int> items) { }
}
```

The `global::` prefix (line 33-36) ensures we always refer to the Sharpy runtime, not a user-defined type.

---

### Design Decision: Types at Namespace Level

From the comment on lines 287-288:
> Types are placed at namespace level (siblings to the module class), so we use Namespace.TypeName, not Namespace.Exports.TypeName.

**The architecture**:
```csharp
// NOT this (types nested in Exports):
namespace Animal
{
    public static class Exports
    {
        public class Dog { }  // Hard to reference
    }
}

// But this (types at namespace level):
namespace Animal
{
    public class Dog { }     // Easy to reference

    public static class Exports
    {
        // Module-level functions
    }
}
```

**Why?** Makes types easier to reference from other files and feels more natural in C#. See `RoslynEmitter.CompilationUnit.md` for the full rationale.

---

### Design Decision: Recursive Type Mapping

Notice how many methods call back into `MapSemanticType()` or `MapType()`:

```csharp
// In MapGenericSemanticType:
var typeArgs = generic.TypeArguments
    .Select(MapSemanticType)  // Recursive!
    .ToArray();

// In MapSemanticTupleType:
var elementTypes = tupleType.ElementTypes
    .Select(MapSemanticType)  // Recursive!
    .ToArray();
```

**Why?** Types can nest arbitrarily:
- `list[list[dict[str, int]]]`
- `(int, (str, bool))`
- `Func<int, Func<str, bool>>`

Recursion naturally handles this nesting without manual stack management.

---

### Design Decision: Func/Action for Function Types

**Sharpy function type**:
```python
type Callback = (int, str) -> bool
```

**Alternatives considered**:
1. **Custom delegate types**: `delegate bool Callback(int, str);`
2. **Interface types**: `interface ICallback { bool Call(int, str); }`
3. **.NET Func/Action**: `Func<int, string, bool>`

**Why Func/Action?**
- Standard .NET convention
- Interoperable with existing .NET libraries
- No need to generate delegate declarations
- Supports up to 16 type parameters (enough for most cases)

**Trade-off**: Less debuggable (stack traces show `Func<...>` instead of semantic names), but the interop benefits outweigh this.

---

### Design Decision: Simple Type Inference (v0.1)

The comment on line 451-452 is honest about limitations:
> For v0.1, we'll use a simple heuristic:
> - If all expressions are the same literal type, use that
> - Otherwise, use object

**Future improvements** (not yet implemented):
- Common base type inference: `[Dog(), Cat()]` → `Animal[]`
- Union types: `[1, "hello"]` → `(int | str)[]`
- Flow-sensitive narrowing

**Why start simple?** Get the compiler working end-to-end first, then add sophistication.

---

## 6. Debugging Tips

### Problem: Type Not Found (Generates `object`)

**Symptom**:
```csharp
// Expected: public void Foo(MyClass obj)
// Got: public void Foo(object obj)
```

**Debug approach**:
1. Check if `MyClass` is in the symbol table: `_context.SymbolTable.LookupType("MyClass")`
2. Add a breakpoint in `GetMappedTypeName()` and step through the resolution order
3. Check if the type is in a different file—verify `DefiningFilePath` is set correctly
4. For cross-file types, verify the project namespace is configured in `_context.ProjectNamespace`

**Common causes**:
- Type not added to symbol table during semantic analysis
- Case sensitivity mismatch (`MyClass` vs. `myclass`)
- Cross-file reference but no `DefiningFilePath` set

---

### Problem: Generic Type Has Wrong Arguments

**Symptom**:
```csharp
// Expected: List<int>
// Got: List<object>
```

**Debug approach**:
1. Breakpoint in `MapGenericSemanticType()` or `MapType()` (depending on source)
2. Inspect `generic.TypeArguments`—are they set correctly?
3. Check the recursive call to `MapSemanticType()`—what does it return?
4. Verify semantic analysis populated type arguments correctly

**Common causes**:
- Type arguments not set during parsing
- Type argument resolution failed in semantic analysis
- Inference fell back to `object`

---

### Problem: Wrong Fully-Qualified Name

**Symptom**:
```csharp
// Expected: MyProject.Models.Animal.Dog
// Got: Dog  (or wrong namespace)
```

**Debug approach**:
1. Breakpoint in `GetFullyQualifiedTypeName()`
2. Check `typeSymbol.DefiningFilePath` and `typeSymbol.DefiningModule`—are they set?
3. Verify `_context.SourceFilePath` is correct for the current file
4. Check `_context.ProjectNamespace` configuration
5. Trace through `GetModuleNameFromFilePath()` or `ConvertModuleToNamespace()`

**Common causes**:
- Symbol table not tracking file paths correctly
- Multi-file compilation not setting project namespace
- Case sensitivity in file path comparison (line 242)

---

### Problem: Nullable Type Not Generated

**Symptom**:
```csharp
// Expected: string?
// Got: string
```

**Debug approach**:
1. Check the AST `TypeAnnotation.IsNullable` property
2. Verify `MapType()` checks `IsNullable` (lines 205-214)
3. For semantic types, check if it's a `NullableType` wrapper (line 68)

**Common causes**:
- Parser not setting `IsNullable` correctly
- Semantic analysis not wrapping in `NullableType`
- Nullable handling logic bypassed

---

### Problem: Function Type Maps to Wrong Delegate

**Symptom**:
```csharp
// Expected: Func<int, string>
// Got: Action<int>  (or vice versa)
```

**Debug approach**:
1. Breakpoint in `MapSemanticFunctionType()` or `MapFunctionType()`
2. Check `funcType.ReturnType`—is it `VoidType`?
3. Verify the `IsVoidType()` check (line 361)
4. Inspect parameter types array

**Common causes**:
- Return type incorrectly resolved as void
- Parameter types wrong length
- Mixing up AST `FunctionType` and semantic `FunctionType`

---

### Useful Test Cases for Manual Verification

```csharp
// Test in a unit test or C# REPL:

// Primitives
typeMapper.MapType(new TypeAnnotation { Name = "int" })     // → int
typeMapper.MapType(new TypeAnnotation { Name = "str" })     // → string

// Collections
typeMapper.MapType(new TypeAnnotation { Name = "list" })    // → global::Sharpy.Core.List

// Generics (need to build TypeAnnotation with TypeArguments)
// Test with semantic types instead:
typeMapper.MapSemanticType(new GenericType {
    Name = "list",
    TypeArguments = new[] { SemanticType.Int }
})  // → global::Sharpy.Core.List<int>

// Nullable
typeMapper.MapType(new TypeAnnotation {
    Name = "str",
    IsNullable = true
})  // → string?

// Function types
typeMapper.MapSemanticType(new Semantic.FunctionType {
    ParameterTypes = new[] { SemanticType.Int, SemanticType.Str },
    ReturnType = SemanticType.Bool
})  // → System.Func<int, string, bool>
```

---

## 7. Contribution Guidelines

### When to Modify This File

**You SHOULD modify `TypeMapper.cs` when**:

1. **Adding a new primitive type**:
   - First, add it to `PrimitiveCatalog.cs`
   - The static constructor will automatically pick it up
   - Test with both `MapType()` and `MapSemanticType()`

2. **Adding a new built-in collection type**:
   ```csharp
   // In static constructor:
   _builtinTypeMap["frozenset"] = "global::Sharpy.Core.FrozenSet";
   ```

3. **Adding a new semantic type variant**:
   ```csharp
   // In MapSemanticType switch:
   UnionType union => MapUnionType(union),
   ```

4. **Improving type inference**:
   - Update `InferExpressionType()` for new literal types
   - Enhance `InferElementType()` with better algorithms

5. **Fixing cross-file type resolution bugs**:
   - Update `GetFullyQualifiedTypeName()`
   - Improve file path comparison logic

6. **Supporting new type annotation syntax**:
   - Extend `MapType()` to handle new AST nodes
   - May require parser changes first

---

### When NOT to Modify This File

**You SHOULD NOT modify `TypeMapper.cs` for**:

1. **Name mangling**: That belongs in `NameMangler.cs`
2. **Code generation**: That belongs in `RoslynEmitter.cs` and its partials
3. **Type checking/validation**: That belongs in semantic analysis
4. **AST modifications**: Parser or semantic analysis phase

---

### Adding Support for a New Generic Type

**Scenario**: Adding `OrderedDict[K, V]` support

**Steps**:

1. **Add to builtin map**:
   ```csharp
   // In static TypeMapper():
   _builtinTypeMap["ordereddict"] = "global::Sharpy.Core.OrderedDict";
   ```

2. **Add runtime type** (in separate PR):
   ```csharp
   // In src/Sharpy.Core/OrderedDict.cs
   public class OrderedDict<K, V> { ... }
   ```

3. **Test**:
   ```csharp
   [Fact]
   public void MapType_OrderedDict_MapsToSharpyCoreOrderedDict()
   {
       var annotation = new TypeAnnotation {
           Name = "ordereddict",
           TypeArguments = new[] {
               new TypeAnnotation { Name = "str" },
               new TypeAnnotation { Name = "int" }
           }
       };
       var result = typeMapper.MapType(annotation);
       // Assert result is GenericNameSyntax with correct name and args
   }
   ```

---

### Testing Your Changes

**Unit tests location**: `src/Sharpy.Compiler.Tests/CodeGen/TypeMapperTests.cs`

**Test categories to cover**:
- Primitive type mappings
- Generic type construction
- Nullable type handling
- Function type to delegate mapping
- Tuple type mapping
- Cross-file type resolution
- Type inference from expressions
- Edge cases (null annotations, empty collections, etc.)

**Integration testing**:
1. Write a `.spy` file using the new type
2. Run `dotnet run --project src/Sharpy.Cli -- emit csharp test.spy`
3. Verify generated C# has correct type syntax
4. Compile the generated C# to ensure it's valid

---

### Code Style Guidelines for This File

1. **Use Roslyn SyntaxFactory exclusively**: No string concatenation for types
2. **Recursive methods should be pure**: No side effects in type mapping
3. **Null-safe**: Always check for null annotations
4. **Fail gracefully**: Unknown types fall back to `object`, not crash
5. **Document special cases**: Why `global::`? Why check file paths? Explain in comments.
6. **Keep methods focused**: One mapping concern per method

---

### Common Pitfalls to Avoid

1. **String concatenation for types**:
   ```csharp
   // WRONG:
   return ParseTypeName($"List<{elementType}>");

   // RIGHT:
   return GenericName("List")
       .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(elementType)));
   ```

2. **Forgetting `global::` for runtime types**:
   ```csharp
   // WRONG: Might be ambiguous
   "Sharpy.Core.List"

   // RIGHT: Always unambiguous
   "global::Sharpy.Core.List"
   ```

3. **Not handling recursion**:
   ```csharp
   // WRONG: Assumes type arguments are simple
   var typeArgs = generic.TypeArguments.Select(t => ParseTypeName(t.Name));

   // RIGHT: Recursively map (handles nested generics)
   var typeArgs = generic.TypeArguments.Select(MapSemanticType);
   ```

4. **Mixing AST and semantic types**:
   ```csharp
   // Know which you have!
   MapType(TypeAnnotation)          // For AST
   MapSemanticType(SemanticType)    // For semantic analysis output
   ```

5. **Case sensitivity in file paths**: Use `StringComparison.OrdinalIgnoreCase` on Windows (line 242)

---

## 8. Cross-References

### Related Documentation Files

Since `TypeMapper.cs` is a single-file class (not a partial class), there are no other files to cross-reference within the same class. However, it heavily interacts with:

- **[RoslynEmitter.md](RoslynEmitter.md)**: Main code generator that uses TypeMapper
- **[RoslynEmitter.TypeDeclarations.md](RoslynEmitter.TypeDeclarations.md)**: How types are used in class/interface declarations
- **[RoslynEmitter.CompilationUnit.md](RoslynEmitter.CompilationUnit.md)**: Module structure and namespace generation
- **[CodeGenContext.md](CodeGenContext.md)**: Context object that TypeMapper depends on
- **[NameMangler.md](NameMangler.md)**: Name transformation logic used by TypeMapper

### Related Specification Documents

From the compiler context, these specs are relevant:
- `docs/language_specification/type_annotations.md`: Defines type annotation syntax
- `docs/language_specification/type_casting.md`: Type conversion rules
- `docs/language_specification/type_hierarchy.md`: Type system structure
- `docs/language_specification/type_narrowing.md`: Type refinement (affects nullable handling)

---

## Quick Reference

### Method Selection Guide

| Use Case | Method | Input | Output |
|----------|--------|-------|--------|
| Map resolved semantic type | `MapSemanticType()` | `SemanticType` | `TypeSyntax` |
| Map AST type annotation | `MapType()` | `TypeAnnotation?` | `TypeSyntax` |
| Create collection type | `CreateCollectionType()` | `string, TypeSyntax` | `TypeSyntax` |
| Create dict type | `CreateDictType()` | `TypeSyntax, TypeSyntax` | `TypeSyntax` |
| Make type nullable | `MakeNullable()` | `TypeSyntax` | `TypeSyntax` |
| Infer from expression | `InferTypeFromExpression()` | `Expression` | `TypeSyntax` |
| Infer collection element | `InferElementType()` | `IEnumerable<Expression>` | `TypeSyntax` |

### Common Type Mappings

```
Sharpy Type              → C# Type
--------------             --------
int                      → int
str                      → string
bool                     → bool
float                    → double
list                     → global::Sharpy.Core.List
dict                     → global::Sharpy.Core.Dict
set                      → global::Sharpy.Core.Set
tuple                    → System.ValueTuple
Optional[str] / str?     → string?
list[int]                → global::Sharpy.Core.List<int>
dict[str, int]           → global::Sharpy.Core.Dict<string, int>
(int, str) -> bool       → System.Func<int, string, bool>
() -> None               → System.Action
(int, str)               → System.ValueTuple<int, string>
MyClass (same file)      → MyClass
MyClass (other file)     → ProjectNamespace.ModuleName.MyClass
```

---

## Summary

`TypeMapper.cs` is the **type translation engine** that bridges Sharpy's type system and C#'s type system. It handles:

1. **Dual input sources**: AST type annotations and semantic types
2. **Complex type structures**: Generics, nullables, tuples, functions
3. **Cross-file resolution**: Fully-qualified names for multi-file projects
4. **Type inference**: Inferring types from literal expressions
5. **Idiomatic output**: Using .NET conventions (Func/Action, ValueTuple, etc.)

When modifying this file:
- **Use Roslyn SyntaxFactory** exclusively (no string concatenation)
- **Test both entry points** (`MapType` and `MapSemanticType`)
- **Handle recursion** for nested types
- **Preserve context** using `CodeGenContext`
- **Document edge cases** in comments

Understanding `TypeMapper` is essential for understanding how Sharpy maintains type safety while compiling to C#. It's where the promise "Sharpy is Python syntax with C# types" becomes reality.
