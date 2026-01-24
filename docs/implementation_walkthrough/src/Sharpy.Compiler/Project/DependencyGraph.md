# Walkthrough: DependencyGraph.cs

**Source File**: `src/Sharpy.Compiler/Project/DependencyGraph.cs`

---

## Overview

The `DependencyGraph` class is the **brain of multi-file compilation** in the Sharpy compiler. It represents the dependency relationships between source files in a project, tracking which files import which other files. This immutable data structure powers critical compilation features:

- **Build ordering**: Ensuring files are compiled in the correct order (dependencies first)
- **Cycle detection**: Finding circular import chains that would cause compilation failures
- **Incremental compilation**: Determining which files need recompilation when sources change
- **Parallel compilation**: Identifying files that can be compiled simultaneously

### Position in the Pipeline

```
Source Files (.spy) 
    ↓
Import Resolution (discovers dependencies)
    ↓
DependencyGraphBuilder (builds the graph)
    ↓
[DependencyGraph] ← YOU ARE HERE
    ↓
Compilation Ordering & Parallelization
    ↓
Semantic Analysis & Code Generation
```

The `DependencyGraph` sits between **import discovery** and **compilation execution**, providing the roadmap for how to process a multi-file project efficiently and correctly.

---

## Class Structure

### Core Properties

The class maintains three primary data structures:

```csharp
public IReadOnlyDictionary<string, ImmutableHashSet<string>> FileDependencies { get; }
public IReadOnlyDictionary<string, ImmutableHashSet<string>> ReverseDependencies { get; }
public IReadOnlySet<string> AllFiles { get; }
```

**FileDependencies (Forward Map)**: 
- Maps each file → the files it depends on (imports)
- Example: `"main.spy" → {"utils.spy", "models.spy"}`
- This answers: "What does this file need?"

**ReverseDependencies (Backward Map)**:
- Maps each file → the files that depend on it (dependents)
- Example: `"utils.spy" → {"main.spy", "models.spy"}`
- This answers: "What files will break if I change this?"

**AllFiles**:
- The complete set of files in the project
- Includes both files that import and files that are imported

### Optional Features

```csharp
public IReadOnlyDictionary<string, string>? FileHashes { get; }
```

**FileHashes**:
- Maps file paths to content hashes (SHA-256, etc.)
- Used for **future incremental compilation support**
- Enables detecting which files have actually changed since last build

---

## Key Methods

### 1. Constructor: Building the Graph

```csharp
public DependencyGraph(
    IReadOnlyDictionary<string, ImmutableHashSet<string>> fileDependencies,
    IReadOnlyDictionary<string, string>? fileHashes = null)
```

**What it does**:
- Takes raw dependency data and constructs an immutable, normalized graph
- Builds the reverse dependency map automatically
- Normalizes file paths for cross-platform consistency

**Key operations**:

1. **Path normalization** (line 82-86):
   - Converts backslashes to forward slashes (`\` → `/`)
   - Lowercases paths on Windows/macOS (case-insensitive filesystems)
   - Ensures consistent comparisons

2. **Completeness guarantee** (line 89-96):
   - Every file mentioned in dependencies gets an entry
   - Files with no dependencies get empty sets
   - Prevents null reference errors during queries

3. **Reverse map construction** (line 102-118):
   - Iterates through all forward edges
   - For each edge `A → B`, adds `A` to B's reverse dependency set
   - Result: bidirectional navigation capability

**Why immutable?**
Once constructed, the graph never changes. This enables:
- Thread-safe queries without locks
- Predictable behavior during compilation
- Safe sharing across compilation phases

---

### 2. GetBuildOrder: Topological Sorting

```csharp
public IReadOnlyList<string> GetBuildOrder()
```

**What it does**:
Returns a valid compilation order where every file appears **after** all its dependencies.

**Algorithm: Kahn's Algorithm** (lines 176-211)

```
Example graph:
utils.spy ←─ models.spy ←─ main.spy
          └───────────────┘

Build order: [utils.spy, models.spy, main.spy]
```

**Step-by-step**:

1. **Calculate in-degrees** (line 184):
   - In-degree = number of files this file depends on
   - `utils.spy`: 0, `models.spy`: 1, `main.spy`: 2

2. **Initialize queue with zero-dependency files** (line 188-194):
   - Start with files that have no dependencies
   - These can be compiled first

3. **Process queue** (line 197-211):
   - Dequeue a file → add to build order
   - "Remove" this file by decrementing dependents' in-degrees
   - When a dependent reaches in-degree 0, enqueue it
   - Repeat until queue is empty

**Critical insight**: If cycles exist, not all files appear in the result. Always call `DetectCycles()` first in production code!

**Use case**: Single-threaded sequential compilation

---

### 3. DetectCycles: Finding Circular Dependencies

```csharp
public IReadOnlyList<ImmutableArray<string>> DetectCycles()
```

**What it does**:
Finds all circular import chains that would make compilation impossible.

**Algorithm: Depth-First Search with Path Tracking** (lines 245-273)

```
Example cycle:
A.spy imports B.spy
B.spy imports C.spy
C.spy imports A.spy

Detected cycle: [A.spy, B.spy, C.spy, A.spy]
```

**Key data structures**:
- `visited`: Files we've explored (prevents re-processing)
- `recursionStack`: Files in the current DFS path (cycle detection)
- `path`: Current exploration path (for extracting cycle details)

**How it works** (lines 252-269):

1. **Mark current file as visiting** (line 253-254):
   - Add to visited (permanent)
   - Add to recursion stack (temporary - current path only)

2. **Explore each dependency** (line 256):
   - If not visited: recurse into it (line 259)
   - If in recursion stack: **cycle found!** (line 262)

3. **Extract cycle** (line 265-267):
   - Find where the cycle starts in the path
   - Take everything from that point forward
   - Append the repeated file to close the loop

4. **Backtrack** (line 271-272):
   - Remove from recursion stack (no longer in current path)
   - Remove from path list

**Why this matters**: Cycles must be reported as errors before attempting compilation. The generated error message should show the full import chain so developers can break the cycle.

---

### 4. GetAffectedFiles: Incremental Recompilation

```csharp
public ImmutableHashSet<string> GetAffectedFiles(string changedFile)
public ImmutableHashSet<string> GetAffectedFiles(IEnumerable<string> changedFiles)
```

**What it does**:
Determines the **blast radius** of a source file change. When you edit a file, which other files need recompilation?

**Algorithm: Breadth-First Search on Reverse Dependencies** (lines 296-326)

```
Example:
utils.spy is changed
    ↓ imported by
models.spy (needs recompilation)
    ↓ imported by
main.spy (needs recompilation)

Result: {utils.spy, models.spy, main.spy}
```

**Step-by-step** (lines 302-325):

1. **Initialize with changed files** (line 302-310):
   - Normalize paths
   - Add to affected set
   - Enqueue for processing

2. **BFS traversal** (line 313-324):
   - Dequeue a file
   - Find all files that import it (reverse dependencies)
   - Add to affected set (if not already present)
   - Enqueue for further traversal

**Why BFS?**
- Explores dependencies level-by-level (immediate dependents first)
- Guarantees we find all transitively affected files
- Efficient: each file processed at most once

**Use case**: 
- Watch mode: user edits a file, only recompile affected files
- CI optimization: only rebuild parts of a project that changed

---

### 5. GetParallelizableGroups: Parallel Compilation Planning

```csharp
public IReadOnlyList<ImmutableHashSet<string>> GetParallelizableGroups()
```

**What it does**:
Organizes files into **compilation waves** where all files in a wave can compile simultaneously.

**Algorithm: Layered Topological Grouping** (lines 346-416)

```
Example:
Group 0: [utils.spy, config.spy]        ← No dependencies, compile in parallel
Group 1: [models.spy, helpers.spy]      ← Depend only on Group 0
Group 2: [main.spy]                      ← Depends on Group 1
```

**Key insight**: A file's group number = 1 + maximum group of any dependency

**Step-by-step** (lines 354-391):

1. **Initialize Group 0** (line 358-366):
   - Find all files with no dependencies
   - These can start immediately
   - Add to queue for processing

2. **Assign levels via BFS** (line 369-391):
   - Process a file at level N
   - For each dependent:
     - Check if all **its** dependencies have been leveled
     - If yes: level = max(dependency levels) + 1
     - Enqueue for processing

3. **Group by level** (line 394-413):
   - Create buckets by level number
   - Convert to ordered list of groups

**Why this matters**:
- **Parallelization**: All files in a group can compile concurrently (multi-core utilization)
- **Correctness**: Groups enforce dependency order (Group N+1 waits for Group N)
- **Performance**: Large projects compile much faster

**Example usage in ProjectCompiler**:
```csharp
var groups = dependencyGraph.GetParallelizableGroups();
foreach (var group in groups)
{
    // Compile all files in this group in parallel
    Parallel.ForEach(group, file => CompileFile(file));
    
    // Wait for all to complete before starting next group
}
```

---

### 6. IsStale: Change Detection

```csharp
public bool IsStale(string filePath, string currentHash)
```

**What it does**:
Checks if a file has changed since the graph was built (for incremental compilation).

**Logic** (lines 432-445):
1. If no hashes available → assume stale (conservative)
2. If file not in hash map → assume stale (new file)
3. Compare stored hash vs. current hash
4. Return true if hashes differ

**Future feature**: Currently used minimally, but designed for incremental compilation where only changed files (and their dependents) are recompiled.

---

### 7. NormalizePath: Cross-Platform Consistency

```csharp
private static string NormalizePath(string path)
```

**What it does**:
Ensures file paths compare correctly across operating systems.

**Normalization rules** (lines 450-462):
1. **Directory separators**: `\` → `/` (line 453)
   - Windows uses backslashes, Unix uses forward slashes
   - Standardize on forward slashes

2. **Case sensitivity** (line 456-460):
   - Windows/macOS: case-insensitive filesystems → lowercase
   - Linux: case-sensitive filesystem → preserve case

**Why this matters**:
Without normalization:
- `"src\\utils.spy"` and `"src/utils.spy"` would be different keys
- `"Utils.spy"` and `"utils.spy"` would be different on Windows but same file
- Dependency lookups would fail unpredictably

---

## Dependencies

### Upstream (Providers)

**DependencyGraphBuilder** (`DependencyGraphBuilder.cs`):
- Constructs `DependencyGraph` instances during import resolution
- Thread-safe accumulation of dependencies
- Provides the raw dependency data to the constructor

**ImportResolver** (`Discovery/ImportResolver.cs`):
- Resolves import statements to actual file paths
- Reports dependencies to `DependencyGraphBuilder`
- Example: `import utils` → `"./utils.spy"`

### Downstream (Consumers)

**ProjectCompiler** (`ProjectCompiler.cs`):
- Uses `GetBuildOrder()` or `GetParallelizableGroups()` for compilation ordering
- Calls `DetectCycles()` before compilation to validate the project
- May use `GetAffectedFiles()` for incremental recompilation

**Build Tools/CLI**:
- Watch mode: monitors files and uses `GetAffectedFiles()` to minimize rebuilds
- Error reporting: uses `DetectCycles()` to generate helpful diagnostics

---

## Patterns and Design Decisions

### 1. Immutability

**Decision**: The graph is immutable after construction.

**Rationale**:
- Thread-safe queries without locking
- No defensive copying needed
- Clear ownership semantics (no unexpected modifications)
- Compiler phases can safely share the graph

**Implementation**:
- All collections are `ImmutableHashSet` or `IReadOnlyDictionary`
- No public setters
- Construction happens once in the constructor

---

### 2. Bidirectional Navigation

**Decision**: Store both forward and reverse dependency maps.

**Cost**: 2x memory overhead for storing edges both ways

**Benefit**: O(1) queries for both:
- "What does file X depend on?" (forward map)
- "What depends on file X?" (reverse map)

**Trade-off analysis**:
- Space: Acceptable (typical projects have <1000 files, edges are just strings)
- Speed: Critical (these queries happen frequently during compilation)
- Verdict: **Memory cost justified by query performance**

---

### 3. Path Normalization

**Decision**: Normalize paths on every insertion and query.

**Problem solved**:
- Cross-platform builds (Windows CI + Linux prod)
- User inconsistency (mixing `\` and `/`)
- Case sensitivity differences

**Cost**: Small CPU overhead per path operation

**Benefit**: Eliminates entire classes of bugs related to path mismatches

---

### 4. Builder Pattern

**Decision**: Use `DependencyGraphBuilder` to construct `DependencyGraph`.

**Rationale**:
- Construction is incremental (files discovered over time)
- Need thread-safety during construction (parallel import resolution)
- Want immutability after construction (safe querying)

**Separation of concerns**:
- **Builder**: Mutable, thread-safe, accumulates dependencies
- **Graph**: Immutable, efficient queries, compilation roadmap

---

### 5. Algorithm Choices

| Problem | Algorithm | Why? |
|---------|-----------|------|
| Build order | Kahn's (BFS-based) | Simple, efficient O(V+E), clear cycle handling |
| Cycle detection | DFS with recursion stack | Standard approach, reports actual cycles |
| Affected files | BFS on reverse graph | Level-by-level exploration, natural fit |
| Parallel groups | Modified topological sort | Assigns levels, enables parallelization |

**Alternative considered for build order**: DFS-based topological sort
- **Rejected**: Harder to detect/handle cycles, less intuitive

**Alternative considered for cycles**: Tarjan's SCC
- **Rejected**: Overkill for this use case, reports components not chains

---

## Debugging Tips

### Problem: GetBuildOrder() returns empty list

**Possible causes**:
1. The graph is actually empty (`AllFiles.Count == 0`)
2. The graph contains cycles (Kahn's algorithm can't complete)

**Debugging steps**:
```csharp
var graph = builder.Build();
Console.WriteLine($"Files: {graph.AllFiles.Count}");

var cycles = graph.DetectCycles();
if (cycles.Count > 0)
{
    Console.WriteLine("Cycles detected!");
    foreach (var cycle in cycles)
    {
        Console.WriteLine($"  {string.Join(" → ", cycle)}");
    }
}

var buildOrder = graph.GetBuildOrder();
Console.WriteLine($"Build order length: {buildOrder.Count}");
```

---

### Problem: File not found in graph

**Symptom**: `GetDirectDependencies()` returns empty set for a file you know exists

**Possible causes**:
1. Path normalization mismatch
   - Input: `"src\\File.spy"` vs. Stored: `"src/file.spy"`
2. File not registered with builder
3. Case sensitivity issue on Linux

**Debugging**:
```csharp
// Check what's actually in the graph
Console.WriteLine("All files in graph:");
foreach (var file in graph.AllFiles)
{
    Console.WriteLine($"  '{file}'");
}

// Check normalization
var testPath = "src\\File.spy";
var normalized = graph.GetDirectDependencies(testPath); // Uses normalization internally
```

**Fix**: Always use consistent path format when adding to builder:
```csharp
// Do this
builder.AddFile(Path.GetFullPath(file).Replace('\\', '/'));

// Not this
builder.AddFile(file); // May have inconsistent separators
```

---

### Problem: Unexpected files in GetAffectedFiles()

**Symptom**: Changing a low-level file marks the entire project for recompilation

**Possible causes**:
1. The low-level file is imported by many files (legitimate)
2. Unintended transitive dependency chain
3. Circular dependency causing over-propagation

**Debugging**:
```csharp
var affected = graph.GetAffectedFiles("utils.spy");
Console.WriteLine($"Affected count: {affected.Count}");

// Trace the dependency chain for unexpected files
foreach (var file in affected.Where(f => f != "utils.spy"))
{
    var deps = graph.GetDirectDependencies(file);
    if (deps.Contains("utils.spy"))
    {
        Console.WriteLine($"  {file} directly imports utils.spy");
    }
    else
    {
        Console.WriteLine($"  {file} transitively depends on utils.spy");
        // Recursively check which dependency led to utils.spy
    }
}
```

---

### Problem: Parallel groups not as parallel as expected

**Symptom**: `GetParallelizableGroups()` returns many groups with few files each (serialization)

**Cause**: The project has a long dependency chain

**Example**:
```
A → B → C → D → E

Groups: [A], [B], [C], [D], [E]  ← All sequential!
```

**Not a bug**: This is correct behavior for a linear dependency chain.

**To improve parallelization**:
- Refactor code to reduce dependency depth
- Break monolithic files into independent modules
- Use dependency inversion (depend on interfaces, not implementations)

---

### Visualization for Debugging

Create a simple DOT file for Graphviz:
```csharp
public static void ExportToDot(DependencyGraph graph, string outputPath)
{
    var sb = new StringBuilder();
    sb.AppendLine("digraph Dependencies {");
    
    foreach (var (file, deps) in graph.FileDependencies)
    {
        var fileName = Path.GetFileName(file);
        foreach (var dep in deps)
        {
            var depName = Path.GetFileName(dep);
            sb.AppendLine($"  \"{fileName}\" -> \"{depName}\";");
        }
    }
    
    sb.AppendLine("}");
    File.WriteAllText(outputPath, sb.ToString());
}

// Usage
ExportToDot(graph, "dependencies.dot");
// Run: dot -Tpng dependencies.dot -o dependencies.png
```

---

## Contribution Guidelines

### When to Modify This File

**Add features for**:
- New queries on the dependency graph (e.g., "shortest path between files")
- Improved cycle detection (e.g., reporting minimal cycles only)
- Better parallel compilation strategies
- Caching/memoization of expensive computations

**Don't modify for**:
- Adding files to the graph → Use `DependencyGraphBuilder`
- Changing how imports are resolved → Modify `ImportResolver`
- Compilation logic → Work in `ProjectCompiler`

---

### Adding a New Query Method

**Example**: Add a method to find the "root" files (files nothing depends on)

```csharp
public ImmutableHashSet<string> GetRootFiles()
{
    var roots = ImmutableHashSet.CreateBuilder<string>();
    
    foreach (var file in AllFiles)
    {
        if (GetDirectDependents(file).IsEmpty)
        {
            roots.Add(file);
        }
    }
    
    return roots.ToImmutable();
}
```

**Checklist**:
1. ✅ Returns immutable collection
2. ✅ Uses existing public methods (`GetDirectDependents`)
3. ✅ Handles empty graph (returns empty set)
4. ✅ O(V) complexity is acceptable
5. ✅ Add XML documentation
6. ✅ Add unit tests in `DependencyGraphTests.cs`

---

### Improving Performance

**Current bottlenecks**:
1. `GetParallelizableGroups()`: O(V + E) but does multiple passes
2. `DetectCycles()`: O(V + E) worst case, runs DFS

**Optimization opportunities**:
- **Memoization**: Cache results of expensive queries
  - `GetBuildOrder()` result is immutable → cache it
  - `GetParallelizableGroups()` result is immutable → cache it
  - Invalidation: Never needed (graph is immutable)

**Example caching**:
```csharp
private IReadOnlyList<string>? _cachedBuildOrder;

public IReadOnlyList<string> GetBuildOrder()
{
    if (_cachedBuildOrder != null)
        return _cachedBuildOrder;
    
    // Existing implementation...
    _cachedBuildOrder = result;
    return result;
}
```

**Trade-off**: Memory (cached results) vs. CPU (recomputation)
- Verdict: Worth it for graphs queried multiple times

---

### Testing Requirements

When modifying this file, ensure tests cover:

1. **Empty graph**: All methods handle zero files gracefully
2. **Single file**: Edge case, no dependencies
3. **Linear chain**: `A → B → C → D`
4. **Diamond shape**: 
   ```
   A → B → D
   A → C → D
   ```
5. **Cycles**: Self-loops, two-node cycles, longer cycles
6. **Disconnected components**: Files with no relationship
7. **Path normalization**: Mixed separators, case variations

**Test file location**: `src/Sharpy.Compiler.Tests/Project/DependencyGraphTests.cs`

---

## Cross-References

### Related Files

**DependencyGraphBuilder.cs** ([Walkthrough](./DependencyGraphBuilder.md)):
- The mutable, thread-safe builder for constructing dependency graphs
- Used during import resolution to accumulate dependencies
- Calls `DependencyGraph` constructor after all dependencies are discovered

**ProjectCompiler.cs** ([Walkthrough](./ProjectCompiler.md)):
- The primary consumer of `DependencyGraph`
- Uses `GetBuildOrder()` for sequential compilation
- Uses `GetParallelizableGroups()` for parallel compilation
- Calls `DetectCycles()` to validate the project before compilation

**ImportResolver.cs** (`Discovery/ImportResolver.cs`):
- Resolves import statements to file paths
- Reports file→file dependencies to `DependencyGraphBuilder`
- The source of dependency information

### Documentation

**Project Compilation Architecture**:
- `docs/language_specification/project_compilation.md`: Overall multi-file compilation design

**Testing Documentation**:
- `.github/instructions/Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md`: How to write compiler tests

---

## Summary

The `DependencyGraph` is a **central data structure** for multi-file Sharpy compilation:

- ✅ **Immutable**: Thread-safe, predictable
- ✅ **Bidirectional**: O(1) forward and reverse lookups
- ✅ **Well-tested algorithms**: Kahn's, DFS, BFS
- ✅ **Performance-oriented**: Supports parallel compilation
- ✅ **Cross-platform**: Path normalization handles OS differences

**Key insight**: It's a **read-only roadmap** constructed once and queried many times throughout the compilation process.

**When debugging compilation ordering issues**, start here. The graph knows the truth about file dependencies.
