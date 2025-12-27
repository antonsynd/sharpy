# Walkthrough: TypeMapper.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

---

## 1. Overview

`TypeMapper.cs` is the **translation layer** between Sharpy's type system and C#'s type system. When the Sharpy compiler generates C# code via Roslyn, it needs to convert type annotations like `list[int]`, `str?`, or `dict[str, int]` into their C# equivalents (`global::Sharpy.Core.List<int>`, `string?`, `global::Sharpy.Core.Dict<string, int>`).

**Role in the compiler pipeline:**
```
Sharpy Source → Lexer → Parser (AST with TypeAnnotations) → Semantic Analysis
                                                                    ↓
                                              TypeMapper ← RoslynEmitter ← C# Code
```

This file is primarily used by `RoslynEmitter` during code generation, but it's critical infrastructure that touches every typed construct in the language.

---

## 2. Class/Type Structure

### Main Class: `TypeMapper`

```csharp
public class TypeMapper
{
    private readonly CodeGenContext _context;
    private static readonly Dictionary<string, string> _builtinTypeMap;
    // ...
}
```

**Architecture:**
- **Stateful instance**: Each `TypeMapper` instance holds a reference to `CodeGenContext`, which provides access to the symbol table and builtin registry
- **Static type catalog**: `_builtinTypeMap` is populated once at class initialization and shared across all instances (performance optimization)

**No interfaces or inheritance**: This is a straightforward service class focused on a single responsibility.

---

## 3. Key Functions/Methods

### 3.1 Static Constructor (Initialization)

```csharp
static TypeMapper()
{
    _builtinTypeMap = new Dictionary<string, string>();
    
    // Populate from PrimitiveCatalog
    foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
    {
        _builtinTypeMap.TryAdd(name, info.CSharpName);
    }
    
    // Add collection types
    _builtinTypeMap["list"] = "global::Sharpy.Core.List";
    _builtinTypeMap["dict"] = "global::Sharpy.Core.Dict";
    // ...
}
```

**What it does:**
- Runs once when the class is first loaded
- Builds a lookup table mapping Sharpy type names to C# type names

**Key design decisions:**
- Uses `PrimitiveCatalog` as the source of truth for primitives (e.g., `int` → `int`, `str` → `string`)
- Uses `global::` prefix for Sharpy runtime types to avoid namespace conflicts (critical when user code creates a namespace called "Sharpy")

**Why static?**: Type mappings are constant across all compilation contexts, so we initialize once and reuse.

---

### 3.2 `MapType(TypeAnnotation? type)` - The Core Mapper

```csharp
public TypeSyntax MapType(TypeAnnotation? type)
{
    if (type == null)
        return PredefinedType(Token(SyntaxKind.ObjectKeyword));
    
    var baseTypeName = GetMappedTypeName(type.Name);
    
    if (type.TypeArguments.Count > 0)
    {
        // Handle generics: list[int] → List<int>
        // ...
    }
    
    return type.IsNullable ? NullableType(typeSyntax) : typeSyntax;
}
```

**Purpose:** Converts a Sharpy `TypeAnnotation` (from the AST) into a Roslyn `TypeSyntax` (C# representation).

**Parameters:**
- `type` - The type annotation from the AST, can be `null` (untyped)

**Return value:** A Roslyn `TypeSyntax` node that can be inserted into generated C# code

**Algorithm:**
1. Handle null (untyped) → default to `object`
2. Map the base type name (`int`, `list`, custom types)
3. If generic, recursively map type arguments and construct generic syntax
4. Apply nullability wrapper if needed (`T` → `T?`)

**Example transformations:**
- `int` → `PredefinedType(IntKeyword)`
- `list[str]` → `GenericName("global::Sharpy.Core.List").WithTypeArgumentList(["string"])`
- `str?` → `NullableType(ParseTypeName("string"))`

---

### 3.3 `GetMappedTypeName(string sharpyTypeName)` - Type Name Resolution

```csharp
private string GetMappedTypeName(string sharpyTypeName)
{
    // 1. Check built-in map
    if (_builtinTypeMap.TryGetValue(sharpyTypeName, out var csharpType))
        return csharpType;
    
    // 2. Check builtin registry (runtime types)
    if (_context.IsBuiltinType(sharpyTypeName))
        return $"Sharpy.{sharpyTypeName}";
    
    // 3. User-defined type - use as-is
    return sharpyTypeName;
}
```

**Three-tier lookup strategy:**

1. **Static built-ins**: Primitives and core collections (fast dictionary lookup)
2. **Runtime built-ins**: Types registered in `BuiltinRegistry` during compilation
3. **User types**: Custom classes/types defined in Sharpy code

**Why this hierarchy?**
- Performance: Most lookups hit the static dictionary
- Extensibility: Runtime registry allows dynamic builtin registration
- Correctness: User types pass through without modification (name mangling happens elsewhere)

---

### 3.4 `MapFunctionType(FunctionType funcType)` - Function Types

```csharp
public TypeSyntax MapFunctionType(FunctionType funcType)
{
    var paramTypes = funcType.ParameterTypes.Select(MapType).ToArray();
    var returnType = MapType(funcType.ReturnType);
    
    if (IsVoidType(funcType.ReturnType))
    {
        // void functions → Action<T1, T2>
        if (paramTypes.Length == 0) return ParseTypeName("System.Action");
        return GenericName("System.Action").WithTypeArgumentList(...);
    }
    
    // Non-void → Func<T1, T2, TResult>
    var allTypes = paramTypes.Append(returnType).ToArray();
    return GenericName("System.Func").WithTypeArgumentList(...);
}
```

**Purpose:** Maps function type annotations to .NET delegate types.

**Mapping rules:**
- `(int, str) -> None` → `System.Action<int, string>`
- `() -> int` → `System.Func<int>`
- `(int) -> str` → `System.Func<int, string>`

**Why Action vs Func?**
- `Action<T...>` for void/None returns (no result)
- `Func<T..., TResult>` for everything else (last type arg is return type)

This follows .NET conventions and allows seamless integration with C# code.

---

### 3.5 `MapTupleType(TupleType tupleType)` - Tuple Handling

```csharp
public TypeSyntax MapTupleType(TupleType tupleType)
{
    if (tupleType.ElementTypes.Count == 0)
        return ParseTypeName("System.ValueTuple");  // Empty tuple
    
    var elementTypes = tupleType.ElementTypes.Select(MapType).ToArray();
    
    if (elementTypes.Length == 1)
        return elementTypes[0];  // Not actually a tuple!
    
    return GenericName("System.ValueTuple").WithTypeArgumentList(...);
}
```

**Edge cases handled:**
- Empty tuple `()` → `System.ValueTuple`
- Single element `(int,)` → Just `int` (Python allows this, but it's not semantically a tuple)
- N-element `(int, str)` → `System.ValueTuple<int, string>`

**Design choice:** Uses `ValueTuple` (structs) instead of `Tuple` (classes) for performance.

---

### 3.6 Type Inference Methods

#### `InferElementType(IEnumerable<Expression> expressions)`

```csharp
public TypeSyntax InferElementType(IEnumerable<Expression> expressions)
{
    // Simple heuristic:
    // - All same literal type → use that type
    // - Otherwise → object
    
    var inferredType = InferExpressionType(firstExpr);
    if (exprs.All(e => TypesMatch(InferExpressionType(e), inferredType)))
        return MapTypeFromInferredType(inferredType);
    
    return PredefinedType(Token(SyntaxKind.ObjectKeyword));
}
```

**Use case:** When compiling untyped list literals like `[1, 2, 3]`, infer that it's `List<int>`.

**Algorithm limitations:**
- **Simple heuristic**: Only works for homogeneous literal collections
- **V0.1 implementation**: Falls back to `object` for mixed types

**Example:**
- `[1, 2, 3]` → Infers `List<int>`
- `["a", "b"]` → Infers `List<string>`
- `[1, "a"]` → Falls back to `List<object>`

#### `InferExpressionType(Expression expr)`

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
        // ...
    };
}
```

**Pattern matching on AST node types:** Uses C# pattern matching to classify expressions.

**Float literal disambiguation:**
- `3.14` → `double`
- `3.14f` → `float`
- `3.14m` → `decimal`

This respects explicit type suffixes from the source code.

---

### 3.7 Utility Methods

#### `CreateCollectionType(string collectionName, TypeSyntax elementType)`

Helper for constructing generic collection types programmatically:
```csharp
typeMapper.CreateCollectionType("list", intType)
// → global::Sharpy.Core.List<int>
```

#### `CreateDictType(TypeSyntax keyType, TypeSyntax valueType)`

Specialized helper for dictionaries (needs two type parameters):
```csharp
typeMapper.CreateDictType(stringType, intType)
// → global::Sharpy.Core.Dict<string, int>
```

#### `MakeNullable(TypeSyntax type)`

Wraps any type in nullable syntax:
```csharp
typeMapper.MakeNullable(intType)
// → int?
```

#### `MakeArrayType(TypeSyntax elementType, int rank = 1)`

Creates C# array types (used for .NET interop):
```csharp
typeMapper.MakeArrayType(intType, 1)  // → int[]
typeMapper.MakeArrayType(intType, 2)  // → int[,]
```

---

## 4. Dependencies

### Internal Dependencies

1. **`CodeGenContext`** (`CodeGen/CodeGenContext.cs`)
   - Provides symbol table access
   - Exposes `IsBuiltinType()` for runtime type checking
   - Maintains compilation state

2. **`PrimitiveCatalog`** (`Semantic/PrimitiveCatalog.cs`)
   - Source of truth for primitive type mappings
   - Provides `(SharpyName, CSharpName)` pairs
   - Examples: `("int", "int")`, `("str", "string")`, `("bool", "bool")`

3. **AST Types** (`Parser/Ast/`)
   - `TypeAnnotation` - Type references in source code
   - `FunctionType` - Function signatures
   - `TupleType` - Tuple type references
   - Expression types (for type inference)

### External Dependencies

1. **Roslyn (`Microsoft.CodeAnalysis.CSharp`)**
   - `TypeSyntax` - The output type for all mapping operations
   - `SyntaxFactory` - Static factory for creating syntax nodes
   - `SyntaxKind` - Token types (e.g., `IntKeyword`, `StringKeyword`)

2. **Sharpy.Core** (indirectly)
   - References runtime types: `List<T>`, `Dict<K,V>`, `Set<T>`
   - These are string references in generated code, not compile-time dependencies

---

## 5. Patterns and Design Decisions

### 5.1 Immutable AST, Separate Type Info

The `TypeMapper` never modifies AST nodes. It **reads** type annotations and **produces** new Roslyn syntax. This follows Sharpy's design principle:

> **Immutable AST** - Semantic info stored in `SemanticInfo` class, not on AST nodes

### 5.2 Use of `global::` Prefix

```csharp
_builtinTypeMap["list"] = "global::Sharpy.Core.List";
```

**Why?** Consider this Sharpy code:

```python
# User creates a namespace collision
namespace Sharpy:
    class List:
        pass

# Later in code
x: list[int] = [1, 2, 3]
```

Without `global::`, the generated C# would try to use `Sharpy.List<int>` (user type) instead of `global::Sharpy.Core.List<int>` (runtime type). The prefix ensures we always reference the correct type.

### 5.3 Recursive Type Mapping

```csharp
var typeArgs = type.TypeArguments.Select(MapType).ToArray();
```

Type arguments are mapped recursively, allowing deeply nested generics:
- `list[dict[str, list[int]]]` works correctly
- Each level calls `MapType()` again

### 5.4 Null Safety

The entire class uses nullable reference types (`TypeAnnotation?`, `string?`) and handles null cases explicitly:

```csharp
if (type == null)
    return PredefinedType(Token(SyntaxKind.ObjectKeyword));
```

Untyped declarations default to `object`, maintaining C# compatibility.

### 5.5 Roslyn Over String Templates

**Never:**
```csharp
return $"List<{elementType}>";  // ❌ String templating
```

**Always:**
```csharp
return GenericName("List")      // ✅ Roslyn syntax trees
    .WithTypeArgumentList(...);
```

This ensures correct syntax tree structure, proper escaping, and IDE tooling support.

---

## 6. Debugging Tips

### 6.1 Inspecting Generated Types

When debugging type mapping issues, dump the generated Roslyn syntax:

```csharp
var typeSyntax = typeMapper.MapType(typeAnnotation);
Console.WriteLine(typeSyntax.ToFullString());  // See the C# output
```

### 6.2 Common Issues

**Issue 1: User type not found**
```
Symptom: Generated C# has "MyClass" but compiler can't find it
Cause: Type might need namespace qualification
Fix: Check if type is in different namespace, add proper using directives
```

**Issue 2: Generic type constraint errors**
```
Symptom: C# compiler error about type constraints
Cause: Sharpy type system allows constructs C# doesn't
Fix: May need to add constraint checking in semantic analysis
```

**Issue 3: Null reference when mapping**
```
Symptom: NullReferenceException in MapType
Cause: TypeAnnotation is null but not handled
Fix: Check calling code, ensure proper null checking
```

### 6.3 Type Mapping Verification

Compare Sharpy → C# mappings:

```csharp
// Test in unit tests
var sharpyType = "list[int]";
var csharpType = typeMapper.MapType(parsedType);
Assert.Equal("global::Sharpy.Core.List<int>", csharpType.ToString());
```

### 6.4 Breakpoint Locations

Set breakpoints at:
1. `MapType()` entry - See what type is being mapped
2. `GetMappedTypeName()` - See type resolution logic
3. Generic type argument loop - Debug nested types

### 6.5 Logging Strategy

Add logging to trace type resolution:

```csharp
Console.WriteLine($"Mapping Sharpy type: {type.Name}");
var mapped = GetMappedTypeName(type.Name);
Console.WriteLine($"Resolved to C# type: {mapped}");
```

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

**Add a new primitive type:**
1. First add to `PrimitiveCatalog.cs`
2. TypeMapper will pick it up automatically (static constructor)

**Add a new collection type:**
```csharp
// In static constructor
_builtinTypeMap["mytype"] = "global::Sharpy.Core.MyType";
```

**Add a new type mapping rule:**
- Modify `GetMappedTypeName()` if special logic needed
- Otherwise, define in runtime and register in builtin registry

### 7.2 Testing Requirements

When modifying TypeMapper:

1. **Add unit tests** in `Sharpy.Compiler.Tests/CodeGen/TypeMapperTests.cs`
2. **Test edge cases**:
   - Null types
   - Nested generics (3+ levels deep)
   - Nullable generic types (`list[int]?`)
   - Empty collections

3. **Integration tests**: Verify full compilation pipeline works
   ```csharp
   [Fact]
   public void TestNewTypeMapping()
   {
       var source = @"
           x: mytype[int] = ...
       ";
       var result = CompileAndExecute(source);
       Assert.True(result.Success);
   }
   ```

### 7.3 Common Modification Patterns

#### Pattern 1: Adding Collection Support

```csharp
// 1. Add to static map
_builtinTypeMap["queue"] = "global::Sharpy.Core.Queue";

// 2. Add helper method
public TypeSyntax CreateQueueType(TypeSyntax elementType)
{
    return GenericName("global::Sharpy.Core.Queue")
        .WithTypeArgumentList(
            TypeArgumentList(SingletonSeparatedList(elementType)));
}

// 3. Update InferExpressionType if literal syntax exists
QueueLiteral => "queue",
```

#### Pattern 2: Improving Type Inference

Current inference is basic. To improve:

```csharp
// Consider implementing:
private TypeSyntax FindCommonSupertype(List<TypeSyntax> types)
{
    // Find common base type or interface
    // More sophisticated than current "all same or object"
}
```

#### Pattern 3: Adding Type Aliases

```csharp
// If Sharpy adds type aliases (e.g., `type MyInt = int`)
private readonly Dictionary<string, TypeSyntax> _typeAliases;

public void RegisterAlias(string alias, TypeSyntax target)
{
    _typeAliases[alias] = target;
}
```

### 7.4 Code Style

Follow these conventions:

```csharp
// ✅ Good: Descriptive names, clear intent
public TypeSyntax MapFunctionType(FunctionType funcType)

// ❌ Bad: Abbreviations, unclear purpose
public TypeSyntax MapFn(FuncType ft)

// ✅ Good: Early returns for null/special cases
if (type == null)
    return PredefinedType(Token(SyntaxKind.ObjectKeyword));

// ✅ Good: Use pattern matching for expression types
return expr switch
{
    IntegerLiteral => "int",
    StringLiteral => "string",
    _ => "object"
};
```

### 7.5 Performance Considerations

- **Static map lookup is O(1)**: Don't introduce linear searches
- **Recursive calls are bounded**: Type nesting is limited by parser
- **Lazy evaluation**: Don't map types that won't be used

### 7.6 Breaking Changes

**High impact changes** (require careful review):
- Changing primitive type mappings
- Modifying nullability handling
- Changing generic type syntax

**Low impact changes**:
- Adding new helper methods
- Improving type inference heuristics
- Adding new collection types

---

## Summary

`TypeMapper.cs` is a **pure translation service** that bridges Sharpy's type system with C#. It has no business logic—just mapping rules. When you see a Sharpy type in source code, this class answers: "What does that look like in C#?"

**Key takeaways:**
1. Uses a three-tier lookup: static builtins → runtime builtins → user types
2. Always produces Roslyn `TypeSyntax`, never strings
3. Handles generics, nullability, tuples, and functions
4. Type inference is intentionally simple (v0.1)
5. Uses `global::` prefix to avoid namespace collisions

**When you need TypeMapper:**
- Implementing new type-related language features
- Adding new builtin types or collections
- Debugging type-related code generation issues
- Understanding how Sharpy types become C# types

Happy mapping! 🎯
