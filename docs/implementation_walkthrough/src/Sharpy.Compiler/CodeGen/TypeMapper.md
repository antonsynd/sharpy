# Walkthrough: TypeMapper.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

---

## Overview

TypeMapper is the **type translation bridge** between Sharpy's type system and C#'s type system. It sits at the heart of the code generation phase, converting every type representation—whether from the AST's `TypeAnnotation` or from semantic analysis's `SemanticType`—into Roslyn's `TypeSyntax` nodes that can be emitted as C# source code.

**Key Responsibilities:**
- Translate Sharpy primitive types (int, str, bool) to C# equivalents
- Map Sharpy collections (list, dict, set) to runtime types in `Sharpy.Core`
- Handle generic types, nullable types, and type parameters
- Convert function types to C# delegates (Func/Action)
- Support type aliases and type inference for literals
- Provide helper methods for creating collection and array types

**Pipeline Position:**
```
Semantic Analysis (SemanticType) ──┐
                                   ├──> TypeMapper ──> TypeSyntax ──> RoslynEmitter ──> C# Code
Parser AST (TypeAnnotation) ───────┘
```

TypeMapper doesn't perform type checking—that's semantic analysis's job. It assumes types are already validated and focuses purely on **syntactic translation**.

---

## Class/Type Structure

### Main Class: `TypeMapper`

```csharp
public class TypeMapper
{
    private readonly CodeGenContext _context;
    private static readonly Dictionary<string, string> _builtinTypeMap;

    // Constructor, mapping methods...
}
```

**Architecture:**
- **Instance-based**: Each `TypeMapper` instance is tied to a `CodeGenContext` for symbol table access
- **Static type catalog**: Built-in type mappings are shared across all instances via `_builtinTypeMap`
- **Stateless translation**: All methods are side-effect-free transformations

---

## Key Functions/Methods

### 1. Static Initialization: Populating Built-in Types

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

**Design Decision: Why `global::`?**
The `global::` prefix prevents namespace collisions. If a user creates a namespace called "Sharpy" in their output code, `Sharpy.Core.List` would become ambiguous. Using `global::Sharpy.Core.List` ensures we always reference the runtime library.

**Connection to Upstream:**
- Uses `PrimitiveCatalog.GetAllPrimitives()` from semantic analysis to discover all primitive type mappings
- This creates a single source of truth: primitives defined once in semantic analysis, automatically propagated to codegen

---

### 2. Core Translation: `MapSemanticType(SemanticType type)`

This is the **primary entry point** for translating types discovered during semantic analysis.

```csharp
public TypeSyntax MapSemanticType(SemanticType type)
{
    return type switch
    {
        null or UnknownType => PredefinedType(Token(SyntaxKind.ObjectKeyword)),
        VoidType => PredefinedType(Token(SyntaxKind.VoidKeyword)),

        // Singleton pattern for primitives
        BuiltinType builtin when type == SemanticType.Int =>
            PredefinedType(Token(SyntaxKind.IntKeyword)),
        // ... more primitive checks ...

        GenericType generic => MapGenericSemanticType(generic),
        NullableType nullable => NullableType(MapSemanticType(nullable.UnderlyingType)),
        UserDefinedType udt => ParseTypeName(GetMappedTypeName(udt.Name)),
        TypeParameterType typeParam => IdentifierName(typeParam.Name),
        FunctionType funcType => MapSemanticFunctionType(funcType),
        TupleType tupleType => MapSemanticTupleType(tupleType),

        _ => PredefinedType(Token(SyntaxKind.ObjectKeyword))
    };
}
```

**Key Insights:**

1. **Null Safety**: Unknown or null types become `object` (safe fallback)

2. **Singleton Pattern for Primitives**: The semantic analyzer uses singleton instances like `SemanticType.Int`, so we can use reference equality (`type == SemanticType.Int`) instead of name matching. This is faster and type-safe.

3. **Recursive Structure**: For complex types (generics, nullables, tuples), this method recursively calls itself. For example:
   ```
   list[int?] → GenericType(list, [NullableType(int)])
              → MapGenericSemanticType
              → Recursively maps NullableType(int)
              → Returns: global::Sharpy.Core.List<int?>
   ```

4. **Upstream Connection**: This method consumes the output of `TypeResolver` and `TypeChecker` from semantic analysis. Every `SemanticType` has already been validated for correctness.

---

### 3. Generic Type Mapping: `MapGenericSemanticType(GenericType generic)`

```csharp
private TypeSyntax MapGenericSemanticType(GenericType generic)
{
    var baseTypeName = GetMappedTypeName(generic.Name);
    var typeArgs = generic.TypeArguments
        .Select(MapSemanticType)  // Recursive!
        .ToArray();

    return GenericName(Identifier(baseTypeName))
        .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
}
```

**Example Translation:**
```
Sharpy: list[dict[str, int]]
        ↓
C#:     global::Sharpy.Core.List<global::Sharpy.Core.Dict<string, int>>
```

**Implementation Detail:**
- `GetMappedTypeName` handles the base type translation (list → global::Sharpy.Core.List)
- `.Select(MapSemanticType)` recursively processes each type argument
- Roslyn's `GenericName` and `TypeArgumentList` build the syntax tree

---

### 4. Function Type Translation: `MapSemanticFunctionType(FunctionType funcType)`

This method maps Sharpy function types to C# delegates.

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

    if (allTypes.Length == 1)  // No params, just return type
        return GenericName("System.Func")
            .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(returnType)));

    return GenericName("System.Func")
        .WithTypeArgumentList(TypeArgumentList(SeparatedList(allTypes)));
}
```

**Translation Examples:**
```
() -> int              →  System.Func<int>
(str) -> bool          →  System.Func<string, bool>
(int, int) -> str      →  System.Func<int, int, string>
() -> void             →  System.Action
(str) -> void          →  System.Action<string>
```

**Design Pattern:**
- C# has two delegate families: `Func<T>` (returns value) and `Action` (returns void)
- `Func` always has the return type as the **last** type argument
- The method uses `.Append(returnType)` to add it at the end

---

### 5. AST Type Annotation Mapping: `MapType(TypeAnnotation? type)`

This method handles types from the **parser's AST** (before semantic analysis).

```csharp
public TypeSyntax MapType(TypeAnnotation? type)
{
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
    if (type.TypeArguments.Count > 0)
    {
        var typeArgs = type.TypeArguments.Select(MapType).ToArray();
        var result = GenericName(Identifier(baseTypeName))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));

        return type.IsNullable ? NullableType(result) : result;
    }

    // Handle nullable non-generic types
    var typeSyntax = ParseTypeName(baseTypeName);
    return type.IsNullable ? NullableType(typeSyntax) : typeSyntax;
}
```

**Key Responsibilities:**

1. **Type Alias Expansion**:
   ```sharpy
   type UserId = int
   def get_user(id: UserId?) -> User:
       ...
   ```
   The `UserId?` gets expanded to `int?` by looking up the alias in the symbol table.

2. **Nullable Handling**: The `IsNullable` flag on `TypeAnnotation` is checked at every level. Generic types can be nullable: `list[int]?`.

3. **Recursive Generics**: Type arguments are recursively mapped, supporting nested generics like `list[dict[str, set[int]]]`.

**Connection to Symbol Table:**
- Uses `_context.SymbolTable.LookupTypeAlias()` to resolve type aliases
- This is why `TypeMapper` needs the `CodeGenContext` dependency

---

### 6. Type Name Translation: `GetMappedTypeName(string sharpyTypeName)`

```csharp
private string GetMappedTypeName(string sharpyTypeName)
{
    // Check if it's a built-in type
    if (_builtinTypeMap.TryGetValue(sharpyTypeName, out var csharpType))
        return csharpType;

    // Check if it's a known builtin from the registry
    if (_context.IsBuiltinType(sharpyTypeName))
        return $"Sharpy.{sharpyTypeName}";

    // User-defined types keep their original name
    return sharpyTypeName;
}
```

**Three-Tier Lookup:**

1. **Static Built-ins** (`_builtinTypeMap`): Fast dictionary lookup for primitives and collections
   - `int` → stays as `int` (handled via predefined tokens)
   - `list` → `global::Sharpy.Core.List`

2. **Runtime Registry**: Dynamic builtin types registered at runtime
   - Returns `Sharpy.{typeName}` for types from the runtime library

3. **User Types**: Passed through unchanged
   - `MyClass` → `MyClass`
   - Name mangling (snake_case → PascalCase) happens elsewhere in `RoslynEmitter`

---

### 7. Type Inference: `InferElementType(IEnumerable<Expression> expressions)`

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
        return MapTypeFromInferredType(inferredType);

    return PredefinedType(Token(SyntaxKind.ObjectKeyword));
}
```

**Use Case:**
When translating collection literals without explicit type annotations:
```sharpy
x = [1, 2, 3]  # Infers list[int]
y = [1, "two", 3.0]  # Falls back to list[object]
```

**Algorithm:**
1. Infer type of first element
2. Check if all elements match that type
3. If yes: use inferred type; if no: fall back to `object`

**Limitations:**
This is a **simple heuristic** for v0.1. It doesn't handle:
- Type widening (int → long → float)
- Common base types (Dog and Cat → Animal)
- Union types

These could be improved with semantic type information in future versions.

---

### 8. Helper: `InferExpressionType(Expression expr)`

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

**Float Literal Handling:**
Sharpy supports typed float literals like C#:
- `3.14f` → `float`
- `3.14m` → `decimal`
- `3.14` → `double` (default)

The suffix is stored in the `FloatLiteral` AST node and examined here.

---

### 9. Expression-to-Type Conversion: `MapTypeFromExpression(Expression expr)`

```csharp
public TypeSyntax MapTypeFromExpression(Expression expr)
{
    if (expr is Identifier id)
    {
        var annotation = new TypeAnnotation { Name = id.Name };
        return MapType(annotation);
    }

    // Handle nested generic types (e.g., Box[int])
    if (expr is IndexAccess indexAccess && indexAccess.Object is Identifier nestedTypeName)
    {
        var typeArgs = MapTypeArgumentsFromExpression(indexAccess.Index);
        return GenericName(NameMangler.ToPascalCase(nestedTypeName.Name))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
    }

    return PredefinedType(Token(SyntaxKind.ObjectKeyword));
}
```

**Use Case:**
In Sharpy, generic instantiation can look like this:
```sharpy
class Box[T]:
    value: T

# Instantiation uses index syntax
my_box = Box[int](42)
```

When parsed, `Box[int]` becomes:
- `Box` → `Identifier`
- `[int]` → `IndexAccess` with `int` as an expression

This method converts the expression `int` into a type syntax node.

**Name Mangling:**
Notice `NameMangler.ToPascalCase(nestedTypeName.Name)` converts `box` → `Box` for C# conventions.

---

### 10. Tuple Type Mapping

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

    return GenericName("System.ValueTuple")
        .WithTypeArgumentList(TypeArgumentList(SeparatedList(elementTypes)));
}
```

**Translation Examples:**
```
()           →  System.ValueTuple
(int,)       →  int  (degenerates to single type)
(int, str)   →  System.ValueTuple<int, string>
```

**Design Decision:**
Single-element tuples aren't real tuples in C#—they degenerate to just the element type. This matches Python's behavior where `(42,)` is just `42` if not explicitly marked as a tuple.

---

## Dependencies

### Internal Dependencies

1. **CodeGenContext** (`_context`):
   - Provides `SymbolTable` for type alias lookups
   - Provides `IsBuiltinType()` for runtime type registry checks
   - Location: `src/Sharpy.Compiler/CodeGen/CodeGenContext.cs`

2. **PrimitiveCatalog** (from `Sharpy.Compiler.Semantic`):
   - Source of truth for primitive type mappings
   - Used during static initialization
   - Location: `src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs`

3. **NameMangler**:
   - Converts snake_case → PascalCase for type names
   - Used in `MapTypeFromExpression`
   - Location: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

4. **AST Types** (from `Sharpy.Compiler.Parser.Ast`):
   - `TypeAnnotation`, `FunctionType`, `TupleType`
   - `Expression` and all literal types
   - Location: `src/Sharpy.Compiler/Parser/Ast/`

5. **Semantic Types** (from `Sharpy.Compiler.Semantic`):
   - `SemanticType` hierarchy (BuiltinType, GenericType, etc.)
   - Location: `src/Sharpy.Compiler/Semantic/SemanticType.cs`

### External Dependencies

1. **Roslyn (Microsoft.CodeAnalysis.CSharp)**:
   - `SyntaxFactory` for creating syntax nodes
   - `TypeSyntax` and all type syntax node types
   - This is the **output format** of all TypeMapper methods

2. **.NET System Types**:
   - Maps to `System.Func`, `System.Action`, `System.ValueTuple`

---

## Patterns and Design Decisions

### 1. Pattern: Static Catalog + Instance Methods

```csharp
private static readonly Dictionary<string, string> _builtinTypeMap;  // Shared
private readonly CodeGenContext _context;  // Per-instance

static TypeMapper() { ... }  // Initialize shared data once
public TypeMapper(CodeGenContext context) { ... }  // Inject instance deps
```

**Why?**
- Built-in type mappings are **immutable and universal**—no need to recreate for each instance
- Context (symbol table, type registry) is **per-compilation**—different projects may have different symbols

### 2. Pattern: Recursive Type Translation

Almost every method follows this pattern:
1. Check base case (primitive, null, etc.)
2. If complex type (generic, function, tuple), recursively process components
3. Combine results using Roslyn syntax builders

**Example:**
```
list[dict[str, int?]]
  ↓ MapSemanticType(GenericType)
    ↓ MapSemanticType(GenericType) for dict
      ↓ MapSemanticType(BuiltinType) for str
      ↓ MapSemanticType(NullableType) for int?
        ↓ MapSemanticType(BuiltinType) for int
```

### 3. Pattern: Fail-Safe Fallbacks

Every mapping method has an **escape hatch**:
```csharp
_ => PredefinedType(Token(SyntaxKind.ObjectKeyword))
```

This prevents crashes during code generation. If something goes wrong, you get `object` instead of a compiler crash. The trade-off is runtime type errors instead of compile-time errors—but those should be caught by semantic analysis anyway.

### 4. Design Decision: `global::` Namespace Prefix

For runtime types, the code uses `global::Sharpy.Core.List` instead of `Sharpy.Core.List`.

**Rationale:**
```csharp
namespace Sharpy;  // User's namespace

class MyClass
{
    // Without global::, this would be ambiguous!
    // Does "Sharpy.Core.List" mean global Sharpy.Core or local Sharpy.Core?
    List<int> items;
}
```

The `global::` prefix guarantees we always reference the **runtime library**, not a user namespace.

### 5. Design Decision: Type Parameter Pass-Through

```csharp
TypeParameterType typeParam => IdentifierName(typeParam.Name)
```

Type parameters (like `T` in `class Box[T]`) are passed through **unchanged**. This is correct because:
- In C# generic declarations, type parameters are just identifiers
- Name mangling doesn't apply (you don't want `Box[t]` to become `Box<T>`)
- They're already validated by semantic analysis to exist in scope

---

## Debugging Tips

### 1. Trace Type Translations

When debugging incorrect C# output, add breakpoints in:
- `MapSemanticType` for types from semantic analysis
- `MapType` for types from AST annotations

Set a conditional breakpoint on the type name you're investigating:
```csharp
// In MapSemanticType
if (type is BuiltinType bt && bt.Name == "problematic_type")
    Console.WriteLine($"Mapping {bt.Name}");
```

### 2. Check the Built-in Type Map

If a type isn't being translated correctly, inspect `_builtinTypeMap` at runtime:
```csharp
// In immediate window during debugging
_builtinTypeMap.ContainsKey("list")  // Should be true
_builtinTypeMap["list"]  // Should be "global::Sharpy.Core.List"
```

### 3. Verify Upstream Semantic Types

If `MapSemanticType` receives `null` or `UnknownType`, the problem is **upstream** in semantic analysis. The type checker should have assigned a concrete type.

Add an assertion in debug builds:
```csharp
public TypeSyntax MapSemanticType(SemanticType type)
{
    System.Diagnostics.Debug.Assert(
        type is not UnknownType,
        "Semantic analysis should resolve all unknown types");

    return type switch { ... };
}
```

### 4. Inspect Generated Roslyn Syntax

Use `.ToFullString()` on any `TypeSyntax` to see the generated C# code:
```csharp
var typeSyntax = mapper.MapSemanticType(someType);
Console.WriteLine(typeSyntax.ToFullString());  // Prints: "global::Sharpy.Core.List<int>"
```

### 5. Common Pitfalls

**Problem:** Generic types render as `List<>` (empty type arguments)
**Cause:** Type arguments weren't recursively mapped
**Fix:** Ensure `Select(MapSemanticType)` is called on all type argument collections

**Problem:** User types get prefixed with `Sharpy.`
**Cause:** `_context.IsBuiltinType()` incorrectly returns true
**Fix:** Check the builtin type registry in `CodeGenContext`

**Problem:** Function types map to `System.Func<void>` instead of `System.Action`
**Cause:** Void check uses wrong condition
**Fix:** Ensure `funcType.ReturnType is VoidType` check happens before Func mapping

---

## Contribution Guidelines

### What Changes Belong Here?

**✅ Appropriate Changes:**

1. **Adding new type mappings**:
   ```csharp
   // Example: Add byte type
   _builtinTypeMap["byte"] = "byte";
   _builtinTypeMap["bytes"] = "byte[]";
   ```

2. **Supporting new type constructs**:
   - Union types: `int | str` → C# with discriminated unions
   - Intersection types: `A & B`
   - Type constraints: `T where T : IComparable`

3. **Improving type inference**:
   - Better element type inference for heterogeneous collections
   - Widening rules (int → long → double)
   - Common base type detection

4. **Bug fixes**:
   - Incorrect nullable handling
   - Missing generic type argument mappings
   - Wrong delegate type selection

**❌ Changes That Don't Belong Here:**

1. **Type checking logic**: That belongs in `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
2. **Type resolution**: That's `src/Sharpy.Compiler/Semantic/TypeResolver.cs`
3. **Name mangling**: That's `src/Sharpy.Compiler/CodeGen/NameMangler.cs`
4. **Expression code generation**: That's `src/Sharpy.Compiler/CodeGen/RoslynEmitter.*.cs`

### Adding a New Type Mapping

To add support for a new built-in type:

1. **Register in `PrimitiveCatalog`** (if it's a primitive):
   ```csharp
   // In src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs
   primitives.Add("byte", new PrimitiveInfo("byte", "byte"));
   ```

2. **Add special case** (if needed) in `MapSemanticType`:
   ```csharp
   BuiltinType builtin when type == SemanticType.Byte =>
       PredefinedType(Token(SyntaxKind.ByteKeyword)),
   ```

3. **Test it**:
   ```csharp
   // In tests
   var mapper = new TypeMapper(context);
   var result = mapper.MapSemanticType(SemanticType.Byte);
   Assert.Equal("byte", result.ToFullString());
   ```

### Adding New Collection Types

To add a new collection type like `deque`:

1. **Add to built-in map**:
   ```csharp
   _builtinTypeMap["deque"] = "global::Sharpy.Core.Deque";
   ```

2. **Implement the runtime type** in `src/Sharpy.Core/`

3. **Add helper method** (optional):
   ```csharp
   public TypeSyntax CreateDequeType(TypeSyntax elementType)
   {
       return CreateCollectionType("deque", elementType);
   }
   ```

### Testing Guidelines

All changes should include tests in `tests/Sharpy.Compiler.Tests/CodeGen/TypeMapperTests.cs`:

```csharp
[Fact]
public void MapSemanticType_WithNewType_ReturnsCorrectCSharpType()
{
    var context = new CodeGenContext(...);
    var mapper = new TypeMapper(context);

    var sharpyType = SemanticType.YourNewType;
    var result = mapper.MapSemanticType(sharpyType);

    Assert.Equal("ExpectedCSharpType", result.ToFullString());
}
```

### Performance Considerations

- **Keep `_builtinTypeMap` static**: It's accessed frequently, don't recreate it
- **Use pattern matching**: C#'s switch expressions are optimized for type patterns
- **Avoid unnecessary allocations**: Reuse `SeparatedList` and `TypeArgumentList` instead of creating arrays

---

## Cross-References

### Related Files in CodeGen Pipeline

- **[RoslynEmitter.CompilationUnit.md](RoslynEmitter.CompilationUnit.md)**: Uses TypeMapper for namespace and class declarations
- **[RoslynEmitter.Expressions.md](RoslynEmitter.Expressions.md)**: Uses TypeMapper for cast expressions and type checks
- **[RoslynEmitter.ClassMembers.md](RoslynEmitter.ClassMembers.md)**: Uses TypeMapper for field/property/method types
- **[CodeGenContext.md](CodeGenContext.md)**: Provides symbol table and type registry to TypeMapper
- *NameMangler.cs* (not yet documented): Handles snake_case → PascalCase conversions

### Upstream Dependencies (Semantic Analysis)

- *PrimitiveCatalog.cs*: Defines all primitive types and their C# mappings
- *SemanticType.cs*: Defines the `SemanticType` hierarchy that TypeMapper consumes
- *TypeResolver.cs*: Resolves type names to `SemanticType` instances
- *TypeChecker.cs*: Validates type compatibility before code generation

### Language Specifications

- **[type_annotations.md](../../language_specification/type_annotations.md)**: Defines Sharpy's type annotation syntax
- **[type_hierarchy.md](../../language_specification/type_hierarchy.md)**: Explains Sharpy's type system structure
- **[type_casting.md](../../language_specification/type_casting.md)**: Type conversion rules (affects cast expression mapping)

---

## Summary

TypeMapper is a **pure translation layer**—it doesn't make decisions about what types are valid, it just converts them to C# syntax. Think of it as a dictionary with complex lookup rules:

- Input: Sharpy type (from AST or semantic analysis)
- Output: Roslyn TypeSyntax (C# syntax tree node)
- Side effects: None (pure function)

The key insight is that Sharpy's type system is **isomorphic** to C#'s in many ways:
- Both have primitives, generics, nullables, tuples, and functions
- TypeMapper exploits these similarities for straightforward translation
- Where they differ (e.g., Sharpy collections), it bridges to runtime library types

When debugging type-related issues, always ask: **"Did semantic analysis produce the right SemanticType?"** If yes, TypeMapper should translate it correctly. If no, the bug is upstream.
