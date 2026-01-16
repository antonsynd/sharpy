# Implementation Plan: Task 0.1.10.6 - Circular Import Detection

## Summary

Implement enhanced circular import detection with clear error messages showing the full import chain, while supporting the two-phase resolution algorithm that allows forward references for type annotations.

---

## Current State Analysis

### Existing Implementation (ImportResolver.cs:147-152)

```csharp
if (_loadingModules.Contains(modulePath))
{
    AddError($"Circular import detected for module '{modulePath}'", lineStart, columnStart);
    return null;
}
```

**Limitations:**
1. Only detects direct circular imports (A→B→A)
2. No import chain tracking (doesn't show: A→B→C→A path)
3. No distinction between allowed circular references (type annotations) and disallowed ones (base classes)
4. Error message is minimal - doesn't help developers understand how the cycle formed

### Language Specification Requirements

From `docs/language_specification/module_system.md`:
- **Allowed**: Circular references for type annotations
- **Not Allowed**: Circular references for base classes
- Two-phase resolution enables forward references

---

## Implementation Approach

### Step 1: Add Import Chain Tracking Data Structure

**File:** `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

Replace simple `HashSet<string> _loadingModules` with a stack-based structure that tracks the full import chain:

```csharp
// Track import chain for error messages
private readonly Stack<ImportChainEntry> _importChain = new();

// Entry in the import chain for error reporting
private record ImportChainEntry(
    string ModulePath,
    int? LineStart,
    int? ColumnStart,
    string? ImportingModule  // Module that initiated this import
);
```

### Step 2: Create Detailed Circular Import Error

**File:** `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

Add helper method to format the circular import chain:

```csharp
private string FormatCircularImportChain(string cycleStartModule)
{
    var chain = new StringBuilder();
    chain.AppendLine("Circular import detected:");

    // Build chain: A -> B -> C -> A
    foreach (var entry in _importChain.Reverse())
    {
        chain.AppendLine($"  -> {Path.GetFileName(entry.ModulePath)}");
        if (entry.ModulePath == cycleStartModule)
            break;
    }
    chain.AppendLine($"  -> {Path.GetFileName(cycleStartModule)} (cycle)");

    return chain.ToString();
}
```

### Step 3: Update LoadModule to Use Chain Tracking

**File:** `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

Modify `LoadModule` method:

```csharp
private ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart)
{
    // Check cache first
    if (_moduleCache.TryGetValue(modulePath, out var cached))
        return cached;

    // Check for circular imports with detailed chain
    if (IsModuleInChain(modulePath))
    {
        var chainMessage = FormatCircularImportChain(modulePath);
        AddError(chainMessage, lineStart, columnStart);
        return null;
    }

    // ... rest of implementation

    // Push to import chain before loading
    _importChain.Push(new ImportChainEntry(
        modulePath,
        lineStart,
        columnStart,
        _currentModulePath
    ));

    try
    {
        // ... existing loading logic ...
    }
    finally
    {
        _importChain.Pop();
    }
}

private bool IsModuleInChain(string modulePath)
{
    return _importChain.Any(e => e.ModulePath == modulePath);
}
```

### Step 4: Track Transitive Imports Within Loaded Modules

When loading a module, we need to recursively resolve its imports to detect transitive cycles (A→B→C→A):

```csharp
private ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart)
{
    // ... existing checks ...

    _importChain.Push(new ImportChainEntry(modulePath, lineStart, columnStart, _currentModulePath));

    try
    {
        // Parse the module
        var module = ParseModule(modulePath);

        // Create module info
        var moduleInfo = new ModuleInfo { ... };

        // Extract exported symbols
        foreach (var statement in module.Body)
        {
            ExtractExportedSymbol(statement, moduleInfo);
        }

        // NEW: Resolve imports within this module to detect transitive cycles
        var previousModulePath = _currentModulePath;
        _currentModulePath = modulePath;
        try
        {
            ResolveModuleImports(module, Path.GetDirectoryName(modulePath));
        }
        finally
        {
            _currentModulePath = previousModulePath;
        }

        _moduleCache[modulePath] = moduleInfo;
        _loadedModules.Add(modulePath);

        return moduleInfo;
    }
    finally
    {
        _importChain.Pop();
    }
}

private void ResolveModuleImports(Module module, string? searchPath)
{
    foreach (var statement in module.Body)
    {
        switch (statement)
        {
            case ImportStatement import:
                ResolveImport(import, searchPath);
                break;
            case FromImportStatement fromImport:
                ResolveFromImport(fromImport, searchPath);
                break;
        }
    }
}
```

### Step 5: Add Tests for Circular Import Detection

**File:** `src/Sharpy.Compiler.Tests/Semantic/CircularImportTests.cs` (new file)

```csharp
public class CircularImportTests : IDisposable
{
    // Test: Direct circular import (A imports B, B imports A)
    [Fact]
    public void CircularImport_Direct_ReportsError()

    // Test: Transitive circular import (A→B→C→A)
    [Fact]
    public void CircularImport_Transitive_ReportsError()

    // Test: Self-import (A imports A)
    [Fact]
    public void CircularImport_SelfImport_ReportsError()

    // Test: Error message contains full chain
    [Fact]
    public void CircularImport_ErrorMessage_ContainsChain()

    // Test: Non-circular diamond dependency works (A→B, A→C, B→D, C→D)
    [Fact]
    public void DiamondDependency_NoCircular_Succeeds()

    // Test: Module caching prevents false positives
    [Fact]
    public void CachedModule_NotReportedAsCircular()
}
```

---

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Semantic/ImportResolver.cs` | Add chain tracking, update `LoadModule`, add helper methods |
| `src/Sharpy.Compiler.Tests/Semantic/CircularImportTests.cs` | New test file for circular import scenarios |

---

## Test Scenarios

### 1. Direct Circular Import
```
# a.spy
from b import ClassB

# b.spy
from a import ClassA  # Error: Circular import
```

### 2. Transitive Circular Import
```
# a.spy
from b import ClassB

# b.spy
from c import ClassC

# c.spy
from a import ClassA  # Error: a -> b -> c -> a
```

### 3. Diamond Dependency (Should Pass)
```
# main.spy
from utils import helper1
from utils import helper2

# utils/helper1.spy
from shared import common

# utils/helper2.spy
from shared import common  # OK - shared is cached, not circular

# shared.spy
def common(): pass
```

### 4. Self Import
```
# a.spy
from a import something  # Error: Self-import
```

---

## Potential Risks and Questions

### Risks

1. **Performance**: Stack-based tracking adds minimal overhead but should be validated with large dependency trees.

2. **Error Message Clarity**: The chain format needs to be clear. Consider both absolute paths and relative/short names for readability.

3. **Edge Cases**:
   - Package `__init__.spy` files that re-export from submodules
   - .NET assembly modules (don't have imports to resolve)

### Questions to Clarify

1. **Two-Phase Resolution Scope**: The task description mentions two-phase resolution (Type Collection → Body Resolution). Is this to be implemented as part of this task, or just the basic circular detection? The description appears truncated.

2. **Type Annotation vs Base Class**: Should we distinguish between:
   - `class A(B): pass` (inheritance - NOT allowed circular)
   - `def func(x: B): pass` (type annotation - allowed circular)

   This would require deeper AST analysis and may be a separate task.

3. **Error Recovery**: When a circular import is detected, should we:
   - Stop processing immediately (current behavior)
   - Continue to find all errors in the file

---

## Implementation Order

1. Add `ImportChainEntry` record and `_importChain` stack
2. Add `IsModuleInChain()` helper method
3. Add `FormatCircularImportChain()` helper method
4. Update `LoadModule()` to use chain tracking
5. Add `ResolveModuleImports()` to detect transitive cycles
6. Write tests for all scenarios
7. Run existing tests to ensure no regressions

---

## Success Criteria

- [x] Direct circular imports detected with clear error message
- [x] Transitive circular imports detected (A→B→C→A)
- [x] Error message shows full import chain
- [x] Diamond dependencies work correctly (no false positives)
- [x] Self-imports detected
- [x] All existing ImportResolver tests still pass (931 tests, 923 passed, 8 skipped)
- [x] New CircularImportTests all pass (9/9 tests passing)

## Implementation Status

✅ **COMPLETED** - All features implemented and tested successfully.

See detailed implementation summary: `docs/implementation/task_0_1_10_6_circular_import_detection.md`
