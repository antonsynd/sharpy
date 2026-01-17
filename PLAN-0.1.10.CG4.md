# Implementation Plan: Fix `__init__.spy` Class Name for Packages (Task 0.1.10.CG4)

## Problem Analysis

### Current Behavior
For a package `__init__.spy` file (e.g., `mypackage/__init__.spy`):
- **Current class name**: `Init` (from converting `__init__` → `Init`)
- **Import expects**: `Exports`

Example of the mismatch:
```csharp
// Generated C# for mypackage/__init__.spy
namespace TestProject.Mypackage {
    public static class Init { ... }  // ← BUG: Class named "Init"
}

// Import system generates
using mypackage = TestProject.Mypackage.Exports;  // ← Expects "Exports"
```

### Root Cause
The `GetModuleClassName()` method (lines 717-729) already returns `"Exports"` for non-entry-point modules. However, looking at the existing code, it seems the issue is elsewhere - possibly in the previous CG3 task that may have changed behavior.

Wait - re-reading the task description and examining the code more carefully:

The current `GetModuleClassName()` implementation at line 728 **already returns `"Exports"`**:
```csharp
private string GetModuleClassName(bool willGenerateMainMethod = false, HashSet<string>? functionNames = null)
{
    // All modules use "Exports" as the class name
    if (willGenerateMainMethod)
    {
        return "Program";
    }
    return "Exports";
}
```

This means either:
1. The bug was already fixed in a previous task (CG3), or
2. The task description's understanding is incorrect, or
3. There's a different code path generating the `Init` class name

Let me trace where `Init` could be coming from:
- `SimpleToPascalCase("__init__")` → `Init` (splitting on underscores)
- But this is for namespace generation, not class name

**After further analysis**: The task description may be referring to an **older state** of the code. The current `GetModuleClassName()` already returns `"Exports"`. However, the task is assigned and we should verify this works correctly.

## Verification Approach

Since the code appears to already return `"Exports"`, the plan is:

### Step 1: Verify Current Behavior
Write a test that specifically checks `__init__.spy` files generate a class named `Exports`.

### Step 2: Check for Edge Cases
Ensure the fix works for:
- Root-level `__init__.spy`
- Nested package `__init__.spy` (e.g., `level1/level2/__init__.spy`)
- Deeply nested packages

### Step 3: Verify Import Resolution
Ensure imports can find the `Exports` class in `__init__.spy` compiled modules.

## Key File

**`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`**
- Lines 717-729: `GetModuleClassName()` - Already returns `"Exports"` for non-entry-point modules

## Implementation Steps

### Step 1: Add Unit Test for Class Name Generation

Add to `RoslynEmitterNamespaceTests.cs` (or create `RoslynEmitterClassNameTests.cs`):

```csharp
[Fact]
public void GetModuleClassName_InitFile_ReturnsExports()
{
    // Arrange - Create emitter for __init__.spy file
    var emitter = CreateEmitterWithProjectContext(
        projectNamespace: "TestProject",
        projectRootPath: "/project/src",
        sourceFilePath: "/project/src/mypackage/__init__.spy"
    );

    // Act - Generate a module (which uses GetModuleClassName internally)
    // Need to verify the class name is "Exports"

    // Assert
    // Verify generated C# has "public static class Exports"
}
```

### Step 2: Add Integration Test

Add to `Phase0110IntegrationTests.cs`:

```csharp
[Fact]
public void PackageInit_GeneratesExportsClass_NotInitClass()
{
    var helper = CreateHelper();

    helper.AddPackage("mypackage", @"
def my_func() -> int:
    return 42
");

    helper.AddSourceFile("main.spy", @"
import mypackage
result = mypackage.my_func()
");

    helper.WithEntryPoint("main.spy");
    var result = helper.Compile();

    Assert.True(result.Success);

    // Verify the generated C# for mypackage/__init__.spy contains "Exports" class
    var initFile = result.GeneratedCSharpFiles
        .FirstOrDefault(f => f.Key.Contains("__init__"));
    Assert.Contains("public static class Exports", initFile.Value);
    Assert.DoesNotContain("public static class Init", initFile.Value);
}
```

### Step 3: Verify No Regression

Run the full test suite to ensure all existing package import tests pass.

## Tests to Verify

### New Tests
1. `GetModuleClassName_InitFile_ReturnsExports` - Unit test for class name
2. `PackageInit_GeneratesExportsClass_NotInitClass` - Integration test verifying generated C#

### Existing Tests to Run
```bash
dotnet test --filter "FullyQualifiedName~Package"
dotnet test --filter "FullyQualifiedName~RoslynEmitter"
dotnet test  # Full suite
```

## Potential Risks

1. **Already Fixed**: The code appears to already return `"Exports"`. If tests pass, this task may be complete.
2. **Other Code Paths**: There might be an alternative code path that generates `Init` that we haven't found.
3. **Test Coverage Gap**: Existing tests may not explicitly verify the class name.

## Questions/Clarifications

1. **Is this task still needed?** The current `GetModuleClassName()` returns `"Exports"`. Need to verify with tests.
2. **Was this fixed in CG3?** The previous task (CG3) dealt with namespace generation, which may have inadvertently affected this.

## Implementation Order

1. ✅ Read and understand current `GetModuleClassName()` implementation
2. Write unit test to verify `__init__.spy` generates `Exports` class
3. Write integration test to verify package imports work
4. Run existing tests to verify no regressions
5. If tests fail, implement the fix (though code appears correct)
6. Mark task complete if all tests pass

## Expected Result

After verification/implementation:
- `mypackage/__init__.spy` generates: `namespace TestProject.Mypackage { public static class Exports { ... } }`
- `import mypackage` generates: `using mypackage = TestProject.Mypackage.Exports;`
- Both align correctly.
