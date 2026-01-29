# Phase 9: `maybe` Expression Code Generation

## Overview

This phase implements code generation for the `maybe` expression, which converts a C# nullable (`T | None`) to a Sharpy Optional (`T?`).

```python
raw: str | None = dotnet_api()  # C# nullable
safe: str? = maybe raw           # Convert to Optional[str]
```

**Prerequisites:** 
- Phase 1 (Core types in Sharpy.Core)
- Phase 5-6 (Semantic types and resolution)
- Phase 8 (Code generation basics)

**Files to modify:**
- `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

**Files to update:**
- `docs/language_specification/maybe_expressions.md`

**Files to create:**
- `src/Sharpy.Compiler.Tests/Integration/MaybeExpressionTests.cs`

---

## Task 9.1: Verify `maybe` Expression Parsing

The parser may already handle `maybe` expressions from existing code. **This must be verified before proceeding.**

### Steps

- [ ] **Search for `MaybeExpression` AST node:**
  ```bash
  grep -r "MaybeExpression" src/Sharpy.Compiler --include="*.cs"
  ```
  - If found in `Parser/Ast/Expression.cs` or similar → proceed
  - If NOT found → you must implement the AST node and parser support first (see "If Not Implemented" below)

- [ ] **Search for `maybe` keyword in parser:**
  ```bash
  grep -r "maybe" src/Sharpy.Compiler/Parser --include="*.cs"
  ```
  - Check if there's parsing logic for `maybe` keyword

- [ ] **Search for `TokenType.Maybe` in lexer:**
  ```bash
  grep -r "Maybe" src/Sharpy.Compiler/Lexer --include="*.cs"
  ```
  - Verify `maybe` is recognized as a keyword

- [ ] Run existing parser tests for `maybe` if any exist

### If NOT Implemented (Add This First)

If `maybe` expression parsing doesn't exist, implement it:

1. **Add to Token.cs:**
   ```csharp
   Maybe,  // maybe keyword
   ```

2. **Add to Lexer (keywords):**
   ```csharp
   { "maybe", TokenType.Maybe },
   ```

3. **Add AST node:**
   ```csharp
   public record MaybeExpression(Expression Operand) : Expression;
   ```

4. **Add parsing logic:**
   ```csharp
   // In ParseUnaryExpression or similar
   if (Current.Type == TokenType.Maybe)
   {
       Advance();
       var operand = ParseUnaryExpression();  // or appropriate precedence
       return new MaybeExpression(operand);
   }
   ```

### Quick Test (After Verification)

```csharp
[Fact]
public void Parse_MaybeExpression_CreatesMaybeNode()
{
    var code = "x = maybe raw";
    var lexer = new Lexer(code);
    var parser = new Parser(lexer.Tokenize());
    var module = parser.Parse();
    
    var stmt = module.Statements[0] as ExpressionStatement;
    var assignment = stmt?.Expression as Assignment;
    Assert.IsType<MaybeExpression>(assignment?.Value);
}
```

### Verification

- [ ] Parsing works for `maybe expr`
- [ ] No changes needed (or fix if broken)

```
# Only commit if changes were needed
git add src/Sharpy.Compiler/Parser/*.cs
git commit -m "fix: ensure maybe expression parsing works"
```

---

## Task 9.2: Implement Type Checking for `maybe`

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`

### Steps

- [ ] Find the method that type-checks expressions
- [ ] Add handling for `MaybeExpression`:

```csharp
private SemanticType CheckMaybeExpression(MaybeExpression expr)
{
    // Type-check the operand
    var operandType = CheckExpression(expr.Operand);
    
    // Operand must be a C# nullable type (T | None)
    if (operandType is not NullableType nullable)
    {
        ReportError(
            $"'maybe' expression requires a nullable type (T | None), " +
            $"but got '{operandType.GetDisplayName()}'",
            expr.LineStart, expr.ColumnStart);
        return SemanticType.Unknown;
    }
    
    // Result is OptionalType wrapping the underlying type
    return new OptionalType { UnderlyingType = nullable.UnderlyingType };
}
```

### Edge Cases to Handle

1. **Already an OptionalType:**
   ```python
   x: int? = Some(42)
   y = maybe x  # ERROR: x is int?, not int | None
   ```

2. **Nested nullables:**
   ```python
   raw: str | None | None = ...  # Not valid anyway
   ```

3. **Non-nullable type:**
   ```python
   x: int = 42
   y = maybe x  # ERROR: x is int, not int | None
   ```

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs
git commit -m "semantic: implement type checking for maybe expression"
```

---

## Task 9.3: Implement Code Generation for `maybe`

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

### Steps

- [ ] Find the expression generation method
- [ ] Add handling for `MaybeExpression`:

```csharp
private ExpressionSyntax GenerateMaybeExpression(MaybeExpression expr, SemanticInfo info)
{
    // Get the underlying type for Optional<T>
    var optionalType = (OptionalType)info.Type;
    var underlyingType = _typeMapper.MapToRoslynType(optionalType.UnderlyingType);
    
    // Generate the operand expression
    var operand = GenerateExpression(expr.Operand);
    
    // Generate: operand == null 
    //           ? Optional<T>.Nothing 
    //           : Optional<T>.Some(operand)
    // 
    // But we need to avoid evaluating operand twice.
    // Use a pattern: operand is T value ? Some(value) : Nothing
    
    // Actually, simpler approach using a helper method would be better.
    // For now, use conditional with potential double evaluation (can optimize later)
    
    var optionalGeneric = GenericName("Optional")
        .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType)));
    
    var nothingExpr = MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        optionalGeneric,
        IdentifierName("Nothing"));
    
    var someExpr = InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            optionalGeneric,
            IdentifierName("Some")))
        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(operand))));
    
    // Generate: operand == null ? Optional<T>.Nothing : Optional<T>.Some(operand)
    var nullCheck = BinaryExpression(
        SyntaxKind.EqualsExpression,
        operand,
        LiteralExpression(SyntaxKind.NullLiteralExpression));
    
    return ConditionalExpression(nullCheck, nothingExpr, someExpr);
}
```

### Better Implementation (Avoiding Double Evaluation)

For value types or when the operand is complex, we should avoid evaluating twice:

```csharp
private ExpressionSyntax GenerateMaybeExpressionOptimized(MaybeExpression expr, SemanticInfo info)
{
    var optionalType = (OptionalType)info.Type;
    var underlyingType = _typeMapper.MapToRoslynType(optionalType.UnderlyingType);
    var operand = GenerateExpression(expr.Operand);
    
    // For reference types, use pattern matching:
    // operand is { } value ? Optional<T>.Some(value) : Optional<T>.Nothing
    
    // For value types (Nullable<T>), use:
    // operand.HasValue ? Optional<T>.Some(operand.Value) : Optional<T>.Nothing
    
    var operandSemanticType = GetExpressionType(expr.Operand);
    var isValueType = operandSemanticType is NullableType nt && nt.UnderlyingType.IsValueType;
    
    if (isValueType)
    {
        // Nullable<T> - use HasValue/Value
        var hasValue = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            operand,
            IdentifierName("HasValue"));
        
        var value = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            operand,
            IdentifierName("Value"));
        
        var someExpr = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                GenericName("Optional").WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
                IdentifierName("Some")))
            .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(value))));
        
        var nothingExpr = MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            GenericName("Optional").WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
            IdentifierName("Nothing"));
        
        return ConditionalExpression(hasValue, someExpr, nothingExpr);
    }
    else
    {
        // Reference type - use pattern matching or simple null check
        // Simple version for now (consider pattern matching for complex cases)
        return GenerateMaybeExpression(expr, info); // Use simple version
    }
}
```

### Verification

- [ ] Build: `dotnet build src/Sharpy.Compiler`
- [ ] No compiler errors

```
git add src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs
git commit -m "codegen: implement maybe expression code generation"
```

---

## Task 9.4: Add Integration Tests for `maybe`

**File:** `src/Sharpy.Compiler.Tests/Integration/MaybeExpressionTests.cs`

### Steps

- [ ] Create new file with comprehensive tests:

```csharp
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

public class MaybeExpressionTests : IntegrationTestBase
{
    #region Type Checking
    
    [Fact]
    public void Maybe_WithNullableType_TypeChecksCorrectly()
    {
        var code = @"
raw: str | None = None
safe: str? = maybe raw
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Maybe_WithNonNullableType_ReportsError()
    {
        var code = @"
x: int = 42
y = maybe x
";
        var result = Compile(code);
        Assert.False(result.Success);
        Assert.Contains("nullable type", GetErrors(result));
    }
    
    [Fact]
    public void Maybe_WithOptionalType_ReportsError()
    {
        var code = @"
x: int? = Some(42)
y = maybe x
";
        var result = Compile(code);
        Assert.False(result.Success);
        Assert.Contains("nullable type", GetErrors(result));
    }
    
    [Fact]
    public void Maybe_ResultType_IsOptional()
    {
        var code = @"
raw: str | None = None
safe: str? = maybe raw
";
        // Verify the result type is OptionalType
        var result = CompileAndGetType(code, "safe");
        Assert.IsType<OptionalType>(result);
    }
    
    #endregion
    
    #region Code Generation
    
    [Fact]
    public void Maybe_GeneratesConditionalExpression()
    {
        var code = @"
raw: str | None = None
safe: str? = maybe raw
";
        var csharp = CompileToCSharp(code);
        
        // Should generate null check and Optional.Some/Nothing
        Assert.Contains("Optional<string>", csharp);
        Assert.Contains("Nothing", csharp);
        Assert.Contains("Some", csharp);
    }
    
    #endregion
    
    #region End-to-End
    
    [Fact]
    public void Maybe_WithNonNullValue_ReturnsSome()
    {
        var code = @"
def convert(raw: str | None) -> str?:
    return maybe raw

def main() -> bool:
    result = convert(""hello"")
    return result.is_some()
";
        var result = CompileAndRun(code);
        Assert.True((bool)result);
    }
    
    [Fact]
    public void Maybe_WithNullValue_ReturnsNothing()
    {
        var code = @"
def convert(raw: str | None) -> str?:
    return maybe raw

def main() -> bool:
    result = convert(None)
    return result.is_nothing()
";
        var result = CompileAndRun(code);
        Assert.True((bool)result);
    }
    
    [Fact]
    public void Maybe_ValueTypeNullable_Works()
    {
        var code = @"
def convert(raw: int | None) -> int?:
    return maybe raw

def main() -> int:
    result = convert(42)
    return result.unwrap()
";
        var result = CompileAndRun(code);
        Assert.Equal(42, result);
    }
    
    [Fact]
    public void Maybe_ValueTypeNullableNull_ReturnsNothing()
    {
        var code = @"
def convert(raw: int | None) -> int?:
    return maybe raw

def main() -> int:
    result = convert(None)
    return result.unwrap_or(99)
";
        var result = CompileAndRun(code);
        Assert.Equal(99, result);
    }
    
    #endregion
    
    #region Complex Expressions
    
    [Fact]
    public void Maybe_ChainedAccess_Works()
    {
        var code = @"
def get_value(dict: dict[str, int] | None, key: str) -> int?:
    # First convert dict to optional, then use it
    safe_dict: dict[str, int]? = maybe dict
    if safe_dict.is_nothing():
        return Nothing
    return Some(safe_dict.unwrap()[key])
";
        var result = Compile(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    #endregion
}
```

### Verification

- [ ] Run tests: `dotnet test src/Sharpy.Compiler.Tests --filter MaybeExpressionTests`
- [ ] All tests pass

```
git add src/Sharpy.Compiler.Tests/Integration/MaybeExpressionTests.cs
git commit -m "test: add integration tests for maybe expression"
```

---

## Task 9.5: Update Documentation

**File:** `docs/language_specification/maybe_expressions.md`

### Steps

- [ ] Review the existing documentation
- [ ] Ensure it accurately reflects the implementation
- [ ] Add implementation notes:

```markdown
## Implementation Notes

The `maybe` expression generates code that:
1. Checks if the nullable value is null
2. If null, returns `Optional<T>.Nothing`
3. If not null, returns `Optional<T>.Some(value)`

For reference types:
```csharp
// maybe raw
raw == null ? Optional<string>.Nothing : Optional<string>.Some(raw)
```

For value types (Nullable<T>):
```csharp
// maybe raw  (where raw is int?)
raw.HasValue ? Optional<int>.Some(raw.Value) : Optional<int>.Nothing
```

*Implementation*
- *✅ Native — Generates conditional expression that wraps nullable in Optional.*
```

### Verification

- [ ] Read through the documentation
- [ ] Ensure examples match implementation behavior

```
git add docs/language_specification/maybe_expressions.md
git commit -m "spec: update maybe_expressions with implementation notes"
```

---

## Task 9.6: Add Helper Method to Optional (Optional)

**File:** `src/Sharpy.Core/Optional.cs`

### Steps

Consider adding a static helper method to make the conversion cleaner:

```csharp
public static class Optional
{
    // Existing
    public static Optional<T> Some<T>(T value) => Optional<T>.Some(value);
    
    // New: Convert nullable to Optional
    public static Optional<T> FromNullable<T>(T? value) where T : class
        => value is null ? Optional<T>.Nothing : Optional<T>.Some(value);
    
    public static Optional<T> FromNullable<T>(T? value) where T : struct
        => value.HasValue ? Optional<T>.Some(value.Value) : Optional<T>.Nothing;
}
```

Then the code generator could simply generate:
```csharp
Optional.FromNullable(raw)
```

### Verification

- [ ] Build Sharpy.Core: `dotnet build src/Sharpy.Core`
- [ ] Update code generator to use helper (if implemented)

```
git add src/Sharpy.Core/Optional.cs
git commit -m "core: add FromNullable helper to Optional"
```

---

## Final Verification

- [ ] Build entire solution: `dotnet build`
- [ ] Run all tests: `dotnet test`
- [ ] All tests pass
- [ ] Review all commits in this phase

```
git log --oneline -5
```

Expected commits:
1. `semantic: implement type checking for maybe expression`
2. `codegen: implement maybe expression code generation`
3. `test: add integration tests for maybe expression`
4. `spec: update maybe_expressions with implementation notes`
5. `core: add FromNullable helper to Optional` (optional)

---

## Notes for Implementer

- **Type distinction is critical:** `maybe` ONLY works on `T | None` (NullableType), NOT on `T?` (OptionalType). This is the whole point — it converts from the unsafe world to the safe world.

- **Double evaluation:** The simple implementation evaluates the operand twice in the ternary. For complex expressions, consider using a local variable or pattern matching.

- **Value types vs reference types:** C# `Nullable<T>` (for value types) has `.HasValue` and `.Value`, while nullable reference types are just `T?` with null checks.

- **The `FromNullable` helper:** If implemented, it makes the generated code much cleaner and handles the value/reference type distinction internally.
