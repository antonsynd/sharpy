# Task List: Phase 0.1.10 Code Generation Fixes

**Goal:** Fix the remaining 27 test failures in the Sharpy compiler related to code generation for modules, imports, and variable redefinitions.

---

## Phase 0.1.10: Code Generation Fixes

### Overview

After fixing the local variable shadowing issues, there remain three categories of test failures:

| Category | Test Count | Root Cause |
|----------|------------|------------|
| Module Import Alias Generation | 3 | Using wrong class name for Sharpy modules (uses last segment instead of `Exports`) |
| Variable Redefinition at Module Level | 6 | Generates duplicate field declarations instead of versioned names |
| Nested Package Namespace Generation | 18 | Namespace path duplication for `__init__.spy` files in packages |

---

### Task 0.1.10.CG1: Fix Module Import Alias to Use `Exports` Class

**Files:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (lines 261-347 `GenerateImportUsings`)

**Problem:**
When importing a Sharpy module (not a .NET namespace), the generated using alias points to the wrong class:
- Current: `using utils_helpers = Utils.Helpers.Helpers;`
- Expected: `using utils_helpers = Utils.Helpers.Exports;`

The current code extracts the last segment of the namespace (`Helpers`) as the class name, but Sharpy modules expose their members via a class named `Exports`.

**Fix Approach:**
1. In `GenerateImportUsings()`, when generating the alias for non-.NET Sharpy modules:
   - Change `moduleClassName` from the last namespace segment to the literal `"Exports"`
   - This applies to both `import module` (line 320-340) and `import module as alias` (line 284-304)

2. Similarly in `GenerateFromImportUsings()` (line 350-398):
   - The `using static` should point to `Exports` class, not the module name

**Test Commands:**
```bash
dotnet test --filter "ConvertModuleNameToNamespace_SnakeCase|GenerateCompilationUnit_WithImportModule"
```

**Expected Generated Code:**
```csharp
// For: import utils.helpers
using utils_helpers = Utils.Helpers.Exports;

// For: import utils.helpers as h
using h = Utils.Helpers.Exports;

// For: from config import MAX_SIZE
using static TestProject.Config.Exports;
```

---

### Task 0.1.10.CG2: Track Module-Level Variable Redefinitions

**Files:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (lines 495-622 `GenerateModuleClass`, lines 2164-2259 `GenerateModuleLevelField`)

**Problem:**
Sharpy allows variable redefinition at module level that changes the type:
```python
x: int = 1
x: auto = "hello"  # Redefines x as string
```

Currently generates invalid C# with duplicate field declarations:
```csharp
public static int X = 1;
public static string X = "hello";  // CS0102: Duplicate definition
```

**Fix Approach:**
1. Add `_moduleVariableVersions` dictionary (similar to `_variableVersions` for locals):
   ```csharp
   private readonly Dictionary<string, int> _moduleVariableVersions = new();
   ```

2. In `GenerateModuleLevelField()`:
   - Check if the variable name already exists in `_moduleVariableVersions`
   - If so, generate a versioned name: `X_1`, `X_2`, etc.
   - Track the current version so later code references resolve correctly

3. In `GenerateModuleClass()`:
   - Pre-scan all variable declarations to build the version map BEFORE generating fields
   - This ensures the Main() method can reference the correct versioned variable

4. Update `GetMangledVariableName()`:
   - When resolving module-level variable references, consult `_moduleVariableVersions`
   - Return the highest version for the current reference point

**Expected Generated Code:**
```csharp
public static int X = 1;
public static string X_1 = "hello";

public static void Main()
{
    // References before redefinition use X
    // References after redefinition use X_1
}
```

**Test Commands:**
```bash
dotnet test --filter "VariableRedefinition|Variable_FirstAssignment|Assignment_Reference"
```

**Complexity Notes:**
- This requires tracking statement order to determine which version to reference
- Consider adding a "current statement index" context to resolve references correctly
- The Main() method generation happens after field generation, so version tracking must persist

---

### Task 0.1.10.CG3: Fix Nested Package Namespace Generation

**Files:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (lines 193-220 `GenerateProjectNamespace`)
- `src/Sharpy.Compiler/ProjectCompiler.cs` (namespace generation logic)

**Problem:**
For deeply nested packages like `level1/level2/level3/__init__.spy`, the namespace is generated incorrectly:
- Current: `TestProject.Level1.Level2.Level3.Level3` (duplicated segment)
- Expected: `TestProject.Level1.Level2.Level3.Init`

The import alias also has issues:
- Current: `using level1_level2_level3 = TestProject.Level1.Level2.Level3.Level3;`
- Expected: `using level1_level2_level3 = TestProject.Level1.Level2.Level3.Exports;`

**Root Cause Analysis:**
1. `GenerateProjectNamespace()` adds directory parts AND the filename
2. For `__init__.spy` files, the filename becomes `Init`
3. The import generates using the last directory segment as the class name

**Fix Approach:**
1. In `GenerateProjectNamespace()`:
   - When the file is `__init__.spy`, use `Init` as the class name (already correct)
   - Verify the directory path doesn't include the file's containing directory twice

2. In `GenerateImportUsings()`:
   - For package imports (paths ending in `__init__.spy`), the class name should be `Exports` (same as Task CG1)
   - The namespace path should NOT duplicate the last segment

3. Debug by logging the intermediate values:
   - `relativePath`, `relativeDir`, `fileName`
   - Compare expected vs actual for multi-level paths

**Example Structure:**
```
src/
  main.spy              → TestProject.Main
  level1/
    __init__.spy        → TestProject.Level1.Init
    level2/
      __init__.spy      → TestProject.Level1.Level2.Init
      level3/
        __init__.spy    → TestProject.Level1.Level2.Level3.Init
```

**Test Commands:**
```bash
dotnet test --filter "EdgeCase_DeepNesting|PackageInit_NestedPackages|ComplexScenario_Nested"
```

---

### Task 0.1.10.CG4: Fix `__init__.spy` Class Name for Packages

**Files:**
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (lines 625-655 `GetModuleClassName`)

**Problem:**
Package `__init__.spy` files generate a class named `Init`, but the import system expects `Exports`:
- Generated: `namespace TestProject.Mypackage { public static class Init { ... } }`
- Import expects: `using mypackage = TestProject.Mypackage.Exports;`

**Fix Approach:**
1. In `GetModuleClassName()`:
   - If the filename is `__init__`, return `"Exports"` instead of `"Init"`
   - This makes package exports consistent with the import alias generation

2. Alternative: Change import generation to use `Init` for packages
   - Less preferred because `Exports` is a clearer semantic name

**Test Commands:**
```bash
dotnet test --filter "PackageInit"
```

---

### Task 0.1.10.CG5: Handle Re-export Syntax in `__init__.spy`

**Files:**
- `src/Sharpy.Compiler/Parser/Parser.cs` (import/export parsing)
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (re-export code generation)

**Problem:**
The test `PackageInit_WithReExports_ExportsModuleMembers` fails with parser errors:
```
Parser error at line 3, column 6: Expected identifier, got Dot
```

This suggests the re-export syntax in `__init__.spy` is not being parsed correctly. The syntax likely involves:
```python
from .basic import BasicOperation
from .advanced import AdvancedOperation
```

**Fix Approach:**
1. Check if relative imports (starting with `.`) are supported in the parser
2. Ensure the lexer/parser handles dotted imports correctly
3. Generate appropriate `using static` or re-export statements

**Test Commands:**
```bash
dotnet test --filter "PackageInit_WithReExports|ComplexScenario_PackageWithMultipleModules"
```

---

## Summary

| Task ID | Title | Estimated Complexity |
|---------|-------|---------------------|
| 0.1.10.CG1 | Fix Module Import Alias to Use `Exports` Class | Low |
| 0.1.10.CG2 | Track Module-Level Variable Redefinitions | High |
| 0.1.10.CG3 | Fix Nested Package Namespace Generation | Medium |
| 0.1.10.CG4 | Fix `__init__.spy` Class Name for Packages | Low |
| 0.1.10.CG5 | Handle Re-export Syntax in `__init__.spy` | Medium |

**Recommended Order:**
1. CG1 (quick win, unblocks many tests)
2. CG4 (quick win, related to CG1)
3. CG3 (after CG1/CG4 are fixed, easier to debug)
4. CG5 (depends on CG3 for package namespace fixes)
5. CG2 (most complex, can be done in parallel)

**Full Test Command:**
```bash
dotnet test
```

Current status: 27 failing, 2994 passing, 82 skipped
Target: 0 failing (excluding legitimately skipped tests)
