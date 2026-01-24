# Plan: Enable Strict Entry Point Main Function Requirement

## Problem Summary

In `src/Sharpy.Compiler/Semantic/Validation/ModuleLevelValidatorV2.cs` (lines 111-123), there is commented-out code that would enforce the requirement that **entry point files must have a `main()` function**. This is currently disabled for "backward compatibility."

According to the language specification in `docs/language_specification/program_entry_point.md`:
> "Every executable Sharpy program requires a `main()` function as its entry point"

The validator currently:
1. ✅ Rejects bare executable statements when there IS a `main()` function present
2. ✅ Rejects bare executable statements in non-entry-point (library) modules
3. ❌ Allows entry point files without a `main()` function (for backward compatibility)

## Current State Analysis

### Test Fixtures Already Compliant
After analysis, **all test fixture files that are entry points already have `main()` functions**. The files without `main()` are all library modules (in multi-file test scenarios), which correctly don't need one.

### Files Without `main()` (Library Modules - Correct)
These are all imported modules, not entry points:
- `imports/import_with_classes/math_utils.spy`
- `imports/simple_import_test/math_utils.spy`
- `imports/module_import_access/calculator.spy`
- `module_imports/geometry_shapes/geometry.spy`
- `module_imports/geometry_shapes/validators.spy`
- `module_imports/complex_type_relationships/geometry.spy`
- `module_imports/complex_type_relationships/calculator.spy`
- `cross_module_inheritance/*/*.spy` (all library files)

These files are correctly identified as non-entry-points via `IsEntryPoint = false`.

### Error Test Cases (Already Testing the Rules)
- `errors/main_function_with_statements.spy` - Tests rejection of bare executable statements when `main()` exists
- `errors/module_level_executable_statement.spy` - Tests rejection of bare `print()` at module level

## Implementation Plan

### Phase 1: Enable Strict Enforcement

**File:** `src/Sharpy.Compiler/Semantic/Validation/ModuleLevelValidatorV2.cs`

Uncomment and modify the entry point validation logic (lines 111-123):

```csharp
// Entry point files should have a main() function
if (_context.IsEntryPoint && !hasMainFunction)
{
    AddError(_context,
        "Entry point file requires a 'main()' function",
        module.LineStart, module.ColumnStart);
}
```

**Rationale for removing the inner condition:** The original commented code had an inner check `if (executableStatements.Count == 0 && untypedVariables.Count == 0)` which would only error if there were NO executable statements AND NO untyped variables. This seems backwards - we should always require `main()` for entry points regardless of whether there are other violations.

### Phase 2: Add Error Test Case

**New file:** `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/entry_point_missing_main.spy`

```python
# Error test: Entry point file must have a main() function

counter: int = 0

def helper() -> int:
    return counter + 1

# No main() function defined - this is an error for entry point files
```

**New file:** `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/entry_point_missing_main.error`

```
Entry point file requires a 'main()' function
```

### Phase 3: Update Unit Tests

**File:** `src/Sharpy.Compiler.Tests/Semantic/Validation/ModuleLevelValidatorV2Tests.cs`

Add a test case verifying that entry point files without `main()` are rejected:

```csharp
[Fact]
public void EntryPointFile_WithoutMainFunction_ReportsError()
{
    // Arrange: Entry point file with only declarations, no main()
    var source = @"
counter: int = 0

def helper() -> int:
    return 42
";
    var (module, context) = ParseWithContext(source, isEntryPoint: true);
    var validator = new ModuleLevelValidatorV2();

    // Act
    validator.Validate(module, context);

    // Assert
    Assert.True(context.Diagnostics.HasErrors);
    Assert.Contains(context.Diagnostics.Errors,
        e => e.Message.Contains("Entry point file requires a 'main()' function"));
}

[Fact]
public void LibraryModule_WithoutMainFunction_NoError()
{
    // Arrange: Library module (not entry point) with only declarations
    var source = @"
counter: int = 0

def helper() -> int:
    return 42
";
    var (module, context) = ParseWithContext(source, isEntryPoint: false);
    var validator = new ModuleLevelValidatorV2();

    // Act
    validator.Validate(module, context);

    // Assert
    Assert.False(context.Diagnostics.HasErrors);
}
```

### Phase 4: Verify All Tests Pass

Run the full test suite to ensure:
1. All existing tests still pass (they all have `main()`)
2. The new error test case works correctly
3. Library modules (non-entry-points) are not affected

```bash
dotnet test
```

## Risk Assessment

**Low Risk:** All existing test fixture files already have `main()` functions, so this change should not break any existing tests. The change only affects:
1. The theoretical case of an entry point file without `main()` (which was "working" via backward compatibility)
2. Adding proper validation for what the spec already requires

## Acceptance Criteria

1. ✅ Entry point files without `main()` produce error: "Entry point file requires a 'main()' function"
2. ✅ Library modules without `main()` continue to work (no error)
3. ✅ Entry point files WITH `main()` continue to work
4. ✅ All existing tests pass
5. ✅ New error test case added and passing
6. ✅ Unit tests for the validator updated

## Files to Modify

1. `src/Sharpy.Compiler/Semantic/Validation/ModuleLevelValidatorV2.cs` - Enable strict enforcement
2. `src/Sharpy.Compiler.Tests/Semantic/Validation/ModuleLevelValidatorV2Tests.cs` - Add unit tests
3. `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/entry_point_missing_main.spy` - New error test
4. `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/entry_point_missing_main.error` - Expected error message
