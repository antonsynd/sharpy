# Phase 5: Semantic Type System Updates

## Overview

This phase adds proper `OptionalType` and `ResultType` to the semantic type system, distinguishing them from `NullableType` (C# nullable interop).

**Prerequisites:** 
- Phase 1 (Core types in Sharpy.Core)
- Phase 3 (AST changes)

**Files to modify:**
- `src/Sharpy.Compiler/Semantic/SemanticType.cs`
- `src/Sharpy.Compiler/Semantic/ITypeInfo.cs` (if needed)

**Files to create:**
- `src/Sharpy.Compiler.Tests/Semantic/OptionalTypeTests.cs`
- `src/Sharpy.Compiler.Tests/Semantic/ResultTypeTests.cs`

---

## Task 5.1: Add OptionalType Semantic Type

**File:** `src/Sharpy.Compiler/Semantic/SemanticType.cs`

### Steps

- [ ] Open `src/Sharpy.Compiler/Semantic/SemanticType.cs`
- [ ] Find the `NullableType` record (should be around line 300-330)
- [ ] Add `OptionalType` **before** `NullableType`:
  ```csharp
  /// <summary>
  /// Optional type (T? → Optional[T]).
  /// This is a SAFE tagged union, distinct from NullableType (C# nullable interop).
  /// 
  /// <para><b>Semantic Meaning:</b></para>
  /// <list type="bullet">
  /// <item><description>Represents Sharpy's native optional value</description></item>
  /// <item><description>Maps to Sharpy.Optional&lt;T&gt; struct</description></item>
  /// <item><description>Zero heap allocation</description></item>
  /// <item><description>Uses Some(value) / Nothing cases</description></item>
  /// </list>
  /// </summary>
  public record OptionalType : SemanticType
  {
      /// <summary>
      /// The underlying type T in Optional[T].
      /// </summary>
      public SemanticType UnderlyingType { get; init; } = SemanticType.Unknown;

      public override string GetDisplayName() => $"{UnderlyingType.GetDisplayName()}?";

      /// <summary>
      /// Optional types can hold "nothing" which is conceptually similar to null.
      /// </summary>
      public override bool IsNullable => true;

      public override bool IsValueType => true; // Optional<T> is a struct

      public override bool IsAssignableTo(SemanticType other)
      {
          // OptionalType is assignable to same OptionalType
          if (other is OptionalType otherOpt)
              return UnderlyingType.IsAssignableTo(otherOpt.UnderlyingType);
          
          // OptionalType is NOT assignable to NullableType or raw type
          // (explicit conversion needed)
          
          return base.IsAssignableTo(other);
      }
      
      public override ITypeInfo MakeNullable()
      {
          // Optional<T> | None → NullableType wrapping OptionalType
          return new NullableType { UnderlyingType = this };
      }
      
      public override ITypeInfo UnwrapNullable()
      {
          // OptionalType is not a nullable type in the C# sense
          return this;
      }
  }
  ```

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/Semantic/SemanticType.cs
git commit -m "semantic: add OptionalType for T? syntax"
```

---

## Task 5.2: Add ResultType Semantic Type

**File:** `src/Sharpy.Compiler/Semantic/SemanticType.cs`

### Steps

- [ ] In the same file, add `ResultType` after `OptionalType`:
  ```csharp
  /// <summary>
  /// Result type (T !E → Result[T, E]).
  /// This is a SAFE tagged union for error handling.
  /// 
  /// <para><b>Semantic Meaning:</b></para>
  /// <list type="bullet">
  /// <item><description>Represents Sharpy's native result/error type</description></item>
  /// <item><description>Maps to Sharpy.Result&lt;T, E&gt; struct</description></item>
  /// <item><description>Zero heap allocation</description></item>
  /// <item><description>Uses Ok(value) / Err(error) cases</description></item>
  /// </list>
  /// </summary>
  public record ResultType : SemanticType
  {
      /// <summary>
      /// The success type T in Result[T, E].
      /// </summary>
      public SemanticType OkType { get; init; } = SemanticType.Unknown;
      
      /// <summary>
      /// The error type E in Result[T, E].
      /// </summary>
      public SemanticType ErrorType { get; init; } = SemanticType.Unknown;

      public override string GetDisplayName() => $"{OkType.GetDisplayName()} !{ErrorType.GetDisplayName()}";

      public override bool IsValueType => true; // Result<T, E> is a struct

      public override bool IsAssignableTo(SemanticType other)
      {
          // ResultType is assignable to same ResultType with compatible types
          if (other is ResultType otherResult)
              return OkType.IsAssignableTo(otherResult.OkType) 
                  && ErrorType.IsAssignableTo(otherResult.ErrorType);
          
          return base.IsAssignableTo(other);
      }
      
      public override ITypeInfo MakeNullable()
      {
          // Result<T, E> | None → NullableType wrapping ResultType
          return new NullableType { UnderlyingType = this };
      }
  }
  ```

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/Semantic/SemanticType.cs
git commit -m "semantic: add ResultType for T !E syntax"
```

---

## Task 5.3: Update NullableType Documentation

**File:** `src/Sharpy.Compiler/Semantic/SemanticType.cs`

### Steps

- [ ] Find the existing `NullableType` record
- [ ] Update its documentation to clarify it's for C# interop:
  ```csharp
  /// <summary>
  /// C# nullable type (T | None).
  /// This represents .NET nullable reference types or Nullable&lt;T&gt; for value types.
  /// 
  /// <para><b>Semantic Meaning:</b></para>
  /// <list type="bullet">
  /// <item><description>Used for .NET interop when APIs return/accept null</description></item>
  /// <item><description>Maps to C# T? (nullable reference) or Nullable&lt;T&gt;</description></item>
  /// <item><description>NOT the same as OptionalType (T? Sharpy syntax)</description></item>
  /// </list>
  /// 
  /// <para><b>Distinction from OptionalType:</b></para>
  /// <list type="bullet">
  /// <item><description>NullableType: C# null semantics, for .NET interop</description></item>
  /// <item><description>OptionalType: Safe tagged union, for Sharpy-native code</description></item>
  /// </list>
  /// </summary>
  public record NullableType : SemanticType
  {
      // ... existing implementation ...
  }
  ```

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/Semantic/SemanticType.cs
git commit -m "semantic: clarify NullableType is for C# interop"
```

---

## Task 5.4: Add Unit Tests for OptionalType

**File:** `src/Sharpy.Compiler.Tests/Semantic/OptionalTypeTests.cs`

### Steps

- [ ] Create new file `src/Sharpy.Compiler.Tests/Semantic/OptionalTypeTests.cs`
- [ ] Add test class:

```csharp
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class OptionalTypeTests
{
    [Fact]
    public void OptionalType_DisplayName_ShowsQuestionMark()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.Equal("int?", opt.GetDisplayName());
    }
    
    [Fact]
    public void OptionalType_IsNullable_ReturnsTrue()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Str };
        Assert.True(opt.IsNullable);
    }
    
    [Fact]
    public void OptionalType_IsValueType_ReturnsTrue()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.True(opt.IsValueType);
    }
    
    [Fact]
    public void OptionalType_AssignableToSameOptional()
    {
        var opt1 = new OptionalType { UnderlyingType = SemanticType.Int };
        var opt2 = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.True(opt1.IsAssignableTo(opt2));
    }
    
    [Fact]
    public void OptionalType_NotAssignableToDifferentOptional()
    {
        var optInt = new OptionalType { UnderlyingType = SemanticType.Int };
        var optStr = new OptionalType { UnderlyingType = SemanticType.Str };
        Assert.False(optInt.IsAssignableTo(optStr));
    }
    
    [Fact]
    public void OptionalType_NotAssignableToNullableType()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        var nullable = new NullableType { UnderlyingType = SemanticType.Int };
        Assert.False(opt.IsAssignableTo(nullable));
    }
    
    [Fact]
    public void OptionalType_NotAssignableToRawType()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.False(opt.IsAssignableTo(SemanticType.Int));
    }
    
    [Fact]
    public void OptionalType_MakeNullable_WrapsInNullableType()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        var nullable = opt.MakeNullable();
        
        Assert.IsType<NullableType>(nullable);
        var nt = (NullableType)nullable;
        Assert.IsType<OptionalType>(nt.UnderlyingType);
    }
    
    [Fact]
    public void OptionalType_UnwrapNullable_ReturnsSelf()
    {
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        var unwrapped = opt.UnwrapNullable();
        Assert.Same(opt, unwrapped);
    }
    
    [Fact]
    public void OptionalType_NestedOptional_DisplaysCorrectly()
    {
        var inner = new OptionalType { UnderlyingType = SemanticType.Int };
        var outer = new OptionalType { UnderlyingType = inner };
        Assert.Equal("int??", outer.GetDisplayName());
    }
}
```

### Verification

- [ ] Run tests: `dotnet test src/Sharpy.Compiler.Tests --filter OptionalTypeTests`
- [ ] All tests pass

```
git add src/Sharpy.Compiler.Tests/Semantic/OptionalTypeTests.cs
git commit -m "test: add unit tests for OptionalType"
```

---

## Task 5.5: Add Unit Tests for ResultType

**File:** `src/Sharpy.Compiler.Tests/Semantic/ResultTypeTests.cs`

### Steps

- [ ] Create new file `src/Sharpy.Compiler.Tests/Semantic/ResultTypeTests.cs`
- [ ] Add test class:

> **Note:** The test uses `UserDefinedType` to represent error types. If `UserDefinedType` doesn't exist in the codebase, create a simple mock type or use an existing type that has a `Name` property. Alternatively, create error types as `ClassType` or whatever represents user-defined classes in the semantic type system.

```csharp
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class ResultTypeTests
{
    // Create a simple error type for testing
    // Note: Adjust to match actual semantic type system (e.g., ClassType, UserDefinedType, etc.)
    private static readonly SemanticType ValueError = new UserDefinedType { Name = "ValueError" };
    private static readonly SemanticType IOError = new UserDefinedType { Name = "IOError" };
    
    [Fact]
    public void ResultType_DisplayName_ShowsBangSyntax()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        Assert.Equal("int !ValueError", result.GetDisplayName());
    }
    
    [Fact]
    public void ResultType_IsValueType_ReturnsTrue()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        Assert.True(result.IsValueType);
    }
    
    [Fact]
    public void ResultType_AssignableToSameResult()
    {
        var r1 = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var r2 = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        Assert.True(r1.IsAssignableTo(r2));
    }
    
    [Fact]
    public void ResultType_NotAssignableToDifferentOkType()
    {
        var r1 = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var r2 = new ResultType { OkType = SemanticType.Str, ErrorType = ValueError };
        Assert.False(r1.IsAssignableTo(r2));
    }
    
    [Fact]
    public void ResultType_NotAssignableToDifferentErrorType()
    {
        var r1 = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var r2 = new ResultType { OkType = SemanticType.Int, ErrorType = IOError };
        Assert.False(r1.IsAssignableTo(r2));
    }
    
    [Fact]
    public void ResultType_NotAssignableToOptional()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var opt = new OptionalType { UnderlyingType = SemanticType.Int };
        Assert.False(result.IsAssignableTo(opt));
    }
    
    [Fact]
    public void ResultType_NotAssignableToRawType()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        Assert.False(result.IsAssignableTo(SemanticType.Int));
    }
    
    [Fact]
    public void ResultType_MakeNullable_WrapsInNullableType()
    {
        var result = new ResultType { OkType = SemanticType.Int, ErrorType = ValueError };
        var nullable = result.MakeNullable();
        
        Assert.IsType<NullableType>(nullable);
        var nt = (NullableType)nullable;
        Assert.IsType<ResultType>(nt.UnderlyingType);
    }
    
    [Fact]
    public void ResultType_ComplexTypes_DisplaysCorrectly()
    {
        // Note: Adjust GenericType usage to match actual semantic type system
        // This might be ListType, GenericType, or ConstructedGenericType depending on implementation
        var okType = new GenericType 
        { 
            Name = "list", 
            TypeArguments = new List<SemanticType> { SemanticType.Int } 
        };
        var result = new ResultType { OkType = okType, ErrorType = ValueError };
        Assert.Equal("list[int] !ValueError", result.GetDisplayName());
    }
}
```

### Verification

- [ ] Run tests: `dotnet test src/Sharpy.Compiler.Tests --filter ResultTypeTests`
- [ ] All tests pass

```
git add src/Sharpy.Compiler.Tests/Semantic/ResultTypeTests.cs
git commit -m "test: add unit tests for ResultType"
```

---

## Task 5.6: Verify Existing Semantic Tests Still Pass

### Steps

- [ ] Run all semantic tests: `dotnet test src/Sharpy.Compiler.Tests --filter "Semantic"`
- [ ] Investigate any failures
- [ ] Fix any regressions

### Verification

- [ ] All semantic tests pass

```
# If fixes were needed:
git add -A
git commit -m "fix: resolve semantic test regressions"
```

---

## Final Verification

- [ ] Build entire compiler: `dotnet build src/Sharpy.Compiler`
- [ ] Run all semantic tests: `dotnet test src/Sharpy.Compiler.Tests --filter "Semantic"`
- [ ] All tests pass
- [ ] Review all commits in this phase

```
git log --oneline -5
```

Expected commits:
1. `semantic: add OptionalType for T? syntax`
2. `semantic: add ResultType for T !E syntax`
3. `semantic: clarify NullableType is for C# interop`
4. `test: add unit tests for OptionalType`
5. `test: add unit tests for ResultType`

---

## Notes for Implementer

- **OptionalType vs NullableType:** These are fundamentally different:
  - `OptionalType` → `Sharpy.Optional<T>` struct, safe tagged union
  - `NullableType` → C# `T?` or `Nullable<T>`, for .NET interop

- **Assignability rules are strict:** An `OptionalType` is NOT implicitly convertible to a `NullableType` or vice versa. Explicit conversion (via `maybe` expression) is required.

- **Value type semantics:** Both `OptionalType` and `ResultType` are value types (structs), which affects copy semantics and boxing behavior.
