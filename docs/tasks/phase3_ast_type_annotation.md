# Phase 3: AST Changes for Type Annotations

## Overview

This phase updates the `TypeAnnotation` AST node to distinguish between:
- `T?` → `Optional[T]` (Sharpy-native optional)
- `T | None` → C# nullable (interop)
- `T !E` → `Result[T, E]`

Currently, the AST only has `IsNullable` which conflates `T?` and `T | None`.

**Prerequisites:** None (can be done in parallel with Phases 1-2)

**Files to modify:**
- `src/Sharpy.Compiler/Parser/Ast/Types.cs`

**Files to create:**
- `src/Sharpy.Compiler.Tests/Ast/TypeAnnotationTests.cs`

---

## Task 3.1: Update TypeAnnotation Record

**File:** `src/Sharpy.Compiler/Parser/Ast/Types.cs`

### Steps

- [ ] Open `src/Sharpy.Compiler/Parser/Ast/Types.cs`
- [ ] Find the `TypeAnnotation` record (should be near the top)
- [ ] Current structure looks like:
  ```csharp
  public record TypeAnnotation
  {
      public string Name { get; init; } = "";
      public ImmutableArray<TypeAnnotation> TypeArguments { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
      public bool IsNullable { get; init; }  // T? syntax
      // ... source location fields ...
  }
  ```
- [ ] Rename `IsNullable` to `IsCSharpNullable` and update its comment:
  ```csharp
  /// <summary>
  /// True if this type uses T | None syntax (C# nullable interop).
  /// This is distinct from IsOptional (T? syntax for Optional[T]).
  /// </summary>
  public bool IsCSharpNullable { get; init; }  // T | None syntax
  ```
- [ ] Add `IsOptional` property:
  ```csharp
  /// <summary>
  /// True if this type uses T? syntax (desugars to Optional[T]).
  /// This is distinct from IsCSharpNullable (T | None for C# interop).
  /// </summary>
  public bool IsOptional { get; init; }  // T? syntax
  ```
- [ ] Add `ErrorType` property for result syntax:
  ```csharp
  /// <summary>
  /// The error type E in T !E syntax (desugars to Result[T, E]).
  /// Null if this is not a result type.
  /// </summary>
  public TypeAnnotation? ErrorType { get; init; }  // E in T !E
  ```
- [ ] Add helper properties for clarity:
  ```csharp
  /// <summary>
  /// True if this type uses T !E syntax (desugars to Result[T, E]).
  /// </summary>
  public bool IsResult => ErrorType != null;
  ```

### Final Structure

```csharp
public record TypeAnnotation
{
    public string Name { get; init; } = "";
    public ImmutableArray<TypeAnnotation> TypeArguments { get; init; } = ImmutableArray<TypeAnnotation>.Empty;
    
    /// <summary>
    /// True if this type uses T? syntax (desugars to Optional[T]).
    /// </summary>
    public bool IsOptional { get; init; }
    
    /// <summary>
    /// True if this type uses T | None syntax (C# nullable interop).
    /// </summary>
    public bool IsCSharpNullable { get; init; }
    
    /// <summary>
    /// The error type E in T !E syntax (desugars to Result[T, E]).
    /// Null if this is not a result type.
    /// </summary>
    public TypeAnnotation? ErrorType { get; init; }
    
    /// <summary>
    /// True if this type uses T !E syntax.
    /// </summary>
    public bool IsResult => ErrorType != null;

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}
```

### Verification

- [ ] Build the compiler: `dotnet build src/Sharpy.Compiler`
- [ ] **EXPECT ERRORS** — This will break code that uses `IsNullable`

```
git add src/Sharpy.Compiler/Parser/Ast/Types.cs
git commit -m "ast: update TypeAnnotation for T?, T | None, and T !E syntax"
```

---

## Task 3.2: Update All References from IsNullable to IsOptional

**Search and Replace**

The rename from `IsNullable` to having two separate properties will break existing code. We need to update all references.

### Steps

- [ ] Search the codebase for `IsNullable` usage in type annotation contexts:
  ```bash
  grep -r "IsNullable" src/Sharpy.Compiler --include="*.cs"
  ```
- [ ] For each occurrence, determine the correct replacement:
  - If it's about `T?` syntax → use `IsOptional`
  - If it's about C# nullable interop → use `IsCSharpNullable`
  - If it's about either/both → may need to check both properties

### Files Likely to Need Updates

Based on typical compiler structure:

- [ ] `src/Sharpy.Compiler/Parser/Parser.Types.cs` — where `IsNullable = true` is set
- [ ] `src/Sharpy.Compiler/Semantic/TypeResolver.cs` — resolves type annotations
- [ ] `src/Sharpy.Compiler/Semantic/TypeChecker.*.cs` — type checking logic
- [ ] `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` — maps to C# types
- [ ] `src/Sharpy.Compiler/CodeGen/RoslynEmitter.*.cs` — code generation

### Important: Current Behavior

Currently, `T?` sets `IsNullable = true` and maps to C# nullable. 

After this change:
- `T?` should set `IsOptional = true` and map to `Optional<T>`
- `T | None` should set `IsCSharpNullable = true` and map to C# nullable

**However**, full parser support for `T | None` comes in Phase 4. For now:
- [ ] Change existing `IsNullable = true` to `IsOptional = true` (temporary — maintains current behavior that `T?` works)
- [ ] Leave the actual semantic change (Optional vs nullable) for Phase 5-6

### Verification

- [ ] Build the compiler: `dotnet build src/Sharpy.Compiler`
- [ ] All compiler errors resolved
- [ ] Run existing tests: `dotnet test src/Sharpy.Compiler.Tests`
- [ ] Tests should still pass (behavior unchanged for now)

```
git add -A
git commit -m "refactor: update IsNullable references to IsOptional (temporary)"
```

---

## Task 3.3: Add AST Unit Tests

**File:** `src/Sharpy.Compiler.Tests/Ast/TypeAnnotationTests.cs`

### Steps

- [ ] Create new file `src/Sharpy.Compiler.Tests/Ast/TypeAnnotationTests.cs`
- [ ] Add test class `TypeAnnotationTests`
- [ ] Add tests for the new AST structure:

```csharp
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.Ast;

public class TypeAnnotationTests
{
    [Fact]
    public void TypeAnnotation_Default_NoModifiers()
    {
        var type = new TypeAnnotation { Name = "int" };
        
        Assert.False(type.IsOptional);
        Assert.False(type.IsCSharpNullable);
        Assert.False(type.IsResult);
        Assert.Null(type.ErrorType);
    }
    
    [Fact]
    public void TypeAnnotation_Optional_IsOptionalTrue()
    {
        var type = new TypeAnnotation { Name = "int", IsOptional = true };
        
        Assert.True(type.IsOptional);
        Assert.False(type.IsCSharpNullable);
        Assert.False(type.IsResult);
    }
    
    [Fact]
    public void TypeAnnotation_CSharpNullable_IsCSharpNullableTrue()
    {
        var type = new TypeAnnotation { Name = "str", IsCSharpNullable = true };
        
        Assert.False(type.IsOptional);
        Assert.True(type.IsCSharpNullable);
        Assert.False(type.IsResult);
    }
    
    [Fact]
    public void TypeAnnotation_Result_HasErrorType()
    {
        var errorType = new TypeAnnotation { Name = "ValueError" };
        var type = new TypeAnnotation { Name = "int", ErrorType = errorType };
        
        Assert.False(type.IsOptional);
        Assert.False(type.IsCSharpNullable);
        Assert.True(type.IsResult);
        Assert.NotNull(type.ErrorType);
        Assert.Equal("ValueError", type.ErrorType!.Name);
    }
    
    [Fact]
    public void TypeAnnotation_ResultWithNullable_BothModifiers()
    {
        // int !ValueError | None → Result[int, ValueError] | None
        var errorType = new TypeAnnotation { Name = "ValueError" };
        var type = new TypeAnnotation 
        { 
            Name = "int", 
            ErrorType = errorType,
            IsCSharpNullable = true
        };
        
        Assert.True(type.IsResult);
        Assert.True(type.IsCSharpNullable);
        Assert.False(type.IsOptional);
    }
    
    [Fact]
    public void TypeAnnotation_WithImmutability_RecordCopyWorks()
    {
        var original = new TypeAnnotation { Name = "int" };
        var optional = original with { IsOptional = true };
        
        Assert.False(original.IsOptional);
        Assert.True(optional.IsOptional);
        Assert.Equal("int", optional.Name);
    }
}
```

### Verification

- [ ] Run tests: `dotnet test src/Sharpy.Compiler.Tests --filter TypeAnnotationTests`
- [ ] All tests pass

```
git add src/Sharpy.Compiler.Tests/Ast/TypeAnnotationTests.cs
git commit -m "test: add unit tests for TypeAnnotation modifiers"
```

---

## Final Verification

- [ ] Build entire compiler: `dotnet build src/Sharpy.Compiler`
- [ ] Run all compiler tests: `dotnet test src/Sharpy.Compiler.Tests`
- [ ] All tests pass
- [ ] Review all commits in this phase

```
git log --oneline -3
```

Expected commits:
1. `ast: update TypeAnnotation for T?, T | None, and T !E syntax`
2. `refactor: update IsNullable references to IsOptional (temporary)`
3. `test: add unit tests for TypeAnnotation modifiers`

---

## Notes for Implementer

- This phase only changes the AST structure. The parser still only handles `T?` syntax.
- Phase 4 will add parser support for `T | None` and `T !E`.
- The semantic meaning change (Optional vs nullable) happens in Phases 5-6.
- Keep the changes minimal to avoid introducing bugs. The goal is to prepare the AST without changing behavior.
