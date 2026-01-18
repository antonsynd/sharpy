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

- **`SymbolTable`**: Lookup table for all declared types, functions, and variables in the current compilation unit. Used to resolve user-defined type names like `MyClass` to their corresponding `TypeSymbol`, as well as type aliases.

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

The resolver tries multiple approaches in a cascading order:

#### a) Builtin Types
```csharp
if (TryResolveBuiltinType(annotation.Name, out var builtinType))
{
    result = builtinType;
}
```
Check if it's a primitive like `int`, `str`, `bool`, etc. These are resolved directly to singleton `SemanticType` instances for efficiency.

#### b) Type Aliases
```csharp
else if (_symbolTable.LookupTypeAlias(annotation.Name) is TypeAliasSymbol aliasSymbol)
{
    result = ExpandTypeAlias(aliasSymbol, annotation.IsNullable);
}
```
If the name refers to a type alias (e.g., `type UserId = int`), expand the alias to its underlying type. Type aliases can point to either simple types or function types.

#### c) Generic Types
```csharp
else if (annotation.TypeArguments.Count > 0)
{
    result = ResolveGenericType(annotation);
}
```
If there are type arguments (e.g., `list[int]`, `dict[str, float]`), this is a generic type instantiation. Delegate to `ResolveGenericType()`. Note that tuples are handled specially within this method.

#### d) Type Parameters
```csharp
else if (_symbolTable.Lookup(annotation.Name) is TypeParameterSymbol typeParamSymbol)
{
    result = new TypeParameterType
    {
        Name = annotation.Name,
        DeclaringType = typeParamSymbol.DeclaringType
    };
}
```
If the name refers to a type parameter (e.g., `T` in `class Box[T]`), create a `TypeParameterType`. This allows generic classes and functions to refer to their own type parameters during semantic analysis.

#### e) User-Defined Types
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
// Handle nullable types (already handled for type aliases in ExpandTypeAlias)
// For non-alias types, apply nullable modifier here
if (annotation.IsNullable && result != SemanticType.Unknown
    && _symbolTable.LookupTypeAlias(annotation.Name) == null)
{
    result = new NullableType { UnderlyingType = result };
}
```
If the annotation has the `?` suffix (e.g., `int?`), wrap the resolved type in a `NullableType`.

**Important**: Type aliases are handled separately in `ExpandTypeAlias()`, which already applies the nullable modifier. This check skips aliases to avoid double-wrapping.

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

**Purpose:** Resolve generic types like `list[int]`, `dict[str, int]`, or `MyGenericClass[T, U]`. This method also handles the special case of tuple types.

**Signature:**
```csharp
private SemanticType ResolveGenericType(TypeAnnotation annotation)
```

**Flow:**

#### Special Case: Tuple Types
```csharp
// Special handling for tuple types - they have variable arity (tuple[int], tuple[int, str], etc.)
if (annotation.Name == "tuple")
{
    var elementTypes = annotation.TypeArguments
        .Select(ResolveTypeAnnotation)
        .ToList();

    return new TupleType { ElementTypes = elementTypes };
}
```
Tuples are treated specially because they have **variable arity**—unlike `list[T]` which always takes exactly one type argument, `tuple` can take any number: `tuple[int]`, `tuple[int, str]`, `tuple[int, str, bool]`, etc.

By creating a `TupleType` instead of a `GenericType`, we enable tuple-specific semantics like element-wise assignability checking.

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

### 4. `ExpandTypeAlias(TypeAliasSymbol aliasSymbol, bool isNullable)` — Type Alias Expansion

**Purpose:** Expand a type alias to its underlying type definition. Type aliases are compile-time only and don't generate C# code—they're purely for developer convenience.

**Signature:**
```csharp
private SemanticType ExpandTypeAlias(TypeAliasSymbol aliasSymbol, bool isNullable)
```

**Flow:**

```csharp
SemanticType result;

// Expand type annotation
if (aliasSymbol.TypeAnnotation != null)
{
    result = ResolveTypeAnnotation(aliasSymbol.TypeAnnotation);
}
// Expand function type
else if (aliasSymbol.FunctionType != null)
{
    result = ResolveFunctionType(aliasSymbol.FunctionType);
}
else
{
    AddError($"Type alias '{aliasSymbol.Name}' has no type definition",
        aliasSymbol.DeclarationLine, aliasSymbol.DeclarationColumn);
    return SemanticType.Unknown;
}
```

Type aliases can refer to either:
1. **Simple type annotations**: `type UserId = int` → expands via `ResolveTypeAnnotation()`
2. **Function types**: `type Callback = (int, str) -> bool` → expands via `ResolveFunctionType()`

**Nullable Handling:**
```csharp
// Apply nullable modifier if present at usage site
if (isNullable && result != SemanticType.Unknown)
{
    result = new NullableType { UnderlyingType = result };
}

return result;
```

The nullable modifier (`?`) is applied **at the usage site**, not at the alias definition site. For example:

```python
type UserId = int
x: UserId?   # Creates NullableType wrapping the expanded int type
```

This allows the same alias to be used in both nullable and non-nullable contexts.

---

### 5. `ResolveFunctionType(Parser.Ast.FunctionType functionType)` — Function Type Resolution

**Purpose:** Resolve function type annotations like `(int, str) -> bool`. These are used for type aliases, lambda types, and higher-order function parameters.

**Signature:**
```csharp
private Semantic.FunctionType ResolveFunctionType(Parser.Ast.FunctionType functionType)
```

**Implementation:**
```csharp
var paramTypes = functionType.ParameterTypes
    .Select(ResolveTypeAnnotation)
    .ToList();

var returnType = ResolveTypeAnnotation(functionType.ReturnType);

return new Semantic.FunctionType
{
    ParameterTypes = paramTypes,
    ReturnType = returnType
};
```

**Note the Namespace Distinction:**
- `Parser.Ast.FunctionType`: The AST representation (input)
- `Semantic.FunctionType`: The semantic representation (output)

This method recursively resolves all parameter types and the return type, then constructs a semantic function type that can be used for type checking.

**Example Usage:**
```python
type Callback = (int, str) -> bool

def apply(f: Callback, x: int, y: str) -> bool:
    return f(x, y)
```

The `Callback` alias uses `ResolveFunctionType()` to create a `FunctionType` with:
- `ParameterTypes`: `[SemanticType.Int, SemanticType.Str]`
- `ReturnType`: `SemanticType.Bool`

---

### 6. `AddError(string message, int? line, int? column)` — Error Accumulation

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

**Note:** Currently, `line` and `column` are often `null` in `TypeResolver` because the `TypeAnnotation` AST nodes don't yet consistently track location info. The exception is when resolving type aliases, where the error can include the alias definition's location from the `TypeAliasSymbol`.

---

## Dependencies

### External Components

#### 1. `SymbolTable` (`Semantic/SymbolTable.cs`)
- **Purpose**: Central registry of all symbols (types, functions, variables) in scope.
- **Used For**: Looking up user-defined types, type aliases, type parameters, and generic definitions.
- **Key Methods**:
  - `LookupType(string name)` → `TypeSymbol?`
  - `LookupTypeAlias(string name)` → `TypeAliasSymbol?`
  - `Lookup(string name)` → `Symbol?` (for type parameters)

#### 2. `SemanticInfo` (`Semantic/SemanticInfo.cs`)
- **Purpose**: Side-table mapping AST nodes to semantic data.
- **Used For**: Caching resolved types to avoid redundant work.
- **Key Methods**:
  - `GetTypeAnnotation(TypeAnnotation)` → `SemanticType?`
  - `SetTypeAnnotation(TypeAnnotation, SemanticType)`

#### 3. `SemanticType` and Subclasses (`Semantic/SemanticType.cs`)
- **Records**: `BuiltinType`, `GenericType`, `UserDefinedType`, `NullableType`, `FunctionType`, `TupleType`, `TypeParameterType`, `UnknownType`, `VoidType`
- **Used For**: Representing the resolved type system.
- **Key Feature**: Singleton instances for common types (`SemanticType.Int`, etc.).

#### 4. `Symbol` and Subclasses (`Semantic/Symbol.cs`)
- **Records**: `TypeSymbol`, `TypeAliasSymbol`, `TypeParameterSymbol`
- **Used For**: Representing declared types, aliases, and type parameters.
- **Key Fields**:
  - `TypeSymbol.TypeParameters`: List of generic type parameters
  - `TypeAliasSymbol.TypeAnnotation`: The aliased type
  - `TypeAliasSymbol.FunctionType`: For function type aliases
  - `TypeParameterSymbol.DeclaringType`: Which type declares this parameter

#### 5. `TypeAnnotation` (`Parser/Ast/Types.cs`)
- **Purpose**: AST representation of a type annotation.
- **Key Fields**:
  - `Name`: The type name (e.g., "int", "list", "MyClass")
  - `TypeArguments`: List of nested type annotations (for generics)
  - `IsNullable`: Whether the `?` suffix is present

#### 6. `Parser.Ast.FunctionType` (`Parser/Ast/Types.cs`)
- **Purpose**: AST representation of function type annotations.
- **Key Fields**:
  - `ParameterTypes`: List of parameter type annotations
  - `ReturnType`: Return type annotation

#### 7. `ICompilerLogger` (`Logging/ICompilerLogger.cs`)
- **Purpose**: Abstraction for logging errors, warnings, and info messages.
- **Used For**: Reporting type resolution errors.

### Data Flow

```
TypeAnnotation (AST)
        ↓
  TypeResolver.ResolveTypeAnnotation()
        ↓
  ┌─────┴──────┐
  ↓            ↓
SymbolTable    (recursive)
  ↓
LookupType() / LookupTypeAlias() / Lookup()
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

### 7. **Cascading Resolution Strategy**
- **Why?** Different kinds of types (builtins, aliases, generics, user types) require different resolution logic.
- **Pattern**: Check builtins first (fast path), then aliases, then generics, then type parameters, finally user types.
- **Benefit**: Fast common case (primitives), with fallback to more complex lookups.

### 8. **Tuple Special-Casing**
- **Why?** Tuples have variable arity unlike other generics, and need element-wise type checking semantics.
- **Pattern**: Check for `"tuple"` name before general generic handling.
- **Benefit**: Enables proper tuple semantics without complicating the generic type system.

---

## Debugging Tips

### Common Issues and How to Diagnose

#### 1. **"Type 'X' not found" Error**

**Symptom:** `TypeResolver` reports a missing type that you know exists.

**Causes:**
- The type wasn't added to the `SymbolTable` by `NameResolver`.
- The type is defined later in the file (forward reference issue).
- Import statement is missing or failed to resolve.
- The name is a type alias but `LookupTypeAlias()` isn't finding it.

**How to Debug:**
```csharp
// Add breakpoint in ResolveTypeAnnotation()
var typeSymbol = _symbolTable.LookupType(annotation.Name);
// Inspect _symbolTable._scopeStack to see what's in scope
// Also check:
var alias = _symbolTable.LookupTypeAlias(annotation.Name);
```

**Fix Strategy:**
- Check `NameResolver` to ensure it visits class/type definitions and type aliases.
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

#### 4. **Type Alias Expansion Issues**

**Symptom:** Type alias doesn't expand correctly, or nullable modifier is applied incorrectly.

**Causes:**
- The `TypeAliasSymbol` has neither `TypeAnnotation` nor `FunctionType` set.
- Nullable modifier is being double-applied (both in alias and at usage site).

**How to Debug:**
```csharp
// In ExpandTypeAlias(), check:
aliasSymbol.TypeAnnotation  // Should be non-null for simple aliases
aliasSymbol.FunctionType    // Should be non-null for function aliases
// Verify only one is set, not both
```

**Fix Strategy:**
- Ensure `NameResolver` properly creates `TypeAliasSymbol` instances.
- Check that the nullable wrapping logic in `ResolveTypeAnnotation()` skips aliases.

#### 5. **Type Parameter Resolution Failure**

**Symptom:** Inside a generic class, type parameter `T` is not recognized.

**Causes:**
- Type parameters weren't registered in the `SymbolTable` during name resolution.
- The `TypeParameterSymbol` doesn't have the correct `DeclaringType` set.

**How to Debug:**
```csharp
// In ResolveTypeAnnotation(), check:
var symbol = _symbolTable.Lookup(annotation.Name);
// Verify it's a TypeParameterSymbol
// Check symbol.DeclaringType points to the correct generic type
```

**Fix Strategy:**
- Ensure `NameResolver` creates `TypeParameterSymbol` entries for generic type parameters.
- Verify the declaring type relationship is correctly established.

#### 6. **Recursive Type Definitions (Future Issue)**

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
**Current Issue:** Errors often don't include line/column numbers because `TypeAnnotation` nodes don't consistently track location.

**Task:**
- Modify `TypeAnnotation` AST nodes to include `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`.
- Update `AddError()` calls to extract location from annotation nodes:
  ```csharp
  AddError($"Type '{annotation.Name}' not found",
           annotation.LineStart, annotation.ColumnStart);
  ```

**Impact:** Better error messages for users.

**Difficulty:** Medium (requires parser changes)

---

#### 2. **Cycle Detection for Recursive Types**
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

**Difficulty:** Medium-Hard (requires careful design of recursive type semantics)

---

#### 3. **Better Error Messages**
**Current Issue:** Generic errors like "Type 'X' not found" aren't actionable.

**Task:**
- Add suggestions using Levenshtein distance:
  ```csharp
  var suggestions = FindSimilarTypeNames(annotation.Name);
  if (suggestions.Any())
      AddError($"Type '{annotation.Name}' not found. Did you mean '{suggestions.First()}'?", ...);
  else
      AddError($"Type '{annotation.Name}' not found.", ...);
  ```
- Distinguish between "not imported" vs. "doesn't exist".
- For arity mismatches, suggest correct types (e.g., "Did you mean tuple[int, str] instead of list[int, str]?").

**Impact:** Significantly better developer experience.

**Difficulty:** Medium

---

#### 4. **Support for Union Types**
**Current Issue:** No support for `int | str` union type syntax.

**Task:**
- Extend `TypeAnnotation` to support union syntax in parser.
- Add `UnionType` semantic type.
- In `ResolveTypeAnnotation()`, handle union annotations:
  ```csharp
  if (annotation is UnionTypeAnnotation union)
  {
      var types = union.Types.Select(ResolveTypeAnnotation).ToList();
      return new UnionType { Types = types };
  }
  ```

**Impact:** Enable Python-style union types (major language feature).

**Difficulty:** Hard (requires language design decisions and parser changes)

---

#### 5. **Validation of Type Parameter Constraints**
**Current Issue:** No validation that type arguments satisfy constraints.

**Example:**
```python
class Box[T: Comparable]:  # T must implement Comparable
    pass

Box[int]      # OK if int implements Comparable
Box[object]   # Should error if object doesn't
```

**Task:**
- Store constraints in `TypeParameterDef`.
- In `ResolveGenericType()`, validate each type argument against its constraint:
  ```csharp
  for (int i = 0; i < typeArgs.Count; i++)
  {
      var arg = typeArgs[i];
      var param = typeSymbol.TypeParameters[i];
      if (param.Constraint != null && !arg.SatisfiesConstraint(param.Constraint))
      {
          AddError($"Type '{arg.GetDisplayName()}' does not satisfy constraint '{param.Constraint}'", ...);
      }
  }
  ```

**Impact:** Enable constrained generics (major language feature).

**Difficulty:** Hard

---

#### 6. **Performance Optimization: Intern Type Names**
**Current Issue:** String comparisons for type names may be inefficient at scale.

**Task:**
- Use string interning for type names to enable reference equality checks.
- In `TryResolveBuiltinType()`, compare interned strings by reference instead of value.

**Impact:** Minor performance improvement in large codebases.

**Difficulty:** Easy-Medium

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

2. **Test type aliases**:
   ```csharp
   [Fact]
   public void ResolveTypeAlias_SimpleAlias_ExpandsToUnderlyingType()
   {
       var alias = new TypeAliasSymbol
       {
           Name = "UserId",
           TypeAnnotation = new TypeAnnotation { Name = "int" }
       };
       symbolTable.AddTypeAlias(alias);

       var annotation = new TypeAnnotation { Name = "UserId" };
       var result = resolver.ResolveTypeAnnotation(annotation);
       Assert.Equal(SemanticType.Int, result);
   }
   ```

3. **Test function types**:
   ```csharp
   [Fact]
   public void ResolveFunctionType_SimpleFunction_ResolvesCorrectly()
   {
       var funcType = new Parser.Ast.FunctionType
       {
           ParameterTypes = new List<TypeAnnotation>
           {
               new TypeAnnotation { Name = "int" },
               new TypeAnnotation { Name = "str" }
           },
           ReturnType = new TypeAnnotation { Name = "bool" }
       };

       var result = resolver.ResolveFunctionType(funcType);
       Assert.Equal(2, result.ParameterTypes.Count);
       Assert.Equal(SemanticType.Int, result.ParameterTypes[0]);
       Assert.Equal(SemanticType.Str, result.ParameterTypes[1]);
       Assert.Equal(SemanticType.Bool, result.ReturnType);
   }
   ```

4. **Test type parameters**:
   ```csharp
   [Fact]
   public void ResolveTypeParameter_InsideGenericClass_ResolvesCorrectly()
   {
       var typeSymbol = new TypeSymbol { Name = "Box" };
       var paramSymbol = new TypeParameterSymbol
       {
           Name = "T",
           DeclaringType = typeSymbol
       };
       symbolTable.Add("T", paramSymbol);

       var annotation = new TypeAnnotation { Name = "T" };
       var result = resolver.ResolveTypeAnnotation(annotation);

       Assert.IsType<TypeParameterType>(result);
       Assert.Equal("T", ((TypeParameterType)result).Name);
       Assert.Equal(typeSymbol, ((TypeParameterType)result).DeclaringType);
   }
   ```

5. **Test error cases**:
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

6. **Test caching**:
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

7. **Test tuple types**:
   ```csharp
   [Fact]
   public void ResolveGenericType_Tuple_CreatesTupleType()
   {
       var annotation = new TypeAnnotation
       {
           Name = "tuple",
           TypeArguments = new List<TypeAnnotation>
           {
               new TypeAnnotation { Name = "int" },
               new TypeAnnotation { Name = "str" }
           }
       };

       var result = resolver.ResolveTypeAnnotation(annotation);
       Assert.IsType<TupleType>(result);
       var tuple = (TupleType)result;
       Assert.Equal(2, tuple.ElementTypes.Count);
       Assert.Equal(SemanticType.Int, tuple.ElementTypes[0]);
       Assert.Equal(SemanticType.Str, tuple.ElementTypes[1]);
   }
   ```

8. **Test float type mappings**:
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

9. **Integration tests** with full compilation pipeline:
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

---

## Summary

`TypeResolver` is the bridge between **syntax** (type annotations in source code) and **semantics** (the type system the compiler reasons about). It's a straightforward translator that:

1. **Resolves** type names to their definitions (builtins, user types, aliases, type parameters)
2. **Handles** generics, tuples, nullables, and function types
3. **Expands** type aliases transparently
4. **Caches** results for performance
5. **Recovers** from errors gracefully

**Key Takeaways:**
- **Simple but critical**: A focused class (~210 lines) that every type in the program flows through.
- **Recursive by nature**: Handles nested generics and complex type structures naturally.
- **Performance-conscious**: Caching and singletons minimize allocations.
- **Error-tolerant**: Returns `Unknown` on failure to enable continued compilation and better error reporting.
- **Extensible**: New type forms (unions, intersections, etc.) can be added by extending the resolution cascade.

When working with `TypeResolver`, always remember: **it's about translation, not validation**. Type checking comes later in `TypeChecker`.

---

## Cross-References

### Related Semantic Analysis Components
- **[SemanticType.md](SemanticType.md)**: Understand the type hierarchy and assignability rules
- **[Symbol.md](Symbol.md)**: Learn about type symbols, aliases, and parameters
- **[SymbolTable.md](SymbolTable.md)**: See how types are stored and looked up
- **[SemanticInfo.md](SemanticInfo.md)**: Understand the caching mechanism
- **[TypeChecker.md](TypeChecker.md)**: The next step that validates types
- **[NameResolver.md](NameResolver.md)**: The previous step that creates symbols

### Language Specifications
- **`docs/language_specification/type_annotations.md`**: Type annotation syntax
- **`docs/language_specification/type_hierarchy.md`**: Type hierarchy design
- **`docs/language_specification/type_casting.md`**: The `to` operator and casting rules
- **`docs/language_specification/type_narrowing.md`**: How types are narrowed in conditionals
