# Implementation Plan: Task 0.1.3.6 - Create Phase 0.1.3 Integration Tests

## Executive Summary

Create comprehensive end-to-end integration tests for Phase 0.1.3 variable declaration features in `src/Sharpy.Compiler.Tests/Integration/Phase013IntegrationTests.cs`. Tests will verify variable declarations with type annotations, type inference, const declarations, and error cases through the full compilation pipeline.

## Step-by-Step Implementation Approach

### Step 1: Create Test File Structure

Create `Phase013IntegrationTests.cs` following the existing pattern from `Phase012IntegrationTests.cs`:

```csharp
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for Phase 0.1.3: Variable declarations, type inference, and const.
/// </summary>
public class Phase013IntegrationTests : IntegrationTestBase
{
    public Phase013IntegrationTests(ITestOutputHelper output) : base(output)
    {
    }

    // Tests organized by regions
}
```

### Step 2: Implement Spec Example Tests

Test the exact example from the task specification:

```csharp
#region Spec Example Tests

[Fact]
public void SpecExample_VariableDeclarationsWithOperations_CompilesAndRuns()
{
    var source = @"
x: int = 10
y: int = 20
z = x + y
z += 5
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void SpecExample_ConstDeclaration_CompilesAndRuns()
{
    var source = @"const MAX: int = 100";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

#endregion
```

### Step 3: Implement Type Inference Tests

Test type inference from literal values:

```csharp
#region Type Inference Tests

[Fact]
public void TypeInference_IntegerLiteral_CompilesAndRuns()
{
    var source = @"x = 42";  // Inferred as int32
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void TypeInference_FloatLiteral_CompilesAndRuns()
{
    var source = @"y = 3.14";  // Inferred as float64
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void TypeInference_AutoKeyword_CompilesAndRuns()
{
    var source = @"
x: auto = 42
y: auto = 3.14
z: auto = ""hello""
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void TypeInference_StringLiteral_CompilesAndRuns()
{
    var source = @"s = ""hello world""";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void TypeInference_BooleanLiteral_CompilesAndRuns()
{
    var source = @"
flag = True
other = False
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

#endregion
```

### Step 4: Implement Const Tests

```csharp
#region Const Declaration Tests

[Fact]
public void Const_IntegerDeclaration_CompilesAndRuns()
{
    var source = @"const MAX: int = 100";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void Const_FloatDeclaration_CompilesAndRuns()
{
    var source = @"const PI: float = 3.14159";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void Const_StringDeclaration_CompilesAndRuns()
{
    var source = @"const NAME: str = ""Sharpy""";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void Const_BoolDeclaration_CompilesAndRuns()
{
    var source = @"const DEBUG: bool = True";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void Const_MultipleDeclarations_CompilesAndRuns()
{
    var source = @"
const MAX: int = 100
const MIN: int = 0
const PI: float = 3.14159
const NAME: str = ""App""
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void Const_UsedInExpression_CompilesAndRuns()
{
    var source = @"
const BASE: int = 10
x = BASE * 2
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

#endregion
```

### Step 5: Implement Error Case Tests

```csharp
#region Error Cases - Undefined Variable

[Fact]
public void Error_UndefinedVariable_ReportsError()
{
    var source = @"y = x";  // x not defined
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
    Assert.NotEmpty(result.CompilationErrors);
    Assert.Contains(result.CompilationErrors, e =>
        e.Contains("Undefined", StringComparison.OrdinalIgnoreCase) ||
        e.Contains("undefined", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void Error_UndefinedVariableInExpression_ReportsError()
{
    var source = @"
x: int = 10
z = x + y
";  // y not defined
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
    Assert.NotEmpty(result.CompilationErrors);
}

#endregion

#region Error Cases - Const Reassignment

[Fact]
public void Error_ConstReassignment_ReportsError()
{
    var source = @"
const MAX: int = 100
MAX = 50
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
    Assert.NotEmpty(result.CompilationErrors);
    Assert.Contains(result.CompilationErrors, e =>
        e.Contains("constant", StringComparison.OrdinalIgnoreCase) ||
        e.Contains("const", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void Error_ConstAugmentedAssignment_ReportsError()
{
    var source = @"
const MAX: int = 100
MAX += 10
";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
    Assert.NotEmpty(result.CompilationErrors);
    Assert.Contains(result.CompilationErrors, e =>
        e.Contains("constant", StringComparison.OrdinalIgnoreCase) ||
        e.Contains("const", StringComparison.OrdinalIgnoreCase));
}

#endregion

#region Error Cases - Type Mismatch

[Fact]
public void Error_TypeMismatch_IntToString_ReportsError()
{
    var source = @"x: str = 42";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
    Assert.NotEmpty(result.CompilationErrors);
    Assert.Contains(result.CompilationErrors, e =>
        e.Contains("Cannot assign", StringComparison.OrdinalIgnoreCase) ||
        e.Contains("type", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public void Error_TypeMismatch_StringToInt_ReportsError()
{
    var source = @"x: int = ""hello""";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
    Assert.NotEmpty(result.CompilationErrors);
}

[Fact]
public void Error_TypeMismatch_BoolToInt_ReportsError()
{
    var source = @"x: int = True";
    var result = CompileAndExecute(source);
    Assert.False(result.Success);
    Assert.NotEmpty(result.CompilationErrors);
}

#endregion
```

### Step 6: Implement Variable Scope and Redefinition Tests

```csharp
#region Variable Redefinition Tests

[Fact]
public void VariableRedefinition_SameType_CompilesAndRuns()
{
    var source = @"
x: int = 1
x: int = 2
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void VariableRedefinition_DifferentType_CompilesAndRuns()
{
    var source = @"
x: int = 1
x: str = ""hello""
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void VariableReassignment_AfterDeclaration_CompilesAndRuns()
{
    var source = @"
x: int = 1
x = 2
x = 3
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

#endregion
```

### Step 7: Implement Mixed Declaration Tests

```csharp
#region Mixed Declaration Tests

[Fact]
public void MixedDeclarations_VariablesAndConsts_CompilesAndRuns()
{
    var source = @"
const MAX: int = 100
x: int = 10
y = 20
z = x + y
const MIN: int = 0
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

[Fact]
public void MixedDeclarations_AllTypeAnnotations_CompilesAndRuns()
{
    var source = @"
a: int = 42
b: float = 3.14
c: str = ""hello""
d: bool = True
e = 100
f: auto = 200
";
    var result = CompileAndExecute(source);
    Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
}

#endregion
```

## Key Files to Modify

| File | Action |
|------|--------|
| `src/Sharpy.Compiler.Tests/Integration/Phase013IntegrationTests.cs` | **CREATE** - New integration test file |

## Tests to Verify

### Success Cases (should compile and run)
1. Spec example with typed variables and operations
2. Const declarations (int, float, str, bool)
3. Type inference from literals
4. Auto keyword type inference
5. Variable redefinition
6. Variable reassignment
7. Mixed declarations

### Error Cases (should fail compilation)
1. Undefined variable usage
2. Const reassignment (simple)
3. Const augmented assignment
4. Type mismatch (string to int, int to string, bool to int)

## Potential Risks and Questions

### Risks

1. **Type Inference Behavior**: The exact type inference behavior for simple assignments (`x = 42`) vs typed declarations (`x: int = 42`) vs auto (`x: auto = 42`) needs verification. Test should document actual behavior.

2. **Variable Redefinition with Different Types**: Sharpy allows Python-like redefinition where a variable can be redefined with a different type. This generates versioned C# variables (`x`, `x_1`, `x_2`). Tests should verify this works correctly.

3. **Module-Level vs Function-Level Scope**: All tests in Phase 0.1.2 are at module level. Need to verify the same behavior works inside functions if required.

4. **Const in Expressions**: Using const values in expressions should work (e.g., `x = MAX * 2`), but need to verify code generation handles this correctly.

### Questions

1. **Should `x = 42` (without type annotation) work at module level?**
   - Based on Phase012 tests, it appears to work (`MinimalProgram_SimpleAssignment_CompilesAndRuns`)
   - Need to verify if this is considered a declaration or reassignment

2. **What happens with `const X: auto = 42`?**
   - Should this infer the type, or is `auto` not allowed with `const`?
   - Current parser appears to require explicit type for const

3. **Are there any implicit type conversions?**
   - e.g., `x: float = 42` (int literal to float)
   - This may succeed or fail depending on implementation

## Test Count Summary

| Category | Count |
|----------|-------|
| Spec Examples | 2 |
| Type Inference | 5 |
| Const Declarations | 6 |
| Error - Undefined Variable | 2 |
| Error - Const Reassignment | 2 |
| Error - Type Mismatch | 3 |
| Variable Redefinition | 3 |
| Mixed Declarations | 2 |
| **Total** | **~25 tests** |
