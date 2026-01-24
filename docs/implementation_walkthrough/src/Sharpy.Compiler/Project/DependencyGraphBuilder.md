# Walkthrough: DependencyGraphBuilder.cs

**Source File**: `src/Sharpy.Compiler/Project/DependencyGraphBuilder.cs`

---

## Overview

`DependencyGraphBuilder` is a **thread-safe builder** for constructing immutable `DependencyGraph` instances during multi-file Sharpy project compilation. It provides a safe way to accumulate file dependencies as imports are resolved across multiple threads, then "freeze" that information into an immutable graph used for build ordering, cycle detection, and incremental compilation analysis.

**Role in the Compiler Pipeline:**
- **Phase**: Import Resolution (Phase 4 of ProjectCompiler)
- **Input**: File paths and import statements discovered during parsing
- **Output**: An immutable `DependencyGraph` with complete dependency information
- **Position**: Between import resolution and semantic analysis

Think of it as a "shopping cart" that collects dependency relationships during import resolution, then gets "checked out" into a final immutable graph when you call `Build()`.

---

## Class Structure

### Main Components

```csharp
public class DependencyGraphBuilder
{
    // Core state
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _dependencies;
    private readonly ConcurrentDictionary<string, string> _fileHashes;
    
    // Thread safety
    private readonly object _buildLock;
    
    // Performance optimization
    private DependencyGraph? _cachedGraph;
}
```

#### `_dependencies`
- **Type**: `ConcurrentDictionary<string, ConcurrentBag<string>>`
- **Purpose**: Maps each file to its list of dependencies (files it imports)
- **Why `ConcurrentBag`?**: Allows multiple threads to add dependencies to the same file simultaneously without blocking
- **Example**: `"main.spy" → ["utils.spy", "models.spy"]`

#### `_fileHashes`
- **Type**: `ConcurrentDictionary<string, string>`
- **Purpose**: Stores content hashes (e.g., SHA-256) for incremental compilation
- **Future-proofing**: Enables staleness detection for incremental builds
- **Example**: `"main.spy" → "a3f8b2c..."`

#### `_buildLock`
- **Purpose**: Ensures only one thread builds the graph at a time
- **Why needed?**: Even though adding is thread-safe, building requires consistent snapshot

#### `_cachedGraph`
- **Purpose**: Caches the built graph to avoid rebuilding if nothing changed
- **Invalidation**: Set to `null` whenever `AddFile()`, `AddDependency()`, or `SetFileHash()` is called

---

## Key Methods

### 1. `AddFile(string filePath)`

**Purpose**: Registers a file in the dependency graph.

```csharp
public void AddFile(string filePath)
{
    ArgumentNullException.ThrowIfNull(filePath);
    var normalized = NormalizePath(filePath);
    _dependencies.TryAdd(normalized, new ConcurrentBag<string>());
    _cachedGraph = null; // Invalidate cache
}
```

**Key Details:**
- **Thread-safe**: Multiple threads can register files simultaneously
- **Idempotent**: Calling twice with same file does nothing (thanks to `TryAdd`)
- **Auto-registration**: Files are automatically registered when used in `AddDependency`, so this is often optional
- **Cache invalidation**: Any modification invalidates the cached graph

**When to use:**
- When you know all files upfront before processing dependencies
- Optional if you're using `AddDependency` (which auto-registers both files)

---

### 2. `AddDependency(string fromFile, string toFile)`

**Purpose**: Records that `fromFile` imports `toFile`.

```csharp
public void AddDependency(string fromFile, string toFile)
{
    var normalizedFrom = NormalizePath(fromFile);
    var normalizedTo = NormalizePath(toFile);
    
    // Ensure both files exist in the dictionary
    var deps = _dependencies.GetOrAdd(normalizedFrom, _ => new ConcurrentBag<string>());
    _dependencies.TryAdd(normalizedTo, new ConcurrentBag<string>());
    
    deps.Add(normalizedTo);
    _cachedGraph = null;
}
```

**Key Details:**
- **Auto-registration**: Both files are added to `_dependencies` if not already present
- **Allows duplicates**: The same dependency can be added multiple times; they're deduplicated during `Build()`
- **Target gets empty bag**: Even if `toFile` has no dependencies, it gets registered with an empty bag

**Usage Pattern:**
```csharp
// During import resolution:
// When parser sees: from utils import helper
builder.AddDependency("main.spy", "utils.spy");
```

**Why allow duplicates?**
- Simplifies parallel processing—multiple threads don't need to check "have we seen this before?"
- Deduplication happens once at build time using `ImmutableHashSet`

---

### 3. `SetFileHash(string filePath, string hash)`

**Purpose**: Stores a content hash for future incremental compilation support.

```csharp
public void SetFileHash(string filePath, string hash)
{
    var normalized = NormalizePath(filePath);
    _fileHashes[normalized] = hash;
    _cachedGraph = null;
}
```

**Key Details:**
- **Optional**: Not required for graph construction
- **Future feature**: Enables incremental compilation by detecting which files changed
- **Overwrites**: If called twice with same file, latest hash wins

**How hashes might be used:**
```csharp
// Before recompiling:
var currentHash = ComputeSHA256(File.ReadAllBytes("main.spy"));
if (graph.IsStale("main.spy", currentHash)) {
    // File changed, need to recompile
}
```

---

### 4. `Build(bool validateTargets = false)`

**Purpose**: Constructs the immutable `DependencyGraph` from accumulated dependencies.

```csharp
public DependencyGraph Build(bool validateTargets = false)
{
    lock (_buildLock)
    {
        // Return cached graph if available
        if (_cachedGraph != null) return _cachedGraph;
        
        // 1. Collect all files (sources + targets)
        var allFiles = new HashSet<string>();
        foreach (var (file, deps) in _dependencies)
        {
            allFiles.Add(file);
            foreach (var dep in deps) allFiles.Add(dep);
        }
        
        // 2. Optionally validate all targets exist
        if (validateTargets) { /* ... */ }
        
        // 3. Deduplicate dependencies
        var fileDependencies = new Dictionary<string, ImmutableHashSet<string>>();
        foreach (var (file, deps) in _dependencies)
        {
            fileDependencies[file] = deps.ToImmutableHashSet();
        }
        
        // 4. Ensure all target files have entries
        foreach (var file in allFiles)
        {
            if (!fileDependencies.ContainsKey(file))
            {
                fileDependencies[file] = ImmutableHashSet<string>.Empty;
            }
        }
        
        // 5. Build file hashes dictionary
        Dictionary<string, string>? hashes = null;
        if (!_fileHashes.IsEmpty)
        {
            hashes = new Dictionary<string, string>(_fileHashes);
        }
        
        // 6. Create and cache the graph
        _cachedGraph = new DependencyGraph(fileDependencies, hashes);
        return _cachedGraph;
    }
}
```

**Algorithm Breakdown:**

1. **Collection Phase**:
   - Gathers all files mentioned in the dependency map
   - Includes both "source" files (have dependencies) and "target" files (are dependencies)

2. **Validation Phase** (if `validateTargets = true`):
   - Checks that every dependency target exists as a registered file
   - **Note**: This check is usually redundant since `AddDependency` auto-registers targets
   - Mainly useful for catching logic errors during development

3. **Deduplication Phase**:
   - Converts each `ConcurrentBag<string>` to `ImmutableHashSet<string>`
   - This removes duplicate dependencies added during parallel processing
   - Example: `["utils.spy", "utils.spy", "models.spy"]` → `{"utils.spy", "models.spy"}`

4. **Completeness Phase**:
   - Ensures *every* file in the graph has an entry, even if it has no dependencies
   - Example: If `utils.spy` is imported but imports nothing, it still gets an entry: `"utils.spy" → {}`

5. **Hash Transfer Phase**:
   - Copies file hashes from `ConcurrentDictionary` to regular `Dictionary`
   - Only creates the dictionary if hashes were set (saves memory otherwise)

6. **Caching**:
   - Stores result in `_cachedGraph`
   - Subsequent calls return the same instance (until something changes)

**Thread Safety:**
- Uses `lock (_buildLock)` to ensure only one thread builds at a time
- Multiple threads can safely call `Build()` simultaneously—first one builds, others wait and get cached result

**Performance:**
- **First call**: O(n + e) where n = files, e = edges (dependencies)
- **Subsequent calls**: O(1) if nothing changed (returns cached graph)
- **After modification**: Cache invalidated, next `Build()` rebuilds

**Return Value:**
- Always returns the same `DependencyGraph` instance until builder state changes
- Can continue adding dependencies and call `Build()` again for updated graph

---

### 5. `Clear()`

**Purpose**: Resets the builder to empty state.

```csharp
public void Clear()
{
    _dependencies.Clear();
    _fileHashes.Clear();
    _cachedGraph = null;
}
```

**Use Cases:**
- Reusing the same builder instance for multiple projects
- Clearing state after a build failure

---

### 6. `NormalizePath(string path)` (Private)

**Purpose**: Cross-platform path normalization for consistent comparison.

```csharp
private static string NormalizePath(string path)
{
    // Normalize directory separators to forward slash
    var normalized = path.Replace('\\', '/');
    
    // Normalize to lowercase on case-insensitive systems
    if (!OperatingSystem.IsLinux())
    {
        normalized = normalized.ToLowerInvariant();
    }
    
    return normalized;
}
```

**Why This Matters:**
- **Windows/macOS**: Filesystems are case-insensitive (`Main.spy` === `main.spy`)
- **Linux**: Filesystems are case-sensitive (`Main.spy` ≠ `main.spy`)
- **Path separators**: Windows uses `\`, Unix uses `/`

**Examples:**
```csharp
// Windows/macOS:
NormalizePath("src\\Main.SPY") → "src/main.spy"
NormalizePath("src/Main.SPY") → "src/main.spy"

// Linux:
NormalizePath("src\\Main.SPY") → "src/Main.SPY"  // preserves case
NormalizePath("src/Main.SPY") → "src/Main.SPY"
```

**Impact:**
- Prevents duplicate entries like `{"src/main.spy", "src\\main.spy", "src/Main.spy"}`
- Ensures consistent lookups across platforms

---

## Dependencies

### Upstream Dependencies (What This Uses)

| Type | From | Purpose |
|------|------|---------|
| `DependencyGraph` | `Sharpy.Compiler.Project` | The immutable graph this builder creates |
| `ConcurrentDictionary` | `System.Collections.Concurrent` | Thread-safe dependency storage |
| `ConcurrentBag` | `System.Collections.Concurrent` | Thread-safe dependency list per file |
| `ImmutableHashSet` | `System.Collections.Immutable` | Immutable sets in the final graph |

### Downstream Dependencies (What Uses This)

| Component | Usage |
|-----------|-------|
| `ProjectCompiler` | Creates builder in Phase 4 (import resolution) |
| `ImportResolver` | Calls `AddDependency()` as imports are discovered |
| Parallel import resolution | Multiple threads add dependencies simultaneously |

---

## Patterns and Design Decisions

### 1. **Builder Pattern**
- **Separates construction from representation**: Mutable builder → immutable product
- **Benefits**:
  - Thread-safe accumulation phase
  - Clean immutable graph for queries
  - Can rebuild multiple times with different states

### 2. **Thread Safety Strategy**

**Two-Phase Concurrency:**

**Phase 1: Accumulation (lock-free)**
```csharp
// Multiple threads can add concurrently
Parallel.ForEach(files, file => {
    builder.AddDependency(file, dependency);
});
```
- Uses `ConcurrentDictionary` and `ConcurrentBag` for lock-free adds
- High throughput during parallel import resolution

**Phase 2: Building (locked)**
```csharp
// Only one thread builds at a time
lock (_buildLock) {
    if (_cachedGraph != null) return _cachedGraph;
    // ... build logic ...
}
```
- Locks only during snapshot/conversion phase
- Caches result to avoid repeated builds

**Why This Design?**
- **Writes are common** (adding dependencies): lock-free
- **Reads are rare** (building): locked is fine
- Optimizes for the common case (parallel import resolution)

### 3. **Cache Invalidation**

Every mutation invalidates the cache:
```csharp
_cachedGraph = null;  // Clear cache
```

**Why Invalidate Eagerly?**
- Simple: No need to track "dirty" state
- Correct: Ensures `Build()` always reflects current state
- Efficient: Rebuilding is cheap (O(n+e) where n, e are typically small)

### 4. **Defensive Copying**

When building:
```csharp
// Copy hashes to regular dictionary
if (!_fileHashes.IsEmpty)
{
    hashes = new Dictionary<string, string>(_fileHashes);
}
```

**Why Copy?**
- Graph is immutable, but builder is mutable
- Prevents builder modifications from affecting existing graphs
- Isolates the graph from future changes

### 5. **Auto-Registration Design**

`AddDependency` auto-registers both files:
```csharp
var deps = _dependencies.GetOrAdd(normalizedFrom, _ => new ConcurrentBag<string>());
_dependencies.TryAdd(normalizedTo, new ConcurrentBag<string>());
```

**Trade-offs:**
- ✅ **Pro**: Simpler API—no need to call `AddFile()` separately
- ✅ **Pro**: Thread-safe—multiple threads can "discover" same file
- ❌ **Con**: Can't distinguish "this file exists" from "this file is mentioned"
- ❌ **Con**: Validation is less useful (targets are auto-registered)

**Design Choice Rationale:**
- Import resolution naturally discovers files as they're referenced
- Parallel processing means multiple threads might discover same file
- Auto-registration eliminates synchronization points

---

## Debugging Tips

### 1. **Inspecting Builder State**

```csharp
// Check file count
Console.WriteLine($"Files tracked: {builder.FileCount}");

// Build and inspect
var graph = builder.Build();
Console.WriteLine($"Total files: {graph.AllFiles.Count}");
Console.WriteLine($"Files: {string.Join(", ", graph.AllFiles)}");

// Check specific file's dependencies
var deps = graph.GetDirectDependencies("main.spy");
Console.WriteLine($"main.spy imports: {string.Join(", ", deps)}");
```

### 2. **Catching Duplicate Adds**

If you suspect duplicate dependencies are causing issues:

```csharp
// Before building
var builder = new DependencyGraphBuilder();
builder.AddDependency("a.spy", "b.spy");
builder.AddDependency("a.spy", "b.spy");  // Duplicate

// Check in tests
var graph = builder.Build();
var deps = graph.GetDirectDependencies("a.spy");
Assert.Equal(1, deps.Count);  // Should be deduplicated
```

### 3. **Path Normalization Issues**

If dependencies aren't linking correctly:

```csharp
// Test path normalization
var builder = new DependencyGraphBuilder();
builder.AddDependency("src/main.spy", "src/utils.spy");

var graph = builder.Build();

// Try different path formats
Console.WriteLine(graph.GetDirectDependencies("src\\main.spy"));  // Should work
Console.WriteLine(graph.GetDirectDependencies("SRC/MAIN.SPY"));   // Works on Windows/Mac
```

### 4. **Thread Safety Issues**

If you suspect race conditions:

```csharp
// Stress test with lots of parallel adds
var builder = new DependencyGraphBuilder();

Parallel.For(0, 10000, i => {
    builder.AddDependency($"src{i}.spy", "common.spy");
});

var graph = builder.Build();
Console.WriteLine($"Expected: 10001, Got: {graph.AllFiles.Count}");
```

### 5. **Build Caching Behavior**

```csharp
var builder = new DependencyGraphBuilder();
builder.AddDependency("a.spy", "b.spy");

var graph1 = builder.Build();
var graph2 = builder.Build();
Console.WriteLine($"Same instance? {ReferenceEquals(graph1, graph2)}");  // True

builder.AddDependency("c.spy", "d.spy");
var graph3 = builder.Build();
Console.WriteLine($"Still same? {ReferenceEquals(graph1, graph3)}");  // False
```

### 6. **Common Error: Null Arguments**

```csharp
// All throw ArgumentNullException
builder.AddFile(null!);
builder.AddDependency(null!, "b.spy");
builder.AddDependency("a.spy", null!);
builder.SetFileHash(null!, "hash");
builder.SetFileHash("a.spy", null!);
```

### 7. **Debugging Build Order**

```csharp
var graph = builder.Build();
var buildOrder = graph.GetBuildOrder();

Console.WriteLine("Build order:");
for (int i = 0; i < buildOrder.Count; i++)
{
    var file = buildOrder[i];
    var deps = graph.GetDirectDependencies(file);
    Console.WriteLine($"  {i}: {file} (depends on: {string.Join(", ", deps)})");
}
```

---

## Contribution Guidelines

### When to Modify This File

1. **Adding new metadata**: If you need to track additional information per file (like timestamps, file sizes, etc.)
2. **Performance optimization**: If profiling shows builder is a bottleneck
3. **New validation rules**: If you need stricter validation than current `validateTargets` option
4. **Concurrency improvements**: If you find thread-safety issues or contention

### What NOT to Change

❌ **Don't change path normalization logic** without considering:
- Cross-platform compatibility (Windows, macOS, Linux)
- Existing projects that might break
- Need comprehensive tests on all platforms

❌ **Don't remove thread safety** even if "we don't use parallel compilation":
- Future features may need it
- Tests rely on it
- Removing concurrency primitives can introduce subtle bugs

❌ **Don't change the immutability of `DependencyGraph`**:
- Many parts of compiler assume graphs don't change after creation
- Immutability enables safe sharing across threads

### Adding New Features

#### Example: Adding Timestamps

```csharp
// 1. Add storage
private readonly ConcurrentDictionary<string, DateTime> _fileTimestamps = new();

// 2. Add setter method
public void SetFileTimestamp(string filePath, DateTime timestamp)
{
    var normalized = NormalizePath(filePath);
    _fileTimestamps[normalized] = timestamp;
    _cachedGraph = null;
}

// 3. Pass to DependencyGraph in Build()
var timestamps = !_fileTimestamps.IsEmpty 
    ? new Dictionary<string, DateTime>(_fileTimestamps)
    : null;
    
_cachedGraph = new DependencyGraph(fileDependencies, hashes, timestamps);
```

#### Example: Adding Validation

```csharp
public DependencyGraph Build(bool validateTargets = false, bool validateNoCycles = false)
{
    lock (_buildLock)
    {
        // ... existing build logic ...
        
        if (validateNoCycles)
        {
            var cycles = _cachedGraph.DetectCycles();
            if (cycles.Count > 0)
            {
                var cycleDescriptions = cycles.Select(c => string.Join(" → ", c));
                throw new InvalidOperationException(
                    $"Circular dependencies detected:\n{string.Join("\n", cycleDescriptions)}");
            }
        }
        
        return _cachedGraph;
    }
}
```

### Testing Requirements

When modifying this file, ensure:

1. **Thread safety tests pass**: See `DependencyGraphBuilderTests.cs` → `#region Thread Safety Tests`
2. **Path normalization works**: Test on Windows, macOS, and Linux
3. **Cache invalidation works**: Verify `Build()` returns new graph after modifications
4. **Backward compatibility**: Existing code should still work

### Performance Considerations

**Current Performance Characteristics:**
- `AddFile()`: O(1) amortized (concurrent dictionary add)
- `AddDependency()`: O(1) amortized (concurrent bag add)
- `Build()`: O(n + e) where n = files, e = edges
- `Build()` (cached): O(1)

**If You Need Better Performance:**

1. **Profile first**: Use `dotnet-trace` or BenchmarkDotNet
2. **Identify bottleneck**:
   - Is `Build()` called too often? → Cache more aggressively
   - Is adding dependencies slow? → Already optimized with concurrent collections
   - Is deduplication slow? → Consider using `ConcurrentHashSet` (though it doesn't exist in BCL)

3. **Measure impact**: Performance changes must be benchmarked

---

## Cross-References

### Related Documentation

- **[DependencyGraph.md](./DependencyGraph.md)**: The immutable graph produced by this builder
- **[ProjectCompiler.md](./ProjectCompiler.md)**: How the builder is used in the compilation pipeline
- **[SpyProject.md](./SpyProject.md)**: Project configuration that feeds into compilation

### Related Source Files

- **`DependencyGraph.cs`**: The immutable product of this builder
- **`ProjectCompiler.cs`**: Creates and uses this builder during Phase 4 (import resolution)
- **`../Semantic/ImportResolver.cs`**: Calls `AddDependency()` as imports are discovered

### Test Files

- **`Sharpy.Compiler.Tests/Project/DependencyGraphBuilderTests.cs`**: Comprehensive unit tests
- **`Sharpy.Compiler.Tests/Semantic/ImportResolverDependencyTests.cs`**: Integration tests with import resolution

---

## Example Usage Scenarios

### Scenario 1: Simple Linear Dependencies

```csharp
// Project structure:
// main.spy imports utils.spy
// utils.spy imports core.spy
// core.spy has no dependencies

var builder = new DependencyGraphBuilder();
builder.AddDependency("main.spy", "utils.spy");
builder.AddDependency("utils.spy", "core.spy");

var graph = builder.Build();

// Build order: [core.spy, utils.spy, main.spy]
var order = graph.GetBuildOrder();
```

### Scenario 2: Parallel Import Resolution

```csharp
var builder = new DependencyGraphBuilder();
var files = new[] { "a.spy", "b.spy", "c.spy", "d.spy" };

// Parse and resolve imports in parallel
Parallel.ForEach(files, file =>
{
    var imports = ParseAndGetImports(file);
    foreach (var import in imports)
    {
        builder.AddDependency(file, import);
    }
});

var graph = builder.Build();
```

### Scenario 3: Incremental Compilation (Future)

```csharp
var builder = new DependencyGraphBuilder();

// Initial build
foreach (var file in allFiles)
{
    builder.AddFile(file);
    var hash = ComputeHash(file);
    builder.SetFileHash(file, hash);
}

var graph = builder.Build();

// Later, check what needs recompilation
var changedFiles = DetectChangedFiles(graph);
var affectedFiles = graph.GetAffectedFiles(changedFiles);

// Only recompile affected files
foreach (var file in affectedFiles)
{
    Recompile(file);
}
```

### Scenario 4: Cycle Detection

```csharp
var builder = new DependencyGraphBuilder();
builder.AddDependency("a.spy", "b.spy");
builder.AddDependency("b.spy", "c.spy");
builder.AddDependency("c.spy", "a.spy");  // Cycle!

var graph = builder.Build();
var cycles = graph.DetectCycles();

if (cycles.Count > 0)
{
    foreach (var cycle in cycles)
    {
        Console.WriteLine($"Circular import: {string.Join(" → ", cycle)}");
    }
}
```

---

## Summary

`DependencyGraphBuilder` is the **thread-safe entry point** for building dependency graphs during Sharpy compilation. It excels at:

✅ **Parallel accumulation** of dependencies during import resolution  
✅ **Deduplication** of repeated dependencies  
✅ **Cross-platform** path normalization  
✅ **Performance** through caching  
✅ **Safety** through immutable output

**Mental Model**: Think of it as a concurrent shopping cart that collects file dependencies as you discover them, then checks out into a permanent, unchangeable receipt (the `DependencyGraph`) when you're ready to analyze it.

**Key Takeaway**: This builder sacrifices a bit of memory (allowing duplicate adds, concurrent bags) for massive gains in parallelism and simplicity during the chaotic import resolution phase.
