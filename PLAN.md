# Implementation Plan: Task 0.1.10.CG7 - Update Integration Tests

## Summary

**Task:** Fix Phase 0.1.10 integration tests to pass after code generation updates
**Current Status:** 29 failing, 4 passing, 1 skipped (34 total tests)
**Root Causes:** Multiple code generation issues in `RoslynEmitter.cs` related to module imports and namespace resolution

---

## Step-by-Step Implementation Approach

### Phase 1: Fix Using Directive Namespace Resolution (Highest Priority)
**Problem:** `import utils` generates `using utils = Utils.Exports;` but should be `using utils = TestProject.Utils.Utils;`

The using directive for regular imports (not `from X import Y`) is missing the project namespace prefix and using the old `Exports` class pattern instead of the module class name pattern.

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Location:** `GenerateImportUsings()` method (line ~245-281)

**Changes Required:**
1. Update the non-aliased Sharpy module import to include project namespace prefix
2. Change from `.Exports` to `.<ModuleClassName>` pattern (matching `GenerateFromImportUsings`)

**Before:**
```csharp
yield return UsingDirective(
    NameEquals(sanitizedAlias),
    ParseName($"{namespaceName}.Exports"));
```

**After:**
```csharp
// Build: ProjectNamespace.ModuleNamespace.ModuleClassName
var moduleClassName = /* extract last part of namespaceName */;
var fullPath = string.IsNullOrEmpty(_context.ProjectNamespace)
    ? $"{namespaceName}.{moduleClassName}"
    : $"{_context.ProjectNamespace}.{namespaceName}.{moduleClassName}";
yield return UsingDirective(
    NameEquals(sanitizedAlias),
    ParseName(fullPath));
```

**Tests Fixed:** BasicImport_SimpleModule_Works, BasicImport_ImportFromSubdirectory_Works, BasicImport_MultipleImports_Works, and ~15 more

---

### Phase 2: Fix Empty Module Code Generation
**Problem:** Empty modules or modules with only comments don't generate a class, causing "namespace not found" errors.

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Location:** `GenerateModuleClass()` method (line ~419-529)

**Changes Required:**
1. Always generate a module class, even if empty
2. Currently the class is generated, but the issue is that an empty class with no members triggers other issues

**Investigation Needed:** Check if the class is being generated for empty modules - the error suggests it's not being found by the importing module.

**Tests Fixed:** EdgeCase_EmptyModule_CompilesSuccessfully, EdgeCase_ModuleWithOnlyComments_CompilesSuccessfully

---

### Phase 3: Fix Entry Point Main() Generation
**Problem:** Some tests fail with "Program does not contain a static 'Main' method suitable for an entry point"

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Location:** `GenerateModuleClass()` method - entry point detection logic (line ~477-515)

**Current Logic:**
- Main() is generated if: no user main function AND is entry point AND has executable statements
- Main() is NOT generated if the file only has declarations (like `x: int = 42`)

**Investigation Needed:**
- Verify `_context.IsEntryPoint` is being set correctly
- Variable declarations (`x: int = 42`) need to generate a Main() or be treated as executable

**Tests Fixed:** ProjectFile_CustomSourceDirectory_FindsSourceFiles

---

### Phase 4: Fix Aliased Import Using Generation
**Problem:** `import config as cfg` should generate correct using directive with alias

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
**Location:** `GenerateImportUsings()` method, aliased branch (line ~254-261)

**Current Code:**
```csharp
var targetName = isNetFramework ? namespaceName : $"{namespaceName}.Exports";
```

**Changes Required:** Similar to Phase 1 - add project namespace and use module class name

---

### Phase 5: Handle Relative Imports (May Defer)
**Problem:** `from .helpers import utility_func` fails with parser error - relative imports not supported

**Files:** `src/Sharpy.Compiler/Parser/Parser.cs`

**Scope:** Parser doesn't recognize `.` prefix for relative imports. This is a parser feature, not code generation.

**Recommendation:** Mark these tests as `[Fact(Skip = "...")]` for now if parser changes are complex.

**Tests Affected:**
- PackageInit_WithReExports_ExportsModuleMembers
- ComplexScenario_PackageWithMultipleModulesAndReExports_Works
- ComplexScenario_PackageImportingFromParentPackage_Works

---

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Fix `GenerateImportUsings()` namespace resolution |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Ensure empty modules generate valid class |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Review entry point Main() generation |
| `src/Sharpy.Compiler.Tests/Integration/Phase0110IntegrationTests.cs` | Mark relative import tests as skipped |

---

## Tests to Verify (Priority Order)

### Must Pass (Core Import System)
1. BasicImport_SimpleModule_Works
2. BasicImport_ImportFromSubdirectory_Works
3. BasicImport_MultipleImports_Works
4. BasicImport_ImportVariable_Works

### Must Pass (Multi-File)
5. MultiFile_TwoFilesWithDependency_CompilesInCorrectOrder
6. MultiFile_ThreeFilesChainedDependency_CompilesCorrectly
7. MultiFile_SharedDependency_CompilesOnce
8. MultiFile_ComplexDependencyGraph_ResolvesCorrectly
9. MultiFile_FunctionCallAcrossModules_TypeChecksCorrectly

### Should Pass (Packages)
10. PackageInit_EmptyInit_MarksAsPackage
11. PackageInit_WithVariables_DefinesPackageLevelVariables
12. PackageInit_WithFunctions_DefinesPackageLevelFunctions
13. PackageInit_NestedPackages_Works
14. PackageInit_ImportFromPackage_Works

### Should Pass (Project Files)
15. ProjectFile_BasicConfiguration_CompilesSuccessfully
16. ProjectFile_LibraryOutputType_CompilesWithoutEntryPoint
17. ProjectFile_MultipleSourceFiles_CompilesAll
18. ProjectFile_CustomSourceDirectory_FindsSourceFiles

### Should Pass (Edge Cases)
19. EdgeCase_EmptyModule_CompilesSuccessfully
20. EdgeCase_ModuleWithOnlyComments_CompilesSuccessfully
21. EdgeCase_ImportSameName_FromDifferentPackages_Works
22. EdgeCase_DeepNesting_Works

### Defer (Relative Imports - Parser Change Required)
23. PackageInit_WithReExports_ExportsModuleMembers
24. ComplexScenario_PackageWithMultipleModulesAndReExports_Works
25. ComplexScenario_PackageImportingFromParentPackage_Works

---

## Potential Risks

1. **Cascading Changes:** Fixing namespace resolution may reveal other issues in semantic analysis
2. **Test Infrastructure:** Some failures may be test setup issues rather than compiler bugs
3. **Backward Compatibility:** Changes to code generation could break existing compilations
4. **Parser Complexity:** Relative imports require parser changes that may be out of scope

---

## Questions to Clarify

1. **Relative Imports:** Should `from .module import X` be implemented in this task or deferred?
   - Recommendation: Defer to separate task, skip affected tests

2. **Empty Modules:** Should `import empty_module` be valid when module has no exports?
   - Current tests expect this to work

3. **Variable Declarations as Entry Point:** Should a file with only `x: int = 42` generate a Main()?
   - Current logic requires executable statements, not just declarations

---

## Execution Order

1. **Phase 1 First** - Fixing using directive generation will fix the majority of tests
2. **Phase 2-3** - Fix edge cases for empty modules and entry points
3. **Phase 4** - Fix aliased imports
4. **Phase 5** - Defer or implement relative imports based on complexity assessment
5. **Run all tests** to verify improvements
6. **Skip remaining relative import tests** if not implementing parser changes
