# Implementation Plan: Fix Nested Package Namespace Generation (Task 0.1.10.CG3)

## Problem Analysis

### Current Behavior
For a file at `level1/level2/level3/__init__.spy`:
- **Current namespace**: `TestProject.Level1.Level2.Level3.Level3` (duplicated segment)
- **Expected namespace**: `TestProject.Level1.Level2.Level3.Init`

The import alias also has issues:
- **Current**: `using level1_level2_level3 = ...` (incomplete/incorrect)
- **Expected**: Properly formed alias pointing to correct namespace

### Root Cause
In `RoslynEmitter.cs:203-230`, the `GenerateProjectNamespace()` method:

1. Adds directory parts to namespace (correct): `Level1.Level2.Level3`
2. Adds filename as final component (problematic): For `__init__.spy`, `Path.GetFileNameWithoutExtension()` returns `__init__`, then `SimpleToPascalCase("__init__")` tries to convert it

The issue is that:
- `__init__` should become `Init` (not be skipped, and not cause duplication)
- The directory name `level3` and `__init__` are being conflated somehow

Looking at the code more carefully:

```csharp
// Line 218-220: Add directory parts (Level1, Level2, Level3)
var dirParts = relativeDir.Split(...)
    .Select(p => SimpleToPascalCase(p));
namespaceParts.AddRange(dirParts);

// Line 224-227: Add filename (__init__ -> Init or Level3?)
if (!string.IsNullOrEmpty(fileName))
{
    namespaceParts.Add(SimpleToPascalCase(fileName));
}
```

For `level1/level2/level3/__init__.spy`:
- `relativePath` = `level1/level2/level3/__init__.spy`
- `relativeDir` = `level1/level2/level3`
- `fileName` = `__init__`

So the namespace becomes: `TestProject.Level1.Level2.Level3.__init__` where `SimpleToPascalCase("__init__")` is called.

The `SimpleToPascalCase` function (lines 438-474) splits on underscores, so `__init__` becomes `Init` (after splitting and capitalizing).

**Wait - the bug description says it produces `Level3.Level3`, not `Level3.Init`**. Let me re-examine the actual bug.

If the result is `Level3.Level3`, the directory is being included twice. This could happen if:
1. The file is being treated as if it's named `level3.spy` instead of `__init__.spy`
2. Or there's special handling for `__init__` that's incorrectly using the parent directory name

## Implementation Approach

### Step 1: Understand the actual bug flow
Create a test that reproduces the exact issue to understand the data flow.

### Step 2: Fix `GenerateProjectNamespace()` in `RoslynEmitter.cs`

Location: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs:203-230`

The fix should:
1. Detect when the file is `__init__.spy`
2. For `__init__.spy` files, the namespace should be:
   - The project namespace + directory path + `Init`
   - NOT directory path + directory name (which causes duplication)

**Proposed change:**

```csharp
private NameSyntax GenerateProjectNamespace()
{
    // Start with project root namespace
    var namespaceParts = new List<string> { _context.ProjectNamespace! };

    // Get relative path from project src directory to source file
    var relativePath = Path.GetRelativePath(_context.ProjectRootPath!, _context.SourceFilePath!);

    // Extract directory path (without filename)
    var relativeDir = Path.GetDirectoryName(relativePath) ?? "";
    var fileName = Path.GetFileNameWithoutExtension(_context.SourceFilePath);

    // Add directory parts to namespace (if not at root)
    if (!string.IsNullOrEmpty(relativeDir) && relativeDir != ".")
    {
        var dirParts = relativeDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => SimpleToPascalCase(p));
        namespaceParts.AddRange(dirParts);
    }

    // Add file name as final namespace component
    // Special case: __init__.spy should become "Init", not cause duplication
    if (!string.IsNullOrEmpty(fileName))
    {
        var pascalFileName = SimpleToPascalCase(fileName);
        // __init__ converts to "Init" after SimpleToPascalCase strips underscores
        // This is the correct behavior
        namespaceParts.Add(pascalFileName);
    }

    return ParseName(string.Join(".", namespaceParts));
}
```

Actually, looking at `SimpleToPascalCase`:
- Input: `__init__`
- After sanitizing: `__init__`
- After splitting on `_`: `["init"]` (empty parts removed)
- After capitalizing: `Init`

So the current code SHOULD produce `Init`. The bug must be elsewhere.

### Step 3: Investigate potential issue in `SimpleToPascalCase`

Looking at line 469: `var parts = sanitized.ToString().Split('_', StringSplitOptions.RemoveEmptyEntries);`

For `__init__`:
- sanitized = `__init__`
- parts = `["init"]` (leading/trailing underscores create empty parts that are removed)
- Result should be `Init`

This seems correct. The bug might be:
1. In how `_context.SourceFilePath` is set
2. In project compilation code that sets up the context
3. A different code path being taken

### Step 4: Check `ProjectCompiler.cs`

The task mentions `src/Sharpy.Compiler/ProjectCompiler.cs` but this file doesn't exist at that path. The actual file is:
`src/Sharpy.Compiler/Project/ProjectCompiler.cs`

Need to check how it sets up `CodeGenContext` for `__init__.spy` files.

### Step 5: Fix import alias generation

Related to lines 271-350 in `RoslynEmitter.cs` (`GenerateImportUsings`).

For nested package imports like `import level1.level2.level3`:
- The alias should be `level1_level2_level3`
- The target should be `TestProject.Level1.Level2.Level3.Init.Exports`

## Key Files to Modify

1. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`** (primary)
   - Lines 203-230: `GenerateProjectNamespace()` - fix namespace generation for `__init__.spy`
   - Lines 271-350: `GenerateImportUsings()` - fix import alias generation for nested packages

2. **`src/Sharpy.Compiler/Project/ProjectCompiler.cs`** (if needed)
   - Check how `CodeGenContext` is configured for package init files

## Tests to Verify

### New Tests Needed

1. **Namespace generation for `__init__.spy`**:
```csharp
[Fact]
public void GenerateProjectNamespace_NestedPackageInit_ProducesCorrectNamespace()
{
    // Setup: project root at /project with file at /project/src/level1/level2/__init__.spy
    // Expected: TestProject.Level1.Level2.Init
}
```

2. **Deeply nested `__init__.spy`**:
```csharp
[Fact]
public void GenerateProjectNamespace_DeeplyNestedPackageInit_NoDuplication()
{
    // Setup: /project/src/level1/level2/level3/__init__.spy
    // Expected: TestProject.Level1.Level2.Level3.Init
    // NOT: TestProject.Level1.Level2.Level3.Level3
}
```

3. **Import alias for nested packages**:
```csharp
[Fact]
public void GenerateImportUsings_NestedPackageImport_CorrectAlias()
{
    // import level1.level2.level3
    // Expected alias: level1_level2_level3
    // Expected target: TestProject.Level1.Level2.Level3.Init.Exports
}
```

### Existing Tests to Run

- `RoslynEmitterIntegrationTests.cs` - ensure no regressions
- `ProjectCompilationTests.cs` - verify package imports still work
- Full test suite with `dotnet test`

## Potential Risks

1. **Breaking existing package imports**: Changes to namespace generation could break imports that rely on current (buggy) behavior
2. **Class name collision with "Init"**: If a package has both `__init__.spy` and `init.spy`, there could be a collision
3. **Build path differences**: Windows vs Unix path separators in test environments

## Questions to Clarify

1. **Should `__init__.spy` always become `Init` class?** Or should it use the package directory name?
   - Python convention: `__init__.py` makes the directory a package
   - Current Sharpy convention seems to use filename for class name

2. **What about the `Exports` class?** Is there an `Exports` class generated for `__init__.spy` files like for regular modules?

3. **Import alias completeness**: The task description truncates the current alias output. What is the complete expected form?

## Implementation Order

1. Write failing test that reproduces the exact bug
2. Debug to find exact cause of duplication
3. Fix `GenerateProjectNamespace()`
4. Fix `GenerateImportUsings()` if needed
5. Run full test suite
6. Add comprehensive tests for edge cases
