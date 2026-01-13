# Implementation Plan: R-0.1.1.5 - Audit `to` Operator Code Generation

## Summary

The `to` operator (TypeCoercion) was implemented in parsing (Task 0.1.1.4), but code generation in `RoslynEmitter.cs` does **NOT** currently handle `TypeCoercion` expressions. This task requires implementing the code generation to transform `to` expressions into proper C# cast expressions.

---

## Current State Analysis

### What's Already Implemented
- **AST Definition**: `TypeCoercion` record in `Expression.cs:359-368`
- **Lexer**: Tokenizes `to` as `TokenType.To`
- **Parser**: Creates `TypeCoercion` nodes with `Value` and `TargetType` properties (`Parser.cs:2024-2040`)
- **Type Annotation**: `TargetType.IsNullable` captures the `T` vs `T?` distinction

### What's Missing
- **RoslynEmitter.cs**: The expression dispatch switch (`GenerateExpression` at line 1718-1769) has **no case for `TypeCoercion`**
- Currently will throw `NotImplementedException` if code with `to` operator is compiled
- No tests for code generation

---

## Step-by-Step Implementation Approach

### Step 1: Add TypeCoercion Case in GenerateExpression Switch

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Location**: Add to switch statement at line ~1757 (after `TypeCast` and `TypeCheck` cases)

```csharp
TypeCast cast => GenerateTypeCast(cast),
TypeCheck check => GenerateTypeCheck(check),
TypeCoercion coercion => GenerateTypeCoercion(coercion),  // ADD THIS
```

### Step 2: Implement GenerateTypeCoercion Method

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Location**: After `GenerateTypeCheck` method (around line 2423)

**Transformation Rules** (per `type_casting.md:212-225`):

| Sharpy Source | C# Output | Notes |
|---------------|-----------|-------|
| `value to T` | `(T)value` | Throwing cast for reference types |
| `value to T?` (reference type) | `value as T` | Safe cast returning null on failure |
| `value to T?` (value type) | `value is T _temp ? (T?)_temp : null` | Pattern match for value types |

**Implementation**:
```csharp
private ExpressionSyntax GenerateTypeCoercion(TypeCoercion coercion)
{
    var value = GenerateExpression(coercion.Value);
    var targetType = _typeMapper.MapType(coercion.TargetType);

    if (!coercion.TargetType.IsNullable)
    {
        // value to T → (T)value (throwing cast)
        return CastExpression(targetType, value);
    }
    else
    {
        // value to T? → value as T (safe cast)
        // For reference types, C# "as" works directly
        // For value types, need pattern: value is T temp ? (T?)temp : null

        // Get the non-nullable target type for the "as" expression
        var baseType = GetNonNullableType(coercion.TargetType);
        var baseTypeSyntax = _typeMapper.MapType(baseType);

        if (IsValueType(baseType))
        {
            // Value type: value is T temp ? (T?)temp : null
            var tempName = GenerateUniqueIdentifier("_cast");
            return ConditionalExpression(
                IsPatternExpression(
                    value,
                    DeclarationPattern(baseTypeSyntax, SingleVariableDesignation(Identifier(tempName)))),
                CastExpression(
                    NullableType(baseTypeSyntax),
                    IdentifierName(tempName)),
                LiteralExpression(SyntaxKind.NullLiteralExpression));
        }
        else
        {
            // Reference type: value as T
            return BinaryExpression(SyntaxKind.AsExpression, value, baseTypeSyntax);
        }
    }
}
```

### Step 3: Add Helper Methods (if not already present)

May need to add:
1. `GetNonNullableType(TypeAnnotation)` - strips nullable marker from type annotation
2. `IsValueType(TypeAnnotation)` - determines if target is a value type (int, float, etc.)
3. `GenerateUniqueIdentifier(string prefix)` - generates unique temp variable names

**Known Value Types in Sharpy**: `int`, `int8`, `int16`, `int32`, `int64`, `uint`, `uint8`, `uint16`, `uint32`, `uint64`, `float`, `float32`, `float64`, `bool`, `byte`

---

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add `TypeCoercion` case + `GenerateTypeCoercion` method + helper methods |
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterExpressionTests.cs` | Add tests for TypeCoercion code generation |

---

## Tests to Add

### Unit Tests (RoslynEmitterExpressionTests.cs)

Add to the existing `#region Type Cast and Check Tests` section:

1. **Non-nullable type coercion (throwing)**
   ```csharp
   [Fact]
   public void GenerateExpression_TypeCoercion_NonNullable_GeneratesCastExpression()
   {
       // x to int → (int)x
       var expr = new TypeCoercion
       {
           Value = new Identifier { Name = "x" },
           TargetType = new TypeAnnotation { Name = "int", IsNullable = false }
       };
       var result = InvokeGenerateExpression(expr);
       result.ToString().Should().Contain("(int)").And.Contain("x");
   }
   ```

2. **Nullable reference type coercion (safe)**
   ```csharp
   [Fact]
   public void GenerateExpression_TypeCoercion_NullableReference_GeneratesAsExpression()
   {
       // animal to Dog? → animal as Dog
       var expr = new TypeCoercion
       {
           Value = new Identifier { Name = "animal" },
           TargetType = new TypeAnnotation { Name = "Dog", IsNullable = true }
       };
       var result = InvokeGenerateExpression(expr);
       result.ToString().Should().Contain("animal").And.Contain("as").And.Contain("Dog");
   }
   ```

3. **Nullable value type coercion (pattern match)**
   ```csharp
   [Fact]
   public void GenerateExpression_TypeCoercion_NullableValueType_GeneratesPatternMatch()
   {
       // value to int? → value is int _temp ? (int?)_temp : null
       var expr = new TypeCoercion
       {
           Value = new Identifier { Name = "value" },
           TargetType = new TypeAnnotation { Name = "int", IsNullable = true }
       };
       var result = InvokeGenerateExpression(expr);
       var code = result.ToString();
       code.Should().Contain("value").And.Contain("is").And.Contain("int");
       code.Should().Contain("?").And.Contain("null");
   }
   ```

4. **Type coercion with complex expression**
   ```csharp
   [Fact]
   public void GenerateExpression_TypeCoercion_ComplexExpression()
   {
       // (a + b) to float → (float)(a + b)
       var expr = new TypeCoercion
       {
           Value = new BinaryOp
           {
               Left = new Identifier { Name = "a" },
               Operator = BinaryOperator.Add,
               Right = new Identifier { Name = "b" }
           },
           TargetType = new TypeAnnotation { Name = "float", IsNullable = false }
       };
       var result = InvokeGenerateExpression(expr);
       result.ToString().Should().Contain("(double)");  // float maps to double in C#
   }
   ```

5. **Type coercion to user-defined type**
   ```csharp
   [Fact]
   public void GenerateExpression_TypeCoercion_UserDefinedType()
   {
       // obj to MyClass → (MyClass)obj
       var expr = new TypeCoercion
       {
           Value = new Identifier { Name = "obj" },
           TargetType = new TypeAnnotation { Name = "MyClass", IsNullable = false }
       };
       var result = InvokeGenerateExpression(expr);
       result.ToString().Should().Contain("(MyClass)").And.Contain("obj");
   }
   ```

---

## Potential Risks and Questions

### Risks

1. **Value Type Detection**:
   - Need reliable way to determine if target type is a value type
   - Built-in types like `int`, `float`, `bool` are obvious
   - User-defined structs would need semantic info (may not be available at codegen)
   - **Mitigation**: Use a static list of known value types for now; default to reference type behavior for unknown types

2. **Unique Identifier Generation**:
   - Pattern match for value types needs temporary variable: `value is int _temp ? ...`
   - Must ensure unique names across nested expressions
   - **Mitigation**: Prefix with `_cast` and use counter or context-based naming

3. **Generic Types**:
   - `value to List<int>?` needs proper handling of generic type syntax
   - Should work naturally if `_typeMapper.MapType` handles generics correctly
   - **Verification needed**: Test with generic target types

4. **Nested Nullable Types**:
   - What does `value to int??` mean? (Double nullable)
   - **Likely answer**: Parser should reject or treat as `int?`

### Questions for Clarification

1. **Semantic Analysis**: Does semantic analysis run before code generation? If so, type information (value vs reference) may be available. If not, we need heuristics.

2. **Checked/Unchecked Arithmetic**: For numeric conversions like `int64 to int`, should we use C# `checked` context to throw on overflow, or rely on runtime behavior?
   - Spec says "Throws on overflow" which suggests `checked` context
   - **Recommendation**: Use `checked((int)value)` for numeric narrowing

3. **Interface Types**: `obj to IDisposable?` - this is a reference type cast. Should work with `as` operator.

---

## Implementation Order

1. Add `TypeCoercion` case to switch in `GenerateExpression`
2. Implement `GenerateTypeCoercion` method with:
   - Non-nullable case (simple cast)
   - Nullable reference type case (`as` expression)
   - Nullable value type case (pattern match)
3. Add helper method for value type detection
4. Add unit tests for each case
5. Test with integration/E2E tests

---

## Design Decision: Value Type Detection

Since semantic type information may not be fully available at code generation time, propose using a static list of known value types:

```csharp
private static readonly HashSet<string> KnownValueTypes = new()
{
    "int", "int8", "int16", "int32", "int64",
    "uint", "uint8", "uint16", "uint32", "uint64",
    "float", "float32", "float64", "double",
    "bool", "byte", "sbyte", "short", "ushort", "long", "ulong",
    "char", "decimal"
};

private bool IsValueType(TypeAnnotation type)
{
    return KnownValueTypes.Contains(type.Name.ToLowerInvariant());
}
```

For user-defined types, default to reference type behavior (use `as`). This is safer because:
- If a struct is treated as reference type, `as` will fail to compile (caught by C# compiler)
- If a class is treated as value type incorrectly, the pattern match still works (just less efficient)

---

## Complexity Assessment

- **Code Changes**: ~50-80 lines in RoslynEmitter.cs
- **Test Code**: ~80-100 lines in test file
- **Risk Level**: Medium - involves pattern matching for value types which adds complexity
- **Dependencies**: None beyond existing infrastructure
