# Implementation Plan: R-0.1.1.4 - Audit Pipe Operator Code Generation

## Summary

The pipe operator (`|>`) parsing is complete (Task 0.1.1.3), but code generation in `RoslynEmitter.cs` throws `NotImplementedException` when encountering `BinaryOperator.PipeForward`. This task requires implementing the code generation to transform pipe expressions into proper function calls.

---

## Current State Analysis

### What's Already Implemented
- **Lexer**: Tokenizes `|>` as `TokenType.PipeForward` (`Lexer.cs:1621-1624`)
- **Parser**: Parses pipe expressions into `BinaryOp` AST nodes (`Parser.cs:1678-1700`)
- **AST**: `BinaryOperator.PipeForward` enum value exists (`Expression.cs:301`)

### What's Missing
- **RoslynEmitter.cs:1812-1927**: The `GenerateBinaryOp` method has no case for `PipeForward`
- Currently hits the `_ => throw new NotImplementedException(...)` fallback at line 1923

---

## Step-by-Step Implementation Approach

### Step 1: Add PipeForward Case in GenerateBinaryOp

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Location**: Add to switch statement at line ~1818 (after `IsNot` case, before standard operators)

**Logic**:
```csharp
case BinaryOperator.PipeForward:
    return GeneratePipeForward(binOp);
```

### Step 2: Implement GeneratePipeForward Method

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Location**: After `GenerateBinaryOp` method (around line 1927)

**Transformation Rules** (per `pipe_operator.md`):

| Sharpy Source | C# Output |
|---------------|-----------|
| `x \|> f` | `f(x)` |
| `x \|> f(y)` | `f(x, y)` |
| `x \|> f(y, z)` | `f(x, y, z)` |
| `x \|> obj.method(y)` | `obj.method(x, y)` |
| `x \|> f \|> g` | `g(f(x))` - handled by recursion |

**Implementation Pseudocode**:
```csharp
private ExpressionSyntax GeneratePipeForward(BinaryOp binOp)
{
    // Generate the left side (the piped value)
    var pipedValue = GenerateExpression(binOp.Left);

    // Right side can be:
    // 1. FunctionCall with arguments: f(y) → prepend pipedValue to args
    // 2. Identifier (bare function): f → call with pipedValue as only arg
    // 3. MemberAccess (method): obj.method → call with pipedValue as only arg

    if (binOp.Right is FunctionCall call)
    {
        // Prepend pipedValue to the existing argument list
        // f(y, z) becomes f(pipedValue, y, z)
        return GenerateFunctionCallWithPrependedArg(call, pipedValue);
    }
    else
    {
        // Bare function/method reference: f → f(pipedValue)
        var func = GenerateExpression(binOp.Right);
        return InvocationExpression(func)
            .AddArgumentListArguments(Argument(pipedValue));
    }
}
```

### Step 3: Implement Helper for Prepending Argument

**Method**: `GenerateFunctionCallWithPrependedArg`

Need to handle:
- Simple function calls: `f(y)` → `f(x, y)`
- Method calls: `obj.method(y)` → `obj.method(x, y)`
- Keyword arguments: `f(y, key=z)` → `f(x, y, key=z)`

**Key insight**: Reuse existing `GenerateFunctionCall` logic but prepend the piped value to `Arguments` list.

---

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add `PipeForward` case + `GeneratePipeForward` method |

---

## Tests to Add

### Unit Tests (RoslynEmitterOperatorTests.cs or new file)

1. **Simple pipe to function**
   ```python
   x |> f
   ```
   Expected C#: `f(x)`

2. **Pipe to function with arguments**
   ```python
   x |> f(y)
   ```
   Expected C#: `f(x, y)`

3. **Pipe to function with multiple arguments**
   ```python
   x |> f(y, z)
   ```
   Expected C#: `f(x, y, z)`

4. **Chained pipes**
   ```python
   x |> f |> g
   ```
   Expected C#: `g(f(x))`

5. **Chained pipes with arguments**
   ```python
   x |> f(y) |> g(z)
   ```
   Expected C#: `g(f(x, y), z)`

6. **Pipe to method call**
   ```python
   x |> obj.method(y)
   ```
   Expected C#: `obj.method(x, y)`

### Integration Tests (new file or CompilerIntegrationTests.cs)

1. **Full compilation with pipe operator**
   - Compile Sharpy code with pipes
   - Verify no compilation errors
   - Optionally verify generated C# code structure

---

## Potential Risks and Questions

### Risks

1. **Method vs Function Ambiguity**:
   - When right side is `MemberAccess` (e.g., `obj.method`), it could be:
     - A method to call: `x |> obj.method` → `obj.method(x)`
     - A property that is callable: `x |> obj.func` where `func` is `Func<T,R>`
   - **Mitigation**: For now, treat all as invocations; type checking should catch errors

2. **Bare Identifier Handling**:
   - `x |> f` where `f` is an identifier needs to become `f(x)`
   - Current `GenerateFunctionCall` may expect `FunctionCall` AST node
   - **Mitigation**: Handle `Identifier` case explicitly in `GeneratePipeForward`

3. **Keyword Arguments Order**:
   - `x |> f(key=y)` should become `f(x, key=y)` (piped value is positional, keyword stays keyword)
   - **Mitigation**: Only prepend to positional `Arguments`, leave `KeywordArguments` unchanged

4. **Lambda Expressions**:
   - `x |> (lambda y: y + 1)` - less common but possible
   - **Mitigation**: Should work if we generate the lambda and invoke it with the piped value

### Questions for Clarification

1. **Constructor Pipes**: The spec says "Cannot pipe to constructors directly" - should we emit an error, or let it fail at C# compilation?

2. **Bare Method References**: Is `x |> str.upper` valid (method reference without parens)? The spec examples use `str.upper()` with parens.

3. **Static Methods**: `x |> ClassName.static_method(y)` - should work naturally, but needs verification.

---

## Implementation Order

1. Add `PipeForward` case to switch in `GenerateBinaryOp`
2. Implement `GeneratePipeForward` method handling:
   - `FunctionCall` case (prepend to arguments)
   - `Identifier` case (simple invocation)
   - `MemberAccess` case (method invocation)
3. Add unit tests for each transformation case
4. Add integration test for full compilation
5. Test edge cases (chained pipes, mixed with other operators)

---

## Estimated Complexity

- **Code Changes**: ~30-50 lines in RoslynEmitter.cs
- **Test Code**: ~100-150 lines across test files
- **Risk Level**: Low - isolated change in code generation, well-defined semantics
