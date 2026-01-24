# Task: Remove WrapWithMainIfNeeded Helper

## Goal
Remove the brittle `WrapWithMainIfNeeded` helper and ensure all Sharpy test code explicitly includes proper `main()` functions where required.

## Phase 1: Infrastructure Changes ✅ COMPLETED

- [x] `src/Sharpy.Compiler.Tests/Integration/IntegrationTestBase.cs:53` - Removed `WrapWithMainIfNeeded` call
- [x] `src/Sharpy.Compiler.Tests/Helpers/ProjectCompilationHelper.cs:126` - Removed `WrapWithMainIfNeeded` call
- [x] `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs:17,46` - Removed `wrapInFunction` parameter and calls
- [x] `src/Sharpy.Compiler.Tests/TestHelpers.cs` - Deleted `WrapWithMainIfNeeded` method

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

### DivisionDeviationTests.cs ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### VariableAssignmentNegativeTests.cs (~16 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### Phase012IntegrationTests.cs (~46 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions
- [x] Edge case tests (empty/whitespace files) updated to expect failure without main()

### Phase013IntegrationTests.cs (~54 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### Phase014IntegrationTests.cs (~38 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### Phase015IntegrationTests.cs (~52 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### Phase016IntegrationTests.cs (~39 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### Phase017IntegrationTests.cs (~42 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### Phase018IntegrationTests.cs (~45 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### Phase019IntegrationTests.cs (~31 methods) ✅ COMPLETED
- [x] All tests updated with explicit main() functions

### CrossModuleInheritanceTests.cs ✅ COMPLETED
- [x] Uses ProjectCompiler - inline test files already have main() functions
- [x] Fixed ClassInheritance_FromNetBaseClass_Works test to include main()

### DependencyGraphIntegrationTests.cs ✅ COMPLETED
- [x] Uses ProjectCompiler - inline test files already have main() functions

---

## Phase 3: File-Based Test Fixtures ✅ COMPLETED

Check `.spy` files in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`:
- [x] Review all `.spy` entry point test files for explicit `main()` - all already have main()

---

## Phase 4: TypeCheckerTests.cs ✅ COMPLETED

Tests using `wrapInFunction: false` need special handling:
- [x] Removed wrapInFunction parameter from CompileAndCheck helper methods
- [x] Tests with executable statements now have explicit main() functions
- [x] All tests use isEntryPoint: false (semantic tests don't need execution)

### Phase0110IntegrationTests.cs ✅ COMPLETED
- [x] All tests updated with explicit main() functions in entry point files

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
