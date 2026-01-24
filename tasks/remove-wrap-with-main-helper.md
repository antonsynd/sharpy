# Task: Remove WrapWithMainIfNeeded Helper

## Goal
Remove the brittle `WrapWithMainIfNeeded` helper and ensure all Sharpy test code explicitly includes proper `main()` functions where required.

## Phase 1: Infrastructure Changes (do last, after all tests fixed)

- [ ] `src/Sharpy.Compiler.Tests/Integration/IntegrationTestBase.cs:53` - Remove `WrapWithMainIfNeeded` call
- [ ] `src/Sharpy.Compiler.Tests/Helpers/ProjectCompilationHelper.cs:126` - Remove `WrapWithMainIfNeeded` call
- [ ] `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs:17,46` - Remove `wrapInFunction` parameter and calls
- [ ] `src/Sharpy.Compiler.Tests/TestHelpers.cs` - Delete `WrapWithMainIfNeeded` method (lines 20-127)

---

## Phase 2: Integration Test Files

### BasicProgramTests.cs (~19 methods)
- [ ] HelloWorld_PrintsCorrectly - print only
- [ ] Fibonacci_Recursive_ComputesCorrectly - function + calls
- [ ] Fibonacci_Iterative_ComputesCorrectly - function + calls
- [ ] SimpleArithmetic_WorksCorrectly - vars + prints
- [ ] TypeInference_WorksCorrectly - vars + prints
- [ ] VariableAssignment_WorksCorrectly - vars + prints
- [ ] AugmentedAssignment_WorksCorrectly - vars + augmented ops
- [ ] Comments_AreIgnored - vars + prints
- [ ] MultipleStatements_ExecuteInOrder - prints
- [ ] FunctionWithIfBlockAndAssignment_NoReturn_CompilesAndExecutes - function + calls
- [ ] FunctionWithIfElseBlocksAndAssignments_NoReturn_CompilesAndExecutes - function + calls
- [ ] FunctionWithNestedIfAndAssignment_NoReturn_CompilesAndExecutes - function + calls
- [ ] FunctionWithIfBlockWithInlineComment_CompilesAndExecutes - function + calls
- [ ] FunctionWithMultipleIfBlocksAndComments_CompilesAndExecutes - function + calls
- [ ] FunctionWithWhileLoopAndComments_CompilesAndExecutes - function + calls
- [ ] FunctionWithForLoopAndComments_CompilesAndExecutes - function + calls
- [ ] FunctionWithComplexNestedStructuresAndComments_CompilesAndExecutes - function + calls
- [ ] FunctionWithIfBlockAndMultipleAssignments_NoReturn_CompilesAndExecutes - function + calls

### FunctionTests.cs (~5 methods)
- [ ] SimpleFunction_WithReturn_WorksCorrectly - function + calls
- [ ] VoidFunction_PrintsCorrectly - function + calls
- [ ] RecursiveFunction_Factorial_WorksCorrectly - function + calls
- [ ] FunctionWithDefaultParameter_WorksCorrectly - function + calls
- [ ] MultipleFunctions_CallEachOther_WorksCorrectly - function + calls

### ControlFlowTests.cs (~39 methods)
- [ ] IfStatement_SimpleCondition_WorksCorrectly
- [ ] IfStatement_WithElse_WorksCorrectly
- [ ] IfStatement_WithElif_WorksCorrectly
- [ ] IfStatement_NestedConditions_WorksCorrectly
- [ ] WhileLoop_SimpleCount_WorksCorrectly
- [ ] WhileLoop_WithBreak_WorksCorrectly
- [ ] WhileLoop_WithContinue_WorksCorrectly
- [ ] ForLoop_WithRange_WorksCorrectly
- [ ] ForLoop_WithList_WorksCorrectly
- [ ] ForLoop_WithBreak_WorksCorrectly
- [ ] ForLoop_WithContinue_WorksCorrectly
- [ ] ForLoop_NestedLoops_WorksCorrectly
- [ ] (review file for complete list - ~25+ more methods)

### PipeOperatorTests.cs (~9 methods)
- [ ] PipeOperator_SinglePipe_WorksCorrectly
- [ ] PipeOperator_ChainedPipes_WorksCorrectly
- [ ] PipeOperator_WithLambda_WorksCorrectly
- [ ] (review file for complete list)

### DivisionDeviationTests.cs (~10 methods)
- [ ] IntegerDivision_TruncatesTowardZero_WorksCorrectly
- [ ] FloorDivision_TruncatesTowardNegativeInfinity_WorksCorrectly
- [ ] (review file for complete list)

### VariableAssignmentNegativeTests.cs (~16 methods)
- [ ] (review file for complete list)

### Phase012IntegrationTests.cs (~46 methods)
- [ ] (review file for complete list - minimal program tests)

### Phase013IntegrationTests.cs (~54 methods)
- [ ] (review file for complete list - variable declarations, type inference)

### Phase014IntegrationTests.cs (~38 methods)
- [ ] (review file for complete list)

### Phase015IntegrationTests.cs (~52 methods)
- [ ] (review file for complete list - spec examples, recursive functions)

### Phase016IntegrationTests.cs (~39 methods)
- [ ] (review file for complete list - classes and methods)

### Phase017IntegrationTests.cs (~42 methods)
- [ ] (review file for complete list)

### Phase018IntegrationTests.cs (~45 methods)
- [ ] (review file for complete list)

### Phase019IntegrationTests.cs (~31 methods)
- [ ] (review file for complete list)

### CrossModuleInheritanceTests.cs (~1 method)
- [ ] (review file)

### DependencyGraphIntegrationTests.cs
- [ ] (review file - uses ProjectCompilationHelper)

---

## Phase 3: File-Based Test Fixtures

Check `.spy` files in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`:
- [ ] Review all `.spy` entry point test files for explicit `main()`

---

## Phase 4: TypeCheckerTests.cs

Tests using `wrapInFunction: false` need special handling:
- [ ] Review which tests need `isEntryPoint: false` vs explicit `main()`

---

## How to Update Each Test

**Pattern: Executable code only**
```csharp
// Before
var source = @"
x = 42
print(x)
";

// After
var source = @"
def main():
    x = 42
    print(x)
";
```

**Pattern: Function definitions + executable code**
```csharp
// Before
var source = @"
def helper() -> int:
    return 42

result = helper()
print(result)
";

// After
var source = @"
def helper() -> int:
    return 42

def main():
    result = helper()
    print(result)
";
```

**Pattern: Class definitions + executable code**
```csharp
// Before
var source = @"
class Foo:
    x: int = 0

f = Foo()
print(f.x)
";

// After
var source = @"
class Foo:
    x: int = 0

def main():
    f = Foo()
    print(f.x)
";
```

---

## Verification

After completing all changes:
1. `dotnet build` - should compile
2. `dotnet test` - all tests should pass
3. `grep -r "WrapWithMainIfNeeded" src/` - should return no results
