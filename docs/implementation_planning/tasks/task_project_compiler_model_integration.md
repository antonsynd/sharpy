# Task List: ProjectCompiler Model Integration

**Status:** ✅ COMPLETED

**Goal:** Migrate ProjectCompiler to use CompilationUnit and ProjectModel classes instead of separate dictionaries, providing a cleaner data model for incremental compilation.

**Priority:** Low - Improves architecture but not blocking language features.

**Prerequisites:**
- CompilationUnit implemented (✅ Done)
- ProjectModel implemented (✅ Done)
- DependencyGraph implemented (✅ Done)

**Estimated Total Effort:** 3-5 days

**Related Documents:**
- `architecture_review_and_recommendations.md` - Recommendation 1
- `Model/README.md` - Model namespace documentation

---

## Problem Summary

ProjectCompiler previously used separate dictionaries:

```csharp
// Before (fragmented)
private Dictionary<string, Module> _parsedModules = new();
private Dictionary<string, CompilationMetrics> _fileMetrics = new();
private List<string> _errors = new();
```

This made it hard to:
- Track all artifacts for a single file
- Implement incremental compilation
- Support parallel compilation
- Query compilation state

---

## Current State

### What's Done
- ✅ `CompilationUnit` class with all artifact storage
- ✅ `CompilationUnitFactory` for creating units
- ✅ `ProjectModel` class with unit collection
- ✅ `DependencyGraph` for build ordering
- ✅ ProjectCompiler uses `_projectModel.Units` for all file tracking
- ✅ `_parsedModules` dictionary removed
- ✅ `_fileMetrics` dictionary removed (metrics stored on `unit.Metrics`)
- ✅ Errors consolidated into model diagnostics

---

## Design Decisions

### Two-Way Door Decisions (Reversible)
1. **Dual-path migration**: Keep legacy dictionaries during transition, populate both
2. **Incremental adoption**: Update one compilation phase at a time

### One-Way Door Decisions (Commit Now)
1. **CompilationUnit as unit of work**: Each file is a CompilationUnit through the pipeline
2. **ProjectModel owns all units**: Single source of truth for project state

---

## Phase 1: Wire CompilationUnit into Parsing (2-4 hours)

### Task 1.1: Create CompilationUnits During Parsing
**Status:** ✅ COMPLETED (pre-existing implementation verified)

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Create CompilationUnit instances instead of just storing AST.

**Verification:**
- [x] CompilationUnits created for all files
- [x] Existing tests still pass (via legacy dictionaries)

**Commit:** `refactor(project): Create CompilationUnits during parsing`

---

### Task 1.2: Track Compilation Phase
**Status:** ✅ COMPLETED (pre-existing implementation verified)

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Update CompilationUnit.Phase as compilation progresses.

**Verification:**
- [x] Phase tracking works
- [x] Failed units are skipped

**Commit:** `refactor(project): Track compilation phase in CompilationUnit`

---

## Phase 2: Wire DependencyGraph into Import Resolution (2-4 hours)

### Task 2.1: Build DependencyGraph During Import Resolution
**Status:** ✅ COMPLETED (pre-existing implementation verified)

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Use DependencyGraphBuilder to track file dependencies.

**Verification:**
- [x] DependencyGraph built correctly
- [x] Circular imports detected
- [x] CompilationUnit.DirectDependencies populated

**Commit:** `feat(project): Build DependencyGraph during import resolution`

---

### Task 2.2: Use Build Order for Semantic Analysis
**Status:** ✅ COMPLETED (pre-existing implementation verified)

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Process files in dependency order for semantic analysis.

**Verification:**
- [x] Files processed in dependency order
- [x] Dependencies processed before dependents

**Commit:** `refactor(project): Use dependency order for semantic analysis`

---

## Phase 3: Wire CompilationUnit into Code Generation (2-4 hours)

### Task 3.1: Generate C# Per CompilationUnit
**Status:** ✅ COMPLETED (pre-existing implementation verified)

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Store generated C# on CompilationUnit.

**Verification:**
- [x] Generated C# stored on CompilationUnit
- [x] Phase updated correctly

**Commit:** `refactor(project): Store generated C# on CompilationUnit`

---

### Task 3.2: Create ProjectModel for Results
**Status:** ✅ COMPLETED (pre-existing implementation verified)

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Build a ProjectModel as the compilation result.

**Verification:**
- [x] ProjectModel contains all units
- [x] Dependency graph attached

**Commit:** `feat(project): Return ProjectModel from compilation`

---

## Phase 4: Remove Legacy Dictionaries (1-2 hours)

### Task 4.1: Remove Legacy Module Dictionary
**Status:** ✅ COMPLETED

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Remove `_parsedModules` and use `_projectModel.Units` throughout.

**Verification:**
- [x] No usages of `_parsedModules`
- [x] All tests pass

**Commit:** `refactor(project): Remove legacy _parsedModules dictionary`

---

### Task 4.2: Remove Legacy Metrics Dictionary
**Status:** ✅ COMPLETED

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Store metrics on CompilationUnit.Metrics instead of separate dictionary.

**Verification:**
- [x] Metrics stored on unit.Metrics
- [x] No legacy dictionary
- [x] All tests pass

**Commit:** `refactor(project): Remove legacy _fileMetrics dictionary`

---

### Task 4.3: Consolidate Error Collection
**Status:** ✅ COMPLETED

**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Add errors to ProjectModel.GlobalDiagnostics and CompilationUnit.Diagnostics.

**Verification:**
- [x] Errors aggregated correctly
- [x] Error messages include file paths
- [x] GetAllErrorMessages() method added to ProjectModel

**Commit:** `refactor(project): Consolidate errors into CompilationUnit.Diagnostics`

---

## Phase 5: Add ProjectModel Query Methods (Optional, 1-2 hours)

### Task 5.1: Add Unit Lookup Methods
**Status:** ✅ COMPLETED

**File:** `src/Sharpy.Compiler/Model/ProjectModel.cs`
**Description:** Add helper methods for querying project state.

**Methods Added:**
- `GetFailedUnits()` - Returns units with Failed phase
- `GetSuccessfulUnits()` - Returns units with CodeGenerated phase
- `GetUnitsAtPhase(phase)` - Returns units at any specified phase
- `GetAllErrorMessages()` - Returns formatted error strings

**Pre-existing Methods:**
- `GetUnit(filePath)` - Lookup by file path
- `HasErrors` - Check if any errors exist
- `GetAllDiagnostics()` - Aggregate diagnostics

**Verification:**
- [x] Query methods work correctly
- [x] All tests pass

**Commit:** `feat(model): Add ProjectModel query methods`

---

## Phase 6: Verification (30 minutes)

### Task 6.1: Run Full Test Suite
**Status:** ✅ COMPLETED

```bash
dotnet test Sharpy.Compiler.Tests --verbosity minimal
```

**Verification:**
- [x] All tests pass (4002 passed, 13 skipped)

---

### Task 6.2: Run Integration Tests
**Status:** ✅ COMPLETED

```bash
dotnet test Sharpy.Compiler.Tests --filter "FullyQualifiedName~Integration" --verbosity normal
```

**Verification:**
- [x] Multi-file compilation works (668 passed, 1 skipped)
- [x] Dependency ordering correct

---

### Task 6.3: Verify Incremental Compilation Foundation
**Status:** ✅ COMPLETED

**Description:** Verify that the foundation for incremental compilation is in place.

**Verification:**
- [x] CompilationUnit.ContentHash is computed (in constructor)
- [x] DependencyGraph.GetAffectedFiles works
- [x] CompilationUnit.IsStale() method works

---

## Summary

After completing these tasks:

1. ✅ ProjectCompiler uses CompilationUnit for all file tracking
2. ✅ DependencyGraph integrated for build ordering
3. ✅ ProjectModel returned as compilation result
4. ✅ Legacy dictionaries removed (`_parsedModules`, `_fileMetrics`)
5. ✅ Foundation ready for incremental compilation

**Benefits:**
- Single source of truth for file artifacts
- Clear dependency relationships
- Ready for parallel compilation (process independent units)
- Ready for incremental compilation (check staleness, rebuild affected)

**Commits Made:**
1. `refactor(project): Remove legacy _parsedModules dictionary`
2. `refactor(project): Remove legacy _fileMetrics dictionary`
3. `refactor(project): Consolidate errors into model diagnostics`
4. `feat(model): Add ProjectModel query methods`
