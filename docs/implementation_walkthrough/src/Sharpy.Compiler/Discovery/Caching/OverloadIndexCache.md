# Walkthrough: OverloadIndexCache.cs

**Source File**: `src/Sharpy.Compiler/Discovery/Caching/OverloadIndexCache.cs`

---

## Overview

`OverloadIndexCache` is a **persistent disk cache** for function overload metadata extracted from .NET assemblies. When Sharpy compiles code that imports .NET libraries (like `System.Collections.Generic`), it needs to discover all available function overloads to perform proper type checking and code generation. Scanning assemblies via reflection is expensive, so this cache stores the discovered overload information to disk, dramatically speeding up subsequent compilations.

### Why This Matters

Without caching, every compilation would require:
1. Loading assemblies via reflection
2. Scanning all types for callable methods
3. Extracting parameter types, return types, and generic constraints

With caching, this expensive process happens once per assembly version, then subsequent builds simply deserialize the cached data. This is especially important for large frameworks like the .NET BCL.

### Cache Location

- **Default**: `~/.sharpy/cache/overload-index/`
- **Custom**: Can be specified for testing to avoid parallel test conflicts
- **Format**: Gzipped JSON files named like `system.collections.generic-8.0.0-a1b2c3d4e5f6.json.gz`

---

## Class/Type Structure

### Primary Class: `OverloadIndexCache`

```csharp
public class OverloadIndexCache
{
    private readonly string _cacheDirectory;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 100;
}
```

**Purpose**: Manages loading, saving, and maintenance of cached overload indexes.

**Key Characteristics**:
- **Thread-safe**: Handles concurrent access from multiple compiler processes
- **Resilient**: Retries on file conflicts, gracefully handles corruption
- **Self-cleaning**: Automatically removes stale caches older than 7 days

### Supporting Type: `CacheInfo`

```csharp
public class CacheInfo
{
    public string CacheDirectory { get; set; }
    public int CachedAssemblies { get; set; }
    public long TotalSizeBytes { get; set; }
}
```

**Purpose**: Provides diagnostic information about the cache state (useful for debugging and CLI commands like `sharpy cache info`).

---

## Key Functions/Methods

### 1. Constructor: `OverloadIndexCache(string? cacheDirectory)`

```csharp
public OverloadIndexCache(string? cacheDirectory)
{
    if (cacheDirectory != null)
    {
        _cacheDirectory = cacheDirectory;
    }
    else
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _cacheDirectory = Path.Combine(userHome, ".sharpy", "cache", "overload-index");
    }

    if (!Directory.Exists(_cacheDirectory))
    {
        Directory.CreateDirectory(_cacheDirectory);
    }
}
```

**What It Does**:
- Sets up the cache directory location (custom or default `~/.sharpy/cache/overload-index/`)
- Creates the directory if it doesn't exist
- Ensures the cache is ready to use

**Parameters**:
- `cacheDirectory`: Optional custom path (used primarily in tests)

**Design Decision**: The parameterless constructor `OverloadIndexCache()` delegates to this constructor with `null`, following the telescoping constructor pattern.

---

### 2. Loading: `TryLoad(AssemblyIdentity identity)`

```csharp
public OverloadIndex? TryLoad(AssemblyIdentity identity)
```

**What It Does**:
This is the **main read operation**. It attempts to load a previously cached overload index for a specific assembly.

**Algorithm**:
1. **Generate cache key** from the assembly identity (name + version + content hash)
2. **Check if cache file exists** - return `null` immediately if not
3. **Retry loop** (up to 3 attempts):
   - Open the `.json.gz` file with `FileShare.Read` (allows concurrent reads)
   - Decompress via `GZipStream`
   - Deserialize using `System.Text.Json`
   - **Verify identity matches** - ensures cache hasn't been corrupted or mixed up
   - If identity doesn't match, delete the stale cache
4. **Error handling**:
   - `IOException`: Another process has the file locked → retry with exponential backoff
   - Other exceptions: Corrupted cache → delete it and return `null`

**Return Value**:
- `OverloadIndex?`: The cached index if found and valid, otherwise `null`

**Thread Safety**: Uses `FileShare.Read` to allow multiple processes to read simultaneously.

**Key Implementation Detail** (lines 79-86):
```csharp
// Verify the identity matches
if (index?.Identity.Equals(identity) == true)
{
    return index;
}

// Cache is stale, delete it
TryDeleteFile(cachePath);
return null;
```

This verification step is critical - it ensures that if an assembly is updated (new version or modified content), the old cache is automatically invalidated.

---

### 3. Saving: `Save(OverloadIndex index)`

```csharp
public void Save(OverloadIndex index)
```

**What It Does**:
This is the **main write operation**. It persists an overload index to disk using an **atomic write pattern**.

**Algorithm**:
1. **Generate cache key** and paths:
   - Final path: `{cacheDir}/{name}-{version}-{hash}.json.gz`
   - Temp path: `{finalPath}.{guid}.tmp` (e.g., `system.collections-8.0.0-abc123.json.gz.a1b2c3d4.tmp`)
2. **Clean up old caches** for this assembly (different versions >7 days old)
3. **Retry loop** (up to 3 attempts):
   - Write to temporary file:
     - Use `FileShare.None` (exclusive lock during write)
     - Compress with `GZipStream` at optimal compression level
     - Serialize with `System.Text.Json`
   - **Atomically move** temp file to final location with `File.Move(overwrite: true)`
4. **Error handling**:
   - `IOException`: File locked → retry with exponential backoff
   - Other exceptions: Delete temp file, log warning, but **don't fail** (caching is optional)

**Atomic Write Pattern** (lines 122-132):
```csharp
// Write to a temporary file first
using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
{
    JsonSerializer.Serialize(gzipStream, index, JsonOptions);
}

// Atomically move the temp file to the final location
File.Move(tempPath, cachePath, overwrite: true);
```

**Why Atomic Writes?**: Writing directly to the final location could leave a corrupted half-written file if the process crashes. The temp-then-rename approach ensures the cache file is always in a valid state.

**Design Philosophy** (lines 145-153): Cache failures are **non-critical** - they only slow down future builds but don't break compilation. Notice the method never throws; it only logs warnings.

---

### 4. Cleanup: `CleanupOldCaches(string assemblyName, string currentCacheKey)`

```csharp
private void CleanupOldCaches(string assemblyName, string currentCacheKey)
```

**What It Does**:
Removes outdated cache files for a specific assembly to prevent unbounded disk usage.

**Algorithm**:
1. Find all cache files matching pattern `{assemblyName}-*.json.gz`
2. For each file (except the current cache key):
   - Check last write time
   - If older than 7 days, delete it
3. Ignore errors during cleanup

**Example Scenario**:
```
System.Collections.Generic v8.0.0 → cached
System.Collections.Generic v8.0.1 → cached (triggers cleanup)
System.Collections.Generic v8.0.0 cache → deleted (if >7 days old)
```

**Why 7 Days?**: Balances disk usage with the likelihood of switching between assembly versions during active development.

---

### 5. Diagnostics: `GetInfo()` and `ClearAll()`

```csharp
public CacheInfo GetInfo()
public void ClearAll()
```

**`GetInfo()`**:
- Returns statistics about the cache (directory, file count, total size)
- Used for CLI commands like `sharpy cache info`

**`ClearAll()`**:
- Deletes all `*.json.gz` files in the cache directory
- Used for CLI commands like `sharpy cache clear`
- Continues even if some deletions fail

---

## Dependencies

### Internal Dependencies

1. **`AssemblyIdentity`** (`AssemblyIdentity.cs`):
   - Uniquely identifies assemblies using name + version + SHA256 hash
   - Provides `ToCacheKey()` method to generate cache file names
   - Example: `system.collections.generic-8.0.0-a1b2c3d4e5f6.json.gz`

2. **`OverloadIndex`** (`OverloadIndex.cs`):
   - The data structure being cached
   - Contains:
     - `AssemblyIdentity` for validation
     - `Dictionary<string, ModuleOverloads>` mapping module names to overloads
     - `FunctionSignature`, `ParameterSignature`, `TypeSignature` metadata

3. **`CachedModuleDiscovery`** (`CachedModuleDiscovery.cs`):
   - Primary consumer of this cache
   - Flow: Check cache → if miss, scan assembly → save to cache

### External Dependencies

- **`System.IO.Compression`**: GZip compression (reduces cache files by ~80%)
- **`System.Text.Json`**: Fast, modern JSON serialization
- **`System.IO`**: File operations with retry logic

---

## Patterns and Design Decisions

### 1. **Atomic Writes via Temp Files**

**Pattern**: Write-to-temp-then-rename
```csharp
var tempPath = cachePath + $".{Guid.NewGuid():N}.tmp";
// ... write to tempPath ...
File.Move(tempPath, cachePath, overwrite: true);
```

**Why**: Prevents corrupted cache files if the process crashes mid-write. The rename operation is atomic on most filesystems.

### 2. **Retry Logic with Exponential Backoff**

**Pattern**: Retry loop with increasing delays
```csharp
for (var attempt = 0; attempt < MaxRetries; attempt++)
{
    try { /* operation */ }
    catch (IOException) when (attempt < MaxRetries - 1)
    {
        Thread.Sleep(RetryDelayMs * (attempt + 1)); // 100ms, 200ms, 300ms
    }
}
```

**Why**: Multiple compiler processes may access the cache simultaneously (e.g., parallel builds). Retries with backoff handle transient file locks gracefully.

### 3. **Graceful Degradation**

**Philosophy**: Cache failures are warnings, not errors.

```csharp
catch (Exception ex)
{
    System.Diagnostics.Debug.WriteLine($"Warning: Failed to save cache...");
    // Don't throw - compilation continues without cache
}
```

**Why**: The cache is a performance optimization. If it fails, compilation should still succeed (just slower).

### 4. **JSON Configuration for Size Optimization**

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = false,              // Compact JSON
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,  // Smaller keys
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull  // Omit nulls
};
```

Combined with GZip compression, this reduces cache files significantly (e.g., 500KB → 80KB).

### 5. **Content-Based Cache Keys**

**Pattern**: Include content hash in cache key
```
system.collections.generic-8.0.0-a1b2c3d4e5f6.json.gz
                             ↑                ↑
                          version      SHA256 prefix
```

**Why**: Detects when an assembly file changes without version bump (e.g., during local development or patching).

---

## Debugging Tips

### Problem: Cache Not Being Used

**Check**:
1. Verify cache directory exists: `ls ~/.sharpy/cache/overload-index/`
2. Check for cache files: `ls -lh ~/.sharpy/cache/overload-index/*.json.gz`
3. Enable verbose logging to see cache hits/misses

**Common Cause**: Assembly hash changed (assembly was recompiled), causing cache key mismatch.

### Problem: Corrupted Cache Files

**Symptoms**:
- Deserialization exceptions in `TryLoad()`
- Frequent cache rebuilds

**Solution**:
```bash
sharpy cache clear  # Clear all caches
```

**Root Causes**:
- Incomplete writes (process crash during `Save()`)
- Format version mismatch (upgrading Sharpy version)
- Disk corruption

**Prevention**: The atomic write pattern prevents most corruption, but manual cleanup may be needed if the process is killed mid-write.

### Problem: Disk Space Issues

**Check Cache Size**:
```bash
du -sh ~/.sharpy/cache/overload-index/
```

**Solution**:
```bash
sharpy cache clear  # Or manually delete old files
```

**Auto-Cleanup**: The 7-day cleanup in `CleanupOldCaches()` should prevent unbounded growth, but rapid assembly version changes can accumulate files.

### Debugging Cache Logic

**Add Logging**:
```csharp
// In TryLoad():
System.Diagnostics.Debug.WriteLine($"Cache hit for {identity.Name}");
System.Diagnostics.Debug.WriteLine($"Cache miss for {identity.Name}");

// In Save():
System.Diagnostics.Debug.WriteLine($"Saved cache: {cachePath} ({new FileInfo(cachePath).Length} bytes)");
```

**Test Cache Behavior**:
```csharp
var cache = new OverloadIndexCache("/tmp/test-cache");  // Custom directory
var identity = AssemblyIdentity.FromAssemblyPath("test.dll");
var index = new OverloadIndex { Identity = identity };

cache.Save(index);
var loaded = cache.TryLoad(identity);
Assert.NotNull(loaded);
```

---

## Contribution Guidelines

### When to Modify This File

1. **Changing Cache Format**:
   - Increment `CacheFormatVersion` in `OverloadIndex`
   - Add migration logic or force cache invalidation
   - Update `JsonOptions` if changing serialization strategy

2. **Improving Performance**:
   - Experiment with different compression levels (currently `CompressionLevel.Optimal`)
   - Consider async I/O for large cache files
   - Profile serialization performance

3. **Enhancing Reliability**:
   - Adjust retry counts/delays based on real-world contention patterns
   - Add cache integrity checks (checksums)
   - Improve error messages for diagnostics

4. **Adding Cache Management Features**:
   - Implement cache size limits
   - Add LRU eviction policy
   - Provide cache statistics (hit rate, etc.)

### Making Changes Safely

#### Testing Your Changes

**Unit Tests** (create if needed):
```csharp
[Fact]
public void TestCacheSaveAndLoad()
{
    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    var cache = new OverloadIndexCache(tempDir);
    var identity = new AssemblyIdentity { Name = "Test", Version = "1.0.0", ContentHash = "abc123" };
    var index = new OverloadIndex { Identity = identity };

    cache.Save(index);
    var loaded = cache.TryLoad(identity);

    Assert.NotNull(loaded);
    Assert.Equal(identity.Name, loaded.Identity.Name);
}
```

**Integration Tests**:
- Compile a Sharpy program that imports .NET assemblies
- Verify cache files are created
- Delete cache and verify rebuilding works
- Run parallel compilations to test concurrency

#### Performance Considerations

- **Compression**: Balance CPU time vs disk I/O (Optimal is usually best)
- **Serialization**: Avoid reflection-based serialization for large objects
- **File I/O**: Consider async I/O if cache files exceed ~1MB

#### Backward Compatibility

When changing the cache format:
1. Increment `CacheFormatVersion`
2. Update `TryLoad()` to handle version mismatches:
```csharp
if (index.CacheFormatVersion != 2)  // New version
{
    TryDeleteFile(cachePath);  // Invalidate old cache
    return null;
}
```

### Common Contribution Scenarios

#### Adding a New Cache Statistic

Example: Track cache hit/miss rates

```csharp
public class CacheInfo
{
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    // ...
}

// In TryLoad():
if (index != null)
{
    Interlocked.Increment(ref _cacheHits);  // Thread-safe
}
else
{
    Interlocked.Increment(ref _cacheMisses);
}
```

#### Implementing Cache Eviction

Example: Limit total cache size to 100MB

```csharp
private void EnforceSizeLimit()
{
    var files = Directory.GetFiles(_cacheDirectory, "*.json.gz")
        .Select(f => new FileInfo(f))
        .OrderBy(f => f.LastAccessTime)
        .ToList();

    var totalSize = files.Sum(f => f.Length);
    if (totalSize > 100_000_000)  // 100MB
    {
        foreach (var file in files)
        {
            file.Delete();
            totalSize -= file.Length;
            if (totalSize <= 100_000_000)
                break;
        }
    }
}
```

#### Adding Async Support

Example: Make `TryLoad()` asynchronous for large files

```csharp
public async Task<OverloadIndex?> TryLoadAsync(AssemblyIdentity identity)
{
    var cachePath = GetCachePath(identity);
    if (!File.Exists(cachePath))
        return null;

    await using var fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
    await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
    return await JsonSerializer.DeserializeAsync<OverloadIndex>(gzipStream, JsonOptions);
}
```

---

## Related Files

**Must Read Together**:
- `AssemblyIdentity.cs`: Defines how assemblies are uniquely identified
- `OverloadIndex.cs`: The data structure being cached
- `OverloadIndexBuilder.cs`: Creates indexes by scanning assemblies
- `CachedModuleDiscovery.cs`: Orchestrates cache reads/writes during compilation

**Workflow**:
```
CachedModuleDiscovery.Discover(assembly)
  ↓
  ├─ cache.TryLoad(identity)  ← YOU ARE HERE
  │   └─ Hit? Return cached index
  ↓
  ├─ Miss? OverloadIndexBuilder.Build(assembly)
  │   └─ Scan assembly via reflection
  ↓
  └─ cache.Save(newIndex)  ← YOU ARE HERE
```

---

## Quick Reference

### Key Methods

| Method | Purpose | Thread-Safe? |
|--------|---------|--------------|
| `TryLoad()` | Load cache (retry on lock) | ✅ Yes (read sharing) |
| `Save()` | Save cache (atomic write) | ✅ Yes (temp-then-rename) |
| `ClearAll()` | Delete all caches | ✅ Yes (continues on errors) |
| `GetInfo()` | Cache diagnostics | ✅ Yes (read-only) |
| `CleanupOldCaches()` | Remove stale entries | ⚠️ Called during `Save()` |

### Cache File Format

```
system.collections.generic-8.0.0-a1b2c3d4e5f6.json.gz
└─────┬────────────────┘ └─┬──┘ └──────┬───────┘└─┬──┘
   Assembly name        Version  Hash (12 chars) Extension
```

### Default Paths

- **macOS/Linux**: `~/.sharpy/cache/overload-index/`
- **Windows**: `C:\Users\{Username}\.sharpy\cache\overload-index\`

---

**Last Updated**: December 2024  
**Maintainer**: Sharpy Compiler Team
