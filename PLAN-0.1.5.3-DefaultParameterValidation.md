# Implementation Plan: Task 0.1.5.3 - Default Parameter Validation

## Overview

Implement semantic validation for default parameter values to ensure they are compile-time constants and that `None` is only used with nullable parameter types.

## Step-by-Step Implementation Approach

### Step 1: Create DefaultParameterValidator Class

**File**: `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs` (new file)

Create a new validator following the existing validator patterns (similar to `AccessValidator` and `ControlFlowValidator`):

```csharp
public class DefaultParameterValidator
{
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    public IReadOnlyList<SemanticError> Errors => _errors;

    public void ValidateParameter(Parameter param, SemanticType paramType, string functionName);
    private bool IsCompileTimeConstant(Expression expr);
    private bool IsMutableDefault(Expression expr);
    private void AddError(string message, int? line, int? column);
}
```

**Key validation logic**:

1. **Compile-time constant check** - `IsCompileTimeConstant()`:
   - Allow: `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `BooleanLiteral`, `NoneLiteral`, `TupleLiteral` (with constant elements)
   - Reject: `Identifier`, `FunctionCall`, `BinaryOp`, `UnaryOp`, `MemberAccess`, `ListLiteral`, `DictLiteral`, `SetLiteral`

2. **Mutable default detection** - `IsMutableDefault()`:
   - Detect empty mutable containers: `[]` (ListLiteral), `{}` (DictLiteral), `set()` (FunctionCall with name "set")
   - These are dangerous because Python's "mutable default argument" problem

3. **None/nullable validation**:
   - If default is `NoneLiteral`, parameter type must be `NullableType`
   - Error message: `"Default value 'None' is not valid for non-nullable parameter type '{paramType}'"`

### Step 2: Integrate into TypeChecker

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

Modify the existing `CheckFunction()` method to use the new validator.

**Changes needed**:

1. Add field: `private readonly DefaultParameterValidator _defaultParamValidator;`

2. Initialize in constructor

3. In `CheckFunction()`, around line 264-272 where default values are currently type-checked, add:
   ```csharp
   _defaultParamValidator.ValidateParameter(param, paramType, functionDef.Name);
   ```

4. Add errors to combined `Errors` property (around line 55-67):
   ```csharp
   allErrors.AddRange(_defaultParamValidator.Errors);
   ```

### Step 3: Create Tests

**File**: `src/Sharpy.Compiler.Tests/Semantic/DefaultParameterValidatorTests.cs` (new file)

Test categories:

1. **Valid defaults** - should pass:
   - Integer literal: `def foo(x: int = 42)`
   - String literal: `def foo(x: str = "hello")`
   - Boolean literal: `def foo(x: bool = True)`
   - Float literal: `def foo(x: float = 3.14)`
   - None with nullable: `def foo(x: int? = None)`

2. **Invalid mutable defaults** - should error:
   - Empty list: `def foo(x: list[int] = [])`
   - Empty dict: `def foo(x: dict[str, int] = {})`
   - Empty set: `def foo(x: set[int] = set())`

3. **Non-constant defaults** - should error:
   - Variable reference: `def foo(x: int = y)`
   - Function call: `def foo(x: int = bar())`
   - Binary operation: `def foo(x: int = 1 + 2)`

4. **None nullability errors** - should error:
   - `def foo(x: int = None)` - int is not nullable
   - `def foo(x: str = None)` - str is not nullable

### Step 4: Integration Tests

**File**: `src/Sharpy.Compiler.Tests/Integration/FunctionTests.cs`

Add tests that compile and run programs with default parameters:
- Verify existing `FunctionWithDefaultParameter_WorksCorrectly` test passes
- Add negative test that verifies compilation fails with appropriate errors for invalid defaults

## Key Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs` | Create | New validator class |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Modify | Integrate validator |
| `src/Sharpy.Compiler.Tests/Semantic/DefaultParameterValidatorTests.cs` | Create | Unit tests |
| `src/Sharpy.Compiler.Tests/Integration/FunctionTests.cs` | Modify | Integration tests |

## Tests to Verify

### Unit Tests (DefaultParameterValidatorTests.cs)

1. `ValidateParameter_AcceptsIntegerLiteralDefault`
2. `ValidateParameter_AcceptsStringLiteralDefault`
3. `ValidateParameter_AcceptsBooleanLiteralDefault`
4. `ValidateParameter_AcceptsFloatLiteralDefault`
5. `ValidateParameter_AcceptsNoneForNullableType`
6. `ValidateParameter_RejectsNoneForNonNullableType`
7. `ValidateParameter_RejectsEmptyListDefault`
8. `ValidateParameter_RejectsEmptyDictDefault`
9. `ValidateParameter_RejectsSetCallDefault`
10. `ValidateParameter_RejectsVariableReference`
11. `ValidateParameter_RejectsFunctionCallDefault`
12. `ValidateParameter_RejectsBinaryOperationDefault`
13. `IsCompileTimeConstant_AcceptsTupleLiteralWithConstants`
14. `IsCompileTimeConstant_RejectsTupleLiteralWithNonConstants`

### Integration Tests (FunctionTests.cs)

1. `DefaultParameter_WithNoneForNullableType_Compiles` (new)
2. `DefaultParameter_WithNoneForNonNullableType_FailsCompilation` (new)
3. `DefaultParameter_WithMutableList_FailsCompilation` (new)

## Potential Risks and Questions

### Risks

1. **Breaking existing code**: Need to ensure the validation doesn't reject currently valid programs. Review existing test cases in `FunctionTests.cs` to ensure they still pass.

2. **Tuple literal handling**: Should tuple literals be allowed as defaults? They are immutable in Python, but implementation needs to verify all elements are also constants.

3. **Performance**: The validator will be called for every parameter with a default. Ensure `IsCompileTimeConstant()` is efficient (simple switch expression).

4. **Error recovery**: If validation fails, the compiler should continue checking other parameters rather than stopping immediately.

### Questions to Clarify

1. **Should we allow negative literals?** e.g., `def foo(x: int = -1)` - This parses as `UnaryOp(Neg, IntegerLiteral(1))`.
   - **Recommendation**: Yes, allow `UnaryOp` with `-` or `+` on numeric literals as compile-time constants.

2. **Should we allow simple constant expressions?** e.g., `def foo(x: int = 1 + 2)`
   - **Recommendation**: No, stick to pure literals for simplicity. Users can pre-compute.

3. **What about string interpolation?** e.g., `def foo(x: str = f"hello {world}")`
   - **Recommendation**: Reject - f-strings are not compile-time constants.

4. **Should frozenset() be allowed?** If Sharpy supports frozen sets, these would be immutable.
   - **Recommendation**: Not in initial implementation. Add later if needed.

5. **Error message format**: Should errors reference both the parameter name and function name?
   - **Recommendation**: Yes, for clarity: `"Parameter 'x' in function 'foo': default value must be a compile-time constant"`

## Implementation Order

1. Create `DefaultParameterValidator.cs` with core validation logic
2. Write unit tests for the validator
3. Integrate validator into `TypeChecker.cs`
4. Add integration tests
5. Run full test suite to verify no regressions
6. Review error messages for clarity

## Example Error Messages

```
Semantic error at line 5, column 20: Parameter 'items' in function 'process': mutable default '[]' is not allowed. Use None as default and initialize in function body.

Semantic error at line 8, column 15: Parameter 'x' in function 'foo': default value 'None' is not valid for non-nullable type 'int'. Use 'int?' for nullable parameter.

Semantic error at line 12, column 22: Parameter 'count' in function 'bar': default value must be a compile-time constant, got function call 'get_default()'.
```
