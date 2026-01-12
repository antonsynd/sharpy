# Implementation Plan: Task 0.1.3.4 - Implement `const` Declarations

## Executive Summary

**Good news**: The `const` declaration feature is **already substantially implemented** across all compilation phases. This task is primarily a **verification and gap-filling task** rather than a full implementation.

## Current Implementation Status

### ✅ Lexer (Fully Implemented)
- **File**: `src/Sharpy.Compiler/Lexer/Token.cs` (line 56)
  - `TokenType.Const` is defined
- **File**: `src/Sharpy.Compiler/Lexer/Lexer.cs` (line 66)
  - `"const"` keyword maps to `TokenType.Const`

### ✅ Parser (Fully Implemented)
- **File**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs` (lines 48-54)
  - `VariableDeclaration` has `IsConst` property
- **File**: `src/Sharpy.Compiler/Parser/Parser.cs`
  - Line 103: `TokenType.Const => ParseConstDeclaration()`
  - Lines 650-674: `ParseConstDeclaration()` method
  - Syntax: `const NAME: TYPE = VALUE`
  - Requires explicit type annotation
  - Requires initializer (mandatory)

### ✅ Semantic Analysis (Fully Implemented)
- **File**: `src/Sharpy.Compiler/Semantic/Symbol.cs` (line 24)
  - `VariableSymbol.IsConstant` property tracks const-ness
- **File**: `src/Sharpy.Compiler/Semantic/Scope.cs` (lines 24-25)
  - Prevents redefinition of constants
- **File**: `src/Sharpy.Compiler/Semantic/NameResolver.cs` (lines 86, 414)
  - Pre-declares module-level constants
  - Sets `IsConstant = true` for const declarations
- **File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
  - Lines 444-458: Blocks simple assignment to constants (`PI = 3`)
  - Lines 490-500: Blocks augmented assignment to constants (`PI += 1`)
  - Lines 578-593: Creates `VariableSymbol` with `IsConstant = true`
  - Lines 605-610: Prevents redefinition of existing constants

### ✅ Code Generation (Fully Implemented)
- **File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
  - Lines 1140-1143: Field const → `public const TYPE NAME`
  - Lines 1488-1490: Name mangling for const (uses `ToConstantCase`)
  - Lines 1521-1523: Local const → `const TYPE name`
- **File**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`
  - `ToConstantCase()` method for const name handling

## Identified Gaps

### Gap 1: Missing Integration Tests
No end-to-end tests in `BasicProgramTests.cs` verify const declarations compile and run correctly.

### Gap 2: Missing Negative Tests
No integration tests verify that reassignment errors are caught:
- `const X: int = 10; X = 20` should fail
- `const X: int = 10; X += 5` should fail

### Gap 3: Missing Field Const Tests
No tests verify module-level const fields generate correct C# (`public const int MAX = 100;`).

### Gap 4: Missing Type Inference Tests
Need to verify `const` with `auto` type annotation works (if supported).

## Implementation Steps

### Step 1: Verify Lexer (Already Done)
**Files**: `src/Sharpy.Compiler/Lexer/Token.cs`, `src/Sharpy.Compiler/Lexer/Lexer.cs`
**Status**: ✅ Complete - `const` tokenizes to `TokenType.Const`

### Step 2: Verify Parser (Already Done)
**Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Statement.cs`
**Status**: ✅ Complete - `const PI: float = 3.14159` parses correctly

### Step 3: Add Parser Test for Float Const
**File**: `src/Sharpy.Compiler.Tests/Parser/ParserTests.cs`
**Action**: Add test for `const PI: float = 3.14159` specifically

```csharp
[Fact]
public void ParseConstDeclaration_WithFloatType()
{
    var module = Parse("const PI: float = 3.14159");
    var constDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
    constDecl.Name.Should().Be("PI");
    constDecl.IsConst.Should().BeTrue();
    constDecl.Type.Name.Should().Be("float");
    constDecl.InitialValue.Should().BeOfType<FloatLiteral>();
}
```

### Step 4: Verify Semantic Analysis (Already Done)
**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
**Status**: ✅ Complete - const-ness tracked, reassignment blocked

### Step 5: Add Semantic Test for Simple Reassignment
**File**: `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs`
**Action**: Add test for `const X: int = 10; X = 20` error

```csharp
[Fact]
public void SimpleAssignment_ToConstant_ReportsError()
{
    var source = @"
const X: int = 10
X = 20
";
    var (_, typeChecker) = AnalyzeSource(source);
    typeChecker.Errors.Should().ContainSingle();
    typeChecker.Errors[0].Message.Should().Contain("constant");
}
```

### Step 6: Verify Code Generation (Already Done)
**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Status**: ✅ Complete - emits C# `const` for compile-time constants

### Step 7: Add Integration Test for Const Usage
**File**: `src/Sharpy.Compiler.Tests/Integration/BasicProgramTests.cs`
**Action**: Add end-to-end test for const declaration and usage

```csharp
[Fact]
public void ConstDeclaration_CompilesAndRuns()
{
    var source = @"
const PI: float = 3.14159
const MAX: int = 100
print(PI)
print(MAX)
";
    var output = CompileAndRun(source);
    output.Should().Contain("3.14159");
    output.Should().Contain("100");
}
```

### Step 8: Add Integration Test for Const Reassignment Error
**File**: `src/Sharpy.Compiler.Tests/Integration/VariableAssignmentNegativeTests.cs`
**Action**: Add test that const reassignment produces compile error

```csharp
[Fact]
public void ConstReassignment_ProducesCompileError()
{
    var source = @"
const X: int = 10
X = 20
";
    var exception = Assert.Throws<CompilationException>(() => CompileAndRun(source));
    exception.Message.Should().Contain("constant");
}
```

### Step 9: Add CodeGen Test for Module-Level Const Field
**File**: `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterStatementTests.cs` or `RoslynEmitterModuleTests.cs`
**Action**: Verify module-level const generates `public const`

```csharp
[Fact]
public void GenerateField_ConstDeclaration_GeneratesPublicConstField()
{
    // Test that module-level const generates: public const int MAX = 100;
}
```

### Step 10: Document Const Semantics
**File**: Documentation or code comments
**Action**: Document:
- Syntax: `const NAME: TYPE = VALUE`
- Type annotation required
- Initializer required
- Compile-time constant (must be literal or constant expression)
- Cannot be reassigned
- Cannot use augmented assignment

## Key Files to Modify

| File | Action |
|------|--------|
| `src/Sharpy.Compiler.Tests/Parser/ParserTests.cs` | Add float const parse test |
| `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs` | Add simple reassignment error test |
| `src/Sharpy.Compiler.Tests/Integration/BasicProgramTests.cs` | Add const end-to-end test |
| `src/Sharpy.Compiler.Tests/Integration/VariableAssignmentNegativeTests.cs` | Add const reassignment error test |
| `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterStatementTests.cs` | Add field const test |

## Tests to Verify

### Existing Tests (Should Pass)
- `ParserTests.ParseConstDeclaration` - Parses `const MAX: int = 100`
- `ParserTests.ParseConstWithExplicitType` - Parses const with explicit type
- `TypeCheckerTests.AugmentedAssignment_ToConstant_ReportsError` - Blocks `X += 5` on const
- `RoslynEmitterStatementTests.GenerateStatement_ConstDeclaration_GeneratesConstLocalDeclaration` - Generates `const int`

### New Tests to Add
1. `ParserTests.ParseConstDeclaration_WithFloatType` - Parse `const PI: float = 3.14159`
2. `TypeCheckerTests.SimpleAssignment_ToConstant_ReportsError` - Block `X = 20` on const
3. `BasicProgramTests.ConstDeclaration_CompilesAndRuns` - E2E test
4. `VariableAssignmentNegativeTests.ConstReassignment_ProducesCompileError` - E2E error test
5. `RoslynEmitterTests.GenerateField_ConstDeclaration_GeneratesPublicConstField` - Module-level const

## Potential Risks and Questions

### Risks

1. **Non-literal initializers**: C# `const` requires compile-time constant expressions. If Sharpy allows `const X: int = some_function()`, code generation will fail. Need to either:
   - Restrict to literals only in semantic analysis
   - Use `static readonly` for non-literal consts

2. **String const**: C# allows `const string`, but need to verify the type mapper handles this correctly.

3. **Const in classes**: If const is defined inside a class body, it should generate a `const` field. Need to verify this works.

### Questions to Clarify

1. **Should `const` support `auto` type inference?**
   - Current parser requires explicit type annotation
   - Python-style: `PI = 3.14159` (inferred as const if never reassigned) - NOT supported
   - Decision: Keep explicit type requirement for clarity

2. **Should `const` support complex expressions?**
   - Python allows `const X = 2 + 3`
   - C# `const` requires compile-time evaluation
   - Decision: Support only literal values initially, document limitation

3. **Should const names be UPPER_CASE by convention?**
   - Currently: No enforcement, but `NameMangler.ToConstantCase()` handles it in codegen
   - Decision: Convention, not enforced

## Summary

| Action Item | Status | Priority |
|-------------|--------|----------|
| Lexer tokenizes `const` | ✅ Done | - |
| Parser parses `const` declarations | ✅ Done | - |
| Semantic tracks const-ness | ✅ Done | - |
| Semantic blocks reassignment | ✅ Done | - |
| CodeGen emits C# `const` | ✅ Done | - |
| Add parser float const test | 🔲 Todo | Low |
| Add simple reassignment error test | 🔲 Todo | Medium |
| Add E2E integration test | 🔲 Todo | High |
| Add E2E error test | 🔲 Todo | High |
| Add field const codegen test | 🔲 Todo | Medium |

**Estimated Effort**: Small - primarily adding tests to verify existing functionality.
