# Task List: Dependency Graph Implementation (Recommendation #8)

**Target:** v0.1.x enhancement  
**Priority:** High (enables incremental compilation, parallel compilation)  
**Estimated Effort:** Medium (3-5 focused implementation sessions)  
**Status:** In Progress (Phase 1 Complete)

---

## Overview

This task implements an explicit dependency graph for the Sharpy compiler, replacing the current implicit dependency discovery during import resolution. The dependency graph is critical for:

1. **Incremental Compilation:** Knowing which files to recompile when a file changes
2. **Parallel Compilation:** Determining which files can be compiled simultaneously
3. **Build Ordering:** Computing a valid topological order for compilation
4. **Cycle Detection:** Providing clear error messages for circular imports

### Design Decisions

| Decision | Type | Rationale |
|----------|------|-----------|
| File-level granularity first | Two-way door | Start simple; type-level granularity can be added later without breaking API |
| Immutable graph once built | One-way door | Encourages correct usage patterns; rebuild on changes |
| Separate builder pattern | Two-way door | Decouples construction from queries; easier to test |
| Integration via ProjectCompiler | Two-way door | Non-invasive; existing tests continue to pass |
| Content hashing optional | Two-way door | Add when implementing incremental compilation |

### Dependencies on Other Recommendations

- **Does not require:** CompilationUnit model (Rec #1) - can work with current file paths
- **Does not require:** Immutable AST (Rec #7) - graph is read-only after construction
- **Enhances:** CompilerServices (Rec #5) - can be exposed through services

---

## Phase 1: Core Data Structures (Foundation)

**Goal:** Create the core dependency graph classes without integrating them.

### Task 1.1: Create DependencyGraph Class
**File:** `src/Sharpy.Compiler/Project/DependencyGraph.cs`  
**Estimated:** 45-60 minutes

- [x] Create `DependencyGraph` class with:
  - `FileDependencies`: `IReadOnlyDictionary<string, ImmutableHashSet<string>>` (file → files it depends on)
  - `ReverseDependencies`: `IReadOnlyDictionary<string, ImmutableHashSet<string>>` (file → files that depend on it)
  - `AllFiles`: `IReadOnlySet<string>` (all files in the graph)
- [x] Add constructor that takes both dictionaries
- [x] Add `GetDirectDependencies(string filePath)` method
- [x] Add `GetDirectDependents(string filePath)` method
- [x] Add XML documentation comments
- [x] Ensure paths are normalized (consistent separators, case handling)

**Verification:**
```csharp
// Should compile and allow basic construction
var deps = new Dictionary<string, ImmutableHashSet<string>>
{
    ["a.spy"] = ImmutableHashSet.Create("b.spy"),
    ["b.spy"] = ImmutableHashSet<string>.Empty
};
var graph = new DependencyGraph(deps);
Assert.Equal(new[] { "b.spy" }, graph.GetDirectDependencies("a.spy"));
```

### Task 1.2: Implement Build Order Computation
**File:** `src/Sharpy.Compiler/Project/DependencyGraph.cs`  
**Estimated:** 30-45 minutes

- [x] Add `GetBuildOrder()` method returning `IReadOnlyList<string>`
- [x] Implement Kahn's algorithm for topological sort:
  1. Find all files with no dependencies (roots)
  2. Process roots, removing them from graph
  3. Repeat until all files processed
- [x] Return files with no dependencies first (leaf modules)
- [x] Handle empty graph (return empty list)

**Verification:**
```csharp
// a depends on b, b depends on c → build order: c, b, a
var graph = BuildGraph(("a.spy", "b.spy"), ("b.spy", "c.spy"));
var order = graph.GetBuildOrder();
Assert.True(order.IndexOf("c.spy") < order.IndexOf("b.spy"));
Assert.True(order.IndexOf("b.spy") < order.IndexOf("a.spy"));
```

### Task 1.3: Implement Cycle Detection
**File:** `src/Sharpy.Compiler/Project/DependencyGraph.cs`  
**Estimated:** 30-45 minutes

- [x] Add `DetectCycles()` method returning `IReadOnlyList<ImmutableArray<string>>`
- [x] Implement depth-first search with path tracking
- [x] Each returned array is one cycle (in order of imports)
- [x] Empty list means no cycles
- [x] For multiple cycles, return all distinct cycles

**Verification:**
```csharp
// a → b → c → a is a cycle
var graph = BuildGraph(("a.spy", "b.spy"), ("b.spy", "c.spy"), ("c.spy", "a.spy"));
var cycles = graph.DetectCycles();
Assert.Single(cycles);
Assert.Contains("a.spy", cycles[0]);
```

### Task 1.4: Implement Affected Files Query
**File:** `src/Sharpy.Compiler/Project/DependencyGraph.cs`  
**Estimated:** 30 minutes

- [x] Add `GetAffectedFiles(string changedFile)` returning `ImmutableHashSet<string>`
- [x] Add `GetAffectedFiles(IEnumerable<string> changedFiles)` for batch queries
- [x] Include the changed file(s) in the result
- [x] Compute transitive closure of reverse dependencies
- [x] Use breadth-first search for efficiency

**Verification:**
```csharp
// a depends on b, c depends on b → changing b affects a and c
var graph = BuildGraph(("a.spy", "b.spy"), ("c.spy", "b.spy"));
var affected = graph.GetAffectedFiles("b.spy");
Assert.Contains("a.spy", affected);
Assert.Contains("c.spy", affected);
Assert.Contains("b.spy", affected); // includes itself
```

### Task 1.5: Implement Parallelizable Groups
**File:** `src/Sharpy.Compiler/Project/DependencyGraph.cs`  
**Estimated:** 30-45 minutes

- [x] Add `GetParallelizableGroups()` returning `IReadOnlyList<ImmutableHashSet<string>>`
- [x] Each group contains files that can be compiled in parallel
- [x] Groups are ordered by dependency (compile group 0 before group 1, etc.)
- [x] Files with no dependencies are in group 0
- [x] A file is in group N where N = max(group of dependencies) + 1

**Verification:**
```csharp
// a depends on b and c; b and c have no deps → groups: {b, c}, {a}
var graph = BuildGraph(("a.spy", "b.spy"), ("a.spy", "c.spy"));
var groups = graph.GetParallelizableGroups();
Assert.Equal(2, groups.Count);
Assert.Contains("b.spy", groups[0]);
Assert.Contains("c.spy", groups[0]);
Assert.Contains("a.spy", groups[1]);
```

### ✅ Checkpoint 1.6: Commit Phase 1
- [x] Run existing tests to ensure no regressions
- [x] Commit with message: `feat(project): Add DependencyGraph core data structure`

---

## Phase 2: Unit Tests for DependencyGraph

**Goal:** Comprehensive unit tests before integration.

### Task 2.1: Create Test File
**File:** `src/Sharpy.Compiler.Tests/Project/DependencyGraphTests.cs`  
**Estimated:** 60-90 minutes

- [x] Create test class `DependencyGraphTests`
- [x] Add helper method `BuildGraph(params (string, string)[] edges)` for test setup
- [x] Add tests for:
  - [x] `GetDirectDependencies_ReturnsCorrectDependencies`
  - [x] `GetDirectDependents_ReturnsCorrectDependents`
  - [x] `GetBuildOrder_ReturnsTopologicalOrder`
  - [x] `GetBuildOrder_EmptyGraph_ReturnsEmptyList`
  - [x] `GetBuildOrder_SingleFile_ReturnsSingleFile`
  - [x] `GetBuildOrder_LinearChain_ReturnsCorrectOrder`
  - [x] `GetBuildOrder_Diamond_HandlesCorrectly` (a→b, a→c, b→d, c→d)
  - [x] `DetectCycles_NoCycles_ReturnsEmpty`
  - [x] `DetectCycles_SimpleCycle_ReturnsCycle`
  - [x] `DetectCycles_SelfCycle_ReturnsSingleElementCycle`
  - [x] `GetAffectedFiles_SingleChange_ReturnsTransitiveDependents`
  - [x] `GetAffectedFiles_MultipleChanges_CombinesResults`
  - [x] `GetAffectedFiles_LeafFile_ReturnsSelf`
  - [x] `GetParallelizableGroups_IndependentFiles_AllInFirstGroup`
  - [x] `GetParallelizableGroups_LinearChain_OneFilePerGroup`
  - [x] `GetParallelizableGroups_Diamond_CorrectGrouping`

### Task 2.2: Add Edge Case Tests
**File:** `src/Sharpy.Compiler.Tests/Project/DependencyGraphTests.cs`  
**Estimated:** 30 minutes

- [x] `GetDirectDependencies_UnknownFile_ReturnsEmpty`
- [x] `PathNormalization_HandlesSlashVariants`
- [x] `PathNormalization_HandlesCaseDifferences` (if on case-insensitive OS)
- [ ] `LargeGraph_Performance_CompletesQuickly` (1000+ files) - skipped for now

### ✅ Checkpoint 2.3: Commit Phase 2
- [x] All new tests pass
- [x] Commit with message: `test(project): Add DependencyGraph unit tests`

---

## Phase 3: DependencyGraphBuilder

**Goal:** Create the builder that constructs the graph during compilation.

### Task 3.1: Create DependencyGraphBuilder Class
**File:** `src/Sharpy.Compiler/Project/DependencyGraphBuilder.cs`  
**Estimated:** 30 minutes

- [x] Create `DependencyGraphBuilder` class
- [x] Add `AddFile(string filePath)` to register a file
- [x] Add `AddDependency(string fromFile, string toFile)` to record a dependency
- [x] Add `Build()` method returning `DependencyGraph`
- [x] Normalize paths on add (consistent separators)
- [x] Thread-safe implementation (use `ConcurrentDictionary` or locks)

**Verification:**
```csharp
var builder = new DependencyGraphBuilder();
builder.AddFile("a.spy");
builder.AddFile("b.spy");
builder.AddDependency("a.spy", "b.spy");
var graph = builder.Build();
Assert.Contains("b.spy", graph.GetDirectDependencies("a.spy"));
```

### Task 3.2: Add Builder Validation
**File:** `src/Sharpy.Compiler/Project/DependencyGraphBuilder.cs`  
**Estimated:** 20 minutes

- [x] Validate that dependency targets exist (optional, can be enabled)
- [x] Add `Build(bool validateTargets = false)` overload
- [x] Throw `InvalidOperationException` if validation fails with clear message

### Task 3.3: Create Builder Tests
**File:** `src/Sharpy.Compiler.Tests/Project/DependencyGraphBuilderTests.cs`
**Estimated:** 30 minutes

- [x] `Build_EmptyBuilder_ReturnsEmptyGraph`
- [x] `AddDependency_AutoRegistersFiles`
- [x] `Build_MultipleCalls_ReturnsSameGraph` (builder is idempotent)
- [x] `PathNormalization_WorksCorrectly`
- [x] `ThreadSafety_ConcurrentAdds_NoExceptions` (parallel test)

### ✅ Checkpoint 3.4: Commit Phase 3
- [x] All tests pass
- [x] Commit with message: `feat(project): Add DependencyGraphBuilder`

---

## Phase 4: Integration with ImportResolver

**Goal:** Populate the dependency graph during import resolution.

### Task 4.1: Extend ImportResolver Interface
**File:** `src/Sharpy.Compiler/Semantic/ImportResolver.cs`  
**Estimated:** 45 minutes

- [x] Add optional `DependencyGraphBuilder? _graphBuilder` field
- [x] Add `SetDependencyGraphBuilder(DependencyGraphBuilder builder)` method
- [x] In `LoadModule()`, when a module is loaded, call:
  ```csharp
  _graphBuilder?.AddDependency(_currentModulePath, modulePath);
  ```
- [x] In `ResolveImport()`, after resolving each import alias:
  ```csharp
  _graphBuilder?.AddDependency(_currentModulePath, resolvedPath);
  ```
- [x] In `ResolveFromImport()`, after resolving the module:
  ```csharp
  _graphBuilder?.AddDependency(_currentModulePath, resolvedPath);
  ```

**Verification:** Existing import tests should still pass (builder is optional).

### Task 4.2: Handle .NET Module Dependencies
**File:** `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
**Estimated:** 15 minutes

- [x] For .NET modules (from `ModuleRegistry`), don't add to file dependency graph
- [x] Consider adding a separate mechanism for assembly dependencies later
- [x] Document this decision in comments

### Task 4.3: Add Integration Tests
**File:** `src/Sharpy.Compiler.Tests/Semantic/ImportResolverDependencyTests.cs`
**Estimated:** 45 minutes

- [x] Create test fixture that sets up ImportResolver with DependencyGraphBuilder
- [x] `ResolveImport_AddsDependency`
- [x] `ResolveFromImport_AddsDependency`
- [ ] `NestedImports_AddsTransitiveDependencies` - skipped, tested in ProjectCompiler
- [ ] `CircularImport_StillRecordsDependency` - existing circular import tests cover this
- [x] `NetModule_NotAddedToGraph`

### ✅ Checkpoint 4.4: Commit Phase 4
- [x] All tests pass (including existing ImportResolver tests)
- [x] Commit with message: `feat(semantic): Integrate DependencyGraphBuilder with ImportResolver`

---

## Phase 5: Integration with ProjectCompiler ✅

**Goal:** Use the dependency graph in ProjectCompiler for build ordering.

### Task 5.1: Add Graph to ProjectCompiler
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Estimated:** 45 minutes

- [x] Add `private DependencyGraph? _dependencyGraph` field
- [x] In `InitializeSharedState()`, create a `DependencyGraphBuilder`
- [x] Pass the builder to `_importResolver.SetDependencyGraphBuilder(builder)`
- [x] After `ResolveImports()` completes, call `_dependencyGraph = builder.Build()`
- [x] Add all source files to the builder in `InitializeSharedState()` (after parsing)

### Task 5.2: Validate Build Order
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Estimated:** 30 minutes

- [x] In `ResolveImports()`, after building the graph:
  - [x] Call `_dependencyGraph.DetectCycles()`
  - [x] If cycles detected, add errors and return false
  - [x] Format cycle errors clearly showing the import chain

### Task 5.3: Use Build Order for Semantic Analysis
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Estimated:** 30 minutes

- [x] In `PerformSemanticAnalysis()`:
  - [x] Get build order: `var buildOrder = _dependencyGraph.GetBuildOrder()`
  - [x] Process modules in build order instead of arbitrary iteration
  - [x] This ensures dependencies are analyzed before dependents
- [x] Added path normalization mapping for cross-platform compatibility

### Task 5.4: Expose Graph in Compilation Result
**File:** `src/Sharpy.Compiler/Compiler.cs` (ProjectCompilationResult class)
**Estimated:** 15 minutes

- [x] Add `public DependencyGraph? DependencyGraph { get; init; }` property
- [x] Set it in `CreateFailureResult()` and `CompileAssembly()` before returning result
- [x] Document that this is available for tooling/analysis

### Task 5.5: Add ProjectCompiler Integration Tests
**File:** `src/Sharpy.Compiler.Tests/ProjectCompilationTests.cs`
**Estimated:** 45 minutes

- [x] `CompileProject_BuildsDependencyGraph`
- [x] `CompileProject_DependencyGraphHasCorrectDependencies`
- [x] `CompileProject_CircularDependency_ReportsError`
- [x] `CompileProject_CircularDependency_ErrorShowsChain`
- [x] `CompileProject_TransitiveDependencies_TrackedCorrectly`

### ✅ Checkpoint 5.6: Commit Phase 5
- [x] All existing ProjectCompilationTests still pass
- [x] New integration tests pass
- [x] Commit with message: `feat(project): Integrate DependencyGraph with ProjectCompiler`

---

## Phase 6: API for Incremental Compilation (Preparation) ✅

**Goal:** Add APIs needed for future incremental compilation without implementing the full feature.

### Task 6.1: Add Content Hash Support
**File:** `src/Sharpy.Compiler/Project/DependencyGraph.cs`
**Estimated:** 30 minutes

- [x] Add `FileHashes` property: `IReadOnlyDictionary<string, string>?`
- [x] Update builder to optionally accept hashes
- [x] Add `DependencyGraphBuilder.SetFileHash(string filePath, string hash)`
- [x] Hash can be SHA-256 of file content (implementation deferred)

**Note:** These were implemented in Phase 1-3 as part of the core DependencyGraph design.

### Task 6.2: Add Staleness Check Stub
**File:** `src/Sharpy.Compiler/Project/DependencyGraph.cs`
**Estimated:** 15 minutes

- [x] Add `IsStale(string filePath, string currentHash)` method
- [x] Returns true if hash differs from stored hash or file not in graph
- [x] Document this is for future incremental compilation

**Note:** This was implemented in Phase 1 as part of the core DependencyGraph design.

### Task 6.3: Document Incremental Compilation API
**Files:** `src/Sharpy.Compiler/Project/DependencyGraph.cs`, `DependencyGraphBuilder.cs`
**Estimated:** 20 minutes

- [x] Document the dependency graph API (comprehensive XML docs with examples)
- [x] Explain intended usage for incremental compilation (in IsStale remarks)
- [x] Provide code example for future implementers (in class-level example block)

**Note:** XML documentation was added during implementation phases. No separate README needed as the in-code documentation is comprehensive.

### ✅ Checkpoint 6.4: Commit Phase 6
- [x] All tests pass
- [x] APIs already existed from Phase 1-3 implementation
- [x] No additional commit needed (features already committed)

---

## Phase 7: Cleanup and Documentation ✅

### Task 7.1: Add XML Documentation
**Files:** All new files
**Estimated:** 30 minutes

- [x] Ensure all public types have `<summary>` documentation
- [x] Ensure all public methods have parameter documentation
- [x] Add `<example>` blocks for complex methods
- [x] Add `<remarks>` for design decisions

**Note:** Comprehensive XML documentation was added during implementation phases.

### Task 7.2: Update Architecture Documentation
**Files:** Source files contain comprehensive documentation
**Estimated:** 20 minutes

- [x] Document the DependencyGraph component (in class-level XML docs)
- [x] Explain its role in the compilation pipeline (in class summary)
- [x] Note integration points with ProjectCompiler and ImportResolver (in method remarks)

**Note:** In-code documentation is complete. No separate architecture doc needed as the source files are self-documenting.

### Task 7.3: Add Usage Examples
**File:** `src/Sharpy.Compiler/Project/DependencyGraph.cs`
**Estimated:** 15 minutes

- [x] Add example in class documentation showing:
  - Building a graph
  - Querying build order
  - Finding affected files

**Note:** Class-level `<example>` block added during Phase 1 implementation.

### ✅ Final Checkpoint 7.4: Final Commit
- [x] All tests pass (3832 passed, 13 skipped)
- [x] Documentation complete
- [x] Commit with message: `docs: Mark task_dependency_graph_implementation complete`

---

## Summary

### Files Created
| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/Project/DependencyGraph.cs` | Core dependency graph class |
| `src/Sharpy.Compiler/Project/DependencyGraphBuilder.cs` | Builder for constructing graphs |
| `src/Sharpy.Compiler.Tests/Project/DependencyGraphTests.cs` | Unit tests for DependencyGraph |
| `src/Sharpy.Compiler.Tests/Project/DependencyGraphBuilderTests.cs` | Unit tests for builder |
| `src/Sharpy.Compiler.Tests/Semantic/ImportResolverDependencyTests.cs` | Integration tests |

### Files Modified
| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Semantic/ImportResolver.cs` | Add dependency tracking |
| `src/Sharpy.Compiler/Project/ProjectCompiler.cs` | Build and use dependency graph |
| `src/Sharpy.Compiler/Project/ProjectCompilationResult.cs` | Expose graph in result |
| `src/Sharpy.Compiler.Tests/ProjectCompilationTests.cs` | Add integration tests |

### Test Coverage Strategy
1. **Unit tests first:** Test DependencyGraph in isolation
2. **Builder tests:** Test construction patterns
3. **Integration tests:** Test with real import resolution
4. **Existing tests:** Must continue to pass throughout

### Rollback Points
Each phase has a commit checkpoint. If issues arise:
1. Run tests after each checkpoint
2. If tests fail, revert to previous checkpoint
3. Investigate and fix before proceeding

### Future Extensions (Not in Scope)
- Type-level dependency tracking
- Incremental compilation implementation
- Parallel compilation implementation
- Watch mode / file system monitoring
- LSP integration for real-time dependency updates

---

## Quick Reference: Class Signatures

```csharp
// DependencyGraph.cs
public class DependencyGraph
{
    public IReadOnlyDictionary<string, ImmutableHashSet<string>> FileDependencies { get; }
    public IReadOnlyDictionary<string, ImmutableHashSet<string>> ReverseDependencies { get; }
    public IReadOnlySet<string> AllFiles { get; }
    public IReadOnlyDictionary<string, string>? FileHashes { get; }
    
    public ImmutableHashSet<string> GetDirectDependencies(string filePath);
    public ImmutableHashSet<string> GetDirectDependents(string filePath);
    public IReadOnlyList<string> GetBuildOrder();
    public IReadOnlyList<ImmutableArray<string>> DetectCycles();
    public ImmutableHashSet<string> GetAffectedFiles(string changedFile);
    public ImmutableHashSet<string> GetAffectedFiles(IEnumerable<string> changedFiles);
    public IReadOnlyList<ImmutableHashSet<string>> GetParallelizableGroups();
    public bool IsStale(string filePath, string currentHash);
}

// DependencyGraphBuilder.cs
public class DependencyGraphBuilder
{
    public void AddFile(string filePath);
    public void AddDependency(string fromFile, string toFile);
    public void SetFileHash(string filePath, string hash);
    public DependencyGraph Build(bool validateTargets = false);
}
```
