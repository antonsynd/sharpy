# Phase 7: Constructor Recognition for Some/Nothing/Ok/Err

## Overview

This phase adds support for recognizing `Some(v)`, `Nothing`, `Ok(v)`, and `Err(e)` as tagged union constructors in contexts where the type can be inferred.

**Prerequisites:** 
- Phase 1 (Core types in Sharpy.Core)
- Phase 5-6 (Semantic types and resolution)

> ⚠️ **CRITICAL ARCHITECTURE CHECK:** This phase requires **expected type propagation** in the type checker. The type checker must pass an "expected type" down when checking expressions so that `Some(42)` can be recognized as `Optional<int>.Some(42)` when the expected type is `int?`.
>
> **Before starting this phase:**
> 1. Verify the type checker supports expected type propagation
> 2. If not, this phase will require significant architectural changes to `TypeChecker.Expressions.cs`
> 3. Check existing code for patterns like `CheckExpression(expr, expectedType)` — if the second parameter doesn't exist, you'll need to add it

**Files to modify:**
- `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`
- `src/Sharpy.Compiler/Semantic/NameResolver.cs`
- `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` (if exists)

**Files to create:**
- `src/Sharpy.Compiler.Tests/Semantic/ConstructorInferenceTests.cs`

---

## Task 7.1: Understand the Inference Contexts

Before implementing, understand where constructor inference should work:

### Inference Contexts (type is known)

```python
# Variable declaration with type annotation
x: int? = Some(42)           # ✅ infer Some → Optional<int>.Some
y: int? = Nothing            # ✅ infer Nothing → Optional<int>.Nothing

# Function return with return type
def get_value() -> int?:
    return Some(42)          # ✅ infer from return type
    return Nothing           # ✅ infer from return type

# Function argument with parameter type
def process(opt: int?) -> None:
    pass
process(Some(42))            # ✅ infer from parameter type

# Default parameter value
def foo(x: int? = Nothing):  # ✅ infer from parameter type
    pass
```

### Non-Inference Contexts (type unknown)

```python
# Variable without type annotation
x = Some(42)                 # ❌ Cannot infer - what is T?

# Ambiguous context
result = Some(42) if cond else Nothing  # ❌ Need type annotation
```

### Inference Strategy

The type checker needs an "expected type" to flow down during expression checking:
- When checking `Some(42)` with expected type `int?`, recognize this as `Optional<int>.Some(42)`
- When checking `Nothing` with expected type `int?`, recognize this as `Optional<int>.Nothing`

---

## Task 7.2: Add Constructor Recognition in TypeChecker

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`

### Steps

- [ ] Open `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`
- [ ] Find the method that checks function calls (e.g., `CheckFunctionCall` or `VisitFunctionCall`)
- [ ] Add logic to recognize Optional/Result constructors:

```csharp
private SemanticType CheckFunctionCall(FunctionCall call, SemanticType? expectedType)
{
    // Check if this is a tagged union constructor shorthand
    if (call.Function is Identifier id)
    {
        // Some(value) → Optional<T>.Some(value)
        if (id.Name == "Some" && call.Arguments.Length == 1)
        {
            if (expectedType is OptionalType opt)
            {
                // Check that the argument type is compatible with T
                var argType = CheckExpression(call.Arguments[0], opt.UnderlyingType);
                if (!argType.IsAssignableTo(opt.UnderlyingType))
                {
                    ReportError($"Argument type '{argType.GetDisplayName()}' is not compatible with Optional underlying type '{opt.UnderlyingType.GetDisplayName()}'");
                }
                return expectedType;
            }
            else if (expectedType == null)
            {
                ReportError("Cannot infer type for 'Some()' without a type annotation. Use 'Optional[T].Some(value)' or add a type annotation.");
                return SemanticType.Unknown;
            }
        }
        
        // Ok(value) → Result<T, E>.Ok(value)
        if (id.Name == "Ok" && call.Arguments.Length == 1)
        {
            if (expectedType is ResultType result)
            {
                var argType = CheckExpression(call.Arguments[0], result.OkType);
                if (!argType.IsAssignableTo(result.OkType))
                {
                    ReportError($"Argument type '{argType.GetDisplayName()}' is not compatible with Result Ok type '{result.OkType.GetDisplayName()}'");
                }
                return expectedType;
            }
            else if (expectedType == null)
            {
                ReportError("Cannot infer type for 'Ok()' without a type annotation. Use 'Result[T, E].Ok(value)' or add a type annotation.");
                return SemanticType.Unknown;
            }
        }
        
        // Err(error) → Result<T, E>.Err(error)
        if (id.Name == "Err" && call.Arguments.Length == 1)
        {
            if (expectedType is ResultType result)
            {
                var argType = CheckExpression(call.Arguments[0], result.ErrorType);
                if (!argType.IsAssignableTo(result.ErrorType))
                {
                    ReportError($"Argument type '{argType.GetDisplayName()}' is not compatible with Result Error type '{result.ErrorType.GetDisplayName()}'");
                }
                return expectedType;
            }
            else if (expectedType == null)
            {
                ReportError("Cannot infer type for 'Err()' without a type annotation. Use 'Result[T, E].Err(error)' or add a type annotation.");
                return SemanticType.Unknown;
            }
        }
    }
    
    // ... existing function call logic ...
}
```

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs
git commit -m "semantic: add constructor recognition for Some/Ok/Err"
```

---

## Task 7.3: Add Nothing Recognition for Identifiers

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`

### Steps

`Nothing` is not a function call — it's just an identifier. Handle it separately:

- [ ] Find the method that checks identifiers (e.g., `CheckIdentifier` or `VisitIdentifier`)
- [ ] Add logic to recognize `Nothing`:

```csharp
private SemanticType CheckIdentifier(Identifier id, SemanticType? expectedType)
{
    // Nothing → Optional<T>.Nothing
    if (id.Name == "Nothing")
    {
        if (expectedType is OptionalType)
        {
            return expectedType;
        }
        else if (expectedType == null)
        {
            ReportError("Cannot infer type for 'Nothing' without a type annotation. Use a type annotation like 'x: int? = Nothing'.");
            return SemanticType.Unknown;
        }
        else
        {
            ReportError($"'Nothing' can only be assigned to Optional types, not '{expectedType.GetDisplayName()}'");
            return SemanticType.Unknown;
        }
    }
    
    // ... existing identifier resolution logic ...
}
```

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs
git commit -m "semantic: add Nothing identifier recognition for Optional"
```

---

## Task 7.4: Register Built-in Names

**File:** `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` or `NameResolver.cs`

### Steps

The names `Some`, `Nothing`, `Ok`, `Err` need to be recognized by the name resolver as special built-ins that the type checker handles.

- [ ] Find where built-in names are registered
- [ ] Add these names as "special" built-ins (not regular functions):

```csharp
// In BuiltinRegistry or similar
private static readonly HashSet<string> TaggedUnionConstructors = new()
{
    "Some",
    "Nothing", 
    "Ok",
    "Err"
};

public bool IsTaggedUnionConstructor(string name) 
    => TaggedUnionConstructors.Contains(name);
```

- [ ] Update NameResolver to not error on these names when they're not found in scope:

```csharp
// In NameResolver, when name is not found
if (builtinRegistry.IsTaggedUnionConstructor(name))
{
    // Don't error - type checker will handle this based on expected type
    return null; // or a special "deferred" symbol
}
```

### Alternative Approach

If the architecture doesn't easily support this, consider:
1. Adding `Some`, `Nothing`, `Ok`, `Err` as actual symbols in the global scope
2. Having the type checker specialize their behavior based on expected type

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs src/Sharpy.Compiler/Semantic/NameResolver.cs
git commit -m "semantic: register tagged union constructor names as built-ins"
```

---

## Task 7.5: Add Unit Tests for Constructor Inference

**File:** `src/Sharpy.Compiler.Tests/Semantic/ConstructorInferenceTests.cs`

### Steps

- [ ] Create new file `src/Sharpy.Compiler.Tests/Semantic/ConstructorInferenceTests.cs`
- [ ] Add comprehensive tests:

```csharp
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class ConstructorInferenceTests : IntegrationTestBase
{
    #region Some Inference
    
    [Fact]
    public void Some_WithTypedVariable_InfersCorrectly()
    {
        var code = @"
x: int? = Some(42)
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Some_InReturn_InfersFromReturnType()
    {
        var code = @"
def get_value() -> int?:
    return Some(42)
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Some_AsArgument_InfersFromParameterType()
    {
        var code = @"
def process(opt: int?) -> None:
    pass

process(Some(42))
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Some_WithoutTypeContext_ReportsError()
    {
        var code = @"
x = Some(42)
";
        var result = Compile(code);
        Assert.False(result.Success);
        Assert.Contains("Cannot infer type", GetErrors(result));
    }
    
    [Fact]
    public void Some_TypeMismatch_ReportsError()
    {
        var code = @"
x: str? = Some(42)
";
        var result = Compile(code);
        Assert.False(result.Success);
    }
    
    #endregion
    
    #region Nothing Inference
    
    [Fact]
    public void Nothing_WithTypedVariable_InfersCorrectly()
    {
        var code = @"
x: int? = Nothing
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Nothing_InReturn_InfersFromReturnType()
    {
        var code = @"
def get_nothing() -> int?:
    return Nothing
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Nothing_WithoutTypeContext_ReportsError()
    {
        var code = @"
x = Nothing
";
        var result = Compile(code);
        Assert.False(result.Success);
        Assert.Contains("Cannot infer type", GetErrors(result));
    }
    
    [Fact]
    public void Nothing_AssignedToNonOptional_ReportsError()
    {
        var code = @"
x: int = Nothing
";
        var result = Compile(code);
        Assert.False(result.Success);
    }
    
    #endregion
    
    #region Ok Inference
    
    [Fact]
    public void Ok_WithTypedVariable_InfersCorrectly()
    {
        var code = @"
x: int !str = Ok(42)
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Ok_InReturn_InfersFromReturnType()
    {
        var code = @"
def parse(s: str) -> int !ValueError:
    return Ok(42)
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Ok_WithoutTypeContext_ReportsError()
    {
        var code = @"
x = Ok(42)
";
        var result = Compile(code);
        Assert.False(result.Success);
        Assert.Contains("Cannot infer type", GetErrors(result));
    }
    
    #endregion
    
    #region Err Inference
    
    [Fact]
    public void Err_WithTypedVariable_InfersCorrectly()
    {
        var code = @"
x: int !str = Err(""error message"")
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Err_InReturn_InfersFromReturnType()
    {
        var code = @"
def parse(s: str) -> int !str:
    return Err(""invalid input"")
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Err_WithoutTypeContext_ReportsError()
    {
        var code = @"
x = Err(""error"")
";
        var result = Compile(code);
        Assert.False(result.Success);
        Assert.Contains("Cannot infer type", GetErrors(result));
    }
    
    #endregion
    
    #region Default Parameters
    
    [Fact]
    public void Nothing_AsDefaultParameter_InfersCorrectly()
    {
        var code = @"
def foo(x: int? = Nothing) -> None:
    pass
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Some_AsDefaultParameter_InfersCorrectly()
    {
        var code = @"
def foo(x: int? = Some(0)) -> None:
    pass
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    #endregion
}
```

### Verification

- [ ] Run tests: `dotnet test src/Sharpy.Compiler.Tests --filter ConstructorInferenceTests`
- [ ] All tests pass (or expected failures for not-yet-implemented features)

```
git add src/Sharpy.Compiler.Tests/Semantic/ConstructorInferenceTests.cs
git commit -m "test: add tests for tagged union constructor inference"
```

---

## Task 7.6: Update Documentation

**File:** `docs/language_specification/tagged_unions_optional.md`

### Steps

- [ ] Add a section about constructor shorthand:
  ```markdown
  ## Constructor Shorthand

  When the expected type is known, you can use `Some(value)` and `Nothing` 
  without qualifying with the type name:

  ```python
  # With type annotation - shorthand works
  x: int? = Some(42)
  y: int? = Nothing

  # Function return - shorthand works  
  def get_value() -> int?:
      return Some(42)

  # Without type context - must be explicit
  x = Optional.Some(42)  # or: x = Optional[int].Some(42)
  ```

  The compiler infers the full type from context.
  ```

**File:** `docs/language_specification/tagged_unions_result.md`

- [ ] Add similar section for Result:
  ```markdown
  ## Constructor Shorthand

  When the expected type is known, you can use `Ok(value)` and `Err(error)` 
  without qualifying with the type name:

  ```python
  # With type annotation - shorthand works
  x: int !str = Ok(42)
  y: int !str = Err("failed")

  # Function return - shorthand works
  def parse(s: str) -> int !ValueError:
      if not s:
          return Err(ValueError("empty string"))
      return Ok(int(s))

  # Without type context - must be explicit
  x = Result.Ok[int, str](42)
  ```

  The compiler infers the full type from context.
  ```

### Verification

- [ ] Read through updated documentation
- [ ] Verify examples are consistent with implementation

```
git add docs/language_specification/tagged_unions_optional.md docs/language_specification/tagged_unions_result.md
git commit -m "spec: document constructor shorthand for Optional and Result"
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
1. `semantic: add constructor recognition for Some/Ok/Err`
2. `semantic: add Nothing identifier recognition for Optional`
3. `semantic: register tagged union constructor names as built-ins`
4. `test: add tests for tagged union constructor inference`
5. `spec: document constructor shorthand for Optional and Result`

---

## Notes for Implementer

- **Expected type propagation:** The key to this working is that the type checker needs to propagate an "expected type" down when checking expressions. Many type checkers have this, but if Sharpy's doesn't, this will require architectural changes.

- **Error handling vs ValueError:** The examples use `ValueError` as an error type. You may need to ensure this type exists in the symbol table for tests to pass, or use `str` as a simpler error type for testing.

- **Explicit vs shorthand:** Users can always use explicit syntax like `Optional.Some(42)` or `Result.Ok(42)`. The shorthand just removes the need for the type prefix in contexts where the type is inferrable.
