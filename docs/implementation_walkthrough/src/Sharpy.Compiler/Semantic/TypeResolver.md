# Walkthrough: TypeResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

---

## Overview

`TypeResolver` is a critical component in the Sharpy compiler's semantic analysis pipeline. Its primary responsibility is to **convert type annotations from the Abstract Syntax Tree (AST) into semantic types** that the rest of the compiler can reason about.

Think of it as a translator: when you write `list[int]` or `str?` in Sharpy code, the parser creates a `TypeAnnotation` AST node representing the syntax. The `TypeResolver` takes that AST representation and produces a `SemanticType` object (like `GenericType` or `NullableType`) that encodes the actual type system semantics.

### Role in the Compiler Pipeline

```
Source Code → Lexer → Parser → AST
                                 ↓
                         NameResolver (resolves names to symbols)
                                 ↓
                         TypeResolver ← YOU ARE HERE
                                 ↓
                         TypeChecker (validates type correctness)
                                 ↓
                         RoslynEmitter (generates C#)
```

The `TypeResolver` runs **after** `NameResolver` (which establishes what variables and types exist) but **before** `TypeChecker` (which validates that expressions have correct types). It's a pure translation step—no type checking happens here, just resolution.

---

## Class Structure

### Main Class: `TypeResolver`

```csharp
public class TypeResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    public IReadOnlyList<SemanticError> Errors => _errors;
}
```

**Key Dependencies:**

- **`SymbolTable`**: Lookup table for all declared types, functions, and variables in the current compilation unit. Used to resolve user-defined type names like `MyClass` to their corresponding `TypeSymbol`.

- **`SemanticInfo`**: A side-table that maps AST nodes to their semantic information. The resolver **caches** resolved types here so that if the same `TypeAnnotation` node is resolved multiple times, we don't redo the work.

- **`ICompilerLogger`**: For logging errors and warnings during resolution.

- **`_errors`**: Accumulates errors encountered during resolution (e.g., "Type 'Foo' not found"). The compiler can continue after errors for better error reporting.

### Constructor

```csharp
public TypeResolver(SymbolTable symbolTable, SemanticInfo semanticInfo, ICompilerLogger? logger = null)
{
    _symbolTable = symbolTable;
    _semanticInfo = semanticInfo;
    _logger = logger ?? NullLogger.Instance;
}
```

The logger is optional—if none is provided, a `NullLogger` is used (no-op implementation). This makes testing easier.

---

## Key Methods

### 1. `ResolveTypeAnnotation(TypeAnnotation? annotation)` — The Main Entry Point

**Purpose:** Convert a type annotation AST node into a `SemanticType`.

**Signature:**
```csharp
public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
```

**Flow:**

```csharp
if (annotation == null)
    return SemanticType.Unknown;
```
- **Null handling**: If there's no type annotation (e.g., a variable declared without a type hint), return `SemanticType.Unknown`. The `TypeChecker` will later infer or flag this.

```csharp
// Check cache
var cached = _semanticInfo.GetTypeAnnotation(annotation);
if (cached != null)
    return cached;
```
- **Caching**: Before doing any work, check if we've already resolved this exact AST node. This is crucial for performance—generic types and complex annotations can be expensive to resolve, and the same `TypeAnnotation` instance may be referenced multiple times in the AST.

```csharp
// Handle 'auto' keyword for type inference
if (annotation.Name == "auto")
{
    result = SemanticType.Unknown;
    _semanticInfo.SetTypeAnnotation(annotation, result);
    return result;
}
```
- **Type inference marker**: The `auto` keyword tells the compiler to infer the type. We represent this as `Unknown` and let the `TypeChecker` fill it in later.

**Resolution Strategy:**

The resolver tries three approaches in order:

#### a) Builtin Types
```csharp
if (TryResolveBuiltinType(annotation.Name, out var builtinType))
{
    result = builtinType;
}
```
Check if it's a primitive like `int`, `str`, `bool`, etc. These are resolved directly to singleton `SemanticType` instances for efficiency.

#### b) Generic Types
```csharp
else if (annotation.TypeArguments.Count > 0)
{
    result = ResolveGenericType(annotation);
}
```
If there are type arguments (e.g., `list[int]`, `dict[str, float]`), this is a generic type instantiation. Delegate to `ResolveGenericType()`.

#### c) User-Defined Types
```csharp
else
{
    var typeSymbol = _symbolTable.LookupType(annotation.Name);
    if (typeSymbol != null)
    {
        result = new UserDefinedType
        {
            Name = annotation.Name,
            Symbol = typeSymbol
        };
    }
    else
    {
        AddError($"Type '{annotation.Name}' not found", null, null);
        result = SemanticType.Unknown;
    }
}
```
For simple names like `MyClass`, look them up in the symbol table. If not found, log an error but return `Unknown` to allow compilation to continue (for better error reporting).

**Nullable Wrapping:**
```csharp
// Handle nullable types
if (annotation.IsNullable && result != SemanticType.Unknown)
{
    result = new NullableType { UnderlyingType = result };
}
```
If the annotation has the `?` suffix (e.g., `int?`), wrap the resolved type in a `NullableType`. Note: we skip this for `Unknown` to avoid cascading errors.

**Caching the Result:**
```csharp
// Cache the result
_semanticInfo.SetTypeAnnotation(annotation, result);
return result;
```
Store the resolved type in `SemanticInfo` for future lookups.

---

### 2. `TryResolveBuiltinType(string name, out SemanticType type)` — Primitive Type Resolution

**Purpose:** Map Sharpy primitive type names to their corresponding `SemanticType` singletons.

**Signature:**
```csharp
private bool TryResolveBuiltinType(string name, out SemanticType type)
```

**Implementation:**
```csharp
type = name switch
{
    "int" => SemanticType.Int,
    "long" => SemanticType.Long,
    "float" => SemanticType.Float,       // float -> double (per spec)
    "float32" => SemanticType.Float32,   // float32 -> C# float
    "float64" => SemanticType.Double,    // float64 -> double
    "double" => SemanticType.Double,
    "bool" => SemanticType.Bool,
    "str" => SemanticType.Str,
    "None" => SemanticType.Void,
    _ => null!
};

return type != null;
```

**Design Decision:** This uses a `switch` expression for performance. The primitive types are **singleton instances** defined in `SemanticType.cs`, which means:
- No allocations when resolving `int`, `str`, etc.
- Identity comparisons work: `type == SemanticType.Int`

**Float Type Mapping (per Sharpy specification):**

| Sharpy Type | C# Type | Notes |
|-------------|---------|-------|
| `float` | `double` | Default float is 64-bit for precision |
| `float32` | `float` | Explicit 32-bit when needed |
| `float64` | `double` | Explicit 64-bit (same as `float`) |
| `double` | `double` | Alias for `float64` |

This mapping ensures Sharpy's default `float` type provides double-precision semantics (like Python), while allowing explicit control when 32-bit floats are needed for interop or performance.

**Why not use the `SymbolTable`?** Builtins are checked first because they're extremely common. Avoiding a dictionary lookup in `SymbolTable` for every `int` annotation improves performance.

---

### 3. `ResolveGenericType(TypeAnnotation annotation)` — Generic Type Instantiation

**Purpose:** Resolve generic types like `list[int]`, `dict[str, int]`, or `MyGenericClass[T, U]`.

**Signature:**
```csharp
private SemanticType ResolveGenericType(TypeAnnotation annotation)
```

**Flow:**

#### Step 1: Lookup the Generic Definition
```csharp
var typeSymbol = _symbolTable.LookupType(annotation.Name);
if (typeSymbol == null)
{
    AddError($"Generic type '{annotation.Name}' not found", null, null);
    return SemanticType.Unknown;
}
```
The generic type name (e.g., `list`, `dict`, `MyGenericClass`) must exist as a `TypeSymbol` in the symbol table.

#### Step 2: Resolve Type Arguments Recursively
```csharp
// Resolve type arguments
var typeArgs = annotation.TypeArguments
    .Select(ResolveTypeAnnotation)  // Recursive call!
    .ToList();
```
**Key Insight:** This is **recursive**. For `dict[str, list[int]]`, we:
1. Resolve `dict` (generic definition)
2. Resolve `str` (builtin)
3. Resolve `list[int]` (recursive call)
   - Resolve `list` (generic definition)
   - Resolve `int` (builtin)

This naturally handles arbitrary nesting depth.

#### Step 3: Validate Arity
```csharp
// Validate type argument count
if (typeSymbol.IsGeneric && typeArgs.Count != typeSymbol.TypeParameters.Count)
{
    AddError($"Type '{annotation.Name}' expects {typeSymbol.TypeParameters.Count} type arguments but got {typeArgs.Count}",
        null, null);
    return SemanticType.Unknown;
}
```
Check that the number of type arguments matches the generic definition. For example, `list[int, str]` is invalid because `list[T]` expects exactly one type argument.

#### Step 4: Create the Generic Type
```csharp
return new GenericType
{
    Name = annotation.Name,
    TypeArguments = typeArgs,
    GenericDefinition = typeSymbol
};
```
Package everything into a `GenericType` record. This includes:
- `Name`: The base name (e.g., "list")
- `TypeArguments`: The resolved type parameters (e.g., `[SemanticType.Int]`)
- `GenericDefinition`: Back-reference to the `TypeSymbol` for the generic class/interface

**Why store `GenericDefinition`?** Later passes (like `TypeChecker` and `RoslynEmitter`) need to access members (methods, fields) of the generic type, which are stored in the `TypeSymbol`.

---

### 4. `AddError(string message, int? line, int? column)` — Error Accumulation

**Purpose:** Record an error without stopping compilation.

**Signature:**
```csharp
private void AddError(string message, int? line = null, int? column = null)
```

**Implementation:**
```csharp
private void AddError(string message, int? line = null, int? column = null)
{
    var error = new SemanticError(message, line, column);
    _errors.Add(error);
    _logger.LogError(error.Message, line ?? 0, column ?? 0);
}
```

**Design Philosophy:** The compiler continues after errors to report **multiple** issues in a single compilation run. This is much better UX than stopping at the first error.

**Note:** Currently, `line` and `column` are always `null` in `TypeResolver` because the AST nodes don't yet consistently track location info. This is a known limitation that could be improved (see "Contribution Guidelines" below).

---

## Dependencies

### External Components

#### 1. `SymbolTable` (`Semantic/SymbolTable.cs`)
- **Purpose**: Central registry of all symbols (types, functions, variables) in scope.
- **Used For**: Looking up user-defined types and generic definitions.
- **Key Method**: `LookupType(string name)` → `TypeSymbol?`

#### 2. `SemanticInfo` (`Semantic/SemanticInfo.cs`)
- **Purpose**: Side-table mapping AST nodes to semantic data.
- **Used For**: Caching resolved types to avoid redundant work.
- **Key Methods**:
  - `GetTypeAnnotation(TypeAnnotation)` → `SemanticType?`
  - `SetTypeAnnotation(TypeAnnotation, SemanticType)`

#### 3. `SemanticType` and Subclasses (`Semantic/SemanticType.cs`)
- **Records**: `BuiltinType`, `GenericType`, `UserDefinedType`, `NullableType`, `FunctionType`, `TupleType`, `UnknownType`, `VoidType`
- **Used For**: Representing the resolved type system.
- **Key Feature**: Singleton instances for common types (`SemanticType.Int`, etc.).

#### 4. `TypeAnnotation` (`Parser/Ast/Types.cs`)
- **Purpose**: AST representation of a type annotation.
- **Key Fields**:
  - `Name`: The type name (e.g., "int", "list", "MyClass")
  - `TypeArguments`: List of nested type annotations (for generics)
  - `IsNullable`: Whether the `?` suffix is present

#### 5. `ICompilerLogger` (`Logging/ICompilerLogger.cs`)
- **Purpose**: Abstraction for logging errors, warnings, and info messages.
- **Used For**: Reporting type resolution errors.

### Data Flow

```
TypeAnnotation (AST)
        ↓
  TypeResolver.ResolveTypeAnnotation()
        ↓
  SymbolTable.LookupType() ← (for user types)
        ↓
  SemanticType (output)
        ↓
  SemanticInfo (cached)
        ↓
  TypeChecker (consumer)
```

---

## Patterns and Design Decisions

### 1. **Caching with `SemanticInfo`**
- **Why?** Type resolution can be expensive (especially for deeply nested generics). Caching ensures O(1) repeated lookups.
- **Pattern**: Check cache → compute → store in cache.
- **Trade-off**: Memory usage vs. CPU time. The cache grows with AST size, but the speed improvement is significant.

### 2. **Singleton Pattern for Primitives**
- **Why?** Primitives like `int`, `str` are used everywhere. Creating new instances would cause massive allocation overhead.
- **Pattern**: Static readonly fields in `SemanticType` (e.g., `SemanticType.Int`).
- **Benefit**: Identity checks (`==`) work correctly, and no GC pressure.

### 3. **Recursive Resolution**
- **Why?** Generic types can nest arbitrarily (`dict[str, list[tuple[int, float]]]`).
- **Pattern**: `ResolveTypeAnnotation()` calls itself for type arguments.
- **Edge Case**: Circular type definitions (not currently handled—see "Future Work").

### 4. **Error Recovery with `Unknown`**
- **Why?** If a type can't be resolved, using `SemanticType.Unknown` allows compilation to continue.
- **Pattern**: Return `Unknown` on error, log the issue, continue.
- **Benefit**: Collect multiple errors in one pass (better UX).

### 5. **Immutable AST, Mutable Side-Table**
- **Why?** AST nodes are immutable records (as per Sharpy design), but we need to annotate them with semantic info.
- **Pattern**: Use `SemanticInfo` as a separate dictionary mapping AST nodes to data.
- **Benefit**: Clean separation of syntax and semantics. Makes AST thread-safe and easier to reason about.

### 6. **Switch Expression for Builtins**
- **Why?** Performance. Builtins are checked first, and a switch expression compiles to a jump table.
- **Alternative**: Could use `SymbolTable`, but that adds dictionary lookup overhead.

---

## Debugging Tips

### Common Issues and How to Diagnose

#### 1. **"Type 'X' not found" Error**

**Symptom:** `TypeResolver` reports a missing type that you know exists.

**Causes:**
- The type wasn't added to the `SymbolTable` by `NameResolver`.
- The type is defined later in the file (forward reference issue).
- Import statement is missing or failed to resolve.

**How to Debug:**
```csharp
// Add breakpoint in ResolveTypeAnnotation()
var typeSymbol = _symbolTable.LookupType(annotation.Name);
// Inspect _symbolTable._scopeStack to see what's in scope
```

**Fix Strategy:**
- Check `NameResolver` to ensure it visits class/type definitions.
- Verify import resolution is working correctly.
- Consider whether Sharpy should support forward references (currently limited).

#### 2. **Generic Type Arity Mismatch**

**Symptom:** `"Type 'list' expects 1 type argument but got 2"`

**Causes:**
- User wrote `list[int, str]` instead of `tuple[int, str]`.
- The generic definition in `SymbolTable` has incorrect `TypeParameters`.

**How to Debug:**
```csharp
// In ResolveGenericType(), inspect:
typeSymbol.TypeParameters.Count  // Expected arity
typeArgs.Count                   // Provided arity
```

**Fix Strategy:**
- Add better error messages suggesting correct types (e.g., "Did you mean tuple[int, str]?").
- Validate `TypeSymbol` definitions in `BuiltinRegistry`.

#### 3. **Cache Poisoning**

**Symptom:** A type resolves incorrectly, and the wrong resolution persists even after fixing the source.

**Causes:**
- `SemanticInfo` cache is shared across multiple compilation attempts.
- AST node identity is preserved across re-parses (rare, but possible if AST is reused).

**How to Debug:**
```csharp
// Check if cached value exists:
var cached = _semanticInfo.GetTypeAnnotation(annotation);
// Clear cache before re-resolving
_semanticInfo._typeAnnotations.Clear();  // Not exposed publicly, but useful for debugging
```

**Fix Strategy:**
- Ensure `SemanticInfo` is recreated for each compilation.
- Never reuse `SemanticInfo` across files or compilation runs.

#### 4. **Recursive Type Definitions (Future Issue)**

**Symptom:** Stack overflow when resolving types.

**Example:**
```python
# Hypothetical future syntax
class Node:
    value: int
    next: Node?  # Self-referential type
```

**Current State:** Not handled. Would cause infinite recursion in `ResolveTypeAnnotation()`.

**Fix Strategy (Future):**
- Add a visited set to detect cycles.
- For recursive types, return a placeholder and resolve later.

---

## Contribution Guidelines

### Areas for Improvement

#### 1. **Add Source Location Tracking**
**Current Issue:** Errors don't include line/column numbers.

**Task:**
- Modify `AddError()` to extract location from `annotation.LineStart`, `annotation.ColumnStart`.
- Update error messages to include file context.

**Impact:** Better error messages for users.

**Difficulty:** Easy

---

#### 2. **Support for Tuple Types**
**Current Issue:** `tuple[int, str]` is treated as a generic type, but should be a `TupleType`.

**Task:**
- Add special handling in `ResolveTypeAnnotation()`:
  ```csharp
  if (annotation.Name == "tuple" && annotation.TypeArguments.Count > 0)
  {
      return new TupleType
      {
          ElementTypes = annotation.TypeArguments
              .Select(ResolveTypeAnnotation)
              .ToList()
      };
  }
  ```

**Impact:** Proper support for tuple types (currently partial).

**Difficulty:** Easy

---

#### 3. **Support for Function Types**
**Current Issue:** Function type annotations `(int, str) -> bool` aren't resolved.

**Task:**
- Add a new AST node type for function type annotations (in `Parser/Ast/Types.cs`).
- Handle it in `ResolveTypeAnnotation()`:
  ```csharp
  if (annotation is FunctionTypeAnnotation funcType)
  {
      return new FunctionType
      {
          ParameterTypes = funcType.ParameterTypes
              .Select(ResolveTypeAnnotation)
              .ToList(),
          ReturnType = ResolveTypeAnnotation(funcType.ReturnType)
      };
  }
  ```

**Impact:** Enable first-class function types for lambdas and higher-order functions.

**Difficulty:** Medium (requires parser changes)

---

#### 4. **Cycle Detection for Recursive Types**
**Current Issue:** Self-referential types would cause infinite recursion.

**Task:**
- Add a `HashSet<TypeAnnotation> _resolvingStack` field.
- Check for cycles at the start of `ResolveTypeAnnotation()`:
  ```csharp
  if (_resolvingStack.Contains(annotation))
  {
      // Return a placeholder for the recursive reference
      return new RecursiveTypeRef { Name = annotation.Name };
  }
  _resolvingStack.Add(annotation);
  try
  {
      // ... existing resolution logic
  }
  finally
  {
      _resolvingStack.Remove(annotation);
  }
  ```

**Impact:** Support recursive data structures (linked lists, trees, etc.).

**Difficulty:** Medium

---

#### 5. **Better Error Messages**
**Current Issue:** Generic errors like "Type 'X' not found" aren't actionable.

**Task:**
- Add suggestions using Levenshtein distance:
  ```csharp
  AddError($"Type '{annotation.Name}' not found. Did you mean '{suggestedName}'?", ...);
  ```
- Distinguish between "not imported" vs. "doesn't exist".

**Impact:** Significantly better developer experience.

**Difficulty:** Medium

---

#### 6. **Support for Type Aliases**
**Current Issue:** No support for `type MyAlias = dict[str, int]`.

**Task:**
- Extend `SymbolTable` to store type aliases.
- In `ResolveTypeAnnotation()`, check if name is an alias and resolve to the aliased type.

**Impact:** Enable user-defined type aliases (major language feature).

**Difficulty:** Hard (requires language design decisions)

---

### Testing Considerations

When contributing to `TypeResolver`, ensure you:

1. **Add unit tests** in `Sharpy.Compiler.Tests/Semantic/TypeResolverTests.cs`:
   ```csharp
   [Fact]
   public void ResolveBuiltinType_Int_ReturnsIntType()
   {
       var resolver = new TypeResolver(symbolTable, semanticInfo);
       var annotation = new TypeAnnotation { Name = "int" };
       var result = resolver.ResolveTypeAnnotation(annotation);
       Assert.Equal(SemanticType.Int, result);
   }
   ```

2. **Test error cases**:
   ```csharp
   [Fact]
   public void ResolveTypeAnnotation_UnknownType_AddsError()
   {
       var resolver = new TypeResolver(symbolTable, semanticInfo);
       var annotation = new TypeAnnotation { Name = "FakeType" };
       var result = resolver.ResolveTypeAnnotation(annotation);
       Assert.Equal(SemanticType.Unknown, result);
       Assert.Single(resolver.Errors);
   }
   ```

3. **Test caching**:
   ```csharp
   [Fact]
   public void ResolveTypeAnnotation_SameNodeTwice_UsesCache()
   {
       var resolver = new TypeResolver(symbolTable, semanticInfo);
       var annotation = new TypeAnnotation { Name = "int" };
       var result1 = resolver.ResolveTypeAnnotation(annotation);
       var result2 = resolver.ResolveTypeAnnotation(annotation);
       Assert.Same(result1, result2);  // Should be same instance
   }
   ```

4. **Integration tests** with full compilation pipeline:
   ```csharp
   [Fact]
   public void Compile_GenericType_Resolves()
   {
       var source = @"
           def process(items: list[int]) -> None:
               print(items)
       ";
       var result = CompileAndExecute(source);
       Assert.Empty(result.Errors);
   }
   ```

5. **Test float type mappings**:
   ```csharp
   [Theory]
   [InlineData("float", typeof(double))]
   [InlineData("float32", typeof(float))]
   [InlineData("float64", typeof(double))]
   [InlineData("double", typeof(double))]
   public void ResolveBuiltinType_FloatVariants_MapsCorrectly(string typeName, Type expectedClr)
   {
       var resolver = new TypeResolver(symbolTable, semanticInfo);
       var annotation = new TypeAnnotation { Name = typeName };
       var result = resolver.ResolveTypeAnnotation(annotation);
       Assert.IsType<BuiltinType>(result);
       Assert.Equal(expectedClr, ((BuiltinType)result).ClrType);
   }
   ```

---

## Summary

`TypeResolver` is the bridge between **syntax** (type annotations in source code) and **semantics** (the type system the compiler reasons about). It's a straightforward translator that:

1. **Resolves** type names to their definitions
2. **Handles** generics, nullables, and builtins
3. **Caches** results for performance
4. **Recovers** from errors gracefully

**Key Takeaways:**
- **Simple but critical**: A small class (~140 lines) that every type in the program flows through.
- **Recursive by nature**: Handles nested generics naturally.
- **Performance-conscious**: Caching and singletons minimize allocations.
- **Error-tolerant**: Returns `Unknown` on failure to enable continued compilation.

When working with `TypeResolver`, always remember: **it's about translation, not validation**. Type checking comes later in `TypeChecker`.

---

## Further Reading

- **`SemanticType.cs`**: Understand the type hierarchy and assignability rules.
- **`SymbolTable.cs`**: See how types are stored and looked up.
- **`TypeChecker.cs`**: The next step in the pipeline that uses resolved types.
- **`docs/language_specification/type_annotations.md`**: Language spec for type annotations.
- **`docs/language_specification/type_hierarchy.md`**: Type hierarchy design.
- **`docs/language_specification/type_casting.md`**: The `to` operator and casting rules.
- **`docs/language_specification/type_narrowing.md`**: How types are narrowed in conditionals.
