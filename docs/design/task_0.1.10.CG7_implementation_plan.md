# Task 0.1.10.CG7: Update Integration Tests - Implementation Plan

## Executive Summary

**Current Status:** 29 tests failing, 3 tests passing out of 32 total
**Root Cause:** Code generation issues in RoslynEmitter.cs - the module Exports class pattern is not being properly generated or referenced

## Test Results Analysis

### Passing Tests (3)
1. `BasicImport_CircularImport_ReportsError` - Correctly detects circular imports
2. `BasicImport_ModuleNotFound_ReportsError` - Correctly detects missing modules
3. `MultiFile_TypeMismatchAcrossModules_ReportsError` - Correctly detects type mismatches

### Failing Tests (29) - Categorized by Root Cause

#### Category A: Empty/Comment-Only Module Code Generation (2 tests)
**Issue:** Modules with no declarations don't generate any C# code, causing "namespace not found" errors

- `EdgeCase_EmptyModule_CompilesSuccessfully`
  - Error: `The type or namespace name 'Empty' could not be found`
- `EdgeCase_ModuleWithOnlyComments_CompilesSuccessfully`
  - Error: `The type or namespace name 'Comments' could not be found`

**Fix Required:** Generate empty Exports class even for empty modules

#### Category B: Module Variable Access Code Generation (4 tests)
**Issue:** Module-level variables (consts) aren't being properly exposed in Exports class

- `BasicImport_ImportVariable_Works`
  - Error: `MaxSize does not exist in namespace Config`
- `PackageInit_WithVariables_DefinesPackageLevelVariables`
  - Error: Variable access fails
- `ComplexScenario_NestedPackagesWithImports_Works`
  - Error: `CONFIG` variable access fails
- `ComplexScenario_ProjectWithPackagesAndModules_CompilesCorrectly`
  - Error: `DEBUG` variable access fails

**Fix Required:** Ensure module-level consts/variables are in Exports class

#### Category C: Main Method Generation (Multiple tests)
**Issue:** Entry point files not generating Main() method, or Main() method being generated in wrong class

- `ProjectFile_CustomSourceDirectory_FindsSourceFiles`
  - Error: `Program does not contain a static 'Main' method`
- Multiple other tests have this as secondary error

**Fix Required:** Verify entry point detection and Main() generation

#### Category D: Using Directive / Namespace Resolution (Many tests)
**Issue:** Using directives reference wrong namespace path (missing `.Exports` suffix or wrong path)

- `BasicImport_SimpleModule_Works`
- `BasicImport_ImportFromSubdirectory_Works`
- `BasicImport_MultipleImports_Works`
- `PackageInit_EmptyInit_MarksAsPackage`
- `MultiFile_*` tests
- `ComplexScenario_*` tests
- `EdgeCase_ImportSameName_FromDifferentPackages_Works`
- `EdgeCase_DeepNesting_Works`

**Typical Error Pattern:**
```
main.cs(7,26): error CS0246: The type or namespace name 'Mypackage' could not be found
main.cs(15,26): error CS0234: The type or namespace name 'Helper' does not exist in namespace 'TestProject.Mypackage.Module'
```

**Fix Required:** Using directives must point to `Namespace.Module.Exports`

#### Category E: Relative Import Parsing (.module imports) (2+ tests)
**Issue:** Parser doesn't support relative imports (`from .helpers import X`)

- `PackageInit_WithReExports_ExportsModuleMembers`
- `ComplexScenario_PackageWithMultipleModulesAndReExports_Works`
  - Error: `Parser error: Expected identifier, got Dot`

**Fix Required:** Parser enhancement for relative imports (may be out of scope for CG7)

#### Category F: From-Import Code Generation (2 tests)
**Issue:** `from X import Y` doesn't generate correct using static directive

- `PackageInit_ImportFromPackage_Works`
- `ComplexScenario_MixedImportStyles_Works`

---

## Step-by-Step Implementation Approach

### Phase 1: Investigate Generated C# Code
**Goal:** Understand exactly what C# code is being generated vs expected

1. Add debug output to tests to capture generated .cs files
2. Compare against expected C# structure:
   ```csharp
   // Expected for module utils.spy with function helper():
   namespace TestProject.Utils
   {
       public static class Exports
       {
           public static string Helper() { return "help"; }
       }
   }

   // Expected main.spy using import:
   using utils = TestProject.Utils.Exports;

   namespace TestProject.Main
   {
       public class Program
       {
           public static void Main(string[] args)
           {
               var result = utils.Helper();
           }
       }
   }
   ```

### Phase 2: Fix Empty Module Generation (Category A)
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

1. In `GenerateModuleClass()`, ensure Exports class is always generated
2. Add check: if module has no declarations, still emit:
   ```csharp
   namespace ProjectName.ModuleName
   {
       public static class Exports { }
   }
   ```

### Phase 3: Fix Using Directive Generation (Category D)
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

1. Review `GenerateImportUsings()` method
2. Ensure import aliases point to `.Exports`:
   - `import utils` → `using utils = TestProject.Utils.Exports;`
   - `import lib.math` → `using lib_math = TestProject.Lib.Math.Exports;`
3. Verify nested module path conversion is correct

### Phase 4: Fix Module Variable Export (Category B)
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

1. Review `GenerateModuleClass()` variable handling
2. Ensure module-level variables with type annotations become static fields:
   ```csharp
   public static class Exports
   {
       public static int MAX_SIZE = 100;
       public static string APP_NAME = "MyApp";
   }
   ```
3. Verify name mangling: `MAX_SIZE` → `MaxSize` or keep `MAX_SIZE`

### Phase 5: Fix Entry Point Generation (Category C)
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

1. Review entry point detection logic
2. Ensure Main() is generated only for entry point file
3. Verify class naming when filename would conflict

### Phase 6: Handle From-Import (Category F)
**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

1. Review `GenerateFromImportUsings()` method
2. Ensure `from module import X` generates:
   ```csharp
   using static TestProject.Module.Exports;
   ```

### Phase 7: Parser Enhancement for Relative Imports (Category E) - May Defer
**Files:** `src/Sharpy.Compiler/Parser/Parser.cs`

1. Support parsing `from .module import X`
2. Support parsing `from ..module import X`
3. This may be deferred to a separate task if complex

---

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Primary fixes for code generation |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Using directive generation |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Entry point/Main() generation |
| `src/Sharpy.Compiler/Parser/Parser.cs` | Relative import parsing (if needed) |

---

## Tests to Verify (In Order of Priority)

### Priority 1: Basic Module System (Must Pass)
1. `BasicImport_SimpleModule_Works`
2. `BasicImport_ImportFromSubdirectory_Works`
3. `BasicImport_MultipleImports_Works`
4. `BasicImport_ImportVariable_Works`

### Priority 2: Multi-File Compilation (Must Pass)
5. `MultiFile_TwoFilesWithDependency_CompilesInCorrectOrder`
6. `MultiFile_ThreeFilesChainedDependency_CompilesCorrectly`
7. `MultiFile_SharedDependency_CompilesOnce`
8. `MultiFile_ComplexDependencyGraph_ResolvesCorrectly`
9. `MultiFile_FunctionCallAcrossModules_TypeChecksCorrectly`

### Priority 3: Package System (Should Pass)
10. `PackageInit_EmptyInit_MarksAsPackage`
11. `PackageInit_WithVariables_DefinesPackageLevelVariables`
12. `PackageInit_WithFunctions_DefinesPackageLevelFunctions`
13. `PackageInit_NestedPackages_Works`
14. `PackageInit_ImportFromPackage_Works`

### Priority 4: Project Configuration (Should Pass)
15. `ProjectFile_BasicConfiguration_CompilesSuccessfully`
16. `ProjectFile_LibraryOutputType_CompilesWithoutEntryPoint`
17. `ProjectFile_MultipleSourceFiles_CompilesAll`
18. `ProjectFile_CustomSourceDirectory_FindsSourceFiles`

### Priority 5: Complex Scenarios (Good to Pass)
19. `ComplexScenario_NestedPackagesWithImports_Works`
20. `ComplexScenario_MixedImportStyles_Works`
21. `ComplexScenario_ProjectWithPackagesAndModules_CompilesCorrectly`
22. `ComplexScenario_LargeProjectWithManyFiles_CompilesEfficiently`

### Priority 6: Edge Cases (Good to Pass)
23. `EdgeCase_EmptyModule_CompilesSuccessfully`
24. `EdgeCase_ModuleWithOnlyComments_CompilesSuccessfully`
25. `EdgeCase_ImportSameName_FromDifferentPackages_Works`
26. `EdgeCase_DeepNesting_Works`

### Priority 7: Re-exports / Relative Imports (May Defer)
27. `PackageInit_WithReExports_ExportsModuleMembers`
28. `ComplexScenario_PackageWithMultipleModulesAndReExports_Works`
29. `ComplexScenario_PackageImportingFromParentPackage_Works`

---

## Potential Risks and Questions

### Risks

1. **Cascading Changes:** Fixing code generation may expose other issues in semantic analysis or type checking
2. **Backward Compatibility:** Changes to code generation pattern may break existing code that worked
3. **Test Infrastructure:** Some test failures may be due to test setup issues rather than compiler bugs
4. **Relative Import Scope:** Parser changes for `.` imports may be complex and warrant separate task

### Questions for Clarification

1. **Name Convention:** Should module variables use PascalCase (`MaxSize`) or keep original (`MAX_SIZE`)?
   - Current behavior appears to use PascalCase transformation

2. **Relative Imports:** Are `from .module import X` imports required for Phase 0.1.10, or can they be deferred?
   - 3 tests depend on this feature

3. **Empty Modules:** Should importing an empty module be allowed? Current tests expect it to work.

4. **Entry Point Naming:** When entry point file is named `main.spy`, should class be `Main` or `Program`?
   - CG6 introduced logic for this but may need verification

---

## Execution Checklist

- [ ] Debug: Add diagnostic output to capture generated C# code
- [ ] Fix: Empty module Exports class generation
- [ ] Fix: Using directive namespace paths (add .Exports suffix)
- [ ] Fix: Module variable export to Exports class
- [ ] Fix: Entry point Main() method generation
- [ ] Fix: From-import using static directive
- [ ] Test: Run all 32 tests, verify improvements
- [ ] Assess: Relative import parser enhancement (defer if complex)
- [ ] Document: Update any design docs with findings
