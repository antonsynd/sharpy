# Dogfood Follow-up Tasks

This document tracks remaining issues discovered during the dogfood investigation.

## Completed (2026-01-17)

### Compiler Fixes
- [x] **Issue 0000**: Loop variable scoping in generated C# - Fixed by clearing `_declaredVariables`, `_variableVersions`, `_constVariables` at start of `GenerateConstructor()` and `GenerateClassMethod()`
- [x] **Issue 0001 (partial)**: Inherited field casing - Fixed by using `ToPascalCase` for inherited field access in constructors
- [x] **Issue 0006**: Enum code generation - Fixed by the same scope-clearing changes

### Test Infrastructure
- [x] Multi-file test support for `FileBasedIntegrationTests` - Added `CompileAndExecuteProject()` method and updated test discovery

---

## Remaining Compiler Issues

### 1. Abstract class interface implementation (Issue 0001)
**File**: `dogfood_output/issues/20260117_094358_compilation_failed_0001/source.spy`

**Problem**: When an abstract class declares it implements an interface but doesn't provide all interface methods (expecting subclasses to implement them), the generated C# fails to compile.

**Example**:
```sharpy
interface IDisplayable:
    def display(self) -> None: ...

@abstract
class Shape(IDisplayable):
    # Missing display() method - expects subclass to implement
    def area(self) -> int: ...
```

**Error**: `'Shape' does not implement interface member 'IDisplayable.Display()'`

**Fix needed**: In `RoslynEmitter`, when generating an abstract class that implements interfaces:
1. Collect all interface methods the class declares it implements
2. Check which methods are NOT defined in the class body
3. For each missing method, generate an abstract method stub

**Priority**: Medium - affects inheritance patterns with interfaces

---

### 2. ProjectCompiler cross-file import code generation
**File**: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/imports/simple_import_test/`

**Problem**: The `ProjectCompiler` correctly parses and analyzes multi-file projects with `from X import Y`, but the generated C# code doesn't properly connect the imports across files.

**Example**:
```sharpy
# main.spy
from math_utils import square, multiply_by_two
print(square(5))

# math_utils.spy
def square(n: int) -> int:
    return n * n
```

**Error**: `The type or namespace name 'MathUtils' does not exist in the namespace 'Sharpy.Test'`

**Root cause**: The code generator emits:
- `main.cs` with `using Sharpy.Test.MathUtils;` and calls to `Square()`
- `math_utils.cs` with a `MathUtils` class in `Sharpy.Test.MathUtils` namespace

But when compiling, the namespace/class structure isn't being resolved correctly.

**Fix needed**: Investigate `ProjectCompiler` code generation to ensure:
1. Each module's C# class is in the correct namespace
2. Import statements generate correct `using` directives
3. Imported symbols are correctly qualified

**Priority**: High - blocks multi-file project compilation

---

### 3. Interface method return type inference (Issue 0004)
**File**: `dogfood_output/issues/20260117_094625_execution_failed_0004/source.spy`

**Problem**: When calling a method through an interface type, the return type isn't properly inferred for augmented assignment.

**Example**:
```sharpy
interface ICalculator:
    def calculate(self, x: int) -> int: ...

def run_calculator(calc: ICalculator, value: int) -> int:
    return calc.calculate(value)

total = 0
total += run_calculator(proc, 1)  # Error: Type '<?>' does not support '+='
```

**Error**: `Type 'int' does not support augmented assignment operator '+=' with right operand of type '<?>'`

**Root cause**: The type checker isn't properly resolving the return type of interface method calls.

**Priority**: Medium - affects interface-based polymorphism

---

## Source Code Issues (Not Compiler Bugs)

These dogfood examples have issues in the generated Sharpy code itself, not the compiler:

### Issues 0003, 0005: `main()` function with explicit call
The dogfood generator created code that both:
1. Defines a `main()` function, AND
2. Calls `main()` at module level

This violates the language rule that you can't have module-level executable statements when a `main` function is defined.

**Resolution**: These are invalid Sharpy programs. The dogfood generator should be updated to either:
- Use module-level code without `main()`, OR
- Define `main()` without calling it (it's auto-invoked)

### Issues 0002, 0004, 0007: Missing module dependencies
These examples import modules (`math_utils`, `geometry`, `colors`) that were expected to exist from previous dogfood generation sessions but don't.

**Resolution**: These are incomplete test cases. Either:
- Create the missing modules, OR
- Remove the import dependencies

---

## Test Infrastructure Notes

The multi-file test support is in place with a sample error test. Once the ProjectCompiler issue (#2 above) is fixed:

1. Update `simple_import_test/main.error` → `main.expected` with actual expected output
2. Add more multi-file test cases for:
   - `import module` (whole module import)
   - `from module import *` (star import)
   - Circular imports (should error)
   - Missing module imports (should error)
   - Re-exports
