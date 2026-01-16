# Implementation Plan: Task 0.1.9.8 - Type Alias Expansion

## Overview

Type aliases are **compile-time only** constructs. They are expanded at every usage point, and NO C# declaration is generated for the alias itself.

**Example:**
```python
type UserId = int
type StringList = list[str]
type Callback = (int, str) -> bool

x: UserId = 42           # Treated as int
items: StringList = []   # Treated as list[str]
```

---

## Step-by-Step Implementation

### Step 1: Add TypeAliasSymbol to Symbol System

**File:** `src/Sharpy.Compiler/Semantic/Symbol.cs`

Add a new symbol class for type aliases:

```csharp
/// <summary>
/// Type alias symbol (type UserId = int)
/// Compile-time only - expanded at usage points, no C# output
/// </summary>
public record TypeAliasSymbol : Symbol
{
    /// <summary>Target type annotation (for regular type aliases)</summary>
    public TypeAnnotation? TargetType { get; init; }

    /// <summary>Target function type (for function type aliases)</summary>
    public FunctionType? TargetFunctionType { get; init; }

    /// <summary>Resolved semantic type (cached after first resolution)</summary>
    public SemanticType? ResolvedType { get; set; }
}
```

Update `SymbolKind` enum:
```csharp
public enum SymbolKind
{
    Variable,
    Parameter,
    Function,
    Type,
    Module,
    Property,
    TypeAlias  // NEW
}
```

---

### Step 2: Handle TypeAlias in NameResolver (Declaration Phase)

**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`

Add case to `ResolveDeclaration()` method (around line 97):

```csharp
case TypeAlias typeAlias:
    ResolveTypeAliasDeclaration(typeAlias);
    break;
```

Add new method `ResolveTypeAliasDeclaration()`:

```csharp
private void ResolveTypeAliasDeclaration(TypeAlias typeAlias)
{
    _logger.LogDebug($"Resolving type alias declaration: {typeAlias.Name}");

    // Check for redefinition
    if (_symbolTable.Lookup(typeAlias.Name, searchParents: false) != null)
    {
        AddError($"Type alias '{typeAlias.Name}' conflicts with existing definition",
            typeAlias.LineStart, typeAlias.ColumnStart);
        return;
    }

    var aliasSymbol = new TypeAliasSymbol
    {
        Name = typeAlias.Name,
        Kind = SymbolKind.TypeAlias,
        AccessLevel = AccessLevel.Public,
        TargetType = typeAlias.Type,
        TargetFunctionType = typeAlias.FunctionType,
        DeclarationLine = typeAlias.LineStart,
        DeclarationColumn = typeAlias.ColumnStart
    };

    _symbolTable.Define(aliasSymbol);
}
```

---

### Step 3: Expand Type Aliases in TypeResolver

**File:** `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

This is the key change. Modify `ResolveTypeAnnotation()` to expand aliases.

**Add field for recursion detection:**
```csharp
private readonly HashSet<string> _resolvingAliases = new();
```

**Modify type resolution logic (between builtin check and generic check):**

After line 49 (after `result = builtinType;`), before the generic type check:

```csharp
// NEW: Check for type alias and expand it
else if (TryExpandTypeAlias(annotation.Name, out var aliasType))
{
    result = aliasType;
}
```

**Add helper methods:**

```csharp
private bool TryExpandTypeAlias(string name, out SemanticType type)
{
    type = null!;

    var symbol = _symbolTable.Lookup(name);
    if (symbol is not TypeAliasSymbol aliasSymbol)
        return false;

    // Check for circular alias (A -> B -> A)
    if (_resolvingAliases.Contains(name))
    {
        AddError($"Circular type alias detected: '{name}'");
        type = SemanticType.Unknown;
        return true;
    }

    // Return cached resolution if available
    if (aliasSymbol.ResolvedType != null)
    {
        type = aliasSymbol.ResolvedType;
        return true;
    }

    // Mark as being resolved (for circular detection)
    _resolvingAliases.Add(name);

    try
    {
        if (aliasSymbol.TargetType != null)
        {
            // Regular type alias - recursively resolve target
            type = ResolveTypeAnnotation(aliasSymbol.TargetType);
        }
        else if (aliasSymbol.TargetFunctionType != null)
        {
            // Function type alias
            type = ResolveFunctionTypeAlias(aliasSymbol.TargetFunctionType);
        }
        else
        {
            AddError($"Type alias '{name}' has no target type");
            type = SemanticType.Unknown;
        }

        // Cache the resolved type
        aliasSymbol.ResolvedType = type;
        return true;
    }
    finally
    {
        _resolvingAliases.Remove(name);
    }
}

private SemanticType ResolveFunctionTypeAlias(Parser.Ast.FunctionType funcType)
{
    var paramTypes = funcType.ParameterTypes
        .Select(ResolveTypeAnnotation)
        .ToList();
    var returnType = ResolveTypeAnnotation(funcType.ReturnType);

    return new FunctionType  // FunctionType is in SemanticType.cs (line 225)
    {
        ParameterTypes = paramTypes,
        ReturnType = returnType
    };
}
```

---

### Step 4: Skip Type Aliases in Code Generation

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

The existing code already handles this - the `GenerateStatement()` method at line 443 returns `null` for unrecognized statements via the `_ => null` default case. TypeAlias will fall through to this case.

For clarity and explicit documentation, add an explicit case (around line 451, after EnumDef):

```csharp
TypeAlias => null,  // Type aliases are compile-time only, no C# output
```

---

### Step 5: Add Tests

**File:** `src/Sharpy.Compiler.Tests/Semantic/TypeAliasTests.cs` (new file)

```csharp
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeAliasTests
{
    // Test 1: Basic builtin type alias
    [Fact]
    public void ExpandsBuiltinTypeAlias()
    {
        // type UserId = int
        // x: UserId = 42
        // Verify x is resolved as int
    }

    // Test 2: Generic type alias
    [Fact]
    public void ExpandsGenericTypeAlias()
    {
        // type StringList = list[str]
        // items: StringList = []
        // Verify items is resolved as list[str]
    }

    // Test 3: Function type alias
    [Fact]
    public void ExpandsFunctionTypeAlias()
    {
        // type Callback = (int, str) -> bool
        // cb: Callback = ...
        // Verify cb is resolved as function type
    }

    // Test 4: Transitive alias expansion
    [Fact]
    public void ExpandsTransitiveAliases()
    {
        // type A = int
        // type B = A
        // x: B = 42
        // Verify x is resolved as int (not A, not B)
    }

    // Test 5: Circular alias detection
    [Fact]
    public void DetectsCircularAlias()
    {
        // type A = B
        // type B = A
        // Should produce error about circular type alias
    }

    // Test 6: Duplicate alias error
    [Fact]
    public void ReportsDuplicateAliasError()
    {
        // type UserId = int
        // type UserId = str  // Error: already defined
    }

    // Test 7: No C# output for aliases
    [Fact]
    public void NoCodeGeneratedForAlias()
    {
        // Compile module with type alias
        // Verify generated C# contains no alias declaration
    }

    // Test 8: Nullable type alias
    [Fact]
    public void ExpandsNullableTypeAlias()
    {
        // type OptionalId = int?
        // Verify resolution as NullableType with int underlying
    }
}
```

---

## Files to Modify

| File | Changes | Lines Affected |
|------|---------|----------------|
| `Symbol.cs` | Add `TypeAliasSymbol` class, add `TypeAlias` to `SymbolKind` enum | After line 111, line 120 |
| `NameResolver.cs` | Add `case TypeAlias` in switch, add `ResolveTypeAliasDeclaration()` | Line 97, new method |
| `TypeResolver.cs` | Add `_resolvingAliases` field, add `TryExpandTypeAlias()`, add `ResolveFunctionTypeAlias()` | Line 14, after line 49, new methods |
| `RoslynEmitter.cs` | Add explicit `TypeAlias => null` case (optional) | Line 451 |
| `TypeAliasTests.cs` | New test file | New file |

---

## Key Design Decisions

1. **No SemanticType subclass for aliases**: Aliases are expanded immediately to their target type. There is no `AliasType` - we return the underlying type directly.

2. **Recursion detection**: Track which aliases are currently being resolved to detect circular definitions like `type A = B` + `type B = A`.

3. **Caching**: After resolving an alias once, cache the result on `TypeAliasSymbol.ResolvedType` to avoid repeated resolution.

4. **Module-level only**: Type aliases should only appear at module level. The current parser/NameResolver structure already enforces this by only processing top-level statements.

5. **No special handling in code generation**: Type aliases naturally produce no output since `GenerateStatement()` returns `null` for them.

---

## Potential Risks

1. **Forward references**: If `type A = B` appears before `type B = int`, does resolution work?
   - **Mitigation**: NameResolver registers all aliases first (pass 1), TypeResolver expands them later. This should handle forward references correctly.

2. **Alias shadowing builtins**: `type int = str` could shadow the builtin int.
   - **Mitigation**: SymbolTable already checks for redefinition. Consider adding a warning.

3. **Generic alias parameters**: `type IntList[T] = list[T]` - generic type aliases.
   - **Note**: This is NOT in scope for this task. The current `TypeAlias` AST doesn't support type parameters.

4. **FunctionType**: ✅ Verified - `FunctionType` class exists in `SemanticType.cs` (line 225) with `ParameterTypes` and `ReturnType` properties.

---

## Verification Checklist

- [ ] `type UserId = int` registers a TypeAliasSymbol in symbol table
- [ ] `x: UserId` resolves to `SemanticType.Int` (not to an alias type)
- [ ] `type StringList = list[str]` works with generic types
- [ ] `type Callback = (int, str) -> bool` works with function types
- [ ] `type A = B; type B = A` produces circular alias error
- [ ] `type UserId = int; type UserId = str` produces duplicate error
- [ ] Generated C# contains no type alias declarations
- [ ] All existing tests still pass
- [ ] Transitive aliases expand fully (`type A = B; type B = int` → `A` = `int`)
