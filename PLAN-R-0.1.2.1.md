# Implementation Plan: R-0.1.2.1 - Fix Floor Division Semantics

## Problem Summary

The current floor division implementation (`//` and `//=`) uses incorrect semantics. The issue is:

**Current implementation** (in `RoslynEmitter.cs`):
- `(int)Math.Floor((double)x / y)` - casts result to `int`

**Required per specification** (`docs/language_specification/arithmetic_operators.md`):
- For integers: `(long)Math.Floor((double)a / b)` - result should be `int64`/`long`
- For floats: `Math.Floor(a / b)` - result stays as float type (no cast)

### Issues with Current Code

1. **Wrong result type for integers**: Casts to `int` (32-bit) instead of `long` (64-bit)
2. **Wrong result type for floats**: Casts float results to `int` instead of keeping them as floats
3. **No type-aware code generation**: Treats all operands the same regardless of their types

### Spec Requirements (from `arithmetic_operators.md`)

| Operands | Result Type |
|----------|-------------|
| Any integer types | `int64` |
| Any float type | Same float type |
| Mixed integer and float | Float type of the float operand |

**Examples:**
```python
7 // 3      # 2 (int64)
-7 // 3     # -3 (int64), not -2
7.5 // 2.0  # 3.0 (float64)
7 // 2.0    # 3.0 (float64) - mixed: result is float64
7.0f // 2   # 3.0f (float32) - mixed: result is float32
```

---

## Implementation Approach

### Step 1: Analyze Type Information Flow

**Challenge**: The `RoslynEmitter` doesn't currently have access to resolved type information for expressions during code generation. It only has access to:
- `CodeGenContext` with `SymbolTable` and `BuiltinRegistry`
- The AST nodes themselves (which may have type annotations)

**Solution Options**:

**Option A (Recommended)**: Use literal type inference
- For literals, infer type directly from AST node type (`IntegerLiteral`, `FloatLiteral`)
- For identifiers, check if type annotation is available
- Default to integer semantics (long cast) when type is unknown
- This matches current patterns in the codebase

**Option B**: Pass `SemanticInfo` to `RoslynEmitter`
- Would require modifying the compilation pipeline
- More accurate but more invasive change

### Step 2: Implement Type-Aware Floor Division

Modify `GenerateBinaryExpression` in `RoslynEmitter.cs` to handle `BinaryOperator.FloorDivide`:

```csharp
case BinaryOperator.FloorDivide:
    // Determine if either operand is a float type
    bool isFloatOperation = IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right);

    if (isFloatOperation)
    {
        // Float floor division: Math.Floor(a / b) - result stays as float
        return InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("Math"),
                IdentifierName("Floor")))
            .AddArgumentListArguments(
                Argument(BinaryExpression(SyntaxKind.DivideExpression, left, right)));
    }
    else
    {
        // Integer floor division: (long)Math.Floor((double)a / b)
        return CastExpression(
            PredefinedType(Token(SyntaxKind.LongKeyword)),  // Changed from IntKeyword
            InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Math"),
                    IdentifierName("Floor")))
                .AddArgumentListArguments(
                    Argument(BinaryExpression(SyntaxKind.DivideExpression,
                        CastExpression(
                            PredefinedType(Token(SyntaxKind.DoubleKeyword)),
                            ParenthesizedExpression(left)),
                        right))));
    }
```

### Step 3: Implement Helper Method for Type Detection

Add a helper method to detect float expressions:

```csharp
/// <summary>
/// Determines if an expression is a float type based on AST information.
/// Used for floor division to determine result type.
/// </summary>
private bool IsFloatExpression(Expression expr)
{
    return expr switch
    {
        FloatLiteral => true,
        Identifier id when HasFloatTypeAnnotation(id) => true,
        BinaryOp binOp => IsFloatExpression(binOp.Left) || IsFloatExpression(binOp.Right),
        UnaryOp unaryOp => IsFloatExpression(unaryOp.Operand),
        Call call when HasFloatReturnType(call) => true,
        // Default to non-float (integer semantics)
        _ => false
    };
}

private bool HasFloatTypeAnnotation(Identifier id)
{
    // Check symbol table for type information if available
    // This is a best-effort check
    return false; // Conservative default
}

private bool HasFloatReturnType(Call call)
{
    // Check if call target has known float return type
    return false; // Conservative default
}
```

### Step 4: Update Augmented Assignment (`//=`)

Modify `GenerateAugmentedValue` in `RoslynEmitter.cs` similarly:

```csharp
AssignmentOperator.DoubleSlashAssign =>
    IsFloatExpression(/* need to pass left expression type info */) ?
        // Float: Math.Floor(x / y)
        InvocationExpression(...)
        :
        // Integer: (long)Math.Floor((double)x / y)
        CastExpression(
            PredefinedType(Token(SyntaxKind.LongKeyword)),  // Changed from IntKeyword
            ...)
```

**Note**: The `//=` case is more complex because we need the type of the target variable, not just the RHS.

---

## Files to Modify

1. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`**
   - Modify `case BinaryOperator.FloorDivide:` (~line 1950-1966)
   - Modify `AssignmentOperator.DoubleSlashAssign` case (~line 1499-1513)
   - Add helper method `IsFloatExpression(Expression expr)`

---

## Tests to Add/Verify

Create new test file: `src/Sharpy.Compiler.Tests/Integration/FloorDivisionTests.cs`

### Test Cases

1. **Integer floor division - positive numbers**
   ```python
   result: int = 7 // 3  # Expected: 2
   ```

2. **Integer floor division - negative numbers (critical test)**
   ```python
   result: int = -7 // 3  # Expected: -3 (not -2)
   ```

3. **Integer floor division - large numbers (int64 result)**
   ```python
   result: int = 10000000000 // 3  # Verify no overflow
   ```

4. **Float floor division**
   ```python
   result: float = 7.5 // 2.0  # Expected: 3.0
   ```

5. **Mixed integer and float**
   ```python
   result: float = 7 // 2.0  # Expected: 3.0
   ```

6. **Augmented assignment with integers**
   ```python
   x: int = 7
   x //= 3  # Expected: 2
   ```

7. **Augmented assignment with negative numbers**
   ```python
   x: int = -7
   x //= 3  # Expected: -3
   ```

8. **Augmented assignment with floats**
   ```python
   x: float = 7.5
   x //= 2.0  # Expected: 3.0
   ```

---

## Potential Risks and Questions

### Risks

1. **Type inference accuracy**: Without full semantic type information in the emitter, we rely on AST patterns which may not cover all cases (e.g., function return values, complex expressions)

2. **Breaking existing code**: Changing the result type from `int` to `long` could theoretically break code expecting `int`, but:
   - `long` is wider and can hold all `int` values
   - Python semantics expect consistent int64 behavior
   - The spec explicitly requires `int64` result

3. **Float32 vs Float64**: The spec mentions `float32` should return `float32`, but `Math.Floor` returns `double`. May need explicit cast back to `float` for float32 operands.

### Questions to Consider

1. **Should we add SemanticInfo to CodeGenContext?** This would enable more accurate type-aware code generation but is a larger refactor. For this task, we can use the simpler AST-based approach.

2. **Decimal type handling**: The spec doesn't explicitly mention `decimal` floor division. We should verify if `Math.Floor` handles decimal or if we need `decimal.Floor`.

3. **Test coverage**: Should we add unit tests at the emitter level (checking generated C# code) in addition to integration tests?

---

## Implementation Order

1. Add the `IsFloatExpression` helper method
2. Update `BinaryOperator.FloorDivide` case to use `long` for integers
3. Update `AssignmentOperator.DoubleSlashAssign` case similarly
4. Add integration tests for floor division
5. Run existing tests to verify no regressions
6. Test with negative numbers to verify correct floor semantics
