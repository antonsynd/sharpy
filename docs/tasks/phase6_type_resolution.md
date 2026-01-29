# Phase 6: Type Resolution Updates

## Overview

This phase updates the TypeResolver to correctly resolve the new type annotations:
- `T?` → `OptionalType`
- `T | None` → `NullableType`
- `T !E` → `ResultType`

**Prerequisites:** 
- Phase 3 (AST changes)
- Phase 4 (Parser updates)
- Phase 5 (Semantic types)

**Files to modify:**
- `src/Sharpy.Compiler/Semantic/TypeResolver.cs`
- `src/Sharpy.Compiler/Semantic/TypeAnnotationHelper.cs` (if exists)

**Files to create:**
- `src/Sharpy.Compiler.Tests/Semantic/TypeResolverOptionalResultTests.cs`

---

## Task 6.1: Update TypeResolver for OptionalType

**File:** `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

### Steps

- [x] Open `src/Sharpy.Compiler/Semantic/TypeResolver.cs`
- [x] Find the main type resolution method (likely `ResolveType` or `Resolve`)
- [x] Locate where `IsNullable` (now `IsOptional`) is handled
- [x] Update to create `OptionalType` instead of `NullableType`:

**Before (approximate):**
```csharp
public SemanticType ResolveType(TypeAnnotation annotation)
{
    var baseType = ResolveBaseType(annotation);
    
    // Old: T? created NullableType
    if (annotation.IsNullable)
    {
        return new NullableType { UnderlyingType = baseType };
    }
    
    return baseType;
}
```

**After:**
```csharp
public SemanticType ResolveType(TypeAnnotation annotation)
{
    var baseType = ResolveBaseType(annotation);
    
    // Handle T !E (Result type) - must come before T?
    if (annotation.ErrorType != null)
    {
        var errorType = ResolveType(annotation.ErrorType);
        baseType = new ResultType 
        { 
            OkType = baseType, 
            ErrorType = errorType 
        };
    }
    
    // Handle T? (Optional type) - Sharpy native optional
    if (annotation.IsOptional)
    {
        baseType = new OptionalType { UnderlyingType = baseType };
    }
    
    // Handle T | None (C# nullable) - .NET interop
    if (annotation.IsCSharpNullable)
    {
        baseType = new NullableType { UnderlyingType = baseType };
    }
    
    return baseType;
}
```

### Order of Resolution

The order matters for correct semantics:
1. Resolve base type (including generics)
2. Apply `!E` to create `ResultType`
3. Apply `?` to create `OptionalType`  
4. Apply `| None` to create `NullableType`

This means:
- `int !E?` → `Optional[Result[int, E]]`
- `int !E | None` → `Result[int, E] | None` (nullable result)

### Verification

- [x] Build: `dotnet build src/Sharpy.Compiler`
- [x] No compiler errors

```
git add src/Sharpy.Compiler/Semantic/TypeResolver.cs
git commit -m "semantic: update TypeResolver for OptionalType and ResultType"
```

---

## Task 6.2: Handle "Optional" and "Result" as Type Names

**File:** `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

### Steps

When users write explicit `Optional[T]` or `Result[T, E]`, the resolver should recognize these:

- [x] In the method that resolves type names, add handling:
  ```csharp
  private SemanticType ResolveBaseType(TypeAnnotation annotation)
  {
      var name = annotation.Name;
      
      // Handle explicit Optional[T] syntax
      if (name == "Optional" && annotation.TypeArguments.Length == 1)
      {
          var underlyingType = ResolveType(annotation.TypeArguments[0]);
          return new OptionalType { UnderlyingType = underlyingType };
      }
      
      // Handle explicit Result[T, E] syntax
      if (name == "Result" && annotation.TypeArguments.Length == 2)
      {
          var okType = ResolveType(annotation.TypeArguments[0]);
          var errorType = ResolveType(annotation.TypeArguments[1]);
          return new ResultType { OkType = okType, ErrorType = errorType };
      }
      
      // ... existing resolution logic ...
  }
  ```

### Verification

- [x] Build: `dotnet build src/Sharpy.Compiler`
- [x] No compiler errors

```
git add src/Sharpy.Compiler/Semantic/TypeResolver.cs
git commit -m "semantic: recognize Optional[T] and Result[T, E] as type names"
```

---

## Task 6.3: Add Integration Tests for Type Resolution

**File:** `src/Sharpy.Compiler.Tests/Semantic/TypeResolverOptionalResultTests.cs`

### Steps

- [x] Create new file `src/Sharpy.Compiler.Tests/Semantic/TypeResolverOptionalResultTests.cs`
- [x] Add comprehensive tests:

```csharp
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class TypeResolverOptionalResultTests
{
    private SemanticType ResolveTypeFromCode(string typeAnnotation)
    {
        // Parse a variable declaration and resolve the type
        var code = $"x: {typeAnnotation} = None";
        var lexer = new Lexer(code);
        var parser = new Parser(lexer.Tokenize());
        var module = parser.Parse();
        
        // Create a TypeResolver with appropriate context
        // (This may need adjustment based on actual TypeResolver API)
        var resolver = new TypeResolver(/* dependencies */);
        
        var stmt = module.Statements[0] as VariableDeclaration;
        return resolver.ResolveType(stmt!.TypeAnnotation!);
    }
    
    #region Optional (T?) Resolution
    
    [Fact]
    public void Resolve_OptionalInt_ReturnsOptionalType()
    {
        var type = ResolveTypeFromCode("int?");
        
        Assert.IsType<OptionalType>(type);
        var opt = (OptionalType)type;
        Assert.Equal("int", opt.UnderlyingType.GetDisplayName());
    }
    
    [Fact]
    public void Resolve_OptionalString_ReturnsOptionalType()
    {
        var type = ResolveTypeFromCode("str?");
        
        Assert.IsType<OptionalType>(type);
    }
    
    [Fact]
    public void Resolve_OptionalGeneric_ReturnsOptionalType()
    {
        var type = ResolveTypeFromCode("list[int]?");
        
        Assert.IsType<OptionalType>(type);
        var opt = (OptionalType)type;
        Assert.IsType<GenericType>(opt.UnderlyingType);
    }
    
    [Fact]
    public void Resolve_ExplicitOptional_ReturnsOptionalType()
    {
        var type = ResolveTypeFromCode("Optional[int]");
        
        Assert.IsType<OptionalType>(type);
    }
    
    #endregion
    
    #region C# Nullable (T | None) Resolution
    
    [Fact]
    public void Resolve_CSharpNullable_ReturnsNullableType()
    {
        var type = ResolveTypeFromCode("str | None");
        
        Assert.IsType<NullableType>(type);
        var nullable = (NullableType)type;
        Assert.Equal("str", nullable.UnderlyingType.GetDisplayName());
    }
    
    [Fact]
    public void Resolve_CSharpNullableGeneric_ReturnsNullableType()
    {
        var type = ResolveTypeFromCode("list[int] | None");
        
        Assert.IsType<NullableType>(type);
    }
    
    #endregion
    
    #region Result (T !E) Resolution
    
    [Fact]
    public void Resolve_ResultType_ReturnsResultType()
    {
        var type = ResolveTypeFromCode("int !ValueError");
        
        Assert.IsType<ResultType>(type);
        var result = (ResultType)type;
        Assert.Equal("int", result.OkType.GetDisplayName());
        Assert.Equal("ValueError", result.ErrorType.GetDisplayName());
    }
    
    [Fact]
    public void Resolve_ResultWithGenericOk_ReturnsResultType()
    {
        var type = ResolveTypeFromCode("list[int] !ParseError");
        
        Assert.IsType<ResultType>(type);
        var result = (ResultType)type;
        Assert.IsType<GenericType>(result.OkType);
    }
    
    [Fact]
    public void Resolve_ExplicitResult_ReturnsResultType()
    {
        var type = ResolveTypeFromCode("Result[int, ValueError]");
        
        Assert.IsType<ResultType>(type);
    }
    
    #endregion
    
    #region Combined Modifiers
    
    [Fact]
    public void Resolve_ResultWithNullable_ReturnsNullableResult()
    {
        // int !ValueError | None → NullableType wrapping ResultType
        var type = ResolveTypeFromCode("int !ValueError | None");
        
        Assert.IsType<NullableType>(type);
        var nullable = (NullableType)type;
        Assert.IsType<ResultType>(nullable.UnderlyingType);
    }
    
    [Fact]
    public void Resolve_OptionalVsNullable_AreDifferentTypes()
    {
        var optional = ResolveTypeFromCode("int?");
        var nullable = ResolveTypeFromCode("int | None");
        
        Assert.IsType<OptionalType>(optional);
        Assert.IsType<NullableType>(nullable);
        Assert.False(optional.Equals(nullable));
    }
    
    #endregion
    
    #region Display Names
    
    [Fact]
    public void Resolve_OptionalType_DisplayNameHasQuestion()
    {
        var type = ResolveTypeFromCode("int?");
        Assert.Equal("int?", type.GetDisplayName());
    }
    
    [Fact]
    public void Resolve_ResultType_DisplayNameHasBang()
    {
        var type = ResolveTypeFromCode("int !ValueError");
        Assert.Equal("int !ValueError", type.GetDisplayName());
    }
    
    [Fact]
    public void Resolve_NullableType_DisplayNameHasQuestion()
    {
        var type = ResolveTypeFromCode("int | None");
        // NullableType also uses ? in display (C# convention)
        Assert.Contains("?", type.GetDisplayName());
    }
    
    #endregion
}
```

### Note on Test Setup

> ⚠️ **IMPORTANT:** The test helper `ResolveTypeFromCode` is a **simplified example** that may not work directly with the actual codebase architecture.

The test helper may need adjustment based on:
- How `TypeResolver` is constructed (dependencies, services)
- Whether it needs a full `CompilerServices` setup
- Symbol table initialization for `ValueError` etc.

**Recommended approach:** Check how existing type resolution tests are structured in the codebase and follow that pattern. You may need to:
1. Use `IntegrationTestBase` if it exists
2. Set up a minimal `CompilerServices` context
3. Register necessary types (like `ValueError`) in a symbol table before testing

Consider creating a simpler test that directly creates `TypeAnnotation` AST nodes:

```csharp
[Fact]
public void Resolve_DirectAst_OptionalType()
{
    var annotation = new TypeAnnotation 
    { 
        Name = "int", 
        IsOptional = true 
    };
    
    var resolver = new TypeResolver(/* minimal deps */);
    var type = resolver.ResolveType(annotation);
    
    Assert.IsType<OptionalType>(type);
}
```

### Verification

- [x] Run tests: `dotnet test src/Sharpy.Compiler.Tests --filter TypeResolverOptionalResultTests`
- [x] All tests pass (or adjust test setup as needed)

```
git add src/Sharpy.Compiler.Tests/Semantic/TypeResolverOptionalResultTests.cs
git commit -m "test: add type resolver tests for Optional and Result"
```

---

## Task 6.4: Update TypeChecker to Handle New Types

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.*.cs`

### Steps

The TypeChecker needs to understand the new types for:
- Assignment compatibility
- Return type checking
- Argument type checking

- [x] Search for places that check `NullableType`:
  ```bash
  grep -r "NullableType" src/Sharpy.Compiler/Semantic --include="*.cs"
  ```
- [x] For each occurrence, determine if it should also handle `OptionalType` or `ResultType`
- [x] Common patterns to update:
  ```csharp
  // Before: Only checked NullableType
  if (type is NullableType nullable)
  {
      // handle nullable
  }
  
  // After: Check all "nullable-like" types
  if (type is NullableType nullable)
  {
      // handle C# nullable
  }
  else if (type is OptionalType optional)
  {
      // handle Sharpy optional
  }
  else if (type is ResultType result)
  {
      // handle result type
  }
  ```

### Key Areas to Check

- [x] **Null literal assignment:** `None` should be assignable to `NullableType` but not `OptionalType`
  - For `OptionalType`, use `Nothing` instead (temporarily allowing None until Phase 7)
- [x] **Return type checking:** Ensure function return types are resolved correctly
- [x] **Variable assignment:** Ensure type compatibility is checked correctly

### Verification

- [x] Build: `dotnet build src/Sharpy.Compiler`
- [x] Run existing type checker tests

```
git add src/Sharpy.Compiler/Semantic/TypeChecker*.cs
git commit -m "semantic: update TypeChecker for OptionalType and ResultType"
```

---

## Task 6.5: Verify Existing Tests Still Pass

### Steps

- [x] Run all semantic tests: `dotnet test src/Sharpy.Compiler.Tests --filter "Semantic"`
- [x] Run all integration tests: `dotnet test src/Sharpy.Compiler.Tests --filter "Integration"`
- [x] Investigate and fix any failures

### Common Issues

1. **Tests expecting `NullableType` for `T?`** — should now expect `OptionalType`
2. **Tests for None assignment** — `None` behavior differs between `OptionalType` and `NullableType`
3. **Display name assertions** — may need updating

### Verification

- [x] All tests pass

```
# If fixes were needed:
git add -A
git commit -m "fix: update tests for OptionalType vs NullableType"
```

---

## Final Verification

- [x] Build entire compiler: `dotnet build src/Sharpy.Compiler`
- [x] Run all tests: `dotnet test src/Sharpy.Compiler.Tests`
- [x] All tests pass
- [x] Review all commits in this phase

```
git log --oneline -5
```

Expected commits:
1. `semantic: update TypeResolver for OptionalType and ResultType`
2. `semantic: recognize Optional[T] and Result[T, E] as type names`
3. `test: add type resolver tests for Optional and Result`
4. `semantic: update TypeChecker for OptionalType and ResultType`
5. `fix: update tests for OptionalType vs NullableType` (if needed)

---

## Notes for Implementer

- **Breaking change:** `T?` now resolves to `OptionalType` instead of `NullableType`. This is intentional per the language redesign.

- **None literal:** After this change, `None` should NOT be directly assignable to `OptionalType`. Users should use `Nothing` instead. However, this enforcement may come in Phase 7 with constructor recognition.

- **Explicit syntax:** Both shorthand (`T?`, `T !E`) and explicit (`Optional[T]`, `Result[T, E]`) should resolve to the same semantic types.

- **Test setup complexity:** Creating a full `TypeResolver` may require `CompilerServices`. Consider using integration tests with the full compilation pipeline rather than trying to unit test `TypeResolver` in isolation.
