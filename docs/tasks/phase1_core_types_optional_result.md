# Phase 1: Core Type Definitions in Sharpy.Core

## Overview

This phase implements the `Optional<T>` and `Result<T, E>` structs in Sharpy.Core. These are the foundational types that `T?` and `T !E` syntax desugar to.

**Priority:** P0 (blocks all other phases)  
**Estimated Complexity:** Low  
**Prerequisites:** None

---

## Background

Per the nullability redesign:
- `T?` is syntactic sugar for `Optional[T]` (safe tagged union)
- `T !E` is syntactic sugar for `Result[T, E]` (in return type annotations)
- Both must be **structs** (no heap allocation)

These are NOT user-defined tagged unions — they are compiler-blessed core primitives with special syntax support.

---

## Task 1.1: Create `Optional<T>` Struct

**File:** `/src/Sharpy.Core/Optional.cs`

### Steps

- [x] Create new file `Optional.cs` in `/src/Sharpy.Core/`
- [x] Add file header comment explaining this is the core Optional type
- [x] Implement `Optional<T>` as a `readonly struct`:
  ```csharp
  namespace Sharpy;
  
  /// <summary>
  /// A safe tagged union for optional values. T? desugars to Optional[T].
  /// This is a struct - no heap allocation for returning optional values.
  /// </summary>
  public readonly struct Optional<T>
  {
      private readonly T _value;
      private readonly bool _hasValue;
  
      private Optional(T value, bool hasValue)
      {
          _value = value;
          _hasValue = hasValue;
      }
  
      // Factory methods
      public static Optional<T> Some(T value) => new(value, true);
      public static Optional<T> Nothing => new(default!, false);
  
      // Properties
      public bool IsSome => _hasValue;
      public bool IsNothing => !_hasValue;
  
      // Methods (match spec in tagged_unions_optional.md)
      public T Unwrap() => 
          _hasValue ? _value : throw new InvalidOperationException("Called Unwrap on Nothing");
      
      public T UnwrapOr(T defaultValue) => 
          _hasValue ? _value : defaultValue;
      
      public T UnwrapOrElse(Func<T> f) => 
          _hasValue ? _value : f();
      
      public Optional<U> Map<U>(Func<T, U> f) => 
          _hasValue ? Optional<U>.Some(f(_value)) : Optional<U>.Nothing;
  
      // For pattern matching support (future)
      public void Deconstruct(out bool hasValue, out T value)
      {
          hasValue = _hasValue;
          value = _value;
      }
  
      public override string ToString() => 
          _hasValue ? $"Some({_value})" : "Nothing";
  
      public override bool Equals(object? obj) =>
          obj is Optional<T> other && Equals(other);
  
      public bool Equals(Optional<T> other) =>
          _hasValue == other._hasValue && 
          (!_hasValue || EqualityComparer<T>.Default.Equals(_value, other._value));
  
      public override int GetHashCode() =>
          _hasValue ? HashCode.Combine(true, _value) : HashCode.Combine(false);
  
      public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);
      public static bool operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);
  }
  ```

- [x] Add static helper class for type inference:
  ```csharp
  /// <summary>
  /// Static factory methods for Optional. Enables Some(value) syntax with type inference.
  /// </summary>
  public static class Optional
  {
      public static Optional<T> Some<T>(T value) => Optional<T>.Some(value);
  }
  ```

- [x] Verify the file compiles: `dotnet build src/Sharpy.Core/`

### Commit
```
git add src/Sharpy.Core/Optional.cs
git commit -m "core: add Optional<T> struct for T? syntax support"
```

---

## Task 1.2: Create `Result<T, E>` Struct

**File:** `/src/Sharpy.Core/Result.cs`

### Steps

- [x] Create new file `Result.cs` in `/src/Sharpy.Core/`
- [x] Add file header comment explaining this is the core Result type
- [x] Implement `Result<T, E>` as a `readonly struct`:
  ```csharp
  namespace Sharpy;
  
  /// <summary>
  /// A safe tagged union for error handling. T !E desugars to Result[T, E].
  /// This is a struct - no heap allocation for returning result values.
  /// </summary>
  public readonly struct Result<T, E>
  {
      private readonly T _value;
      private readonly E _error;
      private readonly bool _isOk;
  
      private Result(T value, E error, bool isOk)
      {
          _value = value;
          _error = error;
          _isOk = isOk;
      }
  
      // Factory methods
      public static Result<T, E> Ok(T value) => new(value, default!, true);
      public static Result<T, E> Err(E error) => new(default!, error, false);
  
      // Properties
      public bool IsOk => _isOk;
      public bool IsErr => !_isOk;
  
      // Methods (match spec in tagged_unions_result.md)
      public T Unwrap() =>
          _isOk ? _value : throw new InvalidOperationException($"Called Unwrap on Err: {_error}");
      
      public T UnwrapOr(T defaultValue) =>
          _isOk ? _value : defaultValue;
      
      public T UnwrapOrElse(Func<E, T> f) =>
          _isOk ? _value : f(_error);
      
      public E UnwrapErr() =>
          _isOk ? throw new InvalidOperationException($"Called UnwrapErr on Ok: {_value}") : _error;
      
      public Result<U, E> Map<U>(Func<T, U> f) =>
          _isOk ? Result<U, E>.Ok(f(_value)) : Result<U, E>.Err(_error);
      
      public Result<T, F> MapErr<F>(Func<E, F> f) =>
          _isOk ? Result<T, F>.Ok(_value) : Result<T, F>.Err(f(_error));
  
      // For pattern matching support (future)
      public void Deconstruct(out bool isOk, out T value, out E error)
      {
          isOk = _isOk;
          value = _value;
          error = _error;
      }
  
      public override string ToString() =>
          _isOk ? $"Ok({_value})" : $"Err({_error})";
  
      public override bool Equals(object? obj) =>
          obj is Result<T, E> other && Equals(other);
  
      public bool Equals(Result<T, E> other) =>
          _isOk == other._isOk &&
          (_isOk 
              ? EqualityComparer<T>.Default.Equals(_value, other._value)
              : EqualityComparer<E>.Default.Equals(_error, other._error));
  
      public override int GetHashCode() =>
          _isOk ? HashCode.Combine(true, _value) : HashCode.Combine(false, _error);
  
      public static bool operator ==(Result<T, E> left, Result<T, E> right) => left.Equals(right);
      public static bool operator !=(Result<T, E> left, Result<T, E> right) => !left.Equals(right);
  }
  ```

- [x] Add static helper class for type inference:
  ```csharp
  /// <summary>
  /// Static factory methods for Result. Enables Ok(value)/Err(error) syntax with type inference.
  /// </summary>
  public static class Result
  {
      public static Result<T, E> Ok<T, E>(T value) => Result<T, E>.Ok(value);
      public static Result<T, E> Err<T, E>(E error) => Result<T, E>.Err(error);
  }
  ```

- [x] Verify the file compiles: `dotnet build src/Sharpy.Core/`

### Commit
```
git add src/Sharpy.Core/Result.cs
git commit -m "core: add Result<T, E> struct for T !E syntax support"
```

---

## Task 1.3: Add Unit Tests for Optional

**File:** `/src/Sharpy.Core.Tests/OptionalTests.cs`

### Steps

- [x] Create new test file `OptionalTests.cs`
- [x] Add tests for `Some` construction and properties:
  ```csharp
  [Fact]
  public void Some_CreatesOptionalWithValue()
  {
      var opt = Optional<int>.Some(42);
      Assert.True(opt.IsSome);
      Assert.False(opt.IsNothing);
      Assert.Equal(42, opt.Unwrap());
  }
  ```

- [x] Add tests for `Nothing` construction and properties:
  ```csharp
  [Fact]
  public void Nothing_CreatesEmptyOptional()
  {
      var opt = Optional<int>.Nothing;
      Assert.False(opt.IsSome);
      Assert.True(opt.IsNothing);
  }
  ```

- [x] Add tests for `Unwrap` throwing on Nothing:
  ```csharp
  [Fact]
  public void Unwrap_ThrowsOnNothing()
  {
      var opt = Optional<int>.Nothing;
      Assert.Throws<InvalidOperationException>(() => opt.Unwrap());
  }
  ```

- [x] Add tests for `UnwrapOr`:
  ```csharp
  [Fact]
  public void UnwrapOr_ReturnValueWhenSome()
  {
      var opt = Optional<int>.Some(42);
      Assert.Equal(42, opt.UnwrapOr(0));
  }
  
  [Fact]
  public void UnwrapOr_ReturnDefaultWhenNothing()
  {
      var opt = Optional<int>.Nothing;
      Assert.Equal(0, opt.UnwrapOr(0));
  }
  ```

- [x] Add tests for `UnwrapOrElse`:
  ```csharp
  [Fact]
  public void UnwrapOrElse_DoesNotCallFuncWhenSome()
  {
      var called = false;
      var opt = Optional<int>.Some(42);
      var result = opt.UnwrapOrElse(() => { called = true; return 0; });
      Assert.Equal(42, result);
      Assert.False(called);
  }
  
  [Fact]
  public void UnwrapOrElse_CallsFuncWhenNothing()
  {
      var opt = Optional<int>.Nothing;
      var result = opt.UnwrapOrElse(() => 99);
      Assert.Equal(99, result);
  }
  ```

- [x] Add tests for `Map`:
  ```csharp
  [Fact]
  public void Map_TransformsValueWhenSome()
  {
      var opt = Optional<int>.Some(42);
      var mapped = opt.Map(x => x.ToString());
      Assert.True(mapped.IsSome);
      Assert.Equal("42", mapped.Unwrap());
  }
  
  [Fact]
  public void Map_ReturnsNothingWhenNothing()
  {
      var opt = Optional<int>.Nothing;
      var mapped = opt.Map(x => x.ToString());
      Assert.True(mapped.IsNothing);
  }
  ```

- [x] Add tests for equality:
  ```csharp
  [Fact]
  public void Equality_SomeValuesEqual()
  {
      var a = Optional<int>.Some(42);
      var b = Optional<int>.Some(42);
      Assert.Equal(a, b);
      Assert.True(a == b);
  }
  
  [Fact]
  public void Equality_NothingsEqual()
  {
      var a = Optional<int>.Nothing;
      var b = Optional<int>.Nothing;
      Assert.Equal(a, b);
  }
  
  [Fact]
  public void Equality_SomeAndNothingNotEqual()
  {
      var some = Optional<int>.Some(42);
      var nothing = Optional<int>.Nothing;
      Assert.NotEqual(some, nothing);
  }
  ```

- [x] Add tests for static helper `Optional.Some`:
  ```csharp
  [Fact]
  public void StaticSome_InfersTypeCorrectly()
  {
      var opt = Optional.Some(42);  // Should infer Optional<int>
      Assert.True(opt.IsSome);
      Assert.Equal(42, opt.Unwrap());
  }
  ```

- [x] Run tests: `dotnet test src/Sharpy.Core.Tests/ --filter OptionalTests`

### Commit
```
git add src/Sharpy.Core.Tests/OptionalTests.cs
git commit -m "test: add unit tests for Optional<T>"
```

---

## Task 1.4: Add Unit Tests for Result

**File:** `/src/Sharpy.Core.Tests/ResultTests.cs`

### Steps

- [x] Create new test file `ResultTests.cs`
- [x] Add tests for `Ok` construction and properties:
  ```csharp
  [Fact]
  public void Ok_CreatesResultWithValue()
  {
      var result = Result<int, string>.Ok(42);
      Assert.True(result.IsOk);
      Assert.False(result.IsErr);
      Assert.Equal(42, result.Unwrap());
  }
  ```

- [x] Add tests for `Err` construction and properties:
  ```csharp
  [Fact]
  public void Err_CreatesResultWithError()
  {
      var result = Result<int, string>.Err("failed");
      Assert.False(result.IsOk);
      Assert.True(result.IsErr);
      Assert.Equal("failed", result.UnwrapErr());
  }
  ```

- [x] Add tests for `Unwrap` throwing on Err:
  ```csharp
  [Fact]
  public void Unwrap_ThrowsOnErr()
  {
      var result = Result<int, string>.Err("failed");
      var ex = Assert.Throws<InvalidOperationException>(() => result.Unwrap());
      Assert.Contains("failed", ex.Message);
  }
  ```

- [x] Add tests for `UnwrapErr` throwing on Ok:
  ```csharp
  [Fact]
  public void UnwrapErr_ThrowsOnOk()
  {
      var result = Result<int, string>.Ok(42);
      Assert.Throws<InvalidOperationException>(() => result.UnwrapErr());
  }
  ```

- [x] Add tests for `UnwrapOr`:
  ```csharp
  [Fact]
  public void UnwrapOr_ReturnsValueWhenOk()
  {
      var result = Result<int, string>.Ok(42);
      Assert.Equal(42, result.UnwrapOr(0));
  }
  
  [Fact]
  public void UnwrapOr_ReturnsDefaultWhenErr()
  {
      var result = Result<int, string>.Err("failed");
      Assert.Equal(0, result.UnwrapOr(0));
  }
  ```

- [x] Add tests for `UnwrapOrElse`:
  ```csharp
  [Fact]
  public void UnwrapOrElse_DoesNotCallFuncWhenOk()
  {
      var called = false;
      var result = Result<int, string>.Ok(42);
      var value = result.UnwrapOrElse(e => { called = true; return 0; });
      Assert.Equal(42, value);
      Assert.False(called);
  }
  
  [Fact]
  public void UnwrapOrElse_CallsFuncWithErrorWhenErr()
  {
      var result = Result<int, string>.Err("failed");
      var value = result.UnwrapOrElse(e => e.Length);
      Assert.Equal(6, value);  // "failed".Length
  }
  ```

- [x] Add tests for `Map`:
  ```csharp
  [Fact]
  public void Map_TransformsValueWhenOk()
  {
      var result = Result<int, string>.Ok(42);
      var mapped = result.Map(x => x.ToString());
      Assert.True(mapped.IsOk);
      Assert.Equal("42", mapped.Unwrap());
  }
  
  [Fact]
  public void Map_PreservesErrorWhenErr()
  {
      var result = Result<int, string>.Err("failed");
      var mapped = result.Map(x => x.ToString());
      Assert.True(mapped.IsErr);
      Assert.Equal("failed", mapped.UnwrapErr());
  }
  ```

- [x] Add tests for `MapErr`:
  ```csharp
  [Fact]
  public void MapErr_PreservesValueWhenOk()
  {
      var result = Result<int, string>.Ok(42);
      var mapped = result.MapErr(e => e.Length);
      Assert.True(mapped.IsOk);
      Assert.Equal(42, mapped.Unwrap());
  }
  
  [Fact]
  public void MapErr_TransformsErrorWhenErr()
  {
      var result = Result<int, string>.Err("failed");
      var mapped = result.MapErr(e => e.Length);
      Assert.True(mapped.IsErr);
      Assert.Equal(6, mapped.UnwrapErr());
  }
  ```

- [x] Add tests for equality:
  ```csharp
  [Fact]
  public void Equality_OkValuesEqual()
  {
      var a = Result<int, string>.Ok(42);
      var b = Result<int, string>.Ok(42);
      Assert.Equal(a, b);
  }
  
  [Fact]
  public void Equality_ErrValuesEqual()
  {
      var a = Result<int, string>.Err("failed");
      var b = Result<int, string>.Err("failed");
      Assert.Equal(a, b);
  }
  
  [Fact]
  public void Equality_OkAndErrNotEqual()
  {
      var ok = Result<int, string>.Ok(42);
      var err = Result<int, string>.Err("failed");
      Assert.NotEqual(ok, err);
  }
  ```

- [x] Run tests: `dotnet test src/Sharpy.Core.Tests/ --filter ResultTests`

### Commit
```
git add src/Sharpy.Core.Tests/ResultTests.cs
git commit -m "test: add unit tests for Result<T, E>"
```

---

## Task 1.5: Update Documentation

### Steps

- [x] Update `/docs/language_specification/tagged_unions_optional.md`:
  - Add "Implementation" section at the bottom:
    ```markdown
    ## Implementation Details
    
    `Optional[T]` is implemented as a C# `readonly struct` in `Sharpy.Core`:
    
    ```csharp
    public readonly struct Optional<T>
    {
        // Two fields: the value and a hasValue flag
        // Zero heap allocation
    }
    ```
    
    The static helpers `Some(value)` and `Nothing` are available at module scope
    for convenient construction.
    ```

- [x] Update `/docs/language_specification/tagged_unions_result.md`:
  - Add "Implementation Details" section at the bottom:
    ```markdown
    ## Implementation Details
    
    `Result[T, E]` is implemented as a C# `readonly struct` in `Sharpy.Core`:
    
    ```csharp
    public readonly struct Result<T, E>
    {
        // Three fields: the value, the error, and an isOk flag
        // Zero heap allocation
    }
    ```
    
    The static helpers `Ok(value)` and `Err(error)` are available at module scope
    for convenient construction.
    ```

- [x] Update `/docs/language_specification/tagged_unions.md`:
  - Add clarifying note in "Standard Library Types" section:
    ```markdown
    > **Note:** `Optional[T]` and `Result[T, E]` are **core primitives** implemented as
    > structs for zero-allocation performance. They are distinct from user-defined
    > tagged unions (declared with `union`), which use class-based representation
    > to support recursive types and more than two cases.
    ```

### Commit
```
git add docs/language_specification/tagged_unions_optional.md
git add docs/language_specification/tagged_unions_result.md
git add docs/language_specification/tagged_unions.md
git commit -m "docs: add implementation details for Optional and Result"
```

---

## Verification Checklist

Before marking Phase 1 complete:

- [x] `dotnet build src/Sharpy.Core/` succeeds
- [x] `dotnet test src/Sharpy.Core.Tests/ --filter OptionalTests` - all pass
- [x] `dotnet test src/Sharpy.Core.Tests/ --filter ResultTests` - all pass
- [x] Documentation updated with implementation details
- [x] All commits pushed

---

## Notes for Implementer

1. **Struct semantics matter**: These are value types. Assignment copies the entire struct. This is intentional for performance.

2. **`default!` usage**: We use `default!` for the "unused" field in each case (e.g., `_error` when `_isOk` is true). The null-forgiving operator suppresses warnings since we never access that field in that state.

3. **No inheritance**: Structs can't inherit or be inherited. This is fine — Optional/Result don't need inheritance.

4. **Future pattern matching**: The `Deconstruct` methods are placeholders for when we implement pattern matching. They're not used yet but having them won't hurt.
