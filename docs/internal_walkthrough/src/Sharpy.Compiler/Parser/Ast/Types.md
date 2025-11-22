# Walkthrough: Types.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Types.cs`

---

## 1. Overview

`Types.cs` defines the fundamental data structures for representing **type annotations** in the Sharpy Abstract Syntax Tree (AST). This file is at the heart of Sharpy's static type system, providing the building blocks that allow the compiler to understand, validate, and transform type information throughout the compilation pipeline.

### Role in the Compiler Pipeline

```
Source Code → Lexer → Parser → [Types.cs is used here] → Semantic Analyzer → Code Generator
```

When the parser encounters type annotations in Sharpy code (like `x: int`, `list[str]`, or `dict[str, int]`), it creates instances of the types defined in this file to represent them in the AST. These type representations are then used by:

- **Semantic Analyzer**: For type checking and validation
- **Code Generator**: For mapping Sharpy types to C# types
- **Error Reporting**: For providing meaningful type-related error messages

### What This File Does NOT Do

- ❌ Does not perform type checking or validation
- ❌ Does not resolve type names to actual .NET types
- ❌ Does not handle type inference
- ❌ Does not generate code

This file is purely about **data representation** - it defines the structures that *hold* type information extracted from source code.

---

## 2. Class/Type Structure

The file defines **three record types**, all immutable data carriers using C# record syntax:

```
TypeAnnotation     ← Main type representation (int, list[T], Optional[str], etc.)
FunctionType       ← Function signatures ((int, str) -> bool)
TupleType          ← Tuple types (tuple[int, str, float])
```

All three are **records**, meaning:
- They're immutable (properties use `init` setters)
- They have value-based equality semantics
- They support `with` expressions for creating modified copies
- They're perfect for AST nodes (immutability prevents accidental mutations)

---

## 3. Detailed Type Breakdown

### 3.1 TypeAnnotation (Primary Type Representation)

```csharp
public record TypeAnnotation
{
    public string Name { get; init; } = "";
    public List<TypeAnnotation> TypeArguments { get; init; } = new();
    public bool IsNullable { get; init; }
    
    // Source location tracking
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

#### Purpose
Represents any type annotation in Sharpy code. This is the workhorse of the type system.

#### Key Properties

**`Name` (string)**
- The base type name (e.g., `"int"`, `"str"`, `"list"`, `"dict"`, `"MyClass"`)
- For generic types, this is just the generic type name without brackets
- Examples:
  - `int` → `Name = "int"`
  - `list[str]` → `Name = "list"`
  - `MyCustomType` → `Name = "MyCustomType"`

**`TypeArguments` (List&lt;TypeAnnotation&gt;)**
- Generic type parameters/arguments
- Recursive structure allows nested generics
- Examples:
  - `int` → `TypeArguments = []` (empty list)
  - `list[str]` → `TypeArguments = [TypeAnnotation { Name = "str" }]`
  - `dict[str, int]` → `TypeArguments = [TypeAnnotation { Name = "str" }, TypeAnnotation { Name = "int" }]`
  - `list[dict[str, int]]` → Nested structure with recursive TypeAnnotations

**`IsNullable` (bool)**
- Indicates if the type uses nullable syntax (`T?`)
- Maps to C#'s nullable reference/value types
- Examples:
  - `int` → `IsNullable = false`
  - `int?` → `IsNullable = true`
  - `str?` → `IsNullable = true`

**Source Location Properties**
- `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`
- Track where this type annotation appears in source code
- Critical for error reporting with precise locations
- Example: If type checking fails, the compiler can point to exactly where `list[str]` was written

#### Design Pattern: Composite Pattern

TypeAnnotation uses the **Composite Pattern** through its `TypeArguments` property:
- Each TypeAnnotation can contain other TypeAnnotations
- Allows representing arbitrarily nested generic types
- Makes the structure recursive and self-similar

```csharp
// Example: dict[str, list[int]]
var complexType = new TypeAnnotation 
{
    Name = "dict",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation 
        { 
            Name = "list",
            TypeArguments = new List<TypeAnnotation>
            {
                new TypeAnnotation { Name = "int" }
            }
        }
    }
};
```

#### Common Usage Patterns

**Simple Types**
```csharp
// int
new TypeAnnotation { Name = "int" }

// str
new TypeAnnotation { Name = "str" }
```

**Generic Types**
```csharp
// list[str]
new TypeAnnotation 
{ 
    Name = "list",
    TypeArguments = new List<TypeAnnotation> 
    { 
        new TypeAnnotation { Name = "str" }
    }
}

// dict[str, int]
new TypeAnnotation 
{ 
    Name = "dict",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation { Name = "int" }
    }
}
```

**Nullable Types**
```csharp
// int?
new TypeAnnotation 
{ 
    Name = "int",
    IsNullable = true
}

// list[str]?
new TypeAnnotation 
{ 
    Name = "list",
    TypeArguments = new List<TypeAnnotation> 
    { 
        new TypeAnnotation { Name = "str" }
    },
    IsNullable = true
}
```

---

### 3.2 FunctionType (Function Signatures)

```csharp
public record FunctionType
{
    public List<TypeAnnotation> ParameterTypes { get; init; } = new();
    public TypeAnnotation ReturnType { get; init; } = null!;
}
```

#### Purpose
Represents function type signatures, used for:
- Function pointer types
- Callback type annotations
- Lambda type inference hints
- Higher-order function parameters

#### Key Properties

**`ParameterTypes` (List&lt;TypeAnnotation&gt;)**
- Ordered list of parameter types
- Each element is a full TypeAnnotation (supports generics, nullability, etc.)
- Example: `(int, str, list[int])` → 3-element list

**`ReturnType` (TypeAnnotation)**
- The function's return type
- `null!` default indicates it must be set during construction
- Can be any TypeAnnotation, including generic types

#### Examples

```python
# Sharpy code: Function type annotation
callback: (int, str) -> bool
```

```csharp
// Corresponding FunctionType structure
new FunctionType
{
    ParameterTypes = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "int" },
        new TypeAnnotation { Name = "str" }
    },
    ReturnType = new TypeAnnotation { Name = "bool" }
}
```

**More Complex Example**:
```python
# Sharpy: Higher-order function
mapper: (list[int], (int) -> str) -> list[str]
```

```csharp
// FunctionType with nested function type in parameters
new FunctionType
{
    ParameterTypes = new List<TypeAnnotation>
    {
        new TypeAnnotation 
        { 
            Name = "list",
            TypeArguments = new List<TypeAnnotation> 
            { 
                new TypeAnnotation { Name = "int" }
            }
        },
        // Second parameter is itself a function type
        // (This would require FunctionType to be a TypeAnnotation subtype,
        // or a different representation - see "Design Considerations" below)
    },
    ReturnType = new TypeAnnotation 
    { 
        Name = "list",
        TypeArguments = new List<TypeAnnotation>
        {
            new TypeAnnotation { Name = "str" }
        }
    }
}
```

#### Design Considerations

**Current Limitation**: FunctionType is a separate record from TypeAnnotation, which means:
- Function types can't be nested as easily as other generic types
- May require special handling in the semantic analyzer and code generator
- This is a **pragmatic design choice** - function types have different semantics than regular types

**Future Enhancement Possibility**: Could unify FunctionType into TypeAnnotation hierarchy for consistency, but current design keeps function signatures distinct and explicit.

---

### 3.3 TupleType (Tuple Type Annotations)

```csharp
public record TupleType
{
    public List<TypeAnnotation> ElementTypes { get; init; } = new();
}
```

#### Purpose
Represents tuple type annotations with heterogeneous element types.

#### Key Properties

**`ElementTypes` (List&lt;TypeAnnotation&gt;)**
- Ordered list of types for each tuple element
- Unlike generic lists (homogeneous), tuples can have different types per element
- Order matters: `tuple[int, str]` ≠ `tuple[str, int]`

#### Examples

```python
# Sharpy: Tuple types
point: tuple[int, int]
record: tuple[str, int, bool]
nested: tuple[int, list[str], dict[str, int]]
```

```csharp
// tuple[int, int]
new TupleType
{
    ElementTypes = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "int" },
        new TypeAnnotation { Name = "int" }
    }
}

// tuple[str, int, bool]
new TupleType
{
    ElementTypes = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation { Name = "int" },
        new TypeAnnotation { Name = "bool" }
    }
}
```

#### Why Separate from TypeAnnotation?

Tuples have special semantics:
- **Heterogeneous**: Each position has its own type
- **Positional**: Order matters (unlike dicts or sets)
- **Fixed-size**: Number of elements is part of the type
- **Structural typing**: `tuple[int, str]` is always the same type regardless of where it's defined

Having a separate `TupleType` makes these semantics explicit and easier to handle during type checking.

---

## 4. Dependencies

### Internal Dependencies

**Parser** (`src/Sharpy.Compiler/Parser/`)
- Parser.cs instantiates these types when parsing type annotations
- Parser must handle syntax like `list[str]`, `int?`, `(int, str) -> bool`
- These types are the output of type annotation parsing

**Semantic Analyzer** (`src/Sharpy.Compiler/Semantic/`)
- TypeChecker.cs consumes these types to validate type correctness
- NameResolver.cs maps type names to actual .NET types
- Type narrowing logic uses these to track narrowed types

**Code Generator** (`src/Sharpy.Compiler/CodeGen/`)
- TypeMapper.cs maps TypeAnnotation instances to C# type representations
- Needs to handle generics, nullability, and special types

### External Dependencies

**None!** This file has zero external dependencies beyond:
- `System.Collections.Generic` (for `List<T>`)
- .NET base types (`string`, `int`, `bool`)

This makes it a **foundational leaf node** in the dependency graph.

---

## 5. Patterns and Design Decisions

### 5.1 Immutable Records

**Why records?**
- **Value semantics**: Two TypeAnnotations with the same data are equal
- **Immutability**: AST nodes should never change after creation
- **Thread-safety**: Immutable objects are inherently thread-safe
- **Debugging**: Easier to reason about - no hidden state changes

**Example**:
```csharp
var type1 = new TypeAnnotation { Name = "int" };
var type2 = new TypeAnnotation { Name = "int" };

// Record equality: true (same values)
Console.WriteLine(type1 == type2);  // true

// Can create modified copies with 'with' expressions
var nullableType = type1 with { IsNullable = true };
// Original type1 is unchanged
```

### 5.2 Composite Structure (Recursive Types)

TypeAnnotation's `TypeArguments` property creates a **tree structure**:

```
TypeAnnotation: "dict"
├── TypeArguments[0]: "str"
└── TypeArguments[1]: "list"
    └── TypeArguments[0]: "int"
```

This enables:
- Unlimited nesting depth
- Uniform handling of simple and complex types
- Recursive traversal algorithms (e.g., type validation)

### 5.3 Separation of Concerns

Three distinct types for three distinct concepts:
- **TypeAnnotation**: General-purpose types
- **FunctionType**: Function signatures (special semantics)
- **TupleType**: Tuple types (heterogeneous, positional)

**Alternative approach** (not used): Could have made everything a TypeAnnotation with a "kind" discriminator. 

**Why current design is better**:
- Type safety: Compiler catches misuse
- Clarity: Intent is explicit
- Maintainability: Each type handles its specific case

### 5.4 Source Location Tracking

Every TypeAnnotation includes source position:
```csharp
public int LineStart { get; init; }
public int ColumnStart { get; init; }
public int LineEnd { get; init; }
public int ColumnEnd { get; init; }
```

**Why this matters**:
```
Error: Type mismatch
  Expected: list[str]
  Got: list[int]
  at line 42, column 15-24
```

Without location tracking, errors would just say "type mismatch somewhere" - useless for debugging!

### 5.5 Nullable Type Support

`IsNullable` flag handles both:
- Nullable value types: `int?` → C# `int?`
- Nullable reference types: `str?` → C# `string?`

This mirrors C#'s nullable type system, making code generation straightforward.

---

## 6. How These Types Flow Through the Compiler

### Step-by-Step Example

**Sharpy Source Code**:
```python
def process_items(items: list[str]) -> dict[str, int]:
    result: dict[str, int] = {}
    # ...
    return result
```

### Step 1: Parsing
Parser creates TypeAnnotation instances:

```csharp
// Parameter type: list[str]
var paramType = new TypeAnnotation 
{
    Name = "list",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" }
    },
    LineStart = 1,
    ColumnStart = 23,
    LineEnd = 1,
    ColumnEnd = 32
};

// Return type: dict[str, int]
var returnType = new TypeAnnotation
{
    Name = "dict",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation { Name = "int" }
    },
    LineStart = 1,
    ColumnStart = 37,
    LineEnd = 1,
    ColumnEnd = 52
};
```

### Step 2: Semantic Analysis
TypeChecker validates:
- `list` is a valid generic type
- `str` is a valid type argument
- Type arguments match generic constraints
- Return statements return `dict[str, int]`

### Step 3: Code Generation
TypeMapper converts to C#:
- `list[str]` → `Sharpy.Core.List<string>`
- `dict[str, int]` → `Sharpy.Core.Dict<string, int>`

**Generated C# code**:
```csharp
public Sharpy.Core.Dict<string, int> process_items(Sharpy.Core.List<string> items)
{
    Sharpy.Core.Dict<string, int> result = new();
    // ...
    return result;
}
```

---

## 7. Debugging Tips

### 7.1 Visualizing Type Structures

When debugging, you need to see the type tree. Add a helper method:

```csharp
public static class TypeAnnotationExtensions
{
    public static string ToDebugString(this TypeAnnotation type)
    {
        var sb = new StringBuilder();
        sb.Append(type.Name);
        
        if (type.TypeArguments.Any())
        {
            sb.Append('[');
            sb.Append(string.Join(", ", type.TypeArguments.Select(t => t.ToDebugString())));
            sb.Append(']');
        }
        
        if (type.IsNullable)
            sb.Append('?');
            
        return sb.ToString();
    }
}

// Usage:
Console.WriteLine(typeAnnotation.ToDebugString());
// Output: "dict[str, list[int]]?"
```

### 7.2 Common Issues and Solutions

**Issue**: Type arguments don't match expected count
```csharp
// ❌ Wrong: dict should have 2 type arguments
new TypeAnnotation 
{
    Name = "dict",
    TypeArguments = new List<TypeAnnotation> { new TypeAnnotation { Name = "str" } }
}
```

**Solution**: Validate in semantic analyzer:
```csharp
if (typeAnnotation.Name == "dict" && typeAnnotation.TypeArguments.Count != 2)
{
    throw new SemanticException(
        $"dict requires exactly 2 type arguments, got {typeAnnotation.TypeArguments.Count}",
        typeAnnotation.LineStart,
        typeAnnotation.ColumnStart
    );
}
```

**Issue**: Source locations not set
```csharp
// ❌ Bad: No location info
new TypeAnnotation { Name = "int" }
```

**Impact**: Error messages can't point to source location

**Solution**: Always set locations during parsing:
```csharp
new TypeAnnotation 
{ 
    Name = "int",
    LineStart = currentToken.LineStart,
    ColumnStart = currentToken.ColumnStart,
    LineEnd = currentToken.LineEnd,
    ColumnEnd = currentToken.ColumnEnd
}
```

### 7.3 Debugging Type Equality Issues

Records use value-based equality, but **lists don't**:

```csharp
var type1 = new TypeAnnotation 
{
    Name = "list",
    TypeArguments = new List<TypeAnnotation> { new TypeAnnotation { Name = "int" } }
};

var type2 = new TypeAnnotation 
{
    Name = "list",
    TypeArguments = new List<TypeAnnotation> { new TypeAnnotation { Name = "int" } }
};

// ⚠️ This might be FALSE because List<T> uses reference equality
Console.WriteLine(type1 == type2);
```

**Solution**: Implement deep equality or use structural comparison in semantic analyzer.

---

## 8. Contribution Guidelines

### 8.1 When to Modify This File

**Add new type support**:
- Union types: `int | str`
- Intersection types: `A & B`
- Literal types: `Literal["foo", "bar"]`
- Type aliases: `type Point = tuple[int, int]`

**Add new properties**:
- Type variance (covariant, contravariant, invariant)
- Type constraints (bounds, where clauses)
- Optional vs. required (for typed dicts)

### 8.2 How to Add a New Type Kind

**Example: Adding Union Types**

1. **Define the type**:
```csharp
/// <summary>
/// Union type annotation (int | str | None)
/// </summary>
public record UnionType
{
    public List<TypeAnnotation> Types { get; init; } = new();
    
    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

2. **Update parser** to recognize union syntax (`int | str`)

3. **Update semantic analyzer** to handle union type checking

4. **Update code generator** to map unions to C# types (or use discriminated unions pattern)

5. **Add tests** for each component

### 8.3 Best Practices

**DO**:
- ✅ Keep types immutable (use `init` setters)
- ✅ Always track source locations
- ✅ Use descriptive property names
- ✅ Add XML documentation comments
- ✅ Write unit tests for new types
- ✅ Update this walkthrough when making changes

**DON'T**:
- ❌ Add mutable state
- ❌ Add behavior/logic (keep types as data)
- ❌ Break record equality semantics
- ❌ Forget source location tracking
- ❌ Add dependencies on other compiler components

### 8.4 Testing Considerations

When adding new types, test:
1. **Parsing**: Can parser create instances correctly?
2. **Equality**: Do instances with same data compare equal?
3. **Serialization**: Can types be serialized/deserialized if needed?
4. **Location tracking**: Are source positions preserved?

Example test:
```csharp
[Fact]
public void TypeAnnotation_EqualityWorks()
{
    var type1 = new TypeAnnotation { Name = "int" };
    var type2 = new TypeAnnotation { Name = "int" };
    var type3 = new TypeAnnotation { Name = "str" };
    
    Assert.Equal(type1, type2);
    Assert.NotEqual(type1, type3);
}

[Fact]
public void TypeAnnotation_WithNullable()
{
    var type = new TypeAnnotation { Name = "int" };
    var nullableType = type with { IsNullable = true };
    
    Assert.False(type.IsNullable);
    Assert.True(nullableType.IsNullable);
    Assert.Equal("int", nullableType.Name);
}
```

---

## 9. Related Files and Further Reading

### Related AST Files
- **`Expressions.cs`**: Expression AST nodes that use TypeAnnotation
- **`Statements.cs`**: Statement nodes with type annotations (variable declarations)
- **`Declarations.cs`**: Function, class declarations with type signatures
- **`AstBase.cs`**: Base classes/interfaces for all AST nodes

### Files That Use Types.cs
- **`Parser.cs`**: Creates TypeAnnotation instances
- **`TypeChecker.cs`**: Validates type annotations
- **`TypeMapper.cs`**: Maps to C# types
- **`NameResolver.cs`**: Resolves type names to actual types

### Documentation
- **Language Reference** (`docs/specs/language_reference.md`): Sharpy type system specification
- **Type System Design** (`docs/architecture/type_system.md`): Architecture decisions
- **Parser Guide** (`docs/internal_walkthrough/src/Sharpy.Compiler/Parser/`): Parser walkthrough

---

## 10. Quick Reference

### Type Structure Cheat Sheet

| Sharpy Code | TypeAnnotation Structure |
|-------------|--------------------------|
| `int` | `Name = "int"` |
| `str` | `Name = "str"` |
| `int?` | `Name = "int", IsNullable = true` |
| `list[int]` | `Name = "list", TypeArguments = [int]` |
| `dict[str, int]` | `Name = "dict", TypeArguments = [str, int]` |
| `list[dict[str, int]]?` | `Name = "list", TypeArguments = [dict[str, int]], IsNullable = true` |

### Common Operations

**Creating a simple type**:
```csharp
new TypeAnnotation { Name = "int" }
```

**Creating a generic type**:
```csharp
new TypeAnnotation 
{
    Name = "list",
    TypeArguments = new List<TypeAnnotation> { new TypeAnnotation { Name = "int" } }
}
```

**Creating a nullable type**:
```csharp
new TypeAnnotation { Name = "int", IsNullable = true }
```

**Checking if a type is generic**:
```csharp
bool isGeneric = typeAnnotation.TypeArguments.Any();
```

**Traversing nested types**:
```csharp
void VisitType(TypeAnnotation type)
{
    Console.WriteLine(type.Name);
    foreach (var arg in type.TypeArguments)
        VisitType(arg);  // Recursive
}
```

---

## Summary

`Types.cs` is a **foundational file** that defines the data structures for representing type annotations in the Sharpy compiler's AST. It's simple, focused, and critical to the entire type system. Understanding this file is essential for working on any type-related features in the Sharpy compiler.

**Key Takeaways**:
- ✅ Three record types: TypeAnnotation, FunctionType, TupleType
- ✅ Immutable, value-based equality
- ✅ Composite pattern for nested generics
- ✅ Source location tracking for error reporting
- ✅ Zero dependencies, pure data structures
- ✅ Foundation for parser, semantic analyzer, and code generator

**Next Steps**:
- Read `Parser.cs` to see how these types are created
- Read `TypeChecker.cs` to see how they're validated
- Read `TypeMapper.cs` to see how they're mapped to C#
