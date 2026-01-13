# Implementation Plan: R-0.1.2.1 - Fix Floor Division Semantics

## Summary

The current floor division implementation (`//` and `//=`) uses `(int)(x/y)` which truncates toward zero, but Python/spec requires flooring toward negative infinity.

**Current behavior (WRONG):**
- `-7 // 3` → `(int)(-7/3)` → `-2` (truncates toward zero)

**Required behavior (CORRECT):**
- `-7 // 3` → `Math.Floor(-7.0/3)` → `-3` (floors toward negative infinity)

## Analysis

### Current Implementation

**Location:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

1. **Binary `//` operator** (lines 1831-1837):
```csharp
case BinaryOperator.FloorDivide:
    // x // y → (int)(x / y) for integers
    return CastExpression(
        PredefinedType(Token(SyntaxKind.IntKeyword)),
        ParenthesizedExpression(BinaryExpression(SyntaxKind.DivideExpression, left, right)));
```

2. **Compound assignment `//=`** (lines 1475-1479):
```csharp
AssignmentOperator.DoubleSlashAssign =>
    CastExpression(
        PredefinedType(Token(SyntaxKind.IntKeyword)),
        BinaryExpression(SyntaxKind.DivideExpression, left, right)),
```

### Spec Requirement (from `docs/language_specification/arithmetic_operators.md`)

> `//`: Lowered to `(long)Math.Floor((double)a / b)` for integers, `Math.Floor(a / b)` for floats.

**Return type rules:**
| Operands | Result Type |
|----------|-------------|
| Any integer types | `int64` (long) |
| Any float type | Same float type |
| Mixed integer and float | Float type of the float operand |

## Implementation Approach

### Step 1: Modify Binary FloorDivide Operator

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

Change the `BinaryOperator.FloorDivide` case to generate:
```csharp
// For integers: (long)Math.Floor((double)left / right)
// For floats: Math.Floor(left / right)
// For mixed: Math.Floor((double)left / right) or Math.Floor(left / (double)right)
```

Since the emitter currently doesn't have type information available at code generation time (based on grep results showing no `ResolvedType` usage in the floor divide logic), we have two options:

**Option A (Simple/Conservative):** Always generate the integer formula with casts:
```csharp
(long)Math.Floor((double)left / right)
```
This works correctly for:
- Integer // Integer → long (correct per spec)
- Integer // Float → correct value (implicit cast to double is fine)
- Float // Float → long (loses float result type, but value is correct)

**Option B (Full spec compliance):** Pass type information through to code gen and generate type-specific code. This requires:
1. Checking if semantic type info is available on AST nodes
2. Adding conditional logic based on operand types

**Recommendation:** Start with Option A for correctness, then enhance to Option B if type preservation is critical.

### Step 2: Modify Compound Assignment `//=`

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

Apply the same fix to `GenerateAugmentedValue`:
```csharp
AssignmentOperator.DoubleSlashAssign =>
    CastExpression(
        PredefinedType(Token(SyntaxKind.LongKeyword)),  // Changed from int to long
        InvocationExpression(
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("Math"),
                IdentifierName("Floor")))
            .AddArgumentListArguments(
                Argument(
                    BinaryExpression(SyntaxKind.DivideExpression,
                        CastExpression(PredefinedType(Token(SyntaxKind.DoubleKeyword)), left),
                        right)))),
```

### Step 3: Update Existing Test

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterExpressionTests.cs`

Update the existing test `GenerateExpression_FloorDivide_GeneratesCastAndDivide` to verify the new output:
```csharp
[Fact]
public void GenerateExpression_FloorDivide_GeneratesMathFloorWithCast()
{
    // Arrange
    var expr = new BinaryOp
    {
        Operator = BinaryOperator.FloorDivide,
        Left = new IntegerLiteral { Value = "10" },
        Right = new IntegerLiteral { Value = "3" }
    };

    // Act
    var result = InvokeGenerateExpression(expr);

    // Assert
    var code = result.ToString();
    code.Should().Contain("Math.Floor");
    code.Should().Contain("(long)");
}
```

### Step 4: Add Negative Number Tests

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterExpressionTests.cs`

Add tests for negative numbers to verify correct floor semantics:
```csharp
[Theory]
[InlineData("-7", "3", -3L)]   // -7 // 3 = -3 (not -2!)
[InlineData("7", "-3", -3L)]   // 7 // -3 = -3 (not -2!)
[InlineData("-7", "-3", 2L)]   // -7 // -3 = 2
[InlineData("7", "3", 2L)]     // 7 // 3 = 2
public void GenerateExpression_FloorDivide_NegativeNumbersFloorCorrectly(...)
```

### Step 5: Add Integration Tests

**File:** `src/Sharpy.Compiler.Tests/Integration/BasicProgramTests.cs` or create new file

Add end-to-end tests that compile and execute floor division:
```csharp
[Theory]
[InlineData("-7 // 3", "-3")]
[InlineData("7 // -3", "-3")]
[InlineData("-7 // -3", "2")]
[InlineData("7 // 3", "2")]
public void FloorDivision_CorrectSemantics(string expression, string expected)
```

## Files to Modify

1. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`**
   - `BinaryOperator.FloorDivide` case (~line 1831)
   - `GenerateAugmentedValue` method, `DoubleSlashAssign` case (~line 1475)

2. **`src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterExpressionTests.cs`**
   - Update `GenerateExpression_FloorDivide_GeneratesCastAndDivide`
   - Add negative number theory tests

3. **`src/Sharpy.Compiler.Tests/Integration/` (new or existing file)**
   - Add end-to-end execution tests

## Generated Code Examples

### Before (incorrect):
```csharp
// -7 // 3
(int)(-7 / 3)  // Evaluates to -2 (WRONG)
```

### After (correct):
```csharp
// -7 // 3
(long)Math.Floor((double)(-7) / 3)  // Evaluates to -3 (CORRECT)
```

## Potential Risks and Questions

### Risks

1. **Performance:** `Math.Floor` with casts is slower than integer division. For hot paths, this could matter. However, correctness is more important than performance.

2. **Type mismatch:** The spec says floats should return the same float type, but this implementation always returns `long`. Need to verify if type preservation is critical for downstream operations.

3. **Overflow:** Large integer values cast to `double` may lose precision. For `long.MaxValue`, this could produce incorrect results.

### Questions

1. **Is type information available at codegen time?** If `BinaryOp` nodes have resolved type info, we could generate more specific code.

2. **Should we add a runtime helper?** The `DivMod.cs` already has correct floor division logic. We could expose `FloorDiv` helper methods and call those instead of inline `Math.Floor`.

3. **Decimal support:** The spec doesn't mention decimal floor division. `Math.Floor` doesn't work with `decimal` - would need `Math.Floor((double)x/y)` or a special case.

## Testing Strategy

1. **Unit tests** - Verify generated C# code structure
2. **Integration tests** - Compile and execute, verify actual output values
3. **Edge cases:**
   - Zero divisor (should throw)
   - MAX_VALUE / 1
   - MIN_VALUE / -1
   - Mixed positive/negative operands
   - Float operands

## Rollback Plan

If issues arise, the change is localized to two switch cases in `RoslynEmitter.cs`. Reverting those changes would restore the previous (incorrect) behavior.
