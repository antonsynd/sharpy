# Walkthrough: Types.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Types.cs`

---

## 1. Overview

The `Types.cs` file defines the fundamental **type annotation data structures** used throughout the Sharpy compiler's Abstract Syntax Tree (AST). These immutable records represent how types are written in source code—they capture the **syntactic form** of type expressions before semantic analysis resolves them into concrete .NET types.

**Key Insight**: This file is about *syntax*, not semantics. A `TypeAnnotation` with `Name = "list"` and `TypeArguments = [TypeAnnotation("int")]` represents the *textual notation* `list[int]` as it appears in source code. The semantic analyzer (`TypeResolver.cs`) later resolves this to the actual `Sharpy.Core.List<int>` type, and the code generator (`TypeMapper.cs`) maps it to the final C# `global::Sharpy.Core.List<int>`.

### Role in the Compiler Pipeline

```
Source Code (.spy) → Lexer (Tokens) → Parser (creates TypeAnnotation) → 
    Semantic Analysis (TypeResolver → SemanticType) → 
    Validation Pipeline → 
    Code Generation (TypeMapper → Roslyn C# syntax)
```

**Upstream Components:**
- **Lexer** (`src/Sharpy.Compiler/Lexer/Lexer.cs`): Produces tokens that Parser reads
- **Parser** (`src/Sharpy.Compiler/Parser/Parser.Types.cs`): Creates `TypeAnnotation` instances

**Downstream Components:**
- **TypeResolver** (`src/Sharpy.Compiler/Semantic/TypeResolver.cs`): Resolves annotations to semantic types
- **TypeChecker** (`src/Sharpy.Compiler/Semantic/TypeChecker.*.cs`): Validates type compatibility
- **TypeMapper** (`src/Sharpy.Compiler/CodeGen/TypeMapper.cs`): Maps to C# Roslyn syntax

**Example Flow:**
```python
def process(items: list[str]) -> int:
    return len(items)
```

1. **Lexer** produces: `IDENTIFIER("list")`, `LBRACKET`, `IDENTIFIER("str")`, `RBRACKET`, ...
2. **Parser** (`Parser.Types.cs` line 14-80) creates:
   ```csharp
   TypeAnnotation { 
       Name = "list", 
       TypeArguments = ImmutableArray.Create(TypeAnnotation { Name = "str" }),
       IsNullable = false
   }
   ```
3. **TypeResolver** (`TypeResolver.cs` line 25-90) resolves to:
   ```csharp
   GenericType { 
       Name = "list", 
       TypeArguments = [BuiltinType("str")] 
   }
   ```
4. **TypeMapper** (`TypeMapper.cs` line 87-97) emits Roslyn:
   ```csharp
   GenericName("global::Sharpy.Core.List")
       .WithTypeArgumentList(TypeArgumentList("string"))
   ```

---

## 2. Class/Type Structure

The file defines three immutable record types, each serving a distinct purpose in representing type syntax. All types include source location tracking for precise error reporting.

### 2.1 `TypeAnnotation` (Lines 8-24)

**The workhorse of the type system.** Represents general type annotations including:
- Simple types: `int`, `str`, `bool`
- Generic types: `list[int]`, `dict[str, int]`
- Nested generics: `list[dict[str, list[int]]]`
- Optional types: `int?`, `list[str]?`

```csharp
public record TypeAnnotation
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeAnnotation> TypeArguments { get; init; } 
        = ImmutableArray<TypeAnnotation>.Empty;
    public bool IsNullable { get; init; }  // T? syntax
    
    // Source location tracking (for error messages)
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    
    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}
```

**Key Properties:**
- **`Name`**: The base type identifier (e.g., `"int"`, `"list"`, `"MyClass"`)
- **`TypeArguments`**: For generic types, the type parameters. Uses `ImmutableArray<TypeAnnotation>.Empty` for non-generic types
- **`IsNullable`**: Set to `true` when source uses `T?` syntax (see [`docs/language_specification/nullable_types.md`](../../../../../language_specification/nullable_types.md))
- **Location fields** (`Line*/Column*`): Enable precise error messages pointing to exact type annotation in source
- **`Span`**: Optional character offset span for advanced tooling (LSP, IDE integration)

**Design Decision**: Type arguments use `ImmutableArray<TypeAnnotation>` creating a **recursive tree structure** that naturally represents nested generics. This is a key compiler performance optimization—`ImmutableArray` has no heap allocations for small arrays and supports structural sharing.

### 2.2 `FunctionType` (Lines 29-44)

Represents function type annotations using the arrow syntax: `(param_types...) -> return_type`

```csharp
public record FunctionType
{
    public ImmutableArray<TypeAnnotation> ParameterTypes { get; init; } 
        = ImmutableArray<TypeAnnotation>.Empty;
    public TypeAnnotation ReturnType { get; init; } = null!;
    
    // Source location tracking
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    
    /// <summary>
    /// Character offset-based span. May be null if not tracked.
    /// </summary>
    public Text.TextSpan? Span { get; init; }
}
```

**Use Cases** (see [`docs/language_specification/function_types.md`](../../../../../language_specification/function_types.md)):
```python
# Higher-order function taking a callback
def map_values(items: list[int], transform: (int) -> str) -> list[str]:
    return [transform(x) for x in items]

# Function returning another function (currying)
def make_adder(x: int) -> (int) -> int:
    def add(y: int) -> int:
        return x + y
    return add

# Event handler field
class Button:
    on_click: ((Button) -> None)?  # Nullable function type
```

**Important Notes:**
- **`ReturnType` is `null!`**: The null-forgiving operator indicates this is always set during parsing—function types *must* have a return type (even if it's `None`)
- **Parameter names not stored**: Function types are structural. `(int, str) -> bool` and `(x: int, y: str) -> bool` produce the same AST
- **Maps to C# delegates**: `() -> int` → `Func<int>`, `(T) -> None` → `Action<T>`

### 2.3 `TupleType` (Lines 49-52)

Represents heterogeneous tuple type annotations: `tuple[int, str, float]`

```csharp
public record TupleType
{
    public ImmutableArray<TypeAnnotation> ElementTypes { get; init; } 
        = ImmutableArray<TypeAnnotation>.Empty;
}
```

**Important Distinction:**
- **Heterogeneous tuples** (fixed types per position): `tuple[int, str, bool]` → Uses `TupleType`
- **Homogeneous tuples** (variable length, single type): `tuple[int, ...]` → Uses `TypeAnnotation` with name `"tuple"`

**Note**: Unlike `TypeAnnotation` and `FunctionType`, `TupleType` does **not** include source location fields. This is because tuples are typically used as shorthand syntax (e.g., `(int, str)`) and location tracking happens at a different AST level.

**Example:**
```python
# Heterogeneous tuple (uses TupleType)
user_info: tuple[str, int, bool] = ("Alice", 30, True)

# Shorthand syntax (parser converts to TupleType)
point: (float, float) = (3.14, 2.71)

# Homogeneous tuple (uses TypeAnnotation)
numbers: tuple[int, ...] = (1, 2, 3, 4, 5)
```

**C# Mapping**: Maps to `System.ValueTuple<T1, T2, ...>` for performance (struct, no heap allocation).

---

## 3. Key Design Patterns

### 3.1 Immutable Records with Init-Only Properties

All types use C# 9's `record` feature with `init` setters, following Sharpy's **immutable AST** principle:

```csharp
public record TypeAnnotation
{
    public string Name { get; init; } = "";  // Can only be set during initialization
    public ImmutableArray<TypeAnnotation> TypeArguments { get; init; } 
        = ImmutableArray<TypeAnnotation>.Empty;
    // ...
}
```

**Benefits:**
- **Thread-safe**: AST can be shared across compiler phases without locking
- **Structural equality**: Two `TypeAnnotation` records with same values are equal (`==` works intuitively)
- **Predictable**: Once created, a type annotation never changes—no hidden mutations
- **Compiler optimizations**: C# compiler can cache hash codes, optimize record copying

**Pattern in Use:**
```csharp
// Parser creates immutable instances
var typeAnnotation = new TypeAnnotation
{
    Name = "list",
    TypeArguments = ImmutableArray.Create(
        new TypeAnnotation { Name = "int" }
    ),
    IsNullable = true,
    LineStart = token.Line,
    ColumnStart = token.Column
};

// Later phases can read but never modify
var name = typeAnnotation.Name;  // ✅ OK
typeAnnotation.Name = "dict";     // ❌ Compile error

// To "modify", create a new record with `with` expression
var nonNullable = typeAnnotation with { IsNullable = false };
```

**Why Immutability Matters:**
- **Semantic analysis** reads AST without worrying about concurrent modifications
- **Validation pipeline** can process AST in parallel (see `ValidationPipeline.cs`)
- **Code generation** can cache transformations knowing AST won't change
- **Debugging** is easier—no hidden state changes between observations

### 3.2 Recursive Type Structure with ImmutableArray

`TypeAnnotation` is self-referential through `TypeArguments`, creating a tree that can represent arbitrarily nested generic types:

```
TypeAnnotation("dict")
├─ TypeArguments[0]: TypeAnnotation("str")
└─ TypeArguments[1]: TypeAnnotation("list")
                     └─ TypeArguments[0]: TypeAnnotation("int")
```

This represents: `dict[str, list[int]]`

**Why ImmutableArray?**
The `System.Collections.Immutable.ImmutableArray<T>` type provides:
- **Zero allocations** for empty arrays (static singleton)
- **Struct semantics**: Passed by value, no null checks needed
- **Array-like performance**: Contiguous memory, efficient iteration
- **Structural sharing**: Cheap to create modified copies

**Traversal Pattern** (from `TypeAnnotationHelper.cs` in semantic layer):
```csharp
public static string GetFullName(TypeAnnotation? typeAnnotation)
{
    if (typeAnnotation == null)
        return "void";
    
    // Build base name with generic arguments
    var baseName = typeAnnotation.TypeArguments.IsEmpty
        ? typeAnnotation.Name
        : $"{typeAnnotation.Name}[{string.Join(", ", 
              typeAnnotation.TypeArguments.Select(GetFullName))}]";
    
    // Add nullable suffix if needed
    return typeAnnotation.IsNullable ? $"{baseName}?" : baseName;
}
```

**Performance Consideration**: Recursive descent on deeply nested types (e.g., `list[list[list[list[int]]]]`) can be expensive. The compiler typically limits nesting depth in practice.

### 3.3 Source Location Tracking with Dual Representations

Every type annotation records its exact position in source code using **two complementary systems**:

```csharp
// Line/column-based (human-readable)
public int LineStart { get; init; }
public int ColumnStart { get; init; }
public int LineEnd { get; init; }
public int ColumnEnd { get; init; }

// Character offset-based (machine-efficient, optional)
public Text.TextSpan? Span { get; init; }
```

**Why Both?**
- **Line/Column**: Intuitive for error messages shown to developers
- **TextSpan**: Efficient for tooling (LSP, IDE integration) that operates on character offsets

**Why This Matters:**
When semantic analysis detects a type error, it can point to the exact type annotation:

```
error: Type 'str' is not assignable to parameter of type 'int'
  at line 42, column 24-27 (characters 1024-1027):
    def process(value: str) -> None:
                       ^^^
```

**Population Example** (from `Parser.Types.cs` lines 14-80):
```csharp
private TypeAnnotation ParseTypeAnnotation()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;
    var startToken = Current;  // Capture for Span calculation
    
    // ... parse type name and arguments ...
    
    var endToken = Previous;
    var endLine = endToken.Line;
    var endColumn = endToken.Column + endToken.Value.Length;
    
    return new TypeAnnotation
    {
        Name = name,
        TypeArguments = typeArgs,
        IsNullable = false,
        LineStart = startLine,
        ColumnStart = startColumn,
        LineEnd = endLine,
        ColumnEnd = endColumn,
        Span = GetSpanFromTokens(startToken, endToken)  // Helper method
    };
}
```

**Backward Compatibility**: The `Span` property is nullable (`Text.TextSpan?`) for backward compatibility with code that only sets line/column properties. New code should set both.

### 3.4 Parser Integration: Type Annotation Shorthands

The Parser (`Parser.Types.cs`) supports multiple syntactic forms for type annotations:

```python
# Standard form: identifier with optional generic args
x: list[int]
y: dict[str, int]

# Shorthand forms (see docs/language_specification/type_annotation_shorthand.md)
x: [int]           # Equivalent to list[int]
y: {str: int}      # Equivalent to dict[str, int]
z: {int}           # Equivalent to set[int]
w: (int, str)      # Equivalent to tuple[int, str]

# Array suffix
arr: int[]         # Equivalent to array[int]

# Nullable suffix
maybe: int?        # Nullable int
```

**Parser Decision Flow** (`Parser.Types.cs` lines 14-80):
```csharp
private TypeAnnotation ParseTypeAnnotation()
{
    TypeAnnotation baseType;
    
    if (Current.Type == TokenType.LeftBracket)
        baseType = ParseListTypeShorthand();        // [T] → list[T]
    else if (Current.Type == TokenType.LeftBrace)
        baseType = ParseSetOrDictTypeShorthand();   // {T} or {K: V}
    else if (Current.Type == TokenType.LeftParen)
        baseType = ParseTupleOrFunctionTypeShorthand();  // (T, U) or (T) -> U
    else
        baseType = ParseStandardTypeAnnotation();   // identifier[T]
    
    // Handle array suffix: T[]
    while (Current.Type == TokenType.LeftBracket && 
           Peek().Type == TokenType.RightBracket)
    {
        baseType = WrapInArrayType(baseType);
    }
    
    // Handle nullable suffix: T?
    if (Current.Type == TokenType.Question)
    {
        baseType = baseType with { IsNullable = true };
    }
    
    return baseType;
}
```

**Key Insight**: All shorthand forms are **desugared** into standard `TypeAnnotation` structures during parsing. By the time semantic analysis runs, there's no difference between `[int]` and `list[int]`—both become `TypeAnnotation { Name = "list", TypeArguments = ... }`.

---

## 4. Dependencies and Integration Points

### 4.1 Direct Dependencies (Imports)

```csharp
using System.Collections.Immutable;  // For ImmutableArray<T>
using Sharpy.Compiler.Text;          // For TextSpan (optional source location)
```

**Minimal Dependencies**: This file deliberately has minimal dependencies to remain stable. It doesn't depend on Lexer, Parser, or Semantic layers—those depend on *it*.

### 4.2 Upstream Components (What Creates TypeAnnotation)

**Parser** (`src/Sharpy.Compiler/Parser/Parser.Types.cs`)
- **Primary creator** of all `TypeAnnotation`, `FunctionType`, and `TupleType` instances
- Entry point: `ParseTypeAnnotation()` method (line 14)
- Handles shorthand syntax (`[T]`, `{K: V}`, `(T, U)`)
- Sets source location tracking from token positions
- **Key methods**:
  - `ParseStandardTypeAnnotation()`: Standard `identifier[T, U]` form
  - `ParseListTypeShorthand()`: `[T]` → `list[T]`
  - `ParseSetOrDictTypeShorthand()`: `{T}` or `{K: V}`
  - `ParseTupleOrFunctionTypeShorthand()`: `(T, U)` or `(T) -> U`

**Cross-Reference**: See [`Parser.Types.md`](../Parser.Types.md) for detailed parsing logic.

### 4.3 Downstream Components (What Consumes TypeAnnotation)

#### AST Nodes (`Parser/Ast/Statement.cs`, `Expression.cs`, `Pattern.cs`)

Type annotations appear throughout the AST:

```csharp
// Variable declarations
public record VariableDeclaration : Statement
{
    public TypeAnnotation? Type { get; init; }  // Optional for type inference
    // ...
}

// Function parameters and return types
public record Parameter
{
    public TypeAnnotation? Type { get; init; }
    // ...
}

public record FunctionDef : Statement
{
    public TypeAnnotation? ReturnType { get; init; }
    public ImmutableArray<Parameter> Parameters { get; init; }
    // ...
}

// Class and struct definitions
public record ClassDef : Statement
{
    public ImmutableArray<TypeAnnotation> BaseClasses { get; init; }  // Inheritance
    // ...
}

// Type patterns in match statements
public record TypePattern : Pattern
{
    public TypeAnnotation Type { get; init; } = null!;
    // ...
}
```

**Cross-References**: 
- [`Statement.md`](Statement.md) - Statement AST nodes
- [`Expression.md`](Expression.md) - Expression AST nodes  
- [`Pattern.md`](Pattern.md) - Pattern matching nodes

#### Semantic Analysis (`Semantic/` directory)

**TypeResolver** (`Semantic/TypeResolver.cs`)
- **Primary consumer**: Converts `TypeAnnotation` → `SemanticType`
- Entry point: `ResolveTypeAnnotation(TypeAnnotation? annotation)` method (line 25)
- Handles:
  - Builtin types (`int`, `str`, `bool` → `BuiltinType`)
  - Generic types (`list[int]` → `GenericType`)
  - User-defined types (`MyClass` → `UserDefinedType`)
  - Type parameters (`T` in `class Box[T]` → `TypeParameterType`)
  - Nullable types (`int?` → `NullableType`)
- **Recursive**: Calls itself to resolve nested type arguments

**TypeChecker** (`Semantic/TypeChecker.*.cs`)
- Validates type compatibility between assignments, function calls, etc.
- Uses resolved semantic types, but error messages reference original `TypeAnnotation` locations

**Validators** (`Semantic/Validation/`)
- `OperatorSignatureValidator.cs`: Validates operator method signatures match expected types
- `ProtocolSignatureValidator.cs`: Validates protocol implementations
- `DefaultParameterValidator.cs`: Validates default parameter types

**TypeAnnotationHelper** (`Semantic/TypeAnnotationHelper.cs`)
- Utility functions for working with type annotations
- `GetFullName(TypeAnnotation)`: Converts to string representation
- Used primarily for error messages and debugging

**Cross-References**:
- [`TypeResolver.md`](../../Semantic/TypeResolver.md) - Type resolution logic
- [`TypeChecker.md`](../../Semantic/TypeChecker.md) - Type checking logic

#### Code Generation (`CodeGen/` directory)

**TypeMapper** (`CodeGen/TypeMapper.cs`)
- Converts `SemanticType` (not `TypeAnnotation` directly) to Roslyn C# syntax
- Entry point: `MapSemanticType(SemanticType type)` method (line 47)
- Maps Sharpy types to C# equivalents:
  - `list[T]` → `global::Sharpy.Core.List<T>`
  - `dict[K, V]` → `global::Sharpy.Core.Dict<K, V>`
  - `int` → `int` (C# keyword)
  - `(int) -> str` → `Func<int, string>`

**RoslynEmitter** (`CodeGen/RoslynEmitter*.cs`)
- Uses `TypeMapper` to emit type syntax in C# code generation
- Never directly works with `TypeAnnotation`—always goes through semantic layer first

**Cross-References**:
- [`TypeMapper.md`](../../CodeGen/TypeMapper.md) - Type mapping logic
- [`RoslynEmitter.md`](../../CodeGen/RoslynEmitter.md) - Code generation

#### Diagnostics and Tooling

**AstDumper** (`Parser/AstDumper.cs`)
- Pretty-prints AST including type annotations for debugging
- Useful command: `dotnet run --project src/Sharpy.Cli -- emit ast file.spy`

**IDE Support** (Future)
- `lsp/sharpy/` directory: Planned LSP server will use `TextSpan` for efficient range queries

### 4.4 Data Flow Summary

```
┌─────────────────────────────────────────────────────────┐
│ Source Code: x: list[str] = []                          │
└─────────────────┬───────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────────────────────┐
│ Lexer: IDENTIFIER("list") LBRACKET IDENTIFIER("str") ...│
└─────────────────┬───────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────────────────────┐
│ Parser.Types.cs: Creates TypeAnnotation                 │
│   {                                                      │
│     Name = "list",                                       │
│     TypeArguments = [TypeAnnotation { Name = "str" }],  │
│     IsNullable = false,                                  │
│     LineStart = 1, ColumnStart = 4, ...                 │
│   }                                                      │
└─────────────────┬───────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────────────────────┐
│ TypeResolver.cs: Resolves to SemanticType               │
│   GenericType {                                          │
│     Name = "list",                                       │
│     TypeArguments = [BuiltinType("str")]                │
│   }                                                      │
└─────────────────┬───────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────────────────────┐
│ TypeChecker.cs: Validates type assignments              │
│   - Checks [] literal is assignable to list[str]        │
│   - Reports errors with TypeAnnotation source locations │
└─────────────────┬───────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────────────────────┐
│ TypeMapper.cs: Maps to C# Roslyn syntax                 │
│   GenericName("global::Sharpy.Core.List")               │
│     .WithTypeArgumentList(TypeArgumentList("string"))   │
└─────────────────┬───────────────────────────────────────┘
                  ↓
┌─────────────────────────────────────────────────────────┐
│ RoslynEmitter.cs: Emits C# code                         │
│   global::Sharpy.Core.List<string> x = new();           │
└─────────────────────────────────────────────────────────┘
```

**Key Insight**: `TypeAnnotation` is **syntax**, `SemanticType` is **meaning**. The parser creates syntax, the semantic analyzer creates meaning, and the code generator emits C#.

---

## 5. Common Usage Patterns and Code Examples

### 5.1 Creating Type Annotations in Parser

```csharp
// Simple type: int
var simpleType = new TypeAnnotation 
{ 
    Name = "int",
    TypeArguments = ImmutableArray<TypeAnnotation>.Empty,
    IsNullable = false,
    LineStart = currentToken.Line,
    ColumnStart = currentToken.Column,
    LineEnd = currentToken.Line,
    ColumnEnd = currentToken.Column + 3,
    Span = new TextSpan(startOffset, length)
};

// Generic type: list[str]
var genericType = new TypeAnnotation
{
    Name = "list",
    TypeArguments = ImmutableArray.Create(
        new TypeAnnotation { Name = "str", /* ... */ }
    ),
    IsNullable = false,
    // ... location info
};

// Nullable generic: dict[str, int]?
var nullableGeneric = new TypeAnnotation
{
    Name = "dict",
    TypeArguments = ImmutableArray.Create(
        new TypeAnnotation { Name = "str", /* ... */ },
        new TypeAnnotation { Name = "int", /* ... */ }
    ),
    IsNullable = true,
    // ... location info
};

// Function type: (int, str) -> bool
var funcType = new FunctionType
{
    ParameterTypes = ImmutableArray.Create(
        new TypeAnnotation { Name = "int", /* ... */ },
        new TypeAnnotation { Name = "str", /* ... */ }
    ),
    ReturnType = new TypeAnnotation { Name = "bool", /* ... */ },
    // ... location info
};

// Tuple type: tuple[int, str, float]
var tupleType = new TupleType
{
    ElementTypes = ImmutableArray.Create(
        new TypeAnnotation { Name = "int", /* ... */ },
        new TypeAnnotation { Name = "str", /* ... */ },
        new TypeAnnotation { Name = "float", /* ... */ }
    )
};
```

### 5.2 Inspecting Type Properties

```csharp
// Is this a simple non-generic type?
bool isSimple = typeAnnotation.TypeArguments.IsEmpty && 
                !typeAnnotation.IsNullable;

// Is this a generic type?
bool isGeneric = !typeAnnotation.TypeArguments.IsEmpty;

// Is this a nullable type?
bool isNullable = typeAnnotation.IsNullable;

// Is this a specific type (e.g., list)?
bool isList = typeAnnotation.Name == "list";

// Is this a list of strings specifically?
bool isListOfStrings = 
    typeAnnotation.Name == "list" &&
    typeAnnotation.TypeArguments.Length == 1 &&
    typeAnnotation.TypeArguments[0].Name == "str";

// Get arity (number of type arguments)
int arity = typeAnnotation.TypeArguments.Length;

// Check if location is tracked
bool hasLocation = typeAnnotation.Span.HasValue;
```

### 5.3 Pattern Matching on Type Annotations

C# 9 pattern matching is particularly useful with these immutable records:

```csharp
// Match specific type patterns
var resultType = typeAnnotation switch
{
    { Name: "int", IsNullable: false } => "Non-nullable int",
    { Name: "int", IsNullable: true } => "Nullable int",
    { Name: "list", TypeArguments.Length: 1 } => "List of something",
    { Name: "dict", TypeArguments.Length: 2 } => "Dictionary",
    { TypeArguments.IsEmpty: false } => "Some generic type",
    _ => "Simple type"
};

// Extract nested information
if (typeAnnotation is { Name: "list", TypeArguments: [var elementType] })
{
    Console.WriteLine($"List of {elementType.Name}");
}

// Check for deeply nested generics
bool isListOfDicts = typeAnnotation is
{
    Name: "list",
    TypeArguments: [{ Name: "dict" }]
};
```

### 5.4 Converting to String (Debugging)

Using `TypeAnnotationHelper.GetFullName()` (from semantic layer):

```csharp
var type1 = new TypeAnnotation { Name = "int" };
// Output: "int"

var type2 = new TypeAnnotation 
{ 
    Name = "list", 
    TypeArguments = ImmutableArray.Create(
        new TypeAnnotation { Name = "str" }
    )
};
// Output: "list[str]"

var type3 = new TypeAnnotation 
{ 
    Name = "dict",
    TypeArguments = ImmutableArray.Create(
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation { Name = "int" }
    ),
    IsNullable = true
};
// Output: "dict[str, int]?"

// Nested generics
var type4 = new TypeAnnotation
{
    Name = "list",
    TypeArguments = ImmutableArray.Create(
        new TypeAnnotation
        {
            Name = "dict",
            TypeArguments = ImmutableArray.Create(
                new TypeAnnotation { Name = "str" },
                new TypeAnnotation { Name = "list",
                    TypeArguments = ImmutableArray.Create(
                        new TypeAnnotation { Name = "int" }
                    )
                }
            )
        }
    )
};
// Output: "list[dict[str, list[int]]]"
```

### 5.5 Immutable Updates with `with` Expression

```csharp
// Create base type annotation
var baseType = new TypeAnnotation
{
    Name = "int",
    TypeArguments = ImmutableArray<TypeAnnotation>.Empty,
    IsNullable = false,
    LineStart = 1,
    ColumnStart = 10
};

// Make nullable version
var nullableType = baseType with { IsNullable = true };

// Wrap in list
var listType = new TypeAnnotation
{
    Name = "list",
    TypeArguments = ImmutableArray.Create(baseType),
    LineStart = 1,
    ColumnStart = 5
};

// Update location after parsing completes
var withEndLocation = baseType with 
{ 
    LineEnd = 1, 
    ColumnEnd = 13 
};
```

### 5.6 Recursive Traversal

```csharp
// Count total type annotations in a tree
public int CountTypeAnnotations(TypeAnnotation? type)
{
    if (type == null) return 0;
    
    int count = 1;  // Count this annotation
    
    // Recursively count type arguments
    foreach (var typeArg in type.TypeArguments)
    {
        count += CountTypeAnnotations(typeArg);
    }
    
    return count;
}

// Find all types referenced in an annotation
public HashSet<string> CollectTypeNames(TypeAnnotation? type)
{
    var names = new HashSet<string>();
    
    if (type == null) return names;
    
    names.Add(type.Name);
    
    foreach (var typeArg in type.TypeArguments)
    {
        names.UnionWith(CollectTypeNames(typeArg));
    }
    
    return names;
}

// Check if a type contains any nullable types
public bool ContainsNullable(TypeAnnotation? type)
{
    if (type == null) return false;
    if (type.IsNullable) return true;
    
    return type.TypeArguments.Any(ContainsNullable);
}
```

### 5.7 Working with Function Types

```csharp
// Create a function type: (int, str) -> bool
var funcType = new FunctionType
{
    ParameterTypes = ImmutableArray.Create(
        new TypeAnnotation { Name = "int" },
        new TypeAnnotation { Name = "str" }
    ),
    ReturnType = new TypeAnnotation { Name = "bool" },
    LineStart = 1,
    ColumnStart = 15,
    LineEnd = 1,
    ColumnEnd = 30
};

// Check arity
int paramCount = funcType.ParameterTypes.Length;  // 2

// Check return type
bool returnsVoid = funcType.ReturnType.Name == "None";

// Is this a callback (returns None)?
bool isCallback = funcType.ReturnType is { Name: "None", IsNullable: false };

// Pattern match on function signature
var category = funcType switch
{
    { ParameterTypes.IsEmpty: true } => "Nullary function",
    { ParameterTypes.Length: 1 } => "Unary function",
    { ParameterTypes.Length: 2 } => "Binary function",
    _ => "N-ary function"
};
```

### 5.8 Working with Tuple Types

```csharp
// Create tuple type: tuple[str, int, bool]
var tupleType = new TupleType
{
    ElementTypes = ImmutableArray.Create(
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation { Name = "int" },
        new TypeAnnotation { Name = "bool" }
    )
};

// Get tuple size
int tupleSize = tupleType.ElementTypes.Length;  // 3

// Access specific element type
var firstElementType = tupleType.ElementTypes[0];  // str

// Check if all elements are same type (homogeneous)
bool isHomogeneous = tupleType.ElementTypes
    .Select(t => t.Name)
    .Distinct()
    .Count() == 1;
```

---

## 6. Debugging Tips

### 6.1 Inspecting Type Annotations in the Debugger

When stopped at a breakpoint:

```csharp
// Quick inspection
Console.WriteLine($"Type: {typeAnnotation.Name}");
Console.WriteLine($"Generic: {typeAnnotation.TypeArguments.Count > 0}");
Console.WriteLine($"Nullable: {typeAnnotation.IsNullable}");

// Full tree view
Console.WriteLine(TypeAnnotationHelper.GetName(typeAnnotation));
```

### 6.2 Common Issues and Solutions

**Issue**: Type annotation has wrong location (LineStart/ColumnStart = 0)
- **Cause**: Parser didn't set location info when creating the annotation
- **Fix**: Ensure Parser.cs captures token position before creating TypeAnnotation

**Issue**: Nested generics not parsing correctly (e.g., `list[dict[str, int]]`)
- **Cause**: Recursive descent in `ParseTypeAnnotation()` might be stopping too early
- **Fix**: Check bracket matching logic in Parser.cs

**Issue**: Nullable annotation appearing in wrong place (e.g., `list?[int]` instead of `list[int]?`)
- **Cause**: Parser checks for `?` token at wrong point in parsing
- **Fix**: Nullable check should be *after* type arguments are parsed

**Issue**: `TypeArguments` list is null instead of empty
- **Cause**: Code checking `if (typeAnnotation.TypeArguments == null)` instead of `.Count == 0`
- **Fix**: Always use `.Count == 0` since init value is `new()` (never null)

### 6.3 Using AstDumper

The `AstDumper` utility can visualize entire AST trees including type annotations:

```csharp
var dumper = new AstDumper();
var module = parser.Parse();
Console.WriteLine(dumper.Dump(module));
```

Output example:
```
Module
├─ FunctionDef: process
│  ├─ Parameter: items
│  │  └─ Type: list[str]
│  └─ ReturnType: int
```

### 6.4 Verifying Semantic Resolution

After semantic analysis, check if types resolved correctly:

```csharp
// In TypeResolver or TypeChecker
var resolvedType = semanticInfo.GetResolvedType(typeAnnotation);
if (resolvedType == null)
{
    logger.LogError($"Failed to resolve type: {TypeAnnotationHelper.GetName(typeAnnotation)}");
}
```

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

**Add new record types when:**
- Sharpy adds new type syntax that doesn't fit existing records
- Example: Union types `int | str` might need a `UnionType` record

**Add new properties when:**
- Type annotations need to track additional syntactic information
- Example: If Sharpy adds type variance annotations (`list[+T]`), add `Variance` property

**Modify existing types when:**
- Fundamental changes to type syntax require different structure
- **Rare**: These are core types touched throughout the compiler

### 7.2 Adding a New Type Record

Example: Adding support for union types `int | str | None`

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

**Required follow-up work:**
1. Update `Parser.cs` to recognize `|` syntax and create `UnionType`
2. Update `TypeResolver.cs` to handle union type resolution
3. Update `TypeChecker.cs` to validate union type assignments
4. Update `RoslynEmitter.cs` to generate appropriate C# code
5. Update `TypeAnnotationHelper.cs` to handle union types in `GetName()`
6. Update `AstDumper.cs` to pretty-print union types
7. Add tests in `Parser.Tests`, `Semantic.Tests`, and `CodeGen.Tests`

### 7.3 Coding Standards for This File

**Follow the pattern:**
```csharp
/// <summary>
/// Clear, concise description of what this type represents
/// Include an example in parentheses
/// </summary>
public record NewTypeAnnotation
{
    // Properties grouped logically
    public string ImportantProperty { get; init; } = "";
    public List<SubType> SubTypes { get; init; } = new();
    
    // Source location last (consistent pattern)
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**Conventions:**
- Use `init` for all properties (immutability)
- Provide default values with `= new()` or `= ""`
- Use `null!` only when semantic guarantee exists (like `FunctionType.ReturnType`)
- Always include source location fields for error reporting
- XML doc comments are mandatory
- Keep records simple—no methods, just data

### 7.4 Testing Your Changes

**Unit tests** (if adding new type):
```csharp
// In Sharpy.Compiler.Tests/Parser/TypeAnnotationTests.cs
[Fact]
public void ParseUnionType()
{
    var parser = new Parser("def foo(x: int | str) -> None: pass");
    var module = parser.Parse();
    var func = (FunctionDef)module.Body[0];
    var param = func.Parameters[0];
    
    Assert.IsType<UnionType>(param.Type);
    var unionType = (UnionType)param.Type;
    Assert.Equal(2, unionType.Types.Count);
    Assert.Equal("int", unionType.Types[0].Name);
    Assert.Equal("str", unionType.Types[1].Name);
}
```

**Integration tests** (end-to-end):
```csharp
// In Sharpy.Compiler.Tests/Integration/TypeSystemTests.cs
[Fact]
public void CompileUnionTypeFunction()
{
    var source = @"
def process(value: int | str) -> str:
    if isinstance(value, int):
        return str(value)
    return value
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success);
}
```

### 7.5 Backward Compatibility

**CRITICAL**: This file is part of the compiler's public AST API. Changes here ripple through:
- All compiler phases (Parser, Semantic, CodeGen)
- External tools (if any use Sharpy.Compiler as library)
- Test suites

**Before making breaking changes:**
1. Grep for all usages: `grep -r "TypeAnnotation" src/`
2. Update all affected files in a single commit
3. Run full test suite: `dotnet test`
4. Update documentation

**Safe changes:**
- Adding new record types (doesn't break existing code)
- Adding optional properties with defaults
- Adding XML documentation

**Unsafe changes:**
- Renaming existing properties
- Changing property types
- Removing properties

---

## 8. Real-World Examples

### 8.1 Function with Complex Type Annotations

**Source Code:**
```python
def transform(
    data: dict[str, list[int]],
    mapper: (int) -> str,
    default: str?
) -> list[str]:
    # ... implementation
```

**AST Representation:**
```
FunctionDef: transform
├─ Parameters
│  ├─ Parameter: data
│  │  └─ Type: TypeAnnotation
│  │     ├─ Name: "dict"
│  │     └─ TypeArguments
│  │        ├─ TypeAnnotation { Name: "str" }
│  │        └─ TypeAnnotation
│  │           ├─ Name: "list"
│  │           └─ TypeArguments
│  │              └─ TypeAnnotation { Name: "int" }
│  ├─ Parameter: mapper
│  │  └─ Type: FunctionType
│  │     ├─ ParameterTypes
│  │     │  └─ TypeAnnotation { Name: "int" }
│  │     └─ ReturnType: TypeAnnotation { Name: "str" }
│  └─ Parameter: default
│     └─ Type: TypeAnnotation { Name: "str", IsNullable: true }
└─ ReturnType: TypeAnnotation
   ├─ Name: "list"
   └─ TypeArguments
      └─ TypeAnnotation { Name: "str" }
```

### 8.2 Class with Generic Base and Type Parameters

**Source Code:**
```python
class Repository[T]:
    def __init__(self, items: list[T]) -> None:
        self._items: list[T] = items
    
    def get_all(self) -> list[T]:
        return self._items
```

**Key Type Annotations:**
- `items: list[T]` → `TypeAnnotation { Name: "list", TypeArguments: [TypeAnnotation("T")] }`
- `self._items: list[T]` → Same structure
- Return type `list[T]` → Same structure

**Note**: The `T` in type arguments is just a string name at this stage. The semantic analyzer later binds it to the type parameter.

### 8.3 Struct with Tuple Field

**Source Code:**
```python
struct Point3D:
    x: float
    y: float
    z: float
    metadata: tuple[str, int, bool]
```

**Type Annotation for `metadata`:**
```csharp
new TupleType
{
    ElementTypes = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation { Name = "int" },
        new TypeAnnotation { Name = "bool" }
    },
    LineStart = 5,
    ColumnStart = 14,
    LineEnd = 5,
    ColumnEnd = 35
}
```

---

## 9. Future Enhancements

Potential additions to this file as Sharpy evolves:

### 9.1 Union Types
```csharp
public record UnionType
{
    public List<TypeAnnotation> Alternatives { get; init; } = new();
}
```
For: `int | str | None`

### 9.2 Literal Types
```csharp
public record LiteralType
{
    public object Value { get; init; } = null!;
    public string TypeName { get; init; } = "";  // "int", "str", etc.
}
```
For: `Literal[42]`, `Literal["exact string"]`

### 9.3 Type Variance Annotations
```csharp
public enum Variance
{
    Invariant,   // T
    Covariant,   // +T (out in C#)
    Contravariant  // -T (in in C#)
}

// Then add to TypeAnnotation:
public Variance Variance { get; init; } = Variance.Invariant;
```

### 9.4 Type Constraints
```csharp
public record TypeConstraint
{
    public string TypeParameter { get; init; } = "";
    public List<TypeAnnotation> Bounds { get; init; } = new();  // where T : Base, IInterface
}
```

---

## 10. Additional Resources

**Related Files to Study:**
- `Parser/Parser.cs` (lines ~2300-2330): See how these types are constructed
- `Semantic/TypeAnnotationHelper.cs`: Utility functions for working with type annotations
- `Semantic/TypeResolver.cs`: How type annotations become concrete .NET types
- `Parser/Ast/Node.cs`: Base class for all AST nodes
- `Parser/Ast/Statement.cs`: See how type annotations are used in declarations
- `Parser/Ast/Expression.cs`: Expression nodes (less type annotation usage)

**Documentation:**
- `docs/specs/type_system.md`: High-level type system design
- `docs/architecture/semantic-analyzer-architecture.md`: How types flow through semantic analysis
- `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`: General compiler contribution guide

**Command Line Tools:**
```bash
# See parsed AST including type annotations
dotnet run --project src/Sharpy.Cli -- build --emit-ast snippets/test.spy

# Run type-related tests
dotnet test --filter "FullyQualifiedName~TypeAnnotation"
dotnet test --filter "FullyQualifiedName~TypeResolver"
```

---

## Summary

The `Types.cs` file is deceptively simple—just three small record types—but they form the **syntactic backbone of Sharpy's type system**. Every type you write in Sharpy source code becomes one of these records in the AST.

**Key Takeaways:**
1. **Syntax, not semantics**: These represent *how types are written*, not what they mean
2. **Immutable and structural**: Perfect for compiler AST nodes
3. **Recursive structure**: Type arguments contain type annotations, enabling arbitrarily nested generics
4. **Location tracking**: Essential for good error messages
5. **Touched everywhere**: Parser creates them, semantic analysis resolves them, codegen translates them

When in doubt, remember: If you're dealing with type *syntax* in source code, you're working with these types. If you're dealing with type *meaning* (what types are compatible, what methods exist, etc.), you're working with the semantic layer that consumes these types.

Happy hacking! 🚀
