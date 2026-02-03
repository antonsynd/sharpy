# Tier 5 Compiler Hardening Implementation Plan

## Status Summary

| Item | Status | Action |
|------|--------|--------|
| 5.1 | Infrastructure only | **Full Implementation** |
| 5.2 | ✅ Complete | None - dead code already cleaned |
| 5.3 | Mostly complete | Complete 5.3d + 5.3b partial |
| 5.4 | ✅ Complete | None - 15 determinism tests exist |
| 5.5 | ✅ Complete | None - deduplication implemented |
| 5.6 | ✅ Complete | None - 85 DunderNames constants |

---

## Implementation Order

1. **5.3d** - CI Benchmark Workflow (small, no dependencies)
2. **5.3b** - Semantic/CodeGen Isolation Benchmarks (medium)
3. **5.1** - Full Incremental Compilation (significant)

---

## 1. CI Benchmark Workflow (5.3d)

**Files to create:**
- `.github/workflows/benchmarks.yml`

**Implementation:**
```yaml
name: Benchmarks
on:
  pull_request:
    branches: [mainline]
    paths: ['src/Sharpy.Compiler/**', 'src/Sharpy.Core/**']
  workflow_dispatch:

jobs:
  benchmark:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      pull-requests: write
    steps:
    - uses: actions/checkout@v6
    - uses: actions/setup-dotnet@v5
      with: { dotnet-version: 10.0.x }
    - run: dotnet restore && dotnet build -c Release --no-restore
    - run: |
        dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release --no-build -- \
          --filter "*CompilerBenchmarks*" --exporters json markdown --job short
    - uses: actions/upload-artifact@v4
      with: { name: benchmark-results, path: BenchmarkDotNet.Artifacts/ }
    # Comment PR with results using github-script
```

**Commit:** `ci(5.3d): add benchmark workflow for PRs`

---

## 2. Semantic/CodeGen Isolation Benchmarks (5.3b)

**Files to modify:**
- `src/Sharpy.Compiler.Benchmarks/CompilerBenchmarks.cs`

**Add classes:**

### SemanticBenchmarks
```csharp
[MemoryDiagnoser]
public class SemanticBenchmarks
{
    private Module _largeCorpusModule = null!;  // Pre-parsed

    [GlobalSetup]
    public void Setup() { /* Lex + parse corpus files */ }

    [Benchmark(Description = "Semantic: Large Corpus (~476 lines)")]
    public SemanticInfo Analyze_LargeCorpus()
    {
        // NameResolver → TypeResolver → TypeChecker (no codegen)
    }
}
```

### CodeGenBenchmarks
```csharp
[MemoryDiagnoser]
public class CodeGenBenchmarks
{
    private Module _module = null!;           // Pre-parsed
    private SemanticInfo _semanticInfo = null!;  // Pre-analyzed
    private SemanticBinding _binding = null!;

    [GlobalSetup]
    public void Setup() { /* Full pipeline up to codegen */ }

    [Benchmark(Description = "CodeGen: Large Corpus (~476 lines)")]
    public string Generate_LargeCorpus()
    {
        // RoslynEmitter only (pre-computed SemanticInfo)
    }
}
```

**Commit:** `feat(5.3b): add semantic and codegen isolation benchmarks`

---

## 3. Full Incremental Compilation (5.1)

### Architecture Overview

**Current State:**
- `IncrementalCompilationCache` tracks file hashes and detects stale files
- `DependencyGraph.GetAffectedFiles()` finds transitive dependents
- `ProjectCompiler` calls `GetFilesToRecompile()` but ignores result (line 87-93)

**Goal:** Skip all phases for unchanged files by caching/restoring their symbols and generated code.

### Phase-by-Phase Changes

| Phase | Current Behavior | With Incremental |
|-------|------------------|------------------|
| 1: Parse | All files parsed | Skip unchanged, restore cached AST hash |
| 2: Init | Create fresh SymbolTable | Restore cached symbols for unchanged files |
| 3: Collect Types | All files | Skip unchanged (symbols already restored) |
| 4: Imports | All files | Restore cached import symbols |
| 5: Semantic | All files analyzed | Skip unchanged files |
| 6: CodeGen | All files | Restore cached C# for unchanged files |
| 7: Assembly | Compile all C# | Same (Roslyn handles incremental internally) |

### New Files

#### `src/Sharpy.Compiler/Project/SymbolCache.cs`
```csharp
namespace Sharpy.Compiler.Project;

/// <summary>
/// Serializable representation of a symbol for incremental compilation cache.
/// Uses stable IDs to preserve cross-references after deserialization.
/// </summary>
internal record CachedSymbol
{
    public required string Id { get; init; }           // Stable ID: "{file}:{kind}:{name}"
    public required string Kind { get; init; }         // Type, Function, Variable, etc.
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public string? TypeId { get; init; }               // Reference to type symbol
    public string? BaseTypeId { get; init; }           // For TypeSymbol
    public List<string>? InterfaceIds { get; init; }   // For TypeSymbol
    public List<CachedParameter>? Parameters { get; init; } // For FunctionSymbol
    public string? ReturnTypeId { get; init; }         // For FunctionSymbol
    public Dictionary<string, object>? Properties { get; init; }
}

internal record CachedParameter
{
    public required string Name { get; init; }
    public required string TypeId { get; init; }
    public bool HasDefault { get; init; }
}

/// <summary>
/// Cache for a single file's symbols and generated code.
/// </summary>
internal record FileCacheEntry
{
    public required string ContentHash { get; init; }
    public required List<CachedSymbol> Symbols { get; init; }
    public required string GeneratedCSharp { get; init; }
    public required List<string> Dependencies { get; init; }  // Import paths
}
```

#### `src/Sharpy.Compiler/Project/SymbolSerializer.cs`
```csharp
internal static class SymbolSerializer
{
    public static CachedSymbol Serialize(Symbol symbol, string filePath) { ... }
    public static Symbol Deserialize(CachedSymbol cached, Dictionary<string, Symbol> symbolRegistry) { ... }
    public static string ComputeSymbolId(Symbol symbol, string filePath) =>
        $"{NormalizePath(filePath)}:{symbol.Kind}:{symbol.Name}";
}
```

### Modified Files

#### `IncrementalCompilationCache.cs` - Additions

```csharp
// New fields
private Dictionary<string, FileCacheEntry>? _fileCache;
private readonly string _symbolCachePath;  // obj/{Config}/.sharpy-symbols

// New methods
public void SaveFileCache(string filePath, List<Symbol> symbols, string generatedCSharp, List<string> deps)
{
    var entry = new FileCacheEntry
    {
        ContentHash = ComputeFileHash(filePath),
        Symbols = symbols.Select(s => SymbolSerializer.Serialize(s, filePath)).ToList(),
        GeneratedCSharp = generatedCSharp,
        Dependencies = deps
    };
    _fileCache[NormalizePath(filePath)] = entry;
}

public FileCacheEntry? GetFileCache(string filePath) { ... }
public void SaveAllCaches() { /* Save both hash cache and symbol cache */ }
public void LoadAllCaches() { /* Load both caches */ }
```

#### `ProjectCompiler.cs` - Key Changes

```csharp
// Field additions
private HashSet<string> _filesToSkip = new();
private Dictionary<string, Symbol> _restoredSymbols = new();

// In Compile() - after cache init
if (_incremental)
{
    _incrementalCache = new IncrementalCompilationCache(config, _logger);
    _incrementalCache.LoadAllCaches();

    // Determine which files can be skipped
    var changedFiles = _incrementalCache.GetFilesToRecompile(config.SourceFiles, null);
    _filesToSkip = config.SourceFiles.Except(changedFiles).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

// In ParseAllFiles() - skip unchanged files
if (_filesToSkip.Contains(sourceFile))
{
    _logger.LogDebug($"Incremental: skipping parse for {Path.GetFileName(sourceFile)}");

    // Create a placeholder CompilationUnit
    var unit = _projectModel!.CreateUnit(sourceFile, modulePath, "");
    unit.Phase = CompilationPhase.Skipped;

    // Restore cached generated C# for later
    var cached = _incrementalCache!.GetFileCache(sourceFile);
    if (cached != null)
        unit.GeneratedCSharp = cached.GeneratedCSharp;

    _projectMetrics.AddSkippedFile(sourceFile);
    continue;
}

// In InitializeSharedState() - restore cached symbols
if (_incremental && _incrementalCache != null)
{
    foreach (var filePath in _filesToSkip)
    {
        var cached = _incrementalCache.GetFileCache(filePath);
        if (cached != null)
        {
            foreach (var cachedSym in cached.Symbols)
            {
                var symbol = SymbolSerializer.Deserialize(cachedSym, _restoredSymbols);
                _restoredSymbols[cachedSym.Id] = symbol;
                _symbolTable.TryDefine(symbol);
            }
        }
    }
}

// After dependency graph is built - expand affected files
if (_incremental && _dependencyGraph != null)
{
    var directlyChanged = config.SourceFiles.Except(_filesToSkip).ToHashSet();
    var allAffected = _dependencyGraph.GetAffectedFiles(directlyChanged);

    // Remove files from skip list if they're transitively affected
    foreach (var affected in allAffected)
    {
        if (_filesToSkip.Contains(affected))
        {
            _filesToSkip.Remove(affected);
            _logger.LogInfo($"Incremental: {Path.GetFileName(affected)} affected by dependency change");
            // Re-parse this file (it was skipped earlier)
            ReParseFile(affected, config);
        }
    }
}

// In PerformSemanticAnalysis() - skip unchanged files
if (_filesToSkip.Contains(sourceFile))
{
    _logger.LogDebug($"Incremental: skipping semantic analysis for {Path.GetFileName(sourceFile)}");
    unit.Phase = CompilationPhase.TypeChecked;  // Mark as done
    continue;
}

// In GenerateCode() - use cached C# for unchanged files
if (_filesToSkip.Contains(sourceFile))
{
    var cached = _incrementalCache!.GetFileCache(sourceFile);
    if (cached != null)
    {
        unit.GeneratedCSharp = cached.GeneratedCSharp;
        unit.Phase = CompilationPhase.CodeGenerated;
        generatedCSharp[csharpFileName] = cached.GeneratedCSharp;
    }
    continue;
}

// After successful compilation - save caches
if (_incrementalCache != null)
{
    foreach (var (_, unit) in _projectModel!.Units)
    {
        if (unit.Phase == CompilationPhase.CodeGenerated && !_filesToSkip.Contains(unit.FilePath))
        {
            var fileSymbols = ExtractFileSymbols(unit.FilePath);
            var dependencies = ExtractFileDependencies(unit);
            _incrementalCache.SaveFileCache(unit.FilePath, fileSymbols, unit.GeneratedCSharp!, dependencies);
        }
    }
    _incrementalCache.SaveAllCaches();
}
```

### Test Strategy

#### Unit Tests - `IncrementalCompilationCacheTests.cs`
- `SaveAndLoadSymbolCache_RoundTrips`
- `GetFileCache_ReturnsNullForMissingFile`
- `SymbolSerializer_PreservesTypeSymbol`
- `SymbolSerializer_PreservesFunctionSymbol`
- `SymbolSerializer_PreservesCrossReferences`

#### Integration Tests - `ProjectCompilerIncrementalTests.cs`
```csharp
[Fact]
public async Task IncrementalCompile_SkipsUnchangedFiles()
{
    using var helper = new ProjectCompilationHelper(output);
    helper.AddSourceFile("main.spy", "from lib import greet\ngreet()")
          .AddSourceFile("lib.spy", "def greet(): print('Hello')");

    // First compile
    var result1 = helper.Compile(incremental: true);
    Assert.True(result1.Success);
    Assert.Equal(0, result1.Metrics?.SkippedFileCount);

    // Second compile - no changes
    var result2 = helper.Compile(incremental: true);
    Assert.True(result2.Success);
    Assert.Equal(2, result2.Metrics?.SkippedFileCount);
}

[Fact]
public async Task IncrementalCompile_RecompilesAffectedFiles()
{
    // Setup: main.spy imports lib.spy
    // Modify lib.spy
    // Verify: both main.spy and lib.spy are recompiled (not just lib.spy)
}

[Fact]
public async Task IncrementalCompile_PreservesSymbolReferences()
{
    // Verify cross-file type references work after incremental compile
}
```

### Commit Strategy

Split into 5 commits:

1. **Symbol serialization infrastructure:**
   ```
   feat(5.1): add symbol serialization for incremental compilation

   - Add CachedSymbol, FileCacheEntry records
   - Add SymbolSerializer with Serialize/Deserialize
   - Add symbol cache save/load to IncrementalCompilationCache
   ```

2. **Skip parsing for unchanged files:**
   ```
   feat(5.1): skip parsing unchanged files in incremental mode

   - Track _filesToSkip set based on hash comparison
   - Create placeholder CompilationUnits for skipped files
   - Restore cached generated C# for skipped files
   ```

3. **Restore symbols for unchanged files:**
   ```
   feat(5.1): restore cached symbols for unchanged files

   - Deserialize and register symbols in InitializeSharedState()
   - Handle cross-references via symbol registry
   - Preserve reference equality for deserialized symbols
   ```

4. **Dependency-aware recompilation:**
   ```
   feat(5.1): expand affected files after dependency graph is built

   - Use DependencyGraph.GetAffectedFiles() for transitive deps
   - Remove affected files from skip list
   - Re-parse files that were incorrectly skipped
   ```

5. **Tests and finalization:**
   ```
   test(5.1): add incremental compilation tests

   - Add SymbolSerializerTests
   - Add IncrementalCompilationCacheTests
   - Add ProjectCompilerIncrementalTests
   - Update CLAUDE.md with incremental usage docs
   ```

---

## Critical Files Summary

| Component | Path |
|-----------|------|
| ProjectCompiler | `src/Sharpy.Compiler/Project/ProjectCompiler.cs` |
| IncrementalCache | `src/Sharpy.Compiler/Project/IncrementalCompilationCache.cs` |
| SymbolCache (new) | `src/Sharpy.Compiler/Project/SymbolCache.cs` |
| SymbolSerializer (new) | `src/Sharpy.Compiler/Project/SymbolSerializer.cs` |
| DependencyGraph | `src/Sharpy.Compiler/Project/DependencyGraph.cs` |
| Symbol | `src/Sharpy.Compiler/Semantic/Symbol.cs` |
| Benchmarks | `src/Sharpy.Compiler.Benchmarks/CompilerBenchmarks.cs` |
| CI Workflow (new) | `.github/workflows/benchmarks.yml` |

---

## Verification Plan

### 5.3d
- Create test PR → workflow triggers
- Benchmark results appear as comment

### 5.3b
```bash
dotnet run --project src/Sharpy.Compiler.Benchmarks -c Release -- \
  --filter "*SemanticBenchmarks*" --job short
```

### 5.1
```bash
# Create multi-file project
dotnet run --project src/Sharpy.Cli -- project test.spyproj --incremental
# Modify one file
dotnet run --project src/Sharpy.Cli -- project test.spyproj --incremental
# Verify: "Incremental: skipping X file(s)"
# Verify: output is correct
```
