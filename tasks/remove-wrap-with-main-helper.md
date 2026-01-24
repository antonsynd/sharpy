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

### BasicProgramTests.cs (~19 methods) ✅ COMPLETED
- [x] HelloWorld_PrintsCorrectly - print only
- [x] Fibonacci_Recursive_ComputesCorrectly - function + calls
- [x] Fibonacci_Iterative_ComputesCorrectly - function + calls
- [x] SimpleArithmetic_WorksCorrectly - vars + prints
- [x] TypeInference_WorksCorrectly - vars + prints
- [x] VariableAssignment_WorksCorrectly - vars + prints
- [x] AugmentedAssignment_WorksCorrectly - vars + augmented ops
- [x] Comments_AreIgnored - vars + prints
- [x] MultipleStatements_ExecuteInOrder - prints
- [x] FunctionWithIfBlockAndAssignment_NoReturn_CompilesAndExecutes - function + calls
- [x] FunctionWithIfElseBlocksAndAssignments_NoReturn_CompilesAndExecutes - function + calls
- [x] FunctionWithNestedIfAndAssignment_NoReturn_CompilesAndExecutes - function + calls
- [x] FunctionWithIfBlockWithInlineComment_CompilesAndExecutes - function + calls
- [x] FunctionWithMultipleIfBlocksAndComments_CompilesAndExecutes - function + calls
- [x] FunctionWithWhileLoopAndComments_CompilesAndExecutes - function + calls
- [x] FunctionWithForLoopAndComments_CompilesAndExecutes - function + calls
- [x] FunctionWithComplexNestedStructuresAndComments_CompilesAndExecutes - function + calls
- [x] FunctionWithIfBlockAndMultipleAssignments_NoReturn_CompilesAndExecutes - function + calls

### FunctionTests.cs (~5 methods) ✅ COMPLETED
- [x] SimpleFunction_WithReturn_WorksCorrectly - function + calls
- [x] VoidFunction_PrintsCorrectly - function + calls
- [x] RecursiveFunction_Factorial_WorksCorrectly - function + calls
- [x] FunctionWithDefaultParameter_WorksCorrectly - function + calls
- [x] MultipleFunctions_CallEachOther_WorksCorrectly - function + calls

### ControlFlowTests.cs (~39 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### PipeOperatorTests.cs (~9 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### DivisionDeviationTests.cs - FILE DOES NOT EXIST (task list was incorrect)

### VariableAssignmentNegativeTests.cs (~16 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

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
