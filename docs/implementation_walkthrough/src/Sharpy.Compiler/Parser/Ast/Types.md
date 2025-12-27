# Walkthrough: Types.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Types.cs`

---

## 1. Overview

The `Types.cs` file defines the fundamental **type annotation data structures** used throughout the Sharpy compiler's Abstract Syntax Tree (AST). These records represent how types are written in source code—think of them as the "blueprint" for type information before semantic analysis resolves them into actual .NET types.

**Key Insight**: This file is about *syntax*, not semantics. A `TypeAnnotation` with `Name = "list"` and `TypeArguments = [TypeAnnotation("int")]` represents the *textual notation* `list[int]` in source code. The semantic analyzer later resolves this to the actual `Sharpy.Core.List<T>` type.

### Role in the Compiler Pipeline

```
Source Code → Lexer → Parser (creates TypeAnnotation) → Semantic Analysis (resolves to actual types) → Code Generation
```

**Example Flow:**
```python
def process(items: list[str]) -> int:
    return len(items)
```

1. **Parser** creates `TypeAnnotation { Name = "list", TypeArguments = [TypeAnnotation("str")] }`
2. **Semantic Analyzer** resolves this to the actual `Sharpy.Core.List<string>` type
3. **Code Generator** emits C# code with proper type mappings

---

## 2. Class/Type Structure

The file defines three immutable record types, each serving a distinct purpose in representing type syntax:

### 2.1 `TypeAnnotation` (Lines 6-17)

**The workhorse of the type system.** Represents general type annotations including:
- Simple types: `int`, `str`, `bool`
- Generic types: `list[int]`, `dict[str, int]`
- Nested generics: `list[dict[str, list[int]]]`
- Optional types: `int?`, `list[str]?`

```csharp
public record TypeAnnotation
{
    public string Name { get; init; } = "";
    public List<TypeAnnotation> TypeArguments { get; init; } = new();
    public bool IsNullable { get; init; }
    
    // Source location tracking (for error messages)
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**Key Properties:**
- **`Name`**: The base type identifier (e.g., `"int"`, `"list"`, `"MyClass"`)
- **`TypeArguments`**: For generic types, the type parameters. Empty for non-generic types
- **`IsNullable`**: Set to `true` when source uses `T?` syntax
- **Location fields**: Enable precise error messages pointing to the exact type annotation in source code

**Design Decision**: Type arguments are themselves `TypeAnnotation` records, creating a **recursive tree structure** that naturally represents nested generics.

### 2.2 `FunctionType` (Lines 19-26)

Represents function type annotations using the arrow syntax: `(param_types...) -> return_type`

```csharp
public record FunctionType
{
    public List<TypeAnnotation> ParameterTypes { get; init; } = new();
    public TypeAnnotation ReturnType { get; init; } = null!;
}
```

**Use Cases:**
```python
# Higher-order function taking a callback
def map_values(items: list[int], transform: (int) -> str) -> list[str]:
    return [transform(x) for x in items]

# Function returning another function
def make_adder(x: int) -> (int) -> int:
    def add(y: int) -> int:
        return x + y
    return add
```

**Note**: The `ReturnType` is marked `null!` (null-forgiving operator) because it's always set during parsing—a function type must have a return type.

### 2.3 `TupleType` (Lines 28-34)

Represents heterogeneous tuple type annotations: `tuple[int, str, float]`

```csharp
public record TupleType
{
    public List<TypeAnnotation> ElementTypes { get; init; } = new();
}
```

**Sharpy vs. Python Distinction:**
- Python's `tuple[int, ...]` (homogeneous, variable length) → Use `TypeAnnotation` with name `"tuple"` and one type argument
- Sharpy's `tuple[int, str, bool]` (heterogeneous, fixed length) → Use `TupleType` with specific element types

**Example:**
```python
# Heterogeneous tuple (uses TupleType)
user_info: tuple[str, int, bool] = ("Alice", 30, True)

# Homogeneous tuple (uses TypeAnnotation)
numbers: tuple[int, ...] = (1, 2, 3, 4, 5)
```

---

## 3. Key Design Patterns

### 3.1 Immutable Records with Init-Only Properties

All three types use C# 9's `record` feature with `init` setters:

```csharp
public record TypeAnnotation
{
    public string Name { get; init; } = "";  // Can only be set during initialization
    // ...
}
```

**Benefits:**
- **Thread-safe**: AST can be shared across compiler phases without locking
- **Structural equality**: Two `TypeAnnotation` records with same values are equal
- **Predictable**: Once created, a type annotation never changes

**Pattern in Use:**
```csharp
// Parser creates immutable instances
var typeAnnotation = new TypeAnnotation
{
    Name = "list",
    TypeArguments = new List<TypeAnnotation> 
    { 
        new TypeAnnotation { Name = "int" } 
    },
    IsNullable = true,
    LineStart = token.Line,
    ColumnStart = token.Column
};

// Later phases can read but never modify
var name = typeAnnotation.Name;  // ✅ OK
typeAnnotation.Name = "dict";     // ❌ Compile error
```

### 3.2 Recursive Type Structure

`TypeAnnotation` is self-referential through `TypeArguments`, creating a tree:

```
TypeAnnotation("dict")
├─ TypeArguments[0]: TypeAnnotation("str")
└─ TypeArguments[1]: TypeAnnotation("list")
                     └─ TypeArguments[0]: TypeAnnotation("int")
```

This represents: `dict[str, list[int]]`

**Traversal Pattern** (from `TypeAnnotationHelper.cs`):
```csharp
public static string GetName(TypeAnnotation? typeAnnotation)
{
    if (typeAnnotation == null)
        return "void";
    
    // Recursive call for nested type arguments
    var baseName = typeAnnotation.TypeArguments.Count > 0
        ? $"{typeAnnotation.Name}[{string.Join(", ", 
              typeAnnotation.TypeArguments.Select(GetName))}]"
        : typeAnnotation.Name;
    
    return typeAnnotation.IsNullable ? $"{baseName}?" : baseName;
}
```

### 3.3 Source Location Tracking

Every type annotation records its exact position in source code:

```csharp
public int LineStart { get; init; }
public int ColumnStart { get; init; }
public int LineEnd { get; init; }
public int ColumnEnd { get; init; }
```

**Why This Matters:**
When semantic analysis detects a type error, it can point to the exact type annotation:

```
error: Type 'str' cannot be assigned to parameter of type 'int'
  at line 42, column 15-18:
    def process(value: str) -> None:
                       ^^^
```

**Population Example** (from `Parser.cs` lines 2320-2329):
```csharp
var endLine = Peek(-1).Line;
var endColumn = Peek(-1).Column + Peek(-1).Value.Length;

return new TypeAnnotation
{
    Name = name,
    TypeArguments = typeArgs,
    IsNullable = isNullable,
    LineStart = startLine,     // Set when parsing starts
    ColumnStart = startColumn,
    LineEnd = endLine,         // Calculated from last token
    ColumnEnd = endColumn
};
```

---

## 4. Dependencies and Integration Points

### 4.1 Used By (Consumers)

**Parser (`Parser.cs`)**
- Creates `TypeAnnotation` instances when parsing type syntax
- Method `ParseTypeAnnotation()` at line ~2320
- Handles nullable suffix `?`, generic arguments `[T, U]`, etc.

**AST Nodes (`Statement.cs`, `Expression.cs`)**
- `VariableDeclaration.Type`: Type of variable (line 51 in Statement.cs)
- `Parameter.Type`: Function parameter types (line 265 in Statement.cs)
- `FunctionDef.ReturnType`: Function return type (line 177 in Statement.cs)
- `ClassDef.BaseClasses`: Inheritance and protocol implementation (line 190)
- `StructDef.BaseClasses`: Interface implementations (line 203)

**Semantic Analysis (`Semantic/` directory)**
- `TypeResolver.cs`: Converts `TypeAnnotation` → actual .NET types
- `TypeChecker.cs`: Validates type compatibility
- `TypeAnnotationHelper.cs`: Utility for string representation
- `OperatorSignatureValidator.cs`: Validates operator method signatures
- `ProtocolSignatureValidator.cs`: Validates protocol implementations

**Code Generation (`CodeGen/`)**
- `RoslynEmitter.cs`: Converts type annotations to Roslyn C# type syntax
- `TypeMapper.cs`: Maps Sharpy types to .NET types (e.g., `list[T]` → `Sharpy.Core.List<T>`)

**Diagnostics (`AstDumper.cs`)**
- Pretty-prints AST including type annotations for debugging

### 4.2 Dependencies (What This File Needs)

**Direct Dependencies:**
- `System.Collections.Generic.List<T>` for `TypeArguments` and `ElementTypes`

**Conceptual Dependencies:**
- Token types from `Lexer/Token.cs` (indirectly, through Parser)
- AST base class `Node` from `Node.cs` (not directly inherited, but part of AST family)

---

## 5. Common Usage Patterns

### 5.1 Creating Type Annotations in Parser

```csharp
// Simple type: int
var simpleType = new TypeAnnotation 
{ 
    Name = "int",
    LineStart = currentToken.Line,
    ColumnStart = currentToken.Column,
    LineEnd = currentToken.Line,
    ColumnEnd = currentToken.Column + 3
};

// Generic type: list[str]
var genericType = new TypeAnnotation
{
    Name = "list",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" }
    },
    // ... location info
};

// Nullable generic: dict[str, int]?
var nullableGeneric = new TypeAnnotation
{
    Name = "dict",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation { Name = "int" }
    },
    IsNullable = true,
    // ... location info
};
```

### 5.2 Checking Type Properties

```csharp
// Is this a simple non-generic type?
bool isSimple = typeAnnotation.TypeArguments.Count == 0 && !typeAnnotation.IsNullable;

// Is this a generic type?
bool isGeneric = typeAnnotation.TypeArguments.Count > 0;

// Is this a nullable type?
bool isNullable = typeAnnotation.IsNullable;

// Is this a specific type (e.g., list)?
bool isList = typeAnnotation.Name == "list";

// Is this a list of strings specifically?
bool isListOfStrings = 
    typeAnnotation.Name == "list" &&
    typeAnnotation.TypeArguments.Count == 1 &&
    typeAnnotation.TypeArguments[0].Name == "str";
```

### 5.3 Converting to String (Debugging)

Using `TypeAnnotationHelper.GetName()`:

```csharp
var type1 = new TypeAnnotation { Name = "int" };
Console.WriteLine(TypeAnnotationHelper.GetName(type1));  // "int"

var type2 = new TypeAnnotation 
{ 
    Name = "list", 
    TypeArguments = new List<TypeAnnotation> 
    { 
        new TypeAnnotation { Name = "str" } 
    } 
};
Console.WriteLine(TypeAnnotationHelper.GetName(type2));  // "list[str]"

var type3 = new TypeAnnotation 
{ 
    Name = "dict",
    TypeArguments = new List<TypeAnnotation>
    {
        new TypeAnnotation { Name = "str" },
        new TypeAnnotation { Name = "int" }
    },
    IsNullable = true
};
Console.WriteLine(TypeAnnotationHelper.GetName(type3));  // "dict[str, int]?"
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
