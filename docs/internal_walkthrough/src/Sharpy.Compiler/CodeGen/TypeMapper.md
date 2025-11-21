# Walkthrough: TypeMapper.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

---

## 1. Overview

**What does this file do?**

`TypeMapper.cs` is the bridge between Sharpy's type system and C#'s type system. It translates Sharpy type annotations (like `list[int]`, `str`, `dict[str, int]`) into their C# equivalents using Roslyn's syntax tree API. This is a critical component of the code generation pipeline, ensuring that every Sharpy type has a valid C# representation in the generated code.

**Role in the overall project:**

When the Sharpy compiler generates C# code from Sharpy source, it needs to know:
- `str` → `string` (C#)
- `list[int]` → `Sharpy.Core.List<int>` (C#)
- `dict[str, int]` → `Sharpy.Core.Dict<string, int>` (C#)
- `int | None` → `int?` (nullable C#)

This file handles all these mappings, working within the code generation phase after the semantic analyzer has validated the types.

**Pipeline context:**
```
Source Code → Lexer → Parser → Semantic Analyzer → Code Generator (uses TypeMapper) → C# Code → .NET Assembly
```

---

## 2. Class/Type Structure

### Main Class: `TypeMapper`

```csharp
public class TypeMapper
{
    private readonly CodeGenContext _context;
    private static readonly Dictionary<string, string> _builtinTypeMap;
    
    public TypeMapper(CodeGenContext context)
}
```

**Components:**

1. **`_context` field** - A reference to the code generation context that provides access to:
   - Symbol tables
   - Registered builtin types
   - Name mangling utilities
   - Other code generation state

2. **`_builtinTypeMap` static dictionary** - A lookup table for core type mappings. This is static because the mappings are the same across all compilation units.

**Design rationale:**
- The class is designed to be instantiated per compilation context, allowing it to access context-specific type information
- The static type map provides fast lookups for common types without repeated allocations
- All methods return Roslyn `TypeSyntax` objects, which can be directly inserted into generated C# code

---

## 3. Key Functions/Methods

### 3.1 Built-in Type Mappings (`_builtinTypeMap`)

```csharp
private static readonly Dictionary<string, string> _builtinTypeMap = new()
{
    { "int", "int" },
    { "str", "string" },
    { "list", "Sharpy.Core.List" },
    { "dict", "Sharpy.Core.Dict" },
    { "None", "void" },
    // ... more mappings
};
```

**What it does:**
- Defines the fundamental type mappings between Sharpy and C#
- Handles both primitive types (int, bool, etc.) and Sharpy runtime types (list, dict, set)

**Important mappings to note:**
- `str` → `string` (uses C# string directly for better .NET interop, not `Sharpy.Core.Str`)
- `None` → `void` (Sharpy's None type is equivalent to void in return positions)
- `list`, `dict`, `set` → `Sharpy.Core.*` (collections use Sharpy's wrapper types)
- Supports both Sharpy-style (`str`) and C#-style (`string`) type names

**Design decision:** Using C# `string` instead of a custom wrapper maximizes interoperability with .NET libraries while maintaining Pythonic syntax in Sharpy code.

---

### 3.2 `MapType(TypeAnnotation? type)` - The Core Mapping Method

```csharp
public TypeSyntax MapType(TypeAnnotation? type)
```

**What it does:**
This is the primary entry point for type mapping. It converts a Sharpy `TypeAnnotation` (from the AST) into a Roslyn `TypeSyntax` that can be embedded in generated C# code.

**Parameters:**
- `type`: The Sharpy type annotation to map (nullable - if null, defaults to `object`)

**Return value:**
- A `TypeSyntax` representing the C# equivalent of the Sharpy type

**Key implementation details:**

1. **Null handling:**
   ```csharp
   if (type == null)
       return PredefinedType(Token(SyntaxKind.ObjectKeyword));
   ```
   When no type is specified, defaults to `object` (dynamic typing fallback)

2. **Generic type handling:**
   ```csharp
   if (type.TypeArguments.Count > 0)
   {
       var typeArgs = type.TypeArguments.Select(MapType).ToArray();
       var result = GenericName(Identifier(baseTypeName))
           .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArgs)));
   ```
   - Recursively maps generic type arguments (e.g., `list[int]` → type arguments include `int`)
   - Constructs proper C# generic syntax: `List<int>`, `Dict<string, int>`

3. **Nullable type handling:**
   ```csharp
   return type.IsNullable ? NullableType(result) : result;
   ```
   - If the Sharpy type is marked as nullable (e.g., `int | None`), wraps the C# type with `?`
   - Produces `int?`, `string?`, etc.

**Usage example:**
```csharp
// Sharpy: x: list[int]
var annotation = new TypeAnnotation("list", [new TypeAnnotation("int")]);
var csType = typeMapper.MapType(annotation);
// Result: Sharpy.Core.List<int>
```

---

### 3.3 `GetMappedTypeName(string sharpyTypeName)` - Type Name Resolution

```csharp
private string GetMappedTypeName(string sharpyTypeName)
```

**What it does:**
Resolves a Sharpy type name to its C# equivalent string representation. This is a three-tier lookup:

1. **Built-in types** (from `_builtinTypeMap`)
2. **Registered builtins** (from the context registry)
3. **User-defined types** (pass through unchanged)

**Algorithm:**
```csharp
// Tier 1: Check built-in map
if (_builtinTypeMap.TryGetValue(sharpyTypeName, out var csharpType))
    return csharpType;

// Tier 2: Check registry
if (_context.IsBuiltinType(sharpyTypeName))
    return $"Sharpy.{sharpyTypeName}";

// Tier 3: User-defined type
return sharpyTypeName;
```

**Important notes:**
- User-defined types keep their original names (name mangling happens separately in the code generator)
- Registry-based builtins are placed in the `Sharpy` namespace
- This separation allows the type system to be extensible

**Example flows:**
- `"int"` → `"int"` (built-in map)
- `"MyClass"` → `"MyClass"` (user-defined, unchanged)
- `"CustomBuiltin"` (if registered) → `"Sharpy.CustomBuiltin"`

---

### 3.4 `MapFunctionType(FunctionType funcType)` - Function Type Mapping

```csharp
public TypeSyntax MapFunctionType(Parser.Ast.FunctionType funcType)
```

**What it does:**
Converts Sharpy function types to C# delegate types using `System.Action` (for void-returning) or `System.Func` (for value-returning).

**Key implementation details:**

1. **Void returns → Action:**
   ```csharp
   if (IsVoidType(funcType.ReturnType))
   {
       if (paramTypes.Length == 0)
           return ParseTypeName("System.Action");
       
       return GenericName("System.Action")
           .WithTypeArgumentList(TypeArgumentList(SeparatedList(paramTypes)));
   }
   ```
   - No parameters: `Action`
   - With parameters: `Action<T1, T2, ...>`

2. **Non-void returns → Func:**
   ```csharp
   var allTypes = paramTypes.Append(returnType).ToArray();
   return GenericName("System.Func")
       .WithTypeArgumentList(TypeArgumentList(SeparatedList(allTypes)));
   ```
   - The return type is the **last** type parameter in `Func`
   - `Func<int>` = no params, returns int
   - `Func<string, int>` = takes string, returns int
   - `Func<string, int, bool>` = takes string and int, returns bool

**Example mappings:**
- `() -> None` → `System.Action`
- `(int, str) -> None` → `System.Action<int, string>`
- `(int) -> str` → `System.Func<int, string>`
- `(int, int) -> int` → `System.Func<int, int, int>`

---

### 3.5 `MapTupleType(TupleType tupleType)` - Tuple Type Mapping

```csharp
public TypeSyntax MapTupleType(Parser.Ast.TupleType tupleType)
```

**What it does:**
Maps Sharpy tuple types to C# `ValueTuple` types.

**Special cases handled:**

1. **Empty tuple:**
   ```csharp
   if (tupleType.ElementTypes.Count == 0)
       return ParseTypeName("System.ValueTuple");
   ```
   Maps to `System.ValueTuple` (unit type)

2. **Single element:**
   ```csharp
   if (elementTypes.Length == 1)
       return elementTypes[0];
   ```
   A single-element tuple is just the element type (not a tuple at all)
   - This matches Python semantics: `(x,)` with one element is different from `(x)` which is just `x`

3. **Multiple elements:**
   ```csharp
   return GenericName("System.ValueTuple")
       .WithTypeArgumentList(TypeArgumentList(SeparatedList(elementTypes)));
   ```
   - `(int, str)` → `ValueTuple<int, string>`
   - `(int, int, bool)` → `ValueTuple<int, int, bool>`

**Why ValueTuple?**
- Value types (no heap allocation)
- Structural equality (tuples with same values are equal)
- Deconstruction support
- Better performance than reference-based `Tuple<>`

---

### 3.6 Collection Type Helpers

#### `CreateCollectionType(string collectionName, TypeSyntax elementType)`

```csharp
public TypeSyntax CreateCollectionType(string collectionName, TypeSyntax elementType)
```

**What it does:**
Creates a generic collection type (list, set) with the specified element type.

**Usage:**
```csharp
var intType = PredefinedType(Token(SyntaxKind.IntKeyword));
var listType = CreateCollectionType("list", intType);
// Result: Sharpy.Core.List<int>
```

**When it's used:**
- List comprehensions: `[x for x in range(10)]` needs to infer `list[int]`
- Set literals: `{1, 2, 3}` becomes `set[int]`
- When the compiler needs to construct collection types programmatically

#### `CreateDictType(TypeSyntax keyType, TypeSyntax valueType)`

```csharp
public TypeSyntax CreateDictType(TypeSyntax keyType, TypeSyntax valueType)
```

**What it does:**
Creates a dictionary type with specified key and value types.

**Direct mapping:**
- Always creates `Sharpy.Core.Dict<TKey, TValue>`
- No need for name lookup since the type is hardcoded

**Usage:**
```csharp
var keyType = PredefinedType(Token(SyntaxKind.StringKeyword));
var valueType = PredefinedType(Token(SyntaxKind.IntKeyword));
var dictType = CreateDictType(keyType, valueType);
// Result: Sharpy.Core.Dict<string, int>
```

---

### 3.7 Type Inference: `InferElementType(IEnumerable<Expression> expressions)`

```csharp
public TypeSyntax InferElementType(IEnumerable<Expression> expressions)
```

**What it does:**
Attempts to infer the element type of a collection from its literal elements.

**Algorithm:**

1. **Empty collection:** defaults to `object`
   ```csharp
   if (exprs.Count == 0)
       return PredefinedType(Token(SyntaxKind.ObjectKeyword));
   ```

2. **Homogeneous types:** Use the inferred type
   ```csharp
   var inferredType = InferExpressionType(firstExpr);
   if (exprs.All(e => TypesMatch(InferExpressionType(e), inferredType)))
       return MapTypeFromInferredType(inferredType);
   ```

3. **Heterogeneous types:** Fall back to `object`
   ```csharp
   return PredefinedType(Token(SyntaxKind.ObjectKeyword));
   ```

**Example scenarios:**
- `[1, 2, 3]` → all integers → `List<int>`
- `["a", "b"]` → all strings → `List<string>`
- `[1, "a"]` → mixed types → `List<object>`
- `[]` → empty → `List<object>`

**Supporting methods:**

**`InferExpressionType(Expression expr)`** - Simple literal-based type inference:
```csharp
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
    // ... more cases
};
```

**Limitations (noted in comments):**
- "For v0.5, we'll use a simple heuristic"
- Only handles literal types, not complex expressions
- Future versions may use semantic analysis for more sophisticated inference

---

### 3.8 Utility Methods

#### `MakeNullable(TypeSyntax type)`

```csharp
public TypeSyntax MakeNullable(TypeSyntax type)
{
    return NullableType(type);
}
```

Simple wrapper for creating nullable types. Converts `int` → `int?`, etc.

#### `MakeArrayType(TypeSyntax elementType, int rank = 1)`

```csharp
public TypeSyntax MakeArrayType(TypeSyntax elementType, int rank = 1)
```

**What it does:**
Creates C# array types with specified rank (dimensionality).

**Parameters:**
- `elementType`: The type of array elements
- `rank`: Number of dimensions (default = 1)

**Examples:**
- `rank = 1`: `int[]`
- `rank = 2`: `int[,]`
- `rank = 3`: `int[,,]`

**Implementation detail:**
```csharp
var rankSpecifier = ArrayRankSpecifier(
    SeparatedList<ExpressionSyntax>(
        Enumerable.Repeat(OmittedArraySizeExpression(), rank)));
```
Uses `OmittedArraySizeExpression()` for each dimension - this creates the `[]` or `[,]` syntax without specifying sizes.

---

## 4. Dependencies

### External Dependencies (NuGet packages)

1. **Microsoft.CodeAnalysis.CSharp** (Roslyn)
   - `SyntaxFactory` - Factory methods for creating syntax nodes
   - `SyntaxKind` - Enumeration of syntax node types
   - `TypeSyntax`, `GenericNameSyntax`, etc. - Syntax tree node types

### Internal Dependencies (Sharpy codebase)

1. **`Sharpy.Compiler.Parser.Ast`**
   - `TypeAnnotation` - Sharpy type annotations from the AST
   - `FunctionType`, `TupleType` - Specialized type AST nodes
   - `Expression` and subclasses - For type inference

2. **`Sharpy.Compiler.Semantic`**
   - Used indirectly through `CodeGenContext`
   - Symbol tables and type information

3. **`Sharpy.Compiler.CodeGen.CodeGenContext`**
   - `IsBuiltinType()` - Check if a type is registered as builtin
   - Access to symbol tables and compilation state

4. **`Sharpy.Core` (runtime library)**
   - The actual implementation of `List<T>`, `Dict<K,V>`, `Set<T>` that the generated code references

**Dependency flow:**
```
TypeMapper depends on:
    ↓
CodeGenContext → Semantic Analysis → Parser/AST → Lexer
    ↓
Sharpy.Core (runtime - referenced in generated code)
```

---

## 5. Patterns and Design Decisions

### 5.1 Roslyn Syntax Trees

**Design pattern:** Fluent API for building syntax trees

```csharp
GenericName("System.Action")
    .WithTypeArgumentList(
        TypeArgumentList(SeparatedList(paramTypes)));
```

**Why this approach:**
- Type-safe: Compiler catches errors in syntax construction
- Immutable: Syntax nodes are immutable value objects
- Composable: Methods return new nodes, allowing chaining
- Precise: Generated code has exact source text representation

**Alternative (not used):** String concatenation would be error-prone and brittle.

### 5.2 Three-Tier Type Resolution

**Pattern:**
1. Static built-in map (fastest)
2. Context registry (extensible)
3. User-defined pass-through (flexible)

**Benefits:**
- **Performance:** Common types use O(1) dictionary lookup
- **Extensibility:** New builtins can be registered without modifying the map
- **Flexibility:** User types don't need pre-registration

### 5.3 Separation of Concerns

**What TypeMapper does:**
- ✅ Type system mapping (Sharpy types → C# types)
- ✅ Syntax tree construction
- ✅ Generic type argument handling

**What TypeMapper does NOT do:**
- ❌ Name mangling (handled by `NameMangler`)
- ❌ Symbol resolution (handled by semantic analyzer)
- ❌ Type validation (handled by `TypeChecker`)
- ❌ Code emission (handled by `RoslynEmitter`)

This separation makes the code easier to test, maintain, and reason about.

### 5.4 Design Decision: `str` → `string`

```csharp
{ "str", "string" },  // Use C# string instead of Sharpy.Core.Str
```

**Rationale:**
- **Interoperability:** C# strings work seamlessly with .NET libraries
- **Performance:** No wrapper overhead
- **Familiarity:** C# developers expect `string`
- **Immutability:** Both C# and Python strings are immutable

**Trade-off:**
- Pro: Better .NET integration
- Con: Can't add Python-specific string methods as instance methods
- Resolution: Sharpy provides extension methods and global functions for Python-style string operations

### 5.5 ValueTuple vs Tuple

**Decision:** Use `System.ValueTuple` instead of `System.Tuple`

**Benefits:**
- Value type = stack allocation (better performance)
- Structural equality (matches Python tuple semantics)
- Deconstruction syntax: `var (x, y) = tuple;`
- More modern (C# 7+)

---

## 6. Debugging Tips

### 6.1 Inspecting Generated Type Syntax

When debugging type mapping issues, inspect the generated `TypeSyntax`:

```csharp
var typeSyntax = typeMapper.MapType(annotation);
Console.WriteLine(typeSyntax.ToString());
// Or use with normalized whitespace:
Console.WriteLine(typeSyntax.NormalizeWhitespace().ToString());
```

### 6.2 Common Issues and Solutions

**Issue: Generic type appears as `List` instead of `List<int>`**

**Cause:** Type arguments not being mapped

**Debug:**
```csharp
// Check if type arguments exist
if (annotation.TypeArguments.Count > 0)
    Console.WriteLine($"Type args: {annotation.TypeArguments.Count}");

// Check mapped arguments
var mappedArgs = annotation.TypeArguments.Select(MapType).ToList();
foreach (var arg in mappedArgs)
    Console.WriteLine($"Mapped arg: {arg}");
```

**Issue: Unknown type defaults to `object` unexpectedly**

**Cause:** Type not in built-in map and not registered in context

**Debug:**
```csharp
var typeName = annotation.Name;
Console.WriteLine($"Type name: {typeName}");
Console.WriteLine($"In builtin map: {_builtinTypeMap.ContainsKey(typeName)}");
Console.WriteLine($"Is builtin: {_context.IsBuiltinType(typeName)}");
Console.WriteLine($"Mapped to: {GetMappedTypeName(typeName)}");
```

**Issue: Nullable types not generating `?` correctly**

**Cause:** `IsNullable` flag not set on `TypeAnnotation`

**Debug:**
```csharp
Console.WriteLine($"Is nullable: {annotation.IsNullable}");
```

### 6.3 Testing Type Mappings

**Unit test pattern:**
```csharp
[Fact]
public void MapType_ListOfInt_GeneratesCorrectSyntax()
{
    // Arrange
    var context = new CodeGenContext();
    var mapper = new TypeMapper(context);
    var intType = new TypeAnnotation("int");
    var listType = new TypeAnnotation("list", new[] { intType });
    
    // Act
    var result = mapper.MapType(listType);
    
    // Assert
    Assert.Equal("Sharpy.Core.List<int>", result.ToString());
}
```

### 6.4 Tracing Type Resolution Path

Add logging to understand which resolution path is taken:

```csharp
private string GetMappedTypeName(string sharpyTypeName)
{
    if (_builtinTypeMap.TryGetValue(sharpyTypeName, out var csharpType))
    {
        Console.WriteLine($"[TypeMapper] {sharpyTypeName} → {csharpType} (builtin map)");
        return csharpType;
    }
    
    if (_context.IsBuiltinType(sharpyTypeName))
    {
        var result = $"Sharpy.{sharpyTypeName}";
        Console.WriteLine($"[TypeMapper] {sharpyTypeName} → {result} (registry)");
        return result;
    }
    
    Console.WriteLine($"[TypeMapper] {sharpyTypeName} → {sharpyTypeName} (user-defined)");
    return sharpyTypeName;
}
```

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

**Add new built-in type mappings:**
```csharp
// In _builtinTypeMap
{ "bytes", "byte[]" },  // Adding bytes type support
```

**Add new helper methods:**
- Creating specialized collection types
- Handling new type categories (e.g., union types, intersection types)
- Advanced type inference logic

**Extend type inference:**
- Improve `InferExpressionType()` to handle more expression types
- Add context-aware type inference using semantic analysis
- Implement least common ancestor type resolution

### 7.2 What NOT to Change

**❌ Don't break existing mappings** without migration plan:
```csharp
// BAD: Changes existing behavior
{ "str", "Sharpy.Core.Str" }  // This will break all existing code
```

**❌ Don't add name mangling logic here:**
```csharp
// BAD: TypeMapper should not mangle names
return $"__mangled_{sharpyTypeName}";  // Use NameMangler instead
```

**❌ Don't add semantic analysis:**
```csharp
// BAD: TypeMapper should not validate types
if (!IsValidType(type))
    throw new SemanticException(...);  // Use TypeChecker instead
```

### 7.3 Adding Support for New Type Features

**Example: Adding union type support (`int | str`)**

1. **Extend the AST** (`TypeAnnotation` or new `UnionType` node)
2. **Add mapping method:**
   ```csharp
   public TypeSyntax MapUnionType(UnionType unionType)
   {
       // Decision: Map to common base type or use dynamic
       var types = unionType.Types.Select(MapType);
       // ... implementation
   }
   ```

3. **Update `MapType()` to handle the new case:**
   ```csharp
   if (type is UnionType unionType)
       return MapUnionType(unionType);
   ```

4. **Add tests:**
   ```csharp
   [Fact]
   public void MapUnionType_IntOrStr_MapsToObject()
   {
       // Test implementation
   }
   ```

5. **Document the mapping decision** in comments and docs

### 7.4 Testing Requirements

**All new mappings must have:**
1. ✅ Unit tests for basic cases
2. ✅ Unit tests for edge cases (null, empty, complex nesting)
3. ✅ Integration tests showing end-to-end compilation
4. ✅ Documentation of the mapping rationale

**Test categories:**
- Simple types: `int`, `str`, `bool`
- Generic types: `list[int]`, `dict[str, int]`
- Nested generics: `list[dict[str, int]]`
- Nullable types: `int?`, `str?`
- Function types: `(int) -> str`
- Tuple types: `(int, str, bool)`
- Edge cases: `None`, empty collections, circular references

### 7.5 Code Style Guidelines

**Use Roslyn factory methods consistently:**
```csharp
// ✅ Good: Use SyntaxFactory methods
PredefinedType(Token(SyntaxKind.IntKeyword))

// ❌ Bad: Parse strings
ParseTypeName("int")
```

**Prefer pattern matching for type discrimination:**
```csharp
// ✅ Good: Clear pattern matching
return expr switch
{
    IntegerLiteral => "int",
    StringLiteral => "string",
    _ => "object"
};

// ❌ Bad: Nested if-else
if (expr is IntegerLiteral) return "int";
else if (expr is StringLiteral) return "string";
else return "object";
```

**Add XML documentation comments:**
```csharp
/// <summary>
/// Maps a Sharpy type annotation to a C# TypeSyntax
/// </summary>
/// <param name="type">The Sharpy type to map</param>
/// <returns>The equivalent C# type syntax</returns>
public TypeSyntax MapType(TypeAnnotation? type)
```

### 7.6 Performance Considerations

**Do:**
- ✅ Use static dictionary for built-in types (O(1) lookup)
- ✅ Cache frequently used TypeSyntax nodes if needed
- ✅ Avoid string concatenation for type names

**Don't:**
- ❌ Perform expensive operations in mapping (should be fast)
- ❌ Do semantic analysis (should be done earlier in pipeline)
- ❌ Access I/O or external resources

### 7.7 Common Contribution Patterns

**Adding a new primitive type:**
```csharp
// 1. Add to builtin map
{ "newtype", "System.NewType" }

// 2. Add inference support
NewTypeLiteral => "newtype",

// 3. Add to MapTypeFromInferredType
"newtype" => ParseTypeName("System.NewType"),

// 4. Add tests
```

**Improving type inference:**
```csharp
// Enhance InferExpressionType to handle more cases
private string InferExpressionType(Expression expr)
{
    return expr switch
    {
        // Add new cases
        MyNewExpression myExpr => InferFromMyExpression(myExpr),
        // ... existing cases
    };
}
```

---

## Summary

`TypeMapper.cs` is the type system bridge in the Sharpy compiler. It:

- **Translates** Sharpy type annotations to C# syntax trees
- **Handles** primitives, collections, generics, nullables, functions, and tuples
- **Uses** a three-tier resolution system for flexibility and performance
- **Integrates** with Roslyn for type-safe syntax tree construction
- **Supports** both built-in and user-defined types

When working with this file:
- Focus on correctness of type mappings
- Maintain separation from name mangling and semantic analysis
- Test thoroughly with edge cases
- Document mapping decisions and rationales
- Keep performance in mind (use the static map for common types)

This component is foundational to code generation quality - accurate type mapping ensures the generated C# code compiles and runs correctly!
